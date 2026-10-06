using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.Presentation.WorldUi;
using TrollStrategy.Support;
using TrollStrategy.UI;

namespace TrollStrategy.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GameContentCatalog _catalog;
        [Tooltip("Play the catalog's tutorial and quest chain. Off: sandbox with every building and creature open.")]
        [SerializeField] private bool _campaign = true;

        [Header("World & Map")]
        [SerializeField] private TilemapWorldView _worldView;
        [SerializeField] private Camera _camera;

        [Header("Visual Managers")]
        [SerializeField] private BuildingVisualsManager _buildingManager;
        [SerializeField] private UnitVisualsManager _unitManager;
        [SerializeField] private PlacementPreviewRenderer _placementPreview;
        [SerializeField] private SelectionBoxRenderer _selectionBox;
        [SerializeField] private HaulRouteVisualizer _routeVisualizer;
        [SerializeField] private MapInputHandler _inputHandler;
        [Tooltip("Shows the bought, wild and cleared land on the colony island; its IslandView is found in the scene.")]
        [SerializeField] private LandPresenter _land;

        [Header("UI")]
        [SerializeField] private ColonyHud _hud;
        [SerializeField] private BattleHud _battleHud;
        [Tooltip("Version, FPS and \"send logs\" over every screen; reports carry the colony's state.")]
        [SerializeField] private SupportHud _support;
        [Tooltip("World-space panel settings for labels, numbers and bars over things in the world.")]
        [SerializeField] private PanelSettings _worldPanel;
        [Tooltip("Translation files (LocalizationSetup fills it).")]
        [SerializeField] private LanguageTable _languages;

        private GameSession _session;
        private InteractionController _interaction;
        private CampaignTelemetry _telemetry;
        // the pause menu stops the colony's time; the session never advances while it is up
        private bool _paused;
        private PanelSettings _screenPanel;
        private Vector2Int _designedResolution;

        public GameSession Session => _session;
        public InteractionController Interaction => _interaction;
        public Camera ColonyCamera => _camera;
        /// <summary>The colony HUD; the battle scene hides it while it covers the screen.</summary>
        public bool HudVisible
        {
            get => _hud != null && _hud.Visible;
            set
            {
                if (_hud != null) _hud.Visible = value;
            }
        }
        /// <summary>The battle's screen UI; hidden in the colony until a battle opens it.</summary>
        public IBattleScreen BattleScreen => _battleHud;
        public MapInputHandler MapInput => _inputHandler;
        public SelectionBoxRenderer SelectionBox => _selectionBox;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            GameSettings.Load();
            Localization.Use(_languages);
            Localization.Load();
            GameSettings.Changed += ApplyUiScale;
            WorldPanel.Configure(_worldPanel);

            if (_catalog == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} requires a serialized content catalog reference.", this);
                enabled = false;
                return;
            }
            if (!ContentPrefabs.Validate(_catalog, out var contentError))
            {
                Debug.LogError($"{nameof(GameBootstrap)}: content prefabs are invalid:\n{contentError}", this);
                enabled = false;
                return;
            }

            if (_buildingManager == null) _buildingManager = FindAnyObjectByType<BuildingVisualsManager>();
            if (_unitManager == null) _unitManager = FindAnyObjectByType<UnitVisualsManager>();
            if (_worldView == null) _worldView = FindAnyObjectByType<TilemapWorldView>();
            if (_placementPreview == null) _placementPreview = FindAnyObjectByType<PlacementPreviewRenderer>();
            if (_selectionBox == null) _selectionBox = FindAnyObjectByType<SelectionBoxRenderer>();
            if (_routeVisualizer == null) _routeVisualizer = FindAnyObjectByType<HaulRouteVisualizer>();
            if (_inputHandler == null) _inputHandler = FindAnyObjectByType<MapInputHandler>();
            if (_land == null) _land = FindAnyObjectByType<LandPresenter>();
            if (_hud == null) _hud = FindAnyObjectByType<ColonyHud>();
            if (_battleHud == null) _battleHud = FindAnyObjectByType<BattleHud>();
            if (_support == null) _support = FindAnyObjectByType<SupportHud>();

            if (_worldView == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} needs a {nameof(TilemapWorldView)} to read scene buildings.", this);
                enabled = false;
                return;
            }

            // Building prefabs placed in the scene are the starting colony; the session validates the layout.
            var placements = SceneBuildingPlacements.Collect(_worldView, _catalog);
            var startingBuildings = new List<StartingBuilding>(placements.Count);
            foreach (var placement in placements) startingBuildings.Add(placement.Building);
            bool campaign = _campaign && _catalog.Progression != null;
            if (_campaign && !campaign)
                Debug.LogError($"{nameof(GameBootstrap)}: the catalog has no progression; starting a sandbox game.", this);
            try
            {
                _session = new GameSession(_catalog, startingBuildings, campaign);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"{nameof(GameBootstrap)}: scene building layout is invalid. {exception.Message}", this);
                enabled = false;
                return;
            }
            var sceneViews = new Dictionary<string, BuildingView>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
                sceneViews.Add(_session.StartingBuildingIds[i], placements[i].View);
            _interaction = new InteractionController(_session);
            // where players stop: quests, battles and a heartbeat go to analytics while the player allows it
            _telemetry = new CampaignTelemetry(_session, e => Telemetry.Game.Record(e.Name, e.Fields));
            if (_support != null) _support.Init(() => SessionDigest.Describe(_session));

            if (_buildingManager != null)
                _buildingManager.Init(_session, _interaction, _worldView, _catalog, sceneViews);

            if (_unitManager != null)
                _unitManager.Init(_session, _interaction, _catalog, _worldView, _buildingManager);

            if (_placementPreview != null)
                _placementPreview.Init(_session, _interaction, _worldView, _catalog, _camera);

            if (_selectionBox != null)
                _selectionBox.Init(_interaction, _unitManager, _camera, _worldView, _buildingManager);

            if (_routeVisualizer != null)
                _routeVisualizer.Init(_session, _interaction, _worldView, _camera);

            // the trails creatures tread into the lawn, under the cell grid
            var trails = new GameObject("Trails").AddComponent<TrailView>();
            trails.transform.SetParent(_worldView.Grid.transform, false);
            trails.Init(_session, _worldView);

            if (_inputHandler != null)
                _inputHandler.Init(_interaction, _selectionBox);

            if (_land != null)
                _land.Init(_session, _interaction, _worldView, _camera, FindAnyObjectByType<IslandView>());
            else if (_session.CurrentSnapshot.Land != null)
                Debug.LogError($"{nameof(GameBootstrap)} has no {nameof(LandPresenter)}; the island does not show the land.", this);

            if (_hud != null)
            {
                // the tutorial pointer finds buildings, creatures and cells on the screen through the colony camera
                var locator = new ScreenLocator(_camera, _worldView, _buildingManager, _unitManager);
                var context = new ColonyHudContext(_session, _interaction)
                {
                    ToggleGuides = _routeVisualizer != null ? _routeVisualizer.ToggleGuides : null,
                    GuidesVisible = _routeVisualizer != null ? () => _routeVisualizer.GuidesVisible : null,
                    OpenBattle = mission => BattleSceneController.Open(this, mission),
                    SetPaused = SetPaused,
                    Restart = Restart,
                    IntroClosed = PlayFirstFlight,
                    ReportBug = _support != null ? _support.ReportFromMenu : null,
                    Languages = Localization.Languages,
                    CurrentLanguage = () => Localization.Current,
                    SetLanguage = Localization.Select,
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
                    OpenQuickBattle = mission => BattleSceneController.OpenQuick(this, mission),
#endif
                    Showcase = new BuildingShowcase(),
                    MapToScreen = locator.MapToScreen,
                    UnitToScreen = locator.UnitToScreen,
                    BuildingToScreen = locator.BuildingToScreen
                };
                _hud.Init(context, _inputHandler);
                _screenPanel = _hud.Document != null ? _hud.Document.panelSettings : null;
                if (_screenPanel != null) _designedResolution = _screenPanel.referenceResolution;
                ApplyUiScale();
                // a new version opens with the alpha notice; the first launch flies in once it is closed
                if (_hud.View == null || !_hud.View.Intro.OpenOnce(Telemetry.Game)) PlayFirstFlight();
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
                if (_support != null) _support.SetCheats(() => _hud.View?.ToggleCheat());
#endif
            }
            else Debug.LogError($"{nameof(GameBootstrap)} has no {nameof(ColonyHud)}; the colony has no HUD.", this);

            // every command answers with sound, a mark and, if refused, why
            var feedback = new GameObject("ColonyFeedback", typeof(ColonyFeedback)).GetComponent<ColonyFeedback>();
            feedback.transform.SetParent(transform, false);
            feedback.Init(_session, _interaction, _worldView, _buildingManager,
                _hud != null ? _hud.PlayRefusalCue : null);
            // the camera goes to the creature the player asked to see
            _interaction.FocusRequested += FocusOn;
            // the theme opens the game, then colony music and the meadow bed
            Soundscape.Enter(SoundScene.Colony);
        }

        private void Update()
        {
            if (!_paused) _session?.Advance(Time.deltaTime);
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            Soundscape.SetPaused(paused);
        }

        // a new colony from the scene: the same as launching the game again
        private void Restart()
        {
            SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private IslandCameraRig Rig => _camera != null ? _camera.GetComponent<IslandCameraRig>() : null;

        private void PlayFirstFlight()
        {
            if (GameSettings.FlightShown) return;
            var rig = Rig;
            if (rig == null) return;
            rig.PlayIntro();
            GameSettings.MarkFlightShown();
        }

        private void FocusOn(TrollStrategy.Domain.WorldPosition position)
        {
            var rig = Rig;
            if (rig == null || _worldView == null) return;
            rig.Frame(_worldView.MapToWorld(new Vector3(position.X, position.Y, 0f)), rig.Side);
        }

        private void OnDestroy()
        {
            _telemetry?.Dispose();
            GameSettings.Changed -= ApplyUiScale;
            // the panel settings are an asset: leave them as designed
            if (_screenPanel != null && _designedResolution != default) _screenPanel.referenceResolution = _designedResolution;
        }

        // a larger interface frames a smaller part of the reference resolution
        private void ApplyUiScale()
        {
            if (_screenPanel == null || _designedResolution == default) return;
            float scale = GameSettings.EffectiveUiScale;
            _screenPanel.referenceResolution = new Vector2Int(
                Mathf.RoundToInt(_designedResolution.x / scale), Mathf.RoundToInt(_designedResolution.y / scale));
        }
    }
}
