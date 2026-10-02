using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.WorldUi;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;
using Label = UnityEngine.UIElements.Label;
using Pivot = UnityEngine.UIElements.Pivot;

namespace TrollStrategy.Presentation.Buildings
{
    public class BuildingView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Building this prefab renders; also identifies instances placed in the scene as starting buildings.")]
        [SerializeField] private BuildingKind _kind;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionHighlight;
        [Tooltip("Production bar; move it in the prefab to place it.")]
        [SerializeField] private BuildingProgressBar _productionProgress;
        [Tooltip("Name and stock over the building while it is hovered or targeted; stands on the model's roof.")]
        [SerializeField] private WorldPanel _label;
        [SerializeField] private BoxCollider2D _collider;
        [Tooltip("Model child of this building variant.")]
        [SerializeField] private BuildingModel _model;

        // gap between the top of the model and the bottom of its label, m
        private const float LabelGap = .3f;

        private BuildingSnapshot _snapshot;
        private TilemapWorldView _worldView;
        private Label _labelTitle;
        private Label _labelInfo;
        private MeshRenderer[] _modelMeshes = Array.Empty<MeshRenderer>();

        public BuildingKind Kind => _kind;
        public string BuildingId => _snapshot?.Id;
        public BuildingModel Model => _model;
        public Sprite OutgoingProductSprite => _model != null ? _model.OutgoingProductSprite : null;

        public void PlaySale(Sprite productSprite, int gold)
        {
            if (gold <= 0) return;
            var anchor = _model != null ? _model.SaleFeedbackAnchor : null;
            if (anchor == null)
            {
                Debug.LogError($"{name} has no sale feedback anchor", this);
                return;
            }
            SaleFeedback.Spawn(anchor.position, productSprite, _model.SaleIncomeSprite, gold);
        }

        public void Setup(BuildingSnapshot snapshot, Sprite sprite, Action<string> onClick, TilemapWorldView worldView = null)
        {
            _snapshot = snapshot;
            _worldView = worldView;
            _ = onClick;

            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 10;
                if (sprite != null) _spriteRenderer.sprite = sprite;
                _spriteRenderer.enabled = false;
            }
            if (_model == null)
                Debug.LogError($"{name} has no building model", this);
            else
            {
                if (_worldView != null)
                {
                    _model.transform.localPosition = Vector3.zero;
                    _model.transform.localRotation = Quaternion.identity;
                }
                _modelMeshes = _model.GetComponentsInChildren<MeshRenderer>(true);
            }

            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.size = new Vector2(snapshot.Width, snapshot.Height);
                _collider.offset = Vector2.zero;
                _collider.enabled = _worldView == null;
            }

            if (_productionProgress == null)
                Debug.LogError($"{name} has no production progress bar", this);

            // the label stands on the roof and grows upwards as the camera backs off
            if (_label != null) _label.Pivot = Pivot.BottomCenter;

            UpdateVisuals(snapshot, false);
        }

        private static Sprite _proceduralBoxOutlineSprite;

        public static Sprite GetBoxOutlineSprite()
        {
            if (_proceduralBoxOutlineSprite != null) return _proceduralBoxOutlineSprite;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color border = Color.white;
            Color fill = new Color(1f, 1f, 1f, 0.16f);
            int bw = 4;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < bw || x >= size - bw || y < bw || y >= size - bw;
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            _proceduralBoxOutlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _proceduralBoxOutlineSprite;
        }

        [SerializeField] private SpriteRenderer _spriteOutline;
        private static readonly Vector2[] OutlineDirections =
        {
            Vector2.left,
            Vector2.right,
            Vector2.up,
            Vector2.down,
            new Vector2(-0.7071f, -0.7071f),
            new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f),
            new Vector2(0.7071f, 0.7071f)
        };

        private static readonly Color OutlineColor = ColonyPalette.Gold;
        private SpriteRenderer[] _outlineRenderers;
        private bool _isHovered;
        private bool _isTarget;

        private void Update()
        {
            if (_snapshot == null || Mouse.current == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            if (!WorldProjection.TryGroundPoint(cam, mouseScreen, _worldView, out var mouseWorld)) return;
            bool isInside = ContainsWorldPoint(mouseWorld);
            if (isInside != _isHovered)
            {
                _isHovered = isInside;
                ApplyActiveHighlight();
            }
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (_snapshot == null) return false;
            var point = _worldView != null ? _worldView.WorldToMap(worldPoint) : worldPoint;
            float cellSize = _worldView != null ? _worldView.CellSize : 1f;
            return point.x >= _snapshot.Cell.X * cellSize && point.x < (_snapshot.Cell.X + _snapshot.Width) * cellSize &&
                   point.y >= _snapshot.Cell.Y * cellSize && point.y < (_snapshot.Cell.Y + _snapshot.Height) * cellSize;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            ApplyActiveHighlight();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            ApplyActiveHighlight();
        }

        private void OnMouseEnter()
        {
            _isHovered = true;
            ApplyActiveHighlight();
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            ApplyActiveHighlight();
        }

        private void ApplyActiveHighlight()
        {
            bool show = _isTarget || _isHovered;
            if (_model != null) _model.SetHighlighted(show);
            if (_selectionHighlight != null)
                _selectionHighlight.gameObject.SetActive(false);

            SetOutlineVisible(false);

            if (_label != null)
                _label.Visible = show;

            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;
        }

        private void EnsureHighlightVisuals()
        {
            if (_selectionHighlight != null)
                _selectionHighlight.gameObject.SetActive(false);

            if (_spriteOutline == null)
            {
                var soGo = new GameObject("SpriteOutline");
                soGo.transform.SetParent(transform, false);
                soGo.transform.localPosition = Vector3.zero;

                _spriteOutline = soGo.AddComponent<SpriteRenderer>();
            }

            _spriteOutline.gameObject.SetActive(false);
            for (int i = 1; i < OutlineDirections.Length; i++)
            {
                var oldOutline = transform.Find($"SpriteOutline_{i}");
                if (oldOutline != null) oldOutline.gameObject.SetActive(false);
            }

            if (_spriteRenderer == null || !_spriteRenderer.enabled || _spriteRenderer.sprite == null)
                return;

            EnsureOutlineRenderers();
            float offset = 4f / _spriteRenderer.sprite.pixelsPerUnit;
            for (int i = 0; i < _outlineRenderers.Length; i++)
            {
                var outline = _outlineRenderers[i];
                outline.sprite = _spriteRenderer.sprite;
                outline.sortingLayerID = _spriteRenderer.sortingLayerID;
                outline.sortingOrder = _spriteRenderer.sortingOrder - 1;
                outline.color = OutlineColor;
                outline.transform.localScale = Vector3.one;
                outline.transform.localPosition = (Vector3)(OutlineDirections[i] * offset);
            }
        }

        private void EnsureOutlineRenderers()
        {
            if (_outlineRenderers != null && _outlineRenderers.Length == OutlineDirections.Length)
                return;

            _outlineRenderers = new SpriteRenderer[OutlineDirections.Length];
            _outlineRenderers[0] = _spriteOutline;
            for (int i = 1; i < _outlineRenderers.Length; i++)
            {
                string objectName = $"SpriteOutline_{i}";
                var child = transform.Find(objectName);
                if (child == null)
                {
                    child = new GameObject(objectName).transform;
                    child.SetParent(transform, false);
                }

                _outlineRenderers[i] = child.GetComponent<SpriteRenderer>();
                if (_outlineRenderers[i] == null)
                    _outlineRenderers[i] = child.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private void SetOutlineVisible(bool visible)
        {
            if (_outlineRenderers == null) return;
            foreach (var outline in _outlineRenderers)
                if (outline != null) outline.gameObject.SetActive(visible);
        }

        private void UpdateProductionProgress(BuildingSnapshot snapshot)
        {
            if (_productionProgress == null) return;
            bool supportsProduction = snapshot.MaxWorkers > 0;
            _productionProgress.gameObject.SetActive(supportsProduction);
            if (supportsProduction) _productionProgress.SetProgress(snapshot.ProductionProgress);
        }

        public void UpdateVisuals(BuildingSnapshot snapshot, bool isTarget)
        {
            _snapshot = snapshot;
            _isTarget = isTarget;

            EnsureHighlightVisuals();

            ApplyActiveHighlight();

            UpdateProductionProgress(snapshot);

            if (_label != null)
            {
                PlaceLabel(snapshot);
                _label.Face(Camera.main);
                if (_labelTitle == null)
                {
                    _labelTitle = _label.AddLabel("world-label world-label--building");
                    _labelInfo = _label.AddLabel("world-label world-label--building-info");
                }
                string info = LabelInfo(snapshot);
                _labelTitle.text = Localization.T(snapshot.Name);
                _labelInfo.text = Localization.T(info);
                _labelInfo.style.display = info.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // over the middle of the model and clear of its top: seen from above, the building never covers its label
        private void PlaceLabel(BuildingSnapshot snapshot)
        {
            // the building lies in the map plane with its local -z pointing up from the ground
            var up = transform.rotation * Vector3.back;
            if (!ModelBounds(out var bounds))
            {
                _label.transform.localPosition = new Vector3(0f, snapshot.Height * 0.5f + 0.45f, -2.2f);
                return;
            }
            var extents = bounds.extents;
            float halfHeight = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
            _label.transform.position = bounds.center + up * (halfHeight + LabelGap);
        }

        private bool ModelBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var mesh in _modelMeshes)
            {
                if (mesh == null || !mesh.enabled || !mesh.gameObject.activeInHierarchy) continue;
                if (any) bounds.Encapsulate(mesh.bounds);
                else bounds = mesh.bounds;
                any = true;
            }
            return any;
        }

        private static string LabelInfo(BuildingSnapshot snapshot)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (snapshot.IsWorkplace) parts.Add($"Раб: {snapshot.WorkerCount}/{snapshot.MaxWorkers}");
            if (snapshot.Slots.Count > 0) parts.Add($"Товары: {snapshot.TotalStock}/{snapshot.Capacity}");
            else if (snapshot.Stock.Count > 0)
            {
                // Show the last good in the list: outputs follow inputs in ResourceKind order along each chain.
                var shown = snapshot.Stock[snapshot.Stock.Count - 1];
                parts.Add($"{shown.Name}: {shown.Amount}/{snapshot.Capacity}");
            }
            return string.Join(" | ", parts);
        }
    }
}
