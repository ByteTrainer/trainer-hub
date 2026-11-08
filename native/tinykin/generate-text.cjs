const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..');
const quote=value=>JSON.stringify(value).replace(/\u2028/g,'\\u2028').replace(/\u2029/g,'\\u2029');
const rows=['using System.Collections.Generic;','namespace ByteTrainer.Tinykin { internal static class TrackerText {', 'private static readonly Dictionary<string,string[]> Text=new Dictionary<string,string[]> {'];
const locales=['en','pt-BR','es'].map(locale=>({...JSON.parse(fs.readFileSync(path.join(root,'locales/games/tinykin',locale+'.json'),'utf8')),'Collectible tracker':JSON.parse(fs.readFileSync(path.join(root,'locales/common',locale+'.json'),'utf8'))['Collectible tracker']}));
for(const key of Object.keys(locales[0]).sort()) rows.push('{'+quote(key)+',new string[]{'+locales.map(locale=>quote(locale[key]||key)).join(',')+'}},');
rows.push('}; internal static string Get(string key) { string[] text;if(!Text.TryGetValue(key,out text))return key;string language=I2.Loc.LocalizationManager.CurrentLanguageCode??"en";return text[language.StartsWith("pt")?1:language.StartsWith("es")?2:0]; } } }');
fs.mkdirSync(path.join(root,'.research/tinykin'),{recursive:true});
fs.writeFileSync(path.join(root,'.research/tinykin/TrackerText.cs'),rows.join('\n'));
