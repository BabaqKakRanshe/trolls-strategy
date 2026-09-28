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
    /// Who serves the inspected building: its workers, and the haulers whose route starts or ends there. Each
    /// row can be taken off the job alone, and each group all at once; released creatures stay in the colony,
    /// free. Rows are pooled and follow the snapshot.
    /// </summary>
    public sealed class StaffList
    {
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
            public Label Caption;
            public Label Empty;
            public Button ReleaseAll;
            public VisualElement Rows;
            public readonly List<Row> Pool = new();
            public readonly List<string> Ids = new();
            public int Count;
        }

        private readonly ColonyHudContext _context;
        private readonly VisualElement _root;
        private readonly Group _workers;
        private readonly Group _haulers;

        public StaffList(VisualElement container, ColonyHudContext context)
        {
            _context = context;
            _root = container;
            _workers = CreateGroup();
            _haulers = CreateGroup();
            Hide();
        }

        public bool IsShown => Ui.IsShown(_root);
        public Button ReleaseAllWorkers => _workers.ReleaseAll;
        public Button ReleaseAllHaulers => _haulers.ReleaseAll;
        public string WorkersCaption => _workers.Caption.text;
        public string HaulersCaption => _haulers.Caption.text;
        public IReadOnlyList<Button> WorkerButtons => Buttons(_workers);
        public IReadOnlyList<Button> HaulerButtons => Buttons(_haulers);
        public IReadOnlyList<string> WorkerIds => _workers.Ids;
        public IReadOnlyList<string> HaulerIds => _haulers.Ids;

        public void Show(BuildingSnapshot building, GameSnapshot snapshot)
        {
            _workers.Ids.Clear();
            _haulers.Ids.Clear();
            _workers.Count = 0;
            _haulers.Count = 0;
            foreach (var unit in snapshot.Units)
            {
                var a = unit.Assignment;
                if ((a.Kind == AssignmentKind.Work || a.Kind == AssignmentKind.ToWork) && a.BuildingId == building.Id)
                    AddRow(_workers, unit, a.Kind == AssignmentKind.Work ? "работает" : "идёт на работу");
                else if (a.Kind == AssignmentKind.Haul && (a.SourceId == building.Id || a.DestinationId == building.Id))
                    AddRow(_haulers, unit, HaulText(building, a, snapshot));
            }
            EndGroup(_workers, building.IsWorkplace,
                building.MaxWorkers > 0 ? $"РАБОЧИЕ · {_workers.Count} / {building.MaxWorkers}" : $"РАБОЧИЕ · {_workers.Count}",
                "Никто не работает: выделите существ → «Работа» → это здание.");
            EndGroup(_haulers, false, $"НОСИЛЬЩИКИ · {_haulers.Count}", null);
            Ui.Show(_root, Ui.IsShown(_workers.Root) || Ui.IsShown(_haulers.Root));
        }

        public void Hide() => Ui.Show(_root, false);

        private string HaulText(BuildingSnapshot building, Assignment assignment, GameSnapshot snapshot)
        {
            string cargo = _context.Session.DescribeCargo(assignment);
            if (assignment.SourceId == building.Id)
                return $"увозит → {NameOf(snapshot, assignment.DestinationId)} · {cargo}";
            return $"привозит из «{NameOf(snapshot, assignment.SourceId)}» · {cargo}";
        }

        private static string NameOf(GameSnapshot snapshot, string buildingId)
        {
            foreach (var building in snapshot.Buildings)
                if (building.Id == buildingId) return building.Name;
            return "?";
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
            Ui.SetText(row.Name, $"{(definition != null ? definition.DisplayName : unit.Name)} №{unit.Number}");
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
            group.Caption = Ui.Text(string.Empty, "staff__caption t-caption");
            header.Add(group.Caption);
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
