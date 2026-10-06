using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class BattleBoardTests
    {
        [Test]
        public void OddRNeighboursAndDistanceMatchOffsetRows()
        {
            var board = new BattleBoard(9, 5, Array.Empty<Cell>(), new[] { new Cell(0, 0) });
            var even = new HashSet<Cell>(board.Neighbours(new Cell(3, 2)));
            var odd = new HashSet<Cell>(board.Neighbours(new Cell(3, 3)));

            Assert.That(even, Is.EquivalentTo(new[]
            {
                new Cell(2, 2), new Cell(4, 2), new Cell(2, 1),
                new Cell(3, 1), new Cell(2, 3), new Cell(3, 3)
            }));
            Assert.That(odd, Is.EquivalentTo(new[]
            {
                new Cell(2, 3), new Cell(4, 3), new Cell(3, 2),
                new Cell(4, 2), new Cell(3, 4), new Cell(4, 4)
            }));
            Assert.That(BattleBoard.HexDistance(new Cell(3, 2), new Cell(3, 3)), Is.EqualTo(1));
            Assert.That(BattleBoard.HexDistance(new Cell(3, 3), new Cell(4, 2)), Is.EqualTo(1));
        }

        [Test]
        public void VariableBoardRoutesAroundBlockedCell()
        {
            var board = new BattleBoard(11, 7, new[] { new Cell(1, 0) }, new[] { new Cell(0, 0) });

            Assert.That(board.CanPlace(new Cell(0, 0)), Is.True);
            Assert.That(board.CanPlace(new Cell(1, 0)), Is.False);
            Assert.That(board.TryShortestPath(new Cell(0, 0), new Cell(2, 0), null, out var path), Is.True);
            Assert.That(path[0], Is.EqualTo(new Cell(0, 0)));
            Assert.That(path[path.Count - 1], Is.EqualTo(new Cell(2, 0)));
            Assert.That(path, Has.None.EqualTo(new Cell(1, 0)));
            Assert.That(path.Count, Is.GreaterThan(3));
        }

        [Test]
        public void InvalidMissionGeometryIsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new BattleBoard(9, 5, new[] { new Cell(0, 0) }, new[] { new Cell(0, 0) }));
            Assert.Throws<ArgumentException>(() =>
                new BattleBoard(9, 5, new[] { new Cell(9, 0) }, new[] { new Cell(0, 0) }));

            var mission = ScriptableObject.CreateInstance<BattleMissionDefinition>();
            try
            {
                mission.SetDesign("mission-test", "Test", 7, 5, 2,
                    new[] { new Cell(0, 0), new Cell(0, 1) },
                    new[] { new Cell(6, 0) },
                    new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(6, 0) } });
                Assert.Throws<InvalidOperationException>(() => mission.CreateBoard());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mission);
            }
        }

        [Test]
        public void AutomaticBattleIsRepeatableAndNeverWalksOntoObstacle()
        {
            var board = new BattleBoard(7, 5, new[] { new Cell(2, 2) },
                new[] { new Cell(0, 2), new Cell(1, 2) });
            var fighters = new[]
            {
                new BattleFighterInput("ally-troll", UnitKind.Troll, true, new Cell(0, 2),
                    55, 7, 3, 2600, 1, 500),
                new BattleFighterInput("enemy-goblin", UnitKind.Goblin, false, new Cell(6, 2),
                    20, 2, 1, 2000, 3, 200)
            };

            var first = BattleSimulation.Run(board, fighters, 17, 10);
            var second = BattleSimulation.Run(board, fighters, 17, 10);

            Assert.That(first.Outcome, Is.EqualTo(second.Outcome));
            Assert.That(first.DurationMs, Is.EqualTo(second.DurationMs));
            Assert.That(first.Events.Count, Is.EqualTo(second.Events.Count));
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.That(first.Events[i].Kind, Is.EqualTo(second.Events[i].Kind));
                Assert.That(first.Events[i].TimeMs, Is.EqualTo(second.Events[i].TimeMs));
                Assert.That(first.Events[i].ActorId, Is.EqualTo(second.Events[i].ActorId));
                Assert.That(first.Events[i].Cell, Is.EqualTo(second.Events[i].Cell));
                if (first.Events[i].Kind == BattleEventKind.Move)
                    Assert.That(first.Events[i].Cell, Is.Not.EqualTo(new Cell(2, 2)));
            }
        }

        [Test]
        public void FighterPursuesReachableEnemyWhenNearestIsBehindWall()
        {
            var wall = new[]
            {
                new Cell(2, 0), new Cell(2, 1), new Cell(2, 2),
                new Cell(2, 3), new Cell(2, 4)
            };
            var board = new BattleBoard(5, 5, wall, new[] { new Cell(1, 2) });
            var fighters = new[]
            {
                new BattleFighterInput("ally", UnitKind.Troll, true, new Cell(1, 2),
                    100, 7, 3, 2600, 1, 500),
                new BattleFighterInput("enemy-behind-wall", UnitKind.Goblin, false,
                    new Cell(3, 2), 20, 2, 1, 2000, 1, 200),
                new BattleFighterInput("enemy-reachable", UnitKind.Goblin, false,
                    new Cell(0, 0), 40, 2, 1, 2000, 1, 200)
            };

            var report = BattleSimulation.Run(board, fighters, 23, 10);
            Assert.That(report.Events.Any(eventEntry => eventEntry.Kind == BattleEventKind.Move &&
                eventEntry.ActorId == "ally"), Is.True);
        }
    }
}
