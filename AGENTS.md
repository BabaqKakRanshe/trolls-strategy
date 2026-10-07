# Project Architecture Rules

These rules apply to every change in the Unity project at `unity/TrollStategy`. Topic rules live in the docs the pointers below name: read the doc before working on its topic.

## Scope and ownership

- For build, persistence, or state-ownership changes, analyze affected dependencies before editing.
- `GameSession` owns mutable campaign state. Gameplay changes enter through validated commands and commit atomically.
- `Runtime/Domain` contains deterministic game rules and has no Unity presentation or wall-clock dependencies.
- `Runtime/Application` coordinates commands and exposes snapshots. Gameplay outcomes are decided in domain and application code, never in presentation, scene-local state or bootstrap.
- `Runtime/Content` and its ScriptableObjects own gameplay values; UI and simulation read them from there.
- `Runtime/Presentation` renders game state; `Runtime/UI` reads snapshots and submits commands.
- `Runtime/Bootstrap` composes the Unity scene and services.
- `Runtime/Support` (build stamp, frame meter, bug reports) knows no gameplay.
- Battle deployment (who stands where, who wears what) is `BattleDeployment` in the application layer. `BattleSceneController` owns the arena, camera and replay clock and reaches the screen only through `IBattleScreen`.
- Bots (`Assets/Game/Bots`, Editor-only `TrollStrategy.Bots`) play the campaign for balance checks through `GameSession` snapshots and commands, as a player does. Before changing bots or reading a bot run: read `docs/campaign-bots.md`.

## State, simulation, and assets

- Grid coordinates, capacity, building footprints, and placement validity are domain rules. Previews and purchase commands use the same validation.
- Selection and target modes are transient application state, never a second owner of units, buildings, gold, or assignments.
- The economy runs on a fixed timestep (the step time in `EconomyConfig`); domain results never depend on render frame rate.
- Randomness and derived IDs must be explicit and repeatable when new systems require them.
- Keep save data serializable and versioned if persistence is added. Reject invalid references and ownership rather than silently repairing them.
- Third-party art, sounds and music: each gets an `assets/licenses.json` entry with its licence evidence, and the commercial-release gate (`commercialReleaseBlocked` there) is preserved. The repository is public: a source file whose licence forbids redistribution stays out of git.

## Presentation and validation

- Required player actions must remain available through the Unity UI and input flow.
- Test behavior at the narrowest correct boundary with Unity EditMode tests and relevant scene or player checks.
- Architecture-significant work is done when focused and full relevant Unity tests, compilation, and the applicable player build pass. `TrollStrategy/Build Windows Player` builds into `Builds/Windows` and writes `build-result.txt` there.
- Stop and redesign if fixes reveal errors one by one, ownership becomes ambiguous, or a second source of truth appears.

## UI

- All UI is UI Toolkit, never uGUI canvases or TextMeshPro. Layout lives in `Assets/Game/UI/Uxml`, look in `Assets/Game/UI/Styles`. Colours, fonts and button styles live only in `Theme.uss`; screens and world labels add layout and sizes.
- Texts reach the screen through `Ui.SetText`, `Ui.Text`, `Ui.TextButton` and `Ui.CaptionButton` (world labels: `Localization.T`), never by setting `.text` on HUD elements: the Russian text is the translation key. Before writing or changing player-facing text: read `docs/localization.md`.
- UI look ("air"): before changing USS, colours, icons, overlays, dialogs, reward moments, or the catalog and order bands: read `docs/ui-style.md`.
- UI documents: before adding a screen, panel, button or world label, or changing the `UI` prefab or pointer input: read `docs/ui-toolkit.md`.
- UI redesign: before redesigning a screen with the user: follow `docs/ui-redesign-workflow.md`.

## Topic docs

- Art: before importing models, sprites or icons, or changing source art in `assets/Strategy_Kit`: read `docs/art-asset-pipeline.md`.
- Audio: before adding or changing sounds, music, or calls to `GameAudio` or `Soundscape`: read `docs/audio-direction.md`.
- Telemetry: before adding or changing a campaign event, its fields or the consent question: read `docs/analytics.md`.
- Player builds: before changing editions (Steam demo, itch.io alpha), store links, the support corner or bug reports: read `docs/player-builds.md`.
