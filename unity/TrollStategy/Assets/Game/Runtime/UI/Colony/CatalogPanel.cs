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
    /// the content catalog. Buying only starts placement on the map; the session decides the rest.
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
        }

        private sealed class BuildingRow
        {
            public BuildingDefinition Definition;
            public VisualElement Root;
            public Button Buy;
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

            // the first step of every colony is a mine, so the drawer opens on buildings
            ShowUnits(false);
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

        public void Refresh(GameSnapshot snapshot)
        {
            _gold = snapshot.Gold;
            _hasUnits = snapshot.Units.Count > 0;
            _hasMine = false;
            foreach (var building in snapshot.Buildings)
                if (building.Kind == BuildingKind.Mine) _hasMine = true;
            RefreshPrices();
        }

        /// <summary>The buy button of a building's row, or of a creature's card.</summary>
        public Button BuyButton(BuildingKind kind) => _buildings.Find(row => row.Definition.Kind == kind)?.Buy;
        public Button HireButton(UnitKind kind) => _units.Find(card => card.Definition.Kind == kind)?.Buy;

        private void RefreshPrices()
        {
            foreach (var card in _units)
            {
                int total = card.Definition.Price * _hireAmount;
                Ui.SetText(card.BuyTitle, _hireAmount > 1 ? $"Нанять ×{_hireAmount}" : "Нанять");
                Ui.SetText(card.BuyPrice, Ui.Gold(total));
                UiFeel.SetAvailable(card.Buy, _gold >= total);
                // once the mine stands, the next step is hands to work it
                card.Root.EnableInClassList("is-suggested", _hasMine && !_hasUnits);
            }
            foreach (var row in _buildings)
            {
                UiFeel.SetAvailable(row.Buy, _gold >= row.Definition.Price);
                row.Root.EnableInClassList("is-suggested", !_hasMine && row.Definition.Kind == BuildingKind.Mine);
            }
        }

        private void ShowUnits(bool units)
        {
            Ui.Show(_unitsPage, units);
            Ui.Show(_buildingsPage, !units);
            _unitsTab.EnableInClassList("is-active", units);
            _buildingsTab.EnableInClassList("is-active", !units);
        }

        private UnitCard CreateUnitCard(VisualElement parent, UnitDefinition unit)
        {
            var root = Ui.Box("card");
            root.Add(Ui.Art(unit.PortraitSprite, unit.DisplayName, "card__art"));
            var body = Ui.Box("card__body");
            body.Add(Ui.Text(unit.DisplayName, "card__title"));
            body.Add(Ui.Text(UnitStatsText.Compact(unit), "card__text"));
            root.Add(body);

            var buy = Ui.StackButton("btn--primary card__buy", out var title, out var price);
            UiFeel.Bind(buy, () => _context.Interaction.BeginUnitPlacement(unit.Kind, _hireAmount));
            root.Add(buy);
            parent.Add(root);
            return new UnitCard { Definition = unit, Root = root, Buy = buy, BuyTitle = title, BuyPrice = price };
        }

        private BuildingRow CreateBuildingRow(BuildingDefinition building)
        {
            var root = Ui.Box("card");
            var icon = building.Icon != null ? building.Icon : building.Sprite;
            root.Add(Ui.Art(icon, building.DisplayName, "card__art card__art--small"));
            var body = Ui.Box("card__body");
            body.Add(Ui.Text(building.DisplayName, "card__title"));
            body.Add(Ui.Text(Describe(building), "card__text"));
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
            return new BuildingRow { Definition = building, Root = root, Buy = buy };
        }

        private string Describe(BuildingDefinition building)
        {
            string text = $"{building.Width}×{building.Height}";
            if (building.MaxWorkers > 0) text += $" · до {building.MaxWorkers} рабочих";
            switch (building.StorageRole)
            {
                case StorageRole.Stockpile:
                    text += " · хранит сырьё";
                    break;
                case StorageRole.Market:
                    text += " · продаёт товары";
                    break;
                case StorageRole.Armory:
                    text += " · снаряжение отряда";
                    break;
            }
            string recipes = _context.Session.DescribeRecipes(building);
            return string.IsNullOrEmpty(recipes) ? text : text + "\n" + recipes;
        }
    }
}
