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
        PlacingBuilding,
        MovingBuilding,
        PlacingUnits,
        ChoosingWorkTarget,
        ChoosingHaulSource,
        ChoosingHaulDestination
    }

    public class InteractionMode
    {
        public InteractionModeType Type { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public BuildingKind BuildingKind { get; private set; }
        public int Amount { get; private set; }
        public string SourceId { get; private set; }
        public string BuildingId { get; private set; }

        public static InteractionMode Neutral => new() { Type = InteractionModeType.Neutral };
        public static InteractionMode PlacingBuilding(BuildingKind kind) => new() { Type = InteractionModeType.PlacingBuilding, BuildingKind = kind };
        public static InteractionMode MovingBuilding(string buildingId, BuildingKind kind) => new()
        {
            Type = InteractionModeType.MovingBuilding,
            BuildingId = buildingId,
            BuildingKind = kind
        };
        public static InteractionMode PlacingUnits(UnitKind kind, int amount) => new() { Type = InteractionModeType.PlacingUnits, UnitKind = kind, Amount = amount };
        public static InteractionMode ChoosingWorkTarget => new() { Type = InteractionModeType.ChoosingWorkTarget };
        public static InteractionMode ChoosingHaulSource => new() { Type = InteractionModeType.ChoosingHaulSource };
        public static InteractionMode ChoosingHaulDestination(string sourceId) => new()
        {
            Type = InteractionModeType.ChoosingHaulDestination,
            SourceId = sourceId
        };
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
        /// <summary>The player's intent was refused before reaching the session (no selection, wrong target…).</summary>
        public event Action<string> OnRefused;

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
            if (!SnapshotContainsUnit(unitId)) return;
            if (!additive) _selected.Clear();
            if (additive && _selected.Contains(unitId))
                _selected.Remove(unitId);
            else
                _selected.Add(unitId);

            _mode = InteractionMode.Neutral;
            _commandsOpen = false;
            _inspectedBuildingId = null;
            _inspectedUnitId = null;
            _message = _selected.Count == 0 ? "Выбор снят." : $"Выбрано юнитов: {_selected.Count}";
            Emit();
        }

        public void InspectSquad(string unitId, IEnumerable<string> squadIds)
        {
            if (!SnapshotContainsUnit(unitId)) return;
            _selected.Clear();
            _selected.Add(unitId);
            if (squadIds != null)
            {
                foreach (var id in squadIds)
                {
                    if (SnapshotContainsUnit(id)) _selected.Add(id);
                }
            }

            _mode = InteractionMode.Neutral;
            _commandsOpen = false;
            _inspectedBuildingId = null;
            _inspectedUnitId = unitId;
            _message = $"Выбрано юнитов: {_selected.Count}";
            Emit();
        }

        public void SelectUnits(IEnumerable<string> unitIds, bool additive = false) =>
            Select(unitIds, additive, "В рамке нет юнитов.", "Выбрано рамкой: {0}");

        private void Select(IEnumerable<string> unitIds, bool additive, string noneMessage, string countMessage)
        {
            if (!additive) _selected.Clear();
            if (unitIds != null)
            {
                foreach (var id in unitIds)
                {
                    if (SnapshotContainsUnit(id)) _selected.Add(id);
                }
            }

            _mode = InteractionMode.Neutral;
            _commandsOpen = false;
            _inspectedBuildingId = null;
            _inspectedUnitId = null;
            _message = _selected.Count == 0 ? noneMessage : string.Format(countMessage, _selected.Count);
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
            Select(ids, false, "Свободных существ нет.", "Выбрано свободных: {0}");
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
            Select(foundId != null ? new[] { foundId } : Array.Empty<string>(), false,
                "Свободных существ нет.", "Выбрано свободное существо.");
        }

        public void BeginUnitPlacement(UnitKind kind, int amount)
        {
            _commandsOpen = false;
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

        public void BeginBuildingPlacement(BuildingKind kind)
        {
            _commandsOpen = false;
            _mode = InteractionMode.PlacingBuilding(kind);
            _message = $"Выберите свободные клетки: {_session.Catalog.GetBuilding(kind).DisplayName}.";
            Emit();
        }

        public void PlaceBuilding(Cell cell)
        {
            if (_mode.Type != InteractionModeType.PlacingBuilding) return;
            var kind = _mode.BuildingKind;
            var result = _session.Dispatch(new BuildBuildingCommand(kind, cell));
            if (result.Ok)
            {
                _message = kind == BuildingKind.Mine
                    ? "Шахта построена. Теперь назначьте рабочих."
                    : $"Построено: {_session.Catalog.GetBuilding(kind).DisplayName}.";
                _mode = InteractionMode.Neutral;
            }
            else
            {
                _message = result.Error;
            }
            Emit();
        }

        public void PlaceBuildingAutomatically()
        {
            if (_mode.Type != InteractionModeType.PlacingBuilding) return;
            var cell = _session.FindFirstBuildingCell(_mode.BuildingKind);
            if (cell.HasValue)
                PlaceBuilding(cell.Value);
            else
                Refuse("На поле не осталось места для этой постройки.");
        }

        public void BeginWorkTarget()
        {
            if (!HasSelection()) return;
            _commandsOpen = false;
            _mode = InteractionMode.ChoosingWorkTarget;
            _message = "Укажите производство для выбранных рабочих.";
            Emit();
        }

        public void BeginHaulTarget()
        {
            if (!HasSelection()) return;
            _commandsOpen = false;
            _mode = InteractionMode.ChoosingHaulSource;
            _message = "Сначала укажите, откуда носить товар.";
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
            _commandsOpen = false;
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
            _commandsOpen = open && _mode.Type == InteractionModeType.Neutral && _selected.Count > 0;
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


        public void UpgradeInspectedBuilding()
        {
            if (string.IsNullOrEmpty(_inspectedBuildingId)) return;
            var result = _session.Dispatch(new UpgradeBuildingCommand(_inspectedBuildingId));
            _message = result.Ok ? "Постройка улучшена." : result.Error;
            Emit();
        }

        public void DemolishInspectedBuilding()
        {
            if (string.IsNullOrEmpty(_inspectedBuildingId)) return;
            var result = _session.Dispatch(new DemolishBuildingCommand(_inspectedBuildingId));
            if (result.Ok) _inspectedBuildingId = null;
            _message = result.Ok ? "Постройка разобрана." : result.Error;
            Emit();
        }

        public void BeginMoveInspectedBuilding()
        {
            BuildingSnapshot building = null;
            var buildings = _session.CurrentSnapshot.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].Id == _inspectedBuildingId) { building = buildings[i]; break; }
            }
            if (building == null) return;

            _commandsOpen = false;
            _mode = InteractionMode.MovingBuilding(building.Id, building.Kind);
            _message = $"Кликните по новому месту: {building.Name}.";
            Emit();
        }

        public void MoveBuilding(Cell cell)
        {
            if (_mode.Type != InteractionModeType.MovingBuilding) return;
            var result = _session.Dispatch(new MoveBuildingCommand(_mode.BuildingId, cell));
            if (result.Ok)
            {
                _message = "Постройка перенесена.";
                _mode = InteractionMode.Neutral;
            }
            else
            {
                _message = result.Error;
            }
            Emit();
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
                Refuse("Это здание нельзя выбрать для текущего шага.");
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
                FinishCommand(true, $"На работу назначено {assignedCount} из {ids.Count}.{suffix}");
                return;
            }

            if (_mode.Type == InteractionModeType.ChoosingHaulSource)
            {
                _mode = InteractionMode.ChoosingHaulDestination(buildingId);
                _message = "Теперь укажите, куда доставлять товар.";
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
            int refund = SaleRefund(ids);
            var result = _session.Dispatch(new SellUnitsCommand(ids));
            if (result.Ok)
            {
                _selected.Clear();
                _inspectedUnitId = null;
                _commandsOpen = false;
                FinishCommand(true, $"Продано существ: {ids.Count}, получено {refund} зол.");
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
            _commandsOpen = false;
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
                    if (buildings[i].IsWorkplace)
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
                        if (j != i && ColonySimulation.IsValidHaulRoute(src.Kind, buildings[j].Kind, _session.Catalog))
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
                        if (dest.Id != src.Id && ColonySimulation.IsValidHaulRoute(src.Kind, dest.Kind, _session.Catalog))
                            list.Add(dest.Id);
                    }
                }
            }

            return list;
        }

        private int SaleRefund(ICollection<string> unitIds)
        {
            int refund = 0;
            foreach (var unit in _session.CurrentSnapshot.Units)
                if (unitIds.Contains(unit.Id))
                    refund += ColonySimulation.UnitSaleRefund(_session.Catalog.GetUnit(unit.UnitKind));
            return refund;
        }

        private bool HasSelection()
        {
            if (_selected.Count > 0) return true;
            Refuse("Сначала выберите хотя бы одного юнита.");
            return false;
        }

        /// <summary>An intent refused before any command was dispatched; presentation says no at once.</summary>
        private void Refuse(string message)
        {
            _message = message;
            OnRefused?.Invoke(message);
            Emit();
        }

        private void FinishCommand(bool ok, string message)
        {
            _message = message;
            _commandsOpen = false;
            if (ok) _mode = InteractionMode.Neutral;
            Emit();
        }

        private bool SnapshotContainsUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return false;
            var units = _session.CurrentSnapshot.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Id == unitId) return true;
            }
            return false;
        }

        private void Emit()
        {
            OnInteractionChanged?.Invoke();
        }
    }
}
