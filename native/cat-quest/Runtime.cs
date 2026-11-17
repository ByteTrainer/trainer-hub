using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;

namespace ByteTrainer.CatQuest
{
    public static class Runtime
    {
        private const int Health = 0, Mana = 1, OneHit = 2, Experience = 4, Damage = 5, Movement = 6;
        private const int MaximumBalance = 1000000000;
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-593280");
        private static readonly string Pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
        private static int flags;
        private static long deadline, nextPoll, applied;
        private static float experience, damage, movement;
        private static bool backgroundEnabled;
        private static CombatData playerCombat;
        private static string session = "", ack = "", error = "";

        private static bool Enabled(int index)
        {
            return (flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < deadline;
        }

        public static int ScaleDamage(int amount, CombatData source, CombatData target)
        {
            if (ReferenceEquals(target, playerCombat) && Enabled(Health)) return 0;
            if (!ReferenceEquals(source, playerCombat) || ReferenceEquals(target, playerCombat)) return amount;
            if (Enabled(OneHit))
                return (int)Math.Min(int.MaxValue, (long)Math.Max(0, target.currentHealth.Value) +
                    Math.Max(0, target.currentDefence.Value));
            return Enabled(Damage) ? (int)Math.Min(int.MaxValue, Math.Round(amount * (double)damage,
                MidpointRounding.AwayFromZero)) : amount;
        }

        public static int SpendMana(int current, int cost)
        {
            return Enabled(Mana) ? current : current - cost;
        }

        public static int AddExperience(int current, int amount)
        {
            if (amount <= 0 || !Enabled(Experience)) return current + amount;
            return (int)Math.Min(int.MaxValue, current + Math.Round(amount * (double)experience,
                MidpointRounding.AwayFromZero));
        }

        public static float ScaleSpeed(float speed)
        {
            return Enabled(Movement) ? speed * movement : speed;
        }

        public static void Tick(Game game)
        {
            if (!ReferenceEquals(game, Game.instance)) return;
            if (!backgroundEnabled)
            {
                // Unity otherwise stops polling as soon as the Hub takes focus.
                UnityEngine.Application.runInBackground = true;
                backgroundEnabled = true;
            }
            var data = game.gameData;
            bool ready = data != null && data.system.GameStarted && game.player != null;
            playerCombat = ready ? data.player.combat : null;
            if (!ready) flags = 0;

            long now = DateTime.UtcNow.Ticks;
            if (now < nextPoll) return;
            nextPoll = now + TimeSpan.TicksPerMillisecond * 100;
            flags = 0;
            deadline = 0;
            try { ReadSettings(now); }
            catch (IsolatedStorageException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            try
            {
                if (!ready) flags = 0;
                if (ready && deadline > now)
                {
                    if (!game.player.IsDead)
                    {
                        if (Enabled(Health) && playerCombat.currentHealth.Value < playerCombat.hp.Value)
                            playerCombat.currentHealth.Value = playerCombat.hp.Value;
                        if (Enabled(Mana) && playerCombat.currentMana.Value < playerCombat.maxMana.Value)
                            playerCombat.currentMana.Value = playerCombat.maxMana.Value;
                    }
                    ApplyBalance(data.player, now);
                }

                Directory.CreateDirectory(Folder);
                string status = string.Format(CultureInfo.InvariantCulture, "2|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                    Pid, now, ready ? 1 : 0, flags, session, ack, applied, error);
                string destination = Path.Combine(Folder, "status.txt");
                string temporary = destination + "." + Pid + ".new";
                File.WriteAllText(temporary, status);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            catch (IsolatedStorageException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (Exception exception)
            {
                flags = 0;
                deadline = 0;
                UnityEngine.Debug.LogWarning("ByteTrainer: " + exception.GetType().Name);
            }
        }

        private static bool ReadMultiplier(string text, float max, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                value >= 1f && value <= max;
        }

        private static void ReadSettings(long now)
        {
            string file = Path.Combine(Folder, "settings.txt");
            // This Unity Mono can throw IsolatedStorageException for a missing file.
            if (!File.Exists(file)) return;
            string[] parts = File.ReadAllText(file).Trim().Split('|');
            long expiry;
            int mask;
            if (parts.Length != 5 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out mask) ||
                mask > 127 || parts[3].Length != 32) return;

            string[] values = parts[4].Split(',');
            if (values.Length != 7 || !ReadMultiplier(values[Experience], 10f, out experience) ||
                !ReadMultiplier(values[Damage], 10f, out damage) ||
                !ReadMultiplier(values[Movement], 3f, out movement)) return;
            if (session != parts[3])
            {
                session = parts[3];
                ack = error = "";
                applied = 0;
            }
            flags = mask;
            deadline = expiry;
        }

        private static bool ValidExpiry(string text, long now, out long expiry)
        {
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out expiry) &&
                expiry > now && expiry <= now + TimeSpan.TicksPerSecond * 5;
        }

        private static void ApplyBalance(PlayerData player, long now)
        {
            string file = Path.Combine(Folder, "resource.txt");
            if (!File.Exists(file)) return;
            string[] parts = File.ReadAllText(file).Trim().Split('|');
            long expiry, value;
            if (parts.Length != 6 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry) ||
                parts[2] != session || parts[3].Length != 32 || parts[3] == ack || parts[4] != "1" ||
                !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out value)) return;

            ack = parts[3];
            error = "";
            var gold = player.currency.gold;
            applied = gold.Value;
            if (value > MaximumBalance)
            {
                error = "Maximum balance: 1000000000.";
                return;
            }
            try
            {
                gold.Value = (int)value;
                Game.gameStream.Publish(new SaveEvent { type = SaveEvent.EventType.UPDATE_SAVE });
                applied = gold.Value;
                if (applied != value) error = "The game returned a different balance.";
            }
            catch (Exception exception)
            {
                applied = gold.Value;
                error = "Check the balance before retrying: " + exception.GetType().Name;
            }
        }
    }
}
