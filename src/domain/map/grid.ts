import type { BuildingKind, GridCell } from '../model';

export const GRID_COLUMNS = 40;
export const GRID_ROWS = 23;
export const GRID_CELL_SIZE = 32;
export const MAX_UNITS_PER_CELL = 10;

export interface Footprint {
  width: number;
  height: number;
}

export const BUILDING_FOOTPRINTS: Record<BuildingKind, Footprint> = {
  'town-hall': { width: 4, height: 4 },
  mine: { width: 3, height: 4 },
  warehouse: { width: 3, height: 3 },
  market: { width: 3, height: 3 },
};

export const BARRACKS_FOOTPRINT: Footprint = { width: 3, height: 2 };
export const DEFAULT_MINE_CELL: GridCell = { x: 20, y: 7 };
export const BARRACKS_CELL: GridCell = { x: 19, y: 18 };
export const MARKET_CELL: GridCell = { x: 9, y: 10 };
export const WAREHOUSE_CELL: GridCell = { x: 29, y: 10 };

interface GridOccupant {
  readonly mapCell: GridCell | null;
}

interface GridBuilding extends GridOccupant {
  readonly kind: BuildingKind;
}

export interface GridOccupancy {
  readonly units: readonly GridOccupant[];
  readonly buildings: readonly GridBuilding[];
}

export function isGridCell(cell: GridCell): boolean {
  return Number.isInteger(cell.x)
    && Number.isInteger(cell.y)
    && cell.x >= 0
    && cell.x < GRID_COLUMNS
    && cell.y >= 0
    && cell.y < GRID_ROWS;
}

export function isBuildableCell(cell: GridCell): boolean {
  return isGridCell(cell)
    && cell.x >= 3
    && cell.x < GRID_COLUMNS - 3
    && cell.y >= 2
    && cell.y < GRID_ROWS - 2;
}

export function sameCell(a: GridCell | null, b: GridCell): boolean {
  return a !== null && a.x === b.x && a.y === b.y;
}

export function cellKey(cell: GridCell): string {
  return `${cell.x}:${cell.y}`;
}

export function footprintContains(
  anchor: GridCell | null,
  footprint: Footprint,
  cell: GridCell,
): boolean {
  return anchor !== null
    && cell.x >= anchor.x
    && cell.x < anchor.x + footprint.width
    && cell.y >= anchor.y
    && cell.y < anchor.y + footprint.height;
}

export function footprintCells(anchor: GridCell, footprint: Footprint): GridCell[] {
  const cells: GridCell[] = [];
  for (let y = 0; y < footprint.height; y += 1) {
    for (let x = 0; x < footprint.width; x += 1) cells.push({ x: anchor.x + x, y: anchor.y + y });
  }
  return cells;
}

export function isFootprintInside(anchor: GridCell, footprint: Footprint): boolean {
  return isGridCell(anchor)
    && anchor.x + footprint.width <= GRID_COLUMNS
    && anchor.y + footprint.height <= GRID_ROWS;
}

export function buildingAtCell(
  buildings: readonly GridBuilding[],
  cell: GridCell,
): GridBuilding | undefined {
  return buildings.find((building) => (
    footprintContains(building.mapCell, BUILDING_FOOTPRINTS[building.kind], cell)
  ));
}

export function isBuildingPlacementValid(
  occupancy: GridOccupancy,
  kind: BuildingKind,
  anchor: GridCell,
): boolean {
  const footprint = BUILDING_FOOTPRINTS[kind];
  if (!isFootprintInside(anchor, footprint)) return false;
  const cells = footprintCells(anchor, footprint);
  if (cells.some((cell) => !isBuildableCell(cell))) return false;
  if (cells.some((cell) => footprintContains(BARRACKS_CELL, BARRACKS_FOOTPRINT, cell))) return false;
  if (cells.some((cell) => occupancy.units.some((unit) => sameCell(unit.mapCell, cell)))) return false;
  return !cells.some((cell) => buildingAtCell(occupancy.buildings, cell) !== undefined);
}

export function buildingInteractionCell(kind: BuildingKind, anchor: GridCell): GridCell {
  const footprint = BUILDING_FOOTPRINTS[kind];
  const below = { x: anchor.x + Math.floor(footprint.width / 2), y: anchor.y + footprint.height };
  return isGridCell(below) ? below : { x: anchor.x, y: Math.max(0, anchor.y - 1) };
}
