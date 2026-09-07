using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    [Serializable]
    public class GameState
    {
        public int Gold { get; set; }
        public int SoldOre { get; set; }
        public List<BuildingState> Buildings { get; set; } = new();
        public List<UnitState> Units { get; set; } = new();
        public int NextBuildingId { get; set; } = 1;
        public int NextUnitId { get; set; } = 1;

        public static GameState CreateInitialState(int startingGold = 1000)
        {
            return new GameState
            {
                Gold = startingGold,
                SoldOre = 0,
                Buildings = new List<BuildingState>
                {
                    new()
                    {
                        Id = "warehouse-1",
                        Kind = BuildingKind.Warehouse,
                        Cell = new Cell(10, 8),
                        Ore = 0,
                        ProductionProgress = 0f
                    },
                    new()
                    {
                        Id = "market-1",
                        Kind = BuildingKind.Market,
                        Cell = new Cell(10, 2),
                        Ore = 0,
                        ProductionProgress = 0f
                    }
                },
                Units = new List<UnitState>(),
                NextBuildingId = 1,
                NextUnitId = 1
            };
        }

        public GameState Clone()
        {
            var clone = new GameState
            {
                Gold = Gold,
                SoldOre = SoldOre,
                NextBuildingId = NextBuildingId,
                NextUnitId = NextUnitId,
                Buildings = new List<BuildingState>(Buildings.Count),
                Units = new List<UnitState>(Units.Count)
            };

            for (int i = 0; i < Buildings.Count; i++)
                clone.Buildings.Add(Buildings[i].Clone());

            for (int i = 0; i < Units.Count; i++)
                clone.Units.Add(Units[i].Clone());

            return clone;
        }
    }
}
