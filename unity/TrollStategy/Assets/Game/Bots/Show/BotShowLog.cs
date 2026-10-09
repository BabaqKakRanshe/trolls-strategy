using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TrollStrategy.Bots
{
    /// <summary>What a show bot's moment in the video is.</summary>
    public enum ShowEventKind
    {
        /// <summary>A command performed at the HUD.</summary>
        Command,
        /// <summary>A command the hands could not do at the HUD; it went to the session directly.</summary>
        Fallback,
        /// <summary>The bot waits: the colony runs fast in the video.</summary>
        Wait,
        /// <summary>A new quest began.</summary>
        Quest,
        /// <summary>Something the viewer should notice: an error, a refusal of the HUD.</summary>
        Note
    }

    /// <summary>One moment of the show: when in the video, when in the colony, what and why.</summary>
    public sealed class ShowEvent
    {
        public double Start;
        public double End;
        public int ColonyMs;
        public ShowEventKind Kind;
        public string Text;
        public string Why;
        /// <summary>The session's answer: null for an accepted command or a non-command moment.</summary>
        public string Refused;
        /// <summary>Where the HUD failed the hands, in words; null when it did not.</summary>
        public string Problem;
        /// <summary>Picture of the screen at a problem, relative to the report folder.</summary>
        public string Picture;
    }

    /// <summary>
    /// The show bot's account of its game: subtitles for the video, a timeline page that jumps the video to any
    /// moment, and a text summary. Times are video seconds.
    /// </summary>
    public sealed class BotShowLog
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public List<ShowEvent> Events { get; } = new();

        public ShowEvent Add(ShowEventKind kind, double at, int colonyMs, string text, string why = null)
        {
            var e = new ShowEvent { Kind = kind, Start = at, End = at, ColonyMs = colonyMs, Text = text, Why = why };
            Events.Add(e);
            return e;
        }

        public IEnumerable<ShowEvent> Problems => Events.Where(e => e.Problem != null);

        /// <summary>SubRip subtitles: every moment shows until the next one begins (at least 1.5 s, at most 12 s).</summary>
        public string Subtitles(bool webVtt = false)
        {
            var text = new StringBuilder();
            var shown = Events.Where(e => e.Kind != ShowEventKind.Note || e.Problem != null).OrderBy(e => e.Start).ToList();
            for (int i = 0; i < shown.Count; i++)
            {
                var e = shown[i];
                double next = i + 1 < shown.Count ? shown[i + 1].Start : e.End + 3;
                double end = Math.Min(e.Start + 12, Math.Max(Math.Max(e.End, e.Start + 1.5), Math.Min(next, e.Start + 4)));
                if (i + 1 < shown.Count) end = Math.Min(end, Math.Max(next, e.Start + 0.5));
                text.AppendLine((i + 1).ToString(Invariant));
                text.AppendLine($"{Srt(e.Start, webVtt)} --> {Srt(end, webVtt)}");
                text.AppendLine(Line(e));
                if (!string.IsNullOrEmpty(e.Why)) text.AppendLine("Зачем: " + e.Why);
                text.AppendLine();
            }
            return text.ToString();
        }

        /// <summary>The same moments as data for the timeline page.</summary>
        public string Script(string title, string video, string subtitles, IEnumerable<(string Name, string Value)> facts)
        {
            string Num(double value) => value.ToString("0.###", Invariant);
            var items = Events.OrderBy(e => e.Start).Select(e => BotReportData.Obj(
                ("t", Num(e.Start)),
                ("end", Num(e.End)),
                ("colonyMs", e.ColonyMs.ToString(Invariant)),
                ("kind", BotReportData.Str(e.Kind.ToString())),
                ("text", BotReportData.Str(e.Text)),
                ("why", BotReportData.Str(e.Why)),
                ("refused", BotReportData.Str(e.Refused)),
                ("problem", BotReportData.Str(e.Problem)),
                ("picture", BotReportData.Str(e.Picture))));
            return "window.BOT_SHOW = " + BotReportData.Obj(
                ("title", BotReportData.Str(title)),
                ("video", BotReportData.Str(video)),
                ("subtitles", BotReportData.Str(subtitles)),
                ("facts", "[" + string.Join(",", facts.Select(f => BotReportData.Obj(("name", BotReportData.Str(f.Name)),
                    ("value", BotReportData.Str(f.Value))))) + "]"),
                ("events", "[\n" + string.Join(",\n", items) + "\n]")) + ";\n";
        }

        /// <summary>A plain-text account for a quick read: the facts, then the HUD's problems, then every moment.</summary>
        public string Markdown(string title, IEnumerable<(string Name, string Value)> facts)
        {
            var text = new StringBuilder();
            text.AppendLine($"# {title}").AppendLine();
            foreach (var (name, value) in facts) text.AppendLine($"- {name}: {value}");
            text.AppendLine();
            var problems = Problems.ToList();
            text.AppendLine(problems.Count == 0
                ? "Интерфейс справился с каждой командой бота."
                : $"## Где интерфейс не дал сделать ход ({problems.Count})").AppendLine();
            foreach (var e in problems)
                text.AppendLine($"- {Clock(e.Start)} — {e.Text}: {e.Problem}" + (e.Picture != null ? $" ([кадр]({e.Picture}))" : ""));
            if (problems.Count > 0) text.AppendLine();
            text.AppendLine("## Ход игры").AppendLine();
            foreach (var e in Events.OrderBy(e => e.Start))
                text.AppendLine($"- {Clock(e.Start)} (колония {Clock(e.ColonyMs / 1000.0)}) {Line(e)}" +
                                (string.IsNullOrEmpty(e.Why) ? "" : $" — {e.Why}"));
            return text.ToString();
        }

        public static string Clock(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss", Invariant) : span.ToString(@"m\:ss", Invariant);
        }

        private static string Line(ShowEvent e) => e.Kind switch
        {
            ShowEventKind.Fallback => $"{e.Text} (мимо интерфейса: {e.Problem})",
            ShowEventKind.Quest => "Задание: " + e.Text,
            _ when e.Refused != null => $"{e.Text} — отказ: {e.Refused}",
            _ => e.Text
        };

        private static string Srt(double seconds, bool webVtt)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}{(webVtt ? "." : ",")}{span.Milliseconds:000}";
        }

        public void WriteAll(string folder, string title, string video, IReadOnlyList<(string Name, string Value)> facts)
        {
            Directory.CreateDirectory(folder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(folder, "video.srt"), Subtitles(), utf8);
            File.WriteAllText(Path.Combine(folder, "video.vtt"), "WEBVTT\n\n" + Subtitles(true), utf8);
            File.WriteAllText(Path.Combine(folder, "show-data.js"), Script(title, video, "video.vtt", facts), utf8);
            File.WriteAllText(Path.Combine(folder, "show.md"), Markdown(title, facts), utf8);
        }
    }
}
