import {t} from './i18n.js';
import React from 'react';
import RefreshCw from 'lucide-react/icons/refresh-cw';
import CircleCheck from 'lucide-react/icons/circle-check';
import AlertCircle from 'lucide-react/icons/circle-exclamation-point';

export default function IntegrationPanel({id,status,online,remote,api,action,busy,notify}){
 const c=status.compatibility;
 if(!c)return null;
 const ok=c.state==='compatible', recovery=c.state==='recovery-needed';
 const canUpdate=!recovery&&!!status.path&&(online||(!ok&&c.canInstall));
 const hasUpdate=remote||c.state==='outdated';
 const label=hasUpdate?'Atualizar integração':ok?'Buscar atualização':c.state==='removed'?'Reinstalar integração':c.canInstall?'Instalar integração':'Buscar integração compatível';
 const disabled=!!busy||status.gameRunning;
 const patching=busy==='patch';
 const apply=command=>action('patch',async()=>{
  const result=await api.patch(command,id);
  if(result?.compatibility?.state==='compatible')notify(`Integração ${result.compatibility.version} pronta para usar.`);
 });
 return (
  <section className={`integration-panel ${ok?'compatible':'attention'}`} aria-labelledby="game-integration-title">
   <div className="integration-heading">
    <h2 id="game-integration-title">{t('Compatibilidade da integração')}</h2>
    <span className="integration-state">{ok?<CircleCheck size={17}/>:<AlertCircle size={17}/>} {t(c.label)}</span>
   </div>
   <p>{t(
    remote?`Pacote ${remote.version} disponível. Atualize com o jogo fechado.`:c.message
   )}</p>
   <div className="integration-meta">
    <span>{t('Build Steam: ')}{t(c.buildId||'não informada')}</span>
    <span>{t('Pacote local: ')}{t(c.version||'não identificado')}</span>
    {c.installedRelease>0&&<span>{t('Revisão instalada: ')}{t(c.installedRelease)}</span>}
   </div>
   <div className="button-row">
    {!status.path&&<a className="primary" href="#game-installation">{t('Localizar pasta do jogo')}</a>}
    {canUpdate&&<button className={hasUpdate||!ok?'primary':'secondary'} disabled={disabled} onClick={()=>apply('install')}>{t(patching?'Verificando e aplicando…':label)}</button>}
    {recovery&&<button className="primary" disabled={disabled} onClick={()=>apply('recover')}>{t('Recuperar instalação')}</button>}
    <button className="secondary" disabled={!!busy} onClick={()=>action('integration-check',async()=>{await api.checkIntegration(id);notify('Verificação dos arquivos concluída.');})}><RefreshCw size={15} className={busy==='integration-check'?'spin':''}/>{t(busy==='integration-check'?'Verificando…':'Verificar arquivos')}</button>
    {c.canRestore&&<button className="secondary" disabled={disabled} onClick={()=>apply('restore')}>{t('Restaurar original')}</button>}
    {online&&!ok&&c.canInstall&&<button className="secondary" disabled={disabled} onClick={()=>apply('install-local')}>{t('Aplicar pacote local')}</button>}
   </div>
   {status.gameRunning&&(canUpdate||recovery)&&<p className="integration-help">{t('Feche o jogo para aplicar uma atualização.')}</p>}
   {patching&&<p className="integration-help" role="status">{t('Aguarde a verificação e a aplicação dos arquivos.')}</p>}
  </section>
 );
}
