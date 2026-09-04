import { describe, expect, it } from 'vitest';
import type { UnitSpecies } from '../../src/content/catalog';
import { createInitialState } from '../../src/domain/initialState';
import {
  assignHauler,
  assignWorkers,
  recruitUnit,
  setIdle,
} from '../../src/domain/colony/commands';
import { tickColony } from '../../src/domain/colony/tickColony';
import {
  buildingId,
  unitId,
  type BuildingKind,
  type GameState,
} from '../../src/domain/model';

function inventoryOf(state: GameState, kind: BuildingKind) {
  const building = state.buildings.find((candidate) => candidate.kind === kind);
  if (!building) {
    throw new Error(`Missing ${kind} fixture building`);
  }
  return building.inventory;
}

function recruit(state: GameState, species: UnitSpecies, suffix: string) {
  const result = recruitUnit(state, species, unitId(`${species}-${suffix}`), suffix);
  if (!result.ok) {
    throw new Error(`Could not recruit fixture unit: ${result.error.code}`);
  }
  return result.value;
}

function stateWithAssignedWorkers(species: readonly UnitSpecies[]): GameState {
  const state = createInitialState();
  state.wallet.gold = 10_000;
  const workerIds = species.map((unitSpecies, index) => (
    recruit(state, unitSpecies, `worker-${index}`)
  ));
  const result = assignWorkers(state, workerIds, buildingId('mine'));
  if (!result.ok) {
    throw new Error(`Could not assign fixture workers: ${result.error.code}`);
  }
  return state;
}

function stateWithHaulersAndMineOre(
  species: readonly UnitSpecies[],
  mineOre: number,
): GameState {
  const state = createInitialState();
  state.wallet.gold = 10_000;
  inventoryOf(state, 'mine').ironOre = mineOre;
  species.forEach((unitSpecies, index) => {
    const id = recruit(state, unitSpecies, `hauler-${index}`);
    const result = assignHauler(
      state,
      id,
      buildingId('mine'),
      buildingId('warehouse'),
    );
    if (!result.ok) {
      throw new Error(`Could not assign fixture hauler: ${result.error.code}`);
    }
  });
  return state;
}

function totalIronOre(state: GameState): number {
  const stored = state.buildings.reduce(
    (total, building) => total + building.inventory.ironOre,
    0,
  );
  const carried = state.units.reduce((total, unit) => (
    unit.assignment.kind === 'haul' ? total + unit.assignment.carried : total
  ), 0);
  return stored + carried;
}

describe('tickColony mining', () => {
  it('mines ore from total worker strength', () => {
    const state = stateWithAssignedWorkers(['goblin', 'troll']);

    tickColony(state, 10_000);

    expect(inventoryOf(state, 'mine').ironOre).toBe(12);
    expect(state.accumulators.mineOre).toBe(0);
    expect(state.simulationTimeMs).toBe(10_000);
  });

  it('tracks fractional production between ticks', () => {
    const state = stateWithAssignedWorkers(['goblin']);

    tickColony(state, 250);
    expect(inventoryOf(state, 'mine').ironOre).toBe(0);
    expect(state.accumulators.mineOre).toBeCloseTo(0.075);

    tickColony(state, 3_250);
    expect(inventoryOf(state, 'mine').ironOre).toBe(1);
    expect(state.accumulators.mineOre).toBeCloseTo(0.05);
  });

  it.each([
    { species: 'goblin' as const, ticks: 120 },
    { species: 'troll' as const, ticks: 40 },
  ])('lands exactly on the integer boundary for $species', ({ species, ticks }) => {
    const state = stateWithAssignedWorkers([species]);

    for (let index = 0; index < ticks; index += 1) {
      tickColony(state, 250);
    }

    expect(inventoryOf(state, 'mine').ironOre).toBe(9);
    expect(state.accumulators.mineOre).toBe(0);
  });

  it('does not accumulate floating-point drift during a long run', () => {
    const state = stateWithAssignedWorkers(['goblin']);

    for (let index = 0; index < 1_200; index += 1) {
      tickColony(state, 250);
    }

    expect(inventoryOf(state, 'mine').ironOre).toBe(90);
    expect(state.accumulators.mineOre).toBe(0);
  });

  it('never exceeds the mine capacity or banks production while full', () => {
    const state = stateWithAssignedWorkers(['troll']);
    inventoryOf(state, 'mine').ironOre = 99;
    state.accumulators.mineOre = 0.75;

    tickColony(state, 10_000);
    expect(inventoryOf(state, 'mine').ironOre).toBe(100);
    expect(state.accumulators.mineOre).toBe(0);

    inventoryOf(state, 'mine').ironOre -= 1;
    tickColony(state, 250);
    expect(inventoryOf(state, 'mine').ironOre).toBe(99);
    expect(state.accumulators.mineOre).toBeCloseTo(0.225);
  });
});

describe('tickColony hauling', () => {
  it('hauls only the unit carry capacity and preserves resources', () => {
    const state = stateWithHaulersAndMineOre(['goblin'], 25);
    const totalBefore = totalIronOre(state);

    tickColony(state, 20_000);

    expect(inventoryOf(state, 'warehouse').ironOre).toBeGreaterThan(0);
    expect(totalIronOre(state)).toBe(totalBefore);
    expect(state.units[0]!.assignment.kind).toBe('haul');
    if (state.units[0]!.assignment.kind === 'haul') {
      expect(state.units[0]!.assignment.carried).toBeLessThanOrEqual(10);
    }
  });

  it('waits at an empty source without creating resources', () => {
    const state = stateWithHaulersAndMineOre(['goblin'], 0);

    tickColony(state, 20_000);

    expect(totalIronOre(state)).toBe(0);
    expect(inventoryOf(state, 'warehouse').ironOre).toBe(0);
    expect(state.units[0]!.assignment).toMatchObject({
      kind: 'haul',
      phase: 'loading',
      carried: 0,
    });
  });

  it('respects warehouse capacity across multiple haulers', () => {
    const state = stateWithHaulersAndMineOre(['goblin', 'goblin'], 25);
    inventoryOf(state, 'warehouse').ironOre = 495;
    const totalBefore = totalIronOre(state);

    tickColony(state, 20_000);

    expect(inventoryOf(state, 'warehouse').ironOre).toBe(500);
    expect(inventoryOf(state, 'mine').ironOre).toBe(20);
    expect(totalIronOre(state)).toBe(totalBefore);
  });

  it('does not load cargo while the warehouse is full', () => {
    const state = stateWithHaulersAndMineOre(['troll'], 25);
    inventoryOf(state, 'warehouse').ironOre = 500;
    const totalBefore = totalIronOre(state);

    tickColony(state, 20_000);

    expect(inventoryOf(state, 'mine').ironOre).toBe(25);
    expect(inventoryOf(state, 'warehouse').ironOre).toBe(500);
    expect(totalIronOre(state)).toBe(totalBefore);
  });

  it('reserves inbound cargo against mine production and eventually unloads it', () => {
    const state = stateWithAssignedWorkers(['goblin']);
    state.wallet.gold = 10_000;
    const haulerId = recruit(state, 'goblin', 'reverse-hauler');
    inventoryOf(state, 'mine').ironOre = 90;
    inventoryOf(state, 'warehouse').ironOre = 10;
    const assignmentResult = assignHauler(
      state,
      haulerId,
      buildingId('warehouse'),
      buildingId('mine'),
    );
    if (!assignmentResult.ok) {
      throw new Error(`Could not assign fixture hauler: ${assignmentResult.error.code}`);
    }

    tickColony(state, 1_500);
    const hauler = state.units.find((unit) => unit.id === haulerId)!;
    expect(hauler.assignment).toMatchObject({
      kind: 'haul',
      phase: 'toDestination',
      carried: 9,
      progressMs: 50,
    });
    expect(
      inventoryOf(state, 'mine').ironOre
        + state.accumulators.mineOre
        + (hauler.assignment.kind === 'haul' ? hauler.assignment.carried : 0),
    ).toBeLessThanOrEqual(100);

    const beforeCancellation = structuredClone(state);
    expect(setIdle(state, [haulerId])).toEqual({
      ok: false,
      error: { code: 'INVALID_ASSIGNMENT' },
    });
    expect(state).toEqual(beforeCancellation);

    tickColony(state, 2_000);
    expect(inventoryOf(state, 'mine').ironOre).toBe(100);
    expect(state.accumulators.mineOre).toBe(0);
    expect(inventoryOf(state, 'warehouse').ironOre).toBe(1);
    expect(hauler.assignment).toMatchObject({
      kind: 'haul',
      phase: 'toSource',
      carried: 0,
      progressMs: 600,
    });
  });
});

describe('tickColony fixed-step contract', () => {
  it('is equivalent for one chunk and repeated 250 ms ticks', () => {
    const chunked = stateWithAssignedWorkers(['goblin', 'troll']);
    chunked.wallet.gold = 10_000;
    const haulerId = recruit(chunked, 'goblin', 'hauler');
    const assignment = assignHauler(
      chunked,
      haulerId,
      buildingId('mine'),
      buildingId('warehouse'),
    );
    if (!assignment.ok) {
      throw new Error(`Could not assign fixture hauler: ${assignment.error.code}`);
    }
    const single = structuredClone(chunked);

    for (let index = 0; index < 40; index += 1) {
      tickColony(chunked, 250);
    }
    tickColony(single, 10_000);

    expect(chunked).toEqual(single);
  });

  it.each([0, -250, 125, 250.5, Number.NaN, Number.POSITIVE_INFINITY])(
    'rejects invalid delta %s before mutating state',
    (deltaMs) => {
      const state = stateWithAssignedWorkers(['goblin']);
      const before = structuredClone(state);

      expect(() => tickColony(state, deltaMs)).toThrow(RangeError);
      expect(state).toEqual(before);
    },
  );
});
