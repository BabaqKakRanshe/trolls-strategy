using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine;
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
            Assert.That(_session.DebugAddGold(1000).Ok, Is.True);
            Assert.That(_session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, _session.FindSpawnCell())).Ok, Is.True);
            Assert.That(_session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, _session.FindSpawnCell())).Ok, Is.True);
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
        public void Deployment_ShowsTheMissionAndTheReserve_AndTheStartWaitsForASquad()
        {
            Assert.That(_hud.IsDeploying, Is.True);
            Assert.That(_hud.Replay.IsShown, Is.False);
            Assert.That(_hud.Header.Title, Is.EqualTo(_mission.DisplayName));
            Assert.That(_hud.Header.Enemies, Is.EqualTo(_mission.Enemies.GroupBy(enemy => enemy.Kind)
                .Select(group => group.Count().ToString()).ToArray()));
            Assert.That(_hud.Header.Reward, Does.StartWith(_deployment.WinGold.Min.ToString()));

            Assert.That(_hud.Squad.Count, Is.EqualTo(2), "a button per kind: goblins and trolls");
            foreach (var kind in _deployment.Kinds)
                Assert.That(_hud.Squad.ReserveOf(kind), Is.EqualTo(_deployment.ReserveOf(kind).ToString()));
            Assert.That(_hud.Squad.KindButton(UnitKind.Goblin).ClassListContains("is-on"), Is.True, "picked first");
            Assert.That(_hud.Squad.Squad, Is.EqualTo($"0/{_mission.MaxPlayerUnits}"));
            Assert.That(_hud.Gear.IsShown, Is.False, "no fighter is selected");
            Assert.That(_hud.Actions.Hint, Is.EqualTo("Нажми синюю клетку: встанет гоблин"));
            Assert.That(UiFeel.IsAvailable(_hud.Actions.AutoPlace), Is.True);
            Assert.That(UiFeel.IsAvailable(_hud.Actions.Start), Is.False);
        }

        [Test]
        public void PressingAKind_PicksWhoTheNextClickBrings()
        {
            UiFeel.Press(_hud.Squad.KindButton(UnitKind.Troll));
            Assert.That(_hud.Squad.KindButton(UnitKind.Troll).ClassListContains("is-on"), Is.True);
            Assert.That(_hud.Squad.KindButton(UnitKind.Goblin).ClassListContains("is-on"), Is.False);
            Assert.That(_hud.Actions.Hint, Is.EqualTo("Нажми синюю клетку: встанет тролль"));

            Assert.That(_deployment.ClickCell(FreeCell()), Is.EqualTo(DeploymentResult.Placed));

            string unitId = _deployment.SelectedUnitId;
            Assert.That(_deployment.UnitKinds[unitId], Is.EqualTo(UnitKind.Troll));
            Assert.That(_hud.Squad.ReserveOf(UnitKind.Troll), Is.EqualTo(_deployment.ReserveOf(UnitKind.Troll).ToString()));
            Assert.That(_hud.Squad.Squad, Is.EqualTo($"1/{_mission.MaxPlayerUnits}"));
            Assert.That(_hud.Gear.IsShown, Is.True);
            Assert.That(_hud.Gear.Health, Is.EqualTo(_deployment.DefinitionOf(unitId).CombatHealth.ToString()));
            Assert.That(UiFeel.IsAvailable(_hud.Actions.Start), Is.True);
        }

        [Test]
        public void DraggingAKindOutOfTheReserve_HandsItToTheBattle()
        {
            UnitKind? dropped = null;
            _hud.KindDropped += kind => dropped = kind;

            _hud.Squad.Press(UnitKind.Troll, Vector2.zero);
            _hud.Squad.Drag(new Vector2(3f, 0f));
            Assert.That(_hud.Squad.IsDragging, Is.False, "a twitch is still a click");
            _hud.Squad.Drag(new Vector2(240f, -180f));
            Assert.That(_hud.Squad.IsDragging, Is.True);
            _hud.Squad.LetGo();

            Assert.That(dropped, Is.EqualTo(UnitKind.Troll));
            Assert.That(_hud.Squad.IsDragging, Is.False);
        }

        [Test]
        public void AKindWithNobodyLeft_IsUnavailable_AndThePickMovesOn()
        {
            UiFeel.Press(_hud.Squad.KindButton(UnitKind.Troll));
            while (_deployment.ReserveOf(UnitKind.Troll) > 0) _deployment.ClickCell(FreeCell());

            Assert.That(UiFeel.IsAvailable(_hud.Squad.KindButton(UnitKind.Troll)), Is.False);
            Assert.That(_hud.Squad.KindButton(UnitKind.Goblin).ClassListContains("is-on"), Is.True);
            UiFeel.Press(_hud.Squad.KindButton(UnitKind.Troll));
            Assert.That(_deployment.PickedKind, Is.EqualTo(UnitKind.Goblin));
        }

        [Test]
        public void ARefusedClick_ShowsTheReasonUnderTheBoard()
        {
            var enemyCell = _mission.EnemyDeployment.First(cell => !_board.IsBlocked(cell));
            Assert.That(_deployment.ClickCell(enemyCell), Is.EqualTo(DeploymentResult.Refused));
            _hud.Refuse();

            Assert.That(_hud.Actions.Hint, Is.EqualTo("Бойцов ставят на синие клетки"));
            Assert.That(_deployment.Placements, Is.Empty);
        }

        [Test]
        public void AutoPlace_FillsTheSquad_AndTheCrossSendsTheSelectedBack()
        {
            UiFeel.Press(_hud.Actions.AutoPlace);
            Assert.That(_deployment.Placements.Count, Is.EqualTo(_deployment.SquadLimit));
            Assert.That(UiFeel.IsAvailable(_hud.Actions.AutoPlace), Is.False);

            _deployment.ClickCell(_deployment.Placements[0].Cell);
            UiFeel.Press(_hud.Gear.Remove);

            Assert.That(_deployment.Placements.Count, Is.EqualTo(_deployment.SquadLimit - 1));
            Assert.That(_hud.Gear.IsShown, Is.False);
            Assert.That(UiFeel.IsAvailable(_hud.Actions.AutoPlace), Is.True);
        }

        [Test]
        public void Gear_GoesToTheSelectedFighter_AndIsHandedOverFromAnother()
        {
            if (_deployment.Equipment.Count == 0) Assert.Ignore("The catalog starts the colony without gear");
            var item = _deployment.Equipment[0];
            _deployment.ClickCell(FreeCell());
            string first = _deployment.SelectedUnitId;

            UiFeel.Press(_hud.Gear.ItemButton(item.Id));
            Assert.That(_deployment.OwnerOf(item.Id), Is.EqualTo(first));
            Assert.That(_hud.Gear.ItemButton(item.Id).ClassListContains("is-on"), Is.True);
            Assert.That(_hud.Gear.HintOf(item.Id), Does.Contain("Надето"));

            _deployment.ClickCell(FreeCell());
            Assert.That(_hud.Gear.ItemButton(item.Id).ClassListContains("is-taken"), Is.True, "worn by the first fighter");
            UiFeel.Press(_hud.Gear.ItemButton(item.Id));
            Assert.That(_deployment.OwnerOf(item.Id), Is.EqualTo(_deployment.SelectedUnitId));
            Assert.That(_deployment.OwnerOf(item.Id), Is.Not.EqualTo(first));
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
        public void Replay_ShowsBothSides_AndTheVerdictOffersTheWayHome()
        {
            UiFeel.Press(_hud.Actions.AutoPlace);
            _hud.BeginReplay();
            Assert.That(_hud.IsDeploying, Is.False);
            Assert.That(_hud.Replay.IsShown, Is.True);
            Assert.That(_hud.Banner.Text, Is.EqualTo("В бой!"));

            _hud.ShowReplay(true, 2f, 2, 1);
            Assert.That((_hud.Replay.Ours, _hud.Replay.Theirs), Is.EqualTo(("2", "1")));
            Assert.That(_hud.Replay.IsPaused, Is.True);
            Assert.That(_hud.Replay.Pause.ClassListContains("is-on"), Is.True);
            Assert.That(_hud.Replay.SpeedButton(2f).ClassListContains("is-on"), Is.True);

            _hud.ShowResult(BattleOutcome.PlayerVictory, 2, 0, 0);
            Assert.That(_hud.Replay.Verdict, Is.EqualTo("Победа"));
            Assert.That((_hud.Replay.Survived, _hud.Replay.Fallen), Is.EqualTo(("2", "0")));
            Assert.That(_hud.Replay.ShowsLostGear, Is.False);
            Assert.That(_hud.Replay.ShowsPrize, Is.True);
            Assert.That(Ui.IsShown(_hud.Replay.Return), Is.True);
            Assert.That(Ui.IsShown(_hud.Replay.Pause), Is.False);

            _hud.ShowResult(BattleOutcome.EnemyVictory, 0, 2, 1);
            Assert.That(_hud.Replay.Verdict, Is.EqualTo("Поражение"));
            Assert.That(_hud.Replay.ShowsLostGear, Is.True);
            Assert.That(_hud.Replay.ShowsPrize, Is.False);

            // what the defeat cost: the stake, the level's longer rest and the top of the ladder closed again
            _hud.ShowResult(BattleOutcome.EnemyVictory, 0, 2, 1, new BattleCost(60, 4, 3, 240000, false));
            Assert.That(_hud.Replay.BurnedStake, Is.EqualTo("−60"));
            Assert.That(_hud.Replay.Rest, Is.EqualTo(TopBar.Duration(240000)));
            Assert.That(_hud.Replay.Closed, Is.EqualTo("Уровень 4 закрыт до победы на 3-м"));
            _hud.ShowResult(BattleOutcome.Draw, 1, 0, 0, new BattleCost(60, 0, 3, 120000, true));
            Assert.That(_hud.Replay.Closed, Is.Null, "A draw closes nothing");
            Assert.That(_hud.Replay.ShowsPrize, Is.True, "A draw's share waits in the colony");
            _hud.ShowResult(BattleOutcome.PlayerVictory, 2, 0, 0, new BattleCost(0, 0, 3, 120000, false));
            Assert.That(_hud.Replay.BurnedStake, Is.Null, "A win keeps the stake");
            Assert.That(_hud.Replay.Rest, Is.Null);
            Assert.That(_hud.Replay.ShowsPrize, Is.False, "A repeat win from an empty fund leaves nothing to take");
        }

        private Cell FreeCell() => _mission.PlayerDeployment.First(cell =>
            _board.CanPlace(cell) && _deployment.UnitAt(cell) == null);
    }
}
