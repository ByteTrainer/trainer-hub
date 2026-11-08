import {useSyncExternalStore} from 'react';
import languages from '../locales/languages.json';
import {normalizeLocale, translate} from '../locales/translate.mjs';

const files = import.meta.glob('../locales/*.json', {eager: true, import: 'default'});
const dictionaries = Object.fromEntries(languages.map(language => [language.code, files[`../locales/${language.code}.json`]]));
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
export {languages};
