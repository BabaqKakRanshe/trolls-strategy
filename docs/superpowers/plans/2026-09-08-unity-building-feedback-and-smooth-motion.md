# Unity Building Feedback and Smooth Motion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make building selection pale white-yellow, eliminate unit stop-and-go motion, and show a real production progress bar below every current production building.

**Architecture:** Keep fixed 250 ms domain simulation unchanged. Expose canonical world movement speed and fractional production progress through immutable snapshots; presentation interpolates at that speed and renders generated outline/bar sprites without owning gameplay values.

**Tech Stack:** Unity 6000.6, C#, SpriteRenderer, NUnit EditMode tests.

**Spec:** The three numbered requirements in the 2026-09-08 follow-up request.

## Global Constraints

- Economy remains deterministic at fixed 250 ms steps.
- `GameSession` remains the sole owner of mutable state.
- Presentation consumes snapshot values and does not recalculate production.
- Existing uncommitted command-fan and selected-access-point work is preserved.
- The pre-existing Unity package settings modification remains untouched.

---

### Task 1: Canonical smooth unit motion

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Domain/ColonySimulation.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/GameSnapshot.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/GameSession.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Units/UnitView.cs`
- Modify: `unity/TrollStategy/Assets/Game/Tests/EditMode/UnitVisualScaleTests.cs`

**Interfaces:**
- Produces: `UnitSnapshot.MovementSpeed` from the same domain formula used by worker and hauler simulation.
- Consumes: that speed in `UnitView.AdvanceVisual(float)` for frame-rate-independent movement.

- [x] Add a test proving a visual moves halfway through one domain step instead of arriving early and pausing.
- [x] Centralize the movement-speed formula in `ColonySimulation` and expose its result in the snapshot.
- [x] Replace the hardcoded `5.5` presentation speed with the snapshot speed.
- [x] Keep animation choice based on actual remaining visual distance.

### Task 2: White-yellow building selection

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Buildings/BuildingView.cs`
- Modify: `unity/TrollStategy/Assets/Game/Tests/EditMode/BuildingPresentationTests.cs`
- Create: `unity/TrollStategy/Assets/Game/Tests/EditMode/BuildingPresentationTests.cs.meta`

**Interfaces:**
- Produces: an opaque pale white-yellow eight-direction sprite outline for hovered, inspected, and valid target buildings.

- [x] Add a presentation test that activates a selected building and inspects the generated outline color.
- [x] Change the outline color to pale white-yellow while retaining the existing silhouette technique.

### Task 3: Production progress below buildings

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/GameSnapshot.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/GameSession.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Buildings/BuildingView.cs`
- Modify: `unity/TrollStategy/Assets/Game/Editor/Setup/GameSceneBuilder.cs`
- Modify: `unity/TrollStategy/Assets/Game/Tests/EditMode/BuildingPresentationTests.cs`

**Interfaces:**
- Produces: `BuildingSnapshot.ProductionProgress`, a generated dark track, and a left-to-right pale-yellow fill below buildings with production capacity.

- [x] Add a test for fill width, below-building placement, and non-producer visibility.
- [x] Copy canonical fractional production progress from state into snapshots.
- [x] Generate missing solid sprites at runtime so the committed prefab renders without manual asset setup.
- [x] Position the track below the footprint and anchor fill growth from the left edge.
- [x] Keep the scene builder aligned with the runtime-generated bar naming and placement.

### Task 4: Verification

**Files:**
- Modify: this plan only to mark completed steps.

**Interfaces:**
- Produces: compile, Unity test, Phaser test, lint, build, and diff evidence.

- [x] Run all Unity EditMode tests through the open Editor Test Runner and inspect the completion result.
- [x] Run Phaser unit tests, lint, and production build.
- [x] Remove only Unity import noise known to be clean before the run.
- [x] Review the scoped diff and preserve unrelated user changes.

## Self-review

- Spec coverage: building color is Task 2, jerk diagnosis/fix is Task 1, and production bars are Task 3.
- Placeholder scan: no deferred steps or unspecified interfaces remain.
- Type consistency: both simulation and presentation consume the same canonical movement speed; the bar consumes snapshot progress only.
