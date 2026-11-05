# Trainer Hub

<!-- impeccable:product-schema 1 -->

## Platform
Windows desktop application.

## Purpose
User confirmed a functional Windows application inspired by WeMod, with a large game catalog and game support added gradually. Name undecided; Trainer Hub is provisional.

## Stack
Implementation choice: Electron and React; a local desktop bridge handles Steam discovery and game adapters. The existing My Friend Pedro trainer is the first supported adapter.

## Capabilities
Searchable catalog, installed Steam game discovery, favorites, local support requests, per-game support status, and My Friend Pedro controls. A catalog listing never implies trainer support.

## Open decisions
Final name, commercial model, accounts, online request aggregation, distribution signing, and future game priorities are undecided. Initial release stores preferences locally; support requests are local planning entries.

## Sound feedback
Configurable local activation/deactivation sounds for cheat buttons and global shortcuts; master mute, 0–100 volume, four variants and non-mutating previews. Preferences persist across restarts.

## In-game controls and shortcuts

Supported games have a central overlay with larger controls for windowed or borderless play. Overlay availability and shortcut bindings are saved locally per game. The default overlay shortcut is Alt+W; users can change or remove individual bindings and restore a game's defaults. Duplicate combinations within a game and reserved Windows combinations are rejected. Cheat shortcuts are suspended while editing fields; shortcut capture pauses registered shortcuts while recording a new binding.

## Sol Cesto controls

Sol Cesto includes unlimited health, a charged solar ability, preservation of owned items, and poison immunity. One-time actions set strength (0–999), magic (0–999), and gold (0–999999), or expand inventory up to six slots while preserving existing items and capacity. Saved action values are used by shortcuts; applying these values requires an active run. Item preservation does not grant unowned items.

## Integration compatibility and recovery

My Friend Pedro and Sol Cesto integrations check relevant local file fingerprints, package metadata, and the Steam build and update state when available. Checks run on startup and during the regular status poll; connecting and the manual file-check action force a fresh check. A changed, unrecognized game version is reported as unvalidated, which does not establish that its cheats are broken. Connection requires recognized compatible files. Individual controls are also guarded by the package's declared cheats and runtime capability reports, allowing other compatible controls to remain available when one option is unsupported.

Installing, updating, restoring originals, and recovering an interrupted installation require the game to be closed. File changes are staged and verified through a transaction journal. A failed commit attempts to recover the previous installation; an interrupted transaction exposes an explicit recovery action. Recovery stops if another program has changed the affected files. Original backups are preserved, with older originals archived by game and fingerprint when a supported new base version requires a replacement backup.

## Integration package updates

Integration packages can be updated independently of the desktop application by importing a signed `.thub` file. The Hub verifies its Ed25519 signature against the bundled publisher key, validates the package and file hashes, and rejects older releases or conflicting contents under the same release. Importing makes the package available locally; applying it to the game is a separate action with the game closed. A package may still require a newer Hub version.

No update server is currently provided. Local signed package import is the available distribution path. The optional update client accepts an HTTPS endpoint for a signed catalog when a publisher server exists; downloaded packages must also carry valid independent signatures and match the catalog hash. Existing per-game shortcuts, sound preferences and previews, and overlay controls remain available alongside compatibility and update management.
