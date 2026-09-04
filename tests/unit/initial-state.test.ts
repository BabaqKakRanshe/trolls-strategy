import { describe, expect, it } from 'vitest';
import { createInitialState } from '../../src/domain/initialState';
import {
  combatantId,
  missionRunId,
  unitId,
  type ActiveMissionRun,
} from '../../src/domain/model';

describe('createInitialState', () => {
  it('creates the playable colony baseline', () => {
    const state = createInitialState();

    expect(state.schemaVersion).toBe(1);
    expect(state.campaignSeed).toBe(createInitialState().campaignSeed);
    expect(state.simulationTimeMs).toBe(0);
    expect(state.accumulators).toEqual({ mineOre: 0 });
    expect(state.wallet.gold).toBe(1_000);
    expect(state.units).toEqual([]);
    expect(state.buildings.map((building) => building.kind)).toEqual([
      'town-hall',
      'mine',
      'warehouse',
      'market',
    ]);
    expect(state.buildings.every((building) => building.inventory.ironOre === 0)).toBe(true);
    expect(state.items).toHaveLength(8);
    expect(state.colonyInventory.itemIds).toHaveLength(8);
    expect(state.items.filter((item) => item.kind === 'rusty-sword')).toHaveLength(4);
    expect(state.items.filter((item) => item.kind === 'patched-armor')).toHaveLength(4);
    expect(new Set(state.colonyInventory.itemIds).size).toBe(8);
    expect(state.mission).toEqual({
      nextMissionAtMs: 120_000,
      nextRunSequence: 1,
      appliedRunIds: [],
      clearedMissionIds: [],
      activeRun: null,
    });
  });

  it('does not share mutable references between new games', () => {
    const first = createInitialState();
    const second = createInitialState();

    first.wallet.gold = 0;
    first.accumulators.mineOre = 0.75;
    first.buildings[0]!.inventory.ironOre = 10;
    first.colonyInventory.itemIds.pop();
    first.items[0]!.damageBonus = 99;
    first.mission.appliedRunIds.push(missionRunId('run-1'));

    expect(second.wallet.gold).toBe(1_000);
    expect(second.accumulators.mineOre).toBe(0);
    expect(second.buildings[0]!.inventory.ironOre).toBe(0);
    expect(second.colonyInventory.itemIds).toHaveLength(8);
    expect(second.items[0]!.damageBonus).toBe(1);
    expect(second.mission.appliedRunIds).toEqual([]);
  });

  it('stores a normalized campaign seed and uses a deterministic default', () => {
    expect(createInitialState(0x1_0000_0001).campaignSeed).toBe(1);
    expect(createInitialState(-1).campaignSeed).toBe(0xffff_ffff);
    expect(createInitialState().campaignSeed).toBe(createInitialState().campaignSeed);
  });

  it('supports a fully serializable active mission while starting with none', () => {
    const state = createInitialState();
    const playerId = combatantId('player-u1');
    const enemyId = combatantId('enemy-1');
    const activeRun: ActiveMissionRun = {
      runId: missionRunId('run-1'),
      missionId: 'mission-1',
      formation: {
        unitIds: [unitId('u1')],
        slots: [{ x: 1, y: 2 }],
      },
      combatSeed: 42,
      combatInput: {
        seed: 42,
        maxDurationMs: 120_000,
        playerSlots: [
          {
            x: 1,
            y: 2,
            combatant: {
              id: playerId,
              unitId: unitId('u1'),
              side: 'player',
              maxHealth: 20,
              speed: 5,
              damage: 3,
              armor: 1,
              attackIntervalMs: 2_000,
            },
          },
        ],
        enemySlots: [
          {
            x: 1,
            y: 1,
            combatant: {
              id: enemyId,
              unitId: null,
              side: 'enemy',
              maxHealth: 20,
              speed: 5,
              damage: 2,
              armor: 1,
              attackIntervalMs: 2_000,
            },
          },
        ],
      },
      combatReport: {
        winner: 'player',
        durationMs: 1_000,
        frames: [
          {
            timeMs: 0,
            combatants: [
              { id: playerId, x: 1, y: 2, health: 20 },
              { id: enemyId, x: 1, y: 1, health: 20 },
            ],
          },
        ],
        deadPlayerUnitIds: [],
        deadEnemyCombatantIds: [enemyId],
      },
      status: 'active',
    };

    expect(state.mission.activeRun).toBeNull();
    state.mission.activeRun = activeRun;
    expect(structuredClone(state).mission.activeRun).toEqual(activeRun);
  });
});
