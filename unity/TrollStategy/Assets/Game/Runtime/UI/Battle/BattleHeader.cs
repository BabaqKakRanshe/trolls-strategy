using System;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Mission name, goal and the step the player is on, with the way back to the colony.</summary>
    public sealed class BattleHeader
    {
        private readonly Label _title;
        private readonly Label _goal;
        private readonly Label _guide;

        public BattleHeader(VisualElement root, Action back)
        {
            _title = Ui.Require<Label>(root, "battle-title");
            _goal = Ui.Require<Label>(root, "battle-goal");
            _guide = Ui.Require<Label>(root, "battle-guide");
            Back = UiFeel.Bind(Ui.Require<Button>(root, "battle-back"), back, Sfx.UiBack);
        }

        public Button Back { get; }
        public string Title => _title.text;
        public string Goal => _goal.text;
        public string Guide => _guide.text;

        public void Show(string title, string goal, string guide)
        {
            Ui.SetText(_title, title);
            Ui.SetText(_goal, goal);
            Ui.SetText(_guide, guide);
        }
    }
}
