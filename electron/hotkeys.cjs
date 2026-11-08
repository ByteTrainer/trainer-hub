const COMMANDS=require('./commands.json');
const SPECIAL=['Space','Insert','Delete','Home','End','PageUp','PageDown','Up','Down','Left','Right'];
const RESERVED=new Set(['Alt+F4','Alt+Tab','Control+Escape','Control+Shift+Escape','Control+Alt+Delete']);
function accelerator(value){
 if(value===null||value==='')return null;
 if(typeof value!=='string'||value.length>60)throw new Error('Invalid shortcut.');
 const parts=value.split('+');let key=parts.pop(),mods=[];
 for(let part of parts){if(/^(ctrl|control)$/i.test(part))part='Control';else if(/^alt$/i.test(part))part='Alt';else if(/^shift$/i.test(part))part='Shift';else throw new Error('Use Ctrl, Alt or Shift as modifiers.');if(mods.includes(part))throw new Error('Duplicate modifier.');mods.push(part);}
 if(/^f([1-9]|1\d|2[0-4])$/i.test(key))key=key.toUpperCase();else if(/^num[0-9]$/i.test(key))key=key.toLowerCase();else if(/^[a-z0-9]$/i.test(key))key=key.toUpperCase();else{const named=SPECIAL.find(n=>n.toLowerCase()===key?.toLowerCase());if(!named)throw new Error('Use F1–F24, a numeric key or a combination with Ctrl, Alt or Shift.');key=named;}
 mods=['Control','Alt','Shift'].filter(m=>mods.includes(m));
 if(!mods.length&&!/^F\d+$|^num\d$/.test(key))throw new Error('Combine letters and numbers with Ctrl, Alt or Shift.');
 const result=[...mods,key].join('+');if(RESERVED.has(result))throw new Error('This combination is reserved by Windows.');return result;
}
function defaults(id){if(!COMMANDS[id])throw new Error('No trainer for this game.');return Object.fromEntries([...COMMANDS[id].map(c=>[c.id,c.key]),['overlay','Alt+W']]);}
function validateMap(id,value){const base=defaults(id);if(!value||typeof value!=='object'||Array.isArray(value)||Object.keys(value).some(k=>!Object.hasOwn(base,k)))throw new Error('Invalid shortcut configuration.');const result={...base};for(const[key,v]of Object.entries(value))result[key]=accelerator(v);const seen=new Map();for(const[key,v]of Object.entries(result)){if(v&&seen.has(v))throw new Error(`The key ${v.replace('Control','Ctrl')} is already assigned to another control for this game.`);if(v)seen.set(v,key);}return result;}
function normalizeAll(value){const result={};for(const id of Object.keys(COMMANDS)){try{result[id]=validateMap(id,value?.[id]||{});}catch{result[id]=defaults(id);}}return result;}
class ShortcutRegistry{
 constructor(api){this.api=api;this.current=[];this.paused=false;}
 replace(next){if(this.paused){this.current=next;return;}const old=this.current;for(const[k]of old)this.api.unregister(k);const added=[];try{for(const[k,fn]of next){if(!this.api.register(k,fn))throw new Error(`The shortcut ${k.replace('Control','Ctrl')} is used by another app. Choose another key.`);added.push(k);}this.current=next;}catch(error){for(const k of added)this.api.unregister(k);for(const[k,fn]of old){if(!this.api.register(k,fn))error.message+=' A previous shortcut could not be restored. Reconnect the trainer.';}this.current=old;throw error;}}
 pause(){if(this.paused)return;for(const[k]of this.current)this.api.unregister(k);this.paused=true;}
 resume(){if(!this.paused)return;const desired=this.current;this.paused=false;this.current=[];this.replace(desired);}
 clear(){for(const[k]of this.current)this.api.unregister(k);this.current=[];this.paused=false;}
}
module.exports={COMMANDS,accelerator,defaults,validateMap,normalizeAll,ShortcutRegistry};
