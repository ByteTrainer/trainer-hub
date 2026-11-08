import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
const source=fs.readFileSync(new URL('../src/theme.js',import.meta.url),'utf8').replace(/^import[^\n]+\n/,'').replaceAll('export function','function')+'\nglobalThis.api={applyTheme,useSystemTheme};';
function fixture(saved){
 const values=new Map(saved?[['bytetrainer.theme',saved]]:[]),events={};
 const document={documentElement:{dataset:{},style:{}}},media={matches:false,addEventListener(name,fn){events.media=fn;}};
 const context={document,window:{matchMedia:()=>media,addEventListener(name,fn){events[name]=fn;}},localStorage:{getItem:key=>values.get(key),setItem:(key,value)=>values.set(key,value)},useSyncExternalStore:(subscribe,snapshot)=>snapshot()};
 vm.runInNewContext(source,context);return {...context,events,media,values};
}
test('appearance defaults to the system and restores a saved explicit theme',()=>{
 const system=fixture();assert.equal(system.document.documentElement.dataset.theme,'system');assert.equal(system.document.documentElement.style.colorScheme,'light dark');
 const saved=fixture('dark');assert.equal(saved.document.documentElement.style.colorScheme,'dark');
 assert.equal(fixture('invalid').document.documentElement.dataset.theme,'system');
});
test('theme changes persist and system changes do not discard an explicit override',()=>{
 const f=fixture();f.api.applyTheme('light');assert.equal(f.values.get('bytetrainer.theme'),'light');
 f.media.matches=true;f.events.media();assert.equal(f.document.documentElement.style.colorScheme,'light');
 f.api.applyTheme('system');assert.equal(f.document.documentElement.style.colorScheme,'light dark');assert.equal(f.api.useSystemTheme(),'dark');
 f.events.storage({key:'bytetrainer.theme',newValue:'dark'});assert.equal(f.document.documentElement.style.colorScheme,'dark');
});
