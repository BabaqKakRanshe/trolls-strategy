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
    /// reports to Builds/Stats/bots (git-ignored): summary.md, one Markdown and one CSV per profile, and the report
    /// page (index.html with bots-data.js; history.jsonl keeps the earlier runs it compares against). Builds/Stats
    /// also holds the players' page (tools/stats/players.py) and the hub that shows both.
    /// Batch: -executeMethod TrollStrategy.Bots.BotMenu.RunAllBatch -quit.
    /// </summary>
    public static class BotMenu
    {
        public const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        public const string ColonyScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        public const string PageTemplatePath = "Assets/Game/Bots/Dashboard/BotsDashboard.html";
        private const string Folder = "Builds/Stats/bots";
        /// <summary>The page over the bots' and the players' statistics, kept with the players' script.</summary>
        public const string HubTemplatePath = "../../tools/stats/templates/index.html";
        private const int HistoryLength = 50;

        [MenuItem("TrollStrategy/Bots/Run Campaign Bots")]
        public static void RunAll()
        {
            try
            {
                var runs = RunProfiles(BotProfile.All, (i, profile) =>
                    EditorUtility.DisplayProgressBar("Боты играют кампанию", profile.Title, i / (float)BotProfile.All.Count));
                WriteHub(Folder);
                Debug.Log($"[Bots] report page {PagePath(Folder)}\n{BotReport.Summary(runs)}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Opens the statistics hub (bots and players), or the bots' page where there is no hub yet.</summary>
        [MenuItem("TrollStrategy/Bots/Open Report Page")]
        public static void OpenPage()
        {
            string hub = PagePath(Path.Combine(Folder, "..")), page = File.Exists(hub) ? hub : PagePath(Folder);
            if (File.Exists(page)) UnityEngine.Application.OpenURL(new Uri(page).AbsoluteUri);
            else Debug.LogWarning($"[Bots] no report page yet: run TrollStrategy/Bots/Run Campaign Bots ({page})");
        }

        public static void RunAllBatch()
        {
            int code = 0;
            try
            {
                string summary = Run(null, 0, Folder);
                WriteHub(Folder);
                Debug.Log($"[Bots] report page {PagePath(Folder)}\n{summary}");
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

            var (commit, branch) = GitHead();
            var info = new BotReportInfo(DateTime.Now, commit, branch,
                string.Join(", ", layout.Select(b => $"{b.Kind} ({b.Cell.X}, {b.Cell.Y})")),
                catalog.Progression.Quests.Count, stopAfterLevel);
            WritePage(runs, info, folder);
            return runs;
        }

        /// <summary>
        /// The report page: the latest runs in bots-data.js with the run history (one line per set of runs in
        /// history.jsonl, the newest <see cref="HistoryLength"/> kept), and the page itself from its template.
        /// An open page reloads the data on its own when a new run writes it.
        /// </summary>
        public static void WritePage(IReadOnlyList<BotRun> runs, BotReportInfo info, string folder)
        {
            Directory.CreateDirectory(folder);
            string historyPath = Path.Combine(folder, "history.jsonl");
            var history = File.Exists(historyPath)
                ? File.ReadAllLines(historyPath).Where(line => line.Trim().Length > 0).ToList()
                : new List<string>();
            history.Add(BotReportData.HistoryLine(runs, info));
            if (history.Count > HistoryLength) history.RemoveRange(0, history.Count - HistoryLength);
            File.WriteAllLines(historyPath, history);
            File.WriteAllText(Path.Combine(folder, "bots-data.js"), BotReportData.Script(runs, info, history));
            if (File.Exists(PageTemplatePath)) File.Copy(PageTemplatePath, PagePath(folder), true);
            else Debug.LogWarning($"[Bots] no page template at {PageTemplatePath}");
        }

        /// <summary>The hub one folder above the bots' page, from <see cref="HubTemplatePath"/>.</summary>
        public static void WriteHub(string folder)
        {
            string template = Path.GetFullPath(HubTemplatePath);
            if (File.Exists(template)) File.Copy(template, PagePath(Path.Combine(folder, "..")), true);
            else Debug.LogWarning($"[Bots] no hub template at {template}");
        }

        private static string PagePath(string folder) => Path.GetFullPath(Path.Combine(folder, "index.html"));

        // The commit and branch the project is checked out at, read from .git; nulls outside a repository.
        private static (string Commit, string Branch) GitHead()
        {
            try
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git"))) dir = dir.Parent;
                if (dir == null) return (null, null);
                string git = Path.Combine(dir.FullName, ".git");
                string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return (Short(head), null);
                string reference = head.Substring(5);
                string branch = reference.StartsWith("refs/heads/", StringComparison.Ordinal)
                    ? reference.Substring(11)
                    : reference;
                string loose = Path.Combine(git, reference.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(loose)) return (Short(File.ReadAllText(loose).Trim()), branch);
                string packed = Path.Combine(git, "packed-refs");
                if (File.Exists(packed))
                    foreach (string line in File.ReadLines(packed))
                        if (line.EndsWith(" " + reference, StringComparison.Ordinal))
                            return (Short(line.Substring(0, line.IndexOf(' '))), branch);
                return (null, branch);
            }
            catch (IOException)
            {
                return (null, null);
            }
        }

        private static string Short(string hash) => hash.Length > 7 ? hash.Substring(0, 7) : hash;

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
