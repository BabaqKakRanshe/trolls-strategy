using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The tutorial pointer (specs/006-tutorial-guidance): the next press of every tutorial step as the colony and the
    /// deployment HUDs show it, the first worker's hire by the warehouse door and the mine card's one-press haul.
    /// </summary>
    public class TutorialGuideTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private InteractionController _interaction;
        private ColonyHudView _hud;
        private bool _hintsBefore;

        [SetUp]
        public void SetUp()
        {
            _hintsBefore = GameSettings.TutorialHints;
            GameSettings.SetTutorialHints(true);
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            _interaction = new InteractionController(_session);
            _hud = TestUi.Colony(_session, _interaction);
            Assert.That(_hud.Guide, Is.Not.Null, "The UI prefab needs its Guide layer: run TrollStrategy/Dev/Setup UI");
        }

        [TearDown]
        public void TearDown() => GameSettings.SetTutorialHints(_hintsBefore);

        [Test]
        public void BothHuds_HaveThePointersLayer()
        {
            Assert.That(TestUi.Battle().Guide, Is.Not.Null);
            Assert.That(_hud.Guide.EdgeButton.userData, Is.Not.Null, "The edge button answers like every button");
        }

        [Test]
        public void FirstWorker_PointsAtTheGoblinToken_InOneStep()
        {
            var step = _hud.CurrentStep;
            Assert.That(step.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)));
            Assert.That(step.Title, Is.EqualTo("Найми первого работника"));
            Assert.That(step.Text, Does.Contain("Гоблин").And.Contain("у склада"));
            Assert.That((step.Number, step.Count), Is.EqualTo((1, 1)));
            Assert.That(step.Veil, Is.True);
            Assert.That(_hud.Guide.IsShowing, Is.True);
            Assert.That(_hud.Guide.CardTitle, Is.EqualTo(step.Title));
            Assert.That(_hud.Guide.CardStep, Is.Empty, "One step: no count on the card");
        }

        [Test]
        public void FirstWorker_TokenHiresByTheWarehouseDoor_AndTheCameraGoesThere()
        {
            var expected = TutorialPlaces.HireCell(_session, UnitKind.Goblin);
            Assert.That(expected, Is.Not.Null);
            WorldPosition? focus = null;
            _interaction.FocusRequested += position => focus = position;

            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));

            var units = _session.CurrentSnapshot.Units;
            Assert.That(units.Count, Is.EqualTo(1));
            Assert.That(CellOf(units[0].Position), Is.EqualTo(expected.Value));
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral), "No cell to pick: it is already hired");
            Assert.That(focus, Is.EqualTo(TutorialPlaces.CenterOf(expected.Value, _catalog)));
            Assert.That(_session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);

            // the door is in front of the warehouse: the cell is next to it
            var warehouse = TutorialPlaces.Warehouse(_session.CurrentSnapshot);
            var door = ColonyNavigation.DoorwayAt(warehouse.Kind, warehouse.Cell, _catalog).Approach;
            Assert.That(System.Math.Abs(expected.Value.X + .5f - door.X), Is.LessThanOrEqualTo(2.5f));
            Assert.That(System.Math.Abs(expected.Value.Y + .5f - door.Y), Is.LessThanOrEqualTo(2.5f));
        }

        [Test]
        public void BuildingsTab_LeadsBackToTheCreaturesTabFirst()
        {
            UiFeel.Press(_hud.Root.Q<Button>("tab-buildings"));
            Refresh();
            var step = _hud.CurrentStep;
            Assert.That(step.Target.Element, Is.SameAs(_hud.Root.Q<Button>("tab-units")));
            Assert.That(step.Title, Is.EqualTo("Вкладка «Существа»"));
            Assert.That(step.Number, Is.EqualTo(1));
        }

        [Test]
        public void TutorialStart_LeadsFromTheFirstWorkerThroughTheMineToTheNextHire()
        {
            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            Claim();
            Assert.That(_session.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo("tutorial-mine"));

            // «Своя шахта»: the token (the catalog turns to its buildings for the quest), the place by the warehouse
            PressTabIfPointed();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Catalog.BuyButton(BuildingKind.Mine)));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 2)));
            UiFeel.Press(_hud.Catalog.BuyButton(BuildingKind.Mine));
            Refresh();
            var place = TutorialPlaces.BuildingCell(_session, BuildingKind.Mine);
            Assert.That(place, Is.Not.Null);
            Assert.That(_session.CanPlaceBuilding(BuildingKind.Mine, place.Value).Ok, Is.True);
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Footprint));
            Assert.That(_hud.CurrentStep.Target.Cell, Is.EqualTo(place.Value));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((2, "Сюда")));
            _interaction.PlaceBuilding(place.Value);
            Claim();
            Assert.That(_session.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo("tutorial-work"));

            // «За работу!»: the goblin, «Работа», the mine
            string goblin = _session.CurrentSnapshot.Units.Single().Id;
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Unit));
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(goblin));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 3)));
            _interaction.ClickUnit(goblin, false);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.ContextBar.WorkButton));
            UiFeel.Press(_hud.ContextBar.WorkButton);
            Refresh();
            string mine = _session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Building));
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(mine));
            Assert.That(_hud.CurrentStep.Number, Is.EqualTo(3));
            _interaction.ChooseBuilding(mine);
            Refresh();

            // the haul needs a free goblin: back to the catalog (the digger is still selected), its creatures, the token
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.TopBar.CatalogButton));
            Assert.That(_hud.CurrentStep.Title, Is.EqualTo("Вернись к каталогу"));
            UiFeel.Press(_hud.TopBar.CatalogButton);
            Refresh();
            PressTabIfPointed();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 2)));
            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            Refresh();
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingUnits), "Only the colony's first creature is placed for the player");
            var cell = TutorialPlaces.HireCell(_session, UnitKind.Goblin);
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Cell));
            Assert.That(_hud.CurrentStep.Target.Cell, Is.EqualTo(cell.Value));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((2, "Сюда")));
            Assert.That(_hud.Guide.CardStep, Is.EqualTo("Шаг 2 из 2"));
        }

        [Test]
        public void MineCard_HaulsToTheWarehouse_InOnePress()
        {
            string mine = ReachWork(freeGoblin: true);
            string idle = _session.CurrentSnapshot.Units.First(u => u.Assignment.Kind == AssignmentKind.Idle).Id;
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Building));
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(mine));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 2)));

            _interaction.SelectBuilding(mine);
            Refresh();
            var button = _hud.Inspect.HaulToWarehouseButton;
            Assert.That(Ui.IsShown(button), Is.True, "The quest asks to carry from the mine to the warehouse");
            Assert.That(UiFeel.IsAvailable(button), Is.True);
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(button));
            Assert.That(_hud.CurrentStep.Number, Is.EqualTo(2));

            UiFeel.Press(button);
            var assignment = _session.CurrentSnapshot.Units.First(u => u.Id == idle).Assignment;
            Assert.That(assignment.Kind, Is.EqualTo(AssignmentKind.Haul));
            Assert.That(assignment.SourceId, Is.EqualTo(mine));
            Assert.That(assignment.DestinationId, Is.EqualTo(TutorialPlaces.Warehouse(_session.CurrentSnapshot).Id));
            Assert.That(_session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);
        }

        [Test]
        public void MineCard_WithoutAFreeGoblin_IsUnavailable_AndSaysWhy()
        {
            string mine = ReachWork(freeGoblin: false);
            _interaction.SelectBuilding(mine);
            Refresh();
            var button = _hud.Inspect.HaulToWarehouseButton;
            Assert.That(Ui.IsShown(button), Is.True);
            Assert.That(UiFeel.IsAvailable(button), Is.False);
            Assert.That(_interaction.HaulToWarehouseBlocker, Does.Contain("Гоблин"));
            PressTabIfPointed();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)),
                "Without a free goblin the pointer leads to the hire first");
        }

        [Test]
        public void MineCard_HasNoHaulButton_WhenTheQuestAsksNothingOfIt()
        {
            string mine = ReachWork(freeGoblin: true);
            Advance("tutorial-market");
            _interaction.SelectBuilding(mine);
            Refresh();
            Assert.That(Ui.IsShown(_hud.Inspect.HaulToWarehouseButton), Is.False);
        }

        [Test]
        public void TwoBuildingHaul_NamesItsStepsMarksTheDestinationAndShowsTheRoute()
        {
            ReachWork(freeGoblin: true);
            Advance("tutorial-market");
            Hire();
            Refresh();
            var snapshot = _session.CurrentSnapshot;
            string warehouse = TutorialPlaces.Warehouse(snapshot).Id;
            string market = snapshot.Buildings.First(b => b.Kind == BuildingKind.Market).Id;
            Assert.That(_hud.Quest.RouteOf(0), Is.EqualTo((RewardArt.BuildingIcon(_catalog, BuildingKind.Warehouse),
                RewardArt.BuildingIcon(_catalog, BuildingKind.Market))), "The quest card shows the route as pictures");

            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Unit));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 5)));
            _interaction.ClickUnit(_hud.CurrentStep.Target.Id, false);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.ContextBar.HaulButton));

            UiFeel.Press(_hud.ContextBar.HaulButton);
            Refresh();
            Assert.That(_hud.ContextBar.Title, Is.EqualTo("1. Откуда носить"));
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(warehouse));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((3, "1. Откуда: Склад")));

            _interaction.ChooseBuilding(warehouse);
            Refresh();
            Assert.That(_hud.HaulCargo.IsOpen, Is.True);
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.HaulCargo.ConfirmButton));
            Assert.That(_hud.CurrentStep.Veil, Is.False, "The cargo dialog has a veil of its own");

            UiFeel.Press(_hud.HaulCargo.ConfirmButton);
            Refresh();
            Assert.That(_hud.ContextBar.Title, Is.EqualTo("2. Куда носить"));
            var marketChip = _hud.ContextBar.TargetButton(BuildingKind.Market);
            Assert.That(marketChip, Is.Not.Null, "Where to carry is listed like where from");
            Assert.That(marketChip.ClassListContains("is-suggested"), Is.True, "The quest's destination is marked");
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(market));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((5, "2. Куда: Рынок")));

            _interaction.ChooseBuilding(market);
            Assert.That(_session.CurrentSnapshot.Progress.Quest.Goals[0].Done, Is.True);
        }

        [Test]
        public void Arena_PointsAtTheToolThenAtTheFightButton()
        {
            Advance("tutorial-battle");
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.TopBar.BattleButton));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, BattleGuide.BattleSteps)));
            _hud.Arena.Open();
            _hud.Tick(.2f);
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Arena.FightButton));
            Assert.That(_hud.CurrentStep.Veil, Is.False, "The arena's dialog has a veil of its own");
        }

        [Test]
        public void WithHintsOff_OrAfterTheTutorial_ThereIsNoPointer_AndTheButtonsBreathe()
        {
            GameSettings.SetTutorialHints(false);
            Refresh();
            Assert.That(_hud.CurrentStep.IsShown, Is.False);
            Assert.That(_hud.Guide.IsShowing, Is.False);
            _hud.Tick(.6f);
            Assert.That(_hud.Root.ClassListContains("hud-pulse"), Is.True, "Without the pointer the suggested buttons breathe");

            GameSettings.SetTutorialHints(true);
            Refresh();
            Assert.That(_hud.CurrentStep.IsShown, Is.True);
            // 1.7 s: an odd half-beat, when the buttons would breathe without the pointer
            _hud.Tick(1.1f);
            Assert.That(_hud.Root.ClassListContains("hud-pulse"), Is.False, "The pointer is the one mark of the next press");

            for (int i = 0; i < 40 && _session.CurrentSnapshot.Progress.Quest.IsTutorial; i++) Complete();
            Refresh();
            Assert.That(_session.CurrentSnapshot.Progress.Quest.IsTutorial, Is.False);
            Assert.That(_hud.CurrentStep.IsShown, Is.False, "After the tutorial the blinking is the only mark");
        }

        [Test]
        public void MenuSetting_TurnsThePointerOff()
        {
            _hud.Menu.Open();
            _hud.Menu.Show(MenuPage.Settings);
            var buttons = _hud.Menu.HintButtons;
            Assert.That(buttons, Has.Count.EqualTo(2));
            Assert.That(buttons[0].ClassListContains("is-on"), Is.True);
            UiFeel.Press(buttons[1]);
            Assert.That(GameSettings.TutorialHints, Is.False);
            Assert.That(buttons[1].ClassListContains("is-on"), Is.True);
        }

        [Test]
        public void Deployment_PointsAtTheAutomaticPlacementThenAtTheStart()
        {
            Advance("tutorial-battle");
            var (hud, deployment) = Deploy();
            Assert.That(hud.CurrentStep.Target.Element, Is.SameAs(hud.Actions.AutoPlace));
            Assert.That((hud.CurrentStep.Number, hud.CurrentStep.Count), Is.EqualTo((3, BattleGuide.BattleSteps)));
            UiFeel.Press(hud.Actions.AutoPlace);
            Assert.That(deployment.Placements, Is.Not.Empty);
            Assert.That(hud.CurrentStep.Target.Element, Is.SameAs(hud.Actions.Start));
            Assert.That(hud.CurrentStep.Number, Is.EqualTo(BattleGuide.BattleSteps));
        }

        [Test]
        public void GearStep_LeadsToAFighterThenToItsItemsThenToTheStart()
        {
            Advance("tutorial-gear");
            Assert.That(_session.DebugGrantGear(1).Ok, Is.True);
            var (hud, deployment) = Deploy();
            deployment.AutoPlace();
            int goal = _session.CurrentSnapshot.Progress.Quest.NextGoal.Target;

            var step = hud.CurrentStep;
            Assert.That(step.Target.Kind, Is.EqualTo(GuideTargetKind.BattleCell));
            Assert.That((step.Number, step.Count), Is.EqualTo((4, BattleGuide.GearSteps)));
            string fighter = deployment.UnitAt(step.Target.Cell);
            Assert.That(fighter, Is.Not.Null);
            deployment.Select(fighter);

            for (int worn = 0; worn < goal; worn++)
            {
                step = hud.CurrentStep;
                Assert.That(step.Number, Is.EqualTo(5), $"item {worn + 1}: {step}");
                var item = deployment.Equipment.First(e => hud.Gear.ItemButton(e.Id) == step.Target.Element &&
                                                           deployment.OwnerOf(e.Id) == null);
                deployment.ToggleEquipment(item.Id);
            }
            Assert.That(hud.CurrentStep.Target.Element, Is.SameAs(hud.Actions.Start));
            Assert.That(hud.CurrentStep.Number, Is.EqualTo(BattleGuide.GearSteps));
        }

        // --- helpers

        private void Refresh() => _hud.OnInteractionChanged(_session.CurrentSnapshot);

        // a step that only turns the catalog to the right tab: take it, as the player would
        private void PressTabIfPointed()
        {
            var element = _hud.CurrentStep.Target.Element;
            if (element == null || (element.name != "tab-units" && element.name != "tab-buildings")) return;
            Assert.That(_hud.CurrentStep.Title, Does.StartWith("Вкладка"));
            UiFeel.Press((Button)element);
            Refresh();
        }

        private static Cell CellOf(WorldPosition position) =>
            new((int)System.Math.Floor(position.X), (int)System.Math.Floor(position.Y));

        private void Hire()
        {
            Assert.That(_session.DebugAddGold(500).Ok, Is.True);
            var cell = TutorialPlaces.HireCell(_session, UnitKind.Goblin) ?? _session.FindSpawnCell();
            var result = _session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, cell));
            Assert.That(result.Ok, Is.True, result.Error);
        }

        // takes the finished quest's reward the way the reveal does, and closes the reveal
        private void Claim()
        {
            Refresh();
            Assert.That(_session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True, "The quest's goals are met");
            Assert.That(_interaction.ClaimQuestReward().Ok, Is.True);
            if (_hud.Reward.IsOpen) _hud.Reward.Hide();
            Refresh();
        }

        private void Complete()
        {
            Assert.That(_session.DebugCompleteQuest().Ok, Is.True);
            Claim();
        }

        private void Advance(string questId)
        {
            for (int i = 0; i < 30 && _session.CurrentSnapshot.Progress.Quest.Id != questId; i++) Complete();
            Assert.That(_session.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo(questId));
        }

        // «За работу!» with the mine built and the first goblin at work there; one more goblin idle if asked
        private string ReachWork(bool freeGoblin)
        {
            Hire();
            Claim();
            var place = _session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, place.Value)).Ok, Is.True);
            Claim();
            Assert.That(_session.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo("tutorial-work"));
            string mine = _session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
            string first = _session.CurrentSnapshot.Units.Single().Id;
            Assert.That(_session.Dispatch(new AssignWorkCommand(new[] { first }.ToList(), mine)).Ok, Is.True);
            if (freeGoblin) Hire();
            Refresh();
            return mine;
        }

        private (BattleHudView Hud, BattleDeployment Deployment) Deploy()
        {
            Assert.That(_session.DebugAddGold(1000).Ok, Is.True);
            for (int i = 0; i < 3; i++) Hire();
            var mission = _catalog.Missions.First(m => m != null);
            var deployment = new BattleDeployment(_session, mission, mission.CreateBoard());
            var hud = TestUi.Battle();
            hud.Open(deployment);
            return (hud, deployment);
        }
    }
}
