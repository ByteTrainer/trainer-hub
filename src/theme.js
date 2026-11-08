import {useSyncExternalStore} from 'react';
const key='bytetrainer.theme';
const listeners=new Set();
const media=window.matchMedia('(prefers-color-scheme: dark)');
let preference='system';
try {const saved=localStorage.getItem(key);if(['system','light','dark'].includes(saved))preference=saved;}catch{}
function render(){
 document.documentElement.dataset.theme=preference;
 document.documentElement.style.colorScheme=preference==='system'?'light dark':preference;
 for(const listener of listeners)listener();
}
export function applyTheme(value){
 if(!['system','light','dark'].includes(value))return;
 preference=value;try{localStorage.setItem(key,value);}catch{}render();
}
media.addEventListener('change',render);
window.addEventListener('storage',event=>{if(event.key===key)applyTheme(event.newValue||'system');});
render();
export function useSystemTheme(){return useSyncExternalStore(callback=>{listeners.add(callback);return()=>listeners.delete(callback);},()=>media.matches?'dark':'light');}
