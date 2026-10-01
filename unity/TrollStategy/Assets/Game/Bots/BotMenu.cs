using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Runs every bot profile on the shipped catalog and the colony scene's starting buildings and writes the
    /// reports to Builds/Bots (git-ignored): summary.md, one Markdown and one CSV per profile.
    /// Batch: -executeMethod TrollStrategy.Bots.BotMenu.RunAllBatch -quit.
    /// </summary>
    public static class BotMenu
    {
        public const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        public const string ColonyScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private const string Folder = "Builds/Bots";

        [MenuItem("TrollStrategy/Bots/Run Campaign Bots")]
        public static void RunAll()
        {
            try
            {
                var runs = RunProfiles(BotProfile.All, (i, profile) =>
                    EditorUtility.DisplayProgressBar("Боты играют кампанию", profile.Title, i / (float)BotProfile.All.Count));
                Debug.Log($"[Bots] reports in {Path.GetFullPath(Folder)}\n{BotReport.Summary(runs)}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void RunAllBatch()
        {
            int code = 0;
            try
            {
                Debug.Log($"[Bots] reports in {Path.GetFullPath(Folder)}\n{Run(null, 0, Folder)}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// <summary>
        /// Runs the profiles named in <paramref name="profileIds"/> (comma-separated; null or empty runs all) up to
        /// <paramref name="stopAfterLevel"/> (0: the whole chain), writes the reports and returns the summary.
        /// </summary>
        public static string Run(string profileIds, int stopAfterLevel, string folder)
        {
            var ids = string.IsNullOrEmpty(profileIds) ? null : profileIds.Split(',').Select(id => id.Trim()).ToList();
            var profiles = BotProfile.All.Where(p => ids == null || ids.Contains(p.Id)).ToList();
            return BotReport.Summary(RunProfiles(profiles, null, folder, stopAfterLevel));
        }

        public static List<BotRun> RunProfiles(IReadOnlyList<BotProfile> profiles, Action<int, BotProfile> progress,
            string folder = Folder, int stopAfterLevel = 0)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Боты читают сцену колонии: сначала выйдите из Play Mode");
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException($"Нет каталога: {CatalogPath}");
            var layout = SceneLayout(catalog);
            Directory.CreateDirectory(folder);
            var runs = new List<BotRun>();
            for (int i = 0; i < profiles.Count; i++)
            {
                progress?.Invoke(i, profiles[i]);
                var bot = new CampaignBot(new GameSession(catalog, layout, campaign: true), profiles[i])
                {
                    StopAfterLevel = stopAfterLevel
                };
                var run = bot.Run();
                runs.Add(run);
                File.WriteAllText(Path.Combine(folder, $"{run.Profile.Id}.md"), BotReport.Markdown(run));
                File.WriteAllText(Path.Combine(folder, $"{run.Profile.Id}.csv"), BotReport.Csv(run));
            }
            File.WriteAllText(Path.Combine(folder, "summary.md"), BotReport.Summary(runs));
            return runs;
        }

        /// <summary>
        /// The starting buildings as the colony scene places them. The scene is read where it is loaded, or opened
        /// beside the open scenes and closed again, so the editor keeps what it shows.
        /// </summary>
        public static List<StartingBuilding> SceneLayout(GameContentCatalog catalog)
        {
            var scene = SceneManager.GetSceneByPath(ColonyScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ColonyScenePath, OpenSceneMode.Additive);
            try
            {
                var world = scene.GetRootGameObjects()
                    .Select(root => root.GetComponentInChildren<TilemapWorldView>(true))
                    .FirstOrDefault(view => view != null);
                if (world == null) throw new InvalidOperationException($"В {ColonyScenePath} нет TilemapWorldView");
                return SceneBuildingPlacements.Collect(world, catalog)
                    .Where(p => p.View.gameObject.scene == scene)
                    .Select(p => p.Building).ToList();
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
