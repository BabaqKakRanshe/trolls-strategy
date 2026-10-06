# Data Model: Склад экипировки показывает снаряжение

Пути — от `unity/TrollStategy/Assets/Game/`.

## Состояние и контент: без изменений

- `GameState.Equipment` (`EquipmentState { Id, DefinitionId, OwnerUnitId }`) остаётся единственным источником снаряжения колонии. Новых полей, команд и событий нет.
- `BuildingDefinition` склада и склада экипировки не меняются: `StorageRole`, вместимость, `Stores`, `SlotStackSize` прежние.
- `EquipmentDefinition` уже несёт всё, что нужно сетке: `ItemId`, `DisplayName`, `Slot`, `DamageBonus`, `ArmorBonus`, `Icon`, `Enchanted`.
- Исключение — история 5 (по ответу на FR-017): меняется только `_displayName` в `Content/Definitions/Building_Armory.asset` и тексты задания `armory-build` в `Progression.asset`.

## Снимок: без изменений

- `GameSnapshot.Equipment`: `EquipmentSnapshot { Id, DefinitionId, DisplayName, Slot, DamageBonus, ArmorBonus, OwnerUnitId }`.
- `GameSnapshot.Units`: имена бойцов для подсказки «Носят».
- `BuildingSnapshot.Slots`, `SlotStackSize`, `Capacity`: ячейки склада, как сейчас.

## Вид на экране (только UI, `Runtime/UI/Colony/GearStock.cs`)

### GearCell — ячейка вида

| Поле | Откуда | Правило |
|---|---|---|
| `DefinitionId` | `EquipmentSnapshot.DefinitionId` | ключ ячейки |
| `Name` | `EquipmentDefinition.DisplayName` | заголовок подсказки |
| `Icon` | `EquipmentDefinition.Icon` | null → ячейка без картинки, число остаётся |
| `Slot` | `EquipmentDefinition.Slot` | порядок: Weapon, Armor, Helmet |
| `Enchanted` | `EquipmentDefinition.Enchanted` | класс `is-enchanted`; простой и зачарованный никогда не в одной ячейке |
| `DamageBonus`, `ArmorBonus` | `EquipmentDefinition` | «урон +N», «броня +N» в подсказке; нулевые не пишутся |
| `Count` | число предметов вида в своей сетке | в ячейке «N», больше 999 — «999+» |
| `Owners` | имена из `GameSnapshot.Units` по `OwnerUnitId` | только в сетке «На бойцах»; владелец не найден — не называется, но считается |

### GearStock — две сетки

- **Запас**: предметы с `OwnerUnitId == null`, сгруппированные в `GearCell`.
- **На бойцах**: предметы с `OwnerUnitId != null`, так же.
- **Порядок ячеек** (обе сетки): `Slot`, затем простые раньше зачарованных, затем индекс вида в `catalog.Equipment`.
- **Инвариант**: `InStock + OnFighters == snapshot.Equipment.Count` — то же число, что считает цель `OwnEquipment`.
- **Пустые состояния**:
  - запас пуст и надетого нет → строка «Запас пуст: проложите сюда перенос из мастерской, где делают снаряжение.»;
  - запас пуст, а надетое есть → «Запас пуст: всё снаряжение на бойцах.»;
  - надетого нет → заголовка и сетки «На бойцах» нет.

### Назначение хранилища (`GameSession`, слова приложения)

- `DescribeRole(BuildingDefinition) → string` — короткая фраза, ячейка таблицы книги.
- `ExplainStorage(BuildingDefinition) → IReadOnlyList<string>` — целые предложения для подписи карточки, подсказки каталога, окна награды и страницы книги. Для зданий без `StorageRole` — пусто.
- Имена зданий в предложениях — из данных: склад экипировки — первое здание с `StorageRole.Armory`, склад — первый `Stockpile`, который хранит не только снаряжение. Нет такого здания — предложение пропускается.

Тексты — в [contracts/armory-ui.md](contracts/armory-ui.md).
