# Specification Quality Checklist: Указатель обучения

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-05
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — FR-001 и FR-013 закрыты решением владельца 2026-10-06 (раздел Clarifications спеки)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Проверка 2026-10-05, итерация 1:
  - SC-003 «выше хотя бы на 20%» читалось двояко (пункты или доля) → «не меньше 90% или в 1,2 раза выше, чем до фичи»;
  - FR-012 «мигает только цель указателя или ничего» допускал два поведения → «пока указатель показан, мигание выключено»;
  - сценарий 7 истории 2 (смесь 3) не говорил, что делает «Вывозить на склад» без свободного гоблина → кнопка недоступна, подсказка говорит почему; что кнопка остаётся после обучения — в допущениях.
- Итерация 2: все пункты, кроме открытых вопросов, проходят.
- Три истории — один механизм: указатель обучения (FR-002 — FR-008). Истории 2 и 3 — его применение к переносу и постановке; смеси из таблицы «Варианты» меняют вид указателя, а не требования.
- Сведения о коде сняты 2026-10-05: подсветка цели — `QuestFocus` и класс `is-suggested`, мигание — `ColonyHudView.Tick` (`hud-pulse`, 0,55 с); `QuestFocus.HaulTo` считается и нигде не используется; на шаге «Куда носить» `ContextBar` не показывает целей (`MarkQuestTargets` знает только «Куда на работу» и «Откуда носить»); постановка — `PlacementPreviewRenderer` (только клетка под курсором); перенос — `InteractionController` (`ChoosingHaulSource` → `ChoosingHaulCargo` → `ChoosingHaulDestination` → `AssignHaulCommand`); цепочка — `Editor/Setup/ProgressionContentSetup.cs`.
- Прожектор — исключение из правила UI «воздух» (AGENTS.md), записано в допущениях; при выборе смеси с прожектором правило в AGENTS.md дополняется.
- Итерация 3 (та же дата): цепочка обучения сверена с рабочим деревом после правки параллельной сессии (не закоммичена) — 9 шагов: «Первый бой» только гоблинами открывает тролля и склад экипировки, затем «Склад экипировки» (перенос бараки → склад экипировки) и «В броне на арену» (2 предмета в бою). Добавлены: сценарий 6 истории 2 (указатель на маршрутах «Первой выручки» и «Склада экипировки»), крайние случаи экрана расстановки и пустых бараков; FR-013 включает экран расстановки, открытым остаётся только «после обучения».
- Итерация 4 (2026-10-06): владелец выбрал смесь 1 + 3. Clarifications записаны в спеку; FR-001, FR-004, FR-005, FR-007, FR-013, FR-015, FR-017 переписаны под выбранную смесь, добавлен FR-018 (когда показывается «Вывозить на склад»); требования смесей 2 (демонстрация) и 4–6 сняты; истории 2 и 3 описывают короткие пути «За работу!» и первого гоблина и перенос двумя зданиями в «Первой выручке». Все пункты проходят.
- Дальше — `plan.md`, `research.md`, `data-model.md`, `tasks.md` в этой папке.
