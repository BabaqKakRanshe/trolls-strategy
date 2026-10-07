# Localization

The game is written in Russian, and the Russian text is the translation key
(`Runtime/Presentation/Localization.cs`). Which calls carry a text to the screen, and why: `CODING_STANDARDS.md`
§ UI code.

## Template keys

- A template key is an interpolated string: `$"Построить: {name}"` is the key `Построить: {0}`, and the
  filled part is translated as a name.
- Write whole sentences with holes rather than strings glued from pieces.

## After adding or changing player-facing text

1. `python tools/localization/extract.py`.
2. Translate the new keys into every file in `tools/localization/translations`; check each with
   `python tools/localization/validate.py <code>`.
3. `python tools/localization/build.py`.
4. Run the menu `TrollStrategy/Dev/Setup Localization Fonts`.
