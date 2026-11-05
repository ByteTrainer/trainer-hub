(function(){
 'use strict';
 try{
  const fs=require('fs'),path=require('path');
  const folder=path.join(path.dirname(process.execPath),'SolCestoTrainerData');
  fs.mkdirSync(folder,{recursive:true});
  let engine=null,error=null,lastStatus=0;
  function writeStatus(){const file=path.join(folder,'connected.json'),temp=file+'.tmp';fs.writeFileSync(temp,JSON.stringify({protocol:2,pid:process.pid,time:Date.now(),error,...engine?.status()}));fs.renameSync(temp,file);}
  const timer=setInterval(()=>{
   try{
    const runtime=globalThis.c3_runtimeInterface?._localRuntime;
    if(!engine&&runtime&&!runtime.IsLoading())engine=SolCestoTrainerEngine.createEngine(runtime);
    if(engine){try{engine.configure(JSON.parse(fs.readFileSync(path.join(folder,'settings.json'),'utf8')));}catch{engine.configure({options:[false,false,false],expires:0});}engine.tick();}
    error=null;
   }catch(e){error=String(e.message);engine?.close();engine=null;}
   if(Date.now()-lastStatus>400){lastStatus=Date.now();try{writeStatus();}catch{}}
  },100);
  globalThis.addEventListener('beforeunload',()=>{clearInterval(timer);engine?.close();});
 }catch(e){console.error('Trainer Hub:',e);}
})();
