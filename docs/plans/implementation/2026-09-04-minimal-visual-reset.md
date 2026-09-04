# Minimal Visual Reset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the complete UI and sprite presentation with a white Phaser scene containing only a red market rectangle and a blue warehouse rectangle while preserving all mechanics.

**Architecture:** Keep domain, application, content, and persistence modules unchanged. Replace the presentation/composition root with one geometry-only scene and remove obsolete generated runtime assets and tests that assert the discarded UI.

**Tech Stack:** TypeScript 6, Phaser 4.2.1, Vite 8, Vitest 5, ESLint 10.

**Spec:** `docs/plans/2026-09-04-minimal-visual-reset-design.md`

## Global Constraints

- Preserve the complete original `assets` directory.
- Preserve `src/domain`, `src/application`, `src/content`, and `src/persistence` mechanics.
- Runtime output contains no HUD, text, sprites, textures, audio, or interaction.
- Final scene contains only white background, red market rectangle, and blue warehouse rectangle.
- Bulk removal of generated/runtime presentation files must be performed by one guarded Python script after validating every target is under the repository root.

---

### Task 1: Replace the runtime with one geometry-only scene

**Files:**
- Create: `src/game/scenes/MinimalScene.ts`
- Modify: `src/game/config.ts`
- Modify: `src/main.ts`
- Modify: `src/styles.css`
- Modify: `index.html`
- Create: `tests/unit/minimal-scene.test.ts`

**Interfaces:**
- Produces: `MinimalScene extends Phaser.Scene`
- Produces: `createGameConfig(parent: string): Phaser.Types.Core.GameConfig`
- Consumes: Phaser rectangle primitives only; no domain or asset imports

- [ ] **Step 1: Write the failing scene/config test**

Test that `createGameConfig('game-canvas')` has `backgroundColor: '#ffffff'`, contains only `MinimalScene`, and source inspection rejects `.sprite(`, `.image(`, `this.load`, texture keys, and DOM UI imports. Assert source contains rectangle fills `0xff0000` and `0x0000ff`.

- [ ] **Step 2: Run the focused test and confirm failure**

Run: `npm test -- tests/unit/minimal-scene.test.ts`

Expected: FAIL because `MinimalScene` does not exist and the current config registers four old scenes.

- [ ] **Step 3: Implement the minimal runtime**

Create `MinimalScene` with a white camera background and two centered solid rectangles. Register only that scene. Reduce `main.ts` to constructing `Phaser.Game(createGameConfig('game-canvas'))`. Reduce HTML/CSS to a full-viewport canvas mount without `#ui-root`, live regions, HUD selectors, or overlay styling.

- [ ] **Step 4: Run focused and full verification**

Run: `npm test -- tests/unit/minimal-scene.test.ts`

Expected: PASS.

Run: `npm test -- --run && npm run lint && npm run build`

Expected: PASS.

---

### Task 2: Remove obsolete presentation and generated runtime copies

**Files:**
- Delete: `src/ui/`
- Delete: `src/game/input/`
- Delete: `src/game/scenes/AssetPreloadScene.ts`
- Delete: `src/game/scenes/BootScene.ts`
- Delete: `src/game/scenes/ColonyScene.ts`
- Delete: `src/game/scenes/CombatScene.ts`
- Delete: UI/formation/asset/frame-clock tests under `tests/unit/`
- Delete: `tests/e2e/`, `playwright.config.ts`, `game-screenshot.png`
- Delete: generated `public/assets/`
- Modify: `package.json`
- Modify: `README.md`

**Interfaces:**
- Preserves: all domain/application/persistence public contracts and their tests
- Removes: runtime asset loading, UI events, scene router, Playwright UI contract, and generated asset deployment

- [ ] **Step 1: Create and run a guarded bulk-removal script**

Write a temporary Python script that resolves an explicit allowlist of obsolete paths, asserts each resolved target is below `F:/ClaudeGames/trollstrategy`, removes only those targets, prints every removal, and deletes itself after verification. Never target the original `assets` directory.

- [ ] **Step 2: Simplify package and documentation**

Remove `assets:prepare` from `build`, remove the obsolete `e2e` script and unused Playwright dependency, then update `README.md` to describe the minimal visual runtime and explicitly state that mechanics remain headless.

- [ ] **Step 3: Verify no presentation residue remains**

Run: `rg -n "AppUi|ColonyScene|CombatScene|AssetPreloadScene|add\.sprite|add\.image|load\.spritesheet|ui-root" src index.html package.json`

Expected: no matches.

- [ ] **Step 4: Run final checks and capture a screenshot**

Run: `npm test -- --run && npm run lint && npm run build`

Expected: all retained mechanics tests and the minimal scene test pass.

Run the Vite server and capture a 1280×720 Chromium screenshot. Expected: a white canvas containing only one red and one blue rectangle.

No commit step is included because this directory is not a Git repository.
