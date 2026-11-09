using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ByteTrainer.Kawaii;

namespace UnityEngine {
    public class Object { public static void DontDestroyOnLoad(Object value) {} }
    public class MonoBehaviour : Object {
        public bool isActiveAndEnabled = true;
        public GameObject gameObject = new GameObject("");
    }
    public class GameObject : Object {
        public SceneManagement.Scene scene;
        public GameObject(string name) {}
        public T AddComponent<T>() where T : new() { return new T(); }
    }
    public static class Application { public static bool isFocused = true; }
    public static class Time { public static float timeScale = 1f; }
}
namespace UnityEngine.SceneManagement {
    public struct Scene : IEquatable<Scene> {
        public int Id;
        public bool Equals(Scene other) { return Id == other.Id; }
        public override bool Equals(object other) { return other is Scene && Equals((Scene)other); }
        public override int GetHashCode() { return Id; }
        public static bool operator ==(Scene a, Scene b) { return a.Equals(b); }
        public static bool operator !=(Scene a, Scene b) { return !a.Equals(b); }
    }
    public static class SceneManager {
        public static Scene Current;
        public static Scene GetActiveScene() { return Current; }
    }
}
namespace Pippin.kawaiideathudesu.idol {
    public class HabilidadeIdol { public bool HabilidadeAtivada; }
    public class Idol {
        public bool IdolIniciada = true;
        public float ForcaVital = 50, MaximoForcaVital = 100, PoderMagico = 0, MaximoPoderMagico = 25;
        public HabilidadeIdol Habilidade = new HabilidadeIdol();
    }
}
namespace Pippin.save {
    public class SoulsIdentify { public int valor; }
    public class KawaiiSaveFile {
        public List<SoulsIdentify> ganhosSouls = new List<SoulsIdentify>();
        public List<SoulsIdentify> gastosSouls = new List<SoulsIdentify>();
        public void InserirGanhosSouls(int value) { ganhosSouls.Add(new SoulsIdentify {valor = value}); }
        public void InserirGastoSouls(int value) { gastosSouls.Add(new SoulsIdentify {valor = value}); }
        public int RecuperarSoulsDisponiveis() {
            int total = 0;
            foreach (var entry in ganhosSouls) total += entry.valor;
            foreach (var entry in gastosSouls) total -= entry.valor;
            return total;
        }
    }
}
public class ControladorUI : UnityEngine.MonoBehaviour {
    public float DisplayedHealth, DisplayedSpecial;
    public void setForcaVital(float value) { DisplayedHealth = value; }
    public void setPoderMagico(float value) { DisplayedSpecial = value; }
}
public class ControladorIdol {
    public object Ui;
    public Pippin.kawaiideathudesu.idol.Idol getIdol = new Pippin.kawaiideathudesu.idol.Idol();
}
public class KawaiiAutoSave {
    public static KawaiiAutoSave Instance = new KawaiiAutoSave();
    public Pippin.save.KawaiiSaveFile kawaiiSaveFile = new Pippin.save.KawaiiSaveFile();
    public int Saves;
    public void Save() { Saves++; }
}
internal static class KawaiiTests {
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-1102880");
    private const string Session = "0123456789abcdef0123456789abcdef";
    private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    private static void Tick() {
        foreach (string name in new[] {"nextRead", "nextWrite"})
            typeof(Runtime).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0L);
        Runtime.Tick();
    }
    private static void Settings(int mask, string speed, int seconds = 3) {
        File.WriteAllText(Path.Combine(Folder, "settings.txt"), Process.GetCurrentProcess().Id + "|" +
            DateTime.UtcNow.AddSeconds(seconds).Ticks + "|" + mask + "|" + Session + "|1,1," + speed + ",1,1");
    }
    private static void Souls(char token, int amount) {
        File.WriteAllText(Path.Combine(Folder, "resource.txt"), Process.GetCurrentProcess().Id + "|" +
            DateTime.UtcNow.AddSeconds(3).Ticks + "|" + Session + "|" + new string(token, 32) + "|1|" + amount);
    }
    private static void Main() {
        Directory.CreateDirectory(Folder);
        var ui = new ControladorUI();
        var controller = new ControladorIdol {Ui = ui};
        var wallet = KawaiiAutoSave.Instance.kawaiiSaveFile;
        Tick();
        Check(File.Exists(Path.Combine(Folder, "status.txt")), "heartbeat starts before the Hub creates its IPC files");
        Settings(5, "2");
        Souls('a', 120);
        Tick();
        Check(UnityEngine.Time.timeScale == 1 && wallet.RecuperarSoulsDisponiveis() == 0, "menus or multiplayer must never apply changes");
        Runtime.ObserveSolo(ui, controller, true, false, false);
        Tick();
        Check(controller.getIdol.ForcaVital == 100 && ui.DisplayedHealth == 100, "restore health through the solo HUD");
        Check(Runtime.BlockDamage(controller.getIdol) && !Runtime.BlockDamage(new Pippin.kawaiideathudesu.idol.Idol()), "only block damage to the solo idol");
        Check(UnityEngine.Time.timeScale == 2 && Runtime.SuppressRank(), "speed and rank guard");
        Check(wallet.RecuperarSoulsDisponiveis() == 120 && KawaiiAutoSave.Instance.Saves == 1, "soul set uses native ledger and saves");
        Tick();
        Check(KawaiiAutoSave.Instance.Saves == 1, "duplicate action is ignored");
        Souls('b', 25);
        Tick();
        Check(wallet.RecuperarSoulsDisponiveis() == 25 && wallet.gastosSouls.Count == 1, "lower the balance using a native debit");
        Settings(29, "2");
        controller.getIdol.Habilidade.HabilidadeAtivada = true;
        Tick();
        Check(controller.getIdol.PoderMagico == 0, "active special retains its native depletion");
        Check(Runtime.EnemyDamage(1, controller, 5) == 5, "positive hits defeat an enemy at its current health");
        Check(Runtime.EnemyDamage(0, controller, 5) == 0 && Runtime.EnemyDamage(1, new ControladorIdol(), 5) == 1,
            "status-only hits and other controllers are untouched");
        controller.getIdol.Habilidade.HabilidadeAtivada = false;
        Tick();
        Check(controller.getIdol.PoderMagico == 25 && ui.DisplayedSpecial == 25, "refill special and update the HUD when the ability ends");
        Runtime.ObserveSolo(ui, controller, true, true, false);
        UnityEngine.Time.timeScale = 0;
        Tick();
        Check(UnityEngine.Time.timeScale == 0, "pause remains paused");
        Check(Runtime.EnemyDamage(1, controller, 5) == 1, "pause prevents combat modifications");
        UnityEngine.Time.timeScale = 1;
        Runtime.ObserveSolo(ui, controller, true, false, false);
        Tick();
        Check(UnityEngine.Time.timeScale == 2, "resume reapplies the multiplier");
        Settings(5, "2", -1);
        Tick();
        Check(UnityEngine.Time.timeScale == 1 && !Runtime.BlockDamage(controller.getIdol), "expired lease restores native behaviour");
        Check(Runtime.SuppressRank(), "assisted run remains excluded after disabling controls");
        UnityEngine.SceneManagement.SceneManager.Current = new UnityEngine.SceneManagement.Scene {Id = 1};
        Settings(5, "2");
        Souls('c', 999);
        Tick();
        Check(UnityEngine.Time.timeScale == 1 && !Runtime.SuppressRank() && wallet.RecuperarSoulsDisponiveis() == 25, "changing scene clears solo scope");
        Console.WriteLine("Kawaii runtime checks passed.");
    }
}
