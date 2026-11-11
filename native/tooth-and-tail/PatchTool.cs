using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool {
    private const string OriginalHash = "C2F1CCD6C836E047DC2F61E71002A767E99E6A2DC8DC47B51AB1501FEF070CF8";
    private const string RuntimeFile = "ToothAndTailTrainer.Runtime.dll";

    private static string Hash(string file) {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static MethodDefinition Method(TypeDefinition type, string name) {
        return type.Methods.Single(method => method.Name == name);
    }

    private static Instruction This() { return Instruction.Create(OpCodes.Ldarg_0); }

    private static Instruction Load(TypeDefinition type, string name) {
        return Instruction.Create(OpCodes.Ldfld, type.Fields.Single(field => field.Name == name));
    }

    private static Instruction Call(MethodReference method) { return Instruction.Create(OpCodes.Call, method); }

    private static void Before(MethodDefinition method, Instruction anchor, params Instruction[] additions) {
        var il = method.Body.GetILProcessor();
        foreach (var instruction in additions) il.InsertBefore(anchor, instruction);
    }

    private static void Prefix(MethodDefinition method, params Instruction[] additions) {
        Before(method, method.Body.Instructions[0], additions);
    }

    private static bool FieldOp(Instruction instruction, OpCode opcode, string name) {
        var field = instruction.Operand as FieldReference;
        return instruction.OpCode == opcode && field != null && field.Name == name;
    }

    private static void PatchDamage(TypeDefinition type, MethodReference protect, MethodReference scale) {
        var method = Method(type, "Damage");
        var entry = method.Body.Instructions[0];
        var scaleEntry = This();
        Prefix(method, This(), Instruction.Create(OpCodes.Ldarg_1), Call(protect),
            Instruction.Create(OpCodes.Brfalse, scaleEntry), Instruction.Create(OpCodes.Ret),
            scaleEntry, Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldarg_3),
            Call(scale), Instruction.Create(OpCodes.Starg, method.Parameters[0]));
    }

    private static void Patch(string original, string output, string runtimeFile) {
        using (var game = AssemblyDefinition.ReadAssembly(original))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile)) {
            var module = game.MainModule;
            var hooks = runtime.MainModule.GetType("ByteTrainer.ToothAndTail.Runtime");
            Func<string, MethodReference> hook = name => module.ImportReference(Method(hooks, name));
            Func<string, TypeDefinition> type = name => module.GetType("Armada." + name);

            var state = type("GameStates.GameStateGame");
            Prefix(Method(state, "Update"),
                This(), Load(state, "_session"), This(), Load(state, "_playerGroup"),
                This(), Load(state, "_netInterface"), This(), Load(state, "_usage"),
                This(), Load(state, "_isObserver"), This(), Load(state, "_isRanked"), Call(hook("Tick")));
            Prefix(Method(state, "Stop"), Call(hook("Stop")));

            var actor = type("Actor");
            var structure = type("Structure");
            PatchDamage(actor, hook("Protect"), hook("Damage"));
            PatchDamage(structure, hook("Protect"), hook("Damage"));

            var faction = type("Faction");
            Prefix(Method(faction, "Update"), This(), Call(hook("Refill")));
            var cost = Method(faction, "GetBuildCost");
            var free = cost.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldc_I4_0 && i.Next.OpCode == OpCodes.Call
                && ((MethodReference)i.Next.Operand).FullName == "ArmadaData.FInt ArmadaData.FInt::op_Implicit(System.Int32)"
                && i.Next.Next.OpCode == OpCodes.Ret);
            Prefix(cost, This(), Call(hook("FreeConstruction")), Instruction.Create(OpCodes.Brtrue, free));

            var build = Method(structure, "Update");
            var progress = build.Body.Instructions.Single(i => FieldOp(i, OpCodes.Stfld, "BuildProgress"));
            Before(build, progress, This(), Call(hook("Construction")));

            var production = Method(type("Behaviors.Production"), "Execute");
            var duration = production.Body.Instructions.Single(i => FieldOp(i, OpCodes.Ldfld, "ProductionTime"));
            var il = production.Body.GetILProcessor();
            var producer = This();
            il.InsertAfter(duration, producer);
            var loadMe = Load(type("Behaviors.Behavior"), "Me");
            il.InsertAfter(producer, loadMe);
            il.InsertAfter(loadMe, Call(hook("ProductionDuration")));

            var speed = Method(actor, "GetSpeed");
            il = speed.Body.GetILProcessor();
            foreach (var ret in speed.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
                ret.OpCode = OpCodes.Ldarg_0;
                var call = Call(hook("Movement"));
                il.InsertAfter(ret, call);
                il.InsertAfter(call, Instruction.Create(OpCodes.Ret));
            }

            // Insertions may extend branches beyond the signed byte range.
            WidenBranches(build);
            WidenBranches(production);
            WidenBranches(speed);
            game.Write(output);
        }
    }

    private static void WidenBranches(MethodDefinition method) {
        var longForms = new[] { OpCodes.Br, OpCodes.Brfalse, OpCodes.Brtrue, OpCodes.Beq,
            OpCodes.Bge, OpCodes.Bgt, OpCodes.Ble, OpCodes.Blt, OpCodes.Bne_Un,
            OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un, OpCodes.Leave };
        foreach (var instruction in method.Body.Instructions) {
            if (instruction.OpCode.OperandType != OperandType.ShortInlineBrTarget) continue;
            instruction.OpCode = longForms.Single(opcode => opcode.Name + ".s" == instruction.OpCode.Name);
        }
    }

    private static void Main(string[] args) {
        try {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Use: PatchTool.exe install GAME_FOLDER");
            string directory = args[1];
            string target = Path.Combine(directory, "ToothAndTail.exe");
            string backup = target + ".trainerhub-original";
            string marker = target + ".trainerhub-sha256";
            string current = Hash(target);
            bool original = current == OriginalHash;
            if (!original && (!File.Exists(marker) || File.ReadAllText(marker).Trim() != current))
                throw new InvalidOperationException("Game executable differs from the verified build.");
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
