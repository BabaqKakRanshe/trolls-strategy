using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using TrollStrategy.Application;
using TrollStrategy.Content;

namespace TrollStrategy.Presentation.Buildings
{
    public class BuildingView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionHighlight;
        [SerializeField] private SpriteRenderer _progressBar;
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private BoxCollider2D _collider;

        private BuildingSnapshot _snapshot;
        private Action<string> _onClick;

        public string BuildingId => _snapshot?.Id;

        public void Setup(BuildingSnapshot snapshot, Sprite sprite, Action<string> onClick)
        {
            _snapshot = snapshot;
            _onClick = onClick;

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

        public void UpdateVisuals(BuildingSnapshot snapshot, bool isTarget)
        {
            _snapshot = snapshot;

            if (_selectionHighlight != null)
            {
                _selectionHighlight.gameObject.SetActive(isTarget);
                if (isTarget)
                {
                    _selectionHighlight.transform.localScale = new Vector3(snapshot.Width + 0.2f, snapshot.Height + 0.2f, 1f);
                    _selectionHighlight.color = new Color(0.4f, 0.9f, 1f, 0.5f);
                }
            }

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
                if (snapshot.Kind == BuildingKind.Mine)
                    _label.text = $"{snapshot.Name}\nРуда: {snapshot.Ore}/{snapshot.MaxOre}\nРаб: {snapshot.WorkerCount}/{snapshot.MaxWorkers}";
                else if (snapshot.Kind == BuildingKind.Warehouse)
                    _label.text = $"{snapshot.Name}\nРуда: {snapshot.Ore}/{snapshot.MaxOre}";
                else
                    _label.text = $"{snapshot.Name}";
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_snapshot != null)
                _onClick?.Invoke(_snapshot.Id);
        }

        private void OnMouseDown()
        {
            if (_snapshot != null)
                _onClick?.Invoke(_snapshot.Id);
        }
    }
}
