const {app,BrowserWindow,ipcMain,shell,dialog,globalShortcut,screen}=require('electron');const fs=require('node:fs');const path=require('node:path');const {pathToFileURL}=require('node:url');const {PedroAdapter}=require('./pedro.cjs');
const {execFile}=require('node:child_process');const {promisify}=require('node:util');const run=promisify(execFile);
const {normalizeSound,updateSound,changeCheat}=require('./sound.cjs');
const {SolCestoAdapter}=require('./solcesto.cjs');
const {writeJson}=require('./atomic.cjs');
const {COMMANDS,defaults,validateMap,normalizeAll,ShortcutRegistry}=require('./hotkeys.cjs');
const {updateParameters,normalizeParameters}=require('./sol-parameters.cjs');
const {WindowProbe}=require('./window-probe.cjs');
const {OverlayController}=require('./overlay.cjs');
const {PumpkinHud}=require('./pumpkin-hud.cjs');
const {enhanceAdapter}=require('./integration-adapter.cjs');
const {IntegrationUpdates,endpoint}=require('./integration-updates.cjs');
const {PumpkinAdapter,ID:PUMPKIN_ID,speed:pumpkinSpeed}=require('./pumpkin.cjs');
const {DEFINITIONS}=require('./integration-health.cjs');
const {MoventureAdapter,ID:MOVENTURE_ID}=require('./moventure.cjs');
const {ManagedParametersAdapter,normalizeParameters:normalizeManagedParameters,normalizeResourceDraft}=require('./managed-parameters.cjs');
const {definition,installDefinition}=require('./package-definition.cjs');
const LEGACY_CATALOG='https://bytetrainer.github.io/trainer-integrations/catalog.json';
const OFFICIAL_INTEGRATION_CATALOG=LEGACY_CATALOG.replace('catalog.json','catalog-v2.json');
const managedAdapters=new Map();
const compatibilityTest=process.argv.includes('--compatibility-test'),compatibilityUITest=process.argv.includes('--compatibility-ui-test');
let integrationUpdates,updatesInterval,appUpdates,patchBusy=false,lastCapabilityState='';
const registry=new ShortcutRegistry(globalShortcut),editing=new Set();let overlay,probe,pumpkinHud,captureTimer,captureSender,shortcutError=null;
let pedroAdapter,solAdapter,pumpkinAdapter,activeId=557340,publishing=false;
let soundSaveTimer, publishCatalog=true;
const soundTest=process.argv.includes('--sound-test')||process.argv.includes('--sound-persistence-check');
const solTest=process.argv.includes('--sol-test'),featuresTest=process.argv.includes('--features-test'),overlayTest=process.argv.includes('--overlay-test');
let win,adapter,core,steam,catalog=[],installed=[],prefs={},events=[],dataDir,steamRoot,interval;const root=path.resolve(__dirname,'..');const smoke=process.argv.includes('--smoke-test');const testOutput=app.isPackaged?path.dirname(app.getPath('exe')):root;
if(soundTest||solTest||featuresTest||overlayTest||compatibilityTest||compatibilityUITest)app.setPath('userData',path.join(testOutput,'.validation',compatibilityUITest?'compatibility-ui':compatibilityTest?'compatibility':(featuresTest||overlayTest)?'features-test':solTest?'sol-test':'sound-test'));
const read=(file,fallback)=>{try{return JSON.parse(fs.readFileSync(file,'utf8'));}catch{return fallback;}};
function save(){fs.mkdirSync(dataDir,{recursive:true});writeJson(path.join(dataDir,'preferences.json'),prefs);}
function log(message){events.unshift({message,time:new Date().toISOString()});events=events.slice(0,100);fs.writeFileSync(path.join(dataDir,'activity.json'),JSON.stringify(events));}
function games(){
 const ids=new Set(installed.map(game=>game.id));
 const managed=[...managedAdapters].map(([id,target])=>({id,name:target.definition.name}));
 return core.mergeCatalog(catalog,installed,managed)
  .filter(game=>game.supported||managedAdapters.has(game.id))
  .map(game=>({...game,supported:true,commands:COMMANDS[game.id],installed:ids.has(game.id)||!!prefs.gamePaths[game.id],favorite:prefs.favorites.includes(game.id)}));
}
function find(id){if(!Number.isInteger(id))throw new Error('Jogo inválido.');const game=games().find(g=>g.id===id);if(!game)throw new Error('Jogo fora do catálogo.');return game;}
function gamePath(id){return prefs.gamePaths[id]||installed.find(game=>game.id===id)?.path||null;}
async function scan(){
 installed=await steam.scanSteam(steamRoot);
 for(const id of [557340,2738490,PUMPKIN_ID,...managedAdapters.keys()])getAdapter(id).setPath(gamePath(id));
 return installed;
}
async function snapshot(){return {games:games(),events,pedro:await (soundTest?adapter:pedroAdapter).status(),solcesto:await solAdapter.status(),pumpkin:await pumpkinAdapter.status(),managed:await managedStatuses(),activeId,sound:prefs.sound,hotkeys:prefs.hotkeys,overlayPreferences:prefs.overlays,solParameters:solAdapter.parameters,shortcutError,integrationUpdates:{...integrationUpdates.snapshot(),url:prefs.integrationUpdatesURL||''},gamePaths:prefs.gamePaths,steamRoot,dataDir,version:app.getVersion(),showChangelog:prefs.seenChangelog!==app.getVersion(),updatedAt:prefs.updatedAt||null};}
function getAdapter(id){if(id===557340)return pedroAdapter;if(id===2738490)return solAdapter;if(id===PUMPKIN_ID)return pumpkinAdapter;if(managedAdapters.has(id))return managedAdapters.get(id);throw new Error('Trainer indisponível para este jogo.');}
async function managedStatuses(){
 return Object.fromEntries(await Promise.all([...managedAdapters].map(async([id,target])=>[id,{...await target.status(),commands:COMMANDS[id]}])));
}
function registerManaged(folder,onChange=compatibilityChanged){
 const raw=read(path.join(folder,'game.json'),null);
 if(!raw)return null;
 const spec=definition(raw),id=spec.gameId,previous=managedAdapters.get(id);
 DEFINITIONS[id]??=installDefinition(spec);
 const Driver=spec.driver==='managed-parameters-v1'?ManagedParametersAdapter:MoventureAdapter;
 const target=enhanceAdapter(new Driver(folder,log,spec),id,onChange);
 if(spec.driver==='managed-parameters-v1'){
  target.parameters=normalizeManagedParameters(spec,prefs.managedParameters?.[id]);
  target.resourceDraft=normalizeResourceDraft(spec,prefs.resourceDrafts?.[id]);
 }
 previous?.close();
 managedAdapters.set(id,target);
 if(activeId===id)adapter=target;
 COMMANDS[id]=spec.commands;
 prefs.hotkeys[id]=migrateBindings(id,prefs.hotkeys[id]);
 prefs.overlays[id]={enabled:prefs.overlays[id]?.enabled===true};
 target.setPath(gamePath(id));
 return target;
}
function migrateBindings(id,old){
 const result=defaults(id), taken=new Set();
 for(const key of Object.keys(result))if(Object.hasOwn(old||{},key)){result[key]=old[key];if(result[key])taken.add(result[key]);}
 for(const key of Object.keys(result))if(!Object.hasOwn(old||{},key)&&taken.has(result[key]))result[key]=null;
 try{return validateMap(id,result);}catch{return defaults(id);}
}
function compatibilityChanged(result){log(result.message);if(['removed','outdated','unvalidated','incompatible','recovery-needed','error'].includes(result.state))notifyError(result.message);}
function requireActive(id){if(id!==undefined&&id!==activeId)throw new Error('Conecte o trainer deste jogo primeiro.');return adapter;}
const SHARED=new Set(['snapshot','cheat','combo','gold','sol-action','sol-values','overlay-state','overlay-hide','input-focus','pumpkin-speed','managed-parameter','managed-resource','managed-resource-draft']);
function handle(name,fn){ipcMain.handle(name,async(event,...args)=>{const main=event.sender===win.webContents&&event.senderFrame===win.webContents.mainFrame;if(!main&&!(SHARED.has(name)&&overlay?.isSender(event)))throw new Error('Solicitação não autorizada.');if(name==='input-focus'){if(typeof args[0]!=='boolean')throw new Error('Estado inválido.');args[0]?editing.add(event.sender.id):editing.delete(event.sender.id);try{shortcuts();}catch(e){notifyError(e.message);}return;}return fn(...args);});}
function soundCue(kind){if(win&&!win.isDestroyed()&&prefs.sound.enabled&&prefs.sound.volume>0)win.webContents.send('sound-cue',{kind,settings:prefs.sound});}
function setCheat(index,value,id){requireActive(id);const result=changeCheat(adapter,index,value,soundCue);publish();return result;}
function notifyError(message){if(win&&!win.isDestroyed())win.webContents.send('trainer-error',message);overlay?.error(message);}
function inputFocused(){return [win,overlay?.win].some(w=>w&&!w.isDestroyed()&&w.isFocused()&&editing.has(w.webContents.id));}
async function applySol(kind,value){requireActive(2738490);const values=setSolValues({[kind]:value===undefined?solAdapter.parameters[kind]:value});const result=await solAdapter.apply(kind,values[kind]);soundCue('activate');publish();return result;}
function runCommand(id,command){if(id!==activeId||!adapter.owned)return;if(['toggle','multiplier'].includes(command.type)){try{setCheat(command.index,!adapter.options[command.index],id);}catch(e){notifyError(e.message);}}else if(command.type==='resource')applyManagedResource(id,command.id,adapter.resourceDraft.resource,adapter.resourceDraft.amount).catch(e=>notifyError(e.message));else if(id===2738490&&!solAdapter.action)applySol(command.id).catch(e=>notifyError(e.message));}
function shortcuts(){const pairs=[],id=activeId;if(adapter?.owned){if(!inputFocused())for(const command of COMMANDS[activeId]){if(adapter.capabilities?.[command.id]?.available===false)continue;const key=prefs.hotkeys[activeId][command.id];if(key)pairs.push([key,()=>runCommand(id,command)]);}const key=prefs.hotkeys[activeId].overlay;if(prefs.overlays[activeId].enabled&&key)pairs.push([key,()=>overlay.toggle(false).catch(e=>notifyError(e.message))]);}registry.replace(pairs);shortcutError=null;}
function preferences(){return {hotkeys:prefs.hotkeys,overlayPreferences:prefs.overlays,solParameters:solAdapter.parameters,shortcutError};}
function broadcastPreferences(){if(win&&!win.isDestroyed())win.webContents.send('trainer-settings',preferences());publish();}
function endCapture(){clearTimeout(captureTimer);captureTimer=null;try{registry.resume();shortcuts();}catch(e){shortcutError=e.message;notifyError(e.message);}if(captureSender&&!captureSender.isDestroyed())captureSender.send('capture-ended');captureSender=null;}
function setBindings(id,command,key,reset=false){getAdapter(id);endCapture();const old=prefs.hotkeys[id],next=reset?defaults(id):validateMap(id,{...old,[command]:key});prefs.hotkeys[id]=next;try{shortcuts();save();}catch(e){prefs.hotkeys[id]=old;try{shortcuts();}catch{}throw e;}broadcastPreferences();return next;}
function setSolValues(patch){const old=solAdapter.parameters,next=updateParameters(old,patch);solAdapter.parameters=next;prefs.solParameters=next;try{save();}catch(e){solAdapter.parameters=old;prefs.solParameters=old;throw e;}broadcastPreferences();return next;}
async function overlayPayload(status){return {gameId:activeId,gameName:find(activeId).name,status:{...(status||await adapter.status()),commands:COMMANDS[activeId]},bindings:prefs.hotkeys[activeId],parameters:solAdapter.parameters,overlayShortcut:prefs.hotkeys[activeId].overlay,version:app.getVersion()};}
async function publish(){if(publishing)return;publishing=true;try{const[pedro,solcesto,pumpkin,managed]=await Promise.all([(soundTest?adapter:pedroAdapter).status(),solAdapter.status(),pumpkinAdapter.status(),managedStatuses()]);if(win&&!win.isDestroyed()){const keyState=JSON.stringify([activeId,adapter.owned,Object.entries(adapter.capabilities||{}).map(([id,c])=>[id,c.available])]);if(lastCapabilityState!==keyState){lastCapabilityState=keyState;try{shortcuts();}catch(e){shortcutError=e.message;notifyError(e.message);}}win.webContents.send('status',{pedro,solcesto,pumpkin,managed,...(publishCatalog?{games:games()}:{}),hotkeys:prefs.hotkeys,overlayPreferences:prefs.overlays,activeId,integrationUpdates:{...integrationUpdates.snapshot(),url:prefs.integrationUpdatesURL||''}});publishCatalog=false;overlay?.send(await overlayPayload(activeId===557340?pedro:activeId===PUMPKIN_ID?pumpkin:activeId===2738490?solcesto:managed[activeId]));}}finally{publishing=false;}}
if(!app.requestSingleInstanceLock())app.quit();else{
app.on('second-instance',()=>{if(win){if(win.isMinimized())win.restore();win.focus();}});
app.whenReady().then(async()=>{
 dataDir=app.getPath('userData');fs.mkdirSync(dataDir,{recursive:true});prefs={favorites:[],integrationUpdatesURL:OFFICIAL_INTEGRATION_CATALOG,...read(path.join(dataDir,'preferences.json'),{})};events=read(path.join(dataDir,'activity.json'),[]);
 prefs.gamePaths={...(prefs.pedroPath?{557340:prefs.pedroPath}:{}),...(prefs.solPath?{2738490:prefs.solPath}:{}),...prefs.gamePaths};
 delete prefs.pedroPath;delete prefs.solPath;
 prefs.sound=normalizeSound(prefs.sound);prefs.hotkeys=prefs.hotkeys||{};prefs.overlays=prefs.overlays||{};if(prefs.integrationUpdatesURL===LEGACY_CATALOG)prefs.integrationUpdatesURL=OFFICIAL_INTEGRATION_CATALOG;prefs.solParameters=normalizeParameters(prefs.solParameters);
 core=await import('./catalog.mjs');steam=await import('./steam.mjs');catalog=read(path.join(root,'assets','catalog.json'),[{id:557340,name:'My Friend Pedro'}]);catalog=core.mergeCatalog(catalog,read(path.join(dataDir,'catalog.json'),[]));
 steamRoot=path.join(process.env['ProgramFiles(x86)']||'C:\\Program Files (x86)','Steam');try{const {stdout}=await run('powershell.exe',['-NoProfile','-Command',"(Get-ItemProperty -LiteralPath 'HKCU:\\Software\\Valve\\Steam' -ErrorAction Stop).SteamPath"],{windowsHide:true,timeout:5000});if(stdout.trim())steamRoot=path.resolve(stdout.trim());}catch{}
 pumpkinAdapter=new PumpkinAdapter(path.join(app.isPackaged?process.resourcesPath:path.join(root,'resources'),'pumpkin'),log,require('node:os').tmpdir());
 const resources=app.isPackaged?path.join(process.resourcesPath,'pedro'):path.join(root,'resources','pedro');pedroAdapter=new PedroAdapter(resources,log);solAdapter=new SolCestoAdapter(app.isPackaged?path.join(process.resourcesPath,'solcesto'):path.join(root,'resources','solcesto'),log);const universeResources=path.join(app.isPackaged?process.resourcesPath:path.join(root,'resources'),'universe');const moventureResources=path.join(app.isPackaged?process.resourcesPath:path.join(root,'resources'),'moventure');integrationUpdates=new IntegrationUpdates({root:path.join(dataDir,'integrations'),bundled:{557340:resources,2738490:solAdapter.resources,[MOVENTURE_ID]:moventureResources,2328750:universeResources,[PUMPKIN_ID]:pumpkinAdapter.resources},publicKey:fs.readFileSync(app.isPackaged?path.join(process.resourcesPath,'integrations/public-key.pem'):path.join(root,'resources/integrations/public-key.pem')),hubVersion:app.getVersion(),log});
 try{pumpkinAdapter.multiplier=pumpkinSpeed(prefs.pumpkinSpeed);}catch{}
 pedroAdapter.resources=await integrationUpdates.resolve(557340);solAdapter.resources=await integrationUpdates.resolve(2738490);pumpkinAdapter.resources=await integrationUpdates.resolve(PUMPKIN_ID);pumpkinAdapter.health.setResources(pumpkinAdapter.resources);enhanceAdapter(pedroAdapter,557340,compatibilityChanged);enhanceAdapter(solAdapter,2738490,compatibilityChanged);for(const folder of new Set([await integrationUpdates.resolve(MOVENTURE_ID),await integrationUpdates.resolve(2328750),...await integrationUpdates.discovered()]))registerManaged(folder);prefs.hotkeys=normalizeAll(prefs.hotkeys);prefs.overlays=Object.fromEntries(Object.keys(COMMANDS).map(id=>[id,{enabled:prefs.overlays[id]?.enabled===true}]));
 adapter=pedroAdapter;solAdapter.parameters=prefs.solParameters;probe=new WindowProbe(app.isPackaged?path.join(process.resourcesPath,'desktop'):path.join(root,'resources/desktop'));overlay=new OverlayController({BrowserWindow,screen,root,probe,onBlur:()=>{if(overlay?.win)editing.delete(overlay.win.webContents.id);try{shortcuts();}catch(e){notifyError(e.message);}},getPayload:()=>overlayPayload(),getTarget:async()=>({id:activeId,exe:path.join(adapter.gamePath||'',DEFINITIONS[activeId].exe),owned:adapter.owned,enabled:prefs.overlays[activeId].enabled})});pumpkinHud=new PumpkinHud({BrowserWindow,screen,root,probe,adapter:pumpkinAdapter});await scan();
 handle('app-updates-check',()=>appUpdates?.check()||'A verificação está disponível no Hub instalado.');
 handle('changelog-seen',()=>{prefs.seenChangelog=app.getVersion();save();});
 function saveManagedResourceDraft(id,command,resource,amount){
  const target=getAdapter(id);
  if(!(target instanceof ManagedParametersAdapter))throw Error('Controle indisponível.');
  target.validateResource(command,resource,amount);
  const previous=target.resourceDraft,oldPreferences=prefs.resourceDrafts;
  target.resourceDraft={resource,amount};
  prefs.resourceDrafts={...prefs.resourceDrafts,[id]:target.resourceDraft};
  try{save();}catch(error){target.resourceDraft=previous;prefs.resourceDrafts=oldPreferences;throw error;}
  return target.resourceDraft;
 }
 handle('managed-resource-draft',saveManagedResourceDraft);
 async function applyManagedResource(id,command,resource,amount){
  requireActive(id);
  adapter.health.assertCheat(command);
  saveManagedResourceDraft(id,command,resource,amount);
  const applied=await adapter.resource(command,resource,amount);
  soundCue('activate');await publish();return applied;
 }
 handle('managed-resource',applyManagedResource);
 handle('managed-parameter',async(id,command,value)=>{
  const target=getAdapter(id);
  if(!(target instanceof ManagedParametersAdapter))throw Error('Controle indisponível.');
  const previous={...target.parameters},preferences=prefs.managedParameters;
  const next=target.setParameter(command,value);
  prefs.managedParameters={...prefs.managedParameters,[id]:next};
  try{save();}catch(error){target.setParameter(command,previous[command]);prefs.managedParameters=preferences;throw error;}
  await publish();return next;
 });
 handle('snapshot',snapshot);handle('scan',async()=>{await scan();log(`${installed.length} jogos encontrados na Steam.`);return snapshot();});
 handle('sound-settings',patch=>{prefs.sound=updateSound(prefs.sound,patch);clearTimeout(soundSaveTimer);soundSaveTimer=setTimeout(save,200);win.webContents.send('sound-settings',prefs.sound);return prefs.sound;});
 handle('sound-preview',kind=>{if(!['activate','deactivate'].includes(kind))throw new Error('Prévia de som inválida.');soundCue(kind);return prefs.sound.enabled&&prefs.sound.volume>0;});
 handle('favorite',id=>{
  find(id);
  prefs.favorites=prefs.favorites.includes(id)?prefs.favorites.filter(value=>value!==id):[...prefs.favorites,id];
  save();
  return games();
 });
 handle('open-discord',()=>shell.openExternal('https://discord.gg/pMSJayXFmj'));
 handle('launch',async id=>{find(id);if(!fs.existsSync(path.join(steamRoot,'steam.exe')))throw new Error('Steam não encontrada.');await run(path.join(steamRoot,'steam.exe'),['-applaunch',String(id)],{windowsHide:true,timeout:10000});log(`Inicialização solicitada à Steam: ${find(id).name}.`);return true;});
 handle('connect',async(id=557340)=>{endCapture();const target=getAdapter(id);overlay.hide(false);if(target!==adapter)adapter.disconnect();adapter=target;activeId=id;try{const status=await adapter.connect();shortcuts();publish();return status;}catch(e){adapter.disconnect();registry.clear();publish();throw e;}});
 handle('pumpkin-speed',value=>{const previous=pumpkinAdapter.multiplier;const next=pumpkinAdapter.setMultiplier(value);prefs.pumpkinSpeed=next;try{save();}catch(error){prefs.pumpkinSpeed=previous;pumpkinAdapter.setMultiplier(previous);throw error;}publish();return next;});
 handle('cheat',setCheat);handle('combo',value=>{requireActive(557340);const result=adapter.setCombo(value);publish();return result;});handle('gold',value=>applySol('gold',value));handle('sol-action',applySol);handle('sol-values',setSolValues);
 handle('disconnect',(id=activeId)=>{getAdapter(id).disconnect();overlay.hide(false);shortcuts();publish();return true;});
 handle('patch',async(command,id=557340)=>{
  if(patchBusy||integrationUpdates.busy)throw Error('Aguarde a operação anterior.');
  patchBusy=true;
  try{
   let target=getAdapter(id);
   const install=command==='install'||command==='install-local';
   if(install){
    if(!target.gamePath)throw Error('Selecione a pasta de instalação do jogo.');
    if((await target.status()).gameRunning)throw Error('Feche o jogo antes de atualizar a integração.');
    if(command==='install'&&prefs.integrationUpdatesURL){
     const updates=await checkIntegrationCatalog();
     if(updates.available.some(entry=>entry.gameId===id)){
      await activatePackage(await integrationUpdates.install(id));
      target=getAdapter(id);
     }
    }
   }
   target.resources=await integrationUpdates.resolve(id);
   target.health.setResources(target.resources);
   if(install&&(await target.health.inspect(true)).state==='compatible')return target.status();
   return await target.patch(install?'install':command);
  }finally{patchBusy=false;await publish();}
 });
 async function activatePackage(result){if(!result.unchanged){const managed=registerManaged(result.resources);if(!managed){const target=getAdapter(result.gameId);target.disconnect();target.resources=result.resources;target.health.setResources(result.resources);}save();publishCatalog=true;shortcuts();await publish();}return {...result,resources:undefined};}
 handle('integration-check',async id=>{getAdapter(id);await getAdapter(id).health.inspect(true);await publish();return snapshot();});
 handle('integration-import',async()=>{if(patchBusy||integrationUpdates.busy)throw Error('Aguarde a operação anterior.');patchBusy=true;try{const selected=await dialog.showOpenDialog(win,{title:'Importar pacote de integração assinado',properties:['openFile'],filters:[{name:'Integração ByteTrainer',extensions:['thub']}]});if(selected.canceled)return null;const file=selected.filePaths[0];if(fs.statSync(file).size>20*1024*1024)throw Error('Pacote excede o tamanho permitido.');return await activatePackage(await integrationUpdates.importBytes(fs.readFileSync(file)));}finally{patchBusy=false;}});
 let catalogCheck;
 function checkIntegrationCatalog(){
  catalogCheck??=integrationUpdates.check(prefs.integrationUpdatesURL||'').catch(error=>{
   integrationUpdates.available=[];
   integrationUpdates.status='Não foi possível consultar as atualizações das integrações.';
   throw error;
  }).finally(()=>{catalogCheck=null;});
  return catalogCheck;
 }
 handle('integration-updates-url',value=>{
  if(patchBusy||integrationUpdates.busy||catalogCheck)throw Error('Aguarde a operação anterior.');
  prefs.integrationUpdatesURL=endpoint(value);save();publish();return prefs.integrationUpdatesURL;
 });
 handle('integration-updates-check',async()=>{try{return await checkIntegrationCatalog();}finally{await publish();}});
 handle('integration-update-download',async id=>{if(!Number.isSafeInteger(id))throw Error('Jogo inválido.');if(patchBusy||integrationUpdates.busy)throw Error('Aguarde a operação anterior.');patchBusy=true;try{return await activatePackage(await integrationUpdates.install(id));}finally{patchBusy=false;}});
 handle('hotkey',(id,command,key)=>setBindings(id,command,key));handle('hotkeys-reset',id=>setBindings(id,null,null,true));
 handle('capture-keys',enabled=>{if(typeof enabled!=='boolean')throw new Error('Estado inválido.');if(!enabled){endCapture();return false;}clearTimeout(captureTimer);registry.pause();captureSender=win.webContents;captureTimer=setTimeout(endCapture,15000);return true;});
 handle('overlay-enabled',(id,enabled)=>{getAdapter(id);if(typeof enabled!=='boolean')throw new Error('Estado inválido.');const old=prefs.overlays[id].enabled;prefs.overlays[id].enabled=enabled;try{shortcuts();save();}catch(e){prefs.overlays[id].enabled=old;try{shortcuts();}catch{}throw e;}if(!enabled)overlay.hide(false);broadcastPreferences();return enabled;});
 handle('overlay-toggle',()=>overlay.toggle(true));handle('overlay-state',()=>overlayPayload());handle('overlay-hide',()=>overlay.hide(true));handle('input-focus',()=>{});
 handle('locate',async(id=557340,automatic=false)=>{
  if(patchBusy||integrationUpdates.busy)throw Error('Aguarde a operação anterior.');
  patchBusy=true;
  try{
   const target=getAdapter(id),game=find(id);
   if((await target.status()).gameRunning)throw Error('Feche o jogo antes de alterar a pasta de instalação.');
   const paths={...prefs.gamePaths};
   if(automatic)delete paths[id];
   else{
    const result=await dialog.showOpenDialog(win,{title:'Selecione a pasta de '+game.name,defaultPath:target.gamePath||undefined,properties:['openDirectory']});
    if(result.canceled)return snapshot();
    const dir=result.filePaths[0];
    if(!fs.statSync(path.join(dir,DEFINITIONS[id].exe),{throwIfNoEntry:false})?.isFile())
     throw Error('Selecione a pasta que contém o executável de '+game.name+'.');
    paths[id]=dir;
   }
   const previous=prefs.gamePaths;
   prefs.gamePaths=paths;
   try{save();}catch(error){prefs.gamePaths=previous;throw error;}
   target.setPath(gamePath(id));
   shortcuts();
   await publish();
   return snapshot();
  }finally{patchBusy=false;}
 });
 handle('open-data',()=>shell.openPath(dataDir));
 handle('window',action=>{if(action==='minimize')win.minimize();else if(action==='maximize')win.isMaximized()?win.unmaximize():win.maximize();else if(action==='close')win.close();});
 win=new BrowserWindow({width:1360,height:900,minWidth:1024,minHeight:700,show:!smoke,frame:false,backgroundColor:'#101211',icon:path.join(root,'assets','icon.png'),title:'ByteTrainer',webPreferences:{preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true,autoplayPolicy:'no-user-gesture-required',backgroundThrottling:false}});
 win.removeMenu();win.webContents.setWindowOpenHandler(()=>({action:'deny'}));win.webContents.on('will-navigate',(event,url)=>{if(url!==pathToFileURL(path.join(root,'dist','index.html')).href)event.preventDefault();});
 win.on('blur',()=>{editing.delete(win.webContents.id);endCapture();});win.on('closed',()=>app.quit());
 await win.loadFile(path.join(root,'dist','index.html'));if(!smoke)win.show();interval=setInterval(()=>publish().catch(e=>notifyError(e.message)),2000);
 if(![smoke,soundTest,solTest,featuresTest,overlayTest,compatibilityTest,compatibilityUITest].some(Boolean)){
  const checkIntegrations=()=>checkIntegrationCatalog().catch(()=>{}).finally(()=>publish());
  void checkIntegrations();
  updatesInterval=setInterval(checkIntegrations,6*60*60*1000);
  if(app.isPackaged&&process.platform==='win32')appUpdates=require('./app-updates.cjs').startAppUpdates({win,dialog,log,canInstall:()=>prefs.seenChangelog===app.getVersion()&&!patchBusy&&!integrationUpdates.busy});
 }
 if(compatibilityUITest){try{await require('./compatibility-ui-validation.cjs')({win,solAdapter,publish,output:testOutput});}catch(e){fs.writeFileSync(path.join(testOutput,'compatibility-ui-test-error.txt'),e.stack);process.exitCode=1;}finally{app.quit();}return;}
 if(compatibilityTest){try{await require('./compatibility-validation.cjs')({win,overlay,dialog,pedroAdapter,solAdapter,publish,output:testOutput});}catch(e){fs.writeFileSync(path.join(testOutput,'compatibility-test-error.txt'),e.stack);process.exitCode=1;}finally{app.quit();}return;}
 if(overlayTest){try{await require('./overlay-validation.cjs')({win,overlay,probe,output:testOutput});}catch(e){fs.writeFileSync(path.join(testOutput,'overlay-test-error.txt'),e.stack);process.exitCode=1;}finally{app.quit();}return;}
 if(featuresTest){try{await require('./features-validation.cjs')({win,overlay,probe,output:testOutput});}catch(e){fs.writeFileSync(path.join(testOutput,'features-test-error.txt'),e.stack);process.exitCode=1;}finally{app.quit();}return;}
 if(solTest){try{await require('./sol-validation.cjs')({win,output:testOutput});}catch(e){fs.writeFileSync(path.join(testOutput,'sol-test-error.txt'),e.stack);process.exitCode=1;}finally{app.quit();}return;}
 if(soundTest){
  const live=adapter;adapter={owned:true,options:Array(8).fill(false),combo:10,set(i,value){if(!Number.isInteger(i)||i<0||i>=8||typeof value!=='boolean')throw new Error('Invalid cheat');this.options[i]=value;return{options:this.options,combo:this.combo};},async status(){return{installed:true,gameRunning:false,connected:false,owned:true,options:this.options,combo:10};}};
  try{await require('./audio-validation.cjs')({win,dataDir,output:testOutput,setCheat,persistenceOnly:process.argv.includes('--sound-persistence-check')});}catch(e){fs.writeFileSync(path.join(testOutput,'sound-test-error.txt'),e.stack);process.exitCode=1;}finally{adapter=live;app.quit();}return;
 }
 if(smoke){
  await new Promise(r=>setTimeout(r,5000));
  try{
   const runUI=code=>win.webContents.executeJavaScript(code);
   fs.writeFileSync(path.join(testOutput,'preview.png'),(await win.webContents.capturePage()).toPNG());
   const result=await runUI(`(async()=>{const s=await window.hub.snapshot();const id=s.games[0].id;const before=s.games.find(g=>g.id===id);await window.hub.favorite(id);await window.hub.favorite(id);const after=(await window.hub.snapshot()).games.find(g=>g.id===id);return {games:s.games.length,installed:s.games.filter(g=>g.installed).length,supported:s.games.filter(g=>g.supported).map(g=>g.name),hasSearch:!!document.querySelector('input[type="search"]'),favoritesRestored:before.favorite===after.favorite,supportedOnly:s.games.every(g=>g.supported),overflow:document.documentElement.scrollWidth>innerWidth}})()`);
   await runUI(`document.querySelector('[aria-label^="Abrir My Friend Pedro"]').click()`);
   await new Promise(r=>setTimeout(r,500));
   result.detail=await runUI(`({title:document.querySelector('h1')?.textContent,cheats:document.querySelectorAll('.cheat-row').length,hasConnect:[...document.querySelectorAll('button')].some(b=>b.textContent.includes('Conectar trainer')),overflow:document.documentElement.scrollWidth>innerWidth})`);
   fs.writeFileSync(path.join(testOutput,'preview-trainer.png'),(await win.webContents.capturePage()).toPNG());
   win.setSize(1024,740);await new Promise(r=>setTimeout(r,350));
   result.compact=await runUI(`({width:innerWidth,overflow:document.documentElement.scrollWidth>innerWidth})`);
   fs.writeFileSync(path.join(testOutput,'preview-compact.png'),(await win.webContents.capturePage()).toPNG());
   fs.writeFileSync(path.join(testOutput,'smoke-test.json'),JSON.stringify(result,null,2));
  }catch(e){fs.writeFileSync(path.join(testOutput,'smoke-error.txt'),e.stack);}
  app.quit();
 }
});
app.on('before-quit',()=>{appUpdates?.stop();pedroAdapter?.close();solAdapter?.close();pumpkinAdapter?.close();for(const target of managedAdapters.values())target.close();overlay?.close();pumpkinHud?.close();probe?.close();registry.clear();clearTimeout(captureTimer);clearInterval(interval);clearInterval(updatesInterval);clearTimeout(soundSaveTimer);if(dataDir&&prefs.sound)save();globalShortcut.unregisterAll();});app.on('window-all-closed',()=>app.quit());}
