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
        ChoosingHaulCargo,
        ChoosingHaulDestination,
        ManagingLand
    }

    /// <summary>What the land mode offers for the block the player picked.</summary>
    public enum LandOffer
    {
        None,
        Buy,
        Clear
    }

    public class InteractionMode
    {
        public InteractionModeType Type { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public BuildingKind BuildingKind { get; private set; }
        public int Amount { get; private set; }
        public string SourceId { get; private set; }
        public string BuildingId { get; private set; }
        /// <summary>Land mode: the picked block, -1 when none is picked.</summary>
        public int LandX { get; private set; } = -1;
        public int LandY { get; private set; } = -1;
        public LandOffer LandOffer { get; private set; }

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
        public static InteractionMode ChoosingHaulCargo(string sourceId) => new()
        {
            Type = InteractionModeType.ChoosingHaulCargo,
            SourceId = sourceId
        };
        public static InteractionMode ChoosingHaulDestination(string sourceId) => new()
        {
            Type = InteractionModeType.ChoosingHaulDestination,
            SourceId = sourceId
        };
        public static InteractionMode ManagingLand(int x = -1, int y = -1, LandOffer offer = LandOffer.None) => new()
        {
            Type = InteractionModeType.ManagingLand,
            LandX = offer == LandOffer.None ? -1 : x,
            LandY = offer == LandOffer.None ? -1 : y,
            LandOffer = offer
        };
    }

    public class InteractionController
    {
        private readonly GameSession _session;
        private readonly HashSet<string> _selected = new();
        // Goods the haul order being given may take; empty means everything the route carries.
        private readonly List<ResourceKind> _haulCargo = new();
        private InteractionMode _mode = InteractionMode.Neutral;
        private string _message = "Постройте шахту и наймите рабочих.";
        private string _inspectedBuildingId = null;
        private string _inspectedUnitId = null;
        private bool _commandsOpen = false;
        private int _stackQuantity = 1;

        public event Action OnInteractionChanged;
        /// <summary>The player's intent was refused before reaching the session (no selection, wrong target…).</summary>
        public event Action<string> OnRefused;
        /// <summary>The player asked to see a place on the map (the next idle creature); the camera goes there.</summary>
        public event Action<WorldPosition> FocusRequested;
        private string _lastIdleId;

        public InteractionController(GameSession session)
        {
            _session = session;
            _session.OnSnapshotChanged += DropUnselectable;
            var quest = session.CurrentSnapshot.Progress.Quest;
            if (quest != null) _message = $"Задание «{quest.Title}»: что делать — в карточке слева вверху.";
        }

        public InteractionMode Mode => _mode;
        public string Message => _message;
        public IReadOnlyCollection<string> SelectedIds => _selected;
        public string InspectedBuildingId => _inspectedBuildingId;
        public string InspectedUnitId => _inspectedUnitId;
        public bool CommandsOpen => _commandsOpen;
        public int StackQuantity => _stackQuantity;
        /// <summary>Goods chosen for the haul order being given; empty means everything the route carries.</summary>
        public IReadOnlyList<ResourceKind> HaulCargo => _haulCargo;

        /// <summary>
        /// Goods the haul source picked in this order hands out, so the player can choose what to carry; empty
        /// before a source is picked.
        /// </summary>
        public IReadOnlyList<ResourceKind> HaulCargoChoices
        {
            get
            {
                if (!IsHaulStepAfterSource(_mode.Type)) return Array.Empty<ResourceKind>();
                var source = FindBuilding(_mode.SourceId);
                return source != null
                    ? ColonySimulation.ProvidedResources(source.Kind, _session.Catalog)
                    : (IReadOnlyList<ResourceKind>)Array.Empty<ResourceKind>();
            }
        }

        /// <summary>Whether some building takes the chosen cargo from the haul source; the order can go on.</summary>
        public bool HaulCargoHasDestination =>
            IsHaulStepAfterSource(_mode.Type) && HaulDestinationIds(_mode.SourceId, _haulCargo).Count > 0;

        /// <summary>Adds a good to the haul order's cargo, or takes it out again.</summary>
        public void ToggleHaulCargo(ResourceKind resource)
        {
            if (_mode.Type != InteractionModeType.ChoosingHaulCargo) return;
            if (!_haulCargo.Remove(resource)) _haulCargo.Add(resource);
            _haulCargo.Sort();
            _message = CargoMessage(string.Empty);
            Emit();
        }

        /// <summary>The haul order takes whatever its route can carry.</summary>
        public void CarryEverything()
        {
            if (_mode.Type != InteractionModeType.ChoosingHaulCargo) return;
            _haulCargo.Clear();
            _message = CargoMessage(string.Empty);
            Emit();
        }

        /// <summary>The cargo is settled; the player now picks on the map where to carry it.</summary>
        public void ConfirmHaulCargo()
        {
            if (_mode.Type != InteractionModeType.ChoosingHaulCargo) return;
            if (!HaulCargoHasDestination)
            {
                Refuse("Ни одно здание не примет всё выбранное сразу. Уберите лишнее или выберите «Всё».");
                return;
            }
            _mode = InteractionMode.ChoosingHaulDestination(_mode.SourceId);
            _message = CargoMessage("Кликните на карте по зданию, куда носить.");
            Emit();
        }

        /// <summary>Back from picking the destination to the cargo choice; what was chosen stays chosen.</summary>
        public void ChangeHaulCargo()
        {
            if (_mode.Type != InteractionModeType.ChoosingHaulDestination) return;
            _mode = InteractionMode.ChoosingHaulCargo(_mode.SourceId);
            _message = CargoMessage(string.Empty);
            Emit();
        }

        /// <summary>Takes the given creatures off their work or route; they stay in the colony, free.</summary>
        public void ReleaseUnits(IReadOnlyList<string> unitIds)
        {
            if (unitIds == null || unitIds.Count == 0) return;
            var result = _session.Dispatch(new ReleaseUnitsCommand(new List<string>(unitIds)));
            _message = result.Ok ? $"Снято с работы: {unitIds.Count}." : result.Error;
            Emit();
        }

        /// <summary>
        /// Whether the player can pick this creature: it exists and is not inside a building. A worker that
        /// reached its workplace works inside, out of sight and out of reach, until released from the building.
        /// </summary>
        public bool IsSelectable(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return false;
            foreach (var unit in _session.CurrentSnapshot.Units)
                if (unit.Id == unitId) return IsSelectable(unit);
            return false;
        }

        /// <summary>Every creature the player can pick right now, read from one snapshot.</summary>
        public HashSet<string> SelectableUnitIds()
        {
            var ids = new HashSet<string>();
            foreach (var unit in _session.CurrentSnapshot.Units)
                if (IsSelectable(unit)) ids.Add(unit.Id);
            return ids;
        }

        public static bool IsSelectable(UnitSnapshot unit) =>
            unit != null && unit.Assignment.Kind != AssignmentKind.Work;

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

        /// <summary>
        /// Selects the idle creature after the one picked last time (the first again after the last) and asks the
        /// camera to go to it.
        /// </summary>
        public void SelectNextIdle()
        {
            var units = _session.CurrentSnapshot.Units;
            var idle = new List<UnitSnapshot>();
            foreach (var unit in units)
                if (unit.Assignment.Kind == AssignmentKind.Idle) idle.Add(unit);
            UnitSnapshot found = null;
            if (idle.Count > 0)
            {
                int last = idle.FindIndex(unit => unit.Id == _lastIdleId);
                found = idle[(last + 1) % idle.Count];
            }
            _lastIdleId = found?.Id;
            Select(found != null ? new[] { found.Id } : Array.Empty<string>(), false,
                "Свободных существ нет.",
                idle.Count > 1 ? $"Свободное существо: {found?.Name}. Ещё раз — следующее." : $"Свободное существо: {found?.Name}.");
            if (found != null) FocusRequested?.Invoke(found.Position);
        }

        /// <summary>Asks the camera to go to a place of the map (the tutorial pointer's target off the screen).</summary>
        public void Focus(WorldPosition position) => FocusRequested?.Invoke(position);

        public void BeginUnitPlacement(UnitKind kind, int amount)
        {
            if (!_session.IsUnitUnlocked(kind))
            {
                Refuse($"Существо «{_session.Catalog.GetUnit(kind).DisplayName}» откроется за задание.");
                return;
            }
            _commandsOpen = false;
            // the tutorial's first worker lands by the warehouse door at once: the same hire, the place chosen for it
            if (QuickFirstHire(kind, amount)) return;
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

        /// <summary>
        /// Whether hiring this kind now is the tutorial's first worker: the current quest is a tutorial step whose next
        /// goal hires this kind, and the colony has no creature yet. That hire goes by the warehouse door at once
        /// (specs/006-tutorial-guidance, FR-005).
        /// </summary>
        public bool IsQuickFirstHire(UnitKind kind)
        {
            var snapshot = _session.CurrentSnapshot;
            if (snapshot.Units.Count > 0) return false;
            var progress = snapshot.Progress;
            var quest = progress.Enabled ? progress.Quest : null;
            if (quest == null || !quest.IsTutorial || quest.IsComplete) return false;
            var next = quest.NextGoal;
            if (next == null) return false;
            var goal = next.Goal;
            return goal.Kind == QuestGoalKind.OwnUnits && !goal.AnyUnit && goal.Unit == kind;
        }

        // the first worker: hired on the free cell nearest the warehouse door, and the camera goes there
        private bool QuickFirstHire(UnitKind kind, int amount)
        {
            if (!IsQuickFirstHire(kind)) return false;
            var cell = TutorialPlaces.HireCell(_session, kind, amount);
            if (cell == null) return false;
            var result = _session.Dispatch(new BuyUnitsCommand(kind, amount, cell.Value));
            if (!result.Ok)
            {
                Refuse(result.Error);
                return true;
            }
            _mode = InteractionMode.Neutral;
            _message = $"{_session.Catalog.GetUnit(kind).DisplayName} нанят и стоит у склада.";
            Emit();
            FocusRequested?.Invoke(TutorialPlaces.CenterOf(cell.Value, _session.Catalog));
            return true;
        }

        public void BeginBuildingPlacement(BuildingKind kind)
        {
            if (!_session.IsBuildingUnlocked(kind))
            {
                Refuse($"Постройка «{_session.Catalog.GetBuilding(kind).DisplayName}» откроется за задание.");
                return;
            }
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

        /// <summary>
        /// Land mode (L, the HUD's "Земля"): the map marks the blocks that can be bought; a click on one offers to buy
        /// it, a click on the colony's wild land offers to clear it. Nothing is built in this mode. Again: leaves it.
        /// </summary>
        public void ToggleLandMode()
        {
            if (_mode.Type == InteractionModeType.ManagingLand)
            {
                _mode = InteractionMode.Neutral;
                _message = "Режим земли закрыт.";
                Emit();
                return;
            }
            var land = _session.CurrentSnapshot.Land;
            if (land == null)
            {
                Refuse("Здесь земля не покупается.");
                return;
            }
            _commandsOpen = false;
            _mode = InteractionMode.ManagingLand();
            _message = LandPrompt(land);
            Emit();
        }

        /// <summary>Land mode: the player clicked block (x, y) of the land.</summary>
        public void ChooseLandBlock(int x, int y)
        {
            if (_mode.Type != InteractionModeType.ManagingLand) return;
            var land = _session.CurrentSnapshot.Land;
            if (land == null) return;
            if (!land.Inside(x, y))
            {
                Refuse("За краем острова земли нет.");
                return;
            }
            var block = land.Block(x, y);
            if (block.Cleared)
            {
                _mode = InteractionMode.ManagingLand();
                _message = "Эта земля уже расчищена: здесь можно строить.";
            }
            else if (block.Clearing)
            {
                _mode = InteractionMode.ManagingLand();
                _message = $"Участок расчищают: осталось {Seconds(block.ClearSecondsLeft)} с.";
            }
            else if (block.Owned)
            {
                _mode = InteractionMode.ManagingLand(x, y, LandOffer.Clear);
                var clear = _session.CanClearLand(x, y);
                _message = $"Расчистить участок: {Seconds(land.ClearSeconds)} с" +
                           (land.ClearGold > 0 ? $", {land.ClearGold} зол." : ", бесплатно") +
                           (clear.Ok ? ". Пока лес не сведён, строить здесь нельзя." : $". {clear.Error}.");
            }
            else if (block.CanBuy)
            {
                _mode = InteractionMode.ManagingLand(x, y, LandOffer.Buy);
                var buy = _session.CanBuyLand(x, y);
                _message = buy.Ok
                    ? $"Купить участок {land.BlockSize}×{land.BlockSize} за {land.NextPrice} зол.? Он поднимется из облаков диким."
                    : $"Участок стоит {land.NextPrice} зол. {buy.Error}.";
            }
            else
            {
                // the session refuses it with the reason, where the player clicked
                _mode = InteractionMode.ManagingLand();
                var refused = _session.Dispatch(new BuyLandCommand(x, y));
                _message = refused.Ok ? LandPrompt(_session.CurrentSnapshot.Land) : refused.Error;
            }
            Emit();
        }

        /// <summary>Land mode: buys or clears the picked block.</summary>
        public CommandResult ConfirmLand()
        {
            if (_mode.Type != InteractionModeType.ManagingLand || _mode.LandOffer == LandOffer.None)
                return CommandResult.Fail("Сначала выберите участок");
            int x = _mode.LandX, y = _mode.LandY;
            bool buy = _mode.LandOffer == LandOffer.Buy;
            var result = _session.Dispatch(buy ? new BuyLandCommand(x, y) : new ClearLandCommand(x, y));
            if (result.Ok)
            {
                _mode = InteractionMode.ManagingLand();
                _message = buy
                    ? "Участок куплен и поднимается из облаков. Расчистите его, чтобы строить."
                    : $"Расчистка началась: деревья и камни уберут за {Seconds(_session.Catalog.Economy.LandClearSeconds)} с.";
            }
            else
            {
                _message = result.Error;
            }
            Emit();
            return result;
        }

        private static string LandPrompt(LandSnapshot land) =>
            $"Участки в рамке продаются: следующий за {land.NextPrice} зол. Своя дикая земля расчищается кликом.";

        private static int Seconds(float seconds) => (int)Math.Ceiling(seconds);

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
            _haulCargo.Clear();
            _message = "Сначала укажите, откуда носить товар.";
            Emit();
        }

        /// <summary>
        /// Why the inspected building's "Вывозить на склад" cannot go now, or null when it can. It gives the nearest
        /// idle creature (one the current quest counts, when the quest asks for this haul) a route from the building
        /// to the nearest warehouse with everything the route carries: the same order as "Перенос".
        /// </summary>
        public string HaulToWarehouseBlocker => PlanHaulToWarehouse(out _, out _, out _);

        /// <summary>The inspected building's "Вывозить на склад", see <see cref="HaulToWarehouseBlocker"/>.</summary>
        public void HaulInspectedToWarehouse()
        {
            string blocker = PlanHaulToWarehouse(out var unit, out var source, out var warehouse);
            if (blocker != null)
            {
                Refuse(blocker);
                return;
            }
            var result = _session.Dispatch(new AssignHaulCommand(new[] { unit.Id }, source.Id, warehouse.Id));
            if (!result.Ok)
            {
                Refuse(result.Error);
                return;
            }
            _message = $"{unit.Name} носит на склад: {source.Name}.";
            Emit();
        }

        private string PlanHaulToWarehouse(out UnitSnapshot unit, out BuildingSnapshot source, out BuildingSnapshot warehouse)
        {
            unit = null;
            warehouse = null;
            source = FindBuilding(_inspectedBuildingId);
            if (source == null) return "Сначала выберите здание.";
            var snapshot = _session.CurrentSnapshot;
            var from = TutorialPlaces.CenterOf(source, _session.Catalog);
            float best = float.MaxValue;
            foreach (var building in snapshot.Buildings)
            {
                if (building.Kind != BuildingKind.Warehouse || building.Id == source.Id ||
                    !ColonySimulation.IsValidHaulRoute(source.Kind, building.Kind, _session.Catalog)) continue;
                var at = TutorialPlaces.CenterOf(building, _session.Catalog);
                float distance = (at.X - from.X) * (at.X - from.X) + (at.Y - from.Y) * (at.Y - from.Y);
                if (distance >= best) continue;
                best = distance;
                warehouse = building;
            }
            if (warehouse == null) return "Отсюда на склад не носят.";
            // the quest's haul counts its kind of creature; otherwise anyone free will do
            var quest = snapshot.Progress.Enabled ? snapshot.Progress.Quest : null;
            var next = quest?.NextGoal;
            var goal = next != null ? next.Goal : default;
            bool asked = next != null && goal.Kind == QuestGoalKind.HaulRoute && goal.Building == source.Kind &&
                         goal.Destination == BuildingKind.Warehouse;
            Func<UnitKind, bool> counts = asked ? goal.CountsUnit : (Func<UnitKind, bool>)null;
            unit = TutorialPlaces.NearestIdle(snapshot, counts, from);
            if (unit != null) return null;
            if (asked && !goal.AnyUnit)
                return $"Нет свободного существа «{_session.Catalog.GetUnit(goal.Unit).DisplayName}»: найми его в каталоге.";
            return "Нет свободных существ: найми работника в каталоге.";
        }

        /// <summary>Takes a won battle's gold into the treasury.</summary>
        public CommandResult ClaimBattleReward()
        {
            var reward = _session.CurrentSnapshot.BattleReward;
            var result = _session.Dispatch(new ClaimBattleRewardCommand());
            _message = result.Ok ? $"Награда за бой: +{reward?.Gold ?? 0} зол." : result.Error;
            Emit();
            return result;
        }

        /// <summary>Takes the finished quest's rewards; the next quest begins at once.</summary>
        public CommandResult ClaimQuestReward()
        {
            var headline = _session.CurrentSnapshot.Progress.Quest?.Headline;
            var result = _session.Dispatch(new ClaimQuestRewardCommand());
            _message = result.Ok
                ? headline != null ? $"Награда получена: {headline.Title}." : "Награда получена."
                : result.Error;
            Emit();
            return result;
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

        /// <summary>Raises a colony upgrade one level (the guild's and the barracks' panels).</summary>
        public void BuyUpgrade(string upgradeId)
        {
            var result = _session.Dispatch(new BuyUpgradeCommand(upgradeId));
            _message = result.Ok ? "Улучшение куплено." : result.Error;
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

            if (_mode.Type == InteractionModeType.ChoosingHaulCargo)
            {
                Refuse("Сначала выберите, что носить, и нажмите «Куда носить».");
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

            // every haul order asks what to take: each source hands out its own goods
            if (_mode.Type == InteractionModeType.ChoosingHaulSource)
            {
                _mode = InteractionMode.ChoosingHaulCargo(buildingId);
                _haulCargo.Clear();
                _message = CargoMessage(string.Empty);
                Emit();
                return;
            }

            if (_mode.Type == InteractionModeType.ChoosingHaulDestination)
            {
                var ids = new List<string>(_selected);
                var cargo = new List<ResourceKind>(_haulCargo);
                var result = _session.Dispatch(new AssignHaulCommand(ids, _mode.SourceId, buildingId, cargo));
                FinishCommand(result.Ok, result.Ok
                    ? cargo.Count == 0 ? "Постоянный маршрут назначен." : $"Маршрут назначен, носят: {CargoNames(cargo)}."
                    : result.Error);
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
            // the land mode first drops a picked block, then closes
            if (_mode.Type == InteractionModeType.ManagingLand && _mode.LandOffer != LandOffer.None)
            {
                _mode = InteractionMode.ManagingLand();
                _message = "Выберите участок.";
            }
            else if (_mode.Type != InteractionModeType.Neutral)
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
                list = HaulDestinationIds(_mode.SourceId, _haulCargo);
            }

            return list;
        }

        // Buildings a haul from the source can end at; with goods chosen, only those that take all of them.
        private List<string> HaulDestinationIds(string sourceId, List<ResourceKind> cargo)
        {
            var list = new List<string>();
            var src = FindBuilding(sourceId);
            if (src == null) return list;
            foreach (var dest in _session.CurrentSnapshot.Buildings)
            {
                if (dest.Id == src.Id || !ColonySimulation.IsValidHaulRoute(src.Kind, dest.Kind, _session.Catalog))
                    continue;
                if (cargo.Count > 0)
                {
                    var carriable = ColonySimulation.CarriableResources(src.Kind, dest.Kind, _session.Catalog);
                    if (!cargo.TrueForAll(carriable.Contains)) continue;
                }
                list.Add(dest.Id);
            }
            return list;
        }

        private static bool IsHaulStepAfterSource(InteractionModeType mode) =>
            mode == InteractionModeType.ChoosingHaulCargo || mode == InteractionModeType.ChoosingHaulDestination;

        private string CargoMessage(string next)
        {
            string cargo = _haulCargo.Count == 0 ? "Носить всё, что есть." : $"Носить: {CargoNames(_haulCargo)}.";
            return string.IsNullOrEmpty(next) ? cargo : cargo + " " + next;
        }

        private string CargoNames(IReadOnlyList<ResourceKind> cargo)
        {
            var names = new List<string>(cargo.Count);
            foreach (var resource in cargo) names.Add(_session.ResourceName(resource).ToLowerInvariant());
            return string.Join(", ", names);
        }

        private BuildingSnapshot FindBuilding(string buildingId)
        {
            foreach (var building in _session.CurrentSnapshot.Buildings)
                if (building.Id == buildingId) return building;
            return null;
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

        // Selectable creatures only: those inside a building cannot be picked.
        private bool SnapshotContainsUnit(string unitId) => IsSelectable(unitId);

        // Creatures that went inside a building, were sold or fell drop out of the selection; an order that
        // was waiting for a target is called off once nobody is left to give it to.
        private void DropUnselectable(GameSnapshot snapshot)
        {
            if (_selected.Count == 0 && _inspectedUnitId == null) return;
            var selectable = new HashSet<string>();
            foreach (var unit in snapshot.Units)
                if (IsSelectable(unit)) selectable.Add(unit.Id);
            int before = _selected.Count;
            _selected.RemoveWhere(id => !selectable.Contains(id));
            bool inspectedGone = _inspectedUnitId != null && !selectable.Contains(_inspectedUnitId);
            if (inspectedGone) _inspectedUnitId = null;
            if (_selected.Count == before && !inspectedGone) return;
            if (_selected.Count == 0)
            {
                _commandsOpen = false;
                if (_mode.Type == InteractionModeType.ChoosingWorkTarget ||
                    _mode.Type == InteractionModeType.ChoosingHaulSource ||
                    IsHaulStepAfterSource(_mode.Type))
                {
                    _mode = InteractionMode.Neutral;
                    _message = "Выбранные существа ушли в здание.";
                }
            }
            Emit();
        }

        private void Emit()
        {
            OnInteractionChanged?.Invoke();
        }
    }
}
