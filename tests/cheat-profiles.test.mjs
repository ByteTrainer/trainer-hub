import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const {normalizeProfiles,captureProfile,applyProfile,ProfileSessions}=require('../electron/cheat-profiles.cjs');
const commands=[{id:'health',index:0,type:'toggle'},{id:'speed',index:1,type:'multiplier',min:1,max:5},{id:'coins',index:2,type:'resource'}];
function adapter(){return {owned:true,options:[true,true,false],parameters:{speed:2},set(i,on){this.options[i]=on;},setParameter(id,value){this.parameters[id]=value;},resource(){throw Error('Resource actions must never run');},disconnect(){this.owned=false;}};}
test('profiles survive a JSON round trip and contain toggles and multipliers without resource actions',()=>{
 const a=adapter(),profile=captureProfile('Easy mode',a,commands);
 const saved=normalizeProfiles(JSON.parse(JSON.stringify({123:{profiles:[profile],defaultId:profile.id}})));
 assert.deepEqual(saved[123].profiles[0],profile);assert.equal(saved[123].defaultId,profile.id);
 assert.deepEqual(profile.enabled,['health','speed']);assert.deepEqual(profile.parameters,{speed:2});
 const target=adapter();target.options=[false,false,false];target.parameters.speed=1;
 applyProfile(profile,target,commands);assert.deepEqual(target.options,[true,true,false]);assert.equal(target.parameters.speed,2);
 assert.throws(()=>captureProfile(' '.repeat(3),a,commands),/name/);
 assert.equal(normalizeProfiles({123:{profiles:[profile],defaultId:'missing'}})[123].defaultId,null);
});
test('applying a profile replaces enabled toggles, skips removed capabilities and restores previous state on failure',()=>{
 const a=adapter();a.capabilities={speed:{available:false}};
 const result=applyProfile({enabled:['speed','removed'],parameters:{speed:3}},a,commands);
 assert.deepEqual(result.skipped,['speed','removed']);assert.deepEqual(a.options,[false,false,false]);
 a.options=[true,false,false];a.parameters.speed=2;
 assert.throws(()=>applyProfile({enabled:['speed'],parameters:{speed:99}},a,commands),/limits/);
 assert.deepEqual(a.options,[true,false,false]);assert.equal(a.parameters.speed,2);
});
test('defaults run once per game process, wait for readiness and respect explicit disconnects',()=>{
 const sessions=new ProfileSessions(),state={runtimeSession:'10',connected:true,ready:false};
 assert.equal(sessions.shouldApply(123,state,'a'),false);state.ready=true;
 assert.equal(sessions.shouldApply(123,state,'a'),true);sessions.mark(123,state,'a');
 assert.equal(sessions.shouldApply(123,state,'a'),false);
 assert.equal(sessions.shouldApply(123,{...state,runtimeSession:'11'},'a'),true);
 sessions.suppress(123,{...state,runtimeSession:'11'});
 assert.equal(sessions.shouldApply(123,{...state,runtimeSession:'11'},'a'),false);
 assert.equal(sessions.shouldApply(123,{...state,runtimeSession:'12',compatibility:{canConnect:false}},'a'),false);
 assert.equal(sessions.shouldApply(123,{...state,runtimeSession:null},'a'),false);
});
