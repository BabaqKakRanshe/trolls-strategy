using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The arena ladder in the real content (TrollStrategy/Dev/Setup Arena Ladder): every level a new test —
    /// neighbours stand differently, enemies wear gear from level 10, milestones bring a champion, wins bring
    /// trophies all the way up and every level has its surroundings.
    /// </summary>
    public class ArenaLadderContentTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private const string ArenaFolder = "Assets/Game/Prefabs/Arenas/";

        private GameContentCatalog _catalog;
        private List<BattleMissionDefinition> _ladder;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _ladder = _catalog.Missions.Where(m => m != null).OrderBy(m => m.Level).ToList();
            Assert.That(_ladder.Select(m => m.Level), Is.EqualTo(Enumerable.Range(1, 30)));
        }

        private static string Layout(BattleMissionDefinition mission) =>
            string.Join(";", mission.Enemies.OrderBy(e => e.Cell.X).ThenBy(e => e.Cell.Y).Select(e => $"{e.Cell.X},{e.Cell.Y}:{e.Kind}"));

        [Test]
        public void Neighbours_NeverStandAlike()
        {
            for (int i = 1; i < _ladder.Count; i++)
            {
                Assert.That(_ladder[i].Formation, Is.Not.EqualTo(_ladder[i - 1].Formation), _ladder[i].MissionId);
                Assert.That(Layout(_ladder[i]), Is.Not.EqualTo(Layout(_ladder[i - 1])), _ladder[i].MissionId);
            }
        }

        [Test]
        public void Milestones_EveryFifthLevel_HaveOneChampion_AndNoOtherLevelHasAny()
        {
            foreach (var mission in _ladder)
            {
                bool milestone = mission.Level % 5 == 0;
                Assert.That(mission.Milestone, Is.EqualTo(milestone), mission.MissionId);
                Assert.That(mission.Enemies.Count(e => e.Champion), Is.EqualTo(milestone ? 1 : 0), mission.MissionId);
                Assert.That(mission.ChampionHealthPercent, milestone ? Is.GreaterThan(100) : Is.Zero, mission.MissionId);
                if (milestone)
                    Assert.That(mission.Enemies.First(e => e.Champion).GearIds, Is.Not.Empty, "A champion wears gear");
            }
        }

        [Test]
        public void EnemyGear_FromLevelTen_IsTheCatalogsAndKeepsTheLevelsStrength()
        {
            var items = _catalog.Equipment.Where(e => e != null).ToDictionary(e => e.ItemId);
            int lastDamage = -1, lastArmor = -1;
            foreach (var mission in _ladder)
            {
                var regular = mission.Enemies.Where(e => !e.Champion).ToList();
                foreach (var enemy in mission.Enemies)
                    foreach (string id in enemy.GearIds)
                        Assert.That(items.ContainsKey(id), Is.True, $"{mission.MissionId}: {id}");
                if (mission.Level < 10)
                    Assert.That(regular.All(e => e.GearIds.Count == 0), Is.True, $"{mission.MissionId}: gear starts at level 10");
                else
                    Assert.That(regular.All(e => e.GearIds.Count > 0), Is.True, $"{mission.MissionId}: every enemy wears gear");
                // the level's bonus, flat or worn: it never drops, never jumps, and the gear only changes how it looks
                int damage = regular.Max(e => mission.EnemyDamageBonus + e.GearIds.Sum(id => items[id].DamageBonus));
                int armor = regular.Max(e => mission.EnemyArmorBonus + e.GearIds.Sum(id => items[id].ArmorBonus));
                if (lastDamage >= 0)
                {
                    Assert.That(damage, Is.InRange(lastDamage, lastDamage + 1), $"{mission.MissionId}: damage bonus step");
                    Assert.That(armor, Is.InRange(lastArmor, lastArmor + 1), $"{mission.MissionId}: armour bonus step");
                }
                lastDamage = damage;
                lastArmor = armor;
            }
        }

        [Test]
        public void Trophies_AllTheWayUp_AreGoodsThatCarryGear_AndFromLevelTenTheEnemiesOwn()
        {
            foreach (var mission in _ladder)
            {
                Assert.That(mission.WinGoods, Is.Not.Empty, $"{mission.MissionId} brings trophies");
                var worn = new HashSet<string>(mission.Enemies.SelectMany(e => e.GearIds));
                foreach (var trophy in mission.WinGoods)
                {
                    var resource = _catalog.TryGetResource(trophy.Resource);
                    Assert.That(resource, Is.Not.Null, $"{mission.MissionId}: {trophy.Resource}");
                    Assert.That(resource.IsEquipment, Is.True, $"{mission.MissionId}: {trophy.Resource} is gear");
                    if (mission.Level >= 10 || mission.Milestone)
                        Assert.That(worn.Contains(resource.EquipmentId), Is.True,
                            $"{mission.MissionId}: the trophy {resource.EquipmentId} is what its enemies wear");
                }
                if (mission.Milestone) Assert.That(mission.WinGoods.Sum(t => t.Amount), Is.EqualTo(2), mission.MissionId);
            }
        }

        [Test]
        public void Surroundings_EveryLevelHasOne_AndWithoutItsArtStandsOnTheMeadow()
        {
            var expected = new Dictionary<ArenaBiome, int[]>
            {
                [ArenaBiome.Meadow] = new[] { 1, 2, 3, 5, 7 },
                [ArenaBiome.Forest] = new[] { 4, 6, 9, 12, 15, 20 },
                [ArenaBiome.Swamp] = new[] { 8, 14 },
                [ArenaBiome.Graveyard] = new[] { 10, 16, 17, 19, 24 },
                [ArenaBiome.MountainPass] = new[] { 11, 13, 18, 21, 23, 26, 27, 29, 30 },
                [ArenaBiome.Snow] = new[] { 22, 25, 28 }
            };
            var meadow = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaFolder + "Arena_Meadow.prefab");
            Assert.That(meadow, Is.Not.Null);
            foreach (var mission in _ladder)
            {
                var biome = expected.First(pair => pair.Value.Contains(mission.Level)).Key;
                Assert.That(mission.Biome, Is.EqualTo(biome), mission.MissionId);
                Assert.That(mission.EnvironmentPrefab, Is.Not.Null, mission.MissionId);
                Assert.That(AssetDatabase.GetAssetPath(mission.EnvironmentPrefab), Does.StartWith(ArenaFolder), mission.MissionId);
                var own = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArenaFolder}Arena_{biome}.prefab");
                Assert.That(mission.EnvironmentPrefab, Is.SameAs(own != null ? own : meadow), mission.MissionId);
            }
        }
    }
}
