import {
  BUILDING_CATALOG,
  CELL_SIZE,
  ECONOMY_STEP_MS,
  GRID_HEIGHT,
  GRID_WIDTH,
  MAX_UNITS_PER_CELL,
  ORE_PER_STRENGTH_SECOND,
  ORE_SELL_PRICE,
  TRANSFER_TIME_MS,
  UNIT_CATALOG,
  type BuildingKind,
  type UnitKind,
} from '../content/catalog';

export interface Cell {
  readonly x: number;
  readonly y: number;
}

export interface WorldPosition {
  x: number;
  y: number;
}

export type HaulPhase = 'to-source' | 'loading' | 'to-destination' | 'unloading';

export type Assignment =
  | { kind: 'idle' }
  | { kind: 'to-work'; buildingId: string }
  | { kind: 'work'; buildingId: string }
  | {
      kind: 'haul';
      sourceId: string;
      destinationId: string;
      phase: HaulPhase;
      carried: number;
      phaseElapsedMs: number;
    };

export interface Unit {
  readonly id: string;
  readonly unitKind: UnitKind;
  position: WorldPosition;
  assignment: Assignment;
}

export interface Building {
  readonly id: string;
  readonly kind: BuildingKind;
  readonly cell: Cell;
  ore: number;
  productionProgress: number;
}

export interface GameState {
  gold: number;
  soldOre: number;
  buildings: Building[];
  units: Unit[];
  nextBuildingId: number;
  nextUnitId: number;
}

export type GameCommand =
  | { type: 'BUILD_MINE'; cell: Cell }
  | { type: 'BUY_UNITS'; unitKind: UnitKind; amount: number; cell: Cell }
  | { type: 'ASSIGN_WORK'; unitIds: readonly string[]; buildingId: string }
  | {
      type: 'ASSIGN_HAUL';
      unitIds: readonly string[];
      sourceId: string;
      destinationId: string;
    }
  | { type: 'RELEASE_UNITS'; unitIds: readonly string[] };

export type CommandResult<T = void> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly error: string };

const ok = <T>(value: T): CommandResult<T> => ({ ok: true, value });
const fail = (error: string): CommandResult<never> => ({ ok: false, error });

export function createInitialState(): GameState {
  return {
    gold: 1000,
    soldOre: 0,
    buildings: [
      { id: 'warehouse-1', kind: 'warehouse', cell: { x: 10, y: 8 }, ore: 0, productionProgress: 0 },
      { id: 'market-1', kind: 'market', cell: { x: 10, y: 2 }, ore: 0, productionProgress: 0 },
    ],
    units: [],
    nextBuildingId: 1,
    nextUnitId: 1,
  };
}

export function applyCommand(state: GameState, command: GameCommand): CommandResult<GameState> {
  switch (command.type) {
    case 'BUILD_MINE':
      return buildMine(state, command.cell);
    case 'BUY_UNITS':
      return buyUnits(state, command.unitKind, command.amount, command.cell);
    case 'ASSIGN_WORK':
      return assignWork(state, command.unitIds, command.buildingId);
    case 'ASSIGN_HAUL':
      return assignHaul(state, command.unitIds, command.sourceId, command.destinationId);
    case 'RELEASE_UNITS':
      return releaseUnits(state, command.unitIds);
  }
}

export function tickColony(state: GameState): GameState {
  const next = structuredClone(state);
  produceOre(next);
  for (const unit of next.units) {
    if (unit.assignment.kind === 'to-work') tickWorkerArrival(next, unit);
    else if (unit.assignment.kind === 'haul') tickHauler(next, unit);
  }
  return next;
}

export function findFirstValidMineCell(state: GameState): Cell | null {
  for (let y = 1; y < GRID_HEIGHT - 1; y += 1) {
    for (let x = 1; x < GRID_WIDTH - 1; x += 1) {
      const cell = { x, y };
      if (validateMinePlacement(state, cell).ok) return cell;
    }
  }
  return null;
}

export function buildingCenter(building: Building): WorldPosition {
  const definition = BUILDING_CATALOG[building.kind];
  return {
    x: (building.cell.x + definition.width / 2) * CELL_SIZE,
    y: (building.cell.y + definition.height / 2) * CELL_SIZE,
  };
}

export function productionPerSecond(state: GameState, buildingId: string): number {
  return state.units
    .filter((unit) => unit.assignment.kind === 'work' && unit.assignment.buildingId === buildingId)
    .reduce((total, unit) => total + UNIT_CATALOG[unit.unitKind].strength * ORE_PER_STRENGTH_SECOND, 0);
}

function buildMine(state: GameState, cell: Cell): CommandResult<GameState> {
  const placement = validateMinePlacement(state, cell);
  if (!placement.ok) return placement;
  const price = BUILDING_CATALOG.mine.price;
  if (state.gold < price) return fail('Недостаточно золота');

  const next = structuredClone(state);
  next.gold -= price;
  next.buildings.push({
    id: `mine-${next.nextBuildingId}`,
    kind: 'mine',
    cell: { ...cell },
    ore: 0,
    productionProgress: 0,
  });
  next.nextBuildingId += 1;
  return ok(next);
}

function buyUnits(state: GameState, unitKind: UnitKind, amount: number, cell: Cell): CommandResult<GameState> {
  const purchase = validateUnitPurchase(state, unitKind, amount, cell);
  if (!purchase.ok) return purchase;

  const definition = UNIT_CATALOG[unitKind];
  const next = structuredClone(state);
  const positions = availableCrowdPositions(state, cell).slice(0, amount);
  next.gold -= definition.price * amount;
  positions.forEach((position) => {
    const unitNumber = next.nextUnitId;
    next.units.push({
      id: `unit-${unitNumber}`,
      unitKind,
      position,
      assignment: { kind: 'idle' },
    });
    next.nextUnitId += 1;
  });
  return ok(next);
}

function assignWork(state: GameState, unitIds: readonly string[], buildingId: string): CommandResult<GameState> {
  const selected = resolveUnits(state, unitIds);
  if (!selected.ok) return selected;
  const building = state.buildings.find((candidate) => candidate.id === buildingId);
  if (!building || building.kind !== 'mine') return fail('Работать можно только в шахте');

  const reservedWorkers = state.units.filter(
    (unit) =>
      (unit.assignment.kind === 'to-work' || unit.assignment.kind === 'work') &&
      unit.assignment.buildingId === buildingId,
  );
  const unitsToAssign = selected.value
    .filter(
      (unit) =>
        (unit.assignment.kind !== 'to-work' && unit.assignment.kind !== 'work') ||
        unit.assignment.buildingId !== buildingId,
    )
    .slice(0, Math.max(0, BUILDING_CATALOG.mine.maxWorkers - reservedWorkers.length));
  const next = structuredClone(state);
  unitsToAssign.forEach((unit) => {
    const nextUnit = next.units.find((candidate) => candidate.id === unit.id)!;
    returnCarriedOre(next, nextUnit);
    nextUnit.assignment = { kind: 'to-work', buildingId };
  });
  return ok(next);
}

function assignHaul(
  state: GameState,
  unitIds: readonly string[],
  sourceId: string,
  destinationId: string,
): CommandResult<GameState> {
  const selected = resolveUnits(state, unitIds);
  if (!selected.ok) return selected;
  const source = state.buildings.find((building) => building.id === sourceId);
  const destination = state.buildings.find((building) => building.id === destinationId);
  if (!source || !destination) return fail('Здание не найдено');
  if (!isValidHaulRoute(source.kind, destination.kind)) return fail('Этот маршрут не перевозит руду');

  const next = structuredClone(state);
  selected.value.forEach((unit) => {
    const nextUnit = next.units.find((candidate) => candidate.id === unit.id)!;
    returnCarriedOre(next, nextUnit);
    nextUnit.assignment = {
      kind: 'haul',
      sourceId,
      destinationId,
      phase: 'to-source',
      carried: 0,
      phaseElapsedMs: 0,
    };
  });
  return ok(next);
}

function releaseUnits(state: GameState, unitIds: readonly string[]): CommandResult<GameState> {
  const selected = resolveUnits(state, unitIds);
  if (!selected.ok) return selected;
  const next = structuredClone(state);
  selected.value.forEach((unit) => {
    const nextUnit = next.units.find((candidate) => candidate.id === unit.id)!;
    returnCarriedOre(next, nextUnit);
    nextUnit.assignment = { kind: 'idle' };
    nextUnit.position = idlePosition(unitNumberFromId(nextUnit.id));
  });
  return ok(next);
}

export function validateMinePlacement(state: GameState, cell: Cell): CommandResult<void> {
  const mine = BUILDING_CATALOG.mine;
  if (cell.x < 0 || cell.y < 0 || cell.x + mine.width > GRID_WIDTH || cell.y + mine.height > GRID_HEIGHT) {
    return fail('Шахта выходит за границу поля');
  }
  const blocked = state.buildings.some((building) => footprintsOverlap(cell, 'mine', building.cell, building.kind));
  return blocked ? fail('Здесь уже стоит здание') : ok(undefined);
}

export function validateUnitPurchase(
  state: GameState,
  unitKind: UnitKind,
  amount: number,
  cell: Cell,
): CommandResult<void> {
  if (!Number.isInteger(amount) || amount < 1 || amount > MAX_UNITS_PER_CELL) {
    return fail('Количество должно быть от 1 до 20');
  }
  if (!Number.isInteger(cell.x) || !Number.isInteger(cell.y) || cell.x < 0 || cell.y < 0 || cell.x >= GRID_WIDTH || cell.y >= GRID_HEIGHT) {
    return fail('Клетка находится за границей поля');
  }
  if (state.buildings.some((building) => buildingOccupiesCell(building, cell))) {
    return fail('Эта клетка занята постройкой');
  }
  if (unitsInCell(state, cell).length + amount > MAX_UNITS_PER_CELL) {
    return fail('В одной клетке помещается не больше 20 существ');
  }
  if (state.gold < UNIT_CATALOG[unitKind].price * amount) return fail('Недостаточно золота');
  return ok(undefined);
}

function footprintsOverlap(a: Cell, aKind: BuildingKind, b: Cell, bKind: BuildingKind): boolean {
  const aSize = BUILDING_CATALOG[aKind];
  const bSize = BUILDING_CATALOG[bKind];
  return a.x < b.x + bSize.width && a.x + aSize.width > b.x && a.y < b.y + bSize.height && a.y + aSize.height > b.y;
}

function buildingOccupiesCell(building: Building, cell: Cell): boolean {
  const size = BUILDING_CATALOG[building.kind];
  return (
    cell.x >= building.cell.x &&
    cell.x < building.cell.x + size.width &&
    cell.y >= building.cell.y &&
    cell.y < building.cell.y + size.height
  );
}

function resolveUnits(state: GameState, unitIds: readonly string[]): CommandResult<Unit[]> {
  if (unitIds.length === 0) return fail('Сначала выберите юнитов');
  if (new Set(unitIds).size !== unitIds.length) return fail('Юнит выбран дважды');
  const units = unitIds.map((id) => state.units.find((unit) => unit.id === id));
  return units.some((unit) => !unit) ? fail('Юнит не найден') : ok(units as Unit[]);
}

export function isValidHaulRoute(source: BuildingKind, destination: BuildingKind): boolean {
  return (
    (source === 'mine' && (destination === 'warehouse' || destination === 'market')) ||
    (source === 'warehouse' && destination === 'market')
  );
}

function produceOre(state: GameState): void {
  for (const building of state.buildings) {
    if (building.kind !== 'mine') continue;
    const progress = round(
      building.productionProgress + productionPerSecond(state, building.id) * (ECONOMY_STEP_MS / 1000),
    );
    const produced = Math.floor(progress);
    const room = BUILDING_CATALOG.mine.maxOre - building.ore;
    const stored = Math.min(room, produced);
    building.ore += stored;
    building.productionProgress = building.ore >= BUILDING_CATALOG.mine.maxOre ? 0 : round(progress - stored);
  }
}

function tickWorkerArrival(state: GameState, unit: Unit): void {
  const assignment = unit.assignment;
  if (assignment.kind !== 'to-work') return;
  const building = state.buildings.find((candidate) => candidate.id === assignment.buildingId);
  if (!building) return;
  if (moveToward(unit, buildingEntrancePosition(building))) {
    unit.assignment = { kind: 'work', buildingId: building.id };
  }
}

function tickHauler(state: GameState, unit: Unit): void {
  const assignment = unit.assignment;
  if (assignment.kind !== 'haul') return;
  const source = state.buildings.find((building) => building.id === assignment.sourceId);
  const destination = state.buildings.find((building) => building.id === assignment.destinationId);
  if (!source || !destination) return;

  if (assignment.phase === 'to-source') {
    if (moveToward(unit, buildingAccessPosition(source, unit.id))) beginPhase(assignment, 'loading');
    return;
  }
  if (assignment.phase === 'to-destination') {
    if (moveToward(unit, buildingAccessPosition(destination, unit.id))) beginPhase(assignment, 'unloading');
    return;
  }

  assignment.phaseElapsedMs += ECONOMY_STEP_MS;
  if (assignment.phaseElapsedMs < TRANSFER_TIME_MS) return;
  if (assignment.phase === 'loading') loadOre(unit, source, destination);
  else unloadOre(state, unit, destination);
}

function moveToward(unit: Unit, target: WorldPosition): boolean {
  const speed = 120 + UNIT_CATALOG[unit.unitKind].speed * 12;
  const maximumDistance = speed * (ECONOMY_STEP_MS / 1000);
  const xDistance = target.x - unit.position.x;
  const yDistance = target.y - unit.position.y;
  const distance = Math.hypot(xDistance, yDistance);
  if (distance <= maximumDistance) {
    unit.position = { ...target };
    return true;
  }
  unit.position.x = round(unit.position.x + (xDistance / distance) * maximumDistance);
  unit.position.y = round(unit.position.y + (yDistance / distance) * maximumDistance);
  return false;
}

function loadOre(unit: Unit, source: Building, destination: Building): void {
  const assignment = unit.assignment;
  if (assignment.kind !== 'haul') return;
  const destinationRoom =
    destination.kind === 'market' ? Number.POSITIVE_INFINITY : BUILDING_CATALOG[destination.kind].maxOre - destination.ore;
  const amount = Math.min(source.ore, UNIT_CATALOG[unit.unitKind].cargo, Math.max(0, destinationRoom));
  if (amount <= 0) {
    assignment.phaseElapsedMs = TRANSFER_TIME_MS;
    return;
  }
  source.ore -= amount;
  assignment.carried = amount;
  beginPhase(assignment, 'to-destination');
}

function unloadOre(state: GameState, unit: Unit, destination: Building): void {
  const assignment = unit.assignment;
  if (assignment.kind !== 'haul') return;
  if (destination.kind === 'market') {
    state.gold += assignment.carried * ORE_SELL_PRICE;
    state.soldOre += assignment.carried;
    assignment.carried = 0;
  } else {
    const room = Math.max(0, BUILDING_CATALOG[destination.kind].maxOre - destination.ore);
    const transferred = Math.min(room, assignment.carried);
    destination.ore += transferred;
    assignment.carried -= transferred;
  }
  if (assignment.carried === 0) beginPhase(assignment, 'to-source');
  else assignment.phaseElapsedMs = TRANSFER_TIME_MS;
}

function beginPhase(assignment: Extract<Assignment, { kind: 'haul' }>, phase: HaulPhase): void {
  assignment.phase = phase;
  assignment.phaseElapsedMs = 0;
}

function returnCarriedOre(state: GameState, unit: Unit): void {
  const assignment = unit.assignment;
  if (assignment.kind !== 'haul' || assignment.carried === 0) return;
  const source = state.buildings.find((building) => building.id === assignment.sourceId);
  if (source) source.ore += assignment.carried;
}

function idlePosition(unitNumber: number): WorldPosition {
  const slot = unitNumber - 1;
  const crowd = Math.floor(slot / MAX_UNITS_PER_CELL);
  const cell = {
    x: 7 + (crowd % 3),
    y: 5 + Math.floor(crowd / 3),
  };
  return crowdPosition(cell, slot % MAX_UNITS_PER_CELL);
}

function availableCrowdPositions(state: GameState, cell: Cell): WorldPosition[] {
  const occupied = new Set(unitsInCell(state, cell).map((unit) => positionKey(unit.position)));
  return Array.from({ length: MAX_UNITS_PER_CELL }, (_, slot) => crowdPosition(cell, slot))
    .filter((position) => !occupied.has(positionKey(position)));
}

function unitsInCell(state: GameState, cell: Cell): Unit[] {
  return state.units.filter((unit) => (
    Math.floor(unit.position.x / CELL_SIZE) === cell.x &&
    Math.floor(unit.position.y / CELL_SIZE) === cell.y
  ));
}

function positionKey(position: WorldPosition): string {
  return `${position.x}:${position.y}`;
}

function buildingAccessPosition(building: Building, unitId: string): WorldPosition {
  const definition = BUILDING_CATALOG[building.kind];
  const unitSlot = unitNumberFromId(unitId) - 1;
  const crowd = Math.floor(unitSlot / MAX_UNITS_PER_CELL);
  const belowBuilding = building.cell.y + definition.height;
  const y = belowBuilding < GRID_HEIGHT ? belowBuilding : building.cell.y - 1;
  const x = Math.min(GRID_WIDTH - 1, building.cell.x + Math.floor(definition.width / 2) + crowd);
  return crowdPosition({ x, y }, unitSlot % MAX_UNITS_PER_CELL);
}

function buildingEntrancePosition(building: Building): WorldPosition {
  const definition = BUILDING_CATALOG[building.kind];
  return {
    x: (building.cell.x + definition.width / 2) * CELL_SIZE,
    y: (building.cell.y + definition.height - 0.35) * CELL_SIZE,
  };
}

function crowdPosition(cell: Cell, slot: number): WorldPosition {
  const goldenAngle = Math.PI * (3 - Math.sqrt(5));
  const cellRotation = (cell.x * 17 + cell.y * 31) * 0.19;
  const scatteredSlot = (slot * 11) % MAX_UNITS_PER_CELL;
  const radius = Math.sqrt((scatteredSlot + 0.5) / MAX_UNITS_PER_CELL) * 0.43;
  const angle = scatteredSlot * goldenAngle + cellRotation;
  return {
    x: round((cell.x + 0.5 + Math.cos(angle) * radius) * CELL_SIZE),
    y: round((cell.y + 0.5 + Math.sin(angle) * radius) * CELL_SIZE),
  };
}

function unitNumberFromId(unitId: string): number {
  return Number(unitId.split('-')[1]);
}

function round(value: number): number {
  return Math.round(value * 1_000_000) / 1_000_000;
}
