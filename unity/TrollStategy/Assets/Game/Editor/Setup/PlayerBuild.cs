using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
        private const string WebZip = "Builds/TrollStrategy-itch-webgl.zip";

        [MenuItem("TrollStrategy/Build Windows Player")]
        public static void BuildWindows()
        {
            Build(WindowsFolder, Path.Combine(WindowsFolder, "TrollStrategy.exe"), BuildTarget.StandaloneWindows64);
        }

        /// <summary>
        /// Browser player for itch.io: index.html at the zip root, gzip with the decompression fallback
        /// because itch does not send Content-Encoding for Unity's .gz files. The active target is switched
        /// back to Windows afterwards.
        /// </summary>
        [MenuItem("TrollStrategy/Build WebGL Player (itch.io)")]
        public static void BuildWebGL()
        {
            // WebGL player settings only take while WebGL is the active target.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            if (Directory.Exists(WebFolder))
                Directory.Delete(WebFolder, true);
            bool built = Build(WebFolder, WebFolder, BuildTarget.WebGL);
            if (built)
            {
                if (File.Exists(WebZip))
                    File.Delete(WebZip);
                ZipFile.CreateFromDirectory(WebFolder, WebZip, System.IO.Compression.CompressionLevel.Optimal, false);
                Debug.Log($"[PlayerBuild] itch.io zip: {Path.GetFullPath(WebZip)}");
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        private static bool Build(string folder, string location, BuildTarget target)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            string output = Path.GetFullPath(location);
            Directory.CreateDirectory(Path.GetFullPath(folder));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None
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
