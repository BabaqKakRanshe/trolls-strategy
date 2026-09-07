# Colony Economy Slice Implementation Plan

**Status:** Implemented and verified on 2026-09-05.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать один играбельный экран, где игрок строит шахты, покупает гоблинов и троллей, назначает их на добычу или постоянные маршруты, физически перевозит руду через склад и продаёт её на рынке.

**Architecture:** `GameSession` остаётся единственным владельцем состояния. Один чистый domain-модуль содержит модель, команды, размещение и fixed-step симуляцию; Phaser только рисует snapshot, а DOM-панель и компактный контроллер хранят выбор и режим ввода. Это минимальная структура, которая соблюдает границы проекта без ECS, сохранений, боя и универсальных фреймворков.

**Tech Stack:** TypeScript 6, Phaser 4.2.1, Vite 8, Vitest 5, ESLint 10, Playwright 1.63.

**Spec:** `docs/superpowers/specs/2026-09-05-troll-strategy-vertical-slice-v2.md`, только разделы 5.1–5.6; сообщение пользователя от 2026-09-05 сужает scope и исключает миссии, экипировку и сохранения.

## Global Constraints

- Создавать новый runtime поверх намеренно удалённых файлов, не выполнять `git restore`, `git checkout` или `git reset`.
- В scope входят только шахта, склад, рынок, гоблины, тролли, работа, перевозка и продажа руды.
- Старт: 1000 золота, склад и рынок уже стоят, шахт и юнитов нет.
- Экономика шагает только фиксированными шагами 250 ms.
- `GameSession` — единственный владелец изменяемого состояния; Phaser и DOM читают snapshots и отправляют команды.
- Runtime использует только curated manifest; отсутствующий спрайт заменяется геометрией.
- Минимум тестов: один unit-файл для критических правил и один E2E-файл для полного видимого цикла.
- Не добавлять ECS, persistence, combat, pathfinding, crafting, generic event bus или state-management library.

## Locked File Structure

```text
src/
├─ content/catalog.ts                  # все цены, размеры, скорости и вместимости
├─ domain/colony.ts                    # state, commands, placement, mining, hauling
├─ application/GameSession.ts          # единственный mutable owner, fixed-step accumulator
├─ application/MapInteractionController.ts # selection и режимы целей
├─ game/ColonyScene.ts                 # Phaser rendering/input only
├─ ui/AppUi.ts                         # HUD, shop, roster, native command controls
├─ main.ts                             # composition root
└─ styles.css                          # pixel UI and responsive layout
```

## Task 1: Recreate toolchain and the smallest curated asset set

**Files:**
- Create: `package.json`, `tsconfig.json`, `eslint.config.js`, `vite.config.ts`, `index.html`
- Create: `scripts/prepare-assets.py`
- Modify: `assets/curated-assets.json`
- Create generated files below `public/assets/runtime`

**Interfaces:**
- Produces `npm run dev`, `npm run build`, `npm test`, `npm run lint`, `npm run e2e`.
- Curates three building images plus idle/walk sheets for goblin and troll.

- [ ] **Step 1:** Create exact npm/TypeScript/Vite/ESLint configuration with no runtime dependencies besides Phaser.
- [ ] **Step 2:** Implement one asset script that validates paths, promotes four chosen unit sheets into `assets/sprites/sources/units`, then copies only manifest entries to `public/assets/runtime`.
- [ ] **Step 3:** Run `npm install`, `npm run assets:prepare`, and `npm run build`; expect all to pass.

## Task 2: Implement the complete economy as one pure domain module

**Files:**
- Create: `src/content/catalog.ts`
- Create: `src/domain/colony.ts`
- Create: `src/application/GameSession.ts`
- Test: `tests/unit/colony.test.ts`

**Interfaces:**
- Commands: `BUILD_MINE`, `BUY_UNIT`, `ASSIGN_WORK`, `ASSIGN_HAUL`, `RELEASE_UNITS`.
- `GameSession.dispatch(command): CommandResult` validates atomically and emits a detached snapshot.
- `GameSession.advance(deltaMs)` converts render deltas into ordinary 250 ms domain ticks.

- [ ] **Step 1: Write the narrow failing rules test**

```ts
it('builds, mines, hauls through storage, and sells without losing ore', () => {
  const session = new GameSession();
  expect(session.dispatch({ type: 'BUILD_MINE', cell: { x: 1, y: 1 } }).ok).toBe(true);
  for (let index = 0; index < 5; index += 1) {
    expect(session.dispatch({ type: 'BUY_UNIT', unitKind: 'goblin' }).ok).toBe(true);
  }
  const [a, b, c, d, e] = session.snapshot().units.map((unit) => unit.id);
  const mineId = session.snapshot().buildings.find((building) => building.kind === 'mine')!.id;
  expect(session.dispatch({ type: 'ASSIGN_WORK', unitIds: [a!, b!, c!], buildingId: mineId }).ok).toBe(true);
  expect(session.dispatch({ type: 'ASSIGN_HAUL', unitIds: [d!], sourceId: mineId, destinationId: 'warehouse-1' }).ok).toBe(true);
  expect(session.dispatch({ type: 'ASSIGN_HAUL', unitIds: [e!], sourceId: 'warehouse-1', destinationId: 'market-1' }).ok).toBe(true);
  session.advance(60_000);
  expect(session.snapshot().gold).toBeGreaterThan(600);
  expect(session.snapshot().soldOre).toBeGreaterThan(0);
});
```

- [ ] **Step 2:** Run `npm test -- tests/unit/colony.test.ts`; expect unresolved imports.
- [ ] **Step 3:** Implement catalog values, top-left footprint validation, atomic commands, mine worker cap 5, fixed mining, straight-line hauling with visible phase/progress, warehouse capacity and market sale.
- [ ] **Step 4:** Add only critical negative cases to the same file: overlapping mine does not charge gold; sixth worker assignment fails without partial mutation; invalid route fails.
- [ ] **Step 5:** Run the focused unit test; expect pass.

## Task 3: Build one polished playable colony screen

**Files:**
- Create: `src/application/MapInteractionController.ts`
- Create: `src/game/ColonyScene.ts`
- Create: `src/ui/AppUi.ts`
- Create: `src/main.ts`, `src/styles.css`

**Interfaces:**
- Phaser renders buildings, units, carried ore, selection, grid preview and route arrows from snapshots.
- Native DOM controls expose the same build, purchase, selection, work, haul and release actions.

- [ ] **Step 1:** Implement controller modes `neutral`, `placingMine`, `choosingWorkTarget`, `choosingHaulSource`, `choosingHaulDestination` with click/Ctrl-click/drag selection.
- [ ] **Step 2:** Implement the scene with a 20×14 grid, hidden ordinary grid, green/red placement preview, sprite fallback, object tooltips and interpolated hauler positions.
- [ ] **Step 3:** Implement semantic left HUD, right shop, roster, contextual command palette, building target buttons, visible focus and one polite live region.
- [ ] **Step 4:** Compose the app and ensure world rules remain outside Phaser and DOM modules.

## Task 4: Verify the visible player loop

**Files:**
- Create: `playwright.config.ts`
- Create: `tests/e2e/colony.spec.ts`
- Modify: `README.md`

- [ ] **Step 1: Write one browser test**

```ts
test('runs the mine to market loop using visible controls', async ({ page }) => {
  await page.goto('/?fast=1');
  await page.getByRole('button', { name: 'Купить шахту за 200 золота' }).click();
  await page.getByRole('button', { name: 'Поставить автоматически' }).click();
  for (let index = 0; index < 5; index += 1) await page.getByRole('button', { name: 'Нанять гоблина за 40 золота' }).click();
  await page.getByRole('button', { name: 'Выбрать первых 3 свободных' }).click();
  await page.getByRole('button', { name: 'Работать' }).click();
  await page.getByRole('button', { name: 'Шахта 1' }).click();
  await page.getByRole('button', { name: 'Выбрать следующего свободного' }).click();
  await page.getByRole('button', { name: 'Переносить' }).click();
  await page.getByRole('button', { name: 'Источник Шахта 1' }).click();
  await page.getByRole('button', { name: 'Получатель Склад' }).click();
  await page.getByRole('button', { name: 'Выбрать следующего свободного' }).click();
  await page.getByRole('button', { name: 'Переносить' }).click();
  await page.getByRole('button', { name: 'Источник Склад' }).click();
  await page.getByRole('button', { name: 'Получатель Рынок' }).click();
  await expect(page.getByTestId('sold-ore')).not.toHaveText('0');
});
```

- [ ] **Step 2:** Enable `fast=1` only in development/test composition; it feeds repeated normal 250 ms ticks and changes no game values.
- [ ] **Step 3:** Run `npm run lint`, `npm test`, `npm run build`, and `npm run e2e` separately.
- [ ] **Step 4:** Inspect the real app at 1600×900 and 1280×720; verify readable UI, crisp sprites, mouse selection, build preview, moving cargo and responsive panels.
