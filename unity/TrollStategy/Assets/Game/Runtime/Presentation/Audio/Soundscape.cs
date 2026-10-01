using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Presentation.Audio
{
    /// <summary>
    /// Music and ambience under the feedback cues. Music: YannZ, "Indie Meditations" (CC BY 4.0), all in F at
    /// 120 BPM like the tuned cues; beds: field-recording loops from the Sonniss GDC 2026 bundle. Which track
    /// plays is <see cref="MusicSequencer"/>'s call; this component loads, crossfades and ducks.
    /// </summary>
    public sealed class Soundscape : MonoBehaviour
    {
        // the music's in-game loudness (integrated LUFS), under cues peaking at -44..-22 (docs/audio-direction.md)
        private const float MusicLoudness = -28f;
        private const float MusicFadeIn = 1.5f;
        private const float MusicFadeOut = 2.5f;
        private const float BedFade = 3f;
        private const float PauseFade = .3f;
        private const float PausedMusic = .35f;
        private const float GapMin = 20f;
        private const float GapMax = 45f;

        private static readonly MusicTrack Theme = new("Music/yannz_tutorial", -15.5f);
        private static readonly MusicTrack[] ColonyTracks =
        {
            new("Music/yannz_village", -16.2f), new("Music/yannz_grassland", -20f),
            new("Music/yannz_oasis", -19.5f), new("Music/yannz_raft", -18.1f),
        };
        private static readonly MusicTrack[] BattleTracks =
        {
            new("Music/yannz_royal_palace", -13.3f), new("Music/yannz_volcanic_shore", -15.1f),
        };
        // loops mastered to -30 LUFS short-term; the gains put the meadow at -38 in the colony
        private static readonly (string Resource, float Colony, float Battle)[] Beds =
        {
            ("Ambience/meadow", .4f, .25f),
            ("Ambience/shimmer", .16f, 0f),
        };

        private sealed class Voice
        {
            public AudioSource Source;
            public float Envelope;
            public float Target;
            public float Gain = 1f;
            public bool AwaitsEnd;
            public float StartedAt;
        }

        private static Soundscape s_instance;
        private MusicSequencer _sequencer;
        private readonly Voice[] _music = new Voice[2];
        private Voice[] _beds;
        private int _active;
        private int _version;
        private bool _paused;
        private float _pause = 1f;

        /// <summary>Music volume 0..1.</summary>
        public static float MusicVolume { get; set; } = 1f;
        /// <summary>Ambience volume 0..1.</summary>
        public static float AmbienceVolume { get; set; } = 1f;

        public static IEnumerable<string> MusicResources
        {
            get
            {
                yield return Theme.Resource;
                foreach (var track in ColonyTracks) yield return track.Resource;
                foreach (var track in BattleTracks) yield return track.Resource;
            }
        }

        public static IEnumerable<string> BedResources
        {
            get
            {
                foreach (var bed in Beds) yield return bed.Resource;
            }
        }

        /// <summary>Switches music and beds to the scene; entering a scene also lifts a pause duck.</summary>
        public static void Enter(SoundScene scene)
        {
            var soundscape = Instance;
            if (soundscape == null) return;
            soundscape._paused = false;
            soundscape._sequencer.Enter(scene);
        }

        /// <summary>A paused battle keeps its music, only quieter.</summary>
        public static void SetPaused(bool paused)
        {
            if (s_instance != null) s_instance._paused = paused;
        }

        private static Soundscape Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                if (!UnityEngine.Application.isPlaying) return null;
                var go = new GameObject("Soundscape");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<Soundscape>();
                return s_instance;
            }
        }

        private void Awake()
        {
            // the order of colony tracks is presentation only and never reaches the simulation
            _sequencer = new MusicSequencer(Theme, ColonyTracks, BattleTracks,
                UnityEngine.Random.Range(int.MinValue, int.MaxValue), GapMin, GapMax);
            for (int i = 0; i < _music.Length; i++) _music[i] = new Voice { Source = NewSource(false) };
            _beds = new Voice[Beds.Length];
            for (int i = 0; i < _beds.Length; i++) _beds[i] = new Voice { Source = NewSource(true) };
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.volume = 0f;
            // music and beds give way to feedback cues when voices run short
            source.priority = 32;
            return source;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var active = _music[_active];
            // the pack's tracks are cut to loop and stop mid-note: a one-shot one fades out before its end
            var clip = active.Source.clip;
            if (active.AwaitsEnd && clip != null && active.Source.time >= clip.length - MusicFadeOut)
                active.Target = 0f;
            // a one-shot track ran out: let the sequencer start its gap
            if (active.AwaitsEnd && !active.Source.isPlaying && Time.unscaledTime - active.StartedAt > 1f)
            {
                active.AwaitsEnd = false;
                _sequencer.Finished();
            }
            _sequencer.Advance(dt);
            if (_sequencer.Version != _version) StartTrack();
            else if (_sequencer.Current == null) _music[_active].Target = 0f;

            _pause = Mathf.MoveTowards(_pause, _paused ? PausedMusic : 1f, dt / PauseFade);
            foreach (var voice in _music) Fade(voice, dt, MusicFadeIn, MusicFadeOut, _pause * MusicVolume);

            bool battle = _sequencer.Scene is SoundScene.Battle or SoundScene.BattleResult;
            for (int i = 0; i < _beds.Length; i++)
            {
                var bed = _beds[i];
                bed.Target = _sequencer.Scene == null ? 0f : battle ? Beds[i].Battle : Beds[i].Colony;
                if (bed.Target > 0f && !bed.Source.isPlaying) StartBed(bed, Beds[i].Resource);
                Fade(bed, dt, BedFade, BedFade, AmbienceVolume);
            }
        }

        private void StartTrack()
        {
            _version = _sequencer.Version;
            var fading = _music[_active];
            fading.Target = 0f;
            fading.AwaitsEnd = false;
            var track = _sequencer.Current;
            if (track == null) return;
            var clip = Resources.Load<AudioClip>(track.Resource);
            if (clip == null)
            {
                Debug.LogWarning($"Music '{track.Resource}' is missing from Resources.");
                if (!_sequencer.Loops) _sequencer.Finished();
                return;
            }
            _active = 1 - _active;
            var voice = _music[_active];
            voice.Source.Stop();
            voice.Source.clip = clip;
            voice.Source.loop = _sequencer.Loops;
            voice.Gain = Mathf.Pow(10f, (MusicLoudness - track.Loudness) / 20f);
            voice.Envelope = 0f;
            voice.Target = 1f;
            voice.AwaitsEnd = !_sequencer.Loops;
            voice.StartedAt = Time.unscaledTime;
            voice.Source.volume = 0f;
            voice.Source.Play();
        }

        private static void StartBed(Voice bed, string resource)
        {
            var clip = bed.Source.clip != null ? bed.Source.clip : Resources.Load<AudioClip>(resource);
            if (clip == null)
            {
                bed.Target = 0f;
                return;
            }
            bed.Source.clip = clip;
            // start somewhere inside the loop so a session does not always open on the same bird
            bed.Source.timeSamples = UnityEngine.Random.Range(0, Mathf.Max(1, clip.samples));
            bed.Envelope = 0f;
            bed.Source.volume = 0f;
            bed.Source.Play();
        }

        private static void Fade(Voice voice, float dt, float fadeIn, float fadeOut, float master)
        {
            float seconds = voice.Target > voice.Envelope ? fadeIn : fadeOut;
            voice.Envelope = Mathf.MoveTowards(voice.Envelope, voice.Target, dt / seconds);
            voice.Source.volume = voice.Envelope * voice.Gain * master;
            if (voice.Envelope <= 0f && voice.Target <= 0f && voice.Source.isPlaying) voice.Source.Stop();
        }
    }
}
