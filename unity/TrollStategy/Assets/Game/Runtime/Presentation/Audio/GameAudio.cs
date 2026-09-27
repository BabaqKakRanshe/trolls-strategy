using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Presentation.Audio
{
    /// <summary>Feedback sounds; clips live in Assets/Game/Audio/Resources/Sfx (RPG Essentials SFX, Leohpaz).</summary>
    public enum Sfx
    {
        UiHover, UiClick, UiBack, UiDenied, Equip, Unequip, Coins, Pause, Unpause,
        Select, Land, Build, Step, Spawn, Upgrade, Demolish,
        Swing, Throw, Hit, HitHeavy, Block, Death, BattleStart, Victory, Defeat,
    }

    /// <summary>
    /// Restrained 2D feedback. Limits repeated cues and overlapping voices so busy scenes stay quiet.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const int Voices = 8;
        private const int MaxSameClip = 2;

        private static GameAudio s_instance;
        private static readonly Dictionary<Sfx, string> Files = new()
        {
            [Sfx.UiHover] = "ui_hover", [Sfx.UiClick] = "ui_click", [Sfx.UiBack] = "ui_back",
            [Sfx.UiDenied] = "ui_denied", [Sfx.Equip] = "equip", [Sfx.Unequip] = "unequip",
            [Sfx.Coins] = "coins", [Sfx.Pause] = "pause", [Sfx.Unpause] = "unpause",
            [Sfx.Select] = "select", [Sfx.Land] = "land", [Sfx.Build] = "build", [Sfx.Step] = "step",
            [Sfx.Spawn] = "spawn", [Sfx.Upgrade] = "upgrade", [Sfx.Demolish] = "demolish",
            [Sfx.Swing] = "swing", [Sfx.Throw] = "throw", [Sfx.Hit] = "hit", [Sfx.HitHeavy] = "hit_heavy",
            [Sfx.Block] = "block", [Sfx.Death] = "death", [Sfx.BattleStart] = "battle_start",
            [Sfx.Victory] = "victory", [Sfx.Defeat] = "defeat",
        };
        // UI sits below actions; result cues remain brief and close to the gameplay mix.
        private static readonly Dictionary<Sfx, float> Levels = new()
        {
            [Sfx.UiHover] = 0f, [Sfx.UiClick] = .32f, [Sfx.UiBack] = .28f, [Sfx.UiDenied] = .34f,
            [Sfx.Equip] = .32f, [Sfx.Unequip] = .28f, [Sfx.Pause] = .28f, [Sfx.Unpause] = .28f,
            [Sfx.Select] = .32f, [Sfx.Step] = .16f, [Sfx.Coins] = .25f,
            [Sfx.Land] = .38f, [Sfx.Build] = .42f, [Sfx.Spawn] = .38f,
            [Sfx.Upgrade] = .42f, [Sfx.Demolish] = .42f,
            [Sfx.Swing] = .32f, [Sfx.Throw] = .3f, [Sfx.Hit] = .48f,
            [Sfx.HitHeavy] = .58f, [Sfx.Block] = .32f, [Sfx.Death] = .38f,
            [Sfx.BattleStart] = .4f, [Sfx.Victory] = .45f, [Sfx.Defeat] = .4f,
        };

        private readonly Dictionary<Sfx, AudioClip> _clips = new();
        private readonly Dictionary<Sfx, float> _lastPlayed = new();
        private AudioSource[] _voices = Array.Empty<AudioSource>();
        private int _next;

        /// <summary>Overall effects volume 0..1.</summary>
        public static float Volume { get; set; } = .6f;

        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchJitter = .025f)
        {
            var audio = Instance;
            if (audio != null) audio.PlayInternal(sfx, volume, pitch, pitchJitter);
        }

        private static GameAudio Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                if (!UnityEngine.Application.isPlaying) return null;
                var go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<GameAudio>();
                return s_instance;
            }
        }

        private void Awake()
        {
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.priority = 96;
                _voices[i] = voice;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private void PlayInternal(Sfx sfx, float volume, float pitch, float pitchJitter)
        {
            float gain = Mathf.Clamp01(volume * Level(sfx) * Volume);
            if (gain <= 0f) return;
            var clip = Clip(sfx);
            if (clip == null) return;
            // Space recurring activity independently of render frames and simulation speed.
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) && now - last < RepeatInterval(sfx)) return;

            int playing = 0;
            foreach (var voice in _voices)
                if (voice.isPlaying && voice.clip == clip) playing++;
            int limit = sfx == Sfx.Step || sfx == Sfx.Coins ? 1 : MaxSameClip;
            if (playing >= limit) return;

            var source = FreeVoice();
            if (source == null) return;
            _lastPlayed[sfx] = now;
            source.clip = clip;
            source.volume = gain;
            float jitter = Mathf.Clamp(pitchJitter, 0f, .04f);
            source.pitch = Mathf.Clamp(pitch * (1f + UnityEngine.Random.Range(-jitter, jitter)), .65f, 1.08f);
            source.Play();
        }

        private AudioSource FreeVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = _voices[(_next + i) % _voices.Length];
                if (voice.isPlaying) continue;
                _next = (_next + i + 1) % _voices.Length;
                return voice;
            }
            // Dropping excess feedback avoids both a louder pile-up and a hard cut in a playing clip.
            return null;
        }

        private AudioClip Clip(Sfx sfx)
        {
            if (_clips.TryGetValue(sfx, out var clip)) return clip;
            clip = Files.TryGetValue(sfx, out var file) ? Resources.Load<AudioClip>("Sfx/" + file) : null;
            _clips[sfx] = clip;
            return clip;
        }

        private static float RepeatInterval(Sfx sfx) => sfx switch
        {
            Sfx.Step => .18f,
            Sfx.Coins => .45f,
            Sfx.UiDenied => .25f,
            Sfx.Death => .2f,
            Sfx.BattleStart or Sfx.Victory or Sfx.Defeat => .75f,
            _ => .1f,
        };

        private static float Level(Sfx sfx) => Levels.TryGetValue(sfx, out float level) ? level : .35f;
    }
}
