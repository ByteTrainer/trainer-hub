using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MoventureTrainer {
    public static class Runtime {
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-Moventure");
        private static Timer timer;
        private static long expiry, lastPlayable;
        private static int flags;
        private static int polling;

        public static void Boot() {
            try {
                int pid;
                using (var process = Process.GetCurrentProcess()) pid = process.Id;
                timer = new Timer(_ => Poll(pid), null, 0, 150);
            } catch { }
        }

        public static void Playable() {
            Interlocked.Exchange(ref lastPlayable, DateTime.UtcNow.Ticks);
        }

        public static bool Enabled(int bit) {
            return DateTime.UtcNow.Ticks < Interlocked.Read(ref expiry)
                && (Thread.VolatileRead(ref flags) & bit) != 0;
        }

        public static bool IgnoreImpactState(string state) {
            return Enabled(1) && (state == "Trip" || state == "BodyCrash" || state == "HeadCrash");
        }

        private static void Poll(int pid) {
            if (Interlocked.Exchange(ref polling, 1) != 0) return;
            try {
                long now = DateTime.UtcNow.Ticks;
                int next = 0;
                long deadline = 0;
                try {
                    using (var stream = new FileStream(Path.Combine(Folder, "settings.txt"), FileMode.Open,
                        FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream)) {
                        string[] parts = reader.ReadLine().Split('|');
                        if (parts.Length != 3 || parts[0] != pid.ToString() ||
                            !long.TryParse(parts[1], out deadline) || deadline <= now ||
                            deadline > now + TimeSpan.TicksPerSecond * 5 ||
                            !int.TryParse(parts[2], out next) || next < 0 || next > 7) {
                            next = 0;
                            deadline = 0;
                        }
                    }
                } catch {
                    next = 0;
                    deadline = 0;
                }
                Thread.VolatileWrite(ref flags, next);
                Interlocked.Exchange(ref expiry, deadline);
                Directory.CreateDirectory(Folder);
                bool ready = now - Interlocked.Read(ref lastPlayable) < TimeSpan.TicksPerSecond;
                using (var stream = new FileStream(Path.Combine(Folder, "status.txt"), FileMode.Create,
                    FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(stream))
                    writer.Write("1|" + pid + "|" + now + "|" + (ready ? "1" : "0") + "|" + next);
            } catch { }
            finally { Interlocked.Exchange(ref polling, 0); }
        }
    }
}
