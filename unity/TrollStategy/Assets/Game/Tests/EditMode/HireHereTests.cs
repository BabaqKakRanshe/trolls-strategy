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
    /// <summary>
    /// Hiring straight into a workplace: one order hires the building's best worker for the price and sends it
    /// there (the bots give it). Building cards do not offer it: creatures are hired in the catalog.
    /// </summary>
    public class HireHereTests
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
                new StartingBuilding(BuildingKind.Barracks, new Cell(2, 8)),
                new StartingBuilding(BuildingKind.Mine, new Cell(3, 2))));
            _interaction = new InteractionController(_session);
            _hud = TestUi.Colony(_session, _interaction);
        }

        private BuildingSnapshot Mine => _session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine);
        private UnitKind Worker => _session.SuggestedWorker(BuildingKind.Mine).Value;

        private CommandResult HireAtMine() =>
            _session.Dispatch(new HireWorkerCommand(Worker, Mine.Id, _session.FindSpawnCell()));

        [Test]
        public void HireWorker_HiresOneCreature_AndSendsItToTheBuilding()
        {
            int gold = _session.CurrentSnapshot.Gold;
            int price = _session.HirePrice(Worker);

            var result = HireAtMine();

            Assert.That(result.Ok, Is.True, result.Error);
            var unit = _session.CurrentSnapshot.Units.Single();
            Assert.That(unit.UnitKind, Is.EqualTo(Worker));
            Assert.That(unit.Assignment.Kind, Is.EqualTo(AssignmentKind.ToWork));
            Assert.That(unit.Assignment.BuildingId, Is.EqualTo(Mine.Id));
            Assert.That(_session.CurrentSnapshot.Gold, Is.EqualTo(gold - price));
            Assert.That(Mine.WorkerCount, Is.EqualTo(1));
        }

        [Test]
        public void HireWorker_IsRefusedWhole_WhenTheBuildingHasNoFreePlace()
        {
            for (int i = 0; i < Mine.MaxWorkers; i++)
                Assert.That(HireAtMine().Ok, Is.True, $"worker {i + 1}");
            int units = _session.CurrentSnapshot.Units.Count;
            int gold = _session.CurrentSnapshot.Gold;

            var result = HireAtMine();

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo("Свободных мест для рабочих нет"));
            Assert.That(_session.CurrentSnapshot.Units.Count, Is.EqualTo(units), "nobody is hired");
            Assert.That(_session.CurrentSnapshot.Gold, Is.EqualTo(gold), "no gold is spent");
        }

        [Test]
        public void HireWorker_IsRefused_ForABuildingWithoutWorkers()
        {
            var warehouse = _session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Warehouse);

            var result = _session.Dispatch(new HireWorkerCommand(UnitKind.Goblin, warehouse.Id, _session.FindSpawnCell()));

            Assert.That(result.Ok, Is.False);
            Assert.That(_session.CurrentSnapshot.Units, Is.Empty);
        }

        [Test]
        public void SuggestedWorker_GivesTheMostWorkPerHead_AmongTheWorthTheirPrice()
        {
            var candidates = _catalog.Units.Where(u => u != null && u.Hireable && _session.IsUnitUnlocked(u.Kind)).ToList();
            float PerGold(UnitDefinition u) => HireAdvice.Work(u, BuildingKind.Mine) / _session.HirePrice(u.Kind);
            float best = candidates.Max(PerGold);
            var suggested = _catalog.GetUnit(Worker);

            Assert.That(PerGold(suggested), Is.GreaterThanOrEqualTo(best * HireAdvice.CardShare));
            foreach (var other in candidates.Where(u => PerGold(u) >= best * HireAdvice.CardShare))
                Assert.That(HireAdvice.Work(suggested, BuildingKind.Mine), Is.GreaterThanOrEqualTo(HireAdvice.Work(other, BuildingKind.Mine)),
                    other.DisplayName);
        }

        [Test]
        public void BuildingCards_OfferNoHire()
        {
            foreach (var kind in new[] { BuildingKind.Mine, BuildingKind.Warehouse, BuildingKind.Barracks })
            {
                _interaction.SelectBuilding(_session.CurrentSnapshot.Buildings.First(b => b.Kind == kind).Id);
                Refresh();

                Assert.That(_hud.Inspect.Actions.Where(Ui.IsShown).Select(Title).Any(t => t.StartsWith("Нанять")), Is.False,
                    kind.ToString());
            }
        }

        private void Refresh() => _hud.OnInteractionChanged(_session.CurrentSnapshot);

        private static string Title(Button button) => button.Q<Label>(className: "btn__title").text;

    }
}
