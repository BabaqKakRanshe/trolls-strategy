import { describe, expect, it } from 'vitest';
import { GameSession } from '../../src/application/GameSession';
import { simulateCombat } from '../../src/domain/combat/simulateCombat';
import { createInitialState } from '../../src/domain/initialState';
import {
  buildingId,
  itemId,
  missionRunId,
  unitId,
  type GameState,
  type UnitId,
} from '../../src/domain/model';
import { decodeSave, encodeSave } from '../../src/persistence/saveCodec';

function mutableState(session: GameSession): GameState {
  return (session as unknown as { state: GameState }).state;
}

function recruit(session: GameSession, id: string): UnitId {
  const result = session.dispatch({
    type: 'RECRUIT_UNIT',
    species: 'goblin',
    id: unitId(id),
    name: id,
  });
  if (!result.ok) throw new Error(result.error.code);
  return result.value;
}

function makeMissionAvailable(session: GameSession): void {
  for (let elapsed = 0; elapsed < 120_000; elapsed += 5_000) session.advance(5_000);
}

describe('versioned save codec', () => {
  it('round-trips campaign progress, accumulators, equipment, and hauling state', () => {
    const session = GameSession.createNew(0xfedc_ba98);
    const state = mutableState(session);
    state.wallet.gold = 2_000;
    const worker = recruit(session, 'worker');
    const hauler = recruit(session, 'hauler');
    session.dispatch({ type: 'EQUIP_ITEM', unitId: worker, itemId: itemId('rusty-sword-1') });
    session.dispatch({
      type: 'ASSIGN_HAULER',
      unitId: hauler,
      fromId: buildingId('mine'),
      toId: buildingId('warehouse'),
    });
    session.advance(5_000);
    state.accumulators.mineOre = 0.75;

    const encoded = encodeSave(session.exportState());
    const decoded = decodeSave(encoded);
    expect(decoded).toEqual({ ok: true, value: session.exportState() });
    expect(decoded.ok && encodeSave(decoded.value)).toBe(encoded);
  });

  it('restores a canonical active run byte-for-byte through GameSession.fromSave', () => {
    const session = GameSession.createNew(42);
    mutableState(session).wallet.gold = 10_000;
    const fighters = ['a', 'b', 'c', 'd'].map((id) => recruit(session, id));
    session.dispatch({ type: 'EQUIP_ITEM', unitId: fighters[0]!, itemId: itemId('rusty-sword-1') });
    makeMissionAvailable(session);
    const formation = {
      unitIds: fighters,
      slots: [{ x: 1, y: 2 }, { x: 4, y: 2 }, { x: 1, y: 3 }, { x: 4, y: 3 }],
    };
    expect(session.dispatch({ type: 'START_MISSION', missionId: 'mission-1', formation }).ok).toBe(true);

    const encoded = encodeSave(session.exportState());
    const restored = GameSession.fromSave(encoded);
    expect(restored.ok).toBe(true);
    if (!restored.ok) return;
    expect(encodeSave(restored.value.exportState())).toBe(encoded);
    expect(restored.value.getSnapshot().mission.activeRun).toEqual(session.getSnapshot().mission.activeRun);
  });

  it('repairs safe counters and a missing accumulator', () => {
    const state = createInitialState();
    const raw = JSON.parse(encodeSave(state)) as Record<string, unknown>;
    (raw.wallet as { gold: number }).gold = -5;
    ((raw.buildings as Array<{ inventory: { ironOre: number } }>)[0]!).inventory.ironOre = -2;
    delete raw.accumulators;

    const decoded = decodeSave(JSON.stringify(raw));
    expect(decoded.ok).toBe(true);
    if (!decoded.ok) return;
    expect(decoded.value.wallet.gold).toBe(0);
    expect(decoded.value.buildings[0]?.inventory.ironOre).toBe(0);
    expect(decoded.value.accumulators.mineOre).toBe(0);
  });

  it.each([
    ['malformed JSON', '{'],
    ['missing structure', '{"schemaVersion":1,"wallet":{"gold":-5}}'],
    ['future schema', encodeSave({ ...createInitialState(), schemaVersion: 2 } as unknown as GameState)],
  ])('rejects %s', (_case, raw) => {
    expect(decodeSave(raw).ok).toBe(false);
  });

  it('distinguishes unknown schemas from invalid saves', () => {
    expect(decodeSave('{"schemaVersion":2}')).toEqual({
      ok: false,
      error: { code: 'UNSUPPORTED_SCHEMA' },
    });
    expect(decodeSave('{}')).toEqual({ ok: false, error: { code: 'INVALID_SAVE' } });
  });

  it.each([
    ['duplicate unit IDs', (state: GameState) => {
      state.wallet.gold = 1_000;
      const session = GameSession.fromSave(encodeSave(state));
      if (!session.ok) throw new Error('fixture');
      const id = recruit(session.value, 'same');
      mutableState(session.value).units.push(structuredClone(mutableState(session.value).units[0]!));
      mutableState(session.value).units[1]!.id = id;
      return session.value.exportState();
    }],
    ['dangling building assignment', (state: GameState) => {
      state.wallet.gold = 1_000;
      const session = GameSession.fromSave(encodeSave(state));
      if (!session.ok) throw new Error('fixture');
      recruit(session.value, 'worker');
      mutableState(session.value).units[0]!.assignment = { kind: 'work', buildingId: buildingId('missing') };
      return session.value.exportState();
    }],
    ['duplicate item ownership', (state: GameState) => {
      state.colonyInventory.itemIds.push(state.colonyInventory.itemIds[0]!);
      return state;
    }],
    ['invalid haul state', (state: GameState) => {
      state.wallet.gold = 1_000;
      const session = GameSession.fromSave(encodeSave(state));
      if (!session.ok) throw new Error('fixture');
      recruit(session.value, 'hauler');
      mutableState(session.value).units[0]!.assignment = {
        kind: 'haul',
        fromId: buildingId('mine'),
        toId: buildingId('mine'),
        resource: 'ironOre',
        carried: 0,
        phase: 'toSource',
        progressMs: 0,
      };
      return session.value.exportState();
    }],
  ] as const)('rejects %s', (_case, corrupt) => {
    expect(decodeSave(encodeSave(corrupt(createInitialState()))).ok).toBe(false);
  });

  it('rejects an active run whose report, assignments, or application history is inconsistent', () => {
    const session = GameSession.createNew(7);
    mutableState(session).wallet.gold = 1_000;
    const fighter = recruit(session, 'fighter');
    makeMissionAvailable(session);
    const started = session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: { unitIds: [fighter], slots: [{ x: 1, y: 2 }] },
    });
    if (!started.ok) throw new Error('fixture');

    const tamperedReport = session.exportState();
    tamperedReport.mission.activeRun!.combatReport.durationMs += 1;
    expect(decodeSave(encodeSave(tamperedReport)).ok).toBe(false);

    const appliedActive = session.exportState();
    appliedActive.mission.appliedRunIds.push(started.value);
    expect(decodeSave(encodeSave(appliedActive)).ok).toBe(false);

    const detachedUnit = session.exportState();
    detachedUnit.units[0]!.assignment = { kind: 'idle' };
    expect(decodeSave(encodeSave(detachedUnit)).ok).toBe(false);
  });

  it('rejects a self-consistent combat payload whose seed was not derived from the campaign', () => {
    const session = GameSession.createNew(7);
    mutableState(session).wallet.gold = 1_000;
    const fighter = recruit(session, 'fighter');
    makeMissionAvailable(session);
    const started = session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: { unitIds: [fighter], slots: [{ x: 1, y: 2 }] },
    });
    if (!started.ok) throw new Error('fixture');

    const state = session.exportState();
    const run = state.mission.activeRun!;
    run.combatSeed = (run.combatSeed + 1) >>> 0;
    run.combatInput.seed = run.combatSeed;
    run.combatReport = simulateCombat(run.combatInput);
    expect(decodeSave(encodeSave(state)).ok).toBe(false);
  });

  it('rejects run IDs and next sequences that do not match canonical derivation', () => {
    const session = GameSession.createNew(7);
    mutableState(session).wallet.gold = 1_000;
    const fighter = recruit(session, 'fighter');
    makeMissionAvailable(session);
    const started = session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: { unitIds: [fighter], slots: [{ x: 1, y: 2 }] },
    });
    if (!started.ok) throw new Error('fixture');

    const wrongId = session.exportState();
    wrongId.mission.activeRun!.runId = 'forged-run' as typeof started.value;
    wrongId.units[0]!.assignment = { kind: 'mission', runId: wrongId.mission.activeRun!.runId };
    expect(decodeSave(encodeSave(wrongId)).ok).toBe(false);

    const wrongSequence = session.exportState();
    wrongSequence.mission.nextRunSequence += 1;
    expect(decodeSave(encodeSave(wrongSequence)).ok).toBe(false);
  });

  it('round-trips a canonically settled inactive mission history', () => {
    const session = GameSession.createNew(17);
    mutableState(session).wallet.gold = 10_000;
    const fighters = ['a', 'b', 'c', 'd'].map((id) => {
      const result = session.dispatch({
        type: 'RECRUIT_UNIT', species: 'troll', id: unitId(id), name: id,
      });
      if (!result.ok) throw new Error('fixture');
      return result.value;
    });
    makeMissionAvailable(session);
    const started = session.dispatch({
      type: 'START_MISSION',
      missionId: 'mission-1',
      formation: {
        unitIds: fighters,
        slots: [{ x: 1, y: 2 }, { x: 4, y: 2 }, { x: 1, y: 3 }, { x: 4, y: 3 }],
      },
    });
    if (!started.ok) throw new Error('fixture');
    expect(session.dispatch({ type: 'RESOLVE_MISSION', runId: started.value }).ok).toBe(true);

    const state = session.exportState();
    expect(state.mission.activeRun).toBeNull();
    expect(state.mission.appliedRunIds).toEqual([missionRunId('mission-1-run-1')]);
    expect(state.mission.nextRunSequence).toBe(2);
    expect(decodeSave(encodeSave(state))).toEqual({ ok: true, value: state });
  });

  it.each([
    ['a forged prefix', 2, ['other-mission-run-1']],
    ['a future sequence', 2, ['mission-1-run-2']],
    ['a gap before nextRunSequence', 3, ['mission-1-run-1']],
    ['an out-of-order history', 3, ['mission-1-run-2', 'mission-1-run-1']],
  ] as const)('rejects inactive appliedRunIds with %s', (_case, nextRunSequence, runIds) => {
    const state = createInitialState();
    state.mission.nextRunSequence = nextRunSequence;
    state.mission.appliedRunIds = runIds.map(missionRunId);
    expect(decodeSave(encodeSave(state)).ok).toBe(false);
  });

  it('rejects negative and non-finite haul progress instead of repairing it', () => {
    const session = GameSession.createNew();
    mutableState(session).wallet.gold = 1_000;
    const hauler = recruit(session, 'hauler');
    session.dispatch({
      type: 'ASSIGN_HAULER',
      unitId: hauler,
      fromId: buildingId('mine'),
      toId: buildingId('warehouse'),
    });
    const state = session.exportState();
    const assignment = state.units[0]!.assignment;
    if (assignment.kind !== 'haul') throw new Error('fixture');
    assignment.progressMs = -1;
    expect(decodeSave(encodeSave(state)).ok).toBe(false);

    assignment.progressMs = 0;
    const nonFinite = encodeSave(state).replace('"progressMs":0', '"progressMs":1e999');
    expect(decodeSave(nonFinite).ok).toBe(false);
  });
});
