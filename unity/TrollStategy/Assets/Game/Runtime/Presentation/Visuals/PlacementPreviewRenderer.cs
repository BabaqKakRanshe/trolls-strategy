using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Units;

namespace TrollStrategy.Presentation.Visuals
{
    public class PlacementPreviewRenderer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _ghostRenderer;
        [SerializeField] private SpriteRenderer _boxOutlineRenderer;
        [SerializeField] private LineRenderer _crossRenderer;
        [SerializeField] private TilemapWorldView _worldView;
        [SerializeField] private GameContentCatalog _catalog;

        private GameSession _session;
        private InteractionController _interaction;
        private Camera _camera;
        private readonly Dictionary<BuildingKind, BuildingModel> _ghosts = new();
        private BuildingModel _activeGhost;
        // the tutorial's map of free cells (specs/006-tutorial-guidance, FR-009)
        private const float FreeCellsRefreshSeconds = .25f;
        private MeshRenderer _freeCells;
        private Mesh _freeCellsMesh;
        private GameSnapshot _freeCellsSnapshot;
        private UnitKind _freeCellsKind;
        private int _freeCellsAmount;
        private float _freeCellsAt = float.NegativeInfinity;

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView, GameContentCatalog catalog, Camera cam)
        {
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _catalog = catalog;
            _camera = cam != null ? cam : Camera.main;

            if (_boxOutlineRenderer == null)
            {
                var bGo = new GameObject("PlacementBoxOutline");
                bGo.transform.SetParent(transform, false);
                _boxOutlineRenderer = bGo.AddComponent<SpriteRenderer>();
                _boxOutlineRenderer.sprite = TrollStrategy.Presentation.Buildings.BuildingView.GetBoxOutlineSprite();
                _boxOutlineRenderer.sortingOrder = 98;
            }

            if (_ghostRenderer == null)
            {
                var go = new GameObject("GhostPreview");
                go.transform.SetParent(transform, false);
                _ghostRenderer = go.AddComponent<SpriteRenderer>();
                _ghostRenderer.sortingOrder = 99;
            }

            if (_crossRenderer == null)
            {
                var cGo = new GameObject("PlacementCrossLines");
                cGo.transform.SetParent(transform, false);
                _crossRenderer = cGo.AddComponent<LineRenderer>();
                _crossRenderer.positionCount = 5;
                _crossRenderer.useWorldSpace = true;
                _crossRenderer.startWidth = 0.06f;
                _crossRenderer.endWidth = 0.06f;
                _crossRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _crossRenderer.startColor = ColonyPalette.WithAlpha(ColonyPalette.Clay, 0.9f);
                _crossRenderer.endColor = ColonyPalette.WithAlpha(ColonyPalette.Clay, 0.9f);
                _crossRenderer.sortingOrder = 101;
            }

            _boxOutlineRenderer.gameObject.SetActive(false);
            _ghostRenderer.gameObject.SetActive(false);
            _crossRenderer.gameObject.SetActive(false);
        }

        private InteractionModeType _lastModeType = InteractionModeType.Neutral;
        private int _enteredFrame = -1;
        private Cell _lastGhostCell = new(-1, -1);

        private void Update()
        {
            if (_session == null || _interaction == null || _camera == null || _worldView == null) return;

            var mode = _interaction.Mode;
            UpdateFreeCells(mode);
            if (mode.Type != _lastModeType)
            {
                _lastModeType = mode.Type;
                _enteredFrame = Time.frameCount;
            }

            bool movingBuilding = mode.Type == InteractionModeType.MovingBuilding;
            bool placingBuilding = mode.Type == InteractionModeType.PlacingBuilding || movingBuilding;
            ShowGhost(placingBuilding ? GetGhost(mode.BuildingKind) : null);
            if (!placingBuilding && mode.Type != InteractionModeType.PlacingUnits)
            {
                if (_ghostRenderer != null && _ghostRenderer.gameObject.activeSelf)
                    _ghostRenderer.gameObject.SetActive(false);
                if (_boxOutlineRenderer != null && _boxOutlineRenderer.gameObject.activeSelf)
                    _boxOutlineRenderer.gameObject.SetActive(false);
                if (_crossRenderer != null && _crossRenderer.gameObject.activeSelf)
                    _crossRenderer.gameObject.SetActive(false);
                return;
            }

            if (Mouse.current == null && !MapPointer.UsesTouch) return;
            Vector2 mouseScreen = MapPointer.Position;
            if (!WorldProjection.TryGroundPoint(_camera, mouseScreen, _worldView, out var mouseWorld)) return;
            var cell = _worldView.WorldToCell(mouseWorld);

            if (!_worldView.IsInBounds(cell))
            {
                if (_ghostRenderer != null) _ghostRenderer.gameObject.SetActive(false);
                if (_boxOutlineRenderer != null) _boxOutlineRenderer.gameObject.SetActive(false);
                if (_crossRenderer != null) _crossRenderer.gameObject.SetActive(false);
                ShowGhost(null);
                return;
            }

            // a tap places where it lands; a tap never starts on the HUD
            bool leftClicked = MapPointer.UsesTouch
                ? MapPointer.Tapped(out _)
                : Mouse.current.leftButton.wasPressedThisFrame && !UIInputUtils.IsPointerOverUI();
            var definition = placingBuilding ? _catalog.GetBuilding(mode.BuildingKind) : null;
            int width = definition != null ? definition.Width : 1;
            int height = definition != null ? definition.Height : 1;
            Vector3 centerPos = placingBuilding
                ? _worldView.BuildingCenterWorld(cell, width, height)
                : _worldView.MapToWorld(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f));

            bool valid = placingBuilding
                ? _session.CanPlaceBuilding(mode.BuildingKind, cell, movingBuilding ? mode.BuildingId : null).Ok
                : _session.CanBuyUnits(mode.UnitKind, mode.Amount, cell).Ok;

            Color themeColor = ColonyPalette.WithAlpha(
                valid ? ColonyPalette.GrassLight : ColonyPalette.Clay, 0.95f);

            if (_boxOutlineRenderer != null)
            {
                _boxOutlineRenderer.gameObject.SetActive(true);
                _boxOutlineRenderer.transform.position = centerPos;
                _boxOutlineRenderer.transform.position += _worldView.GroundOffset(0.19f);
                _boxOutlineRenderer.transform.rotation = _worldView.GroundRotation;
                _boxOutlineRenderer.transform.localScale = new Vector3(width, height, 1f);
                _boxOutlineRenderer.color = themeColor;
            }

            if (_ghostRenderer != null)
            {
                _ghostRenderer.gameObject.SetActive(mode.Type == InteractionModeType.PlacingUnits);
                _ghostRenderer.transform.rotation = _camera.transform.rotation;
                if (mode.Type == InteractionModeType.PlacingUnits)
                {
                    var unitView = ContentPrefabs.Unit(_catalog.GetUnit(mode.UnitKind));
                    _ghostRenderer.sprite = unitView != null ? unitView.IdleSprite : null;
                    _ghostRenderer.transform.localScale = Vector3.one * (unitView != null ? unitView.SpriteScale : 1f);
                    // the creature it will be: feet on the lawn, as UnitView stands it
                    _ghostRenderer.transform.position = centerPos + _worldView.GroundOffset(UnitView.GroundLift) +
                        _camera.transform.up * (unitView != null ? unitView.PivotAboveFeet : 0f);
                }
                _ghostRenderer.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.75f);
            }
            if (_activeGhost != null)
            {
                _activeGhost.transform.position = centerPos;
                _activeGhost.transform.rotation = _worldView.GroundRotation;
                // the ghost clicks into each new cell, so the grid feels snapped rather than floaty
                if (cell != _lastGhostCell) TrollStrategy.Presentation.Feel.Juice.Punch(_activeGhost.transform, .07f, .2f);
            }
            _lastGhostCell = cell;

            if (_crossRenderer != null)
            {
                if (!valid)
                {
                    _crossRenderer.gameObject.SetActive(true);
                    Vector3 origin = new Vector3(cell.X, cell.Y, 0f);
                    // Draw X cross inside footprint
                    _crossRenderer.SetPosition(0, _worldView.MapToWorld(origin) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(1, _worldView.MapToWorld(origin + new Vector3(width, height, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(2, _worldView.MapToWorld(origin + new Vector3(width * 0.5f, height * 0.5f, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(3, _worldView.MapToWorld(origin + new Vector3(width, 0f, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(4, _worldView.MapToWorld(origin + new Vector3(0f, height, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.startColor = themeColor;
                    _crossRenderer.endColor = themeColor;
                }
                else
                {
                    _crossRenderer.gameObject.SetActive(false);
                }
            }

            if (leftClicked && Time.frameCount > _enteredFrame)
            {
                if (movingBuilding)
                    _interaction.MoveBuilding(cell);
                else if (placingBuilding)
                    _interaction.PlaceBuilding(cell);
                else if (mode.Type == InteractionModeType.PlacingUnits)
                    _interaction.PlaceUnits(cell);
            }
        }

        // Where the next creature may stand: a light square on every free cell, while a tutorial step waits for a
        // cell and the hints are on. The cells are the ones the purchase itself accepts.
        private void UpdateFreeCells(InteractionMode mode)
        {
            var snapshot = _session.CurrentSnapshot;
            var quest = snapshot.Progress.Enabled ? snapshot.Progress.Quest : null;
            bool show = mode.Type == InteractionModeType.PlacingUnits && GameSettings.TutorialHints &&
                        quest != null && quest.IsTutorial && !quest.IsComplete;
            if (!show)
            {
                if (_freeCells != null && _freeCells.enabled) _freeCells.enabled = false;
                _freeCellsSnapshot = null;
                return;
            }
            EnsureFreeCells();
            _freeCells.enabled = true;
            bool stale = snapshot != _freeCellsSnapshot || mode.UnitKind != _freeCellsKind || mode.Amount != _freeCellsAmount;
            if (!stale || Time.unscaledTime - _freeCellsAt < FreeCellsRefreshSeconds) return;
            _freeCellsSnapshot = snapshot;
            _freeCellsKind = mode.UnitKind;
            _freeCellsAmount = mode.Amount;
            _freeCellsAt = Time.unscaledTime;
            BuildFreeCells(mode.UnitKind, mode.Amount);
        }

        private void EnsureFreeCells()
        {
            if (_freeCells != null) return;
            var go = new GameObject("TutorialFreeCells");
            go.transform.SetParent(transform, false);
            _freeCellsMesh = new Mesh { name = "TutorialFreeCells" };
            _freeCellsMesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = _freeCellsMesh;
            _freeCells = go.AddComponent<MeshRenderer>();
            _freeCells.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            _freeCells.sortingOrder = 97;
            _freeCells.shadowCastingMode = ShadowCastingMode.Off;
            _freeCells.receiveShadows = false;
        }

        private void BuildFreeCells(UnitKind kind, int amount)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var fill = ColonyPalette.WithAlpha(Color.white, .32f);
            var lift = _worldView.GroundOffset(0.14f);
            var local = _freeCells.transform;
            const float inset = .08f;
            for (int y = 0; y < _worldView.GridHeight; y++)
            {
                for (int x = 0; x < _worldView.GridWidth; x++)
                {
                    if (!_session.CanBuyUnits(kind, amount, new Cell(x, y)).Ok) continue;
                    int first = vertices.Count;
                    vertices.Add(local.InverseTransformPoint(_worldView.MapToWorld(new Vector3(x + inset, y + inset, 0f)) + lift));
                    vertices.Add(local.InverseTransformPoint(_worldView.MapToWorld(new Vector3(x + 1 - inset, y + inset, 0f)) + lift));
                    vertices.Add(local.InverseTransformPoint(_worldView.MapToWorld(new Vector3(x + 1 - inset, y + 1 - inset, 0f)) + lift));
                    vertices.Add(local.InverseTransformPoint(_worldView.MapToWorld(new Vector3(x + inset, y + 1 - inset, 0f)) + lift));
                    for (int i = 0; i < 4; i++) colors.Add(fill);
                    triangles.Add(first);
                    triangles.Add(first + 1);
                    triangles.Add(first + 2);
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 3);
                }
            }
            _freeCellsMesh.Clear();
            _freeCellsMesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _freeCellsMesh.SetVertices(vertices);
            _freeCellsMesh.SetColors(colors);
            _freeCellsMesh.SetTriangles(triangles, 0);
            _freeCellsMesh.RecalculateBounds();
        }

        private BuildingModel GetGhost(BuildingKind kind)
        {
            if (_ghosts.TryGetValue(kind, out var ghost)) return ghost;
            var model = ContentPrefabs.Building(_catalog.GetBuilding(kind))?.Model;
            if (model != null)
            {
                ghost = Instantiate(model, transform);
                ghost.name = $"{kind}Preview3D";
                ghost.SetHighlighted(true);
                ghost.gameObject.SetActive(false);
            }
            _ghosts[kind] = ghost;
            return ghost;
        }

        private void ShowGhost(BuildingModel ghost)
        {
            if (_activeGhost == ghost) return;
            if (_activeGhost != null) _activeGhost.gameObject.SetActive(false);
            _activeGhost = ghost;
            if (_activeGhost != null) _activeGhost.gameObject.SetActive(true);
        }
    }
}
