using System;
using UI.PerPlayerUI.FishingMinigame;

namespace ByteTrainer.Universe {
    public static class AfkFishing {
        private static Player owner;
        private static FishingScreen screen;
        private static Action nextAction;

        public static void Queue(Player player,FishingScreen fishingScreen,Action action) {
            if(player==null||!player.IsMainPlayer)return;
            owner=player;
            screen=fishingScreen;
            nextAction=action;
        }

        public static void Clear(FishingScreen fishingScreen) {
            if(screen!=fishingScreen)return;
            owner=null;
            screen=null;
            nextAction=null;
        }

        public static void Update(Player player,bool paused) {
            if(nextAction==null)return;
            if(screen==null||!screen.isActiveAndEnabled) {
                Clear(screen);
                return;
            }
            if(paused||!Runtime.Enabled(Runtime.AfkFishingIndex)||player==null||player!=owner||
                player.Level==null||player.Level.Players.Count!=1||!player.Level.Players.Contains(player))return;

            // A native action can queue the next step synchronously.
            var action=nextAction;
            Clear(screen);
            action();
        }
    }
}
