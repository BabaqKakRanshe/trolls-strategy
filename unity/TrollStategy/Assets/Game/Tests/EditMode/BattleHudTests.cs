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
    /// <summary>The battle HUD built from the UI prefab over a real deployment, driven the way the player drives it.</summary>
    public class BattleHudTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private BattleMissionDefinition _mission;
        private BattleBoard _board;
        private BattleDeployment _deployment;
        private BattleHudView _hud;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _session = TestColony.NewSession(_catalog);
            Assert.That(_session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, _session.FindSpawnCell())).Ok, Is.True);
            _mission = _catalog.Missions.First(mission => mission != null);
            _board = _mission.CreateBoard();
            _deployment = new BattleDeployment(_session, _mission, _board);
            _hud = TestUi.Battle();
            _hud.Open(_deployment);
        }

        [Test]
        public void EveryButtonInTheLayout_IsBoundToAnAction()
        {
            var unbound = _hud.Root.Query<Button>().ToList().Where(button => button.userData == null).ToList();
            Assert.That(unbound.Select(button => button.name), Is.Empty);
        }

        [Test]
        public void Deployment_ListsTheColonysFighters_AndTheStartWaitsForASquad()
        {
            Assert.That(_hud.IsDeploying, Is.True);
            Assert.That(_hud.Replay.IsShown, Is.False);
            Assert.That(_hud.Roster.Count, Is.EqualTo(_session.CurrentSnapshot.Units.Count));
            Assert.That(_hud.Header.Title, Does.Contain("РАССТАНОВКА"));
            Assert.That(_hud.Actions.Squad, Is.EqualTo($"3 · ОТРЯД 0 / {_mission.MaxPlayerUnits}"));
            Assert.That(UiFeel.IsAvailable(_hud.Actions.Start), Is.False);
            Assert.That(UiFeel.IsAvailable(_hud.Gear.Remove), Is.False);
        }

        [Test]
        public void SelectingAFighterThenACell_PlacesIt_AndOpensTheStart()
        {
            var unitId = _deployment.Roster[0].Id;
            UiFeel.Press(_hud.Roster.CardOf(unitId));
            Assert.That(_deployment.SelectedUnitId, Is.EqualTo(unitId));
            Assert.That(_hud.Roster.CardOf(unitId).ClassListContains("is-on"), Is.True);

            Assert.That(_deployment.ClickCell(FreeCell()), Is.EqualTo(DeploymentClick.Placed));

            Assert.That(_hud.Roster.HintOf(unitId), Does.Contain("на поле"));
            Assert.That(_hud.Roster.CardOf(unitId).ClassListContains("is-placed"), Is.True);
            Assert.That(UiFeel.IsAvailable(_hud.Actions.Start), Is.True);
            Assert.That(_hud.Actions.Start.text, Is.EqualTo("НАЧАТЬ БОЙ · 1 В ОТРЯДЕ"));
            Assert.That(UiFeel.IsAvailable(_hud.Gear.Remove), Is.True);
            Assert.That(_hud.Gear.Details, Does.Contain(_deployment.UnitName(unitId)));
        }

        [Test]
        public void ACellClickWithoutAFighter_IsRefusedWithTheReason()
        {
            Assert.That(_deployment.ClickCell(FreeCell()), Is.EqualTo(DeploymentClick.Refused));
            _hud.Refuse();

            Assert.That(_hud.Actions.Hint, Is.EqualTo("Сначала выбери бойца в списке слева"));
            Assert.That(_deployment.Placements, Is.Empty);
        }

        [Test]
        public void AutoPlace_FillsTheSquad_AndRemoveTakesTheSelectedOffTheBoard()
        {
            UiFeel.Press(_hud.Actions.AutoPlace);
            int squad = System.Math.Min(_mission.MaxPlayerUnits, _deployment.Roster.Count);
            Assert.That(_deployment.Placements.Count, Is.EqualTo(squad));
            Assert.That(UiFeel.IsAvailable(_hud.Actions.AutoPlace), Is.False);

            UiFeel.Press(_hud.Gear.Remove);

            Assert.That(_deployment.Placements.Count, Is.EqualTo(squad - 1));
            Assert.That(_hud.Actions.Hint, Does.Contain("убран с поля"));
        }

        [Test]
        public void Gear_GoesOnlyToAFighterOnTheBoard()
        {
            if (_deployment.Equipment.Count == 0)
            {
                Assert.That(_hud.Root.Q<Label>("battle-gear-empty").style.display.value, Is.Not.EqualTo(DisplayStyle.None));
                return;
            }
            var itemId = _deployment.Equipment[0].Id;
            var unitId = _deployment.Roster[0].Id;
            UiFeel.Press(_hud.Roster.CardOf(unitId));
            Assert.That(UiFeel.IsAvailable(_hud.Gear.ItemButton(itemId)), Is.False, "Gear waits until the fighter is placed");
            UiFeel.Press(_hud.Gear.ItemButton(itemId));
            Assert.That(_deployment.OwnerOf(itemId), Is.Null);

            _deployment.ClickCell(FreeCell());
            UiFeel.Press(_hud.Gear.ItemButton(itemId));

            Assert.That(_deployment.OwnerOf(itemId), Is.EqualTo(unitId));
            Assert.That(_hud.Gear.HintOf(itemId), Does.StartWith("Надето"));
            Assert.That(_hud.Gear.ItemButton(itemId).ClassListContains("is-on"), Is.True);
        }

        [Test]
        public void TheControls_AskTheBattle()
        {
            int starts = 0, pauses = 0, closes = 0;
            float speed = 0f;
            _hud.StartRequested += () => starts++;
            _hud.PauseToggled += () => pauses++;
            _hud.SpeedChosen += value => speed = value;
            _hud.CloseRequested += () => closes++;

            UiFeel.Press(_hud.Actions.Start);
            Assert.That(starts, Is.Zero, "No start without a squad");
            UiFeel.Press(_hud.Actions.AutoPlace);
            UiFeel.Press(_hud.Actions.Start);
            UiFeel.Press(_hud.Replay.Pause);
            UiFeel.Press(_hud.Replay.SpeedButton(4f));
            UiFeel.Press(_hud.Header.Back);
            UiFeel.Press(_hud.Replay.Return);

            Assert.That((starts, pauses, speed, closes), Is.EqualTo((1, 1, 4f, 2)));
        }

        [Test]
        public void Replay_ShowsTheFight_AndTheVerdictOffersTheWayHome()
        {
            _hud.BeginReplay();
            Assert.That(_hud.IsDeploying, Is.False);
            Assert.That(_hud.Replay.IsShown, Is.True);
            Assert.That(_hud.Banner.Text, Is.EqualTo("В БОЙ!"));

            _hud.ShowReplay(true, 2f, 3.2f, 2, 1);
            Assert.That(_hud.Replay.Status, Does.StartWith("ПАУЗА · "));
            Assert.That(_hud.Replay.Status, Does.Contain("Враги: 1 в строю"));
            Assert.That(_hud.Replay.Pause.text, Is.EqualTo("ПРОДОЛЖИТЬ"));
            Assert.That(_hud.Replay.SpeedButton(2f).ClassListContains("is-on"), Is.True);

            _hud.ShowResult(BattleOutcome.PlayerVictory, 2, 0, 0);
            Assert.That(_hud.Replay.Status, Does.StartWith("ПОБЕДА · награда ждёт в поселении"));
            Assert.That(_hud.Header.Title, Does.EndWith("ПОБЕДА"));
            Assert.That(Ui.IsShown(_hud.Replay.Return), Is.True);
            Assert.That(Ui.IsShown(_hud.Replay.Pause), Is.False);
        }

        private Cell FreeCell() => _mission.PlayerDeployment.First(cell =>
            _board.CanPlace(cell) && !_deployment.Placements.Any(placement => placement.Cell == cell));
    }
}
