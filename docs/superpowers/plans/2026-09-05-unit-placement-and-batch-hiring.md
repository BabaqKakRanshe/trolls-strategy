# Unit Placement and Batch Hiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player choose a quantity and a free map cell before hiring goblins or trolls, and restore the goblin sprite to its original 24×24 display size.

**Architecture:** Replace the old single-unit purchase command with one atomic domain command containing unit kind, amount, and cell. Keep the pending placement only in `MapInteractionController`; Phaser and the native DOM controls submit the same cell choice without owning gold, capacity, or placement rules.

**Tech Stack:** TypeScript 6, Phaser 4, native HTML controls, Vitest, Playwright.

**Spec:** Current user request dated 2026-09-05; no separate design document is needed for this small change.

## Global Constraints

- `GameSession` remains the sole owner of mutable campaign state.
- The domain validates quantity, gold, map bounds, building occupancy, and the 20-unit cell limit before mutation.
- Use the existing files and interaction controller; do not add a new subsystem.
- Keep the test set minimal: update one domain test and the existing end-to-end journey.

---

### Task 1: Atomic batch purchase in a selected cell

**Files:**
- Modify: `src/domain/colony.ts`
- Modify: `src/application/GameSession.ts`
- Test: `tests/unit/colony.test.ts`

**Interfaces:**
- Produces: `GameCommand` variant `{ type: 'BUY_UNITS'; unitKind: UnitKind; amount: number; cell: Cell }`.
- Produces: `validateUnitPurchase(state, unitKind, amount, cell)` and `GameSession.canBuyUnits(unitKind, amount, cell)`.

- [x] **Step 1: Replace the crowd test with a failing selected-cell batch test**

```ts
const result = session.dispatch({
  type: 'BUY_UNITS',
  unitKind: 'goblin',
  amount: 20,
  cell: { x: 4, y: 6 },
});
expect(result.ok).toBe(true);
expect(new Set(session.snapshot().units.map(unitCell)).size).toBe(1);
```

- [x] **Step 2: Run the focused unit test and confirm the new command is not implemented**

Run: `npm.cmd test -- tests/unit/colony.test.ts`

Expected: FAIL because `BUY_UNITS` is absent.

- [x] **Step 3: Implement full validation followed by one cloned-state commit**

```ts
type GameCommand =
  | { type: 'BUILD_MINE'; cell: Cell }
  | { type: 'BUY_UNITS'; unitKind: UnitKind; amount: number; cell: Cell }
  | { type: 'ASSIGN_WORK'; unitIds: readonly string[]; buildingId: string }
  | { type: 'ASSIGN_HAUL'; unitIds: readonly string[]; sourceId: string; destinationId: string }
  | { type: 'RELEASE_UNITS'; unitIds: readonly string[] };

export function validateUnitPurchase(
  state: GameState,
  unitKind: UnitKind,
  amount: number,
  cell: Cell,
): CommandResult<void>;
```

Create sequential IDs and distinct `crowdPosition(cell, slot)` positions only after validation succeeds. Leave failed purchases completely unchanged.

- [x] **Step 4: Run the unit suite**

Run: `npm.cmd test`

Expected: all four focused economy tests pass.

### Task 2: Quantity and cell selection through existing presentation layers

**Files:**
- Modify: `src/application/MapInteractionController.ts`
- Modify: `src/game/ColonyScene.ts`
- Modify: `src/ui/AppUi.ts`
- Modify: `src/styles.css`
- Modify: `README.md`
- Test: `tests/e2e/colony.spec.ts`

**Interfaces:**
- Consumes: `GameSession.dispatch({ type: 'BUY_UNITS', unitKind, amount, cell })` and `canBuyUnits(...)`.
- Produces: interaction mode `{ kind: 'placing-units'; unitKind: UnitKind; amount: number }`.

- [x] **Step 1: Add one native quantity input and one keyboard-accessible coordinate form**

```html
<label for="hire-amount">За один клик</label>
<input id="hire-amount" type="number" min="1" max="20" value="1">
```

The hire buttons start placement rather than charging immediately. The coordinate form and a canvas click both call `placeUnits(cell)`.

- [x] **Step 2: Draw the existing one-cell placement preview from the canonical validator**

```ts
if (mode.kind === 'placing-units') {
  const result = this.session.canBuyUnits(mode.unitKind, mode.amount, cell);
  this.#drawCellPreview(cell, result.ok);
}
```

- [x] **Step 3: Restore only goblins to the prior display size**

```ts
if (unit.unitKind === 'goblin') sprite.setDisplaySize(24, 24);
else sprite.setScale(1.1);
```

- [x] **Step 4: Update the existing browser journey**

Set the quantity to five, start goblin placement, click a free cell once, and assert that goblin 5 exists. Hire and place the troll once, then continue the existing mine-to-market flow.

- [x] **Step 5: Run final verification**

Run: `npm.cmd run lint`, `npm.cmd test`, `npm.cmd run build`, and `npm.cmd run e2e`.

Expected: all checks pass and manual browser inspection shows five distinct goblins in the selected cell at 24×24 display size.
