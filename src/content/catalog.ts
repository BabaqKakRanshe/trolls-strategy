export const UNIT_CATALOG = {
  goblin: {
    cost: 40,
    maxHealth: 20,
    strength: 3,
    speed: 5,
    carryCapacity: 10,
    damage: 2,
    armor: 1,
    attackIntervalMs: 2_000,
  },
  troll: {
    cost: 170,
    maxHealth: 55,
    strength: 9,
    speed: 2,
    carryCapacity: 30,
    damage: 7,
    armor: 3,
    attackIntervalMs: 2_600,
  },
} as const;

export const BUILDING_CATALOG = {
  mine: {
    purchaseCost: 200,
    unlockId: 'building:mine',
    workerCapacity: 5,
    orePerStrengthSecond: 0.1,
    inventoryCapacity: 100,
  },
  warehouse: {
    inventoryCapacity: 500,
  },
  market: {
    oreSalePrice: 3,
  },
} as const;

export const ITEM_CATALOG = {
  'rusty-sword': {
    slot: 'weapon',
    damageBonus: 1,
    armorBonus: 0,
  },
  'patched-armor': {
    slot: 'armor',
    damageBonus: 0,
    armorBonus: 1,
  },
} as const;

export type UnitSpecies = keyof typeof UNIT_CATALOG;
export type ItemKind = keyof typeof ITEM_CATALOG;
