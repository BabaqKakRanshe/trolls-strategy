using System;
using TrollStrategy.Presentation.Audio;
using UnityEngine;

namespace TrollStrategy.Presentation
{
    /// <summary>
    /// The player's settings: music, sound and ambience volumes, graphics quality (automatic by default) and
    /// language, kept in PlayerPrefs and applied at start. Presentation only: nothing here touches the game.
    /// </summary>
    public static class GameSettings
    {
        private const string MusicKey = "settings.music";
        private const string SoundKey = "settings.sound";
        private const string AmbienceKey = "settings.ambience";
        private const string QualityKey = "settings.quality";
        private const string LanguageKey = "settings.language";
        private const string UiScaleKey = "settings.uiScale";
        private const string HintsKey = "settings.tutorialHints";
        private const string IntroKey = "intro.seen";
        private const string ControlsKey = "tutorial.controls";

        /// <summary>The quality choice that lets the game pick a level for the device.</summary>
        public const int AutoQuality = -1;

        public static float Music { get; private set; } = 1f;
        public static float Sound { get; private set; } = 1f;
        public static float Ambience { get; private set; } = 1f;
        /// <summary><see cref="AutoQuality"/> or an index into QualitySettings.names.</summary>
        public static int Quality { get; private set; } = AutoQuality;
        /// <summary>Two-letter language code; empty means English until the player picks one.</summary>
        public static string Language { get; private set; } = string.Empty;
        /// <summary>The interface size the player chose; 0 is automatic.</summary>
        public static float UiScale { get; private set; }
        /// <summary>Whether the tutorial pointer shows: the veil, the hand, the hint card and the free cells. On by default.</summary>
        public static bool TutorialHints { get; private set; } = true;

        public static event Action Changed;

        public static void Load()
        {
            Music = Read(MusicKey, 1f);
            Sound = Read(SoundKey, 1f);
            Ambience = Read(AmbienceKey, 1f);
            Quality = ReadInt(QualityKey, AutoQuality);
            Language = ReadString(LanguageKey, string.Empty);
            UiScale = ReadFloat(UiScaleKey, 0f);
            TutorialHints = ReadInt(HintsKey, 1) != 0;
            Apply();
        }

        public static void SetMusic(float value) => Set(MusicKey, Music = Mathf.Clamp01(value));
        public static void SetSound(float value) => Set(SoundKey, Sound = Mathf.Clamp01(value));
        public static void SetAmbience(float value) => Set(AmbienceKey, Ambience = Mathf.Clamp01(value));

        public static void SetQuality(int level)
        {
            Quality = level < 0 ? AutoQuality : Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            Try(() =>
            {
                PlayerPrefs.SetInt(QualityKey, Quality);
                PlayerPrefs.Save();
            });
            Apply();
        }

        public static void SetLanguage(string code)
        {
            Language = code ?? string.Empty;
            Try(() =>
            {
                PlayerPrefs.SetString(LanguageKey, Language);
                PlayerPrefs.Save();
            });
            Changed?.Invoke();
        }

        /// <summary>Whether the player has chosen hints or none (the first launch's notice asks it once).</summary>
        public static bool TutorialHintsChosen
        {
            get
            {
                try { return PlayerPrefs.HasKey(HintsKey); }
                catch (Exception) { return true; }
            }
        }

        public static void SetTutorialHints(bool on)
        {
            TutorialHints = on;
            Try(() =>
            {
                PlayerPrefs.SetInt(HintsKey, on ? 1 : 0);
                PlayerPrefs.Save();
            });
            Changed?.Invoke();
        }

        public static void SetUiScale(float scale)
        {
            UiScale = scale <= 0f ? 0f : Mathf.Clamp(scale, .8f, 1.8f);
            Try(() =>
            {
                PlayerPrefs.SetFloat(UiScaleKey, UiScale);
                PlayerPrefs.Save();
            });
            Changed?.Invoke();
        }

        /// <summary>
        /// The interface size in use: the chosen one, or for the device a third larger on a phone or a tablet
        /// (a finger needs bigger buttons and the screen is small), the designed size elsewhere.
        /// </summary>
        public static float EffectiveUiScale
        {
            get
            {
                if (UiScale > 0f) return UiScale;
                if (UnityEngine.Application.isMobilePlatform) return 1.35f;
                float dpi = Screen.dpi;
                // a small touch screen in a browser (a phone) also gets the larger size
                if (dpi > 0f && Screen.width / dpi < 7f && UnityEngine.InputSystem.Touchscreen.current != null) return 1.35f;
                return 1f;
            }
        }

        /// <summary>The quality level in use: the chosen one, or the device's when the choice is automatic.</summary>
        public static int EffectiveQuality => Quality == AutoQuality ? DetectQuality() : Quality;

        /// <summary>
        /// A level for this device: phones, tablets and the browser start in the middle, weak graphics lower,
        /// a desktop with a real graphics card at the top.
        /// </summary>
        public static int DetectQuality()
        {
            int top = Mathf.Max(0, QualitySettings.names.Length - 1);
            int memory = SystemInfo.graphicsMemorySize;
            int level;
            if (UnityEngine.Application.isMobilePlatform) level = memory >= 3000 ? 3 : 2;
            else if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer) level = memory >= 2000 ? 3 : 2;
            else if (memory > 0 && memory < 1500) level = 2;
            else if (memory > 0 && memory < 3500) level = 4;
            else level = top;
            return Mathf.Clamp(level, 0, top);
        }

        /// <summary>Whether the player has had the tutorial's controls lesson (the camera, the zoom, the keys); once per player.</summary>
        public static bool ControlsLearned => ReadInt(ControlsKey, 0) == 1;

        public static void SetControlsLearned(bool learned) => Try(() =>
        {
            PlayerPrefs.SetInt(ControlsKey, learned ? 1 : 0);
            PlayerPrefs.Save();
        });

        /// <summary>Whether the alpha notice was already shown for this build's version.</summary>
        public static bool IntroSeen(string version) => ReadString(IntroKey, string.Empty) == version;

        public static void MarkIntroSeen(string version) => Try(() =>
        {
            PlayerPrefs.SetString(IntroKey, version ?? string.Empty);
            PlayerPrefs.Save();
        });

        private static void Apply()
        {
            Soundscape.MusicVolume = Music;
            Soundscape.AmbienceVolume = Ambience;
            GameAudio.Volume = Sound;
            int level = EffectiveQuality;
            if (QualitySettings.names.Length > 0 && QualitySettings.GetQualityLevel() != level)
                QualitySettings.SetQualityLevel(level, true);
            Changed?.Invoke();
        }

        private static void Set(string key, float value)
        {
            Try(() =>
            {
                PlayerPrefs.SetFloat(key, value);
                PlayerPrefs.Save();
            });
            Apply();
        }

        // PlayerPrefs can throw where storage is blocked (some browsers); settings then last for the session.
        private static float Read(string key, float fallback)
        {
            try { return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback)); }
            catch (Exception) { return fallback; }
        }

        private static float ReadFloat(string key, float fallback)
        {
            try { return PlayerPrefs.GetFloat(key, fallback); }
            catch (Exception) { return fallback; }
        }

        private static int ReadInt(string key, int fallback)
        {
            try { return PlayerPrefs.GetInt(key, fallback); }
            catch (Exception) { return fallback; }
        }

        private static string ReadString(string key, string fallback)
        {
            try { return PlayerPrefs.GetString(key, fallback); }
            catch (Exception) { return fallback; }
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception) { }
        }
    }
}
