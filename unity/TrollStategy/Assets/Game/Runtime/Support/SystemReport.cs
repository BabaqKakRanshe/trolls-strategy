using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>The running build and the machine under it, as rows for the tech panel and bug reports.</summary>
    public static class SystemReport
    {
        public static List<KeyValuePair<string, string>> Rows(BuildInfo build, FrameRateMeter frames)
        {
            var rows = new List<KeyValuePair<string, string>>();
            void Add(string key, string value) => rows.Add(new KeyValuePair<string, string>(key, value));

            Add("Версия", build.Label);
            if (build.BuiltUtc.HasValue)
                Add("Собрано", build.BuiltUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));
            Add("FPS", FpsText(frames));
            Add("Экран", $"{Screen.width}×{Screen.height}, {Screen.currentResolution.refreshRateRatio.value:0} Гц, " +
                         Screen.fullScreenMode);
            Add("Видеокарта", $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} МБ, " +
                              $"{SystemInfo.graphicsDeviceType})");
            Add("Процессор", $"{SystemInfo.processorType} ({SystemInfo.processorCount} потоков)");
            Add("Память", $"{SystemInfo.systemMemorySize} МБ");
            Add("Система", SystemInfo.operatingSystem);
            int quality = QualitySettings.GetQualityLevel();
            string qualityName = quality >= 0 && quality < QualitySettings.names.Length
                ? QualitySettings.names[quality]
                : quality.ToString(CultureInfo.InvariantCulture);
            Add("Качество", $"{qualityName}, vSync {QualitySettings.vSyncCount}");
            Add("Unity", UnityEngine.Application.unityVersion);
            Add("В игре", Duration(Time.realtimeSinceStartupAsDouble));
            return rows;
        }

        public static string FpsText(FrameRateMeter frames) =>
            frames == null || frames.Fps <= 0f
                ? "—"
                : string.Format(CultureInfo.InvariantCulture, "{0:0} (худший кадр {1:0} мс)", frames.Fps, frames.WorstFrameMs);

        /// <summary>"1 ч 02 мин", "3 мин 05 с" or "12 с".</summary>
        public static string Duration(double seconds)
        {
            long total = (long)System.Math.Max(0d, seconds);
            long hours = total / 3600, minutes = total / 60 % 60, rest = total % 60;
            if (hours > 0) return $"{hours} ч {minutes:00} мин";
            return minutes > 0 ? $"{minutes} мин {rest:00} с" : $"{rest} с";
        }

        /// <summary>One "key: value" line per row.</summary>
        public static string Text(IEnumerable<KeyValuePair<string, string>> rows)
        {
            var text = new StringBuilder();
            foreach (var row in rows) text.Append(row.Key).Append(": ").AppendLine(row.Value);
            return text.ToString();
        }
    }
}
