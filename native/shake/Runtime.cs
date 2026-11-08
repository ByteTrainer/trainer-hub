using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using ShakeItCo.Core;
namespace ByteTrainer.Shake {
    public static class Runtime {
        private static readonly string Folder=Path.Combine(Path.GetTempPath(),"TrainerHub-3578070");
        private static readonly int Pid=Process.GetCurrentProcess().Id;
        private static long deadline,nextPoll,lastPlayer;
        private static int flags;
        private static float movement=2;
        private static string session="",lastAction="",ack="",error="";
        private static long applied;
        private static float shakeWait;
        private const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly FieldInfo DealOrder=typeof(BlackjackRound).GetField("_dealOrder",PrivateInstance);
        private static readonly FieldInfo DealStep=typeof(BlackjackRound).GetField("_dealStep",PrivateInstance);
        private static readonly FieldInfo ShoeCards=typeof(BlackjackShoe).GetField("_cards",PrivateInstance);
        private static readonly FieldInfo ShoeNext=typeof(BlackjackShoe).GetField("_next",PrivateInstance);
        private static readonly PropertyInfo Pot=typeof(SlotMachineService).GetProperty("Pot");
        public static void Touch() { lastPlayer=DateTime.UtcNow.Ticks; }
        private static bool Ready() {
            return Services.Economy!=null&&Services.Wizard!=null&&Services.Save!=null&&DateTime.UtcNow.Ticks-lastPlayer<TimeSpan.TicksPerSecond*2;
        }
        public static bool Enabled(int index) { return Ready()&&DateTime.UtcNow.Ticks<deadline&&(flags&(1<<index))!=0; }
        public static float Movement(float original) { return Enabled(5)?original*movement:original; }
        public static float Clock(float delta) { return Enabled(3)?0:delta; }
        public static bool AutoChest(bool original) { return original||Enabled(4); }
        public static bool EasyFishing() { return Enabled(2); }
        public static bool Bite(bool pressed) { return pressed||Enabled(2); }
        public static bool Afk() { return Enabled(10); }
        public static float CastPower(float original) { return Afk()?1f:original; }
        public static float RecastPower(float original) { return Afk()?FishCast.PowerOf(1f):original; }
        public static bool PerfectCast(bool original) { return original||Afk(); }
        public static float BonusTime(float delta) { return Enabled(11)?0:delta; }
        public static bool AutoSale(bool original) { return original||Enabled(12); }
        public static bool AutoSaleFor(bool original,string player) { return original||(Enabled(12)&&Services.Economy!=null&&player==Services.Economy.LocalPlayerId); }
        public static bool Shake(float delta) {
            if(!Enabled(9)||delta<=0) { shakeWait=0;return false; }
            shakeWait-=Math.Min(delta,.2f);
            if(shakeWait>0)return false;
            shakeWait=.2f;return true;
        }
        // Replace only the next local deal, restoring the shoe even when no card was consumed.
        // Dealer cards, the random generator and every other seat retain native behavior.
        public static void BlackjackTick(BlackjackService service,double delta,ulong owner) {
            if(!Enabled(7)||service.Mirrored) { service.Tick(delta);return; }
            var state=service.State;var seat=state.SeatOf(owner);
            Card[] cards=null;int next=-1;Card original=Card.None;
            if(seat!=null&&seat.InHand) {
                if(state.Phase==BlackjackPhase.Dealing) {
                    var order=(List<int>)DealOrder.GetValue(service.Round);int step=(int)DealStep.GetValue(service.Round);
                    if(step<order.Count&&order[step]>=0&&state.Seats[order[step]]==seat) {
                        cards=(Card[])ShoeCards.GetValue(service.Round.Shoe);next=(int)ShoeNext.GetValue(service.Round.Shoe);
                        if(next>=0&&next<cards.Length) {
                            original=cards[next];
                            if(seat.Cards.Count==1)seat.Cards[0]=new Card(0,(CardSuit)0);
                            cards[next]=new Card(seat.Cards.Count==0?0:9,(CardSuit)0);
                        } else next=-1;
                    }
                }
                // Also supports enabling the control during an unfinished hand.
                if((state.Phase==BlackjackPhase.Dealing||state.Phase==BlackjackPhase.PlayerTurn)&&seat.Cards.Count>=2&&seat.Total!=21) {
                    seat.Cards.Clear();seat.Cards.Add(new Card(0,(CardSuit)0));seat.Cards.Add(new Card(9,(CardSuit)0));state.Revision++;
                }
            }
            try { service.Tick(delta); }
            finally { if(next>=0)cards[next]=original; }
        }
        public static bool MoveBlackjack(BlackjackRound round,ulong owner,BlackjackMove move) {
            var state=round.State;var seat=state.SeatOf(owner);
            if(Enabled(7)&&state.IsTurnOf(owner)&&seat!=null&&seat.State==BlackjackSeatState.Playing) {
                seat.Cards.Clear();seat.Cards.Add(new Card(0,(CardSuit)0));seat.Cards.Add(new Card(9,(CardSuit)0));
                // A hand already at 21 stands instead of taking another card and busting.
                move=BlackjackMove.Stand;
            }
            return round.Move(owner,move);
        }
        public static int SpinMany(SlotMachineService service,string player,int rows,List<SlotSpinResult> results,CasinoCharms charms) {
            if(!Enabled(8))return service.TrySpinMany(player,rows,results,charms);
            int count=0;
            for(int i=0;i<SlotMachineService.ClampRows(rows);i++) {
                // Do not leave a forced spin pending when the bet cannot be paid.
                if(!service.CanSpin(player))break;
                service.ForceNextSpin(SlotSymbol.Co,SlotSymbol.Co,SlotSymbol.Co);
                SlotSpinResult spin;
                if(!service.TrySpin(player,charms,out spin))break;
                // The native result already contains the pot plus this spin's contribution.
                // Preserve that accumulated value without resetting unrelated statistics.
                Pot.SetValue(service,spin.FromPot,null);
                if(results!=null)results.Add(spin);count++;
            }
            return count;
        }
        public static void Tick() {
            long now=DateTime.UtcNow.Ticks;
            if(now<nextPoll)return;
            nextPoll=now+TimeSpan.TicksPerMillisecond*100;
            try {
                var parts=File.ReadAllText(Path.Combine(Folder,"settings.txt")).Trim().Split('|');
                long expiry;int mask;
                if(parts.Length!=5||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<=now||expiry>now+TimeSpan.TicksPerSecond*5||!int.TryParse(parts[2],out mask)||mask<0||mask>8191||parts[3].Length!=32)throw new InvalidDataException();
                var numbers=parts[4].Split(',');
                float value;
                if(numbers.Length!=13||!float.TryParse(numbers[5],NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||value<0.1f||value>10f)throw new InvalidDataException();
                if(session!=parts[3]) { session=parts[3];lastAction="";ack="";error=""; }
                flags=mask;movement=value;deadline=expiry;
            } catch { flags=0;deadline=0; }
            try {
                if(Ready()&&deadline>now)ApplyCurrency(now);
                Directory.CreateDirectory(Folder);
                Atomic(Path.Combine(Folder,"status.txt"),"2|"+Pid+"|"+now+"|"+(Ready()?"1":"0")+"|"+flags+"|"+session+"|"+ack+"|"+applied+"|"+error);
            } catch(IOException) { } catch(Exception exception) { flags=0;deadline=0;error="Falha na integração: "+exception.GetType().Name; }
        }
        private static void ApplyCurrency(long now) {
            string file=Path.Combine(Folder,"resource.txt");
            if(!File.Exists(file))return;
            string[] parts;
            try { parts=File.ReadAllText(file).Trim().Split('|'); } catch(IOException) { return; }
            long expiry,value;int kind;
            if(parts.Length!=6||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<now||expiry>now+TimeSpan.TicksPerSecond*5||parts[2]!=session||parts[3].Length!=32||parts[3]==lastAction||!int.TryParse(parts[4],out kind)||(kind<1||kind>3)||!long.TryParse(parts[5],out value)||value<0||value>999999999999L)return;
            lastAction=parts[3];ack=lastAction;applied=0;error="";
            try {
                if(kind==1) { Services.Economy.SetMoney(value);applied=Services.Economy.Money; }
                else if(kind==2) {
                    var snapshot=Services.Wizard.Capture();
                    snapshot.Spirits=value;
                    snapshot.SpiritsLifetime=Math.Max(snapshot.SpiritsLifetime,value);
                    Services.Wizard.Restore(snapshot,false,false,false);
                    applied=Services.Wizard.Spirits;
                }
                else {
                    var cashier=Services.Cashier;
                    if(cashier==null)throw new InvalidOperationException("Casino unavailable");
                    string player=Services.Economy.LocalPlayerId;
                    var row=cashier.CaptureRow(player);row.Chips=value;
                    cashier.ApplyRow(player,row);applied=cashier.ChipsOf(player);
                }
            } catch(Exception exception) { error="Confira o saldo antes de repetir: "+exception.GetType().Name;return; }
            try { Services.Save.Save(SaveReason.Manual); }
            catch(Exception exception) { error="Valor aplicado, mas houve falha ao salvar: "+exception.GetType().Name; }
        }
        private static void Atomic(string file,string text) {
            string temporary=file+".new";File.WriteAllText(temporary,text);
            if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
        }
    }
}
