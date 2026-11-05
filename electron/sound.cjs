const STYLES=Object.freeze(['digital','soft','arcade','minimal']);
const DEFAULT_SOUND=Object.freeze({enabled:true,volume:35,style:'digital'});
function normalizeSound(value){const v=value&&typeof value==='object'?value:{};return {enabled:typeof v.enabled==='boolean'?v.enabled:DEFAULT_SOUND.enabled,volume:typeof v.volume==='number'&&Number.isFinite(v.volume)?Math.round(Math.max(0,Math.min(100,v.volume))):DEFAULT_SOUND.volume,style:STYLES.includes(v.style)?v.style:DEFAULT_SOUND.style};}
function updateSound(current,patch){
 if(!patch||typeof patch!=='object'||Array.isArray(patch)||Object.keys(patch).some(k=>!['enabled','volume','style'].includes(k)))throw new Error('Configuração de som inválida.');
 if('enabled'in patch&&typeof patch.enabled!=='boolean')throw new Error('Escolha ativar ou desativar o som.');
 if('volume'in patch&&(typeof patch.volume!=='number'||!Number.isFinite(patch.volume)||patch.volume<0||patch.volume>100))throw new Error('O volume deve estar entre 0 e 100.');
 if('style'in patch&&!STYLES.includes(patch.style))throw new Error('Estilo de som inválido.');
 return normalizeSound({...normalizeSound(current),...patch});
}
function changeCheat(adapter,index,enabled,notify){
 const before=adapter.options[index];const result=adapter.set(index,enabled);
 if(adapter.owned&&before!==adapter.options[index])notify(adapter.options[index]?'activate':'deactivate');
 return result;
}
module.exports={STYLES,DEFAULT_SOUND,normalizeSound,updateSound,changeCheat};
