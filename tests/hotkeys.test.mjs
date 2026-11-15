import test from 'node:test';import assert from 'node:assert/strict';import{createRequire}from'node:module';
import {eventKey} from '../src/hotkey-event.js';
test('keyboard capture supports physical keys and events without code',()=>{assert.equal(eventKey({code:'F10',key:'F10'}),'F10');assert.equal(eventKey({key:'F10'}),'F10');assert.equal(eventKey({code:'KeyW',key:'w',altKey:true}),'Alt+W');assert.equal(eventKey({code:'Numpad4',key:'4',ctrlKey:true}),'Control+num4');assert.equal(eventKey({code:'Unidentified',key:'Unidentified'}),null);});
const require=createRequire(import.meta.url),{accelerator,defaults,validateMap,normalizeAll,ShortcutRegistry}=require('../electron/hotkeys.cjs');
test('hotkeys canonicalize modifiers and reject duplicate or unsafe combinations',()=>{assert.equal(accelerator('shift+ctrl+f10'),'Control+Shift+F10');assert.equal(accelerator('alt+w'),'Alt+W');assert.equal(accelerator('num4'),'num4');assert.equal(accelerator(null),null);for(const value of ['W','ctrlOops+F1','Alt+F4','Control+Alt+Delete','Control+Control+X'])assert.throws(()=>accelerator(value));assert.throws(()=>validateMap(2738490,{health:'F2'}),/assigned/);assert.throws(()=>validateMap(2738490,{health:'Alt+W'}),/assigned/);assert.throws(()=>validateMap(2738490,{unknown:'F10'}));});
test('bindings persist independently per game, disabled shortcuts remain disabled',()=>{const prefs=normalizeAll();prefs[2738490]=validateMap(2738490,{...prefs[2738490],health:'F10',gold:null});const reloaded=normalizeAll(JSON.parse(JSON.stringify(prefs)));assert.equal(reloaded[2738490].health,'F10');assert.equal(reloaded[2738490].gold,null);assert.equal(reloaded[557340].health,'F1');assert.deepEqual(normalizeAll({2738490:{bad:true}})[2738490],defaults(2738490));});
test('shortcut capture pauses the listener and restores the latest bindings',()=>{
 let active=new Map(),hits=0;
 const registry=new ShortcutRegistry({setBindings:bindings=>{active=new Map(bindings);}});
 registry.replace([['F5',()=>hits++]]);
 active.get('F5')();assert.equal(hits,1);
 registry.pause();assert.equal(active.size,0);
 registry.replace([['Alt+W',()=>hits++]]);assert.equal(active.size,0);
 registry.resume();assert.deepEqual([...active.keys()],['Alt+W']);
 active.get('Alt+W')();assert.equal(hits,2);
 registry.clear();assert.equal(active.size,0);
});
test('Windows bindings distinguish modifiers, navigation keys and the number pad',()=>{
 const {bindingCode}=require('../electron/keyboard-listener.cjs');
 assert.equal(bindingCode('F5'),0x74);
 assert.equal(bindingCode('Alt+W'),0x257);
 assert.equal(bindingCode('Control+Shift+F24'),0x587);
 assert.equal(bindingCode('Control+End'),0x123);
 assert.equal(bindingCode('Control+num1'),0x161);
});
