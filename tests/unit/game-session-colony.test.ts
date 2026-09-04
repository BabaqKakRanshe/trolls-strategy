import { describe, expect, expectTypeOf, it } from 'vitest';
import { GameSession } from '../../src/application/GameSession';
import {
  buildingId,
  combatantId,
  itemId,
  missionRunId,
  unitId,
  type GameState,
  type UnitId,
} from '../../src/domain/model';
import type { CommandResult } from '../../src/domain/results';

function recruitGoblin(session: GameSession, id: string): void {
  const result = session.dispatch({
    type: 'RECRUIT_UNIT',
    species: 'goblin',
    id: unitId(id),
    name: id,
  });
  if (!result.ok) {
    throw new Error(`Could not recruit fixture: ${result.error.code}`);
  }
}

function sessionWithWarehouseOre(): GameSession {
  const session = GameSession.createNew(7);
  recruitGoblin(session, 'worker');
  recruitGoblin(session, 'hauler');
  expect(session.dispatch({
    type: 'ASSIGN_WORKERS',
    unitIds: [unitId('worker')],
    buildingId: buildingId('mine'),
  }).ok).toBe(true);
  expect(session.dispatch({
    type: 'ASSIGN_HAULER',
    unitId: unitId('hauler'),
    fromId: buildingId('mine'),
    toId: buildingId('warehouse'),
  }).ok).toBe(true);
  session.advance(5_000);
  session.advance(5_000);
  session.advance(5_000);
  session.advance(5_000);
  return session;
}

describe('GameSession commands', () => {
  it('preserves exact result types for each command discriminant', () => {
    const session = GameSession.createNew(7);
    const recruitResult = session.dispatch({
      type: 'RECRUIT_UNIT',
      species: 'goblin',
      id: unitId('typed-unit'),
      name: 'Typed unit',
    });
    const sellResult = session.dispatch({
      type: 'SELL_ORE',
      buildingId: buildingId('warehouse'),
      amount: 1,
    });
    const assignResult = session.dispatch({
      type: 'ASSIGN_WORKERS',
      unitIds: [unitId('typed-unit')],
      buildingId: buildingId('mine'),
    });

    expectTypeOf(recruitResult).toEqualTypeOf<CommandResult<UnitId>>();
    expectTypeOf(sellResult).toEqualTypeOf<CommandResult<number>>();
    expectTypeOf(assignResult).toEqualTypeOf<CommandResult<void>>();
  });

  it('dispatches recruitment and equipment commands through one owner', () => {
    const session = GameSession.createNew(7);
    recruitGoblin(session, 'u1');

    expect(session.dispatch({
      type: 'EQUIP_ITEM',
      unitId: unitId('u1'),
      itemId: itemId('rusty-sword-1'),
    })).toEqual({ ok: true, value: undefined });
    expect(session.getSnapshot().units[0]?.equipment.weaponId).toBe(itemId('rusty-sword-1'));

    expect(session.dispatch({
      type: 'UNEQUIP_ITEM',
      unitId: unitId('u1'),
      slot: 'weapon',
    })).toEqual({ ok: true, value: undefined });
    expect(session.getSnapshot().colonyInventory.itemIds).toContain(itemId('rusty-sword-1'));
  });

  it('sells warehouse ore through one atomic command', () => {
    const session = sessionWithWarehouseOre();
    const before = session.getSnapshot();
    const warehouseOre = before.buildings.find((building) => building.id === 'warehouse')!
      .inventory.ironOre;

    expect(warehouseOre).toBeGreaterThanOrEqual(1);
    expect(session.dispatch({
      type: 'SELL_ORE',
      buildingId: buildingId('warehouse'),
      amount: 1,
    })).toEqual({ ok: true, value: 3 });
    const after = session.getSnapshot();
    expect(after.wallet.gold).toBe(before.wallet.gold + 3);
    expect(after.buildings.find((building) => building.id === 'warehouse')?.inventory.ironOre)
      .toBe(warehouseOre - 1);
  });

  it.each([0, -1, 1.5, Number.NaN, Number.POSITIVE_INFINITY])(
    'rejects invalid sale amount %s atomically',
    (amount) => {
      const session = sessionWithWarehouseOre();
      const before = session.exportState();

      expect(session.dispatch({
        type: 'SELL_ORE',
        buildingId: buildingId('warehouse'),
        amount,
      })).toEqual({ ok: false, error: { code: 'INVALID_AMOUNT' } });
      expect(session.exportState()).toEqual(before);
    },
  );

  it('rejects unavailable ore without partially changing state or revision', () => {
    const session = GameSession.createNew(7);
    const before = session.getSnapshot();

    expect(session.dispatch({
      type: 'SELL_ORE',
      buildingId: buildingId('warehouse'),
      amount: 1,
    })).toEqual({ ok: false, error: { code: 'INSUFFICIENT_RESOURCES' } });
    expect(session.getSnapshot()).toEqual(before);
  });
});

describe('GameSession ticking and projections', () => {
  it('reuses a frozen snapshot until the session revision changes', () => {
    const session = GameSession.createNew(7);
    const first = session.getSnapshot();

    expect(session.getSnapshot()).toBe(first);
    expect(session.getRevision()).toBe(0);
    expect(session.getSimulationTimeMs()).toBe(0);
    session.advance(250);

    expect(session.getSnapshot()).not.toBe(first);
    expect(session.getRevision()).toBe(1);
    expect(session.getSimulationTimeMs()).toBe(250);
  });

  it('uses a 250 ms accumulator and caps one frame contribution at five seconds', () => {
    const session = GameSession.createNew(7);

    session.advance(249);
    expect(session.getSnapshot()).toMatchObject({ simulationTimeMs: 0, revision: 0 });
    session.advance(1);
    expect(session.getSnapshot()).toMatchObject({ simulationTimeMs: 250, revision: 1 });

    session.advance(9_000);
    expect(session.getSnapshot()).toMatchObject({ simulationTimeMs: 5_250, revision: 21 });
  });

  it('ignores invalid elapsed time', () => {
    const session = GameSession.createNew(7);
    session.advance(-250);
    session.advance(Number.NaN);
    session.advance(Number.POSITIVE_INFINITY);
    expect(session.getSnapshot()).toMatchObject({ simulationTimeMs: 0, revision: 0 });
  });

  it('returns recursively frozen, detached snapshots', () => {
    const session = GameSession.createNew(7);
    recruitGoblin(session, 'u1');
    expect(session.dispatch({
      type: 'EQUIP_ITEM',
      unitId: unitId('u1'),
      itemId: itemId('rusty-sword-1'),
    }).ok).toBe(true);
    const snapshot = session.getSnapshot();

    expect(Object.isFrozen(snapshot)).toBe(true);
    expect(Object.isFrozen(snapshot.wallet)).toBe(true);
    expect(Object.isFrozen(snapshot.units)).toBe(true);
    expect(Object.isFrozen(snapshot.units[0]?.equipment)).toBe(true);
    expect(Object.isFrozen(snapshot.colonyInventory.itemIds)).toBe(true);
    expect(Reflect.set(snapshot.wallet, 'gold', 0)).toBe(false);
    expect(session.getSnapshot().wallet.gold).toBe(960);
  });

  it('exports detached mutable state without exposing session ownership', () => {
    const session = GameSession.createNew(7);
    const exported = session.exportState();
    exported.wallet.gold = 0;
    exported.colonyInventory.itemIds.length = 0;

    expect(session.getSnapshot().wallet.gold).toBe(1_000);
    expect(session.getSnapshot().colonyInventory.itemIds).toHaveLength(8);
  });

  it('detaches and freezes nested active mission combat data', () => {
    const session = GameSession.createNew(7);
    const ownedState = (session as unknown as { state: GameState }).state;
    const runId = missionRunId('run-1');
    const fighterId = combatantId('player-u1');
    ownedState.mission.activeRun = {
      runId,
      missionId: 'mission-1',
      formation: { unitIds: [unitId('u1')], slots: [{ x: 2, y: 1 }] },
      combatSeed: 123,
      combatInput: {
        seed: 123,
        maxDurationMs: 60_000,
        playerSlots: [{
          x: 2,
          y: 1,
          combatant: {
            id: fighterId,
            unitId: unitId('u1'),
            side: 'player',
            maxHealth: 20,
            speed: 5,
            damage: 2,
            armor: 1,
            attackIntervalMs: 2_000,
          },
        }],
        enemySlots: [],
      },
      combatReport: {
        winner: 'player',
        durationMs: 250,
        frames: [{
          timeMs: 250,
          combatants: [{ id: fighterId, x: 2, y: 1, health: 19 }],
        }],
        deadPlayerUnitIds: [],
        deadEnemyCombatantIds: [],
      },
      status: 'active',
    };

    const snapshot = session.getSnapshot();
    const exported = session.exportState();
    const snapshotRun = snapshot.mission.activeRun!;
    const exportedRun = exported.mission.activeRun!;

    expect(Object.isFrozen(snapshotRun.combatInput.playerSlots[0]?.combatant)).toBe(true);
    expect(Object.isFrozen(snapshotRun.combatReport.frames[0]?.combatants)).toBe(true);
    exportedRun.combatInput.playerSlots[0]!.combatant.maxHealth = 0;
    exportedRun.combatReport.frames[0]!.combatants[0]!.health = 0;

    expect(snapshotRun.combatInput.playerSlots[0]?.combatant.maxHealth).toBe(20);
    expect(snapshotRun.combatReport.frames[0]?.combatants[0]?.health).toBe(19);
    expect(session.exportState().mission.activeRun?.combatReport.frames[0]?.combatants[0]?.health)
      .toBe(19);
  });
});
