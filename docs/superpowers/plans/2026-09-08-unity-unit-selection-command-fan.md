# Unity Unit Selection and Command Fan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore reliable RTS unit selection and the right-click command fan, while making every building-bound order use the exact building point clicked by the player.

**Architecture:** `InteractionController` remains the owner of transient selection and target modes; Unity presentation translates pointer gestures into IDs and world coordinates. Building access points cross the typed command boundary, are validated atomically by the deterministic domain, and are stored with assignments so simulation never guesses a different destination.

**Tech Stack:** Unity 6000.6, C#, Unity Input System, uGUI/TextMeshPro, NUnit EditMode tests.

**Spec:** The three numbered requirements in the 2026-09-08 user request; the previous Phaser fan behavior is documented in `docs/superpowers/plans/2026-09-05-grid-crowd-command-fan.md`.

## Global Constraints

- `GameSession` remains the only owner of mutable campaign state.
- Selection and fan visibility remain transient interaction/presentation state.
- Building access points are validated before mutation and stored in domain assignments.
- Existing keyboard commands remain available.
- The unrelated modified Unity package settings file is preserved untouched.

---

### Task 1: Reliable RTS selection

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/InteractionController.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/SelectionBoxRenderer.cs`

**Interfaces:**
- Consumes: `UnitVisualsManager.Views` and modifier-key state.
- Produces: deterministic nearest-unit click selection, Ctrl additive/toggle selection, additive drag selection, and a visible translucent marquee.

- [x] Add focused controller coverage for replacement, toggle, additive rectangle, and stale IDs.
- [x] Resolve unit clicks from unit views before building colliders so overlapping colliders cannot steal selection.
- [x] Use a screen-space drag threshold and show both fill and outline only after a real drag begins.
- [x] Preserve Ctrl selection semantics for clicks and drag rectangles.

### Task 2: Right-click command fan

**Files:**
- Create: `unity/TrollStategy/Assets/Game/Runtime/UI/CommandFanView.cs`
- Create: `unity/TrollStategy/Assets/Game/Runtime/UI/CommandFanView.cs.meta`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/MapInputHandler.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Bootstrap/GameBootstrap.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/InteractionController.cs`

**Interfaces:**
- Consumes: selected IDs and a right-click screen position from `MapInputHandler`.
- Produces: a clamped three-button hex fan with `Работа`, `Перенос`, and `Свободны` actions.

- [x] Replace right-click clear with fan opening when the mode is neutral and units are selected.
- [x] Generate the fan under the existing Canvas at runtime and position it around the pointer.
- [x] Close the fan on selection changes, command choice, cancellation, and successful order completion.
- [x] Keep W/H/R and the existing target controls as keyboard-accessible equivalents.

### Task 3: Player-chosen building access points

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Domain/Commands.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Domain/Entities.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Domain/ColonySimulation.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/InteractionController.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/SelectionBoxRenderer.cs`
- Modify: `unity/TrollStategy/Assets/Game/Tests/EditMode/ColonySimulationTests.cs`

**Interfaces:**
- Consumes: the world coordinate of the player's click on each target building.
- Produces: `AssignWorkCommand.AccessPoint`, haul source/destination access points, atomic footprint validation, and deterministic travel to those stored points.

- [x] Add failing tests that reject points outside a footprint and prove workers/haulers arrive at the requested coordinates.
- [x] Extend commands and assignments with optional access points while retaining default constructors for keyboard target buttons.
- [x] Pass the exact world click through work and both haul-target steps.
- [x] Make worker and hauler ticks consume stored points, falling back to canonical entrance positions only for legacy/button-issued commands.

### Task 4: Verification

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: the complete interaction slice.
- Produces: documented controls and compile/test evidence.

- [x] Document click, Ctrl-click, drag, Ctrl-drag, right-click fan, and point-on-building targeting.
- [x] Run Unity EditMode tests in batch mode and inspect the XML result.
- [x] Run the Phaser unit suite, lint, and production build as the migration baseline.
- [x] Review the final diff and confirm the pre-existing package settings change remains untouched.

## Self-review

- Spec coverage: normal selection is Task 1, the previous right-click fan is Task 2, and explicit building points are Task 3.
- Placeholder scan: no deferred implementation placeholders remain.
- Type consistency: pointer coordinates enter domain commands as `WorldPosition`; assignments clone the same fields and simulation consumes them.
