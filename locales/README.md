# Contributing translations

ByteTrainer's desktop app, overlay, game controls and website share the JSON catalogs in this folder. The source language is Brazilian Portuguese. English and Spanish are included officially.

## Improve a translation

1. Edit values in `en.json` or `es.json`. The keys identify the original messages. Keep keys unchanged.
2. Preserve placeholders such as `{0}` and `{1}`, including repeated placeholders. You may reorder them to fit your language.
3. Keep game titles, ByteTrainer, Steam, keyboard shortcuts, technical file names and URLs unchanged.
4. Run `npm install` and `npm run translations:check` from the repository root.
5. Open a pull request describing the language and the pages you reviewed.

The checker verifies structure and placeholders. Human review is still needed for meaning, grammar and terminology. Use concise wording that fits buttons and labels. JSON values are plain text, never HTML.

## Add a language

1. Copy `en.json` to a file named with a canonical BCP 47 language code, such as `fr.json` or `de.json`.
2. Add its code and native name to `languages.json`. For example, add `{"code":"fr","name":"Français"}`.
3. Translate every value in the new file. Keep all original keys.
4. Run `npm run translations:check` and `npm test`.
5. Run the desktop app and review the catalog, settings, game pages, overlay, error messages and changelog in the new language.

The manifest controls the desktop language picker and the generated website pages, so a new registered language does not need a new UI implementation. The app saves the language in its local preferences and the website saves its own preference in the browser.

## Website preview

The integration website is maintained in the separate `ByteTrainer/trainer-integrations` repository. With both repositories checked out side by side, generate the localized pages from this repository with:

```powershell
node scripts/prepare-integration-site.mjs ../trainer-integrations --render
```

Portuguese keeps the existing URLs. Other languages use their language code as a directory, such as `/en/catalogo.html` and `/es/jogos/3578070.html`. Each page offers a language picker that retains the current game or catalog page. Do not edit generated translations in the website repository, because the generator will replace them.

## Adding product messages

Add each new source message to `pt-BR.json` and translate it in every registered language. In React, pass visible strings and dynamic integration messages through `t` from `src/i18n.js`. Dynamic messages use numbered placeholders in the catalogs. Text from future integration packages falls back to its original wording until a translation is contributed. Keep driver IDs, resource IDs, protocol fields and signed packages independent of the selected language.
