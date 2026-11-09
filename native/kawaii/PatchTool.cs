using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool {
    private const string OriginalHash = "1CF701B7578F5A45598D80FAFD82D62F35C6E954142BBADBC5E5B772FBA40638";
    private const string RuntimeFile = "KawaiiTrainer.Runtime.dll";

    private static string Hash(string file) {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static MethodDefinition Method(TypeDefinition type, string name, int parameters) {
        return type.Methods.Single(method => method.Name == name && method.Parameters.Count == parameters);
    }

    private static void Prefix(MethodDefinition method, params Instruction[] instructions) {
        var il = method.Body.GetILProcessor();
        var entry = method.Body.Instructions[0];
        foreach (var instruction in instructions) il.InsertBefore(entry, instruction);
    }

    private static void Patch(string original, string output, string runtimeFile) {
        using (var game = AssemblyDefinition.ReadAssembly(original))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile)) {
            var module = game.MainModule;
            var hooks = runtime.MainModule.GetType("ByteTrainer.Kawaii.Runtime");
            Func<string, int, MethodReference> hook = (name, count) => module.ImportReference(Method(hooks, name, count));
            Prefix(Method(module.GetType("KawaiiAutoSave"), "Awake", 0), Instruction.Create(OpCodes.Call, hook("Boot", 0)));

            var ui = module.GetType("ControladorUI");
            var behaviour = module.GetType("Pippin.PippinBehaviour");
            Prefix(Method(ui, "Update", 0),
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, ui.Fields.Single(f => f.Name == "_controlador")),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, behaviour.Fields.Single(f => f.Name == "partidaComecou")),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, behaviour.Fields.Single(f => f.Name == "partidaPausada")),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, behaviour.Fields.Single(f => f.Name == "partidaAcabou")),
                Instruction.Create(OpCodes.Call, hook("ObserveSolo", 5)));

            var damage = Method(module.GetType("Pippin.kawaiideathudesu.idol.Idol"), "retirarVida", 1);
            Prefix(damage, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, hook("BlockDamage", 1)),
                Instruction.Create(OpCodes.Brfalse, damage.Body.Instructions[0]), Instruction.Create(OpCodes.Ret));

            var rank = Method(module.GetType("Pippin.Steam.SteamRank"), "_EventoGameOverScoreRanks", 2);
            Prefix(rank, Instruction.Create(OpCodes.Call, hook("SuppressRank", 0)),
                Instruction.Create(OpCodes.Brfalse, rank.Body.Instructions[0]), Instruction.Create(OpCodes.Ret));
            game.Write(output);
        }
    }

    private static void Main(string[] args) {
        try {
            if (args.Length != 2 || args[0] != "install") throw new ArgumentException("Use: PatchTool.exe install GAME_FOLDER");
            string directory = Path.Combine(args[1], "kdd_Data", "Managed");
            string target = Path.Combine(directory, "Assembly-CSharp.dll");
            string backup = target + ".trainerhub-original", marker = target + ".trainerhub-sha256";
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
        } catch (Exception error) {
            Console.Error.WriteLine(error.Message);
            Environment.ExitCode = 1;
        }
    }
}
