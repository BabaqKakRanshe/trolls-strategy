import type {
  GameCommandContractMap,
  GameCommandOf,
  GameCommandType,
} from './GameSession';
import type { GameSnapshot } from './snapshot';
import { unitId, type BuildingId, type GridCell, type UnitId } from '../domain/model';
import { BUILDING_FOOTPRINTS, cellKey, footprintContains, sameCell } from '../domain/map/grid';
import type { CommandResult } from '../domain/results';

export interface MapRuntime {
  getSnapshot(): GameSnapshot;
  dispatch<TType extends GameCommandType>(
    command: GameCommandOf<TType>,
  ): CommandResult<GameCommandContractMap[TType]['value']>;
}

export type InteractionMode =
  | 'idle'
  | 'spawn-goblin'
  | 'place-mine'
  | 'target-work'
  | 'target-haul-source'
  | 'target-haul-destination';

export interface StackPickerState {
  cell: GridCell;
  unitIds: readonly UnitId[];
  quantity: number;
}

export interface InteractionViewState {
  mode: InteractionMode;
  selectedUnitIds: readonly UnitId[];
  commandsOpen: boolean;
  stackPicker: StackPickerState | null;
  message: string;
}

export class MapInteractionController {
  private mode: InteractionMode = 'idle';
  private selectedUnitIds: UnitId[] = [];
  private commandsOpen = false;
  private stackPicker: StackPickerState | null = null;
  private haulSourceId: BuildingId | null = null;
  private message = '';
  private nextGoblinSequence: number;
  private readonly listeners = new Set<() => void>();

  public constructor(private readonly runtime: MapRuntime) {
    this.nextGoblinSequence = runtime.getSnapshot().units.length + 1;
  }

  public getSnapshot(): GameSnapshot { return this.runtime.getSnapshot(); }

  public getViewState(): InteractionViewState {
    return {
      mode: this.mode,
      selectedUnitIds: [...this.selectedUnitIds],
      commandsOpen: this.commandsOpen,
      stackPicker: this.stackPicker === null ? null : {
        cell: { ...this.stackPicker.cell },
        unitIds: [...this.stackPicker.unitIds],
        quantity: this.stackPicker.quantity,
      },
      message: this.message,
    };
  }

  public subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  public beginGoblinPlacement(): void {
    this.setMode('spawn-goblin', 'Выберите клетку для гоблина');
  }

  public beginMinePlacement(): void {
    this.setMode('place-mine', 'Выберите свободную клетку для шахты');
  }

  public beginWorkTarget(): void {
    if (!this.hasSelection()) return;
    this.setMode('target-work', 'Выберите шахту');
  }

  public beginHaulTarget(): void {
    if (!this.hasSelection()) return;
    this.haulSourceId = null;
    this.setMode('target-haul-source', 'Выберите здание-источник');
  }

  public cancelMode(): void {
    this.haulSourceId = null;
    this.setMode('idle', 'Действие отменено');
  }

  public openCommands(): void {
    this.commandsOpen = true;
    this.notify();
  }

  public sellSelected(): void {
    const result = this.runtime.dispatch({ type: 'SELL_UNITS', unitIds: this.selectedUnitIds });
    if (result.ok) {
      this.selectedUnitIds = [];
      this.commandsOpen = false;
      this.message = `Продано за ${result.value} золота`;
    } else this.message = errorMessage(result.error.code);
    this.notify();
  }

  public sendSelectedToBarracks(): void {
    const result = this.runtime.dispatch({
      type: 'SEND_TO_BARRACKS', unitIds: this.selectedUnitIds,
    });
    if (result.ok) this.finishCommand('Юниты отправлены в бараки');
    else { this.message = errorMessage(result.error.code); this.notify(); }
  }

  public handleCellClick(cell: GridCell): void {
    if (this.mode === 'spawn-goblin') return this.spawnGoblin(cell);
    if (this.mode === 'place-mine') return this.placeMine(cell);
    if (this.mode === 'target-work') return this.assignWork(cell);
    if (this.mode === 'target-haul-source') return this.chooseHaulSource(cell);
    if (this.mode === 'target-haul-destination') return this.chooseHaulDestination(cell);
    this.selectCell(cell);
  }

  public handleBoxSelection(from: GridCell, to: GridCell): void {
    if (this.mode !== 'idle') return;
    const minX = Math.min(from.x, to.x);
    const maxX = Math.max(from.x, to.x);
    const minY = Math.min(from.y, to.y);
    const maxY = Math.max(from.y, to.y);
    this.selectedUnitIds = this.runtime.getSnapshot().units
      .filter((unit) => unit.mapCell !== null
        && unit.mapCell.x >= minX && unit.mapCell.x <= maxX
        && unit.mapCell.y >= minY && unit.mapCell.y <= maxY)
      .map((unit) => unit.id);
    this.stackPicker = null;
    this.commandsOpen = false;
    this.message = this.selectedUnitIds.length === 0
      ? 'В области нет юнитов'
      : `Выбрано юнитов: ${this.selectedUnitIds.length}`;
    this.notify();
  }

  public setStackQuantity(quantity: number): void {
    if (this.stackPicker === null) return;
    this.stackPicker = {
      ...this.stackPicker,
      quantity: Math.max(1, Math.min(this.stackPicker.unitIds.length, Math.round(quantity))),
    };
  }

  public confirmStackSelection(): void {
    if (this.stackPicker === null) return;
    this.selectedUnitIds = this.stackPicker.unitIds.slice(0, this.stackPicker.quantity) as UnitId[];
    this.stackPicker = null;
    this.commandsOpen = false;
    this.message = `Выбрано юнитов: ${this.selectedUnitIds.length}`;
    this.notify();
  }

  private selectCell(cell: GridCell): void {
    const ids = this.runtime.getSnapshot().units
      .filter((unit) => sameCell(unit.mapCell, cell))
      .map((unit) => unit.id);
    this.commandsOpen = false;
    if (ids.length > 1) {
      this.stackPicker = { cell: { ...cell }, unitIds: ids, quantity: ids.length };
      this.message = `В клетке ${ids.length} юнитов`;
    } else {
      this.stackPicker = null;
      this.selectedUnitIds = ids;
      this.message = ids.length === 1 ? 'Выбран 1 юнит' : 'Выделение снято';
    }
    this.notify();
  }

  private spawnGoblin(cell: GridCell): void {
    const sequence = this.nextGoblinSequence;
    const result = this.runtime.dispatch({
      type: 'SPAWN_UNIT', species: 'goblin', id: unitId(`goblin-${sequence}`),
      name: `Гоблин ${sequence}`, cell,
    });
    if (result.ok) {
      this.nextGoblinSequence += 1;
      this.mode = 'idle';
      this.message = `Гоблин создан в клетке ${cellKey(cell)}`;
    } else this.message = errorMessage(result.error.code);
    this.notify();
  }

  private placeMine(cell: GridCell): void {
    const result = this.runtime.dispatch({ type: 'PURCHASE_MINE', cell });
    if (result.ok) {
      this.mode = 'idle';
      this.message = 'Шахта построена';
    } else this.message = errorMessage(result.error.code);
    this.notify();
  }

  private assignWork(cell: GridCell): void {
    const mine = this.buildingAt(cell);
    if (mine?.kind !== 'mine' || !this.runtime.getSnapshot().unlocks.includes('building:mine')) {
      this.message = 'Нужно выбрать построенную шахту';
      return this.notify();
    }
    const result = this.runtime.dispatch({
      type: 'ASSIGN_WORKERS', unitIds: this.selectedUnitIds, buildingId: mine.id,
    });
    if (result.ok) this.finishCommand('Юниты отправлены работать');
    else { this.message = errorMessage(result.error.code); this.notify(); }
  }

  private chooseHaulSource(cell: GridCell): void {
    const source = this.buildingAt(cell);
    if (!source) { this.message = 'Выберите здание'; return this.notify(); }
    this.haulSourceId = source.id;
    this.setMode('target-haul-destination', 'Теперь выберите здание назначения');
  }

  private chooseHaulDestination(cell: GridCell): void {
    const destination = this.buildingAt(cell);
    if (!destination || this.haulSourceId === null) {
      this.message = 'Выберите здание назначения'; return this.notify();
    }
    const result = this.runtime.dispatch({
      type: 'ASSIGN_HAULERS', unitIds: this.selectedUnitIds,
      fromId: this.haulSourceId, toId: destination.id,
    });
    if (result.ok) this.finishCommand('Маршрут переноса назначен');
    else { this.message = errorMessage(result.error.code); this.notify(); }
  }

  private buildingAt(cell: GridCell) {
    return this.runtime.getSnapshot().buildings.find((building) => (
      footprintContains(building.mapCell, BUILDING_FOOTPRINTS[building.kind], cell)
    ));
  }

  private finishCommand(message: string): void {
    this.mode = 'idle';
    this.commandsOpen = false;
    this.haulSourceId = null;
    this.message = message;
    this.notify();
  }

  private hasSelection(): boolean {
    if (this.selectedUnitIds.length > 0) return true;
    this.message = 'Сначала выберите юнитов';
    this.notify();
    return false;
  }

  private setMode(mode: InteractionMode, message: string): void {
    this.mode = mode;
    this.message = message;
    this.notify();
  }

  private notify(): void { this.listeners.forEach((listener) => listener()); }
}

export function errorMessage(code: string): string {
  if (code === 'INSUFFICIENT_GOLD') return 'Недостаточно золота';
  if (code === 'ALREADY_APPLIED') return 'Шахта уже построена';
  if (code === 'CELL_FULL') return 'В клетке уже 10 существ';
  if (code === 'CELL_OCCUPIED') return 'Клетка занята постройкой';
  if (code === 'CAPACITY_EXCEEDED') return 'На шахте нет свободных рабочих мест';
  if (code === 'INVALID_ASSIGNMENT') return 'Команда недоступна для выбранных юнитов';
  return 'Действие недоступно';
}
