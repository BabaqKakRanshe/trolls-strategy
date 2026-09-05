import type { ItemKind, UnitSpecies } from '../content/catalog';

declare const idBrand: unique symbol;

type BrandedId<TName extends string> = string & {
  readonly [idBrand]: TName;
};

export type UnitId = BrandedId<'UnitId'>;
export type BuildingId = BrandedId<'BuildingId'>;
export type ItemId = BrandedId<'ItemId'>;
export type MissionRunId = BrandedId<'MissionRunId'>;
export type CombatantId = BrandedId<'CombatantId'>;

export const unitId = (value: string): UnitId => value as UnitId;
export const buildingId = (value: string): BuildingId => value as BuildingId;
export const itemId = (value: string): ItemId => value as ItemId;
export const missionRunId = (value: string): MissionRunId => value as MissionRunId;
export const combatantId = (value: string): CombatantId => value as CombatantId;

export type ResourceKind = 'ironOre';
export type EquipmentSlot = 'weapon' | 'armor';
export type BuildingKind = 'town-hall' | 'mine' | 'warehouse' | 'market';
export type HaulPhase = 'toSource' | 'loading' | 'toDestination' | 'unloading';

export interface GridCell {
  x: number;
  y: number;
}

export type Assignment =
  | { kind: 'idle' }
  | { kind: 'work'; buildingId: BuildingId }
  | {
      kind: 'haul';
      fromId: BuildingId;
      toId: BuildingId;
      resource: ResourceKind;
      carried: number;
      phase: HaulPhase;
      progressMs: number;
    }
  | { kind: 'mission'; runId: MissionRunId }
  | { kind: 'dead' };

export interface Equipment {
  weaponId: ItemId | null;
  armorId: ItemId | null;
}

export interface Unit {
  id: UnitId;
  species: UnitSpecies;
  name: string;
  level: number;
  health: number;
  maxHealth: number;
  strength: number;
  speed: number;
  carryCapacity: number;
  attackIntervalMs: number;
  damage: number;
  armor: number;
  assignment: Assignment;
  equipment: Equipment;
  mapCell: GridCell | null;
}

export interface Item {
  id: ItemId;
  kind: ItemKind;
  slot: EquipmentSlot;
  damageBonus: number;
  armorBonus: number;
}

export interface ColonyInventory {
  itemIds: ItemId[];
}

export interface Inventory {
  ironOre: number;
}

export interface Building {
  id: BuildingId;
  kind: BuildingKind;
  inventory: Inventory;
  mapCell: GridCell | null;
}

export interface FormationSlot {
  x: number;
  y: number;
}

export interface Formation {
  unitIds: UnitId[];
  slots: FormationSlot[];
}

export type CombatSide = 'player' | 'enemy';

export interface BaseCombatantSpec {
  id: CombatantId;
  maxHealth: number;
  speed: number;
  damage: number;
  armor: number;
  attackIntervalMs: number;
}

export interface PlayerCombatantSpec extends BaseCombatantSpec {
  side: 'player';
  unitId: UnitId;
}

export interface EnemyCombatantSpec extends BaseCombatantSpec {
  side: 'enemy';
  unitId: null;
}

export type CombatantSpec = PlayerCombatantSpec | EnemyCombatantSpec;

export interface CombatSlot extends FormationSlot {
  combatant: CombatantSpec;
}

export interface CombatInput {
  seed: number;
  maxDurationMs: number;
  playerSlots: CombatSlot[];
  enemySlots: CombatSlot[];
}

export interface CombatantFrame {
  id: CombatantId;
  x: number;
  y: number;
  health: number;
}

export interface CombatFrame {
  timeMs: number;
  combatants: CombatantFrame[];
}

export type CombatWinner = CombatSide | 'draw';

export interface CombatReport {
  winner: CombatWinner;
  durationMs: number;
  frames: CombatFrame[];
  deadPlayerUnitIds: UnitId[];
  deadEnemyCombatantIds: CombatantId[];
}

export type ActiveMissionStatus = 'active' | 'resolved' | 'aborted';

export interface ActiveMissionRun {
  runId: MissionRunId;
  missionId: string;
  formation: Formation;
  combatSeed: number;
  combatInput: CombatInput;
  combatReport: CombatReport;
  status: ActiveMissionStatus;
}

export interface MissionState {
  nextMissionAtMs: number;
  nextRunSequence: number;
  appliedRunIds: MissionRunId[];
  clearedMissionIds: string[];
  activeRun: ActiveMissionRun | null;
}

export interface GameState {
  schemaVersion: 1;
  campaignSeed: number;
  simulationTimeMs: number;
  accumulators: {
    mineOre: number;
  };
  wallet: {
    gold: number;
  };
  units: Unit[];
  items: Item[];
  colonyInventory: ColonyInventory;
  buildings: Building[];
  mission: MissionState;
  unlocks: string[];
}
