import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import fs from 'node:fs';
const require=createRequire(import.meta.url);
const {MoventureAdapter}=require('../electron/moventure.cjs');

function fixture() {
 const adapter=new MoventureAdapter('',()=>{});
 clearInterval(adapter.timer);
 adapter.setPath('test-game');
 adapter.write=()=>{};
 let state={pid:12345,ready:true,flags:1};
 adapter.readStatus=()=>state;
 return {adapter,setState(value){state=value;}};
}

test('verified live runtime keeps enabled controls without repeated process scans',async()=>{
 const {adapter}=fixture();let scans=0;
 adapter.scanProcesses=async()=>{scans++;return [12345];};
 adapter.processAlive=()=>true;
 try {
  await adapter.connect();adapter.set(0,true);
  for(let i=0;i<20;i++)assert.equal((await adapter.status()).connected,true);
  assert.equal(scans,1);assert.equal(adapter.owned,true);assert.equal(adapter.options[0],true);
 } finally {adapter.close();}
});

test('an inconclusive process scan does not clear the session but confirmed absence does',async()=>{
 const {adapter}=fixture();
 adapter.owned=true;adapter.pid=12345;adapter.options[0]=true;
 adapter.scanProcesses=async()=>null;
 try {
  assert.equal((await adapter.status()).connected,false);
  assert.equal(adapter.owned,true);assert.equal(adapter.options[0],true);
  adapter.scanProcesses=async()=>[];
  await adapter.status();assert.equal(adapter.owned,false);assert.equal(adapter.options[0],false);
 } finally {adapter.close();}
});

test('temporary status loss and access errors preserve ownership; process exit clears it',async()=>{
 const {adapter,setState}=fixture();
 adapter.scanProcesses=async()=>[12345];adapter.processAlive=()=>true;
 try {
  await adapter.connect();adapter.set(0,true);
  setState(null);
  assert.equal((await adapter.status()).connected,false);
  assert.equal(adapter.owned,true);assert.equal(adapter.options[0],true);
  adapter.processAlive=()=>null;
  setState({pid:12345,ready:true,flags:1});
  assert.equal((await adapter.status()).connected,true);
  adapter.processAlive=()=>false;adapter.scanProcesses=async()=>[];
  assert.equal((await adapter.status()).gameRunning,false);
  assert.equal(adapter.owned,false);assert.equal(adapter.options[0],false);
 } finally {adapter.close();}
});

test('a replacement PID never inherits active controls and path changes reset trust',async()=>{
 const {adapter,setState}=fixture();let pid=12345;
 adapter.scanProcesses=async()=>[pid];adapter.processAlive=()=>true;
 try {
  await adapter.connect();adapter.set(0,true);
  adapter.processAlive=()=>false;pid=67890;setState({pid,ready:true,flags:0});
  assert.equal((await adapter.status()).connected,true);
  assert.equal(adapter.owned,false);assert.equal(adapter.options[0],false);
  adapter.setPath('other-game');assert.equal(adapter.verifiedPid,null);assert.equal(adapter.processCache,null);
 } finally {adapter.close();}
});

test('one sharing violation is retried on the next heartbeat; persistent or fatal writes disconnect',()=>{
 const {adapter}=fixture();
 try {
  adapter.owned=true;adapter.options[0]=true;
  adapter.write=()=>{throw Object.assign(Error('busy'),{code:'EBUSY'});};
  adapter.heartbeat();assert.equal(adapter.owned,true);assert.equal(adapter.options[0],true);
  adapter.write=()=>{};adapter.heartbeat();assert.equal(adapter.writeFailures,0);
  adapter.write=()=>{throw Object.assign(Error('busy'),{code:'EACCES'});};
  adapter.heartbeat();assert.equal(adapter.owned,true);
  adapter.heartbeat();assert.equal(adapter.owned,false);assert.equal(adapter.options[0],false);
  adapter.owned=true;adapter.write=()=>{throw Object.assign(Error('disk'),{code:'ENOSPC'});};
  adapter.heartbeat();assert.equal(adapter.owned,false);
 } finally {adapter.close();}
});

test('sharing violations reuse only fresh status; expired, missing and malformed status stay disconnected',()=>{
 const {adapter}=fixture();
 adapter.readStatus=MoventureAdapter.prototype.readStatus;
 const original=fs.readFileSync;
 const text=(age=0)=>`1|12345|${621355968000000000n+BigInt(Date.now()-age)*10000n}|1|1`;
 try {
  fs.readFileSync=()=>text();assert.equal(adapter.readStatus().pid,12345);
  fs.readFileSync=()=>{throw Object.assign(Error('busy'),{code:'EBUSY'});};
  assert.equal(adapter.readStatus().pid,12345);
  adapter.statusText=text(3000);assert.equal(adapter.readStatus(),null);
  adapter.statusText=text();fs.readFileSync=()=>{throw Object.assign(Error('missing'),{code:'ENOENT'});};
  assert.equal(adapter.readStatus(),null);
  fs.readFileSync=()=> 'invalid';assert.equal(adapter.readStatus(),null);
 } finally {fs.readFileSync=original;adapter.close();}
});
