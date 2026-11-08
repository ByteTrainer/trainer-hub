// Package text is signed data, never markup or executable code.
const keysFor=commands=>[...new Set(commands.flatMap(command=>[command.name,command.description,...(command.choices||[]).map(choice=>choice.name)]))].sort();
const placeholders=value=>[...value.matchAll(/\{\d+\}/g)].map(match=>match[0]).sort().join(',');
function packageTranslations(value,commands){
 if(value===undefined)return undefined; // Older signed packages remain valid.
 const keys=keysFor(commands),allowed=new Set(keys);
 if(!value||typeof value!=='object'||Array.isArray(value)||!Object.hasOwn(value,'en')||Object.keys(value).length>32||JSON.stringify(value).length>1024*1024)throw Error('Invalid package translations.');
 const result=[];
 for(const [locale,messages] of Object.entries(value)){
  if(!/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(locale)||locale.length>35||Intl.getCanonicalLocales(locale)[0]!==locale||!messages||typeof messages!=='object'||Array.isArray(messages)||Object.keys(messages).length!==keys.length)throw Error('Invalid package translations.');
  for(const [key,text] of Object.entries(messages)){
   if(!allowed.has(key)||['__proto__','constructor','prototype'].includes(key)||typeof text!=='string'||!text.trim()||text.length>2048||/[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]/.test(text)||placeholders(key)!==placeholders(text)||(locale==='en'&&text!==key))throw Error('Invalid package translation message.');
  }
  result.push([locale,Object.fromEntries(keys.map(key=>[key,messages[key]]))]);
 }
 return Object.fromEntries(result);
}
module.exports={keysFor,packageTranslations};
