using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>What a board click, a drop or a squad press did, so the scene can answer it.</summary>
    public enum DeploymentResult
    {
        Placed,
        Moved,
        Selected,
        Deselected,
        Removed,
        Refused
    }

    public enum EquipmentChange
    {
        None,
        Equipped,
        Unequipped
    }

    /// <summary>
    /// The choices the player makes before a battle: who stands on which cell, who is selected and who
    /// wears what. Transient, like a colony interaction mode: nothing reaches the session until
    /// <see cref="Start"/> dispatches the StartBattleCommand. The player picks a kind of creature from the
    /// reserve; a click on a free deployment cell brings the next fighter of that kind; a click on a placed
    /// one selects it; a drop moves it, swaps it with another or takes it back to the reserve. Fighters of
    /// one kind fight alike, so the kind is the choice and the roster order decides which of them goes.
    /// Only a fighter on the board can be selected, and gear goes to the selected one.
    /// <see cref="Message"/> says what to do next or why the last action was refused.
    /// </summary>
    public sealed class BattleDeployment
    {
        private readonly BattleBoard _board;
        private readonly List<UnitSnapshot> _roster;
        private readonly Dictionary<string, UnitSnapshot> _units = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UnitKind> _kinds = new(StringComparer.Ordinal);
        private readonly List<EquipmentSnapshot> _equipment;
        private readonly Dictionary<string, EquipmentSnapshot> _items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);
        private readonly List<BattlePlacement> _placements = new();

        public BattleDeployment(GameSession session, BattleMissionDefinition mission, BattleBoard board)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            var snapshot = session.CurrentSnapshot;
            _roster = snapshot.Units.ToList();
            foreach (var unit in _roster)
            {
                _units[unit.Id] = unit;
                _kinds[unit.Id] = unit.UnitKind;
            }
            _equipment = snapshot.Equipment.ToList();
            foreach (var item in _equipment)
            {
                _items[item.Id] = item;
                _owners[item.Id] = item.OwnerUnitId;
            }
            Kinds = _roster.Select(unit => unit.UnitKind).Distinct().OrderBy(kind => kind).ToArray();
            PickedKind = NextKind();
            Message = NextStep();
        }

        /// <summary>After every change of placements, selection, the picked kind or gear.</summary>
        public event Action Changed;

        public GameSession Session { get; }
        public BattleMissionDefinition Mission { get; }
        public IReadOnlyList<UnitSnapshot> Roster => _roster;
        public IReadOnlyList<EquipmentSnapshot> Equipment => _equipment;
        /// <summary>Fighters on the board, in the order they came.</summary>
        public IReadOnlyList<BattlePlacement> Placements => _placements;
        public IReadOnlyDictionary<string, UnitKind> UnitKinds => _kinds;
        /// <summary>Every kind of creature the colony has, in the catalog order.</summary>
        public IReadOnlyList<UnitKind> Kinds { get; }
        /// <summary>Who a click on a free cell brings; null only while the whole roster is on the board.</summary>
        public UnitKind? PickedKind { get; private set; }
        /// <summary>A fighter on the board, or null.</summary>
        public string SelectedUnitId { get; private set; }
        public string Message { get; private set; }
        public int MaxUnits => Mission.MaxPlayerUnits;
        /// <summary>How many fighters this colony can bring: the mission's limit or the whole roster.</summary>
        public int SquadLimit => Math.Min(Mission.MaxPlayerUnits, _roster.Count);
        public bool CanPlaceMore => _placements.Count < SquadLimit;
        public bool CanStart => _placements.Count > 0;
        /// <summary>No mission won yet: a victory brings the first win's gold.</summary>
        public bool FirstWin => Session.FirstMissionWins == 0;
        /// <summary>The gold a victory here brings, as a range; the amount is rolled when it is won.</summary>
        public (int Min, int Max) WinGold => FirstWin
            ? (Mission.FirstWinGold, Mission.FirstWinGoldMax)
            : (Mission.RepeatWinGold, Mission.RepeatWinGoldMax);

        public bool IsPlaced(string unitId) => _placements.Exists(p => p.UnitId == unitId);

        /// <summary>Fighters of the kind still off the board.</summary>
        public int ReserveOf(UnitKind kind) => _roster.Count(unit => unit.UnitKind == kind && !IsPlaced(unit.Id));

        public Cell? CellOf(string unitId)
        {
            int index = _placements.FindIndex(p => p.UnitId == unitId);
            return index >= 0 ? _placements[index].Cell : null;
        }

        /// <summary>The fighter standing on the cell, or null.</summary>
        public string UnitAt(Cell cell)
        {
            int index = _placements.FindIndex(p => p.Cell == cell);
            return index >= 0 ? _placements[index].UnitId : null;
        }

        /// <summary>Who wears the item now: a unit id, or null while it lies in the inventory.</summary>
        public string OwnerOf(string itemId) => _owners.TryGetValue(itemId, out var owner) ? owner : null;

        public string UnitName(string unitId) => unitId != null && _units.TryGetValue(unitId, out var unit)
            ? unit.Name : "другого бойца";

        public UnitDefinition DefinitionOf(string unitId) => Session.Catalog.GetUnit(_kinds[unitId]);

        public UnitDefinition DefinitionOf(UnitKind kind) => Session.Catalog.GetUnit(kind);

        /// <summary>Damage and armour with the gear the unit wears in this deployment.</summary>
        public (int Damage, int Armor) GearedStats(string unitId)
        {
            var definition = DefinitionOf(unitId);
            int damage = definition.CombatDamage, armor = definition.CombatArmor;
            foreach (var item in _equipment)
                if (_owners[item.Id] == unitId)
                {
                    damage += item.DamageBonus;
                    armor += item.ArmorBonus;
                }
            return (damage, armor);
        }

        /// <summary>Picks who the next click on a free cell brings.</summary>
        public DeploymentResult Pick(UnitKind kind)
        {
            if (!Kinds.Contains(kind)) return Refuse("Таких бойцов в колонии нет");
            if (ReserveOf(kind) == 0) return Refuse($"{DefinitionOf(kind).DisplayName}: все уже на поле");
            PickedKind = kind;
            return Commit(DeploymentResult.Selected);
        }

        /// <summary>
        /// A click on the board: selects the fighter standing there, or brings the next fighter of the picked
        /// kind from the reserve. When that kind runs out, the next kind still in the reserve is picked.
        /// </summary>
        public DeploymentResult ClickCell(Cell cell)
        {
            string standing = UnitAt(cell);
            if (standing != null) return Select(standing);
            if (!_board.CanPlace(cell)) return Refuse(PlaceRefusal(cell));
            if (_placements.Count >= Mission.MaxPlayerUnits)
                return Refuse($"В отряде не больше {Mission.MaxPlayerUnits} бойцов");
            if (PickedKind is not UnitKind kind) return Refuse("Все бойцы уже на поле");
            return Bring(kind, cell);
        }

        /// <summary>
        /// A kind dragged from the reserve onto the board: a fighter of that kind takes the cell and the kind
        /// becomes the pick. One of another kind standing there goes back to the reserve with its gear taken
        /// off, so a full squad can still change who it brings.
        /// </summary>
        public DeploymentResult PlaceKind(UnitKind kind, Cell cell)
        {
            if (!Kinds.Contains(kind)) return Refuse("Таких бойцов в колонии нет");
            if (!_board.CanPlace(cell)) return Refuse(PlaceRefusal(cell));
            string standing = UnitAt(cell);
            if (standing != null && _kinds[standing] == kind)
            {
                SelectedUnitId = standing;
                return Commit(DeploymentResult.Selected);
            }
            if (ReserveOf(kind) == 0) return Refuse($"{DefinitionOf(kind).DisplayName}: все уже на поле");
            if (standing == null && _placements.Count >= Mission.MaxPlayerUnits)
                return Refuse($"В отряде не больше {Mission.MaxPlayerUnits} бойцов");
            if (standing != null) TakeOff(standing);
            PickedKind = kind;
            return Bring(kind, cell);
        }

        /// <summary>Selects a fighter on the board for its gear; selecting the selected one again lets it go.</summary>
        public DeploymentResult Select(string unitId)
        {
            if (unitId == null || !IsPlaced(unitId)) return Refuse("Этого бойца нет на поле");
            bool again = SelectedUnitId == unitId;
            SelectedUnitId = again ? null : unitId;
            return Commit(again ? DeploymentResult.Deselected : DeploymentResult.Selected);
        }

        public bool Deselect()
        {
            if (SelectedUnitId == null) return false;
            SelectedUnitId = null;
            Commit(DeploymentResult.Deselected);
            return true;
        }

        /// <summary>
        /// A fighter carried across the board: to a free deployment cell it moves, onto another fighter the two
        /// swap, off the board (<paramref name="cell"/> null) it goes back to the reserve with its gear taken off.
        /// </summary>
        public DeploymentResult Drop(string unitId, Cell? cell)
        {
            int from = _placements.FindIndex(p => p.UnitId == unitId);
            if (from < 0) return Refuse("Этого бойца нет на поле");
            if (cell == null) return Remove(unitId) ? DeploymentResult.Removed : DeploymentResult.Refused;
            var target = cell.Value;
            var origin = _placements[from].Cell;
            if (target != origin && !_board.CanPlace(target)) return Refuse(PlaceRefusal(target));
            SelectedUnitId = unitId;
            if (target == origin) return Commit(DeploymentResult.Selected);
            int other = _placements.FindIndex(p => p.Cell == target);
            if (other >= 0) _placements[other] = new BattlePlacement(_placements[other].UnitId, origin);
            _placements[from] = new BattlePlacement(unitId, target);
            return Commit(DeploymentResult.Moved);
        }

        /// <summary>Takes a fighter off the board back to the reserve; its gear returns to the inventory.</summary>
        public bool Remove(string unitId)
        {
            if (unitId == null || !IsPlaced(unitId)) return false;
            TakeOff(unitId);
            PickedKind ??= _kinds[unitId];
            Commit(DeploymentResult.Removed);
            return true;
        }

        public bool RemoveSelected() => Remove(SelectedUnitId);

        /// <summary>Puts reserve fighters on the first free deployment cells until the squad is full.</summary>
        public void AutoPlace()
        {
            foreach (var unit in _roster)
            {
                if (_placements.Count >= Mission.MaxPlayerUnits) break;
                if (IsPlaced(unit.Id)) continue;
                foreach (var cell in Mission.PlayerDeployment)
                {
                    if (!_board.CanPlace(cell) || _placements.Exists(p => p.Cell == cell)) continue;
                    _placements.Add(new BattlePlacement(unit.Id, cell));
                    break;
                }
            }
            if (PickedKind is UnitKind picked && ReserveOf(picked) == 0) PickedKind = NextKind();
            Commit(DeploymentResult.Placed);
        }

        /// <summary>Puts the item on the selected fighter, one per slot, or takes it off again.</summary>
        public EquipmentChange ToggleEquipment(string itemId)
        {
            if (SelectedUnitId == null || !_items.TryGetValue(itemId, out var item)) return EquipmentChange.None;
            EquipmentChange change;
            if (_owners[itemId] == SelectedUnitId)
            {
                _owners[itemId] = null;
                change = EquipmentChange.Unequipped;
            }
            else
            {
                foreach (var otherId in _owners.Keys.ToList())
                    if (_owners[otherId] == SelectedUnitId && _items[otherId].Slot == item.Slot)
                        _owners[otherId] = null;
                _owners[itemId] = SelectedUnitId;
                change = EquipmentChange.Equipped;
            }
            Commit(DeploymentResult.Selected);
            return change;
        }

        /// <summary>Commits the squad and gear; the session simulates the battle at once.</summary>
        public CommandResult Start()
        {
            var assignments = new List<BattleEquipmentAssignment>();
            foreach (var item in _equipment)
                if (_owners[item.Id] != item.OwnerUnitId)
                    assignments.Add(new BattleEquipmentAssignment(item.Id, _owners[item.Id]));
            var result = Session.Dispatch(new StartBattleCommand(Mission.MissionId, _placements.ToArray(), assignments));
            if (!result.Ok) Message = result.Error;
            return result;
        }

        /// <summary>Items worn in this deployment by fighters who fell.</summary>
        public int LostItems(IReadOnlyList<string> fallenUnitIds)
        {
            int lost = 0;
            foreach (var owner in _owners.Values)
                if (owner != null && fallenUnitIds.Contains(owner)) lost++;
            return lost;
        }

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        /// <summary>Developer quick battle: the roster fills the deployment and the first fighter takes one item per slot.</summary>
        public bool QuickFill()
        {
            int nextUnit = 0;
            foreach (var cell in Mission.PlayerDeployment)
            {
                if (nextUnit >= _roster.Count || _placements.Count >= Mission.MaxPlayerUnits) break;
                if (!_board.CanPlace(cell) || _placements.Exists(p => p.Cell == cell)) continue;
                _placements.Add(new BattlePlacement(_roster[nextUnit].Id, cell));
                nextUnit++;
            }
            if (_placements.Count == 0) return false;
            SelectedUnitId = _placements[0].UnitId;
            PickedKind = NextKind();
            var usedSlots = new HashSet<EquipmentSlot>();
            foreach (var item in _equipment)
                if (usedSlots.Add(item.Slot)) _owners[item.Id] = SelectedUnitId;
            Changed?.Invoke();
            return true;
        }
#endif

        // the next fighter of the kind takes the free cell and is selected; a spent kind hands the pick on
        private DeploymentResult Bring(UnitKind kind, Cell cell)
        {
            var next = _roster.First(unit => unit.UnitKind == kind && !IsPlaced(unit.Id));
            _placements.Add(new BattlePlacement(next.Id, cell));
            SelectedUnitId = next.Id;
            if (ReserveOf(kind) == 0) PickedKind = NextKind();
            return Commit(DeploymentResult.Placed);
        }

        // off the board, gear back in the inventory
        private void TakeOff(string unitId)
        {
            _placements.RemoveAll(p => p.UnitId == unitId);
            foreach (var itemId in _owners.Keys.ToList())
                if (_owners[itemId] == unitId) _owners[itemId] = null;
            if (SelectedUnitId == unitId) SelectedUnitId = null;
        }

        // what to do next, one short line under the board
        private string NextStep() =>
            _roster.Count == 0 ? "Нет бойцов: найми их в колонии"
            : CanPlaceMore && PickedKind is UnitKind kind
                ? $"Нажми синюю клетку: встанет {DefinitionOf(kind).DisplayName.ToLowerInvariant()}"
            : "Перетащи бойца, чтобы переставить";

        // the first kind, in the catalog order, with fighters still off the board
        private UnitKind? NextKind()
        {
            foreach (var kind in Kinds)
                if (ReserveOf(kind) > 0) return kind;
            return null;
        }

        private string PlaceRefusal(Cell cell) =>
            _board.IsBlocked(cell) ? "Клетка занята преградой" : "Бойцов ставят на синие клетки";

        private DeploymentResult Commit(DeploymentResult result)
        {
            Message = NextStep();
            Changed?.Invoke();
            return result;
        }

        private DeploymentResult Refuse(string reason)
        {
            Message = reason;
            return DeploymentResult.Refused;
        }
    }
}
