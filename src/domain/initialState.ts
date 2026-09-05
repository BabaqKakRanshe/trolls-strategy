import { ITEM_CATALOG } from '../content/catalog';
import { MISSION_ONE } from '../content/missionCatalog';
import {
  buildingId,
  itemId,
  type Building,
  type GameState,
  type Item,
} from './model';
import { MARKET_CELL, WAREHOUSE_CELL } from './map/grid';

const INITIAL_GOLD = 1_000;
const DEFAULT_CAMPAIGN_SEED = 0x5eed_1234;

function createBuildings(): Building[] {
  return [
    { id: buildingId('town-hall'), kind: 'town-hall', inventory: { ironOre: 0 }, mapCell: null },
    { id: buildingId('mine'), kind: 'mine', inventory: { ironOre: 0 }, mapCell: null },
    { id: buildingId('warehouse'), kind: 'warehouse', inventory: { ironOre: 0 }, mapCell: { ...WAREHOUSE_CELL } },
    { id: buildingId('market'), kind: 'market', inventory: { ironOre: 0 }, mapCell: { ...MARKET_CELL } },
  ];
}

function createStarterItems(): Item[] {
  return [
    ...createItemSet('rusty-sword', 4),
    ...createItemSet('patched-armor', 4),
  ];
}

function createItemSet(
  kind: keyof typeof ITEM_CATALOG,
  count: number,
): Item[] {
  const definition = ITEM_CATALOG[kind];

  return Array.from({ length: count }, (_, index) => ({
    id: itemId(`${kind}-${index + 1}`),
    kind,
    ...definition,
  }));
}

export function createInitialState(seed = DEFAULT_CAMPAIGN_SEED): GameState {
  const items = createStarterItems();

  return {
    schemaVersion: 1,
    campaignSeed: seed >>> 0,
    simulationTimeMs: 0,
    accumulators: { mineOre: 0 },
    wallet: { gold: INITIAL_GOLD },
    units: [],
    items,
    colonyInventory: {
      itemIds: items.map((item) => item.id),
    },
    buildings: createBuildings(),
    mission: {
      nextMissionAtMs: MISSION_ONE.cooldownMs,
      nextRunSequence: 1,
      appliedRunIds: [],
      clearedMissionIds: [],
      activeRun: null,
    },
    unlocks: [],
  };
}
