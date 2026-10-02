Подключи к игре новые товары и снаряжение из кита Vitaria (выгрузка 02.10): 35 новых значков в атласе resources-icons и 41 новую модель в Models/Resources. Работа из двух частей, по порядку. После каждой покажи снимки и подожди моего ответа, только потом начинай следующую.

Если у тебя есть доступ к редактору (Unity MCP), сам смотри Console, запускай Play Mode и снимай Game view. Если нет — говори мне, что сделать и что прислать.

## Что пришло из кита

- **Атлас** assets/sprites/Atlases/resources-icons-0.png и resources-icons.json:
  - 2048 × 2048, 59 кадров по 254 px; было 1024 × 2048 и 24 кадра;
  - прежние 24 кадра на тех же координатах, пиксель в пиксель; новые 35 — после них;
  - имя кадра — id предмета.
- **Модели** Assets/Vitaria/Models/Resources: 41 новый FBX, всего 71.
  - 10 из них — модели прежних предметов, у которых был только значок: сноп, шкура, кожа, кристаллы, три меча, две брони, деревянный щит. Это часть 2 PROMPT_ITEMS.md, её не повторяй. Кучек ещё нет.
  - Размеры и треугольники — assets/Strategy_Kit/asset_list.json (size_unity, tris). Низ модели в y = 0. Оружие стоит на навершии или пятке древка, броня, шлем и щиты — на нижней кромке.
  - Часть моделей превышает лимит PROMPT_ITEMS в 300 треугольников (брони — до 736). Это решаю я вместе с китом: в Unity модели не упрощай.
- **Палитра** Assets/Vitaria/Textures/Vitaria_Palette.png: 5 новых цветов в конце (сталь, ткань), прежние ячейки на месте. kit_version.json обновлён.
- Подробности — README кита (assets/Strategy_Kit/README.md), разделы «Ресурсы vitaria_resources», «Снаряжение vitaria_gear», «Иконки зданий и ресурсов».

## Значки и модели по предметам

Товары: значок — модель одной штуки — модель полной кучки (если есть).
- Новые:
  - stone — Res_Stone — Res_StonePile;
  - copper-ore — Res_Ore_Copper — Res_OrePile_Copper;
  - copper-ingot — Res_Ingot_Copper — Res_IngotStack_Copper;
  - gold-ore — Res_Ore_Gold — Res_OrePile_Gold (это руда, не самородок gold-nugget);
  - gold-ingot — Res_Ingot_Gold — Res_IngotStack_Gold;
  - steel — Res_Ingot_Steel — Res_IngotStack_Steel;
  - flour, bread, cheese, ale, wool, cloth — Res_Flour, Res_Bread, Res_Cheese, Res_Ale, Res_Wool, Res_Cloth, кучек пока нет.
- Прежние:
  - iron-ore — Res_Ore_Iron — Res_OrePile_Iron;
  - iron-ingot — Res_Ingot_Iron — Res_IngotStack_Iron;
  - logs — Res_Log — Res_LogPile;
  - wheat — Res_Wheat, animal-hide — Res_Hide, leather — Res_Leather, planks — Res_Planks, violet-crystal — Res_Crystal, coal — Res_Coal, gold-nugget — Res_GoldNugget, straw — Res_Straw, golden-wheat — Res_GoldenWheat, meat — Res_Meat, milk — Res_Milk, scrap — Res_Scrap, feast — Res_Feast.

Снаряжение: 15 предметов, у каждого обычная и зачарованная версия. Зачарованная — значок enchanted-<id>, модель <модель>_Enchanted. Исключение — железный меч: его зачарованная версия — прежний enchanted-sword, Res_Sword_Enchanted.
- оружие: rusty-sword — Res_Sword_Rusty, iron-sword — Res_Sword_Iron, steel-sword — Res_Sword_Steel, battle-axe — Res_BattleAxe, spear — Res_Spear, bow — Res_Bow, war-hammer — Res_WarHammer;
- броня: patched-armor — Res_Armor_Patched, leather-armor — Res_Armor_Leather, iron-armor — Res_Armor_Iron, chainmail — Res_Armor_Chainmail, steel-armor — Res_Armor_Steel, helmet — Res_Helmet;
- щиты: wooden-shield — Res_Shield_Wood, iron-shield — Res_Shield_Iron.

## Часть 1: значки и модели (без новых предметов)

1. **Атлас.**
   - TrollStrategy/Dev/Import Icon Atlases: скопирует атлас в Assets/Game/Art/Sprites/Atlases и нарежет 59 спрайтов.
   - AssetSlicer сохраняет spriteID по имени, поэтому ссылки на прежние 24 значка должны остаться: EquipmentDefinition._icon, значки товаров в GameContentCatalog, _outgoingProductSprite и _saleIncomeSprite в префабах зданий. Проверь: в Console нет Missing, в интерфейсе колонии и боя прежние значки на месте.
   - Импорт: PPU 362, мипмапы, без сжатия, maxTextureSize 2048. Атлас вдвое больше: около 21 МБ видеопамяти с мипмапами вместо 11. Сжатие сам не меняй; если в сборке это заметно — скажи.
2. **Модели.**
   - FBX настраивает VitariaModelPostprocessor: материалы Vitaria_Palette и Vitaria_FX (руны и камни зачарованных предметов светятся). Tools/Vitaria/2. Create Prefabs для этого не нужен.
   - Проверь все 41: нет розовых материалов, светятся только руны и фиолетовые камни, масштаб 1 юнит = 1 м.
3. **Модель товара рядом со значком.**
   - В ResourceDefinition добавь два поля: модель одной штуки и модель полной кучки (пусто, если кучки нет). Это та «одна таблица», которая нужна части 3 PROMPT_ITEMS.
   - Заполняй их в ProductionContentSetup.Resource(...) по имени FBX из списка выше — так же, как значок по имени кадра. Нет модели — предупреждение в лог, как для значка.
   - Ссылайся на сами FBX из Assets/Vitaria/Models/Resources, копий префабов не заводи.
   - Запусти TrollStrategy/Dev/Setup Production Content. Он перезаписывает цены и рецепты из кода, поэтому в git diff ассетов должны поменяться только новые поля моделей. Если меняется что-то ещё (числа правили руками в ассетах) — остановись и скажи мне.
4. **Проверка.**
   - Тест: у каждого товара в каталоге есть значок и модель одной штуки (у 21 нынешнего товара модели уже есть).
   - Все EditMode-тесты зелёные.
   - Снимки: атлас в Sprite Editor; 41 новая модель в ряд при свете колонии.

## Часть 2: новые предметы в игре

Сначала план, потом код: правила экономики решаю я.

1. **План — таблица на 35 предметов.**
   - id, название, цена продажи.
   - Где делается: здание, рецепт, работа, уровень, побочные шансы, порча в лом.
   - Кто принимает: Склад (Warehouse, список RawAndIntermediateGoods), Рынок, Склад экипировки (Armory).
   - Для снаряжения: слот, бонусы урона и брони, место в ряду (ржавый — железный — стальной, обычный — зачарованный).
   - Цены и рецепты — по правилам docs/economy-balance.md. Если в docs/GDD.md эти предметы уже расписаны, бери оттуда.
2. **По умолчанию, если я не решу иначе.**
   - Только нынешние здания. Новое здание — это новая модель кита: сначала спроси меня.
   - Слотов два, как сейчас (EquipmentSlot: Weapon, Armor). Шлем и щиты — броня, лук и копьё — оружие, без новой механики боя.
   - Зачарование — у Зачарователя (Enchanter), как у enchanted-sword: предмет и кристалл, с шансом порчи в лом.
3. **Отдельно спроси меня.**
   - Ржавый меч и латаная броня — стартовое снаряжение, ResourceKind у них нет. Чтобы их зачаровывать, их надо сделать товарами — или зачарованные версии появляются иначе.
   - Мука, хлеб, сыр, эль: делать в Таверне и включить в рецепт пира — или отдельно. Шерсть и ткань: где делать и куда девать.
   - Всё, что не ложится в нынешние правила.
4. **После моего ответа — код.**
   - ResourceKind: новые значения только в конец, порядок не меняй. По индексу сериализуются ассеты, а порядок перечисления — это порядок, в котором носильщики берут товары и склады заполняют ячейки.
   - ProductionContentSetup: Resource(...) со значком и моделями, EnsureEquipment для снаряжения (создаст Equipment_<Id>.asset), рецепты.
   - Названия для игрока — через конвейер локализации (AGENTS.md, раздел Localization).
   - Запуск: Setup Production Content, затем ещё раз Import Icon Atlases — он раздаст значки новым EquipmentDefinition по ItemId.
5. **Проверка.**
   - Тест: у каждого EquipmentDefinition есть значок; каждый новый товар делается хотя бы одним рецептом и где-то принимается — тупиков нет.
   - CampaignBotTests и все EditMode-тесты зелёные, сборка Windows (TrollStrategy/Build Windows Player) проходит.
   - Снимки: рецепты зданий с новыми товарами; Склад, Рынок и Склад экипировки с новыми значками; отряд в новом снаряжении на расстановке.

## Ограничения

- Domain и Application не меняй: правила боя, слоты и хранение — как есть. ResourceKind и определения — это Content.
- Assets/Vitaria (FBX, палитра, раскладки, материалы) руками не правь, только выгрузкой кита. Атлас не перепаковывай: его собирает Blender/render_icons.py кита.
- Сторонний арт не добавляй: репозиторий публичный.
- Если часть 1 PROMPT_ITEMS (пиксельное снаряжение на бойце) ещё впереди: снаряжения станет 30 предметов, а не 7. Объём спрайтов согласуй со мной.

## Отчёт

- Снимки по каждой части.
- Какие файлы изменены и что в каждом.
- Таблица новых предметов, как они вошли в игру.
- Что не получилось или требует моего решения.
