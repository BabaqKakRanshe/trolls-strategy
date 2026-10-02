"""
Лесозаготовка: синий шатёр на четырёх столбах, массивный пень с вогнанным топором и
штабель длинных брёвен. Силуэт держат горизонталь брёвен и пирамида шатра с кремовыми
фестонами — в отличие от кожевни, где работают вертикальные рамы и односкатный навес.
"""
import math, random
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, p_prism
from vitaria_buildings.common import Frame, axe, brace, STONE_TOP
from vitaria_buildings.levels import pennant, wall_banner, wall_lantern, finial

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
# Уровни 2 и 3 (vitaria_buildings/levels.py)
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


def _tripod(a, x, y, h=2.55):
    """Тренога-подъёмник над штабелем: три ноги, цепь с крюком и бревно на стропе."""
    top = (x, y, h)
    for ang in (90, 210, 330):
        bx, by = x + 0.62 * math.cos(math.radians(ang)), y + 0.62 * math.sin(math.radians(ang))
        dx, dy, dz = top[0] - bx, top[1] - by, h
        L = math.sqrt(dx * dx + dy * dy + dz * dz)
        tilt = math.degrees(math.acos(dz / L))
        rz = math.degrees(math.atan2(dy, dx))
        a.add(p_cyl(0.06, 0.05, L, 6, loc=(bx, by, 0.0), rot=(0, tilt, rz)), "wood_mid")
    a.add(p_box((0.03, 0.03, 0.80), loc=(x, y, h - 0.42), bevel=0.0), "iron_dark")
    a.add(p_box((0.10, 0.04, 0.08), loc=(x, y, h - 0.84), bevel=0.0), "iron_dark")
    a.add(p_cyl(0.13, 0.13, 1.10, 8, loc=(x - 0.55, y, h - 1.02), rot=(0, 90, 0)),
          lambda f: "wood_pale" if abs(f.normal.x) > 0.7 else "bark")


def upgrade(a, level):
    """2: флажок над правым передним столбом шатра, козлы с бревном и пилой перед штабелем, ещё
    поленница у ели. 3: + тренога с подвешенным бревном над штабелем, знамя под передней обвязкой
    шатра, золотое навершие шатра, фонари на передних столбах, топор в пне с золотым обухом."""
    pennant(a, XT + PX, YT - PY, ZE - 0.10, level, h=1.40, side=1)
    _sawhorse_log(a, Frame((0.42, -1.42, 0.0), rz=4))
    for k in range(2):
        a.add(p_cyl(0.11, 0.11, 0.62, 7, loc=(1.62 - k * 0.24, 0.40, 0.11), rot=(-90, 0, 0)),
              lambda f: "wood_pale" if abs(f.normal.y) > 0.7 else "bark")
    a.add(p_cyl(0.11, 0.11, 0.62, 7, loc=(1.50, 0.40, 0.31), rot=(-90, 0, 0)),
          lambda f: "wood_pale" if abs(f.normal.y) > 0.7 else "bark")
    if level < 3:
        return
    _tripod(a, 0.92, -0.55, h=2.60)
    wall_banner(a, XT, YT - PY - 0.12, ZE - 0.22, w=0.40, h=0.66)
    zt = ZE + 0.02 + 0.72
    finial(a, XT, YT, zt + 0.28, h=0.30)
    for sx in (-1, 1):
        wall_lantern(a, XT + sx * PX, YT - PY - 0.10, 1.70, out=(sx * 0.3, -0.95))
