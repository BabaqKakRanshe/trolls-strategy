using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public enum InteractionModeType
    {
        Neutral,
        PlacingMine,
        PlacingUnits,
        ChoosingWorkTarget,
        ChoosingHaulSource,
        ChoosingHaulDestination
    }

    public class InteractionMode
    {
        public InteractionModeType Type { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public int Amount { get; private set; }
        public string SourceId { get; private set; }

        public static InteractionMode Neutral => new() { Type = InteractionModeType.Neutral };
        public static InteractionMode PlacingMine => new() { Type = InteractionModeType.PlacingMine };
        public static InteractionMode PlacingUnits(UnitKind kind, int amount) => new() { Type = InteractionModeType.PlacingUnits, UnitKind = kind, Amount = amount };
        public static InteractionMode ChoosingWorkTarget => new() { Type = InteractionModeType.ChoosingWorkTarget };
        public static InteractionMode ChoosingHaulSource => new() { Type = InteractionModeType.ChoosingHaulSource };
        public static InteractionMode ChoosingHaulDestination(string sourceId) => new() { Type = InteractionModeType.ChoosingHaulDestination, SourceId = sourceId };
    }

    public class InteractionController
    {
        private readonly GameSession _session;
        private readonly HashSet<string> _selected = new();
        private InteractionMode _mode = InteractionMode.Neutral;
        private string _message = "Постройте шахту и наймите рабочих.";
        private string _inspectedBuildingId = null;
        private string _inspectedUnitId = null;
        private bool _commandsOpen = false;
        private int _stackQuantity = 1;

        public event Action OnInteractionChanged;

        public InteractionController(GameSession session)
        {
            _session = session;
        }

        public InteractionMode Mode => _mode;
        public string Message => _message;
        public IReadOnlyCollection<string> SelectedIds => _selected;
        public string InspectedBuildingId => _inspectedBuildingId;
        public string InspectedUnitId => _inspectedUnitId;
        public bool CommandsOpen => _commandsOpen;
        public int StackQuantity => _stackQuantity;

        public void ClickUnit(string unitId, bool additive)
        {
            if (!additive) _selected.Clear();
            if (additive && _selected.Contains(unitId))
                _selected.Remove(unitId);
            else
                _selected.Add(unitId);

            _mode = InteractionMode.Neutral;
            _inspectedBuildingId = null;
            _inspectedUnitId = _selected.Count == 1 ? unitId : null;
            _message = _selected.Count == 0 ? "Выбор снят." : $"Выбрано юнитов: {_selected.Count}";
            Emit();
        }

        public void SelectUnits(IEnumerable<string> unitIds)
        {
            _selected.Clear();
            int count = 0;
            if (unitIds != null)
            {
                foreach (var id in unitIds)
                {
                    _selected.Add(id);
                    count++;
                }
            }

            _mode = InteractionMode.Neutral;
            _message = count == 0 ? "В рамке нет юнитов." : $"Выбрано рамкой: {count}";
            Emit();
        }

        public void SelectFirstIdle(int amount)
        {
            var units = _session.CurrentSnapshot.Units;
            var ids = new List<string>();
            for (int i = 0; i < units.Count && ids.Count < amount; i++)
            {
                if (units[i].Assignment.Kind == AssignmentKind.Idle)
                    ids.Add(units[i].Id);
            }
            SelectUnits(ids);
        }

        public void SelectNextIdle()
        {
            var units = _session.CurrentSnapshot.Units;
            string foundId = null;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Assignment.Kind == AssignmentKind.Idle)
                {
                    foundId = units[i].Id;
                    break;
                }
            }
            SelectUnits(foundId != null ? new[] { foundId } : Array.Empty<string>());
        }

        public void BeginUnitPlacement(UnitKind kind, int amount)
        {
            _mode = InteractionMode.PlacingUnits(kind, amount);
            _message = $"Кликните по клетке, где появятся все {amount} существ.";
            Emit();
        }

        public void PlaceUnits(Cell cell)
        {
            if (_mode.Type != InteractionModeType.PlacingUnits) return;
            var result = _session.Dispatch(new BuyUnitsCommand(_mode.UnitKind, _mode.Amount, cell));
            if (result.Ok)
            {
                _message = $"Нанято существ: {_mode.Amount}.";
                _mode = InteractionMode.Neutral;
            }
            else
            {
                _message = result.Error;
            }
            Emit();
        }

        public void BeginMinePlacement()
        {
            _mode = InteractionMode.PlacingMine;
            _message = "Выберите свободные клетки для шахты.";
            Emit();
        }

        public void PlaceMine(Cell cell)
        {
            var result = _session.Dispatch(new BuildMineCommand(cell));
            if (result.Ok)
            {
                _message = "Шахта построена. Теперь назначьте рабочих.";
                _mode = InteractionMode.Neutral;
            }
            else
            {
                _message = result.Error;
            }
            Emit();
        }

        public void PlaceMineAutomatically()
        {
            var cell = _session.FindFirstMineCell();
            if (cell.HasValue)
                PlaceMine(cell.Value);
            else
            {
                _message = "На поле не осталось места для шахты.";
                Emit();
            }
        }

        public void BeginWorkTarget()
        {
            if (!HasSelection()) return;
            _mode = InteractionMode.ChoosingWorkTarget;
            _message = "Укажите шахту для выбранных рабочих.";
            Emit();
        }

        public void BeginHaulTarget()
        {
            if (!HasSelection()) return;
            _mode = InteractionMode.ChoosingHaulSource;
            _message = "Сначала укажите источник руды.";
            Emit();
        }

        public void RecruitUnit(UnitKind kind, int amount = 1)
        {
            var cell = _session.FindSpawnCell();
            var result = _session.Dispatch(new BuyUnitsCommand(kind, amount, cell));
            if (result.Ok)
            {
                var def = _session.Catalog.GetUnit(kind);
                string name = def != null ? def.DisplayName : kind.ToString();
                _message = $"{name} нанят в поселение!";
                _mode = InteractionMode.Neutral;
            }
            else
            {
                _message = result.Error;
            }
            Emit();
        }

        public void SelectBuilding(string buildingId)
        {
            _inspectedBuildingId = buildingId;
            _inspectedUnitId = null;
            BuildingSnapshot b = null;
            var bList = _session.CurrentSnapshot.Buildings;
            for (int i = 0; i < bList.Count; i++)
            {
                if (bList[i].Id == buildingId) { b = bList[i]; break; }
            }
            _message = b != null ? $"Выбрано: {b.Name}" : "Здание выбрано.";
            Emit();
        }

        public void ToggleCommands(bool open)
        {
            _commandsOpen = open;
            Emit();
        }

        public void CloseInspect()
        {
            _inspectedBuildingId = null;
            _inspectedUnitId = null;
            Emit();
        }

        public void SetStackQuantity(int qty)
        {
            _stackQuantity = UnityEngine.Mathf.Max(1, qty);
            Emit();
        }

        public void ConfirmStackSelection(IReadOnlyList<string> fullList)
        {
            if (fullList == null || fullList.Count == 0) return;
            int take = UnityEngine.Mathf.Clamp(_stackQuantity, 1, fullList.Count);
            var subset = new List<string>(take);
            for (int i = 0; i < take; i++) subset.Add(fullList[i]);
            SelectUnits(subset);
        }

        public void ChooseBuilding(string buildingId)
        {
            if (_mode.Type == InteractionModeType.Neutral)
            {
                SelectBuilding(buildingId);
                return;
            }

            var validTargets = GetTargetBuildingIds();
            if (!validTargets.Contains(buildingId))
            {
                _message = "Это здание нельзя выбрать для текущего шага.";
                Emit();
                return;
            }

            if (_mode.Type == InteractionModeType.ChoosingWorkTarget)
            {
                var ids = new List<string>(_selected);
                var result = _session.Dispatch(new AssignWorkCommand(ids, buildingId));
                if (!result.Ok)
                {
                    FinishCommand(false, result.Error);
                    return;
                }

                int assignedCount = 0;
                var units = _session.CurrentSnapshot.Units;
                for (int i = 0; i < units.Count; i++)
                {
                    if (_selected.Contains(units[i].Id) &&
                        (units[i].Assignment.Kind == AssignmentKind.ToWork || units[i].Assignment.Kind == AssignmentKind.Work) &&
                        units[i].Assignment.BuildingId == buildingId)
                    {
                        assignedCount++;
                    }
                }

                string suffix = assignedCount < ids.Count ? " Остальные остались на прежних задачах." : "";
                FinishCommand(true, $"В шахту назначено {assignedCount} из {ids.Count}.{suffix}");
                return;
            }

            if (_mode.Type == InteractionModeType.ChoosingHaulSource)
            {
                _mode = InteractionMode.ChoosingHaulDestination(buildingId);
                _message = "Теперь укажите склад или рынок.";
                Emit();
                return;
            }

            if (_mode.Type == InteractionModeType.ChoosingHaulDestination)
            {
                var ids = new List<string>(_selected);
                var result = _session.Dispatch(new AssignHaulCommand(ids, _mode.SourceId, buildingId));
                FinishCommand(result.Ok, result.Ok ? "Постоянный маршрут назначен." : result.Error);
            }
        }

        public void SellSelected()
        {
            if (!HasSelection()) return;
            var ids = new List<string>(_selected);
            var result = _session.Dispatch(new SellUnitsCommand(ids));
            if (result.Ok)
            {
                _selected.Clear();
                _inspectedUnitId = null;
                _commandsOpen = false;
                FinishCommand(true, "Юниты проданы за 50% стоимости.");
            }
            else
            {
                FinishCommand(false, result.Error);
            }
        }

        public void SendSelectedToBarracks()
        {
            if (!HasSelection()) return;
            var ids = new List<string>(_selected);
            var result = _session.Dispatch(new SendToBarracksCommand(ids));
            _commandsOpen = false;
            FinishCommand(result.Ok, result.Ok ? "Юниты отправлены в бараки." : result.Error);
        }

        public void ReleaseSelected()
        {
            if (!HasSelection()) return;
            var ids = new List<string>(_selected);
            var result = _session.Dispatch(new ReleaseUnitsCommand(ids));
            _commandsOpen = false;
            FinishCommand(result.Ok, result.Ok ? "Юниты освобождены от работы." : result.Error);
        }

        public void CancelOrClear()
        {
            if (_mode.Type != InteractionModeType.Neutral)
            {
                _mode = InteractionMode.Neutral;
                _message = "Команда отменена.";
            }
            else
            {
                _selected.Clear();
                _message = "Выбор снят.";
            }
            Emit();
        }

        public IReadOnlyList<string> GetTargetBuildingIds()
        {
            var buildings = _session.CurrentSnapshot.Buildings;
            var list = new List<string>();

            if (_mode.Type == InteractionModeType.ChoosingWorkTarget)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i].Kind == BuildingKind.Mine)
                        list.Add(buildings[i].Id);
                }
            }
            else if (_mode.Type == InteractionModeType.ChoosingHaulSource)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    var src = buildings[i];
                    bool hasDest = false;
                    for (int j = 0; j < buildings.Count; j++)
                    {
                        if (ColonySimulation.IsValidHaulRoute(src.Kind, buildings[j].Kind))
                        {
                            hasDest = true;
                            break;
                        }
                    }
                    if (hasDest) list.Add(src.Id);
                }
            }
            else if (_mode.Type == InteractionModeType.ChoosingHaulDestination)
            {
                BuildingSnapshot src = null;
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i].Id == _mode.SourceId)
                    {
                        src = buildings[i];
                        break;
                    }
                }
                if (src != null)
                {
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        var dest = buildings[i];
                        if (dest.Id != src.Id && ColonySimulation.IsValidHaulRoute(src.Kind, dest.Kind))
                            list.Add(dest.Id);
                    }
                }
            }

            return list;
        }

        private bool HasSelection()
        {
            if (_selected.Count > 0) return true;
            _message = "Сначала выберите хотя бы одного юнита.";
            Emit();
            return false;
        }

        private void FinishCommand(bool ok, string message)
        {
            _message = message;
            if (ok) _mode = InteractionMode.Neutral;
            Emit();
        }

        private void Emit()
        {
            OnInteractionChanged?.Invoke();
        }
    }
}
