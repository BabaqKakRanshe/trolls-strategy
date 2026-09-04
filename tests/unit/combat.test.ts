import { describe, expect, it } from 'vitest';
import { UNIT_CATALOG } from '../../src/content/catalog';
import { simulateCombat } from '../../src/domain/combat/simulateCombat';
import {
  combatantId,
  unitId,
  type CombatInput,
  type CombatSlot,
  type BaseCombatantSpec,
  type CombatantSpec,
} from '../../src/domain/model';

type StatOverrides = Partial<Omit<BaseCombatantSpec, 'id'>>;

const combatant = (
  id: string,
  side: 'player' | 'enemy',
  overrides: StatOverrides = {},
): CombatantSpec => {
  const stats = {
    id: combatantId(id),
    maxHealth: 20,
    speed: 5,
    damage: 2,
    armor: 1,
    attackIntervalMs: 2_000,
    ...overrides,
  };
  return side === 'player'
    ? { ...stats, unitId: unitId(id), side }
    : { ...stats, unitId: null, side };
};

const slot = (combatantSpec: CombatantSpec, x: number, y: number): CombatSlot => ({
  combatant: combatantSpec,
  x,
  y,
});

const inputOf = (
  playerSlots: CombatSlot[],
  enemySlots: CombatSlot[],
  overrides: Partial<CombatInput> = {},
): CombatInput => ({
  seed: 12_345,
  maxDurationMs: 120_000,
  playerSlots,
  enemySlots,
  ...overrides,
});

const missionOneCombatInput = (seed: number): CombatInput => {
  const troll = UNIT_CATALOG.troll;
  const goblin = UNIT_CATALOG.goblin;
  const players = [0, 1, 2, 3].map((index) =>
    slot(
      combatant(`troll-${index + 1}`, 'player', {
        maxHealth: troll.maxHealth,
        speed: troll.speed,
        damage: troll.damage,
        armor: troll.armor,
        attackIntervalMs: troll.attackIntervalMs,
      }),
      0,
      index,
    ),
  );
  const enemies = [0, 1, 2, 3].map((index) =>
    slot(combatant(`enemy-${index + 1}`, 'enemy', {
      maxHealth: goblin.maxHealth,
      speed: goblin.speed,
      damage: goblin.damage,
      armor: goblin.armor,
      attackIntervalMs: goblin.attackIntervalMs,
    }), 5, index),
  );

  return inputOf(players, enemies, { seed });
};

const frameAt = (input: CombatInput, timeMs: number) => {
  const frame = simulateCombat(input).frames.find((candidate) => candidate.timeMs === timeMs);
  expect(frame).toBeDefined();
  return frame!;
};

describe('simulateCombat', () => {
  it('resolves the same formation identically for the same seed', () => {
    const input = missionOneCombatInput(12_345);
    expect(simulateCombat(input)).toEqual(simulateCombat(input));
  });

  it('matches the mission-one golden outcome', () => {
    const report = simulateCombat(missionOneCombatInput(12_345));
    expect({
      winner: report.winner,
      durationMs: report.durationMs,
      dead: report.deadPlayerUnitIds,
    }).toEqual({
      winner: 'player',
      durationMs: 8_400,
      dead: [],
    });
  });

  it('applies the armor damage floor and keeps player and enemy casualties separate', () => {
    const player = combatant('player', 'player', { maxHealth: 2, damage: 1, armor: 50 });
    const enemy = combatant('enemy', 'enemy', { maxHealth: 1, damage: 1, armor: 50 });
    const report = simulateCombat(inputOf([slot(player, 0, 0)], [slot(enemy, 1, 0)]));

    expect(report.winner).toBe('player');
    expect(report.durationMs).toBe(100);
    expect(report.deadPlayerUnitIds).toEqual([]);
    expect(report.deadEnemyCombatantIds).toEqual([combatantId('enemy')]);
  });

  it('honors attack cooldowns', () => {
    const player = combatant('player', 'player', {
      maxHealth: 100,
      damage: 2,
      armor: 0,
      attackIntervalMs: 500,
    });
    const enemy = combatant('enemy', 'enemy', {
      maxHealth: 100,
      damage: 1,
      armor: 0,
      attackIntervalMs: 10_000,
    });
    const input = inputOf([slot(player, 0, 0)], [slot(enemy, 1, 0)], { maxDurationMs: 700 });

    expect(frameAt(input, 200).combatants.find(({ id }) => id === enemy.id)?.health).toBe(98);
    expect(frameAt(input, 600).combatants.find(({ id }) => id === enemy.id)?.health).toBe(96);
  });

  it('uses the seed to break equal-distance target ties deterministically', () => {
    const attacker = combatant('attacker', 'player', { damage: 5, attackIntervalMs: 10_000 });
    const left = combatant('enemy-a', 'enemy', { damage: 1 });
    const right = combatant('enemy-b', 'enemy', { damage: 1 });
    const makeInput = (seed: number) =>
      inputOf(
        [slot(attacker, 1, 1)],
        [slot(left, 0, 1), slot(right, 2, 1)],
        { seed, maxDurationMs: 200 },
      );

    const attackedEnemy = (seed: number) => frameAt(makeInput(seed), 200).combatants.find(
      ({ id, health }) => id !== attacker.id && health < 20,
    )?.id;
    const first = attackedEnemy(1);
    const repeat = attackedEnemy(1);
    expect(first).toEqual(repeat);
    expect(first).not.toEqual(attackedEnemy(5));
  });

  it('returns an immediate enemy win for an empty player formation', () => {
    const report = simulateCombat(
      inputOf([], [slot(combatant('enemy', 'enemy'), 2, 0)]),
    );

    expect(report.winner).toBe('enemy');
    expect(report.durationMs).toBe(0);
    expect(report.frames).toHaveLength(1);
  });

  it('uses remaining-health ratio as the timeout tiebreak', () => {
    const player = combatant('player', 'player', { maxHealth: 100, damage: 1, armor: 10 });
    const enemy = combatant('enemy', 'enemy', { maxHealth: 10, damage: 1, armor: 10 });
    const report = simulateCombat(
      inputOf([slot(player, 0, 0)], [slot(enemy, 1, 0)], { maxDurationMs: 100 }),
    );

    expect(report.winner).toBe('player');
    expect(report.durationMs).toBe(100);
  });

  it('returns a draw for equal remaining-health ratios at timeout', () => {
    const player = combatant('player', 'player', { maxHealth: 10, damage: 1, armor: 10 });
    const enemy = combatant('enemy', 'enemy', { maxHealth: 10, damage: 1, armor: 10 });
    const report = simulateCombat(
      inputOf([slot(player, 0, 0)], [slot(enemy, 1, 0)], { maxDurationMs: 100 }),
    );

    expect(report.winner).toBe('draw');
  });

  it('captures frames every 200 ms and includes a terminal off-interval frame', () => {
    const player = combatant('player', 'player', { damage: 51, speed: 10 });
    const enemy = combatant('enemy', 'enemy', { maxHealth: 50, speed: 0 });
    const report = simulateCombat(
      inputOf([slot(player, 0, 0)], [slot(enemy, 3, 0)]),
    );

    expect(report.frames.map(({ timeMs }) => timeMs)).toEqual([0, 200, 300]);
  });

  it('keeps a rear combatant behind an occupied front cell', () => {
    const rear = combatant('a-rear', 'player', { speed: 10 });
    const front = combatant('b-front', 'player', { speed: 10 });
    const enemy = combatant('z-enemy', 'enemy', { speed: 0, maxHealth: 100 });
    const frame = frameAt(
      inputOf(
        [slot(rear, 0, 0), slot(front, 1, 0)],
        [slot(enemy, 3, 0)],
        { maxDurationMs: 100 },
      ),
      100,
    );

    expect(frame.combatants.find(({ id }) => id === rear.id)).toMatchObject({ x: 0, y: 0 });
    expect(frame.combatants.find(({ id }) => id === front.id)).toMatchObject({ x: 2, y: 0 });
  });

  it('uses the orthogonal fallback when the primary movement cell is reserved', () => {
    const mover = combatant('a-mover', 'player', { speed: 10 });
    const blocker = combatant('b-blocker', 'player', { speed: 0 });
    const enemy = combatant('z-enemy', 'enemy', { speed: 0, maxHealth: 100 });
    const frame = frameAt(
      inputOf(
        [slot(mover, 0, 1), slot(blocker, 1, 1)],
        [slot(enemy, 2, 2)],
        { maxDurationMs: 100 },
      ),
      100,
    );

    expect(frame.combatants.find(({ id }) => id === mover.id)).toMatchObject({ x: 0, y: 2 });
  });

  it('does not let opposing combatants cross or share a living cell', () => {
    const player = combatant('a-player', 'player', { speed: 10, maxHealth: 100 });
    const enemy = combatant('z-enemy', 'enemy', { speed: 10, maxHealth: 100 });
    const report = simulateCombat(
      inputOf([slot(player, 0, 0)], [slot(enemy, 2, 0)], { maxDurationMs: 200 }),
    );

    for (const frame of report.frames) {
      const livingCells = frame.combatants
        .filter(({ health }) => health > 0)
        .map(({ x, y }) => `${x}:${y}`);
      expect(new Set(livingCells).size).toBe(livingCells.length);
    }
    expect(report.frames.at(-1)?.combatants.find(({ id }) => id === player.id)).toMatchObject({ x: 1 });
    expect(report.frames.at(-1)?.combatants.find(({ id }) => id === enemy.id)).toMatchObject({ x: 2 });
  });

  it.each([
    ['wrong player side', (input: CombatInput) => {
      input.playerSlots[0]!.combatant = combatant('replacement', 'enemy');
    }],
    ['wrong enemy side', (input: CombatInput) => {
      input.enemySlots[0]!.combatant = combatant('replacement', 'player');
    }],
    ['duplicate combatant id', (input: CombatInput) => {
      input.enemySlots[0]!.combatant.id = input.playerSlots[0]!.combatant.id;
    }],
    ['duplicate player unit id', (input: CombatInput) => {
      input.playerSlots[1]!.combatant.unitId = input.playerSlots[0]!.combatant.unitId;
    }],
    ['duplicate starting cell', (input: CombatInput) => {
      input.enemySlots[0]!.x = input.playerSlots[0]!.x;
      input.enemySlots[0]!.y = input.playerSlots[0]!.y;
    }],
    ['non-integer coordinate', (input: CombatInput) => {
      input.playerSlots[0]!.x = 0.5;
    }],
    ['out-of-bounds coordinate', (input: CombatInput) => {
      input.playerSlots[0]!.x = 6;
    }],
    ['non-positive health', (input: CombatInput) => {
      input.playerSlots[0]!.combatant.maxHealth = 0;
    }],
    ['negative speed', (input: CombatInput) => {
      input.playerSlots[0]!.combatant.speed = -1;
    }],
    ['non-finite damage', (input: CombatInput) => {
      input.playerSlots[0]!.combatant.damage = Number.NaN;
    }],
    ['negative armor', (input: CombatInput) => {
      input.playerSlots[0]!.combatant.armor = -1;
    }],
    ['non-positive attack interval', (input: CombatInput) => {
      input.playerSlots[0]!.combatant.attackIntervalMs = 0;
    }],
    ['invalid duration', (input: CombatInput) => {
      input.maxDurationMs = 101;
    }],
    ['duration above cap', (input: CombatInput) => {
      input.maxDurationMs = 120_100;
    }],
    ['invalid seed', (input: CombatInput) => {
      input.seed = Number.POSITIVE_INFINITY;
    }],
    ['seed outside uint32 range', (input: CombatInput) => {
      input.seed = 0x1_0000_0000;
    }],
    ['empty combatant id', (input: CombatInput) => {
      input.playerSlots[0]!.combatant.id = combatantId('');
    }],
    ['empty player unit id', (input: CombatInput) => {
      const combatant = input.playerSlots[0]!.combatant;
      if (combatant.side === 'player') {
        combatant.unitId = unitId('');
      }
    }],
  ] as const)('rejects %s without mutation', (_name, invalidate) => {
    const input = missionOneCombatInput(12_345);
    invalidate(input);
    const before = structuredClone(input);

    expect(() => simulateCombat(input)).toThrow(RangeError);
    expect(input).toEqual(before);
  });

  it('does not mutate its input', () => {
    const input = missionOneCombatInput(12_345);
    const before = structuredClone(input);

    simulateCombat(input);

    expect(input).toEqual(before);
  });
});
