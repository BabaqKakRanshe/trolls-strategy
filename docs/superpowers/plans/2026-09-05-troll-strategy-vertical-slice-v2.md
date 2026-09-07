# Troll Strategy Vertical Slice v2 Implementation Plan

**Status:** Deferred. The current playable scope is the economy-only slice in `docs/superpowers/plans/2026-09-05-colony-economy-slice.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать играбельный web-срез, в котором игрок строит шахту, нанимает и назначает юнитов, перевозит и продаёт руду, затем выставляет до четырёх тех же юнитов на гексагональный автобой с постоянными потерями.

**Architecture:** Чистое детерминированное доменное ядро владеет правилами экономики, логистики, сетки и боя; `GameSession` является единственным владельцем изменяемого состояния и принимает атомарные команды. Phaser и DOM получают immutable snapshots, а сохранение и wall-clock изолированы адаптерами на границе приложения.

**Tech Stack:** TypeScript 6, Phaser 4.2.1, Vite 8, Vitest 5, ESLint 10, Playwright, browser localStorage.

**Spec:** `docs/superpowers/specs/2026-09-05-troll-strategy-vertical-slice-v2.md`

## Global Constraints

- Выполнять план только после подтверждения, что текущие удаления tracked-файлов в `F:\ClaudeGames\trollstrategy` являются намеренным reset и их можно заменить новой реализацией.
- Не восстанавливать рабочее дерево через `git checkout`, `git restore` или `git reset`; создавать новые версии файлов обычными патчами.
- Сохранять пользовательские каталоги `assets/sprites/MegaPackFree`, `assets/sprites/Sample pack`, `media` и все несвязанные изменения.
- `GameSession` — единственный владелец изменяемого состояния кампании.
- Domain не импортирует Phaser, DOM, localStorage, `Date`, `performance` или wall-clock API.
- Экономика шагает по 250 ms, бой — по 100 ms; ни один результат не зависит от render FPS.
- Runtime использует только manifest-approved ассеты; геометрия является безопасным fallback.
- Canvas-взаимодействия имеют доступные DOM-эквиваленты.
- Перед каждым коммитом запускать узкие тесты задачи; перед завершением — lint, все unit tests, production build и browser E2E.

## Impact Analysis and Locked File Structure

Текущий HEAD содержит прежний прототип с похожими границами, но рабочая копия удаляет весь runtime и тесты. Новый срез сохраняет удачное направление зависимостей, но меняет модель боя на гексагональную, разрешает несколько шахт, делает маршруты общими и исправляет ошибочное сопоставление `unit.goblin.*` со спрайтами тролля.

```text
src/
├─ content/
│  ├─ catalog.ts                 # единственные числовые параметры юнитов/зданий/предметов
│  └─ missionCatalog.ts          # враги, поле, cooldown и награды миссии
├─ domain/
│  ├─ model.ts                   # сериализуемые типы и branded IDs
│  ├─ results.ts                 # единый CommandResult
│  ├─ map/grid.ts                # footprints и проверка размещения
│  ├─ colony/commands.ts         # найм, строительство, работа, экипировка
│  ├─ colony/tickColony.ts       # fixed-step добыча
│  ├─ logistics/tickHaulers.ts   # FSM маршрутов и атомарный груз
│  ├─ combat/hexGrid.ts          # odd-r/axial conversion, distance, neighbors
│  ├─ combat/simulateCombat.ts   # чистый seeded combat report
│  └─ progression/missions.ts    # start/settle/idempotency/rewards
├─ application/
│  ├─ GameSession.ts             # owner state, dispatch, advance, snapshots
│  ├─ MapInteractionController.ts# только transient selection/target modes
│  └─ snapshot.ts                # readonly presentation DTO
├─ persistence/
│  ├─ saveCodec.ts               # полная schema validation
│  ├─ savePort.ts                # порт и browser adapter
│  └─ autosaveCoordinator.ts     # command/foreground save policy
├─ game/
│  ├─ config.ts
│  ├─ assets/manifest.ts
│  ├─ scenes/BootScene.ts
│  ├─ scenes/ColonyScene.ts
│  └─ scenes/CombatScene.ts
├─ ui/
│  ├─ AppUi.ts
│  ├─ renderHud.ts
│  ├─ renderShop.ts
│  ├─ renderContextMenu.ts
│  ├─ renderEquipmentPanel.ts
│  └─ renderMissionPanel.ts
└─ main.ts                       # composition root only
```

## Task 1: Recreate the toolchain and guarded runtime asset pipeline

**Files:**
- Create: `package.json`
- Create: `package-lock.json` via `npm install`
- Create: `tsconfig.json`
- Create: `eslint.config.js`
- Create: `vite.config.ts`
- Create: `index.html`
- Create: `scripts/copy-curated-assets.py`
- Modify: `assets/curated-assets.json`
- Modify: `assets/licenses.json`
- Create: `src/game/assets/manifest.ts`
- Test: `tests/unit/source-sprite-manifest.test.ts`

**Interfaces:**
- Produces: `npm run dev`, `npm run build`, `npm test`, `npm run lint`, `npm run e2e`, `npm run assets:prepare`.
- Produces: `loadRuntimeManifest(fetcher: typeof fetch): Promise<RuntimeAssetManifest>`.
- Produces: runtime keys `building.mine`, `building.warehouse`, `building.market`, `unit.goblin.{idle,walk,attack,damage,death}`, `unit.troll.{idle,walk,attack,damage,death}`.

- [ ] **Step 1: Add the manifest contract test**

```ts
import { describe, expect, it } from 'vitest';
import { readFileSync, statSync } from 'node:fs';
import { resolve, sep } from 'node:path';

it('maps goblin and troll keys to their own sprite families', () => {
  const manifest = JSON.parse(readFileSync('assets/curated-assets.json', 'utf8'));
  const byKey = new Map(manifest.assets.map((entry: { key: string; source: string }) => [entry.key, entry]));
  expect(byKey.get('unit.goblin.idle')?.source).toContain('/Goblin/GoblinIdle.png');
  expect(byKey.get('unit.troll.idle')?.source).toContain('/Troll/TrollIdle.png');
});

it('keeps every source below assets and every output below public/assets/runtime', () => {
  const root = resolve('.');
  const sourceRoot = resolve('assets') + sep;
  const outputRoot = resolve('public/assets/runtime') + sep;
  const manifest = JSON.parse(readFileSync('assets/curated-assets.json', 'utf8'));
  for (const entry of manifest.assets) {
    const source = resolve(root, entry.source);
    const output = resolve(root, entry.output);
    expect(source.startsWith(sourceRoot)).toBe(true);
    expect(output.startsWith(outputRoot)).toBe(true);
    expect(statSync(source).isFile()).toBe(true);
  }
});
```

- [ ] **Step 2: Run the manifest test and verify the known mismatch**

Run: `npm test -- tests/unit/source-sprite-manifest.test.ts`  
Expected: FAIL because `unit.goblin.idle` currently points at `Monsters/Troll/TrollIdle.png`.

- [ ] **Step 3: Recreate configuration and exact scripts**

Create `package.json` with Node `^20.19.0 || ^22.13.0 || >=24`, Phaser `4.2.1`, and scripts:

```json
{
  "scripts": {
    "assets:prepare": "python scripts/copy-curated-assets.py",
    "dev": "npm run assets:prepare && vite",
    "build": "npm run assets:prepare && tsc --noEmit && vite build",
    "test": "vitest run",
    "lint": "eslint .",
    "e2e": "playwright test"
  }
}
```

Use strict TypeScript with `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, DOM/ES2023 libs, and `moduleResolution: Bundler`. Vite base stays relative (`base: './'`) so the build runs from static hosting.

- [ ] **Step 4: Correct and validate the curated manifest**

Map goblin keys to `Base_Humanoids/Goblin/GoblinIdle.png`, `GoblinWalk.png`, `GoblinAttack.png`, `GoblinDmg.png`, and `GoblinSpinDie.png`. Map troll keys to `Monsters/Troll/TrollIdle.png`, `TrollWalk.png`, `TrollAttack.png`, `TrollDmg.png`, and `TrollDie.png`. Retain the three user-provided building sprites. For every spritesheet record the verified `32×32` frame size and animation range from its adjacent `_AnimationInfo.txt`.

Implement `copy-curated-assets.py` with `Path.resolve()`, `Path.is_relative_to()`, per-file `shutil.copy2`, temporary manifest replacement, and deletion limited to stale outputs named in the previous generated manifest. It must reject a missing license, duplicate key/output, directory source, or path escape before copying any file.

- [ ] **Step 5: Verify the asset boundary**

Run: `npm run assets:prepare`  
Expected: PASS; generated files exist only below `public/assets/runtime`.  
Run: `npm test -- tests/unit/source-sprite-manifest.test.ts`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS with an empty composition root allowed at this stage.

- [ ] **Step 6: Commit**

```bash
git add package.json package-lock.json tsconfig.json eslint.config.js vite.config.ts index.html scripts assets/curated-assets.json assets/licenses.json src/game/assets tests/unit/source-sprite-manifest.test.ts
git commit -m "chore: bootstrap troll strategy vertical slice"
```

## Task 2: Define canonical content, state, IDs, and placement rules

**Files:**
- Create: `src/content/catalog.ts`
- Create: `src/content/missionCatalog.ts`
- Create: `src/domain/model.ts`
- Create: `src/domain/results.ts`
- Create: `src/domain/map/grid.ts`
- Create: `src/domain/initialState.ts`
- Test: `tests/unit/initial-state.test.ts`
- Test: `tests/unit/grid.test.ts`

**Interfaces:**
- Produces: branded `UnitId`, `BuildingId`, `ItemId`, `MissionRunId`, `CombatantId` constructors.
- Produces: `UNIT_CATALOG`, `BUILDING_CATALOG`, `ITEM_CATALOG`, `MISSION_CATALOG` matching the spec.
- Produces: `createInitialState(campaignSeed: number): GameState`.
- Produces: `validatePlacement(state, kind, anchor): CommandResult<readonly GridCell[]>`.

- [ ] **Step 1: Write failing initial-state and footprint tests**

```ts
it('starts with the three fixed buildings, starter equipment, and no units', () => {
  const state = createInitialState(123);
  expect(state.wallet.gold).toBe(1000);
  expect(state.units).toEqual([]);
  expect(state.buildings.map((building) => building.kind)).toEqual(['town-hall', 'warehouse', 'market']);
  expect(state.colonyInventory.itemIds).toHaveLength(2);
  expect(state.mission.nextMissionAtMs).toBe(120_000);
});

it('rejects a mine footprint that overlaps the town hall', () => {
  const state = createInitialState(123);
  const before = structuredClone(state);
  expect(validatePlacement(state, 'mine', { x: 8, y: 5 }).ok).toBe(false);
  expect(state).toEqual(before);
});
```

- [ ] **Step 2: Run tests to verify missing domain files**

Run: `npm test -- tests/unit/initial-state.test.ts tests/unit/grid.test.ts`  
Expected: FAIL with unresolved imports.

- [ ] **Step 3: Implement the serializable model and catalogs**

Use discriminated assignments:

```ts
export type Assignment =
  | { kind: 'idle' }
  | { kind: 'work'; buildingId: BuildingId }
  | { kind: 'haul'; route: HaulRoute; phase: HaulPhase; carried: number; progressMs: number }
  | { kind: 'mission'; runId: MissionRunId }
  | { kind: 'dead' };
```

`GameState` stores schema version 2, campaign seed, simulation time, wallet, buildings, units, items, colony item IDs, mission state, unlocks, and integer ID sequences. Catalog values must be copied exactly from the spec rather than repeated in commands or UI.

- [ ] **Step 4: Implement one canonical footprint validator**

`cellsForFootprint(anchor, footprint)` expands a top-left anchor in row-major order. `validatePlacement` rejects negative/out-of-grid coordinates and intersection with any existing building footprint, returning `{ ok: true, value: cells }` only when every cell is valid. Initial placements are town hall `(8,5)`, warehouse `(14,8)`, and market `(14,2)`.

- [ ] **Step 5: Verify and commit**

Run: `npm test -- tests/unit/initial-state.test.ts tests/unit/grid.test.ts`  
Expected: PASS.  

```bash
git add src/content src/domain tests/unit/initial-state.test.ts tests/unit/grid.test.ts
git commit -m "feat: define troll strategy domain model"
```

## Task 3: Implement atomic construction, recruitment, work, and equipment commands

**Files:**
- Create: `src/domain/colony/commands.ts`
- Test: `tests/unit/colony-commands.test.ts`

**Interfaces:**
- Produces: `buildMine(state, anchor): CommandResult<GameState>`.
- Produces: `recruitUnit(state, species, spawnCell): CommandResult<GameState>`.
- Produces: `assignWorkers(state, unitIds, buildingId): CommandResult<GameState>`.
- Produces: `releaseUnits(state, unitIds): CommandResult<GameState>`.
- Produces: `equipItem(state, unitId, itemId): CommandResult<GameState>` and `unequipItem(state, unitId, slot): CommandResult<GameState>`.
- Invariant: failure returns the original state reference; success returns a detached state with no partial mutation.

- [ ] **Step 1: Write failing transactional tests**

```ts
it('builds multiple non-overlapping mines and charges once per mine', () => {
  const first = expectOk(buildMine(createInitialState(1), { x: 1, y: 1 }));
  const second = expectOk(buildMine(first, { x: 4, y: 1 }));
  expect(second.wallet.gold).toBe(600);
  expect(second.buildings.filter((b) => b.kind === 'mine')).toHaveLength(2);
});

it('does not partially assign a group when the mine would exceed five workers', () => {
  const state = stateWithMineAndIdleGoblins(6);
  const result = assignWorkers(state, state.units.map((unit) => unit.id), mineId(state));
  expect(result).toEqual({ ok: false, error: { code: 'WORKER_CAPACITY_EXCEEDED' } });
  expect(state.units.every((unit) => unit.assignment.kind === 'idle')).toBe(true);
});
```

Add cases for insufficient gold, occupied spawn, duplicate unit IDs, dead/mission units, incompatible building, double-owned equipment, wrong slot, and unequip returning the item exactly once.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `npm test -- tests/unit/colony-commands.test.ts`  
Expected: FAIL because the command functions are missing.

- [ ] **Step 3: Implement validation-before-copy command handlers**

Every handler first resolves all IDs and checks the complete request. Only then create updated arrays. Generate IDs from serializable monotonic sequences (`unit-1`, `building-4`, `item-1`) so reload and deterministic tests retain identity. Recruitment uses the species catalog and starts the unit in `idle` with empty equipment.

- [ ] **Step 4: Verify commands and commit**

Run: `npm test -- tests/unit/colony-commands.test.ts`  
Expected: PASS.  

```bash
git add src/domain/colony tests/unit/colony-commands.test.ts
git commit -m "feat: add transactional colony commands"
```

## Task 4: Add fixed-step mining and visible hauling routes

**Files:**
- Create: `src/domain/colony/tickColony.ts`
- Create: `src/domain/logistics/routes.ts`
- Create: `src/domain/logistics/tickHaulers.ts`
- Modify: `src/domain/colony/commands.ts`
- Test: `tests/unit/colony-tick.test.ts`
- Test: `tests/unit/logistics.test.ts`

**Interfaces:**
- Produces: `assignHaulRoute(state, unitIds, route): CommandResult<GameState>`.
- Produces: `tickColony(state, stepMs: 250): GameState`.
- Produces: `advanceColony(state, elapsedMs): { state: GameState; remainderMs: number }`.
- Route supports exactly `mine→warehouse`, `mine→market`, and `warehouse→market` for `ironOre`.

- [ ] **Step 1: Write failing mining and route-FSM tests**

```ts
it('produces the same ore regardless of render delta partitioning', () => {
  const state = workingMineFixture([3, 3, 3]);
  const a = advanceInDeltas(state, [16, 16, 218, 250, 500]);
  const b = advanceInDeltas(state, [1000]);
  expect(a).toEqual(b);
  expect(mine(a).inventory.ironOre).toBeCloseTo(0.9, 6);
});

it('moves ore mine to warehouse to market and credits gold only on market unload', () => {
  const state = completeRouteFixture();
  const advanced = advanceSeconds(state, 30);
  expect(warehouse(advanced).inventory.ironOre).toBeGreaterThanOrEqual(0);
  expect(advanced.wallet.gold).toBeGreaterThan(state.wallet.gold);
  expect(totalOreAndSoldValue(advanced)).toBeCloseTo(totalOreAndSoldValue(state), 6);
});
```

Add tests for source empty, destination full, waiting without route loss, multi-hauler conservation, speed-dependent travel, capacity limits, and route cancellation returning carried ore to the source.

- [ ] **Step 2: Verify the tests fail**

Run: `npm test -- tests/unit/colony-tick.test.ts tests/unit/logistics.test.ts`  
Expected: FAIL with missing tick and route modules.

- [ ] **Step 3: Implement fixed steps and the haul FSM**

Accumulate render elapsed time outside the domain and invoke only 250 ms ticks. Mining adds `strengthSum × 0.1 × 0.25`, clamped by mine capacity. Travel duration is `distancePixels / (120 + speed × 12) × 1000`; loading and unloading each consume 500 ms. A tick may cross multiple phases, so consume remaining step time in a bounded loop of at most four transitions and carry unused phase time forward.

Market unload performs `wallet.gold += carried × 3` and sets carried to zero. All other unloads transfer the exact carried amount to destination inventory. Floating-point comparisons in conservation tests use `1e-6` tolerance.

- [ ] **Step 4: Verify, profile the fixed loop, and commit**

Run: `npm test -- tests/unit/colony-tick.test.ts tests/unit/logistics.test.ts`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS with no per-tick timers or browser dependencies.

```bash
git add src/domain/colony src/domain/logistics tests/unit/colony-tick.test.ts tests/unit/logistics.test.ts
git commit -m "feat: simulate mining and hauling"
```

## Task 5: Implement deterministic hex combat and mission settlement

**Files:**
- Create: `src/domain/combat/hexGrid.ts`
- Create: `src/domain/combat/rng.ts`
- Create: `src/domain/combat/simulateCombat.ts`
- Create: `src/domain/progression/missions.ts`
- Test: `tests/unit/hex-grid.test.ts`
- Test: `tests/unit/combat.test.ts`
- Test: `tests/unit/mission-lifecycle.test.ts`

**Interfaces:**
- Produces: `offsetToAxial(cell)`, `hexDistance(a, b)`, `neighbors(cell)` for a `9×5` odd-r grid.
- Produces: `simulateCombat(input: CombatInput): CombatReport`.
- Produces: `startMission(state, missionId, formation): CommandResult<{ state: GameState; runId: MissionRunId }>`.
- Produces: `settleMission(state, runId): CommandResult<GameState>`.

- [ ] **Step 1: Write failing hex and repeatability tests**

```ts
it('computes symmetric odd-r hex distance', () => {
  expect(hexDistance({ x: 0, y: 0 }, { x: 2, y: 1 })).toBe(2);
  expect(hexDistance({ x: 2, y: 1 }, { x: 0, y: 0 })).toBe(2);
});

it('returns byte-identical reports for the same input and seed', () => {
  const input = missionOneCombatFixture(777);
  expect(JSON.stringify(simulateCombat(input))).toBe(JSON.stringify(simulateCombat(input)));
});
```

Add cases for unique cells, 1–4 living player units, deployment zones, stable tie-breaking, armor floor of 1 damage, ranged goblins, melee trolls, simultaneous death, 90-second draw, and no mutation of input.

- [ ] **Step 2: Write failing lifecycle tests**

```ts
it('removes dead units and their equipment and cannot settle a run twice', () => {
  const started = expectStarted(startMission(losingFormationState(), 'mission-1', losingFormation()));
  const settled = expectOk(settleMission(started.state, started.runId));
  expect(settled.units.some((unit) => unit.id === doomedUnitId())).toBe(false);
  expect(settled.items.some((item) => item.id === doomedWeaponId())).toBe(false);
  expect(settleMission(settled, started.runId)).toEqual({ ok: false, error: { code: 'RUN_ALREADY_SETTLED' } });
});
```

Add first-win 250 gold, repeat-win 75 gold, no reward for defeat/draw, full-health survivors, and cooldown anchored at start time.

- [ ] **Step 3: Run focused tests and confirm failure**

Run: `npm test -- tests/unit/hex-grid.test.ts tests/unit/combat.test.ts tests/unit/mission-lifecycle.test.ts`  
Expected: FAIL with missing combat and mission modules.

- [ ] **Step 4: Implement pure combat in three phases per 100 ms tick**

For each tick: snapshot living combatants; collect ready attack/move intents in stable combatant-ID order; apply moves only to unoccupied cells with lower-ID priority; aggregate damage and apply it simultaneously. Target order is distance, current HP, stable ID. Combat output records frames every 200 ms plus the exact final frame. RNG is an explicit serializable xorshift32 stream even if mission 1 uses no random damage.

`startMission` validates availability and formation before changing assignments. It derives the run ID and seed from campaign seed and run sequence, stores the canonical input and complete report, and advances `nextMissionAtMs` by 120 seconds. `settleMission` reads only the stored report, removes casualties/equipment, returns survivors to idle, applies reward once, records `runId`, and clears the active run.

- [ ] **Step 5: Verify and commit**

Run: `npm test -- tests/unit/hex-grid.test.ts tests/unit/combat.test.ts tests/unit/mission-lifecycle.test.ts`  
Expected: PASS.  

```bash
git add src/domain/combat src/domain/progression tests/unit/hex-grid.test.ts tests/unit/combat.test.ts tests/unit/mission-lifecycle.test.ts
git commit -m "feat: add deterministic hex autobattles"
```

## Task 6: Add GameSession, immutable snapshots, versioned saves, and autosave

**Files:**
- Create: `src/application/GameSession.ts`
- Create: `src/application/snapshot.ts`
- Create: `src/persistence/saveCodec.ts`
- Create: `src/persistence/savePort.ts`
- Create: `src/persistence/autosaveCoordinator.ts`
- Test: `tests/unit/game-session.test.ts`
- Test: `tests/unit/save-codec.test.ts`
- Test: `tests/unit/autosave-coordinator.test.ts`

**Interfaces:**
- Produces: `GameCommandContractMap` and `GameSession.dispatch<K>(command: GameCommand<K>): CommandResult<CommandValue<K>>`.
- Produces: `GameSession.advance(realDeltaMs: number): GameSnapshot` and `GameSession.snapshot(): GameSnapshot`.
- Produces: `encodeSave(state): string`, `decodeSave(raw): CommandResult<GameState>`.
- Produces: `SavePort = { load(): string | null; save(raw: string): void; clear(): void }`.

- [ ] **Step 1: Write command routing and immutability tests**

```ts
it('exposes detached snapshots and routes all mutations through dispatch', () => {
  const session = GameSession.newGame(42);
  const before = session.snapshot();
  expect(session.dispatch({ type: 'RECRUIT_UNIT', species: 'goblin', spawnCell: { x: 2, y: 10 } }).ok).toBe(true);
  expect(before.units).toHaveLength(0);
  expect(session.snapshot().units).toHaveLength(1);
});
```

Test every command type, rejected-command revision stability, 250 ms remainder behavior, and hidden-tab deltas being ignored by the composition clock rather than the domain.

- [ ] **Step 2: Write codec and autosave tests**

Round-trip a state containing two mines, active workers, a hauler in each phase, carried ore, equipped items, and an active mission report. Reject malformed JSON, unknown schema, duplicate IDs, dangling building/route references, invalid formation, forged report identity, dead owned units, double-owned items, and applied active run IDs. Autosave immediately after each successful command and once per 10 seconds of foreground simulation; rejected commands do not save.

- [ ] **Step 3: Run focused tests and confirm failure**

Run: `npm test -- tests/unit/game-session.test.ts tests/unit/save-codec.test.ts tests/unit/autosave-coordinator.test.ts`  
Expected: FAIL with unresolved application and persistence imports.

- [ ] **Step 4: Implement the single command boundary**

Command union includes `BUILD_MINE`, `RECRUIT_UNIT`, `ASSIGN_WORK`, `ASSIGN_HAUL`, `RELEASE_UNITS`, `EQUIP_ITEM`, `UNEQUIP_ITEM`, `START_MISSION`, and `SETTLE_MISSION`. `GameSession` keeps state private, increments revision only after success or an effective fixed step, and returns readonly view models with copied arrays.

The schema-2 codec validates the entire object before construction. It may clamp finite negative gold/ore to zero, but it rejects structural and ownership corruption. The browser adapter alone reads/writes localStorage key `troll-strategy.save.v2`.

- [ ] **Step 5: Verify and commit**

Run: `npm test -- tests/unit/game-session.test.ts tests/unit/save-codec.test.ts tests/unit/autosave-coordinator.test.ts`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS.

```bash
git add src/application src/persistence tests/unit/game-session.test.ts tests/unit/save-codec.test.ts tests/unit/autosave-coordinator.test.ts
git commit -m "feat: coordinate and persist game sessions"
```

## Task 7: Render the colony, grid, buildings, units, and route state in Phaser

**Files:**
- Create: `src/game/config.ts`
- Create: `src/game/scenes/BootScene.ts`
- Create: `src/game/scenes/ColonyScene.ts`
- Create: `src/game/rendering/createFallbackTexture.ts`
- Create: `src/game/rendering/unitAnimation.ts`
- Create: `src/main.ts`
- Create: `src/styles.css`
- Test: `tests/unit/scene-boundaries.test.ts`

**Interfaces:**
- Consumes: `RuntimeFacade = { snapshot(): GameSnapshot; advance(deltaMs): GameSnapshot; dispatch(command): CommandResult<unknown> }`.
- Produces: Phaser scenes that render snapshots but never import domain command handlers or persistence adapters.

- [ ] **Step 1: Write a boundary test before scene code**

```ts
it('keeps presentation from importing domain mutation or persistence modules', () => {
  const sources = readSceneSources();
  expect(sources).not.toMatch(/from ['"].*domain\/colony/);
  expect(sources).not.toMatch(/from ['"].*persistence/);
  expect(sources).not.toMatch(/localStorage|Date\.now|performance\.now/);
});
```

- [ ] **Step 2: Run the boundary test and confirm scene files are absent**

Run: `npm test -- tests/unit/scene-boundaries.test.ts`  
Expected: FAIL because scene sources do not exist.

- [ ] **Step 3: Implement boot and fallback assets**

`BootScene` fetches the generated runtime manifest, preloads declared images/sheets, creates animations from manifest metadata, and records non-blocking warnings for failures. `createFallbackTexture` generates stable colored 32×32 unit diamonds and footprint-sized building blocks for missing optional art.

- [ ] **Step 4: Implement snapshot-driven colony rendering**

Render a dark forest border and light buildable ground, buildings at cell anchors, units by their map cells or interpolated route progress, selection circles, building capacity bars, and route arrows. Grid graphics are created once and toggled only in placement/target modes. Pool unit sprites by `UnitId`; do not instantiate/destroy sprites on every snapshot. `update(_, delta)` advances the facade only while `document.visibilityState === 'visible'` and redraws changed entities when snapshot revision changes.

- [ ] **Step 5: Compose the app and verify**

`main.ts` creates browser save port, loads or creates a session, wraps it in autosave coordinator, constructs UI, then creates Phaser with the narrow facade. It contains no gameplay values.

Run: `npm test -- tests/unit/scene-boundaries.test.ts`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/game src/main.ts src/styles.css tests/unit/scene-boundaries.test.ts
git commit -m "feat: render the troll settlement"
```

## Task 8: Implement selection, placement, fan commands, shop, and tooltips

**Files:**
- Create: `src/application/MapInteractionController.ts`
- Create: `src/ui/AppUi.ts`
- Create: `src/ui/renderHud.ts`
- Create: `src/ui/renderShop.ts`
- Create: `src/ui/renderContextMenu.ts`
- Modify: `src/game/scenes/ColonyScene.ts`
- Modify: `src/styles.css`
- Test: `tests/unit/map-interactions.test.ts`
- Test: `tests/unit/shop-ui.test.ts`

**Interfaces:**
- Produces: transient modes `neutral`, `placingBuilding`, `choosingWorkTarget`, `choosingHaulSource`, `choosingHaulDestination`.
- Produces: `SelectionModel.click`, `toggle`, `selectRect`, `clear`, and ordered `ids()`.
- UI dispatches typed commands through `RuntimeFacade` only.

- [ ] **Step 1: Write failing selection and target-mode tests**

```ts
it('selects by drag rectangle and completes a two-building route target', () => {
  const controller = controllerFixture();
  controller.selectRect({ left: 0, top: 0, right: 200, bottom: 200 });
  controller.beginHaulTarget();
  expect(controller.chooseBuilding(mineId())).toEqual({ kind: 'awaitingDestination' });
  expect(controller.chooseBuilding(warehouseId())).toEqual({ kind: 'command', command: expectedHaulCommand() });
});
```

Test plain click replacement, Ctrl toggle, empty-ground cancel, invalid target preserving the mode, and selected unit deletion clearing stale IDs.

- [ ] **Step 2: Write failing shop rendering tests**

Render into JSDOM and assert that insufficient-gold buttons are disabled, hover/focus exposes the same stat card, mine placement does not spend gold until a valid cell is confirmed, and building tooltip values come only from the snapshot.

- [ ] **Step 3: Run focused tests and confirm failure**

Run: `npm test -- tests/unit/map-interactions.test.ts tests/unit/shop-ui.test.ts`  
Expected: FAIL with missing controller and UI modules.

- [ ] **Step 4: Implement input projection and accessible command UI**

Project pointer pixels to grid cells once in `ColonyScene`; pass IDs/cells to the controller. The fan menu follows the selected-group centroid and contains native buttons `Работать`, `Переносить`, `Освободить`, `В отряд`. The right shop has separate `Постройки` and `Юниты` tabs. The left HUD displays gold, total/free/working/hauling/mission units, and total ore by location.

Tooltips use one renderer for shop and world hover/focus. Mine tooltip displays `Шахта I`, stored ore, workers `n/5`, and current ore/second. Route preview marks source gold, destination blue, and draws a directional dotted line.

- [ ] **Step 5: Verify interaction and commit**

Run: `npm test -- tests/unit/map-interactions.test.ts tests/unit/shop-ui.test.ts`  
Expected: PASS.  
Run: `npm run lint`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS.

```bash
git add src/application/MapInteractionController.ts src/ui src/game/scenes/ColonyScene.ts src/styles.css tests/unit/map-interactions.test.ts tests/unit/shop-ui.test.ts
git commit -m "feat: add settlement controls and shop"
```

## Task 9: Build formation UI and canonical combat playback

**Files:**
- Create: `src/ui/renderMissionPanel.ts`
- Create: `src/ui/renderEquipmentPanel.ts`
- Create: `src/application/FormationDraft.ts`
- Create: `src/game/scenes/CombatScene.ts`
- Modify: `src/game/config.ts`
- Modify: `src/ui/AppUi.ts`
- Modify: `src/styles.css`
- Test: `tests/unit/formation-draft.test.ts`
- Test: `tests/unit/combat-playback.test.ts`
- Test: `tests/unit/equipment-ui.test.ts`

**Interfaces:**
- Produces: `FormationDraft.place(unitId, cell)`, `remove(cell)`, `toFormation()` without campaign mutation.
- Produces: accessible equipment controls that dispatch `EQUIP_ITEM` and `UNEQUIP_ITEM` through `RuntimeFacade` and render ownership only from snapshots.
- Combat scene receives only `runId` and reads stored `CombatReport` from snapshots.
- Playback dispatches `SETTLE_MISSION` exactly once after the final recorded frame.

- [ ] **Step 1: Write failing draft, equipment, and playback tests**

```ts
it('keeps a four-unit unique-cell formation without changing assignments', () => {
  const session = preparedSession();
  const before = session.snapshot();
  const draft = new FormationDraft(before.units);
  draft.place(before.units[0]!.id, { x: 0, y: 1 });
  expect(session.snapshot()).toEqual(before);
  expect(draft.toFormation().unitIds).toEqual([before.units[0]!.id]);
});

it('settles only after the stored final frame and never simulates in the scene', () => {
  const playback = playbackFixture(storedReport());
  playback.seekToEnd();
  playback.update(100);
  playback.update(100);
  expect(playback.dispatchedCommands()).toEqual([{ type: 'SETTLE_MISSION', runId: storedRunId() }]);
});

it('equips starter items through visible commands and snapshot ownership', () => {
  const fixture = equipmentPanelFixture();
  fixture.click('Выбрать бойца 1');
  fixture.click('Надеть Ржавый меч');
  fixture.click('Надеть Латаную броню');
  expect(fixture.dispatchedCommands()).toEqual([
    { type: 'EQUIP_ITEM', unitId: fixture.unitId, itemId: fixture.weaponId },
    { type: 'EQUIP_ITEM', unitId: fixture.unitId, itemId: fixture.armorId },
  ]);
  fixture.applyCommandsAndRender();
  expect(fixture.text()).toContain('Оружие: Ржавый меч');
  expect(fixture.text()).toContain('Броня: Латаная броня');
});
```

- [ ] **Step 2: Run tests and confirm failure**

Run: `npm test -- tests/unit/formation-draft.test.ts tests/unit/combat-playback.test.ts tests/unit/equipment-ui.test.ts`  
Expected: FAIL with missing formation, equipment panel, and playback modules.

- [ ] **Step 3: Implement the mission and formation panel**

Show cooldown, four enemy cards, first/repeat rewards, living owned units, settlement equipment, and 9×5 keyboard-navigable hex buttons. Selecting a unit exposes weapon and armor slots plus compatible inventory items; equip/unequip buttons dispatch the canonical commands, preserve focus, and immediately reflect the new single owner from the next snapshot. Only columns 0–1 accept player placement. Clicking a unit then a free deployment cell places it; clicking an occupied cell removes it. Confirm dispatches one `START_MISSION` command with the complete formation and starts `CombatScene` using returned `runId`.

- [ ] **Step 4: Implement report-only playback**

Interpolate positions and HP between recorded frames, choose attack/damage/death animations from frame transitions, and expose `1×`, `2×`, `4×` speed buttons. Do not import `simulateCombat`. On final frame dispatch settlement once, then show victory/defeat/draw, exact casualties, equipment destroyed, and gold reward. Reload with an active run offers `Продолжить бой` and replays the same report.

- [ ] **Step 5: Verify and commit**

Run: `npm test -- tests/unit/formation-draft.test.ts tests/unit/combat-playback.test.ts tests/unit/equipment-ui.test.ts tests/unit/combat.test.ts tests/unit/mission-lifecycle.test.ts`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS.

```bash
git add src/application/FormationDraft.ts src/ui src/game/scenes/CombatScene.ts src/game/config.ts src/styles.css tests/unit/formation-draft.test.ts tests/unit/combat-playback.test.ts tests/unit/equipment-ui.test.ts
git commit -m "feat: add autobattle formation and playback"
```

## Task 10: Verify the complete player journey and document the build

**Files:**
- Create: `playwright.config.ts`
- Create: `tests/e2e/vertical-slice.spec.ts`
- Create: `src/application/clock.ts`
- Modify: `src/main.ts`
- Modify: `src/ui/AppUi.ts`
- Modify: `README.md`

**Interfaces:**
- Produces: production foreground clock and query-gated test clock; both feed normal 250 ms domain steps.
- Produces: one browser test proving the full economy-to-combat loop and reload behavior.

- [ ] **Step 1: Write the failing E2E test**

```ts
test('builds an economy, enters combat, applies losses once, and reloads', async ({ page }) => {
  await page.goto('/?testClock=fast&seed=12345');
  await page.getByRole('button', { name: 'Новая игра' }).click();
  await page.getByRole('button', { name: 'Шахта — 200 золота' }).click();
  await page.getByRole('button', { name: 'Клетка 1, 1' }).click();
  for (let index = 0; index < 5; index += 1) {
    await page.getByRole('button', { name: 'Нанять гоблина — 40 золота' }).click();
  }
  await page.getByRole('button', { name: 'Выбрать первых 3 свободных' }).click();
  await page.getByRole('button', { name: 'Работать' }).click();
  await page.getByRole('button', { name: 'Шахта I' }).click();
  await page.getByRole('button', { name: 'Выбрать следующего свободного' }).click();
  await page.getByRole('button', { name: 'Переносить' }).click();
  await page.getByRole('button', { name: 'Источник: Шахта I' }).click();
  await page.getByRole('button', { name: 'Получатель: Склад' }).click();
  await page.getByRole('button', { name: 'Выбрать следующего свободного' }).click();
  await page.getByRole('button', { name: 'Переносить' }).click();
  await page.getByRole('button', { name: 'Источник: Склад' }).click();
  await page.getByRole('button', { name: 'Получатель: Рынок' }).click();
  await expect(page.getByText(/Продано руды: [1-9]/)).toBeVisible();
  await page.getByRole('button', { name: 'Миссия 1' }).click();
  await page.getByRole('button', { name: 'Выбрать первых 4 живых' }).click();
  await page.getByRole('button', { name: 'Выбрать бойца 1' }).click();
  await page.getByRole('button', { name: 'Надеть Ржавый меч' }).click();
  await page.getByRole('button', { name: 'Надеть Латаную броню' }).click();
  await page.getByRole('button', { name: 'Авторасстановка' }).click();
  await page.getByRole('button', { name: 'Начать бой' }).click();
  await page.getByRole('button', { name: 'Скорость 4×' }).click();
  await expect(page.getByRole('heading', { name: /Победа|Поражение|Ничья/ })).toBeVisible();
  const summary = await page.getByTestId('campaign-summary').textContent();
  await page.reload();
  await page.getByRole('button', { name: /Продолжить игру|Продолжить бой/ }).click();
  await expect(page.getByTestId('campaign-summary')).toHaveText(summary ?? '');
});
```

- [ ] **Step 2: Run E2E and confirm the missing clock/onboarding flow**

Run: `npm run e2e -- tests/e2e/vertical-slice.spec.ts`  
Expected: FAIL until the test clock and new/continue screen are connected.

- [ ] **Step 3: Implement clocks and start/continue flow**

Production clock ignores hidden-tab time and clamps a single visible render delta to 1000 ms before accumulation. Test clock is enabled only when both `import.meta.env.DEV` and `testClock=fast` are present; it feeds ordinary 250 ms steps rapidly and never changes production rates, combat values, rewards, command validation, or persistence.

New game asks for confirmation if a save exists. Continue loads schema 2. Active run routes to combat; otherwise it routes to colony. Update README with controls, architecture boundaries, commands, asset license gate, and all verification commands.

- [ ] **Step 4: Run every quality gate separately**

Run: `npm run lint`  
Expected: PASS.  
Run: `npm test`  
Expected: PASS.  
Run: `npm run build`  
Expected: PASS.  
Run: `npx playwright install chromium`  
Expected: Chromium is installed.  
Run: `npm run e2e`  
Expected: PASS.

- [ ] **Step 5: Perform the manual visual acceptance pass**

Run: `npm run dev -- --host 127.0.0.1`. Inspect at `1600×900`, `1920×1080`, and `1280×720`. Confirm crisp sprites, green/red placement preview, click/Ctrl/drag selection, readable fan menu, mine `n/5` tooltip, animated hauling, route arrows, keyboard focus, visible equipment ownership, 9×5 hex formation, report-only playback, single settlement, and reload persistence. Convert every functional defect into a failing focused test before correcting it.

- [ ] **Step 6: Commit**

```bash
git add playwright.config.ts tests/e2e src/application/clock.ts src/main.ts src/ui/AppUi.ts README.md
git commit -m "test: verify troll strategy vertical slice"
```

## Final Acceptance

From `F:\ClaudeGames\trollstrategy`, run each command separately:

```bash
npm ci
npm run assets:prepare
npm run lint
npm test
npm run build
npm run e2e
```

Acceptance requires the visible player journey to work without developer-console mutations; identical combat inputs to produce identical reports; colony results to be FPS-independent; failed commands to be atomic; mission settlement to be idempotent; casualties and equipped items to disappear from the persisted campaign; active missions to resume from the stored report; and the production bundle to contain only curated manifest assets.
