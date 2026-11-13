using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool
{
    private const string OriginalHash = "927BBB878BD798F76B6528773CE42273C8613B8721DEB617959CE6D124CE3172";
    private const string RuntimeFile = "MaraTrainer.Runtime.dll";

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
            var hooks = runtime.MainModule.GetType("ByteTrainer.Mara.Runtime");
            Func<string, int, MethodReference> hook = (name, count) => module.ImportReference(Method(hooks, name, count));

            var player = module.GetType("Chibig.PlayerCharacterController");
            var inputs = Method(player, "SetInputs", 1);
            var jumpInput = At(inputs, 0x04ae);
            Before(inputs, jumpInput, Instruction.Create(OpCodes.Ldc_I4_0),
                Instruction.Create(OpCodes.Call, hook("Enabled", 1)),
                Instruction.Create(OpCodes.Brtrue, At(inputs, 0x04b6)));
            ExpandBranches(inputs);
            var velocity = Method(player, "UpdateVelocity", 2);
            var enabled = hook("Enabled", 1);
            var status = module.GetType("Chibig.PlayerStatus");
            Guard(status, "EnergyDecrease", 2, 7, enabled);
            Guard(status, "EnergyWaterDecrease", 1, 7, enabled);
            Guard(status, "HungerDecrease", 1, 8, enabled);
            var world = module.GetType("Chibig.World");
            var clock = Method(world, "Update", 0);
            Before(clock, clock.Body.Instructions[0], Instruction.Create(OpCodes.Call, hook("Tick", 0)));
            Guard(world, "Update", 0, 9, enabled);

            var scale = hook("ScaleSpeed", 1);
            var jumpCheck = At(velocity, 0x03a2);
            Before(velocity, jumpCheck, Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Call, enabled),
                Instruction.Create(OpCodes.Brtrue, At(velocity, 0x03ea)));
            Before(velocity, velocity.Body.Instructions[0], Instruction.Create(OpCodes.Call, hook("Tick", 0)));
            foreach (var load in velocity.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldfld &&
                new[] { "groundSpeed", "airMoveSpeed", "swimSpeed" }.Contains(((FieldReference)i.Operand).Name)).ToArray())
                velocity.Body.GetILProcessor().InsertAfter(load, Instruction.Create(OpCodes.Call, scale));
            ExpandBranches(velocity);

            var recipes = module.GetType("Chibig.PlayerRecipes");
            var check = Method(recipes, "RecipeCheckRequirements", 1);
            Before(check, At(check, 0), Instruction.Create(OpCodes.Ldc_I4_2), Instruction.Create(OpCodes.Call, enabled),
                Instruction.Create(OpCodes.Brtrue, At(check, 0x0059)));

            var crafting = module.GetType("Chibig.UICrafting");
            var accept = Method(crafting, "Accept", 0);
            Before(accept, At(accept, 0x0016), Instruction.Create(OpCodes.Ldc_I4_2),
                Instruction.Create(OpCodes.Call, enabled), Instruction.Create(OpCodes.Brtrue, At(accept, 0x0023)));
            ExpandBranches(accept);

            var entry = module.GetType("Chibig.EntryRecipe");
            foreach (var set in entry.Methods.Where(method => method.Name == "Set"))
            {
                var compare = set.Body.Instructions.Single(instruction =>
                    instruction.OpCode == OpCodes.Blt_S &&
                    instruction.Previous.OpCode == OpCodes.Ldloc_S &&
                    instruction.Previous.Previous.OpCode == OpCodes.Ldloc_S);
                var required = compare.Previous;
                var actual = required.Previous;
                Before(set, actual, Instruction.Create(OpCodes.Ldc_I4_2),
                    Instruction.Create(OpCodes.Call, enabled), Instruction.Create(OpCodes.Brtrue, compare.Next));
                ExpandBranches(set);
            }

            var redeem = Method(module.GetType("Chibig.Recipe"), "RedeemRequirements", 0);
            Before(redeem, At(redeem, 0), Instruction.Create(OpCodes.Ldc_I4_2), Instruction.Create(OpCodes.Call, enabled),
                Instruction.Create(OpCodes.Brtrue, At(redeem, 0x0056)));

            var inventory = module.GetType("Chibig.PlayerInventory");
            var item = inventory.NestedTypes.Single(type => type.Name == "InventoryItem");
            var quantity = item.Fields.Single(field => field.Name == "quantity");
            var key = item.Fields.Single(field => field.Name == "key");
            var fill = new MethodDefinition("ByteTrainerFill", Mono.Cecil.MethodAttributes.Private, module.TypeSystem.Void);
            item.Methods.Add(fill);
            var il = fill.Body.GetILProcessor();
            var done = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Call, enabled);
            il.Emit(OpCodes.Brfalse, done);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, key);
            il.Emit(OpCodes.Ldc_I4, 52);
            il.Emit(OpCodes.Beq, done);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, Method(item, "IsUnique", 0));
            il.Emit(OpCodes.Brtrue, done);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, 99);
            il.Emit(OpCodes.Stfld, quantity);
            il.Append(done);
            var ctor = Method(item, ".ctor", 2);
            Before(ctor, At(ctor, 0x001f), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, fill));
            var add = Method(item, "ItemAdd", 1);
            var addEnd = At(add, 0x003a);
            addEnd.OpCode = OpCodes.Ldarg_0;
            add.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Call, fill));
            add.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));

            var remove = Method(inventory, "RemoveItem", 5);
            // Keep the original availability checks. Report how many items the action could consume.
            var normal = At(remove, 0x0025);
            var protect = Instruction.Create(OpCodes.Ldc_I4_1);
            var hasItem = At(remove, 0x0020);
            Before(remove, normal, protect, Instruction.Create(OpCodes.Call, enabled),
                Instruction.Create(OpCodes.Brfalse, normal), Instruction.Create(OpCodes.Ldloc_0),
                Instruction.Create(OpCodes.Ldarg_2), Instruction.Create(OpCodes.Call,
                    module.ImportReference(typeof(Math).GetMethod("Min", new[] { typeof(int), typeof(int) }))),
                Instruction.Create(OpCodes.Ret));
            hasItem.Operand = protect;
            ExpandBranches(remove);

            var fishing = module.GetType("Chibig.CanvasFishing");
            var fishingInput = Method(fishing, "SetInputs", 1);
            var fishingStep = fishing.Fields.Single(field => field.Name == "fishingStep");
            var originalInput = At(fishingInput, 0x000b);
            var reel = Instruction.Create(OpCodes.Ldarg_0);
            var automate = Instruction.Create(OpCodes.Call, hook("Tick", 0));
            Before(fishingInput, originalInput, automate,
                Instruction.Create(OpCodes.Ldc_I4_6), Instruction.Create(OpCodes.Call, enabled),
                Instruction.Create(OpCodes.Brfalse, originalInput),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, fishingStep),
                Instruction.Create(OpCodes.Brtrue, reel),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldc_I4_1),
                Instruction.Create(OpCodes.Call, Method(fishing, "CaughtFish", 1)), Instruction.Create(OpCodes.Ret),
                reel, Instruction.Create(OpCodes.Ldfld, fishingStep), Instruction.Create(OpCodes.Ldc_I4_1),
                Instruction.Create(OpCodes.Bne_Un, originalInput),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldc_I4_2),
                Instruction.Create(OpCodes.Stfld, fishingStep),
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Call, Method(fishing, "ConfigureFishingStep", 0)),
                Instruction.Create(OpCodes.Ret));
            ExpandBranches(fishingInput);

            var crop = module.GetType("Chibig.Crop");
            var refresh = Method(crop, "UpdateCrop", 0);
            var start = At(refresh, 0);
            var growth = crop.Fields.Single(field => field.Name == "growth");
            Before(refresh, start, Instruction.Create(OpCodes.Ldc_I4_4), Instruction.Create(OpCodes.Call, enabled),
                Instruction.Create(OpCodes.Brfalse, start), Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldfld, growth), Instruction.Create(OpCodes.Ldc_I4_2),
                Instruction.Create(OpCodes.Sub), Instruction.Create(OpCodes.Ldc_I4_1),
                Instruction.Create(OpCodes.Bgt_Un, start), Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldc_I4_4), Instruction.Create(OpCodes.Stfld, growth));

            var map = module.GetType("Chibig.UIMap");
            var open = Method(map, "Open", 0);
            var highlight = hook("HighlightObjective", 4);
            var worldMap = map.Fields.Single(field => field.Name == "MapBoat");
            var localMap = map.Fields.Single(field => field.Name == "MapQalis");
            var marker = map.Fields.Single(field => field.Name == "highlight");
            var close = Method(map, "Close", 1);
            Before(close, close.Body.Instructions[0], Instruction.Create(OpCodes.Call, hook("ClearObjectives", 0)));
            foreach (var ret in open.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
            {
                ret.OpCode = OpCodes.Ldarg_0;
                var mapIl = open.Body.GetILProcessor();
                var loadMap = Instruction.Create(OpCodes.Ldarg_0);
                var loadWorldMap = Instruction.Create(OpCodes.Ldfld, worldMap);
                var call = Instruction.Create(OpCodes.Call, highlight);
                mapIl.InsertAfter(ret, loadMap);
                mapIl.InsertAfter(loadMap, loadWorldMap);
                var loadLocalOwner = Instruction.Create(OpCodes.Ldarg_0);
                var loadLocalMap = Instruction.Create(OpCodes.Ldfld, localMap);
                mapIl.InsertAfter(loadWorldMap, loadLocalOwner);
                mapIl.InsertAfter(loadLocalOwner, loadLocalMap);
                var loadMarkerOwner = Instruction.Create(OpCodes.Ldarg_0);
                var loadMarker = Instruction.Create(OpCodes.Ldfld, marker);
                mapIl.InsertAfter(loadLocalMap, loadMarkerOwner);
                mapIl.InsertAfter(loadMarkerOwner, loadMarker);
                mapIl.InsertAfter(loadMarker, call);
                mapIl.InsertAfter(call, Instruction.Create(OpCodes.Ret));
            }
            ExpandBranches(open);

            var energy = module.GetType("Chibig.KoaEnergy");
            var gui = new MethodDefinition("OnGUI", Mono.Cecil.MethodAttributes.Private, module.TypeSystem.Void);
            energy.Methods.Add(gui);
            var guiIl = gui.Body.GetILProcessor();
            guiIl.Emit(OpCodes.Call, hook("DrawObjective", 0));
            guiIl.Emit(OpCodes.Ret);

            assembly.Write(destination);
        }
    }

    private static void Guard(TypeDefinition type, string name, int count, int option, MethodReference enabled)
    {
        var method = Method(type, name, count);
        var start = method.Body.Instructions[0];
        Before(method, start, Instruction.Create(OpCodes.Ldc_I4, option),
            Instruction.Create(OpCodes.Call, enabled), Instruction.Create(OpCodes.Brfalse, start),
            Instruction.Create(OpCodes.Ret));
    }

    private static Instruction At(MethodDefinition method, int offset)
    {
        return method.Body.Instructions.Single(i => i.Offset == offset);
    }

    private static void Before(MethodDefinition method, Instruction target, params Instruction[] instructions)
    {
        var il = method.Body.GetILProcessor();
        foreach (var instruction in instructions) il.InsertBefore(target, instruction);
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
            var folder = Path.Combine(args[1], "Summer in Mara_Data", "Managed");
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
