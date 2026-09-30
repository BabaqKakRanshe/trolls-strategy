using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.WorldUi;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>
    /// One fighter on the battle board: the unit's pixel-art sprite facing the battle camera, a blob shadow
    /// and team ring on the ground, an HP bar over the head. Poses (idle, walk, attack, hurt, death) and
    /// offsets (hop, lunge, knockback) run on the board's replay clock, so pause and speed apply to them.
    /// Never decides gameplay: the board tells it what the battle report says happened.
    /// </summary>
    public sealed class BattleFighterView : MonoBehaviour
    {
        /// <summary>Battle-only size: a 9 px troll stands about 0.96 m, a 6 px goblin about 0.64 m.</summary>
        public const float PixelSize = .17f / 1.6f;
        private const float GroundLift = .05f;
        private const float BarHeight = .13f / 1.6f;
        private const float IdleFrame = .2f;
        private const float WalkFrame = .1f;
        private const float ActionFrame = .1f;
        private const float HurtFrame = .08f;

        private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
        private static readonly Color PlayerRing = new(.62f, .86f, 1f, .95f);
        private static readonly Color EnemyRing = new(1f, .36f, .3f, .9f);
        private static readonly Color PlayerFill = new(.4f, .86f, .42f, 1f);
        private static readonly Color EnemyFill = new(.93f, .33f, .29f, 1f);

        private enum Pose { Idle, Walk, Attack, Hurt, Dead }

        private Camera _camera;
        private SpriteRenderer _sprite;
        private SpriteRenderer _shadow;
        private SpriteRenderer _ring;
        private WorldPanel _bar;
        private VisualElement _barFill;
        private VisualElement _barChip;
        private Label _barLabel;
        private Transform _hitbox;
        private CapsuleCollider _collider;
        private BattleCellView _cellView;
        private MaterialPropertyBlock _block;

        private IReadOnlyList<Sprite> _idle;
        private IReadOnlyList<Sprite> _walk;
        private IReadOnlyList<Sprite> _attack;
        private IReadOnlyList<Sprite> _hurt;
        private IReadOnlyList<Sprite> _death;
        private float _spriteScale;
        private float _feet;
        private float _height;

        private Pose _pose;
        private float _poseTime;
        private float _idleOffset;
        private bool _faceLeft;
        private bool _enemy;
        private int _maxHp;
        private int _hp;
        private float _chipHp;
        private float _chipDelay;
        private float _barPunch;
        private bool _selected;

        private Vector3 _ground;
        private Vector3 _moveFrom;
        private float _moveTime;
        private float _moveDuration;
        private Vector3 _lungeDirection;
        private bool _melee;
        private Vector3 _knockDirection;
        private float _knockTime = 10f;
        private float _freeze;
        private float _flash;
        private float _deadTime;
        private float _spawnTime = 10f;
        private float _removeTime = -1f;

        public string Id { get; private set; }
        public UnitKind Kind { get; private set; }
        public bool IsEnemy => _enemy;
        public bool IsDead => _pose == Pose.Dead;
        public Cell Cell { get; private set; }
        public int Hp => _hp;
        public int MaxHp => _maxHp;
        /// <summary>Where the fighter stands logically; moves tween towards it.</summary>
        public Vector3 Ground => _ground;
        /// <summary>A point in the upper body, for sparks, projectiles and numbers (along the billboard, like the sprite).</summary>
        public Vector3 Chest => transform.position + Vector3.up * GroundLift + BodyUp * (_height * .6f);
        public Vector3 AboveHead => transform.position + Vector3.up * GroundLift + BodyUp * (_height + .45f / 1.6f);
        private Vector3 BodyUp => _camera != null ? _camera.transform.up : Vector3.up;

        public void Init(string id, UnitKind kind, bool enemy, Cell cell, Vector3 ground, UnitView visuals,
            int maxHp, Camera camera)
        {
            Id = id;
            Kind = kind;
            _enemy = enemy;
            _camera = camera;
            _maxHp = Mathf.Max(1, maxHp);
            _hp = _maxHp;
            _chipHp = _maxHp;
            _faceLeft = enemy;
            _idle = visuals != null ? visuals.IdleFrames : null;
            _walk = visuals != null ? visuals.WalkFrames : null;
            _attack = visuals != null ? visuals.AttackFrames : null;
            _hurt = visuals != null ? visuals.HurtFrames : null;
            _death = visuals != null ? visuals.DeathFrames : null;
            _spriteScale = 32f * PixelSize;
            _idleOffset = Mathf.Abs(id != null ? id.GetHashCode() % 7 : 0) * .13f;
            _block = new MaterialPropertyBlock();
            MeasureBody();
            Build();
            SetCell(cell, ground, 0f);
            RefreshBar();
            ApplyVisuals();
            Billboard();
        }

        /// <summary>Walks (or snaps, with duration 0) to a cell.</summary>
        public void SetCell(Cell cell, Vector3 ground, float duration)
        {
            Cell = cell;
            if (_cellView != null) _cellView.Init(cell);
            if (duration <= 0f)
            {
                _ground = ground;
                _moveDuration = 0f;
                transform.position = ground;
                return;
            }
            float dx = ground.x - _ground.x;
            if (Mathf.Abs(dx) > .01f) _faceLeft = dx < 0f;
            _moveFrom = transform.position;
            _ground = ground;
            _moveTime = 0f;
            _moveDuration = duration;
            if (_pose == Pose.Idle || _pose == Pose.Walk) SetPose(Pose.Walk);
        }

        /// <summary>Starts a swing (melee) or throw at a target; returns the delay until the blow lands.</summary>
        public float BeginAttack(Vector3 target, bool melee)
        {
            if (IsDead) return 0f;
            float dx = target.x - _ground.x;
            if (Mathf.Abs(dx) > .01f) _faceLeft = dx < 0f;
            var flat = target - _ground;
            flat.y = 0f;
            _lungeDirection = flat.sqrMagnitude > .0001f ? flat.normalized : Vector3.right;
            _melee = melee;
            SetPose(Pose.Attack);
            // release on the third frame of the swing
            return melee ? .22f : .2f;
        }

        /// <summary>Shows a landed blow: flash, knockback, hurt pose, HP bar; the pose holds for <paramref name="freeze"/> s.</summary>
        public void ReceiveHit(int damage, int hpAfter, Vector3 from, float freeze)
        {
            if (IsDead) return;
            _hp = Mathf.Clamp(hpAfter, 0, _maxHp);
            _chipDelay = .35f;
            _barPunch = 1f;
            _flash = 1f;
            _freeze = Mathf.Max(_freeze, freeze);
            var away = _ground - from;
            away.y = 0f;
            _knockDirection = away.sqrMagnitude > .0001f ? away.normalized : Vector3.zero;
            _knockTime = 0f;
            if (_pose != Pose.Attack) SetPose(Pose.Hurt);
            RefreshBar();
        }

        public void Die()
        {
            if (IsDead) return;
            _hp = 0;
            _flash = 1f;
            SetPose(Pose.Dead);
            _deadTime = 0f;
            if (_collider != null) _collider.enabled = false;
            RefreshBar();
        }

        /// <summary>Selected in the roster: the ring glows gold and breathes.</summary>
        public void SetSelected(bool selected) => _selected = selected;

        /// <summary>Holds the current pose for a beat (the attacker's side of a hit-stop).</summary>
        public void Hold(float seconds) => _freeze = Mathf.Max(_freeze, seconds);

        /// <summary>Full health from the battle report (equipment never changes it, but the report is the source).</summary>
        public void ResetHealth(int maxHp)
        {
            _maxHp = Mathf.Max(1, maxHp);
            _hp = _maxHp;
            _chipHp = _maxHp;
            RefreshBar();
        }

        public void PopIn(float delay = 0f)
        {
            _spawnTime = -delay;
            // hidden from this frame on, not after one full-size frame
            ApplyVisuals();
            Billboard();
        }

        /// <summary>Shrinks away, then destroys itself.</summary>
        public void Remove()
        {
            if (_removeTime >= 0f) return;
            _removeTime = 0f;
            if (_collider != null) _collider.enabled = false;
        }

        public void Advance(float dt)
        {
            if (_removeTime >= 0f)
            {
                _removeTime += dt;
                if (_removeTime >= .18f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            _spawnTime += dt;
            _flash = _freeze > 0f ? _flash : Mathf.MoveTowards(_flash, 0f, dt / .12f);
            if (_freeze > 0f)
            {
                // local hit-stop: the pose holds for a beat so the blow reads
                _freeze -= dt;
                ApplyVisuals();
                return;
            }

            _poseTime += dt;
            _knockTime += dt;
            _barPunch = Mathf.MoveTowards(_barPunch, 0f, dt / .25f);
            if (_chipDelay > 0f) _chipDelay -= dt;
            else _chipHp = Mathf.MoveTowards(_chipHp, _hp, dt * _maxHp * 1.1f);

            if (_moveDuration > 0f)
            {
                _moveTime += dt;
                if (_moveTime >= _moveDuration)
                {
                    _moveDuration = 0f;
                    if (_pose == Pose.Walk) SetPose(Pose.Idle);
                }
            }

            switch (_pose)
            {
                case Pose.Attack when _poseTime >= Frames(_attack, 4) * ActionFrame + .06f:
                case Pose.Hurt when _poseTime >= Frames(_hurt, 3) * HurtFrame:
                    SetPose(_moveDuration > 0f ? Pose.Walk : Pose.Idle);
                    break;
                case Pose.Dead:
                    _deadTime += dt;
                    break;
            }
            ApplyVisuals();
        }

        private void LateUpdate() => Billboard();

        private void Billboard()
        {
            if (_camera == null || _sprite == null) return;
            var facing = _camera.transform.rotation;
            _sprite.transform.rotation = facing;
            _bar.transform.rotation = facing;
            // feet stay on the cell while the sprite pops, squashes or shrinks: scale the pivot offset with it
            float scale = _spriteScale > 0f ? _sprite.transform.localScale.y / _spriteScale : 1f;
            _sprite.transform.position = transform.position + Vector3.up * GroundLift + BodyUp * (_feet * scale);
            // the bar's document hangs from its bottom edge: the bar itself stays centred where it always was
            _bar.transform.position = AboveHead - BodyUp * (BarHeight * .5f + .03f);
            // the hitbox leans with the billboard, so a click on the head still means this fighter
            _hitbox.rotation = Quaternion.FromToRotation(Vector3.up, BodyUp);
            _hitbox.position = transform.position + Vector3.up * GroundLift;
        }

        private void SetPose(Pose pose)
        {
            if (_pose == Pose.Dead) return;
            _pose = pose;
            _poseTime = 0f;
        }

        private void ApplyVisuals()
        {
            // position: tween between cells with a hop, plus lunge and knockback
            var position = _ground;
            if (_moveDuration > 0f)
            {
                float t = Mathf.Clamp01(_moveTime / _moveDuration);
                position = Vector3.Lerp(_moveFrom, _ground, Ease.OutCubic(t)) + Vector3.up * (Ease.Hump(t) * .16f);
            }
            if (_pose == Pose.Attack) position += _lungeDirection * LungeOffset(_poseTime, _melee);
            if (_knockTime < .3f)
            {
                float t = _knockTime;
                float knock = t < .05f ? t / .05f : Mathf.Pow(1f - Mathf.Clamp01((t - .05f) / .25f), 2f);
                position += _knockDirection * (.24f * knock);
            }
            transform.position = position;

            _sprite.sprite = CurrentFrame();
            _sprite.flipX = _faceLeft;
            float squash = _knockTime < .3f ? Ease.Spring(_knockTime / .3f, 1.5f) * .16f : 0f;
            float spawn = _spawnTime < 0f ? 0f : _spawnTime < .3f ? Ease.OutBack(_spawnTime / .3f, 2.4f) : 1f;
            float remove = _removeTime >= 0f ? 1f - Ease.InCubic(_removeTime / .18f) : 1f;
            float size = _spriteScale * spawn * remove;
            _sprite.transform.localScale = new Vector3(size * (1f + squash), size * (1f - squash), 1f);

            var tint = Color.white;
            if (_pose == Pose.Dead)
            {
                float fade = Mathf.Clamp01((_deadTime - 1.3f) / .6f);
                tint = Color.Lerp(Color.white, new Color(.55f, .55f, .55f, .45f), fade);
            }
            _sprite.color = tint;
            _sprite.GetPropertyBlock(_block);
            _block.SetFloat(FlashAmount, _flash);
            _block.SetColor(FlashColor, Color.white);
            _sprite.SetPropertyBlock(_block);

            float groundScale = spawn * remove * (_pose == Pose.Dead ? 1f - Mathf.Clamp01(_deadTime / .5f) : 1f);
            _shadow.transform.localScale = new Vector3(1.05f, .7f, 1f) * groundScale;
            float breathe = _selected ? 1f + Mathf.Sin(_poseTime * 7f + _idleOffset) * .06f : 1f;
            _ring.transform.localScale = Vector3.one * (1.25f * groundScale * breathe);
            var ring = _selected ? new Color(1f, .84f, .35f, 1f) : _enemy ? EnemyRing : PlayerRing;
            if (_pose == Pose.Dead) ring.a *= 1f - Mathf.Clamp01(_deadTime / .4f);
            _ring.color = ring;

            float barScale = spawn * remove * (_pose == Pose.Dead ? 1f - Mathf.Clamp01(_deadTime / .25f) : 1f);
            _bar.transform.localScale = Vector3.one * (barScale * (1f + _barPunch * .18f));
            SetBarSegment(_barChip, _chipHp / _maxHp);
            SetBarSegment(_barFill, (float)_hp / _maxHp);
            _barFill.style.backgroundColor = Color.Lerp(Color.white, _enemy ? EnemyFill : PlayerFill, 1f - _barPunch * .7f);
        }

        private static float LungeOffset(float t, bool melee)
        {
            float reach = melee ? .5f : .16f;
            if (t < .18f) return -.12f * Ease.OutCubic(t / .18f);
            if (t < .28f) return Mathf.Lerp(-.12f, reach, Ease.OutCubic((t - .18f) / .1f));
            if (t < .55f) return Mathf.Lerp(reach, 0f, Ease.OutCubic((t - .28f) / .27f));
            return 0f;
        }

        private Sprite CurrentFrame()
        {
            switch (_pose)
            {
                case Pose.Attack when Has(_attack):
                    return _attack[Mathf.Min(_attack.Count - 1, (int)(_poseTime / ActionFrame))];
                case Pose.Hurt when Has(_hurt):
                    return _hurt[Mathf.Min(_hurt.Count - 1, (int)(_poseTime / HurtFrame))];
                case Pose.Dead when Has(_death):
                    return _death[Mathf.Min(_death.Count - 1, (int)(_poseTime / ActionFrame))];
                case Pose.Walk when Has(_walk):
                    return _walk[(int)(_poseTime / WalkFrame) % _walk.Count];
            }
            if (!Has(_idle)) return _sprite.sprite;
            return _idle[(int)((_poseTime + _idleOffset) / IdleFrame) % _idle.Count];
        }

        private static bool Has(IReadOnlyList<Sprite> frames) => frames != null && frames.Count > 0;

        private static int Frames(IReadOnlyList<Sprite> frames, int fallback) =>
            Has(frames) ? frames.Count : fallback;

        /// <summary>Opaque feet and head height; the tight mesh still includes transparent extrusion around tiny sprites.</summary>
        private void MeasureBody()
        {
            float min = -.1f, max = .2f;
            if (Has(_idle))
            {
                var body = SpriteBody.Opaque(_idle[0]);
                min = body.yMin;
                max = body.yMax;
            }
            _feet = -min * _spriteScale;
            _height = Mathf.Max(.4f, (max - min) * _spriteScale);
        }

        private void Build()
        {
            _sprite = Child<SpriteRenderer>("Sprite");
            _sprite.sharedMaterial = FeelSprites.SpriteFlash;
            _sprite.sortingOrder = 10;

            _shadow = Child<SpriteRenderer>("Shadow");
            _shadow.sprite = FeelSprites.SoftCircle;
            _shadow.color = new Color(0f, 0f, 0f, .45f);
            _shadow.sortingOrder = 4;
            _shadow.transform.SetLocalPositionAndRotation(Vector3.up * GroundLift, Quaternion.Euler(90f, 0f, 0f));

            _ring = Child<SpriteRenderer>("TeamRing");
            _ring.sprite = FeelSprites.Ring;
            _ring.sortingOrder = 5;
            _ring.transform.SetLocalPositionAndRotation(Vector3.up * (GroundLift + .005f), Quaternion.Euler(90f, 0f, 0f));

            // HP over the head: the number, then the bar with its lagging chip (sizes and colours in WorldUi.uss)
            _bar = WorldPanel.Create("HpBar", transform, 33, Pivot.BottomCenter, "hp-bar");
            _barLabel = _bar.AddLabel("world-label hp-bar__label");
            var track = BarPart(_bar.Content, "hp-bar__track");
            var inside = BarPart(track, "hp-bar__inside");
            _barChip = BarPart(inside, "hp-bar__chip");
            _barFill = BarPart(inside, "hp-bar__fill");

            _hitbox = new GameObject("Hitbox").transform;
            _hitbox.SetParent(transform, false);
            _collider = _hitbox.gameObject.AddComponent<CapsuleCollider>();
            _collider.direction = 1;
            _collider.radius = .38f;
            _collider.height = Mathf.Max(.8f, _height + .2f);
            _collider.center = new Vector3(0f, _collider.height * .5f, 0f);
            _cellView = _hitbox.gameObject.AddComponent<BattleCellView>();
        }

        private static VisualElement BarPart(VisualElement parent, string className)
        {
            var part = new VisualElement { pickingMode = PickingMode.Ignore };
            part.AddToClassList(className);
            parent.Add(part);
            return part;
        }

        // segments grow from the left edge of the bar
        private static void SetBarSegment(VisualElement segment, float fraction) =>
            segment.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);

        private void RefreshBar()
        {
            if (_barLabel != null) _barLabel.text = _hp > 0 ? $"{_hp}/{_maxHp}" : string.Empty;
        }

        private T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(T));
            go.transform.SetParent(transform, false);
            return go.GetComponent<T>();
        }
    }
}
