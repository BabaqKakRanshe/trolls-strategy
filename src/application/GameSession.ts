import { BUILDING_CATALOG, UNIT_CATALOG, type UnitSpecies } from '../content/catalog';
import { MISSION_ONE } from '../content/missionCatalog';
import {
  assignHauler,
  assignWorkers,
  equipItem,
  recruitUnit,
  setIdle,
  unequipItem,
} from '../domain/colony/commands';
import { tickColony } from '../domain/colony/tickColony';
import { simulateCombat } from '../domain/combat/simulateCombat';
import { createInitialState } from '../domain/initialState';
import type {
  ActiveMissionRun,
  BuildingId,
  CombatInput,
  CombatSlot,
  EquipmentSlot,
  Formation,
  GameState,
  ItemId,
  MissionRunId,
  Unit,
  UnitId,
} from '../domain/model';
import {
  combatantId,
} from '../domain/model';
import {
  getEmptyReward,
  getMissionOneReward,
  type Reward,
} from '../domain/progression/rewards';
import { deriveMissionRunIdentity } from '../domain/progression/missionIdentity';
import type { CommandResult } from '../domain/results';
import { decodeSave } from '../persistence/saveCodec';
import { createSnapshot, type GameSnapshot } from './snapshot';

const FIXED_STEP_MS = 250;
const MAX_FRAME_DELTA_MS = 5_000;

export interface GameCommandContractMap {
  RECRUIT_UNIT: {
    payload: { species: UnitSpecies; id: UnitId; name: string };
    value: UnitId;
  };
  ASSIGN_WORKERS: {
    payload: { unitIds: readonly UnitId[]; buildingId: BuildingId };
    value: void;
  };
  ASSIGN_HAULER: {
    payload: { unitId: UnitId; fromId: BuildingId; toId: BuildingId };
    value: void;
  };
  SET_IDLE: { payload: { unitIds: readonly UnitId[] }; value: void };
  EQUIP_ITEM: { payload: { unitId: UnitId; itemId: ItemId }; value: void };
  UNEQUIP_ITEM: {
    payload: { unitId: UnitId; slot: EquipmentSlot };
    value: void;
  };
  SELL_ORE: { payload: { buildingId: BuildingId; amount: number }; value: number };
  PURCHASE_MINE: { payload: Record<never, never>; value: void };
  START_MISSION: {
    payload: { missionId: typeof MISSION_ONE.id; formation: Formation };
    value: MissionRunId;
  };
  RESOLVE_MISSION: { payload: { runId: MissionRunId }; value: Reward };
  ABORT_MISSION: { payload: { runId: MissionRunId }; value: void };
}

export type GameCommandType = keyof GameCommandContractMap;
export type GameCommandOf<TType extends GameCommandType> = {
  type: TType;
} & GameCommandContractMap[TType]['payload'];

export type GameCommand = {
  [TType in GameCommandType]: GameCommandOf<TType>;
}[GameCommandType];

type GameCommandHandlerMap = {
  [TType in GameCommandType]: (
    command: GameCommandOf<TType>,
  ) => CommandResult<GameCommandContractMap[TType]['value']>;
};

export class GameSession {
  private accumulatedMs = 0;
  private revision = 0;
  private cachedSnapshot: GameSnapshot | null = null;

  private readonly commandHandlers = {
    RECRUIT_UNIT: (command: GameCommandOf<'RECRUIT_UNIT'>) => (
      recruitUnit(this.state, command.species, command.id, command.name)
    ),
    ASSIGN_WORKERS: (command: GameCommandOf<'ASSIGN_WORKERS'>) => (
      assignWorkers(this.state, command.unitIds, command.buildingId)
    ),
    ASSIGN_HAULER: (command: GameCommandOf<'ASSIGN_HAULER'>) => (
      assignHauler(this.state, command.unitId, command.fromId, command.toId)
    ),
    SET_IDLE: (command: GameCommandOf<'SET_IDLE'>) => (
      setIdle(this.state, command.unitIds)
    ),
    EQUIP_ITEM: (command: GameCommandOf<'EQUIP_ITEM'>) => (
      equipItem(this.state, command.unitId, command.itemId)
    ),
    UNEQUIP_ITEM: (command: GameCommandOf<'UNEQUIP_ITEM'>) => (
      unequipItem(this.state, command.unitId, command.slot)
    ),
    SELL_ORE: (command: GameCommandOf<'SELL_ORE'>) => (
      this.sellOre(command.buildingId, command.amount)
    ),
    PURCHASE_MINE: () => this.purchaseMine(),
    START_MISSION: (command: GameCommandOf<'START_MISSION'>) => (
      this.startMission(command.missionId, command.formation)
    ),
    RESOLVE_MISSION: (command: GameCommandOf<'RESOLVE_MISSION'>) => (
      this.resolveMission(command.runId)
    ),
    ABORT_MISSION: (command: GameCommandOf<'ABORT_MISSION'>) => (
      this.abortMission(command.runId)
    ),
  } satisfies GameCommandHandlerMap;

  private constructor(private readonly state: GameState) {}

  static createNew(seed?: number): GameSession {
    return new GameSession(createInitialState(seed));
  }

  static fromSave(raw: string): CommandResult<GameSession> {
    const decoded = decodeSave(raw);
    return decoded.ok
      ? { ok: true, value: new GameSession(decoded.value) }
      : decoded;
  }

  dispatch<TType extends GameCommandType>(
    command: GameCommandOf<TType>,
  ): CommandResult<GameCommandContractMap[TType]['value']> {
    const handler = this.commandHandlers[command.type] as GameCommandHandlerMap[TType];
    const result = handler(command);
    if (result.ok) {
      this.revision += 1;
      this.cachedSnapshot = null;
    }
    return result;
  }

  advance(realDeltaMs: number): void {
    if (!Number.isFinite(realDeltaMs) || realDeltaMs <= 0) {
      return;
    }

    this.accumulatedMs += Math.min(realDeltaMs, MAX_FRAME_DELTA_MS);
    const elapsedSteps = Math.floor(this.accumulatedMs / FIXED_STEP_MS);
    if (elapsedSteps === 0) {
      return;
    }

    const simulatedDeltaMs = elapsedSteps * FIXED_STEP_MS;
    this.accumulatedMs -= simulatedDeltaMs;
    tickColony(this.state, simulatedDeltaMs);
    this.revision += elapsedSteps;
    this.cachedSnapshot = null;
  }

  getSnapshot(): GameSnapshot {
    this.cachedSnapshot ??= createSnapshot(this.state, this.revision);
    return this.cachedSnapshot;
  }

  getRevision(): number {
    return this.revision;
  }

  getSimulationTimeMs(): number {
    return this.state.simulationTimeMs;
  }

  exportState(): GameState {
    return structuredClone(this.state);
  }

  private sellOre(buildingId: BuildingId, amount: number): CommandResult<number> {
    if (!Number.isSafeInteger(amount) || amount <= 0) {
      return { ok: false, error: { code: 'INVALID_AMOUNT' } };
    }

    const building = this.state.buildings.find((candidate) => candidate.id === buildingId);
    if (!building) {
      return { ok: false, error: { code: 'NOT_FOUND' } };
    }
    if (building.kind !== 'warehouse') {
      return { ok: false, error: { code: 'INVALID_ASSIGNMENT' } };
    }
    if (building.inventory.ironOre < amount) {
      return { ok: false, error: { code: 'INSUFFICIENT_RESOURCES' } };
    }

    const proceeds = amount * BUILDING_CATALOG.market.oreSalePrice;
    building.inventory.ironOre -= amount;
    this.state.wallet.gold += proceeds;
    return { ok: true, value: proceeds };
  }

  private purchaseMine(): CommandResult<void> {
    const definition = BUILDING_CATALOG.mine;
    if (this.state.unlocks.includes(definition.unlockId)) {
      return { ok: false, error: { code: 'ALREADY_APPLIED' } };
    }
    if (this.state.wallet.gold < definition.purchaseCost) {
      return { ok: false, error: { code: 'INSUFFICIENT_GOLD' } };
    }

    this.state.wallet.gold -= definition.purchaseCost;
    this.state.unlocks.push(definition.unlockId);
    return { ok: true, value: undefined };
  }

  private startMission(
    missionId: typeof MISSION_ONE.id,
    formation: Formation,
  ): CommandResult<MissionRunId> {
    if (missionId !== MISSION_ONE.id) {
      return { ok: false, error: { code: 'INVALID_MISSION' } };
    }
    if (
      this.state.mission.activeRun !== null
      || this.state.simulationTimeMs < this.state.mission.nextMissionAtMs
    ) {
      return { ok: false, error: { code: 'MISSION_UNAVAILABLE' } };
    }

    const formationUnits = validateFormation(this.state, formation);
    if (!formationUnits.ok) {
      return formationUnits;
    }

    const sequence = this.state.mission.nextRunSequence;
    const { runId, combatSeed, nextSequence } = createUniqueRunIdentity(this.state, sequence);
    const canonicalFormation = structuredClone(formation);
    const combatInput = createMissionOneCombatInput(
      this.state,
      canonicalFormation,
      formationUnits.value,
      combatSeed,
    );
    const activeRun: ActiveMissionRun = {
      runId,
      missionId,
      formation: canonicalFormation,
      combatSeed,
      combatInput,
      combatReport: simulateCombat(combatInput),
      status: 'active',
    };

    for (const unit of formationUnits.value) {
      unit.assignment = { kind: 'mission', runId };
    }
    this.state.mission.nextRunSequence = nextSequence;
    this.state.mission.activeRun = activeRun;
    return { ok: true, value: runId };
  }

  private resolveMission(runId: MissionRunId): CommandResult<Reward> {
    return this.settleMission(runId);
  }

  private abortMission(runId: MissionRunId): CommandResult<void> {
    const result = this.settleMission(runId);
    return result.ok
      ? { ok: true, value: undefined }
      : result;
  }

  private settleMission(runId: MissionRunId): CommandResult<Reward> {
    if (this.state.mission.appliedRunIds.includes(runId)) {
      return { ok: false, error: { code: 'ALREADY_APPLIED' } };
    }

    const activeRun = this.state.mission.activeRun;
    if (activeRun === null || activeRun.runId !== runId || activeRun.status !== 'active') {
      return { ok: false, error: { code: 'INVALID_MISSION' } };
    }

    const deadUnitIds = new Set(activeRun.combatReport.deadPlayerUnitIds);
    const participatingUnitIds = new Set(activeRun.formation.unitIds);
    const equippedItemsToDelete = new Set<ItemId>();
    for (const unit of this.state.units) {
      if (!deadUnitIds.has(unit.id) || !participatingUnitIds.has(unit.id)) {
        continue;
      }
      if (unit.equipment.weaponId !== null) {
        equippedItemsToDelete.add(unit.equipment.weaponId);
      }
      if (unit.equipment.armorId !== null) {
        equippedItemsToDelete.add(unit.equipment.armorId);
      }
    }

    this.state.units = this.state.units.filter((unit) => {
      if (deadUnitIds.has(unit.id) && participatingUnitIds.has(unit.id)) {
        return false;
      }
      if (participatingUnitIds.has(unit.id)) {
        unit.assignment = { kind: 'idle' };
      }
      return true;
    });
    this.state.items = this.state.items.filter((item) => !equippedItemsToDelete.has(item.id));
    this.state.colonyInventory.itemIds = this.state.colonyInventory.itemIds.filter(
      (itemId) => !equippedItemsToDelete.has(itemId),
    );

    const won = activeRun.combatReport.winner === 'player';
    const firstClear = !this.state.mission.clearedMissionIds.includes(activeRun.missionId);
    const reward = won ? getMissionOneReward(firstClear) : getEmptyReward();
    if (won) {
      this.applyReward(reward);
      if (firstClear) {
        this.state.mission.clearedMissionIds.push(activeRun.missionId);
      }
    }

    this.state.mission.appliedRunIds.push(runId);
    this.state.mission.nextMissionAtMs = this.state.simulationTimeMs + MISSION_ONE.cooldownMs;
    this.state.mission.activeRun = null;
    return { ok: true, value: reward };
  }

  private applyReward(reward: Reward): void {
    this.state.wallet.gold += reward.gold;
    const warehouse = this.state.buildings.find((building) => building.kind === 'warehouse');
    const townHall = this.state.buildings.find((building) => building.kind === 'town-hall');
    if (warehouse !== undefined && townHall !== undefined) {
      const warehouseSpace = Math.max(
        0,
        BUILDING_CATALOG.warehouse.inventoryCapacity - warehouse.inventory.ironOre,
      );
      const warehouseDeposit = Math.min(reward.ironOre, warehouseSpace);
      warehouse.inventory.ironOre += warehouseDeposit;
      townHall.inventory.ironOre += reward.ironOre - warehouseDeposit;
    }
    for (const unlock of reward.unlocks) {
      if (!this.state.unlocks.includes(unlock)) {
        this.state.unlocks.push(unlock);
      }
    }
  }
}

function validateFormation(
  state: GameState,
  formation: Formation,
): CommandResult<Unit[]> {
  if (
    formation.unitIds.length < 1
    || formation.unitIds.length > MISSION_ONE.maxFormationSize
    || formation.unitIds.length !== formation.slots.length
    || new Set(formation.unitIds).size !== formation.unitIds.length
  ) {
    return { ok: false, error: { code: 'INVALID_FORMATION' } };
  }

  const cellKeys = formation.slots.map(({ x, y }) => `${x}:${y}`);
  if (
    new Set(cellKeys).size !== cellKeys.length
    || formation.slots.some(({ x, y }) => (
      !Number.isInteger(x)
      || !Number.isInteger(y)
      || x < 0
      || x >= 6
      || y < 2
      || y >= 4
    ))
  ) {
    return { ok: false, error: { code: 'INVALID_FORMATION' } };
  }

  const units = formation.unitIds.map((id) => state.units.find((unit) => unit.id === id));
  if (units.some((unit) => unit === undefined)) {
    return { ok: false, error: { code: 'INVALID_FORMATION' } };
  }
  const selectedUnits = units as Unit[];
  if (selectedUnits.some((unit) => unit.assignment.kind !== 'idle')) {
    return { ok: false, error: { code: 'INVALID_ASSIGNMENT' } };
  }
  return { ok: true, value: selectedUnits };
}

function createUniqueRunIdentity(
  state: GameState,
  sequence: number,
): { runId: MissionRunId; combatSeed: number; nextSequence: number } {
  let candidateSequence = sequence;
  let candidate = deriveMissionRunIdentity(
    MISSION_ONE.id,
    state.campaignSeed,
    candidateSequence,
  );
  const usedIds = new Set(state.mission.appliedRunIds);
  while (usedIds.has(candidate.runId) || state.mission.activeRun?.runId === candidate.runId) {
    candidateSequence += 1;
    candidate = deriveMissionRunIdentity(MISSION_ONE.id, state.campaignSeed, candidateSequence);
  }
  return {
    ...candidate,
    nextSequence: candidateSequence + 1,
  };
}

function createMissionOneCombatInput(
  state: GameState,
  formation: Formation,
  units: Unit[],
  seed: number,
): CombatInput {
  const playerSlots = units.map((unit, index) => ({
    ...formation.slots[index]!,
    combatant: {
      id: combatantId(`player-${unit.id}`),
      unitId: unit.id,
      side: 'player' as const,
      maxHealth: unit.health,
      speed: unit.speed,
      damage: unit.damage + equipmentBonus(state, unit.equipment.weaponId, 'damageBonus'),
      armor: unit.armor + equipmentBonus(state, unit.equipment.armorId, 'armorBonus'),
      attackIntervalMs: unit.attackIntervalMs,
    },
  }));

  return {
    seed,
    maxDurationMs: MISSION_ONE.maxDurationMs,
    playerSlots,
    enemySlots: createMissionOneEnemies(),
  };
}

function equipmentBonus(
  state: GameState,
  equippedItemId: ItemId | null,
  bonus: 'damageBonus' | 'armorBonus',
): number {
  if (equippedItemId === null) {
    return 0;
  }
  return state.items.find((item) => item.id === equippedItemId)?.[bonus] ?? 0;
}

function createMissionOneEnemies(): CombatSlot[] {
  const definition = UNIT_CATALOG[MISSION_ONE.enemySpecies];
  return MISSION_ONE.enemyCells.map((cell, index) => ({
    ...cell,
    combatant: {
      id: combatantId(`${MISSION_ONE.id}-enemy-${index + 1}`),
      unitId: null,
      side: 'enemy',
      maxHealth: definition.maxHealth,
      speed: definition.speed,
      damage: definition.damage,
      armor: definition.armor,
      attackIntervalMs: definition.attackIntervalMs,
    },
  }));
}
