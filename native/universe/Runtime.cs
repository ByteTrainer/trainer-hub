using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Pawns.Attributes;
namespace ByteTrainer.Universe  {
    public static class Runtime  {
        private static readonly string Folder=Path.Combine(Path.GetTempPath(),"TrainerHub-2328750");
        private static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
        private static readonly int Pid=Process.GetCurrentProcess().Id;
        private static Player player;
        private static long deadline,nextPoll,lastPlayer;
        private static int flags;
        private static string session="",lastAction="",ack="",error="";
        private static int applied;
        private const int CommandCount=18;
        private static float[] values=new float[CommandCount];
        private static bool speedOwned;
        private static float baseSpeed=1f;
        // Native ResourceSave validation clears the inventory above ten of these special items.
        private static readonly int[] LimitedResources={77,79,81,80,82,78,60,62,94,28,58,61,59,63,95,98,13,27,25};
        public static bool Enabled(int index) {
            return DateTime.UtcNow.Ticks<deadline&&(flags&(1<<index))!=0;
        }
        public static float Factor(int index) {
            return Enabled(index)?values[index]:1f;
        }
        private static bool IsMain(Pawn pawn) {
            return pawn!=null&&pawn is Player&&((Player)pawn).IsMainPlayer;
        }
        private static bool Ready() {
            return player!=null&&player.State!=null&&player.State.Alive&&DateTime.UtcNow.Ticks-lastPlayer<TimeSpan.TicksPerSecond*2;
        }
        public static void Touch(Player value) {
            if(value!=null&&value.IsMainPlayer) {
                player=value;
                lastPlayer=DateTime.UtcNow.Ticks;
            }
        }
        public static void Tick(bool paused,ref float previousTimeScale) {
            long now=DateTime.UtcNow.Ticks;
            if(now<nextPoll)return;
            nextPoll=now+TimeSpan.TicksPerMillisecond*100;
            try {
                string[] parts=File.ReadAllText(Path.Combine(Folder,"settings.txt")).Trim().Split('|');
                long expiry;
                int mask;
                if(parts.Length!=5||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<=now||expiry>now+TimeSpan.TicksPerSecond*5||!int.TryParse(parts[2],out mask)||mask<0||mask>262143||parts[3].Length!=32)throw new InvalidDataException();
                string[] numbers=parts[4].Split(',');
                if(numbers.Length!=CommandCount)throw new InvalidDataException();
                var parsed=new float[CommandCount];
                for(int i=0;i<CommandCount;i++)if(!float.TryParse(numbers[i],NumberStyles.Float,Culture,out parsed[i])||float.IsNaN(parsed[i])||float.IsInfinity(parsed[i])||parsed[i]<0.1f||parsed[i]>(i==6?100f:i==13?5f:20f))throw new InvalidDataException();
                if(session!=parts[3]) {
                    session=parts[3];
                    lastAction="";
                    ack="";
                    error="";
                }
                flags=mask;
                values=parsed;
                deadline=expiry;
            }
            catch {
                flags=0;
                deadline=0;
            }
            try {
                Tracker.Update(player,Enabled(16)&&Ready(),Enabled(17)&&Ready(),paused);
                bool speed=Enabled(13)&&Ready();
                if(speed&&!speedOwned) {
                    baseSpeed=paused?previousTimeScale:Time.timeScale;
                    if(baseSpeed<=0)baseSpeed=1;
                    speedOwned=true;
                }
                if(speedOwned) {
                    float desired=speed?Math.Min(5f,baseSpeed*Factor(13)):baseSpeed;
                    if(paused)previousTimeScale=desired;
                    else Time.timeScale=desired;
                    if(!speed)speedOwned=false;
                }
                if(Ready()&&deadline>now)ApplyResource(now);
                Directory.CreateDirectory(Folder);
                Atomic(Path.Combine(Folder,"status.txt"),"2|"+Pid+"|"+now+"|"+(Ready()?"1":"0")+"|"+flags+"|"+session+"|"+ack+"|"+applied+"|"+error);
            }
            catch(IOException) {
                // A reader may briefly hold the status file. Retry on the next update.
            }
            catch(Exception exception) {
                flags=0;
                deadline=0;
                error="Falha no controle: "+exception.GetType().Name;
            }
        }
        private static void Atomic(string file,string text) {
            string temp=file+".new";
            File.WriteAllText(temp,text);
            if(File.Exists(file))File.Replace(temp,file,null);
            else File.Move(temp,file);
        }
        private static void ApplyResource(long now) {
            string file=Path.Combine(Folder,"resource.txt");
            if(!File.Exists(file))return;
            string[] parts;
            try {
                parts=File.ReadAllText(file).Trim().Split('|');
            }
            catch {
                return;
            }
            long expiry;
            int kind,delta;
            if(parts.Length!=6||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<now||expiry>now+TimeSpan.TicksPerSecond*5||parts[2]!=session||parts[3].Length!=32||parts[3]==lastAction||!int.TryParse(parts[4],out kind)||!int.TryParse(parts[5],out delta)||Math.Abs((long)delta)>9999999||delta==0||kind<=0||!Enum.IsDefined(typeof(ResourceType),kind))return;
            lastAction=parts[3];
            ack=lastAction;
            applied=0;
            error="";
            // Consume the request before mutating the inventory. The Hub never retries a timed-out action.
            try {
                if(SaveManager.I==null||!SaveManager.I.SaveReady)throw new InvalidOperationException("Save não disponível");
                var save=SaveManager.I.Resources;
                int before=save.GetResourceAmount((ResourceType)kind);
                int maximum=Array.IndexOf(LimitedResources,kind)>=0?10:99999999;
                if(before>maximum&&(long)before+delta>maximum) {
                    error="O estoque já ultrapassa o limite nativo. Remova pelo menos "+(before-maximum)+" unidades deste item.";
                    return;
                }
                long next=Math.Max(0,Math.Min(maximum,(long)before+delta));
                int change=(int)(next-before);
                if(change!=0)save.AddResource((ResourceType)kind,change,true,false,true);
                applied=save.GetResourceAmount((ResourceType)kind)-before;
            }
            catch(Exception exception) {
                error="Recurso não aplicado: "+exception.GetType().Name;
            }
        }
        public static bool BlockHealth(PawnHealth health,Pawn pawn,int next) {
            return Enabled(0)&&IsMain(pawn)&&next<health.HealthCurrent&&health.HealthCurrent>0;
        }
        public static bool Combat(Pawn target,ref float damage,Pawn attacker) {
            if(target==null||target.Health==null)return false;
            if(Enabled(0)&&IsMain(target))return true;
            if(damage>0&&IsMain(attacker)&&!IsMain(target)) {
                damage=Enabled(2)?Math.Max(damage,target.Health.HealthCurrent):Math.Min(100000000,damage*Factor(4));
            }
            return false;
        }
        public static void ResourceDamage(Resource resource,ref float damage,Pawn attacker) {
            if(Enabled(3)&&IsMain(attacker))damage=Math.Max(damage,resource.HealthCurrent+1);
        }
        public static float Attribute(float original,Pawns.Attributes.Attribute attribute) {
            if(player==null||player.Attributes==null)return original;
            var attributes=player.Attributes;
            int index=ReferenceEquals(attribute,attributes.AttackRange)?6:ReferenceEquals(attribute,attributes.AttackSpeed)?7:ReferenceEquals(attribute,attributes.GatherSpeed)?8:ReferenceEquals(attribute,attributes.GroundMovementSpeedMultiplier)?9:ReferenceEquals(attribute,attributes.WaterMovementSpeedMultiplier)?10:-1;
            if(index<0)return original;
            return index==9||index==10?(1+original)*Factor(index)-1:original*Factor(index);
        }
        public static Vector3 Movement(Vector3 original,Pawn owner,bool swimming) {
            return IsMain(owner)?original*Factor(swimming?10:9):original;
        }
        public static int Experience(Pawns.Attributes.PlayerAttributes attributes,int total) {
            if(player==null||!ReferenceEquals(player.Attributes,attributes)||!Enabled(5))return total;
            int current=attributes.Experience;
            return total<=current?total:(int)Math.Min(2000000000,(double)current+(total-current)*Factor(5));
        }
        public static double Regeneration(double original) {
            return Enabled(11)?0:original;
        }
        public static int RefiningAmount(int original,RefineryCore core) {
            return Enabled(15)&&Ready()?core.AmountQueued:original;
        }
        public static bool ShouldRefine(Refinery refinery) {
            return Enabled(15)&&Ready()&&refinery!=null&&refinery.Core.AmountQueued>0;
        }
        public static bool BlockCustom(int deaths,PawnData data) {
            return Enabled(14)&&deaths>0&&data!=null&&(int)data.Gang>(int)Gang.Player;
        }
        public static bool BlockLoop(bool looping,int wave,int waveCount) {
            return Enabled(14)&&looping&&waveCount>0&&wave>=waveCount;
        }
    }
}
