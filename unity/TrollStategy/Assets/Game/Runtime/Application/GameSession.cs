using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>A building the colony starts with, authored in the scene.</summary>
    public readonly struct StartingBuilding
    {
        public StartingBuilding(BuildingKind kind, Cell cell)
        {
            Kind = kind;
            Cell = cell;
        }

        public BuildingKind Kind { get; }
        public Cell Cell { get; }
    }

    public class GameSession
    {
        private readonly GameContentCatalog _catalog;
        private GameState _state;
        private int _revision;
        private float _remainderSeconds;
        private bool _debugBattleAccess;
        // Which level of the authored chain opens each building, creature and mission; read once from content.
        private Dictionary<BuildingKind, int> _buildingUnlockLevels;
        private Dictionary<UnitKind, int> _unitUnlockLevels;
        private Dictionary<string, int> _missionUnlockLevels;
        private int _tutorialSteps;

        public event Action<GameSnapshot> OnSnapshotChanged;
        /// <summary>Every dispatched command with its result, accepted or refused, after the state committed.</summary>
        public event Action<IGameCommand, CommandResult> OnCommandResolved;

        /// <summary>
        /// Starts a colony with the given buildings. Throws when the layout breaks placement rules;
        /// StartingBuildingIds lists the created ids in layout order. A campaign game plays the catalog's
        /// quest chain and starts with only its opening unlocks; a sandbox game has everything open.
        /// </summary>
        public GameSession(GameContentCatalog catalog, IReadOnlyList<StartingBuilding> startingBuildings,
            bool campaign = false)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (startingBuildings == null) throw new ArgumentNullException(nameof(startingBuildings));
            _state = GameState.CreateInitialState(_catalog.Economy.StartingGold);
            // land first: the starting buildings must stand on the cleared start land
            _state.Land = LandRules.CreateStart(_catalog.Economy);
            var ids = new string[startingBuildings.Count];
            for (int i = 0; i < startingBuildings.Count; i++)
            {
                var start = startingBuildings[i];
                var placed = ColonySimulation.PlaceStartingBuilding(_state, start.Kind, start.Cell, _catalog, out ids[i]);
                if (!placed.Ok)
                    throw new InvalidOperationException(
                        $"Starting {start.Kind} at ({start.Cell.X}, {start.Cell.Y}): {placed.Error}");
            }
            StartingBuildingIds = ids;
            foreach (var definition in _catalog.Equipment)
            {
                if (definition == null) continue;
                for (int i = 0; i < definition.StartingQuantity; i++)
                    _state.Equipment.Add(new EquipmentState
                    {
                        Id = $"item-{_state.Equipment.Count + 1:D3}",
                        DefinitionId = definition.ItemId
                    });
            }
            if (campaign) Progression.Start(_state, _catalog);
            _revision = 1;
        }

        public IReadOnlyList<string> StartingBuildingIds { get; }
        public GameSnapshot CurrentSnapshot => CreateSnapshot();
        public GameContentCatalog Catalog => _catalog;
        public BattleRunState ActiveBattle => _state.ActiveBattle;
        public int ActiveTimeMs => _state.ActiveTimeMs;
        /// <summary>Wins at one arena mission so far.</summary>
        public int MissionWins(string missionId) => _state.WinsOf(missionId);
        /// <summary>Highest arena level the colony has won.</summary>
        public int HighestMissionLevel => _state.HighestMissionLevel;
        public int BattlesWon => _state.BattlesWon;
        public bool IsCampaign => _state.Progress != null;

        public bool IsBuildingUnlocked(BuildingKind kind) => Progression.IsBuildingUnlocked(_state, kind);
        public bool IsUnitUnlocked(UnitKind kind) => Progression.IsUnitUnlocked(_state, kind);

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        public void EnableDebugBattleAccess() => _debugBattleAccess = true;

        /// <summary>Development shortcut: marks the current quest done so it can be claimed at once.</summary>
        public CommandResult DebugCompleteQuest()
        {
            if (_state.ActiveBattle != null) return CommandResult.Fail("Сначала завершите текущий бой");
            var candidate = _state.Clone();
            if (!Progression.CompleteCurrentQuest(candidate, _catalog))
                return CommandResult.Fail("Нет текущего задания");
            _state = candidate;
            _revision++;
            Emit();
            return CommandResult.Success();
        }

        /// <summary>Development shortcut: every building of the catalog may be built without its quest.</summary>
        public CommandResult DebugUnlockAllBuildings()
        {
            if (_state.Progress == null) return CommandResult.Fail("В этой игре все постройки уже открыты");
            if (_state.ActiveBattle != null) return CommandResult.Fail("Сначала завершите текущий бой");
            var candidate = _state.Clone();
            foreach (var definition in _catalog.Buildings)
                if (definition != null) candidate.Progress.UnlockedBuildings.Add(definition.Kind);
            _state = candidate;
            _revision++;
            Emit();
            return CommandResult.Success();
        }

        /// <summary>Development shortcut: puts gold into the treasury; "have gold" goals count it at once.</summary>
        public CommandResult DebugAddGold(int amount)
        {
            if (amount <= 0) return CommandResult.Fail("Сумма должна быть больше нуля");
            if (_state.ActiveBattle != null) return CommandResult.Fail("Сначала завершите текущий бой");
            var candidate = _state.Clone();
            candidate.Gold = (int)Math.Min(int.MaxValue, (long)candidate.Gold + amount);
            Progression.Update(candidate, _catalog);
            _state = candidate;
            _revision++;
            Emit();
            return CommandResult.Success();
        }

        /// <summary>Development shortcut: every block of land becomes the colony's, wild where it was not owned.</summary>
        public CommandResult DebugOwnAllLand() => DebugLand(LandBlock.Wild);

        /// <summary>Development shortcut: every owned block is cleared at once.</summary>
        public CommandResult DebugClearAllLand() => DebugLand(LandBlock.Cleared);

        private CommandResult DebugLand(LandBlock target)
        {
            if (_state.Land == null) return CommandResult.Fail("В этой игре земля не покупается");
            if (_state.ActiveBattle != null) return CommandResult.Fail("Сначала завершите текущий бой");
            var candidate = _state.Clone();
            var land = candidate.Land;
            for (int y = 0; y < land.BlocksPerSide; y++)
            for (int x = 0; x < land.BlocksPerSide; x++)
            {
                var block = land.Block(x, y);
                if (target == LandBlock.Wild && block == LandBlock.Unowned)
                {
                    land.Set(x, y, LandBlock.Wild);
                    land.Purchases++;
                }
                else if (target == LandBlock.Cleared && block == LandBlock.Wild)
                {
                    land.Set(x, y, LandBlock.Cleared);
                }
            }
            candidate.LayoutVersion++;
            _state = candidate;
            _revision++;
            Emit();
            return CommandResult.Success();
        }
#endif

        public CommandResult CanEnterMission(string missionId)
        {
            foreach (var mission in _catalog.Missions)
                if (mission != null && mission.MissionId == missionId)
                    return BattleApplication.ValidateAvailability(_state, mission, _debugBattleAccess);
            return CommandResult.Fail("Миссия не найдена");
        }

        /// <summary>Fighters the colony may send to this mission now.</summary>
        public int SquadLimit(BattleMissionDefinition mission) => BattleApplication.SquadLimit(_state, mission, _catalog);

        /// <summary>A win's gold range at this mission now, first or repeat win, with the barracks' glory.</summary>
        public (int Min, int Max) WinGold(BattleMissionDefinition mission) =>
            BattleApplication.WinGold(_state, mission, _catalog);

        /// <summary>Whether the mission is open on the ladder (not counting the time it rests).</summary>
        public bool IsMissionUnlocked(string missionId) => Progression.IsMissionUnlocked(_state, missionId);

        /// <summary>The arena ladder in level order: the missions of the catalog sorted by level.</summary>
        public IReadOnlyList<BattleMissionDefinition> ArenaLadder()
        {
            var ladder = new List<BattleMissionDefinition>();
            foreach (var mission in _catalog.Missions) if (mission != null) ladder.Add(mission);
            ladder.Sort((a, b) => a.Level.CompareTo(b.Level));
            return ladder;
        }

        /// <summary>
        /// The mission the battle button should lead to: the highest open level that is ready, else the highest
        /// open level, else the first level.
        /// </summary>
        public BattleMissionDefinition SuggestedMission()
        {
            BattleMissionDefinition open = null, ready = null;
            foreach (var mission in ArenaLadder())
            {
                if (!IsMissionUnlocked(mission.MissionId)) continue;
                open = mission;
                if (CanEnterMission(mission.MissionId).Ok) ready = mission;
            }
            var ladder = ArenaLadder();
            return ready ?? open ?? (ladder.Count > 0 ? ladder[0] : null);
        }

        /// <summary>Active colony time left before the mission opens or recovers; 0 when time does not hold it back.</summary>
        public int MissionWaitMs(string missionId)
        {
            foreach (var mission in _catalog.Missions)
                if (mission != null && mission.MissionId == missionId)
                    return BattleApplication.WaitMs(_state, mission);
            return 0;
        }

        public CommandResult Dispatch(IGameCommand command)
        {
            if (command == null)
                return CommandResult.Fail("Команда не задана");
            if (_state.ActiveBattle != null && command is not AcknowledgeBattleCommand)
                return CommandResult.Fail("Сначала завершите текущий бой");

            var candidate = _state.Clone();
            var result = command switch
            {
                StartBattleCommand start => BattleApplication.Start(candidate, start, _catalog,
                    _debugBattleAccess),
                AcknowledgeBattleCommand => BattleApplication.Acknowledge(candidate),
                ClaimBattleRewardCommand => BattleApplication.ClaimReward(candidate),
                _ => ColonySimulation.ApplyCommand(candidate, command, _catalog)
            };
            if (result.Ok)
            {
                // quest goals met by this command count in the same commit
                Progression.Update(candidate, _catalog);
                _state = candidate;
                _revision++;
                Emit();
            }
            OnCommandResolved?.Invoke(command, result);
            return result;
        }

        public GameSnapshot Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return CurrentSnapshot;
            if (_state.ActiveBattle != null)
                return CurrentSnapshot;

            float step = _catalog.Economy.EconomyStepSeconds;
            if (step <= 0f)
                throw new InvalidOperationException("Шаг экономики должен быть больше нуля");

            _remainderSeconds += deltaSeconds;
            bool changed = false;

            while (_remainderSeconds >= step)
            {
                ColonySimulation.TickColony(_state, step, _catalog);
                _state.ActiveTimeMs += (int)Math.Round(step * 1000f);
                Progression.Update(_state, _catalog);
                _remainderSeconds -= step;
                changed = true;
            }

            if (changed)
            {
                _revision++;
                Emit();
            }

            return CurrentSnapshot;
        }

        public Cell FindSpawnCell()
        {
            var barracks = _state.Buildings.Find(b => b.Kind == BuildingKind.Barracks);
            if (barracks != null)
            {
                var def = _catalog.GetBuilding(BuildingKind.Barracks);
                int x = barracks.Cell.X + def.Width / 2;
                int y = barracks.Cell.Y + def.Height;
                var candidate = new Cell(x, y);
                if (CanBuyUnits(UnitKind.Goblin, 1, candidate).Ok)
                    return candidate;
            }

            // south-west of the middle of the start land (of the grid without land)
            var center = LandRules.StartCenter(_catalog.Economy);
            var home = new Cell(center.X - 4, center.Y - 4);
            // rings out from home over the whole grid, so a grown colony still finds a free cell for a hire
            int reach = Math.Max(_catalog.Economy.GridWidth, _catalog.Economy.GridHeight);
            for (int r = 0; r < reach; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var candidate = new Cell(home.X + dx, home.Y + dy);
                        if (CanBuyUnits(UnitKind.Goblin, 1, candidate).Ok)
                            return candidate;
                    }
                }
            }

            return home;
        }

        public CommandResult CanPlaceBuilding(BuildingKind kind, Cell cell, string ignoredId = null) =>
            ColonySimulation.ValidateBuildingPlacement(_state, kind, cell, _catalog, ignoredId);

        public Cell? FindFirstBuildingCell(BuildingKind kind) =>
            ColonySimulation.FindFirstValidBuildingCell(_state, kind, _catalog);

        public CommandResult CanBuyUnits(UnitKind kind, int amount, Cell cell)
        {
            return ColonySimulation.ValidateUnitPurchase(_state, kind, amount, cell, _catalog);
        }

        /// <summary>Gold the next building of this kind costs now; it grows with every one already standing.</summary>
        public int BuildingPrice(BuildingKind kind) => ColonySimulation.BuildingPrice(_state, kind, _catalog);

        /// <summary>Gold for hiring a group now; every creature in the colony makes the next one dearer.</summary>
        public int HirePrice(UnitKind kind, int amount = 1) => ColonySimulation.HirePrice(_state, kind, amount, _catalog);

        public CommandResult CanBuyLand(int blockX, int blockY) => LandRules.ValidateBuy(_state, blockX, blockY, _catalog);

        public CommandResult CanClearLand(int blockX, int blockY) =>
            LandRules.ValidateClear(_state, blockX, blockY, _catalog);

        public GameSnapshot CreateSnapshot()
        {
            var buildingSnapshots = new List<BuildingSnapshot>(_state.Buildings.Count);
            int totalOreInBuildings = 0;

            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                var b = _state.Buildings[i];
                var def = _catalog.GetBuilding(b.Kind);
                totalOreInBuildings += b.GetStock(ResourceKind.IronOre);

                int workers = 0;
                int goblinHaulers = 0, trollHaulers = 0;
                for (int u = 0; u < _state.Units.Count; u++)
                {
                    var unit = _state.Units[u];
                    var a = unit.Assignment;
                    if ((a.Kind == AssignmentKind.ToWork || a.Kind == AssignmentKind.Work) && a.BuildingId == b.Id)
                        workers++;
                    if (a.Kind == AssignmentKind.Haul && (a.SourceId == b.Id || a.DestinationId == b.Id))
                    {
                        if (unit.Kind == UnitKind.Goblin) goblinHaulers++;
                        else if (unit.Kind == UnitKind.Troll) trollHaulers++;
                    }
                }

                buildingSnapshots.Add(new BuildingSnapshot(
                    b.Id,
                    b.Kind,
                    b.Kind == BuildingKind.Mine || GetIdNumber(b.Id) > 1 ? $"{def.DisplayName} {GetIdNumber(b.Id)}" : def.DisplayName,
                    b.Cell,
                    def.Width,
                    def.Height,
                    b.TotalStock,
                    def.Capacity(b.Level),
                    workers,
                    def.WorkerCapacity(b.Level),
                    ColonySimulation.ProductionPerSecond(_state, b.Id, _catalog),
                    ColonySimulation.CycleProgress(b, _catalog),
                    b.Level,
                    b.InvestedGold / 2,
                    def.UpgradeCost(b.Level),
                    def.SaleBonus(b.Level),
                    goblinHaulers,
                    trollHaulers,
                    def.SlotStackSize,
                    def.StorageRole == StorageRole.Stockpile
                        ? StorageSlots.Fill(b.Stock, def.Capacity(b.Level), def.SlotStackSize)
                        : null,
                    StockOf(b),
                    def.IsWorkplace,
                    ColonySimulation.DescribeProduction(_state, b, _catalog),
                    DescribeRecipes(def)));
            }

            var unitSnapshots = new List<UnitSnapshot>(_state.Units.Count);
            int carriedOre = 0;

            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                var def = _catalog.GetUnit(u.Kind);
                if (u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.CarriedResource == ResourceKind.IronOre)
                    carriedOre += u.Assignment.Carried;

                unitSnapshots.Add(new UnitSnapshot(
                    u.Id,
                    u.Kind,
                    u.Name ?? def.DisplayName,
                    def.Strength,
                    def.Speed,
                    def.Stamina,
                    u.Position,
                    u.Assignment,
                    FormatAssignmentStatus(u.Assignment, u.Position, buildingSnapshots),
                    ColonySimulation.UnitMovementSpeed(_state, def, _catalog)));
            }

            var equipmentSnapshots = new List<EquipmentSnapshot>(_state.Equipment.Count);
            foreach (var item in _state.Equipment)
                equipmentSnapshots.Add(new EquipmentSnapshot(item.Id,
                    _catalog.GetEquipment(item.DefinitionId), item.OwnerUnitId));

            return new GameSnapshot(
                _revision,
                _state.Gold,
                _state.SoldGoods,
                totalOreInBuildings + carriedOre,
                buildingSnapshots,
                unitSnapshots,
                equipmentSnapshots,
                CreateProgressSnapshot(),
                CreateBattleRewardSnapshot(),
                CreateLandSnapshot(),
                CreateUpgradeSnapshots());
        }

        private List<UpgradeSnapshot> CreateUpgradeSnapshots()
        {
            var upgrades = new List<UpgradeSnapshot>(_catalog.Upgrades.Count);
            foreach (var upgrade in _catalog.Upgrades)
            {
                if (upgrade == null) continue;
                int level = _state.UpgradeLevel(upgrade.Id);
                upgrades.Add(new UpgradeSnapshot(upgrade.Id, upgrade.DisplayName, upgrade.Description, upgrade.Host,
                    upgrade.Effect, upgrade.AmountPerLevel, level, upgrade.MaxLevel, upgrade.CostFrom(level),
                    UpgradeRules.HostLevel(_state, upgrade.Host), upgrade.HostLevelFrom(level)));
            }
            return upgrades;
        }

        /// <summary>Whether the colony may raise this upgrade now, and why not.</summary>
        public CommandResult CanBuyUpgrade(string upgradeId) => UpgradeRules.Validate(_state, upgradeId, _catalog);

        private LandSnapshot CreateLandSnapshot()
        {
            var land = _state.Land;
            if (land == null) return null;
            var blocks = new LandBlockSnapshot[land.Count];
            for (int y = 0; y < land.BlocksPerSide; y++)
            for (int x = 0; x < land.BlocksPerSide; x++)
            {
                var block = land.Block(x, y);
                blocks[y * land.BlocksPerSide + x] = new LandBlockSnapshot(x, y, block != LandBlock.Unowned,
                    block == LandBlock.Cleared, land.IsClearing(x, y), land.ClearProgress(x, y),
                    land.ClearSecondsLeft(x, y), LandRules.ValidatePlace(_state, x, y).Ok);
            }
            var economy = _catalog.Economy;
            return new LandSnapshot(land.BlocksPerSide, land.BlockSize, blocks, LandRules.NextPrice(land, economy),
                economy.LandClearGold, economy.LandClearSeconds);
        }

        private BattleRewardSnapshot CreateBattleRewardSnapshot()
        {
            var pending = _state.PendingBattleReward;
            if (pending == null) return null;
            string name = pending.MissionId;
            foreach (var mission in _catalog.Missions)
                if (mission != null && mission.MissionId == pending.MissionId) name = mission.DisplayName;
            return new BattleRewardSnapshot(name, pending.Gold, pending.MinGold, pending.MaxGold, pending.FirstWin);
        }

        private ProgressSnapshot CreateProgressSnapshot()
        {
            var progress = _state.Progress;
            if (progress == null) return ProgressSnapshot.Sandbox;
            EnsureUnlockLevels();

            QuestSnapshot questSnapshot = null;
            var quest = Progression.CurrentQuest(_state, _catalog);
            if (quest != null)
            {
                var goals = new List<GoalSnapshot>(quest.Goals.Count);
                for (int i = 0; i < quest.Goals.Count; i++)
                {
                    var goal = quest.Goals[i];
                    bool done = i < progress.GoalDone.Count && progress.GoalDone[i];
                    goals.Add(new GoalSnapshot(goal, DescribeGoal(goal), Progression.GoalProgress(_state, i, goal), done));
                }
                var rewards = new List<RewardSnapshot>(quest.Rewards.Count);
                foreach (var reward in quest.Rewards) rewards.Add(DescribeReward(reward));
                bool tutorial = quest.IsTutorial && progress.QuestIndex < _tutorialSteps;
                questSnapshot = new QuestSnapshot(quest.Id, progress.QuestIndex + 1, quest.Title, quest.Description,
                    tutorial, tutorial ? progress.QuestIndex + 1 : 0, _tutorialSteps, goals, rewards,
                    Progression.IsQuestComplete(_state, _catalog));
            }

            return new ProgressSnapshot(true, progress.QuestIndex + 1, questSnapshot,
                new List<BuildingKind>(progress.UnlockedBuildings), new List<UnitKind>(progress.UnlockedUnits),
                new List<string>(progress.UnlockedMissions), _buildingUnlockLevels, _unitUnlockLevels,
                _missionUnlockLevels);
        }

        private void EnsureUnlockLevels()
        {
            if (_buildingUnlockLevels != null) return;
            _buildingUnlockLevels = new Dictionary<BuildingKind, int>();
            _unitUnlockLevels = new Dictionary<UnitKind, int>();
            _missionUnlockLevels = new Dictionary<string, int>(StringComparer.Ordinal);
            var chain = _catalog.Progression != null ? _catalog.Progression.Quests : Array.Empty<QuestDefinition>();
            _tutorialSteps = 0;
            while (_tutorialSteps < chain.Count && chain[_tutorialSteps] != null && chain[_tutorialSteps].IsTutorial)
                _tutorialSteps++;
            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i] == null) continue;
                foreach (var reward in chain[i].Rewards)
                {
                    switch (reward.Kind)
                    {
                        case QuestRewardKind.UnlockBuilding:
                            _buildingUnlockLevels.TryAdd(reward.Building, i + 1);
                            break;
                        case QuestRewardKind.UnlockUnit:
                            _unitUnlockLevels.TryAdd(reward.Unit, i + 1);
                            break;
                        case QuestRewardKind.UnlockMission:
                            if (!string.IsNullOrEmpty(reward.MissionId))
                                _missionUnlockLevels.TryAdd(reward.MissionId, i + 1);
                            break;
                    }
                }
            }
        }

        /// <summary>A goal as a line in the quest tracker; the numbers are shown beside it.</summary>
        public string DescribeGoal(QuestGoal goal)
        {
            string unit = goal.AnyUnit ? null : UnitName(goal.Unit);
            switch (goal.Kind)
            {
                case QuestGoalKind.OwnUnits:
                    return unit == null ? "Существ в поселении" : $"Нанять: {unit}";
                case QuestGoalKind.OwnBuildings:
                    return $"Построить: {BuildingName(goal.Building)}";
                case QuestGoalKind.WorkAt:
                    return unit == null
                        ? $"Рабочие → {BuildingName(goal.Building)}"
                        : $"Работает: {unit} → {BuildingName(goal.Building)}";
                case QuestGoalKind.HaulRoute:
                    string route = $"{BuildingName(goal.Building)} → {BuildingName(goal.Destination)}";
                    return unit == null ? $"Носильщики: {route}" : $"Носит {unit}: {route}";
                case QuestGoalKind.HaveGold:
                    return "Золото в казне";
                case QuestGoalKind.EarnGold:
                    return "Выручка рынка, золото";
                case QuestGoalKind.SellGoods:
                    return "Продано товаров";
                case QuestGoalKind.SellResource:
                    return $"Продано: {ResourceName(goal.Resource).ToLowerInvariant()}";
                case QuestGoalKind.WinBattles:
                    return "Победы в бою";
                case QuestGoalKind.UpgradeBuilding:
                    return $"Уровень: {BuildingName(goal.Building)}";
                case QuestGoalKind.ReachArenaLevel:
                    return "Уровень арены";
                case QuestGoalKind.OwnLand:
                    return "Куплено земли, блоков";
                case QuestGoalKind.ProduceResource:
                    return $"Сделано: {ResourceName(goal.Resource).ToLowerInvariant()}";
                case QuestGoalKind.BuyUpgrades:
                    return "Улучшения гильдии и бараков, уровней";
                case QuestGoalKind.EquipFighters:
                    return "Бойцов в снаряжении";
                case QuestGoalKind.OwnEquipment:
                    return "Предметов на складе экипировки";
                default:
                    return goal.Kind.ToString();
            }
        }

        public RewardSnapshot DescribeReward(QuestReward reward)
        {
            switch (reward.Kind)
            {
                case QuestRewardKind.UnlockBuilding:
                {
                    // the reward is the right to build, so say where and for how much
                    var building = TryBuilding(reward.Building);
                    return new RewardSnapshot(reward, building?.DisplayName ?? reward.Building.ToString(), "Новая постройка",
                        building == null
                            ? string.Empty
                            : $"Теперь её можно строить: каталог внизу, вкладка «Здания», {BuildingPrice(building.Kind)} золота.\n" +
                              DescribeBuilding(building));
                }
                case QuestRewardKind.UnlockUnit:
                {
                    var unit = TryUnit(reward.Unit);
                    return new RewardSnapshot(reward, unit?.DisplayName ?? reward.Unit.ToString(), "Новое существо",
                        unit == null
                            ? string.Empty
                            : $"Теперь его можно нанимать: каталог внизу, вкладка «Существа», {HirePrice(unit.Kind)} золота.\n" + unit.Description);
                }
                case QuestRewardKind.UnlockMission:
                {
                    BattleMissionDefinition mission = null;
                    foreach (var candidate in _catalog.Missions)
                        if (candidate != null && candidate.MissionId == reward.MissionId) mission = candidate;
                    string description = mission == null
                        ? "Кнопка «В бой» справа открыта."
                        : $"Кнопка «В бой» справа открыта. В бой идут до {mission.MaxPlayerUnits} бойцов, " +
                          $"первая победа принесёт {GoldRange(mission.FirstWinGold, mission.FirstWinGoldMax)} золота.";
                    return new RewardSnapshot(reward, mission?.DisplayName ?? reward.MissionId, "Новый бой", description);
                }
                default:
                    return new RewardSnapshot(reward, $"{reward.Gold} золота", "Золото", "Пополнит казну поселения.");
            }
        }

        /// <summary>"от 200 до 350" for a range, the single amount when there is none.</summary>
        public static string GoldRange(int min, int max) => max > min ? $"от {min} до {max}" : min.ToString();

        /// <summary>A building as the catalog and rewards describe it: size, staff, role and recipes.</summary>
        /// <summary>
        /// The recipe a building is known by: the simplest one it runs from the first level. The catalog and the
        /// quest reward name only this one; the building's card lists them all.
        /// </summary>
        public static ProductionRecipe MainRecipe(BuildingDefinition definition)
        {
            if (definition == null || definition.Recipes.Count == 0) return null;
            ProductionRecipe main = null;
            foreach (var recipe in definition.Recipes)
                if (recipe.MinLevel <= 1 && (main == null || recipe.Inputs.Length < main.Inputs.Length)) main = recipe;
            return main ?? definition.Recipes[0];
        }

        /// <summary>The main recipe in words, "2 руда → 1 слиток"; empty for a building without recipes.</summary>
        public string DescribeMainRecipe(BuildingDefinition definition)
        {
            var recipe = MainRecipe(definition);
            if (recipe == null) return string.Empty;
            return (recipe.Inputs.Length > 0 ? FormatAmounts(recipe.Inputs) + " → " : "") + FormatAmounts(recipe.Outputs);
        }

        public string DescribeBuilding(BuildingDefinition building, bool recipes = true)
        {
            string text = $"{building.Width}×{building.Height}";
            if (building.MaxWorkers > 0) text += $", до {building.MaxWorkers} рабочих";
            switch (building.StorageRole)
            {
                case StorageRole.Stockpile:
                    text += ", хранит сырьё";
                    break;
                case StorageRole.Market:
                    text += ", продаёт товары";
                    break;
                case StorageRole.Armory:
                    text += ", снаряжение отряда";
                    break;
            }
            string list = recipes ? DescribeRecipes(building) : string.Empty;
            return string.IsNullOrEmpty(list) ? text : text + "\n" + list;
        }

        private string UnitName(UnitKind kind) => (TryUnit(kind)?.DisplayName ?? kind.ToString()).ToLowerInvariant();

        private string BuildingName(BuildingKind kind) =>
            (TryBuilding(kind)?.DisplayName ?? kind.ToString()).ToLowerInvariant();

        private BuildingDefinition TryBuilding(BuildingKind kind)
        {
            foreach (var definition in _catalog.Buildings)
                if (definition != null && definition.Kind == kind) return definition;
            return null;
        }

        private UnitDefinition TryUnit(UnitKind kind)
        {
            foreach (var definition in _catalog.Units)
                if (definition != null && definition.Kind == kind) return definition;
            return null;
        }

        private void Emit()
        {
            OnSnapshotChanged?.Invoke(CurrentSnapshot);
        }

        public string ResourceName(ResourceKind resource) =>
            _catalog.TryGetResource(resource)?.DisplayName ?? resource.ToString();

        private List<ResourceStack> StockOf(BuildingState building)
        {
            var stock = new List<ResourceStack>();
            foreach (ResourceKind resource in Enum.GetValues(typeof(ResourceKind)))
            {
                int amount = building.GetStock(resource);
                if (amount > 0) stock.Add(new ResourceStack(resource, ResourceName(resource), amount));
            }
            return stock;
        }

        /// <summary>
        /// The building's recipes as the player reads them, one per line; empty for non-producers. A recipe or
        /// by-product that needs a higher level says so; chances are given in percent.
        /// </summary>
        public string DescribeRecipes(BuildingDefinition definition)
        {
            var lines = new List<string>();
            foreach (var recipe in definition.Recipes)
            {
                string line = (recipe.MinLevel > 1 ? $"С {recipe.MinLevel} уровня: " : "") +
                    (recipe.Inputs.Length > 0 ? FormatAmounts(recipe.Inputs) + " → " : "") +
                    FormatAmounts(recipe.Outputs);
                var notes = new List<string>();
                foreach (var extra in recipe.Extras)
                    notes.Add($"{extra.ChancePercent}%: +{extra.Output.Amount} {ResourceName(extra.Output.Resource)}" +
                              (extra.MinLevel > recipe.MinLevel ? $" с {extra.MinLevel} уровня" : ""));
                if (recipe.FailChancePercent > 0)
                    notes.Add($"брак {recipe.FailChancePercent}%" +
                              (recipe.FailOutputs.Length > 0 ? $" → {FormatAmounts(recipe.FailOutputs)}" : ""));
                if (notes.Count > 0) line += " (" + string.Join("; ", notes) + ")";
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        private string FormatAmounts(ResourceAmount[] amounts)
        {
            var parts = new List<string>(amounts.Length);
            foreach (var amount in amounts)
                parts.Add($"{amount.Amount} {ResourceName(amount.Resource)}");
            return string.Join(" + ", parts);
        }

        private static int GetIdNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            int dash = id.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(id.Substring(dash + 1), out int num))
                return num;
            return 1;
        }

        private string FormatAssignmentStatus(Assignment assignment, WorldPosition position, List<BuildingSnapshot> buildings)
        {
            if (assignment.Kind == AssignmentKind.Idle)
            {
                var place = assignment.BuildingId != null && assignment.CrowdSlot >= 0
                    ? _state.Buildings.Find(b => b.Id == assignment.BuildingId)
                    : null;
                if (place == null || place.Kind != BuildingKind.Barracks) return "Свободен";
                return position.Equals(ColonyNavigation.CrowdSlotPosition(place, assignment.CrowdSlot, _catalog))
                    ? "Отдыхает в бараках"
                    : "Идёт в бараки";
            }
            if (assignment.Kind == AssignmentKind.ToWork)
            {
                var target = buildings.Find(b => b.Id == assignment.BuildingId);
                return $"Идёт на работу: {target?.Name ?? "производство"}";
            }
            if (assignment.Kind == AssignmentKind.Work)
            {
                var target = buildings.Find(b => b.Id == assignment.BuildingId);
                return $"Работает: {target?.Name ?? "производство"}";
            }
            var source = buildings.Find(b => b.Id == assignment.SourceId)?.Name ?? "источник";
            var dest = buildings.Find(b => b.Id == assignment.DestinationId)?.Name ?? "цель";
            if (assignment.Carried > 0)
                return $"Несёт {assignment.Carried} {ResourceName(assignment.CarriedResource).ToLowerInvariant()} " +
                       $"на {CargoValue(assignment)} зол.: {source} → {dest}";
            return $"Возит {DescribeCargo(assignment)}: {source} → {dest}";
        }

        /// <summary>What the load a hauler carries now fetches at the colony's best market.</summary>
        public int CargoValue(Assignment assignment)
        {
            if (assignment == null || assignment.Kind != AssignmentKind.Haul || assignment.Carried <= 0) return 0;
            int level = 1;
            foreach (var building in _state.Buildings)
                if (building.Kind == BuildingKind.Market) level = Math.Max(level, building.Level);
            if (_catalog.TryGetResource(assignment.CarriedResource) == null) return 0;
            return assignment.Carried * ColonySimulation.SalePrice(_catalog, assignment.CarriedResource, level);
        }

        /// <summary>What a hauler is allowed to take: "всё" or the chosen goods.</summary>
        public string DescribeCargo(Assignment assignment)
        {
            if (assignment == null || assignment.CarriesAnything) return "всё";
            var names = new List<string>(assignment.Cargo.Count);
            foreach (var resource in assignment.Cargo) names.Add(ResourceName(resource).ToLowerInvariant());
            return string.Join(", ", names);
        }

        /// <summary>
        /// A good as a hint shows it: its market price, which buildings make it and which use it, and what it
        /// gives a fighter when it is equipment.
        /// </summary>
        public string DescribeResource(ResourceKind resource)
        {
            var lines = new List<string>();
            var definition = _catalog.TryGetResource(resource);
            if (definition != null) lines.Add($"На рынке: {definition.SellPrice} зол. за штуку");
            var makers = new List<string>();
            var users = new List<string>();
            foreach (var building in _catalog.Buildings)
            {
                if (building == null) continue;
                if (building.ProducesInRecipe(resource)) makers.Add(building.DisplayName);
                if (building.ConsumesInRecipe(resource)) users.Add(building.DisplayName);
            }
            if (makers.Count > 0) lines.Add("Делают: " + string.Join(", ", makers));
            if (users.Count > 0) lines.Add("Нужен для: " + string.Join(", ", users));
            if (definition != null && definition.IsEquipment)
            {
                foreach (var item in _catalog.Equipment)
                {
                    if (item == null || item.ItemId != definition.EquipmentId) continue;
                    var bonus = new List<string>();
                    if (item.DamageBonus != 0) bonus.Add($"урон +{item.DamageBonus}");
                    if (item.ArmorBonus != 0) bonus.Add($"броня +{item.ArmorBonus}");
                    lines.Add("Снаряжение бойца: " + string.Join(", ", bonus));
                }
            }
            return string.Join("\n", lines);
        }
    }
}
