using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The screen UI prefab cloned without a panel: each nested UIDocument becomes an element holding its
    /// UXML, inside its parent's element in sorting order, as Unity nests them in the scene.
    /// </summary>
    public sealed class TestUi
    {
        public const string PrefabPath = "Assets/Game/UI/Prefabs/UI.prefab";

        private readonly Dictionary<UIDocument, VisualElement> _roots = new();

        private TestUi(GameObject prefab)
        {
            Prefab = prefab;
            Screen = Clone(prefab.GetComponent<UIDocument>(), null);
        }

        public GameObject Prefab { get; }
        public VisualElement Screen { get; }

        public static TestUi Load()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            return new TestUi(prefab);
        }

        /// <summary>A colony HUD over a fresh clone of the prefab, with stub battle and guide hooks.</summary>
        public static ColonyHudView Colony(GameSession session, InteractionController interaction) =>
            Colony(new ColonyHudContext(session, interaction)
            {
                ToggleGuides = () => { },
                GuidesVisible = () => false,
                OpenBattle = _ => { }
            });

        public static ColonyHudView Colony(ColonyHudContext context)
        {
            var ui = Load();
            var hud = ui.Prefab.GetComponentInChildren<ColonyHud>(true);
            Assert.That(hud, Is.Not.Null, "The UI prefab has no ColonyHud");
            return new ColonyHudView(hud.CollectRoots(ui.RootOf), context);
        }

        public VisualElement RootOf(UIDocument document) => _roots[document];

        private VisualElement Clone(UIDocument document, VisualElement parent)
        {
            var root = new VisualElement { name = document.name + "-container", pickingMode = PickingMode.Ignore };
            var classes = document.GetComponent<UiDocumentClasses>();
            if (classes != null) classes.ApplyTo(root);
            if (document.visualTreeAsset != null) document.visualTreeAsset.CloneTree(root);
            parent?.Add(root);
            _roots.Add(document, root);

            var children = new List<UIDocument>();
            foreach (Transform child in document.transform)
                if (child.TryGetComponent(out UIDocument nested)) children.Add(nested);
            foreach (var nested in children.OrderBy(d => d.sortingOrder))
                Clone(nested, root);
            return root;
        }
    }
}
