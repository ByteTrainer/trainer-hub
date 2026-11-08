const {spawn}=require('node:child_process'),readline=require('node:readline'),path=require('node:path');
class WindowProbe{
 constructor(folder){this.folder=folder;this.next=0;this.pending=new Map();}
 request(data){if(!this.child){this.child=spawn(path.join(this.folder,'WindowProbe.exe'),[],{windowsHide:true,stdio:['pipe','pipe','ignore']});const child=this.child;readline.createInterface({input:child.stdout}).on('line',line=>{try{const value=JSON.parse(line),pending=this.pending.get(value.id);if(pending){this.pending.delete(value.id);clearTimeout(pending.timer);value.error?pending.reject(new Error(value.error)):pending.resolve(value.result);}}catch{}});const fail=()=>{if(this.child===child)this.child=null;for(const p of this.pending.values()){clearTimeout(p.timer);p.reject(new Error('Could not access the game window.'));}this.pending.clear();};child.on('error',fail);child.on('exit',fail);}
  return new Promise((resolve,reject)=>{const id=++this.next,timer=setTimeout(()=>{this.pending.delete(id);reject(new Error('The game window took too long to respond.'));},2000);this.pending.set(id,{resolve,reject,timer});this.child.stdin.write(JSON.stringify({...data,id})+'\n',error=>{if(error){clearTimeout(timer);this.pending.delete(id);reject(error);}});});
 }
 close(){this.child?.kill();this.child=null;}
}
module.exports={WindowProbe};
