using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool {
    private const string OriginalHash = "4D66432141D17402F9898E19B5086ED137644026B13BEEC282E27616033E2C7C";
    private const string RuntimeFile = "MoventureTrainer.Runtime.dll";

    private static string Hash(string file) {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static MethodDefinition Method(TypeDefinition type, string name, int parameters) {
        return type.Methods.Single(method => method.Name == name && method.Parameters.Count == parameters);
    }

    private static MethodDefinition AddMethod(TypeDefinition type, string name, TypeReference returns) {
        var method = new MethodDefinition(name, MethodAttributes.Private | MethodAttributes.HideBySig, returns);
        type.Methods.Add(method);
        return method;
    }

    private static MethodDefinition AddScope(TypeDefinition character, MethodDefinition getMain) {
        var method = AddMethod(character, "TrainerHubIsMain", character.Module.TypeSystem.Boolean);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, getMain);
        il.Emit(OpCodes.Ceq);
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static void Protect(MethodDefinition method, MethodReference scope, MethodReference enabled, FieldReference killzone) {
        var il = method.Body.GetILProcessor();
        var original = method.Body.Instructions[0];
        var prefix = new[] {
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, scope),
            Instruction.Create(OpCodes.Brfalse, original), Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld, killzone), Instruction.Create(OpCodes.Brtrue, original),
            Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Call, enabled),
            Instruction.Create(OpCodes.Brfalse, original), Instruction.Create(OpCodes.Ret)
        };
        foreach (var instruction in prefix) il.InsertBefore(original, instruction);
    }

    private static void ProtectTarget(MethodDefinition method, OpCode loadTarget, MethodReference getMain, MethodReference enabled) {
        var il = method.Body.GetILProcessor();
        var original = method.Body.Instructions[0];
        var prefix = new[] {
            Instruction.Create(loadTarget), Instruction.Create(OpCodes.Brfalse, original),
            Instruction.Create(loadTarget), Instruction.Create(OpCodes.Call, getMain),
            Instruction.Create(OpCodes.Bne_Un, original), Instruction.Create(OpCodes.Ldc_I4_1),
            Instruction.Create(OpCodes.Call, enabled), Instruction.Create(OpCodes.Brfalse, original)
        };
        foreach (var instruction in prefix) il.InsertBefore(original, instruction);
        if (method.ReturnType.MetadataType == MetadataType.Boolean)
            il.InsertBefore(original, Instruction.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(original, Instruction.Create(OpCodes.Ret));
    }

    private static void ProtectImpactState(MethodDefinition method, MethodReference scope, MethodReference impact) {
        var il = method.Body.GetILProcessor();
        var original = method.Body.Instructions[0];
        foreach (var instruction in new[] {
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, scope),
            Instruction.Create(OpCodes.Brfalse, original), Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Call, impact), Instruction.Create(OpCodes.Brfalse, original),
            Instruction.Create(OpCodes.Ret)
        }) il.InsertBefore(original, instruction);
    }

    private static MethodDefinition JumpLimit(TypeDefinition character, FieldDefinition field, int minimum, MethodReference scope, MethodReference enabled) {
        var method = AddMethod(character, "TrainerHubRead_" + field.Name, character.Module.TypeSystem.Int32);
        var il = method.Body.GetILProcessor();
        var normal = Instruction.Create(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, scope);
        il.Emit(OpCodes.Brfalse, normal);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Call, enabled);
        il.Emit(OpCodes.Brfalse, normal);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ldc_I4, minimum);
        il.Emit(OpCodes.Bge, normal);
        il.Emit(OpCodes.Ldc_I4, minimum);
        il.Emit(OpCodes.Ret);
        il.Append(normal);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static void ReplaceRead(MethodDefinition method, FieldDefinition field, MethodDefinition replacement) {
        var instruction = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldfld
            && ((FieldReference)i.Operand).FullName == field.FullName);
        instruction.OpCode = OpCodes.Call;
        instruction.Operand = replacement;
    }

    private static void Patch(string original, string destination, string runtimeFile) {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(original));
        using (var asm = AssemblyDefinition.ReadAssembly(original, new ReaderParameters { AssemblyResolver = resolver }))
        using (var runtime = AssemblyDefinition.ReadAssembly(runtimeFile)) {
            var module = asm.MainModule;
            var character = module.GetType("Character");
            var getMain = Method(module.GetType("GameManager"), "get_MainCharacter", 0);
            var runtimeType = runtime.MainModule.GetType("MoventureTrainer.Runtime");
            var enabled = module.ImportReference(Method(runtimeType, "Enabled", 1));
            var scope = AddScope(character, getMain);
            var killzone = character.Fields.Single(f => f.Name == "forceNextFallSplat");
            Protect(Method(character, "Damage", 1), scope, enabled, killzone);
            Protect(Method(character, "Kill", 1), scope, enabled, killzone);
            ProtectTarget(Method(character, "DoSmash", 2), OpCodes.Ldarg_2, getMain, enabled);
            ProtectTarget(Method(character, "ApplyKickHit", 1), OpCodes.Ldarg_1, getMain, enabled);
            foreach (var name in new[] { "ApplyPunchKnockback", "BodyCrash", "HeadCrash", "Trip", "ApplyImpale", "ApplyShred", "ApplyCrush" }) {
                var target = character.Methods.Single(method => method.Name == name);
                ProtectTarget(target, OpCodes.Ldarg_0, getMain, enabled);
            }
            ProtectImpactState(Method(character, "ChangeState", 1), scope,
                module.ImportReference(Method(runtimeType, "IgnoreImpactState", 1)));

            var available = character.Fields.Single(f => f.Name == "jumpsAvailable");
            var maximum = character.Fields.Single(f => f.Name == "maxJumps");
            var readAvailable = JumpLimit(character, available, 1, scope, enabled);
            var airborne = Method(character, "State_Airborne", 0);
            ReplaceRead(airborne, available, readAvailable);
            ReplaceRead(airborne, maximum, JumpLimit(character, maximum, 2, scope, enabled));
            ReplaceRead(Method(character, "Jump", 0), available, readAvailable);
            ReplaceRead(Method(character, "ShortJump", 0), available, readAvailable);

            var update = Method(character, "Update", 0);
            var il = update.Body.GetILProcessor();
            var first = update.Body.Instructions[0];
            var alive = character.Fields.Single(f => f.Name == "isAlive");
            foreach (var instruction in new[] {
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, scope),
                Instruction.Create(OpCodes.Brfalse, first), Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldfld, alive), Instruction.Create(OpCodes.Brfalse, first),
                Instruction.Create(OpCodes.Call, module.ImportReference(Method(runtimeType, "Playable", 0)))
            }) il.InsertBefore(first, instruction);

            var root = module.Types.Single(t => t.Name == "<Module>");
            var initializer = root.Methods.SingleOrDefault(m => m.Name == ".cctor");
            var boot = Instruction.Create(OpCodes.Call, module.ImportReference(Method(runtimeType, "Boot", 0)));
            if (initializer == null) {
                initializer = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.Private
                    | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
                initializer.Body.Instructions.Add(boot);
                initializer.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                root.Methods.Add(initializer);
            } else initializer.Body.GetILProcessor().InsertBefore(initializer.Body.Instructions[0], boot);
            asm.Write(destination);
        }
    }

    private static void Main(string[] args) {
        try {
            if (args.Length != 2 || args[0] != "install") throw new ArgumentException("Uso: PatchTool.exe install pasta");
            var dir = Path.Combine(args[1], "Moventure_Data", "Managed");
            var target = Path.Combine(dir, "Moventure.dll");
            var backup = target + ".trainerhub-original";
            var marker = target + ".trainerhub-sha256";
            string currentHash = Hash(target);
            bool original = currentHash == OriginalHash;
            if (!original && (!File.Exists(marker) || File.ReadAllText(marker).Trim() != currentHash))
                throw new InvalidOperationException("Moventure assembly differs from the verified version.");
            if (!File.Exists(backup) && original) File.Copy(target, backup);
            if (!File.Exists(backup) || Hash(backup) != OriginalHash)
                throw new InvalidOperationException("The original backup is missing or invalid.");
            string runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RuntimeFile);
            string staged = target + ".new";
            Patch(backup, staged, runtime);
            File.Copy(staged, target, true);
            File.Delete(staged);
            File.Copy(runtime, Path.Combine(dir, RuntimeFile), true);
            File.WriteAllText(marker, Hash(target));
        } catch (Exception error) {
            Console.Error.WriteLine(error.Message);
            Environment.ExitCode = 1;
        }
    }
}
