"""
Ферма: синий амбар с ломаной (gambrel) кровлей сзади слева и загон спереди справа.

Главный приём — животные как герои: пять крупных коренастых фигур разного цвета
(чёрно-белая корова, розовая свинья, овца с чёрной мордой, две белые курицы), каждая
в своей раме и повёрнута к камере вполоборота. Амбар — классический: кремовая обвязка
и X-раскосы ворот на синих досках, круглый люк сеновала с сеном и круглой створкой
(отсылка к Ширу), балка подъёмника и купол-вентиляция с петушком на коньке.
"""
import math, random
from mathutils import Vector
from build_vitaria import p_box, p_ico, by_normal
from vitaria_buildings.common import Frame, fence_line, hay_bale
from vitaria_buildings.levels import pennant, gold_ridge, wall_banner, wall_lantern, finial

NAME = "Bld_Farm"
TITLE = "Ферма"
TARGET = (5.0, 4.4, 3.9)

BX, BY = -1.12, 0.92          # центр амбара
W, D = 2.40, 2.30             # коробка амбара
F = 0.22                      # верх цоколя
ZE = 1.55                     # линия свеса: здесь нижний скат опирается на стену
A1, A2 = 62.0, 27.0           # крутой нижний и пологий верхний скаты
BK = 0.74                     # излом кровли от оси
OX, OY = 0.14, 0.18           # свесы кровли
DL = 0.09                     # корпус ниже линии кровли: подшивка на 8 см ниже, зазор 1 см
WALL = "roof_light"           # стены светлее кровли: синие стены под синей кровлей иначе сливаются
TRIM = "cream"
PEN = (0.22, 2.42, -2.00, 0.90)   # загон: x0, x1, y0, y1
ZG = 0.03                     # верх утоптанной земли загона
EAR = by_normal("wheat_light", "wheat", "wheat", 0.2)


def _roof_geom():
    hw = W / 2
    t1, t2 = math.tan(math.radians(A1)), math.tan(math.radians(A2))
    zb = ZE + 0.08 + (hw - BK) * t1
    zr = zb + BK * t2
    return hw, t1, t2, zb, zr


def _wall_top(x):
    """Верх корпуса над точкой x фронтона (по профилю кровли минус DL)."""
    hw, t1, t2, zb, zr = _roof_geom()
    x = abs(x)
    if x <= BK:
        return zr - x * t2 - DL / math.cos(math.radians(A2))
    return ZE + 0.08 + (hw - x) * t1 - DL / math.cos(math.radians(A1))


def _gambrel(a, fr):
    """Ломаная кровля: на скат два пояса — крутой низ roof_dark и пологий верх roof
    (верх светлее, как у всего кита). Верхний пояс свисает на 11 см за излом — капельник;
    кремовые причелины обводят ломаный силуэт фронтона, по нему амбар и узнаётся."""
    hw, t1, t2, zb, zr = _roof_geom()
    Ly = D + 2 * OY
    for sx in (-1, 1):
        for ang, x0, z0, x1, col, ext0, ext1 in ((A2, 0.0, zr, BK, "roof", 0.05, 0.11),
                                                 (A1, BK, zb, hw + OX, "roof_dark", 0.03, 0.03)):
            r = math.radians(ang)
            dn = Vector((sx * math.cos(r), 0, -math.sin(r)))
            nr = Vector((sx * math.sin(r), 0, math.cos(r)))
            p0 = Vector((sx * x0, 0, z0))
            L = (x1 - x0) / math.cos(r)
            mid = p0 + dn * (L / 2 + (ext1 - ext0) / 2)
            rot = (0, sx * ang, 0)
            fr.box(a, (L + ext0 + ext1, Ly, 0.17), mid + nr * 0.06, rot, col=col, bevel=0.05)
            if col == "roof_dark":        # подшивка свеса: тёмная кромка под крутым скатом
                fr.box(a, (L, Ly, 0.10), p0 + dn * (L / 2) - nr * 0.03, rot, col="wood_dark", bevel=0.03)
            for sy in (-1, 1):
                c = p0 + dn * (L / 2) + nr * 0.05
                fr.box(a, (L + 0.10, 0.12, 0.24), (c.x, sy * (Ly / 2 + 0.04), c.z), rot, col=TRIM,
                       bevel=0.035)
    fr.box(a, (0.30, Ly + 0.14, 0.30), (0, 0, zr + 0.08), (0, 45, 0), col="roof_dark", bevel=0.05)
    return zr


def _body(a, fr):
    """Корпус одной призмой по профилю кровли, цоколь, кремовые угловые доски."""
    hw, t1, t2, zb, zr = _roof_geom()
    c1, c2 = math.cos(math.radians(A1)), math.cos(math.radians(A2))
    xs = BK - DL * (1 / c2 - 1 / c1) / (t1 + t2)      # излом корпуса: пересечение смещённых скатов
    pts = [(-hw, F - 0.03), (hw, F - 0.03), (hw, _wall_top(hw)), (xs, _wall_top(xs)), (0, _wall_top(0)),
           (-xs, _wall_top(xs)), (-hw, _wall_top(hw))]
    fr.prism(a, pts, D, col=WALL)
    fr.box(a, (W + 0.24, D + 0.24, F + 0.14), (0, 0, (F - 0.14) / 2),
           col=by_normal("stone_mid", "stone_dark", "stone_dark", 0.8), bevel=0.06)
    # угловые доски; верх ниже подшивки — иначе угол доски протыкает свес
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.box(a, (0.15, 0.15, ZE - 0.24 - F), (sx * hw, sy * D / 2, (F + ZE - 0.24) / 2), col=TRIM,
                   bevel=0.03)


def _battens(a, fr):
    """Нащельники: узкие тёмные рейки по синей стене — «дощатая» обшивка амбара.
    Фаски нет: на рейке 7 см она не видна, а стоила бы 400 треугольников."""
    hw = W / 2

    def bat(x, y, z0, z1, along_x=True):
        size = (0.07, 0.04, z1 - z0) if along_x else (0.04, 0.07, z1 - z0)
        fr.box(a, size, (x, y, (z0 + z1) / 2), col="roof", bevel=0.0)

    yf, yb = -D / 2 - 0.02, D / 2 + 0.02
    for sx in (-1, 1):
        bat(sx * 0.92, yf, F, _wall_top(0.96) - 0.04)
        bat(sx * 0.52, yf, 1.72, _wall_top(0.56) - 0.04)       # над притолокой ворот, мимо люка
        for x in (0.45, 0.90):
            bat(sx * x, yb, F, _wall_top(x + 0.035) - 0.04)
    ztop = ZE - 0.26
    for y in (0.37, 0.76):
        bat(hw + 0.02, y, F, ztop, along_x=False)
    for y in (-0.75, -0.35, 0.80):
        bat(-hw - 0.02, y, F, ztop, along_x=False)


def _doors(a, fr, y, z0, w=1.24, h=1.30):
    """Ворота: синее полотно, кремовые косяки, притолока, средник и X-раскос в каждой створке.
    Раскосы одной створки разнесены на 8 мм по глубине — пересечение без общей плоскости."""
    fr.box(a, (w, 0.08, h), (0, y - 0.03, z0 + h / 2), col="roof", bevel=0.02)
    for sx in (-1, 1):
        fr.box(a, (0.13, 0.13, h + 0.02), (sx * (w / 2 + 0.04), y - 0.06, z0 + h / 2), col=TRIM, bevel=0.03)
    fr.box(a, (w + 0.40, 0.15, 0.15), (0, y - 0.07, z0 + h + 0.06), col=TRIM, bevel=0.035)
    fr.box(a, (0.09, 0.10, h - 0.02), (0, y - 0.07, z0 + h / 2), col=TRIM, bevel=0.02)
    fr.box(a, (w, 0.10, 0.10), (0, y - 0.07, z0 + 0.07), col=TRIM, bevel=0.02)
    dx, dz = w / 2 - 0.16, h - 0.22
    ang = math.degrees(math.atan2(dx, dz))
    Ld = math.hypot(dx, dz) + 0.06
    for sx in (-1, 1):
        for k, s2 in enumerate((1, -1)):
            fr.box(a, (0.08, 0.05, Ld), (sx * w / 4, y - 0.075 - 0.008 * k, z0 + 0.06 + h / 2),
                   rot=(0, s2 * ang, 0), col=TRIM, bevel=0.0)
        fr.box(a, (0.07, 0.07, 0.16), (sx * 0.12, y - 0.14, z0 + h * 0.52), col="iron_dark", bevel=0.0)
    # пандус к воротам: пол амбара выше земли на цоколь, верх пандуса приходит в кромку цоколя
    run = 0.50
    ang = math.degrees(math.atan2(F, run))
    fr.box(a, (w + 0.2, math.hypot(F, run) + 0.04, 0.10), (0, y - 0.12 - run / 2, F / 2 - 0.05),
           rot=(ang, 0, 0), col=by_normal("wood_light", "wood_mid", "wood_dark", 0.8), bevel=0.03)


def _hayloft(a, fr, y, z):
    """Круглый люк сеновала: кремовое кольцо, тёмный проём, из него вываливается сено.
    Круглая створка распахнута почти к стене (160°): открытая на 120° стояла к камере ребром."""
    fr.cyl(a, 0.36, 0.36, 0.10, 14, loc=(0, y + 0.02, z), rot=(90, 0, 0), col=TRIM)
    fr.cyl(a, 0.26, 0.26, 0.10, 14, loc=(0, y + 0.00, z), rot=(90, 0, 0), col="black")
    fr.ico(a, 0.24, loc=(0.0, y - 0.10, z - 0.20), scl=(1.15, 0.55, 0.62), col=EAR, cut=-0.1)
    for x, rz in ((-0.14, 25), (0.12, -30)):
        fr.box(a, (0.06, 0.22, 0.05), (x, y - 0.20, z - 0.16), rot=(-35, 0, rz), col="wheat_light", bevel=0.0)
    s = fr.sub((0.33, y - 0.10, z), rz=-20)
    s.cyl(a, 0.24, 0.24, 0.05, 12, loc=(0.24, 0.0, 0.0), rot=(90, 0, 0), col=TRIM)
    s.cyl(a, 0.18, 0.18, 0.05, 12, loc=(0.24, -0.015, 0.0), rot=(90, 0, 0), col="roof")


def _hoist(a, fr, y, z):
    """Балка подъёмника с блоком. Вылет 0.4 м: длиннее — конец балки ложится на люк в кадре."""
    fr.box(a, (0.13, 0.55, 0.13), (0, y - 0.125, z), col="wood_dark", bevel=0.03)
    fr.box(a, (0.10, 0.12, 0.12), (0, y - 0.34, z - 0.11), col="iron_dark", bevel=0.0)


def _cupola(a, fr, zr):
    """Вентиляционный купол на коньке с флюгером-петушком: он даёт силуэту амбара вершину."""
    fr.box(a, (0.54, 0.54, 0.40), (0, 0, zr + 0.11), col=TRIM, bevel=0.04)
    for rz in (0, 90):
        fr.box(a, (0.30, 0.58, 0.20), (0, 0, zr + 0.12), rot=(0, 0, rz), col="black", bevel=0.0)
    fr.box(a, (0.70, 0.70, 0.07), (0, 0, zr + 0.345), col="roof_dark", bevel=0.025)
    fr.cyl(a, 0.46, 0.0, 0.30, 4, loc=(0, 0, zr + 0.375), spin=45, col="roof")
    fr.box(a, (0.04, 0.04, 0.20), (0, 0, zr + 0.74), col="iron_dark", bevel=0.0)
    fr.ico(a, 0.05, loc=(0, 0, zr + 0.67), col="gold")
    rooster = [(-0.10, 0.0), (0.06, 0.0), (0.09, 0.07), (0.10, 0.14), (0.15, 0.15), (0.10, 0.19),
               (0.07, 0.25), (0.04, 0.20), (0.04, 0.13), (-0.03, 0.09), (-0.08, 0.14), (-0.13, 0.26),
               (-0.14, 0.12)]
    fr.prism(a, [(x * 0.85, z * 0.85) for x, z in rooster], 0.04, loc=(0, 0, zr + 0.82), col="gold")


def _side_door(a, fr, x, y):
    """Полудверь в загон на правой стене: нижняя створка закрыта, верхняя распахнута (чёрный проём)."""
    h, w = 1.02, 0.66
    fr.box(a, (0.10, w, h * 0.5), (x + 0.03, y, F + h * 0.75), col="black", bevel=0.0)
    fr.box(a, (0.08, w, h * 0.5), (x + 0.03, y, F + h * 0.25), col="roof", bevel=0.02)
    for sy in (-1, 1):
        fr.box(a, (0.12, 0.12, h + 0.04), (x + 0.05, y + sy * (w / 2 + 0.05), F + h / 2), col=TRIM, bevel=0.03)
    fr.box(a, (0.14, w + 0.34, 0.13), (x + 0.06, y, F + h + 0.06), col=TRIM, bevel=0.03)
    fr.box(a, (0.12, w + 0.02, 0.08), (x + 0.07, y, F + h * 0.5), col=TRIM, bevel=0.02)


def _window_x(a, fr, x, y, z, sx):
    """Окно на боковой стене: кремовая рама, тёмное стекло, кремовый крест."""
    fr.box(a, (0.12, 0.56, 0.50), (x + sx * 0.02, y, z), col=TRIM, bevel=0.03)
    fr.box(a, (0.06, 0.40, 0.34), (x + sx * 0.07, y, z), col="glass", bevel=0.0)
    fr.box(a, (0.05, 0.40, 0.06), (x + sx * 0.10, y, z), col=TRIM, bevel=0.0)
    fr.box(a, (0.05, 0.06, 0.34), (x + sx * 0.105, y, z), col=TRIM, bevel=0.0)


def _barn(a):
    fr = Frame((BX, BY, 0.0))
    _body(a, fr)
    zr = _gambrel(a, fr)
    _battens(a, fr)
    yF = -D / 2
    _doors(a, fr, yF, F)
    _hayloft(a, fr, yF, 2.14)
    _hoist(a, fr, yF, 2.76)
    _cupola(a, fr.sub((0, 0.15, 0)), zr)
    _side_door(a, fr, W / 2, -0.45)
    _window_x(a, fr, -W / 2, 0.25, 1.0, -1)
    # задний фронтон: продух, чтобы тыл не был глухим
    fr.box(a, (0.46, 0.10, 0.40), (0, D / 2 + 0.03, 2.14), col=TRIM, bevel=0.03)
    fr.box(a, (0.32, 0.06, 0.26), (0, D / 2 + 0.07, 2.14), col="black", bevel=0.0)


# =========================================================================================
# Животные: локальная рама — вперёд +X, влево +Y, начало на земле под серединой туловища.
# Ноги утоплены на 1 см: торец ноги не ложится в плоскость земли.
# =========================================================================================
def _cow(a, fr, rng):
    """Корова: туловище-брус с крупной фаской, большая голова с розовой мордой, рога,
    колокольчик на синем ошейнике. Пятна — сплюснутые икосферы, на 3–4 см выступающие из
    туловища: неровный контур пятна получается сам, без булевых."""
    for x in (-0.32, 0.30):
        for y in (-0.17, 0.17):
            fr.taper(a, (0.13, 0.13), (0.16, 0.16), 0.49, loc=(x, y, -0.01), col="cream")
            fr.box(a, (0.16, 0.16, 0.09), (x, y, 0.035), col="coal", bevel=0.0)
    fr.box(a, (1.00, 0.58, 0.50), (0, 0, 0.60), col="cream", bevel=0.12)
    for loc, r, scl in (((-0.18, 0.27, 0.60), 0.19, (1.3, 0.3, 1.0)), ((0.14, -0.27, 0.64), 0.16, (1.2, 0.3, 1.0)),
                        ((-0.06, 0.04, 0.83), 0.20, (1.2, 1.0, 0.3)), ((-0.48, -0.10, 0.66), 0.14, (0.3, 1.0, 1.0))):
        fr.ico(a, r, loc=loc, scl=scl, col="coal", jitter=0.15, rng=rng)
    fr.box(a, (0.18, 0.20, 0.10), (-0.12, 0, 0.33), col="pig", bevel=0.0)           # вымя
    fr.box(a, (0.05, 0.05, 0.34), (-0.52, 0, 0.62), rot=(0, 12, 0), col="cream", bevel=0.0)
    fr.box(a, (0.08, 0.08, 0.10), (-0.555, 0, 0.44), col="coal", bevel=0.0)
    # голова
    fr.box(a, (0.36, 0.40, 0.38), (0.60, 0, 0.80), col="cream", bevel=0.08)
    fr.box(a, (0.16, 0.38, 0.22), (0.80, 0, 0.70), col="pig", bevel=0.06)
    for sy in (-1, 1):
        fr.box(a, (0.02, 0.05, 0.05), (0.885, sy * 0.08, 0.72), col="coal", bevel=0.0)
        fr.box(a, (0.02, 0.06, 0.07), (0.787, sy * 0.11, 0.88), col="coal", bevel=0.0)
        fr.cyl(a, 0.045, 0.02, 0.15, 6, loc=(0.56, sy * 0.14, 0.96), rot=(-sy * 25, 0, 0), col="wood_pale")
        fr.box(a, (0.08, 0.17, 0.08), (0.55, sy * 0.26, 0.86), rot=(-sy * 15, 0, 0), col="coal", bevel=0.0)
    # синий ошейник с колокольчиком — цвет связывает корову с амбаром
    fr.box(a, (0.08, 0.44, 0.44), (0.46, 0, 0.74), col="roof", bevel=0.03)
    fr.cyl(a, 0.065, 0.045, 0.11, 8, loc=(0.53, 0, 0.42), col="gold")


def _pig(a, fr):
    """Свинья: розовый брус с большой фаской, пятачок-цилиндр, уши свисают вперёд."""
    for x in (-0.24, 0.22):
        for y in (-0.14, 0.14):
            fr.taper(a, (0.11, 0.11), (0.13, 0.13), 0.33, loc=(x, y, -0.01), col="pig_dark")
    fr.box(a, (0.80, 0.52, 0.46), (0, 0, 0.44), col="pig", bevel=0.14)
    fr.box(a, (0.34, 0.44, 0.38), (0.40, 0, 0.52), col="pig", bevel=0.10)
    fr.cyl(a, 0.11, 0.11, 0.09, 8, loc=(0.55, 0, 0.47), rot=(0, 90, 0), col="pig_dark")
    for sy in (-1, 1):
        fr.box(a, (0.02, 0.035, 0.05), (0.645, sy * 0.04, 0.47), col="coal", bevel=0.0)
        fr.box(a, (0.02, 0.05, 0.06), (0.575, sy * 0.12, 0.60), col="coal", bevel=0.0)
        fr.taper(a, (0.04, 0.13), (0.02, 0.03), 0.15, loc=(0.36, sy * 0.13, 0.66), rot=(-sy * 18, 42, 0),
                 col="pig_dark")
    fr.cyl(a, 0.03, 0.01, 0.12, 4, loc=(-0.40, 0, 0.56), rot=(0, -60, 0), col="pig_dark")


def _sheep(a, fr, rng):
    """Овца: шуба из пяти икосфер wool, чёрные морда и ноги, белые глаза."""
    for x in (-0.2, 0.2):
        for y in (-0.12, 0.12):
            fr.taper(a, (0.07, 0.07), (0.08, 0.08), 0.39, loc=(x, y, -0.01), col="coal")
    for loc, r, scl in (((0, 0, 0.52), 0.28, (1.35, 1.05, 0.9)), ((0.12, 0.08, 0.72), 0.17, (1, 1, 1)),
                        ((-0.14, -0.06, 0.73), 0.17, (1, 1, 1)), ((-0.28, 0.10, 0.56), 0.17, (1, 1, 1))):
        fr.ico(a, r, loc=loc, scl=scl, col="wool", jitter=0.1, rng=rng)
    fr.box(a, (0.22, 0.22, 0.26), (0.42, 0, 0.60), col="coal", bevel=0.06)
    fr.ico(a, 0.10, loc=(0.38, 0, 0.75), scl=(1.0, 1.1, 0.7), col="wool")
    for sy in (-1, 1):
        fr.box(a, (0.06, 0.14, 0.05), (0.38, sy * 0.15, 0.66), rot=(-sy * 20, 0, 0), col="coal", bevel=0.0)
        fr.box(a, (0.02, 0.045, 0.045), (0.535, sy * 0.06, 0.64), col="wool", bevel=0.0)
    fr.ico(a, 0.08, loc=(-0.40, 0, 0.56), col="wool")


def _chicken(a, fr, peck=False):
    """Курица: тело и голова — икосферы wool, красный гребешок и бородка, жёлтые клюв и ноги."""
    for sy in (-1, 1):
        fr.box(a, (0.04, 0.04, 0.16), (0.0, sy * 0.05, 0.08), col="flower_y", bevel=0.0)
    b = fr.sub((0, 0, 0.14), rot=(0, 28 if peck else 0, 0))
    b.ico(a, 0.16, loc=(0, 0, 0.11), scl=(1.2, 0.95, 0.95), col="wool")
    b.box(a, (0.08, 0.15, 0.17), (-0.20, 0, 0.21), rot=(0, -30, 0), col="wool", bevel=0.0)
    b.ico(a, 0.09, loc=(0.14, 0, 0.28), col="wool")
    b.box(a, (0.11, 0.035, 0.07), (0.15, 0, 0.38), col="berry", bevel=0.0)
    b.box(a, (0.035, 0.03, 0.07), (0.21, 0, 0.22), col="berry", bevel=0.0)
    b.cyl(a, 0.035, 0.0, 0.08, 4, loc=(0.21, 0, 0.28), rot=(0, 90, 0), spin=45, col="flower_y")
    for sy in (-1, 1):
        b.box(a, (0.02, 0.02, 0.025), (0.205, sy * 0.05, 0.31), col="coal", bevel=0.0)


# =========================================================================================
# Реквизит
# =========================================================================================
def _feeder(a, fr):
    """Кормушка вдоль X на козлах, полная зерна: зерно чуть выше борта и горкой посередине."""
    fr.box(a, (1.10, 0.36, 0.20), (0, 0, 0.44), col=by_normal("wood_light", "wood_mid", "wood_dark", 0.8),
           bevel=0.03)
    fr.box(a, (1.00, 0.26, 0.03), (0, 0, 0.56), col="wheat", bevel=0.0)
    fr.ico(a, 0.30, loc=(0.05, 0, 0.54), scl=(1.5, 0.42, 0.32), col=EAR, cut=0.0)
    for sx in (-1, 1):
        fr.box(a, (0.07, 0.46, 0.30), (sx * 0.58, 0, 0.47), col="wood_dark", bevel=0.0)
        for k, s2 in enumerate((1, -1)):
            fr.taper(a, (0.06, 0.06), (0.06, 0.06), 0.42, loc=(sx * (0.40 + 0.07 * k), s2 * 0.10, 0.0),
                     rot=(s2 * 22, 0, 0), col="wood_dark")


def _trough(a, fr):
    """Каменная поилка вдоль X: четыре стенки и вода на 6 см ниже борта."""
    col = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
    for sy in (-1, 1):
        fr.box(a, (0.86, 0.09, 0.32), (0, sy * 0.17, 0.16), col=col, bevel=0.03)
    for sx in (-1, 1):
        fr.box(a, (0.09, 0.30, 0.32), (sx * 0.38, 0, 0.16), col=col, bevel=0.0)
    fr.box(a, (0.72, 0.28, 0.04), (0, 0, 0.24), col="water", bevel=0.0)


def _sack(a, fr, rng):
    """Мешок: гранёная икосфера с плоским дном, горловина с перетяжкой."""
    fr.ico(a, 0.21, loc=(0, 0, 0.17), scl=(1.0, 0.85, 1.12), col="burlap", jitter=0.06, rng=rng, cut=-0.7)
    fr.cyl(a, 0.09, 0.05, 0.14, 7, loc=(0, 0, 0.36), col="burlap_dark")
    fr.cyl(a, 0.07, 0.07, 0.04, 7, loc=(0, 0, 0.42), col="rope")


def _haystack(a, fr):
    """Стог-«улей»: три гранёных усечённых конуса и жердь. Икосфера читалась то кристаллом,
    то грушей; ступенчатый конус с пологой шапкой — стогом."""
    hay = by_normal("wheat", "wood_yellow", "wood_yellow", 0.45)
    fr.cyl(a, 0.54, 0.50, 0.32, 9, loc=(0, 0, -0.02), col=hay)
    fr.cyl(a, 0.50, 0.30, 0.34, 9, loc=(0, 0, 0.30), col=hay, spin=20)
    fr.cyl(a, 0.30, 0.05, 0.26, 9, loc=(0, 0, 0.64), col=hay, spin=40)
    fr.cyl(a, 0.515, 0.515, 0.06, 9, loc=(0, 0, 0.20), col="rope")
    fr.box(a, (0.06, 0.06, 0.42), (0.02, 0.0, 1.02), rot=(0, 6, 0), col="wood_dark", bevel=0.0)


def build(a):
    rng = random.Random(5)
    _barn(a)

    # загон: утоптанная земля и изгородь, левый бок которой упирается в угол амбара
    x0, x1, y0, y1 = PEN
    a.add(p_box((x1 - x0 - 0.06, y1 - y0 - 0.06, 0.07), loc=((x0 + x1) / 2, (y0 + y1) / 2, ZG - 0.035),
                bevel=0.025), by_normal("soil_light", "soil_mid", "soil_mid", 0.8))
    fence_line(a, (x0, y0), (x1, y0), posts=3)
    fence_line(a, (x1, y0), (x1, y1), posts=3, end_posts=(False, True))
    fence_line(a, (x1, y1), (0.16, y1), posts=3, end_posts=(False, True))
    fence_line(a, (x0, y0), (x0, -0.34), posts=2, end_posts=(False, True))

    _feeder(a, Frame((1.55, 0.52, ZG)))
    _trough(a, Frame((2.08, -0.36, ZG), rz=90))
    a.add(p_ico(0.42, 1, loc=(1.70, -1.34, ZG - 0.01), scl=(1.3, 1.0, 0.07), rot=(0, 0, 20)), "soil_dark")

    # корова пьёт из поилки, свинья и овца смотрят в разные стороны — «V» к камере
    _cow(a, Frame((1.12, -0.28, ZG), rz=-28), rng)
    _pig(a, Frame((1.72, -1.34, ZG), rz=-35))
    _sheep(a, Frame((0.82, -1.45, ZG), rz=235), rng)
    _chicken(a, Frame((-1.36, -1.18, 0.0), rz=-55))
    _chicken(a, Frame((-0.78, -1.62, 0.0), rz=150), peck=True)
    a.add(p_ico(0.10, 1, loc=(-0.98, -1.70, 0.0), scl=(1.4, 1.0, 0.3), cut=0.0), "wheat")

    # сено и мешки: тюки у левого угла амбара, стог за загоном, мешки у ворот
    hay_bale(a, (-2.15, -0.66, 0.0), s=(0.56, 0.42, 0.36), rot=(0, 0, 8))
    hay_bale(a, (-2.12, -0.62, 0.36), s=(0.52, 0.40, 0.34), rot=(0, 0, -6))
    _haystack(a, Frame((1.62, 1.62, 0.0)))
    _sack(a, Frame((-0.14, -0.66, 0.0), rz=20), rng)
    _sack(a, Frame((-0.05, -0.98, 0.0), rz=-30), rng)


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py)
# =========================================================================================
def _well(a, fr, level):
    """Колодец: каменный сруб, ворот на двух стойках, двускатная крышка цвета кровли, ведро."""
    st = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
    fr.cyl(a, 0.36, 0.36, 0.46, 10, loc=(0, 0, 0.0), col=st, bevel=0.03)
    fr.cyl(a, 0.27, 0.27, 0.02, 10, loc=(0, 0, 0.40), col="water")
    for sx in (-1, 1):
        fr.box(a, (0.09, 0.09, 1.12), (sx * 0.30, 0, 0.56), col="wood_dark", bevel=0.0)
        fr.box(a, (0.07, 0.62, 0.06), (sx * 0.24, 0, 1.17), rot=(sx * 0, 0, 0), col="wood_dark", bevel=0.0)
    fr.cyl(a, 0.06, 0.06, 0.62, 8, loc=(-0.31, 0, 0.84), rot=(0, 90, 0), col="wood_light")
    fr.box(a, (0.05, 0.05, 0.22), (0.33, 0.0, 0.76), col="iron_dark", bevel=0.0)
    for sy in (-1, 1):
        fr.box(a, (0.80, 0.42, 0.06), (0, sy * 0.17, 1.30), rot=(-sy * 34, 0, 0),
               col="roof" if sy < 0 else "roof_dark", bevel=0.0)
    fr.cyl(a, 0.08, 0.10, 0.14, 8, loc=(0.0, 0.0, 0.50), col="wood_mid")


def _silo(a, fr, level):
    """Силос: каменный цоколь, дощатый ствол с железными обручами, купол цвета кровли, золотое навершие."""
    r, h = 0.44, 2.30
    fr.cyl(a, r + 0.06, r + 0.06, 0.30, 12, loc=(0, 0, -0.06), col=by_normal("stone_mid", "stone_dark", "stone_dark", 0.8),
           bevel=0.03)
    fr.cyl(a, r, r, h, 12, loc=(0, 0, 0.24), col=by_normal("wood_light", "wood_mid", "wood_dark", 0.8))
    for z in (0.75, 1.45, 2.15):
        fr.cyl(a, r + 0.02, r + 0.02, 0.06, 12, loc=(0, 0, 0.24 + z - 0.4), col="iron_dark")
    fr.cyl(a, r + 0.07, r + 0.07, 0.08, 12, loc=(0, 0, 0.24 + h), col="cream")
    fr.cyl(a, r + 0.04, 0.08, 0.50, 12, loc=(0, 0, 0.24 + h + 0.08), col="roof")
    finial(a, fr.at((0, 0, 0)).x, fr.at((0, 0, 0)).y, fr.at((0, 0, 0.24 + h + 0.56)).z, h=0.40)
    # окошко-люк к камере
    fr.box(a, (0.26, 0.08, 0.30), (0, -r + 0.01, 0.24 + h - 0.45), col="cream", bevel=0.0)
    fr.box(a, (0.18, 0.06, 0.22), (0, -r - 0.02, 0.24 + h - 0.45), col="black", bevel=0.0)


def upgrade(a, level):
    """2: флажок на правом скате амбара, колодец у левого угла, вторая овца у кормушки, поросёнок,
    ещё два тюка сена. 3: + силос между амбаром и стогом, золото по коньку амбара с навершиями,
    знамя слева от ворот, фонари у ворот."""
    rng = random.Random(17)
    hw, t1, t2, zb, zr = _roof_geom()
    xp = 0.45
    pennant(a, BX + xp, BY - 0.85, zr - xp * t2 + 0.10, level, side=1)
    _well(a, Frame((-2.02, -1.55, 0.0), rz=-8), level)
    _sheep(a, Frame((0.62, 0.38, ZG), rz=-12), rng)
    pg = Frame((2.08, -1.66, ZG), rz=-150, s=0.62)
    _pig(a, pg)
    hay_bale(a, (-1.62, -0.62, 0.0), s=(0.52, 0.40, 0.34), rot=(0, 0, -14))
    hay_bale(a, (-1.95, -0.30, 0.0), s=(0.50, 0.38, 0.32), rot=(0, 0, 80))
    if level < 3:
        return
    _silo(a, Frame((0.66, 1.58, 0.0)), level)
    gold_ridge(a, D + 2 * OY + 0.14, zr - 0.01, axis="y", x=BX, y=BY, cap=0.30)
    yF = BY - D / 2
    wall_banner(a, BX - 0.92, yF - 0.07, 1.58, w=0.36, h=0.70)
    for sx in (-1, 1):
        wall_lantern(a, BX + sx * 0.84, yF - 0.06, 1.44, out=(sx * 0.35, -0.94))
