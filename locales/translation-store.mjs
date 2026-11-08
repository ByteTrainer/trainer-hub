import {normalizeLocale,translate} from './translate.mjs';
import {withIntegrationTranslations} from './package-overrides.mjs';

export function createTranslationStore(base,languages,initial,onLocale=()=>{}) {
 let locale=normalizeLocale(initial,languages),dictionaries=base,revision=0,packageKey='';
 const listeners=new Set();
 const notify=()=>{revision++;for(const listener of listeners)listener();};
 return {
  getLocale:()=>locale,
  snapshot:()=>revision,
  subscribe:listener=>{listeners.add(listener);return()=>listeners.delete(listener);},
  setLanguage(value){const next=normalizeLocale(value,languages);if(next===locale)return;locale=next;onLocale(next);notify();},
  setIntegrationTranslations(packages={}){
   const key=JSON.stringify(packages);if(key===packageKey)return;
   packageKey=key;dictionaries=withIntegrationTranslations(base,packages);notify();
  },
  translate:(value,parameters,scope)=>translate(value,locale,dictionaries,parameters,scope)
 };
}
