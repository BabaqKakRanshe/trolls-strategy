using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TrollStrategy.Support;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Stamps every player build with its version, commit, time and edition: writes Resources/BuildInfo before the
    /// build and removes it after, so the editor never shows a stale stamp. The folder is git-ignored in case
    /// a failed build leaves it behind; the next build overwrites it.
    /// </summary>
    public sealed class BuildInfoStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string Folder = "Assets/Game/Generated";
        private const string ResourcesFolder = Folder + "/Resources";
        private const string StampPath = ResourcesFolder + "/" + BuildInfo.ResourceName + ".txt";

        public int callbackOrder => 0;

        /// <summary>The edition the next player build is stamped with; PlayerBuild sets it around the Steam demo.</summary>
        public static BuildEdition Edition { get; set; } = BuildEdition.Alpha;

        public void OnPreprocessBuild(BuildReport report)
        {
            var info = Collect();
            Directory.CreateDirectory(ResourcesFolder);
            File.WriteAllText(StampPath, info.Serialize());
            AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[BuildInfoStamp] {info.Label}");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }

        /// <summary>The stamp for the working copy as it is now; unknowns when git is not at hand.</summary>
        public static BuildInfo Collect()
        {
            int.TryParse(Git("rev-list --count HEAD"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int build);
            string commit = Git("rev-parse --short HEAD");
            bool dirty = !string.IsNullOrEmpty(Git("status --porcelain"));
            return new BuildInfo(UnityEngine.Application.version, build, commit, dirty, DateTime.UtcNow, false, Edition);
        }

        private static string Git(string arguments)
        {
            try
            {
                using var git = Process.Start(new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? ".",
                    // stderr stays unread and unredirected: line-ending warnings could fill its pipe and stall git
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (git == null) return null;
                // read before waiting: a full output pipe would stall git
                string output = git.StandardOutput.ReadToEnd();
                if (!git.WaitForExit(10000) || git.ExitCode != 0) return null;
                return output.Trim();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BuildInfoStamp] git {arguments} failed: {exception.Message}");
                return null;
            }
        }
    }
}
