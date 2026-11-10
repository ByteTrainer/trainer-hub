using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ByteTrainer.DungeonOfTheEndless {
    public static class Runtime {
        private const int MaxBalance = 9999999;
        private const int InstantResearchFlag = 1 << 11;
        private const int RefreshFlags = (1 << 5) | (1 << 9) | (1 << 10) | InstantResearchFlag | (1 << 13);
        private const string ExpirationMethod = "ByteTrainerExpireActiveSkill";
        private static readonly MethodInfo ResearchComplete =
            typeof(Artifact).GetMethod("OnResearchComplete", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ModuleDungeon =
            typeof(global::Module).GetField("dungeon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-249050");
        private static readonly string Pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
        private static readonly PropertyInfo[] Stocks = {
            typeof(Player).GetProperty("IndustryStock"), typeof(Player).GetProperty("ScienceStock"),
            typeof(Player).GetProperty("FoodStock")
        };
        private static Dungeon active;
        private static GameNetworkManager network;
        private static long deadline, nextPoll;
        private static int flags, visibleFlags, applied;
        private static float resources = 2f, movement = 2f;
        private static string session = "", ack = "", error = "";
        private static bool adjustingDust;

        public static void Enter(Dungeon dungeon, GameNetworkManager manager) {
            active = dungeon;
            network = manager;
        }

        public static void Leave(Dungeon dungeon) {
            if (!object.ReferenceEquals(active, dungeon)) return;
            active = null;
            network = null;
            visibleFlags = 0;
        }

        private static bool Ready() {
            if (active == null || active.IsLevelOver || network == null || Player.LocalPlayer == null) return false;
            var gameSession = network.GetSession();
            return gameSession != null && gameSession.SessionMode == SessionMode.Single && !gameSession.IsAborted;
        }

        private static bool Enabled(int index) {
            return (flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < deadline && Ready();
        }

        public static float HealthChange(float amount, Health health, Dungeon dungeon, bool ignoreInvincibility) {
            if (!(amount < 0f) || !object.ReferenceEquals(dungeon, active)) return amount;
            var actor = health.SimMB;
            if (!ignoreInvincibility && actor is Hero && Enabled(0)) return 0f;
            var monster = actor as Mob;
            if (monster != null && monster.Tamer == null && Enabled(6)) return Math.Min(amount, -health.GetHealth());
            return amount;
        }

        public static float ConstructionCost(float original) { return Enabled(5) ? 0f : original; }
        public static float ResearchCost(float original) { return Enabled(9) ? 0f : original; }
        public static int HealingCost(int original) { return Enabled(10) ? 0 : original; }

        public static int ResearchDoorCount(int actual) { return Enabled(11) ? Math.Max(1, actual) : actual; }

        public static void CompleteResearch(Artifact artifact) {
            if (Enabled(11) && artifact != null && artifact.ResearchedBP != null
                && object.ReferenceEquals(ModuleDungeon.GetValue(artifact), active))
                ResearchComplete.Invoke(artifact, null);
        }

        public static int CooldownThreshold(int original) { return Enabled(13) ? int.MinValue : original; }
        public static int CooldownRemaining(int original) { return Enabled(13) ? 0 : original; }

        public static bool HoldTurnSkill(ActiveSkill skill, Dungeon dungeon) {
            return object.ReferenceEquals(dungeon, active) && Enabled(12) && skill.Config.DeactivateOnNewTurn;
        }

        public static void CancelSkillExpiration(ActiveSkill skill) {
            skill.CancelInvoke(ExpirationMethod);
        }

        public static bool DeferSkillExpiration(ActiveSkill skill, Dungeon dungeon) {
            CancelSkillExpiration(skill);
            if (!object.ReferenceEquals(dungeon, active) || !Enabled(12)) return false;
            skill.IsActivated = true;
            skill.Invoke(ExpirationMethod, 0.25f);
            return true;
        }

        public static void SubscribeCooldown(IGameEventService events, DungeonTurnChangedEventHandler handler) {
            events.OnDungeonTurnChanged -= handler;
            events.OnDungeonTurnChanged += handler;
        }

        public static void SubscribeTurnExpiration(IGameEventService events, Action handler) {
            events.OnRoomEventFinished -= handler;
            events.OnRoomEventFinished += handler;
        }

        public static float Movement(float original, SimMonoBehaviour actor, Dungeon dungeon) {
            return actor is Hero && object.ReferenceEquals(dungeon, active) && Enabled(8)
                ? original * movement : original;
        }

        public static void AwardFids(Player player, float amount, FIDS kind, bool feedback,
            bool refresh, bool checkLocal, bool transfer) {
            player.AddFIDS(ScaleFids(player, amount), kind, feedback, refresh, checkLocal, transfer);
        }

        public static void AwardFood(Player player, float amount, bool feedback, bool refresh, bool checkLocal, bool transfer) {
            player.AddFood(ScaleFids(player, amount), feedback, refresh, checkLocal, transfer);
        }

        public static void AwardIndustry(Player player, float amount, bool feedback, bool refresh, bool checkLocal, bool transfer) {
            player.AddIndustry(ScaleFids(player, amount), feedback, refresh, checkLocal, transfer);
        }

        public static void AwardScience(Player player, float amount, bool feedback, bool refresh, bool checkLocal, bool transfer) {
            player.AddScience(ScaleFids(player, amount), feedback, refresh, checkLocal, transfer);
        }

        private static float ScaleFids(Player player, float amount) {
            if (!(amount > 0f) || player != Player.LocalPlayer || !Enabled(7)) return amount;
            return Math.Min(MaxBalance, amount * resources);
        }

        public static void AwardDust(Dungeon dungeon, float amount, bool feedback, bool refresh) {
            if (amount > 0f && object.ReferenceEquals(dungeon, active) && Enabled(7))
                amount = Math.Min(dungeon.GetMaxDustStock(), amount * resources);
            dungeon.AddDust(amount, feedback, refresh);
        }

        public static bool CountDustGain() { return !adjustingDust; }

        public static void Tick(Dungeon dungeon, IGameEventService events) {
            long now = DateTime.UtcNow.Ticks;
            if (now < nextPoll) return;
            nextPoll = now + 100 * TimeSpan.TicksPerMillisecond;
            ReadSettings(now);
            bool ready = object.ReferenceEquals(dungeon, active) && events != null && Ready();
            int newVisibleFlags = ready ? flags & RefreshFlags : 0;
            int changed = visibleFlags ^ newVisibleFlags;
            visibleFlags = newVisibleFlags;
            if ((changed & newVisibleFlags & InstantResearchFlag) != 0) {
                foreach (Artifact artifact in UnityEngine.Object.FindObjectsOfType(typeof(Artifact)))
                    CompleteResearch(artifact);
            }
            if (ready && changed != 0) {
                events.TriggerDungeonFIDSChangedEvent();
                events.TriggerHeroListChangedEvent();
            }
            if (ready && now < deadline) ApplyResource(now, dungeon, events);
            try {
                Directory.CreateDirectory(Folder);
                WriteStatus("2|" + Pid + "|" + now + "|" + (ready ? "1" : "0") + "|"
                    + (ready ? flags : 0) + "|" + session + "|" + ack + "|" + applied + "|" + error);
            } catch (IOException) { }
              catch (UnauthorizedAccessException) { }
        }

        private static void ReadSettings(long now) {
            flags = 0;
            deadline = 0;
            string[] parts = Read("settings.txt");
            long expiry;
            int mask;
            if (parts.Length != 5 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry)
                || !int.TryParse(parts[2], out mask) || mask < 0 || mask > 16383 || parts[3].Length != 32) return;
            string[] values = parts[4].Split(',');
            float gain, speed;
            if (values.Length != 14 || !ReadMultiplier(values[7], 1f, 10f, out gain)
                || !ReadMultiplier(values[8], 0.1f, 5f, out speed)) return;
            if (session != parts[3]) {
                session = parts[3];
                ack = error = "";
                applied = 0;
            }
            resources = gain;
            movement = speed;
            flags = mask;
            deadline = expiry;
        }

        private static bool ReadMultiplier(string text, float min, float max, out float value) {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= min && value <= max;
        }

        private static void ApplyResource(long now, Dungeon dungeon, IGameEventService events) {
            string[] parts = Read("resource.txt");
            long expiry, amount;
            int kind;
            if (parts.Length != 6 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry)
                || parts[2] != session || parts[3].Length != 32 || parts[3] == ack
                || !int.TryParse(parts[4], out kind) || kind < 1 || kind > 4
                || !long.TryParse(parts[5], out amount) || amount < 0) return;
            ack = parts[3];
            applied = 0;
            error = "";
            int max = kind == 4 ? (int)Math.Min(MaxBalance, dungeon.GetMaxDustStock()) : MaxBalance;
            int min = kind == 4 ? 1 : 0;
            if (amount < min || amount > max) {
                error = "Enter a value between " + min + " and " + max + ".";
                return;
            }

            try {
                if (kind == 4) {
                    adjustingDust = true;
                    try { dungeon.AddDust((float)amount - dungeon.DustStock, false, true); }
                    finally { adjustingDust = false; }
                    applied = (int)Math.Round(dungeon.DustStock);
                } else {
                    var player = Player.LocalPlayer;
                    var stock = Stocks[kind - 1];
                    stock.SetValue(player, (float)amount, null);
                    events.TriggerDungeonFIDSChangedEvent();
                    events.TriggerHeroListChangedEvent();
                    applied = (int)(float)stock.GetValue(player, null);
                }
            } catch (Exception exception) {
                error = "Check the balance before retrying: " + exception.GetType().Name;
            }
        }

        private static bool ValidExpiry(string text, long now, out long expiry) {
            return long.TryParse(text, out expiry) && expiry > now && expiry <= now + 5 * TimeSpan.TicksPerSecond;
        }

        private static string[] Read(string file) {
            try { return File.ReadAllText(Path.Combine(Folder, file)).Trim().Split('|'); }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string source, string destination, int flags);

        private static void WriteStatus(string text) {
            string file = Path.Combine(Folder, "status.txt");
            string temporary = file + "." + Pid + ".new";
            File.WriteAllText(temporary, text);
            // Replace in one operation on the game's legacy Windows Mono runtime.
            if (!MoveFileEx(temporary, file, 1)) throw new IOException("Could not replace trainer status.");
        }
    }
}
