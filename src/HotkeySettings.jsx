import {gameTranslator} from './i18n.js';
import React,{useEffect,useRef,useState} from 'react';
import commands from '../electron/commands.json';
import {keyLabel} from './TrainerControls.jsx';
import {eventKey} from './hotkey-event.js';
export default function HotkeySettings({id,bindings,enabled,status,api,action,busy,notify}){
 const t=gameTranslator(id);
 const [capture,setCapture]=useState(null),recording=useRef(null),[message,setMessage]=useState('');
 useEffect(()=>{const stop=()=>{recording.current=null;setCapture(null);};const off=api.onCaptureEnded(stop);const cancel=()=>{stop();api.captureKeys(false).catch(()=>{});};window.addEventListener('blur',cancel);return()=>{off();window.removeEventListener('blur',cancel);api.captureKeys(false).catch(()=>{});};},[id]);
 async function start(command){try{await api.captureKeys(true);recording.current=command;setCapture(command);setMessage('Press the new combination. Esc cancels.');}catch(e){notify(e.message);}}
 useEffect(()=>{if(!capture)return;const listener=async e=>{if(e.repeat)return;if(['Control','Alt','Shift','Meta'].includes(e.key))return;if(e.key==='Escape'||e.key==='Tab'){if(e.key==='Escape'){e.preventDefault();e.stopImmediatePropagation();}recording.current=null;setCapture(null);await api.captureKeys(false);setMessage('Change cancelled.');return;}e.preventDefault();e.stopImmediatePropagation();const command=recording.current;if(!command)return;const key=eventKey(e);if(!key){setMessage('Use F1–F24 or a combination with Ctrl, Alt or Shift.');return;}recording.current=null;try{await api.setBinding(id,command,key);setMessage('Shortcut saved for this game.');}catch(error){setMessage(error.message);}finally{setCapture(null);await api.captureKeys(false);}};window.addEventListener('keydown',listener,true);return()=>window.removeEventListener('keydown',listener,true);},[capture,id]);
 const list=[...(status.commands||commands[id]),{id:'overlay',name:'Open / close overlay'}];
 return (
  <section className="hotkey-settings" id="game-shortcuts"><div className="section-title"><h2>{t('Shortcuts and overlay')}</h2><button className="secondary" disabled={!!busy||!!capture} onClick={()=>action('reset',async()=>{await api.resetBindings(id);setMessage('Default shortcuts restored for this game.');})}>{t('Restore defaults')}</button></div><p className="section-copy">{t(
    'Click a key to change it. Your choices are saved for this game only.'
   )}</p><div className="overlay-setting"><div><label htmlFor={`overlay-enabled-${id}`}>{t('In-game overlay')}</label><p>{t(
    'Central panel with larger controls. Use windowed or borderless mode.'
   )}</p></div><input id={`overlay-enabled-${id}`} className="switch" role="switch" type="checkbox" checked={enabled} disabled={!!busy||!!capture} onChange={e=>action('overlay',()=>api.setOverlay(id,e.target.checked))}/><button className="secondary" disabled={!enabled||!status.owned||!status.gameRunning||!!busy||!!capture} onClick={()=>action('overlay-open',()=>api.toggleOverlay())}>{t('Open overlay')}</button></div><div className="hotkey-grid">{list.map(command=><div className="hotkey-row" key={command.id}><span id={`binding-${command.id}`}>{t(command.name)}</span><button className={capture===command.id?'key-button recording':'key-button'} aria-label={t(`Change shortcut: ${command.name}, ${keyLabel(bindings[command.id])}`)} aria-describedby="shortcut-message" disabled={!!busy||!!capture&&capture!==command.id} onClick={()=>start(command.id)}>{t(capture===command.id?'Press the keys…':keyLabel(bindings[command.id]))}</button><button className="clear-key" aria-label={t(`Remove shortcut: ${command.name}`)} disabled={!bindings[command.id]||!!busy||!!capture} onClick={()=>action('binding',()=>api.setBinding(id,command.id,null))}>{t('Remove')}</button></div>)}</div><p id="shortcut-message" role="status" className="shortcut-message">{t(
    message||'Shortcuts work while the trainer is connected. Duplicate combinations are blocked.'
   )}</p></section>
 );
}
