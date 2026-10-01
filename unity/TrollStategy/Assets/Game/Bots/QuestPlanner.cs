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
            }
        }

        private UnitKind WorkerFor(QuestGoal goal) => goal.AnyUnit ? _hands.Hireable(_profile.WorkerKind) : goal.Unit;

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
                    kind: goal.AnyUnit ? UnitKind.Goblin : goal.Unit);
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

            if (producer.WorkerCount < ChainWorkers)
                _hands.Staff(producer, _hands.Hireable(_profile.WorkerKind), ChainWorkers - producer.WorkerCount, wait,
                    $"рабочие: {producer.Name}");
            var recipe = _hands.RecipeFor(producer.Kind, resource);
            if (recipe != null)
                foreach (var input in recipe.Inputs)
                    EnsureSupply(input.Resource, _hands.Building(producer.Id), wait, depth + 1);
            var current = _hands.Building(producer.Id);
            if (recipe != null && current?.ProductionState == ProductionState.MissingInputs)
                foreach (var input in recipe.Inputs)
                    Redirect(input.Resource, current);

            producer = _hands.Building(producer.Id);
            if (producer != null && _hands.Haulers(producer.Id, destination.Id) == 0)
                _hands.Haul(producer, destination, 1, wait, $"носильщик: {producer.Name} → {destination.Name}");
        }

        /// <summary>
        /// The chain's workshop stands idle while the building that holds its input sells that input: half of
        /// the haulers taking goods from there to the market carry to the workshop instead.
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
                var ids = _hands.Snapshot.Units
                    .Where(u => u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.SourceId == source.Id &&
                                u.Assignment.DestinationId == market.Id)
                    .Select(u => u.Id).ToList();
                int move = (ids.Count + 1) / 2;
                if (move > 0) _hands.Dispatch(new AssignHaulCommand(ids.Take(move).ToList(), source.Id, consumer.Id));
            }
        }

        /// <summary>A route into the consumer from a building that hands out the resource, or a new chain.</summary>
        private void EnsureSupply(ResourceKind resource, BuildingSnapshot consumer, BotWait wait, int depth)
        {
            if (consumer == null) return;
            foreach (var route in _hands.Routes())
            {
                if (route.Destination != consumer.Id) continue;
                var source = _hands.Building(route.Source);
                if (source != null && ColonySimulation.Provides(_hands.Def(source.Kind), resource)) return;
            }
            EnsureChain(resource, consumer, wait, depth);
        }
    }
}
