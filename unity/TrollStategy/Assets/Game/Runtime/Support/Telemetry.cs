using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>Where analytics events go: Unity Analytics in a player, a stand-in in tests.</summary>
    public interface IAnalyticsBackend
    {
        /// <summary>Starts the service; false when there is none to start (no cloud project).</summary>
        Task<bool> StartAsync();
        /// <summary>Tells the service whether the player lets it collect.</summary>
        void SetConsent(bool granted);
        void Record(string name, IReadOnlyList<KeyValuePair<string, object>> fields);
        /// <summary>Uploads what is recorded so far.</summary>
        void Flush();
        /// <summary>The service's privacy policy, for the player.</summary>
        string PrivacyUrl { get; }
    }

    /// <summary>A few numbers that outlive the session: PlayerPrefs in the game, a dictionary in tests.</summary>
    public interface IPrefs
    {
        int Get(string key, int fallback);
        void Set(string key, int value);
    }

    public sealed class PlayerPrefsStore : IPrefs
    {
        public int Get(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);

        public void Set(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Anonymous play statistics: the game hands it events, it sends them while the player lets it. Nothing is
    /// sent until the player answers the question in the intro; the answer is kept between runs and the tech
    /// panel (F8) changes it. Events from before the service is up
    /// wait in a short queue. Recorded events go out within <see cref="FlushSeconds"/>, so closing the game (a
    /// browser tab gives no warning) loses little. In the editor there is no service and nothing is sent.
    /// </summary>
    public sealed class Telemetry
    {
        public const string CollectKey = "telemetry.collect";
        public const string NoticeKey = "telemetry.notice";
        public const int MaxPending = 100;
        public const float FlushSeconds = 10f;

        private enum Phase { Idle, Starting, Running, Failed }

        private static Telemetry s_game;

        private readonly IAnalyticsBackend _backend;
        private readonly IPrefs _prefs;
        private readonly List<(string Name, IReadOnlyList<KeyValuePair<string, object>> Fields)> _pending = new();
        private Phase _phase;
        private Task<bool> _start;
        private bool _unsent;
        private float _sinceFlush;
        private bool _warned;

        public Telemetry(IAnalyticsBackend backend, IPrefs prefs)
        {
            _backend = backend;
            _prefs = prefs ?? throw new ArgumentNullException(nameof(prefs));
            if (_backend == null) return;
            Call(() => _backend.SetConsent(Collecting));
            if (Collecting) Begin();
        }

        /// <summary>The game's statistics: Unity Analytics in a player, nothing in the editor.</summary>
        public static Telemetry Game => s_game ??= new Telemetry(
            UnityEngine.Application.isEditor ? null : new UnityAnalyticsBackend(), new PlayerPrefsStore());

        /// <summary>False in the editor, or once the service failed to start: there is nothing to switch.</summary>
        public bool Available
        {
            get
            {
                Settle();
                return _backend != null && _phase != Phase.Failed;
            }
        }

        /// <summary>The player's choice; off until they answer yes.</summary>
        public bool Collecting => Answered && _prefs.Get(CollectKey, 1) != 0;

        /// <summary>The player has answered whether statistics may be sent.</summary>
        public bool Answered => _prefs.Get(NoticeKey, 0) != 0;

        public string PrivacyUrl => _backend?.PrivacyUrl;

        /// <summary>Events waiting for the service to start.</summary>
        public int Pending => _pending.Count;

        /// <summary>The player's answer, from the intro or the tech panel: send statistics or not.</summary>
        public void Answer(bool collect)
        {
            bool was = Collecting;
            _prefs.Set(CollectKey, collect ? 1 : 0);
            _prefs.Set(NoticeKey, 1);
            if (_backend == null || was == Collecting) return;
            Call(() => _backend.SetConsent(collect));
            if (!collect)
            {
                _pending.Clear();
                _unsent = false;
            }
            else if (_phase == Phase.Idle) Begin();
        }

        public void Record(string name, IReadOnlyList<KeyValuePair<string, object>> fields)
        {
            if (_backend == null || !Collecting) return;
            Settle();
            switch (_phase)
            {
                case Phase.Running:
                    Call(() => _backend.Record(name, fields));
                    _unsent = true;
                    break;
                case Phase.Starting:
                    if (_pending.Count < MaxPending) _pending.Add((name, fields));
                    break;
            }
        }

        /// <summary>Called every frame: sends what was recorded once <see cref="FlushSeconds"/> have passed.</summary>
        public void Tick(float deltaSeconds)
        {
            if (_backend == null) return;
            Settle();
            if (!_unsent) return;
            _sinceFlush += deltaSeconds;
            if (_sinceFlush >= FlushSeconds) Flush();
        }

        /// <summary>Sends what was recorded now: the game is losing focus or closing.</summary>
        public void Flush()
        {
            if (_backend == null || _phase != Phase.Running || !Collecting) return;
            _unsent = false;
            _sinceFlush = 0f;
            Call(_backend.Flush);
        }

        private void Begin()
        {
            _phase = Phase.Starting;
            try
            {
                _start = _backend.StartAsync();
            }
            catch (Exception exception)
            {
                _start = Task.FromException<bool>(exception);
            }
            Settle();
        }

        // The start is polled, not awaited: the game reads the outcome on its own frame and tests stay synchronous.
        private void Settle()
        {
            if (_phase != Phase.Starting || _start == null || !_start.IsCompleted) return;
            bool started = _start.Status == TaskStatus.RanToCompletion && _start.Result;
            if (_start.IsFaulted)
                Debug.LogWarning($"[Support] Analytics did not start: {_start.Exception?.GetBaseException().Message}");
            _start = null;
            if (!started)
            {
                _phase = Phase.Failed;
                _pending.Clear();
                return;
            }
            _phase = Phase.Running;
            if (!Collecting) return;
            foreach (var (name, fields) in _pending) Call(() => _backend.Record(name, fields));
            _unsent = _pending.Count > 0;
            _pending.Clear();
        }

        // Statistics never break the game: a failing call is logged once and the game goes on.
        private void Call(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning($"[Support] Analytics call failed: {exception.Message}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => s_game = null;
    }
}
