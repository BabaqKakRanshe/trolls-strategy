using TrollStrategy.Application;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Scene end of the colony HUD: owns the UIDocument, feeds session and interaction changes to
    /// <see cref="ColonyHudView"/> and tells the map input which screen areas belong to the HUD.
    /// The document is never disabled (that would rebuild its tree); hiding only switches display off.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ColonyHud : MonoBehaviour
    {
        private UIDocument _document;
        private ColonyHudContext _context;
        private MapInputHandler _mapInput;
        private ColonyHudView _view;
        private bool _visible = true;

        public ColonyHudView View => _view;
        public UIDocument Document => _document;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                ApplyVisibility();
            }
        }

        public void Init(ColonyHudContext context, MapInputHandler mapInput)
        {
            Unsubscribe();
            _context = context;
            _mapInput = mapInput;
            _context.Session.OnSnapshotChanged += OnSnapshotChanged;
            _context.Interaction.OnInteractionChanged += OnInteractionChanged;
            if (_mapInput != null) _mapInput.CommandFanRequested += OnCommandFanRequested;
            TryBuild();
        }

        /// <summary>Plays the HUD's "no" for a refused intent or command.</summary>
        public void PlayRefusalCue() => _view?.PlayRefusal();

        private void Awake() => _document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            UIInputUtils.RegisterDocument(_document);
        }

        private void OnDisable() => UIInputUtils.UnregisterDocument(_document);

        private void OnDestroy()
        {
            Unsubscribe();
            _context?.Showcase?.Dispose();
        }

        private void Update()
        {
            if (_context == null) return;
            // UI Builder live reload replaces the document's tree; rebuild on the new one
            if ((_view == null || _view.Root.panel == null) && !TryBuild()) return;
            _view.Tick(Time.unscaledDeltaTime);
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            if (_visible && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                _view.ToggleCheat();
#endif
        }

        private bool TryBuild()
        {
            if (_context == null || _document == null) return false;
            var root = _document.rootVisualElement;
            if (root == null || root.Q("hud-screen") == null) return false;
            // the document root must cover the whole screen, yet only real HUD parts may catch the pointer
            root.StretchToParentSize();
            root.pickingMode = PickingMode.Ignore;
            _view = new ColonyHudView(root, _context);
            ApplyVisibility();
            return true;
        }

        private void ApplyVisibility()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            _document.rootVisualElement.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            _view?.SetHudVisible(_visible);
        }

        private void OnSnapshotChanged(GameSnapshot snapshot) => _view?.Refresh(snapshot);

        private void OnInteractionChanged()
        {
            if (_view != null) _view.OnInteractionChanged(_context.Session.CurrentSnapshot);
        }

        private void OnCommandFanRequested(Vector2 screenPosition)
        {
            var panel = _view?.Root.panel;
            if (panel == null) return;
            // Input System screen space starts at the bottom left, the panel's at the top left
            var point = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            _view.OpenCommandFan(point);
        }

        private void Unsubscribe()
        {
            if (_context != null)
            {
                _context.Session.OnSnapshotChanged -= OnSnapshotChanged;
                _context.Interaction.OnInteractionChanged -= OnInteractionChanged;
            }
            if (_mapInput != null) _mapInput.CommandFanRequested -= OnCommandFanRequested;
        }
    }
}
