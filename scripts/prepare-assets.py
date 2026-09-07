from __future__ import annotations

import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = (ROOT / "assets" / "sprites" / "sources").resolve()
RUNTIME_ROOT = (ROOT / "public" / "assets" / "runtime").resolve()
MANIFEST_PATH = ROOT / "assets" / "curated-assets.json"
LICENSES_PATH = ROOT / "assets" / "licenses.json"

PROMOTIONS = {
    "assets/sprites/ForgottenMemories (1)/TileSet.png": "assets/sprites/sources/environment/forgotten-memories-tiles.png",
    "assets/sprites/ForgottenMemories (1)/Props.png": "assets/sprites/sources/environment/forgotten-memories-props.png",
    "assets/sprites/Minifantasy_Creatures_Assets/Base_Humanoids/Goblin/GoblinIdle.png": "assets/sprites/sources/units/goblin-idle.png",
    "assets/sprites/Minifantasy_Creatures_Assets/Base_Humanoids/Goblin/GoblinWalk.png": "assets/sprites/sources/units/goblin-walk.png",
    "assets/sprites/Minifantasy_Creatures_Assets/Monsters/Troll/TrollIdle.png": "assets/sprites/sources/units/troll-idle.png",
    "assets/sprites/Minifantasy_Creatures_Assets/Monsters/Troll/TrollWalk.png": "assets/sprites/sources/units/troll-walk.png",
}


def inside(path: Path, parent: Path) -> bool:
    return path == parent or path.is_relative_to(parent)


def promote_unit_sources() -> None:
    for original_name, curated_name in PROMOTIONS.items():
        original = (ROOT / original_name).resolve()
        curated = (ROOT / curated_name).resolve()
        if not original.is_file() or not inside(curated, SOURCE_ROOT):
            raise RuntimeError(f"Invalid curated unit source: {original_name}")
        curated.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, curated)


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected object in {path.name}")
    return value


def prepare_runtime() -> None:
    promote_unit_sources()
    manifest = load_json(MANIFEST_PATH)
    licenses = load_json(LICENSES_PATH)
    license_ids = {entry["id"] for entry in licenses.get("licenses", [])}
    assets = manifest.get("assets", [])
    keys: set[str] = set()
    outputs: set[Path] = set()
    resolved: list[tuple[Path, Path]] = []

    for entry in assets:
        source = (ROOT / entry["source"]).resolve()
        output = (ROOT / entry["output"]).resolve()
        if entry["key"] in keys or output in outputs:
            raise RuntimeError(f"Duplicate manifest entry: {entry['key']}")
        if entry["licenseId"] not in license_ids:
            raise RuntimeError(f"Unknown license: {entry['licenseId']}")
        if not source.is_file() or not inside(source, SOURCE_ROOT):
            raise RuntimeError(f"Source is outside curated root: {source}")
        if not inside(output, RUNTIME_ROOT):
            raise RuntimeError(f"Output escapes runtime root: {output}")
        keys.add(entry["key"])
        outputs.add(output)
        resolved.append((source, output))

    public_assets = (ROOT / "public" / "assets").resolve()
    if not inside(RUNTIME_ROOT, public_assets):
        raise RuntimeError("Refusing to clear an unsafe runtime path")
    if RUNTIME_ROOT.exists():
        shutil.rmtree(RUNTIME_ROOT)
    for source, output in resolved:
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, output)

    print(f"Prepared {len(resolved)} curated runtime assets")


if __name__ == "__main__":
    prepare_runtime()
