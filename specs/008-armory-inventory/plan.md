# Implementation Plan: Склад экипировки показывает снаряжение

**Branch**: `008-armory-inventory` (код — в рабочей ветке `feature/unity-migration`) | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-armory-inventory/spec.md`

## Summary

Отвечаем на пункты 12–13 плейтеста экраном и словами, правила не трогаем:

- **Карточка склада экипировки** вместо строки «В инвентаре: N» показывает две сетки: «В запасе» и «На бойцах». Ячейки те же, что у склада: картинка и число на вид предмета; у зачарованных кайма цвета зачарователя. Подсказка ячейки называет предмет, прибавку, сколько в запасе и на бойцах и кто носит.
- **Обе карточки, подсказка каталога, окно награды и книга** говорят о назначении одними словами из одного места (`GameSession.ExplainStorage`): склад — сырьё и полуфабрикаты для зданий и рынка, снаряжение не берёт; склад экипировки — готовое снаряжение для бойцов, без предела, обратно не вывозят.
- **Книга** получает страницы склада и рынка (стоят с начала игры, без цены), а у хранилищ — чипы «Хранит» со ссылками на товары.
- **Ячейки склада** получают подсказку с названием товара.
- **Подпись цели** «предметы снаряжения» называет всё снаряжение колонии, как она его и считает.
- **Переименование** (история 5) — отдельная последняя фаза, ждёт ответа владельца на FR-017.

Техника (подробно в [research.md](research.md)):

- **Группировка** — новый UI-класс `GearStock` над `snapshot.Equipment` и каталогом. Нового состояния и снимка нет (R1, R2).
- **Сетки** стоят внутри прокрутки карточки, явными рядами по 8 (R3).
- **Слова** — `DescribeRole` (коротко) и новый `ExplainStorage` в `GameSession`; имена зданий в предложениях берутся из данных (R6).

## Technical Context

**Language/Version**: C# 9 (Unity 6000.6, .NET Standard 2.1 profile)

**Primary Dependencies**: Unity 6000.6, UI Toolkit (UXML/USS); новых пакетов нет

**Storage**: N/A — состояние не меняется

**Testing**:

- Unity Test Framework, EditMode (`Assets/Game/Tests/EditMode`): новый `ArmoryCardTests` на `TestUi`, правки `NewPanelsTests` и `ColonyHudTests`.
- `HudSnapshots` (без Play Mode) на ru/en/de, 1920×1080 и 1280×720.
- Play Mode или сборка на копии проекта для ручного прохода.

**Target Platform**: Windows player (`TrollStrategy/Build Windows Player`), WebGL (itch.io)

**Project Type**: однопользовательская игра (Unity desktop/WebGL)

**Performance Goals**:

- Карточка обновляется на каждом снимке, как сейчас. `GearStock` группирует до нескольких сотен предметов за проход, без новых элементов: ячейки, ряды и подсказки берутся из пула. Текст и картинку ячейки меняет только при изменении.
- Книга строит страницы при открытии, как сейчас.

**Constraints**:

- Правила, команды, снимки и боты не меняются (FR-015).
- UI — только UI Toolkit по `AGENTS.md` § UI: цвета в `Theme.uss`, картинка и число, названия в `HudTooltip`, тексты через `Ui.SetText` / `Ui.Text`.
- Тексты — через локализацию (русский текст — ключ) на все 16 языков.
- Структура префаба `UI` не меняется: новый контейнер лежит внутри `Inspect.uxml`, перестраивать префаб через `UiSetup` не нужно.
- Файлы, которые сейчас правят другие сессии, меняются только после их коммита: `InspectPanel.cs`, `Inspect.uxml`, `WikiPanel.cs`, `GameSession.cs`, `NewPanelsTests.cs`, `ColonyHud.uss`, `Theme.uss`, `Wiki.uss`.

**Scale/Scope**:

- 30 видов снаряжения (15 простых, 15 зачарованных), 23 вида сырья на складе.
- 2 карточки, 3 страницы книги (склад, рынок, склад экипировки) и правка страницы бараков.
- Около 15 новых строк интерфейса.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Принцип | Как план его выполняет | Итог |
|---|---|---|
| **I. Единый владелец состояния** | Снаряжение — только `GameState.Equipment`. Карточка читает его из снимка и группирует на экране, своего счёта не держит. Сумма сеток равна счёту цели `OwnEquipment` по построению. | ✅ |
| **II. Слои** | Группировка и вёрстка — `Runtime/UI/Colony/GearStock.cs`. Слова о назначении — `Runtime/Application/GameSession.cs`, рядом с `DescribeRole`/`DescribeBuilding`, которыми уже пользуются каталог, награда и книга. Домен не меняется. | ✅ |
| **III. Детерминизм** | Симуляция не меняется. Порядок ячеек детерминирован: слот, зачарован, индекс в каталоге. | ✅ |
| **IV. Числа в контенте** | Картинки, прибавки, зачарование — из `EquipmentDefinition`. Что хранит склад — из `BuildingDefinition.Stores`. Имена зданий в предложениях — из данных. Своих чисел у UI нет (кроме вёрстки: 8 ячеек в ряду). | ✅ |
| **V. Боты по правилам игрока** | Новых действий и целей нет. Подпись цели меняется только словами, счёт прежний. | ✅ |
| **VI. Минимальный полный срез** | Один новый UI-класс, ни нового состояния, ни фреймворка. Каждая история — экран, слова, тест. Переименование отделено и ждёт решения. | ✅ |
| **VII. Проверка на узкой границе** | Сетки проверяются `ArmoryCardTests` на `TestUi` синтетическими снимками. Правила владения снаряжением уже покрыты тестами домена. Книга — `NewPanelsTests`. Вид — `HudSnapshots`. Перед сдачей — весь EditMode, компиляция, Windows-сборка. | ✅ |
| **Платформа и UI** | Ячейки — те же классы, что у склада. Кайма — `--magic` в `Theme.uss`. Подсказки ячеек — `HudTooltip.Attach`. Новые кнопки — только чипы «Хранит» в книге, через `UiFeel.Bind`, как `BuildingChips`; диск «Подробнее» уже связан. Тексты — `Ui.SetText`/`Ui.Text`. Без `UiSetup`: структура документов прежняя. | ✅ |
| **Телеметрия** | `CampaignTelemetry.Schema` не меняется, `docs/analytics.md` синхронизировать не нужно. | ✅ |

Post-design re-check (после Phase 1): нарушений нет, Complexity Tracking пуст.

## Project Structure

### Documentation (this feature)

```text
specs/008-armory-inventory/
├── spec.md              # спека
├── checklists/
│   └── requirements.md  # проверка спеки
├── plan.md              # этот файл
├── research.md          # решения R1–R11
├── data-model.md        # вид на экране; состояние и снимок без изменений
├── quickstart.md        # как проверить фичу целиком
├── contracts/
│   └── armory-ui.md     # что показывают карточки, каталог, награда и книга; тексты
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

Все пути — от `unity/TrollStategy/Assets/Game/`.

```text
Runtime/Application/
└── GameSession.cs            # DescribeRole (новые короткие фразы), ExplainStorage (НОВЫЙ метод),
                              # DescribeBuilding через ExplainStorage, DescribeGoal(OwnEquipment)

Runtime/UI/Colony/
├── GearStock.cs              # НОВЫЙ: две сетки снаряжения (запас, на бойцах), пул ячеек, подсказки
├── InspectPanel.cs           # склад экипировки → GearStock; подписи из ExplainStorage; подсказки ячеек
│                             # склада; диск «Подробнее» у всех зданий; GearStock.Hide на прочих карточках
└── WikiPanel.cs              # все здания каталога; нестроящиеся без цены; ExplainStorage; «Хранит» чипами
                              # со ссылкой на товар; BuildingChips ссылаются на любое здание

UI/Uxml/Colony/Inspect.uxml   # + <VisualElement name="inspect-gear"> внутри inspect-rows-scroll
UI/Styles/ColonyHud.uss       # .gear-stock, .gear-stock__row, .gear-stock__empty; толщина каймы .slot.is-enchanted
UI/Styles/Theme.uss           # .is-enchanted { border-color: var(--magic) }
UI/Styles/Wiki.uss            # толщина каймы .wiki-icon.is-enchanted

Editor/Tools/HudSnapshots.cs  # снимки warehouse, armory, armory-full, wiki-warehouse, wiki-armory; аргумент -hudSize WxH

Tests/EditMode/
├── ArmoryCardTests.cs        # НОВЫЙ: сетки, порядок, кайма, подсказки, пустые состояния, инвариант суммы, 999+
├── ColonyHudTests.cs         # подпись склада, подсказка ячейки склада, подсказка жетона в каталоге
└── NewPanelsTests.cs         # книга: склад и рынок со страницами, «Хранит», поиск «склад», «Подробнее»

tools/localization/…          # новые ключи и переводы на 15 языков

# Только история 5 (переименование, по ответу на FR-017):
Content/Definitions/Building_Armory.asset      # _displayName
Editor/Setup/ProgressionContentSetup.cs        # задание armory-build: название и описания
Content/Definitions/Progression.asset          # перезапись меню TrollStrategy/Dev/Setup Progression
Runtime/UI/Colony/InspectPanel.cs              # подпись бараков
Bots/QuestPlanner.cs                           # строка журнала
docs/GDD.md, docs/economy-balance.md, docs/campaign-bots.md
```

**Structure Decision**: структура проекта Unity и префаба `UI` не меняется. Один новый файл в `Runtime/UI/Colony` (рядом с `RecipeList` и `StaffList`, тем же образцом: класс над своим контейнером, `Show`/`Hide`, свойства для тестов) и один новый тест. Остальное — правки существующих файлов. Все они, кроме `HudSnapshots.cs`, сейчас грязные у других сессий, поэтому правки начинаются после их коммита (tasks T001).

## Порядок реализации и зависимости

1. **Дождаться чужих коммитов:** карточка здания (другая сессия), удаление «Нанять» (родительская). Согласовать с автором `NewPanelsTests` разворот «у рынка нет страницы» (R7).
2. **Слова (основа для US1–US4):** `ExplainStorage`, новые `DescribeRole`, `DescribeBuilding` через них, `DescribeGoal(OwnEquipment)`. Тесты слов.
3. **US1:** `GearStock` (сетка запаса), `inspect-gear` в UXML, стили, подключение в `InspectPanel`. Тесты.
4. **US2:** подписи обеих карточек из `ExplainStorage`, подсказки ячеек склада. Подсказка каталога и окно награды идут сами через `DescribeBuilding`. Тесты.
5. **US3:** сетка «На бойцах», подсказка «Носят», инвариант суммы. Тесты.
6. **US4:** книга — все здания, нестроящиеся без цены, «Хранит», диск «Подробнее» у всех. Тесты.
7. **Локализация:** extract, переводы, validate, build, шрифты.
8. **Снимки и сдача:** `HudSnapshots` (1920×1080 и 1280×720), весь EditMode, Windows-сборка, ручной проход.
9. **US5 (после ответа владельца):** переименование одной правкой, повторная локализация, прогон ботов.

## Complexity Tracking

Нарушений Constitution Check нет.
