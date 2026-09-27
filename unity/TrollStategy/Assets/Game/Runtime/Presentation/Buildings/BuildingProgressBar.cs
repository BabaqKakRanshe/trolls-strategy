using UnityEngine;

namespace TrollStrategy.Presentation.Buildings
{
    // Production bar of a building prefab. Place it by moving this object in the prefab; the view only fills it.
    public sealed class BuildingProgressBar : MonoBehaviour
    {
        private static readonly Color FillColor = ColonyPalette.Gold;
        private static readonly Color TrackColor = ColonyPalette.WithAlpha(ColonyPalette.Night, 0.9f);
        private const float FillLift = 0.01f;

        [SerializeField] private SpriteRenderer _track;
        [SerializeField] private SpriteRenderer _fill;
        [Tooltip("Length of a full bar.")]
        [SerializeField, Min(0.1f)] private float _width = 2.4f;
        [Tooltip("Thickness of the fill.")]
        [SerializeField, Min(0.01f)] private float _thickness = 0.14f;
        [Tooltip("Track margin around the fill.")]
        [SerializeField, Min(0f)] private float _border = 0.04f;

        private static Sprite _solidSprite;

        public void SetProgress(float progress)
        {
            if (_track == null || _fill == null)
            {
                Debug.LogError($"{name} has no progress track or fill", this);
                return;
            }

            var sprite = GetSolidSprite();
            _track.sprite = sprite;
            _track.color = TrackColor;
            _track.sortingOrder = 12;
            _fill.sprite = sprite;
            _fill.color = FillColor;
            _fill.sortingOrder = 13;

            float fillWidth = _width * Mathf.Clamp01(progress);
            _track.transform.localPosition = Vector3.zero;
            _track.transform.localScale = new Vector3(_width + _border * 2f, _thickness + _border * 2f, 1f);
            _fill.transform.localPosition = new Vector3((fillWidth - _width) * 0.5f, 0f, -FillLift);
            _fill.transform.localScale = new Vector3(fillWidth, _thickness, 1f);
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "BuildingProgressSolidTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _solidSprite;
        }

        // The bar has no sprite until play, so the frame shows where it sits while editing the prefab.
        private void OnDrawGizmos()
        {
            if (UnityEngine.Application.isPlaying) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = FillColor;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width + _border * 2f, _thickness + _border * 2f, 0f));
        }
    }
}
