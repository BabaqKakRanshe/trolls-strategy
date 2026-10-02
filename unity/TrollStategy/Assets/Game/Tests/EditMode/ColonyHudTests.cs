using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>The colony HUD built from its real layout and catalog, driven the way the player drives it.</summary>
    public class ColonyHudTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private InteractionController _interaction;
        private ColonyHudView _hud;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _session = new GameSession(_catalog, TestColony.LayoutFor(_catalog,
                new StartingBuilding(BuildingKind.Warehouse, new Cell(10, 8)),
                new StartingBuilding(BuildingKind.Market, new Cell(10, 2)),
                new StartingBuilding(BuildingKind.Barracks, new Cell(2, 8))));
            _interaction = new InteractionController(_session);
            _hud = TestUi.Colony(_session, _interaction);
        }

        [Test]
        public void EveryButtonInTheLayout_IsBoundToAnAction()
        {
            var buttons = _hud.Root.Query<Button>().ToList();
            Assert.That(buttons, Is.Not.Empty);
            foreach (var button in buttons)
                Assert.That(button.userData, Is.Not.Null, $"'{button.name}' ({button.text}) does nothing");
        }

        [Test]
        public void TopBar_ShowsTheSnapshotCounters()
        {
            Assert.That(Text("gold-value"), Is.EqualTo(_session.CurrentSnapshot.Gold.ToString()));

            BuyUnit(UnitKind.Goblin);
            Refresh();

            Assert.That(Text("population-value"), Is.EqualTo("1"));
            Assert.That(Text("gold-value"), Is.EqualTo(_session.CurrentSnapshot.Gold.ToString()));
        }

        [Test]
        public void Catalog_OffersEveryConstructibleBuildingAtItsCurrentPrice()
        {
            var constructible = _catalog.Buildings.Where(b => b != null && b.Constructible).ToList();
            Assert.That(_hud.Catalog.BuildingCount, Is.EqualTo(constructible.Count));
            foreach (var building in constructible)
            {
                var buy = _hud.Catalog.BuyButton(building.Kind);
                Assert.That(buy, Is.Not.Null, building.DisplayName);
                Assert.That(_hud.Catalog.PriceOf(building.Kind), Is.EqualTo(_session.BuildingPrice(building.Kind).ToString()),
                    building.DisplayName);
                if (_session.CurrentSnapshot.Buildings.All(standing => standing.Kind != building.Kind))
                    Assert.That(_session.BuildingPrice(building.Kind), Is.EqualTo(building.Price),
                        $"{building.DisplayName}: the first one costs its catalog price");
            }

            UiFeel.Press(_hud.Catalog.BuyButton(BuildingKind.Mine));

            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingBuilding));
            Assert.That(_interaction.Mode.BuildingKind, Is.EqualTo(BuildingKind.Mine));
        }

        [Test]
        public void Catalog_PricesTheNextCopy_OnceOneStands()
        {
            var mine = _catalog.GetBuilding(BuildingKind.Mine);
            var cell = _session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(cell.HasValue, Is.True);
            Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, cell.Value)).Ok, Is.True);
            Refresh();

            int next = _session.BuildingPrice(BuildingKind.Mine);
            Assert.That(_hud.Catalog.PriceOf(BuildingKind.Mine), Is.EqualTo(next.ToString()));
            if (_catalog.Economy.BuildingCopyPriceGrowth > 1f)
                Assert.That(next, Is.GreaterThan(mine.Price), "The second mine costs more than the first");
        }

        [Test]
        public void Catalog_HireButtonPricesTheWholeGroup()
        {
            var goblin = _catalog.GetUnit(UnitKind.Goblin);
            _hud.Catalog.SetHireAmount(3);
            var hire = _hud.Catalog.HireButton(UnitKind.Goblin);
            Assert.That(_hud.Catalog.PriceOf(UnitKind.Goblin), Is.EqualTo(_session.HirePrice(UnitKind.Goblin, 3).ToString()));
            Assert.That(_session.HirePrice(UnitKind.Goblin, 3), Is.GreaterThanOrEqualTo(goblin.Price * 3),
                "Each creature hired makes the next one no cheaper");

            UiFeel.Press(hire);

            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingUnits));
            Assert.That(_interaction.Mode.UnitKind, Is.EqualTo(UnitKind.Goblin));
            Assert.That(_interaction.Mode.Amount, Is.EqualTo(3));
        }

        [Test]
        public void Catalog_CreatureHint_SaysWhatItIsFor_NotItsRawStats()
        {
            var goblin = _catalog.GetUnit(UnitKind.Goblin);
            string hint = _hud.Catalog.Hint(UnitKind.Goblin);

            Assert.That(hint, Does.StartWith(goblin.Description.TrimEnd('.')));
            Assert.That(hint, Does.Not.Contain("Сила").And.Not.Contain("Выносливость"));
            Assert.That(hint, Does.Not.Contain("золота"), "The price stands under the token");

            _hud.Catalog.SetHireAmount(3);

            Assert.That(_hud.Catalog.Hint(UnitKind.Goblin), Does.Contain($"{_session.HirePrice(UnitKind.Goblin)} золота за одного"),
                "While a group is hired the token shows its total, the hint the price of one");
        }

        [Test]
        public void Catalog_BuildingHint_NamesOnlyTheMainRecipe()
        {
            string hint = _hud.Catalog.Hint(BuildingKind.Smeltery);
            var smeltery = _catalog.GetBuilding(BuildingKind.Smeltery);
            Assume.That(smeltery.Recipes.Count, Is.GreaterThan(1), "The smeltery has a coal and a scrap recipe too");

            Assert.That(hint, Does.StartWith(_session.DescribeMainRecipe(smeltery)), "What goes in and what comes out");
            Assert.That(GameSession.MainRecipe(smeltery).Inputs.Length, Is.EqualTo(1), "The plain ore recipe, not the coal one");
            Assert.That(hint, Does.Contain($"{smeltery.Width}×{smeltery.Height}"));
            Assert.That(hint, Does.Not.Contain("%"), "Chances and spoilage are in the building's card");
            Assert.That(hint, Does.Not.Contain("уровня"), "So are the recipes of later levels");
        }

        [Test]
        public void UnaffordableHire_IsRefusedWithoutStartingPlacement()
        {
            var troll = _catalog.GetUnit(UnitKind.Troll);
            Assume.That(_session.HirePrice(troll.Kind, CatalogPanel.MaxHireAmount), Is.GreaterThan(_session.CurrentSnapshot.Gold));
            _hud.Catalog.SetHireAmount(CatalogPanel.MaxHireAmount);
            var hire = _hud.Catalog.HireButton(UnitKind.Troll);
            Assert.That(UiFeel.IsAvailable(hire), Is.False);

            UiFeel.Press(hire);

            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
        }

        [Test]
        public void Inspect_ShowsBuildingManagementAndHidesWhileMoving()
        {
            _interaction.SelectBuilding("warehouse-1");
            Refresh();

            Assert.That(_hud.Inspect.IsShown, Is.True);
            Assert.That(_hud.Inspect.Title, Is.EqualTo(Building("warehouse-1").Name));
            Assert.That(_hud.Inspect.Actions.Select(Title), Is.EqualTo(new[] { "Перенести", "Улучшить", "Снести" }));

            _interaction.BeginMoveInspectedBuilding();
            Refresh();

            Assert.That(_hud.Inspect.IsShown, Is.False, "The card must not cover the map while moving");
        }

        [Test]
        public void Inspect_BarracksHiresAtCatalogPrices()
        {
            _interaction.SelectBuilding("barracks-1");
            Refresh();

            var hints = _hud.Inspect.Actions.Select(Hint).ToList();
            // the card has room for two hires beside "move": the first creatures that may join the colony
            foreach (var unit in _catalog.Units.Where(u => u != null && u.Hireable && _session.IsUnitUnlocked(u.Kind)).Take(2))
                Assert.That(hints.Contains(Ui.Gold(_session.HirePrice(unit.Kind))), Is.True,
                    $"{unit.DisplayName}: {string.Join(", ", hints)}");

            var goblin = _catalog.GetUnit(UnitKind.Goblin);
            var hireGoblin = _hud.Inspect.Actions.First(b => Title(b).Contains(goblin.DisplayName.ToLowerInvariant()));
            UiFeel.Press(hireGoblin);

            Assert.That(_session.CurrentSnapshot.Units.Count, Is.EqualTo(1));
            Assert.That(_session.CurrentSnapshot.Units[0].UnitKind, Is.EqualTo(UnitKind.Goblin));
        }

        [Test]
        public void Inspect_UnitSaleOffersAndPaysTheSessionRefund()
        {
            var id = BuyUnit(UnitKind.Troll);
            _interaction.InspectSquad(id, null);
            Refresh();
            int refund = ColonySimulation.UnitSaleRefund(_catalog.GetUnit(UnitKind.Troll));
            var sell = _hud.Inspect.Actions.Last();
            Assert.That(Hint(sell), Is.EqualTo("+" + Ui.Gold(refund)));
            int gold = _session.CurrentSnapshot.Gold;

            UiFeel.Press(sell);

            Assert.That(_session.CurrentSnapshot.Gold, Is.EqualTo(gold + refund));
            Assert.That(_session.CurrentSnapshot.Units, Is.Empty);
        }

        [Test]
        public void Inspect_UnitCard_SaysWhatTheCreatureIsFor_InPlainWords()
        {
            var troll = _catalog.GetUnit(UnitKind.Troll);
            var id = BuyUnit(UnitKind.Troll);
            _interaction.InspectSquad(id, null);
            Refresh();

            Assert.That(Text("inspect-note"), Does.StartWith(troll.Description.Trim()));
            var rows = _hud.Root.Query<Label>(className: "kv__key").ToList().Where(key => Ui.IsShown(key.parent))
                .ToDictionary(key => key.text, key => key.parent.Q<Label>(className: "kv__value").text);
            foreach (var stat in new[] { "Сила", "Скорость", "Выносливость" })
                Assert.That(rows.Keys, Has.No.Member(stat), "Raw stats say nothing to the player");
            // a troll's 150 % stamina: one good a trip, and every second trip one more
            Assert.That(rows["Носит за ходку"], Is.EqualTo("1–2"));
        }

        [Test]
        public void ContextBar_CommandsTheSelectionAndListsTargets()
        {
            Assert.That(_hud.ContextBar.IsShown, Is.False, "Nothing selected, nothing to command");
            var mineCell = _session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(mineCell.HasValue, Is.True);
            Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, mineCell.Value)).Ok, Is.True);
            var id = BuyUnit(UnitKind.Goblin);
            _interaction.ClickUnit(id, false);
            Refresh();
            Assert.That(_hud.ContextBar.IsShown, Is.True);

            UiFeel.Press(_hud.ContextBar.WorkButton);
            Refresh();

            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.ChoosingWorkTarget));
            Assert.That(_hud.ContextBar.TargetButtons.Count, Is.EqualTo(1), "The mine is the only workplace");
            UiFeel.Press(_hud.ContextBar.TargetButtons[0]);
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
            var assignment = _session.CurrentSnapshot.Units[0].Assignment.Kind;
            Assert.That(assignment == AssignmentKind.ToWork || assignment == AssignmentKind.Work, Is.True,
                assignment.ToString());
        }

        [Test]
        public void Orders_TakeTheTraysPlace_AndTheCatalogToolBringsItBack()
        {
            var id = BuyUnit(UnitKind.Goblin);
            Refresh();
            Assert.That(_hud.Catalog.IsCovered, Is.False);

            _interaction.ClickUnit(id, false);
            Refresh();
            Assert.That(_hud.ContextBar.IsShown, Is.True);
            Assert.That(_hud.Catalog.IsCovered, Is.True, "The orders hold the bottom while a creature is selected");
            Assert.That(_hud.ContextBar.Title, Is.EqualTo(_catalog.GetUnit(UnitKind.Goblin).DisplayName));

            UiFeel.Press(_hud.TopBar.CatalogButton);
            Refresh();
            Assert.That(_interaction.SelectedIds, Is.Empty, "The catalog tool drops the selection");
            Assert.That(_hud.ContextBar.IsShown, Is.False);
            Assert.That(_hud.Catalog.IsCovered, Is.False);
            Assert.That(_hud.Catalog.IsOpen, Is.True);
        }

        [Test]
        public void StatusLine_ShowsTheControllersLatestMessage()
        {
            Assert.That(_hud.Status.Text, Is.EqualTo(_interaction.Message));

            _interaction.CancelOrClear();
            Refresh();

            Assert.That(_hud.Status.Text, Is.EqualTo(_interaction.Message));
        }

        private void Refresh() => _hud.OnInteractionChanged(_session.CurrentSnapshot);

        private string Text(string name) => _hud.Root.Q<Label>(name).text;

        private static string Title(Button button) => button.Q<Label>(className: "btn__title").text;

        private static string Hint(Button button) => button.Q<Label>(className: "btn__hint").text;

        private BuildingSnapshot Building(string id) => _session.CurrentSnapshot.Buildings.First(b => b.Id == id);

        private string BuyUnit(UnitKind kind)
        {
            var result = _session.Dispatch(new BuyUnitsCommand(kind, 1, _session.FindSpawnCell()));
            Assert.That(result.Ok, Is.True, result.Error);
            return _session.CurrentSnapshot.Units.Last().Id;
        }
    }
}
