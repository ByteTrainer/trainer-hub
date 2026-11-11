# ByteTrainer

<p align="center">
  <img src="assets/icon.png" width="84" height="84" alt="">
</p>

<p align="center"><strong>A Windows trainer hub for single-player Steam games</strong></p>

<p align="center">
  <strong>English</strong> · <a href="README.pt-BR.md">Português (Brasil)</a> · <a href="README.es.md">Español</a>
</p>

<p align="center">
  <a href="https://github.com/ByteTrainer/trainer-hub/releases/latest"><strong>Download for Windows</strong></a> ·
  <a href="https://bytetrainer.github.io/trainer-integrations/en/">Official website</a> ·
  <a href="https://discord.gg/pMSJayXFmj">Discord</a>
</p>

## Features

- **Steam library detection** with a searchable catalog, favorites, and a configurable installation folder for each game.
- **In-game overlay** with the same trainer controls as the desktop app. Enable it per game and open it with <kbd>Alt</kbd> + <kbd>W</kbd> by default in windowed or borderless mode.
- **Custom shortcuts and cheat profiles** saved per game. A default profile can apply saved toggles and multipliers when a new session is ready. One-time actions such as setting currency are applied manually.
- **Customizable feedback** with four sound styles, volume control and mute. The app and overlay can follow the Windows theme or use a light or dark theme.
- **Three languages** across the app, overlay and website. Choose English, Brazilian Portuguese or Spanish.
- **Separate updates** for the app and game integrations. ByteTrainer checks GitHub Releases for app updates and uses a signed GitHub Pages catalog to discover game packages.

## Supported games

Each game page lists the available controls and offers the latest signed integration package. Compatibility depends on the installed Steam build. [Browse the live catalog](https://bytetrainer.github.io/trainer-integrations/en/catalogo.html)

| Game | Example controls |
| --- | --- |
| [Dungeon of the Endless](https://bytetrainer.github.io/trainer-integrations/en/jogos/249050.html) | Unlimited health, resource controls, instant research and active skills |
| [Kawaii Deathu Desu](https://bytetrainer.github.io/trainer-integrations/en/jogos/1102880.html) | Unlimited health, set souls, unlimited special |
| [Last Hope Bunker: Zombie Survival](https://bytetrainer.github.io/trainer-integrations/en/jogos/2475440.html) | Unlimited filter, infinite ammo, no reload, stealth mode |
| [Moventure](https://bytetrainer.github.io/trainer-integrations/en/jogos/3826060.html) | Invincibility, unlimited jumps, unlimited underwater oxygen |
| [My Friend Pedro](https://bytetrainer.github.io/trainer-integrations/en/jogos/557340.html) | Unlimited focus, locked combo, S rank, level timer freeze |
| [My Little Universe](https://bytetrainer.github.io/trainer-integrations/en/jogos/2328750.html) | Resource adjustments, instant refining, crystal and map expansion trackers |
| [Out of Space](https://bytetrainer.github.io/trainer-integrations/en/jogos/400080.html) | Unlimited stamina, set coins, infinite water bucket, prevent eggs from hatching |
| [Pigeons Attack](https://bytetrainer.github.io/trainer-integrations/en/jogos/814300.html) | unlimited health, ammo and special, game speed, one-hit kills, damage and reward multipliers |
| [Tooth and Tail](https://bytetrainer.github.io/trainer-integrations/en/jogos/286000.html) | Unlimited health and food, instant troops, free and instant construction, movement and damage multipliers |
| [Pumpkin Jack](https://bytetrainer.github.io/trainer-integrations/en/jogos/1186640.html) | Unlimited jumps, movement speed, instant crow cooldown, collectible tracker |
| [Shake it Co!](https://bytetrainer.github.io/trainer-integrations/en/jogos/3578070.html) | Easy fishing, automatic tree shaking, perfect chests, automatic selling |
| [Sol Cesto](https://bytetrainer.github.io/trainer-integrations/en/jogos/2738490.html) | Set gold, unlimited item uses, poison immunity, expanded inventory |
| [Tinykin](https://bytetrainer.github.io/trainer-integrations/en/jogos/1599020.html) | Unlimited gliding, mission and collectible trackers, race tracker |

## Getting started

1. Download and run the Windows setup file. [Latest app release](https://github.com/ByteTrainer/trainer-hub/releases/latest)
2. Open the game in ByteTrainer. If it is missing, go to **Settings → Integration updates → Check online** and select **Download package**.
3. With the game closed, use **Install integration** or **Update integration** on its page. You can set the installation folder there if Steam detection needs help.
4. Launch the game through Steam, enter a solo session and select **Connect trainer**. Enable controls from the app, hotkeys or overlay.

**Single-player use only.** ByteTrainer provides trainers for single-player sessions. Use solo mode in games that also offer multiplayer.

## Compatibility and updates

ByteTrainer checks the relevant game files before installation. A Steam update can require a compatible integration revision. Use **Restore original** on the game's page with the game closed to restore its backed-up files.

The app verifies each signed `.thub` package before accepting it. Game packages can add or update supported controls independently of the Windows app. To check for app updates manually, go to **Settings → ByteTrainer updates**. Release notes are available from the version number in the app and on GitHub. [App releases](https://github.com/ByteTrainer/trainer-hub/releases) · [Integration repository](https://github.com/ByteTrainer/trainer-integrations)

## Contributing

- **Request a game or report a trainer issue** in the community Discord. The **Confirmed games** tab in the app lists planned integrations. [Join Discord](https://discord.gg/pMSJayXFmj)
- **Improve or add a translation** with the shared language catalogs and the per-game files in `locales/`. [Translation guide](locales/README.md)
- **Work on game integrations** using the build checks, installation and publishing workflow. [Integration guide](INTEGRATIONS.md)

<details>
<summary>Build from source</summary>

On Windows, use Node.js 22.12 or later and npm.

```powershell
npm ci
npm run build
npm start
```

Run `npm test` and `npm run translations:check` before submitting changes. Run `npm run dist` to create the Windows installer.

| Directory | Purpose |
| --- | --- |
| `src/` | Desktop interface and overlay |
| `electron/` | Steam detection, desktop services and trainer connections |
| `native/` and `resources/` | Game integration source and package assets |
| `locales/` | Shared and game-specific translations |
| `scripts/` | Build, validation and publishing tools |

</details>
