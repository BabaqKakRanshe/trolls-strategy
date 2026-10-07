<!--
Sync Impact Report
- Version change: 1.0.0 → 1.0.1 (PATCH: pointers only)
- Modified: Platform and Presentation Constraints, Governance. The detailed runtime guidance moved
  from AGENTS.md to CODING_STANDARDS.md; AGENTS.md now routes to it and to the topic docs.
- Principles and sections: unchanged
- Source of 1.0.0: AGENTS.md (2026-10-02). This file holds the non-negotiable principles that every
  spec and plan is checked against.
- Deferred TODOs: none
-->
# TrollStrategy Constitution

## Core Principles

### I. Single Owner of Game State

- `GameSession` owns all mutable campaign state. Gameplay changes MUST enter through validated
  commands and commit atomically.
- Selection and target modes are transient application state and MUST NOT own units, buildings,
  gold or assignments.
- A feature that introduces a second source of truth for any gameplay value MUST be redesigned
  before implementation.

Rationale: one owner keeps the campaign consistent, testable and saveable.

### II. Layered Architecture

- `Runtime/Domain` holds deterministic game rules with no Unity presentation or wall-clock
  dependencies.
- `Runtime/Application` coordinates commands and exposes snapshots; presentation never decides
  gameplay outcomes.
- `Runtime/Presentation` renders state; `Runtime/UI` reads snapshots and submits commands.
- `Runtime/Bootstrap` composes the scene and services and holds no gameplay rules.
- Grid coordinates, capacity, footprints and placement validity are domain rules; previews and
  purchase commands MUST use the same validation.

Rationale: rules testable without a scene; UI changes cannot break the economy.

### III. Deterministic Simulation

- The economy advances in fixed 250 ms simulation steps; domain results MUST NOT depend on frame
  rate.
- Randomness and derived IDs MUST be explicit, seeded and repeatable. A new random system gets its
  own stream so it does not shift existing ones (e.g. battle rewards).
- Save data, when added, MUST be serializable and versioned; invalid references and ownership are
  rejected, never silently repaired.

Rationale: reproducible runs make bots, tests and bug reports meaningful.

### IV. Content Owns the Numbers

- `Runtime/Content` and its ScriptableObjects own every gameplay value. UI, simulation and bots
  MUST NOT duplicate them.
- Balance changes MUST be checked with the campaign bots (`docs/campaign-bots.md`) against the
  campaign length target and documented in `docs/economy-balance.md`.

Rationale: one place to tune; balance claims backed by runs, not feel.

### V. Bots Play by Player Rules

- `Assets/Game/Bots` plays only through `GameSession` snapshots and commands. Bots MUST NOT read or
  write `GameState` and MUST NOT get a rule of their own.
- Every new player action or quest goal MUST be reachable by the bots in the same feature.

Rationale: what a player cannot do, a bot cannot do — so bot results describe real play.

### VI. Smallest Complete Vertical Slice

- A feature MUST belong to the current playable scope before runtime code is added.
- Prefer the smallest complete vertical slice (rule + content + UI + test) over speculative
  frameworks. Complexity beyond that MUST be justified in the plan.
- Required player actions MUST stay available through the Unity UI and input flow.

Rationale: the game is in alpha; playable increments beat unfinished systems.

### VII. Verified at the Narrowest Boundary

- Behaviour is tested at the narrowest correct boundary with Unity EditMode tests, plus scene or
  player checks where presentation matters.
- Architecture-significant work is done only when focused and full relevant tests, compilation and
  the applicable player build (`TrollStrategy/Build Windows Player` → `Builds/Windows`) pass.
- If fixes reveal errors one by one or ownership becomes ambiguous, work MUST stop and be
  redesigned.

Rationale: green tests and a working build are the definition of done.

## Platform and Presentation Constraints

- Engine: Unity, project at `unity/TrollStategy`; targets Windows and WebGL (itch.io).
- All UI is UI Toolkit (no uGUI, no TextMeshPro), following the "air" look, the `UI` prefab
  structure built by `UiSetup`, and `Theme.uss` as the only home of colours, fonts and button
  styles. Detailed UI rules live in `CODING_STANDARDS.md` § UI code, `docs/ui-style.md` and
  `docs/ui-toolkit.md` and are binding.
- Every button is bound with `UiFeel.Bind`; unavailable ones use `UiFeel.SetAvailable`.
- Art follows `docs/art-asset-pipeline.md`; audio follows `docs/audio-direction.md` (generated
  cues in F major pentatonic, `Soundscape` owns music).
- Third-party art and audio keep licence evidence (`assets/licenses.json`) and the
  commercial-release gate. The repository is public: sources whose licence forbids
  redistribution MUST NOT be committed.
- Play telemetry changes MUST keep `CampaignTelemetry.Schema` and `docs/analytics.md` in step.

## Development Workflow

- Small fixes (hours) go straight to implementation. Features marked L/XL in the backlog
  (`docs/backlog/`) go through Spec Kit: specify → clarify → plan → tasks → implement.
- Build, persistence or state-ownership changes start with a dependency impact analysis in the
  plan; the full design is written before code, and implemented in one pass.
- Several agents may drive the same Unity editor: code changes happen outside Play Mode and
  peers are notified before domain reloads.
- Every plan includes a Constitution Check against Principles I–VII.

## Governance

- This constitution supersedes conflicting practice. `CODING_STANDARDS.md` is the detailed
  runtime guidance (`AGENTS.md` routes to it); when the two disagree, update both in the same change.
- Amendments: edit via `/speckit-constitution`, record the reason, bump the version.
- Versioning: MAJOR — a principle removed or redefined; MINOR — a principle or section added or
  materially expanded; PATCH — wording and clarifications.
- Compliance: every plan's Constitution Check and every review verifies the principles;
  deviations are listed with justification in the plan's Complexity Tracking.

**Version**: 1.0.1 | **Ratified**: 2026-10-02 | **Last Amended**: 2026-10-07
