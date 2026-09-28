using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// During the replay: time, speed and who still stands, with pause and the speed buttons. After the
    /// verdict the controls give way to the button back to the colony.
    /// </summary>
    public sealed class ReplayBar
    {
        private static readonly float[] SpeedValues = { 1f, 2f, 4f };

        private readonly VisualElement _panel;
        private readonly Label _status;
        private readonly Button[] _speeds = new Button[SpeedValues.Length];
        // what the status line shows now; the text is rebuilt only when one of these changes
        private (bool Paused, float Speed, int Tenths, int Players, int Enemies) _shown = (false, -1f, -1, -1, -1);

        public ReplayBar(VisualElement root, Action pause, Action<float> speed, Action close)
        {
            _panel = Ui.Require<VisualElement>(root, "battle-replay");
            _status = Ui.Require<Label>(root, "battle-replay-status");
            Pause = UiFeel.Bind(Ui.Require<Button>(root, "battle-pause"), pause, silentClick: true);
            for (int i = 0; i < SpeedValues.Length; i++)
            {
                float value = SpeedValues[i];
                _speeds[i] = UiFeel.Bind(Ui.Require<Button>(root, $"battle-speed-{value:0}"), () => speed?.Invoke(value));
            }
            Return = UiFeel.Bind(Ui.Require<Button>(root, "battle-return"), close);
            Hide();
        }

        public Button Pause { get; }
        public Button Return { get; }
        public bool IsShown => Ui.IsShown(_panel);
        public string Status => _status.text;

        public Button SpeedButton(float speed) => _speeds[Array.IndexOf(SpeedValues, speed)];

        public void Hide() => Ui.Show(_panel, false);

        public void Begin()
        {
            _shown = (false, -1f, -1, -1, -1);
            Ui.Show(_panel, true);
            Ui.Show(Pause, true);
            foreach (var button in _speeds) Ui.Show(button, true);
            Ui.Show(Return, false);
        }

        public void Show(bool paused, float speed, float seconds, int players, int enemies)
        {
            var shown = (paused, speed, Mathf.FloorToInt(seconds * 10f), players, enemies);
            if (shown == _shown) return;
            _shown = shown;
            Ui.SetText(_status, $"{(paused ? "ПАУЗА" : "БОЙ")} · {shown.Item3 / 10f:0.0} с · {speed:0}×\n" +
                $"Твой отряд: {players} в строю     ·     Враги: {enemies} в строю");
            Ui.SetCaption(Pause, paused ? "ПРОДОЛЖИТЬ" : "ПАУЗА");
            Pause.EnableInClassList("is-on", paused);
            for (int i = 0; i < _speeds.Length; i++)
                _speeds[i].EnableInClassList("is-on", Mathf.Approximately(SpeedValues[i], speed));
        }

        public void ShowResult(string text)
        {
            Ui.SetText(_status, text);
            Ui.Show(Pause, false);
            foreach (var button in _speeds) Ui.Show(button, false);
            Ui.Show(Return, true);
        }
    }
}
