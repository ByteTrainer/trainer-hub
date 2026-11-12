using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool
{
    private const string OriginalHash = "7B0452E18F9886DD3503B3832D95F7A7F34A7478A158CA0137E464C118740881";
    private const string RuntimeFile = "TuncheTrainer.Runtime.dll";

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

    private static void Protect(MethodDefinition method, MethodReference condition)
    {
        var il = method.Body.GetILProcessor();
        var start = method.Body.Instructions[0];
        il.InsertBefore(start, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(start, Instruction.Create(OpCodes.Call, condition));
        il.InsertBefore(start, Instruction.Create(OpCodes.Brfalse, start));
        if (method.ReturnType.MetadataType == MetadataType.Boolean)
            il.InsertBefore(start, Instruction.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(start, Instruction.Create(OpCodes.Ret));
    }

    private static void Patch(string original, string destination, string runtimeFile)
    {
        using (var resolver = new DefaultAssemblyResolver())
        {
            resolver.AddSearchDirectory(Path.GetDirectoryName(original));
            Patch(original, destination, runtimeFile, resolver);
        }
    }

    private static void Patch(string original, string destination, string runtimeFile, IAssemblyResolver resolver)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(original, new ReaderParameters { AssemblyResolver = resolver }))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile))
        {
            var module = assembly.MainModule;
            var hooks = runtime.MainModule.GetType("ByteTrainer.Tunche.Runtime");
            Func<string, int, MethodReference> hook = (name, count) => module.ImportReference(Method(hooks, name, count));

            var update = Method(module.GetType("GameManager"), "Update", 0);
            var start = update.Body.Instructions[0];
            var updateIl = update.Body.GetILProcessor();
            updateIl.InsertBefore(start, Instruction.Create(OpCodes.Ldarg_0));
            updateIl.InsertBefore(start, Instruction.Create(OpCodes.Call, hook("Tick", 1)));

            var player = module.GetType("PlayerController");
            Protect(Method(player, "DoDamage", 3), hook("ProtectHealth", 1));
            Protect(Method(player, "DecreaseHP", 1), hook("ProtectHealth", 1));
            Protect(Method(player, "DecreaseMP", 1), hook("ProtectMana", 1));

            var combo = module.GetType("CharacterStylishComboController");
            var comboUpdate = Method(combo, "Update", 0);
            var comboIl = comboUpdate.Body.GetILProcessor();
            var timerActive = combo.Fields.Single(field => field.Name == "comboTimerActivated");
            var controller = combo.Fields.Single(field => field.Name == "controller");
            var checkTimer = comboUpdate.Body.Instructions.Single(i => i.Operand == timerActive);
            var afterTimer = (Instruction)checkTimer.Next.Operand;
            var timerStart = checkTimer.Previous;
            comboIl.InsertBefore(timerStart, Instruction.Create(OpCodes.Ldarg_0));
            comboIl.InsertBefore(timerStart, Instruction.Create(OpCodes.Ldfld, controller));
            comboIl.InsertBefore(timerStart, Instruction.Create(OpCodes.Call, hook("FreezeCombo", 1)));
            comboIl.InsertBefore(timerStart, Instruction.Create(OpCodes.Brtrue, afterTimer));
            ExpandBranches(comboUpdate);

            foreach (var name in new[] { "EnemyController", "EnemyAppend", "DarkQaruComplexClonController" })
            {
                var takeDamage = Method(module.GetType(name), "TakeDamage", 7);
                var damageCalls = takeDamage.Body.Instructions.Where(i => i.OpCode == OpCodes.Callvirt &&
                    i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName == "EnemyController" &&
                    ((MethodReference)i.Operand).Name == "DoDamage").ToArray();
                if (damageCalls.Length != (name == "EnemyController" ? 1 : 2))
                    throw new InvalidOperationException("Unexpected damage calls in " + name);

                var il = takeDamage.Body.GetILProcessor();
                var appliedDamage = new VariableDefinition(module.TypeSystem.Single);
                takeDamage.Body.Variables.Add(appliedDamage);
                // The original damage number follows the relevant hit, before any later forwarded hit.
                var displayedDamage = damageCalls[name == "EnemyAppend" ? 1 : 0].Next.Next;
                if (name == "EnemyAppend") displayedDamage = displayedDamage.Next;
                if (displayedDamage.OpCode != (name == "EnemyAppend" ? OpCodes.Ldloc_1 : OpCodes.Ldloc_0))
                    throw new InvalidOperationException("Unexpected damage number in " + name);
                if (name == "EnemyAppend")
                {
                    displayedDamage.Next.OpCode = OpCodes.Nop;
                    displayedDamage.Next.Operand = null;
                }
                displayedDamage.OpCode = OpCodes.Ldloc;
                displayedDamage.Operand = appliedDamage;

                foreach (var call in damageCalls)
                {
                    call.OpCode = OpCodes.Ldarg_2;
                    call.Operand = null;
                    var apply = Instruction.Create(OpCodes.Call, hook("DamageEnemy", 5));
                    il.InsertAfter(call, apply);
                    il.InsertAfter(apply, Instruction.Create(OpCodes.Stloc, appliedDamage));
                }
                ExpandBranches(takeDamage);
            }

            assembly.Write(destination);
        }
    }

    private static void ExpandBranches(MethodDefinition method)
    {
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)).ToDictionary(code => code.Name);
        foreach (var instruction in method.Body.Instructions)
            if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                instruction.OpCode = codes[instruction.OpCode.Name.Substring(0, instruction.OpCode.Name.Length - 2)];
    }
    private static void Main(string[] args)
    {
        try
        {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Usage: PatchTool.exe install directory");
            var folder = Path.Combine(args[1], "Tunche_Data", "Managed");
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

