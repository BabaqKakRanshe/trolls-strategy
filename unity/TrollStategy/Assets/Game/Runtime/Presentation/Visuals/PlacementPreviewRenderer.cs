using UnityEngine;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;

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
                _crossRenderer.startColor = new Color(0.95f, 0.3f, 0.3f, 0.9f);
                _crossRenderer.endColor = new Color(0.95f, 0.3f, 0.3f, 0.9f);
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
                return;
            }

            if (Mouse.current == null) return;
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -_camera.transform.position.z));
            var cell = _worldView.WorldToCell(mouseWorld);

            if (!_worldView.IsInBounds(cell))
            {
                if (_ghostRenderer != null) _ghostRenderer.gameObject.SetActive(false);
                if (_boxOutlineRenderer != null) _boxOutlineRenderer.gameObject.SetActive(false);
                if (_crossRenderer != null) _crossRenderer.gameObject.SetActive(false);
                return;
            }

            bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
            float size = mode.Type == InteractionModeType.PlacingMine ? 3f : 1f;
            Vector3 centerPos = mode.Type == InteractionModeType.PlacingMine
                ? _worldView.BuildingCenterWorld(cell, 3, 3)
                : _worldView.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);

            bool valid = mode.Type == InteractionModeType.PlacingMine
                ? _session.CanBuildMine(cell).Ok
                : _session.CanBuyUnits(mode.UnitKind, mode.Amount, cell).Ok;

            Color themeColor = valid
                ? new Color(0.553f, 0.941f, 0.424f, 0.95f) // #8df06c
                : new Color(0.941f, 0.392f, 0.341f, 0.95f); // #f06457

            if (_boxOutlineRenderer != null)
            {
                _boxOutlineRenderer.gameObject.SetActive(true);
                _boxOutlineRenderer.transform.position = centerPos;
                _boxOutlineRenderer.transform.localScale = new Vector3(size, size, 1f);
                _boxOutlineRenderer.color = themeColor;
            }

            if (_ghostRenderer != null)
            {
                _ghostRenderer.gameObject.SetActive(true);
                _ghostRenderer.transform.position = centerPos;
                if (mode.Type == InteractionModeType.PlacingMine)
                {
                    var mineDef = _catalog.GetBuilding(BuildingKind.Mine);
                    _ghostRenderer.sprite = mineDef.Sprite;
                    _ghostRenderer.transform.localScale = Vector3.one;
                }
                else
                {
                    var unitDef = _catalog.GetUnit(mode.UnitKind);
                    _ghostRenderer.sprite = unitDef.IdleSprite;
                    _ghostRenderer.transform.localScale = Vector3.one * unitDef.SpriteScale;
                }
                _ghostRenderer.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.75f);
            }

            if (_crossRenderer != null)
            {
                if (!valid)
                {
                    _crossRenderer.gameObject.SetActive(true);
                    float half = size * 0.5f;
                    Vector3 origin = _worldView.CellToWorld(cell);
                    // Draw X cross inside footprint
                    _crossRenderer.SetPosition(0, origin);
                    _crossRenderer.SetPosition(1, origin + new Vector3(size, size, 0f));
                    _crossRenderer.SetPosition(2, origin + new Vector3(half, half, 0f));
                    _crossRenderer.SetPosition(3, origin + new Vector3(size, 0f, 0f));
                    _crossRenderer.SetPosition(4, origin + new Vector3(0f, size, 0f));
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
