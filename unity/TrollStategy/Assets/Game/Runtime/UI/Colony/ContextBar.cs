using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Bottom bar for the current intent. With creatures selected it offers their commands; while placing,
    /// moving or picking a target it says what the map expects and offers a way out. Workplaces and haul
    /// sources are also listed as buttons; where a haul goes is picked on the map only. While the haul cargo
    /// dialog is up the bar steps aside. Every action goes to the interaction controller, the same path as
    /// the hotkeys.
    /// </summary>
    public sealed class ContextBar
    {
        private readonly ColonyHudContext _context;
        private readonly VisualElement _bar;
        private readonly Label _title;
        private readonly Label _prompt;
        private readonly VisualElement _targets;
        private readonly Button _work;
        private readonly Button _haul;
        private readonly Button _release;
        private readonly Button _barracks;
        private readonly Button _sell;
        private readonly Button _auto;
        private readonly Button _cargo;
        private readonly Button _cancel;
        private readonly Button _clear;
        private readonly List<Button> _targetButtons = new();
        private readonly List<BuildingKind> _targetKinds = new();
        private string _targetSignature;
        private QuestFocus _focus = QuestFocus.None;

        public ContextBar(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            var interaction = context.Interaction;
            _bar = Ui.Require<VisualElement>(root, "context");
            _title = Ui.Require<Label>(root, "context-title");
            _prompt = Ui.Require<Label>(root, "context-prompt");
            _targets = Ui.Require<VisualElement>(root, "context-targets");
            var actions = Ui.Require<VisualElement>(root, "context-actions");

            _work = Command(actions, "Работа", "W", "btn--primary", interaction.BeginWorkTarget);
            _haul = Command(actions, "Перенос", "H", "btn--primary", interaction.BeginHaulTarget);
            _release = Command(actions, "Свободны", "R", null, interaction.ReleaseSelected, silent: true);
            _barracks = Command(actions, "В бараки", null, null, interaction.SendSelectedToBarracks, silent: true);
            _sell = Command(actions, "Продать", null, "btn--danger", interaction.SellSelected, silent: true);
            _auto = Command(actions, "Поставить сам", null, "btn--primary", interaction.PlaceBuildingAutomatically);
            _cargo = Command(actions, "Изменить груз", null, null, interaction.ChangeHaulCargo);
            _cancel = Command(actions, "Отмена", "Esc", null, interaction.CancelOrClear, click: Sfx.UiBack);
            _clear = Command(actions, "×", "Esc", "btn-close", interaction.CancelOrClear, click: Sfx.UiBack);
            Ui.Show(_bar, false);
        }

        public bool IsShown => Ui.IsShown(_bar);
        public Label Prompt => _prompt;
        public string Title => _title.text;
        public Button WorkButton => _work;
        public Button SellButton => _sell;
        public Button CancelButton => _cancel;
        public IReadOnlyList<Button> TargetButtons => _targetButtons;
        public Button HaulButton => _haul;
        /// <summary>Back to the cargo choice while the haul destination is being picked.</summary>
        public Button ChangeCargoButton => _cargo;

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
                return;
            }

            bool fresh = !IsShown;
            Ui.Show(_bar, true);
            if (mode.Type == InteractionModeType.Neutral)
                ShowSelection(snapshot, interaction);
            else
                ShowMode(snapshot, interaction, mode);
            if (fresh) UiMotion.PopIn(_title, .2f);
        }

        private void ShowSelection(GameSnapshot snapshot, InteractionController interaction)
        {
            int goblins = 0, trolls = 0, refund = 0;
            bool ordered = false;
            foreach (var unit in snapshot.Units)
            {
                if (!Contains(interaction.SelectedIds, unit.Id)) continue;
                if (unit.UnitKind == UnitKind.Goblin) goblins++;
                else if (unit.UnitKind == UnitKind.Troll) trolls++;
                refund += Domain.ColonySimulation.UnitSaleRefund(_context.Catalog.GetUnit(unit.UnitKind));
                ordered |= _focus.Orders(unit.UnitKind);
            }
            Ui.SetText(_title, $"Выбрано: {interaction.SelectedIds.Count}");
            Ui.SetText(_prompt, Breakdown(goblins, trolls));
            ClearTargets();
            Ui.SetCaption(_sell, "Продать +" + Ui.Gold(refund));
            SetButtons(selection: true, auto: false, cargo: false, cancel: false);
            // the quest's next order for these creatures
            _work.EnableInClassList("is-suggested", ordered && _focus.WorkTarget != null);
            _haul.EnableInClassList("is-suggested", ordered && _focus.HaulFrom != null);
        }

        private void ShowMode(GameSnapshot snapshot, InteractionController interaction, InteractionMode mode)
        {
            var catalog = _context.Catalog;
            string title;
            switch (mode.Type)
            {
                case InteractionModeType.PlacingBuilding:
                    var building = catalog.GetBuilding(mode.BuildingKind);
                    title = $"{building.DisplayName} · {Ui.Gold(building.Price)}";
                    break;
                case InteractionModeType.MovingBuilding:
                    title = "Перенос постройки";
                    break;
                case InteractionModeType.PlacingUnits:
                    var unit = catalog.GetUnit(mode.UnitKind);
                    title = $"{unit.DisplayName} ×{mode.Amount} · {Ui.Gold(unit.Price * mode.Amount)}";
                    break;
                case InteractionModeType.ChoosingWorkTarget:
                    title = "Куда на работу";
                    break;
                case InteractionModeType.ChoosingHaulSource:
                    title = "Откуда носить";
                    break;
                case InteractionModeType.ChoosingHaulDestination:
                    title = "Куда носить";
                    break;
                default:
                    title = string.Empty;
                    break;
            }
            Ui.SetText(_title, title);
            Ui.SetText(_prompt, interaction.Message);

            // the haul destination is the player's call on the map: no list of buildings for it
            bool listed = mode.Type == InteractionModeType.ChoosingWorkTarget ||
                          mode.Type == InteractionModeType.ChoosingHaulSource;
            if (listed) ShowTargets(snapshot, interaction, mode);
            else ClearTargets();
            SetButtons(selection: false, auto: mode.Type == InteractionModeType.PlacingBuilding,
                cargo: mode.Type == InteractionModeType.ChoosingHaulDestination, cancel: true);
            MarkQuestTargets(mode.Type);
        }

        // The target the quest asks for in this step: its workplace, where to carry from.
        private void MarkQuestTargets(InteractionModeType mode)
        {
            BuildingKind? wanted = mode switch
            {
                InteractionModeType.ChoosingWorkTarget => _focus.WorkTarget,
                InteractionModeType.ChoosingHaulSource => _focus.HaulFrom,
                _ => null
            };
            for (int i = 0; i < _targetButtons.Count; i++)
                _targetButtons[i].EnableInClassList("is-suggested", wanted != null && _targetKinds[i] == wanted.Value);
        }

        /// <summary>Valid targets as buttons, rebuilt only when the step or the set of targets changes.</summary>
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
                var button = UiFeel.Bind(Ui.TextButton(name, "btn chip"), () => interaction.ChooseBuilding(target),
                    silentClick: true);
                _targets.Add(button);
                _targetButtons.Add(button);
                _targetKinds.Add(kind);
            }
            Ui.Show(_targets, ids.Count > 0);
        }

        private void ClearTargets()
        {
            foreach (var button in _targetButtons) button.RemoveFromHierarchy();
            _targetButtons.Clear();
            _targetKinds.Clear();
            _targetSignature = null;
            Ui.Show(_targets, false);
        }

        private void SetButtons(bool selection, bool auto, bool cargo, bool cancel)
        {
            Ui.Show(_work, selection);
            Ui.Show(_haul, selection);
            Ui.Show(_release, selection);
            Ui.Show(_barracks, selection);
            Ui.Show(_sell, selection);
            Ui.Show(_clear, selection);
            Ui.Show(_auto, auto);
            Ui.Show(_cargo, cargo);
            Ui.Show(_cancel, cancel);
        }

        private static Button Command(VisualElement parent, string text, string hotkey, string style, Action action,
            bool silent = false, Sfx click = Sfx.UiClick)
        {
            var button = Ui.CaptionButton(text, hotkey, "btn " + style);
            UiFeel.Bind(button, action, click, silent);
            parent.Add(button);
            return button;
        }

        private static string Breakdown(int goblins, int trolls)
        {
            var parts = new List<string>(2);
            if (goblins > 0) parts.Add($"гоблины: {goblins}");
            if (trolls > 0) parts.Add($"тролли: {trolls}");
            return parts.Count > 0 ? string.Join(" · ", parts) + " · ПКМ по карте — веер команд" : string.Empty;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var candidate in ids)
                if (candidate == id) return true;
            return false;
        }
    }
}
