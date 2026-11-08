import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const placeholders = value => [...value.matchAll(/\{\d+\}/g)].map(match => match[0]).sort().join(',');

export async function validateTranslations(directory = path.join(root, 'locales')) {
  const languages = JSON.parse(await fs.readFile(path.join(directory, 'languages.json'), 'utf8'));
  if (!Array.isArray(languages) || !languages.length || new Set(languages.map(language => language.code)).size !== languages.length) throw Error('Language codes must be unique.');
  for (const language of languages) {
    if (!/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(language.code) || Intl.getCanonicalLocales(language.code)[0] !== language.code || typeof language.name !== 'string' || !language.name.trim()) throw Error('Invalid language entry.');
  }
  const source = JSON.parse(await fs.readFile(path.join(directory, 'pt-BR.json'), 'utf8'));
  const keys = Object.keys(source).sort();
  for (const language of languages) {
    const dictionary = JSON.parse(await fs.readFile(path.join(directory, language.code + '.json'), 'utf8'));
    const missing = keys.filter(key => !Object.hasOwn(dictionary, key));
    const extra = Object.keys(dictionary).filter(key => !Object.hasOwn(source, key));
    if (missing.length || extra.length) throw Error(language.code + ': missing ' + JSON.stringify(missing) + ', extra ' + JSON.stringify(extra));
    for (const key of keys) {
      if (typeof dictionary[key] !== 'string' || !dictionary[key].trim() || placeholders(key) !== placeholders(dictionary[key])) throw Error(language.code + ': invalid value or placeholders for ' + JSON.stringify(key));
    }
  }
  return {languages: languages.length, messages: keys.length};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { const result = await validateTranslations(); console.log(`${result.languages} languages, ${result.messages} messages. All keys and placeholders are valid.`); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
