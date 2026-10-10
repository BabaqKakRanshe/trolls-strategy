using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The arrow drawn while the player picks where to carry (GuideRoute): a dotted track from the source to a notched
    /// head at the pointer, with tokens of the goods riding it, and the goods the HUD gives the tokens.
    /// </summary>
    public class GuideRouteTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private static readonly Vector2 Source = new(400f, 620f);
        private static readonly Vector2 Pointer = new(980f, 560f);

        private readonly List<Vector2> _track = new();
        private readonly List<GuideRoute.Token> _tokens = new();
        private readonly Vector2[] _head = new Vector2[GuideRoute.HeadCorners];

        [Test]
        public void PointerOnTheSource_DrawsNoArrow()
        {
            bool drawn = GuideRoute.Trace(Source, Source + new Vector2(20f, 12f), 0f, _track, _tokens, _head);

            Assert.That(drawn, Is.False);
            Assert.That(_track, Is.Empty);
            Assert.That(_tokens, Is.Empty);
        }

        [Test]
        public void Head_PointsAtThePointer_TheWayTheTrackComes()
        {
            Assert.That(GuideRoute.Trace(Source, Pointer, 0f, _track, _tokens, _head), Is.True);
            Vector2 tip = _head[0], barb = _head[1], notch = _head[2], otherBarb = _head[3];

            Assert.That(Vector2.Distance(tip, Pointer), Is.LessThan(.01f), "The tip is the pointer");
            var axis = (tip - notch).normalized;
            var way = (tip - _track[_track.Count - 3]).normalized;
            Assert.That(Vector2.Dot(axis, way), Is.GreaterThan(.95f), "The head goes on the way the track comes");
            Assert.That(Vector2.Distance(barb, notch), Is.EqualTo(Vector2.Distance(otherBarb, notch)).Within(.01f),
                "The barbs stand evenly on both sides");
            Assert.That(Vector2.Dot(barb - notch, axis), Is.LessThan(0f), "A barb sweeps back past the notch");
            Assert.That(Vector2.Dot(otherBarb - notch, axis), Is.LessThan(0f), "So does the other");
        }

        [Test]
        public void Track_RunsInEvenStepsFromTheSourceIntoTheNotch()
        {
            GuideRoute.Trace(Source, Pointer, 0f, _track, _tokens, _head);

            Assert.That(_track.Count, Is.GreaterThan(30), "A dotted track runs from the source");
            Assert.That(Vector2.Distance(_track[0], Source), Is.LessThan(GuideRoute.TrackSpacing), "It starts at the source");
            for (int i = 1; i < _track.Count; i++)
            {
                Assert.That(Vector2.Distance(_track[i], _track[i - 1]), Is.EqualTo(GuideRoute.TrackSpacing).Within(.5f),
                    $"Dot {i} keeps the step");
                Assert.That(Vector2.Distance(_track[i], Pointer), Is.LessThan(Vector2.Distance(_track[i - 1], Pointer)),
                    $"Dot {i} is nearer the pointer");
            }
            float notch = Vector2.Distance(_head[2], Pointer);
            Assert.That(Vector2.Distance(_track[_track.Count - 1], Pointer),
                Is.GreaterThan(notch - .5f).And.LessThan(notch + GuideRoute.TrackSpacing + .5f), "It ends at the notch");
        }

        [Test]
        public void Tokens_RideTowardThePointer_EachKeepingItsTurn()
        {
            var before = new List<GuideRoute.Token>();
            GuideRoute.Trace(Source, Pointer, 3f, _track, before, _head);
            GuideRoute.Trace(Source, Pointer, 3.5f, _track, _tokens, _head);

            Assert.That(before.Count, Is.GreaterThanOrEqualTo(4), "Several tokens ride the track at once");
            for (int i = 1; i < before.Count; i++)
                Assert.That(before[i - 1].Turn - before[i].Turn, Is.EqualTo(1), "Each token carries the next of the goods");
            // half a second moves a token well under the spacing: the nearest token later is the same one
            var token = before[before.Count / 2];
            var moved = _tokens.OrderBy(t => Vector2.Distance(t.Centre, token.Centre)).First();
            Assert.That(Vector2.Distance(moved.Centre, Pointer), Is.LessThan(Vector2.Distance(token.Centre, Pointer)),
                "Half a second later the token is further along");
            Assert.That(moved.Turn, Is.EqualTo(token.Turn), "It still carries the same good");
            Assert.That(token.Radius, Is.EqualTo(GuideRoute.TokenRadius).Within(.01f), "On the way a token is full size");
        }

        [Test]
        public void Tokens_GrowOutOfTheSource_AndShrinkIntoTheHead()
        {
            float notchAt = 0f;
            for (float time = 0f; time < 3f; time += .05f)
            {
                GuideRoute.Trace(Source, Pointer, time, _track, _tokens, _head);
                notchAt = Vector2.Distance(_head[2], Pointer);
                foreach (var token in _tokens)
                {
                    float fromSource = Vector2.Distance(token.Centre, Source);
                    float toNotch = Vector2.Distance(token.Centre, Pointer) - notchAt;
                    if (fromSource < 15f || toNotch < 8f)
                        Assert.That(token.Radius, Is.LessThan(GuideRoute.TokenRadius * .5f),
                            $"At {time:0.00} s a token by the source or the head is small");
                    Assert.That(toNotch, Is.GreaterThan(-1f), "No token comes out past the notch");
                }
            }
        }

        [Test]
        public void ChoosingWhereToCarry_TheTokensCarryTheChosenGoods_OrWhatAllMeans()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            var session = new GameSession(catalog, TestColony.LayoutFor(catalog), campaign: true);
            var interaction = new InteractionController(session);
            var hud = TestUi.Colony(session, interaction);
            Assert.That(hud.Guide, Is.Not.Null, "The UI prefab needs its Guide layer: run TrollStrategy/Dev/Setup UI");
            Assert.That(session.DebugAddGold(500).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, session.FindSpawnCell())).Ok, Is.True);
            void Refresh() => hud.OnInteractionChanged(session.CurrentSnapshot);

            interaction.ClickUnit(session.CurrentSnapshot.Units.First(u => u.UnitKind == UnitKind.Goblin).Id, false);
            Refresh();
            UiFeel.Press(hud.ContextBar.HaulButton);
            Refresh();
            string warehouse = TutorialPlaces.Warehouse(session.CurrentSnapshot).Id;
            interaction.ChooseBuilding(warehouse);
            Refresh();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.ChoosingHaulCargo));

            // everything: the goods the cargo dialog's "Всё" card shows
            var everything = HaulCargoDialog.Everything(session.CurrentSnapshot, warehouse, interaction.HaulCargoChoices)
                .Select(r => catalog.TryGetResource(r)?.Icon).Where(icon => icon != null).ToList();
            Assert.That(everything, Is.Not.Empty, "The warehouse hands out goods with pictures");
            interaction.ConfirmHaulCargo();
            Refresh();
            hud.Tick(.1f);
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.ChoosingHaulDestination));
            Assert.That(hud.Guide.RouteCargo, Is.EqualTo(everything), "All goods: the tokens carry what «Всё» shows, in turn");

            // one good chosen: every token carries it
            interaction.ChangeHaulCargo();
            Refresh();
            ResourceKind? chosen = null;
            foreach (var good in interaction.HaulCargoChoices)
            {
                if (catalog.TryGetResource(good)?.Icon == null) continue;
                interaction.ToggleHaulCargo(good);
                if (interaction.HaulCargoHasDestination)
                {
                    chosen = good;
                    break;
                }
                interaction.ToggleHaulCargo(good);
            }
            Assert.That(chosen, Is.Not.Null, "Some good of the warehouse has somewhere to go");
            interaction.ConfirmHaulCargo();
            Refresh();
            hud.Tick(.1f);
            Assert.That(hud.Guide.RouteCargo, Is.EqualTo(new[] { catalog.TryGetResource(chosen.Value).Icon }));

            // the order given or dropped: the arrow carries nothing
            interaction.CancelOrClear();
            Refresh();
            hud.Tick(.1f);
            Assert.That(hud.Guide.RouteCargo, Is.Empty);
        }
    }
}
