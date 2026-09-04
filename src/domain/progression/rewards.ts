export interface Reward {
  gold: number;
  ironOre: number;
  unlocks: string[];
}

const FIRST_CLEAR_REWARD: Readonly<Reward> = {
  gold: 300,
  ironOre: 40,
  unlocks: ['smelter-recipe'],
};

const REPEAT_REWARD: Readonly<Reward> = {
  gold: 80,
  ironOre: 10,
  unlocks: [],
};

export function getMissionOneReward(firstClear: boolean): Reward {
  const reward = firstClear ? FIRST_CLEAR_REWARD : REPEAT_REWARD;
  return {
    gold: reward.gold,
    ironOre: reward.ironOre,
    unlocks: [...reward.unlocks],
  };
}

export function getEmptyReward(): Reward {
  return { gold: 0, ironOre: 0, unlocks: [] };
}
