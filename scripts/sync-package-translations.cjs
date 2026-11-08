const fs=require('node:fs'),path=require('node:path');
const {keysFor,packageTranslations}=require('../electron/package-translations.cjs');
const {definition}=require('../electron/package-definition.cjs');
function sync(folder){
 const file=path.join(folder,'game.json');
 if(!fs.existsSync(file))return;
 const raw=JSON.parse(fs.readFileSync(file,'utf8'));
 delete raw.translations; // Regenerate after adding/removing controls; never validate stale generated keys.
 const spec=definition(raw),catalogs=require('../locales/load.cjs')(),languages=require('../locales/languages.json');
 const keys=keysFor(spec.commands),translations={};
 for(const {code} of languages){
  const catalog=catalogs.scopes[spec.gameId]?.[code]||catalogs[code];
  translations[code]=Object.fromEntries(keys.map(key=>{
   if(!Object.hasOwn(catalog,key))throw Error('Missing '+code+' package translation for '+spec.gameId+': '+key);
   return [key,catalog[key]];
  }));
 }
 raw.translations=packageTranslations(translations,spec.commands);
 const text=JSON.stringify(raw,null,2)+'\n';
 if(fs.readFileSync(file,'utf8')!==text)fs.writeFileSync(file,text);
 return raw.translations;
}
module.exports={sync};
if(require.main===module)sync(path.resolve(process.argv[2]));
