import { describe, expect, expectTypeOf, it } from 'vitest';
import { GameSession, type GameCommand } from '../../src/application/GameSession';
import {
  buildingId,
  itemId,
  unitId,
  type Formation,
  type GameState,
  type MissionRunId,
  type UnitId,
} from '../../src/domain/model';
import { getMissionOneReward } from '../../src/domain/progression/rewards';
import type { CommandResult } from '../../src/domain/results';

const MISSION_COOLDOWN_MS = 120_000;

function ownedState(session: GameSession): GameState {
  return (session as unknown as { state: GameState }).state;
}

function makeMissionAvailable(session: GameSession): void {
  for (let elapsed = 0; elapsed < MISSION_COOLDOWN_MS; elapsed += 5_000) {
    session.advance(5_000);
  }
}

function recruit(
  session: GameSession,
  species: 'goblin' | 'troll',
  id: string,
): UnitId {
  const idValue = unitId(id);
  const result = session.dispatch({ type: 'RECRUIT_UNIT', species, id: idValue, name: id });
  if (!result.ok) {
    throw new Error(`Fixture recruitment failed: ${result.error.code}`);
  }
  return idValue;
}

function readySession(species: 'goblin' | 'troll', count: number): GameSession {
  const session = GameSession.createNew(12_345);
  ownedState(session).wallet.gold = 10_000;
  for (let index = 1; index <= count; index += 1) {
    recruit(session, species, `${species}-${index}`);
  }
  makeMissionAvailable(session);
  return session;
}

function formationFor(unitIds: UnitId[]): Formation {
  const cells = [
    { x: 1, y: 2 },
    { x: 4, y: 2 },
    { x: 1, y: 3 },
    { x: 4, y: 3 },
  ];
  return { unitIds: [...unitIds], slots: cells.slice(0, unitIds.length) };
}

function start(session: GameSession, formation: Formation): MissionRunId {
  const result = session.dispatch({ type: 'START_MISSION', missionId: 'mission-1', formation });
  if (!result.ok) {
    throw new Error(`Fixture mission start failed: ${result.error.code}`);
  }
  return result.value;
}

describe('mission rewards', () => {
  it('returns detached first-clear and repeat rewards', () => {
    const first = getMissionOneReward(true);
    const second = getMissionOneReward(true);
    first.unlocks.length = 0;

    expect(second).toEqual({ gold: 300, ironOre: 40, unlocks: ['smelter-recipe'] });
    expect(getMissionOneReward(false)).toEqual({ gold: 80, ironOre: 10, unlocks: [] });
  });
});

describe('mission start', () => {
  it('preserves compiler-checked result types and does not accept a report contract', () => {
    const session = readySession('troll', 1);
    const result = session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: formationFor([unitId('troll-1')]),
    });
    type StartCommand = Extract<GameCommand, { type: 'START_MISSION' }>;

    expectTypeOf(result).toEqualTypeOf<CommandResult<MissionRunId>>();
    expectTypeOf<keyof StartCommand>().toEqualTypeOf<'type' | 'missionId' | 'formation'>();
  });

  it('rejects an unavailable mission without mutation', () => {
    const session = GameSession.createNew(1);
    ownedState(session).wallet.gold = 1_000;
    const fighterId = recruit(session, 'troll', 'fighter');
    const before = session.exportState();

    expect(session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: formationFor([fighterId]),
    })).toEqual({ ok: false, error: { code: 'MISSION_UNAVAILABLE' } });
    expect(session.exportState()).toEqual(before);
  });

  it.each([
    ['empty', { unitIds: [], slots: [] }],
    ['more than four', formationFor([
      unitId('troll-1'), unitId('troll-2'), unitId('troll-3'), unitId('troll-4'), unitId('troll-5'),
    ])],
    ['duplicate units', {
      unitIds: [unitId('troll-1'), unitId('troll-1')],
      slots: [{ x: 1, y: 2 }, { x: 2, y: 2 }],
    }],
    ['duplicate cells', {
      unitIds: [unitId('troll-1'), unitId('troll-2')],
      slots: [{ x: 1, y: 2 }, { x: 1, y: 2 }],
    }],
    ['enemy cell', {
      unitIds: [unitId('troll-1')],
      slots: [{ x: 1, y: 1 }],
    }],
    ['outside grid', {
      unitIds: [unitId('troll-1')],
      slots: [{ x: 6, y: 2 }],
    }],
  ] satisfies [string, Formation][])('rejects an invalid %s formation atomically', (_name, formation) => {
    const session = readySession('troll', 5);
    const before = session.exportState();

    expect(session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation,
    })).toEqual({ ok: false, error: { code: 'INVALID_FORMATION' } });
    expect(session.exportState()).toEqual(before);
  });

  it('locks selected units and creates a canonical reload-safe run', () => {
    const session = readySession('troll', 4);
    const ids = ownedState(session).units.map((unit) => unit.id);
    expect(session.dispatch({
      type: 'EQUIP_ITEM',
      unitId: ids[0]!,
      itemId: itemId('rusty-sword-1'),
    }).ok).toBe(true);
    expect(session.dispatch({
      type: 'EQUIP_ITEM',
      unitId: ids[0]!,
      itemId: itemId('patched-armor-1'),
    }).ok).toBe(true);
    const draft = formationFor(ids);
    const runId = start(session, draft);
    draft.unitIds.length = 0;
    draft.slots[0]!.x = 5;

    const snapshotRun = session.getSnapshot().mission.activeRun!;
    const exportedRun = session.exportState().mission.activeRun!;
    expect(snapshotRun.runId).toBe(runId);
    expect(snapshotRun.formation.unitIds).toEqual(ids);
    expect(snapshotRun.combatSeed).toBe(snapshotRun.combatInput.seed);
    expect(snapshotRun.combatReport).toEqual(exportedRun.combatReport);
    expect(snapshotRun).toEqual(exportedRun);
    expect(snapshotRun.combatInput.playerSlots[0]?.combatant.damage).toBe(8);
    expect(snapshotRun.combatInput.playerSlots[0]?.combatant.armor).toBe(4);
    expect(snapshotRun.combatInput.enemySlots).toHaveLength(4);
    expect(session.exportState().units.every((unit) => unit.assignment.kind === 'mission')).toBe(true);
    expect(session.dispatch({
      type: 'ASSIGN_WORKERS',
      unitIds: [ids[0]!],
      buildingId: buildingId('mine'),
    })).toEqual({ ok: false, error: { code: 'INVALID_ASSIGNMENT' } });
    expect(session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: formationFor(ids),
    })).toEqual({ ok: false, error: { code: 'MISSION_UNAVAILABLE' } });
  });

  it('rejects working units without mutating their assignment', () => {
    const session = readySession('troll', 1);
    const fighterId = unitId('troll-1');
    expect(session.dispatch({
      type: 'ASSIGN_WORKERS', unitIds: [fighterId], buildingId: buildingId('mine'),
    }).ok).toBe(true);
    const before = session.exportState();

    expect(session.dispatch({
      type: 'START_MISSION', missionId: 'mission-1', formation: formationFor([fighterId]),
    })).toEqual({ ok: false, error: { code: 'INVALID_ASSIGNMENT' } });
    expect(session.exportState()).toEqual(before);
  });
});

describe('mission settlement', () => {
  it('settles a losing stored report on abort, including death and equipment loss', () => {
    const session = readySession('goblin', 1);
    const fighterId = unitId('goblin-1');
    expect(session.dispatch({
      type: 'EQUIP_ITEM', unitId: fighterId, itemId: itemId('rusty-sword-1'),
    }).ok).toBe(true);
    expect(session.dispatch({
      type: 'EQUIP_ITEM', unitId: fighterId, itemId: itemId('patched-armor-1'),
    }).ok).toBe(true);
    const goldBefore = session.getSnapshot().wallet.gold;
    const runId = start(session, formationFor([fighterId]));
    expect(session.getSnapshot().mission.activeRun?.combatReport.winner).toBe('enemy');

    expect(session.dispatch({ type: 'ABORT_MISSION', runId }))
      .toEqual({ ok: true, value: undefined });
    const settled = session.exportState();
    expect(settled.mission.activeRun).toBeNull();
    expect(settled.mission.appliedRunIds).toEqual([runId]);
    expect(settled.wallet.gold).toBe(goldBefore);
    expect(settled.units).toEqual([]);
    expect(settled.items.find(({ id }) => id === itemId('rusty-sword-1'))).toBeUndefined();
    expect(settled.items.find(({ id }) => id === itemId('patched-armor-1'))).toBeUndefined();
    expect(settled.mission.nextMissionAtMs).toBe(
      settled.simulationTimeMs + MISSION_COOLDOWN_MS,
    );
    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId }))
      .toEqual({ ok: false, error: { code: 'ALREADY_APPLIED' } });
    expect(session.dispatch({ type: 'ABORT_MISSION', runId }))
      .toEqual({ ok: false, error: { code: 'ALREADY_APPLIED' } });
  });

  it('settles a winning stored report on abort and applies its reward once', () => {
    const session = readySession('troll', 4);
    const ids = ownedState(session).units.map((unit) => unit.id);
    const goldBefore = session.getSnapshot().wallet.gold;
    const runId = start(session, formationFor(ids));
    expect(session.getSnapshot().mission.activeRun?.combatReport.winner).toBe('player');

    expect(session.dispatch({ type: 'ABORT_MISSION', runId }))
      .toEqual({ ok: true, value: undefined });
    expect(session.getSnapshot().wallet.gold).toBe(goldBefore + 300);
    expect(session.getSnapshot().mission.clearedMissionIds).toEqual(['mission-1']);
    const settled = session.getSnapshot();
    expect(session.dispatch({ type: 'ABORT_MISSION', runId }))
      .toEqual({ ok: false, error: { code: 'ALREADY_APPLIED' } });
    expect(session.getSnapshot()).toEqual(settled);
  });

  it('applies first-clear and repeat victory rewards exactly once', () => {
    const session = readySession('troll', 4);
    const ids = ownedState(session).units.map((unit) => unit.id);
    const warehouse = ownedState(session).buildings.find(({ kind }) => kind === 'warehouse')!;
    const townHall = ownedState(session).buildings.find(({ kind }) => kind === 'town-hall')!;
    warehouse.inventory.ironOre = 490;
    const firstRunId = start(session, formationFor(ids));
    expect(session.getSnapshot().mission.activeRun?.combatReport.winner).toBe('player');
    const goldBefore = session.getSnapshot().wallet.gold;

    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId: firstRunId })).toEqual({
      ok: true,
      value: { gold: 300, ironOre: 40, unlocks: ['smelter-recipe'] },
    });
    expect(session.getSnapshot().wallet.gold).toBe(goldBefore + 300);
    expect(session.getSnapshot().buildings.find(({ kind }) => kind === 'warehouse')?.inventory.ironOre)
      .toBe(500);
    expect(session.getSnapshot().buildings.find(({ kind }) => kind === 'town-hall')?.inventory.ironOre)
      .toBe(30);
    expect(session.getSnapshot().unlocks).toEqual(['smelter-recipe']);
    const afterFirst = session.getSnapshot();
    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId: firstRunId }))
      .toEqual({ ok: false, error: { code: 'ALREADY_APPLIED' } });
    expect(session.getSnapshot()).toEqual(afterFirst);

    makeMissionAvailable(session);
    const secondRunId = start(session, formationFor(ids));
    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId: secondRunId })).toEqual({
      ok: true,
      value: { gold: 80, ironOre: 10, unlocks: [] },
    });
    expect(session.getSnapshot().buildings.find(({ kind }) => kind === 'warehouse')?.inventory.ironOre)
      .toBe(500);
    expect(session.getSnapshot().buildings.find(({ kind }) => kind === 'town-hall')?.inventory.ironOre)
      .toBe(40);
    expect(warehouse.inventory.ironOre + townHall.inventory.ironOre).toBe(490 + 40 + 10);
    expect(session.getSnapshot().unlocks).toEqual(['smelter-recipe']);
  });

  it('permanently removes dead players and their equipped items without rewarding defeat', () => {
    const session = readySession('goblin', 1);
    const fighterId = unitId('goblin-1');
    expect(session.dispatch({
      type: 'EQUIP_ITEM', unitId: fighterId, itemId: itemId('rusty-sword-1'),
    }).ok).toBe(true);
    expect(session.dispatch({
      type: 'EQUIP_ITEM', unitId: fighterId, itemId: itemId('patched-armor-1'),
    }).ok).toBe(true);
    const runId = start(session, formationFor([fighterId]));
    expect(session.getSnapshot().mission.activeRun?.combatReport.winner).toBe('enemy');
    expect(session.getSnapshot().mission.activeRun?.combatReport.deadPlayerUnitIds)
      .toEqual([fighterId]);
    const goldBefore = session.getSnapshot().wallet.gold;

    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId })).toEqual({
      ok: true,
      value: { gold: 0, ironOre: 0, unlocks: [] },
    });
    const state = session.exportState();
    expect(state.wallet.gold).toBe(goldBefore);
    expect(state.units).toEqual([]);
    expect(state.items.find(({ id }) => id === itemId('rusty-sword-1'))).toBeUndefined();
    expect(state.items.find(({ id }) => id === itemId('patched-armor-1'))).toBeUndefined();
    expect(state.mission.clearedMissionIds).toEqual([]);
  });

  it('gives no reward and no first clear for a draw', () => {
    const session = readySession('troll', 1);
    const fighterId = unitId('troll-1');
    const runId = start(session, formationFor([fighterId]));
    const state = ownedState(session);
    state.mission.activeRun!.combatReport.winner = 'draw';
    state.mission.activeRun!.combatReport.deadPlayerUnitIds = [];
    const before = session.exportState();

    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId })).toEqual({
      ok: true,
      value: { gold: 0, ironOre: 0, unlocks: [] },
    });
    expect(session.getSnapshot().wallet.gold).toBe(before.wallet.gold);
    expect(session.getSnapshot().mission.clearedMissionIds).toEqual([]);
    expect(session.getSnapshot().units[0]?.assignment).toEqual({ kind: 'idle' });
  });
});
