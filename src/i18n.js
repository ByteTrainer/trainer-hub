import {useSyncExternalStore} from 'react';
import languages from '../locales/languages.json';
import {normalizeLocale, translate} from '../locales/translate.mjs';

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
const listeners = new Set();
const storageKey = 'bytetrainer.language';
let saved;
try { saved = localStorage.getItem(storageKey); } catch {}
let current = normalizeLocale(saved || navigator.language, languages);
function updateDocument() { document.documentElement.lang = current; }
updateDocument();
export function setLanguage(value) {
  const next = normalizeLocale(value, languages);
  if (next === current) return;
  current = next;
  try { localStorage.setItem(storageKey, next); } catch {}
  updateDocument();
  for (const listener of listeners) listener();
}
window.addEventListener('storage', event => { if (event.key === storageKey) setLanguage(event.newValue); });
export function useLanguage() {
  return useSyncExternalStore(listener => { listeners.add(listener); return () => listeners.delete(listener); }, () => current);
}
export const getLocale = () => current;
export const t = (value, parameters) => translate(value, current, dictionaries, parameters);
export const gameTranslator = id => (value,parameters) => translate(value,current,dictionaries,parameters,id);
export {languages};
