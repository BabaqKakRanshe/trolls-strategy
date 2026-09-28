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
        public List<BuildingState> Buildings { get; set; } = new();
        public List<UnitState> Units { get; set; } = new();
        public List<EquipmentState> Equipment { get; set; } = new();
        public int NextBuildingId { get; set; } = 1;
        public int NextUnitId { get; set; } = 1;
        public int NextHaulQueueTicket { get; set; } = 1;
        // Bumped whenever a footprint appears, moves or disappears; stale unit routes are replanned.
        public int LayoutVersion { get; set; }
        public int ActiveTimeMs { get; set; }
        public int FirstMissionWins { get; set; }
        public int FirstMissionNextReadyAtMs { get; set; }
        public BattleRunState ActiveBattle { get; set; }
        // Gold a won battle rolled and the player has not taken yet; null when there is none.
        public PendingBattleReward PendingBattleReward { get; set; }
        // State of the reward dice: every roll reads and advances it, so the same game rolls the same amounts.
        public uint RewardRoll { get; set; } = RewardDice.Seed;
        // Quest chain and unlocks; null in a sandbox game, where everything is open.
        public ProgressState Progress { get; set; }

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

        public GameState Clone()
        {
            var clone = new GameState
            {
                Gold = Gold,
                SoldGoods = SoldGoods,
                SalesGold = SalesGold,
                SoldByResource = new Dictionary<ResourceKind, int>(SoldByResource),
                BattlesWon = BattlesWon,
                NextBuildingId = NextBuildingId,
                NextUnitId = NextUnitId,
                NextHaulQueueTicket = NextHaulQueueTicket,
                LayoutVersion = LayoutVersion,
                ActiveTimeMs = ActiveTimeMs,
                FirstMissionWins = FirstMissionWins,
                FirstMissionNextReadyAtMs = FirstMissionNextReadyAtMs,
                ActiveBattle = ActiveBattle,
                PendingBattleReward = PendingBattleReward?.Clone(),
                RewardRoll = RewardRoll,
                Progress = Progress?.Clone(),
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

    /// <summary>A battle's gold waiting in the colony: the amount it rolled and the range it rolled in.</summary>
    [Serializable]
    public class PendingBattleReward
    {
        public string MissionId { get; set; }
        public int Gold { get; set; }
        public int MinGold { get; set; }
        public int MaxGold { get; set; }
        public bool FirstWin { get; set; }

        public PendingBattleReward Clone() => (PendingBattleReward)MemberwiseClone();
    }

    /// <summary>Repeatable dice for surprise rewards: xorshift over a state kept in the game.</summary>
    public static class RewardDice
    {
        public const uint Seed = 0x9E3779B9u;

        /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public static int Roll(GameState state, int min, int max)
        {
            if (max <= min) return min;
            uint x = state.RewardRoll == 0 ? Seed : state.RewardRoll;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state.RewardRoll = x;
            return min + (int)(x % (uint)(max - min + 1));
        }
    }
}
