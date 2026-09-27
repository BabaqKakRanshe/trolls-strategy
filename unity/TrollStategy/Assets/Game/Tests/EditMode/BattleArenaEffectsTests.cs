using System;
using NUnit.Framework;
using TrollStrategy.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Tests
{
    public class BattleArenaEffectsTests
    {
        private const string MeadowPrefab = "Assets/Game/Prefabs/Arenas/Arena_Meadow.prefab";
        private const string MeadowLayout = "Assets/Vitaria/Layout/arena_meadow_layout.json";

#pragma warning disable 0649   // filled by JsonUtility
        [Serializable]
        private sealed class LayoutSockets
        {
            public SocketEntry[] sockets;
        }

        [Serializable]
        private sealed class SocketEntry
        {
            public string kind;
        }
#pragma warning restore 0649

        [Test]
        public void MeadowArena_CarriesEffectMeshesSocketsSwaysAndGusts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeadowPrefab);
            Assert.That(prefab, Is.Not.Null, MeadowPrefab);
            var set = prefab.GetComponent<BattleArenaSet>();
            foreach (ArenaFx role in Enum.GetValues(typeof(ArenaFx)))
                Assert.That(set.Effect(role, 0), Is.Not.Null, role.ToString());
            var shard = set.Effect(ArenaFx.Spark, 0).GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(shard.name, Is.EqualTo("Vitaria_FX"), "sparks glow through the emissive palette");

            var layout = JsonUtility.FromJson<LayoutSockets>(AssetDatabase.LoadAssetAtPath<TextAsset>(MeadowLayout).text);
            var ambience = prefab.GetComponent<BattleArenaAmbience>();
            Assert.That(ambience.Sockets, Has.Count.EqualTo(layout.sockets.Length));
            Assert.That(ambience.Sways, Is.Not.Empty);
            foreach (var sway in ambience.Sways)
                Assert.That(sway.Target != null && sway.Target.name.EndsWith("_Cloth", StringComparison.Ordinal),
                    "only banner cloths sway");
            Assert.That(ambience.Spinners, Has.Some.Matches<BattleArenaAmbience.Spinner>(s => s.Gust > 0f));
        }

        [Test]
        public void Cloth_SwaysAboutItsCrossbarFromItsRestPose()
        {
            var root = new GameObject("Arena");
            var cloth = new GameObject("Cloth").transform;
            cloth.SetParent(root.transform, false);
            var rest = Quaternion.Euler(0f, 30f, 0f);
            cloth.localRotation = rest;
            var ambience = root.AddComponent<BattleArenaAmbience>();
            ambience.AddSway(cloth, 5f, .35f, .629f);
            try
            {
                // slow sway plus a 2.3x flutter at 30% of the amplitude
                Assert.That(BattleArenaAmbience.SwayAngle(5f, .35f, .629f, 0f),
                    Is.EqualTo(6.5f * Mathf.Sin(2f * Mathf.PI * .629f)).Within(1e-4f));

                ambience.Step(.02f, 1.7f);
                float expected = BattleArenaAmbience.SwayAngle(5f, .35f, .629f, 1.7f);
                var swing = Quaternion.Inverse(rest) * cloth.localRotation;
                Assert.That(Quaternion.Angle(swing, Quaternion.Euler(expected, 0f, 0f)), Is.LessThan(.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Gusts_VarySailSpeedWithinTheirShare()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float t = 0f; t < 120f; t += .25f)
            {
                float factor = BattleArenaAmbience.GustFactor(.3f, t, 0);
                min = Mathf.Min(min, factor);
                max = Mathf.Max(max, factor);
            }
            Assert.That(min, Is.GreaterThanOrEqualTo(.7f - 1e-4f));
            Assert.That(max, Is.LessThanOrEqualTo(1.3f + 1e-4f));
            Assert.That(max - min, Is.GreaterThan(.1f), "the sails must actually gust");
            Assert.That(BattleArenaAmbience.GustFactor(0f, 17f, 3), Is.EqualTo(1f));
        }

        [Test]
        public void Sockets_NeverKeepMoreThanTheCapAliveAndMistCanBeSwitchedOff()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var root = new GameObject("Arena");
            var set = root.AddComponent<BattleArenaSet>();
            set.ConfigureEffects(null, new[] { template }, null, null, null, null, new[] { template });
            var ambience = root.AddComponent<BattleArenaAmbience>();
            for (int i = 0; i < 30; i++) ambience.AddSocket(BattleArenaAmbience.SocketKind.Smoke, new Vector3(i, 0f, 0f), 1f);
            for (int i = 0; i < 10; i++) ambience.AddSocket(BattleArenaAmbience.SocketKind.Embers, new Vector3(i, 0f, 5f), .6f);
            for (int i = 0; i < 10; i++) ambience.AddSocket(BattleArenaAmbience.SocketKind.Mist, new Vector3(i, 0f, 9f), 2f);
            try
            {
                int most = 0;
                for (int step = 0; step < 200; step++)
                {
                    ambience.Step(.05f, step * .05f);
                    most = Mathf.Max(most, ambience.LiveParticles);
                    Assert.That(ambience.LiveParticles, Is.LessThanOrEqualTo(BattleArenaAmbience.MaxLiveParticles));
                }
                Assert.That(most, Is.EqualTo(BattleArenaAmbience.MaxLiveParticles), "the busy test arena reaches the cap");

                var quiet = new GameObject("Quiet");
                var quietSet = quiet.AddComponent<BattleArenaSet>();
                quietSet.ConfigureEffects(null, new[] { template }, null, null, null, null, null);
                var mistOnly = quiet.AddComponent<BattleArenaAmbience>();
                mistOnly.AddSocket(BattleArenaAmbience.SocketKind.Mist, Vector3.zero, 2f);
                mistOnly.Mist = false;
                for (int step = 0; step < 40; step++) mistOnly.Step(.05f, step * .05f);
                Assert.That(mistOnly.LiveParticles, Is.Zero);
                Object.DestroyImmediate(quiet);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void Effects_WithoutAnArenaSpawnNoMeshes()
        {
            var effects = new GameObject("Effects", typeof(BattleEffects)).GetComponent<BattleEffects>();
            try
            {
                effects.Init(null);
                effects.Landing(Vector3.zero);
                effects.DeathCloud(Vector3.zero);
                effects.Debris(Vector3.up, 0f, Vector3.right, ArenaFx.DebrisEarth, 3, 6);
                effects.Advance(.1f);
                Assert.That(effects.LiveMeshes, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(effects.gameObject);
            }
        }

        [Test]
        public void Effects_RunOnTheReplayClockAndReuseTheirMeshes()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var arena = new GameObject("Arena").AddComponent<BattleArenaSet>();
            arena.ConfigureEffects(new[] { template }, null, new[] { template }, new[] { template }, null,
                new[] { template }, new[] { template });
            var effects = new GameObject("Effects", typeof(BattleEffects)).GetComponent<BattleEffects>();
            try
            {
                effects.Init(null);
                effects.UseArena(arena);

                // a delayed landing waits for replay time, not for frames
                effects.Landing(Vector3.zero, .2f);
                effects.Advance(0f);
                effects.Advance(.1f);
                Assert.That(effects.LiveMeshes, Is.Zero, "still waiting");
                effects.Advance(.15f);
                Assert.That(effects.LiveMeshes, Is.InRange(3, 5));
                effects.Advance(1f);
                Assert.That(effects.LiveMeshes, Is.Zero, "puffs are gone within half a second");

                // five more landings one after another never need more than one landing's worth of meshes
                for (int i = 0; i < 5; i++)
                {
                    effects.Landing(Vector3.zero);
                    effects.Advance(.1f);
                    effects.Advance(1f);
                }
                Assert.That(effects.transform.childCount, Is.LessThanOrEqualTo(5));

                effects.Projectile(Vector3.zero, new Vector3(4f, 0f, 0f), .3f, null, .3f);
                effects.Advance(.1f);
                Assert.That(effects.LiveMeshes, Is.EqualTo(1), "the thrown rock is a mesh");
                effects.Advance(.3f);
                Assert.That(effects.LiveMeshes, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(effects.gameObject);
                Object.DestroyImmediate(arena.gameObject);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void Debris_FallsOntoTheGroundThenShrinksAwayAndBurstsAreCapped()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var arena = new GameObject("Arena").AddComponent<BattleArenaSet>();
            arena.ConfigureEffects(null, null, new[] { template }, new[] { template }, null, null, null);
            var effects = new GameObject("Effects", typeof(BattleEffects)).GetComponent<BattleEffects>();
            try
            {
                effects.Init(null);
                effects.UseArena(arena);
                effects.Debris(new Vector3(0f, 1f, 0f), 0f, Vector3.right, ArenaFx.DebrisEarth, 3, 6);
                bool rose = false;
                for (int step = 0; step < 120 && (step == 0 || effects.LiveMeshes > 0); step++)
                {
                    effects.Advance(1f / 60f);
                    foreach (Transform piece in effects.transform)
                    {
                        if (!piece.gameObject.activeSelf) continue;
                        rose |= piece.position.y > 1.15f;
                        Assert.That(piece.position.y, Is.GreaterThanOrEqualTo(0f), "chunks never sink into the ground");
                    }
                }
                Assert.That(rose, "chunks are thrown up first");
                Assert.That(effects.LiveMeshes, Is.Zero, "and are recycled after landing");

                for (int i = 0; i < 40; i++)
                    effects.Debris(Vector3.up, 0f, Vector3.right, ArenaFx.DebrisStone, 6, 6);
                effects.Advance(.02f);
                Assert.That(effects.LiveMeshes, Is.EqualTo(BattleEffects.MaxLiveMeshes));
            }
            finally
            {
                Object.DestroyImmediate(effects.gameObject);
                Object.DestroyImmediate(arena.gameObject);
                Object.DestroyImmediate(template);
            }
        }
    }
}
