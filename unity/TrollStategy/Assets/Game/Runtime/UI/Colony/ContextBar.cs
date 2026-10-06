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
    /// The orders tray. While creatures are selected, or while the map waits for a pick (a place, a workplace,
    /// where to carry from or to, a block of land), the catalog steps down and this takes its place along the
    /// bottom: at the left end who is selected or what the map expects, in the middle round buttons with their
    /// keys — the selection's orders, the valid targets with their pictures, the step's own answer and the way
    /// out; names and meanings are in the hints. While the haul cargo dialog is up the tray steps aside. Every
    /// action goes to the interaction controller, the same path as the hotkeys.
    /// </summary>
    public sealed class ContextBar
    {
        private sealed class Order
        {
            public VisualElement Root;
            public Button Button;
            public Label Name;
            public VisualElement Sub;
            public Image Coin;
            public Label SubText;
        }

        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private readonly Sprite _coin;
        private readonly VisualElement _bar;
        private readonly VisualElement _portrait;
        private readonly Image _portraitImage;
        private readonly Label _title;
        private readonly Label _prompt;
        private readonly VisualElement _targets;
        private readonly VisualElement _load;
        private readonly Image _loadArt;
        private readonly Label _loadCount;
        private readonly Label _loadValue;
        private readonly Button _clear;
        private readonly Order _work;
        private readonly Order _haul;
        private readonly Order _release;
        private readonly Order _sell;
        private readonly Order _auto;
        private readonly Order _cargo;
        private readonly Order _land;
        private readonly Order _cancel;
        private readonly List<Order> _targetOrders = new();
        private readonly List<Button> _targetButtons = new();
        private readonly List<BuildingKind> _targetKinds = new();
        private string _targetSignature;
        private QuestFocus _focus = QuestFocus.None;

        public ContextBar(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _tooltip = tooltip;
            _coin = RewardArt.Coin(context.Catalog);
            var interaction = context.Interaction;
            _bar = Ui.Require<VisualElement>(root, "context");
            _portrait = Ui.Require<VisualElement>(root, "context-portrait");
            _portraitImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _portraitImage.AddToClassList("orders__portrait-art");
            _portrait.Add(_portraitImage);
            _title = Ui.Require<Label>(root, "context-title");
            _prompt = Ui.Require<Label>(root, "context-prompt");
            _targets = Ui.Require<VisualElement>(root, "context-targets");
            var actions = Ui.Require<VisualElement>(root, "context-actions");
            _load = Ui.Require<VisualElement>(root, "context-load");
            _loadArt = LoadArt(null);
            _loadCount = Ui.Text(string.Empty, "orders__load-text t-black");
            _loadCount.pickingMode = PickingMode.Ignore;
            var coin = LoadArt(_coin);
            _loadValue = Ui.Text(string.Empty, "orders__load-text t-black t-gold");
            _loadValue.pickingMode = PickingMode.Ignore;
            _load.Add(_loadArt);
            _load.Add(_loadCount);
            _load.Add(coin);
            _load.Add(_loadValue);
            Ui.Show(_load, false);

            _clear = UiFeel.Bind(Ui.CaptionButton("Снять выбор", "Esc", "btn orders__clear"), interaction.CancelOrClear, Sfx.UiBack);
            Ui.Require<VisualElement>(root, "context-side-text").Add(_clear);

            _work = Command(actions, "Работа", "E", "glyph--work", primary: true, interaction.BeginWorkTarget,
                "Потом кликни по зданию: туда встанут работать.");
            _haul = Command(actions, "Перенос", "H", "glyph--haul", primary: true, interaction.BeginHaulTarget,
                "Кликни по двум зданиям: откуда и куда носить.");
            _release = Command(actions, "Свободны", "R", "glyph--free", primary: false, interaction.ReleaseSelected,
                "Снять с работы и маршрута.", silent: true);
            _sell = Command(actions, "Продать", null, null, primary: false, interaction.SellSelected,
                "Вернёт часть цены найма.", silent: true, coinArt: true);
            _auto = Command(actions, "Поставить сам", null, "glyph--auto", primary: true, interaction.PlaceBuildingAutomatically,
                "Постройка встанет на первое свободное место.");
            _cargo = Command(actions, "Изменить груз", null, "glyph--haul", primary: false, interaction.ChangeHaulCargo,
                "Вернуться к выбору, что носить.");
            // the land mode's answer to a picked block: buy it or clear it; ColonyFeedback sounds the result
            _land = Command(actions, "Купить", null, "glyph--land", primary: true, () => interaction.ConfirmLand(),
                null, silent: true);
            _cancel = Command(actions, "Отмена", "Esc", "glyph--cancel", primary: false, interaction.CancelOrClear,
                "Вернуться, ничего не меняя.", click: Sfx.UiBack);
            Ui.Show(_bar, false);
        }

        public bool IsShown => Ui.IsShown(_bar);
        public Label Prompt => _prompt;
        /// <summary>Whether the bar shows the selected hauler's load and its price.</summary>
        public bool ShowsLoad => Ui.IsShown(_load);
        /// <summary>The load as the bar shows it: the count and the price, as "×4" and "12".</summary>
        public (string Count, string Value) Load => (_loadCount.text, _loadValue.text);
        public string Title => _title.text;
        public Button WorkButton => _work.Button;
        public Button SellButton => _sell.Button;
        public Button CancelButton => _cancel.Button;
        /// <summary>Drops the selection, as Esc does.</summary>
        public Button ClearButton => _clear;
        public IReadOnlyList<Button> TargetButtons => _targetButtons;
        /// <summary>The round button of a target of this kind in the current pick, or null.</summary>
        public Button TargetButton(BuildingKind kind)
        {
            int at = _targetKinds.IndexOf(kind);
            return at >= 0 ? _targetButtons[at] : null;
        }
        /// <summary>"Поставить сам" while a building waits for its place.</summary>
        public Button AutoButton => _auto.Button;
        public Button HaulButton => _haul.Button;
        /// <summary>Back to the cargo choice while the haul destination is being picked.</summary>
        public Button ChangeCargoButton => _cargo.Button;
        /// <summary>Land mode: buys or clears the picked block.</summary>
        public Button LandButton => _land.Button;

        /// <summary>What the current quest asks for; the matching command and targets are marked.</summary>
        public void SetFocus(QuestFocus focus) => _focus = focus ?? QuestFocus.None;

        public void Refresh(GameSnapshot snapshot)
        {
            var interaction = _context.Interaction;
            var mode = interaction.Mode;
            int selected = interaction.SelectedIds.Count;

            // the cargo dialog in the middle of the screen asks alone
            if (mode.Type == InteractionModeType.Neutral && selected == 0 ||
                mode.Type == InteractionModeType.ChoosingHaulCargo)
            {
                Ui.Show(_bar, false);
                ClearTargets();
                Ui.Show(_load, false);
                return;
            }

            bool fresh = !IsShown;
            Ui.Show(_bar, true);
            Ui.Show(_load, false);
            if (mode.Type == InteractionModeType.Neutral)
                ShowSelection(snapshot, interaction);
            else
                ShowMode(snapshot, interaction, mode);
            if (fresh) UiMotion.PopIn(_bar, .2f);
        }

        private void ShowSelection(GameSnapshot snapshot, InteractionController interaction)
        {
            var kinds = new List<UnitKind>();
            var counts = new List<int>();
            UnitSnapshot first = null;
            int refund = 0;
            bool ordered = false;
            foreach (var unit in snapshot.Units)
            {
                if (!Contains(interaction.SelectedIds, unit.Id)) continue;
                first ??= unit;
                int at = kinds.IndexOf(unit.UnitKind);
                if (at < 0)
                {
                    kinds.Add(unit.UnitKind);
                    counts.Add(1);
                }
                else counts[at]++;
                refund += Domain.ColonySimulation.UnitSaleRefund(_context.Catalog.GetUnit(unit.UnitKind));
                ordered |= _focus.Orders(unit.UnitKind);
            }
            Ui.SetText(_title, Names(kinds, counts));
            // one creature says what it is doing; a group, where else its orders are
            Ui.SetText(_prompt, interaction.SelectedIds.Count == 1 && first != null
                ? first.Status ?? string.Empty
                : "Правый клик по карте: приказы у курсора");
            ShowPortrait(first != null ? _context.Catalog.GetUnit(first.UnitKind).PortraitSprite : null, tight: true);
            if (interaction.SelectedIds.Count == 1 && first != null) ShowLoad(first);
            ClearTargets();
            SetSub(_sell, "+" + refund, coin: true);
            SetButtons(selection: true, auto: false, cargo: false, cancel: false);
            // the quest's next order for these creatures
            _work.Button.EnableInClassList("is-suggested", ordered && _focus.WorkTarget != null);
            _haul.Button.EnableInClassList("is-suggested", ordered && _focus.HaulFrom != null);
        }

        private void ShowMode(GameSnapshot snapshot, InteractionController interaction, InteractionMode mode)
        {
            var catalog = _context.Catalog;
            string title;
            Sprite picture = null;
            bool tight = false;
            switch (mode.Type)
            {
                case InteractionModeType.PlacingBuilding:
                    var building = catalog.GetBuilding(mode.BuildingKind);
                    title = $"{building.DisplayName} за {Ui.Gold(_context.Session.BuildingPrice(mode.BuildingKind))}";
                    picture = RewardArt.BuildingIcon(building);
                    break;
                case InteractionModeType.MovingBuilding:
                    title = "Перенос постройки";
                    break;
                case InteractionModeType.PlacingUnits:
                    var unit = catalog.GetUnit(mode.UnitKind);
                    title = $"{unit.DisplayName} ×{mode.Amount} за {Ui.Gold(_context.Session.HirePrice(mode.UnitKind, mode.Amount))}";
                    picture = unit.PortraitSprite;
                    tight = true;
                    break;
                case InteractionModeType.ChoosingWorkTarget:
                    title = "Куда на работу";
                    break;
                case InteractionModeType.ChoosingHaulSource:
                    title = "1. Откуда носить";
                    break;
                case InteractionModeType.ChoosingHaulDestination:
                    title = "2. Куда носить";
                    break;
                case InteractionModeType.ManagingLand:
                    title = snapshot.Land != null ? $"Земля: участок за {Ui.Gold(snapshot.Land.NextPrice)}" : "Земля";
                    break;
                default:
                    title = string.Empty;
                    break;
            }
            // the orders for the selected creatures keep their picture
            if (picture == null && interaction.SelectedIds.Count > 0)
            {
                foreach (var unit in snapshot.Units)
                {
                    if (!Contains(interaction.SelectedIds, unit.Id)) continue;
                    picture = catalog.GetUnit(unit.UnitKind).PortraitSprite;
                    tight = true;
                    break;
                }
            }
            Ui.SetText(_title, title);
            Ui.SetText(_prompt, interaction.Message);
            ShowPortrait(picture, tight);

            // every pick of a building lists its targets, where to carry too; the quest's one is marked
            bool listed = mode.Type == InteractionModeType.ChoosingWorkTarget ||
                          mode.Type == InteractionModeType.ChoosingHaulSource ||
                          mode.Type == InteractionModeType.ChoosingHaulDestination;
            if (listed) ShowTargets(snapshot, interaction, mode);
            else ClearTargets();
            SetButtons(selection: false, auto: mode.Type == InteractionModeType.PlacingBuilding,
                cargo: mode.Type == InteractionModeType.ChoosingHaulDestination, cancel: true);
            ShowLandOffer(snapshot, mode);
            MarkQuestTargets(mode.Type);
        }

        // The target the quest asks for in this step: its workplace, where to carry from, where to carry to.
        private void MarkQuestTargets(InteractionModeType mode)
        {
            BuildingKind? wanted = mode switch
            {
                InteractionModeType.ChoosingWorkTarget => _focus.WorkTarget,
                InteractionModeType.ChoosingHaulSource => _focus.HaulFrom,
                InteractionModeType.ChoosingHaulDestination => _focus.HaulTo,
                _ => null
            };
            for (int i = 0; i < _targetButtons.Count; i++)
                _targetButtons[i].EnableInClassList("is-suggested", wanted != null && _targetKinds[i] == wanted.Value);
        }

        private void ShowLandOffer(GameSnapshot snapshot, InteractionMode mode)
        {
            var land = snapshot.Land;
            bool offer = mode.Type == InteractionModeType.ManagingLand && mode.LandOffer != LandOffer.None && land != null;
            Show(_land, offer);
            if (!offer) return;
            if (mode.LandOffer == LandOffer.Buy)
            {
                Ui.SetText(_land.Name, "Купить");
                SetSub(_land, land.NextPrice.ToString(), coin: true);
                UiFeel.SetAvailable(_land.Button, snapshot.Gold >= land.NextPrice);
            }
            else
            {
                int seconds = (int)Math.Ceiling(land.ClearSeconds);
                Ui.SetText(_land.Name, $"Расчистить, {seconds} с");
                SetSub(_land, land.ClearGold > 0 ? land.ClearGold.ToString() : string.Empty, coin: land.ClearGold > 0);
                UiFeel.SetAvailable(_land.Button, snapshot.Gold >= land.ClearGold);
            }
        }

        /// <summary>Valid targets as round buttons with their pictures, rebuilt only when the step or the set changes.</summary>
        private void ShowTargets(GameSnapshot snapshot, InteractionController interaction, InteractionMode mode)
        {
            var ids = interaction.GetTargetBuildingIds();
            string signature = mode.Type + ":" + mode.SourceId + ":" + string.Join("|", ids);
            if (signature == _targetSignature) return;
            ClearTargets();
            _targetSignature = signature;
            foreach (var id in ids)
            {
                string name = id;
                var kind = default(BuildingKind);
                foreach (var building in snapshot.Buildings)
                {
                    if (building.Id != id) continue;
                    name = building.Name;
                    kind = building.Kind;
                }
                var target = id;
                var order = Token(_targets, name, null);
                var icon = RewardArt.BuildingIcon(_context.Catalog, kind);
                if (icon != null)
                {
                    var art = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    art.AddToClassList("token__art");
                    order.Button.Add(art);
                }
                UiFeel.Bind(order.Button, () => interaction.ChooseBuilding(target), silentClick: true);
                _targetOrders.Add(order);
                _targetButtons.Add(order.Button);
                _targetKinds.Add(kind);
            }
            Ui.Show(_targets, ids.Count > 0);
        }

        private void ClearTargets()
        {
            foreach (var order in _targetOrders) order.Root.RemoveFromHierarchy();
            _targetOrders.Clear();
            _targetButtons.Clear();
            _targetKinds.Clear();
            _targetSignature = null;
            Ui.Show(_targets, false);
        }

        private void SetButtons(bool selection, bool auto, bool cargo, bool cancel)
        {
            Show(_work, selection);
            Show(_haul, selection);
            Show(_release, selection);
            Show(_sell, selection);
            Ui.Show(_clear, selection);
            Show(_auto, auto);
            Show(_cargo, cargo);
            Show(_land, false);
            Show(_cancel, cancel);
        }

        // A token and its button show and hide together, so the button itself tells whether it is offered.
        private static void Show(Order order, bool shown)
        {
            Ui.Show(order.Root, shown);
            Ui.Show(order.Button, shown);
        }

        // a hauler with goods in hand: the good, how many, and what they fetch at the market, over the orders' keys
        private void ShowLoad(UnitSnapshot unit)
        {
            var assignment = unit.Assignment;
            if (assignment == null || assignment.Kind != Domain.AssignmentKind.Haul || assignment.Carried <= 0) return;
            var resource = _context.Catalog.TryGetResource(assignment.CarriedResource);
            _loadArt.sprite = resource?.Icon;
            Ui.Show(_loadArt, resource?.Icon != null);
            Ui.SetText(_loadCount, $"×{assignment.Carried}");
            Ui.SetText(_loadValue, _context.Session.CargoValue(assignment).ToString());
            Ui.Show(_load, true);
        }

        private static Image LoadArt(Sprite sprite)
        {
            var art = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            art.AddToClassList("orders__load-art");
            return art;
        }

        private void ShowPortrait(Sprite sprite, bool tight)
        {
            _portraitImage.sprite = tight ? RewardArt.Tight(sprite) : sprite;
            Ui.Show(_portrait, sprite != null);
        }

        private static void SetSub(Order order, string text, bool coin)
        {
            Ui.SetText(order.SubText, text);
            if (order.Coin != null) Ui.Show(order.Coin, coin);
            Ui.Show(order.Sub, !string.IsNullOrEmpty(text));
        }

        // An order as a catalog token: a round button with its glyph, the name and key under it, a price line.
        private Order Command(VisualElement parent, string name, string hotkey, string glyph, bool primary, Action action,
            string hint, bool silent = false, Sfx click = Sfx.UiClick, bool coinArt = false)
        {
            var order = Token(parent, name, hotkey);
            if (primary) order.Button.AddToClassList("is-primary");
            if (glyph != null)
            {
                var icon = Ui.Box("glyph " + glyph);
                icon.pickingMode = PickingMode.Ignore;
                order.Button.Add(icon);
            }
            else if (coinArt && _coin != null)
            {
                var art = new Image { sprite = _coin, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                art.AddToClassList("orders__coin-art");
                order.Button.Add(art);
            }
            UiFeel.Bind(order.Button, action, click, silent);
            _tooltip?.Attach(order.Button, () => order.Name.text, () => hint, hotkey);
            Show(order, false);
            return order;
        }

        private Order Token(VisualElement parent, string name, string hotkey)
        {
            var root = Ui.Box("token orders__token");
            root.pickingMode = PickingMode.Ignore;
            var button = Ui.TextButton(string.Empty, "btn btn-disc token__disc");
            var line = Ui.Box("orders__name");
            line.pickingMode = PickingMode.Ignore;
            var title = Ui.Text(name, "token__name t-bold");
            title.pickingMode = PickingMode.Ignore;
            line.Add(title);
            if (hotkey != null)
            {
                var key = Ui.Text(hotkey, "hotkey orders__key");
                key.pickingMode = PickingMode.Ignore;
                line.Add(key);
            }
            var sub = Ui.Box("token__price");
            sub.pickingMode = PickingMode.Ignore;
            Image coin = null;
            if (_coin != null)
            {
                coin = new Image { sprite = _coin, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                coin.AddToClassList("token__coin");
                sub.Add(coin);
            }
            var subText = Ui.Text(string.Empty, "token__price-value t-black");
            subText.pickingMode = PickingMode.Ignore;
            sub.Add(subText);
            Ui.Show(sub, false);
            root.Add(button);
            root.Add(line);
            root.Add(sub);
            parent.Add(root);
            return new Order { Root = root, Button = button, Name = title, Sub = sub, Coin = coin, SubText = subText };
        }

        // "Гоблин", "Гоблин ×3", "Гоблин ×2, тролль": the catalog's names, the first one capitalised.
        private string Names(List<UnitKind> kinds, List<int> counts)
        {
            var parts = new List<string>(kinds.Count);
            for (int i = 0; i < kinds.Count; i++)
            {
                string name = _context.Catalog.GetUnit(kinds[i]).DisplayName;
                if (i > 0) name = name.ToLowerInvariant();
                parts.Add(counts[i] > 1 ? $"{name} ×{counts[i]}" : name);
            }
            return string.Join(", ", parts);
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var candidate in ids)
                if (candidate == id) return true;
            return false;
        }
    }
}
