using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Chibig;

namespace ByteTrainer.Mara
{
    public static class Runtime
    {
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-962580");
        private static readonly string Pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
        private static long deadline, nextPoll;
        private static int flags;
        private static float multiplier;
        private static string session = "", ack = "", error = "";
        private static int applied;
        private static volatile string heartbeat = "0|0|||0|";

        public static bool Enabled(int index)
        {
            return (flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < deadline;
        }

        public static float ScaleSpeed(float value) { return Enabled(3) ? value * multiplier : value; }

        static Runtime()
        {
            var worker = new Thread(PublishHeartbeat) { IsBackground = true };
            worker.Start();
        }

        public static void Tick()
        {
            long now = DateTime.UtcNow.Ticks;
            if (now < nextPoll) return;
            nextPoll = now + 100 * TimeSpan.TicksPerMillisecond;
            flags = 0;
            deadline = 0;
            try
            {
                ReadSettings(now);
                if (deadline > now)
                {
                    RefillVitals();
                    ApplyCoins(now);
                }
                heartbeat = string.Format(CultureInfo.InvariantCulture, "1|{0}|{1}|{2}|{3}|{4}",
                    flags, session, ack, applied, error);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void PublishHeartbeat()
        {
            string destination = Path.Combine(Folder, "status.txt");
            string temporary = destination + "." + Pid + ".new";
            while (true)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    string status = string.Format(CultureInfo.InvariantCulture, "2|{0}|{1}|{2}",
                        Pid, DateTime.UtcNow.Ticks, heartbeat);
                    File.WriteAllText(temporary, status);
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                Thread.Sleep(150);
            }
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
                (mask & ~991) != 0 || parts[3].Length != 32) return;

            string[] values = parts[4].Split(',');
            float value;
            if (values.Length != 10 || !float.TryParse(values[3], NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) || !(value >= 1f && value <= 3f)) return;

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

        private static void RefillVitals()
        {
            if (!Enabled(7) && !Enabled(8)) return;
            var player = PlayerStatus.Instance;
            if (player == null) return;
            if (Enabled(7))
            {
                if (player.Energy < player.EnergyMax) player.EnergyReset(1f);
                if (player.IsDiving && player.EnergyWater < player.EnergyWaterMax) player.EnergyWaterReset();
            }
            if (Enabled(8) && player.Hunger < player.HungerMax) player.HungerReset();
        }

        private static void ApplyCoins(long now)
        {
            string file = Path.Combine(Folder, "resource.txt");
            if (!File.Exists(file)) return;
            string[] parts = File.ReadAllText(file).Trim().Split('|');
            long expiry, amount;
            if (parts.Length != 6 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry) ||
                parts[2] != session || parts[3].Length != 32 || parts[3] == ack || parts[4] != "1" ||
                !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out amount)) return;

            var inventory = PlayerInventory.Instance;
            if (inventory == null) return;
            ack = parts[3];
            error = "";
            applied = inventory[Constants.Item.Coin];
            if (amount > 1000000000)
            {
                error = "Maximum balance: 1000000000.";
                return;
            }

            try
            {
                var coins = inventory.Inventory.Find(item => item.key == Constants.Item.Coin);
                if (coins != null) coins.quantity = (int)amount;
                else if (amount > 0) inventory.AddItem(Constants.Item.Coin, (int)amount,
                    default(PlayerInventory.ItemAddedHow), false, false);
                applied = inventory[Constants.Item.Coin];
                if (applied != amount) error = "The game returned a different balance.";
            }
            catch (Exception exception)
            {
                error = "Check the balance before retrying: " + exception.GetType().Name;
            }
        }

        private static bool ValidExpiry(string text, long now, out long expiry)
        {
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out expiry) &&
                expiry > now && expiry <= now + 5 * TimeSpan.TicksPerSecond;
        }

    }
}
