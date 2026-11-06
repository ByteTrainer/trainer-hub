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
    if (!stopped) log(`Falha na atualização do Hub: ${error.message}`);
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
          type: 'info', title: 'Atualização do ByteTrainer',
          message: `ByteTrainer ${version} disponível`,
          detail: 'Você pode continuar usando o Hub enquanto o instalador é baixado.',
          buttons: ['Baixar atualização', 'Mais tarde'], defaultId: 0, cancelId: 1
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
        type: 'info', title: 'Atualização do ByteTrainer',
        message: `ByteTrainer ${version} pronto para instalar`,
        detail: 'O Hub será reiniciado e os cheats ativos serão desligados.',
        buttons: ['Instalar e reiniciar', 'Mais tarde'], defaultId: 0, cancelId: 1
      });
      if (response !== 0) {
        deferred = version;
      } else if (!stopped && canInstall()) {
        installFailed = false;
        autoUpdater.quitAndInstall(true, true);
        if (installFailed) throw Error('Não foi possível iniciar o instalador.');
        deferred = version;
      }
    } catch {
      deferred = version;
      if (!stopped && !win.isDestroyed()) await dialog.showMessageBox(win, {
        type: 'warning', title: 'Atualização do ByteTrainer',
        message: 'Não foi possível concluir a atualização.',
        detail: 'O Hub tentará verificar a atualização novamente na próxima abertura.'
      }).catch(() => {});
    } finally {
      if (!win.isDestroyed()) win.setProgressBar(-1);
      prompting = false;
    }
  }

  async function check(manual = false) {
    if (stopped || checking || prompting) return 'Uma atualização já está sendo verificada ou preparada.';
    if (manual) deferred = undefined;
    checking = true;
    let message;
    try {
      const result = await autoUpdater.checkForUpdates();
      pending = result?.isUpdateAvailable ? result.updateInfo.version : undefined;
      message = pending ? `ByteTrainer ${pending} disponível.` : 'Você já está na versão mais recente.';
    } catch {
      message = 'Não foi possível verificar as atualizações agora. Tente novamente.';
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
