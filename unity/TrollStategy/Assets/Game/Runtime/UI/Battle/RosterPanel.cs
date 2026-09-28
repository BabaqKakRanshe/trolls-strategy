using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Step 1: every fighter of the colony. A press selects it; its card says where it stands.</summary>
    public sealed class RosterPanel
    {
        private sealed class Card
        {
            public string UnitId;
            public Button Button;
            public Label Title;
            public Label Hint;
        }

        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly List<Card> _cards = new();
        private BattleDeployment _deployment;

        public RosterPanel(VisualElement root)
        {
            _list = Ui.Require<ScrollView>(root, "battle-roster-list");
            _empty = Ui.Require<Label>(root, "battle-roster-empty");
        }

        public int Count => _cards.Count;

        public Button CardOf(string unitId) => _cards.Find(card => card.UnitId == unitId)?.Button;

        public string HintOf(string unitId) => _cards.Find(card => card.UnitId == unitId)?.Hint.text;

        public void Build(BattleDeployment deployment)
        {
            _deployment = deployment;
            _list.Clear();
            _cards.Clear();
            foreach (var unit in deployment.Roster)
            {
                var button = Ui.StackButton("battle-card", out var title, out var hint);
                string unitId = unit.Id;
                UiFeel.Bind(button, () =>
                {
                    if (_deployment.Select(unitId)) GameAudio.Play(Sfx.Select);
                }, silentClick: true);
                _list.Add(button);
                _cards.Add(new Card { UnitId = unitId, Button = button, Title = title, Hint = hint });
            }
            Ui.Show(_empty, _cards.Count == 0);
            Refresh();
        }

        public void Refresh()
        {
            if (_deployment == null) return;
            foreach (var card in _cards)
            {
                var definition = _deployment.DefinitionOf(card.UnitId);
                var cell = _deployment.CellOf(card.UnitId);
                card.Button.EnableInClassList("is-on", card.UnitId == _deployment.SelectedUnitId);
                card.Button.EnableInClassList("is-placed", cell.HasValue);
                Ui.SetText(card.Title, _deployment.UnitName(card.UnitId));
                Ui.SetText(card.Hint, $"{definition.CombatHealth} HP · {definition.CombatDamage} урон · " +
                    (cell.HasValue ? $"на поле {BattleDeployment.CellName(cell.Value)}" : "в резерве"));
            }
        }
    }
}
