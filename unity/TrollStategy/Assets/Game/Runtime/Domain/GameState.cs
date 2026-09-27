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

        public GameState Clone()
        {
            var clone = new GameState
            {
                Gold = Gold,
                SoldGoods = SoldGoods,
                NextBuildingId = NextBuildingId,
                NextUnitId = NextUnitId,
                NextHaulQueueTicket = NextHaulQueueTicket,
                LayoutVersion = LayoutVersion,
                ActiveTimeMs = ActiveTimeMs,
                FirstMissionWins = FirstMissionWins,
                FirstMissionNextReadyAtMs = FirstMissionNextReadyAtMs,
                ActiveBattle = ActiveBattle,
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
}
