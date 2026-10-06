using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// Plays the bots' population (<see cref="BotPopulation"/>) on the shipped catalog and the colony scene's
    /// starting buildings, on every core at once, and writes the reports to Builds/Stats/bots (git-ignored):
    /// summary.md, population.csv, full reports of a few bots in runs/, and the report page (index.html with
    /// bots-data.js; history.jsonl keeps the earlier runs it compares against). Builds/Stats also holds the
    /// players' page (tools/stats/players.py) and the hub that shows both.
    /// Batch: -executeMethod TrollStrategy.Bots.BotMenu.RunAllBatch [-botCount 200].
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
        /// <summary>
        /// Where the rules the bots measure live (git pathspecs from the repository root): the domain, the
        /// application, content code and data, and the bots' play. The files that only report on the bots or check
        /// them are left out: changing them changes no result. tools/stats/code_state.py keeps the same list.
        /// </summary>
        public static readonly string[] RulePaths =
        {
            "unity/TrollStategy/Assets/Game/Runtime/Domain",
            "unity/TrollStategy/Assets/Game/Runtime/Application",
            "unity/TrollStategy/Assets/Game/Runtime/Content",
            "unity/TrollStategy/Assets/Game/Content/Definitions",
            "unity/TrollStategy/Assets/Game/Bots/*.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotMenu.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotReport.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotReportData.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotPopulationStats.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotInGame.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotParity.cs",
            ":(exclude)unity/TrollStategy/Assets/Game/Bots/SnapshotDigest.cs"
        };

        [MenuItem("TrollStrategy/Bots/Run Campaign Bots")]
        public static void RunAll() => RunInEditor(BotPopulation.DefaultCount);

        [MenuItem("TrollStrategy/Bots/Run Campaign Bots (1000)")]
        public static void RunLarge() => RunInEditor(BotPopulation.LargeCount);

        private static void RunInEditor(int count)
        {
            try
            {
                var result = RunPopulation(count, (done, total) => EditorUtility.DisplayCancelableProgressBar(
                    "Боты играют кампанию", $"{done} из {total}: каждый бот — своя партия, все ядра сразу", done / (float)total));
                if (result == null)
                {
                    Debug.Log("[Bots] the run was cancelled; the reports were left as they were");
                    return;
                }
                WriteHub(Folder);
                Debug.Log($"[Bots] report page {PagePath(Folder)}\n{BotReport.Headline(result.Value.Stats)}");
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

        /// <summary>Batch: [-botCount 200] personas, [-botStopAfter 0] quests; exits 1 when the run could not be played.</summary>
        public static void RunAllBatch()
        {
            int code = 0;
            try
            {
                int count = int.TryParse(Argument("-botCount"), out int n) && n > 0 ? n : BotPopulation.DefaultCount;
                int stopAfter = int.TryParse(Argument("-botStopAfter"), out int level) ? level : 0;
                var result = RunPopulation(count, null, Folder, stopAfter).Value;
                WriteHub(Folder);
                Debug.Log($"[Bots] report page {PagePath(Folder)}\n{BotReport.Headline(result.Stats)}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        /// <summary>Runs at once: every core but one, so the editor stays responsive.</summary>
        public static int Threads => Math.Max(1, Environment.ProcessorCount - 1);

        /// <summary>
        /// Plays personas 1 to <paramref name="count"/> on the shipped catalog and the colony scene's starting
        /// buildings, up to <see cref="Threads"/> at once, and writes the reports. <paramref name="progress"/> sees
        /// the finished count on the main thread and cancels with true; a cancelled run writes nothing and is null.
        /// </summary>
        public static (List<BotRun> Runs, BotPopulationStats Stats)? RunPopulation(int count,
            Func<int, int, bool> progress = null, string folder = Folder, int stopAfterLevel = 0)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Боты читают сцену колонии: сначала выйдите из Play Mode");
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException($"Нет каталога: {CatalogPath}");
            var layout = SceneLayout(catalog);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var runs = Play(BotPopulation.Personas(count), catalog, layout, stopAfterLevel, progress, Threads);
            if (runs == null) return null;

            var (commit, branch) = GitHead();
            var info = new BotReportInfo(DateTime.Now, commit, branch,
                string.Join(", ", layout.Select(b => $"{b.Kind} ({b.Cell.X}, {b.Cell.Y})")),
                catalog.Progression.Quests.Count, stopAfterLevel, UncommittedRules(),
                catalog.Progression.Quests.Select(q => q.Title).ToList())
            {
                WallSeconds = clock.Elapsed.TotalSeconds,
                Threads = Threads
            };
            return (runs, WriteReports(runs, info, folder));
        }

        /// <summary>
        /// Plays every profile to the end of the chain (or <paramref name="stopAfterLevel"/>), up to
        /// <paramref name="threads"/> at once, and returns the runs in the profiles' order. Each run has a session of
        /// its own; the catalog and the layout are only read. A run whose bot or session throws comes back
        /// <see cref="BotOutcome.Crashed"/> and is logged. Null when <paramref name="progress"/> cancelled.
        /// </summary>
        public static List<BotRun> Play(IReadOnlyList<BotProfile> profiles, GameContentCatalog catalog,
            IReadOnlyList<StartingBuilding> layout, int stopAfterLevel = 0, Func<int, int, bool> progress = null,
            int threads = 0)
        {
            int chain = catalog.Progression.Quests.Count;
            var runs = new BotRun[profiles.Count];
            var errors = new Exception[profiles.Count];
            int done = 0, cancelled = 0;
            void PlayOne(int i)
            {
                if (Volatile.Read(ref cancelled) != 0) return;
                try
                {
                    runs[i] = new CampaignBot(new GameSession(catalog, layout, campaign: true), profiles[i])
                    {
                        StopAfterLevel = stopAfterLevel
                    }.Run();
                }
                catch (Exception exception)
                {
                    errors[i] = exception;
                    runs[i] = new BotRun(profiles[i], stopAfterLevel > 0 ? stopAfterLevel : chain)
                    {
                        Outcome = BotOutcome.Crashed,
                        StopReason = $"{exception.GetType().Name}: {exception.Message}"
                    };
                }
                Interlocked.Increment(ref done);
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Threads };
            var work = Task.Run(() => Parallel.For(0, profiles.Count, options, PlayOne));
            while (!work.Wait(100))
                if (progress != null && progress(Volatile.Read(ref done), profiles.Count))
                    Interlocked.Exchange(ref cancelled, 1);
            if (cancelled != 0) return null;
            for (int i = 0; i < errors.Length; i++)
                if (errors[i] != null) Debug.LogError($"[Bots] {profiles[i].Id} crashed: {errors[i]}");
            return runs.ToList();
        }

        /// <summary>
        /// Writes the reports of a population run to <paramref name="folder"/>: summary.md, population.csv, the page
        /// with its data and history, and a full report in runs/ for every bot that did not finish and for the
        /// fastest, the median and the slowest. The reports of the run before go; the in-game check keeps its own.
        /// </summary>
        public static BotPopulationStats WriteReports(IReadOnlyList<BotRun> runs, BotReportInfo info, string folder)
        {
            var stats = BotPopulationStats.Of(runs, info.QuestTitles);
            Directory.CreateDirectory(folder);
            foreach (string file in Directory.GetFiles(folder, "*.md").Concat(Directory.GetFiles(folder, "*.csv")))
                if (Path.GetFileName(file) != "in-game.md") File.Delete(file);
            string reports = Path.Combine(folder, "runs");
            if (Directory.Exists(reports)) Directory.Delete(reports, true);
            Directory.CreateDirectory(reports);
            foreach (var run in runs.Where(r => stats.FullReports.Contains(r.Profile.Id)))
                File.WriteAllText(Path.Combine(reports, run.Profile.Id + ".md"), BotReport.Markdown(run));
            File.WriteAllText(Path.Combine(folder, "summary.md"), BotReport.Summary(stats, runs, info));
            File.WriteAllText(Path.Combine(folder, "population.csv"), BotReport.Population(runs));
            WritePage(runs, info, folder, stats);
            return stats;
        }

        /// <summary>
        /// The report page: the latest run in bots-data.js with the run history (one line per run in history.jsonl,
        /// the newest <see cref="HistoryLength"/> kept), and the page itself from its template. An open page reloads
        /// the data on its own when a new run writes it.
        /// </summary>
        public static void WritePage(IReadOnlyList<BotRun> runs, BotReportInfo info, string folder,
            BotPopulationStats stats = null)
        {
            stats ??= BotPopulationStats.Of(runs, info.QuestTitles);
            Directory.CreateDirectory(folder);
            string historyPath = Path.Combine(folder, "history.jsonl");
            var history = File.Exists(historyPath)
                ? File.ReadAllLines(historyPath).Where(line => line.Trim().Length > 0).ToList()
                : new List<string>();
            history.Add(BotReportData.HistoryLine(stats, runs, info));
            if (history.Count > HistoryLength) history.RemoveRange(0, history.Count - HistoryLength);
            File.WriteAllLines(historyPath, history);
            File.WriteAllText(Path.Combine(folder, "bots-data.js"), BotReportData.Script(stats, runs, info, history));
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

        /// <summary>
        /// Rule files (<see cref="RulePaths"/>) changed and not committed: a run on them measures work in progress,
        /// not the commit it names. Null when git cannot be asked.
        /// </summary>
        public static List<string> UncommittedRules()
        {
            var root = RepositoryRoot();
            if (root == null) return null;
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git",
                    "status --porcelain -- " + string.Join(" ", RulePaths.Select(p => $"\"{p}\"")))
                {
                    WorkingDirectory = root.FullName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var git = System.Diagnostics.Process.Start(start);
                string output = git.StandardOutput.ReadToEnd();
                if (!git.WaitForExit(10000) || git.ExitCode != 0) return null;
                return output.Split('\n').Where(line => line.Length > 3).Select(line => line.Substring(3).Trim()).ToList();
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException
                                                  or InvalidOperationException)
            {
                return null;
            }
        }

        // The folder that holds .git (a folder, or a file in a worktree); null outside a repository.
        private static DirectoryInfo RepositoryRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) &&
                   !File.Exists(Path.Combine(dir.FullName, ".git")))
                dir = dir.Parent;
            return dir;
        }

        /// <summary>The commit and branch the project is checked out at, read from .git; nulls outside a repository.</summary>
        internal static (string Commit, string Branch) GitHead()
        {
            try
            {
                var dir = RepositoryRoot();
                if (dir == null) return (null, null);
                string git = Path.Combine(dir.FullName, ".git");
                // a worktree's .git is a file that names its own git folder
                if (File.Exists(git))
                    git = Path.GetFullPath(Path.Combine(dir.FullName,
                        File.ReadAllText(git).Trim().Substring("gitdir:".Length).Trim()));
                string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return (Short(head), null);
                string reference = head.Substring(5);
                string branch = reference.StartsWith("refs/heads/", StringComparison.Ordinal)
                    ? reference.Substring(11)
                    : reference;
                // a worktree keeps the branches in the main repository's git folder
                string common = Path.Combine(git, "commondir");
                if (File.Exists(common)) git = Path.GetFullPath(Path.Combine(git, File.ReadAllText(common).Trim()));
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
