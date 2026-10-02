using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>One file of a bug report.</summary>
    public sealed class ReportFile
    {
        public ReportFile(string name, byte[] data, string mediaType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            MediaType = mediaType ?? "application/octet-stream";
        }

        public string Name { get; }
        public byte[] Data { get; }
        public string MediaType { get; }
    }

    /// <summary>
    /// What one press of "send logs" carries: a summary, fields to sort reports by, and the files — the tech
    /// info, the game state, the recent log from memory, the tails of the current and previous log files, the
    /// browser's part in a web player and a screenshot.
    /// </summary>
    public sealed class BugReport
    {
        public const string InfoFile = "info.txt";
        public const string GameFile = "game.txt";
        public const string ScreenshotFile = "screenshot.jpg";
        /// <summary>The game's recent log kept in memory, see <see cref="LogRecorder"/>.</summary>
        public const string RecentLogFile = "log.txt";
        /// <summary>How much of the end of each log goes into a report.</summary>
        public const int LogTailBytes = 4 * 1024 * 1024;

        public BugReport(string summary, string version, IReadOnlyList<KeyValuePair<string, string>> fields,
            IReadOnlyList<ReportFile> files, DateTime createdLocal)
        {
            Summary = summary ?? string.Empty;
            Version = version ?? string.Empty;
            Fields = fields ?? Array.Empty<KeyValuePair<string, string>>();
            Files = files ?? Array.Empty<ReportFile>();
            CreatedLocal = createdLocal;
        }

        public string Summary { get; }
        /// <summary>The build's version label, a dimension reports are grouped by.</summary>
        public string Version { get; }
        public IReadOnlyList<KeyValuePair<string, string>> Fields { get; }
        public IReadOnlyList<ReportFile> Files { get; }
        public DateTime CreatedLocal { get; }

        public string ArchiveName =>
            "report-" + CreatedLocal.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".zip";

        public ReportFile Find(string name)
        {
            foreach (var file in Files)
                if (file.Name == name) return file;
            return null;
        }

        /// <summary>Every file in one zip: the attachment that is uploaded and the copy saved when that fails.</summary>
        public byte[] Archive()
        {
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            {
                foreach (var file in Files)
                {
                    var entry = zip.CreateEntry(file.Name, System.IO.Compression.CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(file.Data, 0, file.Data.Length);
                }
            }
            return buffer.ToArray();
        }

        /// <summary>
        /// Gathers a report. <paramref name="gameState"/>, <paramref name="screenshotJpg"/>,
        /// <paramref name="recentLog"/> and <paramref name="browser"/> may be null; log paths that do not exist
        /// are skipped.
        /// </summary>
        public static BugReport Create(BuildInfo build, IReadOnlyList<KeyValuePair<string, string>> techRows,
            string gameState, IEnumerable<string> logPaths, byte[] screenshotJpg, DateTime createdLocal,
            string recentLog = null, string browser = null)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            techRows ??= Array.Empty<KeyValuePair<string, string>>();
            var files = new List<ReportFile>();

            var info = new StringBuilder();
            info.Append("Отчёт: ").AppendLine(createdLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            info.Append(SystemReport.Text(techRows));
            files.Add(Text(InfoFile, info.ToString()));
            if (!string.IsNullOrEmpty(gameState)) files.Add(Text(GameFile, gameState));
            if (!string.IsNullOrEmpty(recentLog)) files.Add(Text(RecentLogFile, recentLog));
            if (!string.IsNullOrEmpty(browser)) files.Add(Text(BrowserReport.File, browser));
            if (logPaths != null)
                foreach (var path in logPaths)
                {
                    var tail = ReadTail(path, LogTailBytes);
                    if (tail != null) files.Add(new ReportFile(Path.GetFileName(path), tail, "text/plain"));
                }
            if (screenshotJpg != null && screenshotJpg.Length > 0)
                files.Add(new ReportFile(ScreenshotFile, screenshotJpg, "image/jpeg"));

            string summary = $"Отчёт игрока · {build.Label} · " +
                             createdLocal.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
            return new BugReport(summary, build.VersionLabel, techRows, files, createdLocal);
        }

        /// <summary>The current log and the previous run's, which holds the last session if it crashed.</summary>
        public static IEnumerable<string> LogPaths()
        {
            string current = UnityEngine.Application.consoleLogPath;
            if (string.IsNullOrEmpty(current)) yield break;
            yield return current;
            string folder = Path.GetDirectoryName(current) ?? string.Empty;
            yield return Path.Combine(folder,
                Path.GetFileNameWithoutExtension(current) + "-prev" + Path.GetExtension(current));
        }

        /// <summary>
        /// The last <paramref name="maxBytes"/> of a file the game may still be writing, marked when cut;
        /// null when there is no such file.
        /// </summary>
        public static byte[] ReadTail(string path, int maxBytes)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            long skipped = Math.Max(0L, length - Math.Max(0, maxBytes));
            stream.Seek(skipped, SeekOrigin.Begin);
            var tail = new byte[length - skipped];
            int read = 0;
            while (read < tail.Length)
            {
                int chunk = stream.Read(tail, read, tail.Length - read);
                if (chunk <= 0) break;
                read += chunk;
            }
            if (read < tail.Length) Array.Resize(ref tail, read);
            if (skipped == 0) return tail;
            var marker = Encoding.UTF8.GetBytes($"[… начало лога обрезано: {skipped} байт …]\n");
            var marked = new byte[marker.Length + tail.Length];
            Buffer.BlockCopy(marker, 0, marked, 0, marker.Length);
            Buffer.BlockCopy(tail, 0, marked, marker.Length, tail.Length);
            return marked;
        }

        private static ReportFile Text(string name, string text) =>
            new(name, Encoding.UTF8.GetBytes(text), "text/plain");
    }
}
