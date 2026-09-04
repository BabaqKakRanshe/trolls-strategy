import type {
  CombatFrame,
  CombatInput,
  CombatReport,
  CombatSide,
  CombatSlot,
  CombatWinner,
  CombatantId,
  UnitId,
} from '../model';

const STEP_MS = 100;
const FRAME_INTERVAL_MS = 200;
const MAX_COMBAT_DURATION_MS = 120_000;
const GRID_WIDTH = 6;
const GRID_HEIGHT = 4;

interface MutableCombatant {
  id: CombatantId;
  unitId: UnitId | null;
  side: CombatSide;
  x: number;
  y: number;
  health: number;
  maxHealth: number;
  speed: number;
  damage: number;
  armor: number;
  attackIntervalMs: number;
  attackCooldownMs: number;
  movementAccumulatorMs: number;
}

type RandomSource = () => number;

export type {
  CombatFrame,
  CombatInput,
  CombatReport,
  CombatantSpec,
  FormationSlot,
} from '../model';

export function simulateCombat(input: CombatInput): CombatReport {
  validateInput(input);
  const combatants = createCombatants(input);
  const random = createRandomSource(input.seed);
  const frames = [captureFrame(combatants, 0)];
  const durationLimitMs = input.maxDurationMs;
  let durationMs = 0;

  while (!hasFinished(combatants) && durationMs < durationLimitMs) {
    durationMs += STEP_MS;
    advanceCombat(combatants, random);

    if (durationMs % FRAME_INTERVAL_MS === 0) {
      frames.push(captureFrame(combatants, durationMs));
    }
  }

  if (frames.at(-1)?.timeMs !== durationMs) {
    frames.push(captureFrame(combatants, durationMs));
  }

  return createReport(combatants, frames, durationMs);
}

function createCombatants(input: CombatInput): MutableCombatant[] {
  return [...input.playerSlots, ...input.enemySlots]
    .map(createCombatant)
    .sort((left, right) => compareCodeUnits(left.id, right.id));
}

function createCombatant(slot: CombatSlot): MutableCombatant {
  return {
    ...slot.combatant,
    x: slot.x,
    y: slot.y,
    health: slot.combatant.maxHealth,
    attackCooldownMs: 0,
    movementAccumulatorMs: 0,
  };
}

function advanceCombat(combatants: MutableCombatant[], random: RandomSource): void {
  const occupiedCells = new Set(
    combatants.filter(isAlive).map(({ x, y }) => cellKey(x, y)),
  );
  for (const combatant of combatants) {
    if (!isAlive(combatant)) {
      continue;
    }

    combatant.attackCooldownMs = Math.max(0, combatant.attackCooldownMs - STEP_MS);
    const target = chooseTarget(combatant, combatants, random);
    if (target === undefined) {
      continue;
    }

    if (manhattanDistance(combatant, target) <= 1) {
      if (combatant.attackCooldownMs === 0) {
        attack(combatant, target);
        if (!isAlive(target)) {
          occupiedCells.delete(cellKey(target.x, target.y));
        }
      }
      continue;
    }

    advanceMovement(combatant, target, occupiedCells);
  }
}

function chooseTarget(
  combatant: MutableCombatant,
  combatants: MutableCombatant[],
  random: RandomSource,
): MutableCombatant | undefined {
  const candidates = combatants.filter(
    (candidate) => candidate.side !== combatant.side && isAlive(candidate),
  );
  const nearestDistance = Math.min(
    ...candidates.map((candidate) => manhattanDistance(combatant, candidate)),
  );
  const nearestCandidates = candidates.filter(
    (candidate) => manhattanDistance(combatant, candidate) === nearestDistance,
  );
  if (nearestCandidates.length === 1) {
    return nearestCandidates[0];
  }

  return nearestCandidates
    .map((candidate) => ({ candidate, tieValue: random() }))
    .sort((left, right) =>
      left.tieValue === right.tieValue
        ? compareCodeUnits(left.candidate.id, right.candidate.id)
        : left.tieValue - right.tieValue,
    )[0]?.candidate;
}

function advanceMovement(
  combatant: MutableCombatant,
  target: MutableCombatant,
  occupiedCells: Set<string>,
): void {
  if (combatant.speed <= 0) {
    return;
  }

  combatant.movementAccumulatorMs += STEP_MS;
  const movementIntervalMs = 1_000 / combatant.speed;
  if (combatant.movementAccumulatorMs < movementIntervalMs) {
    return;
  }

  combatant.movementAccumulatorMs -= movementIntervalMs;
  const deltaX = target.x - combatant.x;
  const deltaY = target.y - combatant.y;
  const horizontal = { x: combatant.x + Math.sign(deltaX), y: combatant.y };
  const vertical = { x: combatant.x, y: combatant.y + Math.sign(deltaY) };
  const horizontalFirst = Math.abs(deltaX) >= Math.abs(deltaY);
  const candidates = horizontalFirst
    ? [deltaX === 0 ? null : horizontal, deltaY === 0 ? null : vertical]
    : [deltaY === 0 ? null : vertical, deltaX === 0 ? null : horizontal];
  const destination = candidates.find(
    (candidate) => candidate !== null && !occupiedCells.has(cellKey(candidate.x, candidate.y)),
  );
  if (destination !== undefined && destination !== null) {
    occupiedCells.delete(cellKey(combatant.x, combatant.y));
    combatant.x = destination.x;
    combatant.y = destination.y;
    occupiedCells.add(cellKey(combatant.x, combatant.y));
  }
}

function attack(attacker: MutableCombatant, target: MutableCombatant): void {
  target.health = Math.max(0, target.health - Math.max(1, attacker.damage - target.armor));
  attacker.attackCooldownMs = Math.max(STEP_MS, attacker.attackIntervalMs);
}

function manhattanDistance(left: MutableCombatant, right: MutableCombatant): number {
  return Math.abs(left.x - right.x) + Math.abs(left.y - right.y);
}

function isAlive(combatant: MutableCombatant): boolean {
  return combatant.health > 0;
}

function hasFinished(combatants: MutableCombatant[]): boolean {
  return !hasLivingSide(combatants, 'player') || !hasLivingSide(combatants, 'enemy');
}

function hasLivingSide(combatants: MutableCombatant[], side: CombatSide): boolean {
  return combatants.some((combatant) => combatant.side === side && isAlive(combatant));
}

function captureFrame(combatants: MutableCombatant[], timeMs: number): CombatFrame {
  return {
    timeMs,
    combatants: combatants.map(({ id, x, y, health }) => ({ id, x, y, health })),
  };
}

function createReport(
  combatants: MutableCombatant[],
  frames: CombatFrame[],
  durationMs: number,
): CombatReport {
  return {
    winner: determineWinner(combatants),
    durationMs,
    frames,
    deadPlayerUnitIds: combatants.flatMap((combatant) =>
      combatant.side === 'player' && !isAlive(combatant) && combatant.unitId !== null
        ? [combatant.unitId]
        : [],
    ),
    deadEnemyCombatantIds: combatants.flatMap((combatant) =>
      combatant.side === 'enemy' && !isAlive(combatant) ? [combatant.id] : [],
    ),
  };
}

function determineWinner(combatants: MutableCombatant[]): CombatWinner {
  const playerAlive = hasLivingSide(combatants, 'player');
  const enemyAlive = hasLivingSide(combatants, 'enemy');
  if (playerAlive !== enemyAlive) {
    return playerAlive ? 'player' : 'enemy';
  }

  const playerHealthRatio = remainingHealthRatio(combatants, 'player');
  const enemyHealthRatio = remainingHealthRatio(combatants, 'enemy');
  if (playerHealthRatio === enemyHealthRatio) {
    return 'draw';
  }

  return playerHealthRatio > enemyHealthRatio ? 'player' : 'enemy';
}

function remainingHealthRatio(combatants: MutableCombatant[], side: CombatSide): number {
  const sideCombatants = combatants.filter((combatant) => combatant.side === side);
  const totalMaxHealth = sideCombatants.reduce((total, combatant) => total + combatant.maxHealth, 0);
  if (totalMaxHealth === 0) {
    return 0;
  }

  return sideCombatants.reduce((total, combatant) => total + combatant.health, 0) / totalMaxHealth;
}

function createRandomSource(seed: number): RandomSource {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4_294_967_296;
  };
}

function validateInput(input: CombatInput): void {
  if (
    !Number.isInteger(input.seed)
    || input.seed < 0
    || input.seed > 0xffff_ffff
  ) {
    throw new RangeError('Combat seed must be an unsigned 32-bit integer');
  }
  if (
    !Number.isInteger(input.maxDurationMs)
    || input.maxDurationMs < STEP_MS
    || input.maxDurationMs > MAX_COMBAT_DURATION_MS
    || input.maxDurationMs % STEP_MS !== 0
  ) {
    throw new RangeError('Combat duration must be a 100 ms multiple from 100 to 120000');
  }

  const slots = [...input.playerSlots, ...input.enemySlots];
  if (
    input.playerSlots.some(({ combatant }) => combatant.side !== 'player' || combatant.unitId === null)
    || input.enemySlots.some(({ combatant }) => combatant.side !== 'enemy' || combatant.unitId !== null)
    || slots.some(({ combatant }) => combatant.id.length === 0)
    || input.playerSlots.some(({ combatant }) => (
      combatant.unitId === null || combatant.unitId.length === 0
    ))
    || hasDuplicates(slots.map(({ combatant }) => combatant.id))
    || hasDuplicates(input.playerSlots.map(({ combatant }) => combatant.unitId))
    || hasDuplicates(slots.map(({ x, y }) => cellKey(x, y)))
    || slots.some(isInvalidSlot)
  ) {
    throw new RangeError('Invalid combat formation');
  }
}

function isInvalidSlot({ x, y, combatant }: CombatSlot): boolean {
  return (
    !Number.isInteger(x)
    || !Number.isInteger(y)
    || x < 0
    || x >= GRID_WIDTH
    || y < 0
    || y >= GRID_HEIGHT
    || !isFiniteAbove(combatant.maxHealth, 0)
    || !isFiniteAtLeast(combatant.speed, 0)
    || !isFiniteAtLeast(combatant.damage, 0)
    || !isFiniteAtLeast(combatant.armor, 0)
    || !isFiniteAbove(combatant.attackIntervalMs, 0)
  );
}

function isFiniteAbove(value: number, minimum: number): boolean {
  return Number.isFinite(value) && value > minimum;
}

function isFiniteAtLeast(value: number, minimum: number): boolean {
  return Number.isFinite(value) && value >= minimum;
}

function hasDuplicates(values: readonly unknown[]): boolean {
  return new Set(values).size !== values.length;
}

function cellKey(x: number, y: number): string {
  return `${x}:${y}`;
}

function compareCodeUnits(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}
