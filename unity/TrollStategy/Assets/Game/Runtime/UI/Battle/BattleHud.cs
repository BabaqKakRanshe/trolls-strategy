using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Scene end of the battle HUD: its parts are nested UIDocuments under this GameObject, which lives
    /// in the colony's UI and stays hidden until a battle opens it. Collects the part roots for
    /// <see cref="BattleHudView"/> and serves the battle as its <see cref="IBattleScreen"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleHud : MonoBehaviour, IBattleScreen
    {
        public const string OpenClass = "is-open";

        [Header("Part documents nested under this one")]
        [SerializeField] private UIDocument _header;
        [SerializeField] private UIDocument _deploymentBand;
        [SerializeField] private UIDocument _roster;
        [SerializeField] private UIDocument _selected;
        [SerializeField] private UIDocument _actions;
        [SerializeField] private UIDocument _replay;
        [SerializeField] private UIDocument _banner;

        private readonly List<(VisualElement Root, VisualElement Content)> _builtFrom = new();
        private UIDocument _document;
        private BattleHudView _view;
        private BattleDeployment _deployment;
        private bool _open;

        public event Action StartRequested;
        public event Action PauseToggled;
        public event Action<float> SpeedChosen;
        public event Action CloseRequested;

        public BattleHudView View => _view;
        public bool IsOpen => _open;

        /// <summary>The root element of every part, taken from its document by <paramref name="rootOf"/>.</summary>
        public BattleHudRoots CollectRoots(Func<UIDocument, VisualElement> rootOf)
        {
            if (rootOf == null) throw new ArgumentNullException(nameof(rootOf));
            VisualElement Of(UIDocument document) => document != null ? rootOf(document) : null;
            return new BattleHudRoots
            {
                Screen = Of(GetComponent<UIDocument>()),
                Header = Of(_header),
                Deployment = Of(_deploymentBand),
                Roster = Of(_roster),
                Selected = Of(_selected),
                Actions = Of(_actions),
                Replay = Of(_replay),
                Banner = Of(_banner)
            };
        }

        public void Open(BattleDeployment deployment)
        {
            if (!EnsureView())
                throw new InvalidOperationException("The battle HUD's documents are not attached to a panel yet");
            _deployment = deployment;
            _open = true;
            ApplyVisibility();
            _view.Open(deployment);
        }

        public void Refuse() => _view?.Refuse();

        public void BeginReplay() => _view?.BeginReplay();

        public void ShowReplay(bool paused, float speed, float seconds, int alivePlayers, int aliveEnemies) =>
            _view?.ShowReplay(paused, speed, seconds, alivePlayers, aliveEnemies);

        public void ShowResult(BattleOutcome outcome, int survived, int fallen, int lostItems) =>
            _view?.ShowResult(outcome, survived, fallen, lostItems);

        public void Close()
        {
            _view?.Close();
            _deployment = null;
            _open = false;
            ApplyVisibility();
        }

        private void Awake() => _document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            UIInputUtils.RegisterDocument(_document);
            ApplyVisibility();
        }

        private void OnDisable() => UIInputUtils.UnregisterDocument(_document);

        private void Update()
        {
            // UI Builder live reload replaces a part's tree; rebuild on the new ones
            if (_view != null && IsStale())
            {
                _view = null;
                if (EnsureView() && _open && _deployment != null) _view.Open(_deployment);
            }
            if (_open) _view?.Tick();
        }

        private bool EnsureView()
        {
            if (_view != null) return true;
            if (_document == null) _document = GetComponent<UIDocument>();
            var roots = CollectRoots(document => document.rootVisualElement);
            foreach (var root in roots.Required)
                if (root == null || root.panel == null) return false;
            _view = new BattleHudView(roots);
            _view.StartRequested += () => StartRequested?.Invoke();
            _view.PauseToggled += () => PauseToggled?.Invoke();
            _view.SpeedChosen += speed => SpeedChosen?.Invoke(speed);
            _view.CloseRequested += () => CloseRequested?.Invoke();
            _builtFrom.Clear();
            foreach (var root in roots.Required) _builtFrom.Add((root, root.childCount > 0 ? root[0] : null));
            return true;
        }

        private bool IsStale()
        {
            foreach (var (root, content) in _builtFrom)
                if (root.panel == null || (root.childCount > 0 ? root[0] : null) != content) return true;
            return false;
        }

        // .battle-screen hides the document, in the editor too; .is-open shows it for a battle
        private void ApplyVisibility()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            _document.rootVisualElement.EnableInClassList(OpenClass, _open);
        }
    }
}
