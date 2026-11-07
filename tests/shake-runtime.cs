using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using ShakeItCo.Core;
using ByteTrainer.Shake;
class TestSave:ISaveService { public int Calls;public void Save(SaveReason reason){Calls++;}public void Load(){} }
class ShakeTest {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main(){
  var bus=new EventBus();Services.Economy=new EconomyService(bus);Services.Wizard=new WizardService(bus,null,()=>0);var save=new TestSave();Services.Save=save;
  Runtime.Touch();
  string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-3578070");Directory.CreateDirectory(folder);
  int pid=Process.GetCurrentProcess().Id;string session=new string('a',32);
  Action<int> settings=mask=>File.WriteAllText(Path.Combine(folder,"settings.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+mask+"|"+session+"|1,1,1,1,1,2");
  settings(60);Runtime.Tick();
  Check(Runtime.Clock(1)==0&&Runtime.Movement(3)==6,"clock and movement");Check(Runtime.AutoChest(false)&&Runtime.EasyFishing()&&Runtime.Bite(false),"minigames enabled");
  Action<int,long,char> set=(kind,value,nonce)=>{File.WriteAllText(Path.Combine(folder,"resource.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+session+"|"+new string(nonce,32)+"|"+kind+"|"+value);Thread.Sleep(120);Runtime.Touch();settings(60);Runtime.Tick();};
  Services.Economy.SetMoney(999);set(1,0,'b');Check(Services.Economy.Money==0&&save.Calls==1,"coins can be set to zero");
  Thread.Sleep(120);Runtime.Tick();Check(save.Calls==1,"request is consumed once");
  set(1,999999999999L,'c');Check(Services.Economy.Money==999999999999L,"large coin balance");
  var before=Services.Wizard.Capture();set(2,250,'d');Check(Services.Wizard.Spirits==250&&Services.Wizard.Step==before.Step,"souls preserve progression");
  set(2,0,'e');Check(Services.Wizard.Spirits==0&&Services.Wizard.SpiritsLifetime>=250,"souls can be set to zero");
  set(1,-1,'f');Check(Services.Economy.Money==999999999999L,"negative balance rejected");
  var ring=new ChestLock();ring.Begin((ChestTier)1,0,0,true);bool hit=false;
  for(int i=0;i<1000;i++){ring.Tick(.016f);if(Runtime.AutoChest(false)&&ring.PerfectOpen){Check(ring.Press().ToString()=="Perfect","perfect chest timing");hit=true;break;}}
  Check(hit,"perfect chest window reached");
  settings(0);Thread.Sleep(120);Runtime.Tick();Check(Runtime.Clock(1)==1&&Runtime.Movement(3)==3&&!Runtime.AutoChest(false)&&!Runtime.EasyFishing(),"disabled controls restore native behavior");
  Console.WriteLine("Shake runtime checks passed.");
 }
}

