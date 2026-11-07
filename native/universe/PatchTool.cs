using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
internal static class PatchTool  {
    private const string OriginalHash="C158F687EE2C2AC8C35D206C2DE3A523480BA1B7A804AB068B6503FFC8077BE6";
    private const string RuntimeFile="UniverseTrainer.Runtime.dll";
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
            var r=runtime.MainModule.GetType("ByteTrainer.Universe.Runtime");
            Func<string,int,MethodReference> hook=(name,count)=>module.ImportReference(Method(r,name,count));
            var manager=module.GetType("GameManager");
            Prefix(Method(manager,"Update",0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,manager.Fields.Single(f=>f.Name=="paused")),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldflda,manager.Fields.Single(f=>f.Name=="_previousTimeScale")),Instruction.Create(OpCodes.Call,hook("Tick",2)));
            Prefix(Method(module.GetType("Player"),"OnUpdate",2),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("Touch",1)));
            var health=Method(module.GetType("PawnHealth"),"SetHealth",4);
            Prefix(health,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Method(module.GetType("PawnComponent"),"get_Pawn",0)),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("BlockHealth",3)),Instruction.Create(OpCodes.Brfalse,health.Body.Instructions[0]),Instruction.Create(OpCodes.Ret));
            var combat=Method(module.GetType("PawnCombat"),"Damage",3);
            Prefix(combat,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Method(module.GetType("PawnComponent"),"get_Pawn",0)),Instruction.Create(OpCodes.Ldarga,combat.Parameters[0]),Instruction.Create(OpCodes.Ldarg_3),Instruction.Create(OpCodes.Call,hook("Combat",3)),Instruction.Create(OpCodes.Brfalse,combat.Body.Instructions[0]),Instruction.Create(OpCodes.Ret));
            var resource=module.GetType("Resource");
            var gather=Method(resource,"Gather",5);
            Prefix(gather,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarga,gather.Parameters[0]),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,hook("ResourceDamage",3)));
            var value=Method(module.GetType("Pawns.Attributes.Attribute"),"get_Value",0);
            foreach(var ret in value.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())Insert(value,ret,new[] {
                Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,hook("Attribute",2))
            }
            ,true);
            var movement=Method(module.GetType("PawnMovement"),"OnFixedUpdate",1);
            var acceleration=movement.Body.Instructions.Single(i=>i.Offset==0x03a4);
            if(acceleration.OpCode!=OpCodes.Call||((MethodReference)acceleration.Operand).Name!="get_MoveVelocity")throw new InvalidOperationException("Movement acceleration changed.");
            Insert(movement,acceleration.Next,new[] {
                Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Method(module.GetType("PawnComponent"),"get_Pawn",0)),Instruction.Create(OpCodes.Ldloc_0),Instruction.Create(OpCodes.Call,hook("Movement",3))
            }
            ,false);
            var experience=Method(module.GetType("Pawns.Attributes.PlayerAttributes"),"set_Experience",1);
            Prefix(experience,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("Experience",2)),Instruction.Create(OpCodes.Starg,experience.Parameters[0]));
            var regeneration=hook("Regeneration",1);
            var remaining=resource.Fields.Single(f=>f.Name=="timeRemaining");
            var sequence=module.GetTypes().Single(t=>t.FullName=="Resource/<RegenerationSequence>d__86");
            foreach(var instruction in Method(sequence,"MoveNext",0).Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldfld&&((FieldReference)i.Operand).FullName==remaining.FullName).ToArray())Method(sequence,"MoveNext",0).Body.GetILProcessor().InsertAfter(instruction,Instruction.Create(OpCodes.Call,regeneration));
            var time=Method(resource,"GetTimeRemaining",0);
            foreach(var ret in time.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())Insert(time,ret,new[] {
                Instruction.Create(OpCodes.Call,regeneration)
            }
            ,true);
            var custom=module.GetType("CustomPawnSpawner");
            foreach(var method in new[]{Method(custom,"SpawnAvailable",0),Method(custom,"SpawnPawn",1)}) {
                var originalEntry=method.Body.Instructions[0];
                var guard=new System.Collections.Generic.List<Instruction> {
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldfld,custom.Fields.Single(f=>f.Name=="_deaths")),
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldfld,custom.Fields.Single(f=>f.Name=="pawnData")),
                    Instruction.Create(OpCodes.Call,hook("BlockCustom",2)),
                    Instruction.Create(OpCodes.Brfalse,originalEntry)
                };
                if(method.ReturnType.MetadataType==MetadataType.Boolean)guard.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                guard.Add(Instruction.Create(OpCodes.Ret));
                Prefix(method,guard.ToArray());
            }
            var wave=module.GetTypes().Single(t=>t.FullName=="PawnSpawner/WaveData");
            var spawner=module.GetType("PawnSpawner");
            foreach(var method in new[]{Method(spawner,"SpawnAvailable",0),Method(wave,"SpawnPawn",0)}) {
                var originalEntry=method.Body.Instructions[0];
                var guard=new System.Collections.Generic.List<Instruction>();
                Action owner=()=>{
                    guard.Add(Instruction.Create(OpCodes.Ldarg_0));
                    if(method.DeclaringType==wave)guard.Add(Instruction.Create(OpCodes.Ldfld,wave.Fields.Single(f=>f.Name=="pawnSpawner")));
                };
                owner();
                guard.Add(Instruction.Create(OpCodes.Ldfld,spawner.Fields.Single(f=>f.Name=="looping")));
                owner();
                guard.Add(Instruction.Create(OpCodes.Call,Method(spawner,"get_CurrentWave",0)));
                owner();
                guard.Add(Instruction.Create(OpCodes.Ldfld,spawner.Fields.Single(f=>f.Name=="waves")));
                guard.Add(Instruction.Create(OpCodes.Ldlen));
                guard.Add(Instruction.Create(OpCodes.Conv_I4));
                guard.Add(Instruction.Create(OpCodes.Call,hook("BlockLoop",3)));
                guard.Add(Instruction.Create(OpCodes.Brfalse,originalEntry));
                if(method.ReturnType.MetadataType==MetadataType.Boolean)guard.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                guard.Add(Instruction.Create(OpCodes.Ret));
                Prefix(method,guard.ToArray());
            }
            var fish=module.GetType("UI.PerPlayerUI.FishingMinigame.FishingScreen");
            var input=Method(fish,"OnInput",1);
            var target=input.Body.Instructions.Single(i=>i.Offset==0x001a);
            Insert(input,target,new[] {
                Instruction.Create(OpCodes.Ldc_I4,12),
                Instruction.Create(OpCodes.Call,hook("Enabled",1)),
                Instruction.Create(OpCodes.Brfalse,target),
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Call,Method(fish,"OnMinigameWin",0)),
                Instruction.Create(OpCodes.Ret)
            },true);
            // Widen all short branches after insertion so changed method sizes cannot corrupt jump targets.
            var opcodes=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
            foreach(var type in module.GetTypes())foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions)if(instruction.OpCode.OperandType==OperandType.ShortInlineBrTarget)instruction.OpCode=opcodes[instruction.OpCode.Name.Substring(0,instruction.OpCode.Name.Length-2)];
            assembly.Write(destination);
        }
    }
    private static void Main(string[] args) {
        try {
            if(args.Length!=2||args[0]!="install")throw new ArgumentException("Uso: PatchTool.exe install pasta");
            string dir=Path.Combine(args[1],"My Little Universe_Data","Managed");
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
