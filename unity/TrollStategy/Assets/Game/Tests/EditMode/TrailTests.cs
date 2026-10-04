using System.Collections.Generic;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// Trails (TrailRules, GDD 5.6): creatures wear the cells they step into, worn cells are quicker to walk and
    /// routes take them when that is quicker, quiet cells grow over, buildings erase what lies under them. The test
    /// grid is 20x20 cells without land; the warehouse and market of TestColony stand at x 10..12.
    /// </summary>
    public class TrailTests
    {
        private const float Step = .25f;
        private const string EconomyPath = "Assets/Game/Content/Definitions/EconomyConfig.asset";
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private readonly List<ScriptableObject> _assets = new();
        private GameContentCatalog _catalog;
        private EconomyConfig _economy;

        [SetUp]
        public void SetUp()
        {
            _economy = Create<EconomyConfig>();
            _economy.Init(20, 20, 1f, 1000, 20, Step, 0.1f, 0.5f);
            _economy.SetTrails(true);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 5f, 150);
            troll.SetTrailWear(2);
            var mine = Create<BuildingDefinition>();
            mine.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, null);
            mine.SetConstructible(true);
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            warehouse.SetStorage(StorageRole.Stockpile, ResourceKind.IronOre);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            market.SetStorage(StorageRole.Market);
            _catalog = Create<GameContentCatalog>();
            _catalog.Init(_economy, new[] { goblin, troll }, new[] { mine, warehouse, market },
                resources: new[] { new ResourceDefinition(ResourceKind.IronOre, "Руда", 3) });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        // ------------------------------------------------------------------------------------------ wear

        [Test]
        public void Walk_WearsEveryCellSteppedIntoButNotTheOneLeft()
        {
            var state = NewState();
            TrailRules.Walk(state, new WorldPosition(.5f, .5f), new WorldPosition(3.5f, .5f), 1, _economy);

            Assert.That(state.Trails.Wear(0, 0), Is.EqualTo(0), "the cell it left");
            for (int x = 1; x <= 3; x++) Assert.That(state.Trails.Wear(x, 0), Is.EqualTo(1), $"({x}, 0)");
            Assert.That(state.Trails.Wear(4, 0), Is.EqualTo(0), "past the end");
        }

        [Test]
        public void Walk_ThroughCornersSteppingIntoOneCellAtATime()
        {
            var state = NewState();
            // the diagonal runs through cell corners: every step enters one neighbour, never skips a cell
            TrailRules.Walk(state, new WorldPosition(.5f, .5f), new WorldPosition(2.5f, 2.5f), 1, _economy);

            int worn = 0;
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                worn += state.Trails.Wear(x, y);
            Assert.That(worn, Is.EqualTo(4), "two steps right and two up");
            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(1), "it ends in the goal's cell");
        }

        [Test]
        public void Wear_StopsAtTheMost()
        {
            var state = NewState();
            state.Trails.Set(1, 0, TrailState.Max - 1);
            for (int i = 0; i < 3; i++)
                TrailRules.Walk(state, new WorldPosition(.5f, .5f), new WorldPosition(1.5f, .5f), 1, _economy);

            Assert.That(state.Trails.Wear(1, 0), Is.EqualTo(TrailState.Max));
        }

        [Test]
        public void WalkingCreatures_WearTheirWay_TrollsTwiceAsHard()
        {
            var goblinState = NewState();
            var goblin = Walker(goblinState, UnitKind.Goblin);
            WalkUntilAtWork(goblinState, goblin);
            var trollState = NewState();
            var troll = Walker(trollState, UnitKind.Troll);
            WalkUntilAtWork(trollState, troll);

            // straight up from (5.5, 0.5) to the mine's door
            for (int y = 1; y <= 6; y++)
            {
                Assert.That(goblinState.Trails.Wear(5, y), Is.EqualTo(1), $"goblin at (5, {y})");
                Assert.That(trollState.Trails.Wear(5, y), Is.EqualTo(2), $"troll at (5, {y})");
            }
            Assert.That(goblinState.Trails.Wear(5, 0), Is.EqualTo(0), "it set out from there");
        }

        // ------------------------------------------------------------------------------------------ growing over

        [Test]
        public void QuietCells_HoldThroughTheGraceAndThenGrowOver()
        {
            var state = NewState();
            state.Trails.Set(2, 2, 50);

            Run(state, 60f);
            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(50), "a minute of quiet takes nothing");
            Run(state, 3f);
            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(49), "then a point every 3 s");
            Run(state, 6f);
            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(47));

            // a step resets the quiet: another full minute before it loses anything
            TrailRules.Walk(state, new WorldPosition(1.5f, 2.5f), new WorldPosition(2.5f, 2.5f), 1, _economy);
            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(48));
            Run(state, 60f);
            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(48));
        }

        [Test]
        public void QuietCells_GrowBackToLawn()
        {
            var state = NewState();
            state.Trails.Set(2, 2, TrailState.Max);

            Run(state, 60f + 3f * TrailState.Max + 30f);

            Assert.That(state.Trails.Wear(2, 2), Is.EqualTo(0));
            Assert.That(state.Trails.QuietMs(2, 2), Is.EqualTo(0), "a lawn cell keeps no clock");
        }

        [Test]
        public void Buildings_EraseTheTrailUnderThem()
        {
            var state = NewState();
            for (int x = 2; x <= 7; x++) state.Trails.Set(x, 3, 60);

            Place(state, BuildingKind.Mine, new Cell(4, 2));
            Run(state, Step);

            for (int x = 4; x <= 6; x++) Assert.That(state.Trails.Wear(x, 3), Is.EqualTo(0), $"under the mine ({x}, 3)");
            Assert.That(state.Trails.Wear(3, 3), Is.EqualTo(60), "beside it");
            Assert.That(state.Trails.Wear(7, 3), Is.EqualTo(60), "beside it");
        }

        // ------------------------------------------------------------------------------------------ walking

        [Test]
        public void Stages_FollowTheWear()
        {
            Assert.That(TrailRules.Stage(9, _economy), Is.EqualTo(TrailStage.Grass));
            Assert.That(TrailRules.Stage(10, _economy), Is.EqualTo(TrailStage.Trampled));
            Assert.That(TrailRules.Stage(40, _economy), Is.EqualTo(TrailStage.Path));
            Assert.That(TrailRules.Stage(80, _economy), Is.EqualTo(TrailStage.Road));
            Assert.That(TrailRules.SpeedFactor(39, _economy), Is.EqualTo(1f), "trodden grass is still lawn underfoot");
            Assert.That(TrailRules.SpeedFactor(40, _economy), Is.EqualTo(1.15f));
            Assert.That(TrailRules.SpeedFactor(80, _economy), Is.EqualTo(1.3f));
        }

        [Test]
        public void Roads_SpeedCreaturesUp()
        {
            var lawn = NewState();
            var onLawn = Walker(lawn, UnitKind.Goblin);
            var road = NewState();
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
                road.Trails.Set(x, y, TrailState.Max);
            var onRoad = Walker(road, UnitKind.Goblin);
            var start = onLawn.Position;

            ColonySimulation.TickColony(lawn, Step, _catalog);
            ColonySimulation.TickColony(road, Step, _catalog);

            float lawnStep = Distance(start, onLawn.Position), roadStep = Distance(start, onRoad.Position);
            Assert.That(lawnStep, Is.GreaterThan(0f));
            Assert.That(roadStep / lawnStep, Is.EqualTo(1.3f).Within(1e-3f));
        }

        [Test]
        public void Routes_TakeTheRoadWhenItIsQuicker()
        {
            var state = NewState();
            // a road one column east of the straight way: half a step aside at each end, then a quick run
            for (int y = 1; y <= 9; y++) state.Trails.Set(2, y, TrailState.Max);

            var route = ColonyNavigation.FindRoute(state, new WorldPosition(1.5f, 1.5f), new WorldPosition(1.5f, 9.5f),
                null, _catalog);

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Exists(p => p.X > 2.2f && p.Y > 3f && p.Y < 8f), Is.True,
                $"the route runs along the road: {string.Join(" ", route)}");
        }

        [Test]
        public void Routes_DoNotDetourForASlowerRoad()
        {
            var state = NewState();
            // four columns east: the detour is longer than the road saves
            for (int y = 1; y <= 9; y++) state.Trails.Set(5, y, TrailState.Max);

            var route = ColonyNavigation.FindRoute(state, new WorldPosition(1.5f, 1.5f), new WorldPosition(1.5f, 9.5f),
                null, _catalog);

            Assert.That(route, Is.Not.Null);
            Assert.That(route.TrueForAll(p => p.X < 2f), Is.True, $"the route goes straight: {string.Join(" ", route)}");
        }

        [Test]
        public void Routes_GoStraightOverTheLawn()
        {
            var state = NewState();
            var route = ColonyNavigation.FindRoute(state, new WorldPosition(1.5f, 1.5f), new WorldPosition(6.5f, 9.5f),
                null, _catalog);

            Assert.That(route, Is.EqualTo(new List<WorldPosition> { new(6.5f, 9.5f) }), "one straight leg");
        }

        // ------------------------------------------------------------------------------------------ state and session

        [Test]
        public void NoTrails_WhenTheEconomyHasNone()
        {
            _economy.SetTrails(false);
            Assert.That(TrailRules.CreateStart(_economy), Is.Null);
            Assert.That(TestColony.NewSession(_catalog).CurrentSnapshot.Trails, Is.Null);

            var state = TestColony.NewState(_catalog);
            Assert.That(TrailRules.SpeedAt(state, new WorldPosition(1.5f, 1.5f), _economy), Is.EqualTo(1f));
            TrailRules.Walk(state, new WorldPosition(.5f, .5f), new WorldPosition(3.5f, .5f), 1, _economy);
        }

        [Test]
        public void Session_ShowsTheWearAndWhereStagesStart()
        {
            var trails = TestColony.NewSession(_catalog).CurrentSnapshot.Trails;

            Assert.That(trails, Is.Not.Null);
            Assert.That(trails.Width, Is.EqualTo(20));
            Assert.That(trails.Height, Is.EqualTo(20));
            Assert.That(trails.MaxWear, Is.EqualTo(TrailState.Max));
            Assert.That((trails.TrampledAt, trails.PathAt, trails.RoadAt), Is.EqualTo((10, 40, 80)));
            Assert.That(trails.Wear(3, 3), Is.EqualTo(0), "a new colony is all lawn");
        }

        [Test]
        public void Clone_CopiesTheTrailsApart()
        {
            var state = NewState();
            state.Trails.Set(3, 3, 42);
            var clone = state.Clone();
            state.Trails.Set(3, 3, 7);

            Assert.That(clone.Trails.Wear(3, 3), Is.EqualTo(42));
        }

        // ------------------------------------------------------------------------------------------ content and view

        [Test]
        public void Game_HasTrails_AndTrollsTreadHarder()
        {
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath);
            Assert.That(economy, Is.Not.Null, EconomyPath);
            Assert.That(economy.TrailsEnabled, Is.True);
            Assert.That(economy.TrailPathAt, Is.LessThan(economy.TrailRoadAt));
            Assert.That(economy.TrailRoadSpeed, Is.GreaterThan(economy.TrailPathSpeed));

            var goblin = AssetDatabase.LoadAssetAtPath<UnitDefinition>(DefinitionFolder + "Unit_Goblin.asset");
            var troll = AssetDatabase.LoadAssetAtPath<UnitDefinition>(DefinitionFolder + "Unit_Troll.asset");
            Assert.That(goblin.TrailWear, Is.EqualTo(1));
            Assert.That(troll.TrailWear, Is.EqualTo(2));
        }

        [Test]
        public void TrailShader_LoadsAndCompiles()
        {
            var shader = Resources.Load<Shader>("Shaders/Trails");
            Assert.That(shader, Is.Not.Null, "Resources/Shaders/Trails");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        // ------------------------------------------------------------------------------------------ helpers

        private GameState NewState()
        {
            var state = TestColony.NewState(_catalog);
            state.Trails = TrailRules.CreateStart(_economy);
            return state;
        }

        // A creature south of a mine at (4, 7), whose door opens onto (5.5, 6.5), sent to work there.
        private UnitState Walker(GameState state, UnitKind kind)
        {
            var mine = Place(state, BuildingKind.Mine, new Cell(4, 7));
            var unit = new UnitState
            {
                Id = $"unit-{state.Units.Count + 1}",
                Kind = kind,
                Position = new WorldPosition(5.5f, .5f),
                Assignment = Assignment.ToWork(mine.Id)
            };
            state.Units.Add(unit);
            return unit;
        }

        private void WalkUntilAtWork(GameState state, UnitState unit)
        {
            for (int tick = 0; tick < 400 && unit.Assignment.Kind != AssignmentKind.Work; tick++)
                ColonySimulation.TickColony(state, Step, _catalog);
            Assert.That(unit.Assignment.Kind, Is.EqualTo(AssignmentKind.Work), "it reaches the mine");
        }

        private void Run(GameState state, float seconds)
        {
            for (int tick = 0; tick < Mathf.RoundToInt(seconds / Step); tick++)
                TrailRules.Tick(state, Step, _catalog);
        }

        private BuildingState Place(GameState state, BuildingKind kind, Cell cell)
        {
            Assert.That(ColonySimulation.PlaceStartingBuilding(state, kind, cell, _catalog, out var id).Ok, Is.True,
                $"{kind} at ({cell.X}, {cell.Y})");
            return state.Buildings.Find(b => b.Id == id);
        }

        private static float Distance(WorldPosition a, WorldPosition b) =>
            Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
