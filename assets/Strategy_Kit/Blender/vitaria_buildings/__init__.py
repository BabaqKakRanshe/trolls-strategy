"""
Здания Vitaria в стиле набора (build_vitaria.py): шахта, рынок и 10 производственных построек.

Каждый модуль bld_*.py задаёт NAME (имя ассета и FBX), TITLE (подпись по-русски),
TARGET (ширина по X, глубина по Y, высота — в метрах) и build(a), где a — build_vitaria.Asset.

Добавить в .blend и выгрузить FBX:   add_buildings.py (рядом с build_vitaria.py)
Полная пересборка набора:            build_vitaria.py подхватывает этот список сам.
"""
import importlib

# ---------------------------------------------------------------------------------------------
# Контракт масштаба: модели зданий строятся в метрах кита и стоят в игре в ОДНОМ масштабе KIT_SCALE.
# Раньше каждое здание вписывали в свой след масштабом 0.40–0.71, и одинаковые двери и окна у соседей
# расходились в 1.8 раза. Теперь наоборот: модель вписана в след при KIT_SCALE (габарит не больше
# max_native), дверь кита 1.16–1.3 м = 0.58–0.65 м в игре (тролль 0.56 м проходит).
# ---------------------------------------------------------------------------------------------
KIT_SCALE = 0.5            # 2 м кита = 1 м игры: здания поля и фон колонии
MARGIN = 0.12              # зазор от модели до края следа, м игры
BEVEL_MIN = 0.16           # build_vitaria.BEVEL_MIN при сборке зданий: 8 см в игре = 4.5 px на 1080p, фаску не видно
TRI_BUDGET = {2: 3500, 3: 4500}      # треугольников на здание по ширине следа (было 4.0–5.7 тыс.)
FOOTPRINT = {                        # след в клетках (ширина по X, глубина по Y) — как BuildingDefinition
    "Bld_Armory": (2, 2), "Bld_Enchanter": (2, 2), "Bld_Forge": (2, 2), "Bld_LumberCamp": (2, 2),
    "Bld_ShieldWorkshop": (2, 2), "Bld_Tannery": (2, 2),
    "Bld_Barracks": (3, 3), "Bld_Farm": (3, 3), "Bld_Field": (3, 3), "Bld_LumberMill": (3, 3),
    "Bld_Mine": (3, 3), "Bld_Smeltery": (3, 3), "Bld_Warehouse": (3, 3),
    "Bld_Market": (3, 2),
}
# кровли по цепочкам производства (build_vitaria.THEMES); без темы — синяя черепица кита
ROOF_THEME = {
    "Bld_LumberCamp": "thatch", "Bld_LumberMill": "thatch", "Bld_ShieldWorkshop": "thatch",   # дерево
    "Bld_Mine": "tile", "Bld_Smeltery": "tile", "Bld_Forge": "tile",                          # железо
    "Bld_Barracks": "slate", "Bld_Armory": "slate", "Bld_Warehouse": "slate",                 # склады, войско
    # Field, Farm, Tannery — ферма и кожа: синяя; Market — свои тенты; Enchanter — кристаллы
}


def max_native(name):
    """Наибольший габарит модели (x, y) в метрах кита, при котором она влезает в свой след."""
    w, d = FOOTPRINT[name]
    return (w - 2 * MARGIN) / KIT_SCALE, (d - 2 * MARGIN) / KIT_SCALE


FIT_MIN = 0.82             # сильнее сжимать нельзя: круглое читается овалом — такое здание перекомпоновать
FIT = {}                   # имя -> (сжатие по X, по Y) последней сборки, для отчёта


def finish(V, me):
    """Общая доводка меша здания:
      1) габарит по центру следа — пивот в середине следа на земле, у префабов нет своих сдвигов модели;
      2) вписывание в след: если модель шире max_native, она сжимается по X/Y (высоты не трогаются,
         дверь остаётся той же высоты); сжатие не сильнее FIT_MIN, иначе здание надо перекомпоновать;
      3) кровля по теме цепочки и слот Vitaria_FX у светящихся граней.
    Возвращает (перекрашено граней, светящихся граней)."""
    if me.name in FOOTPRINT and me.vertices:
        xs = [v.co.x for v in me.vertices]
        ys = [v.co.y for v in me.vertices]
        dx, dy = -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2
        mw, md = max_native(me.name)
        fx = min(1.0, mw / (max(xs) - min(xs)))
        fy = min(1.0, md / (max(ys) - min(ys)))
        if min(fx, fy) < FIT_MIN:
            raise RuntimeError("%s шире следа: сжатие %.2f x %.2f < %.2f — перекомпоновать модель" % (me.name, fx, fy, FIT_MIN))
        for v in me.vertices:
            v.co.x = (v.co.x + dx) * fx
            v.co.y = (v.co.y + dy) * fy
        FIT[me.name] = (fx, fy)
        me.update()
    th = ROOF_THEME.get(me.name)
    return (V.recolor_mesh(me, th) if th else 0), V.split_emissive(me)


def check(me):
    """Строка отчёта: габарит против следа и треугольники против бюджета ('!!' — не влезает)."""
    xs, ys, zs = zip(*[tuple(v.co) for v in me.vertices])
    w, d = max(xs) - min(xs), max(ys) - min(ys)
    me.calc_loop_triangles()
    tris = len(me.loop_triangles)
    if me.name not in FOOTPRINT:
        return "%-20s %5d tris  %.2f x %.2f" % (me.name, tris, w, d)
    mw, md = max_native(me.name)
    budget = TRI_BUDGET[FOOTPRINT[me.name][0]]
    flags = ("" if w <= mw + 1e-3 else " !!W") + ("" if d <= md + 1e-3 else " !!D") + ("" if tris <= budget else " !!tris")
    fx, fy = FIT.get(me.name, (1.0, 1.0))
    squeeze = "" if fx > 0.999 and fy > 0.999 else "  сжато %.2f x %.2f" % (fx, fy)
    return "%-20s %5d/%d tris  %.2f x %.2f (max %.2f x %.2f)  в игре %.2f x %.2f%s%s" % (
        me.name, tris, budget, w, d, mw, md, w * KIT_SCALE, d * KIT_SCALE, squeeze, flags)

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


# здания поля, которые строятся модулями Ref_Buildings/Source/parts (свои модели казармы и склада)
REF_PLAYER = ("bld_barracks", "bld_warehouse")


def build_ref_player(V, build_mesh, mat):
    """Казарма и склад из Ref_Buildings по тому же контракту, что add_buildings: без невидимых
    фасок, по центру следа, вписаны в след, кровля по цепочке, слот Vitaria_FX.
    build_mesh — build_arena.build_mesh; parts должны быть в sys.path. Возвращает [(меш, отчёт)]."""
    out = []
    for mod in REF_PLAYER:
        m = importlib.reload(importlib.import_module("parts." + mod))
        V.BEVEL_MIN = BEVEL_MIN
        try:
            me = build_mesh(V, m.NAME, m.build, mat)
        finally:
            V.BEVEL_MIN = 0.0
        while len(me.materials) > 1:
            me.materials.pop()
        themed, hot = finish(V, me)
        out.append((me, check(me) + ("  roof %d" % themed if themed else "") + ("  fx %d" % hot if hot else "")))
    return out
