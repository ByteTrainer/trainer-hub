import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {createTranslationStore} from '../locales/translation-store.mjs';
const require=createRequire(import.meta.url);
const {definition}=require('../electron/package-definition.cjs');
const {keysFor}=require('../electron/package-translations.cjs');
const {verifyEnvelope,validatePackage,IntegrationUpdates,digest}=require('../electron/integration-updates.cjs');
const spec=JSON.parse(fs.readFileSync('resources/tinykin/game.json'));
const languages=require('../locales/languages.json');

test('package translations fix Tinykin descriptions and new controls with an older Hub dictionary',()=>{
 const base={en:{'Unlimited health':'Unlimited health'},'pt-BR':{'Unlimited health':'Vida ilimitada','Change shortcut: {0}, {1}':'Alterar atalho: {0}, {1}'},es:{'Unlimited health':'Vida ilimitada'},scopes:{}};
 const store=createTranslationStore(base,languages,'pt-BR');
 const command=spec.commands[6];
 assert.equal(store.translate(command.name,undefined,spec.gameId),'Pollen tracker');
 store.setIntegrationTranslations({[spec.gameId]:definition(spec).translations});
 for(const command of spec.commands){
  assert.equal(store.translate(command.name,undefined,spec.gameId),spec.translations['pt-BR'][command.name]);
  assert.equal(store.translate(command.description,undefined,spec.gameId),spec.translations['pt-BR'][command.description]);
 }
 assert.equal(store.translate(command.name),command.name,'package text does not change global app text');
 assert.equal(store.translate(command.name,undefined,400080),command.name,'package text does not change another game');
 assert.equal(store.translate('Change shortcut: Pollen tracker, F7',undefined,spec.gameId),'Alterar atalho: Rastreador de pólen, F7','shortcut accessibility labels use the same game scope');
 store.setLanguage('es-MX');assert.equal(store.translate(command.name,undefined,spec.gameId),'Rastreador de polen');
 store.setLanguage('en');assert.equal(store.translate(command.name,undefined,spec.gameId),command.name);
 assert.equal(base.scopes[spec.gameId],undefined,'bundled dictionaries remain immutable');
});

test('translation changes invalidate cached lookups and notify React even when locale is unchanged',()=>{
 const store=createTranslationStore({en:{},'pt-BR':{},scopes:{}},languages,'pt-BR');
 let notifications=0;const off=store.subscribe(()=>notifications++);
 const first={1599020:{en:{'New cheat':'New cheat'},'pt-BR':{'New cheat':'Novo cheat'}}};
 const before=store.snapshot();store.setIntegrationTranslations(first);
 assert.ok(store.snapshot()>before);assert.equal(store.getLocale(),'pt-BR');assert.equal(store.translate('new cheat',undefined,1599020),'novo cheat');
 const snapshot=store.snapshot();store.setIntegrationTranslations(structuredClone(first));assert.equal(store.snapshot(),snapshot);
 store.setIntegrationTranslations({1599020:{en:{'New cheat':'New cheat'},'pt-BR':{'New cheat':'Cheat corrigido'}}});
 assert.equal(store.translate('new cheat',undefined,1599020),'cheat corrigido');
 store.setIntegrationTranslations({});assert.equal(store.translate('New cheat',undefined,1599020),'New cheat');
 assert.equal(notifications,3);off();store.setLanguage('es');assert.equal(notifications,3);
});

test('definition rejects invalid localization data and continues accepting older packages',()=>{
 const old=structuredClone(spec);delete old.translations;assert.equal(definition(old).translations,undefined);
 for(const alter of [
  s=>s.translations['pt-BR']['App language']='Override app settings',
  s=>delete s.translations.en,
  s=>s.translations.en['Pollen tracker']='Not the source key',
  s=>delete s.translations.es['Pollen tracker'],
  s=>s.translations.es['Pollen tracker']='x'.repeat(2049),
  s=>s.translations['../pt-BR']=s.translations['pt-BR'],
  s=>s.translations.es['Pollen tracker']='Rastreador {0}'
 ]){const changed=structuredClone(spec);alter(changed);assert.throws(()=>definition(changed),/translation/i);}
});

test('new controls regenerate complete translations from canonical source catalogs',()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-package-text-'));
 try{
  const file=path.join(folder,'game.json'),changed=structuredClone(spec);
  changed.commands.push({id:'new-test-control',index:10,type:'toggle',name:'One-hit kills',description:'Pollen tracker',key:'F11'});
  fs.writeFileSync(file,JSON.stringify(changed));
  const messages=require('../scripts/sync-package-translations.cjs').sync(folder);
  assert.equal(messages['pt-BR']['One-hit kills'],'Mortes com um golpe');
  assert.deepEqual(Object.keys(messages.en).sort(),keysFor(changed.commands));
  assert.ok(definition(JSON.parse(fs.readFileSync(file))).translations);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});

test('signed update roundtrip carries translations and rejects changed text',async()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-signed-text-'));
 const pair=crypto.generateKeyPairSync('ed25519');
 const source=path.resolve('resources/tinykin');
 const recipe=JSON.parse(fs.readFileSync(path.join(source,'recipe.json')));
 const names=['recipe.json','game.json','PatchTool.exe','Mono.Cecil.dll','Mono.Cecil.LICENSE.txt',...spec.payload];
 const files=names.map(name=>{const bytes=fs.readFileSync(path.join(source,name));return {name,data:bytes.toString('base64'),sha256:digest(bytes)};});
 const value={kind:'integration',gameId:spec.gameId,release:recipe.release,version:recipe.version,minimumHubVersion:'1.4.0',files};
 const payload=Buffer.from(JSON.stringify(value));
 const envelope={algorithm:'Ed25519',payload:payload.toString('base64'),signature:crypto.sign(null,payload,pair.privateKey).toString('base64')};
 try{
  const bytes=Buffer.from(JSON.stringify(envelope));validatePackage(verifyEnvelope(bytes,pair.publicKey),'1.6.1');
  const updater=new IntegrationUpdates({root:folder,bundled:{},publicKey:pair.publicKey,hubVersion:'1.6.1'});
  await updater.importBytes(bytes);const resolved=await updater.resolve(spec.gameId);
  assert.deepEqual(definition(JSON.parse(fs.readFileSync(path.join(resolved,'game.json')))).translations,spec.translations);
  const changed=JSON.parse(payload);changed.files.find(file=>file.name==='game.json').data=Buffer.from(JSON.stringify({...spec,translations:{}})).toString('base64');
  assert.throws(()=>verifyEnvelope(Buffer.from(JSON.stringify({...envelope,payload:Buffer.from(JSON.stringify(changed)).toString('base64')})),pair.publicKey),/signature/i);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});
