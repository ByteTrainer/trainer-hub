using System;
using System.Collections.Generic;
using System.Linq;
using ByteTrainer.Tinykin;
namespace UnityEngine {
 public class Object {
  public static List<Object> All=new List<Object>();
  public Object(){All.Add(this);}
  public static T[] FindObjectsOfType<T>() where T:Component {return All.OfType<T>().Where(c=>c.gameObject.activeInHierarchy&&c.gameObject.scene.isLoaded).ToArray();}
 }
 public struct Scene {public bool isLoaded;public bool IsValid(){return true;}}
 public class GameObject {public bool activeInHierarchy=true;public Scene scene=new Scene{isLoaded=true};}
 public class Component:Object {
  public GameObject gameObject=new GameObject();public Transform transform=new Transform();public string name="fixture";
  public T GetComponent<T>() where T:class {return this as T;}
 }
 public class MonoBehaviour:Component {public bool isActiveAndEnabled=true;}
 public class Transform {public Vector3 position;public GameObject gameObject=new GameObject();}
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public float sqrMagnitude{get{return x*x+y*y+z*z;}}
  public static Vector3 up{get{return new Vector3(0,1,0);}}
  public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
  public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
 }
 public struct Bounds {public Vector3 center;}
 public class Collider {public bool enabled=true;public Bounds bounds;}
}
namespace UnityEngine.Events {
 public enum UnityEventCallState {Off, EditorAndRuntime, RuntimeOnly}
 public class PersistentCall {
  public UnityEngine.Object Target;public string Method;private UnityEventCallState m_CallState;
  public PersistentCall(UnityEngine.Object target,string method,UnityEventCallState state){Target=target;Method=method;m_CallState=state;}
 }
 public class PersistentCallGroup {
  private List<PersistentCall> m_Calls=new List<PersistentCall>();
  public List<PersistentCall> Calls{get{return m_Calls;}}
 }
 public class UnityEventBase {
  private PersistentCallGroup m_PersistentCalls=new PersistentCallGroup();
  public int GetPersistentEventCount(){return m_PersistentCalls.Calls.Count;}
  public string GetPersistentMethodName(int index){return m_PersistentCalls.Calls[index].Method;}
  public UnityEngine.Object GetPersistentTarget(int index){return m_PersistentCalls.Calls[index].Target;}
  public void Bind(UnityEngine.Object target,string method="Execute",UnityEventCallState state=UnityEventCallState.RuntimeOnly){m_PersistentCalls.Calls.Add(new PersistentCall(target,method,state));}
 }
 public class UnityEvent<A,B>:UnityEventBase {}
}
public class SubSceneData {public bool[] pollen;}
public class Collectible:UnityEngine.MonoBehaviour {public bool IsCollected;public SubSceneData parentScene;public int ID;}
public class CollectibleContainer:UnityEngine.MonoBehaviour {public int pollenCount;private int firstIndex;public void Fill(int count){firstIndex=count;}}
public enum SbirzType {Carrier,Explosive,Ladder,Bridge,Electric}
public class SbirzEgg:UnityEngine.MonoBehaviour {public bool IsCollected;public SbirzType sbirzType;public UnityEngine.Transform eggTransform;}
public class CollectibleItemData {public string displayName;public bool ignoreCollected;}
public class CollectibleItem {public CollectibleItemData ItemData;public bool Collected;}
public class UniqueCollectible:UnityEngine.MonoBehaviour {public bool Collected;public UnityEngine.Collider trigger;public UnityEngine.Transform rendererTransform;public CollectibleItem collectibleItem;}
public class FilteredOnExecute {public string filterEntry;public UnityEngine.Events.UnityEvent<string,Trigger> OnExecute;}
public class Trigger:UnityEngine.MonoBehaviour {public bool IsCompleted;public string entryName;public UnityEngine.Events.UnityEvent<string,Trigger> OnExecute;public FilteredOnExecute[] filteredOnExecutes;}
public class InteractionTrigger:Trigger {public bool IsSelectable=true,IsFullLife;public UnityEngine.Bounds InteractionBounds;}
public class Cage:InteractionTrigger {public int amount;public SbirzType sbirzType;}
public class SmallLiftable:InteractionTrigger {public bool ArrivedAtDestination;}
public class SbirzTrigger:InteractionTrigger {public SmallLiftable[] requiredObjects;}
public class SimpleNPC:UnityEngine.MonoBehaviour {public string npcName;}
public class ContextualZone:UnityEngine.MonoBehaviour {}
public class Dialogue:Trigger {
 public SimpleNPC Host=new SimpleNPC();public NPCDialogData dialogData;public int DialogueIndex;
 public bool IsActionAvailable=true,IsActionDisabled;
 private ContextualZone contextualZone=new ContextualZone();
 public ContextualZone Zone{get{return contextualZone;}}
}
public class NPCDialogData {public List<ContextDialog> dialogs;}
public struct ContextDialog {public string startTrigger;public List<DialogEntry> dialog;}
public struct DialogEntry {public List<DialogLine> lines;}
public struct DialogLine {public string trigger;}
public class Quest:Trigger {
 public enum QuestStatus {Hidden,Active,Completed}
 public QuestStatus Status;public QuestObjective[] objectives;public string displayName,questName;
}
public class QuestObjective {public int Progress,Success;public Trigger[] triggers;}
public class MultiTrigger:UnityEngine.MonoBehaviour {
 public Trigger[] triggers;private Trigger trigger;private Dictionary<Trigger,bool> validations=new Dictionary<Trigger,bool>();
 public void Setup(Trigger output,params Trigger[] children){trigger=output;triggers=children;foreach(var child in children)validations[child]=false;}
 public void Complete(Trigger child){validations[child]=true;}
}
namespace ByteTrainer.Tinykin {internal static class TrackerText {internal static string Get(string key){return key;}}}
class TrackerTests {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main(){
  var scanner=new TrackerScanner();int count;
  var near=new Collectible();near.transform.position=new UnityEngine.Vector3(1,0,0);
  var far=new Collectible();far.transform.position=new UnityEngine.Vector3(4,0,0);
  var saved=new Collectible {parentScene=new SubSceneData{pollen=new[]{true}}};
  var hidden=new Collectible();hidden.gameObject.activeInHierarchy=false;
  var unloaded=new Collectible();unloaded.gameObject.scene=new UnityEngine.Scene{isLoaded=false};
  scanner.Scan(64);Check(scanner.Nearest(0,new UnityEngine.Vector3(),out count).Anchor==near&&count==2,"saved, hidden and unloaded pollen excluded");
  near.IsCollected=true;Check(scanner.Nearest(0,new UnityEngine.Vector3(),out count).Anchor==far&&count==1,"collected target immediately replaced without rescan");
  var container=new CollectibleContainer{pollenCount=4};scanner.Scan(64);
  Check(scanner.Nearest(0,new UnityEngine.Vector3(),out count).Anchor==container&&count==2,"unopened container tracked independently");
  container.Fill(4);Check(scanner.Nearest(0,new UnityEngine.Vector3(),out count).Anchor==far&&count==1,"empty container excluded");
  var egg=new SbirzEgg();scanner.Scan(128);Check(scanner.Nearest(1,new UnityEngine.Vector3(),out count).Anchor==egg,"uncollected egg found");
  egg.IsCollected=true;Check(scanner.Nearest(1,new UnityEngine.Vector3(),out count)==null&&count==0,"collected egg excluded");
  var output=new Trigger();var one=new InteractionTrigger();var two=new InteractionTrigger();
  var multi=new MultiTrigger();multi.Setup(output,one,two);multi.Complete(one);
  var step=new QuestObjective{Success=1,triggers=new[]{output}};
  var quest=new Quest{Status=Quest.QuestStatus.Active,displayName="Posters",objectives=new[]{step}};
  scanner.Scan(256);Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count).Anchor==two&&count==1,"aggregated quest uses only unfinished physical child");
  step.Progress=1;Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count)==null,"completed objective immediately hidden");
  step.Progress=0;one.IsCompleted=true;two.IsCompleted=true;
  var logic=new Trigger();var source=new InteractionTrigger{OnExecute=new UnityEngine.Events.UnityEvent<string,Trigger>()};source.OnExecute.Bind(logic);
  var side=new Quest {Status=Quest.QuestStatus.Active,displayName="Side",objectives=new[]{new QuestObjective{Success=1,triggers=new[]{logic}}}};
  scanner.Scan(256);Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count).Anchor==source&&count==1,"persistent event resolves logical trigger to physical interaction");
  source.gameObject.activeInHierarchy=false;Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count)==null,"scene departure drops old target");
  var rare=new UniqueCollectible{trigger=new UnityEngine.Collider(),collectibleItem=new CollectibleItem{ItemData=new CollectibleItemData{displayName="Letter"}}};
  scanner.Scan(512);Check(scanner.Nearest(3,new UnityEngine.Vector3(),out count).Anchor==rare,"visible special item tracked");
  rare.collectibleItem.Collected=true;Check(scanner.Nearest(3,new UnityEngine.Vector3(),out count)==null,"saved special item excluded");
  var cage=new Cage{amount=3,sbirzType=SbirzType.Explosive};
  var broken=new Cage{amount=2,IsCompleted=true};
  var empty=new Cage{amount=0};
  scanner.Scan(128);Check(scanner.Nearest(1,new UnityEngine.Vector3(),out count).Anchor==cage&&count==1,"only unopened cages containing tinykins are counted");
  cage.IsFullLife=true;Check(scanner.Nearest(1,new UnityEngine.Vector3(),out count)==null,"opened cage removed before next discovery");
  var delivered=new SmallLiftable{ArrivedAtDestination=true};
  var letter=new SmallLiftable();letter.transform.position=new UnityEngine.Vector3(3,0,0);
  var letter2=new SmallLiftable();letter2.transform.position=new UnityEngine.Vector3(5,0,0);
  var unrelated=new SmallLiftable();
  var mailbox=new SbirzTrigger{requiredObjects=new[]{delivered,letter,letter2,letter2}};
  var post=new Quest{questName="PostOffice",Status=Quest.QuestStatus.Hidden,objectives=new[]{new QuestObjective{triggers=new[]{mailbox}}}};
  scanner.Scan(1024);Check(scanner.Nearest(4,new UnityEngine.Vector3(),out count).Anchor==letter&&count==2,"post office tracks only undelivered unique letters, even when optional quest is hidden");
  letter.ArrivedAtDestination=true;Check(scanner.Nearest(4,new UnityEngine.Vector3(),out count).Anchor==letter2&&count==1,"delivery switches to the next letter without discovery");
  post.Status=Quest.QuestStatus.Completed;Check(scanner.Nearest(4,new UnityEngine.Vector3(),out count)==null,"finished mail quest hides letters");
  var pending=new Quest{questName="Side story",Status=Quest.QuestStatus.Hidden};
  var later=new Quest{questName="Locked",Status=Quest.QuestStatus.Hidden};
  var disabled=new Quest{questName="Disabled event",Status=Quest.QuestStatus.Hidden};
  var npc=new Dialogue{dialogData=new NPCDialogData{dialogs=new List<ContextDialog>{
    new ContextDialog{dialog=new List<DialogEntry>{new DialogEntry{lines=new List<DialogLine>{new DialogLine{trigger="StartQuest"}}}}},
    new ContextDialog{startTrigger="Later"}
  }}};
  npc.Host.npcName="Guide";
  var start=new UnityEngine.Events.UnityEvent<string,Trigger>();start.Bind(pending,"Activate");start.Bind(disabled,"Activate",UnityEngine.Events.UnityEventCallState.Off);
  var unlock=new UnityEngine.Events.UnityEvent<string,Trigger>();unlock.Bind(later,"Activate");
  npc.filteredOnExecutes=new[]{new FilteredOnExecute{filterEntry="StartQuest",OnExecute=start},new FilteredOnExecute{filterEntry="Later",OnExecute=unlock}};
  scanner.Scan(256);Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count).Anchor==npc.Host&&count==1,"current dialogue exposes only its enabled quest start");
  pending.Status=Quest.QuestStatus.Active;Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count)==null,"NPC marker disappears once quest starts");
  npc.DialogueIndex=1;scanner.Scan(256);Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count).Label.Contains("Locked")&&count==1,"new dialogue can expose a different quest");
  npc.Zone.gameObject.activeInHierarchy=false;Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count)==null,"disabled dialogue zone is not a quest starter");
  scanner.Scan(0);Check(scanner.Targets.Count==0,"disabled categories cleared");
  Console.WriteLine("Tinykin tracker filtering, nearest selection, quest resolution and collection transitions passed.");
 }
}
