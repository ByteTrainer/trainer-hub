using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool
{
    private const string OriginalHash = "C613F3C95ABCC7D8DB05F04C9695FF6A542C06E82AE66B60D462A28ACD8DFFF4";
    private const string RuntimeFile = "CatQuestTrainer.Runtime.dll";

    private static string Hash(string file)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static MethodDefinition Method(TypeDefinition type, string name, int count)
    {
        return type.Methods.Single(method => method.Name == name && method.Parameters.Count == count);
    }

    private static bool IsCall(Instruction instruction, string type, string name)
    {
        var method = instruction.Operand as MethodReference;
        return method != null && method.DeclaringType.FullName == type && method.Name == name;
    }

    private static void HookDamage(MethodDefinition method, MethodReference hook)
    {
        // Both paths calculate base damage and then apply the target's defence.
        var firstMax = method.Body.Instructions.First(i => IsCall(i, "UnityEngine.Mathf", "Max"));
        var il = method.Body.GetILProcessor();
        var storeDamage = firstMax.Next;
        il.InsertBefore(storeDamage, Instruction.Create(OpCodes.Ldloc_0));
        il.InsertBefore(storeDamage, Instruction.Create(OpCodes.Ldloc_1));
        il.InsertBefore(storeDamage, Instruction.Create(OpCodes.Call, hook));
    }

    private static void HookExperience(MethodDefinition method, MethodReference hook)
    {
        var xp = method.Body.Instructions.Single(i =>
            i.Operand is FieldReference && ((FieldReference)i.Operand).FullName == "UniRx.IntReactiveProperty ProgressionData::xp");
        var add = method.Body.Instructions.SkipWhile(i => i != xp).First(i => i.OpCode == OpCodes.Add);
        add.OpCode = OpCodes.Call;
        add.Operand = hook;
    }

    private static void Patch(string original, string destination, string runtimeFile)
    {
        using (var resolver = new DefaultAssemblyResolver())
        {
            resolver.AddSearchDirectory(Path.GetDirectoryName(original));
            PatchAssembly(original, destination, runtimeFile, resolver);
        }
    }

    private static void PatchAssembly(string original, string destination, string runtimeFile, IAssemblyResolver resolver)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(original, new ReaderParameters { AssemblyResolver = resolver }))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile))
        {
            var module = assembly.MainModule;
            var hooks = runtime.MainModule.GetType("ByteTrainer.CatQuest.Runtime");
            Func<string, int, MethodReference> hook = (name, count) => module.ImportReference(Method(hooks, name, count));

            var update = Method(module.GetType("Game"), "Update", 0);
            var start = update.Body.Instructions[0];
            var il = update.Body.GetILProcessor();
            il.InsertBefore(start, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(start, Instruction.Create(OpCodes.Call, hook("Tick", 1)));

            var melee = Method(module.GetType("CombatManager_v2"), "ResolveCombat", 1);
            var magic = Method(module.GetType("EffectManager"), "<Start>m__1", 1);
            HookDamage(melee, hook("ScaleDamage", 3));
            HookDamage(magic, hook("ScaleDamage", 3));

            var movement = Method(module.GetType("Player"), "FixedUpdate", 0);
            var speed = movement.Body.Instructions.Single(i => IsCall(i, "Stat`1<System.Single>", "get_Value"));
            movement.Body.GetILProcessor().InsertAfter(speed, Instruction.Create(OpCodes.Call, hook("ScaleSpeed", 1)));

            var cast = Method(module.GetType("SkillManager/<Start>c__AnonStorey1"), "<>m__0", 1);
            var spend = cast.Body.Instructions.Single(i => i.OpCode == OpCodes.Sub);
            spend.OpCode = OpCodes.Call;
            spend.Operand = hook("SpendMana", 2);

            var pickup = Method(module.GetType("PlayerManager/<Start>c__AnonStorey1"), "<>m__2", 1);
            var quest = Method(module.GetType("QuestManager/<Initialize>c__AnonStorey0"), "<>m__6", 1);
            var mewReward = Method(module.GetType("MewGameEnd/<Start>c__AnonStorey0"), "<>m__1", 1);
            HookExperience(pickup, hook("AddExperience", 2));
            HookExperience(quest, hook("AddExperience", 2));
            HookExperience(mewReward, hook("AddExperience", 2));

            assembly.Write(destination);
        }
    }

    private static void Main(string[] args)
    {
        try
        {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Usage: PatchTool.exe install directory");
            var folder = Path.Combine(args[1], "Cat Quest_Data", "Managed");
            var target = Path.Combine(folder, "Assembly-CSharp.dll");
            var backup = target + ".trainerhub-original";
            var marker = target + ".trainerhub-sha256";
            var currentHash = Hash(target);
            if (currentHash != OriginalHash && (!File.Exists(marker) || File.ReadAllText(marker).Trim() != currentHash))
                throw new InvalidOperationException("Game assembly differs from the verified version.");
            if (!File.Exists(backup) && currentHash == OriginalHash) File.Copy(target, backup);
            if (!File.Exists(backup) || Hash(backup) != OriginalHash)
                throw new InvalidOperationException("Original backup missing or invalid.");

            var runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RuntimeFile);
            var staged = target + ".new";
            Patch(backup, staged, runtime);
            File.Copy(staged, target, true);
            File.Delete(staged);
            File.Copy(runtime, Path.Combine(folder, RuntimeFile), true);
            File.WriteAllText(marker, Hash(target));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
    }
}
