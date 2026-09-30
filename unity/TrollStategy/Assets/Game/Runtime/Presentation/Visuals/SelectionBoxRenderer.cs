using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Visuals
{
    public class SelectionBoxRenderer : MonoBehaviour
    {
        private const float UnitClickRadius = 0.48f;
        private const float DoubleClickSeconds = 0.35f;
        private const float DoubleClickMaxPixels = 12f;

        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private SpriteRenderer _fillRenderer;
        private InteractionController _interaction;
        private UnitVisualsManager _unitVisuals;
        private BuildingVisualsManager _buildingVisuals;
        private TilemapWorldView _worldView;
        private Camera _camera;
        private Vector3 _startWorldPos;
        private Vector2 _startScreenPos;
        private bool _isDragging;
        private bool _dragVisible;
        private float _lastUnitClickTime = float.NegativeInfinity;
        private Vector2 _lastUnitClickScreenPos;

        public void Init(InteractionController interaction, UnitVisualsManager unitVisuals, Camera cam,
            TilemapWorldView worldView = null, BuildingVisualsManager buildingVisuals = null)
        {
            _interaction = interaction;
            _unitVisuals = unitVisuals;
            _buildingVisuals = buildingVisuals;
            _worldView = worldView;
            _camera = cam != null ? cam : Camera.main;

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                _lineRenderer.positionCount = 5;
                _lineRenderer.startWidth = 0.045f;
                _lineRenderer.endWidth = 0.045f;
                _lineRenderer.useWorldSpace = true;
                _lineRenderer.sortingOrder = 96;
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _lineRenderer.startColor = ColonyPalette.GrassLight;
                _lineRenderer.endColor = ColonyPalette.GrassLight;
            }

            if (_fillRenderer == null)
            {
                var fillObject = new GameObject("SelectionFill");
                fillObject.transform.SetParent(transform, false);
                _fillRenderer = fillObject.AddComponent<SpriteRenderer>();
                _fillRenderer.sprite = CreateFillSprite();
                _fillRenderer.color = ColonyPalette.WithAlpha(ColonyPalette.GrassLight, 0.16f);
                _fillRenderer.sortingOrder = 95;
            }

            SetMarqueeVisible(false);
        }

        private void Update()
        {
            if (_interaction == null || _camera == null) return;
            // a finger selects by tapping; a finger that moves is the camera's, so touch draws no marquee
            if (MapPointer.UsesTouch)
            {
                ResetDrag();
                if (!MapPointer.Tapped(out var tap)) return;
                var tapWorld = ScreenToWorld(tap);
                if (_interaction.Mode.Type == InteractionModeType.Neutral) SelectPoint(tapWorld, tap, false);
                else if (IsBuildingTargetMode(_interaction.Mode.Type)) SelectBuildingPoint(tapWorld);
                return;
            }
            if (Mouse.current == null) return;
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = ScreenToWorld(mouseScreen);

            if (_interaction.Mode.Type != InteractionModeType.Neutral)
            {
                ResetDrag();
                if (IsBuildingTargetMode(_interaction.Mode.Type) &&
                    Mouse.current.leftButton.wasReleasedThisFrame &&
                    !UIInputUtils.IsPointerOverUI())
                {
                    SelectBuildingPoint(mouseWorld);
                }
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (UIInputUtils.IsPointerOverUI()) return;

                _startScreenPos = mouseScreen;
                _startWorldPos = mouseWorld;
                _isDragging = true;
                _dragVisible = false;
                SetMarqueeVisible(false);
            }

            if (_isDragging && Mouse.current.leftButton.isPressed)
            {
                if (!_dragVisible && UIInputUtils.IsDrag(_startScreenPos, mouseScreen))
                {
                    _dragVisible = true;
                    SetMarqueeVisible(true);
                }

                if (_dragVisible) DrawMarquee(_startWorldPos, mouseWorld);
            }

            if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool wasMarquee = _dragVisible;
                ResetDrag();

                if (wasMarquee)
                    SelectRectangle(_startWorldPos, mouseWorld, IsAdditiveModifierPressed());
                else
                    SelectPoint(mouseWorld, mouseScreen, IsAdditiveModifierPressed());
            }
        }

        private void SelectRectangle(Vector3 start, Vector3 end, bool additive)
        {
            if (_worldView != null)
            {
                start = _worldView.WorldToMap(start);
                end = _worldView.WorldToMap(end);
            }
            float xMin = Mathf.Min(start.x, end.x);
            float xMax = Mathf.Max(start.x, end.x);
            float yMin = Mathf.Min(start.y, end.y);
            float yMax = Mathf.Max(start.y, end.y);
            var enclosed = new List<string>();

            if (_unitVisuals != null)
            {
                foreach (var pair in _unitVisuals.Views)
                {
                    // creatures working inside a building are out of reach
                    if (pair.Value.IsHiddenInside) continue;
                    var pos = _worldView != null
                        ? _worldView.WorldToMap(pair.Value.transform.position)
                        : pair.Value.transform.position;
                    if (pos.x >= xMin && pos.x <= xMax && pos.y >= yMin && pos.y <= yMax)
                        enclosed.Add(pair.Key);
                }
            }

            enclosed.Sort(System.StringComparer.Ordinal);
            _interaction.SelectUnits(enclosed, additive);
        }

        private void SelectPoint(Vector3 mouseWorld, Vector2 mouseScreen, bool additive)
        {
            UnitView nearestUnit = FindUnitAt(mouseWorld);

            if (nearestUnit != null)
            {
                float now = Time.unscaledTime;
                bool isDoubleClick = now - _lastUnitClickTime <= DoubleClickSeconds &&
                                     Vector2.Distance(_lastUnitClickScreenPos, mouseScreen) <= DoubleClickMaxPixels;
                if (isDoubleClick)
                {
                    _lastUnitClickTime = float.NegativeInfinity;
                    _interaction.InspectSquad(nearestUnit.UnitId, FindUnitIdsAt(mouseWorld));
                    return;
                }

                _lastUnitClickTime = now;
                _lastUnitClickScreenPos = mouseScreen;
                _interaction.ClickUnit(nearestUnit.UnitId, additive);
                return;
            }

            _lastUnitClickTime = float.NegativeInfinity;
            if (SelectBuildingPoint(mouseWorld)) return;

            if (!additive) _interaction.CancelOrClear();
        }

        public bool IsBuildingAt(Vector2 screenPosition)
        {
            if (_camera == null || _buildingVisuals == null ||
                !WorldProjection.TryGroundPoint(_camera, screenPosition, _worldView, out var world))
                return false;

            foreach (var pair in _buildingVisuals.Views)
            {
                if (pair.Value.ContainsWorldPoint(world)) return true;
            }
            return false;
        }

        private UnitView FindUnitAt(Vector3 worldPoint)
        {
            if (_unitVisuals == null) return null;
            var mapPoint = _worldView != null ? _worldView.WorldToMap(worldPoint) : worldPoint;
            UnitView nearest = null;
            float nearestSqrDistance = UnitClickRadius * UnitClickRadius;
            var selectable = _interaction.SelectableUnitIds();
            foreach (var pair in _unitVisuals.Views)
            {
                // a worker inside, or just stepping in, must not take the click meant for its building
                if (pair.Value.IsHiddenInside || !selectable.Contains(pair.Key)) continue;
                var unitMapPoint = _worldView != null
                    ? _worldView.WorldToMap(pair.Value.transform.position)
                    : pair.Value.transform.position;
                float sqrDistance = ((Vector2)(unitMapPoint - mapPoint)).sqrMagnitude;
                if (sqrDistance <= nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = pair.Value;
                }
            }
            return nearest;
        }

        private List<string> FindUnitIdsAt(Vector3 worldPoint)
        {
            var ids = new List<string>();
            if (_unitVisuals == null) return ids;
            var mapPoint = _worldView != null ? _worldView.WorldToMap(worldPoint) : worldPoint;
            float maxSqrDistance = UnitClickRadius * UnitClickRadius;
            var selectable = _interaction.SelectableUnitIds();
            foreach (var pair in _unitVisuals.Views)
            {
                if (pair.Value.IsHiddenInside || !selectable.Contains(pair.Key)) continue;
                var unitMapPoint = _worldView != null
                    ? _worldView.WorldToMap(pair.Value.transform.position)
                    : pair.Value.transform.position;
                if (((Vector2)(unitMapPoint - mapPoint)).sqrMagnitude <= maxSqrDistance)
                    ids.Add(pair.Key);
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        private bool SelectBuildingPoint(Vector3 mouseWorld)
        {
            if (_buildingVisuals == null) return false;
            foreach (var pair in _buildingVisuals.Views)
            {
                var building = pair.Value;
                if (!building.ContainsWorldPoint(mouseWorld)) continue;

                _interaction.ChooseBuilding(building.BuildingId);
                return true;
            }
            return false;
        }

        private void DrawMarquee(Vector3 start, Vector3 end)
        {
            if (_worldView != null)
            {
                start = _worldView.WorldToMap(start);
                end = _worldView.WorldToMap(end);
            }
            float xMin = Mathf.Min(start.x, end.x);
            float xMax = Mathf.Max(start.x, end.x);
            float yMin = Mathf.Min(start.y, end.y);
            float yMax = Mathf.Max(start.y, end.y);

            _lineRenderer.SetPosition(0, ProjectMarqueePoint(xMin, yMin));
            _lineRenderer.SetPosition(1, ProjectMarqueePoint(xMax, yMin));
            _lineRenderer.SetPosition(2, ProjectMarqueePoint(xMax, yMax));
            _lineRenderer.SetPosition(3, ProjectMarqueePoint(xMin, yMax));
            _lineRenderer.SetPosition(4, ProjectMarqueePoint(xMin, yMin));

            _fillRenderer.transform.position = ProjectMarqueePoint((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            _fillRenderer.transform.rotation = _worldView != null ? _worldView.GroundRotation : Quaternion.identity;
            _fillRenderer.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);
        }

        private Vector3 ProjectMarqueePoint(float x, float y)
        {
            var point = new Vector3(x, y, 0f);
            return _worldView != null
                ? _worldView.MapToWorld(point) + _worldView.GroundOffset(0.24f)
                : point;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            return WorldProjection.TryGroundPoint(_camera, screenPosition, _worldView, out var world) ? world : Vector3.zero;
        }

        private void ResetDrag()
        {
            _isDragging = false;
            _dragVisible = false;
            SetMarqueeVisible(false);
        }

        private void SetMarqueeVisible(bool visible)
        {
            if (_lineRenderer != null) _lineRenderer.enabled = visible;
            if (_fillRenderer != null) _fillRenderer.gameObject.SetActive(visible);
        }

        private static bool IsAdditiveModifierPressed()
        {
            return Keyboard.current != null &&
                   (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
        }

        private static bool IsBuildingTargetMode(InteractionModeType mode)
        {
            return mode == InteractionModeType.ChoosingWorkTarget ||
                   mode == InteractionModeType.ChoosingHaulSource ||
                   mode == InteractionModeType.ChoosingHaulDestination;
        }

        private static Sprite CreateFillSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "SelectionFillTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
