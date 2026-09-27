using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Writes the production chains into the content assets. The numbers are draft balance values
    /// (docs/GDD.md §5.3); edit them here or directly in the assets, then re-run to reset.
    /// </summary>
    public static class ProductionContentSetup
    {
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string CatalogPath = DefinitionFolder + "GameContentCatalog.asset";
        private const string ResourceAtlasPath = "Assets/Game/Art/Sprites/Atlases/resources-icons-0.png";

        private static readonly ResourceKind[] RawAndIntermediateGoods =
        {
            ResourceKind.IronOre, ResourceKind.IronIngot, ResourceKind.Wheat, ResourceKind.AnimalHide,
            ResourceKind.Leather, ResourceKind.Logs, ResourceKind.Planks, ResourceKind.VioletCrystal
        };

        [MenuItem("TrollStrategy/Setup Production Content")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("Missing " + CatalogPath);
            var icons = AssetDatabase.LoadAllAssetsAtPath(ResourceAtlasPath).OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

            EnsureEquipment(catalog, "iron-sword", "Железный меч", EquipmentSlot.Weapon, 2, 0);
            EnsureEquipment(catalog, "iron-armor", "Усиленная броня", EquipmentSlot.Armor, 0, 3);
            EnsureEquipment(catalog, "wooden-shield", "Деревянный щит", EquipmentSlot.Armor, 0, 2);
            EnsureEquipment(catalog, "enchanted-sword", "Зачарованный меч", EquipmentSlot.Weapon, 4, 0);

            catalog.SetResources(new[]
            {
                Resource(icons, ResourceKind.IronOre, "Руда", 3, "iron-ore"),
                Resource(icons, ResourceKind.IronIngot, "Слиток", 9, "iron-ingot"),
                Resource(icons, ResourceKind.Wheat, "Пшеница", 2, "wheat"),
                Resource(icons, ResourceKind.AnimalHide, "Шкура", 9, "animal-hide"),
                Resource(icons, ResourceKind.Leather, "Кожа", 14, "leather"),
                Resource(icons, ResourceKind.Logs, "Брёвна", 2, "logs"),
                Resource(icons, ResourceKind.Planks, "Доски", 4, "planks"),
                Resource(icons, ResourceKind.IronSword, "Железный меч", 26, "iron-sword", "iron-sword"),
                Resource(icons, ResourceKind.IronArmor, "Усиленная броня", 45, "iron-armor", "iron-armor"),
                Resource(icons, ResourceKind.WoodenShield, "Деревянный щит", 18, "wooden-shield", "wooden-shield"),
                Resource(icons, ResourceKind.VioletCrystal, "Кристалл", 30, "violet-crystal"),
                Resource(icons, ResourceKind.EnchantedSword, "Зачарованный меч", 90, "enchanted-sword", "enchanted-sword")
            });
            EditorUtility.SetDirty(catalog);

            Producer(BuildingKind.Mine, 100, 5,
                Recipe(1f, null, Out(ResourceKind.IronOre, 1), 25, new ResourceAmount(ResourceKind.VioletCrystal, 1)));
            Storage(BuildingKind.Warehouse, StorageRole.Stockpile, RawAndIntermediateGoods);
            Storage(BuildingKind.Market, StorageRole.Market);
            Storage(BuildingKind.Armory, StorageRole.Armory);
            Producer(BuildingKind.Smeltery, 50, 4,
                Recipe(2f, In(ResourceKind.IronOre, 2), Out(ResourceKind.IronIngot, 1)));
            Producer(BuildingKind.Forge, 20, 4,
                Recipe(5f, In(ResourceKind.IronIngot, 2, ResourceKind.Leather, 1), Out(ResourceKind.IronArmor, 1)),
                Recipe(4f, In(ResourceKind.IronIngot, 2), Out(ResourceKind.IronSword, 1)));
            Producer(BuildingKind.Field, 100, 5,
                Recipe(1f, null, Out(ResourceKind.Wheat, 1)));
            Producer(BuildingKind.Farm, 50, 4,
                Recipe(3f, In(ResourceKind.Wheat, 3), Out(ResourceKind.AnimalHide, 1)));
            Producer(BuildingKind.Tannery, 50, 4,
                Recipe(2f, In(ResourceKind.AnimalHide, 1), Out(ResourceKind.Leather, 1)));
            Producer(BuildingKind.LumberCamp, 100, 5,
                Recipe(1f, null, Out(ResourceKind.Logs, 1)));
            Producer(BuildingKind.LumberMill, 50, 4,
                Recipe(1f, In(ResourceKind.Logs, 1), Out(ResourceKind.Planks, 1)));
            Producer(BuildingKind.ShieldWorkshop, 20, 4,
                Recipe(4f, In(ResourceKind.Planks, 3), Out(ResourceKind.WoodenShield, 1)));
            Producer(BuildingKind.Enchanter, 20, 3,
                Recipe(6f, In(ResourceKind.IronSword, 1, ResourceKind.VioletCrystal, 1), Out(ResourceKind.EnchantedSword, 1)));

            AssetDatabase.SaveAssets();
            Debug.Log("[ProductionContentSetup] Production chains written to content assets.");
        }

        private static ResourceDefinition Resource(Dictionary<string, Sprite> icons, ResourceKind kind, string name,
            int price, string iconName, string equipmentId = null)
        {
            if (!icons.TryGetValue(iconName, out var icon))
                throw new InvalidOperationException("Missing resource icon sprite: " + iconName);
            return new ResourceDefinition(kind, name, price, icon, equipmentId);
        }

        private static void Producer(BuildingKind kind, int capacity, int workers, params ProductionRecipe[] recipes)
        {
            var definition = LoadBuilding(kind);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_capacity").intValue = capacity;
            serialized.FindProperty("_maxWorkers").intValue = workers;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            definition.SetRecipes(recipes);
            definition.SetStorage(StorageRole.None);
            EditorUtility.SetDirty(definition);
        }

        private static void Storage(BuildingKind kind, StorageRole role, params ResourceKind[] stored)
        {
            var definition = LoadBuilding(kind);
            definition.SetRecipes();
            definition.SetStorage(role, stored);
            EditorUtility.SetDirty(definition);
        }

        private static BuildingDefinition LoadBuilding(BuildingKind kind)
        {
            string path = DefinitionFolder + "Building_" + kind + ".asset";
            return AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path)
                ?? throw new InvalidOperationException("Missing building definition: " + path);
        }

        private static void EnsureEquipment(GameContentCatalog catalog, string id, string name, EquipmentSlot slot,
            int damage, int armor)
        {
            string path = DefinitionFolder + "Equipment_" + ToPascal(id) + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Init(id, name, slot, damage, armor, 0);
            EditorUtility.SetDirty(definition);

            if (catalog.Equipment.Contains(definition)) return;
            var serialized = new SerializedObject(catalog);
            var list = serialized.FindProperty("_equipment");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ToPascal(string id) =>
            string.Concat(id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

        private static ProductionRecipe Recipe(float work, ResourceAmount[] inputs, ResourceAmount[] outputs,
            int bonusEveryCycles = 0, ResourceAmount bonus = default) =>
            new(work, inputs, outputs, bonusEveryCycles, bonus);

        private static ResourceAmount[] Out(ResourceKind resource, int amount) => new[] { new ResourceAmount(resource, amount) };

        private static ResourceAmount[] In(ResourceKind resource, int amount) => new[] { new ResourceAmount(resource, amount) };

        private static ResourceAmount[] In(ResourceKind first, int firstAmount, ResourceKind second, int secondAmount) =>
            new[] { new ResourceAmount(first, firstAmount), new ResourceAmount(second, secondAmount) };
    }
}
