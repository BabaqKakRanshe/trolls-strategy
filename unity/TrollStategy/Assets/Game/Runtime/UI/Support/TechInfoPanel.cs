using System;
using System.Collections.Generic;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Support;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The support corner: build version and FPS over every screen. A click on it, or F8, opens the tech
    /// panel with the machine's details, "send logs" and the switch for anonymous play statistics; the player
    /// answers that question first in the intro.
    /// </summary>
    public sealed class TechInfoPanel
    {
        public const float RefreshSeconds = .5f;
        public const float GoodFps = 50f;
        public const float PoorFps = 30f;

        private readonly SupportContext _context;
        private readonly Label _fps;
        private readonly VisualElement _details;
        private readonly VisualElement _rows;
        private readonly Button _send;
        private readonly Button _cheats;
        private Action _openCheats;
        private readonly Label _status;
        private readonly Button _stats;
        private readonly List<(Label Key, Label Value)> _rowLabels = new();
        private float _sinceRefresh;
        private bool _sending;

        public TechInfoPanel(VisualElement root, SupportContext context)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Ui.SetText(Ui.Require<Label>(root, "tech-version"), context.Build.Label);
            _fps = Ui.Require<Label>(root, "tech-fps");
            _details = Ui.Require<VisualElement>(root, "tech-details");
            _rows = Ui.Require<VisualElement>(root, "tech-rows");
            _send = Ui.Require<Button>(root, "tech-send");
            _cheats = Ui.Require<Button>(root, "tech-cheats");
            _status = Ui.Require<Label>(root, "tech-status");
            _stats = Ui.Require<Button>(root, "tech-stats");
            UiFeel.Bind(Ui.Require<Button>(root, "tech-strip"), Toggle);
            UiFeel.Bind(Ui.Require<Button>(root, "tech-close"), Hide, Sfx.UiBack);
            UiFeel.Bind(_send, Send);
            UiFeel.Bind(_cheats, OpenCheats);
            UiFeel.Bind(_stats, ToggleStats);
            SetCheats(null);
            Ui.Show(_details, false);
            ShowStatus(null);
            Refresh();
        }

        public bool IsOpen => Ui.IsShown(_details);
        public bool IsSending => _sending;
        public string Status => Ui.IsShown(_status) ? _status.text : null;

        private Telemetry Stats => _context.Telemetry;

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public void Show()
        {
            Ui.Show(_details, true);
            Refresh();
        }

        public void Hide()
        {
            Ui.Show(_details, false);
            ShowStats();
        }

        /// <summary>Sends a bug report as "send logs" does: the menu's "report a bug".</summary>
        public void SendReport() => Send();

        /// <summary>
        /// The way to the developer cheat menu for touch screens, which have no F1; null (release players)
        /// hides the button.
        /// </summary>
        public void SetCheats(Action open)
        {
            _openCheats = open;
            Ui.Show(_cheats, open != null);
        }

        private void OpenCheats()
        {
            Hide();
            _openCheats?.Invoke();
        }

        public void Tick(float deltaSeconds)
        {
            _sinceRefresh += deltaSeconds;
            if (_sinceRefresh >= RefreshSeconds) Refresh();
        }

        public static string Describe(ReportOutcome outcome) => outcome.Result switch
        {
            ReportResult.Sent => "Отчёт отправлен. Спасибо!",
            ReportResult.Saved => "Отправить не удалось — отчёт сохранён в файл:\n" + outcome.Path,
            _ => "Не удалось собрать отчёт: " + outcome.Error
        };

        private void Refresh()
        {
            _sinceRefresh = 0f;
            float fps = _context.Frames.Fps;
            bool reading = fps > 0f;
            Ui.SetText(_fps, reading ? $"{Mathf.RoundToInt(fps)} FPS" : "— FPS");
            _fps.EnableInClassList("t-good", reading && fps >= GoodFps);
            _fps.EnableInClassList("t-warn", reading && fps < GoodFps && fps >= PoorFps);
            _fps.EnableInClassList("t-bad", reading && fps < PoorFps);
            if (IsOpen) FillRows(_context.Rows());
            ShowStats();
        }

        // the switch shows where a service can take the answer
        private void ShowStats()
        {
            bool available = Stats != null && Stats.Available;
            Ui.Show(_stats, available);
            if (available)
            {
                _stats.EnableInClassList("is-on", Stats.Collecting);
                Ui.SetText(_stats, Stats.Collecting ? "Статистика: отправляется" : "Статистика: не отправляется");
            }
        }

        private void ToggleStats()
        {
            Stats.Answer(!Stats.Collecting);
            ShowStats();
        }

        private void FillRows(IReadOnlyList<KeyValuePair<string, string>> rows)
        {
            if (_rowLabels.Count != rows.Count)
            {
                _rows.Clear();
                _rowLabels.Clear();
                foreach (var _ in rows)
                {
                    var line = Ui.Box("kv");
                    var key = Ui.Text(string.Empty, "kv__key");
                    var value = Ui.Text(string.Empty, "kv__value");
                    line.Add(key);
                    line.Add(value);
                    _rows.Add(line);
                    _rowLabels.Add((key, value));
                }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                Ui.SetText(_rowLabels[i].Key, rows[i].Key);
                Ui.SetText(_rowLabels[i].Value, rows[i].Value);
            }
        }

        private async void Send()
        {
            if (_sending) return;
            _sending = true;
            UiFeel.SetAvailable(_send, false);
            ShowStatus("Собираю отчёт…");
            ReportOutcome outcome;
            try
            {
                outcome = await _context.SendReport(progress =>
                    ShowStatus($"Отправляю… {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%"));
            }
            catch (Exception exception)
            {
                outcome = ReportOutcome.Failed(exception.Message);
            }
            _sending = false;
            UiFeel.SetAvailable(_send, true);
            ShowStatus(Describe(outcome));
            _status.EnableInClassList("t-good", outcome.Result == ReportResult.Sent);
            _status.EnableInClassList("t-bad", outcome.Result == ReportResult.Failed);
        }

        private void ShowStatus(string text)
        {
            Ui.Show(_status, !string.IsNullOrEmpty(text));
            Ui.SetText(_status, text);
            _status.RemoveFromClassList("t-good");
            _status.RemoveFromClassList("t-bad");
        }
    }
}
