using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Bots;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The saves on screen, over the real layout and real save files: the launch window with no save, with a save to
    /// go on with and with only saves this build cannot open; the slot sheet's buttons for each kind of slot; the
    /// questions before an overwrite, a delete and a load that drops unsaved play; the status line's word; and the
    /// island pictures the sheet lets go of when it closes.
    /// </summary>
    public class SaveUiTests
    {
        // a short build's own stamp: a whole-chain game from another edition stays shut in it
        private static readonly SaveStamp Alpha = new("v1.0.58", "Alpha", 12);

        private GameContentCatalog _catalog;
        private GameSession _session;
        private InteractionController _interaction;
        private string _folder;
        private DateTime _now;
        private readonly List<SavedGame> _opened = new();
        private byte[] _picture;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
            _session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            _interaction = new InteractionController(_session);
            _folder = Path.Combine(Path.GetTempPath(), "troll-save-ui-" + Guid.NewGuid().ToString("N"));
            _now = new DateTime(2026, 10, 9, 9, 30, 0, DateTimeKind.Utc);
            _opened.Clear();
            var texture = new Texture2D(16, 9, TextureFormat.RGB24, false);
            texture.SetPixels(Enumerable.Repeat(new Color(.4f, .7f, .3f), 16 * 9).ToArray());
            texture.Apply();
            _picture = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // ----- the launch window -----

        [Test]
        public void LaunchWindow_WithoutSaves_StaysTheNoticeWithPlay()
        {
            var hud = Hud(Saves(), launch: true);
            var intro = Quiet(hud.Intro);

            Assert.That(intro.OfferSaves(), Is.False);
            Assert.That(intro.OffersSaves, Is.False);
            Assert.That(Ui.IsShown(intro.ContinueButton), Is.False);
            Assert.That(Ui.IsShown(intro.LoadButton), Is.False);
            Assert.That(Caption(intro.PlayButton), Is.EqualTo("Играть"));
            Assert.That(intro.PlayButton.ClassListContains("btn--primary"), Is.True);
            Assert.That(UiFeel.IsAvailable(intro.PlayButton), Is.True);
            Assert.That(intro.CardTitle, Is.Null);

            intro.Open();
            intro.Back();
            Assert.That(intro.IsOpen, Is.False, "without saves Esc closes the notice as «Играть» did");
        }

        [Test]
        public void LaunchWindow_WithALatestSave_ShowsItsCard_AndContinueOpensIt()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Later(60);
            SaveGameTests.HireFighters(_session, 1);
            _session.Advance(61f);
            Assert.That(saves.AutosaveIfDue()?.Ok, Is.True, "the timed autosave writes the newest save");
            var hud = Hud(saves, launch: true);
            var intro = Quiet(hud.Intro);

            Assert.That(intro.OfferSaves(), Is.True);
            Assert.That(intro.Latest.SlotId, Is.EqualTo(SaveGames.AutosaveSlotId), "the newest save is the card's");
            Assert.That(intro.CardTitle, Does.StartWith("Уровень 1: "));
            Assert.That(intro.CardPictures, Has.Count.EqualTo(1), "the card shows the island");
            Assert.That(Ui.IsShown(intro.ContinueButton) && UiFeel.IsAvailable(intro.ContinueButton), Is.True);
            Assert.That(Ui.IsShown(intro.LoadButton), Is.True);
            Assert.That(Caption(intro.PlayButton), Is.EqualTo("Новая колония"));
            Assert.That(intro.PlayButton.ClassListContains("btn--primary"), Is.False, "«Продолжить» leads");

            intro.Open();
            intro.Back();
            Assert.That(intro.IsOpen, Is.True, "Esc never starts a new colony over the saves");

            intro.AskHints(true);
            Assert.That(UiFeel.IsAvailable(intro.ContinueButton), Is.False, "the questions hold «Продолжить» too");
            Assert.That(UiFeel.IsAvailable(intro.LoadButton), Is.False);
            intro.AskHints(false);
            Assert.That(UiFeel.IsAvailable(intro.ContinueButton), Is.True);

            var picture = intro.CardPictures[0];
            intro.Confirm();
            Assert.That(_opened, Has.Count.EqualTo(1), "Enter is «Продолжить»: the latest save opens");
            Assert.That(intro.IsOpen, Is.False);
            Assert.That(picture == null, Is.True, "the card's picture goes with the window");
        }

        [Test]
        public void LaunchWindow_WithOnlyLockedSaves_PlaysANewColony_AndTheListSaysWhy()
        {
            var full = new SaveGames(_session, new FileSaveStore(_folder), new SaveStamp("v1.0.58", "Full"), null, null, () => _now)
            {
                Thumbnail = () => _picture
            };
            Assert.That(full.Save("slot-1").Ok, Is.True);
            var saves = Saves();
            Assert.That(saves.Save("slot-2").Ok, Is.True);
            File.WriteAllBytes(Path.Combine(_folder, "slot-3.save"), NewerFormat(File.ReadAllBytes(Path.Combine(_folder, "slot-2.save"))));
            Assert.That(saves.Delete("slot-2"), Is.True);
            File.WriteAllText(Path.Combine(_folder, "slot-4.save"), "not a save");
            var hud = Hud(saves, launch: true);
            var intro = Quiet(hud.Intro);

            Assert.That(intro.OfferSaves(), Is.True, "saves to show, even shut ones");
            Assert.That(intro.Latest, Is.Null, "none this build can open");
            Assert.That(Ui.IsShown(intro.ContinueButton), Is.False);
            Assert.That(Caption(intro.PlayButton), Is.EqualTo("Играть"));
            Assert.That(intro.PlayButton.ClassListContains("btn--primary"), Is.True);
            Assert.That(UiFeel.IsAvailable(intro.PlayButton), Is.True);

            intro.Open();
            UiFeel.Press(intro.LoadButton);
            Assert.That(intro.ShowsList, Is.True);
            var list = intro.List;
            Assert.That(list.RowIds, Is.EqualTo(new[] { "slot-1", "slot-3", "slot-4" }), "at launch only slots with a save");
            Assert.That(list.ProblemOf("slot-1"), Is.EqualTo(SaveProblem.OtherEdition));
            Assert.That(list.NoteOf("slot-1"), Is.EqualTo(SaveParts.EditionSentence("Full")));
            Assert.That(list.ProblemOf("slot-3"), Is.EqualTo(SaveProblem.NewerBuild));
            Assert.That(list.NoteOf("slot-3"), Is.EqualTo("Сохранено версией v9.9.99, у вас v1.0.58"));
            Assert.That(list.ProblemOf("slot-4"), Is.EqualTo(SaveProblem.Damaged));
            Assert.That(UiFeel.IsAvailable(list.LoadButton), Is.False, "a shut slot never loads");
            Assert.That(Ui.IsShown(list.SaveButton), Is.False, "nothing to save at launch");

            list.Select("slot-4");
            Assert.That(UiFeel.IsAvailable(list.DeleteButton), Is.True, "a damaged file can go");
            UiFeel.Press(list.DeleteButton);
            Assert.That(list.Asking, Is.EqualTo(SaveSheet.Question.Delete));
            UiFeel.Press(list.ConfirmButton);
            Assert.That(saves.Describe("slot-4").IsEmpty, Is.True);
            Assert.That(list.RowIds, Is.EqualTo(new[] { "slot-1", "slot-3" }));

            intro.Back();
            Assert.That(intro.ShowsList, Is.False, "Esc goes back to the window");
            Assert.That(intro.IsOpen, Is.True);
        }

        [Test]
        public void ASaveFromAShorterEdition_OpensHere_AndItsRowSaysWhereItCameFrom()
        {
            var alphaGame = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true,
                _catalog.Progression.AlphaLastQuestId);
            var alpha = new SaveGames(alphaGame, new FileSaveStore(_folder), new SaveStamp("v1.0.58", "Alpha"), null, null,
                () => _now) { Thumbnail = () => _picture };
            Assert.That(alpha.Save("slot-1").Ok, Is.True);
            var full = new SaveGames(_session, new FileSaveStore(_folder), new SaveStamp("v1.0.58", "Full"), null,
                _opened.Add, () => _now) { Thumbnail = () => _picture };
            Assert.That(full.Describe("slot-1").CarriedFrom, Is.EqualTo("Alpha"), "the full game carries an alpha game on");
            var hud = Hud(full, launch: true);
            var intro = Quiet(hud.Intro);
            Assert.That(intro.OfferSaves(), Is.True);
            Assert.That(intro.Latest?.SlotId, Is.EqualTo("slot-1"), "a carried save is one to go on with");

            var sheet = OpenSheet(hud);
            sheet.Select("slot-1");
            var chip = sheet.RowButton("slot-1").Q<Label>(className: "save-row__carried");
            Assert.That(chip?.text, Is.EqualTo("из альфа-версии"));
            Assert.That(sheet.ProblemOf("slot-1"), Is.EqualTo(SaveProblem.None));
            Assert.That(UiFeel.IsAvailable(sheet.LoadButton), Is.True);
            Assert.That(hud.Tooltip.Hover(chip), Is.True);
            Assert.That(hud.Tooltip.Body, Does.Contain("альфа-версия"));
        }

        [Test]
        public void NewColony_WhileOnlyTheAutosaveOpens_AsksFirst_AndBackingOutKeepsIt()
        {
            var saves = Saves();
            SaveGameTests.HireFighters(_session, 1);
            _session.Advance(61f);
            Assert.That(saves.AutosaveIfDue()?.Ok, Is.True);
            string autosave = Path.Combine(_folder, "autosave.save");
            byte[] kept = File.ReadAllBytes(autosave);
            int flights = 0;
            var hud = TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                OpenBattle = _ => { },
                Saves = saves,
                OffersSavesAtLaunch = true,
                IntroClosed = () => flights++
            });
            var intro = Quiet(hud.Intro);
            Assert.That(intro.OfferSaves(), Is.True);
            Assert.That(intro.NewColonyAsks, Is.True, "the autosave is the only save that opens");
            intro.Open();

            UiFeel.Press(intro.PlayButton);
            Assert.That(intro.AsksNewColony, Is.True, "«Новая колония» asks first");
            Assert.That(intro.IsOpen && !intro.ShowsList, Is.True);
            Assert.That(hud.Root.Q<Label>("intro-saves-title").text, Is.EqualTo("Начать новую колонию?"));
            Assert.That(intro.NewColonyQuestion, Is.EqualTo(IntroPanel.NewColonyWarning));
            Assert.That(hud.Root.Q("intro-saves").Q<Label>(className: "save-last__title").text, Does.StartWith("Уровень "),
                "the card of the colony that would be lost");

            intro.Confirm();
            Assert.That(intro.AsksNewColony && intro.IsOpen, Is.True, "Enter never answers the question");
            intro.Back();
            Assert.That(intro.AsksNewColony, Is.False, "Esc backs out to the window");
            Assert.That(intro.IsOpen, Is.True);

            UiFeel.Press(intro.PlayButton);
            UiFeel.Press(intro.NewColonyCancelButton);
            Assert.That(intro.AsksNewColony, Is.False);
            Assert.That(intro.IsOpen, Is.True);
            Assert.That(flights, Is.Zero, "no new colony started");
            Assert.That(File.ReadAllBytes(autosave), Is.EqualTo(kept), "backing out keeps the autosave as it was");

            UiFeel.Press(intro.PlayButton);
            UiFeel.Press(intro.NewColonyConfirmButton);
            Assert.That(intro.IsOpen, Is.False, "the danger button starts the new colony");
            Assert.That(flights, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(autosave), Is.EqualTo(kept), "the autosave stays until the new colony's first order");
        }

        [Test]
        public void NewColony_WithASavedSlot_StartsAtOnce()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Later(60);
            SaveGameTests.HireFighters(_session, 1);
            _session.Advance(61f);
            Assert.That(saves.AutosaveIfDue()?.Ok, Is.True);
            var hud = Hud(saves, launch: true);
            var intro = Quiet(hud.Intro);
            Assert.That(intro.OfferSaves(), Is.True);
            Assert.That(intro.NewColonyAsks, Is.False, "a slot keeps a colony");
            intro.Open();

            UiFeel.Press(intro.PlayButton);
            Assert.That(intro.AsksNewColony, Is.False);
            Assert.That(intro.IsOpen, Is.False, "no question: the new colony starts");
        }

        [Test]
        public void RestartPage_WhileOnlyTheAutosaveKeepsTheColony_SaysToSaveItIntoASlot()
        {
            var saves = Saves();
            var hud = Hud(saves, launch: false);
            hud.Menu.Open();
            UiFeel.Press(hud.Menu.RestartButton);
            Assert.That(hud.Menu.RestartText, Is.EqualTo(MenuPanel.RestartOverAutosave));

            hud.Menu.Show(MenuPage.Pause);
            Assert.That(saves.Save("slot-2").Ok, Is.True);
            UiFeel.Press(hud.Menu.RestartButton);
            Assert.That(hud.Menu.RestartText, Is.EqualTo(MenuPanel.RestartWarning), "a slot keeps the colony: the page as before");
        }

        // ----- the slot sheet in the pause menu -----

        [Test]
        public void PauseMenu_OffersTheSaves_WithTheirWordsInTheHint()
        {
            var hud = Hud(Saves(), launch: false);
            hud.Menu.Open();

            Assert.That(hud.Menu.SavesButton, Is.Not.Null);
            Assert.That(hud.Tooltip.Hover(hud.Menu.SavesButton), Is.True);
            Assert.That(hud.Tooltip.Title, Is.EqualTo("Сохранения"));
            Assert.That(hud.Tooltip.Body, Does.Contain("ячейку"));

            UiFeel.Press(hud.Menu.SavesButton);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Saves));
            Assert.That(MenuTitle(hud), Is.EqualTo("Сохранения"));
            Assert.That(hud.Root.Q("menu-dialog").ClassListContains("saves-dialog"), Is.True, "the wider sheet");
            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Pause));
            Assert.That(hud.Root.Q("menu-dialog").ClassListContains("saves-dialog"), Is.False);

            var bare = TestUi.Colony(_session, _interaction);
            Assert.That(bare.Menu.SavesButton, Is.Null, "no saves, no action");
        }

        [Test]
        public void SlotSheet_TheButtonsFollowTheChosenSlot()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-2").Ok, Is.True);
            File.WriteAllBytes(Path.Combine(_folder, "slot-3.save"), NewerFormat(File.ReadAllBytes(Path.Combine(_folder, "slot-2.save"))));
            File.WriteAllText(Path.Combine(_folder, "slot-4.save"), "not a save");
            var hud = Hud(saves, launch: false);
            var sheet = OpenSheet(hud);

            Assert.That(sheet.RowIds, Is.EqualTo(new[] { "autosave", "slot-1", "slot-2", "slot-3", "slot-4" }));
            Assert.That(sheet.Selected, Is.EqualTo("slot-1"), "a free slot is chosen first");
            Assert.That(Buttons(sheet), Is.EqualTo((false, true, false)), "an empty slot: save only");
            Assert.That(Caption(sheet.SaveButton), Is.EqualTo("Сохранить"));
            Assert.That(sheet.RowButton("slot-1").ClassListContains("is-on"), Is.True);

            UiFeel.Press(sheet.RowButton(SaveGames.AutosaveSlotId));
            Assert.That(Buttons(sheet), Is.EqualTo((false, false, false)), "the autosave is the game's; nothing in it yet");

            UiFeel.Press(sheet.RowButton("slot-2"));
            Assert.That(sheet.RowButton("slot-1").ClassListContains("is-on"), Is.False, "one slot is chosen at a time");
            Assert.That(Buttons(sheet), Is.EqualTo((true, true, true)));
            Assert.That(Caption(sheet.SaveButton), Is.EqualTo("Перезаписать"));

            UiFeel.Press(sheet.RowButton("slot-3"));
            Assert.That(sheet.ProblemOf("slot-3"), Is.EqualTo(SaveProblem.NewerBuild));
            Assert.That(sheet.RowButton("slot-3").ClassListContains("is-locked"), Is.True);
            Assert.That(Buttons(sheet), Is.EqualTo((true, true, false)), "a newer version's save: no load");

            UiFeel.Press(sheet.RowButton("slot-4"));
            Assert.That(sheet.ProblemOf("slot-4"), Is.EqualTo(SaveProblem.Damaged));
            Assert.That(Buttons(sheet), Is.EqualTo((true, false, false)), "a damaged file: only delete");
            Assert.That(sheet.RowButton("slot-4").Q(className: "save-fact"), Is.Null, "no numbers from a damaged file");

            UiFeel.Press(sheet.RowButton("slot-1"));
            UiFeel.Press(sheet.SaveButton);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.None), "an empty slot saves at once");
            Assert.That(saves.Describe("slot-1").Loadable, Is.True);
            Assert.That(sheet.ShowsSaved("slot-1"), Is.True, "the row says «Сохранено»");
            Assert.That(hud.Status.ShowsNote && hud.Status.Text == "Колония сохранена", Is.True);
            Assert.That(Buttons(sheet), Is.EqualTo((true, true, true)));
        }

        [Test]
        public void Overwrite_AsksWasAndWillBe_AndCancelKeepsTheSlot()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-2").Ok, Is.True);
            int before = saves.Describe("slot-2").Header.Summary.Gold;
            Assert.That(_session.DebugAddGold(1234).Ok, Is.True);
            var hud = Hud(saves, launch: false);
            var sheet = OpenSheet(hud);
            sheet.Select("slot-2");

            UiFeel.Press(sheet.SaveButton);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.Overwrite));
            Assert.That(MenuTitle(hud), Is.EqualTo("Перезаписать ячейку 2?"));
            Assert.That(sheet.QuestionText, Is.EqualTo("Колония из ячейки 2 пропадёт, на её месте будет нынешняя."));
            var cards = hud.Root.Query(className: "save-compare__card").ToList();
            Assert.That(cards, Has.Count.EqualTo(2), "was and will be");
            Assert.That(cards[1].Q<Label>(className: "save-fact__value").text, Is.EqualTo((before + 1234).ToString()),
                "«Будет» shows the colony now");
            Assert.That(sheet.Pictures, Has.Count.EqualTo(2), "the slot's picture and the island now");

            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.None), "Esc goes back to the list");
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Saves));
            Assert.That(MenuTitle(hud), Is.EqualTo("Сохранения"));
            Assert.That(saves.Describe("slot-2").Header.Summary.Gold, Is.EqualTo(before), "nothing written");

            UiFeel.Press(sheet.SaveButton);
            UiFeel.Press(sheet.CancelButton);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.None));
            Assert.That(saves.Describe("slot-2").Header.Summary.Gold, Is.EqualTo(before));

            UiFeel.Press(sheet.SaveButton);
            UiFeel.Press(sheet.ConfirmButton);
            Assert.That(saves.Describe("slot-2").Header.Summary.Gold, Is.EqualTo(before + 1234), "overwritten");
            Assert.That(sheet.ShowsSaved("slot-2"), Is.True);
            Assert.That(sheet.Selected, Is.EqualTo("slot-2"));
        }

        [Test]
        public void Delete_AsksFirst_ThenEmptiesTheSlot()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            var hud = Hud(saves, launch: false);
            var sheet = OpenSheet(hud);
            sheet.Select("slot-1");

            UiFeel.Press(sheet.DeleteButton);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.Delete));
            Assert.That(MenuTitle(hud), Is.EqualTo("Удалить ячейку 1?"));
            Assert.That(sheet.QuestionText, Is.EqualTo("Колония из ячейки 1 пропадёт насовсем."));
            Assert.That(saves.Describe("slot-1").IsEmpty, Is.False, "nothing goes before the answer");

            UiFeel.Press(sheet.ConfirmButton);
            Assert.That(saves.Describe("slot-1").IsEmpty, Is.True);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.None));
            Assert.That(Buttons(sheet), Is.EqualTo((false, true, false)), "the slot is free again");
        }

        [Test]
        public void Load_WithUnsavedPlay_AsksFirst_NamingTheLastSave()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            var hud = Hud(saves, launch: false);
            var sheet = OpenSheet(hud);
            sheet.Select("slot-1");

            UiFeel.Press(sheet.LoadButton);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.None), "nothing unsaved: the load goes at once");
            Assert.That(_opened, Has.Count.EqualTo(1));
            Assert.That(hud.Menu.IsOpen, Is.False, "the menu steps aside for the loaded colony");

            Assert.That(_session.DebugAddGold(1).Ok, Is.True);
            Assert.That(saves.HasUnsavedChanges, Is.True);
            sheet = OpenSheet(hud);
            sheet.Select("slot-1");
            UiFeel.Press(sheet.LoadButton);
            Assert.That(sheet.Asking, Is.EqualTo(SaveSheet.Question.Load));
            Assert.That(MenuTitle(hud), Is.EqualTo("Загрузить ячейку 1?"));
            string time = saves.LastSavedAtUtc.Value.ToLocalTime().ToString("HH:mm");
            Assert.That(sheet.QuestionText, Does.StartWith("Всё, что не сохранено с ").And.Contain(time).And.EndWith(", пропадёт."));
            Assert.That(_opened, Has.Count.EqualTo(1));

            UiFeel.Press(sheet.ConfirmButton);
            Assert.That(_opened, Has.Count.EqualTo(2));
            Assert.That(hud.Menu.IsOpen, Is.False);
        }

        [Test]
        public void SlotSheet_LetsItsPicturesGo_WhenItIsRebuiltOrClosed()
        {
            var saves = Saves();
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Assert.That(saves.Save("slot-3").Ok, Is.True);
            var hud = Hud(saves, launch: false);
            var sheet = OpenSheet(hud);
            var first = sheet.Pictures.ToList();
            Assert.That(first, Has.Count.EqualTo(2), "a texture for each saved slot");
            Assert.That(first.All(t => t != null && t.width == 16), Is.True, "the save's PNG decoded");

            sheet.Refresh();
            Assert.That(first.All(t => t == null), Is.True, "a rebuilt list lets the old textures go");
            var second = sheet.Pictures.ToList();
            Assert.That(second, Has.Count.EqualTo(2));

            hud.Menu.Close();
            Assert.That(second.All(t => t == null), Is.True, "closing the menu lets them go");
            Assert.That(sheet.Pictures, Is.Empty);
        }

        [Test]
        public void StatusLine_SaysSaved_ForThePlayersSave_NotForTheTimedAutosave()
        {
            var saves = Saves();
            var hud = Hud(saves, launch: false);
            SaveGameTests.HireFighters(_session, 1);
            _session.Advance(61f);
            var timed = saves.AutosaveIfDue();
            Assert.That(timed?.Ok == true && timed.Reason == "interval", Is.True);
            Assert.That(hud.Status.ShowsNote, Is.False, "the minute's autosave stays quiet");

            Assert.That(_session.DebugCompleteQuest().Ok, Is.True);
            Assert.That(_session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            var quest = saves.AutosaveIfDue();
            Assert.That(quest?.Reason, Is.EqualTo("quest"));
            Assert.That(hud.Status.ShowsNote, Is.True, "a quest's autosave is named");
            Assert.That(hud.Status.Text, Is.EqualTo("Колония сохранена"));
        }

        // ----- helpers -----

        private SaveGames Saves() =>
            new(_session, new FileSaveStore(_folder), Alpha, null, _opened.Add, () => _now) { Thumbnail = () => _picture };

        private ColonyHudView Hud(SaveGames saves, bool launch) =>
            TestUi.Colony(new ColonyHudContext(_session, _interaction)
            {
                OpenBattle = _ => { },
                Saves = saves,
                OffersSavesAtLaunch = launch
            });

        // the launch window with both questions answered, so only the saves decide what it offers
        private static IntroPanel Quiet(IntroPanel intro)
        {
            intro.AskConsent(null);
            intro.AskHints(false);
            return intro;
        }

        private static SaveSheet OpenSheet(ColonyHudView hud)
        {
            if (!hud.Menu.IsOpen) hud.Menu.Open();
            UiFeel.Press(hud.Menu.SavesButton);
            Assert.That(hud.Menu.Page, Is.EqualTo(MenuPage.Saves));
            return hud.Menu.SaveSlots;
        }

        private static (bool Delete, bool Save, bool Load) Buttons(SaveSheet sheet) =>
            (UiFeel.IsAvailable(sheet.DeleteButton), UiFeel.IsAvailable(sheet.SaveButton), UiFeel.IsAvailable(sheet.LoadButton));

        private static string Caption(Button button) => button.Q<Label>(className: "btn__caption").text;

        private static string MenuTitle(ColonyHudView hud) => hud.Root.Q<Label>("menu-title").text;

        private void Later(int seconds) => _now = _now.AddSeconds(seconds);

        // a save as a newer build would write it: a format this build does not read yet
        private static byte[] NewerFormat(byte[] document)
        {
            var (header, body) = SaveCodec.Open(document);
            byte[] picture = SaveCodec.Thumbnail(document);
            header.Set("version", new JsonNumber((SaveCodec.Version + 1).ToString()));
            header.Set("build", new JsonString("v9.9.99"));
            return SaveCodec.Pack(header, body.ToString(), picture);
        }
    }
}
