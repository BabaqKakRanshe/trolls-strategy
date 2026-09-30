using System;
using System.Collections.Generic;
using TrollStrategy.Presentation.Feel;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>
    /// Small life of a battle arena: windmill sails turn in gusts, banner cloths sway, water textures flow
    /// along their UV V axis (repeats per second), flames flicker, and the layout's sockets emit chimney smoke,
    /// embers and waterfall mist from the arena's effect meshes (see <see cref="BattleArenaSet"/>). Filled by the
    /// arena prefab builder from the layout. Runs on game time, independent of the battle replay.
    /// </summary>
    public sealed class BattleArenaAmbience : MonoBehaviour
    {
        /// <summary>Most emitted meshes alive at once for the whole environment.</summary>
        public const int MaxLiveParticles = 40;

        public enum SocketKind { Smoke, Embers, Mist }

        [Serializable]
        public struct Spinner
        {
            public Transform Target;
            public Vector3 LocalAxis;
            public float DegreesPerSecond;
            [Tooltip("Share of the speed that comes and goes in gusts (0 = steady).")]
            public float Gust;
        }

        [Serializable]
        public struct Flow
        {
            public Renderer Renderer;
            public float RepeatsPerSecond;
        }

        [Serializable]
        public struct Flame
        {
            public Transform Target;
            public float Amount;
        }

        /// <summary>Cloth swinging about its local X axis (the banner's crossbar).</summary>
        [Serializable]
        public struct Sway
        {
            public Transform Target;
            public float Degrees;
            public float Frequency;
            [Range(0f, 1f)] public float Phase;
        }

        /// <summary>
        /// Emitter in arena-root space. Size: smoke — largest puff scale; embers and mist — width of the area
        /// the particles start from (brazier bowl, waterfall foot).
        /// </summary>
        [Serializable]
        public struct Socket
        {
            public SocketKind Kind;
            public Vector3 Position;
            public float Size;
        }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color MistTint = new(1.25f, 1.25f, 1.25f, 1f);

        [SerializeField] private List<Spinner> _spinners = new();
        [SerializeField] private List<Flow> _flows = new();
        [SerializeField] private List<Flame> _flames = new();
        [SerializeField] private List<Sway> _sways = new();
        [SerializeField] private List<Socket> _sockets = new();
        [Tooltip("Spray at the waterfall feet; off leaves the smoke and embers running.")]
        [SerializeField] private bool _mist = true;
        [Tooltip("Smoke and mist meshes of an environment without a BattleArenaSet (the colony); an arena takes its set's.")]
        [SerializeField] private GameObject[] _smoke = Array.Empty<GameObject>();
        [Tooltip("Ember meshes of an environment without a BattleArenaSet.")]
        [SerializeField] private GameObject[] _spark = Array.Empty<GameObject>();

        // runtime state only: never carried over by serialization into a clone
        [NonSerialized] private readonly List<Particle> _live = new();
        [NonSerialized] private readonly Dictionary<(GameObject, bool), Stack<Transform>> _pool = new();
        [NonSerialized] private MaterialPropertyBlock _block;
        [NonSerialized] private Vector3[] _flameScales = Array.Empty<Vector3>();
        [NonSerialized] private Vector4[] _flowTiling = Array.Empty<Vector4>();
        [NonSerialized] private Quaternion[] _swayRest = Array.Empty<Quaternion>();
        [NonSerialized] private float[] _nextEmit = Array.Empty<float>();
        [NonSerialized] private BattleArenaSet _set;
        [NonSerialized] private System.Random _random;
        [NonSerialized] private Transform _particleRoot;
        [NonSerialized] private float _clock;

        public IReadOnlyList<Spinner> Spinners => _spinners;
        public IReadOnlyList<Sway> Sways => _sways;
        public IReadOnlyList<Socket> Sockets => _sockets;
        public int LiveParticles => _live.Count;

        public bool Mist
        {
            get => _mist;
            set => _mist = value;
        }

        public void AddSpinner(Transform target, Vector3 localAxis, float degreesPerSecond, float gust = 0f) =>
            _spinners.Add(new Spinner
            {
                Target = target, LocalAxis = localAxis, DegreesPerSecond = degreesPerSecond, Gust = Mathf.Clamp01(gust)
            });

        public void AddFlow(Renderer renderer, float repeatsPerSecond) =>
            _flows.Add(new Flow { Renderer = renderer, RepeatsPerSecond = repeatsPerSecond });

        public void AddFlame(Transform target, float amount) =>
            _flames.Add(new Flame { Target = target, Amount = amount });

        public void AddSway(Transform target, float degrees, float frequency, float phase) =>
            _sways.Add(new Sway { Target = target, Degrees = degrees, Frequency = frequency, Phase = phase });

        /// <summary>Effect meshes for an environment that carries no <see cref="BattleArenaSet"/>.</summary>
        public void ConfigureEffects(GameObject[] smoke, GameObject[] spark)
        {
            _smoke = smoke ?? Array.Empty<GameObject>();
            _spark = spark ?? Array.Empty<GameObject>();
        }

        public void AddSocket(SocketKind kind, Vector3 position, float size) =>
            _sockets.Add(new Socket { Kind = kind, Position = position, Size = Mathf.Max(.05f, size) });

        /// <summary>Swing in degrees at <paramref name="time"/>: a slow sway with a faster flutter on top.</summary>
        public static float SwayAngle(float degrees, float frequency, float phase, float time) =>
            degrees * Mathf.Sin(2f * Mathf.PI * (frequency * time + phase)) +
            .3f * degrees * Mathf.Sin(2f * Mathf.PI * (2.3f * frequency * time + phase));

        /// <summary>Speed factor of a gusting spinner, within 1 ± <paramref name="gust"/>.</summary>
        public static float GustFactor(float gust, float time, int seed) =>
            1f + gust * (2f * Mathf.Clamp01(Mathf.PerlinNoise(time * .25f, seed * 1.37f + .5f)) - 1f);

        private void Awake() => Prepare();

        private void Update() => Step(Time.deltaTime, Time.time);

        private void OnDestroy()
        {
            foreach (var particle in _live)
                if (particle.Piece != null) Destroy(particle.Piece.gameObject);
            _live.Clear();
        }

        /// <summary>Advances the ambience; <see cref="Update"/> calls it with the frame's game time.</summary>
        public void Step(float dt, float time)
        {
            Prepare();
            _clock += dt;
            for (int i = 0; i < _spinners.Count; i++)
            {
                var spinner = _spinners[i];
                if (spinner.Target == null) continue;
                float speed = spinner.DegreesPerSecond * GustFactor(spinner.Gust, time, i);
                spinner.Target.Rotate(spinner.LocalAxis, speed * dt, Space.Self);
            }

            for (int i = 0; i < _sways.Count && i < _swayRest.Length; i++)
            {
                var sway = _sways[i];
                if (sway.Target == null) continue;
                float angle = SwayAngle(sway.Degrees, sway.Frequency, sway.Phase, time);
                sway.Target.localRotation = _swayRest[i] * Quaternion.Euler(angle, 0f, 0f);
            }

            for (int i = 0; i < _flows.Count && i < _flowTiling.Length; i++)
            {
                var flow = _flows[i];
                if (flow.Renderer == null) continue;
                // offset grows with time, so the stripes travel downstream (UV V runs against the flow)
                var scaleOffset = _flowTiling[i];
                scaleOffset.w = Mathf.Repeat(scaleOffset.w + flow.RepeatsPerSecond * time, 1f);
                flow.Renderer.GetPropertyBlock(_block);
                _block.SetVector(BaseMapSt, scaleOffset);
                _block.SetVector(MainTexSt, scaleOffset);
                flow.Renderer.SetPropertyBlock(_block);
            }

            for (int i = 0; i < _flames.Count && i < _flameScales.Length; i++)
            {
                var flame = _flames[i];
                if (flame.Target == null) continue;
                float tall = Mathf.PerlinNoise(time * 3.1f, i * 1.37f) - .5f;
                float wide = Mathf.PerlinNoise(i * 2.11f, time * 4.3f) - .5f;
                var baseScale = _flameScales[i];
                flame.Target.localScale = new Vector3(
                    baseScale.x * (1f + flame.Amount * wide),
                    baseScale.y * (1f + flame.Amount * 2f * tall),
                    baseScale.z * (1f + flame.Amount * wide));
            }

            Emit();
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].Tick(dt)) continue;
                Return(_live[i]);
                _live.RemoveAt(i);
            }
        }

        private void Prepare()
        {
            if (_block != null) return;
            _block = new MaterialPropertyBlock();
            _set = GetComponent<BattleArenaSet>();
            _random = new System.Random(7919);
            _flameScales = new Vector3[_flames.Count];
            for (int i = 0; i < _flames.Count; i++)
                _flameScales[i] = _flames[i].Target != null ? _flames[i].Target.localScale : Vector3.one;
            _swayRest = new Quaternion[_sways.Count];
            for (int i = 0; i < _sways.Count; i++)
                _swayRest[i] = _sways[i].Target != null ? _sways[i].Target.localRotation : Quaternion.identity;
            // keep the material's own tiling and offset; only the V offset is animated
            _flowTiling = new Vector4[_flows.Count];
            for (int i = 0; i < _flows.Count; i++)
            {
                var material = _flows[i].Renderer != null ? _flows[i].Renderer.sharedMaterial : null;
                if (material != null && material.HasProperty(BaseMap))
                {
                    var scale = material.GetTextureScale(BaseMap);
                    var offset = material.GetTextureOffset(BaseMap);
                    _flowTiling[i] = new Vector4(scale.x, scale.y, offset.x, offset.y);
                }
                else
                {
                    _flowTiling[i] = new Vector4(1f, 1f, 0f, 0f);
                }
            }
            // emitters start out of step, so the chimneys do not puff together
            _nextEmit = new float[_sockets.Count];
            for (int i = 0; i < _sockets.Count; i++) _nextEmit[i] = Range(0f, Interval(_sockets[i].Kind));
        }

        private void Emit()
        {
            if (_set == null && _smoke.Length == 0 && _spark.Length == 0) return;
            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_clock < _nextEmit[i]) continue;
                var socket = _sockets[i];
                _nextEmit[i] = _clock + Interval(socket.Kind);
                if (socket.Kind == SocketKind.Mist && !_mist) continue;
                if (_live.Count >= MaxLiveParticles) continue;
                var source = Effect(socket.Kind == SocketKind.Embers, _random.Next());
                if (source == null) continue;
                bool mist = socket.Kind == SocketKind.Mist;
                var piece = Take(source, mist);
                var origin = transform.TransformPoint(socket.Position);
                _live.Add(socket.Kind switch
                {
                    SocketKind.Smoke => Smoke(piece, source, origin, socket.Size),
                    SocketKind.Embers => Ember(piece, source, origin, socket.Size),
                    _ => MistPuff(piece, source, origin, socket.Size)
                });
            }
        }

        private GameObject Effect(bool ember, int variant)
        {
            if (_set != null) return _set.Effect(ember ? ArenaFx.Spark : ArenaFx.Smoke, variant);
            var pool = ember ? _spark : _smoke;
            if (pool == null || pool.Length == 0) return null;
            int start = (variant % pool.Length + pool.Length) % pool.Length;
            for (int i = 0; i < pool.Length; i++)
                if (pool[(start + i) % pool.Length] != null) return pool[(start + i) % pool.Length];
            return null;
        }

        private float Interval(SocketKind kind) => kind switch
        {
            SocketKind.Smoke => Range(.6f, .9f),
            SocketKind.Embers => Range(.16f, .28f),
            _ => Range(.28f, .42f)
        };

        // smoke: a puff rises 1.2-1.8 m, drifts downwind, grows to the socket size and shrinks away
        private Particle Smoke(Transform piece, GameObject source, Vector3 origin, float size)
        {
            float life = Range(2.1f, 2.7f);
            var drift = new Vector3(Range(.25f, .5f), Range(1.2f, 1.8f), Range(-.15f, .15f));
            return new Particle(piece, source, false, origin + Jitter(.05f * size), drift, Vector3.zero, 0f, life,
                size * Range(.85f, 1f), Particle.Shape.Grow, Range(-35f, 35f));
        }

        // embers: small glowing shards shoot up out of the fire and burn out in 0.4-0.7 s
        private Particle Ember(Transform piece, GameObject source, Vector3 origin, float size)
        {
            var start = origin + Disc(.4f * size);
            var velocity = new Vector3(Range(-.35f, .35f), Range(1.3f, 2.1f), Range(-.35f, .35f));
            return new Particle(piece, source, false, start, Vector3.zero, velocity, -1.2f, Range(.4f, .7f),
                Range(.45f, .7f), Particle.Shape.Shard, 0f);
        }

        // mist: lightened puffs pop low over the water where the falls land
        private Particle MistPuff(Transform piece, GameObject source, Vector3 origin, float size)
        {
            var start = origin + Disc(.45f * size);
            var drift = Disc(.25f * size) + Vector3.up * Range(.15f, .35f);
            return new Particle(piece, source, true, start, drift, Vector3.zero, 0f, Range(.5f, .8f),
                Range(.55f, .9f), Particle.Shape.Pop, Range(-60f, 60f));
        }

        private Transform Take(GameObject source, bool mist)
        {
            if (!_pool.TryGetValue((source, mist), out var stack)) _pool[(source, mist)] = stack = new Stack<Transform>();
            Transform piece = null;
            while (stack.Count > 0 && piece == null) piece = stack.Pop();
            if (piece == null)
            {
                if (_particleRoot == null)
                {
                    _particleRoot = new GameObject("AmbienceParticles").transform;
                    _particleRoot.SetParent(transform, false);
                }
                piece = Instantiate(source, _particleRoot).transform;
                piece.name = source.name;
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (!mist) continue;
                    renderer.GetPropertyBlock(_block);
                    _block.SetColor(BaseColor, MistTint);
                    renderer.SetPropertyBlock(_block);
                }
            }
            piece.gameObject.SetActive(true);
            piece.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            piece.localScale = Vector3.zero;
            return piece;
        }

        private void Return(Particle particle)
        {
            if (particle.Piece == null) return;
            particle.Piece.gameObject.SetActive(false);
            _pool[(particle.Source, particle.Mist)].Push(particle.Piece);
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private Vector3 Jitter(float radius) => new(Range(-radius, radius), 0f, Range(-radius, radius));

        private Vector3 Disc(float diameter)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float radius = .5f * diameter * Mathf.Sqrt((float)_random.NextDouble());
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>One emitted mesh: moves, then vanishes by scale.</summary>
        private sealed class Particle
        {
            public enum Shape { Grow, Pop, Shard }

            public readonly Transform Piece;
            public readonly GameObject Source;
            public readonly bool Mist;
            private readonly Vector3 _origin;
            private readonly Vector3 _drift;
            private readonly float _gravity;
            private readonly float _life;
            private readonly float _size;
            private readonly Shape _shape;
            private readonly float _spin;
            private readonly Quaternion _rest;
            private Vector3 _position;
            private Vector3 _velocity;
            private float _age;

            public Particle(Transform piece, GameObject source, bool mist, Vector3 origin, Vector3 drift,
                Vector3 velocity, float gravity, float life, float size, Shape shape, float spin)
            {
                Piece = piece;
                Source = source;
                Mist = mist;
                _origin = origin;
                _drift = drift;
                _velocity = velocity;
                _gravity = gravity;
                _life = Mathf.Max(.05f, life);
                _size = size;
                _shape = shape;
                _spin = spin;
                _rest = piece.rotation;
                _position = origin;
                piece.position = origin;
            }

            public bool Tick(float dt)
            {
                _age += dt;
                float t = _age / _life;
                if (Piece == null || t >= 1f) return false;
                switch (_shape)
                {
                    case Shape.Grow:
                        // rises fast, then hangs; peaks at 60% of its life, then shrinks
                        Piece.position = _origin + new Vector3(_drift.x * t, _drift.y * Ease.OutCubic(t), _drift.z * t);
                        Piece.localScale = Vector3.one * (_size * (t < .6f ? Mathf.Lerp(.25f, 1f, Ease.OutCubic(t / .6f)) :
                            1f - Ease.InCubic((t - .6f) / .4f)));
                        Piece.rotation = Quaternion.Euler(0f, _spin * _age, 0f) * _rest;
                        break;
                    case Shape.Pop:
                        Piece.position = _origin + _drift * Ease.OutCubic(t);
                        Piece.localScale = Vector3.one * (_size * (t < .3f ? Ease.OutCubic(t / .3f) : 1f - Ease.InCubic((t - .3f) / .7f)));
                        Piece.rotation = Quaternion.Euler(0f, _spin * _age, 0f) * _rest;
                        break;
                    default:
                        _velocity += Vector3.up * (_gravity * dt);
                        _position += _velocity * dt;
                        Piece.position = _position;
                        Piece.rotation = Quaternion.FromToRotation(Vector3.up, _velocity);
                        Piece.localScale = Vector3.one * (_size * (1f - t));
                        break;
                }
                return true;
            }
        }
    }
}
