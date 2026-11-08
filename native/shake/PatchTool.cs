using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
internal static class PatchTool  {
    private const string OriginalHash="C5F9DE45D4A646BDC972071E731C558A4891853577C7BDAA05F9A1CE03B27F59";
    private const string RuntimeFile="ShakeTrainer.Runtime.dll";
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
            var r=runtime.MainModule.GetType("ByteTrainer.Shake.Runtime");
            Func<string,int,MethodReference> hook=(name,count)=>module.ImportReference(Method(r,name,count));

            Prefix(Method(module.GetType("GameBootstrap"),"LateUpdate",0),Instruction.Create(OpCodes.Call,hook("Tick",0)));
            Prefix(Method(module.GetType("PlayerController"),"Update",0),Instruction.Create(OpCodes.Call,hook("Touch",0)));
            // Minigames disable the walking controller while their own updates remain active.
            Prefix(Method(module.GetType("FishingRig"),"Update",0),Instruction.Create(OpCodes.Call,hook("Touch",0)));
            Prefix(Method(module.GetType("LoopTreeShaker"),"Update",0),Instruction.Create(OpCodes.Call,hook("Touch",0)));
            var movement=Method(module.GetType("PlayerController"),"SkillSpeedScale",0);
            foreach(var ret in movement.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())Insert(movement,ret,new[]{Instruction.Create(OpCodes.Call,hook("Movement",1))},true);
            var clock=Method(module.GetType("DayNightSystem"),"Update",0);
            var advance=clock.Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).FullName.Contains("DayNightService::Tick("));
            Insert(clock,advance,new[]{Instruction.Create(OpCodes.Call,hook("Clock",1))},true);
            var chest=Method(module.GetType("ChestRuntime"),"TickRing",1);
            var auto=chest.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldsfld&&((FieldReference)i.Operand).FullName.Contains("ChestDebug::AutoPerfect"));
            Insert(chest,auto.Next,new[]{Instruction.Create(OpCodes.Call,hook("AutoChest",1))},false);
            var fishing=module.GetType("FishingRig");
            var bite=Method(fishing,"StepBite",2);
            Prefix(bite,Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,hook("Bite",1)),Instruction.Create(OpCodes.Starg,bite.Parameters[1]));
            var reel=Method(fishing,"StepReeling",4);
            var play=reel.Body.Instructions.Single(i=>i.Offset==0x0014);
            Insert(reel,play,new[]{
                Instruction.Create(OpCodes.Call,hook("EasyFishing",0)),Instruction.Create(OpCodes.Brfalse,play),
                Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Method(fishing,"BeginLanding",0)),Instruction.Create(OpCodes.Ret)
            },true);
            var recastCheck=Method(fishing,"CanRecast",0);
            var safety=recastCheck.Body.Instructions.Single(i=>i.Offset==0x20);
            Prefix(recastCheck,Instruction.Create(OpCodes.Call,hook("Afk",0)),Instruction.Create(OpCodes.Brtrue,safety));
            var release=Method(fishing,"Release",0);
            var powerAt=release.Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="PowerAt");
            Insert(release,powerAt.Next,new[]{Instruction.Create(OpCodes.Call,hook("CastPower",1))},false);
            var recast=Method(fishing,"Recast",0);
            var powerField=fishing.Fields.Single(f=>f.Name=="_power");
            Prefix(recast,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,powerField),Instruction.Create(OpCodes.Call,hook("RecastPower",1)),Instruction.Create(OpCodes.Stfld,powerField));
            var perfect=recast.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld&&((FieldReference)i.Operand).Name=="_perfect");
            Insert(recast,perfect,new[]{Instruction.Create(OpCodes.Call,hook("PerfectCast",1))},true);
            var tree=module.GetType("TreeShaker");
            var treeUpdate=Method(tree,"Update",0);
            var detector=treeUpdate.Body.Instructions.Single(i=>i.Offset==0x005f);
            var delta=module.ImportReference(treeUpdate.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().First(m=>m.Name=="get_deltaTime"));
            Insert(treeUpdate,detector,new[]{Instruction.Create(OpCodes.Call,delta),Instruction.Create(OpCodes.Call,hook("Shake",1)),Instruction.Create(OpCodes.Brfalse,detector),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldc_I4_1),Instruction.Create(OpCodes.Ldc_I4_1),Instruction.Create(OpCodes.Call,Method(tree,"TriggerShake",2))},true);
            foreach(var typeName in new[]{"SpellRunner","FishingRuntime","FrenzyRunner"}) {
                var update=Method(module.GetType(typeName),"Update",0);
                foreach(var tick in update.Body.Instructions.Where(i=>i.Operand is MethodReference&&new[]{"Tick","TickMirror"}.Contains(((MethodReference)i.Operand).Name)&&new[]{"SpellService","FrenzyService","LakeFrenzyService"}.Contains(((MethodReference)i.Operand).DeclaringType.Name)).ToArray())
                    Insert(update,tick,new[]{Instruction.Create(OpCodes.Call,hook("BonusTime",1))},true);
            }
            var fruitSale=Method(module.GetType("PlayerCollecter"),"FrenzySale",1);
            var spellSale=fruitSale.Body.Instructions.Single(i=>i.Offset==0x0029);
            Insert(fruitSale,spellSale,new[]{Instruction.Create(OpCodes.Call,hook("AutoSale",1))},true);
            var fishSale=Method(module.GetType("FishingRuntime"),"ServerLand",8);
            var frenzy=fishSale.Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="get_LakeFrenzyCounts");
            Insert(fishSale,frenzy.Next,new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("AutoSaleFor",2))},false);
            // Native frenzy sells only when the creel is full. Auto-sale uses that same
            // payout path for every new fish, including an otherwise empty creel.
            var sellBranch=fishSale.Body.Instructions.Single(i=>i.Offset==0x00bf);
            Insert(fishSale,sellBranch,new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,hook("AutoSaleFor",2))},true);
            var blackjack=module.GetType("BlackjackTable");
            var blackjackUpdate=Method(blackjack,"Update",0);
            Prefix(blackjackUpdate,Instruction.Create(OpCodes.Call,hook("Touch",0)));
            var blackjackTick=blackjackUpdate.Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).FullName.Contains("BlackjackService::Tick("));
            Insert(blackjackUpdate,blackjackTick,new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Method(blackjack,"get_LocalOwner",0))},true);
            blackjackTick.OpCode=OpCodes.Call;blackjackTick.Operand=hook("BlackjackTick",3);
            var move=Method(blackjack,"SendMove",1).Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="Move");
            move.OpCode=OpCodes.Call;move.Operand=hook("MoveBlackjack",3);
            var slots=module.GetType("SlotMachine");
            Prefix(Method(slots,"Update",0),Instruction.Create(OpCodes.Call,hook("Touch",0)));
            var spins=Method(slots,"Spin",0).Body.Instructions.Single(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="TrySpinMany");
            spins.OpCode=OpCodes.Call;spins.Operand=hook("SpinMany",5);
            // Widen all short branches after insertion so changed method sizes cannot corrupt jump targets.
            var opcodes=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
            foreach(var type in module.GetTypes())foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions)if(instruction.OpCode.OperandType==OperandType.ShortInlineBrTarget)instruction.OpCode=opcodes[instruction.OpCode.Name.Substring(0,instruction.OpCode.Name.Length-2)];
            assembly.Write(destination);
        }
    }
    private static void Main(string[] args) {
        try {
            if(args.Length!=2||args[0]!="install")throw new ArgumentException("Uso: PatchTool.exe install pasta");
            string dir=Path.Combine(args[1],"Shake it Co!_Data","Managed");
            string target=Path.Combine(dir,"Assembly-CSharp.dll"),backup=target+".trainerhub-original",marker=target+".trainerhub-sha256";
            string current=Hash(target);
            bool original=current==OriginalHash;
            if(!original&&(!File.Exists(marker)||File.ReadAllText(marker).Trim()!=current))throw new InvalidOperationException("Game assembly differs from the verified version.");
            if(!File.Exists(backup)&&original)File.Copy(target,backup);
            if(!File.Exists(backup)||Hash(backup)!=OriginalHash)throw new InvalidOperationException("Original backup missing or invalid.");
            string runtime=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,RuntimeFile),staged=target+".new";
            string core=Path.Combine(dir,"ShakeItCo.Core.dll");
            if(Hash(core)!="0BB2996BCA38791B4713CD90BDE05E5C7A444294FFD5FD7E1003AE2B44C23007")throw new InvalidOperationException("Core assembly differs from the verified version.");
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

