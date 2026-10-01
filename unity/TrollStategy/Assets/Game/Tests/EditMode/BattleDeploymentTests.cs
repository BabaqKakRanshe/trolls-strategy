using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>The deployment's rules as the reserve's picks, the board's clicks and drops use them.</summary>
    public class BattleDeploymentTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameSession _session;
        private BattleMissionDefinition _mission;
        private BattleBoard _board;
        private BattleDeployment _deployment;

        [SetUp]
        public void SetUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            _session = TestColony.NewSession(catalog);
            Assert.That(_session.DebugAddGold(1000).Ok, Is.True);
            Assert.That(_session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, _session.FindSpawnCell())).Ok, Is.True);
            Assert.That(_session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, _session.FindSpawnCell())).Ok, Is.True);
            _mission = catalog.Missions.First(mission => mission != null);
            _board = _mission.CreateBoard();
            _deployment = new BattleDeployment(_session, _mission, _board);
            Assume.That(_deployment.SquadLimit, Is.GreaterThanOrEqualTo(3), "the tests place three fighters");
        }

        [Test]
        public void TheReserve_HoldsEveryKind_AndTheFirstOneIsPicked()
        {
            Assert.That(_deployment.Kinds, Is.EqualTo(new[] { UnitKind.Goblin, UnitKind.Troll }));
            Assert.That(_deployment.ReserveOf(UnitKind.Goblin), Is.EqualTo(Count(UnitKind.Goblin)));
            Assert.That(_deployment.ReserveOf(UnitKind.Troll), Is.EqualTo(Count(UnitKind.Troll)));
            Assert.That(_deployment.PickedKind, Is.EqualTo(UnitKind.Goblin));
            Assert.That(_deployment.Message, Is.EqualTo("Нажми синюю клетку: встанет гоблин"));
        }

        [Test]
        public void AClickOnAFreeBlueCell_BringsAFighterOfThePickedKind_AndSelectsIt()
        {
            Assert.That(_deployment.Pick(UnitKind.Troll), Is.EqualTo(DeploymentResult.Selected));
            Assert.That(_deployment.Message, Is.EqualTo("Нажми синюю клетку: встанет тролль"));
            var cell = FreeCell();

            Assert.That(_deployment.ClickCell(cell), Is.EqualTo(DeploymentResult.Placed));

            string trollId = _deployment.UnitAt(cell);
            Assert.That(_deployment.UnitKinds[trollId], Is.EqualTo(UnitKind.Troll));
            Assert.That(_deployment.SelectedUnitId, Is.EqualTo(trollId));
            Assert.That(_deployment.ReserveOf(UnitKind.Troll), Is.EqualTo(Count(UnitKind.Troll) - 1));
            Assert.That(_deployment.CanStart, Is.True);
        }

        [Test]
        public void WhenThePickedKindRunsOut_TheNextKindInTheReserveIsPicked()
        {
            _deployment.Pick(UnitKind.Troll);
            for (int i = 0; i < Count(UnitKind.Troll); i++) _deployment.ClickCell(FreeCell());

            Assert.That(_deployment.PickedKind, Is.EqualTo(UnitKind.Goblin));
            Assert.That(_deployment.Pick(UnitKind.Troll), Is.EqualTo(DeploymentResult.Refused));
            Assert.That(_deployment.Message, Is.EqualTo("Тролль: все уже на поле"));
        }

        [Test]
        public void ADraggedKind_TakesAFreeCell_AndBecomesThePick()
        {
            var cell = FreeCell();

            Assert.That(_deployment.PlaceKind(UnitKind.Troll, cell), Is.EqualTo(DeploymentResult.Placed));

            string trollId = _deployment.UnitAt(cell);
            Assert.That(_deployment.UnitKinds[trollId], Is.EqualTo(UnitKind.Troll));
            Assert.That(_deployment.SelectedUnitId, Is.EqualTo(trollId));
            if (Count(UnitKind.Troll) == 1)
                Assert.That(_deployment.PickedKind, Is.EqualTo(UnitKind.Goblin), "the only troll is on the board");
        }

        [Test]
        public void ADraggedKind_DroppedOnAnotherKind_SendsThatOneBackWithoutItsGear()
        {
            var cell = FreeCell();
            _deployment.ClickCell(cell);
            string goblinId = _deployment.UnitAt(cell);
            string itemId = _deployment.Equipment.Count > 0 ? _deployment.Equipment[0].Id : null;
            if (itemId != null) _deployment.ToggleEquipment(itemId);

            Assert.That(_deployment.PlaceKind(UnitKind.Troll, cell), Is.EqualTo(DeploymentResult.Placed));

            Assert.That(_deployment.UnitKinds[_deployment.UnitAt(cell)], Is.EqualTo(UnitKind.Troll));
            Assert.That(_deployment.IsPlaced(goblinId), Is.False);
            Assert.That(_deployment.Placements.Count, Is.EqualTo(1));
            if (itemId != null) Assert.That(_deployment.OwnerOf(itemId), Is.Null);
        }

        [Test]
        public void ADraggedKind_WithNobodyLeft_OrOffTheBlueCells_IsRefused()
        {
            while (_deployment.ReserveOf(UnitKind.Troll) > 0) _deployment.PlaceKind(UnitKind.Troll, FreeCell());

            Assert.That(_deployment.PlaceKind(UnitKind.Troll, FreeCell()), Is.EqualTo(DeploymentResult.Refused));
            Assert.That(_deployment.Message, Is.EqualTo("Тролль: все уже на поле"));
            Assert.That(_deployment.PlaceKind(UnitKind.Goblin, EnemyCell()), Is.EqualTo(DeploymentResult.Refused));
            Assert.That(_deployment.Message, Is.EqualTo("Бойцов ставят на синие клетки"));
        }

        [Test]
        public void AFullSquad_ChangesWhoItBrings_WhenAKindIsDroppedOnAFighter()
        {
            Assert.That(_session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, _session.FindSpawnCell())).Ok, Is.True);
            _deployment = new BattleDeployment(_session, _mission, _board);
            Assume.That(Count(UnitKind.Goblin), Is.GreaterThanOrEqualTo(_mission.MaxPlayerUnits));
            _deployment.Pick(UnitKind.Goblin);
            while (_deployment.Placements.Count < _mission.MaxPlayerUnits) _deployment.ClickCell(FreeCell());

            Assert.That(_deployment.PlaceKind(UnitKind.Troll, FreeCell()), Is.EqualTo(DeploymentResult.Refused));
            Assert.That(_deployment.Message, Is.EqualTo($"В отряде не больше {_mission.MaxPlayerUnits} бойцов"));

            var cell = _deployment.Placements[0].Cell;
            Assert.That(_deployment.PlaceKind(UnitKind.Troll, cell), Is.EqualTo(DeploymentResult.Placed));
            Assert.That(_deployment.UnitKinds[_deployment.UnitAt(cell)], Is.EqualTo(UnitKind.Troll));
            Assert.That(_deployment.Placements.Count, Is.EqualTo(_mission.MaxPlayerUnits));
        }

        [Test]
        public void AClickOnAPlacedFighter_SelectsIt_AndASecondClickLetsItGo()
        {
            var first = FreeCell();
            _deployment.ClickCell(first);
            _deployment.ClickCell(FreeCell());

            Assert.That(_deployment.ClickCell(first), Is.EqualTo(DeploymentResult.Selected));
            Assert.That(_deployment.SelectedUnitId, Is.EqualTo(_deployment.UnitAt(first)));
            Assert.That(_deployment.ClickCell(first), Is.EqualTo(DeploymentResult.Deselected));
            Assert.That(_deployment.SelectedUnitId, Is.Null);
            Assert.That(_deployment.Placements.Count, Is.EqualTo(2), "selecting never places or removes");
        }

        [Test]
        public void ClicksPastTheSquad_AreRefusedWithTheReason()
        {
            for (int i = 0; i < _deployment.SquadLimit; i++) _deployment.ClickCell(FreeCell());
            Assert.That(_deployment.CanPlaceMore, Is.False);

            Assert.That(_deployment.ClickCell(FreeCell()), Is.EqualTo(DeploymentResult.Refused));

            Assert.That(_deployment.Message, Is.EqualTo(_deployment.Roster.Count >= _mission.MaxPlayerUnits
                ? $"В отряде не больше {_mission.MaxPlayerUnits} бойцов"
                : "Все бойцы уже на поле"));
            Assert.That(_deployment.Placements.Count, Is.EqualTo(_deployment.SquadLimit));
        }

        [Test]
        public void AClickOffTheBlueCells_IsRefusedWithTheReason()
        {
            Assert.That(_deployment.ClickCell(EnemyCell()), Is.EqualTo(DeploymentResult.Refused));

            Assert.That(_deployment.Message, Is.EqualTo("Бойцов ставят на синие клетки"));
            Assert.That(_deployment.Placements, Is.Empty);
        }

        [Test]
        public void ADropOnAFreeBlueCell_MovesTheFighter()
        {
            _deployment.ClickCell(FreeCell());
            string unitId = _deployment.Placements[0].UnitId;
            _deployment.Deselect();
            var target = FreeCell();

            Assert.That(_deployment.Drop(unitId, target), Is.EqualTo(DeploymentResult.Moved));

            Assert.That(_deployment.CellOf(unitId), Is.EqualTo(target));
            Assert.That(_deployment.SelectedUnitId, Is.EqualTo(unitId), "the fighter in hand stays selected");
            Assert.That(_deployment.Placements.Count, Is.EqualTo(1));
        }

        [Test]
        public void ADropOnAnotherFighter_SwapsTheirCells()
        {
            var first = FreeCell();
            _deployment.ClickCell(first);
            var second = FreeCell();
            _deployment.ClickCell(second);
            string a = _deployment.UnitAt(first), b = _deployment.UnitAt(second);

            Assert.That(_deployment.Drop(a, second), Is.EqualTo(DeploymentResult.Moved));

            Assert.That(_deployment.UnitAt(second), Is.EqualTo(a));
            Assert.That(_deployment.UnitAt(first), Is.EqualTo(b));
        }

        [Test]
        public void ADropOffTheBlueCells_IsRefused_AndTheFighterStays()
        {
            var cell = FreeCell();
            _deployment.ClickCell(cell);
            string unitId = _deployment.UnitAt(cell);

            Assert.That(_deployment.Drop(unitId, EnemyCell()), Is.EqualTo(DeploymentResult.Refused));

            Assert.That(_deployment.CellOf(unitId), Is.EqualTo(cell));
            Assert.That(_deployment.Message, Is.EqualTo("Бойцов ставят на синие клетки"));
        }

        [Test]
        public void ADropOffTheBoard_SendsTheFighterBackToTheReserve_AndItsGearToTheInventory()
        {
            _deployment.Pick(UnitKind.Troll);
            _deployment.ClickCell(FreeCell());
            string unitId = _deployment.Placements[0].UnitId;
            string itemId = _deployment.Equipment.Count > 0 ? _deployment.Equipment[0].Id : null;
            if (itemId != null)
                Assert.That(_deployment.ToggleEquipment(itemId), Is.EqualTo(EquipmentChange.Equipped));

            Assert.That(_deployment.Drop(unitId, null), Is.EqualTo(DeploymentResult.Removed));

            Assert.That(_deployment.IsPlaced(unitId), Is.False);
            Assert.That(_deployment.ReserveOf(UnitKind.Troll), Is.EqualTo(Count(UnitKind.Troll)));
            Assert.That(_deployment.SelectedUnitId, Is.Null);
            if (itemId != null) Assert.That(_deployment.OwnerOf(itemId), Is.Null);
        }

        [Test]
        public void Gear_GoesOnlyToTheSelectedFighter()
        {
            if (_deployment.Equipment.Count == 0) Assert.Ignore("The catalog starts the colony without gear");
            string itemId = _deployment.Equipment[0].Id;
            Assert.That(_deployment.ToggleEquipment(itemId), Is.EqualTo(EquipmentChange.None), "nobody is selected");

            _deployment.ClickCell(FreeCell());
            Assert.That(_deployment.ToggleEquipment(itemId), Is.EqualTo(EquipmentChange.Equipped));
            Assert.That(_deployment.OwnerOf(itemId), Is.EqualTo(_deployment.SelectedUnitId));

            // the next fighter takes it from the first
            _deployment.ClickCell(FreeCell());
            Assert.That(_deployment.ToggleEquipment(itemId), Is.EqualTo(EquipmentChange.Equipped));
            Assert.That(_deployment.OwnerOf(itemId), Is.EqualTo(_deployment.SelectedUnitId));
        }

        private int Count(UnitKind kind) => _deployment.Roster.Count(unit => unit.UnitKind == kind);

        private Cell FreeCell() => _mission.PlayerDeployment.First(cell =>
            _board.CanPlace(cell) && _deployment.UnitAt(cell) == null);

        private Cell EnemyCell() => _mission.EnemyDeployment.First(cell => !_board.IsBlocked(cell));
    }
}
