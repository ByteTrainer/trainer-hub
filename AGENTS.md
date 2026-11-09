# ByteTrainer app

## Scope and repository layout

- ByteTrainer is a Windows app for trainers in single-player Steam sessions. For games that also support multiplayer, scope new controls and live tests to solo play. Launch games through Steam.
- `src/` contains the React interface and overlay. `electron/` owns Steam discovery, Windows integration, IPC, package verification and trainer sessions. Keep privileged operations behind the existing preload and IPC boundaries.
- `native/<game>/` contains game runtimes and patchers where applicable. `resources/<game>/` contains integration definitions, recipes and package inputs. Reuse the existing adapter and package driver when they support the required controls.
- The companion [trainer-integrations repository](https://github.com/ByteTrainer/trainer-integrations) hosts GitHub Pages, the signed catalog and released packages. The main repository hosts Windows app releases.

## Game integrations

- Follow [INTEGRATIONS.md](INTEGRATIONS.md). Verify the real Steam build and relevant hashes before declaring compatibility. Preserve installation staging, verified original backups, rollback and restoration. Use isolated copies for installation tests and apply changes to a live installation only while the game is closed.
- Keep Steam app IDs, command IDs and control indices stable. Preserve existing saved settings, shortcuts, profiles and package compatibility. Keep runtime commands bound to the correct game process and session, with expiry when the Hub disconnects.
- Prefer an independent signed `.thub` update for new games, controls and fixes supported by the current app. Change the minimum app version only when a package requires newer app behavior. Increment the package revision and use a new version and filename when changing a published package. Keep old published files intact.
- Use English source text. Put reusable strings in `locales/common/` and game-specific text in `locales/games/<game>/`. Update English, Brazilian Portuguese and Spanish together, preserving placeholders and IDs. Generate package translations through the packer. See [the translation guide](locales/README.md).
- Keep private signing keys, credentials, personal saves and extracted game binaries out of Git and release artifacts. Use the established signing tools and public key. Preserve the publisher identity used by installed clients.

## Validation

Use Windows with Node.js 22.12 or later.

```powershell
npm ci
npm test
npm run translations:check
npm run build
```

- Run the checks relevant to the change. For native integrations, also build the affected runtime, verify signed package discovery and installation, and test the changed controls in the supported Steam build. Report gameplay checks that could not be performed.
- When the supported game list changes, update all three root READMEs and the companion repository's catalog documentation. Keep their language links and the official game-page links consistent.

## Publishing

- For a game integration release, validate and sign the package with `scripts/integrations.mjs pack`. Publish it with `scripts/publish-integrations.ps1` against the companion checkout. Verify that GitHub Pages serves the new catalog, package and game page, and that the app updater accepts the package.
- For an app release, keep `package.json`, `package-lock.json` and `assets/changelog.json` aligned. Build with `npm run dist`, then use `scripts/publish-release.ps1`. The installer and `latest.yml` must come from the same build.
- Publish source and integration artifacts for the corresponding change. A package-only update uses the integrations release flow.
