using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
internal static class PatchTool  {
    private const string OriginalHash="4D5E474A5AD7D6227DFB7CEF7D09FFA357A19CCF601D65503CFE01C68EAE934C";
    private const string RuntimeFile="OutOfSpaceTrainer.Runtime.dll";
    private static string Hash(string file) {
        using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
    }
    private static MethodDefinition Method(TypeDefinition type,string name,int count) {
        return type.Methods.Single(method=>method.Name==name&&method.Parameters.Count==count);
    }
    private static void Prefix(MethodDefinition method,params Instruction[] instructions) {
        Insert(method,method.Body.Instructions[0],instructions,false);
    }
    private static void Insert(MethodDefinition method,Instruction target,Instruction[] instructions,bool redirect) {
        if(redirect)foreach(var existing in method.Body.Instructions.ToArray()) {
            if(existing.Operand==target)existing.Operand=instructions[0];
            var branches=existing.Operand as Instruction[];
            if(branches!=null)for(int i=0;i<branches.Length;i++)if(branches[i]==target)branches[i]=instructions[0];
        }
        var il=method.Body.GetILProcessor();
        foreach(var instruction in instructions)il.InsertBefore(target,instruction);
    }
    private static void Patch(string original,string destination,string runtimeFile) {
        var resolver=new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(original));
        using(var assembly=AssemblyDefinition.ReadAssembly(original,new ReaderParameters {
            AssemblyResolver=resolver
        }
        ))   using(var runtime=AssemblyDefinition.ReadAssembly(runtimeFile)) {
            var module=assembly.MainModule;
            var r=runtime.MainModule.GetType("ByteTrainer.OutOfSpace.Runtime");
            Func<string,int,MethodReference> hook=(name,count)=>module.ImportReference(Method(r,name,count));

            var manager=module.GetType("LoopManager");
            Prefix(Method(manager,"Update",0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,manager.Fields.Single(f=>f.Name=="gameStarted")),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,manager.Fields.Single(f=>f.Name=="gameOver")),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,manager.Fields.Single(f=>f.Name=="victory")),Instruction.Create(OpCodes.Or),Instruction.Create(OpCodes.Call,hook("Tick",3)));
            var stats=module.GetType("PlayerStats");
            Prefix(Method(stats,"Update",0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("Touch",1)));
            var names=new[]{"UpdateFatigue","UpdateHunger","UpdateDirt"};var counts=new[]{3,2,3};
            for(int index=0;index<names.Length;index++) {
                var change=Method(stats,names[index],counts[index]);var entry=change.Body.Instructions[0];
                Prefix(change,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldc_I4,index),Instruction.Create(OpCodes.Call,hook("BlockIncrease",3)),Instruction.Create(OpCodes.Brfalse,entry),Instruction.Create(OpCodes.Ret));
            }
            var direct=Method(stats,"SetCurrentDirtness",1);
            Prefix(direct,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("DirtValue",2)),Instruction.Create(OpCodes.Starg,direct.Parameters[0]));
            // Widen all short branches after insertion so changed method sizes cannot corrupt jump targets.
            var opcodes=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
            foreach(var type in module.GetTypes())foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions)if(instruction.OpCode.OperandType==OperandType.ShortInlineBrTarget)instruction.OpCode=opcodes[instruction.OpCode.Name.Substring(0,instruction.OpCode.Name.Length-2)];
            assembly.Write(destination);
        }
    }
    private static void Main(string[] args) {
        try {
            if(args.Length!=2||args[0]!="install")throw new ArgumentException("Uso: PatchTool.exe install pasta");
            string dir=Path.Combine(args[1],"Out of Space_Data","Managed");
            string target=Path.Combine(dir,"Assembly-CSharp.dll"),backup=target+".trainerhub-original",marker=target+".trainerhub-sha256";
            string current=Hash(target);
            bool original=current==OriginalHash;
            if(!original&&(!File.Exists(marker)||File.ReadAllText(marker).Trim()!=current))throw new InvalidOperationException("Game assembly differs from the verified version.");
            if(!File.Exists(backup)&&original)File.Copy(target,backup);
            if(!File.Exists(backup)||Hash(backup)!=OriginalHash)throw new InvalidOperationException("Original backup missing or invalid.");
            string runtime=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,RuntimeFile),staged=target+".new";
            Patch(backup,staged,runtime);
            File.Copy(staged,target,true);
            File.Delete(staged);
            File.Copy(runtime,Path.Combine(dir,RuntimeFile),true);
            File.WriteAllText(marker,Hash(target));
        }
        catch(Exception error) {
            Console.Error.WriteLine(error);
            Environment.ExitCode=1;
        }
    }
}

