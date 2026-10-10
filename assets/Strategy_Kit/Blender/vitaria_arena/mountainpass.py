"""
Арена «Горная застава» (Arena_MountainPass) — биом боя по строке промпта (макета нет): тот же парящий остров с
полем 9x5, что у «Луга», но поле — двор заставы в горном перевале. Ясный день высоко в горах: высокое белое
солнце справа спереди, синее небо, прозрачный воздух.

Контракт поля и камеры — как у «Луга»: клетки, зоны, камера боя, корень на высоте поля; меняется окружение.
Раскладка — по кадру камеры боя (верх кадра у заднего края поля — всего 3.6 м над травой, по бокам при 16:9 —
2–5 м за краем поля):
- остров одной террасой: альпийский луг, вокруг поля — кольцо гравийной дороги; у кузни и у лагеря отряда —
  вытоптанный гравий;
- сзади — каменная стена с зубцами и контрфорсами, посередине — ворота с распахнутыми створками и светильниками
  на пилонах, по углам — круглые бастионы; за воротами дорога уходит вверх по склону между скал; за стеной
  земля поднимается к горам, над зубцами видны серые скалы и горные сосны;
- по бокам двора — две сторожевые башни (при 16:9 кадр режет их кровли, при 21:9 они целиком); в задних углах —
  высокие скалы горла перевала;
- лагерь отряда слева: синее знамя, стойка с оружием, жаровня, факел, ящики; лагерь врага справа: красное
  знамя, кузня под навесом (огонь горна, дым из трубы), ящики и бочки, факел;
- спереди — руины стены, валуны, можжевельник, эдельвейсы и горечавка (ниже линии взгляда на нижний ряд
  клеток), у кромки — серые скальные столбы;
- обрыв — серые пласты и столбы скалы, свес дёрна; вокруг — облака и дальние снежные вершины из облаков.

Рельеф, россыпь, небо и плитки поля (Hex_Tile_Mount_*) красятся палитрой биома Vitaria_Palette_MountainPass
(build_vitaria.PALETTE_VARIANTS["MountainPass"]); знамёна, жаровня, огонь, зоны и препятствия — общей палитрой.
Ясный день — светом, небом и постом (build_isle.LOOK_VARIANTS["mountainpass"]); угли горна, светильников и
факелов — в слоте Vitaria_FX.
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix
from build_vitaria import p_box, p_ico, ramp_uv, SW_UV, clamp
from . import board as B
from . import terrain as T
from . import meadow as Mw
from . import swamp_assets as SA
from . import mountainpass_assets as MA
import build_isle as _BI

NAME = "Arena_MountainPass"
PREFIX = "AP_"                       # коллекции сцены: AP_Terrain, AP_Props...
TERRAIN_DIR = "MountainPass"         # Models/Arena/MountainPass
PALETTE_VARIANT = "MountainPass"     # Vitaria_Palette_MountainPass: рельеф, россыпь, небо, плитки поля
PALETTE_ASSETS = ("Hex_Tile_Mount_",)  # свои ассеты в палитре биома (в ките и в FBX)
WATER_STYLE = None                   # воды у заставы нет
BACKDROP_USED = False

CAMERA = Mw.CAMERA
# высокое солнце справа спереди, как у «Луга»: стена и ворота сзади — на свету, тени башен ложатся влево-назад
SUN = dict(direction=(-0.42, 0.5, -0.76), energy=4.4, color=(1.0, 0.96, 0.9))

TILES = {"neutral": ["Hex_Tile_Mount_A", "Hex_Tile_Mount_A", "Hex_Tile_Mount_B", "Hex_Tile_Mount_C"],
         "player": "Hex_Tile_Blue", "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]}


def GAME_LOOK(sun_fwd):
    return _BI.game_look(sun_fwd, "mountainpass")


VIEW_BOX = Mw.VIEW_BOX
board_flat = Mw.board_flat
KEEP_OUT_BOARD = Mw.KEEP_OUT_BOARD

NATURE_EXTRA = ("Mount_",)
# радиусы занятости (м при масштабе 1): длинные префиксы раньше коротких — берётся первый подходящий
RADII = {"Mount_WallPillar": 0.45, "Mount_WallRuin": 1.15, "Mount_Wall": 0.6, "Mount_Gate": 2.5,
         "Mount_Bastion": 1.25, "Mount_Tower": 1.55, "Mount_Forge": 1.9, "Mount_Sconce": 0.2, "Mount_Torch": 0.3,
         "Mount_CrateStack_S": 0.75, "Mount_CrateStack": 1.0, "Mount_Crag_A": 1.9, "Mount_Crag_B": 1.35,
         "Mount_Boulder_A": 1.0, "Mount_Boulder_B": 0.5, "Mount_Scree": 0.55, "Mount_Pine": 0.6,
         "Mount_Juniper": 0.6, "Mount_Flowers": 0.2, "Mount_Peak": 10.0}
_FF, _FS = MA.FORGE_FIRE, MA.FORGE_SMOKE
ATTACHMENTS = {
    "Mount_Torch": dict(fx=("FX_Flame_Small", 0.0, 0.0, MA.TORCH_TOP, 0.72),
                        sockets=[("embers", MA.TORCH_TOP + 0.35, 0.4)]),
    "Mount_Sconce": dict(fx=("FX_Flame_Small", 0.0, 0.0, MA.SCONCE_TOP, 0.55),
                         sockets=[("embers", MA.SCONCE_TOP + 0.3, 0.3)]),
    # горн кузни: огонь над углями, искры над ним, дым из трубы (смещения — в осях кузни, крутятся с ней)
    "Mount_Forge": dict(fx=("FX_Flame_Small", _FF[0], _FF[1], _FF[2], 0.85),
                        sockets=[("embers", _FF[2] + 0.35, 0.5, _FF[0], _FF[1]),
                                 ("smoke", _FS[2] + 0.1, 0.8, _FS[0], _FS[1])]),
}
SCROLL = {}
PREVIEW_FIGHTERS = [("Preview_Troll", 1, 1, 0), ("Preview_Troll", 0, 3, 0), ("Preview_Goblin", 7, 1, 0),
                    ("Preview_Goblin", 8, 2, 0), ("Preview_Goblin", 7, 3, 0)]

# ---------------------------------------------------------------------------------------
# остров: одна терраса, обрыв — серые пласты и столбы скалы
# ---------------------------------------------------------------------------------------
FADE = ("cliff_fade", -20.0, 2.4, 0.06)
DEEP = dict(bottom=-22.0, jag=7.0, strata=(0.035, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.64, 0.77),
            batter=-0.25, amp=0.42, wave=0.6, lean=0.09, crack=0.22, zref=-0.36, nseed=5.1, union=True, ledge=0.16)
GRASS = "grass"
BOULDERS = False
WOB, TSEED = 0.22, 23

CTRL = [
    # перед (слева направо)
    (-15.4, -3.4), (-14.5, -4.6), (-13.3, -5.5), (-12.0, -6.1), (-10.6, -6.55), (-9.0, -6.85), (-7.2, -7.05),
    (-5.2, -7.2), (-3.0, -7.3), (-0.8, -7.35), (1.5, -7.35), (3.7, -7.3), (5.8, -7.2), (7.8, -7.0), (9.6, -6.75),
    (11.2, -6.4), (12.6, -5.8), (13.8, -5.0), (14.8, -3.9), (15.5, -2.4),
    # правый край
    (15.9, -0.4), (16.1, 1.8), (16.0, 4.2), (15.8, 6.7), (15.5, 8.9), (14.6, 10.8),
    # зад (справа налево): задние углы шире — в них скалы горла перевала
    (12.1, 11.95), (8.6, 12.2), (5.6, 12.35), (2.8, 12.45), (0.0, 12.5), (-2.8, 12.45), (-5.6, 12.35), (-8.6, 12.2),
    (-12.1, 11.95),
    # левый край
    (-14.6, 10.8), (-15.5, 8.9), (-15.9, 6.7), (-16.2, 4.2), (-16.4, 1.8), (-16.2, -0.6), (-15.9, -2.0),
]
# тот же контур, что построит терраса (сплайн + шум кромки)
OUTLINE = T.wobble(T.closed_spline(T.ccw(CTRL), 0.42), WOB, 0.33, TSEED)
OUT_N = T.edge_normals(OUTLINE)


def _rim_point(x, y):
    i = min(range(len(OUTLINE)), key=lambda k: (OUTLINE[k].x - x) ** 2 + (OUTLINE[k].y - y) ** 2)
    return OUTLINE[i].copy(), OUT_N[i].copy()


def _front_rim_y(x):
    """y передней кромки острова над точкой x (по тому же контуру, что строит терраса)."""
    pts = [p for p in OUTLINE if p.y < -2.0]
    pts.sort(key=lambda p: p.x)
    for p0, p1 in zip(pts, pts[1:]):
        if p0.x <= x <= p1.x:
            t = (x - p0.x) / max(1e-6, p1.x - p0.x)
            return p0.y + (p1.y - p0.y) * t
    return min(pts, key=lambda p: abs(p.x - x)).y


# ---------------------------------------------------------------------------------------
# двор заставы: стена с воротами, бастионы, башни, кузня
# ---------------------------------------------------------------------------------------
WALL_Y = 6.35                        # ось задней стены: верх кадра боя здесь на 3.66 м, ворота (3.45) — в кадре
GATE_IN = MA.GATE_W / 2 - 0.31       # стена входит в пилон ворот: концевой столб спрятан в кладке
WALL_X = 10.75                       # стена уходит в бастион
BASTIONS = [(-11.3, WALL_Y, 15.0), (11.3, WALL_Y, -20.0)]
# башни у боков двора: при 16:9 кадр режет их кровли снаружи (ближе к полю — уже дорога), при 21:9 — целиком;
# дверь — к полю и камере
TOWERS = [(-12.0, 1.2, 38.0), (12.0, 1.55, -38.0)]
# кузня справа перед башней, открытой стороной — к полю и камере (при 16:9 — у края кадра целиком; передний
# столб навеса — на гравии двора у дороги)
FORGE_AT = (12.2, -1.9, -62.0)
RISE_H = 1.5                         # за стеной земля поднимается к горам


def rise(x, y):
    """Склон за стеной: от стены к заднему краю острова земля поднимается до RISE_H, у самой кромки — снова к
    уровню поля (кромка и пласты обрыва — как у «Луга»)."""
    k = T.smoothstep(WALL_Y + 1.0, WALL_Y + 4.2, y)
    if k <= 0.0:
        return 0.0
    r = T.dist_to_outline(x, y, OUTLINE)
    n = T.noise.noise(Vector((x * 0.25, y * 0.25, 2.7)))
    return RISE_H * k * T.smoothstep(0.5, 3.0, r) * (0.85 + 0.25 * n)


def _sink(x, y):
    return -rise(x, y)


# ---------------------------------------------------------------------------------------
# дороги и гравий (грунт по рельефу)
# ---------------------------------------------------------------------------------------
# кольцо вокруг поля: сзади уже (до стены 0.95 м), по бокам и спереди — как тропа «Леса»
RING_CTRL = [(-10.45, -4.3), (-9.9, -5.25), (-8.6, -5.55), (-4.5, -5.6), (0.0, -5.6), (4.5, -5.6), (8.6, -5.55),
             (9.9, -5.25), (10.45, -4.3), (10.5, 0.0), (10.45, 4.3), (9.9, 5.15), (8.6, 5.4), (4.5, 5.42),
             (0.0, 5.42), (-4.5, 5.42), (-8.6, 5.4), (-9.9, 5.15), (-10.45, 4.3), (-10.5, 0.0)]
RING = T.closed_spline(RING_CTRL, 0.3)
RING_HALF = 0.4
# дорога от ворот вверх по склону между скал (у ворот — брусчатка самих ворот)
ROAD_CTRL = [(0.0, WALL_Y + MA.GATE_D / 2 + 0.05), (0.1, 7.9), (0.55, 9.2), (0.35, 10.5), (-0.35, 11.6)]
ROAD_LINE = T.open_spline(ROAD_CTRL, 0.3)
ROAD_HALF = 0.68
# гравий: (центр, полуоси, поворот, сид)
CLEARINGS = [((12.15, -2.0), (2.15, 1.6), -62, 1), ((-11.75, -1.75), (1.45, 1.2), 80, 2),
             ((-12.4, 4.4), (1.2, 0.85), 20, 3), ((12.45, 4.45), (1.2, 0.85), -20, 4)]


def _seg_dist(L, x, y, closed=False):
    best = 99.0
    n = len(L)
    for i in range(n if closed else n - 1):
        a, b = L[i], L[(i + 1) % n]
        if abs(a.x - x) > 3.0 and abs(b.x - x) > 3.0 and abs(a.y - y) > 3.0:
            continue
        dx, dy = b.x - a.x, b.y - a.y
        t = max(0.0, min(1.0, ((x - a.x) * dx + (y - a.y) * dy) / max(1e-9, dx * dx + dy * dy)))
        best = min(best, math.hypot(a.x + dx * t - x, a.y + dy * t - y))
    return best


def path_dist(x, y):
    """Расстояние до края гравия кольца или дороги за воротами (< 0 — на дороге)."""
    return min(_seg_dist(RING, x, y, True) - RING_HALF, _seg_dist(ROAD_LINE, x, y) - ROAD_HALF)


def grass_patches(x, y):
    """Альпийский луг пятнами: светлые прогалины и тёмные куртины; дальше от поля — чуть темнее."""
    n = T.noise.noise(Vector((x * 0.21, y * 0.21, 7.4)))
    m = T.noise.noise(Vector((x * 0.43, y * 0.43, 3.2)))
    far = T.smoothstep(2.5, 7.0, B.dist_to_board(x, y))
    return -0.2 * T.smoothstep(0.05, 0.45, n) + 0.18 * T.smoothstep(0.15, 0.5, m) - 0.12 * far


def terraces():
    t = T.Terrace("arena", CTRL, z=0.0, bottom=-3.0, seed=TSEED, wob=WOB, flat=board_flat, grid=0.72, hills=0.0,
                  grass=(0.3, 0.86), shelves=(0.42, 0.72), fade=FADE, deep=DEEP, ramp=GRASS, sink=_sink,
                  patches=grass_patches)
    return {"arena": t}


OCCLUDERS = {}
FLOORS = {}


def _blob(c, r, rot, seed, k=15, rag=0.22):
    rng = random.Random(seed)
    ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    pts = []
    for i in range(k):
        t = math.tau * i / k
        f = 1.0 + rng.uniform(-rag, rag)
        x, y = math.cos(t) * r[0] * f, math.sin(t) * r[1] * f
        pts.append((c[0] + x * ca - y * sa, c[1] + x * sa + y * ca))
    return T.closed_spline(pts, 0.3)


def ring_road(a, t, half=RING_HALF, seed=3, lift=0.026):
    """Гравийное кольцо вокруг поля: замкнутая лента профилем дорог кита (светлая середина, тёмные края)."""
    bm, uvl = a.bm, a.uv
    s = RING
    n = len(s)
    cum = [0.0]
    for k in range(1, n):
        cum.append(cum[-1] + (s[k] - s[k - 1]).length)
    rows = []
    for k, c in enumerate(s):
        tan = (s[(k + 1) % n] - s[k - 1]).normalized()
        nr = Vector((-tan.y, tan.x, 0))
        hw = max(0.08, half * (1 + 0.28 * T.noise.noise(Vector((cum[k] * 0.45, seed * 7.3, 0.5)))))
        offs = [-(hw + 0.16), -hw, -hw * 0.5, 0.0, hw * 0.5, hw, hw + 0.16]
        ts = [0.0, 0.3, 0.72, 0.9, 0.72, 0.3, 0.0]
        row = []
        for oi, (o, tt) in enumerate(zip(offs, ts)):
            jit = 0.05 * T.noise.noise(Vector((cum[k] * 1.7, oi * 3.1, seed * 2.0))) if oi != 3 else 0
            p = c + nr * (o + jit)
            z = t.height(p.x, p.y) + (lift - 0.012 if oi in (0, 6) else lift)
            tt = tt + 0.08 * T.noise.noise(Vector((p.x * 1.3, p.y * 1.3, 9.0)))
            row.append((bm.verts.new((p.x, p.y, z)), tt))
        rows.append(row)
    made = 0
    for k in range(n):
        r0, r1 = rows[k], rows[(k + 1) % n]
        for i in range(6):
            q = (r0[i], r0[i + 1], r1[i + 1], r1[i])
            try:
                f = bm.faces.new([x[0] for x in q])
            except ValueError:
                continue
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
            tm = {x[0]: x[1] for x in q}
            for l in f.loops:
                l[uvl].uv = ramp_uv("dirt", tm[l.vert])
            made += 1
    return made


def terrain_extra(V, TR, ts, assets):
    t = ts["arena"]
    g = assets["ground"]
    n_r = ring_road(g, t)
    TR.build_road(g, ROAD_CTRL, t, half=ROAD_HALF, taper=(0.0, 1.4), seed=5, lift=0.03)
    for c, r, rot, sd in CLEARINGS:
        TR.decal(g, t, _blob(c, r, rot, sd), "dirt",
                 lambda x, y, rr: 0.4 + 0.35 * min(1.0, rr / 0.6), lift=0.02, grid=0.5)
    n_d = grass_drapes(t, assets["cliffs"])
    n_k = rim_columns(t, assets["cliffs"], random.Random(29))
    n_b = rim_blocks(t, assets["cliffs"], random.Random(41))
    n_s = rim_stones(t, assets["cliffs"], random.Random(43))
    print("mountainpass: ring %d faces, road, gravel %d, drapes %d, rim columns %d, rim blocks %d, rim stones %d" % (
        n_r, len(CLEARINGS), n_d, n_k, n_b, n_s))


# ---------------------------------------------------------------------------------------
# кромка: свес дёрна, серые столбы скалы, глыбы
# ---------------------------------------------------------------------------------------
def _walk(t, rng, step_fn, start=(0.0, 1.0)):
    pts, nrm = t.outline, t.normals
    n = len(pts)
    L = [0.0]
    for k in range(n):
        L.append(L[-1] + (pts[(k + 1) % n] - pts[k]).length)
    total = L[-1]
    s, i = rng.uniform(*start), 0
    while s < total:
        while L[i + 1] < s:
            i += 1
        f = (s - L[i]) / max(1e-6, L[i + 1] - L[i])
        P = pts[i % n].lerp(pts[(i + 1) % n], f)
        N = nrm[i % n].lerp(nrm[(i + 1) % n], f).normalized()
        s += step_fn(P, N)


def grass_drapes(t, a):
    """Короткие космы дёрна с кромки (горный луг суше леса: реже и короче моховых бород «Леса»)."""
    rng = random.Random(57)
    made = [0]

    def step(P, N):
        if not T.in_view(P.x, P.y, 1.0) or N.y > 0.6:
            return rng.uniform(0.7, 1.6)
        top = P + N * rng.uniform(0.0, 0.04) + Vector((0, 0, -0.1))
        Lh = rng.uniform(0.22, 0.55) if rng.random() < 0.75 else rng.uniform(0.6, 1.0)
        SA.drape(a, top, N, Lh, rng.uniform(0.16, 0.34), rng.choice(["sod", "sod_dark", "moss", "sod"]), rng,
                 taper=0.55)
        made[0] += 1
        return rng.uniform(0.7, 1.6)
    _walk(t, rng, step)
    return made[0]


def _add_ramp(a, part, t_of, top=None):
    """Влить bmesh в рельеф: грани — рампой FADE по высоте вершин (как пласты обрыва), верх — swatch top."""
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


def _column(a, rng, P, N, w, d, h, top, out, tilt):
    """Тёсаный столб серой скалы у кромки: брусок наклонён внутрь вдоль пластов, грани — рампой обрыва по высоте,
    макушка — светлый камень с кочкой травы; с кромки макушки иногда свисает дёрн."""
    z0, z1, jamp = FADE[1], FADE[2], FADE[3]
    theta = math.atan2(N.y, N.x) - math.pi / 2 + math.radians(rng.uniform(-6.0, 6.0))
    M = Matrix.Translation(Vector((P.x + N.x * out, P.y + N.y * out, top))) @ \
        Matrix.Rotation(theta, 4, "Z") @ Matrix.Rotation(-math.radians(tilt), 4, "X")
    part = p_box((w, d, h), loc=(0.0, -d / 2, -h / 2), bevel=0.06)
    bmesh.ops.transform(part, matrix=M, verts=part.verts)
    jit = rng.uniform(-1.0, 1.0) * jamp
    _add_ramp(a, part, lambda z, nz, j=jit: clamp((z - z0) / (z1 - z0) - 0.05 + j + 0.1 * nz), top="rock")
    if rng.random() < 0.6:                              # кочка травы на макушке
        mx, my = rng.uniform(-0.3, 0.3) * w, -d / 2 + rng.uniform(-0.25, 0.25) * d
        r = rng.uniform(0.22, 0.38) * min(w, d)
        cap = p_ico(r, 1, loc=(mx, my, 0.0), scl=(1.3, 1.0, 0.3), jitter=0.12, rng=rng, cut=-0.02)
        bmesh.ops.transform(cap, matrix=M, verts=cap.verts)
        a.add(cap, lambda f: "sod" if f.normal.z > 0.6 else "sod_dark")
    if rng.random() < 0.5:
        side = Vector((-N.y, N.x, 0.0))
        tp = P + N * (out - 0.06) + side * (rng.uniform(-0.3, 0.3) * w) + Vector((0, 0, top - 0.03))
        SA.drape(a, tp, N, rng.uniform(0.25, 0.7), rng.uniform(0.12, 0.26), rng.choice(["sod", "sod_dark"]), rng,
                 taper=0.8)


def rim_columns(t, a, rng):
    """Столбы обрыва: тёсаные блоки серой скалы у кромки выступают из пластов, макушки на разной высоте (обрыв
    ступенями)."""
    made = [0]

    def step(P, N):
        w = rng.uniform(1.1, 2.3)
        if not T.in_view(P.x, P.y, 1.5) or N.y > 0.35:
            return w * rng.uniform(0.9, 1.4)
        top = rng.uniform(-0.42, -0.2) if rng.random() < 0.3 else rng.uniform(-2.6, -0.8)
        _column(a, rng, P, N, w, rng.uniform(1.0, 1.6), rng.uniform(8.0, 13.0), top, rng.uniform(0.12, 0.45),
                rng.uniform(7.0, 11.0))
        made[0] += 1
        return w * rng.uniform(0.9, 1.4)
    _walk(t, rng, step)
    return made[0]


def rim_blocks(t, a, rng):
    """Глыбы на самой кромке: гранёные камни вросли в дёрн и рвут ровный свес."""
    made = [0]
    rock = lambda f: "rock" if f.normal.z > 0.55 else ("rock_dark" if f.normal.z > -0.3 else "stone_dark")

    def step(P, N):
        if not T.in_view(P.x, P.y, 1.0) or N.y > 0.35:
            return rng.uniform(3.0, 5.4)
        r = rng.uniform(0.42, 0.7)
        c = P + N * rng.uniform(-0.12, 0.12)
        a.add(p_ico(r, 1, loc=(c.x, c.y, rng.uniform(-0.14, -0.02)), scl=(1.35, 1.0, 0.62),
                    rot=(0, 0, rng.uniform(0, 180)), jitter=0.18, rng=rng), rock)
        made[0] += 1
        return rng.uniform(3.0, 5.4)
    _walk(t, rng, step, (0.5, 2.0))
    return made[0]


# скальные «пальцы» у передней кромки — шпили из пластов обрыва, макушки выше травы (x, радиус, верх)
RIM_STONES = [(-8.3, 0.62, 0.62), (-0.9, 0.5, 0.42), (6.7, 0.7, 0.75), (13.1, 0.66, 0.85)]


def rim_stones(t, a, rng):
    made = 0
    z0, z1 = FADE[1], FADE[2]
    for x, r, top in RIM_STONES:
        P, N = _rim_point(x, _front_rim_y(x))
        side = Vector((-N.y, N.x, 0.0))
        for k, (dx, kr, kt) in enumerate(((0.0, 1.0, 1.0), (rng.choice((-1, 1)) * r * 1.3, 0.7, 0.55))):
            c = P - N * (r * 0.25) + side * dx
            h = rng.uniform(2.6, 3.4)
            part = MA.spire(rng, c.x, c.y, r * kr, h, tilt=4.0, z=top * kt - h)
            jit = rng.uniform(-1.0, 1.0) * FADE[3]
            _add_ramp(a, part, lambda z, nz, j=jit: clamp((z - z0) / (z1 - z0) - 0.05 + j + 0.1 * nz), top="rock")
            made += 1
    return made


# ---------------------------------------------------------------------------------------
# расстановка
# ---------------------------------------------------------------------------------------
# Высота у кромки перед полем: верх не выше линии взгляда камеры боя на нижние вершины клеток переднего ряда
_CAM_Y = CAMERA["target"][1] - CAMERA["distance"] * math.cos(math.radians(CAMERA["pitch"]))
_CAM_Z = CAMERA["distance"] * math.sin(math.radians(CAMERA["pitch"]))


def front_height(y, margin=0.3):
    y0 = -4.62
    if y >= y0:
        return 0.0
    return _CAM_Z * (y - y0) / (_CAM_Y - y0) - margin


def in_yard(x, y):
    """Двор заставы: внутри кольца дороги и до стены — тут только трава, цветы и мелкие камни."""
    return abs(x) < 11.0 and -5.0 < y < WALL_Y + 0.6


def scatter_ok(tname, x, y, r):
    if path_dist(x, y) < r * 0.6 + 0.25:
        return False
    db = B.dist_to_board(x, y)
    if r > 0.33 and (db < 1.6 + r or in_yard(x, y)):    # кусты и камни — не во дворе и не вплотную к полю
        return False
    if abs(y - WALL_Y) < 0.9 + r and abs(x) < 12.4:     # стена и бастионы
        return False
    if r >= 0.9:                                          # деревья: не перед полем и не во дворе
        if db < 2.0 + r * 0.5 or (y < -4.3 and abs(x) < 12.5):
            return False
    elif r >= 0.5 and y < -4.3 and abs(x) < 11.5:        # перед полем крупное — только руками (placements)
        return False
    for (cx, cy), (ra, rb), _, _ in CLEARINGS:
        if r > 0.25 and (x - cx) ** 2 / (ra + r) ** 2 + (y - cy) ** 2 / (rb + r) ** 2 < 1.0:
            return False
    return True


PINE_H = {"Mount_Pine_A": 5.0, "Mount_Pine_B": 3.2, "Mount_Pine_C": 4.2}

# перед полем, между дорогой и кромкой: (ассет, x, отступ от кромки внутрь, масштаб) — ниже линии взгляда
FRONT = [
    ("Mount_Juniper", -7.9, 0.5, 0.9), ("Mount_Flowers_W", -8.9, 0.75, 1.0), ("Mount_Boulder_B", -6.9, 0.6, 1.0),
    ("Mount_Flowers_B", -6.2, 0.55, 1.0), ("Mount_Scree", -5.2, 0.6, 1.0), ("Mount_Juniper", -3.7, 0.5, 1.0),
    ("Mount_Flowers_W", -2.6, 0.7, 1.1), ("Mount_Boulder_A", 0.9, 0.85, 0.62), ("Mount_Flowers_B", 2.1, 0.55, 1.0),
    ("Mount_Flowers_W", 3.3, 0.75, 1.0), ("Mount_Juniper", 4.5, 0.55, 0.85), ("Mount_Scree", 8.6, 0.6, 1.0),
    ("Mount_Flowers_W", 9.5, 0.5, 1.0), ("Mount_Boulder_B", 7.6, 0.65, 0.9),
]


def placements():
    P = []

    def add(asset, x, y, rz=0.0, s=1.0, on="arena", z=None, tilt=(0.0, 0.0), zabs=None):
        P.append(dict(asset=asset, x=x, y=y, rz=rz, s=s, on=on, z=z, tilt=tilt, zabs=zabs))

    rng = random.Random(2031)
    # --- ворота, светильники на пилонах (лицом к полю), бастионы, башни
    add("Mount_Gate", 0.0, WALL_Y, 0)
    for sx in (-1, 1):
        add("Mount_Sconce", sx * (MA.GATE_W / 2 - 0.55), WALL_Y - MA.GATE_D / 2 - 0.37, 0, z=1.85)
    for x, y, rz in BASTIONS:
        add("Mount_Bastion", x, y, rz)
    for x, y, rz in TOWERS:
        add("Mount_Tower", x, y, rz)
    # --- лагерь отряда (слева): знамя, стойка с оружием, жаровня, факел, ящики
    add("Prop_Banner_Blue", -11.3, 4.05, 0)
    add("Prop_WeaponRack", -11.75, -1.45, 50)
    add("Prop_Brazier", -11.35, -3.05, 0)
    add("Mount_Torch", -11.0, 2.75, 0)
    add("Mount_CrateStack", -12.5, 4.35, 70)
    add("Mount_CrateStack_S", -12.85, -2.6, 100)
    # --- лагерь врага (справа): кузня под навесом, знамя, ящики и бочки, факел
    fx_, fy_, frz = FORGE_AT
    add("Mount_Forge", fx_, fy_, frz)
    add("Prop_Banner_Red", 11.3, 3.95, 0)
    add("Mount_Torch", 11.0, 2.95, 0)
    add("Mount_CrateStack", 12.75, 4.6, -110)
    add("Mount_CrateStack_S", 12.9, -4.15, 60)
    add("Prop_Barrel", 11.35, -4.75, 0)
    add("Prop_Barrel", 11.9, -5.2, 35, 0.95)
    # --- за стеной: скалы и сосны вдоль дороги вверх по склону (над зубцами видны макушки)
    add("Mount_Crag_A", -4.1, 9.1, 20, 1.15)
    add("Mount_Crag_A", 4.4, 9.0, 200, 1.2)
    add("Mount_Crag_B", -8.4, 8.6, 60, 1.3)
    add("Mount_Crag_B", 8.3, 8.7, 140, 1.25)
    add("Mount_Pine_A", -6.3, 8.0, 0, 1.0)
    add("Mount_Pine_C", 6.55, 7.9, 0, 1.0)
    add("Mount_Boulder_A", -2.25, 7.85, 30, 0.7)
    add("Mount_Boulder_B", 2.2, 7.7, 0, 1.0)
    # --- горло перевала: высокие скалы в задних углах (кадр режет их верх)
    add("Mount_Crag_A", -13.35, 8.75, 35, 1.35)
    add("Mount_Crag_A", 13.45, 8.85, 150, 1.4)
    add("Mount_Crag_B", -14.3, 5.5, 0, 1.1)
    add("Mount_Crag_B", 14.35, 5.95, 90, 1.05)
    # --- спереди и по бокам: руины стены, кромка — по линии взгляда
    add("Mount_WallRuin", -11.7, -5.15, 62)
    add("Mount_WallRuin", 14.55, 0.0, -80)
    for asset, x, d, s in FRONT:
        y = _front_rim_y(x) + d
        if asset in PINE_H and abs(x) < 11.0:
            s = min(s, front_height(y) / PINE_H[asset])
        add(asset, x, y, rng.uniform(0, 360), s)
    # --- пояса вокруг двора: сосны, скалы, можжевельник, валуны
    taken = [(p["x"], p["y"], _rad(p["asset"], p["s"])) for p in P]
    for f in FENCES:
        for (x0, y0), (x1, y1) in zip(f["pts"], f["pts"][1:]):
            n = max(1, int(math.hypot(x1 - x0, y1 - y0) / 0.6))
            taken += [(x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n, 0.5) for k in range(n + 1)]

    def ok_spot(x, y, r):
        if not T.point_in_poly(x, y, OUTLINE) or T.dist_to_outline(x, y, OUTLINE) < 0.45 + r * 0.45:
            return False
        if path_dist(x, y) < r * 0.5 + 0.25 or B.dist_to_board(x, y) < 1.4 + r or in_yard(x, y):
            return False
        if abs(y - WALL_Y) < 0.75 + r * 0.6 and abs(x) < 12.4:
            return False
        for (cx, cy), (ra, rb), _, _ in CLEARINGS:
            if (x - cx) ** 2 / (ra + r * 0.5) ** 2 + (y - cy) ** 2 / (rb + r * 0.5) ** 2 < 1.0:
                return False
        return all((ox - x) ** 2 + (oy - y) ** 2 >= (orr + r) ** 2 * 0.72 for ox, oy, orr in taken)

    def belt(box, n, kinds, srange, tries=400):
        x0, x1, y0, y1 = box
        names, wts = [k for k, _ in kinds], [w for _, w in kinds]
        got = 0
        for _ in range(tries):
            if got >= n:
                break
            x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
            asset = rng.choices(names, wts)[0]
            sc = rng.uniform(*srange)
            r = _rad(asset, sc)
            if not ok_spot(x, y, r):
                continue
            add(asset, x, y, rng.uniform(0, 360), sc)
            taken.append((x, y, r))
            got += 1

    TALL = [("Mount_Pine_A", 3.0), ("Mount_Pine_C", 2.0), ("Mount_Pine_B", 1.5), ("Mount_Crag_B", 0.45)]
    UNDER = [("Mount_Juniper", 2.0), ("Mount_Boulder_A", 0.8), ("Mount_Boulder_B", 1.5), ("Mount_Scree", 1.0),
             ("Mount_Pine_B", 0.8)]
    belt((-11.0, 11.0, 7.3, 9.8), 8, TALL, (0.85, 1.15))               # за стеной у дороги
    belt((-11.0, 11.0, 7.1, 8.4), 8, UNDER, (0.85, 1.2))
    belt((-13.0, 13.0, 9.6, 12.0), 14, TALL, (0.9, 1.2))               # гребень за стеной
    belt((-16.3, -13.4, -3.2, 9.0), 10, TALL, (0.85, 1.15))            # слева, за башней
    belt((13.4, 16.2, -2.4, 9.0), 9, TALL, (0.85, 1.15))               # справа
    belt((-16.2, -12.6, -5.0, 8.0), 9, UNDER, (0.8, 1.1))
    belt((12.6, 16.0, -5.2, 8.0), 8, UNDER, (0.8, 1.1))
    return P


def _rad(asset, s):
    for k, v in RADII.items():
        if asset.startswith(k):
            return v * s
    return 0.5 * s


# задняя стена: сегменты по 2 м (контрфорс в начале сегмента), лицом к полю; концы спрятаны в пилоне ворот
# и в бастионе. Разворот и наклон сегментов — пятая часть жердевой ограды: кладка ровнее
_W = dict(on="arena", asset="Mount_Wall", seg=MA.WALL_SEG, end="Mount_WallPillar", jitter=0.2, align_end=True)
FENCES = [
    dict(_W, pts=[(-WALL_X, WALL_Y), (-GATE_IN, WALL_Y)]),
    dict(_W, pts=[(GATE_IN, WALL_Y), (WALL_X, WALL_Y)]),
]

ROADS = []
RIVER = None
WATER = None
FALLS = []
BRIDGE, MILL_AT = Mw.BRIDGE, Mw.MILL_AT

FOREST = [
    # горные сосны и скалы за двором (радиус 1.0: scatter_ok держит их от поля, двора и стены)
    ("arena", [("Mount_Pine_A", 2.4), ("Mount_Pine_C", 1.6), ("Mount_Pine_B", 1.6), ("Mount_Crag_B", 0.35),
               ("Mount_Boulder_A", 0.5)], 0.2, (0.85, 1.15), 1.0),
    # можжевельник, валуны, осыпь
    ("arena", [("Mount_Juniper", 1.6), ("Mount_Boulder_B", 1.2), ("Mount_Scree", 1.0), ("Mount_Pine_B", 0.6)],
     0.1, (0.8, 1.15), 0.6),
]
GROUND = [
    ("arena", [("Env_Tuft_B", 3.0), ("Env_Tuft_A", 2.0), ("Mount_Flowers_W", 0.9), ("Mount_Flowers_B", 0.7),
               ("Env_Flowers_A", 0.6), ("Mount_Scree", 0.25)], 0.42, (0.8, 1.2), 0.26),
]
RIM_TUFTS = [("arena", 0.65)]

# ---------------------------------------------------------------------------------------
# небо: облака под и вокруг острова, дальние снежные вершины из облаков
# ---------------------------------------------------------------------------------------
CLOUDS = dict(items=[("Env_CloudPuff_A", 3), ("Env_CloudPuff_B", 2), ("Env_CloudPuff_C", 2)],
              center=(0.0, 1.0),
              layers=[
                  dict(ring=(1.5, 9.0), n=10, z=(-15.0, -10.5), scale=(0.9, 1.5), box=(-42.0, -10.0, -20.0, 12.0)),
                  dict(ring=(1.5, 9.0), n=8, z=(-15.0, -10.5), scale=(0.9, 1.5), box=(10.0, 42.0, -20.0, 12.0)),
                  dict(ring=(-1.5, 7.0), n=28, z=(-20.0, -12.0), scale=(1.4, 2.4), box=(-42.0, 42.0, -20.0, 16.0)),
                  dict(ring=(8.0, 34.0), n=22, z=(-15.0, -8.0), scale=(1.3, 2.4), box=(-52.0, 52.0, -40.0, 2.0)),
                  dict(ring=(3.0, 26.0), n=18, z=(-9.0, -4.0), scale=(1.3, 2.2), box=(-40.0, 40.0, 10.0, 40.0)),
              ])
DISTANT = []
# вершины из облаков (build_arena: SKY_PROPS): (ассет, x, y, z подошвы, поворот, масштаб). Подошвы в облаках и
# дымке; макушки — в углах кадра (при 16:9 — спереди у нижних углов и сзади у верхних, при 21:9 — и по бокам)
SKY_PROPS = [
    ("Mount_Peak_A", -24.0, 21.0, -32.0, 60.0, 1.4),          # за задними углами: склоны уходят за верх кадра
    ("Mount_Peak_B", 25.0, 22.0, -34.0, 300.0, 1.35),
    ("Mount_Peak_C", -23.0, 5.0, -21.0, 40.0, 1.05),          # по бокам (21:9)
    ("Mount_Peak_A", 23.5, 9.5, -26.0, 120.0, 1.0),
    ("Mount_Peak_B", -20.5, -8.0, -28.0, 15.0, 0.9),          # у передних углов
    ("Mount_Peak_C", 21.0, -7.0, -21.0, 200.0, 1.0),
]

# ---------------------------------------------------------------------------------------
# look-dev превью: ясный день высоко в горах — высокое белое солнце, синее небо, прозрачный воздух
# (в игре — LOOK_VARIANTS["mountainpass"] в build_isle.py)
# ---------------------------------------------------------------------------------------
LOOKDEV = dict(
    fog="#9fc4e8",
    ambient=(0.36, 0.46, 0.64),
    view="AgX", look="AgX - Base Contrast", exposure=0.22,
    sun_angle=3.0, sun_energy=4.4, world=1.0,
    shots={
        "hero": dict(fog=(30.0, 70.0, 0.55), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "wide": dict(fog=(30.0, 70.0, 0.55), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "ipad": dict(fog=(32.0, 74.0, 0.55), dof=(0.0, 0.0, 41.0, 60.0, 6.0)),
        "mock": dict(fog=(30.0, 70.0, 0.55), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
    },
    bloom=dict(threshold=1.6, strength=0.25, size=0.6),
    lift=(1.0, 1.0, 1.03), gamma=(1.0, 1.0, 1.0), gain=(1.0, 1.0, 1.02),
    saturation=1.12, vignette=0.12,
    sky_bounce=False,
    grass_noise=dict(scale=0.22, dark=(0.9, 0.92, 0.9), light=(1.06, 1.05, 0.96)),
    palettes=["Vitaria_Palette", "Vitaria_Palette_MountainPass"],
)
