import {load} from 'cheerio';
export const PEDRO_ID=557340;
export const SOLCESTO_ID=2738490;
export function parseSearch(html) {
 const $=load(html); const games=[];
 $('a.search_result_row').each((_,el)=>{const id=Number($(el).attr('data-ds-appid')?.split(',')[0]); const name=$(el).find('.title').text().trim(); if(Number.isInteger(id)&&id>0&&name) games.push({id,name,header:`https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/${id}/header.jpg`,cover:`https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/${id}/library_600x900_2x.jpg`});});
 return games;
}
export function mergeCatalog(...groups) {
 const map=new Map(); for(const group of groups)for(const game of group){if(Number.isInteger(game.id)&&game.id>0&&typeof game.name==='string'&&game.name.trim())map.set(game.id,{...map.get(game.id),...game,supported:[PEDRO_ID,SOLCESTO_ID].includes(game.id)});}
 return [...map.values()].sort((a,b)=>Number(b.supported)-Number(a.supported)||a.name.localeCompare(b.name,'pt-BR'));
}
export async function fetchCatalogPage(start=0,term='') {
 const url=new URL('https://store.steampowered.com/search/results/');
 for(const [key,value]of Object.entries({query:term,term,start,count:50,infinite:1,category1:998,json:1,cc:'BR',l:'portuguese'}))url.searchParams.set(key,String(value));
 const response=await fetch(url,{signal:AbortSignal.timeout(18000)}); if(!response.ok)throw new Error('A Steam não respondeu. O catálogo salvo continua disponível.');
 const data=await response.json(); return {games:parseSearch(data.results_html||''),total:Number(data.total_count)||0};
}
