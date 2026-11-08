const { autoUpdater } = require('electron-updater');

function startAppUpdates({ win, dialog, canInstall, log }) {
  autoUpdater.autoDownload = false;
  autoUpdater.autoInstallOnAppQuit = false;
  autoUpdater.allowPrerelease = false;
  autoUpdater.disableDifferentialDownload = true;

  let pending;
  let downloaded;
  let deferred;
  let checking = false;
  let prompting = false;
  let stopped = false;
  let installFailed = false;
  const reportError = error => {
    installFailed = true;
    if (!stopped) log(`Hub update failed: ${error.message}`);
  };
  const progress = ({ percent }) => {
    if (!win.isDestroyed()) win.setProgressBar(percent / 100);
  };

  async function offer() {
    if (stopped || prompting || checking || !pending || pending === deferred ||
        win.isDestroyed() || !win.isFocused() || !canInstall()) return;
    prompting = true;
    const version = pending;
    try {
      if (downloaded !== version) {
        const { response } = await dialog.showMessageBox(win, {
          type: 'info', title: 'ByteTrainer update',
          message: `ByteTrainer ${version} is available`,
          detail: 'You can keep using the Hub while the installer downloads.',
          buttons: ['Download update', 'Later'], defaultId: 0, cancelId: 1
        });
        if (response !== 0 || stopped) {
          deferred = version;
          return;
        }
        win.setProgressBar(2);
        await autoUpdater.downloadUpdate();
        downloaded = version;
      }
      if (stopped || !win.isFocused() || !canInstall()) return;
      win.setProgressBar(-1);
      const { response } = await dialog.showMessageBox(win, {
        type: 'info', title: 'ByteTrainer update',
        message: `ByteTrainer ${version} is ready to install`,
        detail: 'The Hub will restart and active cheats will be disabled.',
        buttons: ['Install and restart', 'Later'], defaultId: 0, cancelId: 1
      });
      if (response !== 0) {
        deferred = version;
      } else if (!stopped && canInstall()) {
        installFailed = false;
        autoUpdater.quitAndInstall(true, true);
        if (installFailed) throw Error('Could not start the installer.');
        deferred = version;
      }
    } catch {
      deferred = version;
      if (!stopped && !win.isDestroyed()) await dialog.showMessageBox(win, {
        type: 'warning', title: 'ByteTrainer update',
        message: 'Could not complete the update.',
        detail: 'The Hub will try checking for updates again the next time it opens.'
      }).catch(() => {});
    } finally {
      if (!win.isDestroyed()) win.setProgressBar(-1);
      prompting = false;
    }
  }

  async function check(manual = false) {
    if (stopped || checking || prompting) return 'An update is already being checked or prepared.';
    if (manual) deferred = undefined;
    checking = true;
    let message;
    try {
      const result = await autoUpdater.checkForUpdates();
      pending = result?.isUpdateAvailable ? result.updateInfo.version : undefined;
      message = pending ? `ByteTrainer ${pending} disponível.` : 'You already have the latest version.';
    } catch {
      message = 'Could not check for updates now. Try again.';
    } finally {
      checking = false;
    }
    void offer();
    return message;
  }

  autoUpdater.on('error', reportError);
  autoUpdater.on('download-progress', progress);
  win.on('focus', offer);
  const retry = setInterval(offer, 15000);
  const interval = setInterval(() => void check(), 6 * 60 * 60 * 1000);
  void check();
  function stop() {
    stopped = true;
    clearInterval(interval);
    clearInterval(retry);
    win.removeListener('focus', offer);
    autoUpdater.removeListener('download-progress', progress);
  }
  return { stop, check: () => check(true) };
}

module.exports = { startAppUpdates };
