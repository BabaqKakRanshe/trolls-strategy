using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The selected fighter's row over the squad: health, damage and armour with its gear on, the inventory
    /// as round buttons (lit on the fighter who wears them, faint on another) and the way back to the
    /// reserve. Hidden while no fighter is selected; names, bonuses and owners are in the hints.
    /// </summary>
    public sealed class GearPanel
    {
        private sealed class Item
        {
            public EquipmentSnapshot Equipment;
            public Button Button;
        }

        private readonly VisualElement _panel;
        private readonly Label _health;
        private readonly Label _damage;
        private readonly Label _armor;
        private readonly VisualElement _list;
        private readonly HudTooltip _tooltip;
        private readonly List<Item> _items = new();
        private BattleDeployment _deployment;

        public GearPanel(VisualElement root, HudTooltip tooltip = null)
        {
            _panel = Ui.Require<VisualElement>(root, "battle-gear");
            _health = Ui.Require<Label>(root, "battle-health-value");
            _damage = Ui.Require<Label>(root, "battle-damage-value");
            _armor = Ui.Require<Label>(root, "battle-armor-value");
            _list = Ui.Require<VisualElement>(root, "battle-items");
            _tooltip = tooltip;
            Remove = UiFeel.Bind(Ui.Require<Button>(root, "battle-remove"), RemoveSelected, silentClick: true);
            if (tooltip == null) return;
            tooltip.Attach(Remove, () => "В резерв",
                () => "Убрать бойца с поля, его снаряжение вернётся. Можно и унести бойца за край поля.");
            tooltip.Attach(Ui.Require<VisualElement>(root, "battle-health"), () => "Здоровье", null);
            tooltip.Attach(Ui.Require<VisualElement>(root, "battle-damage"), () => "Урон", () => GearShare(true));
            tooltip.Attach(Ui.Require<VisualElement>(root, "battle-armor"), () => "Броня", () => GearShare(false));
        }

        public bool IsShown => Ui.IsShown(_panel);
        public Button Remove { get; }
        public string Health => _health.text;
        public string Damage => _damage.text;
        public string Armor => _armor.text;

        public Button ItemButton(string itemId) => _items.Find(item => item.Equipment.Id == itemId)?.Button;

        public string HintOf(string itemId)
        {
            var item = _items.Find(candidate => candidate.Equipment.Id == itemId);
            return item != null ? ItemHint(item.Equipment) : null;
        }

        public void Build(BattleDeployment deployment)
        {
            _deployment = deployment;
            _list.Clear();
            _items.Clear();
            foreach (var equipment in deployment.Equipment)
            {
                var button = Ui.TextButton(string.Empty, "btn btn-disc battle-item");
                button.Add(Ui.Art(IconOf(deployment, equipment.DefinitionId), equipment.DisplayName, "battle-item__art"));
                var item = new Item { Equipment = equipment, Button = button };
                UiFeel.Bind(button, () => Toggle(item.Equipment.Id), silentClick: true);
                _tooltip?.Attach(button, () => item.Equipment.DisplayName, () => ItemHint(item.Equipment));
                _list.Add(button);
                _items.Add(item);
            }
            Ui.Show(_list, _items.Count > 0);
            Refresh();
        }

        public void Refresh()
        {
            if (_deployment == null) return;
            string selected = _deployment.SelectedUnitId;
            Ui.Show(_panel, selected != null);
            if (selected == null) return;
            var (damage, armor) = _deployment.GearedStats(selected);
            Ui.SetText(_health, _deployment.DefinitionOf(selected).CombatHealth.ToString());
            Ui.SetText(_damage, damage.ToString());
            Ui.SetText(_armor, armor.ToString());
            foreach (var item in _items)
            {
                string owner = _deployment.OwnerOf(item.Equipment.Id);
                item.Button.EnableInClassList("is-on", owner == selected);
                item.Button.EnableInClassList("is-taken", owner != null && owner != selected);
            }
        }

        private string ItemHint(EquipmentSnapshot equipment)
        {
            if (_deployment == null) return null;
            var bonus = new List<string>();
            if (equipment.DamageBonus != 0) bonus.Add($"урон {equipment.DamageBonus:+0;-0;0}");
            if (equipment.ArmorBonus != 0) bonus.Add($"броня {equipment.ArmorBonus:+0;-0;0}");
            string owner = _deployment.OwnerOf(equipment.Id);
            string where = owner == null ? "Лежит в инвентаре, нажми, чтобы надеть."
                : owner == _deployment.SelectedUnitId ? "Надето, нажми, чтобы снять."
                : $"Сейчас на бойце {_deployment.UnitName(owner)}, нажми, чтобы передать.";
            return bonus.Count > 0 ? $"{string.Join(", ", bonus)}. {where}" : where;
        }

        // how much of the selected fighter's damage or armour its gear gives
        private string GearShare(bool damage)
        {
            string selected = _deployment?.SelectedUnitId;
            if (selected == null) return null;
            var definition = _deployment.DefinitionOf(selected);
            var geared = _deployment.GearedStats(selected);
            int own = damage ? definition.CombatDamage : definition.CombatArmor;
            int bonus = (damage ? geared.Damage : geared.Armor) - own;
            return bonus == 0 ? null : $"{(damage ? "Свой" : "Своя")} {own}, от снаряжения {bonus:+0;-0;0}.";
        }

        private static UnityEngine.Sprite IconOf(BattleDeployment deployment, string definitionId)
        {
            foreach (EquipmentDefinition definition in deployment.Session.Catalog.Equipment)
                if (definition != null && definition.ItemId == definitionId) return definition.Icon;
            return null;
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
