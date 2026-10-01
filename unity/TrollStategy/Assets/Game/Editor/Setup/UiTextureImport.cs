using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Import settings for the HUD's own pictures in Assets/Game/UI/Sprites: soft shadows, mist and the
    /// screen haze are smooth alpha ramps that block compression bands and mipmaps would spoil, and 9-sliced
    /// sheets must not wrap at their edges.
    /// </summary>
    public sealed class UiTextureImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/UI/Sprites/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
