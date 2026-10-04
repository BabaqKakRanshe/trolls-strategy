# Implementation Plan: Прогрессия боёв на арене

**Branch**: `001-arena-progression` (код — в рабочей ветке `feature/unity-migration`) | **Date**: 2026-10-04 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-arena-progression/spec.md`

## Summary

Лестница арены из 30 уровней уже есть. Делаем из неё прогрессию с риском и наградой:

- **Окно арены до боя показывает:**
  - силу врагов;
  - оценку лучшего отряда колонии (сильнее, на равных, слабее);
  - всю награду;
  - ставку, призовой фонд и цену поражения.
- **Бой стоит ставки.** Повторные победы платят из одного призового фонда арены, который пополняется со временем.
- **Поражение** сжигает ставку и павших с их снаряжением, удваивает отдых уровня и закрывает вершину лестницы.
- **Ничья** платит долю награды.
- **Уровни отличаются:**
  - расстановками врагов;
  - снаряжением врагов с 10 уровня;
  - чемпионом на вехах 5, 10, …, 30;
  - трофеями по всей лестнице;
  - окружением с откатом на «Луг».
- **Бараки** показывают высший уровень и следующую веху. После цепочки заданий задание арены ведёт по вехам.
- **Правила общие для всех.** Все правила живут в домене и приложении, игрок и боты получают их через `GameSession`. Все числа — в данных: `EconomyConfig` для арены целиком, `BattleMissionDefinition` для уровня.

Техника (подробно в [research.md](research.md)):

- **Фонд.** Два целых в `GameState`, накопление считается лениво по `ActiveTimeMs`, отдельного тика нет.
- **Закрытие вершины.** Уровень удаляется из `Progress.UnlockedMissions`, второго источника правды нет.
- **Оценка отряда.** Агрегат Ланчестера по тем же `BattleFighterInput`, что получает бой.
- **Снаряжение врагов** заменяет часть плоской прибавки уровня. Поэтому калибровка «сложной арены» (§12 `docs/economy-balance.md`) сохраняется.

## Technical Context

**Language/Version**: C# 9 (Unity 6000.6, .NET Standard 2.1 profile)

**Primary Dependencies**: Unity 6000.6, UI Toolkit (UXML/USS), URP; no new packages

**Storage**: N/A — сохранений нет. Новое состояние арены — поля `GameState`, которые копирует `Clone()`.

**Testing**:

- Unity Test Framework, EditMode: `Assets/Game/Tests/EditMode`.
- Кампания ботов (`TrollStrategy/Bots/Run Campaign Bots` или `tools/bots/run-bots.ps1`).
- `HudSnapshots` для вида окна арены.
- Play Mode smoke на копии проекта.

**Target Platform**: Windows player (основная проверка `TrollStrategy/Build Windows Player`), WebGL (itch.io)

**Project Type**: однопользовательская игра (Unity desktop/WebGL)

**Performance Goals**:

- Окно арены обновляется раз в 0,5 с и не пересчитывает оценку отряда без изменений. Оценка кэшируется по ревизии сессии и уровню.
- Оценка на 30 уровнях — меньше 1 мс.
- Бой по-прежнему считается одним вызовом `BattleSimulation.Run` в команде старта.

**Constraints**:

- Домен детерминирован, без часов и случайности вне `RewardDice`.
- Числа только в контенте.
- Боты — только через `GameSession`.
- UI — только UI Toolkit по правилам `AGENTS.md` § UI.
- Тексты — через локализацию (ключ — русский текст) на все 14 языков.
- Телеметрия не меняется (research R13).

**Scale/Scope**:

- 30 уровней арены, 4 расстановки, 6 вех, 6 окружений (арт пяти из них пока не готов).
- Около 40 новых строк интерфейса.
- 5 профилей ботов.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Принцип | Как план его выполняет | Итог |
|---|---|---|
| **I. Единый владелец состояния** | Фонд арены — `GameState.ArenaFundPayouts` и `ArenaFundSinceMs`. Закрытие вершины — удаление из `Progress.UnlockedMissions`. Высший уровень за всё время — уже существующий `HighestMissionLevel`. Ставка и выплаты меняют `Gold` внутри `StartBattleCommand`, на клоне состояния, атомарно. Своего состояния у окна арены нет: выбранный уровень — UI-состояние, как сейчас. | ✅ |
| **II. Слои** | Расчёт фонда, доли ничьей и оценки отряда — в `Runtime/Domain`, без Unity. Сборка лучшего отряда, ставка и предложение уровня — в `Runtime/Application`. Окно арены и карточка Бараков читают `GameSession`. Жетоны снаряжения врагов и чемпион — `Runtime/Presentation/Battle`. | ✅ |
| **III. Детерминизм** | Фонд считается по `ActiveTimeMs`, который идёт шагами по 250 мс. Награда за ничью берёт тот же бросок `RewardDice`, что и победа, — новых потоков случайности нет. Расстановки и снаряжение врагов задаются при настройке контента, бой их не выбирает. | ✅ |
| **IV. Числа в контенте** | Темп и предел фонда, доля ставки, множитель отдыха после поражения и порог оценки — поля `EconomyConfig`. Расстановка, вехи, чемпион, снаряжение врагов, трофеи и окружение — поля `BattleMissionDefinition`, их пишет `ArenaContentSetup`. Шаг вех задания — поле `ProgressionDefinition`. Баланс проверяется прогоном ботов и записывается в `docs/economy-balance.md`. | ✅ |
| **V. Боты по правилам игрока** | `BattlePlanner` читает то же предложение уровня (`GameSession.ArenaOffer`), что и окно арены: оценку, ставку, фонд. Бой он начинает той же `StartBattleCommand`. Задание-веха — существующий `ReachArenaLevel`, его `QuestPlanner` уже умеет. | ✅ |
| **VI. Минимальный полный срез** | Каждая история — правило, контент, UI и тест. Новых фреймворков нет. Окружения — только раздача и откат, без нового арта. Телеметрия не меняется. Два новых класса домена (`ArenaFund`, `BattleOdds`) — ровно то, что требуют правила. | ✅ |
| **VII. Проверка на узкой границе** | Правила проверяются EditMode-тестами домена и сессии, окно — тестами `ArenaPanel` на `TestUi`, контент — тестами лестницы. Бой проверяется сценой в Play Mode smoke. Перед сдачей — полный набор тестов, компиляция, Windows-сборка и прогон ботов. | ✅ |
| **Платформа и UI** | Кнопки — `UiFeel.Bind`/`SetAvailable`. Цвета — только в `Theme.uss`. Числа — картинка плюс число. Тексты — `Ui.SetText`. Трофеи и существо — картинками. Новые панели — в существующих UXML арены; если структура меняется, через `UiSetup`. | ✅ |
| **Телеметрия** | `CampaignTelemetry.Schema` не меняется, поэтому `docs/analytics.md` синхронизировать не нужно (research R13). | ✅ |

Post-design re-check (после Phase 1): нарушений нет, Complexity Tracking пуст.

## Project Structure

### Documentation (this feature)

```text
specs/001-arena-progression/
├── plan.md              # этот файл
├── research.md          # решения R1–R14
├── data-model.md        # состояние, контент, снимки
├── quickstart.md        # как проверить фичу целиком
├── contracts/
│   ├── session-arena.md # что GameSession отдаёт окну, Баракам и ботам
│   └── arena-ui.md      # что показывает окно арены, карточка Бараков и экран итога
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

Все пути — от `unity/TrollStategy/Assets/Game/`.

```text
Runtime/Domain/
├── ArenaFund.cs              # НОВЫЙ: ленивый фонд арены (Available, NextInMs, Take)
├── BattleOdds.cs             # НОВЫЙ: агрегат Ланчестера → OddsGrade
├── BattleSimulation.cs       # + BattleReport.DefeatedShare(enemies)
├── GameState.cs              # + ArenaFundPayouts, ArenaFundSinceMs; PendingBattleReward.Draw; Clone
└── Progression.cs            # Repeat(): ReachArenaLevel растёт на шаг вех, до потолка

Runtime/Content/
├── EconomyConfig.cs          # + раздел «Арена»: фонд, ставка, отдых после поражения, порог оценки
├── BattleMissionDefinition.cs# + formation, milestone, champion %, enemy gear, biome; BattleEnemyStart.Gear/Champion
├── ArenaBiome.cs             # НОВЫЙ: enum окружений и BattleFormation
└── ProgressionDefinition.cs  # + RepeatArenaLevelStep, RepeatArenaLevelCap

Runtime/Application/
├── BattleApplication.cs      # Start: ставка, фонд, ничья, цена поражения, закрытие вершины, снаряжение врагов
├── ArenaOffer.cs             # НОВЫЙ: ArenaOfferSnapshot + сборка лучшего отряда и оценки
├── GameSession.cs            # + ArenaOffer(mission), ArenaFund; кэш оценки по ревизии
└── GameSnapshot.cs           # BattleRewardSnapshot.Draw

Runtime/UI/Colony/
├── ArenaPanel.cs             # сила врагов, оценка, трофеи, фонд, ставка, «на кону», веха, окружение
├── InspectPanel.cs           # Бараки: высший уровень арены, следующая веха
└── BattleRewardOverlay.cs    # подпись «за ничью»
Runtime/UI/Battle/BattleHudView.cs, ReplayPanel  # итог: сгоревшая ставка, закрытый уровень
Runtime/Presentation/Battle/
├── BattleBoardView.cs        # жетоны снаряжения у врагов, чемпион
├── BattleFighterView.cs      # SetChampion (масштаб, рамка полосы здоровья)
└── BattleSceneController.cs  # передаёт снаряжение врагов и цену боя экрану
UI/Uxml/Colony/Arena*.uxml, UI/Styles/Arena.uss, WorldUi.uss   # новые строки окна, чемпион

Editor/Setup/
├── ArenaContentSetup.cs      # расстановки, вехи, чемпионы, снаряжение врагов, трофеи, окружения
├── ArenaPrefabBuilder.cs     # больше не назначает окружение миссиям (только ArenaContentSetup)
└── ProgressionContentSetup.cs# повторяемое «Арена» → задание-веха (10, +5 за круг, до 30)

Bots/
├── BattlePlanner.cs          # выбор уровня по оценке, ставка, фонд; без своего «потолка»
├── BotRun.cs, BotReport*.cs  # доля золота с арены, оплаченные повторы, ничьи, закрытия
└── QuestPlanner.cs           # без изменений (ReachArenaLevel уже есть)

Tests/EditMode/
├── ArenaFundTests.cs         # НОВЫЙ: накопление, предел, выплата, пустой фонд, чередование
├── ArenaStakeTests.cs        # НОВЫЙ: ставка, поражение, закрытие вершины, ничья, отдых
├── BattleOddsTests.cs        # НОВЫЙ: ступени и калибровка по BattleSimulation
├── ArenaLadderContentTests.cs# НОВЫЙ: расстановки соседей, вехи, снаряжение, трофеи, окружения
├── ArenaPanelTests.cs        # НОВЫЙ или в NewPanelsTests: что показывает окно
└── ProgressionTests.cs       # задание-веха в повторяемом круге

docs/economy-balance.md       # §13: прогон ботов и выбранные числа
tools/localization/…          # новые ключи и переводы на 14 языков
```

**Structure Decision**: структура проекта Unity не меняется. Новые файлы лежат рядом с соседями по слою. Правила — в двух новых файлах домена (`ArenaFund`, `BattleOdds`) и одном файле приложения (`ArenaOffer`). Существующие файлы с чужими незакоммиченными правками (`BattleApplication`, `GameState`, `BattleMissionDefinition`, `ArenaContentSetup`, `GameSession`, `InspectPanel`, `BattleRewardOverlay`) меняются только добавлением рядом с ними. Перед записью — сообщение сессии-владельцу.

## Порядок реализации и зависимости

1. **Контент и состояние:**
   - поля `EconomyConfig` и `BattleMissionDefinition`;
   - `ArenaBiome`/`BattleFormation`;
   - поля `GameState`.

   Всё со значениями по умолчанию, поэтому старые ассеты читаются без изменений.
2. **Домен:** `ArenaFund`, `BattleReport.DefeatedShare`, `BattleOdds`. Тесты.
3. **Приложение:**
   - `BattleApplication.Start`: ставка, фонд, ничья, поражение, вершина, снаряжение врагов и чемпион;
   - `ArenaOffer`;
   - API сессии.

   Тесты (US3, US1).
4. **Контент лестницы:** `ArenaContentSetup` и `ProgressionContentSetup`, прогон меню. Тесты контента (US2, US4, US5).
5. **UI:** окно арены, Бараки, итог боя, окно награды, жетоны врагов и чемпион. Тесты панелей, `HudSnapshots`.
6. **Боты:** `BattlePlanner`, метрики отчёта. Прогон кампании, подбор чисел, §13 в `docs/economy-balance.md`.
7. **Локализация:** extract, переводы, build, шрифты.
8. **Сдача:** полный набор EditMode-тестов, Windows-сборка, Play Mode smoke.

## Complexity Tracking

Нарушений Constitution Check нет.
