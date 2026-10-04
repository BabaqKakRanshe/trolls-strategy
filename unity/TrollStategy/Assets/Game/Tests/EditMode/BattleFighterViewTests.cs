using System.IO;
using System.Reflection;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Units;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class BattleFighterViewTests
    {
        [TestCase("Assets/Game/Prefabs/Units/Troll.prefab")]
        [TestCase("Assets/Game/Prefabs/Units/Goblin.prefab")]
        public void UnitPrefab_HasBattlePoses(string path)
        {
            var view = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<UnitView>(true);

            Assert.That(view.AttackFrames.Count, Is.GreaterThanOrEqualTo(3), "attack");
            Assert.That(view.HurtFrames.Count, Is.GreaterThanOrEqualTo(3), "hurt");
            Assert.That(view.DeathFrames.Count, Is.GreaterThanOrEqualTo(6), "death");
            Assert.That(view.AttackFrames, Has.None.Null);
            Assert.That(view.DeathFrames, Has.None.Null);
        }

        [TestCase("Assets/Game/Prefabs/Units/Troll.prefab", UnitKind.Troll)]
        [TestCase("Assets/Game/Prefabs/Units/Goblin.prefab", UnitKind.Goblin)]
        public void Fighter_UsesSmallerArtAndGroundsOpaqueFeetThroughBillboardAndSpawn(string path, UnitKind kind)
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var fighter = new GameObject("Fighter", typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            var source = new Texture2D(2, 2);
            try
            {
                var visuals = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<UnitView>(true);
                var sprite = visuals.IdleSprite;
                source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
                var rect = sprite.rect;
                int bottom = (int)rect.height;
                for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                    if (source.GetPixel((int)rect.x + x, (int)rect.y + y).a > 0f)
                        bottom = Mathf.Min(bottom, y);
                Assert.That(bottom, Is.LessThan(rect.height), "the source frame contains visible art");
                float feetY = (bottom - sprite.pivot.y) / sprite.pixelsPerUnit;
                var ground = new Vector3(3f, 2f, 4f);
                cameraObject.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
                fighter.Init("unit-1", kind, false, new Cell(1, 0), ground, visuals, 55,
                    cameraObject.GetComponent<Camera>());
                var rendered = fighter.transform.Find("Sprite").GetComponent<SpriteRenderer>();
                Assert.That(rendered.transform.localScale.y, Is.EqualTo(32f * .17f / 1.6f).Within(.00001f));
                var feet = rendered.transform.TransformPoint(new Vector3(0f, feetY, 0f));
                Assert.That(Vector3.Distance(feet, ground + Vector3.up * .05f), Is.LessThan(.001f),
                    "visible feet, rather than the extruded mesh edge, touch the ground");

                fighter.PopIn();
                fighter.Advance(.15f);
                cameraObject.transform.rotation = Quaternion.Euler(60f, 15f, 0f);
                typeof(BattleFighterView).GetMethod("Billboard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(fighter, null);
                feet = rendered.transform.TransformPoint(new Vector3(0f, feetY, 0f));
                Assert.That(Vector3.Distance(feet, ground + Vector3.up * .05f), Is.LessThan(.001f),
                    "camera tilt and spawn scaling preserve the same feet anchor");
                var hitbox = fighter.GetComponentInChildren<CapsuleCollider>().transform;
                Assert.That(Vector3.Distance(hitbox.position, ground + Vector3.up * .05f), Is.LessThan(.001f));
                Assert.That(Vector3.Angle(hitbox.up, cameraObject.transform.up), Is.LessThan(.01f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(fighter.gameObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Fighter_WearsGearAsTokensAtTheBar_FreeSlotsOnlyWhilePlaced()
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Units/Goblin.prefab")
                .GetComponentInChildren<UnitView>(true);
            var fighter = new GameObject("Fighter", typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            var icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            try
            {
                fighter.Init("unit-1", UnitKind.Goblin, false, new Cell(0, 1), Vector3.zero, prefab, 20,
                    cameraObject.GetComponent<Camera>());
                var slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Helmet };
                var sword = new WornItem(EquipmentSlot.Weapon, icon, enchanted: true);

                fighter.SetGear(new[] { sword }, slots, showEmpty: true);
                Assert.That(fighter.GearTokens, Is.EqualTo(new[]
                {
                    (EquipmentSlot.Weapon, false, true), (EquipmentSlot.Armor, true, false), (EquipmentSlot.Helmet, true, false)
                }), "The sword with its enchanted rim, the free armour and helmet slots as hollows");

                fighter.HideEmptyGear();
                Assert.That(fighter.GearTokens, Is.EqualTo(new[] { (EquipmentSlot.Weapon, false, true) }),
                    "In battle only what is worn shows");

                fighter.SetGear(null, new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }, showEmpty: false);
                Assert.That(fighter.GearTokens, Is.Empty, "Nothing worn, not placing: no tokens");

                fighter.SetGear(new[] { new WornItem(EquipmentSlot.Helmet, icon, false) }, new[] { EquipmentSlot.Weapon }, true);
                Assert.That(fighter.GearTokens, Is.EqualTo(new[] { (EquipmentSlot.Weapon, true, false) }),
                    "Only the slots items of the game fit are shown");
            }
            finally
            {
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(fighter.gameObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Champion_IsAQuarterBigger_WithAFramedBar_AndWearsItsGearWithoutHollows()
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Units/Goblin.prefab")
                .GetComponentInChildren<UnitView>(true);
            var plain = new GameObject("Plain", typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            var champion = new GameObject("Champion", typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            var icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            try
            {
                foreach (var view in new[] { plain, champion })
                    view.Init("enemy-000", UnitKind.Goblin, true, new Cell(8, 1), Vector3.zero, prefab, 20,
                        cameraObject.GetComponent<Camera>());
                champion.SetChampion();
                plain.Advance(1f);
                champion.Advance(1f);
                float Scale(BattleFighterView view) => view.transform.Find("Sprite").localScale.y;
                Assert.That(champion.IsChampion, Is.True);
                Assert.That(Scale(champion) / Scale(plain), Is.EqualTo(1.25f).Within(.001f));
                Assert.That(champion.ShowsChampionFrame, Is.True);
                Assert.That(plain.ShowsChampionFrame, Is.False);

                // an enemy shows what it wears for the whole battle, never a free slot
                var slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Helmet };
                champion.SetGear(new[] { new WornItem(EquipmentSlot.Weapon, icon, false), new WornItem(EquipmentSlot.Armor, icon, false) },
                    slots, showEmpty: false);
                Assert.That(champion.GearTokens, Is.EqualTo(new[]
                {
                    (EquipmentSlot.Weapon, false, false), (EquipmentSlot.Armor, false, false)
                }));
            }
            finally
            {
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(plain.gameObject);
                Object.DestroyImmediate(champion.gameObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Fighter_ShowsBlowsUntilDeathAndThenIgnoresClicks()
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Units/Goblin.prefab")
                .GetComponentInChildren<UnitView>(true);
            var fighter = new GameObject("Fighter", typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            try
            {
                fighter.Init("enemy-000", UnitKind.Goblin, true, new Cell(8, 1), Vector3.zero, prefab, 20,
                    cameraObject.GetComponent<Camera>());
                var collider = fighter.GetComponentInChildren<CapsuleCollider>();
                Assert.That(fighter.GetComponentInChildren<BattleCellView>().Cell, Is.EqualTo(new Cell(8, 1)));
                Assert.That(collider.enabled, Is.True);

                fighter.ReceiveHit(7, 13, Vector3.left, .05f);
                fighter.Advance(.5f);
                Assert.That(fighter.Hp, Is.EqualTo(13));
                Assert.That(fighter.IsDead, Is.False);

                fighter.ReceiveHit(13, 0, Vector3.left, 0f);
                fighter.Die();
                fighter.Advance(2f);
                Assert.That(fighter.IsDead, Is.True);
                Assert.That(fighter.Hp, Is.EqualTo(0));
                Assert.That(collider.enabled, Is.False, "a fallen fighter must not take placement clicks");
            }
            finally
            {
                Object.DestroyImmediate(fighter.gameObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Fighter_WalksToItsNewCellAndStaysOnIt()
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var fighter = new GameObject("Fighter", typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            try
            {
                fighter.Init("unit-1", UnitKind.Troll, false, new Cell(0, 0), Vector3.zero, null, 55,
                    cameraObject.GetComponent<Camera>());
                var target = new Vector3(2f, 0f, 0f);
                fighter.SetCell(new Cell(1, 0), target, .3f);
                fighter.Advance(.15f);
                Assert.That(fighter.transform.position.x, Is.InRange(.1f, 1.99f), "mid-step");
                fighter.Advance(.3f);
                Assert.That(Vector3.Distance(fighter.transform.position, target), Is.LessThan(.001f));
                Assert.That(fighter.Cell, Is.EqualTo(new Cell(1, 0)));
                Assert.That(fighter.GetComponentInChildren<BattleCellView>().Cell, Is.EqualTo(new Cell(1, 0)));
            }
            finally
            {
                Object.DestroyImmediate(fighter.gameObject);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
