using System.Collections.Generic;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Island;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// Buying and clearing land (LandRules) and where land limits the colony. The test grid is 20x20 cells in 4x4
    /// blocks of 5; the start land is blocks (1..2, 1..2), cells 5..14, cleared.
    /// </summary>
    public class LandTests
    {
        private const float Step = .25f;
        private readonly List<ScriptableObject> _assets = new();
        private GameContentCatalog _catalog;
        private EconomyConfig _economy;

        [SetUp]
        public void SetUp()
        {
            _economy = Create<EconomyConfig>();
            _economy.Init(20, 20, 1f, 1000, 20, Step, 0.1f, 0.5f);
            _economy.SetLand(true, 5, new RectInt(1, 1, 2, 2), 200, 100, 10f, 0);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
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
            _catalog.Init(_economy, new[] { goblin }, new[] { mine, warehouse, market },
                resources: new[] { new ResourceDefinition(ResourceKind.IronOre, "Руда", 3) });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        // ------------------------------------------------------------------------------------------ buying

        [Test]
        public void Start_ClearsTheStartBlocksAndOwnsNothingElse()
        {
            var land = NewState().Land;
            Assert.That(land.BlocksPerSide, Is.EqualTo(4));
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                bool start = x >= 1 && x <= 2 && y >= 1 && y <= 2;
                Assert.That(land.Block(x, y), Is.EqualTo(start ? LandBlock.Cleared : LandBlock.Unowned), $"({x}, {y})");
            }
        }

        [Test]
        public void Buy_OnlyNextToOwnLand()
        {
            var state = NewState();
            var diagonal = Apply(state, new BuyLandCommand(0, 0));
            Assert.That(diagonal.Ok, Is.False);
            Assert.That(diagonal.Error, Is.EqualTo("Покупать можно только рядом со своей землёй"));
            Assert.That(state.Land.Block(0, 0), Is.EqualTo(LandBlock.Unowned));

            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            // a wild neighbour counts as own land too
            Assert.That(Apply(state, new BuyLandCommand(3, 0)).Ok, Is.True);
        }

        [Test]
        public void Buy_TakesThePriceAndThePriceGrows()
        {
            var state = NewState();
            Assert.That(LandRules.NextPrice(state.Land, _economy), Is.EqualTo(200));
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(state.Gold, Is.EqualTo(800));
            Assert.That(state.Land.Block(3, 1), Is.EqualTo(LandBlock.Wild), "A bought block comes wild");
            Assert.That(LandRules.NextPrice(state.Land, _economy), Is.EqualTo(300));
            Assert.That(Apply(state, new BuyLandCommand(0, 1)).Ok, Is.True);
            Assert.That(state.Gold, Is.EqualTo(500));
            Assert.That(state.Land.Purchases, Is.EqualTo(2));
        }

        [Test]
        public void Buy_RefusedWithoutTheGold()
        {
            var state = NewState();
            state.Gold = 150;
            var result = Apply(state, new BuyLandCommand(3, 1));
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo("Не хватает золота: нужно 200"));
            Assert.That(state.Gold, Is.EqualTo(150));
        }

        [Test]
        public void Buy_SameBlockTwice_Refused()
        {
            var state = NewState();
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            int gold = state.Gold;
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.False);
            Assert.That(Apply(state, new BuyLandCommand(1, 1)).Ok, Is.False, "The start land is owned already");
            Assert.That(state.Gold, Is.EqualTo(gold));
        }

        [TestCase(4, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, 4)]
        [TestCase(1, -1)]
        public void Buy_OutsideTheGrid_Refused(int x, int y)
        {
            var state = NewState();
            var result = Apply(state, new BuyLandCommand(x, y));
            Assert.That(result.Ok, Is.False);
            Assert.That(state.Gold, Is.EqualTo(1000));
        }

        // ------------------------------------------------------------------------------------------ clearing

        [Test]
        public void Clear_OnlyTheColonysOwnWildBlock()
        {
            var state = NewState();
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Error, Is.EqualTo("Сначала купите эту землю"));
            Assert.That(Apply(state, new ClearLandCommand(1, 1)).Ok, Is.False, "Start land is cleared already");
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.True);
            Assert.That(state.Land.IsClearing(3, 1), Is.True);
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.False, "Already being cleared");
        }

        [Test]
        public void Clear_TakesItsGold()
        {
            _economy.SetLand(true, 5, new RectInt(1, 1, 2, 2), 200, 100, 10f, 50);
            var state = NewState();
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.True);
            Assert.That(state.Gold, Is.EqualTo(1000 - 200 - 50));
        }

        [Test]
        public void Clear_WhenTheTimerEnds_TheBlockIsClearedAndRoutesReplan()
        {
            var state = NewState();
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.True);
            int layout = state.LayoutVersion;

            // 10 s of colony time in 0.25 s steps: still wild one step before the end
            for (int i = 0; i < 39; i++) ColonySimulation.TickColony(state, Step, _catalog);
            Assert.That(state.Land.Block(3, 1), Is.EqualTo(LandBlock.Wild));
            Assert.That(state.Land.ClearProgress(3, 1), Is.EqualTo(39f / 40f).Within(1e-4f));
            Assert.That(state.LayoutVersion, Is.EqualTo(layout));

            ColonySimulation.TickColony(state, Step, _catalog);
            Assert.That(state.Land.Block(3, 1), Is.EqualTo(LandBlock.Cleared));
            Assert.That(state.Land.IsClearing(3, 1), Is.False);
            Assert.That(state.LayoutVersion, Is.GreaterThan(layout));
        }

        [Test]
        public void Clear_WithoutClearingTime_IsDoneAtOnce()
        {
            _economy.SetLand(true, 5, new RectInt(1, 1, 2, 2), 200, 100, 0f, 0);
            var state = NewState();
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            int layout = state.LayoutVersion;
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.True);
            Assert.That(state.Land.Block(3, 1), Is.EqualTo(LandBlock.Cleared));
            Assert.That(state.LayoutVersion, Is.GreaterThan(layout));
        }

        // ------------------------------------------------------------------------------------------ building

        [Test]
        public void Build_RefusedOnUnboughtAndWildLand()
        {
            var state = NewState();
            var unbought = ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Mine, new Cell(16, 6), _catalog);
            Assert.That(unbought.Ok, Is.False);
            Assert.That(unbought.Error, Is.EqualTo("Сначала купите и расчистите эту землю"));

            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            var wild = ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Mine, new Cell(16, 6), _catalog);
            Assert.That(wild.Ok, Is.False);
            Assert.That(wild.Error, Is.EqualTo("Сначала купите и расчистите эту землю"));
            Assert.That(Apply(state, new BuildBuildingCommand(BuildingKind.Mine, new Cell(16, 6))).Ok, Is.False);
        }

        [Test]
        public void Build_AllowedOnClearedLand()
        {
            var state = NewState();
            Assert.That(Apply(state, new BuildBuildingCommand(BuildingKind.Mine, new Cell(6, 6))).Ok, Is.True);

            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.True);
            for (int i = 0; i < 40; i++) ColonySimulation.TickColony(state, Step, _catalog);
            Assert.That(Apply(state, new BuildBuildingCommand(BuildingKind.Mine, new Cell(16, 6))).Ok, Is.True);
        }

        [Test]
        public void Build_FootprintOverTwoClearedBlocks_Allowed_ButNotHalfOnWild()
        {
            var state = NewState();
            // cells 8..10 cross the border of blocks 1 and 2 (cell 10 starts block 2)
            Assert.That(ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Mine, new Cell(8, 7), _catalog).Ok,
                Is.True);
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            // cells 13..15 reach into the wild block 3
            Assert.That(ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Mine, new Cell(13, 7), _catalog).Ok,
                Is.False);
        }

        [Test]
        public void Hire_RefusedOnUnboughtLand()
        {
            var state = NewState();
            Assert.That(ColonySimulation.ValidateUnitPurchase(state, UnitKind.Goblin, 1, new Cell(2, 2), _catalog).Error,
                Is.EqualTo("Сначала купите и расчистите эту землю"));
            Assert.That(ColonySimulation.ValidateUnitPurchase(state, UnitKind.Goblin, 1, new Cell(7, 7), _catalog).Ok,
                Is.True);
        }

        // ------------------------------------------------------------------------------------------ walking

        [Test]
        public void Walking_OnlyOnClearedLand()
        {
            var state = NewState();
            Assert.That(ColonyNavigation.IsWalkable(state, new Cell(7, 7), _catalog), Is.True);
            Assert.That(ColonyNavigation.IsWalkable(state, new Cell(16, 7), _catalog), Is.False, "Not bought");
            Assert.That(Apply(state, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(ColonyNavigation.IsWalkable(state, new Cell(16, 7), _catalog), Is.False, "Wild");
            Assert.That(Apply(state, new ClearLandCommand(3, 1)).Ok, Is.True);
            for (int i = 0; i < 40; i++) ColonySimulation.TickColony(state, Step, _catalog);
            Assert.That(ColonyNavigation.IsWalkable(state, new Cell(16, 7), _catalog), Is.True, "Cleared");
        }

        [Test]
        public void Walking_NoRouteAcrossWildLand()
        {
            var state = NewState();
            Assert.That(ColonyNavigation.FindRoute(state, new WorldPosition(7.5f, 7.5f), new WorldPosition(17.5f, 7.5f),
                null, _catalog), Is.Null);
            Assert.That(ColonyNavigation.FindRoute(state, new WorldPosition(7.5f, 7.5f), new WorldPosition(12.5f, 12.5f),
                null, _catalog), Is.Not.Null);
        }

        // ------------------------------------------------------------------------------------------ state

        [Test]
        public void Clone_DoesNotShareTheLand()
        {
            var state = NewState();
            var clone = state.Clone();
            Assert.That(Apply(clone, new BuyLandCommand(3, 1)).Ok, Is.True);
            Assert.That(Apply(clone, new ClearLandCommand(3, 1)).Ok, Is.True);
            Assert.That(state.Land.Block(3, 1), Is.EqualTo(LandBlock.Unowned));
            Assert.That(state.Land.Purchases, Is.EqualTo(0));
            Assert.That(state.Land.IsClearing(3, 1), Is.False);
            Assert.That(clone.Land.IsClearing(3, 1), Is.True);
        }

        [Test]
        public void WithoutLand_TheWholeGridIsOpen()
        {
            _economy.SetLand(false, 5, new RectInt(1, 1, 2, 2), 200, 100, 10f, 0);
            var session = new GameSession(_catalog, new[] { new StartingBuilding(BuildingKind.Market, new Cell(0, 1)) });
            Assert.That(session.CurrentSnapshot.Land, Is.Null);
            Assert.That(session.CanPlaceBuilding(BuildingKind.Mine, new Cell(16, 16)).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyLandCommand(3, 1)).Ok, Is.False);
        }

        // ------------------------------------------------------------------------------------------ session

        [Test]
        public void Session_StartsOnItsLandAndShowsWhatIsForSale()
        {
            var session = NewSession();
            var land = session.CurrentSnapshot.Land;
            Assert.That(land, Is.Not.Null);
            Assert.That(land.NextPrice, Is.EqualTo(200));
            Assert.That(land.ClearSeconds, Is.EqualTo(10f));
            Assert.That(land.Block(3, 1).CanBuy, Is.True);
            Assert.That(land.Block(0, 0).CanBuy, Is.False, "Diagonal to the start land");
            Assert.That(land.Block(1, 1).Cleared, Is.True);

            var spawn = session.FindSpawnCell();
            Assert.That(session.CanBuyUnits(UnitKind.Goblin, 1, spawn).Ok, Is.True, $"Spawn cell {spawn}");
        }

        [Test]
        public void Session_BuysAndClearsThroughCommands()
        {
            var session = NewSession();
            Assert.That(session.Dispatch(new BuyLandCommand(3, 1)).Ok, Is.True);
            var block = session.CurrentSnapshot.Land.Block(3, 1);
            Assert.That(block.Wild, Is.True);
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(800));
            Assert.That(session.CurrentSnapshot.Land.NextPrice, Is.EqualTo(300));

            Assert.That(session.Dispatch(new ClearLandCommand(3, 1)).Ok, Is.True);
            session.Advance(5f);
            block = session.CurrentSnapshot.Land.Block(3, 1);
            Assert.That(block.Clearing, Is.True);
            Assert.That(block.ClearProgress, Is.EqualTo(.5f).Within(1e-4f));
            session.Advance(5f);
            Assert.That(session.CurrentSnapshot.Land.Block(3, 1).Cleared, Is.True);
            Assert.That(session.CanPlaceBuilding(BuildingKind.Mine, new Cell(16, 6)).Ok, Is.True);
        }

        [Test]
        public void Session_RejectsStartingBuildingsOffTheStartLand()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                new GameSession(_catalog, new[] { new StartingBuilding(BuildingKind.Market, new Cell(0, 1)) }));
        }

        [Test]
        public void LandMode_PicksABlockThenBuysIt()
        {
            var session = NewSession();
            var interaction = new InteractionController(session);
            interaction.ToggleLandMode();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.ManagingLand));

            interaction.ChooseLandBlock(0, 0);
            Assert.That(interaction.Mode.LandOffer, Is.EqualTo(LandOffer.None), "Not next to own land");
            Assert.That(interaction.Message, Is.EqualTo("Покупать можно только рядом со своей землёй"));

            interaction.ChooseLandBlock(3, 1);
            Assert.That(interaction.Mode.LandOffer, Is.EqualTo(LandOffer.Buy));
            Assert.That(session.CurrentSnapshot.Land.Block(3, 1).Owned, Is.False, "Nothing is bought before the confirm");
            Assert.That(interaction.ConfirmLand().Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Land.Block(3, 1).Wild, Is.True);

            interaction.ChooseLandBlock(3, 1);
            Assert.That(interaction.Mode.LandOffer, Is.EqualTo(LandOffer.Clear));
            Assert.That(interaction.ConfirmLand().Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Land.Block(3, 1).Clearing, Is.True);

            interaction.ChooseLandBlock(0, 1);
            Assert.That(interaction.Mode.LandOffer, Is.EqualTo(LandOffer.Buy));
            interaction.CancelOrClear();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.ManagingLand), "Esc drops the pick first");
            Assert.That(interaction.Mode.LandOffer, Is.EqualTo(LandOffer.None));
            interaction.CancelOrClear();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral), "then leaves the land mode");
            interaction.ToggleLandMode();
            interaction.ToggleLandMode();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral), "L again leaves it too");
        }

        // ------------------------------------------------------------------------------------------ shipped content

        [Test]
        public void ShippedColony_StartsOn15x15CellsAndTheIslandShowsJustThat()
        {
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/Game/Content/Definitions/EconomyConfig.asset");
            var island = AssetDatabase.LoadAssetAtPath<IslandView>("Assets/Game/Prefabs/Environments/Colony_Isle.prefab");
            var start = economy.StartLand;
            Assert.That(economy.LandEnabled, Is.True);
            Assert.That(new Vector2Int(start.width, start.height) * economy.LandBlockSize, Is.EqualTo(new Vector2Int(15, 15)));
            Assert.That(island.BlocksPerSide, Is.EqualTo(LandRules.CreateStart(economy).BlocksPerSide));
            Assert.That(island.StartBlocks, Is.EqualTo(start), "the edit-time island is the land Play Mode starts on");
            for (int y = 0; y < island.BlocksPerSide; y++)
            for (int x = 0; x < island.BlocksPerSide; x++)
            {
                bool owned = start.Contains(new Vector2Int(x, y));
                Assert.That(island.State(x, y), Is.EqualTo(owned ? LandBlockState.Cleared : LandBlockState.Hidden), $"({x}, {y})");
                Assert.That(island.Block(x, y).Land.gameObject.activeSelf, Is.EqualTo(owned), $"land of ({x}, {y})");
            }
        }

        // ------------------------------------------------------------------------------------------ helpers

        private GameState NewState()
        {
            var state = GameState.CreateInitialState(_economy.StartingGold);
            state.Land = LandRules.CreateStart(_economy);
            return state;
        }

        private GameSession NewSession() => new(_catalog, new[]
        {
            new StartingBuilding(BuildingKind.Warehouse, new Cell(6, 10)),
            new StartingBuilding(BuildingKind.Market, new Cell(10, 6))
        });

        private CommandResult Apply(GameState state, IGameCommand command) =>
            ColonySimulation.ApplyCommand(state, command, _catalog);

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
