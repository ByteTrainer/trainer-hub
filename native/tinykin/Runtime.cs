using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

namespace ByteTrainer.Tinykin {
    public sealed class Bridge : MonoBehaviour {
        private void Update() { Runtime.Tick(); }
        private void OnGUI() { Tracker.Draw(); }
        private void OnApplicationFocus(bool focused) { Runtime.SetFocus(focused); }
        private void OnApplicationPause(bool paused) { Runtime.SetFocus(!paused&&Application.isFocused); }
        private void OnApplicationQuit() { Runtime.Shutdown(); }
        private void OnDestroy() { Runtime.Shutdown(); }
    }
    public static class Runtime {
        private const int CommandCount=12;
        private const int TrackerMask=((1<<CommandCount)-1)&~63;
        private static readonly string Folder=Path.Combine(Path.GetTempPath(),"TrainerHub-1599020");
        private static readonly int Pid=Process.GetCurrentProcess().Id;
        private static Bridge bridge;
        private static readonly object StateGate=new object();
        private static Timer heartbeat;
        private static bool focused=true,cachedReady,stopping;
        private static long lastMainTick;
        private static string cachedDiagnostic="-|0|0|0|0|False|";
        private static long deadline,nextPoll,nextStatus;
        private static int flags;
        private static float movement=2f;
        private static string session="",fatal="";
        private static long jumps,protectedHits,levelFrames;
        public static void Boot() {
            try {
                if(bridge!=null)return;
                var gameObject=new GameObject("ByteTrainer.Tinykin.Bridge");
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                bridge=gameObject.AddComponent<Bridge>();
                focused=Application.isFocused;stopping=false;
                heartbeat=new Timer(delegate { Heartbeat(); },null,250,250);
            } catch(Exception error) { Fail(error); }
        }
        public static bool Ready() {
            return fatal.Length==0&&Game.IsGameStarted&&Game.PLAYER!=null&&!Game.PLAYER.IsRespawning;
        }
        public static bool Enabled(int index) {
            lock(StateGate)return index>=0&&index<CommandCount&&Ready()&&DateTime.UtcNow.Ticks<deadline&&(flags&(1<<index))!=0;
        }
        public static void SetFocus(bool value) { lock(StateGate)focused=value; }
        public static void Shutdown() {
            lock(StateGate) { stopping=true;flags=0;deadline=0;cachedReady=false;if(heartbeat!=null)heartbeat.Dispose();heartbeat=null; }
        }
        private static bool Local(PlayerController player) { return player!=null&&player==Game.PLAYER; }
        public static bool Protect(PlayerController player) { return Local(player)&&Enabled(0); }
        public static bool BlockDeath(PlayerController player,string tag) {
            // Null-tag deaths are the game's fall recovery; preserve them to avoid trapping Milo outside the map.
            bool block=Protect(player)&&!string.IsNullOrEmpty(tag);
            if(block)protectedHits++;
            return block;
        }
        public static bool Danger(bool original,PlayerController player) { return Protect(player)?false:original; }
        public static bool JumpAllowed(PlayerController player,bool original) {
            if(!Local(player)||!Enabled(1))return original;
            return !player.ControlsLocked&&!player.IsDrowning&&player.CeilingHeight-player.transform.position.y>1.5f;
        }
        public static bool Anticipate(PlayerController player,bool original) { return Local(player)&&Enabled(1)?false:original; }
        public static void CountJump(PlayerController player) { if(Local(player)&&Enabled(1))jumps++; }
        public static float Movement(float original,PlayerController player) { return Local(player)&&Enabled(2)?original*movement:original; }
        public static bool FreeFuel(PlayerController player) { return Local(player)&&Enabled(4); }
        public static Vector3 GlideVelocity(Vector3 original,PlayerController player) {
            if(Local(player)&&Enabled(5)&&player.Gliding&&!player.ControlsLocked&&original.y<=0) { original.y=0;levelFrames++; }
            return original;
        }
        public static float RaceDelta() { return Enabled(3)?0f:Time.deltaTime; }
        public static float CameraHeight(float original,PlayerCamera camera) {
            var player=Game.PLAYER;
            if((Enabled(1)||Enabled(5))&&player!=null&&!player.TouchesGround&&!player.ControlsLocked&&camera!=null&&camera==PlayerCamera.INSTANCE&&camera.Target==player.transform&&!camera.FreeCameraMode&&!camera.IsCinematic&&!camera.IsInCinematicTransition)
                return player.transform.position.y;
            return original;
        }
        private static void Fail(Exception error) {
            flags=0;deadline=0;cachedReady=false;fatal=error.GetType().Name+": "+error.Message;
            try { Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"error.txt"),error.ToString()); }catch{}
        }
        private static string ReadShared(string file) {
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream))return reader.ReadToEnd();
        }
        private static void Atomic(string file,string value) {
            string temp=file+".new";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.Read|FileShare.Delete))
            using(var writer=new StreamWriter(stream))writer.Write(value);
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
        private static void PollSettings(long now) {
            if(now>=nextPoll) {
                nextPoll=now+TimeSpan.TicksPerMillisecond*100;
                try {
                    var parts=ReadShared(Path.Combine(Folder,"settings.txt")).Trim().Split('|');
                    long expiry;int mask;
                    if(parts.Length!=5||parts[0]!=Pid.ToString()||!long.TryParse(parts[1],out expiry)||expiry<=now||expiry>now+TimeSpan.TicksPerSecond*5||!int.TryParse(parts[2],out mask)||mask<0||mask>=(1<<CommandCount)||parts[3].Length!=32)throw new InvalidDataException();
                    var values=parts[4].Split(',');
                    if(values.Length!=CommandCount)throw new InvalidDataException();
                    float speed=2;
                    for(int i=0;i<CommandCount;i++) {
                        float value;
                        if(!float.TryParse(values[i],NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||(i==2?value<.25f||value>5f:value!=1f))throw new InvalidDataException();
                        if(i==2)speed=value;
                    }
                    flags=mask;deadline=expiry;session=parts[3];movement=speed;
                } catch(InvalidDataException) { flags=0;deadline=0; }
                  catch(IOException) { /* A brief sharing error cannot extend the existing lease. */ }
                  catch(UnauthorizedAccessException) {}
                  catch(Exception error) { Fail(error); }
            }
        }
        private static void Publish(long now,bool background) {
            if(now<nextStatus)return;
            nextStatus=now+TimeSpan.TicksPerMillisecond*250;
            try {
                Directory.CreateDirectory(Folder);
                int active=deadline>now&&fatal.Length==0?flags:0;
                Atomic(Path.Combine(Folder,"status.txt"),"2|"+Pid+"|"+now+"|"+(cachedReady&&fatal.Length==0?"1":"0")+"|"+active+"|"+session+"||0|");
                Atomic(Path.Combine(Folder,"diagnostic.txt"),Pid+"|"+now+"|"+active+"|"+cachedDiagnostic+"|"+(background?"background":"main")+"|"+Math.Max(0,(now-lastMainTick)/TimeSpan.TicksPerMillisecond));
            } catch(IOException) { /* Retry publication next pulse without destroying a valid command lease. */ }
              catch(UnauthorizedAccessException) {}
        }
        // The Unity main loop can stop while minimized or behind the Hub overlay.
        // This worker touches only IPC and snapshots of plain data, never Unity objects.
        public static void Heartbeat() {
            if(!Monitor.TryEnter(StateGate))return;
            try {
                long now=DateTime.UtcNow.Ticks;
                if(stopping||focused||now-lastMainTick<TimeSpan.TicksPerMillisecond*750)return;
                PollSettings(now);
                Publish(now,true);
            } catch(Exception error) { Fail(error); }
              finally { Monitor.Exit(StateGate); }
        }
        public static void Tick() {
            int trackerMask=0;
          lock(StateGate) {
            if(stopping)return;
            long now=DateTime.UtcNow.Ticks;lastMainTick=now;
            PollSettings(now);
            try {
                if(Enabled(4)) {
                    var player=Game.PLAYER;
                    if(player.MaxUsableFuel>0&&player.JetpackFuel<player.MaxUsableFuel)player.ReloadJetpackFuel(false);
                }
                if(Enabled(3))TimerHooks.Ensure();
                cachedReady=Ready();
                trackerMask=cachedReady&&now<deadline?flags&TrackerMask:0;
                var currentPlayer=Game.PLAYER;
                cachedDiagnostic=(currentPlayer!=null?currentPlayer.JetpackFuel.ToString(CultureInfo.InvariantCulture):"-")+"|"+(currentPlayer!=null&&currentPlayer.Gliding?"1":"0")+"|"+jumps+"|"+protectedHits+"|"+levelFrames+"|"+TimerHooks.Installed+"|"+fatal.Replace('|','/');
                Publish(now,false);
            } catch(Exception error) { Fail(error); }
          }
          Tracker.Update(trackerMask);
        }
    }
}
