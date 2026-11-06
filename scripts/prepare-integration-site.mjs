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
if (!siteArgument || !packages.length) throw Error('Use: prepare-integration-site.mjs SITE pacote.thub ...');
const site = path.resolve(siteArgument);
const origin = 'https://bytetrainer.github.io/trainer-integrations/';
const key = await fs.readFile(path.join(root, 'resources/integrations/public-key.pem'));
const names = {557340:'My Friend Pedro',2738490:'Sol Cesto',1186640:'Pumpkin Jack',3826060:'Moventure'};
const escape = value => String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const icon = '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 3v12m-5-5 5 5 5-5M5 16v4h14v-4"/></svg>';
const indexPath = path.join(site, 'index.html');
const html = await fs.readFile(indexPath, 'utf8');
const catalogPath = path.join(site, 'catalog-v2.json');
const old = await fs.readFile(catalogPath).catch(error => {
  if (error.code !== 'ENOENT') throw error;
  return fs.readFile(path.join(site, 'catalog.json'));
});
const rows = new Map(verifyEnvelope(old, key).packages.map(entry => [entry.gameId, entry]));
const additions = new Map();
for (const file of packages) {
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
const slugs = {557340:'my-friend-pedro',2738490:'sol-cesto',1186640:'pumpkin-jack',3826060:'moventure'};
const cards = entries.map(entry => {
  const name = escape(entry.name), count = entry.cheats.length;
  return `      <article class="game" id="${slugs[entry.gameId] || 'jogo-'+entry.gameId}">
        <div class="game-title"><h3>${name}</h3><p>Steam <span>·</span> ${count} ${count===1?'cheat':'cheats'}</p><a href="https://store.steampowered.com/app/${entry.gameId}/">Ver na Steam</a></div>
        <div class="game-options"><details><summary>Ver controles <span aria-hidden="true"></span></summary><ul>${entry.cheats.map(cheat=>'<li>'+escape(cheat)+'</li>').join('')}</ul></details></div>
      </article>`;
}).join('\n');
const links = entries.map(entry=>`        <li><span>${escape(entry.name)} <small>v${escape(entry.version)}</small></span><a href="${entry.relative}" download>Baixar pacote<span class="sr-only"> de ${escape(entry.name)}</span> ${icon}</a></li>`).join('\n');
const withCards = html.replace(/      <article class="game"[\s\S]*?\n    <\/section>/, cards+'\n    </section>');
const output = withCards.replace(/(<ul class="package-list">)[\s\S]*?(<\/ul>)/, '$1\n'+links+
  '\n        <li><span>Catálogo assinado <small>Índice usado pelo Hub atual</small></span><a href="catalog-v2.json">Ver JSON</a></li>\n      $2')
  .replace(/\d+ jogos compatíveis/, entries.length+' jogos compatíveis')
  .replace('Explore os cheats de My Friend Pedro e Sol Cesto.', 'Explore os cheats dos jogos compatíveis.');
if (withCards === html && !html.includes(cards) || !output.includes(links)) throw Error('Seções do site não encontradas.');

const temp = await fs.mkdtemp(path.join(tmpdir(), 'thub-catalog-'));
try {
  const unsigned = path.join(temp, 'catalog.json'), signed = path.join(temp, 'signed.json');
  await fs.writeFile(unsigned, JSON.stringify({kind:'catalog', packages:entries.map(({relative,cheats,...row})=>row)}));
  execFileSync(process.execPath, [path.join(root,'scripts/integrations.mjs'),'catalog',unsigned,signed], {stdio:'inherit'});
  const catalog = await fs.readFile(signed);
  verifyEnvelope(catalog, key);
  for (const [target, bytes] of additions) await fs.writeFile(target, bytes);
  await fs.writeFile(indexPath, output);
  await fs.writeFile(catalogPath, catalog);
} finally {
  await fs.rm(temp, {recursive:true,force:true});
}
