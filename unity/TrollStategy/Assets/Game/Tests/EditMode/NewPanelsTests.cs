using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>The arena ladder, the pause menu, the guild's upgrades and the grouped gear, over the real layout.</summary>
    public class NewPanelsTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private InteractionController _interaction;

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
        }

        [Test]
        public void ArenaPoster_ShowsEveryOpenLevel_AndAChosenReadyLevelOpensItsBattle()
        {
            BattleMissionDefinition opened = null;
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction) { OpenBattle = mission => opened = mission });
            UiFeel.Press(hud.TopBar.BattleButton);

            Assert.That(hud.Arena.IsOpen, Is.True, "The battle tool opens the arena");
            Assert.That(hud.BlocksMap, Is.True);
            var levels = hud.Arena.LevelButtons;
            Assert.That(levels, Has.Count.EqualTo(_catalog.Missions.Count), "A sandbox has every level open");

            UiFeel.Press(levels[0]);
            Assert.That(hud.Arena.Chosen.Level, Is.EqualTo(1), "A disc chooses its level");
            Assert.That(levels[0].ClassListContains("is-on"), Is.True);
            Assert.That(UiFeel.IsAvailable(hud.Arena.FightButton), Is.False, "The first level opens after two minutes of colony time");

            UiFeel.Press(levels[1]);
            Assert.That(hud.Arena.Chosen.Level, Is.EqualTo(2));
            Assert.That(levels[0].ClassListContains("is-on"), Is.False, "One level is chosen at a time");
            Assert.That(UiFeel.IsAvailable(hud.Arena.FightButton), Is.True);

            UiFeel.Press(hud.Arena.FightButton);

            Assert.That(opened, Is.Not.Null);
            Assert.That(opened.Level, Is.EqualTo(2));
            Assert.That(hud.Arena.IsOpen, Is.False);
        }

        [Test]
        public void ArenaRail_ShowsTheWholeLadder_TheNextLevelLocked_AndTheRoadBeyondFaded()
        {
            var campaign = new GameSession(_catalog, TestColony.LayoutFor(_catalog,
                new StartingBuilding(BuildingKind.Warehouse, new Cell(10, 8)),
                new StartingBuilding(BuildingKind.Market, new Cell(10, 2)),
                new StartingBuilding(BuildingKind.Barracks, new Cell(2, 8))), campaign: true);
            var hud = TestUi.Colony(new ColonyHudContext(campaign, new InteractionController(campaign)) { OpenBattle = _ => { } });
            hud.Arena.Open();
            var ladder = campaign.ArenaLadder();
            Assume.That(campaign.IsMissionUnlocked(ladder[0].MissionId), Is.False, "A new campaign has no battle open");

            Assert.That(hud.Arena.LevelButtons, Has.Count.EqualTo(ladder.Count), "Every level of the ladder is on the rail");
            Assert.That(hud.Arena.HasLock(ladder[0]), Is.True, "The next level to open carries the lock");
            Assert.That(hud.Arena.IsFaded(ladder[0]), Is.False);
            Assert.That(hud.Arena.HasLock(ladder[1]), Is.False);
            Assert.That(hud.Arena.IsFaded(ladder[1]), Is.True, "The road beyond it fades");
            var hiring = ladder.First(m => m.UnlockUnit.HasValue);
            Assert.That(hud.Arena.ShowsFolk(hiring), Is.True, "A level that opens a folk for hire shows it");
            Assert.That(hud.Arena.ShowsFolk(ladder[0]), Is.False);

            UiFeel.Press(hud.Arena.LevelButtons[hiring.Level - 1]);
            Assert.That(hud.Arena.Chosen, Is.SameAs(hiring), "A closed level can be looked at");
            Assert.That(UiFeel.IsAvailable(hud.Arena.FightButton), Is.False, "but not fought");
        }

        [Test]
        public void ArenaPoster_ShowsTheSquadAgainstTheEnemy_TheGold_AndTheFolkAFirstWinHires()
        {
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction) { OpenBattle = _ => { } });
            hud.Arena.Open();
            var hiring = _session.ArenaLadder().First(m => m.UnlockUnit.HasValue);

            UiFeel.Press(hud.Arena.LevelButtons[hiring.Level - 1]);

            Assert.That(hud.Arena.Chosen, Is.SameAs(hiring));
            Assert.That(hud.Arena.SquadPlaces, Is.EqualTo(_session.SquadLimit(hiring)), "A place for each fighter the level takes");
            int kinds = hiring.Enemies.Select(enemy => enemy.Kind).Distinct().Count();
            Assert.That(hud.Arena.EnemyKindsShown, Is.EqualTo(System.Math.Min(4, kinds)), "A disc for each kind of enemy");
            var (min, max) = _session.WinGold(hiring);
            Assert.That(hud.Arena.Gold, Is.EqualTo($"{min}–{max}"), "The gold as a number, beside the coin");
            Assert.That(hud.Arena.Hire, Is.EqualTo(_catalog.TryGetUnit(hiring.UnlockUnit.Value).DisplayName),
                "The folk a first win opens for hire");
        }

        [Test]
        public void Catalog_TurnsWholePagesOfEight_AndListingDoesNotDependOnThePage()
        {
            var hud = TestUi.Colony(_session, _interaction);
            hud.Refresh(_session.CurrentSnapshot);
            var pages = hud.Catalog.BuildingPages;
            var kinds = _catalog.Buildings.Where(b => b != null && b.Constructible).Select(b => b.Kind).ToList();
            Assume.That(kinds.Count, Is.GreaterThan(CatalogPager.MaxPerPage), "More buildings than one page holds");

            Assert.That(pages.PerPage, Is.EqualTo(CatalogPager.MaxPerPage));
            Assert.That(pages.PageCount, Is.EqualTo((kinds.Count + 7) / 8));
            Assert.That(Ui.IsShown(pages.NextButton), Is.True);
            Assert.That(UiFeel.IsAvailable(pages.PrevButton), Is.False, "Nothing before the first page");
            Assert.That(kinds.Take(8).All(hud.Catalog.IsOnPage), Is.True, "The first eight stand on page one");
            Assert.That(hud.Catalog.IsOnPage(kinds.Last()), Is.False);
            Assert.That(hud.Catalog.IsListed(kinds.Last()), Is.True, "Off the page is not out of the catalog");

            UiFeel.Press(pages.NextButton);

            Assert.That(pages.Page, Is.EqualTo(1));
            Assert.That(hud.Catalog.IsOnPage(kinds.Last()), Is.True);
            Assert.That(hud.Catalog.IsOnPage(kinds.First()), Is.False);
            Assert.That(UiFeel.IsAvailable(pages.PrevButton), Is.True);
            hud.Refresh(_session.CurrentSnapshot);
            Assert.That(pages.Page, Is.EqualTo(1), "A refresh keeps the page the player turned to");
        }

        [Test]
        public void Book_ListsTheContent_FindsAcrossSections_AndStopsTheColony()
        {
            var paused = new List<bool>();
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction) { SetPaused = paused.Add });
            Assert.That(hud.Wiki, Is.Not.Null, "The UI prefab carries the book: run TrollStrategy/Dev/Setup UI");
            hud.Refresh(_session.CurrentSnapshot);
            Assert.That(Ui.IsShown(hud.TopBar.WikiButton.parent), Is.True, "The book tool shows once the HUD has a book");

            UiFeel.Press(hud.TopBar.WikiButton);
            var book = hud.Wiki;
            Assert.That(book.IsOpen, Is.True);
            Assert.That(paused, Is.EqualTo(new[] { true }), "The colony stands still while the book is open");
            Assert.That(hud.BlocksMap, Is.True);
            Assert.That(book.Section, Is.EqualTo(WikiSection.Creatures));
            var hireable = _catalog.Units.Where(u => u != null && u.Hireable).Select(u => u.DisplayName).ToList();
            Assert.That(book.RowNames, Is.EqualTo(hireable));

            var dwarf = _catalog.GetUnit(UnitKind.Dwarf);
            UiFeel.Press(book.Rows[hireable.IndexOf(dwarf.DisplayName)]);
            Assert.That(book.SelectedName, Is.EqualTo(dwarf.DisplayName));
            Assert.That(book.DetailTexts, Does.Contain($"Работает быстрее на {dwarf.FavoredWorkPercent}%"));

            UiFeel.Press(book.NavButton(WikiSection.Goods));
            var leather = _catalog.GetResource(ResourceKind.Leather).DisplayName;
            Assert.That(book.RowNames, Does.Contain(leather));

            book.Search("кож");
            Assert.That(book.RowNames, Does.Contain(leather));
            Assert.That(book.RowNames, Does.Contain(_catalog.GetBuilding(BuildingKind.Tannery).DisplayName),
                "The search finds the building too");

            book.Go(WikiSection.Buildings, BuildingKind.Forge.ToString());
            Assert.That(book.Section, Is.EqualTo(WikiSection.Buildings));
            Assert.That(book.SelectedName, Is.EqualTo(_catalog.GetBuilding(BuildingKind.Forge).DisplayName));

            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(book.IsOpen, Is.False, "Esc closes the book");
            Assert.That(paused, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void Menu_StopsTheColony_AndEscapeWalksBack()
        {
            var paused = new List<bool>();
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                OpenBattle = _ => { },
                SetPaused = paused.Add
            });

            hud.Menu.Open();
            Assert.That(hud.Menu.IsOpen, Is.True);
            Assert.That(paused, Is.EqualTo(new[] { true }));
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Pause));

            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(hud.Menu.IsOpen, Is.False, "Esc on the first page closes the menu");
            Assert.That(paused, Is.EqualTo(new[] { true, false }));
            Assert.That(hud.CloseTopOverlay(), Is.False, "Nothing left to close");
        }

        [Test]
        public void Menu_RoundActionsOpenTheirPages_AndBackWalksUpOnePage()
        {
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                OpenBattle = _ => { },
                Languages = Localization.Languages,
                CurrentLanguage = () => "ru"
            });
            hud.Menu.Open();

            UiFeel.Press(hud.Menu.SettingsButton);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Settings));
            Assert.That(hud.Menu.LanguageButton.Q<Label>(className: "btn__caption").text, Is.EqualTo("Русский"));

            UiFeel.Press(hud.Menu.LanguageButton);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Languages));
            Assert.That(hud.Menu.LanguageButtons, Has.Count.EqualTo(Localization.Languages.Count));

            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Settings), "Esc on the languages goes back to the settings");
            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Pause));

            UiFeel.Press(hud.Menu.RestartButton);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Restart));
            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Pause));
            Assert.That(hud.Menu.IsOpen, Is.True);
        }

        [Test]
        public void Menu_PickingALanguage_AppliesItAndGoesBackToTheSettings()
        {
            string picked = null;
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                OpenBattle = _ => { },
                Languages = Localization.Languages,
                CurrentLanguage = () => "ru",
                SetLanguage = code => picked = code
            });
            hud.Menu.Open();
            hud.Menu.Show(MenuPage.Languages);

            UiFeel.Press(hud.Menu.LanguageButtons.First(l => l.Code == "de").Button);

            Assert.That(picked, Is.EqualTo("de"));
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Settings));
        }

        [Test]
        public void Menu_VolumeBar_IsFilledAsFarAsTheSavedVolume()
        {
            float music = GameSettings.Music;
            try
            {
                GameSettings.SetMusic(.4f);
                var hud = TestUi.Colony(_session, _interaction);
                hud.Menu.Open();
                hud.Menu.Show(MenuPage.Settings);

                Assert.That(hud.Menu.MusicSlider.value, Is.EqualTo(.4f).Within(.001f));
                Assert.That(hud.Menu.MusicFill, Is.EqualTo(.4f).Within(.001f));
                Assert.That(hud.Menu.MusicValue, Is.EqualTo("40"));
            }
            finally
            {
                GameSettings.SetMusic(music);
            }
        }

        [Test]
        public void IntroAndAbout_NameTheEdition_AndSteamWaitsForItsStorePage()
        {
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                OpenBattle = _ => { },
                Edition = BuildEdition.SteamDemo
            });

            Assert.That(hud.Intro.Title, Is.EqualTo("Демо-версия"));
            Assert.That(hud.Intro.OffersSteam, Is.EqualTo(GameLinks.HasSteamPage));
            hud.Menu.Open();
            UiFeel.Press(hud.Menu.AboutButton);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.About));
            Assert.That(hud.Menu.AboutText, Does.Contain(MenuPanel.AboutDemo));

            hud.Intro.SetEdition(BuildEdition.Alpha);
            Assert.That(hud.Intro.Title, Is.EqualTo("Альфа-версия"));
        }

        [Test]
        public void GuildCard_ListsItsUpgrades_AndBuysOne()
        {
            Assert.That(_session.DebugAddGold(5000).Ok, Is.True);
            var cell = _session.FindFirstBuildingCell(BuildingKind.HaulersGuild);
            Assert.That(cell, Is.Not.Null);
            Assert.That(_session.Dispatch(new BuildBuildingCommand(BuildingKind.HaulersGuild, cell.Value)).Ok, Is.True);
            var hud = TestUi.Colony(_session, _interaction);
            var guild = _session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.HaulersGuild);
            _interaction.SelectBuilding(guild.Id);
            hud.Refresh(_session.CurrentSnapshot);

            int hosted = _catalog.Upgrades.Count(u => u.Host == BuildingKind.HaulersGuild);
            Assert.That(hosted, Is.GreaterThan(0));
            Assert.That(hud.Inspect.UpgradeButtons, Has.Count.EqualTo(hosted));
            int gold = _session.CurrentSnapshot.Gold;

            UiFeel.Press(hud.Inspect.UpgradeButtons[0]);

            var first = _catalog.Upgrades.First(u => u.Host == BuildingKind.HaulersGuild);
            Assert.That(_session.CurrentSnapshot.Upgrades.Single(u => u.Id == first.Id).Level, Is.EqualTo(1));
            Assert.That(_session.CurrentSnapshot.Gold, Is.EqualTo(gold - first.CostFrom(0)));
        }

        [Test]
        public void BarracksCard_RaisesTheBuilding_WhoseLevelOpensTheNextStepOfItsUpgrades()
        {
            Assert.That(_session.DebugAddGold(20000).Ok, Is.True);
            var hud = TestUi.Colony(_session, _interaction);
            var barracks = _session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.Barracks);
            _interaction.SelectBuilding(barracks.Id);
            hud.Refresh(_session.CurrentSnapshot);
            Assert.That(hud.Inspect.Actions, Has.Count.EqualTo(2), "Move and upgrade: creatures are hired in the catalog");

            var hosted = _catalog.Upgrades.Where(u => u.Host == BuildingKind.Barracks).ToList();
            var squad = hosted.First(u => u.HostLevelFrom(1) == 2);
            int row = hosted.IndexOf(squad);
            Assert.That(_session.Dispatch(new BuyUpgradeCommand(squad.Id)).Ok, Is.True);
            hud.Refresh(_session.CurrentSnapshot);
            Assert.That(UiFeel.IsAvailable(hud.Inspect.UpgradeButtons[row]), Is.False, "The next step waits for the barracks' level 2");
            Assert.That(hud.Inspect.UpgradeInfos[row], Does.Contain("2-го уровня"), "The row says what opens it");
            Assert.That(hud.Inspect.Note, Does.Contain("Новый уровень здания открывает"));

            UiFeel.Press(hud.Inspect.Actions[1]);
            hud.Refresh(_session.CurrentSnapshot);
            Assert.That(_session.CurrentSnapshot.Buildings.Single(b => b.Id == barracks.Id).Level, Is.EqualTo(2),
                "The second action raises the barracks");
            Assert.That(UiFeel.IsAvailable(hud.Inspect.UpgradeButtons[row]), Is.True);
            UiFeel.Press(hud.Inspect.UpgradeButtons[row]);
            Assert.That(_session.CurrentSnapshot.Upgrades.Single(u => u.Id == squad.Id).Level, Is.EqualTo(2));
        }

        [Test]
        public void Localization_TranslatesTexts_TemplatesAndTheNamesInside()
        {
            string chosen = GameSettings.Language;
            try
            {
                Localization.Use(AssetDatabase.LoadAssetAtPath<LanguageTable>("Assets/Game/UI/Localization/Languages.asset"));
                Localization.Select("en");
                Assert.That(Localization.Loaded, Is.EqualTo("en"), "Languages.asset lists the English file");
                Assert.That(Localization.T("Каталог"), Is.Not.EqualTo("Каталог"));
                Assert.That(Localization.T("12 зол."), Does.Not.Contain("зол"), "A template with its number");
                string built = Localization.T("Построить: шахта");
                Assert.That(built, Does.Not.Contain("Построить"));
                Assert.That(built, Does.Not.Contain("шахта"), "The name inside the template is translated too");
                Assert.That(Localization.T("12345"), Is.EqualTo("12345"));
            }
            finally
            {
                GameSettings.SetLanguage(chosen);
                Localization.Unload();
            }
            Assert.That(Localization.T("Каталог"), Is.EqualTo("Каталог"), "Unloaded: the source text again");
        }
    }
}
