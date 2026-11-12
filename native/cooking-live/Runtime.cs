using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cooking.Common.Enum;
using Cooking.Core.State;
using Cooking.Kitchen;
using Cooking.Kitchen.Models;
using Cooking.Kitchen.PlaceFry;
using Cooking.Utils.MonoBehaviourWrapper.Spine;
using Cooking.Kitchen.Visitor;
using Cooking.Manager.Kitchen;

namespace ByteTrainer.CookingLive
{
    public static class Runtime
    {
        private const int Supplies = 2, InstantCooking = 3, NoBurning = 4,
            CoinMultiplier = 5, PatientCustomers = 6, KeepCombo = 7, QuickWin = 8;
        private const int MaximumBalance = 1000000000;
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "TrainerHub-1990690");
        private static readonly string Pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
        private static int flags;
        private static long deadline, nextPoll, applied;
        private static float multiplier = 2;
        private static LevelProcessManager pendingWin, dispatchingWin;
        private static string session = "", ack = "", error = "";

        private static bool Enabled(int index)
        {
            return (flags & (1 << index)) != 0 && DateTime.UtcNow.Ticks < deadline;
        }

        public static bool SkipEnergySpend() { return Enabled(Supplies); }
        public static bool PauseCombo() { return Enabled(KeepCombo); }

        private sealed class BurnTimer { }
        private static readonly ConditionalWeakTable<CallbackTimer, BurnTimer> BurnTimers =
            new ConditionalWeakTable<CallbackTimer, BurnTimer>();
        private static SkeletonGraphicsWrapper comboBar;
        private static Spine.TrackEntry frozenTrack;
        private static float previousTrackSpeed;

        public static void TrackCookingTimer(CallbackTimer timer, List<FoodModel> foods, StateModel stage, bool overcooking)
        {
            if (timer == null) return;
            BurnTimers.Remove(timer);
            if (overcooking || (stage != null && stage.TimerType == TimerType.Burning) ||
                (foods != null && foods.Exists(food => food != null && food.State == CookingState.Burnt)))
                BurnTimers.Add(timer, new BurnTimer());
        }

        public static void TrackComboBar(SkeletonGraphicsWrapper bar)
        {
            if (!ReferenceEquals(bar, comboBar)) frozenTrack = null;
            comboBar = bar;
            UpdateComboBar(bar);
        }

        public static void UpdateComboBar(SkeletonGraphicsWrapper bar)
        {
            if (!ReferenceEquals(bar, comboBar) || bar == null) return;
            bool pause = PauseCombo();
            if (!pause && frozenTrack == null) return;
            var animations = bar.AnimationState;
            var track = animations == null ? null : animations.GetCurrent(0);
            if (!ReferenceEquals(track, frozenTrack)) frozenTrack = null;
            if (track == null) return;
            if (pause && (frozenTrack == null || track.TimeScale != 0f))
            {
                frozenTrack = track;
                previousTrackSpeed = track.TimeScale;
                track.TimeScale = 0f;
            }
            else if (!pause && frozenTrack != null)
            {
                track.TimeScale = previousTrackSpeed;
                frozenTrack = null;
            }
        }

        public static void QueueQuickWin(bool lastDish)
        {
            if (!lastDish || !Enabled(QuickWin)) return;
            var level = GameState.TryGetManager<LevelProcessManager>(true);
            if (level != null && level.IsLevelActive) pendingWin = level;
        }

        public static bool IsQuickWinDispatch(LevelProcessManager level)
        {
            return ReferenceEquals(level, dispatchingWin);
        }

        private static void DispatchQuickWin()
        {
            var level = pendingWin;
            pendingWin = null;
            if (!Enabled(QuickWin) || level == null || !level.IsLevelActive ||
                !ReferenceEquals(level, GameState.TryGetManager<LevelProcessManager>(true))) return;

            dispatchingWin = level;
            try { level.StartCompleteLevel(false); }
            finally { dispatchingWin = null; }
        }

        public static int ScaleCoins(int amount)
        {
            if (!Enabled(CoinMultiplier) || amount <= 0) return amount;
            return (int)Math.Min(1000000, Math.Round(amount * (double)multiplier, MidpointRounding.AwayFromZero));
        }

        public static float TimerDelta(float delta, object owner, MG.Utils.CustomTimer timer, CallbackTimer callback)
        {
            if (owner is VisitorOrderController && Enabled(PatientCustomers)) return 0f;
            BurnTimer burn;
            if (BurnTimers.TryGetValue(callback, out burn)) return Enabled(NoBurning) ? 0f : delta;
            var place = owner as BaseCookingPlace;
            if (place == null || place.NextStateModel == null) return delta;
            var stage = place.NextStateModel;
            if (stage.TimerType == TimerType.Burning || FoodModelUtils.FoodsCookingState(stage.After) == CookingState.Burnt)
                return Enabled(NoBurning) ? 0f : delta;
            return delta > 0f && Enabled(InstantCooking) ? Math.Max(delta, timer.Interval - timer.Timer + 0.001f) : delta;
        }

        public static void Tick(GameState state)
        {
            if (!ReferenceEquals(state, GameState.Instance)) return;
            long now = DateTime.UtcNow.Ticks;
            if (now < nextPoll) return;
            nextPoll = now + TimeSpan.TicksPerMillisecond * 100;
            flags = 0;
            deadline = 0;
            try { ReadSettings(now); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            bool ready = state.CurrencyController != null && state.EnergyController != null;
            try
            {
                if (ready && deadline > now)
                {
                    if (Enabled(Supplies) && state.EnergyController.EnergyAmount < state.EnergyController.MaxEnergy)
                        state.EnergyController.AddEnergyAmount(state.EnergyController.MaxEnergy - state.EnergyController.EnergyAmount, false, true, 0);
                    ApplyBalance(state, now);
                    DispatchQuickWin();
                }
                else
                {
                    if (!ready) flags = 0;
                    pendingWin = null;
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
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (Exception exception)
            {
                flags = 0;
                deadline = 0;
                UnityEngine.Debug.LogWarning("ByteTrainer: " + exception.GetType().Name);
            }
        }

        private static void ReadSettings(long now)
        {
            string[] parts = File.ReadAllText(Path.Combine(Folder, "settings.txt")).Trim().Split('|');
            long expiry;
            int mask;
            if (parts.Length != 5 || parts[0] != Pid || !ValidExpiry(parts[1], now, out expiry) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out mask) ||
                mask > 511 || parts[3].Length != 32) return;

            string[] values = parts[4].Split(',');
            float value;
            if (values.Length != 9 || !float.TryParse(values[CoinMultiplier], NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) || !(value >= 1f && value <= 10f)) return;
            if (session != parts[3])
            {
                session = parts[3];
                pendingWin = null;
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
                expiry > now && expiry <= now + TimeSpan.TicksPerSecond * 5;
        }

        private static void ApplyBalance(GameState state, long now)
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
            var controller = state.CurrencyController;
            var currency = parts[4] == "1" ? CurrencyType.Hard : CurrencyType.Soft;
            applied = controller.GetCurrencyValue(currency);
            if (value > MaximumBalance)
            {
                error = "Maximum balance: 1000000000.";
                return;
            }
            try
            {
                long difference = value - applied;
                if (difference > 0) controller.AddCurrency(currency, checked((int)difference), "ByteTrainer");
                else if (difference < 0) controller.SpendCurrency(currency, checked((int)-difference), "ByteTrainer");
                applied = controller.GetCurrencyValue(currency);
                if (applied != value) error = "The game returned a different balance.";
            }
            catch (Exception exception)
            {
                applied = controller.GetCurrencyValue(currency);
                error = "Check the balance before retrying: " + exception.GetType().Name;
            }
        }
    }
}
