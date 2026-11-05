const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('hub',{
 checkIntegration:id=>ipcRenderer.invoke('integration-check',id),importIntegration:()=>ipcRenderer.invoke('integration-import'),setUpdatesURL:value=>ipcRenderer.invoke('integration-updates-url',value),checkUpdates:()=>ipcRenderer.invoke('integration-updates-check'),downloadIntegration:id=>ipcRenderer.invoke('integration-update-download',id),
 setBinding:(id,command,key)=>ipcRenderer.invoke('hotkey',id,command,key),resetBindings:id=>ipcRenderer.invoke('hotkeys-reset',id),captureKeys:enabled=>ipcRenderer.invoke('capture-keys',enabled),
 setOverlay:(id,enabled)=>ipcRenderer.invoke('overlay-enabled',id,enabled),toggleOverlay:()=>ipcRenderer.invoke('overlay-toggle'),overlayState:()=>ipcRenderer.invoke('overlay-state'),hideOverlay:()=>ipcRenderer.invoke('overlay-hide'),inputFocus:focused=>ipcRenderer.invoke('input-focus',focused),
 setSolValues:patch=>ipcRenderer.invoke('sol-values',patch),solAction:(kind,value)=>ipcRenderer.invoke('sol-action',kind,value),
 onPreferences:callback=>{const fn=(_,v)=>callback(v);ipcRenderer.on('trainer-settings',fn);return()=>ipcRenderer.removeListener('trainer-settings',fn);},
 onCaptureEnded:callback=>{ipcRenderer.on('capture-ended',callback);return()=>ipcRenderer.removeListener('capture-ended',callback);},
 onTrainerError:callback=>{const fn=(_,v)=>callback(v);ipcRenderer.on('trainer-error',fn);return()=>ipcRenderer.removeListener('trainer-error',fn);},
 onOverlayState:callback=>{const fn=(_,v)=>callback(v);ipcRenderer.on('overlay-state',fn);return()=>ipcRenderer.removeListener('overlay-state',fn);},
 setSound:patch=>ipcRenderer.invoke('sound-settings',patch),previewSound:kind=>ipcRenderer.invoke('sound-preview',kind),
 onSound:callback=>{const listener=(_,value)=>callback(value);ipcRenderer.on('sound-cue',listener);return()=>ipcRenderer.removeListener('sound-cue',listener);},
 onSoundSettings:callback=>{const listener=(_,value)=>callback(value);ipcRenderer.on('sound-settings',listener);return()=>ipcRenderer.removeListener('sound-settings',listener);},
 snapshot:()=>ipcRenderer.invoke('snapshot'),scan:()=>ipcRenderer.invoke('scan'),
 favorite:id=>ipcRenderer.invoke('favorite',id),request:id=>ipcRenderer.invoke('request',id),
 refresh:()=>ipcRenderer.invoke('refresh'),search:term=>ipcRenderer.invoke('search',term),
 launch:id=>ipcRenderer.invoke('launch',id),connect:id=>ipcRenderer.invoke('connect',id),
 cheat:(index,value,id)=>ipcRenderer.invoke('cheat',index,value,id),combo:value=>ipcRenderer.invoke('combo',value),gold:value=>ipcRenderer.invoke('gold',value),
 disconnect:id=>ipcRenderer.invoke('disconnect',id),patch:(command,id)=>ipcRenderer.invoke('patch',command,id),
 locate:()=>ipcRenderer.invoke('locate'),openData:()=>ipcRenderer.invoke('open-data'),
 window:action=>ipcRenderer.invoke('window',action),
 onStatus:callback=>{const listener=(_,value)=>callback(value);ipcRenderer.on('status',listener);return()=>ipcRenderer.removeListener('status',listener);}
});
