using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>
    /// One 3D battle scene reused by mission definitions of different sizes. It owns the arena, camera
    /// and replay clock; the player's deployment is a <see cref="BattleDeployment"/> and the screen UI is
    /// the colony's <see cref="IBattleScreen"/>, opened for the battle and closed with it.
    /// </summary>
    public sealed class BattleSceneController : MonoBehaviour
    {
        private static BattleSceneController s_active;
        private GameBootstrap _colony;
        private BattleBoardView _boardView;
        private BattleDeployment _deployment;
        private IBattleScreen _screen;
        private Camera _battleCamera;
        private float _framedAspect;
        private readonly BattleArenaLighting _arenaLighting = new();
        private VolumeProfile _arenaProfile;
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
        private BattleReport _report;
        private float _elapsedMs;
        private float _speed = 1f;
        private bool _paused;
        private int _eventIndex;
        private float _hitStop;
        private float _tail;
        private bool _resultShown;
        private bool _restored;

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
            if (colony.BattleScreen == null)
            {
                Debug.LogError("The colony has no battle screen; run TrollStrategy/Dev/Setup UI.", colony);
                return null;
            }
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
            if (!_deployment.QuickFill()) return false;
            StartBattle();
            return _report != null;
        }
#endif

        private void Initialize(GameBootstrap colony, BattleMissionDefinition mission,
            BattleBoard board, Scene scene)
        {
            _colony = colony;
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
            Soundscape.Enter(SoundScene.Battle);
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
            _boardView.FighterDropped += OnFighterDropped;
            // a kill freezes the whole replay for a beat so it reads as the turning point it is
            _boardView.KillLanded += () => _hitStop = Mathf.Max(_hitStop, .1f);
            if (_boardView.Arena != null) _arenaLighting.Apply(_boardView.Arena, light);
            _battleCamera = camera;
            FrameCamera();

            _deployment = new BattleDeployment(colony.Session, mission, board);
            _deployment.Changed += ShowDeployment;
            _screen = colony.BattleScreen;
            _screen.StartRequested += StartBattle;
            _screen.PauseToggled += TogglePause;
            _screen.SpeedChosen += SetSpeed;
            _screen.CloseRequested += Close;
            _screen.KindDropped += OnKindDropped;
            _screen.Open(_deployment);
            _screen.LocateCells(CellOnScreen);
            ShowDeployment();
        }

        // who wears what in the deployment, as the item pictures of the catalog
        private Dictionary<string, List<WornItem>> WornGear()
        {
            var worn = new Dictionary<string, List<WornItem>>(StringComparer.Ordinal);
            var catalog = _deployment.Session.Catalog;
            foreach (var item in _deployment.Equipment)
            {
                string owner = _deployment.OwnerOf(item.Id);
                if (owner == null) continue;
                EquipmentDefinition definition = null;
                foreach (var candidate in catalog.Equipment)
                    if (candidate != null && candidate.ItemId == item.DefinitionId) definition = candidate;
                if (definition == null) continue;
                if (!worn.TryGetValue(owner, out var gear)) worn[owner] = gear = new List<WornItem>();
                gear.Add(new WornItem(definition.Slot, definition.Icon, definition.Enchanted));
            }
            return worn;
        }

        // the slots some item of the game fits, in slot order: a free slot nothing could fill is not offered
        private List<EquipmentSlot> GearSlots()
        {
            var slots = new List<EquipmentSlot>();
            foreach (var definition in _deployment.Session.Catalog.Equipment)
                if (definition != null && !slots.Contains(definition.Slot)) slots.Add(definition.Slot);
            slots.Sort();
            return slots;
        }

        /// <summary>The board shows the deployment: placed fighters, the selection, the hover ring and dragging.</summary>
        private void ShowDeployment()
        {
            _boardView.ShowPlayerPlacements(_deployment.Placements, _deployment.UnitKinds);
            _boardView.ShowGear(WornGear(), GearSlots(), _report == null);
            _boardView.ShowSelection(_deployment.SelectedUnitId);
            _boardView.Deploying = _report == null;
        }

        private void TogglePause()
        {
            if (_report == null || _resultShown) return;
            _paused = !_paused;
            GameAudio.Play(_paused ? Sfx.Pause : Sfx.Unpause);
            Soundscape.SetPaused(_paused);
            ShowReplay();
        }

        private void SetSpeed(float speed)
        {
            if (_report == null || _resultShown) return;
            _speed = speed;
            ShowReplay();
        }

        private void ShowReplay() => _screen.ShowReplay(_paused, _speed, _boardView.AlivePlayers, _boardView.AliveEnemies);

        // the tutorial pointer finds a fighter by the cell it stands on
        private Vector2? CellOnScreen(Cell cell)
        {
            if (_battleCamera == null || _boardView == null) return null;
            var point = _battleCamera.WorldToScreenPoint(_boardView.CellWorldPosition(cell));
            // pixels from the top left, as the HUD counts them
            return point.z > 0f ? new Vector2(point.x, _battleCamera.pixelHeight - point.y) : (Vector2?)null;
        }

        private void OnCellClicked(Cell cell)
        {
            if (_report != null) return;
            Answer(_deployment.ClickCell(cell), cell);
        }

        private void OnFighterDropped(string unitId, Cell? cell)
        {
            if (_report != null) return;
            Answer(_deployment.Drop(unitId, cell), cell);
        }

        // let go over the HUD or off the board: the kind stays in the reserve
        private void OnKindDropped(UnitKind kind)
        {
            if (_report != null) return;
            var pointer = MapPointer.Position;
            if (UIInputUtils.IsOverDocument(pointer) || !_boardView.TryCellAt(pointer, out var cell)) return;
            Answer(_deployment.PlaceKind(kind, cell), cell);
        }

        /// <summary>Answers the player's hand on the board: a ring where it landed, a sound, or why not.</summary>
        private void Answer(DeploymentResult result, Cell? cell)
        {
            switch (result)
            {
                case DeploymentResult.Placed:
                case DeploymentResult.Moved:
                    // the landing sound comes with the fighter
                    if (cell.HasValue) _boardView.PulseCell(cell.Value, true);
                    break;
                case DeploymentResult.Selected:
                    if (cell.HasValue) _boardView.PulseCell(cell.Value, true);
                    GameAudio.Play(Sfx.Select);
                    break;
                case DeploymentResult.Deselected:
                case DeploymentResult.Removed:
                    GameAudio.Play(Sfx.UiBack);
                    break;
                default:
                    // says no right where the player looked: red ring on the cell, a shake of the hint and why
                    if (cell.HasValue) _boardView.PulseCell(cell.Value, false);
                    _screen.Refuse();
                    GameAudio.Play(Sfx.UiDenied);
                    break;
            }
        }

        private void StartBattle()
        {
            if (_report != null) return;
            var result = _deployment.Start();
            if (!result.Ok)
            {
                _screen.Refuse();
                GameAudio.Play(Sfx.UiDenied);
                return;
            }
            _report = _colony.Session.ActiveBattle?.Report;
            if (_report == null) return;
            _boardView.BeginReplay(_report);
            _elapsedMs = 0f;
            _eventIndex = 0;
            _tail = 0f;
            _screen.BeginReplay();
            ShowReplay();
            GameAudio.Play(Sfx.BattleStart);
            CameraShake.Kick(_battleCamera, .25f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var keyboard = Keyboard.current;
            if (_report != null && !_resultShown && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                TogglePause();
            // Esc is the way back: while deploying it first lets the selected fighter go
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_report == null && _deployment.Deselect()) GameAudio.Play(Sfx.UiBack);
                else
                {
                    GameAudio.Play(Sfx.UiBack);
                    Close();
                    return;
                }
            }
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
            ShowReplay();
            if (_elapsedMs < _report.DurationMs || _boardView.HasPendingBeats) return;
            // let the last blow and fall play out before the verdict
            _tail += replayDt;
            if (_tail >= .7f) ShowResult();
        }

        private void ShowResult()
        {
            _resultShown = true;
            // the verdict stinger plays over silence
            Soundscape.Enter(SoundScene.BattleResult);
            var run = _colony.Session.ActiveBattle;
            int fallen = run?.FallenUnitIds.Count ?? 0;
            int lostItems = run != null ? _deployment.LostItems(run.FallenUnitIds) : 0;
            var cost = BattleCost.Of(run, _colony.Session.Catalog, _colony.Session.CurrentSnapshot.BattleReward != null);
            _screen.ShowResult(_report.Outcome, _deployment.Placements.Count - fallen, fallen, lostItems, cost);
            GameAudio.Play(_report.Outcome == BattleOutcome.PlayerVictory ? Sfx.Victory
                : _report.Outcome == BattleOutcome.EnemyVictory ? Sfx.Defeat : Sfx.UiBack);
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
            // the island look: fog and depth of field back off with the camera, as in the colony
            if (arena.HasLook)
                IslandAtmosphere.Apply(distance, ArenaProfile(arena), arena.FogStartPerDistance,
                    arena.FogEndPerDistance, arena.DofStartPerDistance, arena.DofEndPerDistance);
            // backdrop houses stand far behind the board; a backed-off camera would leave them unshadowed
            _arenaLighting.FitShadows(distance, arena.ShadowReach);
        }

        /// <summary>
        /// The arena volume's own copy of its profile (the asset stays as the builder made it). The builder counts the
        /// haze heights from y = 0, where the board stands; a board elsewhere moves them along.
        /// </summary>
        private VolumeProfile ArenaProfile(BattleArenaSet arena)
        {
            if (_arenaProfile != null || arena.Volume == null || arena.Volume.sharedProfile == null) return _arenaProfile;
            _arenaProfile = arena.Volume.profile;
            float ground = arena.transform.position.y;
            if (ground != 0f && _arenaProfile.TryGet(out IslandHaze haze))
            {
                haze.fogStart.Override(haze.fogStart.value + ground);
                haze.fogFull.Override(haze.fogFull.value + ground);
            }
            return _arenaProfile;
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
            Soundscape.Enter(SoundScene.Colony);
            if (_battleScene.IsValid() && _battleScene.isLoaded)
                SceneManager.UnloadSceneAsync(_battleScene);
        }

        private void RestoreColony()
        {
            if (_restored) return;
            _restored = true;
            if (_screen != null)
            {
                _screen.StartRequested -= StartBattle;
                _screen.PauseToggled -= TogglePause;
                _screen.SpeedChosen -= SetSpeed;
                _screen.CloseRequested -= Close;
                _screen.KindDropped -= OnKindDropped;
                _screen.Close();
            }
            if (_deployment != null) _deployment.Changed -= ShowDeployment;
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

        private void OnDestroy()
        {
            RestoreColony();
            if (_arenaProfile != null) Destroy(_arenaProfile);
        }
    }
}
