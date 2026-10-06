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
    /// <summary>The campaign as the colony HUD shows it: the quest card, closed catalog cards and the reward reveal.</summary>
    public class ColonyHudProgressionTests
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
            Assert.That(_catalog.Progression, Is.Not.Null, "The catalog must link Progression.asset");
            _session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            _interaction = new InteractionController(_session);
            _hud = TestUi.Colony(_session, _interaction);
        }

        [Test]
        public void QuestCard_ShowsTheFirstTutorialStep()
        {
            Assert.That(_hud.Quest.IsShown, Is.True);
            Assert.That(_hud.Quest.Chapter, Does.StartWith("Обучение, уровень 1 из"));
            Assert.That(_hud.Quest.Title, Is.EqualTo(_catalog.Progression.Quests[0].Title));
            Assert.That(_hud.Quest.GoalLines, Is.EqualTo(new[] { "Нанять: гоблин 0/1" }));
            Assert.That(UiFeel.IsAvailable(_hud.Quest.ClaimButton), Is.True);
            Assert.That(Ui.IsShown(_hud.Quest.ClaimButton), Is.False, "Nothing to take yet");
            Assert.That(_interaction.Message, Does.Contain(_catalog.Progression.Quests[0].Title));
        }

        [Test]
        public void QuestCard_FoldsToItsTitleAndCount_KeepsTheReward_AndOpensForTheNextQuest()
        {
            var body = _hud.Root.Q("quest-body");
            _hud.Quest.ToggleCollapsed();

            Assert.That(_hud.Quest.IsCollapsed, Is.True);
            Assert.That(Ui.IsShown(body), Is.False, "Folded, the card keeps only its heading and title");
            Assert.That(_hud.Quest.ToggleCaption, Is.EqualTo("Развернуть"), "The button says what it will do");
            Assert.That(_hud.Quest.Title, Is.EqualTo(_catalog.Progression.Quests[0].Title));
            Assert.That(_hud.Quest.Progress, Is.EqualTo("0/1"));

            BuyGoblin();
            Refresh();
            Assert.That(_hud.Quest.Progress, Is.EqualTo("1/1"), "A folded card still counts the goals");
            Assert.That(Ui.IsShown(_hud.Quest.ClaimButton), Is.True, "The reward is taken from a folded card too");

            _hud.Tick(5f);
            UiFeel.Press(_hud.Reward.ClaimButton);
            Refresh();
            Assert.That(_hud.Quest.Title, Is.EqualTo(_catalog.Progression.Quests[1].Title));
            Assert.That(_hud.Quest.IsCollapsed, Is.False, "A new quest opens so its steps are read");
            Assert.That(Ui.IsShown(body), Is.True);
            Assert.That(_hud.Quest.ToggleCaption, Is.EqualTo("Свернуть"));
        }

        [Test]
        public void QuestCard_FoldsForABuildingCard_AndOpensWhenThePlayerAsks()
        {
            var building = _session.CurrentSnapshot.Buildings.First();
            _interaction.SelectBuilding(building.Id);
            Refresh();
            Assert.That(_hud.Inspect.IsShown, Is.True);
            Assert.That(_hud.Quest.IsCollapsed, Is.True, "The building card takes the column");

            _hud.Quest.ToggleCollapsed();
            Refresh();
            Refresh();
            Assert.That(_hud.Quest.IsCollapsed, Is.False, "The player opened it: the next refresh must not fold it again");

            _hud.Quest.ToggleCollapsed();
            _interaction.CloseInspect();
            Refresh();
            Assert.That(_hud.Inspect.IsShown, Is.False);
            Assert.That(_hud.Quest.IsCollapsed, Is.True, "Folded by the player, it stays folded");

            _hud.Quest.ToggleCollapsed();
            _interaction.SelectBuilding(building.Id);
            Refresh();
            _interaction.CloseInspect();
            Refresh();
            Assert.That(_hud.Quest.IsCollapsed, Is.False, "Folded for the card, it opens again when the card closes");
        }

        [Test]
        public void Catalog_OpensOnCreatures_PointsAtTheGoblin_AndKeepsClosedThingsShut()
        {
            Assert.That(_hud.Catalog.ShowsUnits, Is.True);
            Assert.That(_hud.Catalog.IsSuggested(UnitKind.Goblin), Is.True);
            Assert.That(UiFeel.IsAvailable(_hud.Catalog.HireButton(UnitKind.Goblin)), Is.True);
            Assert.That(UiFeel.IsAvailable(_hud.Catalog.HireButton(UnitKind.Troll)), Is.False);

            Assert.That(_hud.Catalog.IsListed(BuildingKind.Mine), Is.True, "The next building to open is shown");
            Assert.That(_hud.Catalog.IsListed(BuildingKind.Smeltery), Is.False, "Later buildings stay out of the list");
            Assert.That(UiFeel.IsAvailable(_hud.Catalog.BuyButton(BuildingKind.Mine)), Is.False);

            UiFeel.Press(_hud.Catalog.HireButton(UnitKind.Troll));
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
            _interaction.BeginBuildingPlacement(BuildingKind.Mine);
            Assert.That(_interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral), "A closed building cannot be placed");
        }

        [Test]
        public void BattleButton_SaysAtWhichLevelTheBattleOpens()
        {
            int level = _session.CurrentSnapshot.Progress.MissionUnlockLevel("mission-1");
            Assert.That(level, Is.GreaterThan(1));
            Assert.That(_hud.TopBar.BattleText, Is.EqualTo($"Бой с {level} уровня"));
            Assert.That(UiFeel.IsAvailable(_hud.TopBar.BattleButton), Is.False);
        }

        [Test]
        public void FinishedStep_OpensTheRevealWhichLandsOnTheRewardAndClaimsIt()
        {
            BuyGoblin();
            Refresh();

            Assert.That(_hud.Reward.IsOpen, Is.True, "A finished quest opens its reveal at once");
            Assert.That(_hud.Reward.IsReady, Is.False, "The reveal plays first");
            Assert.That(_hud.Quest.GoalLines, Is.EqualTo(new[] { "Нанять: гоблин 1/1" }));
            Assert.That(Ui.IsShown(_hud.Quest.ClaimButton), Is.True);

            UiFeel.Press(_hud.Reward.ClaimButton);
            Assert.That(_session.CurrentSnapshot.Progress.Level, Is.EqualTo(1), "A press during the reveal only skips ahead");
            _hud.Tick(5f);

            Assert.That(_hud.Reward.IsReady, Is.True);
            Assert.That(_hud.Reward.RewardName, Is.EqualTo(_catalog.GetBuilding(BuildingKind.Mine).DisplayName));
            Assert.That(_hud.Reward.RewardKind, Is.EqualTo("Новая постройка"));

            UiFeel.Press(_hud.Reward.ClaimButton);
            Refresh();
            _hud.Tick(1f);

            Assert.That(_session.IsBuildingUnlocked(BuildingKind.Mine), Is.True);
            Assert.That(_hud.Reward.IsOpen, Is.False);
            Assert.That(_hud.Quest.Title, Is.EqualTo(_catalog.Progression.Quests[1].Title));
            Assert.That(_hud.Catalog.ShowsUnits, Is.False, "The next step builds, so the drawer turns to buildings");
            Assert.That(_hud.Catalog.IsSuggested(BuildingKind.Mine), Is.True);
            Assert.That(UiFeel.IsAvailable(_hud.Catalog.BuyButton(BuildingKind.Mine)), Is.True);
        }

        [Test]
        public void Reveal_WaitsForAPlacementToFinishAndForTheHudToShow()
        {
            _hud.SetHudVisible(false);
            BuyGoblin();
            Refresh();
            Assert.That(_hud.Reward.IsOpen, Is.False, "No reveal while the battle scene covers the colony");
            Assert.That(_hud.Quest.GoalLines, Is.EqualTo(new[] { "Нанять: гоблин 0/1" }),
                "A battle's result must not tick the hidden card and give the outcome away");

            _hud.SetHudVisible(true);
            Assert.That(_hud.Quest.GoalLines, Is.EqualTo(new[] { "Нанять: гоблин 1/1" }));
            Assert.That(_hud.Reward.IsOpen, Is.True);
            _hud.Reward.Hide();

            _interaction.BeginUnitPlacement(UnitKind.Goblin, 1);
            Refresh();
            Assert.That(_hud.Reward.IsOpen, Is.False, "No reveal in the middle of a placement");

            UiFeel.Press(_hud.Quest.ClaimButton);
            Assert.That(_hud.Reward.IsOpen, Is.True, "The card's button opens it anyway");
        }

        [Test]
        public void OrderStep_PointsAtTheCommandAndTheBuilding()
        {
            ClaimCurrent(() => BuyGoblin());
            ClaimCurrent(() =>
            {
                var cell = _session.FindFirstBuildingCell(BuildingKind.Mine);
                Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, cell.Value)).Ok, Is.True);
            });
            Refresh();
            Assert.That(_hud.Catalog.IsSuggested(UnitKind.Goblin), Is.False, "Any creature digs: the goblin at hand will do");

            string goblin = _session.CurrentSnapshot.Units.Last(u => u.UnitKind == UnitKind.Goblin).Id;
            _interaction.ClickUnit(goblin, false);
            Refresh();

            Assert.That(_hud.Focus.WorkTarget, Is.EqualTo(BuildingKind.Mine));
            Assert.That(_hud.ContextBar.WorkButton.ClassListContains("is-suggested"), Is.True);
            Assert.That(_hud.ContextBar.HaulButton.ClassListContains("is-suggested"), Is.False);

            UiFeel.Press(_hud.ContextBar.WorkButton);
            Refresh();
            var mineChip = _hud.ContextBar.TargetButtons.Single();
            Assert.That(mineChip.ClassListContains("is-suggested"), Is.True);
        }

        [Test]
        public void SceneBuildings_OfferNoDemolition()
        {
            _interaction.SelectBuilding("warehouse-1");
            Refresh();

            var demolish = _hud.Inspect.Actions.Last();
            Assert.That(demolish.Q<Label>(className: "btn__title").text, Is.EqualTo("Снести"));
            Assert.That(UiFeel.IsAvailable(demolish), Is.False, "The warehouse cannot be built again");
            UiFeel.Press(demolish);
            Assert.That(_session.CurrentSnapshot.Buildings.Any(b => b.Id == "warehouse-1"), Is.True);
        }

        [Test]
        public void SandboxHud_HasNoQuestCard()
        {
            var session = TestColony.NewSession(_catalog);
            var hud = TestUi.Colony(new ColonyHudContext(session, new InteractionController(session)));

            Assert.That(hud.Quest.IsShown, Is.False);
            Assert.That(hud.Reward.IsOpen, Is.False);
            Assert.That(hud.Catalog.ShowsUnits, Is.False, "A sandbox starts on buildings, mine first");
            Assert.That(UiFeel.IsAvailable(hud.Catalog.HireButton(UnitKind.Troll)), Is.True);
        }

        [Test]
        public void Reveal_ShowsANewBuildingAsFacts_AndWhereToFindIt()
        {
            BuyGoblin();
            Refresh();
            _hud.Tick(5f);

            var mine = _catalog.GetBuilding(BuildingKind.Mine);
            var recipe = mine.Recipes[0];
            string Words(ResourceAmount amount) => $"{amount.Amount} {_catalog.GetResource(amount.Resource).DisplayName}";
            var parts = recipe.Inputs.Select(Words).ToList();
            if (recipe.Inputs.Length > 0) parts.Add("→");
            parts.AddRange(recipe.Outputs.Select(Words));

            Assert.That(_hud.Reward.Facts[0], Is.EqualTo($"{mine.Width}×{mine.Height}"));
            Assert.That(_hud.Reward.Facts, Does.Contain($"до {mine.MaxWorkers} рабочих"));
            Assert.That(_hud.Reward.Facts, Does.Contain(string.Join(" ", parts)), "The recipe as pictures and numbers");
            Assert.That(_hud.Reward.Facts, Does.Contain(_catalog.GetBuilding(BuildingKind.Smeltery).DisplayName),
                "Where the ore goes: the smeltery takes it");
            Assert.That(_hud.Reward.Facts, Does.Contain(_catalog.GetBuilding(BuildingKind.Market).DisplayName),
                "and the market buys it");
            Assert.That(_hud.Reward.Where,
                Is.EqualTo($"Уже в каталоге, вкладка «Здания», {_session.BuildingPrice(BuildingKind.Mine)} золота"));
        }

        private void Refresh() => _hud.OnInteractionChanged(_session.CurrentSnapshot);

        private void BuyGoblin()
        {
            var result = _session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, _session.FindSpawnCell()));
            Assert.That(result.Ok, Is.True, result.Error);
        }

        // Does what the current quest asks, then takes its reward through the reveal.
        private void ClaimCurrent(System.Action complete)
        {
            complete();
            Refresh();
            Assert.That(_hud.Reward.IsOpen, Is.True);
            _hud.Tick(5f);
            UiFeel.Press(_hud.Reward.ClaimButton);
            _hud.Tick(1f);
            Assert.That(_hud.Reward.IsOpen, Is.False);
        }
    }
}
