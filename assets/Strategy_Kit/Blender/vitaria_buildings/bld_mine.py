"""
Шахта: скальный холм из крупных валунов с золотыми жилами, в нём — вход, обшитый брусом,
над входом синий навес, из штольни выходят рельсы с вагонеткой, полной золота.

Референсы: Gold Mine из Warcraft III (холм, тяжёлая деревянная рама, золото у входа) и
стилизованные входы в шахту (рама из бруса с раскосами, рельсы, вагонетка, фонарь).
От Bld_MineHoist из Ref_Buildings отличается всем силуэтом: там каменная стенка и копёр,
здесь природная скала и штольня.

Штольня собрана без булевых: обшивка из досок по бокам и потолку, в глубине — чёрная плита
перед скалой. Плита стоит ближе грани валуна, иначе в проёме была бы видна серая скала.
"""
import math
import random

from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal, rock_face_color, ore_chunk, TM
from vitaria_buildings.common import Frame, lantern, STONE_TOP
from vitaria_buildings.common import plank_door
from vitaria_buildings.levels import (Shift, quoins, stack, win, arched_wall, gable_front, gable_x_at, gable_y_at,
                                    porch_post, wall_lamp, WALL, BEAM, PLANKS)

NAME = "Bld_Mine"
TITLE = "Шахта"
TARGET = (4.3, 3.6, 3.0)

YF = -0.72                  # плоскость устья штольни
PX = 0.80                   # оси стоек рамы
YP = YF - 0.12              # стойки чуть впереди скалы
ZL = 1.98                   # ось перемычки
GOLD = by_normal("gold_light", "gold", "gold_dark", 0.5)
NUG = by_normal("gold_light", "ore_gold", "gold_dark", 0.4)


def _boulder(a, rng, loc, r, scl, seed, gold=0):
    """Валун кита (20 граней + дрожание), низ подрезан по z=-0.06; gold — число золотых
    вкраплений на гранях, смотрящих на камеру. Точки берём с реальных граней валуна,
    иначе из-за дрожания самородки то тонут, то висят."""
    b = p_ico(r, 1, loc=loc, scl=scl, jitter=0.26, rng=rng, rot=(0, 0, rng.uniform(0, 360)),
              cut=-0.06 - loc[2])
    faces = [f for f in b.faces if f.normal.y < -0.2 and f.normal.z > -0.3 and f.calc_center_median().z > 0.3]
    picks = []
    for f in rng.sample(faces, min(gold, len(faces))):
        picks.append((f.calc_center_median().copy(), f.normal.copy()))
    a.add(b, rock_face_color(seed))
    for c, n in picks:
        for k in range(rng.randint(2, 3)):
            off = Vector((rng.uniform(-0.09, 0.09), rng.uniform(-0.04, 0.04), rng.uniform(-0.09, 0.09)))
            a.add(p_ico(rng.uniform(0.06, 0.10), 1, loc=c + n * 0.02 + off, scl=(1, 0.8, 1.15),
                        rot=(rng.uniform(0, 60), rng.uniform(0, 60), rng.uniform(0, 360))), NUG)


def _rock(a):
    rng = random.Random(41)
    # (центр, радиус, масштаб, семя шума, вкраплений). Валуны близкого размера и на разной
    # высоте — правило кита: один доминирующий давал бы конус-«палатку». Холм выше навеса:
    # задний край навеса уходит в камень, и вход читается как штольня в скале, а не сарай.
    for loc, r, scl, seed, gold in (
            ((0.00, 0.95, 0.00), 1.00, (1.45, 1.10, 1.95), 1.0, 0),
            ((0.12, 0.62, 1.75), 0.85, (1.30, 1.00, 1.00), 2.0, 2),
            ((-0.30, 1.00, 2.30), 0.60, (1.10, 1.00, 0.90), 3.0, 1),
            ((0.00, -0.05, 2.25), 0.75, (1.35, 0.80, 0.70), 4.0, 2),
            ((-1.38, 0.20, 0.00), 0.95, (1.00, 1.05, 1.35), 5.0, 3),
            ((1.36, 0.30, 0.00), 0.98, (1.02, 1.00, 1.40), 6.0, 3),
            ((-0.88, 0.72, 1.10), 0.70, (1.05, 1.00, 1.00), 7.0, 2),
            ((0.98, 0.85, 1.25), 0.75, (1.00, 1.00, 1.00), 8.0, 2),
            ((-1.10, 1.30, 0.30), 0.80, (1.00, 0.95, 1.00), 9.0, 0),
            ((1.15, 1.35, 0.30), 0.85, (1.00, 0.95, 0.95), 10.0, 0),
            ((-1.72, -0.80, 0.00), 0.36, (1.00, 0.90, 0.85), 11.0, 1),
            ((1.80, -0.66, 0.00), 0.30, (1.00, 0.90, 0.80), 12.0, 1)):
        _boulder(a, rng, loc, r, scl, seed, gold)


def _adit(a):
    """Устье: обшивка, внутренняя рама для глубины, чёрная плита в глубине."""
    yb = YF + 0.40
    a.add(p_box((1.32, 0.08, 1.64), loc=(0, yb, 0.84), bevel=0.0), "black")
    for sx in (-1, 1):
        a.add(p_box((0.10, yb - YF + 0.12, 1.66), loc=(sx * 0.68, (YF + yb) / 2 - 0.04, 0.85), bevel=0.02), "wood_dark")
    a.add(p_box((1.46, yb - YF + 0.12, 0.12), loc=(0, (YF + yb) / 2 - 0.04, 1.70), bevel=0.02), "wood_dark")
    # внутренняя рама — светлее обшивки, ловит свет из проёма и даёт глубину
    yi = YF + 0.22
    for sx in (-1, 1):
        a.add(p_box((0.15, 0.15, 1.52), loc=(sx * 0.54, yi, 0.78), bevel=0.03), "wood_mid")
    a.add(p_box((1.24, 0.16, 0.15), loc=(0, yi, 1.56), bevel=0.03), "wood_mid")


def _portal(a):
    """Рама входа: каменные подушки, толстые стойки, перемычка с колпаком, подкосы."""
    for sx in (-1, 1):
        a.add(p_box((0.44, 0.44, 0.22), loc=(sx * PX, YP, 0.07), bevel=0.05), STONE_TOP)
        a.add(p_box((0.28, 0.28, 1.72), loc=(sx * PX, YP, 0.18 + 0.86), bevel=0.05),
              by_normal("wood_light", "wood_mid", "wood_dark", 0.7))
        # подкос из стойки под перемычку
        a.add(p_box((0.13, 0.15, 0.56), loc=(sx * (PX - 0.28), YP - 0.01, 1.60), rot=(0, -sx * 45, 0), bevel=0.025),
              "wood_dark")
    a.add(p_box((2.30, 0.38, 0.32), loc=(0, YP, ZL), bevel=0.06), by_normal("wood_light", "wood_mid2", "wood_dark", 0.7))
    a.add(p_box((2.44, 0.46, 0.10), loc=(0, YP + 0.02, ZL + 0.20), bevel=0.03), "wood_dark")
    # концы перемычки перехвачены железом — единственная «кованая» деталь рамы
    for sx in (-1, 1):
        a.add(p_box((0.08, 0.40, 0.34), loc=(sx * PX, YP, ZL), bevel=0.01), "iron_dark")


def _sign(a):
    """Доска на перемычке с двумя скрещёнными кирками."""
    ys = YP - 0.21
    a.add(p_box((0.86, 0.06, 0.30), loc=(0, ys, ZL), bevel=0.02), "wood_pale")
    for sgn in (-1, 1):
        fr = Frame((0, ys - 0.04, ZL), rot=(0, sgn * 42, 0))
        fr.box(a, (0.035, 0.03, 0.34), (0, 0, 0), col="wood_dark", bevel=0.0)
        # головка кирки: брусок поперёк рукояти с острыми концами
        fr.box(a, (0.22, 0.035, 0.045), (0, -0.005, 0.15), col="iron_dark", bevel=0.0)


def _awning(a):
    """Синий козырёк над входом: неглубокий, чтобы вывеска на перемычке оставалась видна
    с игровой камеры; задний край уходит в валун над штольней."""
    zb, yb = 2.52, YF + 0.22          # задний край (в скале)
    zf, yfr = 2.24, YP - 0.40         # передний край
    L = math.hypot(yb - yfr, zb - zf)
    ang = math.degrees(math.atan2(zb - zf, yb - yfr))      # подъём ската к скале
    W = 2.40
    mid = Vector((0, (yb + yfr) / 2, (zb + zf) / 2))
    nrm = Vector((0, -math.sin(math.radians(ang)), math.cos(math.radians(ang))))
    a.add(p_box((W, L, 0.10), loc=mid - nrm * 0.02, rot=(ang, 0, 0), bevel=0.03), "wood_dark")
    dirv = Vector((0, yfr - yb, zf - zb)).normalized()
    top = Vector((0, yb, zb))
    for k, col in ((0, "roof"), (1, "roof_dark")):
        c = top + dirv * (L * (0.25 + 0.5 * k)) + nrm * 0.08
        a.add(p_box((W + 0.02, L / 2 + 0.06, 0.16), loc=c, rot=(ang, 0, 0), bevel=0.05), col)
    for sx in (-1, 1):
        a.add(p_box((0.12, L + 0.06, 0.22), loc=mid + nrm * 0.06 + Vector((sx * (W / 2 + 0.05), 0, 0)),
                    rot=(ang, 0, 0), bevel=0.035), "wood_dark")
    return zf, yfr


def _rails(a):
    y0, y1 = YF + 0.36, -1.95
    L = y0 - y1
    for sx in (-1, 1):
        a.add(p_box((0.08, L, 0.08), loc=(sx * 0.26, (y0 + y1) / 2, 0.10), bevel=0.015), by_normal("iron_light", "rail"))
    n = 7
    for i in range(n):
        y = y1 + 0.12 + i * (L - 0.24) / (n - 1)
        a.add(p_box((0.76, 0.17, 0.08), loc=(0, y, 0.035), bevel=0.02), "wood_dark")


def _minecart(a, fr, rng):
    """Вагонетка с горкой золота; колёса стоят на рельсах (x = ±0.26)."""
    fr.taper(a, (0.62, 0.88), (0.76, 1.02), 0.46, loc=(0, 0, 0.22), col=by_normal("wood_light", "wood_mid2", "iron_dark", 0.8),
             bevel=0.025)
    for z in (0.30, 0.62):
        k = (z - 0.22) / 0.46
        fr.box(a, (0.62 + 0.14 * k + 0.04, 0.88 + 0.14 * k + 0.04, 0.06), (0, 0, z), col="iron_dark", bevel=0.012)
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.cyl(a, 0.14, 0.14, 0.07, 10, loc=(sx * 0.225, sy * 0.28, 0.26), rot=(0, 90 * sx, 0), col="iron_dark")
            fr.cyl(a, 0.05, 0.05, 0.03, 6, loc=(sx * 0.29, sy * 0.28, 0.26), rot=(0, 90 * sx, 0), col="iron")
    # горка: тёмная подложка и самородки поверх
    fr.ico(a, 0.42, loc=(0, 0, 0.64), scl=(0.85, 1.12, 0.40), col="gold_dark", sub=1, jitter=0.08, rng=rng, cut=0.0)
    for k in range(14):
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0, 1) ** 0.6
        x, y = math.cos(ang) * d * 0.28, math.sin(ang) * d * 0.40
        z = 0.66 + 0.14 * (1 - d * d)
        fr.ico(a, rng.uniform(0.07, 0.10), loc=(x, y, z), scl=(1, 0.85, 0.9), col=NUG,
               rot=(rng.uniform(0, 60), rng.uniform(0, 60), rng.uniform(0, 360)))


def _pickaxe(a, fr, L=0.92):
    """Кирка: низ рукояти в z=0, головка поперёк по X."""
    fr.cyl(a, 0.035, 0.03, L, 6, col="wood_light")
    fr.box(a, (0.10, 0.08, 0.09), (0, 0, L - 0.04), col="iron_dark", bevel=0.01)
    for sx in (-1, 1):
        fr.taper(a, (0.07, 0.06), (0.012, 0.012), 0.24, loc=(sx * 0.04, 0, L - 0.04), rot=(0, sx * 100, 0), col="iron")


def _shovel(a, fr, L=0.92):
    fr.cyl(a, 0.03, 0.028, L, 6, col="wood_light")
    fr.box(a, (0.14, 0.05, 0.05), (0, 0, L), col="wood_dark", bevel=0.01)
    fr.taper(a, (0.18, 0.03), (0.22, 0.035), 0.26, loc=(0, 0, -0.24), col="iron", bevel=0.0)


def _ore_pile(a, x, y, seed):
    rng = random.Random(seed)
    a.add(p_ico(0.5, 1, loc=(x, y, 0.0), scl=(1.0, 0.85, 0.42), jitter=0.12, rng=rng, cut=0.0),
          by_normal("ore_rock", "ore_rock_dk", "ore_rock_dk", 0.3))
    for k in range(7):
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0.0, 0.42)
        xx, yy = x + math.cos(ang) * d, y + math.sin(ang) * d * 0.85
        z = max(0.0, 0.18 * (1 - (d / 0.5) ** 2)) - 0.02
        ore_chunk(a, "gold", seed * 13 + k, loc=(xx, yy, z), size=rng.uniform(0.11, 0.15))


def build(a):
    rng = random.Random(7)
    _rock(a)
    _adit(a)
    _portal(a)
    _sign(a)
    zf, yfr = _awning(a)
    # фонарь под передним краем навеса, справа от вывески
    lantern(a, Frame((0.62, YF - 0.30, 1.40)))
    _rails(a)
    _minecart(a, Frame((0, -1.52, 0)), rng)

    # у входа: куча руды с золотом слева, ящик с золотом справа, инструмент у правой стойки
    _ore_pile(a, -1.30, -1.30, 5)
    cx, cy, s = 1.28, -1.38, 0.50
    a.add(p_box((s, s, s * 0.84), loc=(cx, cy, s * 0.42), rot=(0, 0, 12), bevel=0.03),
          by_normal("wood_pale", "wood_light", "wood_dark", 0.7))
    fr = Frame((cx, cy, s * 0.84), rz=12)
    for sy in (-1, 1):
        fr.box(a, (s + 0.03, 0.06, 0.08), (0, sy * s / 2, -s * 0.42), col="wood_dark", bevel=0.012)
    for k in range(6):
        fr.ico(a, rng.uniform(0.07, 0.10), loc=(rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 0.02),
               scl=(1, 0.85, 0.8), col=NUG)
    _pickaxe(a, Frame((PX + 0.30, YP - 0.10, 0.0), rot=(12, -14, 0)))
    _shovel(a, Frame((PX + 0.52, YP + 0.06, 0.24), rot=(10, -20, 0)))


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): штольня -> штольня с копром -> рудник
# =========================================================================================
def _siding(a, rng, x=1.72, y0=-1.10, y1=-1.95, cart_y=-1.52):
    """Запасной путь справа от главного и вторая вагонетка на нём — шахта даёт больше руды."""
    for sx in (-1, 1):
        a.add(p_box((0.08, y0 - y1, 0.08), loc=(x + sx * 0.26, (y0 + y1) / 2, 0.10), bevel=0.0),
              by_normal("iron_light", "rail"))
    for i in range(4):
        y = y1 + 0.12 + i * (y0 - y1 - 0.24) / 3
        a.add(p_box((0.76, 0.17, 0.08), loc=(x, y, 0.035), bevel=0.0), "wood_dark")
    _minecart(a, Frame((x, cart_y, 0), s=0.92), rng)


def _sheave(a, fr, r=0.38):
    """Шкив копра в плоскости XZ рамы (лицом к камере): обод, ступица, четыре спицы, ось вдоль Y."""
    fr.cyl(a, r, r, 0.08, 14, loc=(0, 0.04, 0), rot=(90, 0, 0), col="iron_dark")
    fr.cyl(a, r - 0.07, r - 0.07, 0.09, 14, loc=(0, 0.045, 0), rot=(90, 0, 0), col="wood_mid")
    for k in range(4):
        fr.box(a, (0.05, 0.05, 2 * r - 0.08), (0, -0.02, 0), rot=(0, k * 45, 0), col="iron", bevel=0.0)
    fr.cyl(a, 0.07, 0.07, 0.30, 8, loc=(0, 0.15, 0), rot=(90, 0, 0), col="iron_dark")


def _headframe(a, cx, cy, z0, h, r=0.38, roof=False):
    """Копёр над шурфом на вершине холма: две А-рамы (передняя и задняя) из наклонных ног, ригели, наверху —
    шкив лицом к камере, трос в шурф; у подножия — сруб шурфа. roof — наверху будка-шатёр (уровень 3)."""
    for sy in (-1, 1):
        y = cy + sy * 0.36
        for sx in (-1, 1):
            xb, xt = cx + sx * 0.62, cx + sx * 0.20
            ln = math.hypot(xb - xt, h)
            ang = math.degrees(math.atan2(xt - xb, h))
            a.add(p_box((0.14, 0.14, ln + 0.10), loc=((xb + xt) / 2, y, z0 + h / 2), rot=(0, ang, 0), bevel=0.0),
                  "wood_mid")
        for t in (0.30, 0.62):
            hw = 0.62 - (0.62 - 0.20) * t
            a.add(p_box((2 * hw + 0.10, 0.10, 0.10), loc=(cx, y, z0 + h * t), bevel=0.0), BEAM)
    for sx in (-1, 1):
        a.add(p_box((0.10, 0.82, 0.10), loc=(cx + sx * 0.36, cy, z0 + h * 0.46), bevel=0.0), BEAM)
    a.add(p_box((0.60, 0.92, 0.14), loc=(cx, cy, z0 + h + 0.02), bevel=0.0), BEAM)
    zc = z0 + h + 0.10 + r
    for sy in (-1, 1):
        a.add(p_box((0.10, 0.10, r + 0.12), loc=(cx, cy + sy * 0.20, z0 + h + 0.08 + (r + 0.12) / 2), bevel=0.0), BEAM)
    _sheave(a, Frame((cx, cy - 0.06, zc)), r=r)
    a.add(p_box((0.03, 0.03, zc - z0 + 0.10), loc=(cx - r + 0.03, cy - 0.06, (zc + z0 - 0.10) / 2), bevel=0.0), "rope")
    # сруб шурфа
    for sy in (-1, 1):
        a.add(p_box((1.00, 0.12, 0.14), loc=(cx, cy + sy * 0.34, z0 + 0.02), bevel=0.0), BEAM)
    for sx in (-1, 1):
        a.add(p_box((0.12, 0.60, 0.14), loc=(cx + sx * 0.44, cy, z0 + 0.02), bevel=0.0), BEAM)
    a.add(p_box((0.78, 0.56, 0.03), loc=(cx, cy, z0 + 0.06), bevel=0.0), "black")
    if roof:
        # будка лебёдки за шкивом: дощатый короб с окошком и своя кровля — копёр читается башней
        zb = z0 + h + 0.09
        hb = 0.62
        a.add(p_box((0.62, 0.56, hb), loc=(cx, cy + 0.26, zb + hb / 2), bevel=0.0), PLANKS)
        a.add(p_box((0.20, 0.03, 0.20), loc=(cx + 0.16, cy - 0.025, zb + hb * 0.62), bevel=0.0), "lantern_glow")
        gable_y_at(a, cx, cy + 0.26, 0.62, 0.56, zb + hb, pitch_deg=40, ox=0.08, oy=0.10, bands=1)


def _entry_shed(a, z_eave=2.30, stone=False, w=2.20, sign=True):
    """Крытый вход: двускатная кровля коньком вдоль Y над рельсами, задний край уходит в скалу,
    передние стойки на каменных подушках; фронтон дощатый с вывеской-кирками (stone — каменные столбы)."""
    yf = -1.34
    px = w / 2 - 0.16
    for sx in (-1, 1):
        if stone:
            a.add(p_box((0.34, 0.34, z_eave), loc=(sx * px, yf, z_eave / 2), bevel=0.04), WALL)
        else:
            porch_post(a, sx * px, yf, 0.0, z_eave, s=0.20, col="wood_mid")
    a.add(p_box((w + 0.10, 0.22, 0.22), loc=(0, yf, z_eave - 0.04), bevel=0.0), BEAM)
    for sx in (-1, 1):
        a.add(p_box((0.18, yf - YP + 0.40, 0.18), loc=(sx * px, (yf + YP) / 2 + 0.10, z_eave - 0.04), bevel=0.0), BEAM)
        a.add(p_box((0.12, 0.12, 0.56), loc=(sx * (px - 0.18), yf, z_eave - 0.30), rot=(0, sx * 45, 0), bevel=0.0),
              BEAM)
    d = 1.30
    zr = gable_y_at(a, 0.0, yf + d / 2 - 0.06, w, d, z_eave + 0.06, pitch_deg=30, ox=0.10, oy=0.16, bands=2)
    gable_front(a, 0.0, yf, w, z_eave + 0.06, zr, depth=0.16, face=-1)
    if sign:
        zs = z_eave + 0.06 + (zr - z_eave) * 0.36
        a.add(p_box((0.70, 0.06, 0.28), loc=(0, yf - 0.11, zs), bevel=0.0), "wood_pale")
        for sgn in (-1, 1):
            fr = Frame((0, yf - 0.15, zs), rot=(0, sgn * 42, 0))
            fr.box(a, (0.035, 0.03, 0.32), (0, 0, 0), col="wood_dark", bevel=0.0)
            fr.box(a, (0.20, 0.035, 0.045), (0, -0.005, 0.14), col="iron_dark", bevel=0.0)
    return zr


def _winding_house(a, cx, cy):
    """Машинный дом у копра: каменная коробка, кровля коньком вдоль X, труба, дверь и окно со светом."""
    w, d, h = 1.16, 0.92, 1.36
    a.add(p_box((w + 0.12, d + 0.12, 0.30), loc=(cx, cy, 0.01), bevel=0.04), "stone_dark")
    a.add(p_box((w, d, h), loc=(cx, cy, 0.14 + h / 2), bevel=0.05), WALL)
    quoins(a, cx, cy, w, d, 0.16, 0.14 + h, corners=((-1, -1), (1, -1)))
    plank_door(Shift(a, (cx - 0.22, 0, 0)), cy - d / 2 + 0.06, 0.16, w=0.44, h=0.86)
    win(a, Frame((cx + 0.30, cy - d / 2 - 0.03, 0.94)), w=0.28, h=0.30, lit=True, shutters="roof_dark", sill="stone_light")
    zr = gable_x_at(a, cx, cy, w, d, 0.14 + h, pitch_deg=36, ox=0.12, oy=0.16, bands=1)
    for s in (-1, 1):
        a.add(p_prism([(-d / 2, 0.14 + h), (d / 2, 0.14 + h), (0, zr - 0.05)], 0.14, loc=(cx + s * (w / 2 - 0.07), cy, 0),
                      rot=(0, 0, 90)), WALL)
    stack(a, cx - 0.34, cy + 0.18, 1.20, zr + 0.55 - 1.20, w=0.30, d=0.30)


def _hopper(a, cx, cy, z_top=2.10):
    """Рудный бункер на ногах: короб с рудой, жёлоб вперёд над вагонеткой запасного пути."""
    hb = 0.62
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.13, 0.13, z_top - hb + 0.05), loc=(cx + sx * 0.36, cy + sy * 0.34, (z_top - hb) / 2),
                        bevel=0.0), BEAM)
    a.add(p_box((0.08, 0.80, 0.08), loc=(cx + 0.36, cy, 0.62), rot=(48, 0, 0), bevel=0.0), BEAM)
    a.add(p_box((0.08, 0.80, 0.08), loc=(cx - 0.36, cy, 0.62), rot=(-48, 0, 0), bevel=0.0), BEAM)
    Frame((cx, cy, z_top - hb)).taper(a, (0.62, 0.62), (0.92, 0.88), hb, col=by_normal("wood_light", "wood_mid", "wood_dark", 0.8))
    a.add(p_ico(0.42, 1, loc=(cx, cy, z_top - 0.04), scl=(1.0, 0.95, 0.40), jitter=0.10, rng=random.Random(5), cut=0.0),
          by_normal("ore_rock", "ore_rock_dk", "ore_rock_dk", 0.3))
    for k, (dx, dy) in enumerate(((-0.14, -0.10), (0.16, 0.04), (0.0, 0.18), (0.20, -0.16))):
        ore_chunk(a, "gold", 50 + k, loc=(cx + dx, cy + dy, z_top + 0.06), size=0.12)
    # жёлоб: наклонный лоток вперёд
    ln = 0.80
    a.add(p_box((0.40, ln, 0.05), loc=(cx, cy - 0.62, z_top - hb - 0.16), rot=(-28, 0, 0), bevel=0.0), "wood_mid")
    for sx in (-1, 1):
        a.add(p_box((0.05, ln, 0.14), loc=(cx + sx * 0.20, cy - 0.62, z_top - hb - 0.12), rot=(-28, 0, 0), bevel=0.0),
              "wood_dark")


def _gold_crate(a, rng, cx=1.28, cy=-1.38, s=0.50):
    a.add(p_box((s, s, s * 0.84), loc=(cx, cy, s * 0.42), rot=(0, 0, 12), bevel=0.03),
          by_normal("wood_pale", "wood_light", "wood_dark", 0.7))
    fr = Frame((cx, cy, s * 0.84), rz=12)
    for sy in (-1, 1):
        fr.box(a, (s + 0.03, 0.06, 0.08), (0, sy * s / 2, -s * 0.42), col="wood_dark", bevel=0.012)
    for k in range(6):
        fr.ico(a, rng.uniform(0.07, 0.10), loc=(rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 0.02),
               scl=(1, 0.85, 0.8), col=NUG)


def _l2(a):
    """Штольня с копром: на вершине холма — деревянный копёр со шкивом над шурфом; вход под двускатной
    кровлей на столбах; справа запасной путь со второй вагонеткой. Холм, рама входа, вагонетка с золотом,
    куча руды и инструмент — прежние."""
    rng = random.Random(7)
    _rock(a)
    _adit(a)
    _portal(a)
    _entry_shed(a)
    lantern(a, Frame((0.62, YF - 0.30, 1.40)))
    _rails(a)
    _minecart(a, Frame((0, -1.52, 0)), rng)
    _headframe(a, -0.20, 0.66, 2.50, 1.62)
    _siding(a, random.Random(23))
    _ore_pile(a, -1.30, -1.30, 5)
    _pickaxe(a, Frame((PX + 0.30, YP - 0.10, 0.0), rot=(12, -14, 0)))


def _l3(a):
    """Рудник: каменный портал с аркой и замком, над ним кровля входа на каменных столбах; на вершине — высокий
    копёр с будкой-шатром; слева машинный дом с трубой, справа рудный бункер с жёлобом над вагонеткой
    запасного пути; фонари у портала."""
    rng = random.Random(7)
    _rock(a)
    _adit(a)
    arched_wall(a, -1.22, 1.22, YP, 0.40, 0.0, 2.40, -0.70, 0.70, 1.26)
    a.add(p_box((2.56, 0.50, 0.16), loc=(0, YP, 2.44), bevel=0.0), "stone_light")
    _entry_shed(a, z_eave=2.62, stone=True, w=2.44)
    for sx in (-1, 1):
        wall_lamp(a, sx * 0.98, YP - 0.22, 1.86, out=(sx * 0.3, -0.95))
    _rails(a)
    _minecart(a, Frame((0, -1.52, 0)), rng)
    _headframe(a, -0.20, 0.66, 2.50, 2.10, r=0.40, roof=True)
    _winding_house(a, -1.90, -1.30)
    _siding(a, random.Random(23), x=1.84, y0=-0.70, cart_y=-1.42)
    _hopper(a, 1.84, -0.34, z_top=2.10)


def evolve(a, level):
    """2: штольня с копром. 3: рудник с машинным домом и бункером."""
    (_l2 if level == 2 else _l3)(a)
