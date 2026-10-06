# Tasks: Указатель обучения

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md)

**Tests**: EditMode-тесты обязательны (AGENTS.md: проверка на самой узкой границе). Кампания ботов, сборка и снимки Play Mode — в конце.

**Organization**: по историям спеки. US1 (указатель и первый работник) — MVP; US2 (перенос) и US3 (постановка) опираются на слой и модель шага из фазы 2.

Пути — от `unity/TrollStategy/Assets/Game/`, если не сказано иначе. Вся правка идёт через `C:\tmp\Troll006\patch006\apply.py` (анкерные замены и `new/`); настоящее дерево не трогается до проверки.

## Phase 1: Setup

- [x] T001 Копия проекта `C:\tmp\Troll006` (robocopy `Assets`, `Packages`, `ProjectSettings`, `Library`, `assets`, `docs`, `tools`; удалить `Library/ilpp.pid`, `burst.pid`)
- [x] T002 Скелет патча `C:\tmp\Troll006\patch006\`: `apply.py` с анкерными заменами (ровно одно вхождение, CRLF/LF и BOM как в файле), копированием `new/` и вливанием `new-keys.json` в `tools/localization/translations`
- [x] T003 [P] Графика: `UI/Sprites/hand.png` (белая рука, тёмный контур, тень) и `UI/Sprites/spot.png` (9-срезное мягкое окно) — скрипт `patch006/art/make_sprites.py`

## Phase 2: Foundational

- [x] T004 `Runtime/UI/Common/GuideStep.cs`: `GuideStep`, `GuideTarget`, `GuideTargetKind`
- [x] T005 `Runtime/UI/Common/GuideOverlay.cs`: вуаль из четырёх полос и окна `spot.png`, рука с лёгким «тычком», карточка `.sheet` «Шаг N из M», кнопка «к цели» у края, стрелка-резинка (Painter2D); всё `PickingMode.Ignore`, кроме кнопки; снятие вуали нажатием мимо окна
- [x] T006 `UI/Styles/Guide.uss` (только раскладка и токены темы) + `<Style>` в `UI/Uxml/ColonyHud.uxml` и `BattleHud.uxml`
- [x] T007 `Editor/Setup/UiSetup.cs`: узел `Guide` (порядок 28 в колонии, 20 в бою); поля `_guide` в `ColonyHud.cs`, `BattleHud.cs`; `Guide` в `ColonyHudRoots`, `BattleHudRoots` (необязательный)
- [x] T008 `Runtime/Presentation/Visuals/ScreenLocator.cs` и делегаты `MapToScreen`, `UnitToScreen`, `BuildingToScreen` в `ColonyHudContext`; связь в `GameBootstrap`
- [x] T009 `Runtime/Presentation/GameSettings.cs`: `TutorialHints` + строка «Подсказки обучения» в `MenuPanel` (настройки)
- [x] T010 `Runtime/Application/TutorialPlaces.cs`: `HireCell`, `BuildingCell`, `Warehouse`; `InteractionController.Focus(WorldPosition)`

**Checkpoint**: слой собирается, пустой шаг ничего не показывает, настройка переключается.

## Phase 3: User Story 1 — указатель ведёт к первому нажатию (P1) 🎯 MVP

**Goal**: на каждом шаге обучения вуаль с окном, рука и карточка; первый гоблин — одним нажатием у склада.

**Independent Test**: новая игра → окно и рука на жетоне гоблина → нажатие → гоблин у двери склада, вуаль ушла.

- [x] T011 [P] [US1] Тесты `Tests/EditMode/TutorialGuideTests.cs`:
  - первый шаг указывает на жетон, 1 из 1;
  - нажатие нанимает у двери склада и двигает камеру;
  - вкладка «Здания» → сначала вкладка «Существа»;
  - шаги «Своей шахты»;
  - шаги работы;
  - «Арена» → «В бой»;
  - после обучения и с выключенными подсказками шага нет;
  - пока шаг есть, мигание выключено
- [x] T012 [US1] Короткий путь в `InteractionController.BeginUnitPlacement` (FR-005)
- [x] T013 [US1] `Runtime/UI/Colony/ColonyGuide.cs`: шаги найма, постройки, работы, арены, «Отмена»; без шага у целей без нажатия
- [x] T014 [US1] `ColonyHudView`: строить `ColonyGuide` на `Refresh`, вести `GuideOverlay` в `Tick`, не мигать при указателе (FR-012), подъезд камеры раз за шаг к цели за краем (FR-007)
- [x] T015 [US1] `Runtime/UI/Battle/BattleGuide.cs` + `BattleHudView`: «Авторасстановка», боец, снаряжение, «Начать бой»; `IBattleScreen.LocateCells` в `BattleHud` и `BattleSceneController`
- [x] T016 [P] [US1] Тесты боя в `TutorialGuideTests`: пустое поле → «Авторасстановка»; броня → боец → предмет → «Начать бой»

**Checkpoint**: US1 проходит свои тесты; остальные истории ещё без коротких путей переноса и карты клеток.

## Phase 4: User Story 2 — перенос: откуда и куда (P2)

**Goal**: «Вывозить на склад» в «За работу!»; перенос двумя зданиями с подписанными шагами и стрелкой в «Первой выручке».

**Independent Test**: «За работу!» → шахта → «Вывозить на склад» засчитывает маршрут; «Первая выручка» → «Перенос» → склад → «Куда носить» → рынок с отметкой и стрелкой.

- [x] T017 [P] [US2] Тесты:
  - кнопка видна и доступна только при цели «носить на склад»;
  - недоступна без свободного гоблина;
  - назначает маршрут;
  - «1. Откуда носить» / «2. Куда носить»;
  - кружки целей на шаге «куда» с отмеченным рынком;
  - маршрут иконками в карточке задания;
  - шаги 1–5 указателя;
  - обновить `ColonyHudOrdersTests` (шаг «куда» теперь со списком)
- [x] T018 [US2] `InteractionController.HaulInspectedToWarehouse()` и `HaulToWarehouseBlocker` (FR-018)
- [x] T019 [US2] `InspectPanel`: кнопка «Вывозить на склад» над рядом действий (якоря вокруг чужих правок)
- [x] T020 [US2] `ContextBar`: подписи шагов, цели на шаге «куда», отметка `HaulTo`, `TargetButton(kind)`
- [x] T021 [US2] `QuestTracker`: иконки «откуда → куда» у цели-переноса
- [x] T022 [US2] `ColonyGuide`: короткий путь (здание → кнопка) и шаги 1–5 переноса; стрелка-резинка в `GuideOverlay` на шаге «куда»
- [x] T023 [US2] `Editor/Setup/ProgressionContentSetup.cs`: тексты «За работу!» и «Первой выручки»; пересобрать `Progression.asset`

## Phase 5: User Story 3 — куда поставить (P3)

**Goal**: свободные клетки видны, клетка у склада — «Сюда», рука на ней; место под шахту у склада.

**Independent Test**: второй гоблин в «За работу!» → клетки видны, окно на клетке у склада, клик туда нанимает.

- [x] T024 [P] [US3] Тесты:
  - `TutorialPlaces.HireCell` у двери склада и проходит `CanBuyUnits`;
  - шаг «Сюда» — клетка, 2 из 2;
  - место шахты проходит `CanPlaceBuilding`
- [x] T025 [US3] `PlacementPreviewRenderer`: сетка свободных клеток в обучении при включённых подсказках (R7)

## Phase 6: Polish & Cross-Cutting

- [x] T026 Тексты: `extract.py`, переводы новых ключей на 15 языков в `patch006/new-keys.json`, `validate.py`, `build.py`, «Setup Localization Fonts»
- [x] T027 `AGENTS.md`: строка об исключении вуали обучения
- [x] T028 Пакетно на копии: «Setup Progression Content», «Setup UI», полный EditMode — 508: 504 прошли, 0 упали, 4 пропущены (`[Explicit]` прогоны ботов); `TutorialGuideTests` 14/14
- [x] T029 Кампания ботов `BotMenu.RunAllBatch`: все 5 профилей до конца, время как до фичи — 34/34 у всех; отчёты с патчем и без него совпадают побайтно (76.4 / 83.0 / 49.7 / 51.8 / 36.7 мин)
- [x] T030 Сборка `PlayerBuild.BuildWindows`, `build-result.txt` — `Succeeded errors=0`, 6:36; предупреждения только шейдеров Sentis
- [x] T031 Снимки Play Mode каждого шага (`Editor/Tools/GuideShots.cs` только в копии), просмотреть каждый — 30 кадров, все просмотрены
- [x] T032 Отчёт: пути скрипта патча, результаты, снимки, список файлов настоящего дерева

## Dependencies

- Фаза 2 блокирует все истории; T005 зависит от T003, T004; T007 — от T006.
- US1 (T012–T016) — MVP; US2 и US3 независимы друг от друга, обе зависят от T013/T014.
- T026 — после всех текстов (T013, T015, T019–T023, T009); T028–T031 — после всего кода.

## Parallel Opportunities

- T003 параллельно с T004–T010.
- Тесты каждой истории (T011, T016, T017, T024) пишутся параллельно с кодом своей истории.
