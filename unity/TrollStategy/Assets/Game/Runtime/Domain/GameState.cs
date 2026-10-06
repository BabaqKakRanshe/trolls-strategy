using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    [Serializable]
    public class GameState
    {
        public int Gold { get; set; }
        public int SoldGoods { get; set; }
        // Gold the market has paid over the whole game, and goods it took by kind; quests count from them.
        public int SalesGold { get; set; }
        public Dictionary<ResourceKind, int> SoldByResource { get; set; } = new();
        public int BattlesWon { get; set; }
        // Items fighters wore as their battles started, over the whole game; quests count from it.
        public int GearWornInBattles { get; set; }
        public List<BuildingState> Buildings { get; set; } = new();
        public List<UnitState> Units { get; set; } = new();
        public List<EquipmentState> Equipment { get; set; } = new();
        public int NextBuildingId { get; set; } = 1;
        public int NextUnitId { get; set; } = 1;
        public int NextHaulQueueTicket { get; set; } = 1;
        // Bumped whenever a footprint appears, moves or disappears; stale unit routes are replanned.
        public int LayoutVersion { get; set; }
        public int ActiveTimeMs { get; set; }
        // Wins and the active time each arena mission is ready again at, by mission id.
        public Dictionary<string, int> MissionWins { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> MissionReadyAtMs { get; set; } = new(StringComparer.Ordinal);
        // Highest arena level won so far; quests count from it.
        public int HighestMissionLevel { get; set; }
        public BattleRunState ActiveBattle { get; set; }
        // Gold a won battle rolled and the player has not taken yet; null when there is none.
        public PendingBattleReward PendingBattleReward { get; set; }
        // The arena's prize fund for repeat wins: the payouts it held at ArenaFundSinceMs, the active time the next
        // payout gathers from. ArenaFund counts what came since.
        public int ArenaFundPayouts { get; set; }
        public int ArenaFundSinceMs { get; set; }
        // State of the reward dice: every roll reads and advances it, so the same game rolls the same amounts.
        public uint RewardRoll { get; set; } = RewardDice.Seed;
        // State of the production dice (by-products, spoilage); separate from the reward dice so a change in
        // the economy never shifts what battles pay.
        public uint ProductionRoll { get; set; } = ProductionDice.Seed;
        // Goods the colony's buildings have made over the whole game, by kind; quests count from them.
        public Dictionary<ResourceKind, int> ProducedByResource { get; set; } = new();
        // Levels of the colony improvements bought in the guild and the barracks, by upgrade id.
        public Dictionary<string, int> Upgrades { get; set; } = new(StringComparer.Ordinal);
        // Quest chain and unlocks; null in a sandbox game, where everything is open.
        public ProgressState Progress { get; set; }
        // The colony's land blocks; null when land limits nothing (the whole grid is open).
        public LandState Land { get; set; }

        public static GameState CreateInitialState(int startingGold = 1000)
        {
            return new GameState
            {
                Gold = startingGold,
                SoldGoods = 0,
                // Starting buildings come from the scene layout via ColonySimulation.PlaceStartingBuilding.
                Buildings = new List<BuildingState>(),
                Units = new List<UnitState>(),
                Equipment = new List<EquipmentState>(),
                NextBuildingId = 1,
                NextUnitId = 1
            };
        }

        public int SoldOf(ResourceKind resource) => SoldByResource.TryGetValue(resource, out int amount) ? amount : 0;

        public int WinsOf(string missionId) =>
            missionId != null && MissionWins.TryGetValue(missionId, out int wins) ? wins : 0;

        public int ReadyAtOf(string missionId) =>
            missionId != null && MissionReadyAtMs.TryGetValue(missionId, out int at) ? at : 0;

        public int ProducedOf(ResourceKind resource) =>
            ProducedByResource.TryGetValue(resource, out int amount) ? amount : 0;

        public int UpgradeLevel(string upgradeId) =>
            upgradeId != null && Upgrades.TryGetValue(upgradeId, out int level) ? level : 0;

        public GameState Clone()
        {
            var clone = new GameState
            {
                Gold = Gold,
                SoldGoods = SoldGoods,
                SalesGold = SalesGold,
                SoldByResource = new Dictionary<ResourceKind, int>(SoldByResource),
                BattlesWon = BattlesWon,
                GearWornInBattles = GearWornInBattles,
                NextBuildingId = NextBuildingId,
                NextUnitId = NextUnitId,
                NextHaulQueueTicket = NextHaulQueueTicket,
                LayoutVersion = LayoutVersion,
                ActiveTimeMs = ActiveTimeMs,
                MissionWins = new Dictionary<string, int>(MissionWins, StringComparer.Ordinal),
                MissionReadyAtMs = new Dictionary<string, int>(MissionReadyAtMs, StringComparer.Ordinal),
                HighestMissionLevel = HighestMissionLevel,
                ActiveBattle = ActiveBattle,
                PendingBattleReward = PendingBattleReward?.Clone(),
                ArenaFundPayouts = ArenaFundPayouts,
                ArenaFundSinceMs = ArenaFundSinceMs,
                RewardRoll = RewardRoll,
                ProductionRoll = ProductionRoll,
                ProducedByResource = new Dictionary<ResourceKind, int>(ProducedByResource),
                Upgrades = new Dictionary<string, int>(Upgrades, StringComparer.Ordinal),
                Progress = Progress?.Clone(),
                Land = Land?.Clone(),
                Buildings = new List<BuildingState>(Buildings.Count),
                Units = new List<UnitState>(Units.Count),
                Equipment = new List<EquipmentState>(Equipment.Count)
            };

            for (int i = 0; i < Buildings.Count; i++)
                clone.Buildings.Add(Buildings[i].Clone());

            for (int i = 0; i < Units.Count; i++)
                clone.Units.Add(Units[i].Clone());

            for (int i = 0; i < Equipment.Count; i++)
                clone.Equipment.Add(Equipment[i].Clone());

            return clone;
        }
    }

    /// <summary>
    /// A battle's gold waiting in the colony: the amount it rolled and the range it rolled in, and the trophies
    /// its wins brought, which go to the barracks when the reward is taken.
    /// </summary>
    [Serializable]
    public class PendingBattleReward
    {
        public string MissionId { get; set; }
        public int Gold { get; set; }
        public int MinGold { get; set; }
        public int MaxGold { get; set; }
        public bool FirstWin { get; set; }
        // The last battle that added to the reward ended in a draw: the gold is a share of a win's.
        public bool Draw { get; set; }
        public Dictionary<ResourceKind, int> Goods { get; set; } = new();

        public PendingBattleReward Clone()
        {
            var clone = (PendingBattleReward)MemberwiseClone();
            clone.Goods = Goods != null ? new Dictionary<ResourceKind, int>(Goods) : new Dictionary<ResourceKind, int>();
            return clone;
        }
    }

    /// <summary>Repeatable dice for surprise rewards: xorshift over a state kept in the game.</summary>
    public static class RewardDice
    {
        public const uint Seed = 0x9E3779B9u;

        /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public static int Roll(GameState state, int min, int max)
        {
            if (max <= min) return min;
            uint x = Next(state.RewardRoll == 0 ? Seed : state.RewardRoll);
            state.RewardRoll = x;
            return min + (int)(x % (uint)(max - min + 1));
        }

        internal static uint Next(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }

    /// <summary>Repeatable dice for production chances (by-products, spoilage): xorshift over a state kept in the game.</summary>
    public static class ProductionDice
    {
        public const uint Seed = 0x85EBCA6Bu;

        /// <summary>True with the given chance in percent; every roll advances the state.</summary>
        public static bool Chance(GameState state, int percent)
        {
            if (percent <= 0) return false;
            uint x = RewardDice.Next(state.ProductionRoll == 0 ? Seed : state.ProductionRoll);
            state.ProductionRoll = x;
            return percent >= 100 || x % 100u < (uint)percent;
        }
    }
}
