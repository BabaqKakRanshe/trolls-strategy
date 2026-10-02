using System.Linq;
using NUnit.Framework;
using TrollStrategy.UI;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>The screen UI keeps its nested documents in sorting order, whatever order Unity attached them in.</summary>
    public class UiDocumentOrderTests
    {
        [Test]
        public void Restore_PutsDocumentRootsBackInSortingOrder_AfterTheParentsOwnElements()
        {
            var parent = new VisualElement();
            var own = new VisualElement { name = "own" };
            var top = new VisualElement { name = "top" };
            var middle = new VisualElement { name = "middle" };
            var bottom = new VisualElement { name = "bottom" };
            // as Unity left the colony frame after a scene load: the bottom band before the middle one
            parent.Add(top);
            parent.Add(own);
            parent.Add(bottom);
            parent.Add(middle);

            Assert.That(UiDocumentOrder.Restore(parent, new[] { top, middle, bottom }), Is.True);
            Assert.That(parent.Children().Select(e => e.name), Is.EqualTo(new[] { "own", "top", "middle", "bottom" }));

            Assert.That(UiDocumentOrder.Restore(parent, new[] { top, middle, bottom }), Is.False, "An ordered tree is left alone");
        }

        [Test]
        public void Restore_SkipsRootsThatAreMissingOrElsewhere()
        {
            var parent = new VisualElement();
            var other = new VisualElement();
            var first = new VisualElement { name = "first" };
            var second = new VisualElement { name = "second" };
            var away = new VisualElement { name = "away" };
            parent.Add(second);
            parent.Add(first);
            other.Add(away);

            Assert.That(UiDocumentOrder.Restore(parent, new[] { first, null, away, second }), Is.True);
            Assert.That(parent.Children().Select(e => e.name), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(away.parent, Is.SameAs(other));
        }

        [Test]
        public void ScreenUiPrefab_KeepsItsDocumentsInOrder()
        {
            Assert.That(TestUi.Load().Prefab.GetComponent<UiDocumentOrder>(), Is.Not.Null,
                "TrollStrategy/Dev/Setup UI puts UiDocumentOrder on the screen UI's root");
        }
    }
}
