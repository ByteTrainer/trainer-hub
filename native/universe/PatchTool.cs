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
    private static void PatchInstantDeposit(ModuleDefinition module,MethodReference enabled) {
        var construction=module.GetType("Construction");
        var interact=Method(construction,"Interact",1);
        var construct=Method(construction,"Construct",4);
        var batch=interact.Body.Instructions.Single(i=>i.Offset==0x0117);
        var storeBatch=interact.Body.Instructions.Single(i=>i.Offset==0x013e);
        var checkStage=Method(construction,"CheckStage",0);
        var endInteraction=interact.Body.Instructions.Last();
        var startInteraction=Method(construction,"Construct",1);
        var callInteract=startInteraction.Body.Instructions.Single(i=>i.Operand==interact);
        var finishInteraction=startInteraction.Body.Instructions.Last();
        var deposited=new VariableDefinition(module.TypeSystem.Boolean);
        startInteraction.Body.Variables.Add(deposited);
        interact.ReturnType=module.TypeSystem.Boolean;

        // Keep the native stock and remaining-amount caps. Replace only the animated batch size.
        Insert(interact,batch,new[] {
            Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,enabled),
            Instruction.Create(OpCodes.Brfalse,batch),
            Instruction.Create(OpCodes.Ldloc,interact.Body.Variables[5]),Instruction.Create(OpCodes.Br,storeBatch)
        },false);
        var cooldownReturn=interact.Body.Instructions.Single(i=>i.Offset==0x0046);
        Insert(interact,cooldownReturn,new[] {
            Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,enabled),
            Instruction.Create(OpCodes.Brtrue,cooldownReturn.Next),Instruction.Create(OpCodes.Ldc_I4_0)
        },false);

        var contribution=construction.NestedTypes.Single(t=>t.Name=="<>c__DisplayClass133_1");
        var complete=Method(contribution,"<Construct>b__3",0);
        var pickup=contribution.Fields.Single(f=>f.Name=="pickupObject");
        var pending=contribution.Fields.Single(f=>f.Name=="CS$<>8__locals1");
        var confirm=complete.Body.Instructions.Single(i=>i.Offset==0x001b);
        var advanceStage=complete.Body.Instructions.Single(i=>i.Offset==0x00da);
        var endContribution=complete.Body.Instructions.Last();

        // Reuse the native confirmation and UI refresh, with no spawned pickup to return.
        Prefix(complete,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,pickup),
            Instruction.Create(OpCodes.Brfalse,confirm));
        Insert(complete,advanceStage,new[] {
            Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,pickup),
            Instruction.Create(OpCodes.Brfalse,endContribution)
        },true);

        var animate=construct.Body.Instructions.Single(i=>i.Offset==0x012c);
        Insert(construct,animate,new[] {
            Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,enabled),
            Instruction.Create(OpCodes.Brfalse,animate),
            Instruction.Create(OpCodes.Newobj,Method(contribution,".ctor",0)),Instruction.Create(OpCodes.Dup),
            Instruction.Create(OpCodes.Ldloc_0),Instruction.Create(OpCodes.Stfld,pending),
            Instruction.Create(OpCodes.Call,complete),Instruction.Create(OpCodes.Ret)
        },false);

        // Advance only after the native interaction finishes using the current stage and requirements.
        Insert(interact,endInteraction,new[] {
            Instruction.Create(OpCodes.Ldloc_2),
            Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,enabled),
            Instruction.Create(OpCodes.And)
        },false);
        startInteraction.Body.GetILProcessor().InsertAfter(callInteract,Instruction.Create(OpCodes.Stloc,deposited));
        Insert(startInteraction,finishInteraction,new[] {
            Instruction.Create(OpCodes.Ldloc,deposited),Instruction.Create(OpCodes.Brfalse,finishInteraction),
            Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,checkStage)
        },false);
    }
    private static void PatchAfkFishing(ModuleDefinition module,TypeDefinition runtime,TypeDefinition fish) {
        var queue=module.ImportReference(Method(runtime,"Queue",3));
        var clear=module.ImportReference(Method(runtime,"Clear",1));
        var getPlayer=Method(module.GetType("UI.PerPlayerUI.PlayerScreen"),"get_Player",0);
        var action=module.ImportReference(typeof(Action).GetConstructor(new[]{typeof(object),typeof(IntPtr)}));

        // Execute the same handlers as the game UI, outside the active fishing coroutine.
        Action<MethodDefinition,Instruction,MethodDefinition,FieldDefinition> queueAction=(method,point,handler,receiver)=>{
            var instructions=new System.Collections.Generic.List<Instruction> {
                Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,getPlayer),
                Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0)
            };
            if(receiver!=null)instructions.Add(Instruction.Create(OpCodes.Ldfld,receiver));
            instructions.Add(Instruction.Create(OpCodes.Ldftn,handler));
            instructions.Add(Instruction.Create(OpCodes.Newobj,action));
            instructions.Add(Instruction.Create(OpCodes.Call,queue));
            Insert(method,point,instructions.ToArray(),false);
        };
        var start=Method(fish,"StartMiniGame",0);
        queueAction(start,start.Body.Instructions.Last(),Method(fish,"OnMinigameWin",0),null);

        var result=Method(fish,"ShowNextPopUp",1);
        var claim=result.Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="Claim");
        var catchPopup=fish.Fields.Single(f=>f.Name=="catchPopUp");
        queueAction(result,claim.Next,Method(catchPopup.FieldType.Resolve(),"Close",0),catchPopup);

        var info=Method(fish,"ShowInfo",0);
        var infoPopup=fish.Fields.Single(f=>f.Name=="infoPopUp");
        queueAction(info,info.Body.Instructions.Last(),Method(infoPopup.FieldType.Resolve(),"RestartMinigame",0),infoPopup);

        foreach(var method in new[]{Method(fish,"PostGameSetup",1),Method(fish,"Close",1),Method(fish,"OnDisable",0),Method(fish,"RestartMinigame",0)}) {
            Prefix(method,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,clear));
        }
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
            var nativeHealthEntry=health.Body.Instructions[0];
            Prefix(health,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Method(module.GetType("PawnComponent"),"get_Pawn",0)),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("BlockHealth",3)),Instruction.Create(OpCodes.Brfalse,health.Body.Instructions[0]),Instruction.Create(OpCodes.Ret));
            var checkQuitting=health.Body.Instructions.Single(i=>i.Offset==0x001b);
            Insert(health,nativeHealthEntry,new[] {
                Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),
                Instruction.Create(OpCodes.Call,hook("IsForcedDeath",2)),Instruction.Create(OpCodes.Brtrue,checkQuitting)
            },true);
            var playerHealth=Method(module.GetType("PlayerHealth"),"SetHealth",4);
            var baseHealth=playerHealth.Body.Instructions.Single(i=>i.Offset==0x001c);
            Prefix(playerHealth,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),
                Instruction.Create(OpCodes.Call,hook("IsForcedDeath",2)),Instruction.Create(OpCodes.Brtrue,baseHealth));
            var attributes=module.GetType("Pawns.Attributes.PlayerAttributes");
            var boosters=module.GetType("Pawns.Player.PlayerBoosters");
            foreach(var penalty in new[]{Method(attributes,"OnDeath",0),Method(boosters,"RemoveBoostersAfterDeath",0)}) {
                Prefix(penalty,Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Call,hook("PreserveProgressOnDeath",1)),
                    Instruction.Create(OpCodes.Brfalse,penalty.Body.Instructions[0]),Instruction.Create(OpCodes.Ret));
            }
            PatchInstantDeposit(module,hook("InstantDeposit",1));
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
            var experience=Method(attributes,"set_Experience",1);
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
            PatchAfkFishing(module,runtime.MainModule.GetType("ByteTrainer.Universe.AfkFishing"),fish);
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
            var refinery=module.GetType("Refinery");
            var core=module.GetType("RefineryCore");
            var collect=Method(refinery,"CollectIdleProducts",0);
            collect.IsPublic=true;
            var completed=collect.Body.Instructions.Single(i=>i.Offset==0x00ab);
            if(completed.OpCode!=OpCodes.Call||((MethodReference)completed.Operand).Name!="FloorToInt")throw new InvalidOperationException("Refinery completion changed.");
            Insert(collect,completed.Next,new[] {
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Call,Method(refinery,"get_Core",0)),
                Instruction.Create(OpCodes.Call,hook("RefiningAmount",2))
            },false);
            foreach(var sequenceName in new[]{"RefineryCore/<RefiningSequence>d__25","RefineryCore/<IndependentRefiningSequence>d__26"}) {
                var refiningSequence=module.GetTypes().Single(t=>t.FullName==sequenceName);
                var advance=Method(refiningSequence,"MoveNext",0);
                var entry=advance.Body.Instructions[0];
                // Finish paid entries through the game's existing offline completion path.
                Prefix(advance,
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldfld,refiningSequence.Fields.Single(f=>f.Name=="<>4__this")),
                    Instruction.Create(OpCodes.Ldfld,core.Fields.Single(f=>f.Name=="refinery")),
                    Instruction.Create(OpCodes.Call,hook("ShouldRefine",1)),
                    Instruction.Create(OpCodes.Brfalse,entry),
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldfld,refiningSequence.Fields.Single(f=>f.Name=="<>4__this")),
                    Instruction.Create(OpCodes.Ldfld,core.Fields.Single(f=>f.Name=="refinery")),
                    Instruction.Create(OpCodes.Call,collect));
            }
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
