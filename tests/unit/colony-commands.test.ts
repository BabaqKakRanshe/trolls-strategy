import { describe, expect, it } from 'vitest';
import { createInitialState } from '../../src/domain/initialState';
import {
  assignHauler,
  assignWorkers,
  equipItem,
  recruitUnit,
  setIdle,
  unequipItem,
} from '../../src/domain/colony/commands';
import {
  buildingId,
  itemId,
  missionRunId,
  unitId,
  type EquipmentSlot,
  type GameState,
  type UnitId,
} from '../../src/domain/model';

function recruit(state: GameState, id: string, species: 'goblin' | 'troll' = 'goblin'): UnitId {
  const result = recruitUnit(state, species, unitId(id), id);
  if (!result.ok) {
    throw new Error(`Could not recruit fixture unit: ${result.error.code}`);
  }
  return result.value;
}

function stateWithGoblinCount(count: number): GameState {
  const state = createInitialState();
  state.wallet.gold = count * 40;
  for (let index = 1; index <= count; index += 1) {
    recruit(state, `u${index}`);
  }
  return state;
}

function expectRejectedWithoutMutation(
  state: GameState,
  command: () => unknown,
  expected: unknown,
): void {
  const before = structuredClone(state);
  expect(command()).toEqual(expected);
  expect(state).toEqual(before);
}

describe('recruitUnit', () => {
  it('recruits atomically and deducts the exact price', () => {
    const state = createInitialState();
    const result = recruitUnit(state, 'goblin', unitId('u1'), 'Grik');

    expect(result).toEqual({ ok: true, value: unitId('u1') });
    expect(state.wallet.gold).toBe(960);
    expect(state.units[0]).toMatchObject({
      id: unitId('u1'),
      species: 'goblin',
      name: 'Grik',
      health: 20,
      maxHealth: 20,
      assignment: { kind: 'idle' },
      equipment: { weaponId: null, armorId: null },
    });
  });

  it('rejects insufficient gold without mutation', () => {
    const state = createInitialState();
    state.wallet.gold = 39;

    expectRejectedWithoutMutation(
      state,
      () => recruitUnit(state, 'goblin', unitId('u1'), 'Grik'),
      { ok: false, error: { code: 'INSUFFICIENT_GOLD' } },
    );
  });

  it('rejects duplicate unit IDs before deducting gold', () => {
    const state = createInitialState();
    recruit(state, 'u1');

    expectRejectedWithoutMutation(
      state,
      () => recruitUnit(state, 'troll', unitId('u1'), 'Duplicate'),
      { ok: false, error: { code: 'DUPLICATE_ID' } },
    );
  });
});

describe('assignWorkers', () => {
  it('rejects an empty worker selection without mutation', () => {
    const state = createInitialState();

    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, [], buildingId('mine')),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });

  it('assigns idle units to the mine', () => {
    const state = stateWithGoblinCount(2);

    expect(assignWorkers(state, state.units.map((unit) => unit.id), buildingId('mine')))
      .toEqual({ ok: true, value: undefined });
    expect(state.units.map((unit) => unit.assignment)).toEqual([
      { kind: 'work', buildingId: buildingId('mine') },
      { kind: 'work', buildingId: buildingId('mine') },
    ]);
  });

  it('does not partially assign workers past mine capacity', () => {
    const state = stateWithGoblinCount(6);

    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, state.units.map((unit) => unit.id), buildingId('mine')),
      { ok: false, error: { code: 'CAPACITY_EXCEEDED' } },
    );
  });

  it('counts existing workers once and allows reassignment to the same mine', () => {
    const state = stateWithGoblinCount(5);
    const ids = state.units.map((unit) => unit.id);
    expect(assignWorkers(state, ids, buildingId('mine')).ok).toBe(true);

    expect(assignWorkers(state, ids.slice(0, 2), buildingId('mine')))
      .toEqual({ ok: true, value: undefined });
    expect(state.units).toHaveLength(5);
  });

  it('rejects duplicate input IDs and missing or non-mine buildings atomically', () => {
    const state = stateWithGoblinCount(1);
    const id = state.units[0]!.id;

    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, [id, id], buildingId('mine')),
      { ok: false, error: { code: 'DUPLICATE_ID' } },
    );
    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, [id], buildingId('missing')),
      { ok: false, error: { code: 'NOT_FOUND' } },
    );
    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, [id], buildingId('warehouse')),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });

  it('rejects missing and mission-assigned units without changing any worker', () => {
    const state = stateWithGoblinCount(2);
    state.units[1]!.assignment = { kind: 'mission', runId: missionRunId('run-1') };

    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, [state.units[0]!.id, unitId('missing')], buildingId('mine')),
      { ok: false, error: { code: 'NOT_FOUND' } },
    );
    expectRejectedWithoutMutation(
      state,
      () => assignWorkers(state, state.units.map((unit) => unit.id), buildingId('mine')),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });
});

describe('assignHauler and setIdle', () => {
  it('rejects an empty idle selection without mutation', () => {
    const state = createInitialState();

    expectRejectedWithoutMutation(
      state,
      () => setIdle(state, []),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });

  it('creates a fresh mine-to-warehouse hauling assignment', () => {
    const state = stateWithGoblinCount(1);
    const id = state.units[0]!.id;

    expect(assignHauler(state, id, buildingId('mine'), buildingId('warehouse')))
      .toEqual({ ok: true, value: undefined });
    expect(state.units[0]!.assignment).toEqual({
      kind: 'haul',
      fromId: buildingId('mine'),
      toId: buildingId('warehouse'),
      resource: 'ironOre',
      carried: 0,
      phase: 'toSource',
      progressMs: 0,
    });
  });

  it('rejects identical endpoints, missing buildings, and non-idle units atomically', () => {
    const state = stateWithGoblinCount(1);
    const id = state.units[0]!.id;

    expectRejectedWithoutMutation(
      state,
      () => assignHauler(state, id, buildingId('mine'), buildingId('mine')),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
    expectRejectedWithoutMutation(
      state,
      () => assignHauler(state, id, buildingId('missing'), buildingId('warehouse')),
      { ok: false, error: { code: 'NOT_FOUND' } },
    );

    state.units[0]!.assignment = { kind: 'work', buildingId: buildingId('mine') };
    expectRejectedWithoutMutation(
      state,
      () => assignHauler(state, id, buildingId('mine'), buildingId('warehouse')),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });

  it('sets workers and haulers idle, but rejects duplicates and mission units', () => {
    const state = stateWithGoblinCount(2);
    state.units[0]!.assignment = { kind: 'work', buildingId: buildingId('mine') };
    state.units[1]!.assignment = {
      kind: 'haul',
      fromId: buildingId('mine'),
      toId: buildingId('warehouse'),
      resource: 'ironOre',
      carried: 0,
      phase: 'loading',
      progressMs: 50,
    };

    expect(setIdle(state, state.units.map((unit) => unit.id)))
      .toEqual({ ok: true, value: undefined });
    expect(state.units.every((unit) => unit.assignment.kind === 'idle')).toBe(true);

    const firstId = state.units[0]!.id;
    expectRejectedWithoutMutation(
      state,
      () => setIdle(state, [firstId, firstId]),
      { ok: false, error: { code: 'DUPLICATE_ID' } },
    );

    state.units[0]!.assignment = { kind: 'mission', runId: missionRunId('run-1') };
    expectRejectedWithoutMutation(
      state,
      () => setIdle(state, [state.units[0]!.id, state.units[1]!.id]),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });

  it('does not idle any selected unit when a hauler is carrying ore', () => {
    const state = stateWithGoblinCount(2);
    state.units[0]!.assignment = {
      kind: 'haul',
      fromId: buildingId('mine'),
      toId: buildingId('warehouse'),
      resource: 'ironOre',
      carried: 4,
      phase: 'toDestination',
      progressMs: 250,
    };
    state.units[1]!.assignment = { kind: 'work', buildingId: buildingId('mine') };

    expectRejectedWithoutMutation(
      state,
      () => setIdle(state, state.units.map((unit) => unit.id)),
      { ok: false, error: { code: 'INVALID_ASSIGNMENT' } },
    );
  });
});

describe('equipment ownership', () => {
  it('moves an item from colony inventory into the matching unit slot', () => {
    const state = stateWithGoblinCount(1);
    const unit = state.units[0]!;
    const swordId = itemId('rusty-sword-1');

    expect(equipItem(state, unit.id, swordId)).toEqual({ ok: true, value: undefined });
    expect(unit.equipment.weaponId).toBe(swordId);
    expect(state.colonyInventory.itemIds).not.toContain(swordId);
  });

  it('returns a replaced item to colony inventory exactly once', () => {
    const state = stateWithGoblinCount(1);
    const unit = state.units[0]!;
    const firstSwordId = itemId('rusty-sword-1');
    const secondSwordId = itemId('rusty-sword-2');
    expect(equipItem(state, unit.id, firstSwordId).ok).toBe(true);

    expect(equipItem(state, unit.id, secondSwordId)).toEqual({ ok: true, value: undefined });
    expect(unit.equipment.weaponId).toBe(secondSwordId);
    expect(state.colonyInventory.itemIds.filter((id) => id === firstSwordId)).toHaveLength(1);
    expect(state.colonyInventory.itemIds).not.toContain(secondSwordId);
  });

  it('rejects an incompatible item definition and already-owned item without mutation', () => {
    const incompatibleState = stateWithGoblinCount(1);
    const sword = incompatibleState.items.find((item) => item.id === itemId('rusty-sword-1'))!;
    sword.slot = 'armor';
    expectRejectedWithoutMutation(
      incompatibleState,
      () => equipItem(incompatibleState, incompatibleState.units[0]!.id, sword.id),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );

    const ownedState = stateWithGoblinCount(2);
    const swordId = itemId('rusty-sword-1');
    expect(equipItem(ownedState, ownedState.units[0]!.id, swordId).ok).toBe(true);
    expectRejectedWithoutMutation(
      ownedState,
      () => equipItem(ownedState, ownedState.units[1]!.id, swordId),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );
  });

  it('rejects mission units and duplicate colony ownership without mutation', () => {
    const state = stateWithGoblinCount(1);
    const unit = state.units[0]!;
    const swordId = itemId('rusty-sword-1');
    unit.assignment = { kind: 'mission', runId: missionRunId('run-1') };
    expectRejectedWithoutMutation(
      state,
      () => equipItem(state, unit.id, swordId),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );

    unit.assignment = { kind: 'idle' };
    state.colonyInventory.itemIds.push(swordId);
    expectRejectedWithoutMutation(
      state,
      () => equipItem(state, unit.id, swordId),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );
  });

  it('does not replace a slot whose existing ownership is corrupt', () => {
    const state = stateWithGoblinCount(1);
    const unit = state.units[0]!;
    unit.equipment.weaponId = itemId('missing-item');

    expectRejectedWithoutMutation(
      state,
      () => equipItem(state, unit.id, itemId('rusty-sword-1')),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );
  });

  it.each<EquipmentSlot>(['weapon', 'armor'])(
    'unequips the %s item into colony inventory exactly once',
    (slot) => {
      const state = stateWithGoblinCount(1);
      const unit = state.units[0]!;
      const equippedId = slot === 'weapon'
        ? itemId('rusty-sword-1')
        : itemId('patched-armor-1');
      expect(equipItem(state, unit.id, equippedId).ok).toBe(true);

      expect(unequipItem(state, unit.id, slot)).toEqual({ ok: true, value: undefined });
      expect(unit.equipment[`${slot}Id`]).toBeNull();
      expect(state.colonyInventory.itemIds.filter((id) => id === equippedId)).toHaveLength(1);
    },
  );

  it('rejects empty, corrupt, and unavailable unequip operations without mutation', () => {
    const emptyState = stateWithGoblinCount(1);
    expectRejectedWithoutMutation(
      emptyState,
      () => unequipItem(emptyState, emptyState.units[0]!.id, 'weapon'),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );

    const corruptState = stateWithGoblinCount(1);
    const unit = corruptState.units[0]!;
    const swordId = itemId('rusty-sword-1');
    expect(equipItem(corruptState, unit.id, swordId).ok).toBe(true);
    corruptState.colonyInventory.itemIds.push(swordId);
    expectRejectedWithoutMutation(
      corruptState,
      () => unequipItem(corruptState, unit.id, 'weapon'),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );

    const missionState = stateWithGoblinCount(1);
    const missionUnit = missionState.units[0]!;
    expect(equipItem(missionState, missionUnit.id, swordId).ok).toBe(true);
    missionUnit.assignment = { kind: 'mission', runId: missionRunId('run-1') };
    expectRejectedWithoutMutation(
      missionState,
      () => unequipItem(missionState, missionUnit.id, 'weapon'),
      { ok: false, error: { code: 'INVALID_EQUIPMENT' } },
    );
  });
});
