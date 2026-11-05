export function eventKey(event){
 let key=event.code||event.key;
 if(/^F\d+$/.test(key)){}else if(/^Numpad\d$/.test(key))key=`num${key.slice(-1)}`;else if(/^Key[A-Z]$/.test(key))key=key.slice(-1);else if(/^Digit\d$/.test(key))key=key.slice(-1);else if(/^[a-z0-9]$/i.test(key))key=key.toUpperCase();else key=({ArrowUp:'Up',ArrowDown:'Down',ArrowLeft:'Left',ArrowRight:'Right',Space:'Space',' ':'Space',Insert:'Insert',Delete:'Delete',Home:'Home',End:'End',PageUp:'PageUp',PageDown:'PageDown'})[key];
 if(!key||event.metaKey)return null;
 return [event.ctrlKey?'Control':null,event.altKey?'Alt':null,event.shiftKey?'Shift':null,key].filter(Boolean).join('+');
}
