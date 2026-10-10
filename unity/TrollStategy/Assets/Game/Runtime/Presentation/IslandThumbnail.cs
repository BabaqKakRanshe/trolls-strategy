using System;
using UnityEngine;

namespace TrollStrategy.Presentation
{
    /// <summary>
    /// A small picture of the island for a save slot: the colony camera rendered once into a reused 640×360 texture,
    /// halved to 320×180 (the halving smooths the edges) and read back as a PNG. UI Toolkit screens are not part of a
    /// camera's render, so the HUD stays out of it. The composer takes it on the frame of a save, outside the colony's
    /// step; the textures are made once and kept, and only the 320×180 picture is read back from the GPU.
    /// </summary>
    public sealed class IslandThumbnail : IDisposable
    {
        public const int Width = 320;
        public const int Height = 180;

        private RenderTexture _render;
        private RenderTexture _half;
        private Texture2D _pixels;

        /// <summary>The island as <paramref name="camera"/> sees it now, as a PNG; null without a camera.</summary>
        public byte[] Capture(Camera camera)
        {
            if (camera == null) return null;
            if (_render == null) _render = Target(Width * 2, Height * 2, 24);
            if (_half == null) _half = Target(Width, Height, 0);
            if (_pixels == null)
                _pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false) { name = "IslandThumbnail" };

            var target = camera.targetTexture;
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = _render;
                camera.Render();
                Graphics.Blit(_render, _half);
                RenderTexture.active = _half;
                _pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                return _pixels.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = target;
                RenderTexture.active = active;
            }
        }

        public void Dispose()
        {
            Free(ref _render);
            Free(ref _half);
            if (_pixels != null) UnityEngine.Object.Destroy(_pixels);
            _pixels = null;
        }

        private static RenderTexture Target(int width, int height, int depth) =>
            new(width, height, depth, RenderTextureFormat.ARGB32) { name = "IslandThumbnail", antiAliasing = 1 };

        private static void Free(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
