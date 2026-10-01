using System;
using TrollStrategy.Content;
using UnityEngine;

namespace TrollStrategy.Presentation.Buildings
{
    /// <summary>
    /// Renders one building model, slowly turning, into a texture for the catalog preview. The model sits
    /// far from the map on its own layer with its own camera and light, so the colony view never sees it.
    /// </summary>
    public sealed class BuildingShowcase : IDisposable
    {
        private const int Layer = 30;
        private const float TurnDegreesPerSecond = 12f;
        private static readonly Vector3 StagePosition = new(1000f, 1000f, 1000f);

        private GameObject _instance;
        private Camera _camera;
        private RenderTexture _texture;

        /// <summary>
        /// Shows the building's model; returns the texture it renders into, or null without a model. Framing is
        /// the room around the model: 1 fills the picture, larger values leave a margin.
        /// </summary>
        public Texture Show(BuildingDefinition definition, float framing = 1.65f)
        {
            Hide();
            var model = definition != null ? ContentPrefabs.Building(definition)?.Model : null;
            if (model == null) return null;

            // the model is authored in the map plane; stand it upright like the rotated colony grid does
            _instance = UnityEngine.Object.Instantiate(model.gameObject, StagePosition, Quaternion.Euler(90f, 0f, 0f));
            _instance.name = $"Showcase_{definition.Kind}";
            SetLayer(_instance.transform);
            foreach (var collider in _instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            var renderers = _instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Hide();
                return null;
            }
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            EnsureCamera();
            float radius = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
            _camera.orthographicSize = Mathf.Max(bounds.extents.y, radius) * Mathf.Max(.5f, framing);
            _camera.transform.position = bounds.center + new Vector3(1f, .7f, -1f).normalized * 20f;
            _camera.transform.LookAt(bounds.center);
            _camera.enabled = true;
            return _texture;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_instance != null)
                _instance.transform.Rotate(Vector3.up, TurnDegreesPerSecond * unscaledDeltaTime, Space.World);
        }

        public void Hide()
        {
            if (_camera != null) _camera.enabled = false;
            if (_instance != null) UnityEngine.Object.Destroy(_instance);
            _instance = null;
        }

        public void Dispose()
        {
            Hide();
            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            _camera = null;
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
            }
            _texture = null;
        }

        private void EnsureCamera()
        {
            if (_texture == null)
            {
                _texture = new RenderTexture(512, 512, 16) { name = "BuildingShowcaseTexture" };
                _texture.Create();
            }
            if (_camera != null) return;

            var cameraObject = new GameObject("BuildingShowcaseCamera");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(234, 240, 247, 255);
            _camera.cullingMask = 1 << Layer;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 100f;
            _camera.targetTexture = _texture;
            var light = cameraObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.cullingMask = 1 << Layer;
            light.shadows = LightShadows.None;
        }

        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = Layer;
            foreach (Transform child in root) SetLayer(child);
        }
    }
}
