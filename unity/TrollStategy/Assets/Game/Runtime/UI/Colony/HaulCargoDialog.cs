using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The middle step of every haul order, in the middle of the screen: what the hauler takes from the source
    /// just picked. "Всё" or chosen goods, each a card with its icon, what the source holds now and a hint on
    /// hover; a mine offers its ore and crystals, a farm its wheat. Where to carry it is picked on the map next.
    /// Choices go to the interaction controller, which owns the order being given.
    /// </summary>
    public sealed class HaulCargoDialog
    {
        // Icons of the goods shown together on the "everything" card.
        private const int StackIcons = 3;

        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private readonly VisualElement _overlay;
        private readonly VisualElement _dialog;
        private readonly Label _title;
        private readonly Label _note;
        private readonly VisualElement _choices;
        private readonly Button _confirm;
        private readonly Button _cancel;
        private readonly List<Button> _cards = new();
        private readonly List<ResourceKind> _kinds = new();
        private readonly List<Label> _counts = new();
        private Button _carryAll;
        private string _signature;

        public HaulCargoDialog(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _tooltip = tooltip;
            var interaction = context.Interaction;
            _overlay = Ui.Require<VisualElement>(root, "haul-cargo-overlay");
            _dialog = Ui.Require<VisualElement>(root, "haul-cargo-dialog");
            _title = Ui.Require<Label>(root, "haul-cargo-title");
            _note = Ui.Require<Label>(root, "haul-cargo-note");
            _choices = Ui.Require<VisualElement>(root, "haul-cargo-choices");
            var actions = Ui.Require<VisualElement>(root, "haul-cargo-actions");
            _cancel = UiFeel.Bind(Ui.CaptionButton("Отмена", "Esc", "btn"), interaction.CancelOrClear, Sfx.UiBack);
            _confirm = UiFeel.Bind(Ui.CaptionButton("Куда носить →", "Enter", "btn btn--primary"),
                interaction.ConfirmHaulCargo);
            actions.Add(_cancel);
            actions.Add(_confirm);
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => Ui.IsShown(_overlay);
        public string Title => _title.text;
        public string Note => _note.text;
        /// <summary>The "everything" card; null while the dialog is closed.</summary>
        public Button CarryAllButton => _carryAll;
        public IReadOnlyList<Button> CargoButtons => _cards;
        public IReadOnlyList<ResourceKind> CargoKinds => _kinds;
        public Button ConfirmButton => _confirm;
        public Button CancelButton => _cancel;

        /// <summary>The hover hint of a good: what the source holds now, its price and who makes and needs it.</summary>
        public string Hint(ResourceKind resource)
        {
            var session = _context.Session;
            var source = Find(session.CurrentSnapshot, _context.Interaction.Mode.SourceId);
            string held = source != null ? $"Сейчас в «{source.Name}»: {Stock(source, resource)}" : string.Empty;
            string about = session.DescribeResource(resource);
            return string.IsNullOrEmpty(about) ? held : held + "\n" + about;
        }

        public void Refresh(GameSnapshot snapshot)
        {
            var interaction = _context.Interaction;
            var mode = interaction.Mode;
            if (mode.Type != InteractionModeType.ChoosingHaulCargo)
            {
                if (IsOpen) Close();
                return;
            }

            var source = Find(snapshot, mode.SourceId);
            var choices = interaction.HaulCargoChoices;
            string signature = mode.SourceId + ":" + string.Join("|", choices);
            if (signature != _signature) Build(source, choices, signature);

            var cargo = interaction.HaulCargo;
            _carryAll.EnableInClassList("is-on", cargo.Count == 0);
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].EnableInClassList("is-on", Contains(cargo, _kinds[i]));
                Ui.SetText(_counts[i], "в здании: " + Stock(source, _kinds[i]));
            }

            bool onward = interaction.HaulCargoHasDestination;
            UiFeel.SetAvailable(_confirm, onward);
            Ui.SetText(_note, onward ? Summary(cargo) : "Ни одно здание не примет всё выбранное сразу — уберите лишнее.");
            _note.EnableInClassList("t-bad", !onward);

            if (IsOpen) return;
            Ui.Show(_overlay, true);
            UiMotion.PopIn(_dialog, .2f);
        }

        /// <summary>A refused intent while the dialog asks: the note that explains it shakes.</summary>
        public void PlayRefusal(Color color)
        {
            UiMotion.Nudge(_note, 8f);
            UiMotion.Flash(_note, color, .6f);
        }

        private void Build(BuildingSnapshot source, IReadOnlyList<ResourceKind> choices, string signature)
        {
            ClearCards();
            _signature = signature;
            var interaction = _context.Interaction;
            string sourceName = source?.Name ?? "здания";
            Ui.SetText(_title, $"Что носить из «{sourceName}»?");

            _carryAll = Card("Всё", out var art, out var sub);
            art.AddToClassList("cargo-card__stack");
            for (int i = 0; i < choices.Count && i < StackIcons; i++)
                art.Add(Icon(choices[i], "cargo-card__stack-icon"));
            Ui.SetText(sub, "что есть");
            UiFeel.Bind(_carryAll, interaction.CarryEverything);
            _tooltip?.Attach(_carryAll, () => "Всё подряд",
                () => $"Носильщик берёт по очереди всё, что отдаёт «{sourceName}» и примет место доставки.");
            _choices.Add(_carryAll);

            foreach (var resource in choices)
            {
                string name = _context.Session.ResourceName(resource);
                var card = Card(name, out var picture, out var count);
                picture.Add(Icon(resource, "cargo-card__icon"));
                var good = resource;
                UiFeel.Bind(card, () => interaction.ToggleHaulCargo(good));
                _tooltip?.Attach(card, () => name, () => Hint(good));
                _choices.Add(card);
                _cards.Add(card);
                _kinds.Add(resource);
                _counts.Add(count);
            }
        }

        private void Close()
        {
            ClearCards();
            Ui.Show(_overlay, false);
        }

        // Removing the cards also takes down a hint shown over one of them.
        private void ClearCards()
        {
            _choices.Clear();
            _cards.Clear();
            _kinds.Clear();
            _counts.Clear();
            _carryAll = null;
            _signature = null;
        }

        private static Button Card(string name, out VisualElement art, out Label sub)
        {
            var card = Ui.TextButton(string.Empty, "btn cargo-card");
            art = Ui.Box("cargo-card__art");
            art.pickingMode = PickingMode.Ignore;
            var caption = Ui.Text(name, "btn__caption cargo-card__name");
            caption.pickingMode = PickingMode.Ignore;
            sub = Ui.Text(string.Empty, "cargo-card__sub");
            sub.pickingMode = PickingMode.Ignore;
            card.Add(art);
            card.Add(caption);
            card.Add(sub);
            return card;
        }

        // The good's icon, or its first letter while it has no art.
        private VisualElement Icon(ResourceKind resource, string classes)
        {
            var sprite = _context.Catalog.TryGetResource(resource)?.Icon;
            if (sprite != null)
            {
                var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList(classes);
                return image;
            }
            string name = _context.Session.ResourceName(resource);
            var letter = Ui.Text(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1), classes + " cargo-card__letter t-bold");
            letter.pickingMode = PickingMode.Ignore;
            return letter;
        }

        private string Summary(IReadOnlyList<ResourceKind> cargo)
        {
            if (cargo.Count == 0) return "Носильщик будет брать по очереди всё, что есть.";
            var names = new List<string>(cargo.Count);
            foreach (var resource in cargo) names.Add(_context.Session.ResourceName(resource).ToLowerInvariant());
            return "Носить: " + string.Join(", ", names) + ".";
        }

        private static int Stock(BuildingSnapshot source, ResourceKind resource)
        {
            if (source == null) return 0;
            foreach (var stack in source.Stock)
                if (stack.Resource == resource) return stack.Amount;
            return 0;
        }

        private static BuildingSnapshot Find(GameSnapshot snapshot, string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            foreach (var building in snapshot.Buildings)
                if (building.Id == buildingId) return building;
            return null;
        }

        private static bool Contains(IReadOnlyList<ResourceKind> items, ResourceKind item)
        {
            foreach (var candidate in items)
                if (candidate == item) return true;
            return false;
        }
    }
}
