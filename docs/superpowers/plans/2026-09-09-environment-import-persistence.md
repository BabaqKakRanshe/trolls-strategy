# Environment sprite import persistence

## Impact analysis and design

The two environment PNGs are already in the playable scope. Their authored slices in Git use 8 named props and 19 named tiles. Current metadata instead contains automatic slices; the authored version also retained obsolete name/ID lookup entries. AssetSlicer uses TextureImporter.spritesheet, which Unity 6000.6 explicitly reports as unsupported. No project startup slicer was found. The package's SpriteSlicePostprocessor exists, but SliceOnImport is not enabled in the inspected metadata; it is not a confirmed cause.

Change the existing slicer's save path to ISpriteEditorDataProvider and synchronize ISpriteNameFileIdDataProvider. Reuse sprite GUIDs by name and explicitly disable SliceOnImport. Add the already installed Unity.2D.Sprite.Editor reference only to the Editor assembly. Runtime dependencies and game rules stay unchanged; no packages or new assembly boundaries are introduced. Unit slicing shares the same save path to remove the unsupported API completely, but this repair invokes only environment slicing.

Back up both damaged metadata files, recover authored sprite rectangles/pivots/identities, and rebuild their lookup tables. Retain unrelated importer settings. Rebind only the generated EnvironmentWorld using its existing builder; preserve other scene roots. Save and validate actual scene sprite references.

## Verification

- Execute RepairAndVerify in Unity batch mode: repair two assets, save generated environment, force two reimports, compare names, rects, pivots, PPU, GUID and file IDs.
- Start another Unity process and execute Verify without repair, proving the restored metadata survives reopening.
- Run the existing EditMode suite and a Windows player build because the Editor assembly reference changed.
- Inspect final metadata diffs and compilation diagnostics. Web lint/E2E are unrelated to this Unity-only change.

## Recovery

Damaged metadata backups: test-results/environment-slice-backup. Repeat verification with -executeMethod TrollStrategy.Editor.Setup.EnvironmentImportVerification.Verify. Repair entry point: TrollStrategy.Editor.Setup.EnvironmentImportVerification.RepairAndVerify. No initialization hook continually overwrites manual edits.

## Results

- RepairAndVerify passed: 8 props, 19 tiles, stable imported identities and 472 valid generated scene sprite references.
- A separate Unity process ran VerifyAndBuild without repair: repeated imports preserved names, rectangles, pivots, PPU and local file IDs; Windows player build succeeded.
- Existing EditMode suite: 18 passed, 0 failed, 0 skipped (test-results/environment-editmode.xml).
- Logs: test-results/environment-repair.log and test-results/environment-reopen-build.log. Player: test-results/environment-player/TrollStrategy.exe.
- The SliceOnImport custom interface is internal in the installed package; only that flag uses the inspected serialized importer property. Sprite rectangles and identity mappings use the public data-provider API.
