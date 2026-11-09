import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';

const require = createRequire(import.meta.url);
const run = promisify(execFile);
const {definition, installDefinition} = require('../electron/package-definition.cjs');
const {DEFINITIONS, IntegrationHealth, sha256} = require('../electron/integration-health.cjs');
const {installTransaction} = require('../electron/integration-transaction.cjs');
const resources = path.resolve('resources/last-hope-bunker');
const spec = definition(require('../resources/last-hope-bunker/game.json'));
const steam = path.join(process.env['ProgramFiles(x86)'] || 'C:/Program Files (x86)', 'Steam/steamapps/common/Last Hope Bunker');
const assembly = path.join(steam, 'GameAssembly.dll');
const plugin = path.join(steam, spec.base, spec.target);

test('Last Hope Bunker declares seven independent controls and translates them in the signed definition', () => {
  assert.deepEqual(spec.commands.map(({id, index}) => [id, index]), [
    ['health', 0], ['stamina', 1], ['filter', 2], ['resources', 3],
    ['ammo', 4], ['no-reload', 5], ['stealth', 6],
  ]);
  assert.equal(spec.translations['pt-BR']['Infinite ammo'], 'Munição infinita');
  assert.equal(spec.translations['es']['No reload'], 'Sin recarga');
});

test('Last Hope Bunker refuses changed gameplay code and can restore the original plugin', {
  skip: process.platform !== 'win32' || !fs.existsSync(plugin) || !fs.existsSync(assembly),
}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'byte-bunker-install-'));
  DEFINITIONS[spec.gameId] = installDefinition(spec);
  try {
    const dir = path.join(root, spec.base);
    fs.mkdirSync(dir, {recursive:true});
    fs.writeFileSync(path.join(root, spec.exe), '');
    const target = path.join(dir, spec.target);
    const backup = plugin + '.trainerhub-original';
    fs.copyFileSync(fs.existsSync(backup) ? backup : plugin, target);
    const original = await sha256(target);
    const gameplay = path.join(root, 'GameAssembly.dll');
    fs.writeFileSync(gameplay, 'Changed gameplay library');
    const health = new IntegrationHealth(spec.gameId, resources);
    health.setPath(root);
    const adapter = {gamePath:root, resources, health, status:async()=>({gameRunning:false})};
    const install = stage => run(path.join(resources, 'PatchTool.exe'), ['install', stage], {windowsHide:true});
    await assert.rejects(installTransaction(adapter, 'install', install), /gameplay library does not match/);
    assert.equal(await sha256(target), original);

    fs.copyFileSync(assembly, gameplay);
    await installTransaction(adapter, 'install', install);
    assert.equal((await health.inspect(true)).canConnect, true);
    assert.notEqual(await sha256(target), original);
    assert.ok(fs.readFileSync(target).includes(Buffer.from('ByteTrainerRuntime\0')));
    await installTransaction(adapter, 'restore', () => { throw Error('Restore must use the verified backup'); });
    assert.equal(await sha256(target), original);
    assert.ok(spec.payload.every(file => !fs.existsSync(path.join(dir, file))));
  } finally {
    fs.rmSync(root, {recursive:true,force:true});
  }
});
