# UI style: "air"

The look is "air": the island stays in view. Every screen, band and label serves that. Where the colours,
fonts and button styles live is in `AGENTS.md` § UI; this page is what the screen looks like. How the
redesign of a screen is run (mockups, choice, checks) is `docs/ui-redesign-workflow.md`.

## Text over the world

- Text over the world is ink with a white halo (`.halo`). The quest reads on the halo alone.
- Other text that runs to several lines sits on mist: `.float`, or `.float--tight` hugging one text.
- Mist stays under the text it carries: no screen-edge vignette and no mist bands across the screen.
- The halo is a `text-shadow`. A light `-unity-text-outline` eats into small letters and greys them.

## Sheets, veils and shadows

- White `.sheet`s with a baked shadow are only for dialogs and the hint card.
- Panels that hold text carry a baked shadow, never `filter: drop-shadow`: it blurs the text inside.
- A dialog that asks before the map may go on (the haul cargo) takes the full veil, so the island reads
  as out of reach.
- The one other veil is the tutorial pointer's (`GuideOverlay`, `Guide.uss`): a light veil with a soft
  window around the step's target, the hand pointing at it and a white hint card «Шаг N из M». It shows
  only in tutorial steps, is gone once the step is done, never catches the pointer, and adds no second
  veil over a dialog that has its own.

## Reward moments

Reward moments have no card.

- A quest reward is a white disc in the light (`glow.png`, `rays.png`) with the building's facts as
  pictures and numbers.
- A battle prize spins one white reel disc per digit, and its coins fly straight into the treasury
  counter, which `TopBar.ExpectGold` holds until they land.

## Catalog band and orders

- The catalog band runs the full width: tabs at its left end, tokens in the middle of the screen, the
  status line at its right end.
- While creatures are selected or the map waits for a pick, the orders (`ContextBar`) take the catalog's
  place in the same shape: who or what at the left end, round order tokens with their keys in the
  middle. The catalog tool drops the selection and brings the catalog back.
- The right-click orders (`CommandFan`) are an arc of round buttons over the click, the key's letter on
  each.
- A catalog tray shows whole tokens a page at a time (`CatalogPager`: eight on 1920 px, round arrows at
  its ends, a dot per page); a token never shrinks.

## Fighters' gear

A fighter's gear shows as tokens at the sides of its HP bar (`.hp-gear` in `WorldUi.uss`): the weapon on
the left, armour and helmet on the right, free slots as hollows only while the squad is placed.

## Numbers, buttons and words

- Numbers show as a picture and a number.
- Tools and catalog items are round `.btn-disc` buttons.
- Names, details and keys go into `HudTooltip`, in words the player can act on: what a creature is good
  for, not its raw stats.
- The book (`WikiPanel`, K) reads everything from the content catalog and keeps no numbers of its own.

## Type and colour

- One accent (roof blue); coin gold for spending and rewards.
- Nunito with tabular digits, sentence case, no letter-spacing and no "·" metadata.

## Sprites and icons

- Soft shapes are PNGs in `UI/Sprites`, imported by `UiTextureImport`.
- Pictograms are one-colour SVGs in `UI/Icons`, tinted from USS.
