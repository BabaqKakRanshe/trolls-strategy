"""
Пилорама: вытянутый открытый навес, в середине — рамная пила с тремя полотнами.
Слева в раму по рольгангу входит бревно, справа выходят доски и ложатся в штабель.

Рама и маховик вынесены к фасаду: камера смотрит под 40°, и под скатом с коньком вдоль X
видно только то, что ниже линии свеса. Рама в глубине навеса пряталась бы под кровлю
верхней половиной, а маховик в глубине не виден вовсе. Над пилой — щипец: он отмечает
«героя» в силуэте и не даёт длинной кровле читаться плоской крышкой.
"""
import math
import random

import bmesh
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal, TM
from vitaria_buildings.common import (Frame, gable_roof_x, gable_wall_x, stone_base, post, brace,
                                      cornice, plank_stack, STONE_TOP)

NAME = "Bld_LumberMill"
TITLE = "Пилорама"
TARGET = (5.4, 3.0, 3.5)

PX, PY = 1.75, 1.20      # оси угловых стоек навеса
F = 0.30                 # верх каменного пола
ZT = 2.55                # верх обвязки
PITCH, OY = 24.0, 0.25   # уклон и вынос основного ската
YS = -0.70               # линия подачи: бревно, рама и доски
ZR = 0.66                # верх роликов рольганга
LR = 0.17                # радиус бревна
GX, GY = 0.75, 0.36      # оси направляющих стоек рамы от центра и от линии подачи
WX, WY, WZ = 1.13, -1.23, 0.62   # центр маховика: перед правым рольгангом, низ в приямке

BOARD = lambda f: ("wood_pale" if f.normal.z > 0.7 else
                   "wood_yellow" if abs(f.normal.x) > 0.7 else "wood_light")


def _bark(f):
    """Бревно: торцы светлые, верх коры светлее боков — иначе тёмный ствол тонет в тени навеса."""
    n = f.normal
    if abs(n.x) > 0.7:
        return "wood_pale"
    return "wood_mid" if n.z > 0.55 else ("bark_dark" if n.z < -0.55 else "bark")


def _ring(a, r_out, r_in, h, seg, mat, col):
    """Шайба с отверстием по оси Z рамы mat (основание в z=0): обод без булевых."""
    b = bmesh.new()
    ob, ot, ib, it = [], [], [], []
    for i in range(seg):
        t = math.tau * i / seg
        c, s = math.cos(t), math.sin(t)
        ob.append(b.verts.new((r_out * c, r_out * s, 0.0)))
        ot.append(b.verts.new((r_out * c, r_out * s, h)))
        ib.append(b.verts.new((r_in * c, r_in * s, 0.0)))
        it.append(b.verts.new((r_in * c, r_in * s, h)))
    for i in range(seg):
        j = (i + 1) % seg
        b.faces.new((ob[i], ob[j], ot[j], ot[i]))
        b.faces.new((ib[j], ib[i], it[i], it[j]))
        b.faces.new((ot[i], ot[j], it[j], it[i]))
        b.faces.new((ob[j], ob[i], ib[i], ib[j]))
    bmesh.ops.transform(b, matrix=mat, verts=b.verts)
    b.normal_update()
    a.add(b, col)


def _xz_bar(a, p0, p1, y, t=(0.085, 0.06), col="wood_mid", bevel=0.02):
    """Брус в плоскости XZ от точки p0 до p1 (обе (x, z)) на глубине y."""
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]
    ang = math.degrees(math.atan2(dz, dx))
    a.add(p_box((math.hypot(dx, dz), t[1], t[0]), loc=((p0[0] + p1[0]) / 2, y, (p0[1] + p1[1]) / 2),
                rot=(0, -ang, 0), bevel=bevel), col)


def _front_gable(a, zr):
    """
    Щипец над пилой поверх переднего ската. Основной скат не разрезан: щипец закрытый,
    его скаты за ендовой уходят под основную кровлю и снаружи не видны ни с одного ракурса.
    Фронтон стоит на 3 см впереди торца полосы ската, иначе синяя кромка прорезает его низ.
    """
    tp = math.tan(math.radians(PITCH))
    ye = PY + OY                          # свес основного ската по Y
    xe = 1.15                             # полуширина щипца на линии свеса
    q = math.atan(ye * tp / xe)           # уклон, при котором свес щипца = свесу ската
    cq, sq, qd = math.cos(q), math.sin(q), math.degrees(q)
    yf = -ye - 0.10                       # передний край скатов щипца
    Lq = xe / cq
    rl = Lq / 2
    for sx in (-1, 1):
        dq = (sx * cq, -sq)               # вниз по скату в плоскости XZ
        nq = (sx * sq, cq)
        for k in range(2):
            s = (k + 0.5) * rl
            a.add(p_box((rl + 0.1, -yf, 0.17), loc=(dq[0] * s + nq[0] * 0.06, yf / 2, zr + dq[1] * s + nq[1] * 0.06),
                        rot=(0, sx * qd, 0), bevel=0.05), "roof" if k == 0 else "roof_dark")
        a.add(p_box((Lq + 0.08, 0.12, 0.24),
                    loc=(dq[0] * Lq / 2 + nq[0] * 0.05, yf - 0.045, zr + dq[1] * Lq / 2 + nq[1] * 0.05),
                    rot=(0, sx * qd, 0), bevel=0.035), "wood_dark")
    # конёк щипца выведен на 5 см за причелины: иначе в их стыке на вершине видна чёрная щель
    a.add(p_box((0.38, -yf + 0.16, 0.38), loc=(0, (yf - 0.16) / 2, zr + 0.09), rot=(0, 45, 0), bevel=0.055),
          "roof_dark")
    # фронтон: пятиугольник — низ опущен до кромки свеса, чтобы линия карниза не прерывалась
    zb, ze = 2.44, zr - xe * math.tan(q) - 0.01
    yw = -ye - 0.01               # передняя грань на -1.53: впереди торца полосы ската (-1.50)
    a.add(p_prism([(-xe, zb), (xe, zb), (xe, ze), (0, zr - 0.01), (-xe, ze)], 0.14, loc=(0, yw, 0)),
          "wood_light")
    a.add(p_box((2 * xe + 0.06, 0.08, 0.14), loc=(0, yw - 0.08, zb + 0.06), bevel=0.03), "wood_dark")
    # слуховое окно как у дома кита
    zv = zb + 0.36
    a.add(p_cyl(0.20, 0.20, 0.10, 8, loc=(0, yw - 0.06, zv), rot=(90, 0, 0), spin=22.5), "wood_dark")
    a.add(p_cyl(0.13, 0.13, 0.06, 8, loc=(0, yw - 0.12, zv), rot=(90, 0, 0), spin=22.5), "black")


def _table(a, x0, x1):
    """Рольганг: два продольных бруса на козлах и железные ролики поперёк."""
    L, xc = x1 - x0, (x0 + x1) / 2
    for sy in (-1, 1):
        a.add(p_box((L, 0.09, 0.12), loc=(xc, YS + sy * 0.27, ZR - 0.10), bevel=0.025), "wood_mid")
    for x in (x0 + 0.10, x1 - 0.10):
        a.add(p_box((0.14, 0.68, ZR - 0.16 - F + 0.01), loc=(x, YS, (ZR - 0.16 + F) / 2), bevel=0.035),
              "wood_dark")
    for i in range(3):
        x = x0 + 0.17 + i * (L - 0.34) / 2
        a.add(p_cyl(0.055, 0.055, 0.60, 6, loc=(x, YS - 0.30, ZR - 0.055), rot=(-90, 0, 0)), "iron_light")


def _saw(a):
    # --- станина: четыре направляющие стойки на лежнях, сверху венец ---------------
    zc = 2.05                                  # середина венца, верх 2.15 — предел видимости под свесом
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.18, 0.20, zc + 0.06 - 0.40), loc=(sx * GX, YS + sy * GY, (zc + 0.06 + 0.40) / 2),
                        bevel=0.04), "wood_dark")
    # лежень только передний: задний закрыт бревном, досками и нижним брусом рамки
    a.add(p_box((1.86, 0.22, 0.14), loc=(0, YS - GY, F + 0.06), bevel=0.04), "wood_dark")
    for sy in (-1, 1):
        a.add(p_box((1.94, 0.24, 0.20), loc=(0, YS + sy * GY, zc), bevel=0.05),
              by_normal("wood_mid", "wood_dark", "wood_dark", 0.7))
    for sx in (-1, 1):
        a.add(p_box((0.20, 0.94, 0.16), loc=(sx * GX, YS, zc), bevel=0.04), "wood_dark")
        # железные накладки на узлах венца — рама читается тяжёлой, «машинной»
        a.add(p_box((0.26, 0.03, 0.26), loc=(sx * GX, YS - GY - 0.13, zc - 0.04), bevel=0.0), "iron_dark")

    # --- подвижная пильная рамка светлее станины: иначе две рамы сливаются в одну --
    # Верхний брус тоньше нижнего и поднят под венец: каждый сантиметр над бревном — это полотно.
    sash = by_normal("wood_pale", "wood_light", "wood_mid", 0.7)
    zh0, zh1 = 1.72, 1.88
    zb0, zb1 = 0.46, 0.58
    a.add(p_box((1.26, 0.86, zh1 - zh0), loc=(0, YS, (zh0 + zh1) / 2), bevel=0.04), sash)
    a.add(p_box((1.26, 0.86, zb1 - zb0), loc=(0, YS, (zb0 + zb1) / 2), bevel=0.04), sash)
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.12, 0.16, zh1 - zb0 - 0.08), loc=(sx * 0.56, YS + sy * GY, (zh1 + zb0) / 2),
                        bevel=0.035), "wood_light")
        # ползуны: железо между рамкой и стойкой — видно, что рамка ходит в направляющих
        for z in (1.0, 1.55):
            a.add(p_box((0.07, 0.14, 0.12), loc=(sx * 0.635, YS - GY, z), bevel=0.0), "iron_dark")

    # --- три полотна: разнесены по X, иначе с фасада сливаются в одно ----------------
    # Каждое сдвинуто по Y на свою линию пропила между досками. Зубья только над бревном:
    # ниже их закрывают бревно и доски, а тёмное железо на светлой стали читает пилу, не решётку.
    zm, hb = (zb1 + zh0) / 2, zh0 - zb1 + 0.02
    for bx, by in ((-0.30, -0.09), (0.0, 0.0), (0.30, 0.09)):
        a.add(p_box((0.14, 0.035, hb), loc=(bx, YS + by, zm), bevel=0.0), "steel")
        for k in range(3):
            a.add(p_prism([(0.0, 0.0), (0.0, 0.17), (-0.06, 0.0)], 0.03,
                          loc=(bx - 0.065, YS + by, 1.04 + k * 0.21)), "iron")
        for z in (zb1 + 0.04, zh0 - 0.04):
            a.add(p_box((0.19, 0.08, 0.10), loc=(bx, YS + by, z), bevel=0.0), "iron_dark")


def _drive(a):
    """Маховик с кривошипом и шатун к правой стойке пильной рамки."""
    m = TM((WX, WY + 0.05, WZ), (90, 0, 0))          # ось маховика смотрит в -Y

    def rim_col(f):
        n = f.normal
        if abs(n.y) > 0.7:
            return "wood_mid"
        c = f.calc_center_median()
        out = (c.x - WX) * n.x + (c.z - WZ) * n.z
        return "iron_dark" if out > 0 else "wood_dark"     # снаружи — железная шина
    _ring(a, 0.50, 0.38, 0.10, 16, m, rim_col)
    for k in range(6):
        ang = math.radians(15 + k * 60)
        _xz_bar(a, (WX + 0.08 * math.cos(ang), WZ + 0.08 * math.sin(ang)),
                (WX + 0.40 * math.cos(ang), WZ + 0.40 * math.sin(ang)), WY, t=(0.08, 0.06),
                col="wood_dark", bevel=0.0)
    a.add(p_cyl(0.10, 0.10, 0.16, 10, loc=(WX, WY + 0.08, WZ), rot=(90, 0, 0)), "iron_dark")
    a.add(p_cyl(0.06, 0.06, 0.03, 8, loc=(WX, WY - 0.075, WZ), rot=(90, 0, 0)), "copper")

    # кривошип и шатун: палец на 0.22 от оси, шатун уходит вверх-влево к рамке
    th = math.radians(125)
    pin = (WX + 0.22 * math.cos(th), WZ + 0.22 * math.sin(th))
    lug = (0.56, 1.30)
    _xz_bar(a, (WX, WZ), pin, WY - 0.075, t=(0.09, 0.05), col="iron_dark", bevel=0.015)
    a.add(p_cyl(0.04, 0.04, 0.11, 8, loc=(pin[0], WY - 0.07, pin[1]), rot=(90, 0, 0)), "iron_light")
    _xz_bar(a, pin, lug, WY - 0.13, t=(0.10, 0.06), col="wood_mid", bevel=0.02)
    for p in (pin, lug):
        a.add(p_box((0.14, 0.075, 0.14), loc=(p[0], WY - 0.13, p[1]), bevel=0.0), "iron_dark")
    # проушина на пильной рамке: вынесена вперёд, мимо направляющей стойки
    a.add(p_box((0.10, 0.26, 0.12), loc=(0.56, YS - GY - 0.20, lug[1]), bevel=0.02), "wood_dark")
    a.add(p_cyl(0.035, 0.035, 0.10, 8, loc=(lug[0], WY - 0.08, lug[1]), rot=(90, 0, 0)), "iron_light")

    # вал уходит назад в подшипник на стулке; щель приямка в полу
    a.add(p_cyl(0.045, 0.045, 0.18, 6, loc=(WX, WY + 0.03, WZ), rot=(-90, 0, 0)), "iron")
    a.add(p_box((0.12, 0.12, WZ - 0.06 - F), loc=(WX, WY + 0.12, (WZ - 0.06 + F) / 2), bevel=0.03), "wood_dark")
    a.add(p_box((0.18, 0.14, 0.12), loc=(WX, WY + 0.12, WZ), bevel=0.0), "iron_dark")
    a.add(p_box((1.06, 0.16, 0.02), loc=(WX, WY, F), bevel=0.0), "black")


def _log_pile(a):
    """Брёвна на лагах за левым торцом, торцами к камере — так куча читается издалека."""
    rng = random.Random(7)
    r, L, cx, cy = 0.14, 1.45, -2.30, -0.48
    for y in (cy - 0.50, cy + 0.50):
        a.add(p_box((1.02, 0.13, 0.10), loc=(cx, y, 0.05), bevel=0.03), "wood_dark")
    step = 2 * r * 1.03
    col = lambda f: "wood_pale" if abs(f.normal.y) > 0.7 else "bark"
    for row, n in enumerate((3, 2, 1)):
        z = 0.10 + r - 0.005 + row * step * 0.866
        for k in range(n):
            x = cx + (k - (n - 1) / 2) * step
            Lk = L + rng.uniform(-0.12, 0.05)
            a.add(p_cyl(r, r, Lk, 8, loc=(x, cy - Lk / 2 + rng.uniform(-0.05, 0.05), z),
                        rot=(-90, 0, 0), bevel=0.02), col)
    # колья только с внешней стороны: со стороны навеса нижнее бревно упирается в лаги
    for y in (cy - 0.50, cy + 0.50):
        a.add(p_box((0.10, 0.10, 0.62), loc=(cx - 0.47, y, 0.27), bevel=0.025), "wood_dark")


def build(a):
    # --- основание: тёмный цоколь и светлая каменная плита пола ----------------------
    stone_base(a, 3.9, 2.9, h=0.26)
    a.add(p_box((3.7, 2.7, 0.16), loc=(0, 0, F - 0.08), bevel=0.04), STONE_TOP)

    # --- каркас навеса. На фасаде стоек в пролёте нет: они легли бы ровно на раму -----
    for sx in (-1, 1):
        for sy in (-1, 1):
            post(a, sx * PX, sy * PY, F, ZT - F - 0.04, s=0.20)
        brace(a, sx * 1.52, -PY, 2.12, length=0.62, angle=-sx * 45)
    cornice(a, 2 * PX, 2 * PY, ZT - 0.09, t=0.18)

    # задняя стена — горизонтальные доски снаружи стоек, на 1 см от их грани;
    # нижняя опущена к цоколю (0.27), иначе за краем плиты пола видна щель
    for i in range(4):
        a.add(p_box((2 * PX + 0.2, 0.10, 0.525), loc=(0, PY + 0.16, 0.53 + i * 0.52), bevel=0.03),
              "wood_light" if i % 2 else "wood_mid")

    zr = gable_roof_x(a, 2 * PX, 2 * PY, ZT, pitch_deg=PITCH, ox=0.30, oy=OY)
    gable_wall_x(a, 2 * PX, 2 * PY, ZT - 0.02, zr, col="wood_light", inset=0.10, depth=0.22)
    _front_gable(a, zr)

    # --- рольганги по обе стороны рамы ------------------------------------------------
    _table(a, -1.82, -0.88)
    _table(a, 0.88, 1.90)

    _saw(a)
    _drive(a)

    # --- бревно: входит в раму до первого полотна -------------------------------------
    x0, x1 = -1.85, -0.30
    zl = ZR + LR
    a.add(p_cyl(LR, LR, x1 - x0, 10, loc=(x0, YS, zl), rot=(0, 90, 0), bevel=0.02), _bark)
    a.add(p_cyl(LR * 0.55, LR * 0.55, 0.01, 8, loc=(x0 - 0.008, YS, zl), rot=(0, 90, 0)), "wood_light")
    for k in range(4):
        fr = Frame((-0.34, YS - 0.135 + k * 0.09, ZR + 0.005), rz=(k - 1.5) * 1.6)
        fr.box(a, (1.64, 0.075, 0.32), (0.82, 0, 0.16), col=BOARD, bevel=0.015)

    # --- опилки из-под рамы -------------------------------------------------------
    rng = random.Random(3)
    for x, y, r, h in ((-0.32, -1.18, 0.30, 0.20), (0.06, -1.24, 0.20, 0.13)):
        a.add(p_ico(r, 1, loc=(x, y, F - 0.01), scl=(1.35, 0.75, h / r), jitter=0.12, rng=rng, cut=0.0),
              by_normal("wood_pale", "wood_yellow", "wood_yellow", 0.4))

    # --- снаружи торцов: брёвна слева, штабель досок справа ---------------------------
    _log_pile(a)
    plank_stack(a, Frame((2.33, -0.55, 0.0), rz=90), L=1.3, w=0.24, t=0.07, cols=2, layers=5)
