"""
Арена боя Vitaria: гекс-плитки, препятствия, реквизит, ориентиры фона и рельеф.

Модули с ассетами (tiles, props, landmarks, fx) отдают ASSETS = [(категория, имя, подпись, builder)].
Композиция конкретной арены — meadow.py (террасы, вода, расстановка, камера).
Сборка сцены, превью и экспорт в Unity — build_arena.py рядом с build_vitaria.py.
"""
import importlib

MODULES = ["tiles", "props", "landmarks", "fx"]


def load(reload=False):
    subs = ["board", "terrain"] + MODULES + ["meadow"]
    mods = {}
    for n in subs:
        m = importlib.import_module(__name__ + "." + n)
        if reload:
            m = importlib.reload(m)
        mods[n] = m
    return mods


def asset_entries(reload=False):
    mods = load(reload)
    out = []
    for n in MODULES:
        out.extend(mods[n].ASSETS)
    return out
