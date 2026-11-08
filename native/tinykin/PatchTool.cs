using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool {
    private const string OriginalHash="32D8F41F78A1EA976330D2250896BA526971B4DFB2EA3E8CE022E192CA1ED886";
    private const string RuntimeFile="TinykinTrainer.Runtime.dll";
    private static string Hash(string file) { using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""); }
    private static MethodDefinition Method(TypeDefinition type,string name,int count) { return type.Methods.Single(m=>m.Name==name&&m.Parameters.Count==count); }
    private static void Insert(MethodDefinition method,Instruction target,Instruction[] instructions,bool redirect) {
        if(redirect)foreach(var existing in method.Body.Instructions.ToArray()) {
            if(existing.Operand==target)existing.Operand=instructions[0];
            var branches=existing.Operand as Instruction[];
            if(branches!=null)for(int i=0;i<branches.Length;i++)if(branches[i]==target)branches[i]=instructions[0];
        }
        foreach(var instruction in instructions)method.Body.GetILProcessor().InsertBefore(target,instruction);
    }
    private static void Prefix(MethodDefinition method,params Instruction[] instructions) { Insert(method,method.Body.Instructions[0],instructions,false); }
    private static void Patch(string original,string output,string runtimeFile) {
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(original));
        using(var assembly=AssemblyDefinition.ReadAssembly(original,new ReaderParameters {AssemblyResolver=resolver}))
        using(var runtime=AssemblyDefinition.ReadAssembly(runtimeFile)) {
            var module=assembly.MainModule;var hooks=runtime.MainModule.GetType("ByteTrainer.Tinykin.Runtime");
            Func<string,int,MethodReference> hook=(name,count)=>module.ImportReference(Method(hooks,name,count));
            foreach(var name in new[]{"Bootstrap","StartupScreen","MainMenu","Game"})Prefix(Method(module.GetType(name),"Awake",0),Instruction.Create(OpCodes.Call,hook("Boot",0)));
            var player=module.GetType("PlayerController");
            foreach(var pair in new[]{new[]{"set_CanJump","JumpAllowed"},new[]{"set_CanAnticipateJump","Anticipate"}}) {
                var setter=Method(player,pair[0],1);
                Prefix(setter,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook(pair[1],2)),Instruction.Create(OpCodes.Starg,setter.Parameters[0]));
            }
            Prefix(Method(player,"Jump",1),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("CountJump",1)));
            var move=Method(player,"get_MoveSpeed",0);
            foreach(var ret in move.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())Insert(move,ret,new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("Movement",2))},true);
            var danger=Method(player,"IsDeadly",1);
            foreach(var ret in danger.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())Insert(danger,ret,new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("Danger",2))},true);
            foreach(var pair in new[]{new[]{"SpendGliderFuel","FreeFuel"},new[]{"StartDrowning","Protect"}}) {
                var method=Method(player,pair[0],1);var entry=method.Body.Instructions[0];
                Prefix(method,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook(pair[1],1)),Instruction.Create(OpCodes.Brfalse,entry),Instruction.Create(OpCodes.Ret));
            }
            var death=Method(player,"Die",2);var deathEntry=death.Body.Instructions[0];
            Prefix(death,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("BlockDeath",2)),Instruction.Create(OpCodes.Brfalse,deathEntry),Instruction.Create(OpCodes.Ret));
            var physics=Method(player,"FixedUpdate",0);
            var writes=physics.Body.Instructions.Where(i=>i.OpCode==OpCodes.Callvirt&&i.Operand is MethodReference&&((MethodReference)i.Operand).FullName=="System.Void UnityEngine.Rigidbody::set_velocity(UnityEngine.Vector3)").ToArray();
            if(writes.Length!=1)throw new InvalidOperationException("Player velocity layout changed.");
            Insert(physics,writes[0],new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("GlideVelocity",2))},true);
            var camera=Method(module.GetType("PlayerCamera"),"SetCameraPositionAndAngles",1);
            var heights=camera.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldfld&&((FieldReference)i.Operand).Name=="currentGroundY"&&i.Next.OpCode==OpCodes.Stfld&&((FieldReference)i.Next.Operand).FullName=="System.Single UnityEngine.Vector3::y").ToArray();
            if(heights.Length!=1)throw new InvalidOperationException("Camera height layout changed.");
            Insert(camera,heights[0].Next,new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("CameraHeight",2))},false);
            var opcodes=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
            foreach(var type in module.GetTypes())foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var i in method.Body.Instructions)if(i.OpCode.OperandType==OperandType.ShortInlineBrTarget)i.OpCode=opcodes[i.OpCode.Name.Substring(0,i.OpCode.Name.Length-2)];
            assembly.Write(output);
        }
    }
    private static void Main(string[] args) {
        try {
            if(args.Length!=2||args[0]!="install")throw new ArgumentException("Use: PatchTool.exe install GAME_FOLDER");
            string dir=Path.Combine(args[1],"Tinykin_Data","Managed"),target=Path.Combine(dir,"Game.dll"),backup=target+".trainerhub-original",marker=target+".trainerhub-sha256";
            string current=Hash(target);bool original=current==OriginalHash;
            if(!original&&(!File.Exists(marker)||File.ReadAllText(marker).Trim()!=current))throw new InvalidOperationException("Game assembly differs from the verified version.");
            if(!File.Exists(backup)&&original)File.Copy(target,backup);
            if(!File.Exists(backup)||Hash(backup)!=OriginalHash)throw new InvalidOperationException("Original backup missing or invalid.");
            string runtime=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,RuntimeFile),staged=target+".new";
            Patch(backup,staged,runtime);File.Copy(staged,target,true);File.Delete(staged);
            foreach(var file in new[]{RuntimeFile,"0Harmony.dll"})File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file),Path.Combine(dir,file),true);
            File.WriteAllText(marker,Hash(target));
        } catch(Exception error) { Console.Error.WriteLine(error);Environment.ExitCode=1; }
    }
}
