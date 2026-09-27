using System;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Copies what a building prefab authors for walking units (the EntranceAnchor and the crowd spacing
    /// on BuildingModel) into its BuildingDefinition, which stays the only source the simulation reads.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildingEntranceBaker
    {
        public const string AnchorName = "EntranceAnchor";
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        static BuildingEntranceBaker()
        {
            PrefabStage.prefabSaved -= OnPrefabSaved;
            PrefabStage.prefabSaved += OnPrefabSaved;
        }

        [MenuItem("TrollStrategy/Bake Building Entrances")]
        public static void BakeAll()
        {
            var catalog = LoadCatalog();
            foreach (var definition in catalog.Buildings)
                if (definition != null && definition.Prefab != null)
                    Bake(definition, definition.Prefab, catalog.Economy.CellSize);
            AssetDatabase.SaveAssets();
        }

        private static void OnPrefabSaved(GameObject root)
        {
            var stage = PrefabStageUtility.GetPrefabStage(root);
            if (stage == null) return;
            var catalog = LoadCatalog();
            foreach (var definition in catalog.Buildings)
            {
                if (definition == null || definition.Prefab == null ||
                    AssetDatabase.GetAssetPath(definition.Prefab) != stage.assetPath) continue;
                Bake(definition, root, catalog.Economy.CellSize);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
        }

        /// <summary>Entrance in cells from the footprint's south-west corner.</summary>
        public static Vector2 ReadEntrance(GameObject prefabRoot, BuildingDefinition definition, float cellSize)
        {
            var anchor = prefabRoot.transform.Find("Model/" + AnchorName);
            if (anchor == null)
                throw new InvalidOperationException($"{prefabRoot.name} has no Model/{AnchorName}");
            // The prefab root sits at the footprint centre; its local XY plane is the map plane.
            var local = prefabRoot.transform.InverseTransformPoint(anchor.position);
            return new Vector2(
                Round(definition.Width * 0.5f + local.x / cellSize),
                Round(definition.Height * 0.5f + local.y / cellSize));
        }

        private static void Bake(BuildingDefinition definition, GameObject prefabRoot, float cellSize)
        {
            try
            {
                var entrance = ReadEntrance(prefabRoot, definition, cellSize);
                var model = prefabRoot.GetComponentInChildren<BuildingModel>(true)
                    ?? throw new InvalidOperationException($"{prefabRoot.name} has no BuildingModel");
                float spacing = Round(model.CrowdSpacingCells);
                if (Mathf.Approximately(entrance.x, definition.EntranceX) &&
                    Mathf.Approximately(entrance.y, definition.EntranceY) &&
                    Mathf.Approximately(spacing, definition.CrowdSpacingCells)) return;
                Undo.RecordObject(definition, "Bake building entrance");
                definition.SetEntrance(entrance);
                definition.SetCrowdSpacing(spacing);
                EditorUtility.SetDirty(definition);
                Debug.Log($"[BuildingEntranceBaker] {definition.Kind} entrance = {entrance}, crowd spacing = {spacing}", definition);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
            {
                Debug.LogError($"[BuildingEntranceBaker] {exception.Message}", definition);
            }
        }

        private static GameContentCatalog LoadCatalog() =>
            AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
            ?? throw new InvalidOperationException("Missing content catalog: " + CatalogPath);

        private static float Round(float value) => Mathf.Round(value * 1000f) / 1000f;
    }
}
