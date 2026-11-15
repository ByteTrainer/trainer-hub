const {spawn}=require('node:child_process');
const {createInterface}=require('node:readline');
const path=require('node:path');

const namedKeys={Space:0x20,PageUp:0x21,PageDown:0x22,End:0x23,Home:0x24,Left:0x25,
 Up:0x26,Right:0x27,Down:0x28,Insert:0x2d,Delete:0x2e};
const modifierBits={Control:1,Alt:2,Shift:4};

function bindingCode(accelerator){
 const parts=accelerator.split('+'),key=parts.pop();
 const vk=namedKeys[key]??(key.length===1?key.charCodeAt(0):key.startsWith('num')?0x60+Number(key.slice(3)):0x6f+Number(key.slice(1)));
 const modifiers=parts.reduce((bits,mod)=>bits|modifierBits[mod],0);
 return vk|(modifiers<<8);
}

class KeyboardListener{
 constructor(folder,onError){
  this.file=path.join(folder,'KeyboardListener.exe');
  this.onError=onError;
  this.generation=0;
  this.bindings=[];
 }
 setBindings(bindings){
  this.generation++;
  this.bindings=bindings.map(([key,callback])=>[bindingCode(key),callback]);
  if(!this.child&&this.bindings.length)this.start();
  if(this.ready)this.send();
 }
 start(){
  const child=spawn(this.file,[],{windowsHide:true,stdio:['pipe','pipe','ignore']});
  this.child=child;
  const fail=()=>{
   if(this.child!==child)return;
   this.child=null;
   this.ready=false;
   if(this.bindings.length)this.onError('Could not start the keyboard listener.');
  };
  child.once('error',fail);
  child.once('exit',fail);
  child.stdin.on('error',fail);
  createInterface({input:child.stdout}).on('line',line=>{
   if(this.child!==child)return;
   if(line==='ready'){this.ready=true;this.send();return;}
   const match=/^(\d+) (\d+)$/.exec(line);
   if(match&&Number(match[1])===this.generation)this.bindings[Number(match[2])]?.[1]();
  });
 }
 send(){
  this.child.stdin.write([this.generation,...this.bindings.map(([code])=>code)].join(' ')+'\n');
 }
 close(){
  const child=this.child;
  this.child=null;
  this.ready=false;
  this.bindings=[];
  child?.stdin.end();
 }
}
module.exports={KeyboardListener,bindingCode};
