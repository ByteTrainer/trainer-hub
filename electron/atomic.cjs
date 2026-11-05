const fs=require('node:fs');
const pause=new Int32Array(new SharedArrayBuffer(4));
function writeJson(file,value){
 const temp=file+'.tmp';fs.writeFileSync(temp,JSON.stringify(value));
 try{for(let attempt=0;;attempt++){try{fs.renameSync(temp,file);return;}catch(error){if(!['EPERM','EACCES','EBUSY'].includes(error.code)||attempt===7)throw error;Atomics.wait(pause,0,0,10);}}}
 finally{try{fs.unlinkSync(temp);}catch{}}
}
module.exports={writeJson};
