"""
Ассеты арены «Горная застава» (Arena_MountainPass): каменная стена с зубцами, ворота с открытыми створками,
угловой бастион, сторожевая башня, руина стены, кузня под навесом, настенный светильник, факел, скалы, валуны и
осыпь, горные сосны, можжевельник, эдельвейсы и горечавка, штабель ящиков, дальние снежные вершины;
плитки поля альпийского луга.

Соглашения кита: пивот внизу по центру, лицо к -Y, одна грань — один swatch, фаски на блоках, детали не
тоньше 6 см. Природа, постройки и мелочь (префикс Mount_) сливаются в Arena_MountainPass_Scatter и красятся
палитрой биома Vitaria_Palette_MountainPass (build_vitaria.PALETTE_VARIANTS["MountainPass"]): кладка стен и башен —
stone_*, скалы — rock, rock_dark, stone_light (макушки), кровля башни — slate_*, дерево — wood_*, хвоя — pine_*,
снег вершин — foam. Угли горна и светильников — на светящихся swatch-ах (при выгрузке — слот Vitaria_FX), огонь
над ними — FX_Flame_Small «Луга».
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from . import tiles as TL
from . import swamp_assets as SA
from . import forest_assets as FA

STONE = by_normal("stone_light", "stone_mid", "stone_dark", 0.6)          # кладка: верх светлее, низ в тени
STONE_W = by_normal("stone_light", "stone_warm", "stone_dark", 0.6)       # тёплые блоки вперемешку
ROCK = by_normal("stone_light", "rock", "rock_dark", 0.55)                # скалы: макушки светлые
WOOD = by_normal("wood_light", "wood_mid", "wood_dark", 0.6)
SLATE = by_normal("slate_light", "slate", "slate_dark", 0.5)


# ---------------------------------------------------------------------------------------
# стены и ворота
# ---------------------------------------------------------------------------------------
WALL_SEG = 2.0                     # сегмент стены вдоль X
WALL_H, WALL_D = 1.75, 0.75        # высота кладки до зубцов, толщина: над зубцами из кадра видны скалы за стеной


def _courses(a, x0, x1, z0, z1, d, rng, rows=3, y=0.0, face=-1):
    """Лицевая кладка: ряды блоков с перевязкой швов, каждый блок чуть выступает (рельеф кладки)."""
    h = (z1 - z0) / rows
    for r in range(rows):
        x = x0 + (0.0 if r % 2 == 0 else -rng.uniform(0.25, 0.4))
        while x < x1 - 0.05:
            w = rng.uniform(0.55, 0.85)
            xa, xb = max(x, x0), min(x + w, x1)
            if xb - xa > 0.12:
                out = rng.uniform(0.03, 0.07)
                a.add(p_box((xb - xa - 0.04, 0.12, h - 0.05), loc=((xa + xb) / 2, y + face * (d / 2 + out - 0.06),
                                                                  z0 + h * (r + 0.5)), bevel=0.025),
                      STONE_W if rng.random() < 0.3 else STONE)
            x += w


def _merlons(a, x0, x1, z, d, rng, w=0.46, gap=0.38):
    """Зубцы по верху стены."""
    n = max(1, int(round((x1 - x0 + gap) / (w + gap))))
    step = (x1 - x0) / n
    for k in range(n):
        cx = x0 + step * (k + 0.5)
        a.add(p_box((min(w, step - 0.1), d * 0.9, 0.42), loc=(cx, 0.0, z + 0.21), rot=(0, 0, rng.uniform(-2, 2)),
                    bevel=0.035), STONE)


def wall(a, seed=1):
    """Сегмент стены 2 м: контрфорс в начале (x = -WALL_SEG/2), ядро кладки, лицевые ряды блоков, карниз и зубцы."""
    rng = random.Random(seed)
    L = WALL_SEG
    a.add(p_box((L + 0.02, WALL_D - 0.1, WALL_H), loc=(0, 0, WALL_H / 2 - 0.15), bevel=0.0), "stone_dark")
    _courses(a, -L / 2, L / 2, -0.1, WALL_H - 0.12, WALL_D, rng, rows=3)
    a.add(p_box((L + 0.04, WALL_D + 0.12, 0.16), loc=(0, 0, WALL_H - 0.04), bevel=0.03), STONE)       # карниз
    _merlons(a, -L / 2 + 0.25, L / 2 - 0.05, WALL_H + 0.04, WALL_D, rng)
    a.add(p_box((0.5, WALL_D + 0.3, WALL_H + 0.25), loc=(-L / 2, 0, (WALL_H + 0.25) / 2 - 0.15), bevel=0.04),
          STONE)                                                                                   # контрфорс
    a.add(p_box((0.62, WALL_D + 0.42, 0.18), loc=(-L / 2, 0, WALL_H + 0.18), bevel=0.03), STONE)


def wall_ruin(a, seed=3):
    """Руина стены: обломок той же кладки — ряды блоков обрываются ступенями (верх неровный), на изломе —
    сдвинутые камни; у подножия — выпавшие блоки."""
    rng = random.Random(seed)
    L, row = WALL_SEG, 0.42
    rows = [3, 3, 2, 1]                                          # рядов кладки по четвертям: обрыв ступенями
    for k, nr in enumerate(rows):                                # ядро
        x0, x1 = -L / 2 + L * k / 4, -L / 2 + L * (k + 1) / 4
        h = nr * row - 0.06
        a.add(p_box((x1 - x0 + 0.02, WALL_D - 0.1, h + 0.12), loc=((x0 + x1) / 2, 0, (h + 0.12) / 2 - 0.15),
                    bevel=0.0), "stone_dark")
    for r in range(max(rows)):                                   # ряды блоков с перевязкой швов, лицо и тыл
        z = -0.1 + row * (r + 0.5)
        x = -L / 2 + (0.0 if r % 2 == 0 else -rng.uniform(0.25, 0.4))
        while x < L / 2 - 0.05:
            w = rng.uniform(0.5, 0.8)
            xa, xb = max(x, -L / 2), min(x + w, L / 2)
            q = min(3, max(0, int(((xa + xb) / 2 + L / 2) / (L / 4))))
            if xb - xa > 0.12 and r < rows[q]:
                for face in (-1, 1):
                    out = rng.uniform(0.03, 0.07)
                    a.add(p_box((xb - xa - 0.04, 0.12, row - 0.05), loc=((xa + xb) / 2, face * (WALL_D / 2 + out - 0.06), z),
                                rot=(0, 0, rng.uniform(-2, 2) if r == rows[q] - 1 else 0.0), bevel=0.025),
                          STONE_W if rng.random() < 0.3 else STONE)
            x += w
    for j in range(5):                                           # выпавшие блоки
        x = rng.uniform(-0.2, 0.95) * L / 2
        y = -WALL_D / 2 - rng.uniform(0.25, 0.75)
        sz = (rng.uniform(0.38, 0.55), rng.uniform(0.28, 0.38), rng.uniform(0.24, 0.34))
        a.add(p_box(sz, loc=(x, y, sz[2] / 2 - 0.05), rot=(rng.uniform(-14, 14), rng.uniform(-14, 14), rng.uniform(-35, 35)),
                    bevel=0.03), STONE if j % 2 else STONE_W)


def wall_pillar(a, seed=5):
    """Столб-контрфорс в конце ломаной стены (на изломах и у ворот)."""
    rng = random.Random(seed)
    a.add(p_box((0.62, WALL_D + 0.34, WALL_H + 0.3), loc=(0, 0, (WALL_H + 0.3) / 2 - 0.15), bevel=0.04), STONE)
    a.add(p_box((0.74, WALL_D + 0.46, 0.2), loc=(0, 0, WALL_H + 0.24), bevel=0.03), STONE)
    a.add(p_box((0.48, 0.48, 0.32), loc=(0, 0, WALL_H + 0.5), rot=(0, 0, rng.uniform(-5, 5)), bevel=0.04), STONE)


GATE_W = 4.6                       # ворота: ширина вдоль X (стена примыкает к пилонам)
GATE_H = 2.95                      # высота пилонов до карниза
GATE_D = 1.15                      # глубина пилонов вдоль Y


def gate(a, seed=7):
    """Ворота заставы, как в промпте: два пилона кладки, арка с замковым камнем, створки из досок с железными
    полосами распахнуты внутрь (к +Y, от поля), над аркой — зубцы. Пивот — земля под серединой проёма.
    Верх зубцов — 3.45 м: ворота у заднего края поля целиком в кадре боя (верх кадра там на 3.6 м)."""
    rng = random.Random(seed)
    pw, H = 1.1, GATE_H
    for sx in (-1, 1):                                           # пилоны
        cx = sx * (GATE_W / 2 - pw / 2)
        a.add(p_box((pw, 1.15, H), loc=(cx, 0, H / 2 - 0.15), bevel=0.0), "stone_dark")
        _courses(a, cx - pw / 2, cx + pw / 2, -0.1, H - 0.1, 1.15, rng, rows=4)
        a.add(p_box((pw + 0.16, 1.3, 0.18), loc=(cx, 0, H + 0.0), bevel=0.03), STONE)
        _merlons(a, cx - pw / 2, cx + pw / 2, H + 0.08, 1.15, rng, w=0.42, gap=0.2)
    # арка: прямоугольник кладки над проёмом с полукруглым вырезом
    ow = GATE_W - 2 * pw                                          # проём
    r, zc = ow / 2, 1.45
    pts = [(-ow / 2, zc)]
    for k in range(1, 8):
        t = math.pi - math.pi * k / 8
        pts.append((math.cos(t) * r, zc + math.sin(t) * r))
    pts += [(ow / 2, zc), (ow / 2, H), (-ow / 2, H)]
    arch = p_prism(pts[::-1], 1.0, loc=(0, 0, 0))
    a.add(arch, lambda f: "stone_light" if f.normal.z > 0.6 else ("stone_dark" if f.normal.z < -0.2 else "stone_mid"))
    a.add(p_box((ow + 0.1, 1.2, 0.16), loc=(0, 0, H), bevel=0.03), STONE)
    _merlons(a, -ow / 2, ow / 2, H + 0.08, 1.1, rng, w=0.44, gap=0.32)
    a.add(p_box((0.34, 0.14, 0.42), loc=(0, -0.53, zc + r - 0.02), bevel=0.03), "stone_light")      # замковый камень
    # створки: распахнуты внутрь, петли — у пилонов с тыльной стороны
    dw, dh = ow / 2 - 0.04, 2.3
    for sx in (-1, 1):
        hinge = Vector((sx * (ow / 2 - 0.03), 0.42, 0.0))
        ang = math.radians(sx * -72)
        M = Matrix.Translation(hinge) @ Matrix.Rotation(ang, 4, "Z")
        for k in range(4):                                       # доски
            x = -sx * (dw * (k + 0.5) / 4)
            part = p_box((dw / 4 - 0.02, 0.11, dh - (0.08 if k % 2 else 0.0)), loc=(x, 0.06, dh / 2 - 0.05), bevel=0.012)
            bmesh.ops.transform(part, matrix=M, verts=part.verts)
            a.add(part, ("wood_mid", "wood_mid2", "wood_mid", "wood_dark")[k])
        for z in (0.45, 1.25, 2.05):                             # железные полосы
            part = p_box((dw - 0.06, 0.04, 0.12), loc=(-sx * dw / 2, -0.01, z), bevel=0.01)
            bmesh.ops.transform(part, matrix=M, verts=part.verts)
            a.add(part, "iron_dark")
    # брусчатка в проёме
    for k in range(6):
        a.add(p_box((ow / 2 - 0.08, 0.5, 0.1), loc=((k % 2 - 0.5) * ow / 2, -0.4 + (k // 2) * 0.42, -0.03),
                    rot=(0, 0, rng.uniform(-3, 3)), bevel=0.02), "stone_mid" if k % 3 else "stone_warm")


TOWER_SHAFT = 3.5                  # каменный ствол башни до смотровой площадки


def bastion(a, seed=9, r=1.0, h=2.45):
    """Угловой бастион стены: невысокая круглая башня кладки с зубцами (на углах стены)."""
    rng = random.Random(seed)
    a.add(p_cyl(r * 1.05, r, h, 10, loc=(0, 0, -0.15)), STONE)
    for z in (0.65, 1.55):                                       # пояса кладки
        a.add(p_cyl(r * 1.06, r * 1.06, 0.12, 10, loc=(0, 0, z), spin=rng.uniform(0, 36)), "stone_light")
    a.add(p_cyl(r * 1.14, r * 1.14, 0.18, 10, loc=(0, 0, h - 0.2)), STONE)
    for k in range(5):
        ang = math.tau * k / 5 + 0.3
        a.add(p_box((0.45, 0.42, 0.42), loc=(math.cos(ang) * r * 0.92, math.sin(ang) * r * 0.92, h + 0.18),
                    rot=(0, 0, math.degrees(ang)), bevel=0.035), STONE)


def tower(a, seed=11, h=TOWER_SHAFT):
    """Сторожевая башня: восьмигранный каменный ствол с поясами кладки и бойницами, вход у подножия (к -Y),
    деревянная смотровая площадка на кронштейнах с перилами, шатровая кровля из сланца со шпилем.
    Верх шпиля — TOWER_H: у бока поля (y около 1) башня целиком под верхом кадра боя."""
    rng = random.Random(seed)
    r0, r1 = 1.1, 0.97
    a.add(p_cyl(r0, r1, h, 8, loc=(0, 0, -0.15), spin=22.5), STONE)
    for z in (0.7, 1.65, 2.6):                                   # пояса кладки
        f = (z + 0.15) / h
        rr = r0 + (r1 - r0) * f + 0.03
        a.add(p_cyl(rr, rr, 0.12, 8, loc=(0, 0, z), spin=22.5 + rng.uniform(-2, 2)), "stone_light")
    a.add(p_cyl(r1 + 0.17, r1 + 0.17, 0.22, 8, loc=(0, 0, h - 0.25), spin=22.5), STONE)               # карниз
    # вход и бойницы
    a.add(p_box((0.6, 0.2, 1.12), loc=(0, -r0 + 0.07, 0.48), bevel=0.02), "black")
    a.add(p_box((0.8, 0.24, 0.14), loc=(0, -r0 + 0.08, 1.08), bevel=0.02), "stone_light")
    for z, ang in ((2.1, 90), (2.1, -30), (2.1, 210), (2.85, 30)):
        c = Vector((math.cos(math.radians(ang)), math.sin(math.radians(ang)), 0)) * (r0 * 0.93)
        a.add(p_box((0.12, 0.2, 0.42), loc=(c.x, c.y, z), rot=(0, 0, ang + 90), bevel=0.0), "black")
    # смотровая площадка
    zf = h - 0.05
    rp = 1.45
    a.add(p_cyl(rp, rp, 0.16, 8, loc=(0, 0, zf), spin=22.5), WOOD)
    for k in range(8):
        ang = math.tau * (k + 0.5) / 8
        c = Vector((math.cos(ang), math.sin(ang), 0))
        SA._limb(a, c * (r1 - 0.05) + Vector((0, 0, zf - 0.55)), c * (rp - 0.1) + Vector((0, 0, zf)), 0.06, 0.06,
                 "wood_dark", seg=4)
    posts = []
    for k in range(8):
        ang = math.tau * k / 8 + math.pi / 8
        c = Vector((math.cos(ang), math.sin(ang), 0)) * (rp - 0.1)
        posts.append(c)
        a.add(p_box((0.12, 0.12, 1.25), loc=(c.x, c.y, zf + 0.7), rot=(0, 0, math.degrees(ang)), bevel=0.02), "wood_dark")
    for k in range(8):                                           # перила
        p0, p1 = posts[k], posts[(k + 1) % 8]
        SA._limb(a, p0 + Vector((0, 0, zf + 0.62)), p1 + Vector((0, 0, zf + 0.62)), 0.05, 0.05, "wood_mid", seg=4)
    # кровля
    zr = zf + 1.3
    a.add(p_cyl(1.75, 0.0, 1.5, 8, loc=(0, 0, zr), spin=22.5), SLATE)
    a.add(p_cyl(1.8, 1.8, 0.1, 8, loc=(0, 0, zr - 0.05), spin=22.5), "slate_dark")
    a.add(p_cyl(0.05, 0.04, 0.55, 5, loc=(0, 0, zr + 1.4)), "iron_dark")
    a.add(p_box((0.32, 0.03, 0.19), loc=(0.17, 0, zr + 1.8), bevel=0.005), "cream")


TOWER_H = TOWER_SHAFT - 0.05 + 1.3 + 1.4 + 0.55


# ---------------------------------------------------------------------------------------
# кузня
# ---------------------------------------------------------------------------------------
FORGE_FIRE = (-0.62, 0.42, 0.86)          # где над горном стоит FX_Flame_Small (в осях ассета)
FORGE_SMOKE = (-0.62, 0.62, 3.75)          # верх трубы — сокет дыма


def forge(a, seed=13):
    """Кузня под навесом, как в промпте: навес из досок на четырёх столбах (высокая сторона к -Y), каменный горн с
    углями и зонтом-трубой сквозь кровлю, наковальня на колоде, бочка для закалки, точило, стойка с инструментом,
    мехи. Пивот — земля под серединой."""
    import build_vitaria as V
    rng = random.Random(seed)
    W, D = 3.0, 2.2
    H0, H1 = 2.45, 1.95
    yf, yb = -D / 2, D / 2
    for sx in (-1, 1):
        for y, h in ((yf, H0), (yb, H1)):
            a.add(p_box((0.16, 0.16, h + 0.15), loc=(sx * (W / 2 - 0.1), y, (h + 0.15) / 2 - 0.12), bevel=0.03), WOOD)
    for y, z in ((yf, H0), (yb, H1)):
        a.add(p_box((W + 0.3, 0.14, 0.16), loc=(0, y, z), bevel=0.03), "wood_dark")
    y0, z0 = yf - 0.3, H0 + 0.1
    y1, z1 = yb + 0.3, H1 + 0.02
    L = math.hypot(y1 - y0, z1 - z0)
    ang = math.degrees(math.atan2(z1 - z0, y1 - y0))
    n = 9
    for k in range(n):                                           # кровля из досок вдоль ската
        x = -W / 2 - 0.15 + (W + 0.3) * (k + 0.5) / n
        if abs(x - FORGE_FIRE[0]) < 0.32:                        # проём под трубу
            continue
        a.add(p_box(((W + 0.3) / n - 0.03, L, 0.07), loc=(x, (y0 + y1) / 2, (z0 + z1) / 2 + rng.uniform(-0.01, 0.01)),
                    rot=(ang, 0, rng.uniform(-1, 1)), bevel=0.012),
              lambda f, c=("wood_mid", "wood_mid2", "wood_dark")[k % 3]: "wood_dark" if f.normal.z < -0.3 else c)
    # горн: каменный короб, угли, зонт и труба
    fx, fy, fz = FORGE_FIRE
    a.add(p_box((1.25, 0.95, 0.9), loc=(fx, fy, 0.3), bevel=0.04), STONE)
    a.add(p_box((1.05, 0.75, 0.06), loc=(fx, fy, fz - 0.13), bevel=0.0), "coal")
    for k in range(9):
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0.0, 0.32)
        a.add(p_ico(0.08, 1, loc=(fx + math.cos(ang) * d, fy + math.sin(ang) * d * 0.8, fz - 0.1), scl=(1, 1, 0.6)),
              "ember" if k % 2 else "glow")
    a.add(p_box((1.25, 0.95, 0.12), loc=(fx, fy, fz + 0.78), bevel=0.03), "stone_dark")                 # зонт
    for sx in (-1, 1):
        a.add(p_box((0.14, 0.9, 0.8), loc=(fx + sx * 0.55, fy, fz + 0.35), bevel=0.02), STONE)
    a.add(p_box((0.48, 0.48, 2.0), loc=(fx, fy + 0.2, fz + 1.75), bevel=0.04), STONE)                   # труба
    a.add(p_box((0.58, 0.58, 0.14), loc=(fx, fy + 0.2, fz + 2.78), bevel=0.03), "stone_dark")
    # мехи у горна
    a.add(p_box((0.55, 0.42, 0.16), loc=(fx - 0.9, fy + 0.05, 0.62), rot=(0, 12, 0), bevel=0.05), "leather")
    a.add(p_cyl(0.05, 0.03, 0.4, 6, loc=(fx - 0.65, fy + 0.05, 0.66), rot=(0, 80, 0)), "iron_dark")
    # наковальня на колоде
    ax, ay = 0.55, -0.2
    a.add(p_cyl(0.3, 0.27, 0.48, 8, loc=(ax, ay, -0.06)), lambda f: "wood_pale" if f.normal.z > 0.9 else "bark")
    a.add(p_box((0.2, 0.15, 0.1), loc=(ax, ay, 0.46), bevel=0.012), "iron_dark")
    a.add(p_box((0.46, 0.2, 0.1), loc=(ax, ay, 0.55), bevel=0.012), "iron_dark")
    a.add(p_box((0.54, 0.22, 0.08), loc=(ax, ay, 0.63), bevel=0.012), by_normal("iron_light", "iron", "iron_dark", 0.5))
    a.add(p_cyl(0.09, 0.0, 0.28, 6, loc=(ax + 0.27, ay, 0.63), rot=(0, 90, 0)), "iron")
    SA._limb(a, (ax - 0.1, ay - 0.05, 0.69), (ax - 0.38, ay - 0.25, 0.71), 0.022, 0.02, "wood_light", seg=5)   # молот
    a.add(p_box((0.1, 0.14, 0.09), loc=(ax - 0.06, ay - 0.03, 0.7), rot=(0, 0, 35), bevel=0.01), "iron_dark")
    # бочка для закалки с водой
    V.build_barrel(a, loc=(1.15, 0.55, 0.0))
    a.add(p_cyl(0.205, 0.205, 0.02, 12, loc=(1.15, 0.55, 0.7)), "water")
    # точило
    a.add(p_cyl(0.3, 0.3, 0.12, 10, loc=(0.95, -0.82, 0.62), rot=(90, 0, 0)), "stone_light")
    for sx in (-1, 1):
        a.add(p_box((0.07, 0.07, 0.6), loc=(0.95 + sx * 0.14, -0.82, 0.28), bevel=0.01), "wood_dark")
    # стойка с инструментом у задней стороны
    a.add(p_box((1.2, 0.08, 0.08), loc=(0.6, yb - 0.15, 1.35), bevel=0.015), "wood_dark")
    for k, x in enumerate((0.2, 0.5, 0.8, 1.05)):
        SA._limb(a, (x, yb - 0.2, 0.55), (x + 0.02, yb - 0.2, 1.4), 0.025, 0.022, "wood_light", seg=4)
        a.add(p_box((0.16 if k % 2 else 0.1, 0.05, 0.14 if k % 2 else 0.22), loc=(x + 0.02, yb - 0.21, 0.5),
                    bevel=0.01), "iron")


def sconce(a, seed=15):
    """Светильник на стене: кронштейн, чаша с углями. Пивот — чаша (ставится выше земли, на пилон ворот)."""
    rng = random.Random(seed)
    SA._limb(a, (0, 0.32, -0.3), (0, 0.06, -0.05), 0.035, 0.03, "iron_dark", seg=5)
    a.add(p_box((0.18, 0.06, 0.32), loc=(0, 0.34, -0.25), bevel=0.01), "iron_dark")
    a.add(p_cyl(0.07, 0.16, 0.16, 8, loc=(0, 0, -0.12)), by_normal("iron_dark", "iron", "iron_dark", 0.6))
    for k in range(4):
        ang = rng.uniform(0, math.tau)
        a.add(p_ico(0.045, 1, loc=(math.cos(ang) * 0.06, math.sin(ang) * 0.06, 0.02), scl=(1, 1, 0.7)),
              "ember" if k % 2 else "glow")


SCONCE_TOP = 0.04


def torch(a, seed=21):
    SA.torch(a, seed)


TORCH_TOP = SA.TORCH_TOP


# ---------------------------------------------------------------------------------------
# скалы, природа
# ---------------------------------------------------------------------------------------
def spire(rng, x, y, r, h, tilt=7.0, z=-0.15, seg=None):
    """Гранёный скальный шпиль: призма в 5–6 граней сужается кверху, срез макушки косой, наклон — несколько
    градусов. Отдаёт bmesh (нормали пересчитаны) с подошвой на высоте z."""
    seg = seg or rng.choice((5, 6))
    b = p_cyl(r, r * rng.uniform(0.5, 0.72), h, seg, spin=rng.uniform(0, 72), bevel=min(0.06, r * 0.12))
    ang = rng.uniform(0, math.tau)
    k = rng.uniform(0.18, 0.45)
    top = max(v.co.z for v in b.verts)
    for v in b.verts:
        if v.co.z > top - 0.12:
            v.co.z += (v.co.x * math.cos(ang) + v.co.y * math.sin(ang)) * k
    M = Matrix.Translation(Vector((x, y, z))) @ Matrix.Rotation(math.radians(rng.uniform(-tilt, tilt)), 4, "X") @ \
        Matrix.Rotation(math.radians(rng.uniform(-tilt, tilt)), 4, "Y")
    bmesh.ops.transform(b, matrix=M, verts=b.verts)
    b.normal_update()
    return b


def crag(a, seed=31, w=2.8, d=1.9, h=3.2, n=7):
    """Скальная гряда: гранёные серые шпили разной высоты, сужаются кверху, косой срез макушки светлый;
    у подножия — осыпь."""
    rng = random.Random(seed)
    for k in range(n):
        x = rng.uniform(-w / 2, w / 2) * 0.85
        y = rng.uniform(-d / 2, d / 2) * 0.85
        hh = h * rng.uniform(0.4, 1.0) * (1.0 - 0.35 * abs(x) / (w / 2))
        a.add(spire(rng, x, y, rng.uniform(0.4, 0.62), hh), ROCK)
    for j in range(5):
        ang = rng.uniform(0, math.tau)
        rr = rng.uniform(0.6, 1.0) * max(w, d) / 2 + 0.2
        a.add(p_ico(rng.uniform(0.14, 0.28), 1, loc=(math.cos(ang) * rr, math.sin(ang) * rr * 0.8, 0.0), jitter=0.25,
                    rng=rng, scl=(1, 0.9, 0.6), cut=-0.05), ROCK)


def boulder(a, seed=41, size=0.8, n=2):
    """Гранитный валун: крупная огранка, светлая макушка, рядом — камни поменьше."""
    rng = random.Random(seed)
    for k in range(n):
        s = size * (1.0 if k == 0 else rng.uniform(0.4, 0.6))
        off = (0, 0) if k == 0 else (rng.choice([-1, 1]) * size * rng.uniform(0.8, 1.05), rng.uniform(-0.6, 0.6) * size)
        a.add(p_ico(s, 2 if (k == 0 and size > 0.6) else 1, loc=(off[0], off[1], s * 0.25), jitter=0.2, rng=rng,
                    scl=(1.0, rng.uniform(0.8, 1.0), 0.72), rot=(0, 0, rng.uniform(0, 360)), cut=-s * 0.3), ROCK)


def scree(a, seed=45, n=7, r=0.6):
    """Осыпь: плоские угловатые камни россыпью."""
    rng = random.Random(seed)
    for k in range(n):
        ang = rng.uniform(0, math.tau)
        d = r * math.sqrt(rng.random())
        s = rng.uniform(0.08, 0.2)
        a.add(p_box((s * 1.6, s * 1.2, s * 0.7), loc=(math.cos(ang) * d, math.sin(ang) * d, s * 0.2),
                    rot=(rng.uniform(-12, 12), rng.uniform(-12, 12), rng.uniform(0, 180)), bevel=0.02),
              ROCK if k % 3 else "stone_mid")


def juniper(a, seed=51, size=1.0):
    """Можжевельник: низкий распластанный тёмный куст из плоских комьев."""
    rng = random.Random(seed)
    for j in range(4):
        ang = math.tau * j / 4 + rng.uniform(-0.4, 0.4)
        d = 0.0 if j == 0 else rng.uniform(0.3, 0.45)
        r = (0.42 if j == 0 else rng.uniform(0.28, 0.36)) * size
        a.add(p_ico(r, 1, loc=(math.cos(ang) * d * size, math.sin(ang) * d * size, 0.12 * size),
                    scl=(1.3, 1.2, 0.55), jitter=0.14, rng=rng, cut=-0.08 * size),
              lambda f: "pine_dark" if f.normal.z < -0.2 else ("pine_light" if f.normal.z > 0.75 else "pine_mid"))


def flowers(a, seed=55, col="flower_w", n=4):
    """Альпийские цветы: эдельвейсы (белые звёзды) или горечавка (синие) на коротких стеблях и пучок травы."""
    rng = random.Random(seed)
    for j in range(n):
        ang = math.tau * j / n + rng.uniform(-0.4, 0.4)
        d = 0.0 if j == 0 else rng.uniform(0.08, 0.16)
        FA._daisy(a, math.cos(ang) * d, math.sin(ang) * d, rng.uniform(0.1, 0.18), rng.uniform(0.06, 0.08), col, rng)
    for j in range(4):
        ang = rng.uniform(0, math.tau)
        p0 = Vector((math.cos(ang) * 0.05, math.sin(ang) * 0.05, -0.02))
        p1 = p0 + Vector((math.cos(ang) * 0.06, math.sin(ang) * 0.06, rng.uniform(0.1, 0.16)))
        SA._limb(a, p0, p1, 0.02, 0.0, "blade" if j % 2 else "blade_dark", seg=3)


def _crate(a, s, loc, rz=0.0):
    """Ящик заставы: брусок с фаской и косыми накладками спереди и сзади (дешевле Prop_Crate: штабелями)."""
    o = Vector(loc)
    m = Matrix.Translation(o) @ Matrix.Rotation(math.radians(rz), 4, "Z")
    part = p_box((s, s, s), loc=(0, 0, s / 2), bevel=0.035)
    bmesh.ops.transform(part, matrix=m, verts=part.verts)
    a.add(part, by_normal("wood_light", "wood_mid", "wood_dark", 0.6))
    for sy in (-1, 1):
        part = p_box((s * 1.25, 0.04, 0.08), loc=(0, sy * (s / 2 + 0.01), s / 2), rot=(0, 45 * sy, 0), bevel=0.0)
        bmesh.ops.transform(part, matrix=m, verts=part.verts)
        a.add(part, "wood_dark")


def crate_stack(a, seed=61, big=True):
    """Штабель ящиков с бочкой: склад заставы."""
    import build_vitaria as V
    rng = random.Random(seed)
    _crate(a, 0.62, (0.0, 0.0, 0.0), rng.uniform(-6, 6))
    _crate(a, 0.56, (0.64, 0.05, 0.0), rng.uniform(-8, 8))
    if big:
        _crate(a, 0.5, (0.3, 0.0, 0.62), rng.uniform(-10, 10))
        V.build_barrel(a, loc=(-0.64, 0.15, 0.0))
    _crate(a, 0.46, (0.25, -0.58, 0.0), rng.uniform(-12, 12))


# ---------------------------------------------------------------------------------------
# дальние вершины (небо)
# ---------------------------------------------------------------------------------------
def peak(a, seed=71, h=26.0, r=11.0, snow=0.55, k=7):
    """Дальняя вершина: гранёный конус с рваными гранями, снежная шапка выше доли snow высоты, второй пик сбоку.
    Пивот — подошва (её прячут облака под островом)."""
    rng = random.Random(seed)
    for j, (hh, rr, off) in enumerate(((h, r, Vector((0, 0, 0))),
                                       (h * rng.uniform(0.55, 0.7), r * 0.62,
                                        Vector((rng.choice([-1, 1]) * r * 0.75, rng.uniform(-0.3, 0.3) * r, 0))))):
        b = bmesh.new()
        top = b.verts.new(off + Vector((rng.uniform(-0.1, 0.1) * rr, rng.uniform(-0.1, 0.1) * rr, hh)))
        rings = []
        for zf, rf in ((0.0, 1.0), (0.38, 0.72), (0.72, 0.36)):
            ring = []
            for i in range(k):
                ang = math.tau * i / k + rng.uniform(-0.18, 0.18) + j
                rad = rr * rf * rng.uniform(0.8, 1.15)
                ring.append(b.verts.new(off + Vector((math.cos(ang) * rad, math.sin(ang) * rad,
                                                      hh * zf + rng.uniform(-0.05, 0.05) * hh * (zf > 0)))))
            rings.append(ring)
        for r0, r1 in zip(rings, rings[1:]):
            for i in range(k):
                i2 = (i + 1) % k
                b.faces.new((r0[i], r0[i2], r1[i2], r1[i]))
        for i in range(k):
            b.faces.new((rings[-1][i], rings[-1][(i + 1) % k], top))
        bmesh.ops.triangulate(b, faces=b.faces[:])
        bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
        b.normal_update()
        line = hh * snow

        def col(f, line=line, base=off.z):
            z = f.calc_center_median().z - base
            if z > line and f.normal.z > -0.2:
                return "foam"
            if z > line * 0.82 and f.normal.z > 0.45:
                return "foam"
            return "rock" if f.normal.z > 0.6 else ("rock_dark" if f.normal.z > 0.1 else "stone_dark")
        a.add(b, col)


ASSETS = [
    ("MountainPass", "Mount_Wall", "Стена с зубцами, сегмент 2 м (контрфорс в начале)", wall),
    ("MountainPass", "Mount_WallPillar", "Столб стены на изломе", wall_pillar),
    ("MountainPass", "Mount_WallRuin", "Руина стены", wall_ruin),
    ("MountainPass", "Mount_Gate", "Ворота заставы", gate),
    ("MountainPass", "Mount_Bastion", "Угловой бастион", bastion),
    ("MountainPass", "Mount_Tower", "Сторожевая башня", tower),
    ("MountainPass", "Mount_Forge", "Кузня под навесом", forge),
    ("MountainPass", "Mount_Sconce", "Светильник на стене", sconce),
    ("MountainPass", "Mount_Torch", "Факел на шесте", torch),
    ("MountainPass", "Mount_CrateStack", "Штабель ящиков с бочкой", crate_stack),
    ("MountainPass", "Mount_CrateStack_S", "Ящики", lambda a: crate_stack(a, 62, False)),
    ("MountainPass", "Mount_Crag_A", "Скальная гряда", lambda a: crag(a, 31, 3.0, 2.0, 3.6, 8)),
    ("MountainPass", "Mount_Crag_B", "Скальная гряда, низкая", lambda a: crag(a, 32, 2.2, 1.5, 2.0, 5)),
    ("MountainPass", "Mount_Boulder_A", "Гранитный валун", lambda a: boulder(a, 41, 0.95, 2)),
    ("MountainPass", "Mount_Boulder_B", "Камень", lambda a: boulder(a, 42, 0.45, 2)),
    ("MountainPass", "Mount_Scree", "Осыпь", scree),
    ("MountainPass", "Mount_Pine_A", "Горная сосна", lambda a: FA.spruce(a, 71, 5.0, 6, 0.85, 8, 0.3, (0.04, 0.0))),
    ("MountainPass", "Mount_Pine_B", "Горная сосна, малая", lambda a: FA.spruce(a, 72, 3.2, 5, 0.9, 7, 0.3, (0.02, 0.03))),
    ("MountainPass", "Mount_Pine_C", "Сосна на ветру", lambda a: FA.spruce(a, 73, 4.2, 6, 0.75, 7, 0.34, (0.09, 0.02))),
    ("MountainPass", "Mount_Juniper", "Можжевельник", juniper),
    ("MountainPass", "Mount_Flowers_W", "Эдельвейсы", lambda a: flowers(a, 55, "flower_w", 4)),
    ("MountainPass", "Mount_Flowers_B", "Горечавка", lambda a: flowers(a, 56, "roof_light", 4)),
    ("MountainPass", "Mount_Peak_A", "Дальняя вершина", lambda a: peak(a, 71, 26.0, 11.0, 0.55)),
    ("MountainPass", "Mount_Peak_B", "Дальняя вершина, острая", lambda a: peak(a, 72, 30.0, 9.0, 0.5, 6)),
    ("MountainPass", "Mount_Peak_C", "Дальняя вершина, пологая", lambda a: peak(a, 73, 18.0, 12.0, 0.62, 8)),
    # плитки поля биома: геометрия и UV — как у Hex_Tile_A/B/C, цвет — палитра Vitaria_Palette_MountainPass
    ("MountainPass", "Hex_Tile_Mount_A", "Гекс-плитка заставы A", TL.tile_plain(11, base=0.5)),
    ("MountainPass", "Hex_Tile_Mount_B", "Гекс-плитка заставы B (кочки)", TL.tile_tufts),
    ("MountainPass", "Hex_Tile_Mount_C", "Гекс-плитка заставы C (камни)", TL.tile_pebbles),
    ("Preview", "Preview_Troll", "Тролль-заглушка (превью)", SA.preview_troll),
    ("Preview", "Preview_Goblin", "Гоблин-заглушка (превью)", SA.preview_goblin),
]
