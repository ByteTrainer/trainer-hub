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
        private static readonly FieldInfo PetEnergyField=typeof(Pet).GetField("currentEnergy",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo EnemyField=typeof(Vulnerable).GetField("enemy",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static float movement=2f,gameSpeed=2f,baseTimeScale=1f;
        private static bool speedOwned;
        private static long bucketRefills,dogRefills,heldEnemies,heldEggs;

        private static bool IsLocal(PlayerStats stats) {
            return stats!=null&&PlayerField.GetValue(stats) is PlayerCharacter&&stats.photonView!=null&&stats.photonView.IsMine;
        }
        private static bool Ready() {
            return loop!=null&&running&&local!=null&&IsLocal(local)&&DateTime.UtcNow.Ticks-lastPlayer<TimeSpan.TicksPerSecond*2;
        }
        public static bool Enabled(int index) { return index>=0&&index<10&&index!=3&&Ready()&&DateTime.UtcNow.Ticks<deadline&&(flags&(1<<index))!=0; }
        private static bool Authority() { return !PhotonNetwork.InRoom||PhotonNetwork.IsMasterClient; }
        public static bool BucketFull(Bucket bucket,bool full) {
            if(!full&&Enabled(4)&&bucket!=null&&bucket.photonView!=null&&bucket.photonView.IsMine) { bucketRefills++;return true; }
            return full;
        }
        public static void TouchBucket(Bucket bucket) {
            if(Enabled(4)&&bucket!=null&&bucket.photonView!=null&&bucket.photonView.IsMine&&!bucket.IsFull) { bucket.FillBucket(true,false);bucketRefills++; }
        }
        public static void StorePetEnergy(Pet pet,int value) {
            if(pet==null)return;
            if(pet is Dog&&Enabled(5)&&pet.photonView!=null&&pet.photonView.IsMine) { value=pet.MaxEnergy;dogRefills++; }
            PetEnergyField.SetValue(pet,value);
        }
        public static void TouchDog(Pet pet) {
            if(pet is Dog&&Enabled(5)&&pet.photonView!=null&&pet.photonView.IsMine&&pet.CurrentEnergy!=pet.MaxEnergy)StorePetEnergy(pet,pet.MaxEnergy);
        }
        public static bool HoldKnockout(Vulnerable target) {
            return Enabled(6)&&Authority()&&target!=null&&target.isStunned&&(EnemyAI)EnemyField.GetValue(target)!=null;
        }
        public static float StunDelta(float delta,Vulnerable target) { if(HoldKnockout(target)) { heldEnemies++;return 0; }return delta; }
        public static bool HoldHatch() { if(Enabled(7)&&Authority()) { heldEggs++;return true; }return false; }
        public static void StoreEggTimer(Egg egg,float value) { if(!HoldHatch())egg.spawnTimer=value; }
        public static float Movement(float original,PlayerCharacter character) {
            return Enabled(8)&&character!=null&&character.photonView!=null&&character.photonView.IsMine?original*movement:original;
        }
        private static void UpdateTimeScale() {
            bool active=Enabled(9)&&Authority();
            bool paused=loop!=null&&loop.GamePaused;
            if(active&&!speedOwned&&!paused&&UnityEngine.Time.timeScale>0) { baseTimeScale=UnityEngine.Time.timeScale;speedOwned=true; }
            if(speedOwned) {
                UnityEngine.Time.timeScale=active&&!paused?Math.Min(5f,Math.Max(.1f,baseTimeScale*gameSpeed)):baseTimeScale;
                if(!active)speedOwned=false;
            }
        }
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
                if(parts.Length!=5||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<=now||expiry>now+TimeSpan.TicksPerSecond*5||!int.TryParse(parts[2],out mask)||mask<0||mask>1023||parts[3].Length!=32)throw new InvalidDataException();
                var numbers=parts[4].Split(',');
                if(numbers.Length!=10)throw new InvalidDataException();
                var parsed=new float[10];
                for(int i=0;i<10;i++)if(!float.TryParse(numbers[i],NumberStyles.Float,CultureInfo.InvariantCulture,out parsed[i])||float.IsNaN(parsed[i])||float.IsInfinity(parsed[i])||(i<8?parsed[i]!=1f:parsed[i]<.1f||parsed[i]>(i==8?10f:5f)))throw new InvalidDataException();
                if(session!=parts[3]) { session=parts[3];lastAction="";ack="";error=""; }
                flags=mask;deadline=expiry;movement=parsed[8];gameSpeed=parsed[9];
            } catch { flags=0;deadline=0; }
            try {
                UpdateTimeScale();
                if(Ready()&&deadline>now)ApplyCurrency(now);
                Directory.CreateDirectory(Folder);
                Atomic(Path.Combine(Folder,"status.txt"),"2|"+Pid+"|"+now+"|"+(Ready()?"1":"0")+"|"+flags+"|"+session+"|"+ack+"|"+applied+"|"+error);
                if(Ready()&&now>=nextDiagnostic) {
                    nextDiagnostic=now+TimeSpan.TicksPerSecond;
                    Atomic(Path.Combine(Folder,"diagnostic.txt"),Pid+"|"+now+"|"+flags+"|"+local.currentFatigue+"|"+local.currentHunger+"|"+local.currentDirtiness+"|"+loop.Credits+"|"+(Authority()?"host":"guest")+"|"+UnityEngine.Time.timeScale.ToString(CultureInfo.InvariantCulture)+"|"+bucketRefills+"|"+dogRefills+"|"+heldEnemies+"|"+heldEggs);
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
