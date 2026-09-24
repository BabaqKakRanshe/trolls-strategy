using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    // Batch entry points also exercise a fresh Editor process, without rebuilding the game scene.
    public static class EnvironmentImportVerification
    {
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private static readonly string[] Paths = {
            "Assets/Game/Art/Sprites/Environment/forgotten-memories-props.png",
            "Assets/Game/Art/Sprites/Environment/forgotten-memories-tiles.png"
        };

        public static void RepairAndVerify()
        {
            AssetSlicer.RepairEnvironmentSprites();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var environment = GameObject.Find("EnvironmentWorld");
            if (environment == null) throw new InvalidOperationException("Generated environment is missing.");
            // Rebind the generated terrain/forest to the repaired named subassets.
            EnvironmentBuilder.BuildEnvironment(environment.transform.parent);
            EditorSceneManager.SaveScene(scene);
            Verify();
        }

        public static void Verify()
        {
            for (int i = 0; i < Paths.Length; i++)
            {
                var path = Paths[i];
                var before = Signature(path, i == 0 ? 8 : 19);
                for (int pass = 0; pass < 2; pass++)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    if (Signature(path, i == 0 ? 8 : 19) != before)
                        throw new InvalidOperationException($"Sprite identities changed after import: {path}");
                }
            }
            EditorSceneManager.OpenScene(ScenePath);
            var environment = GameObject.Find("EnvironmentWorld");
            var renderers = environment.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length < 196 || renderers.Any(renderer => renderer.sprite == null))
                throw new InvalidOperationException("Environment has missing sprite references.");
            Debug.Log($"ENVIRONMENT_IMPORT_PASS: stable names, rectangles, pivots, PPU and file IDs; {renderers.Length} valid scene sprites.");
        }

        public static void VerifyAndBuild()
        {
            Verify();
            string output = Path.GetFullPath("../../test-results/environment-player/TrollStrategy.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Environment verification player build failed.");
            Debug.Log("ENVIRONMENT_PLAYER_BUILD_PASS");
        }

        private static string Signature(string path, int expectedCount)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (sprites.Length != expectedCount || sprites.Any(sprite => sprite.name.StartsWith("forgotten-memories")))
                throw new InvalidOperationException($"Unexpected slicing in {path}: {sprites.Length}");
            return string.Join("\n", sprites.Select(sprite => {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
                return $"{sprite.name}|{guid}|{id}|{sprite.rect}|{sprite.pivot}|{sprite.pixelsPerUnit}";
            }));
        }
    }
}
