using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Buildings
{
    public class BuildingView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionHighlight;
        [SerializeField] private SpriteRenderer _progressBar;
        [SerializeField] private SpriteRenderer _progressTrack;
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private BoxCollider2D _collider;
        [SerializeField] private PrimitiveBuilding _mineModelPrefab;
        [SerializeField] private PrimitiveBuilding _warehouseModelPrefab;
        [SerializeField] private PrimitiveBuilding _marketModelPrefab;
        [SerializeField] private PrimitiveBuilding _barracksModelPrefab;

        private BuildingSnapshot _snapshot;
        private PrimitiveBuilding _model;
        private TilemapWorldView _worldView;

        public string BuildingId => _snapshot?.Id;

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
            EnsureModel(snapshot.Kind);
            if (_model != null && _worldView != null)
            {
                _model.transform.localPosition = Vector3.zero;
                _model.transform.localRotation = Quaternion.identity;
            }

            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.size = new Vector2(snapshot.Width, snapshot.Height);
                _collider.offset = Vector2.zero;
                _collider.enabled = _worldView == null;
            }

            EnsureProgressVisuals();

            if (_label != null)
            {
                _label.transform.localPosition = new Vector3(0f, snapshot.Height * 0.5f + 0.35f, 0f);
            }

            UpdateVisuals(snapshot, false);
        }

        private static Sprite _proceduralBoxOutlineSprite;

        private void EnsureModel(BuildingKind kind)
        {
            var prefab = kind switch
            {
                BuildingKind.Mine => _mineModelPrefab,
                BuildingKind.Warehouse => _warehouseModelPrefab,
                BuildingKind.Market => _marketModelPrefab,
                BuildingKind.Barracks => _barracksModelPrefab,
                _ => null
            };
            if (prefab == null)
            {
                Debug.LogError($"Missing model prefab for {kind}", this);
                return;
            }

            if (_model == null)
                _model = transform.Find("PrimitiveModel")?.GetComponent<PrimitiveBuilding>();
            if (_model != null && _model.Kind == kind) return;
            if (_model != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(_model.gameObject);
                else DestroyImmediate(_model.gameObject);
            }
#if UNITY_EDITOR
            _model = !UnityEngine.Application.isPlaying
                ? (PrimitiveBuilding)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform)
                : Instantiate(prefab, transform);
#else
            _model = Instantiate(prefab, transform);
#endif
            _model.name = "PrimitiveModel";
        }

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
        private static readonly Color ProgressFillColor = ColonyPalette.Gold;
        private static readonly Color ProgressTrackColor = ColonyPalette.WithAlpha(ColonyPalette.Night, 0.9f);
        private static Sprite _solidSprite;
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
            if (_model != null && _snapshot != null) _model.Sync(_snapshot, show);
            if (_selectionHighlight != null)
                _selectionHighlight.gameObject.SetActive(false);

            SetOutlineVisible(false);

            if (_label != null)
                _label.gameObject.SetActive(show);

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

        private void EnsureProgressVisuals()
        {
            if (_progressBar == null)
            {
                var fillTransform = transform.Find("ProductionProgressFill") ?? transform.Find("ProgressBar");
                if (fillTransform == null)
                {
                    fillTransform = new GameObject("ProductionProgressFill").transform;
                    fillTransform.SetParent(transform, false);
                }

                _progressBar = fillTransform.GetComponent<SpriteRenderer>();
                if (_progressBar == null) _progressBar = fillTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (_progressTrack == null)
            {
                var trackTransform = transform.Find("ProductionProgressTrack");
                if (trackTransform == null)
                {
                    trackTransform = new GameObject("ProductionProgressTrack").transform;
                    trackTransform.SetParent(transform, false);
                }

                _progressTrack = trackTransform.GetComponent<SpriteRenderer>();
                if (_progressTrack == null) _progressTrack = trackTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            var solidSprite = GetSolidSprite();
            _progressBar.sprite = solidSprite;
            _progressBar.color = ProgressFillColor;
            _progressBar.sortingOrder = 13;
            _progressTrack.sprite = solidSprite;
            _progressTrack.color = ProgressTrackColor;
            _progressTrack.sortingOrder = 12;
        }

        private void UpdateProductionProgress(BuildingSnapshot snapshot)
        {
            EnsureProgressVisuals();
            bool supportsProduction = snapshot.MaxWorkers > 0;
            _progressTrack.gameObject.SetActive(supportsProduction);
            _progressBar.gameObject.SetActive(supportsProduction);
            if (!supportsProduction) return;

            float fullWidth = Mathf.Max(0.8f, snapshot.Width * 0.8f);
            float fill = Mathf.Clamp01(snapshot.ProductionProgress);
            float fillWidth = fullWidth * fill;
            float y = -snapshot.Height * 0.5f - 0.24f;

            _progressTrack.transform.localPosition = new Vector3(0f, y, -2.1f);
            _progressTrack.transform.localScale = new Vector3(fullWidth + 0.08f, 0.22f, 1f);
            _progressBar.transform.localPosition = new Vector3(-fullWidth * 0.5f + fillWidth * 0.5f, y, -2.11f);
            _progressBar.transform.localScale = new Vector3(fillWidth, 0.14f, 1f);
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "BuildingProgressSolidTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _solidSprite;
        }

        public void UpdateVisuals(BuildingSnapshot snapshot, bool isTarget)
        {
            _snapshot = snapshot;
            _isTarget = isTarget;

            if (_model != null) _model.Sync(snapshot, isTarget || _isHovered);

            EnsureHighlightVisuals();

            ApplyActiveHighlight();

            UpdateProductionProgress(snapshot);

            if (_label != null)
            {
                _label.sortingOrder = 25;
                _label.transform.localPosition = new Vector3(0f, snapshot.Height * 0.5f + 0.45f, -2.2f);
                if (Camera.main != null) _label.transform.rotation = Camera.main.transform.rotation;
                _label.fontSize = 2.4f;
                _label.color = ColonyPalette.Text;
                _label.alignment = TextAlignmentOptions.Center;
                if (snapshot.Kind == BuildingKind.Mine)
                    _label.text = $"<b>{snapshot.Name}</b>\n<size=80%>Руда: {snapshot.Ore}/{snapshot.MaxOre} | Раб: {snapshot.WorkerCount}/{snapshot.MaxWorkers}</size>";
                else if (snapshot.Kind == BuildingKind.Warehouse)
                    _label.text = $"<b>{snapshot.Name}</b>\n<size=80%>Руда: {snapshot.Ore}/{snapshot.MaxOre}</size>";
                else
                    _label.text = $"<b>{snapshot.Name}</b>";
            }
        }

    }
}
