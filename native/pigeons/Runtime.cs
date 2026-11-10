using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ByteTrainer.Pigeons {
    public static class Runtime {
        private const int MaxBalance = 999999999;
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-814300");
        private static readonly string Pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
        private static long deadline, nextPoll;
        private static int flags;
        private static float movement = 2f, speed = 2f, damage = 2f, money = 2f, pointsMultiplier = 2f;
        private static float nativeTimeScale = 1f, appliedTimeScale = 1f;
        private static bool ownsTimeScale;
        private static string session = "", ack = "", error = "";
        private static int applied;

        private static bool Ready() {
            var game = GameManager.Instance;
            return game != null && game.OfflineMode && !game.InSteamLobby && !game.Coop
                && !GameManager.gameOver && !GameManager.jogadorMorto
                && game.TotalPlayersLocal() == 1 && game.EstaJogando(0);
        }

        private static bool Enabled(int index) {
            return (flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < deadline && Ready();
        }

        private static bool Enabled(int index, IdentificadorPlayer player) {
            return Enabled(index) && player != null && player.IdJogadorRef == 1;
        }

        public static bool BlockDamage(int damage, IdentificadorPlayer player) {
            return damage > 0 && Enabled(0, player);
        }

        public static bool FreeShot(bool original, Arma weapon, IdentificadorPlayer player, ref bool freeShot) {
            freeShot = !original && !(weapon is Consumivel) && Enabled(1, player);
            return original || freeShot;
        }

        public static int RefundShot(ref bool freeShot) {
            int refund = freeShot ? 0 : 1;
            freeShot = false;
            return refund;
        }

        public static bool MagicAmmo(bool original, IdentificadorPlayer player) {
            return original || Enabled(1, player);
        }

        public static bool ChargeSpecial(IdentificadorPlayer player, int maximum, ref int charge) {
            if (charge >= maximum || !Enabled(2, player)) return false;
            charge = maximum;
            return true;
        }

        public static float Movement(float original, IdentificadorPlayer player) {
            return Enabled(5, player) ? original * movement : original;
        }

        public static int DamageToEnemy(int amount, int player, int health) {
            if (amount <= 0 || player != 1) return amount;
            if (Enabled(7) && health > 0) return Math.Max(amount, health);
            return Enabled(8) ? Scale(amount, damage, int.MaxValue) : amount;
        }

        public static void CollectCoins(MoedaManager coins, int amount) {
            if (amount > 0 && Enabled(9)) amount = Scale(amount, money, Remaining(coins.saldoPartida));
            coins.AddSaldoPartida(amount);
        }

        public static int AwardPoints(int amount, int player, int[] points, int total, int absent) {
            if (amount <= 0 || player != 1 || !Enabled(10)) return amount;
            return Scale(amount, pointsMultiplier, Remaining(Math.Max(points[0], Math.Max(total, absent))));
        }

        private static int Scale(int amount, float multiplier, int limit) {
            return (int)Math.Min(limit, Math.Round(amount * (double)multiplier, MidpointRounding.AwayFromZero));
        }

        private static int Remaining(int balance) {
            return MaxBalance - Math.Min(MaxBalance, Math.Max(0, balance));
        }

        public static void SetGameTime(float value) {
            nativeTimeScale = value;
            ownsTimeScale = value > 0 && Enabled(6);
            appliedTimeScale = ownsTimeScale ? value * speed : value;
            if (Time.timeScale != appliedTimeScale) Time.timeScale = appliedTimeScale;
        }

        private static void RefreshGameTime() {
            float current = Time.timeScale;
            SetGameTime(ownsTimeScale && current == appliedTimeScale ? nativeTimeScale : current);
        }

        public static void RestoreGameTime() {
            if (ownsTimeScale && Time.timeScale == appliedTimeScale) Time.timeScale = nativeTimeScale;
            ownsTimeScale = false;
        }

        public static void Tick(int[] points, int[] previousPoints, ref int score) {
            long now = DateTime.UtcNow.Ticks;
            if (now < nextPoll) return;
            nextPoll = now + 100 * TimeSpan.TicksPerMillisecond;
            ReadSettings(now);
            RefreshGameTime();
            bool ready = Ready() && MoedaManager.Instance != null;
            if (ready && now < deadline) ApplyResource(now, points, previousPoints, ref score);
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
                || !int.TryParse(parts[2], out mask) || mask < 0 || mask > 2047 || parts[3].Length != 32) return;

            string[] numbers = parts[4].Split(',');
            float moveValue, speedValue, damageValue, moneyValue, pointsValue;
            if (numbers.Length != 11 || !ReadMultiplier(numbers[5], 0.1f, 5f, out moveValue)
                || !ReadMultiplier(numbers[6], 0.1f, 5f, out speedValue)
                || !ReadMultiplier(numbers[8], 1f, 10f, out damageValue)
                || !ReadMultiplier(numbers[9], 1f, 10f, out moneyValue)
                || !ReadMultiplier(numbers[10], 1f, 10f, out pointsValue)) return;

            if (session != parts[3]) {
                session = parts[3];
                ack = "";
                applied = 0;
                error = "";
            }
            flags = mask;
            movement = moveValue;
            speed = speedValue;
            damage = damageValue;
            money = moneyValue;
            pointsMultiplier = pointsValue;
            deadline = expiry;
        }

        private static bool ReadMultiplier(string text, float min, float max, out float value) {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= min && value <= max;
        }

        private static void ApplyResource(long now, int[] points, int[] previousPoints, ref int score) {
            string[] parts = Read("resource.txt");
            long expiry, amount;
            int kind;
            if (parts.Length != 6 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry)
                || parts[2] != session || parts[3].Length != 32 || parts[3] == ack
                || !int.TryParse(parts[4], out kind) || (kind != 1 && kind != 2)
                || !long.TryParse(parts[5], out amount) || amount < 0) return;

            ack = parts[3];
            error = "";
            applied = 0;
            if (amount > MaxBalance) {
                error = "Maximum value: 999,999,999.";
                return;
            }
            int value = (int)amount;
            try {
                if (kind == 1) {
                    var coins = MoedaManager.Instance;
                    coins.AddSaldoPartida(value - coins.saldoPartida);
                    applied = coins.saldoPartida;
                } else {
                    points[0] = value;
                    previousPoints[0] = value;
                    score = value;
                    applied = score;
                }
            } catch (Exception exception) {
                error = "Check the balance before retrying: " + exception.GetType().Name;
            }
        }

        private static bool ValidExpiry(string text, long now, out long expiry) {
            return long.TryParse(text, out expiry) && expiry > now && expiry <= now + 5 * TimeSpan.TicksPerSecond;
        }

        private static string[] Read(string file) {
            try {
                return File.ReadAllText(Path.Combine(Folder, file)).Trim().Split('|');
            } catch (IOException) { return new string[0]; }
              catch (UnauthorizedAccessException) { return new string[0]; }
        }

        private static void WriteStatus(string text) {
            string file = Path.Combine(Folder, "status.txt");
            string temporary = file + "." + Pid + ".new";
            File.WriteAllText(temporary, text);
            if (File.Exists(file)) File.Replace(temporary, file, null);
            else File.Move(temporary, file);
        }
    }
}
