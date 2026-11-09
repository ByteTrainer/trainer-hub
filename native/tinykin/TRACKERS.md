# Tinykin trackers (integration 1.1.0, revision 3)

Four independent toggles extend the existing six controls without changing their IDs or indices:

| Shortcut | ID | Category |
| --- | --- | --- |
| F7 | pollen-tracker | Loose pollen and unopened pollen containers |
| F8 | tinykin-tracker | Uncollected tinykin eggs, labeled by color |
| F9 | objective-tracker | Pending main, side and optional mission interactions with physical anchors |
| F10 | collectible-tracker | Visible, uncollected special items |

Each category selects the nearest eligible target by straight-line distance, draws an on-screen marker or an edge indicator, and lists the number of available targets. Counts describe loaded targets, not total remaining collectibles in the level. An unopened pollen container is one target, regardless of its contents. Hidden or unloaded objects are excluded. No navigation or wall avoidance is provided.

Discovery runs once per second; nearest-target selection runs every 100 ms. Collection state and object validity are checked again when drawing. The scanner never invokes collection, save, quest completion or interaction methods.

Mission resolution follows unfinished `QuestObjective` triggers, `MultiTrigger` children and persistent `Execute` event links back to `InteractionTrigger` bounds. Validated children, completed objectives and nonselectable interactions are excluded. Logical controllers without a resolved interaction are recorded in diagnostics, never shown at their manager transforms. This is intentionally a conservative subset of mission targets and requires live validation of each game's scene bindings.

All Unity object discovery and HUD rendering stay on the main thread, outside the IPC state lock. The background heartbeat remains limited to files and plain snapshots. Tracker failures are isolated from the six existing controls.

HUD text is generated at build time from the canonical English, Portuguese and Spanish catalogs and follows the game's language. New control labels are supplied by the signed package; Hub versions without the new language entries use their English fallback.

Integration 1.1.1 (revision 4) also carries the translated control names and descriptions in its signed definition. Hub 1.6.1 loads those dictionaries into the game page, shortcut settings and overlay at runtime. This update changes translation metadata only; it does not change the game runtime DLL or tracker behavior.

Diagnostics: `%TEMP%/TrainerHub-1599020/trackers.txt` contains target counts, selected object IDs/positions and quest binding resolution. `tracker-error.txt` records scanner errors. These files contain no save edits.

Automated validation covers persisted pollen state, inactive and unloaded objects, empty containers, egg collection, nearest-target replacement, aggregate quest progress, persistent event resolution, scene departure and special-item collection. The native runtime tests also cover the extended mask and preserve minimized/overlay heartbeat isolation.

Live diagnostics on Steam build 10185644 identified four pollen targets, five tinykin eggs and three unfinished poster interactions in Ambrose Land. During play, pollen targets decreased from four to two and poster progress advanced from 3/6 to 4/6, removing the completed physical interaction and selecting another. The player confirmed the markers appeared and switched targets. No eligible special item was present in that scene, so its collection transition remains covered by automated tests only.

Release 1.1.0 was published to the signed GitHub Pages feed. A separate cache using the actual Hub updater downloaded and verified revision 3, including all ten command definitions.

## Mail, cages and quest starters (1.2.0)

- **F11 — Letter tracker:** discovers the `SmallLiftable` objects assigned to the loaded room's `PostOffice` quest mailbox. It excludes letters already delivered via the game's `ArrivedAtDestination` state, and tracks the current position of a letter even while it is being carried. The letters are separate from special items.
- **F8 — Tinykin tracker:** includes actual `Cage` components with prisoners, in addition to eggs. The marker shows the box, tinykin color and number of prisoners. Empty, opened, completed and unavailable boxes are excluded.
- **F9 — Mission tracker:** also marks a talkable NPC whose currently selected conversation invokes an enabled `Quest.Activate` event for a hidden quest. It matches the current dialogue's start and line events against the configured filters. It ignores disabled interactions and future dialogue variants. Quests activated by routes without an identifiable NPC dialogue link are outside this detector.

These links were checked against the installed Steam scene bundles and game code. Automated fixtures verify delivery and cage opening remove their markers, that an activated quest stops marking its NPC, and that disabled dialogue bindings, inactive zones and unrelated objects are excluded. The existing command indices and background connection behavior are preserved.
