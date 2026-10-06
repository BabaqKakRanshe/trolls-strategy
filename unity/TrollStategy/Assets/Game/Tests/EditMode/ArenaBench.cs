using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The arena bench: the squads the campaign gives the player at its arena quests, fought through the real
    /// battle on every level of the ladder, placed as the bots place them. A fight reports its outcome, the share
    /// of the squad's health it took, the fallen and its length (docs/economy-balance.md §18).
    /// </summary>
    internal static class ArenaBench
    {
        internal sealed class Squad
        {
            public string Name;
            public (UnitKind Kind, string[] Gear)[] Fighters;
            public (string Id, int Level)[] Upgrades = Array.Empty<(string, int)>();
        }

        internal readonly struct Fight
        {
            public readonly int Level;
            public readonly BattleOutcome Outcome;
            /// <summary>The share of the squad's health the battle took, 0…1.</summary>
            public readonly double Lost;
            public readonly int Fallen;
            public readonly int DurationMs;

            public Fight(int level, BattleOutcome outcome, double lost, int fallen, int durationMs)
            {
                Level = level;
                Outcome = outcome;
                Lost = lost;
                Fallen = fallen;
                DurationMs = durationMs;
            }

            public bool Won => Outcome == BattleOutcome.PlayerVictory;
        }

        private static (UnitKind, string[]) F(UnitKind kind, params string[] gear) => (kind, gear);

        private static (UnitKind, string[])[] Times(int count, UnitKind kind, params string[] gear) =>
            Enumerable.Range(0, count).Select(_ => F(kind, gear)).ToArray();

        /// <summary>The first fight: the troll comes with its win, so goblins fight it.</summary>
        internal static readonly Squad Tutorial = new() { Name = "4 гоблина", Fighters = Times(4, UnitKind.Goblin) };

        /// <summary>The third level's quest: two trolls, the forge's first swords, the first fight's trophies.</summary>
        internal static readonly Squad Early = new()
        {
            Name = "2 тролля с мечами + 2 гоблина",
            Fighters = new[]
            {
                F(UnitKind.Troll, "iron-sword", "patched-armor"), F(UnitKind.Troll, "iron-sword"),
                F(UnitKind.Goblin, "rusty-sword"), F(UnitKind.Goblin)
            }
        };

        /// <summary>
        /// «Слава арены»: four trolls in iron and a goblin, with the barracks' first squad place and toughening, as
        /// the bots field it after «Крепкие бараки».
        /// </summary>
        internal static readonly Squad CampaignEnd = new()
        {
            Name = "4 тролля в железе + гоблин, здоровье +15%",
            Fighters = Times(4, UnitKind.Troll, "iron-sword", "iron-armor").Append(F(UnitKind.Goblin, "rusty-sword")).ToArray(),
            Upgrades = new[] { ("barracks-squad", 1), ("barracks-health", 1) }
        };

        internal static readonly Squad AfterCampaign = new()
        {
            Name = "5 троллей в стали, здоровье +30%, урон +1",
            Fighters = Times(5, UnitKind.Troll, "steel-sword", "steel-armor", "helmet"),
            Upgrades = new[] { ("barracks-squad", 1), ("barracks-health", 2), ("armory-drill", 1) }
        };

        internal static readonly Squad LateGame = new()
        {
            Name = "7 троллей, зачарованная сталь, здоровье +60%, урон +3",
            Fighters = Times(7, UnitKind.Troll, "enchanted-steel-sword", "enchanted-steel-armor", "enchanted-helmet"),
            Upgrades = new[] { ("barracks-squad", 3), ("barracks-health", 4), ("armory-drill", 3) }
        };

        internal static readonly Squad[] Squads = { Tutorial, Early, CampaignEnd, AfterCampaign, LateGame };

        internal static Fight Run(Squad squad, BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            var state = GameState.CreateInitialState(100000);
            foreach (var (id, level) in squad.Upgrades) state.Upgrades[id] = level;
            int limit = BattleApplication.SquadLimit(state, mission, catalog);
            for (int i = 0; i < squad.Fighters.Length && i < limit; i++)
            {
                string unitId = $"unit-{i + 1:D3}";
                state.Units.Add(new UnitState { Id = unitId, Kind = squad.Fighters[i].Kind, Assignment = Assignment.Idle() });
                foreach (string item in squad.Fighters[i].Gear)
                    state.Equipment.Add(new EquipmentState
                    {
                        Id = $"item-{state.Equipment.Count + 1:D3}", DefinitionId = item, OwnerUnitId = unitId
                    });
            }

            var board = mission.CreateBoard();
            int middle = board.Height / 2;
            var cells = board.DeploymentCells.OrderByDescending(c => c.X).ThenBy(c => Math.Abs(c.Y - middle))
                .ThenBy(c => c.Y).ToList();
            var units = state.Units.OrderBy(u => catalog.GetUnit(u.Kind).AttackRange).ToList();
            var placements = units.Select((u, i) => new BattlePlacement(u.Id, cells[i])).ToList();
            var ids = new HashSet<string>(units.Select(u => u.Id), StringComparer.Ordinal);

            var result = BattleApplication.Start(state, new StartBattleCommand(mission.MissionId, placements), catalog, true);
            Assert.That(result.Ok, Is.True, $"{squad.Name}, {mission.MissionId}: {result.Error}");
            var report = state.ActiveBattle.Report;
            int fallen = ids.Count - report.Survivors.Count(ids.Contains);
            return new Fight(mission.Level, report.Outcome, report.DefeatedShare(false), fallen, report.DurationMs);
        }

        internal static List<Fight> Ladder(Squad squad, GameContentCatalog catalog) =>
            catalog.Missions.Where(m => m != null).OrderBy(m => m.Level).Select(m => Run(squad, m, catalog)).ToList();

        /// <summary>The last level of the unbroken run of wins from the first; 0 when the first is lost.</summary>
        internal static int Reach(IReadOnlyList<Fight> fights)
        {
            int reach = 0;
            foreach (var fight in fights.OrderBy(f => f.Level))
            {
                if (!fight.Won) break;
                reach = fight.Level;
            }
            return reach;
        }

        /// <summary>The bench as a Markdown table: a row per level, a column per squad.</summary>
        internal static string Table(IReadOnlyList<(Squad Squad, List<Fight> Fights)> runs)
        {
            var text = new StringBuilder();
            text.AppendLine("| Уровень | " + string.Join(" | ", runs.Select(r => r.Squad.Name)) + " |");
            text.AppendLine("|---|" + string.Concat(runs.Select(_ => "---|")));
            text.AppendLine("| **Доходит до** | " + string.Join(" | ", runs.Select(r => $"**{Reach(r.Fights)}**")) + " |");
            int levels = runs.Max(r => r.Fights.Count);
            for (int i = 0; i < levels; i++)
                text.AppendLine($"| {i + 1} | " + string.Join(" | ", runs.Select(r => i < r.Fights.Count ? Cell(r.Fights[i]) : "")) + " |");
            return text.ToString();
        }

        private static string Cell(Fight fight)
        {
            string lost = (fight.Lost * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            string fallen = fight.Fallen > 0 ? $", пало {fight.Fallen}" : "";
            return fight.Outcome switch
            {
                BattleOutcome.PlayerVictory => $"победа, −{lost}{fallen}",
                BattleOutcome.Draw => $"ничья, −{lost}{fallen}",
                _ => "поражение"
            };
        }
    }
}
