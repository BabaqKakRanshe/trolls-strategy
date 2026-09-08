# Unity Asset Deduplication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove duplicate Unity prefabs and content definitions while preserving scene references and complete unit animation data.

**Architecture:** Keep `Assets/Game/Prefabs` and the modern assets in `Assets/Game/Content/Definitions` as the only canonical runtime assets. `MainColonyScene` supplies serialized references to `GameBootstrap`; the bootstrap validates them instead of loading copied assets from `Resources`. The editor scene builder recreates only canonical assets and loads every sliced animation frame in numeric order.

**Tech Stack:** Unity 6.6, C#, Unity AssetDatabase, YAML-serialized Unity assets, NUnit EditMode tests

**Spec:** `AGENTS.md`

## Global Constraints

- Preserve all unrelated working-tree changes.
- Keep Unity GUID references valid and remove each deleted asset together with its `.meta` file.
- Keep gameplay facts in one canonical content asset.
- Run focused EditMode tests, the full Unity EditMode suite, the Unity player build, and the repository checks.

---

### Task 1: Preserve canonical unit animation data

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Editor/Setup/GameSceneBuilder.cs`
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Unit_Goblin.asset`
- Modify: `unity/TrollStategy/Assets/Game/Content/Definitions/Unit_Troll.asset`

**Interfaces:**
- Consumes: sliced Sprite sub-assets imported from the four unit sprite sheets.
- Produces: `LoadSpriteFrames(string): Sprite[]`, ordered by the numeric suffix in each Sprite name.

- [x] Add a helper that calls `AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()` and sorts frames by the final numeric suffix.
- [x] Use the helper for goblin and troll idle/walk arrays and pass frame zero as the portrait.
- [x] Copy the currently valid 16/4 goblin and 16/6 troll frame references into the canonical assets before deleting legacy definitions.
- [x] Rebuild the scene and verify the canonical assets retain full frame arrays.

### Task 2: Remove the Resources fallback and duplicate assets

**Files:**
- Modify: `unity/TrollStategy/Assets/Game/Editor/Setup/GameSceneBuilder.cs`
- Modify: `unity/TrollStategy/Assets/Game/Runtime/Bootstrap/GameBootstrap.cs`
- Delete: duplicate assets in `unity/TrollStategy/Assets/Game/Resources`
- Delete: legacy `GoblinDef`, `TrollDef`, `MineDef`, `MarketDef`, and `WarehouseDef` assets from `Assets/Game/Content/Definitions`

**Interfaces:**
- Consumes: serialized `_catalog`, `_buildingPrefab`, and `_unitPrefab` references in `MainColonyScene`.
- Produces: one explicit validation path for required bootstrap assets.

- [x] Stop copying the catalog and saving prefabs into `Resources` in `GameSceneBuilder`.
- [x] Replace the three `Resources.Load` calls with a single required-reference guard that logs an error, disables the component, and returns.
- [x] Run an explicit-target cleanup script that verifies all paths remain inside `Assets/Game`, then removes the 14 duplicate assets, their `.meta` files, and the empty `Resources` folder/meta.
- [x] Scan GUID references and duplicate content again; expect no missing references and no duplicate gameplay assets.

### Task 3: Verify the complete project

**Files:**
- Test: `unity/TrollStategy/Assets/Game/Tests/EditMode`

**Interfaces:**
- Consumes: the deduplicated Unity project.
- Produces: Unity test/build logs and repository validation results.

- [x] Run Unity in batch mode to execute the scene builder, then run focused and full EditMode tests.
- [x] Build the configured Windows player in batch mode and require a successful BuildReport.
- [x] Run repository unit tests, lint, production build, and relevant Playwright E2E tests.
- [x] Inspect `git diff --check`, final status, and all changed/deleted paths without committing unrelated user work.
