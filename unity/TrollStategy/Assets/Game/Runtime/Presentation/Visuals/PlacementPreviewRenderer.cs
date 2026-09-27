using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Buildings;

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

            if (Mouse.current == null) return;
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
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

            bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
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
                _ghostRenderer.transform.position = centerPos;
                _ghostRenderer.transform.position += _worldView.GroundOffset(0.55f);
                _ghostRenderer.transform.rotation = _camera.transform.rotation;
                if (mode.Type == InteractionModeType.PlacingUnits)
                {
                    var unitView = ContentPrefabs.Unit(_catalog.GetUnit(mode.UnitKind));
                    _ghostRenderer.sprite = unitView != null ? unitView.IdleSprite : null;
                    _ghostRenderer.transform.localScale = Vector3.one * (unitView != null ? unitView.SpriteScale : 1f);
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

            if (leftClicked && Time.frameCount > _enteredFrame && !UIInputUtils.IsPointerOverUI())
            {
                if (movingBuilding)
                    _interaction.MoveBuilding(cell);
                else if (placingBuilding)
                    _interaction.PlaceBuilding(cell);
                else if (mode.Type == InteractionModeType.PlacingUnits)
                    _interaction.PlaceUnits(cell);
            }
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
