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
    /// <para>
    /// As the camera backs off the panel grows, so that its largest text keeps <see cref="ReadableTextSize"/> px
    /// on a 1080 px tall screen. It owns its transform scale for that; owners size it through <see cref="Scale"/>.
    /// Panels under a camera that does not zoom turn <see cref="KeepReadable"/> off and keep their world size.
    /// Where a world unit takes fewer than <see cref="FarPixelsPerUnit"/> px on screen, the content has the
    /// <c>world-panel--far</c> class, under which styles drop details.
    /// </para>
    /// <para>
    /// Panels stand on <see cref="Layer"/>, which the colony renderer draws after post-processing
    /// (<see cref="WorldUiFeature"/>), so depth of field and grading never reach them.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(1000)]   // after the camera rigs have placed the camera for this frame
    public sealed class WorldPanel : MonoBehaviour
    {
        /// <summary>Panel pixels per world unit in the world panel settings.</summary>
        public const float PixelsPerUnit = 100f;
        /// <summary>Screen height the sizes below are measured on, as the HUD's reference resolution.</summary>
        public const float ReferenceScreenHeight = 1080f;
        /// <summary>The largest text of a panel does not get smaller on screen than this, px.</summary>
        public const float ReadableTextSize = 18f;
        /// <summary>A world unit smaller on screen than this, px, makes the content far.</summary>
        public const float FarPixelsPerUnit = 30f;
        public const string FarClass = "world-panel--far";
        /// <summary>Unity's built-in UI layer: drawn by <see cref="WorldUiFeature"/>, not by the renderer's own passes.</summary>
        public const int Layer = 5;

        [Tooltip("USS classes of the content element, for panels placed in prefabs.")]
        [SerializeField] private string[] _classes = Array.Empty<string>();
        [Tooltip("Draw order among sprites in the same queue: fighters and buildings draw at 10.")]
        [SerializeField] private int _sortingOrder = 25;
        [Tooltip("Grow as the camera backs off so the text stays readable. Off keeps the world size and leaves " +
                 "the transform scale to the owner, as under a camera that does not zoom.")]
        [SerializeField] private bool _keepReadable = true;

        private static bool s_warned;
        private VisualElement _content;
        private UIDocument _document;
        private Renderer _renderer;
        private float _scale = 1f;
        private bool _far;

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

        /// <summary>The point of the content that stands at the transform position; the panel grows away from it.</summary>
        public Pivot Pivot
        {
            get => Document.pivot;
            set => Document.pivot = value;
        }

        private UIDocument Document => _document != null ? _document : _document = GetComponent<UIDocument>();

        /// <summary>Grow as the camera backs off (see the class summary); off leaves the transform scale alone.</summary>
        public bool KeepReadable
        {
            get => _keepReadable;
            set => _keepReadable = value;
        }

        /// <summary>The owner's own size, such as a pop; the transform scale is this times <see cref="Hold"/>.</summary>
        public float Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                ApplyScale();
            }
        }

        /// <summary>
        /// How many times the panel grows where the camera is now so that its text stays readable; 1 up close.
        /// Measured with <see cref="KeepReadable"/> off too, for owners that grow a whole group by it.
        /// </summary>
        public float Hold { get; private set; } = 1f;

        /// <summary>The content has the <c>world-panel--far</c> class.</summary>
        public bool Far => _far;

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
            var go = new GameObject(name) { layer = Layer };
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

        /// <summary>World units the screen height spans at <paramref name="position"/>; 0 behind the camera.</summary>
        public static float ViewHeight(Camera camera, Vector3 position)
        {
            if (camera.orthographic) return 2f * camera.orthographicSize;
            var view = camera.transform;
            float depth = Vector3.Dot(position - view.position, view.forward);
            return depth > 0f ? 2f * depth * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) : 0f;
        }

        /// <summary>
        /// How many times a panel whose largest text is <paramref name="textSize"/> panel px grows so that the text
        /// keeps <see cref="ReadableTextSize"/> px on screen where the screen spans <paramref name="viewHeight"/>
        /// world units. Never below 1; 1 when there is nothing to measure.
        /// </summary>
        public static float HoldFor(float viewHeight, float textSize)
        {
            if (!(viewHeight > 0f) || !(textSize > 0f)) return 1f;
            return Mathf.Max(1f, ReadableTextSize * PixelsPerUnit * viewHeight / (ReferenceScreenHeight * textSize));
        }

        /// <summary>A world unit takes fewer than <see cref="FarPixelsPerUnit"/> px where the screen spans
        /// <paramref name="viewHeight"/> world units.</summary>
        public static bool IsFar(float viewHeight) => viewHeight * FarPixelsPerUnit > ReferenceScreenHeight;

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

        /// <summary>Sizes the panel for <paramref name="camera"/> where it stands now; every frame for the main camera.</summary>
        public void Measure(Camera camera)
        {
            if (_content == null || camera == null) return;
            float view = ViewHeight(camera, transform.position);
            if (view <= 0f) return;
            Hold = HoldFor(view, TextSize());
            bool far = IsFar(view);
            if (far != _far)
            {
                _far = far;
                _content.EnableInClassList(FarClass, far);
            }
            ApplyScale();
        }

        private void OnEnable()
        {
            gameObject.layer = Layer;           // panels placed in prefabs too
            Attach();
            ApplySorting();
        }

        // the document builds its root and its world-space renderer in its own OnEnable, which may come after this one
        private void LateUpdate()
        {
            if (_content != null && _content.parent == null) Attach();
            if (_renderer == null) ApplySorting();
            Measure(Camera.main);
        }

        // the largest text among the labels (AddLabel puts them straight under the content) once they are laid out
        private float TextSize()
        {
            float size = 0f;
            for (int i = 0; i < _content.childCount; i++)
                if (_content[i] is TextElement text && !float.IsNaN(text.layout.width))
                    size = Mathf.Max(size, text.resolvedStyle.fontSize);
            return size;
        }

        private void ApplyScale()
        {
            if (!_keepReadable) return;
            var scale = Vector3.one * (_scale * Hold);
            if (transform.localScale != scale) transform.localScale = scale;
        }

        private void ApplySorting()
        {
            if (_renderer == null && !TryGetComponent(out _renderer)) return;
            _renderer.sortingOrder = _sortingOrder;
        }

        private void Attach()
        {
            if (_content == null) return;
            var root = Document != null ? Document.rootVisualElement : null;
            if (root == null || _content.parent == root) return;
            root.pickingMode = PickingMode.Ignore;
            root.Add(_content);
        }
    }
}
