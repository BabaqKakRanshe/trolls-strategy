using System;
using System.Threading.Tasks;
using Unity.Services.UserReporting;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>
    /// Sends a report to Unity User Reporting; it shows in the project's Unity Dashboard under User Reports.
    /// Services start on the first report, unless analytics started them already. The service's client keeps
    /// every attachment ever added until it is configured again, so each report starts from a fresh configuration.
    /// </summary>
    public sealed class UnityReportUploader : IReportUploader
    {
        public const float TimeoutSeconds = 60f;
        private const string VersionDimension = "Version";

        public async Task<bool> UploadAsync(BugReport report, Action<float> progress)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (!CloudServices.Linked)
            {
                Debug.LogWarning("[Support] The project is not linked to Unity Cloud; the report is saved instead.");
                return false;
            }
            await CloudServices.StartAsync();

            var service = UserReportingService.Instance;
            service.Configure();
            service.AddAttachmentToReport("Логи и техинфо", report.ArchiveName, report.Archive(), "application/zip");
            var screenshot = report.Find(BugReport.ScreenshotFile);
            if (screenshot != null)
                service.AddAttachmentToReport("Снимок экрана", screenshot.Name, screenshot.Data, screenshot.MediaType);
            foreach (var field in report.Fields) service.AddMetadata(field.Key, field.Value);
            service.CreateNewUserReport();
            if (!service.HasOngoingReport) return false;
            service.SetReportSummary(report.Summary);
            service.AddDimensionValue(VersionDimension, report.Version);

            var done = new TaskCompletionSource<bool>();
            service.SendUserReport(value => progress?.Invoke(value), ok => done.TrySetResult(ok));
            // the service does not answer at all for some failures; give up and save the report instead
            double deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;
            while (!done.Task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                await Awaitable.NextFrameAsync();
            service.ClearOngoingReport();
            return done.Task.IsCompleted && done.Task.Result;
        }
    }
}
