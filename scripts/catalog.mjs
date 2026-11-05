import fs from 'node:fs/promises';import {fetchCatalogPage,mergeCatalog} from '../electron/catalog.mjs';
let list=[{id:557340,name:'My Friend Pedro',header:'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/557340/header.jpg',cover:'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/557340/library_600x900_2x.jpg'}];
for(let start=0;start<600;start+=50){try{const result=await fetchCatalogPage(start);list=mergeCatalog(list,result.games);console.log(`Catálogo: ${list.length} jogos`);}catch(e){console.error(e.message);break;}}
await fs.writeFile(new URL('../assets/catalog.json',import.meta.url),JSON.stringify(list,null,2));
