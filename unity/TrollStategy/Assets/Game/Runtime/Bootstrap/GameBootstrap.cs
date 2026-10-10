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
        private SaveGames _saves;
        // the colony came from a save: no flight in, and the scene's views of buildings it no longer has go
        private bool _restored;
        // a checked save the next colony scene opens instead of a new colony (a load reloads the scene with it)
        private static SavedGame s_pendingLoad;
        private static bool s_launchLoadTried;
        // the run's first colony scene has offered the saved colonies (the launch window); later ones do not
        private static bool s_launchOffered;
        // the pause menu stops the colony's time; the session never advances while it is up
        private bool _paused;
        private PanelSettings _screenPanel;
        private Vector2Int _designedResolution;

        public GameSession Session => _session;
        /// <summary>The colony's saves for the menu: list, save, load, delete; null until the colony is up.</summary>
        public SaveGames Saves => _saves;
        /// <summary>This colony was loaded from a save.</summary>
        public bool Restored => _restored;
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
            // the public builds end early, each after its quest in Progression.asset; the editor plays the whole game
            string lastQuest = !campaign ? null : BuildInfo.Current.Edition switch
            {
                BuildEdition.Alpha => _catalog.Progression.AlphaLastQuestId,
                BuildEdition.SteamDemo => _catalog.Progression.SteamDemoLastQuestId,
                _ => null
            };
            try
            {
                _session = new GameSession(_catalog, startingBuildings, campaign, lastQuest);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"{nameof(GameBootstrap)}: scene building layout is invalid. {exception.Message}", this);
                enabled = false;
                return;
            }
            // the scene's buildings as a new colony names them; a save opens in its place before anything sees it
            var sceneIds = _session.StartingBuildingIds;
            var saveStore = new FileSaveStore(SaveKeeper.Folder());
            // this build's games end after LastLevel quests (0: the whole chain); a shorter edition's save is carried
            // forward to it, a longer one's refused
            var saveStamp = new SaveStamp(BuildInfo.Current.VersionLabel, BuildInfo.Current.Edition.ToString(),
                _session.CurrentSnapshot.Progress.LastLevel);
            var saved = TakeSavedGame(saveStore, saveStamp);
            if (saved != null)
            {
                _session = GameSession.Restore(_catalog, saved);
                _restored = true;
                var summary = saved.Header?.Summary;
                Debug.Log($"[Saves] colony {saved.Header?.ColonyId} opens from its save: quest {summary?.QuestLevel}, " +
                          $"{(summary?.ActiveTimeMs ?? 0) / 1000} s played, {summary?.Creatures} creatures");
                if (saved.CarriedFrom != null)
                    Debug.Log($"[Saves] carried forward from the {saved.CarriedFrom} edition into {saveStamp.Edition}: " +
                              $"chain {summary?.ChainLength} -> {saveStamp.ChainLength} (0 is the whole chain)" +
                              (saved.ChainExtended ? $", its ended game opens on quest {_session.CurrentSnapshot.Progress.Level}" : ""));
            }
            var sceneViews = SceneViews(placements, sceneIds);
            _saves = new SaveGames(_session, saveStore, saveStamp, _restored ? saved.Header : null, LoadSaved);
            _interaction = new InteractionController(_session);
            // where players stop: quests, battles and a heartbeat go to analytics while the player allows it
            _telemetry = new CampaignTelemetry(_session, e => Telemetry.Game.Record(e.Name, e.Fields), resumed: _restored);
            if (_restored) _telemetry.Loaded(saved, saveStamp.Edition);
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

            if (_inputHandler != null)
                _inputHandler.Init(_interaction, _selectionBox);

            if (_land != null)
                _land.Init(_session, _interaction, _worldView, _camera, FindAnyObjectByType<IslandView>());
            else if (_session.CurrentSnapshot.Land != null)
                Debug.LogError($"{nameof(GameBootstrap)} has no {nameof(LandPresenter)}; the island does not show the land.", this);

            // the launch window offers the saves once a run: not after a load, a new start or «Начать заново»
            bool offerSaves = !_restored && !s_launchOffered;
            s_launchOffered = true;
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
                    Saves = _saves,
                    OffersSavesAtLaunch = offerSaves,
                    IntroClosed = PlayFlight,
                    CameraBusy = () => Rig != null && Rig.IsPlayingIntro,
                    CameraPanned = () => Rig != null ? Rig.PlayerPanned : 0f,
                    CameraZoomed = () => Rig != null ? Rig.PlayerZoomed : 0f,
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
                // a new version opens with the alpha notice once the HUD is built; every new colony flies in once it is closed
                _hud.OpenIntro(Telemetry.Game);
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
            // the autosave runs beside the colony: a battle switching the colony off leaves it on and holds its timer;
            // each save carries a picture of the island from the colony camera
            var keeper = new GameObject("SaveKeeper", typeof(SaveKeeper)).GetComponent<SaveKeeper>();
            keeper.transform.SetParent(transform, false);
            keeper.Init(_saves, () => this == null || !isActiveAndEnabled, () => _camera);
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

        // a checked save opens in place of this colony: the scene starts again and takes it instead of a new colony
        private void LoadSaved(SavedGame saved)
        {
            s_pendingLoad = saved;
            SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // The save this scene opens: the one a load handed over across the reload, else, on the run's first scene,
        // the slot -loadSave names (?loadSave= on WebGL), for testers and checks. Null: a new colony.
        private SavedGame TakeSavedGame(ISaveStore store, SaveStamp stamp)
        {
            var saved = s_pendingLoad;
            s_pendingLoad = null;
            if (saved != null) return saved;
            if (s_launchLoadTried) return null;
            s_launchLoadTried = true;
            string slot = SaveKeeper.Argument("loadSave");
            if (string.IsNullOrEmpty(slot)) return null;
            var io = store.Read(slot);
            var read = io.Ok ? SaveCodec.Read(io.Bytes, _catalog, stamp) : LoadResult.Fail(io.Error, io.Detail);
            if (read.Ok) return read.Game.InSlot(slot);
            Debug.LogError($"[Saves] -loadSave {slot}: {read.Error} {read.Detail}; a new colony starts instead.", this);
            return null;
        }

        // The scene's starting buildings become their buildings' views. A loaded colony keeps the view of a starting
        // building it still has (moved or raised, the view follows the save); one it no longer has goes.
        private Dictionary<string, BuildingView> SceneViews(List<SceneBuildingPlacements.Placement> placements,
            IReadOnlyList<string> ids)
        {
            var kinds = new Dictionary<string, BuildingKind>();
            if (_restored)
                foreach (var building in _session.CurrentSnapshot.Buildings) kinds[building.Id] = building.Kind;
            var views = new Dictionary<string, BuildingView>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                var view = placements[i].View;
                if (!_restored || (kinds.TryGetValue(ids[i], out var kind) && kind == placements[i].Building.Kind))
                    views.Add(ids[i], view);
                else
                {
                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                }
            }
            return views;
        }

        // Enter Play Mode keeps statics in the editor: every play starts with no load waiting
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLoads()
        {
            s_pendingLoad = null;
            s_launchLoadTried = false;
            s_launchOffered = false;
        }

        private IslandCameraRig Rig => _camera != null ? _camera.GetComponent<IslandCameraRig>() : null;

        // the flight over the island opens every new colony, not one loaded from a save; any key lands it
        private void PlayFlight()
        {
            if (!_restored) Rig?.PlayIntro();
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
