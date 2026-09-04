# Project Architecture Rules

These rules apply to every change in this repository.

## Scope before structure

- Confirm that a feature belongs to the current playable scope before adding runtime code for it.
- Prefer the smallest complete vertical slice over speculative frameworks or parallel implementations.
- For build, module-boundary, persistence, or state-ownership changes, perform an impact analysis and design the complete change before editing.

## Ownership and dependency direction

- `GameSession` is the single owner of mutable campaign state.
- Domain rules live in `src/domain` and must remain deterministic and independent of Phaser, DOM, storage, and wall-clock APIs.
- `src/application` coordinates domain commands and exposes immutable snapshots; it must not import browser adapters.
- `src/persistence` owns save validation and storage coordination. Browser APIs stay behind `SavePort` adapters.
- `src/game` renders geometry only: white background, red market, blue warehouse, purchased mine, and creatures. Do not introduce sprites, textures, or audio unless the user explicitly changes this constraint.
- `src/ui` contains the counters and native shop buttons. It reads immutable snapshots and submits typed commands; it never mutates domain state or recomputes gameplay outcomes.
- `src/main.ts` is only the composition root. Do not move business rules into it.
- Dependencies flow inward: presentation/adapters → application → domain. Content definitions may be consumed by all inner gameplay layers but must not depend on presentation.

## One source of truth

- Each game fact has one canonical owner. Put unit/item/building values in `src/content/catalog.ts` and mission values in `src/content/missionCatalog.ts`.
- Do not duplicate rules, identifiers, timings, limits, reward values, or save schemas across UI and simulation.
- Do not keep two implementations of the same system during a migration. Use a strangler transition and remove the superseded path in the same completed change.
- Documentation must distinguish current facts, accepted decisions, and future intentions.

## Commands, snapshots, and transactions

- All state changes enter through the typed `GameCommandContractMap` and `GameSession.dispatch`.
- Validate an entire command before mutation and commit it atomically.
- Rich Phaser scenes, if reintroduced, receive a narrow runtime facade and immutable snapshots. Scene-local state may control presentation only.
- Mission start accepts only a formation. `GameSession` derives and stores the canonical run ID, seed, combat input, and combat report.
- Mission resolve/abort accepts only a run ID and settles the stored report exactly once. Leaving playback must never cancel casualties or rewards.

## Determinism and time

- Economy advances in fixed 250 ms simulation steps; do not couple domain outcomes to render frame rate.
- Combat is a pure seeded simulation. Playback consumes recorded frames and never recalculates damage, targeting, deaths, or rewards.
- Random streams and ID/seed derivation are explicit, serializable, and covered by repeatability tests.
- Test-only clock acceleration belongs at the composition boundary and must feed normal simulation steps without changing production behavior.

## Persistence

- Save state must remain serializable and versioned.
- Decode through the single schema-aware codec; reject structural corruption, dangling references, forged mission identity, invalid ownership, and unknown future versions.
- Repair only explicitly safe economy counters. Never silently repair invalid assignments, formations, equipment ownership, combat reports, or haul state.
- Active missions restore the same canonical input/report and continue without generating a second run.
- Autosave follows successful commands and foreground simulation intervals; rejected commands never save.

## Presentation and accessibility

- Phaser renders the world; accessible native DOM controls expose every required gameplay action.
- Canvas-only interaction must have a keyboard-accessible DOM equivalent.
- Preserve focus and scroll across HUD updates; avoid rebuilding unchanged DOM on simulation ticks.
- Scene transitions must explicitly stop the outgoing scene before starting the incoming scene.
- Missing optional audio or visual assets degrade safely and never alter domain state or stop gameplay.
- Use the curated runtime manifest only. Keep license evidence and the commercial-release gate current.

## Testing and definition of done

- Test behavior at the narrowest correct boundary: pure domain rules with unit tests, adapters with contract tests, and the player journey with Playwright.
- Do not make tests depend on private implementation details when an observable command/result contract exists.
- Architecture-significant work is done only when focused tests, the full unit suite, lint, production build, and relevant browser E2E all pass.
- Stop and redesign if fixes reveal errors one by one, ownership becomes ambiguous, a second source of truth appears, or a presentation module begins owning gameplay decisions.
