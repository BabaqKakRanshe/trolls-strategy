using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public enum DeploymentClick
    {
        Placed,
        PickedUp,
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
    /// <see cref="Start"/> dispatches the StartBattleCommand. <see cref="Message"/> says what to do next
    /// or why a click was refused.
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
            Message = "Выбери бойца слева или нажми «Авторасстановка». Бой идёт автоматически; погибшие бойцы и их вещи будут потеряны.";
        }

        /// <summary>After every change of placements, selection or gear.</summary>
        public event Action Changed;

        public GameSession Session { get; }
        public BattleMissionDefinition Mission { get; }
        public IReadOnlyList<UnitSnapshot> Roster => _roster;
        public IReadOnlyList<EquipmentSnapshot> Equipment => _equipment;
        public IReadOnlyList<BattlePlacement> Placements => _placements;
        public IReadOnlyDictionary<string, UnitKind> UnitKinds => _kinds;
        public string SelectedUnitId { get; private set; }
        public string Message { get; private set; }
        public int MaxUnits => Mission.MaxPlayerUnits;
        public bool CanStart => _placements.Count > 0;
        public bool CanAutoPlace => _placements.Count < Math.Min(Mission.MaxPlayerUnits, _roster.Count);
        public bool SelectedIsPlaced => SelectedUnitId != null && IsPlaced(SelectedUnitId);

        public bool IsPlaced(string unitId) => _placements.Exists(p => p.UnitId == unitId);

        public Cell? CellOf(string unitId)
        {
            int index = _placements.FindIndex(p => p.UnitId == unitId);
            return index >= 0 ? _placements[index].Cell : null;
        }

        /// <summary>Who wears the item now: a unit id, or null while it lies in the inventory.</summary>
        public string OwnerOf(string itemId) => _owners.TryGetValue(itemId, out var owner) ? owner : null;

        public string UnitName(string unitId) => unitId != null && _units.TryGetValue(unitId, out var unit)
            ? $"{unit.Name} {unit.Number}" : "другого бойца";

        public UnitDefinition DefinitionOf(string unitId) => Session.Catalog.GetUnit(_kinds[unitId]);

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

        public static string CellName(Cell cell) => $"{cell.X + 1}:{cell.Y + 1}";

        public bool Select(string unitId)
        {
            if (unitId == null || !_units.ContainsKey(unitId)) return false;
            SelectedUnitId = unitId;
            Message = IsPlaced(unitId)
                ? "Боец выбран. Нажми свободную синюю клетку, чтобы переставить, или выдай снаряжение."
                : "Теперь нажми свободную синюю клетку, чтобы поставить бойца.";
            Changed?.Invoke();
            return true;
        }

        /// <summary>A click on the board: picks up a placed fighter, places the selected one, or says why not.</summary>
        public DeploymentClick ClickCell(Cell cell)
        {
            // clicking one of your placed fighters picks it up, whatever was selected
            int onCell = _placements.FindIndex(p => p.Cell == cell);
            if (onCell >= 0 && _placements[onCell].UnitId != SelectedUnitId)
            {
                SelectedUnitId = _placements[onCell].UnitId;
                Message = "Боец выбран: нажми на другую синюю клетку, чтобы переставить";
                Changed?.Invoke();
                return DeploymentClick.PickedUp;
            }
            if (string.IsNullOrEmpty(SelectedUnitId)) return Refuse("Сначала выбери бойца в списке слева");
            if (!_board.CanPlace(cell))
                return Refuse(_board.IsBlocked(cell) ? "Клетка занята преградой" : "Здесь нельзя расставлять бойцов");
            int current = _placements.FindIndex(p => p.UnitId == SelectedUnitId);
            if (current < 0 && _placements.Count >= Mission.MaxPlayerUnits) return Refuse("Отряд заполнен");

            var placement = new BattlePlacement(SelectedUnitId, cell);
            if (current >= 0) _placements[current] = placement;
            else _placements.Add(placement);
            Message = $"Боец на поле: {CellName(cell)}. Выдай снаряжение ниже или выбери следующего бойца.";
            Changed?.Invoke();
            return DeploymentClick.Placed;
        }

        /// <summary>Takes the selected fighter off the board; its gear goes back to the inventory.</summary>
        public bool RemoveSelected()
        {
            if (SelectedUnitId == null || _placements.RemoveAll(p => p.UnitId == SelectedUnitId) == 0) return false;
            foreach (var itemId in _owners.Keys.ToList())
                if (_owners[itemId] == SelectedUnitId) _owners[itemId] = null;
            Message = "Боец убран с поля, его снаряжение возвращено в инвентарь.";
            Changed?.Invoke();
            return true;
        }

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
            if (!IsPlaced(SelectedUnitId))
                SelectedUnitId = _placements.Count > 0 ? _placements[0].UnitId : null;
            Message = "Отряд расставлен. Выбери бойца для снаряжения или начни бой. Погибшие бойцы и их вещи будут потеряны.";
            Changed?.Invoke();
        }

        /// <summary>Puts the item on the selected placed fighter, one per slot, or takes it off again.</summary>
        public EquipmentChange ToggleEquipment(string itemId)
        {
            if (!SelectedIsPlaced || !_items.TryGetValue(itemId, out var item)) return EquipmentChange.None;
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
            Changed?.Invoke();
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
            var usedSlots = new HashSet<EquipmentSlot>();
            foreach (var item in _equipment)
                if (usedSlots.Add(item.Slot)) _owners[item.Id] = SelectedUnitId;
            Changed?.Invoke();
            return true;
        }
#endif

        private DeploymentClick Refuse(string reason)
        {
            Message = reason;
            return DeploymentClick.Refused;
        }
    }
}
