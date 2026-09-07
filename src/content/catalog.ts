export const CELL_SIZE = 48;
export const GRID_WIDTH = 14;
export const GRID_HEIGHT = 14;
export const MAX_UNITS_PER_CELL = 20;
export const ECONOMY_STEP_MS = 250;
export const ORE_PER_STRENGTH_SECOND = 0.1;
export const ORE_SELL_PRICE = 3;
export const TRANSFER_TIME_MS = 500;

export const UNIT_CATALOG = {
  goblin: {
    name: 'Гоблин',
    price: 40,
    strength: 3,
    speed: 5,
    cargo: 10,
    description: 'Быстрый рабочий и носильщик',
  },
  troll: {
    name: 'Тролль',
    price: 170,
    strength: 9,
    speed: 2,
    cargo: 30,
    description: 'Медленный, но очень сильный',
  },
} as const;

export type UnitKind = keyof typeof UNIT_CATALOG;

export const BUILDING_CATALOG = {
  mine: {
    name: 'Шахта',
    price: 200,
    width: 3,
    height: 3,
    maxOre: 100,
    maxWorkers: 5,
  },
  warehouse: {
    name: 'Склад',
    price: 0,
    width: 3,
    height: 3,
    maxOre: 500,
    maxWorkers: 0,
  },
  market: {
    name: 'Рынок',
    price: 0,
    width: 3,
    height: 2,
    maxOre: 0,
    maxWorkers: 0,
  },
} as const;

export type BuildingKind = keyof typeof BUILDING_CATALOG;
