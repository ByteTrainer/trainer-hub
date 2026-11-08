using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ByteTrainer.OutOfSpace;
namespace Photon.Pun {
 public class PhotonView { public bool IsMine=true; }
 public static class PhotonNetwork { public static bool InRoom=false,IsMasterClient=true; }
}
namespace UnityEngine { public static class Time { public static float timeScale=1; } }
public class Character { public Photon.Pun.PhotonView photonView=new Photon.Pun.PhotonView(); }
public class PlayerCharacter:Character {}
public class PlayerStats {
 public Character player=new PlayerCharacter();public Photon.Pun.PhotonView photonView=new Photon.Pun.PhotonView();
 public int currentFatigue=82,currentHunger=33,currentDirtiness=44,Updates;
 public void UpdateFatigue(int value,bool absolute,bool percentage){currentFatigue=absolute?value:currentFatigue+value;Updates++;}
 public void UpdateHunger(int value,bool absolute){currentHunger=absolute?value:currentHunger+value;Updates++;}
 public void UpdateDirt(int value,bool absolute,bool bypass){currentDirtiness=absolute?value:currentDirtiness+value;Updates++;}
}
public class Bucket:Character { public bool IsFull;public int Fills;public void FillBucket(bool full,bool effect){IsFull=full;Fills++;} }
public class Pet:Character { public int currentEnergy=50;public int CurrentEnergy {get{return currentEnergy;}set{currentEnergy=value;}} public int MaxEnergy {get{return 100;}} }
public class Dog:Pet {}
public class EnemyAI {}
public class Vulnerable { public bool isStunned;public EnemyAI enemy; }
public class Egg { public float spawnTimer; }
public class LoopManager { public bool GamePaused; private int credits;public int Calls;public int Credits {get{return credits;}set{credits=Math.Max(0,Math.Min(9999,value));Calls++;}} }
class OutOfSpaceTest {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main(){
  string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-400080");Directory.CreateDirectory(folder);
  int pid=Process.GetCurrentProcess().Id;string session=new string('a',32);var loop=new LoopManager();var local=new PlayerStats();
  Action<int,bool> frame=(mask,started)=>{Thread.Sleep(120);File.WriteAllText(Path.Combine(folder,"settings.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+mask+"|"+session+"|1,1,1,1,1,1,1,1,2,2");Runtime.Touch(local);Runtime.Tick(loop,started,false);Runtime.Touch(local);};
  frame(7,false);Check(local.Updates==0&&!Runtime.Enabled(0),"menu cannot enable controls");
  frame(7,true);Check(local.currentFatigue==0&&local.currentHunger==0&&local.currentDirtiness==0&&local.Updates==3,"native routines clear current needs once");
  Runtime.Touch(local);Check(local.Updates==3,"unchanged healthy states do not repeatedly emit updates");
  for(int i=0;i<3;i++){Check(Runtime.BlockIncrease(local,1,i),"positive local increase blocked");Check(!Runtime.BlockIncrease(local,-1,i)&&!Runtime.BlockIncrease(local,0,i),"recovery remains native");}
  Check(Runtime.DirtValue(local,80)==0,"direct dirt assignment protected");
  var remote=new PlayerStats();remote.photonView.IsMine=false;Runtime.Touch(remote);
  Check(remote.Updates==0&&!Runtime.BlockIncrease(remote,1,0)&&Runtime.DirtValue(remote,80)==80,"remote character unchanged");
  var npc=new PlayerStats{player=new Character()};Runtime.Touch(npc);Check(npc.Updates==0&&!Runtime.BlockIncrease(npc,1,2),"non-player unchanged");
  Action<long,char> set=(amount,nonce)=>{File.WriteAllText(Path.Combine(folder,"resource.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+session+"|"+new string(nonce,32)+"|1|"+amount);frame(7,true);};
  set(1234,'b');Check(loop.Credits==1234&&loop.Calls==1,"currency set through native setter");
  frame(7,true);Check(loop.Calls==1,"currency nonce consumed exactly once");
  set(0,'c');Check(loop.Credits==0,"zero currency supported");set(9999,'d');Check(loop.Credits==9999,"native currency maximum supported");
  int before=loop.Calls;set(10000,'e');Check(loop.Credits==9999&&loop.Calls==before&&File.ReadAllText(Path.Combine(folder,"status.txt")).Contains("entre 0 e 9999"),"above native maximum rejected without applying");
  Photon.Pun.PhotonNetwork.InRoom=true;Photon.Pun.PhotonNetwork.IsMasterClient=false;set(10,'f');
  Check(loop.Credits==9999&&loop.Calls==before&&Runtime.Enabled(0),"guest cannot overwrite shared balance but local controls remain usable");
  frame(1,true);Check(Runtime.BlockIncrease(local,1,0)&&!Runtime.BlockIncrease(local,1,1)&&!Runtime.BlockIncrease(local,1,2),"independent toggles");
  frame(0,true);Check(!Runtime.BlockIncrease(local,10,0)&&!Runtime.BlockIncrease(local,10,1)&&!Runtime.BlockIncrease(local,10,2)&&Runtime.DirtValue(local,80)==80,"disabled controls restore normal behavior");
  Photon.Pun.PhotonNetwork.InRoom=false;Photon.Pun.PhotonNetwork.IsMasterClient=true;
  frame(1<<4,true);var bucket=new Bucket();Runtime.TouchBucket(bucket);
  Check(bucket.IsFull&&bucket.Fills==1&&Runtime.BucketFull(bucket,false),"owned bucket refilled and cannot be emptied");
  bucket.photonView.IsMine=false;Check(!Runtime.BucketFull(bucket,false),"remote bucket unchanged");
  frame(1<<5,true);var dog=new Dog();Runtime.TouchDog(dog);Check(dog.CurrentEnergy==dog.MaxEnergy,"hungry dog refilled");
  Runtime.StorePetEnergy(dog,10);Check(dog.CurrentEnergy==100,"dog energy costs blocked");
  var otherPet=new Pet();Runtime.StorePetEnergy(otherPet,10);Check(otherPet.CurrentEnergy==10,"other pet types retain normal energy costs");
  dog.photonView.IsMine=false;Runtime.StorePetEnergy(dog,12);Check(dog.CurrentEnergy==12,"remote dog unchanged");
  frame((1<<6)|(1<<7),true);var enemy=new Vulnerable{isStunned=true,enemy=new EnemyAI()};
  Check(Runtime.HoldKnockout(enemy)&&Runtime.StunDelta(.02f,enemy)==0,"knocked out enemy recovery frozen");
  enemy.isStunned=false;Check(!Runtime.HoldKnockout(enemy)&&Runtime.StunDelta(.02f,enemy)==.02f,"active enemy is not forced into knockout");
  enemy.isStunned=true;enemy.enemy=null;Check(!Runtime.HoldKnockout(enemy),"non-enemy vulnerable objects unchanged");
  var egg=new Egg{spawnTimer=8};Runtime.StoreEggTimer(egg,.1f);Check(Runtime.HoldHatch()&&egg.spawnTimer==8,"forced hatch requests preserve the frozen timer");
  Photon.Pun.PhotonNetwork.InRoom=true;Photon.Pun.PhotonNetwork.IsMasterClient=false;Check(!Runtime.HoldHatch(),"guest cannot block the host's eggs");
  Photon.Pun.PhotonNetwork.InRoom=false;Photon.Pun.PhotonNetwork.IsMasterClient=true;
  frame(1<<8,true);var character=(PlayerCharacter)local.player;
  Check(Runtime.Movement(5,character)==10&&Runtime.Movement(8,character)==16,"movement speed and acceleration scale together");
  character.photonView.IsMine=false;Check(Runtime.Movement(5,character)==5,"remote movement unchanged");character.photonView.IsMine=true;
  UnityEngine.Time.timeScale=.75f;frame(1<<9,true);Check(UnityEngine.Time.timeScale==1.5f,"game speed uses original scale");
  loop.GamePaused=true;frame(1<<9,true);Check(UnityEngine.Time.timeScale==.75f,"paused menus retain original scale");
  loop.GamePaused=false;frame(1<<9,true);Check(UnityEngine.Time.timeScale==1.5f,"speed resumes after pause");
  frame(0,true);Check(UnityEngine.Time.timeScale==.75f&&!Runtime.HoldHatch()&&Runtime.BucketFull(bucket,false)==false,"disabled controls restore normal behavior and original scale");
  Runtime.StoreEggTimer(egg,.1f);Check(egg.spawnTimer==.1f,"hatch timer resumes");
  frame(1<<9,true);frame(1<<9,false);Check(UnityEngine.Time.timeScale==.75f,"leaving the match restores time scale");
  frame(7,true);Runtime.Tick(loop,true,true);Check(!Runtime.Enabled(0),"finished match cannot use controls");
  Console.WriteLine("Out of Space runtime checks passed.");
 }
}
