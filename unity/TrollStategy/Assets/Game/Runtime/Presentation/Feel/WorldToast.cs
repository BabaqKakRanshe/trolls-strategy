using TrollStrategy.Presentation.WorldUi;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>
    /// A short line of text that pops where something happened in the world (for example why a building
    /// cannot stand there), rises and fades. Faces the main camera; destroys itself.
    /// </summary>
    public sealed class WorldToast : MonoBehaviour
    {
        private const float Life = 1.6f;

        private WorldPanel _panel;
        private Label _label;
        private Vector3 _origin;
        private Vector3 _up;
        private Color _color;
        private float _time;

        public static void Show(Vector3 position, Vector3 up, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var panel = WorldPanel.Create("WorldToast", null, 80);
            var toast = panel.gameObject.AddComponent<WorldToast>();
            toast._panel = panel;
            toast._label = panel.AddLabel("world-label world-label--toast");
            toast._label.text = text;
            toast._origin = position;
            toast._up = up.sqrMagnitude > .0001f ? up.normalized : Vector3.up;
            toast._color = color;
            toast.Tick(0f);
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void Tick(float dt)
        {
            _time += dt;
            float t = _time / Life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
            transform.position = _origin + _up * (Ease.OutCubic(t) * .9f);
            _panel.Scale = t < .12f ? Ease.OutBack(t / .12f, 2.5f) : 1f;
            var color = _color;
            color.a = t < .7f ? 1f : 1f - (t - .7f) / .3f;
            _label.style.color = color;
        }
    }
}
