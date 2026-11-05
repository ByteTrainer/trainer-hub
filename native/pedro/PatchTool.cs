using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

class PatchTool {
 const string OriginalHash="B88961ED24CB207DB6C4752A9C0A92EDBF88EAFA817FDD2EDE40DF322211EE8D";
 static string Hash(string p) { using(var s=File.OpenRead(p)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-",""); }
 static void Main(string[] a) { try { Run(a); } catch(Exception e) { Console.Error.WriteLine(e.Message); Environment.ExitCode=1; } }
 static void Run(string[] a) {
  if(a.Length!=2) throw new Exception("Uso: PatchTool.exe install|restore|verify pasta-do-jogo");
  string dir=Path.Combine(a[1],"My Friend Pedro - Blood Bullets Bananas_Data","Managed");
  string target=Path.Combine(dir,"Assembly-UnityScript.dll"), backup=target+".pedro-trainer-original", stamp=target+".pedro-trainer-sha256";
  if(a[0]=="verify") { using(var asm=AssemblyDefinition.ReadAssembly(target)) Console.WriteLine(asm.MainModule.AssemblyReferences.Any(r=>r.Name=="PedroTrainer.Runtime")?"Trainer instalado":"Trainer ausente"); return; }
  if(Process.GetProcessesByName("My Friend Pedro - Blood Bullets Bananas").Length>0) throw new Exception("Feche o jogo antes de instalar ou restaurar.");
  if(a[0]=="restore") {
   if(!File.Exists(backup)||Hash(backup)!=OriginalHash) throw new Exception("Backup original ausente ou inválido.");
   if(!File.Exists(stamp)||Hash(target)!=File.ReadAllText(stamp).Trim()) throw new Exception("O jogo foi alterado após a instalação. Nenhum arquivo foi sobrescrito.");
   File.Copy(backup,target,true); File.Delete(stamp);
   string runtime=Path.Combine(dir,"PedroTrainer.Runtime.dll"); if(File.Exists(runtime)) File.Delete(runtime);
   string protocol=Path.Combine(dir,"PedroTrainer.protocol"); if(File.Exists(protocol)) File.Delete(protocol);
   Console.WriteLine("Arquivos originais restaurados."); return;
  }
  if(a[0]!="install") throw new Exception("Comando desconhecido.");
  bool ours=File.Exists(stamp)&&Hash(target)==File.ReadAllText(stamp).Trim();
  if(!ours && Hash(target)!=OriginalHash) throw new Exception("Esta versão do jogo é diferente da versão verificada. Instalação cancelada para preservar seus arquivos.");
  if(ours && (!File.Exists(backup)||Hash(backup)!=OriginalHash)) throw new Exception("Backup original ausente ou inválido.");
  if(File.Exists(backup)&&Hash(backup)!=OriginalHash) throw new Exception("Já existe um backup diferente. Nenhum arquivo foi alterado.");
  var resolver=new DefaultAssemblyResolver(); resolver.AddSearchDirectory(dir); resolver.AddSearchDirectory(AppDomain.CurrentDomain.BaseDirectory);
  var parameters=new ReaderParameters { AssemblyResolver=resolver, InMemory=true };
  string temp=target+".trainer-new";
  using(var asm=AssemblyDefinition.ReadAssembly(ours?backup:target,parameters))
  using(var rt=AssemblyDefinition.ReadAssembly(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PedroTrainer.Runtime.dll"),parameters)) {
   var module=asm.MainModule; var runtime=rt.MainModule.GetType("PedroTrainer.Runtime");
   Func<string,MethodReference> hook=name=>module.ImportReference(runtime.Methods.Single(m=>m.Name==name));
   int changes=0, ratingHooks=0, timerHooks=0;
   var timeGetter=module.GetMemberReferences().OfType<MethodReference>().First(m=>m.DeclaringType.FullName=="UnityEngine.Time"&&m.Name=="get_unscaledTime");
   var moduleType=module.Types.Single(t=>t.Name=="<Module>");
   var initializer=new MethodDefinition(".cctor",MethodAttributes.Static|MethodAttributes.Private|MethodAttributes.SpecialName|MethodAttributes.RTSpecialName,module.TypeSystem.Void);
   initializer.Body.Instructions.Add(Instruction.Create(OpCodes.Call,hook("Boot")));
   initializer.Body.Instructions.Add(Instruction.Create(OpCodes.Ret)); moduleType.Methods.Add(initializer);
   foreach(var type in module.Types) foreach(var method in type.Methods.Where(m=>m.HasBody)) {
    var il=method.Body.GetILProcessor();
    foreach(var instruction in method.Body.Instructions.ToArray()) {
     var field=instruction.Operand as FieldReference;
     if(type.Name=="LevelCompleteScreenScript"&&method.Name=="Update"&&instruction.OpCode==OpCodes.Call&&instruction.Operand is MethodReference) {
      var called=(MethodReference)instruction.Operand;
      var before=instruction.Previous;
      var reference=before!=null&&before.Previous!=null?before.Previous.Operand as FieldReference:null;
      if(called.DeclaringType.FullName=="UnityEngine.Mathf"&&called.Name=="Clamp01"&&before.OpCode==OpCodes.Div&&reference!=null&&reference.DeclaringType.Name=="RootScript"&&reference.Name=="maxScoreReference") {
       instruction.Operand=hook("RatingProgress");ratingHooks++;changes++;
      }
     }
     if(instruction.OpCode==OpCodes.Sub&&instruction.Previous!=null) {
      var startField=instruction.Previous.Operand as FieldReference;
      if(startField!=null&&startField.DeclaringType.Name=="RootScript"&&startField.Name=="startTime") {
       var cursor=il.Create(OpCodes.Ldarg_0);il.InsertAfter(instruction,cursor);
       if(type.Name!="RootScript") {
        var rootField=type.Fields.Single(f=>f.Name=="root"&&f.FieldType.Name=="RootScript");
        var load=il.Create(OpCodes.Ldfld,rootField);il.InsertAfter(cursor,load);cursor=load;
       }
       il.InsertAfter(cursor,il.Create(OpCodes.Call,hook("StageElapsed")));timerHooks++;changes++;
      }
     }
     if(instruction.OpCode==OpCodes.Stfld && field!=null) {
      if(field.DeclaringType.Name=="RootScript") {
       string filter=field.Name=="slowMotionAmount"?"FocusValue":field.Name=="multiplier"?"ComboValue":field.Name=="multiplierTimer"?"ComboTimer":null;
       if(filter!=null) { // Keep branch targets on the filter, including assignments reached by branches.
        instruction.OpCode=OpCodes.Call; instruction.Operand=hook(filter);
        il.InsertAfter(instruction,il.Create(OpCodes.Stfld,field)); changes++;
       }
      } else if(field.DeclaringType.Name=="PlayerScript" && field.Name=="health") {
       instruction.OpCode=OpCodes.Call; instruction.Operand=hook("Health"); changes++;
      } else if(new[]{"EnemyScript","DroneScript","BallTurretScript","TurretGunScript","MechScript","PedroBossScript"}.Contains(field.DeclaringType.Name) && new[]{"health","frontHealth","backHealth"}.Contains(field.Name) && field.FieldType.FullName=="System.Single") {
       instruction.OpCode=OpCodes.Ldstr; instruction.Operand=field.Name;
       il.InsertAfter(instruction,il.Create(OpCodes.Call,hook("EnemyHealth"))); changes++;
      }
     }
     if(instruction.OpCode==OpCodes.Stelem_R4) { instruction.OpCode=OpCodes.Call; instruction.Operand=hook("StoreAmmo"); changes++; }
    }
    if(type.Name=="PlayerScript" && (method.Name=="Update" || method.Name=="FixedUpdate")) {
     var first=method.Body.Instructions[0]; il.InsertBefore(first,il.Create(OpCodes.Ldarg_0)); il.InsertBefore(first,il.Create(OpCodes.Call,hook("PlayerTick"))); changes++;
    }
    if(type.Name=="RootScript" && method.Name=="Update") {
     var first=method.Body.Instructions[0]; il.InsertBefore(first,il.Create(OpCodes.Ldarg_0)); il.InsertBefore(first,il.Create(OpCodes.Call,hook("RootTick"))); changes++;
     foreach(var ret in method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
      ret.OpCode=OpCodes.Ldarg_0; ret.Operand=null;
      var loadTime=il.Create(OpCodes.Call,timeGetter);il.InsertAfter(ret,loadTime);
      var tick=il.Create(OpCodes.Call,hook("StageTick"));il.InsertAfter(loadTime,tick);il.InsertAfter(tick,il.Create(OpCodes.Ret));changes++;
     }
    }
    if(type.Name=="RootSharedScript" && method.Name=="Update") {
     il.InsertBefore(method.Body.Instructions[0],il.Create(OpCodes.Call,hook("SharedTick"))); changes++;
    }
    if(type.Name=="RootSharedScript" && (method.Name=="Awake" || method.Name=="Start")) {
     il.InsertBefore(method.Body.Instructions[0],il.Create(OpCodes.Call,hook("Boot"))); changes++;
    }
    if(type.Name=="PlayerScript" && method.Name=="reload") {
     var first=method.Body.Instructions[0]; il.InsertBefore(first,il.Create(OpCodes.Ldarg_0)); il.InsertBefore(first,il.Create(OpCodes.Call,hook("SkipReload"))); il.InsertBefore(first,il.Create(OpCodes.Brfalse,first)); il.InsertBefore(first,il.Create(OpCodes.Ret)); changes++;
    }
    foreach(var branch in method.Body.Instructions) {
     if(branch.OpCode.OperandType==OperandType.ShortInlineBrTarget) {
      string longName=branch.OpCode.Name.Substring(0,branch.OpCode.Name.Length-2);
      foreach(var opField in typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)) {
       if(opField.FieldType!=typeof(OpCode)) continue;
       var op=(OpCode)opField.GetValue(null); if(op.Name==longName) { branch.OpCode=op; break; }
      }
     }
    }
    method.Body.MaxStackSize+=4;
   }
   if(changes<50||ratingHooks!=1||timerHooks<8) throw new Exception("Estrutura inesperada do jogo: nota="+ratingHooks+", cronômetro="+timerHooks);
   asm.Write(temp); Console.WriteLine(changes+" pontos de integração criados.");
  }
  using(var check=AssemblyDefinition.ReadAssembly(temp)) if(!check.MainModule.AssemblyReferences.Any(r=>r.Name=="PedroTrainer.Runtime")) throw new Exception("Falha na verificação.");
  if(!File.Exists(backup)) File.Copy(target,backup,false);
  File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PedroTrainer.Runtime.dll"),Path.Combine(dir,"PedroTrainer.Runtime.dll"),true);
  string patchedHash=Hash(temp); File.Copy(temp,target,true); File.WriteAllText(stamp,patchedHash); File.WriteAllText(Path.Combine(dir,"PedroTrainer.protocol"),"8"); File.Delete(temp);
  Console.WriteLine("Trainer instalado. Backup original preservado.");
 }
}
