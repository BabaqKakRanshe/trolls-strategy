"""
Ориентиры фона арены: мельница с синей конической кровлей (крылья — отдельный меш
для вращения в Unity) и каменный двухарочный мост через протоку.

Пивоты: мельница — низ по центру; крылья — центр ступицы (ось вращения = локальная -Y,
лицом к -Y); мост — середина настила на уровне его верха (z = 0), опоры уходят на DEPTH вниз.
Мост рассчитан на реку в 1.35–1.6 м под настилом: пята арок (SPRING) чуть выше воды.
"""
import math
import random
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from vitaria_buildings.common import Frame, plank_door, window

STONE = by_normal("stone_light", "stone_mid", "stone_dark", 0.55)
STONE_DK = by_normal("stone_mid", "stone_dark", "stone_dark", 0.55)
ROOF = by_normal("roof", "roof_dark", "roof_dark", 0.35)
PLANK = by_normal("wood_light", "wood_mid", "wood_dark", 0.7)

# ступица мельницы в локальных координатах мельницы (для постановки крыльев)
MILL_HUB = (0.0, -1.66, 4.42)
MILL_R0, MILL_R1, MILL_H = 1.5, 1.2, 3.55          # радиус камня внизу/вверху, высота камня


def _oct_face_dist(r):
    return r * math.cos(math.radians(22.5))


def windmill(a):
    rng = random.Random(21)
    # цоколь и каменная башня (грань восьмигранника смотрит в -Y: spin 22.5)
    a.add(p_cyl(1.74, 1.68, 0.36, 8, loc=(0, 0, -0.16), spin=22.5, bevel=0.05), STONE_DK)
    a.add(p_cyl(MILL_R0, MILL_R1, MILL_H, 8, loc=(0, 0, 0.2), spin=22.5, bevel=0.04), STONE)
    # выступающие камни кладки: KayKit даёт фактуру крупными блоками, а не текстурой
    for k in range(22):
        ang = math.radians(rng.choice([0, 45, 90, 135, 180, 225, 270, 315]) + 90 + rng.uniform(-14, 14))
        z = rng.uniform(0.5, MILL_H - 0.2)
        r = MILL_R0 + (MILL_R1 - MILL_R0) * (z - 0.2) / MILL_H
        d = _oct_face_dist(r) + 0.03
        a.add(p_box((rng.uniform(0.34, 0.6), 0.1, rng.uniform(0.2, 0.3)),
                    loc=(math.cos(ang) * d, math.sin(ang) * d, 0.2 + z), rot=(0, 0, math.degrees(ang) + 90),
                    bevel=0.03), rng.choice(["stone_light", "stone_mid", "stone_dark"]))
    # дверь и окна
    yd = -_oct_face_dist(MILL_R0) - 0.02
    plank_door(a, yd, 0.2, w=0.66, h=1.2)
    fr = Frame((0, 0, 0))
    for ang, z in ((135, 2.3), (45, 2.9), (-90, 2.75)):
        r = MILL_R0 + (MILL_R1 - MILL_R0) * (z - 0.2) / MILL_H
        d = _oct_face_dist(r) + 0.02
        t = math.radians(ang)
        window(a, (math.cos(t) * d, math.sin(t) * d, z), rot=(0, 0, ang + 90), w=0.36, h=0.46)
    # галерея: настил, стойки, обвязка
    zg = 0.2 + MILL_H
    a.add(p_cyl(1.62, 1.62, 0.14, 8, loc=(0, 0, zg - 0.02), spin=22.5, bevel=0.03), PLANK)
    for k in range(8):
        t = math.radians(22.5 + 45 * k)
        a.add(p_box((0.08, 0.08, 0.5), loc=(math.cos(t) * 1.52, math.sin(t) * 1.52, zg + 0.36), bevel=0.02),
              "wood_dark")
    for k in range(8):
        t0, t1 = math.radians(22.5 + 45 * k), math.radians(22.5 + 45 * (k + 1))
        p0 = Vector((math.cos(t0), math.sin(t0), 0)) * 1.52
        p1 = Vector((math.cos(t1), math.sin(t1), 0)) * 1.52
        c = (p0 + p1) / 2
        a.add(p_box(((p1 - p0).length + 0.06, 0.06, 0.07), loc=(c.x, c.y, zg + 0.58),
                    rot=(0, 0, math.degrees(math.atan2(p1.y - p0.y, p1.x - p0.x))), bevel=0.015), "wood_mid")
    # деревянный барабан под кровлей
    zb = zg + 0.12
    a.add(p_cyl(1.24, 1.14, 1.14, 8, loc=(0, 0, zb), spin=22.5), "wood_mid")
    for k in range(8):
        t = math.radians(45 * k + 90)
        d = _oct_face_dist(1.19) + 0.02
        a.add(p_box((0.09, 0.06, 1.1), loc=(math.cos(t) * d * 0.99, math.sin(t) * d * 0.99, zb + 0.56),
                    rot=(0, 0, math.degrees(t) + 90), bevel=0.015), "wood_dark")
    # кровля: свес, конус, навершие
    zr = zb + 1.1
    a.add(p_cyl(1.58, 1.52, 0.14, 8, loc=(0, 0, zr - 0.06), spin=22.5, bevel=0.03), "roof_dark")
    a.add(p_cyl(1.5, 0.0, 2.25, 8, loc=(0, 0, zr), spin=22.5), ROOF)
    a.add(p_cyl(0.07, 0.07, 0.32, 6, loc=(0, 0, zr + 2.1)), "wood_dark")
    a.add(p_ico(0.11, 1, loc=(0, 0, zr + 2.45)), "gold")
    # вал и ступица спереди (крылья ставятся отдельным мешем в MILL_HUB)
    hx, hy, hz = MILL_HUB
    a.add(p_box((0.46, 0.5, 0.46), loc=(0, -1.18, hz), bevel=0.05), "wood_dark")
    a.add(p_cyl(0.13, 0.13, 0.42, 8, loc=(0, -1.4, hz), rot=(90, 0, 0)), "wood_mid")


def sails(a):
    """Четыре крыла «иксом», решётка с полотном на одной стороне, ступица в начале координат."""
    a.add(p_cyl(0.25, 0.25, 0.2, 8, loc=(0, 0.1, 0), rot=(90, 0, 0), bevel=0.03), "wood_dark")
    a.add(p_cyl(0.12, 0.08, 0.14, 8, loc=(0, -0.1, 0), rot=(90, 0, 0)), "iron_dark")
    for k in range(4):
        fr = Frame(rot=(0, 45 + 90 * k, 0))
        fr.box(a, (0.12, 0.11, 2.62), (0, 0.0, 1.45), col="wood_dark", bevel=0.02)
        fr.box(a, (0.05, 0.06, 2.02), (0.64, 0.0, 1.76), col="wood_mid", bevel=0.012)
        fr.box(a, (0.05, 0.06, 2.02), (-0.14, 0.0, 1.76), col="wood_mid", bevel=0.012)
        n = 6
        for i in range(n):
            z = 0.8 + i * (1.94 / (n - 1))
            fr.box(a, (0.84, 0.05, 0.045), (0.25, 0.0, z), col="wood_light", bevel=0.01)
        fr.box(a, (0.62, 0.03, 1.72), (0.33, 0.045, 1.8), col="canvas", bevel=0.008)


# ---------------------------------------------------------------------------------------
BR_L = 6.8          # длина вдоль X
BR_W = 2.5          # ширина вдоль Y
PIER = 0.7          # центральный бык
ARCH = 2.0          # пролёт каждой арки
SPRING = -1.5       # пята арок от верха настила: над рекой арки видны почти целиком
DEPTH = 3.4         # опоры уходят на столько вниз от верха настила (ниже дна реки)


def _arch_poly(xl, xr, z_top, z_spring, seg=10):
    r = (xr - xl) / 2
    xc = (xl + xr) / 2
    pts = [(xl, z_top), (xl, z_spring)]
    for i in range(1, seg):
        t = math.pi * (1 - i / seg)
        pts.append((xc + r * math.cos(t), z_spring + r * math.sin(t)))
    pts += [(xr, z_spring), (xr, z_top)]
    return pts


def bridge(a):
    rng = random.Random(31)
    zt = -0.34                                  # низ настила
    zs = SPRING                                 # пята арок; замок арки на 0.16 м ниже настила
    half = BR_L / 2
    x_in = PIER / 2
    x_out = x_in + ARCH
    # настил и мостовая
    a.add(p_box((BR_L, BR_W, 0.34), loc=(0, 0, -0.17), bevel=0.04), STONE)
    for i in range(9):                                            # плиты мостовой
        x = -half + 0.36 + i * (BR_L - 0.72) / 8
        a.add(p_box((0.62, BR_W - 0.7, 0.04), loc=(x, 0, 0.005), bevel=0.012),
              "stone_light" if i % 2 else "stone_warm")
    # тело моста: пазухи над арками, бык, устои (призмы в плоскости XZ на всю ширину)
    for sx in (-1, 1):
        xl, xr = sorted((sx * x_in, sx * x_out))
        a.add(p_prism(_arch_poly(xl, xr, zt, zs), BR_W, loc=(0, 0, 0)), STONE)
        # устой: от края арки до конца моста и вниз до воды
        xa0, xa1 = sorted((sx * x_out, sx * (half + 0.4)))
        a.add(p_box((xa1 - xa0, BR_W + 0.1, DEPTH + zt), loc=((xa0 + xa1) / 2, 0, zt - (DEPTH + zt) / 2), bevel=0.05),
              STONE_DK)
    a.add(p_box((PIER, BR_W, DEPTH + zt), loc=(0, 0, zt - (DEPTH + zt) / 2), bevel=0.04), STONE)
    for sy in (-1, 1):                                           # волнорезы быка
        pts = [(-PIER / 2, 0.0), (PIER / 2, 0.0), (0.0, 0.62)]
        # поворот вокруг X на -90*sy: вершина треугольника смотрит в sy*Y, выдавливание встаёт по Z
        ln = DEPTH + zs + 0.05                                   # от низа опоры до пяты арок
        a.add(p_prism(pts, ln, loc=(0, sy * BR_W / 2, -DEPTH + ln / 2), rot=(-90 * sy, 0, 0)), STONE_DK)
        a.add(p_cyl(PIER * 0.58, 0.0, 0.5, 4, loc=(0, sy * (BR_W / 2 + 0.22), zs), spin=45), "stone_mid")
    # клинчатые камни арок (выступают на 4 см с обеих сторон) и замковый камень
    for sx in (-1, 1):
        xl, xr = sorted((sx * x_in, sx * x_out))
        xc, r = (xl + xr) / 2, (xr - xl) / 2
        n = 9
        for i in range(n):
            t = math.pi * (i + 0.5) / n
            rr = r + 0.16
            key = i == n // 2
            a.add(p_box((0.3 if key else 0.26, BR_W + 0.08, 0.32 if key else 0.26),
                        loc=(xc + rr * math.cos(t), 0, zs + rr * math.sin(t)),
                        rot=(0, 90 - math.degrees(t), 0), bevel=0.025),
                  "stone_light" if (i % 2 == 0 or key) else "stone_mid")
    # карниз под парапетом и парапеты из блоков
    for sy in (-1, 1):
        a.add(p_box((BR_L + 0.1, 0.12, 0.12), loc=(0, sy * (BR_W / 2 + 0.02), zt - 0.02), bevel=0.02), "stone_light")
        x = -half
        while x < half - 0.05:
            ln = min(rng.uniform(0.5, 0.82), half - x)
            a.add(p_box((ln - 0.03, 0.3, 0.46), loc=(x + ln / 2, sy * (BR_W / 2 - 0.15), 0.23), bevel=0.035),
                  rng.choice(["stone_mid", "stone_mid", "stone_warm"]))
            x += ln
        a.add(p_box((BR_L + 0.04, 0.36, 0.1), loc=(0, sy * (BR_W / 2 - 0.15), 0.5), bevel=0.03), "stone_light")
        for sx in (-1, 1):                                       # тумбы на концах
            a.add(p_box((0.44, 0.44, 0.72), loc=(sx * (half - 0.22), sy * (BR_W / 2 - 0.15), 0.36), bevel=0.04),
                  STONE)
            a.add(p_box((0.52, 0.52, 0.1), loc=(sx * (half - 0.22), sy * (BR_W / 2 - 0.15), 0.77), bevel=0.03),
                  "stone_light")


ASSETS = [
    ("Arena", "Bld_Windmill", "Мельница", windmill),
    ("Arena", "Bld_Windmill_Sails", "Крылья мельницы", sails),
    ("Arena", "Env_Bridge", "Каменный мост", bridge),
]
