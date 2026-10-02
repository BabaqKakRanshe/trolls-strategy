using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>Rewrites every authored content asset in dependency order: creatures, production, arena, quests.</summary>
    public static class ContentSetupAll
    {
        [MenuItem("TrollStrategy/Dev/Setup All Content")]
        public static void Apply()
        {
            CreatureSetup.Apply();
            ProductionContentSetup.Apply();
            ArenaContentSetup.Apply();
            ProgressionContentSetup.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentSetupAll] Content rewritten.");
        }

        /// <summary>Icon atlases, content, the models of the newer buildings, the graphics levels and the HUD prefab, in that order.</summary>
        [MenuItem("TrollStrategy/Dev/Setup Everything (atlases, content, models, UI)")]
        public static void ApplyEverything()
        {
            ResourceAtlasImporter.Import();
            Apply();
            BuildingModelSetup.Apply();
            GraphicsQualitySetup.Apply();
            UiSetup.SetupColonyScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentSetupAll] Everything rewritten.");
        }
    }
}
