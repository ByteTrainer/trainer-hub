import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
const require=createRequire(import.meta.url),run=promisify(execFile);
const {definition}=require('../electron/package-definition.cjs');
const spec=definition(require('../resources/outofspace/game.json'));

test('Out of Space definitions preserve independent local needs and native currency limits',()=>{
 assert.equal(spec.commands.length,10);assert.equal(spec.gameId,400080);
 assert.equal(spec.commands[3].operation,'set');assert.equal(spec.commands[3].choices[0].id,1);
 assert.deepEqual(spec.commands.map(c=>c.index),[0,1,2,3,4,5,6,7,8,9]);
 assert.match(spec.commands[3].description,/9\.999/);
});

test('Out of Space runtime isolates local characters, clears needs, restores normal behavior and acknowledges currency once',{skip:process.platform!=='win32'},async()=>{
 const folder=fs.mkdtempSync(path.join(os.tmpdir(),'byte-space-native-'));
 try{
  const compiler=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),output=path.join(folder,'RuntimeTest.exe');
  await run(compiler,['/nologo','/out:'+output,path.resolve('tests/outofspace-runtime.cs'),path.resolve('native/outofspace/Runtime.cs')]);
  const result=await run(output,[],{env:{...process.env,TEMP:folder,TMP:folder}});
  assert.match(result.stdout,/Out of Space runtime checks passed/);
 }finally{fs.rmSync(folder,{recursive:true,force:true});}
});
