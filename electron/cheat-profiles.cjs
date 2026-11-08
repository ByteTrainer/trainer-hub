const {randomUUID}=require('node:crypto');
const toggles=commands=>commands.filter(c=>['toggle','multiplier'].includes(c.type));
function normalizeProfiles(value={}) {
 const result={};
 for(const [gameId,entry] of Object.entries(value||{})) {
  if(!/^\d+$/.test(gameId)||!Array.isArray(entry?.profiles))continue;
  const ids=new Set();
  const profiles=entry.profiles.slice(0,20).flatMap(p=>{
   if(typeof p?.id!=='string'||ids.has(p.id)||typeof p.name!=='string'||!p.name.trim())return [];
   ids.add(p.id);
   return [{id:p.id,name:p.name.trim().slice(0,60),enabled:Array.isArray(p.enabled)?[...new Set(p.enabled.filter(v=>typeof v==='string'))]:[],
    parameters:Object.fromEntries(Object.entries(p.parameters||{}).filter(([,v])=>Number.isFinite(v))),
    ...(Number.isInteger(p.combo)&&p.combo>=1&&p.combo<=9999?{combo:p.combo}:{}),
    ...(Number.isFinite(p.multiplier)&&p.multiplier>=50&&p.multiplier<=250?{multiplier:p.multiplier}:{})}];
  });
  result[gameId]={profiles,defaultId:ids.has(entry.defaultId)?entry.defaultId:null};
 }
 return result;
}
function captureProfile(name,target,commands,id=randomUUID()) {
 if(typeof name!=='string'||!name.trim()||name.trim().length>60)throw Error('Enter a profile name with up to 60 characters.');
 return {id,name:name.trim(),enabled:toggles(commands).filter(c=>target.options[c.index]).map(c=>c.id),
  parameters:Object.fromEntries(commands.filter(c=>c.type==='multiplier'&&Number.isFinite(target.parameters?.[c.id])).map(c=>[c.id,target.parameters[c.id]])),...(target.combo!==undefined?{combo:target.combo}:{}),...(target.multiplier!==undefined?{multiplier:target.multiplier}:{})};
}
function applyProfile(profile,target,commands) {
 if(!target.owned)throw Error('Connect the trainer first.');
 const before=captureProfile('rollback',target,commands);
 const eligible=toggles(commands),skipped=profile.enabled.filter(id=>!eligible.some(c=>c.id===id)||target.capabilities?.[id]?.available===false);
 function values(p){
  for(const c of commands)if(c.type==='multiplier'&&Number.isFinite(p.parameters?.[c.id])) {
   if(p.parameters[c.id]<c.min||p.parameters[c.id]>c.max)throw Error('A saved multiplier is outside the limits of this integration.');
   target.setParameter(c.id,p.parameters[c.id]);
  }
  if(p.combo!==undefined&&target.setCombo)target.setCombo(p.combo);
  if(p.multiplier!==undefined&&target.setMultiplier)target.setMultiplier(p.multiplier);
 }
 try {
  // Clear old toggles before changing values. Resource actions are never replayed.
  for(const c of eligible)if(target.options[c.index])target.set(c.index,false);
  values(profile);
  for(const c of eligible)if(profile.enabled.includes(c.id)&&!skipped.includes(c.id))target.set(c.index,true);
 } catch(error) {
  try {
   for(const c of eligible)if(target.options[c.index])target.set(c.index,false);
   values(before);
   for(const c of eligible)if(before.enabled.includes(c.id))target.set(c.index,true);
  } catch {target.disconnect();}
  throw error;
 }
 return {skipped};
}
class ProfileSessions {
 constructor(){this.attempts=new Map();this.suppressed=new Set();}
 key(id,status){return status.runtimeSession?`${id}:${status.runtimeSession}`:null;}
 suppress(id,status){const key=this.key(id,status);if(key)this.suppressed.add(key);}
 mark(id,status,profileId){const key=this.key(id,status);if(key)this.attempts.set(key,profileId);}
 shouldApply(id,status,profileId){
  const key=this.key(id,status);
  return !!(key&&profileId&&status.connected&&status.ready!==false&&!status.error&&status.compatibility?.canConnect!==false&&!this.suppressed.has(key)&&!this.attempts.has(key));
 }
}
module.exports={normalizeProfiles,captureProfile,applyProfile,ProfileSessions};
