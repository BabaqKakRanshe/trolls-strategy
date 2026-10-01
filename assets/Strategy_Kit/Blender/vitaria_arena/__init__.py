"""
Арена боя Vitaria: гекс-плитки, препятствия, реквизит, ориентиры фона и рельеф.

Модули с ассетами (tiles, props, landmarks, fx) отдают ASSETS = [(категория, имя, подпись, builder)].
Композиция конкретной арены — meadow.py (террасы, вода, расстановка, камера).
Сборка сцены, превью и экспорт в Unity — build_arena.py рядом с build_vitaria.py.
"""
import importlib

MODULES = ["tiles", "props", "landmarks", "fx"]
# какая композиция арены собирается: "isle" (луг парящим островом, вид колонии — арена игры с 01.10) или
# "meadow" (прежний луг над озером, build_arena.py --layout meadow). Обе — Arena_Meadow: поле, расстановка и
# камера одни, меняется окружение поля. Выгрузка любой из них заменяет Arena_Meadow в Unity.
LAYOUT = "isle"


def load(reload=False, layout=None):
    layout = layout or LAYOUT
    subs = ["board", "terrain"] + MODULES + ["meadow"] + ([layout] if layout != "meadow" else [])
    mods = {}
    for n in subs:
        m = importlib.import_module(__name__ + "." + n)
        if reload:
            m = importlib.reload(m)
        mods[n] = m
    mods["meadow"] = mods[layout]
    return mods


def asset_entries(reload=False):
    mods = load(reload)
    out = []
    for n in MODULES:
        out.extend(mods[n].ASSETS)
    return out
