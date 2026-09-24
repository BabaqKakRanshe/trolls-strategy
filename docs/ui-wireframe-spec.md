# Colony UI Wireframe Specification

## Goal

Replace the prototype's scattered controls with a grayscale, primitive-only HUD that makes the first economy loop and the current interaction state obvious without changing game rules.

## Screen structure

- A top bar owns settlement identity and the four live counters.
- A left rail owns the onboarding objective, quick unit selection, and the unit roster.
- The center owns the Phaser world and one visible status message.
- A right rail owns construction and hiring controls.
- A persistent bottom dock owns commands for the current selection and target choices.

## Required behavior

- Preserve every existing command through the current `data-action` dispatch path.
- Preserve resource, roster, status, hire amount, debug, and test selectors.
- Derive onboarding progress and production rate from immutable snapshots only.
- Keep the grid and routes inside Phaser; hide the grid by default and reveal it for placement or on `G`.
- Use semantic HTML, visible focus states, keyboard-readable labels, and one polite live region.
- Use grayscale surfaces, borders, type, and simple CSS shapes only for the HUD. Curated sprites remain limited to the Phaser world.

## Responsive behavior

- Desktop uses three columns with the world as the flexible center.
- Narrow desktop keeps both rails but reduces their widths.
- Tablet and mobile stack the objective, world, shop, and command dock without removing actions.
