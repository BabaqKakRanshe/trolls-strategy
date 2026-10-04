"""
Кожевня: синий односкатный навес-пристройка и перед ним две высокие сушильные рамы
с растянутыми шкурами. Рамы — «паруса» здания: светлый неровный контур шкуры на тёмной
раме читается с игровой дистанции раньше всего остального, поэтому они стоят лицом к
камере и разведены в стороны — в проёме между ними видна мездрильная колода под навесом.
"""
import math, random
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_prism
from vitaria_buildings.common import Frame, brace, STONE_TOP
from vitaria_buildings.common import plank_door
from vitaria_buildings.levels import (Shift, quoins, masonry, stack, win, gable_x_at, gable_y_at, gable_end, gable_front,
                                    wall_lamp, WALL, BEAM, PLANKS)

NAME = "Bld_Tannery"
TITLE = "Кожевня"
TARGET = (4.2, 3.4, 3.0)

SX = 1.50                 # оси угловых стоек: навес уже рам, крайние стойки рам выходят за кровлю
YF, YB = 0.40, 1.40       # оси передних и задних стоек
F = 0.20                  # верх каменной площадки под навесом
ZF, ZB = 2.05, 2.62       # верх переднего прогона и заднего бруса: скат к фасаду, кровлю видно сверху
PITCH = math.atan2(ZB - ZF, YB - YF)


# ---------------------------------------------------------------------------------------
# навес
# ---------------------------------------------------------------------------------------
def _shed(a):
    # мокрая работа идёт на камне; площадка короче навеса по фронту — перед ней стоят рамы
    a.add(p_box((2 * SX + 0.40, YB - YF + 0.50, F + 0.14), loc=(0, (YF + YB) / 2 + 0.03, (F - 0.14) / 2),
                bevel=0.06), STONE_TOP)
    # стойки тоньше прогонов (0.18 против 0.22) и утоплены в них: иначе грани совпадают
    for sx in (-1, 1):
        for y, zt in ((YF, ZF), (YB, ZB)):
            a.add(p_box((0.18, 0.18, zt - F - 0.01), loc=(sx * SX, y, (F - 0.03 + zt - 0.04) / 2), bevel=0.04),
                  "wood_dark")
    for y, zt in ((YF, ZF), (YB, ZB)):
        a.add(p_box((2 * SX + 0.40, 0.22, 0.24), loc=(0, y, zt - 0.12), bevel=0.045), "wood_dark")
    # боковые жерди: на них висят шкуры, заодно закрывают бока навеса
    for sx in (-1, 1):
        a.add(p_box((0.16, YB - YF + 0.30, 0.16), loc=(sx * SX, (YF + YB) / 2, 1.72), bevel=0.035), "wood_dark")
        brace(a, sx * (SX - 0.27), YF, ZF - 0.42, length=0.56, angle=-sx * 45)
    # задняя стена снаружи стоек, с зазором 2 см: доска и брус не делят одну плоскость
    yb = YB + 0.16
    z0, z1, n = F - 0.02, ZB + 0.10, 7          # верхняя доска заходит под свес кровли
    ph = (z1 - z0) / n
    for i in range(n):
        a.add(p_box((2 * SX + 0.22, 0.10, ph + 0.03), loc=(0, yb, z0 + (i + 0.5) * ph), bevel=0.03),
              "wood_light" if i % 2 else "wood_mid")
    for x in (-0.65, 0.65):
        a.add(p_box((0.15, 0.06, z1 - z0 - 0.1), loc=(x, yb + 0.07, (z0 + z1) / 2 - 0.05), bevel=0.02), "wood_dark")
    # шкура, прибитая к тыльной стене: зад остаётся проще фасада, но говорит о том же ремесле
    fr = Frame((0, yb + 0.085, 1.42), rz=180)
    rng = random.Random(7)
    pts = _hide_outline(0.46, 0.52, rng)
    fr.prism(a, pts, 0.05, col=lambda f: "hide" if abs(f.normal.y) > 0.6 else "hide_dark")
    for i in (3, 9, 16, 22):
        fr.cyl(a, 0.03, 0.03, 0.07, 5, loc=(pts[i][0] * 0.9, 0.0, pts[i][1] * 0.9), rot=(90, 0, 0), col="wood_dark")


def _roof(a):
    """Односкатная кровля по схеме gable_roof: обрешётка, два пояса, причелины."""
    p = PITCH
    deg = math.degrees(p)
    up = Vector((0, math.cos(p), math.sin(p)))       # вверх по скату, к тылу
    nrm = Vector((0, -math.sin(p), math.cos(p)))     # наружу
    o = Vector((0, YF, ZF))                          # кромка переднего прогона
    s0 = -0.34 / math.cos(p)                         # передний свес
    s1 = (YB - YF + 0.22) / math.cos(p)              # задний свес
    L, mid = s1 - s0, (s0 + s1) / 2
    W = 2 * SX + 0.48
    a.add(p_box((W, L, 0.10), loc=o + up * mid + nrm * 0.05, rot=(deg, 0, 0), bevel=0.03), "wood_dark")
    rl = L / 2
    for k in range(2):                               # верхний пояс светлее, свес уходит в тень
        c = o + up * (s1 - (k + 0.5) * rl) + nrm * 0.135
        a.add(p_box((W, rl + 0.10, 0.17), loc=c, rot=(deg, 0, 0), bevel=0.05), "roof" if k == 0 else "roof_dark")
    for sx in (-1, 1):
        c = o + up * mid + nrm * 0.12 + Vector((sx * (W / 2 + 0.04), 0, 0))
        a.add(p_box((0.12, L + 0.08, 0.26), loc=c, rot=(deg, 0, 0), bevel=0.035), "wood_dark")
    # доска по верхней кромке: закрывает торец кровли со спины и держит верхнюю линию силуэта
    top = o + up * s1
    a.add(p_box((W + 0.08, 0.12, 0.30), loc=(0, top.y + 0.04, top.z + 0.05), bevel=0.03), "wood_dark")


# ---------------------------------------------------------------------------------------
# сушильные рамы
# ---------------------------------------------------------------------------------------
def _hide_outline(hw, hh, rng):
    """Контур растянутой шкуры в XZ: тело, четыре лапы к углам рамы, шея сверху, хвост снизу."""
    base = [(0.0, -0.92), (0.22, -0.80), (0.58, -0.84), (0.98, -1.0), (0.86, -0.68), (0.74, -0.40),
            (0.80, -0.02), (0.76, 0.34), (0.86, 0.66), (1.0, 0.92), (0.62, 0.80), (0.30, 0.84), (0.16, 1.0)]
    right = base[1:]
    left = [(-x, z) for x, z in reversed(right)]
    pts = [base[0]] + right + left
    return [(x * hw + rng.uniform(-0.02, 0.02), z * hh + rng.uniform(-0.02, 0.02)) for x, z in pts]


def _tie(a, fr, p, q, r=0.03):
    """Шнурок от кромки шкуры к раме: на 4 см заходит в шкуру (точка шнуровки) и на 3 см в брус."""
    d = Vector((q[0] - p[0], 0, q[1] - p[1]))
    L = d.length
    d.normalize()
    s = Vector((p[0], 0, p[1])) - d * 0.04
    fr.cyl(a, r, r, L + 0.07, 5, loc=tuple(s), rot=(0, math.degrees(math.atan2(d.x, d.z)), 0), col="rope")


def _hide_frame(a, fr, w, h, zb, body, spine, seed):
    """Сушильная рама в осях fr: стойки с подкосами назад, две перекладины, шкура на шнуровке."""
    rng = random.Random(seed)
    zt = h - 0.26
    lean = math.degrees(math.atan2(0.42, 1.30))
    for sx in (-1, 1):
        fr.box(a, (0.15, 0.15, h + 0.06), (sx * w / 2, 0, (h - 0.06) / 2), col="wood_dark", bevel=0.035)
        fr.cyl(a, 0.115, 0.0, 0.14, 4, loc=(sx * w / 2, 0, h), spin=45, col="wood_dark")
        # подкос назад: без него «парус» ляжет от первого ветра, а сбоку рама читается мольбертом
        fr.box(a, (0.10, 0.10, 1.44), (sx * w / 2, 0.21, 0.63), rot=(lean, 0, 0), col="wood_dark", bevel=0.025)
    for z in (zb, zt):
        fr.box(a, (w + 0.30, 0.17, 0.14), (0, 0, z), col="wood_mid", bevel=0.035)

    ix, zlo, zhi = w / 2 - 0.075, zb + 0.07, zt - 0.07          # внутренний проём рамы
    zc = (zlo + zhi) / 2
    hw, hh = ix - 0.11, (zhi - zlo) / 2 - 0.10
    pts = _hide_outline(hw, hh, rng)
    fr.prism(a, pts, 0.05, loc=(0, 0, zc), col=lambda f: body if abs(f.normal.y) > 0.6 else "hide_dark")
    # хребет темнее: на дистанции он превращает светлый прямоугольник в шкуру животного
    sp = [(-0.10, -hh * 0.72), (0.10, -hh * 0.72), (0.13, -hh * 0.05), (0.09, hh * 0.74), (-0.09, hh * 0.74),
          (-0.13, -hh * 0.05)]
    sp = [(x + rng.uniform(-0.015, 0.015), z) for x, z in sp]
    fr.prism(a, sp, 0.064, loc=(0, 0, zc), col=spine)

    # Шнуровка лучами от центра шкуры до проёма: параллельные горизонтальные шнурки
    # на первом рендере складывались в перекладины лестницы, лучи читаются как растяжка.
    neck = ((pts[12][0] + pts[13][0]) / 2, (pts[12][1] + pts[13][1]) / 2)
    for x, z in [pts[i] for i in (0, 2, 3, 6, 9, 11, 14, 16, 19, 22, 23)] + [neck]:
        kx = ix / abs(x) if abs(x) > 1e-3 else 9.0
        kz = ((zhi - zc) if z > 0 else (zc - zlo)) / abs(z) if abs(z) > 1e-3 else 9.0
        k = min(kx, kz)
        _tie(a, fr, (x, z + zc), (x * k, z * k + zc))


# ---------------------------------------------------------------------------------------
# под навесом
# ---------------------------------------------------------------------------------------
def _flesh_beam(a):
    """Мездрильная колода: наклонное бревно, верх на козлах, через него перекинута сырая шкура."""
    p0 = Vector((-0.62, 0.95, F + 0.02))
    p1 = Vector((0.56, 0.95, F + 0.64))
    d = (p1 - p0).normalized()
    th = math.degrees(math.atan2(d.x, d.z))
    col = lambda f: "wood_pale" if abs(f.normal.dot(d)) > 0.9 else ("wood_light" if f.normal.z > 0.3 else "wood_mid")
    a.add(p_cyl(0.14, 0.14, (p1 - p0).length + 0.30, 8, loc=p0 - d * 0.10, rot=(0, th, 0), bevel=0.02,
                spin=22.5), col)
    # козлы: ноги разведены по Y, иначе сбоку колода висит на одной палке
    for sy in (-1, 1):
        a.add(p_box((0.10, 0.10, 0.66), loc=(p1.x, 0.95 + sy * 0.16, F + 0.28), rot=(sy * 24, 0, 0),
                    bevel=0.025), "wood_dark")
    # шкура ниже козел по колоде: иначе ноги козел протыкают свисающие полы
    hf = Frame(tuple(p0 + d * ((p1 - p0).length - 0.45)), rot=(0, th - 90, 0))   # X вдоль колоды
    # шкура светлая: под навесом она в тени, бурая сливалась с колодой
    hf.box(a, (0.64, 0.40, 0.045), (0, 0, 0.162), col="hide_light", bevel=0.015)
    flap = [(-0.32, 0.17), (0.32, 0.17), (0.30, -0.16), (0.17, -0.30), (0.04, -0.20), (-0.10, -0.32),
            (-0.24, -0.22), (-0.32, -0.12)]
    for sy in (-1, 1):
        hf.prism(a, flap, 0.045, loc=(0, sy * 0.21, 0.0), rot=(sy * 14, 0, 0),
                 col=lambda f: "hide_light" if abs(f.normal.y) > 0.6 else "hide")


def _side_hides(a):
    """Шкуры на боковых жердях: закрывают открытые бока навеса и видны в ракурсе 3/4."""
    specs = ((1, "hide", 0.93, 0), (-1, "hide_light", 0.90, 1))
    for sx, col, yc, seed in specs:
        rng = random.Random(40 + seed)
        pts = [(-0.40, 1.84), (0.40, 1.84), (0.42, 1.48), (0.52, 1.08), (0.30, 1.20), (0.12, 1.00),
               (-0.08, 1.12), (-0.28, 0.98), (-0.48, 1.12), (-0.42, 1.48)]
        pts = [(u + rng.uniform(-0.02, 0.02), z + (rng.uniform(-0.03, 0.03) if z < 1.8 else 0)) for u, z in pts]
        a.add(p_prism(pts, 0.05, loc=(sx * (SX - 0.14), yc, 0), rot=(0, 0, 90)),
              lambda f, c=col: c if abs(f.normal.x) > 0.6 else "hide_dark")


# ---------------------------------------------------------------------------------------
# реквизит
# ---------------------------------------------------------------------------------------
def _leather_stack(a, fr, n=7, seed=3):
    """Готовая кожа: пласты внахлёст, цвета чередуются, стопка перетянута двумя верёвками на поддоне."""
    rng = random.Random(seed)
    for sx in (-1, 1):
        fr.box(a, (0.12, 0.58, 0.09), (sx * 0.27, 0, 0.045), col="wood_dark", bevel=0.02)
    for j in (-1, 0, 1):
        fr.box(a, (0.80, 0.16, 0.05), (0, j * 0.2, 0.113), col="wood_light", bevel=0.015)
    z, t = 0.135, 0.06
    for k in range(n):
        fr.box(a, (0.66 - 0.03 * (k % 2), 0.48 - 0.02 * (k % 3 == 1), t),
               (rng.uniform(-0.012, 0.012), rng.uniform(-0.008, 0.008), z + t / 2),
               rot=(0, 0, rng.uniform(-2.0, 2.0)), col=("leather", "leather_dark")[k % 2], bevel=0.02)
        z += t - 0.005                              # внахлёст на 5 мм: пласты без щелей, фаска рисует шов
    top = z + 0.005
    for x in (-0.17, 0.17):
        fr.box(a, (0.07, 0.57, 0.035), (x, 0, top + 0.007), col="rope", bevel=0.01)
        for sy in (-1, 1):
            fr.box(a, (0.07, 0.035, top - 0.11), (x, sy * 0.28, (top + 0.11) / 2), col="rope", bevel=0.01)


def _vat(a, fr, r=0.42, h=0.52, fill="water", seg=14):
    """
    Чан/ведро: сплошной корпус, светлый венец сверху, зеркало жидкости внутри венца.
    common.tub/bucket кладут сплошной обод поверх зеркала — сверху чан читался бочкой с крышкой.
    """
    body = lambda f: "wood_light" if f.normal.z > 0.55 else ("wood_dark" if f.normal.z < -0.55 else "wood_mid")
    fr.cyl(a, r * 0.9, r, h, seg, col=body, bevel=0.025)
    for t in (0.25, 0.72):
        rr = r * (0.9 + 0.1 * t) + 0.018
        fr.cyl(a, rr, rr, 0.05, seg, loc=(0, 0, h * t - 0.025), col="iron_dark")
    fr.cyl(a, r * 0.8, r * 0.8, 0.01, seg, loc=(0, 0, h + 0.003), col=fill)


def _hide_roll(a, fr, L=0.62, r=0.12, col="hide"):
    """Свёрнутая шкура вдоль X рамы: светлые торцы с тёмной сердцевиной-спиралью, две перевязки."""
    inv = fr.m.to_3x3().inverted()
    cap = lambda f: "hide_light" if abs((inv @ f.normal).normalized().x) > 0.7 else col
    fr.cyl(a, r, r, L, 8, loc=(-L / 2, 0, r), rot=(0, 90, 0), col=cap, bevel=0.02)
    for s in (-1, 1):
        fr.cyl(a, r * 0.5, r * 0.5, 0.012, 8, loc=(s * (L / 2 + 0.003), 0, r), rot=(0, 90 * s, 0), col="hide_dark")
    for t in (-0.26, 0.26):
        fr.cyl(a, r + 0.014, r + 0.014, 0.055, 8, loc=(t * L - 0.0275, 0, r), rot=(0, 90, 0), col="rope")


def build(a):
    _shed(a)
    _roof(a)
    _flesh_beam(a)
    _side_hides(a)

    # Рамы-«паруса» разведены к краям и чуть развёрнуты к центру, как кулисы, и вынесены
    # на 0.6 м перед навесом. Верх рам заведён на кровлю: при высоте 2.3–2.4 верхняя
    # перекладина с игровой камеры ложилась ровно на линию свеса.
    _hide_frame(a, Frame((-1.20, -0.62, 0), rz=9), w=1.50, h=2.62, zb=0.62, body="hide_light", spine="hide",
                seed=1)
    _hide_frame(a, Frame((1.24, -0.58, 0), rz=-11), w=1.42, h=2.48, zb=0.60, body="hide", spine="hide_dark",
                seed=2)

    # дубильный чан с бурым раствором и мешалкой, рядом чан с водой
    _vat(a, Frame((0.02, -1.14, 0)), r=0.42, h=0.52, fill="leather_dark")
    a.add(p_cyl(0.035, 0.03, 1.25, 6, loc=(0.15, -1.04, 0.2), rot=(18, -22, 0)), "wood_light")
    _vat(a, Frame((1.20, -1.24, 0)), r=0.35, h=0.46, fill="water")
    _vat(a, Frame((0.66, -1.50, 0)), r=0.15, h=0.26, fill="water", seg=10)

    _leather_stack(a, Frame((-1.22, -1.30, 0), rz=7))
    _hide_roll(a, Frame((-0.02, -0.16, 0), rz=-6), L=0.66, r=0.12, col="hide")
    _hide_roll(a, Frame((0.10, -0.38, 0), rz=10), L=0.56, r=0.11, col="hide_dark")


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): навес -> мастерская кожевника -> кожевня с сушильным чердаком
# =========================================================================================
X0, X1, Y0, Y1 = -1.55, 1.55, 0.28, 1.56          # коробка мастерской на месте навеса


def _platform(a):
    a.add(p_box((2 * SX + 0.40, YB - YF + 0.50, F + 0.14), loc=(0, (YF + YB) / 2 + 0.03, (F - 0.14) / 2),
                bevel=0.06), STONE_TOP)


def _frames(a):
    _hide_frame(a, Frame((-1.20, -0.62, 0), rz=9), w=1.50, h=2.62, zb=0.62, body="hide_light", spine="hide",
                seed=1)
    _hide_frame(a, Frame((1.24, -0.58, 0), rz=-11), w=1.42, h=2.48, zb=0.60, body="hide", spine="hide_dark",
                seed=2)


def _pit(a, x, y, w, d, fill):
    """Дубильная яма: каменная обкладка в уровень земли, раствор на 6 см ниже края, мешалка."""
    for sy in (-1, 1):
        a.add(p_box((w + 0.16, 0.12, 0.22), loc=(x, y + sy * (d / 2 + 0.02), 0.07), bevel=0.0), "stone_light")
    for sx in (-1, 1):
        a.add(p_box((0.12, d + 0.04, 0.20), loc=(x + sx * (w / 2 + 0.02), y, 0.06), bevel=0.0), "stone_light")
    a.add(p_box((w, d, 0.04), loc=(x, y, 0.10), bevel=0.0), fill)


def _hung_hide(a, x, y, z, col, seed, face=-1):
    """Шкура, перекинутая через жердь сушильни: два полотнища вниз от жерди, лицом к камере."""
    rng = random.Random(seed)
    pts = [(-0.24, 0.0), (0.24, 0.0), (0.26, -0.34), (0.16, -0.52), (0.04, -0.44), (-0.10, -0.56), (-0.22, -0.40)]
    pts = [(u + rng.uniform(-0.02, 0.02), v + rng.uniform(-0.02, 0.02) * (v < 0)) for u, v in pts]
    a.add(p_prism(pts, 0.04, loc=(x, y, z)), lambda f, c=col: c if abs(f.normal.y) > 0.6 else "hide_dark")


def _l2(a):
    """Мастерская кожевника: навес закрыт — каменный цоколь, дощатые стены на стойках, кровля коньком вдоль Y
    фронтоном к камере (вместо одного ската), на фронтоне — прибитая шкура-вывеска и люк чердака; дверь и
    окна; перед мастерской рамы со шкурами как у навеса, чан с раствором стоит в каменной дубильной яме."""
    _platform(a)
    zp, zw = F + 0.46, 2.12
    a.add(p_box((X1 - X0, Y1 - Y0, zp - F + 0.02), loc=(0, (Y0 + Y1) / 2, (F + zp) / 2), bevel=0.04), WALL)
    a.add(p_box((X1 - X0 - 0.06, Y1 - Y0 - 0.06, zw - zp), loc=(0, (Y0 + Y1) / 2, (zp + zw) / 2), bevel=0.03),
          "wood_mid")
    for x in (X0, -0.52, 0.52, X1):
        a.add(p_box((0.16, 0.16, zw - zp), loc=(x, Y0 - 0.02, (zp + zw) / 2), bevel=0.0), BEAM)
    for x in (X0, X1):
        a.add(p_box((0.16, 0.16, zw - zp), loc=(x, Y1 + 0.02, (zp + zw) / 2), bevel=0.0), BEAM)
    a.add(p_box((X1 - X0 + 0.10, 0.18, 0.16), loc=(0, Y0 - 0.03, zw - 0.06), bevel=0.0), BEAM)
    plank_door(a, Y0 + 0.02, F, w=0.58, h=1.20, x=0.02)
    win(a, Frame((-1.04, Y0 - 0.04, 1.28)), w=0.38, h=0.36, shutters="wood_light", sill="wood_dark")
    win(a, Frame((1.04, Y0 - 0.04, 1.28)), w=0.38, h=0.36, shutters="wood_light", sill="wood_dark")
    yc = (Y0 + Y1) / 2
    zr = gable_y_at(a, 0.0, yc, X1 - X0, Y1 - Y0, zw, pitch_deg=32, ox=0.16, oy=0.12, bands=2)
    gable_front(a, 0.0, Y0 + 0.06, X1 - X0, zw - 0.02, zr, depth=0.12, face=-1)
    gable_front(a, 0.0, Y1 - 0.06, X1 - X0, zw - 0.02, zr, depth=0.12, face=1, boards=False)
    # шкура-вывеска на фронтоне, под ней люк чердака
    rng = random.Random(11)
    pts = _hide_outline(0.40, 0.36, rng)
    fr = Frame((0.0, Y0 - 0.02, zw + 0.42))
    fr.prism(a, pts, 0.05, col=lambda f: "hide_light" if abs(f.normal.y) > 0.6 else "hide_dark")
    for i in (3, 9, 16, 22):
        fr.cyl(a, 0.03, 0.03, 0.07, 5, loc=(pts[i][0] * 0.9, -0.03, pts[i][1] * 0.9), rot=(90, 0, 0), col="wood_dark")
    _side_hides(a)
    _frames(a)
    _pit(a, 0.02, -1.14, 0.96, 0.62, "leather_dark")
    _vat(a, Frame((1.20, -1.24, 0)), r=0.35, h=0.46, fill="water")
    a.add(p_cyl(0.035, 0.03, 1.25, 6, loc=(0.15, -1.04, 0.05), rot=(18, -22, 0)), "wood_light")
    _leather_stack(a, Frame((-1.22, -1.30, 0), rz=7))


def _l3(a):
    """Кожевня с сушильным чердаком: низ — камень с дверью и окнами со светом, верх — открытая сушильня:
    стойки, жердь и редкие жалюзи, между ними висят шкуры; кровля круче, каменная труба варочной печи;
    перед зданием — две дубильные ямы (бурая и красная), рамы со шкурами и стопка кожи."""
    _platform(a)
    zg, zl = 1.52, 2.86
    masonry(a, 0.0, (Y0 + Y1) / 2, X1 - X0, Y1 - Y0, F, zg, corners=((-1, -1), (1, -1)))
    plank_door(a, Y0 + 0.02, F, w=0.58, h=1.12, x=0.02)
    for x in (-1.04, 1.04):
        win(a, Frame((x, Y0 - 0.04, 1.02)), w=0.36, h=0.34, lit=True, shutters="roof_dark", sill="stone_light")
    # сушильня: пол-балка, угловые и промежуточные стойки, жалюзи сверху и снизу, жердь со шкурами
    a.add(p_box((X1 - X0 + 0.12, Y1 - Y0 + 0.12, 0.14), loc=(0, (Y0 + Y1) / 2, zg + 0.05), bevel=0.0), BEAM)
    a.add(p_box((X1 - X0 - 0.30, Y1 - Y0 - 0.30, zl - zg), loc=(0, (Y0 + Y1) / 2, (zg + zl) / 2), bevel=0.0), "black")
    for x in (X0 + 0.05, -0.80, 0.0, 0.80, X1 - 0.05):
        for y in (Y0 + 0.05, Y1 - 0.05):
            a.add(p_box((0.14, 0.14, zl - zg), loc=(x, y, (zg + zl) / 2), bevel=0.0), BEAM)
    for k in range(3):
        z = zl - 0.12 - k * 0.12
        a.add(p_box((X1 - X0, 0.04, 0.07), loc=(0, Y0 + 0.02, z), rot=(-30, 0, 0), bevel=0.0), "wood_mid")
    a.add(p_box((X1 - X0, 0.07, 0.10), loc=(0, Y0 - 0.005, zg + 0.40), bevel=0.0), "wood_mid")
    for k, (x, col) in enumerate(((-1.18, "hide_light"), (-0.40, "hide"), (0.40, "hide_light"), (1.18, "hide"))):
        _hung_hide(a, x, Y0 + 0.10, zl - 0.46, col, 30 + k)
    zr = gable_x_at(a, 0.0, (Y0 + Y1) / 2, X1 - X0, Y1 - Y0, zl, pitch_deg=40, ox=0.20, oy=0.06, bands=2)
    for sx in (-1, 1):
        gable_end(a, sx * (X1 - 0.02), (Y0 + Y1) / 2, Y1 - Y0, zl - 0.02, zr, face=sx)
    # фронтон-ризалит посередине: сушильня поднимается выше карниза, своя кровля, балка с шкурой на крюке
    cw, zc = 1.10, zl + 0.36
    tp = math.tan(math.radians(40))
    zr2 = zc + 0.08 + (cw / 2) * math.tan(math.radians(42))
    yb = (Y0 + Y1) / 2 - (zr - zr2 + 0.10) / tp
    a.add(p_box((cw, 0.14, zc - zl + 0.04), loc=(0.0, Y0 + 0.07, (zl + zc) / 2), bevel=0.0), "wood_mid")
    gable_y_at(a, 0.0, (Y0 + yb) / 2, cw, yb - Y0, zc, pitch_deg=42, ox=0.10, oy=0.18, bands=1)
    gable_front(a, 0.0, Y0 + 0.07, cw, zc - 0.02, zr2, depth=0.14, face=-1, boards=False)
    a.add(p_box((0.44, 0.04, 0.46), loc=(0.0, Y0 - 0.01, zl + 0.20), bevel=0.0), "black")
    a.add(p_box((0.12, 0.62, 0.12), loc=(0.0, Y0 - 0.22, zr2 - 0.30), bevel=0.0), BEAM)
    a.add(p_box((0.03, 0.03, 0.40), loc=(0.0, Y0 - 0.48, zr2 - 0.52), bevel=0.0), "rope")
    _hung_hide(a, 0.0, Y0 - 0.48, zr2 - 0.70, "hide", 41)
    stack(a, -1.00, (Y0 + Y1) / 2 + 0.30, 2.20, zr + 0.40 - 2.20, w=0.40)
    _frames(a)
    _pit(a, -0.30, -1.20, 0.72, 0.58, "leather_dark")
    _pit(a, 0.62, -1.26, 0.62, 0.50, "cloth_dark")
    a.add(p_cyl(0.035, 0.03, 1.20, 6, loc=(-0.20, -1.12, 0.05), rot=(18, -22, 0)), "wood_light")
    _leather_stack(a, Frame((-1.30, -1.36, 0), rz=7))
    wall_lamp(a, 0.50, Y0 - 0.04, 1.30, out=(0.3, -0.95))


def evolve(a, level):
    """2: мастерская кожевника. 3: кожевня с сушильным чердаком."""
    (_l2 if level == 2 else _l3)(a)
