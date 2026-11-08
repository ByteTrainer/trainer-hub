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
 const label=hasUpdate?'Update integration':ok?'Check for update':c.state==='removed'?'Reinstall integration':c.canInstall?'Install integration':'Find compatible integration';
 const disabled=!!busy||status.gameRunning;
 const patching=busy==='patch';
 const apply=command=>action('patch',async()=>{
  const result=await api.patch(command,id);
  if(result?.compatibility?.state==='compatible')notify(`Integration ${result.compatibility.version} is ready to use.`);
 });
 return (
  <section className={`integration-panel ${ok?'compatible':'attention'}`} aria-labelledby="game-integration-title">
   <div className="integration-heading">
    <h2 id="game-integration-title">{t('Integration compatibility')}</h2>
    <span className="integration-state">{ok?<CircleCheck size={17}/>:<AlertCircle size={17}/>} {t(c.label)}</span>
   </div>
   <p>{t(
    remote?`Package ${remote.version} available. Update with the game closed.`:c.message
   )}</p>
   <div className="integration-meta">
    <span>{t('Steam build: ')}{t(c.buildId||'not provided')}</span>
    <span>{t('Local package: ')}{t(c.version||'unidentified')}</span>
    {c.installedRelease>0&&<span>{t('Installed revision: ')}{t(c.installedRelease)}</span>}
   </div>
   <div className="button-row">
    {!status.path&&<a className="primary" href="#game-installation">{t('Locate game folder')}</a>}
    {canUpdate&&<button className={hasUpdate||!ok?'primary':'secondary'} disabled={disabled} onClick={()=>apply('install')}>{t(patching?'Verifying and applying…':label)}</button>}
    {recovery&&<button className="primary" disabled={disabled} onClick={()=>apply('recover')}>{t('Recover installation')}</button>}
    <button className="secondary" disabled={!!busy} onClick={()=>action('integration-check',async()=>{await api.checkIntegration(id);notify('File check complete.');})}><RefreshCw size={15} className={busy==='integration-check'?'spin':''}/>{t(busy==='integration-check'?'Checking…':'Verify files')}</button>
    {c.canRestore&&<button className="secondary" disabled={disabled} onClick={()=>apply('restore')}>{t('Restore original')}</button>}
    {online&&!ok&&c.canInstall&&<button className="secondary" disabled={disabled} onClick={()=>apply('install-local')}>{t('Apply local package')}</button>}
   </div>
   {status.gameRunning&&(canUpdate||recovery)&&<p className="integration-help">{t('Close the game to apply an update.')}</p>}
   {patching&&<p className="integration-help" role="status">{t('Wait while files are verified and applied.')}</p>}
  </section>
 );
}
