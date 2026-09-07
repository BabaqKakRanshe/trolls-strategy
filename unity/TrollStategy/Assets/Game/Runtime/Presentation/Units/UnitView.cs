using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Presentation.Units
{
    public class UnitView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionCircle;
        [SerializeField] private SpriteRenderer _cargoIcon;
        [SerializeField] private TextMeshPro _cargoLabel;
        [SerializeField] private CircleCollider2D _collider;

        private UnitSnapshot _snapshot;
        private UnitDefinition _definition;
        private Action<string, bool> _onClick;
        private Vector3 _targetPosition;
        private bool _isWalking;
        private float _animTimer;
        private int _currentFrame;

        public string UnitId => _snapshot?.Id;

        public void Setup(UnitSnapshot snapshot, UnitDefinition definition, Action<string, bool> onClick)
        {
            _snapshot = snapshot;
            _definition = definition;
            _onClick = onClick;

            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 20;
                if (definition != null)
                    _spriteRenderer.sprite = definition.IdleSprite;
            }

            _targetPosition = new Vector3(snapshot.Position.X, snapshot.Position.Y, 0f);
            transform.position = _targetPosition;

            UpdateVisuals(snapshot, false);
        }

        public void UpdateVisuals(UnitSnapshot snapshot, bool isSelected)
        {
            _snapshot = snapshot;
            _targetPosition = new Vector3(snapshot.Position.X, snapshot.Position.Y, 0f);

            if (_selectionCircle != null)
                _selectionCircle.gameObject.SetActive(isSelected);

            if (snapshot.Assignment != null && snapshot.Assignment.Kind == AssignmentKind.Haul && snapshot.Assignment.Carried > 0)
            {
                if (_cargoIcon != null) _cargoIcon.gameObject.SetActive(true);
                if (_cargoLabel != null)
                {
                    _cargoLabel.gameObject.SetActive(true);
                    _cargoLabel.text = $"{snapshot.Assignment.Carried}";
                }
            }
            else
            {
                if (_cargoIcon != null) _cargoIcon.gameObject.SetActive(false);
                if (_cargoLabel != null) _cargoLabel.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float dist = Vector3.Distance(transform.position, _targetPosition);
            _isWalking = dist > 0.05f;

            if (_isWalking)
            {
                float dx = _targetPosition.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.01f && _spriteRenderer != null)
                    _spriteRenderer.flipX = dx < 0f;

                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, 5.5f * Time.deltaTime);
            }
            else
            {
                transform.position = _targetPosition;
            }

            if (_definition != null && _spriteRenderer != null)
            {
                var frames = _isWalking ? _definition.WalkFrames : _definition.IdleFrames;
                if (frames != null && frames.Length > 0)
                {
                    _animTimer += Time.deltaTime;
                    if (_animTimer >= 0.2f)
                    {
                        _animTimer -= 0.2f;
                        _currentFrame = (_currentFrame + 1) % frames.Length;
                    }
                    if (_currentFrame >= frames.Length) _currentFrame = 0;
                    _spriteRenderer.sprite = frames[_currentFrame];
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            HandleClick();
        }

        private void OnMouseDown()
        {
            HandleClick();
        }

        private void HandleClick()
        {
            if (_snapshot != null)
            {
                bool additive = UnityEngine.InputSystem.Keyboard.current != null &&
                                (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed ||
                                 UnityEngine.InputSystem.Keyboard.current.rightShiftKey.isPressed);
                _onClick?.Invoke(_snapshot.Id, additive);
            }
        }
    }
}
