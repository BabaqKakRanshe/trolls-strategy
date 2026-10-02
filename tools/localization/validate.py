"""Checks a translation file against keys.json.

    python validate.py <code>        checks translations/<code>.json

Every key must be translated; each translation keeps the key's {N} placeholders (same set), its line breaks,
and its hotkey tokens (F8, Esc, Enter and single Latin capitals in parentheses like (E)).
"""
import json, re, sys
from pathlib import Path

HERE = Path(__file__).parent
keys = json.loads((HERE / "keys.json").read_text(encoding="utf-8"))
code = sys.argv[1]
path = HERE / "translations" / f"{code}.json"
data = json.loads(path.read_text(encoding="utf-8"))
problems = []
for key in keys:
    if key not in data:
        problems.append(f"missing: {key[:80]!r}")
        continue
    value = data[key]
    if not isinstance(value, str) or not value.strip():
        problems.append(f"empty: {key[:80]!r}")
        continue
    if sorted(re.findall(r"\{\d+\}", key)) != sorted(re.findall(r"\{\d+\}", value)):
        problems.append(f"placeholders differ: {key[:60]!r} -> {value[:60]!r}")
    if key.count("\n") != value.count("\n"):
        problems.append(f"line breaks differ: {key[:60]!r}")
    for token in re.findall(r"\bF8\b|\bEsc\b|\bEnter\b|\([A-Z]\)", key):
        if token not in value:
            problems.append(f"hotkey {token} lost: {key[:60]!r}")
extra = [k for k in data if k not in keys]
print(f"{code}: {len(keys)} keys, {len(problems)} problems, {len(extra)} extra")
for p in problems[:40]:
    print("  " + p)
sys.exit(1 if problems else 0)
