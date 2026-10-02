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
- `Assets/Game/Bots` (Editor-only `TrollStrategy.Bots`) plays the campaign through `GameSession` snapshots and commands for balance checks; see `docs/campaign-bots.md`. Bots never read or write `GameState` and never get a rule of their own: what a player cannot do, a bot cannot do.

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

## UI

- All UI is UI Toolkit; do not add uGUI canvases or TextMeshPro. Layout lives in `Assets/Game/UI/Uxml`, look in `Assets/Game/UI/Styles`. Colours, fonts and button styles live only in `Theme.uss`; screens and world labels add layout and sizes.
- The look is "air": the island stays in view. Text over the world is ink with a white halo (`.halo`); the quest reads on the halo alone, other text that runs to several lines sits on mist (`.float`, or `.float--tight` hugging one text); no screen-edge vignette and no mist bands across the screen; white `.sheet`s with a baked shadow are only for dialogs and the hint card. A dialog that asks before the map may go on (the haul cargo) takes the full veil, so the island reads as out of reach. Reward moments have no card either: a quest reward is a white disc in the light (`glow.png`, `rays.png`) with the building's facts as pictures and numbers; a battle prize spins one white reel disc per digit, and its coins fly straight into the treasury counter, which `TopBar.ExpectGold` holds until they land. The catalog band runs the full width: tabs at its left end, tokens in the middle of the screen, the status line at its right end. While creatures are selected or the map waits for a pick, the orders (`ContextBar`) take the catalog's place in the same shape: who or what at the left end, round order tokens with their keys in the middle; the catalog tool drops the selection and brings the catalog back. The right-click orders (`CommandFan`) are an arc of round buttons over the click, the key's letter on each. A catalog tray shows whole tokens a page at a time (`CatalogPager`: eight on 1920 px, round arrows at its ends, a dot per page); a token never shrinks. A fighter's gear shows as tokens at the sides of its HP bar (`.hp-gear` in WorldUi.uss): the weapon on the left, armour and helmet on the right, free slots as hollows only while the squad is placed. The book (`WikiPanel`, K) reads everything from the content catalog and never keeps numbers of its own. Numbers show as a picture and a number, tools and catalog items as round `.btn-disc` buttons; names, details and keys go into `HudTooltip`, in words the player can act on (what a creature is good for, not its raw stats). One accent (roof blue), coin gold for spending and rewards, Nunito (tabular digits), sentence case, no letter-spacing and no "·" metadata. Soft shapes are PNGs in `UI/Sprites` (imported by `UiTextureImport`), pictograms are one-colour SVGs in `UI/Icons` tinted from USS. The halo is a `text-shadow`: a light `-unity-text-outline` eats into small letters and greys them. Do not put `filter: drop-shadow` on panels that hold text.
- The screen UI is the `UI` prefab (`Assets/Game/UI/Prefabs/UI.prefab`): one GameObject per screen, band and panel, each a `UIDocument` nested in its parent's. Panels have their own UXML in `Uxml/Colony` or `Uxml/Battle`; bands and columns have none and take their layout classes from `UiDocumentClasses`. `UiSetup` describes the tree and `TrollStrategy/Dev/Setup UI` rebuilds the prefab from it, so change the structure there.
- A nested `UIDocument` finds its parent when the component is added. Add it after the GameObject has its parent, or it becomes a separate panel. After a scene load Unity can attach nested documents out of their sorting order (the catalog band above the quest); `UiDocumentOrder` on the UI root puts them back every frame, so keep it there.
- Screen parts in `Runtime/UI/Colony` and `Runtime/UI/Battle` are plain classes over their document's root, so EditMode tests drive them without a scene; `TestUi` clones the prefab's document tree. `ColonyHud` and `BattleHud` only connect the documents, session and input.
- Battle deployment (who stands where, who wears what) is `BattleDeployment` in the application layer. `BattleSceneController` owns the arena, camera and replay clock and reaches the screen only through `IBattleScreen`.
- Bind every button with `UiFeel.Bind` and mark unavailable ones with `UiFeel.SetAvailable`, so a press always answers with a sound or a refusal.
- A button that holds a badge or other child needs its caption as a child label (`Ui.CaptionButton`); a text element with children stops measuring its own text.
- The game imports no Unity theme: a built-in control (the menu's `Slider`) gets its parts drawn in `Theme.uss`, and a choice from a long list is a page or a grid of buttons, not a `DropdownField`, whose popup lands outside the HUD's documents and their styles.
- Document roots and layout containers ignore the pointer; only panels and buttons catch it, and `UIInputUtils` asks the registered documents before a map or board click.
- The support corner (`SupportHud`, `Uxml/Support`) sits above both HUDs in every build: version, FPS and the F8 tech panel with "send logs". `Runtime/Support` holds the build stamp, frame meter and bug reports and knows no gameplay; reports go to Unity User Reporting and fall back to a zip in `persistentDataPath/Reports`. Play statistics for the drop-off funnel: `CampaignTelemetry` (application) turns snapshots into events, `Telemetry` sends them to Unity Analytics while the player allows it (on until turned off in the tech panel). The dashboard drops events it has no schema for: keep `CampaignTelemetry.Schema` and `docs/analytics.md` in step. Player builds get their version, commit and edition from `BuildInfoStamp`: `TrollStrategy/Build Windows Player (Steam Demo)` stamps the Steam demo, every other build the itch.io alpha. The edition words the intro and the about page; their Steam buttons stay hidden while `GameLinks.SteamPage` is empty.
- Text, numbers and bars over things in the world are `WorldPanel`s: world-space `UIDocument`s on `WorldPanelSettings` (100 px per world unit, no colliders) styled by `WorldUi.uss`; panels made in code get the settings from `GameBootstrap`. A `WorldPanel` owns its transform scale: as the camera backs off it grows so its largest text keeps 18 px on a 1080 px screen, and far away its content takes `.world-panel--far`, where styles drop details. Owners size it through `Scale`, never the transform; panels under a camera that does not zoom (battle) turn `KeepReadable` off and scale themselves.

## Art handoff pipeline

- Follow `docs/art-asset-pipeline.md` for resource and building icon work.
- Put individual source PNGs under `assets/sprites/sources`; the user packs them into TexturePacker atlases under `assets/sprites/Atlases` and supplies the PNG and JSON files.
- Keep the current Unity sprites working until the supplied atlas is imported, references are migrated, and the scene is checked. Then remove superseded Unity imports; keep source PNGs and TexturePacker project files for future rebuilds.
- Keep resource output sprites configurable on building prefabs and semantic building icons configurable in `BuildingDefinition`.

## Audio

- Follow `docs/audio-direction.md`. Feedback cues are generated by `assets/audio/build_sfx.py`; change the recipe and regenerate rather than editing or filtering the WAVs.
- Tuned cues stay in the music's key (F major pentatonic): shift them with `GameAudio.Step` or `GameAudio.Semitones`, never with a free pitch value.
- `Soundscape` owns music and ambience; game code only calls `Soundscape.Enter` and `Soundscape.SetPaused`.
- Every third-party sound or track gets an `assets/licenses.json` entry. This repository is public: do not commit source files whose licence forbids redistribution.

## Localization

- The game is written in Russian and the Russian text is the translation key (`Presentation/Localization`). Texts reach the screen through `Ui.SetText`, `Ui.Text`, `Ui.TextButton` and `Ui.CaptionButton`, which translate and remember the source; world labels call `Localization.T`. Do not set `.text` directly on HUD elements.
- Template keys are interpolated strings: `$"Построить: {name}"` is the key `Построить: {0}`, and the filled part is translated as a name. Prefer whole sentences with holes over strings glued from pieces.
- After adding or changing player-facing text run `python tools/localization/extract.py`, translate the new keys into every file in `tools/localization/translations` (check with `validate.py <code>`), then `python tools/localization/build.py` and the menu `TrollStrategy/Dev/Setup Localization Fonts`.
