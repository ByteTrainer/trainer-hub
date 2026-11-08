import {applyTheme} from './theme.js';
import {t, useLanguage, setLanguage} from './i18n.js';
import React,{useEffect,useRef,useState} from 'react';
import X from 'lucide-react/icons/x';
import TrainerControls,{keyLabel,useInputFocus} from './TrainerControls.jsx';
export default function Overlay(){const locale=useLanguage();const api=window.hub,[data,setData]=useState(null),[busy,setBusy]=useState(''),[message,setMessage]=useState(null),close=useRef(null);useInputFocus(api);
 useEffect(()=>{document.documentElement.classList.add('overlay-document');document.title=t('Overlay · ByteTrainer');api.overlayState().then(setData);const off=api.onOverlayState(state=>{setData(state);});const error=api.onTrainerError(text=>setMessage({text,error:true}));const focus=()=>close.current?.focus();window.addEventListener('focus',focus);return()=>{off();error();window.removeEventListener('focus',focus);};},[]);
 useEffect(()=>{if(data?.theme)applyTheme(data.theme);},[data?.theme]);
 useEffect(()=>{if(data?.locale)setLanguage(data.locale);},[data?.locale]);
 useEffect(()=>{document.title=t('Overlay · ByteTrainer');},[locale]);
 useEffect(()=>{if(!message)return;const t=setTimeout(()=>setMessage(null),6000);return()=>clearTimeout(t);},[message]);
 async function action(name,fn){if(busy)return;setBusy(name);try{await fn();setData(await api.overlayState());}catch(e){setMessage({text:e.message.replace(/^Error invoking remote method '[^']+': Error: /,''),error:true});}finally{setBusy('');}}
 if(!data)return <div className="overlay-loading">{t('Preparing controls…')}</div>;
 const active=data.status.options.filter(Boolean).length;
 return (
  <div className="overlay-panel" role="dialog" aria-labelledby="overlay-title"><header className="overlay-header"><div><h1 id="overlay-title">{t(data.gameName)}</h1><p>{t('ByteTrainer · ')}{t(active)} {t(active===1?"active option":"active options")}</p></div><button ref={close} className="overlay-close" aria-label={t("Close overlay and return to game")} onClick={()=>api.hideOverlay()}><kbd>{t('Esc')}</kbd><X size={22}/></button></header><main className="overlay-controls"><TrainerControls gameId={data.gameId} status={data.status} bindings={data.bindings} parameters={data.parameters} api={api} action={action} busy={busy} notify={text=>setMessage({text})}/></main><footer className="overlay-footer"><span><span className="dot connected"/>{t(
   data.status.diagnostic?.label||(data.status.ready===false?'Enter a game to apply values':'Controls connected')
  )}</span><span>{t(keyLabel(data.overlayShortcut))}{t(' to return')}</span></footer><div className={message?.error?'overlay-message error':'overlay-message'} role={message?.error?'alert':'status'}>{t(message?.text)}</div></div>
 );
}
