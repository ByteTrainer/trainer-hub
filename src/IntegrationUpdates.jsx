import {t} from './i18n.js';
import React,{useEffect,useState} from 'react';import Download from 'lucide-react/icons/download';
const names={557340:'My Friend Pedro',2738490:'Sol Cesto',3826060:'Moventure',1186640:'Pumpkin Jack'};
export default function IntegrationUpdates({state,api,action,busy,notify,openGame,supported}){const[url,setURL]=useState(state.url||''),[saved,setSaved]=useState('');useEffect(()=>{setURL(state.url||'');},[state.url]);
 const newPackages=(state.available||[]).filter(p=>!supported.includes(p.gameId));
 const installedUpdates=(state.available||[]).filter(p=>supported.includes(p.gameId));
 const renderPackages=packages=><>{packages.length>0&&<ul className="integration-downloads">{packages.map(p=><li key={p.gameId}><div><strong>{t(p.name||names[p.gameId]||`Game ${p.gameId}`)}</strong><span>{t('Package ')}{t(p.version)}{t(' available')}</span></div>{supported.includes(p.gameId)?<button className="secondary" disabled={!!busy} onClick={()=>openGame(p.gameId)}>{t('Open game page')}</button>:<button className="secondary" disabled={!!busy} onClick={()=>action('updates-download',async()=>{const result=await api.downloadIntegration(p.gameId);notify(`Package ${result.version} verified. Update the integration with the game closed.`);})}>{t(busy==='updates-download'?'Downloading…':'Download package')}</button>}</li>)}</ul>}</>;
 return (
  <section className="settings-section integration-updates"><h2>{t('Integration updates')}</h2><p>{t(
      'Get game fixes without reinstalling ByteTrainer. Only packages signed by this Hub\'s publisher are accepted.'
    )}</p><p className="updates-status" role="status">{t(state.status)}</p><div className="button-row"><button className="primary" disabled={!!busy} onClick={()=>action('integration-import',async()=>{const result=await api.importIntegration();if(result)notify(result.unchanged?'This package is already installed in the Hub.':`Package ${result.version} is ready. Update the game's integration with the game closed.`);})}><Download size={16}/>{t(
      busy==='integration-import'?'Verifying package…':'Import signed package'
    )}</button><button className="secondary" disabled={!!busy||!state.url} onClick={()=>action('updates-check',()=>api.checkUpdates())}>{t(busy==='updates-check'?'Checking…':'Check online')}</button></div>{renderPackages(newPackages)}
    {installedUpdates.length>0&&<details className="updates-server">
     <summary>{t('Updates for installed packages ({0})',{0:installedUpdates.length})}</summary>
     {renderPackages(installedUpdates)}
    </details>}<details className="updates-server"><summary>{t('Configure update server')}</summary><form onSubmit={e=>{e.preventDefault();action('updates-url',async()=>{await api.setUpdatesURL(url.trim());setSaved(url.trim()?'Server saved. Use Check online to check for packages.':'Server removed. Local package imports are still available.');});}}><label htmlFor="integration-updates-url">{t('Server address')}</label><p id="integration-updates-url-help">{t(
      'Use the HTTPS address provided by the integration publisher. Leave it empty if there is no server.'
    )}</p><div className="updates-url-row"><input id="integration-updates-url" type="url" pattern="https://.*" aria-describedby="integration-updates-url-help" value={url} onChange={e=>setURL(e.target.value)} onInvalid={e=>e.target.setAttribute('aria-invalid','true')} onInput={e=>e.target.removeAttribute('aria-invalid')}/><button className="secondary" disabled={!!busy}>{t('Save address')}</button></div><p role="status" className="integration-help">{t(saved)}</p></form></details><small>{t(
      'Local file compatibility is checked automatically when the Hub opens and while it runs. New packages are applied with the game closed.'
    )}</small></section>
);}
