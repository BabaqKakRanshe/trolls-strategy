using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TrollStrategy.Presentation.Map
{
    /// <summary>
    /// The cell grid over the colony's land: one quad just above the lawn in the map plane, drawn by
    /// TrollStrategy/GroundGrid. The grid shows on cleared land only (a cell mask from the snapshot's land) and grows in
    /// when a block is cleared; it lights up round the pointer and while the player places a building or creatures.
    /// Shows cells only: which cells take a building is the domain's placement rule.
    /// </summary>
    public sealed class GroundGridView : MonoBehaviour
    {
        private const string ShaderPath = "Shaders/GroundGrid";
        private static readonly int LandMaskId = Shader.PropertyToID("_LandMask");
        private static readonly int GridSizeId = Shader.PropertyToID("_GridSize");
        private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
        private static readonly int BlockColorId = Shader.PropertyToID("_BlockColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int EmphasisId = Shader.PropertyToID("_Emphasis");
        private static readonly int PointerId = Shader.PropertyToID("_Pointer");

        [Tooltip("Height of the grid over the lawn, m.")]
        [SerializeField, Min(0f)] private float _lift = .03f;
        [Tooltip("Seconds a block's grid takes to appear or go.")]
        [SerializeField, Min(.01f)] private float _fadeSeconds = .9f;
        [Tooltip("Radius of the light round the pointer, cells.")]
        [SerializeField, Min(.5f)] private float _pointerRadius = 4.5f;
        // block borders every this many cells when the game has no land blocks
        [SerializeField, Min(1)] private int _fallbackBlockCells = 5;

        private GameSession _session;
        private InteractionController _interaction;
        private TilemapWorldView _worldView;
        private Camera _camera;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _mask;
        private int _width, _height;
        private float[] _target, _shown;
        private byte[] _bytes;
        private bool _fading;
        private float _emphasis;
        private Vector2 _pointer;
        private float _pointerOn;

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView, Camera camera)
        {
            Unsubscribe();
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _camera = camera;
            if (_worldView == null || !Build()) return;
            if (_session == null) return;
            _session.OnSnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(_session.CurrentSnapshot);
            // a game that starts or loads shows its grid in place
            System.Array.Copy(_target, _shown, _shown.Length);
            Upload();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_material != null) Destroy(_material);
            if (_mask != null) Destroy(_mask);
            if (_mesh != null) Destroy(_mesh);
        }

        private void Unsubscribe()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
        }

        private bool Build()
        {
            if (_material != null) return true;
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"{nameof(GroundGridView)}: no shader at Resources/{ShaderPath}; the grid is not drawn.", this);
                return false;
            }
            _width = Mathf.Max(1, _worldView.GridWidth);
            _height = Mathf.Max(1, _worldView.GridHeight);
            float cell = _worldView.CellSize;
            float w = _width * cell, h = _height * cell, z = -_lift;   // map plane: local -Z is up

            _mesh = new Mesh { name = "GroundGrid" };
            _mesh.SetVertices(new[] { new Vector3(0f, 0f, z), new Vector3(w, 0f, z), new Vector3(w, h, z), new Vector3(0f, h, z) });
            _mesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            _mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _mask = new Texture2D(_width, _height, TextureFormat.R8, false, true)
            {
                name = "GroundGridMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _target = new float[_width * _height];
            _shown = new float[_target.Length];
            _bytes = new byte[_target.Length];

            _material = new Material(shader) { name = "GroundGrid (runtime)" };
            _material.SetTexture(LandMaskId, _mask);
            _material.SetVector(GridSizeId, new Vector4(_width, _height, _fallbackBlockCells, 0f));
            _material.SetColor(LineColorId, ColonyPalette.Cream);
            _material.SetColor(BlockColorId, ColonyPalette.Gold);
            _material.SetColor(GlowColorId, ColonyPalette.Text);

            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            if (snapshot == null || _target == null) return;
            var land = snapshot.Land;
            _material.SetVector(GridSizeId,
                new Vector4(_width, _height, land != null ? land.BlockSize : _fallbackBlockCells, 0f));
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                bool open = land == null ||
                            land.BlockOf(new Cell(x, y), out int bx, out int by) && land.Block(bx, by).Cleared;
                float value = open ? 1f : 0f;
                int i = y * _width + x;
                if (_target[i] == value) continue;
                _target[i] = value;
                _fading = true;
            }
        }

        private void Update()
        {
            if (_material == null) return;
            float dt = Time.deltaTime;
            if (_fading) Fade(dt / _fadeSeconds);

            var mode = _interaction != null ? _interaction.Mode.Type : InteractionModeType.Neutral;
            float emphasis = mode == InteractionModeType.PlacingBuilding || mode == InteractionModeType.MovingBuilding ||
                             mode == InteractionModeType.PlacingUnits ? 1f
                : mode == InteractionModeType.ManagingLand ? .5f
                : 0f;
            _emphasis = Mathf.MoveTowards(_emphasis, emphasis, dt / .25f);
            _material.SetFloat(EmphasisId, _emphasis);

            // the light follows the pointer over the map and fades out where the pointer leaves it
            bool over = false;
            if (Mouse.current != null && _camera != null && !UIInputUtils.IsPointerOverUI() &&
                WorldProjection.TryGroundPoint(_camera, Mouse.current.position.ReadValue(), _worldView, out var world))
            {
                var map = _worldView.WorldToMap(world) / _worldView.CellSize;
                var target = new Vector2(map.x, map.y);
                _pointer = _pointerOn > .01f ? Vector2.Lerp(_pointer, target, 1f - Mathf.Exp(-25f * dt)) : target;
                over = true;
            }
            _pointerOn = Mathf.MoveTowards(_pointerOn, over ? 1f : 0f, dt / .2f);
            _material.SetVector(PointerId, new Vector4(_pointer.x, _pointer.y, _pointerRadius, _pointerOn));
        }

        private void Fade(float step)
        {
            bool moving = false;
            for (int i = 0; i < _shown.Length; i++)
            {
                if (_shown[i] == _target[i]) continue;
                _shown[i] = Mathf.MoveTowards(_shown[i], _target[i], step);
                moving |= _shown[i] != _target[i];
            }
            _fading = moving;
            Upload();
        }

        private void Upload()
        {
            for (int i = 0; i < _shown.Length; i++) _bytes[i] = (byte)Mathf.RoundToInt(_shown[i] * 255f);
            _mask.SetPixelData(_bytes, 0);
            _mask.Apply(false, false);
        }
    }
}
