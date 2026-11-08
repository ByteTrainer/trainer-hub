const escapePattern = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const cache = new WeakMap();
const folded = new WeakMap();
function lookup(dictionary, source) {
  if (Object.hasOwn(dictionary, source)) return dictionary[source];
  if (!/^\p{Ll}/u.test(source)) return undefined;
  if (!folded.has(dictionary)) folded.set(dictionary, new Map(Object.entries(dictionary).map(([key,value])=>[key.toLocaleLowerCase('pt-BR'),value])));
  const found=folded.get(dictionary).get(source.toLocaleLowerCase('pt-BR'));
  return found ? found[0].toLocaleLowerCase()+found.slice(1) : undefined;
}

export function normalizeLocale(value, languages) {
  if (typeof value !== 'string') return 'pt-BR';
  const exact = languages.find(language => language.code.toLowerCase() === value.toLowerCase());
  if (exact) return exact.code;
  const base = value.split('-')[0].toLowerCase();
  return languages.find(language => language.code.split('-')[0].toLowerCase() === base)?.code || 'pt-BR';
}

function templates(dictionary) {
  if (cache.has(dictionary)) return cache.get(dictionary);
  const entries = Object.keys(dictionary).filter(key => /\{\d+\}/.test(key)).map(key => {
    const indices = [];
    const parts = key.split(/(\{\d+\})/).map(part => {
      if (/^\{\d+\}$/.test(part)) { indices.push(Number(part.slice(1, -1))); return '(.+?)'; }
      return escapePattern(part);
    });
    return {key, indices, pattern: new RegExp('^' + parts.join('') + '$')};
  }).sort((a, b) => b.key.replace(/\{\d+\}/g, '').length - a.key.replace(/\{\d+\}/g, '').length);
  cache.set(dictionary, entries);
  return entries;
}

export function translate(value, locale, dictionaries, parameters) {
  if (typeof value !== 'string' || !value.trim()) return value;
  const dictionary = dictionaries[locale] || dictionaries['pt-BR'];
  if (!dictionary) return value;
  const leading = value.match(/^\s*/)[0], trailing = value.match(/\s*$/)[0];
  const source = value.trim().replace(/\s+/g, ' ');
  let message = lookup(dictionary, source), args = parameters;
  if (message === undefined && locale !== 'pt-BR') {
    for (const entry of templates(dictionary)) {
      const match = entry.pattern.exec(source);
      if (!match) continue;
      args = {};
      entry.indices.forEach((index, offset) => { const captured=match[offset + 1]; args[index] = captured===source?captured:translate(captured,locale,dictionaries); });
      message = dictionary[entry.key];
      break;
    }
  }
  if (message === undefined) return value;
  return leading + message.replace(/\{(\d+)\}/g, (token, index) => args?.[index] ?? token) + trailing;
}
