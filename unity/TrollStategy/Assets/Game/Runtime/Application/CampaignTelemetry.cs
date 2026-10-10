using System;
using System.Collections.Generic;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>One analytics event: a name and a few int, string or bool fields.</summary>
    public sealed class TelemetryEvent
    {
        private readonly KeyValuePair<string, object>[] _fields;

        public TelemetryEvent(string name, params (string Key, object Value)[] fields)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("An event needs a name", nameof(name));
            Name = name;
            _fields = new KeyValuePair<string, object>[fields?.Length ?? 0];
            for (int i = 0; i < _fields.Length; i++) _fields[i] = new(fields[i].Key, fields[i].Value);
        }

        public string Name { get; }
        public IReadOnlyList<KeyValuePair<string, object>> Fields => _fields;

        /// <summary>The field's value; null when the event has no such field.</summary>
        public object this[string key]
        {
            get
            {
                foreach (var field in _fields)
                    if (field.Key == key) return field.Value;
                return null;
            }
        }

        public override string ToString()
        {
            var parts = new string[_fields.Length];
            for (int i = 0; i < _fields.Length; i++) parts[i] = $"{_fields[i].Key}={_fields[i].Value}";
            return $"{Name}({string.Join(", ", parts)})";
        }
    }

    /// <summary>
    /// The campaign as the drop-off funnel sees it: each quest started and completed, each battle, the end of the
    /// authored chain, and a heartbeat every minute of colony time with the quest at hand. A player who leaves is
    /// on the quest of their last event. Reads snapshots and resolved commands only; a sandbox game sends nothing.
    /// Every event and field is listed in <see cref="Schema"/>, which the analytics dashboard must match
    /// (docs/analytics.md): the service drops events it has no schema for.
    /// </summary>
    public sealed class CampaignTelemetry : IDisposable
    {
        public const string QuestStarted = "questStarted";
        public const string QuestCompleted = "questCompleted";
        public const string CampaignCompleted = "campaignCompleted";
        public const string BattleFinished = "battleFinished";
        public const string Heartbeat = "progressHeartbeat";
        public const string GameLoaded = "gameLoaded";

        /// <summary>Colony time between heartbeats.</summary>
        public const int HeartbeatMs = 60000;

        /// <summary>Each event's fields and their types, as the dashboard declares them.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, Type>> Schema =
            new Dictionary<string, IReadOnlyDictionary<string, Type>>
            {
                [QuestStarted] = Fields(("questId", typeof(string)), ("questLevel", typeof(int)),
                    ("activeSeconds", typeof(int))),
                [QuestCompleted] = Fields(("questId", typeof(string)), ("questLevel", typeof(int)),
                    ("questSeconds", typeof(int)), ("activeSeconds", typeof(int))),
                [CampaignCompleted] = Fields(("questLevel", typeof(int)), ("activeSeconds", typeof(int))),
                [BattleFinished] = Fields(("missionId", typeof(string)), ("won", typeof(bool)),
                    ("fallen", typeof(int)), ("questLevel", typeof(int)), ("activeSeconds", typeof(int))),
                [Heartbeat] = Fields(("questId", typeof(string)), ("questLevel", typeof(int)),
                    ("activeSeconds", typeof(int)), ("gold", typeof(int)), ("buildingCount", typeof(int)),
                    ("unitCount", typeof(int))),
                [GameLoaded] = Fields(("slot", typeof(string)), ("questId", typeof(string)),
                    ("questLevel", typeof(int)), ("activeSeconds", typeof(int)), ("edition", typeof(string)),
                    ("carriedFrom", typeof(string)), ("formatVersion", typeof(int)))
            };

        private readonly GameSession _session;
        private readonly Action<TelemetryEvent> _send;
        private readonly int _authoredQuests;
        private int _level;
        private string _questId;
        private int _questStartMs;
        private int _completedLevel;
        private bool _campaignDone;
        private int _nextHeartbeatMs = HeartbeatMs;
        private bool _disposed;

        /// <summary>
        /// Starts listening, and in a campaign at once sends the quest the player starts on. A
        /// <paramref name="resumed"/> game (loaded from a save) sends nothing for the quest at hand: its start, and
        /// its completion if its goals are met, went out before the save; its time counts from the quest's start.
        /// </summary>
        public CampaignTelemetry(GameSession session, Action<TelemetryEvent> send, bool resumed = false)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _authoredQuests = session.Catalog.Progression != null ? session.Catalog.Progression.Quests.Count : 0;
            _session.OnSnapshotChanged += Observe;
            _session.OnCommandResolved += ObserveCommand;
            if (resumed) Resume(session.CurrentSnapshot);
            else Observe(session.CurrentSnapshot);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session.OnSnapshotChanged -= Observe;
            _session.OnCommandResolved -= ObserveCommand;
        }

        private int ActiveSeconds => _session.ActiveTimeMs / 1000;

        private void Observe(GameSnapshot snapshot)
        {
            var progress = snapshot.Progress;
            if (progress == null || !progress.Enabled) return;
            var quest = progress.Quest;

            // a claim ends a quest and begins the next one in the same commit
            if (quest == null || quest.Level != _level)
            {
                if (_level > 0 && _completedLevel != _level) Complete();
                if (quest != null) Begin(quest);
                if (!_campaignDone && (quest == null || quest.Level > _authoredQuests))
                {
                    _campaignDone = true;
                    _send(new TelemetryEvent(CampaignCompleted,
                        ("questLevel", quest != null ? quest.Level : _level), ("activeSeconds", ActiveSeconds)));
                }
                if (quest == null) _level = 0;
            }
            if (quest != null && quest.IsComplete && _completedLevel != _level) Complete();

            int now = _session.ActiveTimeMs;
            if (now >= _nextHeartbeatMs)
            {
                _nextHeartbeatMs = (now / HeartbeatMs + 1) * HeartbeatMs;
                if (_level > 0)
                    _send(new TelemetryEvent(Heartbeat, ("questId", _questId), ("questLevel", _level),
                        ("activeSeconds", now / 1000), ("gold", snapshot.Gold),
                        ("buildingCount", snapshot.Buildings.Count), ("unitCount", snapshot.Units.Count)));
            }
        }

        /// <summary>
        /// A game opened from a save (call it after a <c>resumed</c> start): which slot, the quest it opens on, the
        /// colony time played, this build's edition and the one the save was carried forward from ("" when none),
        /// and the save's format version. A carried game whose short chain had ended opens on a new quest, which
        /// starts here. A sandbox game sends nothing, as always.
        /// </summary>
        public void Loaded(SavedGame saved, string edition)
        {
            if (_disposed || saved == null || !_session.IsCampaign) return;
            var progress = _session.CurrentSnapshot.Progress;
            var quest = progress.Quest;
            _send(new TelemetryEvent(GameLoaded,
                ("slot", SaveGames.IsAutosave(saved.SlotId) ? "autosave" : "manual"),
                ("questId", quest?.Id ?? string.Empty), ("questLevel", progress.Level),
                ("activeSeconds", ActiveSeconds), ("edition", edition ?? string.Empty),
                ("carriedFrom", saved.CarriedFrom ?? string.Empty),
                ("formatVersion", saved.Header?.Version ?? SaveCodec.Version)));
            if (!saved.ChainExtended || quest == null) return;
            _campaignDone = false;
            Begin(quest);
        }

        // where the saved game stood in the funnel, without sending it again; the heartbeat goes on from the next minute
        private void Resume(GameSnapshot snapshot)
        {
            int now = _session.ActiveTimeMs;
            _nextHeartbeatMs = (now / HeartbeatMs + 1) * HeartbeatMs;
            var progress = snapshot.Progress;
            if (progress == null || !progress.Enabled) return;
            var quest = progress.Quest;
            if (quest == null)
            {
                _campaignDone = true;
                return;
            }
            _level = quest.Level;
            _questId = quest.Id;
            _questStartMs = _session.QuestStartedMs;
            if (quest.IsComplete) _completedLevel = _level;
            _campaignDone = quest.Level > _authoredQuests;
        }

        private void Begin(QuestSnapshot quest)
        {
            _level = quest.Level;
            _questId = quest.Id;
            _questStartMs = _session.ActiveTimeMs;
            _send(new TelemetryEvent(QuestStarted, ("questId", _questId), ("questLevel", _level),
                ("activeSeconds", ActiveSeconds)));
        }

        private void Complete()
        {
            _completedLevel = _level;
            _send(new TelemetryEvent(QuestCompleted, ("questId", _questId), ("questLevel", _level),
                ("questSeconds", (_session.ActiveTimeMs - _questStartMs) / 1000), ("activeSeconds", ActiveSeconds)));
        }

        private void ObserveCommand(IGameCommand command, CommandResult result)
        {
            if (command is not StartBattleCommand || !result.Ok || !_session.IsCampaign) return;
            var battle = _session.ActiveBattle;
            if (battle == null) return;
            _send(new TelemetryEvent(BattleFinished, ("missionId", battle.MissionId),
                ("won", battle.Report.Outcome == BattleOutcome.PlayerVictory),
                ("fallen", battle.FallenUnitIds.Count), ("questLevel", _level), ("activeSeconds", ActiveSeconds)));
        }

        private static IReadOnlyDictionary<string, Type> Fields(params (string Name, Type Type)[] fields)
        {
            var map = new Dictionary<string, Type>(fields.Length);
            foreach (var (name, type) in fields) map.Add(name, type);
            return map;
        }
    }
}
