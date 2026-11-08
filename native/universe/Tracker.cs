using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ByteTrainer.Universe {
    // Read-only tracking: no resource grants, construction changes or save writes.
    internal static class Tracker {
        private static TrackerHud hud;
        public static void Update(Player player,bool crystals,bool expansions,bool paused) {
            if(!crystals&&!expansions) { if(hud!=null)hud.Hide();return; }
            if(player==null)return;
            if(hud==null||hud.Owner!=player) {
                if(hud!=null)UnityEngine.Object.Destroy(hud);
                hud=player.gameObject.AddComponent<TrackerHud>();hud.Owner=player;
            }
            hud.Refresh(crystals,expansions,paused);
        }
    }

    public sealed class TrackerHud:MonoBehaviour {
        public Player Owner;
        private Pickup crystal;
        private Construction expansion;
        private Level level;
        private bool crystalsOn,expansionsOn,paused;
        private long lastRefresh,nextScan,nextDiagnostic;
        private int crystalCount,expansionCount;
        private string fault="",requirements="";
        private GUIStyle titleStyle,detailStyle,markerStyle,markerLabelStyle;
        private readonly List<Construction> expansionCandidates=new List<Construction>();
        private static readonly FieldInfo PickupCache=typeof(Level).GetField("levelPickupCache",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo Collected=typeof(Pickup).GetField("_collected",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo ForceDisabled=typeof(Construction).GetField("_forceDisabled",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly Color CrystalColor=new Color(.65f,.88f,1f,1f),ExpansionColor=new Color(.8f,1f,.4f,1f);

        public void Hide() { crystalsOn=false;expansionsOn=false;crystal=null;expansion=null; }
        public void Refresh(bool crystals,bool expansions,bool isPaused) {
            bool changed=crystalsOn!=crystals||expansionsOn!=expansions;
            crystalsOn=crystals;expansionsOn=expansions;paused=isPaused;
            lastRefresh=DateTime.UtcNow.Ticks;
            if(Owner==null||Owner.Level==null) { crystal=null;expansion=null;return; }
            try {
                if(level!=Owner.Level) { level=Owner.Level;crystal=null;expansion=null;nextScan=0; }
                // Drop completed targets immediately, even between full scans.
                if(crystal!=null&&!CrystalAvailable(crystal)) { crystal=null;nextScan=0; }
                if(expansion!=null&&!ExpansionAvailable(expansion)) { expansion=null;nextScan=0; }
                if(changed||lastRefresh>=nextScan) {
                    nextScan=lastRefresh+TimeSpan.TicksPerMillisecond*350;
                    Scan();fault="";
                }
                if(lastRefresh>=nextDiagnostic) {
                    nextDiagnostic=lastRefresh+TimeSpan.TicksPerSecond;
                    Diagnostic();
                }
            } catch(Exception exception) {
                crystal=null;expansion=null;fault="Rastreador indisponível: "+exception.GetType().Name;
            }
        }
        private bool CrystalAvailable(Pickup pickup) {
            if(pickup==null||!pickup.Initialized||pickup.Level!=level)return false;
            if((bool)Collected.GetValue(pickup))return false;
            // Check the persistent native state too, including crystals loaded as collected.
            return SaveManager.I!=null&&SaveManager.I.SaveReady&&!SaveManager.I.CurrentLevel.GetPickupSaveData(pickup.ElementIndex).Completed;
        }
        private bool ExpansionAvailable(Construction construction) {
            return construction!=null&&construction.Initialized&&construction.Level==level&&construction.Unlocked&&!construction.Completed&&!(bool)ForceDisabled.GetValue(construction);
        }
        private void Scan() {
            crystal=null;expansion=null;crystalCount=0;expansionCount=0;requirements="";
            Vector3 position=Owner.transform.position;float crystalDistance=float.PositiveInfinity,expansionDistance=float.PositiveInfinity;
            if(crystalsOn) {
                var pickups=(Dictionary<Pickup,List<ResourceType>>)PickupCache.GetValue(level);
                foreach(var entry in pickups) {
                    if(!entry.Value.Contains(ResourceType.EnergyCrystal)||!CrystalAvailable(entry.Key))continue;
                    crystalCount++;
                    float distance=(entry.Key.transform.position-position).sqrMagnitude;
                    if(distance<crystalDistance) { crystalDistance=distance;crystal=entry.Key; }
                }
            }
            if(expansionsOn) {
                expansionCandidates.Clear();
                // Only terrain expansion sites; equipment upgrades and refineries are excluded.
                foreach(var tile in level.GroundTiles) {
                    if(tile==null||tile.Construction==null||!tile.Construction.Enabled)continue;
                    var construction=tile.Construction.Value;
                    if(!ExpansionAvailable(construction)||expansionCandidates.Contains(construction))continue;
                    expansionCandidates.Add(construction);expansionCount++;
                    float distance=(ExpansionPosition(construction)-position).sqrMagnitude;
                    if(distance<expansionDistance) { expansionDistance=distance;expansion=construction; }
                }
                if(expansion!=null) {
                    var text=new StringBuilder();
                    foreach(var entry in expansion.CurrentResources) {
                        int remaining=entry.Value.RemainingAmountConfirmed;
                        if(remaining<=0)continue;
                        if(text.Length>0)text.Append(" · ");
                        text.Append(ResourceLabel(entry.Key));text.Append(": ");text.Append(remaining.ToString("N0",CultureInfo.GetCultureInfo("pt-BR")));
                    }
                    requirements=text.ToString();
                }
            }
        }
        private static Vector3 ExpansionPosition(Construction site) {
            return site.Interactable!=null&&site.Interactable.Indicator!=null?site.Interactable.Indicator.position:site.transform.position;
        }
        private void Diagnostic() {
            string folder=Path.Combine(Path.GetTempPath(),"TrainerHub-2328750");Directory.CreateDirectory(folder);
            string text=System.Diagnostics.Process.GetCurrentProcess().Id+"|"+lastRefresh+"|"+level.Index+"|"+(Owner.ZoneTracker!=null&&Owner.ZoneTracker.IsDungeon?"dungeon":"world")+"|"+crystalCount+"|"+(crystal==null?-1:crystal.ElementIndex)+"|"+expansionCount+"|"+(expansion==null?-1:expansion.ElementIndex)+"|"+requirements;
            try { File.WriteAllText(Path.Combine(folder,"trackers.txt"),text); } catch(IOException) { }
        }
        private void OnGUI() {
            if(paused||Owner==null||Owner.State==null||!Owner.State.Alive||DateTime.UtcNow.Ticks-lastRefresh>TimeSpan.TicksPerSecond||(!crystalsOn&&!expansionsOn))return;
            var oldColor=GUI.color;var oldMatrix=GUI.matrix;
            try {
                var camera=Owner.CameraController!=null?Owner.CameraController.Camera:Camera.main;
                if(camera==null)return;
                if(titleStyle==null) {
                    titleStyle=new GUIStyle(GUI.skin.label);titleStyle.fontSize=16;titleStyle.fontStyle=FontStyle.Bold;
                    detailStyle=new GUIStyle(GUI.skin.label);detailStyle.fontSize=13;detailStyle.wordWrap=true;
                    markerStyle=new GUIStyle(titleStyle);markerStyle.alignment=TextAnchor.MiddleCenter;markerStyle.fontSize=23;
                    markerLabelStyle=new GUIStyle(detailStyle);markerLabelStyle.alignment=TextAnchor.MiddleCenter;
                }
                float scale=Mathf.Clamp(Screen.height/1080f,.75f,1.5f);
                GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
                float width=Screen.width/scale,height=Screen.height/scale,y=130;
                if(crystalsOn) { Panel(new Rect(width-348,y,328,80),"Energy Crystal",crystal==null?(fault.Length>0?fault:"Nenhum cristal pendente neste mapa"):Distance(crystal.transform.position)+" · "+crystalCount+" restantes",CrystalColor);y+=90; }
                if(expansionsOn)Panel(new Rect(width-348,y,328,100),"Expansão do mapa",expansion==null?(fault.Length>0?fault:"Nenhuma expansão disponível neste mapa"):Distance(ExpansionPosition(expansion))+" · "+expansionCount+" disponíveis\n"+requirements,ExpansionColor);
                if(crystalsOn&&crystal!=null)Marker(camera,crystal.transform.position,CrystalColor,"CRISTAL",width,height,scale);
                if(expansionsOn&&expansion!=null)Marker(camera,ExpansionPosition(expansion),ExpansionColor,"EXPANSÃO",width,height,scale);
            } catch { /* A scene can unload its camera and target during rendering. */ }
            finally { GUI.color=oldColor;GUI.matrix=oldMatrix; }
        }
        private string Distance(Vector3 point) { return Vector3.Distance(Owner.transform.position,point).ToString("0",CultureInfo.InvariantCulture)+" m"; }
        private void Panel(Rect rect,string title,string description,Color color) {
            GUI.color=new Color(.04f,.06f,.08f,.9f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=color;GUI.DrawTexture(new Rect(rect.x,rect.y,3,rect.height),Texture2D.whiteTexture);
            GUI.Label(new Rect(rect.x+14,rect.y+8,rect.width-24,24),title,titleStyle);
            GUI.color=Color.white;GUI.Label(new Rect(rect.x+14,rect.y+34,rect.width-24,rect.height-38),description,detailStyle);
        }
        private void Marker(Camera camera,Vector3 point,Color color,string label,float width,float height,float scale) {
            Vector3 projected=camera.WorldToScreenPoint(point+Vector3.up*.8f);
            Vector2 target=new Vector2(projected.x/scale,height-projected.y/scale);
            Vector2 center=new Vector2(width*.5f,height*.5f);
            bool offscreen=projected.z<=0||target.x<40||target.x>width-40||target.y<100||target.y>height-70;
            if(offscreen) {
                Vector2 direction=target-center;if(projected.z<=0)direction=-direction;
                if(direction.sqrMagnitude<.001f)direction=Vector2.down;
                float factor=Mathf.Min((width*.5f-60)/Mathf.Max(Mathf.Abs(direction.x),.001f),(height*.5f-110)/Mathf.Max(Mathf.Abs(direction.y),.001f));
                target=center+direction*factor;
                var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,target);
                GUI.color=color;GUI.Label(new Rect(target.x-16,target.y-16,32,32),"▶",markerStyle);GUI.matrix=matrix;
            } else { GUI.color=color;GUI.Label(new Rect(target.x-16,target.y-16,32,32),"◆",markerStyle); }
            var box=new Rect(Mathf.Clamp(target.x-95,8,width-198),Mathf.Clamp(target.y+18,70,height-44),190,28);
            GUI.color=new Color(.04f,.06f,.08f,.9f);GUI.DrawTexture(box,Texture2D.whiteTexture);
            GUI.color=color;GUI.Label(box,label+" · "+Distance(point),markerLabelStyle);
        }
        private static string ResourceLabel(ResourceType resource) { switch((int)resource) {
                case 4: return "Aço";
                case 29: return "Adventurer";
                case 73: return "Água";
                case 34: return "Alien Shell";
                case 48: return "Amethyst Refined";
                case 9: return "Ametista";
                case 87: return "Arenito";
                case 56: return "Argila";
                case 8: return "Azurita";
                case 41: return "Azurite Refined";
                case 74: return "Bambu";
                case 19: return "Bear";
                case 36: return "Bolota";
                case 12: return "Cacto";
                case 54: return "Calcário";
                case 99: return "Candy Corn";
                case 26: return "Captain";
                case 31: return "Caranguejo";
                case 16: return "Carne";
                case 22: return "Carvão";
                case 62: return "Cerberus Collar (limite 10)";
                case 24: return "Chave";
                case 43: return "Chave azul";
                case 120: return "Chave branca";
                case 44: return "Chave verde";
                case 42: return "Chave vermelha";
                case 39: return "Chocolate";
                case 96: return "Cleopatra Heart";
                case 10: return "Cogumelo";
                case 104: return "Cookie";
                case 112: return "Copper";
                case 113: return "Copper Ingot";
                case 57: return "Corrupted Azurite";
                case 45: return "Cristal de energia";
                case 18: return "Crock";
                case 106: return "Cupid Feather";
                case 60: return "Cyclope Eye (limite 10)";
                case 14: return "Diamante";
                case 50: return "Diamond Refined";
                case 38: return "Doce";
                case 77: return "Dragonstone Air (limite 10)";
                case 79: return "Dragonstone Fire (limite 10)";
                case 81: return "Dragonstone Light (limite 10)";
                case 80: return "Dragonstone Poison (limite 10)";
                case 82: return "Dragonstone Shadow (limite 10)";
                case 78: return "Dragonstone Water (limite 10)";
                case 94: return "Emerald Tablet (limite 10)";
                case 21: return "Engrenagem";
                case 6: return "Enxofre";
                case 105: return "Event Coin";
                case 28: return "Farmer (limite 10)";
                case 37: return "Ficha";
                case 65: return "Fire Crystal";
                case 68: return "Fireflies";
                case 75: return "Firework";
                case 66: return "Gradient Crystal";
                case 72: return "Hard Plank";
                case 88: return "Hard Rock";
                case 58: return "Harpie Wings (limite 10)";
                case 89: return "Jar";
                case 119: return "Katana Blade";
                case 118: return "Katana Collar";
                case 117: return "Katana Handle";
                case 116: return "Kelp";
                case 125: return "Key DLC1";
                case 103: return "Key Gold Blue";
                case 102: return "Key Gold Red";
                case 107: return "Koi Fish Black";
                case 108: return "Koi Fish White";
                case 20: return "Lã";
                case 101: return "Ladder";
                case 11: return "Lava";
                case 109: return "Leaf";
                case 90: return "Leite";
                case 46: return "Maçã";
                case 1: return "Madeira";
                case 70: return "Madeira de Lei";
                case 55: return "Mármore";
                case 61: return "Medusa Head (limite 10)";
                case 59: return "Minotaur Head (limite 10)";
                case 7: return "Moedas";
                case 124: return "Óleo de Peixe";
                case 69: return "Opal";
                case 40: return "Ornament";
                case 17: return "Osso";
                case 2: return "Pedra";
                case 30: return "Pedra fantasma";
                case 53: return "Peixe";
                case 32: return "Pergaminho";
                case 122: return "Pérola";
                case 49: return "Pétala";
                case 92: return "Plan";
                case 33: return "Plazma";
                case 91: return "Poção";
                case 76: return "Pólvora";
                case 63: return "Poseidon Crown (limite 10)";
                case 85: return "Prata";
                case 35: return "Presente";
                case 67: return "Puffin Tree";
                case 95: return "Purification Staff (limite 10)";
                case 115: return "Qilin Horn";
                case 83: return "Resource Crate";
                case 93: return "Rice";
                case 15: return "Rubi";
                case 64: return "Rubi Corrompido";
                case 47: return "Ruby Refined";
                case 86: return "Sal";
                case 114: return "Ship Builder";
                case 100: return "Snow";
                case 97: return "Soul";
                case 98: return "Soul Cleopatra (limite 10)";
                case 13: return "Source Core (limite 10)";
                case 110: return "Soy";
                case 71: return "Space Crystal";
                case 121: return "Spirit Leaf";
                case 52: return "Sulphur Refined";
                case 3: return "Tábuas";
                case 27: return "Tailor (limite 10)";
                case 84: return "Tinta";
                case 111: return "Tofu";
                case 23: return "Trigo";
                case 25: return "Troll (limite 10)";
                case 123: return "Tungsten";
                case 5: return "Urânio";
                case 51: return "Uranium Refined";
                default: return resource.ToString();
            } }
    }
}
