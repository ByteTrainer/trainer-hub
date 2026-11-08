const fs = require('node:fs');
const fsp = fs.promises;
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const run = require('node:util').promisify(require('node:child_process').execFile);
const { DEFINITIONS, IntegrationHealth, connectionDiagnostic, read, recipe, sha256 } = require('./integration-health.cjs');
const { commit, recover } = require('./integration-transaction.cjs');
const { writeText } = require('./atomic.cjs');

const ID = 1186640;
const definition = DEFINITIONS[ID];
const files = [...definition.payload, definition.releaseFile];
const loaderHash = '4B47D4BCEDDD2F561A4E395BFA00924CCFC945AF576A2D0C613E6537846C57EC';
const exeName = path.basename(definition.exe);
const mod = 'Mods/TrainerHubPumpkin';
const capabilityIds = require('./commands.json')[ID].map(command => command.id);
const TRACKER = capabilityIds.indexOf('tracker');

function speed(value) {
  if (!Number.isInteger(value) || value < 50 || value > 250 || value % 25 !== 0) {
    throw Error('Choose a speed between 50% and 250%, in steps of 25%.');
  }
  return value;
}

async function regularFile(root, name) {
  let current = root;
  const parts = name.split('/');
  for (const [index, part] of parts.entries()) {
    current = path.join(current, part);
    const info = await fsp.lstat(current).catch(error => {
      if (error.code === 'ENOENT') return null;
      throw error;
    });
    if (!info) return false;
    if (info.isSymbolicLink() || !(index === parts.length - 1 ? info.isFile() : info.isDirectory())) {
      throw Error('The integration folder contains an unsupported path.');
    }
  }
  return true;
}

class PumpkinHealth extends IntegrationHealth {
  async inspectNow(force) {
    const r = recipe(this.resources);
    const result = { version: r.version, release: r.release, canInstall: false, canRestore: false, canConnect: false };
    const status = (state, label, message, fields) => ({ ...result, state, label, message, ...fields });
    if (!this.gamePath || !fs.existsSync(path.join(this.gamePath, definition.exe))) {
      return status('not-found', 'Game not found', 'Detect games installed through Steam.');
    }
    try {
      const bin = path.join(this.gamePath, definition.base);
      const steam = await fsp.readFile(path.resolve(this.gamePath, '../../appmanifest_1186640.acf'), 'utf8').catch(() => '');
      result.buildId = steam.match(/"buildid"\s+"(\d+)"/i)?.[1];
      if (Number(steam.match(/"StateFlags"\s+"(\d+)"/i)?.[1]) & 2) {
        return status('updating', 'Steam is updating the game', 'Wait for the update to finish.');
      }
      if (fs.existsSync(path.join(this.gamePath, '.trainerhub-transaction.json'))) {
        return status('recovery-needed', 'Interrupted installation', 'Use Recover installation with the game closed.');
      }
      const present = new Set();
      for (const file of files) if (await regularFile(bin, file)) present.add(file);
      const installed = present.has(definition.releaseFile) ? read(path.join(bin, definition.releaseFile)) : null;
      if (present.size && (installed?.gameId !== ID || !Number.isSafeInteger(installed.release))) {
        return status('conflict', 'Files from another mod detected', 'Remove the external UE4SS installation to avoid overwriting your files.');
      }
      if (installed) {
        for (const file of definition.payload) {
          if (present.has(file) && await this.hash(path.join(bin, file), force) !== installed.files?.[file]) {
            return status('conflict', 'Integration files were modified', 'Modified files were preserved.');
          }
        }
        result.canRestore = true;
        result.installedRelease = installed.release;
      }
      const otherMods = (await fsp.readdir(path.join(bin, 'Mods')).catch(error => {
        if (error.code === 'ENOENT') return [];
        throw error;
      })).some(name => name.toLowerCase() !== 'trainerhubpumpkin');
      if (otherMods) {
        return status('conflict', 'Other mods detected', 'Remove other mods before using the Hub integration.');
      }
      const originalHash = await this.hash(path.join(this.gamePath, definition.exe), force);
      const pak = path.join(this.gamePath, 'PumpkinJack/Content/Paks/PumpkinJack-WindowsNoEditor.pak');
      if (!r.supportedOriginalHashes.includes(originalHash) || await this.hash(pak, force) !== r.assetHash) {
        return status('unvalidated', 'Version not yet validated', 'Game files differ from the tested version.');
      }
      if (installed?.release > r.release) {
        return status('newer-package-required', 'Newer package required', 'Update the Hub before using this integration.');
      }
      if (!installed || present.size !== files.length || installed.release !== r.release) {
        return status(installed ? 'outdated' : 'not-installed', 'Integration available',
          'Install the integration with the game closed. The original executable will be preserved.', { canInstall: true });
      }
      return status('compatible', 'Compatible', 'Integration files verified.', { canConnect: true });
    } catch (error) {
      return status('unavailable', 'Could not verify', error.message);
    }
  }
}

class PumpkinAdapter {
  constructor(resources, log, folder) {
    Object.assign(this, { resources, log, folder, multiplier: 150, options: Array(capabilityIds.length).fill(false), owned: false });
    this.health = new PumpkinHealth(ID, resources);
    this.timer = setInterval(() => {
      if (!this.owned) return;
      try { this.write(); } catch (error) {
        this.error = error.message;
        this.disconnect();
      }
    }, 1000);
    this.timer.unref();
  }

  setPath(gamePath) {
    if (gamePath !== this.gamePath) this.disconnect();
    this.gamePath = gamePath;
    this.health.setPath(gamePath);
  }

  write() {
    fs.mkdirSync(this.folder, { recursive: true });
    const file = path.join(this.folder, 'trainer-hub-pumpkin-settings.txt');
    const expiration = this.owned ? Math.floor(Date.now() / 1000) + 4 : 0;
    writeText(file, [expiration, this.token, ...this.options.map(Number), this.multiplier].join(' '));
  }

  async gameRunning() {
    if (!this.gamePath) return false;
    this.scan ??= run('tasklist.exe', ['/FI', 'IMAGENAME eq ' + exeName, '/FO', 'CSV', '/NH'], {
      windowsHide: true, timeout: 5000
    }).then(({ stdout }) => {
      const match=[...stdout.matchAll(/"([^"]+)","(\d+)"/g)].find(m=>m[1].toLowerCase()===exeName.toLowerCase());
      this.runningPid=match?Number(match[2]):null;
      return !!match;
    })
      .finally(() => { this.scan = null; });
    return this.scan;
  }

  readStatus() {
    try {
      const text = fs.readFileSync(path.join(this.folder, 'trainer-hub-pumpkin-status.txt'), 'utf8');
      const match = /^2 (\d+) ([01]) ([01]) ([a-f0-9]{32}|-) ([01]) (-?\d{1,6}) (-?\d{1,3}) (-?\d{1,6}) (-?\d{1,3})$/.exec(text);
      const age = Math.floor(Date.now() / 1000) - Number(match?.[1]);
      if (match && age >= 0 && age <= 3) {
        const targets = ['skull', 'vinyl'].flatMap((kind, index) => {
          const distance = Number(match[6 + index * 2]), angle = Number(match[7 + index * 2]);
          return distance >= 0 && Math.abs(angle) <= 180 ? [{ kind, distance, angle }] : [];
        });
        return { ready: match[2] === '1', damage: match[3] === '1', failed: match[5] === '1', token: match[4], targets };
      }
    } catch {}
    return null;
  }

  trackerTargets() {
    if (!this.owned || !this.options[TRACKER]) {
      this.lastTargets = null;
      return [];
    }
    const state = this.readStatus();
    if (!state) {
      const last = this.lastTargets;
      return last?.token === this.token && Date.now() - last.time < 550 ? last.targets : [];
    }
    const targets = state.ready && !state.failed && state.token === this.token ? state.targets : [];
    this.lastTargets = { time: Date.now(), token: this.token, targets };
    return targets;
  }

  async status() {
    const [compatibility, gameRunning] = await Promise.all([this.health.inspect(), this.gameRunning().catch(() => false)]);
    if (!compatibility.canConnect && this.owned) this.disconnect();
    const live = gameRunning && compatibility.canConnect ? this.readStatus() : null;
    if (this.owned && (live?.failed || (this.seenGame && !gameRunning))) this.disconnect();
    this.seenGame = this.owned && gameRunning;
    const capabilities = Object.fromEntries(capabilityIds.map(id => [id, {
      available: !!live?.ready && (id !== 'one-hit' || live.damage),
      reason: live?.ready ? 'The game has not loaded its damage controls yet.' : 'Enter a level with Jack in his body.'
    }]));
    this.capabilities = capabilities;
    const state = { installed: compatibility.canConnect, compatibility, gameRunning, runtimeSession: live && this.runningPid ? String(this.runningPid) : null, path: this.gamePath, connected: !!live,
      ready: !!live?.ready, owned: this.owned, options: [...this.options], capabilities, multiplier: this.multiplier,
      error: this.error || (live?.failed ? 'Character controls stopped responding.' : null) };
    return { ...state, diagnostic: connectionDiagnostic(compatibility, state) };
  }

  async connect() {
    const status = await this.health.inspect(true);
    if (!status.canConnect) throw Error(status.message);
    this.options.fill(false);
    this.token = crypto.randomBytes(16).toString('hex');
    this.owned = true;
    this.error = null;
    try { this.write(); } catch (error) { this.disconnect(); throw error; }
    return this.status();
  }

  set(index, value) {
    if (!this.owned || !Number.isInteger(index) || !capabilityIds[index] || typeof value !== 'boolean') throw Error('Control unavailable.');
    const live = this.readStatus();
    if (value && (!live?.ready || (index === 3 && !live.damage))) throw Error('The game has not made this control available yet.');
    const previous = this.options[index];
    this.options[index] = value;
    try { this.write(); } catch (error) { this.options[index] = previous; throw error; }
    return { options: [...this.options] };
  }

  setMultiplier(value) {
    const previous = this.multiplier;
    this.multiplier = speed(value);
    try { if (this.owned) this.write(); } catch (error) { this.multiplier = previous; throw error; }
    return this.multiplier;
  }

  disconnect() {
    const wasOwned = this.owned;
    this.owned = false;
    this.options.fill(false);
    if (wasOwned) try { this.write(); } catch {}
  }

  async patch(command) {
    if (!this.gamePath || !['install', 'restore', 'recover'].includes(command)) throw Error('Invalid operation.');
    if (await this.gameRunning()) throw Error('Close Pumpkin Jack before changing the integration.');
    this.disconnect();
    if (command === 'recover') {
      const bin = path.join(this.gamePath, definition.base);
      for (const file of files) await regularFile(bin, file);
      await recover(this.gamePath, ID);
      this.health.hashes.clear();
      return this.status();
    }
    const info = await this.health.inspect(true);
    if (!(command === 'install' ? info.canInstall : info.canRestore)) throw Error(info.message);
    const stage = await fsp.mkdtemp(path.join(os.tmpdir(), 'trainer-pumpkin-'));
    try {
      if (command === 'install') {
        const archive = path.join(this.resources, 'UE4SS_v3.0.1.zip');
        if (await sha256(archive) !== loaderHash) throw Error('The loader file was modified.');
        await run('tar.exe', ['-xf', archive, '-C', stage, 'UE4SS.dll', 'dwmapi.dll', 'UE4SS-settings.ini'], {
          windowsHide: true, timeout: 30000
        });
        const settings = path.join(stage, 'UE4SS-settings.ini');
        const ini = (await fsp.readFile(settings, 'utf8')).replace(/^(ConsoleEnabled|GuiConsoleEnabled|GuiConsoleVisible|EnableHotReloadSystem)\s*=.*$/gm, '$1 = 0');
        await fsp.writeFile(settings, ini);
        await fsp.mkdir(path.join(stage, mod, 'Scripts'), { recursive: true });
        await fsp.writeFile(path.join(stage, mod, 'enabled.txt'), '');
        for (const file of ['main.lua', 'engine.lua']) {
          await fsp.copyFile(path.join(this.resources, file), path.join(stage, mod, 'Scripts', file));
        }
        const hashes = {};
        for (const file of definition.payload) hashes[file] = await sha256(path.join(stage, file));
        await fsp.writeFile(path.join(stage, definition.releaseFile), JSON.stringify({ gameId: ID, release: info.release, files: hashes }));
      }
      if (await this.gameRunning()) throw Error('The game opened during preparation. Close it and try again.');
      await commit(this.gamePath, ID, stage, files);
      this.health.hashes.clear();
    } finally {
      await fsp.rm(stage, { recursive: true, force: true });
    }
    this.log(command === 'install' ? 'Pumpkin Jack integration installed.' : 'Pumpkin Jack integration removed.');
    return this.status();
  }

  close() {
    this.disconnect();
    clearInterval(this.timer);
  }
}

module.exports = { PumpkinAdapter, ID, speed };
