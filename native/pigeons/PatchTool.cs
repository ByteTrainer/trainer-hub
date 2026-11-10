using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool {
    private const string OriginalHash = "6F68EAA560833480200C41E0097380E465B6E6A310A4DA22A26D822853118C72";
    private const string RuntimeFile = "PigeonsTrainer.Runtime.dll";

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

    private static Instruction Load(FieldDefinition field, bool address = false) {
        return Instruction.Create(address ? OpCodes.Ldflda : OpCodes.Ldfld, field);
    }

    private static Instruction This() {
        return Instruction.Create(OpCodes.Ldarg_0);
    }

    private static void Prefix(MethodDefinition method, params Instruction[] instructions) {
        var il = method.Body.GetILProcessor();
        var entry = method.Body.Instructions[0];
        foreach (var instruction in instructions) il.InsertBefore(entry, instruction);
    }

    private static void PatchPlayerDamage(TypeDefinition health, MethodReference hook) {
        var apply = Method(health, "AplicarDano");
        var decrease = Method(health, "DiminuirVida");
        var owner = Field(health, "identificador");
        Prefix(apply, Instruction.Create(OpCodes.Ldarg_1), This(), Load(owner),
            Instruction.Create(OpCodes.Call, hook), Instruction.Create(OpCodes.Brfalse, apply.Body.Instructions[0]),
            Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Ret));
        Prefix(decrease, Instruction.Create(OpCodes.Ldarg_1), This(), Load(owner),
            Instruction.Create(OpCodes.Call, hook), Instruction.Create(OpCodes.Brfalse, decrease.Body.Instructions[0]),
            Instruction.Create(OpCodes.Ret));
    }

    private static void PatchEnemyDamage(TypeDefinition enemy, MethodReference hook) {
        foreach (string name in new[] { "AplicarDano", "LevarDanoSemEfeito" }) {
            var method = Method(enemy, name);
            Prefix(method, Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldarg_2),
                This(), Load(Field(enemy, "vidaAtual")), Instruction.Create(OpCodes.Call, hook),
                Instruction.Create(OpCodes.Starg, method.Parameters[0]));
        }
    }

    private static void PatchPoints(TypeDefinition score, MethodReference hook) {
        var award = Method(score, "AddScore");
        // The last assignment incorporates the game's own score multipliers.
        var result = award.Body.Instructions.Last(i => i.OpCode == OpCodes.Starg_S && i.Operand == award.Parameters[0]);
        var il = award.Body.GetILProcessor();
        var entry = result.Next;
        foreach (var instruction in new[] {
            Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldarg_2),
            This(), Load(Field(score, "pontos")), This(), Load(Field(score, "pontosAcumulados")),
            This(), Load(Field(score, "pontosAusentes")), Instruction.Create(OpCodes.Call, hook),
            Instruction.Create(OpCodes.Starg, award.Parameters[0])
        }) il.InsertBefore(entry, instruction);
    }

    private static void PatchCharge(TypeDefinition ultimate, string methodName, MethodReference hook) {
        var method = Method(ultimate, methodName);
        Prefix(method, This(), Load(Field(ultimate, "identificador")),
            This(), Load(Field(ultimate, "maximo")), This(), Load(Field(ultimate, "progresso"), true),
            Instruction.Create(OpCodes.Call, hook), Instruction.Create(OpCodes.Brfalse, method.Body.Instructions[0]),
            This(), Instruction.Create(OpCodes.Call, Method(ultimate, "ChecarProgresso")));
    }

    private static void WrapRead(MethodDefinition method, FieldDefinition field, FieldDefinition owner, MethodReference hook) {
        var read = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldfld
            && i.Operand is FieldReference && ((FieldReference)i.Operand).FullName == field.FullName);
        var il = method.Body.GetILProcessor();
        var loadThis = This();
        var loadOwner = Load(owner);
        il.InsertAfter(read, loadThis);
        il.InsertAfter(loadThis, loadOwner);
        il.InsertAfter(loadOwner, Instruction.Create(OpCodes.Call, hook));
    }

    private static void PatchAmmo(ModuleDefinition module, Func<string, MethodReference> hook) {
        var weapon = module.GetType("Arma");
        var owner = Field(weapon, "identificador");
        var nativeInfinite = Field(weapon, "municaoInfinita");
        var freeShot = new FieldDefinition("byteTrainerFreeShot", FieldAttributes.Family, module.TypeSystem.Boolean);
        weapon.Fields.Add(freeShot);

        var fire = Method(weapon, "TentarAtacar");
        var readInfinite = fire.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldfld && i.Operand == nativeInfinite);
        var il = fire.Body.GetILProcessor();
        var branch = readInfinite.Next;
        foreach (var instruction in new[] {
            This(), This(), Load(owner), This(), Load(freeShot, true), Instruction.Create(OpCodes.Call, hook("FreeShot"))
        }) il.InsertBefore(branch, instruction);
        // The attack cooldown has a short branch across the extended ammo block.
        var longBranches = typeof(OpCodes).GetFields().Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)).ToDictionary(opcode => opcode.Name);
        foreach (var instruction in fire.Body.Instructions.Where(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget))
            instruction.OpCode = longBranches[instruction.OpCode.Name.Substring(0, instruction.OpCode.Name.Length - 2)];

        var cancel = Method(module.GetType("ArmaArremesso"), "OnDisable");
        var refund = cancel.Body.Instructions.Single(i => i.OpCode == OpCodes.Add
            && i.Previous.OpCode == OpCodes.Ldc_I4_1 && i.Next.Operand == Field(weapon, "municaoAtual"));
        il = cancel.Body.GetILProcessor();
        var loadAddress = Load(freeShot, true);
        refund.Previous.OpCode = OpCodes.Ldarg_0;
        il.InsertBefore(refund, loadAddress);
        il.InsertAfter(loadAddress, Instruction.Create(OpCodes.Call, hook("RefundShot")));

        WrapRead(Method(module.GetType("Cajado"), "TentarAtacar"), nativeInfinite, owner, hook("MagicAmmo"));
        WrapRead(Method(module.GetType("Espada"), "Atacar"), nativeInfinite, owner, hook("MagicAmmo"));
    }

    private static void ReplaceCall(MethodDefinition method, string target, MethodReference replacement) {
        var call = method.Body.Instructions.Single(i => i.Operand is MethodReference && ((MethodReference)i.Operand).FullName == target);
        call.OpCode = OpCodes.Call;
        call.Operand = replacement;
    }

    private static void PatchGameTime(ModuleDefinition module, MethodReference setter, MethodReference restore) {
        const string target = "System.Void UnityEngine.Time::set_timeScale(System.Single)";
        foreach (var instruction in module.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)) {
            var call = instruction.Operand as MethodReference;
            if (instruction.OpCode == OpCodes.Call && call != null && call.FullName == target)
                instruction.Operand = setter;
        }
        var game = module.GetType("GameManager");
        Prefix(Method(game, "MostrarMenuGameOver"), Instruction.Create(OpCodes.Call, restore));
        Prefix(Method(game, "OnDestroy"), Instruction.Create(OpCodes.Call, restore));
    }

    private static void Patch(string original, string output, string runtimeFile) {
        using (var resolver = new DefaultAssemblyResolver())
        using (var game = AssemblyDefinition.ReadAssembly(original, new ReaderParameters { AssemblyResolver = resolver }))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile)) {
            resolver.AddSearchDirectory(Path.GetDirectoryName(original));
            var module = game.MainModule;
            var hooks = runtime.MainModule.GetType("ByteTrainer.Pigeons.Runtime");
            Func<string, MethodReference> hook = name => module.ImportReference(Method(hooks, name));
            var score = module.GetType("Score");
            Prefix(Method(score, "Update"),
                This(), Load(Field(score, "pontos")), This(), Load(Field(score, "pontosAnterior")),
                This(), Load(Field(score, "pontosAcumulados"), true), Instruction.Create(OpCodes.Call, hook("Tick")));
            PatchPlayerDamage(module.GetType("VidaJogador"), hook("BlockDamage"));
            PatchEnemyDamage(module.GetType("VidaInimigo"), hook("DamageToEnemy"));
            PatchPoints(score, hook("AwardPoints"));
            ReplaceCall(Method(module.GetType("Dinheiro"), "ColetarLoot"),
                "System.Void MoedaManager::AddSaldoPartida(System.Int32)", hook("CollectCoins"));
            PatchGameTime(module, hook("SetGameTime"), hook("RestoreGameTime"));
            PatchAmmo(module, hook);

            var ultimate = module.GetType("Ultimate");
            PatchCharge(ultimate, "Update", hook("ChargeSpecial"));
            PatchCharge(ultimate, "UsarPontos", hook("ChargeSpecial"));

            var movement = module.GetType("MovimentoJogador");
            WrapRead(Method(movement, "Movimentar"), Field(movement, "velocidade"),
                Field(movement, "identificador"), hook("Movement"));
            game.Write(output);
        }
    }

    private static void Main(string[] args) {
        try {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Use: PatchTool.exe install GAME_FOLDER");
            string directory = Path.Combine(args[1], "PigeonsAttack_Data", "Managed");
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
