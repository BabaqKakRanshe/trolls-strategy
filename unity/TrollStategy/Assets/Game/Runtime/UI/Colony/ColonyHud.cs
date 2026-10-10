using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.Support;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Scene end of the colony HUD: its parts are nested UIDocuments under this GameObject, one per panel
    /// or band. Collects their roots for <see cref="ColonyHudView"/>, feeds it session and interaction
    /// changes and tells the map input which screen areas belong to the HUD. The document is never
    /// disabled (that would rebuild its tree); hiding only switches display off.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ColonyHud : MonoBehaviour
    {
        [Header("Part documents nested under this one")]
        [SerializeField] private UIDocument _topBar;
        [SerializeField] private UIDocument _quest;
        [SerializeField] private UIDocument _inspect;
        [SerializeField] private UIDocument _showcase;
        [SerializeField] private UIDocument _catalog;
        [SerializeField] private UIDocument _status;
        [SerializeField] private UIDocument _contextBar;
        [SerializeField] private UIDocument _commandFan;
        [SerializeField] private UIDocument _haulCargo;
        [SerializeField] private UIDocument _arena;
        [SerializeField] private UIDocument _menu;
        [SerializeField] private UIDocument _wiki;
        [SerializeField] private UIDocument _intro;
        [SerializeField] private UIDocument _reward;
        [SerializeField] private UIDocument _battleReward;
        [Tooltip("Developer cheat menu (F1); removed from release players.")]
        [SerializeField] private UIDocument _cheat;
        [SerializeField] private UIDocument _tooltip;
        [Tooltip("The tutorial pointer's layer (veil, hand, hint card).")]
        [SerializeField] private UIDocument _guide;

        private readonly List<(VisualElement Root, VisualElement Content)> _builtFrom = new();
        private UIDocument _document;
        private ColonyHudContext _context;
        private MapInputHandler _mapInput;
        private ColonyHudView _view;
        private bool _visible = true;
        // the launch notice waits for the view: nested documents may attach after Init
        private bool _introWanted;
        private Telemetry _introStats;

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
            if (_mapInput != null)
            {
                _mapInput.CommandFanRequested += OnCommandFanRequested;
                _mapInput.MenuRequested += OnMenuRequested;
                // an open dialog takes Esc and keeps the map's keys off while it is up
                _mapInput.EscapeOverlay = () => _view != null && _view.CloseTopOverlay();
                _mapInput.InputBlocked = () => _view != null && _view.BlocksMap;
            }
            TryBuild();
        }

        /// <summary>The root element of every part, taken from its document by <paramref name="rootOf"/>.</summary>
        public ColonyHudRoots CollectRoots(Func<UIDocument, VisualElement> rootOf)
        {
            if (rootOf == null) throw new ArgumentNullException(nameof(rootOf));
            VisualElement Of(UIDocument document) => document != null ? rootOf(document) : null;
            return new ColonyHudRoots
            {
                Screen = Of(GetComponent<UIDocument>()),
                TopBar = Of(_topBar),
                Quest = Of(_quest),
                Inspect = Of(_inspect),
                Showcase = Of(_showcase),
                Catalog = Of(_catalog),
                Status = Of(_status),
                Context = Of(_contextBar),
                Fan = Of(_commandFan),
                HaulCargo = Of(_haulCargo),
                Arena = Of(_arena),
                Menu = Of(_menu),
                Wiki = Of(_wiki),
                Intro = Of(_intro),
                Reward = Of(_reward),
                BattleReward = Of(_battleReward),
                Cheat = Of(_cheat),
                Tooltip = Of(_tooltip),
                Guide = Of(_guide)
            };
        }

        /// <summary>
        /// Opens the launch notice (<see cref="IntroPanel.OpenOnce"/>) as soon as the view is built; when it has
        /// nothing to show, the context's IntroClosed runs at once, as if it had been closed.
        /// </summary>
        public void OpenIntro(Telemetry stats)
        {
            _introStats = stats;
            _introWanted = true;
            if (_view != null) ShowIntro();
        }

        private void ShowIntro()
        {
            _introWanted = false;
            if (!_view.Intro.OpenOnce(_introStats)) _context.IntroClosed?.Invoke();
        }

        /// <summary>Plays the HUD's "no" for a refused intent or command.</summary>
        public void PlayRefusalCue() => _view?.PlayRefusal();

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
#if !(UNITY_EDITOR || UNITY_ENABLE_CHECKS)
            if (_cheat != null) Destroy(_cheat.gameObject);
            _cheat = null;
#endif
        }

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            UIInputUtils.RegisterDocument(_document);
        }

        private void OnDisable() => UIInputUtils.UnregisterDocument(_document);

        private void OnDestroy()
        {
            Unsubscribe();
            _view?.Detach();
            _context?.Showcase?.Dispose();
        }

        private void Update()
        {
            if (_context == null) return;
            // UI Builder live reload replaces a part's tree; rebuild on the new ones
            if ((_view == null || IsStale()) && !TryBuild()) return;
            // the tutorial pointer lifts its veil on a press outside its window and draws the haul's arrow to the pointer
            // Input System screen space starts at the bottom left, the panel's at the top left
            var pointer = Pointer.current;
            var at = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            _view.TrackPointer(pointer != null ? new Vector2(at.x, Screen.height - at.y) : (Vector2?)null,
                pointer != null && pointer.press.wasPressedThisFrame);
            _view.Tick(Time.unscaledDeltaTime);
            // the quest card folds and opens like its button; a HUD-only view state, not a colony command
            if (_visible && !_view.BlocksMap)
            {
                if (Hotkeys.Quest.WasPressed) _view.Quest.ToggleCollapsed();
                if (Hotkeys.Catalog.WasPressed) _view.ToggleCatalogTool();
                if (Hotkeys.Arena.WasPressed) _view.Arena.Toggle();
            }
            // K opens the book (on the hinted or inspected entry) and closes it again, unless the player types it
            // into the book's search
            if (_visible && _view.Wiki != null && Hotkeys.Wiki.WasPressed && !_view.Wiki.IsTyping &&
                (_view.Wiki.IsOpen || !_view.BlocksMap))
                _view.ToggleWiki();
            if (_visible && _view.Intro.IsOpen && Hotkeys.Confirm.WasPressed) _view.Intro.Confirm();
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            if (_visible && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                _view.ToggleCheat();
#endif
        }

        private bool TryBuild()
        {
            if (_context == null || _document == null) return false;
            var roots = CollectRoots(document => document.rootVisualElement);
            // nested documents attach to this one when they are enabled, which may come after Init
            foreach (var root in roots.Required)
                if (root == null || root.panel == null) return false;
            // a rebuilt view starts with its notice closed: one that was open opens again
            bool introOpen = _view != null && _view.Intro.IsOpen;
            _view?.Detach();
            _view = new ColonyHudView(roots, _context);
            _builtFrom.Clear();
            foreach (var root in roots.Required) _builtFrom.Add((root, FirstChild(root)));
            ApplyVisibility();
            if (_introWanted || introOpen) ShowIntro();
            return true;
        }

        private bool IsStale()
        {
            foreach (var (root, content) in _builtFrom)
                if (root.panel == null || FirstChild(root) != content) return true;
            return false;
        }

        private static VisualElement FirstChild(VisualElement root) => root.childCount > 0 ? root[0] : null;

        private void ApplyVisibility()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            _document.rootVisualElement.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            _view?.SetHudVisible(_visible);
        }

        private void OnSnapshotChanged(GameSnapshot snapshot) => _view?.Refresh(snapshot);

        private void OnMenuRequested()
        {
            if (_visible) _view?.Menu.Toggle();
        }

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
            if (_mapInput != null)
            {
                _mapInput.CommandFanRequested -= OnCommandFanRequested;
                _mapInput.MenuRequested -= OnMenuRequested;
            }
        }
    }
}
