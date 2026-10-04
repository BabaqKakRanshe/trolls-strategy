"""
Лесозаготовка: синий шатёр на четырёх столбах, массивный пень с вогнанным топором и
штабель длинных брёвен. Силуэт держат горизонталь брёвен и пирамида шатра с кремовыми
фестонами — в отличие от кожевни, где работают вертикальные рамы и односкатный навес.
"""
import math, random
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, p_prism
from vitaria_buildings.common import Frame, axe, brace, STONE_TOP
from vitaria_buildings.common import plank_door
from vitaria_buildings.levels import (Shift, plinth, log_box, win, gable_x_at, gable_y_at, gable_end, gable_front,
                                    stovepipe, stack, shed_roof, porch_post, log_ends, RUBBLE, BEAM, PLANKS)

NAME = "Bld_LumberCamp"
TITLE = "Лесозаготовка"
TARGET = (4.2, 3.4, 3.0)

XT, YT = -1.06, 0.84      # центр шатра: слева сзади, фасад отдан пню
PX, PY = 0.80, 0.58       # полуразнос стоек
ZE = 2.10                 # верх обвязки: ниже фестоны закрывают стойку с топорами от игровой камеры
SX_, SY_ = -1.12, -0.82   # центр пня


# ---------------------------------------------------------------------------------------
# шатёр
# ---------------------------------------------------------------------------------------
def _flap_row(a, fr, length, n, col="cream"):
    """Ряд фестонов вдоль X рамы: соседние лопасти смыкаются боками, низ — зубец."""
    fw = length / n
    for i in range(n):
        x = -length / 2 + fw * (i + 0.5)
        fr.prism(a, [(-fw / 2, 0.02), (fw / 2, 0.02), (fw / 2, -0.12), (0.0, -0.25), (-fw / 2, -0.12)], 0.05,
                 loc=(x, 0, 0), col=col)


def _tent(a):
    """Четыре столба на каменных подпятниках, обвязка, синяя пирамида, юбка с фестонами, флажок."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = XT + sx * PX, YT + sy * PY
            a.add(p_box((0.30, 0.30, 0.18), loc=(x, y, 0.05), bevel=0.05), STONE_TOP)
            a.add(p_box((0.17, 0.17, ZE - 0.14), loc=(x, y, 0.10 + (ZE - 0.14) / 2), bevel=0.04), "wood_dark")
    # обвязка: брусья по X и по Y разной высоты — верхние грани не совпадают на перекрёстках
    for sy in (-1, 1):
        a.add(p_box((2 * PX + 0.34, 0.19, 0.19), loc=(XT, YT + sy * PY, ZE - 0.095), bevel=0.04), "wood_dark")
        for sx in (-1, 1):
            brace(a, XT + sx * (PX - 0.27), YT + sy * PY, ZE - 0.42, length=0.56, angle=-sx * 45)
    for sx in (-1, 1):
        a.add(p_box((0.19, 2 * PY + 0.34, 0.17), loc=(XT + sx * PX, YT, ZE - 0.15), bevel=0.04), "wood_dark")

    # полотно: пирамида над обвязкой; кромка юбкой, фестоны кремовые — как у тента рынка
    zb = ZE + 0.02
    hx, hy, ht = PX + 0.30, PY + 0.28, 0.72
    Frame((XT, YT, zb), s=(hx, hy, 1.0)).cyl(a, math.sqrt(2), 0.0, ht, 4, spin=45, col="roof")
    for sy in (-1, 1):
        a.add(p_box((2 * hx + 0.07, 0.07, 0.14), loc=(XT, YT + sy * hy, zb - 0.06), bevel=0.02), "roof_dark")
        _flap_row(a, Frame((XT, YT + sy * (hy + 0.004), zb - 0.10)), 2 * hx + 0.07, 6)
    for sx in (-1, 1):
        a.add(p_box((0.07, 2 * hy + 0.08, 0.13), loc=(XT + sx * hx, YT, zb - 0.065), bevel=0.02), "roof_dark")
        _flap_row(a, Frame((XT + sx * (hx + 0.004), YT, zb - 0.10), rz=90), 2 * hy + 0.07, 5)
    # навершие и флажок: единственная тонкая вертикаль над шатром, держит силуэт
    zt = zb + ht
    a.add(p_cyl(0.035, 0.03, 0.40, 6, loc=(XT, YT, zt - 0.12)), "wood_dark")
    a.add(p_ico(0.07, 1, loc=(XT, YT, zt - 0.02)), "wood_dark")
    a.add(p_prism([(0.0, 0.0), (0.32, -0.09), (0.0, -0.19)], 0.04, loc=(XT + 0.02, YT, zt + 0.27)), "cream")


# ---------------------------------------------------------------------------------------
# под шатром
# ---------------------------------------------------------------------------------------
def _axe_rack(a, fr):
    """Стойка с топорами: козлы с низкой жердью, топорища пяткой в землю и опираются на жердь."""
    for sx in (-1, 1):
        fr.box(a, (0.10, 0.10, 0.70), (sx * 0.52, 0, 0.33), col="wood_mid", bevel=0.025)
        fr.box(a, (0.10, 0.40, 0.08), (sx * 0.52, 0, 0.04), col="wood_dark", bevel=0.02)
    fr.box(a, (1.22, 0.12, 0.10), (0, 0, 0.62), col="wood_mid", bevel=0.03)
    fr.box(a, (1.22, 0.10, 0.08), (0, 0.08, 0.14), col="wood_dark", bevel=0.02)
    for x, dbl, L in ((-0.30, False, 0.86), (0.02, True, 0.92), (0.32, False, 0.84)):
        lean = math.degrees(math.atan2(0.14, 0.62))       # топорище ложится на переднюю грань жерди
        axe(a, fr.sub((x, -0.20, 0.0), rot=(-lean, 0, 0)), L=L, double=dbl)


def _grindstone(a, fr):
    """Точильный круг лицом к -Y рамы: камень на оси между двумя «А», корыто с водой, рукоять."""
    inv = fr.m.to_3x3().inverted()
    zc, R = 0.56, 0.30
    fr.cyl(a, R, R, 0.11, 14, loc=(0, 0.055, zc), rot=(90, 0, 0), bevel=0.02,
           col=lambda f: "stone_light" if abs((inv @ f.normal).normalized().y) > 0.7 else "stone_mid")
    fr.cyl(a, 0.04, 0.04, 0.42, 6, loc=(0, 0.21, zc), rot=(90, 0, 0), col="iron_dark")
    lean = math.degrees(math.atan2(0.26, 0.62))
    for y in (-0.13, 0.13):
        for sx in (-1, 1):
            fr.box(a, (0.08, 0.08, 0.70), (sx * 0.13, y, 0.30), rot=(0, -sx * lean, 0), col="wood_mid", bevel=0.02)
    fr.box(a, (0.07, 0.06, 0.22), (0, -0.24, zc - 0.09), col="wood_dark", bevel=0.015)
    fr.cyl(a, 0.03, 0.03, 0.16, 6, loc=(0, -0.24, zc - 0.18), rot=(90, 0, 0), col="wood_light")
    # корыто: круг макает кромку в воду — без него камень не точит
    fr.box(a, (0.46, 0.20, 0.30), (0, 0, 0.15), col="wood_mid", bevel=0.03)
    fr.box(a, (0.38, 0.13, 0.012), (0, 0, 0.303), col="water", bevel=0.0)


def _half_log(a, fr, r=0.11, h=0.30):
    """Половинка полена стоймя в осях fr: плоский раскол смотрит в -Y рамы."""
    inv = fr.m.to_3x3().inverted()
    pts = [(-r, 0.0)] + [(r * math.cos(t), -r * math.sin(t)) for t in
                         [math.radians(180 - 30 * k) for k in range(1, 6)]] + [(r, 0.0)]

    def col(f):
        n = (inv @ f.normal).normalized()
        if abs(n.z) > 0.9:
            return "wood_light"
        return "wood_pale" if n.y < -0.9 else "bark"
    fr.prism(a, pts, h, loc=(0, 0, h / 2), rot=(90, 0, 0), col=col)


def _chop_block(a, x, y, seed=12):
    """Колода для колки: чурбан, на нём расколотое полено — половинки разошлись в стороны."""
    rng = random.Random(seed)
    a.add(p_cyl(0.26, 0.23, 0.46, 9, loc=(x, y, -0.03), bevel=0.025),
          lambda f: "wood_pale" if f.normal.z > 0.5 else "bark")
    for s in (-1, 1):
        # половинки стоят плоской гранью друг к другу и отклонены наружу
        _half_log(a, Frame((x + 0.02, y + s * 0.03, 0.425), rot=(s * 10, 0, 90 + 90 * s)))
    for k, (dx, dy, rz) in enumerate(((0.36, -0.26, 20), (-0.34, -0.30, -35), (0.12, -0.44, 70))):
        _half_log(a, Frame((x + dx, y + dy, 0.11), rot=(90, 0, rz + rng.uniform(-10, 10))), r=0.1, h=0.3)


# ---------------------------------------------------------------------------------------
# герой: пень с топором
# ---------------------------------------------------------------------------------------
def _stump(a, cx, cy, r=0.58, h=0.56, seed=9):
    """Гранёный ствол с полосами коры, светлый срез с годичным кольцом, корни веером в землю."""
    def col(f):
        n = f.normal
        if n.z > 0.9:
            return "wood_pale"
        if n.z > 0.45:
            return "wood_yellow"                   # фаска среза — заболонь светлым ободком
        if n.z < -0.5:
            return "bark_dark"
        k = int(((math.degrees(math.atan2(n.y, n.x)) + 360) % 360) // 30)
        return "bark" if k % 2 else "bark_dark"   # продольные полосы коры
    a.add(p_cyl(r, r * 0.9, h + 0.06, 12, loc=(cx, cy, -0.06), bevel=0.05), col)
    # годичное кольцо: тёмный диск, поверх светлый поменьше; сердцевина точкой
    for rr, c, dz in ((r * 0.60, "wood_light", 0.004), (r * 0.46, "wood_pale", 0.019), (r * 0.09, "wood_light", 0.034)):
        a.add(p_cyl(rr, rr, 0.012, 12, loc=(cx, cy, h + dz)), c)
    rng = random.Random(seed)
    for ang in (60, 118, 178, 238, 300):
        ang += rng.uniform(-8, 8)
        drop = rng.uniform(28, 36)
        rl = 0.36 / math.sin(math.radians(drop))            # кончик корня уходит в землю на 5 см
        t = math.radians(ang)
        a.add(p_cyl(0.19, 0.05, rl, 6, loc=(cx + math.cos(t) * r * 0.55, cy + math.sin(t) * r * 0.55, 0.30),
                    rot=(0, 90 + drop, ang)), lambda f: "bark" if f.normal.z > 0.3 else "bark_dark")


def _stuck_axe(a, target, rz, s=1.65):
    """Топор из common, вогнанный в срез: лезвие вниз, топорище поднимается под 40 градусов.
    Увеличен в 1.65 раза: на игровой дистанции топор обычного размера лежал на пне щепкой."""
    rot = (0, 130, rz)
    q = Frame((0, 0, 0), rot=rot, s=s).at((0.12, 0, 0.95 - 0.13))   # точка лезвия у самого среза
    axe(a, Frame(tuple(Vector(target) - q), rot=rot, s=s), L=0.95, head="steel", handle="wood_light")


# ---------------------------------------------------------------------------------------
# штабель, ель, щепа
# ---------------------------------------------------------------------------------------
def _log(a, fr, x0, y, z, L, r, inv):
    """Бревно вдоль X рамы: верхние грани коры светлее, торцы светлые с кольцом."""
    def col(f):
        n = (inv @ f.normal).normalized()
        if abs(n.x) > 0.9:
            return "wood_pale"
        return "bark" if n.z > 0.2 else "bark_dark"
    fr.cyl(a, r, r, L, 8, loc=(x0, y, z), rot=(0, 90, 0), col=col, bevel=0.02, spin=22.5)
    for s, xx in ((-1, x0 - 0.004), (1, x0 + L + 0.004)):
        fr.cyl(a, r * 0.55, r * 0.55, 0.01, 8, loc=(xx, y, z), rot=(0, 90 * s, 0), col="wood_light", spin=22.5)


def _log_stack(a, fr, L=2.1, r=0.155, rows=(4, 3, 2), seed=4):
    """Штабель целых брёвен вдоль X рамы на двух подкладках, с кольями по бокам."""
    rng = random.Random(seed)
    inv = fr.m.to_3x3().inverted()
    step = 2 * r * 1.03
    zs = 0.10
    for x in (-L * 0.30, L * 0.30):
        fr.box(a, (0.16, rows[0] * step + 0.26, 0.12), (x, 0, 0.04), col="wood_dark", bevel=0.03)
    for row, n in enumerate(rows):
        z = zs + r + row * step * 0.866                 # ряд ложится в ложбины нижнего
        for k in range(n):
            y = (k - (n - 1) / 2) * step
            ll = L + rng.uniform(-0.12, 0.06)
            _log(a, fr, -ll / 2 + rng.uniform(-0.05, 0.05), y, z, ll, r * rng.uniform(0.95, 1.04), inv)
    # колья держат нижний ряд с боков: без них пирамида расползается
    half = (rows[0] - 1) / 2 * step + r + 0.05
    hs = zs + 2 * r + step * 0.866 * 1.2
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.box(a, (0.10, 0.10, hs + 0.06), (sx * L * 0.36, sy * half, hs / 2 - 0.03), col="wood_dark", bevel=0.02)
            fr.cyl(a, 0.075, 0.0, 0.10, 4, loc=(sx * L * 0.36, sy * half, hs), spin=45, col="wood_dark")


def _fir(a, x, y, height=2.4, radius=0.58, tiers=4, seed=5):
    """Молодая ель по рецепту Tree_Pine кита: шестигранные ярусы без дрожания вершин."""
    rng = random.Random(seed)
    trunk_h = height * 0.22
    a.add(p_cyl(0.15 * radius, 0.11 * radius, trunk_h + 0.15, 6, loc=(x, y, -0.12)), "bark")
    z = trunk_h * 0.62
    tier_h = (height - z) / (1 + (tiers - 1) * 0.58) * 1.02
    cols = ["pine_dark", "pine_mid", "pine_mid", "pine_light", "pine_light"]
    for i in range(tiers):
        f = i / max(1, tiers - 1)
        rr = radius * (1.0 - 0.62 * f) * rng.uniform(0.96, 1.04)
        hh = tier_h * (1.0 - 0.14 * f)
        c = cols[min(i, len(cols) - 1)]
        a.add(p_cyl(rr, 0.0, hh, 6, loc=(x, y, z), spin=rng.uniform(0, 60)),
              lambda fc, c=c: "pine_dark" if fc.normal.z < -0.5 else c)
        z += hh * 0.58


def _chips(a, cx, cy, n, r0, r1, a0, a1, seed):
    """Щепа: плоские треугольные клинья, веером вокруг точки (углы a0..a1 в градусах)."""
    rng = random.Random(seed)
    for k in range(n):
        t = math.radians(rng.uniform(a0, a1))
        d = rng.uniform(r0, r1)
        s = rng.uniform(0.8, 1.25)
        pts = [(-0.07 * s, -0.035 * s), (0.08 * s, -0.03 * s), (0.0, 0.05 * s)]
        a.add(p_prism(pts, 0.04, loc=(cx + math.cos(t) * d, cy + math.sin(t) * d, 0.018),
                      rot=(-90 + rng.uniform(-8, 8), 0, rng.uniform(0, 360))),
              rng.choice(("wood_pale", "wood_yellow", "wood_pale")))


def build(a):
    _tent(a)
    _axe_rack(a, Frame((XT + 0.08, YT + 0.12, 0)))
    _grindstone(a, Frame((XT - 0.44, YT - 0.28, 0), rz=18))
    _chop_block(a, XT + 0.44, YT - 0.30)

    # герой: пень r 0.58 слева спереди, топор поднят топорищем к центру участка
    _stump(a, SX_, SY_)
    _stuck_axe(a, (SX_ + 0.18, SY_ + 0.02, 0.56), rz=165)
    _chips(a, SX_, SY_, 7, 0.72, 1.02, 150, 350, seed=21)

    # штабель чуть развёрнут: правые торцы ловят ракурс 3/4, длинный бок — фасад
    _log_stack(a, Frame((0.86, -0.60, 0), rz=-10))
    _fir(a, 1.50, 1.12)
    _chips(a, XT + 0.44, YT - 0.30, 3, 0.34, 0.55, 200, 340, seed=22)


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): шатёр -> изба лесорубов -> двухэтажная артель
# =========================================================================================
def _sawhorse_log(a, fr):
    """Козлы с бревном и лучковой пилой: бревно вдоль X рамы, пила воткнута в пропил."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.box(a, (0.08, 0.08, 0.66), (sx * 0.40, sy * 0.12, 0.30), rot=(sy * 22, 0, 0), col="wood_dark",
                   bevel=0.0)
        fr.box(a, (0.10, 0.10, 0.10), (sx * 0.40, 0, 0.58), col="wood_dark", bevel=0.0)
    fr.cyl(a, 0.15, 0.15, 1.30, 8, loc=(-0.65, 0, 0.75), rot=(0, 90, 0),
           col=lambda f: "wood_pale" if abs(f.normal.x) > 0.7 else "bark")
    fr.box(a, (0.03, 0.30, 0.03), (0.10, 0, 1.08), col="wood_light", bevel=0.0)
    for sy in (-1, 1):
        fr.box(a, (0.03, 0.03, 0.36), (0.10, sy * 0.14, 0.92), col="wood_light", bevel=0.0)
    fr.box(a, (0.02, 0.28, 0.06), (0.10, 0, 0.78), col="iron", bevel=0.0)


def _firewood(a, x0, y0, z0=0.0, cols=3, rows=3, r=0.09, L=0.44):
    """Поленница торцами к камере: поленья вдоль Y от y0 вглубь, ряды вперевязку."""
    a.add(p_box((cols * 2 * r * 1.02 + 0.06, 0.08, 0.06), loc=(x0 + (cols - 1) * r * 1.02, y0 + L * 0.2, z0 + 0.03),
                bevel=0.0), "wood_dark")
    for row in range(rows):
        n = cols - (row % 2)
        for k in range(n):
            x = x0 + (k + 0.5 * (row % 2)) * 2 * r * 1.02
            z = z0 + 0.06 + r + row * 2 * r * 0.86
            a.add(p_cyl(r, r, L, 6, loc=(x, y0, z), rot=(-90, 0, 0), spin=30),
                  lambda f: "wood_pale" if abs(f.normal.y) > 0.7 else "bark")


def _l2(a):
    """Изба лесорубов: сруб на месте шатра (конёк вдоль X, соломенная кровля к камере), дверь и окно,
    печная труба; у избы поленница, перед ней — пень с топором, штабель и козлы с бревном."""
    cx, cy, w, d, r = -0.72, 0.72, 2.30, 1.40, 0.12
    z0 = 0.10
    plinth(a, cx, cy, w + 0.14, d + 0.14, h=z0)
    yf = cy - d / 2
    door_x, win_x = -0.02, -1.22
    top = log_box(a, cx, cy, w, d, z0, 7, r=r,
                  openings={"-y": [(door_x - 0.34, door_x + 0.34, z0, z0 + 1.20),
                                   (win_x - 0.22, win_x + 0.22, 0.74, 1.16)]})
    ze = z0 + r + 6 * 2 * r * 0.88 + r                  # верх венцов вдоль X — на них ложится кровля
    plank_door(a, yf, z0 + 0.02, w=0.60, h=1.10, x=door_x)
    win(a, Frame((win_x, yf - 0.06, 0.95)), w=0.40, h=0.38, shutters="wood_light", sill="wood_dark")
    zr = gable_x_at(a, cx, cy, w, d, ze, pitch_deg=38, ox=0.18, oy=0.18, bands=3)
    for s in (-1, 1):
        gable_end(a, cx + s * w / 2, cy, d, top - 0.04, zr, face=s)
    stovepipe(a, cx - 0.55, cy + 0.34, ze + 0.30, zr + 0.40 - ze - 0.30)
    _firewood(a, 0.58, yf - 0.02, cols=3, rows=3)

    _stump(a, SX_, SY_)
    _stuck_axe(a, (SX_ + 0.18, SY_ + 0.02, 0.56), rz=165)
    _chips(a, SX_, SY_, 7, 0.72, 1.02, 150, 350, seed=21)
    _log_stack(a, Frame((1.12, -0.86, 0), rz=-8), L=1.70)
    _chop_block(a, 0.36, -0.38)
    _fir(a, 1.50, 1.12)


def _l3(a):
    """Артель: двухэтажный сруб фронтоном к камере (конёк вдоль Y), галерея-балкон над дверью, окна со
    светом, каменная труба; брёвна — под навесом торцами к камере; пень с топором и ель — прежние."""
    cx, cy, w, d, r = -0.66, 0.74, 2.40, 1.50, 0.12
    z0 = 0.12
    plinth(a, cx, cy, w + 0.16, d + 0.16, h=z0)
    yf = cy - d / 2
    door_x = 0.02
    wl = (-1.28, 0.80, 1.20)                           # окно низа: x, z0, z1
    up = ((-1.30, 1.86, 2.28), (-0.20, 1.86, 2.28))    # окна верха
    ops = [(door_x - 0.34, door_x + 0.34, z0, z0 + 1.22), (wl[0] - 0.22, wl[0] + 0.22, wl[1], wl[2])]
    ops += [(x - 0.22, x + 0.22, z_0, z_1) for x, z_0, z_1 in up]
    top = log_box(a, cx, cy, w, d, z0, 11, r=r, openings={"-y": ops})
    zx = z0 + r + 10 * 2 * r * 0.88 + r                # верх венцов фасада: от него фронтон
    plank_door(a, yf, z0 + 0.02, w=0.62, h=1.12, x=door_x)
    win(a, Frame((wl[0], yf - 0.06, (wl[1] + wl[2]) / 2)), w=0.40, h=0.36, lit=True, shutters="wood_light",
        sill="wood_dark")
    for x, z_0, z_1 in up:
        win(a, Frame((x, yf - 0.06, (z_0 + z_1) / 2)), w=0.40, h=0.36, lit=True, shutters="wood_light",
            sill=None)
    # межэтажный пояс по фасаду: доска с тёмной кромкой делит сруб на два этажа
    a.add(p_box((w + 0.40, 0.10, 0.12), loc=(cx, yf - 0.14, 1.50), bevel=0.0), BEAM)
    zr = gable_y_at(a, cx, cy, w, d, top, pitch_deg=36, ox=0.16, oy=0.14, bands=3)
    for s in (-1, 1):
        gable_front(a, cx, cy + s * d / 2, w, zx - 0.02, zr, face=s, boards=(s < 0))
    win(a, Frame((cx, yf - 0.08, zx + 0.42)), w=0.34, h=0.30, lit=True, shutters=None, sill=None, cross=True)
    # причелины: доски по кромке фронтона и «полотенце» под коньком
    a.add(p_box((0.14, 0.06, 0.40), loc=(cx, yf - 0.26, zr - 0.30), bevel=0.0), "wood_pale")
    a.add(p_box((0.20, 0.06, 0.20), loc=(cx, yf - 0.26, zr - 0.52), rot=(0, 45, 0), bevel=0.0), "wood_pale")
    # галерея над дверью: настил на консолях, перила со стойками
    gx0, gx1, gy = -1.70, 0.40, yf - 0.36
    a.add(p_box((gx1 - gx0, 0.40, 0.10), loc=((gx0 + gx1) / 2, yf - 0.18, 1.42), bevel=0.0), PLANKS)
    for x in (gx0 + 0.12, (gx0 + gx1) / 2, gx1 - 0.12):
        a.add(p_box((0.10, 0.36, 0.10), loc=(x, yf - 0.16, 1.32), rot=(-28, 0, 0), bevel=0.0), BEAM)
    for k in range(8):
        x = gx0 + 0.05 + k * (gx1 - gx0 - 0.10) / 7
        a.add(p_box((0.06, 0.06, 0.46), loc=(x, gy + 0.04, 1.70), bevel=0.0), "wood_light")
    a.add(p_box((gx1 - gx0, 0.10, 0.08), loc=((gx0 + gx1) / 2, gy + 0.04, 1.95), bevel=0.0), "wood_mid")
    for x in (gx0, gx1):
        a.add(p_box((0.10, 0.40, 0.08), loc=(x, yf - 0.16, 1.95), bevel=0.0), "wood_mid")
    stack(a, 0.18, cy + 0.36, 2.30, zr + 0.30 - 2.30, w=0.46)

    # навес для брёвен: штабель торцами к камере
    sx0, sx1, sy0, sy1 = 0.14, 1.84, -1.48, -0.42
    for x in (sx0, sx1):
        for y, h in ((sy0, 1.40), (sy1, 1.74)):
            porch_post(a, x, y, 0.0, h, s=0.15)
    shed_roof(a, (sx0 + sx1) / 2, (sy0 + sy1) / 2, sx1 - sx0, sy1 - sy0, 1.80, 1.44, face=-1, ox=0.12, oy=0.14)
    log_ends(a, (sx0 + sx1) / 2, sy0 + 0.06, sy1 - sy0 - 0.12, rows=(5, 4, 3), r=0.15)

    _stump(a, SX_, SY_)
    _stuck_axe(a, (SX_ + 0.18, SY_ + 0.02, 0.56), rz=165)
    _chips(a, SX_, SY_, 7, 0.72, 1.02, 150, 350, seed=21)
    _fir(a, 1.50, 1.12)
    _firewood(a, 0.70, yf + 0.10, cols=2, rows=3)


def evolve(a, level):
    """2: изба лесорубов. 3: двухэтажная артель с навесом для брёвен."""
    (_l2 if level == 2 else _l3)(a)
