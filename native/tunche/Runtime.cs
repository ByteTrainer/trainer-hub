using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ByteTrainer.Tunche
{
    public static class Runtime
    {
        private const int Health = 0, Mana = 1, OneHit = 2, DamageMultiplier = 3, KeepCombo = 4;
        private const int MaximumBalance = 1000000000;
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-887450");
        private static readonly string Pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
        private static long deadline, nextPoll, applied;
        private static int flags;
        private static float multiplier;
        private static string session = "", ack = "", error = "";

        private static PlayerController SoloPlayer(GameManager game)
        {
            if (game == null || game.ConnectedPlayers != 1 || game.ActivatedPlayersList == null ||
                game.ActivatedPlayersList.Count != 1) return null;
            var player = game.Players[0];
            return player != null && !player.IsDead && player.HP > 0 ? player : null;
        }

        private static bool Enabled(int index)
        {
            return (flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < deadline;
        }

        public static bool ProtectHealth(PlayerController player)
        {
            return Enabled(Health) && player == SoloPlayer(GameManager.Instance);
        }

        public static bool ProtectMana(PlayerController player)
        {
            return Enabled(Mana) && player == SoloPlayer(GameManager.Instance);
        }

        public static bool FreezeCombo(PlayerController player)
        {
            return Enabled(KeepCombo) && player == SoloPlayer(GameManager.Instance);
        }

        public static float DamageEnemy(EnemyController enemy, float damage, bool isHit, bool isSuper, PlayerController attacker)
        {
            if (damage > 0 && attacker != null && (Enabled(OneHit) || Enabled(DamageMultiplier)) &&
                attacker == SoloPlayer(GameManager.Instance))
            {
                if (Enabled(OneHit) && enemy.HP > 0)
                {
                    enemy.Shield = 0;
                    damage = enemy.HP;
                }
                else if (Enabled(DamageMultiplier))
                {
                    damage = Math.Min(1000000000f, damage * multiplier);
                }
            }

            enemy.DoDamage(damage, isHit, isSuper);
            return damage;
        }

        public static void Tick(GameManager game)
        {
            if (game != GameManager.Instance) return;
            long now = DateTime.UtcNow.Ticks;
            if (now < nextPoll) return;
            nextPoll = now + 100 * TimeSpan.TicksPerMillisecond;
            flags = 0;
            deadline = 0;

            var player = SoloPlayer(game);
            bool ready = player != null && player.SaveData != null && game.currentLevelData != null &&
                CurrencyManager.Instance != null && PlayerSaveData.assignedSlot > 0;
            try
            {
                if (ready)
                {
                    ReadSettings(now);
                    if (deadline > now)
                    {
                        Refill(player);
                        ApplyBalance(player, now);
                    }
                }
                WriteStatus(now, ready);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (Exception exception)
            {
                flags = 0;
                deadline = 0;
                UnityEngine.Debug.LogWarning("ByteTrainer: " + exception.GetType().Name);
            }
        }

        private static void Refill(PlayerController player)
        {
            if (Enabled(Health))
            {
                int maximum = player.GetMaxHP();
                if (player.HP < maximum) player.IncreaseHP(maximum - player.HP, false);
            }
            if (Enabled(Mana))
            {
                int maximum = player.GetMaxMP();
                if (player.MP < maximum) player.IncreaseMP(maximum - player.MP);
            }
        }

        private static void WriteStatus(long now, bool ready)
        {
            Directory.CreateDirectory(Folder);
            string status = string.Format(CultureInfo.InvariantCulture, "2|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                Pid, now, ready ? 1 : 0, flags, session, ack, applied, error);
            string destination = Path.Combine(Folder, "status.txt");
            string temporary = destination + "." + Pid + ".new";
            File.WriteAllText(temporary, status);
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }

        private static void ReadSettings(long now)
        {
            string file = Path.Combine(Folder, "settings.txt");
            if (!File.Exists(file)) return;
            string[] parts = File.ReadAllText(file).Trim().Split('|');
            long expiry;
            int mask;
            if (parts.Length != 5 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out mask) ||
                mask > 127 || parts[3].Length != 32) return;

            string[] values = parts[4].Split(',');
            float value;
            if (values.Length != 7 || !float.TryParse(values[DamageMultiplier], NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) || !(value >= 1f && value <= 10f)) return;

            if (session != parts[3])
            {
                session = parts[3];
                ack = error = "";
                applied = 0;
            }
            flags = mask;
            multiplier = value;
            deadline = expiry;
        }

        private static bool ValidExpiry(string text, long now, out long expiry)
        {
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out expiry) &&
                expiry > now && expiry <= now + 5 * TimeSpan.TicksPerSecond;
        }

        private static void ApplyBalance(PlayerController player, long now)
        {
            string file = Path.Combine(Folder, "resource.txt");
            if (!File.Exists(file)) return;
            string[] parts = File.ReadAllText(file).Trim().Split('|');
            long expiry, value;
            if (parts.Length != 6 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry) ||
                parts[2] != session || parts[3].Length != 32 || parts[3] == ack ||
                (parts[4] != "1" && parts[4] != "2") ||
                !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out value)) return;

            ack = parts[3];
            error = "";
            applied = 0;
            if (value > MaximumBalance)
            {
                error = "Maximum balance: 1000000000.";
                return;
            }

            try
            {
                if (parts[4] == "1")
                {
                    var progress = PlayerSaveData.Instance.GetCurrencyProgress(GameManager.Instance.currentLevelData.levelTag);
                    progress.ShardAmount = (int)value;
                    applied = progress.ShardAmount;
                }
                else
                {
                    player.SaveData.Experience = (int)value;
                    applied = player.SaveData.Experience;
                }
                if (applied != value) error = "The game returned a different balance.";
            }
            catch (Exception exception)
            {
                error = "Check the balance before retrying: " + exception.GetType().Name;
            }
        }
    }
}
