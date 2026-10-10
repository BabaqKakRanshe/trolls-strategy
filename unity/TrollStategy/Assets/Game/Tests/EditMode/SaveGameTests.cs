using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Saves at the narrowest boundary: a game written by <see cref="SaveCodec"/> and restored by
    /// <see cref="GameSession.Restore"/> plays on exactly as the game it came from, and a save that breaks a rule is
    /// refused with its own reason instead of throwing or being mended.
    /// </summary>
    public class SaveGameTests
    {
        // the itch.io alpha, whose games end after 3 quests: another edition's longer game is refused here
        internal static readonly SaveStamp Stamp = new("v1.0.58", "Alpha", 3);
        internal static readonly DateTime SavedAt = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
        }

        // ----- round trip -----

        [Test]
        public void RestoredColony_PlaysOnInStepWithTheGameItCameFrom()
        {
            var original = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            var play = new CampaignBot(original, BotProfile.Typical) { StopAfterLevel = 999 }.Start();
            // a colony grown by play: buildings, workers, haulers, quests, arena wins, land
            for (int looks = 0; looks < 4000 && original.ActiveTimeMs < 45 * 60000; looks++)
            {
                if (!play.Look()) break;
                original.Advance(play.WaitSeconds);
                var grown = original.CurrentSnapshot;
                if (grown.Progress.Level >= 12 && original.BattlesWon > 0 && grown.Buildings.Count >= 8) break;
            }
            Assert.That(original.BattlesWon, Is.GreaterThan(0), "the bot won no battle before the save");
            Assert.That(original.CurrentSnapshot.Units.Count, Is.GreaterThan(4));

            // and what play may not have at this moment: gear on a fighter, a battle reward waiting, land clearing
            Assert.That(original.DebugGrantGear(1).Ok, Is.True);
            Assert.That(original.DebugAddGold(100000).Ok, Is.True);
            BuyAndClearLand(original);
            WinARewardToTake(original);
            WaitForALoadedHauler(original);
            var before = original.CurrentSnapshot;
            Assert.That(before.BattleReward, Is.Not.Null, "a battle reward waits");
            Assert.That(before.Equipment.Any(e => e.OwnerUnitId != null), Is.True, "a fighter wears gear");
            Assert.That(before.Land.Blocks.Any(b => b.Clearing), Is.True, "land is being cleared");
            Assert.That(before.Units.Any(u => u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.Carried > 0),
                Is.True, "a hauler carries goods");
            Assert.That(before.Progress.Quest, Is.Not.Null);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            byte[] document = SaveCodec.Write(original.Export(), _catalog, Stamp, SavedAt, "colony-1");
            double writeMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var read = SaveCodec.Read(document, _catalog, Stamp);
            double readMs = watch.Elapsed.TotalMilliseconds;
            TestContext.WriteLine($"{before.Units.Count} creatures, {before.Buildings.Count} buildings: " +
                                  $"{document.Length} bytes, written in {writeMs:F1} ms, read in {readMs:F1} ms");
            Assert.That(read.Ok, Is.True, read.ToString());
            var restored = GameSession.Restore(_catalog, read.Game);
            restored.EnableDebugBattleAccess(); // the original's developer switch; it is not part of a save
            AssertSameGame(original, restored, "after the load");

            // the bot plays on in the original; every command it sends and every clock call goes to the copy too
            var sent = new List<(IGameCommand Command, CommandResult Result)>();
            original.OnCommandResolved += (command, result) => sent.Add((command, result));
            int replayed = 0, battles = 0;
            for (int look = 0; look < 160; look++)
            {
                sent.Clear();
                bool goesOn = play.Look();
                foreach (var (command, result) in sent)
                {
                    var again = restored.Dispatch(command);
                    Assert.That((again.Ok, again.Error), Is.EqualTo((result.Ok, result.Error)),
                        $"look {look}: {command.GetType().Name}");
                    replayed++;
                    if (command is StartBattleCommand && result.Ok) battles++;
                }
                if (!goesOn) break;
                original.Advance(play.WaitSeconds);
                restored.Advance(play.WaitSeconds);
                AssertSameState(original, restored, $"look {look}");
                if (look % 10 == 0) AssertSameGame(original, restored, $"look {look}");
            }
            TestContext.WriteLine($"replayed {replayed} commands, {battles} battles, to {original.ActiveTimeMs / 1000} s");
            Assert.That(replayed, Is.GreaterThan(20), "the bot played on after the save");
            AssertSameGame(original, restored, "at the end");
        }

        [Test]
        public void Restore_RebuildsWhatTheSessionKeepsAside()
        {
            var session = Campaign();
            session.DebugCompleteQuest();
            session.Dispatch(new ClaimQuestRewardCommand());
            session.Advance(3.3f);
            var restored = Restore(session);

            var a = session.CurrentSnapshot.Progress;
            var b = restored.CurrentSnapshot.Progress;
            Assert.That(b.Level, Is.EqualTo(a.Level));
            Assert.That(b.Quest.Id, Is.EqualTo(a.Quest.Id));
            Assert.That(b.Quest.TutorialSteps, Is.EqualTo(a.Quest.TutorialSteps), "tutorial length comes from content");
            foreach (var building in _catalog.Buildings)
                Assert.That(b.UnlockLevel(building.Kind), Is.EqualTo(a.UnlockLevel(building.Kind)), building.Kind.ToString());
            foreach (var mission in session.ArenaLadder())
                Assert.That((restored.ArenaOffer(mission).GoldMin, restored.ArenaOffer(mission).GoldMax),
                    Is.EqualTo((session.ArenaOffer(mission).GoldMin, session.ArenaOffer(mission).GoldMax)), mission.MissionId);
            Assert.That(restored.StartingBuildingIds, Is.Empty, "a loaded colony places no starting buildings");
            Assert.That(restored.CurrentSnapshot.Revision, Is.EqualTo(1), "the revision counts this session's commits");
        }

        [Test]
        public void SaveDuringABattle_OpensInTheColonyWithTheBattleDone()
        {
            var session = Campaign();
            HireFighters(session, 3);
            session.EnableDebugBattleAccess();
            Assert.That(Fight(session, session.ArenaLadder()[0]).Ok, Is.True);
            Assert.That(session.ActiveBattle, Is.Not.Null, "the battle is being watched");

            var restored = Restore(session);

            Assert.That(restored.ActiveBattle, Is.Null, "a save keeps no replay");
            Assert.That(restored.BattlesWon, Is.EqualTo(session.BattlesWon), "the outcome is in the colony");
            Assert.That(restored.CurrentSnapshot.Units.Count, Is.EqualTo(session.CurrentSnapshot.Units.Count));
            Assert.That(restored.CurrentSnapshot.Gold, Is.EqualTo(session.CurrentSnapshot.Gold));
            Assert.That(restored.Dispatch(new BuildBuildingCommand(BuildingKind.Warehouse, new Cell(-5, -5))).Error,
                Is.Not.EqualTo("Сначала завершите текущий бой"), "the restored colony takes orders");
        }

        [Test]
        public void Floats_ComeBackBitForBit()
        {
            var random = new Random(12345);
            var bytes = new byte[4];
            for (int i = 0; i < 200000; i++)
            {
                random.NextBytes(bytes);
                float value = BitConverter.ToSingle(bytes, 0);
                if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                string text = new JsonWriter().Value(value).ToString();
                var node = (JsonNumber)JsonParser.Parse(text);
                float back = float.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(back), 0),
                    Is.EqualTo(BitConverter.ToInt32(bytes, 0)), $"{value:R} as {text}");
            }
        }

        [Test]
        public void Header_SaysWhatTheSlotCardShows()
        {
            var session = Campaign();
            HireFighters(session, 2);
            session.Advance(61f);
            byte[] document = SaveCodec.Write(session.Export(), _catalog, Stamp, SavedAt, "colony-7", "Перед боем");
            int newline = Array.IndexOf(document, (byte)'\n');
            var (header, error, detail, _) = SaveCodec.ReadHeader(document.Take(newline).ToArray(), document.Length, Stamp);

            Assert.That(error, Is.EqualTo(SaveError.None), detail);
            var snapshot = session.CurrentSnapshot;
            var summary = header.Summary;
            Assert.That(header.Version, Is.EqualTo(SaveCodec.Version));
            Assert.That(header.SavedAtUtc, Is.EqualTo(SavedAt));
            Assert.That((header.Build, header.Edition, header.ColonyId, header.Label),
                Is.EqualTo(("v1.0.58", "Alpha", "colony-7", "Перед боем")));
            Assert.That(summary.Campaign, Is.True);
            Assert.That(summary.ActiveTimeMs, Is.EqualTo(session.ActiveTimeMs));
            Assert.That(summary.QuestLevel, Is.EqualTo(snapshot.Progress.Level));
            Assert.That(summary.QuestTotal, Is.EqualTo(_catalog.Progression.Quests.Count));
            Assert.That((summary.QuestId, summary.QuestTitle), Is.EqualTo((snapshot.Progress.Quest.Id, snapshot.Progress.Quest.Title)));
            Assert.That(summary.Gold, Is.EqualTo(snapshot.Gold));
            Assert.That(summary.Creatures, Is.EqualTo(2));
            Assert.That(summary.Buildings, Is.EqualTo(snapshot.Buildings.Count));
            Assert.That(summary.LandOwned, Is.EqualTo(snapshot.Land.Blocks.Count(b => b.Owned)));
        }

        // ----- refusals -----

        private static IEnumerable<TestCaseData> Faults()
        {
            TestCaseData Case(string name, SaveError error, Action<JsonObject, JsonObject> change) =>
                new TestCaseData(change, error).SetName($"Read_Refuses_{name}");

            yield return Case("UnknownBuildingKind", SaveError.UnknownContent,
                (h, b) => Building(b, 0).Set("kind", new JsonString("Spaceport")));
            yield return Case("UnknownGood", SaveError.UnknownContent,
                (h, b) => ((JsonObject)State(b)["soldByResource"]).Set("Unobtainium", new JsonNumber("3")));
            yield return Case("EnumNumberForAName", SaveError.UnknownContent,
                (h, b) => Unit(b, 0).Set("kind", new JsonString("1")));
            yield return Case("UnknownMission", SaveError.UnknownContent,
                (h, b) => ((JsonObject)State(b)["missionWins"]).Set("mission-404", new JsonNumber("1")));
            yield return Case("UnknownItem", SaveError.UnknownContent,
                (h, b) => Item(b, 0).Set("item", new JsonString("laser-sword")));
            yield return Case("DanglingWorkplace", SaveError.DanglingReference,
                (h, b) => ((JsonObject)Unit(b, 0)["assignment"]).Set("kind", new JsonString("Work"))
                    .Set("building", new JsonString("mine-99")));
            yield return Case("DanglingHaulRoute", SaveError.DanglingReference,
                (h, b) => ((JsonObject)Unit(b, 1)["assignment"]).Set("destination", new JsonString("market-77")));
            yield return Case("GearOfAMissingCreature", SaveError.DanglingReference,
                (h, b) => Item(b, 0).Set("owner", new JsonString("unit-404")));
            yield return Case("TwoItemsInOneSlot", SaveError.InvalidOwnership, (h, b) =>
            {
                var items = (JsonArray)State(b)["equipment"];
                var weapons = items.Items.Cast<JsonObject>()
                    .Where(i => ((JsonString)i["item"]).Value.Contains("sword")).Take(2).ToList();
                Assert.That(weapons.Count, Is.EqualTo(2), "the test colony holds two swords");
                foreach (var weapon in weapons) weapon.Set("owner", new JsonString("unit-1"));
            });
            yield return Case("DuplicateBuildingId", SaveError.DuplicateId,
                (h, b) => Building(b, 1).Set("id", Building(b, 0)["id"]));
            yield return Case("DuplicateUnitId", SaveError.DuplicateId,
                (h, b) => Unit(b, 1).Set("id", Unit(b, 0)["id"]));
            yield return Case("UnitCounterBehindItsIds", SaveError.InvalidValue,
                (h, b) => State(b).Set("nextUnitId", new JsonNumber("1")));
            yield return Case("NegativeGold", SaveError.InvalidValue,
                (h, b) => State(b).Set("gold", new JsonNumber("-5")));
            yield return Case("NotANumberPosition", SaveError.InvalidValue,
                (h, b) => Unit(b, 0).Set("x", new JsonString("NaN")));
            yield return Case("LevelPastTheTop", SaveError.InvalidValue,
                (h, b) => Building(b, 0).Set("level", new JsonNumber("99")));
            yield return Case("OverlappingBuildings", SaveError.InvalidLayout,
                (h, b) => Building(b, 1).Set("x", Building(b, 0)["x"]).Set("y", Building(b, 0)["y"]));
            yield return Case("OtherLandGrid", SaveError.LandMismatch, (h, b) =>
            {
                var land = (JsonObject)State(b)["land"];
                land.Set("blocksPerSide", new JsonNumber("2")).Set("blocks", new JsonString("cccc"))
                    .Set("clearing", new JsonArray());
            });
            yield return Case("UnknownQuest", SaveError.QuestMismatch,
                (h, b) => ((JsonObject)State(b)["progress"]).Set("questId", new JsonString("quest-from-the-future")));
            yield return Case("QuestGoalsOfAnotherShape", SaveError.QuestMismatch,
                (h, b) => ((JsonArray)((JsonObject)State(b)["progress"])["goalDone"]).Add(new JsonBool(true)));
            yield return Case("NewerVersion", SaveError.NewerVersion,
                (h, b) => h.Set("version", new JsonNumber((SaveCodec.Version + 1).ToString())));
            yield return Case("OlderThanAnyMigration", SaveError.UnsupportedVersion,
                (h, b) => h.Set("version", new JsonNumber("0")));
            yield return Case("OtherEdition", SaveError.OtherEdition,
                (h, b) => h.Set("edition", new JsonString("SteamDemo")));
            yield return Case("OtherFormat", SaveError.UnknownFormat,
                (h, b) => h.Set("format", new JsonString("someone-elses-save")));
            yield return Case("MissingField", SaveError.Corrupt, (h, b) => State(b).Remove("gold"));
        }

        [TestCaseSource(nameof(Faults))]
        public void Read_RefusesABrokenRuleWithItsReason(Action<JsonObject, JsonObject> change, SaveError expected)
        {
            byte[] document = Changed(SmallColonyDocument(), change);
            LoadResult read = null;
            Assert.DoesNotThrow(() => read = SaveCodec.Read(document, _catalog, Stamp));
            Assert.That(read.Error, Is.EqualTo(expected), read.Detail);
            Assert.That(read.Game, Is.Null);
            Assert.That(read.Detail, Is.Not.Empty);
        }

        [Test]
        public void Read_TheUntouchedSmallColony_Passes()
        {
            var read = SaveCodec.Read(SmallColonyDocument(), _catalog, Stamp);
            Assert.That(read.Ok, Is.True, read.ToString());
        }

        [Test]
        public void Read_RefusesBrokenBytesWithoutThrowing()
        {
            byte[] good = SmallColonyDocument();
            int newline = Array.IndexOf(good, (byte)'\n');
            var broken = new Dictionary<string, byte[]>
            {
                ["empty"] = Array.Empty<byte>(),
                ["no newline"] = System.Text.Encoding.UTF8.GetBytes("{\"format\":\"troll-strategy-save\"}"),
                ["not json"] = System.Text.Encoding.UTF8.GetBytes("{not json at all\n{}"),
                ["cut in the body"] = good.Take(good.Length - 40).ToArray(),
                ["cut in the header"] = good.Take(newline / 2).ToArray(),
                ["a flipped byte"] = Flip(good, newline + 50),
                ["random bytes"] = RandomBytes(4096),
                ["not utf-8"] = new byte[] { 0xff, 0xfe, 0x00, (byte)'\n', 0xc3, 0x28 }
            };
            foreach (var pair in broken)
            {
                LoadResult read = null;
                Assert.DoesNotThrow(() => read = SaveCodec.Read(pair.Value, _catalog, Stamp), pair.Key);
                Assert.That(read.Error, Is.EqualTo(SaveError.Corrupt), $"{pair.Key}: {read.Detail}");
            }
        }

        [Test]
        public void Read_FindsTheQuestByIdWhenTheChainMovedIt()
        {
            var session = Campaign();
            for (int i = 0; i < 3; i++)
            {
                session.DebugCompleteQuest();
                Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            }
            byte[] saved = SaveCodec.Write(session.Export(), _catalog, Stamp, SavedAt, "moved");
            // as if this build's chain had two quests fewer in front of it
            byte[] document = Changed(saved, (h, b) =>
                ((JsonObject)State(b)["progress"]).Set("questIndex", new JsonNumber("1")));

            var read = SaveCodec.Read(document, _catalog, Stamp);

            Assert.That(read.Ok, Is.True, read.ToString());
            Assert.That(read.Game.State.Progress.QuestIndex, Is.EqualTo(3),
                "the quest's id puts it where this build's chain has it");
        }

        // ----- editions -----

        [Test]
        public void ASaveLoadsOnlyInItsOwnEdition()
        {
            var session = Campaign();
            byte[] demo = SaveCodec.Write(session.Export(), _catalog, new SaveStamp("v1.0.58", "SteamDemo"), SavedAt, "c");

            Assert.That(SaveCodec.Read(demo, _catalog, new SaveStamp("v1.0.58", "SteamDemo")).Ok, Is.True);
            var elsewhere = SaveCodec.Read(demo, _catalog, Stamp);
            Assert.That(elsewhere.Error, Is.EqualTo(SaveError.OtherEdition), elsewhere.Detail);
            Assert.That(elsewhere.Header?.Edition, Is.EqualTo("SteamDemo"), "the slot card can still say whose it is");
        }

        [Test]
        public void AShortGame_KeepsItsOwnEndThroughASave()
        {
            var chain = _catalog.Progression.Quests;
            var session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true, chain[2].Id);
            byte[] early = SaveCodec.Write(session.Export(), _catalog, Stamp, SavedAt, "short");
            for (int i = 0; i < 3; i++)
            {
                session.DebugCompleteQuest();
                Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            }
            Assert.That(session.CurrentSnapshot.Progress.Over, Is.True);
            byte[] over = SaveCodec.Write(session.Export(), _catalog, Stamp, SavedAt, "short");

            var started = SaveCodec.Read(early, _catalog, Stamp);
            Assert.That(started.Ok, Is.True, started.ToString());
            Assert.That(started.Header.Summary.QuestTotal, Is.EqualTo(3), "the slot card counts the short chain");
            Assert.That(GameSession.Restore(_catalog, started.Game).CurrentSnapshot.Progress.LastLevel, Is.EqualTo(3),
                "a loaded short game still ends where it was going to");

            Assert.That(started.Game.State.Progress.ChainLength, Is.EqualTo(3), "the save keeps the game's own end");

            var ended = SaveCodec.Read(over, _catalog, Stamp);
            Assert.That(ended.Ok, Is.True, ended.ToString());
            Assert.That(ended.Header.Summary.Finished, Is.True);
            Assert.That(ended.Game.State.Progress.ChainLength, Is.EqualTo(3));
            Assert.That(ended.Game.State.Progress.ArenaCap, Is.EqualTo(session.CurrentSnapshot.Progress.ArenaCap));
            Assert.That(ended.Game.State.Progress.ArenaCap, Is.GreaterThan(0), "a finished short game caps the arena");
            var restored = GameSession.Restore(_catalog, ended.Game);
            var progress = restored.CurrentSnapshot.Progress;
            Assert.That(progress.Over, Is.True);
            Assert.That(progress.Quest, Is.Null);
            Assert.That(progress.LastLevel, Is.EqualTo(3));
            Assert.That(progress.ArenaCap, Is.EqualTo(session.CurrentSnapshot.Progress.ArenaCap));
            foreach (var mission in session.ArenaLadder())
                Assert.That(restored.InFullGameOnly(mission), Is.EqualTo(session.InFullGameOnly(mission)), mission.MissionId);
            Assert.That(session.ArenaLadder().Any(restored.InFullGameOnly), Is.True, "the cap still closes the levels above it");
            Assert.That(restored.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.False, "no quest follows the end");

            byte[] stretched = Changed(early, (h, b) =>
                ((JsonObject)State(b)["progress"]).Set("chainLength", new JsonNumber((chain.Count + 1).ToString())));
            Assert.That(SaveCodec.Read(stretched, _catalog, Stamp).Error, Is.EqualTo(SaveError.QuestMismatch),
                "an end past this build's chain");
        }

        // ----- editions carried forward -----

        // the editions as these tests stand them: the alpha ends after 3 quests, the demo after 6, the full game never
        internal static readonly SaveStamp AlphaStamp = new("v1.0.58", "Alpha", 3);
        internal static readonly SaveStamp DemoStamp = new("v1.0.58", "SteamDemo", 6);
        internal static readonly SaveStamp FullStamp = new("v1.0.58", "Full", 0);

        [Test]
        public void ADemoGameThatEnded_ContinuesInTheFullGameWithItsNextQuest()
        {
            var demo = ShortGame(6);
            PlayToTheEnd(demo);
            Assert.That(demo.CurrentSnapshot.Progress.Over, Is.True);
            Assert.That(demo.CurrentSnapshot.Progress.ArenaCap, Is.GreaterThan(0));
            Assert.That(demo.ArenaLadder().Any(demo.InFullGameOnly), Is.True, "the demo closed the ladder above its cap");
            byte[] saved = SaveCodec.Write(demo.Export(), _catalog, DemoStamp, SavedAt, "demo");

            var read = SaveCodec.Read(saved, _catalog, FullStamp);

            Assert.That(read.Ok, Is.True, read.ToString());
            Assert.That((read.CarriedFrom, read.Game.CarriedFrom, read.Game.ChainExtended), Is.EqualTo(("SteamDemo", "SteamDemo", true)));
            Assert.That(read.Header.Edition, Is.EqualTo("SteamDemo"), "the header still says who wrote it");
            var full = GameSession.Restore(_catalog, read.Game);
            var progress = full.CurrentSnapshot.Progress;
            Assert.That(progress.Over, Is.False);
            Assert.That(progress.Quest?.Id, Is.EqualTo(_catalog.Progression.Quests[6].Id), "the quest after the demo's end");
            Assert.That((progress.Level, progress.LastLevel, progress.ArenaCap), Is.EqualTo((7, 0, 0)));
            Assert.That(progress.Quest.IsComplete, Is.False);
            Assert.That(full.QuestStartedMs, Is.EqualTo(full.ActiveTimeMs), "the new quest starts at the load");
            foreach (var mission in full.ArenaLadder())
                Assert.That(full.InFullGameOnly(mission), Is.False, $"{mission.MissionId}: the ladder is open");
            Assert.That(full.CurrentSnapshot.Gold, Is.EqualTo(demo.CurrentSnapshot.Gold), "the colony is the demo's");

            // saved again in the full game it is a full game's save
            var again = SaveCodec.Read(SaveCodec.Write(full.Export(), _catalog, FullStamp, SavedAt, "demo"), _catalog, FullStamp);
            Assert.That((again.Ok, again.CarriedFrom, again.Game.State.Progress.ChainLength), Is.EqualTo((true, (string)null, 0)));
        }

        [Test]
        public void AnAlphaGame_OpensInTheDemoWithTheDemosChain()
        {
            var alpha = ShortGame(3);
            alpha.DebugCompleteQuest();
            Assert.That(alpha.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            string questId = alpha.CurrentSnapshot.Progress.Quest.Id;
            byte[] saved = SaveCodec.Write(alpha.Export(), _catalog, AlphaStamp, SavedAt, "alpha");

            var read = SaveCodec.Read(saved, _catalog, DemoStamp);

            Assert.That(read.Ok, Is.True, read.ToString());
            Assert.That((read.CarriedFrom, read.Game.ChainExtended), Is.EqualTo(("Alpha", false)));
            Assert.That(read.Game.State.Progress.ChainLength, Is.EqualTo(6), "the demo's chain length");
            var demo = GameSession.Restore(_catalog, read.Game);
            Assert.That(demo.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo(questId), "the quest at hand goes on");
            Assert.That(demo.CurrentSnapshot.Progress.LastLevel, Is.EqualTo(6));

            // an alpha game played to its end opens in the demo on its next quest
            PlayToTheEnd(alpha);
            var ended = SaveCodec.Read(SaveCodec.Write(alpha.Export(), _catalog, AlphaStamp, SavedAt, "alpha"), _catalog, DemoStamp);
            Assert.That(ended.Ok, Is.True, ended.ToString());
            Assert.That(ended.Game.ChainExtended, Is.True);
            Assert.That(GameSession.Restore(_catalog, ended.Game).CurrentSnapshot.Progress.Level, Is.EqualTo(4));
        }

        [Test]
        public void ALongerEditionsSave_IsRefusedInAShorterOne()
        {
            byte[] full = SaveCodec.Write(Campaign().Export(), _catalog, FullStamp, SavedAt, "full");
            byte[] demo = SaveCodec.Write(ShortGame(6).Export(), _catalog, DemoStamp, SavedAt, "demo");

            Assert.That(SaveCodec.Read(full, _catalog, DemoStamp).Error, Is.EqualTo(SaveError.OtherEdition), "full → demo");
            Assert.That(SaveCodec.Read(full, _catalog, AlphaStamp).Error, Is.EqualTo(SaveError.OtherEdition), "full → alpha");
            Assert.That(SaveCodec.Read(demo, _catalog, AlphaStamp).Error, Is.EqualTo(SaveError.OtherEdition), "demo → alpha");
            int newline = Array.IndexOf(full, (byte)'\n');
            var (_, error, _, _) = SaveCodec.ReadHeader(full.Take(newline).ToArray(), full.Length, DemoStamp);
            Assert.That(error, Is.EqualTo(SaveError.OtherEdition), "a slot list already says so");

            // a header written before the editions carried forward says nothing of its chain: the state decides
            byte[] older = Changed(demo, (h, b) => ((JsonObject)h["summary"]).Remove("chainLength"));
            Assert.That(SaveCodec.Read(older, _catalog, AlphaStamp).Error, Is.EqualTo(SaveError.OtherEdition));
            var carried = SaveCodec.Read(older, _catalog, FullStamp);
            Assert.That((carried.Ok, carried.CarriedFrom), Is.EqualTo((true, "SteamDemo")), carried.ToString());
        }

        [Test]
        public void ASaveOfThisEdition_KeepsItsOwnEndEvenWhenTheBuildsEndMoved()
        {
            var alpha = ShortGame(3);
            PlayToTheEnd(alpha);
            int cap = alpha.CurrentSnapshot.Progress.ArenaCap;
            byte[] saved = SaveCodec.Write(alpha.Export(), _catalog, AlphaStamp, SavedAt, "alpha");

            var read = SaveCodec.Read(saved, _catalog, new SaveStamp("v1.0.59", "Alpha", 5));

            Assert.That(read.Ok, Is.True, read.ToString());
            Assert.That(read.CarriedFrom, Is.Null);
            Assert.That((read.Game.State.Progress.ChainLength, read.Game.State.Progress.ArenaCap), Is.EqualTo((3, cap)));
            Assert.That(GameSession.Restore(_catalog, read.Game).CurrentSnapshot.Progress.Over, Is.True);
        }

        private GameSession ShortGame(int length) =>
            new(_catalog, TestColony.LayoutFor(_catalog), true, _catalog.Progression.Quests[length - 1].Id);

        private static void PlayToTheEnd(GameSession session)
        {
            for (int i = 0; i < 100 && !session.CurrentSnapshot.Progress.Over; i++)
            {
                session.DebugCompleteQuest();
                Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            }
        }

        // ----- telemetry -----

        [Test]
        public void Telemetry_OfALoadedGame_PicksTheFunnelUpWhereTheSaveLeftIt()
        {
            var session = Campaign();
            session.Advance(30f);
            session.DebugCompleteQuest();
            session.Dispatch(new ClaimQuestRewardCommand());
            session.Advance(70f);
            var restored = Restore(session);

            var events = new List<TelemetryEvent>();
            using var telemetry = new CampaignTelemetry(restored, events.Add, resumed: true);
            Assert.That(events, Is.Empty, "the quest at hand was reported before the save");

            restored.Advance(1f);
            Assert.That(events, Is.Empty, "the next heartbeat waits for the next minute of colony time");
            Assume.That(restored.CurrentSnapshot.Progress.Quest.IsComplete, Is.False, "the quest after the first is open");
            int questStart = restored.QuestStartedMs;
            Assert.That(questStart, Is.EqualTo(session.QuestStartedMs));
            restored.DebugCompleteQuest();
            var completed = events.Single(e => e.Name == CampaignTelemetry.QuestCompleted);
            Assert.That(completed["questSeconds"], Is.EqualTo((restored.ActiveTimeMs - questStart) / 1000),
                "the quest's time counts from its start in the saved game");

            var fresh = new List<TelemetryEvent>();
            using (new CampaignTelemetry(Restore(session), fresh.Add)) { }
            Assert.That(fresh.Select(e => e.Name), Does.Contain(CampaignTelemetry.QuestStarted),
                "a session not marked as resumed starts its funnel as a new game does");
        }

        [Test]
        public void Telemetry_ALoadedGame_SaysWhereItCameFrom()
        {
            var demo = ShortGame(6);
            PlayToTheEnd(demo);
            demo.Advance(125f);
            var read = SaveCodec.Read(SaveCodec.Write(demo.Export(), _catalog, DemoStamp, SavedAt, "demo"), _catalog, FullStamp);
            Assert.That(read.Ok, Is.True, read.ToString());
            var saved = read.Game.InSlot(SaveGames.AutosaveSlotId);
            var full = GameSession.Restore(_catalog, saved);

            var events = new List<TelemetryEvent>();
            using (var telemetry = new CampaignTelemetry(full, events.Add, resumed: true))
                telemetry.Loaded(saved, "Full");

            Assert.That(events.Select(e => e.Name), Is.EqualTo(new[] { CampaignTelemetry.GameLoaded, CampaignTelemetry.QuestStarted }),
                "the quest after the demo's end starts with the load");
            var loaded = events[0];
            Assert.That(loaded["slot"], Is.EqualTo("autosave"));
            Assert.That(loaded["questId"], Is.EqualTo(_catalog.Progression.Quests[6].Id));
            Assert.That(loaded["questLevel"], Is.EqualTo(7));
            Assert.That(loaded["activeSeconds"], Is.EqualTo(full.ActiveTimeMs / 1000));
            Assert.That(loaded["edition"], Is.EqualTo("Full"));
            Assert.That(loaded["carriedFrom"], Is.EqualTo("SteamDemo"));
            Assert.That(loaded["formatVersion"], Is.EqualTo(SaveCodec.Version));
            foreach (var e in events) AssertMatchesSchema(e);

            // a manual slot of the same edition: the load alone, nothing carried
            var plain = Campaign();
            var again = SaveCodec.Read(SaveCodec.Write(plain.Export(), _catalog, FullStamp, SavedAt, "full"), _catalog, FullStamp)
                .Game.InSlot("slot-2");
            var quiet = new List<TelemetryEvent>();
            using (var telemetry = new CampaignTelemetry(GameSession.Restore(_catalog, again), quiet.Add, resumed: true))
                telemetry.Loaded(again, "Full");
            Assert.That(quiet.Select(e => e.Name), Is.EqualTo(new[] { CampaignTelemetry.GameLoaded }));
            Assert.That(quiet[0]["slot"], Is.EqualTo("manual"));
            Assert.That(quiet[0]["carriedFrom"], Is.EqualTo(string.Empty));
            AssertMatchesSchema(quiet[0]);

            // a sandbox game says nothing, as ever
            var sandbox = new List<TelemetryEvent>();
            var sandboxGame = SaveCodec.Read(SaveCodec.Write(TestColony.NewSession(_catalog).Export(), _catalog, FullStamp, SavedAt, "s"),
                _catalog, FullStamp).Game;
            using (var telemetry = new CampaignTelemetry(GameSession.Restore(_catalog, sandboxGame), sandbox.Add, resumed: true))
                telemetry.Loaded(sandboxGame, "Full");
            Assert.That(sandbox, Is.Empty);
        }

        private static void AssertMatchesSchema(TelemetryEvent e)
        {
            Assert.That(CampaignTelemetry.Schema.TryGetValue(e.Name, out var fields), Is.True, $"no schema for {e}");
            Assert.That(e.Fields.Select(f => f.Key), Is.EquivalentTo(fields.Keys), e.ToString());
            foreach (var field in e.Fields)
                Assert.That(field.Value?.GetType(), Is.EqualTo(fields[field.Key]), $"{e.Name}.{field.Key}");
        }

        // ----- helpers -----

        private GameSession Campaign() => new(_catalog, TestColony.LayoutFor(_catalog), campaign: true);

        private GameSession Restore(GameSession session)
        {
            var read = SaveCodec.Read(SaveCodec.Write(session.Export(), _catalog, Stamp, SavedAt, "colony"), _catalog, Stamp);
            Assert.That(read.Ok, Is.True, read.ToString());
            return GameSession.Restore(_catalog, read.Game);
        }

        // A colony small enough to edit by hand: a mine with a worker, a hauler on the road, idle creatures, gear.
        internal byte[] SmallColonyDocument()
        {
            var session = Campaign();
            Assert.That(session.DebugUnlockAllBuildings().Ok, Is.True);
            Assert.That(session.DebugAddGold(50000).Ok, Is.True);
            Assert.That(session.DebugGrantGear(2).Ok, Is.True);
            var mineCell = session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(mineCell.HasValue, Is.True);
            Assert.That(session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, mineCell.Value)).Ok, Is.True);
            HireFighters(session, 3);
            var snapshot = session.CurrentSnapshot;
            string mine = snapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
            string warehouse = snapshot.Buildings.First(b => b.Kind == BuildingKind.Warehouse).Id;
            var units = snapshot.Units.Select(u => u.Id).ToList();
            Assert.That(session.Dispatch(new AssignWorkCommand(new[] { units[0] }, mine)).Ok, Is.True);
            Assert.That(session.Dispatch(new AssignHaulCommand(new[] { units[1] }, mine, warehouse)).Ok, Is.True);
            session.Advance(20f);
            return SaveCodec.Write(session.Export(), _catalog, Stamp, SavedAt, "small");
        }

        internal static void HireFighters(GameSession session, int count)
        {
            for (int i = 0; i < count; i++)
            {
                session.DebugAddGold(10000);
                var result = session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, session.FindSpawnCell()));
                Assert.That(result.Ok, Is.True, result.Error);
            }
        }

        private static byte[] Changed(byte[] document, Action<JsonObject, JsonObject> change)
        {
            var (header, body) = SaveCodec.Open(document);
            change(header, body);
            return SaveCodec.Pack(header, body.ToString());
        }

        private static JsonObject State(JsonObject body) => (JsonObject)body["state"];
        private static JsonObject Building(JsonObject body, int i) => (JsonObject)((JsonArray)State(body)["buildings"])[i];
        private static JsonObject Unit(JsonObject body, int i) => (JsonObject)((JsonArray)State(body)["units"])[i];
        private static JsonObject Item(JsonObject body, int i) => (JsonObject)((JsonArray)State(body)["equipment"])[i];

        private static byte[] Flip(byte[] bytes, int at)
        {
            var copy = (byte[])bytes.Clone();
            copy[at] ^= 0x01;
            return copy;
        }

        private static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            new Random(7).NextBytes(bytes);
            return bytes;
        }

        // A battle with the strongest creatures that fit, the first free item on the first of them.
        internal static CommandResult Fight(GameSession session, BattleMissionDefinition mission)
        {
            var board = mission.CreateBoard();
            var snapshot = session.CurrentSnapshot;
            var placements = new List<BattlePlacement>();
            foreach (var unit in snapshot.Units.OrderByDescending(u => u.Strength).ThenBy(u => u.Id, StringComparer.Ordinal))
            {
                if (placements.Count >= session.SquadLimit(mission)) break;
                foreach (var cell in mission.PlayerDeployment)
                {
                    if (!board.CanPlace(cell) || placements.Any(p => p.Cell == cell)) continue;
                    placements.Add(new BattlePlacement(unit.Id, cell));
                    break;
                }
            }
            var gear = new List<BattleEquipmentAssignment>();
            if (placements.Count > 0)
            {
                string fighter = placements[0].UnitId;
                foreach (var item in snapshot.Equipment.Where(e => e.OwnerUnitId == null))
                    if (!snapshot.Equipment.Any(e => e.OwnerUnitId == fighter && e.Slot == item.Slot) &&
                        !gear.Any(g => snapshot.Equipment.First(e => e.Id == g.ItemId).Slot == item.Slot))
                        gear.Add(new BattleEquipmentAssignment(item.Id, fighter));
            }
            return session.Dispatch(new StartBattleCommand(mission.MissionId, placements, gear));
        }

        // Fights until a paid win waits in the colony: a level not won yet pays in full.
        private static void WinARewardToTake(GameSession session)
        {
            session.EnableDebugBattleAccess();
            var order = session.ArenaLadder().OrderBy(m => session.MissionWins(m.MissionId) > 0 ? 1 : 0)
                .ThenBy(m => m.Level).ToList();
            foreach (var mission in order.Take(6))
            {
                var result = Fight(session, mission);
                if (session.ActiveBattle != null) session.Dispatch(new AcknowledgeBattleCommand());
                if (result.Ok && session.CurrentSnapshot.BattleReward != null) return;
                if (session.CurrentSnapshot.Units.Count < 3) HireFighters(session, 3);
            }
        }

        private static void BuyAndClearLand(GameSession session)
        {
            var land = session.CurrentSnapshot.Land;
            var block = land.Blocks.First(b => b.CanBuy);
            Assert.That(session.Dispatch(new BuyLandCommand(block.X, block.Y)).Ok, Is.True);
            Assert.That(session.Dispatch(new ClearLandCommand(block.X, block.Y)).Ok, Is.True);
            var next = session.CurrentSnapshot.Land.Blocks.FirstOrDefault(b => b.CanBuy);
            if (next.CanBuy) session.Dispatch(new BuyLandCommand(next.X, next.Y));
        }

        private static void WaitForALoadedHauler(GameSession session)
        {
            for (int i = 0; i < 2000; i++)
            {
                if (session.CurrentSnapshot.Units.Any(u =>
                        u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.Carried > 0)) return;
                session.Advance(0.25f);
            }
        }

        private void AssertSameState(GameSession expected, GameSession actual, string when)
        {
            string a = Body(expected), b = Body(actual);
            if (a == b) return;
            int at = 0;
            while (at < a.Length && at < b.Length && a[at] == b[at]) at++;
            int from = Math.Max(0, at - 120);
            Assert.Fail($"{when}: the states part at character {at}\noriginal: …{a.Substring(from, Math.Min(240, a.Length - from))}\n" +
                        $"restored: …{b.Substring(from, Math.Min(240, b.Length - from))}");
        }

        private void AssertSameGame(GameSession expected, GameSession actual, string when)
        {
            AssertSameState(expected, actual, when);
            var a = Digest(expected);
            var b = Digest(actual);
            string difference = SnapshotDigest.FirstDifference(a, b, "original", "restored");
            Assert.That(difference, Is.Null, $"{when}: {difference}");
        }

        private string Body(GameSession session)
        {
            var game = session.Export();
            return SaveCodec.WriteBody(game.State, game.StepRemainderSeconds, _catalog);
        }

        // the snapshot's lines, the unlocks as sorted sets (a set's order is its history, which a save does not keep)
        private List<string> Digest(GameSession session)
        {
            var progress = session.CurrentSnapshot.Progress;
            var lines = SnapshotDigest.Lines(session).Where(line => !line.StartsWith("Progress", StringComparison.Ordinal))
                .ToList();
            lines.Add("Progress.Level: " + progress.Level);
            lines.Add($"Progress.End: over={progress.Over} last={progress.LastLevel} cap={progress.ArenaCap}");
            lines.Add("Progress.Quest: " + progress.Quest?.Id + " " +
                      string.Join(",", progress.Quest?.Goals.Select(g => $"{g.Current}/{g.Done}") ?? Array.Empty<string>()) +
                      " complete=" + progress.Quest?.IsComplete);
            lines.Add("Progress.Buildings: " + string.Join(",", _catalog.Buildings.Where(d => d != null)
                .Select(d => $"{d.Kind}:{progress.IsBuildingUnlocked(d.Kind)}:{progress.UnlockLevel(d.Kind)}")));
            lines.Add("Progress.Units: " + string.Join(",", _catalog.Units.Where(d => d != null)
                .Select(d => $"{d.Kind}:{progress.IsUnitUnlocked(d.Kind)}")));
            lines.Add("Progress.Missions: " + string.Join(",", _catalog.Missions.Where(d => d != null)
                .Select(d => $"{d.MissionId}:{progress.IsMissionUnlocked(d.MissionId)}")));
            return lines;
        }
    }
}
