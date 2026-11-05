const DEFAULTS={strength:10,magic:10,inventory:6,gold:100};
function validate(kind,value){const max=kind==='inventory'?6:kind==='gold'?999999:999,min=kind==='inventory'?1:0;if(!Object.hasOwn(DEFAULTS,kind)||!Number.isInteger(value)||value<min||value>max)throw new Error(`Escolha um valor inteiro entre ${min} e ${max}.`);return value;}
function updateParameters(current,patch){if(!patch||typeof patch!=='object'||Array.isArray(patch)||!Object.keys(patch).length)throw new Error('Valores inválidos.');const result={...DEFAULTS,...current};for(const[k,v]of Object.entries(patch))result[k]=validate(k,v);return result;}
function normalizeParameters(value){const result={...DEFAULTS};for(const k of Object.keys(DEFAULTS)){try{result[k]=validate(k,value?.[k]);}catch{}}return result;}
module.exports={DEFAULTS,validate,updateParameters,normalizeParameters};
