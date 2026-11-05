const fs=require('node:fs');const path=require('node:path');const assert=require('node:assert/strict');
module.exports=async function validate({win,dataDir,output,setCheat,persistenceOnly=false}){
 const ui=code=>win.webContents.executeJavaScript(code);const wait=ms=>new Promise(r=>setTimeout(r,ms));
 await wait(800);await ui(`window.hub.snapshot()`);
 if(persistenceOnly){const actual=await ui(`(async()=>{const s=await window.hub.snapshot();return s.sound})()`);assert.deepEqual(actual,{enabled:true,volume:17,style:'arcade'});fs.writeFileSync(path.join(output,'sound-persistence-test.json'),JSON.stringify({passed:true,settings:actual},null,2));return;}
 await ui(`globalThis.soundEvents=[];window.hub.onSound(v=>soundEvents.push(v));window.hub.setSound({enabled:true,volume:25,style:'digital'})`);await wait(100);
 const originalOptions=await ui(`(async()=>[...(await window.hub.snapshot()).pedro.options])()`);
 let audible=false;const observer=event=>{if(event.audible)audible=true;};win.webContents.on('audio-state-changed',observer);
 setCheat(0,true);await wait(400);setCheat(0,false);await wait(400);setCheat(0,false);
 const events=await ui(`soundEvents.map(e=>e.kind)`);assert.deepEqual(events,['activate','deactivate']);
 try{setCheat(99,true);assert.fail('Invalid cheat accepted');}catch(e){assert.notEqual(e.message,'Invalid cheat accepted');}
 await wait(200);assert.equal(await ui(`soundEvents.length`),2);
 win.minimize();setCheat(1,true);await wait(250);setCheat(1,false);await wait(250);assert.equal(await ui(`soundEvents.length`),4);win.restore();
 await ui(`window.hub.setSound({enabled:false})`);await wait(100);setCheat(2,true);setCheat(2,false);assert.equal(await ui(`soundEvents.length`),4);assert.equal(await ui(`window.hub.previewSound('activate')`),false);
 await ui(`window.hub.setSound({enabled:true,volume:0})`);await wait(80);setCheat(3,true);setCheat(3,false);assert.equal(await ui(`soundEvents.length`),4);assert.equal(await ui(`window.hub.previewSound('deactivate')`),false);
 for(const style of ['digital','soft','arcade','minimal']){await ui(`window.hub.setSound({enabled:true,volume:17,style:${JSON.stringify(style)}})`);await ui(`window.hub.previewSound('activate')`);await wait(300);await ui(`window.hub.previewSound('deactivate')`);await wait(300);}
 const afterOptions=await ui(`(async()=>[...(await window.hub.snapshot()).pedro.options])()`);assert.deepEqual(afterOptions,originalOptions);
 await ui(`window.hub.setSound({enabled:true,volume:17,style:'arcade'})`);await wait(350);
 const saved=JSON.parse(fs.readFileSync(path.join(dataDir,'preferences.json'),'utf8'));assert.deepEqual(saved.sound,{enabled:true,volume:17,style:'arcade'});
 await ui(`[...document.querySelectorAll('button')].find(b=>b.textContent==='Configurações').click()`);await wait(250);
 const controls=await ui(`({switch:!!document.querySelector('#sound-enabled'),range:document.querySelector('#sound-volume')?.value,styles:document.querySelectorAll('input[name="sound-style"]').length,overflow:document.documentElement.scrollWidth>innerWidth})`);
 assert.equal(controls.switch,true);assert.equal(controls.range,'17');assert.equal(controls.styles,4);assert.equal(controls.overflow,false);
 fs.writeFileSync(path.join(output,'preview-sound.png'),(await win.webContents.capturePage()).toPNG());win.setSize(1024,740);await wait(250);assert.equal(await ui(`document.documentElement.scrollWidth>innerWidth`),false);fs.writeFileSync(path.join(output,'preview-sound-compact.png'),(await win.webContents.capturePage()).toPNG());
 win.webContents.removeListener('audio-state-changed',observer);
 assert.equal(audible,true,'Electron did not report audible Web Audio output');
 fs.writeFileSync(path.join(output,'sound-test.json'),JSON.stringify({passed:true,audible,clickAndShortcutRoute:true,minimizedEvents:true,mute:true,zeroVolume:true,variants:4,previewDoesNotChangeCheats:true,preferencesSaved:true,controls},null,2));
};
