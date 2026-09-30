using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Editor.Setup
{
    // Screen-space contact shadows of the colony renderer. The procedural ground textures that used to be made here
    // (Meadow, PathGrain, WoodGrain, StoneGrain on the Primitive_* materials) went away with PrimitiveEnvironment:
    // the colony ground is now the Vitaria kit's Colony_Meadow (ColonyEnvironmentBuilder).
    public static class DioramaSurfaceSetup
    {
        public const string RendererPath = "Assets/Settings/Colony3DRenderer.asset";

        public static void ConfigureContactShadows() =>
            ConfigureContactShadows(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath));

        public static void ConfigureContactShadows(UniversalRendererData renderer)
        {
            if (renderer == null) return;
            const string featureName = "Diorama Contact Shadows";
            var feature = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.name == featureName);
            if (feature == null)
            {
                // URP's built-in feature type is internal; configure its serialized settings just as its Inspector does.
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", true);
                feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var so = new SerializedObject(feature);
            var settings = so.FindProperty("m_Settings");
            settings.FindPropertyRelative("AfterOpaque").boolValue = false;
            settings.FindPropertyRelative("Downsample").boolValue = true;
            settings.FindPropertyRelative("AOMethod").intValue = 1;
            settings.FindPropertyRelative("Source").intValue = 1;
            settings.FindPropertyRelative("Intensity").floatValue = .8f;
            settings.FindPropertyRelative("DirectLightingStrength").floatValue = .18f;
            settings.FindPropertyRelative("Radius").floatValue = .25f;
            settings.FindPropertyRelative("Falloff").floatValue = 55f;
            so.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            feature.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }
    }
}
