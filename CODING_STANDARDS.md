# Coding standards

How code in the Unity project `unity/TrollStategy` is written: C#, USS, UXML and the prefabs and
ScriptableObjects that wire them. The look, art, audio, telemetry, builds and bots have their own docs,
named in `AGENTS.md`.

## Layers and ownership

All `Runtime` layers compile into one assembly (`TrollStrategy.Runtime`), so the compiler does not keep
them apart: these rules do.

`GameSession` is the single owner of mutable campaign state. Gameplay changes enter it through validated
commands and commit atomically. Gameplay outcomes are decided in domain and application code only.

- `Runtime/Domain`: deterministic game rules, free of Unity presentation and the wall clock (see
  *State and simulation*).
- `Runtime/Application` coordinates commands and exposes snapshots. Battle deployment (who stands where,
  who wears what) is `BattleDeployment` here.
- `Runtime/Content` and its ScriptableObjects own gameplay values; UI and simulation read them from there.
- `Runtime/Presentation` renders game state; scene-local state controls presentation only.
  `BattleSceneController` owns the battle arena, camera and replay clock and reaches the screen only
  through `IBattleScreen`.
- `Runtime/UI` reads snapshots and submits commands (see *UI code*).
- `Runtime/Bootstrap` only composes the Unity scene and services.
- `Runtime/Support` (build stamp, frame meter, bug reports) knows no gameplay.
- Bots (`Assets/Game/Bots`, Editor-only `TrollStrategy.Bots`) play the campaign for balance checks
  through `GameSession` snapshots and commands, as a player does.

## State and simulation

- **Placement.** Grid coordinates, capacity, building footprints and placement validity are domain rules;
  previews and purchase commands use the same validation.
- **Transient selection.** Selection and target modes are transient application state, never a second
  owner of units, buildings, gold or assignments.
- **Deterministic.** The economy runs on a fixed timestep (the step time in `EconomyConfig`), so domain
  results are the same at any render frame rate. Randomness and derived IDs that new systems need are
  explicit and repeatable.
- **Saves**, once persistence is added: serializable and versioned. Loading rejects invalid references
  and ownership instead of repairing them silently.

## UI code

- **UI Toolkit** is the only UI: no uGUI canvases, no TextMeshPro. Layout lives in
  `Assets/Game/UI/Uxml`, look in `Assets/Game/UI/Styles`. Colours, fonts and button styles live only in
  `Theme.uss`; screens and world labels add layout and sizes.
- **Text** reaches the screen through `Ui.SetText`, `Ui.Text`, `Ui.TextButton` and `Ui.CaptionButton`
  (world labels: `Localization.T`). They translate the Russian source text, which is the key, and
  remember it for a language change; a `.text` set directly on a HUD element gets neither.
- **Buttons.** Bind every button with `UiFeel.Bind` and mark unavailable ones with `UiFeel.SetAvailable`,
  so a press always answers with a sound or a refusal. A button that holds a badge or other child takes
  its caption as a child label (`Ui.CaptionButton`): a text element with children stops measuring its
  own text.
- **Screen code.** Screen parts in `Runtime/UI/Colony` and `Runtime/UI/Battle` are plain classes over
  their document's root, so EditMode tests drive them without a scene; `TestUi` clones the prefab's
  document tree. `ColonyHud` and `BattleHud` only connect the documents, session and input.
- **Player actions.** Every required player action stays reachable through the UI and input.
- **UI documents:** before adding a screen, panel or world label, or changing the `UI` prefab, a
  built-in control or pointer input: read `docs/ui-toolkit.md`.

## Boundary changes

- **Impact analysis first.** Before a build, persistence or state-ownership change, analyse the
  dependencies it affects, then edit.
- **Redesign** when fixes reveal errors one by one, ownership becomes ambiguous, or a second source of
  truth appears: stop patching and rethink the change.

## Tests and definition of done

- Test behaviour at the narrowest correct boundary: Unity EditMode tests, plus scene or player checks
  where they are relevant.
- **Done.** Architecture-significant work is done when focused and full relevant Unity tests,
  compilation and the applicable player build pass.
  - `TrollStrategy/Dev/Tools/Run EditMode Tests` runs every EditMode test and writes
    `Builds/Tests/editmode-summary.txt`.
  - `TrollStrategy/Build Windows Player` builds into `Builds/Windows` and writes `build-result.txt` there.
