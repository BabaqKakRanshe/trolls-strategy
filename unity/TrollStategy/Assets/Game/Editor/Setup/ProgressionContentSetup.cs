using System;
using TrollStrategy.Content;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Writes the tutorial and quest chain into Progression.asset and links it from the content catalog
    /// (docs/GDD.md §9). Goals and rewards come from the balance pass in docs/economy-balance.md; edit them
    /// here or in the asset, then re-run to reset. The game opens with the market and warehouse of the scene and goblins for hire; everything
    /// else is a quest reward.
    /// </summary>
    public static class ProgressionContentSetup
    {
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string CatalogPath = DefinitionFolder + "GameContentCatalog.asset";
        private const string ProgressionPath = DefinitionFolder + "Progression.asset";
        private const string FirstMission = "mission-1";

        [MenuItem("TrollStrategy/Setup Progression Content")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("Missing " + CatalogPath);
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionDefinition>(ProgressionPath);
            if (progression == null)
            {
                progression = ScriptableObject.CreateInstance<ProgressionDefinition>();
                AssetDatabase.CreateAsset(progression, ProgressionPath);
            }

            progression.Init(new[] { UnitKind.Goblin }, Array.Empty<BuildingKind>(), Array.Empty<string>(),
                new[]
                {
                    // Tutorial: one new idea per step.
                    Tutorial("tutorial-goblin", "Первый работник",
                        "Внизу, во вкладке «Существа», нажми на гоблина и кликни по свободной клетке на карте.",
                        Goals(QuestGoal.OwnUnits(UnitKind.Goblin, 1)),
                        Rewards(QuestReward.UnlockBuilding(BuildingKind.Mine))),
                    Tutorial("tutorial-mine", "Своя шахта",
                        "Внизу, во вкладке «Здания», нажми на шахту. Кликни по свободному месту на карте или нажми «Поставить сам».",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Mine)),
                        Rewards(QuestReward.UnlockUnit(UnitKind.Troll))),
                    Tutorial("tutorial-work", "За работу!",
                        "Найми тролля. Кликни по нему → «Работа» (E) → шахта. Затем кликни по гоблину → «Перенос» (H) → шахта → выбери, что носить → склад.",
                        Goals(QuestGoal.WorkAt(UnitKind.Troll, BuildingKind.Mine),
                            QuestGoal.HaulRoute(UnitKind.Goblin, BuildingKind.Mine, BuildingKind.Warehouse)),
                        Rewards(QuestReward.Coins(100))),
                    Tutorial("tutorial-market", "Первая выручка",
                        "Руда копится на складе. Найми ещё гоблина и дай ему «Перенос»: склад → рынок. Рынок платит за каждую доставленную руду.",
                        Goals(QuestGoal.AnyHaulRoute(BuildingKind.Warehouse, BuildingKind.Market),
                            QuestGoal.EarnGold(30)),
                        Rewards(QuestReward.Coins(150))),
                    // Earn, not hold: a goal on gold in hand would punish hiring the carriers the hint asks for.
                    Tutorial("tutorial-treasury", "Казна",
                        "Золото приходит, только когда руду довозят до рынка. Найми ещё гоблинов и дай им перенос: шахта → рынок или склад → рынок.",
                        Goals(QuestGoal.EarnGold(300)),
                        Rewards(QuestReward.Coins(150), QuestReward.UnlockBuilding(BuildingKind.Barracks))),
                    // The battle opens with the barracks, not with a head count: the squad is whoever the colony has.
                    Tutorial("tutorial-barracks", "Бараки",
                        "Внизу, во вкладке «Здания», нажми на бараки и поставь их на свободное место. Снятые с работы существа отдыхают у их дверей, а из бараков открывается дорога на арену.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Barracks)),
                        Rewards(QuestReward.UnlockMission(FirstMission), QuestReward.Coins(100))),
                    Tutorial("tutorial-battle", "Первый бой",
                        "Нажми «В бой» справа. Поставь до 4 бойцов в левые клетки: тролля вперёд, гоблинов за ним. Бой идёт сам.",
                        Goals(QuestGoal.WinBattles()),
                        Rewards(QuestReward.UnlockBuilding(BuildingKind.Field))),

                    // Quests alternate a short "build it and staff it" with a longer "use it" that asks for goods.
                    // The "use it" quest opens the next building and pays about 40% of its price; the colony earns
                    // the rest. Sizes are from docs/economy-balance.md.
                    Quest("field-build", "Урожай",
                        "Поле растит пшеницу без сырья и собирает её разом. Построй его поближе к складу и поставь трёх рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Field), QuestGoal.AnyWorkAt(BuildingKind.Field, 3)),
                        Rewards(QuestReward.Coins(100))),
                    Quest("wheat-sell", "Хлеб на рынок",
                        "Дай носильщикам перенос с поля или со склада на рынок и продай пшеницу.",
                        Goals(QuestGoal.SellResource(ResourceKind.Wheat, 40)),
                        Rewards(QuestReward.Coins(300), QuestReward.UnlockBuilding(BuildingKind.Smeltery))),
                    Quest("smeltery-build", "Плавильня",
                        "Плавильня делает из двух руд слиток. Построй её и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Smeltery), QuestGoal.AnyWorkAt(BuildingKind.Smeltery, 2)),
                        Rewards(QuestReward.Coins(100))),
                    Quest("ingot-sell", "Железо в цене",
                        "Вози руду в плавильню, а слитки — на рынок: слиток стоит дороже двух руд.",
                        Goals(QuestGoal.SellResource(ResourceKind.IronIngot, 30)),
                        Rewards(QuestReward.Coins(600), QuestReward.UnlockBuilding(BuildingKind.Forge))),
                    Quest("forge-build", "Кузница",
                        "Кузница куёт мечи из слитков. Построй её и поставь двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Forge), QuestGoal.AnyWorkAt(BuildingKind.Forge, 2)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("market-upgrade", "Богатый рынок",
                        "Выбери рынок на карте и нажми «Улучшить»: каждый уровень поднимает цену всех товаров.",
                        Goals(QuestGoal.UpgradeBuilding(BuildingKind.Market, 2)),
                        Rewards(QuestReward.Coins(250), QuestReward.UnlockBuilding(BuildingKind.Armory))),
                    Quest("armory-build", "Склад экипировки",
                        "Построй склад экипировки и вози туда мечи из кузницы: перед боем их можно выдать бойцам.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Armory),
                            QuestGoal.AnyHaulRoute(BuildingKind.Forge, BuildingKind.Armory)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("battle-veteran", "Боевой опыт",
                        "Выиграй ещё один бой. Снаряжение со склада экипировки делает бойцов сильнее.",
                        Goals(QuestGoal.WinBattles()),
                        Rewards(QuestReward.Coins(250), QuestReward.UnlockBuilding(BuildingKind.Farm))),
                    Quest("farm-build", "Ферма",
                        "Ферма растит скот на пшенице и даёт шкуры. Построй её и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Farm), QuestGoal.AnyWorkAt(BuildingKind.Farm, 2)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("population", "Растущее поселение",
                        "Больше рук — больше дел. Доведи население до 20 существ; каждое новое нанять чуть дороже.",
                        Goals(QuestGoal.Population(20)),
                        Rewards(QuestReward.Coins(550), QuestReward.UnlockBuilding(BuildingKind.Tannery))),
                    Quest("tannery-build", "Кожевня",
                        "Кожевня выделывает шкуры в кожу. Построй её и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Tannery), QuestGoal.AnyWorkAt(BuildingKind.Tannery, 2)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("leather-sell", "Кожа на продажу",
                        "Кожа дорого стоит на рынке, а в кузнице из неё и слитков выходит броня.",
                        Goals(QuestGoal.SellResource(ResourceKind.Leather, 15)),
                        Rewards(QuestReward.Coins(200), QuestReward.UnlockBuilding(BuildingKind.LumberCamp))),
                    Quest("lumber-build", "Лесозаготовка",
                        "Лесозаготовка валит брёвна без сырья. Построй её и поставь трёх рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.LumberCamp), QuestGoal.AnyWorkAt(BuildingKind.LumberCamp, 3)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("trade-route", "Торговый путь",
                        "Налаженная торговля — основа казны. Заработай на рынке 2000 золота.",
                        Goals(QuestGoal.EarnGold(2000)),
                        Rewards(QuestReward.Coins(400), QuestReward.UnlockBuilding(BuildingKind.LumberMill))),
                    Quest("mill-build", "Пилорама",
                        "Пилорама режет брёвна на доски. Построй её и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.LumberMill), QuestGoal.AnyWorkAt(BuildingKind.LumberMill, 2)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("planks-sell", "Доски",
                        "Продай доски на рынке — или копи их для щитов.",
                        Goals(QuestGoal.SellResource(ResourceKind.Planks, 60)),
                        Rewards(QuestReward.Coins(500), QuestReward.UnlockBuilding(BuildingKind.ShieldWorkshop))),
                    Quest("shields-build", "Мастерская щитов",
                        "Щиты из досок укрепляют отряд. Построй мастерскую и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.ShieldWorkshop),
                            QuestGoal.AnyWorkAt(BuildingKind.ShieldWorkshop, 2)),
                        Rewards(QuestReward.Coins(200))),
                    Quest("mine-upgrade", "Глубокая шахта",
                        "Улучши шахту до третьего уровня: больше мест для рабочих и руды. В породе иногда попадаются кристаллы.",
                        Goals(QuestGoal.UpgradeBuilding(BuildingKind.Mine, 3)),
                        Rewards(QuestReward.Coins(1200), QuestReward.UnlockBuilding(BuildingKind.Enchanter))),
                    Quest("enchanter-build", "Зачарователь",
                        "Зачарователь соединяет меч и кристалл в зачарованный меч. Построй его и поставь рабочего.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Enchanter), QuestGoal.AnyWorkAt(BuildingKind.Enchanter, 1)),
                        Rewards(QuestReward.Coins(400)))
                },
                new[]
                {
                    // After the chain: gold only, targets and rewards grow every cycle.
                    Quest("repeat-trade", "Торговля",
                        "Заработай на рынке золото. Задание повторяется, и цель растёт.",
                        Goals(QuestGoal.EarnGold(2000)),
                        Rewards(QuestReward.Coins(500))),
                    Quest("repeat-battle", "Арена",
                        "Выиграй бой. Повторные победы тоже приносят золото.",
                        Goals(QuestGoal.WinBattles()),
                        Rewards(QuestReward.Coins(400))),
                    Quest("repeat-goods", "Оборот",
                        "Продай на рынке товары любого вида.",
                        Goals(QuestGoal.SellGoods(200)),
                        Rewards(QuestReward.Coins(400)))
                },
                repeatGrowthPercent: 25);
            EditorUtility.SetDirty(progression);

            catalog.SetProgression(progression);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProgressionContentSetup] {progression.Quests.Count} quests written to {ProgressionPath}.");
        }

        private static QuestDefinition Tutorial(string id, string title, string description, QuestGoal[] goals,
            QuestReward[] rewards) => new(id, title, description, true, goals, rewards);

        private static QuestDefinition Quest(string id, string title, string description, QuestGoal[] goals,
            QuestReward[] rewards) => new(id, title, description, false, goals, rewards);

        private static QuestGoal[] Goals(params QuestGoal[] goals) => goals;

        private static QuestReward[] Rewards(params QuestReward[] rewards) => rewards;
    }
}
