using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using HarmonyLib;
using ByteTrainer.Tinykin;

namespace ByteTrainer.Tinykin { internal static class Tracker { internal static void Update(int mask){if(Game.DenyUnity)throw new Exception("Tracker on worker");} internal static void Draw(){} } }
namespace UnityEngine {
 public class Object { public static void DontDestroyOnLoad(Object value) {} }
 public class MonoBehaviour : Object {}
 public class GameObject : Object { public GameObject(string name){} public T AddComponent<T>() where T:new(){return new T();} }
 public struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
 public class Transform {public Vector3 position;}
 public static class Time {public static float deltaTime {get{return .5f;}}}
 public static class Application {public static bool isFocused=true;}
}
public class PlayerController {
 public bool IsRespawning,ControlsLocked,IsDrowning,Gliding,TouchesGround;
 public float CeilingHeight=10,JetpackFuel=1,MaxUsableFuel=3;
 public UnityEngine.Transform transform=new UnityEngine.Transform();
 public int Reloads;
 public void ReloadJetpackFuel(bool unused){JetpackFuel=MaxUsableFuel;Reloads++;}
}
public static class Game {
 public static bool DenyUnity;
 private static bool started=true;
 public static bool IsGameStarted {get{if(DenyUnity)throw new Exception("Unity state accessed from heartbeat");return started;}set{started=value;}}
 public static PlayerController PLAYER=new PlayerController();
}
public class PlayerCamera {
 public static PlayerCamera INSTANCE=new PlayerCamera();
 public bool FreeCameraMode,IsCinematic,IsInCinematicTransition;
 public UnityEngine.Transform Target=Game.PLAYER.transform;
}
class TinykinTests {
 static string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-1599020");
 static string ReadShared(string file){using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Settings(int flags,float speed=2,bool expired=false,int pid=0,bool tick=true){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"settings.txt"),(pid==0?Process.GetCurrentProcess().Id:pid)+"|"+DateTime.UtcNow.AddSeconds(expired?-1:3.5).Ticks+"|"+flags+"|0123456789abcdef0123456789abcdef|1,1,"+speed.ToString(System.Globalization.CultureInfo.InvariantCulture)+",1,1,1,1,1,1,1");typeof(Runtime).GetField("nextPoll",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,0L);if(tick)Runtime.Tick();}
 static void StalledFrame(){typeof(Runtime).GetField("lastMainTick",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,DateTime.UtcNow.AddSeconds(-10).Ticks);typeof(Runtime).GetField("nextStatus",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,0L);}
 static void Main(){
  var player=Game.PLAYER;var stranger=new PlayerController();
  Settings(960);Check(Runtime.Enabled(6)&&Runtime.Enabled(9),"tracker flag range");
  Settings(1024);Check(!Runtime.Enabled(6),"reject out of range mask");
  Settings(0);Check(!Runtime.Protect(player)&&Runtime.Movement(3,player)==3,"disabled baseline");
  Settings(1);Check(Runtime.Protect(player)&&!Runtime.Protect(stranger),"local protection");
  Check(Runtime.BlockDeath(player,"Fire")&&!Runtime.BlockDeath(player,null),"preserve fall recovery");
  Check(!Runtime.Danger(true,player)&&Runtime.Danger(true,stranger),"hazard scope");
  Settings(2);Check(Runtime.JumpAllowed(player,false)&&!Runtime.Anticipate(player,true),"air jump");
  player.CeilingHeight=1;Check(!Runtime.JumpAllowed(player,false),"ceiling collision");player.CeilingHeight=10;
  player.ControlsLocked=true;Check(!Runtime.JumpAllowed(player,false),"locked controls");player.ControlsLocked=false;
  player.transform.position=new UnityEngine.Vector3(0,20,0);Check(Runtime.CameraHeight(0,PlayerCamera.INSTANCE)==20,"camera follows air jump altitude");
  PlayerCamera.INSTANCE.IsCinematic=true;Check(Runtime.CameraHeight(0,PlayerCamera.INSTANCE)==0,"cinematic camera preserved");PlayerCamera.INSTANCE.IsCinematic=false;
  player.TouchesGround=true;Check(Runtime.CameraHeight(0,PlayerCamera.INSTANCE)==0,"normal ground camera preserved");player.TouchesGround=false;
  Settings(4,3);Check(Runtime.Movement(2,player)==6&&Runtime.Movement(2,stranger)==2,"movement multiplier");
  Settings(16);Check(Runtime.FreeFuel(player)&&player.JetpackFuel==3&&player.Reloads==1,"glide refill");
  Settings(32);player.Gliding=true;var value=Runtime.GlideVelocity(new UnityEngine.Vector3(2,-3,4),player);
  Check(value.y==0&&value.x==2&&value.z==4,"level glide");
  Check(Runtime.GlideVelocity(new UnityEngine.Vector3(0,2,0),player).y==2,"preserve ascent");
  player.Gliding=false;Check(Runtime.GlideVelocity(new UnityEngine.Vector3(0,-3,0),player).y==-3,"release glide");
  // Exercise the real Harmony transpiler on a representative generated race iterator.
  var assembly=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("TinykinTimerFixture"),AssemblyBuilderAccess.Run);
  var type=assembly.DefineDynamicModule("fixture").DefineType("RaceFixture",TypeAttributes.Public);
  var elapsed=type.DefineField("<elapsedTime>5__3",typeof(float),FieldAttributes.Public);
  var step=type.DefineMethod("Step",MethodAttributes.Public,typeof(void),Type.EmptyTypes);var il=step.GetILGenerator();
  il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,elapsed);il.Emit(OpCodes.Call,typeof(UnityEngine.Time).GetProperty("deltaTime").GetGetMethod());il.Emit(OpCodes.Add);il.Emit(OpCodes.Stfld,elapsed);il.Emit(OpCodes.Ret);
  var built=type.CreateType();var instance=Activator.CreateInstance(built);var timer=built.GetField(elapsed.Name);var method=built.GetMethod("Step");
  new Harmony("org.bytetrainer.tests.tinykin").Patch(method,transpiler:new HarmonyMethod(typeof(TimerHooks).GetMethod("Transpile")));
  TimerHooks.Installed=true;
  timer.SetValue(instance,12f);Settings(8);method.Invoke(instance,null);Check((float)timer.GetValue(instance)==12f,"timer freezes current accumulated value");
  Check(UnityEngine.Time.deltaTime==.5f,"world delta unchanged");
  Settings(0);method.Invoke(instance,null);Check((float)timer.GetValue(instance)==12.5f,"timer resumes");
  Check(Runtime.CameraHeight(0,PlayerCamera.INSTANCE)==0,"disabled camera returns to native terrain reference");
  // Minimized/overlay communication continues without another Unity Update or Unity API access.
  Settings(0);Runtime.SetFocus(false);StalledFrame();Settings(3,tick:false);
  Game.DenyUnity=true;Runtime.Heartbeat();Game.DenyUnity=false;
  var status=ReadShared(Path.Combine(folder,"status.txt")).Split('|');
  Check(status[3]=="1"&&status[4]=="3","background controls and readiness stay available");
  Check(DateTime.UtcNow.Ticks-long.Parse(status[2])<TimeSpan.TicksPerSecond,"background heartbeat stays fresh");
  Check(ReadShared(Path.Combine(folder,"diagnostic.txt")).Contains("background"),"background path observed");
  Settings(0,tick:false);StalledFrame();Game.DenyUnity=true;Runtime.Heartbeat();Game.DenyUnity=false;
  Check(ReadShared(Path.Combine(folder,"status.txt")).Split('|')[4]=="0","overlay can turn controls off while main loop is stopped");
  Runtime.SetFocus(true);StalledFrame();var previous=ReadShared(Path.Combine(folder,"status.txt"));Runtime.Heartbeat();
  Check(ReadShared(Path.Combine(folder,"status.txt"))==previous,"a foreground hang is not concealed by heartbeat");
  Runtime.SetFocus(false);StalledFrame();Settings(63,2,true,tick:false);Runtime.Heartbeat();
  Check(ReadShared(Path.Combine(folder,"status.txt")).Split('|')[4]=="0","background heartbeat does not extend expired lease");
  Runtime.SetFocus(true);
  Settings(63,2,true);Check(!Runtime.Enabled(0)&&!Runtime.Enabled(5),"expiry");
  Settings(63,2,false,123);Check(!Runtime.Enabled(0),"PID isolation");
  Settings(63,6);Check(!Runtime.Enabled(0),"reject invalid multiplier");
  Settings(63);Game.IsGameStarted=false;Check(!Runtime.Enabled(0)&&!Runtime.Enabled(5),"menu readiness");
  // Exercise the actual periodic worker, then ensure application shutdown disposes it.
  Game.IsGameStarted=true;Settings(3);UnityEngine.Application.isFocused=false;Runtime.Boot();StalledFrame();
  var oldStamp=ReadShared(Path.Combine(folder,"status.txt")).Split('|')[2];
  Game.DenyUnity=true;
  var until=DateTime.UtcNow.AddSeconds(2.5);
  while(DateTime.UtcNow<until&&ReadShared(Path.Combine(folder,"status.txt")).Split('|')[2]==oldStamp)System.Threading.Thread.Sleep(50);
  Game.DenyUnity=false;
  var workerStatus=ReadShared(Path.Combine(folder,"status.txt")).Split('|');
  Check(workerStatus[2]!=oldStamp&&workerStatus[3]=="1"&&workerStatus[4]=="3","periodic worker stays alive without Unity frames");
  Runtime.Shutdown();var stopped=ReadShared(Path.Combine(folder,"status.txt"));System.Threading.Thread.Sleep(500);
  Check(ReadShared(Path.Combine(folder,"status.txt"))==stopped,"shutdown stops background publications");
  Console.WriteLine("Tinykin native runtime and Harmony timer checks passed.");
 }
}
