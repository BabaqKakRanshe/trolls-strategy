# Troll Strategy Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Создать первый полностью играбельный цикл поселения и детерминированной боевой миссии с сохранением состояния.

**Architecture:** Единственный изменяемый владелец партии — независимый от Phaser `GameSession`. Доменная симуляция принимает команды и фиксированные шаги времени, Phaser и HTML UI получают только immutable snapshots, а бой возвращает один типизированный settlement.

**Tech Stack:** Phaser 4.2.1, TypeScript 6.0.3, Vite 8.2.2, Vitest 5.0.0, ESLint 10.9.1, Playwright 1.62.1, WebGL.

**Spec:** `docs/plans/2026-09-04-troll-strategy-vertical-slice-design.md`

## Global Constraints

- Базовое логическое разрешение — 1280×720, Phaser Scale Mode — `FIT`, пиксельные текстуры — nearest-neighbor.
- `GameSession` — единственный изменяемый владелец состояния партии.
- Домен и application layer не импортируют `phaser` или DOM API. Persistence codec и coordinator также DOM-free; только `BrowserSaveStore` обращается к `localStorage` через `SavePort`.
- Экономика и бой используют одну модель `Unit` и одни `UnitId`.
- Симуляция обновляется фиксированными шагами и не зависит от FPS.
- Все балансные значения находятся в `src/content`, а не в сценах.
- Команды атомарны и возвращают discriminated union `CommandResult<T>`.
- Результат каждого запуска миссии применяется не более одного раза.
- `START_MISSION` принимает окончательную formation; `GameSession` сам создаёт и сохраняет canonical combat seed/input/report.
- `RESOLVE_MISSION` принимает только `MissionRunId`; presentation никогда не поставляет результат боя.
- Награды выдаются только при `winner === 'player'`; поражение и ничья не дают наград.
- Application layer не импортирует DOM или `localStorage`; сохранение подключается через `SavePort` в composition root.
- Runtime загружает только curated asset manifest с подтверждёнными лицензиями, а не всю папку `assets`.
- Офлайн-прогресс, фермы, магия, гарнизон, карта территорий и сложный крафт не входят в этот план.

---

## Planned file map

```text
index.html                         HTML shell and accessible overlay root
package.json                       scripts and pinned toolchain
vite.config.ts                     Vite and Vitest configuration
eslint.config.js                   TypeScript lint rules
playwright.config.ts               browser smoke configuration
src/main.ts                        composition root
src/styles.css                     responsive pixel UI shell
src/content/catalog.ts             all MVP balance and content records
src/domain/model.ts                IDs, units, buildings, inventories, mission types
src/domain/results.ts              command errors and result unions
src/domain/initialState.ts         canonical new-game state factory
src/domain/colony/commands.ts      recruit and assignment rules
src/domain/colony/tickColony.ts    mining and hauling fixed-step simulation
src/domain/combat/simulateCombat.ts deterministic combat resolver
src/domain/progression/rewards.ts  first-clear and repeat rewards
src/application/GameSession.ts     commands, ticking, mission lifecycle, snapshots
src/application/snapshot.ts        readonly presentation projection
src/persistence/saveCodec.ts       versioned encode/decode/repair
src/persistence/autosave.ts        browser SavePort adapter
src/persistence/autosaveCoordinator.ts composition-root save scheduling
src/game/config.ts                 Phaser 4 configuration
src/game/scenes/BootScene.ts       generated placeholder textures
src/game/scenes/ColonyScene.ts     colony world rendering and selection
src/game/scenes/CombatScene.ts     combat playback
src/ui/AppUi.ts                    DOM UI coordinator
src/ui/renderColonyPanel.ts        resources, selected units and building actions
src/ui/renderMissionPanel.ts       mission selection and formation editor
tests/unit/*.test.ts               domain/application/persistence tests
tests/e2e/vertical-slice.spec.ts   full browser cycle
scripts/copy-curated-assets.mjs    copy only manifest-approved source assets
assets/curated-assets.json         canonical source-to-runtime asset manifest
assets/licenses.json               source and license evidence for curated packs
public/assets/manifest.json        generated Phaser loader manifest
public/assets/runtime/             generated curated runtime files
```

## Task 1: Bootstrap the executable Phaser 4 shell

**Files:**
- Create: `package.json`
- Create: `tsconfig.json`
- Create: `vite.config.ts`
- Create: `eslint.config.js`
- Create: `index.html`
- Create: `src/main.ts`
- Create: `src/styles.css`
- Create: `src/game/config.ts`
- Create: `src/game/scenes/BootScene.ts`
- Test: `tests/unit/bootstrap.test.ts`

**Interfaces:**
- Produces: `createGameConfig(parent: string): Phaser.Types.Core.GameConfig`
- Produces: a browser page containing `#game-root`, `#game-canvas`, and `#ui-root`

- [ ] **Step 1: Create the package and compiler configuration**

```json
{
  "name": "troll-strategy",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc --noEmit && vite build",
    "test": "vitest run",
    "test:watch": "vitest",
    "lint": "eslint .",
    "e2e": "playwright test"
  },
  "dependencies": { "phaser": "4.2.1" },
  "devDependencies": {
    "@eslint/js": "10.0.1",
    "@playwright/test": "1.62.1",
    "eslint": "10.9.1",
    "typescript": "6.0.3",
    "typescript-eslint": "8.69.0",
    "vite": "8.2.2",
    "vitest": "5.0.0"
  }
}
```

Use `strict: true`, `noUncheckedIndexedAccess: true`, `exactOptionalPropertyTypes: true`, `moduleResolution: "Bundler"`, `target: "ES2022"`, and include `src`, `tests`, `vite.config.ts`, and `playwright.config.ts` in `tsconfig.json`.

- [ ] **Step 2: Install dependencies and write the failing bootstrap test**

```ts
import { describe, expect, it } from 'vitest';
import { createGameConfig } from '../../src/game/config';

describe('Phaser configuration', () => {
  it('uses the approved logical size and pixel rendering', () => {
    const config = createGameConfig('game-canvas');
    expect(config.width).toBe(1280);
    expect(config.height).toBe(720);
    expect(config.pixelArt).toBe(true);
    expect(config.parent).toBe('game-canvas');
  });
});
```

Run: `npm install` then `npm test -- tests/unit/bootstrap.test.ts`  
Expected: FAIL because `src/game/config.ts` does not exist.

- [ ] **Step 3: Implement the boot shell**

`createGameConfig` must return WebGL, 1280×720, transparent `false`, background `#173f2a`, pixel art enabled, round pixels enabled, `Phaser.Scale.FIT`, `Phaser.Scale.CENTER_BOTH`, and the scene list `[BootScene]`. `BootScene.create()` must create a 16×16 generated checker texture named `ground` and render a full-screen tiled green background without loading external assets. `main.ts` must instantiate Phaser only after `DOMContentLoaded` and expose no mutable game state on `window`.

- [ ] **Step 4: Verify the shell**

Run: `npm test -- tests/unit/bootstrap.test.ts`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS and `dist/index.html` exists.

- [ ] **Step 5: Commit**

```bash
git init
git add package.json tsconfig.json vite.config.ts eslint.config.js index.html src tests/unit/bootstrap.test.ts
git commit -m "chore: bootstrap Phaser 4 project"
```

## Task 2: Define canonical content and new-game state

**Files:**
- Create: `src/content/catalog.ts`
- Create: `src/domain/model.ts`
- Create: `src/domain/results.ts`
- Create: `src/domain/initialState.ts`
- Test: `tests/unit/initial-state.test.ts`

**Interfaces:**
- Produces: branded string types `UnitId`, `BuildingId`, `ItemId`, `MissionRunId`
- Produces: `GameState`, `Unit`, `Item`, `Equipment`, `ColonyInventory`, `Building`, `Inventory`, `Assignment`, `Formation`, `MissionState`
- Produces: `createInitialState(seed?: number): GameState`; the seed is normalized to uint32 and a fixed deterministic default is used when omitted
- Produces: `CommandResult<T> = { ok: true; value: T } | { ok: false; error: CommandError }`

- [ ] **Step 1: Write the failing initial-state test**

```ts
import { describe, expect, it } from 'vitest';
import { createInitialState } from '../../src/domain/initialState';

describe('createInitialState', () => {
  it('creates a playable colony with no shared mutable references', () => {
    const first = createInitialState();
    const second = createInitialState();
    expect(first.wallet.gold).toBe(500);
    expect(first.buildings.map((building) => building.kind)).toEqual([
      'town-hall', 'mine', 'warehouse', 'market',
    ]);
    first.wallet.gold = 0;
    expect(second.wallet.gold).toBe(500);
  });
});
```

Run: `npm test -- tests/unit/initial-state.test.ts`  
Expected: FAIL because the state factory is missing.

- [ ] **Step 2: Implement domain records and catalogs**

Define `Unit` with `id`, `species`, `name`, `level`, `health`, `maxHealth`, `strength`, `speed`, `carryCapacity`, `attackIntervalMs`, `damage`, `armor`, `assignment`, and `equipment: Equipment`. `Equipment` has exclusive `weaponId` and `armorId` slots, each `ItemId | null`. Define `Item` with `id`, `kind`, `slot`, `damageBonus`, and `armorBonus`; every live item is referenced either once by `GameState.colonyInventory.itemIds` or once by a unit equipment slot. Define assignments as `{ kind: 'idle' }`, `{ kind: 'work'; buildingId }`, `{ kind: 'haul'; fromId; toId; resource: 'ironOre'; carried; phase: 'toSource' | 'loading' | 'toDestination' | 'unloading'; progressMs: number }`, `{ kind: 'mission'; runId }`, or `{ kind: 'dead' }`.

Catalog values:

```ts
export const UNIT_CATALOG = {
  goblin: { cost: 35, maxHealth: 20, strength: 3, speed: 5, carryCapacity: 10, damage: 2, armor: 1, attackIntervalMs: 2000 },
  troll: { cost: 170, maxHealth: 55, strength: 9, speed: 2, carryCapacity: 30, damage: 7, armor: 3, attackIntervalMs: 2600 },
} as const;

export const BUILDING_CATALOG = {
  mine: { workerCapacity: 5, orePerStrengthSecond: 0.1, inventoryCapacity: 100 },
  warehouse: { inventoryCapacity: 500 },
  market: { oreSalePrice: 3 },
} as const;

export const ITEM_CATALOG = {
  'rusty-sword': { slot: 'weapon', damageBonus: 1, armorBonus: 0 },
  'patched-armor': { slot: 'armor', damageBonus: 0, armorBonus: 1 },
} as const;
```

Initial state must contain the four buildings, 500 gold, zero ore, no units, four rusty swords and four patched armors in `colonyInventory`, mission cooldown `120_000`, simulation time `0`, normalized `campaignSeed`, schema version `1`, empty applied-run IDs, `accumulators: { mineOre: 0 }`, and `MissionState.activeRun: null`. `GameState` owns both `campaignSeed` and the fractional mining accumulator so deterministic mission seeds and partial production progress survive save/load. The active-run contract is already serializable as `ActiveMissionRun | null` and includes formation, combat seed, canonical input/report, and lifecycle status.

- [ ] **Step 3: Verify state isolation and types**

Run: `npm test -- tests/unit/initial-state.test.ts`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS with no unchecked-index or optional-property errors.

- [ ] **Step 4: Commit**

```bash
git add src/content src/domain tests/unit/initial-state.test.ts
git commit -m "feat: define colony domain model"
```

## Task 3: Implement atomic recruitment and assignment commands

**Files:**
- Create: `src/domain/colony/commands.ts`
- Test: `tests/unit/colony-commands.test.ts`

**Interfaces:**
- Consumes: `GameState`, IDs, catalogs, and `CommandResult`
- Produces: `recruitUnit(state, species, id, name): CommandResult<UnitId>`
- Produces: `assignWorkers(state, unitIds, buildingId): CommandResult<void>`
- Produces: `assignHauler(state, unitId, fromId, toId): CommandResult<void>`
- Produces: `setIdle(state, unitIds): CommandResult<void>`
- Produces: `equipItem(state, unitId, itemId): CommandResult<void>`
- Produces: `unequipItem(state, unitId, slot): CommandResult<void>`

- [ ] **Step 1: Write failing command tests**

```ts
it('recruits atomically and deducts the exact price', () => {
  const state = createInitialState();
  const result = recruitUnit(state, 'goblin', unitId('u1'), 'Grik');
  expect(result).toEqual({ ok: true, value: unitId('u1') });
  expect(state.wallet.gold).toBe(465);
  expect(state.units[0]?.assignment).toEqual({ kind: 'idle' });
});

it('does not partially assign workers past mine capacity', () => {
  const state = stateWithGoblinCount(6);
  const before = structuredClone(state);
  const result = assignWorkers(state, state.units.map((unit) => unit.id), buildingId('mine'));
  expect(result).toEqual({ ok: false, error: { code: 'CAPACITY_EXCEEDED' } });
  expect(state).toEqual(before);
});
```

Add cases for insufficient gold, duplicate IDs, missing buildings, mission-assigned units, same haul source/destination, and changing a worker back to idle.

Add equipment cases proving that equipping atomically moves an item from `colonyInventory` into the matching unit slot, replacing an occupied slot returns the old item to colony inventory, incompatible slots and already-owned items are rejected without mutation, and unequipping returns the item exactly once.

Run: `npm test -- tests/unit/colony-commands.test.ts`  
Expected: FAIL because commands are missing.

- [ ] **Step 2: Implement validate-then-commit mutations**

Each function must validate every input before changing `state`. `assignWorkers` and `setIdle` reject an empty unit selection with `INVALID_ASSIGNMENT`, so a successful batch command always changes state. `assignWorkers` accepts only a mine, ensures all units exist and are currently idle or already assigned to that mine, and checks the final unique worker count against capacity five. `assignHauler` requires two existing distinct buildings and an idle unit, then initializes `phase: 'toSource'`, `progressMs: 0`, and `carried: 0`. `setIdle` rejects the entire selection with `INVALID_ASSIGNMENT` when any selected hauler has `carried > 0`, preserving both cargo and every selected assignment. `equipItem` and `unequipItem` validate unit availability, item ownership and slot compatibility before changing either side of the exclusive ownership relation. All failures use stable codes: `NOT_FOUND`, `INSUFFICIENT_GOLD`, `INVALID_ASSIGNMENT`, `INVALID_EQUIPMENT`, `CAPACITY_EXCEEDED`, or `DUPLICATE_ID`.

- [ ] **Step 3: Run focused and full domain tests**

Run: `npm test -- tests/unit/colony-commands.test.ts`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/domain/colony tests/unit/colony-commands.test.ts
git commit -m "feat: add atomic colony commands"
```

## Task 4: Implement deterministic production and hauling

**Files:**
- Create: `src/domain/colony/tickColony.ts`
- Test: `tests/unit/colony-tick.test.ts`

**Interfaces:**
- Consumes: `GameState` and `deltaMs`
- Produces: `tickColony(state: GameState, deltaMs: number): void`
- Invariant: caller passes positive multiples of `250`

- [ ] **Step 1: Write failing simulation tests**

```ts
it('mines ore from total worker strength', () => {
  const state = stateWithAssignedWorkers(['goblin', 'troll']);
  tickColony(state, 10_000);
  expect(inventoryOf(state, 'mine').ironOre).toBe(12);
});

it('hauls only the unit carry capacity and preserves resources', () => {
  const state = stateWithHaulerAndMineOre('goblin', 25);
  const totalBefore = totalIronOre(state);
  tickColony(state, 20_000);
  expect(inventoryOf(state, 'warehouse').ironOre).toBeGreaterThan(0);
  expect(totalIronOre(state)).toBe(totalBefore);
});
```

Add cases for full mine inventory, full warehouse inventory, empty source, invalid `deltaMs`, and equivalence of forty 250 ms ticks with one 10,000 ms call.

Run: `npm test -- tests/unit/colony-tick.test.ts`  
Expected: FAIL because the tick function is missing.

- [ ] **Step 2: Implement fixed-step accumulation**

Track fractional production in `state.accumulators.mineOre`. Mining per second equals total assigned worker strength multiplied by `0.1`. Haulers alternate `toSource`, `loading`, `toDestination`, and `unloading`; travel time is derived from a fixed building-distance table divided by unit speed. Loading never exceeds source stock, capacity, or target free space. No resource may be created or destroyed during hauling.

- [ ] **Step 3: Verify deterministic chunking**

Run: `npm test -- tests/unit/colony-tick.test.ts`  
Expected: PASS with identical final state for equivalent elapsed time.  
Run: `npm test`  
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/domain/colony/tickColony.ts tests/unit/colony-tick.test.ts
git commit -m "feat: simulate mining and hauling"
```

## Task 5: Add market selling and immutable snapshots

**Files:**
- Create: `src/application/snapshot.ts`
- Create: `src/application/GameSession.ts`
- Test: `tests/unit/game-session-colony.test.ts`

**Interfaces:**
- Produces: `GameSession.createNew(seed?: number): GameSession`
- Produces: `session.dispatch(command: GameCommand): CommandResult<unknown>`
- Produces: `session.advance(realDeltaMs: number): void`
- Produces: `session.getSnapshot(): GameSnapshot`
- Produces: `session.exportState(): GameState`
- Produces command: `{ type: 'SELL_ORE'; buildingId; amount }`
- Consumes commands: `EQUIP_ITEM` and `UNEQUIP_ITEM`

- [ ] **Step 1: Write failing application tests**

```ts
it('sells warehouse ore through one atomic command', () => {
  const session = sessionWithWarehouseOre(10);
  expect(session.dispatch({ type: 'SELL_ORE', buildingId: buildingId('warehouse'), amount: 4 })).toEqual({ ok: true, value: 12 });
  expect(session.getSnapshot().wallet.gold).toBe(512);
  expect(session.getSnapshot().buildings.find((b) => b.id === 'warehouse')?.inventory.ironOre).toBe(6);
});

it('returns detached frozen snapshots', () => {
  const session = GameSession.createNew(7);
  const snapshot = session.getSnapshot();
  expect(Object.isFrozen(snapshot)).toBe(true);
  expect(() => Reflect.set(snapshot.wallet, 'gold', 0)).toThrow();
  expect(session.getSnapshot().wallet.gold).toBe(500);
});

it('exports a detached state for persistence without exposing session ownership', () => {
  const session = GameSession.createNew(7);
  const exported = session.exportState();
  exported.wallet.gold = 0;
  expect(session.getSnapshot().wallet.gold).toBe(500);
});
```

Run: `npm test -- tests/unit/game-session-colony.test.ts`  
Expected: FAIL because `GameSession` is missing.

- [ ] **Step 2: Implement the application owner**

`GameSession` stores private `GameState`, accepts recruit/assign/idle/equip/unequip/sell commands, and accumulates real delta until it can call `tickColony` in 250 ms steps. Cap one rendered-frame contribution at 5,000 ms to prevent a background-tab spiral. `SELL_ORE` validates a positive integer amount and available warehouse stock, deducts ore, and adds `amount * 3` gold atomically. `getSnapshot` returns a recursively frozen plain-data projection including colony items and unit equipment. `exportState()` returns a detached structured clone for the save codec; mutating it cannot mutate the session.

- [ ] **Step 3: Verify the command boundary**

Run: `npm test -- tests/unit/game-session-colony.test.ts`  
Expected: PASS.  
Run: `rg "from ['\"]phaser" src/domain src/application src/persistence`  
Expected: no matches.

- [ ] **Step 4: Commit**

```bash
git add src/application tests/unit/game-session-colony.test.ts
git commit -m "feat: add game session command boundary"
```

## Task 6: Implement seeded deterministic autobattle

**Files:**
- Create: `src/domain/combat/simulateCombat.ts`
- Test: `tests/unit/combat.test.ts`

**Interfaces:**
- Produces: `CombatantSpec`, `FormationSlot`, `CombatFrame`, `CombatReport`
- Produces: `simulateCombat(input: CombatInput): CombatReport`
- Input includes `seed`, four player slots, enemy slots, and `maxDurationMs: 120_000`

- [ ] **Step 1: Write failing golden tests**

```ts
it('resolves the same formation identically for the same seed', () => {
  const input = missionOneCombatInput(12345);
  expect(simulateCombat(input)).toEqual(simulateCombat(input));
});

it('matches the mission-one golden outcome', () => {
  const report = simulateCombat(missionOneCombatInput(12345));
  expect({ winner: report.winner, durationMs: report.durationMs, dead: report.deadPlayerUnitIds }).toEqual({
    winner: 'player',
    durationMs: 18000,
    dead: [],
  });
});
```

The helper formation must use four trolls against four catalog enemy goblins so the golden result is stable and clearly winning; fixtures import those stats from `UNIT_CATALOG` instead of duplicating balance values. Add tests for armor floor of one damage, attack cooldowns, deterministic target selection (including two seeds that select different tied targets while either seed repeats exactly), empty player formation, timeout, occupied-cell movement, and input non-mutation.

Run: `npm test -- tests/unit/combat.test.ts`  
Expected: FAIL because the resolver is missing.

- [ ] **Step 2: Implement the pure resolver**

Use a local seeded PRNG with no `Math.random`. Advance at 100 ms fixed steps. Combatants target the nearest living enemy by Manhattan distance, breaking ties by seeded random value and then stable code-unit ID order (never locale-dependent comparison). They move one orthogonal cell when their movement accumulator reaches `1000 / speed`; movement reserves living occupied cells for the whole action order, tries the primary axis before the orthogonal fallback, and never overlaps or crosses an occupied cell. Adjacent combatants attack when cooldown reaches zero. Damage equals `max(1, damage - armor)`. Record presentation frames at 200 ms intervals. End when one side has no living combatants or at 120 seconds; timeout winner is the side with higher remaining-health ratio, with equal ratios producing `draw`. `CombatReport` exposes `deadPlayerUnitIds: UnitId[]` and separate enemy casualty identifiers so mission settlement can never confuse an enemy ID with a colony ID.

Before creating mutable simulation state or advancing the PRNG, validate the complete input and throw `RangeError` without mutating it: `playerSlots` contain only player specs with non-null unique `UnitId`s, `enemySlots` contain only enemy specs with `unitId: null`, all combatant IDs and starting cells are unique, coordinates are integer cells inside the 6×4 board, numeric stats are finite with positive health/attack interval and non-negative speed/damage/armor, the seed is an integer, and `maxDurationMs` is a 100 ms multiple from 100 through 120,000. `CombatantSpec` is a discriminated union of player and enemy variants so invalid side/unit-ID pairs are also rejected by TypeScript.

- [ ] **Step 3: Lock the golden report**

Run the focused test once, inspect the actual deterministic duration, and set the asserted `durationMs` to that observed value while keeping all other mechanics fixed. This is the only expected-value calibration; do not change combat code merely to force 18,000 ms.

Run: `npm test -- tests/unit/combat.test.ts`  
Expected: PASS on repeated runs.

- [ ] **Step 4: Commit**

```bash
git add src/domain/combat tests/unit/combat.test.ts
git commit -m "feat: add deterministic autobattle"
```

## Task 7: Integrate mission cooldown, formation, rewards, and permanent death

**Files:**
- Create: `src/domain/progression/rewards.ts`
- Modify: `src/domain/model.ts`
- Modify: `src/application/GameSession.ts`
- Modify: `src/application/snapshot.ts`
- Test: `tests/unit/mission-lifecycle.test.ts`

**Interfaces:**
- Produces command: `{ type: 'START_MISSION'; missionId: 'mission-1'; formation: Formation }`
- Produces command: `{ type: 'RESOLVE_MISSION'; runId: MissionRunId }`
- Produces command: `{ type: 'ABORT_MISSION'; runId: MissionRunId }`
- Produces: `getMissionOneReward(firstClear: boolean): Reward`
- `START_MISSION` returns the active `MissionRunId`; `GameSession` creates and stores its seed, `CombatInput`, and `CombatReport`
- Produces: `ActiveMissionRun` with `runId`, `missionId`, final `formation`, `combatSeed`, canonical `combatInput`, canonical `combatReport`, and lifecycle status; `MissionState.activeRun` becomes `ActiveMissionRun | null`

- [ ] **Step 1: Write failing lifecycle tests**

```ts
it('locks selected units until the mission is settled', () => {
  const session = readySessionWithFourTrolls();
  const started = session.dispatch({ type: 'START_MISSION', missionId: 'mission-1', formation: fourTrollFormation() });
  expect(started.ok).toBe(true);
  expect(session.dispatch({ type: 'ASSIGN_WORKERS', unitIds: [trollIds()[0]!], buildingId: buildingId('mine') })).toEqual({
    ok: false,
    error: { code: 'INVALID_ASSIGNMENT' },
  });
});

it('applies death and reward exactly once', () => {
  const { session, runId } = resolvedMissionFixture();
  expect(session.dispatch({ type: 'RESOLVE_MISSION', runId }).ok).toBe(true);
  const afterFirst = session.getSnapshot();
  expect(session.dispatch({ type: 'RESOLVE_MISSION', runId })).toEqual({ ok: false, error: { code: 'ALREADY_APPLIED' } });
  expect(session.getSnapshot()).toEqual(afterFirst);
});
```

Add cases for unavailable cooldown, fewer than one or more than four units, duplicate units, invalid or duplicate cells, abort returning living units, and canonical report creation without accepting a report from the caller. Assert that equipped item bonuses are present exactly once in canonical combatant stats and that exported active-run state contains identical formation, seed, input, and report to the mission snapshot. Defeat deletes only `deadPlayerUnitIds` and their equipped items and gives `{ gold: 0, ironOre: 0, unlocks: [] }`; draw gives the same empty reward. A player victory gives first-clear reward `{ gold: 300, ironOre: 40, unlocks: ['smelter-recipe'] }` or repeat reward `{ gold: 80, ironOre: 10, unlocks: [] }`.

- [ ] **Step 2: Implement mission transactions**

Mission one becomes available when `simulationTimeMs >= nextMissionAtMs`. `START_MISSION` validates the complete formation before mutation, creates a monotonically unique run ID and deterministic combat seed from state, marks selected units with the mission assignment, builds canonical `CombatInput`, calls `simulateCombat`, and stores immutable formation, seed, input, report, and lifecycle status in the active run. `RESOLVE_MISSION` accepts only the matching run ID and applies the stored report: it deletes `deadPlayerUnitIds` and their equipped items, restores survivors to idle, and applies first-clear or repeat rewards only when `winner === 'player'`. Defeat and draw apply no reward and do not mark first clear. Settlement records the run ID, schedules the next mission 120,000 simulation milliseconds later, and clears the active mission in one validated transaction. The formation draft exists only before `START_MISSION`; afterward `ABORT_MISSION` settles that same stored report (including casualties, reward, idempotence, and cooldown) and maps a successful result to `void`, so it cannot be used to escape a loss. Ore rewards fill the warehouse only to its catalog capacity and atomically place all overflow in the town hall without loss.

- [ ] **Step 3: Verify the complete headless loop**

Run: `npm test -- tests/unit/mission-lifecycle.test.ts`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/domain/progression src/application tests/unit/mission-lifecycle.test.ts
git commit -m "feat: connect colony progression to missions"
```

## Task 8: Add versioned persistence and autosave

**Files:**
- Create: `src/persistence/saveCodec.ts`
- Create: `src/persistence/autosave.ts`
- Create: `src/persistence/autosaveCoordinator.ts`
- Modify: `src/application/GameSession.ts`
- Modify: `src/main.ts`
- Test: `tests/unit/save-codec.test.ts`
- Test: `tests/unit/autosave-coordinator.test.ts`

**Interfaces:**
- Produces: `encodeSave(state: GameState): string`
- Produces: `decodeSave(raw: string): DecodeResult<GameState>`
- Produces: `SavePort = { load(): string | null; save(serialized: string): void; clear(): void }`
- Produces: `BrowserSaveStore implements SavePort`
- Produces: `AutosaveCoordinator.dispatch(command)`, `advance(realDeltaMs)`, and `flush()`
- Produces: `GameSession.fromSave(raw: string): CommandResult<GameSession>`
- Consumes: detached `GameSession.exportState()`; neither `GameSession` nor the coordinator imports DOM APIs

- [ ] **Step 1: Write failing codec tests**

```ts
it('round-trips a progressed game without losing IDs or assignments', () => {
  const state = progressedStateFixture();
  expect(decodeSave(encodeSave(state))).toEqual({ ok: true, value: state });
});

it('repairs safe numeric corruption and rejects structural corruption', () => {
  expect(decodeSave('{"schemaVersion":1,"wallet":{"gold":-5}}').ok).toBe(false);
  const repaired = decodeSave(saveFixture({ wallet: { gold: -5 } }));
  expect(repaired.ok && repaired.value.wallet.gold).toBe(0);
});
```

Add cases for malformed JSON, unknown future schema, duplicate unit IDs, dangling building assignments, active mission consistency, and applied-run IDs.

Add a round-trip case that starts but does not settle a mission, saves it, restores it through `GameSession.fromSave`, and proves that formation, seed, canonical input, canonical report, equipment ownership, haul `phase`/`progressMs`, and mission status are byte-for-byte equivalent after re-encoding. Add coordinator tests with an in-memory `SavePort`: successful recruit, assignment, equip, unequip, sale, mission start, abort, and resolve flush immediately; a rejected command does not save; foreground advancement flushes once per ten simulated seconds.

- [ ] **Step 2: Implement one schema-aware codec**

Validate the complete version-one object before constructing state. Clamp finite economy counters to zero, restore missing optional UI-independent accumulators to zero, reject duplicate IDs, duplicate item ownership, invalid equipment slots, invalid haul state, dangling references, and inconsistent active missions; never silently accept an unknown future version. `GameSession.fromSave` receives serialized data, but delegates all decoding to the single codec. Keep the storage key `troll-strategy.save.v1` and all `localStorage` access in `autosave.ts`. `AutosaveCoordinator` depends only on `SavePort`, wraps session dispatch/advance at the composition root, serializes `session.exportState()`, saves after successful recruit, assignment, equip, unequip, sale, mission start, abort, and resolution commands, and saves once per ten seconds of foreground simulation. `GameSession` and application modules never import the browser adapter or DOM APIs.

- [ ] **Step 3: Verify persistence**

Run: `npm test -- tests/unit/save-codec.test.ts tests/unit/autosave-coordinator.test.ts`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/persistence src/application/GameSession.ts src/main.ts tests/unit/save-codec.test.ts tests/unit/autosave-coordinator.test.ts
git commit -m "feat: persist versioned game sessions"
```

## Task 9: Curate, license-check, copy, and preload runtime assets

**Files:**
- Create: `assets/curated-assets.json`
- Create: `assets/licenses.json`
- Create: `scripts/copy-curated-assets.mjs`
- Create: `src/game/scenes/AssetPreloadScene.ts`
- Modify: `src/game/scenes/BootScene.ts`
- Modify: `src/game/config.ts`
- Create: `public/assets/manifest.json` (generated)
- Create: `public/assets/runtime/` (generated curated files only)
- Test: `tests/unit/asset-manifest.test.ts`

**Interfaces:**
- Produces: `RuntimeAssetManifest` entries `{ key, type, source, output, licenseId, frameWidth?, frameHeight?, frameDurationMs?, animationStart?, animationEnd?, preload? }`; generated `output` URLs are relative to Vite `BASE_URL`
- Produces: `npm run assets:prepare`, which validates and copies only listed files
- Produces: Phaser cache keys `unit.goblin.*`, `unit.troll.*`, `world.ground`, `world.props`, `music.colony`, `music.battle`, `sfx.attack`, `sfx.death`, and `sfx.buy`

- [ ] **Step 1: Write the failing manifest test**

The test reads `assets/curated-assets.json` and `assets/licenses.json`, asserts unique cache keys and output paths, verifies every source is a regular file below the project `assets` directory, rejects `..`, absolute output paths, and directory entries, and requires every `licenseId` to resolve to a record with non-empty `sourceUrl`, `licenseName`, and `licenseEvidence`. Assert that animated goblin and troll sheets declare `frameWidth: 32`, `frameHeight: 32`, and their documented 100/200 ms frame durations. Assert that no manifest output is outside `public/assets/runtime`.

Run: `npm test -- tests/unit/asset-manifest.test.ts`  
Expected: FAIL because curated manifests do not exist.

- [ ] **Step 2: Record the exact curated source set and license evidence**

Include only the needed Minifantasy goblin and troll `Idle`, `Walk`, `Attack`, `Dmg`, and death PNG sheets; Forgotten Plains ground tiles and props; the OGG colony, battle, and victory tracks; and one buy, attack, and death WAV effect. Declare the first directional row for each four-direction sheet and the complete single row for death. Mark battle and victory music `preload: false`; boot may load visuals, effects, and colony music only. Use the exact existing source paths under `assets`, including the 32×32 animation metadata. Each manifest entry references one license record. Inspect the supplied pack documentation and original publisher page, then record the exact license name, source URL, attribution requirement, and local evidence path in `assets/licenses.json`. Generate a deployed credits artifact containing required attribution. If commercial evidence cannot be established, record a visible commercial-release gate; never infer a license from the presence of a file.

- [ ] **Step 3: Implement the guarded copy script**

Add `"assets:prepare": "node scripts/copy-curated-assets.mjs"` to `package.json`. The script resolves every source and output path, verifies the resolved source remains below `assets` and the output remains below `public/assets/runtime`, refuses missing license evidence, copies files one-by-one, removes only stale files previously listed by `public/assets/manifest.json`, and writes the new runtime manifest atomically. It must never recursively copy or delete the source `assets` directory.

- [ ] **Step 4: Preload the curated files with fallback behavior**

`BootScene` loads only `/assets/manifest.json`, then starts `AssetPreloadScene`. `AssetPreloadScene` queues images, spritesheets, and audio from manifest runtime URLs, creates goblin/troll animations from the declared 32×32 metadata, and starts the colony after completion. A missing manifest or individual failed file uses the already-generated placeholder texture for that logical key and surfaces one non-blocking loading warning; it must not change domain state. Register both scenes in `createGameConfig`.

Run: `npm run assets:prepare`  
Expected: PASS and only manifest-listed files exist below `public/assets/runtime`.  
Run: `npm test -- tests/unit/asset-manifest.test.ts`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add package.json assets/curated-assets.json assets/licenses.json scripts src/game public/assets tests/unit/asset-manifest.test.ts
git commit -m "feat: curate and preload licensed game assets"
```

## Task 10: Build the colony presentation and HTML command UI

**Files:**
- Modify: `src/main.ts`
- Modify: `src/game/config.ts`
- Create: `src/game/scenes/ColonyScene.ts`
- Create: `src/game/input/selection.ts`
- Create: `src/ui/AppUi.ts`
- Create: `src/ui/renderColonyPanel.ts`
- Modify: `src/styles.css`
- Test: `tests/unit/selection.test.ts`
- Test: `tests/unit/render-colony-panel.test.ts`

**Interfaces:**
- Consumes: composition-root `AutosaveCoordinator.dispatch`, `advance`, and its session snapshots
- Produces: `SelectionModel` with click, Ctrl-click, drag rectangle, and clear operations
- Emits DOM `CustomEvent<GameCommand>('game-command')` from accessible buttons

- [ ] **Step 1: Write failing selection tests**

```ts
it('replaces selection on click and toggles on modified click', () => {
  const selection = new SelectionModel();
  selection.click(unitId('u1'), false);
  selection.click(unitId('u2'), true);
  selection.click(unitId('u1'), true);
  expect(selection.ids()).toEqual([unitId('u2')]);
});
```

Add drag-box inclusion using world coordinates and clearing on empty ground.

Run: `npm test -- tests/unit/selection.test.ts`  
Expected: FAIL because the selection model is missing.

- [ ] **Step 2: Implement the colony scene**

Render the colony ground/props and goblin/troll animations from the curated Phaser cache, assembling four visually distinct building markers from licensed tiles/props; use generated placeholders only for keys that failed to load. Render assignment icons and short movement interpolation for haulers from their saved phase/progress. Scene `update(_, delta)` calls the composition-root coordinator's `advance(delta)` and re-renders only when snapshot revision changes. Pointer clicks and drag selection update `SelectionModel`; building clicks set the active building. The scene dispatches no domain mutation directly.

- [ ] **Step 3: Implement the accessible DOM overlay**

Create a left resources panel, selected-unit panel, active-building actions, recruit buttons, colony-equipment list, equipped weapon/armor slots, equip/unequip buttons, and mission cooldown button. Include accessible bulk actions “Выбрать первых 4 свободных”, “Выбрать работников шахты”, and the contextual route action “Назначить маршрут: шахта → склад”, so the full logistics loop is executable without developer controls. UI commands flow through `AutosaveCoordinator.dispatch`, not directly to mutable state. Every interactive element is a native `<button type="button">` with visible focus state and `aria-label`; numeric changes use an `aria-live="polite"` status region without announcing every simulation tick. CSS uses a local system monospace fallback until art fonts are added, crisp borders, responsive `clamp()`, safe-area padding, and absolute overlay positioning above the Phaser canvas. Add a DOM rendering test that selects a unit, equips a starter weapon, verifies it leaves colony inventory and appears in the weapon slot, then unequips it and verifies the inverse.

- [ ] **Step 4: Verify the colony UI**

Run: `npm test -- tests/unit/selection.test.ts tests/unit/render-colony-panel.test.ts`  
Expected: PASS.  
Run: `npm run lint && npm run build`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/main.ts src/game src/ui src/styles.css tests/unit/selection.test.ts tests/unit/render-colony-panel.test.ts
git commit -m "feat: render interactive colony"
```

## Task 11: Build mission selection, formation, and combat playback

**Files:**
- Create: `src/game/scenes/CombatScene.ts`
- Create: `src/ui/renderMissionPanel.ts`
- Modify: `src/ui/AppUi.ts`
- Modify: `src/game/config.ts`
- Test: `tests/unit/formation.test.ts`

**Interfaces:**
- Consumes: active mission snapshot and `CombatReport.frames`
- Produces: `validateFormation(unitIds, slots): CommandResult<Formation>`
- Scene transition payload contains only `runId`; `CombatScene` reads the canonical report from the immutable session snapshot

- [ ] **Step 1: Write failing formation tests**

```ts
it('accepts one to four unique colony units in unique player cells', () => {
  expect(validateFormation(trollIds(), fourPlayerSlots()).ok).toBe(true);
});

it('rejects duplicate cells without mutating the draft', () => {
  const draft = duplicateCellFormation();
  const before = structuredClone(draft);
  expect(validateFormation(draft.unitIds, draft.slots)).toEqual({ ok: false, error: { code: 'INVALID_FORMATION' } });
  expect(draft).toEqual(before);
});
```

Run: `npm test -- tests/unit/formation.test.ts`  
Expected: FAIL because formation validation is missing.

- [ ] **Step 2: Implement mission and formation UI**

Render mission one with enemies, combined health, four-unit limit, first-clear/repeat reward, and availability. Formation uses a 6×4 grid: player rows are 2–3 and enemy rows are 0–1. Clicking an available unit then a valid cell creates the draft; clicking an occupied player cell removes it. Equipment controls reuse the unit equipment commands before confirmation. Confirm dispatches the single `START_MISSION` command with the complete formation. On success, read only the returned `runId` and start `CombatScene`; `GameSession` has already generated and stored the seed, canonical input, and canonical report. UI never calls `simulateCombat` and never passes a report into a command.

- [ ] **Step 3: Implement deterministic playback**

`CombatScene` resolves `runId` against the active mission snapshot, reads its stored report frames sequentially, interpolates curated unit animations between recorded positions, displays health bars, provides speed buttons 1×/2×/4×, and never recalculates damage. On the final frame it dispatches `{ type: 'RESOLVE_MISSION', runId }` exactly once, shows victory/defeat/draw, casualties, and either the stored victory reward or an explicit “Без награды”, then returns to `ColonyScene`. Leaving after `START_MISSION` dispatches `ABORT_MISSION`, which immediately settles the already-calculated outcome and cannot cancel casualties or rewards. On reload with an active run, the continue flow reopens `CombatScene` for the same run and canonical report instead of creating a second run.

- [ ] **Step 4: Verify mission flow**

Run: `npm test -- tests/unit/formation.test.ts tests/unit/combat.test.ts tests/unit/mission-lifecycle.test.ts`  
Expected: PASS.  
Run: `npm run lint && npm run build`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/game/scenes/CombatScene.ts src/ui src/game/config.ts tests/unit/formation.test.ts
git commit -m "feat: add mission formation and combat playback"
```

## Task 12: Verify the full vertical slice in a browser

**Files:**
- Create: `playwright.config.ts`
- Create: `tests/e2e/vertical-slice.spec.ts`
- Modify: `src/ui/AppUi.ts`
- Modify: `README.md`

**Interfaces:**
- Consumes: user-visible labels and the real browser storage adapter
- Produces: one browser test proving the approved end-to-end loop

- [ ] **Step 1: Write the failing browser test**

```ts
import { expect, test } from '@playwright/test';

test('player can run the complete colony-to-combat loop', async ({ page }) => {
  await page.goto('/?testClock=fast&seed=12345');
  await page.getByRole('button', { name: 'Нанять тролля' }).click();
  for (let i = 0; i < 4; i += 1) {
    await page.getByRole('button', { name: 'Нанять гоблина' }).click();
  }
  await page.getByRole('button', { name: 'Выбрать первых 4 свободных' }).click();
  await page.getByRole('button', { name: 'Назначить в шахту' }).click();
  await page.getByRole('button', { name: 'Выбрать всех свободных' }).click();
  await page.getByRole('button', { name: 'Назначить маршрут: шахта → склад' }).click();
  await expect(page.getByText(/Склад — железная руда: [1-9]/)).toBeVisible();
  await page.getByRole('button', { name: 'Продать руду' }).click();
  await page.getByRole('button', { name: 'Выбрать работников шахты' }).click();
  await page.getByRole('button', { name: 'Освободить от работы' }).click();
  await page.getByRole('button', { name: 'Экипировать ржавый меч' }).first().click();
  await page.getByRole('button', { name: 'Миссия 1' }).click();
  const candidates = page.getByRole('button', { name: /Добавить .+ в построение/ });
  for (let slot = 1; slot <= 4; slot += 1) {
    await candidates.first().click();
    await page.getByRole('button', { name: `Клетка игрока ${slot}` }).click();
  }
  await page.getByRole('button', { name: 'Подтвердить построение' }).click();
  await expect(page.getByText(/Победа|Поражение|Ничья/)).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Продолжить игру' })).toBeVisible();
});
```

The test must prove five distinct recruited units exist, exactly one is a hauler, ore reaches the warehouse before sale, four former miners enter four unique formation cells, at least one item is equipped, and the mission settles from the canonical stored report. The query flag may accelerate only waiting by feeding the normal coordinator positive 250 ms fixed steps and may advance the normal mission cooldown; it must not change production rates, combat, rewards, save rules, or bypass commands.

Run: `npm run e2e -- tests/e2e/vertical-slice.spec.ts`  
Expected: FAIL until the remaining UI labels and test clock adapter are connected.

- [ ] **Step 2: Complete the test clock and new/continue flow**

Add a clock adapter at the composition root with production implementation using real deltas and test implementation advancing approved fixed steps without delays. Add new-game and continue buttons, confirmation before overwriting an existing save, reset focus to the first heading after screen transitions, and document controls and commands in `README.md`.

- [ ] **Step 3: Run all quality gates**

Run: `npm run lint`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS with no TypeScript errors.  
Run: `npx playwright install chromium` then `npm run e2e`  
Expected: PASS in Chromium.

- [ ] **Step 4: Manually verify the approved visual behavior**

Run: `npm run dev -- --host 127.0.0.1` and inspect at 1280×720, 1920×1080, and 390×844. Confirm crisp sprites, readable overlay, keyboard focus, selection, mining, hauling, selling, formation, 1×/2×/4× playback, casualties, rewards, and reload persistence. Record any defect as a failing test before correcting it.

- [ ] **Step 5: Commit**

```bash
git add playwright.config.ts tests/e2e README.md src/ui/AppUi.ts
git commit -m "test: verify complete vertical slice"
```

## Final acceptance

Run all four gates from a clean checkout:

```bash
npm ci
npm run lint
npm test
npm run build
npm run e2e
```

Acceptance requires the complete loop to work without developer-console commands, Phaser imports to remain absent from domain/application/persistence, repeated mission settlement to be rejected, deterministic combat reports to remain stable, and a saved game—including an in-progress active mission—to resume with the same units, haul phase/progress, colony inventory, equipment ownership, formation, combat seed/input/report, mission progression, and applied-run IDs. Only `deadPlayerUnitIds` may remove colony units; defeat and draw award nothing. The production bundle contains only assets named in the curated licensed manifest.
