using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.WorldUi
{
    /// <summary>
    /// A small piece of UI Toolkit standing in the world: a world-space UIDocument, sized by its content,
    /// whose elements the owner builds in code (a label, a health bar). The panel settings map 100 panel
    /// pixels to one world unit, so a 24px label is 0.24 units tall; styles live in WorldUi.uss. Nothing
    /// in it takes the pointer. Disabling the GameObject drops the document's tree; the content is
    /// attached again when it comes back, so pooled panels keep what they show.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WorldPanel : MonoBehaviour
    {
        [Tooltip("USS classes of the content element, for panels placed in prefabs.")]
        [SerializeField] private string[] _classes = Array.Empty<string>();
        [Tooltip("Draw order among sprites in the same queue: fighters and buildings draw at 10.")]
        [SerializeField] private int _sortingOrder = 25;

        private static bool s_warned;
        private VisualElement _content;
        private UIDocument _document;
        private Renderer _renderer;

        /// <summary>World-space panel settings for panels made in code; the bootstrap sets them.</summary>
        public static PanelSettings Settings { get; private set; }

        public static void Configure(PanelSettings settings) => Settings = settings;

        /// <summary>The element the owner fills; exists (unattached) even before the panel does, as in tests.</summary>
        public VisualElement Content
        {
            get
            {
                if (_content == null)
                {
                    _content = new VisualElement { name = "world-panel", pickingMode = PickingMode.Ignore };
                    foreach (var name in _classes)
                        if (!string.IsNullOrWhiteSpace(name)) _content.AddToClassList(name.Trim());
                }
                Attach();
                return _content;
            }
        }

        public bool Visible
        {
            get => Content.style.display != DisplayStyle.None;
            set => Content.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// A panel under <paramref name="parent"/> in the configured world-space settings; its transform
        /// position is the content's <paramref name="pivot"/>, and it draws over sprites of a lower
        /// <paramref name="sortingOrder"/>.
        /// </summary>
        public static WorldPanel Create(string name, Transform parent, int sortingOrder, Pivot pivot = Pivot.Center,
            params string[] classes)
        {
            if (Settings == null && UnityEngine.Application.isPlaying && !s_warned)
            {
                s_warned = true;
                Debug.LogError("World labels have no panel settings; the bootstrap's World Panel field is empty.");
            }
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = Settings;
            document.worldSpaceSizeMode = WorldSpaceSizeMode.Dynamic;
            document.pivot = pivot;
            var panel = go.AddComponent<WorldPanel>();
            panel._classes = classes ?? Array.Empty<string>();
            panel._sortingOrder = sortingOrder;
            panel.ApplySorting();
            return panel;
        }

        /// <summary>A label in the content; its classes set the size and colour.</summary>
        public Label AddLabel(string classes)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            foreach (var name in (classes ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                label.AddToClassList(name);
            Content.Add(label);
            return label;
        }

        public void Face(Camera camera)
        {
            if (camera != null) transform.rotation = camera.transform.rotation;
        }

        private void OnEnable()
        {
            Attach();
            ApplySorting();
        }

        // the document builds its root and its world-space renderer in its own OnEnable, which may come after this one
        private void LateUpdate()
        {
            if (_content != null && _content.parent == null) Attach();
            if (_renderer == null) ApplySorting();
        }

        private void ApplySorting()
        {
            if (_renderer == null && !TryGetComponent(out _renderer)) return;
            _renderer.sortingOrder = _sortingOrder;
        }

        private void Attach()
        {
            if (_content == null) return;
            if (_document == null) _document = GetComponent<UIDocument>();
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null || _content.parent == root) return;
            root.pickingMode = PickingMode.Ignore;
            root.Add(_content);
        }
    }
}
