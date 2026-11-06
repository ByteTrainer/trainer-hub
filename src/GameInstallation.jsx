import React from 'react';
import FolderOpen from 'lucide-react/icons/folder-open';

export default function GameInstallation({game,status,customPath,onLocate,busy}){
 const disabled=!!busy||status.gameRunning;
 return <section className="settings-section game-installation" id="game-installation" aria-labelledby="game-installation-title" tabIndex={-1}>
  <h2 id="game-installation-title">Pasta de instalação</h2>
  <p>{customPath?'Pasta escolhida manualmente.':'Detecção automática pela Steam.'}</p>
  <p className="game-installation-path">{status.path||'Nenhuma pasta detectada neste computador.'}</p>
  <div className="button-row">
   <button className="secondary" disabled={disabled} onClick={()=>onLocate(game.id,false)}><FolderOpen size={16}/>Escolher pasta</button>
   {customPath&&<button className="secondary" disabled={disabled} onClick={()=>onLocate(game.id,true)}>Usar detecção automática</button>}
  </div>
  {status.gameRunning&&<p className="integration-help">Feche o jogo para alterar a pasta.</p>}
 </section>;
}
