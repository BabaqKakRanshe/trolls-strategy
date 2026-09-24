# Unity HUD readability implementation plan

**Goal:** Correct the Unity HUD shown in the review: compact readable panels, explicit purchases, guided onboarding and contained scrolling.
**Architecture:** Presentation-only changes to the existing uGUI builder and snapshot consumers. Keep campaign ownership, commands, content, assembly boundaries and world objects unchanged. Rebuild only the HUD in the current scene and rewire its existing bootstrap references.
**Tech Stack:** Unity 6, uGUI, TMP, existing Unity MCP editor connection.
**Spec:** Screenshot review in this task. Existing grayscale wireframe is historical context; use restrained warm/green accents for current actions.

## Implementation
- [x] Correct layout sizing in GameSceneBuilder: disable forced expansion, bound text widths, anchor rails between top and bottom bars, vertically scroll roster/catalog, keep command targets inside dock.
- [x] Replace shop placeholders with existing catalog sprites; derive descriptions/prices from catalog. Show action and total gold in ShopDockView.
- [x] Add snapshot-driven objective highlighting in HudPresenter with serialized UI references; no gameplay state or progression saved in UI.
- [x] Reduce grid contrast in HaulRouteVisualizer.
- [x] Refresh scripts, replace only HUD via editor command, save scene, verify runtime button flow and bounds at multiple canvas aspect ratios, inspect capture and console.

## Impact and verification
No domain, persistence, package, assembly or build changes. Existing working-tree modifications must be preserved. Full scene setup would overwrite unrelated environment/prefab work, so it must not be used. Verify construction, hire amount and cost, unit placement, objective progression, selection and work target through existing public commands/buttons. Check all visible HUD buttons stay within the canvas and cards do not overlap. Record any unavailable verification honestly.

## Results
Unity compilation passed. The saved colony scene received only a HUD rebuild. A runtime journey through button callbacks and public placement commands verified construction, two-unit hire pricing, quick selection, work assignment and objective completion (720 gold remaining). Canvas bounds/text checks passed at 1440, 1920 and 2560 by 1080 layout units; this is a layout check, not physical Game View resizing. Preview inspected at test-results/unity-hud-refined.png. Fixed pre-existing quick-selection callbacks being registered only during editor setup. No standalone player build or full EditMode suite run for this presentation change.
