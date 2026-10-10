using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Bots;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The save slots on disk (<see cref="FileSaveStore"/>) and the colony's saves over them (<see cref="SaveGames"/>):
    /// whole-or-nothing writes with the save before kept, slot lists from headers and pictures alone, broken files
    /// listed as such, the autosave's moments, and a new colony that leaves the slots alone until it is played.
    /// </summary>
    public class SaveStoreTests
    {
        private static readonly byte[] PictureA = { 0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3 };
        private static readonly byte[] PictureB = { 0x89, (byte)'P', (byte)'N', (byte)'G', 4, 5, 6, 7, 8 };

        private GameContentCatalog _catalog;
        private string _folder;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
            _folder = Path.Combine(Path.GetTempPath(), "troll-saves-" + Guid.NewGuid().ToString("N"));
            _now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // ----- the store -----

        [Test]
        public void Write_ReplacesTheSaveWholeAndKeepsThePreviousAsBackup()
        {
            var store = new FileSaveStore(_folder);
            byte[] first = { 1, 2, 3 }, second = { 4, 5, 6, 7 };

            Assert.That(store.Write("slot-a", first).Ok, Is.True);
            Assert.That(store.Has("slot-a", true), Is.False, "a first save has no backup");
            Assert.That(store.Write("slot-a", second).Ok, Is.True);

            Assert.That(store.Read("slot-a").Bytes, Is.EqualTo(second));
            Assert.That(store.Read("slot-a", true).Bytes, Is.EqualTo(first));
            Assert.That(store.ReadRange("slot-a", false, 1, 2).Bytes, Is.EqualTo(new byte[] { 5, 6 }));
            Assert.That(Directory.GetFiles(_folder).Select(Path.GetFileName),
                Is.EquivalentTo(new[] { "slot-a.save", "slot-a.save.bak" }), "no temporary file stays behind");
            Assert.That(store.Slots(), Is.EqualTo(new[] { "slot-a" }));

            Assert.That(store.Delete("slot-a"), Is.True);
            Assert.That(store.Slots(), Is.Empty);
            Assert.That(store.Read("slot-a").Error, Is.EqualTo(SaveError.NotFound));
        }

        [Test]
        public void SlotIds_AreFileNamesOnEveryPlatform()
        {
            var store = new FileSaveStore(_folder);
            foreach (string bad in new[] { null, "", "../escape", "Upper", "-dash-first", "a b", "con.save", new string('a', 65) })
            {
                Assert.That(SaveSlots.IsValidId(bad), Is.False, bad);
                Assert.That(store.Write(bad, new byte[] { 1 }).Error, Is.EqualTo(SaveError.InvalidSlotId), bad);
            }
            foreach (string good in new[] { "a", "autosave", "slot-1", "save-20261009-120000", "slot_2" })
                Assert.That(SaveSlots.IsValidId(good), Is.True, good);
        }

        // ----- listing -----

        [Test]
        public void List_ReadsHeadersAndPicturesNewestFirst()
        {
            var session = Campaign();
            var saves = Saves(session);
            saves.Thumbnail = () => PictureA;
            Assert.That(saves.Save("slot-1", "Начало").Ok, Is.True);
            Later(30);
            session.Advance(5f);
            SaveGameTests.HireFighters(session, 2);
            saves.Thumbnail = () => PictureB;
            var second = saves.Save("slot-2");
            Assert.That(second.Ok, Is.True, second.Detail);

            var slots = saves.List();

            Assert.That(slots.Select(s => s.SlotId), Is.EqualTo(new[] { "slot-2", "slot-1" }));
            Assert.That(slots.All(s => s.Loadable && !s.IsAutosave && !s.FromBackup && s.Problem == SaveProblem.None), Is.True);
            Assert.That(slots[1].Header.Label, Is.EqualTo("Начало"));
            Assert.That(slots[0].Header.Summary.Creatures, Is.EqualTo(2));
            Assert.That(slots[0].Header.SavedAtUtc, Is.EqualTo(_now));
            Assert.That(slots[0].Header.ColonyId, Is.EqualTo(saves.ColonyId));
            Assert.That(slots[0].WrittenBy, Is.EqualTo(SaveGameTests.Stamp.Build));
            Assert.That(slots[0].Thumbnail, Is.EqualTo(PictureB), "each save its own picture");
            Assert.That(slots[1].Thumbnail, Is.EqualTo(PictureA), "each save its own picture");
            Assert.That(second.Slot.Header.Summary.Gold, Is.EqualTo(session.CurrentSnapshot.Gold));
            Assert.That(second.Slot.Thumbnail, Is.EqualTo(PictureB));
            Assert.That(saves.LastSavedAtUtc, Is.EqualTo(_now));
            Assert.That(saves.Describe("slot-4").IsEmpty, Is.True);
            Assert.That(saves.Describe("slot-4").Problem, Is.EqualTo(SaveProblem.None), "an empty slot is not damaged");
        }

        [Test]
        public void Latest_IsTheNewestSaveThisBuildCanLoad()
        {
            var session = Campaign();
            var saves = Saves(session);
            Assert.That(saves.Latest(), Is.Null, "nothing saved yet");
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Later(10);
            Arm(session);
            session.Advance(61f);
            Assert.That(saves.AutosaveIfDue()?.Ok, Is.True);
            Later(10);
            Assert.That(saves.Save("slot-2").Ok, Is.True);
            Later(10);
            byte[] newer = Edited(NewerFormat(File.ReadAllBytes(Path.Combine(_folder, "slot-2.save"))),
                h => h.Set("savedAt", new JsonString(_now.AddHours(1).ToString("o"))));
            File.WriteAllBytes(Path.Combine(_folder, "slot-3.save"), newer);

            Assert.That(saves.Latest().SlotId, Is.EqualTo("slot-2"), "the newer build's save is newest, but not loadable");
            File.Delete(Path.Combine(_folder, "slot-2.save"));
            Assert.That(saves.Latest().SlotId, Is.EqualTo("autosave"));
        }

        [Test]
        public void List_TellsANewerBuildsSaveFromADamagedOne()
        {
            var session = Campaign();
            var saves = Saves(session);
            saves.Thumbnail = () => PictureA;
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Assert.That(saves.Save("cut").Ok, Is.True);
            Assert.That(saves.Save("demo").Ok, Is.True);
            byte[] good = File.ReadAllBytes(Path.Combine(_folder, "slot-1.save"));
            File.WriteAllBytes(Path.Combine(_folder, "slot-2.save"), NewerFormat(good));
            File.WriteAllBytes(Path.Combine(_folder, "demo.save"), Edited(good, h => h.Set("edition", new JsonString("SteamDemo"))));
            File.WriteAllText(Path.Combine(_folder, "junk.save"), "this is not a save");
            File.WriteAllBytes(Path.Combine(_folder, "empty.save"), Array.Empty<byte>());
            string cut = Path.Combine(_folder, "cut.save");
            File.WriteAllBytes(cut, good.Take(good.Length - 100).ToArray());
            File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not a slot");

            IReadOnlyList<SaveSlotInfo> slots = null;
            Assert.DoesNotThrow(() => slots = saves.List());

            var byId = slots.ToDictionary(s => s.SlotId);
            Assert.That(byId.Keys, Is.EquivalentTo(new[] { "slot-1", "slot-2", "demo", "cut", "junk", "empty" }));
            Assert.That(byId["slot-1"].Loadable, Is.True);

            var newer = byId["slot-2"];
            Assert.That((newer.Loadable, newer.Status, newer.Problem),
                Is.EqualTo((false, SaveError.NewerVersion, SaveProblem.NewerBuild)));
            Assert.That(newer.WrittenBy, Is.EqualTo("v9.9.99"), "the card names the build that wrote it");
            Assert.That(newer.Header?.Summary.QuestLevel, Is.EqualTo(1), "and still shows the colony at a glance");
            Assert.That(newer.Thumbnail, Is.EqualTo(PictureA), "and its picture");

            Assert.That((byId["demo"].Status, byId["demo"].Problem), Is.EqualTo((SaveError.OtherEdition, SaveProblem.OtherEdition)));
            foreach (string broken in new[] { "cut", "junk", "empty" })
            {
                Assert.That(byId[broken].Loadable, Is.False, broken);
                Assert.That(byId[broken].Status, Is.EqualTo(SaveError.Corrupt), $"{broken}: {byId[broken].Detail}");
                Assert.That(byId[broken].Problem, Is.EqualTo(SaveProblem.Damaged), broken);
                Assert.That(byId[broken].Thumbnail, Is.Null, broken);
            }
            Assert.That(byId["cut"].Header, Is.Not.Null, "a cut file still names its colony");
            Assert.That(saves.Load("cut").Problem, Is.EqualTo(SaveProblem.Damaged));
            Assert.That(saves.Load("junk").Error, Is.EqualTo(SaveError.Corrupt));
            Assert.That(saves.Load("slot-2").Problem, Is.EqualTo(SaveProblem.NewerBuild));
        }

        [Test]
        public void Load_OfANewerBuildsSaveWithContentThisBuildLacks_SaysNewerBuild()
        {
            var session = Campaign();
            var saves = Saves(session);
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            byte[] good = File.ReadAllBytes(Path.Combine(_folder, "slot-1.save"));
            var (header, body) = SaveCodec.Open(good);
            ((JsonObject)((JsonArray)((JsonObject)body["state"])["buildings"])[0]).Set("kind", new JsonString("Lighthouse"));
            header.Set("build", new JsonString("v9.9.99"));
            File.WriteAllBytes(Path.Combine(_folder, "slot-2.save"), SaveCodec.Pack(header, body.ToString()));
            header.Set("build", new JsonString("v0.0.1"));
            File.WriteAllBytes(Path.Combine(_folder, "slot-3.save"), SaveCodec.Pack(header, body.ToString()));

            Assert.That(saves.Describe("slot-2").Loadable, Is.True, "the header alone cannot tell");
            var fromNewer = saves.Load("slot-2");
            Assert.That((fromNewer.Error, fromNewer.Problem), Is.EqualTo((SaveError.UnknownContent, SaveProblem.NewerBuild)));
            var fromOlder = saves.Load("slot-3");
            Assert.That((fromOlder.Error, fromOlder.Problem), Is.EqualTo((SaveError.UnknownContent, SaveProblem.Damaged)));
        }

        [Test]
        public void ASlotWhoseSaveWentMissing_ListsAndLoadsItsBackupWithItsOwnPicture()
        {
            var session = Campaign();
            SavedGame opened = null;
            var saves = new SaveGames(session, new FileSaveStore(_folder), SaveGameTests.Stamp, null,
                game => opened = game, () => _now);
            saves.Thumbnail = () => PictureA;
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            session.Advance(10f);
            saves.Thumbnail = () => PictureB;
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Assert.That(saves.Describe("slot-1", true).Thumbnail, Is.EqualTo(PictureA), "the backup keeps its picture");
            // a crash after the old save became the backup and before the new one took its name
            File.Delete(Path.Combine(_folder, "slot-1.save"));

            var slot = saves.List().Single();
            Assert.That((slot.SlotId, slot.FromBackup, slot.Loadable), Is.EqualTo(("slot-1", true, true)));
            Assert.That(slot.Header.Summary.ActiveTimeMs, Is.EqualTo(0), "the backup is the save before");
            Assert.That(slot.Thumbnail, Is.EqualTo(PictureA));
            var load = saves.Load("slot-1");
            Assert.That(load.Ok, Is.True, load.ToString());
            Assert.That(opened, Is.SameAs(load.Game));
        }

        [Test]
        public void ADamagedPicture_IsLeftOutAndTheSaveStillLoads()
        {
            var session = Campaign();
            var saves = Saves(session);
            saves.Thumbnail = () => PictureB;
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            string path = Path.Combine(_folder, "slot-1.save");
            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 1] ^= 0xff;
            File.WriteAllBytes(path, bytes);

            var slot = saves.Describe("slot-1");
            Assert.That(slot.Loadable, Is.True);
            Assert.That(slot.Thumbnail, Is.Null);
            Assert.That(saves.Check("slot-1").Ok, Is.True);
            Assert.That(SaveCodec.Thumbnail(bytes), Is.Null);
        }

        [Test]
        public void AFailingPicture_DoesNotFailTheSave()
        {
            var session = Campaign();
            var saves = Saves(session);
            saves.Thumbnail = () => throw new InvalidOperationException("no camera");
            var saved = saves.Save("slot-1");
            Assert.That(saved.Ok, Is.True, saved.Detail);
            Assert.That(saved.Slot.Thumbnail, Is.Null);
            Assert.That(saves.Check("slot-1").Ok, Is.True);
        }

        [Test]
        public void Load_OpensOnlyACheckedSave()
        {
            var session = Campaign();
            var opened = new List<SavedGame>();
            var saves = new SaveGames(session, new FileSaveStore(_folder), SaveGameTests.Stamp, null, opened.Add, () => _now);
            session.Advance(12f);
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            File.WriteAllText(Path.Combine(_folder, "bad.save"), "{}\n{}");

            Assert.That(saves.Load("missing").Error, Is.EqualTo(SaveError.NotFound));
            Assert.That(saves.Load("bad").Error, Is.EqualTo(SaveError.UnknownFormat));
            Assert.That(opened, Is.Empty, "a refused save opens nothing");

            var load = saves.Load("slot-1");
            Assert.That(load.Ok, Is.True, load.ToString());
            Assert.That(opened.Single().State.ActiveTimeMs, Is.EqualTo(session.ActiveTimeMs));
            Assert.That(opened.Single().Header.ColonyId, Is.EqualTo(saves.ColonyId));

            // the loaded colony keeps its id and knows when it was saved
            var restored = GameSession.Restore(_catalog, opened.Single());
            var again = new SaveGames(restored, new FileSaveStore(_folder), SaveGameTests.Stamp, opened.Single().Header,
                null, () => _now);
            Assert.That((again.ColonyId, again.LastSavedAtUtc, again.AutosaveArmed), Is.EqualTo((saves.ColonyId, (DateTime?)_now, true)));
        }

        [Test]
        public void TheAutosaveSlot_IsTheGames()
        {
            var session = Campaign();
            var saves = Saves(session);
            Arm(session);
            session.Advance(61f);
            Assert.That(saves.AutosaveIfDue()?.Ok, Is.True);

            Assert.That(saves.Save(SaveGames.AutosaveSlotId).Error, Is.EqualTo(SaveError.Reserved));
            Assert.That(saves.Delete(SaveGames.AutosaveSlotId), Is.False, "the autosave cannot be deleted");
            Assert.That(saves.Describe(SaveGames.AutosaveSlotId).Loadable, Is.True);
            Assert.That(saves.Save("slot-1").Ok, Is.True);
            Assert.That(saves.Delete("slot-1"), Is.True);
            Assert.That(saves.Delete("slot-1"), Is.False, "nothing left to delete");
            var fresh = saves.SaveNew("Моя");
            Assert.That(fresh.Ok, Is.True);
            Assert.That(fresh.SlotId, Does.StartWith("save-"));
            Assert.That(saves.NewSlotId(), Is.Not.EqualTo(fresh.SlotId));
        }

        [Test]
        public void PreviewAndPictureNow_ShowWhatASaveWouldCarry_AndWriteNothing()
        {
            var session = Campaign();
            var saves = Saves(session);
            saves.Thumbnail = () => PictureA;
            SaveGameTests.HireFighters(session, 2);
            session.Advance(3f);

            var preview = saves.Preview();
            Assert.That(saves.PictureNow(), Is.EqualTo(PictureA));
            Assert.That(Directory.Exists(_folder) ? Directory.GetFiles(_folder, "*", SearchOption.AllDirectories) : Array.Empty<string>(),
                Is.Empty, "nothing written");

            var saved = saves.Save("slot-1");
            var summary = saved.Slot.Header.Summary;
            Assert.That((preview.QuestLevel, preview.QuestId, preview.Gold, preview.Creatures, preview.Buildings,
                    preview.LandOwned, preview.ActiveTimeMs, preview.ChainLength),
                Is.EqualTo((summary.QuestLevel, summary.QuestId, summary.Gold, summary.Creatures, summary.Buildings,
                    summary.LandOwned, summary.ActiveTimeMs, summary.ChainLength)));
            Assert.That(preview.Creatures, Is.EqualTo(2));

            saves.Thumbnail = () => throw new InvalidOperationException("no camera");
            byte[] none = PictureA;
            Assert.DoesNotThrow(() => none = saves.PictureNow());
            Assert.That(none, Is.Null);
            saves.Thumbnail = null;
            Assert.That(saves.PictureNow(), Is.Null);
        }

        [Test]
        public void ASlotOfAShorterEdition_ListsAsCarriedForward_ALongerOnesAsOtherEdition()
        {
            var demoGame = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true, _catalog.Progression.Quests[5].Id);
            var demo = new SaveGames(demoGame, new FileSaveStore(_folder), SaveGameTests.DemoStamp, null, null, () => _now);
            Assert.That(demo.Save("slot-1").Ok, Is.True);
            var fullGame = Campaign();
            var opened = new List<SavedGame>();
            var full = new SaveGames(fullGame, new FileSaveStore(_folder), SaveGameTests.FullStamp, null, opened.Add, () => _now);
            Assert.That(full.Save("slot-2").Ok, Is.True);

            var inFull = full.Describe("slot-1");
            Assert.That((inFull.Loadable, inFull.CarriedFrom, inFull.Problem), Is.EqualTo((true, "SteamDemo", SaveProblem.None)));
            Assert.That(inFull.Header.Summary.ChainLength, Is.EqualTo(6));
            Assert.That(full.Describe("slot-2").CarriedFrom, Is.Null, "its own edition's save");
            var inDemo = demo.Describe("slot-2");
            Assert.That((inDemo.Loadable, inDemo.Status, inDemo.Problem, inDemo.CarriedFrom),
                Is.EqualTo((false, SaveError.OtherEdition, SaveProblem.OtherEdition, (string)null)));

            var load = full.Load("slot-1");
            Assert.That((load.Ok, load.CarriedFrom), Is.EqualTo((true, "SteamDemo")), load.ToString());
            Assert.That(opened.Single().SlotId, Is.EqualTo("slot-1"));
            Assert.That(opened.Single().State.Progress.ChainLength, Is.EqualTo(0));
            Assert.That(demo.Load("slot-2").Problem, Is.EqualTo(SaveProblem.OtherEdition));
        }

        // ----- the autosave -----

        [Test]
        public void Autosave_ComesEveryMinuteOfColonyTimeWithSomethingChanged()
        {
            var session = Campaign();
            var saves = Saves(session);
            Arm(session);
            Assert.That(saves.AutosaveIfDue(), Is.Null, "nothing due yet");

            session.Advance(59f);
            Assert.That(saves.AutosaveDue, Is.Null);
            Assert.That(saves.AutosaveIfDue(), Is.Null);
            session.Advance(1f);
            Assert.That(saves.AutosaveDue, Is.EqualTo("interval"));
            var saved = saves.AutosaveIfDue();
            Assert.That(saved?.Ok, Is.True, saved?.Detail);
            Assert.That((saved.SlotId, saved.Autosave, saved.Slot.IsAutosave), Is.EqualTo(("autosave", true, true)));
            Assert.That(saves.Dirty, Is.False);
            Assert.That(saves.AutosaveIfDue(), Is.Null, "nothing due right after");

            session.Advance(30f);
            Assert.That(saves.AutosaveNow("quit")?.Ok, Is.True, "a quit saves whatever changed");
            Assert.That(saves.AutosaveNow("quit"), Is.Null, "and nothing when nothing changed");
            Assert.That(saves.List().Single().SlotId, Is.EqualTo("autosave"));
        }

        [Test]
        public void ANewColony_LeavesTheAutosaveAloneUntilItIsPlayed()
        {
            var old = Campaign();
            var oldSaves = Saves(old);
            Arm(old);
            old.Advance(61f);
            Assert.That(oldSaves.AutosaveIfDue()?.Ok, Is.True);
            Assert.That(oldSaves.Save("slot-1").Ok, Is.True);
            oldSaves.Dispose();
            string oldColony = oldSaves.ColonyId;

            Later(60);
            var fresh = Campaign();
            var saves = Saves(fresh);
            fresh.Advance(120f);
            Assert.That(saves.AutosaveArmed, Is.False);
            Assert.That(saves.AutosaveIfDue(), Is.Null, "a colony nobody has played yet is not saved");
            Assert.That(saves.AutosaveNow("quit"), Is.Null, "not even on quit");
            Assert.That(saves.Describe("autosave").Header.ColonyId, Is.EqualTo(oldColony));

            Arm(fresh);
            fresh.Advance(61f);
            Assert.That(saves.AutosaveIfDue()?.Ok, Is.True, "played, it takes the autosave");
            Assert.That(saves.Describe("autosave").Header.ColonyId, Is.EqualTo(saves.ColonyId));
            Assert.That(saves.Describe("autosave", true).Header.ColonyId, Is.EqualTo(oldColony), "the old one stays as backup");
            Assert.That(saves.Describe("slot-1").Header.ColonyId, Is.EqualTo(oldColony), "player slots are never touched");
        }

        [Test]
        public void AnAutosaveThisBuildCannotRead_IsSetAsideNotOverwritten()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, "autosave.save"), "damaged beyond reading");
            var session = Campaign();
            var saves = Saves(session);
            Assert.That(saves.Latest(), Is.Null);
            Arm(session);
            session.Advance(61f);

            var saved = saves.AutosaveIfDue();

            Assert.That(saved?.Ok, Is.True, saved?.Detail);
            Assert.That(saves.Describe("autosave").Loadable, Is.True);
            Assert.That(saves.Describe("autosave").HasBackup, Is.False, "the damaged file did not become the backup");
            var aside = Directory.GetFiles(Path.Combine(_folder, FileSaveStore.AsideFolder));
            Assert.That(aside.Select(File.ReadAllText), Is.EqualTo(new[] { "damaged beyond reading" }));
            Assert.That(saves.List().Select(s => s.SlotId), Is.EqualTo(new[] { "autosave" }), "set aside is out of the list");
        }

        [Test]
        public void Autosave_FollowsAClaimedQuest()
        {
            var session = Campaign();
            var saves = Saves(session);
            session.DebugCompleteQuest();
            Assert.That(saves.AutosaveDue, Is.Null, "a quest done is not yet a quest claimed");
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            Assert.That(saves.AutosaveDue, Is.EqualTo("quest"));
            var saved = saves.AutosaveIfDue();
            Assert.That(saved?.Reason, Is.EqualTo("quest"));
            Assert.That(saved.Slot.Header.Summary.QuestLevel, Is.EqualTo(2));
        }

        [Test]
        public void Autosave_WaitsForTheColonyAfterABattle()
        {
            var session = Campaign();
            SaveGameTests.HireFighters(session, 3);
            var saves = Saves(session);
            Arm(session);
            session.Advance(61f);
            session.EnableDebugBattleAccess();
            Assert.That(SaveGameTests.Fight(session, session.ArenaLadder()[0]).Ok, Is.True);
            Assert.That(saves.AutosaveDue, Is.EqualTo("interval"));
            Assert.That(saves.AutosaveIfDue(), Is.Null, "never while a battle runs");

            Assert.That(session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
            saves.HoldAutosave = () => true;
            Assert.That(saves.AutosaveIfDue(), Is.Null, "nor while the composer holds it");
            saves.HoldAutosave = null;
            var saved = saves.AutosaveIfDue();
            Assert.That(saved?.Reason, Is.EqualTo("battle"));
            Assert.That(saved.Ok, Is.True, saved.Detail);
        }

        [Test]
        public void AFailedWrite_ReportsAndDoesNotThrow()
        {
            Directory.CreateDirectory(_folder);
            string blocked = Path.Combine(_folder, "blocked");
            File.WriteAllText(blocked, "a file where the save folder should be");
            var session = Campaign();
            var results = new List<SaveResult>();
            var saves = new SaveGames(session, new FileSaveStore(blocked), SaveGameTests.Stamp, null, null, () => _now);
            saves.Saved += results.Add;
            Arm(session);
            session.Advance(61f);

            SaveResult saved = null;
            Assert.DoesNotThrow(() => saved = saves.AutosaveIfDue());
            Assert.That(saved.Error, Is.EqualTo(SaveError.WriteFailed), saved.Detail);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(saves.AutosaveIfDue(), Is.Null, "a failed autosave waits for the next minute");
            Assert.That(saves.Dirty, Is.True);
        }

        private GameSession Campaign() => new(_catalog, TestColony.LayoutFor(_catalog), campaign: true);

        private SaveGames Saves(GameSession session) =>
            new(session, new FileSaveStore(_folder), SaveGameTests.Stamp, null, null, () => _now);

        // the player's first order: a new colony's autosave may write from now on
        private static void Arm(GameSession session) => SaveGameTests.HireFighters(session, 1);

        private void Later(int seconds) => _now = _now.AddSeconds(seconds);

        private static byte[] NewerFormat(byte[] document) => Edited(document, header =>
        {
            header.Set("version", new JsonNumber((SaveCodec.Version + 1).ToString()));
            header.Set("build", new JsonString("v9.9.99"));
        });

        private static byte[] Edited(byte[] document, Action<JsonObject> change)
        {
            var (header, body) = SaveCodec.Open(document);
            byte[] picture = SaveCodec.Thumbnail(document);
            change(header);
            return SaveCodec.Pack(header, body.ToString(), picture);
        }
    }
}
