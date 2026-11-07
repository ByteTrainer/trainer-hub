using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        public static void Tick() {
            long now=DateTime.UtcNow.Ticks;
            if(now<nextPoll)return;
            nextPoll=now+TimeSpan.TicksPerMillisecond*100;
            try {
                var parts=File.ReadAllText(Path.Combine(Folder,"settings.txt")).Trim().Split('|');
                long expiry;int mask;
                if(parts.Length!=5||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<=now||expiry>now+TimeSpan.TicksPerSecond*5||!int.TryParse(parts[2],out mask)||mask<0||mask>63||parts[3].Length!=32)throw new InvalidDataException();
                var numbers=parts[4].Split(',');
                float value;
                if(numbers.Length!=6||!float.TryParse(numbers[5],NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||value<0.1f||value>10f)throw new InvalidDataException();
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
            if(parts.Length!=6||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<now||expiry>now+TimeSpan.TicksPerSecond*5||parts[2]!=session||parts[3].Length!=32||parts[3]==lastAction||!int.TryParse(parts[4],out kind)||(kind!=1&&kind!=2)||!long.TryParse(parts[5],out value)||value<0||value>999999999999L)return;
            lastAction=parts[3];ack=lastAction;applied=0;error="";
            try {
                if(kind==1) { Services.Economy.SetMoney(value);applied=Services.Economy.Money; }
                else {
                    var snapshot=Services.Wizard.Capture();
                    snapshot.Spirits=value;
                    snapshot.SpiritsLifetime=Math.Max(snapshot.SpiritsLifetime,value);
                    Services.Wizard.Restore(snapshot,false,false,false);
                    applied=Services.Wizard.Spirits;
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
