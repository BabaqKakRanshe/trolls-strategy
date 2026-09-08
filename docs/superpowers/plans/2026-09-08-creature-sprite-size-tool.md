# Creature Sprite Size Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Unity editor tool for tuning each existing creature's world-sprite scale without changing selection, collision, or simulation behavior.

**Architecture:** `UnitDefinition` remains the canonical owner of a creature's visual scale. Runtime presentation reads that value for the dedicated sprite child and the placement ghost; an editor-only window edits the catalog definitions with Undo support and refreshes live play-mode views.

**Tech Stack:** Unity 6, C#, ScriptableObject content, IMGUI EditorWindow, NUnit EditMode tests

**Spec:** User request in the 2026-09-08 task: tune only creature sprite size.

## Global Constraints

- `GameSession` remains the sole owner of mutable campaign state.
- Visual scale must not affect colliders, selection visuals, grid capacity, movement, or persistence.
- Existing uncommitted Unity work must be preserved.
- The runtime content catalog remains the single source of truth for creature definitions.

---

### Task 1: Canonical sprite scale and runtime projection

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Content/UnitDefinition.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Units/UnitView.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Presentation/Visuals/PlacementPreviewRenderer.cs`
- Modify: `unity/TrollStategy/Assets/Game/Editor/Setup/GameSceneBuilder.cs`
- Test: `unity/TrollStategy/Assets/Game/Tests/EditMode/UnitVisualScaleTests.cs`

**Interfaces:**
- Produces: `UnitDefinition.SpriteScale`, `UnitView.ApplySpriteScale()`
- Consumes: existing `UnitDefinition` lookup through `GameContentCatalog`

- [ ] **Step 1: Write the failing EditMode test**

Create a definition with scale `1.75`, set up a `UnitView` whose renderer initially sits on the root, and assert that setup creates/uses `SpriteVisual`, scales it to `1.75`, and leaves both the root scale and collider radius unchanged.

- [ ] **Step 2: Run the focused EditMode test**

Run Unity in batch mode with `-runTests -testPlatform EditMode -testFilter TrollStrategy.Tests.UnitVisualScaleTests` and expect the new assertions to fail before implementation.

- [ ] **Step 3: Implement the visual-scale projection**

Add a positive serialized scale with a safe `1` fallback, isolate the render sprite beneath `SpriteVisual`, apply scale on setup/update, use the same scale for unit placement preview, and make the prefab builder create the dedicated child for future rebuilds.

- [ ] **Step 4: Run the focused EditMode test again**

Run the same Unity command and expect all `UnitVisualScaleTests` tests to pass.

### Task 2: Creature Size editor window

**Files:**
- Create: `unity/TrollStategy/Assets/Game/Editor/Tools/CreatureSizeToolWindow.cs`
- Create: `unity/TrollStategy/Assets/Game/Editor/Tools/CreatureSizeToolWindow.cs.meta`
- Create: `unity/TrollStategy/Assets/Game/Editor/Tools.meta`

**Interfaces:**
- Consumes: `GameContentCatalog.Units`, serialized `_spriteScale`, and `UnitView.ApplySpriteScale()`
- Produces: menu item `TrollStrategy/Tools/Creature Size Tool`

- [ ] **Step 1: Build the editor window**

Load the runtime catalog without a hardcoded filesystem path, show every catalog creature with a cell-relative sprite preview, slider, exact numeric field, and reset button, and write changes through `SerializedObject` so Undo and dirty-state tracking work.

- [ ] **Step 2: Add live play-mode refresh**

After a scale edit, find active `UnitView` instances and call `ApplySpriteScale()` only for views using the edited definition.

- [ ] **Step 3: Verify compilation and the full EditMode suite**

Run Unity EditMode tests in batch mode and confirm the editor assembly and runtime assembly compile with all tests passing.

### Task 3: Repository verification

**Files:**
- Verify only; no additional files expected.

**Interfaces:**
- Consumes: completed runtime and editor changes.
- Produces: evidence that the existing web and Unity code remain healthy.

- [ ] **Step 1: Run web checks**

Run `npm test`, `npm run lint`, and `npm run build` from the repository root and expect success.

- [ ] **Step 2: Review the diff**

Confirm the diff contains no changes to domain state, save schemas, collider values, or unrelated user-owned files.
