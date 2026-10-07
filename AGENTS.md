# Troll Strategy

A colony economy game with battles. The Unity project is `unity/TrollStategy` (the folder name is spelt so); game code lives in its `Assets/Game`.

## Code

Before any change in `unity/TrollStategy`: read `CODING_STANDARDS.md` (layers and ownership, state and simulation, UI code, boundary changes). Work there is done only when its *Definition of done* holds.

## Licences

The repository is public. Every piece of third-party art, sound and music gets an `assets/licenses.json` entry with its licence evidence, and the commercial-release gate (`commercialReleaseBlocked` there) is preserved. A source file whose licence forbids redistribution stays out of git.

## Topic docs

Read the doc before working on its topic:

- UI look ("air"): USS, colours, icons, overlays, dialogs, reward moments, the catalog and order bands: `docs/ui-style.md`.
- UI redesign with the user: follow `docs/ui-redesign-workflow.md`.
- Localization: adding, changing or translating player-facing text: `docs/localization.md`.
- Art: importing models, sprites or icons, or changing source art in `assets/Strategy_Kit`: `docs/art-asset-pipeline.md`.
- Audio: sounds, music, or calls to `GameAudio` or `Soundscape`: `docs/audio-direction.md`.
- Telemetry: a campaign event, its fields or the consent question: `docs/analytics.md`.
- Player builds: editions (Steam demo, itch.io alpha), store links, the support corner, bug reports: `docs/player-builds.md`.
- Bots: changing bots or reading a bot run: `docs/campaign-bots.md`.
