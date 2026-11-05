using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PedroTrainer {
 public static class Runtime {
  public static readonly string Folder = Path.Combine(Path.GetDirectoryName(typeof(Runtime).Assembly.Location), "PedroTrainerData");
  public static readonly string Config = Path.Combine(Folder, "settings.txt");
  static DateTime nextRead;
  static DateTime nextHeartbeat;
  static bool[] options = new bool[8];
  static float combo = 10;
  static float[] clip, reserve, secondary, capacity;
  static readonly Dictionary<string,FieldInfo> fields = new Dictionary<string,FieldInfo>();
  [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
  static bool booted;
  static DateTime validUntil;
  static readonly string[] ids={"health","focus","one-hit","ammo","reload","combo","rank","timer"};
  static readonly Dictionary<string,string> unavailable=new Dictionary<string,string>();
  static bool Supports(string id,object obj,params string[] names) {
   if(unavailable.ContainsKey(id)) return false;
   try { foreach(string name in names) { var f=Field(obj,name);Type expected=name=="isEnemy"||name=="pedroBoss"||name=="reloading"||name=="cCheckMu"||name=="cCheckMuTi"||name=="levelEndedVar"?typeof(bool):name=="ammo"||name=="ammoTotal"||name=="secondaryAmmo"||name=="ammoFullClip"?typeof(float[]):typeof(float);if(f.FieldType!=expected)throw new Exception("Tipo incompatível: "+name); }return true; }
   catch(Exception e) { unavailable[id]="Esta opção não pôde ser validada nesta versão do jogo.";Log(id+": "+e.Message);return false; }
  }
  static void ReportCapabilities(){
   var json=new System.Text.StringBuilder("{\"time\":"+((DateTime.UtcNow.Ticks-621355968000000000L)/10000L)+",\"release\":6,\"capabilities\":{");
   for(int i=0;i<ids.Length;i++){if(i>0)json.Append(',');bool ok=!unavailable.ContainsKey(ids[i]);json.Append("\"").Append(ids[i]).Append("\":{\"available\":").Append(ok?"true":"false").Append(",\"reason\":").Append(ok?"null":"\"Esta opção não pôde ser validada nesta versão do jogo.\"").Append('}');}json.Append("}}");
   string file=Path.Combine(Folder,"capabilities.json"),temp=file+".tmp";File.WriteAllText(temp,json.ToString());if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
  }
  static WeakReference timerRoot;
  static float timerStart, timerOffset, timerFrozen;
  static bool timerWasFrozen;
  static void Log(string message) {
   try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(typeof(Runtime).Assembly.Location),"PedroTrainer.diagnostic.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine); } catch { }
  }
  public static void Boot() {
   if(!booted) { booted=true; Log("BOOT pid="+GetCurrentProcessId()+" config="+Config+" specialFolder="+Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    try { Poll(); }
    catch(Exception e) { Log("POLL ERROR "+e.ToString()); }
   } else Poll();
  }
  static FieldInfo Field(object obj, string name) {
   string key=obj.GetType().FullName+":"+name; FieldInfo f;
   if(!fields.TryGetValue(key,out f)) { f=obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); if(f==null) throw new MissingFieldException(key); fields[key]=f; } return f;
  }
  static bool Enabled(int i) { Poll(); return options[i]&&!unavailable.ContainsKey(ids[i]); }
  public static void Poll() {
   if(DateTime.UtcNow>validUntil) options=new bool[8];
   if(DateTime.UtcNow < nextRead) return;
   nextRead=DateTime.UtcNow.AddMilliseconds(100);
   try {
    if(DateTime.UtcNow>=nextHeartbeat) {
     nextHeartbeat=DateTime.UtcNow.AddSeconds(1);
     Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder,"connected.txt"),GetCurrentProcessId()+"|"+DateTime.UtcNow.Ticks.ToString()+"|6"); ReportCapabilities();
    }
    if(!File.Exists(Config)) { options=new bool[8]; return; }
    string[] parts=File.ReadAllText(Config).Trim().Split('|');
    long expiry; float value;
    int count=parts.Length-2;
    if((count!=6&&count!=8) || !long.TryParse(parts[count+1],out expiry) || expiry<DateTime.UtcNow.Ticks || expiry>DateTime.MaxValue.Ticks) { options=new bool[8]; return; }
    if(!float.TryParse(parts[count],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value) || float.IsNaN(value) || float.IsInfinity(value)) { options=new bool[8]; return; }
    bool[] updated=new bool[8]; for(int i=0;i<count;i++) updated[i]=parts[i]=="1";
    combo=Math.Max(1,Math.Min(9999,value)); options=updated; validUntil=new DateTime(expiry,DateTimeKind.Utc);
   } catch(IOException e) { Log(e.ToString()); } catch(UnauthorizedAccessException e) { options=new bool[8]; Log(e.ToString()); }
  }
  public static void SharedTick() { Boot(); }
  public static void RootTick(object root) {
   bool focusSupported=Supports("focus",root,"slowMotionAmount"),comboSupported=Supports("combo",root,"multiplier","multiplierTimer","cCheckMu","cCheckMuTi");
   if(Enabled(1)&&focusSupported) Field(root,"slowMotionAmount").SetValue(root,1f);
   if(Enabled(5)&&comboSupported) {
    Field(root,"multiplier").SetValue(root,combo); Field(root,"multiplierTimer").SetValue(root,1f);
    Field(root,"cCheckMu").SetValue(root,true); Field(root,"cCheckMuTi").SetValue(root,true);
   }
  }
  public static float FocusValue(float value) { return Enabled(1)?1f:value; }
  public static float ComboValue(float value) { return Enabled(5)?combo:value; }
  public static float ComboTimer(float value) { return Enabled(5)?1f:value; }
  public static float RatingProgress(float value) {
   if(Enabled(6)) return 1f;
   return value<0f?0f:value>1f?1f:value;
  }
  public static float StageElapsed(float raw,object root) {
   Poll();
   if(!Supports("timer",root,"startTime"))return raw;
   if(float.IsNaN(raw)||float.IsInfinity(raw)) return raw;
   float start=(float)Field(root,"startTime").GetValue(root);
   if(timerRoot==null||!Object.ReferenceEquals(timerRoot.Target,root)||start!=timerStart) {
    timerRoot=new WeakReference(root);timerStart=start;timerOffset=0f;timerWasFrozen=false;
   }
   raw=Math.Max(0f,raw);
   if(Enabled(7)) {
    if(!timerWasFrozen) {timerFrozen=Math.Max(0f,raw-timerOffset);timerWasFrozen=true;}
    timerOffset=raw-timerFrozen;
    return timerFrozen;
   }
   if(timerWasFrozen) {timerOffset=raw-timerFrozen;timerWasFrozen=false;}
   return Math.Max(0f,raw-timerOffset);
  }
  public static void StageTick(object root,float now) {
   if(!Supports("timer",root,"startTime","levelEndedVar","finishTime","timePaused"))return;
   float start=(float)Field(root,"startTime").GetValue(root);
   bool ended=(bool)Field(root,"levelEndedVar").GetValue(root);
   float clock=ended?(float)Field(root,"finishTime").GetValue(root):now-(float)Field(root,"timePaused").GetValue(root);
   StageElapsed(clock-start,root);
  }
  static bool IsPlayer(object p) { return !(bool)Field(p,"isEnemy").GetValue(p) && !(bool)Field(p,"pedroBoss").GetValue(p); }
  public static void PlayerTick(object p) {
   Poll();if(!Supports("health",p,"isEnemy","pedroBoss")){Supports("ammo",p,"isEnemy","pedroBoss");Supports("reload",p,"isEnemy","pedroBoss");return;}if(!IsPlayer(p))return;
   bool healthSupported=Supports("health",p,"health"),ammoSupported=Supports("ammo",p,"ammoTotal","secondaryAmmo","ammoFullClip"),reloadSupported=Supports("reload",p,"ammo","ammoFullClip","reloading");
   if(Enabled(0)&&healthSupported)Field(p,"health").SetValue(p,1f);
   if(ammoSupported){reserve=(float[])Field(p,"ammoTotal").GetValue(p);secondary=(float[])Field(p,"secondaryAmmo").GetValue(p);capacity=(float[])Field(p,"ammoFullClip").GetValue(p);if(Enabled(3)&&reserve!=null&&capacity!=null)for(int i=0;i<Math.Min(reserve.Length,capacity.Length);i++)reserve[i]=Math.Max(reserve[i],capacity[i]*10);if(Enabled(3)&&secondary!=null)for(int i=0;i<secondary.Length;i++)secondary[i]=Math.Max(secondary[i],5f);}
   if(reloadSupported){clip=(float[])Field(p,"ammo").GetValue(p);capacity=(float[])Field(p,"ammoFullClip").GetValue(p);var method=p.GetType().GetMethod("finishedReloading");if(method==null){unavailable["reload"]="Rotina de recarga ausente.";}else if(Enabled(4)&&clip!=null&&capacity!=null){for(int i=0;i<Math.Min(clip.Length,capacity.Length);i++)if(capacity[i]>0)clip[i]=capacity[i];if((bool)Field(p,"reloading").GetValue(p))method.Invoke(p,null);}}
  }
  public static bool SkipReload(object p) { return Enabled(4)&&Supports("reload",p,"isEnemy","pedroBoss","reloading") && IsPlayer(p); }
  public static void Health(object p,float value) { Field(p,"health").SetValue(p,Enabled(0)&&Supports("health",p,"isEnemy","pedroBoss","health")&&IsPlayer(p)?1f:value); }
  public static void EnemyHealth(object p,float value,string field) {
   FieldInfo f=Field(p,field); float current=(float)f.GetValue(p);
   f.SetValue(p,Enabled(2)&&value<current&&current>0?0f:value);
  }
  public static void StoreAmmo(float[] array,int index,float value) {
   if(Enabled(4) && Object.ReferenceEquals(array,clip) && capacity!=null && index<capacity.Length && capacity[index]>0) value=capacity[index];
   if(Enabled(3) && Object.ReferenceEquals(array,reserve) && value<array[index]) value=array[index];
   if(options[3] && Object.ReferenceEquals(array,secondary) && value<array[index]) value=array[index];
   array[index]=value;
  }
 }
}
