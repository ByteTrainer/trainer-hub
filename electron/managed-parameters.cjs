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
function normalizeResourceDraft(spec,value={}){
 const resource=spec.commands.find(command=>command.type==='resource');
 return {resource:resource?.choices.some(choice=>choice.id===value.resource)?value.resource:resource?.choices[0]?.id,
  amount:Number.isSafeInteger(value.amount)&&value.amount!==0&&Math.abs(value.amount)<=9999999?value.amount:100};
}

class ManagedParametersAdapter extends MoventureAdapter {
 constructor(resources,log,spec){
  super(resources,log,spec);
  this.parameters=normalizeParameters(spec);
  this.resourceDraft=normalizeResourceDraft(spec);
  this.session=crypto.randomUUID().replaceAll('-','');
 }
 readStatus(){
  try{
   const parts=fs.readFileSync(path.join(this.folder,'status.txt'),'utf8').trim().split('|');
   if(parts.length!==9||parts[0]!=='2'||!/^\d+$/.test(parts[1])||!/^\d+$/.test(parts[2])||!/^[01]$/.test(parts[3])||!/^\d+$/.test(parts[4])||Number(parts[4])>=2**30)return null;
   const age=Date.now()-Number((BigInt(parts[2])-epoch)/10000n);
   if(age<0||age>=2000||!Number.isSafeInteger(Number(parts[7])))return null;
   return {pid:Number(parts[1]),ready:parts[3]==='1',flags:Number(parts[4]),session:parts[5],ack:parts[6],applied:Number(parts[7]),actionError:parts[8]};
  }catch{return null;}
 }
 async status(){return {...await super.status(),parameters:{...this.parameters},resourceDraft:this.resourceDraft};}
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
  if(command?.type==='resource')throw Error('Este controle usa o botão Aplicar.');
  return super.set(index,enabled);
 }
 setParameter(id,value){
  const command=this.definition.commands.find(command=>command.id===id&&command.type==='multiplier');
  if(!command||!Number.isFinite(value)||value<command.min||value>command.max)throw Error('Multiplicador fora dos limites.');
  const previous=this.parameters[id];
  this.parameters[id]=value;
  try{if(this.owned)this.write();}catch(error){this.parameters[id]=previous;throw error;}
  return {...this.parameters};
 }
 validateResource(commandId,resource,amount){
  const command=this.definition.commands.find(command=>command.id===commandId&&command.type==='resource');
  if(!command||!command.choices.some(choice=>choice.id===resource)||!Number.isSafeInteger(amount)||amount===0||Math.abs(amount)>9999999)throw Error('Selecione um recurso e uma quantidade inteira entre -9999999 e 9999999, diferente de zero.');
 }
 async resource(commandId,resource,amount){
  this.validateResource(commandId,resource,amount);
  if(this.pendingAction)throw Error('Aguarde a aplicação anterior.');
  const current=this.readStatus();
  if(!this.owned||!current?.ready||current.pid!==this.pid)throw Error('Conecte o trainer e entre na partida.');
  const nonce=crypto.randomUUID().replaceAll('-',''),session=this.session;
  this.pendingAction=true;
  try{
   const deadline=epoch+BigInt(Date.now()+3000)*10000n;
   writeText(path.join(this.folder,'resource.txt'),[this.pid,deadline,session,nonce,resource,amount].join('|'));
   const start=Date.now();
   while(Date.now()-start<4000){
    await new Promise(resolve=>setTimeout(resolve,80));
    const state=this.readStatus();
    if(!this.owned||this.session!==session)throw Error('A conexão foi encerrada durante a aplicação. Confira o inventário antes de repetir.');
    if(state?.session===session&&state.ack===nonce){
     if(state.actionError)throw Error(state.actionError);
     return state.applied;
    }
   }
   throw Error('O jogo não confirmou a operação. Confira o inventário antes de aplicar novamente.');
  }finally{this.pendingAction=false;}
 }
}
module.exports={ManagedParametersAdapter,normalizeParameters,normalizeResourceDraft};
