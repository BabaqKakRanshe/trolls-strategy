using UnityEngine;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>Small procedural sprites for feedback: bars, blob shadows, rings, sparks and cell highlights.</summary>
    public static class FeelSprites
    {
        private static Sprite s_white;
        private static Sprite s_softCircle;
        private static Sprite s_ring;
        private static Sprite s_spark;
        private static Sprite s_hex;
        private static Material s_spriteFlash;

        /// <summary>1x1 white, 1 unit wide: scale it into bars and flashes.</summary>
        public static Sprite White => s_white != null ? s_white : s_white = Create(4, (x, y, c) => 1f, FilterMode.Point, 4f);

        /// <summary>Radial falloff disc, for ground shadows and glows.</summary>
        public static Sprite SoftCircle => s_softCircle != null ? s_softCircle : s_softCircle =
            Create(64, (x, y, c) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 1.6f), FilterMode.Bilinear, 64f);

        /// <summary>Thin ring, for team markers and click pings.</summary>
        public static Sprite Ring => s_ring != null ? s_ring : s_ring = Create(96, (x, y, c) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - Mathf.Abs(d - .86f) / .09f);
        }, FilterMode.Bilinear, 96f);

        /// <summary>Four-point star, for hit sparks.</summary>
        public static Sprite Spark => s_spark != null ? s_spark : s_spark = Create(64, (x, y, c) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float star = Mathf.Clamp01(1f - (ax * ay * 14f + Mathf.Sqrt(x * x + y * y) * .9f));
            return star * star;
        }, FilterMode.Bilinear, 64f);

        /// <summary>Pointy-top hexagon (corners on ±Y), 1 unit across corners, for cell highlights.</summary>
        public static Sprite Hex => s_hex != null ? s_hex : s_hex = Create(128, (x, y, c) =>
        {
            // distance to a pointy-top hexagon of circumradius 1
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float d = Mathf.Max(ax * .8660254f + ay * .5f, ay);
            float edge = Mathf.Clamp01((1f - d) / .05f);
            float rim = Mathf.Clamp01(1f - Mathf.Abs(d - .9f) / .08f);
            return Mathf.Max(edge * .45f, rim);
        }, FilterMode.Bilinear, 128f);

        /// <summary>Sprite material that can flash to a solid colour (see TrollStrategy/SpriteFlash).</summary>
        public static Material SpriteFlash
        {
            get
            {
                if (s_spriteFlash != null) return s_spriteFlash;
                var shader = Resources.Load<Shader>("Shaders/SpriteFlash");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                s_spriteFlash = new Material(shader) { name = "SpriteFlash (runtime)" };
                return s_spriteFlash;
            }
        }

        private delegate float Alpha(float x, float y, int size);

        private static Sprite Create(int size, Alpha alpha, FilterMode filter, float pixelsPerUnit)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = (px + .5f) / size * 2f - 1f;
                float y = (py + .5f) / size * 2f - 1f;
                pixels[py * size + px] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y, size)) * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), pixelsPerUnit,
                0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
