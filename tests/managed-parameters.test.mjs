import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
const require=createRequire(import.meta.url);
const {definition}=require('../electron/package-definition.cjs');
const {ManagedParametersAdapter,normalizeParameters}=require('../electron/managed-parameters.cjs');
const spec=definition(require('../resources/universe/game.json'));
const epoch=621355968000000000n;
const run=promisify(execFile);

test('native runtime restores movement and pause state, clamps resources and blocks both respawn paths',{skip:process.platform!=='win32'},async()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-native-'));
 try{
  const compiler=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
  const output=path.join(folder,'NativeTest.exe');
  await run(compiler,['/nologo','/r:System.Core.dll','/out:'+output,path.resolve('tests/universe-runtime.cs'),path.resolve('native/universe/Runtime.cs'),path.resolve('native/universe/AfkFishing.cs')]);
  const result=await run(output,[],{env:{...process.env,TEMP:folder,TMP:folder}});
  assert.match(result.stdout,/Native runtime checks passed/);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});

test('parameterized packages validate types, resource IDs and numeric bounds',()=>{
 assert.equal(spec.commands.length,21);
 assert.deepEqual(spec.commands.slice(-3).map(({id,index})=>[id,index]),[['kill-player',18],['instant-deposit',19],['afk-fishing',20]]);
 assert.equal(spec.commands.find(c=>c.id==='range').max,100);
 assert.ok(spec.commands.find(c=>c.id==='resource').choices.length>100);
 const choices=spec.commands.find(c=>c.id==='resource').choices;
 for(const [id,name] of [[70,'Hardwood'],[124,'Fish Oil'],[64,'Corrupted Ruby']])assert.equal(choices.find(choice=>choice.id===id).name,name);
 for(const mutate of [value=>value.commands[4].max=Infinity,value=>value.commands[4].default=30,value=>value.commands[1].choices.push(value.commands[1].choices[0]),value=>value.exe='../game.exe']){
  const invalid=structuredClone(spec);mutate(invalid);assert.throws(()=>definition(invalid));
 }
 const previous=normalizeParameters(spec,{damage:3,range:101});
 assert.equal(previous.damage,3);assert.equal(previous.range,2);
});

test('managed parameters persist disabled choices and resource actions require a matching acknowledgement',async()=>{
 const adapter=new ManagedParametersAdapter('',()=>{},spec);
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-parameters-'));
 adapter.folder=folder;
 try{
  adapter.pid=1234;adapter.owned=true;
  adapter.setParameter('range',100);
  assert.equal(adapter.parameters.range,100);
  assert.throws(()=>adapter.setParameter('range',100.1));
  assert.throws(()=>adapter.setParameter('damage',NaN));
  const resource=spec.commands.find(c=>c.id==='resource').choices[0].id;
  const writeStatus=(ack='',applied=0,session=adapter.session)=>fs.writeFileSync(path.join(folder,'status.txt'),['2',1234,epoch+BigInt(Date.now())*10000n,1,0,session,ack,applied,''].join('|'));
  writeStatus();
  const request=adapter.resource('resource',resource,-12);
  await assert.rejects(adapter.resource('resource',resource,1),/Wait/);
  const parts=fs.readFileSync(path.join(folder,'resource.txt'),'utf8').split('|');
  assert.equal(parts[4],String(resource));assert.equal(parts[5],'-12');
  writeStatus(parts[3],-12);
  assert.equal(await request,-12);
  assert.equal(adapter.pendingAction,false);
  await assert.rejects(adapter.resource('resource',-1,10));
  await assert.rejects(adapter.resource('resource',resource,0));
  adapter.set(4,true);
  const wire=fs.readFileSync(path.join(folder,'settings.txt'),'utf8').split('|');
  assert.equal(Number(wire[2]),16);assert.equal(wire[4].split(',')[6],'100');
  assert.throws(()=>adapter.set(1,true),/Apply/);
 }finally{adapter.close();fs.rmSync(folder,{recursive:true,force:true});}
});
