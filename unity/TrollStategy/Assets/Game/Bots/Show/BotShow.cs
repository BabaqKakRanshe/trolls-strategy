using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TrollStrategy.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// The show bot: a campaign bot that plays the real game with a mouse and a keyboard of its own, in the colony
    /// scene in Play Mode, and records it. The cursor, its clicks and what the bot thinks are drawn over the game; the
    /// video, its subtitles and a timeline page that jumps the video to any move land in
    /// Builds/Stats/bots/show/&lt;bot&gt;-&lt;time&gt;. Moves the HUD could not make are listed there with a picture.
    /// Batch (needs graphics, no -nographics): -executeMethod TrollStrategy.Bots.BotShow.RunBatch [-botProfile p1]
    /// [-botStopAfter 5] [-botNoVideo] [-botIdleSpeed 8] [-botHandSpeed 1.25] [-botVideoSpeed 1] [-botUncapped] [-botSkipAnimations] [-botFast] [-botMaxMinutes 150].
    /// </summary>
    [InitializeOnLoad]
    public static class BotShow
    {
        private const string Key = "TrollStrategy.BotShow.";
        public const string Root = "Builds/Stats/bots/show";
        private const string PagePath = "Assets/Game/Bots/Show/BotShowPage.html";
        private const double PatienceSeconds = 90;
        private const int MaxRestarts = 2;
        private const int VideoWidth = 1920, VideoHeight = 1080;
        /// <summary>Player prefs a new game's welcome window writes; the run puts them back as they were.</summary>
        private static readonly string[] Prefs = { "intro.seen", "settings.tutorialHints", "tutorial.controls", "telemetry.collect" };

        private static BotShowRunner s_runner;
        private static double s_startedAt;

        static BotShow()
        {
            // a reload in the middle (a recompile) loses the run: the editor gets its mouse and settings back
            if (Phase.Length > 0) Attach();
            else BotMouse.RestoreAfterReload();
            EditorApplication.quitting -= OnQuit;
            EditorApplication.quitting += OnQuit;
        }

        // The editor closed in the middle of a run (its window was closed): the video is finished so it plays, the
        // report is written up to that moment, and the editor's settings go back.
        private static void OnQuit()
        {
            if (Phase.Length == 0) return;
            try
            {
                if (s_runner != null)
                {
                    if (!s_runner.Finished) s_runner.End("редактор закрыли во время игры");
                    Write(s_runner);
                }
                BotMouse.RestoreAfterReload();
                PlayerSettings.runInBackground = SessionState.GetBool(Key + "RunInBackground", false);
                foreach (string line in SessionState.GetString(Key + "Prefs", "").Split('\n')) RestorePref(line);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("TrollStrategy/Bots/Show/Record Bot Playthrough")]
        public static void RecordAll() => Start(Options(BotPopulation.Id(1), 0, true));

        /// <summary>
        /// The whole campaign as fast as the HUD allows: the editor draws without a frame cap (the game runs several
        /// times faster than real time), a quick hand, the colony at ×30 while the bot waits, rewards and battle replays
        /// skipped, the video ×2.
        /// </summary>
        [MenuItem("TrollStrategy/Bots/Show/Record Bot Playthrough (Fast)")]
        public static void RecordFast() => Start(Fast(Options(BotPopulation.Id(1), 0, true)));

        private static BotShowOptions Fast(BotShowOptions options)
        {
            options.HandSpeed = 3f;
            options.IdleSpeed = 30f;
            options.VideoSpeed = 2;
            options.Uncapped = true;
            options.SkipAnimations = true;
            return options;
        }

        /// <summary>
        /// The ten personas that differ most (strategy first) recorded one after another, fast, and a gallery page that
        /// sets them side by side.
        /// </summary>
        [MenuItem("TrollStrategy/Bots/Show/Record Ten Different Bots (Fast)")]
        public static void RecordTen()
        {
            var ids = BotPopulation.MostDifferent(10).Select(p => p.Id).ToList();
            StartSeries(ids, Fast(Options(ids[0], 0, true)));
        }

        [MenuItem("TrollStrategy/Bots/Show/Open All Recordings")]
        public static void OpenGallery()
        {
            WriteGallery();
            string page = Path.Combine(Root, "index.html");
            if (File.Exists(page)) UnityEngine.Application.OpenURL(new Uri(Path.GetFullPath(page)).AbsoluteUri);
        }

        [MenuItem("TrollStrategy/Bots/Show/Record First Five Quests")]
        public static void RecordFive() => Start(Options(BotPopulation.Id(1), 5, true));

        [MenuItem("TrollStrategy/Bots/Show/Watch Bot Play (No Video)")]
        public static void Watch() => Start(Options(BotPopulation.Id(1), 0, false));

        [MenuItem("TrollStrategy/Bots/Show/Stop Bot Show")]
        public static void Stop()
        {
            if (Phase.Length == 0) return;
            if (s_runner != null) s_runner.End("остановлено из меню");
            else if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        [MenuItem("TrollStrategy/Bots/Show/Open Last Recording")]
        public static void OpenLast()
        {
            string last = Path.Combine(Root, "last.txt");
            string folder = File.Exists(last) ? File.ReadAllText(last).Trim() : null;
            if (folder == null || !File.Exists(Path.Combine(folder, "index.html")))
            {
                EditorUtility.DisplayDialog("Бот с мышкой", "Записей ещё нет: TrollStrategy → Bots → Show → Record…", "OK");
                return;
            }
            UnityEngine.Application.OpenURL(new Uri(Path.GetFullPath(Path.Combine(folder, "index.html"))).AbsoluteUri);
        }

        public static void RunBatch()
        {
            try
            {
                var options = Options(Argument("-botProfile") ?? BotPopulation.Id(1),
                    int.TryParse(Argument("-botStopAfter"), out int level) ? level : 0,
                    !Environment.GetCommandLineArgs().Contains("-botNoVideo"));
                if (float.TryParse(Argument("-botIdleSpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out float idle)) options.IdleSpeed = idle;
                if (float.TryParse(Argument("-botHandSpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out float hand)) options.HandSpeed = hand;
                if (float.TryParse(Argument("-botMaxMinutes"), NumberStyles.Float, CultureInfo.InvariantCulture, out float max)) options.MaxVideoMinutes = max;
                if (Environment.GetCommandLineArgs().Contains("-botFast")) Fast(options);
                var series = (Argument("-botProfiles")?.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList()) ??
                             (int.TryParse(Argument("-botDifferent"), out int different)
                                 ? BotPopulation.MostDifferent(different).Select(p => p.Id).ToList()
                                 : null);
                if (int.TryParse(Argument("-botVideoSpeed"), out int video)) options.VideoSpeed = Math.Max(1, video);
                if (Environment.GetCommandLineArgs().Contains("-botUncapped")) options.Uncapped = true;
                if (Environment.GetCommandLineArgs().Contains("-botSkipAnimations")) options.SkipAnimations = true;
                SessionState.SetBool(Key + "Batch", true);
                if (series is { Count: > 0 }) StartSeries(series, options);
                else Start(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static BotShowOptions Options(string profileId, int stopAfter, bool record)
        {
            var profile = BotPopulation.Find(profileId) ?? throw new ArgumentException($"Нет такого бота: {profileId}");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            return new BotShowOptions
            {
                Profile = profile,
                StopAfterLevel = stopAfter,
                Record = record,
                Folder = Path.Combine(Root, $"{profile.Id}-{stamp}")
            };
        }

        /// <summary>Records the bots one after another with the same options; each gets its own folder and page.</summary>
        public static void StartSeries(IReadOnlyList<string> ids, BotShowOptions options)
        {
            if (ids.Count == 0) return;
            var unknown = ids.Where(id => BotPopulation.Find(id) == null).ToList();
            if (unknown.Count > 0) throw new ArgumentException($"Нет таких ботов: {string.Join(", ", unknown)}");
            string series = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            SessionState.SetString(Key + "Queue", string.Join(",", ids.Skip(1)));
            SessionState.SetString(Key + "Series", series);
            options.Profile = BotPopulation.Find(ids[0]);
            options.Folder = Path.Combine(Root, $"{ids[0]}-{series}");
            Start(options);
        }

        public static void Start(BotShowOptions options)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Бот с мышкой сам запускает Play Mode: сначала выйдите из него");
            if (Phase.Length > 0) throw new InvalidOperationException("Бот с мышкой уже играет");
            if (EditorSceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            SessionState.SetString(Key + "Profile", options.Profile.Id);
            SessionState.SetInt(Key + "StopAfter", options.StopAfterLevel);
            SessionState.SetBool(Key + "Record", options.Record);
            SessionState.SetFloat(Key + "Idle", options.IdleSpeed);
            SessionState.SetFloat(Key + "Hand", options.HandSpeed);
            SessionState.SetInt(Key + "VideoSpeed", options.VideoSpeed);
            SessionState.SetBool(Key + "Uncapped", options.Uncapped);
            SessionState.SetBool(Key + "Skip", options.SkipAnimations);
            SessionState.SetFloat(Key + "Max", options.MaxVideoMinutes);
            SessionState.SetString(Key + "Folder", options.Folder);
            SessionState.SetBool(Key + "RunInBackground", PlayerSettings.runInBackground);
            SessionState.SetString(Key + "StartScene",
                EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
            PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
            SessionState.SetInt(Key + "Width", (int)width);
            SessionState.SetInt(Key + "Height", (int)height);
            SessionState.SetString(Key + "Prefs", string.Join("\n", Prefs.Select(SavePref)));
            Phase = "enter";
            Debug.Log($"[Bot show] {options.Profile.Id}: {(options.Record ? "запись" : "без записи")} → {options.Folder}");
            Attach();
        }

        private static string Phase
        {
            get => SessionState.GetString(Key + "Phase", "");
            set => SessionState.SetString(Key + "Phase", value);
        }

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
                SessionState.SetString(Key + "Failure", exception.Message);
                if (EditorApplication.isPlaying)
                {
                    s_runner?.End($"сбой: {exception.Message}");
                    Phase = "leave";
                    EditorApplication.isPlaying = false;
                }
                else Leave();
            }
        }

        private static void Enter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BotMenu.ColonyScenePath);
            // an unfocused editor would hold Play Mode at its second frame
            PlayerSettings.runInBackground = true;
            PlayModeWindow.SetCustomRenderingResolution(VideoWidth, VideoHeight, "Bot show 1920×1080");
            s_runner = null;
            s_startedAt = EditorApplication.timeSinceStartup;
            Phase = "play";
            EditorApplication.isPlaying = true;
        }

        private static void Play()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (s_runner == null && now - s_startedAt < PatienceSeconds) return;
                if (s_runner != null && !s_runner.Finished) s_runner.End("Play Mode остановили");
                Phase = "leave";
                return;
            }
            if (s_runner == null)
            {
                if (SessionState.GetBool(Key + "Running", false))
                {
                    // scripts reloaded inside Play Mode (an editor does it once right after its first entry): the
                    // runner and the scene's session are gone; the game starts over
                    Restart("скрипты перезагрузились во время игры");
                    return;
                }
                var game = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
                if (game == null || game.Session == null || !game.Session.IsCampaign)
                {
                    // a scene whose bootstrap ran before a reload keeps no session
                    if (game != null && game.Session == null && now - s_startedAt > 10) Restart("сцена потеряла сессию");
                    else if (now - s_startedAt > PatienceSeconds) Restart("сцена не создала кампанию");
                    return;
                }
                // a game view left at another size (a window docked small) would make a tiny video: ask once more
                if ((Screen.width != VideoWidth || Screen.height != VideoHeight) && now - s_startedAt < 12)
                {
                    PlayModeWindow.SetCustomRenderingResolution(VideoWidth, VideoHeight, "Bot show 1920×1080");
                    return;
                }
                s_runner = new BotShowRunner();
                s_runner.Begin(game, new BotShowOptions
                {
                    Profile = BotPopulation.Find(SessionState.GetString(Key + "Profile", BotPopulation.Id(1))),
                    StopAfterLevel = SessionState.GetInt(Key + "StopAfter", 0),
                    Record = SessionState.GetBool(Key + "Record", true),
                    IdleSpeed = SessionState.GetFloat(Key + "Idle", 8f),
                    HandSpeed = SessionState.GetFloat(Key + "Hand", 1.25f),
                    VideoSpeed = SessionState.GetInt(Key + "VideoSpeed", 1),
                    Uncapped = SessionState.GetBool(Key + "Uncapped", false),
                    SkipAnimations = SessionState.GetBool(Key + "Skip", false),
                    MaxVideoMinutes = SessionState.GetFloat(Key + "Max", 150f),
                    Folder = SessionState.GetString(Key + "Folder", Root)
                });
                SessionState.SetBool(Key + "Running", true);
                return;
            }
            if (!s_runner.Finished) return;
            Write(s_runner);
            Phase = "leave";
            EditorApplication.isPlaying = false;
        }

        private static void Restart(string why)
        {
            int restarts = SessionState.GetInt(Key + "Restarts", 0) + 1;
            Debug.LogWarning($"[Bot show] {why}; заново, попытка {restarts}");
            SessionState.SetBool(Key + "Running", false);
            if (restarts > MaxRestarts)
            {
                SessionState.SetString(Key + "Failure", why);
                Phase = "leave";
            }
            else
            {
                SessionState.SetInt(Key + "Restarts", restarts);
                Phase = "enter";
            }
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void Leave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (s_runner != null && !File.Exists(Path.Combine(SessionState.GetString(Key + "Folder", Root), "show-data.js")))
                Write(s_runner);
            Time.timeScale = 1f;
            Time.captureFramerate = 0;
            BotMouse.RestoreAfterReload();
            string start = SessionState.GetString(Key + "StartScene", "");
            EditorSceneManager.playModeStartScene = start.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(start) : null;
            PlayerSettings.runInBackground = SessionState.GetBool(Key + "RunInBackground", false);
            int width = SessionState.GetInt(Key + "Width", 0), height = SessionState.GetInt(Key + "Height", 0);
            if (width > 0 && height > 0) PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Before bot show");
            foreach (string line in SessionState.GetString(Key + "Prefs", "").Split('\n')) RestorePref(line);
            PlayerPrefs.Save();

            // a series goes on with its next bot: a fresh colony, the welcome window again, as a new player meets it
            string queue = SessionState.GetString(Key + "Queue", "");
            if (queue.Length > 0)
            {
                var ids = queue.Split(',');
                string series = SessionState.GetString(Key + "Series", "series");
                SessionState.SetString(Key + "Queue", string.Join(",", ids.Skip(1)));
                SessionState.SetString(Key + "Profile", ids[0]);
                SessionState.SetString(Key + "Folder", Path.Combine(Root, $"{ids[0]}-{series}"));
                SessionState.EraseString(Key + "Failure");
                SessionState.SetInt(Key + "Restarts", 0);
                SessionState.SetBool(Key + "Running", false);
                s_runner = null;
                WriteGallery();
                Debug.Log($"[Bot show] дальше {ids[0]}, в очереди ещё {ids.Length - 1}");
                Phase = "enter";
                return;
            }
            bool wasSeries = SessionState.GetString(Key + "Series", "").Length > 0;
            WriteGallery();

            string folder = SessionState.GetString(Key + "Folder", Root);
            string failure = SessionState.GetString(Key + "Failure", "");
            bool batch = SessionState.GetBool(Key + "Batch", false);
            bool written = File.Exists(Path.Combine(folder, "index.html"));
            foreach (string name in new[] { "Profile", "Folder", "StartScene", "Prefs", "Failure", "Queue", "Series" })
                SessionState.EraseString(Key + name);
            foreach (string name in new[] { "StopAfter", "Width", "Height", "Restarts", "VideoSpeed" }) SessionState.EraseInt(Key + name);
            foreach (string name in new[] { "Record", "RunInBackground", "Running", "Batch", "Uncapped", "Skip" }) SessionState.EraseBool(Key + name);
            foreach (string name in new[] { "Idle", "Hand", "Max" }) SessionState.EraseFloat(Key + name);
            Phase = "";
            EditorApplication.update -= Update;
            s_runner = null;
            Debug.Log($"[Bot show] done{(failure.Length > 0 ? $": {failure}" : "")}; {Path.GetFullPath(folder)}");
            if (batch) EditorApplication.Exit(written && failure.Length == 0 ? 0 : 1);
            else if (wasSeries) OpenGallery();
            else if (written) UnityEngine.Application.OpenURL(new Uri(Path.GetFullPath(Path.Combine(folder, "index.html"))).AbsoluteUri);
        }

        // the video's page: the template, the run's data, the subtitles; the last run is remembered for the menu
        private static void Write(BotShowRunner runner)
        {
            string folder = SessionState.GetString(Key + "Folder", Root);
            Directory.CreateDirectory(folder);
            var run = runner.Run;
            var (commit, branch) = BotMenu.GitHead();
            var facts = new List<(string, string)>
            {
                ("Бот", BotPopulation.Find(SessionState.GetString(Key + "Profile", ""))?.Title ?? "?"),
                ("Когда", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                ("Коммит", $"{commit ?? "?"} ({branch ?? "?"})"),
                ("Итог", runner.Failure ?? (run != null ? Outcome(run) : "—")),
                ("Дошёл до задания", runner.QuestLevel.ToString(CultureInfo.InvariantCulture)),
                ("Видео", runner.VideoFile != null ? $"{BotShowLog.Clock(runner.VideoSeconds)}, {runner.Frames} кадров" : "без записи"),
                ("Колония", run != null ? BotShowLog.Clock(run.EndMs / 1000.0) : "—"),
                ("Ходов через интерфейс", runner.Log.Events.Count(e => e.Kind == ShowEventKind.Command).ToString(CultureInfo.InvariantCulture)),
                ("Мимо интерфейса", runner.Log.Events.Count(e => e.Kind == ShowEventKind.Fallback).ToString(CultureInfo.InvariantCulture)),
                ("Находок", runner.Problems.ToString(CultureInfo.InvariantCulture))
            };
            var uncommitted = BotMenu.UncommittedRules();
            if (uncommitted is { Count: > 0 }) facts.Add(("Незакоммиченные правки правил", uncommitted.Count.ToString(CultureInfo.InvariantCulture)));
            var profile = BotPopulation.Find(SessionState.GetString(Key + "Profile", ""));
            if (profile != null) facts.Add(("Как играет", profile.Description));
            string title = $"{facts[0].Item2}: прохождение мышкой";
            runner.Log.WriteAll(folder, title, runner.VideoFile != null ? "video.mp4" : null, facts);
            File.WriteAllText(Path.Combine(folder, "summary.json"), BotReportData.Obj(
                ("folder", BotReportData.Str(Path.GetFileName(folder))),
                ("bot", BotReportData.Str(profile?.Id)),
                ("title", BotReportData.Str(facts[0].Item2)),
                ("series", BotReportData.Str(SessionState.GetString(Key + "Series", ""))),
                ("when", BotReportData.Str(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))),
                ("outcome", BotReportData.Str(runner.Failure ?? (run != null ? Outcome(run) : "—"))),
                ("completed", run?.Outcome == BotOutcome.Completed && runner.Failure == null ? "true" : "false"),
                ("quest", BotReportData.Num(runner.QuestLevel)),
                ("video", BotReportData.Str(runner.VideoFile != null ? "video.mp4" : null)),
                ("videoSeconds", BotReportData.Num(Math.Round(runner.VideoSeconds))),
                ("colonySeconds", BotReportData.Num(run != null ? Math.Round(run.EndMs / 1000.0) : 0)),
                ("gold", BotReportData.Num(run?.FinalGold ?? 0)),
                ("buildings", BotReportData.Num(run?.FinalBuildings ?? 0)),
                ("population", BotReportData.Num(run?.FinalPopulation ?? 0)),
                ("battles", BotReportData.Num(run?.Battles.Count ?? 0)),
                ("blunders", BotReportData.Num(run?.Blunders ?? 0)),
                ("moves", BotReportData.Num(runner.Log.Events.Count(e => e.Kind == ShowEventKind.Command))),
                ("past", BotReportData.Num(runner.Log.Events.Count(e => e.Kind == ShowEventKind.Fallback))),
                ("problems", BotReportData.Num(runner.Problems)),
                ("strategy", profile == null ? "{}" : BotReportData.Obj(BotPopulation.Traits
                    .Where(t => BotPopulation.IsStrategic(t.Key))
                    .Select(t => (t.Short, BotReportData.Str(t.Format(BotPopulation.Values(profile.Seed)[BotPopulation.IndexOf(t.Key)]))))
                    .ToArray())),
                ("description", BotReportData.Str(profile?.Description))) + "\n");
            if (File.Exists(PagePath)) File.Copy(PagePath, Path.Combine(folder, "index.html"), true);
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "last.txt"), folder);
        }

        private const string GalleryPath = "Assets/Game/Bots/Show/BotShowGallery.html";

        /// <summary>Every recording's summary in one page: the videos side by side, their strategies and results.</summary>
        public static void WriteGallery()
        {
            if (!Directory.Exists(Root)) return;
            var shows = Directory.GetDirectories(Root).Select(d => Path.Combine(d, "summary.json")).Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc).Select(File.ReadAllText).ToList();
            File.WriteAllText(Path.Combine(Root, "gallery-data.js"),
                "window.BOT_SHOWS = [\n" + string.Join(",\n", shows.Select(s => s.Trim())) + "\n];\n");
            if (File.Exists(GalleryPath)) File.Copy(GalleryPath, Path.Combine(Root, "index.html"), true);
        }

        private static string Outcome(BotRun run) => run.Outcome switch
        {
            BotOutcome.Completed => "дошёл до конца",
            BotOutcome.Stalled => "застрял: " + run.StopReason,
            BotOutcome.TimeLimit => "не успел: " + run.StopReason,
            _ => run.Outcome.ToString()
        };

        private static string SavePref(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return key + "\t-";
            // the prefs of this game are ints, floats or strings; keep whichever reads back
            string text = PlayerPrefs.GetString(key, "\u0001");
            if (text != "\u0001") return $"{key}\ts\t{text}";
            float number = PlayerPrefs.GetFloat(key, float.NaN);
            if (!float.IsNaN(number)) return $"{key}\tf\t{number.ToString(CultureInfo.InvariantCulture)}";
            return $"{key}\ti\t{PlayerPrefs.GetInt(key)}";
        }

        private static void RestorePref(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 2 || parts[0].Length == 0) return;
            if (parts[1] == "-") PlayerPrefs.DeleteKey(parts[0]);
            else if (parts[1] == "s") PlayerPrefs.SetString(parts[0], parts.Length > 2 ? parts[2] : "");
            else if (parts[1] == "f") PlayerPrefs.SetFloat(parts[0], float.Parse(parts[2], CultureInfo.InvariantCulture));
            else if (parts[1] == "i") PlayerPrefs.SetInt(parts[0], int.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
