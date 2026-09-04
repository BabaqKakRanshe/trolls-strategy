# Архитектурный аудит и анализ эволюции herald-corev2

**Дата снимка:** 2026-09-04  
**Ветка / HEAD:** `yandex-games` / `5532da1`  
**Охват истории:** полная локальная Git-история, 89 коммитов, 2026-07-30—2026-08-25  
**Состояние рабочего дерева:** до аудита уже были незакоммиченные изменения в платформенном слое, настройках, Vite-конфигурации и документации. Они не изменялись аудитом.

## Краткий вывод

Проект начинался не как «обычная Phaser-игра», а как измеримый двухэкранный прототип: детерминированная симуляция без движка, браузер как визуальный адаптер, Node-стенд как второй потребитель того же боя. Этот замысел подтверждается и первоначальным design doc, и первым коммитом `7397904`. Главная исходная граница — `src/core` не зависит от Phaser, а сцена не определяет числовой исход боя — оказалась удачной и выдержала почти десятикратный рост исходников.

Основной архитектурный долг возник не из «слишком чистой» архитектуры, избытка интерфейсов или DI. Он возник из недостающих границ владения:

- `SessionScene` осталась одновременно runtime-адаптером, HUD/VFX-композитором, контроллером обучения и транзакцией результатов;
- `ArsenalScene` стала application service для всей мета-игры, mutable store профиля, router и фасадом примерно для всех экранных действий;
- `core/build.js` вырос из модели сборки в агрегат всей схемы профиля и её миграций;
- локализация была добавлена поздно и для сохранения русских каталогов построила обратную зависимость `data ↔ i18n`, дав реальный цикл из семи модулей;
- переход Canvas → DOM → «final» оставил 8 356 строк отключённых runtime-реализаций и продолжает нагружать словари и аудит локализации.

Системе не нужен rewrite и не нужна многослойная Clean Architecture. Реалистичное направление — сохранить сильное детерминированное ядро, добавить один тонкий application-слой для команд над профилем и завершения вахты, сделать схему профиля единственным источником сериализации, разорвать `data ↔ i18n` через projection/presentation adapters и удалить подтверждённо отключённые поколения UI.

Ни одна найденная проблема не оценена как Critical: сборка проходит, числовое ядро хорошо изолировано, данные восстанавливаются, а архитектурные дефекты допускают поэтапное исправление. Четыре проблемы имеют High severity из-за сочетания высокой изменчивости, большого fan-out и риска тихой потери/расхождения данных.

---

# 1. Фактическая архитектура

## 1.1. Состав и направление зависимостей

На текущем снимке — 149 JavaScript-файлов и около 70 435 строк JS. Статический граф ES-import содержит 765 внутренних рёбер:

| Направление | Рёбер | Что это означает |
| --- | ---: | --- |
| `core → core` | 210 | доменная модель, симуляция, каталоги, локализация |
| `game → core` | 243 | UI напрямую читает большое число доменных таблиц и функций |
| `game → game` | 297 | Phaser/DOM/UI/VFX/audio композиция |
| `game → platform` | 3 | сохранение и platform-aware настройки |
| `platform → core` | 1 | платформа читает перечень языков |
| `core → game` | 0 | сильная и реально соблюдаемая граница |

Концептуально приложение устроено так:

```text
main.js
  ├─ platform.init() ──> Yandex SDK / no-op platform
  ├─ settings + global i18n language + diagnostics
  └─ Phaser.Game
       ├─ ArsenalScene
       │    ├─ navigation + modal layer
       │    ├─ DOM/Canvas presentation screens
       │    ├─ mutable profile orchestration
       │    └─ core: build / wallet / forge / quests / catalogs
       └─ SessionScene
            ├─ CombatSim + EventBus + MetricsCollector
            ├─ Phaser rendering / VFX / audio / loot hand
            ├─ onboarding beats / HUD / outro
            └─ result settlement into mutable profile

tools/*
  ├─ core/run.js ──> тот же CombatSim без браузера
  ├─ bench / parity / gates / noise / sweep / invariants
  └─ content IO + admin tools + doc generation

core
  ├─ sim/*                 числовые шаги боя
  ├─ combat.js             состояние и порядок шага
  ├─ data/*                каталоги + валидаторы + часть бизнес-логики
  ├─ build.js              сборка + вся схема профиля + миграции/repair
  ├─ wallet/forge/quests   мета-игровые операции
  └─ i18n/*                глобальный язык + словари + projection каталогов
```

## 1.2. Точки входа

- Runtime: `src/main.js`. До Phaser он ждёт платформу, выбирает язык, включает диагностику и музыкальный монитор.
- Игровая мета-петля: `ArsenalScene`.
- Боевая петля: `SessionScene`.
- Headless simulation: `src/core/run.js`, используемый `bench`, `parity`, `gates`, `noise`, `matrix`, `sweep`, `value` и тестами.
- Контентные входы: многочисленные `tools/*-io.mjs` и admin servers.

## 1.3. Слои по факту, а не по названиям папок

`src/core` является **engine-independent**, но уже не является чистым domain-слоем. В нём одновременно находятся:

- чистая симуляция и доменные вычисления;
- каталоги данных;
- repair/migration всего пользовательского профиля;
- UI-ready строки и view models (`stats`, `briefing`, `verdict`, `wallet`, `forge`);
- глобально выбранный язык.

Поэтому фактическая граница проходит не «domain / presentation», а «без браузерного движка / с браузерным движком». README корректно обещает отсутствие Phaser в `core`, но название `core` создаёт более сильное ожидание, чем реальные зависимости обеспечивают.

`src/game/screens` также не является одним presentation-поколением. В каталоге сосуществуют:

1. Canvas/Phaser-классы (`HubScreen`, `CardEditor`, `ForgeScreen` и др.);
2. промежуточные DOM-классы (`EditorDom`, `ForgeDom`, `SupplyDom` и др.);
3. текущие реализации «final» (`BuildDom`, `ForgeFinal`, `SupplyFinal`, `ModsFinal`, `wikiFinal`, `windowsFinal`).

Фактический runtime импортирует главным образом третье поколение; первые два образуют отключённый подграф.

## 1.4. Состояние и владение данными

- Профиль — один большой изменяемый объект, загружаемый `game/profile.js` и передаваемый между двумя Phaser-сценами.
- `ArsenalScene` считает себя единственным мутатором профиля для экранов, но `SessionScene` тоже изменяет профиль при расчёте результата.
- Сборка перед боем замораживается через `freezeBuild`; это хорошая локальная граница.
- Настройки — module singleton в `game/settings.js`, с подписчиками.
- Язык и словари — module singleton в `core/i18n/index.js` (`DICTS`, `lang`, `misses`).
- Audio — singleton `audio = new AudioDirector()`.
- Platform и diagnostics — module singletons.
- DI-container/service locator отсутствует. Экраны получают вручную собранный `ctx` из `ArsenalScene`, фактически большой интерфейс application facade.

## 1.5. Persistence, networking, error handling, logging, caching

- Основная persistence: синхронный `localStorage`, один ключ `herald.arsenal.v1`.
- Cloud persistence: Yandex adapter асинхронно seed-ит пустой localStorage до старта, затем принимает сериализованный снимок с debounce. LocalStorage остаётся источником правды.
- Settings и diagnostic crash box хранятся отдельными ключами.
- Ошибки хранилища и платформы деградируют в warning + безопасное продолжение; глобальные `error`/`unhandledrejection` ловит diagnostic layer.
- Логирование — структурно не унифицировано: `console.*` и собственная диагностическая отправка.
- Явного data cache нет; есть локальные presentation caches и pools (shape cache, VFX maps/pools, texture generation).
- Обычного HTTP API нет. Внешние границы — Yandex SDK и diagnostic sender.

## 1.6. Навигация

Phaser знает две сцены: арсенал и вахту. Внутри арсенала `ArsenalScene.go()` переключает собственные screen-объекты и модальные окна. Это простой и подходящий масштабу router, но его owner одновременно владеет всеми application-командами.

---

# 2. Первоначальный архитектурный замысел

## Подтверждённые факты

**Факт.** Design doc в первом коммите определял ровно две сцены: `ArsenalScene` изменяет сборку, `SessionScene` получает неизменяемый снимок.

**Факт.** В первый коммит уже входили `core/combat.js`, `core/run.js`, `core/events.js`, `core/metrics.js`, `tools/bench.mjs`, `tools/parity.mjs`, `tools/smoke.mjs` и `tools/test-core.mjs`. Headless-измерение не было поздней пристройкой.

**Факт.** События боя изначально связывали расчёт с VFX и метриками. `VfxDirector` централизовал дорогие эффекты, а не создавался как generic bus ради гипотетических подписчиков.

**Факт.** Профиль изначально имел schema version, repair повреждённых данных и localStorage adapter.

**Факт.** Изначальный этап намеренно исключал экономику, смерть, добычу, прогрессию, особых врагов и ручные решения в бою. Большинство этих требований появилось уже в первые три недели.

## Восстановленный замысел

**Сильная гипотеза.** Проект проектировался как «functional core / imperative shell» без формального следования этому названию: детерминированное ядро считает, Phaser визуализирует и принимает ввод, инструменты повторно используют core.

Признаки: фиксированный шаг, seed, snapshot сборки, event bus, parity browser↔Node, отсутствие Phaser imports в core.

**Сильная гипотеза.** Сцены предполагались достаточно небольшими orchestration roots, потому отдельный application/service слой не вводился. Для двух экранов и одного типа сессии это было разумно.

**Сильная гипотеза.** Каталоги должны были быть «единственной правдой» и для чисел, и для русской прозы, чтобы admin/IO-инструменты могли редактировать их без второй схемы. Это позже определило необычную архитектуру i18n.

**Гипотеза.** Имена `core`, `data`, `screens` отражают раннюю двухчастную систему и не пересматривались после превращения прототипа в мета-игру с картой, кузницей, квестами и платформой.

## Что было заложено «на будущее»

- В коммите `100f744` появились раздельные RNG streams до того, как все потребители существовали. Позже отдельные `loot` и `xp` streams реально использовались и не сдвинули соседние эталоны.
- Schema 2 сразу получила поля нескольких параллельных веток. Эти ветки появились почти сразу; это не «архитектура для никогда не пришедшего будущего».
- Раздельные бюджеты оружия/костюма и EventBus оказались востребованы.

Иными словами, существенных доказательств классического premature overengineering в исходном фундаменте мало.

---

# 3. Эволюция проекта

## Этап 0 — измеримый двухэкранный прототип (`7397904`, 2026-07-30)

**Что изменилось:** первая версия уже содержала 23 JS-файла / 6 011 строк, детерминированный бой, две сцены, VFX/audio, profile adapter и инструменты проверки.

**Почему понадобилось:** исходная цель была не просто показать бой, а доказать, что карточки дают измеримую и видимую разницу.

**Что решило:** общий `CombatSim` сделал числовой баланс воспроизводимым и сравнимым между Node и браузером.

**Какую сложность создало:** две сцены получили слишком широкое первоначальное право владеть соответственно всей мета-игрой и всей вахтой. Пока функций было мало, цена была почти нулевой.

## Этап 1 — контрактный проход и быстрый рост боевой модели (`100f744`—`655fa3e`, 2026-07-30—08-01)

**Что изменилось:** streams RNG, schema 2, живучесть костюма, шесть стволов, контракты, площадки, presets, endless, опыт, восьмая модель.

**Что решило:** бой получил реальные оси выбора и совместимость параллельных веток.

**Новая сложность:** `CombatSim` вырос с 873 до 1 663 строк, `build.js` с 220 до 668, `SessionScene` с 863 до 1 311.

**Оценка момента:** рост был виден уже 2026-08-01. Для `CombatSim` команда отреагировала, для сцен и профиля — нет.

## Этап 2 — карта, фракции и декомпозиция симуляции (`c29227c`—`c5d4d15`, 2026-08-03—08-04)

**Что изменилось:** появилась звёздная карта; фракция стала отдельным хозяином roster/defense; механики боя вынесены в `core/sim/*`; EventBus получил strict test mode; появился trace.

**Что решило:** `combat.js` сократился с 1 663 до 453 строк. Функции `f(sim, …)` сохранили один state owner и вынесли механику по причинам изменения.

**Новая сложность:** `StarmapScreen` сразу появился как файл на 1 749 строк, потому совместил астрономическую раскладку, canvas rendering, DOM card, навигацию и правила доступности.

**Оценка момента:** это лучший пример успешного архитектурного refactoring в истории проекта. Он показывает безопасный шаблон для текущих hotspots: сначала контракт и parity, затем выделение функций/координатора.

## Этап 3 — мета-игра и инструменты контента (`1165ebe`—`c5fa579`, 2026-08-07—08-09)

**Что изменилось:** ростер/миссии, дроп, музыка файлами, арт, admin/IO, инварианты, модель игрока в стенде; профиль вобрал inventory, blueprints, mods, forge и progress.

**Что решило:** прототип превратился в связную игровую петлю с производством и прогрессом.

**Новая сложность:** `build.js` достиг 2 155 строк; каждая новая capability требовала default + repair + save whitelist + scene commands + UI. Доменный aggregate начал меняться по несвязанным причинам.

## Этап 4 — поздняя локализация (`ffa88a7`—`8bde23a`, 2026-08-12—08-13)

**Что изменилось:** появился `core/i18n`, английский, переключение языка, tutorial/briefing; затем английский стал runtime-default для itch.io; справочник и каталоги дорабатывались после обнаружения русских утечек.

**Что решило:** игра стала двуязычной без переноса русских catalog fields и без поломки admin writers.

**Новая сложность:** localization projection импортирует почти все каталоги, а часть каталогов и core services импортирует перевод обратно. Появился большой сильно связанный компонент и глобальное состояние языка.

## Этап 5 — отмена преждевременно сложной механики (`e0ccedd`, 2026-08-17)

**Что изменилось:** полярности, «Печать» и особый слот были удалены: −977 / +188 строк в 13 core/data-файлах.

**Что решило:** снята механика, цена которой превышала подтверждённую ценность.

**Новая сложность:** исторические названия/комментарии/документы и UI-поколения продолжили хранить контекст снятой модели.

**Оценка:** это хороший пример своевременного отказа от overengineering, а не его накопления.

## Этап 6 — UI v4 и незавершённая миграция (`aa6e23f`, 2026-08-17)

**Что изменилось:** за один коммит добавлены DOM-layer, новые окна и `*Final` screens; 15 584 вставки / 2 983 удаления в 80 файлах.

**Что решило:** интерфейс получил единый визуальный язык, HTML text/layout и общие модальные окна.

**Новая сложность:** прежние Canvas и промежуточные DOM реализации оставлены «до конца переноса». Runtime больше их не импортирует, но i18n tooling и комментарии продолжают их учитывать.

## Этап 7 — quests/objectives и снятие фиксированных 60 секунд (`7835ef0`—`3d92ea9`, 2026-08-17—08-21)

**Что изменилось:** сценарий решений, три типа задач, reactor defense, квесты, onboarding через quests, hotkeys, mission tooling, result settlement.

**Что решило:** бой перестал быть только наблюдением фиксированной длины и стал выполнять цель узла.

**Новая сложность:** `SessionScene` вырос с 2 223 до 3 549 строк; `combat.js` снова вырос до 1 001; возник order-sensitive data/i18n graph. Для конкретного TDZ-дефекта был выделен import-free `objective-ids.js`, но широкий цикл остался.

## Этап 8 — лестница сложности и платформенный слой (`d1a7fba`—`5532da1`, 2026-08-22—08-25)

**Что изменилось:** уровень оружия/планет, Yandex adapter, cloud seed/save, platform language, diagnostics/reporting.

**Что решило:** появилась реальная deployment boundary; Yandex-specific код не растёкся по игре.

**Новая сложность:** boot стал асинхронным, настройки зависят от platform singleton, cloud consistency строится на локальном профиле и delay/flush. Для текущего масштаба это приемлемая цена.

---

# 4. Localization/i18n как case study

## Была ли локализация предусмотрена изначально

**Факт.** Нет. В первом коммите экранные строки были русскими литералами, а каталоги одновременно хранили идентификаторы, числа и русские `name/desc/short`. В initial design localization не входила в scope.

**Факт.** Полноценный слой появился одним крупным коммитом `ffa88a7` 2026-08-12, когда проект уже вырос до 120 JS-файлов / 54 929 строк.

**Сильная гипотеза.** Локализацию отложили разумно для русскоязычного прототипа, но к моменту решения игра уже имела слишком много прямых catalog reads и строк по месту, поэтому миграция обязана была быть массовой и рискованной.

## Эволюция representative cases

1. **Экранная строка.** До `ffa88a7` подпись жила литералом в `ArsenalScene`/`SessionScene`; после — `t('section.role')` с ru/en entries. Это обычная и удачная миграция.
2. **Имя предмета.** Русский остался в catalog record, ключ другого языка выводится из устойчивого `kind + id + field`; `tItem`/`tField` стали projection boundary. Это сохранило admin/IO и save IDs.
3. **Причина отказа из core.** Сначала core отдавал готовую русскую фразу; затем часть отказов стала `msg(key, params)`, а presentation делает `render`. Однако `forge`, `wallet`, `verdict`, `briefing` и `stats` до сих пор вызывают `t()` напрямую, поэтому миграция boundary завершена неравномерно.
4. **Таблицы, вычисленные на import time.** После появления live language switch подписи пришлось превращать в ключи или функции, иначе они запоминали язык загрузки.
5. **Сравнение по подписи.** По истории три ветки логики сравнивали localized label; их заменили устойчивыми IDs. Это подтверждение того, что текст раньше был частью модели поведения.

## Текущая схема

```text
Russian catalog prose ───────────────┐
                                     ├─ catalog.js ── tField/tItem ── consumers
ru.js screen dictionary ── register ┤
en.js all translations ─── register ┘
                        global mutable `lang`
```

Плюсы:

- сохранения и admin tools работают на стабильных IDs;
- английский покрывает и catalog, и screen keys;
- placeholders/plurals и прямые catalog reads проверяются отдельным gate;
- новый язык концептуально добавляется одним dictionary file + регистрацией.

Цена:

- русский привилегирован и распределён между каталогами и `ru.js`;
- `catalog.js` импортирует 17 catalog modules;
- `all.js` имеет fan-in 45 и стал обязательным service locator для текста;
- глобальный язык делает pure-looking функции `stats`/`briefing` зависимыми от hidden state;
- `data/items.js` и `data/objectives.js` импортируют i18n обратно;
- перевод на третий язык требует поддерживать монолитный flat dictionary уже на 1 207 ключей;
- layout покрывает только латиницу/кириллицу и проверен на RU/EN длинах.

## Реальные остатки долга на 2026-09-04

`npm run i18n -- --strict` завершился с кодом 1:

- 1 207 переводимых ключей, English 100%;
- 8 прямых чтений catalog prose мимо перевода в `dom/kit.js`, `ForgeFinal.js`, `SupplyFinal.js`;
- 7 русских литералов в живом diagnostic code;
- 152 литерала в шести отключённых Canvas screens.

Документ `docs/i18n/README.md` всё ещё сообщает старый снимок «879 ключей, ноль литералов». Это не дефект самого localization engine, но факт drift между утверждаемым контрактом и исполняемым gate.

## Насколько легко добавить язык

Для третьего европейского языка — **технически просто, операционно дорого**: API расширения ясен, но нужен один файл примерно на 1 207 записей, visual pass всех fixed-height/fixed-width экранов и проверка шрифта. Для CJK/RTL — **архитектурно не готово**: один набор fonts, uppercase assumptions и layout, выверенный на кириллице/латинице.

## Как стоило спроектировать при известных будущих требованиях

- Catalog records должны хранить IDs/numbers и либо `baseText`, либо authoring prose, но runtime domain operations не должны импортировать active locale.
- `CatalogTextProjector(locale)` строит localized read models для UI/admin.
- Domain failures — только typed codes + params.
- `stats` разделяется на numeric stats и presentation rows.
- i18n registry получает catalog descriptors, а не импортирует весь data graph сам.

Это не требует переносить русские поля немедленно; достаточно изменить направление зависимости.

---

# 5. Hotspots

## 5.1. Комбинация churn, размера и зависимостей

| Файл | Строк | Коммитов с изменением | Churn | Fan-out | Наблюдение |
| --- | ---: | ---: | ---: | ---: | --- |
| `SessionScene.js` | 3 563 | 31 | 6 681 | 47 | главный runtime hotspot; меняется с combat, metrics, profile, screens, i18n |
| `ArsenalScene.js` | 2 118 | 26 | 7 476 | 48 | application kernel мета-игры и screen facade |
| `core/build.js` | 1 941 | 21 | 3 853 | 15 | сборка + inventory/mods/profile/migrations |
| `StarmapScreen.js` | 2 858 | 19 | 4 974 | 22 | graph/navigation + DOM + canvas + effects |
| `core/combat.js` | 1 001 | 23 | 4 829 | 25 | central sim state; часть сложности essential |
| `tools/test-core.mjs` | >7 200 | 30 | 11 187 | — | один test program для многих доменов |
| `tools/bench.mjs` | крупный | 22 | 13 446 | — | измерительный contract и generator отчётов |

`SessionScene ↔ ArsenalScene` менялись вместе в 23 коммитах. `build ↔ ArsenalScene` — в 17, `combat ↔ SessionScene` — в 17, `combat ↔ metrics` — в 17, `build ↔ profile` — в 15. Это подтверждает не случайный размер файлов, а системные границы изменения.

## 5.2. Центральные зависимости

- `game/theme.js`: fan-in 46. Это ожидаемо для presentation tokens.
- `core/i18n/all.js`: fan-in 45. Для глобального presentation service это высоко и повышает blast radius.
- `game/screens/layout.js`: fan-in 27. Единые координаты полезны для UI contract/smoke, но связывают много экранов.
- `data/cards.js`: fan-in 23; `data/items.js` и `data/weapons.js`: по 22. Это естественные каталоги, но изменения их shape системны.
- `core/build.js`: fan-in 19 — слишком высоко для файла, который также владеет миграциями всей мета-игры.

## 5.3. Что не следует считать hotspot только по размеру

- `data/quests.js`, `data/weapons.js`, `data/drops.js`, `i18n/en.js` велики прежде всего из-за объёма контента. Их размер — не доказательство God Object.
- `VfxDirector` и `AudioDirector` велики, но имеют высокую внутреннюю связность вокруг общего бюджета/графа и чёткие lifecycle boundaries.
- `combat.js` остаётся центральным, но после выделения `sim/*` его причины изменения гораздо ближе к одной: state/lifecycle/order of step.

---

# 6. Архитектурные проблемы

## SessionScene совмещает runtime adapter, presentation и транзакцию прогресса

**Severity:** High

**Тип:** God Object / Missing Boundary / Evolution Debt

**Где:** `src/game/scenes/SessionScene.js`, особенно `init` (строка 174), `create` (291), `_finish` (2933), `_commitMaterials` (3175), `_grantFirstClear` (3243).

**Что произошло:** класс одновременно разрешает условия вахты из URL/node/data, создаёт симуляцию, подписывает audio/VFX, рисует HUD и мир, ведёт tutorial beats, управляет временем/камерой/outro, строит отчёт, начисляет loot/xp/quests/first-clear и сохраняет профиль.

**Как это, вероятно, появилось:**  
**Факт.** Изначально сцена имела 863 строки и отвечала только за одну 60-секундную автосессию.  
**Сильная гипотеза.** Каждое новое требование было локально разумно добавить в owner текущей вахты: contract → arena → faction → objective → hand → quest beat → settlement. Отдельного application owner для «сыграть вахту и применить результат» не существовало.

**Доказательства:**

- рост 863 → 1 311 → 1 543 → 2 379 → 3 563 строк;
- 31 затронувший коммит, максимальная частота среди runtime-файлов;
- fan-out 47;
- co-change с `combat` 17 раз, `metrics` 14, `run` 14, `StarmapScreen` 14, `profile` 13;
- профиль передаётся в сцену живой ссылкой, хотя только `build` и `boosts` снимаются immutable snapshots;
- 16 прямых присваиваний полям `this.profile` и итоговый `saveProfile` внутри presentation scene.

**Почему это проблема:** изменение результата вахты требует понимать Phaser lifecycle; изменение UI может задеть application transaction; headless run не проверяет settlement; повторный вход/выход зависит от ручного сброса десятков latch/state fields.

**Последствия:** высокий regression risk, сложное unit-тестирование result application, co-change несвязанных доменов, onboarding cost, риск повторной/частичной выдачи при исключении между мутациями.

**Как должно было быть спроектировано изначально:** для исходного scope текущая сцена была приемлема. Граница стала нужна при появлении persistence-changing результата: `resolveWatchSpec(input)`, `run/watch session runtime`, `settleWatch(profile, report) -> { profile, events }`. Scene должна отображать и передавать команды.

**Что делать сейчас:**

1. Зафиксировать существующий settlement golden tests на representative reports.
2. Выделить pure `settleWatch(profile, report, context)` из `_finish/_commitMaterials/_grantFirstClear`.
3. Выделить `resolveWatchSpec` из `init`, общий с parity/debug URL.
4. Только затем делить visual responsibilities на HUD/world/onboarding controllers, не меняя Phaser scene ownership.

**Стоит ли исправлять:** Да. Это главный hotspot, и первый этап можно сделать без визуального rewrite.

## ArsenalScene стала application kernel и ручным DI-фасадом всей мета-игры

**Severity:** High

**Тип:** God Object / Coupling / Missing Boundary

**Где:** `src/game/scenes/ArsenalScene.js`, `claimRewards` (219), `_context` (1095), mutation methods 1316—2047, navigation 721+, launch 2091+.

**Что произошло:** Scene владеет mutable profile, routing, screens, modals, tutorial gates, previews, purchases, inventory sale, crafting lifecycle, quest rewards, debug grants, persistence and messaging. `_context()` вручную экспортирует широкий интерфейс почти ко всем этим действиям.

**Как это, вероятно, появилось:**  
**Факт.** В initial design `ArsenalScene` должна была менять маленькую сборку.  
**Сильная гипотеза.** Когда арсенал стал shell всей мета-игры, существующее правило «экраны профиль не мутируют, мутирует сцена» сохранило один owner, но owner не был преобразован в отдельный store/application service.

**Доказательства:**

- рост 465 → 660 → 684 → 1 894 → 2 118 строк;
- 26 коммитов, churn 7 476, fan-out 48 — максимум в runtime;
- 36 прямых присваиваний полям профиля, 19 save calls;
- `_context` содержит десятки getters/commands от бюджета до крафта и навигации;
- `build ↔ ArsenalScene` менялись вместе 17 раз, `profile ↔ ArsenalScene` 14.

**Почему это проблема:** хорошее правило единственного owner реализовано в framework object. Application logic невозможно использовать/тестировать без сцены или ручной сборки большого ctx; граница screen contract меняется при любой новой мета-функции.

**Последствия:** высокий fan-out, oversized mocks, несвязанные причины изменения, риск забыть save/render/note в одной из команд, затруднённый второй frontend или автоматический player model.

**Как должно было быть спроектировано изначально:** для четырёх слотов и двух оружий текущий подход был оптимален. После появления wallet/forge/quests стоило ввести `ProfileSession`/`MetaGameStore` с командами, оставив `ArsenalScene` composition root и router.

**Что делать сейчас:** создать тонкий store с `dispatch(command) -> outcome`, который атомарно заменяет профиль, сохраняет его и публикует изменения. Переносить команды вертикальными срезами: craft, mods, rewards, build editing. Не переносить screen rendering одновременно.

**Стоит ли исправлять:** Да, но после/вместе с централизацией profile codec.

## Схема профиля описана в нескольких местах, а build.js владеет несвязанными доменами

**Severity:** High

**Тип:** Cohesion / Historical Debt / Missing Boundary

**Где:** `src/core/build.js` (`defaultProfile` 1792, `repairProfile` 1820 и множество repair-функций), `src/game/profile.js` (`saveProfile` 39—111).

**Что произошло:** `build.js` начинался как сборка оружия, а теперь содержит build slots, budgets, weapon ownership, mods, inventory delivery, XP, starmap, records, blueprints, forge, quests, tally и repair полного profile aggregate. `game/profile.js` независимо поддерживает ручной JSON whitelist тех же полей.

**Как это, вероятно, появилось:**  
**Факт.** Файл вырос с 220 до 2 155 строк к 2026-08-12; удаление полярностей сократило его до 1 770, затем новые progression requirements подняли до 1 941.  
**Сильная гипотеза.** Repair был помещён рядом со сборкой, потому изначально профиль и был сборкой. Новые поля продолжали добавляться к существующему repair root.

**Доказательства:**

- `defaultProfile` и return из `repairProfile` перечисляют 16 верхнеуровневых полей;
- `saveProfile` перечисляет тот же набор вручную третьим описанием;
- комментарии `profile.js` многократно предупреждают: забытая «третья правка» тихо теряет данные после reload;
- `build ↔ profile` co-change 15 коммитов;
- schema key называется `herald.arsenal.v1`, хотя хранит карту, кузницу, квесты и весь прогресс — архитектурная окаменелость исходного scope.

**Почему это проблема:** корректность persistence зависит от синхронного ручного обновления default, repair и serializer. Компилятор/типизация этого не проверяют. Имя файла скрывает настоящий blast radius.

**Последствия:** риск тихой потери прогресса, большие merge conflicts, трудный review, невозможность независимо версионировать/migrate bounded slices, ложная связанность features.

**Как должно было быть спроектировано изначально:** исходный `build.js` был разумен. После второго-третьего несборочного поля нужен был `profile/schema.js` как единственный codec, а feature-specific defaults/repair — в своих модулях.

**Что делать сейчас:**

1. Ввести `encodeProfile(profile)` рядом с `defaultProfile/repairProfile` и удалить whitelist из browser adapter.
2. Добавить round-trip test, который автоматически сравнивает top-level keys default/repaired/encoded.
3. Перенести profile root в `core/profile/*`; оставить re-export из `build.js` на время миграции.
4. Затем разнести slices (`build`, `inventory`, `forge`, `quests`, `progress`) без изменения wire format.

**Стоит ли исправлять:** Да. Первый шаг небольшой, а закрывает самый опасный silent-failure mode.

## Локализация развернула зависимость обратно в data и создала реальный цикл

**Severity:** High

**Тип:** Coupling / Missing Boundary / Evolution Debt

**Где:** `src/core/i18n/catalog.js:24+`, `src/core/data/items.js:34`, `src/core/data/objectives.js:68`, `src/core/data/starmap.js:103+`, `src/core/data/objective-ids.js`, а также `stats/wallet/forge/briefing/verdict`.

**Что произошло:** localization registry импортирует каталоги, чтобы вывести catalog keys; каталоги и core presentation helpers импортируют active translator обратно. Статический SCC включает семь модулей: `quests`, `items`, `objectives`, `missions`, `starmap`, `i18n/catalog`, `i18n/all`.

**Как это, вероятно, появилось:**  
**Факт.** I18n появился после 54k строк кода и должен был сохранить русскую prose внутри catalog authoring flow.  
**Факт.** `objective-ids.js` извлечён после реального `Cannot access 'OBJECTIVES' before initialization`.  
**Сильная гипотеза.** Локальное выделение import-free IDs починило конкретный порядок загрузки, поэтому более широкая смена направления зависимостей была отложена.

**Доказательства:**

- реальный SCC из семи файлов;
- комментарий в `starmap.js` документирует order-sensitive TDZ failure;
- `catalog.js` импортирует 17 data sources;
- `all.js` имеет fan-in 45;
- `i18n/index.js` держит скрытое mutable global state языка;
- `core` functions возвращают уже переведённые строки, поэтому одинаковые numeric inputs могут дать разные outputs в зависимости от process-global locale.

**Почему это проблема:** import order снова может стать функциональным при добавлении catalog source; headless tools платят за presentation graph; domain/presentation tests влияют друг на друга через global language; круг затрудняет lazy loading и независимое использование data modules.

**Последствия:** TDZ regressions, высокий blast radius локализации, сложные mocks, неочевидная чистота функций, затруднённый третий consumer core.

**Как должно было быть спроектировано изначально:** data экспортирует descriptor lists/records без импорта i18n; presentation вызывает projector с явным locale. Domain failures возвращают codes, а не prose.

**Что делать сейчас:** сначала запретить новые imports `data -> i18n` test-gate. Затем вынести `resolveItem` в localized catalog projector, перевести `objectives.goalText` на token/template data, передавать locale/translator в UI projection helpers. Не переносить все строки из каталогов одним большим коммитом.

**Стоит ли исправлять:** Да. Уже был production-class загрузочный дефект; это не теоретическое нарушение слоёв.

## Незавершённая UI-миграция оставила три поколения реализации

**Severity:** Medium

**Тип:** Historical Debt / Architectural Fossil / Evolution Debt

**Где:** старые `HubScreen`, `CardEditor`, `BuildEditor`, `SuitEditor`, `ForgeScreen`, `ModsScreen`, `SupplyScreen`, промежуточные `*Dom`, `game/wiki.js`, `game/weaponPassport.js`, `dom/dnd.js`, `dom/cardops.js`; текущая развилка документирована в начале `ArsenalScene`.

**Что произошло:** runtime перешёл на `BuildDom` и `*Final`, но старые подграфы сохранены в source tree. Они недостижимы из `main.js`, хотя часть tooling продолжает сканировать их.

**Как это, вероятно, появилось:**  
**Факт.** Комментарии прямо говорят «файлы оставлены до конца переноса».  
**Факт.** Коммит `aa6e23f` был очень крупным и сознательно оставил обратимый путь.  
**Сильная гипотеза.** После успешного включения новых экранов cleanup не получил отдельного завершённого этапа.

**Доказательства:**

- 16 подтверждённо отключённых runtime-файлов, суммарно 8 356 строк;
- текущий i18n audit отдельно считает 152 литерала шести unplugged Canvas screens;
- словари сохраняют секции с комментариями на старые screen files;
- admin tooling содержит явные списки старых экранов;
- новые `wikiFinal`/`ForgeFinal` прямо ссылаются на старые реализации как источник перенесённого поведения.

**Почему это проблема:** поиск и аудит показывают ложных потребителей; разработчик может исправить не ту реализацию; словари/инструменты несут мёртвую поверхность; naming `Dom`/`Final` фиксирует временное состояние как постоянную архитектуру.

**Последствия:** onboarding cost, ошибочные patches, лишний i18n debt, трудный impact analysis. На runtime bundle это почти не влияет, потому мёртвый подграф не импортируется.

**Как должно было быть спроектировано изначально:** большой UI rewrite разумно проводить через временный strangler, но каждый переключённый экран должен завершаться удалением старого пути и переименованием нового.

**Что делать сейчас:** построить автоматический reachability report, подтвердить отсутствие imports/tests, удалить поколения вертикально по экрану, обновить `tools/i18n.mjs` и admin lists, затем переименовать `*Final` в смысловые имена. Делать отдельными recoverable commits.

**Стоит ли исправлять:** Да, после фикса падающих gates. Риск невысок, а когнитивная отдача велика.

## StarmapScreen — feature-монолит со смешанными причинами изменения

**Severity:** Medium

**Тип:** God Object / Cohesion

**Где:** `src/game/screens/StarmapScreen.js`: DOM card/list (337—614), sky/effects (940+), system/planet rendering (1455+ / 2202+), nodes and selection (2572+).

**Что произошло:** один screen class владеет state/navigation, unlock rules, DOM inspection cards, procedural background, orbits, planets/moons/rings, camera lens, animation and node interaction.

**Как это, вероятно, появилось:**  
**Факт.** Файл появился уже на 1 749 строках в первом map stage и вырос до 2 858.  
**Сильная гипотеза.** Карта воспринималась как один экран/feature, поэтому cohesion оценивалась по пользовательскому экрану, а не по независимым жизненным циклам renderers и selection model.

**Доказательства:** 19 коммитов, churn 4 974, fan-out 22; методы образуют по меньшей мере четыре независимых кластера; co-change с layout 12 и SessionScene 14.

**Почему это проблема:** визуальная правка кольца требует работать в том же owner, что node progression и DOM card; тестировать навигацию без Phaser rendering трудно; при следующем режиме карты файл продолжит расти.

**Последствия:** regression risk, дорогой visual review, невозможность отдельно профилировать renderer, конфликтующие изменения.

**Как должно было быть спроектировано изначально:** один `StarmapScreen` как coordinator плюс `StarmapModel`, `SystemRenderer`, `PlanetRenderer`, `StarmapInspectorDom`. Это не обязано быть пакетами/DI; достаточно focused modules.

**Что делать сейчас:** сначала вынести pure selection/unlock/view-state, затем stateless drawing helpers с явным input. Не разбивать на классы один метод за раз и не менять визуал одновременно.

**Стоит ли исправлять:** Возможно. Приоритет возрастает перед следующим крупным map feature; сейчас profile/scene/i18n risks выше.

## Проверочные gates перестали быть достоверной зелёной линией

**Severity:** High

**Тип:** Testing Architecture / Historical Debt

**Где:** `tools/test-core.mjs`, `tools/i18n.mjs`, утверждения в README/docs.

**Что произошло:** build проходит, но два заявленных contract gates падают. `npm test` сообщает 6 failures и 7 skipped checks; strict i18n сообщает 8 catalog-read divergences и 7 live literals. При этом docs описывают прошлое зелёное состояние.

**Как это, вероятно, появилось:**  
**Факт.** Test-core менялся в 30 коммитах с churn 11 187, bench — 22/13 446; правила проекта эволюционировали почти каждый день.  
**Сильная гипотеза.** Один большой custom test program удобен для cross-domain invariants, но обновление каталога/quests/map может одновременно инвалидировать fixture, gate и docs. Нет внешнего CI-gate, который остановил бы текущий HEAD.

**Доказательства:**

- `npm test` exit 1: failures по hits-to-kill, elements, RNG baseline, moved route children, objective step ordering, quest UI keys;
- 7 тестов пропущены из-за отсутствующих `rate/chain` entries;
- `npm run i18n -- --strict` exit 1;
- `npm run build` при этом exit 0, поэтому компилируемость не сигнализирует о contract drift;
- docs/i18n всё ещё утверждает прежние 879/0 вместо текущих 1 207/7/8.

**Почему это проблема:** главная сила проекта — доказуемые инварианты. Когда gates красные долго или skip трактуется как нейтральный, архитектурные решения снова принимаются без своей основной страховки.

**Последствия:** ложная уверенность, невозможность безопасно начинать structural refactoring, смешение product regression и stale expectation, дорогая диагностика общего скрипта.

**Как должно было быть спроектировано изначально:** custom numerical harness оправдан, но тесты должны быть сгруппированы по contract suites, иметь явную политику skip и выполняться в CI вместе с strict i18n/doc check.

**Что делать сейчас:** классифицировать 6 failures как regression или stale expectation; запретить silent skip для обязательных invariants; разделить запуск на быстрые suites без изменения общей реализации; обновлять generated snapshots/docs только после зелёного gate; добавить CI.

**Стоит ли исправлять:** Да, первым действием. Без зелёной линии риск остальных рефакторингов неоправданно высок.

## Один eager bundle скрывает границы загрузки

**Severity:** Low

**Тип:** Build / Performance Boundary

**Где:** `src/main.js` и eager import graph всех runtime screens/i18n/assets.

**Что произошло:** production build создаёт JS chunk 2 004.77 kB (559.22 kB gzip; source map 15.4 MB) и Vite предупреждает о chunk >500 kB.

**Как это, вероятно, появилось:** две Phaser scenes загружаются вместе; screen registry и `i18n/all.js` подтягивают все runtime UI/catalogs. Для локального прототипа это было самым простым и надёжным boot path.

**Доказательства:** текущий `npm run build`, 168 transformed modules, один основной JS chunk.

**Почему это проблема:** cold start платформенной игры платит за мета-экраны и обе локали до первого кадра. Однако значительная доля — Phaser, и без measurement нельзя утверждать, что это пользовательский bottleneck.

**Последствия:** потенциально более медленный mobile startup и более дорогая parse/compile phase.

**Как должно было быть спроектировано изначально:** для initial scope eager bundle был правильным выбором.

**Что делать сейчас:** сначала измерить startup на целевых устройствах. Если проблема подтверждена — лениво грузить тяжелые modal/wiki/map presentation modules после boot, не дробить core без пользы.

**Стоит ли исправлять:** Возможно, только по измерению.

---

# 7. Overengineering и underengineering

## 7.1. Что похоже на overengineering, но им не является

### EventBus и строгие payloads

Есть одна реализация EventBus, но это не бесполезная abstraction: на события одновременно подписаны VFX, audio, metrics и scene feedback; headless consumer использует тот же поток. Strict freeze включён только в тестах, поэтому runtime не платит полной ценой.

### Раздельные RNG streams

Они появились очень рано, но поздние loot/xp mechanics реально использовали расширяемость. История прямо подтверждает, что новый stream не сдвигал соседние эталоны.

### `core/sim/*` свободными функциями

Это не wrapper-over-wrapper. Выделение сократило `combat.js` на ~1 200 строк и сохранило единый state owner.

### Большой измерительный tooling

Объём tools высок относительно игры, но предметная цель проекта — доказуемый баланс и browser↔Node parity. Это essential complexity. Проблема не в существовании tools, а в текущей красной линии и монолитности suites.

## 7.2. Реальное первоначальное переусложнение

Сильных случаев мало.

1. **Полярности/особые слоты/Печать.** Механика распространилась по build/cards/craft/drops/UI до подтверждения ценности, затем удалена `e0ccedd` с чистым уменьшением почти на 800 строк. Гибкость не окупилась. Исправлять сейчас почти нечего: команда уже выбрала правильное действие — удалить.
2. **Слишком богатая profile schema в `100f744`.** Часть полей появилась впереди UI, но была использована в ближайшие дни. Это краткий speculative cost, не долг.
3. **Сохранение старых UI поколений как rollback path.** Во время `aa6e23f` было разумно; после стабилизации стало historical debt.

Искать пять «лишних интерфейсов» здесь было бы искусственным: интерфейсов, factories, repositories, DI и generic service layers в проекте почти нет.

## 7.3. Что было недопроектировано

1. Application boundary между screens и mutation профиля.
2. Profile codec/schema как один источник default/repair/serialization.
3. Localization boundary и явный locale до появления второго языка.
4. Ownership результата вахты отдельно от Phaser scene.
5. Завершающий этап удаления старого UI после strangler migration.

---

# 8. Архитектурные окаменелости

| Элемент | Почему появился | Почему остался | Текущая оценка |
| --- | --- | --- | --- |
| ключ `herald.arsenal.v1` | профиль первоначально был арсеналом | переименование ломает сохранения и smoke literals | имя устарело, значение менять не надо; документировать alias |
| Canvas `*Screen` и промежуточные `*Dom` | предыдущее UI и rollback при v4 | cleanup не завершён | удалить после reachability/test audit |
| `*Final` в именах | различить новую манеру во время миграции | новое поколение стало production | после удаления старого переименовать |
| старые dictionary sections | переводы прежних экранов | i18n tooling продолжает их видеть | удалить вместе с кодом |
| `objective-ids.js` | разорвать конкретный TDZ path | нужен как import-free contract | оставить, но не считать полным решением цикла |
| `core/build.js` как имя profile root | профиль состоял из build | функции наращивались рядом с repair root | сделать compatibility facade, реальный root перенести |
| README/i18n snapshots | фиксировали доказанное состояние дня | не генерируются из gates | заменить generated block или дату/команду сделать явной |

---

# 9. Как архитектура росла через патчи

## Цепочка 1 — profile

```text
профиль = сборка
→ добавили wallet/ownership
→ добавили xp/starmap/record
→ добавили inventory/blueprints/forge/mods
→ добавили quests/tally
→ каждый feature требует default + repair + serialize + scene mutation
→ build.js стал profile aggregate, ArsenalScene — application kernel
```

Каждый шаг локально разумен; глобальная проблема — не было момента сменить owner после того, как профиль перестал быть сборкой.

## Цепочка 2 — session

```text
60 секунд авто-боя
→ смерть и два режима конца
→ contracts/arenas/factions
→ loot/hand/progression
→ node objectives и decisions
→ onboarding beats и first-clear settlement
→ SessionScene владеет и кадром, и бизнес-транзакцией результата
```

## Цепочка 3 — localization

```text
русские строки по месту + prose в catalogs
→ нужен английский без поломки admin writers
→ catalog.js импортирует все sources
→ consumers начинают читать tField/t напрямую из core
→ data и i18n замыкаются
→ TDZ чинится import-free листом IDs
→ широкий цикл сохраняется
```

## Цепочка 4 — UI

```text
Canvas screens
→ DOM variants
→ новый визуальный стандарт «final»
→ runtime переключён на новые реализации
→ старые оставлены для обратимости
→ tooling и словари продолжают считать мёртвую поверхность
```

---

# 10. Классификация technical debt

## Accidental Complexity

- profile schema в нескольких местах;
- широкий scene ctx и ручные save/render/note sequences;
- три поколения UI;
- data↔i18n cycle;
- stale generated/documented counts.

## Essential Complexity

- детерминированный fixed-step combat;
- раздельные RNG streams;
- browser↔Node parity;
- разные rules of end для player session и benchmark denominator;
- objective/contract/arena/faction как независимые оси;
- content validation и balance gates;
- VFX/audio budgets при автоогне;
- platform init before first frame and cloud/local fallback.

## Historical Debt

- `build.js` и storage key, отражающие старый scope;
- старые Canvas/DOM screens;
- `*Final` naming;
- docs snapshots, переставшие быть текущими.

## Premature Architecture

- удалённая система полярностей/особых слотов;
- кратковременно — schema fields до их features, но будущее пришло почти сразу и долг не материализовался.

## Missing Architecture

- atomic application commands над профилем;
- единственный profile codec;
- typed result settlement;
- явная localization projection boundary;
- CI policy для mandatory/skip gates.

---

# 11. Архитектурная ретроспектива

## 1. Первоначальный замысел

Минимальный Phaser shell вокруг детерминированного engine-independent ядра. Две сцены соответствовали двум действиям игрока; события отделяли числовой бой от обратной связи; стенд был частью продукта разработки, а не тестовой надстройкой.

## 2. Что изначально было переусложнено

Главный подтверждённый случай — система полярностей/особых слотов. Остальные ранние abstractions в основном окупились. Называть EventBus, RNG streams или parity «переусложнением» противоречило бы истории их использования.

## 3. Что изначально было недопроектировано

Сцены получили ownership, достаточный для прототипа, но без точки эволюции в application layer. Profile persistence предполагала мало полей. Localization не имела boundary. UI migration не имела обязательного cleanup gate.

## 4. Ключевые поворотные моменты

1. `7397904`: deterministic core + two scenes + bench/parity.
2. `100f744`: RNG streams, schema 2, suit survival.
3. `500b2f7`: endless/contract/arena expansion показал предел `CombatSim` и scenes.
4. 2026-08-04: `sim/*` extraction — удачное разделение mechanics/state.
5. `ffa88a7`: i18n после 55k LOC — массовая миграция и новый dependency hub.
6. `e0ccedd`: удаление полярностей — сознательное уменьшение сложности.
7. `aa6e23f`: UI v4 — качественный скачок с незакрытым strangler tail.
8. `68799b7`: objectives и снятие fixed duration — SessionScene становится application owner.
9. `0e78187`—`bf258e8`: quests/onboarding срастаются с профилем и вахтой.
10. `b0363b0`: platform boundary добавлена удачно и локально.

## 5. Как накапливался technical debt

Реальные будущие требования приходили гораздо быстрее, чем исходный прототип предполагал. Сильная core boundary позволяла безопасно добавлять механику, но одновременно маскировала рост shell: parity продолжал доказывать бой, пока persistence/result/UI orchestration разрастались вне его охвата. Там, где был числовой contract (`CombatSim`), декомпозиция произошла. Там, где contract был неформальным (`profile`, scene actions, UI migration), owner продолжал расширяться.

## 6. Главные архитектурные ошибки

1. Не выделить profile/application owner после появления экономики и квестов.
2. Оставить settlement внутри SessionScene.
3. Дублировать profile schema в serializer.
4. Разрешить active localization войти обратно в data/core graph.
5. Не завершить cleanup UI v4 и не переименовать production generation.

## 7. Что было сделано правильно

1. Один CombatSim для browser и Node.
2. Fixed-step + seeded independent RNG streams.
3. EventBus отделяет числовой outcome от VFX/audio/metrics.
4. `sim/*` extraction сохранил единый state owner.
5. Build snapshot перед вахтой.
6. Repair повреждённых сохранений и explicit migration order.
7. Platform adapter локализует Yandex-specific поведение.
8. Content IDs и derived translation keys сохраняют save/admin compatibility.
9. Отказ от полярностей после оценки стоимости.
10. Документация часто фиксирует не только решение, но и его измеряемую причину.

## 8. Что выглядит плохо, но оправдано

- Большой EventBus vocabulary отражает реальные доменные события и несколько потребителей.
- Два режима окончания вахты нужны для честного benchmark denominator.
- `SPAWN` как live payload нарушает полную immutability, но позволяет view следовать за объектом без копирования каждый tick; исключение явно документировано и тестируется.
- Русский как authoring language каталогов асимметричен, но сохранил admin writers и stable IDs; проблема в направлении runtime dependencies, не в самом authoring choice.
- Большие data/i18n files частично являются объёмом контента, а не низкой cohesion.
- LocalStorage как синхронная правда с cloud mirror — прагматичная граница для одной web-платформы.

## 9. Архитектура, которая была бы оптимальной при известных требованиях

```text
core/sim                 deterministic combat + reports
core/catalog             IDs, numeric/content records, no active locale
core/profile             schema, codec, migrations, immutable slices
application/meta         commands: craft/buy/equip/claim
application/watch        resolve spec + settle report
presentation/i18n        locale + catalog projection + UI messages
game/phaser              two scenes as composition/lifecycle adapters
game/screens             one current implementation per screen
platform                 local/cloud/diagnostic adapters
tools                    reuse core + application contracts
```

Это пять практических границ, а не «слой на каждую функцию». Репозитории, DI-container, DTO chains и use-case class на каждый click не нужны.

## 10. Архитектура, к которой стоит двигаться сейчас

1. Вернуть зелёные mandatory gates и определить baseline.
2. Централизовать encode/decode profile без изменения JSON format.
3. Выделить pure settlement вахты; покрыть golden tests.
4. Ввести `MetaGameStore` и переносить команды по feature slice.
5. Запретить новые `data -> i18n`, затем разорвать существующий SCC projection-слоем.
6. Удалить отключённые UI generations и переименовать `*Final`.
7. Разделять Starmap/visual controllers только перед соответствующим feature growth.

---

# 12. Приоритизация

| Проблема | Severity | Источник | Стоимость для проекта | Сложность исправления | Риск рефакторинга | Приоритет |
| --- | --- | --- | --- | --- | --- | ---: |
| Красные/пропущенные contract gates | High | Evolution/Historical | лишает проект главной страховки | Medium | Low | 1 |
| Profile schema/serializer в нескольких местах | High | Missing Architecture | риск тихой потери прогресса | Low→Medium | Low | 2 |
| Settlement внутри SessionScene | High | Underengineering | смешивает UI и атомарность наград | Medium | Medium | 3 |
| ArsenalScene как application kernel | High | Underengineering | высокий blast radius мета-функций | High | Medium | 4 |
| Data↔i18n SCC и hidden locale | High | Late Requirement | TDZ и системная связанность | High | Medium | 5 |
| Три поколения UI / 8 356 строк | Medium | Historical Debt | onboarding и ложный impact surface | Medium | Low→Medium | 6 |
| StarmapScreen feature-монолит | Medium | Cohesion | дорогие map changes | Medium | Medium | 7 |
| Один eager JS bundle | Low | Simplicity | возможный cold-start cost | Medium | Medium | 8 |

## Top-5 ошибок первоначального проектирования

1. Владение всей мета-игрой закрепили за Phaser scene без критерия, когда выделять application owner.
2. Профиль приравняли к build-модулю и не ввели единый codec при расширении схемы.
3. Settlement результата не отделили от presentation lifecycle.
4. Текст каталогов не получил явной runtime projection boundary до второго языка.
5. Не определили migration-done criterion: новый UI включён, старый должен быть удалён.

## Top-5 проблем, появившихся в процессе развития

1. `SessionScene` вырос до 3 563 строк и 47 прямых dependencies.
2. `ArsenalScene` стал большим command facade/store/router одновременно.
3. I18n создал реальный семимодульный cycle и уже вызвал TDZ failure.
4. UI v4 оставил 8 356 строк отключённого runtime-кода.
5. Mandatory test/i18n gates и документированный baseline разошлись с HEAD.

## Top-5 архитектурных решений, которые оказались удачными

1. Engine-independent deterministic combat core.
2. Browser↔Node parity на одном `CombatSim`.
3. Independent seeded RNG streams.
4. Event-driven VFX/audio/metrics с immutable-by-contract payloads.
5. Выделение `core/sim/*` и поздний platform adapter без утечки SDK.

## Top-5 действий с максимальной отдачей

1. Разобрать 6 failed + 7 skipped core checks и 8 strict i18n divergences до зелёного baseline.
2. Ввести `encodeProfile` и автоматический schema round-trip, убрав ручной whitelist из `game/profile.js`.
3. Вынести `settleWatch` из `SessionScene` с golden tests и атомарной заменой профиля.
4. Удалить reachability-confirmed старые UI реализации и очистить i18n/admin lists.
5. Поставить dependency gate `core/data` не импортирует active i18n и начать вынос localized projections.

---

# 13. Верификация и ограничения анализа

## Выполненные проверки

- `npm run build` — **PASS**, Vite собрал 168 modules; warning о 2.0 MB chunk.
- `npm test` — **FAIL**, 1 006 passed / 7 skipped / 6 failed.
- `npm run i18n -- --strict` — **FAIL**, English 1 207/1 207, но 8 прямых catalog reads и 7 live literals.
- Статический import graph — 149 JS files / 765 internal edges / 1 SCC из 7 модулей.
- Git churn — все 89 коммитов через `git log --numstat`.
- Co-change — пары файлов на уровне коммита; крупные squash-like commits могут завышать связь, поэтому метрика использована только вместе с code evidence.
- Runtime reachability — от `src/main.js`; tools-only модули (`core/run`, invariants, targets) не считались мёртвым кодом. UI fossil оценивался только после поиска всех импортёров в `src` и `tools`.

## Ограничения

- История короткая и очень плотная: 26 дней. «Почему команда решила» выводилось только там, где это записано в docs/comments/commit, иначе использована маркировка гипотезы.
- Некоторые крупные коммиты (`Update`, `Finished`, UI v4) объединяют несколько причин изменения и снижают точность исторической атрибуции.
- Аудит не запускал полный visual smoke/parity в браузере; выводы о визуальном поведении основаны на коде, docs и build/test outputs.
- Рабочее дерево содержит пользовательские изменения. Build/test отражают текущий workspace, но failures не приписываются этим изменениям без отдельного bisect.

## Итоговая оценка

Архитектура проекта не «сломана». У неё есть сильное, доказанное ядро и слабее оформленная оболочка, которая за месяц вобрала объём требований полноценной мета-игры. Главный исторический урок здесь не «надо было сразу строить Clean Architecture», а более конкретный:

> Граница, защищённая исполняемым контрактом, эволюционировала хорошо; граница, защищённая только договорённостью о владельце, расширялась вместе с владельцем, пока тот не стал hotspot.

Поэтому следующий архитектурный этап должен начинаться не с массового разрезания файлов, а с восстановления зелёных контрактов и превращения неформальных границ profile/settlement/localization в исполняемые интерфейсы. После этого проект можно упрощать по одному вертикальному срезу без rewrite и без потери его главного преимущества — воспроизводимости.
