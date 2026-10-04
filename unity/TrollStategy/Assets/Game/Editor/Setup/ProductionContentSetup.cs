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
            ResourceKind.Meat, ResourceKind.Milk, ResourceKind.Scrap, ResourceKind.Stone, ResourceKind.CopperOre,
            ResourceKind.CopperIngot, ResourceKind.GoldOre, ResourceKind.GoldIngot, ResourceKind.Steel, ResourceKind.Wool,
            ResourceKind.Cloth
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
            // the gear of the Vitaria kit 2026-10-02, each piece with its enchanted twin; the rusty sword and the
            // patched armour are starting gear (their assets keep the starting quantity) and the arena's trophies
            Gear(catalog, "steel-sword", "Стальной меч", "Зачарованный стальной меч", EquipmentSlot.Weapon, 4, 0);
            Gear(catalog, "spear", "Копьё", "Зачарованное копьё", EquipmentSlot.Weapon, 1, 1);
            Gear(catalog, "bow", "Лук", "Зачарованный лук", EquipmentSlot.Weapon, 3, 0);
            Gear(catalog, "war-hammer", "Боевой молот", "Зачарованный боевой молот", EquipmentSlot.Weapon, 5, 0);
            Gear(catalog, "leather-armor", "Кожаная броня", "Зачарованная кожаная броня", EquipmentSlot.Armor, 0, 2);
            Gear(catalog, "chainmail", "Кольчуга", "Зачарованная кольчуга", EquipmentSlot.Armor, 0, 4);
            Gear(catalog, "steel-armor", "Стальная броня", "Зачарованная стальная броня", EquipmentSlot.Armor, 0, 5);
            Gear(catalog, "helmet", "Шлем", "Зачарованный шлем", EquipmentSlot.Helmet, 0, 1);
            Gear(catalog, "iron-shield", "Железный щит", "Зачарованный железный щит", EquipmentSlot.Armor, 0, 3);
            Enchanted(catalog, "rusty-sword", "Зачарованный ржавый меч", EquipmentSlot.Weapon, 1, 0);
            Enchanted(catalog, "battle-axe", "Зачарованный боевой топор", EquipmentSlot.Weapon, 3, 1);
            Enchanted(catalog, "patched-armor", "Зачарованная латаная броня", EquipmentSlot.Armor, 0, 1);
            Enchanted(catalog, "iron-armor", "Зачарованная усиленная броня", EquipmentSlot.Armor, 0, 3);
            Enchanted(catalog, "wooden-shield", "Зачарованный деревянный щит", EquipmentSlot.Armor, 0, 2);

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
                Resource(icons, ResourceKind.BattleAxe, "Боевой топор", 85, "battle-axe", "Res_BattleAxe", equipmentId: "battle-axe"),
                // Vitaria kit 2026-10-02 (docs/economy-balance.md §10): prices by tools/balance/pricing.py, a by-product
                // counted as raw at 3 gold a unit of work
                Resource(icons, ResourceKind.Stone, "Камень", 3, "stone", "Res_Stone", "Res_StonePile"),
                Resource(icons, ResourceKind.CopperOre, "Медная руда", 4, "copper-ore", "Res_Ore_Copper", "Res_OrePile_Copper"),
                Resource(icons, ResourceKind.CopperIngot, "Медный слиток", 17, "copper-ingot", "Res_Ingot_Copper", "Res_IngotStack_Copper"),
                Resource(icons, ResourceKind.GoldOre, "Золотая руда", 20, "gold-ore", "Res_Ore_Gold", "Res_OrePile_Gold"),
                Resource(icons, ResourceKind.GoldIngot, "Золотой слиток", 65, "gold-ingot", "Res_Ingot_Gold", "Res_IngotStack_Gold"),
                Resource(icons, ResourceKind.Steel, "Сталь", 26, "steel", "Res_Ingot_Steel", "Res_IngotStack_Steel"),
                Resource(icons, ResourceKind.Wool, "Шерсть", 5, "wool", "Res_Wool"),
                Resource(icons, ResourceKind.Cloth, "Ткань", 22, "cloth", "Res_Cloth"),
                Resource(icons, ResourceKind.RustySword, "Ржавый меч", 20, "rusty-sword", "Res_Sword_Rusty", equipmentId: "rusty-sword"),
                Resource(icons, ResourceKind.PatchedArmor, "Латаная броня", 40, "patched-armor", "Res_Armor_Patched", equipmentId: "patched-armor"),
                Resource(icons, ResourceKind.SteelSword, "Стальной меч", 80, "steel-sword", "Res_Sword_Steel", equipmentId: "steel-sword"),
                Resource(icons, ResourceKind.Spear, "Копьё", 35, "spear", "Res_Spear", equipmentId: "spear"),
                Resource(icons, ResourceKind.Bow, "Лук", 75, "bow", "Res_Bow", equipmentId: "bow"),
                Resource(icons, ResourceKind.WarHammer, "Боевой молот", 90, "war-hammer", "Res_WarHammer", equipmentId: "war-hammer"),
                Resource(icons, ResourceKind.LeatherArmor, "Кожаная броня", 60, "leather-armor", "Res_Armor_Leather", equipmentId: "leather-armor"),
                Resource(icons, ResourceKind.Chainmail, "Кольчуга", 95, "chainmail", "Res_Armor_Chainmail", equipmentId: "chainmail"),
                Resource(icons, ResourceKind.SteelArmor, "Стальная броня", 130, "steel-armor", "Res_Armor_Steel", equipmentId: "steel-armor"),
                Resource(icons, ResourceKind.Helmet, "Шлем", 50, "helmet", "Res_Helmet", equipmentId: "helmet"),
                Resource(icons, ResourceKind.IronShield, "Железный щит", 50, "iron-shield", "Res_Shield_Iron", equipmentId: "iron-shield"),
                Enchanted(icons, ResourceKind.EnchantedRustySword, "Зачарованный ржавый меч", 85, "rusty-sword", "Res_Sword_Rusty"),
                Enchanted(icons, ResourceKind.EnchantedSteelSword, "Зачарованный стальной меч", 170, "steel-sword", "Res_Sword_Steel"),
                Enchanted(icons, ResourceKind.EnchantedBattleAxe, "Зачарованный боевой топор", 175, "battle-axe", "Res_BattleAxe"),
                Enchanted(icons, ResourceKind.EnchantedSpear, "Зачарованное копьё", 105, "spear", "Res_Spear"),
                Enchanted(icons, ResourceKind.EnchantedBow, "Зачарованный лук", 160, "bow", "Res_Bow"),
                Enchanted(icons, ResourceKind.EnchantedWarHammer, "Зачарованный боевой молот", 180, "war-hammer", "Res_WarHammer"),
                Enchanted(icons, ResourceKind.EnchantedPatchedArmor, "Зачарованная латаная броня", 110, "patched-armor", "Res_Armor_Patched"),
                Enchanted(icons, ResourceKind.EnchantedLeatherArmor, "Зачарованная кожаная броня", 145, "leather-armor", "Res_Armor_Leather"),
                Enchanted(icons, ResourceKind.EnchantedIronArmor, "Зачарованная усиленная броня", 180, "iron-armor", "Res_Armor_Iron"),
                Enchanted(icons, ResourceKind.EnchantedChainmail, "Зачарованная кольчуга", 185, "chainmail", "Res_Armor_Chainmail"),
                Enchanted(icons, ResourceKind.EnchantedSteelArmor, "Зачарованная стальная броня", 230, "steel-armor", "Res_Armor_Steel"),
                Enchanted(icons, ResourceKind.EnchantedHelmet, "Зачарованный шлем", 130, "helmet", "Res_Helmet"),
                Enchanted(icons, ResourceKind.EnchantedWoodenShield, "Зачарованный деревянный щит", 115, "wooden-shield", "Res_Shield_Wood"),
                Enchanted(icons, ResourceKind.EnchantedIronShield, "Зачарованный железный щит", 125, "iron-shield", "Res_Shield_Iron")
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
                    Extra(ResourceKind.GoldNugget, 1, 2, 3),
                    // the deeper mine finds stone and copper from level 2, gold ore from level 3
                    Extra(ResourceKind.Stone, 1, 10, 2),
                    Extra(ResourceKind.CopperOre, 1, 15, 2),
                    Extra(ResourceKind.GoldOre, 1, 4, 3)));
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
            // the arena's trophies wait here for haulers: to the armory, the enchanter or the market
            Storage(BuildingKind.Barracks, StorageRole.Stockpile, ResourceKind.RustySword, ResourceKind.PatchedArmor);
            Capacity(BuildingKind.Barracks, 20);
            Price(BuildingKind.Barracks, 300);
            Upgrades(BuildingKind.Barracks, new[] { 800, 2000 }, 0, 0, 0);
            // a building runs the first recipe it has the inputs for, so the deliveries choose: two coal and up make
            // steel from level 2 (listed first, or the coal ingots would always win), copper and gold ore their ingots
            Producer(BuildingKind.Smeltery, 700, 25, 4,
                Recipe(3f, In(ResourceKind.IronOre, 2, ResourceKind.Coal, 2), Out(ResourceKind.Steel, 1), minLevel: 2),
                Recipe(3f, In(ResourceKind.IronOre, 2, ResourceKind.Coal, 1), Out(ResourceKind.IronIngot, 2)),
                Recipe(2f, In(ResourceKind.IronOre, 2), Out(ResourceKind.IronIngot, 1)),
                Recipe(1f, In(ResourceKind.Scrap, 2), Out(ResourceKind.IronIngot, 1), minLevel: 2),
                Recipe(2f, In(ResourceKind.CopperOre, 2), Out(ResourceKind.CopperIngot, 1), minLevel: 2),
                Recipe(3f, In(ResourceKind.GoldOre, 2), Out(ResourceKind.GoldIngot, 1), minLevel: 2));
            Upgrades(BuildingKind.Smeltery, new[] { 900, 1500 }, 15, 2, 0);
            // every piece has its own set of inputs, the wider sets first: what the haulers bring decides what is
            // forged (steel, cloth, wool and copper each switch to their piece)
            Producer(BuildingKind.Forge, 1500, 10, 4,
                Spoils(Recipe(7f, In(ResourceKind.Steel, 2, ResourceKind.Leather, 1), Out(ResourceKind.SteelArmor, 1), minLevel: 3), 8),
                Spoils(Recipe(6f, In(ResourceKind.Steel, 2, ResourceKind.Planks, 1), Out(ResourceKind.WarHammer, 1), minLevel: 3), 8),
                Spoils(Recipe(6f, In(ResourceKind.Steel, 2), Out(ResourceKind.SteelSword, 1), minLevel: 2), 8),
                Spoils(Recipe(6f, In(ResourceKind.IronIngot, 3, ResourceKind.Cloth, 1), Out(ResourceKind.Chainmail, 1), minLevel: 2), 8),
                Spoils(Recipe(4f, In(ResourceKind.Leather, 1, ResourceKind.Wool, 1), Out(ResourceKind.LeatherArmor, 1), minLevel: 2), 8),
                Spoils(Recipe(5f, In(ResourceKind.IronIngot, 2, ResourceKind.Leather, 1), Out(ResourceKind.IronArmor, 1)), 8),
                Spoils(Recipe(6f, In(ResourceKind.IronIngot, 1, ResourceKind.Planks, 1, ResourceKind.Leather, 1),
                    Out(ResourceKind.BattleAxe, 1), minLevel: 2), 8),
                Spoils(Recipe(3f, In(ResourceKind.CopperIngot, 2), Out(ResourceKind.Helmet, 1), minLevel: 2), 8),
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
                    Extra(ResourceKind.Milk, 1, 50, 2),
                    Extra(ResourceKind.Wool, 1, 30, 2)));
            Upgrades(BuildingKind.Farm, new[] { 800, 1400 }, 15, 2, 0);
            // three units of work, like the farm: one farm keeps one tannery busy
            Producer(BuildingKind.Tannery, 1400, 25, 4,
                Recipe(3f, In(ResourceKind.AnimalHide, 1), Out(ResourceKind.Leather, 1)),
                Recipe(3f, In(ResourceKind.Wool, 2), Out(ResourceKind.Cloth, 1), minLevel: 2));
            Upgrades(BuildingKind.Tannery, new[] { 1200, 2000 }, 15, 2, 0);
            Producer(BuildingKind.LumberCamp, 400, 50, 5,
                Recipe(1f, null, Out(ResourceKind.Logs, 1)));
            Upgrades(BuildingKind.LumberCamp, new[] { 500, 900 }, 25, 2, 0);
            Producer(BuildingKind.LumberMill, 1000, 25, 4,
                Recipe(4f, In(ResourceKind.Logs, 2, ResourceKind.IronIngot, 1), Out(ResourceKind.Spear, 1), minLevel: 2),
                Recipe(1f, In(ResourceKind.Logs, 1), Out(ResourceKind.Planks, 1)));
            Upgrades(BuildingKind.LumberMill, new[] { 900, 1600 }, 15, 2, 0);
            Producer(BuildingKind.ShieldWorkshop, 1200, 10, 4,
                Recipe(5f, In(ResourceKind.Planks, 2, ResourceKind.IronIngot, 1), Out(ResourceKind.IronShield, 1), minLevel: 2),
                Recipe(4f, In(ResourceKind.Planks, 2, ResourceKind.Leather, 1), Out(ResourceKind.Bow, 1), minLevel: 2),
                Recipe(4f, In(ResourceKind.Planks, 3), Out(ResourceKind.WoodenShield, 1)));
            Upgrades(BuildingKind.ShieldWorkshop, new[] { 1200, 2000 }, 5, 2, 0);
            // whatever gear the haulers bring, with a crystal, comes out enchanted; one in ten spoils into scrap
            Producer(BuildingKind.Enchanter, 3000, 10, 3,
                Enchant(ResourceKind.IronSword, ResourceKind.EnchantedSword),
                Enchant(ResourceKind.RustySword, ResourceKind.EnchantedRustySword),
                Enchant(ResourceKind.SteelSword, ResourceKind.EnchantedSteelSword),
                Enchant(ResourceKind.BattleAxe, ResourceKind.EnchantedBattleAxe),
                Enchant(ResourceKind.Spear, ResourceKind.EnchantedSpear),
                Enchant(ResourceKind.Bow, ResourceKind.EnchantedBow),
                Enchant(ResourceKind.WarHammer, ResourceKind.EnchantedWarHammer),
                Enchant(ResourceKind.PatchedArmor, ResourceKind.EnchantedPatchedArmor),
                Enchant(ResourceKind.LeatherArmor, ResourceKind.EnchantedLeatherArmor),
                Enchant(ResourceKind.IronArmor, ResourceKind.EnchantedIronArmor),
                Enchant(ResourceKind.Chainmail, ResourceKind.EnchantedChainmail),
                Enchant(ResourceKind.SteelArmor, ResourceKind.EnchantedSteelArmor),
                Enchant(ResourceKind.Helmet, ResourceKind.EnchantedHelmet),
                Enchant(ResourceKind.WoodenShield, ResourceKind.EnchantedWoodenShield),
                Enchant(ResourceKind.IronShield, ResourceKind.EnchantedIronShield));
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

        private static void Capacity(BuildingKind kind, int capacity)
        {
            var definition = LoadBuilding(kind);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_capacity").intValue = capacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        // A piece of gear and its enchanted twin: the enchanter adds 2 to its main bonus (damage for a weapon).
        private static void Gear(GameContentCatalog catalog, string id, string name, string enchantedName,
            EquipmentSlot slot, int damage, int armor)
        {
            EnsureEquipment(catalog, id, name, slot, damage, armor);
            Enchanted(catalog, id, enchantedName, slot, damage, armor);
        }

        private static void Enchanted(GameContentCatalog catalog, string id, string name, EquipmentSlot slot,
            int damage, int armor)
        {
            bool weapon = slot == EquipmentSlot.Weapon;
            EnsureEquipment(catalog, "enchanted-" + id, name, slot, damage + (weapon ? 2 : 0), armor + (weapon ? 0 : 2));
        }

        // The good an enchanted piece travels as: icon enchanted-<id>, model <model>_Enchanted.
        private static ResourceDefinition Enchanted(Dictionary<string, Sprite> icons, ResourceKind kind, string name,
            int price, string id, string model) =>
            Resource(icons, kind, name, price, "enchanted-" + id, model + "_Enchanted", equipmentId: "enchanted-" + id);

        private static ProductionRecipe Enchant(ResourceKind gear, ResourceKind enchanted) =>
            Spoils(Recipe(6f, In(gear, 1, ResourceKind.VioletCrystal, 1), Out(enchanted, 1)), 10);

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
