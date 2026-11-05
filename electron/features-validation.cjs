const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
module.exports=async({win,overlay,probe,output})=>{
 const ui=code=>win.webContents.executeJavaScript(code),wait=ms=>new Promise(r=>setTimeout(r,ms)),folder=path.join(output,'.impeccable/review');fs.mkdirSync(folder,{recursive:true});
 const report={passed:false};const stage=value=>fs.writeFileSync(path.join(output,'features-stage.json'),JSON.stringify({...report,stage:value},null,2));
 async function until(fn,timeout=15000){const end=Date.now()+timeout;while(Date.now()<end){if(await fn())return;await wait(200);}throw Error('Timed out');}
 await until(()=>ui(`!!document.querySelector('[aria-label^="Abrir Sol Cesto"]')`));
 await ui(`document.querySelector('[aria-label^="Abrir Sol Cesto"]').click()`);await until(()=>ui(`!!document.querySelector('#game-shortcuts')`));
 await ui(`window.hub.setBinding(2738490,'health','F10')`);let snap=await ui('window.hub.snapshot()');assert.equal(snap.hotkeys[2738490].health,'F10');assert.equal(snap.hotkeys[557340].health,'F1');
 assert.equal(await ui(`window.hub.setBinding(2738490,'health','F2').then(()=>false,()=>true)`),true);
 await ui(`window.hub.setOverlay(2738490,true)`);await ui('window.hub.connect(2738490)');
 await wait(300);report.hotkeys=true;report.perGame=true;report.duplicateRejected=true;
 stage('ready-for-game');
 await until(async()=>{const s=await ui('window.hub.snapshot()');return s.solcesto.connected;},180000);
 report.bridge=true;stage('game-connected');
 await until(async()=>{const s=await ui('window.hub.snapshot()');return s.solcesto.ready;},240000);
 const before=(await ui('window.hub.snapshot()')).solcesto.values;report.before=before;
 for(const[k,v]of [['strength',7],['magic',5],['inventory',6],['gold',234]]){assert.equal(await ui(`window.hub.solAction('${k}',${v})`),v);}
 await ui(`window.hub.cheat(3,true,2738490)`);await wait(600);report.after=(await ui('window.hub.snapshot()')).solcesto.values;assert.equal(report.after.strength,7);assert.equal(report.after.magic,5);assert.equal(report.after.inventory,6);assert.equal(report.after.gold,234);assert.equal(report.after.poison,0);await ui(`window.hub.cheat(3,false,2738490)`);
 assert.equal(await ui(`window.hub.cheat(0,true,557340).then(()=>false,()=>true)`),true);report.liveActions=true;report.crossGameRejected=true;
 for(const[k,v]of [['strength',before.strength],['magic',before.magic],['gold',before.gold]])await ui(`window.hub.solAction('${k}',${v})`);
 await wait(400);await ui(`document.querySelector('#content').scrollTo(0,0)`);fs.writeFileSync(path.join(folder,'desktop.png'),(await win.webContents.capturePage()).toPNG());
 await ui(`document.querySelector('#game-shortcuts').scrollIntoView()`);await wait(150);fs.writeFileSync(path.join(folder,'shortcuts.png'),(await win.webContents.capturePage()).toPNG());
 win.setSize(1024,740);await wait(250);assert.equal(await ui('document.documentElement.scrollWidth>innerWidth'),false);fs.writeFileSync(path.join(folder,'compact.png'),(await win.webContents.capturePage()).toPNG());win.setSize(1360,900);
 stage('native-overlay-test');
 await until(()=>overlay.win?.isVisible(),180000);await wait(500);report.overlayBounds=overlay.win.getBounds();report.overlayShown=true;
 assert.equal(await overlay.win.webContents.executeJavaScript(`document.querySelectorAll('.cheat-row').length`),4);assert.equal(await overlay.win.webContents.executeJavaScript(`document.documentElement.scrollWidth>innerWidth`),false);fs.writeFileSync(path.join(folder,'overlay.png'),(await overlay.win.webContents.capturePage()).toPNG());
 assert.equal(await overlay.win.webContents.executeJavaScript(`window.hub.launch(2738490).then(()=>false,()=>true)`),true);report.overlayPermissions=true;
 const overlayUI=code=>overlay.win.webContents.executeJavaScript(code);await overlayUI(`(()=>{const input=document.querySelector('#sol-strength'),setter=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;input.focus();setter.call(input,'321');input.dispatchEvent(new Event('input',{bubbles:true}));})()`);await wait(4500);assert.equal(await overlayUI(`document.querySelector('#sol-strength').value`),'321');await overlayUI(`document.querySelector('#sol-strength').blur()`);report.draftSurvivedRefresh=true;stage('overlay-visible');await until(()=>!overlay.win.isVisible(),180000);report.overlayClosed=true;await wait(800);report.focusWindow=await probe.request({op:'foreground'});report.focusReturned=report.focusWindow?.exe?.toLowerCase().endsWith('solcesto.exe');stage('focus-result');assert.equal(report.focusReturned,true);
 await wait(3000);stage('hotkey-test');await until(async()=>(await ui('window.hub.snapshot()')).solcesto.options[0],180000);report.customKeyActivated=true;
 await ui('window.hub.cheat(0,false,2738490)');await ui('window.hub.disconnect(2738490)');
 report.passed=true;stage('complete');fs.writeFileSync(path.join(output,'features-test.json'),JSON.stringify(report,null,2));
};
