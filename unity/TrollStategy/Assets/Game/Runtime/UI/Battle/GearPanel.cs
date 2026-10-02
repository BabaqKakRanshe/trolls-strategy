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
        // one button per kind of item: thirty swords are one sword with "×30"
        private sealed class Item
        {
            public string DefinitionId;
            public string Name;
            public readonly List<EquipmentSnapshot> Items = new();
            public Button Button;
            public Label Count;
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

        public Button ItemButton(string itemId) => GroupOf(itemId)?.Button;

        /// <summary>How many buttons the inventory row has: one per kind of item.</summary>
        public int ButtonCount => _items.Count;

        public string HintOf(string itemId)
        {
            var group = GroupOf(itemId);
            return group != null ? ItemHint(group) : null;
        }

        private Item GroupOf(string itemId) => _items.Find(group => group.Items.Exists(item => item.Id == itemId));

        public void Build(BattleDeployment deployment)
        {
            _deployment = deployment;
            _list.Clear();
            _items.Clear();
            foreach (var equipment in deployment.Equipment)
            {
                var item = _items.Find(group => group.DefinitionId == equipment.DefinitionId);
                if (item == null)
                {
                    var button = Ui.TextButton(string.Empty, "btn btn-disc battle-item");
                    button.Add(Ui.Art(IconOf(deployment, equipment.DefinitionId), equipment.DisplayName, "battle-item__art"));
                    var count = Ui.Text(string.Empty, "battle-item__count t-black");
                    count.pickingMode = PickingMode.Ignore;
                    button.Add(count);
                    item = new Item { DefinitionId = equipment.DefinitionId, Name = equipment.DisplayName, Button = button, Count = count };
                    var group = item;
                    UiFeel.Bind(button, () => Toggle(group), silentClick: true);
                    _tooltip?.Attach(button, () => group.Name, () => ItemHint(group));
                    _list.Add(button);
                    _items.Add(item);
                }
                item.Items.Add(equipment);
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
                bool worn = false;
                int free = 0;
                foreach (var equipment in item.Items)
                {
                    string owner = _deployment.OwnerOf(equipment.Id);
                    if (owner == selected) worn = true;
                    else if (owner == null) free++;
                }
                item.Button.EnableInClassList("is-on", worn);
                item.Button.EnableInClassList("is-taken", !worn && free == 0);
                Ui.SetText(item.Count, item.Items.Count > 1 ? "×" + free : string.Empty);
            }
        }

        private string ItemHint(Item group)
        {
            if (_deployment == null || group.Items.Count == 0) return null;
            var equipment = group.Items[0];
            var bonus = new List<string>();
            if (equipment.DamageBonus != 0) bonus.Add($"урон {equipment.DamageBonus:+0;-0;0}");
            if (equipment.ArmorBonus != 0) bonus.Add($"броня {equipment.ArmorBonus:+0;-0;0}");
            string selected = _deployment.SelectedUnitId;
            int free = 0;
            string worn = null, other = null;
            foreach (var item in group.Items)
            {
                string owner = _deployment.OwnerOf(item.Id);
                if (owner == null) free++;
                else if (owner == selected) worn = item.Id;
                else other ??= owner;
            }
            string where = worn != null ? "Надето, нажми, чтобы снять."
                : free > 0 ? (group.Items.Count > 1 ? $"Свободно {free} из {group.Items.Count}, нажми, чтобы надеть." : "Лежит в инвентаре, нажми, чтобы надеть.")
                : $"Сейчас на бойце {_deployment.UnitName(other)}, нажми, чтобы передать.";
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
            if (bonus == 0) return null;
            return damage
                ? $"Свой урон {own}, от снаряжения {bonus:+0;-0;0}."
                : $"Своя броня {own}, от снаряжения {bonus:+0;-0;0}.";
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

        // the selected fighter takes off the one it wears, or takes a free one, or one from another fighter
        private void Toggle(Item group)
        {
            if (_deployment == null || group.Items.Count == 0) return;
            string selected = _deployment.SelectedUnitId;
            EquipmentSnapshot pick = group.Items.Find(item => _deployment.OwnerOf(item.Id) == selected && selected != null) ??
                                     group.Items.Find(item => _deployment.OwnerOf(item.Id) == null) ??
                                     group.Items[0];
            Toggle(pick.Id);
        }

        private void Toggle(string itemId)
        {
            var change = _deployment?.ToggleEquipment(itemId) ?? EquipmentChange.None;
            if (change == EquipmentChange.Equipped) GameAudio.Play(Sfx.Equip);
            else if (change == EquipmentChange.Unequipped) GameAudio.Play(Sfx.Unequip);
        }
    }
}
