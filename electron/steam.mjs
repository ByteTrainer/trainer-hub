import fs from 'node:fs/promises'; import path from 'node:path';
export function parseVdfFields(text){return Object.fromEntries([...text.matchAll(/"(appid|name|installdir|path)"\s+"([^"\r\n]*)"/g)].map(m=>[m[1],m[2].replace(/\\\\/g,'\\')]));}
export function libraryPaths(text){return [...text.matchAll(/"path"\s+"([^"\r\n]+)"/g)].map(m=>m[1].replace(/\\\\/g,'\\'));}
export async function scanSteam(root){
 const roots=new Set([root]);try{for(const p of libraryPaths(await fs.readFile(path.join(root,'steamapps','libraryfolders.vdf'),'utf8')))roots.add(p);}catch{}
 const installed=[];for(const library of roots){let names=[];try{names=await fs.readdir(path.join(library,'steamapps'));}catch{continue;}
 for(const name of names.filter(n=>/^appmanifest_\d+\.acf$/.test(n))){try{const fields=parseVdfFields(await fs.readFile(path.join(library,'steamapps',name),'utf8'));const id=Number(fields.appid); if(!id||!fields.name||!fields.installdir||fields.name==='Steamworks Common Redistributables')continue;const gamePath=path.resolve(library,'steamapps','common',fields.installdir);const common=path.resolve(library,'steamapps','common')+path.sep;if(!gamePath.toLowerCase().startsWith(common.toLowerCase()))continue;installed.push({id,name:fields.name,installed:true,path:gamePath,header:`https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/${id}/header.jpg`,cover:`https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/${id}/library_600x900_2x.jpg`});}catch{}}
 }return installed;
}
