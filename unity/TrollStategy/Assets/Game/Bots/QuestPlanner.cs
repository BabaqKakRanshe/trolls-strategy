using System;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Turns the current quest's open goals into colony steps. Every goal kind has one general recipe, so the
    /// planner reads the quests as they are authored rather than following a script: a sale goal builds the
    /// chain that makes the good, staffs it, feeds its inputs and opens a route to the market.
    /// </summary>
    internal sealed class QuestPlanner
    {
        // Workers a producer gets when a quest needs its goods; growth adds more.
        private const int ChainWorkers = 2;
        private const int MaxChainDepth = 5;

        private readonly BotHands _hands;
        private readonly BattlePlanner _battles;
        private readonly BotProfile _profile;

        public QuestPlanner(BotHands hands, BattlePlanner battles, BotProfile profile)
        {
            _hands = hands;
            _battles = battles;
            _profile = profile;
        }

        public BotWait Pursue(QuestSnapshot quest)
        {
            var wait = new BotWait();
            _hands.SpendLimit = int.MaxValue;
            if (quest == null) return wait;
            foreach (var goal in quest.Goals)
                if (!goal.Done)
                    Pursue(goal.Goal, wait);
            return wait;
        }

        private void Pursue(QuestGoal goal, BotWait wait)
        {
            switch (goal.Kind)
            {
                case QuestGoalKind.OwnUnits:
                {
                    int missing = goal.Amount - _hands.CountUnits(goal.AnyUnit ? null : goal.Unit);
                    if (missing > 0)
                        _hands.Hire(goal.AnyUnit ? UnitKind.Goblin : goal.Unit, missing, wait, "найм по заданию");
                    break;
                }
                case QuestGoalKind.OwnBuildings:
                    if (_hands.BuildingsOf(goal.Building).Count < goal.Amount)
                        _hands.Build(goal.Building, wait, _hands.Def(goal.Building).DisplayName);
                    break;
                case QuestGoalKind.WorkAt:
                    WorkAt(goal, wait);
                    break;
                case QuestGoalKind.HaulRoute:
                    HaulRoute(goal, wait);
                    break;
                case QuestGoalKind.HaveGold:
                    if (_hands.Gold < goal.Amount) wait.NeedGold(goal.Amount, "накопить");
                    break;
                case QuestGoalKind.EarnGold:
                case QuestGoalKind.SellGoods:
                    EnsureSales(wait);
                    break;
                case QuestGoalKind.SellResource:
                    EnsureChain(goal.Resource, _hands.First(BuildingKind.Market), wait, 0);
                    break;
                case QuestGoalKind.WinBattles:
                    _battles.TryFight(wait, hire: true);
                    break;
                case QuestGoalKind.UpgradeBuilding:
                    Upgrade(goal, wait);
                    break;
                case QuestGoalKind.ReachArenaLevel:
                    // a lost climb asks for a stronger squad first: the barracks' cheapest upgrade
                    if (_battles.LostLast || _battles.NeedsStrength) _hands.BuyUpgrade(wait, "отряд сильнее", BuildingKind.Barracks);
                    _battles.TryFight(wait, hire: true);
                    break;
                case QuestGoalKind.OwnLand:
                    _hands.BuyLandBlock(wait, "земля по заданию");
                    break;
                case QuestGoalKind.ProduceResource:
                    EnsureChain(goal.Resource, _hands.First(BuildingKind.Market), wait, 0);
                    break;
                case QuestGoalKind.BuyUpgrades:
                    _hands.BuyUpgrade(wait, "улучшение по заданию");
                    break;
                case QuestGoalKind.OwnEquipment:
                    EnsureEquipment(goal.Amount, wait);
                    break;
                case QuestGoalKind.EquipFighters:
                case QuestGoalKind.WearGearInBattle:
                    // gear is dealt as the squad marches out: enough items, then a battle
                    if (EnsureEquipment(goal.Amount, wait)) _battles.TryFightSafely(wait);
                    break;
            }
        }

        /// <summary>Swords on their way to the armory until it holds <paramref name="items"/>; true once it does.</summary>
        private bool EnsureEquipment(int items, BotWait wait)
        {
            if (_hands.Snapshot.Equipment.Count >= items) return true;
            var armory = _hands.First(BuildingKind.Armory) ?? _hands.Build(BuildingKind.Armory, wait, "склад экипировки");
            if (armory == null) return false;
            EnsureChain(ResourceKind.IronSword, armory, wait, 0);
            // the forge's swords go to the armory, not to the market, until it holds enough
            var market = _hands.First(BuildingKind.Market);
            foreach (var forge in _hands.BuildingsOf(BuildingKind.Forge))
            {
                var ids = _hands.Snapshot.Units
                    .Where(u => u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.SourceId == forge.Id &&
                                market != null && u.Assignment.DestinationId == market.Id && u.Assignment.CarriesAnything)
                    .Select(u => u.Id).ToList();
                if (ids.Count > 0) _hands.Dispatch(new AssignHaulCommand(ids, forge.Id, armory.Id));
            }
            return false;
        }

        private UnitKind WorkerFor(QuestGoal goal) => goal.AnyUnit ? _hands.WorkerFor(goal.Building) : goal.Unit;

        private void WorkAt(QuestGoal goal, BotWait wait)
        {
            UnitKind? counted = goal.AnyUnit ? null : goal.Unit;
            int missing = goal.Amount - _hands.BuildingsOf(goal.Building).Sum(b => _hands.Workers(b.Id, counted));
            for (int guard = 0; missing > 0 && guard < 4; guard++)
            {
                var building = _hands.BuildingsOf(goal.Building).FirstOrDefault(b => b.WorkerCount < b.MaxWorkers) ??
                               _hands.Build(goal.Building, wait, _hands.Def(goal.Building).DisplayName);
                if (building == null) return;
                int staffed = _hands.Staff(building, WorkerFor(goal), missing, wait, "рабочие по заданию");
                if (staffed == 0) return;
                missing -= staffed;
            }
        }

        private void HaulRoute(QuestGoal goal, BotWait wait)
        {
            var source = _hands.First(goal.Building) ??
                         _hands.Build(goal.Building, wait, _hands.Def(goal.Building).DisplayName);
            var destination = _hands.First(goal.Destination) ??
                              _hands.Build(goal.Destination, wait, _hands.Def(goal.Destination).DisplayName);
            if (source == null || destination == null) return;
            UnitKind? counted = goal.AnyUnit ? null : goal.Unit;
            int have = 0;
            foreach (var s in _hands.BuildingsOf(goal.Building))
            foreach (var d in _hands.BuildingsOf(goal.Destination))
                have += _hands.Haulers(s.Id, d.Id, counted);
            if (have < goal.Amount)
                _hands.Haul(source, destination, goal.Amount - have, wait, "носильщики по заданию",
                    kind: goal.AnyUnit ? null : goal.Unit);
        }

        private void Upgrade(QuestGoal goal, BotWait wait)
        {
            var best = _hands.BuildingsOf(goal.Building).OrderByDescending(b => b.Level).FirstOrDefault() ??
                       _hands.Build(goal.Building, wait, _hands.Def(goal.Building).DisplayName);
            if (best != null && best.Level < goal.Amount)
                _hands.Upgrade(best, wait, $"улучшение: {best.Name}");
        }

        /// <summary>Something must be on its way to the market; the economy keeper scales what is there.</summary>
        private void EnsureSales(BotWait wait)
        {
            var market = _hands.First(BuildingKind.Market);
            if (market == null) return;
            if (_hands.Routes().Any(r => r.Destination == market.Id))
            {
                wait.Note("ждёт продаж");
                return;
            }
            var source = _hands.Snapshot.Buildings.FirstOrDefault(b => b.TotalStock > 0 && _hands.CanHaul(b, market)) ??
                         _hands.Snapshot.Buildings.FirstOrDefault(b => b.WorkerCount > 0 && _hands.CanHaul(b, market));
            if (source != null) _hands.Haul(source, market, 1, wait, "носильщик на рынок");
            else wait.Note("нечего продавать");
        }

        /// <summary>
        /// A producer of the resource staffed, its inputs supplied, and a route from it to the destination:
        /// the building is built when the colony has none, its inputs come the same way one level deeper.
        /// </summary>
        public void EnsureChain(ResourceKind resource, BuildingSnapshot destination, BotWait wait, int depth)
        {
            if (destination == null || depth > MaxChainDepth) return;
            var producer = _hands.Snapshot.Buildings
                .Where(b => _hands.Makes(b.Kind, resource))
                .OrderByDescending(b => b.WorkerCount).FirstOrDefault();
            if (producer == null)
            {
                var kind = _hands.Catalog.Buildings
                    .Where(d => d != null && d.Constructible && _hands.IsUnlocked(d.Kind) && _hands.Makes(d.Kind, resource))
                    .OrderBy(d => _hands.Session.BuildingPrice(d.Kind))
                    .Select(d => (BuildingKind?)d.Kind).FirstOrDefault();
                if (kind == null)
                {
                    wait.Note($"нечем делать: {_hands.Session.ResourceName(resource)}");
                    return;
                }
                producer = _hands.Build(kind.Value, wait, _hands.Def(kind.Value).DisplayName);
                if (producer == null) return;
            }

            // a by-product or recipe that opens at a higher level asks for the upgrade first
            int level = _hands.MinLevelFor(producer.Kind, resource);
            if (producer.Level < level)
            {
                _hands.Upgrade(producer, wait, $"уровень {level}: {producer.Name}");
                producer = _hands.Building(producer.Id);
                if (producer == null || producer.Level < level) return;
            }

            if (producer.WorkerCount < ChainWorkers)
                _hands.Staff(producer, _hands.WorkerFor(producer.Kind), ChainWorkers - producer.WorkerCount, wait,
                    $"рабочие: {producer.Name}");
            var recipe = _hands.RecipeFor(producer.Kind, resource);
            if (recipe != null)
                foreach (var input in recipe.Inputs)
                    EnsureSupply(input.Resource, _hands.Building(producer.Id), wait, depth + 1);
            var current = _hands.Building(producer.Id);
            if (recipe != null && current?.ProductionState == ProductionState.MissingInputs)
                foreach (var input in recipe.Inputs)
                    if (_hands.StockOf(current, input.Resource) < input.Amount)
                        Redirect(input.Resource, current);
            // a by-product (meat and milk at the farm, scrap at the forge) that nobody takes fills the building up
            if (current?.ProductionState == ProductionState.OutputFull)
                ClearSurplus(current, resource, wait);

            producer = _hands.Building(producer.Id);
            if (producer != null && _hands.Haulers(producer.Id, destination.Id) == 0)
                _hands.Haul(producer, destination, 1, wait, $"носильщик: {producer.Name} → {destination.Name}");
        }

        /// <summary>
        /// The chain's workshop stands idle while the building that holds its input sells that input: half of
        /// the haulers taking goods from there to the market carry to the workshop instead, while its route has
        /// room for them.
        /// </summary>
        private void Redirect(ResourceKind resource, BuildingSnapshot consumer)
        {
            var market = _hands.First(BuildingKind.Market);
            if (market == null) return;
            foreach (var route in _hands.Routes().Where(r => r.Destination == market.Id).ToList())
            {
                var source = _hands.Building(route.Source);
                if (source == null || source.Id == consumer.Id ||
                    (!_hands.Makes(source.Kind, resource) && _hands.StockOf(source, resource) == 0) ||
                    !ColonySimulation.Provides(_hands.Def(source.Kind), resource) || !_hands.CanHaul(source, consumer))
                    continue;
                int room = _profile.MaxHaulersPerRoute - _hands.Haulers(source.Id, consumer.Id);
                if (room <= 0) continue;
                // a hauler sent with chosen goods (a by-product clearing) stays on its own errand
                var ids = _hands.Snapshot.Units
                    .Where(u => u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.SourceId == source.Id &&
                                u.Assignment.DestinationId == market.Id && u.Assignment.MayCarry(resource))
                    .Select(u => u.Id).ToList();
                int move = Math.Min(room, (ids.Count + 1) / 2);
                if (move > 0) _hands.Dispatch(new AssignHaulCommand(ids.Take(move).ToList(), source.Id, consumer.Id));
            }
        }

        /// <summary>One hauler takes the producer's other goods to the market, so its by-products never block it.</summary>
        private void ClearSurplus(BuildingSnapshot producer, ResourceKind kept, BotWait wait)
        {
            var market = _hands.First(BuildingKind.Market);
            if (market == null || producer.Id == market.Id) return;
            var surplus = ColonySimulation.ProvidedResources(producer.Kind, _hands.Catalog)
                .Where(r => r != kept && _hands.StockOf(producer, r) > 0).ToList();
            if (surplus.Count == 0) return;
            bool taken = _hands.Snapshot.Units.Any(u => u.Assignment.Kind == AssignmentKind.Haul &&
                u.Assignment.SourceId == producer.Id && u.Assignment.DestinationId == market.Id &&
                !u.Assignment.CarriesAnything && surplus.All(u.Assignment.MayCarry));
            if (taken) return;
            // one of the haulers already queuing at the producer takes the surplus; a new one only if none waits
            var spare = _hands.Snapshot.Units
                .Where(u => u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.SourceId == producer.Id)
                .OrderByDescending(u => u.Assignment.Phase == HaulPhase.QueuedAtSource).Select(u => u.Id).FirstOrDefault();
            if (spare != null) _hands.Dispatch(new AssignHaulCommand(new[] { spare }, producer.Id, market.Id, surplus));
            else _hands.Haul(producer, market, 1, wait, $"лишнее на рынок: {producer.Name}", surplus);
        }

        /// <summary>A route into the consumer from a building that hands out the resource, or a new chain.</summary>
        private void EnsureSupply(ResourceKind resource, BuildingSnapshot consumer, BotWait wait, int depth)
        {
            if (consumer == null) return;
            foreach (var route in _hands.Routes())
            {
                if (route.Destination != consumer.Id) continue;
                var source = _hands.Building(route.Source);
                if (source == null || !ColonySimulation.Provides(_hands.Def(source.Kind), resource)) continue;
                // a producer whose workers fell in battle or left supplies nothing: the chain staffs it again
                if (source.IsWorkplace && source.WorkerCount == 0 && _hands.Makes(source.Kind, resource)) break;
                // the supplier makes other goods too; when they fill it up it stops supplying this one
                if (source.ProductionState == ProductionState.OutputFull) ClearSurplus(source, resource, wait);
                return;
            }
            EnsureChain(resource, consumer, wait, depth);
        }
    }
}
