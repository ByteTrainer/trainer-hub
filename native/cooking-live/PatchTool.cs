using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchTool
{
    private const string OriginalHash = "5835E87C5653CB13C742BA9797658B7881206FD5807D90C95DA25534533D4FE4";
    private const string RuntimeFile = "CookingLiveTrainer.Runtime.dll";

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

    private static void SkipWhen(MethodDefinition method, MethodReference condition)
    {
        var il = method.Body.GetILProcessor();
        var start = method.Body.Instructions[0];
        il.InsertBefore(start, Instruction.Create(OpCodes.Call, condition));
        il.InsertBefore(start, Instruction.Create(OpCodes.Brfalse, start));
        il.InsertBefore(start, Instruction.Create(OpCodes.Ret));
    }

    private static void ScaleReturns(MethodDefinition method, MethodReference scale)
    {
        // Replace each ret in place so branches that target it also scale the value.
        var il = method.Body.GetILProcessor();
        foreach (var ret in method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
        {
            ret.OpCode = OpCodes.Call;
            ret.Operand = scale;
            il.InsertAfter(ret, Instruction.Create(OpCodes.Ret));
        }
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
            var hooks = runtime.MainModule.GetType("ByteTrainer.CookingLive.Runtime");
            Func<string, int, MethodReference> hook = (name, count) => module.ImportReference(Method(hooks, name, count));

            var update = Method(module.GetType("Cooking.Core.State.GameState"), "Update", 0);
            var start = update.Body.Instructions[0];
            update.Body.GetILProcessor().InsertBefore(start, Instruction.Create(OpCodes.Ldarg_0));
            update.Body.GetILProcessor().InsertBefore(start, Instruction.Create(OpCodes.Call, hook("Tick", 1)));

            var energy = Method(module.GetType("Cooking.Core.Controller.EnergyController"), "TrySpendEnergy", 0);
            SkipWhen(energy, hook("SkipEnergySpend", 0));

            var combo = module.GetType("Cooking.Manager.Kitchen.ComboManager");
            SkipWhen(Method(combo, "Update", 0), hook("PauseCombo", 0));

            var comboView = module.GetType("Cooking.Kitchen.UI.ComboView");
            var playBar = Method(comboView, "PlayBarAnimation", 2);
            var barField = comboView.Fields.Single(field => field.Name == "CooldownBar");
            var barRet = playBar.Body.Instructions.Last();
            var barIl = playBar.Body.GetILProcessor();
            barIl.InsertBefore(barRet, Instruction.Create(OpCodes.Ldarg_0));
            barIl.InsertBefore(barRet, Instruction.Create(OpCodes.Ldfld, barField));
            barIl.InsertBefore(barRet, Instruction.Create(OpCodes.Call, hook("TrackComboBar", 1)));
            var barUpdate = Method(module.GetType(barField.FieldType.FullName), "Update", 0);
            var barStart = barUpdate.Body.Instructions[0];
            barUpdate.Body.GetILProcessor().InsertBefore(barStart, Instruction.Create(OpCodes.Ldarg_0));
            barUpdate.Body.GetILProcessor().InsertBefore(barStart, Instruction.Create(OpCodes.Call, hook("UpdateComboBar", 1)));

            var level = module.GetType("Cooking.Manager.Kitchen.LevelProcessManager");
            var complete = Method(level, "StartCompleteLevel", 1);
            var completeIl = complete.Body.GetILProcessor();
            var completeStart = complete.Body.Instructions[0];
            var won = module.GetType("Cooking.Core.Controller.LevelFinishReason").Fields.Single(field => field.Name == "LevelWon");
            foreach (var instruction in new[]
            {
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Call, hook("IsQuickWinDispatch", 1)),
                Instruction.Create(OpCodes.Brfalse, completeStart),
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldc_I4_1),
                Instruction.Create(OpCodes.Ldc_I4, Convert.ToInt32(won.Constant)),
                Instruction.Create(OpCodes.Call, Method(level, "CompleteLevel", 2)),
                Instruction.Create(OpCodes.Ret)
            }) completeIl.InsertBefore(completeStart, instruction);

            var visitor = module.GetType("Cooking.Kitchen.Visitor.VisitorOrderController");
            var completeItem = Method(visitor, "CompleteItem", 3);
            var itemCompleted = module.GetType("Cooking.Events.Visitor_ItemCompleted");
            var notify = completeItem.Body.Instructions.Single(i => i.Operand is GenericInstanceMethod &&
                ((GenericInstanceMethod)i.Operand).Name == "Fire" &&
                ((GenericInstanceMethod)i.Operand).GenericArguments[0].FullName == itemCompleted.FullName);
            var visitorIl = completeItem.Body.GetILProcessor();
            // Keep the last-dish flag across the native event, then queue completion once its listeners have run.
            var isLastDish = itemCompleted.Fields.Single(field => field.Name == "IsLastDish");
            var lastDish = new VariableDefinition(module.TypeSystem.Boolean);
            completeItem.Body.Variables.Add(lastDish);
            visitorIl.InsertBefore(notify, Instruction.Create(OpCodes.Dup));
            visitorIl.InsertBefore(notify, Instruction.Create(OpCodes.Ldfld, isLastDish));
            visitorIl.InsertBefore(notify, Instruction.Create(OpCodes.Stloc, lastDish));
            var afterNotify = notify.Next;
            visitorIl.InsertBefore(afterNotify, Instruction.Create(OpCodes.Ldloc, lastDish));
            visitorIl.InsertBefore(afterNotify, Instruction.Create(OpCodes.Call, hook("QueueQuickWin", 1)));

            var foodCoins = Method(level, "CalculateMoneyForFoods", 1);
            var comboCoins = Method(combo, "GetMoneyForStep", 1);
            ScaleReturns(foodCoins, hook("ScaleCoins", 1));
            ScaleReturns(comboCoins, hook("ScaleCoins", 1));

            var timer = module.GetType("Cooking.Kitchen.PlaceFry.CallbackTimer");
            var place = module.GetType("Cooking.Kitchen.BaseCookingPlace");
            var startCooking = Method(place, "TryStartCooking", 4);
            var overcooking = place.Fields.Single(field => field.Name == "_isOvercooking");
            var cookingIl = startCooking.Body.GetILProcessor();
            var setTimer = startCooking.Body.Instructions.Single(i => Equals(i.Operand, Method(place, "set_Timer", 1)));
            var setup = startCooking.Body.Instructions.Single(i => Equals(i.Operand, Method(timer, "SetupCallbackAction", 2)));
            // Tag the exact timer with the game's overcook flag and the foods its callback will produce.
            cookingIl.InsertBefore(setTimer, Instruction.Create(OpCodes.Dup));
            var afterSetup = setup.Next;
            cookingIl.InsertBefore(afterSetup, Instruction.Create(OpCodes.Ldarg_0));
            cookingIl.InsertBefore(afterSetup, Instruction.Create(OpCodes.Call, Method(place, "get_Timer", 0)));
            foreach (var insertion in new[] { setTimer, afterSetup })
            {
                cookingIl.InsertBefore(insertion, Instruction.Create(OpCodes.Ldarg_1));
                cookingIl.InsertBefore(insertion, Instruction.Create(OpCodes.Ldarg_2));
                cookingIl.InsertBefore(insertion, Instruction.Create(OpCodes.Ldarg_0));
                cookingIl.InsertBefore(insertion, Instruction.Create(OpCodes.Ldfld, overcooking));
                cookingIl.InsertBefore(insertion, Instruction.Create(OpCodes.Call, hook("TrackCookingTimer", 4)));
            }

            var callback = timer.NestedTypes.Single(type => type.Name == "<>c__DisplayClass18_0");
            var tick = Method(callback, "<UpdateCallbackAction>b__0", 0);
            var nativeTick = tick.Body.Instructions.Single(instruction => instruction.Operand is MethodReference &&
                ((MethodReference)instruction.Operand).DeclaringType.FullName == "MG.Utils.CustomTimer" &&
                ((MethodReference)instruction.Operand).Name == "Tick");
            var il = tick.Body.GetILProcessor();
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldfld, callback.Fields.Single(field => field.Name == "<>4__this")));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldfld, timer.Fields.Single(field => field.Name == "_owner")));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldfld, callback.Fields.Single(field => field.Name == "<>4__this")));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldfld, timer.Fields.Single(field => field.Name == "_timer")));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Ldfld, callback.Fields.Single(field => field.Name == "<>4__this")));
            il.InsertBefore(nativeTick, Instruction.Create(OpCodes.Call, hook("TimerDelta", 4)));

            var longBranches = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(OpCode))
                .Select(field => (OpCode)field.GetValue(null)).ToDictionary(code => code.Name);
            foreach (var method in new[] { update, energy, Method(combo, "Update", 0), foodCoins, comboCoins, startCooking, tick, completeItem })
                foreach (var instruction in method.Body.Instructions)
                    if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                        instruction.OpCode = longBranches[instruction.OpCode.Name.Substring(0, instruction.OpCode.Name.Length - 2)];

            assembly.Write(destination);
        }
    }

    private static void Main(string[] args)
    {
        try
        {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Usage: PatchTool.exe install directory");
            var folder = Path.Combine(args[1], "cl_Data", "Managed");
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
