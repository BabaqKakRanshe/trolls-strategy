# Collision Debug Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct the shifted building and unit hit areas, then expose their real Phaser geometry through an F1 debug panel.

**Architecture:** Keep building footprints and unit hit shapes inside `ColonyScene`, where Phaser input is configured. Store only the two transient debug toggles in the existing `MapInteractionController`; `AppUi` renders a native checkbox and the scene uses Phaser's built-in `enableDebug`/`removeDebug` functions, so the overlay cannot drift from the actual hit areas.

**Tech Stack:** TypeScript 6, Phaser 4, native HTML, CSS, Playwright.

**Spec:** Current user request dated 2026-09-05; this plan is the complete small-feature specification.

## Global Constraints

- Do not add a physics engine or a second collision representation.
- Building placement continues using the canonical domain grid footprints.
- Debug flags remain transient and never enter `GameSession` or save state.
- Update the existing E2E only; do not add a separate test file.

---

### Task 1: Correct Phaser hit areas

**Files:**
- Modify: `src/game/ColonyScene.ts`

**Interfaces:**
- Consumes: existing `BuildingSnapshot.width`, `BuildingSnapshot.height`, `CELL_SIZE`, and `UnitKind`.
- Produces: centered building rectangles and centered unit circles used for pointer events and debug rendering.

- [x] **Step 1: Replace centered custom coordinates with Phaser-local coordinates**

```ts
container.setSize(width, height).setInteractive(
  new Phaser.Geom.Rectangle(0, 0, width, height),
  Phaser.Geom.Rectangle.Contains,
);

const radius = unitCollisionRadius(unit.unitKind);
container.setSize(radius * 2, radius * 2).setInteractive(
  new Phaser.Geom.Circle(radius, radius, radius),
  Phaser.Geom.Circle.Contains,
);
```

- [x] **Step 2: Reuse the unit radius for the visible selection ring**

```ts
#drawSelection(graphics, selected, unitKind): void {
  const radius = unitCollisionRadius(unitKind);
  graphics.strokeCircle(0, 2, radius);
}
```

### Task 2: F1 debug control and real hit-area overlay

**Files:**
- Modify: `src/application/MapInteractionController.ts`
- Modify: `src/game/ColonyScene.ts`
- Modify: `src/ui/AppUi.ts`
- Modify: `src/styles.css`
- Modify: `README.md`
- Test: `tests/e2e/colony.spec.ts`

**Interfaces:**
- Produces: `debugPanelOpen`, `collisionDebugVisible`, `toggleDebugPanel()`, and `setCollisionDebugVisible(visible)` on `MapInteractionController`.
- Consumes: the same interactive Phaser containers configured in Task 1.

- [x] **Step 1: Add a failing F1 assertion to the existing E2E**

```ts
await page.keyboard.press('F1');
await expect(page.getByRole('region', { name: 'Отладка коллизий' })).toBeVisible();
await page.getByRole('checkbox', { name: 'Показывать коллизии' }).check();
```

Run: `npm.cmd run e2e`

Expected: FAIL because the debug region does not exist.

- [x] **Step 2: Add transient toggles and an accessible native panel**

```ts
toggleDebugPanel(): void;
setCollisionDebugVisible(visible: boolean): void;
```

`F1` prevents the browser help action, opens or closes the panel, and focuses the checkbox when opened. The checkbox keeps its native checked state and visible focus style.

- [x] **Step 3: Display Phaser's actual interactive shapes**

```ts
if (collisionDebugVisible && !container.input?.hitAreaDebug) {
  this.input.enableDebug(container, color);
} else if (!collisionDebugVisible && container.input?.hitAreaDebug) {
  this.input.removeDebug(container);
}
```

Buildings use cyan outlines and units use yellow outlines. New objects receive debug shapes during snapshot synchronization.

- [x] **Step 4: Verify the complete change**

Run: `npm.cmd run lint`, `npm.cmd test`, `npm.cmd run build`, and `npm.cmd run e2e`.

Expected: all checks pass; manual browser inspection shows centered collision shapes and pointer hover/click matches them.
