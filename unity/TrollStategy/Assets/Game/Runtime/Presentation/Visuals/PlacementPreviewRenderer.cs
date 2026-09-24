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
        [SerializeField] private PrimitiveBuilding _mineModelPrefab;

        private GameSession _session;
        private InteractionController _interaction;
        private Camera _camera;
        private PrimitiveBuilding _mineGhost;
        private static readonly BuildingSnapshot MinePreview = new BuildingSnapshot(
            "preview-mine", BuildingKind.Mine, "Шахта", new Cell(0, 0), 3, 3, 0, 100, 0, 0, 0f);

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView, GameContentCatalog catalog, Camera cam)
        {
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _catalog = catalog;
            _camera = cam != null ? cam : Camera.main;
            if (_mineModelPrefab != null)
            {
                _mineGhost = Instantiate(_mineModelPrefab, transform);
                _mineGhost.name = "MinePreview3D";
                _mineGhost.Sync(MinePreview, false);
                _mineGhost.gameObject.SetActive(false);
            }

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

        private void Update()
        {
            if (_session == null || _interaction == null || _camera == null || _worldView == null) return;

            var mode = _interaction.Mode;
            if (mode.Type != _lastModeType)
            {
                _lastModeType = mode.Type;
                _enteredFrame = Time.frameCount;
            }

            if (mode.Type != InteractionModeType.PlacingMine && mode.Type != InteractionModeType.PlacingUnits)
            {
                if (_ghostRenderer != null && _ghostRenderer.gameObject.activeSelf)
                    _ghostRenderer.gameObject.SetActive(false);
                if (_boxOutlineRenderer != null && _boxOutlineRenderer.gameObject.activeSelf)
                    _boxOutlineRenderer.gameObject.SetActive(false);
                if (_crossRenderer != null && _crossRenderer.gameObject.activeSelf)
                    _crossRenderer.gameObject.SetActive(false);
                if (_mineGhost != null) _mineGhost.gameObject.SetActive(false);
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
                if (_mineGhost != null) _mineGhost.gameObject.SetActive(false);
                return;
            }

            bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
            float size = mode.Type == InteractionModeType.PlacingMine ? 3f : 1f;
            Vector3 centerPos = mode.Type == InteractionModeType.PlacingMine
                ? _worldView.BuildingCenterWorld(cell, 3, 3)
                : _worldView.MapToWorld(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f));

            bool valid = mode.Type == InteractionModeType.PlacingMine
                ? _session.CanBuildMine(cell).Ok
                : _session.CanBuyUnits(mode.UnitKind, mode.Amount, cell).Ok;

            Color themeColor = ColonyPalette.WithAlpha(
                valid ? ColonyPalette.GrassLight : ColonyPalette.Clay, 0.95f);

            if (_boxOutlineRenderer != null)
            {
                _boxOutlineRenderer.gameObject.SetActive(true);
                _boxOutlineRenderer.transform.position = centerPos;
                _boxOutlineRenderer.transform.position += _worldView.GroundOffset(0.19f);
                _boxOutlineRenderer.transform.rotation = _worldView.GroundRotation;
                _boxOutlineRenderer.transform.localScale = new Vector3(size, size, 1f);
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
                    var unitDef = _catalog.GetUnit(mode.UnitKind);
                    _ghostRenderer.sprite = unitDef.IdleSprite;
                    _ghostRenderer.transform.localScale = Vector3.one * unitDef.SpriteScale;
                }
                _ghostRenderer.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.75f);
            }
            if (_mineGhost != null)
            {
                _mineGhost.gameObject.SetActive(mode.Type == InteractionModeType.PlacingMine);
                _mineGhost.transform.position = centerPos;
                _mineGhost.transform.rotation = _worldView.GroundRotation;
                _mineGhost.Sync(MinePreview, true);
            }

            if (_crossRenderer != null)
            {
                if (!valid)
                {
                    _crossRenderer.gameObject.SetActive(true);
                    float half = size * 0.5f;
                    Vector3 origin = new Vector3(cell.X, cell.Y, 0f);
                    // Draw X cross inside footprint
                    _crossRenderer.SetPosition(0, _worldView.MapToWorld(origin) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(1, _worldView.MapToWorld(origin + new Vector3(size, size, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(2, _worldView.MapToWorld(origin + new Vector3(half, half, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(3, _worldView.MapToWorld(origin + new Vector3(size, 0f, 0f)) + _worldView.GroundOffset(0.2f));
                    _crossRenderer.SetPosition(4, _worldView.MapToWorld(origin + new Vector3(0f, size, 0f)) + _worldView.GroundOffset(0.2f));
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
                if (mode.Type == InteractionModeType.PlacingMine)
                    _interaction.PlaceMine(cell);
                else if (mode.Type == InteractionModeType.PlacingUnits)
                    _interaction.PlaceUnits(cell);
            }
        }
    }
}
