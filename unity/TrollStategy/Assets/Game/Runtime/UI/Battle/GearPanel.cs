using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Step 2: the selected fighter with the stats its gear gives it, and the inventory. Gear goes only to
    /// a fighter on the board; until then the items and "take off the board" answer with a refusal.
    /// </summary>
    public sealed class GearPanel
    {
        private sealed class Item
        {
            public string ItemId;
            public Button Button;
            public Label Title;
            public Label Hint;
        }

        private readonly Label _details;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly List<Item> _items = new();
        private BattleDeployment _deployment;

        public GearPanel(VisualElement root)
        {
            _details = Ui.Require<Label>(root, "battle-selection");
            _list = Ui.Require<ScrollView>(root, "battle-gear-list");
            _empty = Ui.Require<Label>(root, "battle-gear-empty");
            Remove = UiFeel.Bind(Ui.Require<Button>(root, "battle-remove"), RemoveSelected, silentClick: true);
        }

        public Button Remove { get; }
        public string Details => _details.text;

        public Button ItemButton(string itemId) => _items.Find(item => item.ItemId == itemId)?.Button;

        public string HintOf(string itemId) => _items.Find(item => item.ItemId == itemId)?.Hint.text;

        public void Build(BattleDeployment deployment)
        {
            _deployment = deployment;
            _list.Clear();
            _items.Clear();
            foreach (var equipment in deployment.Equipment)
            {
                var button = Ui.StackButton("battle-card battle-card--gear", out var title, out var hint);
                string itemId = equipment.Id;
                UiFeel.Bind(button, () => Toggle(itemId), silentClick: true);
                _list.Add(button);
                _items.Add(new Item { ItemId = itemId, Button = button, Title = title, Hint = hint });
            }
            Ui.Show(_empty, _items.Count == 0);
            Refresh();
        }

        public void Refresh()
        {
            if (_deployment == null) return;
            string selected = _deployment.SelectedUnitId;
            bool placed = _deployment.SelectedIsPlaced;
            UiFeel.SetAvailable(Remove, placed);
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var equipment = _deployment.Equipment[i];
                string owner = _deployment.OwnerOf(item.ItemId);
                UiFeel.SetAvailable(item.Button, placed);
                item.Button.EnableInClassList("is-on", owner != null && owner == selected);
                var bonus = new List<string>();
                if (equipment.DamageBonus != 0) bonus.Add($"{equipment.DamageBonus:+0;-0;0} урон");
                if (equipment.ArmorBonus != 0) bonus.Add($"{equipment.ArmorBonus:+0;-0;0} броня");
                Ui.SetText(item.Title, bonus.Count > 0 ? $"{equipment.DisplayName} · {string.Join(", ", bonus)}" : equipment.DisplayName);
                Ui.SetText(item.Hint, owner == null ? "В инвентаре"
                    : owner == selected ? "Надето · нажми, чтобы снять"
                    : $"У {_deployment.UnitName(owner)} · нажми, чтобы передать");
            }
            Ui.SetText(_details, DescribeSelection(selected, placed));
        }

        private string DescribeSelection(string unitId, bool placed)
        {
            if (unitId == null)
                return "Выбери бойца слева, затем синюю клетку.\nСиние клетки — твои; красные — враг; серые — преграды.";
            var definition = _deployment.DefinitionOf(unitId);
            var (damage, armor) = _deployment.GearedStats(unitId);
            var cell = _deployment.CellOf(unitId);
            return $"{_deployment.UnitName(unitId)} · {(cell.HasValue ? "На поле: " + BattleDeployment.CellName(cell.Value) : "Выбери синюю клетку")}\n" +
                $"{definition.CombatHealth} HP · {damage} урон · {armor} броня · дальность {definition.AttackRange}\n" +
                (placed ? "Выдай снаряжение ниже · при старте уйдёт с работы" : "Сначала поставь на поле, чтобы выдать снаряжение");
        }

        private void RemoveSelected()
        {
            if (_deployment != null && _deployment.RemoveSelected()) GameAudio.Play(Sfx.UiBack);
        }

        private void Toggle(string itemId)
        {
            var change = _deployment?.ToggleEquipment(itemId) ?? EquipmentChange.None;
            if (change == EquipmentChange.Equipped) GameAudio.Play(Sfx.Equip);
            else if (change == EquipmentChange.Unequipped) GameAudio.Play(Sfx.Unequip);
        }
    }
}
