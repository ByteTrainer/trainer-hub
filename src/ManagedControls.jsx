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
 return <div className="managed-multiplier">
  <label className="sr-only" htmlFor={id}>{command.name}, multiplicador</label>
  <input id={id} type="range" min={command.min} max={command.max} step={command.step} value={Number(draft)||command.min} disabled={!!busy} onChange={event=>setDraft(event.target.value)} onPointerUp={commit} onKeyUp={commit} onBlur={commit}/>
  <label className="sr-only" htmlFor={id+'-number'}>Valor de {command.name}</label>
  <input id={id+'-number'} type="number" min={command.min} max={command.max} step={command.step} value={draft} disabled={!!busy} onChange={event=>setDraft(event.target.value)} onBlur={event=>{if(event.target.validity.valid)commit();else setDraft(String(value??command.default));}} onKeyDown={event=>{if(event.key==='Enter')event.currentTarget.blur();}}/>
  <span>×</span>
 </div>;
}

function Resources({command,gameId,status,api,action,busy}){
 const [resource,setResource]=useState(String(status.resourceDraft?.resource||command.choices[0].id));
 const [amount,setAmount]=useState(String(status.resourceDraft?.amount||100));
 const [message,setMessage]=useState('');
 useEffect(()=>{
  setResource(String(status.resourceDraft?.resource||command.choices[0].id));
  setAmount(String(status.resourceDraft?.amount||100));
 },[status.resourceDraft?.resource,status.resourceDraft?.amount]);
 const id=`resource-${gameId}`;
 const saveDraft=(selected,quantity)=>{
  const value=Number(quantity);
  if(Number.isSafeInteger(value)&&value!==0&&Math.abs(value)<=9999999)
   api.setManagedResourceDraft(gameId,command.id,Number(selected),value).catch(error=>setMessage(error.message));
 };
 return <form className="managed-resource" onSubmit={event=>{
  event.preventDefault();
  action('resource',async()=>{
   const applied=await api.applyManagedResource(gameId,command.id,Number(resource),Number(amount));
   setMessage(applied>0?`${applied} unidades adicionadas.`:applied<0?`${-applied} unidades removidas.`:'O estoque já estava no limite.');
  });
 }}>
  <h3>{command.name}</h3><p>{command.description}</p>
  <div className="managed-resource-fields">
   <label htmlFor={id}>Recurso<select id={id} value={resource} disabled={!!busy} onChange={event=>{setResource(event.target.value);setMessage('');saveDraft(event.target.value,amount);}}>{command.choices.map(choice=><option value={choice.id} key={choice.id}>{choice.name}</option>)}</select></label>
   <label htmlFor={id+'-amount'}>Quantidade a adicionar ou remover<input id={id+'-amount'} type="number" required min="-9999999" max="9999999" step="1" value={amount} disabled={!!busy} onChange={event=>setAmount(event.target.value)} onBlur={()=>saveDraft(resource,amount)}/></label>
   <button className="secondary" disabled={!status.owned||!status.ready||!!busy||Number(amount)===0}>{busy==='resource'?'Aplicando…':'Aplicar'}</button>
  </div>
  <p role="status" className="managed-resource-message">{message}</p>
 </form>;
}

export default function ManagedControls({gameId,status,bindings={},api,action,busy}){
 return <div className="managed-controls">{status.commands.map(command=>command.type==='resource'?
  <Resources key={command.id} command={command} gameId={gameId} status={status} api={api} action={action} busy={busy}/>:
  <div key={command.id} className="managed-cheat">
   <div className="cheat-row">
    <div><label htmlFor={`cheat-${gameId}-${command.index}`}>{command.name}</label><p>{status.capabilities?.[command.id]?.reason||command.description}</p></div>
    <kbd>{keyLabel(bindings[command.id])}</kbd>
    <input id={`cheat-${gameId}-${command.index}`} className="switch" type="checkbox" role="switch" checked={!!status.options[command.index]} disabled={!status.owned||!!busy||status.capabilities?.[command.id]?.available===false} onChange={event=>action('cheat',()=>api.cheat(command.index,event.target.checked,gameId))}/>
   </div>
   {command.type==='multiplier'&&<Multiplier command={command} value={status.parameters?.[command.id]} gameId={gameId} api={api} action={action} busy={busy}/>}
  </div>
 )}</div>;
}
