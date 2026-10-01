using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The latest word from the colony (built, hired, refused…) at the bottom left. It fades a few seconds
    /// after the last change; while a mode is active the context bar carries the message instead.
    /// </summary>
    public sealed class StatusLine
    {
        private const float VisibleSeconds = 6f;
        private const float FirstVisibleSeconds = 14f;
        private static readonly Color RefusalColor = new(.72f, .21f, .14f, 1f);

        private readonly VisualElement _panel;
        private readonly Label _text;
        private float _fadeAt = -1f;

        public StatusLine(VisualElement root)
        {
            _panel = Ui.Require<VisualElement>(root, "status");
            _text = Ui.Require<Label>(root, "status-text");
            _panel.pickingMode = PickingMode.Ignore;
            _text.pickingMode = PickingMode.Ignore;
        }

        public bool IsShown => Ui.IsShown(_panel) && !_panel.ClassListContains("is-faded");
        public string Text => _text.text;

        /// <summary>Shows the controller's message; the first one (the colony's opening goal) stays longer.</summary>
        public void Show(InteractionController interaction, bool first = false)
        {
            string message = interaction.Message;
            if (interaction.Mode.Type != InteractionModeType.Neutral || string.IsNullOrWhiteSpace(message))
            {
                Ui.Show(_panel, false);
                _fadeAt = -1f;
                return;
            }
            Ui.SetText(_text, message);
            Ui.Show(_panel, true);
            _panel.RemoveFromClassList("is-faded");
            _fadeAt = Time.unscaledTime + (first ? FirstVisibleSeconds : VisibleSeconds);
        }

        public void Tick()
        {
            if (_fadeAt < 0f || Time.unscaledTime < _fadeAt) return;
            _fadeAt = -1f;
            _panel.AddToClassList("is-faded");
        }

        /// <summary>"No": the line wobbles and the reason flashes red.</summary>
        public void PlayRefusal()
        {
            if (!Ui.IsShown(_panel)) return;
            _panel.RemoveFromClassList("is-faded");
            _fadeAt = Time.unscaledTime + VisibleSeconds;
            UiMotion.Nudge(_panel, 10f);
            UiMotion.Flash(_text, RefusalColor, .6f);
        }
    }
}
