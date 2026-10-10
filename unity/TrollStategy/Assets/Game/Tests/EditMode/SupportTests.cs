using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>The support corner: build stamp, frame meter, bug reports and the tech panel from the UI prefab.</summary>
    public class SupportTests
    {
        private readonly List<ScriptableObject> _assets = new();
        private string _folder;

        [SetUp]
        public void SetUp() => _folder = Path.Combine(Path.GetTempPath(), "troll-support-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void BuildStamp_RoundTripsThroughItsResourceText()
        {
            var built = new DateTime(2026, 9, 30, 11, 30, 0, DateTimeKind.Utc);
            var stamp = new BuildInfo("1.0", 412, "a1b2c3d", true, built, false).Serialize();

            var info = BuildInfo.Parse(stamp, "9.9");

            Assert.That(info.Label, Is.EqualTo("v1.0.412 · a1b2c3d*"), "A star marks uncommitted changes");
            Assert.That(info.BuiltUtc, Is.EqualTo(built));
            Assert.That(info.IsEditor, Is.False);
        }

        [Test]
        public void BuildStamp_MissingKeepsTheProductVersionAndSaysTheCommitIsUnknown()
        {
            Assert.That(BuildInfo.Parse(null, "1.0").Label, Is.EqualTo("v1.0 · ?"));
            Assert.That(new BuildInfo("1.0", 0, null, false, null, true).Label, Is.EqualTo("v1.0 · редактор"));
        }

        [Test]
        public void BuildStamp_CarriesTheEdition_AndAStampWithoutOneIsTheAlpha()
        {
            var demo = new BuildInfo("1.0", 412, "a1b2c3d", false, null, false, BuildEdition.SteamDemo).Serialize();

            var info = BuildInfo.Parse(demo, "9.9");

            Assert.That(info.Edition, Is.EqualTo(BuildEdition.SteamDemo));
            Assert.That(info.VersionNumber, Is.EqualTo("1.0.412"));
            Assert.That(BuildInfo.Parse("version=1.0\nbuild=3\n", "1.0").Edition, Is.EqualTo(BuildEdition.Alpha));
        }

        [Test]
        public void BuildStamp_TheTestersBuildAndTheEditorPlayTheWholeGame()
        {
            var full = new BuildInfo("1.0", 412, "a1b2c3d", false, null, false, BuildEdition.Full).Serialize();

            Assert.That(full, Does.Contain("edition=full"));
            Assert.That(BuildInfo.Parse(full, "1.0").Edition, Is.EqualTo(BuildEdition.Full));
            Assert.That(BuildInfo.Current.Edition, Is.EqualTo(BuildEdition.Full), "The editor plays the whole game");
        }

        [Test]
        public void FrameMeter_ReadsHalfSecondWindowsAndRemembersTheSlowestFrameForFiveSeconds()
        {
            var meter = new FrameRateMeter();
            Assert.That(meter.Fps, Is.Zero);

            Feed(meter, 1f / 60f, 31);
            Assert.That(meter.Fps, Is.EqualTo(60f).Within(1f));

            meter.Add(.1f);
            Feed(meter, 1f / 60f, 31);
            Assert.That(meter.WorstFrameMs, Is.EqualTo(100f).Within(.5f));

            Feed(meter, 1f / 60f, 31 * (FrameRateMeter.WorstWindows + 1));
            Assert.That(meter.WorstFrameMs, Is.EqualTo(1000f / 60f).Within(.5f), "The spike ages out");

            meter.Add(0f);
            meter.Add(float.NaN);
            Assert.That(meter.Fps, Is.EqualTo(60f).Within(1f), "Empty and broken frame times are ignored");
        }

        [Test]
        public void ReadTail_KeepsTheEndOfALongLogAndMarksTheCut()
        {
            Directory.CreateDirectory(_folder);
            string path = Path.Combine(_folder, "Player.log");
            File.WriteAllText(path, new string('a', 60) + "LAST-LINE");

            var tail = Encoding.UTF8.GetString(BugReport.ReadTail(path, 12));

            Assert.That(tail, Does.EndWith("aaaLAST-LINE"));
            Assert.That(tail, Does.StartWith("[… начало лога обрезано: 57 байт …]"));
            Assert.That(BugReport.ReadTail(Path.Combine(_folder, "missing.log"), 12), Is.Null);
            Assert.That(Encoding.UTF8.GetString(BugReport.ReadTail(path, 1000)), Does.StartWith("aaa"),
                "A short log is kept whole, unmarked");
        }

        [Test]
        public void Report_ZipsTechInfoGameStateLogsAndScreenshot()
        {
            Directory.CreateDirectory(_folder);
            string log = Path.Combine(_folder, "Player.log");
            File.WriteAllText(log, "boot\nNullReferenceException\n");
            var rows = new List<KeyValuePair<string, string>> { new("Версия", "v1.0.7 · abc1234") };
            var build = new BuildInfo("1.0", 7, "abc1234", false, null, false);

            var report = BugReport.Create(build, rows, "Золото: 500", new[] { log, Path.Combine(_folder, "Player-prev.log") },
                new byte[] { 1, 2, 3 }, new DateTime(2026, 9, 30, 14, 5, 6));

            Assert.That(report.Summary, Does.Contain("v1.0.7 · abc1234"));
            Assert.That(report.Version, Is.EqualTo("v1.0.7"));
            Assert.That(report.ArchiveName, Is.EqualTo("report-20260930-140506.zip"));
            var entries = Unzip(report.Archive());
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { "info.txt", "game.txt", "Player.log", "screenshot.jpg" }),
                "The previous log is left out when there is none");
            Assert.That(Encoding.UTF8.GetString(entries["info.txt"]), Does.Contain("Версия: v1.0.7 · abc1234"));
            Assert.That(Encoding.UTF8.GetString(entries["game.txt"]), Is.EqualTo("Золото: 500"));
            Assert.That(Encoding.UTF8.GetString(entries["Player.log"]), Does.Contain("NullReferenceException"));
        }

        [Test]
        public void Reporter_SavesTheZipAndShowsTheFolderWhenUploadFails()
        {
            string revealed = null;
            var reporter = new BugReporter(new FakeUploader(false), _folder, folder => revealed = folder);

            var outcome = reporter.SendAsync(SmallReport()).Result;

            Assert.That(outcome.Result, Is.EqualTo(ReportResult.Saved));
            Assert.That(File.Exists(outcome.Path), Is.True);
            Assert.That(Unzip(File.ReadAllBytes(outcome.Path)).Keys, Does.Contain("info.txt"));
            Assert.That(revealed, Is.EqualTo(_folder));
        }

        [Test]
        public void Reporter_SavesTheZipWhenTheUploaderThrows_AndNothingWhenTheServiceTookIt()
        {
            var thrown = new BugReporter(new FakeUploader(null), _folder).SendAsync(SmallReport()).Result;
            Assert.That(thrown.Result, Is.EqualTo(ReportResult.Saved));

            Directory.Delete(_folder, true);
            var sent = new BugReporter(new FakeUploader(true), _folder).SendAsync(SmallReport()).Result;
            Assert.That(sent.Result, Is.EqualTo(ReportResult.Sent));
            Assert.That(Directory.Exists(_folder), Is.False, "A sent report leaves no file behind");
        }

        [Test]
        public void Report_CarriesTheRecentLogAndTheBrowserPart()
        {
            var report = BugReport.Create(new BuildInfo("1.0", 1, "abc", false, null, false), null, null, null, null,
                new DateTime(2026, 10, 1, 12, 0, 0), "boot\nshader failed", "GPU: Adreno (TM) 505");

            var entries = Unzip(report.Archive());
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { "info.txt", "log.txt", "browser.txt" }));
            Assert.That(Encoding.UTF8.GetString(entries["log.txt"]), Does.Contain("shader failed"));
            Assert.That(Encoding.UTF8.GetString(entries["browser.txt"]), Does.Contain("Adreno"));
        }

        [Test]
        public void Reporter_HandsTheZipToTheDownloadInsteadOfTheDisk_WhenUploadFails()
        {
            string name = null;
            byte[] data = null;
            var reporter = new BugReporter(new FakeUploader(false), _folder, null, (n, d) =>
            {
                name = n;
                data = d;
                return true;
            });

            var outcome = reporter.SendAsync(SmallReport()).Result;

            Assert.That(outcome.Result, Is.EqualTo(ReportResult.Saved));
            Assert.That(name, Does.EndWith(".zip"));
            Assert.That(Unzip(data).Keys, Does.Contain("info.txt"));
            Assert.That(Directory.Exists(_folder), Is.False, "A browser cannot reach that folder, so nothing goes there");

            var refused = new BugReporter(new FakeUploader(false), _folder, null, (_, _) => false)
                .SendAsync(SmallReport()).Result;
            Assert.That(refused.Result, Is.EqualTo(ReportResult.Failed));
        }

        [Test]
        public void LogRecorder_KeepsTheLastLinesInOrder_CountsErrors_AndKeepsTheirStacks()
        {
            var recorder = new LogRecorder(3);
            recorder.Add("one", "ignored stack", LogType.Log);
            recorder.Add("two", null, LogType.Warning);
            recorder.Add("three", "at Thing.Do()", LogType.Exception);
            recorder.Add("four", null, LogType.Error);

            string text = recorder.Text();
            Assert.That(text, Does.Contain("раньше было ещё 1 строк"));
            Assert.That(text, Does.Not.Contain("one"));
            Assert.That(text.IndexOf("two", StringComparison.Ordinal),
                Is.LessThan(text.IndexOf("four", StringComparison.Ordinal)));
            Assert.That(text, Does.Contain("at Thing.Do()"), "An exception keeps its stack");
            Assert.That(recorder.Errors, Is.EqualTo(2));
        }

        [Test]
        public void TechPanel_ShowsTheCheatsButtonOnlyWhenTheBuildHasCheats_AndClosesBeforeOpeningThem()
        {
            var panel = Panel(new FrameRateMeter(), _ => Task.FromResult(ReportOutcome.Sent()), out var root);
            var cheats = root.Q<Button>("tech-cheats");
            Assert.That(Ui.IsShown(cheats), Is.False, "Release players have no cheat menu, so no button");

            int opened = 0;
            panel.SetCheats(() => opened++);
            panel.Show();
            Assert.That(Ui.IsShown(cheats), Is.True);
            UiFeel.Press(cheats);

            Assert.That(opened, Is.EqualTo(1));
            Assert.That(panel.IsOpen, Is.False, "The tech panel steps aside for the cheat menu");
        }

        [Test]
        public void TechPanel_ShowsVersionAndFps_AndOpensWithTheMachineRows()
        {
            var meter = new FrameRateMeter();
            Feed(meter, 1f / 25f, 13);
            var panel = Panel(meter, _ => Task.FromResult(ReportOutcome.Sent()), out var root);

            Assert.That(root.Q<Label>("tech-version").text, Is.EqualTo("v1.0.7 · abc1234"));
            Assert.That(root.Q<Label>("tech-fps").text, Is.EqualTo("25 FPS"));
            Assert.That(root.Q<Label>("tech-fps").ClassListContains("t-bad"), Is.True, "25 FPS reads as poor");
            Assert.That(panel.IsOpen, Is.False);

            UiFeel.Press(root.Q<Button>("tech-strip"));

            Assert.That(panel.IsOpen, Is.True);
            var keys = root.Q("tech-rows").Query<Label>(className: "kv__key").ToList().Select(label => label.text);
            Assert.That(keys, Does.Contain("Версия").And.Contain("Видеокарта"));
            UiFeel.Press(root.Q<Button>("tech-close"));
            Assert.That(panel.IsOpen, Is.False);
        }

        [Test]
        public void TechPanel_SendRefusesASecondPressWhileSending_AndSaysWhereTheReportWent()
        {
            var pending = new TaskCompletionSource<ReportOutcome>();
            int sends = 0;
            var panel = Panel(new FrameRateMeter(), _ =>
            {
                sends++;
                return pending.Task;
            }, out var root);
            var send = root.Q<Button>("tech-send");
            panel.Show();

            UiFeel.Press(send);
            UiFeel.Press(send);

            Assert.That(sends, Is.EqualTo(1));
            Assert.That(panel.IsSending, Is.True);
            Assert.That(UiFeel.IsAvailable(send), Is.False);
            Assert.That(panel.Status, Is.EqualTo("Собираю отчёт…"));

            var saved = Panel(new FrameRateMeter(), _ => Task.FromResult(ReportOutcome.Saved(@"C:\reports\r.zip")), out var savedRoot);
            UiFeel.Press(savedRoot.Q<Button>("tech-send"));
            Assert.That(saved.IsSending, Is.False);
            Assert.That(saved.Status, Does.Contain("сохранён").And.Contain(@"C:\reports\r.zip"));
            Assert.That(UiFeel.IsAvailable(savedRoot.Q<Button>("tech-send")), Is.True);
        }

        [Test]
        public void SessionDigest_DescribesModeGoldAndColony()
        {
            var session = TestColony.NewSession(Catalog());

            string digest = SessionDigest.Describe(session);

            Assert.That(digest, Does.Contain("Режим: песочница"));
            Assert.That(digest, Does.Contain("Золото: 1000"));
            Assert.That(digest, Does.Contain("Warehouse ур.1 ×1").And.Contain("Market ур.1 ×1"));
            Assert.That(digest, Does.Contain("Жители (0): нет"));
        }

        private static void Feed(FrameRateMeter meter, float delta, int frames)
        {
            for (int i = 0; i < frames; i++) meter.Add(delta);
        }

        private static TechInfoPanel Panel(FrameRateMeter meter, Func<Action<float>, Task<ReportOutcome>> send,
            out VisualElement root)
        {
            var ui = TestUi.Load();
            var hud = ui.Prefab.GetComponentInChildren<SupportHud>(true);
            Assert.That(hud, Is.Not.Null, "The UI prefab has no SupportHud");
            root = hud.TechInfoRoot(ui.RootOf);
            Assert.That(root, Is.Not.Null, "The SupportHud has no tech info document");
            var build = new BuildInfo("1.0", 7, "abc1234", false, null, false);
            return new TechInfoPanel(root, new SupportContext(build, meter, () => SystemReport.Rows(build, meter), send));
        }

        private static BugReport SmallReport() =>
            BugReport.Create(new BuildInfo("1.0", 1, "abc", false, null, false),
                new List<KeyValuePair<string, string>> { new("Версия", "v1.0.1") }, null, null, null, DateTime.Now);

        private static Dictionary<string, byte[]> Unzip(byte[] archive)
        {
            var entries = new Dictionary<string, byte[]>();
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                using var stream = entry.Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                entries.Add(entry.FullName, copy.ToArray());
            }
            return entries;
        }

        private GameContentCatalog Catalog()
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1000, 20, .25f, .1f, .5f);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { goblin }, new[] { warehouse, market }, Array.Empty<BattleMissionDefinition>());
            return catalog;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private sealed class FakeUploader : IReportUploader
        {
            private readonly bool? _answer;

            // null throws, as a service that is not set up does
            public FakeUploader(bool? answer) => _answer = answer;

            public Task<bool> UploadAsync(BugReport report, Action<float> progress)
            {
                if (_answer == null) throw new InvalidOperationException("no service");
                progress?.Invoke(1f);
                return Task.FromResult(_answer.Value);
            }
        }
    }
}
