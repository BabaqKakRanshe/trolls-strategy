using System;
using System.Collections.Generic;

namespace TrollStrategy.Presentation.Audio
{
    /// <summary>What the player is doing, as far as music and ambience are concerned.</summary>
    public enum SoundScene { Colony, Battle, BattleResult }

    /// <summary>One piece of music: its resource path and the integrated loudness it was mastered at (LUFS).</summary>
    public sealed class MusicTrack
    {
        public MusicTrack(string resource, float loudness)
        {
            Resource = resource;
            Loudness = loudness;
        }

        public string Resource { get; }
        public float Loudness { get; }
    }

    /// <summary>
    /// Decides which music plays. The theme opens the game once; after it the colony tracks follow in
    /// shuffled order with a quiet gap after each, so the ambience carries the island between them. A
    /// battle loops its own track (the next one each battle) and falls silent under the result; back in
    /// the colony a short breath comes before the next track.
    /// </summary>
    public sealed class MusicSequencer
    {
        /// <summary>Silence between leaving a battle and the next colony track.</summary>
        public const float ReturnGap = 3f;

        private readonly MusicTrack _theme;
        private readonly MusicTrack[] _colony;
        private readonly MusicTrack[] _battle;
        private readonly Random _random;
        private readonly float _gapMin;
        private readonly float _gapMax;
        private readonly List<int> _bag = new();
        private int _lastColony = -1;
        private int _lastBattle = -1;
        private bool _themePlayed;
        private float _gap;

        public MusicSequencer(MusicTrack theme, IReadOnlyList<MusicTrack> colony, IReadOnlyList<MusicTrack> battle,
            int seed, float gapMin, float gapMax)
        {
            if (colony == null || colony.Count == 0) throw new ArgumentException("The colony needs at least one track.", nameof(colony));
            if (battle == null || battle.Count == 0) throw new ArgumentException("Battles need at least one track.", nameof(battle));
            _theme = theme;
            _colony = new MusicTrack[colony.Count];
            for (int i = 0; i < colony.Count; i++) _colony[i] = colony[i];
            _battle = new MusicTrack[battle.Count];
            for (int i = 0; i < battle.Count; i++) _battle[i] = battle[i];
            _random = new Random(seed);
            _gapMin = Math.Max(0f, gapMin);
            _gapMax = Math.Max(_gapMin, gapMax);
        }

        /// <summary>The scene last entered; null before the first.</summary>
        public SoundScene? Scene { get; private set; }
        /// <summary>The track that should be playing, or null for silence.</summary>
        public MusicTrack Current { get; private set; }
        /// <summary>Whether <see cref="Current"/> repeats until the scene changes.</summary>
        public bool Loops { get; private set; }
        /// <summary>Changes whenever a track starts, so the player notices a restart of the same one.</summary>
        public int Version { get; private set; }

        public void Enter(SoundScene scene)
        {
            if (Scene == scene) return;
            var from = Scene;
            Scene = scene;
            switch (scene)
            {
                case SoundScene.Colony:
                    if (!_themePlayed && _theme != null)
                    {
                        _themePlayed = true;
                        Start(_theme, false);
                    }
                    else Wait(from == null ? 0f : ReturnGap);
                    break;
                case SoundScene.Battle:
                    _lastBattle = (_lastBattle + 1) % _battle.Length;
                    Start(_battle[_lastBattle], true);
                    break;
                default:
                    Wait(0f);
                    break;
            }
        }

        /// <summary>The player reports that <see cref="Current"/> played to its end.</summary>
        public void Finished()
        {
            if (Current == null || Loops) return;
            Wait(_gapMin + (float)_random.NextDouble() * (_gapMax - _gapMin));
        }

        /// <summary>Counts down a gap in the colony and starts the next track when it runs out.</summary>
        public void Advance(float seconds)
        {
            if (Scene != SoundScene.Colony || Current != null) return;
            _gap -= seconds;
            if (_gap <= 0f) Start(NextColony(), false);
        }

        private void Start(MusicTrack track, bool loops)
        {
            Current = track;
            Loops = loops;
            Version++;
        }

        private void Wait(float seconds)
        {
            Current = null;
            Loops = false;
            _gap = seconds;
        }

        // a shuffled bag: every colony track once before any repeats, and never the same one twice in a row
        private MusicTrack NextColony()
        {
            if (_bag.Count == 0)
            {
                for (int i = 0; i < _colony.Length; i++) _bag.Add(i);
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
                if (_bag.Count > 1 && _bag[0] == _lastColony) (_bag[0], _bag[1]) = (_bag[1], _bag[0]);
            }
            _lastColony = _bag[0];
            _bag.RemoveAt(0);
            return _colony[_lastColony];
        }
    }
}
