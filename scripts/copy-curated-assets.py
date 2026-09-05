import json
import shutil
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
ASSETS_ROOT = (PROJECT_ROOT / "assets").resolve()
PUBLIC_ROOT = (PROJECT_ROOT / "public" / "assets").resolve()
MANIFEST_SOURCE = ASSETS_ROOT / "curated-assets.json"
RUNTIME_MANIFEST = PUBLIC_ROOT / "manifest.json"


def resolve_inside(root: Path, relative_path: str) -> Path:
    candidate = (PROJECT_ROOT / relative_path).resolve()
    if candidate != root and root not in candidate.parents:
        raise ValueError(f"Path escapes allowed root: {relative_path}")
    return candidate


def main() -> None:
    manifest = json.loads(MANIFEST_SOURCE.read_text(encoding="utf-8"))
    previous_manifest = (
        json.loads(RUNTIME_MANIFEST.read_text(encoding="utf-8"))
        if RUNTIME_MANIFEST.is_file()
        else {"assets": []}
    )
    runtime_assets = []
    for entry in manifest["assets"]:
        source = resolve_inside(ASSETS_ROOT, entry["source"])
        output = resolve_inside(PUBLIC_ROOT, entry["output"])
        if not source.is_file():
            raise FileNotFoundError(source)
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, output)
        runtime_entry = {
            "key": entry["key"],
            "type": entry["type"],
            "url": "/" + output.relative_to(PROJECT_ROOT / "public").as_posix(),
        }
        for metadata_key in (
            "frameWidth", "frameHeight", "frameDurationMs", "animationStart", "animationEnd"
        ):
            if metadata_key in entry:
                runtime_entry[metadata_key] = entry[metadata_key]
        runtime_assets.append(runtime_entry)

    current_urls = {entry["url"] for entry in runtime_assets}
    for previous_entry in previous_manifest.get("assets", []):
        stale_url = previous_entry.get("url")
        if not isinstance(stale_url, str) or stale_url in current_urls:
            continue
        stale_output = resolve_inside(PUBLIC_ROOT, "public/" + stale_url.lstrip("/"))
        if stale_output.is_file():
            stale_output.unlink()

    PUBLIC_ROOT.mkdir(parents=True, exist_ok=True)
    temporary_manifest = RUNTIME_MANIFEST.with_suffix(".json.tmp")
    temporary_manifest.write_text(
        json.dumps({"schemaVersion": 1, "assets": runtime_assets}, indent=2),
        encoding="utf-8",
    )
    temporary_manifest.replace(RUNTIME_MANIFEST)
    print(f"Prepared {len(runtime_assets)} curated sprites.")


if __name__ == "__main__":
    main()
