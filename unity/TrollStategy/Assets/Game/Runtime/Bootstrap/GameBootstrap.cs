using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
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

        [Header("Prefabs")]
        [SerializeField] private BuildingView _buildingPrefab;
        [SerializeField] private UnitView _unitPrefab;

        [Header("UI")]
        [SerializeField] private HudPresenter _hudPresenter;
        [SerializeField] private ResourceBarView _resourceBar;
        [SerializeField] private ShopDockView _shopDockView;
        [SerializeField] private CommandDockView _commandDockView;
        [SerializeField] private InspectCardView _inspectCardView;
        [SerializeField] private StatusMessageView _statusMessageView;

        private GameSession _session;
        private InteractionController _interaction;

        public GameSession Session => _session;
        public InteractionController Interaction => _interaction;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            if (_catalog == null) _catalog = Resources.Load<GameContentCatalog>("GameContentCatalog");
            if (_buildingPrefab == null) _buildingPrefab = Resources.Load<BuildingView>("BuildingPrefab");
            if (_unitPrefab == null) _unitPrefab = Resources.Load<UnitView>("UnitPrefab");

            if (_buildingManager == null) _buildingManager = FindAnyObjectByType<BuildingVisualsManager>();
            if (_unitManager == null) _unitManager = FindAnyObjectByType<UnitVisualsManager>();
            if (_worldView == null) _worldView = FindAnyObjectByType<TilemapWorldView>();
            if (_placementPreview == null) _placementPreview = FindAnyObjectByType<PlacementPreviewRenderer>();
            if (_selectionBox == null) _selectionBox = FindAnyObjectByType<SelectionBoxRenderer>();
            if (_routeVisualizer == null) _routeVisualizer = FindAnyObjectByType<HaulRouteVisualizer>();
            if (_inputHandler == null) _inputHandler = FindAnyObjectByType<MapInputHandler>();
            if (_hudPresenter == null) _hudPresenter = FindAnyObjectByType<HudPresenter>();
            if (_resourceBar == null) _resourceBar = FindAnyObjectByType<ResourceBarView>();
            if (_shopDockView == null) _shopDockView = FindAnyObjectByType<ShopDockView>();
            if (_commandDockView == null) _commandDockView = FindAnyObjectByType<CommandDockView>();
            if (_inspectCardView == null) _inspectCardView = FindAnyObjectByType<InspectCardView>();
            if (_statusMessageView == null) _statusMessageView = FindAnyObjectByType<StatusMessageView>();

            _session = new GameSession(_catalog);
            _interaction = new InteractionController(_session);

            if (_buildingManager != null)
                _buildingManager.Init(_session, _interaction, _worldView, _catalog, _buildingPrefab);

            if (_unitManager != null)
                _unitManager.Init(_session, _interaction, _catalog, _unitPrefab);

            if (_placementPreview != null)
                _placementPreview.Init(_session, _interaction, _worldView, _catalog, _camera);

            if (_selectionBox != null)
                _selectionBox.Init(_interaction, _unitManager, _camera);

            if (_routeVisualizer != null)
                _routeVisualizer.Init(_session, _worldView);

            if (_inputHandler != null)
                _inputHandler.Init(_interaction);

            if (_hudPresenter != null)
                _hudPresenter.Init(_session, _interaction, _resourceBar, _shopDockView, _commandDockView, _inspectCardView, _statusMessageView);
        }

        private void Update()
        {
            _session?.Advance(Time.deltaTime);
        }

        public void InjectDependencies(
            GameContentCatalog catalog,
            TilemapWorldView worldView,
            Camera cam,
            BuildingVisualsManager buildingManager,
            UnitVisualsManager unitManager,
            PlacementPreviewRenderer placementPreview,
            SelectionBoxRenderer selectionBox,
            HaulRouteVisualizer routeVisualizer,
            MapInputHandler inputHandler,
            BuildingView buildingPrefab,
            UnitView unitPrefab,
            HudPresenter hudPresenter,
            ResourceBarView resourceBar,
            ShopDockView shopDockView,
            CommandDockView commandDockView,
            InspectCardView inspectCardView,
            StatusMessageView statusMessageView)
        {
            _catalog = catalog;
            _worldView = worldView;
            _camera = cam;
            _buildingManager = buildingManager;
            _unitManager = unitManager;
            _placementPreview = placementPreview;
            _selectionBox = selectionBox;
            _routeVisualizer = routeVisualizer;
            _inputHandler = inputHandler;
            _buildingPrefab = buildingPrefab;
            _unitPrefab = unitPrefab;
            _hudPresenter = hudPresenter;
            _resourceBar = resourceBar;
            _shopDockView = shopDockView;
            _commandDockView = commandDockView;
            _inspectCardView = inspectCardView;
            _statusMessageView = statusMessageView;
        }
    }
}