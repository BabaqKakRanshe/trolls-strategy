# Contract: GameSession → окно арены, Бараки, боты

Всё, что окно арены, карточка Бараков, экран итога и боты знают об арене, они читают через `GameSession`. Изменить арену можно только командами `StartBattleCommand`, `AcknowledgeBattleCommand` и `ClaimBattleRewardCommand`. В `GameState` никто из них не заглядывает.

## Запросы

| Член `GameSession` | Новое | Возвращает | Требование |
|---|---|---|---|
| `ArenaOfferSnapshot ArenaOffer(BattleMissionDefinition mission)` | да | Предложение уровня (поля ниже); null для null | FR-001…FR-003, FR-009, FR-010, FR-014 |
| `ArenaFundSnapshot ArenaFund` | да | `Payouts`, `Cap`, `NextInMs` (−1 когда полон), `PeriodMs` | FR-009 |
| `BattleMissionDefinition NextMilestone()` | да | Ближайший уровень-веха выше `HighestMissionLevel`; null — все вехи пройдены | FR-016 |
| `int HighestMissionLevel` | нет | Высший уровень, на котором колония когда-либо победила | FR-012 |
| `CommandResult CanEnterMission(string id)` | меняется | + отказ «Не хватает золота на ставку: ещё {0}» | FR-010 |
| `bool IsMissionUnlocked(string id)` | нет | false и для уровня, закрытого поражением | FR-011 |
| `(int Min, int Max) WinGold(mission)` | нет | Диапазон без учёта фонда (как сейчас) | — |
| `BattleRunState ActiveBattle` | меняется | + `BurnedStake`, `ClosedMissionId`, `RestMs` | FR-011, итог боя |

### `ArenaOfferSnapshot`

```text
Mission                   BattleMissionDefinition
Level, Milestone          int, bool
Formation, Biome          BattleFormation, ArenaBiome
EnemyHealthPercent        int    // 100 = как в справочнике
EnemyDamageBonus          int    // плоская прибавка уровня + снаряжение врагов (у ведущего вида)
EnemyArmorBonus           int
EnemyGear                 IReadOnlyList<EquipmentDefinition>  // без повторов, в порядке слотов
Champion                  UnitKind?   // вид чемпиона вехи
Odds                      OddsGrade   // Weaker | Even | Stronger
OddsRatio                 double
SquadCount                int         // сколько бойцов оценено: min(существ, SquadLimit)
FirstWin                  bool
PaysFromFund              bool        // повтор и в фонде есть выплата
GoldMin, GoldMax          int         // с «Славой арены»; повтор при пустом фонде → 0, 0
Trophies                  IReadOnlyList<ResourceStack>  // пусто, если победа их не даст (повтор без фонда)
UnlockUnit                UnitKind?   // только до первой победы
Stake                     int
GoldShort                 int         // max(0, Stake − Gold)
DefeatRestMs              int
ClosesOnDefeat            bool
ReopenLevel               int         // уровень, победа на котором откроет этот снова (Level − 1)
LadderComplete            bool        // HighestMissionLevel == верх лестницы
```

**Инварианты:**
- Одинаковые состояние и уровень дают одинаковое предложение. Окно и боты получают одно и то же.
- `Stake` совпадает с тем, что спишет `Start` при поражении.
- `PaysFromFund` и `GoldMin/GoldMax` совпадают с тем, что заплатит `Start` при победе (до броска).
- `Odds` пересчитывается при новой ревизии сессии. Без новой ревизии отдаётся из кэша.

## Команды

### `StartBattleCommand` (сигнатура без изменений)

Предусловия — `ValidateAvailability`, по порядку:

1. Бой не идёт.
2. Уровень открыт.
3. Время открытия прошло.
4. Отдых прошёл.
5. **Новое:** `Gold ≥ Stake`.

Отладочный доступ пропускает проверки 2–5.

Постусловия — по таблице исходов в [data-model.md](../data-model.md#исход-боя-battleapplicationstart). Всё атомарно на клоне состояния:

- ошибка возвращает `Fail` без изменений;
- успех делает `Progression.Update`, и задание `ReachArenaLevel` засчитывается в том же коммите.

### `ClaimBattleRewardCommand`

Без изменений: золото в казну, трофеи в Бараки или в снаряжение. Флаг `Draw` только для подписи.

## Экран боя (`IBattleScreen`)

```text
void ShowResult(BattleOutcome outcome, int survived, int fallen, int lostItems, BattleCost cost)
BattleCost { int BurnedStake; int ClosedLevel (0 — не закрыт); int ReopenLevel; int RestMs }
```

`BattleSceneController` собирает `BattleCost` из `ActiveBattle`. Экран показывает цену только при поражении и ничьей.
