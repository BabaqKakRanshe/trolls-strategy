using System;
using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.WorldUi;
using Label = UnityEngine.UIElements.Label;

namespace TrollStrategy.Presentation.Units
{
    public class UnitView : MonoBehaviour
    {
        /// <summary>How high the opaque feet stand over the unit's point on the lawn, m (as a battle fighter's).</summary>
        public const float GroundLift = .05f;
        // the carried goods' middle over the head, along the billboard: half the 0.75 m icon and a little air, m
        private const float CargoGap = .45f;
        // a frame without art: the body the battle's fighters assume
        private static readonly Rect DefaultBody = Rect.MinMaxRect(-.15f, -.1f, .15f, .2f);

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _selectionCircle;
        [SerializeField] private SpriteRenderer _cargoIcon;
        [Tooltip("How much the hauler carries, beside the cargo icon.")]
        [SerializeField] private WorldPanel _cargoLabel;
        [SerializeField] private CircleCollider2D _collider;
        private Label _cargoCount;

        [Header("Внешний вид")]
        [SerializeField] private Sprite[] _idleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] _walkFrames = Array.Empty<Sprite>();
        [Tooltip("Бой: замах и удар, 100 мс на кадр.")]
        [SerializeField] private Sprite[] _attackFrames = Array.Empty<Sprite>();
        [Tooltip("Бой: получение урона, 100 мс на кадр.")]
        [SerializeField] private Sprite[] _hurtFrames = Array.Empty<Sprite>();
        [Tooltip("Бой: гибель, 100 мс на кадр; последний кадр остаётся.")]
        [SerializeField] private Sprite[] _deathFrames = Array.Empty<Sprite>();
        [Tooltip("Масштаб только изображения; коллайдер и круг выделения не меняются.")]
        [SerializeField, Min(0.05f)] private float _spriteScale = 1f;

        private UnitSnapshot _snapshot;
        private UnitDefinition _definition;
        private TilemapWorldView _worldView;
        private Vector3 _targetPosition;
        private bool _isWalking;
        private float _animTimer;
        private int _currentFrame;
        private float _movementSpeed;
        private LineRenderer _selectionRing;
        private SpriteRenderer _shadow;
        private bool _isSelected;
        private bool _worksInside;
        private bool _hiddenInside;

        public string UnitId => _snapshot?.Id;
        public UnitSnapshot Snapshot => _snapshot;
        public UnitDefinition Definition => _definition;
        public Sprite IdleSprite => _idleFrames != null && _idleFrames.Length > 0 ? _idleFrames[0] : null;
        public float SpriteScale => _spriteScale > 0f ? _spriteScale : 1f;
        public int IdleFrameCount => _idleFrames?.Length ?? 0;
        public int WalkFrameCount => _walkFrames?.Length ?? 0;
        public IReadOnlyList<Sprite> IdleFrames => _idleFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> WalkFrames => _walkFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> AttackFrames => _attackFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> HurtFrames => _hurtFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> DeathFrames => _deathFrames ?? Array.Empty<Sprite>();
        /// <summary>A worker that reached its workplace door is inside the building and not drawn.</summary>
        public bool IsHiddenInside => _hiddenInside;
        /// <summary>From the opaque feet up the billboard to the sprite's pivot, m at <see cref="SpriteScale"/>.</summary>
        public float PivotAboveFeet => -Body.yMin * SpriteScale;
        private Rect Body => IdleSprite != null ? SpriteBody.Opaque(IdleSprite) : DefaultBody;

        public void SetSpriteScale(float scale)
        {
            _spriteScale = Mathf.Max(0.05f, scale);
            ApplySpriteScale();
        }

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
                if (IdleSprite != null)
                    _spriteRenderer.sprite = IdleSprite;
            }
            EnsureShadow();
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

            float scale = SpriteScale;
            _spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            Stand();
        }

        private void FaceCamera()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                if (_spriteRenderer != null) _spriteRenderer.transform.rotation = camera.transform.rotation;
                if (_selectionCircle != null) _selectionCircle.transform.rotation = camera.transform.rotation;
                if (_cargoIcon != null) _cargoIcon.transform.rotation = camera.transform.rotation;
                if (_cargoLabel != null) _cargoLabel.Face(camera);
            }
            Stand();
        }

        /// <summary>
        /// Stands the creature on the lawn: its opaque feet <see cref="GroundLift"/> over the unit's point (local -Z is
        /// up from the map plane), the body up the billboard, a soft shadow under the feet as wide as the body and the
        /// carried goods over the head. The offsets are local, so a pop-in or shrink of the root keeps the feet down.
        /// </summary>
        private void Stand()
        {
            if (_spriteRenderer == null) return;
            var body = Body;
            float scale = SpriteScale;
            var feet = Vector3.back * GroundLift;
            var up = Quaternion.Inverse(transform.rotation) * _spriteRenderer.transform.up;
            _spriteRenderer.transform.localPosition = feet - up * (body.yMin * scale);
            if (_shadow != null)
            {
                // wider than the feet: from the colony camera the body hides the shadow's far half
                float width = Mathf.Max(.1f, body.width * scale * 1.5f);
                _shadow.transform.SetLocalPositionAndRotation(feet, Quaternion.identity);
                _shadow.transform.localScale = new Vector3(width, width * .55f, 1f);
            }
            if (_cargoIcon != null) _cargoIcon.transform.localPosition = feet + up * (body.height * scale + CargoGap);
        }

        /// <summary>A blob shadow flat on the lawn, drawn under every creature (as under the battle's fighters).</summary>
        private void EnsureShadow()
        {
            if (_shadow != null) return;
            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(transform, false);
            _shadow = shadowObject.AddComponent<SpriteRenderer>();
            _shadow.sprite = FeelSprites.SoftCircle;
            _shadow.color = new Color(0f, 0f, 0f, .5f);
            _shadow.sortingOrder = 18;
        }

        private void ApplyInsideVisibility()
        {
            bool visible = !_hiddenInside;
            if (_spriteRenderer != null) _spriteRenderer.enabled = visible;
            if (_shadow != null) _shadow.enabled = visible;
            if (_collider != null) _collider.enabled = visible;
            if (_selectionRing != null) _selectionRing.enabled = _isSelected && visible;
            if (_selectionCircle != null) _selectionCircle.gameObject.SetActive(_isSelected && visible);
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

        public void UpdateVisuals(UnitSnapshot snapshot, bool isSelected, Sprite cargoSprite = null)
        {
            _snapshot = snapshot;
            _targetPosition = MapPosition(snapshot);
            if (snapshot.MovementSpeed > 0f) _movementSpeed = snapshot.MovementSpeed;
            _isSelected = isSelected;
            _worksInside = snapshot.Assignment != null && snapshot.Assignment.Kind == AssignmentKind.Work;

            EnsureSelectionVisuals();
            EnsureShadow();
            ApplySpriteScale();
            FaceCamera();

            ApplyInsideVisibility();

            if (_spriteRenderer != null)
                _spriteRenderer.color = isSelected ? ColonyPalette.Cream : Color.white;

            if (snapshot.Assignment != null && snapshot.Assignment.Kind == AssignmentKind.Haul && snapshot.Assignment.Carried > 0)
            {
                if (_cargoIcon != null)
                {
                    _cargoIcon.sprite = cargoSprite != null ? cargoSprite : GetFallbackOreSprite();
                    _cargoIcon.sortingOrder = 24;
                    _cargoIcon.transform.localScale = Vector3.one * 0.75f;
                    _cargoIcon.gameObject.SetActive(true);
                }
                if (_cargoLabel != null)
                {
                    _cargoLabel.gameObject.SetActive(true);
                    _cargoCount ??= _cargoLabel.AddLabel("world-label world-label--cargo");
                    _cargoCount.text = $"{snapshot.Assignment.Carried}";
                    _cargoLabel.transform.localPosition = new Vector3(0.48f, -0.2f, -0.05f);
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

        private static Sprite _fallbackOreSprite;

        public static Sprite GetFallbackOreSprite()
        {
            if (_fallbackOreSprite != null) return _fallbackOreSprite;
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[size * size];
            var dark = new Color32(45, 53, 69, 255);
            var mid = new Color32(103, 121, 139, 255);
            var light = new Color32(178, 194, 202, 255);
            for (int y = 2; y < 13; y++)
            for (int x = 2; x < 14; x++)
            {
                if (x + y < 7 || x - y > 8 || y - x > 9 || x + y > 24) continue;
                pixels[y * size + x] = y > 8 || x > 9 ? dark : (x < 7 && y > 5 ? mid : light);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _fallbackOreSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _fallbackOreSprite;
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

            // Hide only once the view has walked to the door, not when the simulation already arrived.
            bool hidden = _worksInside && !_isWalking;
            if (hidden != _hiddenInside)
            {
                _hiddenInside = hidden;
                ApplyInsideVisibility();
            }

            if (_spriteRenderer != null)
            {
                var frames = _isWalking && WalkFrameCount > 0 ? _walkFrames : _idleFrames;
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
