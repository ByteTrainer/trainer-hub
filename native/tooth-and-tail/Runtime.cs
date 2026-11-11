using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Armada;
using Armada.Network;
using ArmadaData;

namespace ByteTrainer.ToothAndTail {
    public static class Runtime {
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-286000");
        private static readonly int Pid = Process.GetCurrentProcess().Id;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static long nextPoll;
        private static volatile Controls current;

        private sealed class Controls {
            public World World;
            public Faction Faction;
            public long Deadline;
            public int Flags;
            public string Session = "";
            public FInt Movement;
            public FInt Damage;
        }

        private static Faction LocalFaction(Session session, PlayerGroup group, LobbyInterface lobby,
                GameRules.Usage usage, bool observer, bool ranked) {
            if (session == null || session.World == null || session.Rules == null || session.Description == null
                || session.IsStopped || session.Results != null || group == null || observer || ranked
                || usage != GameRules.Usage.Game || session.Rules.CurrentSessionUsage != GameRules.Usage.Game)
                return null;

            bool story = session.Description.MissionType == SessionDescription.MissionTypes.CampaignMission;
            bool skirmish = session.Description.MissionType == SessionDescription.MissionTypes.PVP;
            if ((!story && !skirmish) || (lobby != null && !(lobby is OfflineLobby))
                || (skirmish && !(lobby is OfflineLobby))) return null;

            Player local = null;
            int bots = 0;
            foreach (Player player in group.Players) {
                if (player == null || player.Observer) return null;
                if (player.IsBot) {
                    bots++;
                    continue;
                }
                if (!player.Local || local != null) return null;
                local = player;
            }
            if (local == null || (skirmish && bots == 0)) return null;
            Faction faction = local.GetFaction(session);
            return faction != null && faction.Alive && faction.PlayerControlled ? faction : null;
        }

        public static void Tick(Session session, PlayerGroup group, LobbyInterface lobby,
                GameRules.Usage usage, bool observer, bool ranked) {
            Faction faction = LocalFaction(session, group, lobby, usage, observer, ranked);
            long now = DateTime.UtcNow.Ticks;
            var previous = current;
            if (previous != null && (previous.Faction != faction || previous.World != session.World)) current = null;
            if (now < nextPoll) return;
            nextPoll = now + TimeSpan.TicksPerMillisecond * 100;
            var settings = new Controls { Faction = faction, World = faction == null ? null : session.World };
            ReadSettings(settings, now);
            current = faction == null ? null : settings;
            try {
                Directory.CreateDirectory(Folder);
                string status = Path.Combine(Folder, "status.txt");
                string temporary = Path.Combine(Folder, "status." + Pid + ".tmp");
                File.WriteAllText(temporary, string.Format(Invariant, "2|{0}|{1}|{2}|{3}|{4}||0|",
                    Pid, now, faction != null ? 1 : 0, faction != null ? settings.Flags : 0, settings.Session));
                if (File.Exists(status)) File.Replace(temporary, status, null);
                else File.Move(temporary, status);
            } catch (IOException) { }
              catch (UnauthorizedAccessException) { }
        }

        public static void Stop() {
            current = null;
            nextPoll = 0;
        }

        private static void ReadSettings(Controls settings, long now) {
            try {
                var fields = File.ReadAllText(Path.Combine(Folder, "settings.txt")).Split('|');
                int pid, flags;
                long deadline;
                Guid connection;
                if (fields.Length != 5 || !int.TryParse(fields[0], out pid) || pid != Pid
                    || !long.TryParse(fields[1], out deadline) || deadline <= now
                    || deadline - now > 5 * TimeSpan.TicksPerSecond
                    || !int.TryParse(fields[2], out flags) || flags < 0 || flags > 127
                    || !Guid.TryParseExact(fields[3], "N", out connection)) return;
                var values = fields[4].Split(',');
                double movement, damage;
                if (values.Length != 7 || !Multiplier(values[5], 5, out movement)
                    || !Multiplier(values[6], 10, out damage)) return;
                settings.Deadline = deadline;
                settings.Flags = flags;
                settings.Session = fields[3];
                settings.Movement = FInt.Create(movement);
                settings.Damage = FInt.Create(damage);
            } catch (IOException) { }
              catch (UnauthorizedAccessException) { }
        }

        private static bool Multiplier(string value, double maximum, out double result) {
            return double.TryParse(value, NumberStyles.Float, Invariant, out result)
                && result >= 1 && result <= maximum;
        }

        private static Controls Enabled(Faction faction, int index) {
            var settings = current;
            return settings != null && settings.Faction == faction && faction.Alive
                && (settings.Flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < settings.Deadline
                ? settings : null;
        }

        private static Controls Enabled(GameplayEntity entity, int index) {
            var settings = current;
            return settings != null && entity != null && entity.World == settings.World
                && entity.Faction == settings.Faction && settings.Faction.Alive
                && (settings.Flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < settings.Deadline
                ? settings : null;
        }

        public static bool Protect(Buildable target, FInt damage) {
            return damage > 0 && Enabled(target, 0) != null;
        }

        public static void Refill(Faction faction) {
            if (Enabled(faction, 1) != null && faction.Food < 10000) faction.Food = 10000;
        }

        public static FInt ProductionDuration(FInt duration, Buildable producer) {
            return Enabled(producer, 2) != null ? (FInt)0 : duration;
        }

        public static bool FreeConstruction(Faction faction) {
            return Enabled(faction, 3) != null;
        }

        public static FInt Construction(FInt progress, Structure structure) {
            return Enabled(structure, 4) != null ? structure.GetData().BuildTime : progress;
        }

        public static FInt Movement(FInt speed, Actor actor) {
            var settings = Enabled(actor, 5);
            return settings == null ? speed : speed * settings.Movement;
        }

        public static FInt Damage(Buildable target, FInt damage, GameplayEntity source) {
            var settings = Enabled(source, 6);
            return settings != null && damage > 0 && target.World == settings.World
                && target.Faction != null && target.TeamIndex != source.TeamIndex
                ? damage * settings.Damage : damage;
        }
    }
}
