import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import {normalizeLocale,translate} from '../locales/translate.mjs';
import {validateTranslations} from '../scripts/check-translations.mjs';
const languages=JSON.parse(await fs.readFile(new URL('../locales/languages.json',import.meta.url)));
const dictionaries=require('../locales/load.cjs')();

test('official languages have complete matching catalogs and placeholders',async()=>{
 const result=await validateTranslations();assert.equal(result.languages,languages.length);assert.ok(result.languages>=3);assert.ok(result.messages>700);
 assert.equal(normalizeLocale('en-US',languages),'en');assert.equal(normalizeLocale('es-MX',languages),'es');assert.equal(normalizeLocale('pt-PT',languages),'pt-BR');assert.equal(normalizeLocale('../../invalid',languages),'en');
});

test('translations preserve IDs and values, support dynamic messages and fall back to source text',()=>{
 const en=value=>translate(value,'en',dictionaries),es=value=>translate(value,'es',dictionaries);
 assert.equal(en('Vida ilimitada'),'Unlimited health');assert.equal(es('Vida ilimitada'),'Vida ilimitada');
 assert.equal(en('Pacote 1.0.2 verificado. Atualize a integração com o jogo fechado.'),'Package 1.0.2 verified. Update the integration with the game closed.');
 assert.equal(en('Adicionar My Friend Pedro aos favoritos'),'Add My Friend Pedro to favorites');
 assert.equal(es('Steam · 6 cheats · Pacote 1.0.0'),'Steam · 6 trucos · Paquete 1.0.0');
 assert.equal(en('Alterar atalho: Vida ilimitada, F1'),'Change shortcut: Unlimited health, F1');
 assert.equal(en('definir força'),'set strength');
 assert.equal(en('C:/Games/Shake it Co!/game.dll'),'C:/Games/Shake it Co!/game.dll');
 assert.equal(en('Unknown future package label'),'Unknown future package label');
 assert.equal(en('  Configurações  '),'  Settings  ');
 assert.equal(translate('Quantidade definida para {0}.','es',dictionaries,{0:0}),'Cantidad definida en 0.');
 const node={type:'button'};assert.equal(en(node),node);assert.equal(en(false),false);assert.equal(en(0),0);
});

test('translation checker rejects missing entries and altered placeholders',async()=>{
 const directory=await fs.mkdtemp(path.join(os.tmpdir(),'byte-language-check-'));
 try{
  await fs.writeFile(path.join(directory,'languages.json'),JSON.stringify([{code:'pt-BR',name:'Português'},{code:'en',name:'English'}]));
  await fs.mkdir(path.join(directory,'common'));
  await fs.writeFile(path.join(directory,'namespaces.json'),'{}');
  await fs.writeFile(path.join(directory,'common/pt-BR.json'),JSON.stringify({'Value {0}':'Valor {0}'}));
  await fs.writeFile(path.join(directory,'common/en.json'),JSON.stringify({'Value {0}':'Value {0}'}));
  await fs.writeFile(path.join(directory,'common/pt-BR.json'),'{}');
  await assert.rejects(validateTranslations(directory),/missing/);
  await fs.writeFile(path.join(directory,'common/pt-BR.json'),JSON.stringify({'Value {0}':'Valor'}));
  await assert.rejects(validateTranslations(directory),/placeholders/);
 }finally{await fs.rm(directory,{recursive:true,force:true});}
});

test('English keys resolve within game namespaces and older signed package labels remain translated',()=>{
 assert.equal(translate('Unlimited health','pt-BR',dictionaries),'Vida ilimitada');
 assert.equal(translate('Configurações','en',dictionaries),'Settings');
 const games=Object.keys(dictionaries.scopes);
 assert.ok(games.includes('2328750'));assert.ok(games.includes('3578070'));
 assert.equal(translate('Madeira de Lei','en',dictionaries,undefined,2328750),'Hardwood');
 assert.equal(translate('Hardwood','pt-BR',dictionaries,undefined,2328750),'Madeira de Lei');
 assert.equal(translate('Unknown future label','es',dictionaries),'Unknown future label');
 assert.equal(translate('Amount set to {0}.','es',dictionaries,{0:0}),'Cantidad definida en 0.');
});
