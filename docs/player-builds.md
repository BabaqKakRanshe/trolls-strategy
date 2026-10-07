# Player builds and the support corner

## Editions

- Player builds get their version, commit and edition from `BuildInfoStamp`:
  `TrollStrategy/Build Windows Player (Steam Demo)` stamps the Steam demo, every other build the itch.io
  alpha.
- The edition words the intro and the about page. Their Steam buttons stay hidden while
  `GameLinks.SteamPage` is empty.

## Support corner

- The support corner (`SupportHud`, `Uxml/Support`) sits above both HUDs in every build: version, FPS and
  the F8 tech panel with "send logs".
- `Runtime/Support` holds the build stamp, frame meter and bug reports.
- Bug reports go to Unity User Reporting and fall back to a zip in `persistentDataPath/Reports`.
- The tech panel also switches play statistics on and off; the statistics themselves are
  `docs/analytics.md`.
