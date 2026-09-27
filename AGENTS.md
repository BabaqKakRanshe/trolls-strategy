# Project Architecture Rules

These rules apply to every change in the Unity project at `unity/TrollStategy`.

## Scope and ownership

- Confirm that a feature belongs to the current playable scope before adding runtime code.
- Prefer the smallest complete vertical slice over speculative frameworks.
- For build, persistence, or state-ownership changes, analyze affected dependencies before editing.
- `GameSession` owns mutable campaign state. Gameplay changes enter through validated commands and commit atomically.
- `Runtime/Domain` contains deterministic game rules and has no Unity presentation or wall-clock dependencies.
- `Runtime/Application` coordinates commands and exposes snapshots; presentation does not own gameplay outcomes.
- `Runtime/Content` and its ScriptableObjects own gameplay values. Do not duplicate them in UI or simulation.
- `Runtime/Presentation` renders game state; `Runtime/UI` reads snapshots and submits commands.
- `Runtime/Bootstrap` composes the Unity scene and services. Keep gameplay rules out of it.

## State, simulation, and assets

- Grid coordinates, capacity, building footprints, and placement validity are domain rules. Previews and purchase commands use the same validation.
- Selection and target modes are transient application state, never a second owner of units, buildings, gold, or assignments.
- Economy advances in fixed 250 ms simulation steps. Do not couple domain results to render frame rate.
- Randomness and derived IDs must be explicit and repeatable when new systems require them.
- Keep save data serializable and versioned if persistence is added. Reject invalid references and ownership rather than silently repairing them.
- Unity project assets live under `unity/TrollStategy/Assets`. The shared `assets/Strategy_Kit` is source art; imported assets must be checked against it when changed.
- Preserve license evidence and the commercial-release gate for third-party art.

## Presentation and validation

- Scene-local state controls presentation only. Gameplay rules remain in domain/application code.
- Required player actions must remain available through the Unity UI and input flow.
- Test behavior at the narrowest correct boundary with Unity EditMode tests and relevant scene or player checks.
- Architecture-significant work is done when focused and full relevant Unity tests, compilation, and the applicable player build pass. `TrollStrategy/Build Windows Player` builds into `Builds/Windows` and writes `build-result.txt` there.
- Stop and redesign if fixes reveal errors one by one, ownership becomes ambiguous, or a second source of truth appears.

## Screen UI

- The colony HUD is UI Toolkit: layout in `Assets/Game/UI/Uxml`, look in `Assets/Game/UI/Styles`. Colours, fonts and button styles live only in `Theme.uss`; screens add layout.
- `Runtime/UI/Colony` screen parts are plain classes over a cloned UXML tree, so EditMode tests drive them without a scene; `ColonyHud` only connects the `UIDocument`, session and input. `TrollStrategy/Setup Colony HUD` installs the document into the scene.
- Bind every HUD button with `UiFeel.Bind` and mark unaffordable ones with `UiFeel.SetAvailable`, so a press always answers with a sound or a refusal.
- A button that holds a badge or other child needs its caption as a child label (`Ui.CaptionButton`); a text element with children stops measuring its own text.
- Layout containers are `picking-mode="Ignore"`; only panels and buttons catch the pointer, and `UIInputUtils` asks the HUD documents before a map click.
- The battle HUD is still uGUI inside `BattleSceneController`; its move is stage 2 of `docs/superpowers/plans/2026-09-27-ui-toolkit-hud.md`.

## Art handoff pipeline

- Follow `docs/art-asset-pipeline.md` for resource and building icon work.
- Put individual source PNGs under `assets/sprites/sources`; the user packs them into TexturePacker atlases under `assets/sprites/Atlases` and supplies the PNG and JSON files.
- Keep the current Unity sprites working until the supplied atlas is imported, references are migrated, and the scene is checked. Then remove superseded Unity imports; keep source PNGs and TexturePacker project files for future rebuilds.
- Keep resource output sprites configurable on building prefabs and semantic building icons configurable in `BuildingDefinition`.
