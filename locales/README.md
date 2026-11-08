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

## Website

The generated site lives in `ByteTrainer/trainer-integrations`. With both repositories checked out side by side, generate it with:

```powershell
node scripts/prepare-integration-site.mjs ../trainer-integrations --render
```

Portuguese keeps existing URLs; other languages use directories such as `/en/catalogo.html`. Edit catalogs here instead of generated site translations.
