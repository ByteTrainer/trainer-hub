const fs=require('node:fs'),path=require('node:path');
module.exports=function load(directory=__dirname){
 const languages=JSON.parse(fs.readFileSync(path.join(directory,'languages.json'),'utf8'));
 const namespaces=JSON.parse(fs.readFileSync(path.join(directory,'namespaces.json'),'utf8'));
 const dictionaries={legacy:JSON.parse(fs.readFileSync(path.join(directory,'legacy-aliases.json'),'utf8')),scopes:{}};
 for(const language of languages){
  const code=language.code,common=JSON.parse(fs.readFileSync(path.join(directory,'common',code+'.json'),'utf8'));
  dictionaries[code]={...common};
  for(const [id,slug] of Object.entries(namespaces)){
   const game=JSON.parse(fs.readFileSync(path.join(directory,'games',slug,code+'.json'),'utf8'));
   Object.assign(dictionaries[code],game);
   (dictionaries.scopes[id]??={})[code]={...common,...game};
  }
 }
 return dictionaries;
};
