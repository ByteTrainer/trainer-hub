const fileName = /^[a-z0-9][a-z0-9_-]*(?:\.[a-z0-9_-]+)+$/i;
const segment = /^[a-z0-9][a-z0-9 _-]*$/i;
const reserved = /^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;

function safeFile(name) {
  return typeof name === 'string' && name.length <= 100 && fileName.test(name) && !reserved.test(name);
}

function label(value, size) {
  return typeof value === 'string' && value.trim() === value && value.length > 0 &&
    value.length <= size && !/[\x00-\x1f\x7f]/.test(value);
}

function definition(value) {
  const { accelerator } = require('./hotkeys.cjs');
  const valid = value && value.schema === 1 && value.driver === 'managed-toggle-v1' &&
    Number.isSafeInteger(value.gameId) && value.gameId > 0 && value.gameId <= 0xffffffff &&
    label(value.name, 100) && safeFile(value.exe) && /\.exe$/i.test(value.exe) &&
    typeof value.base === 'string' && value.base.length <= 160 &&
    value.base.split('/').every(part => segment.test(part) && part.trim() === part && !reserved.test(part)) &&
    safeFile(value.target) && /\.dll$/i.test(value.target) &&
    Array.isArray(value.payload) && value.payload.length >= 1 && value.payload.length <= 10 &&
    value.payload.every(name => safeFile(name) && /\.dll$/i.test(name) && name.toLowerCase() !== value.target.toLowerCase()) &&
    Array.isArray(value.commands) && value.commands.length > 0 && value.commands.length <= 30;
  if (!valid || new Set(value.payload.map(name => name.toLowerCase())).size !== value.payload.length) {
    throw Error('Definição da integração inválida.');
  }
  const ids = new Set(), keys = new Set(['Alt+W']), indices = new Set();
  const commands = value.commands.map(command => {
    if (!command || !/^[a-z][a-z0-9-]{0,39}$/.test(command.id || '') || command.id === 'overlay' ||
        ids.has(command.id) || command.type !== 'toggle' ||
        !Number.isInteger(command.index) || command.index < 0 || command.index >= 30 || indices.has(command.index) ||
        !label(command.name, 75) || !label(command.description, 260)) throw Error('Controle da integração inválido.');
    const key = accelerator(command.key ?? null);
    if (key && keys.has(key)) throw Error('Atalho padrão duplicado.');
    ids.add(command.id);
    indices.add(command.index);
    if (key) keys.add(key);
    return { id: command.id, type: 'toggle', index: command.index, name: command.name, description: command.description, key };
  });
  const { gameId, name, exe, base, target, payload } = value;
  return { schema: 1, driver: 'managed-toggle-v1', gameId, name, exe, base, target, payload, commands };
}

function installDefinition(value) {
  const { exe, base, target, payload } = value;
  return {
    exe, base, target, payload, managed: true, protocolValue: 0,
    backup: target + '.trainerhub-original',
    marker: target + '.trainerhub-sha256',
    releaseFile: 'TrainerHub-' + value.gameId + '.release.json'
  };
}

module.exports = { definition, installDefinition, safeFile, label };
