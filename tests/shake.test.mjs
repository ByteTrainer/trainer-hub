import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createRequire} from 'node:module';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
const require=createRequire(import.meta.url);
const {definition}=require('../electron/package-definition.cjs');
const {ManagedParametersAdapter,normalizeResourceDrafts}=require('../electron/managed-parameters.cjs');
const spec=definition(require('../resources/shake/game.json'));
const run=promisify(execFile);

test('currency definitions preserve set operations and independent drafts, including zero',()=>{
 assert.equal(spec.commands.length,13);
 assert.equal(spec.commands[0].operation,'set');
 assert.equal(spec.exe,'Shake it Co!.exe');
 assert.deepEqual(normalizeResourceDrafts(spec,{byCommand:{coins:{resource:1,amount:0},souls:{resource:2,amount:250}}}),{coins:{resource:1,amount:0},souls:{resource:2,amount:250},chips:{resource:3,amount:100}});
 const legacy=normalizeResourceDrafts(definition(require('../resources/universe/game.json')),{resource:70,amount:-5});
 assert.deepEqual(legacy.resource,{resource:70,amount:-5});
 for(const exe of ['../bad.exe','folder/game.exe','bad!.exe/other.exe'])assert.throws(()=>definition({...spec,exe}));
 assert.throws(()=>definition({...spec,commands:spec.commands.map((command,index)=>index===0?{...command,operation:'execute'}:command)}));
 const adapter=new ManagedParametersAdapter('',()=>{},spec);
 try{
  adapter.validateResource('coins',1,0);
  adapter.validateResource('souls',2,999999999999);
  assert.throws(()=>adapter.validateResource('coins',2,1));
  for(const value of [-1,1000000000000,NaN,1.5])assert.throws(()=>adapter.validateResource('coins',1,value));
 }finally{adapter.close();}
});

const managed=process.env.SHAKE_MANAGED||'C:/Program Files (x86)/Steam/steamapps/common/Shake it Co!/Shake it Co!_Data/Managed';
test('native Shake services set balances once and preserve progression and perfect chest timing',{skip:process.platform!=='win32'||!fs.existsSync(path.join(managed,'ShakeItCo.Core.dll'))},async()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-shake-native-'));
 try{
  const compiler=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
  const output=path.join(folder,'RuntimeTest.exe');
  const references=['mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','System.Numerics.dll','ShakeItCo.Core.dll'].map(file=>'/r:'+path.join(managed,file));
  await run(compiler,['/nologo','/noconfig','/target:exe','/nostdlib',...references,'/out:'+output,path.resolve('tests/shake-runtime.cs'),path.resolve('native/shake/Runtime.cs')]);
  fs.copyFileSync(path.join(managed,'ShakeItCo.Core.dll'),path.join(folder,'ShakeItCo.Core.dll'));
  fs.writeFileSync(path.join(folder,'RuntimeTest.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm:'net8.0',framework:{name:'Microsoft.NETCore.App',version:'8.0.0'}}}));
  const result=await run('dotnet',[output],{env:{...process.env,TEMP:folder,TMP:folder}});
  assert.match(result.stdout,/Shake runtime checks passed/);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});

test('a changed game dependency disables installation before the integration runs',async()=>{
 const {createHash}=await import('node:crypto');
 const {IntegrationHealth,DEFINITIONS}=require('../electron/integration-health.cjs');
 const {installDefinition}=require('../electron/package-definition.cjs');
 const hash=value=>createHash('sha256').update(value).digest('hex');
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'byte-shake-health-'));
 try{
  const game=path.join(root,'game'),resources=path.join(root,'resources'),managedFolder=path.join(game,spec.base);
  fs.mkdirSync(managedFolder,{recursive:true});fs.mkdirSync(resources);
  fs.writeFileSync(path.join(game,spec.exe),'');
  fs.writeFileSync(path.join(managedFolder,spec.target),'original');
  fs.writeFileSync(path.join(managedFolder,'ShakeItCo.Core.dll'),'known core');
  fs.writeFileSync(path.join(resources,'recipe.json'),JSON.stringify({gameId:spec.gameId,release:1,version:'1.0.0',supportedOriginalHashes:[hash('original')],cheats:spec.commands.map(command=>command.id),dependencies:[{file:'ShakeItCo.Core.dll',sha256:hash('known core')}]}));
  DEFINITIONS[spec.gameId]=installDefinition(spec);
  const health=new IntegrationHealth(spec.gameId,resources);health.setPath(game);
  assert.equal((await health.inspect()).canInstall,true);
  fs.writeFileSync(path.join(managedFolder,'ShakeItCo.Core.dll'),'new core');
  const changed=await health.inspect(true);
  assert.equal(changed.state,'unvalidated');assert.equal(changed.canInstall,false);assert.equal(changed.canConnect,false);
 }finally{fs.rmSync(root,{recursive:true,force:true});}
});
