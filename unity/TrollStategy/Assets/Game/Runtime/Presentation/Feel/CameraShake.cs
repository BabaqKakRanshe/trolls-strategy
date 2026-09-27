using UnityEngine;
using UnityEngine.Rendering;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>
    /// Trauma-based camera shake. The offset exists only while the camera renders, so framing code,
    /// picking rays and billboards never see a shaken camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("Offset at full trauma, metres.")]
        [SerializeField, Min(0f)] private float _maxOffset = .35f;
        [Tooltip("Roll at full trauma, degrees.")]
        [SerializeField, Min(0f)] private float _maxRoll = 1.2f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField, Min(.1f)] private float _decay = 2.4f;

        private Camera _camera;
        private float _trauma;
        private float _time;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private bool _applied;

        /// <summary>Adds trauma 0..1; the visible shake grows with its square, so small kicks stay small.</summary>
        public void Kick(float trauma) => _trauma = Mathf.Clamp01(_trauma + trauma);

        public static void Kick(Camera camera, float trauma)
        {
            if (camera == null) return;
            if (!camera.TryGetComponent<CameraShake>(out var shake)) shake = camera.gameObject.AddComponent<CameraShake>();
            shake.Kick(trauma);
        }

        private void Awake() => _camera = GetComponent<Camera>();

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            Restore();
            _trauma = 0f;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            _trauma = Mathf.Max(0f, _trauma - _decay * Time.unscaledDeltaTime);
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera || _trauma <= 0f) return;
            _savedPosition = transform.position;
            _savedRotation = transform.rotation;
            _applied = true;
            float shake = _trauma * _trauma;
            float x = Mathf.PerlinNoise(_time * 23f, 1.7f) * 2f - 1f;
            float y = Mathf.PerlinNoise(4.3f, _time * 23f) * 2f - 1f;
            float roll = Mathf.PerlinNoise(_time * 19f, 9.1f) * 2f - 1f;
            transform.position += (transform.right * x + transform.up * y) * (_maxOffset * shake);
            transform.rotation *= Quaternion.Euler(0f, 0f, roll * _maxRoll * shake);
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _camera) Restore();
        }

        private void Restore()
        {
            if (!_applied) return;
            _applied = false;
            transform.SetPositionAndRotation(_savedPosition, _savedRotation);
        }
    }
}
