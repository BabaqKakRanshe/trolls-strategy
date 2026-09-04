# Minimal Visual Reset Design

## Goal

Replace the current game presentation with a single non-interactive Phaser scene containing only a white background, a red market rectangle, and a blue warehouse rectangle.

## Preserved systems

All mechanics remain in `src/domain`, `src/application`, `src/content`, and `src/persistence`: economy, units, assignments, equipment, deterministic combat, missions, save validation, and autosave adapters. Their unit tests remain authoritative even though the minimal runtime does not currently expose controls for them.

The original user-provided `assets` directory is preserved as source material. Only generated runtime copies and code that loads or renders sprites/audio are removed.

## Runtime presentation

`src/main.ts` creates only a Phaser game. `MinimalScene` sets a white camera background and draws two solid rectangles with Phaser geometry primitives:

- market: red `0xff0000`;
- warehouse: blue `0x0000ff`.

There is no DOM HUD, text, selection, pointer behavior, unit rendering, mission screen, combat playback, audio, texture preload, or save composition in the running page. The mechanics remain importable and tested but dormant until a later presentation layer reconnects them.

## Removal boundary

Remove `src/ui`, the previous colony/combat/preload/boot scenes, selection and frame-clock presentation helpers, their UI/E2E tests, stale screenshots, and generated `public/assets` runtime copies. Remove the asset-copy step and Playwright command from the active package scripts. Do not delete or rewrite the original `assets` directory, domain/application/persistence code, architecture audits, or domain tests.

## Verification

A focused scene/config test must prove that the game config contains only `MinimalScene`, uses a white background, and that the scene draws exactly the red market and blue warehouse rectangles without texture or sprite calls. The remaining unit suite, ESLint, and production build must pass. A browser screenshot must visually confirm the final result.
