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
    /// Who serves the inspected building: its workers, and the haulers whose route starts or ends there, one
    /// group per cargo they were told to carry (a good, a mix of goods, or any). Every group starts folded to
    /// its caption and opens on a click until the card closes or turns to another building. Each row can be
    /// taken off the job alone, and each group all at once, folded or not; released creatures stay in the
    /// colony, free. Groups and rows are pooled and follow the snapshot.
    /// </summary>
    public sealed class StaffList
    {
        // Icons of the goods shown before a hauler group's caption.
        private const int CargoIcons = 3;
        // Hauler groups are keyed by goods' names, so the workers' key never meets one.
        private const string WorkersKey = "workers";

        private sealed class Row
        {
            public VisualElement Root;
            public Image Portrait;
            public Label Name;
            public Label State;
            public Button Release;
            public string UnitId;
        }

        private sealed class Group
        {
            public VisualElement Root;
            // The arrow, icons and caption: opens and folds the rows.
            public Button Toggle;
            public VisualElement Arrow;
            public VisualElement Icons;
            public Label Caption;
            public Label Empty;
            public Button ReleaseAll;
            public VisualElement Rows;
            public readonly List<Row> Pool = new();
            public readonly List<string> Ids = new();
            public int Count;
            // What the group stands for now, the workers or a cargo; hauler groups are pooled by position.
            public string Key;
        }

        private readonly ColonyHudContext _context;
        private readonly VisualElement _root;
        private readonly Group _workers;
        private readonly List<Group> _haulers = new();
        // The cargo of each shown hauler group, sorted goods; empty is "any".
        private readonly List<ResourceKind[]> _cargoes = new();
        private readonly List<string> _haulerIds = new();
        // Haulers of the building while it is being shown; a snapshot copies the assignment on every read.
        private readonly List<(UnitSnapshot Unit, Assignment Assignment)> _hauling = new();
        // Keys of the groups the player opened on the building shown, so a group stays open as others go.
        private readonly HashSet<string> _open = new();
        private string _buildingId;

        public StaffList(VisualElement container, ColonyHudContext context)
        {
            _context = context;
            _root = container;
            _workers = CreateGroup();
            _workers.Key = WorkersKey;
            Hide();
        }

        public bool IsShown => Ui.IsShown(_root);
        public Button ReleaseAllWorkers => _workers.ReleaseAll;
        public string WorkersCaption => _workers.Caption.text;
        public Button WorkersToggle => _workers.Toggle;
        /// <summary>Whether the workers' group lists them or is folded to its caption.</summary>
        public bool AreWorkersOpen => Ui.IsShown(_workers.Rows);
        public IReadOnlyList<Button> WorkerButtons => Buttons(_workers);
        public IReadOnlyList<string> WorkerIds => _workers.Ids;
        /// <summary>Every hauler shown, group after group.</summary>
        public IReadOnlyList<string> HaulerIds => _haulerIds;
        /// <summary>Hauler groups shown, ordered by their goods, "any cargo" last.</summary>
        public int HaulerGroupCount => _cargoes.Count;
        public string HaulerCaption(int group) => _haulers[group].Caption.text;
        public IReadOnlyList<string> HaulerGroupIds(int group) => _haulers[group].Ids;
        public Button ReleaseAllHaulers(int group) => _haulers[group].ReleaseAll;
        public Button HaulerGroupToggle(int group) => _haulers[group].Toggle;
        /// <summary>Whether the group lists its haulers or is folded to its caption.</summary>
        public bool IsHaulerGroupOpen(int group) => Ui.IsShown(_haulers[group].Rows);

        public void Show(BuildingSnapshot building, GameSnapshot snapshot)
        {
            if (building.Id != _buildingId)
            {
                _buildingId = building.Id;
                _open.Clear();
            }

            Begin(_workers);
            _cargoes.Clear();
            foreach (var unit in snapshot.Units)
            {
                var a = unit.Assignment;
                if ((a.Kind == AssignmentKind.Work || a.Kind == AssignmentKind.ToWork) && a.BuildingId == building.Id)
                    AddRow(_workers, unit, a.Kind == AssignmentKind.Work ? "работает" : "идёт на работу");
                else if (Hauls(a, building))
                {
                    _hauling.Add((unit, a));
                    if (!HasCargo(a.Cargo)) _cargoes.Add(Sorted(a.Cargo));
                }
            }
            EndGroup(_workers, building.IsWorkplace,
                building.MaxWorkers > 0 ? $"Рабочие: {_workers.Count} из {building.MaxWorkers}" : $"Рабочие: {_workers.Count}",
                "Никто не работает: выделите существ → «Работа» → это здание.");
            Fold(_workers);

            _cargoes.Sort(CompareCargo);
            _haulerIds.Clear();
            for (int i = 0; i < _cargoes.Count; i++)
            {
                if (i == _haulers.Count) _haulers.Add(CreateGroup());
                var group = _haulers[i];
                Begin(group);
                group.Key = string.Join(",", _cargoes[i]);
                foreach (var (unit, assignment) in _hauling)
                    if (SameCargo(assignment.Cargo, _cargoes[i]))
                        AddRow(group, unit, HaulText(building, assignment, snapshot));
                _haulerIds.AddRange(group.Ids);
                ShowCargo(group, _cargoes[i]);
                EndGroup(group, false, CargoCaption(_cargoes[i], group.Count), null);
                Fold(group);
            }
            for (int i = _cargoes.Count; i < _haulers.Count; i++) Ui.Show(_haulers[i].Root, false);
            _hauling.Clear();
            Ui.Show(_root, Ui.IsShown(_workers.Root) || _cargoes.Count > 0);
        }

        /// <summary>Hides the list; the next building shown starts with its groups folded.</summary>
        public void Hide()
        {
            _buildingId = null;
            Ui.Show(_root, false);
        }

        private static bool Hauls(Assignment assignment, BuildingSnapshot building) =>
            assignment.Kind == AssignmentKind.Haul &&
            (assignment.SourceId == building.Id || assignment.DestinationId == building.Id);

        // The cargo is in the group caption, so the row tells only where the hauler goes.
        private static string HaulText(BuildingSnapshot building, Assignment assignment, GameSnapshot snapshot)
        {
            if (assignment.SourceId == building.Id)
                return $"увозит в «{NameOf(snapshot, assignment.DestinationId)}»";
            return $"привозит из «{NameOf(snapshot, assignment.SourceId)}»";
        }

        private static string NameOf(GameSnapshot snapshot, string buildingId)
        {
            foreach (var building in snapshot.Buildings)
                if (building.Id == buildingId) return building.Name;
            return "?";
        }

        // "Руда: 10", "Руда, кристалл: 3", "Любой груз: 4".
        private string CargoCaption(ResourceKind[] cargo, int count)
        {
            if (cargo.Length == 0) return $"Любой груз: {count}";
            var names = new string[cargo.Length];
            for (int i = 0; i < cargo.Length; i++)
            {
                string name = _context.Session.ResourceName(cargo[i]);
                names[i] = i == 0 ? name : name.ToLowerInvariant();
            }
            return $"{string.Join(", ", names)}: {count}";
        }

        private void ShowCargo(Group group, ResourceKind[] cargo)
        {
            int shown = 0;
            foreach (var resource in cargo)
            {
                var sprite = _context.Catalog.TryGetResource(resource)?.Icon;
                if (sprite == null || shown == CargoIcons) continue;
                if (shown == group.Icons.childCount) group.Icons.Add(CargoIcon());
                var icon = (Image)group.Icons[shown++];
                if (icon.sprite != sprite) icon.sprite = sprite;
                Ui.Show(icon, true);
            }
            for (int i = shown; i < group.Icons.childCount; i++) Ui.Show(group.Icons[i], false);
            Ui.Show(group.Icons, shown > 0);
        }

        // A folded group keeps its caption, "Снять всех" and the note that nobody works; its rows show once the
        // player opens it. A group with nobody has nothing to open and shows no arrow.
        private void Fold(Group group)
        {
            bool open = _open.Contains(group.Key);
            Ui.Show(group.Rows, open);
            Ui.Show(group.Arrow, group.Count > 0);
            group.Root.EnableInClassList("is-collapsed", !open);
        }

        private void ToggleOpen(Group group)
        {
            if (group.Key == null) return;
            if (!_open.Remove(group.Key)) _open.Add(group.Key);
            Fold(group);
        }

        private bool HasCargo(List<ResourceKind> cargo)
        {
            foreach (var key in _cargoes)
                if (SameCargo(cargo, key)) return true;
            return false;
        }

        // The session keeps a hauler's goods distinct, so equal counts and containment mean the same set.
        private static bool SameCargo(List<ResourceKind> cargo, ResourceKind[] key)
        {
            int count = cargo?.Count ?? 0;
            if (count != key.Length) return false;
            foreach (var resource in key)
                if (!cargo.Contains(resource)) return false;
            return true;
        }

        private static ResourceKind[] Sorted(List<ResourceKind> cargo)
        {
            var key = cargo != null ? cargo.ToArray() : Array.Empty<ResourceKind>();
            Array.Sort(key);
            return key;
        }

        // By their goods in catalog order, "any cargo" last.
        private static int CompareCargo(ResourceKind[] a, ResourceKind[] b)
        {
            if (a.Length == 0 || b.Length == 0) return b.Length.CompareTo(a.Length);
            for (int i = 0; i < a.Length && i < b.Length; i++)
                if (a[i] != b[i]) return ((int)a[i]).CompareTo((int)b[i]);
            return a.Length.CompareTo(b.Length);
        }

        private static void Begin(Group group)
        {
            group.Ids.Clear();
            group.Count = 0;
        }

        private void AddRow(Group group, UnitSnapshot unit, string state)
        {
            if (group.Count == group.Pool.Count) group.Pool.Add(CreateRow(group));
            var row = group.Pool[group.Count++];
            row.UnitId = unit.Id;
            group.Ids.Add(unit.Id);
            var definition = Definition(unit.UnitKind);
            var portrait = RewardArt.Tight(definition != null ? definition.PortraitSprite : null);
            if (row.Portrait.sprite != portrait) row.Portrait.sprite = portrait;
            Ui.SetText(row.Name, unit.Name);
            Ui.SetText(row.State, state);
            Ui.Show(row.Root, true);
        }

        // Hides the unused rows, and the whole group when it has nobody and should not say so.
        private static void EndGroup(Group group, bool showEmpty, string caption, string empty)
        {
            for (int i = group.Count; i < group.Pool.Count; i++) Ui.Show(group.Pool[i].Root, false);
            Ui.SetText(group.Caption, caption);
            Ui.Show(group.Root, group.Count > 0 || showEmpty);
            Ui.Show(group.ReleaseAll, group.Count > 1);
            Ui.SetText(group.Empty, empty);
            Ui.Show(group.Empty, group.Count == 0 && !string.IsNullOrEmpty(empty));
        }

        private Group CreateGroup()
        {
            var group = new Group { Root = Ui.Box("staff") };
            var header = Ui.Box("staff__header");
            group.Toggle = UiFeel.Bind(Ui.TextButton(string.Empty, "btn btn-flat staff__toggle"), () => ToggleOpen(group));
            group.Arrow = Ui.Box("glyph glyph--chevron staff__chevron");
            group.Arrow.pickingMode = PickingMode.Ignore;
            group.Toggle.Add(group.Arrow);
            group.Icons = Ui.Box("staff__icons");
            group.Icons.pickingMode = PickingMode.Ignore;
            Ui.Show(group.Icons, false);
            group.Toggle.Add(group.Icons);
            group.Caption = Ui.Text(string.Empty, "staff__caption t-caption");
            group.Caption.pickingMode = PickingMode.Ignore;
            group.Toggle.Add(group.Caption);
            header.Add(group.Toggle);
            group.ReleaseAll = UiFeel.Bind(Ui.CaptionButton("Снять всех", null, "btn staff__all"),
                () => _context.Interaction.ReleaseUnits(new List<string>(group.Ids)), Sfx.UiBack);
            header.Add(group.ReleaseAll);
            group.Root.Add(header);
            group.Empty = Ui.Text(string.Empty, "note staff__empty");
            group.Root.Add(group.Empty);
            group.Rows = Ui.Box("staff__rows");
            group.Root.Add(group.Rows);
            _root.Add(group.Root);
            return group;
        }

        private static Image CargoIcon()
        {
            var icon = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("staff__icon");
            return icon;
        }

        private Row CreateRow(Group group)
        {
            var row = new Row { Root = Ui.Box("staff-row") };
            row.Portrait = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            row.Portrait.AddToClassList("staff-row__portrait");
            var text = Ui.Box("staff-row__text");
            row.Name = Ui.Text(string.Empty, "staff-row__name t-medium");
            row.State = Ui.Text(string.Empty, "staff-row__state");
            text.Add(row.Name);
            text.Add(row.State);
            row.Release = UiFeel.Bind(Ui.TextButton("×", "btn btn-close staff-row__release"), () =>
            {
                if (row.UnitId != null) _context.Interaction.ReleaseUnits(new[] { row.UnitId });
            }, Sfx.UiBack);
            row.Root.Add(row.Portrait);
            row.Root.Add(text);
            row.Root.Add(row.Release);
            group.Rows.Add(row.Root);
            return row;
        }

        private UnitDefinition Definition(UnitKind kind)
        {
            foreach (var unit in _context.Catalog.Units)
                if (unit != null && unit.Kind == kind) return unit;
            return null;
        }

        private static IReadOnlyList<Button> Buttons(Group group)
        {
            var buttons = new List<Button>(group.Count);
            for (int i = 0; i < group.Count; i++) buttons.Add(group.Pool[i].Release);
            return buttons;
        }
    }
}
