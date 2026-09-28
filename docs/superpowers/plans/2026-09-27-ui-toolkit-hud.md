# Переделка UI на UI Toolkit

Дата: 27.09.2026, дополнено 28.09.2026. Основание: аудит UI от 26.09 (весь HUD собирался кодом в пяти местах, мёртвые view, неверные цены, клавиатурные действия без кнопок); 28.09 — требование видеть структуру UI в Hierarchy и убрать разнобой uGUI / UI Toolkit / TMP.

## Решение

Весь UI игры — UI Toolkit (UXML + USS): экранный HUD колонии и боя и подписи над объектами в мире. uGUI и TextMeshPro в коде игры не используются; `com.unity.ugui` остаётся ради `EventSystem` (ввод UI Toolkit и наведение на здания).

- Вёрстка и стили — `Assets/Game/UI/` (`Uxml/`, `Styles/`, `Fonts/`, `Settings/`, `Prefabs/`). Один `Theme.uss` задаёт цвета, шрифт, кнопки и панели; экраны и подписи в мире добавляют только раскладку и размеры.
- Шрифт — Rubik 1.x (OFL, кириллица), лицензия лежит рядом со шрифтом.
- **Экранный UI — префаб `UI.prefab`, дерево вложенных `UIDocument`.** Каждый экран, полоса и панель — свой GameObject со своим `UIDocument`; Unity вкладывает корень дочернего документа в корень родительского (`parentUI`), так что это одно дерево элементов в одной панели. Панели имеют свой UXML (`Uxml/Colony/*`, `Uxml/Battle/*`); полосы и колонки UXML не имеют и получают классы раскладки (`.middle`, `.left-column`, `.layer`…) из компонента `UiDocumentClasses`. Порядок соседей — `sortingOrder`.
- Дерево описано в `UiSetup`; `TrollStrategy/Setup UI` пересобирает префаб, ставит его в сцену и подключает к `GameBootstrap`. `UIDocument` добавляется после того, как у GameObject есть родитель: родительский документ запоминается в момент добавления компонента.
- Код экранов — обычные классы поверх корня своего документа (`Runtime/UI/Colony`, `Runtime/UI/Battle`); `ColonyHud` и `BattleHud` только собирают корни частей и связывают документы, сессию и ввод. EditMode-тесты собирают то же дерево из префаба (`TestUi`).
- **Бой.** Расстановка (кто где стоит, кто выбран, кто что надел) — `BattleDeployment` в слое Application. `BattleSceneController` держит арену, камеру и часы повтора и говорит с экраном только через `IBattleScreen`; реализацию (`BattleHud`) отдаёт `GameBootstrap`.
- **Подписи в мире** — `WorldPanel`: world-space `UIDocument` с элементами, собранными в коде, на `WorldPanelSettings` (100 px на единицу мира, тема `WorldTheme.tss`, без коллайдеров). Масштаб объекта 1; размер шрифта в USS = прежний размер TMP ×10. Порядок относительно спрайтов задаёт `sortingOrder` рендерера (подписи 25, HP 33, выручка 43, урон 60, тост 80).
- Клики по карте и полю боя блокирует `UIInputUtils`: он спрашивает зарегистрированные документы через `Pick`; корни документов и контейнеры раскладки не ловят указатель.
- Отзывчивость кнопок — `UiFeel`/`UiMotion`/`CounterLabel`; недоступное действие отвечает отказом, а не молчанием.

## Структура в Hierarchy

```
UI                      корень экрана (.ui-screen)
├─ ColonyHud            .layer, ColonyHud
│  ├─ Frame             .hud-root
│  │  ├─ TopBar
│  │  ├─ Middle         .middle
│  │  │  ├─ LeftColumn  .left-column — Quest, Inspect
│  │  │  └─ RightColumn .right-column — Showcase, Catalog
│  │  └─ Bottom         .bottom — Status, ContextBar
│  └─ CommandFan, HaulCargo, Reward, BattleReward, Cheat, Tooltip   .layer
└─ BattleHud            .layer .battle-screen (скрыт до боя), BattleHud
   ├─ Frame             .battle-frame
   │  ├─ Header
   │  └─ Bottom — Deployment (Roster, Selected, Actions), Replay
   └─ Banner            .layer
```

Подписи в мире: `Label` в `BuildingBase.prefab`, `CargoLabel` в `UnitBase.prefab`; тосты, выручка, цифры урона и полоски HP создаются в коде как объекты `WorldToast`, `Amount`, `DamageNumber`, `HpBar`.

## Этапы

1. HUD колонии на UI Toolkit, удаление старых view и uGUI-канваса колонии.
2. HUD колонии — дерево вложенных документов в префабе `UI`.
3. HUD боя на UI Toolkit, `BattleDeployment`, `IBattleScreen`.
4. Подписи в мире на world-space UI Toolkit.
5. Уборка: `ButtonFeel`, `Juice.Flash(Graphic)`, uGUI-ветка `UIInputUtils`, TMP в asmdef, шрифт `Arial Dynamic`, мёртвый превью-HUD в `MineModelBuilder`.

Готово, когда проходят компиляция, EditMode-тесты, проверка в Play Mode и сборка Windows player.

## Состояние на 28.09.2026

Все этапы выполнены.

- EditMode: 182 из 183. Новые тесты: 8 в `BattleHudTests`, проверка вложенного дерева в `SavedScene_UsesTheUiToolkitHudWiredToTheBootstrap`. Единственное падение — `SavedScene_StartingBuildingsFormValidLayoutOnGrid` (склад в сцене смещён от сетки на 0.42, к UI не относится). Тесты запускаются меню `TrollStrategy/Tools/Run EditMode Tests`, итог — `Builds/Tests/editmode-summary.txt`.
- Play Mode 1920×1080: HUD колонии в прежней раскладке; клик по карте проходит, над панелями блокируется. Бой: расстановка внизу, повтор со скоростями, итог и возврат в колонию; HUD колонии возвращается, HUD боя скрывается. Подписи зданий, тост отказа, «+золото» при продаже, полоски HP и цифры урона видны поверх спрайтов.
- Windows player (`TrollStrategy/Build Windows Player`) собран: 0 ошибок.

Открыто:

- `Setup UI` пересоздаёт объекты префаба, поэтому их fileID меняются при каждой пересборке: правки структуры — только в `UiSetup`, ссылки на части префаба держат `ColonyHud`/`BattleHud`, которые установщик переподключает.
- Число груза у носильщика в Play Mode не проверено глазами (нужен носильщик в пути); префаб и код проверены тестом `Hauler_ShowsCargoSpriteAndHidesItAfterUnloading`.
- Собранный player не запускался для визуальной проверки после этапов 2–5.
- Портреты существ — кадры анимации 32×32 с большим полем, в карточках каталога они мелкие; нужен отдельный портрет.
- Резервный шрифт: `GameTextSettings` берёт недостающие в Rubik символы (стрелки) из `LiberationSans Fallback.asset`; это динамический атлас, он меняется после игры.
