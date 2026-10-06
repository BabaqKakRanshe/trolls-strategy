---

description: "Tasks: склад экипировки показывает снаряжение"
---

# Tasks: Склад экипировки показывает снаряжение

**Input**: Design documents from `specs/008-armory-inventory/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/armory-ui.md](contracts/armory-ui.md), [quickstart.md](quickstart.md)

**Tests**: Входят в задачи. Конституция (VII) требует EditMode-тестов на узкой границе, у каждой истории спеки есть Independent Test.

**Organization**: По историям спеки. US1, US2 — P1; US3, US4 — P2; US5 — P3 и ждёт ответа владельца на FR-017.

## Format: `[ID] [P?] [Story] Description`

- **[P]** — можно делать параллельно: другой файл, нет зависимостей от незакрытых задач.
- Пути — от `unity/TrollStategy/Assets/Game/`, если не указан корень репозитория.
- Тексты — из [contracts/armory-ui.md](contracts/armory-ui.md), дословно. На экран — только через `Ui.SetText` / `Ui.Text`.

---

## Phase 1: Setup

- [ ] T001 Убедиться, что чужая работа закоммичена и файлы свободны:
  - карточка здания другой сессии: `Runtime/UI/Colony/InspectPanel.cs`, `Runtime/UI/Colony/RecipeList.cs`, `UI/Uxml/Colony/Inspect.uxml`, `Runtime/UI/Colony/WikiPanel.cs`, `Tests/EditMode/NewPanelsTests.cs`, `UI/Styles/ColonyHud.uss`, `UI/Styles/Theme.uss`, `UI/Styles/Wiki.uss`;
  - удаление «Нанять» из карточек (родительская сессия, пункт 11);
  - чужие правки `Runtime/Application/GameSession.cs`.

  Получить согласие автора `NewPanelsTests.Book_OpensOnTheEntry_FromAHintAndFromTheInspectCard` на разворот «у рынка нет страницы и диска» (research R7). Ответ записать в Notes.
- [ ] T002 Подготовить проверку:
  - копия проекта по памяти `unity-private-copy-verification` (`C:\tmp\<имя>`, весь `tools/`);
  - офлайн-компиляция по памяти `unity-offline-compile`;
  - на копии прогнать весь EditMode-набор и записать исходные падения в Notes, чтобы отличать свои падения от чужих.

---

## Phase 2: Foundational — слова о назначении (блокирует US1–US4)

- [ ] T003 В `Runtime/Application/GameSession.cs` добавить `public IReadOnlyList<string> ExplainStorage(BuildingDefinition building)` с текстами из контракта:
  - **поиск зданий для дырок шаблона:** склад экипировки — первое здание `_catalog.Buildings` с `StorageRole.Armory`; склад — первое с `StorageRole.Stockpile` и `!KeepsOnlyGear(...)`. Имя — `BuildingName(kind)`. Нет здания — предложение не добавляется;
  - **Stockpile с сырьём:** «Сырьё и полуфабрикаты: из них здания делают товары, рынок их продаёт.» и `$"Оружие и броню не берёт: их хранит {armory}."`;
  - **Armory:** «Готовое снаряжение колонии: его выдают бойцам в расстановке перед боем.», «Места не ограничены. Отсюда ничего не вывозят, ни на рынок, ни к зачарователю: снаряжение уходит только на бойцов.» и `$"Сырьё хранит {warehouse}."`;
  - **бараки и рынок:** одна строка — их `DescribeRole`;
  - **без `StorageRole`:** пустой список.
- [ ] T004 В `Runtime/Application/GameSession.cs`:
  - `DescribeRole`: Stockpile с сырьём → «Сырьё для зданий и рынка», Armory → «Снаряжение для бойцов»; бараки и рынок без изменений;
  - `DescribeBuilding`: вместо строки `DescribeRole` добавлять строки `ExplainStorage` (строка размера и рецепты — как были).
- [ ] T005 В `Runtime/Application/GameSession.cs` `DescribeGoal`: `QuestGoalKind.OwnEquipment` → «Снаряжение колонии, предметов». Счёт цели не трогать.
- [ ] T006 [P] Создать `Tests/EditMode/ArmoryCardTests.cs`:
  - **общая настройка:** каталог `Assets/Game/Content/Definitions/GameContentCatalog.asset`, сессия `new GameSession(_catalog, TestColony.LayoutFor(_catalog, Warehouse, Market, Barracks, Armory))` на свободных клетках, как в `ColonyHudTests.SetUp`, `InteractionController`, `TestUi.Colony`;
  - **помощник синтетических снимков** в `Tests/EditMode/TestColony.cs`: `static GameSnapshot With(GameSnapshot s, IReadOnlyList<BuildingSnapshot> buildings = null, IReadOnlyList<EquipmentSnapshot> equipment = null)` — новый `GameSnapshot` с теми же ревизией, золотом, существами, прогрессом, землёй и улучшениями, где заменено только переданное. Им пользуются T011, T014, T017;
  - **первые тесты слов:**
    - `ExplainStorage(склад)` — две строки, вторая содержит имя склада экипировки в нижнем регистре;
    - `ExplainStorage(склад экипировки)` — три строки, третья содержит «склад»;
    - `ExplainStorage(бараки)` == `[DescribeRole(бараки)]`;
    - `DescribeBuilding(склад экипировки)` содержит все строки `ExplainStorage`;
    - `DescribeGoal(OwnEquipment)` == «Снаряжение колонии, предметов».

**Checkpoint**: офлайн-компиляция без ошибок, тесты слов зелёные.

---

## Phase 3: User Story 1 — склад экипировки показывает снаряжение картинками (P1) 🎯 MVP

**Goal**: Карточка склада экипировки показывает запас сеткой ячеек: картинка и число на вид, зачарованные отдельно с каймой, подсказка с названием, прибавкой и числами.

**Independent Test**: колония со складом экипировки, `DebugGrantGear(3)`. 30 ячеек, у ржавого меча и латаной брони 4, у остальных 3, у 15 зачарованных кайма.

- [ ] T007 [US1] В `UI/Uxml/Colony/Inspect.uxml` внутри `inspect-rows-scroll`, после `inspect-recipes`, добавить `<ui:VisualElement name="inspect-gear" class="gear-stock" />`. Структура префаба `UI` не меняется, `UiSetup` не нужен.
- [ ] T008 [P] [US1] Стили:
  - `UI/Styles/Theme.uss` — `.is-enchanted { border-color: var(--magic); }` рядом с определением `--magic`, с комментарием в стиле файла;
  - `UI/Styles/ColonyHud.uss` рядом с `.slot` — `.gear-stock` (колонка), `.gear-stock__row` (`flex-direction: row`), `.gear-stock__empty` (как `.note`) и `.slot.is-enchanted { border-width: 2px; }`. Цветов в `ColonyHud.uss` не задавать.
- [ ] T009 [US1] Создать `Runtime/UI/Colony/GearStock.cs` — `public sealed class GearStock` по образцу `RecipeList`/`StaffList` (класс над своим контейнером):
  - **конструктор** `GearStock(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)`; `public const int CellsPerRow = 8`;
  - **методы:** `Show(GameSnapshot snapshot)`, `Hide()`, `IsShown`;
  - **группировка** `snapshot.Equipment` по `DefinitionId`, отдельно запас (`OwnerUnitId == null`) и надетое. Определение — `context.Catalog.GetEquipment(id)`: `Icon`, `Enchanted`, `Slot`, бонусы, имя;
  - **порядок:** `Slot` (Weapon, Armor, Helmet) → простые раньше зачарованных → индекс в `catalog.Equipment`;
  - **вёрстка:**
    - заголовок `Ui.Text(…, "slots-header t-medium")` с `$"В запасе: {inStock}"`;
    - ряды `Ui.Box("gear-stock__row")` по `CellsPerRow`, явными рядами, без `flex-wrap` (research R3);
    - ячейка — `Ui.Box("slot is-filled")` + `Image` `slot__icon` (`ScaleToFit`, `PickingMode.Ignore`) + `Ui.Text` `slot__count`, класс `is-enchanted` по виду;
    - число: больше 999 → «999+»;
  - **пул:** ряды и ячейки создаются один раз и переиспользуются; спрайт и текст меняются только при изменении;
  - **подсказка** (`tooltip?.Attach` один раз на ячейку, функции читают текущий вид ячейки):
    - заголовок — имя;
    - текст строками: прибавки через запятую с ключами `$"урон {bonus:+0;-0;0}"` / `$"броня {bonus:+0;-0;0}"` (как `GearPanel.ItemHint`), нулевые не писать; затем `$"В запасе: {free}. На бойцах: {worn}."`;
  - **пустой запас:** вместо сетки `Ui.Text(…, "gear-stock__empty t-wrap")`. Без надетого — «Запас пуст: проложите сюда перенос из мастерской, где делают снаряжение.», с надетым — «Запас пуст: всё снаряжение на бойцах.»;
  - **для тестов:** `int InStock`, `IReadOnlyList<(string DefinitionId, string Count, bool Enchanted)> StockCells` (видимые, по порядку), `string EmptyText` (null, если скрыт), `VisualElement CellOf(string definitionId, bool worn)` для наведения подсказки.
- [ ] T010 [US1] В `Runtime/UI/Colony/InspectPanel.cs`:
  - поле `_gear = new GearStock(Ui.Require<VisualElement>(root, "inspect-gear"), context, tooltip)`, свойство `public GearStock Gear => _gear`;
  - в ветке `StorageRole.Armory` убрать строку «В инвентаре» и звать `_gear.Show(snapshot)`;
  - на всех остальных зданиях, в `RenderUnit` и в `Hide()` звать `_gear.Hide()`;
  - чужие строки (рецепты, «Подробнее», улучшения) не переписывать.
- [ ] T011 [US1] Тесты в `Tests/EditMode/ArmoryCardTests.cs` (`_interaction.SelectBuilding(<id склада экипировки>)`, `_hud.Refresh(_session.CurrentSnapshot)`):
  - **старт:** две ячейки (ржавый меч 1, латаная броня 1), `InStock` 2;
  - **`DebugGrantGear(3)`:**
    - 30 ячеек, ржавый меч и латаная броня 4, остальные 3, ровно 15 с `Enchanted`;
    - порядок слотов не убывает, внутри слота простые раньше зачарованных;
    - класс `is-enchanted` у каждой ячейки совпадает с `EquipmentDefinition.Enchanted` её вида;
  - **подсказка:** `_hud.Tooltip.Hover(_hud.Inspect.Gear.CellOf("…iron sword id…", false))`, `Title` = имя, `Body` содержит «урон +» и «В запасе: 4. На бойцах: 0.» (числа по факту);
  - **живое обновление:** ещё `DebugGrantGear(1)` и `Refresh` без повторного выбора — числа выросли;
  - **пустой запас:** `TestColony.With(CurrentSnapshot, equipment: пусто)` → `EmptyText` = строка пустого запаса, `StockCells` пуст;
  - **«999+»:** `TestColony.With(CurrentSnapshot, equipment: 1500 предметов одного вида)` → в ячейке «999+», в подсказке «В запасе: 1500.»;
  - **другие карточки:** на складе и на существе блок снаряжения скрыт.

**Checkpoint**: US1 работает сама по себе: склад экипировки показывает запас.

---

## Phase 4: User Story 2 — видно, чем склад отличается от склада экипировки (P1)

**Goal**: Обе карточки, подсказка каталога и окно награды говорят о назначении словами `ExplainStorage`; ячейки склада называют товар.

**Independent Test**: карточка склада, карточка склада экипировки и подсказка жетона склада экипировки в каталоге называют, что здание хранит, для чего и где лежит другое.

- [ ] T012 [US2] В `Runtime/UI/Colony/InspectPanel.cs` подписи `inspect-note`:
  - `StorageRole.Stockpile` и `StorageRole.Armory` → `string.Join("\n", _context.Session.ExplainStorage(definition))`, вместо «Хранит сырьё и полуфабрикаты от носильщиков.» и «Готовое снаряжение попадает в инвентарь отряда.»;
  - подпись бараков остаётся своей: её присваивание идёт после `switch` и перекрывает подпись `Stockpile`, как сейчас; проверить, что так и осталось.
- [ ] T013 [US2] В `Runtime/UI/Colony/InspectPanel.cs` — подсказка ячейки склада:
  - в `Slot` хранить товар, количество и вместимость ячейки (`StorageSlots.SlotCapacity(i, building.Capacity, building.SlotStackSize)`);
  - в `CreateSlot` — `_tooltip?.Attach(root, () => <имя товара или null>, () => <$"В ячейке: {amount} из {capacity}">)`;
  - пустая ячейка подсказки не даёт (заголовок null; проверить, как `HudTooltip` обрабатывает пустой заголовок, и при нужде не показывать).
- [ ] T014 [US2] Тесты в `Tests/EditMode/ColonyHudTests.cs` (её `SetUp` уже ставит склад, рынок и бараки):
  - подпись карточки склада (`Text("inspect-note")`) == строки `ExplainStorage(склад)` через `\n` и содержит имя склада экипировки;
  - наведение на заполненную ячейку склада: `Title` — имя товара, `Body` — «В ячейке: 30 из 50». Команды «положить товар» у сессии нет, поэтому снимок синтетический: `TestColony.With(CurrentSnapshot, buildings: …)`, где снимок склада заменён новым `BuildingSnapshot` с тем же `Id` и `slots: StorageSlots.Fill({IronOre: 30}, capacity, 50)`;
  - подсказка жетона склада экипировки в каталоге (`_hud.Tooltip.Hover(_hud.Catalog.BuyButton(BuildingKind.Armory))`) содержит все строки `ExplainStorage(склад экипировки)`;
  - `_session.DescribeReward(QuestReward.UnlockBuilding(BuildingKind.Armory)).Description` содержит те же строки.
- [ ] T015 [P] [US2] Тест в `Tests/EditMode/ArmoryCardTests.cs`: подпись карточки склада экипировки == строки `ExplainStorage(склад экипировки)` через `\n`.

**Checkpoint**: US1 + US2 — пункты 12 и 13 закрыты по существу.

---

## Phase 5: User Story 3 — видно, что надето на бойцах (P2)

**Goal**: Вторая сетка «На бойцах», в подсказке — кто носит; сумма сеток равна счёту задания.

**Independent Test**: двое бойцов с железными мечами. «На бойцах: 2», ячейка 2, подсказка называет обоих, в запасе на 2 меньше.

- [ ] T016 [US3] В `Runtime/UI/Colony/GearStock.cs`:
  - **сетка надетого:** заголовок `$"На бойцах: {worn}"` и ряды так же, как у запаса, тем же порядком; при нуле надетого скрыты;
  - **подсказка ячейки надетого:** третьей строкой `$"Носят: {names}."` — имена из `snapshot.Units` по `OwnerUnitId`, не больше шести, дальше `$"Носят: {names} и ещё {more}."`. Владелец не найден — считается, но не называется;
  - **для тестов:** `int OnFighters`, `IReadOnlyList<(string DefinitionId, string Count, bool Enchanted)> WornCells`.
- [ ] T017 [US3] Тесты в `Tests/EditMode/ArmoryCardTests.cs`:
  - **помощник** `WithOwners(GameSnapshot s, params (string ItemId, string UnitId)[] owners)` поверх `TestColony.With` (T006): снаряжение пересобрано через `new EquipmentSnapshot(id, _catalog.GetEquipment(defId), owner)`; `_hud.Refresh` принимает его как обычный снимок;
  - **бойцы:** двое настоящих существ сессии (нанять, как `ColonyHudTests.BuyUnit`), им — два железных меча после `DebugGrantGear(3)` → `OnFighters` 2, ячейка железного меча в `WornCells` «2», в запасе 1, подсказка содержит «Носят:» и оба имени;
  - **продан:** одного владельца убрать (`OwnerUnitId` null) → меч в запасе, «На бойцах: 1»;
  - **пал:** предмет убрать из снимка → его нет ни в одной сетке;
  - **инвариант** на каждом снимке выше: `InStock + OnFighters == snapshot.Equipment.Count`;
  - **всё надето:** `EmptyText` = «Запас пуст: всё снаряжение на бойцах.»;
  - **ничего не надето:** заголовка «На бойцах» нет;
  - **семь владельцев одного вида:** в подсказке шесть имён и «и ещё 1».

**Checkpoint**: US1–US3 вместе; карточка и задание считают одинаково.

---

## Phase 6: User Story 4 — книга объясняет оба склада (P2)

**Goal**: У склада и рынка есть страницы без цены; у хранилищ — абзацы `ExplainStorage` и чипы «Хранит»; «Подробнее» ведёт и со склада.

**Independent Test**: поиск «склад» в книге находит оба здания; на страницах назначение и чипы хранимого.

- [ ] T018 [US4] В `Runtime/UI/Colony/WikiPanel.cs`:
  - **список:** `Build` берёт все здания каталога, не только `Constructible`;
  - **`Building()` для нестроящегося:** в столбце цены `Num("—", cols[4])`, в подробностях вместо `Price(...)` — `Para("Стоит на острове с начала игры.")`;
  - **`BuildingChips`:** ссылается на страницу любого здания — убрать условие `building.Constructible`.
- [ ] T019 [US4] В `Runtime/UI/Colony/WikiPanel.cs` `Building().Detail` для `StorageRole` ≠ None:
  - **абзацы:** по абзацу на строку `session.ExplainStorage(building)` вместо одной фразы `DescribeRole`;
  - **Stockpile:** `Caption("Хранит")` и новый `GoodsChips(IEnumerable<ResourceDefinition>)` по образцу `BuildingChips` — `btn wiki-chip wiki-chip--link`, картинка `resource.Icon` (`wiki-icon`), имя, `UiFeel.Bind` → `Go(WikiSection.Goods, kind.ToString())`. Товары — те, что здание `Stores`, в порядке `ResourceKind`;
  - **Armory:** `Caption("Хранит")` и чипы видов `catalog.Equipment`, сначала простые, потом зачарованные; у картинки зачарованного класс `is-enchanted`. Ссылка — на товар каталога, чей `EquipmentId == ItemId`; нет такого — чип без перехода, но связан пустым действием, как сейчас нестроящиеся здания в `BuildingChips`;
  - **ячейка таблицы** «Главный рецепт» остаётся короткой `DescribeRole`.
- [ ] T020 [P] [US4] `UI/Styles/Wiki.uss`: `.wiki-icon.is-enchanted { border-width: 2px; border-radius: …; }` под размер `.wiki-icon`. Цвет приходит из `.is-enchanted` в `Theme.uss` (T008).
- [ ] T021 [US4] В `Runtime/UI/Colony/InspectPanel.cs` `RenderBuilding`: диск «Подробнее» у каждого здания — `SetWikiLink(_context.WikiLink(WikiSection.Buildings, building.Kind.ToString()), hint)`, без условия `Constructible`. Подсказка диска: «Что хранит и для чего.» для `StorageRole` ≠ None, иначе прежняя.
- [ ] T022 [US4] Тесты в `Tests/EditMode/NewPanelsTests.cs`:
  - **правка** `Book_OpensOnTheEntry_FromAHintAndFromTheInspectCard` по договорённости T001: рынок теперь с диском, книга открывается на его странице;
  - **новый** `Book_ExplainsTheWarehouseAndTheArmory`:
    - `book.Search("склад")` → в `RowNames` склад и склад экипировки;
    - `Go(Buildings, Warehouse)` → `DetailTexts` содержит строки `ExplainStorage(склад)` и «Стоит на острове с начала игры.»;
    - чипов «Хранит» столько же, сколько видов склад `Stores` (23). Считать через `book`-свойство или запрос `wiki-chip--link` в подробностях; при нужде добавить свойство для тестов в `WikiPanel`;
    - нажатие первого чипа открывает раздел «Товары» на этом товаре;
    - `Go(Buildings, Armory)` → строки `ExplainStorage(склад экипировки)`, 30 чипов, у 15 картинка с `is-enchanted`;
    - карточка склада → `UiFeel.Press(hud.Inspect.WikiButton)` → `book.SelectedName` = имя склада.

**Checkpoint**: US1–US4; книга и карточки говорят одно.

---

## Phase 7: Polish & сдача (после US1–US4)

- [ ] T023 Локализация (корень репозитория):
  - `python tools/localization/extract.py`;
  - перевести новые и изменённые ключи из контракта во все 15 файлов `tools/localization/translations/*.json`; английский — «warehouse» / «gear store», как в уже переведённых ключах;
  - `python tools/localization/validate.py <code>` по каждому языку;
  - `python tools/localization/build.py`;
  - меню `TrollStrategy/Dev/Setup Localization Fonts` (на копии, batch).

  Ключи «Хранит сырьё», «Снаряжение отряда», «Предметов на складе экипировки», «Хранит сырьё и полуфабрикаты от носильщиков.», «Готовое снаряжение попадает в инвентарь отряда.», «В инвентаре» перестают использоваться; убирает ли их `extract.py` сам — проверить.
- [ ] T024 [P] `Editor/Tools/HudSnapshots.cs`:
  - аргумент `-hudSize WxH` для размера `RenderTexture` (по умолчанию 1920×1080);
  - в сессии снимков поставить склад экипировки (`BuildBuildingCommand` на `FindFirstBuildingCell` или стартовой раскладкой);
  - снимки `{code}-warehouse`, `{code}-armory` (стартовое снаряжение), `{code}-armory-full`: `DebugGrantGear(3)` и `view.Refresh` снимком с владельцами для нескольких существ, как `WithOwners` в T017;
  - `{code}-wiki-warehouse`, `{code}-wiki-armory`.
- [ ] T025 Прогнать `HudSnapshots` на копии (`-hudLanguages ru,en,de`) в 1920×1080 и `-hudSize 1280x720`. Проверить по [quickstart.md](quickstart.md) §2: на 1280×720 карточка склада экипировки не выходит за экран, строка улучшения видна (SC-003). Снимки приложить к отчёту.
- [ ] T026 Весь EditMode-набор на копии зелёный (кроме исходных падений из T002), компиляция без ошибок. `TrollStrategy/Build Windows Player` → `Builds/Windows/build-result.txt` — успех.
- [ ] T027 Ручной проход по [quickstart.md](quickstart.md) §3 в сборке или Play Mode на копии:
  - мечи из кузницы растят ячейку;
  - выданный в расстановке меч после боя в «На бойцах»;
  - проданный боец вернул меч в запас.

---

## Phase 8: User Story 5 — название (P3, ждёт ответа на FR-017)

**Goal**: Если владелец выбрал новое имя — оно везде за одну правку. Вариант A («оставить») закрывает фазу без работы.

**Independent Test**: каталог, карточка, задание `armory-build`, книга и окно награды на ru и en показывают одно новое имя, старого нет.

- [ ] T028 [US5] `Content/Definitions/Building_Armory.asset`: `_displayName` — новое имя (YAML хранит `\uXXXX`; править через инспектор или скриптом с перекодировкой, проверить диффом).
- [ ] T029 [US5] `Editor/Setup/ProgressionContentSetup.cs`, задание `armory-build`: название и описания под новое имя; описание цели «пять предметов» сказать про снаряжение колонии, а не про склад. Перезаписать `Content/Definitions/Progression.asset` меню `TrollStrategy/Dev/Setup Progression` на копии. Дифф должен менять только тексты `armory-build`, иначе остановиться: порядок заданий может править другая сессия (пункт 7). Перенести в рабочее дерево по согласованию.
- [ ] T030 [P] [US5] `Runtime/UI/Colony/InspectPanel.cs`: подпись бараков «…носильщики унесут их на склад экипировки, к зачарователю или на рынок» переписать под новое имя в нужном падеже.
- [ ] T031 [P] [US5] `Bots/QuestPlanner.cs`: строка журнала `"склад экипировки"` → новое имя.
- [ ] T032 [US5] Локализация, как T023: новые ключи имени и заданий во всех 15 файлах; старые ключи со «складом экипировки» уходят.
- [ ] T033 [P] [US5] Документы (корень репозитория): `docs/GDD.md`, `docs/economy-balance.md`, `docs/campaign-bots.md` — новое имя. Записанные заметки плейтеста и спеки не трогать.
- [ ] T034 [US5] Проверка:
  - поиск старого имени по `unity/TrollStategy/Assets/Game` (кроме тестовых литералов вроде `ColonySimulationTests`) и по `tools/localization/translations` пуст;
  - весь EditMode зелёный;
  - `tools/bots/run-bots.ps1 -NoOpen` — все профили проходят кампанию;
  - снимки T025 на ru/en повторены.

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2 → US1–US4.** Ни одна правка файлов кода не начинается до T001: все они, кроме `HudSnapshots.cs`, сейчас грязные у других сессий.
- **US1 (Phase 3)** зависит только от Phase 2 (подпись карточки берёт `ExplainStorage` в US2, а сетка — нет).
- **US2 (Phase 4)** зависит от Phase 2; с US1 общий файл `InspectPanel.cs`, поэтому делать по очереди: US1 → US2.
- **US3 (Phase 5)** зависит от US1 (`GearStock`).
- **US4 (Phase 6)** зависит от Phase 2; T021 правит `InspectPanel.cs` — после US2.
- **Phase 7** — после тех историй, что входят в поставку.
- **US5 (Phase 8)** — после ответа владельца и после Phase 7. Независима от US1–US4, но правит те же тексты, поэтому идёт последней.

### Внутри историй

- Вёрстка и стили (T007, T008) — до `GearStock` (T009).
- Код истории — до её тестов в общем файле; тесты в новом файле (`ArmoryCardTests`) можно писать параллельно и запускать после.

## Parallel Opportunities

- **Phase 2:** T006 (новый файл тестов) параллельно с T003–T005, запуск — после них.
- **US1:** T008 (USS) параллельно с T007 (UXML); T009 — после обоих.
- **US2:** T015 параллельно с T012–T014.
- **US4:** T020 (USS) параллельно с T018–T019.
- **Polish:** T024 (`HudSnapshots.cs`, не грязный) можно начать сразу после US1.
- **US5:** T030, T031, T033 — разные файлы, параллельно.

```text
# US1, после T007:
T008 Theme.uss + ColonyHud.uss   ||   T009 GearStock.cs (после T008 для вида, логика независима)
# US4:
T018–T019 WikiPanel.cs   ||   T020 Wiki.uss
```

## Implementation Strategy

### MVP

Phase 1 → Phase 2 → US1 → US2. Это пункты 12 и 13 по существу: склад экипировки показывает снаряжение, обе карточки говорят, чем здания отличаются. Остановиться, прогнать T023, T025, T026 и показать владельцу.

### Дальше

1. **US3** — надетое и инвариант с заданием.
2. **US4** — книга.
3. **Polish** целиком: снимки на 1280×720, сборка, ручной проход.
4. **US5** — когда владелец ответит на FR-017.

## Notes

- T001: (записать ответ о чужих коммитах и о странице рынка)
- T002: (записать исходные падения EditMode на копии)
