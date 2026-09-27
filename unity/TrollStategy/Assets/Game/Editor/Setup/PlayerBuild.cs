using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Windows player of the enabled build scenes into Builds/Windows (git-ignored). The result also goes
    /// to Builds/Windows/build-result.txt, so tools that cannot call the build API can still read it.
    /// </summary>
    public static class PlayerBuild
    {
        private const string Folder = "Builds/Windows";

        [MenuItem("TrollStrategy/Build Windows Player")]
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            string output = Path.GetFullPath(Path.Combine(Folder, "TrollStrategy.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
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
            File.WriteAllText(Path.Combine(Folder, "build-result.txt"), text.ToString());
            Debug.Log($"[PlayerBuild] {summary.result}: {output}");
        }
    }
}
