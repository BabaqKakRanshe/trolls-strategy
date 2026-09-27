"""
Гекс-плитки поля, препятствия на клетку и мелкий декор луга.

Плитка: шестигранная плита острым углом вдоль Y (как BattleBoardView.MakeCell), 1.96 м между
плоскими гранями — между соседями остаётся шов 4 см, в нём видна трава плато. Верх на 3.5 см
выше травы, фаска по краю — так на макете читается сетка без линий-обводок.
Верх окрашен рампой с шумом по вершинам: у каждой плитки своё пятно, как на макете.

Пивот — центр клетки на уровне травы (z = 0). Зоны расстановки — отдельные плитки: заливка
темнее поля и широкая тёмная кайма цвета команды. Зона отличается от поля рисунком, а не только
оттенком (ч/б, дальтонизм), а светлая подсветка клетки (hover игры) на ней самая контрастная.

Читаемость (game feel: игрок должен сразу видеть правила поля):
  - на плитках нет деталей тоньше 10 см: при ~67 px на метр они становятся «сыпью» в 1–3 пикселя
    и похожи на подбираемые предметы. Декор — 1–2 низкие кочки или плоских камня (≤ 6 см) в кольце
    0.6–0.85 м от центра; середина клетки чистая, боец не стоит в траве;
  - препятствие стоит на тёмном земляном пятаке: занятая клетка читается пятном ещё до силуэта,
    и её не спутать с декором вокруг поля. Высота 0.8–1.0 м — боец в соседней клетке за ним виден.
"""
import math
import random
from mathutils import Vector, Matrix, noise
import bmesh
from build_vitaria import ramp_uv, SW_UV, p_ico, p_cyl, by_normal
from vitaria_buildings.common import Frame
from .board import HEX

GAP = 0.035
TOP = 0.035
BOTTOM = -0.07
BEVEL = 0.034
ZONE_BAND = 0.1            # ширина тёмной каймы зоны (по перпендикуляру к ребру)


def _tile(a, ramp, rim, side, seed, base=0.5, spread=0.32, band=0.0):
    bm, uvl = a.bm, a.uv
    R = (HEX - GAP) / math.sqrt(3)            # описанный радиус плитки
    Ri = R - BEVEL / math.cos(math.radians(30))
    Rb = Ri - band / math.cos(math.radians(30))
    ang = [math.radians(30 + 60 * k) for k in range(6)]

    def corner(r, k):
        return Vector((r * math.cos(ang[k]), r * math.sin(ang[k]), 0))

    def edge_pt(r, k, t):
        return corner(r, k).lerp(corner(r, (k + 1) % 6), t)

    rng = random.Random(seed)
    off = Vector((rng.uniform(-9, 9), rng.uniform(-9, 9), seed * 1.37))

    def t_at(p):
        n1 = noise.noise(p * 0.85 + off)
        n2 = noise.noise(p * 2.1 + off * 1.7)
        return max(0.0, min(1.0, base + spread * (0.75 * n1 + 0.35 * n2)))

    # верх: центр + внутреннее кольцо (6) + край заливки (12: углы и середины рёбер)
    # [+ кайма зоны до края верха] + фаска + бок
    c = bm.verts.new((0, 0, TOP))
    inner = []                                   # смотрят на середины рёбер (60 + 60k)
    rot30 = Matrix.Rotation(math.radians(30), 3, "Z")
    for k in range(6):
        p = rot30 @ corner(Rb * 0.48, k)
        inner.append(bm.verts.new((p.x, p.y, TOP)))
    fill_edge, outer_top, outer_mid, outer_bot = [], [], [], []
    for k in range(6):
        for t in (0.0, 0.5):
            p = edge_pt(Ri, k, t)
            q = edge_pt(R, k, t)
            outer_top.append(bm.verts.new((p.x, p.y, TOP)))
            outer_mid.append(bm.verts.new((q.x, q.y, TOP - BEVEL * 0.55)))
            outer_bot.append(bm.verts.new((q.x, q.y, BOTTOM)))
            if band > 0:
                pb = edge_pt(Rb, k, t)
                fill_edge.append(bm.verts.new((pb.x, pb.y, TOP)))
    if band <= 0:
        fill_edge = outer_top

    def face(vs, col=None, ramp_on=False):
        try:
            f = bm.faces.new(vs)
        except ValueError:
            return None
        f.normal_update()
        if f.normal.z < -0.1:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = ramp_uv(ramp, t_at(l.vert.co)) if ramp_on else SW_UV[col]
        return f

    for k in range(6):
        face([c, inner[k], inner[(k + 1) % 6]], ramp_on=True)
    for k in range(6):
        i0, i1 = inner[k], inner[(k + 1) % 6]
        # сектор 60+60k .. 120+60k: середина ребра k, угол k+1, середина ребра k+1
        o0, o1, o2 = fill_edge[(2 * k + 1) % 12], fill_edge[(2 * k + 2) % 12], fill_edge[(2 * k + 3) % 12]
        face([i0, o0, o1], ramp_on=True)
        face([i0, o1, i1], ramp_on=True)
        face([i1, o1, o2], ramp_on=True)
    for k in range(12):
        j = (k + 1) % 12
        if band > 0:
            face([fill_edge[k], outer_top[k], outer_top[j], fill_edge[j]], col=rim)
        face([outer_top[k], outer_mid[k], outer_mid[j], outer_top[j]], col=rim)
        face([outer_mid[k], outer_bot[k], outer_bot[j], outer_mid[j]], col=side)


# ---------------------------------------------------------------------------------------
# крупный декор: кочки, камни, толстые пучки — не тоньше 5 см в самом узком месте
# ---------------------------------------------------------------------------------------
def _ring_spot(rng, r0=0.62, r1=0.72, a0=None):
    ang = rng.uniform(0, math.tau) if a0 is None else a0
    d = rng.uniform(r0, r1)
    return math.cos(ang) * d, math.sin(ang) * d, ang


def _mound(a, rng, x, y, z=TOP, s=1.0, cols=("sod", "moss")):
    """Низкая кочка дёрна: 2–3 приплюснутых икосферы, 20–26 см в поперечнике, не выше 6 см."""
    n = rng.choice((2, 3))
    for k in range(n):
        r = rng.uniform(0.075, 0.1) * s
        ox, oy = (0.0, 0.0) if k == 0 else (rng.uniform(-0.08, 0.08) * s, rng.uniform(-0.08, 0.08) * s)
        a.add(p_ico(r, 1, loc=(x + ox, y + oy, z - r * 0.08), scl=(1, rng.uniform(0.8, 1.0), 0.5), jitter=0.15,
                    rng=rng, rot=(0, 0, rng.uniform(0, 360)), cut=-r * 0.12), cols[k % 2])


def _pebble(a, rng, x, y, s, z=TOP):
    a.add(p_ico(s, 1, loc=(x, y, z + s * 0.12), scl=(1, rng.uniform(0.75, 1), 0.45), jitter=0.2, rng=rng,
                rot=(0, 0, rng.uniform(0, 360)), cut=-s * 0.3), by_normal("stone_light", "stone_mid", "stone_dark", 0.5))


def _blade(a, rng, x, y, z, h, ang, col, w=0.07, lean=0.05):
    """Толстая травинка-клин: 7 x 2.5 см у земли, сходится в точку; наклон наружу от центра пучка."""
    b = bmesh.new()
    bmesh.ops.create_cone(b, cap_ends=True, cap_tris=False, segments=4, radius1=0.5, radius2=0.0, depth=1.0)
    bmesh.ops.translate(b, vec=(0, 0, 0.5), verts=b.verts)
    bmesh.ops.scale(b, vec=(w, w * 0.38, h), verts=b.verts)
    tip = Vector((math.cos(ang), math.sin(ang), 0)) * lean
    for v in b.verts:
        if v.co.z > h * 0.99:
            v.co += tip
    bmesh.ops.rotate(b, matrix=Matrix.Rotation(ang + math.pi / 2, 3, "Z"), verts=b.verts, cent=(0, 0, 0))
    bmesh.ops.translate(b, vec=(x, y, z - 0.01), verts=b.verts)
    a.add(b, col)


def _fat_tuft(a, rng, x, y, z=0.0, h=0.2, n=4, cols=("blade", "blade_dark")):
    for k in range(n):
        ang = math.tau * k / n + rng.uniform(-0.4, 0.4)
        d = rng.uniform(0.015, 0.045)
        _blade(a, rng, x + math.cos(ang) * d, y + math.sin(ang) * d, z, h * rng.uniform(0.7, 1.05), ang,
               cols[k % len(cols)], lean=rng.uniform(0.04, 0.08))


def tile_plain(seed, ramp="hex_grass", rim="hex_rim", side="hex_side", base=0.5, band=0.0):
    return lambda a: _tile(a, ramp, rim, side, seed, base, band=band)


def tile_tufts(a):
    """B: две низкие кочки у края (бывшие цветы — белые точки читались как предметы)."""
    _tile(a, "hex_grass", "hex_rim", "hex_side", 21, 0.55)
    rng = random.Random(4)
    for a0 in (0.9, 3.6):
        x, y, _ = _ring_spot(rng, a0=a0 + rng.uniform(-0.3, 0.3))
        _mound(a, rng, x, y)


def tile_pebbles(a):
    """C: два плоских камня и кочка у края."""
    _tile(a, "hex_grass", "hex_rim", "hex_side", 33, 0.45)
    rng = random.Random(9)
    x, y, ang = _ring_spot(rng, a0=3.9)
    _pebble(a, rng, x, y, 0.085)
    _pebble(a, rng, x + math.cos(ang + 1.6) * 0.17, y + math.sin(ang + 1.6) * 0.17, 0.065)
    x, y, _ = _ring_spot(rng, a0=0.7)
    _mound(a, rng, x, y, s=0.9)


# ---------------------------------------------------------------------------------------
# препятствия: тёмный пятак ~1.5 м + предмет внутри круга 0.75 м, высота 0.8–1.0 м
# ---------------------------------------------------------------------------------------
PAD_R = 0.78


def _pad(a, rng, r=PAD_R, n=14):
    """Утоптанная земля под препятствием: верх soil_mid на 1 см выше плитки, край soil_dark.
    Ниже подсветки клетки и пингов игры (+5..6 см над травой) — они ложатся поверх пятака."""
    bm, uvl = a.bm, a.uv
    top, rim, bot = [], [], []
    for k in range(n):
        ang = math.tau * k / n
        rr = r * rng.uniform(0.9, 1.04)
        cs, sn = math.cos(ang), math.sin(ang)
        top.append(bm.verts.new((cs * rr * 0.86, sn * rr * 0.86, TOP + 0.008)))
        rim.append(bm.verts.new((cs * rr, sn * rr, TOP + 0.002)))
        bot.append(bm.verts.new((cs * rr * 1.01, sn * rr * 1.01, TOP - 0.03)))
    c = bm.verts.new((0, 0, TOP + 0.01))

    def face(vs, col):
        f = bm.faces.new(vs)
        f.normal_update()
        if f.normal.z < -0.1:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = SW_UV[col]

    for k in range(n):
        j = (k + 1) % n
        face([c, top[k], top[j]], "soil_mid")
        face([top[k], rim[k], rim[j], top[j]], "soil_mid")
        face([rim[k], bot[k], bot[j], rim[j]], "soil_dark")
    # пара камешков и кочка по краю пятака связывают его с травой
    for ang, s in ((rng.uniform(0, math.tau), 0.07), (rng.uniform(0, math.tau), 0.055)):
        _pebble(a, rng, math.cos(ang) * r * 0.8, math.sin(ang) * r * 0.8, s, z=TOP + 0.005)


def _rock(a, rng, x, y, r, flat=0.8, col=None, z=TOP + 0.005, tall=1.0):
    a.add(p_ico(r, 1, loc=(x, y, z + r * flat * 0.3 * tall), scl=(1, rng.uniform(0.8, 1.0), flat * tall), jitter=0.28,
                rng=rng, rot=(0, 0, rng.uniform(0, 360)), cut=-r * flat * tall * 0.35),
          col or by_normal("cliff_light", "cliff_mid", "cliff_deep", 0.55))


def obst_boulders(a):
    """Стоячий камень с двумя валунами: вертикальный угловатый силуэт — не спутать с кустом."""
    rng = random.Random(51)
    _pad(a, rng)
    _rock(a, rng, -0.05, 0.08, 0.42, 1.0, tall=1.75)
    _rock(a, rng, 0.38, -0.2, 0.3, 0.9)
    _rock(a, rng, -0.4, -0.3, 0.22, 0.8)
    for x, y in ((0.5, 0.28), (-0.52, 0.22)):
        _fat_tuft(a, rng, x, y, z=TOP + 0.005, h=0.18, n=4)


def obst_stump(a):
    rng = random.Random(52)
    _pad(a, rng)
    fr = Frame((0.0, 0.05, -0.02))
    fr.cyl(a, 0.46, 0.4, 0.66, 9, col=by_normal("wood_pale", "bark", "bark_dark", 0.7), bevel=0.03)
    fr.cyl(a, 0.3, 0.3, 0.02, 9, loc=(0, 0, 0.665), col="wood_yellow")          # годовые кольца
    for ang in (15, 95, 170, 250, 320):
        r = math.radians(ang)
        a.add(p_cyl(0.13, 0.05, 0.5, 5, loc=(math.cos(r) * 0.36, 0.05 + math.sin(r) * 0.36, -0.04), rot=(0, 72, ang)),
              "bark")
    # гриб-трутовик на боку: толстые полки вместо ножек-палочек
    for z, s in ((0.28, 1.0), (0.42, 0.75)):
        a.add(p_cyl(0.13 * s, 0.1 * s, 0.05 * s, 7, loc=(0.36, -0.2, z), rot=(0, 0, -30)), "burlap")
    _fat_tuft(a, rng, -0.55, 0.3, z=TOP + 0.005, h=0.2, n=4)


def obst_bush(a):
    """Густой куст тёмной хвойной зелени с ягодами: темнее и холоднее травы плиток."""
    rng = random.Random(53)
    _pad(a, rng)
    parts = [((-0.1, 0.08, 0.42), 0.5, "pine_mid"), ((0.34, -0.14, 0.3), 0.36, "pine_dark"),
             ((-0.42, -0.28, 0.26), 0.3, "pine_dark"), ((0.2, 0.4, 0.28), 0.3, "pine_mid")]
    for off, r, col in parts:
        a.add(p_ico(r, 1, loc=off, jitter=0.12, rng=rng, scl=(1, 1, 0.9)), by_normal("pine_light", col, col, 0.75))
    for k in range(8):
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.25, 0.9))).normalized()
        base, r = parts[k % 2][0], parts[k % 2][1]
        p = Vector(base) + Vector((d.x, d.y, d.z * 0.9)) * r * 0.96
        a.add(p_ico(0.06, 1, loc=p), "berry")


# ---------------------------------------------------------------------------------------
# декор плато (сливается в Arena_Meadow_Scatter, в Unity отдельным FBX не выгружается)
# ---------------------------------------------------------------------------------------
def env_tuft_a(a):
    _fat_tuft(a, random.Random(61), 0, 0, h=0.22, n=5)


def env_tuft_b(a):
    _fat_tuft(a, random.Random(62), 0, 0, h=0.3, n=6, cols=("blade_dark", "moss", "blade_dark"))


def env_flowers(a):
    """Кустик с тремя крупными цветами 9 см: пятно цвета, а не россыпь точек."""
    rng = random.Random(63)
    _fat_tuft(a, rng, 0, 0, h=0.2, n=4)
    for k, col in enumerate(("flower_y", "wheat", "flower_y")):
        ang = math.tau * k / 3 + 0.5
        x, y = math.cos(ang) * 0.09, math.sin(ang) * 0.09
        a.add(p_cyl(0.025, 0.022, 0.17, 5, loc=(x, y, 0.0)), "blade_dark")
        a.add(p_ico(0.045, 1, loc=(x, y, 0.19), scl=(1, 1, 0.55), rot=(0, 0, k * 40)), col)


ASSETS = [
    ("Arena", "Hex_Tile_A", "Гекс-плитка A", tile_plain(11, base=0.5)),
    ("Arena", "Hex_Tile_B", "Гекс-плитка B (кочки)", tile_tufts),
    ("Arena", "Hex_Tile_C", "Гекс-плитка C (камни)", tile_pebbles),
    ("Arena", "Hex_Tile_Blue", "Гекс-плитка зоны игрока",
     tile_plain(12, "zone_blue", "hex_rim_blue", "hex_side_blue", 0.5, band=ZONE_BAND)),
    ("Arena", "Hex_Tile_Red", "Гекс-плитка зоны врага",
     tile_plain(13, "zone_red", "hex_rim_red", "hex_side_red", 0.5, band=ZONE_BAND)),
    ("Arena", "Obst_Boulders", "Препятствие: стоячий камень", obst_boulders),
    ("Arena", "Obst_Stump", "Препятствие: пень", obst_stump),
    ("Arena", "Obst_Bush", "Препятствие: куст", obst_bush),
    ("ArenaDecor", "Env_Tuft_A", "Пучок травы (декор плато)", env_tuft_a),
    ("ArenaDecor", "Env_Tuft_B", "Пучок травы высокий (декор плато)", env_tuft_b),
    ("ArenaDecor", "Env_Flowers_A", "Цветы (декор плато)", env_flowers),
]
