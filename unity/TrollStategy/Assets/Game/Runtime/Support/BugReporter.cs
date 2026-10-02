using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>Sends a report to a service.</summary>
    public interface IReportUploader
    {
        /// <summary>True when the service took the report.</summary>
        Task<bool> UploadAsync(BugReport report, Action<float> progress);
    }

    public enum ReportResult
    {
        /// <summary>The service took the report.</summary>
        Sent,
        /// <summary>Upload failed; the report is a zip on disk or in the browser's downloads.</summary>
        Saved,
        /// <summary>Neither worked.</summary>
        Failed
    }

    public readonly struct ReportOutcome
    {
        private ReportOutcome(ReportResult result, string path, string error)
        {
            Result = result;
            Path = path;
            Error = error;
        }

        public ReportResult Result { get; }
        /// <summary>Where the saved zip is, for <see cref="ReportResult.Saved"/>: a path, or a note on the downloads.</summary>
        public string Path { get; }
        public string Error { get; }

        public static ReportOutcome Sent() => new(ReportResult.Sent, null, null);
        public static ReportOutcome Saved(string path) => new(ReportResult.Saved, path, null);
        public static ReportOutcome Failed(string error) => new(ReportResult.Failed, null, error);
    }

    /// <summary>
    /// Sends a report, and when that fails (no network, no service) saves it as a zip and shows the folder,
    /// so the tester can pass the file on by hand. With a download (a browser, whose files are out of reach)
    /// the zip goes to the browser's downloads instead.
    /// </summary>
    public sealed class BugReporter
    {
        private readonly IReportUploader _uploader;
        private readonly string _folder;
        private readonly Action<string> _reveal;
        private readonly Func<string, byte[], bool> _download;

        public BugReporter(IReportUploader uploader, string folder, Action<string> reveal = null,
            Func<string, byte[], bool> download = null)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("A folder for saved reports is required", nameof(folder));
            _uploader = uploader;
            _folder = folder;
            _reveal = reveal;
            _download = download;
        }

        public static string DefaultFolder => System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Reports");

        public async Task<ReportOutcome> SendAsync(BugReport report, Action<float> progress = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (_uploader != null)
            {
                try
                {
                    if (await _uploader.UploadAsync(report, progress)) return ReportOutcome.Sent();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Support] Report upload failed: {exception.Message}");
                }
            }

            try
            {
                if (_download != null)
                    return _download(report.ArchiveName, report.Archive())
                        ? ReportOutcome.Saved("загрузки браузера, " + report.ArchiveName)
                        : ReportOutcome.Failed("браузер не сохранил файл");
                Directory.CreateDirectory(_folder);
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(_folder, report.ArchiveName));
                File.WriteAllBytes(path, report.Archive());
                _reveal?.Invoke(_folder);
                return ReportOutcome.Saved(path);
            }
            catch (Exception exception)
            {
                return ReportOutcome.Failed(exception.Message);
            }
        }
    }
}
