using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ByteTrainer.Tinykin {
    internal static class TimerHooks {
        internal static bool Installed;
        internal static void Ensure() {
            if(Installed)return;
            var assembly=Assembly.Load("DLC-Challenges");
            var type=assembly.GetTypes().Single(t=>t.FullName.StartsWith("RaceManager+<Coroutine_Race>"));
            var method=type.GetMethod("MoveNext",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            if(method==null)throw new InvalidOperationException("Race timer method changed.");
            new Harmony("org.bytetrainer.tinykin.timer").Patch(method,transpiler:new HarmonyMethod(typeof(TimerHooks).GetMethod("Transpile",BindingFlags.Public|BindingFlags.Static)));
            Installed=true;
        }
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
            var list=instructions.ToList();int matches=0;
            for(int i=1;i+2<list.Count;i++) {
                var read=list[i-1].operand as FieldInfo;
                var delta=list[i].operand as MethodInfo;
                var write=list[i+2].operand as FieldInfo;
                if(list[i-1].opcode==OpCodes.Ldfld&&read!=null&&read.Name.StartsWith("<elapsedTime>")&&delta!=null&&delta.DeclaringType.FullName=="UnityEngine.Time"&&delta.Name=="get_deltaTime"&&list[i+1].opcode==OpCodes.Add&&list[i+2].opcode==OpCodes.Stfld&&write==read) {
                    list[i].operand=typeof(Runtime).GetMethod("RaceDelta");matches++;
                }
            }
            if(matches!=1)throw new InvalidOperationException("Race timer layout changed; no timer patch was applied.");
            return list;
        }
    }
}
