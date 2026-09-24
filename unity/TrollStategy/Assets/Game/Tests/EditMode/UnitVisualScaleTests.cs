using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Units;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class UnitVisualScaleTests
    {
        [Test]
        public void Setup_ScalesOnlyDedicatedSpriteVisual()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Init(
                UnitKind.Goblin,
                "Гоблин",
                40,
                3,
                5f,
                10,
                "",
                null,
                null,
                null,
                1.75f);

            var root = new GameObject("UnitUnderTest");
            var rootRenderer = root.AddComponent<SpriteRenderer>();
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.37f;
            var view = root.AddComponent<UnitView>();
            var snapshot = new UnitSnapshot(
                "unit-1",
                1,
                UnitKind.Goblin,
                "Гоблин",
                3,
                5f,
                10,
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
            definition.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 10, "", null, null, null);
            var root = new GameObject("MovingUnitUnderTest");
            root.AddComponent<SpriteRenderer>();
            var view = root.AddComponent<UnitView>();
            var initial = new UnitSnapshot(
                "unit-1", 1, UnitKind.Goblin, "Гоблин", 3, 5f, 10,
                new WorldPosition(0f, 0f), Assignment.Idle(), "Свободен", 4f);
            var nextStep = new UnitSnapshot(
                "unit-1", 1, UnitKind.Goblin, "Гоблин", 3, 5f, 10,
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
