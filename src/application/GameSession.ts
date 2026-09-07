import {
  BUILDING_CATALOG,
  ECONOMY_STEP_MS,
  UNIT_CATALOG,
  type BuildingKind,
  type UnitKind,
} from '../content/catalog';
import {
  applyCommand,
  createInitialState,
  findFirstValidMineCell,
  productionPerSecond,
  tickColony,
  validateMinePlacement,
  validateUnitPurchase,
  type Assignment,
  type Cell,
  type CommandResult,
  type GameCommand,
  type GameState,
  type WorldPosition,
} from '../domain/colony';

export interface BuildingSnapshot {
  readonly id: string;
  readonly kind: BuildingKind;
  readonly name: string;
  readonly cell: Cell;
  readonly width: number;
  readonly height: number;
  readonly ore: number;
  readonly maxOre: number;
  readonly workerCount: number;
  readonly maxWorkers: number;
  readonly productionPerSecond: number;
}

export interface UnitSnapshot {
  readonly id: string;
  readonly number: number;
  readonly unitKind: UnitKind;
  readonly name: string;
  readonly strength: number;
  readonly speed: number;
  readonly cargoCapacity: number;
  readonly position: Readonly<WorldPosition>;
  readonly assignment: Readonly<Assignment>;
  readonly status: string;
}

export interface GameSnapshot {
  readonly revision: number;
  readonly gold: number;
  readonly soldOre: number;
  readonly totalOre: number;
  readonly buildings: readonly BuildingSnapshot[];
  readonly units: readonly UnitSnapshot[];
}

type SnapshotListener = (snapshot: GameSnapshot) => void;
export type PublicCommandResult = Exclude<CommandResult<void>, { ok: true }> | { readonly ok: true };

export class GameSession {
  readonly #listeners = new Set<SnapshotListener>();
  #state: GameState = createInitialState();
  #remainderMs = 0;
  #revision = 0;

  dispatch(command: GameCommand): PublicCommandResult {
    const result = applyCommand(this.#state, command);
    if (!result.ok) return result;
    this.#state = result.value;
    this.#revision += 1;
    this.#emit();
    return { ok: true };
  }

  advance(deltaMs: number): GameSnapshot {
    this.#remainderMs += Math.max(0, deltaMs);
    let changed = false;
    while (this.#remainderMs >= ECONOMY_STEP_MS) {
      this.#state = tickColony(this.#state);
      this.#remainderMs -= ECONOMY_STEP_MS;
      changed = true;
    }
    if (changed) {
      this.#revision += 1;
      this.#emit();
    }
    return this.snapshot();
  }

  snapshot(): GameSnapshot {
    const buildings = this.#state.buildings.map((building) => {
      const definition = BUILDING_CATALOG[building.kind];
      const workerCount = this.#state.units.filter(
        (unit) =>
          (unit.assignment.kind === 'to-work' || unit.assignment.kind === 'work') &&
          unit.assignment.buildingId === building.id,
      ).length;
      return {
        id: building.id,
        kind: building.kind,
        cell: { ...building.cell },
        ore: building.ore,
        name: numberedBuildingName(building.kind, building.id),
        width: definition.width,
        height: definition.height,
        maxOre: definition.maxOre,
        workerCount,
        maxWorkers: definition.maxWorkers,
        productionPerSecond: productionPerSecond(this.#state, building.id),
      };
    });
    const units = this.#state.units.map((unit) => {
      const definition = UNIT_CATALOG[unit.unitKind];
      return {
        ...structuredClone(unit),
        number: Number(unit.id.split('-')[1]),
        name: definition.name,
        strength: definition.strength,
        speed: definition.speed,
        cargoCapacity: definition.cargo,
        status: assignmentStatus(unit.assignment, buildings),
      };
    });
    const carriedOre = units.reduce(
      (sum, unit) => sum + (unit.assignment.kind === 'haul' ? unit.assignment.carried : 0),
      0,
    );
    return {
      revision: this.#revision,
      gold: this.#state.gold,
      soldOre: this.#state.soldOre,
      totalOre: buildings.reduce((sum, building) => sum + building.ore, carriedOre),
      buildings,
      units,
    };
  }

  findFirstMineCell(): Cell | null {
    return findFirstValidMineCell(this.#state);
  }

  canBuildMine(cell: Cell): PublicCommandResult {
    const result = validateMinePlacement(this.#state, cell);
    return result.ok ? { ok: true } : result;
  }

  canBuyUnits(unitKind: UnitKind, amount: number, cell: Cell): PublicCommandResult {
    const result = validateUnitPurchase(this.#state, unitKind, amount, cell);
    return result.ok ? { ok: true } : result;
  }

  subscribe(listener: SnapshotListener): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  #emit(): void {
    const snapshot = this.snapshot();
    this.#listeners.forEach((listener) => listener(snapshot));
  }
}

function numberedBuildingName(kind: BuildingKind, id: string): string {
  if (kind !== 'mine') return BUILDING_CATALOG[kind].name;
  return `Шахта ${id.split('-')[1]}`;
}

function assignmentStatus(assignment: Assignment, buildings: readonly BuildingSnapshot[]): string {
  if (assignment.kind === 'idle') return 'Свободен';
  if (assignment.kind === 'to-work') {
    return `Идёт в ${buildings.find((building) => building.id === assignment.buildingId)?.name ?? 'шахту'}`;
  }
  if (assignment.kind === 'work') {
    return `Работает: ${buildings.find((building) => building.id === assignment.buildingId)?.name ?? 'шахта'}`;
  }
  const source = buildings.find((building) => building.id === assignment.sourceId)?.name ?? 'источник';
  const destination = buildings.find((building) => building.id === assignment.destinationId)?.name ?? 'цель';
  return `Несёт: ${source} → ${destination}`;
}
