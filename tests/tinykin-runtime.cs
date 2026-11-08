using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using HarmonyLib;
using ByteTrainer.Tinykin;

namespace UnityEngine {
 public class Object { public static void DontDestroyOnLoad(Object value) {} }
 public class MonoBehaviour : Object {}
 public class GameObject : Object { public GameObject(string name){} public T AddComponent<T>() where T:new(){return new T();} }
 public struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
 public class Transform {public Vector3 position;}
 public static class Time {public static float deltaTime {get{return .5f;}}}
}
public class PlayerController {
 public bool IsRespawning,ControlsLocked,IsDrowning,Gliding,TouchesGround;
 public float CeilingHeight=10,JetpackFuel=1,MaxUsableFuel=3;
 public UnityEngine.Transform transform=new UnityEngine.Transform();
 public int Reloads;
 public void ReloadJetpackFuel(bool unused){JetpackFuel=MaxUsableFuel;Reloads++;}
}
public static class Game {public static bool IsGameStarted=true;public static PlayerController PLAYER=new PlayerController();}
public class PlayerCamera {
 public static PlayerCamera INSTANCE=new PlayerCamera();
 public bool FreeCameraMode,IsCinematic,IsInCinematicTransition;
 public UnityEngine.Transform Target=Game.PLAYER.transform;
}
class TinykinTests {
 static string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-1599020");
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Settings(int flags,float speed=2,bool expired=false,int pid=0){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"settings.txt"),(pid==0?Process.GetCurrentProcess().Id:pid)+"|"+DateTime.UtcNow.AddSeconds(expired?-1:3.5).Ticks+"|"+flags+"|0123456789abcdef0123456789abcdef|1,1,"+speed.ToString(System.Globalization.CultureInfo.InvariantCulture)+",1,1,1");typeof(Runtime).GetField("nextPoll",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,0L);Runtime.Tick();}
 static void Main(){
  var player=Game.PLAYER;var stranger=new PlayerController();
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
  Settings(63,2,true);Check(!Runtime.Enabled(0)&&!Runtime.Enabled(5),"expiry");
  Settings(63,2,false,123);Check(!Runtime.Enabled(0),"PID isolation");
  Settings(63,6);Check(!Runtime.Enabled(0),"reject invalid multiplier");
  Settings(63);Game.IsGameStarted=false;Check(!Runtime.Enabled(0)&&!Runtime.Enabled(5),"menu readiness");
  Console.WriteLine("Tinykin native runtime and Harmony timer checks passed.");
 }
}
