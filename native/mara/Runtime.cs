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

        private static UnityEngine.GameObject objectiveMarkers;

        public static void ClearObjectives()
        {
            if (objectiveMarkers == null) return;
            objectiveMarkers.SetActive(false);
            UnityEngine.Object.Destroy(objectiveMarkers);
            objectiveMarkers = null;
        }

        public static void HighlightObjective(UIMap map, UnityEngine.GameObject worldMap, UnityEngine.GameObject localMap,
            UnityEngine.RectTransform highlight)
        {
            ClearObjectives();
            Tick();
            if (!Enabled(10)) return;
            bool local = localMap != null && localMap.activeInHierarchy;
            if (!local && (worldMap == null || !worldMap.activeInHierarchy || highlight == null)) return;
            var journal = QuestManager.Instance;
            if (journal == null) return;

            System.Collections.Generic.List<Quest> quests;
            System.Collections.Generic.List<int> states;
            System.Collections.Generic.List<Constants.Requester> requesters;
            journal.GetAllCurrentQuests(out quests, out states, out requesters);
            var targets = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cells = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || states[i] <= 0) continue;
                var target = quest.NpcTarget;
                if (Enum.IsDefined(typeof(Constants.Requester), target) && (int)target != 0 &&
                    target.ToString() != "Koa") targets.Add(target.ToString());
                var cell = quest.Coordinates;
                if (cell.Item1 >= 0 && cell.Item1 < 6 && cell.Item2 >= 0 && cell.Item2 < 6)
                    cells.Add(cell.Item1 * 6 + cell.Item2);
            }

            var parent = local ? localMap.transform : highlight.parent;
            objectiveMarkers = new UnityEngine.GameObject("ByteTrainerObjectives", typeof(UnityEngine.RectTransform));
            objectiveMarkers.transform.SetParent(parent, false);
            if (local)
            {
                foreach (var label in localMap.GetComponentsInChildren<UnityEngine.UI.Text>())
                {
                    if (targets.Contains(label.text.Trim())) MarkLabel(label);
                }
                if (targets.Count > 0)
                {
                    var panel = new UnityEngine.GameObject("Targets", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Text));
                    panel.transform.SetParent(objectiveMarkers.transform, false);
                    var text = panel.GetComponent<UnityEngine.UI.Text>();
                    text.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("Arial.ttf");
                    text.fontSize = 24;
                    text.alignment = UnityEngine.TextAnchor.MiddleCenter;
                    text.color = new UnityEngine.Color(1f, 0.85f, 0.2f);
                    text.raycastTarget = false;
                    text.text = string.Join("  •  ", new System.Collections.Generic.List<string>(targets).ToArray());
                    var outline = panel.AddComponent<UnityEngine.UI.Outline>();
                    outline.effectColor = UnityEngine.Color.black;
                    outline.effectDistance = new UnityEngine.Vector2(2, -2);
                    text.rectTransform.sizeDelta = new UnityEngine.Vector2(600, 45);
                    text.rectTransform.anchoredPosition = new UnityEngine.Vector2(0, 300);
                }
                return;
            }

            foreach (int cell in cells)
            {
                map.SelectQuadrant(cell / 6, cell % 6);
                var marker = UnityEngine.Object.Instantiate(highlight, objectiveMarkers.transform);
                marker.position = highlight.position;
                foreach (var graphic in marker.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    graphic.raycastTarget = false;
            }
            if (cells.Count > 0)
            {
                LeanTween.cancel(highlight.gameObject);
                highlight.gameObject.SetActive(false);
            }
        }

        private static void MarkLabel(UnityEngine.UI.Text label)
        {
            var marker = UnityEngine.Object.Instantiate(label, objectiveMarkers.transform);
            marker.raycastTarget = false;
            marker.text = "» " + label.text + " «";
            marker.color = new UnityEngine.Color(1f, 0.85f, 0.2f);
            marker.fontStyle = UnityEngine.FontStyle.Bold;
            marker.rectTransform.position = label.rectTransform.position;
            marker.rectTransform.sizeDelta = label.rectTransform.sizeDelta * 1.5f;
        }

        private static InteractablePerson nearbyObjective;
        private static float nextObjectiveScan;
        private static readonly System.Reflection.FieldInfo PersonKey = typeof(Interactable).GetField(
            "key", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public static void DrawObjective()
        {
            if (!Enabled(10) || UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
            var activity = MainActivity.Instance;
            if (activity == null || activity.Game == null || activity.Player == null) return;
            var mode = activity.Game.Mode;
            if (mode != Constants.PlayMode.Koa && mode != Constants.PlayMode.Carry &&
                mode != Constants.PlayMode.Swim && mode != Constants.PlayMode.Boat) return;
            var koa = activity.Player.Koa;
            var orbit = activity.Game.Cam;
            if (koa == null || orbit == null || orbit.Camera == null) return;

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now >= nextObjectiveScan)
            {
                nextObjectiveScan = now + 0.75f;
                nearbyObjective = FindNearbyObjective(koa.position);
            }
            if (nearbyObjective == null || !nearbyObjective.isActiveAndEnabled) return;
            var target = nearbyObjective.transform.position;
            var camera = orbit.Camera.transform;
            var direction = target - koa.position;
            direction.y = 0;
            var forward = camera.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) forward = koa.forward;
            forward.Normalize();
            var right = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, forward);
            float angle = UnityEngine.Mathf.Atan2(UnityEngine.Vector3.Dot(direction, right),
                UnityEngine.Vector3.Dot(direction, forward)) * UnityEngine.Mathf.Rad2Deg;
            string name = (string)PersonKey.GetValue(nearbyObjective);
            float distance = UnityEngine.Vector3.Distance(koa.position, target);
            var style = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.box);
            style.alignment = UnityEngine.TextAnchor.MiddleCenter;
            style.fontSize = 18;
            UnityEngine.GUI.Box(new UnityEngine.Rect((UnityEngine.Screen.width - 260) / 2f, 45, 260, 34),
                string.Format(CultureInfo.InvariantCulture, "{0}  ·  {1:0} m", name, distance), style);

            float center = UnityEngine.Screen.width / 2f - 105;
            var previous = UnityEngine.GUI.matrix;
            try
            {
                UnityEngine.GUIUtility.RotateAroundPivot(angle, new UnityEngine.Vector2(center, 62));
                UnityEngine.GUI.Label(new UnityEngine.Rect(center - 14, 45, 28, 34), "▲", style);
            }
            finally { UnityEngine.GUI.matrix = previous; }
        }

        private static InteractablePerson FindNearbyObjective(UnityEngine.Vector3 origin)
        {
            var journal = QuestManager.Instance;
            if (journal == null || PersonKey == null) return null;
            System.Collections.Generic.List<Quest> quests;
            System.Collections.Generic.List<int> states;
            System.Collections.Generic.List<Constants.Requester> requesters;
            journal.GetAllCurrentQuests(out quests, out states, out requesters);
            var targets = new System.Collections.Generic.HashSet<Constants.Requester>();
            for (int i = 0; i < quests.Count; i++)
                if (quests[i] != null && states[i] > 0 && quests[i].NpcTarget != Constants.Requester.None)
                    targets.Add(quests[i].NpcTarget);

            float nearest = float.PositiveInfinity;
            InteractablePerson result = null;
            foreach (var person in UnityEngine.Object.FindObjectsOfType<InteractablePerson>())
            {
                Constants.Requester key;
                if (!person.isActiveAndEnabled || !Enum.TryParse((string)PersonKey.GetValue(person), true, out key) ||
                    !targets.Contains(key)) continue;
                float distance = (person.transform.position - origin).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                result = person;
            }
            return result;
        }

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
                (mask & ~2015) != 0 || parts[3].Length != 32) return;

            string[] values = parts[4].Split(',');
            float value;
            if (values.Length != 11 || !float.TryParse(values[3], NumberStyles.Float,
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
