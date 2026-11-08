using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ByteTrainer.Tinykin {
    internal static class Tracker {
        private static readonly TrackerScanner Scanner=new TrackerScanner();
        private static readonly TrackerTarget[] Selected=new TrackerTarget[4];
        private static readonly int[] Counts=new int[4];
        private static readonly string[] Titles={"Pollen tracker","Tinykin tracker","Mission tracker","Collectible tracker"};
        private static readonly Color[] Colors={new Color(1f,.82f,.25f),new Color(.55f,1f,.4f),new Color(.35f,.85f,1f),new Color(.85f,.55f,1f)};
        private static long nextScan,nextUpdate,lastUpdate,nextDiagnostic;
        private static int activeMask;
        private static GUIStyle titleStyle,bodyStyle,markerStyle;
        internal static void Update(int mask) {
            long now=DateTime.UtcNow.Ticks;
            if(mask==0) { activeMask=0;nextScan=0;Scanner.Targets.Clear();return; }
            try {
                if(mask!=activeMask||now>=nextScan) { Scanner.Scan(mask);nextScan=now+TimeSpan.TicksPerSecond; }
                activeMask=mask;
                if(now<nextUpdate)return;
                nextUpdate=now+TimeSpan.TicksPerMillisecond*100;
                Vector3 origin=Game.PLAYER.transform.position;
                for(int i=0;i<4;i++)Selected[i]=Scanner.Nearest(i,origin,out Counts[i]);
                lastUpdate=now;
                if(now>=nextDiagnostic) {
                    nextDiagnostic=now+TimeSpan.TicksPerSecond*2;
                    var text=new StringBuilder();
                    for(int i=0;i<4;i++)text.AppendLine(Titles[i]+" | "+Counts[i]+" | "+(Selected[i]==null?"none":Selected[i].Anchor.GetInstanceID()+" "+Selected[i].Label+" @ "+Selected[i].Position()));
                    foreach(string line in Scanner.QuestDiagnostics)text.AppendLine(line);
                    File.WriteAllText(Path.Combine(Path.GetTempPath(),"TrainerHub-1599020/trackers.txt"),text.ToString());
                }
            } catch(Exception error) {
                // A tracker fault must never disable the trainer or its IPC heartbeat.
                activeMask=0;nextScan=now+TimeSpan.TicksPerSecond*2;
                try {File.WriteAllText(Path.Combine(Path.GetTempPath(),"TrainerHub-1599020/tracker-error.txt"),error.ToString());}catch{}
            }
        }
        internal static void Draw() {
            if(activeMask==0||!Application.isFocused||Time.timeScale==0||DateTime.UtcNow.Ticks-lastUpdate>TimeSpan.TicksPerSecond||!Runtime.Ready())return;
            Color oldColor=GUI.color;Matrix4x4 oldMatrix=GUI.matrix;
            try {
                if(titleStyle==null) {
                    titleStyle=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold,wordWrap=true};
                    bodyStyle=new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true};
                    markerStyle=new GUIStyle(GUI.skin.box){fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true};
                }
                float scale=Mathf.Clamp(Screen.height/900f,.7f,1.6f);
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                float width=Screen.width/scale,height=Screen.height/scale,y=90;
                var camera=Camera.main;
                for(int i=0;i<4;i++) {
                    if((activeMask&(64<<i))==0)continue;
                    var target=Selected[i];
                    bool valid=target!=null&&target.Valid();
                    GUI.color=new Color(.035f,.05f,.065f,.9f);GUI.DrawTexture(new Rect(width-320,y,300,85),Texture2D.whiteTexture);
                    GUI.color=Colors[i];GUI.DrawTexture(new Rect(width-320,y,3,85),Texture2D.whiteTexture);
                    GUI.Label(new Rect(width-306,y+7,277,23),TrackerText.Get(Titles[i]),titleStyle);
                    string details=valid?target.Label+" · "+Vector3.Distance(Game.PLAYER.transform.position,target.Position()).ToString("0")+" m":TrackerText.Get("No eligible target in loaded areas");
                    GUI.color=Color.white;GUI.Label(new Rect(width-306,y+31,277,47),details+"\n"+Counts[i]+" "+TrackerText.Get("available targets"),bodyStyle);
                    y+=94;
                    if(!valid||camera==null)continue;
                    Vector3 screen=camera.WorldToScreenPoint(target.Position());
                    Vector2 point=new Vector2(screen.x/scale-width/2,height/2-screen.y/scale);
                    if(screen.z<0)point=-point;
                    bool off=screen.z<0||Mathf.Abs(point.x)>width/2-90||Mathf.Abs(point.y)>height/2-60;
                    if(off) {
                        float factor=Mathf.Min((width/2-90)/Mathf.Max(1,Mathf.Abs(point.x)),(height/2-60)/Mathf.Max(1,Mathf.Abs(point.y)));
                        point*=factor;
                    }
                    string arrow=off?(Mathf.Abs(point.x)>Mathf.Abs(point.y)?(point.x>0?">":"<"):(point.y>0?"v":"^")):"+";
                    GUI.color=Colors[i];GUI.Box(new Rect(width/2+point.x-65,height/2+point.y-22,130,44),arrow+" "+TrackerText.Get(Titles[i])+"\n"+Vector3.Distance(Game.PLAYER.transform.position,target.Position()).ToString("0")+" m",markerStyle);
                }
            } catch { /* Destroyed objects during scene changes are retried next scan. */ }
            finally {GUI.color=oldColor;GUI.matrix=oldMatrix;}
        }
    }
}
