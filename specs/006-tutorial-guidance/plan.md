# Implementation Plan: Указатель обучения

**Branch**: `006-tutorial-guidance` (код — в рабочей ветке `feature/unity-migration`, патчем поверх чужих незакоммиченных правок) | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-tutorial-guidance/spec.md` (смесь 1 + 3, Clarifications 2026-10-06)

## Summary

**Указатель на каждом шаге обучения.** Колония и экран расстановки получают по слою указателя. На каждом шаге обучения в слое:

- светлая вуаль с мягким окном вокруг цели;
- белая рука, показывающая на цель;
- белая карточка: «Шаг N из M», действие, одна строка «зачем».

Шаг выводится из текущей цели задания, режима взаимодействия и того, что открыто на экране. Своего прогресса указатель не хранит.

**Короткие пути.** Оба — уже существующими командами:

- первый гоблин нанимается у двери склада одним нажатием на жетон;
- в «За работу!» в карточке шахты есть «Вывозить на склад».

**Перенос двумя зданиями.** Его учит «Первая выручка»:

- нижняя панель подписывает шаги «1. Откуда носить» / «2. Куда носить»;
- на втором шаге показывает цели и отмечает цель задания;
- от источника к курсору тянется стрелка;
- карточка задания показывает маршрут иконками.

**Постановка второго гоблина.** Видны свободные клетки, клетка у склада — «Сюда».

**Настройки и тексты.**

- Подсказки выключаются в меню.
- Пока указатель показан, мигание выключено.
- Тексты «За работу!» и «Первой выручки» переписаны под новые пути.
- В AGENTS.md — одна строка об исключении из правила «воздуха».

Подробности решений — [research.md](research.md), модели и таблицы шагов — [data-model.md](data-model.md).

## Technical Context

**Language/Version**: C# 9 (Unity 6000.6)

**Primary Dependencies**: UI Toolkit (UXML/USS, `generateVisualContent`/Painter2D для стрелки), Input System; новых пакетов нет

**Storage**: N/A для кампании. Одна настройка в PlayerPrefs (`settings.tutorialHints`).

**Testing**:

- Unity Test Framework, EditMode, через `TestUi` — HUD без сцены.
- Полный набор EditMode в пакетном режиме на копии.
- Кампания ботов `TrollStrategy.Bots.BotMenu.RunAllBatch` — все 5 профилей.
- Сборка `TrollStrategy.Editor.Setup.PlayerBuild.BuildWindows`.
- Снимки Play Mode каждого шага (`Editor/Tools/TutorialShots.cs` только в копии).

**Target Platform**: Windows player, WebGL (itch.io); сенсорный экран — тот же указатель

**Project Type**: однопользовательская игра Unity (desktop/WebGL)

**Performance Goals**:

- Шаг пересчитывается на обновлении HUD (команда, шаг симуляции). Положение окна — каждый кадр: несколько `worldBound` и проекций.
- Карта свободных клеток — проверка `CanBuyUnits` на клетках сетки при входе в режим и не чаще раза в 0,25 с.

**Constraints**:

- Все элементы слоя не ловят указатель, кроме кнопки «к цели». Нажатия проходят в игру (`UIInputUtils` спрашивает `panel.Pick`).
- Цвета — токены темы; раскладка — в `Guide.uss`.
- Чужие незакоммиченные правки в `InspectPanel`, `ColonyHud*`, `CatalogPanel`, `InteractionController`, `GameSession`, USS остаются: правка — анкерным скриптом.

**Scale/Scope**: 9 шагов обучения; около 15 изменённых файлов, 8 новых файлов кода и графики, около 40 новых ключей перевода на 15 языков

## Constitution Check

*Проверка до исследования и повторно после дизайна — проходит.*

| Принцип | Как выполняется |
|---|---|
| I. Single Owner of Game State | Указатель только читает снимок и режим; короткие пути отдают `BuyUnitsCommand` и `AssignHaulCommand` через `GameSession.Dispatch`. Новых полей в `GameState` нет. |
| II. Layered Architecture | Домен не меняется. Приложение: `TutorialPlaces` (запросы) и `InteractionController` (короткие пути, `Focus`). UI: `GuideStep`, `ColonyGuide`, `BattleGuide`, `GuideOverlay`. Презентация: `ScreenLocator`, карта свободных клеток. Bootstrap только связывает делегаты. |
| III. Deterministic Simulation | Симуляция не меняется. Поиск места — детерминированный обход колец. |
| IV. Content Owns the Numbers | Новых игровых чисел нет. Тексты заданий — в `ProgressionContentSetup`. |
| V. Bots Play by Player Rules | Короткие пути — те же команды, что боты уже отдают. Ботам ничего не добавляется и не отнимается; прогон ботов должен совпасть. |
| VI. Smallest Complete Vertical Slice | Только шаги обучения; после обучения указателя нет (FR-013). «Вывозить на склад» — только пока задание просит носить на склад. |
| VII. Verified at the Narrowest Boundary | EditMode-тесты на модель шагов, короткие пути, `ContextBar` и карточку задания; снимки Play Mode — на вид. |

Исключение из правила UI «воздух» (вуаль) согласовано владельцем, записано в AGENTS.md и в Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/006-tutorial-guidance/
├── spec.md              # требования, Clarifications 2026-10-06
├── plan.md              # этот файл
├── research.md          # решения R1–R12
├── data-model.md        # GuideStep, таблицы шагов, TutorialPlaces
├── tasks.md             # задачи по историям
└── checklists/requirements.md
```

### Source Code (`unity/TrollStategy/Assets/Game`)

```text
Runtime/Application/
├── TutorialPlaces.cs            # NEW: клетка найма и место здания у двери склада
└── InteractionController.cs     # + короткий путь первого найма, HaulInspectedToWarehouse, Focus
Runtime/Presentation/
├── GameSettings.cs              # + TutorialHints
├── Visuals/ScreenLocator.cs     # NEW: карта, существо, здание → пиксели экрана
├── Visuals/PlacementPreviewRenderer.cs  # + карта свободных клеток в обучении
└── Battle/IBattleScreen.cs, BattleSceneController.cs  # + LocateCells
Runtime/Bootstrap/GameBootstrap.cs       # + делегаты ScreenLocator в контекст HUD
Runtime/UI/Common/
├── GuideStep.cs                 # NEW: шаг и цель
└── GuideOverlay.cs              # NEW: вуаль с окном, рука, карточка, стрелка, кнопка «к цели»
Runtime/UI/Colony/
├── ColonyGuide.cs               # NEW: шаг колонии из задания, режима и экрана
├── ColonyHudContext.cs, ColonyHudRoots.cs, ColonyHud.cs, ColonyHudView.cs  # + слой указателя
├── ContextBar.cs                # «1. Откуда носить» / «2. Куда носить», цели шага «куда», отметка HaulTo
├── InspectPanel.cs              # + «Вывозить на склад»
├── QuestTracker.cs              # + маршрут иконками у цели-переноса
└── MenuPanel.cs                 # + «Подсказки обучения»
Runtime/UI/Battle/
├── BattleGuide.cs               # NEW: шаг расстановки
└── BattleHud.cs, BattleHudRoots.cs, BattleHudView.cs  # + слой указателя, LocateCells
Editor/Setup/UiSetup.cs          # + узел Guide в обоих HUD
Editor/Setup/ProgressionContentSetup.cs  # тексты «За работу!» и «Первой выручки»
UI/Styles/Guide.uss              # NEW: раскладка слоя
UI/Uxml/ColonyHud.uxml, BattleHud.uxml  # + <Style src="../Styles/Guide.uss" />
UI/Sprites/hand.png, spot.png    # NEW: рука и мягкое окно
Tests/EditMode/TutorialGuideTests.cs     # NEW
Tests/EditMode/ColonyHudOrdersTests.cs   # шаг «куда» теперь со списком целей
```

Плюс вне Unity:

- `AGENTS.md` — одна строка;
- `tools/localization/translations/*.json` — новые ключи.

**Structure Decision**: один игровой проект Unity, слои по AGENTS.md. Новый UI-код — в `UI/Common` (общий для двух HUD) и в папках экранов.

## Delivery and Verification

- **Копия.** `C:\tmp\Troll006`: robocopy проекта с `Library`, `assets`, `docs`, `tools`; `ilpp.pid` и `burst.pid` удалены.
- **Патч.** `C:\tmp\Troll006\patch006\`:
  - `apply.py <repo root>` — анкерные замены, каждый якорь ровно один раз, CRLF/LF и BOM сохраняются;
  - копирование `new/`;
  - вливание ключей перевода из `new-keys.json`.

  Скрипт прогоняется на копии; то же применение на настоящем дереве — дело родительской сессии.
- **После применения меню:**
  1. «Setup Progression Content»;
  2. «Setup UI»;
  3. `extract.py` → `build.py` → «Setup Localization Fonts».
- **Проверка на копии:**
  - полный EditMode;
  - кампания ботов, все 5 профилей до конца;
  - сборка Windows;
  - снимки Play Mode каждого шага обучения (просмотрены).

## Complexity Tracking

| Отступление | Зачем | Чем проще не вышло |
|---|---|---|
| Вуаль во время шагов обучения (правило «воздуха» допускает её только у окон, останавливающих карту) | Решение владельца 2026-10-06: окно вокруг цели на каждом шаге | Только рука без вуали — это смесь 1, владелец выбрал 1 + 3 |
| Делегаты местоположения в `ColonyHudContext` и `IBattleScreen.LocateCells` | Окно и рука на здании, существе, клетке и бойце | Цели только в HUD не покрывают шаги на карте и на поле |
