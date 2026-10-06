using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Plays bots inside the game itself: the colony scene in Play Mode with its <see cref="GameBootstrap"/> session,
    /// HUD and views, the colony clock run by the scene's own frames (sped up). Beside it the same bot plays a plain
    /// session without the scene, brought to the same colony second at every look (<see cref="BotParity"/>). A match
    /// look for look shows that the headless reports measure the game as it runs; the first difference, commands
    /// the scene sent on its own and errors in the console are written down.
    /// Writes Builds/Stats/bots/in-game.md and in-game-data.js (the statistics hub shows it).
    /// Batch: -executeMethod TrollStrategy.Bots.BotInGame.RunBatch [-botProfiles p1,p2,p3] [-botSpeed 30]
    /// [-botStopAfter 12]; the editor exits when the last bot is done (code 1 if a check failed). Bots are named as
    /// <see cref="BotPopulation.Find"/> reads them: personas p1, p2… and the tests' reference bot "typical".
    /// </summary>
    [InitializeOnLoad]
    public static class BotInGame
    {
        private const string Key = "TrollStrategy.BotInGame.";
        private const string Folder = "Builds/Stats/bots";
        private const float DefaultSpeed = 30f;
        /// <summary>Real seconds the scene may take to start the colony, or keep its clock still, before the check gives up.</summary>
        private const double PatienceSeconds = 60;
        private const int MaxRestarts = 2;
        private const int MaxErrors = 20;

        private static BotParity s_parity;
        private static GameSession s_live;
        private static int s_lastClockMs;
        private static double s_clockMovedAt, s_startedAt;
        private static readonly List<string> s_errors = new();
        private static int s_errorCount;

        static BotInGame()
        {
            if (Phase.Length > 0) Attach();
        }

        /// <summary>The personas the check plays when none are named: a few, as each takes minutes of real time.</summary>
        private static readonly string[] Sample = { BotPopulation.Id(1), BotPopulation.Id(2), BotPopulation.Id(3) };

        [MenuItem("TrollStrategy/Bots/Play Bot In Game")]
        public static void PlayOne() => Start(Sample.Take(1), DefaultSpeed, 0, false);

        [MenuItem("TrollStrategy/Bots/Play Three Bots In Game")]
        public static void PlayThree() => Start(Sample, DefaultSpeed, 0, false);

        [MenuItem("TrollStrategy/Bots/Stop Bots In Game")]
        public static void StopAll()
        {
            if (Phase.Length == 0) return;
            Fail("остановлено из меню");
            Abort();
        }

        public static void RunBatch()
        {
            string profiles = Argument("-botProfiles") ?? string.Join(",", Sample);
            float speed = float.TryParse(Argument("-botSpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : DefaultSpeed;
            int stopAfter = int.TryParse(Argument("-botStopAfter"), out int level) ? level : 0;
            try
            {
                Start(profiles.Split(',').Select(p => p.Trim()), speed, stopAfter, true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Starts a check of the given profiles; it runs on the editor's update until the last one is done.</summary>
        public static void Start(IEnumerable<string> profileIds, float speed, int stopAfterLevel, bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Бот в игре сам запускает Play Mode: сначала выйдите из него");
            if (Phase.Length > 0) throw new InvalidOperationException("Проверка в игре уже идёт");
            var ids = profileIds.ToList();
            var unknown = ids.Where(id => BotPopulation.Find(id) == null).ToList();
            if (ids.Count == 0 || unknown.Count > 0)
                throw new ArgumentException($"Нет таких ботов: {string.Join(", ", unknown)}");
            var catalog = Catalog();
            var layout = BotMenu.SceneLayout(catalog);

            Directory.CreateDirectory(ResultFolder);
            // a profile's result and its report line come from this job only
            foreach (string file in Directory.GetFiles(ResultFolder, "*.json").Concat(Directory.GetFiles(ResultFolder, "*.md")))
                File.Delete(file);
            SessionState.SetString(Key + "Profiles", string.Join(",", ids));
            SessionState.SetInt(Key + "Index", 0);
            SessionState.SetFloat(Key + "Speed", Mathf.Clamp(speed, 1f, 100f));
            SessionState.SetInt(Key + "StopAfter", stopAfterLevel);
            SessionState.SetBool(Key + "Batch", batch);
            SessionState.SetString(Key + "Layout", string.Join("|", layout.Select(b => $"{b.Kind}:{b.Cell.X}:{b.Cell.Y}")));
            SessionState.SetBool(Key + "RunInBackground", PlayerSettings.runInBackground);
            SessionState.SetString(Key + "StartScene",
                EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
            SessionState.SetInt(Key + "Restarts", 0);
            Phase = "enter";
            Debug.Log($"[Bots in game] {string.Join(", ", ids)} at ×{speed:0.#}");
            Attach();
        }

        private static string ResultFolder => Path.Combine(Folder, "in-game");

        private static string Phase
        {
            get => SessionState.GetString(Key + "Phase", "");
            set => SessionState.SetString(Key + "Phase", value);
        }

        private static List<string> Profiles => SessionState.GetString(Key + "Profiles", "")
            .Split(',').Where(id => id.Length > 0).ToList();

        private static BotProfile Current
        {
            get
            {
                var profiles = Profiles;
                int index = SessionState.GetInt(Key + "Index", 0);
                return index < profiles.Count ? BotPopulation.Find(profiles[index]) : null;
            }
        }

        private static GameContentCatalog Catalog() =>
            AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath)
            ?? throw new InvalidOperationException($"Нет каталога: {BotMenu.CatalogPath}");

        private static List<StartingBuilding> Layout() => SessionState.GetString(Key + "Layout", "")
            .Split('|').Where(part => part.Length > 0).Select(part =>
            {
                var bits = part.Split(':');
                return new StartingBuilding((BuildingKind)Enum.Parse(typeof(BuildingKind), bits[0]),
                    new Domain.Cell(int.Parse(bits[1]), int.Parse(bits[2])));
            }).ToList();

        private static void Attach()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                switch (Phase)
                {
                    case "enter": Enter(); break;
                    case "play": Play(); break;
                    case "leave": Leave(); break;
                    case "": EditorApplication.update -= Update; break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Phase == "play")
                {
                    // this profile failed; the others still play
                    Fail($"сбой проверки: {exception.GetType().Name}: {exception.Message}");
                    Close();
                    return;
                }
                // between profiles or while writing the report: the job cannot go on
                bool batch = SessionState.GetBool(Key + "Batch", false);
                Phase = "";
                EditorApplication.update -= Update;
                if (batch) EditorApplication.Exit(1);
            }
        }

        private static void Enter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BotMenu.ColonyScenePath);
            // an unfocused editor would hold Play Mode at its second frame
            PlayerSettings.runInBackground = true;
            s_parity = null;
            s_startedAt = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Key + "Running", false);
            Phase = "play";
            EditorApplication.isPlaying = true;
        }

        private static void Play()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(Key + "Running", false))
                {
                    Fail("Play Mode остановили во время игры");
                    Abort();
                }
                else if (now - s_startedAt > PatienceSeconds) Restart("Play Mode не запустился");
                return;
            }

            if (s_parity == null)
            {
                // scripts reloaded inside Play Mode (a batch editor does it once): the scene lost its session
                if (SessionState.GetBool(Key + "Running", false))
                {
                    Restart("скрипты перезагрузились во время игры");
                    return;
                }
                var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
                var session = bootstrap != null ? bootstrap.Session : null;
                if (session == null || !session.IsCampaign)
                {
                    if (now - s_startedAt > PatienceSeconds) Restart("сцена не создала кампанию");
                    return;
                }
                s_live = session;
                s_parity = new BotParity(session, new GameSession(Catalog(), Layout(), campaign: true), Current,
                    SessionState.GetInt(Key + "StopAfter", 0));
                s_errors.Clear();
                s_errorCount = 0;
                UnityEngine.Application.logMessageReceived -= NoteLog;
                UnityEngine.Application.logMessageReceived += NoteLog;
                Time.timeScale = SessionState.GetFloat(Key + "Speed", DefaultSpeed);
                s_lastClockMs = session.ActiveTimeMs;
                s_clockMovedAt = now;
                s_startedAt = now;
                SessionState.SetBool(Key + "Running", true);
            }

            bool goesOn = s_parity.Tick();
            if (s_live.ActiveTimeMs != s_lastClockMs)
            {
                s_lastClockMs = s_live.ActiveTimeMs;
                s_clockMovedAt = now;
            }
            else if (goesOn && now - s_clockMovedAt > PatienceSeconds)
            {
                Write(Result($"колония в игре стоит {PatienceSeconds:0} с: {SessionDigest.Describe(s_live)}", out string stuck), stuck);
                Close();
                return;
            }
            if (goesOn) return;
            Write(Result(null, out string line), line);
            Close();
        }

        // Leaves Play Mode; the next update in edit mode goes on with the next profile.
        private static void Close()
        {
            UnityEngine.Application.logMessageReceived -= NoteLog;
            s_parity?.Dispose();
            s_parity = null;
            s_live = null;
            Time.timeScale = 1f;
            SessionState.SetBool(Key + "Running", false);
            Phase = "leave";
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void Restart(string why)
        {
            int restarts = SessionState.GetInt(Key + "Restarts", 0) + 1;
            Debug.LogWarning($"[Bots in game] {why}; restart {restarts}");
            if (restarts > MaxRestarts)
            {
                Fail(why);
                Close();
                return;
            }
            SessionState.SetInt(Key + "Restarts", restarts);
            UnityEngine.Application.logMessageReceived -= NoteLog;
            s_parity?.Dispose();
            s_parity = null;
            Time.timeScale = 1f;
            SessionState.SetBool(Key + "Running", false);
            Phase = "enter";
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void Leave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            RestoreEditor();
            SessionState.SetInt(Key + "Restarts", 0);
            int index = SessionState.GetInt(Key + "Index", 0) + 1;
            SessionState.SetInt(Key + "Index", index);
            if (index < Profiles.Count)
            {
                Phase = "enter";
                return;
            }
            Finish();
        }

        private static void Abort()
        {
            UnityEngine.Application.logMessageReceived -= NoteLog;
            s_parity?.Dispose();
            s_parity = null;
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            RestoreEditor();
            Finish();
        }

        private static void RestoreEditor()
        {
            string start = SessionState.GetString(Key + "StartScene", "");
            EditorSceneManager.playModeStartScene = start.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(start) : null;
            PlayerSettings.runInBackground = SessionState.GetBool(Key + "RunInBackground", false);
        }

        // The report from every profile's result, then the job is over.
        private static void Finish()
        {
            bool batch = SessionState.GetBool(Key + "Batch", false);
            var results = Directory.Exists(ResultFolder)
                ? Profiles.Select(id => Path.Combine(ResultFolder, id + ".json")).Where(File.Exists)
                    .Select(File.ReadAllText).ToList()
                : new List<string>();
            var failed = results.Count(r => !r.Contains("\"same\":true"));
            var (commit, branch) = BotMenu.GitHead();
            var uncommitted = BotMenu.UncommittedRules();
            File.WriteAllText(Path.Combine(Folder, "in-game-data.js"), "window.BOT_IN_GAME = " + BotReportData.Obj(
                ("generatedAt", BotReportData.Str(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))),
                ("commit", BotReportData.Str(commit)),
                ("branch", BotReportData.Str(branch)),
                ("uncommitted", uncommitted == null ? "null" : BotReportData.Arr(uncommitted, BotReportData.Str)),
                ("speed", BotReportData.Num(SessionState.GetFloat(Key + "Speed", DefaultSpeed))),
                ("runs", "[" + string.Join(",\n", results) + "]")) + ";\n");
            var lines = Profiles.Select(id => Path.Combine(ResultFolder, id + ".md")).Where(File.Exists)
                .Select(File.ReadAllText).ToList();
            File.WriteAllText(Path.Combine(Folder, "in-game.md"), Markdown(lines, failed, commit, uncommitted));
            foreach (string name in new[] { "Profiles", "Layout", "StartScene" }) SessionState.EraseString(Key + name);
            foreach (string name in new[] { "Index", "StopAfter", "Restarts" }) SessionState.EraseInt(Key + name);
            foreach (string name in new[] { "Batch", "RunInBackground", "Running" }) SessionState.EraseBool(Key + name);
            SessionState.EraseFloat(Key + "Speed");
            Phase = "";
            EditorApplication.update -= Update;
            Debug.Log($"[Bots in game] done: {results.Count - failed} of {results.Count} matched, {Path.GetFullPath(Path.Combine(Folder, "in-game.md"))}");
            if (batch) EditorApplication.Exit(failed > 0 || results.Count == 0 ? 1 : 0);
        }

        private static void NoteLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            s_errorCount++;
            if (s_errors.Count < MaxErrors) s_errors.Add(message.Split('\n')[0]);
        }

        private static void Fail(string why)
        {
            var profile = Current;
            if (profile == null) return;
            Write(BotReportData.Obj(
                ("id", BotReportData.Str(profile.Id)),
                ("title", BotReportData.Str(profile.Title)),
                ("same", "false"),
                ("looks", "0"),
                ("difference", BotReportData.Str(why))), $"- **{profile.Title}**: проверка не прошла — {why}" + Environment.NewLine);
        }

        private static string Result(string stopped, out string markdown)
        {
            var profile = Current;
            var (live, shadow) = s_parity.Finish();
            var headless = new CampaignBot(new GameSession(Catalog(), Layout(), campaign: true), profile)
            {
                StopAfterLevel = SessionState.GetInt(Key + "StopAfter", 0)
            }.Run();
            string difference = stopped ?? s_parity.Difference;
            var text = new StringBuilder();
            text.Append($"- **{profile.Title}**: {s_parity.Looks} взглядов, ")
                .Append(difference == null ? "колонии совпали до конца" : "колонии разошлись")
                .Append($"; кампания в игре {Minutes(live?.EndMs)}, без сцены {Minutes(shadow?.EndMs)}, ")
                .Append($"на своих часах {Minutes(headless.EndMs)}; ")
                .AppendLine($"команд от сцены: {s_parity.ForeignCommands.Count}, ошибок в консоли: {s_errorCount}");
            if (difference != null)
            {
                text.AppendLine().AppendLine("  ```");
                foreach (string line in difference.Split('\n')) text.AppendLine("  " + line);
                text.AppendLine("  ```").AppendLine();
            }
            foreach (string command in s_parity.ForeignCommands.Take(5)) text.AppendLine($"  - команда сцены: {command}");
            foreach (string error in s_errors.Take(5)) text.AppendLine($"  - ошибка: {error}");
            markdown = text.ToString();
            return BotReportData.Obj(
                ("id", BotReportData.Str(profile.Id)),
                ("title", BotReportData.Str(profile.Title)),
                ("same", difference == null ? "true" : "false"),
                ("looks", BotReportData.Num(s_parity.Looks)),
                ("difference", BotReportData.Str(difference)),
                ("foreignCommands", BotReportData.Arr(s_parity.ForeignCommands.Take(MaxErrors), BotReportData.Str)),
                ("foreignCount", BotReportData.Num(s_parity.ForeignCommands.Count)),
                ("errors", BotReportData.Arr(s_errors, BotReportData.Str)),
                ("errorCount", BotReportData.Num(s_errorCount)),
                ("realSeconds", BotReportData.Num(Math.Round(EditorApplication.timeSinceStartup - s_startedAt))),
                ("inGame", Outcome(live)),
                ("withoutScene", Outcome(shadow)),
                ("ownClock", Outcome(headless)));
        }

        private static string Outcome(BotRun run) => run == null ? "null" : BotReportData.Obj(
            ("outcome", BotReportData.Str(run.Outcome.ToString())),
            ("levels", BotReportData.Num(run.Quests.Count)),
            ("endMs", BotReportData.Num(run.EndMs)),
            ("finalGold", BotReportData.Num(run.FinalGold)),
            ("finalPopulation", BotReportData.Num(run.FinalPopulation)),
            ("commands", BotReportData.Num(run.CommandsAccepted)),
            ("decisions", BotReportData.Num(run.Decisions)));

        private static string Minutes(int? ms) => ms == null ? "—" : $"{ms.Value / 60000f:0.0} мин";

        private static void Write(string result, string markdown)
        {
            var profile = Current;
            if (profile == null) return;
            Directory.CreateDirectory(ResultFolder);
            File.WriteAllText(Path.Combine(ResultFolder, profile.Id + ".json"), result);
            File.WriteAllText(Path.Combine(ResultFolder, profile.Id + ".md"), markdown);
        }

        private static string Markdown(IReadOnlyList<string> lines, int failed, string commit,
            IReadOnlyList<string> uncommitted)
        {
            int count = lines.Count;
            var text = new StringBuilder();
            text.AppendLine("# Боты в игре");
            text.AppendLine();
            text.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm}, коммит {commit ?? "?"}, ×{SessionState.GetFloat(Key + "Speed", DefaultSpeed):0.#}." +
                            (uncommitted is { Count: > 0 } ? $" Незакоммиченные правки правил: {uncommitted.Count}." : ""));
            text.AppendLine();
            text.AppendLine(failed == 0 && count > 0
                ? $"Все {count} профилей: колония в игре и без сцены совпала на каждом взгляде."
                : $"Совпало {count - failed} из {count}.");
            text.AppendLine();
            foreach (string line in lines) text.Append(line);
            text.AppendLine();
            text.AppendLine("«В игре» — сцена колонии в Play Mode, часы идут кадрами сцены; «без сцены» — та же игра без сцены, " +
                            "взгляды в те же секунды колонии; «на своих часах» — обычный прогон отчёта ботов.");
            return text.ToString();
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
