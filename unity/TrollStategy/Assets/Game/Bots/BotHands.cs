using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>What a look at the colony found its quest waiting for, and the gold growth must leave alone.</summary>
    internal sealed class BotWait
    {
        /// <summary>Gold of the first quest step the colony could not afford; 0 when none.</summary>
        public int GoldNeeded { get; private set; }
        public bool Time { get; private set; }
        /// <summary>The first thing the quest waits for, in words for the report.</summary>
        public string Reason { get; private set; }

        public BotWaitKind Kind => GoldNeeded > 0 ? BotWaitKind.Gold : Time ? BotWaitKind.Time : BotWaitKind.Flow;

        public void NeedGold(int cost, string what)
        {
            if (GoldNeeded > 0) return;
            GoldNeeded = Math.Max(1, cost);
            Reason = $"золото: {what} ({cost})";
        }

        public void NeedTime(string what)
        {
            Time = true;
            Reason ??= what;
        }

        public void Note(string what) => Reason ??= what;
    }

    /// <summary>
    /// The bot's hands: it reads the session's snapshot and sends the same commands the HUD sends, so the
    /// session validates every move. Spending checks the treasury and, for growth, a limit the caller sets.
    /// </summary>
    internal sealed class BotHands
    {
        private readonly BotRun _run;
        private readonly BotProfile _profile;

        public BotHands(GameSession session, BotRun run, BotProfile profile)
        {
            Session = session;
            Catalog = session.Catalog;
            Economy = session.Catalog.Economy;
            _run = run;
            _profile = profile;
            Dice = new BotDice(profile.Seed, 1);
            Refresh();
        }

        /// <summary>The choices this bot takes while it plays: its own dice, the same in every run of it.</summary>
        public BotDice Dice { get; }

        public GameSession Session { get; }
        public GameContentCatalog Catalog { get; }
        public EconomyConfig Economy { get; }
        public GameSnapshot Snapshot { get; private set; }
        public int Gold => Snapshot.Gold;
        /// <summary>Gold this phase of a look may still spend; quest steps may spend everything.</summary>
        public int SpendLimit { get; set; } = int.MaxValue;

        public void Refresh() => Snapshot = Session.CurrentSnapshot;

        public bool Dispatch(IGameCommand command)
        {
            var result = Session.Dispatch(command);
            if (result.Ok)
            {
                _run.CommandsAccepted++;
                Refresh();
            }
            else
            {
                _run.Refused($"{command.GetType().Name}: {result.Error}");
            }
            return result.Ok;
        }

        // ---------- queries

        public BuildingDefinition Def(BuildingKind kind) => Catalog.GetBuilding(kind);

        public BuildingSnapshot Building(string id)
        {
            foreach (var building in Snapshot.Buildings)
                if (building.Id == id) return building;
            return null;
        }

        public List<BuildingSnapshot> BuildingsOf(BuildingKind kind) =>
            Snapshot.Buildings.Where(b => b.Kind == kind).ToList();

        public BuildingSnapshot First(BuildingKind kind) => Snapshot.Buildings.FirstOrDefault(b => b.Kind == kind);

        public bool IsUnlocked(BuildingKind kind) => Snapshot.Progress.IsBuildingUnlocked(kind);
        public bool IsUnlocked(UnitKind kind) => Snapshot.Progress.IsUnitUnlocked(kind);

        /// <summary>The kind asked for when it can be hired, else the goblin every colony starts with.</summary>
        public UnitKind Hireable(UnitKind kind) => IsUnlocked(kind) ? kind : UnitKind.Goblin;

        /// <summary>The highest arena level the colony has won; 0 before its first win.</summary>
        public int ArenaLevel() => Session.ArenaLadder()
            .Where(m => Session.MissionWins(m.MissionId) > 0).Select(m => m.Level).DefaultIfEmpty(0).Max();

        // ---------- whom to hire

        /// <summary>Who to hire to work at the building: see <see cref="BotProfile.RoleHiring"/>.</summary>
        public UnitKind WorkerFor(BuildingKind building) =>
            _profile.RoleHiring > 0f ? Best(unit => BotHiring.Work(unit, building)) : Hireable(_profile.WorkerKind);

        /// <summary>Who to hire to carry goods: see <see cref="BotProfile.RoleHiring"/>.</summary>
        public UnitKind HaulerKind() => _profile.RoleHiring > 0f ? Best(BotHiring.Carry) : UnitKind.Goblin;

        private UnitKind Best(Func<UnitDefinition, float> value) =>
            BotHiring.Best(Catalog.Units.Where(u => u != null && u.Hireable && IsUnlocked(u.Kind)),
                u => Session.HirePrice(u.Kind), value, _profile.RoleHiring)?.Kind ?? UnitKind.Goblin;

        public int CountUnits(UnitKind? kind) => Snapshot.Units.Count(u => kind == null || u.UnitKind == kind);

        public List<UnitSnapshot> Idle(UnitKind kind) =>
            Snapshot.Units.Where(u => u.UnitKind == kind && u.Assignment.Kind == AssignmentKind.Idle).ToList();

        public int Workers(string buildingId, UnitKind? kind = null) => Snapshot.Units.Count(u =>
        {
            var a = u.Assignment;
            return (kind == null || u.UnitKind == kind) && a.BuildingId == buildingId &&
                   (a.Kind == AssignmentKind.Work || a.Kind == AssignmentKind.ToWork);
        });

        public int Haulers(string sourceId, string destinationId, UnitKind? kind = null) => Snapshot.Units.Count(u =>
        {
            var a = u.Assignment;
            return (kind == null || u.UnitKind == kind) && a.Kind == AssignmentKind.Haul && a.SourceId == sourceId &&
                   a.DestinationId == destinationId;
        });

        /// <summary>Every source → destination pair at least one hauler works, in the order units were hired.</summary>
        public List<(string Source, string Destination)> Routes()
        {
            var routes = new List<(string, string)>();
            foreach (var unit in Snapshot.Units)
            {
                var a = unit.Assignment;
                if (a.Kind != AssignmentKind.Haul) continue;
                var route = (a.SourceId, a.DestinationId);
                if (!routes.Contains(route)) routes.Add(route);
            }
            return routes;
        }

        public bool CanHaul(BuildingSnapshot source, BuildingSnapshot destination) =>
            source != null && destination != null && source.Id != destination.Id &&
            ColonySimulation.IsValidHaulRoute(source.Kind, destination.Kind, Catalog);

        public bool Makes(BuildingKind kind, ResourceKind resource)
        {
            foreach (var recipe in Def(kind).Recipes)
                if (recipe.CanYield(resource)) return true;
            return false;
        }

        /// <summary>The recipe of a building that makes the resource, with its inputs; null when it makes none.</summary>
        public ProductionRecipe RecipeFor(BuildingKind kind, ResourceKind resource) =>
            Def(kind).Recipes.Where(r => r.Outputs.Any(o => o.Resource == resource))
                .OrderBy(r => r.MinLevel).ThenBy(r => r.Inputs.Length).FirstOrDefault() ??
            Def(kind).Recipes.Where(r => r.CanYield(resource)).OrderBy(r => r.MinLevel).FirstOrDefault();

        /// <summary>The building level from which the kind can yield the resource at all (a product or a by-product).</summary>
        public int MinLevelFor(BuildingKind kind, ResourceKind resource)
        {
            int best = int.MaxValue;
            foreach (var recipe in Def(kind).Recipes)
            {
                if (recipe.Outputs.Any(o => o.Resource == resource) || recipe.FailOutputs.Any(o => o.Resource == resource))
                    best = Math.Min(best, recipe.MinLevel);
                foreach (var extra in recipe.Extras)
                    if (extra.Output.Resource == resource) best = Math.Min(best, Math.Max(recipe.MinLevel, extra.MinLevel));
            }
            return best == int.MaxValue ? 1 : best;
        }

        /// <summary>Raw producers: workplaces whose recipes need nothing brought in.</summary>
        public bool IsRaw(BuildingKind kind)
        {
            var def = Def(kind);
            return def.IsWorkplace && def.Recipes.All(r => r.Inputs.Length == 0);
        }

        public int StockOf(BuildingSnapshot building, ResourceKind resource)
        {
            foreach (var stack in building.Stock)
                if (stack.Resource == resource) return stack.Amount;
            return 0;
        }

        // ---------- spending

        private bool Affordable(int cost, BotWait wait, string what)
        {
            if (cost > Gold)
            {
                wait?.NeedGold(cost, what);
                return false;
            }
            return cost <= SpendLimit;
        }

        private void Spent(int cost)
        {
            if (SpendLimit != int.MaxValue) SpendLimit -= cost;
        }

        /// <summary>Hires up to <paramref name="amount"/> creatures; returns the new ids (fewer when gold runs short).</summary>
        public List<string> Hire(UnitKind kind, int amount, BotWait wait, string why)
        {
            var hired = new List<string>();
            kind = Hireable(kind);
            int fits = amount;
            while (fits > 0 && !Affordable(Session.HirePrice(kind, fits), fits == amount ? wait : null, why)) fits--;
            if (fits <= 0) return hired;

            var before = new HashSet<string>(Snapshot.Units.Select(u => u.Id));
            var cell = Session.FindSpawnCell();
            for (int n = Math.Min(fits, Economy.MaxUnitsPerCell); n > 0; n--)
            {
                if (!Session.CanBuyUnits(kind, n, cell).Ok) continue;
                int cost = Session.HirePrice(kind, n);
                if (!Dispatch(new BuyUnitsCommand(kind, n, cell))) break;
                Spent(cost);
                _run.Hired += n;
                CountHire(kind, n);
                break;
            }
            hired.AddRange(Snapshot.Units.Where(u => !before.Contains(u.Id)).Select(u => u.Id));
            if (hired.Count == 0) wait?.Note($"некуда поставить нанятых: {why}");
            return hired;
        }

        /// <summary>Idle creatures of the kind first, then new hires; returns up to <paramref name="amount"/> ids.</summary>
        public List<string> Obtain(UnitKind kind, int amount, BotWait wait, string why)
        {
            kind = Hireable(kind);
            var ids = Idle(kind).Take(amount).Select(u => u.Id).ToList();
            if (ids.Count < amount) ids.AddRange(Hire(kind, amount - ids.Count, wait, why));
            return ids;
        }

        public int Staff(BuildingSnapshot building, UnitKind kind, int amount, BotWait wait, string why)
        {
            if (building == null || !building.IsWorkplace) return 0;
            int take = Math.Min(amount, building.MaxWorkers - building.WorkerCount);
            if (take <= 0) return 0;
            kind = Hireable(kind);
            // one worker and nobody idle to send: the card's "hire here", when it offers this very creature
            if (take == 1 && Idle(kind).Count == 0 && Session.SuggestedWorker(building.Kind) == kind &&
                HireHere(building, kind, wait, why))
                return 1;
            var ids = Obtain(kind, take, wait, why);
            if (ids.Count == 0) return 0;
            return Dispatch(new AssignWorkCommand(ids, building.Id)) ? ids.Count : 0;
        }

        // A workplace card's "hire here": one press hires the worker and sends it to the building.
        private bool HireHere(BuildingSnapshot building, UnitKind kind, BotWait wait, string why)
        {
            int cost = Session.HirePrice(kind);
            if (!Affordable(cost, wait, why)) return false;
            var cell = Session.FindSpawnCell();
            if (!Session.CanBuyUnits(kind, 1, cell).Ok) return false;
            if (!Dispatch(new HireWorkerCommand(kind, building.Id, cell))) return false;
            Spent(cost);
            _run.Hired++;
            CountHire(kind, 1);
            return true;
        }

        private void CountHire(UnitKind kind, int amount)
        {
            string name = Catalog.GetUnit(kind).DisplayName;
            _run.HiredByKind.TryGetValue(name, out int count);
            _run.HiredByKind[name] = count + amount;
        }

        public int Haul(BuildingSnapshot source, BuildingSnapshot destination, int amount, BotWait wait, string why,
            IReadOnlyList<ResourceKind> cargo = null, UnitKind? kind = null)
        {
            if (amount <= 0 || !CanHaul(source, destination)) return 0;
            var ids = Obtain(kind ?? HaulerKind(), amount, wait, why);
            if (ids.Count == 0) return 0;
            return Dispatch(new AssignHaulCommand(ids, source.Id, destination.Id, cargo)) ? ids.Count : 0;
        }

        public bool Upgrade(BuildingSnapshot building, BotWait wait, string why)
        {
            if (building == null || building.UpgradeCost < 0) return false;
            int cost = building.UpgradeCost;
            if (!Affordable(cost, wait, why)) return false;
            if (!Dispatch(new UpgradeBuildingCommand(building.Id))) return false;
            Spent(cost);
            return true;
        }

        /// <summary>
        /// Builds one more building of the kind on the free spot nearest the colony's core; without a spot it
        /// buys and clears land. Returns the new building, or null while gold, land or the unlock is missing.
        /// </summary>
        public BuildingSnapshot Build(BuildingKind kind, BotWait wait, string why)
        {
            if (!IsUnlocked(kind) || !Def(kind).Constructible)
            {
                wait?.Note($"не открыто: {Def(kind).DisplayName}");
                return null;
            }
            int cost = Session.BuildingPrice(kind);
            if (!Affordable(cost, wait, why)) return null;
            var cell = FindBuildingCell(kind);
            if (cell == null)
            {
                ExpandLand(wait, $"место под {Def(kind).DisplayName}");
                return null;
            }
            var before = new HashSet<string>(Snapshot.Buildings.Select(b => b.Id));
            if (!Dispatch(new BuildBuildingCommand(kind, cell.Value))) return null;
            Spent(cost);
            return Snapshot.Buildings.FirstOrDefault(b => !before.Contains(b.Id));
        }

        // ---------- land and placement

        /// <summary>Middle of the colony's starting buildings: new buildings and land gather around it.</summary>
        public (float X, float Y) Core()
        {
            float x = 0f, y = 0f;
            int n = 0;
            foreach (var b in Snapshot.Buildings)
            {
                if (Def(b.Kind).Constructible) continue;
                x += b.Cell.X + b.Width * 0.5f;
                y += b.Cell.Y + b.Height * 0.5f;
                n++;
            }
            if (n > 0) return (x / n, y / n);
            var center = LandRules.StartCenter(Economy);
            return (center.X, center.Y);
        }

        /// <summary>
        /// A valid spot that keeps a one-cell lane to every other building and covers no creature: the nearest to
        /// the core, or any of the <see cref="BotProfile.PlacementChoice"/> nearest; null when there is none on
        /// cleared land.
        /// </summary>
        public Cell? FindBuildingCell(BuildingKind kind)
        {
            var def = Def(kind);
            var core = Core();
            float cs = Economy.CellSize;
            var unitCells = new HashSet<Cell>();
            foreach (var unit in Snapshot.Units)
                unitCells.Add(new Cell((int)Math.Floor(unit.Position.X / cs), (int)Math.Floor(unit.Position.Y / cs)));

            var candidates = new List<(float Score, Cell Cell)>();
            for (int y = 0; y + def.Height <= Economy.GridHeight; y++)
            for (int x = 0; x + def.Width <= Economy.GridWidth; x++)
            {
                if (!OnClearedLand(x, y, def.Width, def.Height)) continue;
                if (Snapshot.Buildings.Any(b => ColonySimulation.FootprintsOverlap(new Cell(x - 1, y - 1),
                        def.Width + 2, def.Height + 2, b.Cell, b.Width, b.Height)))
                    continue;
                if (unitCells.Any(c => ColonySimulation.BuildingOccupiesCell(new Cell(x, y), def.Width, def.Height, c)))
                    continue;
                float dx = x + def.Width * 0.5f - core.X, dy = y + def.Height * 0.5f - core.Y;
                candidates.Add((dx * dx + dy * dy, new Cell(x, y)));
            }
            int choice = Math.Max(1, _profile.PlacementChoice);
            var valid = new List<Cell>(choice);
            foreach (var candidate in candidates.OrderBy(c => c.Score).ThenBy(c => c.Cell.Y).ThenBy(c => c.Cell.X))
            {
                if (!Session.CanPlaceBuilding(kind, candidate.Cell).Ok) continue;
                valid.Add(candidate.Cell);
                if (valid.Count == choice) break;
            }
            return valid.Count > 0 ? valid[Dice.Pick(valid.Count)] : null;
        }

        private bool OnClearedLand(int x, int y, int width, int height)
        {
            var land = Snapshot.Land;
            if (land == null) return true;
            for (int cy = y; cy < y + height; cy++)
            for (int cx = x; cx < x + width; cx++)
            {
                if (!land.BlockOf(new Cell(cx, cy), out int bx, out int by) || !land.Block(bx, by).Cleared)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Raises the cheapest colony upgrade whose next level is open. While none is, it builds the host of the
        /// cheapest one, or raises that host when its next level waits for a bigger building.
        /// </summary>
        public bool BuyUpgrade(BotWait wait, string why, BuildingKind? host = null)
        {
            var left = Snapshot.Upgrades.Where(u => !u.IsMaxed && (host == null || u.Host == host)).ToList();
            if (left.Count == 0)
            {
                wait?.Note("все улучшения куплены");
                return false;
            }
            var next = left.Where(u => u.IsOpen).OrderBy(u => u.NextCost).FirstOrDefault();
            if (next == null)
            {
                var first = left.OrderBy(u => u.NextCost).First();
                if (!first.HostBuilt)
                    Build(first.Host, wait, $"{Def(first.Host).DisplayName} для улучшений");
                else
                    Upgrade(HostOf(first.Host), wait, $"{why}: {Def(first.Host).DisplayName} открывает «{first.Name}»");
                return false;
            }
            if (!Affordable(next.NextCost, wait, $"{why}: {next.Name}")) return false;
            if (!Dispatch(new BuyUpgradeCommand(next.Id))) return false;
            Spent(next.NextCost);
            return true;
        }

        /// <summary>The colony's highest building of a kind: its level opens the upgrades it hosts.</summary>
        public BuildingSnapshot HostOf(BuildingKind kind) => BuildingsOf(kind).OrderByDescending(b => b.Level).FirstOrDefault();

        /// <summary>Buys one more block of land next to the colony's own, nearest the core.</summary>
        public bool BuyLandBlock(BotWait wait, string why)
        {
            var land = Snapshot.Land;
            if (land == null)
            {
                wait?.Note($"земля не покупается: {why}");
                return false;
            }
            var core = Core();
            var buyable = land.Blocks.Where(b => b.CanBuy)
                .OrderBy(b =>
                {
                    float dx = (b.X + 0.5f) * land.BlockSize - core.X, dy = (b.Y + 0.5f) * land.BlockSize - core.Y;
                    return dx * dx + dy * dy;
                }).ThenBy(b => b.Y).ThenBy(b => b.X).ToList();
            if (buyable.Count == 0)
            {
                wait?.Note($"остров застроен: {why}");
                return false;
            }
            int price = land.NextPrice;
            if (!Affordable(price, wait, why)) return false;
            if (!Dispatch(new BuyLandCommand(buyable[0].X, buyable[0].Y))) return false;
            Spent(price);
            _run.LandBought++;
            return true;
        }

        /// <summary>Clears owned wild land, or buys the block nearest the core next to the colony's land.</summary>
        public void ExpandLand(BotWait wait, string why)
        {
            var land = Snapshot.Land;
            if (land == null)
            {
                wait?.Note($"нет места: {why}");
                return;
            }
            if (land.Blocks.Any(b => b.Clearing))
            {
                wait?.NeedTime($"расчистка земли ({why})");
                return;
            }
            var core = Core();
            float Distance(LandBlockSnapshot b)
            {
                float dx = (b.X + 0.5f) * land.BlockSize - core.X, dy = (b.Y + 0.5f) * land.BlockSize - core.Y;
                return dx * dx + dy * dy;
            }
            var wild = land.Blocks.Where(b => b.Wild).OrderBy(Distance).ThenBy(b => b.Y).ThenBy(b => b.X).ToList();
            if (wild.Count == 0)
            {
                var buyable = land.Blocks.Where(b => b.CanBuy).OrderBy(Distance).ThenBy(b => b.Y).ThenBy(b => b.X)
                    .ToList();
                if (buyable.Count == 0)
                {
                    wait?.Note($"остров застроен: {why}");
                    return;
                }
                int price = land.NextPrice;
                if (!Affordable(price, wait, $"земля под {why}")) return;
                var block = buyable[0];
                if (!Dispatch(new BuyLandCommand(block.X, block.Y))) return;
                Spent(price);
                _run.LandBought++;
                wild = Snapshot.Land.Blocks.Where(b => b.Wild).OrderBy(Distance).ToList();
                if (wild.Count == 0) return;
            }
            if (!Affordable(land.ClearGold, wait, $"расчистка под {why}")) return;
            if (Dispatch(new ClearLandCommand(wild[0].X, wild[0].Y)))
            {
                Spent(land.ClearGold);
                wait?.NeedTime($"расчистка земли ({why})");
            }
        }
    }
}
