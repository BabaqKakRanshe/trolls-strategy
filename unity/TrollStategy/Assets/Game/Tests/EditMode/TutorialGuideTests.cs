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
    /// deployment HUDs show it, every hire placed by the player, the hauls given by the order and the first launch's
    /// question about the hints.
    /// </summary>
    public class TutorialGuideTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private InteractionController _interaction;
        private ColonyHudView _hud;
        private bool _hintsBefore;
        private bool _hintsChosenBefore;

        [SetUp]
        public void SetUp()
        {
            _hintsBefore = GameSettings.TutorialHints;
            _hintsChosenBefore = GameSettings.TutorialHintsChosen;
            GameSettings.SetTutorialHints(true);
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            _interaction = new InteractionController(_session);
            _hud = TestUi.Colony(_session, _interaction);
            Assert.That(_hud.Guide, Is.Not.Null, "The UI prefab needs its Guide layer: run TrollStrategy/Dev/Setup UI");
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.SetTutorialHints(_hintsBefore);
            // an editor that never answered keeps asking at its next start
            if (!_hintsChosenBefore) UnityEngine.PlayerPrefs.DeleteKey("settings.tutorialHints");
        }

        [Test]
        public void BothHuds_HaveThePointersLayer()
        {
            Assert.That(TestUi.Battle().Guide, Is.Not.Null);
            Assert.That(_hud.Guide.EdgeButton.userData, Is.Not.Null, "The edge button answers like every button");
        }

        [Test]
        public void FirstWorker_PointsAtTheGoblinToken_ThenAtTheCellByTheWarehouse()
        {
            var step = _hud.CurrentStep;
            Assert.That(step.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)));
            Assert.That(step.Title, Is.EqualTo("Найми первого работника"));
            Assert.That(step.Text, Does.Contain("Гоблин"));
            Assert.That((step.Number, step.Count), Is.EqualTo((1, 2)));
            Assert.That(step.Veil, Is.True);
            Assert.That(_hud.Guide.IsShowing, Is.True);
            Assert.That(_hud.Guide.CardTitle, Is.EqualTo(step.Title));
            Assert.That(_hud.Guide.CardStep, Is.EqualTo("Шаг 1 из 2"));

            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            Refresh();
            Assert.That(_session.CurrentSnapshot.Units, Is.Empty, "The player puts the first worker down too");
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingUnits));
            var cell = TutorialPlaces.HireCell(_session, UnitKind.Goblin);
            Assert.That(cell, Is.Not.Null);
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Cell));
            Assert.That(_hud.CurrentStep.Target.Cell, Is.EqualTo(cell.Value));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((2, "Сюда")));
            // the pin over the cell shows the goblin, as its token does
            var portrait = RewardArt.Tight(_catalog.GetUnit(UnitKind.Goblin).PortraitSprite);
            Assert.That(portrait, Is.Not.Null);
            Assert.That(_hud.CurrentStep.Target.Art, Is.SameAs(portrait));

            // the door is in front of the warehouse: the cell is next to it
            var warehouse = TutorialPlaces.Warehouse(_session.CurrentSnapshot);
            var door = ColonyNavigation.DoorwayAt(warehouse.Kind, warehouse.Cell, _catalog).Approach;
            Assert.That(System.Math.Abs(cell.Value.X + .5f - door.X), Is.LessThanOrEqualTo(2.5f));
            Assert.That(System.Math.Abs(cell.Value.Y + .5f - door.Y), Is.LessThanOrEqualTo(2.5f));

            _interaction.PlaceUnits(cell.Value);
            Assert.That(_session.CurrentSnapshot.Units.Count, Is.EqualTo(1));
            Assert.That(_session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);
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
        public void TutorialStart_LeadsFromTheFirstWorkerThroughTheMineToTheFirstHaul()
        {
            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            _interaction.PlaceUnits(TutorialPlaces.HireCell(_session, UnitKind.Goblin).Value);
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
            // west of the warehouse (the left of the screen), a lane between them
            var store = TutorialPlaces.Warehouse(_session.CurrentSnapshot);
            Assert.That(place.Value.X + _catalog.GetBuilding(BuildingKind.Mine).Width,
                Is.LessThanOrEqualTo(store.Cell.X - TutorialPlaces.LaneCells));
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Footprint));
            Assert.That(_hud.CurrentStep.Target.Cell, Is.EqualTo(place.Value));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((2, "Сюда")));
            Assert.That(_hud.CurrentStep.Target.Art,
                Is.Not.Null.And.SameAs(RewardArt.BuildingIcon(_catalog.GetBuilding(BuildingKind.Mine))),
                "The pin over the place shows the mine");
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
            Assert.That(_hud.CurrentStep.Text, Does.Contain("правой кнопкой"), "The right click's fan gives the same orders");
            UiFeel.Press(_hud.ContextBar.WorkButton);
            Refresh();
            string mine = _session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Building));
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(mine));
            Assert.That(_hud.CurrentStep.Number, Is.EqualTo(3));
            _interaction.ChooseBuilding(mine);
            Refresh();

            // the digger leaves the selection with its order, so the catalog is back at once: its creatures, the token
            Assert.That(_interaction.SelectedIds, Is.Empty);
            Assert.That(_hud.Catalog.IsCovered, Is.False);
            PressTabIfPointed();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 2)));
            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            Refresh();
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingUnits));
            var cell = TutorialPlaces.HireCell(_session, UnitKind.Goblin);
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Cell));
            Assert.That(_hud.CurrentStep.Target.Cell, Is.EqualTo(cell.Value));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((2, "Сюда")));
            Assert.That(_hud.Guide.CardStep, Is.EqualTo("Шаг 2 из 2"));
            _interaction.PlaceUnits(cell.Value);
            Refresh();

            // the haul from the mine to the warehouse is the order: the new goblin, «Перенос», the mine, what, the warehouse
            string warehouse = TutorialPlaces.Warehouse(_session.CurrentSnapshot).Id;
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Unit));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 5)));
            _interaction.ClickUnit(_hud.CurrentStep.Target.Id, false);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.ContextBar.HaulButton));
            UiFeel.Press(_hud.ContextBar.HaulButton);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(mine));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((3, "1. Откуда: Шахта")));
            _interaction.ChooseBuilding(mine);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.HaulCargo.ConfirmButton));
            UiFeel.Press(_hud.HaulCargo.ConfirmButton);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Id, Is.EqualTo(warehouse));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Title), Is.EqualTo((5, "2. Куда: Склад")));
            _interaction.ChooseBuilding(warehouse);
            Assert.That(_session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);

            // «Первая выручка»: the hauler is still selected, but busy; the pointer does not take it off its route
            Claim();
            Assert.That(_interaction.SelectedIds, Is.Not.Empty);
            Assert.That(_hud.CurrentStep.Target.Element, Is.Not.SameAs(_hud.ContextBar.HaulButton));
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.TopBar.CatalogButton), "No one is free: a hire first");
        }

        [Test]
        public void HaulQuest_WithoutAFreeGoblin_PlacesTheHireInsteadOfCancellingIt()
        {
            ReachWork(freeGoblin: false);
            Advance("tutorial-market");
            Refresh();
            OpenCatalogIfPointed();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 2)));

            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            Refresh();
            // playtest 2026-10-07: «Сначала отмени», and the cancel led back to the token, round and round
            Assert.That(_hud.CurrentStep.Title, Is.Not.EqualTo("Сначала отмени"));
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Cell));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((2, 2)));

            _interaction.PlaceUnits(_hud.CurrentStep.Target.Cell);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Unit), "The new goblin is the carrier");
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((1, 5)));
        }

        [Test]
        public void WorkQuest_WithoutAFreeGoblin_PlacesTheHireInsteadOfCancellingIt()
        {
            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            _interaction.PlaceUnits(TutorialPlaces.HireCell(_session, UnitKind.Goblin).Value);
            Claim();
            var place = _session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, place.Value)).Ok, Is.True);
            Claim();
            Assert.That(_session.Dispatch(new SellUnitsCommand(_session.CurrentSnapshot.Units.Select(u => u.Id).ToList())).Ok,
                Is.True);
            Refresh();
            OpenCatalogIfPointed();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Catalog.HireButton(UnitKind.Goblin)));
            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Goblin));
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Cell));
            Assert.That((_hud.CurrentStep.Number, _hud.CurrentStep.Count), Is.EqualTo((2, 2)));
        }

        [Test]
        public void OrderStep_PointsAtTheRightClickFan_WhenItIsOpen()
        {
            ReachWork(freeGoblin: true);
            Assert.That(_hud.CurrentStep.Target.Kind, Is.EqualTo(GuideTargetKind.Unit));
            _interaction.ClickUnit(_hud.CurrentStep.Target.Id, false);
            Refresh();
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.ContextBar.HaulButton));
            Assert.That(_hud.CurrentStep.Text, Does.Contain("правой кнопкой"));

            _interaction.ToggleCommands(true);
            _hud.Fan.Refresh();
            Refresh();
            Assert.That(_hud.Fan.IsShown, Is.True);
            Assert.That(_hud.CurrentStep.Target.Element, Is.SameAs(_hud.Fan.HaulButton));
            Assert.That(_hud.CurrentStep.Number, Is.EqualTo(2));
        }

        [Test]
        public void BuildingCard_HasNoHaulShortcut()
        {
            string mine = ReachWork(freeGoblin: true);
            _interaction.SelectBuilding(mine);
            Refresh();
            Assert.That(_hud.Root.Q<Button>(className: "inspect-haul"), Is.Null, "Hauls go by the order, at the bottom or in the fan");
        }

        [Test]
        public void PlacesOnTheMap_GetAPinOverThem_InsteadOfTheWindowAndTheHand()
        {
            Assert.That(GuidePlace.OnGround(GuideTarget.Place(new Cell(1, 1), 3, 3)), Is.True);
            Assert.That(GuidePlace.OnGround(GuideTarget.At(new Cell(1, 1))), Is.True);
            Assert.That(GuidePlace.OnGround(GuideTarget.Building("warehouse-1")), Is.False);
            Assert.That(GuidePlace.OnGround(GuideTarget.Unit("unit-1")), Is.False);
            Assert.That(GuidePlace.OnGround(GuideTarget.Of(_hud.TopBar.CatalogButton)), Is.False);

            // seen straight from above at 10 px a cell, down the screen as the map goes up: the corners round the place
            var corners = GuidePlace.Corners(new Cell(10, 20), 3, 2, 1f, p => new UnityEngine.Vector2(p.X * 10f, p.Y * 10f));
            Assert.That(corners, Is.EqualTo(new[]
            {
                new UnityEngine.Vector2(100f, 200f), new UnityEngine.Vector2(130f, 200f),
                new UnityEngine.Vector2(130f, 220f), new UnityEngine.Vector2(100f, 220f)
            }));
            Assert.That(GuidePlace.Corners(new Cell(0, 0), 1, 1, 1f, _ => null), Is.Null, "Nothing to draw off the camera");
            Assert.That(GuidePlace.CellSide(corners, 3, 2), Is.EqualTo(10f).Within(.001f));

            // the light keeps inside the grid's lines
            var light = new UnityEngine.Vector2[4];
            GuidePlace.Inset(corners, 3, 2, GuidePlace.LightInset, light);
            Assert.That(light[0].x, Is.EqualTo(100f + 10f * GuidePlace.LightInset).Within(.001f));
            Assert.That(light[2].y, Is.EqualTo(220f - 10f * GuidePlace.LightInset).Within(.001f));

            // the pin floats over the place's middle, its tail's tip just above it, and rises and falls
            float radius = GuidePlace.PinRadius(3, 2);
            Assert.That(radius, Is.GreaterThan(GuidePlace.PinRadius(1, 1)), "A building's place gets a bigger pin than a cell");
            var low = GuidePlace.PinCentre(corners, radius, 0f);
            var high = GuidePlace.PinCentre(corners, radius, 1f);
            var tip = GuidePlace.PinTip(low, radius);
            Assert.That(tip.x, Is.EqualTo(115f).Within(.001f));
            Assert.That(tip.y, Is.EqualTo(210f - GuidePlace.Hover).Within(.001f));
            Assert.That(low.y - high.y, Is.EqualTo(GuidePlace.FloatHeight).Within(.001f));
            Assert.That(GuidePlace.Float(0f), Is.EqualTo(0f).Within(.001f));
            Assert.That(GuidePlace.Float(GuidePlace.FloatSeconds / 2f), Is.EqualTo(1f).Within(.001f));

            // the tail's sides touch the disc: each meets the radius there at a right angle
            var (from, to) = GuidePlace.PinArc;
            foreach (float angle in new[] { from, to })
            {
                var touch = low + new UnityEngine.Vector2(UnityEngine.Mathf.Cos(angle), UnityEngine.Mathf.Sin(angle)) * radius;
                Assert.That(UnityEngine.Vector2.Dot(touch - low, tip - touch), Is.EqualTo(0f).Within(.01f));
            }
        }

        [Test]
        public void ControlsLesson_ComesFirst_TheCameraTheZoomTheKeys_ThenTheFirstWorker()
        {
            bool learned = GameSettings.ControlsLearned;
            GameSettings.SetControlsLearned(false);
            try
            {
                float panned = 0f, zoomed = 0f;
                var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
                {
                    ToggleGuides = () => { },
                    GuidesVisible = () => false,
                    OpenBattle = _ => { },
                    CameraPanned = () => panned,
                    CameraZoomed = () => zoomed
                });
                void Frame()
                {
                    hud.Tick(.2f);
                    hud.OnInteractionChanged(_session.CurrentSnapshot);
                }

                Frame();
                var step = hud.CurrentStep;
                Assert.That(step.Target.Kind, Is.EqualTo(GuideTargetKind.Card), "Nothing to point at: the card alone");
                Assert.That((step.Number, step.Count, step.Title), Is.EqualTo((1, 3, "Осмотри остров")));
                Assert.That(step.Keys.Select(k => k.ToString()), Is.EqualTo(new[] { "W", "A", "S", "D", "mouse-right" }));
                Assert.That(hud.Guide.CardKeys, Is.EqualTo(new[] { "W", "A", "S", "D", "mouse-right" }));

                panned = .3f;
                Frame();
                Assert.That(hud.Lesson.Current, Is.EqualTo(ControlsLesson.Stage.Move), "Not far enough yet");
                panned = 1f;
                Frame();
                Frame();
                Assert.That((hud.CurrentStep.Number, hud.CurrentStep.Title), Is.EqualTo((2, "Ближе и дальше")));
                Assert.That(hud.Guide.CardKeys, Is.EqualTo(new[] { "mouse-wheel" }));

                zoomed = .5f;
                Frame();
                Frame();
                step = hud.CurrentStep;
                Assert.That((step.Number, step.Title), Is.EqualTo((3, "Клавиши")));
                Assert.That(step.Keys.Select(k => k.Label), Is.EqualTo(ControlsLesson.ToolKeys.Select(k => k.Key.Label)));
                Assert.That(step.Keys.All(k => !string.IsNullOrEmpty(k.Name)), Is.True, "Every key with what it opens");

                hud.Lesson.NoteKey();
                Frame();
                Assert.That(hud.Lesson.IsDone, Is.True);
                Assert.That(GameSettings.ControlsLearned, Is.True, "Once per player");
                Assert.That(hud.CurrentStep.Target.Element, Is.SameAs(hud.Catalog.HireButton(UnitKind.Goblin)));
            }
            finally
            {
                GameSettings.SetControlsLearned(learned);
            }
        }

        [Test]
        public void ControlsLesson_KeysCardGoesByItself_AndATouchScreenSkipsTheLesson()
        {
            var lesson = new ControlsLesson(() => 0f, () => 0f, learned: false);
            Assert.That(lesson.Current, Is.EqualTo(ControlsLesson.Stage.Move));
            bool learned = GameSettings.ControlsLearned;
            try
            {
                var keys = new ControlsLesson(() => 5f, () => 5f, learned: false);
                keys.Tick(.1f, true);
                keys.Tick(.1f, true);
                Assert.That(keys.Current, Is.EqualTo(ControlsLesson.Stage.Move), "Counted from when its card came up");
                float panned = 0f, zoomed = 0f;
                var walk = new ControlsLesson(() => panned, () => zoomed, learned: false);
                walk.Tick(.1f, true);
                panned = 1f;
                walk.Tick(.1f, true);
                walk.Tick(.1f, true);
                zoomed = 1f;
                walk.Tick(.1f, true);
                Assert.That(walk.Current, Is.EqualTo(ControlsLesson.Stage.Keys));
                walk.Tick(ControlsLesson.KeysSeconds / 2f, false);
                Assert.That(walk.Current, Is.EqualTo(ControlsLesson.Stage.Keys), "Time runs only while the card is up");
                walk.Tick(ControlsLesson.KeysSeconds + 1f, true);
                Assert.That(walk.IsDone, Is.True);
            }
            finally
            {
                GameSettings.SetControlsLearned(learned);
            }

            bool touch = false;
            var fingers = new ControlsLesson(() => 0f, () => 0f, learned: false, () => touch);
            touch = true;
            fingers.Tick(.1f, true);
            Assert.That(fingers.IsDone, Is.True, "No keys and no wheel on a touch screen");
            Assert.That(new ControlsLesson(null, null, learned: false).IsDone, Is.True, "No camera, no lesson");
        }

        [Test]
        public void OrderSteps_ShowTheirKeysOnTheCard()
        {
            ReachWork(freeGoblin: true);
            _interaction.ClickUnit(_hud.CurrentStep.Target.Id, false);
            Refresh();
            Assert.That(_hud.CurrentStep.Keys.Select(k => k.ToString()), Is.EqualTo(new[] { "H", "mouse-right" }));
            Assert.That(_hud.Guide.CardKeys, Is.EqualTo(new[] { "H", "mouse-right" }));
        }

        [Test]
        public void Pointer_WaitsWhileTheCameraFliesIn()
        {
            bool flying = true;
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                ToggleGuides = () => { },
                GuidesVisible = () => false,
                OpenBattle = _ => { },
                CameraBusy = () => flying
            });
            hud.OnInteractionChanged(_session.CurrentSnapshot);
            Assert.That(hud.CurrentStep.IsShown, Is.False, "Nothing covers the flight over the island");
            flying = false;
            hud.OnInteractionChanged(_session.CurrentSnapshot);
            Assert.That(hud.CurrentStep.IsShown, Is.True);
        }

        [Test]
        public void FirstLaunch_AsksAboutTheHints_AndPlayWaitsForTheAnswer()
        {
            var intro = _hud.Intro;
            intro.Open();
            intro.AskHints(true);
            Assert.That(intro.ShowsHints, Is.True);
            Assert.That(intro.AsksHints, Is.True);
            Assert.That(UiFeel.IsAvailable(intro.PlayButton), Is.False, "«Играть» waits for an answer");
            intro.Close();
            Assert.That(intro.IsOpen, Is.True, "Neither Esc nor Enter skips the question");

            UiFeel.Press(intro.HintsOffButton);
            Assert.That(GameSettings.TutorialHints, Is.False);
            Assert.That(GameSettings.TutorialHintsChosen, Is.True);
            Assert.That(intro.HintsOffButton.ClassListContains("is-on"), Is.True);
            Assert.That(UiFeel.IsAvailable(intro.PlayButton), Is.True);
            Refresh();
            Assert.That(_hud.CurrentStep.IsShown, Is.False, "No hand, veil or card");

            UiFeel.Press(intro.HintsOnButton);
            Assert.That(GameSettings.TutorialHints, Is.True);
            Assert.That(intro.HintsOnButton.ClassListContains("is-on"), Is.True);
            intro.AskHints(false);
            Assert.That(intro.ShowsHints, Is.False, "Answered once, not asked again");
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

        // the catalog and its creatures, when the pointer asks for them first
        private void OpenCatalogIfPointed()
        {
            var element = _hud.CurrentStep.Target.Element;
            if (element == _hud.TopBar.CatalogButton)
            {
                UiFeel.Press(_hud.TopBar.CatalogButton);
                Refresh();
            }
            PressTabIfPointed();
        }

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
