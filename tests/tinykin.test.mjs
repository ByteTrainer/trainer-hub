import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
const require=createRequire(import.meta.url),run=promisify(execFile);
const spec=require('../electron/package-definition.cjs').definition(require('../resources/tinykin/game.json'));
test('Tinykin package defines ten controls and keeps movement independent of gliding',()=>{
 assert.equal(spec.gameId,1599020);assert.equal(spec.commands.length,10);assert.equal(spec.target,'Game.dll');
 assert.deepEqual(spec.payload,['TinykinTrainer.Runtime.dll','0Harmony.dll']);
 assert.equal(spec.commands[2].type,'multiplier');assert.equal(spec.commands[2].max,5);
 assert.equal(spec.commands[4].id,'infinite-glide');assert.equal(spec.commands[5].id,'level-glide');
});
test('Tinykin runtime isolates controls, preserves fall recovery and freezes only the race timer',{skip:process.platform!=='win32'},async()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-tinykin-'));
 try {
  fs.copyFileSync('resources/tinykin/0Harmony.dll',path.join(folder,'0Harmony.dll'));
  const compiler=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),output=path.join(folder,'RuntimeTest.exe');
  await run(compiler,['/nologo','/r:System.Core.dll','/r:'+path.resolve('resources/tinykin/0Harmony.dll'),'/out:'+output,path.resolve('tests/tinykin-runtime.cs'),path.resolve('native/tinykin/Runtime.cs'),path.resolve('native/tinykin/TimerHooks.cs')]);
  const result=await run(output,[],{env:{...process.env,TEMP:folder,TMP:folder},timeout:30000});assert.match(result.stdout,/Tinykin native runtime and Harmony timer checks passed/);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});

const installed=path.join(process.env['ProgramFiles(x86)']||'C:/Program Files (x86)','Steam/steamapps/common/Tinykin/Tinykin_Data/Managed');

test('Tinykin trackers filter persisted state and switch between real unfinished targets',{skip:process.platform!=='win32'},async()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-tinykin-trackers-'));
 try {
  const compiler=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),output=path.join(folder,'TrackerTest.exe');
  await run(compiler,['/nologo','/r:System.Core.dll','/out:'+output,path.resolve('tests/tinykin-trackers.cs'),path.resolve('native/tinykin/TrackerScanner.cs')]);
  const result=await run(output,[],{timeout:10000});assert.match(result.stdout,/Tinykin tracker filtering, nearest selection, quest resolution and collection transitions passed/);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});
test('Tinykin installation restores the original and never changes the race assembly',{skip:process.platform!=='win32'||!fs.existsSync(path.join(installed,'DLC-Challenges.dll'))},async()=>{
 const {installDefinition}=require('../electron/package-definition.cjs');
 const {DEFINITIONS,IntegrationHealth,sha256}=require('../electron/integration-health.cjs');
 const {installTransaction}=require('../electron/integration-transaction.cjs');
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-tinykin-install-')),resources=path.resolve('resources/tinykin');
 DEFINITIONS[spec.gameId]=installDefinition(spec);
 try {
  const dir=path.join(folder,spec.base);fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(path.join(folder,spec.exe),'');
  const source=path.join(installed,'Game.dll.trainerhub-original');
  fs.copyFileSync(fs.existsSync(source)?source:path.join(installed,'Game.dll'),path.join(dir,'Game.dll'));
  fs.copyFileSync(path.join(installed,'DLC-Challenges.dll'),path.join(dir,'DLC-Challenges.dll'));
  const original=await sha256(path.join(dir,'Game.dll')),race=await sha256(path.join(dir,'DLC-Challenges.dll'));
  const health=new IntegrationHealth(spec.gameId,resources);health.setPath(folder);
  const adapter={gamePath:folder,resources,health,status:async()=>({gameRunning:false})};
  await installTransaction(adapter,'install',stage=>run(path.join(resources,'PatchTool.exe'),['install',stage]));
  assert.equal((await health.inspect(true)).canConnect,true);
  assert.notEqual(await sha256(path.join(dir,'Game.dll')),original);
  assert.equal(await sha256(path.join(dir,'DLC-Challenges.dll')),race);
  await installTransaction(adapter,'restore',()=>{throw Error('Restoration must not call the patch tool');});
  assert.equal(await sha256(path.join(dir,'Game.dll')),original);
  assert.equal(await sha256(path.join(dir,'DLC-Challenges.dll')),race);
  for(const payload of spec.payload)assert.equal(fs.existsSync(path.join(dir,payload)),false);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});
