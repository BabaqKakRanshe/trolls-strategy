using TMPro;
using UnityEngine;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>
    /// A short line of text that pops where something happened in the world (for example why a building
    /// cannot stand there), rises and fades. Faces the main camera; destroys itself.
    /// </summary>
    public sealed class WorldToast : MonoBehaviour
    {
        private const float Life = 1.6f;

        private TextMeshPro _label;
        private Vector3 _origin;
        private Vector3 _up;
        private Color _color;
        private float _time;

        public static void Show(Vector3 position, Vector3 up, string text, Color color, float size = 5f)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var go = new GameObject("WorldToast", typeof(TextMeshPro), typeof(WorldToast));
            var label = go.GetComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(20f, 2f);
            label.outlineWidth = .3f;
            label.outlineColor = new Color32(20, 16, 12, 255);
            label.sortingOrder = 80;
            label.color = color;
            var toast = go.GetComponent<WorldToast>();
            toast._label = label;
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
            transform.localScale = Vector3.one * (t < .12f ? Ease.OutBack(t / .12f, 2.5f) : 1f);
            var color = _color;
            color.a = t < .7f ? 1f : 1f - (t - .7f) / .3f;
            _label.color = color;
        }
    }
}
