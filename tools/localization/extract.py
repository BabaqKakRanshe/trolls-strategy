"""Collects the game's Russian player-facing strings into keys.json: {key: [files]}.

Keys are the Russian source texts. An interpolated C# string becomes a template: each {expression} hole is
numbered {0}, {1}... in order. Adjacent literals joined with + become one key. Skipped: comments, Debug.Log*,
attributes ([Tooltip], [Header], [MenuItem]), the bots, tests and editor tools other than the content setups.
"""
import json, os, re, sys
from pathlib import Path

G = Path(__file__).resolve().parents[2] / "unity/TrollStategy/Assets/Game"
OUT = Path(os.path.dirname(os.path.abspath(__file__))) / "keys.json"
CYR = re.compile(r"[А-Яа-яЁё]")

CODE_DIRS = [G / "Runtime"]
CONTENT_SETUPS = ["ProductionContentSetup.cs", "ProgressionContentSetup.cs", "CreatureSetup.cs", "ArenaContentSetup.cs"]


def lex_strings(src):
    """Yields (kind, text, start, end) for every string literal; kind is 'plain', 'verbatim', 'interp'."""
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if src.startswith("/*", i):
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
            continue
        if c == "'":
            # char literal
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            i = j + 1
            continue
        prefix = None
        for p in ('$@"', '@$"', '$"', '@"', '"'):
            if src.startswith(p, i):
                prefix = p
                break
        if prefix is None:
            i += 1
            continue
        start = i
        i += len(prefix)
        verbatim = "@" in prefix
        interp = "$" in prefix
        buf, holes, depth = [], 0, 0
        while i < n:
            ch = src[i]
            if interp and ch == "{":
                if src.startswith("{{", i):
                    buf.append("{")
                    i += 2
                    continue
                # a hole: skip to its matching brace, minding nested strings and braces
                j, d = i + 1, 1
                while j < n and d > 0:
                    if src[j] == "{":
                        d += 1
                    elif src[j] == "}":
                        d -= 1
                    elif src[j] == '"':
                        k = j + 1
                        while k < n and src[k] != '"':
                            k += 2 if src[k] == "\\" else 1
                        j = k
                    j += 1
                buf.append("{" + str(holes) + "}")
                holes += 1
                i = j
                continue
            if interp and src.startswith("}}", i):
                buf.append("}")
                i += 2
                continue
            if verbatim:
                if ch == '"':
                    if src.startswith('""', i):
                        buf.append('"')
                        i += 2
                        continue
                    i += 1
                    break
                buf.append(ch)
                i += 1
                continue
            if ch == "\\":
                nxt = src[i + 1] if i + 1 < n else ""
                buf.append({"n": "\n", "t": "\t", '"': '"', "\\": "\\", "r": "\r"}.get(nxt, nxt))
                i += 2
                continue
            if ch == '"':
                i += 1
                break
            buf.append(ch)
            i += 1
        yield ("interp" if interp else "plain"), "".join(buf), start, i, holes


def renumber(parts):
    """Joins literal parts, renumbering each part's {0}.. holes so the whole string counts from 0."""
    out, offset = [], 0
    for text, holes in parts:
        if holes:
            text = re.sub(r"\{(\d+)\}", lambda m: "{" + str(int(m.group(1)) + offset) + "}", text)
        out.append(text)
        offset += holes
    return "".join(out)


def strings_of(path):
    src = path.read_text(encoding="utf-8-sig")
    items = list(lex_strings(src))
    keys = []
    k = 0
    while k < len(items):
        group = [items[k]]
        # literals joined with + across whitespace become one string
        while k + 1 < len(items) and re.fullmatch(r"\s*\+\s*", src[group[-1][3]:items[k + 1][2]]):
            k += 1
            group.append(items[k])
        k += 1
        line_start = src.rfind("\n", 0, group[0][2]) + 1
        line = src[line_start:group[0][2]]
        if re.search(r"Debug\.Log|\[(Tooltip|Header|MenuItem|InspectorName)|throw new|Exception\(", line):
            continue
        text = renumber([(g[1], g[4]) for g in group])
        if CYR.search(text):
            keys.append(text)
    return keys


def uxml_strings(path):
    src = path.read_text(encoding="utf-8-sig")
    out = []
    for m in re.finditer(r'\btext="([^"]*)"', src):
        t = m.group(1).replace("&quot;", '"').replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
        if CYR.search(t):
            out.append(t)
    return out


def asset_strings(path):
    src = path.read_text(encoding="utf-8-sig")
    out = []
    for m in re.finditer(r"^\s*(_displayName|_description|_title|_name): (\"(?:[^\"\\]|\\.)*\"|.+)$", src, re.M):
        raw = m.group(2).strip()
        if raw.startswith('"'):
            try:
                text = json.loads(raw)
            except Exception:
                text = raw.strip('"')
        else:
            text = raw
        if CYR.search(text):
            out.append(text)
    return out


def main():
    keys = {}

    def add(text, where):
        text = text.strip("\n")
        if not text.strip():
            return
        keys.setdefault(text, [])
        if where not in keys[text]:
            keys[text].append(where)

    for d in CODE_DIRS:
        for p in sorted(d.rglob("*.cs")):
            # the language names and the patterns of the translator itself are not texts to translate
            if p.name == "Localization.cs":
                continue
            for s in strings_of(p):
                add(s, p.relative_to(G).as_posix())
    for name in CONTENT_SETUPS:
        p = G / "Editor/Setup" / name
        for s in strings_of(p):
            add(s, p.relative_to(G).as_posix())
    for p in sorted((G / "UI/Uxml").rglob("*.uxml")):
        for s in uxml_strings(p):
            add(s, p.relative_to(G).as_posix())
    for p in sorted((G / "Content/Definitions").glob("*.asset")):
        for s in asset_strings(p):
            add(s, p.relative_to(G).as_posix())
    OUT.write_text(json.dumps(keys, ensure_ascii=False, indent=1), encoding="utf-8")
    templates = sum(1 for k in keys if re.search(r"\{\d+\}", k))
    print(f"{len(keys)} keys ({templates} templates) -> {OUT}")


if __name__ == "__main__":
    main()
