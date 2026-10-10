using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>
    /// The state in a save's body, field by field, and back. Writing is canonical (see
    /// <see cref="SaveCodec.WriteBody"/>); reading maps names to this build's content and refuses unknown ones. The
    /// colony-wide checks (references, layout, quest) are <see cref="SaveCheck"/>'s.
    /// </summary>
    internal static class SaveBody
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // ----- writing -----

        public static void WriteState(JsonWriter w, GameState s, GameContentCatalog catalog)
        {
            w.BeginObject();
            w.Name("gold").Value(s.Gold);
            w.Name("soldGoods").Value(s.SoldGoods);
            w.Name("salesGold").Value(s.SalesGold);
            w.Name("soldByResource"); WriteAmounts(w, s.SoldByResource);
            w.Name("producedByResource"); WriteAmounts(w, s.ProducedByResource);
            w.Name("battlesWon").Value(s.BattlesWon);
            w.Name("gearWornInBattles").Value(s.GearWornInBattles);
            w.Name("nextBuildingId").Value(s.NextBuildingId);
            w.Name("nextUnitId").Value(s.NextUnitId);
            w.Name("nextHaulQueueTicket").Value(s.NextHaulQueueTicket);
            w.Name("layoutVersion").Value(s.LayoutVersion);
            w.Name("activeTimeMs").Value(s.ActiveTimeMs);
            w.Name("missionWins"); WriteCounts(w, s.MissionWins);
            w.Name("missionReadyAtMs"); WriteCounts(w, s.MissionReadyAtMs);
            w.Name("highestMissionLevel").Value(s.HighestMissionLevel);
            w.Name("arenaFundPayouts").Value(s.ArenaFundPayouts);
            w.Name("arenaFundSinceMs").Value(s.ArenaFundSinceMs);
            w.Name("rewardRoll").Value(s.RewardRoll);
            w.Name("productionRoll").Value(s.ProductionRoll);
            w.Name("upgrades"); WriteCounts(w, s.Upgrades);
            w.Name("pendingBattleReward"); WriteReward(w, s.PendingBattleReward);
            w.Name("progress"); WriteProgress(w, s, catalog);
            w.Name("land"); WriteLand(w, s.Land);
            w.Name("buildings").BeginArray();
            foreach (var building in s.Buildings) WriteBuilding(w, building);
            w.EndArray();
            w.Name("units").BeginArray();
            foreach (var unit in s.Units) WriteUnit(w, unit);
            w.EndArray();
            w.Name("equipment").BeginArray();
            foreach (var item in s.Equipment)
                w.BeginObject().Name("id").Value(item.Id).Name("item").Value(item.DefinitionId)
                    .Name("owner").Value(item.OwnerUnitId).EndObject();
            w.EndArray();
            // ActiveBattle is not written: the battle's outcome is in the state from its start, the replay is not kept
            w.EndObject();
        }

        private static void WriteBuilding(JsonWriter w, BuildingState b)
        {
            w.BeginObject();
            w.Name("id").Value(b.Id);
            w.Name("kind").Value(Names<BuildingKind>.Of(b.Kind));
            w.Name("x").Value(b.Cell.X);
            w.Name("y").Value(b.Cell.Y);
            w.Name("level").Value(b.Level);
            w.Name("investedGold").Value(b.InvestedGold);
            w.Name("productionProgress").Value(b.ProductionProgress);
            w.Name("completedCycles").Value(b.CompletedCycles);
            w.Name("stock"); WriteAmounts(w, b.Stock);
            w.EndObject();
        }

        private static void WriteUnit(JsonWriter w, UnitState u)
        {
            w.BeginObject();
            w.Name("id").Value(u.Id);
            w.Name("kind").Value(Names<UnitKind>.Of(u.Kind));
            w.Name("name").Value(u.Name);
            w.Name("x").Value(u.Position.X);
            w.Name("y").Value(u.Position.Y);
            w.Name("assignment");
            if (u.Assignment == null) w.Null();
            else WriteAssignment(w, u.Assignment);
            // the walk under way: waypoints as x, y pairs, valid for its goal under the layout version it was planned in
            w.Name("route").BeginArray();
            if (u.Route != null)
                foreach (var point in u.Route) w.Value(point.X).Value(point.Y);
            w.EndArray();
            w.Name("hasRoute").Value(u.HasRoute);
            w.Name("routeGoal").BeginArray().Value(u.RouteGoal.X).Value(u.RouteGoal.Y).EndArray();
            w.Name("routeLayoutVersion").Value(u.RouteLayoutVersion);
            w.EndObject();
        }

        private static void WriteAssignment(JsonWriter w, Assignment a)
        {
            w.BeginObject();
            w.Name("kind").Value(Names<AssignmentKind>.Of(a.Kind));
            w.Name("building").Value(a.BuildingId);
            w.Name("source").Value(a.SourceId);
            w.Name("destination").Value(a.DestinationId);
            w.Name("phase").Value(Names<HaulPhase>.Of(a.Phase));
            w.Name("carried").Value(a.Carried);
            w.Name("carriedResource").Value(Names<ResourceKind>.Of(a.CarriedResource));
            w.Name("carryCreditPercent").Value(a.CarryCreditPercent);
            w.Name("phaseElapsedSeconds").Value(a.PhaseElapsedSeconds);
            w.Name("handlingCreditSeconds").Value(a.HandlingCreditSeconds);
            w.Name("queueTicket").Value(a.QueueTicket);
            w.Name("crowdSlot").Value(a.CrowdSlot);
            // the hauler's chosen goods in the order chosen: the order is the player's, an empty list means everything
            w.Name("cargo").BeginArray();
            if (a.Cargo != null)
                foreach (var resource in a.Cargo) w.Value(Names<ResourceKind>.Of(resource));
            w.EndArray();
            w.EndObject();
        }

        private static void WriteReward(JsonWriter w, PendingBattleReward r)
        {
            if (r == null)
            {
                w.Null();
                return;
            }
            w.BeginObject();
            w.Name("missionId").Value(r.MissionId);
            w.Name("gold").Value(r.Gold);
            w.Name("minGold").Value(r.MinGold);
            w.Name("maxGold").Value(r.MaxGold);
            w.Name("firstWin").Value(r.FirstWin);
            w.Name("draw").Value(r.Draw);
            w.Name("goods"); WriteAmounts(w, r.Goods ?? new Dictionary<ResourceKind, int>());
            w.EndObject();
        }

        private static void WriteProgress(JsonWriter w, GameState s, GameContentCatalog catalog)
        {
            var p = s.Progress;
            if (p == null)
            {
                w.Null();
                return;
            }
            w.BeginObject();
            w.Name("questIndex").Value(p.QuestIndex);
            // the quest by id as well: a chain that grew in front of it still finds it
            w.Name("questId").Value(Progression.CurrentQuest(s, catalog)?.Id);
            w.Name("questStartMs").Value(p.QuestStartMs);
            // a short build's own end, kept as the game was started: a save never takes another build's
            w.Name("chainLength").Value(p.ChainLength);
            w.Name("arenaCap").Value(p.ArenaCap);
            w.Name("goalDone").BeginArray();
            foreach (bool done in p.GoalDone) w.Value(done);
            w.EndArray();
            w.Name("goalBaseline").BeginArray();
            foreach (int baseline in p.GoalBaseline) w.Value(baseline);
            w.EndArray();
            w.Name("unlockedBuildings"); WriteNames(w, p.UnlockedBuildings);
            w.Name("unlockedUnits"); WriteNames(w, p.UnlockedUnits);
            w.Name("unlockedMissions"); WriteSorted(w, p.UnlockedMissions);
            w.EndObject();
        }

        private static void WriteLand(JsonWriter w, LandState land)
        {
            if (land == null)
            {
                w.Null();
                return;
            }
            w.BeginObject();
            w.Name("blocksPerSide").Value(land.BlocksPerSide);
            w.Name("blockSize").Value(land.BlockSize);
            w.Name("purchases").Value(land.Purchases);
            // one letter a block, row by row from block (0, 0): u unowned, w wild, c cleared
            var blocks = new StringBuilder(land.Count);
            for (int y = 0; y < land.BlocksPerSide; y++)
            for (int x = 0; x < land.BlocksPerSide; x++)
                blocks.Append(BlockLetter(land.Block(x, y)));
            w.Name("blocks").Value(blocks.ToString());
            // blocks being cleared: [index, milliseconds left, milliseconds the clearing took in all]
            w.Name("clearing").BeginArray();
            for (int y = 0; y < land.BlocksPerSide; y++)
            for (int x = 0; x < land.BlocksPerSide; x++)
            {
                int left = land.ClearLeftMs(x, y), total = land.ClearTotalMs(x, y);
                if (left == 0 && total == 0) continue;
                w.BeginArray().Value(y * land.BlocksPerSide + x).Value(left).Value(total).EndArray();
            }
            w.EndArray();
            w.EndObject();
        }

        private static char BlockLetter(LandBlock block) => block switch
        {
            LandBlock.Wild => 'w',
            LandBlock.Cleared => 'c',
            _ => 'u'
        };

        private static void WriteAmounts(JsonWriter w, Dictionary<ResourceKind, int> amounts)
        {
            var keys = new List<ResourceKind>(amounts.Keys);
            keys.Sort();
            w.BeginObject();
            foreach (var key in keys) w.Name(Names<ResourceKind>.Of(key)).Value(amounts[key]);
            w.EndObject();
        }

        private static void WriteCounts(JsonWriter w, Dictionary<string, int> counts)
        {
            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            w.BeginObject();
            foreach (var key in keys) w.Name(key).Value(counts[key]);
            w.EndObject();
        }

        private static void WriteNames<T>(JsonWriter w, IEnumerable<T> values) where T : struct, Enum
        {
            var sorted = new List<T>(values);
            sorted.Sort();
            w.BeginArray();
            foreach (var value in sorted) w.Value(Names<T>.Of(value));
            w.EndArray();
        }

        private static void WriteSorted(JsonWriter w, IEnumerable<string> values)
        {
            var sorted = new List<string>(values);
            sorted.Sort(StringComparer.Ordinal);
            w.BeginArray();
            foreach (var value in sorted) w.Value(value);
            w.EndArray();
        }

        // ----- reading -----

        public static GameState ReadState(JsonObject o, GameContentCatalog catalog)
        {
            const string at = "state";
            var s = new GameState
            {
                Gold = Int(o, "gold", at, 0),
                SoldGoods = Int(o, "soldGoods", at, 0),
                SalesGold = Int(o, "salesGold", at, 0),
                SoldByResource = Amounts(Obj(o, "soldByResource", at), "soldByResource", catalog),
                ProducedByResource = Amounts(Obj(o, "producedByResource", at), "producedByResource", catalog),
                BattlesWon = Int(o, "battlesWon", at, 0),
                GearWornInBattles = Int(o, "gearWornInBattles", at, 0),
                NextBuildingId = Int(o, "nextBuildingId", at, 1),
                NextUnitId = Int(o, "nextUnitId", at, 1),
                NextHaulQueueTicket = Int(o, "nextHaulQueueTicket", at, 1),
                LayoutVersion = Int(o, "layoutVersion", at, 0),
                ActiveTimeMs = Int(o, "activeTimeMs", at, 0),
                MissionWins = Counts(Obj(o, "missionWins", at), "missionWins"),
                MissionReadyAtMs = Counts(Obj(o, "missionReadyAtMs", at), "missionReadyAtMs"),
                HighestMissionLevel = Int(o, "highestMissionLevel", at, 0),
                ArenaFundPayouts = Int(o, "arenaFundPayouts", at, 0),
                ArenaFundSinceMs = Int(o, "arenaFundSinceMs", at, 0),
                RewardRoll = UInt(o, "rewardRoll", at),
                ProductionRoll = UInt(o, "productionRoll", at),
                Upgrades = Counts(Obj(o, "upgrades", at), "upgrades"),
                PendingBattleReward = ReadReward(Obj(o, "pendingBattleReward", at, true), catalog),
                Land = ReadLand(Obj(o, "land", at, true)),
                Buildings = new List<BuildingState>(),
                Units = new List<UnitState>(),
                Equipment = new List<EquipmentState>(),
                ActiveBattle = null
            };
            foreach (var pair in s.MissionWins) RequireMission(pair.Key, "missionWins", catalog);
            foreach (var pair in s.MissionReadyAtMs) RequireMission(pair.Key, "missionReadyAtMs", catalog);
            foreach (var pair in s.Upgrades)
            {
                var upgrade = catalog.TryGetUpgrade(pair.Key) ??
                              throw new SaveRejection(SaveError.UnknownContent, $"upgrades: no upgrade \"{pair.Key}\"");
                if (pair.Value > upgrade.MaxLevel)
                    throw new SaveRejection(SaveError.InvalidValue,
                        $"upgrades.{pair.Key}: level {pair.Value}, the top is {upgrade.MaxLevel}");
            }

            var buildings = Arr(o, "buildings", at);
            for (int i = 0; i < buildings.Count; i++)
                s.Buildings.Add(ReadBuilding(Item(buildings, i, "buildings"), $"buildings[{i}]", catalog));
            var units = Arr(o, "units", at);
            for (int i = 0; i < units.Count; i++)
                s.Units.Add(ReadUnit(Item(units, i, "units"), $"units[{i}]", catalog));
            var equipment = Arr(o, "equipment", at);
            for (int i = 0; i < equipment.Count; i++)
            {
                var item = Item(equipment, i, "equipment");
                string path = $"equipment[{i}]";
                string definition = Str(item, "item", path);
                bool known = false;
                foreach (var candidate in catalog.Equipment)
                    if (candidate != null && candidate.ItemId == definition) known = true;
                if (!known) throw new SaveRejection(SaveError.UnknownContent, $"{path}.item: no item \"{definition}\"");
                s.Equipment.Add(new EquipmentState
                {
                    Id = Str(item, "id", path),
                    DefinitionId = definition,
                    OwnerUnitId = Str(item, "owner", path, true)
                });
            }

            // the quest is placed last: its goals measure the colony read above
            s.Progress = ReadProgress(Obj(o, "progress", at, true), s, catalog);
            return s;
        }

        private static BuildingState ReadBuilding(JsonObject o, string path, GameContentCatalog catalog)
        {
            var kind = Kind<BuildingKind>(o, "kind", path);
            var definition = Building(catalog, kind) ??
                             throw new SaveRejection(SaveError.UnknownContent, $"{path}.kind: no building {kind}");
            int level = Int(o, "level", path, 1);
            if (level > definition.MaxLevel)
                throw new SaveRejection(SaveError.InvalidValue, $"{path}.level: {level}, the top is {definition.MaxLevel}");
            var building = new BuildingState
            {
                Id = Id(o, "id", path),
                Kind = kind,
                Cell = new Cell(Int(o, "x", path), Int(o, "y", path)),
                Level = level,
                InvestedGold = Int(o, "investedGold", path, 0),
                ProductionProgress = Finite(o, "productionProgress", path),
                CompletedCycles = Int(o, "completedCycles", path, 0),
                Stock = Amounts(Obj(o, "stock", path), path + ".stock", catalog)
            };
            if (building.ProductionProgress < 0f)
                throw new SaveRejection(SaveError.InvalidValue, $"{path}.productionProgress: {building.ProductionProgress}");
            return building;
        }

        private static UnitState ReadUnit(JsonObject o, string path, GameContentCatalog catalog)
        {
            var kind = Kind<UnitKind>(o, "kind", path);
            if (catalog.TryGetUnit(kind) == null)
                throw new SaveRejection(SaveError.UnknownContent, $"{path}.kind: no creature {kind}");
            var assignmentJson = Obj(o, "assignment", path, true) ??
                                 throw new SaveRejection(SaveError.InvalidValue, $"{path}.assignment: missing");
            var route = Arr(o, "route", path);
            if (route.Count % 2 != 0) throw new SaveFormatException($"{path}.route: an odd number of coordinates");
            var points = new List<WorldPosition>(route.Count / 2);
            for (int i = 0; i < route.Count; i += 2)
                points.Add(new WorldPosition(FiniteItem(route, i, path + ".route"), FiniteItem(route, i + 1, path + ".route")));
            var goal = Arr(o, "routeGoal", path);
            if (goal.Count != 2) throw new SaveFormatException($"{path}.routeGoal: not an x, y pair");
            return new UnitState
            {
                Id = Id(o, "id", path),
                Kind = kind,
                Name = Str(o, "name", path, true),
                Position = new WorldPosition(Finite(o, "x", path), Finite(o, "y", path)),
                Assignment = ReadAssignment(assignmentJson, path + ".assignment", catalog),
                Route = points,
                HasRoute = Bool(o, "hasRoute", path),
                RouteGoal = new WorldPosition(FiniteItem(goal, 0, path + ".routeGoal"), FiniteItem(goal, 1, path + ".routeGoal")),
                RouteLayoutVersion = Int(o, "routeLayoutVersion", path, 0)
            };
        }

        private static Assignment ReadAssignment(JsonObject o, string path, GameContentCatalog catalog)
        {
            var cargoJson = Arr(o, "cargo", path);
            var cargo = new List<ResourceKind>(cargoJson.Count);
            for (int i = 0; i < cargoJson.Count; i++)
                cargo.Add(Resource(ItemString(cargoJson, i, path + ".cargo"), $"{path}.cargo[{i}]", catalog));
            return new Assignment
            {
                Kind = Kind<AssignmentKind>(o, "kind", path),
                BuildingId = Str(o, "building", path, true),
                SourceId = Str(o, "source", path, true),
                DestinationId = Str(o, "destination", path, true),
                Phase = Kind<HaulPhase>(o, "phase", path),
                Carried = Int(o, "carried", path, 0),
                CarriedResource = Resource(Str(o, "carriedResource", path), path + ".carriedResource", catalog),
                CarryCreditPercent = Int(o, "carryCreditPercent", path, 0),
                PhaseElapsedSeconds = Finite(o, "phaseElapsedSeconds", path),
                HandlingCreditSeconds = Finite(o, "handlingCreditSeconds", path),
                QueueTicket = Int(o, "queueTicket", path, 0),
                CrowdSlot = Int(o, "crowdSlot", path, -1),
                Cargo = cargo
            };
        }

        private static PendingBattleReward ReadReward(JsonObject o, GameContentCatalog catalog)
        {
            if (o == null) return null;
            const string path = "pendingBattleReward";
            string mission = Str(o, "missionId", path);
            RequireMission(mission, path + ".missionId", catalog);
            return new PendingBattleReward
            {
                MissionId = mission,
                Gold = Int(o, "gold", path, 0),
                MinGold = Int(o, "minGold", path, 0),
                MaxGold = Int(o, "maxGold", path, 0),
                FirstWin = Bool(o, "firstWin", path),
                Draw = Bool(o, "draw", path),
                Goods = Amounts(Obj(o, "goods", path), path + ".goods", catalog)
            };
        }

        private static ProgressState ReadProgress(JsonObject o, GameState state, GameContentCatalog catalog)
        {
            if (o == null) return null;
            const string path = "progress";
            if (catalog.Progression == null)
                throw new SaveRejection(SaveError.QuestMismatch, "a campaign save, but this build has no quest chain");
            var progress = new ProgressState
            {
                QuestIndex = Int(o, "questIndex", path, 0),
                QuestStartMs = Int(o, "questStartMs", path, 0),
                ChainLength = Int(o, "chainLength", path, 0),
                ArenaCap = Int(o, "arenaCap", path, 0)
            };
            if (progress.ChainLength > catalog.Progression.Quests.Count)
                throw new SaveRejection(SaveError.QuestMismatch,
                    $"{path}.chainLength: {progress.ChainLength}, this build's chain has {catalog.Progression.Quests.Count}");
            var done = Arr(o, "goalDone", path);
            for (int i = 0; i < done.Count; i++)
                progress.GoalDone.Add(done[i] is JsonBool b
                    ? b.Value
                    : throw new SaveFormatException($"{path}.goalDone[{i}]: not true or false"));
            var baseline = Arr(o, "goalBaseline", path);
            for (int i = 0; i < baseline.Count; i++)
                progress.GoalBaseline.Add(IntItem(baseline, i, path + ".goalBaseline"));
            var buildings = Arr(o, "unlockedBuildings", path);
            for (int i = 0; i < buildings.Count; i++)
            {
                string at = $"{path}.unlockedBuildings[{i}]";
                var kind = Names<BuildingKind>.Parse(ItemString(buildings, i, at), at);
                if (Building(catalog, kind) == null)
                    throw new SaveRejection(SaveError.UnknownContent, $"{at}: no building {kind}");
                progress.UnlockedBuildings.Add(kind);
            }
            var units = Arr(o, "unlockedUnits", path);
            for (int i = 0; i < units.Count; i++)
            {
                string at = $"{path}.unlockedUnits[{i}]";
                var kind = Names<UnitKind>.Parse(ItemString(units, i, at), at);
                if (catalog.TryGetUnit(kind) == null)
                    throw new SaveRejection(SaveError.UnknownContent, $"{at}: no creature {kind}");
                progress.UnlockedUnits.Add(kind);
            }
            var missions = Arr(o, "unlockedMissions", path);
            for (int i = 0; i < missions.Count; i++)
            {
                string at = $"{path}.unlockedMissions[{i}]";
                string mission = ItemString(missions, i, at);
                RequireMission(mission, at, catalog);
                progress.UnlockedMissions.Add(mission);
            }
            state.Progress = progress;
            SaveCheck.PlaceQuest(state, Str(o, "questId", path, true), catalog);
            return progress;
        }

        private static LandState ReadLand(JsonObject o)
        {
            if (o == null) return null;
            const string path = "land";
            int side = Int(o, "blocksPerSide", path, 1, 4096);
            int size = Int(o, "blockSize", path, 1, 4096);
            var land = new LandState(side, size) { Purchases = Int(o, "purchases", path, 0) };
            string blocks = Str(o, "blocks", path);
            if (blocks.Length != side * side)
                throw new SaveRejection(SaveError.InvalidValue,
                    $"{path}.blocks: {blocks.Length} letters for {side * side} blocks");
            for (int i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i] switch
                {
                    'u' => LandBlock.Unowned,
                    'w' => LandBlock.Wild,
                    'c' => LandBlock.Cleared,
                    _ => throw new SaveRejection(SaveError.InvalidValue, $"{path}.blocks[{i}]: '{blocks[i]}'")
                };
                land.Restore(i % side, i / side, block, 0, 0);
            }
            var clearing = Arr(o, "clearing", path);
            for (int i = 0; i < clearing.Count; i++)
            {
                string at = $"{path}.clearing[{i}]";
                if (!(clearing[i] is JsonArray entry) || entry.Count != 3)
                    throw new SaveFormatException($"{at}: not [index, left, total]");
                int index = IntItem(entry, 0, at), left = IntItem(entry, 1, at), total = IntItem(entry, 2, at);
                if (index < 0 || index >= side * side)
                    throw new SaveRejection(SaveError.InvalidValue, $"{at}: block {index} is off the land");
                int x = index % side, y = index / side;
                // a clearing runs on a bought wild block, has time left and never more than it took in all
                if (land.Block(x, y) != LandBlock.Wild || left <= 0 || total < left || land.ClearTotalMs(x, y) != 0)
                    throw new SaveRejection(SaveError.InvalidValue, $"{at}: no clearing can stand like this");
                land.Restore(x, y, LandBlock.Wild, left, total);
            }
            return land;
        }

        private static Dictionary<ResourceKind, int> Amounts(JsonObject o, string path, GameContentCatalog catalog)
        {
            var amounts = new Dictionary<ResourceKind, int>();
            foreach (var member in o.Members)
                amounts.Add(Resource(member.Key, $"{path}.{member.Key}", catalog),
                    AsInt(member.Value, $"{path}.{member.Key}", 0));
            return amounts;
        }

        private static Dictionary<string, int> Counts(JsonObject o, string path)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var member in o.Members) counts.Add(member.Key, AsInt(member.Value, $"{path}.{member.Key}", 0));
            return counts;
        }

        private static ResourceKind Resource(string name, string path, GameContentCatalog catalog)
        {
            var resource = Names<ResourceKind>.Parse(name, path);
            if (catalog.TryGetResource(resource) == null)
                throw new SaveRejection(SaveError.UnknownContent, $"{path}: no good {name}");
            return resource;
        }

        private static void RequireMission(string missionId, string path, GameContentCatalog catalog)
        {
            if (catalog.TryGetMission(missionId) == null)
                throw new SaveRejection(SaveError.UnknownContent, $"{path}: no mission \"{missionId}\"");
        }

        internal static BuildingDefinition Building(GameContentCatalog catalog, BuildingKind kind)
        {
            foreach (var definition in catalog.Buildings)
                if (definition != null && definition.Kind == kind) return definition;
            return null;
        }

        // ----- typed access, the place in the file in every fault -----

        public static JsonNode Get(JsonObject o, string name, string path) =>
            o[name] ?? throw new SaveFormatException($"{path}.{name}: missing");

        public static JsonObject Obj(JsonObject o, string name, string path, bool nullable = false)
        {
            var node = Get(o, name, path);
            if (node is JsonObject value) return value;
            if (nullable && node is JsonNull) return null;
            throw new SaveFormatException($"{path}.{name}: not an object");
        }

        public static JsonArray Arr(JsonObject o, string name, string path) =>
            Get(o, name, path) as JsonArray ?? throw new SaveFormatException($"{path}.{name}: not a list");

        public static string Str(JsonObject o, string name, string path, bool nullable = false)
        {
            var node = Get(o, name, path);
            if (node is JsonString value) return value.Value;
            if (nullable && node is JsonNull) return null;
            throw new SaveFormatException($"{path}.{name}: not a string");
        }

        public static bool Bool(JsonObject o, string name, string path) =>
            Get(o, name, path) is JsonBool value
                ? value.Value
                : throw new SaveFormatException($"{path}.{name}: not true or false");

        public static int Int(JsonObject o, string name, string path, int min = int.MinValue, int max = int.MaxValue) =>
            AsInt(Get(o, name, path), $"{path}.{name}", min, max);

        public static uint UInt(JsonObject o, string name, string path)
        {
            long value = AsLong(Get(o, name, path), $"{path}.{name}");
            if (value < 0 || value > uint.MaxValue)
                throw new SaveRejection(SaveError.InvalidValue, $"{path}.{name}: {value} is not a 32-bit unsigned number");
            return (uint)value;
        }

        public static float Float(JsonObject o, string name, string path) => AsFloat(Get(o, name, path), $"{path}.{name}");

        private static float Finite(JsonObject o, string name, string path)
        {
            float value = Float(o, name, path);
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new SaveRejection(SaveError.InvalidValue, $"{path}.{name}: {value}");
            return value;
        }

        private static float FiniteItem(JsonArray a, int index, string path)
        {
            float value = AsFloat(a[index], $"{path}[{index}]");
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new SaveRejection(SaveError.InvalidValue, $"{path}[{index}]: {value}");
            return value;
        }

        private static JsonObject Item(JsonArray a, int index, string path) =>
            a[index] as JsonObject ?? throw new SaveFormatException($"{path}[{index}]: not an object");

        private static string ItemString(JsonArray a, int index, string path) =>
            a[index] is JsonString value ? value.Value : throw new SaveFormatException($"{path}[{index}]: not a string");

        private static int IntItem(JsonArray a, int index, string path) => AsInt(a[index], $"{path}[{index}]");

        private static string Id(JsonObject o, string name, string path)
        {
            string id = Str(o, name, path);
            if (string.IsNullOrEmpty(id)) throw new SaveRejection(SaveError.InvalidValue, $"{path}.{name}: empty");
            return id;
        }

        private static T Kind<T>(JsonObject o, string name, string path) where T : struct, Enum =>
            Names<T>.Parse(Str(o, name, path), $"{path}.{name}");

        private static int AsInt(JsonNode node, string path, int min = int.MinValue, int max = int.MaxValue)
        {
            long value = AsLong(node, path);
            if (value < min || value > max)
                throw new SaveRejection(SaveError.InvalidValue, $"{path}: {value} is out of {min}..{max}");
            return (int)value;
        }

        private static long AsLong(JsonNode node, string path)
        {
            if (!(node is JsonNumber number) ||
                !long.TryParse(number.Text, NumberStyles.AllowLeadingSign, Invariant, out long value))
                throw new SaveFormatException($"{path}: not a whole number");
            return value;
        }

        private static float AsFloat(JsonNode node, string path)
        {
            switch (node)
            {
                case JsonNumber number when float.TryParse(number.Text, NumberStyles.Float, Invariant, out float value):
                    return value;
                case JsonString text when text.Value == "NaN":
                    return float.NaN;
                case JsonString text when text.Value == "Infinity":
                    return float.PositiveInfinity;
                case JsonString text when text.Value == "-Infinity":
                    return float.NegativeInfinity;
                default:
                    throw new SaveFormatException($"{path}: not a number");
            }
        }

        /// <summary>An enum's names both ways; a name the enum does not have is unknown content, numbers are refused.</summary>
        internal static class Names<T> where T : struct, Enum
        {
            private static readonly Dictionary<string, T> ByName = Build();

            public static string Of(T value) => Enum.GetName(typeof(T), value) ?? value.ToString();

            public static T Parse(string name, string path)
            {
                if (name != null && ByName.TryGetValue(name, out var value)) return value;
                throw new SaveRejection(SaveError.UnknownContent, $"{path}: no {typeof(T).Name} \"{name}\"");
            }

            private static Dictionary<string, T> Build()
            {
                var map = new Dictionary<string, T>(StringComparer.Ordinal);
                foreach (T value in Enum.GetValues(typeof(T))) map[Enum.GetName(typeof(T), value)] = value;
                return map;
            }
        }
    }
}
