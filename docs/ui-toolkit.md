# UI Toolkit: the UI prefab, documents and world panels

How the screen UI is built and wired. The look is `docs/ui-style.md`. The UI code rules every edit needs
(UI Toolkit only, `Theme.uss`, text, buttons, screen code) are `CODING_STANDARDS.md` § UI code.

## The UI prefab

- The screen UI is the `UI` prefab (`Assets/Game/UI/Prefabs/UI.prefab`): one GameObject per screen, band
  and panel, each a `UIDocument` nested in its parent's.
- Panels have their own UXML in `Uxml/Colony` or `Uxml/Battle`; bands and columns have none and take
  their layout classes from `UiDocumentClasses`.
- `UiSetup` describes the tree and `TrollStrategy/Dev/Setup UI` rebuilds the prefab from it, so change
  the structure there.

## Nested documents

- A nested `UIDocument` finds its parent when the component is added. Add it after the GameObject has its
  parent, or it becomes a separate panel.
- After a scene load Unity can attach nested documents out of their sorting order (the catalog band above
  the quest). `UiDocumentOrder` on the UI root puts them back every frame, so keep it there.

## Built-in controls

The game imports no Unity theme.

- A built-in control (the menu's `Slider`) gets its parts drawn in `Theme.uss`.
- A choice from a long list is a page or a grid of buttons, not a `DropdownField`: its popup lands
  outside the HUD's documents and their styles.

## Pointer

Document roots and layout containers ignore the pointer; only panels and buttons catch it.
`UIInputUtils` asks the registered documents before a map or board click.

## World panels

- Text, numbers and bars over things in the world are `WorldPanel`s: world-space `UIDocument`s on
  `WorldPanelSettings` (100 px per world unit, no colliders) styled by `WorldUi.uss`. Panels made in code
  get the settings from `GameBootstrap`.
- A `WorldPanel` owns its transform scale: as the camera backs off it grows so its largest text keeps
  18 px on a 1080 px screen, and far away its content takes `.world-panel--far`, where styles drop
  details.
- Owners size a panel through `Scale`, never the transform. Panels under a camera that does not zoom
  (battle) turn `KeepReadable` off and scale themselves.
