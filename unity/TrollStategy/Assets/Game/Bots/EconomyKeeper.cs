using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Keeps goods moving the way any attentive player would: everything made has somewhere to go, a workshop
    /// short of inputs gets them brought, a building whose goods pile up gets another hauler. Growth, for the
    /// profiles that grow, puts spare gold into more workers, more raw producers and the upgrades it favours.
    /// </summary>
    internal sealed class EconomyKeeper
    {
        private const int MaxHiresPerLook = 3;
        private const int MaxGrowthStaffPerLook = 2;
        private const int StockpileBacklog = 30;
        private const int RestaffWorkers = 2;

        private readonly BotHands _hands;
        private readonly BattlePlanner _battles;
        private readonly BotProfile _profile;
        private readonly QuestPlanner _planner;
        private readonly Dictionary<string, int> _lastOutput = new(StringComparer.Ordinal);

        public EconomyKeeper(BotHands hands, BattlePlanner battles, BotProfile profile, QuestPlanner planner)
        {
            _hands = hands;
            _battles = battles;
            _profile = profile;
            _planner = planner;
        }

        /// <summary>Gold growth may spend: the treasury less the profile's share of what the quest saves for.</summary>
        private int Spare(BotWait wait) =>
            Math.Max(0, _hands.Gold - (int)Math.Ceiling(wait.GoldNeeded * _profile.ReserveShare));

        public void Keep(BotWait wait)
        {
            // a producer with nowhere to send its goods stalls and earns nothing: these may use all gold
            _hands.SpendLimit = int.MaxValue;
            EmployIdle();
            Restaff();
            foreach (var id in _hands.Snapshot.Buildings.Select(b => b.Id).ToList())
                OpenOutlet(_hands.Building(id));
            foreach (var id in _hands.Snapshot.Buildings.Select(b => b.Id).ToList())
                Unblock(_hands.Building(id));
            foreach (var id in _hands.Snapshot.Buildings.Select(b => b.Id).ToList())
                Feed(_hands.Building(id), scale: false);

            _hands.SpendLimit = Spare(wait);
            int hires = 0;
            foreach (var id in _hands.Snapshot.Buildings.Select(b => b.Id).ToList())
            {
                if (hires >= MaxHiresPerLook) break;
                var building = _hands.Building(id);
                if (building != null && BackingUp(building) && AddHauler(building)) hires++;
                else if (Feed(_hands.Building(id), scale: true)) hires++;
            }
            foreach (var building in _hands.Snapshot.Buildings)
                _lastOutput[building.Id] = OutputStock(building);
        }

        public void Grow(BotWait wait)
        {
            _hands.SpendLimit = Spare(wait);
            // a land buyer buys land first, as soon as spare gold covers it: the rest of growth takes what is left
            if (_profile.BuysLand) GrowLand();
            int staffed = 0;
            // a colony of crafts fills its workshops first, the others their raw producers
            var producers = _profile.Economy == BotEconomy.Crafts
                ? Producers().OrderBy(b => _hands.IsRaw(b.Kind))
                : Producers().OrderByDescending(b => _hands.IsRaw(b.Kind));
            foreach (var building in producers.ToList())
            {
                if (staffed >= MaxGrowthStaffPerLook) break;
                var current = _hands.Building(building.Id);
                if (current == null || current.WorkerCount == 0 || current.WorkerCount >= current.MaxWorkers) continue;
                if (current.ProductionState != ProductionState.Working || !HasOutlet(current)) continue;
                staffed += _hands.Staff(current, _hands.WorkerFor(current.Kind), 1, null, "рост: рабочий");
            }
            // a bot growing upward raises a full building before it builds another; with none to raise it builds
            if (_profile.Growth != BotGrowth.Up || !GrowUp())
            {
                if (_profile.Economy == BotEconomy.Crafts) GrowCrafts();
                else GrowRaw();
            }
            GrowUpgrades();
            GrowForce();
        }

        // The cheapest level up of a producer that works full and has an outlet; false when none can rise.
        private bool GrowUp()
        {
            var next = Producers().Where(b => b.UpgradeCost >= 0 && b.WorkerCount >= b.MaxWorkers &&
                                              b.ProductionState == ProductionState.Working && HasOutlet(b))
                .OrderBy(b => b.UpgradeCost).FirstOrDefault();
            if (next == null) return false;
            if (_hands.SpendLimit >= next.UpgradeCost * _profile.GrowthGoldFactor)
                _hands.Upgrade(next, null, $"рост вверх: {next.Name} на уровень {next.Level + 1}");
            return true;
        }

        // Crafts: gold from finished goods. A workshop waiting for an input gets one more producer of it once those it
        // has work full; when every workshop is fed and full, the one whose product sells best gets a copy.
        private void GrowCrafts()
        {
            var workshops = Producers().Where(b => !_hands.IsRaw(b.Kind)).ToList();
            var hungry = workshops.FirstOrDefault(b => b.WorkerCount > 0 && b.ProductionState == ProductionState.MissingInputs);
            if (hungry != null)
            {
                var missing = _hands.Def(hungry.Kind).Recipes.SelectMany(r => r.Inputs)
                    .Where(i => _hands.StockOf(hungry, i.Resource) < i.Amount).Select(i => i.Resource).FirstOrDefault();
                var makers = Producers().Where(b => _hands.Makes(b.Kind, missing)).ToList();
                if (makers.Count == 0 || makers.Any(b => b.WorkerCount < b.MaxWorkers)) return;
                var kind = Cheapest(d => _hands.Makes(d.Kind, missing));
                if (kind == null || !CanSetUp(kind.Value)) return;
                var maker = _hands.Build(kind.Value, null, $"рост: ещё {_hands.Session.ResourceName(missing).ToLowerInvariant()} для «{hungry.Name}»");
                if (maker == null) return;
                _hands.Staff(maker, _hands.WorkerFor(maker.Kind), 2, null, "рост: рабочие");
                _hands.Haul(_hands.Building(maker.Id), _hands.Building(hungry.Id), 1, null, $"подвоз: в «{hungry.Name}»");
                return;
            }
            var best = workshops.Where(b => b.WorkerCount >= b.MaxWorkers && b.ProductionState == ProductionState.Working)
                .OrderByDescending(b => Value(b.Kind)).FirstOrDefault();
            if (best == null || _hands.BuildingsOf(best.Kind).Count >= 1 + _profile.MaxRawProducers / 2) return;
            if (!CanSetUp(best.Kind)) return;
            var copy = _hands.Build(best.Kind, null, $"рост: ещё «{_hands.Def(best.Kind).DisplayName}» — её товар дороже всех");
            if (copy != null) _hands.Staff(copy, _hands.WorkerFor(copy.Kind), 2, null, "рост: мастера");
        }

        // the best price among a building's products
        private int Value(BuildingKind kind) => _hands.Def(kind).Recipes.SelectMany(r => r.Outputs)
            .Select(o => _hands.Catalog.TryGetResource(o.Resource)?.SellPrice ?? 0).DefaultIfEmpty(0).Max();

        private BuildingKind? Cheapest(Func<BuildingDefinition, bool> fits) => _hands.Catalog.Buildings
            .Where(d => d != null && d.Constructible && _hands.IsUnlocked(d.Kind) && fits(d))
            .OrderBy(d => _hands.Session.BuildingPrice(d.Kind)).Select(d => (BuildingKind?)d.Kind).FirstOrDefault();

        // the building, two workers and two haulers, the growth factor times over
        private bool CanSetUp(BuildingKind kind) =>
            _hands.SpendLimit >= (_hands.Session.BuildingPrice(kind) + _hands.Session.HirePrice(_hands.WorkerFor(kind), 2) +
                                  _hands.Session.HirePrice(_hands.HaulerKind(), 2)) * _profile.GrowthGoldFactor;

        // The army strategy: a full squad of trolls ahead of the battles, or a weapon for every fighter of the squad.
        private void GrowForce()
        {
            if (_profile.Force == BotForce.Small) return;
            var mission = _battles.OpenMission();
            if (mission == null) return;
            int squad = _hands.Session.SquadLimit(mission);
            if (_profile.Force == BotForce.Large)
            {
                // the army is the free trolls: those at work stay at work when the squad marches out
                int army = _hands.Idle(UnitKind.Troll).Count;
                if (!_hands.IsUnlocked(UnitKind.Troll) || army >= squad) return;
                if (_hands.SpendLimit < _hands.Session.HirePrice(UnitKind.Troll) * _profile.GrowthGoldFactor) return;
                _hands.Hire(UnitKind.Troll, 1, null, $"армия: тролль {army + 1} из {squad}");
                return;
            }
            if (_hands.Snapshot.Equipment.Count < squad && _hands.IsUnlocked(BuildingKind.Armory))
                _planner.EnsureEquipment(squad, new BotWait());
        }

        // Land with spare gold before a building needs it: the nearest block bought, then cleared; within a cap
        // (wider for a bot that builds over the whole island).
        private void GrowLand()
        {
            var land = _hands.Snapshot.Land;
            if (land == null || land.Blocks.Any(b => b.Clearing)) return;
            int cap = _profile.Layout == BotLayout.Spread ? 40 : 24;
            bool wild = land.Blocks.Any(b => b.Wild);
            if (!wild && (land.Blocks.Count(b => b.Owned) >= cap || !land.Blocks.Any(b => b.CanBuy))) return;
            int cost = wild ? land.ClearGold : land.NextPrice + land.ClearGold;
            if (_hands.SpendLimit < Math.Max(1, cost)) return;
            _hands.ExpandLand(null, "земля про запас");
        }

        private void GrowRaw()
        {
            var raws = Producers().Where(b => _hands.IsRaw(b.Kind)).ToList();
            if (raws.Count == 0 || raws.Count >= _profile.MaxRawProducers) return;
            if (raws.Any(b => b.WorkerCount < b.MaxWorkers || b.ProductionState != ProductionState.Working)) return;
            var kind = _hands.Catalog.Buildings
                .Where(d => d != null && d.Constructible && _hands.IsUnlocked(d.Kind) && _hands.IsRaw(d.Kind))
                .OrderBy(d => _hands.Session.BuildingPrice(d.Kind))
                .Select(d => (BuildingKind?)d.Kind).FirstOrDefault();
            if (kind == null) return;
            int setUp = _hands.Session.BuildingPrice(kind.Value) +
                        _hands.Session.HirePrice(_hands.WorkerFor(kind.Value), 2) +
                        _hands.Session.HirePrice(_hands.HaulerKind(), 2);
            if (_hands.SpendLimit < setUp * _profile.GrowthGoldFactor) return;
            var built = _hands.Build(kind.Value, null, "рост: добыча");
            var market = _hands.First(BuildingKind.Market);
            if (built == null || market == null) return;
            _hands.Staff(built, _hands.WorkerFor(built.Kind), 2, null, "рост: рабочие");
            _hands.Haul(_hands.Building(built.Id), market, 2, null, "рост: носильщики");
        }

        // The cheapest open upgrade of each building the profile invests in, once spare gold covers it the growth
        // factor times: with the building's price while the colony has none, or the building's next level when
        // every next upgrade level waits for it.
        private void GrowUpgrades()
        {
            foreach (var host in _profile.UpgradeHosts)
            {
                var left = _hands.Snapshot.Upgrades.Where(u => u.Host == host && !u.IsMaxed).ToList();
                if (left.Count == 0 || (!left[0].HostBuilt && !_hands.IsUnlocked(host))) continue;
                var open = left.Where(u => u.IsOpen).OrderBy(u => u.NextCost).FirstOrDefault();
                int cost = open != null ? open.NextCost
                    : !left[0].HostBuilt ? _hands.Session.BuildingPrice(host) + left.Min(u => u.NextCost)
                    : _hands.HostOf(host)?.UpgradeCost ?? -1;
                if (cost < 0 || _hands.SpendLimit < cost * _profile.GrowthGoldFactor) continue;
                _hands.BuyUpgrade(null, "рост", host);
            }
        }

        public void FightForGold(BotWait wait)
        {
            _hands.SpendLimit = Spare(wait);
            _battles.TryFightForGold(null);
        }

        private IEnumerable<BuildingSnapshot> Producers() =>
            _hands.Snapshot.Buildings.Where(b => b.IsWorkplace);

        private bool HasOutlet(BuildingSnapshot building) => _hands.Routes().Any(r => r.Source == building.Id);

        // Goods the building hands to haulers: a producer's outputs, a stockpile's whole stock.
        private int OutputStock(BuildingSnapshot building)
        {
            var def = _hands.Def(building.Kind);
            int total = 0;
            foreach (var stack in building.Stock)
                if (ColonySimulation.Provides(def, stack.Resource)) total += stack.Amount;
            return total;
        }

        // Idle creatures take free places in producers that already work and have an outlet: a creature its
        // favourite building first, then raw producers. One order per building, as a player selects a group.
        private void EmployIdle()
        {
            var idle = _hands.Snapshot.Units.Where(u => u.Assignment.Kind == AssignmentKind.Idle).ToList();
            // a large army's free trolls are its squad, not workers waiting for a place
            if (_profile.Force == BotForce.Large)
            {
                var army = idle.Where(u => u.UnitKind == UnitKind.Troll).Take(ArmySize()).Select(u => u.Id).ToHashSet();
                idle.RemoveAll(u => army.Contains(u.Id));
            }
            if (idle.Count == 0) return;
            var places = Producers().Where(b => b.WorkerCount > 0 && b.WorkerCount < b.MaxWorkers && HasOutlet(b))
                .ToList();
            var free = places.ToDictionary(b => b.Id, b => b.MaxWorkers - b.WorkerCount);
            var orders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var unit in idle)
            {
                var def = _hands.Catalog.GetUnit(unit.UnitKind);
                var place = places.Where(b => free[b.Id] > 0)
                    .OrderByDescending(b => def.Favors(b.Kind)).ThenByDescending(b => _hands.IsRaw(b.Kind))
                    .FirstOrDefault();
                if (place == null) break;
                free[place.Id]--;
                if (!orders.TryGetValue(place.Id, out var ids)) orders[place.Id] = ids = new List<string>();
                ids.Add(unit.Id);
            }
            foreach (var place in places)
                if (orders.TryGetValue(place.Id, out var ids))
                    _hands.Dispatch(new AssignWorkCommand(ids, place.Id));
        }

        // the squad the open arena level takes; 0 before the arena opens
        private int ArmySize()
        {
            var mission = _battles.OpenMission();
            return mission != null ? _hands.Session.SquadLimit(mission) : 0;
        }

        // A producer with an outlet whose workers all fell in battle gets its chain's workers back.
        private void Restaff()
        {
            foreach (var building in Producers().Where(b => b.WorkerCount == 0 && HasOutlet(b)).ToList())
                _hands.Staff(building, _hands.WorkerFor(building.Kind), RestaffWorkers, null,
                    $"на место павших: {building.Name}");
        }

        // A producer full of goods no hauler takes anywhere they are wanted (a by-product its routes do not carry:
        // crystal on the way to a smeltery) stops for all its goods: those go to the market on their own.
        private void Unblock(BuildingSnapshot building)
        {
            if (building == null || !building.IsWorkplace || building.ProductionState != ProductionState.OutputFull) return;
            var market = _hands.First(BuildingKind.Market);
            if (market == null || !_hands.CanHaul(building, market)) return;
            var def = _hands.Def(building.Kind);
            var stuck = building.Stock.Where(s => s.Amount > 0 && ColonySimulation.Provides(def, s.Resource) &&
                                                  !_hands.Served(building, s.Resource))
                .Select(s => s.Resource).Distinct().ToList();
            if (stuck.Count > 0) _hands.Haul(building, market, 1, null, $"вывоз лишнего: {building.Name}", stuck);
        }

        private void OpenOutlet(BuildingSnapshot building)
        {
            if (building == null || HasOutlet(building)) return;
            var def = _hands.Def(building.Kind);
            bool producing = def.IsWorkplace && (building.WorkerCount > 0 || OutputStock(building) > 0);
            bool stocked = def.StorageRole == StorageRole.Stockpile && building.TotalStock > 0;
            if (!producing && !stocked) return;
            var outlet = Outlet(building);
            if (outlet != null) _hands.Haul(building, outlet, 1, null, $"вывоз: {building.Name}");
        }

        // A working consumer of the building's goods that lacks them, else the market.
        private BuildingSnapshot Outlet(BuildingSnapshot building)
        {
            var def = _hands.Def(building.Kind);
            foreach (var consumer in Producers())
            {
                if (consumer.Id == building.Id || consumer.WorkerCount == 0 ||
                    consumer.ProductionState != ProductionState.MissingInputs) continue;
                var consumerDef = _hands.Def(consumer.Kind);
                if (building.Stock.Any(s => ColonySimulation.Provides(def, s.Resource) &&
                                            consumerDef.ConsumesInRecipe(s.Resource)) &&
                    _hands.CanHaul(building, consumer))
                    return consumer;
            }
            var market = _hands.First(BuildingKind.Market);
            return _hands.CanHaul(building, market) ? market : null;
        }

        // A staffed workshop that cannot start a cycle gets each missing input from a building that holds or
        // makes it: a new route when none brings it, with <paramref name="scale"/> one more hauler on a route
        // whose source has goods waiting. Returns whether a hauler was added.
        private bool Feed(BuildingSnapshot consumer, bool scale)
        {
            if (consumer == null || consumer.WorkerCount == 0 ||
                consumer.ProductionState != ProductionState.MissingInputs) return false;
            foreach (var recipe in _hands.Def(consumer.Kind).Recipes)
            {
                var missing = recipe.Inputs.Where(i => _hands.StockOf(consumer, i.Resource) < i.Amount)
                    .Select(i => i.Resource).ToList();
                var sources = missing.Select(r => Source(r, consumer)).ToList();
                if (missing.Count == 0 || sources.Any(s => s == null)) continue;
                // a by-product clearing that sells the very input the workshop waits for gives it up
                for (int i = 0; i < missing.Count; i++)
                    _hands.StopSelling(missing[i], sources[i], consumer, everyone: false);
                bool added = false;
                foreach (var source in sources)
                {
                    int haulers = _hands.Haulers(source.Id, consumer.Id);
                    bool open = haulers == 0 && !scale;
                    bool more = scale && haulers > 0 && OutputStock(source) > 0 &&
                                haulers < _profile.MaxHaulersPerRoute;
                    if (open || more)
                        added |= _hands.Haul(source, consumer, 1, null,
                            $"подвоз: {source.Name} → {consumer.Name}") > 0;
                }
                return added && scale;
            }
            return false;
        }

        // A building that already holds the resource for haulers, else one that makes it.
        private BuildingSnapshot Source(ResourceKind resource, BuildingSnapshot consumer)
        {
            BuildingSnapshot maker = null;
            foreach (var building in _hands.Snapshot.Buildings)
            {
                if (building.Id == consumer.Id || !ColonySimulation.Provides(_hands.Def(building.Kind), resource)) continue;
                if (!_hands.CanHaul(building, consumer)) continue;
                if (_hands.StockOf(building, resource) > 0) return building;
                if (maker == null && _hands.Makes(building.Kind, resource) && building.WorkerCount > 0) maker = building;
            }
            return maker;
        }

        private bool BackingUp(BuildingSnapshot building)
        {
            if (!HasOutlet(building)) return false;
            if (building.ProductionState == ProductionState.OutputFull) return true;
            int output = OutputStock(building);
            _lastOutput.TryGetValue(building.Id, out int last);
            int threshold = _hands.Def(building.Kind).StorageRole == StorageRole.Stockpile
                ? StockpileBacklog
                : Math.Max(6, building.Capacity / 4);
            return output >= threshold && output >= last;
        }

        private bool AddHauler(BuildingSnapshot building)
        {
            var route = _hands.Routes().Where(r => r.Source == building.Id)
                .OrderBy(r => _hands.Haulers(r.Source, r.Destination)).FirstOrDefault();
            if (route.Source == null || _hands.Haulers(route.Source, route.Destination) >= _profile.MaxHaulersPerRoute)
                return false;
            return _hands.Haul(building, _hands.Building(route.Destination), 1, null, $"вывоз: {building.Name}") > 0;
        }
    }
}
