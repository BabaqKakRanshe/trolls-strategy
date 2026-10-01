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
    /// <summary>Haul cargo choice, the staff of a building and the battle prize, driven through the colony HUD.</summary>
    public class ColonyHudOrdersTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private InteractionController _interaction;
        private ColonyHudView _hud;
        private string _mine;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _session = TestColony.NewSession(_catalog);
            _interaction = new InteractionController(_session);
            _hud = TestUi.Colony(_session, _interaction);
            var cell = _session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, cell.Value)).Ok, Is.True);
            _mine = _session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
        }

        [Test]
        public void HaulOrder_AsksWhatToCarryInTheMiddle_ThenTheMapPicksWhere()
        {
            string goblin = Buy(UnitKind.Goblin);
            _interaction.ClickUnit(goblin, false);
            _interaction.BeginHaulTarget();
            _interaction.ChooseBuilding("warehouse-1");
            Refresh();

            var dialog = _hud.HaulCargo;
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(_hud.ContextBar.IsShown, Is.False, "The dialog asks alone");
            Assert.That(dialog.Title, Does.StartWith("Что носить из «"));
            var stored = _catalog.GetBuilding(BuildingKind.Warehouse).StoredResources;
            Assert.That(dialog.CargoKinds, Is.EquivalentTo(stored));
            Assert.That(dialog.CarryAllButton.ClassListContains("is-on"), Is.True);
            string hint = dialog.Hint(ResourceKind.IronOre);
            Assert.That(hint, Does.Contain("На рынке"));
            Assert.That(hint, Does.Contain("Делают: Шахта"));

            _interaction.ChooseBuilding("market-1");
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.ChoosingHaulCargo),
                "The map waits until the cargo is settled");

            int ingot = dialog.CargoKinds.ToList().IndexOf(ResourceKind.IronIngot);
            UiFeel.Press(dialog.CargoButtons[ingot]);
            Refresh();
            Assert.That(_interaction.HaulCargo, Is.EqualTo(new[] { ResourceKind.IronIngot }));
            Assert.That(dialog.CargoButtons[ingot].ClassListContains("is-on"), Is.True);
            Assert.That(dialog.CarryAllButton.ClassListContains("is-on"), Is.False);

            UiFeel.Press(dialog.ConfirmButton);
            Refresh();
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(_hud.ContextBar.IsShown, Is.True);
            Assert.That(_hud.ContextBar.Title, Is.EqualTo("Куда носить"));
            Assert.That(_hud.ContextBar.TargetButtons, Is.Empty, "Where to carry is the player's pick on the map");
            Assert.That(_interaction.GetTargetBuildingIds(), Does.Contain("market-1"));

            _interaction.ChooseBuilding("market-1");
            var assignment = Unit(goblin).Assignment;
            Assert.That(assignment.Kind, Is.EqualTo(AssignmentKind.Haul));
            Assert.That(assignment.Cargo, Is.EqualTo(new[] { ResourceKind.IronIngot }));
        }

        [Test]
        public void EveryHaulOrder_AsksAgain_WithTheGoodsOfItsSource()
        {
            string goblin = Buy(UnitKind.Goblin);
            _interaction.ClickUnit(goblin, false);
            _interaction.BeginHaulTarget();
            _interaction.ChooseBuilding(_mine);
            Refresh();
            Assert.That(_hud.HaulCargo.CargoKinds,
                Is.EquivalentTo(ColonySimulation.ProvidedResources(BuildingKind.Mine, _catalog)));
            UiFeel.Press(_hud.HaulCargo.ConfirmButton);
            _interaction.ChooseBuilding("warehouse-1");
            Assert.That(Unit(goblin).Assignment.Kind, Is.EqualTo(AssignmentKind.Haul));
            Assert.That(Unit(goblin).Assignment.Cargo, Is.Empty, "«Всё» by default");

            _interaction.ClickUnit(goblin, false);
            _interaction.BeginHaulTarget();
            _interaction.ChooseBuilding("warehouse-1");
            Refresh();
            Assert.That(_hud.HaulCargo.IsOpen, Is.True);
            Assert.That(_interaction.HaulCargo, Is.Empty, "A new order starts from «Всё»");
            Assert.That(_hud.HaulCargo.CargoKinds,
                Is.EquivalentTo(_catalog.GetBuilding(BuildingKind.Warehouse).StoredResources));

            UiFeel.Press(_hud.HaulCargo.CancelButton);
            Refresh();
            Assert.That(_hud.HaulCargo.IsOpen, Is.False);
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
        }

        [Test]
        public void DestinationStep_GoesBackToTheCargo_KeepingTheChoice()
        {
            string goblin = Buy(UnitKind.Goblin);
            _interaction.ClickUnit(goblin, false);
            _interaction.BeginHaulTarget();
            _interaction.ChooseBuilding("warehouse-1");
            _interaction.ToggleHaulCargo(ResourceKind.IronOre);
            _interaction.ConfirmHaulCargo();
            Refresh();
            Assert.That(Ui.IsShown(_hud.ContextBar.ChangeCargoButton), Is.True);

            UiFeel.Press(_hud.ContextBar.ChangeCargoButton);
            Refresh();
            Assert.That(_hud.HaulCargo.IsOpen, Is.True);
            Assert.That(_interaction.HaulCargo, Is.EqualTo(new[] { ResourceKind.IronOre }));
            int ore = _hud.HaulCargo.CargoKinds.ToList().IndexOf(ResourceKind.IronOre);
            Assert.That(_hud.HaulCargo.CargoButtons[ore].ClassListContains("is-on"), Is.True);
        }

        [Test]
        public void BuildingCard_ListsItsStaff_AndTakesThemOffOneByOneOrAllAtOnce()
        {
            var workers = new[] { Buy(UnitKind.Goblin), Buy(UnitKind.Goblin), Buy(UnitKind.Troll) };
            string hauler = Buy(UnitKind.Goblin);
            Assert.That(_session.Dispatch(new AssignWorkCommand(workers, _mine)).Ok, Is.True);
            Assert.That(_session.Dispatch(new AssignHaulCommand(new[] { hauler }, _mine, "warehouse-1")).Ok, Is.True);
            _interaction.SelectBuilding(_mine);
            Refresh();

            var staff = _hud.Inspect.Staff;
            Assert.That(staff.IsShown, Is.True);
            Assert.That(staff.WorkerIds, Is.EquivalentTo(workers));
            Assert.That(staff.WorkersCaption, Is.EqualTo("Рабочие: 3 из 5"));
            Assert.That(staff.HaulerIds, Is.EqualTo(new[] { hauler }));
            Assert.That(staff.AreWorkersOpen, Is.False, "Workers start folded too");

            UiFeel.Press(staff.WorkersToggle);
            Refresh();
            Assert.That(staff.AreWorkersOpen, Is.True);
            UiFeel.Press(staff.WorkerButtons[0]);
            Refresh();
            Assert.That(staff.WorkerIds.Count, Is.EqualTo(2));
            Assert.That(Unit(workers[0]).Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));

            UiFeel.Press(staff.ReleaseAllWorkers);
            Refresh();
            Assert.That(staff.WorkerIds, Is.Empty);
            Assert.That(workers.All(id => Unit(id).Assignment.Kind == AssignmentKind.Idle), Is.True);
            Assert.That(Unit(hauler).Assignment.Kind, Is.EqualTo(AssignmentKind.Haul), "Haulers keep their route");
        }

        [Test]
        public void BuildingCard_GroupsHaulersByCargo_AndTakesOffOneGroupAtOnce()
        {
            var any = new[] { Buy(UnitKind.Goblin), Buy(UnitKind.Goblin) };
            string crystal = Buy(UnitKind.Goblin);
            var ore = new[] { Buy(UnitKind.Goblin), Buy(UnitKind.Goblin) };
            Assert.That(_session.Dispatch(new AssignHaulCommand(any, _mine, "market-1")).Ok, Is.True);
            Assert.That(_session.Dispatch(new AssignHaulCommand(new[] { crystal }, _mine, "market-1",
                new[] { ResourceKind.VioletCrystal })).Ok, Is.True);
            Assert.That(_session.Dispatch(new AssignHaulCommand(ore, _mine, "market-1",
                new[] { ResourceKind.IronOre })).Ok, Is.True);
            _interaction.SelectBuilding(_mine);
            Refresh();

            var staff = _hud.Inspect.Staff;
            string oreName = _session.ResourceName(ResourceKind.IronOre);
            string crystalName = _session.ResourceName(ResourceKind.VioletCrystal);
            Assert.That(staff.HaulerGroupCount, Is.EqualTo(3), "One group per cargo");
            Assert.That(staff.HaulerCaption(0), Is.EqualTo($"{oreName}: 2"));
            Assert.That(staff.HaulerGroupIds(0), Is.EqualTo(ore));
            Assert.That(staff.HaulerCaption(1), Is.EqualTo($"{crystalName}: 1"));
            Assert.That(staff.HaulerGroupIds(1), Is.EqualTo(new[] { crystal }));
            Assert.That(staff.HaulerCaption(2), Is.EqualTo("Любой груз: 2"), "Any cargo comes last");
            Assert.That(staff.HaulerGroupIds(2), Is.EqualTo(any));

            UiFeel.Press(staff.ReleaseAllHaulers(0));
            Refresh();
            Assert.That(ore.All(id => Unit(id).Assignment.Kind == AssignmentKind.Idle), Is.True);
            Assert.That(Unit(crystal).Assignment.Kind, Is.EqualTo(AssignmentKind.Haul), "Other cargo keeps its haulers");
            Assert.That(staff.HaulerGroupCount, Is.EqualTo(2));
            Assert.That(staff.HaulerCaption(0), Is.EqualTo($"{crystalName}: 1"));
            Assert.That(staff.HaulerIds, Is.EqualTo(any.Prepend(crystal)));
        }

        [Test]
        public void HaulerGroups_StartFolded_AndStayOpenUntilTheCardCloses()
        {
            var ore = new[] { Buy(UnitKind.Goblin), Buy(UnitKind.Goblin) };
            string crystal = Buy(UnitKind.Goblin);
            Assert.That(_session.Dispatch(new AssignHaulCommand(ore, _mine, "market-1",
                new[] { ResourceKind.IronOre })).Ok, Is.True);
            Assert.That(_session.Dispatch(new AssignHaulCommand(new[] { crystal }, _mine, "market-1",
                new[] { ResourceKind.VioletCrystal })).Ok, Is.True);
            _interaction.SelectBuilding(_mine);
            Refresh();

            var staff = _hud.Inspect.Staff;
            Assert.That(staff.HaulerGroupCount, Is.EqualTo(2));
            Assert.That(staff.IsHaulerGroupOpen(0) || staff.IsHaulerGroupOpen(1), Is.False, "Groups start folded");
            Assert.That(Ui.IsShown(staff.ReleaseAllHaulers(0)), Is.True, "A folded group can still be taken off");

            UiFeel.Press(staff.HaulerGroupToggle(1));
            Assert.That(staff.IsHaulerGroupOpen(1), Is.True, "Opens on the click, before the next snapshot");
            Refresh();
            Assert.That(staff.IsHaulerGroupOpen(1), Is.True);
            Assert.That(staff.IsHaulerGroupOpen(0), Is.False);

            UiFeel.Press(staff.ReleaseAllHaulers(0));
            Refresh();
            Assert.That(staff.HaulerGroupCount, Is.EqualTo(1));
            Assert.That(staff.HaulerGroupIds(0), Is.EqualTo(new[] { crystal }));
            Assert.That(staff.IsHaulerGroupOpen(0), Is.True, "The open group stays open as the one above goes");

            UiFeel.Press(staff.HaulerGroupToggle(0));
            Assert.That(staff.IsHaulerGroupOpen(0), Is.False, "Folds again");
            UiFeel.Press(staff.HaulerGroupToggle(0));
            _interaction.CloseInspect();
            Refresh();
            _interaction.SelectBuilding(_mine);
            Refresh();
            Assert.That(staff.IsHaulerGroupOpen(0), Is.False, "A card opened again starts folded");
        }

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        [Test]
        public void WonBattle_SpinsTheReelsOnTheRolledPrize_ThenPaysIt()
        {
            var squad = new[] { Buy(UnitKind.Troll), Buy(UnitKind.Troll), Buy(UnitKind.Goblin), Buy(UnitKind.Goblin) };
            _session.EnableDebugBattleAccess();
            var cells = new[] { new Cell(1, 1), new Cell(1, 3), new Cell(0, 1), new Cell(0, 3) };
            Assert.That(_session.Dispatch(new StartBattleCommand("mission-1",
                squad.Select((id, i) => new BattlePlacement(id, cells[i])).ToArray())).Ok, Is.True);
            Assume.That(_session.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(_session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
            var prize = _session.CurrentSnapshot.BattleReward;
            var mission = _catalog.GetMission("mission-1");
            Assert.That(prize.Gold, Is.InRange(mission.FirstWinGold, mission.FirstWinGoldMax));
            int gold = _session.CurrentSnapshot.Gold;

            Refresh();
            var machine = _hud.BattleReward;
            Assert.That(machine.IsOpen, Is.True);
            Assert.That(machine.IsReady, Is.False, "The reels spin first");
            Assert.That(int.Parse(machine.ShownDigits), Is.EqualTo(prize.Gold));
            Assert.That(machine.ReelCount, Is.EqualTo(mission.FirstWinGoldMax.ToString().Length));

            _hud.Tick(10f);
            Assert.That(machine.IsReady, Is.True);
            Assert.That(machine.AmountText, Is.EqualTo($"+{prize.Gold} золота"));
            UiFeel.Press(machine.ClaimButton);
            Refresh();
            _hud.Tick(1f);

            Assert.That(_session.CurrentSnapshot.Gold, Is.EqualTo(gold + prize.Gold));
            Assert.That(machine.IsOpen, Is.False);
        }
#endif

        private void Refresh() => _hud.OnInteractionChanged(_session.CurrentSnapshot);

        private UnitSnapshot Unit(string id) => _session.CurrentSnapshot.Units.Single(u => u.Id == id);

        private string Buy(UnitKind kind)
        {
            var result = _session.Dispatch(new BuyUnitsCommand(kind, 1, _session.FindSpawnCell()));
            Assert.That(result.Ok, Is.True, result.Error);
            return _session.CurrentSnapshot.Units.Last().Id;
        }
    }
}
