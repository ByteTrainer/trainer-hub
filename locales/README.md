# Contributing translations

English is ByteTrainer's source language. The app, overlay and website share these catalogs. English, Brazilian Portuguese and Spanish are official languages.

## Structure

- `common/<language>.json`: navigation, settings, errors and reusable cheats such as unlimited health and movement speed.
- `games/<game>/<language>.json`: game-specific messages. Resource names belong in `games/my-little-universe/`; automatic tree shaking, blackjack and jackpot messages belong in `games/shake-it-co/`.
- `namespaces.json`: maps Steam game IDs to translation folders.
- `languages.json`: supported language codes and native names.

The English text is the key and its English value. Translate only values in the other languages. Preserve placeholders such as `{0}` and `{1}`, game titles, shortcuts and filenames. Values are plain text, never HTML. Move shared messages to `common` instead of duplicating them.

## Improve or add a language

1. Edit values in your language's files in `common` and each game folder.
2. For a new language, copy every `en.json` to its canonical BCP 47 code, such as `fr.json`, and translate every value.
3. Register new languages in `languages.json`, for example `{"code":"fr","name":"Français"}`.
4. Run `npm run translations:check` and `npm test`.
5. Review navigation, game controls, errors, dialogs and the overlay, then open a pull request.

The checker checks each namespace against English, verifies placeholders and rejects duplicate messages. Human review is still needed for meaning and terminology. Registered languages automatically appear in the app and generated website.

## Product messages

Write English source text in code, add it to the appropriate `en.json`, and translate it in every registered language. Use numbered placeholders for dynamic values. React components use `t` for shared messages or `gameTranslator(gameId)` for game controls. Keep protocol and resource IDs independent of language.

`legacy-aliases.json` is a compatibility index for older signed packages and saved activity messages with Portuguese text. It is not a translation source. Do not add new messages there. Existing packages keep their original signatures.

## Changelog

Add release notes in English to `assets/changelog.json`. Add each note as a shared message in `common/en.json` and translate it in every registered language. The translation checker runs during production builds and rejects any changelog note missing from the shared catalogs.

## Signed integration translations

From Hub 1.6.1, managed integration definitions can include a `translations` object in `game.json`, indexed by language code and English message key. The package signature covers these messages along with the cheat definitions and runtime. The Hub validates the dictionaries and applies them only to that game's scope in the app and overlay.

Keep editing the canonical catalogs above. `scripts/integrations.mjs pack` automatically runs `scripts/sync-package-translations.cjs` to generate the names, descriptions and resource-choice translations for every official language in the package. Missing translations stop packaging. The generated `game.json` should be committed with the release. Bump the integration revision/version before republishing changed text; never replace the bytes of a published revision.

Installing a newer signed package refreshes its dictionaries in open game pages and overlays, even if the selected language stays the same. Packages without translation metadata continue using the Hub's bundled catalogs and English fallback. Existing Hub 1.6.0 installations need the one-time Hub update to enable package-provided translations; later cheat translations can ship with integration updates alone. A brand-new app language still needs registration in `languages.json` and an app release to add the language selector entry.

## Website

The generated site lives in `ByteTrainer/trainer-integrations`. With both repositories checked out side by side, generate it with:

```powershell
node scripts/prepare-integration-site.mjs ../trainer-integrations --render
```

Portuguese keeps existing URLs; other languages use directories such as `/en/catalogo.html`. Edit catalogs here instead of generated site translations.
