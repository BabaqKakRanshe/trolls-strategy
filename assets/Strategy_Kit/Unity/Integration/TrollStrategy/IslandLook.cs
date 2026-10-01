using System;
using TrollStrategy.Presentation.Island;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Editor.Setup
{
#pragma warning disable 0649   // filled by JsonUtility
    // The island look of the kit layouts (isle_layout.json and arena_*_layout.json): the kit writes both from one
    // function (Blender/build_isle.py, game_look), so the colony and the battle read it through these classes.

    [Serializable]
    internal sealed class IsleSun
    {
        public float[] forward;
        public float[] color;
        public float intensity;
        public float shadowStrength;
    }

    [Serializable]
    internal sealed class IsleFog
    {
        public float[] color;
        public float startPerDistance;
        public float endPerDistance;
    }

    [Serializable]
    internal sealed class IslePost
    {
        public float exposure;
        public float saturation;
        public float contrast;
        public float temperature;
        public float bloomThreshold;
        public float bloomIntensity;
        public float bloomScatter;
        public float dofStartPerDistance;
        public float dofEndPerDistance;
        public float dofMaxRadius;
        public float vignette;
        public float vignetteSmoothness;
        public float[] lift;
        public float[] gamma;
        public float[] gain;
    }

    // IslandHaze: height fog under the ground level and a light edge haze; missing from the layout means no haze
    [Serializable]
    internal sealed class IsleHaze
    {
        public float[] color;
        public float startDepth;
        public float fullDepth;
        public float opacity;
        public float[] edgeColor;
        public float edgeIntensity;
        public float edgeStart;
        public float edgeFull;
        public float edgeTop;
    }
#pragma warning restore 0649

    /// <summary>
    /// The post-processing profile of the island look, shared by the colony island (ColonyVolume) and the battle
    /// arenas (their prefab's volume): grading, bloom, depth of field and <see cref="IslandHaze"/>.
    /// </summary>
    internal static class IslandLook
    {
        public const float FogStartPerDistance = .85f;
        public const float FogEndPerDistance = 3f;
        public const float DofStartPerDistance = 1.4f;
        public const float DofEndPerDistance = 2f;

        /// <summary>Creates or updates the profile asset at <paramref name="path"/> (its folder must exist).</summary>
        /// <param name="groundHeight">World height of the ground the haze depths count down from: the island's lawn,
        /// the arena's board.</param>
        /// <param name="fogColor">The linear fog's colour, the haze's colour when the layout gives none.</param>
        public static VolumeProfile EnsureVolumeProfile(string path, IslePost post, IsleHaze hazeData,
            float groundHeight, Color fogColor)
        {
            post ??= new IslePost();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            // the kit renders with AgX; URP has Neutral and ACES, Neutral keeps the palette's hues
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(post.exposure);
            color.saturation.Override(post.saturation);
            color.contrast.Override(post.contrast);
            Get<WhiteBalance>(profile).temperature.Override(post.temperature);
            var lgg = Get<LiftGammaGain>(profile);
            lgg.lift.Override(Vec4(post.lift, new Vector4(1f, 1f, 1f, 0f)));
            lgg.gamma.Override(Vec4(post.gamma, new Vector4(1f, 1f, 1f, 0f)));
            lgg.gain.Override(Vec4(post.gain, new Vector4(1f, 1f, 1f, 0f)));
            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(Positive(post.bloomThreshold, .9f));
            bloom.intensity.Override(post.bloomIntensity);
            bloom.scatter.Override(Positive(post.bloomScatter, .6f));
            var dof = Get<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(46f);            // IslandAtmosphere scales both with the camera distance
            dof.gaussianEnd.Override(66f);
            dof.gaussianMaxRadius.Override(Mathf.Clamp(Positive(post.dofMaxRadius, 1f), .5f, 1.5f));
            dof.highQualitySampling.Override(true);
            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(post.vignette);
            vignette.smoothness.Override(Positive(post.vignetteSmoothness, .45f));
            var haze = Get<IslandHaze>(profile);
            haze.fogColor.Override(Rgb(hazeData?.color, fogColor));
            float startDepth = hazeData != null ? Mathf.Max(0f, hazeData.startDepth) : 0f;
            haze.fogStart.Override(groundHeight - startDepth);
            haze.fogFull.Override(groundHeight - Mathf.Max(startDepth + .1f, hazeData?.fullDepth ?? 0f));
            haze.fogOpacity.Override(Mathf.Clamp01(hazeData?.opacity ?? 0f));
            haze.edgeColor.Override(Rgb(hazeData?.edgeColor, Color.white));
            haze.edgeIntensity.Override(Mathf.Clamp01(hazeData?.edgeIntensity ?? 0f));
            float edgeStart = Mathf.Max(0f, hazeData?.edgeStart ?? .45f);
            haze.edgeRange.Override(new Vector2(edgeStart, Mathf.Max(edgeStart + .05f, hazeData?.edgeFull ?? 1.05f)));
            haze.edgeTop.Override(Mathf.Clamp01(hazeData?.edgeTop ?? 1f));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;   // as the profile editor does
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        public static float Positive(float value, float fallback) => value > 0f ? value : fallback;

        public static Color Rgb(float[] c, Color fallback) =>
            c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2], 1f) : fallback;

        private static Vector4 Vec4(float[] v, Vector4 fallback) =>
            v != null && v.Length >= 4 ? new Vector4(v[0], v[1], v[2], v[3]) : fallback;
    }
}
