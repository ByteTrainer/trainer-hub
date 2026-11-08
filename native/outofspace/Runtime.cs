using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Photon.Pun;

namespace ByteTrainer.OutOfSpace {
    public static class Runtime {
        private static readonly string Folder=Path.Combine(Path.GetTempPath(),"TrainerHub-400080");
        private static readonly int Pid=Process.GetCurrentProcess().Id;
        private static LoopManager loop;
        private static PlayerStats local;
        private static bool running;
        private static long deadline,nextPoll,lastPlayer,nextDiagnostic;
        private static int flags;
        private static string session="",lastAction="",ack="",error="";
        private static long applied;
        private static readonly FieldInfo PlayerField=typeof(PlayerStats).GetField("player",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

        private static bool IsLocal(PlayerStats stats) {
            return stats!=null&&PlayerField.GetValue(stats) is PlayerCharacter&&stats.photonView!=null&&stats.photonView.IsMine;
        }
        private static bool Ready() {
            return loop!=null&&running&&local!=null&&IsLocal(local)&&DateTime.UtcNow.Ticks-lastPlayer<TimeSpan.TicksPerSecond*2;
        }
        public static bool Enabled(int index) { return index>=0&&index<3&&Ready()&&DateTime.UtcNow.Ticks<deadline&&(flags&(1<<index))!=0; }
        public static bool BlockIncrease(PlayerStats stats,int amount,int index) { return amount>0&&IsLocal(stats)&&Enabled(index); }
        public static int DirtValue(PlayerStats stats,int amount) { return IsLocal(stats)&&Enabled(2)?0:amount; }
        public static void Touch(PlayerStats stats) {
            if(!IsLocal(stats))return;
            local=stats;lastPlayer=DateTime.UtcNow.Ticks;
            // Reset through the native routines, keeping feedback and normal recovery in sync.
            if(Enabled(0)&&stats.currentFatigue!=0)stats.UpdateFatigue(0,true,false);
            if(Enabled(1)&&stats.currentHunger!=0)stats.UpdateHunger(0,true);
            if(Enabled(2)&&stats.currentDirtiness!=0)stats.UpdateDirt(0,true,true);
        }
        public static void Tick(LoopManager value,bool started,bool finished) {
            loop=value;running=started&&!finished;
            long now=DateTime.UtcNow.Ticks;
            if(now<nextPoll)return;
            nextPoll=now+TimeSpan.TicksPerMillisecond*100;
            try {
                var parts=File.ReadAllText(Path.Combine(Folder,"settings.txt")).Trim().Split('|');
                long expiry;int mask;
                if(parts.Length!=5||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<=now||expiry>now+TimeSpan.TicksPerSecond*5||!int.TryParse(parts[2],out mask)||mask<0||mask>15||parts[3].Length!=32)throw new InvalidDataException();
                var numbers=parts[4].Split(',');
                if(numbers.Length!=4)throw new InvalidDataException();
                foreach(var number in numbers) { float parsed;if(!float.TryParse(number,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)||parsed!=1f)throw new InvalidDataException(); }
                if(session!=parts[3]) { session=parts[3];lastAction="";ack="";error=""; }
                flags=mask;deadline=expiry;
            } catch { flags=0;deadline=0; }
            try {
                if(Ready()&&deadline>now)ApplyCurrency(now);
                Directory.CreateDirectory(Folder);
                Atomic(Path.Combine(Folder,"status.txt"),"2|"+Pid+"|"+now+"|"+(Ready()?"1":"0")+"|"+flags+"|"+session+"|"+ack+"|"+applied+"|"+error);
                if(Ready()&&now>=nextDiagnostic) {
                    nextDiagnostic=now+TimeSpan.TicksPerSecond;
                    Atomic(Path.Combine(Folder,"diagnostic.txt"),Pid+"|"+now+"|"+flags+"|"+local.currentFatigue+"|"+local.currentHunger+"|"+local.currentDirtiness+"|"+loop.Credits+"|"+(PhotonNetwork.InRoom&&!PhotonNetwork.IsMasterClient?"guest":"host"));
                }
            } catch(IOException) { } catch(Exception exception) { flags=0;deadline=0;error="Falha na integração: "+exception.GetType().Name; }
        }
        private static void ApplyCurrency(long now) {
            string file=Path.Combine(Folder,"resource.txt");
            if(!File.Exists(file))return;
            string[] parts;
            try { parts=File.ReadAllText(file).Trim().Split('|'); } catch(IOException) { return; }
            long expiry,amount;int kind;
            if(parts.Length!=6||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<now||expiry>now+TimeSpan.TicksPerSecond*5||parts[2]!=session||parts[3].Length!=32||parts[3]==lastAction||!int.TryParse(parts[4],out kind)||kind!=1||!long.TryParse(parts[5],out amount))return;
            lastAction=parts[3];ack=lastAction;applied=0;error="";
            if(amount<0||amount>9999) { error="Informe um valor inteiro entre 0 e 9999.";return; }
            if(PhotonNetwork.InRoom&&!PhotonNetwork.IsMasterClient) { error="Defina as moedas na partida solo ou no computador que hospeda a sala.";return; }
            try { loop.Credits=(int)amount;applied=loop.Credits; }
            catch(Exception exception) { error="Confira o saldo antes de repetir: "+exception.GetType().Name; }
        }
        private static void Atomic(string file,string text) {
            string temporary=file+".new";File.WriteAllText(temporary,text);
            if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
        }
    }
}
