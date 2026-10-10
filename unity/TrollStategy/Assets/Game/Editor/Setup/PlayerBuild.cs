using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Players of the enabled build scenes into Builds/&lt;platform&gt; (git-ignored). The result also goes
    /// to build-result.txt in that folder, so tools that cannot call the build API can still read it.
    /// </summary>
    public static class PlayerBuild
    {
        private const string WindowsFolder = "Builds/Windows";
        private const string WebFolder = "Builds/WebGL";
        private const string SteamDemoFolder = "Builds/SteamDemo";
        private const string WebZip = "Builds/TrollStrategy-itch-webgl.zip";
        private const string WebCheatsZip = "Builds/TrollStrategy-itch-webgl-cheats.zip";
        /// <summary>Keeps the developer cheat menu (F1, or "Читы" in the tech panel) in a player.</summary>
        private const string CheatsDefine = "UNITY_ENABLE_CHECKS";

        [MenuItem("TrollStrategy/Build Windows Player")]
        public static void BuildWindows()
        {
            Build(WindowsFolder, Path.Combine(WindowsFolder, "TrollStrategy.exe"), BuildTarget.StandaloneWindows64);
        }

        /// <summary>
        /// The Windows player for the Steam demo: the same game stamped as the demo (BuildInfo.Edition), so it ends
        /// after the demo's quest in Progression.asset and the intro and the about page call it a demo and offer the
        /// wishlist. The testers' browser build plays the whole game; every other build is the itch.io alpha, which
        /// ends after the tutorial.
        /// </summary>
        [MenuItem("TrollStrategy/Build Windows Player (Steam Demo)")]
        public static void BuildSteamDemo()
        {
            if (!GameLinks.HasSteamPage)
                Debug.LogWarning("[PlayerBuild] GameLinks.SteamPage is empty: the demo will not offer the wishlist.");
            BuildInfoStamp.Edition = BuildEdition.SteamDemo;
            try
            {
                Build(SteamDemoFolder, Path.Combine(SteamDemoFolder, "TrollStrategy.exe"), BuildTarget.StandaloneWindows64);
            }
            finally
            {
                BuildInfoStamp.Edition = BuildEdition.Alpha;
            }
        }

        /// <summary>
        /// Browser player for itch.io: index.html at the zip root, gzip with the decompression fallback
        /// because itch does not send Content-Encoding for Unity's .gz files. The active target is switched
        /// back to Windows afterwards.
        /// </summary>
        [MenuItem("TrollStrategy/Build WebGL Player (itch.io)")]
        public static void BuildWebGL() => BuildWebGL(false);

        /// <summary>The browser player with the cheat menu and the whole game, for testers; not for the public page.</summary>
        [MenuItem("TrollStrategy/Build WebGL Player (itch.io, cheats)")]
        public static void BuildWebGLWithCheats() => BuildWebGL(true);

        private static void BuildWebGL(bool cheats)
        {
            // WebGL player settings only take while WebGL is the active target.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // report.js in this template collects the browser's half of bug reports, see BrowserReport
            PlayerSettings.WebGL.template = "PROJECT:TrollStrategy";

            if (Directory.Exists(WebFolder))
                Directory.Delete(WebFolder, true);
            // the testers play the whole game; the public page gets the alpha, which ends after the tutorial
            BuildInfoStamp.Edition = cheats ? BuildEdition.Full : BuildEdition.Alpha;
            bool built;
            try
            {
                built = Build(WebFolder, WebFolder, BuildTarget.WebGL, cheats ? new[] { CheatsDefine } : null);
            }
            finally
            {
                BuildInfoStamp.Edition = BuildEdition.Alpha;
            }
            string zip = cheats ? WebCheatsZip : WebZip;
            if (built)
            {
                if (File.Exists(zip))
                    File.Delete(zip);
                ZipFile.CreateFromDirectory(WebFolder, zip, System.IO.Compression.CompressionLevel.Optimal, false);
                Debug.Log($"[PlayerBuild] itch.io zip: {Path.GetFullPath(zip)}");
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        private static bool Build(string folder, string location, BuildTarget target, string[] defines = null)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            string output = Path.GetFullPath(location);
            Directory.CreateDirectory(Path.GetFullPath(folder));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = defines
            });

            var summary = report.summary;
            var text = new StringBuilder();
            text.AppendLine($"{summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                            $"time={summary.totalTime} output={output}");
            foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    text.AppendLine(message.content);
            File.WriteAllText(Path.Combine(folder, "build-result.txt"), text.ToString());
            Debug.Log($"[PlayerBuild] {summary.result}: {output}");
            return summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }
    }
}
