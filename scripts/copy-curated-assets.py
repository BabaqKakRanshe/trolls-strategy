from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parent.parent
SOURCE_ROOT = (PROJECT_ROOT / "assets").resolve()
RUNTIME_ROOT = (PROJECT_ROOT / "public" / "assets" / "runtime").resolve()
CURATED_PATH = SOURCE_ROOT / "curated-assets.json"
LICENSES_PATH = SOURCE_ROOT / "licenses.json"
RUNTIME_MANIFEST_PATH = PROJECT_ROOT / "public" / "assets" / "manifest.json"
CREDITS_PATH = PROJECT_ROOT / "public" / "assets" / "CREDITS.txt"


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def resolve_below(project_relative_path: str, root: Path, label: str) -> Path:
    candidate_path = Path(project_relative_path)
    if candidate_path.is_absolute() or ".." in candidate_path.parts:
        raise ValueError(f"Unsafe {label} path: {project_relative_path}")
    resolved = (PROJECT_ROOT / candidate_path).resolve()
    if not resolved.is_relative_to(root) or resolved == root:
        raise ValueError(f"{label.capitalize()} leaves guarded root: {project_relative_path}")
    return resolved


def runtime_url(output_path: str) -> str:
    public_relative = Path(output_path).relative_to("public")
    return public_relative.as_posix()


def manifest_url_to_path(output_url: str) -> Path:
    relative = output_url.removeprefix("/")
    return resolve_below(f"public/{relative}", RUNTIME_ROOT, "old output")


def validate_manifests(
    curated: dict[str, Any], license_manifest: dict[str, Any]
) -> list[tuple[dict[str, Any], Path, Path]]:
    licenses = license_manifest.get("licenses")
    assets = curated.get("assets")
    if not isinstance(licenses, list) or not isinstance(assets, list):
        raise ValueError("Both manifests must contain arrays")

    license_by_id = {
        record.get("id"): record for record in licenses if isinstance(record, dict)
    }
    keys: set[str] = set()
    cache_keys: set[str] = set()
    outputs: set[Path] = set()
    validated: list[tuple[dict[str, Any], Path, Path]] = []

    for entry in assets:
        if not isinstance(entry, dict):
            raise ValueError("Every asset entry must be an object")
        key = entry.get("key")
        source_name = entry.get("source")
        output_name = entry.get("output")
        license_id = entry.get("licenseId")
        if not all(isinstance(value, str) and value for value in (
            key, source_name, output_name, license_id
        )):
            raise ValueError(f"Incomplete asset entry: {entry}")
        if key in keys:
            raise ValueError(f"Duplicate cache key: {key}")
        keys.add(key)
        effective_cache_key = entry.get("cacheKey", key)
        if not isinstance(effective_cache_key, str) or not effective_cache_key:
            raise ValueError(f"Invalid effective cache key for {key}")
        if effective_cache_key in cache_keys:
            raise ValueError(f"Duplicate effective cache key: {effective_cache_key}")
        cache_keys.add(effective_cache_key)

        source = resolve_below(source_name, SOURCE_ROOT, "source")
        output = resolve_below(output_name, RUNTIME_ROOT, "output")
        if not source.is_file():
            raise FileNotFoundError(f"Asset source is not a regular file: {source_name}")
        if output in outputs:
            raise ValueError(f"Duplicate output: {output_name}")
        outputs.add(output)

        license_record = license_by_id.get(license_id)
        if not isinstance(license_record, dict):
            raise ValueError(f"Unknown licenseId {license_id!r} for {key}")
        for field in ("sourceUrl", "licenseName", "licenseEvidence"):
            value = license_record.get(field)
            if not isinstance(value, str) or not value.strip():
                raise ValueError(f"License {license_id!r} has no {field}")
        evidence = license_record["licenseEvidence"]
        if evidence.startswith("assets/"):
            evidence_path = resolve_below(evidence, SOURCE_ROOT, "license evidence")
            if not evidence_path.is_file():
                raise FileNotFoundError(f"Missing license evidence: {evidence}")

        validated.append((entry, source, output))

    return validated


def remove_stale_previous_outputs(current_outputs: set[Path]) -> None:
    if not RUNTIME_MANIFEST_PATH.is_file():
        return
    previous = load_json(RUNTIME_MANIFEST_PATH)
    previous_assets = previous.get("assets", [])
    if not isinstance(previous_assets, list):
        raise ValueError("Existing runtime manifest has an invalid assets list")
    for entry in previous_assets:
        if not isinstance(entry, dict) or not isinstance(entry.get("output"), str):
            raise ValueError("Existing runtime manifest contains an unsafe entry")
        old_output = manifest_url_to_path(entry["output"])
        if old_output not in current_outputs and old_output.is_file():
            old_output.unlink()


def write_runtime_manifest(
    entries: list[dict[str, Any]], license_manifest: dict[str, Any]
) -> None:
    runtime_entries = []
    for entry in entries:
        runtime_entry = dict(entry)
        runtime_entry["output"] = runtime_url(entry["output"])
        runtime_entries.append(runtime_entry)
    payload = {
        "schemaVersion": 1,
        "licensePolicy": {
            "commercialReleaseBlocked": bool(
                license_manifest.get("commercialReleaseBlocked", False)
            ),
            "reason": license_manifest.get("commercialReleaseBlockReason", ""),
        },
        "assets": runtime_entries,
    }
    RUNTIME_MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    temporary = RUNTIME_MANIFEST_PATH.with_suffix(".json.tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(payload, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    temporary.replace(RUNTIME_MANIFEST_PATH)


def write_credits() -> None:
    credits = """THIRD-PARTY ASSET CREDITS

Minifantasy Creatures and Minifantasy Forgotten Plains
Art by Krishna Palacio
https://krishna-palacio.itch.io/minifantasy-creatures
https://krishna-palacio.itch.io/minifantasy-forgotten-plains
The checked-in free versions are licensed for non-commercial projects only.
Commercial release is blocked until commercial-license purchase evidence is recorded.
Redistribution as standalone game assets is prohibited. Send the creator a project link upon completion.

16-Bit Fantasy & Adventure Music (2025)
Original music by Marllon Silva (xDeviruchi)
https://xdeviruchi.itch.io/16-bit-fantasy-adventure-music-pack
Commercial and non-commercial project use is permitted; standalone redistribution is prohibited.

RPG Essentials SFX - Free
Sound effects by Leohpaz
https://leohpaz.itch.io/rpg-essentials-sfx-free
Project use, including commercial use, is permitted. Credit is appreciated but not mandatory.
Do not sell or redistribute the sound pack.
"""
    CREDITS_PATH.parent.mkdir(parents=True, exist_ok=True)
    temporary = CREDITS_PATH.with_suffix(".txt.tmp")
    temporary.write_text(credits, encoding="utf-8", newline="\n")
    temporary.replace(CREDITS_PATH)


def main() -> int:
    curated = load_json(CURATED_PATH)
    licenses = load_json(LICENSES_PATH)
    validated = validate_manifests(curated, licenses)
    current_outputs = {output for _, _, output in validated}
    remove_stale_previous_outputs(current_outputs)

    total_bytes = 0
    for _, source, output in validated:
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, output)
        total_bytes += output.stat().st_size

    write_runtime_manifest([entry for entry, _, _ in validated], licenses)
    write_credits()
    print(f"Prepared {len(validated)} curated assets ({total_bytes} bytes).")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"Asset preparation failed: {error}", file=sys.stderr)
        raise SystemExit(1) from error
