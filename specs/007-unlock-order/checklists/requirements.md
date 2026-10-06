# Specification Quality Checklist: Порядок открытия зданий

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-05
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain — оставлены намеренно, это решения владельца. Открыто: FR-001 (вариант A, B, C или свой со страницы), FR-008 (задания арены идут за снаряжением или держат свою минуту; в B «Третий уровень арены» уходит с ≈32 на ≈51 мин)
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
  - SC-001 говорил «четыре задания подряд без нового здания», а тест `AfterTheTutorial_ANewBuildingOpensAtLeastEveryThirdLevel` допускает не больше двух → «трёх заданий подряд», и только до последнего здания (хвост цепочки тест не проверяет);
  - FR-009 «около 40%» не проверялся → нижняя граница 15%. Верхняя граница 50% сломала бы C: там «Третий уровень арены» (300) открывает поле (250);
  - в A и B обычные задания стоят перед «Бараками» и «Первым боем». Страница этого не показывает, а тест `TheGameOpensWithGoblinsOnly_AndTheTutorialComesFirst` упадёт → крайний случай, FR-006 и допущение (обучение растягивается до шага про снаряжение).
- Итерация 2: все пункты, кроме двух открытых вопросов, проходят.
- Источники на 2026-10-05:
  - цепочка и награды — `ProgressionContentSetup`;
  - цены и рецепты — `ProductionContentSetup`;
  - уровни открытия видов (3, 5, 7, 9, 11, 13, 15, 18, 21) — `ArenaContentSetup`;
  - тесты цепочки — `ProgressionContentTests`;
  - минуты — модель страницы «Цепочка открытий» (https://claude.ai/artifact/1LCvZ6MGxBUYStVo17DKDD), пересчитанная по длительностям заданий из `Builds/Stats/bots/human.md` (прогон 2026-10-04, 91,8 мин). Пересчёт совпал с пресетами: первый бой 11,1 / 15,4 / 17,1 / 11,1, хоббит 32,5 / 32,5 / 50,7 / 23,8.
- Границы со спекой 005 — FR-011 и допущение «Порядок работ со спекой 005»: эта спека владеет местом заданий в цепочке (а значит, и минутой, на которой игрок доходит до уровней арены 1, 3 и 6). Спека 005 владеет тем, какой уровень или какое задание открывает каждый вид, и целью «не меньше 5 видов» (её SC-001).
- Решения владельца от 2026-10-05 (тролль за первый бой, его новые числа, 1-й уровень из трёх гоблинов, шаг про снаряжение, все 30 уровней в окне арены, дробный груз) делает другая сессия. Спека считает их исходным состоянием, и точку отсчёта для SC-003…SC-005 меряют после их коммита.
- Перед `/speckit-plan` — `/speckit-clarify` по двум открытым вопросам.
