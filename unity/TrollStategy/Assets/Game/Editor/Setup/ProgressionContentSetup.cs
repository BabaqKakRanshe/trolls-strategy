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

        [MenuItem("TrollStrategy/Dev/Setup Progression Content")]
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

                    // Quests alternate a short "build it and staff it" with a longer "use it" that asks for goods,
                    // and since 2026-10-02 with other kinds of goals: land, the armory, the guild's upgrades, the
                    // arena ladder, a dressed squad, a feast, coal from a deep mine. A "use it" quest opens the next
                    // building and pays about 40% of its price; the colony earns the rest. Sizes are from
                    // docs/economy-balance.md; goods targets doubled on 2026-10-02.
                    Quest("field-build", "Урожай",
                        "Поле растит пшеницу и солому без сырья и собирает их разом. Построй его поближе к складу и поставь трёх рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Field), QuestGoal.AnyWorkAt(BuildingKind.Field, 3)),
                        Rewards(QuestReward.Coins(100))),
                    Quest("wheat-sell", "Хлеб на рынок",
                        "Дай носильщикам перенос с поля или со склада на рынок и продай пшеницу.",
                        Goals(QuestGoal.SellResource(ResourceKind.Wheat, 60)),
                        Rewards(QuestReward.Coins(300), QuestReward.UnlockBuilding(BuildingKind.Smeltery))),
                    Quest("smeltery-build", "Плавильня",
                        "Плавильня делает из двух руд слиток, а с углём — два слитка. Построй её и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Smeltery), QuestGoal.AnyWorkAt(BuildingKind.Smeltery, 2)),
                        Rewards(QuestReward.Coins(100))),
                    Quest("ingot-sell", "Железо в цене",
                        "Вози руду в плавильню, а слитки — на рынок: слиток стоит дороже двух руд.",
                        Goals(QuestGoal.SellResource(ResourceKind.IronIngot, 45)),
                        Rewards(QuestReward.Coins(600), QuestReward.UnlockBuilding(BuildingKind.Forge))),
                    Quest("land-buy", "Новая земля",
                        "Острову тесно. Нажми «Земля» справа сверху (L), выбери блок рядом с колонией и купи его. Потом расчисти: на дикой земле строить нельзя.",
                        Goals(QuestGoal.OwnLand(1)),
                        Rewards(QuestReward.Coins(250), QuestReward.UnlockBuilding(BuildingKind.HaulersGuild))),
                    // the guild comes as soon as the haulers queue at the mine's door, not after the armory
                    Quest("guild-build", "Гильдия носильщиков",
                        "Носильщики толпятся у дверей шахты? Гильдия их учит. Построй её из вкладки «Здания», нажми на неё на карте и купи в её карточке два улучшения: «Широкие двери» пускают к двери больше носильщиков, «Быстрые руки» ускоряют погрузку.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.HaulersGuild), QuestGoal.BuyUpgrades(2)),
                        Rewards(QuestReward.Coins(350))),
                    Quest("forge-build", "Кузница",
                        "Кузница куёт мечи из слитков, а с кожей — броню; иногда портит работу в лом. Построй её и поставь двух рабочих.",
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
                    Quest("armory-stock", "Оружие для отряда",
                        "Пусть на складе экипировки будет пять предметов: мечи, броня, щиты.",
                        Goals(QuestGoal.OwnEquipment(5)),
                        Rewards(QuestReward.Coins(200))),
                    Quest("battle-veteran", "Третий уровень арены",
                        "Каждая победа открывает следующий уровень арены. Дойди до третьего: там встретятся хоббиты, и после победы их можно нанимать.",
                        Goals(QuestGoal.ReachArenaLevel(3)),
                        Rewards(QuestReward.Coins(300), QuestReward.UnlockBuilding(BuildingKind.Farm))),
                    Quest("farm-build", "Ферма",
                        "Ферма кормит скот пшеницей и соломой и даёт шкуры, мясо и молоко. Построй её и дай ей двух рабочих.",
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
                        Goals(QuestGoal.SellResource(ResourceKind.Leather, 20)),
                        Rewards(QuestReward.Coins(200), QuestReward.UnlockBuilding(BuildingKind.LumberCamp))),
                    Quest("lumber-build", "Лесозаготовка",
                        "Лесозаготовка валит брёвна без сырья. Построй её и поставь трёх рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.LumberCamp), QuestGoal.AnyWorkAt(BuildingKind.LumberCamp, 3)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("trade-route", "Торговый путь",
                        "Налаженная торговля — основа казны. Заработай на рынке 3000 золота.",
                        Goals(QuestGoal.EarnGold(3000)),
                        Rewards(QuestReward.Coins(400), QuestReward.UnlockBuilding(BuildingKind.LumberMill))),
                    Quest("mill-build", "Пилорама",
                        "Пилорама режет брёвна на доски. Построй её и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.LumberMill), QuestGoal.AnyWorkAt(BuildingKind.LumberMill, 2)),
                        Rewards(QuestReward.Coins(150))),
                    Quest("planks-sell", "Доски",
                        "Продай доски на рынке — или копи их для щитов.",
                        Goals(QuestGoal.SellResource(ResourceKind.Planks, 90)),
                        Rewards(QuestReward.Coins(500), QuestReward.UnlockBuilding(BuildingKind.ShieldWorkshop))),
                    Quest("shields-build", "Мастерская щитов",
                        "Щиты из досок укрепляют отряд. Построй мастерскую и дай ей двух рабочих.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.ShieldWorkshop),
                            QuestGoal.AnyWorkAt(BuildingKind.ShieldWorkshop, 2)),
                        Rewards(QuestReward.Coins(200))),
                    Quest("squad-dressed", "Отряд в железе",
                        "Выдай снаряжение четырём бойцам: перед боем нажми на бойца и выбери предмет внизу.",
                        Goals(QuestGoal.EquipFighters(4)),
                        Rewards(QuestReward.Coins(300), QuestReward.UnlockBuilding(BuildingKind.Tavern))),
                    Quest("tavern-build", "Пир на весь остров",
                        "Таверна собирает пир из четырёх товаров: пшеницы, мяса, молока и брёвен. Построй её, дай ей двух рабочих, привези всё нужное и приготовь шесть пиров.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Tavern), QuestGoal.AnyWorkAt(BuildingKind.Tavern, 2),
                            QuestGoal.ProduceResource(ResourceKind.Feast, 6)),
                        Rewards(QuestReward.Coins(700))),
                    Quest("mine-upgrade", "Глубокая шахта",
                        "Улучши шахту до третьего уровня и добудь 12 угля: со второго уровня в породе попадается уголь, с третьего — самородки. С углём плавильня даёт два слитка вместо одного.",
                        Goals(QuestGoal.UpgradeBuilding(BuildingKind.Mine, 3), QuestGoal.ProduceResource(ResourceKind.Coal, 12)),
                        Rewards(QuestReward.Coins(1500), QuestReward.UnlockBuilding(BuildingKind.Enchanter))),
                    Quest("enchanter-build", "Зачарователь",
                        "Зачарователь соединяет меч и кристалл в зачарованный меч. Построй его и поставь рабочего.",
                        Goals(QuestGoal.OwnBuildings(BuildingKind.Enchanter), QuestGoal.AnyWorkAt(BuildingKind.Enchanter, 1)),
                        Rewards(QuestReward.Coins(400))),
                    // before the sixth arena level: the barracks' level opens the deeper steps of their upgrades
                    Quest("barracks-upgrade", "Крепкие бараки",
                        "Уровень здания открывает следующие ступени его улучшений. Выбери бараки на карте и нажми «Улучшить»: на втором уровне у всех четырёх улучшений бараков — «Больше бойцов», «Закалка», «Отдых» и «Слава арены» — откроется ещё одна ступень.",
                        Goals(QuestGoal.UpgradeBuilding(BuildingKind.Barracks, 2)),
                        Rewards(QuestReward.Coins(400))),
                    Quest("arena-six", "Слава арены",
                        "Дойди до шестого уровня арены. Улучшения бараков делают отряд сильнее.",
                        Goals(QuestGoal.ReachArenaLevel(6)),
                        Rewards(QuestReward.Coins(800)))
                },
                new[]
                {
                    // After the chain: gold only, targets and rewards grow every cycle.
                    Quest("repeat-trade", "Торговля",
                        "Заработай на рынке золото. Задание повторяется, и цель растёт.",
                        Goals(QuestGoal.EarnGold(4000)),
                        Rewards(QuestReward.Coins(500))),
                    Quest("repeat-battle", "Арена",
                        "Выиграй бой. Повторные победы тоже приносят золото.",
                        Goals(QuestGoal.WinBattles()),
                        Rewards(QuestReward.Coins(400))),
                    Quest("repeat-goods", "Оборот",
                        "Продай на рынке товары любого вида.",
                        Goals(QuestGoal.SellGoods(400)),
                        Rewards(QuestReward.Coins(400))),
                    Quest("repeat-feast", "Праздник",
                        "Приготовь в таверне пиры.",
                        Goals(QuestGoal.ProduceResource(ResourceKind.Feast, 10)),
                        Rewards(QuestReward.Coins(500)))
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
