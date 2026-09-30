using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Presentation.Units
{
    /// <summary>
    /// Where a creature is inside its pixel-art frame. The frame keeps transparent margins around the body (and the
    /// tight sprite mesh still extrudes around tiny sprites), so neither edge is the feet: views stand the opaque
    /// bottom row on the ground and size shadows by the opaque width.
    /// </summary>
    public static class SpriteBody
    {
        private static readonly Dictionary<Sprite, Rect> Bounds = new();

        /// <summary>
        /// Opaque pixels of <paramref name="sprite"/> in its local units from the pivot, before any scale: yMin is the
        /// feet, yMax the top of the head. The sprite's own bounds when it has no opaque pixel.
        /// </summary>
        public static Rect Opaque(Sprite sprite)
        {
            if (sprite == null) return Rect.zero;
            if (Bounds.TryGetValue(sprite, out var bounds)) return bounds;
            var rect = sprite.rect;
            var copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            var target = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0,
                RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                // Imported art need not enable Read/Write. Read back just this frame once per creature sprite.
                Graphics.Blit(sprite.texture, target);
                RenderTexture.active = target;
                copy.ReadPixels(rect, 0, 0, false);
                var pixels = copy.GetPixels32();
                int left = copy.width, right = -1, bottom = copy.height, top = -1;
                for (int y = 0; y < copy.height; y++)
                for (int x = 0; x < copy.width; x++)
                {
                    if (pixels[y * copy.width + x].a == 0) continue;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
                float ppu = sprite.pixelsPerUnit;
                bounds = top >= bottom
                    ? Rect.MinMaxRect((left - sprite.pivot.x) / ppu, (bottom - sprite.pivot.y) / ppu,
                        (right + 1f - sprite.pivot.x) / ppu, (top + 1f - sprite.pivot.y) / ppu)
                    : Rect.MinMaxRect(sprite.bounds.min.x, sprite.bounds.min.y, sprite.bounds.max.x, sprite.bounds.max.y);
                Bounds[sprite] = bounds;
                return bounds;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (UnityEngine.Application.isPlaying) Object.Destroy(copy);
                else Object.DestroyImmediate(copy);
            }
        }
    }
}
