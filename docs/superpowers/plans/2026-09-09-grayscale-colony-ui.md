# Grayscale Colony UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete grayscale wireframe HUD around the existing deterministic colony gameplay.

**Architecture:** `AppUi` continues to render immutable `GameSnapshot` data and submit existing `MapInteractionController` actions. CSS owns the responsive shell, while `ColonyScene` changes presentation defaults only; no domain, catalog, persistence, or command contracts change.

**Tech Stack:** TypeScript, semantic HTML, CSS Grid, Phaser 4, Vitest, Playwright.

**Spec:** `docs/ui-wireframe-spec.md`

## Global Constraints

- `GameSession` remains the single owner of mutable campaign state.
- UI reads snapshots and submits typed commands; it does not recompute outcomes.
- HUD artwork uses grayscale CSS primitives; Phaser keeps manifest-approved world sprites.
- All current visible-control E2E actions and accessible names remain operable.

---

### Task 1: Replace the HUD composition

**Files:**
- Modify: `src/ui/AppUi.ts`
- Test: `tests/e2e/colony.spec.ts`

**Interfaces:**
- Consumes: `GameSnapshot`, `MapInteractionController`, existing `data-action` values.
- Produces: the existing selector contract plus `#production-value`, `#objective-*`, and `#selection-summary` snapshot views.

- [x] Replace `shellMarkup()` with semantic top bar, left objective/roster rail, central world, right shop rail, and persistent bottom command dock.
- [x] Add snapshot-only rendering for objective completion, production rate, and selection summary.
- [x] Keep all action names, accessible purchase labels, resource IDs, and roster labels unchanged.
- [x] Run `npm test` and confirm the domain suite remains green.

### Task 2: Implement the grayscale responsive system

**Files:**
- Modify: `src/styles.css`

**Interfaces:**
- Consumes: class names emitted by `shellMarkup()` and state attributes from `AppUi`.
- Produces: a three-column desktop HUD, stacked narrow layout, visible focus indicators, and persistent commands.

- [x] Replace decorative theme rules with a grayscale token set and primitive borders/surfaces.
- [x] Set readable type and control sizes, explicit panel hierarchy, and a flexible world viewport.
- [x] Make selection, placement, disabled, hover, and completed-objective states distinguishable without color.
- [x] Add desktop, narrow-desktop, and mobile layout rules without changing DOM order.

### Task 3: Align the world presentation

**Files:**
- Modify: `src/game/ColonyScene.ts`
- Modify: `src/main.ts`

**Interfaces:**
- Consumes: existing fixed world coordinates and interaction modes.
- Produces: neutral grayscale framing and a grid that is hidden initially but automatically visible during placement.

- [x] Keep the manifest-approved world art intact inside the grayscale HUD frame.
- [x] Initialize guides as hidden and keep `G` behavior intact.
- [x] Preserve collision debug colors because they communicate distinct debug categories.

### Task 4: Verify the complete player loop

**Files:**
- Test: `tests/e2e/colony.spec.ts`

**Interfaces:**
- Consumes: the finished UI and existing Playwright journey.
- Produces: evidence that build, hire, assign, haul, and sell remain available.

- [x] Run `npm run lint` and fix only regressions in changed files.
- [x] Run `npm run build` and confirm TypeScript plus production bundling pass.
- [x] Run `npm run e2e` and confirm the mine-to-market journey passes.
- [x] Capture the redesigned screen at 1600×900 and inspect hierarchy, clipping, and focus-visible controls.
