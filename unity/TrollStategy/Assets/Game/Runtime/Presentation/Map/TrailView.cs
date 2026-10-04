using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrollStrategy.Presentation.Map
{
    /// <summary>
    /// Trails on the colony's lawn: one quad just above the lawn in the map plane, under the cell grid, drawn by
    /// TrollStrategy/Trails from a texture with a texel per cell (R: wear). The wear comes from the snapshot and the
    /// drawn wear eases toward it, so a path darkens while it is trodden and fades while it grows over. Draws only:
    /// wear, stages and speeds are TrailRules'. A colony without trails hides it.
    /// </summary>
    public sealed class TrailView : MonoBehaviour
    {
        private const string ShaderPath = "Shaders/Trails";
        private static readonly int WearId = Shader.PropertyToID("_Wear");
        private static readonly int GridSizeId = Shader.PropertyToID("_GridSize");
        private static readonly int StagesId = Shader.PropertyToID("_Stages");
        private static readonly int TrampledTintId = Shader.PropertyToID("_TrampledTint");
        private static readonly int PathTintId = Shader.PropertyToID("_PathTint");
        private static readonly int RoadTintId = Shader.PropertyToID("_RoadTint");

        [Tooltip("Height over the lawn, m; the cell grid lies higher.")]
        [SerializeField, Min(0f)] private float _lift = .015f;
        [Tooltip("How fast the drawn wear follows the game's, in shares of the most a cell can take per second.")]
        [SerializeField, Min(.01f)] private float _easePerSecond = .2f;
        [Tooltip("The lawn as the colony camera shows it in the sun; the tints turn it into the colours below.")]
        [SerializeField] private Color _lawn = new Color32(145, 183, 70, 255);
        [SerializeField] private Color _trampled = new Color32(172, 192, 98, 255);
        [SerializeField] private Color _path = ColonyPalette.Path;
        [SerializeField] private Color _road = ColonyPalette.Sand;

        private GameSession _session;
        private TilemapWorldView _worldView;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _texture;
        private int _width, _height;
        private float[] _target, _shown;
        private byte[] _wear, _bytes;
        private bool _easing;

        public void Init(GameSession session, TilemapWorldView worldView)
        {
            Unsubscribe();
            _session = session;
            _worldView = worldView;
            if (_session == null || _worldView == null) return;
            var trails = _session.CurrentSnapshot.Trails;
            if (trails == null)
            {
                gameObject.SetActive(false);
                return;
            }
            if (!Build(trails)) return;
            _session.OnSnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(_session.CurrentSnapshot);
            // a game that starts or loads shows its trails in place
            System.Array.Copy(_target, _shown, _shown.Length);
            Upload();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_material != null) Destroy(_material);
            if (_texture != null) Destroy(_texture);
            if (_mesh != null) Destroy(_mesh);
        }

        private void Unsubscribe()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
        }

        private bool Build(TrailSnapshot trails)
        {
            if (_material != null) return true;
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"{nameof(TrailView)}: no shader at Resources/{ShaderPath}; trails are not drawn.", this);
                return false;
            }
            _width = trails.Width;
            _height = trails.Height;
            float cell = _worldView.CellSize;
            float w = _width * cell, h = _height * cell, z = -_lift;   // map plane: local -Z is up

            _mesh = new Mesh { name = "Trails" };
            _mesh.SetVertices(new[] { new Vector3(0f, 0f, z), new Vector3(w, 0f, z), new Vector3(w, h, z), new Vector3(0f, h, z) });
            _mesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            _mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _texture = new Texture2D(_width, _height, TextureFormat.R8, false, true)
            {
                name = "TrailWear",
                // the shader reads whole cells and rounds them off itself
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _target = new float[_width * _height];
            _shown = new float[_target.Length];
            _wear = new byte[_target.Length];
            _bytes = new byte[_target.Length];

            float max = Mathf.Max(1, trails.MaxWear);
            _material = new Material(shader) { name = "Trails (runtime)" };
            _material.SetTexture(WearId, _texture);
            _material.SetVector(GridSizeId, new Vector4(_width, _height, 0f, 0f));
            _material.SetVector(StagesId,
                new Vector4(trails.TrampledAt / max, trails.PathAt / max, trails.RoadAt / max, 0f));
            _material.SetVector(TrampledTintId, Tint(_trampled));
            _material.SetVector(PathTintId, Tint(_path));
            _material.SetVector(RoadTintId, Tint(_road));

            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        // The shader doubles tint x lawn in the frame's colour space, so the tint that turns the lawn into a colour is
        // half their ratio there. Set as a vector: SetColor would convert it once more.
        private Vector4 Tint(Color color)
        {
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color target = linear ? color.linear : color, lawn = linear ? _lawn.linear : _lawn;
            return new Vector4(
                Mathf.Clamp01(target.r / Mathf.Max(.001f, 2f * lawn.r)),
                Mathf.Clamp01(target.g / Mathf.Max(.001f, 2f * lawn.g)),
                Mathf.Clamp01(target.b / Mathf.Max(.001f, 2f * lawn.b)),
                1f);
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            var trails = snapshot?.Trails;
            if (trails == null || _target == null || trails.Width != _width || trails.Height != _height) return;
            trails.CopyWear(_wear);
            float max = Mathf.Max(1, trails.MaxWear);
            for (int i = 0; i < _target.Length; i++)
            {
                float value = _wear[i] / max;
                if (_target[i] == value) continue;
                _target[i] = value;
                _easing = true;
            }
        }

        private void Update()
        {
            if (_material == null || !_easing) return;
            float step = _easePerSecond * Time.deltaTime;
            bool moving = false;
            for (int i = 0; i < _shown.Length; i++)
            {
                if (_shown[i] == _target[i]) continue;
                _shown[i] = Mathf.MoveTowards(_shown[i], _target[i], step);
                moving |= _shown[i] != _target[i];
            }
            _easing = moving;
            Upload();
        }

        private void Upload()
        {
            for (int i = 0; i < _shown.Length; i++) _bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(_shown[i]) * 255f);
            _texture.SetPixelData(_bytes, 0);
            _texture.Apply(false, false);
        }
    }
}
