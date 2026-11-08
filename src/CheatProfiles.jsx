import React,{useEffect,useState} from 'react';
import {t} from './i18n.js';
export default function CheatProfiles({gameId,entry={profiles:[],defaultId:null},status,api,action,busy}){
 const [selected,setSelected]=useState(''),[name,setName]=useState(''),[message,setMessage]=useState('');
 useEffect(()=>{setSelected('');setName('');setMessage('');},[gameId]);
 const profile=entry.profiles.find(p=>p.id===selected);
 async function run(operation,input){
  return action('profile',async()=>{
   const result=await api.profile(gameId,operation,input);
   setMessage(operation==='apply'?(result.skipped.length?'Some saved cheats are unavailable in this integration and were skipped.':'Profile applied.'):operation==='delete'?'Profile deleted.':'Profile saved.');
   if(operation==='save'){const saved=result.profiles.find(p=>p.name===input.name.trim());if(saved)setSelected(saved.id);}
   if(operation==='delete'){setSelected('');setName('');}
  });
 }
 return <details className="cheat-profiles">
  <summary><span>{t('Cheat profiles')}</span><small>{entry.profiles.find(p=>p.id===entry.defaultId)?.name}</small></summary>
  <p>{t('Save active cheats and multipliers. Choose a default to apply it automatically when the game is ready in a new session.')}</p>
  <div className="profile-fields"><label>{t('Saved profile')}<select value={selected} disabled={!!busy} onChange={event=>{setSelected(event.target.value);setName(entry.profiles.find(p=>p.id===event.target.value)?.name||'');setMessage('');}}><option value="">{t('New profile')}</option>{entry.profiles.map(p=><option value={p.id} key={p.id}>{p.name}</option>)}</select></label>
  <label>{t('Profile name')}<input value={name} maxLength={60} placeholder={t('For example: Easy mode')} disabled={!!busy} onChange={event=>setName(event.target.value)}/></label></div>
  <div className="button-row"><button className="secondary" disabled={!!busy||!status.owned||!name.trim()} onClick={()=>run('save',{id:profile?.id,name})}>{t(profile?'Update with active cheats':'Save active cheats')}</button><button className="secondary" disabled={!!busy||!profile||!status.owned||status.ready===false} onClick={()=>run('apply',{id:selected})}>{t('Apply profile')}</button><button className="text-button" disabled={!!busy||!profile} onClick={()=>run('delete',{id:selected})}>{t('Delete profile')}</button></div>
  <label className="profile-default">{t('Default for new sessions')}<select value={entry.defaultId||''} disabled={!!busy} onChange={event=>run('default',{id:event.target.value||null})}><option value="">{t('None — activate cheats manually')}</option>{entry.profiles.map(p=><option value={p.id} key={p.id}>{p.name}</option>)}</select></label>
  <p className="section-copy">{t('Currency and resource actions are not saved or applied by profiles.')}</p>
  <p className="profile-message" role="status">{t(message)}</p>
 </details>;
}
