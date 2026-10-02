"""Builds the game's translations from tools/localization/translations/<code>.json.

    python tools/localization/build.py

1. Writes unity/TrollStategy/Assets/Game/UI/Localization/<code>.json in the runtime format
   {"entries": [{"k": "<Russian>", "v": "<translation>"}]} (read by Presentation/Localization.cs).
2. Cuts the Noto Sans fonts for the scripts the UI font lacks (Japanese, Korean, Chinese, Devanagari) down to the
   characters the translations use, into Assets/Game/UI/Fonts/Noto (with the OFL licence). The full fonts are
   downloaded once into tools/localization/.fonts (ignored by git).
Then run the Unity menu TrollStrategy/Dev/Setup Localization Fonts (LocalizationSetup) for the font assets.
"""
import json, re, shutil, sys, urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
GAME = REPO / "unity/TrollStategy/Assets/Game"
RESOURCES = GAME / "UI/Localization"
FONTS = GAME / "UI/Fonts/Noto"
CACHE = HERE / ".fonts"
NOTO = "https://raw.githubusercontent.com/notofonts"
SOURCES = {
    "ja": ("NotoSansJP-Regular.otf", f"{NOTO}/noto-cjk/main/Sans/SubsetOTF/JP/NotoSansJP-Regular.otf"),
    "ko": ("NotoSansKR-Regular.otf", f"{NOTO}/noto-cjk/main/Sans/SubsetOTF/KR/NotoSansKR-Regular.otf"),
    "zh": ("NotoSansSC-Regular.otf", f"{NOTO}/noto-cjk/main/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf"),
    "hi": ("NotoSansDevanagari-Regular.ttf",
           f"{NOTO}/notofonts.github.io/main/fonts/NotoSansDevanagari/hinted/ttf/NotoSansDevanagari-Regular.ttf"),
}
LICENCE = f"{NOTO}/noto-cjk/main/Sans/LICENSE"


def main():
    keys = json.loads((HERE / "keys.json").read_text(encoding="utf-8"))
    RESOURCES.mkdir(parents=True, exist_ok=True)
    texts = {}
    for path in sorted((HERE / "translations").glob("*.json")):
        code = path.stem
        data = json.loads(path.read_text(encoding="utf-8"))
        entries = [{"k": k, "v": data[k]} for k in keys if isinstance(data.get(k), str) and data[k].strip()]
        (RESOURCES / f"{code}.json").write_text(json.dumps({"entries": entries}, ensure_ascii=False, indent=0),
                                                encoding="utf-8")
        texts[code] = "".join(e["v"] for e in entries)
    # the language page shows every language under its own name, whatever language the interface is in
    source = (GAME / "Runtime/Presentation/Localization.cs").read_text(encoding="utf-8")
    listed = source[source.index("Languages = new()"):]
    for code, name in re.findall(r'\("(\w+)", "([^"]+)"\)', listed[:listed.index("};")]):
        if code in texts:
            texts[code] += name
        print(f"{code}: {len(entries)} of {len(keys)} texts")

    try:
        from fontTools import subset
    except ImportError:
        sys.exit("pip install fonttools  (needed to cut the fonts)")
    CACHE.mkdir(exist_ok=True)
    (CACHE / ".gitignore").write_text("*\n", encoding="utf-8")
    FONTS.mkdir(parents=True, exist_ok=True)
    for code, (name, url) in SOURCES.items():
        if code not in texts:
            continue
        source = CACHE / name
        if not source.exists():
            print("downloading", name)
            urllib.request.urlretrieve(url, source)
        chars = set(texts[code]) | set(map(chr, range(0x20, 0x7F)))
        target = FONTS / (Path(name).stem + "-Game" + Path(name).suffix)
        options = subset.Options()
        options.layout_features = ["*"]
        options.name_IDs = ["*"]
        options.notdef_outline = True
        font = subset.load_font(str(source), options)
        subsetter = subset.Subsetter(options)
        subsetter.populate(text="".join(sorted(chars)))
        subsetter.subset(font)
        subset.save_font(font, str(target), options)
        print(f"{target.name}: {len(chars)} characters, {target.stat().st_size // 1024} KB")
    licence = FONTS / "OFL.txt"
    if not licence.exists():
        urllib.request.urlretrieve(LICENCE, licence)


if __name__ == "__main__":
    main()
