import test from 'node:test';
import assert from 'node:assert/strict';
import { OverlayController } from '../electron/overlay.cjs';

const exe = 'C:\\Steam\\Game.exe';
const game = {exe, handle:'123', bounds:{x:0,y:0,width:1920,height:1080}};
const payload = {gameId:249050, status:{owned:true}};

function fixture(info=game) {
  const calls=[];
  const win={
    visible:false,
    focused:false,
    isDestroyed:()=>false,
    isVisible(){return this.visible;},
    isFocused(){return this.focused;},
    setBounds(){},
    setFocusable(value){calls.push("focusable:" + value);},
    showInactive(){calls.push('showInactive');this.visible=true;},
    hide(){calls.push('hide');this.visible=false;},
    webContents:{send(){}}
  };
  const probe={info,async request({op}){calls.push(op);return this.info;}};
  const overlay=new OverlayController({
    screen:{getDisplayMatching:()=>({workArea:game.bounds})},
    probe,getTarget:()=>({id:249050,enabled:true,owned:true,exe}),getPayload:()=>payload
  });
  overlay.ensure=async()=>{overlay.win=win;};
  return {overlay,win,probe,calls};
}

test('opens the overlay without taking focus or refocusing the game on inactive close',async()=>{
  const {overlay,win,calls}=fixture();
  assert.equal(await overlay.toggle(),true);
  assert.equal(win.isFocused(),false);
  assert.equal(await overlay.toggle(),false);
  assert.deepEqual(calls,['foreground','focusable:false','showInactive','focusable:true','hide']);
});

test('keeps exclusive fullscreen and minimized game windows untouched',async()=>{
  const exclusive=fixture({...game,exclusiveFullscreen:true});
  await assert.rejects(exclusive.overlay.toggle(),/windowed or borderless/);
  await assert.rejects(exclusive.overlay.toggle(true),/windowed or borderless/);
  assert.deepEqual(exclusive.calls,['foreground','find']);
  const minimized=fixture({...game,minimized:true});
  assert.equal(await minimized.overlay.toggle(),false);
  assert.deepEqual(minimized.calls,['foreground']);
});

test('restores focus after interacting and hides an inactive overlay when the game loses focus',async()=>{
  const {overlay,win,probe,calls}=fixture();
  await overlay.toggle();
  win.focused=true;
  await overlay.hide(true);
  assert.deepEqual(calls.slice(-2),['focus','hide']);
  win.focused=false;
  await overlay.toggle();
  probe.info={handle:'456'};
  overlay.send(payload);
  await overlay.foregroundCheck;
  assert.equal(win.visible,false);
});
