using System;
using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Visuals;
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

        [Header("UI")]
        [SerializeField] private ColonyHud _hud;

        private GameSession _session;
        private InteractionController _interaction;

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
        public MapInputHandler MapInput => _inputHandler;
        public SelectionBoxRenderer SelectionBox => _selectionBox;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;

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
            if (_hud == null) _hud = FindAnyObjectByType<ColonyHud>();

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

            if (_buildingManager != null)
                _buildingManager.Init(_session, _interaction, _worldView, _catalog, sceneViews);

            if (_unitManager != null)
                _unitManager.Init(_session, _interaction, _catalog, _worldView, _buildingManager);

            if (_placementPreview != null)
                _placementPreview.Init(_session, _interaction, _worldView, _catalog, _camera);

            if (_selectionBox != null)
                _selectionBox.Init(_interaction, _unitManager, _camera, _worldView, _buildingManager);

            if (_routeVisualizer != null)
                _routeVisualizer.Init(_session, _worldView);

            if (_inputHandler != null)
                _inputHandler.Init(_interaction, _selectionBox);

            if (_hud != null)
            {
                var context = new ColonyHudContext(_session, _interaction)
                {
                    ToggleGuides = _routeVisualizer != null ? _routeVisualizer.ToggleGuides : null,
                    GuidesVisible = _routeVisualizer != null ? () => _routeVisualizer.GuidesVisible : null,
                    OpenBattle = mission => BattleSceneController.Open(this, mission),
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
                    OpenQuickBattle = mission => BattleSceneController.OpenQuick(this, mission),
#endif
                    Showcase = new BuildingShowcase()
                };
                _hud.Init(context, _inputHandler);
            }
            else Debug.LogError($"{nameof(GameBootstrap)} has no {nameof(ColonyHud)}; the colony has no HUD.", this);

            // every command answers with sound, a mark and, if refused, why
            var feedback = new GameObject("ColonyFeedback", typeof(ColonyFeedback)).GetComponent<ColonyFeedback>();
            feedback.transform.SetParent(transform, false);
            feedback.Init(_session, _interaction, _worldView, _buildingManager,
                _hud != null ? _hud.PlayRefusalCue : null);
        }

        private void Update()
        {
            _session?.Advance(Time.deltaTime);
        }
    }
}
