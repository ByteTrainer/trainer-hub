import React,{useState,useEffect,useRef} from 'react';
import commands from '../electron/commands.json';
import pedro from '../electron/pedro-options.json';
import sol from '../electron/solcesto-options.json';
import ManagedControls from './ManagedControls.jsx';
export const keyLabel=key=>key?key.replace('Control','Ctrl').replace(/num(\d)/,'Num $1'):'Sem atalho';
export function useInputFocus(api){useEffect(()=>{const update=()=>api.inputFocus(!!document.activeElement?.matches('input:not([type=checkbox]):not([type=radio]):not([type=range]),textarea,select,[contenteditable=true]')).catch(()=>{});document.addEventListener('focusin',update);document.addEventListener('focusout',update);window.addEventListener('focus',update);return()=>{document.removeEventListener('focusin',update);document.removeEventListener('focusout',update);window.removeEventListener('focus',update);api.inputFocus(false).catch(()=>{});};},[api]);}
const moventure={options:[
 {name:"Invencibilidade",description:"Bloqueia dano, empurrões e quedas causadas por ataques. Cair fora do mapa reinicia a tentativa."},
 {name:"Pulo infinito",description:"Pressione o botão de pulo novamente no ar para dar outro impulso."}
]};
const pumpkin={options:[
 {name:'Vida ilimitada',description:'Bloqueia o dano normal sofrido por Jack. Quedas fatais ainda podem reiniciar a fase.'},
 {name:'Pulo ilimitado',description:'Repete o pulo aéreo de Jack ao pressionar o comando de pulo novamente.'},
 {name:'Velocidade de movimento',description:'Aplica o multiplicador abaixo ao movimento no solo.'},
 {name:'Mortes com um golpe',description:'Amplifica ataques diretos contra inimigos comuns compatíveis, incluindo morcegos.'},
 {name:'Recarga instantânea do corvo',description:'Deixa a habilidade pronta novamente assim que o corvo pode ser chamado.'},
 {name:'Rastreador de colecionáveis',description:'Mostra a direção e distância da caveira e do vinil mais próximos em modo janela ou sem bordas.'}
]};
const fields=[['strength','Definir força',0,999,'Aplica uma vez. De 0 a 999.'],['magic','Definir magia',0,999,'Aplica uma vez. De 0 a 999.'],['inventory','Ampliar inventário',1,6,'Libera até 6 espaços; preserva os itens e o tamanho atual.'],['gold','Definir ouro',0,999999,'Aplica uma vez. De 0 a 999999.']];
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
    setError('Não foi possível salvar a velocidade. Tente novamente.');
   }finally{saving.current=false;}
  };
  input.addEventListener('change',commit);
  return ()=>input.removeEventListener('change',commit);
 },[api]);
 return <div className="pumpkin-speed">
  <div className="pumpkin-speed-heading"><label htmlFor="pumpkin-speed">Multiplicador de movimento</label><output htmlFor="pumpkin-speed">{value}%</output></div>
  <input ref={slider} id="pumpkin-speed" type="range" min="50" max="250" step="25"
   aria-describedby="pumpkin-speed-help" aria-valuetext={`${value}%`} value={value}
   disabled={!!busy} onChange={event=>setValue(Number(event.target.value))}/>
  <div className="pumpkin-speed-scale" aria-hidden="true"><span>50%</span><span>250%</span></div>
  <small id="pumpkin-speed-help">{active?'O ajuste vale para o movimento no solo.':'Ative Velocidade de movimento para aplicar o ajuste.'}</small>
  {error&&<small role="alert">{error}</small>}
 </div>;
}
export default function TrainerControls({gameId,status,bindings={},parameters,api,action,busy,notify=()=>{}}){
 const [combo,setCombo]=useState(String(status.combo||10)),[draft,setDraft]=useState(parameters||{strength:10,magic:10,inventory:6,gold:100});
 useEffect(()=>{setCombo(String(status.combo||10));},[status.combo]);
 useEffect(()=>{if(parameters)setDraft(parameters);},[parameters?.strength,parameters?.magic,parameters?.inventory,parameters?.gold]);
 const gameCommands=status.commands||commands[gameId];
 if(status.commands?.some(command=>command.type!=='toggle'))return <ManagedControls gameId={gameId} status={status} bindings={bindings} api={api} action={action} busy={busy}/>;
 const manifest=status.commands&&![557340,2738490,1186640].includes(gameId)?{options:status.commands}:gameId===557340?pedro:gameId===1186640?pumpkin:gameId===3826060?moventure:sol;
 return <><div className="cheat-list">{manifest.options.map((option,i)=>{const command=option.index!==undefined?option:gameCommands.find(c=>c.type==='toggle'&&c.index===i),index=command.index,id=`cheat-${gameId}-${i}`;return <div className="cheat-row" key={id}><div><label htmlFor={id}>{option.name}</label><p id={`${id}-help`}>{status.compatibility?.canConnect&&status.capabilities?.[command.id]?.available===false?status.capabilities[command.id].reason:option.description}</p></div><kbd>{keyLabel(bindings[command.id])}</kbd><input id={id} aria-describedby={`${id}-help`} className="switch" type="checkbox" role="switch" checked={!!status.options[index]} disabled={!status.owned||!!busy||status.capabilities?.[command.id]?.available===false} onChange={e=>action('cheat',()=>api.cheat(index,e.target.checked,gameId))}/></div>;})}</div>
 {gameId===557340?<form className="combo-control" onSubmit={e=>{e.preventDefault();action('combo',()=>api.combo(Number(combo)));}}><label htmlFor="combo">Multiplicador do combo<small id="combo-help">De 1 a 9999. Ative “Combo travado” para fixar.</small></label><input id="combo" aria-describedby="combo-help" required type="number" min="1" max="9999" step="1" value={combo} onChange={e=>setCombo(e.target.value)}/><button className="secondary" disabled={!status.owned||!!busy}>Aplicar</button></form>:gameId===1186640?<PumpkinSpeed multiplier={status.multiplier} active={!!status.options[2]} api={api} busy={busy}/>:gameId===2738490?<div className="sol-actions">{fields.map(([kind,name,min,max,help])=><form className="combo-control" key={kind} onSubmit={e=>{e.preventDefault();action(kind,async()=>{const actual=await api.solAction(kind,Number(draft[kind]));notify(`${name}: ${actual}.`);});}}><label htmlFor={`sol-${kind}`}>{name}<small id={`sol-${kind}-help`}>{status.compatibility?.canConnect&&status.capabilities?.[kind]?.available===false?status.capabilities[kind].reason:help}</small></label><kbd>{keyLabel(bindings[kind])}</kbd><input id={`sol-${kind}`} aria-describedby={`sol-${kind}-help`} type="number" min={min} max={max} required step="1" value={draft[kind]} onChange={e=>setDraft(old=>({...old,[kind]:e.target.value}))} onBlur={e=>{if(e.target.validity.valid)api.setSolValues({[kind]:Number(e.target.value)}).catch(error=>notify(error.message));}}/><button className="secondary" aria-label={`Aplicar ${name.toLocaleLowerCase('pt-BR')}`} disabled={!status.owned||!status.ready||!!busy||status.capabilities?.[kind]?.available===false}>{busy===kind?'Aplicando…':'Aplicar'}</button></form>)}<p className="section-copy">Os atalhos aplicam os valores salvos acima na partida atual. Itens ilimitados preserva apenas os itens que você já possui.</p></div>:null}
 </>;
}
