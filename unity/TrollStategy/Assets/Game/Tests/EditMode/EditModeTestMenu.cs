using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// Runs every EditMode test and writes the results to Builds/Tests (git-ignored): the NUnit XML and a
    /// summary with each failure, so tools that cannot call the test runner API can start a run and read it.
    /// </summary>
    public static class EditModeTestMenu
    {
        private const string Folder = "Builds/Tests";

        [MenuItem("TrollStrategy/Dev/Tools/Run EditMode Tests")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            File.Delete(Path.Combine(Folder, "editmode-summary.txt"));
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Path.Combine(Folder, "editmode-results.xml")));
                var text = new StringBuilder();
                text.AppendLine($"pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount} " +
                                $"inconclusive={result.InconclusiveCount} time={result.Duration:F1}s");
                AppendFailures(result, text);
                File.WriteAllText(Path.Combine(Folder, "editmode-summary.txt"), text.ToString());
                Debug.Log($"[EditModeTests] pass={result.PassCount} fail={result.FailCount}");
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder text)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                        text.AppendLine($"FAIL {result.FullName}\n    {result.Message?.Trim().Replace("\n", "\n    ")}");
                    return;
                }
                foreach (var child in result.Children) AppendFailures(child, text);
            }
        }
    }
}
