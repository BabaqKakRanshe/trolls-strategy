using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Writes the production chains and their prices into the content assets: goods with their sale prices,
    /// icons and kit models,
    /// building prices, capacities, workers, recipes with their by-products, building levels (three for every
    /// building, one for each kit model) and the colony upgrades of the haulers' guild, the barracks and the armory,
    /// whose deeper levels open with the level of their building. The numbers come from the balance passes in
    /// docs/economy-balance.md (summary in docs/GDD.md §5.3); edit them here or directly in the assets, then
    /// re-run to reset.
    /// </summary>
    public static class ProductionContentSetup
    {
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string CatalogPath = DefinitionFolder + "GameContentCatalog.asset";
        private const string ResourceAtlasPath = "Assets/Game/Art/Sprites/Atlases/resources-icons-0.png";
        private const string ResourceModelFolder = "Assets/Vitaria/Models/Resources/";

        private static readonly ResourceKind[] RawAndIntermediateGoods =
        {
            ResourceKind.IronOre, ResourceKind.IronIngot, ResourceKind.Wheat, ResourceKind.AnimalHide,
            ResourceKind.Leather, ResourceKind.Logs, ResourceKind.Planks, ResourceKind.VioletCrystal,
            ResourceKind.Coal, ResourceKind.GoldNugget, ResourceKind.Straw, ResourceKind.GoldenWheat,
            ResourceKind.Meat, ResourceKind.Milk, ResourceKind.Scrap
        };

        [MenuItem("TrollStrategy/Dev/Setup Production Content")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("Missing " + CatalogPath);
            var icons = AssetDatabase.LoadAllAssetsAtPath(ResourceAtlasPath).OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

            // Buildings added after the first kit: created here when missing, with a stand-in model until
            // their own prefab is integrated (BuildingKitSetup or the prefab field).
            EnsureBuilding(catalog, BuildingKind.Tavern, "Таверна", 3, 3, BuildingKind.Farm);
            EnsureBuilding(catalog, BuildingKind.HaulersGuild, "Гильдия носильщиков", 3, 3, BuildingKind.Warehouse);

            EnsureEquipment(catalog, "iron-sword", "Железный меч", EquipmentSlot.Weapon, 2, 0);
            EnsureEquipment(catalog, "iron-armor", "Усиленная броня", EquipmentSlot.Armor, 0, 3);
            EnsureEquipment(catalog, "wooden-shield", "Деревянный щит", EquipmentSlot.Armor, 0, 2);
            EnsureEquipment(catalog, "enchanted-sword", "Зачарованный меч", EquipmentSlot.Weapon, 4, 0);
            EnsureEquipment(catalog, "battle-axe", "Боевой топор", EquipmentSlot.Weapon, 3, 1);

            catalog.SetResources(new[]
            {
                // raw goods pay 3 a unit of work; each processing step pays about 30% more per worker-minute
                Resource(icons, ResourceKind.IronOre, "Руда", 3, "iron-ore", "Res_Ore_Iron", "Res_OrePile_Iron"),
                Resource(icons, ResourceKind.IronIngot, "Слиток", 13, "iron-ingot", "Res_Ingot_Iron", "Res_IngotStack_Iron"),
                Resource(icons, ResourceKind.Wheat, "Пшеница", 3, "wheat", "Res_Wheat"),
                // the farm also gives meat and milk since 2026-10-02, so the hide alone is worth less
                Resource(icons, ResourceKind.AnimalHide, "Шкура", 14, "animal-hide", "Res_Hide"),
                Resource(icons, ResourceKind.Leather, "Кожа", 32, "leather", "Res_Leather"),
                Resource(icons, ResourceKind.Logs, "Брёвна", 3, "logs", "Res_Log", "Res_LogPile"),
                Resource(icons, ResourceKind.Planks, "Доски", 8, "planks", "Res_Planks"),
                Resource(icons, ResourceKind.IronSword, "Железный меч", 45, "iron-sword", "Res_Sword_Iron", equipmentId: "iron-sword"),
                Resource(icons, ResourceKind.IronArmor, "Усиленная броня", 90, "iron-armor", "Res_Armor_Iron", equipmentId: "iron-armor"),
                Resource(icons, ResourceKind.WoodenShield, "Деревянный щит", 40, "wooden-shield", "Res_Shield_Wood", equipmentId: "wooden-shield"),
                Resource(icons, ResourceKind.VioletCrystal, "Кристалл", 30, "violet-crystal", "Res_Crystal"),
                Resource(icons, ResourceKind.EnchantedSword, "Зачарованный меч", 130, "enchanted-sword", "Res_Sword_Enchanted", equipmentId: "enchanted-sword"),
                Resource(icons, ResourceKind.Coal, "Уголь", 4, "coal", "Res_Coal"),
                Resource(icons, ResourceKind.GoldNugget, "Самородок", 60, "gold-nugget", "Res_GoldNugget"),
                Resource(icons, ResourceKind.Straw, "Солома", 1, "straw", "Res_Straw"),
                Resource(icons, ResourceKind.GoldenWheat, "Золотой колос", 25, "golden-wheat", "Res_GoldenWheat"),
                Resource(icons, ResourceKind.Meat, "Мясо", 8, "meat", "Res_Meat"),
                Resource(icons, ResourceKind.Milk, "Молоко", 6, "milk", "Res_Milk"),
                Resource(icons, ResourceKind.Scrap, "Лом", 5, "scrap", "Res_Scrap"),
                Resource(icons, ResourceKind.Feast, "Пир", 50, "feast", "Res_Feast"),
                Resource(icons, ResourceKind.BattleAxe, "Боевой топор", 85, "battle-axe", "Res_BattleAxe", equipmentId: "battle-axe")
            });
            EditorUtility.SetDirty(catalog);

            // Loading takes two economy steps and unloading one, so the guild's quick hands have time to save;
            // three haulers load at a door until the guild widens it.
            catalog.Economy.SetHauling(0.5f, 0.25f, 3);
            EditorUtility.SetDirty(catalog.Economy);

            // Raw producers stay cheap, so another copy is always a sensible way to grow. A processing building
            // pays back in about 7 minutes against putting the same goblins on raw work. Capacities are small
            // since 2026-10-02: a buffer fills up and asks for carriers. Every producer has levels: more places,
            // more room, and from level 2 or 3 a new recipe or by-product. Since 2026-10-02 (evening) every building
            // has three levels, one for each model of the kit: the fourth levels went, the rest got a third.
            Producer(BuildingKind.Mine, 200, 50, 5,
                Recipe(1f, null, Out(ResourceKind.IronOre, 1),
                    Extra(ResourceKind.VioletCrystal, 1, 4),
                    Extra(ResourceKind.Coal, 1, 20, 2),
                    Extra(ResourceKind.GoldNugget, 1, 2, 3)));
            Upgrades(BuildingKind.Mine, new[] { 600, 900 }, capacityPerLevel: 25, workersPerLevel: 3, saleBonusPerLevel: 0);
            Storage(BuildingKind.Warehouse, StorageRole.Stockpile, RawAndIntermediateGoods);
            Upgrades(BuildingKind.Warehouse, new[] { 120, 200 }, capacityPerLevel: 250, workersPerLevel: 0, saleBonusPerLevel: 0);
            Storage(BuildingKind.Market, StorageRole.Market);
            Upgrades(BuildingKind.Market, new[] { 1000, 2000 }, capacityPerLevel: 0, workersPerLevel: 0, saleBonusPerLevel: 1);
            // the armory's level opens the deeper drill of the colony's fighters (Hosted below)
            Storage(BuildingKind.Armory, StorageRole.Armory);
            Price(BuildingKind.Armory, 600);
            Upgrades(BuildingKind.Armory, new[] { 800, 1800 }, 0, 0, 0);
            // the tutorial's last building before the first battle: about a minute and a half of income by then;
            // its upgrades (squad, health, rest, glory) are bought inside it, the deeper ones with its level
            Storage(BuildingKind.Barracks, StorageRole.None);
            Price(BuildingKind.Barracks, 300);
            Upgrades(BuildingKind.Barracks, new[] { 800, 2000 }, 0, 0, 0);
            Producer(BuildingKind.Smeltery, 700, 25, 4,
                Recipe(3f, In(ResourceKind.IronOre, 2, ResourceKind.Coal, 1), Out(ResourceKind.IronIngot, 2)),
                Recipe(2f, In(ResourceKind.IronOre, 2), Out(ResourceKind.IronIngot, 1)),
                Recipe(1f, In(ResourceKind.Scrap, 2), Out(ResourceKind.IronIngot, 1), minLevel: 2));
            Upgrades(BuildingKind.Smeltery, new[] { 900, 1500 }, 15, 2, 0);
            Producer(BuildingKind.Forge, 1500, 10, 4,
                Spoils(Recipe(5f, In(ResourceKind.IronIngot, 2, ResourceKind.Leather, 1), Out(ResourceKind.IronArmor, 1)), 8),
                Spoils(Recipe(6f, In(ResourceKind.IronIngot, 1, ResourceKind.Planks, 1, ResourceKind.Leather, 1),
                    Out(ResourceKind.BattleAxe, 1), minLevel: 2), 8),
                Spoils(Recipe(4f, In(ResourceKind.IronIngot, 2), Out(ResourceKind.IronSword, 1)), 8));
            Upgrades(BuildingKind.Forge, new[] { 1500, 2500 }, 5, 2, 0);
            // the field harvests in one batch, straw with the grain; a golden ear now and then from level 2
            Producer(BuildingKind.Field, 250, 50, 5,
                Recipe(10f, null, Out(ResourceKind.Wheat, 10, ResourceKind.Straw, 3),
                    Extra(ResourceKind.GoldenWheat, 1, 15, 2)));
            Upgrades(BuildingKind.Field, new[] { 400, 800 }, 25, 2, 0);
            // grain and straw feed the herd: hides for the tannery, meat and milk for the tavern
            Producer(BuildingKind.Farm, 600, 25, 4,
                Recipe(3f, In(ResourceKind.Wheat, 3, ResourceKind.Straw, 1),
                    Out(ResourceKind.AnimalHide, 1, ResourceKind.Meat, 1),
                    Extra(ResourceKind.Milk, 1, 50),
                    Extra(ResourceKind.Milk, 1, 50, 2)));
            Upgrades(BuildingKind.Farm, new[] { 800, 1400 }, 15, 2, 0);
            // three units of work, like the farm: one farm keeps one tannery busy
            Producer(BuildingKind.Tannery, 1400, 25, 4,
                Recipe(3f, In(ResourceKind.AnimalHide, 1), Out(ResourceKind.Leather, 1)));
            Upgrades(BuildingKind.Tannery, new[] { 1200, 2000 }, 15, 2, 0);
            Producer(BuildingKind.LumberCamp, 400, 50, 5,
                Recipe(1f, null, Out(ResourceKind.Logs, 1)));
            Upgrades(BuildingKind.LumberCamp, new[] { 500, 900 }, 25, 2, 0);
            Producer(BuildingKind.LumberMill, 1000, 25, 4,
                Recipe(1f, In(ResourceKind.Logs, 1), Out(ResourceKind.Planks, 1)));
            Upgrades(BuildingKind.LumberMill, new[] { 900, 1600 }, 15, 2, 0);
            Producer(BuildingKind.ShieldWorkshop, 1200, 10, 4,
                Recipe(4f, In(ResourceKind.Planks, 3), Out(ResourceKind.WoodenShield, 1)));
            Upgrades(BuildingKind.ShieldWorkshop, new[] { 1200, 2000 }, 5, 2, 0);
            // a steadier hand from level 2: the same enchantment spoils a third as often
            Producer(BuildingKind.Enchanter, 3000, 10, 3,
                Spoils(Recipe(6f, In(ResourceKind.IronSword, 1, ResourceKind.VioletCrystal, 1),
                    Out(ResourceKind.EnchantedSword, 1), minLevel: 2), 5),
                Spoils(Recipe(6f, In(ResourceKind.IronSword, 1, ResourceKind.VioletCrystal, 1),
                    Out(ResourceKind.EnchantedSword, 1)), 15));
            Upgrades(BuildingKind.Enchanter, new[] { 2500, 4000 }, 5, 1, 0);
            // four goods in, one feast out; a golden ear makes a double feast from level 2
            Producer(BuildingKind.Tavern, 2000, 15, 4,
                Recipe(6f, In(ResourceKind.GoldenWheat, 1, ResourceKind.Meat, 1, ResourceKind.Milk, 1, ResourceKind.Logs, 1),
                    Out(ResourceKind.Feast, 2), minLevel: 2),
                Recipe(6f, In(ResourceKind.Wheat, 2, ResourceKind.Meat, 1, ResourceKind.Milk, 1, ResourceKind.Logs, 1),
                    Out(ResourceKind.Feast, 1)));
            Upgrades(BuildingKind.Tavern, new[] { 2000, 3000 }, 10, 2, 0);
            Storage(BuildingKind.HaulersGuild, StorageRole.None);
            Price(BuildingKind.HaulersGuild, 1200);
            Upgrades(BuildingKind.HaulersGuild, new[] { 1000, 2500 }, 0, 0, 0);

            // a host building's level opens the levels of its upgrades a third at a time (Hosted)
            catalog.SetUpgrades(new[]
            {
                // the haulers' guild: every carrier of the colony at once
                Hosted("guild-step", "Лёгкий шаг",
                    "Все существа ходят по колонии на 10% быстрее за уровень.", BuildingKind.HaulersGuild,
                    UpgradeEffect.WalkSpeedPercent, 10, new[] { 300, 500, 800, 1200, 1800 }),
                Hosted("guild-backs", "Крепкие спины",
                    "Носильщики поднимают больше: +25% к выносливости за уровень, то есть лишняя единица груза каждые четыре ходки.",
                    BuildingKind.HaulersGuild, UpgradeEffect.CarryPercent, 25, new[] { 400, 700, 1100, 1600 }),
                Hosted("guild-hands", "Быстрые руки",
                    "Погрузка и выгрузка идут на 20% быстрее за уровень: меньше ожидания у дверей.",
                    BuildingKind.HaulersGuild, UpgradeEffect.HandlingTimePercent, 20, new[] { 300, 600, 1000 }),
                Hosted("guild-doors", "Широкие двери",
                    "У двери каждого здания грузится на одного носильщика больше: очередь тает быстрее.",
                    BuildingKind.HaulersGuild, UpgradeEffect.LoadersPerDoor, 1, new[] { 250, 450, 700, 1000 }),
                // the barracks: the squad that goes to the arena
                Hosted("barracks-squad", "Больше бойцов",
                    "В бой идёт на одного бойца больше.", BuildingKind.Barracks,
                    UpgradeEffect.SquadSize, 1, new[] { 800, 1600, 3000 }),
                Hosted("barracks-health", "Закалка",
                    "Бойцы поселения выносливее: +15% здоровья за уровень.", BuildingKind.Barracks,
                    UpgradeEffect.FighterHealthPercent, 15, new[] { 500, 900, 1400, 2000 }),
                Hosted("barracks-rest", "Отдых",
                    "Арена восстанавливается после боя на 20% быстрее за уровень.", BuildingKind.Barracks,
                    UpgradeEffect.BattleCooldownPercent, 20, new[] { 400, 800, 1200 }),
                Hosted("barracks-glory", "Слава арены",
                    "Победы на арене приносят на 15% больше золота за уровень.", BuildingKind.Barracks,
                    UpgradeEffect.BattleRewardPercent, 15, new[] { 700, 1400, 2400 }),
                // the armory: the drill with the colony's weapons, moved here from the barracks on 2026-10-02
                Hosted("armory-drill", "Оружейная выучка",
                    "Каждый удар бойца поселения наносит на 1 урона больше за уровень.", BuildingKind.Armory,
                    UpgradeEffect.FighterDamage, 1, new[] { 600, 1200, 2000 })
            });
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            Debug.Log("[ProductionContentSetup] Production chains written to content assets.");
        }

        private static ResourceDefinition Resource(Dictionary<string, Sprite> icons, ResourceKind kind, string name,
            int price, string iconName, string model, string pile = null, string equipmentId = null)
        {
            // a good added after the atlas was last packed shows no picture until its frame is imported
            if (!icons.TryGetValue(iconName, out var icon))
                Debug.LogWarning("[ProductionContentSetup] No resources-icons frame yet: " + iconName);
            return new ResourceDefinition(kind, name, price, icon, equipmentId, KitModel(model),
                pile != null ? KitModel(pile) : null);
        }

        // The kit's own FBX, found by name like the icon by its frame; the kit's import profile gives it the palette.
        private static GameObject KitModel(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceModelFolder + name + ".fbx");
            if (model == null)
                Debug.LogWarning("[ProductionContentSetup] No Vitaria model yet: " + name);
            return model;
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

        // An upgrade whose levels open a third at a time with its host's level, rounding up: five levels open two
        // at level 1, four at level 2 and all at level 3; three open one by one.
        private static UpgradeDefinition Hosted(string id, string name, string description, BuildingKind host,
            UpgradeEffect effect, int amountPerLevel, int[] costs)
        {
            int hostLevels = LoadBuilding(host).MaxLevel;
            var needs = new int[costs.Length];
            for (int i = 0; i < costs.Length; i++)
            {
                int level = 1;
                while (level < hostLevels && (costs.Length * level + hostLevels - 1) / hostLevels < i + 1) level++;
                needs[i] = level;
            }
            return new UpgradeDefinition(id, name, description, host, effect, amountPerLevel, costs, needs);
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

        // Creates a building definition that is not in the catalog yet, with its own variant of BuildingBase
        // (Prefabs/Buildings/<Kind>.prefab). Until its own model is integrated (BuildingModelSetup) the variant is
        // a copy of a look-alike's and borrows its catalog icon, so the building can be placed and played.
        private static void EnsureBuilding(GameContentCatalog catalog, BuildingKind kind, string name, int width,
            int height, BuildingKind standIn)
        {
            string path = DefinitionFolder + "Building_" + kind + ".asset";
            string prefabPath = "Assets/Game/Prefabs/Buildings/" + kind + ".prefab";
            var model = LoadBuilding(standIn);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null &&
                !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(model.Prefab), prefabPath))
                throw new InvalidOperationException("Could not copy a stand-in prefab to " + prefabPath);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var view = root.GetComponent<TrollStrategy.Presentation.Buildings.BuildingView>();
                var viewSerialized = new SerializedObject(view);
                if (viewSerialized.FindProperty("_kind").intValue != (int)kind)
                {
                    viewSerialized.FindProperty("_kind").intValue = (int)kind;
                    viewSerialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingDefinition>();
                definition.Init(kind, name, 0, width, height, 0, 0, null);
                definition.SetConstructible(true);
                AssetDatabase.CreateAsset(definition, path);
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_icon").objectReferenceValue = model.Icon;
                serialized.FindProperty("_entrance").vector2Value = new Vector2(width * 0.5f, model.EntranceY);
                serialized.FindProperty("_crowdSpacing").floatValue = model.CrowdSpacingCells;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var prefabField = new SerializedObject(definition);
            prefabField.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            prefabField.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            if (catalog.Buildings.Contains(definition)) return;
            var catalogSerialized = new SerializedObject(catalog);
            var list = catalogSerialized.FindProperty("_buildings");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
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
            // the enchanter's work: every enchanted item is named so (enchanted-sword, later enchanted-*)
            definition.SetEnchanted(id.StartsWith("enchanted-", StringComparison.Ordinal));
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
            params RecipeExtra[] extras) =>
            new(work, inputs, outputs, 1, extras);

        private static ProductionRecipe Recipe(float work, ResourceAmount[] inputs, ResourceAmount[] outputs,
            int minLevel) =>
            new(work, inputs, outputs, minLevel);

        // The same recipe, spoiled now and then into a piece of scrap the smeltery can melt again.
        private static ProductionRecipe Spoils(ProductionRecipe recipe, int percent) =>
            new(recipe.Work, recipe.Inputs, recipe.Outputs, recipe.MinLevel, recipe.Extras, percent,
                Out(ResourceKind.Scrap, 1));

        private static RecipeExtra Extra(ResourceKind resource, int amount, int percent, int minLevel = 1) =>
            new(new ResourceAmount(resource, amount), percent, minLevel);

        private static ResourceAmount[] Out(ResourceKind resource, int amount) => new[] { new ResourceAmount(resource, amount) };

        private static ResourceAmount[] Out(ResourceKind first, int firstAmount, ResourceKind second, int secondAmount) =>
            new[] { new ResourceAmount(first, firstAmount), new ResourceAmount(second, secondAmount) };

        private static ResourceAmount[] In(ResourceKind resource, int amount) => new[] { new ResourceAmount(resource, amount) };

        private static ResourceAmount[] In(params object[] pairs)
        {
            var amounts = new ResourceAmount[pairs.Length / 2];
            for (int i = 0; i < amounts.Length; i++)
                amounts[i] = new ResourceAmount((ResourceKind)pairs[i * 2], (int)pairs[i * 2 + 1]);
            return amounts;
        }
    }
}
