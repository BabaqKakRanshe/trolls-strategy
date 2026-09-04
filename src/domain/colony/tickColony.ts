import { BUILDING_CATALOG } from '../../content/catalog';
import type {
  Assignment,
  Building,
  BuildingKind,
  GameState,
  Unit,
} from '../model';

const FIXED_STEP_MS = 250;
const ACTION_DURATION_MS = FIXED_STEP_MS;
const MILLIS_PER_DISTANCE_UNIT = 1_000;
const INTEGER_SNAP_EPSILON = 1e-9;

const BUILDING_POSITIONS: Record<BuildingKind, readonly [number, number]> = {
  'town-hall': [0, 0],
  mine: [8, 0],
  warehouse: [8, 6],
  market: [0, 6],
};

export function tickColony(state: GameState, deltaMs: number): void {
  assertValidDelta(deltaMs);

  for (let elapsedMs = 0; elapsedMs < deltaMs; elapsedMs += FIXED_STEP_MS) {
    tickFixedStep(state);
  }
}

function assertValidDelta(deltaMs: number): void {
  if (!Number.isFinite(deltaMs) || deltaMs <= 0 || deltaMs % FIXED_STEP_MS !== 0) {
    throw new RangeError(`deltaMs must be a positive multiple of ${FIXED_STEP_MS}`);
  }
}

function tickFixedStep(state: GameState): void {
  state.simulationTimeMs += FIXED_STEP_MS;
  produceMineOre(state);
  state.units.forEach((unit) => advanceHauler(state, unit, FIXED_STEP_MS));
}

function produceMineOre(state: GameState): void {
  const mine = state.buildings.find((building) => building.kind === 'mine');
  if (!mine) {
    return;
  }

  const totalWorkerStrength = state.units.reduce((total, unit) => (
    unit.assignment.kind === 'work' && unit.assignment.buildingId === mine.id
      ? total + unit.strength
      : total
  ), 0);
  const producedOre = totalWorkerStrength
    * BUILDING_CATALOG.mine.orePerStrengthSecond
    * (FIXED_STEP_MS / 1_000);
  const incomingOre = reservedIncomingCargo(state, mine.id);
  const productionCapacity = Math.max(
    0,
    BUILDING_CATALOG.mine.inventoryCapacity
      - incomingOre
      - mine.inventory.ironOre
      - state.accumulators.mineOre,
  );
  const acceptedProduction = Math.min(producedOre, productionCapacity);
  const availableOre = snapNearInteger(
    mine.inventory.ironOre + state.accumulators.mineOre + acceptedProduction,
  );
  const wholeOre = Math.floor(availableOre);

  mine.inventory.ironOre = wholeOre;
  state.accumulators.mineOre = normalizeFraction(availableOre - wholeOre);
}

function snapNearInteger(value: number): number {
  const nearestInteger = Math.round(value);
  return Math.abs(value - nearestInteger) < INTEGER_SNAP_EPSILON
    ? nearestInteger
    : value;
}

function normalizeFraction(value: number): number {
  return Math.abs(value) < INTEGER_SNAP_EPSILON ? 0 : value;
}

function advanceHauler(state: GameState, unit: Unit, deltaMs: number): void {
  if (unit.assignment.kind !== 'haul') {
    return;
  }

  let remainingMs = deltaMs;
  while (remainingMs > 0 && unit.assignment.kind === 'haul') {
    const assignment = unit.assignment;
    const phaseDurationMs = getPhaseDurationMs(state, unit, assignment);
    if (phaseDurationMs === null) {
      return;
    }

    const untilTransitionMs = phaseDurationMs - assignment.progressMs;
    const consumedMs = Math.min(remainingMs, untilTransitionMs);
    assignment.progressMs += consumedMs;
    remainingMs -= consumedMs;

    if (assignment.progressMs < phaseDurationMs) {
      return;
    }
    transitionHauler(state, unit, assignment);
  }
}

function getPhaseDurationMs(
  state: GameState,
  unit: Unit,
  assignment: Extract<Assignment, { kind: 'haul' }>,
): number | null {
  if (assignment.phase === 'loading' || assignment.phase === 'unloading') {
    return ACTION_DURATION_MS;
  }

  const source = findBuilding(state, assignment.fromId);
  const destination = findBuilding(state, assignment.toId);
  if (!source || !destination || unit.speed <= 0) {
    return null;
  }

  return buildingDistance(source.kind, destination.kind)
    * MILLIS_PER_DISTANCE_UNIT
    / unit.speed;
}

function buildingDistance(from: BuildingKind, to: BuildingKind): number {
  const [fromX, fromY] = BUILDING_POSITIONS[from];
  const [toX, toY] = BUILDING_POSITIONS[to];
  return Math.abs(toX - fromX) + Math.abs(toY - fromY);
}

function transitionHauler(
  state: GameState,
  unit: Unit,
  assignment: Extract<Assignment, { kind: 'haul' }>,
): void {
  assignment.progressMs = 0;

  switch (assignment.phase) {
    case 'toSource':
      assignment.phase = 'loading';
      break;
    case 'loading':
      loadHauler(state, unit, assignment);
      break;
    case 'toDestination':
      assignment.phase = 'unloading';
      break;
    case 'unloading':
      unloadHauler(state, assignment);
      break;
  }
}

function loadHauler(
  state: GameState,
  unit: Unit,
  assignment: Extract<Assignment, { kind: 'haul' }>,
): void {
  const source = findBuilding(state, assignment.fromId);
  const destination = findBuilding(state, assignment.toId);
  if (!source || !destination) {
    return;
  }

  const destinationFreeSpace = Math.max(
    0,
    inventoryCapacity(destination.kind)
      - occupiedInventory(state, destination)
      - reservedIncomingCargo(state, destination.id),
  );
  const loaded = Math.min(
    source.inventory.ironOre,
    unit.carryCapacity - assignment.carried,
    Math.floor(snapNearInteger(destinationFreeSpace)),
  );

  if (loaded <= 0) {
    return;
  }

  source.inventory.ironOre -= loaded;
  assignment.carried += loaded;
  assignment.phase = 'toDestination';
}

function unloadHauler(
  state: GameState,
  assignment: Extract<Assignment, { kind: 'haul' }>,
): void {
  const destination = findBuilding(state, assignment.toId);
  if (!destination) {
    return;
  }

  const unloaded = Math.min(
    assignment.carried,
    Math.floor(snapNearInteger(Math.max(
      0,
      inventoryCapacity(destination.kind) - occupiedInventory(state, destination),
    ))),
  );
  destination.inventory.ironOre += unloaded;
  assignment.carried -= unloaded;

  if (assignment.carried === 0) {
    assignment.phase = 'toSource';
  }
}

function reservedIncomingCargo(
  state: GameState,
  destinationId: Building['id'],
): number {
  return state.units.reduce((total, unit) => {
    const assignment = unit.assignment;
    return assignment.kind === 'haul'
      && assignment.toId === destinationId
      && assignment.resource === 'ironOre'
      ? total + assignment.carried
      : total;
  }, 0);
}

function occupiedInventory(state: GameState, building: Building): number {
  return building.inventory.ironOre
    + (building.kind === 'mine' ? state.accumulators.mineOre : 0);
}

function inventoryCapacity(kind: BuildingKind): number {
  switch (kind) {
    case 'mine':
      return BUILDING_CATALOG.mine.inventoryCapacity;
    case 'warehouse':
      return BUILDING_CATALOG.warehouse.inventoryCapacity;
    case 'town-hall':
    case 'market':
      return Number.MAX_SAFE_INTEGER;
  }
}

function findBuilding(
  state: GameState,
  id: Extract<Assignment, { kind: 'haul' }>['fromId'],
): Building | undefined {
  return state.buildings.find((building) => building.id === id);
}
