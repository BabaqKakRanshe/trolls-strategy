using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.CrashReportHandler;

namespace TrollStrategy.Support
{
    /// <summary>
    /// Which build this is: the itch.io alpha, the demo on Steam, or the whole game (the editor and the testers'
    /// build). The alpha and the demo end after their quests in Progression.asset.
    /// </summary>
    public enum BuildEdition
    {
        Alpha,
        SteamDemo,
        Full
    }

    /// <summary>
    /// Which build is running: the product version, the commit it was built from and when, and its edition. A player build
    /// carries them in Resources/BuildInfo, written by the editor's build step; the editor has no stamp and
    /// says so rather than showing the last build's.
    /// </summary>
    public sealed class BuildInfo
    {
        public const string ResourceName = "BuildInfo";

        private const string AlphaKey = "alpha";
        private const string SteamDemoKey = "steam-demo";
        private const string FullKey = "full";

        private static BuildInfo s_current;

        public BuildInfo(string version, int build, string commit, bool dirty, DateTime? builtUtc, bool isEditor,
            BuildEdition edition = BuildEdition.Alpha)
        {
            Version = string.IsNullOrEmpty(version) ? "0" : version;
            Build = Math.Max(0, build);
            Commit = string.IsNullOrEmpty(commit) ? null : commit;
            Dirty = dirty;
            BuiltUtc = builtUtc;
            IsEditor = isEditor;
            Edition = edition;
        }

        /// <summary>The product version from the player settings, such as "1.0".</summary>
        public string Version { get; }
        /// <summary>Commits on the built branch; 0 when unknown.</summary>
        public int Build { get; }
        /// <summary>Short hash of the built commit; null when unknown.</summary>
        public string Commit { get; }
        /// <summary>The build had changes that were not committed.</summary>
        public bool Dirty { get; }
        public DateTime? BuiltUtc { get; }
        public bool IsEditor { get; }
        /// <summary>The itch.io alpha unless the build was stamped otherwise; the editor plays the whole game.</summary>
        public BuildEdition Edition { get; }

        /// <summary>"1.0.412", or "1.0" without a build number.</summary>
        public string VersionNumber => Build > 0 ? $"{Version}.{Build}" : Version;

        /// <summary>"v1.0.412", or "v1.0" without a build number.</summary>
        public string VersionLabel => $"v{VersionNumber}";

        /// <summary>"a1b2c3d", starred for uncommitted changes; "редактор" in the editor, "?" when unknown.</summary>
        public string CommitLabel => IsEditor ? "редактор" : Commit == null ? "?" : Dirty ? Commit + "*" : Commit;

        public string Label => $"{VersionLabel} · {CommitLabel}";

        public static BuildInfo Current => s_current ??= Load();

        /// <summary>The stamp as the build writes it: one key=value per line.</summary>
        public string Serialize()
        {
            var text = new StringBuilder();
            text.Append("version=").AppendLine(Version);
            text.Append("build=").AppendLine(Build.ToString(CultureInfo.InvariantCulture));
            if (Commit != null) text.Append("commit=").AppendLine(Commit);
            text.Append("dirty=").AppendLine(Dirty ? "true" : "false");
            text.Append("edition=").AppendLine(Edition == BuildEdition.SteamDemo ? SteamDemoKey
                : Edition == BuildEdition.Full ? FullKey : AlphaKey);
            if (BuiltUtc.HasValue)
                text.Append("built=").AppendLine(BuiltUtc.Value.ToString("o", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        /// <summary>Reads a stamp; a missing or partial one keeps <paramref name="fallbackVersion"/> and unknowns.</summary>
        public static BuildInfo Parse(string text, string fallbackVersion)
        {
            string version = fallbackVersion, commit = null;
            int build = 0;
            bool dirty = false;
            DateTime? built = null;
            var edition = BuildEdition.Alpha;
            foreach (var line in (text ?? string.Empty).Split('\n'))
            {
                int split = line.IndexOf('=');
                if (split <= 0) continue;
                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1).Trim();
                switch (key)
                {
                    case "version": version = value; break;
                    case "build": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out build); break;
                    case "commit": commit = value; break;
                    case "dirty": dirty = value == "true"; break;
                    case "edition":
                        edition = value == SteamDemoKey ? BuildEdition.SteamDemo
                            : value == FullKey ? BuildEdition.Full : BuildEdition.Alpha;
                        break;
                    case "built":
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
                            built = at.ToUniversalTime();
                        break;
                }
            }
            return new BuildInfo(version, build, commit, dirty, built, false, edition);
        }

        private static BuildInfo Load()
        {
#if UNITY_EDITOR
            return new BuildInfo(UnityEngine.Application.version, 0, null, false, null, true, BuildEdition.Full);
#else
            var stamp = Resources.Load<TextAsset>(ResourceName);
            return Parse(stamp != null ? stamp.text : null, UnityEngine.Application.version);
#endif
        }

        // Crash and exception reports from Unity Diagnostics carry the build they came from.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void TagCrashReports()
        {
            var info = Current;
            CrashReportHandler.SetUserMetadata("build", info.Label);
            CrashReportHandler.SetUserMetadata("edition", info.Edition.ToString());
            if (info.Commit != null) CrashReportHandler.SetUserMetadata("commit", info.Commit);
        }
    }
}
