(function(root){'use strict';
 function createEngine(runtime,now=Date.now){
  let settings={options:[false,false,false,false],expires:0},goldAck=null,actionAck=null;
  const heroType=runtime.GetObjectClassByName('heros');if(!heroType)throw Error('Personagem incompatível com esta integração.');
  const schema={strength:['pouvoir_force',5,1],magic:['pouvoir_magie',6,1],health:['vie',8,1],maxHealth:['vieMax',9,1],gold:['or',10,1],charge:['capacite_seuilActuel',13,1],maxCharge:['capacite_seuilMax',14,1],inventory:['inventaireMax',17,1],poison:['poison',25,1]};for(let i=1;i<=6;i++)schema['item'+i]=['inventaire'+i,17+i,2];
  const indices={},semantic=typeof heroType.GetInstanceVariableIndexByName==='function';for(const[key,[name,fallback,type]]of Object.entries(schema)){let index=-1;try{index=semantic?heroType.GetInstanceVariableIndexByName(name):fallback;if(semantic&&typeof heroType.GetInstanceVariableType==='function'&&heroType.GetInstanceVariableType(index)!==type)index=-1;}catch{}indices[key]=index;}
  const deps={health:['health','maxHealth'],ability:['charge','maxCharge'],items:Array.from({length:6},(_,i)=>'item'+(i+1)),poison:['poison'],strength:['strength'],magic:['magic'],inventory:['inventory'],gold:['gold']},capabilities={};
  for(const[id,fields]of Object.entries(deps)){const available=fields.every(key=>indices[key]>=0);capabilities[id]={available,reason:available?null:'Esta opção não pôde ser validada na estrutura atual do jogo.'};}
  const disable=(id,error)=>{capabilities[id]={available:false,reason:'Esta opção parou de responder nesta versão. As demais opções permanecem disponíveis.'};};
  for(const[id,name]of [['items','inventaire_spawnObjet'],['inventory','inventaire_update']])try{const manager=runtime.GetEventSheetManager?.();if(!runtime.GetIRuntime?.().callFunction||manager&&!manager.GetFunctionBlockByName(name))capabilities[id]={available:false,reason:'A rotina necessária para esta opção não foi encontrada nesta versão.'};}catch{disable(id);}
  const original=new Map(),refills=new Map(),toggleIds=['health','ability','items','poison'];
  const enabled=i=>now()<settings.expires&&settings.options[i]===true&&capabilities[toggleIds[i]].available;
  const get=(hero,key)=>indices[key]>=0?hero.GetInstanceVariableValue(indices[key]):undefined;
  const heroes=()=>heroType.GetInstances(),alive=()=>heroes().find(h=>Number(get(h,'health'))>0);
  function attempt(id,fn){if(!capabilities[id].available)return;try{fn();}catch(e){disable(id,e);}}
  function hook(hero){if(original.has(hero))return;if(!semantic&&hero.GetInstanceVariableCount()!==35)throw Error('Estrutura do personagem incompatível.');const set=hero.SetInstanceVariableValue;original.set(hero,set);
   hero.SetInstanceVariableValue=function(index,value){
    if(index===indices.health&&enabled(0)&&get(this,'health')>0&&Number(value)<get(this,'health'))value=Math.max(get(this,'health'),get(this,'maxHealth'));
    if(index===indices.charge&&enabled(1)&&get(this,'maxCharge')>0)value=Math.max(Number(value),get(this,'maxCharge'));
    if(index===indices.poison&&enabled(3))value=0;
    const slot=deps.items.findIndex(key=>indices[key]===index);if(slot>=0&&enabled(2)&&value===''&&this.GetInstanceVariableValue(index)!==''){let slots=refills.get(this);if(!slots){slots=new Map();refills.set(this,slots);}slots.set(index,{value:this.GetInstanceVariableValue(index),slot:slot+1,due:now()+600});return;}
    return set.call(this,index,value);
   };
  }
  return {
   configure(value){if(!value||!Array.isArray(value.options)||![3,4].includes(value.options.length)||!value.options.every(v=>typeof v==='boolean')||!Number.isFinite(value.expires))throw Error('Comando inválido.');settings=value;},
   tick(){const current=heroes();for(const[h,set]of original)if(!current.includes(h)){h.SetInstanceVariableValue=set;original.delete(h);refills.delete(h);}
    for(const hero of current){hook(hero);const set=original.get(hero);if(enabled(0))attempt('health',()=>{if(get(hero,'health')>0)set.call(hero,indices.health,get(hero,'maxHealth'));});if(enabled(1))attempt('ability',()=>{if(get(hero,'maxCharge')>0)set.call(hero,indices.charge,get(hero,'maxCharge'));});if(enabled(3))attempt('poison',()=>set.call(hero,indices.poison,0));}
    const action=settings.action;if(now()<settings.expires&&action&&actionAck?.id!==action.id){const hero=alive(),max=action.kind==='gold'?999999:action.kind==='inventory'?6:999,min=action.kind==='inventory'?1:0;if(hero&&['strength','magic','inventory','gold'].includes(action.kind)&&typeof action.id==='string'&&Number.isInteger(action.value)&&action.value>=min&&action.value<=max)attempt(action.kind,()=>{const value=action.kind==='inventory'?Math.max(action.value,get(hero,'inventory')):action.value;original.get(hero).call(hero,indices[action.kind],value);if(action.kind==='inventory')runtime.GetIRuntime().callFunction('inventaire_update');actionAck={id:action.id,kind:action.kind,value:get(hero,action.kind)};if(action.kind==='gold')goldAck={id:action.id,value:actionAck.value};});}
    for(const[hero,slots]of refills){for(const[index,refill]of slots){if(now()<refill.due)continue;slots.delete(index);if(current.includes(hero)&&hero.GetInstanceVariableValue(index)===refill.value)attempt('items',()=>runtime.GetIRuntime().callFunction('inventaire_spawnObjet',refill.slot,refill.value));}if(!slots.size)refills.delete(hero);}
    const command=settings.gold;if(now()<settings.expires&&command&&goldAck?.id!==command.id){const hero=alive();if(hero&&Number.isInteger(command.value)&&command.value>=0&&command.value<=999999&&typeof command.id==='string')attempt('gold',()=>{original.get(hero).call(hero,indices.gold,command.value);goldAck={id:command.id,value:get(hero,'gold')};});}
   },
   status(){const h=alive();return {ready:!!h,capabilities,resolution:semantic?'names':'verified-legacy-layout',values:h?{...Object.fromEntries(['health','maxHealth','gold','charge','maxCharge','strength','magic','inventory','poison'].map(key=>[key,get(h,key)??null])),items:deps.items.map(key=>get(h,key)??null)}:null,goldAck,actionAck};},
   close(){settings={options:[false,false,false,false],expires:0};for(const[h,set]of original)h.SetInstanceVariableValue=set;original.clear();refills.clear();}
  };
 }
 if(typeof module==='object'&&module.exports)module.exports={createEngine};else root.SolCestoTrainerEngine={createEngine};
})(globalThis);
