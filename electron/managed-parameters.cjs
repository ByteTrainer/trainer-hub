const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const {writeText}=require('./atomic.cjs');
const {MoventureAdapter}=require('./moventure.cjs');
const epoch=621355968000000000n;

function normalizeParameters(spec,value={}){
 return Object.fromEntries(spec.commands.filter(command=>command.type==='multiplier').map(command=>{
  const number=value[command.id];
  return [command.id,Number.isFinite(number)&&number>=command.min&&number<=command.max?number:command.default];
 }));
}
function normalizeResourceDraft(spec,value={},commandId){
 const resource=spec.commands.find(command=>command.type==='resource'&&(!commandId||command.id===commandId));
 const validAmount=resource?.operation==='set'?value.amount>=0&&value.amount<=999999999999:value.amount!==0&&Math.abs(value.amount)<=9999999;
 return {resource:resource?.choices.some(choice=>choice.id===value.resource)?value.resource:resource?.choices[0]?.id,
  amount:Number.isSafeInteger(value.amount)&&validAmount?value.amount:100};
}
function normalizeResourceDrafts(spec,value={}){
 return Object.fromEntries(spec.commands.filter(command=>command.type==='resource').map((command,index)=>[
  command.id,normalizeResourceDraft(spec,value.byCommand?.[command.id]||(index===0?value:{}),command.id)
 ]));
}

class ManagedParametersAdapter extends MoventureAdapter {
 constructor(resources,log,spec){
  super(resources,log,spec);
  this.parameters=normalizeParameters(spec);
  this.resourceDraft=normalizeResourceDraft(spec);
  this.resourceDrafts=normalizeResourceDrafts(spec);
  this.session=crypto.randomUUID().replaceAll('-','');
 }
 readStatus(){
  try{
   const parts=this.readStatusText().trim().split('|');
   if(parts.length!==9||parts[0]!=='2'||!/^\d+$/.test(parts[1])||!/^\d+$/.test(parts[2])||!/^[01]$/.test(parts[3])||!/^\d+$/.test(parts[4])||Number(parts[4])>=2**30)return null;
   const age=Date.now()-Number((BigInt(parts[2])-epoch)/10000n);
   if(age<0||age>=2000||!Number.isSafeInteger(Number(parts[7])))return null;
   return {pid:Number(parts[1]),ready:parts[3]==='1',flags:Number(parts[4]),session:parts[5],ack:parts[6],applied:Number(parts[7]),actionError:parts[8]};
  }catch{return null;}
 }
 async status(){return {...await super.status(),parameters:{...this.parameters},resourceDraft:this.resourceDraft,resourceDrafts:this.resourceDrafts};}
 write(){
  fs.mkdirSync(this.folder,{recursive:true});
  const deadline=this.owned?epoch+BigInt(Date.now()+3500)*10000n:0n;
  const flags=this.options.reduce((mask,on,index)=>mask+(on?2**index:0),0);
  const values=Array(this.options.length).fill(1);
  for(const command of this.definition.commands)if(command.type==='multiplier')values[command.index]=this.parameters[command.id];
  writeText(path.join(this.folder,'settings.txt'),[this.pid,deadline,flags,this.session,values.join(',')].join('|'));
 }
 async connect(){this.session=crypto.randomUUID().replaceAll('-','');return super.connect();}
 set(index,enabled){
  const command=this.definition.commands.find(command=>command.index===index);
  if(command?.type==='resource')throw Error('This control uses the Apply button.');
  return super.set(index,enabled);
 }
 setParameter(id,value){
  const command=this.definition.commands.find(command=>command.id===id&&command.type==='multiplier');
  if(!command||!Number.isFinite(value)||value<command.min||value>command.max)throw Error('Multiplier is out of bounds.');
  const previous=this.parameters[id];
  this.parameters[id]=value;
  try{if(this.owned)this.write();}catch(error){this.parameters[id]=previous;throw error;}
  return {...this.parameters};
 }
 validateResource(commandId,resource,amount){
  const command=this.definition.commands.find(command=>command.id===commandId&&command.type==='resource');
  const validAmount=command?.operation==='set'?amount>=0&&amount<=999999999999:amount!==0&&Math.abs(amount)<=9999999;
  if(!command||!command.choices.some(choice=>choice.id===resource)||!Number.isSafeInteger(amount)||!validAmount)throw Error(command?.operation==='set'?'Enter a whole number between 0 and 999999999999.':'Select a resource and a whole amount between -9999999 and 9999999, excluding zero.');
 }
 async resource(commandId,resource,amount){
  this.validateResource(commandId,resource,amount);
  if(this.pendingAction)throw Error('Wait for the previous operation to finish.');
  const current=this.readStatus();
  if(!this.owned||!current?.ready||current.pid!==this.pid)throw Error('Connect the trainer and enter a game.');
  const nonce=crypto.randomUUID().replaceAll('-',''),session=this.session;
  this.pendingAction=true;
  try{
   const deadline=epoch+BigInt(Date.now()+3000)*10000n;
   writeText(path.join(this.folder,'resource.txt'),[this.pid,deadline,session,nonce,resource,amount].join('|'));
   const start=Date.now();
   while(Date.now()-start<4000){
    await new Promise(resolve=>setTimeout(resolve,80));
    const state=this.readStatus();
    if(!this.owned||this.session!==session)throw Error('The connection ended during the operation. Check your inventory before trying again.');
    if(state?.session===session&&state.ack===nonce){
     if(state.actionError)throw Error(state.actionError);
     return state.applied;
    }
   }
   throw Error('The game did not confirm the operation. Check your inventory before trying again.');
  }finally{this.pendingAction=false;}
 }
}
module.exports={ManagedParametersAdapter,normalizeParameters,normalizeResourceDraft,normalizeResourceDrafts};
