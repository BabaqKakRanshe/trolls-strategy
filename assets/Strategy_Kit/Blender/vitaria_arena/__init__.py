"""
Арена боя Vitaria: гекс-плитки, препятствия, реквизит, ориентиры фона и рельеф.

Модули с ассетами (tiles, props, landmarks, fx) отдают ASSETS = [(категория, имя, подпись, builder)].
Композиция конкретной арены — meadow.py / isle.py («Луг»), swamp.py («Болото»), snow.py («Снега»),
graveyard.py («Кладбище»), forest.py («Лес»), mountainpass.py («Горная застава»): террасы, вода, расстановка,
камера. Свои ассеты окружения — в <окружение>_assets.py (swamp_assets.py, snow_assets.py, graveyard_assets.py,
forest_assets.py, mountainpass_assets.py; категория Swamp, Snow, Graveyard, Forest, MountainPass). Сборка сцены,
превью и экспорт в Unity — build_arena.py рядом с build_vitaria.py (--env swamp, --env snow, --env graveyard,
--env forest, --env mountainpass).
"""
import importlib

MODULES = ["tiles", "props", "landmarks", "fx"]
# какая композиция «Луга» собирается: "isle" (луг парящим островом, вид колонии — арена игры с 01.10) или
# "meadow" (прежний луг над озером, build_arena.py --layout meadow). Обе — Arena_Meadow: поле, расстановка и
# камера одни, меняется окружение поля. Выгрузка любой из них заменяет Arena_Meadow в Unity.
LAYOUT = "isle"
# какое окружение собирается: "meadow" — «Луг» (композиция LAYOUT), иначе модуль с таким именем
# ("swamp" — «Болото», "snow" — «Снега», "graveyard" — «Кладбище», "forest" — «Лес», "mountainpass" — «Горная
# застава»). build_arena.py --env swamp
ENV = "meadow"
ENV_ASSETS = {"swamp": "swamp_assets", "snow": "snow_assets", "graveyard": "graveyard_assets",
              "forest": "forest_assets", "mountainpass": "mountainpass_assets"}


def load(reload=False, layout=None, env=None):
    layout = layout or LAYOUT
    env = env or ENV
    comp = layout if env == "meadow" else env
    subs = ["board", "terrain"] + MODULES + ["meadow"] + ([comp] if comp != "meadow" else [])
    if env in ENV_ASSETS:
        subs.insert(subs.index("meadow"), ENV_ASSETS[env])
    mods = {}
    for n in subs:
        m = importlib.import_module(__name__ + "." + n)
        if reload:
            m = importlib.reload(m)
        mods[n] = m
    mods["meadow"] = mods[comp]
    return mods


def asset_entries(reload=False):
    mods = load(reload)
    out = []
    for n in MODULES:
        out.extend(mods[n].ASSETS)
    return out


def env_asset_entries(reload=False, env=None):
    """Ассеты окружения (своя категория, коллекция Kit_<категория>); у «Луга» их нет."""
    env = env or ENV
    if env not in ENV_ASSETS:
        return []
    m = importlib.import_module(__name__ + "." + ENV_ASSETS[env])
    if reload:
        m = importlib.reload(m)
    return list(m.ASSETS)
