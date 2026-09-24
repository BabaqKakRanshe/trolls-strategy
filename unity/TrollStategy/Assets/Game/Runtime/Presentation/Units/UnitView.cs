using System;
using TMPro;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Units
{
    public class UnitView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionCircle;
        [SerializeField] private SpriteRenderer _cargoIcon;
        [SerializeField] private TextMeshPro _cargoLabel;
        [SerializeField] private CircleCollider2D _collider;

        private UnitSnapshot _snapshot;
        private UnitDefinition _definition;
        private TilemapWorldView _worldView;
        private Vector3 _targetPosition;
        private bool _isWalking;
        private float _animTimer;
        private int _currentFrame;
        private float _movementSpeed;
        private LineRenderer _selectionRing;

        public string UnitId => _snapshot?.Id;
        public UnitSnapshot Snapshot => _snapshot;
        public UnitDefinition Definition => _definition;

        public void Setup(UnitSnapshot snapshot, UnitDefinition definition, Action<string, bool> onClick, TilemapWorldView worldView = null)
        {
            _snapshot = snapshot;
            _definition = definition;
            _worldView = worldView;
            _ = onClick;

            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureDedicatedSpriteRenderer();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 20;
                if (definition != null)
                    _spriteRenderer.sprite = definition.IdleSprite;
            }
            ApplySpriteScale();
            FaceCamera();

            EnsureSelectionVisuals();

            _targetPosition = MapPosition(snapshot);
            transform.position = _targetPosition;

            UpdateVisuals(snapshot, false);
        }

        private void EnsureDedicatedSpriteRenderer()
        {
            if (_spriteRenderer == null || _spriteRenderer.transform != transform) return;

            var source = _spriteRenderer;
            var spriteTransform = transform.Find("SpriteVisual");
            if (spriteTransform == null)
            {
                var spriteObject = new GameObject("SpriteVisual");
                spriteTransform = spriteObject.transform;
                spriteTransform.SetParent(transform, false);
            }

            var dedicated = spriteTransform.GetComponent<SpriteRenderer>();
            if (dedicated == null) dedicated = spriteTransform.gameObject.AddComponent<SpriteRenderer>();

            dedicated.sprite = source.sprite;
            dedicated.color = source.color;
            dedicated.sharedMaterial = source.sharedMaterial;
            dedicated.sortingLayerID = source.sortingLayerID;
            dedicated.sortingOrder = source.sortingOrder;
            dedicated.maskInteraction = source.maskInteraction;
            dedicated.flipX = source.flipX;
            dedicated.flipY = source.flipY;
            source.enabled = false;
            _spriteRenderer = dedicated;
        }

        public void ApplySpriteScale()
        {
            EnsureDedicatedSpriteRenderer();
            if (_spriteRenderer == null) return;

            float scale = _definition != null ? _definition.SpriteScale : 1f;
            _spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            _spriteRenderer.transform.localPosition = new Vector3(0f, 0f, -0.65f);
        }

        private void FaceCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            if (_spriteRenderer != null) _spriteRenderer.transform.rotation = camera.transform.rotation;
            if (_selectionCircle != null) _selectionCircle.transform.rotation = camera.transform.rotation;
            if (_cargoIcon != null) _cargoIcon.transform.rotation = camera.transform.rotation;
            if (_cargoLabel != null) _cargoLabel.transform.rotation = camera.transform.rotation;
        }

        private static Sprite _proceduralSelectionSprite;

        private static Sprite GetSelectionSprite()
        {
            if (_proceduralSelectionSprite != null) return _proceduralSelectionSprite;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float radius = center - 2f;
            float innerRadius = radius - 5f;

            Color transparent = new Color(0, 0, 0, 0);
            Color ringColor = ColonyPalette.Gold;
            Color fillColor = ColonyPalette.WithAlpha(ColonyPalette.Gold, 0.28f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (d <= radius && d >= innerRadius)
                    {
                        tex.SetPixel(x, y, ringColor);
                    }
                    else if (d < innerRadius)
                    {
                        tex.SetPixel(x, y, fillColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }
            tex.Apply();
            _proceduralSelectionSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _proceduralSelectionSprite;
        }

        private void EnsureSelectionVisuals()
        {
            if (_selectionCircle == null)
            {
                var circleGo = new GameObject("SelectionCircle");
                circleGo.transform.SetParent(transform, false);
                circleGo.transform.localPosition = new Vector3(0f, -0.16f, -0.20f);
                circleGo.transform.localScale = new Vector3(1.1f, 0.65f, 1f);

                _selectionCircle = circleGo.AddComponent<SpriteRenderer>();
                _selectionCircle.sprite = GetSelectionSprite();
                _selectionCircle.sortingOrder = 19;
                _selectionCircle.color = Color.white;
            }

            if (_selectionRing == null)
            {
                var ringGo = new GameObject("SelectionRing");
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = new Vector3(0f, -0.16f, -0.21f);

                _selectionRing = ringGo.AddComponent<LineRenderer>();
                _selectionRing.useWorldSpace = false;
                _selectionRing.loop = true;
                _selectionRing.positionCount = 24;
                _selectionRing.startWidth = 0.055f;
                _selectionRing.endWidth = 0.055f;
                _selectionRing.sortingOrder = 22;

                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                if (shader != null) _selectionRing.material = new Material(shader);

                var goldColor = ColonyPalette.Gold;
                _selectionRing.startColor = goldColor;
                _selectionRing.endColor = goldColor;

                float rx = 0.45f;
                float ry = 0.26f;
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI * 2f / 24f;
                    _selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry, 0f));
                }
            }

            if (_collider == null)
            {
                _collider = GetComponent<CircleCollider2D>();
                if (_collider == null)
                {
                    _collider = gameObject.AddComponent<CircleCollider2D>();
                    _collider.radius = 0.4f;
                }
            }
        }

        public void UpdateVisuals(UnitSnapshot snapshot, bool isSelected)
        {
            _snapshot = snapshot;
            _targetPosition = MapPosition(snapshot);
            if (snapshot.MovementSpeed > 0f) _movementSpeed = snapshot.MovementSpeed;

            EnsureSelectionVisuals();
            ApplySpriteScale();
            FaceCamera();

            if (_selectionRing != null)
                _selectionRing.enabled = isSelected;

            if (_selectionCircle != null)
                _selectionCircle.gameObject.SetActive(isSelected);

            if (_spriteRenderer != null)
                _spriteRenderer.color = isSelected ? ColonyPalette.Cream : Color.white;

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
                if (_cargoLabel != null)
                    _cargoLabel.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            AdvanceVisual(Time.deltaTime);
        }

        private Vector3 MapPosition(UnitSnapshot snapshot)
        {
            var mapPosition = new Vector3(snapshot.Position.X, snapshot.Position.Y, 0f);
            return _worldView != null ? _worldView.MapToWorld(mapPosition) : mapPosition;
        }

        public void AdvanceVisual(float deltaSeconds)
        {
            float dist = Vector3.Distance(transform.position, _targetPosition);
            _isWalking = dist > 0.05f;

            if (_isWalking)
            {
                float dx = _targetPosition.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.01f && _spriteRenderer != null)
                    _spriteRenderer.flipX = dx < 0f;

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    _targetPosition,
                    _movementSpeed * Mathf.Max(0f, deltaSeconds));
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
                    _animTimer += Mathf.Max(0f, deltaSeconds);
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

    }
}
