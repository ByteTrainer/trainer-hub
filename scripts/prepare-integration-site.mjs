import {localizeSite} from './localize-site.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';

const require = createRequire(import.meta.url);
const { digest, verifyEnvelope, validatePackage } = require('../electron/integration-updates.cjs');
const legacyCommands = require('../electron/commands.json');
const appVersion = require('../package.json').version;
const root = path.resolve(import.meta.dirname, '..');
const [siteArgument, ...packages] = process.argv.slice(2);
const renderOnly = packages.length === 1 && packages[0] === '--render';
if (!siteArgument || !packages.length) throw Error('Use: prepare-integration-site.mjs SITE pacote.thub ... (ou --render)');
const site = path.resolve(siteArgument);
const origin = 'https://bytetrainer.github.io/trainer-integrations/';
const key = await fs.readFile(path.join(root, 'resources/integrations/public-key.pem'));
const names = {557340:'My Friend Pedro',2738490:'Sol Cesto',1186640:'Pumpkin Jack',3826060:'Moventure'};
const escape = value => String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const icon = '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 3v12m-5-5 5 5 5-5M5 16v4h14v-4"/></svg>';
const indexPath = path.join(site, 'index.html');
const html = await fs.readFile(path.join(root,'site/index.html'), 'utf8');
const versionedAsset = async file => file+'?v='+digest(await fs.readFile(path.join(site,file))).slice(0,16);
const [siteStyle, catalogScript] = await Promise.all(['assets/site.css','assets/catalogo.js'].map(versionedAsset));
const catalogPath = path.join(site, 'catalog-v2.json');
const old = await fs.readFile(catalogPath).catch(error => {
  if (error.code !== 'ENOENT') throw error;
  return fs.readFile(path.join(site, 'catalog.json'));
});
const rows = new Map(verifyEnvelope(old, key).packages.map(entry => [entry.gameId, entry]));
const additions = new Map();
for (const file of renderOnly ? [] : packages) {
  const name = path.basename(file);
  if (!/^[a-z0-9][a-z0-9.-]+\.thub$/i.test(name)) throw Error('Nome de pacote inválido.');
  const bytes = await fs.readFile(file);
  const value = verifyEnvelope(bytes, key);
  const contents = validatePackage(value, appVersion);
  const spec = contents.has('game.json') ? JSON.parse(contents.get('game.json')) : null;
  const current = rows.get(value.gameId);
  if (current && (current.release > value.release || current.release === value.release && current.sha256.toLowerCase() !== digest(bytes))) {
    throw Error('Use uma revisão crescente para alterar um pacote publicado.');
  }
  const target = path.join(site, 'packages', name);
  const existing = await fs.readFile(target).catch(error => { if (error.code !== 'ENOENT') throw error; return null; });
  if (existing && !existing.equals(bytes)) throw Error('Já existe outro pacote com esse nome.');
  additions.set(target, bytes);
  rows.set(value.gameId, { gameId:value.gameId, release:value.release, version:value.version,
    minimumHubVersion:value.minimumHubVersion, name:spec?.name || names[value.gameId], url:origin+'packages/'+name, sha256:digest(bytes) });
}

const entries = [];
for (const row of rows.values()) {
  const relative = row.url.startsWith(origin+'packages/') && row.url.slice(origin.length);
  if (!relative || !/^packages\/[a-z0-9][a-z0-9.-]+\.thub$/i.test(relative)) throw Error('URL de pacote inválida.');
  const target = path.join(site, relative);
  const bytes = additions.get(target) || await fs.readFile(target);
  if (digest(bytes) !== row.sha256.toLowerCase()) throw Error('Hash de pacote diferente do catálogo.');
  const value = verifyEnvelope(bytes, key);
  const files = validatePackage(value, appVersion);
  const recipe = JSON.parse(files.get('recipe.json'));
  const spec = files.has('game.json') ? JSON.parse(files.get('game.json')) : null;
  const commands = spec?.commands || legacyCommands[row.gameId];
  const cheats = recipe.cheats.map(id => commands.find(command => command.id === id).name);
  entries.push({...row, name:spec?.name || row.name || names[row.gameId], relative, cheats});
}
const arrow = '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M5 12h14m-6-6 6 6-6 6"/></svg>';
const back = '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M19 12H5m6 6-6-6 6-6"/></svg>';
const header = html.match(/<header class="site-header">[\s\S]*?<\/header>/)?.[0];
const footer = html.match(/<footer class="site-footer">[\s\S]*?<\/footer>/)?.[0];
if (!header || !footer) throw Error('Cabeçalho ou rodapé do site não encontrado.');

function relativeLinks(fragment, prefix) {
  return fragment.replace(/(href|src)="([^":#][^":]*)"/g, (_, attribute, value) => attribute+'="'+prefix+value+'"');
}

function page({title, description, filename, content, prefix='', script=''}) {
  const navigation = filename==='catalogo.html' ? header.replace('href="catalogo.html"', 'href="catalogo.html" aria-current="page"') : header;
  return `<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="description" content="${escape(description)}">
  <meta name="theme-color" content="#111410">
  <title>${escape(title)} · ByteTrainer</title>
  <link rel="icon" href="${prefix}assets/icon.png" type="image/png">
  <link rel="canonical" href="${origin}${filename}">
  <link rel="stylesheet" href="${prefix}${siteStyle}">${script}
</head>
<body>
  <a class="skip-link" href="#conteudo">Skip to content</a>
  ${relativeLinks(navigation,prefix)}
  ${content}
  ${relativeLinks(footer,prefix)}
</body>
</html>
`;
}

const artwork=new Map(JSON.parse(await fs.readFile(path.join(root,'assets/catalog.json'),'utf8')).map(game=>[game.id,game.header]));
const art = (entry,prefix='') => entry.gameId===3578070 ? prefix+'assets/shake-header.jpg' : entry.gameId===3826060 ? prefix+'assets/moventure-header.jpg' : artwork.get(entry.gameId)||
  'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/'+entry.gameId+'/header.jpg';
const cheatCount = entry => entry.cheats.length+(entry.cheats.length===1?' cheat':' cheats');

async function renderSite() {
  const sorted = [...entries].sort((a,b)=>a.name.localeCompare(b.name,'pt-BR'));
  const list = sorted.map(entry=>`      <li><a class="catalog-game" href="jogos/${entry.gameId}.html">
        <img src="${art(entry)}" width="460" height="215" alt="" loading="lazy">
        <div class="catalog-game-info"><div><h2>${escape(entry.name)}</h2><p>${cheatCount(entry)}</p></div>${arrow}</div>
      </a></li>`).join('\n');
  await fs.writeFile(path.join(site,'catalogo.html'),page({
    title:'Catalog',description:'Find games supported by ByteTrainer and see each integration\'s cheats.',
    filename:'catalogo.html',script:'\n  <script src="'+catalogScript+'" defer></script>',
    content:`<main class="wrap section catalog-page" id="conteudo">
    <div class="catalog-heading">
      <div><h1>Game catalog</h1><p>See available cheats and download signed packages.</p></div>
      <div class="catalog-search" id="catalog-search" role="search" hidden>
        <label for="game-search">Search catalog</label>
        <div class="catalog-search-field"><svg aria-hidden="true" viewBox="0 0 24 24"><circle cx="11" cy="11" r="6"/><path d="m16 16 4 4"/></svg><input id="game-search" type="search" placeholder="Enter a game name"></div>
      </div>
    </div>
    <p class="catalog-results" id="catalog-results" role="status">${entries.length} ${entries.length===1?'game':'games'}</p>
    <ul class="catalog-list">
${list}
    </ul>
    <p id="catalog-empty" hidden>No game found. Try another name.</p>
  </main>`
  }));
  await fs.mkdir(path.join(site,'jogos'),{recursive:true});
  for(const entry of sorted){
    const name=escape(entry.name), filename='jogos/'+entry.gameId+'.html';
    await fs.writeFile(path.join(site,filename),page({
      title:entry.name,description:'See the cheats and download the signed integration for '+entry.name+'.',
      filename,prefix:'../',
      content:`<main class="wrap section game-detail" id="conteudo">
    <a class="game-back" href="../catalogo.html">${back} Back to catalog</a>
    <div class="game-detail-header">
      <div>
        <h1>${name}</h1>
        <p class="game-detail-meta">Steam · ${cheatCount(entry)} · Package ${escape(entry.version)}</p>
        <div class="game-detail-actions">
          <a class="button button-primary" href="../${entry.relative}" download>${icon} Download package ${escape(entry.version)}</a>
          <a href="https://store.steampowered.com/app/${entry.gameId}/">View on Steam</a>
        </div>
      </div>
      <img class="game-detail-art" src="${art(entry,'../')}" width="460" height="215" alt="">
    </div>
    <section class="game-cheats" aria-labelledby="cheat-title">
      <h2 id="cheat-title">Available cheats</h2>
      <ul>${entry.cheats.map(cheat=>'<li>'+escape(cheat)+'</li>').join('')}</ul>
      <p>For manual installation, import the package in Settings → Integration updates in ByteTrainer. With the game closed, apply the local package on the game's page.</p>
    </section>
  </main>`
    }));
  }
  const home=html.replace(/\d+ supported games/,entries.length+' supported games')
    .replace(/href="assets\/site\.css(?:\?[^"]*)?"/, 'href="'+siteStyle+'"');
  if(home!==html)await fs.writeFile(indexPath,home);
  await localizeSite(site,['index.html','catalogo.html',...sorted.map(entry=>'jogos/'+entry.gameId+'.html')]);
}


if (renderOnly) { await renderSite(); process.exit(0); }

const temp = await fs.mkdtemp(path.join(tmpdir(), 'thub-catalog-'));
try {
  const unsigned = path.join(temp, 'catalog.json'), signed = path.join(temp, 'signed.json');
  await fs.writeFile(unsigned, JSON.stringify({kind:'catalog', packages:entries.map(({relative,cheats,...row})=>row)}));
  execFileSync(process.execPath, [path.join(root,'scripts/integrations.mjs'),'catalog',unsigned,signed], {stdio:'inherit'});
  const catalog = await fs.readFile(signed);
  verifyEnvelope(catalog, key);
  for (const [target, bytes] of additions) await fs.writeFile(target, bytes);
  await renderSite();
  await fs.writeFile(catalogPath, catalog);
} finally {
  await fs.rm(temp, {recursive:true,force:true});
}
