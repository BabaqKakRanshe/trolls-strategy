import { describe, expect, it } from 'vitest';

import { GameSession } from '../../src/application/GameSession';
import { MapInteractionController } from '../../src/application/MapInteractionController';
import { CELL_SIZE } from '../../src/content/catalog';

function buyGoblins(session: GameSession, amount: number): void {
  expect(session.dispatch({
    type: 'BUY_UNITS',
    unitKind: 'goblin',
    amount,
    cell: { x: 6, y: 6 },
  }).ok).toBe(true);
}

describe('colony economy', () => {
  it('buys a 20-unit crowd atomically in the selected cell', () => {
    const session = new GameSession();
    const cell = { x: 4, y: 6 };

    expect(session.dispatch({ type: 'BUY_UNITS', unitKind: 'goblin', amount: 20, cell }).ok).toBe(true);

    const units = session.snapshot().units;
    const crowdCells = units.map((unit) =>
      `${Math.floor(unit.position.x / CELL_SIZE)}:${Math.floor(unit.position.y / CELL_SIZE)}`,
    );
    const crowdPositions = units.map((unit) => `${unit.position.x}:${unit.position.y}`);

    expect(new Set(crowdCells)).toEqual(new Set(['4:6']));
    expect(new Set(crowdPositions).size).toBe(20);
    expect(new Set(units.map((unit) => unit.position.x)).size).toBeGreaterThan(5);
    expect(new Set(units.map((unit) => unit.position.y)).size).toBeGreaterThan(4);

    const beforeRejectedPurchase = session.snapshot();
    expect(session.dispatch({ type: 'BUY_UNITS', unitKind: 'goblin', amount: 1, cell }))
      .toEqual({ ok: false, error: 'В одной клетке помещается не больше 20 существ' });
    expect(session.snapshot()).toEqual(beforeRejectedPurchase);
  });

  it('builds, mines, hauls through storage, and sells without losing ore', () => {
    const session = new GameSession();
    expect(session.dispatch({ type: 'BUILD_MINE', cell: { x: 1, y: 1 } }).ok).toBe(true);
    buyGoblins(session, 5);

    const [workerA, workerB, workerC, mineHauler, marketHauler] = session
      .snapshot()
      .units.map((unit) => unit.id);
    const mineId = session.snapshot().buildings.find((building) => building.kind === 'mine')!.id;

    expect(
      session.dispatch({
        type: 'ASSIGN_WORK',
        unitIds: [workerA!, workerB!, workerC!],
        buildingId: mineId,
      }).ok,
    ).toBe(true);
    expect(
      session.dispatch({
        type: 'ASSIGN_HAUL',
        unitIds: [mineHauler!],
        sourceId: mineId,
        destinationId: 'warehouse-1',
      }).ok,
    ).toBe(true);
    expect(
      session.dispatch({
        type: 'ASSIGN_HAUL',
        unitIds: [marketHauler!],
        sourceId: 'warehouse-1',
        destinationId: 'market-1',
      }).ok,
    ).toBe(true);

    session.advance(60_000);
    const snapshot = session.snapshot();
    expect(snapshot.gold).toBeGreaterThan(600);
    expect(snapshot.soldOre).toBeGreaterThan(0);
    expect(snapshot.buildings.find((building) => building.id === 'warehouse-1')!.ore).toBeGreaterThanOrEqual(0);
    expect(Number.isInteger(snapshot.soldOre)).toBe(true);
    expect(snapshot.buildings.every((building) => Number.isInteger(building.ore))).toBe(true);
    expect(snapshot.units.every((unit) => unit.assignment.kind !== 'haul' || Number.isInteger(unit.assignment.carried))).toBe(true);
  });

  it('rejects overlapping construction without charging gold', () => {
    const session = new GameSession();
    const before = session.snapshot();

    const result = session.dispatch({ type: 'BUILD_MINE', cell: { x: 10, y: 8 } });

    expect(result).toEqual({ ok: false, error: 'Здесь уже стоит здание' });
    expect(session.snapshot()).toEqual(before);
  });

  it('fills available mine slots and leaves surplus selected units idle', () => {
    const session = new GameSession();
    expect(session.dispatch({ type: 'BUILD_MINE', cell: { x: 1, y: 1 } }).ok).toBe(true);
    buyGoblins(session, 9);
    const snapshot = session.snapshot();
    const mineId = snapshot.buildings.find((building) => building.kind === 'mine')!.id;
    const unitIds = snapshot.units.map((unit) => unit.id);
    const positionsBeforeAssignment = new Map(
      snapshot.units.map((unit) => [unit.id, unit.position]),
    );

    expect(session.dispatch({ type: 'ASSIGN_WORK', unitIds, buildingId: mineId }).ok).toBe(true);
    const walkingWorkers = session.snapshot().units.filter((unit) => unit.assignment.kind === 'to-work');
    expect(walkingWorkers).toHaveLength(5);
    expect(walkingWorkers.map((unit) => unit.position)).toEqual(
      walkingWorkers.map((unit) => positionsBeforeAssignment.get(unit.id)),
    );
    expect(session.snapshot().units.filter((unit) => unit.assignment.kind === 'idle')).toHaveLength(4);

    for (let step = 0; step < 20; step += 1) {
      session.advance(250);
      expect(session.snapshot().buildings.every((building) => Number.isInteger(building.ore))).toBe(true);
    }
    expect(session.snapshot().units.filter((unit) => unit.assignment.kind === 'work')).toHaveLength(5);

    expect(
      session.dispatch({
        type: 'ASSIGN_HAUL',
        unitIds: [unitIds[8]!],
        sourceId: 'market-1',
        destinationId: mineId,
      }),
    ).toEqual({ ok: false, error: 'Этот маршрут не перевозит руду' });
    expect(session.snapshot().units[8]!.assignment.kind).toBe('idle');
  });

  it('offers only valid buildings while choosing a haul route', () => {
    const session = new GameSession();
    expect(session.dispatch({ type: 'BUILD_MINE', cell: { x: 1, y: 1 } }).ok).toBe(true);
    buyGoblins(session, 1);
    const interactions = new MapInteractionController(session);
    const mineId = session.snapshot().buildings.find((building) => building.kind === 'mine')!.id;
    interactions.clickUnit(session.snapshot().units[0]!.id, false);

    interactions.beginHaulTarget();
    expect(new Set(interactions.targetBuildingIds())).toEqual(new Set([mineId, 'warehouse-1']));

    interactions.chooseBuilding('market-1');
    expect(interactions.mode.kind).toBe('choosing-haul-source');

    interactions.chooseBuilding(mineId);
    expect(interactions.mode.kind).toBe('choosing-haul-destination');
    expect(new Set(interactions.targetBuildingIds())).toEqual(new Set(['warehouse-1', 'market-1']));
  });
});
