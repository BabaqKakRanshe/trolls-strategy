"""
Рынок: площадка из плит, на ней прилавок под сине-кремовым полосатым навесом и круглый
шатёр торговца тканями в красно-кремовую полоску с флажком.

Референсы: средневековые рыночные прилавки (полосатая ткань, ящики с фруктами и овощами,
бочки, мешки, вывеска) и Marketplace из Warcraft III (шатёр с полосатым куполом и товаром).
Главные приметы с игровой дистанции — два полосатых купола разного цвета, яркий товар
на прилавке и весы с золотыми чашами.
"""
import math
import random

from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal, coin, build_sack
from vitaria_buildings.common import Frame, barrel, crate, lantern, STONE_TOP, PLANK_TOP
from vitaria_buildings.levels import (Shift, quoins, arcade, cupola, gable_x_at, gable_end, porch_post, WALL, BEAM,
                                    PLANKS)

NAME = "Bld_Market"
TITLE = "Рынок"
TARGET = (4.4, 3.6, 3.6)

SX, SY = -0.78, -0.10       # центр прилавка
TX, TY = 1.28, 0.62         # центр шатра
GOLD = by_normal("gold_light", "gold", "gold_dark", 0.5)


def _plaza(a):
    """Площадка: цокольная плита и несколько крупных плит мощения поверх."""
    a.add(p_box((4.30, 3.30, 0.16), loc=(0, 0.02, 0.0), bevel=0.05), by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    rng = random.Random(3)
    for x, y, w, d in ((-1.55, -1.10, 0.9, 0.7), (-0.45, -1.25, 0.8, 0.55), (0.55, -1.05, 0.95, 0.75),
                       (1.60, -1.15, 0.8, 0.6), (0.20, 0.10, 0.9, 0.8), (-1.60, 0.95, 0.8, 0.7),
                       (0.35, 1.15, 0.85, 0.6)):
        a.add(p_box((w, d, 0.03), loc=(x, y, 0.085), rot=(0, 0, rng.uniform(-4, 4)), bevel=0.0), "stone_light")


def _awning_stall(a):
    """Прилавок: стойки, прилавок-короб, задняя стенка с полками, наклонный полосатый навес."""
    fr = Frame((SX, SY, 0.08))
    W, yf, yb = 2.30, -0.72, 0.62
    # стойки: передние ниже задних — наклон навеса к покупателю
    for sx in (-1, 1):
        fr.box(a, (0.17, 0.17, 2.02), (sx * W / 2, yf, 1.01), col="wood_dark", bevel=0.035)
        fr.box(a, (0.17, 0.17, 2.48), (sx * W / 2, yb, 1.24), col="wood_dark", bevel=0.035)
    # прилавок: филёнки поочерёдно двух тонов, толстая столешница, цоколь
    yc = yf + 0.22
    n = 4
    pw = (W - 0.16) / n
    for i in range(n):
        fr.box(a, (pw - 0.04, 0.12, 0.72), (-W / 2 + 0.08 + pw * (i + 0.5), yc - 0.20, 0.40),
               col="wood_mid" if i % 2 else "wood_mid2", bevel=0.03)
    fr.box(a, (W - 0.08, 0.46, 0.70), (0, yc, 0.39), col="wood_dark", bevel=0.03)
    fr.box(a, (W + 0.10, 0.62, 0.10), (0, yc - 0.02, 0.80), col=PLANK_TOP, bevel=0.035)
    fr.box(a, (W + 0.02, 0.14, 0.12), (0, yc - 0.26, 0.08), col="wood_dark", bevel=0.03)
    # задняя стенка с двумя полками: на них кувшины — видны из-под навеса
    fr.box(a, (W - 0.10, 0.10, 1.70), (0, yb + 0.02, 0.95), col="wood_mid", bevel=0.03)
    for z in (1.05, 1.55):
        fr.box(a, (W - 0.20, 0.30, 0.07), (0, yb - 0.14, z), col="wood_light", bevel=0.02)
    # кувшины только на нижней полке: верхнюю с игровой камеры закрывает навес
    for k, x in enumerate((-0.80, -0.42, -0.02, 0.40, 0.78)):
        col = ("copper", "soil_light", "copper_dark", "roof", "soil_light")[k]
        fr.cyl(a, 0.085, 0.06, 0.22, 8, loc=(x, yb - 0.14, 1.085), col=col, bevel=0.01)
        fr.cyl(a, 0.045, 0.05, 0.06, 8, loc=(x, yb - 0.14, 1.30), col=col)
    # тыл задней стенки: две нащельные планки, чтобы глухая доска не читалась плитой
    for z in (0.55, 1.35):
        fr.box(a, (W - 0.16, 0.05, 0.12), (0, yb + 0.095, z), col="wood_dark", bevel=0.015)
    # навес: 6 полос вдоль X, фестоны по переднему краю
    zb, zf = 2.50, 2.02
    y0, y1 = yb + 0.10, yf - 0.36
    L = math.hypot(y0 - y1, zb - zf)
    ang = math.degrees(math.atan2(zb - zf, y0 - y1))
    Wa, ns = W + 0.40, 6
    sw = Wa / ns
    cy, cz = (y0 + y1) / 2, (zb + zf) / 2 + 0.06
    for i in range(ns):
        x = -Wa / 2 + sw * (i + 0.5)
        col = "roof" if i % 2 == 0 else "cream"
        fr.box(a, (sw + 0.004, L, 0.10), (x, cy, cz), rot=(ang, 0, 0), col=col, bevel=0.0)
        fr.prism(a, [(-sw / 2, 0.0), (sw / 2, 0.0), (sw / 2, -0.18), (0, -0.32), (-sw / 2, -0.18)], 0.08,
                 loc=(x, y1 - 0.01, zf + 0.06), col=col)
    # прогоны под навесом
    fr.box(a, (W + 0.20, 0.15, 0.15), (0, yf, 2.06), col="wood_dark", bevel=0.035)
    fr.box(a, (W + 0.20, 0.15, 0.15), (0, yb, 2.44), col="wood_dark", bevel=0.035)
    return fr, yc


def _produce_crate(a, fr, loc, rz, fruit, seed, tilt=18):
    """Ящик с товаром, наклонён к покупателю; fruit — (цвет, радиус, масштаб)."""
    rng = random.Random(seed)
    c = fr.sub(loc, rot=(tilt, 0, rz))
    c.box(a, (0.52, 0.38, 0.16), (0, 0, 0.08), col=by_normal("wood_pale", "wood_light", "wood_dark", 0.7), bevel=0.02)
    col, r, scl = fruit
    for i in range(3):
        for j in range(2):
            x = -0.16 + i * 0.16 + rng.uniform(-0.02, 0.02)
            y = -0.08 + j * 0.16 + rng.uniform(-0.02, 0.02)
            c.ico(a, r, loc=(x, y, 0.16 + r * 0.55), scl=scl, col=col,
                  rot=(rng.uniform(0, 40), rng.uniform(0, 40), rng.uniform(0, 360)))
    # верхний слой — два плода поверх стыков, горкой
    for x in (-0.08, 0.08):
        c.ico(a, r, loc=(x, 0.0, 0.16 + r * 1.35), scl=scl, col=col)


def _scales(a, fr):
    """Весы: основание, стойка, коромысло, две золотые чаши (одна ниже — перевешивает)."""
    fr.box(a, (0.26, 0.18, 0.05), (0, 0, 0.025), col="wood_dark", bevel=0.015)
    fr.box(a, (0.05, 0.05, 0.46), (0, 0, 0.25), col="iron_dark", bevel=0.0)
    fr.box(a, (0.56, 0.045, 0.045), (0, 0, 0.47), rot=(0, 8, 0), col=GOLD, bevel=0.0)
    for sx, dz in ((-1, 0.04), (1, -0.04)):
        x = sx * 0.26
        z = 0.47 - sx * 0.036
        fr.box(a, (0.03, 0.03, 0.20), (x, 0, z - 0.10), col="iron_dark", bevel=0.0)
        fr.cyl(a, 0.11, 0.09, 0.04, 10, loc=(x, 0, z - 0.22), col=GOLD)
    # на правой (перевесившей) чаше — горка монет
    for k in range(3):
        coin(a, mat=fr.M((0.26, 0.0, 0.47 - 0.036 * 1 - 0.18 + k * 0.015)))


def _coin_sign(a, fr, side=1):
    """Вывеска на кронштейне передней стойки (side: +1 — вправо от стойки, -1 — влево):
    доска и крупная золотая монета лицом к камере."""
    fr.box(a, (0.62, 0.12, 0.12), (side * 0.28, 0, 0), col="wood_dark", bevel=0.03)
    fr.box(a, (0.10, 0.10, 0.36), (side * 0.10, 0, -0.14), rot=(0, -side * 45, 0), col="wood_dark", bevel=0.02)
    for x in (0.18, 0.46):
        fr.box(a, (0.05, 0.05, 0.14), (side * x, 0, -0.12), col="rope", bevel=0.0)
    fr.box(a, (0.46, 0.08, 0.40), (side * 0.32, 0, -0.38), col="wood_light", bevel=0.03)
    fr.cyl(a, 0.15, 0.15, 0.05, 14, loc=(side * 0.32, -0.04, -0.38), rot=(90, 0, 0), col=GOLD, bevel=0.012)
    fr.cyl(a, 0.07, 0.07, 0.02, 10, loc=(side * 0.32, -0.085, -0.38), rot=(90, 0, 0), col="gold_dark")


def _tent(a, R=1.00, H0=1.78, HC=1.12, posts=None, flag=True, cx=TX, cy=TY):
    """Круглый шатёр: 4 стойки, восьмиклинный конус в полоску, фестоны, навершие с флажком.
    posts — свой обвод стоек (уровни 2-3: столбы ставит вызывающий), flag=False — навершие без вымпела."""
    fr = Frame((cx, cy, 0.08))
    if posts is None:
        for k in range(4):
            ang = math.radians(45 + 90 * k)
            fr.box(a, (0.13, 0.13, H0), (0.66 * math.cos(ang), 0.66 * math.sin(ang), H0 / 2), col="wood_dark", bevel=0.03)

    def gore(f):
        n = f.normal
        if n.z < -0.5:
            return "cream"
        az = (math.degrees(math.atan2(n.y, n.x)) + 360.0) % 360.0
        return "berry" if int(az // 45) % 2 == 0 else "cream"
    fr.cyl(a, R, 0.0, HC, 8, loc=(0, 0, H0), col=gore)
    fr.cyl(a, R + 0.04, R + 0.04, 0.10, 8, loc=(0, 0, H0 - 0.06), col="wood_dark")
    # фестоны по кругу: по два на клин, цвет клина
    for i in range(16):
        ang = 2 * math.pi * (i + 0.5) / 16
        f2 = fr.sub((math.cos(ang) * (R + 0.02), math.sin(ang) * (R + 0.02), H0 - 0.06), rz=math.degrees(ang) + 90)
        col = "berry" if (i // 2) % 2 == 0 else "cream"
        f2.prism(a, [(-0.19, 0.0), (0.19, 0.0), (0.19, -0.14), (0.0, -0.26), (-0.19, -0.14)], 0.05, col=col)
    # навершие и вымпел
    fr.ico(a, 0.09, loc=(0, 0, H0 + HC + 0.02), col=GOLD)
    if flag:
        fr.cyl(a, 0.03, 0.025, 0.52, 6, loc=(0, 0, H0 + HC + 0.08), col="wood_dark")
        fr.prism(a, [(0.0, 0.0), (0.46, -0.10), (0.0, -0.22)], 0.04, loc=(0.02, 0, H0 + HC + 0.58), col="berry")
    # под шатром: стол с рулонами ткани
    fr.box(a, (1.06, 0.62, 0.08), (0, -0.05, 0.72), col=PLANK_TOP, bevel=0.025)
    for sx in (-1, 1):
        fr.box(a, (0.10, 0.52, 0.68), (sx * 0.44, -0.05, 0.34), col="wood_dark", bevel=0.02)
    for k, (x, col) in enumerate(((-0.34, "roof"), (-0.12, "leaf_mid"), (0.10, "gold"), (0.32, "cream"))):
        fr.cyl(a, 0.10, 0.10, 0.50, 8, loc=(x, -0.30, 0.86), rot=(-90, 0, 0), col=col)
        fr.cyl(a, 0.03, 0.03, 0.54, 6, loc=(x, -0.32, 0.86), rot=(-90, 0, 0), col="wood_dark")
    # рулон, развёрнутый к покупателю, свисает со стола
    fr.box(a, (0.30, 0.04, 0.46), (-0.12, -0.39, 0.52), col="leaf_mid", bevel=0.01)


def _handcart(a, fr, rng):
    """Двухколёсная тележка с тыквами, ручки к камере."""
    fr.box(a, (0.84, 0.58, 0.10), (0, 0, 0.40), col=PLANK_TOP, bevel=0.025)
    for sy in (-1, 1):
        fr.box(a, (0.84, 0.06, 0.18), (0, sy * 0.28, 0.52), col="wood_mid", bevel=0.015)
    for sx in (-1, 1):
        fr.box(a, (0.06, 0.56, 0.18), (sx * 0.41, 0, 0.52), col="wood_mid", bevel=0.015)
    for sy in (-1, 1):
        fr.cyl(a, 0.27, 0.27, 0.07, 12, loc=(0.05, sy * 0.37, 0.27), rot=(90 * sy, 0, 0), col="wood_dark", bevel=0.012)
        fr.cyl(a, 0.08, 0.08, 0.04, 8, loc=(0.05, sy * 0.44, 0.27), rot=(90 * sy, 0, 0), col="iron_dark")
        fr.box(a, (0.70, 0.06, 0.06), (-0.70, sy * 0.24, 0.44), rot=(0, -12, 0), col="wood_mid", bevel=0.012)
    fr.box(a, (0.06, 0.06, 0.34), (0.40, 0, 0.18), col="wood_dark", bevel=0.012)
    for x, y, r in ((-0.20, -0.10, 0.15), (0.16, 0.08, 0.16), (0.00, 0.12, 0.13), (0.20, -0.14, 0.12),
                    (-0.02, -0.04, 0.12)):
        z = 0.45 + r * 0.8 + (0.10 if (x, y) == (-0.02, -0.04) else 0.0)
        fr.ico(a, r, loc=(x, y, z), scl=(1.0, 1.0, 0.78), col="leaf_autumn")
        fr.cyl(a, 0.025, 0.02, 0.07, 5, loc=(x, y, z + r * 0.72), col="leaf_dark")


def build(a):
    rng = random.Random(11)
    _plaza(a)
    fr, yc = _awning_stall(a)
    zt = 0.85                                   # верх столешницы
    # товар на прилавке: яблоки, морковь/апельсины, капуста
    _produce_crate(a, fr, (-0.78, yc - 0.06, zt), 0, ("berry", 0.075, (1, 1, 0.95)), 1)
    _produce_crate(a, fr, (-0.22, yc - 0.06, zt), 0, ("copper", 0.07, (1, 1, 1)), 2)
    _produce_crate(a, fr, (0.34, yc - 0.06, zt), 0, ("leaf_light", 0.085, (1, 1, 0.9)), 3)
    _scales(a, fr.sub((0.86, yc + 0.02, zt)))
    # хлеб на доске рядом с весами не ставим: весы — главный знак торговли, их не загораживаем
    # вывеска между прилавком и шатром: в центре композиции, над тележкой
    _coin_sign(a, fr.sub((1.15 + 0.09, -0.72, 1.86)), side=1)

    _tent(a)

    # площадь перед прилавками: бочки слева, мешки, ящики справа, тележка с тыквами
    barrel(a, (-1.78, -1.20, 0.08))
    barrel(a, (-1.30, -1.40, 0.08), r=0.20, h=0.42)
    for (x, y), seed in (((-0.62, -1.38), 4), ((-0.34, -1.52), 5)):
        build_sack(a, loc=(x, y, 0.08), seed=seed)
    crate(a, (1.80, -0.62, 0.08), s=0.44, rot=(0, 0, 8))
    crate(a, (1.80, -0.62, 0.52), s=0.40, rot=(0, 0, -6))
    # ручки тележки к камере: локальная -X смотрит почти в -Y
    _handcart(a, Frame((0.80, -1.10, 0.08), rz=72), rng)


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): прилавок и шатёр -> лавка и павильон -> торговые ряды
# =========================================================================================
def _plaza2(a, w=5.16, d=3.34, x=0.10, border=False):
    """Площадь шире прежней: плита, плиты мощения; border — каменный бордюр по краю (уровень 3)."""
    a.add(p_box((w, d, 0.16), loc=(x, 0.0, 0.0), bevel=0.05), by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    rng = random.Random(3)
    for xx, y, sw, sd in ((-1.55, -1.10, 0.9, 0.7), (-0.45, -1.25, 0.8, 0.55), (0.55, -1.05, 0.95, 0.75),
                          (1.60, -1.15, 0.8, 0.6), (2.30, -0.40, 0.6, 0.7), (-2.20, -0.30, 0.5, 0.8)):
        a.add(p_box((sw, sd, 0.03), loc=(xx, y, 0.085), rot=(0, 0, rng.uniform(-4, 4)), bevel=0.0), "stone_light")
    if border:
        for sy in (-1, 1):
            a.add(p_box((w - 0.02, 0.16, 0.12), loc=(x, sy * (d / 2 - 0.09), 0.12), bevel=0.0), "stone_light")
        for sx in (-1, 1):
            a.add(p_box((0.16, d - 0.26, 0.12), loc=(x + sx * (w / 2 - 0.09), 0, 0.12), bevel=0.0), "stone_light")


def _striped_awning(a, fr, w, y_back, z_back, y_front, z_front, ns=6, cols=("roof", "cream"), flaps=True):
    """Полосатый навес в раме fr: полосы вдоль ската от (y_back, z_back) к (y_front, z_front), фестоны."""
    L = math.hypot(y_back - y_front, z_back - z_front)
    ang = math.degrees(math.atan2(z_back - z_front, y_back - y_front))
    sw = w / ns
    cy, cz = (y_back + y_front) / 2, (z_back + z_front) / 2
    for i in range(ns):
        x = -w / 2 + sw * (i + 0.5)
        col = cols[i % 2]
        fr.box(a, (sw + 0.004, L, 0.08), (x, cy, cz), rot=(ang, 0, 0), col=col, bevel=0.0)
        if flaps:
            fr.prism(a, [(-sw / 2, 0.0), (sw / 2, 0.0), (sw / 2, -0.16), (0, -0.28), (-sw / 2, -0.16)], 0.06,
                     loc=(x, y_front - 0.01, z_front + 0.04), col=col)


def _booth(a, rng):
    """Лавка на месте прилавка: стойки, прилавок с товаром, глухие задняя и боковые стены из досок,
    кровля коньком вдоль X; полосатый навес вынесен ниже карниза перед прилавком."""
    fr = Frame((SX, SY, 0.08))
    W, yf, yb = 2.40, -0.62, 0.66
    for sx in (-1, 1):
        for y in (yf, yb):
            fr.box(a, (0.18, 0.18, 2.36), (sx * W / 2, y, 1.18), col=BEAM, bevel=0.0)
        fr.box(a, (0.10, yb - yf, 2.30), (sx * W / 2, (yf + yb) / 2, 1.15), col="wood_mid", bevel=0.0)
    fr.box(a, (W, 0.10, 2.30), (0, yb, 1.15), col="wood_mid", bevel=0.0)
    for z in (1.05, 1.55):
        fr.box(a, (W - 0.20, 0.30, 0.07), (0, yb - 0.18, z), col="wood_light", bevel=0.0)
    for k, x in enumerate((-0.80, -0.40, 0.0, 0.40, 0.80)):
        col = ("copper", "soil_light", "copper_dark", "roof", "soil_light")[k]
        fr.cyl(a, 0.085, 0.06, 0.22, 8, loc=(x, yb - 0.18, 1.085), col=col)
    yc = yf + 0.24
    fr.box(a, (W - 0.08, 0.46, 0.70), (0, yc, 0.39), col="wood_dark", bevel=0.0)
    for i in range(4):
        pw = (W - 0.16) / 4
        fr.box(a, (pw - 0.04, 0.10, 0.70), (-W / 2 + 0.08 + pw * (i + 0.5), yc - 0.24, 0.40),
               col="wood_mid" if i % 2 else "wood_mid2", bevel=0.0)
    fr.box(a, (W + 0.10, 0.62, 0.10), (0, yc - 0.02, 0.80), col=PLANK_TOP, bevel=0.0)
    zt = 0.85
    _produce_crate(a, fr, (-0.80, yc - 0.06, zt), 0, ("berry", 0.075, (1, 1, 0.95)), 1)
    _produce_crate(a, fr, (-0.24, yc - 0.06, zt), 0, ("copper", 0.07, (1, 1, 1)), 2)
    _produce_crate(a, fr, (0.32, yc - 0.06, zt), 0, ("leaf_light", 0.085, (1, 1, 0.9)), 3)
    _scales(a, fr.sub((0.86, yc + 0.02, zt)))
    fr.box(a, (W + 0.20, 0.16, 0.16), (0, yf, 2.30), col=BEAM, bevel=0.0)
    _striped_awning(a, fr, W + 0.30, yf + 0.04, 2.16, yf - 0.72, 1.74)
    zr = gable_x_at(a, SX, SY + (yf + yb) / 2, W, yb - yf, 2.44, pitch_deg=30, ox=0.18, oy=0.20, bands=2)
    for sx in (-1, 1):
        gable_end(a, SX + sx * W / 2, SY + (yf + yb) / 2, yb - yf, 2.42, zr, face=sx, vent=False)
    return fr


PX_, PY_ = TX, 0.50          # центр павильона: на 12 см ближе шатра — купол крупнее, а глубина следа та же


def _pavilion(a, R=1.00, H0=1.96, HC=1.30, stone=False):
    """Павильон на месте шатра: настил-восьмиугольник, шесть столбов, перила, полосатый купол без вымпела."""
    TX, TY = PX_, PY_
    a.add(p_cyl(R + 0.06, R + 0.06, 0.16, 8, loc=(TX, TY, 0.06), spin=22.5),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8) if stone else PLANKS)
    for k in range(6):
        ang = math.radians(30 + 60 * k)
        x, y = TX + (R - 0.16) * math.cos(ang), TY + (R - 0.16) * math.sin(ang)
        if stone:
            a.add(p_cyl(0.11, 0.10, H0 + 0.06, 8, loc=(x, y, 0.16)), "stone_light")
            a.add(p_box((0.26, 0.26, 0.10), loc=(x, y, H0 + 0.12), bevel=0.0), "stone_mid")
        else:
            a.add(p_box((0.14, 0.14, H0 + 0.06), loc=(x, y, 0.16 + (H0 + 0.06) / 2), bevel=0.0), BEAM)
    for k in range(6):
        if k in (4,):                        # вход к камере
            continue
        a0, a1 = math.radians(30 + 60 * k), math.radians(90 + 60 * k)
        p0 = (TX + (R - 0.16) * math.cos(a0), TY + (R - 0.16) * math.sin(a0))
        p1 = (TX + (R - 0.16) * math.cos(a1), TY + (R - 0.16) * math.sin(a1))
        L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        rz = math.degrees(math.atan2(p1[1] - p0[1], p1[0] - p0[0]))
        f = Frame(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, 0.22), rz=rz)
        f.box(a, (L, 0.08, 0.08), (0, 0, 0.58), col="wood_light", bevel=0.0)
        for t in (-0.25, 0.0, 0.25):
            f.box(a, (0.05, 0.05, 0.56), (t * L, 0, 0.28), col="wood_mid", bevel=0.0)
    _tent(a, R=R + 0.10, H0=H0 + 0.08, HC=HC, posts=False, flag=False, cx=TX, cy=TY)


def _l2(a):
    """Лавка и павильон: прилавок стал лавкой — стены из досок, кровля коньком вдоль X, полосатый навес
    под карнизом; шатёр — павильон на шести столбах с настилом и перилами, купол больше; площадь шире,
    у края — бочки, ящик яблок и корзины."""
    rng = random.Random(11)
    _plaza2(a)
    fr = _booth(a, rng)
    _coin_sign(a, Frame((SX + 1.24 + 0.09, SY - 0.62, 1.86)), side=1)
    _pavilion(a)
    barrel(a, (-1.78, -1.20, 0.08))
    barrel(a, (-1.30, -1.40, 0.08), r=0.20, h=0.42)
    for (x, y), seed in (((-0.62, -1.38), 4), ((-0.34, -1.52), 5)):
        build_sack(a, loc=(x, y, 0.08), seed=seed)
    crate(a, (2.10, -0.80, 0.08), s=0.44, rot=(0, 0, 8))
    crate(a, (2.10, -0.80, 0.52), s=0.40, rot=(0, 0, -6))
    _handcart(a, Frame((0.80, -1.10, 0.08), rz=72), rng)
    _produce_crate(a, Frame((0, 0, 0)), (1.70, -1.52, 0.08), -10, ("berry", 0.075, (1, 1, 0.95)), 7, tilt=0)
    for x, y in ((-2.20, -0.62), (-1.92, -0.80)):
        a.add(p_cyl(0.17, 0.20, 0.24, 8, loc=(x, y, 0.08)), "burlap")
        a.add(p_ico(0.15, 1, loc=(x, y, 0.30), scl=(1.0, 1.0, 0.5), cut=0.0), "leaf_light" if x < -2.0 else "copper")


def _l3(a):
    """Торговые ряды: каменный корпус-аркада на три арки под кровлей коньком вдоль X с башенкой-фонарём;
    из каждой арки — полосатый навес над товаром; павильон — на каменных колоннах и каменном цоколе;
    площадь с бордюром, весы на тумбе, тележка, бочки."""
    rng = random.Random(11)
    _plaza2(a, w=5.30, x=0.12, border=True)
    x0, x1, y0, y1, zw = -2.40, 0.66, -0.66, 1.30, 2.36
    t = 0.30
    a.add(p_box((x1 - x0, t, zw - 0.08), loc=((x0 + x1) / 2, y1 - t / 2, (0.08 + zw) / 2), bevel=0.04), WALL)
    for x in (x0 + t / 2, x1 - t / 2):
        a.add(p_box((t, y1 - y0, zw - 0.08), loc=(x, (y0 + y1) / 2, (0.08 + zw) / 2), bevel=0.04), WALL)
    cs = arcade(a, x0, x1, y0 + t / 2, t, 0.08, zw, 3, 1.30, pier=0.26)
    quoins(a, (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0, 0.08, zw, corners=((-1, -1), (1, -1)))
    a.add(p_box((x1 - x0 + 0.10, t + 0.10, 0.12), loc=((x0 + x1) / 2, y0 + t / 2, zw + 0.02), bevel=0.0), "stone_light")
    zr = gable_x_at(a, (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0, zw, pitch_deg=30, ox=0.12, oy=0.12, bands=2)
    for sx, x in ((-1, x0), (1, x1)):
        a.add(p_prism([(-(y1 - y0) / 2, zw), ((y1 - y0) / 2, zw), (0, zr - 0.05)], t,
                      loc=(x + (-sx) * t / 2, (y0 + y1) / 2, 0), rot=(0, 0, 90)), WALL)
    cupola(a, (x0 + x1) / 2, (y0 + y1) / 2, zr, s=0.62, h=0.48, roof_h=0.60)
    # в арках: прилавки с товаром и полосатые навесы наружу
    cols = (("roof", "cream"), ("berry", "cream"), ("roof", "cream"))
    fruit = (("berry", 0.075, (1, 1, 0.95)), ("copper", 0.07, (1, 1, 1)), ("leaf_light", 0.085, (1, 1, 0.9)))
    for k, cx in enumerate(cs):
        fr = Frame((cx, y0, 0.08))
        fr.box(a, (0.78, 0.40, 0.66), (0, 0.24, 0.33), col="wood_dark", bevel=0.0)
        fr.box(a, (0.84, 0.48, 0.08), (0, 0.22, 0.70), col=PLANK_TOP, bevel=0.0)
        _produce_crate(a, fr, (0.0, 0.18, 0.74), 0, fruit[k], 11 + k, tilt=14)
        _striped_awning(a, fr, 0.96, 0.02, 1.62, -0.62, 1.30, ns=4, cols=cols[k])
    _pavilion(a, stone=True)
    _coin_sign(a, Frame((x1 + 0.06, y0 - 0.08, 1.96)), side=1)
    _scales(a, Frame((1.90, -1.38, 0.08 + 0.74)))
    a.add(p_box((0.42, 0.42, 0.74), loc=(1.90, -1.38, 0.08 + 0.37), bevel=0.03), WALL)
    barrel(a, (-1.98, -1.40, 0.08))
    barrel(a, (-1.52, -1.58, 0.08), r=0.20, h=0.42)
    for (x, y), seed in (((-0.82, -1.56), 4), ((-0.54, -1.66), 5)):
        build_sack(a, loc=(x, y, 0.08), seed=seed)
    _handcart(a, Frame((0.80, -1.10, 0.08), rz=72), rng)
    crate(a, (2.30, -0.80, 0.08), s=0.44, rot=(0, 0, 8))


def evolve(a, level):
    """2: лавка и павильон. 3: торговые ряды с аркадой и павильон на колоннах."""
    (_l2 if level == 2 else _l3)(a)
