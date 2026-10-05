# Specification Quality Checklist: Товары, которые нужны самой колонии

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-05
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain — оставлены намеренно: владелец 2026-10-05 решил «пока как есть, записать задачей». Открыто: FR-001 (какие истории и в каком порядке), FR-004 (товары для уровня везут или списывают), FR-006 (на что тратится пир)
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
  - SC-002 «25% стоимости промежуточных товаров» — не определено и без базы → доля проданного на рынке против нынешнего прогона ботов;
  - крайний случай «товар нужен и улучшению, и рецепту» требовал правила, которого нет в требованиях → FR-009 и допущение;
  - FR-008 «свой поток случайных чисел» — деталь реализации → «повторяемо и не сдвигает награды боёв и побочку»;
  - история 2 предрешала доставку носильщиками, хотя это открытый вопрос FR-004 → нейтральная формулировка.
- Итерация 2: все пункты, кроме открытых вопросов, проходят.
- Граф товаров и тупики сняты с `ProductionContentSetup` на 2026-10-05; числа копилки — `docs/economy-balance.md` §10–11.
- Перед `/speckit-plan` — `/speckit-clarify` по трём открытым вопросам.
- Пункт 2 (ферма и бараки) входит сюда частично: ферма — через историю 3; бараки уже закрыты спекой арены.
