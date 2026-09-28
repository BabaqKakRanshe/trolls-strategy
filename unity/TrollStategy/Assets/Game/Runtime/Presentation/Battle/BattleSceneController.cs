using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>One additive 3D battle scene reused by mission definitions of different sizes.</summary>
    public sealed class BattleSceneController : MonoBehaviour
    {
        private static BattleSceneController s_active;
        private GameBootstrap _colony;
        private BattleMissionDefinition _mission;
        private BattleBoard _board;
        private BattleBoardView _boardView;
        private Camera _battleCamera;
        private float _framedAspect;
        private readonly BattleArenaLighting _arenaLighting = new();
        private Camera _colonyCamera;
        private AudioListener _colonyAudio;
        private MapInputHandler _colonyInput;
        private SelectionBoxRenderer _colonySelection;
        private bool _colonyWasEnabled;
        private bool _cameraWasEnabled;
        private bool _audioWasEnabled;
        private bool _hudWasVisible;
        private bool _inputWasEnabled;
        private bool _selectionWasEnabled;
        private Scene _battleScene;
        private readonly List<BattlePlacement> _placements = new();
        private readonly Dictionary<string, UnitKind> _unitKinds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Image> _rosterBackgrounds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMeshProUGUI> _rosterLabels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UnitSnapshot> _rosterUnits = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EquipmentSnapshot> _equipment = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _equipmentOwners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> _equipmentButtons = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMeshProUGUI> _equipmentLabels = new(StringComparer.Ordinal);
        private string _selectedUnitId;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _phaseGuide;
        private TextMeshProUGUI _squadStatus;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _selectionDetails;
        private TextMeshProUGUI _replayStatus;
        private Button _startButton;
        private Button _pauseButton;
        private Button _removeButton;
        private Button _autoPlaceButton;
        private Button _returnButton;
        private Button _backButton;
        private readonly Button[] _speedButtons = new Button[3];
        private GameObject _preparationPanel;
        private GameObject _replayPanel;
        private GameObject _banner;
        private TextMeshProUGUI _bannerText;
        private float _bannerHideAt = -1f;
        private BattleReport _report;
        private float _elapsedMs;
        private float _speed = 1f;
        private bool _paused;
        private int _eventIndex;
        private float _hitStop;
        private float _tail;
        private bool _resultShown;
        private bool _restored;

        private static readonly Color32 SpeedIdle = new(61, 104, 121, 255);
        private static readonly Color32 SpeedActive = new(214, 164, 68, 255);

        public static void Open(GameBootstrap colony, BattleMissionDefinition mission)
        {
            OpenScene(colony, mission);
        }

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        public static bool OpenQuick(GameBootstrap colony, BattleMissionDefinition mission)
        {
            var controller = OpenScene(colony, mission);
            return controller != null && controller.QuickStart();
        }
#endif

        private static BattleSceneController OpenScene(GameBootstrap colony, BattleMissionDefinition mission)
        {
            if (s_active != null || colony == null || colony.Session == null || mission == null) return null;
            var availability = colony.Session.CanEnterMission(mission.MissionId);
            if (!availability.Ok) return null;

            BattleBoard board;
            try { board = mission.CreateBoard(); }
            catch (Exception exception)
            {
                Debug.LogError($"Battle mission is invalid: {exception.Message}", mission);
                return null;
            }

            var scene = SceneManager.CreateScene("BattleRuntimeScene");
            var root = new GameObject("BattleSceneRoot", typeof(BattleSceneController));
            SceneManager.MoveGameObjectToScene(root, scene);
            var controller = root.GetComponent<BattleSceneController>();
            s_active = controller;
            controller.Initialize(colony, mission, board, scene);
            return controller;
        }

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        private bool QuickStart()
        {
            var snapshot = _colony.Session.CurrentSnapshot;
            if (snapshot.Units.Count == 0) return false;
            int nextUnit = 0;
            foreach (var cell in _mission.PlayerDeployment)
            {
                if (nextUnit >= snapshot.Units.Count || _placements.Count >= _mission.MaxPlayerUnits)
                    break;
                if (!_board.CanPlace(cell) || _placements.Exists(p => p.Cell == cell)) continue;
                _placements.Add(new BattlePlacement(snapshot.Units[nextUnit].Id, cell));
                nextUnit++;
            }
            if (_placements.Count == 0) return false;

            _selectedUnitId = _placements[0].UnitId;
            var usedSlots = new HashSet<EquipmentSlot>();
            foreach (var item in _equipment.Values)
                if (usedSlots.Add(item.Slot)) _equipmentOwners[item.Id] = _selectedUnitId;
            UpdatePreparationUi();
            StartBattle();
            return _report != null;
        }
#endif

        private void Initialize(GameBootstrap colony, BattleMissionDefinition mission,
            BattleBoard board, Scene scene)
        {
            _colony = colony;
            _mission = mission;
            _board = board;
            _battleScene = scene;
            // a building or hiring mode left open would turn battle clicks into colony commands
            if (colony.Interaction != null && colony.Interaction.Mode.Type != InteractionModeType.Neutral)
                colony.Interaction.CancelOrClear();
            _colonyWasEnabled = colony.enabled;
            colony.enabled = false;
            _colonyCamera = colony.ColonyCamera;
            if (_colonyCamera != null)
            {
                _cameraWasEnabled = _colonyCamera.enabled;
                _colonyCamera.enabled = false;
                _colonyAudio = _colonyCamera.GetComponent<AudioListener>();
                if (_colonyAudio != null)
                {
                    _audioWasEnabled = _colonyAudio.enabled;
                    _colonyAudio.enabled = false;
                }
            }
            _hudWasVisible = colony.HudVisible;
            colony.HudVisible = false;
            _colonyInput = colony.MapInput;
            if (_colonyInput != null)
            {
                _inputWasEnabled = _colonyInput.enabled;
                _colonyInput.enabled = false;
            }
            _colonySelection = colony.SelectionBox;
            if (_colonySelection != null)
            {
                _selectionWasEnabled = _colonySelection.enabled;
                _colonySelection.enabled = false;
            }

            var cameraObject = new GameObject("BattleCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 250f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(53, 67, 82, 255);
            MatchRendering(_colonyCamera, camera);

            var lightObject = new GameObject("BattleSun", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;

            var world = new GameObject("BattleWorld", typeof(BattleBoardView));
            world.transform.SetParent(transform, false);
            _boardView = world.GetComponent<BattleBoardView>();
            _boardView.Init(board, mission, camera, new Vector3(1000f, 0f, 1000f), colony.Session.Catalog);
            _boardView.CellClicked += OnCellClicked;
            // a kill freezes the whole replay for a beat so it reads as the turning point it is
            _boardView.KillLanded += () => _hitStop = Mathf.Max(_hitStop, .1f);
            if (_boardView.Arena != null) _arenaLighting.Apply(_boardView.Arena, light);
            _battleCamera = camera;
            FrameCamera();

            var snapshot = colony.Session.CurrentSnapshot;
            foreach (var unit in snapshot.Units)
            {
                _unitKinds[unit.Id] = unit.UnitKind;
                _rosterUnits[unit.Id] = unit;
            }
            foreach (var item in snapshot.Equipment)
            {
                _equipment[item.Id] = item;
                _equipmentOwners[item.Id] = item.OwnerUnitId;
            }
            BuildUi(snapshot);
        }

        private void BuildUi(GameSnapshot snapshot)
        {
            var canvasObject = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var top = Panel("MissionHeader", canvas.transform, new Vector2(0f, 1f),
                Vector2.one, new Vector2(.5f, 1f), Vector2.zero,
                new Vector2(0f, 112f), new Color32(25, 36, 40, 248));
            _title = Label("Title", top.transform, $"{_mission.DisplayName.ToUpperInvariant()} · РАССТАНОВКА", 30,
                TextAlignmentOptions.MidlineLeft);
            SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(1400f, 38f));
            _subtitle = Label("MissionGoal", top.transform,
                $"Цель: победи всех врагов · {EnemySummary()} · Награда за победу — сюрприз: {GameSession.GoldRange(_mission.FirstWinGold, _mission.FirstWinGoldMax)} золота за первую, {GameSession.GoldRange(_mission.RepeatWinGold, _mission.RepeatWinGoldMax)} за повторную",
                18, TextAlignmentOptions.MidlineLeft);
            SetRect(_subtitle.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(28f, 39f), new Vector2(1620f, 25f));
            _phaseGuide = Label("PhaseGuide", top.transform,
                "1  ВЫБЕРИ БОЙЦА     →     2  НАЖМИ СИНЮЮ КЛЕТКУ     →     3  ВЫДАЙ СНАРЯЖЕНИЕ И НАЧНИ БОЙ", 18,
                TextAlignmentOptions.MidlineLeft);
            SetRect(_phaseGuide.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(28f, 9f), new Vector2(1620f, 26f));
            _phaseGuide.color = new Color32(156, 207, 232, 255);
            _backButton = ActionButton("BackButton", top.transform, "В КОЛОНИЮ", new Color32(63, 80, 88, 255), Close);
            _backButton.GetComponent<ButtonFeel>().ClickSound = Sfx.UiBack;
            SetRect((RectTransform)_backButton.transform, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-24f, -24f), new Vector2(185f, 58f));

            _preparationPanel = Panel("PreparationPanel", canvas.transform, Vector2.zero,
                new Vector2(1f, 0f), new Vector2(.5f, 0f), Vector2.zero,
                new Vector2(0f, 285f), new Color32(25, 36, 40, 250));
            var roster = Panel("Roster", _preparationPanel.transform, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(20f, 16f), new Vector2(600f, 253f), new Color32(38, 52, 57, 255));
            var rosterTitle = Label("RosterTitle", roster.transform, "1 · ВЫБЕРИ БОЙЦА", 22, TextAlignmentOptions.MidlineLeft);
            SetRect(rosterTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(560f, 32f));
            var rosterContent = ScrollContent("RosterScroll", roster.transform, new Vector2(12f, 12f), new Vector2(-12f, -51f));
            var grid = rosterContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(270f, 82f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            foreach (var unit in snapshot.Units)
            {
                var unitId = unit.Id;
                var row = ActionButton($"Unit_{unitId}", rosterContent, string.Empty, new Color32(55, 73, 81, 255),
                    () => SelectUnit(unitId));
                row.GetComponent<ButtonFeel>().SilentClick = true;
                var label = row.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = 18;
                label.margin = new Vector4(8f, 0f, 3f, 0f);
                _rosterBackgrounds[unitId] = row.GetComponent<Image>();
                _rosterLabels[unitId] = label;
            }
            if (snapshot.Units.Count == 0)
            {
                var empty = Label("EmptyRoster", rosterContent, "Нет бойцов. Найми отряд в колонии.", 20, TextAlignmentOptions.Center);
                empty.rectTransform.sizeDelta = new Vector2(270f, 82f);
            }

            var selectedPanel = Panel("SelectedUnit", _preparationPanel.transform,
                Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(640f, 16f),
                new Vector2(695f, 253f), new Color32(38, 52, 57, 255));
            var selectedTitle = Label("SelectedTitle", selectedPanel.transform, "2 · РАССТАНОВКА И СНАРЯЖЕНИЕ", 22,
                TextAlignmentOptions.MidlineLeft);
            SetRect(selectedTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(465f, 32f));
            _removeButton = ActionButton("RemoveUnit", selectedPanel.transform, "УБРАТЬ С ПОЛЯ", new Color32(107, 66, 63, 255), RemoveSelectedUnit);
            SetRect((RectTransform)_removeButton.transform, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-14f, -12f), new Vector2(183f, 36f));
            _selectionDetails = Label("SelectionDetails", selectedPanel.transform, string.Empty, 19, TextAlignmentOptions.TopLeft);
            SetRect(_selectionDetails.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, 1f), new Vector2(16f, -56f), new Vector2(-32f, 76f));
            var gearTitle = Label("GearTitle", selectedPanel.transform, "СНАРЯЖЕНИЕ · нажми предмет, чтобы надеть / снять", 17,
                TextAlignmentOptions.MidlineLeft);
            SetRect(gearTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, 1f), new Vector2(16f, -137f), new Vector2(-32f, 24f));
            var gearContent = ScrollContent("EquipmentScroll", selectedPanel.transform, new Vector2(12f, 12f), new Vector2(-12f, -165f));
            var gearLayout = gearContent.gameObject.AddComponent<GridLayoutGroup>();
            gearLayout.cellSize = new Vector2(321f, 68f);
            gearLayout.spacing = new Vector2(8f, 8f);
            gearLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gearLayout.constraintCount = 2;
            foreach (var item in snapshot.Equipment)
            {
                var itemId = item.Id;
                var gearButton = ActionButton($"Equip_{itemId}", gearContent, item.DisplayName, new Color32(55, 73, 81, 255), () => ToggleEquipment(itemId));
                gearButton.GetComponent<ButtonFeel>().SilentClick = true;
                _equipmentButtons[itemId] = gearButton;
                _equipmentLabels[itemId] = gearButton.GetComponentInChildren<TextMeshProUGUI>();
                _equipmentLabels[itemId].fontSize = 17;
            }
            if (snapshot.Equipment.Count == 0)
            {
                var empty = Label("EmptyEquipment", gearContent, "Снаряжения нет · можно начать бой без него", 18, TextAlignmentOptions.Center);
            }

            var actionPanel = Panel("ActionPanel", _preparationPanel.transform,
                Vector2.one, Vector2.one, Vector2.one, new Vector2(-20f, -16f),
                new Vector2(545f, 253f), new Color32(38, 52, 57, 255));
            _squadStatus = Label("SquadStatus", actionPanel.transform, string.Empty, 24, TextAlignmentOptions.MidlineLeft);
            SetRect(_squadStatus.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(-32f, 36f));
            _hint = Label("Hint", actionPanel.transform, "Выбери бойца слева или нажми «Авторасстановка». Бой идёт автоматически; погибшие бойцы и их вещи будут потеряны.",
                18, TextAlignmentOptions.TopLeft);
            SetRect(_hint.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, 1f), new Vector2(16f, -53f), new Vector2(-32f, 68f));
            _autoPlaceButton = ActionButton("AutoPlace", actionPanel.transform, "АВТОРАССТАНОВКА", new Color32(63, 80, 88, 255), AutoPlace);
            SetRect((RectTransform)_autoPlaceButton.transform, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(.5f, 0f), new Vector2(0f, 92f), new Vector2(-32f, 38f));
            _startButton = ActionButton("StartBattle", actionPanel.transform, "НАЧАТЬ БОЙ", new Color32(43, 125, 174, 255), StartBattle);
            SetRect((RectTransform)_startButton.transform, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(.5f, 0f), new Vector2(0f, 16f), new Vector2(-32f, 64f));

            _replayPanel = Panel("ReplayPanel", canvas.transform, Vector2.zero,
                new Vector2(1f, 0f), new Vector2(.5f, 0f), Vector2.zero,
                new Vector2(0f, 118f), new Color32(25, 36, 40, 248));
            _replayStatus = Label("ReplayStatus", _replayPanel.transform, string.Empty, 23, TextAlignmentOptions.MidlineLeft);
            SetRect(_replayStatus.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(28f, 18f), new Vector2(1200f, 82f));
            _pauseButton = ActionButton("Pause", _replayPanel.transform, "ПАУЗА", new Color32(63, 80, 88, 255), TogglePause);
            SetRect((RectTransform)_pauseButton.transform, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-332f, -28f), new Vector2(166f, 60f));
            _pauseButton.GetComponent<ButtonFeel>().SilentClick = true;
            for (int i = 0; i < 3; i++)
            {
                float speed = i == 0 ? 1f : i == 1 ? 2f : 4f;
                var speedButton = ActionButton($"Speed{speed}", _replayPanel.transform, $"{speed:0}×", SpeedIdle, () => SetSpeed(speed));
                SetRect((RectTransform)speedButton.transform, Vector2.one, Vector2.one, Vector2.one,
                    new Vector2(-236f + i * 94f, -28f), new Vector2(82f, 60f));
                _speedButtons[i] = speedButton;
            }
            _returnButton = ActionButton("ReturnToColony", _replayPanel.transform, "ВЕРНУТЬСЯ В КОЛОНИЮ", new Color32(43, 125, 174, 255), Close);
            SetRect((RectTransform)_returnButton.transform, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-28f, -28f), new Vector2(420f, 60f));
            _returnButton.gameObject.SetActive(false);
            _replayPanel.SetActive(false);

            _banner = Panel("Banner", canvas.transform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(.5f, 1f), new Vector2(0f, -145f), new Vector2(640f, 100f), new Color32(22, 30, 30, 225), blocking: false);
            _bannerText = Label("BannerText", _banner.transform, string.Empty, 58, TextAlignmentOptions.Center);
            StretchInside(_bannerText.rectTransform, 6f);
            _banner.SetActive(false);
            RefreshReplayButtons();
            UpdatePreparationUi();
        }

        private string EnemySummary() => "Враги: " + string.Join(", ", _mission.Enemies
            .GroupBy(enemy => enemy.Kind)
            .Select(group => $"{_colony.Session.Catalog.GetUnit(group.Key).DisplayName} ×{group.Count()}"));

        // Both lists have a visible scroll handle; gear remains reachable as the inventory grows.
        private static RectTransform ScrollContent(string name, Transform parent, Vector2 insetMin, Vector2 insetMax)
        {
            var scrollObject = RectObject(name, parent);
            var rect = (RectTransform)scrollObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = insetMin;
            rect.offsetMax = insetMax;
            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = RectObject("Viewport", scrollObject.transform);
            var viewportRect = (RectTransform)viewport.transform;
            StretchInside(viewportRect, 0f);
            viewportRect.offsetMax = new Vector2(-20f, 0f);
            viewport.AddComponent<Image>().color = new Color32(30, 43, 49, 255);
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewportRect;
            var content = RectObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            SetRect(contentRect, new Vector2(0f, 1f), Vector2.one, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;
            var track = Panel("Scrollbar", scrollObject.transform, new Vector2(1f, 0f), Vector2.one,
                new Vector2(1f, .5f), Vector2.zero, new Vector2(14f, 0f), new Color32(25, 36, 40, 255));
            var handle = Panel("Handle", track.transform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero, new Color32(137, 167, 180, 255));
            var scrollbar = track.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = (RectTransform)handle.transform;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = scrollbar;
            return contentRect;
        }

        private void TogglePause()
        {
            if (_report == null || _resultShown) return;
            _paused = !_paused;
            GameAudio.Play(_paused ? Sfx.Pause : Sfx.Unpause);
            RefreshReplayButtons();
        }

        private void SetSpeed(float speed)
        {
            if (_resultShown) return;
            _speed = speed;
            RefreshReplayButtons();
        }

        /// <summary>The active speed is lit and pause reads as the action it will take.</summary>
        private void RefreshReplayButtons()
        {
            if (_pauseButton != null)
            {
                _pauseButton.GetComponentInChildren<TextMeshProUGUI>().text = _paused ? "ПРОДОЛЖИТЬ" : "ПАУЗА";
                _pauseButton.GetComponent<Image>().color = _paused ? SpeedActive : new Color32(75, 89, 86, 255);
            }
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null) continue;
                float speed = i == 0 ? 1f : i == 1 ? 2f : 4f;
                _speedButtons[i].GetComponent<Image>().color = Mathf.Approximately(speed, _speed) ? SpeedActive : SpeedIdle;
            }
            if (_report != null && !_resultShown) RefreshReplayStatus();
        }

        private void RefreshReplayStatus()
        {
            _replayStatus.text = $"{(_paused ? "ПАУЗА" : "БОЙ")} · {_elapsedMs / 1000f:0.0} с · {_speed:0}×\n" +
                $"Твой отряд: {_boardView.AlivePlayers} в строю     ·     Враги: {_boardView.AliveEnemies} в строю";
        }

        /// <summary>Big centred word that pops in: the battle starts, or ends.</summary>
        private void ShowBanner(string text, Color color, float seconds)
        {
            _banner.SetActive(true);
            _bannerText.text = text;
            _bannerText.color = color;
            _bannerHideAt = seconds > 0f ? Time.unscaledTime + seconds : -1f;
            Juice.PopIn(_banner.transform, .38f);
        }

        private void SelectUnit(string unitId)
        {
            if (_report != null || !_rosterUnits.ContainsKey(unitId)) return;
            _selectedUnitId = unitId;
            _hint.text = _placements.Exists(p => p.UnitId == unitId)
                ? "Боец выбран. Нажми свободную синюю клетку, чтобы переставить, или выдай снаряжение."
                : "Теперь нажми свободную синюю клетку, чтобы поставить бойца.";
            GameAudio.Play(Sfx.Select);
            UpdatePreparationUi();
        }

        private void RemoveSelectedUnit()
        {
            if (_report != null || _selectedUnitId == null) return;
            if (_placements.RemoveAll(p => p.UnitId == _selectedUnitId) == 0) return;
            foreach (var itemId in new List<string>(_equipmentOwners.Keys))
                if (_equipmentOwners[itemId] == _selectedUnitId) _equipmentOwners[itemId] = null;
            _hint.text = "Боец убран с поля, его снаряжение возвращено в инвентарь.";
            GameAudio.Play(Sfx.UiBack);
            UpdatePreparationUi();
        }

        private void AutoPlace()
        {
            if (_report != null) return;
            foreach (var unit in _rosterUnits.Values)
            {
                if (_placements.Count >= _mission.MaxPlayerUnits) break;
                if (_placements.Exists(p => p.UnitId == unit.Id)) continue;
                foreach (var cell in _mission.PlayerDeployment)
                {
                    if (!_board.CanPlace(cell) || _placements.Exists(p => p.Cell == cell)) continue;
                    _placements.Add(new BattlePlacement(unit.Id, cell));
                    break;
                }
            }
            if (!_placements.Exists(p => p.UnitId == _selectedUnitId))
                _selectedUnitId = _placements.Count > 0 ? _placements[0].UnitId : null;
            _hint.text = "Отряд расставлен. Выбери бойца для снаряжения или начни бой. Погибшие бойцы и их вещи будут потеряны.";
            UpdatePreparationUi();
        }

        private void OnCellClicked(Cell cell)
        {
            if (_report != null) return;
            // clicking one of your placed fighters picks it up, whatever was selected
            int onCell = _placements.FindIndex(p => p.Cell == cell);
            if (onCell >= 0 && _placements[onCell].UnitId != _selectedUnitId)
            {
                _selectedUnitId = _placements[onCell].UnitId;
                _hint.text = "Боец выбран: нажми на другую синюю клетку, чтобы переставить";
                _boardView.PulseCell(cell, true);
                GameAudio.Play(Sfx.Select);
                UpdatePreparationUi();
                return;
            }
            if (string.IsNullOrEmpty(_selectedUnitId))
            {
                Refuse(cell, "Сначала выбери бойца в списке слева");
                return;
            }
            if (!_board.CanPlace(cell))
            {
                Refuse(cell, _board.IsBlocked(cell) ? "Клетка занята преградой" : "Здесь нельзя расставлять бойцов");
                return;
            }
            int current = _placements.FindIndex(p => p.UnitId == _selectedUnitId);
            if (current < 0 && _placements.Count >= _mission.MaxPlayerUnits)
            {
                Refuse(cell, "Отряд заполнен");
                return;
            }
            var placement = new BattlePlacement(_selectedUnitId, cell);
            if (current >= 0) _placements[current] = placement;
            else _placements.Add(placement);
            _hint.text = $"Боец на поле: {CellName(cell)}. Выдай снаряжение ниже или выбери следующего бойца.";
            _boardView.PulseCell(cell, true);
            UpdatePreparationUi();
        }

        /// <summary>Says no right where the player looked: red ring on the cell, a shake of the hint and why.</summary>
        private void Refuse(Cell cell, string reason)
        {
            _hint.text = reason;
            _boardView.PulseCell(cell, false);
            Juice.Nudge(_hint.transform, 9f);
            Juice.Flash(_hint, new Color(1f, .55f, .45f, 1f), .5f);
            GameAudio.Play(Sfx.UiDenied);
        }

        private void UpdatePreparationUi()
        {
            _boardView.ShowPlayerPlacements(_placements, _unitKinds);
            _boardView.ShowSelection(_selectedUnitId);
            _boardView.PlacementHover = _report == null && !string.IsNullOrEmpty(_selectedUnitId);
            bool selectedPlaced = _selectedUnitId != null && _placements.Exists(p => p.UnitId == _selectedUnitId);
            foreach (var pair in _equipmentButtons)
            {
                string owner = _equipmentOwners[pair.Key];
                pair.Value.interactable = selectedPlaced;
                pair.Value.GetComponent<Image>().color = owner == _selectedUnitId && owner != null
                    ? new Color32(54, 113, 83, 255) : new Color32(55, 73, 81, 255);
                var item = _equipment[pair.Key];
                var bonus = new List<string>();
                if (item.DamageBonus != 0) bonus.Add($"{item.DamageBonus:+0;-0;0} урон");
                if (item.ArmorBonus != 0) bonus.Add($"{item.ArmorBonus:+0;-0;0} броня");
                _equipmentLabels[pair.Key].text = $"{item.DisplayName} · {string.Join(", ", bonus)}\n" +
                    (owner == null ? "В инвентаре" : owner == _selectedUnitId ? "Надето · нажми, чтобы снять" : $"У {UnitName(owner)} · нажми, чтобы передать");
            }
            foreach (var pair in _rosterBackgrounds)
            {
                var placement = _placements.FindIndex(p => p.UnitId == pair.Key);
                bool selected = pair.Key == _selectedUnitId;
                pair.Value.color = selected ? new Color32(44, 109, 147, 255) :
                    placement >= 0 ? new Color32(54, 100, 78, 255) : new Color32(55, 73, 81, 255);
                var definition = _colony.Session.Catalog.GetUnit(_unitKinds[pair.Key]);
                _rosterLabels[pair.Key].text = $"{(selected ? "> " : "")}{UnitName(pair.Key)}\n" +
                    $"{definition.CombatHealth} HP · {definition.CombatDamage} урон\n" +
                    (placement >= 0 ? $"На поле · {CellName(_placements[placement].Cell)}" : "В резерве · нажми для выбора");
            }
            _startButton.interactable = _placements.Count > 0;
            _startButton.GetComponentInChildren<TextMeshProUGUI>().text = _placements.Count == 0
                ? "СНАЧАЛА РАССТАВЬ БОЙЦОВ" : $"НАЧАТЬ БОЙ · {_placements.Count} В ОТРЯДЕ";
            _squadStatus.text = $"3 · ОТРЯД {_placements.Count} / {_mission.MaxPlayerUnits}";
            _removeButton.interactable = selectedPlaced;
            _autoPlaceButton.interactable = _placements.Count < Mathf.Min(_mission.MaxPlayerUnits, _rosterUnits.Count);
            if (_selectedUnitId == null || !_rosterUnits.TryGetValue(_selectedUnitId, out var unit))
                _selectionDetails.text = "Выбери бойца слева, затем синюю клетку.\nСиние клетки — твои; красные — враг; серые — преграды.";
            else
            {
                var definition = _colony.Session.Catalog.GetUnit(unit.UnitKind);
                int damage = definition.CombatDamage, armor = definition.CombatArmor;
                foreach (var item in _equipment.Values)
                    if (_equipmentOwners[item.Id] == unit.Id) { damage += item.DamageBonus; armor += item.ArmorBonus; }
                var placement = _placements.FindIndex(p => p.UnitId == unit.Id);
                _selectionDetails.text = $"{UnitName(unit.Id)} · {(placement >= 0 ? "На поле: " + CellName(_placements[placement].Cell) : "Выбери синюю клетку")}\n" +
                    $"{definition.CombatHealth} HP · {damage} урон · {armor} броня · дальность {definition.AttackRange}\n" +
                    (selectedPlaced ? "Выдай снаряжение ниже · при старте уйдёт с работы" : "Сначала поставь на поле, чтобы выдать снаряжение");
            }
        }

        private string UnitName(string unitId) => _rosterUnits.TryGetValue(unitId, out var unit)
            ? $"{unit.Name} {unit.Number}" : "другого бойца";

        private static string CellName(Cell cell) => $"{cell.X + 1}:{cell.Y + 1}";

        private void ToggleEquipment(string itemId)
        {
            if (_selectedUnitId == null || !_placements.Exists(p => p.UnitId == _selectedUnitId)) return;
            if (_equipmentOwners[itemId] == _selectedUnitId)
            {
                _equipmentOwners[itemId] = null;
                GameAudio.Play(Sfx.Unequip);
            }
            else
            {
                var slot = _equipment[itemId].Slot;
                foreach (var otherId in new List<string>(_equipmentOwners.Keys))
                    if (_equipmentOwners[otherId] == _selectedUnitId && _equipment[otherId].Slot == slot)
                        _equipmentOwners[otherId] = null;
                _equipmentOwners[itemId] = _selectedUnitId;
                GameAudio.Play(Sfx.Equip);
            }
            UpdatePreparationUi();
        }

        private void StartBattle()
        {
            if (_report != null) return;
            var assignments = new List<BattleEquipmentAssignment>();
            foreach (var item in _equipment.Values)
                if (_equipmentOwners[item.Id] != item.OwnerUnitId)
                    assignments.Add(new BattleEquipmentAssignment(item.Id, _equipmentOwners[item.Id]));
            var result = _colony.Session.Dispatch(new StartBattleCommand(_mission.MissionId,
                _placements.ToArray(), assignments));
            if (!result.Ok)
            {
                _hint.text = result.Error;
                Juice.Nudge(_hint.transform, 9f);
                GameAudio.Play(Sfx.UiDenied);
                return;
            }
            _report = _colony.Session.ActiveBattle?.Report;
            if (_report == null) return;
            _preparationPanel.SetActive(false);
            _replayPanel.SetActive(true);
            _boardView.BeginReplay(_report);
            _elapsedMs = 0f;
            _eventIndex = 0;
            _tail = 0f;
            _title.text = $"{_mission.DisplayName.ToUpperInvariant()}  ·  БОЙ";
            _subtitle.text = "Цель: победи всех врагов · синий круг — твой боец · красный — противник · над бойцами — здоровье";
            _phaseGuide.text = "БОЙ ИДЁТ АВТОМАТИЧЕСКИ · ПРОБЕЛ — ПАУЗА / ПРОДОЛЖИТЬ · СКОРОСТЬ ВНИЗУ СПРАВА";
            RefreshReplayStatus();
            ShowBanner("В БОЙ!", new Color32(241, 232, 213, 255), 1.1f);
            GameAudio.Play(Sfx.BattleStart);
            CameraShake.Kick(_battleCamera, .25f);
        }

        private void Update()
        {
            if (_banner != null && _bannerHideAt > 0f && Time.unscaledTime >= _bannerHideAt)
            {
                _bannerHideAt = -1f;
                _banner.SetActive(false);
            }

            float dt = Time.deltaTime;
            if (_report != null && !_resultShown && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                TogglePause();
            if (_report == null || _resultShown)
            {
                // preparation and aftermath: idle poses, cell hover, settling effects
                _boardView.Advance(dt);
                return;
            }
            if (_paused) return;
            if (_hitStop > 0f)
            {
                _hitStop -= Time.unscaledDeltaTime;
                return;
            }

            float replayDt = dt * _speed;
            _elapsedMs = Mathf.Min(_report.DurationMs, _elapsedMs + replayDt * 1000f);
            while (_eventIndex < _report.Events.Count && _report.Events[_eventIndex].TimeMs <= _elapsedMs)
            {
                _boardView.ShowEvent(_report.Events[_eventIndex]);
                _eventIndex++;
            }
            _boardView.Advance(replayDt);
            RefreshReplayStatus();
            if (_elapsedMs < _report.DurationMs || _boardView.HasPendingBeats) return;
            // let the last blow and fall play out before the verdict
            _tail += replayDt;
            if (_tail >= .7f) ShowResult();
        }

        private void ShowResult()
        {
            _resultShown = true;
            var run = _colony.Session.ActiveBattle;
            bool victory = _report.Outcome == BattleOutcome.PlayerVictory;
            bool defeat = _report.Outcome == BattleOutcome.EnemyVictory;
            string outcome = victory ? "ПОБЕДА" : defeat ? "ПОРАЖЕНИЕ" : "НИЧЬЯ";
            int lostItems = 0;
            if (run != null)
                foreach (var owner in _equipmentOwners.Values)
                    if (owner != null && run.FallenUnitIds.Contains(owner)) lostItems++;
            // the amount is a surprise revealed back in the colony
            string reward = victory ? "награда ждёт в поселении" : "без награды";
            _replayStatus.text = $"{outcome} · {reward}\n" +
                $"Выжило: {_placements.Count - (run?.FallenUnitIds.Count ?? 0)} · погибло: {run?.FallenUnitIds.Count ?? 0} · потеряно вещей: {lostItems}";
            _title.text = $"{_mission.DisplayName.ToUpperInvariant()}  ·  {outcome}";
            _subtitle.text = victory
                ? "Бой завершён · потери применены · награда ждёт в поселении · выжившие вернутся в колонию"
                : "Бой завершён · потери применены · выжившие бойцы вернутся в колонию";
            _phaseGuide.text = "Нажми «Вернуться в колонию», чтобы продолжить строительство и добычу.";
            _backButton.interactable = true;
            _returnButton.gameObject.SetActive(true);
            ShowBanner(outcome, victory ? new Color32(255, 214, 102, 255) : defeat ? new Color32(255, 120, 100, 255)
                : new Color32(210, 210, 205, 255), 2f);
            GameAudio.Play(victory ? Sfx.Victory : defeat ? Sfx.Defeat : Sfx.UiBack);
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(false);
            foreach (var button in _speedButtons)
                if (button != null) button.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // window resized or device rotated: keep the board framed
            if (_battleCamera != null && !Mathf.Approximately(_battleCamera.aspect, _framedAspect))
                FrameCamera();
        }

        /// <summary>
        /// Arena missions: the perspective shot the environment was dressed for (pitch, FOV and distance
        /// at 16:9); on narrower screens the camera backs off until the board fits its share of the width.
        /// Blockout missions keep the orthographic overview.
        /// </summary>
        private void FrameCamera()
        {
            var camera = _battleCamera;
            if (camera == null || _boardView == null) return;
            if (!(camera.aspect > 0f) || float.IsInfinity(camera.aspect)) return;     // zero-size view
            _framedAspect = camera.aspect;
            var arena = _boardView.Arena;
            if (arena == null)
            {
                var bounds = _boardView.WorldBounds;
                float span = Mathf.Max(bounds.size.x, bounds.size.z);
                camera.orthographic = true;
                camera.transform.position = bounds.center + new Vector3(0f, span * .9f, -span * 1.1f);
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize = Mathf.Max(bounds.size.z * .95f,
                    bounds.size.x / Mathf.Max(1f, camera.aspect * 1.4f)) + 2f;
                return;
            }

            camera.orthographic = false;
            camera.fieldOfView = arena.FieldOfView;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300f;
            camera.backgroundColor = arena.Background;
            float halfWidth = Mathf.Tan(arena.FieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Max(.1f, camera.aspect);
            float fit = _boardView.BoardSize.x / (2f * arena.BoardWidthShare * halfWidth);
            float distance = Mathf.Max(arena.Distance, fit);
            float pitch = arena.Pitch * Mathf.Deg2Rad;
            var focus = _boardView.BoardCenter + new Vector3(0f, 0f, arena.FocusOffset);
            camera.transform.position = focus + new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * distance;
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);
            // backdrop houses stand far behind the board; a backed-off camera would leave them unshadowed
            _arenaLighting.FitShadows(distance, arena.ShadowReach);
        }

        /// <summary>
        /// The battle camera renders like the colony camera: same renderer, post-processing and
        /// anti-aliasing. Without anti-aliasing the thin tile rims shimmer.
        /// </summary>
        private static void MatchRendering(Camera source, Camera target)
        {
            if (source == null || !source.TryGetComponent<UniversalAdditionalCameraData>(out var from)) return;
            var to = target.GetUniversalAdditionalCameraData();
            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.antialiasingQuality = from.antialiasingQuality;
            to.dithering = from.dithering;
            to.stopNaN = from.stopNaN;
            to.volumeLayerMask = from.volumeLayerMask;
            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;
            var pipeline = UniversalRenderPipeline.asset;
            var renderer = from.scriptableRenderer;
            if (pipeline == null || renderer == null) return;
            var renderers = pipeline.rendererDataList;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || pipeline.GetRenderer(i) != renderer) continue;
                to.SetRenderer(i);
                return;
            }
        }

        private void Close()
        {
            if (_colony != null && _colony.Session != null && _colony.Session.ActiveBattle != null)
                _colony.Session.Dispatch(new AcknowledgeBattleCommand());
            // the scene unloads next frame: switch its camera, listener and UI off before the colony's return
            gameObject.SetActive(false);
            RestoreColony();
            if (_battleScene.IsValid() && _battleScene.isLoaded)
                SceneManager.UnloadSceneAsync(_battleScene);
        }

        private void RestoreColony()
        {
            if (_restored) return;
            _restored = true;
            _arenaLighting.Restore();
            if (_colony != null) _colony.enabled = _colonyWasEnabled;
            if (_colonyCamera != null) _colonyCamera.enabled = _cameraWasEnabled;
            if (_colonyAudio != null) _colonyAudio.enabled = _audioWasEnabled;
            if (_colony != null) _colony.HudVisible = _hudWasVisible;
            if (_colonyInput != null) _colonyInput.enabled = _inputWasEnabled;
            if (_colonySelection != null) _colonySelection.enabled = _selectionWasEnabled;
            if (s_active == this) s_active = null;
        }

        // a script reload in Play Mode drops the snapshot; the pipeline asset must not keep the battle's shadows
        private void OnDisable() => _arenaLighting.Restore();

        private void OnDestroy() => RestoreColony();

        private static GameObject RectObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>A solid panel; unless <paramref name="blocking"/> is off, clicks on it never reach the board.</summary>
        private static GameObject Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size, Color color, bool blocking = true)
        {
            var go = RectObject(name, parent);
            SetRect((RectTransform)go.transform, anchorMin, anchorMax, pivot, position, size);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocking;
            return go;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string value,
            float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color32(241, 232, 213, 255);
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static Button ActionButton(string name, Transform parent, string caption, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = RectObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var label = Label("Label", go.transform, caption, 19, TextAlignmentOptions.Center);
            StretchInside(label.rectTransform, 6f);
            ButtonFeel.Attach(button);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void StretchInside(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
