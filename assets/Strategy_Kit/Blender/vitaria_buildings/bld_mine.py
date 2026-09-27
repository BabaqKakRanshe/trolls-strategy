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
from build_vitaria import p_box, p_cyl, p_ico, by_normal, rock_face_color, ore_chunk, TM
from vitaria_buildings.common import Frame, lantern, STONE_TOP

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
