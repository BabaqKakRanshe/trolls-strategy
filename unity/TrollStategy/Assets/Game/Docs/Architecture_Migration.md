# Архитектурная документация миграции: TrollStrategy (Phaser 4 -> Unity 6)

## 1. Current Architecture (Что было в Phaser 4)

В исходном Phaser-проекте игровая логика и рендеринг были частично смешаны:
- **`src/content/catalog.ts`**: Статические константы в коде (цены, тайминги, размеры зданий).
- **`src/domain/colony.ts`**: Чистая логика поселения (`GameState`, `applyCommand`, `tickColony`), которая была качественно написана, но привязана к TS интерфейсам.
- **`src/application/GameSession.ts`**: Владелец сессии и снапшотов.
- **`src/game/ColonyScene.ts`** (**Mega-class, 885 строк**): Содержал отрисовку сетки, создание контейнеров зданий и юнитов, управление полосками прогресса, прямоугольник выделения (marquee drag-box), полупрозрачное превью постройки шахты, отображение маршрутов руды, тултипы и обработку событий указателя.
- **`src/ui/AppUi.ts`**: Разметка HTML DOM и прямое управление стилями.

### Выявленные проблемы в Phaser
1. Нехватка визуального инструментария (Scene View, Prefabs, Inspector, Tile Palette).
2. Раздутый класс `ColonyScene.ts` (God Object отображения и ввода).
3. Привязка UI к DOM-структуре браузера.

---

## 2. Target Architecture (Целевая архитектура в Unity 6)

В Unity 6 проект разделён на 4 чётких слоя с использованием строгой модульности и namespaces:

```text
TrollStrategy
 +-- Content      (ScriptableObjects: определения зданий, юнитов, экономики)
 +-- Domain       (Чистый C#, POCO/Records: GameState, EntityState, Simulation, Commands)
 +-- Application  (GameSession, InteractionController, Snapshots)
 +-- Presentation (Unity-specific: Views, TilemapWorldView, Visual Managers, Prefabs)
 +-- UI           (uGUI + TextMeshPro: Presenter, ResourceBar, Docks)
 L-- Bootstrap    (Composition Root: GameBootstrap, GameSceneBuilder)
```

### Основные Unity-системы и их ответственность:
1. **`GameContentCatalog` & ScriptableObjects (`BuildingDefinition`, `UnitDefinition`, `EconomyConfig`)**:
   - Data-driven конфигурация без хардкода в C#-коде.
   - Разделение `Definition` (статические данные) и `RuntimeState` (мутабельное состояние экземпляра).
2. **`ColonySimulation` & `GameState`**:
   - Чистая детерминированная C# симуляция поселения.
   - Фиксированный шаг симуляции экономики (250 мс).
   - Не зависит от `MonoBehaviour`, физики или частоты кадров.
3. **`GameSession`**:
   - Единственный владелец мутабельного состояния `GameState`.
   - Принимает типизированные команды `IGameCommand`, возвращает `CommandResult`.
   - Генерирует неизменяемые снапшоты `GameSnapshot` для представления.
4. **`InteractionController`**:
   - Машина состояний взаимодействия игрока с миром (Neutral, PlacingMine, PlacingUnits, ChoosingWorkTarget, ChoosingHaulSource, ChoosingHaulDestination).
   - Хранит выделение юнитов и формирует намерения пользователя.
5. **Presentation Layer (View-компоненты)**:
   - `TilemapWorldView`: Проекция координат сетки `Cell(x, y)` в координаты мира Unity.
   - `BuildingVisualsManager` & `BuildingView`: Префабы зданий, шкалы руды, подписи TextMeshPro.
   - `UnitVisualsManager` & `UnitView`: Префабы существ, интерполяция движения, спрайты покоя/ходьбы, иконка груза.
   - `PlacementPreviewRenderer`: Полупрозрачный "призрак" шахты (зелёный/красный индикатор валидности).
   - `SelectionBoxRenderer`: Прямоугольная рамка множественного выделения мышью.
   - `HaulRouteVisualizer`: Линии активных маршрутов доставки руды (`LineRenderer`).
   - `MapInputHandler`: Новая Unity Input System (мышь, Escape, горячие клавиши B/W/H/R/1).
6. **UI Layer (uGUI)**:
   - `HudPresenter`: Связывает `GameSession` и `InteractionController` с UI-панелями.
   - `ResourceBarView`, `ShopDockView`, `CommandDockView`, `StatusMessageView`.

---

## 3. Migration Mapping

| Phaser 4 (Исходник) | Ответственность | Замена в Unity 6 |
|---|---|---|
| `catalog.ts` | Константы параметров юнитов и зданий | `ScriptableObject`: `BuildingDefinition`, `UnitDefinition`, `EconomyConfig` |
| `colony.ts` | Состояние, команды и симуляция | `TrollStrategy.Domain` (`GameState`, `ColonySimulation`, `Commands`) |
| `GameSession.ts` | Квантование времени 250мс и снапшоты | `TrollStrategy.Application.GameSession` |
| `MapInteractionController.ts` | Выбор юнитов, целеуказание, режим постройки | `TrollStrategy.Application.InteractionController` |
| `ColonyScene.ts` (Тайлы и сетка) | Отрисовка фона и линий сетки | `TilemapWorldView` + процедурная текстура сетки 14x14 |
| `ColonyScene.ts` (Здания) | Контейнеры спрайтов зданий | `BuildingVisualsManager` + префаб `BuildingPrefab` |
| `ColonyScene.ts` (Юниты) | Контейнеры спрайтов юнитов | `UnitVisualsManager` + префаб `UnitPrefab` |
| `ColonyScene.ts` (Превью шахты) | Graphics-прямоугольник под курсором | `PlacementPreviewRenderer` |
| `ColonyScene.ts` (Рамка выбора) | Marquee drag rectangle | `SelectionBoxRenderer` |
| `ColonyScene.ts` (Маршруты) | Линии между зданиями | `HaulRouteVisualizer` (`LineRenderer`) |
| `AppUi.ts` | DOM-интерфейс | uGUI Canvas + TextMeshPro (`HudPresenter`) |
| `main.ts` | Composition Root | `GameBootstrap.cs` на сцене |

---

## 4. Data Flow

```text
Игрок (Input: Клик, Перетаскивание рамки, Горячие клавиши)
      ¦
      Ў
MapInputHandler / UI Buttons
      ¦
      Ў (Формирует пользовательское намерение)
InteractionController (Режим: PlacingMine, PlacingUnits, TargetPicking, Selection)
      ¦
      Ў (Формирует IGameCommand: BuildMine, BuyUnits, AssignWork, AssignHaul, Release)
GameSession.Dispatch(command)
      ¦
      +-- Валидация правил в ColonySimulation
      +-- Атомарное обновление GameState
      ¦
      Ў (Квантование времени: Advance(Time.deltaTime) -> каждые 250мс TickColony)
OnSnapshotChanged / GameSnapshot
      ¦
      +----------------------T----------------------T----------------------¬
      Ў                      Ў                      Ў                      Ў
BuildingVisualsManager  UnitVisualsManager    HaulRouteVisualizer     HudPresenter
(Обновляет здания,      (Двигает префабы,     (Рисует линии          (Обновляет золото,
шкалы руды, подписи)    анимации, груз)       переноски руды)        руду, статус)
```

---

## 5. Tile & Map Architecture

1. **Модель мира (Source of Truth)**:
   - Игровой мир имеет размер 14x14 клеток.
   - Координаты `Cell(x, y)` целочисленные (от 0 до 13).
   - Занятость клеток и коллизии вычисляются строго в чистом C# коде (`ColonySimulation.ValidateMinePlacement`, `ValidateUnitPurchase`).
   - Unity Tilemap НЕ является владельцем данных — это исключительно презентационный слой.
2. **Слой представления (Presentation)**:
   - 1 клетка = 1.0 единица Unity (Grid `cellSize = (1, 1, 0)`).
   - Спрайты зданий (312x312 px) импортируются с `PixelsPerUnit = 104`, благодаря чему они ровно занимают 3x3 клетки сетки.
   - Спрайты существ (32x32 px) импортируются с `PixelsPerUnit = 48`, благодаря чему они пропорционально вписываются в клетку (~0.67 клетки).
   - Камера Orthographic с размером `8.5` отцентрована точно по центру сетки `(7, 7, -10)`.

---

## 6. Architectural Decisions & Rejected Alternatives

1. **Отказ от ECS / DOTS**:
   - *Причина:* Проект представляет собой компактный прототип на десятки/сотни юнитов. Стандартный GameObject + чистый C# Domain даёт 100% производительность (60+ FPS) при минимальной когнитивной нагрузке и простоте отладки.
2. **Отказ от тяжелых DI-фреймворков (Zenject/Extenject)**:
   - *Причина:* Простой и наглядный Composition Root (`GameBootstrap`) связывает все подсистемы при старте сцены, исключая магию рефлексии и скрытые зависимости.
3. **uGUI vs UI Toolkit**:
   - *Выбор:* uGUI с `CanvasScaler` и `TextMeshPro`.
   - *Причина:* uGUI в Unity 6 работает стабильно «из коробки», идеально настраивается программно и через префабы, полностью покрывая потребности стратегии.
4. **Автоматизация через `GameSceneBuilder`**:
   - *Причина:* Вместо ручной правки сцены мышью или хрупкого редактирования YAML, меню `TrollStrategy -> Setup Game Scene` автоматически настраивает сцену, камеру, Canvas, префабы и ScriptableObjects одной кнопкой или через CLI.
