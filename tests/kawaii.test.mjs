import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';

const require = createRequire(import.meta.url), run = promisify(execFile);
const {definition, installDefinition} = require('../electron/package-definition.cjs');
const {DEFINITIONS, IntegrationHealth, sha256} = require('../electron/integration-health.cjs');
const {installTransaction} = require('../electron/integration-transaction.cjs');
const resources = path.resolve('resources/kawaii');
const spec = definition(require('../resources/kawaii/game.json'));
const game = path.join(process.env['ProgramFiles(x86)'] || 'C:/Program Files (x86)', 'Steam/steamapps/common/Kawaii Deathu Desu/windows_content');

test('Kawaii solo controls restore time scale, apply souls once, and guard public scores', {skip:process.platform !== 'win32'}, async () => {
  assert.equal(spec.gameId, 1102880);
  assert.deepEqual(spec.commands.map(command => command.id), ['health', 'souls', 'game-speed', 'one-hit-kills', 'unlimited-special']);
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'byte-kawaii-runtime-'));
  try {
    const compiler = path.join(process.env.WINDIR, 'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
    const output = path.join(temp, 'KawaiiTests.exe');
    await run(compiler, ['/nologo', '/out:' + output, path.resolve('tests/kawaii-runtime.cs'), path.resolve('native/kawaii/Runtime.cs')]);
    const result = await run(output, [], {env:{...process.env,TEMP:temp,TMP:temp}, timeout:10000});
    assert.match(result.stdout, /Kawaii runtime checks passed/);
  } finally { fs.rmSync(temp,{recursive:true,force:true}); }
});

const installed = path.join(game, spec.base, spec.target);
test('Kawaii installs and restores the verified Steam assembly transactionally', {skip:process.platform !== 'win32' || !fs.existsSync(installed)}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'byte-kawaii-install-'));
  DEFINITIONS[spec.gameId] = installDefinition(spec);
  try {
    const dir = path.join(root,spec.base);
    fs.mkdirSync(dir,{recursive:true});
    fs.writeFileSync(path.join(root,spec.exe),'');
    const backup = installed + '.trainerhub-original';
    const target = path.join(dir,spec.target);
    fs.copyFileSync(fs.existsSync(backup) ? backup : installed,target);
    const before = await sha256(target);
    const health = new IntegrationHealth(spec.gameId,resources);
    health.setPath(root);
    const adapter = {gamePath:root,resources,health,status:async()=>({gameRunning:false})};
    await installTransaction(adapter,'install',stage=>run(path.join(resources,'PatchTool.exe'),['install',stage]));
    assert.equal((await health.inspect(true)).canConnect,true);
    assert.notEqual(await sha256(target),before);
    await installTransaction(adapter,'restore',()=>{throw Error('Restore must use the verified backup');});
    assert.equal(await sha256(target),before);
    assert.ok(spec.payload.every(file=>!fs.existsSync(path.join(dir,file))));
  } finally { fs.rmSync(root,{recursive:true,force:true}); }
});
