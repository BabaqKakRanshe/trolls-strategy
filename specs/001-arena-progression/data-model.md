# Data Model: Прогрессия боёв на арене

Пути — от `unity/TrollStategy/Assets/Game/`. «Новое» — поле или тип, которого сейчас нет.

## Состояние кампании (`Runtime/Domain/GameState.cs`)

| Поле | Тип | Новое | Смысл |
|---|---|---|---|
| `ArenaFundPayouts` | int | да | Выплат в фонде на момент `ArenaFundSinceMs`, 0…cap |
| `ArenaFundSinceMs` | int | да | Активное время, с которого копится следующая выплата |
| `HighestMissionLevel` | int | нет | Высший уровень, на котором колония **когда-либо** победила; только растёт; задания считают его |
| `MissionWins[id]` | int | нет | Победы по уровню; `> 0` — уровень пройден |
| `MissionReadyAtMs[id]` | int | нет | Когда уровень отдохнёт; поражение ставит вдвое дольше |
| `Progress.UnlockedMissions` | set | нет | Открытые уровни; поражение на вершине удаляет её отсюда |
| `PendingBattleReward.Draw` | bool | да | Незабранная награда пришла за ничью (подпись в окне награды) |

`Clone()` копирует оба новых целых. `PendingBattleReward.Clone()` копирует `Draw` через `MemberwiseClone`.

### Правила фонда (`Runtime/Domain/ArenaFund.cs`, новый)

```text
Available(state, cfg) = min(cfg.Cap, P + max(0, now − S) / cfg.PeriodMs)
NextInMs(state, cfg)  = Available == Cap ? −1 : S + (Available − P + 1) × PeriodMs − now
Take(state, cfg):        a = Available; требует a > 0
                         S = (a == Cap) ? now : S + (a − P) × PeriodMs
                         P = a − 1
```

Здесь P = `ArenaFundPayouts`, S = `ArenaFundSinceMs`, now = `ActiveTimeMs`. Инвариант: `0 ≤ P ≤ Cap`, `S ≤ now`.

### Исход боя (`BattleApplication.Start`)

| Исход | Уровень пройден? | Фонд | Золото | Трофеи | Победы/открытия | Отдых | Вершина |
|---|---|---|---|---|---|---|---|
| Победа | нет | не трогается | бросок первой награды | `WinGoods` | +1, следующий уровень, существо | обычный | — |
| Победа | да, фонд > 0 | −1 | бросок повтора | `WinGoods` | +1, следующий уровень | обычный | — |
| Победа | да, фонд = 0 | — | 0 | нет | +1, следующий уровень | обычный | — |
| Ничья | нет | — | бросок первой × доля | нет | нет | обычный | — |
| Ничья | да, фонд > 0 | −1 | бросок повтора × доля | нет | нет | обычный | — |
| Ничья | да, фонд = 0 | — | 0 | нет | нет | обычный | — |
| Поражение | любой | — | 0 | нет | нет | × `DefeatRestMultiplier` | закрыта, если уровень — высший открытый и > 1 |

Ставка списывается при ничьей и поражении: `Gold −= min(stake, Gold)`.

Павшие и их снаряжение уничтожаются при любом исходе (как сейчас).

Награда за победу или ничью копится в `PendingBattleReward`, как сейчас.

### Итог боя (`Runtime/Domain/BattleSimulation.cs`)

| Тип/поле | Новое | Смысл |
|---|---|---|
| `BattleReport.DefeatedShare(bool enemies)` | да | Σ `min(урон, здоровье)` / Σ здоровья стороны, 0…1 |
| `BattleRunState.BurnedStake` | да | Сгоревшая ставка (0 при победе) |
| `BattleRunState.ClosedMissionId` | да | Уровень, закрытый поражением, или null |
| `BattleRunState.RestMs` | да | Отдых уровня после боя |

### Оценка (`Runtime/Domain/BattleOdds.cs`, новый)

- `enum OddsGrade { Weaker, Even, Stronger }`.
- `BattleOdds.Compare(players, enemies, margin) → (double Ratio, OddsGrade Grade)` — по формуле из research R7.

## Контент

### `EconomyConfig` — раздел «Арена» (новые поля, значения по умолчанию = начальные числа спеки)

| Поле | По умолчанию | Смысл |
|---|---|---|
| `_arenaFundPeriodSeconds` | 180 | Одна выплата за столько активных секунд |
| `_arenaFundCap` | 3 | Не больше выплат в фонде |
| `_arenaStakePercent` | 30 | Ставка — доля награды уровня |
| `_arenaDefeatRestMultiplier` | 2 | Во сколько раз дольше отдых после поражения |
| `_arenaOddsMargin` | 1.5 | Отношение сил, начиная с которого отряд «сильнее» (и обратное — «слабее») |

Метод `SetArena(...)` нужен тестам.

### `BattleMissionDefinition` (новые поля)

| Поле | Тип | Смысл |
|---|---|---|
| `_formation` | `BattleFormation` | Расстановка уровня (для окна и тестов; клетки уже в `_enemies`) |
| `_milestone` | bool | Уровень-веха |
| `_championHealthPercent` | int | Здоровье чемпиона относительно врагов уровня, 0 — без чемпиона |
| `_biome` | `ArenaBiome` | Окружение уровня; префаб — `_environmentPrefab` |
| `_winGoods` | `ResourceAmount[]` | уже есть (чужая правка) — трофеи уровня |

`BattleEnemyStart` (новые поля):

| Поле | Тип | Смысл |
|---|---|---|
| `Gear` | `string[]` | Id предметов снаряжения врага; неизвестный id → `CreateBoard`/валидация отказывает |
| `Champion` | bool | Чемпион вехи |

Новые методы: `SetProgression(formation, milestone, championHealthPercent)`, `SetBiome(biome)`, `EnemyGear(i)`.

### `Runtime/Content/ArenaBiome.cs` (новый)

```text
enum ArenaBiome { Meadow, Forest, Swamp, Graveyard, MountainPass, Snow }
enum BattleFormation { Wall, ArchersBack, Flanks, Crowd }
```

### `ProgressionDefinition` (новые поля)

| Поле | По умолчанию | Смысл |
|---|---|---|
| `_repeatArenaLevelStep` | 5 | На сколько уровней растёт цель `ReachArenaLevel` повторяемого задания за круг |
| `_repeatArenaLevelCap` | 30 | Выше этой цели не растёт |

## Снимки приложения

### `ArenaOfferSnapshot` (`Runtime/Application/ArenaOffer.cs`, новый)

Что окно арены, карточка Бараков и боты знают об уровне. Целиком в [contracts/session-arena.md](contracts/session-arena.md).

| Поле | Смысл |
|---|---|
| `Mission` | уровень |
| `EnemyHealthPercent`, `EnemyDamageBonus`, `EnemyArmorBonus` | сила врагов относительно базовой, со снаряжением (FR-001) |
| `EnemyGear` | предметы, которые носят враги, без повторов |
| `Odds` (`OddsGrade`), `OddsRatio`, `SquadCount` | оценка лучшего отряда (FR-002) |
| `FirstWin`, `GoldMin`, `GoldMax`, `Trophies`, `UnlockUnit` | награда (FR-003). Повтор при пустом фонде — 0…0 |
| `Stake`, `GoldShort` | ставка и сколько золота не хватает (FR-010) |
| `FundPayouts`, `FundCap`, `FundNextInMs`, `PaysFromFund` | фонд (FR-009); `PaysFromFund` — победа здесь возьмёт выплату |
| `DefeatRestMs`, `ClosesOnDefeat`, `ReopenLevel` | что на кону (FR-014) |
| `Milestone`, `Champion`, `Formation`, `Biome` | особый уровень, расстановка и окружение (FR-005, FR-017) |

### Прочие снимки

- `BattleRewardSnapshot.Draw` (новое) — для подписи «за ничью».
- `GameSession.HighestMissionLevel` (есть).
- `GameSession.NextMilestone` (новое): ближайший уровень-веха выше высшего пройденного или null (FR-016).

## Связи и переходы

```text
            win(level-1)                 defeat at top (>1)
 closed ───────────────► open ─────────────────────────────► closed
                          │ win
                          ▼
                    won (MissionWins>0) ── win/draw/defeat ──► won (стоит отдых)
```

- Уровень 1 никогда не закрывается.
- Закрытый уровень после победы на уровне ниже открывается снова. Счёт его побед сохраняется, поэтому повторная победа на нём — повтор, из фонда.
- `HighestMissionLevel` не уменьшается.
