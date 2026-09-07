import type { UnitKind } from '../content/catalog';
import { isValidHaulRoute, type Cell } from '../domain/colony';
import { GameSession } from './GameSession';

export type InteractionMode =
  | { readonly kind: 'neutral' }
  | { readonly kind: 'placing-mine' }
  | { readonly kind: 'placing-units'; readonly unitKind: UnitKind; readonly amount: number }
  | { readonly kind: 'choosing-work-target' }
  | { readonly kind: 'choosing-haul-source' }
  | { readonly kind: 'choosing-haul-destination'; readonly sourceId: string };

type InteractionListener = () => void;

export class MapInteractionController {
  readonly #listeners = new Set<InteractionListener>();
  readonly #selected = new Set<string>();
  #mode: InteractionMode = { kind: 'neutral' };
  #message = 'Постройте шахту и наймите рабочих.';
  #debugPanelOpen = false;
  #collisionDebugVisible = false;

  constructor(private readonly session: GameSession) {}

  get mode(): InteractionMode {
    return this.#mode;
  }

  get message(): string {
    return this.#message;
  }

  get debugPanelOpen(): boolean {
    return this.#debugPanelOpen;
  }

  get collisionDebugVisible(): boolean {
    return this.#collisionDebugVisible;
  }

  toggleDebugPanel(): void {
    this.#debugPanelOpen = !this.#debugPanelOpen;
    this.#emit();
  }

  setCollisionDebugVisible(visible: boolean): void {
    if (this.#collisionDebugVisible === visible) return;
    this.#collisionDebugVisible = visible;
    this.#emit();
  }

  selectedIds(): readonly string[] {
    return [...this.#selected];
  }

  targetBuildingIds(): readonly string[] {
    const buildings = this.session.snapshot().buildings;
    const mode = this.#mode;
    if (mode.kind === 'choosing-work-target') {
      return buildings.filter((building) => building.kind === 'mine').map((building) => building.id);
    }
    if (mode.kind === 'choosing-haul-source') {
      return buildings
        .filter((source) => buildings.some((destination) => isValidHaulRoute(source.kind, destination.kind)))
        .map((building) => building.id);
    }
    if (mode.kind === 'choosing-haul-destination') {
      const source = buildings.find((building) => building.id === mode.sourceId);
      if (!source) return [];
      return buildings
        .filter((destination) => destination.id !== source.id && isValidHaulRoute(source.kind, destination.kind))
        .map((building) => building.id);
    }
    return [];
  }

  clickUnit(unitId: string, additive: boolean): void {
    if (!additive) this.#selected.clear();
    if (additive && this.#selected.has(unitId)) this.#selected.delete(unitId);
    else this.#selected.add(unitId);
    this.#mode = { kind: 'neutral' };
    this.#message = this.#selected.size === 0 ? 'Выбор снят.' : `Выбрано юнитов: ${this.#selected.size}`;
    this.#emit();
  }

  selectUnits(unitIds: readonly string[]): void {
    this.#selected.clear();
    unitIds.forEach((id) => this.#selected.add(id));
    this.#mode = { kind: 'neutral' };
    this.#message = unitIds.length === 0 ? 'В рамке нет юнитов.' : `Выбрано рамкой: ${unitIds.length}`;
    this.#emit();
  }

  selectFirstIdle(amount: number): void {
    const idle = this.session
      .snapshot()
      .units.filter((unit) => unit.assignment.kind === 'idle')
      .slice(0, amount)
      .map((unit) => unit.id);
    this.selectUnits(idle);
  }

  selectNextIdle(): void {
    const idle = this.session.snapshot().units.find((unit) => unit.assignment.kind === 'idle');
    this.selectUnits(idle ? [idle.id] : []);
  }

  beginUnitPlacement(unitKind: UnitKind, amount: number): void {
    this.#mode = { kind: 'placing-units', unitKind, amount };
    this.#message = `Кликните по клетке, где появятся все ${amount} существ.`;
    this.#emit();
  }

  placeUnits(cell: Cell): void {
    if (this.#mode.kind !== 'placing-units') return;
    const { unitKind, amount } = this.#mode;
    const result = this.session.dispatch({ type: 'BUY_UNITS', unitKind, amount, cell });
    if (result.ok) {
      this.#mode = { kind: 'neutral' };
      this.#message = `Нанято существ: ${amount}.`;
    } else {
      this.#message = result.error;
    }
    this.#emit();
  }

  beginMinePlacement(): void {
    this.#mode = { kind: 'placing-mine' };
    this.#message = 'Выберите свободные клетки для шахты.';
    this.#emit();
  }

  placeMine(cell: Cell): void {
    const result = this.session.dispatch({ type: 'BUILD_MINE', cell });
    if (result.ok) {
      this.#mode = { kind: 'neutral' };
      this.#message = 'Шахта построена. Теперь назначьте рабочих.';
    } else {
      this.#message = result.error;
    }
    this.#emit();
  }

  placeMineAutomatically(): void {
    const cell = this.session.findFirstMineCell();
    if (cell) this.placeMine(cell);
    else {
      this.#message = 'На поле не осталось места для шахты.';
      this.#emit();
    }
  }

  beginWorkTarget(): void {
    if (!this.#hasSelection()) return;
    this.#mode = { kind: 'choosing-work-target' };
    this.#message = 'Укажите шахту для выбранных рабочих.';
    this.#emit();
  }

  beginHaulTarget(): void {
    if (!this.#hasSelection()) return;
    this.#mode = { kind: 'choosing-haul-source' };
    this.#message = 'Сначала укажите источник руды.';
    this.#emit();
  }

  chooseBuilding(buildingId: string): void {
    if (this.#mode.kind.startsWith('choosing-') && !this.targetBuildingIds().includes(buildingId)) {
      this.#message = 'Это здание нельзя выбрать для текущего шага.';
      this.#emit();
      return;
    }
    if (this.#mode.kind === 'choosing-work-target') {
      const selectedIds = this.selectedIds();
      const result = this.session.dispatch({
        type: 'ASSIGN_WORK',
        unitIds: selectedIds,
        buildingId,
      });
      if (!result.ok) {
        this.#finishCommand(false, result.error);
        return;
      }
      const selected = new Set(selectedIds);
      const assignedCount = this.session.snapshot().units.filter(
        (unit) =>
          selected.has(unit.id) &&
          (unit.assignment.kind === 'to-work' || unit.assignment.kind === 'work') &&
          unit.assignment.buildingId === buildingId,
      ).length;
      const suffix = assignedCount < selectedIds.length ? ' Остальные остались на прежних задачах.' : '';
      this.#finishCommand(true, `В шахту назначено ${assignedCount} из ${selectedIds.length}.${suffix}`);
      return;
    }
    if (this.#mode.kind === 'choosing-haul-source') {
      this.#mode = { kind: 'choosing-haul-destination', sourceId: buildingId };
      this.#message = 'Теперь укажите склад или рынок.';
      this.#emit();
      return;
    }
    if (this.#mode.kind === 'choosing-haul-destination') {
      const result = this.session.dispatch({
        type: 'ASSIGN_HAUL',
        unitIds: this.selectedIds(),
        sourceId: this.#mode.sourceId,
        destinationId: buildingId,
      });
      this.#finishCommand(result.ok, result.ok ? 'Постоянный маршрут назначен.' : result.error);
    }
  }

  releaseSelected(): void {
    if (!this.#hasSelection()) return;
    const result = this.session.dispatch({ type: 'RELEASE_UNITS', unitIds: this.selectedIds() });
    this.#finishCommand(result.ok, result.ok ? 'Юниты освобождены от работы.' : result.error);
  }

  cancelOrClear(): void {
    if (this.#mode.kind !== 'neutral') {
      this.#mode = { kind: 'neutral' };
      this.#message = 'Команда отменена.';
    } else {
      this.#selected.clear();
      this.#message = 'Выбор снят.';
    }
    this.#emit();
  }

  subscribe(listener: InteractionListener): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  #hasSelection(): boolean {
    if (this.#selected.size > 0) return true;
    this.#message = 'Сначала выберите хотя бы одного юнита.';
    this.#emit();
    return false;
  }

  #finishCommand(ok: boolean, message: string): void {
    this.#message = message;
    if (ok) this.#mode = { kind: 'neutral' };
    this.#emit();
  }

  #emit(): void {
    this.#listeners.forEach((listener) => listener());
  }
}
