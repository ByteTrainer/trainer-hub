import {useSyncExternalStore} from 'react';
import languages from '../locales/languages.json';
import {createTranslationStore} from '../locales/translation-store.mjs';

import namespaces from '../locales/namespaces.json';
import legacy from '../locales/legacy-aliases.json';
const files = import.meta.glob('../locales/{common,games}/**/*.json', {eager: true, import: 'default'});
const dictionaries = {legacy,scopes:{}};
for(const {code} of languages){
 const common=files[`../locales/common/${code}.json`];
 dictionaries[code]={...common};
 for(const [id,slug] of Object.entries(namespaces)){
  const game=files[`../locales/games/${slug}/${code}.json`];
  Object.assign(dictionaries[code],game);
  (dictionaries.scopes[id]??={})[code]={...common,...game};
 }
}
const storageKey = 'bytetrainer.language';
let saved;
try { saved = localStorage.getItem(storageKey); } catch {}
const store=createTranslationStore(dictionaries,languages,saved || navigator.language,next=>{
 try { localStorage.setItem(storageKey,next); } catch {}
 document.documentElement.lang=next;
});
document.documentElement.lang=store.getLocale();
export const setLanguage=store.setLanguage;
export const setIntegrationTranslations=store.setIntegrationTranslations;
window.addEventListener('storage',event=>{if(event.key===storageKey)setLanguage(event.newValue);});
export function useLanguage() {
 useSyncExternalStore(store.subscribe,store.snapshot);
 return store.getLocale();
}
export const getLocale=store.getLocale;
export const t=(value,parameters)=>store.translate(value,parameters);
export const gameTranslator=id=>(value,parameters)=>store.translate(value,parameters,id);
export {languages};
