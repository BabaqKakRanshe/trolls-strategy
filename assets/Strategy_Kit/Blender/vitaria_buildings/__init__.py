"""
Здания Vitaria в стиле набора (build_vitaria.py): шахта, рынок и 10 производственных построек.

Каждый модуль bld_*.py задаёт NAME (имя ассета и FBX), TITLE (подпись по-русски),
TARGET (ширина по X, глубина по Y, высота — в метрах) и build(a), где a — build_vitaria.Asset.

Добавить в .blend и выгрузить FBX:   add_buildings.py (рядом с build_vitaria.py)
Полная пересборка набора:            build_vitaria.py подхватывает этот список сам.
"""
import importlib

# порядок = порядок в витрине набора
MODULES = [
    "bld_mine", "bld_market",
    "bld_smeltery", "bld_forge", "bld_armory",
    "bld_field", "bld_farm", "bld_tannery",
    "bld_lumbercamp", "bld_lumbermill", "bld_shieldworkshop",
    "bld_enchanter",
]


def load(names=None, reload=False):
    """Импортировать модули зданий. reload=True — перечитать с диска (для итераций в GUI)."""
    common = importlib.import_module(__name__ + ".common")
    if reload:
        importlib.reload(common)
    mods = []
    for n in (names or MODULES):
        m = importlib.import_module(__name__ + "." + n)
        if reload:
            m = importlib.reload(m)
        mods.append(m)
    return mods


def kit_entries(reload=False):
    """Строки для KIT в build_vitaria.py: (категория, имя, билдер)."""
    return [("Buildings", m.NAME, m.build) for m in load(reload=reload)]
