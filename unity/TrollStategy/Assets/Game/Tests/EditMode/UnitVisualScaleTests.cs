using System.IO;
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

        [TestCase("Assets/Game/Prefabs/Units/Troll.prefab", UnitKind.Troll)]
        [TestCase("Assets/Game/Prefabs/Units/Goblin.prefab", UnitKind.Goblin)]
        public void Colony_StandsOpaqueFeetOnTheLawnOverAShadow(string path, UnitKind kind)
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Init(kind, kind.ToString(), 40, 3, 5f, 100);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var source = new Texture2D(2, 2);
            try
            {
                var view = root.GetComponent<UnitView>();
                var sprite = view.IdleSprite;
                source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
                var rect = sprite.rect;
                int bottom = (int)rect.height;
                for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                    if (source.GetPixel((int)rect.x + x, (int)rect.y + y).a > 0f)
                        bottom = Mathf.Min(bottom, y);
                Assert.That(bottom, Is.GreaterThan(0), "the frame has a transparent margin under the feet");
                float feetY = (bottom - sprite.pivot.y) / sprite.pixelsPerUnit;

                // the colony grid's map plane: x east, y north, local -Z up
                root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                view.Setup(new UnitSnapshot("unit-1", 1, kind, kind.ToString(), 3, 5f, 100, new WorldPosition(2f, 3f),
                    Assignment.Idle(), "Свободен"), definition, null);
                var visual = root.transform.Find("SpriteVisual");
                var lawn = root.transform.TransformPoint(Vector3.back * UnitView.GroundLift);
                Assert.That(Vector3.Distance(visual.TransformPoint(new Vector3(0f, feetY, 0f)), lawn), Is.LessThan(.001f),
                    "visible feet, rather than the frame's edge, stand on the lawn");

                var shadow = root.transform.Find("Shadow").GetComponent<SpriteRenderer>();
                Assert.That(shadow.sprite, Is.Not.Null);
                Assert.That(shadow.enabled, Is.True);
                Assert.That(Vector3.Distance(shadow.transform.position, lawn), Is.LessThan(.001f), "under the feet");
                Assert.That(Vector3.Angle(shadow.transform.forward, root.transform.forward), Is.LessThan(.01f),
                    "flat on the lawn");
                Assert.That(shadow.bounds.size.x, Is.GreaterThan(.3f), "about as wide as the creature");

                root.transform.localScale = Vector3.one * .4f;          // a pop-in
                view.UpdateVisuals(view.Snapshot, false);
                lawn = root.transform.TransformPoint(Vector3.back * UnitView.GroundLift);
                Assert.That(Vector3.Distance(visual.TransformPoint(new Vector3(0f, feetY, 0f)), lawn), Is.LessThan(.001f),
                    "a scaled creature keeps its feet down");
            }
            finally
            {
                Object.DestroyImmediate(source);
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
