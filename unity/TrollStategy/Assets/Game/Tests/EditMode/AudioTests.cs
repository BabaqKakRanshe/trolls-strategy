using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Presentation.Audio;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>Feedback cues, music order and the audio resources the game loads by name.</summary>
    public class AudioTests
    {
        private static readonly MusicTrack Theme = new("theme", -15f);
        private static readonly MusicTrack[] Colony = { new("a", -16f), new("b", -18f), new("c", -20f) };
        private static readonly MusicTrack[] Battle = { new("fight1", -13f), new("fight2", -15f) };

        private static MusicSequencer Sequencer(int seed = 7) => new(Theme, Colony, Battle, seed, 20f, 45f);

        [Test]
        public void Music_OpensWithTheThemeOnceThenColonyTracksAfterAQuietGap()
        {
            var music = Sequencer();
            music.Enter(SoundScene.Colony);
            Assert.That(music.Current, Is.SameAs(Theme));
            Assert.That(music.Loops, Is.False);

            music.Finished();
            Assert.That(music.Current, Is.Null, "The ambience carries the island between tracks");
            music.Advance(19.9f);
            Assert.That(music.Current, Is.Null, "The gap lasts at least 20 s");
            music.Advance(25.2f);
            Assert.That(Colony, Does.Contain(music.Current));
            Assert.That(music.Loops, Is.False);
        }

        [Test]
        public void Music_ColonyTracksNeverRepeatBackToBackAndAllComeRound()
        {
            var music = Sequencer(3);
            music.Enter(SoundScene.Colony);
            var played = new List<MusicTrack>();
            for (int i = 0; i < 60; i++)
            {
                music.Finished();
                music.Advance(45f);
                played.Add(music.Current);
            }
            for (int i = 1; i < played.Count; i++)
                Assert.That(played[i], Is.Not.SameAs(played[i - 1]), $"Track {i} repeats the one before it");
            Assert.That(played.Take(3), Is.EquivalentTo(Colony), "Every track plays before any repeats");
            Assert.That(played, Has.None.SameAs(Theme), "The theme opens the game only");
        }

        [Test]
        public void Music_BattleLoopsItsTrackAlternatesBetweenBattlesAndHandsBackToTheColony()
        {
            var music = Sequencer();
            music.Enter(SoundScene.Colony);
            int themeVersion = music.Version;

            music.Enter(SoundScene.Battle);
            Assert.That(music.Current, Is.SameAs(Battle[0]));
            Assert.That(music.Loops, Is.True);
            Assert.That(music.Version, Is.GreaterThan(themeVersion));
            music.Finished();
            Assert.That(music.Current, Is.SameAs(Battle[0]), "A looping battle track never finishes");

            music.Enter(SoundScene.BattleResult);
            Assert.That(music.Current, Is.Null, "The result stinger plays over silence");

            music.Enter(SoundScene.Colony);
            Assert.That(music.Current, Is.Null);
            music.Advance(MusicSequencer.ReturnGap + .1f);
            Assert.That(Colony, Does.Contain(music.Current), "After a short breath the colony music returns");

            music.Enter(SoundScene.Battle);
            Assert.That(music.Current, Is.SameAs(Battle[1]), "The next battle brings the other track");
        }

        [Test]
        public void Music_EnteringTheSameSceneAgainKeepsTheTrack()
        {
            var music = Sequencer();
            music.Enter(SoundScene.Colony);
            int version = music.Version;
            music.Enter(SoundScene.Colony);
            Assert.That(music.Version, Is.EqualTo(version));
            Assert.That(music.Current, Is.SameAs(Theme));
        }

        [Test]
        public void Step_WalksTheMajorPentatonicFromTheCuesOwnNote()
        {
            Assert.That(GameAudio.Step(0), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(GameAudio.Step(1), Is.EqualTo(Mathf.Pow(2f, 2f / 12f)).Within(1e-5f));
            Assert.That(GameAudio.Step(3), Is.EqualTo(Mathf.Pow(2f, 7f / 12f)).Within(1e-5f));
            Assert.That(GameAudio.Step(5), Is.EqualTo(2f).Within(1e-5f), "Five steps make an octave");
            Assert.That(GameAudio.Step(-1), Is.EqualTo(Mathf.Pow(2f, -3f / 12f)).Within(1e-5f), "F down to D");
        }

        [Test]
        public void PickTake_NeverRepeatsTheLastTakeAndReachesEveryOther()
        {
            var seen = new HashSet<int>();
            for (float roll = 0f; roll <= 1f; roll += .01f)
            {
                int take = GameAudio.PickTake(4, 2, roll);
                Assert.That(take, Is.InRange(0, 3));
                Assert.That(take, Is.Not.EqualTo(2));
                seen.Add(take);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1, 3 }));
            Assert.That(GameAudio.PickTake(1, 0, .7f), Is.Zero);
            Assert.That(GameAudio.PickTake(3, -1, .99f), Is.EqualTo(2), "With no previous take every take is open");
        }

        [Test]
        public void EveryCue_HasTakesThatLoadWholeIntoMemory()
        {
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                var takes = GameAudio.LoadTakes(sfx);
                Assert.That(takes, Is.Not.Empty, $"{sfx} has no take in Resources/Sfx");
                foreach (var take in takes)
                {
                    Assert.That(take.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), $"{take.name} must not stream");
                    Assert.That(take.length, Is.LessThan(2.5f), $"{take.name} is a feedback cue, not a piece of music");
                }
            }
            Assert.That(GameAudio.LoadTakes(Sfx.UiClick).Length, Is.GreaterThanOrEqualTo(3), "Frequent cues vary");
            Assert.That(GameAudio.LoadTakes(Sfx.Step).Length, Is.GreaterThanOrEqualTo(3), "Frequent cues vary");
        }

        [Test]
        public void MusicAndBeds_LoadFromResourcesWithMusicStreamed()
        {
            foreach (var resource in Soundscape.MusicResources)
            {
                var clip = Resources.Load<AudioClip>(resource);
                Assert.That(clip, Is.Not.Null, $"{resource} is missing");
                Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming), $"{resource} should stream");
                Assert.That(clip.length, Is.GreaterThan(30f), $"{resource} is too short for music");
            }
            foreach (var resource in Soundscape.BedResources)
            {
                var clip = Resources.Load<AudioClip>(resource);
                Assert.That(clip, Is.Not.Null, $"{resource} is missing");
                Assert.That(clip.loadType, Is.Not.EqualTo(AudioClipLoadType.DecompressOnLoad),
                    $"{resource} is a long loop; decompressed it would take tens of megabytes");
                Assert.That(clip.length, Is.GreaterThan(30f), $"{resource} loops too soon to go unnoticed");
            }
        }
    }
}
