---

description: "Tasks: прогрессия боёв на арене"
---

# Tasks: Прогрессия боёв на арене

**Input**: Design documents from `specs/001-arena-progression/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Входят в задачи. Конституция (VII) требует EditMode-тестов на узкой границе, у каждой истории спеки есть Independent Test.

**Organization**: По историям спеки. US1–US3 — P1, US4 — P2, US5 — P3.

## Format: `[ID] [P?] [Story] Description`

- **[P]** — можно делать параллельно: другой файл, нет зависимостей от незакрытых задач.
- Пути — от `unity/TrollStategy/Assets/Game/`, если не указан корень репозитория.

---

## Phase 1: Setup

- [X] T001 Получить от сессии-владельца (trollstrategy-97 или автор правок) ответ о незакоммиченных правках арены: `Runtime/Application/BattleApplication.cs`, `Runtime/Domain/GameState.cs`, `Runtime/Content/BattleMissionDefinition.cs`, `Editor/Setup/ArenaContentSetup.cs`, `Tests/EditMode/ArenaTrophyTests.cs`. Решение записать в notes этого файла. Писать только добавлением, их строки не переписывать.
- [X] T002 Синхронизировать копию проверки `C:\tmp\TrollVerify\unity\TrollStategy` (Assets, Packages, ProjectSettings) с рабочим деревом скриптом `scratchpad/sync_verify.py`. Прогнать на ней весь EditMode-набор и записать исходные падения в notes, чтобы дальше отличать свои падения от чужих.

---

## Phase 2: Foundational (блокирует все истории)

- [X] T003 [P] Создать `Runtime/Content/ArenaBiome.cs`: `enum ArenaBiome { Meadow, Forest, Swamp, Graveyard, MountainPass, Snow }` и `enum BattleFormation { Wall, ArchersBack, Flanks, Crowd }`, с XML-комментариями.
- [X] T004 [P] Добавить в `Runtime/Content/EconomyConfig.cs` раздел «Арена». Поля и значения по умолчанию:
  - `_arenaFundPeriodSeconds` = 180, геттер `ArenaFundPeriodMs` ≥ 1000;
  - `_arenaFundCap` = 3, ≥ 1;
  - `_arenaStakePercent` = 30, 0…100;
  - `_arenaDefeatRestMultiplier` = 2, ≥ 1;
  - `_arenaOddsMargin` = 1.5, ≥ 1.01.

  Геттеры с ограничениями. Метод `SetArena(periodSeconds, cap, stakePercent, defeatRestMultiplier, oddsMargin)` для тестов. Атрибуты `[Header("Арена")]` и `[Tooltip]` по-русски, как у соседних разделов.
- [X] T005 Добавить в `Runtime/Content/BattleMissionDefinition.cs` (чужие строки `_winGoods` не трогать):
  - **поля:** `_formation` (`BattleFormation`), `_milestone` (bool), `_championHealthPercent` (int ≥ 0; 0 — без чемпиона), `_biome` (`ArenaBiome`);
  - **геттеры** и **сеттеры** `SetLadderTraits(BattleFormation formation, bool milestone, int championHealthPercent)` и `SetBiome(ArenaBiome biome)`;
  - **в `BattleEnemyStart`:** поля `public string[] Gear` и `public bool Champion`, а также `IReadOnlyList<string> GearIds` с null → пусто.
- [X] T006 Добавить в `Runtime/Domain/GameState.cs`:
  - `ArenaFundPayouts` и `ArenaFundSinceMs` (int, копируются в `Clone()`);
  - `PendingBattleReward.Draw` (bool).

  Комментарии в стиле файла.
- [X] T007 В `Runtime/Domain/BattleSimulation.cs`:
  - `BattleReport.DefeatedShare(bool enemies)` — Σ `min(урон по бойцу, его здоровье)` / Σ здоровья бойцов стороны, по `Fighters` и событиям `Attack`; 0, если на стороне нет здоровья;
  - в `BattleRunState` — необязательные параметры конструктора и свойства `BurnedStake` (int), `ClosedMissionId` (string, null), `RestMs` (int).
- [X] T008 В `Runtime/Application/BattleApplication.cs` сделать `FighterInput` `internal static`. Для врагов прибавлять урон и броню предметов из `BattleEnemyStart.Gear` (по `catalog.GetEquipment`). Чемпион получает здоровье `EnemyHealthPercent × ChampionHealthPercent / 100`. В `Start` неизвестный id предмета врага → `Fail("Данные миссии некорректны")`.
- [X] T009 [P] Тесты в новом `Tests/EditMode/ArenaRulesTests.cs`:
  - `DefeatedShare` на собранном вручную `BattleReport`: перебор урона не превышает здоровья, пустая сторона;
  - враг с мечом и бронёй получает их урон и броню;
  - чемпион с 200% вдвое здоровее.

**Checkpoint**: компиляция (офлайн `scratchpad/oc/compile.py`) без ошибок, `ArenaRulesTests` зелёные.

---

## Phase 3: User Story 1 — видно, что ждёт на уровне (P1) 🎯 MVP

**Goal**: До боя окно арены показывает силу врагов, оценку лучшего отряда и полную награду.

**Independent Test**: Открыть окно на уровнях 1, 10, 25. Сила врагов совпадает с данными уровня, оценка меняется после найма и снаряжения, награда показана картинками.

- [X] T010 [P] [US1] Создать `Runtime/Domain/BattleOdds.cs`:
  - `enum OddsGrade { Weaker, Even, Stronger }`;
  - `BattleOdds.Compare(IReadOnlyList<BattleFighterInput> players, IReadOnlyList<BattleFighterInput> enemies, double margin) → (double Ratio, OddsGrade Grade)`. Формула research R7: H — сумма здоровья, D — сумма `max(1, урон − средняя броня противника) × 1000 / интервал`, r = (H_p·D_p)/(H_e·D_e); `r ≥ margin` → Stronger, `r ≤ 1/margin` → Weaker. Пустой отряд → (0, Weaker).
- [X] T011 [P] [US1] Создать `Tests/EditMode/BattleOddsTests.cs`:
  - ступени на синтетических бойцах;
  - монотонность: больше бойцов → не хуже;
  - калибровка по `Mission_01…30` из каталога: пять отрядов стенда из `docs/economy-balance.md` §12, расставленных как у ботов. «Stronger» выигрывает в `BattleSimulation` ≥ 80% случаев, «Weaker» не выигрывает ≥ 80%. Порог подбирается числом `EconomyConfig.ArenaOddsMargin`, а не кодом.
- [X] T012 [US1] Создать `Runtime/Application/ArenaOffer.cs`:
  - `ArenaOfferSnapshot` с полями US1 из [contracts/session-arena.md](contracts/session-arena.md): Mission, Level, Milestone, Formation, Biome, EnemyHealthPercent, EnemyDamageBonus, EnemyArmorBonus (плоская прибавка + снаряжение ведущего вида), EnemyGear, Champion, Odds, OddsRatio, SquadCount, FirstWin, GoldMin, GoldMax, Trophies, UnlockUnit, LadderComplete;
  - `ArenaOffers.Create(GameState, mission, catalog)`. Лучший отряд: существа по `CombatHealth × CombatDamage` по убыванию, затем по id, не больше `SquadLimit`. Снаряжение — жадно по слотам: предметы по `DamageBonus + ArmorBonus` по убыванию, затем по id, сильнейшему бойцу без этого слота. Бойцы — `BattleApplication.FighterInput`.
- [X] T013 [US1] В `Runtime/Application/GameSession.cs` добавить `ArenaOffer(BattleMissionDefinition mission)` с кэшем по `(_revision, MissionId)`. Только добавление, чужие правки файла не трогать.
- [X] T014 [P] [US1] Создать `Tests/EditMode/ArenaOfferTests.cs`:
  - уровень 10: числа силы = данные + снаряжение;
  - найм троллей и выдача железа поднимает оценку;
  - `UnlockUnit` до первой победы и null после;
  - трофеи уровня в `Trophies`;
  - одно состояние → одно предложение.
- [X] T015 [US1] Вёрстка окна в `UI/Uxml/Colony/Arena.uxml` и `UI/Styles/ColonyHud.uss` (стили окна арены лежат там):
  - под «Противник» — ряд `arena-strength`: три `arena-stat` из глифа и числа (`glyph--health` «×1,9», `glyph--damage` «+2», `glyph--armor` «+2»);
  - ряд `arena-enemy-gear` из жетонов предметов;
  - между сторонами — плашка `arena-odds`;
  - в фактах — `arena-trophies`.

  Если структура документа задаётся `UiSetup`, поправить там и перестроить префаб меню `TrollStrategy/Dev/Setup UI`.
- [X] T016 [P] [US1] В `UI/Styles/Theme.uss` — классы цвета плашки оценки: `.odds--stronger` (`--good`), `.odds--even` (`--warn`), `.odds--weaker` (`--bad`), если подходящих классов текста ещё нет. Цвета только здесь.
- [X] T017 [US1] В `Runtime/UI/Colony/ArenaPanel.cs` рисовать предложение `Session.ArenaOffer`:
  - сила врагов, подсказка «Враги сильнее обычного: здоровье ×{0}, урон +{1}, броня +{2}»;
  - снаряжение врагов, подсказка — название предмета;
  - оценка «Отряд сильнее» / «На равных» / «Отряд слабее», подсказки из [contracts/arena-ui.md](contracts/arena-ui.md);
  - трофеи — картинки с «×N», подсказка «Трофеи придут в бараки»;
  - «Лестница пройдена. Повторы платят из фонда.»

  Все тексты через `Ui.SetText`/`Ui.Text`.
- [X] T018 [US1] Тесты окна в `Tests/EditMode/ArenaPanelTests.cs` (новый, на `TestUi`). На уровне 10 видны три числа силы, плашка оценки с правильной ступенью, трофеи. Публичные свойства для тестов добавить в `ArenaPanel` по образцу `Gold`, `Hire`.

**Checkpoint**: US1 работает на текущей лестнице, даже без расстановок и снаряжения врагов.

---

## Phase 4: User Story 2 — каждый уровень новое испытание (P1)

**Goal**: Расстановки, снаряжение врагов с 10 уровня, чемпион на вехах.

**Independent Test**: Уровни 1–12 подряд — соседние расстановки разные. На 10 и выше враги носят снаряжение, его видно. Вехи 5 и 10 отмечены, на них чемпион.

- [X] T019 [US2] В `Editor/Setup/ArenaContentSetup.cs` — расстановки по research R9. Функция `EnemyCells(BattleFormation, roster)` выдаёт порядок клеток зоны 2×5 (передний столбец x = 7, задний x = 8):
  - **Wall** — передний сверху вниз, затем задний;
  - **ArchersBack** — `AttackRange > 1` в задний от середины наружу, остальные в передний от середины наружу, перелив в другой столбец;
  - **Flanks** — ряды 0, 4, 1, 3, 2, спереди назад;
  - **Crowd** — ряды 2, 1, 3, 0, 4, спереди назад.

  Уровень L получает `formations[(L−1) % 4]`.
- [X] T020 [US2] В `Editor/Setup/ArenaContentSetup.cs` — снаряжение врагов с уровня 10 по research R8. Пусть `b = (L−1)/4`.
  - Оружие: лучший по урону предмет ≤ b из ряда — стрелкам лук или зачарованный лук, остальным меч, молот или топор.
  - Броня и шлем: сумма брони ≤ b.
  - `SetArena(level, health, b − урон оружия, b − броня комплекта, unlock)`, так что итог урона и брони равен прежнему.
  - Предметы ищутся по `ItemId` в каталоге.
- [X] T021 [US2] В `Editor/Setup/ArenaContentSetup.cs` — вехи 5, 10, 15, 20, 25, 30 по research R10: `SetLadderTraits(formation, milestone: true, championHealthPercent: 200)`. Чемпион — первый враг ведущего вида, он занимает место одного «лишнего» врага. Снаряжение: на 5-й вехе железный меч и железный щит, с 10-й — лучшее снаряжение уровня на ступень выше (`b + 1`), если такое есть.
- [X] T022 [US2] Прогнать `TrollStrategy/Dev/Setup Arena Ladder` на копии (batch `-executeMethod TrollStrategy.Editor.Setup.ArenaContentSetup.Apply`). Проверить `Mission_*.asset` диффом. Перенести ассеты в рабочее дерево.
- [X] T023 [P] [US2] Создать `Tests/EditMode/ArenaLadderContentTests.cs` по каталогу:
  - раскладки «клетка → вид» соседних уровней различаются;
  - формации соседей различаются;
  - ровно 6 вех, на каждой ровно один чемпион, вне вех чемпионов нет;
  - с 10 уровня у всех врагов есть снаряжение из каталога;
  - итог урона и брони (прибавка + снаряжение ведущего вида) = `(L−1)/4`.
- [X] T024 [US2] В `Runtime/Presentation/Battle/BattleBoardView.cs` и `Runtime/Presentation/Battle/BattleSceneController.cs` — жетоны снаряжения у врагов. `ShowGear` получает снаряжение врагов из `mission.Enemies[i].Gear` (`WornItem` с иконкой и `Enchanted`) и показывает его без пустых гнёзд, на всё время боя.
- [X] T025 [US2] В `Runtime/Presentation/Battle/BattleFighterView.cs` — `SetChampion()`: масштаб 1,25 и класс `.hp-bar--champion` на полосе здоровья. Правило `.hp-bar--champion` (рамка цвета `--coin`) — в `UI/Styles/WorldUi.uss`; если переменной там нет, классом из `Theme.uss`. `BattleBoardView` зовёт его для врагов с `Champion`.
- [X] T026 [US2] Звезда вехи:
  - `UI/Icons/star.svg` — одноцветная, по образцу соседних;
  - `.glyph--star` в `UI/Styles/Theme.uss`;
  - в `Runtime/UI/Colony/ArenaPanel.cs` — значок на диске вехи и надпись «Веха. Уровень {0}».
- [X] T027 [P] [US2] Тесты:
  - `Tests/EditMode/BattleFighterViewTests.cs` — у врага с предметами есть жетоны без пустых гнёзд, у чемпиона класс `hp-bar--champion`;
  - `Tests/EditMode/ArenaPanelTests.cs` — на вехе звезда и надпись.

**Checkpoint**: US1 и US2 вместе. Оценка учитывает снаряжение врагов и чемпиона.

---

## Phase 5: User Story 3 — победа стоит риска (P1)

**Goal**: Ставка, общий фонд повторов, цена поражения, закрытие вершины, награда за ничью.

**Independent Test**: Тесты исходов (таблица в [data-model.md](data-model.md)). Прогон бота-воителя: доля золота с арены ≤ 35%.

- [X] T028 [P] [US3] Создать `Runtime/Domain/ArenaFund.cs`:
  - `Available(GameState, EconomyConfig)`;
  - `NextInMs(...)` (−1 при полном фонде);
  - `Take(...)` — по формулам из [data-model.md](data-model.md). Инвариант `0 ≤ P ≤ Cap`, `S ≤ now`.
- [X] T029 [P] [US3] Создать `Tests/EditMode/ArenaFundTests.cs`:
  - 0 → 1 через 180 с, 3 через 540 с, дальше не растёт;
  - `Take` при полном фонде перезапускает отсчёт;
  - при неполном сохраняет набранную часть;
  - `NextInMs` после `Take`.
- [X] T030 [US3] В `Runtime/Application/BattleApplication.cs`:
  - `Stake(GameState, mission, catalog)` = `max(10, round10(StakePercent × (FirstWinGold пока не пройден, иначе RepeatWinGold) / 100))`;
  - в `ValidateAvailability` после проверки отдыха (и не в отладке): `Gold < Stake` → `Fail($"Не хватает золота на ставку: ещё {Stake − Gold}")`.
- [X] T031 [US3] В `Runtime/Application/BattleApplication.cs`, `Start` — исходы по таблице [data-model.md](data-model.md#исход-боя-battleapplicationstart):
  - **Победа:** первая — бросок первой награды; повтор при `ArenaFund.Available > 0` — `Take` и бросок повтора; иначе 0 без броска. Трофеи (чужой блок `WinGoods`) — только при первой или оплаченной.
  - **Ничья:** бросок той же награды × `report.DefeatedShare(enemies: true)`, на пройденном уровне только при выплате фонда, которую она забирает. `PendingBattleReward.Draw = true`, ставка списывается.
  - **Поражение:** ставка списывается, отдых × `DefeatRestMultiplier`. Если уровень — высший открытый в `Progress.UnlockedMissions` и `Level > 1`, удалить его оттуда.
  - **Всегда:** `BattleRunState(…, BurnedStake, ClosedMissionId, RestMs)`.
- [X] T032 [P] [US3] Создать `Tests/EditMode/ArenaStakeTests.cs` через `GameSession.Dispatch`:
  - ставка списывается при поражении и ничьей, не списывается при победе;
  - отказ при нехватке золота с числом;
  - поражение на вершине: уровень закрыт, `HighestMissionLevel` прежний, двойной отдых;
  - поражение на 1 уровне ничего не закрывает;
  - поражение ниже вершины ничего не закрывает;
  - победа ниже снова открывает закрытый;
  - фонд 2 → два оплаченных повтора на разных уровнях → третий без золота и трофеев;
  - первая победа фонд не трогает;
  - две незабранные победы не берут больше, чем было в фонде;
  - ничья: доля, ставка, без побед и трофеев.
- [X] T033 [US3] Перевести существующие тесты, которые ждут золото за каждую повторную победу, на фонд (дать время на выплату, `EconomyConfig.SetArena`): `Tests/EditMode/BattleSessionTests.cs`, `Tests/EditMode/CampaignBotTests.cs`, `Tests/EditMode/ArenaTrophyTests.cs` (чужой файл — минимальная правка с пометкой в notes).
- [X] T034 [US3] Дополнить `Runtime/Application/ArenaOffer.cs` полями US3: Stake, GoldShort, FundPayouts, FundCap, FundNextInMs, PaysFromFund, DefeatRestMs, ClosesOnDefeat, ReopenLevel. При пустом фонде на повторе `GoldMin = GoldMax = 0`, трофеев нет. В `Runtime/Application/GameSession.cs` добавить `ArenaFundSnapshot ArenaFund` (Payouts, Cap, NextInMs, PeriodMs).
- [X] T035 [US3] Окно арены (`Runtime/UI/Colony/ArenaPanel.cs`, `UI/Uxml/Colony/Arena.uxml`, `UI/Styles/ColonyHud.uss`):
  - строка фонда `arena-fund`: точки по пределу, полные = выплаты, «через {0}» или «фонд полон», подсказка из контракта;
  - в подвале — «Ставка {0}» с монетой;
  - строка риска «Поражение: ставка сгорит, отдых {0}», а при `ClosesOnDefeat` — «Поражение: ставка сгорит, отдых {0}, уровень закроется до победы на {1}-м»;
  - при нехватке — текст отказа;
  - заметка к золоту при пустом фонде: «фонд пуст: только ставка назад».

  Совет про Бараки переезжает в подсказку плашки «Отряд слабее».
- [X] T036 [US3] Экран итога:
  - `Runtime/Presentation/Battle/IBattleScreen.cs` → `ShowResult(outcome, survived, fallen, lostItems, BattleCost cost)` с `struct BattleCost { BurnedStake, ClosedLevel, ReopenLevel, RestMs }` в том же файле;
  - `Runtime/Presentation/Battle/BattleSceneController.cs` собирает его из `ActiveBattle`;
  - `Runtime/UI/Battle/BattleHud.cs` и `Runtime/UI/Battle/BattleHudView.cs` и панель реплея показывают «Ставка сгорела: {0}», «Уровень {0} закрыт до победы на {1}-м», «Отдых {0}»;
  - тестовые реализации `IBattleScreen` поправить.
- [X] T037 [US3] `BattleRewardSnapshot.Draw` в `Runtime/Application/GameSnapshot.cs` и `GameSession.CreateBattleRewardSnapshot`. Подпись «за ничью» в `Runtime/UI/Colony/BattleRewardOverlay.cs`, только добавлением.
- [X] T038 [P] [US3] Тесты окна и итога:
  - `Tests/EditMode/ArenaPanelTests.cs` — ставка и фонд видны, при нехватке кнопка недоступна и текст с числом, на вершине выше 1 есть «уровень закроется»;
  - `Tests/EditMode/BattleHudTests.cs` — строки цены при поражении, при победе их нет.
- [X] T039 [US3] Переписать выбор боя в `Bots/BattlePlanner.cs` по `Session.ArenaOffer`:
  - **Подъём** (задание `ReachArenaLevel` или «ещё не пройден») — на высший открытый уровень с `Odds ≥ Even`. Если такого нет — сигнал `NeedsStrength` (QuestPlanner уже покупает улучшение Бараков).
  - **Фарм** (`FightsForGold`) — только если `PaysFromFund`, на высший пройденный уровень с `Odds == Stronger`.
  - Ставка: при `GoldShort > 0` — `wait.NeedGold`.
  - `_ceiling` и `_winsInRow` убрать. Найм троллей по `_squadTrolls` оставить.
- [X] T040 [US3] Метрики ботов:
  - `Bots/BotRun.cs` — `BattleRecord` + `Stake`, `PaidFromFund`, `Draw`, `ClosedLevel`; `BotRun.TotalGoldEarned` — выручка рынка + золото заданий + золото арены, из снимков сессии;
  - `Bots/BotReport.cs` и `Bots/BotReportData.cs` — доля золота с арены и максимум оплаченных повторов за любые 10 мин.

**Checkpoint**: US1–US3. Правила арены полные, боты играют по ним.

---

## Phase 6: User Story 4 — прогресс арены виден в колонии (P2)

**Goal**: Трофеи по всей лестнице, задания-вехи после цепочки, Бараки показывают уровень и веху.

**Independent Test**: Уровни 1–10. Трофеи каждого уровня в Бараках. Карточка Бараков — высший уровень и следующая веха. Задание-веха 10 выполняется в повторяемом круге.

- [X] T041 [US4] В `Editor/Setup/ArenaContentSetup.cs` — трофеи по research R11 через существующий `SetWinGoods`:
  - уровень 1 — как сейчас;
  - 2–9 — по одному из ряда «ржавый меч, латаная броня, деревянный щит, копьё, кожаная броня, шлем» по кругу, на вехе 5 — два;
  - с 10 — один предмет из снаряжения врагов уровня (оружие на чётных, броня на нечётных), на вехах — оружие и броня чемпиона.

  Ресурсы — по `EquipmentId` в каталоге. Перезапустить меню (как T022).
- [X] T042 [P] [US4] Дополнить `Tests/EditMode/ArenaLadderContentTests.cs`: у уровней 2–30 трофеи заданы; с 10 каждый трофей — предмет из `Gear` врагов уровня; у каждого трофея есть `EquipmentId`.
- [X] T043 [US4] В `Runtime/Content/ProgressionDefinition.cs` добавить `_repeatArenaLevelStep` = 5 и `_repeatArenaLevelCap` = 30, геттеры и сеттер для настройки. В `Runtime/Domain/Progression.cs`, `Repeat()`, цель `ReachArenaLevel` = `min(cap, amount + cycle × step)` вместо процента.
- [X] T044 [US4] В `Editor/Setup/ProgressionContentSetup.cs` заменить повторяемое «repeat-battle» на `Quest("repeat-arena", "Веха арены", "Победи на следующем уровне-вехе арены. Каждый круг веха выше.", Goals(QuestGoal.ReachArenaLevel(10)), Rewards(QuestReward.Coins(800)))`. Задать шаг и потолок, прогнать `TrollStrategy/Dev/Setup Progression` (как T022).
- [X] T045 [P] [US4] `Tests/EditMode/ProgressionTests.cs`: повторяемые цели «Веха арены» по кругам — 10, 15, 20, 25, 30, 30; задание выполняется в коммите победы на уровне цели. `Tests/EditMode/ProgressionContentTests.cs`: «repeat-arena» есть, «arena-six» остался в цепочке.
- [X] T046 [US4] Карточка Бараков:
  - в `Runtime/Application/GameSession.cs` — `NextMilestone()`;
  - в `Runtime/UI/Colony/InspectPanel.cs`, в ветке Бараков, строки «Арена» → «уровень {0}» / «нет побед» и «Следующая веха» → «уровень {0}» / «все пройдены», только добавлением.
- [X] T047 [P] [US4] Тест карточки в `Tests/EditMode/NewPanelsTests.cs` (или `ColonyHudTests.cs`, где уже тестируется `InspectPanel`): строки Бараков до и после победы на 5 уровне.

**Checkpoint**: US1–US4.

---

## Phase 7: User Story 5 — окружения (P3)

**Goal**: У каждого уровня своё окружение, без готового арта — «Луг».

**Independent Test**: Тест контента — у 30 уровней есть окружение, префаб из `Prefabs/Arenas`. Окно называет окружение.

- [X] T048 [US5] В `Editor/Setup/ArenaContentSetup.cs` добавить в таблицу `Ladder` поле `ArenaBiome` по распределению research R14. `SetBiome` и `SetEnvironment(Arena_<Biome>.prefab)` из `Assets/Game/Prefabs/Arenas`; без префаба — `Arena_Meadow.prefab` и `Debug.LogWarning`. Строку `var environment = first.EnvironmentPrefab;` убрать.
- [X] T049 [US5] В `Editor/Setup/ArenaPrefabBuilder.cs` (около строки 367) не назначать окружение миссиям: окружение уровню назначает только `ArenaContentSetup`. Комментарий об этом. `BuilderVersion` не трогать: сборка префабов не меняется.
- [X] T050 [US5] В `Runtime/UI/Colony/ArenaPanel.cs` и `UI/Uxml/Colony/Arena.uxml` — подпись окружения `arena-biome` справа от названия: «Луг», «Лес», «Болото», «Кладбище», «Горная застава», «Снега».
- [X] T051 [P] [US5] Дополнить `Tests/EditMode/ArenaLadderContentTests.cs`: у каждого уровня есть `EnvironmentPrefab` из `Assets/Game/Prefabs/Arenas`. Без своего префаба окружения — это `Arena_Meadow`. Распределение совпадает с R14.

**Checkpoint**: все истории.

---

## Phase 8: Polish & Cross-Cutting

- [X] T052 Локализация:
  1. `python tools/localization/extract.py`;
  2. перевести новые ключи во все 14 файлов `tools/localization/translations/*.json`, проверить `validate.py <code>` по каждому;
  3. `python tools/localization/build.py`;
  4. меню `TrollStrategy/Dev/Setup Localization Fonts`.

  Шаблонные ключи — целыми фразами с дырками.
- [X] T053 Баланс ботами (`tools/bots/run-bots.ps1 -NoOpen` на копии). Цели:
  - SC-003 — арена ≤ 35% золота воителя;
  - SC-004 — живой игрок 60 ± 10 мин, «Слава арены» ≤ 10 мин;
  - SC-006 — все 5 профилей проходят;
  - SC-008 — оплаченные повторы ≤ выплатам + предел.

  Рычаги — только данные: период и предел фонда, доля ставки, цель «arena-six», прибавки уровней 2–6, `OddsMargin`. Итог и таблица — `docs/economy-balance.md` §13.
- [X] T054 Продлённый прогон воителя за конец цепочки (`stopAfterLevel` = длина цепочки + 4, через `BotMenu`): задание-веха 10 выполнено (SC-006, FR-015). Записать в §13.
- [X] T055 [P] Снимки окна арены (`HudSnapshots`, `-hudLanguages ru,en,ja`) на уровнях 1, 10 и вехе 10, при 1920×1080 и 1280×720. Проверить глазами: ничего не обрезано, правила «воздуха» соблюдены.
- [X] T056 Весь EditMode-набор на копии зелёный, кроме исходных чужих падений из T002. Затем на рабочем проекте.
- [X] T057 `TrollStrategy/Build Windows Player`: `Builds/Windows/build-result.txt` — успех.
- [X] T058 Play Mode smoke (`PlaySmokeCheck` на копии) с быстрым боем на уровне 10 и на вехе 10: жетоны врагов, чемпион, ошибок 0.
- [X] T059 [P] Документация:
  - `docs/GDD.md` — раздел арены: ставка, фонд, поражение, ничья, вехи, окружения;
  - `docs/campaign-bots.md` — новые метрики арены;
  - `docs/backlog/playtest-2026-10-01.md`, пункт 6 — ссылка на спеку и статус.

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → **Foundational (T003–T009)** → истории.
- **US1** (T010–T018) — MVP, зависит только от Foundational.
- **US2** (T019–T027):
  - контент (T019–T023) не зависит от US1;
  - звезда в окне (T026) — после T017.
- **US3** (T028–T040):
  - правила (T028–T033) не зависят от US1/US2;
  - окно (T035) — после T017;
  - боты (T039) — после T034.
- **US4** (T041–T047): трофеи (T041) — после T020 (снаряжение врагов); вехи-задания и Бараки — независимы.
- **US5** (T048–T051): независима; T050 — после T017.
- **Polish:** локализация (T052) — после всех текстов; баланс (T053–T054) — после US3 и US4; сдача (T056–T058) — в конце.

## Parallel Example

```text
После Foundational, параллельно:
  T010 BattleOdds.cs + T011 BattleOddsTests.cs      (US1, домен)
  T028 ArenaFund.cs  + T029 ArenaFundTests.cs       (US3, домен)
  T019–T021 ArenaContentSetup (один файл — по порядку) (US2)
  T043 Progression.Repeat + T045 ProgressionTests    (US4)
```

## Implementation Strategy

1. **MVP:** Foundational + US1. Окно честно показывает силу, оценку и награду.
2. **US3** — правила риска и фонд, сразу с ботами. Без них рост наград из US2 усиливает фарм.
3. **US2** — расстановки, снаряжение врагов, чемпион.
4. **US4, US5.**
5. **Polish:**
   - локализация;
   - баланс ботами — числа меняются только в данных;
   - снимки, полный прогон, сборка, smoke.

Один файл (`ArenaContentSetup`, `BattleApplication`, `ArenaPanel`) правится задачами по порядку, без параллели внутри файла.

## Notes

- **T001.** trollstrategy-97 ответил, что правки арены (WinGoods, «сложная арена», ArenaTrophyTests) не его. Их автор — завершённая сессия. Дальше их строки только дополнялись. Исключение — трофеи 1-го уровня: они обобщены на всю лестницу в `ArenaContentSetup.Trophies`, набор 1-го уровня тот же.
- **T002.** До работы на копии было 462 теста, падал 1: `CampaignBotTests.ReportPage_KeepsTheRunHistoryNextToThePage`. Это страница статистики, её ведёт сессия trollstrategy-97, к арене тест не относится.
- **T015/T016.** Стили окна арены лежат в `ColonyHud.uss`, отдельного `Arena.uss` нет. Цвет плашки оценки — рамка через токены `--good-fill`, `--coin` и `--bad` из `Theme.uss`.
- **T033.** `BattleSessionTests` и `CampaignBotTests` прошли без правок. В `ArenaTrophyTests` вторая победа теперь оплачивается фондом: добавлена секунда колонии при периоде фонда 1 с.
- **R11 и Бараки.** `ResourceContentTests.ArenaTrophies_AreGearTheBarracksKeep` требует, чтобы Бараки хранили каждый трофей. Поэтому в `ProductionContentSetup` Бараки хранят все товары-снаряжение каталога, меню перезапущено.

- Чужие незакоммиченные правки — только добавлением рядом. Перед записью `.cs` в `Assets` — сообщение активным сессиям (память `unity-shared-editor-etiquette`).
- Коммит — только по просьбе владельца. Свои правки отделяются от чужих (`scratchpad/stage_mine.py` по образцу).
- **T053.** Прогон ботов 2026-10-04 (`docs/economy-balance.md` §13):
  - SC-003 выполнен — воитель 25%;
  - SC-006 выполнен — 5 из 5;
  - SC-008 выполнен — 5 при пределе 6;
  - SC-004 выполнен частично: «Слава арены» 5,3 мин, но кампания живого игрока 73,7 мин при верхней границе 70. Перебор не из-за арены (её задания около 11 мин), рычагами арены его не убрать.
- **T054.** `CampaignBot.StopAfterLevel` больше длины цепочки теперь играет и повторяемые задания. Воитель с пределом 37 выполнил задание 35 «Веха арены» (уровень 10).
- **T055.** Снимки ru, en, ja — окно на уровнях 1, 10 и 25; для них `GameSession.DebugOpenArenaLadder`. По снимкам строка силы считала чемпиона, теперь она идёт по обычным врагам.
- **T056.** Весь EditMode-набор на копии: 495 тестов, 490 прошли, 4 пропущены (`[Explicit]`), 1 упал — `CampaignBotTests.ReportPage_KeepsTheRunHistoryNextToThePage`. Он падал из-за копии: в ней не было `tools/stats/templates`. С шаблонами `CampaignBotTests` проходит, 13 из 14.
- **T056, явные тесты кампании.** `Profile_FinishesTheCampaign("human")` с раскладкой `TestColony` застревает на задании 29 «Пир на весь остров»: число пиров не растёт 30 минут. A/B со старым хранением Бараков даёт тот же результат на той же минуте. Боёв до этого задания три, все победы без потерь, так что арена путь экономики не меняет. Это отдельная проблема экономики. Через `BotMenu` со сценой живой игрок кампанию проходит.
- **T057.** `TrollStrategy/Build Windows Player` на копии: `Succeeded errors=0`.
- **T058.** `PlaySmokeCheck` на копии, вех 10:
  - окно арены открыто: оценка «слабее», ставка 240, снаряжение врагов 3, трофеи 2, кладбище;
  - быстрый бой: 6 врагов в снаряжении, 1 чемпион в рамке; после поражения сгорела ставка 240;
  - ошибок 0.
