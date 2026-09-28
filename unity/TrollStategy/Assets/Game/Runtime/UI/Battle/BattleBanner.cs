using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Big centred word that pops in for a moment: the battle starts, or ends.</summary>
    public sealed class BattleBanner
    {
        private static readonly string[] Tones = { "is-victory", "is-defeat", "is-draw" };

        private readonly VisualElement _panel;
        private readonly Label _text;
        private float _hideAt = -1f;

        public BattleBanner(VisualElement root)
        {
            _panel = Ui.Require<VisualElement>(root, "battle-banner");
            _text = Ui.Require<Label>(root, "battle-banner-text");
            Hide();
        }

        public bool IsShown => Ui.IsShown(_panel);
        public string Text => _text.text;

        /// <param name="tone">is-victory, is-defeat, is-draw, or null for the plain ink colour.</param>
        public void Show(string text, string tone, float seconds)
        {
            Ui.SetText(_text, text);
            foreach (var name in Tones) _panel.EnableInClassList(name, name == tone);
            Ui.Show(_panel, true);
            _hideAt = Time.unscaledTime + seconds;
            UiMotion.PopIn(_panel, .38f);
        }

        public void Tick()
        {
            if (_hideAt < 0f || Time.unscaledTime < _hideAt) return;
            Hide();
        }

        public void Hide()
        {
            _hideAt = -1f;
            Ui.Show(_panel, false);
        }
    }
}
