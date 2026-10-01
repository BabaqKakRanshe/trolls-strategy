using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Writes the production chains and their prices into the content assets: goods and their sale prices,
    /// building prices, capacities, workers, recipes and upgrades. The numbers come from the balance pass in
    /// docs/economy-balance.md (summary in docs/GDD.md §5.3); edit them here or directly in the assets, then
    /// re-run to reset.
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
                // raw goods pay 3 a unit of work; each processing step pays about 30% more per worker-minute
                Resource(icons, ResourceKind.IronOre, "Руда", 3, "iron-ore"),
                Resource(icons, ResourceKind.IronIngot, "Слиток", 13, "iron-ingot"),
                Resource(icons, ResourceKind.Wheat, "Пшеница", 3, "wheat"),
                Resource(icons, ResourceKind.AnimalHide, "Шкура", 18, "animal-hide"),
                Resource(icons, ResourceKind.Leather, "Кожа", 32, "leather"),
                Resource(icons, ResourceKind.Logs, "Брёвна", 3, "logs"),
                Resource(icons, ResourceKind.Planks, "Доски", 8, "planks"),
                Resource(icons, ResourceKind.IronSword, "Железный меч", 45, "iron-sword", "iron-sword"),
                Resource(icons, ResourceKind.IronArmor, "Усиленная броня", 90, "iron-armor", "iron-armor"),
                Resource(icons, ResourceKind.WoodenShield, "Деревянный щит", 40, "wooden-shield", "wooden-shield"),
                Resource(icons, ResourceKind.VioletCrystal, "Кристалл", 30, "violet-crystal"),
                Resource(icons, ResourceKind.EnchantedSword, "Зачарованный меч", 130, "enchanted-sword", "enchanted-sword")
            });
            EditorUtility.SetDirty(catalog);

            // Raw producers stay cheap, so another copy is always a sensible way to grow. A processing building
            // pays back in about 7 minutes against putting the same goblins on raw work.
            Producer(BuildingKind.Mine, 200, 100, 5,
                Recipe(1f, null, Out(ResourceKind.IronOre, 1), 25, new ResourceAmount(ResourceKind.VioletCrystal, 1)));
            Upgrades(BuildingKind.Mine, new[] { 600, 900, 1300 }, capacityPerLevel: 50, workersPerLevel: 3, saleBonusPerLevel: 0);
            Storage(BuildingKind.Warehouse, StorageRole.Stockpile, RawAndIntermediateGoods);
            Storage(BuildingKind.Market, StorageRole.Market);
            Upgrades(BuildingKind.Market, new[] { 1000, 2000, 3000 }, capacityPerLevel: 0, workersPerLevel: 0, saleBonusPerLevel: 1);
            Storage(BuildingKind.Armory, StorageRole.Armory);
            Price(BuildingKind.Armory, 600);
            // the tutorial's last building before the first battle: about a minute and a half of income by then
            Price(BuildingKind.Barracks, 300);
            Producer(BuildingKind.Smeltery, 700, 50, 4,
                Recipe(2f, In(ResourceKind.IronOre, 2), Out(ResourceKind.IronIngot, 1)));
            Producer(BuildingKind.Forge, 1500, 20, 4,
                Recipe(5f, In(ResourceKind.IronIngot, 2, ResourceKind.Leather, 1), Out(ResourceKind.IronArmor, 1)),
                Recipe(4f, In(ResourceKind.IronIngot, 2), Out(ResourceKind.IronSword, 1)));
            Producer(BuildingKind.Field, 250, 100, 5,
                Recipe(10f, null, Out(ResourceKind.Wheat, 10)));
            Producer(BuildingKind.Farm, 600, 50, 4,
                Recipe(3f, In(ResourceKind.Wheat, 3), Out(ResourceKind.AnimalHide, 1)));
            // three units of work, like the farm: one farm keeps one tannery busy
            Producer(BuildingKind.Tannery, 1400, 50, 4,
                Recipe(3f, In(ResourceKind.AnimalHide, 1), Out(ResourceKind.Leather, 1)));
            Producer(BuildingKind.LumberCamp, 400, 100, 5,
                Recipe(1f, null, Out(ResourceKind.Logs, 1)));
            Producer(BuildingKind.LumberMill, 1000, 50, 4,
                Recipe(1f, In(ResourceKind.Logs, 1), Out(ResourceKind.Planks, 1)));
            Producer(BuildingKind.ShieldWorkshop, 1200, 20, 4,
                Recipe(4f, In(ResourceKind.Planks, 3), Out(ResourceKind.WoodenShield, 1)));
            Producer(BuildingKind.Enchanter, 3000, 20, 3,
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

        private static void Producer(BuildingKind kind, int price, int capacity, int workers,
            params ProductionRecipe[] recipes)
        {
            var definition = LoadBuilding(kind);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_price").intValue = price;
            serialized.FindProperty("_capacity").intValue = capacity;
            serialized.FindProperty("_maxWorkers").intValue = workers;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            definition.SetRecipes(recipes);
            definition.SetStorage(StorageRole.None);
            EditorUtility.SetDirty(definition);
        }

        private static void Price(BuildingKind kind, int price)
        {
            var definition = LoadBuilding(kind);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_price").intValue = price;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void Upgrades(BuildingKind kind, int[] costs, int capacityPerLevel, int workersPerLevel,
            int saleBonusPerLevel)
        {
            var definition = LoadBuilding(kind);
            definition.SetUpgrades(costs, capacityPerLevel, workersPerLevel, saleBonusPerLevel);
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
