using UnityEngine;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>A ring that spreads over the ground and fades: "here", on a target, a landing or a refusal.</summary>
    public sealed class WorldPing : MonoBehaviour
    {
        private SpriteRenderer _ring;
        private Color _color;
        private float _radius;
        private float _duration;
        private float _time;

        /// <param name="groundRotation">Rotation whose XY plane is the ground (the colony grid's rotation).</param>
        public static void Show(Vector3 position, Quaternion groundRotation, Color color, float radius, float duration = .45f)
        {
            var go = new GameObject("WorldPing", typeof(SpriteRenderer), typeof(WorldPing));
            go.transform.SetPositionAndRotation(position, groundRotation);
            var ping = go.GetComponent<WorldPing>();
            ping._ring = go.GetComponent<SpriteRenderer>();
            ping._ring.sprite = FeelSprites.Ring;
            ping._ring.sortingOrder = 18;
            ping._color = color;
            ping._radius = radius;
            ping._duration = Mathf.Max(.05f, duration);
            ping.Tick(0f);
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void Tick(float dt)
        {
            _time += dt;
            float t = _time / _duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * (_radius * 2f * Mathf.Lerp(.5f, 1.15f, Ease.OutCubic(t)));
            var color = _color;
            color.a *= 1f - t;
            _ring.color = color;
        }
    }
}
