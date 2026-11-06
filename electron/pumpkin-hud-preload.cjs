const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('pumpkinTracker', {
  onTargets(callback) {
    const listener = (_, targets) => callback(targets);
    ipcRenderer.on('pumpkin-targets', listener);
    return () => ipcRenderer.removeListener('pumpkin-targets', listener);
  }
});
