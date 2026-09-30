using System.Collections.Generic;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.WorldUi;
using UnityEngine;
using UnityEngine.Rendering;
using Label = UnityEngine.UIElements.Label;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>
    /// Short-lived battle feedback on the board's replay clock: floating damage numbers, hit sparks, thrown
    /// projectiles and cell pings, plus — when the arena brings effect meshes (<see cref="BattleArenaSet"/>) —
    /// landing dust, flying debris and death clouds. Pausing or speeding up the replay pauses or speeds them up
    /// too. Sprites and meshes are pooled; meshes vanish by scale, never by alpha.
    /// </summary>
    public sealed class BattleEffects : MonoBehaviour
    {
        /// <summary>Most effect meshes alive at once; a burst beyond it is trimmed, never queued.</summary>
        public const int MaxLiveMeshes = 90;
        public const float Gravity = 9f;

        private const int SortText = 60;
        // a world font size of 1 used to be 10 panel pixels: the numbers keep their old sizes
        private const float FontPixels = 10f;
        private const int SortSpark = 50;

        private readonly List<Effect> _active = new();
        private readonly Stack<WorldPanel> _textPool = new();
        private readonly Stack<SpriteRenderer> _spritePool = new();
        private readonly Dictionary<GameObject, Stack<Transform>> _meshPool = new();
        // presentation only, but still repeatable: the same replay kicks up the same dust
        private readonly System.Random _random = new(4211);
        private Camera _camera;
        private BattleArenaSet _arena;
        private int _liveMeshes;

        public void Init(Camera camera) => _camera = camera;

        /// <summary>Effect meshes come from this arena; without one only the flat effects play.</summary>
        public void UseArena(BattleArenaSet arena) => _arena = arena;

        public int Count => _active.Count;
        public int LiveMeshes => _liveMeshes;

        /// <summary>A number that pops above a fighter and drifts up while fading.</summary>
        public void Number(Vector3 position, string text, Color color, float size, float lateral)
        {
            var panel = TakeText();
            var label = (Label)panel.Content[0];
            label.text = text;
            label.style.color = color;
            label.style.fontSize = size * FontPixels;
            panel.transform.position = position;
            _active.Add(new FloatingText(panel, label, color, position, lateral, this));
        }

        /// <summary>Star burst with a few glowing shards at the point of impact.</summary>
        public void Spark(Vector3 position, Color color, float size)
        {
            var star = TakeSprite(FeelSprites.Spark, color, SortSpark + 1);
            star.transform.position = position;
            var right = _camera != null ? _camera.transform.right : Vector3.right;
            var depth = _camera != null ? _camera.transform.forward : Vector3.forward;
            var shard = Source(ArenaFx.Spark);
            if (shard != null)
            {
                _active.Add(new Burst(this, star, System.Array.Empty<SpriteRenderer>(), null, size));
                for (int i = 0; i < 5; i++)
                {
                    float angle = (i * 72f + Range(-18f, 18f)) * Mathf.Deg2Rad;
                    var velocity = (right * Mathf.Cos(angle) + Vector3.up * (Mathf.Sin(angle) + .9f) +
                                    depth * Range(-.4f, .4f)) * (2.2f + i * .25f);
                    _active.Add(Piece.Shard(this, shard, position, velocity, Range(.3f, .42f), size * Range(.8f, 1.15f)));
                }
                return;
            }

            var shards = new SpriteRenderer[5];
            var velocities = new Vector3[5];
            for (int i = 0; i < shards.Length; i++)
            {
                shards[i] = TakeSprite(FeelSprites.White, color, SortSpark);
                shards[i].transform.position = position;
                float angle = (i * 72f + size * 37f) * Mathf.Deg2Rad;
                velocities[i] = (right * Mathf.Cos(angle) + Vector3.up * (Mathf.Sin(angle) + .9f)) * (2.2f + i * .25f);
            }
            _active.Add(new Burst(this, star, shards, velocities, size));
        }

        /// <summary>
        /// A thrown rock that arcs from <paramref name="from"/> to <paramref name="to"/>: the arena's rock mesh
        /// tumbling at 540-720°/s, or <paramref name="sprite"/> without an arena.
        /// </summary>
        public void Projectile(Vector3 from, Vector3 to, float duration, Sprite sprite, float size)
        {
            var rock = Source(ArenaFx.Rock);
            if (rock != null)
            {
                // the projectile itself is never trimmed: the blow it carries must be seen
                var mesh = TakeMesh(rock, true);
                _active.Add(new Flight(this, null, rock, mesh, from, to, duration, RandomAxis(),
                    Range(540f, 720f)));
                return;
            }
            var flat = TakeSprite(sprite != null ? sprite : FeelSprites.SoftCircle, Color.white, SortSpark);
            flat.transform.localScale = Vector3.one * size;
            _active.Add(new Flight(this, flat, null, null, from, to, duration, Vector3.forward, 900f));
        }

        /// <summary>Flat expanding ring on the ground: where something landed or was refused.</summary>
        public void GroundPing(Vector3 position, Color color, float radius, float duration = .35f)
        {
            var ring = TakeSprite(FeelSprites.Ring, color, 6);
            ring.transform.SetPositionAndRotation(position + Vector3.up * .05f, Quaternion.Euler(90f, 0f, 0f));
            _active.Add(new Ping(this, ring, radius, duration));
        }

        /// <summary>Dust kicked up where a fighter lands: 3-5 puffs spreading about 0.3 m from the feet.</summary>
        public void Landing(Vector3 ground, float delay = 0f) =>
            Puffs(ground, 3, 5, .6f, .9f, .3f, .08f, .35f, .45f, delay);

        /// <summary>One or two puffs under a heavy blow.</summary>
        public void HitDust(Vector3 ground) => Puffs(ground, 1, 2, .7f, 1f, .35f, .12f, .38f, .5f, 0f);

        /// <summary>A cloud of 4-6 large puffs where a fighter falls.</summary>
        public void DeathCloud(Vector3 ground, float delay = 0f) =>
            Puffs(ground, 4, 6, 1f, 1.3f, .45f, .25f, .5f, .68f, delay);

        /// <summary>
        /// Chunks thrown up by a blow: 2-3.5 m/s upwards and away from the attacker, falling with g = 9 onto
        /// <paramref name="floorY"/>, tumbling, shrinking away over the last 30% of their life.
        /// </summary>
        public void Debris(Vector3 origin, float floorY, Vector3 away, ArenaFx role, int min, int max)
        {
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, away);
            int count = RangeInt(min, max);
            for (int i = 0; i < count; i++)
            {
                var source = Source(role);
                if (source == null) return;
                float up = Range(2f, 3.5f);
                var velocity = Vector3.up * up + away * Range(.5f, 1.4f) + side * Range(-1f, 1f);
                float height = Mathf.Max(0f, origin.y - floorY);
                float flight = (up + Mathf.Sqrt(up * up + 2f * Gravity * height)) / Gravity;
                var start = origin + new Vector3(Range(-.08f, .08f), 0f, Range(-.08f, .08f));
                // the kit's chunks are 10-17 cm; at battle distance they need a little more to read
                _active.Add(Piece.Chunk(this, source, start, velocity, floorY + .04f, flight + .35f,
                    Range(1.2f, 1.7f), RandomAxis(), Range(360f, 900f)));
            }
        }

        /// <summary>Advances every effect by replay time; finished ones are recycled.</summary>
        public void Advance(float dt)
        {
            var facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Tick(dt, facing)) continue;
                _active[i].Release();
                _active.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            foreach (var effect in _active) effect.Release();
            _active.Clear();
        }

        private void Puffs(Vector3 ground, int min, int max, float minScale, float maxScale, float spread, float rise,
            float minLife, float maxLife, float delay)
        {
            int count = RangeInt(min, max);
            float turn = Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                var source = Source(ArenaFx.Dust);
                if (source == null) return;
                var direction = Quaternion.Euler(0f, turn + i * 360f / count + Range(-20f, 20f), 0f) * Vector3.forward;
                var start = ground + direction * (.12f * spread / .3f) + Vector3.up * .02f;
                var drift = direction * (spread * Range(.8f, 1.15f)) + Vector3.up * (rise * Range(.6f, 1.2f));
                _active.Add(Piece.Puff(this, source, start, drift, Range(minLife, maxLife), Range(minScale, maxScale),
                    delay, Range(-70f, 70f)));
            }
        }

        private GameObject Source(ArenaFx role) => _arena != null ? _arena.Effect(role, _random.Next()) : null;

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private int RangeInt(int min, int maxInclusive) => _random.Next(min, maxInclusive + 1);

        private Vector3 RandomAxis()
        {
            var axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
            return axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.up;
        }

        private Transform TakeMesh(GameObject source, bool always = false)
        {
            if (source == null || (!always && _liveMeshes >= MaxLiveMeshes)) return null;
            if (!_meshPool.TryGetValue(source, out var pool)) _meshPool[source] = pool = new Stack<Transform>();
            Transform mesh = null;
            while (pool.Count > 0 && mesh == null) mesh = pool.Pop();
            if (mesh == null)
            {
                mesh = Instantiate(source, transform).transform;
                mesh.name = source.name;
                // specks casting sun shadows cost more than they add
                foreach (var renderer in mesh.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            mesh.localScale = Vector3.zero;
            mesh.gameObject.SetActive(true);
            _liveMeshes++;
            return mesh;
        }

        private void ReturnMesh(GameObject source, Transform mesh)
        {
            if (mesh == null) return;
            _liveMeshes--;
            mesh.gameObject.SetActive(false);
            if (source != null && _meshPool.TryGetValue(source, out var pool)) pool.Push(mesh);
        }

        private SpriteRenderer TakeSprite(Sprite sprite, Color color, int order)
        {
            SpriteRenderer renderer = null;
            while (_spritePool.Count > 0 && renderer == null) renderer = _spritePool.Pop();
            if (renderer == null)
            {
                var go = new GameObject("EffectSprite", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                renderer = go.GetComponent<SpriteRenderer>();
            }
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.localScale = Vector3.one;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private void ReturnSprite(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.gameObject.SetActive(false);
            _spritePool.Push(renderer);
        }

        private WorldPanel TakeText()
        {
            WorldPanel panel = null;
            while (_textPool.Count > 0 && panel == null) panel = _textPool.Pop();
            if (panel == null)
            {
                panel = WorldPanel.Create("DamageNumber", transform, SortText);
                panel.KeepReadable = false;     // the battle camera does not zoom; the number scales itself
                panel.AddLabel("world-label world-label--damage");
            }
            panel.gameObject.SetActive(true);
            return panel;
        }

        private void ReturnText(WorldPanel panel)
        {
            if (panel == null) return;
            panel.gameObject.SetActive(false);
            _textPool.Push(panel);
        }

        private abstract class Effect
        {
            protected float Age;
            public abstract bool Tick(float dt, Quaternion facing);
            public abstract void Release();
        }

        private sealed class FloatingText : Effect
        {
            private const float Life = .95f;
            private readonly WorldPanel _panel;
            private readonly Label _label;
            private readonly Vector3 _origin;
            private readonly float _lateral;
            private readonly BattleEffects _owner;
            private readonly Color _color;

            public FloatingText(WorldPanel panel, Label label, Color color, Vector3 origin, float lateral, BattleEffects owner)
            {
                _panel = panel;
                _label = label;
                _origin = origin;
                _lateral = lateral;
                _owner = owner;
                _color = color;
            }

            public override bool Tick(float dt, Quaternion facing)
            {
                Age += dt;
                if (_panel == null) return false;
                float t = Age / Life;
                // pop past full size, settle, then rise and fade
                float scale = t < .18f ? Ease.OutBack(t / .18f, 3f) : 1f;
                float rise = Ease.OutCubic(t) * .9f;
                var right = facing * Vector3.right;
                _panel.transform.SetPositionAndRotation(_origin + Vector3.up * rise + right * (_lateral * Ease.OutCubic(t)), facing);
                _panel.transform.localScale = Vector3.one * scale;
                var color = _color;
                color.a = t < .6f ? 1f : 1f - (t - .6f) / .4f;
                _label.style.color = color;
                return t < 1f;
            }

            public override void Release() => _owner.ReturnText(_panel);
        }

        /// <summary>The spark star; without an arena it also flings flat shards.</summary>
        private sealed class Burst : Effect
        {
            private const float Life = .32f;
            private readonly BattleEffects _owner;
            private readonly SpriteRenderer _star;
            private readonly SpriteRenderer[] _shards;
            private readonly Vector3[] _velocities;
            private readonly float _size;

            public Burst(BattleEffects owner, SpriteRenderer star, SpriteRenderer[] shards, Vector3[] velocities, float size)
            {
                _owner = owner;
                _star = star;
                _shards = shards;
                _velocities = velocities;
                _size = size;
            }

            public override bool Tick(float dt, Quaternion facing)
            {
                Age += dt;
                float t = Age / Life;
                if (_star != null)
                {
                    _star.transform.rotation = facing * Quaternion.Euler(0f, 0f, Age * 240f);
                    _star.transform.localScale = Vector3.one * (_size * (t < .25f ? Ease.OutBack(t / .25f) : 1f - (t - .25f) / .75f));
                }
                for (int i = 0; i < _shards.Length; i++)
                {
                    if (_shards[i] == null) continue;
                    _velocities[i] += Vector3.down * (Gravity * dt);
                    _shards[i].transform.position += _velocities[i] * dt;
                    _shards[i].transform.rotation = facing;
                    _shards[i].transform.localScale = Vector3.one * (.09f * _size * (1f - t));
                }
                return t < 1f;
            }

            public override void Release()
            {
                _owner.ReturnSprite(_star);
                foreach (var shard in _shards) _owner.ReturnSprite(shard);
            }
        }

        private sealed class Flight : Effect
        {
            private readonly BattleEffects _owner;
            private readonly SpriteRenderer _sprite;
            private readonly GameObject _source;
            private readonly Transform _mesh;
            private readonly Vector3 _from;
            private readonly Vector3 _to;
            private readonly float _duration;
            private readonly Vector3 _axis;
            private readonly float _spin;

            public Flight(BattleEffects owner, SpriteRenderer sprite, GameObject source, Transform mesh, Vector3 from,
                Vector3 to, float duration, Vector3 axis, float spin)
            {
                _owner = owner;
                _sprite = sprite;
                _source = source;
                _mesh = mesh;
                _from = from;
                _to = to;
                _duration = Mathf.Max(.05f, duration);
                _axis = axis;
                _spin = spin;
            }

            public override bool Tick(float dt, Quaternion facing)
            {
                Age += dt;
                float t = Mathf.Clamp01(Age / _duration);
                float arc = Ease.Hump(t) * (.35f + Vector3.Distance(_from, _to) * .08f);
                var position = Vector3.Lerp(_from, _to, t) + Vector3.up * arc;
                if (_mesh != null)
                {
                    _mesh.SetPositionAndRotation(position, Quaternion.AngleAxis(Age * _spin, _axis));
                    _mesh.localScale = Vector3.one;
                }
                else if (_sprite != null)
                {
                    _sprite.transform.SetPositionAndRotation(position, facing * Quaternion.Euler(0f, 0f, -Age * _spin));
                }
                return t < 1f;
            }

            public override void Release()
            {
                _owner.ReturnSprite(_sprite);
                _owner.ReturnMesh(_source, _mesh);
            }
        }

        private sealed class Ping : Effect
        {
            private readonly BattleEffects _owner;
            private readonly SpriteRenderer _ring;
            private readonly float _radius;
            private readonly float _duration;
            private readonly Color _color;

            public Ping(BattleEffects owner, SpriteRenderer ring, float radius, float duration)
            {
                _owner = owner;
                _ring = ring;
                _radius = radius;
                _duration = Mathf.Max(.05f, duration);
                _color = ring.color;
            }

            public override bool Tick(float dt, Quaternion facing)
            {
                Age += dt;
                float t = Age / _duration;
                if (_ring != null)
                {
                    _ring.transform.localScale = Vector3.one * (_radius * 2f * Mathf.Lerp(.55f, 1.1f, Ease.OutCubic(t)));
                    var color = _color;
                    color.a *= 1f - t;
                    _ring.color = color;
                }
                return t < 1f;
            }

            public override void Release() => _owner.ReturnSprite(_ring);
        }

        /// <summary>One pooled effect mesh: a dust puff, a tumbling chunk or a glowing shard.</summary>
        private sealed class Piece : Effect
        {
            private enum Shape { Puff, Chunk, Shard }

            private readonly BattleEffects _owner;
            private readonly GameObject _source;
            private readonly Shape _shape;
            private readonly Vector3 _origin;
            private readonly Vector3 _drift;
            private readonly float _floor;
            private readonly float _life;
            private readonly float _size;
            private readonly float _delay;
            private readonly Vector3 _axis;
            private Vector3 _position;
            private Vector3 _velocity;
            private Quaternion _rotation;
            private float _spin;
            private Transform _mesh;
            private bool _started;

            private Piece(BattleEffects owner, GameObject source, Shape shape, Vector3 origin, Vector3 drift,
                Vector3 velocity, float floor, float life, float size, float delay, Vector3 axis, float spin)
            {
                _owner = owner;
                _source = source;
                _shape = shape;
                _origin = origin;
                _drift = drift;
                _velocity = velocity;
                _floor = floor;
                _life = Mathf.Max(.05f, life);
                _size = size;
                _delay = Mathf.Max(0f, delay);
                _axis = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.up;
                _spin = spin;
                _position = origin;
                _rotation = Quaternion.Euler(0f, owner.Range(0f, 360f), 0f);
            }

            /// <summary>Grows from nothing to <paramref name="size"/> in the first 30%, drifting by <paramref name="drift"/>, then shrinks away.</summary>
            public static Piece Puff(BattleEffects owner, GameObject source, Vector3 origin, Vector3 drift, float life,
                float size, float delay, float spin) =>
                new(owner, source, Shape.Puff, origin, drift, Vector3.zero, 0f, life, size, delay, Vector3.up, spin);

            public static Piece Chunk(BattleEffects owner, GameObject source, Vector3 origin, Vector3 velocity,
                float floor, float life, float size, Vector3 axis, float spin) =>
                new(owner, source, Shape.Chunk, origin, Vector3.zero, velocity, floor, life, size, 0f, axis, spin);

            public static Piece Shard(BattleEffects owner, GameObject source, Vector3 origin, Vector3 velocity,
                float life, float size) =>
                new(owner, source, Shape.Shard, origin, Vector3.zero, velocity, float.NegativeInfinity, life, size, 0f,
                    Vector3.up, 0f);

            public override bool Tick(float dt, Quaternion facing)
            {
                Age += dt;
                if (Age < _delay) return true;
                if (!_started)
                {
                    _started = true;
                    _mesh = _owner.TakeMesh(_source);
                }
                if (_mesh == null) return false;
                float age = Age - _delay;
                float t = age / _life;
                if (t >= 1f) return false;

                switch (_shape)
                {
                    case Shape.Puff:
                        _mesh.SetPositionAndRotation(_origin + _drift * Ease.OutCubic(t),
                            Quaternion.Euler(0f, _spin * age, 0f) * _rotation);
                        _mesh.localScale = Vector3.one * (_size * (t < .3f ? Ease.OutCubic(t / .3f) :
                            1f - Ease.InCubic((t - .3f) / .7f)));
                        break;
                    case Shape.Chunk:
                        _velocity += Vector3.down * (Gravity * dt);
                        _position += _velocity * dt;
                        if (_position.y < _floor)
                        {
                            // a small bounce, then it lies still and shrinks away
                            _position.y = _floor;
                            if (_velocity.y < 0f) _velocity = new Vector3(_velocity.x * .45f, -_velocity.y * .25f, _velocity.z * .45f);
                            _spin *= .5f;
                        }
                        _rotation = Quaternion.AngleAxis(_spin * dt, _axis) * _rotation;
                        _mesh.SetPositionAndRotation(_position, _rotation);
                        _mesh.localScale = Vector3.one * (_size * (t < .7f ? 1f : 1f - (t - .7f) / .3f));
                        break;
                    default:
                        _velocity += Vector3.down * (Gravity * dt);
                        _position += _velocity * dt;
                        _mesh.SetPositionAndRotation(_position, Quaternion.FromToRotation(Vector3.up, _velocity));
                        _mesh.localScale = Vector3.one * (_size * (1f - t));
                        break;
                }
                return true;
            }

            public override void Release() => _owner.ReturnMesh(_source, _mesh);
        }
    }
}
