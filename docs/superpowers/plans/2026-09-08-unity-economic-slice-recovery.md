# Unity Economic Slice Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the Unity 6 colony economy slice to behavioral parity with the verified Phaser implementation and make it compile, test, run, and build reliably.

**Architecture:** `GameSession` remains the only owner of mutable campaign state and commits cloned command transactions atomically. Domain simulation stays deterministic at fixed 250 ms steps; Unity presentation consumes snapshots and one input component owns each physical gesture. ScriptableObject assets provide the Unity content adapter, while committed scene and build settings are regenerated only after code and tests compile.

**Tech Stack:** Unity 6000.6.0f1, C#, Unity Input System, uGUI/TextMeshPro, Unity Test Framework 1.8, URP 2D.

**Spec:** `docs/superpowers/specs/2026-09-05-troll-strategy-vertical-slice-v2.md`

## Global Constraints

- Preserve the existing uncommitted Unity UI and scene work; incorporate it rather than resetting it.
- Economy advances only in fixed 0.25 second steps.
- Canonical values are mine 200/3x3/100 ore/5 workers, goblin 40/3/5/10, troll 170/9/2/30, warehouse 500, ore sale price 3.
- `GameSession` is the sole owner of mutable state; rejected commands commit no mutation.
- Runtime input uses only `UnityEngine.InputSystem` and handles each click/key once.
- No combat, missions, persistence, audio, or speculative buildings are added in this slice.
- Completion requires Unity compilation, EditMode tests, the correct build scene, and a player build smoke check.

---

### Task 1: Restore compilable UI contracts

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/UI/CommandDockView.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/UI/HudPresenter.cs`

**Interfaces:**
- Consumes: `GameSnapshot.Buildings : IReadOnlyList<BuildingSnapshot>` and `InteractionController.GetTargetBuildingIds()`.
- Produces: target buttons that are rebuilt only when the interaction target set changes.

- [ ] Replace the invalid `IReadOnlyList.Find` call with an indexed lookup.
- [ ] Cache a target signature so 250 ms snapshot refreshes do not destroy and recreate unchanged buttons.
- [ ] Compile scripts through Unity MCP and confirm zero C# errors before proceeding.

### Task 2: Make commands atomic and restore economy parity

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Application/GameSession.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Domain/ColonySimulation.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Domain/GameState.cs`

**Interfaces:**
- Consumes: `IGameCommand`, `GameContentCatalog`, fixed `EconomyStepSeconds`.
- Produces: `GameSession.Dispatch(IGameCommand)`, which applies to a clone and commits only on success.

- [ ] Add command-contract tests for duplicate IDs, missing IDs, mine worker capacity, rejected-command immutability, full warehouses, and returned cargo.
- [ ] Make initial gold come from `EconomyConfig.StartingGold`.
- [ ] Resolve and validate every unit ID before command mutation; reject empty, duplicate, or unknown IDs.
- [ ] Load no more than source ore, cargo capacity, and destination room.
- [ ] Keep undelivered cargo in the unloading phase and return cancelled cargo to its route source.
- [ ] Clamp negative elapsed time and preserve fixed-step advancement.
- [ ] Run the focused EditMode tests.

### Task 3: Establish canonical Unity content values

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Building_Mine.asset`
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Building_Warehouse.asset`
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Building_Market.asset`
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Unit_Goblin.asset`
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Unit_Troll.asset`
- Modify: `unity/TrollStategy/Assets/Game/Editor/Tests/ColonyGameplayVerification.cs`

**Interfaces:**
- Consumes: values in the vertical-slice spec and Phaser `src/content/catalog.ts`.
- Produces: one runtime catalog with matching prices, capacities, footprint, strength, speed, and cargo.

- [ ] Correct the five serialized definition assets.
- [ ] Update or replace assertions that currently certify the wrong Unity-only values.
- [ ] Verify the scene catalog resolves exactly one definition for every runtime kind.

### Task 4: Consolidate player input

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/MapInputHandler.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/PlacementPreviewRenderer.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/HaulRouteVisualizer.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/SelectionBoxRenderer.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Units/UnitView.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Buildings/BuildingView.cs`

**Interfaces:**
- Consumes: mouse and keyboard state from `UnityEngine.InputSystem`.
- Produces: one call per gesture into `InteractionController`.

- [ ] Keep `G` handling only in `MapInputHandler`.
- [ ] Keep right-click cancellation only in `MapInputHandler`.
- [ ] Route world clicks only through `SelectionBoxRenderer`; views expose identity but do not independently dispatch pointer callbacks.
- [ ] Use Ctrl, matching the documented additive-selection control.
- [ ] Add PlayMode coverage for single-click selection and single `G` toggling where practical.

### Task 5: Add a real Unity test assembly

**Files:**
- Create: `unity/TrollStategy/Assets/Game/Tests/EditMode/TrollStrategy.EditModeTests.asmdef`
- Create: `unity/TrollStategy/Assets/Game/Tests/EditMode/ColonySimulationTests.cs`

**Interfaces:**
- Consumes: public runtime command/session contracts and test-created ScriptableObject definitions.
- Produces: NUnit tests discoverable by `-runTests -testPlatform EditMode`.

- [ ] Create an Editor-only TestAssemblies asmdef referencing `TrollStrategy.Runtime`.
- [ ] Build an in-memory catalog matching Phaser values.
- [ ] Cover the full mine → warehouse → market path and all Task 2 edge cases.
- [ ] Run tests through Unity MCP and export the result summary.

### Task 6: Regenerate and validate the playable scene

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Editor/Setup/GameSceneBuilder.cs`
- Modify: `unity/TrollStategy/Assets/Game/Scenes/MainColonyScene.unity`
- Modify: `unity/TrollStategy/ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: compiled runtime components, canonical content assets, and existing prefabs.
- Produces: `MainColonyScene` with non-null bindings and the only enabled build-scene entry.

- [ ] Back up the current generated scene outside the repository before regeneration.
- [ ] Execute `TrollStrategy/Setup Game Scene` once through Unity MCP.
- [ ] Inspect `GameBootstrap`, HUD, shop, command dock, world view, prefabs, EventSystem, and camera bindings.
- [ ] Enter Play Mode, complete build-mine and hire-unit smoke actions, and confirm no console errors.

### Task 7: Verification gate

**Files:**
- Modify: `README.md` only if Unity run/test commands are absent or inaccurate.

**Interfaces:**
- Consumes: the completed economic slice.
- Produces: reproducible compile, test, play, and build evidence.

- [ ] Run all Unity EditMode tests.
- [ ] Run the Phaser unit, lint, build, and Playwright suites as the parity baseline.
- [ ] Build the Unity Windows player to a temporary output directory outside the repository.
- [ ] Confirm the player contains `MainColonyScene`, starts without exceptions, and preserves the expected initial snapshot.
- [ ] Review `git diff` for unrelated or generated noise and report any retained pre-existing changes separately.

## Self-review

- Spec coverage: the plan covers every behavior in the current colony economy slice except the Phaser-only debug collision panel and command fan, which require a separate presentation plan after the core is stable.
- Placeholder scan: no deferred implementation placeholders are present.
- Type consistency: all tasks use existing `IGameCommand`, `GameSession`, `GameSnapshot`, and `InteractionController` contracts; new tests consume only public runtime APIs.
