using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Bots;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The drop-off funnel: what a campaign sends, how the player's consent and the service's start gate it, and the
    /// tech panel's notice and switch.
    /// </summary>
    public class TelemetryTests
    {
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
        }

        private GameSession Campaign() => new(_catalog, TestColony.LayoutFor(_catalog), campaign: true);

        [Test]
        public void Campaign_StartsAndCompletesEveryQuestInOrder_AndEveryEventMatchesTheSchema()
        {
            var session = Campaign();
            var events = new List<TelemetryEvent>();
            using var telemetry = new CampaignTelemetry(session, events.Add);

            var run = new CampaignBot(session, BotProfile.Typical).Run();

            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
            foreach (var e in events) AssertMatchesSchema(e);
            int chain = _catalog.Progression.Quests.Count;
            var started = Levels(events, CampaignTelemetry.QuestStarted);
            var completed = Levels(events, CampaignTelemetry.QuestCompleted);
            Assert.That(started.Take(chain), Is.EqualTo(Enumerable.Range(1, chain)), "one start per quest, in order");
            Assert.That(completed.Take(chain), Is.EqualTo(Enumerable.Range(1, chain)), "one completion per quest");
            Assert.That(events.Count(e => e.Name == CampaignTelemetry.CampaignCompleted), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Name == CampaignTelemetry.BattleFinished), Is.EqualTo(run.Battles.Count));
            Assert.That(events.Where(e => e.Name == CampaignTelemetry.BattleFinished).Select(e => (bool)e["won"]),
                Is.EqualTo(run.Battles.Select(b => b.Outcome == BattleOutcome.PlayerVictory)));

            var beats = events.Where(e => e.Name == CampaignTelemetry.Heartbeat).Select(e => (int)e["activeSeconds"])
                .ToList();
            Assert.That(beats, Is.Not.Empty);
            Assert.That(beats.Count, Is.LessThanOrEqualTo(session.ActiveTimeMs / CampaignTelemetry.HeartbeatMs));
            for (int i = 1; i < beats.Count; i++)
                Assert.That(beats[i] - beats[i - 1], Is.GreaterThanOrEqualTo(CampaignTelemetry.HeartbeatMs / 1000));
        }

        [Test]
        public void Claim_CompletesTheQuestOnce_ThenStartsTheNextWithItsOwnClock()
        {
            var session = Campaign();
            var events = new List<TelemetryEvent>();
            using var telemetry = new CampaignTelemetry(session, events.Add);
            Assert.That(events.Single().Name, Is.EqualTo(CampaignTelemetry.QuestStarted), "the first quest at once");
            Assert.That(events[0]["questLevel"], Is.EqualTo(1));
            Assert.That(events[0]["questId"], Is.EqualTo(_catalog.Progression.Quests[0].Id));

            session.Advance(5f);
            Assert.That(session.DebugCompleteQuest().Ok, Is.True);
            session.Advance(3f);
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);

            var names = events.Select(e => e.Name).ToList();
            Assert.That(names, Is.EqualTo(new[]
                { CampaignTelemetry.QuestStarted, CampaignTelemetry.QuestCompleted, CampaignTelemetry.QuestStarted }));
            Assert.That(events[1]["questSeconds"], Is.EqualTo(5), "the quest's own time, up to its goals being met");
            Assert.That(events[2]["questLevel"], Is.EqualTo(2));
            Assert.That(events[2]["activeSeconds"], Is.EqualTo(8));
        }

        [Test]
        public void Heartbeat_ComesOncePerMinuteOfColonyTime_WithTheQuestAtHand()
        {
            var session = Campaign();
            var events = new List<TelemetryEvent>();
            using var telemetry = new CampaignTelemetry(session, events.Add);

            for (int i = 0; i < 150; i++) session.Advance(1f);

            var beats = events.Where(e => e.Name == CampaignTelemetry.Heartbeat).ToList();
            Assert.That(beats.Select(e => e["activeSeconds"]), Is.EqualTo(new object[] { 60, 120 }));
            Assert.That(beats[0]["questLevel"], Is.EqualTo(1));
            Assert.That(beats[0]["gold"], Is.TypeOf<int>());
        }

        [Test]
        public void Sandbox_SendsNothing_AndADisposedFunnelStopsListening()
        {
            var sandbox = TestColony.NewSession(_catalog);
            var events = new List<TelemetryEvent>();
            using (new CampaignTelemetry(sandbox, events.Add))
                for (int i = 0; i < 70; i++) sandbox.Advance(1f);
            Assert.That(events, Is.Empty);

            var session = Campaign();
            new CampaignTelemetry(session, events.Add).Dispose();
            events.Clear();
            for (int i = 0; i < 70; i++) session.Advance(1f);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void Telemetry_QueuesUntilTheServiceStarts_ThenSendsAndFlushesOnTime()
        {
            var backend = new FakeBackend();
            var telemetry = new Telemetry(backend, new MemoryPrefs());

            Assert.That(backend.Consent, Is.True, "collecting is on until the player turns it off");
            Assert.That(backend.Starts, Is.EqualTo(1));
            telemetry.Record("questStarted", Fields(("questLevel", 1)));
            Assert.That(telemetry.Pending, Is.EqualTo(1));
            Assert.That(backend.Recorded, Is.Empty);

            backend.Start.SetResult(true);
            telemetry.Tick(0.1f);
            Assert.That(backend.Recorded, Is.EqualTo(new[] { "questStarted" }));
            Assert.That(telemetry.Pending, Is.Zero);

            telemetry.Tick(Telemetry.FlushSeconds - 1f);
            Assert.That(backend.Flushes, Is.Zero);
            telemetry.Tick(1f);
            Assert.That(backend.Flushes, Is.EqualTo(1));
            telemetry.Tick(Telemetry.FlushSeconds);
            Assert.That(backend.Flushes, Is.EqualTo(1), "nothing new, nothing to send");

            telemetry.Record("progressHeartbeat", Fields(("gold", 5)));
            telemetry.Flush();
            Assert.That(backend.Flushes, Is.EqualTo(2), "losing focus sends at once");
        }

        [Test]
        public void Telemetry_TurnedOff_DeniesConsentDropsEvents_AndStaysOffNextRun()
        {
            var prefs = new MemoryPrefs();
            var backend = new FakeBackend();
            var telemetry = new Telemetry(backend, prefs);
            backend.Start.SetResult(true);

            telemetry.SetCollecting(false);
            telemetry.Record("questStarted", Fields(("questLevel", 1)));
            telemetry.Flush();

            Assert.That(backend.Consent, Is.False);
            Assert.That(backend.Recorded, Is.Empty);
            Assert.That(backend.Flushes, Is.Zero);

            var next = new FakeBackend();
            var nextRun = new Telemetry(next, prefs);
            Assert.That(nextRun.Collecting, Is.False);
            Assert.That(next.Consent, Is.False);
            Assert.That(next.Starts, Is.Zero, "the service does not start for a player who said no");

            nextRun.SetCollecting(true);
            Assert.That(next.Consent, Is.True);
            Assert.That(next.Starts, Is.EqualTo(1));
        }

        [Test]
        public void Telemetry_WithoutAService_IsUnavailableAndIgnoresEvents()
        {
            var editor = new Telemetry(null, new MemoryPrefs());
            editor.Record("questStarted", Fields(("questLevel", 1)));
            editor.Tick(60f);
            editor.Flush();
            Assert.That(editor.Available, Is.False);

            var backend = new FakeBackend();
            var failed = new Telemetry(backend, new MemoryPrefs());
            failed.Record("questStarted", Fields(("questLevel", 1)));
            backend.Start.SetResult(false);
            Assert.That(failed.Available, Is.False, "no cloud project: nothing to switch");
            failed.Record("questCompleted", Fields(("questLevel", 1)));
            Assert.That(backend.Recorded, Is.Empty);
            Assert.That(failed.Pending, Is.Zero);
        }

        [Test]
        public void Telemetry_AFailingServiceCallNeverReachesTheGame()
        {
            var backend = new FakeBackend { Throw = true };
            var telemetry = new Telemetry(backend, new MemoryPrefs());
            backend.Start.SetResult(true);

            LogAssert.Expect(LogType.Warning, "[Support] Analytics call failed: service down");
            Assert.DoesNotThrow(() =>
            {
                telemetry.Record("questStarted", Fields(("questLevel", 1)));
                telemetry.Flush();
            });
        }

        [Test]
        public void TechPanel_SaysWhatIsSent_UntilThePlayerAnswers()
        {
            var prefs = new MemoryPrefs();
            var telemetry = new Telemetry(Started(), prefs);
            var panel = Panel(telemetry, out var root);

            Assert.That(panel.NoticeShown, Is.True, "first run: the line above the strip");
            panel.Show();
            Assert.That(panel.NoticeShown, Is.False, "the open panel holds the same switch");
            panel.Hide();
            Assert.That(panel.NoticeShown, Is.True);

            UiFeel.Press(root.Q<Button>("tech-notice-ok"));

            Assert.That(panel.NoticeShown, Is.False);
            Assert.That(telemetry.Collecting, Is.True);
            Assert.That(Panel(new Telemetry(Started(), prefs), out _).NoticeShown, Is.False, "answered once for good");
        }

        [Test]
        public void TechPanel_NoticeTurnsStatisticsOff_AndTheSwitchTurnsThemBackOn()
        {
            var backend = Started();
            var telemetry = new Telemetry(backend, new MemoryPrefs());
            var panel = Panel(telemetry, out var root);
            var stats = root.Q<Button>("tech-stats");

            UiFeel.Press(root.Q<Button>("tech-notice-off"));

            Assert.That(telemetry.Collecting, Is.False);
            Assert.That(backend.Consent, Is.False);
            Assert.That(panel.NoticeShown, Is.False);
            panel.Show();
            Assert.That(Ui.IsShown(stats), Is.True);
            Assert.That(stats.text, Is.EqualTo("Статистика: не отправляется"));
            Assert.That(stats.ClassListContains("is-on"), Is.False);

            UiFeel.Press(stats);

            Assert.That(telemetry.Collecting, Is.True);
            Assert.That(backend.Consent, Is.True);
            Assert.That(stats.text, Is.EqualTo("Статистика: отправляется"));
            Assert.That(stats.ClassListContains("is-on"), Is.True);
        }

        [Test]
        public void TechPanel_WithoutAService_HidesTheNoticeAndTheSwitch()
        {
            var panel = Panel(new Telemetry(null, new MemoryPrefs()), out var root);
            panel.Show();
            Assert.That(panel.NoticeShown, Is.False);
            Assert.That(Ui.IsShown(root.Q<Button>("tech-stats")), Is.False);

            var noStats = Panel(null, out var bare);
            Assert.That(noStats.NoticeShown, Is.False);
            Assert.That(Ui.IsShown(bare.Q<Button>("tech-stats")), Is.False);
        }

        private static void AssertMatchesSchema(TelemetryEvent e)
        {
            Assert.That(CampaignTelemetry.Schema.TryGetValue(e.Name, out var fields), Is.True, $"no schema for {e}");
            Assert.That(e.Fields.Select(f => f.Key), Is.EquivalentTo(fields.Keys), e.ToString());
            foreach (var field in e.Fields)
                Assert.That(field.Value, Is.TypeOf(fields[field.Key]), $"{e.Name}.{field.Key}");
        }

        private static List<int> Levels(IEnumerable<TelemetryEvent> events, string name) =>
            events.Where(e => e.Name == name).Select(e => (int)e["questLevel"]).ToList();

        private static IReadOnlyList<KeyValuePair<string, object>> Fields(params (string Key, object Value)[] fields) =>
            new TelemetryEvent("test", fields).Fields;

        private static FakeBackend Started()
        {
            var backend = new FakeBackend();
            backend.Start.SetResult(true);
            return backend;
        }

        private static TechInfoPanel Panel(Telemetry telemetry, out VisualElement root)
        {
            var ui = TestUi.Load();
            var hud = ui.Prefab.GetComponentInChildren<SupportHud>(true);
            Assert.That(hud, Is.Not.Null, "The UI prefab has no SupportHud");
            root = hud.TechInfoRoot(ui.RootOf);
            var build = new BuildInfo("1.0", 7, "abc1234", false, null, false);
            var meter = new FrameRateMeter();
            return new TechInfoPanel(root, new SupportContext(build, meter, () => SystemReport.Rows(build, meter),
                _ => Task.FromResult(ReportOutcome.Sent()), telemetry));
        }

        private sealed class FakeBackend : IAnalyticsBackend
        {
            public readonly TaskCompletionSource<bool> Start = new();
            public readonly List<string> Recorded = new();
            public bool? Consent;
            public int Starts;
            public int Flushes;
            public bool Throw;

            public string PrivacyUrl => "https://example.com/privacy";

            public Task<bool> StartAsync()
            {
                Starts++;
                return Start.Task;
            }

            public void SetConsent(bool granted) => Consent = granted;

            public void Record(string name, IReadOnlyList<KeyValuePair<string, object>> fields)
            {
                if (Throw) throw new InvalidOperationException("service down");
                Recorded.Add(name);
            }

            public void Flush()
            {
                if (Throw) throw new InvalidOperationException("service down");
                Flushes++;
            }
        }

        private sealed class MemoryPrefs : IPrefs
        {
            private readonly Dictionary<string, int> _values = new();
            public int Get(string key, int fallback) => _values.TryGetValue(key, out int value) ? value : fallback;
            public void Set(string key, int value) => _values[key] = value;
        }
    }
}
