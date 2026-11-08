import {t} from './i18n.js';
import React from 'react';
import FolderOpen from 'lucide-react/icons/folder-open';

export default function GameInstallation({game,status,customPath,onLocate,busy}){
 const disabled=!!busy||status.gameRunning;
 return (
  <section className="settings-section game-installation" id="game-installation" aria-labelledby="game-installation-title" tabIndex={-1}>
   <h2 id="game-installation-title">{t('Installation folder')}</h2>
   <p>{t(
    customPath?'Folder chosen manually.':'Automatic detection through Steam.'
   )}</p>
   <p className="game-installation-path">{t(status.path||'No folder detected on this computer.')}</p>
   <div className="button-row">
    <button className="secondary" disabled={disabled} onClick={()=>onLocate(game.id,false)}><FolderOpen size={16}/>{t('Choose folder')}</button>
    {customPath&&<button className="secondary" disabled={disabled} onClick={()=>onLocate(game.id,true)}>{t('Use automatic detection')}</button>}
   </div>
   {status.gameRunning&&<p className="integration-help">{t('Close the game to change its folder.')}</p>}
  </section>
 );
}
