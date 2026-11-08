import {t, getLocale, gameTranslator} from './i18n.js';
import React,{useState,useEffect,useRef} from 'react';
import commands from '../electron/commands.json';
import pedro from '../electron/pedro-options.json';
import sol from '../electron/solcesto-options.json';
import ManagedControls from './ManagedControls.jsx';
export const keyLabel=key=>key?key.replace('Control','Ctrl').replace(/num(\d)/,'Num $1'):'No shortcut';
export function useInputFocus(api){useEffect(()=>{const update=()=>api.inputFocus(!!document.activeElement?.matches('input:not([type=checkbox]):not([type=radio]):not([type=range]),textarea,select,[contenteditable=true]')).catch(()=>{});document.addEventListener('focusin',update);document.addEventListener('focusout',update);window.addEventListener('focus',update);return()=>{document.removeEventListener('focusin',update);document.removeEventListener('focusout',update);window.removeEventListener('focus',update);api.inputFocus(false).catch(()=>{});};},[api]);}
const moventure={options:[
 {name:"Invincibility",description:"Blocks damage, knockback and falls caused by attacks. Falling off the map restarts the attempt."},
 {name:"Infinite jumps",description:"Press jump again in midair for another jump."}
]};
const pumpkin={options:[
 {name:'Unlimited health',description:'Blocks normal damage to Jack. Fatal falls can still restart the level.'},
 {name:'Unlimited jumps',description:'Repeats Jack\'s aerial jump when jump is pressed again.'},
 {name:'Movement speed',description:'Applies the multiplier below to ground movement.'},
 {name:'One-hit kills',description:'Amplifies direct attacks against supported common enemies, including bats.'},
 {name:'Instant crow cooldown',description:'Readies the ability as soon as the crow can be called again.'},
 {name:'Collectible tracker',description:'Shows the direction and distance to the nearest skull and vinyl in windowed or borderless mode.'}
]};
const fields=[['strength','Set strength',0,999,'Applies once. From 0 to 999.'],['magic','Set magic',0,999,'Applies once. From 0 to 999.'],['inventory','Expand inventory',1,6,'Unlocks up to 6 slots and preserves your items and current capacity.'],['gold','Set gold',0,999999,'Applies once. From 0 to 999999.']];
function PumpkinSpeed({multiplier,active,api,busy}){
 const slider=useRef(null);
 const saving=useRef(false), queued=useRef(null), saved=useRef(multiplier);
 const [value,setValue]=useState(multiplier),[error,setError]=useState('');
 useEffect(()=>{if(!saving.current){saved.current=multiplier;setValue(multiplier);}},[multiplier]);
 useEffect(()=>{
  const input=slider.current;
  const commit=async()=>{
   queued.current=Number(input.value);
   if(saving.current)return;
   saving.current=true;
   setError('');
   try{
    while(queued.current!==null){
     const selected=queued.current;
     queued.current=null;
     saved.current=await api.setPumpkinSpeed(selected);
    }
   }catch{
    queued.current=null;
    setValue(saved.current);
    setError('Could not save the speed. Try again.');
   }finally{saving.current=false;}
  };
  input.addEventListener('change',commit);
  return ()=>input.removeEventListener('change',commit);
 },[api]);
 return (
  <div className="pumpkin-speed">
   <div className="pumpkin-speed-heading"><label htmlFor="pumpkin-speed">{t('Movement multiplier')}</label><output htmlFor="pumpkin-speed">{t(value)}{t('%')}</output></div>
   <input ref={slider} id="pumpkin-speed" type="range" min="50" max="250" step="25"
    aria-describedby="pumpkin-speed-help" aria-valuetext={t(`${value}%`)} value={value}
    disabled={!!busy} onChange={event=>setValue(Number(event.target.value))}/>
   <div className="pumpkin-speed-scale" aria-hidden="true"><span>{t('50%')}</span><span>{t('250%')}</span></div>
   <small id="pumpkin-speed-help">{t(
    active?'This setting applies to ground movement.':'Enable Movement speed to apply this setting.'
   )}</small>
   {error&&<small role="alert">{t(error)}</small>}
  </div>
 );
}
export default function TrainerControls({gameId,status,bindings={},parameters,api,action,busy,notify=()=>{}}){
 const t=gameTranslator(gameId);
 const [combo,setCombo]=useState(String(status.combo||10)),[draft,setDraft]=useState(parameters||{strength:10,magic:10,inventory:6,gold:100});
 useEffect(()=>{setCombo(String(status.combo||10));},[status.combo]);
 useEffect(()=>{if(parameters)setDraft(parameters);},[parameters?.strength,parameters?.magic,parameters?.inventory,parameters?.gold]);
 const gameCommands=status.commands||commands[gameId];
 if(status.commands?.some(command=>command.type!=='toggle'))return <ManagedControls gameId={gameId} status={status} bindings={bindings} api={api} action={action} busy={busy}/>;
 const manifest=status.commands&&![557340,2738490,1186640].includes(gameId)?{options:status.commands}:gameId===557340?pedro:gameId===1186640?pumpkin:gameId===3826060?moventure:sol;
 return (
  <><div className="cheat-list">{manifest.options.map((option,i)=>{const command=option.index!==undefined?option:gameCommands.find(c=>c.type==='toggle'&&c.index===i),index=command.index,id=`cheat-${gameId}-${i}`;return (
    <div className="cheat-row" key={id}><div><label htmlFor={id}>{t(option.name)}</label><p id={`${id}-help`}>{t(
     status.compatibility?.canConnect&&status.capabilities?.[command.id]?.available===false?status.capabilities[command.id].reason:option.description
    )}</p></div><kbd>{t(keyLabel(bindings[command.id]))}</kbd><input id={id} aria-describedby={`${id}-help`} className="switch" type="checkbox" role="switch" checked={!!status.options[index]} disabled={!status.owned||!!busy||status.capabilities?.[command.id]?.available===false} onChange={e=>action('cheat',()=>api.cheat(index,e.target.checked,gameId))}/></div>
   );})}</div>
   {gameId===557340?<form className="combo-control" onSubmit={e=>{e.preventDefault();action('combo',()=>api.combo(Number(combo)));}}><label htmlFor="combo">{t('Combo multiplier')}<small id="combo-help">{t('From 1 to 9999. Enable Lock combo to keep it fixed.')}</small></label><input id="combo" aria-describedby="combo-help" required type="number" min="1" max="9999" step="1" value={combo} onChange={e=>setCombo(e.target.value)}/><button className="secondary" disabled={!status.owned||!!busy}>{t('Apply')}</button></form>:gameId===1186640?<PumpkinSpeed multiplier={status.multiplier} active={!!status.options[2]} api={api} busy={busy}/>:gameId===2738490?<div className="sol-actions">{fields.map(([kind,name,min,max,help])=><form className="combo-control" key={kind} onSubmit={e=>{e.preventDefault();action(kind,async()=>{const actual=await api.solAction(kind,Number(draft[kind]));notify(`${name}: ${actual}.`);});}}><label htmlFor={`sol-${kind}`}>{t(name)}<small id={`sol-${kind}-help`}>{t(
    status.compatibility?.canConnect&&status.capabilities?.[kind]?.available===false?status.capabilities[kind].reason:help
   )}</small></label><kbd>{t(keyLabel(bindings[kind]))}</kbd><input id={`sol-${kind}`} aria-describedby={`sol-${kind}-help`} type="number" min={min} max={max} required step="1" value={draft[kind]} onChange={e=>setDraft(old=>({...old,[kind]:e.target.value}))} onBlur={e=>{if(e.target.validity.valid)api.setSolValues({[kind]:Number(e.target.value)}).catch(error=>notify(error.message));}}/><button className="secondary" aria-label={t(`Apply ${name.toLocaleLowerCase(getLocale())}`)} disabled={!status.owned||!status.ready||!!busy||status.capabilities?.[kind]?.available===false}>{t(busy===kind?'Applying…':'Apply')}</button></form>)}<p className="section-copy">{t(
    'Shortcuts apply the values saved above to your current game. Unlimited items only preserves items you already own.'
   )}</p></div>:null}
  </>
 );
}
