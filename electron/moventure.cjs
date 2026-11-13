const fs = require('node:fs');
const path = require('node:path');
const { execFile } = require('node:child_process');
const run = require('node:util').promisify(execFile);
const { writeText } = require('./atomic.cjs');

const ID = 3826060;
const temp = require('node:os').tmpdir();
const epoch = 621355968000000000n;

class MoventureAdapter {
  constructor(resources, log, definition = {gameId:ID,exe:"Moventure.exe",commands:[{index:0},{index:1}]}) {
    Object.assign(this, { resources, log, definition, options: Array(Math.max(...definition.commands.map(c=>c.index))+1).fill(false), owned: false, folder: path.join(temp,definition.gameId===ID?"TrainerHub-Moventure":`TrainerHub-${definition.gameId}`) });
    this.timer = setInterval(() => {
      this.heartbeat();
    }, 1000);
    this.timer.unref();
  }

  setPath(gamePath) {
    if (gamePath !== this.gamePath) {
      this.disconnect();
      this.verifiedPid = null;
      this.processCache = null;
      this.statusText = null;
    }
    this.gamePath = gamePath;
  }

  readStatus() {
    try {
      const parts = this.readStatusText().split('|');
      if (parts.length !== 5 || parts[0] !== '1' || !/^\d+$/.test(parts[1]) ||
          !/^\d+$/.test(parts[2]) || !/^[01]$/.test(parts[3]) || !/^\d{1,10}$/.test(parts[4]) || Number(parts[4]) >= 2**30) return null;
      const time = Number((BigInt(parts[2]) - epoch) / 10000n);
      const age = Date.now() - time;
      return age >= 0 && age < 2000 ? { pid: Number(parts[1]), ready: parts[3] === '1', flags: Number(parts[4]) } : null;
    } catch {
      return null;
    }
  }

  readStatusText() {
    try {
      const text = fs.readFileSync(path.join(this.folder, 'status.txt'), 'utf8');
      this.statusText = text;
      return text;
    } catch (error) {
      // The parsers still enforce the original two-second runtime timestamp.
      if (['EACCES', 'EPERM', 'EBUSY'].includes(error.code) && this.statusText) return this.statusText;
      throw error;
    }
  }

  heartbeat() {
    if (!this.owned) return;
    try { this.write(); this.writeFailures = 0; }
    catch (error) {
      this.writeFailures = (this.writeFailures || 0) + 1;
      if (['EACCES', 'EPERM', 'EBUSY'].includes(error.code) && this.writeFailures === 1) return;
      this.error = 'Could not send controls to the game.';
      this.disconnect();
    }
  }

  processAlive(pid) {
    try { process.kill(pid, 0); return true; }
    catch (error) { return error.code === 'ESRCH' ? false : null; }
  }

  async scanProcesses() {
    if (this.processCache && Date.now() - this.processCache.time < 3000) return this.processCache.pids;
    const executable = path.basename(this.definition.exe);
      this.scan ??= run('tasklist.exe', ['/FI', `IMAGENAME eq ${executable}`, '/FO', 'CSV', '/NH'],
        { windowsHide: true, timeout: 5000 })
        .then(({ stdout }) => [...stdout.matchAll(/"([^"]+)","(\d+)"/g)].filter(match=>match[1].toLowerCase()===executable.toLowerCase()).map(match => Number(match[2])))
        .then(pids => { this.processCache = {time: Date.now(), pids}; return pids; })
        .catch(() => null).finally(() => { this.scan = null; });
      return this.scan;
  }

  async status() {
    const gamePath = this.gamePath;
    let pids = [];
    if (gamePath) {
      // A verified runtime PID can be checked without launching tasklist every poll.
      // A timeout or access error is unknown, never proof that the game exited.
      const alive = this.verifiedPid ? this.processAlive(this.verifiedPid) : false;
      if (alive !== false && this.verifiedPid) pids = [this.verifiedPid];
      else {
        if (this.verifiedPid) {
          this.verifiedPid = null;
          this.processCache = null;
          if (this.owned) this.disconnect();
        }
        pids = await this.scanProcesses();
      }
    }
    if (gamePath !== this.gamePath) return this.status();
    const state = this.readStatus();
    const connected = !!state && !!pids?.includes(state.pid);
    if (connected) this.verifiedPid = state.pid;
    if (this.owned && pids && !pids.includes(this.pid)) this.disconnect();
    return { connected, runtimeSession: connected ? String(state.pid) : null, gameRunning: pids ? pids.length > 0 : this.owned, ready: connected && state.ready,
      owned: this.owned, options: [...this.options], path: this.gamePath, error: this.error || null };
  }

  write() {
    fs.mkdirSync(this.folder, { recursive: true });
    const deadline = this.owned ? epoch + BigInt(Date.now() + 3500) * 10000n : 0n;
    const flags = this.options.reduce((mask, on, index) => mask + (on ? 2**index : 0), 0);
    writeText(path.join(this.folder, 'settings.txt'), [this.pid, deadline, flags].join('|'));
  }

  async connect() {
    const current = await this.status();
    if (!current.connected) throw Error('Launch the game through Steam and wait for the integration to load.');
    const state = this.readStatus();
    if (!state) throw Error('The game has not responded yet. Try connecting again.');
    this.pid = state.pid;
    this.options.fill(false);
    this.owned = true;
    this.error = null;
    this.writeFailures = 0;
    try { this.write(); } catch (error) { this.disconnect(); throw error; }
    return this.status();
  }

  set(index, value) {
    if (!this.owned || !this.definition.commands.some(command=>command.index===index) || typeof value !== 'boolean') throw Error('Control unavailable.');
    const state = this.readStatus();
    if (value && (state?.pid !== this.pid || !state.ready)) throw Error('Enter a level with a living character.');
    const previous = this.options[index];
    this.options[index] = value;
    try { this.write(); } catch (error) { this.options[index] = previous; throw error; }
    return { options: [...this.options] };
  }

  disconnect() {
    const wasOwned = this.owned;
    this.owned = false;
    this.options.fill(false);
    if (wasOwned) try { this.write(); } catch {}
  }

  close() {
    this.disconnect();
    clearInterval(this.timer);
  }
}

module.exports = { ID, MoventureAdapter };
