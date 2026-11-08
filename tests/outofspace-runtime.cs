using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ByteTrainer.OutOfSpace;
namespace Photon.Pun {
 public class PhotonView { public bool IsMine=true; }
 public static class PhotonNetwork { public static bool InRoom=false,IsMasterClient=true; }
}
public class Character {}
public class PlayerCharacter:Character {}
public class PlayerStats {
 public Character player=new PlayerCharacter();public Photon.Pun.PhotonView photonView=new Photon.Pun.PhotonView();
 public int currentFatigue=82,currentHunger=33,currentDirtiness=44,Updates;
 public void UpdateFatigue(int value,bool absolute,bool percentage){currentFatigue=absolute?value:currentFatigue+value;Updates++;}
 public void UpdateHunger(int value,bool absolute){currentHunger=absolute?value:currentHunger+value;Updates++;}
 public void UpdateDirt(int value,bool absolute,bool bypass){currentDirtiness=absolute?value:currentDirtiness+value;Updates++;}
}
public class LoopManager { private int credits;public int Calls;public int Credits {get{return credits;}set{credits=Math.Max(0,Math.Min(9999,value));Calls++;}} }
class OutOfSpaceTest {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main(){
  string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-400080");Directory.CreateDirectory(folder);
  int pid=Process.GetCurrentProcess().Id;string session=new string('a',32);var loop=new LoopManager();var local=new PlayerStats();
  Action<int,bool> frame=(mask,started)=>{Thread.Sleep(120);File.WriteAllText(Path.Combine(folder,"settings.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+mask+"|"+session+"|1,1,1,1");Runtime.Touch(local);Runtime.Tick(loop,started,false);Runtime.Touch(local);};
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
  frame(7,true);Runtime.Tick(loop,true,true);Check(!Runtime.Enabled(0),"finished match cannot use controls");
  Console.WriteLine("Out of Space runtime checks passed.");
 }
}
