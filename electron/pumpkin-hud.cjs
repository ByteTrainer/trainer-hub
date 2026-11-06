const path = require('node:path');

class PumpkinHud {
  constructor({ BrowserWindow, screen, root, probe, adapter }) {
    Object.assign(this, { BrowserWindow, screen, root, probe, adapter });
    this.timer = setInterval(() => void this.update(), 200);
    this.timer.unref();
  }

  async update() {
    if (this.updating || this.closed) return;
    const targets = this.adapter.trackerTargets();
    if (!targets.length) {
      this.win?.hide();
      return;
    }
    this.updating = true;
    try {
      const expected = path.resolve(this.adapter.gamePath, 'PumpkinJack/Binaries/Win64/PumpkinJack-Win64-Shipping.exe');
      const game = await this.probe.request({ op: 'foreground' });
      if (this.closed || !game?.bounds || path.normalize(game.exe || '').toLowerCase() !== expected.toLowerCase()) {
        this.win?.hide();
        return;
      }
      if (!this.win) await this.create();
      if (this.closed || !this.adapter.trackerTargets().length) return;
      const area = this.screen.getDisplayMatching(game.bounds).workArea;
      const width = Math.min(390, game.bounds.width - 24, area.width);
      if (width < 220) {
        this.win.hide();
        return;
      }
      const x = Math.round(Math.max(area.x, Math.min(area.x + area.width - width, game.bounds.x + (game.bounds.width - width) / 2)));
      const y = Math.round(Math.max(area.y, game.bounds.y) + 40);
      const current = this.win.getBounds();
      if (current.x !== x || current.y !== y || current.width !== width) this.win.setBounds({ x, y, width, height: 64 });
      this.win.webContents.send('pumpkin-targets', targets);
      if (!this.win.isVisible()) this.win.showInactive();
    } catch {
      if (!this.closed) this.win?.hide();
    } finally {
      this.updating = false;
    }
  }

  async create() {
    const win = this.win = new this.BrowserWindow({
      width: 390, height: 64, show: false, frame: false, transparent: true,
      focusable: false, skipTaskbar: true, resizable: false,
      webPreferences: {
        preload: path.join(this.root, 'electron/pumpkin-hud-preload.cjs'),
        contextIsolation: true, nodeIntegration: false, sandbox: true, backgroundThrottling: false
      }
    });
    win.setAlwaysOnTop(true, 'screen-saver');
    win.setIgnoreMouseEvents(true);
    win.webContents.on('will-navigate', event => event.preventDefault());
    win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    try {
      await win.loadFile(path.join(this.root, 'dist/index.html'), { query: { pumpkinTracker: '1' } });
    } catch (error) {
      if (!win.isDestroyed()) win.destroy();
      this.win = null;
      throw error;
    }
  }

  close() {
    this.closed = true;
    clearInterval(this.timer);
    this.win?.destroy();
    this.win = null;
  }
}

module.exports = { PumpkinHud };
