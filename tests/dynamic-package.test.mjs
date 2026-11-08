import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';

const require = createRequire(import.meta.url);
const {IntegrationUpdates, digest} = require('../electron/integration-updates.cjs');
const {definition} = require('../electron/package-definition.cjs');

test('a signed package adds an unknown game and supports a later revision with another control', async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'thub-dynamic-test-'));
  const {publicKey, privateKey} = crypto.generateKeyPairSync('ed25519');
  const gameId = 4000123;
  const spec = {schema:1, driver:'managed-toggle-v1', gameId, name:'Fixture', exe:'Fixture.exe',
    base:'Fixture_Data/Managed', target:'Fixture.dll', payload:['Fixture.Runtime.dll'],
    commands:[{id:'health', index:0, type:'toggle', key:'F1', name:'Vida', description:'Protege o jogador.'}]};
  const create = (release, game) => {
    const recipe = {gameId, release, version:'1.0.0', supportedOriginalHashes:['a'.repeat(64)],
      cheats:game.commands.map(command=>command.id)};
    const payload = Buffer.from(JSON.stringify({kind:'integration', gameId, release, version:'1.0.0',
      minimumHubVersion:'1.0.0', files:Object.entries({
        'game.json':JSON.stringify(game), 'recipe.json':JSON.stringify(recipe),
        'PatchTool.exe':'fixture', 'Mono.Cecil.dll':'fixture', 'Mono.Cecil.LICENSE.txt':'fixture',
        'Fixture.Runtime.dll':'fixture'
      }).map(([name, source])=>({name,sha256:digest(source),data:Buffer.from(source).toString('base64')}))}));
    return Buffer.from(JSON.stringify({algorithm:'Ed25519',payload:payload.toString('base64'),
      signature:crypto.sign(null,payload,privateKey).toString('base64')}));
  };
  try {
    const options = {root,publicKey,bundled:{},hubVersion:'1.0.0'};
    const updates = new IntegrationUpdates(options);
    const first = create(1,spec);
    assert.equal((await updates.importBytes(first)).gameId,gameId);
    const next = {...spec,commands:[...spec.commands,{id:'jump',index:7,type:'toggle',key:'F2',name:'Pulo',description:'Pula novamente.'}]};
    await updates.importBytes(create(2,next));
    await assert.rejects(updates.importBytes(first),/older/);
    await assert.rejects(updates.importBytes(create(3,{...next,base:'../outside'})),/definition/);
    const reopened = new IntegrationUpdates(options);
    const [folder] = await reopened.discovered();
    const active = definition(JSON.parse(await fs.readFile(path.join(folder,'game.json'),'utf8')));
    assert.deepEqual(active.commands.map(command=>command.index),[0,7]);
    const old = new IntegrationUpdates({...options,hubVersion:'0.9.1'});
    await assert.rejects(old.importBytes(first),/Update/);
  } finally {
    await fs.rm(root,{recursive:true,force:true});
  }
});
