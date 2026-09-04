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
} from '../model';
import type { CommandErrorCode, CommandResult } from '../results';

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
  };

  state.wallet.gold -= definition.cost;
  state.units.push(unit);
  return success(id);
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
  });
  return success(undefined);
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
