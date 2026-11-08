using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace ByteTrainer.Tinykin {
    // Discovery is read-only and runs exclusively on Unity's main thread.
    internal sealed class TrackerTarget {
        public int Category;
        public Component Anchor;
        public string Label;
        public Func<bool> Available;
        public Func<Vector3> Position;
        public bool Valid() { return TrackerScanner.Live(Anchor)&&Available(); }
    }
    internal sealed class TrackerScanner {
        internal readonly List<TrackerTarget> Targets=new List<TrackerTarget>();
        internal readonly List<string> QuestDiagnostics=new List<string>();
        private readonly Dictionary<Trigger,List<Trigger>> inputs=new Dictionary<Trigger,List<Trigger>>();
        private readonly Dictionary<Trigger,MultiTrigger> aggregates=new Dictionary<Trigger,MultiTrigger>();
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly FieldInfo First=typeof(CollectibleContainer).GetField("firstIndex",Private);
        private static readonly FieldInfo Output=typeof(MultiTrigger).GetField("trigger",Private);
        private static readonly FieldInfo Validations=typeof(MultiTrigger).GetField("validations",Private);
        internal static bool Live(Component c) {
            return c!=null&&c.gameObject.scene.IsValid()&&c.gameObject.scene.isLoaded&&c.gameObject.activeInHierarchy;
        }
        private void Add(int category,Component anchor,string label,Func<bool> available,Func<Vector3> position) {
            Targets.Add(new TrackerTarget {Category=category,Anchor=anchor,Label=label,Available=available,Position=position});
        }
        internal void Scan(int mask) {
            Targets.Clear();QuestDiagnostics.Clear();inputs.Clear();aggregates.Clear();
            if((mask&64)!=0) {
                foreach(var item in UnityEngine.Object.FindObjectsOfType<Collectible>()) {
                    var pollen=item;
                    Add(0,pollen,TrackerText.Get("Pollen"),delegate {
                        return !pollen.IsCollected&&(pollen.parentScene==null||pollen.parentScene.pollen==null||pollen.ID<0||pollen.ID>=pollen.parentScene.pollen.Length||!pollen.parentScene.pollen[pollen.ID]);
                    },delegate {return pollen.transform.position;});
                }
                foreach(var item in UnityEngine.Object.FindObjectsOfType<CollectibleContainer>()) {
                    var container=item;
                    Add(0,container,TrackerText.Get("Pollen container"),delegate {return First!=null&&container.pollenCount>(int)First.GetValue(container);},delegate {return container.transform.position;});
                }
            }
            if((mask&128)!=0)foreach(var item in UnityEngine.Object.FindObjectsOfType<SbirzEgg>()) {
                var egg=item;
                Add(1,egg,TrackerText.Get("Tinykin")+" · "+TrackerText.Get(egg.sbirzType.ToString()+" tinykin"),delegate {return !egg.IsCollected;},delegate {return egg.eggTransform!=null?egg.eggTransform.position:egg.transform.position;});
            }
            if((mask&512)!=0)foreach(var item in UnityEngine.Object.FindObjectsOfType<UniqueCollectible>()) {
                var rare=item;
                string name=TrackerText.Get("Collectible");
                if(rare.collectibleItem!=null&&rare.collectibleItem.ItemData!=null) {
                    string translated=rare.collectibleItem.ItemData.displayName;
                    if(!string.IsNullOrEmpty(translated))name=translated;
                }
                Add(3,rare,name,delegate {return !rare.Collected&&rare.trigger!=null&&rare.trigger.enabled&&(rare.rendererTransform==null||rare.rendererTransform.gameObject.activeInHierarchy)&&(rare.collectibleItem==null||(!rare.collectibleItem.Collected&&(rare.collectibleItem.ItemData==null||!rare.collectibleItem.ItemData.ignoreCollected)));},delegate {return rare.trigger.bounds.center;});
            }
            if((mask&256)!=0)ScanQuests();
        }
        private void Link(Trigger destination,Trigger source) {
            if(destination==null||source==null||destination==source)return;
            List<Trigger> list;if(!inputs.TryGetValue(destination,out list))inputs[destination]=list=new List<Trigger>();
            if(!list.Contains(source))list.Add(source);
        }
        private void LinkEvent(UnityEvent<string,Trigger> action,Trigger source) {
            if(action==null)return;
            for(int i=0;i<action.GetPersistentEventCount();i++)
                if(action.GetPersistentMethodName(i)=="Execute")Link(action.GetPersistentTarget(i) as Trigger,source);
        }
        private void ScanQuests() {
            foreach(var multi in UnityEngine.Object.FindObjectsOfType<MultiTrigger>()) {
                var output=Output==null?null:Output.GetValue(multi) as Trigger;
                if(output!=null)aggregates[output]=multi;
            }
            foreach(var source in UnityEngine.Object.FindObjectsOfType<Trigger>()) {
                LinkEvent(source.OnExecute,source);
                if(source.filteredOnExecutes!=null)foreach(var filtered in source.filteredOnExecutes)
                    if(filtered!=null)LinkEvent(filtered.OnExecute,source);
            }
            foreach(var quest in UnityEngine.Object.FindObjectsOfType<Quest>()) {
                if(quest.Status!=Quest.QuestStatus.Active||quest.objectives==null)continue;
                string label=quest.displayName;if(string.IsNullOrEmpty(label))label=quest.questName;
                foreach(var objective in quest.objectives) {
                    if(objective==null||objective.Progress>=objective.Success||objective.triggers==null)continue;
                    foreach(var trigger in objective.triggers) {
                        var found=new List<InteractionTrigger>();
                        Resolve(trigger,new HashSet<Trigger>(),found,0);
                        QuestDiagnostics.Add(label+" | "+objective.Progress+"/"+objective.Success+" | "+Describe(trigger)+" | anchors="+found.Count);
                        foreach(var match in found) {
                            var anchor=match;var owner=quest;var step=objective;
                            Add(2,anchor,label,delegate {return owner!=null&&owner.Status==Quest.QuestStatus.Active&&step.Progress<step.Success&&!anchor.IsCompleted&&anchor.IsSelectable;},delegate {return anchor.InteractionBounds.center;});
                        }
                    }
                }
            }
        }
        private static string Describe(Trigger trigger) { return trigger==null?"null":trigger.GetType().Name+":"+trigger.name+"@"+trigger.transform.position; }
        private void Resolve(Trigger trigger,HashSet<Trigger> visited,List<InteractionTrigger> result,int depth) {
            if(trigger==null||depth>16||!visited.Add(trigger)||trigger.IsCompleted)return;
            QuestDiagnostics.Add(new string(' ',depth)+Describe(trigger));
            var nested=trigger as Quest;
            if(nested!=null&&nested.Status==Quest.QuestStatus.Active&&nested.objectives!=null) {
                foreach(var objective in nested.objectives)if(objective!=null&&objective.Progress<objective.Success&&objective.triggers!=null)
                    foreach(var child in objective.triggers)Resolve(child,visited,result,depth+1);
                return;
            }
            MultiTrigger multi;
            if(aggregates.TryGetValue(trigger,out multi)&&multi.triggers!=null) {
                var validated=Validations==null?null:Validations.GetValue(multi) as Dictionary<Trigger,bool>;
                foreach(var child in multi.triggers) {
                    bool done;if(child!=null&&(validated==null||!validated.TryGetValue(child,out done)||!done))Resolve(child,visited,result,depth+1);
                }
                return;
            }
            var physical=trigger as InteractionTrigger;
            if(physical==null)physical=trigger.GetComponent<InteractionTrigger>();
            if(physical!=null&&Live(physical)&&!result.Contains(physical))result.Add(physical);
            List<Trigger> sources;
            if(inputs.TryGetValue(trigger,out sources))foreach(var source in sources)Resolve(source,visited,result,depth+1);
        }
        internal TrackerTarget Nearest(int category,Vector3 origin,out int count) {
            TrackerTarget nearest=null;float best=float.PositiveInfinity;count=0;
            foreach(var target in Targets) {
                if(target.Category!=category||!target.Valid())continue;
                count++;float distance=(target.Position()-origin).sqrMagnitude;
                if(distance<best){best=distance;nearest=target;}
            }
            return nearest;
        }
    }
}
