using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool {
    private const string OriginalHash = "2FE24B4EA3CD19D60C4C3A58A2D54EEEBE7585FF6649D119E53119B00CAEE311";
    private const string RuntimeFile = "DungeonTrainer.Runtime.dll";

    private static string Hash(string file) {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static MethodDefinition Method(TypeDefinition type, string name) {
        return type.Methods.Single(method => method.Name == name);
    }

    private static FieldDefinition Field(TypeDefinition type, string name) {
        return type.Fields.Single(field => field.Name == name);
    }

    private static Instruction This() { return Instruction.Create(OpCodes.Ldarg_0); }

    private static Instruction Load(FieldDefinition field) { return Instruction.Create(OpCodes.Ldfld, field); }

    private static void InsertBefore(MethodDefinition method, Instruction entry, params Instruction[] instructions) {
        var il = method.Body.GetILProcessor();
        foreach (var instruction in instructions) il.InsertBefore(entry, instruction);
    }

    private static void Prefix(MethodDefinition method, params Instruction[] instructions) {
        InsertBefore(method, method.Body.Instructions[0], instructions);
    }

    private static void WrapReturns(MethodDefinition method, MethodReference hook) {
        var il = method.Body.GetILProcessor();
        foreach (var instruction in method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
            instruction.OpCode = OpCodes.Call;
            instruction.Operand = hook;
            il.InsertAfter(instruction, Instruction.Create(OpCodes.Ret));
        }
    }

    private static void PatchDungeon(TypeDefinition dungeon, Func<string, MethodReference> hook) {
        var display = Method(dungeon, "DisplayGame");
        var entry = display.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
        // Keep existing branches to the return on the same path as the notification.
        entry.OpCode = OpCodes.Ldarg_0;
        var il = display.Body.GetILProcessor();
        var owner = This();
        var network = Load(Field(dungeon, "gameNetManager"));
        var call = Instruction.Create(OpCodes.Call, hook("Enter"));
        il.InsertAfter(entry, owner);
        il.InsertAfter(owner, network);
        il.InsertAfter(network, call);
        il.InsertAfter(call, Instruction.Create(OpCodes.Ret));

        Prefix(Method(dungeon, "Update"), This(), This(), Load(Field(dungeon, "gameEventManager")),
            Instruction.Create(OpCodes.Call, hook("Tick")));
        Prefix(Method(dungeon, "OnDestroy"), This(), Instruction.Create(OpCodes.Call, hook("Leave")));

        var addDust = Method(dungeon, "DoAddDust");
        var stats = addDust.Body.Instructions.Single(i => i.Operand is FieldReference &&
            ((FieldReference)i.Operand).Name == "Stat_LootedDust").Previous.Previous;
        var done = addDust.Body.Instructions.Last();
        InsertBefore(addDust, stats, Instruction.Create(OpCodes.Call, hook("CountDustGain")),
            Instruction.Create(OpCodes.Brfalse, done));
    }

    private static void PatchHealth(TypeDefinition health, MethodReference hook) {
        var method = Method(health, "AddHealth");
        var ignore = method.Parameters.Single(p => p.Name == "ignoreInvincibility");
        Prefix(method, Instruction.Create(OpCodes.Ldarg_1), This(), This(), Load(Field(health, "dungeon")),
            Instruction.Create(OpCodes.Ldarg, ignore), Instruction.Create(OpCodes.Call, hook),
            Instruction.Create(OpCodes.Starg, method.Parameters[0]));
    }

    private static void PatchMovement(TypeDefinition mover, MethodReference hook) {
        var method = Method(mover, "GetMoveSpeed");
        var il = method.Body.GetILProcessor();
        var entry = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
        entry.OpCode = OpCodes.Ldarg_0;
        var actor = Load(Field(mover, "simMB"));
        var owner = This();
        var dungeon = Load(Field(mover, "dungeon"));
        var call = Instruction.Create(OpCodes.Call, hook);
        il.InsertAfter(entry, actor);
        il.InsertAfter(actor, owner);
        il.InsertAfter(owner, dungeon);
        il.InsertAfter(dungeon, call);
        il.InsertAfter(call, Instruction.Create(OpCodes.Ret));
    }

    private static void PatchAwards(ModuleDefinition module, Func<string, MethodReference> hook) {
        var methods = new[] {
            Method(module.GetType("Door"), "Open"),
            Method(module.GetType("Hero"), "ParentRoom_OnMobDied"),
            Method(module.GetType("Hero"), "OnTargetKilled"),
            Method(module.GetType("MinorModule"), "OnMobDied"),
            Method(module.GetType("MobDustLoot"), "Update"),
            Method(module.GetType("DustFactory"), "RPC_RunActivation"),
            Method(module.GetType("CryoCapsule"), "OnOpenEndAnimEvent"),
            Method(module.GetTypes().Single(t => t.FullName.StartsWith("Dungeon/<ApplyFIDSIncome>")), "MoveNext"),
            Method(module.GetTypes().Single(t => t.FullName.StartsWith("Dungeon/<TriggerEvents>")), "MoveNext")
        };
        foreach (var method in methods) {
            int changed = 0;
            foreach (var instruction in method.Body.Instructions) {
                var called = instruction.Operand as MethodReference;
                if (called == null || (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)) continue;
                string name = null;
                if (called.DeclaringType.Name == "Player") {
                    switch (called.Name) {
                        case "AddFIDS": name = "AwardFids"; break;
                        case "AddFood": name = "AwardFood"; break;
                        case "AddIndustry": name = "AwardIndustry"; break;
                        case "AddScience": name = "AwardScience"; break;
                    }
                } else if (called.DeclaringType.Name == "Dungeon" && called.Name == "AddDust") name = "AwardDust";
                if (name == null) continue;
                instruction.OpCode = OpCodes.Call;
                instruction.Operand = hook(name);
                changed++;
            }
            if (changed == 0) throw new InvalidOperationException("Could not find resource awards in " + method.FullName);
        }
    }

    private static Instruction FindCall(MethodDefinition method, string declaringType, string name) {
        return method.Body.Instructions.Single(i => {
            var called = i.Operand as MethodReference;
            return called != null && called.DeclaringType.Name == declaringType && called.Name == name;
        });
    }

    private static void HookCallResult(MethodDefinition method, Instruction result, MethodReference hook) {
        method.Body.GetILProcessor().InsertAfter(result, Instruction.Create(OpCodes.Call, hook));
    }

    private static void PatchResearch(ModuleDefinition module, Func<string, MethodReference> hook) {
        var panel = module.GetType("ResearchPanel");
        foreach (string name in new[] { "RefreshContent", "OnResetButtonClick" }) {
            var method = Method(panel, name);
            HookCallResult(method, FindCall(method, "CurveDefinedValue", "GetValue"), hook("ResearchCost"));
        }

        var canResearch = Method(module.GetType("Dungeon"), "CanResearchBluePrint");
        var count = FindCall(canResearch, "Door", "get_OpenableDoors").Next;
        if (!(count.Operand is MethodReference) || ((MethodReference)count.Operand).Name != "get_Count")
            throw new InvalidOperationException("Missing research door count.");
        HookCallResult(canResearch, count, hook("ResearchDoorCount"));

        var start = Method(module.GetType("Artifact"), "DoResearchBluePrint");
        var success = start.Body.Instructions.Last();
        if (success.OpCode != OpCodes.Ret) throw new InvalidOperationException("Missing research success path.");
        success.OpCode = OpCodes.Ldarg_0;
        var il = start.Body.GetILProcessor();
        var complete = Instruction.Create(OpCodes.Call, hook("CompleteResearch"));
        il.InsertAfter(success, complete);
        il.InsertAfter(complete, Instruction.Create(OpCodes.Ret));
    }

    private static void ReplaceCall(MethodDefinition method, string declaringType, string name, MethodReference hook) {
        var call = FindCall(method, declaringType, name);
        call.OpCode = OpCodes.Call;
        call.Operand = hook;
    }

    private static void PatchActiveSkills(ModuleDefinition module, Func<string, MethodReference> hook) {
        var type = module.GetType("ActiveSkill");
        var activate = Method(type, "Activate");
        var deactivate = Method(type, "Deactivate");
        var apply = Method(type, "RPC_DoActivate");
        var dungeon = Field(type, "dungeon");
        foreach (var method in new[] { activate, Method(module.GetType("ActiveSkillItem"), "RefreshContent") }) {
            var threshold = FindCall(method, "SkillConfig", "get_CooldownTurnsCount").Next;
            if (threshold.OpCode != OpCodes.Add) throw new InvalidOperationException("Missing skill cooldown threshold.");
            HookCallResult(method, threshold, hook("CooldownThreshold"));
        }
        WrapReturns(Method(type, "GetRemainingTurns"), hook("CooldownRemaining"));

        // Timed and door-based expiry are separate from explicit teardown.
        var expire = new MethodDefinition("ByteTrainerExpireActiveSkill", MethodAttributes.Private | MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        type.Methods.Add(expire);
        var il = expire.Body.GetILProcessor();
        var normal = This();
        foreach (var instruction in new[] {
            This(), This(), Load(dungeon), Instruction.Create(OpCodes.Call, hook("DeferSkillExpiration")),
            Instruction.Create(OpCodes.Brfalse, normal), Instruction.Create(OpCodes.Ret),
            normal, Instruction.Create(OpCodes.Call, deactivate), Instruction.Create(OpCodes.Ret)
        }) il.Append(instruction);

        foreach (var method in new[] { apply, deactivate }) {
            var timer = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "Deactivate");
            timer.Operand = expire.Name;
            var callback = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldftn && i.Operand == deactivate);
            callback.Operand = expire;
        }
        Prefix(deactivate, This(), Instruction.Create(OpCodes.Call, hook("CancelSkillExpiration")));

        // A zero-second effect can still last until the next room event.
        var zeroDuration = apply.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld
            && i.Operand == Field(type, "IsActivated") && i.Previous.OpCode == OpCodes.Ldc_I4_0);
        zeroDuration.Previous.OpCode = OpCodes.Ldarg_0;
        InsertBefore(apply, zeroDuration, This(), Load(dungeon), Instruction.Create(OpCodes.Call, hook("HoldTurnSkill")));

        ReplaceCall(apply, "IGameEventService", "add_OnDungeonTurnChanged", hook("SubscribeCooldown"));
        ReplaceCall(apply, "IGameEventService", "add_OnRoomEventFinished", hook("SubscribeTurnExpiration"));
        var actionCtor = (MethodReference)apply.Body.Instructions.Single(i => i.OpCode == OpCodes.Newobj &&
            ((MethodReference)i.Operand).DeclaringType.FullName == "System.Action").Operand;
        var remove = (MethodReference)FindCall(deactivate, "IGameEventService", "remove_OnRoomEventFinished").Operand;
        Prefix(Method(type, "OnDestroy"), This(), Load(Field(type, "gameEventManager")), This(),
            Instruction.Create(OpCodes.Ldftn, expire), Instruction.Create(OpCodes.Newobj, actionCtor),
            Instruction.Create(OpCodes.Callvirt, remove));
    }

    private static void Patch(string original, string output, string runtimeFile) {
        using (var resolver = new DefaultAssemblyResolver())
        using (var game = AssemblyDefinition.ReadAssembly(original, new ReaderParameters { AssemblyResolver = resolver }))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile)) {
            resolver.AddSearchDirectory(Path.GetDirectoryName(original));
            var module = game.MainModule;
            var hooks = runtime.MainModule.GetType("ByteTrainer.DungeonOfTheEndless.Runtime");
            Func<string, MethodReference> hook = name => module.ImportReference(Method(hooks, name));
            PatchDungeon(module.GetType("Dungeon"), hook);
            PatchHealth(module.GetType("Health"), hook("HealthChange"));
            WrapReturns(Method(module.GetType("ModuleConfig"), "GetIndustryCost"), hook("ConstructionCost"));
            WrapReturns(Method(module.GetType("BluePrintConfig"), "get_ResearchScienceCost"), hook("ResearchCost"));
            WrapReturns(Method(module.GetType("Hero"), "GetHealFoodCost"), hook("HealingCost"));
            PatchMovement(module.GetType("Mover"), hook("Movement"));
            PatchAwards(module, hook);
            PatchResearch(module, hook);
            PatchActiveSkills(module, hook);
            game.Write(output);
        }
    }

    private static void Main(string[] args) {
        try {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Use: PatchTool.exe install GAME_FOLDER");
            string directory = Path.Combine(args[1], "DungeonoftheEndless_Data", "Managed");
            string target = Path.Combine(directory, "Assembly-CSharp.dll");
            string backup = target + ".trainerhub-original";
            string marker = target + ".trainerhub-sha256";
            string current = Hash(target);
            bool original = current == OriginalHash;
            if (!original && (!File.Exists(marker) || File.ReadAllText(marker).Trim() != current))
                throw new InvalidOperationException("Game assembly differs from the verified build.");
            if (!File.Exists(backup) && original) File.Copy(target, backup);
            if (!File.Exists(backup) || Hash(backup) != OriginalHash)
                throw new InvalidOperationException("Original backup missing or invalid.");

            string runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RuntimeFile);
            string staged = target + ".new";
            Patch(backup, staged, runtime);
            File.Copy(staged, target, true);
            File.Delete(staged);
            File.Copy(runtime, Path.Combine(directory, RuntimeFile), true);
            File.WriteAllText(marker, Hash(target));
        } catch (Exception exception) {
            Console.Error.WriteLine(exception.Message);
            Environment.ExitCode = 1;
        }
    }
}
