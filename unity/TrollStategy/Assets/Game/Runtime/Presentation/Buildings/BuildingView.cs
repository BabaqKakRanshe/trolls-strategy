using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Content;

namespace TrollStrategy.Presentation.Buildings
{
    public class BuildingView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionHighlight;
        [SerializeField] private SpriteRenderer _progressBar;
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private BoxCollider2D _collider;

        private BuildingSnapshot _snapshot;

        public string BuildingId => _snapshot?.Id;

        public void Setup(BuildingSnapshot snapshot, Sprite sprite, Action<string> onClick)
        {
            _snapshot = snapshot;
            _ = onClick;

            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 10;
                if (sprite != null) _spriteRenderer.sprite = sprite;
            }

            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.size = new Vector2(snapshot.Width, snapshot.Height);
                _collider.offset = Vector2.zero;
            }

            if (_progressBar != null)
            {
                _progressBar.transform.localPosition = new Vector3(0f, -snapshot.Height * 0.5f + 0.15f, 0f);
            }

            if (_label != null)
            {
                _label.transform.localPosition = new Vector3(0f, snapshot.Height * 0.5f + 0.35f, 0f);
            }

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

        private static readonly Color OutlineColor = new(1f, 0.78f, 0.28f, 0.95f);
        private SpriteRenderer[] _outlineRenderers;
        private bool _isHovered;
        private bool _isTarget;

        private void Update()
        {
            if (_collider == null || Mouse.current == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
            bool isInside = _collider.OverlapPoint(new Vector2(mouseWorld.x, mouseWorld.y));
            if (isInside != _isHovered)
            {
                _isHovered = isInside;
                ApplyActiveHighlight();
            }
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
            if (_selectionHighlight != null)
                _selectionHighlight.gameObject.SetActive(false);

            SetOutlineVisible(show);

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

            if (_spriteRenderer == null || _spriteRenderer.sprite == null)
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

        public void UpdateVisuals(BuildingSnapshot snapshot, bool isTarget)
        {
            _snapshot = snapshot;
            _isTarget = isTarget;

            EnsureHighlightVisuals();

            ApplyActiveHighlight();

            if (_progressBar != null)
            {
                if (snapshot.MaxOre > 0)
                {
                    _progressBar.gameObject.SetActive(true);
                    float fill = Mathf.Clamp01((float)snapshot.Ore / snapshot.MaxOre);
                    _progressBar.transform.localScale = new Vector3(fill * snapshot.Width * 0.8f, 0.15f, 1f);
                }
                else
                {
                    _progressBar.gameObject.SetActive(false);
                }
            }

            if (_label != null)
            {
                _label.sortingOrder = 25;
                _label.transform.localPosition = new Vector3(0f, snapshot.Height * 0.5f + 0.45f, 0f);
                _label.fontSize = 2.4f;
                _label.color = new Color(0.965f, 0.93f, 0.79f);
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
