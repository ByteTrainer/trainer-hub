using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Pippin.kawaiideathudesu.idol;
using Pippin.save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ByteTrainer.Kawaii {
    public sealed class Bridge : MonoBehaviour {
        private void Update() { Runtime.Tick(); }
        private void OnApplicationFocus(bool focused) { Runtime.SetFocus(focused); }
        private void OnApplicationPause(bool paused) { Runtime.SetFocus(!paused && Application.isFocused); }
        private void OnDestroy() { Runtime.Shutdown(); }
    }

    public static class Runtime {
        private const int MaxSouls = 999999999;
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-1102880");
        private static readonly int Pid = Process.GetCurrentProcess().Id;
        private static readonly object Gate = new object();
        private static Bridge bridge;
        private static Timer heartbeat;
        private static ControladorUI soloUi;
        private static ControladorIdol soloController;
        private static bool started, paused, ended, assisted, focused, ready;
        private static int flags;
        private static Scene assistedScene;
        private static long deadline, nextRead, nextWrite, lastTick, applied;
        private static string session = "", ack = "", error = "";
        private static float speed = 2f, baseScale, appliedScale;
        private static bool ownsScale;

        public static void Boot() {
            if (bridge != null) return;
            var host = new GameObject("ByteTrainer.Kawaii");
            UnityEngine.Object.DontDestroyOnLoad(host);
            bridge = host.AddComponent<Bridge>();
            focused = Application.isFocused;
            heartbeat = new Timer(delegate { BackgroundTick(); }, null, 250, 250);
        }

        public static void ObserveSolo(ControladorUI ui, ControladorIdol controller, bool playing, bool pause, bool finished) {
            if (ui != soloUi || (ended && !finished)) assisted = false;
            soloUi = ui;
            soloController = controller;
            started = playing;
            paused = pause;
            ended = finished;
        }

        private static bool SoloReady() {
            return started && !ended && soloUi != null && soloUi.isActiveAndEnabled &&
                soloUi.gameObject.scene == SceneManager.GetActiveScene() && soloController != null &&
                (soloController.Ui as ControladorUI) == soloUi && soloController.getIdol != null && soloController.getIdol.IdolIniciada &&
                soloController.getIdol.ForcaVital > 0;
        }

        private static void MarkAssisted() {
            assisted = true;
            assistedScene = SceneManager.GetActiveScene();
        }

        public static bool SuppressRank() {
            return assisted && assistedScene == SceneManager.GetActiveScene();
        }

        public static bool BlockDamage(Idol target) {
            lock (Gate) {
                if ((flags & 1) == 0 || deadline <= DateTime.UtcNow.Ticks || !SoloReady() ||
                    paused || target != soloController.getIdol) return false;
                MarkAssisted();
                return true;
            }
        }

        public static void SetFocus(bool value) { lock (Gate) focused = value; }

        public static void Shutdown() {
            lock (Gate) {
                if (heartbeat != null) heartbeat.Dispose();
                heartbeat = null;
                ReleaseSpeed();
            }
        }

        private static string Read(string name) {
            string file = Path.Combine(Folder, name);
            if (!File.Exists(file)) return "";
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd().Trim();
        }

        private static void ReadSettings(long now) {
            if (now < nextRead) return;
            nextRead = now + TimeSpan.TicksPerMillisecond * 100;
            try {
                var parts = Read("settings.txt").Split('|');
                long expiry;
                int requested;
                float multiplier;
                if (parts.Length != 5 || parts[0] != Pid.ToString() || !long.TryParse(parts[1], out expiry) ||
                    expiry <= now || expiry > now + TimeSpan.TicksPerSecond * 5 || !int.TryParse(parts[2], out requested) ||
                    (requested & ~5) != 0 || parts[3].Length != 32) throw new FormatException();
                var values = parts[4].Split(',');
                if (values.Length != 3 || values[0] != "1" || values[1] != "1" ||
                    !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier) ||
                    float.IsNaN(multiplier) || multiplier < .25f || multiplier > 3f) throw new FormatException();
                if (session != parts[3]) {
                    session = parts[3];
                    ack = error = "";
                    applied = 0;
                }
                flags = requested;
                deadline = expiry;
                speed = multiplier;
            } catch (FormatException) { flags = 0; deadline = 0; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.IO.IsolatedStorage.IsolatedStorageException) { }
        }

        private static void ReleaseSpeed() {
            if (ownsScale && Time.timeScale == appliedScale) Time.timeScale = baseScale;
            ownsScale = false;
        }

        private static void UpdateSpeed(bool enabled) {
            if (!enabled || Time.timeScale <= 0 || speed == 1f) {
                ReleaseSpeed();
                return;
            }
            if (!ownsScale || Time.timeScale != appliedScale) baseScale = Time.timeScale;
            appliedScale = baseScale * speed;
            Time.timeScale = appliedScale;
            ownsScale = true;
            MarkAssisted();
        }

        private static long Sum(System.Collections.Generic.List<SoulsIdentify> entries) {
            long total = 0;
            foreach (var entry in entries) total += entry.valor;
            return total;
        }

        private static void ApplySouls(long now) {
            string[] parts;
            try { parts = Read("resource.txt").Split('|'); }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            catch (System.IO.IsolatedStorage.IsolatedStorageException) { return; }
            long expiry, amount;
            if (parts.Length != 6 || parts[0] != Pid.ToString() || !long.TryParse(parts[1], out expiry) ||
                expiry <= now || expiry > now + TimeSpan.TicksPerSecond * 5 || parts[2] != session ||
                parts[3].Length != 32 || parts[3] == ack || parts[4] != "1" || !long.TryParse(parts[5], out amount)) return;
            ack = parts[3];
            applied = 0;
            error = "";
            if (amount < 0 || amount > MaxSouls) {
                error = "Enter a soul balance between 0 and 999999999.";
                return;
            }
            try {
                var save = KawaiiAutoSave.Instance;
                if (save == null || save.kawaiiSaveFile == null) {
                    error = "The game save is not ready.";
                    return;
                }
                var wallet = save.kawaiiSaveFile;
                long earned = Sum(wallet.ganhosSouls), spent = Sum(wallet.gastosSouls);
                long delta = amount - (earned - spent);
                if (earned < 0 || spent < 0 || earned + Math.Max(0, delta) > int.MaxValue ||
                    spent + Math.Max(0, -delta) > int.MaxValue) {
                    error = "The soul balance exceeds the game's safe range.";
                    return;
                }
                if (delta != 0) {
                    if (delta > 0) wallet.InserirGanhosSouls((int)delta);
                    else wallet.InserirGastoSouls((int)-delta);
                    MarkAssisted();
                    save.Save();
                }
                applied = wallet.RecuperarSoulsDisponiveis();
                if (applied != amount) error = "Check the current soul balance before trying again.";
            } catch (Exception) { error = "Check the current soul balance before trying again."; }
        }

        private static void Publish(long now) {
            if (now < nextWrite) return;
            nextWrite = now + TimeSpan.TicksPerMillisecond * 250;
            string file = Path.Combine(Folder, "status.txt");
            try {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(file + ".new", "2|" + Pid + "|" + now + "|" + (ready ? 1 : 0) +
                    "|" + (deadline > now && ready ? flags : 0) + "|" + session + "|" + ack + "|" + applied + "|" + error);
                if (File.Exists(file)) File.Replace(file + ".new", file, null);
                else File.Move(file + ".new", file);
            } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.IO.IsolatedStorage.IsolatedStorageException) { }
        }

        private static void BackgroundTick() {
            lock (Gate) {
                long now = DateTime.UtcNow.Ticks;
                if (heartbeat == null || focused || now - lastTick < TimeSpan.TicksPerMillisecond * 750) return;
                // Read IPC and publish the last main-thread observation while Unity is suspended.
                ReadSettings(now);
                Publish(now);
            }
        }

        public static void Tick() {
            lock (Gate) {
                long now = DateTime.UtcNow.Ticks;
                lastTick = now;
                ReadSettings(now);
                ready = SoloReady();
                bool active = ready && deadline > now;
                UpdateSpeed(active && !paused && (flags & 4) != 0);
                if (active) {
                    var idol = soloController.getIdol;
                    if ((flags & 1) != 0 && !paused && idol.ForcaVital < idol.MaximoForcaVital) {
                        idol.ForcaVital = idol.MaximoForcaVital;
                        soloUi.setForcaVital(idol.ForcaVital);
                        MarkAssisted();
                    }
                    if (now >= nextWrite) ApplySouls(now);
                }
                Publish(now);
            }
        }
    }
}
