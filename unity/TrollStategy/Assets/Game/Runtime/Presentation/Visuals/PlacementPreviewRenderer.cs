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

            if (_ghostRenderer == null)
            {
                var go = new GameObject("GhostPreview");
                go.transform.SetParent(transform);
                _ghostRenderer = go.AddComponent<SpriteRenderer>();
                _ghostRenderer.sortingOrder = 100;
            }

            _ghostRenderer.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_session == null || _interaction == null || _camera == null || _worldView == null) return;

            var mode = _interaction.Mode;
            if (mode.Type != InteractionModeType.PlacingMine && mode.Type != InteractionModeType.PlacingUnits)
            {
                if (_ghostRenderer.gameObject.activeSelf)
                    _ghostRenderer.gameObject.SetActive(false);
                return;
            }

            Vector2 mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -_camera.transform.position.z));
            var cell = _worldView.WorldToCell(mouseWorld);

            if (!_worldView.IsInBounds(cell))
            {
                _ghostRenderer.gameObject.SetActive(false);
                return;
            }

            _ghostRenderer.gameObject.SetActive(true);

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                _interaction.CancelOrClear();
                return;
            }

            if (mode.Type == InteractionModeType.PlacingMine)
            {
                var mineDef = _catalog.GetBuilding(BuildingKind.Mine);
                _ghostRenderer.sprite = mineDef.Sprite;
                _ghostRenderer.transform.position = _worldView.BuildingCenterWorld(cell, mineDef.Width, mineDef.Height);

                bool valid = _session.CanBuildMine(cell).Ok;
                _ghostRenderer.color = valid ? new Color(0.2f, 1f, 0.3f, 0.75f) : new Color(1f, 0.2f, 0.2f, 0.75f);

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !UIInputUtils.IsPointerOverInteractiveUI())
                {
                    _interaction.PlaceMine(cell);
                }
            }
            else if (mode.Type == InteractionModeType.PlacingUnits)
            {
                var unitDef = _catalog.GetUnit(mode.UnitKind);
                _ghostRenderer.sprite = unitDef.IdleSprite;
                _ghostRenderer.transform.position = _worldView.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);

                bool valid = _session.CanBuyUnits(mode.UnitKind, mode.Amount, cell).Ok;
                _ghostRenderer.color = valid ? new Color(0.2f, 1f, 0.3f, 0.8f) : new Color(1f, 0.2f, 0.2f, 0.8f);

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !UIInputUtils.IsPointerOverInteractiveUI())
                {
                    _interaction.PlaceUnits(cell);
                }
            }
        }
    }
}
