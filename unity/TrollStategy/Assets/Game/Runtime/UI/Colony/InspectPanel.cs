using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Card for the building or creature the player inspects: what it does, what it holds, why it idles,
    /// who works and carries there (each can be taken off the job), and what can be done with it. Hidden while
    /// a building is being moved so the map stays clear.
    /// </summary>
    public sealed class InspectPanel
    {
        // the barracks shows the most: move, upgrade and two hires
        private const int ActionCount = 4;

        private sealed class Row
        {
            public VisualElement Root;
            public Label Key;
            public Label Value;
        }

        private sealed class Slot
        {
            public VisualElement Root;
            public Image Icon;
            public Label Count;
        }

        private sealed class ActionButton
        {
            public Button Button;
            public Label Title;
            public Label Hint;
        }

        private sealed class UpgradeRow
        {
            public VisualElement Root;
            public Label Title;
            public Label Info;
            public Button Buy;
            public string Id;
            public string Description;
        }

        // what one level gives, short; the whole description is in the hint
        private static string EffectText(UpgradeSnapshot upgrade)
        {
            int n = upgrade.AmountPerLevel;
            switch (upgrade.Effect)
            {
                case UpgradeEffect.WalkSpeedPercent: return $"+{n}% к шагу за уровень";
                case UpgradeEffect.CarryPercent: return $"+{n}% к грузу за уровень";
                case UpgradeEffect.HandlingTimePercent: return $"−{n}% к погрузке за уровень";
                case UpgradeEffect.LoadersPerDoor: return $"+{n} место у двери за уровень";
                case UpgradeEffect.SquadSize: return $"+{n} боец в отряде за уровень";
                case UpgradeEffect.FighterHealthPercent: return $"+{n}% здоровья бойцов за уровень";
                case UpgradeEffect.FighterDamage: return $"+{n} к урону бойцов за уровень";
                case UpgradeEffect.BattleCooldownPercent: return $"−{n}% к отдыху арены за уровень";
                case UpgradeEffect.BattleRewardPercent: return $"+{n}% к награде арены за уровень";
                default: return upgrade.Description;
            }
        }

        private readonly ColonyHudContext _context;
        private readonly VisualElement _panel;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly VisualElement _rows;
        private readonly Label _note;
        private readonly Label _slotsHeader;
        private readonly VisualElement _slots;
        private readonly List<Row> _rowPool = new();
        private readonly List<Slot> _slotPool = new();
        private readonly VisualElement _upgrades;
        private readonly List<UpgradeRow> _upgradePool = new();
        private readonly StaffList _staff;
        private readonly ActionButton[] _actions = new ActionButton[ActionCount];
        private readonly VisualElement _actionRow;
        private int _rowCount;
        private int _actionCount;
        private string _shownId;

        private readonly HudTooltip _tooltip;

        public InspectPanel(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _tooltip = tooltip;
            _panel = Ui.Require<VisualElement>(root, "inspect");
            _title = Ui.Require<Label>(root, "inspect-title");
            _subtitle = Ui.Require<Label>(root, "inspect-subtitle");
            _rows = Ui.Require<VisualElement>(root, "inspect-rows");
            _note = Ui.Require<Label>(root, "inspect-note");
            _slotsHeader = Ui.Require<Label>(root, "inspect-slots-header");
            _slots = Ui.Require<VisualElement>(root, "inspect-slots");
            // the colony upgrades of the guild, the barracks and the armory, one row each, under the facts
            _upgrades = Ui.Box("upgrades");
            _rows.parent.Insert(_rows.parent.IndexOf(_rows) + 1, _upgrades);
            var staffScroll = Ui.Require<ScrollView>(root, "inspect-staff-scroll");
            staffScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            staffScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _staff = new StaffList(Ui.Require<VisualElement>(root, "inspect-staff"), context);
            UiFeel.Bind(Ui.Require<Button>(root, "inspect-close"), () => context.Interaction.CloseInspect(), Sfx.UiBack);

            var actions = _actionRow = Ui.Require<VisualElement>(root, "inspect-actions");
            for (int i = 0; i < ActionCount; i++)
            {
                var button = Ui.StackButton("action", out var title, out var hint);
                UiFeel.Bind(button, null);
                actions.Add(button);
                _actions[i] = new ActionButton { Button = button, Title = title, Hint = hint };
            }
            Hide();
        }

        public bool IsShown => Ui.IsShown(_panel);
        public string Title => _title.text;
        public StaffList Staff => _staff;

        /// <summary>The buy buttons of the shown upgrades, top to bottom.</summary>
        public IReadOnlyList<Button> UpgradeButtons
        {
            get
            {
                var visible = new List<Button>();
                foreach (var row in _upgradePool)
                    if (Ui.IsShown(row.Root)) visible.Add(row.Buy);
                return visible;
            }
        }

        /// <summary>The line under each shown upgrade: what a level gives, or what opens the next one.</summary>
        public IReadOnlyList<string> UpgradeInfos
        {
            get
            {
                var visible = new List<string>();
                foreach (var row in _upgradePool)
                    if (Ui.IsShown(row.Root)) visible.Add(row.Info.text);
                return visible;
            }
        }

        public string Note => _note.text;

        /// <summary>The visible action buttons, left to right.</summary>
        public IReadOnlyList<Button> Actions
        {
            get
            {
                var visible = new List<Button>(ActionCount);
                foreach (var action in _actions)
                    if (Ui.IsShown(action.Button)) visible.Add(action.Button);
                return visible;
            }
        }

        public void Refresh(GameSnapshot snapshot)
        {
            var interaction = _context.Interaction;
            // the card would cover the map while the player picks a new place for the building
            if (interaction.Mode.Type == InteractionModeType.MovingBuilding)
            {
                Hide();
                return;
            }

            var building = FindBuilding(snapshot, interaction.InspectedBuildingId);
            if (building != null)
            {
                Show(building.Id);
                RenderBuilding(building, snapshot);
                return;
            }
            var unit = FindUnit(snapshot, interaction.InspectedUnitId);
            if (unit != null)
            {
                Show(unit.Id);
                RenderUnit(unit, snapshot);
                return;
            }
            Hide();
        }

        private void Show(string id)
        {
            bool fresh = !IsShown || _shownId != id;
            _shownId = id;
            Ui.Show(_panel, true);
            if (fresh) UiMotion.PopIn(_panel, .22f);
        }

        private void Hide()
        {
            _shownId = null;
            _staff.Hide();
            Ui.Show(_panel, false);
        }

        private void RenderBuilding(BuildingSnapshot building, GameSnapshot snapshot)
        {
            var catalog = _context.Catalog;
            var definition = catalog.GetBuilding(building.Kind);
            Ui.SetText(_title, building.Name);
            Ui.SetText(_subtitle, $"Уровень {building.Level}, {building.Width}×{building.Height}");

            BeginRows();
            if (building.RecipeText.Length > 0)
                AddRow("Рецепт", building.RecipeText);
            if (building.IsWorkplace)
                ProductionRow(building);
            if (building.Slots.Count == 0 && building.Stock.Count > 0)
                AddRow("Запас", $"{StockText(building)} (до {building.Capacity})");

            string note = null;
            switch (definition.StorageRole)
            {
                case StorageRole.Market:
                    AddRow("Продано товаров", snapshot.SoldGoods.ToString());
                    AddRow("Надбавка к цене", $"+{building.SaleBonus} зол. за ед.");
                    break;
                case StorageRole.Armory:
                    int inInventory = 0;
                    foreach (var item in snapshot.Equipment)
                        if (item.OwnerUnitId == null) inInventory++;
                    AddRow("В инвентаре", inInventory.ToString());
                    note = "Готовое снаряжение попадает в инвентарь отряда.";
                    break;
                case StorageRole.Stockpile:
                    note = "Хранит сырьё и полуфабрикаты от носильщиков.";
                    break;
            }
            if (building.Kind == BuildingKind.Barracks)
                note = "Здесь отдыхают свободные существа. Улучшения бараков делают сильнее отряд на арене.";
            if (building.Kind == BuildingKind.HaulersGuild)
                note = "Улучшения гильдии действуют на всех носильщиков колонии сразу.";
            // the deeper levels of the colony upgrades it hosts wait for this building's own level
            if (building.UpgradeCost >= 0 && HostsUpgrades(building.Kind, snapshot))
                note = (note != null ? note + "\n" : string.Empty) + "Новый уровень здания открывает следующие ступени его улучшений.";
            EndRows();
            SetNote(note);
            RenderUpgrades(building.Kind, snapshot);
            RenderSlots(building);
            _staff.Show(building, snapshot);

            BeginActions();
            var interaction = _context.Interaction;
            AddAction("Перенести", "указать на карте", "", true, interaction.BeginMoveInspectedBuilding);
            bool maxed = building.UpgradeCost < 0;
            // every building of the game has three levels; one without them would show no upgrade at all
            if (definition.MaxLevel > 1)
                AddAction("Улучшить", maxed ? "макс. уровень" : Ui.Gold(building.UpgradeCost), "btn--primary",
                    !maxed && snapshot.Gold >= building.UpgradeCost, interaction.UpgradeInspectedBuilding);
            if (building.Kind == BuildingKind.Barracks)
            {
                foreach (var unit in catalog.Units)
                {
                    if (unit == null || !unit.Hireable || _actionCount >= ActionCount) continue;
                    var kind = unit.Kind;
                    bool open = snapshot.Progress.IsUnitUnlocked(kind);
                    if (!open) continue;
                    int price = _context.Session.HirePrice(kind);
                    AddAction("Нанять: " + unit.DisplayName.ToLowerInvariant(), open ? Ui.Gold(price) : "закрыто",
                        "btn--primary", open && snapshot.Gold >= price, () => interaction.RecruitUnit(kind));
                }
            }
            else
            {
                bool removable = definition.Constructible;
                AddAction("Снести", removable ? "вернуть " + Ui.Gold(building.RefundGold) : "нельзя снести", "btn--danger",
                    removable, interaction.DemolishInspectedBuilding);
            }
            EndActions();
        }

        private void RenderUnit(UnitSnapshot unit, GameSnapshot snapshot)
        {
            var catalog = _context.Catalog;
            var definition = catalog.GetUnit(unit.UnitKind);
            string species = definition != null ? definition.DisplayName : unit.UnitKind.ToString();
            Ui.SetText(_title, unit.Name);
            Ui.SetText(_subtitle, string.IsNullOrEmpty(unit.Status)
                ? species
                : $"{species}, {char.ToLowerInvariant(unit.Status[0])}{unit.Status.Substring(1)}");

            // what the creature is good for is in its words below; of its stats only the load says something alone
            BeginRows();
            AddRow("Носит за ходку", UnitStatsText.Load(unit.CarryCapacity));
            if (definition != null && definition.CombatHealth > 0)
                AddRow("В бою", $"здоровье {definition.CombatHealth}, урон {definition.CombatDamage}, броня {definition.CombatArmor}");
            var gear = new List<string>();
            foreach (var item in snapshot.Equipment)
                if (item.OwnerUnitId == unit.Id) gear.Add(item.DisplayName);
            if (gear.Count > 0) AddRow("Снаряжение", string.Join(", ", gear));
            var assignment = unit.Assignment;
            if (assignment.Kind == AssignmentKind.Haul) AddRow("Возит", _context.Session.DescribeCargo(assignment));
            EndRows();
            _staff.Hide();
            HideUpgrades();

            var selected = _context.Interaction.SelectedIds;
            var note = new List<string>();
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Description)) note.Add(definition.Description.Trim());
            if (selected.Count > 1) note.Add($"Команды ниже получат все выбранные: {selected.Count}.");
            SetNote(note.Count > 0 ? string.Join("\n", note) : null);
            HideSlots();

            BeginActions();
            var interaction = _context.Interaction;
            int refund = SaleRefund(snapshot, selected);
            // a freed creature walks to the barracks; with none standing it would only stop where it is
            AddAction("В бараки", "отдыхать", "", HasBarracks(snapshot), interaction.ReleaseSelected, silent: true);
            AddAction(selected.Count > 1 ? $"Продать ×{selected.Count}" : "Продать", "+" + Ui.Gold(refund),
                "btn--danger", true, interaction.SellSelected, silent: true);
            EndActions();
        }

        /// <summary>Gold the selected creatures fetch when sold, by the session's own rule.</summary>
        private int SaleRefund(GameSnapshot snapshot, IReadOnlyCollection<string> selected)
        {
            int refund = 0;
            foreach (var unit in snapshot.Units)
            {
                if (!Contains(selected, unit.Id)) continue;
                var definition = _context.Catalog.GetUnit(unit.UnitKind);
                if (definition != null) refund += ColonySimulation.UnitSaleRefund(definition);
            }
            return refund;
        }

        private static bool HasBarracks(GameSnapshot snapshot)
        {
            foreach (var building in snapshot.Buildings)
                if (building.Kind == BuildingKind.Barracks) return true;
            return false;
        }

        private void ProductionRow(BuildingSnapshot building)
        {
            string text;
            string tone;
            switch (building.ProductionState)
            {
                case ProductionState.Working:
                    text = $"работает, цикл {1f / building.ProductionPerSecond:0.#} с";
                    tone = "t-good";
                    break;
                case ProductionState.NoWorkers:
                    text = "простой: нет рабочих";
                    tone = "t-warn";
                    break;
                case ProductionState.MissingInputs:
                    text = "простой: не хватает сырья";
                    tone = "t-warn";
                    break;
                case ProductionState.OutputFull:
                    text = "простой: выход заполнен";
                    tone = "t-warn";
                    break;
                default:
                    return;
            }
            AddRow("Состояние", text).AddToClassList(tone);
        }

        private static string StockText(BuildingSnapshot building)
        {
            var parts = new List<string>(building.Stock.Count);
            foreach (var stack in building.Stock) parts.Add($"{stack.Name} {stack.Amount}");
            return string.Join(", ", parts);
        }

        private void BeginRows() => _rowCount = 0;

        private Label AddRow(string key, string value)
        {
            if (_rowCount == _rowPool.Count)
            {
                var row = new Row { Root = Ui.Box("kv"), Key = Ui.Text(string.Empty, "kv__key"), Value = Ui.Text(string.Empty, "kv__value") };
                row.Root.Add(row.Key);
                row.Root.Add(row.Value);
                _rows.Add(row.Root);
                _rowPool.Add(row);
            }
            var entry = _rowPool[_rowCount++];
            Ui.Show(entry.Root, true);
            Ui.SetText(entry.Key, key);
            Ui.SetText(entry.Value, value);
            entry.Value.RemoveFromClassList("t-good");
            entry.Value.RemoveFromClassList("t-warn");
            return entry.Value;
        }

        private void EndRows()
        {
            for (int i = _rowCount; i < _rowPool.Count; i++) Ui.Show(_rowPool[i].Root, false);
        }

        private void SetNote(string note)
        {
            Ui.SetText(_note, note);
            Ui.Show(_note, !string.IsNullOrEmpty(note));
        }

        private void RenderSlots(BuildingSnapshot building)
        {
            int count = building.Slots.Count;
            if (count == 0)
            {
                HideSlots();
                return;
            }

            int used = 0;
            for (int i = 0; i < count; i++)
                if (!building.Slots[i].IsEmpty) used++;
            Ui.SetText(_slotsHeader, $"Инвентарь: слоты {used} из {count}, товары {building.TotalStock} из {building.Capacity}");
            Ui.Show(_slotsHeader, true);
            Ui.Show(_slots, true);

            while (_slotPool.Count < count) _slotPool.Add(CreateSlot());
            for (int i = 0; i < _slotPool.Count; i++)
            {
                var cell = _slotPool[i];
                Ui.Show(cell.Root, i < count);
                if (i >= count) continue;
                var slot = building.Slots[i];
                int amount = slot.Amount;
                var icon = amount > 0 ? _context.Catalog.TryGetResource(slot.Resource)?.Icon : null;
                if (cell.Icon.sprite != icon) cell.Icon.sprite = icon;
                Ui.Show(cell.Icon, icon != null);
                Ui.SetText(cell.Count, amount > 0 ? amount.ToString() : string.Empty);
                cell.Root.EnableInClassList("is-filled", amount > 0);
                cell.Root.EnableInClassList("is-full", amount > 0 && amount >= building.SlotStackSize);
            }
        }

        private void HideSlots()
        {
            Ui.Show(_slotsHeader, false);
            Ui.Show(_slots, false);
        }

        private Slot CreateSlot()
        {
            var root = Ui.Box("slot");
            var icon = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("slot__icon");
            var count = Ui.Text(string.Empty, "slot__count");
            count.pickingMode = PickingMode.Ignore;
            root.Add(icon);
            root.Add(count);
            _slots.Add(root);
            return new Slot { Root = root, Icon = icon, Count = count };
        }

        // One row per upgrade this building hosts: its level, what it does, and a button with the next price.
        private void RenderUpgrades(BuildingKind host, GameSnapshot snapshot)
        {
            int shown = 0;
            foreach (var upgrade in snapshot.Upgrades)
            {
                if (upgrade.Host != host) continue;
                if (shown == _upgradePool.Count) _upgradePool.Add(CreateUpgradeRow());
                var row = _upgradePool[shown++];
                row.Id = upgrade.Id;
                Ui.Show(row.Root, true);
                Ui.SetText(row.Title, $"{upgrade.Name}: {upgrade.Level} из {upgrade.MaxLevel}");
                // a level the building is still too small for says what opens it, in place of what it gives
                Ui.SetText(row.Info, upgrade.WaitsForHost
                    ? $"Дальше — когда здание станет {upgrade.NextHostLevel}-го уровня"
                    : EffectText(upgrade));
                row.Description = upgrade.Description;
                Ui.SetCaption(row.Buy, upgrade.IsMaxed ? "максимум" : Ui.Gold(upgrade.NextCost));
                UiFeel.SetAvailable(row.Buy, upgrade.IsOpen && snapshot.Gold >= upgrade.NextCost);
            }
            for (int i = shown; i < _upgradePool.Count; i++) Ui.Show(_upgradePool[i].Root, false);
            Ui.Show(_upgrades, shown > 0);
        }

        private static bool HostsUpgrades(BuildingKind kind, GameSnapshot snapshot)
        {
            foreach (var upgrade in snapshot.Upgrades)
                if (upgrade.Host == kind) return true;
            return false;
        }

        private void HideUpgrades()
        {
            foreach (var row in _upgradePool) Ui.Show(row.Root, false);
            Ui.Show(_upgrades, false);
        }

        private UpgradeRow CreateUpgradeRow()
        {
            var row = new UpgradeRow { Root = Ui.Box("upgrade") };
            var text = Ui.Box("upgrade__text");
            row.Title = Ui.Text(string.Empty, "upgrade__title t-bold");
            row.Info = Ui.Text(string.Empty, "upgrade__info");
            text.Add(row.Title);
            text.Add(row.Info);
            row.Buy = Ui.CaptionButton(string.Empty, null, "btn btn--primary upgrade__buy");
            UiFeel.Bind(row.Buy, () => { if (row.Id != null) _context.Interaction.BuyUpgrade(row.Id); });
            _tooltip?.Attach(row.Root, () => row.Title.text, () => row.Description);
            row.Root.Add(text);
            row.Root.Add(row.Buy);
            _upgrades.Add(row.Root);
            return row;
        }

        private void BeginActions() => _actionCount = 0;

        private void AddAction(string title, string hint, string style, bool available, Action action, bool silent = false)
        {
            if (_actionCount >= ActionCount) return;
            var entry = _actions[_actionCount++];
            Ui.SetText(entry.Title, title);
            Ui.SetText(entry.Hint, hint);
            entry.Button.EnableInClassList("btn--primary", style == "btn--primary");
            entry.Button.EnableInClassList("btn--danger", style == "btn--danger");
            UiFeel.Bind(entry.Button, action, silentClick: silent);
            UiFeel.SetAvailable(entry.Button, available);
            Ui.Show(entry.Button, true);
        }

        private void EndActions()
        {
            for (int i = _actionCount; i < ActionCount; i++) Ui.Show(_actions[i].Button, false);
            // four in a row would break their words; four go two by two
            _actionRow.EnableInClassList("actions--grid", _actionCount > 3);
        }

        private static BuildingSnapshot FindBuilding(GameSnapshot snapshot, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var building in snapshot.Buildings)
                if (building.Id == id) return building;
            return null;
        }

        private static UnitSnapshot FindUnit(GameSnapshot snapshot, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var unit in snapshot.Units)
                if (unit.Id == id) return unit;
            return null;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var candidate in ids)
                if (candidate == id) return true;
            return false;
        }
    }
}
