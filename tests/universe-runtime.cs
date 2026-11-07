using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using ByteTrainer.Universe;
namespace UnityEngine { public static class Time { public static float timeScale=1; } public struct Vector3 { public float x; public static Vector3 operator *(Vector3 v,float f){return new Vector3{x=v.x*f};} } }
public enum Gang { Neutral,Player,Troll }
public enum ResourceType { None,Wood,Key=25 }
public class PawnData { public Gang Gang; }
public class PawnState { public bool Alive=true; }
public class Pawn { public PawnState State=new PawnState();public PawnHealth Health=new PawnHealth();public Gang Gang=Gang.Troll; }
public class Player:Pawn { public bool IsMainPlayer=true;public Pawns.Attributes.PlayerAttributes Attributes=new Pawns.Attributes.PlayerAttributes(); }
public class PawnHealth { public int HealthCurrent=20; }
public class Resource { public float HealthCurrent=50; }
public class ResourceSave { public int Stock=8,Calls;public int GetResourceAmount(ResourceType type){return Stock;}public void AddResource(ResourceType type,int amount,bool save,bool total,bool refresh){Stock+=amount;Calls++;} }
public class SaveManager { public static SaveManager I=new SaveManager();public bool SaveReady=true;public ResourceSave Resources=new ResourceSave(); }
namespace Pawns.Attributes {
 public class Attribute {}
 public class PlayerAttributes {
  public int Experience=10;
  public Attribute AttackRange=new Attribute(),AttackSpeed=new Attribute(),GatherSpeed=new Attribute(),GroundMovementSpeedMultiplier=new Attribute(),WaterMovementSpeedMultiplier=new Attribute();
 }
}
class RuntimeTest {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main(){
  var player=new Player();Runtime.Touch(player);
  string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-2328750");Directory.CreateDirectory(folder);
  int pid=Process.GetCurrentProcess().Id;string session=new string('a',32);float previous=1;
  var values=Enumerable.Repeat("1",15).ToArray();values[5]="3";values[6]="100";values[9]="2";values[13]="2";
  int flags=(1<<0)|(1<<2)|(1<<3)|(1<<5)|(1<<6)|(1<<9)|(1<<11)|(1<<13)|(1<<14);
  Action<int> settings=mask=>File.WriteAllText(Path.Combine(folder,"settings.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+mask+"|"+session+"|"+String.Join(",",values));
  settings(flags);Runtime.Tick(false,ref previous);
  Check(Runtime.BlockHealth(player.Health,player,1),"health");
  var enemy=new Pawn();float damage=1;Check(!Runtime.Combat(enemy,ref damage,player)&&damage==20,"one hit");
  var resource=new Resource();damage=1;Runtime.ResourceDamage(resource,ref damage,player);Check(damage==51,"resource one hit");
  Check(Runtime.Attribute(3,player.Attributes.AttackRange)==300,"range");
  Check(Runtime.Attribute(3,new Pawns.Attributes.Attribute())==3,"NPC attributes unchanged");
  Check(Runtime.Attribute(0,player.Attributes.GroundMovementSpeedMultiplier)==1,"ground bonus conversion");
  Check(Runtime.Movement(new UnityEngine.Vector3{x=3},player,false).x==6,"ground acceleration");
  Check(Runtime.Experience(player.Attributes,20)==40,"experience");
  Check(Runtime.Experience(player.Attributes,8)==8,"XP loss unchanged");
  Check(Runtime.Regeneration(100)==0,"regeneration");
  var enemyData=new PawnData{Gang=Gang.Troll};
  Check(!Runtime.BlockCustom(0,enemyData),"initial custom spawn");
  Check(Runtime.BlockCustom(1,enemyData),"custom respawn");
  Check(!Runtime.BlockCustom(1,new PawnData{Gang=Gang.Player}),"friendly spawn");
  Check(!Runtime.BlockLoop(true,0,1),"first enemy wave");
  Check(Runtime.BlockLoop(true,1,1),"looped enemy wave");
  Check(UnityEngine.Time.timeScale==2,"game speed");
  string nonce=new string('b',32);
  File.WriteAllText(Path.Combine(folder,"resource.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+session+"|"+nonce+"|1|-100");
  Thread.Sleep(120);Runtime.Tick(false,ref previous);Check(SaveManager.I.Resources.Stock==0&&SaveManager.I.Resources.Calls==1,"resource removal clamps");
  Thread.Sleep(120);Runtime.Tick(false,ref previous);Check(SaveManager.I.Resources.Calls==1,"resource request is not repeated");
  SaveManager.I.Resources.Stock=8;
  File.WriteAllText(Path.Combine(folder,"resource.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+session+"|"+new string('c',32)+"|25|1000");
  Thread.Sleep(120);Runtime.Tick(false,ref previous);
  Check(SaveManager.I.Resources.Stock==10,"special items respect native inventory cap");
  settings(0);UnityEngine.Time.timeScale=0;previous=2;Thread.Sleep(120);Runtime.Tick(true,ref previous);
  Check(previous==1&&UnityEngine.Time.timeScale==0,"pause restores baseline");
  Check(!Runtime.BlockCustom(1,enemyData)&&!Runtime.BlockLoop(true,1,1),"respawn resumes");
  Console.WriteLine("Native runtime checks passed.");
 }
}
