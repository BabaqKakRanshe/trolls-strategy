# Grid, Crowd, and Command Fan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the settlement board square, arrange up to 20 units per cell as a readable crowd, move unit orders into a right-click hex fan, add building hover outlines and labels, and replace the current typography.

**Architecture:** Keep campaign facts and deterministic positions in the existing catalog/domain, keep selection and commands in `MapInteractionController`, and keep the command fan plus hover effects as presentation-only state in `ColonyScene`. Retain the existing native command buttons as a keyboard-accessible equivalent, but visually reveal them only when focused.

**Tech Stack:** TypeScript, Phaser 4, native HTML/CSS, Vitest, Playwright.

**Spec:** The five numbered requirements in the current 2026-09-05 user request; no duplicate standalone spec is needed for this contained interaction pass.

## Global Constraints

- `GameSession` remains the only owner of mutable campaign state.
- Grid and crowd-capacity facts remain deterministic and presentation-independent.
- Canvas actions retain native DOM equivalents.
- No new runtime dependency, subsystem, or speculative movement command is introduced.
- Tests remain minimal: one domain crowd check and the existing player journey.

---

### Task 1: Square board and deterministic crowds

**Files:**
- Modify: `src/content/catalog.ts`
- Modify: `src/domain/colony.ts`
- Modify: `tests/unit/colony.test.ts`

**Interfaces:**
- Consumes: existing `Cell`, `WorldPosition`, unit IDs, and building footprints.
- Produces: `MAX_UNITS_PER_CELL = 20`, a 14×14 board, and unique deterministic positions for each crowd slot.

- [x] **Step 1: Write the failing crowd test**

Buy 21 goblins, verify that the first 20 occupy 20 distinct positions inside one cell, and verify that goblin 21 is placed in the next rally cell.

- [x] **Step 2: Run the unit suite and confirm failure**

Run `npm test`; expect the new crowd assertion to fail with the current four-column loose placement.

- [x] **Step 3: Implement the smallest domain change**

Set both grid dimensions to 14, move the two initial buildings inside the square board, and calculate positions with a five-by-four crowd slot function. Reuse the same deterministic slotting for building approach points and return released units to their rally crowd.

- [x] **Step 4: Run the unit suite**

Run `npm test`; expect all tests to pass.

### Task 2: Hex command fan and building hover feedback

**Files:**
- Modify: `src/game/ColonyScene.ts`

**Interfaces:**
- Consumes: `MapInteractionController.selectedIds()`, `beginWorkTarget()`, `beginHaulTarget()`, and `releaseSelected()`.
- Produces: a scene-local three-action hex fan opened by right-clicking a selected unit; hover-only building silhouette and name.

- [x] **Step 1: Center and clarify the square grid**

Derive `GRID_X` from the square ground width and keep a faint checker/grid visible in neutral mode, strengthening it during targeting or placement.

- [x] **Step 2: Add the command fan**

Create three interactive hex buttons—`Работа`, `Перенос`, `Свободны`—inside one Phaser container. Right-clicking a selected unit opens it at a clamped pointer position; clicking elsewhere or choosing an action closes it.

- [x] **Step 3: Add building hover treatment**

Place a tinted enlarged copy of each transparent building sprite behind the original as a silhouette outline. Show that outline and the building name only while hovered, while retaining the detailed tooltip.

- [x] **Step 4: Reduce unit visuals to crowd scale**

Size the sprite, selection ring, shadow, hit area, and cargo marker so the 20 deterministic slots remain visually distinct.

### Task 3: Typography and accessible command fallback

**Files:**
- Modify: `src/ui/AppUi.ts`
- Modify: `src/styles.css`
- Modify: `tests/e2e/colony.spec.ts`
- Modify: `README.md`

**Interfaces:**
- Consumes: existing native buttons and delegated click handler.
- Produces: non-pixel humanist typography and keyboard-revealable native command controls.

- [x] **Step 1: Replace typography and interaction copy**

Use `Trebuchet MS` with `Segoe UI`/sans-serif fallbacks in DOM and Phaser text. Update the hints to explain selection followed by right-click on the selected crowd.

- [x] **Step 2: Hide the old persistent dock without removing accessibility**

Apply a visually-hidden pattern to the native command dock unless it contains keyboard focus. Disabled controls remain disabled until units are selected; focused controls reveal as the keyboard equivalent.

- [x] **Step 3: Update the existing E2E journey**

Focus the native `Работать` button before asserting visibility and clicking it. Continue the same complete mine-to-market journey; do not add another broad E2E test.

- [x] **Step 4: Document and verify**

Update controls in `README.md`, then run `npm run lint`, `npm test`, `npm run build`, and `npm run e2e`. Manually verify the square board, a 20-unit crowd, the right-click fan, and building hover at 1280×720.

## Self-review

- Coverage: all five requested changes map to Tasks 1–3.
- Placeholders: none; every task names concrete files, behavior, and verification.
- Type consistency: crowd capacity is a catalog constant; presentation invokes only existing controller commands.
