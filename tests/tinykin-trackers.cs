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
 public class MonoBehaviour:Component {}
 public class Transform {public Vector3 position;public GameObject gameObject=new GameObject();}
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public float sqrMagnitude{get{return x*x+y*y+z*z;}}
  public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
 }
 public struct Bounds {public Vector3 center;}
 public class Collider {public bool enabled=true;public Bounds bounds;}
}
namespace UnityEngine.Events {
 public class UnityEvent<A,B> {
  public List<UnityEngine.Object> targets=new List<UnityEngine.Object>();
  public int GetPersistentEventCount(){return targets.Count;}
  public string GetPersistentMethodName(int index){return "Execute";}
  public UnityEngine.Object GetPersistentTarget(int index){return targets[index];}
 }
}
public class SubSceneData {public bool[] pollen;}
public class Collectible:UnityEngine.MonoBehaviour {public bool IsCollected;public SubSceneData parentScene;public int ID;}
public class CollectibleContainer:UnityEngine.MonoBehaviour {public int pollenCount;private int firstIndex;public void Fill(int count){firstIndex=count;}}
public enum SbirzType {Carrier,Explosive,Ladder,Bridge,Electric}
public class SbirzEgg:UnityEngine.MonoBehaviour {public bool IsCollected;public SbirzType sbirzType;public UnityEngine.Transform eggTransform;}
public class CollectibleItemData {public string displayName;public bool ignoreCollected;}
public class CollectibleItem {public CollectibleItemData ItemData;public bool Collected;}
public class UniqueCollectible:UnityEngine.MonoBehaviour {public bool Collected;public UnityEngine.Collider trigger;public UnityEngine.Transform rendererTransform;public CollectibleItem collectibleItem;}
public class FilteredOnExecute {public UnityEngine.Events.UnityEvent<string,Trigger> OnExecute;}
public class Trigger:UnityEngine.MonoBehaviour {public bool IsCompleted;public UnityEngine.Events.UnityEvent<string,Trigger> OnExecute;public FilteredOnExecute[] filteredOnExecutes;}
public class InteractionTrigger:Trigger {public bool IsSelectable=true;public UnityEngine.Bounds InteractionBounds;}
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
  var logic=new Trigger();var source=new InteractionTrigger{OnExecute=new UnityEngine.Events.UnityEvent<string,Trigger>()};source.OnExecute.targets.Add(logic);
  var side=new Quest {Status=Quest.QuestStatus.Active,displayName="Side",objectives=new[]{new QuestObjective{Success=1,triggers=new[]{logic}}}};
  scanner.Scan(256);Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count).Anchor==source&&count==1,"persistent event resolves logical trigger to physical interaction");
  source.gameObject.activeInHierarchy=false;Check(scanner.Nearest(2,new UnityEngine.Vector3(),out count)==null,"scene departure drops old target");
  var rare=new UniqueCollectible{trigger=new UnityEngine.Collider(),collectibleItem=new CollectibleItem{ItemData=new CollectibleItemData{displayName="Letter"}}};
  scanner.Scan(512);Check(scanner.Nearest(3,new UnityEngine.Vector3(),out count).Anchor==rare,"visible special item tracked");
  rare.collectibleItem.Collected=true;Check(scanner.Nearest(3,new UnityEngine.Vector3(),out count)==null,"saved special item excluded");
  scanner.Scan(0);Check(scanner.Targets.Count==0,"disabled categories cleared");
  Console.WriteLine("Tinykin tracker filtering, nearest selection, quest resolution and collection transitions passed.");
 }
}
