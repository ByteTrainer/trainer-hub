export const SOUND_STYLES=[
 {id:'digital',name:'Digital',description:'A short electronic tone.'},
 {id:'soft',name:'Soft',description:'Two soft, rounded notes.'},
 {id:'arcade',name:'Arcade',description:'A three-note retro sequence.'},
 {id:'minimal',name:'Minimal',description:'A brief, subtle tap.'}
];
export function cueNotes(style,kind){
 if(!['activate','deactivate'].includes(kind))return [];
 const on=kind==='activate';
 if(style==='soft')return (on?[660,880]:[660,440]).map((frequency,i)=>({frequency,end:frequency,type:'sine',delay:i*.075,duration:.18,level:.55}));
 if(style==='arcade')return (on?[523.25,659.25,783.99]:[783.99,659.25,392]).map((frequency,i)=>({frequency,end:frequency,type:'square',delay:i*.065,duration:.065,level:.32}));
 if(style==='minimal')return [{frequency:on?900:500,end:on?900:500,type:'sine',delay:0,duration:.065,level:.6}];
 return [{frequency:on?520:980,end:on?1040:440,type:'triangle',delay:0,duration:.15,level:.7}];
}
export function createSoundPlayer(contextFactory=()=>new AudioContext({latencyHint:'interactive'})){
 let context,master,voices=[],settings={enabled:true,volume:35,style:'digital'},serial=0;
 function clear(){for(const voice of voices){try{voice.osc.stop();}catch{}try{voice.osc.disconnect();voice.gain.disconnect();}catch{}}voices=[];}
 function configure(next){settings={...settings,...next};if(!settings.enabled||settings.volume===0){serial++;clear();}if(master&&context.state!=='closed')master.gain.setTargetAtTime(settings.volume/100*.18,context.currentTime,.012);}
 async function play(kind,next){if(next)configure(next);const notes=cueNotes(settings.style,kind);if(!settings.enabled||settings.volume<=0||!notes.length)return false;
  const own=++serial;
  if(!context||context.state==='closed'){context=contextFactory();master=context.createGain();master.gain.value=settings.volume/100*.18;master.connect(context.destination);}
  if(context.state==='suspended')await context.resume();
  if(own!==serial||!settings.enabled||settings.volume===0)return false;
  clear();const start=context.currentTime+.008;
  for(const note of notes){const osc=context.createOscillator(),gain=context.createGain();const time=start+note.delay;osc.type=note.type;osc.frequency.setValueAtTime(note.frequency,time);osc.frequency.exponentialRampToValueAtTime(note.end,time+note.duration);
   gain.gain.setValueAtTime(.0001,time);gain.gain.linearRampToValueAtTime(note.level,time+.006);gain.gain.exponentialRampToValueAtTime(.0001,time+note.duration);osc.connect(gain);gain.connect(master);
   const voice={osc,gain};voices.push(voice);osc.onended=()=>{osc.disconnect();gain.disconnect();voices=voices.filter(v=>v!==voice);};osc.start(time);osc.stop(time+note.duration+.01);
  }return true;
 }
 return {configure,play,dispose(){serial++;clear();const closing=context?.close();context=null;master=null;return closing;}};
}
