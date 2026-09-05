import {
  BUILDING_CATALOG,
  ITEM_CATALOG,
  UNIT_CATALOG,
  type UnitSpecies,
} from '../../content/catalog';
import type {
  BuildingId,
  EquipmentSlot,
  GameState,
  Item,
  ItemId,
  Unit,
  UnitId,
  GridCell,
} from '../model';
import type { CommandErrorCode, CommandResult } from '../results';
import {
  BARRACKS_CELL,
  BARRACKS_FOOTPRINT,
  MAX_UNITS_PER_CELL,
  buildingAtCell,
  buildingInteractionCell,
  footprintContains,
  isGridCell,
  sameCell,
} from '../map/grid';

function success<T>(value: T): CommandResult<T> {
  return { ok: true, value };
}

function failure<T>(code: CommandErrorCode): CommandResult<T> {
  return { ok: false, error: { code } };
}

function findUnit(state: GameState, id: UnitId): Unit | undefined {
  return state.units.find((unit) => unit.id === id);
}

function hasDuplicateIds(ids: readonly UnitId[]): boolean {
  return new Set(ids).size !== ids.length;
}

function isUnavailable(unit: Unit): boolean {
  return unit.assignment.kind === 'mission' || unit.assignment.kind === 'dead';
}

function itemOwnerCount(state: GameState, itemId: ItemId): number {
  const colonyOwners = state.colonyInventory.itemIds
    .filter((candidateId) => candidateId === itemId).length;
  const equipmentOwners = state.units.reduce((count, unit) => (
    count
    + Number(unit.equipment.weaponId === itemId)
    + Number(unit.equipment.armorId === itemId)
  ), 0);

  return colonyOwners + equipmentOwners;
}

function isValidItemDefinition(item: Item): boolean {
  const catalogItem = ITEM_CATALOG[item.kind];
  return item.slot === catalogItem.slot;
}

function equipmentKey(slot: EquipmentSlot): 'weaponId' | 'armorId' {
  return slot === 'weapon' ? 'weaponId' : 'armorId';
}

export function recruitUnit(
  state: GameState,
  species: UnitSpecies,
  id: UnitId,
  name: string,
): CommandResult<UnitId> {
  if (findUnit(state, id)) {
    return failure('DUPLICATE_ID');
  }

  const definition = UNIT_CATALOG[species];
  if (state.wallet.gold < definition.cost) {
    return failure('INSUFFICIENT_GOLD');
  }

  const unit: Unit = {
    id,
    species,
    name,
    level: 1,
    health: definition.maxHealth,
    maxHealth: definition.maxHealth,
    strength: definition.strength,
    speed: definition.speed,
    carryCapacity: definition.carryCapacity,
    attackIntervalMs: definition.attackIntervalMs,
    damage: definition.damage,
    armor: definition.armor,
    assignment: { kind: 'idle' },
    equipment: { weaponId: null, armorId: null },
    mapCell: null,
  };

  state.wallet.gold -= definition.cost;
  state.units.push(unit);
  return success(id);
}

export function spawnUnit(
  state: GameState,
  species: UnitSpecies,
  id: UnitId,
  name: string,
  cell: GridCell,
): CommandResult<UnitId> {
  if (!isGridCell(cell)) return failure('INVALID_CELL');
  if (footprintContains(BARRACKS_CELL, BARRACKS_FOOTPRINT, cell)
    || buildingAtCell(state.buildings, cell) !== undefined) {
    return failure('CELL_OCCUPIED');
  }
  if (state.units.filter((unit) => sameCell(unit.mapCell, cell)).length >= MAX_UNITS_PER_CELL) {
    return failure('CELL_FULL');
  }
  const result = recruitUnit(state, species, id, name);
  if (result.ok) state.units.at(-1)!.mapCell = { ...cell };
  return result;
}

export function assignWorkers(
  state: GameState,
  unitIds: readonly UnitId[],
  buildingId: BuildingId,
): CommandResult<void> {
  if (unitIds.length === 0) {
    return failure('INVALID_ASSIGNMENT');
  }
  if (hasDuplicateIds(unitIds)) {
    return failure('DUPLICATE_ID');
  }

  const building = state.buildings.find((candidate) => candidate.id === buildingId);
  if (!building) {
    return failure('NOT_FOUND');
  }
  if (building.kind !== 'mine') {
    return failure('INVALID_ASSIGNMENT');
  }

  const units = unitIds.map((unitId) => findUnit(state, unitId));
  if (units.some((unit) => !unit)) {
    return failure('NOT_FOUND');
  }

  const selectedUnits = units as Unit[];
  const hasInvalidAssignment = selectedUnits.some((unit) => (
    unit.assignment.kind !== 'idle'
    && !(unit.assignment.kind === 'work' && unit.assignment.buildingId === buildingId)
  ));
  if (hasInvalidAssignment) {
    return failure('INVALID_ASSIGNMENT');
  }

  const existingWorkerIds = new Set(
    state.units
      .filter((unit) => (
        unit.assignment.kind === 'work'
        && unit.assignment.buildingId === buildingId
      ))
      .map((unit) => unit.id),
  );
  unitIds.forEach((unitId) => existingWorkerIds.add(unitId));
  if (existingWorkerIds.size > BUILDING_CATALOG.mine.workerCapacity) {
    return failure('CAPACITY_EXCEEDED');
  }

  selectedUnits.forEach((unit) => {
    unit.assignment = { kind: 'work', buildingId };
    if (building.mapCell !== null) unit.mapCell = buildingInteractionCell(building.kind, building.mapCell);
  });
  return success(undefined);
}

export function assignHaulers(
  state: GameState,
  unitIds: readonly UnitId[],
  fromId: BuildingId,
  toId: BuildingId,
): CommandResult<void> {
  if (unitIds.length === 0) return failure('INVALID_ASSIGNMENT');
  if (hasDuplicateIds(unitIds)) return failure('DUPLICATE_ID');
  const source = state.buildings.find((building) => building.id === fromId);
  const destination = state.buildings.find((building) => building.id === toId);
  if (!source || !destination) return failure('NOT_FOUND');
  const units = unitIds.map((id) => findUnit(state, id));
  if (fromId === toId || units.some((unit) => !unit || unit.assignment.kind !== 'idle')) {
    return failure('INVALID_ASSIGNMENT');
  }
  for (const unit of units as Unit[]) {
    unit.assignment = {
      kind: 'haul', fromId, toId, resource: 'ironOre', carried: 0,
      phase: 'toSource', progressMs: 0,
    };
    if (source.mapCell !== null) unit.mapCell = buildingInteractionCell(source.kind, source.mapCell);
  }
  return success(undefined);
}

export function sellUnits(
  state: GameState,
  unitIds: readonly UnitId[],
): CommandResult<number> {
  if (unitIds.length === 0) return failure('INVALID_ASSIGNMENT');
  if (hasDuplicateIds(unitIds)) return failure('DUPLICATE_ID');
  const units = unitIds.map((id) => findUnit(state, id));
  if (units.some((unit) => !unit)) return failure('NOT_FOUND');
  const selected = units as Unit[];
  if (selected.some((unit) => isUnavailable(unit)
    || (unit.assignment.kind === 'haul' && unit.assignment.carried > 0))) {
    return failure('INVALID_ASSIGNMENT');
  }
  const refund = selected.reduce(
    (sum, unit) => sum + Math.floor(UNIT_CATALOG[unit.species].cost * 0.5), 0,
  );
  for (const unit of selected) {
    if (unit.equipment.weaponId !== null) state.colonyInventory.itemIds.push(unit.equipment.weaponId);
    if (unit.equipment.armorId !== null) state.colonyInventory.itemIds.push(unit.equipment.armorId);
  }
  const selectedIds = new Set(unitIds);
  state.units = state.units.filter((unit) => !selectedIds.has(unit.id));
  state.wallet.gold += refund;
  return success(refund);
}

export function assignHauler(
  state: GameState,
  unitId: UnitId,
  fromId: BuildingId,
  toId: BuildingId,
): CommandResult<void> {
  const unit = findUnit(state, unitId);
  const source = state.buildings.find((building) => building.id === fromId);
  const destination = state.buildings.find((building) => building.id === toId);
  if (!unit || !source || !destination) {
    return failure('NOT_FOUND');
  }
  if (fromId === toId || unit.assignment.kind !== 'idle') {
    return failure('INVALID_ASSIGNMENT');
  }

  unit.assignment = {
    kind: 'haul',
    fromId,
    toId,
    resource: 'ironOre',
    carried: 0,
    phase: 'toSource',
    progressMs: 0,
  };
  return success(undefined);
}

export function setIdle(
  state: GameState,
  unitIds: readonly UnitId[],
): CommandResult<void> {
  if (unitIds.length === 0) {
    return failure('INVALID_ASSIGNMENT');
  }
  if (hasDuplicateIds(unitIds)) {
    return failure('DUPLICATE_ID');
  }

  const units = unitIds.map((unitId) => findUnit(state, unitId));
  if (units.some((unit) => !unit)) {
    return failure('NOT_FOUND');
  }

  const selectedUnits = units as Unit[];
  const cannotBecomeIdle = selectedUnits.some((unit) => (
    isUnavailable(unit)
    || (unit.assignment.kind === 'haul' && unit.assignment.carried > 0)
  ));
  if (cannotBecomeIdle) {
    return failure('INVALID_ASSIGNMENT');
  }

  selectedUnits.forEach((unit) => {
    unit.assignment = { kind: 'idle' };
  });
  return success(undefined);
}

export function sendToBarracks(
  state: GameState,
  unitIds: readonly UnitId[],
): CommandResult<void> {
  const result = setIdle(state, unitIds);
  if (!result.ok) return result;
  const selected = new Set(unitIds);
  state.units.forEach((unit) => {
    if (selected.has(unit.id)) unit.mapCell = { ...BARRACKS_CELL };
  });
  return success(undefined);
}

export function equipItem(
  state: GameState,
  unitId: UnitId,
  itemId: ItemId,
): CommandResult<void> {
  const unit = findUnit(state, unitId);
  const item = state.items.find((candidate) => candidate.id === itemId);
  if (!unit || !item) {
    return failure('NOT_FOUND');
  }
  if (isUnavailable(unit) || !isValidItemDefinition(item)) {
    return failure('INVALID_EQUIPMENT');
  }

  const inventoryIndex = state.colonyInventory.itemIds.indexOf(itemId);
  if (inventoryIndex < 0 || itemOwnerCount(state, itemId) !== 1) {
    return failure('INVALID_EQUIPMENT');
  }

  const slotKey = equipmentKey(item.slot);
  const replacedItemId = unit.equipment[slotKey];
  if (replacedItemId !== null) {
    const replacedItem = state.items.find((candidate) => candidate.id === replacedItemId);
    if (
      !replacedItem
      || replacedItem.slot !== item.slot
      || !isValidItemDefinition(replacedItem)
      || itemOwnerCount(state, replacedItemId) !== 1
    ) {
      return failure('INVALID_EQUIPMENT');
    }
  }

  state.colonyInventory.itemIds.splice(inventoryIndex, 1);
  if (replacedItemId !== null) {
    state.colonyInventory.itemIds.push(replacedItemId);
  }
  unit.equipment[slotKey] = itemId;
  return success(undefined);
}

export function unequipItem(
  state: GameState,
  unitId: UnitId,
  slot: EquipmentSlot,
): CommandResult<void> {
  const unit = findUnit(state, unitId);
  if (!unit) {
    return failure('NOT_FOUND');
  }
  if (isUnavailable(unit)) {
    return failure('INVALID_EQUIPMENT');
  }

  const slotKey = equipmentKey(slot);
  const equippedItemId = unit.equipment[slotKey];
  if (equippedItemId === null) {
    return failure('INVALID_EQUIPMENT');
  }

  const item = state.items.find((candidate) => candidate.id === equippedItemId);
  if (
    !item
    || item.slot !== slot
    || !isValidItemDefinition(item)
    || itemOwnerCount(state, equippedItemId) !== 1
  ) {
    return failure('INVALID_EQUIPMENT');
  }

  unit.equipment[slotKey] = null;
  state.colonyInventory.itemIds.push(equippedItemId);
  return success(undefined);
}
