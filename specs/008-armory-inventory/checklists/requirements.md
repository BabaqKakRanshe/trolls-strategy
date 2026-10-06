# Specification Quality Checklist: Склад экипировки показывает снаряжение

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-05
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain — открыт один вопрос владельцу: FR-017 (название: оставить, «Оружейная», «Арсенал» или переименовать оба склада). Истории 1–4 от ответа не зависят, от него зависит только история 5
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
  - в Independent Test истории 1 было «у каждой число 3», а колония начинает с ржавым мечом и латаной бронёй → у этих двух видов 4;
  - требование «подсказка кнопки „Снести“ говорит, что снаряжение не пропадёт» убрано: у кнопок карточки нет подсказки, только вторая строка с ценой сноса; поведение осталось в крайнем случае «Снос склада экипировки», FR перенумерованы;
  - «задание считает всё снаряжение» сверено с правилом цели (`OwnEquipment` считает все предметы колонии, надетые тоже) → FR-006 и история 3, сценарий 4.
- Итерация 2: все пункты, кроме открытого вопроса, проходят.
- Исходное состояние снято с кода на 2026-10-05: `Building_Warehouse.asset`, `Building_Armory.asset`, `ColonySimulation.Accepts`/`Unload`/`StoreTrophies`, `InspectPanel.RenderBuilding`/`RenderSlots`, `GameSession.DescribeRole`, `WikiPanel.Build` (только `Constructible`), `Equipment_*.asset` (30 видов, стартовые ржавый меч и латаная броня).
- Книга со страницами склада и рынка меняет решение другой сессии: её незакоммиченный тест `Book_OpensOnTheEntry_FromAHintAndFromTheInspectCard` проверяет, что у рынка страницы нет. Согласовать до реализации (tasks T001).
- Можно идти в `/speckit-plan` без `/speckit-clarify`: открытый вопрос касается только P3-истории; план держит переименование отдельной фазой.
