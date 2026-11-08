import {t, getLocale} from './i18n.js';
import React,{useState,useEffect} from 'react';

const keyLabel=key=>key?key.replace('Control','Ctrl'):'Sem atalho';

function Multiplier({command,value,gameId,api,action,busy}){
 const [draft,setDraft]=useState(String(value??command.default));
 useEffect(()=>setDraft(String(value??command.default)),[value,command.default]);
 const commit=()=>{
  const number=Number(draft);
  if(Number.isFinite(number)&&number>=command.min&&number<=command.max&&number!==value)
   action('parameter',()=>api.setManagedParameter(gameId,command.id,number));
 };
 const id=`multiplier-${gameId}-${command.id}`;
 return (
  <div className="managed-multiplier">
   <label className="sr-only" htmlFor={id}>{t(command.name)}{t(', multiplicador')}</label>
   <input id={id} type="range" min={command.min} max={command.max} step={command.step} value={Number(draft)||command.min} disabled={!!busy} onChange={event=>setDraft(event.target.value)} onPointerUp={commit} onKeyUp={commit} onBlur={commit}/>
   <label className="sr-only" htmlFor={id+'-number'}>{t('Valor de ')}{t(command.name)}</label>
   <input id={id+'-number'} type="number" min={command.min} max={command.max} step={command.step} value={draft} disabled={!!busy} onChange={event=>setDraft(event.target.value)} onBlur={event=>{if(event.target.validity.valid)commit();else setDraft(String(value??command.default));}} onKeyDown={event=>{if(event.key==='Enter')event.currentTarget.blur();}}/>
   <span>{t('×')}</span>
  </div>
 );
}

function Resources({command,gameId,status,api,action,busy}){
 const saved=status.resourceDrafts?.[command.id]||status.resourceDraft;
 const setting=command.operation==='set';
 const validAmount=value=>Number.isSafeInteger(value)&&(setting?value>=0&&value<=999999999999:value!==0&&Math.abs(value)<=9999999);
 const initialResource=command.choices.some(choice=>choice.id===saved?.resource)?saved.resource:command.choices[0].id;
 const [resource,setResource]=useState(String(initialResource));
 const [amount,setAmount]=useState(String(saved?.amount??100));
 const [message,setMessage]=useState('');
 useEffect(()=>{
  setResource(String(initialResource));
  setAmount(String(saved?.amount??100));
 },[initialResource,saved?.amount]);
 const choices=[...command.choices].sort((a,b)=>t(a.name).localeCompare(t(b.name),getLocale()));
 const id=`resource-${gameId}-${command.id}`;
 const saveDraft=(selected,quantity)=>{
  const value=Number(quantity);
  if(quantity.trim()!==''&&validAmount(value))
   api.setManagedResourceDraft(gameId,command.id,Number(selected),value).catch(error=>setMessage(error.message));
 };
 return (
  <form className="managed-resource" onSubmit={event=>{
   event.preventDefault();
   action('resource',async()=>{
    const applied=await api.applyManagedResource(gameId,command.id,Number(resource),Number(amount));
    setMessage(setting?`Quantidade definida para ${applied}.`:applied>0?`${applied} unidades adicionadas.`:applied<0?`${-applied} unidades removidas.`:'O estoque já estava no limite.');
   });
  }}>
   <h3>{t(command.name)}</h3><p>{t(command.description)}</p>
   <div className="managed-resource-fields">
    {command.choices.length>1&&<label htmlFor={id}>{t('Recurso')}<select id={id} value={resource} disabled={!!busy} onChange={event=>{setResource(event.target.value);setMessage('');saveDraft(event.target.value,amount);}}>{choices.map(choice=><option value={choice.id} key={choice.id}>{t(choice.name)}</option>)}</select></label>}
    <label htmlFor={id+'-amount'}>{t(
     setting?`Quantidade de ${command.choices[0].name.toLocaleLowerCase(getLocale())}`:'Quantidade a adicionar ou remover'
    )}<input id={id+'-amount'} type="number" required min={setting?0:-9999999} max={setting?999999999999:9999999} step="1" value={amount} disabled={!!busy} onChange={event=>setAmount(event.target.value)} onBlur={()=>saveDraft(resource,amount)}/></label>
    <button className="secondary" disabled={!status.owned||!status.ready||!!busy||amount.trim()===''||!validAmount(Number(amount))}>{t(busy==='resource'?'Aplicando…':'Aplicar')}</button>
   </div>
   <p role="status" className="managed-resource-message">{t(message)}</p>
  </form>
 );
}

export default function ManagedControls({gameId,status,bindings={},api,action,busy}){
 return (
  <div className="managed-controls">{status.commands.map(command=>command.type==='resource'?
    <Resources key={command.id} command={command} gameId={gameId} status={status} api={api} action={action} busy={busy}/>:
    <div key={command.id} className="managed-cheat">
     <div className="cheat-row">
      <div><label htmlFor={`cheat-${gameId}-${command.index}`}>{t(command.name)}</label><p>{t(status.capabilities?.[command.id]?.reason||command.description)}</p></div>
      <kbd>{t(keyLabel(bindings[command.id]))}</kbd>
      <input id={`cheat-${gameId}-${command.index}`} className="switch" type="checkbox" role="switch" checked={!!status.options[command.index]} disabled={!status.owned||!!busy||status.capabilities?.[command.id]?.available===false} onChange={event=>action('cheat',()=>api.cheat(command.index,event.target.checked,gameId))}/>
     </div>
     {command.type==='multiplier'&&<Multiplier command={command} value={status.parameters?.[command.id]} gameId={gameId} api={api} action={action} busy={busy}/>}
    </div>
   )}</div>
 );
}
