using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The catalog drawer: creatures to hire (in groups) and every constructible building, all read from
    /// the content catalog. Buying only starts placement on the map; the session decides the rest. In a
    /// campaign, closed creatures show when they open, and of the closed buildings only the next one is shown.
    /// </summary>
    public sealed class CatalogPanel
    {
        public const int MaxHireAmount = 20;

        private sealed class UnitCard
        {
            public UnitDefinition Definition;
            public VisualElement Root;
            public Button Buy;
            public Label BuyTitle;
            public Label BuyPrice;
            public Label Lock;
        }

        private sealed class BuildingRow
        {
            public BuildingDefinition Definition;
            public VisualElement Root;
            public Button Buy;
            public Label BuyTitle;
            public Label BuyPrice;
            public Label Lock;
        }

        private readonly ColonyHudContext _context;
        private readonly ShowcasePanel _showcase;
        private readonly VisualElement _panel;
        private readonly VisualElement _unitsPage;
        private readonly ScrollView _buildingsPage;
        private readonly Button _unitsTab;
        private readonly Button _buildingsTab;
        private readonly Label _hireAmountLabel;
        private readonly List<UnitCard> _units = new();
        private readonly List<BuildingRow> _buildings = new();
        private int _hireAmount = 1;
        private int _gold;
        private bool _hasMine;
        private bool _hasUnits;
        private ProgressSnapshot _progress = ProgressSnapshot.Sandbox;
        private QuestFocus _focus = QuestFocus.None;
        private string _focusQuestId;

        public event Action<bool> OpenChanged;

        public CatalogPanel(VisualElement root, ColonyHudContext context, ShowcasePanel showcase)
        {
            _context = context;
            _showcase = showcase;
            _panel = Ui.Require<VisualElement>(root, "catalog");
            UiFeel.Bind(Ui.Require<Button>(root, "catalog-close"), () => SetOpen(false), Sfx.UiBack);
            _unitsTab = UiFeel.Bind(Ui.Require<Button>(root, "tab-units"), () => ShowUnits(true));
            _buildingsTab = UiFeel.Bind(Ui.Require<Button>(root, "tab-buildings"), () => ShowUnits(false));
            _unitsPage = Ui.Require<VisualElement>(root, "units-page");
            _buildingsPage = Ui.Require<ScrollView>(root, "buildings-page");
            _buildingsPage.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _buildingsPage.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _hireAmountLabel = Ui.Require<Label>(root, "hire-amount");
            UiFeel.Bind(Ui.Require<Button>(root, "hire-less"), () => SetHireAmount(_hireAmount - 1));
            UiFeel.Bind(Ui.Require<Button>(root, "hire-more"), () => SetHireAmount(_hireAmount + 1));

            var cards = Ui.Require<VisualElement>(root, "unit-cards");
            foreach (var unit in context.Catalog.Units)
                if (unit != null) _units.Add(CreateUnitCard(cards, unit));
            foreach (var building in context.Catalog.Buildings)
                if (building != null && building.Constructible) _buildings.Add(CreateBuildingRow(building));

            // a sandbox colony starts with a mine; a campaign starts by hiring its first creature
            ShowUnits(context.Session.IsCampaign);
            SetOpen(true);
        }

        public bool IsOpen { get; private set; }
        public bool ShowsUnits => Ui.IsShown(_unitsPage);
        public int HireAmount => _hireAmount;
        public int BuildingCount => _buildings.Count;

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            IsOpen = open;
            _panel.EnableInClassList("is-closed", !open);
            if (!open) _showcase.Hide();
            OpenChanged?.Invoke(open);
        }

        public void SetHireAmount(int amount)
        {
            _hireAmount = Mathf.Clamp(amount, 1, MaxHireAmount);
            Ui.SetText(_hireAmountLabel, _hireAmount.ToString());
            RefreshPrices();
        }

        /// <summary>
        /// Points at what the current quest needs. When the quest changes the drawer turns to the page it
        /// needs, once; after that the player's own tab choice stands.
        /// </summary>
        public void SetFocus(QuestFocus focus, string questId)
        {
            _focus = focus ?? QuestFocus.None;
            if (questId == _focusQuestId) return;
            _focusQuestId = questId;
            if (_focus.Hire != null) ShowUnits(true);
            else if (_focus.Build != null) ShowUnits(false);
        }

        public void Refresh(GameSnapshot snapshot)
        {
            _gold = snapshot.Gold;
            _progress = snapshot.Progress;
            _hasUnits = snapshot.Units.Count > 0;
            _hasMine = false;
            foreach (var building in snapshot.Buildings)
                if (building.Kind == BuildingKind.Mine) _hasMine = true;
            RefreshPrices();
        }

        /// <summary>The buy button of a building's row, or of a creature's card.</summary>
        public Button BuyButton(BuildingKind kind) => _buildings.Find(row => row.Definition.Kind == kind)?.Buy;
        public Button HireButton(UnitKind kind) => _units.Find(card => card.Definition.Kind == kind)?.Buy;

        public bool IsListed(BuildingKind kind)
        {
            var row = _buildings.Find(candidate => candidate.Definition.Kind == kind);
            return row != null && Ui.IsShown(row.Root);
        }

        public bool IsSuggested(BuildingKind kind) =>
            _buildings.Find(row => row.Definition.Kind == kind)?.Root.ClassListContains("is-suggested") == true;

        public bool IsSuggested(UnitKind kind) =>
            _units.Find(card => card.Definition.Kind == kind)?.Root.ClassListContains("is-suggested") == true;

        private void RefreshPrices()
        {
            bool campaign = _progress.Enabled;
            foreach (var card in _units)
            {
                var kind = card.Definition.Kind;
                bool locked = !_progress.IsUnitUnlocked(kind);
                int total = card.Definition.Price * _hireAmount;
                card.Root.EnableInClassList("is-locked", locked);
                SetLock(card.Lock, locked, _progress.UnlockLevel(kind));
                Ui.SetText(card.BuyTitle, locked ? "Закрыто" : _hireAmount > 1 ? $"Нанять ×{_hireAmount}" : "Нанять");
                Ui.SetText(card.BuyPrice, Ui.Gold(total));
                UiFeel.SetAvailable(card.Buy, !locked && _gold >= total);
                // a campaign points at what the quest asks; a sandbox, once the mine stands, at hands to work it
                bool suggested = campaign ? _focus.Hire == kind : _hasMine && !_hasUnits;
                card.Root.EnableInClassList("is-suggested", suggested && !locked);
            }

            int nextLevel = int.MaxValue;
            foreach (var row in _buildings)
            {
                int level = _progress.UnlockLevel(row.Definition.Kind);
                if (!_progress.IsBuildingUnlocked(row.Definition.Kind) && level > 0) nextLevel = Math.Min(nextLevel, level);
            }
            foreach (var row in _buildings)
            {
                var kind = row.Definition.Kind;
                bool locked = !_progress.IsBuildingUnlocked(kind);
                int level = _progress.UnlockLevel(kind);
                // closed buildings stay out of the list, except the next one to open
                Ui.Show(row.Root, !locked || level == nextLevel);
                row.Root.EnableInClassList("is-locked", locked);
                SetLock(row.Lock, locked, level);
                Ui.SetText(row.BuyTitle, locked ? "Закрыто" : "Построить");
                UiFeel.SetAvailable(row.Buy, !locked && _gold >= row.Definition.Price);
                bool suggested = campaign ? _focus.Build == kind : !_hasMine && kind == BuildingKind.Mine;
                row.Root.EnableInClassList("is-suggested", suggested && !locked);
            }

            _unitsTab.EnableInClassList("is-suggested", campaign && _focus.Hire != null && !ShowsUnits);
            _buildingsTab.EnableInClassList("is-suggested", campaign && _focus.Build != null && ShowsUnits);
        }

        private static void SetLock(Label label, bool locked, int level)
        {
            Ui.Show(label, locked);
            if (locked) Ui.SetText(label, level > 0 ? $"ОТКРОЕТСЯ ПОСЛЕ УРОВНЯ {level}" : "ПОКА ЗАКРЫТО");
        }

        private void ShowUnits(bool units)
        {
            Ui.Show(_unitsPage, units);
            Ui.Show(_buildingsPage, !units);
            _unitsTab.EnableInClassList("is-active", units);
            _buildingsTab.EnableInClassList("is-active", !units);
            RefreshPrices();
        }

        private UnitCard CreateUnitCard(VisualElement parent, UnitDefinition unit)
        {
            var root = Ui.Box("card");
            root.Add(Ui.Art(unit.PortraitSprite, unit.DisplayName, "card__art"));
            var body = Ui.Box("card__body");
            body.Add(Ui.Text(unit.DisplayName, "card__title"));
            var lockLabel = Ui.Text(string.Empty, "card__lock t-medium");
            body.Add(lockLabel);
            body.Add(Ui.Text(UnitStatsText.Compact(unit), "card__text"));
            root.Add(body);

            var buy = Ui.StackButton("btn--primary card__buy", out var title, out var price);
            UiFeel.Bind(buy, () => _context.Interaction.BeginUnitPlacement(unit.Kind, _hireAmount));
            root.Add(buy);
            parent.Add(root);
            return new UnitCard
            {
                Definition = unit, Root = root, Buy = buy, BuyTitle = title, BuyPrice = price, Lock = lockLabel
            };
        }

        private BuildingRow CreateBuildingRow(BuildingDefinition building)
        {
            var root = Ui.Box("card");
            var icon = building.Icon != null ? building.Icon : building.Sprite;
            root.Add(Ui.Art(icon, building.DisplayName, "card__art card__art--small"));
            var body = Ui.Box("card__body");
            body.Add(Ui.Text(building.DisplayName, "card__title"));
            var lockLabel = Ui.Text(string.Empty, "card__lock t-medium");
            body.Add(lockLabel);
            body.Add(Ui.Text(_context.Session.DescribeBuilding(building), "card__text"));
            root.Add(body);

            if (_showcase.CanShow)
            {
                var look = UiFeel.Bind(Ui.TextButton("Вид", "btn card__look"),
                    () => _showcase.Show(building, _context.Session.DescribeRecipes(building)));
                root.Add(look);
            }
            var buy = Ui.StackButton("btn--primary card__buy", out var title, out var price);
            Ui.SetText(title, "Построить");
            Ui.SetText(price, Ui.Gold(building.Price));
            UiFeel.Bind(buy, () => _context.Interaction.BeginBuildingPlacement(building.Kind));
            root.Add(buy);
            _buildingsPage.Add(root);
            return new BuildingRow
            {
                Definition = building, Root = root, Buy = buy, BuyTitle = title, BuyPrice = price, Lock = lockLabel
            };
        }
    }
}
