"""Деревянная вышка: открытый решётчатый сруб, камень только под пятами опор."""
import math
from mathutils import Vector, Matrix
from build_vitaria import p_box, by_normal
from parts.common import hip_roof, railing, banner

NAME = "Bld_WatchTower"
TARGET = (2.6, 2.6, 6.0)   # w, d, h в метрах

# Опоры сходятся кверху: 0.82 -> 0.62 по радиусу от оси. Наклон всего 3.5 градуса,
# но силуэт перестаёт читаться как ящик, а связи ложатся по-настоящему наискось.
ZL0, ZL1 = 0.26, 3.56      # низ опоры утоплен в пяту, верх — в настил
R0, R1 = 0.82, 0.62
ZD = 3.58                  # центр настила; верх настила 3.66 — с него начинаются перила
OUT = 0.15                 # связи выносим наружу опор, иначе решётка тонет в стойках


def _leg_r(z):
    """Радиус оси опоры на высоте z — по нему выставляются все связи."""
    return R0 + (R1 - R0) * (z - ZL0) / (ZL1 - ZL0)


def _beam(a, p0, p1, s, col):
    """Брус между двумя точками. Направление задаём кватернионом, а не парой углов
    Эйлера: у раскоса наклон сразу по X и по Y, и Эйлер такой брус перекручивает."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    m = Matrix.Translation((p0 + p1) / 2) @ Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()
    a.add(p_box((s, s, d.length), bevel=min(0.03, s * 0.28), mat=m), col)


def build(a):
    # --- каменные пяты: единственный камень в ассете, низ чуть ниже нуля ---
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.46, 0.46, 0.34), loc=(sx * 0.72, sy * 0.72, 0.13), bevel=0.05),
                  by_normal("stone_light", "stone_mid", "stone_dark", 0.7))

    # --- четыре опоры ---
    for sx in (-1, 1):
        for sy in (-1, 1):
            _beam(a, (sx * R0, sy * R0, ZL0), (sx * R1, sy * R1, ZL1), 0.22,
                  by_normal("wood_light", "wood_mid", "wood_dark", 0.7))

    # --- решётка: крестовина плюс два ригеля на каждой из четырёх сторон ---
    # Это главный мотив ассета, поэтому брус толстый и вынесен на лицо опор.
    faces = ((0, -1), (0, 1), (-1, 0), (1, 0))
    for nx, ny in faces:
        pair = ((nx, -1), (nx, 1)) if nx else ((-1, ny), (1, ny))

        def node(corner, z, nx=nx, ny=ny):
            r = _leg_r(z)
            return (corner[0] * r + nx * OUT, corner[1] * r + ny * OUT, z)

        zb, zt = 0.42, 3.32
        _beam(a, node(pair[0], zb), node(pair[1], zt), 0.16, "wood_dark")
        _beam(a, node(pair[1], zb), node(pair[0], zt), 0.16, "wood_dark")
        for z in (1.70, 2.90):
            _beam(a, node(pair[0], z), node(pair[1], z), 0.15, "wood_dark")

    # --- настил со свесом и рама под ним ---
    for s in (-1, 1):
        a.add(p_box((2.00, 0.16, 0.16), loc=(0, s * 0.64, ZD - 0.16), bevel=0.03), "wood_dark")
        a.add(p_box((0.16, 2.00, 0.16), loc=(s * 0.64, 0, ZD - 0.16), bevel=0.03), "wood_dark")
    a.add(p_box((2.05, 2.05, 0.16), loc=(0, 0, ZD), bevel=0.035),
          by_normal("wood_pale", "wood_light", "wood_dark", 0.7))

    railing(a, w=1.95, d=1.95, z=ZD + 0.08, h=0.54)

    # --- стойки под шатёр ---
    # Веду их от настила, а не от 4.2: перила кончаются ровно на 4.2 и стойка,
    # начатая там, висела бы в воздухе над дощатым полом.
    RP = 0.95
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.16, 0.16, 1.28), loc=(sx * RP, sy * RP, 4.26), bevel=0.03),
                  by_normal("wood_light", "wood_mid", "wood_dark", 0.7))

    hip_roof(a, r=1.45, h=1.25, zbase=4.90, seg=4, spin=45)

    # --- лестница с земли на настил ---
    # common.ladder() наклон не держит: перекладины там крутятся вокруг своих центров
    # и при высоте 3.7 м уезжают из тетив на четверть метра, поэтому набираю вручную.
    B, T, LW = Vector((0, -1.58, -0.03)), Vector((0, -1.05, 3.72)), 0.50
    for sx in (-1, 1):
        off = Vector((sx * LW / 2, 0, 0))
        _beam(a, B + off, T + off, 0.09, "wood_mid")
    rungs = 10
    for i in range(rungs):
        p = B + (T - B) * ((i + 0.5) / rungs)
        _beam(a, p - Vector((LW / 2, 0, 0)), p + Vector((LW / 2, 0, 0)), 0.07, "wood_light")

    # --- хоругвь на фасадной (-Y) стойке; древко выключено, иначе пробьёт свес шатра ---
    # Полотно подвешено под самый свес и взято крупным: короткая хоругвь на этой
    # высоте читалась плоской карточкой, а не тканью.
    bx, byy, bz = 0.86, -1.04, 4.76
    banner(a, (bx, byy, bz), w=0.50, h=0.96, pole=False)
    # Знак на полотне — лилия из трёх лепестков и перевязи. Навершие над стеблем убрано:
    # вместе со стеблем оно вытягивалось в одно остриё и знак читался трезубцем.
    gy = byy - 0.05
    a.add(p_box((0.11, 0.04, 0.50), loc=(bx, gy, bz - 0.46), bevel=0.014), "gold")
    a.add(p_box((0.34, 0.04, 0.08), loc=(bx, gy, bz - 0.72), bevel=0.014), "gold")
    for sx in (-1, 1):
        a.add(p_box((0.09, 0.04, 0.32), loc=(bx + sx * 0.15, gy, bz - 0.48),
                    rot=(0, sx * 42, 0), bevel=0.014), "gold")
