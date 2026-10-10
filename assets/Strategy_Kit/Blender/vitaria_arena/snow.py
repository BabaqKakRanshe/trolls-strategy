"""
Арена «Снега» (Arena_Snow) — третий биом боя, по макету «Snow»: тот же парящий остров с полем 9x5, что у «Луга»
и «Болота», но заснеженный: морозный день, низкое солнце.

Контракт поля и камеры — как у «Луга»: клетки, зоны, камера боя, корень на высоте поля; меняется окружение.
Раскладка снята с макета (гомография по центрам синих и красных клеток макета -> плоскость поля):
- остров одной террасой под снегом; замёрзшие пруды (лёд вровень со снегом) сзади и у переднего края справа;
  два замёрзших ручья в неглубоких руслах выходят к кромке слева и справа и стекают с неё ледопадами в облака;
  над ледопадами — верёвочные мосты на скальные столбы за краем острова;
- лагерь игрока слева: сторожевая вышка, синее знамя, палатка, факел, ящики, забор в снегу, ели, валуны;
- лагерь гоблинов справа: красное знамя, факел, шатёр с бочками, скальные гряды в снегу, ели;
- сзади — заборы, ели, голое дерево, ледяная глыба, разбитые сани; спереди — малые ёлки, валуны, сухие кусты;
- обрыв — пласты острова: сине-серые скалы со снегом на уступах, с кромки свисают сосульки;
- вокруг — облака и дальние островки в снегу.

Рельеф, россыпь, небо и ледяные плитки поля (Hex_Tile_Snow_*) красятся палитрой биома Vitaria_Palette_Snow
(build_vitaria.PALETTE_VARIANTS["Snow"]); воды нет — лёд прудов, ручьёв и ледопадов на палитре; знамёна,
палатка, огонь, зоны и препятствия — общей палитрой. Холод — светом, туманом и постом
(build_isle.LOOK_VARIANTS["snow"]), а не тёмной палитрой.
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix
from build_vitaria import p_box, p_ico, SW_UV, ramp_uv, clamp
from . import board as B
from . import terrain as T
from . import meadow as Mw
from . import swamp_assets as SA
from . import snow_assets as NA
import build_isle as _BI

NAME = "Arena_Snow"
PREFIX = "AW_"                       # коллекции сцены: AW_Terrain, AW_Props... (W — winter)
TERRAIN_DIR = "Snow"                 # Models/Arena/Snow
PALETTE_VARIANT = "Snow"             # Vitaria_Palette_Snow: рельеф, россыпь, небо, плитки поля
PALETTE_ASSETS = ("Hex_Tile_Snow_",)  # свои ассеты в палитре биома (в ките и в FBX)
WATER_STYLE = None                   # воды нет: лёд — на палитре
BACKDROP_USED = False

CAMERA = Mw.CAMERA
# низкое солнце колонии (направление как у «Луга»: тени деревьев лагерей ложатся назад, на снег), но белее —
# холод светом: снег остаётся голубым, а не серым
SUN = dict(direction=_BI.SUN_DIR, energy=4.8, color=(1.0, 0.97, 0.93))

TILES = {"neutral": ["Hex_Tile_Snow_A", "Hex_Tile_Snow_A", "Hex_Tile_Snow_B", "Hex_Tile_Snow_C"],
         "player": "Hex_Tile_Blue", "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]}


def GAME_LOOK(sun_fwd):
    return _BI.game_look(sun_fwd, "snow")


VIEW_BOX = Mw.VIEW_BOX
board_flat = Mw.board_flat
KEEP_OUT_BOARD = Mw.KEEP_OUT_BOARD

NATURE_EXTRA = ("Snow_",)
RADII = {"Snow_Pine": 0.9, "Snow_Rock_A": 0.6, "Snow_Rock_B": 0.32, "Snow_Crag": 1.6, "Snow_IceBlock": 0.55,
         "Snow_Twigs": 0.3, "Snow_DeadTree": 1.2, "Snow_Drift": 0.6, "Snow_Watchtower": 1.6, "Snow_Pavilion": 1.6,
         "Snow_RopeBridge": 1.9, "Snow_Pillar": 1.2, "Snow_Fence": 0.5, "Snow_Sled": 0.85, "Snow_Torch": 0.3}
ATTACHMENTS = {"Snow_Torch": dict(fx=("FX_Flame_Small", 0.0, 0.0, SA.TORCH_TOP, 0.72),
                                  sockets=[("embers", SA.TORCH_TOP + 0.35, 0.4)])}
SCROLL = {}
PREVIEW_FIGHTERS = [("Preview_Troll", 1, 1, 0), ("Preview_Troll", 0, 3, 0), ("Preview_Goblin", 7, 1, 0),
                    ("Preview_Goblin", 8, 2, 0), ("Preview_Goblin", 7, 3, 0)]

ICE_Z = -0.32                        # лёд ручьёв в руслах

# ---------------------------------------------------------------------------------------
# остров: одна терраса, пласты почти отвесные, снег на уступах (ledge уводит их в снежную часть рампы)
# ---------------------------------------------------------------------------------------
FADE = ("cliff_fade", -20.0, 2.4, 0.05)
DEEP = dict(bottom=-22.0, jag=7.0, strata=(0.035, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.64, 0.77),
            batter=-0.3, amp=0.42, wave=0.6, lean=0.1, crack=0.22, zref=-0.36, nseed=2.3, union=True, ledge=0.24)
GRASS = "grass"                      # снег (рампа grass палитры Snow)
BOULDERS = False

# кромка по макету
CTRL = [
    # перед (слева направо): слева — уступ ледопада лицом к камере, кромка уходит назад к левому краю
    (-14.6, -3.7), (-13.6, -4.15), (-12.7, -4.45), (-12.2, -5.3), (-11.3, -6.0), (-10.2, -6.55), (-8.5, -7.1),
    (-6.4, -7.35), (-3.8, -7.4), (-1.4, -7.45),
    (1.0, -7.5), (3.4, -7.55), (5.8, -7.55), (8.2, -7.35), (10.2, -7.1), (12.1, -6.4), (13.6, -4.8),
    # правый край
    (14.5, -2.6), (14.9, 0.1), (15.1, 3.2), (15.1, 6.4), (14.2, 8.9),
    # зад (справа налево)
    (12.0, 10.0), (9.0, 10.2), (6.0, 10.1), (3.0, 10.2), (0.0, 10.1), (-3.0, 10.0), (-6.0, 10.0), (-9.0, 9.6),
    (-11.8, 8.9), (-13.9, 6.6),
    # левый край
    (-15.4, 3.6), (-15.8, 0.4), (-15.55, -1.4), (-15.1, -2.8),
]
_ISLAND = T.closed_spline(T.ccw(CTRL), 0.45)

# замёрзшие пруды: лёд вровень со снегом (пятно по рельефу), с трещинами
PONDS = [
    dict(seed=1, pts=[(0.4, 6.3), (1.2, 5.75), (2.4, 5.6), (3.5, 5.75), (4.3, 6.3), (4.0, 7.0), (3.0, 7.35),
                      (1.6, 7.4), (0.6, 7.0)]),
    dict(seed=2, pts=[(0.8, -6.6), (1.07, -6.21), (2.07, -6.09), (2.65, -5.63), (3.77, -5.44), (5.05, -5.56),
                      (5.2, -6.05), (4.4, -6.38), (3.27, -6.81), (1.84, -6.96), (0.95, -6.9)]),
]
POND_LINES = [T.wobble(T.closed_spline(T.ccw(p["pts"]), 0.3), 0.1, 1.1, p["seed"]) for p in PONDS]

# замёрзшие ручьи: русло (ломаная от истока к кромке), полуширина; у кромки — ледопад
STREAMS = [
    # слева — из-под моста вперёд, к уступу у переднего левого угла: ледопад лицом к камере, как на макете
    dict(pts=[(-13.35, -0.65), (-13.4, -1.9), (-13.45, -3.05), (-13.4, -4.2)], half=0.6, falls=2.2),
    dict(pts=[(10.6, -3.65), (11.35, -4.5), (12.25, -5.2), (12.95, -5.7)], half=0.55, falls=2.3),
]
STREAM_D = 0.38                       # глубина русла (м)
STREAM_LINES = [T.open_spline(s["pts"], 0.25) for s in STREAMS]

# мосты: (ближний конец на острове, дальний — на скальном столбе за краем); длина пролёта — NA.BRIDGE_L
BRIDGES = [((-12.95, -0.5), (-16.28, -1.86)), ((11.05, -4.05), (14.05, -6.05))]


def _seg_dist(x, y, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
    return math.hypot(ax + dx * t - x, ay + dy * t - y)


def _poly_dist(x, y, pts):
    return min(_seg_dist(x, y, (a.x, a.y), (b.x, b.y)) for a, b in zip(pts, pts[1:]))


def stream_dist(x, y):
    return min(_poly_dist(x, y, L) - s["half"] for L, s in zip(STREAM_LINES, STREAMS))


def stream_sink(x, y):
    """Русло ручья: проседание до STREAM_D, у кромки плато — без проседания (бровка держит обрыв)."""
    d = stream_dist(x, y)
    if d > 0.6:
        return 0.0
    k = T.smoothstep(0.15, 0.55, T.dist_to_outline(x, y, _ISLAND)) if T.point_in_poly(x, y, _ISLAND) else 0.0
    return STREAM_D * T.smoothstep(0.6, -0.25, d) * k


def pond_dist(x, y):
    best = -9.0
    for L in POND_LINES:
        best = max(best, T.signed_dist(x, y, L))
    return best


def stream_points():
    """Вершины по берегам русла: ровный откос без зубцов сетки."""
    pts = []
    for L, s in zip(STREAM_LINES, STREAMS):
        nr = _line_normals(L)
        h = s["half"]
        for d in (0.0, h - 0.25, h, h + 0.3, h + 0.6):
            for sgn in ((1,) if d == 0.0 else (1, -1)):
                for p, n in zip(L, nr):
                    q = p + n * (sgn * d)
                    pts.append((q.x, q.y))
    return pts


def _line_normals(L):
    out = []
    for i in range(len(L)):
        a, b = L[max(0, i - 1)], L[min(len(L) - 1, i + 1)]
        d = (b - a).normalized()
        out.append(Vector((-d.y, d.x, 0.0)))
    return out


def snow_patches(x, y):
    """Снег неровный: голубые ложбинки и светлые надувы пятнами, у русел — тень."""
    n = T.noise.noise(Vector((x * 0.19, y * 0.19, 3.1)))
    m = T.noise.noise(Vector((x * 0.37, y * 0.37, 8.4)))
    d = stream_dist(x, y)
    k = T.smoothstep(1.2, 0.2, d) if d < 1.2 else 0.0
    return -0.14 * T.smoothstep(0.1, 0.45, n) + 0.12 * T.smoothstep(0.2, 0.5, m) - 0.18 * k


def terraces():
    t = T.Terrace("arena", CTRL, z=0.0, bottom=-3.0, seed=21, wob=0.3, flat=board_flat, grid=0.72, hills=0.0,
                  grass=(0.3, 0.86), shelves=(0.42, 0.72), fade=FADE, deep=DEEP, ramp=GRASS, sink=stream_sink,
                  extra_pts=stream_points(), patches=snow_patches)
    return {"arena": t}


OCCLUDERS = {}
FLOORS = {}


# ---------------------------------------------------------------------------------------
# своё у окружения в рельефе: лёд прудов и ручьёв, ледопады, сосульки по кромке
# ---------------------------------------------------------------------------------------
def _flat_poly(V, a, outline, z, col_of):
    """Плоский многоугольник на высоте z (лёд в русле), цвет — col_of(x, y, r) -> swatch."""
    from mathutils import geometry
    bm, uvl = a.bm, a.uv
    pts2 = [Vector((p.x, p.y)) for p in outline]
    res = geometry.delaunay_2d_cdt(pts2, [], [list(range(len(pts2)))], 1, 1e-5)
    vs = [bm.verts.new((v.x, v.y, z)) for v in res[0]]
    n = 0
    for f in res[2]:
        try:
            nf = bm.faces.new([vs[i] for i in f])
        except ValueError:
            continue
        nf.normal_update()
        if nf.normal.z < 0:
            nf.normal_flip()
        c = nf.calc_center_median()
        uv = V.SW_UV[col_of(c.x, c.y)]
        for l in nf.loops:
            l[uvl].uv = uv
        n += 1
    return n


def _ribbon_outline(L, half):
    nr = _line_normals(L)
    left = [p + n * half for p, n in zip(L, nr)]
    right = [p - n * half for p, n in zip(L, nr)]
    return left + list(reversed(right))


def ice_falls(a, P, N, width, rng):
    """Ледопад с кромки, как на макете: сплошная ледяная завеса во всю ширину русла (тёмный лёд), перед ней —
    столбы-сосульки, в середине длиннее, и наплыв льда на бровке."""
    N = Vector((N.x, N.y, 0.0)).normalized()
    side = Vector((-N.y, N.x, 0.0))
    SA.drape(a, P + N * 0.02 + Vector((0, 0, -0.1)), N, 8.5, width * 1.05, "water_dark", rng, t=0.35, bow=0.5,
             taper=0.45)
    k = max(4, int(width / 0.26))
    for i in range(k):
        f = (i + 0.5) / k - 0.5
        top = P + side * (f * width) + N * rng.uniform(0.12, 0.3) + Vector((0, 0, -0.08))
        L = rng.uniform(5.0, 10.0) * (1.0 - 0.9 * abs(f))
        SA.drape(a, top, N, max(1.2, L), rng.uniform(0.4, 0.66), rng.choice(["water", "foam", "water", "water"]),
                 rng, t=0.3, bow=0.6, taper=0.55)
    for i in range(3):                                   # наплыв льда на бровке
        c = P + side * ((i - 1) * width * 0.32) + N * 0.12 + Vector((0, 0, -0.05))
        a.add(NA.p_ico(width * 0.24, 2, loc=c, scl=(1.2, 1.0, 0.45), jitter=0.06, rng=rng, cut=-0.1),
              NA.ICE_FLOW)


def _add_ramp(a, part, t_of, top=None):
    """Влить bmesh в ассет рельефа: грани — рампой FADE по высоте вершин (как пласты обрыва), верх — swatch top."""
    ramp = FADE[0]
    vmap = {v: a.bm.verts.new(v.co) for v in part.verts}
    for f in part.faces:
        try:
            nf = a.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.normal_update()
        if top and nf.normal.z > 0.6:
            for l in nf.loops:
                l[a.uv].uv = SW_UV[top]
        else:
            for l in nf.loops:
                l[a.uv].uv = ramp_uv(ramp, t_of(l.vert.co.z, nf.normal.z))
    part.free()


def rim_columns(t, a, rng):
    """Столбы обрыва, как на макете: тёсаные блоки скалы у кромки выступают из пластов на 0.15–0.6 м, макушки на
    разной высоте под снежным свесом (обрыв ступенями), на каждой — снежная подушка, с кромки — сосульки; низ
    уходит в дымку. Наклон внутрь ~10° — вдоль пластов (DEEP batter). Только там, где обрыв видно из камеры боя."""
    pts, nrm = t.outline, t.normals
    n = len(pts)
    L = [0.0]
    for k in range(n):
        L.append(L[-1] + (pts[(k + 1) % n] - pts[k]).length)
    total = L[-1]
    z0, z1, jamp = FADE[1], FADE[2], FADE[3]
    s, i, made = rng.uniform(0.0, 1.0), 0, 0
    while s < total:
        while L[i + 1] < s:
            i += 1
        f = (s - L[i]) / max(1e-6, L[i + 1] - L[i])
        P = pts[i % n].lerp(pts[(i + 1) % n], f)
        N = nrm[i % n].lerp(nrm[(i + 1) % n], f).normalized()
        w = rng.uniform(1.2, 2.4)
        s += w * rng.uniform(0.85, 1.3)
        if not T.in_view(P.x, P.y, 1.5) or N.y > 0.35:
            continue
        if stream_dist(P.x, P.y) < 2.2 or bridge_dist(P.x, P.y) < 1.7:      # устье ледопада открыто
            continue
        d = rng.uniform(1.0, 1.7)
        h = rng.uniform(8.0, 13.0)
        top = rng.uniform(-3.4, -1.1)                      # ступени ниже снежного свеса
        out = rng.uniform(0.15, 0.6)
        theta = math.atan2(N.y, N.x) - math.pi / 2 + math.radians(rng.uniform(-6.0, 6.0))
        M = Matrix.Translation(Vector((P.x + N.x * out, P.y + N.y * out, top))) @ \
            Matrix.Rotation(theta, 4, "Z") @ Matrix.Rotation(-math.radians(rng.uniform(8.0, 12.0)), 4, "X")
        part = p_box((w, d, h), loc=(0.0, -d / 2, -h / 2), bevel=0.06)
        bmesh.ops.transform(part, matrix=M, verts=part.verts)
        jit = rng.uniform(-1.0, 1.0) * jamp
        _add_ramp(a, part, lambda z, nz, j=jit: clamp((z - z0) / (z1 - z0) - 0.05 + j + 0.1 * nz), top="sod_dark")
        # снежная плита со скруглёнными краями на всю макушку и чуть за край (эллипсоид не закрывал углы бруска:
        # они торчали из-под него тёмными квадратиками в тени)
        cap = p_box((w * 1.05 + 0.1, d * 1.05 + 0.1, 0.24), loc=(0.0, -d / 2 + 0.04, 0.1),
                    rot=(rng.uniform(-2, 2), rng.uniform(-2, 2), 0.0), bevel=0.09)
        bmesh.ops.transform(cap, matrix=M, verts=cap.verts)
        a.add(cap, NA.SNOW)
        side = Vector((-N.y, N.x, 0.0))
        for j in range(rng.randint(1, 3)):              # сосульки с кромки столба
            u = rng.uniform(-0.38, 0.38) * w
            tp = P + N * (out - 0.02) + side * u + Vector((0, 0, top + 0.06))      # верх спрятан в подушке
            SA.drape(a, tp, N, rng.uniform(0.8, 2.4), rng.uniform(0.2, 0.42), "water",
                     rng, t=0.14, bow=0.05, taper=0.96)
        made += 1
    return made


def terrain_extra(V, TR, ts, assets):
    t = ts["arena"]
    rng = random.Random(91)
    g, cl = assets["ground"], assets["cliffs"]
    n_i = 0
    # пруды: лёд по рельефу — светлый край, голубее к середине, трещины
    for k, L in enumerate(POND_LINES):
        TR.decal(g, t, L, "dirt", lambda x, y, r: 0.95 - 0.8 * min(1.0, r / 1.3), lift=0.025, grid=0.45)
        n_i += 1
        cracks(V, TR, g, t, L, random.Random(300 + k))
    # ручьи: лёд в русле плоский, на ICE_Z; берег — снег рельефа
    for L, s in zip(STREAM_LINES, STREAMS):
        out = _ribbon_outline(L, s["half"] + 0.14)
        _flat_poly(V, g, out, ICE_Z, lambda x, y: "water")
        # ледопад там, где русло выходит к кромке
        end = L[-1]
        best, bi = 1e9, 0
        for i, p in enumerate(t.outline):
            d = (p - end).length
            if d < best:
                best, bi = d, i
        ice_falls(cl, t.outline[bi], t.normals[bi], s["falls"], rng)
    n_c = icicles(TR, t, cl)
    n_k = rim_columns(t, cl, random.Random(73))
    print("snow: ice ponds %d, streams %d, icicles %d, rim columns %d" % (n_i, len(STREAMS), n_c, n_k))


def cracks(V, TR, a, t, L, rng):
    """Трещины во льду: ломаные белёсые полосы 7 см от середины к краю, как на макете."""
    xs = [p.x for p in L]
    ys = [p.y for p in L]
    c = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, 0))
    for k in range(3):
        ang = rng.uniform(0, math.tau)
        p = c + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.2, 0.2), 0))
        pts = [p]
        for j in range(4):
            ang += rng.uniform(-0.6, 0.6)
            q = pts[-1] + Vector((math.cos(ang), math.sin(ang), 0)) * rng.uniform(0.3, 0.55)
            if T.signed_dist(q.x, q.y, L) < 0.25:
                break
            pts.append(q)
        for p0, p1 in zip(pts, pts[1:]):
            d = (p1 - p0)
            n = Vector((-d.y, d.x, 0)).normalized() * 0.035
            quad = [p0 + n, p1 + n, p1 - n, p0 - n]
            TR.decal(a, t, quad, None, None, lift=0.035, grid=5.0, swatch="foam")


def icicles(TR, t, a):
    rng = random.Random(57)
    pts, nrm = t.outline, t.normals
    n = len(pts)
    L = [0.0]
    for k in range(n):
        L.append(L[-1] + (pts[(k + 1) % n] - pts[k]).length)
    total = L[-1]
    s, i, made = 0.0, 0, 0
    while s < total:
        while L[i + 1] < s:
            i += 1
        f = (s - L[i]) / max(1e-6, L[i + 1] - L[i])
        P = pts[i % n].lerp(pts[(i + 1) % n], f)
        N = nrm[i % n].lerp(nrm[(i + 1) % n], f).normalized()
        s += rng.uniform(1.2, 2.6)                       # между кучками — голая кромка
        if not TR.in_view(P.x, P.y, 1.0) or N.y > 0.6:
            continue
        if stream_dist(P.x, P.y) < 0.6:                  # у ледопада — свой лёд
            continue
        side = Vector((-N.y, N.x, 0.0))
        for j in range(rng.randint(2, 4)):               # кучка: одна длинная и толстая, короткие рядом
            top = P + side * rng.uniform(-0.5, 0.5) + N * rng.uniform(0.0, 0.05) + Vector((0, 0, -0.1))
            Lh = rng.uniform(1.6, 3.4) if j == 0 else rng.uniform(0.5, 1.5)
            SA.drape(a, top, N, Lh, rng.uniform(0.32, 0.6) if j == 0 else rng.uniform(0.18, 0.36),
                     rng.choice(["foam", "water", "water", "foam"]), rng, t=0.18, bow=0.05, taper=0.96)
            made += 1
    return made


# ---------------------------------------------------------------------------------------
# расстановка
# ---------------------------------------------------------------------------------------
def bridge_dist(x, y):
    return min(_seg_dist(x, y, a, b) for a, b in BRIDGES)


def scatter_ok(tname, x, y, r):
    if pond_dist(x, y) > -(r + 0.2):
        return False
    if stream_dist(x, y) < r + 0.3:
        return False
    if bridge_dist(x, y) < 0.9 + r:
        return False
    return True


def placements():
    P = []

    def add(asset, x, y, rz=0.0, s=1.0, on="arena", z=None, tilt=(0.0, 0.0), zabs=None):
        P.append(dict(asset=asset, x=x, y=y, rz=rz, s=s, on=on, z=z, tilt=tilt, zabs=zabs))

    rng = random.Random(707)
    # --- лагерь игрока (слева): вышка, знамя, палатка, факел, ящики
    add("Snow_Watchtower", -12.75, 3.95, 12)
    add("Prop_Banner_Blue", -11.35, 2.05, -10)
    add("Prop_Tent", -9.35, 6.95, 90)
    add("Snow_Torch", -10.45, 5.5, 0)
    add("Prop_Crate", -8.15, 6.3, 15)
    add("Prop_Crate", -7.75, 6.85, -20)
    add("Prop_Barrel", -13.7, 1.55, 30)
    add("Snow_Crag_A", -11.1, 8.75, 170)
    # --- лагерь гоблинов (справа): знамя, факел, шатёр с бочками, скалы
    add("Prop_Banner_Red", 11.3, 3.85, 10)
    add("Snow_Torch", 12.25, 1.5, 0)
    add("Snow_Pavilion", 12.05, -1.7, 8)
    add("Prop_Barrel", 11.75, -1.4, 0)
    add("Prop_Barrel", 12.35, -2.05, 40)
    add("Prop_Barrel", 11.7, -2.2, 75)
    add("Prop_Crate", 12.65, -1.1, 20)
    add("Snow_Crag_A", 14.45, 5.7, 10)
    add("Snow_Crag_B", 14.05, 0.75, 95)
    add("Snow_Crag_A", 8.45, 9.25, 200)
    # --- мосты на скальные столбы за краем: настил на 0.22 м над снегом у концов
    for (x0, y0), (x1, y1) in BRIDGES:
        ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
        add("Snow_RopeBridge", (x0 + x1) / 2, (y0 + y1) / 2, ang, zabs=0.22)
        d = Vector((x1 - x0, y1 - y0, 0)).normalized()
        add("Snow_Pillar", x1 + d.x * 0.75, y1 + d.y * 0.75, rng.uniform(0, 360), zabs=0.12)
        add("Snow_Pine_C", x1 + d.x * 1.15 - d.y * 0.35, y1 + d.y * 1.15 + d.x * 0.35, rng.uniform(0, 360), 0.75,
            zabs=0.0)
    # --- ели и голое дерево по краям (как на макете)
    for asset, x, y, s in (("Snow_Pine_A", -14.35, 4.55, 1.0), ("Snow_Pine_B", -14.85, 1.7, 0.9),
                           ("Snow_Pine_A", -15.2, 0.25, 0.85), ("Snow_Pine_C", -12.0, 0.45, 0.9),
                           ("Snow_Pine_A", -5.05, 8.1, 1.0), ("Snow_Pine_C", -0.95, 7.85, 1.0),
                           ("Snow_Pine_C", 1.35, 7.95, 0.95), ("Snow_Pine_B", 4.95, 8.15, 0.95),
                           ("Snow_Pine_A", 9.35, 8.3, 0.9), ("Snow_Pine_B", 13.65, 8.05, 0.95),
                           ("Snow_Pine_C", -4.6, -6.8, 0.85), ("Snow_Pine_C", -3.85, -7.0, 0.7),
                           ("Snow_Pine_C", -1.45, -6.85, 0.8), ("Snow_Pine_A", 9.05, -6.15, 0.75),
                           ("Snow_Pine_C", 10.05, -6.4, 0.75)):
        add(asset, x, y, rng.uniform(0, 360), s)
    add("Snow_DeadTree", -7.05, 8.15, 150, 1.0)
    # --- камни, глыбы, кусты, сани, сугробы
    for asset, x, y, s in (("Snow_Rock_A", -10.6, -1.45, 0.9), ("Snow_Rock_B", -11.55, -4.7, 1.1),
                           ("Snow_Rock_A", -2.6, -6.6, 0.9),
                           ("Snow_Rock_B", 0.35, -5.55, 1.1), ("Snow_Rock_B", 1.5, -5.3, 1.0),
                           ("Snow_Rock_B", 2.6, -6.45, 0.75), ("Snow_Rock_B", 7.75, -6.35, 1.1),
                           ("Snow_Rock_A", 14.25, -3.45, 0.9),
                           ("Snow_Rock_B", -14.6, 6.2, 1.0), ("Snow_Rock_B", 6.4, 8.9, 1.1)):
        add(asset, x, y, rng.uniform(0, 360), s)
    add("Snow_IceBlock", 3.85, 5.55, 25)
    add("Snow_IceBlock", -12.2, -2.5, 60, 0.85)            # у правого берега ручья, как на макете
    add("Snow_IceBlock", 12.15, -6.0, 10, 0.75)
    add("Prop_Crate", 6.9, 6.75, 35)
    add("Snow_Sled", -6.05, 6.05, -25)
    for x, y in ((-11.2, 2.95), (-10.95, -0.85), (-0.6, -5.35), (5.2, -7.0), (-6.6, 5.55), (7.3, 5.6),
                 (10.75, -5.75), (-8.9, -5.95), (5.2, 5.4), (11.0, 2.3), (-12.9, 6.6)):
        add("Snow_Twigs", x, y, rng.uniform(0, 360), rng.uniform(0.85, 1.2))
    for x, y, rz, s in ((-9.4, -6.25, 10, 1.0), (-6.85, 7.7, 170, 0.9), (10.4, 6.6, 30, 1.0), (-13.75, 2.55, 80, 0.9),
                        (13.35, -3.4, 120, 0.9), (-2.4, 8.6, 0, 1.1), (-6.4, -6.95, 20, 0.85)):
        add("Snow_Drift", x, y, rz, s)
    # глыбы по кромке: серые скалы в снегу, местами нависают над обрывом — край острова рваный, как на макете
    rim_rocks(add, rng)
    return P


RIM_ROCKS = [   # (x, y, ассет, масштаб): у переднего края и по бокам, не у мостов и ледопадов
    (-12.05, -5.25, "Snow_Rock_B", 1.2), (-10.75, -6.2, "Snow_Rock_A", 1.35), (-7.4, -6.95, "Snow_Rock_A", 1.3),
    (-0.2, -7.1, "Snow_Crag_B", 0.8), (4.7, -7.1, "Snow_Rock_A", 1.3), (6.75, -6.85, "Snow_Rock_A", 1.6),
    (11.1, -6.55, "Snow_Crag_B", 0.85), (14.35, -1.4, "Snow_Rock_A", 1.4), (14.6, 3.2, "Snow_Crag_B", 1.0),
    (-15.25, 2.75, "Snow_Crag_B", 0.9), (-14.7, 5.95, "Snow_Crag_A", 0.85),
    (-9.6, 9.2, "Snow_Crag_B", 1.0), (-2.9, 9.6, "Snow_Rock_A", 1.4), (3.0, 9.65, "Snow_Crag_B", 0.9),
    (11.9, 9.4, "Snow_Crag_B", 0.95),
]


def rim_rocks(add, rng):
    for x, y, asset, s in RIM_ROCKS:
        add(asset, x, y, rng.uniform(0, 360), s)


# заборы в снегу (сегмент 1 м, как Prop_Fence): лагерь игрока, спереди слева, сзади, сзади справа
FENCES = [
    dict(on="arena", pts=[(-14.25, 0.75), (-12.15, 1.75)], asset="Snow_Fence", seg=1.0),
    dict(on="arena", pts=[(-8.05, -5.55), (-5.85, -6.3)], asset="Snow_Fence", seg=1.0),
    dict(on="arena", pts=[(-7.2, 7.05), (-4.6, 7.25), (-2.05, 7.4)], asset="Snow_Fence", seg=1.0),
    dict(on="arena", pts=[(-0.85, 7.3), (0.8, 7.3)], asset="Snow_Fence", seg=1.0),
    dict(on="arena", pts=[(6.95, 7.05), (9.1, 6.6), (11.2, 5.9)], asset="Snow_Fence", seg=1.0),
]

ROADS = []
RIVER = None
WATER = None
FALLS = []
BRIDGE, MILL_AT = Mw.BRIDGE, Mw.MILL_AT

FOREST = [
    ("arena", [("Snow_Pine_C", 1.0), ("Snow_Pine_A", 0.5), ("Snow_Rock_A", 0.7), ("Snow_Drift", 0.6)],
     0.006, (0.75, 1.0), 0.7),
]
GROUND = [
    ("arena", [("Snow_Twigs", 2.0), ("Snow_Rock_B", 1.0), ("Snow_Drift", 0.8), ("Env_Tuft_B", 0.6)],
     0.06, (0.8, 1.2), 0.25),
]
RIM_TUFTS = []                       # сухой травы по кромке нет: она читалась рядом бурых точек

# ---------------------------------------------------------------------------------------
# небо: облака, дальние островки в снегу — в верхних углах кадра боя и в нижних углах
# ---------------------------------------------------------------------------------------
CLOUDS = dict(items=[("Env_CloudPuff_A", 3), ("Env_CloudPuff_B", 2), ("Env_CloudPuff_C", 2)],
              center=(0.0, 1.0),
              layers=[
                  dict(ring=(1.5, 9.0), n=16, z=(-13.0, -8.5), scale=(0.9, 1.5), box=(-42.0, -10.0, -20.0, 12.0)),
                  dict(ring=(1.5, 9.0), n=12, z=(-13.0, -8.5), scale=(0.9, 1.5), box=(10.0, 42.0, -20.0, 12.0)),
                  dict(ring=(-1.5, 7.0), n=28, z=(-20.0, -12.0), scale=(1.4, 2.4), box=(-42.0, 42.0, -20.0, 16.0)),
                  dict(ring=(8.0, 34.0), n=22, z=(-15.0, -8.0), scale=(1.3, 2.4), box=(-52.0, 52.0, -40.0, 2.0)),
                  dict(ring=(3.0, 26.0), n=18, z=(-9.0, -4.0), scale=(1.3, 2.2), box=(-40.0, 40.0, 10.0, 40.0)),
              ])
DISTANT = [
    ((-18.6, 14.6), -2.6, 3.2, 41, 3),
    ((1.5, 31.0), 2.4, 5.2, 42, 5),
    ((18.4, 14.0), -2.2, 3.0, 43, 2),
    ((-17.5, -10.8), -5.0, 1.3, 44, 1),
    ((19.0, -12.2), -6.6, 1.2, 45, 0),
]
# деревья дальних островков: ели в снегу и скалы ((крупный островок), (малый))
DISTANT_TREES = (["Snow_Pine_A", "Snow_Pine_B", "Snow_Crag_B"], ["Snow_Pine_C"])

# ---------------------------------------------------------------------------------------
# look-dev превью: морозный день — тёплое низкое солнце, голубые тени и дымка, снег не выбеливается
# (в игре — LOOK_VARIANTS["snow"] в build_isle.py)
# ---------------------------------------------------------------------------------------
LOOKDEV = dict(
    fog="#a9c4e0",
    ambient=(0.44, 0.54, 0.74),
    view="AgX", look="AgX - Base Contrast", exposure=0.25,
    sun_angle=8.0, sun_energy=4.8, world=1.0,
    shots={
        "hero": dict(fog=(30.0, 70.0, 0.5), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "wide": dict(fog=(30.0, 70.0, 0.5), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "ipad": dict(fog=(32.0, 74.0, 0.5), dof=(0.0, 0.0, 41.0, 60.0, 6.0)),
        "mock": dict(fog=(30.0, 70.0, 0.5), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
    },
    # снег на солнце ярче 1.0 (свет в Blender линейный): порог выше, иначе свечение всего снега мылит кадр;
    # светятся факелы и самые яркие блики
    bloom=dict(threshold=2.2, strength=0.2, size=0.6),
    lift=(1.0, 1.0, 1.04), gamma=(1.0, 1.0, 1.0), gain=(0.98, 1.0, 1.04),
    saturation=1.08, vignette=0.15,
    sky_bounce=False,
    grass_noise=dict(scale=0.22, dark=(0.95, 0.97, 1.0), light=(1.02, 1.02, 1.02)),
    palettes=["Vitaria_Palette", "Vitaria_Palette_Snow"],
)
