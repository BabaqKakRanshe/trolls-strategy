using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Units;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class UnitVisualScaleTests
    {
        [Test]
        public void Hauler_ShowsCargoSpriteAndHidesItAfterUnloading()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Game/Prefabs/UnitBase.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var assignment = Assignment.Haul("mine-1", "market-1");
            assignment.Carried = 7;
            var carrying = new UnitSnapshot("unit-1", 1, UnitKind.Goblin, "Гоблин", 3, 5f, 100,
                new WorldPosition(0f, 0f), assignment, "Несёт");

            try
            {
                var view = root.GetComponent<UnitView>();
                view.Setup(carrying, definition, null);
                var icon = root.transform.Find("CargoIcon").GetComponent<SpriteRenderer>();
                Assert.That(icon.gameObject.activeSelf, Is.True);
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.transform.localPosition.y, Is.GreaterThan(0.5f));

                assignment.Carried = 0;
                view.UpdateVisuals(new UnitSnapshot("unit-1", 1, UnitKind.Goblin, "Гоблин", 3, 5f, 100,
                    new WorldPosition(0f, 0f), assignment, "Разгрузил"), false);
                Assert.That(icon.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Setup_ScalesOnlyDedicatedSpriteVisual()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);

            var root = new GameObject("UnitUnderTest");
            var rootRenderer = root.AddComponent<SpriteRenderer>();
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.37f;
            var view = root.AddComponent<UnitView>();
            view.SetSpriteScale(1.75f);
            var snapshot = new UnitSnapshot(
                "unit-1",
                1,
                UnitKind.Goblin,
                "Гоблин",
                3,
                5f,
                100,
                new WorldPosition(2f, 3f),
                Assignment.Idle(),
                "Свободен");

            try
            {
                view.Setup(snapshot, definition, null);

                var spriteVisual = root.transform.Find("SpriteVisual");
                Assert.That(spriteVisual, Is.Not.Null);
                Assert.That(spriteVisual.localScale, Is.EqualTo(new Vector3(1.75f, 1.75f, 1f)));
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(collider.radius, Is.EqualTo(0.37f));
                Assert.That(rootRenderer.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void AdvanceVisual_UsesSnapshotSpeedWithoutArrivingEarly()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var root = new GameObject("MovingUnitUnderTest");
            root.AddComponent<SpriteRenderer>();
            var view = root.AddComponent<UnitView>();
            var initial = new UnitSnapshot(
                "unit-1", 1, UnitKind.Goblin, "Гоблин", 3, 5f, 100,
                new WorldPosition(0f, 0f), Assignment.Idle(), "Свободен", 4f);
            var nextStep = new UnitSnapshot(
                "unit-1", 1, UnitKind.Goblin, "Гоблин", 3, 5f, 100,
                new WorldPosition(1f, 0f), Assignment.ToWork("mine-1"), "Идёт", 4f);

            try
            {
                view.Setup(initial, definition, null);
                view.UpdateVisuals(nextStep, false);

                view.AdvanceVisual(0.125f);
                Assert.That(root.transform.position.x, Is.EqualTo(0.5f).Within(0.0001f));

                view.AdvanceVisual(0.125f);
                Assert.That(root.transform.position.x, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
