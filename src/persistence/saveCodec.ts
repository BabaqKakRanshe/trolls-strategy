import { ITEM_CATALOG, UNIT_CATALOG } from '../content/catalog';
import { MISSION_ONE } from '../content/missionCatalog';
import { simulateCombat } from '../domain/combat/simulateCombat';
import type {
  ActiveMissionRun,
  Assignment,
  Building,
  CombatInput,
  CombatReport,
  CombatSlot,
  Equipment,
  Formation,
  GameState,
  Item,
  MissionState,
  Unit,
} from '../domain/model';
import { deriveMissionRunIdentity } from '../domain/progression/missionIdentity';
import type { CommandResult } from '../domain/results';

export type DecodeResult<T> = CommandResult<T>;

const SCHEMA_VERSION = 1;
const VALID_BUILDING_KINDS = new Set(['town-hall', 'mine', 'warehouse', 'market']);
const VALID_PHASES = new Set(['toSource', 'loading', 'toDestination', 'unloading']);
const VALID_STATUSES = new Set(['active', 'resolved', 'aborted']);

export function encodeSave(state: GameState): string {
  return JSON.stringify(structuredClone(state));
}

export function decodeSave(raw: string): DecodeResult<GameState> {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw) as unknown;
  } catch {
    return invalidSave();
  }

  if (!isRecord(parsed)) {
    return invalidSave();
  }
  if (parsed.schemaVersion !== SCHEMA_VERSION) {
    return parsed.schemaVersion === undefined
      ? invalidSave()
      : { ok: false, error: { code: 'UNSUPPORTED_SCHEMA' } };
  }

  const state = parseGameState(parsed);
  return state === null ? invalidSave() : { ok: true, value: state };
}

function parseGameState(value: Record<string, unknown>): GameState | null {
  const campaignSeed = uint32(value.campaignSeed);
  const simulationTimeMs = repairedCounter(value.simulationTimeMs);
  const wallet = isRecord(value.wallet) ? repairedCounter(value.wallet.gold) : null;
  const units = parseArray(value.units, parseUnit);
  const items = parseArray(value.items, parseItem);
  const buildings = parseArray(value.buildings, parseBuilding);
  const colonyInventory = parseStringArrayFromRecord(value.colonyInventory, 'itemIds');
  const mission = parseMissionState(value.mission);
  const unlocks = parseStringArray(value.unlocks);
  const accumulators = parseAccumulators(value.accumulators);

  if (
    campaignSeed === null
    || simulationTimeMs === null
    || wallet === null
    || units === null
    || items === null
    || buildings === null
    || colonyInventory === null
    || mission === null
    || unlocks === null
    || accumulators === null
  ) {
    return null;
  }

  const state = {
    schemaVersion: SCHEMA_VERSION,
    campaignSeed,
    simulationTimeMs,
    accumulators,
    wallet: { gold: wallet },
    units,
    items,
    colonyInventory: { itemIds: colonyInventory },
    buildings,
    mission,
    unlocks,
  } as GameState;

  return hasValidStateInvariants(state) ? state : null;
}

function parseAccumulators(value: unknown): GameState['accumulators'] | null {
  if (value === undefined) {
    return { mineOre: 0 };
  }
  if (!isRecord(value)) {
    return null;
  }
  const mineOre = value.mineOre === undefined ? 0 : repairedCounter(value.mineOre);
  return mineOre === null ? null : { mineOre };
}

function parseUnit(value: unknown): Unit | null {
  if (!isRecord(value) || !isString(value.id) || !isString(value.name)) {
    return null;
  }
  if (value.species !== 'goblin' && value.species !== 'troll') {
    return null;
  }
  const level = positiveInteger(value.level);
  const health = repairedCounter(value.health);
  const maxHealth = positiveFinite(value.maxHealth);
  const strength = nonNegativeFinite(value.strength);
  const speed = positiveFinite(value.speed);
  const carryCapacity = nonNegativeFinite(value.carryCapacity);
  const attackIntervalMs = positiveFinite(value.attackIntervalMs);
  const damage = nonNegativeFinite(value.damage);
  const armor = nonNegativeFinite(value.armor);
  const assignment = parseAssignment(value.assignment);
  const equipment = parseEquipment(value.equipment);
  if ([level, health, maxHealth, strength, speed, carryCapacity, attackIntervalMs, damage, armor]
    .some((entry) => entry === null) || assignment === null || equipment === null) {
    return null;
  }
  if (health! > maxHealth!) {
    return null;
  }
  return {
    id: value.id,
    species: value.species,
    name: value.name,
    level: level!,
    health: health!,
    maxHealth: maxHealth!,
    strength: strength!,
    speed: speed!,
    carryCapacity: carryCapacity!,
    attackIntervalMs: attackIntervalMs!,
    damage: damage!,
    armor: armor!,
    assignment,
    equipment,
  } as Unit;
}

function parseEquipment(value: unknown): Equipment | null {
  if (!isRecord(value)) {
    return null;
  }
  const weaponId = nullableString(value.weaponId);
  const armorId = nullableString(value.armorId);
  return weaponId === undefined || armorId === undefined
    ? null
    : { weaponId, armorId } as Equipment;
}

function parseAssignment(value: unknown): Assignment | null {
  if (!isRecord(value) || !isString(value.kind)) {
    return null;
  }
  if (value.kind === 'idle' || value.kind === 'dead') {
    return { kind: value.kind };
  }
  if (value.kind === 'work' && isString(value.buildingId)) {
    return { kind: 'work', buildingId: value.buildingId } as Assignment;
  }
  if (value.kind === 'mission' && isString(value.runId)) {
    return { kind: 'mission', runId: value.runId } as Assignment;
  }
  if (value.kind !== 'haul' || !isString(value.fromId) || !isString(value.toId)) {
    return null;
  }
  const carried = repairedCounter(value.carried);
  const progressMs = nonNegativeFinite(value.progressMs);
  if (
    value.resource !== 'ironOre'
    || !isString(value.phase)
    || !VALID_PHASES.has(value.phase)
    || carried === null
    || progressMs === null
  ) {
    return null;
  }
  return {
    kind: 'haul',
    fromId: value.fromId,
    toId: value.toId,
    resource: 'ironOre',
    carried,
    phase: value.phase,
    progressMs,
  } as Assignment;
}

function parseItem(value: unknown): Item | null {
  if (!isRecord(value) || !isString(value.id) || !isString(value.kind)) {
    return null;
  }
  const definition = ITEM_CATALOG[value.kind as keyof typeof ITEM_CATALOG];
  if (definition === undefined) {
    return null;
  }
  const damageBonus = nonNegativeFinite(value.damageBonus);
  const armorBonus = nonNegativeFinite(value.armorBonus);
  if (
    value.slot !== definition.slot
    || damageBonus === null
    || armorBonus === null
    || damageBonus !== definition.damageBonus
    || armorBonus !== definition.armorBonus
  ) {
    return null;
  }
  return {
    id: value.id,
    kind: value.kind,
    slot: value.slot,
    damageBonus,
    armorBonus,
  } as Item;
}

function parseBuilding(value: unknown): Building | null {
  if (
    !isRecord(value)
    || !isString(value.id)
    || !isString(value.kind)
    || !VALID_BUILDING_KINDS.has(value.kind)
    || !isRecord(value.inventory)
  ) {
    return null;
  }
  const ironOre = repairedCounter(value.inventory.ironOre);
  return ironOre === null ? null : {
    id: value.id,
    kind: value.kind,
    inventory: { ironOre },
  } as Building;
}

function parseMissionState(value: unknown): MissionState | null {
  if (!isRecord(value)) {
    return null;
  }
  const nextMissionAtMs = repairedCounter(value.nextMissionAtMs);
  const nextRunSequence = positiveInteger(value.nextRunSequence);
  const appliedRunIds = parseStringArray(value.appliedRunIds);
  const clearedMissionIds = parseStringArray(value.clearedMissionIds);
  const activeRun = value.activeRun === null ? null : parseActiveRun(value.activeRun);
  if (
    nextMissionAtMs === null
    || nextRunSequence === null
    || appliedRunIds === null
    || clearedMissionIds === null
    || (value.activeRun !== null && activeRun === null)
  ) {
    return null;
  }
  return {
    nextMissionAtMs,
    nextRunSequence,
    appliedRunIds,
    clearedMissionIds,
    activeRun,
  } as MissionState;
}

function parseActiveRun(value: unknown): ActiveMissionRun | null {
  if (
    !isRecord(value)
    || !isString(value.runId)
    || !isString(value.missionId)
    || !isString(value.status)
    || !VALID_STATUSES.has(value.status)
  ) {
    return null;
  }
  const formation = parseFormation(value.formation);
  const combatSeed = uint32(value.combatSeed);
  const combatInput = parseCombatInput(value.combatInput);
  const combatReport = parseCombatReport(value.combatReport);
  if (formation === null || combatSeed === null || combatInput === null || combatReport === null) {
    return null;
  }
  return {
    runId: value.runId,
    missionId: value.missionId,
    formation,
    combatSeed,
    combatInput,
    combatReport,
    status: value.status,
  } as ActiveMissionRun;
}

function parseFormation(value: unknown): Formation | null {
  if (!isRecord(value)) {
    return null;
  }
  const unitIds = parseStringArray(value.unitIds);
  const slots = parseArray(value.slots, parseFormationSlot);
  return unitIds === null || slots === null ? null : { unitIds, slots } as Formation;
}

function parseFormationSlot(value: unknown): { x: number; y: number } | null {
  if (!isRecord(value) || !Number.isInteger(value.x) || !Number.isInteger(value.y)) {
    return null;
  }
  return { x: value.x as number, y: value.y as number };
}

function parseCombatInput(value: unknown): CombatInput | null {
  if (!isRecord(value)) {
    return null;
  }
  const seed = uint32(value.seed);
  const maxDurationMs = positiveFinite(value.maxDurationMs);
  const playerSlots = parseArray(value.playerSlots, parseCombatSlot);
  const enemySlots = parseArray(value.enemySlots, parseCombatSlot);
  if (seed === null || maxDurationMs === null || playerSlots === null || enemySlots === null) {
    return null;
  }
  return { seed, maxDurationMs, playerSlots, enemySlots };
}

function parseCombatSlot(value: unknown): CombatSlot | null {
  const position = parseFormationSlot(value);
  if (position === null || !isRecord(value) || !isRecord(value.combatant)) {
    return null;
  }
  const combatant = value.combatant;
  if (!isString(combatant.id) || (combatant.side !== 'player' && combatant.side !== 'enemy')) {
    return null;
  }
  const maxHealth = positiveFinite(combatant.maxHealth);
  const speed = positiveFinite(combatant.speed);
  const damage = nonNegativeFinite(combatant.damage);
  const armor = nonNegativeFinite(combatant.armor);
  const attackIntervalMs = positiveFinite(combatant.attackIntervalMs);
  if ([maxHealth, speed, damage, armor, attackIntervalMs].some((entry) => entry === null)) {
    return null;
  }
  const unitId = combatant.side === 'player'
    ? (isString(combatant.unitId) ? combatant.unitId : undefined)
    : (combatant.unitId === null ? null : undefined);
  if (unitId === undefined) {
    return null;
  }
  return {
    ...position,
    combatant: {
      id: combatant.id,
      unitId,
      side: combatant.side,
      maxHealth: maxHealth!,
      speed: speed!,
      damage: damage!,
      armor: armor!,
      attackIntervalMs: attackIntervalMs!,
    },
  } as CombatSlot;
}

function parseCombatReport(value: unknown): CombatReport | null {
  if (!isRecord(value) || !['player', 'enemy', 'draw'].includes(String(value.winner))) {
    return null;
  }
  const durationMs = nonNegativeFinite(value.durationMs);
  const frames = parseArray(value.frames, (frame) => {
    if (!isRecord(frame)) return null;
    const timeMs = nonNegativeFinite(frame.timeMs);
    const combatants = parseArray(frame.combatants, (combatant) => {
      if (!isRecord(combatant) || !isString(combatant.id)) return null;
      const x = finite(combatant.x);
      const y = finite(combatant.y);
      const health = nonNegativeFinite(combatant.health);
      return x === null || y === null || health === null
        ? null
        : { id: combatant.id, x, y, health };
    });
    return timeMs === null || combatants === null ? null : { timeMs, combatants };
  });
  const deadPlayerUnitIds = parseStringArray(value.deadPlayerUnitIds);
  const deadEnemyCombatantIds = parseStringArray(value.deadEnemyCombatantIds);
  if (durationMs === null || frames === null || deadPlayerUnitIds === null || deadEnemyCombatantIds === null) {
    return null;
  }
  return {
    winner: value.winner as CombatReport['winner'],
    durationMs,
    frames,
    deadPlayerUnitIds,
    deadEnemyCombatantIds,
  } as CombatReport;
}

function hasValidStateInvariants(state: GameState): boolean {
  if (
    state.accumulators.mineOre >= 1
    || hasDuplicates(state.units.map(({ id }) => id))
    || hasDuplicates(state.items.map(({ id }) => id))
    || hasDuplicates(state.buildings.map(({ id }) => id))
    || hasDuplicates(state.colonyInventory.itemIds)
    || hasDuplicates(state.mission.appliedRunIds)
    || hasDuplicates(state.mission.clearedMissionIds)
    || hasDuplicates(state.unlocks)
  ) {
    return false;
  }

  const buildings = new Map<string, Building>(
    state.buildings.map((building) => [building.id, building]),
  );
  const mineOre = buildings.get('mine')?.inventory.ironOre;
  const mineWorkerCount = state.units.filter((unit) => (
    unit.assignment.kind === 'work' && unit.assignment.buildingId === 'mine'
  )).length;
  if (
    !hasCanonicalBuildings(state.buildings)
    || mineOre === undefined
    || mineOre + state.accumulators.mineOre > 100
    || mineWorkerCount > 5
    || !state.units.every((unit) => validAssignment(unit, buildings))
  ) {
    return false;
  }
  if (!hasExclusiveItemOwnership(state)) {
    return false;
  }
  return hasConsistentMission(state);
}

function hasCanonicalBuildings(buildings: Building[]): boolean {
  const expected = new Map([
    ['town-hall', 'town-hall'],
    ['mine', 'mine'],
    ['warehouse', 'warehouse'],
    ['market', 'market'],
  ]);
  if (buildings.length !== expected.size) return false;
  return buildings.every((building) => {
    if (expected.get(building.id) !== building.kind) return false;
    if (building.kind === 'mine') {
      return building.inventory.ironOre <= 100;
    }
    if (building.kind === 'warehouse') {
      return building.inventory.ironOre <= 500;
    }
    return true;
  });
}

function validAssignment(unit: Unit, buildings: Map<string, Building>): boolean {
  const assignment = unit.assignment;
  if (assignment.kind === 'dead') {
    return false;
  }
  if (assignment.kind === 'work') {
    return buildings.get(assignment.buildingId)?.kind === 'mine';
  }
  if (assignment.kind !== 'haul') {
    return true;
  }
  if (
    assignment.fromId === assignment.toId
    || !buildings.has(assignment.fromId)
    || !buildings.has(assignment.toId)
    || assignment.carried > unit.carryCapacity
  ) {
    return false;
  }
  if ((assignment.phase === 'toSource' || assignment.phase === 'loading') && assignment.carried !== 0) {
    return false;
  }
  if ((assignment.phase === 'toDestination' || assignment.phase === 'unloading') && assignment.carried <= 0) {
    return false;
  }
  const source = buildings.get(assignment.fromId)!;
  const destination = buildings.get(assignment.toId)!;
  const positions: Record<Building['kind'], readonly [number, number]> = {
    'town-hall': [0, 0],
    mine: [8, 0],
    warehouse: [8, 6],
    market: [0, 6],
  };
  const [fromX, fromY] = positions[source.kind];
  const [toX, toY] = positions[destination.kind];
  const phaseDuration = assignment.phase === 'loading' || assignment.phase === 'unloading'
    ? 250
    : ((Math.abs(toX - fromX) + Math.abs(toY - fromY)) * 1_000 / unit.speed);
  return assignment.progressMs < phaseDuration;
}

function hasExclusiveItemOwnership(state: GameState): boolean {
  const items = new Map<string, Item>(state.items.map((item) => [item.id, item]));
  const ownership = new Map<string, number>();
  const own = (id: string): void => {
    ownership.set(id, (ownership.get(id) ?? 0) + 1);
  };
  state.colonyInventory.itemIds.forEach(own);
  for (const unit of state.units) {
    const slots = [
      ['weapon', unit.equipment.weaponId],
      ['armor', unit.equipment.armorId],
    ] as const;
    for (const [slot, id] of slots) {
      if (id === null) continue;
      if (items.get(id)?.slot !== slot) return false;
      own(id);
    }
  }
  return [...items.keys()].every((id) => ownership.get(id) === 1)
    && [...ownership.keys()].every((id) => items.has(id));
}

function hasConsistentMission(state: GameState): boolean {
  const run = state.mission.activeRun;
  const assigned = state.units.filter(({ assignment }) => assignment.kind === 'mission');
  const expectedAppliedCount = state.mission.nextRunSequence - (run === null ? 1 : 2);
  if (
    expectedAppliedCount < 0
    || state.mission.appliedRunIds.length !== expectedAppliedCount
    || !state.mission.appliedRunIds.every((runId, index) => (
      runId === deriveMissionRunIdentity(MISSION_ONE.id, state.campaignSeed, index + 1).runId
    ))
  ) {
    return false;
  }
  if (run === null) {
    return assigned.length === 0;
  }
  if (
    run.status !== 'active'
    || run.missionId !== MISSION_ONE.id
    || state.mission.appliedRunIds.includes(run.runId)
    || run.combatSeed !== run.combatInput.seed
    || !validFormation(run.formation)
  ) {
    return false;
  }
  const runSequence = state.mission.nextRunSequence - 1;
  if (runSequence < 1) {
    return false;
  }
  const expectedIdentity = deriveMissionRunIdentity(
    run.missionId,
    state.campaignSeed,
    runSequence,
  );
  if (run.runId !== expectedIdentity.runId || run.combatSeed !== expectedIdentity.combatSeed) {
    return false;
  }
  const formationIds = new Set(run.formation.unitIds);
  if (
    assigned.length !== formationIds.size
    || assigned.some((unit) => unit.assignment.kind !== 'mission' || unit.assignment.runId !== run.runId || !formationIds.has(unit.id))
  ) {
    return false;
  }
  if (!validCanonicalCombatInput(state, run)) {
    return false;
  }
  return JSON.stringify(simulateCombat(run.combatInput)) === JSON.stringify(run.combatReport);
}

function validFormation(formation: Formation): boolean {
  if (
    formation.unitIds.length < 1
    || formation.unitIds.length > 4
    || formation.unitIds.length !== formation.slots.length
    || hasDuplicates(formation.unitIds)
  ) {
    return false;
  }
  const cells = formation.slots.map(({ x, y }) => `${x}:${y}`);
  return !hasDuplicates(cells) && formation.slots.every(({ x, y }) => (
    Number.isInteger(x) && Number.isInteger(y) && x >= 0 && x < 6 && y >= 2 && y < 4
  ));
}

function validCanonicalCombatInput(state: GameState, run: ActiveMissionRun): boolean {
  const input = run.combatInput;
  if (input.maxDurationMs !== 120_000 || input.playerSlots.length !== run.formation.unitIds.length) {
    return false;
  }
  const items = new Map(state.items.map((item) => [item.id, item]));
  const units = new Map(state.units.map((unit) => [unit.id, unit]));
  const playersValid = input.playerSlots.every((slot, index) => {
    const id = run.formation.unitIds[index];
    const position = run.formation.slots[index];
    const unit = id === undefined ? undefined : units.get(id);
    if (unit === undefined || position === undefined || slot.combatant.side !== 'player') return false;
    const weaponBonus = unit.equipment.weaponId === null ? 0 : (items.get(unit.equipment.weaponId)?.damageBonus ?? Number.NaN);
    const armorBonus = unit.equipment.armorId === null ? 0 : (items.get(unit.equipment.armorId)?.armorBonus ?? Number.NaN);
    return slot.x === position.x
      && slot.y === position.y
      && slot.combatant.id === `player-${unit.id}`
      && slot.combatant.unitId === unit.id
      && slot.combatant.maxHealth === unit.health
      && slot.combatant.speed === unit.speed
      && slot.combatant.damage === unit.damage + weaponBonus
      && slot.combatant.armor === unit.armor + armorBonus
      && slot.combatant.attackIntervalMs === unit.attackIntervalMs;
  });
  return playersValid && validCanonicalEnemies(input.enemySlots);
}

function validCanonicalEnemies(slots: CombatSlot[]): boolean {
  const definition = UNIT_CATALOG[MISSION_ONE.enemySpecies];
  const cells = MISSION_ONE.enemyCells;
  return slots.length === cells.length && slots.every((slot, index) => {
    const cell = cells[index]!;
    const combatant = slot.combatant;
    return slot.x === cell.x
      && slot.y === cell.y
      && combatant.side === 'enemy'
      && combatant.id === `${MISSION_ONE.id}-enemy-${index + 1}`
      && combatant.unitId === null
      && combatant.maxHealth === definition.maxHealth
      && combatant.speed === definition.speed
      && combatant.damage === definition.damage
      && combatant.armor === definition.armor
      && combatant.attackIntervalMs === definition.attackIntervalMs;
  });
}

function parseArray<T>(value: unknown, parse: (entry: unknown) => T | null): T[] | null {
  if (!Array.isArray(value)) return null;
  const parsed = value.map(parse);
  return parsed.some((entry) => entry === null) ? null : parsed as T[];
}

function parseStringArray(value: unknown): string[] | null {
  return Array.isArray(value) && value.every(isString) ? [...value] : null;
}

function parseStringArrayFromRecord(value: unknown, key: string): string[] | null {
  return isRecord(value) ? parseStringArray(value[key]) : null;
}

function nullableString(value: unknown): string | null | undefined {
  return value === null || isString(value) ? value : undefined;
}

function repairedCounter(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? Math.max(0, value) : null;
}

function nonNegativeFinite(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : null;
}

function positiveFinite(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : null;
}

function positiveInteger(value: unknown): number | null {
  return Number.isSafeInteger(value) && (value as number) > 0 ? value as number : null;
}

function uint32(value: unknown): number | null {
  return Number.isSafeInteger(value) && (value as number) >= 0 && (value as number) <= 0xffff_ffff
    ? value as number
    : null;
}

function finite(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function hasDuplicates(values: readonly string[]): boolean {
  return new Set(values).size !== values.length;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isString(value: unknown): value is string {
  return typeof value === 'string';
}

function invalidSave<T>(): DecodeResult<T> {
  return { ok: false, error: { code: 'INVALID_SAVE' } };
}
