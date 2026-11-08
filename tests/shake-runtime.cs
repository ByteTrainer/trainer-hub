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
  Action<int> settings=mask=>File.WriteAllText(Path.Combine(folder,"settings.txt"),pid+"|"+DateTime.UtcNow.AddSeconds(3).Ticks+"|"+mask+"|"+session+"|1,1,1,1,1,2,1,1,1,1,1,1,1");
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
  Services.Cashier=new CashierService(bus);
  var cashier=Services.Cashier.CaptureRow(Services.Economy.LocalPlayerId);cashier.Won=50;cashier.Spent=10;
  Services.Cashier.ApplyRow(Services.Economy.LocalPlayerId,cashier);
  set(3,12345,'g');Check(Services.Cashier.ChipsOf(Services.Economy.LocalPlayerId)==12345,"casino chip balance");
  var cashierAfter=Services.Cashier.CaptureRow(Services.Economy.LocalPlayerId);
  Check(cashierAfter.Won==50&&cashierAfter.Spent==10,"chips preserve casino records");
  set(3,0,'h');Check(Services.Cashier.ChipsOf(Services.Economy.LocalPlayerId)==0,"zero chips");
  Action<int> activate=mask=>{Thread.Sleep(120);Runtime.Touch();settings(mask);Runtime.Tick();};
  activate(8191);
  Check(Runtime.Afk()&&Runtime.CastPower(.2f)==1&&Runtime.PerfectCast(false)&&Runtime.RecastPower(.2f)==FishCast.PowerOf(1),"AFK maximum cast");
  Check(Runtime.BonusTime(1)==0&&Runtime.AutoSale(false),"bonus and sale toggles");
  Check(Runtime.AutoSaleFor(false,Services.Economy.LocalPlayerId)&&!Runtime.AutoSaleFor(false,"another-player"),"sale applies only to local player");
  Check(Runtime.Shake(.016f)&&!Runtime.Shake(.016f),"automatic shake rate limited");
  var spells=new SpellService(bus);spells.Begin(ChestLootId.AbundanceBook,20);
  var frenzy=new FrenzyService(bus);frenzy.Begin(20);var lake=new LakeFrenzyService(bus);lake.Begin(20);
  float spellLeft=spells.SecondsLeft(ChestLootId.AbundanceBook),frenzyLeft=frenzy.SecondsLeft,lakeLeft=lake.SecondsLeft;
  spells.Tick(Runtime.BonusTime(5));frenzy.Tick(Runtime.BonusTime(5));lake.Tick(Runtime.BonusTime(5));
  Check(spells.SecondsLeft(ChestLootId.AbundanceBook)==spellLeft&&frenzy.SecondsLeft==frenzyLeft&&lake.SecondsLeft==lakeLeft,"all temporary timers are frozen without replacing effects");
  var slots=new SlotMachineService(Services.Economy,123);var spins=new System.Collections.Generic.List<SlotSpinResult>();
  long pot=slots.Pot;int count=Runtime.SpinMany(slots,Services.Economy.LocalPlayerId,5,spins,CasinoCharms.None);
  Check(count==5&&spins.All(s=>s.Win==SlotWin.Jackpot),"every row gives a native jackpot");
  Check(slots.Pot>=pot&&slots.Pot==spins.Last().FromPot&&slots.Jackpots==5,"jackpot preserves pot and native statistics");
  var blackjack=new BlackjackService(Services.Economy,321);ulong owner=42;
  blackjack.Round.Press(owner,Services.Economy.LocalPlayerId,100);blackjack.Round.Press(owner,Services.Economy.LocalPlayerId,100);
  for(int i=0;i<100&&blackjack.State.Phase!=BlackjackPhase.Settled;i++)Runtime.BlackjackTick(blackjack,.5,owner);
  Check(blackjack.State.SeatOf(owner).Total==21,"local Blackjack hand is actually 21 at native settlement");
  Check(blackjack.State.Phase==BlackjackPhase.Settled&&blackjack.State.Dealer.Count>=2,"Blackjack completes native settlement");
  var hand=blackjack.State.SeatOf(owner);blackjack.State.Phase=BlackjackPhase.PlayerTurn;
  blackjack.State.Turn=blackjack.State.SeatIndexOf(owner);hand.State=BlackjackSeatState.Playing;
  hand.Cards.Clear();hand.Cards.Add(new Card(9,(CardSuit)0));hand.Cards.Add(new Card(7,(CardSuit)0));
  Check(Runtime.MoveBlackjack(blackjack.Round,owner,BlackjackMove.Hit)&&hand.Total==21&&hand.State!=BlackjackSeatState.Busted,"enabling during a hand cannot bust when requesting another card");
  activate(0);
  Check(!Runtime.Afk()&&Runtime.CastPower(.2f)==.2f&&!Runtime.Shake(1)&&!Runtime.AutoSale(false)&&Runtime.BonusTime(1)==1,"new controls restore when disabled");
  spells.Tick(Runtime.BonusTime(1));frenzy.Tick(Runtime.BonusTime(1));lake.Tick(Runtime.BonusTime(1));
  Check(spells.SecondsLeft(ChestLootId.AbundanceBook)==spellLeft-1&&frenzy.SecondsLeft==frenzyLeft-1&&lake.SecondsLeft==lakeLeft-1,"bonus timers resume from preserved remaining time");
  spins.Clear();Runtime.SpinMany(slots,Services.Economy.LocalPlayerId,5,spins,CasinoCharms.None);
  Check(spins.Any(s=>s.Win!=SlotWin.Jackpot),"jackpot override leaves no forced win after disable");
  settings(0);Thread.Sleep(120);Runtime.Tick();Check(Runtime.Clock(1)==1&&Runtime.Movement(3)==3&&!Runtime.AutoChest(false)&&!Runtime.EasyFishing(),"disabled controls restore native behavior");
  Console.WriteLine("Shake runtime checks passed.");
 }
}

