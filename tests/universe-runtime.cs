using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using UI.PerPlayerUI.FishingMinigame;
using UnityEngine;
using System.Threading;
using ByteTrainer.Universe;
namespace UnityEngine {
 public static class Time { public static float timeScale=1; }
 public struct Vector3 {
  public float x;
  public static Vector3 operator *(Vector3 v,float f){return new Vector3{x=v.x*f};}
 }
}
namespace UI.PerPlayerUI.FishingMinigame {
 public class FishingScreen { public bool isActiveAndEnabled=true; }
}
public class Level { public readonly HashSet<Player> Players=new HashSet<Player>(); }
public enum AttackType { None }
public enum Gang { Neutral,Player,Troll }
public enum ResourceType { None,Wood,Key=25 }
public class PawnData { public Gang Gang; }
public class PawnState { public bool Alive=true; }
public class Pawn { public PawnState State=new PawnState();public PawnHealth Health=new PawnHealth();public Gang Gang=Gang.Troll; }
public class Player:Pawn { public Level Level;public bool IsMainPlayer=true;public readonly object Boosters=new object();public Pawns.Attributes.PlayerAttributes Attributes=new Pawns.Attributes.PlayerAttributes(); }
public class PawnHealth {
 public int HealthCurrent=20,DeathCalls;
 public Action OnDeath;
 public void SetHealth(int next,AttackType attack,Pawn attacker,bool updateFull) {
  if(next!=0||!Runtime.IsForcedDeath(this,next))throw new Exception("Missing scoped death override.");
  HealthCurrent=next;
  DeathCalls++;
  if(OnDeath!=null)OnDeath();
 }
}
public class Resource { public float HealthCurrent=50; }
public class RefineryCore { public int AmountQueued; }
public class Refinery { public RefineryCore Core=new RefineryCore(); }
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
  var values=Enumerable.Repeat("1",21).ToArray();values[5]="3";values[6]="100";values[9]="2";values[13]="2";
  int flags=(1<<16)|(1<<17)|(1<<0)|(1<<2)|(1<<3)|(1<<5)|(1<<6)|(1<<9)|(1<<11)|(1<<13)|(1<<14)|(1<<15);
  Action<int> settings=mask=>File.WriteAllText(Path.Combine(folder,"settings.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+mask+"|"+session+"|"+String.Join(",",values));
  settings(flags);Runtime.Tick(false,ref previous);
  Check(Runtime.BlockHealth(player.Health,player,1),"health");
  Check(Tracker.Crystals&&Tracker.Expansions,"tracker controls have independent new bits");
  var enemy=new Pawn();float damage=1;Check(!Runtime.Combat(enemy,ref damage,player)&&damage==20,"one hit");
  var resource=new Resource();damage=1;Runtime.ResourceDamage(resource,ref damage,player);Check(damage==51,"resource one hit");
  Check(Runtime.Attribute(3,player.Attributes.AttackRange)==300,"range");
  Check(Runtime.Attribute(3,new Pawns.Attributes.Attribute())==3,"NPC attributes unchanged");
  Check(Runtime.Attribute(0,player.Attributes.GroundMovementSpeedMultiplier)==1,"ground bonus conversion");
  Check(Runtime.Movement(new UnityEngine.Vector3{x=3},player,false).x==6,"ground acceleration");
  Check(Runtime.Experience(player.Attributes,20)==40,"experience");
  Check(Runtime.Experience(player.Attributes,8)==8,"XP loss unchanged");
  Check(Runtime.Regeneration(100)==0,"regeneration");
  var refinery=new Refinery();
  Check(!Runtime.ShouldRefine(refinery),"empty refinery is unchanged");
  refinery.Core.AmountQueued=52400;
  Check(Runtime.ShouldRefine(refinery)&&Runtime.RefiningAmount(0,refinery.Core)==52400,"complete the entire paid queue");
  Check(!Runtime.ShouldRefine(null),"missing refinery");
  var enemyData=new PawnData{Gang=Gang.Troll};
  Check(!Runtime.BlockCustom(0,enemyData),"initial custom spawn");
  Check(Runtime.BlockCustom(1,enemyData),"custom respawn");
  Check(!Runtime.BlockCustom(1,new PawnData{Gang=Gang.Player}),"friendly spawn");
  Check(!Runtime.BlockLoop(true,0,1),"first enemy wave");
  Check(Runtime.BlockLoop(true,1,1),"looped enemy wave");
  Check(UnityEngine.Time.timeScale==2,"game speed");
  string nonce=new string('b',32);
  File.WriteAllText(Path.Combine(folder,"resource.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+session+"|"+nonce+"|1|-100");
  Thread.Sleep(120);Runtime.Touch(player);settings(flags);Runtime.Tick(false,ref previous);Check(SaveManager.I.Resources.Stock==0&&SaveManager.I.Resources.Calls==1,"resource removal clamps");
  Thread.Sleep(120);Runtime.Touch(player);settings(flags);Runtime.Tick(false,ref previous);Check(SaveManager.I.Resources.Calls==1,"resource request is not repeated");
  SaveManager.I.Resources.Stock=8;
  File.WriteAllText(Path.Combine(folder,"resource.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+session+"|"+new string('c',32)+"|25|1000");
  Thread.Sleep(120);Runtime.Touch(player);settings(flags);Runtime.Tick(false,ref previous);
  Check(SaveManager.I.Resources.Stock==10,"special items respect native inventory cap");
  settings(0);UnityEngine.Time.timeScale=0;previous=2;Thread.Sleep(120);Runtime.Tick(true,ref previous);
  Check(previous==1&&UnityEngine.Time.timeScale==0,"pause restores baseline");
  Check(!Runtime.BlockCustom(1,enemyData)&&!Runtime.BlockLoop(true,1,1),"respawn resumes");
  Check(!Runtime.ShouldRefine(refinery)&&Runtime.RefiningAmount(3,refinery.Core)==3,"refining timer resumes");
  Check(!Tracker.Crystals&&!Tracker.Expansions,"trackers hide when disabled");
  settings(1<<16);Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(false,ref previous);
  Check(Tracker.Crystals&&!Tracker.Expansions,"crystal tracker works independently");
  settings(1<<17);Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(false,ref previous);
  Check(!Tracker.Crystals&&Tracker.Expansions,"expansion tracker works independently");
  const int instantDeposit=1<<19, killPlayer=1<<18, unlimitedHealth=1<<0;
  player.Health.OnDeath=()=>{
   Check(Runtime.PreserveProgressOnDeath(player.Attributes),"forced death preserves level and experience");
   Check(Runtime.PreserveProgressOnDeath(player.Boosters),"forced death preserves skills");
   Check(!Runtime.PreserveProgressOnDeath(new Player().Boosters),"penalty exemption is scoped to the main character");
  };
  settings(instantDeposit|killPlayer|unlimitedHealth);Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(false,ref previous);
  Check(player.Health.DeathCalls==1&&player.Health.HealthCurrent==0,"kill player bypasses unlimited health");
  Check(!Runtime.IsForcedDeath(player.Health,0),"death override ends after the command");
  Check(!Runtime.PreserveProgressOnDeath(player.Attributes)&&!Runtime.PreserveProgressOnDeath(player.Boosters),"normal deaths still incur penalties");
  Check(Runtime.InstantDeposit(player)&&!Runtime.InstantDeposit(enemy),"instant deposit applies to the main character");
  player.Health.HealthCurrent=20;
  Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(false,ref previous);
  Check(player.Health.DeathCalls==1,"holding the control does not kill again after respawn");
  settings(unlimitedHealth);Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(false,ref previous);
  Check(Runtime.BlockHealth(player.Health,player,1)&&!Runtime.InstantDeposit(player),"normal health and deposit behavior resumes");
  settings(killPlayer|unlimitedHealth);Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(false,ref previous);
  Check(player.Health.DeathCalls==2,"toggle can be rearmed");
  player.Health.HealthCurrent=20;
  var level=new Level();
  player.Level=level;level.Players.Add(player);
  var fishingScreen=new FishingScreen();
  var steps=new List<string>();
  Action startRound=null;
  startRound=()=>AfkFishing.Queue(player,fishingScreen,()=>{
   steps.Add("win");
   AfkFishing.Queue(player,fishingScreen,()=>{
    steps.Add("dismiss capture");
    AfkFishing.Queue(player,fishingScreen,()=>{
     steps.Add("continue fishing");
     startRound();
    });
   });
  });
  startRound();
  AfkFishing.Update(player,false);
  Check(steps.Count==0,"fishing is manual while AFK is disabled");
  settings(1<<20);Thread.Sleep(120);Runtime.Touch(player);Runtime.Tick(true,ref previous);
  Check(steps.Count==0,"AFK waits while paused");
  level.Players.Add(new Player());
  AfkFishing.Update(player,false);
  Check(steps.Count==0,"AFK does not play in co-op");
  level.Players.RemoveWhere(other=>other!=player);
  for(int i=0;i<6;i++)AfkFishing.Update(player,false);
  Check(string.Join("|",steps)=="win|dismiss capture|continue fishing|win|dismiss capture|continue fishing","AFK completes two rounds in the same fishing screen");
  AfkFishing.Clear(fishingScreen);
  AfkFishing.Update(player,false);
  Check(steps.Count==6,"leaving fishing cancels the queued action");
  Console.WriteLine("Native runtime checks passed.");
 }
}

namespace ByteTrainer.Universe { internal static class Tracker { public static bool Crystals,Expansions;public static void Update(Player player,bool crystals,bool expansions,bool paused){Crystals=crystals;Expansions=expansions;} } }
