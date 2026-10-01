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
    /// The catalog: a tray of round tokens along the bottom, creatures to hire (in groups) and every
    /// constructible building, all read from the content catalog. A token shows the picture, the name and
    /// the price; what the thing does is in its hint. Pressing a token only starts placement on the map; the
    /// session decides the rest. In a campaign, closed creatures show when they open, and of the closed
    /// buildings only the next one is shown.
    /// </summary>
    public sealed class CatalogPanel
    {
        public const int MaxHireAmount = 20;

        private sealed class Token
        {
            public VisualElement Root;
            public Button Buy;
            public Label Name;
            public VisualElement Price;
            public Label PriceValue;
            public Label Lock;
        }

        private sealed class UnitToken
        {
            public UnitDefinition Definition;
            public Token View;
        }

        private sealed class BuildingToken
        {
            public BuildingDefinition Definition;
            public Token View;
        }

        private readonly ColonyHudContext _context;
        private readonly ShowcasePanel _showcase;
        private readonly HudTooltip _tooltip;
        private readonly Sprite _coin;
        private readonly VisualElement _panel;
        private readonly VisualElement _unitsPage;
        private readonly VisualElement _buildingsPage;
        private readonly Button _unitsTab;
        private readonly Button _buildingsTab;
        private readonly Label _hireAmountLabel;
        private readonly List<UnitToken> _units = new();
        private readonly List<BuildingToken> _buildings = new();
        private int _hireAmount = 1;
        private int _gold;
        private bool _hasMine;
        private bool _hasUnits;
        private ProgressSnapshot _progress = ProgressSnapshot.Sandbox;
        private QuestFocus _focus = QuestFocus.None;
        private string _focusQuestId;

        public event Action<bool> OpenChanged;

        public CatalogPanel(VisualElement root, ColonyHudContext context, ShowcasePanel showcase, HudTooltip tooltip = null)
        {
            _context = context;
            _showcase = showcase;
            _tooltip = tooltip;
            _coin = RewardArt.Coin(context.Catalog);
            _panel = Ui.Require<VisualElement>(root, "catalog");
            UiFeel.Bind(Ui.Require<Button>(root, "catalog-close"), () => SetOpen(false), Sfx.UiBack);
            _unitsTab = UiFeel.Bind(Ui.Require<Button>(root, "tab-units"), () => ShowUnits(true));
            _buildingsTab = UiFeel.Bind(Ui.Require<Button>(root, "tab-buildings"), () => ShowUnits(false));
            _unitsPage = Ui.Require<VisualElement>(root, "units-page");
            _buildingsPage = Ui.Require<VisualElement>(root, "buildings-page");
            _hireAmountLabel = Ui.Require<Label>(root, "hire-amount");
            UiFeel.Bind(Ui.Require<Button>(root, "hire-less"), () => SetHireAmount(_hireAmount - 1));
            UiFeel.Bind(Ui.Require<Button>(root, "hire-more"), () => SetHireAmount(_hireAmount + 1));

            var cards = Ui.Require<VisualElement>(root, "unit-cards");
            foreach (var unit in context.Catalog.Units)
                if (unit != null) _units.Add(CreateUnitToken(cards, unit));
            foreach (var building in context.Catalog.Buildings)
                if (building != null && building.Constructible) _buildings.Add(CreateBuildingToken(building));

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
        /// Points at what the current quest needs. When the quest changes the tray turns to the page it
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

        /// <summary>The button of a building's token, or of a creature's.</summary>
        public Button BuyButton(BuildingKind kind) => _buildings.Find(token => token.Definition.Kind == kind)?.View.Buy;
        public Button HireButton(UnitKind kind) => _units.Find(token => token.Definition.Kind == kind)?.View.Buy;

        /// <summary>The price under a building's token, or under a creature's for the whole group.</summary>
        public string PriceOf(BuildingKind kind) => _buildings.Find(token => token.Definition.Kind == kind)?.View.PriceValue.text;
        public string PriceOf(UnitKind kind) => _units.Find(token => token.Definition.Kind == kind)?.View.PriceValue.text;

        public bool IsListed(BuildingKind kind)
        {
            var token = _buildings.Find(candidate => candidate.Definition.Kind == kind);
            return token != null && Ui.IsShown(token.View.Root);
        }

        public bool IsSuggested(BuildingKind kind) =>
            _buildings.Find(token => token.Definition.Kind == kind)?.View.Root.ClassListContains("is-suggested") == true;

        public bool IsSuggested(UnitKind kind) =>
            _units.Find(token => token.Definition.Kind == kind)?.View.Root.ClassListContains("is-suggested") == true;

        private void RefreshPrices()
        {
            bool campaign = _progress.Enabled;
            foreach (var token in _units)
            {
                var unit = token.Definition;
                bool locked = !_progress.IsUnitUnlocked(unit.Kind);
                int total = _context.Session.HirePrice(unit.Kind, _hireAmount);
                Ui.SetText(token.View.Name, _hireAmount > 1 ? $"{unit.DisplayName} ×{_hireAmount}" : unit.DisplayName);
                SetPrice(token.View, locked, _progress.UnlockLevel(unit.Kind), total);
                // a campaign points at what the quest asks; a sandbox, once the mine stands, at hands to work it
                bool suggested = campaign ? _focus.Hire == unit.Kind : _hasMine && !_hasUnits;
                token.View.Root.EnableInClassList("is-suggested", suggested && !locked);
                token.View.Buy.EnableInClassList("is-suggested", suggested && !locked);
            }

            int nextLevel = int.MaxValue;
            foreach (var token in _buildings)
            {
                int level = _progress.UnlockLevel(token.Definition.Kind);
                if (!_progress.IsBuildingUnlocked(token.Definition.Kind) && level > 0) nextLevel = Math.Min(nextLevel, level);
            }
            foreach (var token in _buildings)
            {
                var kind = token.Definition.Kind;
                bool locked = !_progress.IsBuildingUnlocked(kind);
                int level = _progress.UnlockLevel(kind);
                // closed buildings stay out of the tray, except the next one to open
                Ui.Show(token.View.Root, !locked || level == nextLevel);
                SetPrice(token.View, locked, level, _context.Session.BuildingPrice(kind));
                bool suggested = campaign ? _focus.Build == kind : !_hasMine && kind == BuildingKind.Mine;
                token.View.Root.EnableInClassList("is-suggested", suggested && !locked);
                token.View.Buy.EnableInClassList("is-suggested", suggested && !locked);
            }

            _unitsTab.EnableInClassList("is-suggested", campaign && _focus.Hire != null && !ShowsUnits);
            _buildingsTab.EnableInClassList("is-suggested", campaign && _focus.Build != null && ShowsUnits);
        }

        // An open token shows its price, red while the treasury is short; a closed one when it opens.
        private void SetPrice(Token token, bool locked, int level, int price)
        {
            token.Root.EnableInClassList("is-locked", locked);
            token.Root.EnableInClassList("is-short", !locked && _gold < price);
            Ui.Show(token.Price, !locked);
            Ui.Show(token.Lock, locked);
            Ui.SetText(token.PriceValue, price.ToString());
            if (locked) Ui.SetText(token.Lock, level > 0 ? $"с {level} уровня" : "пока закрыто");
            UiFeel.SetAvailable(token.Buy, !locked && _gold >= price);
        }

        private void ShowUnits(bool units)
        {
            Ui.Show(_unitsPage, units);
            Ui.Show(_buildingsPage, !units);
            _unitsTab.EnableInClassList("is-active", units);
            _buildingsTab.EnableInClassList("is-active", !units);
            RefreshPrices();
        }

        private UnitToken CreateUnitToken(VisualElement parent, UnitDefinition unit)
        {
            var view = CreateToken(RewardArt.Tight(unit.PortraitSprite), unit.DisplayName, "token token--unit");
            UiFeel.Bind(view.Buy, () => _context.Interaction.BeginUnitPlacement(unit.Kind, _hireAmount));
            _tooltip?.Attach(view.Buy, () => unit.DisplayName, () => UnitHint(unit));
            parent.Add(view.Root);
            return new UnitToken { Definition = unit, View = view };
        }

        private BuildingToken CreateBuildingToken(BuildingDefinition building)
        {
            var view = CreateToken(RewardArt.BuildingIcon(building), building.DisplayName, "token");
            UiFeel.Bind(view.Buy, () => _context.Interaction.BeginBuildingPlacement(building.Kind));
            _tooltip?.Attach(view.Buy, () => building.DisplayName, () => BuildingHint(building));
            if (_showcase.CanShow)
            {
                var look = Ui.TextButton(string.Empty, "btn btn-disc token__look");
                var glyph = Ui.Box("glyph glyph--look");
                glyph.pickingMode = PickingMode.Ignore;
                look.Add(glyph);
                UiFeel.Bind(look, () => _showcase.Show(building, _context.Session.BuildingPrice(building.Kind),
                    _context.Session.DescribeRecipes(building)));
                _tooltip?.Attach(look, () => "Посмотреть", () => $"{building.DisplayName} в 3D");
                view.Root.Add(look);
            }
            _buildingsPage.Add(view.Root);
            return new BuildingToken { Definition = building, View = view };
        }

        // The disc is the button; the name and price under it only tell.
        private Token CreateToken(Sprite picture, string name, string classes)
        {
            var root = Ui.Box(classes);
            root.pickingMode = PickingMode.Ignore;
            var buy = Ui.TextButton(string.Empty, "btn btn-disc token__disc");
            if (picture != null)
            {
                var image = new Image { sprite = picture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList("token__art");
                buy.Add(image);
            }
            else
            {
                var letter = Ui.Text(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1), "token__monogram t-black");
                letter.pickingMode = PickingMode.Ignore;
                buy.Add(letter);
            }
            var title = Ui.Text(name, "token__name t-bold");
            var price = Ui.Box("token__price");
            if (_coin != null)
            {
                var coin = new Image { sprite = _coin, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                coin.AddToClassList("token__coin");
                price.Add(coin);
            }
            var value = Ui.Text(string.Empty, "token__price-value t-black");
            price.Add(value);
            var lockLabel = Ui.Text(string.Empty, "token__lock");
            foreach (var part in new VisualElement[] { title, price, value, lockLabel }) part.pickingMode = PickingMode.Ignore;
            root.Add(buy);
            root.Add(title);
            root.Add(price);
            root.Add(lockLabel);
            return new Token { Root = root, Buy = buy, Name = title, Price = price, PriceValue = value, Lock = lockLabel };
        }

        /// <summary>
        /// The hover hint of a creature: what it is good for in plain words, from its content. The price is under
        /// the token; the hint adds the price of one while a group is hired, and when a closed one opens.
        /// </summary>
        public string Hint(UnitKind kind)
        {
            var token = _units.Find(candidate => candidate.Definition.Kind == kind);
            return token != null ? UnitHint(token.Definition) : string.Empty;
        }

        private string UnitHint(UnitDefinition unit)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(unit.Description)) lines.Add(Sentence(unit.Description));
            if (_hireAmount > 1) lines.Add($"{_context.Session.HirePrice(unit.Kind)} золота за одного.");
            if (_context.Catalog.Economy.HirePricePercentPerCreature > 0f)
                lines.Add("Каждое новое существо в поселении поднимает цену найма.");
            if (!_progress.IsUnitUnlocked(unit.Kind)) lines.Add(Opens(_progress.UnlockLevel(unit.Kind)));
            return string.Join("\n", lines);
        }

        private string BuildingHint(BuildingDefinition building)
        {
            string text = _context.Session.DescribeBuilding(building);
            if (_context.Catalog.Economy.BuildingCopyPriceGrowth > 1f) text += "\nКаждая следующая такая постройка дороже.";
            return _progress.IsBuildingUnlocked(building.Kind) ? text : text + "\n" + Opens(_progress.UnlockLevel(building.Kind));
        }

        private static string Sentence(string text)
        {
            text = text.Trim();
            return text.EndsWith(".") || text.EndsWith("!") ? text : text + ".";
        }

        private static string Opens(int level) => level > 0 ? $"Откроется на {level} уровне." : "Пока закрыто.";
    }
}
