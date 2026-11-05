const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
module.exports=async({win,output})=>{
 const ui=code=>win.webContents.executeJavaScript(code),wait=ms=>new Promise(r=>setTimeout(r,ms));await wait(1000);
 async function waitFor(selector){for(let i=0;i<80;i++){if(await ui(`!!document.querySelector(${JSON.stringify(selector)})`))return;await wait(250);}throw new Error('Controle não apareceu: '+selector);}
 const before=await ui(`window.hub.snapshot()`);assert.equal(before.games.filter(g=>g.supported).length,2);assert.equal(before.solcesto.installed,true);
 await waitFor('[aria-label^="Abrir Sol Cesto"]');await ui(`document.querySelector('[aria-label^="Abrir Sol Cesto"]').click()`);await waitFor('#sol-gold');
 assert.equal(await ui(`document.querySelectorAll('[id^="cheat-2738490-"]').length`),4);assert.equal(await ui(`document.querySelector('#sol-gold').disabled`),false);
 await ui(`window.hub.connect(2738490)`);
 await ui(`window.hub.cheat(0,true,2738490)`);await ui(`window.hub.cheat(1,true,2738490)`);await ui(`window.hub.cheat(2,true,2738490)`);await wait(700);
 const active=await ui(`window.hub.snapshot()`);assert.equal(active.activeId,2738490);assert.equal(active.pedro.owned,false);assert.deepEqual(active.solcesto.options,[true,true,true,false]);
 assert.equal(await ui(`window.hub.cheat(0,true,557340).then(()=>false,()=>true)`),true);
 if(active.solcesto.ready){const gold=active.solcesto.values.gold;assert.equal(await ui(`window.hub.gold(234)`),234);await ui(`window.hub.gold(${gold})`);}
 await wait(500);assert.equal(await ui(`document.documentElement.scrollWidth>innerWidth`),false);fs.writeFileSync(path.join(output,'preview-solcesto.png'),(await win.webContents.capturePage()).toPNG());
 win.setSize(1024,740);await wait(350);assert.equal(await ui(`document.documentElement.scrollWidth>innerWidth`),false);fs.writeFileSync(path.join(output,'preview-solcesto-compact.png'),(await win.webContents.capturePage()).toPNG());
 await ui(`window.hub.disconnect(2738490)`);await wait(600);const off=await ui(`window.hub.snapshot()`);assert.deepEqual(off.solcesto.options,[false,false,false,false]);assert.equal(off.solcesto.owned,false);
 await ui(`document.querySelector('.back').click()`);await waitFor('[aria-label^="Abrir My Friend Pedro"]');await ui(`document.querySelector('[aria-label^="Abrir My Friend Pedro"]').click()`);await waitFor('#cheat-557340-7');assert.equal(await ui(`document.querySelectorAll('.cheat-row').length`),8);
 fs.writeFileSync(path.join(output,'sol-test.json'),JSON.stringify({passed:true,supported:2,solControls:8,pedroControls:8,session:true,gameConnected:active.solcesto.connected,goldAcknowledged:!!active.solcesto.ready,crossGameCommandsRejected:true,disconnect:true,overflow:false},null,2));
};
