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
        public void Catalog_OffersEveryConstructibleBuildingAtItsCatalogPrice()
        {
            var constructible = _catalog.Buildings.Where(b => b != null && b.Constructible).ToList();
            Assert.That(_hud.Catalog.BuildingCount, Is.EqualTo(constructible.Count));
            foreach (var building in constructible)
            {
                var buy = _hud.Catalog.BuyButton(building.Kind);
                Assert.That(buy, Is.Not.Null, building.DisplayName);
                Assert.That(Hint(buy), Is.EqualTo(Ui.Gold(building.Price)), building.DisplayName);
            }

            UiFeel.Press(_hud.Catalog.BuyButton(BuildingKind.Mine));

            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingBuilding));
            Assert.That(_interaction.Mode.BuildingKind, Is.EqualTo(BuildingKind.Mine));
        }

        [Test]
        public void Catalog_HireButtonPricesTheWholeGroup()
        {
            var goblin = _catalog.GetUnit(UnitKind.Goblin);
            _hud.Catalog.SetHireAmount(3);
            var hire = _hud.Catalog.HireButton(UnitKind.Goblin);
            Assert.That(Hint(hire), Is.EqualTo(Ui.Gold(goblin.Price * 3)));

            UiFeel.Press(hire);

            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingUnits));
            Assert.That(_interaction.Mode.UnitKind, Is.EqualTo(UnitKind.Goblin));
            Assert.That(_interaction.Mode.Amount, Is.EqualTo(3));
        }

        [Test]
        public void UnaffordableHire_IsRefusedWithoutStartingPlacement()
        {
            var troll = _catalog.GetUnit(UnitKind.Troll);
            Assume.That(troll.Price * CatalogPanel.MaxHireAmount, Is.GreaterThan(_session.CurrentSnapshot.Gold));
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
            foreach (var unit in _catalog.Units)
                Assert.That(hints.Contains(Ui.Gold(unit.Price)), Is.True, $"{unit.DisplayName}: {string.Join(", ", hints)}");

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
