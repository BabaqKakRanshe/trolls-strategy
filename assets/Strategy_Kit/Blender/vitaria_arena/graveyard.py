"""
Арена «Кладбище» (Arena_Graveyard) — биом боя по макету «Graveyard»: тот же парящий остров с полем 9x5, что у
«Луга», но вокруг поля — старое кладбище. Ночь, луна, холодный свет; тёплые огни фонарей и факелов.

Контракт поля и камеры — как у «Луга»: клетки, зоны, камера боя, корень на высоте поля; меняется окружение.
Раскладка снята с макета (гомография по центрам синих и красных клеток макета -> плоскость поля, ошибка до 3 см):
- остров одной террасой, тёмная мшистая трава; три пруда в низинах: слева спереди под мостками, сзади справа за
  оградой, у переднего края справа;
- лагерь игрока слева: часовня-склеп с горящим входом и окном, два кривых дерева, синее знамя, факелы, надгробия,
  фонарь на кривом столбе у мостков, жаровня у пруда;
- лагерь гоблинов справа: красное знамя, факел, бочка, кривое дерево у края, надгробия, телега с гробом, столб с
  огнём в нише, свечи;
- кованая ограда со столбами: сзади (за ней кресты, памятник-столп), спереди вдоль кромки, по правому краю;
  у переднего края — надгробия, кресты, кости, свечи, «призрачное» голубое надгробие, саркофаг, фонарь;
- с кромки — деревянные ступени (спереди слева) и лестница (спереди справа);
- обрыв — пласты и тёсаные столбы серой скалы, мох на уступах и бородами с кромки, корни;
- вокруг — облака и дальние островки с сухими деревьями и часовнями.

Рельеф, россыпь, небо и каменные плитки поля (Hex_Tile_Grave_*) красятся палитрой биома
Vitaria_Palette_Graveyard (build_vitaria.PALETTE_VARIANTS["Graveyard"]); вода прудов — Vitaria_Water_Swamp
(стоячая); знамёна, огонь, зоны и препятствия — общей палитрой. Ночь — светом, туманом и постом
(build_isle.LOOK_VARIANTS["graveyard"]), а не тёмной палитрой; свет фонарей, свечей, входа часовни и
голубого надгробия — в слоте Vitaria_FX.
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
from . import graveyard_assets as GA
import build_isle as _BI

NAME = "Arena_Graveyard"
PREFIX = "AG_"                       # коллекции сцены: AG_Terrain, AG_Props...
TERRAIN_DIR = "Graveyard"            # Models/Arena/Graveyard
PALETTE_VARIANT = "Graveyard"        # Vitaria_Palette_Graveyard: рельеф, россыпь, небо, плитки поля
PALETTE_ASSETS = ("Hex_Tile_Grave_",)  # свои ассеты в палитре биома (в ките и в FBX)
WATER_STYLE = "swamp"                # Vitaria_Water_Swamp: стоячая вода прудов
BACKDROP_USED = False

CAMERA = Mw.CAMERA
# луна: высоко слева спереди, холодный белый свет; тени ложатся назад вправо, мимо поля
SUN = dict(direction=(0.42, 0.42, -0.8), energy=4.0, color=(0.92, 0.95, 1.0))

TILES = {"neutral": ["Hex_Tile_Grave_A", "Hex_Tile_Grave_A", "Hex_Tile_Grave_B", "Hex_Tile_Grave_C"],
         "player": "Hex_Tile_Blue", "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]}


def GAME_LOOK(sun_fwd):
    return _BI.game_look(sun_fwd, "graveyard")


VIEW_BOX = Mw.VIEW_BOX
board_flat = Mw.board_flat
KEEP_OUT_BOARD = Mw.KEEP_OUT_BOARD

NATURE_EXTRA = ("Grave_",)
RADII = {"Grave_Chapel": 2.6, "Grave_DeadTree": 1.4, "Grave_Stone": 0.45, "Grave_Cross": 0.35, "Grave_Monument": 0.7,
         "Grave_Sarcophagus": 1.0, "Grave_Mound": 0.7, "Grave_Pillar": 0.4, "Grave_Fence": 0.5, "Grave_Wall": 1.2,
         "Grave_Lantern": 0.25, "Grave_LampPost": 0.35, "Grave_Candles": 0.3, "Grave_Torch": 0.3,
         "Grave_CoffinCart": 1.6, "Grave_Bones": 0.4, "Grave_Pumpkins": 0.8, "Grave_Rock_A": 0.6, "Grave_Rock_B": 0.32,
         "Grave_Ladder": 0.4, "Grave_Stairs": 0.8, "Grave_Pier": 1.1, "Grave_Slabs": 0.3, "Grave_DryBush": 0.45}
ATTACHMENTS = {"Grave_Torch": dict(fx=("FX_Flame_Small", 0.0, 0.0, SA.TORCH_TOP, 0.72),
                                   sockets=[("embers", SA.TORCH_TOP + 0.35, 0.4)])}
SCROLL = {"Arena_Graveyard_Water": 0.01}     # стоячая вода
PREVIEW_FIGHTERS = [("Preview_Troll", 1, 1, 0), ("Preview_Troll", 0, 3, 0), ("Preview_Goblin", 7, 1, 0),
                    ("Preview_Goblin", 8, 2, 0), ("Preview_Goblin", 7, 3, 0)]

WATER_Z = -0.2                       # уровень прудов (трава поля — 0)

# ---------------------------------------------------------------------------------------
# остров: одна терраса, обрыв — пласты и столбы серой скалы, мох на уступах
# ---------------------------------------------------------------------------------------
FADE = ("cliff_fade", -20.0, 2.4, 0.06)
DEEP = dict(bottom=-22.0, jag=7.0, strata=(0.035, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.64, 0.77),
            batter=-0.25, amp=0.42, wave=0.6, lean=0.09, crack=0.22, zref=-0.36, nseed=3.1, union=True, ledge=0.16)
GRASS = "grass"                      # мшистая трава (рампа grass палитры Graveyard)
BOULDERS = False

# кромка по макету; сзади — за оградой, часовней и прудом
CTRL = [
    # перед (слева направо)
    (-14.5, -5.0), (-13.2, -5.45), (-12.0, -5.35), (-10.4, -5.55), (-8.8, -6.25), (-7.2, -6.85), (-5.6, -7.25),
    (-4.0, -7.45), (-2.4, -7.4), (-0.8, -7.35), (0.8, -7.4), (2.3, -7.55), (3.4, -8.1), (4.6, -8.25), (5.8, -8.25),
    (7.0, -7.95), (8.4, -7.55), (10.0, -7.05), (11.0, -6.3), (12.4, -5.55), (13.9, -4.9),
    # правый край
    (14.75, -4.0), (15.25, -2.6), (15.55, -1.2), (15.7, 0.6), (15.6, 3.0), (15.0, 5.3), (13.8, 7.3), (12.0, 8.9),
    # зад (справа налево)
    (10.0, 9.7), (7.5, 10.1), (5.0, 9.9), (2.5, 9.6), (0.0, 9.7), (-2.5, 9.9), (-5.0, 10.4), (-7.5, 11.0),
    (-10.0, 10.9), (-12.0, 9.7),
    # левый край
    (-13.3, 7.8), (-14.3, 5.4), (-15.2, 3.1), (-16.1, 1.4), (-16.5, -0.6), (-16.3, -2.2), (-15.6, -3.9),
]
_ISLAND = T.closed_spline(T.ccw(CTRL), 0.45)

# пруды в низинах (контуры по макету; до клеток не ближе метра, до кромки — 0.6 м бровки)
PONDS = [
    dict(name="left", seed=1, pts=[(-11.45, -2.2), (-11.6, -3.4), (-12.3, -4.35), (-13.5, -4.65), (-14.7, -4.2),
                                   (-15.3, -3.1), (-15.2, -2.0), (-14.3, -1.6), (-12.6, -1.5)]),
    dict(name="back_right", seed=2, pts=[(5.65, 7.2), (6.2, 6.55), (7.2, 6.4), (8.3, 6.65), (8.85, 7.35), (8.45, 8.15),
                                         (7.4, 8.5), (6.3, 8.3)]),
    dict(name="front_right", seed=3, pts=[(3.85, -6.95), (4.3, -6.55), (5.3, -6.5), (6.25, -6.75), (6.4, -7.35),
                                          (5.6, -7.75), (4.5, -7.8), (3.9, -7.45)]),
]
POND_LINES = [T.wobble(T.closed_spline(T.ccw(p["pts"]), 0.3), 0.12, 0.9, p["seed"]) for p in PONDS]
POND_BOX = [(min(p.x for p in L) - 1.5, max(p.x for p in L) + 1.5, min(p.y for p in L) - 1.5, max(p.y for p in L) + 1.5)
            for L in POND_LINES]
POND_D = 0.55                        # глубина низины пруда (м)


def pond_dist(x, y):
    """Знаковая глубина захода в пруды: > 0 — внутри контура воды (м)."""
    best = -9.0
    for L, (x0, x1, y0, y1) in zip(POND_LINES, POND_BOX):
        if x0 <= x <= x1 and y0 <= y <= y1:
            best = max(best, T.signed_dist(x, y, L))
    return best


def pond_sink(x, y):
    """Низина пруда; у кромки плато — не проседает: бровка держит обрыв."""
    d = pond_dist(x, y)
    if d < -0.6:
        return 0.0
    k = T.smoothstep(0.2, 0.6, T.dist_to_outline(x, y, _ISLAND)) if T.point_in_poly(x, y, _ISLAND) else 0.0
    return POND_D * T.smoothstep(-0.6, 0.75, d) * k


def pond_points():
    pts = []
    for L in POND_LINES:
        for off in (-0.62, -0.32, 0.0, 0.3, 0.7):
            ring = T.offset_outline(L, -off) if off else L
            step = 2 if off > 0.5 else 1
            for p in ring[::step]:
                pts.append((p.x, p.y))
    return pts


def grass_patches(x, y):
    """Трава пятнами: темнее к воде и в сырых низинах, светлее на сухих буграх; у поля — вытоптанная земля."""
    d = pond_dist(x, y)
    k = T.smoothstep(-1.4, -0.2, d) if d > -1.4 else 0.0
    n = T.noise.noise(Vector((x * 0.23, y * 0.23, 4.4)))
    m = T.noise.noise(Vector((x * 0.37, y * 0.37, 9.2)))
    o = T.noise.noise(Vector((x * 0.9, y * 0.9, 1.3)))
    far = T.smoothstep(1.5, 6.0, B.dist_to_board(x, y))          # дальше от поля — темнее (поле — светлое пятно)
    return -0.36 * k - 0.3 * T.smoothstep(0.05, 0.45, n) + 0.2 * T.smoothstep(0.15, 0.5, m) - \
        0.1 * T.smoothstep(0.2, 0.6, o) - 0.16 * far


def terraces():
    t = T.Terrace("arena", CTRL, z=0.0, bottom=-3.0, seed=13, wob=0.24, flat=board_flat, grid=0.72, hills=0.0,
                  grass=(0.3, 0.86), shelves=(0.42, 0.72), fade=FADE, deep=DEEP, ramp=GRASS, sink=pond_sink,
                  extra_pts=pond_points(), patches=grass_patches)
    return {"arena": t}


OCCLUDERS = {}
FLOORS = {}

# пятна сырой земли: тропа к часовне, у могил, у телеги (центр, полуоси, поворот, сид)
MUD = [
    ((-8.2, 5.4), (1.1, 0.6), 30, 1), ((-6.4, 6.0), (0.9, 0.5), -10, 2), ((10.6, -3.4), (1.1, 0.6), 15, 3),
    ((-12.6, 1.3), (0.8, 0.5), 40, 4), ((9.3, 4.9), (0.9, 0.5), -25, 5), ((-3.4, -6.1), (0.8, 0.4), 5, 6),
    ((-10.6, -1.3), (0.9, 0.55), 70, 7), ((11.6, 0.4), (0.9, 0.5), -40, 8), ((2.4, 6.5), (1.0, 0.45), 10, 9),
    ((-1.2, -5.6), (0.8, 0.35), -5, 10), ((7.8, -5.4), (0.7, 0.4), 20, 11), ((-13.9, -0.4), (0.7, 0.45), 15, 12),
    ((13.0, 3.6), (0.8, 0.45), 60, 13), ((-4.8, 6.6), (0.8, 0.4), 0, 14),
]


def _blob(c, r, rot, seed, k=15, rag=0.25):
    rng = random.Random(seed)
    ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    pts = []
    for i in range(k):
        t = math.tau * i / k
        f = 1.0 + rng.uniform(-rag, rag)
        x, y = math.cos(t) * r[0] * f, math.sin(t) * r[1] * f
        pts.append((c[0] + x * ca - y * sa, c[1] + x * sa + y * ca))
    return T.closed_spline(pts, 0.3)


# ---------------------------------------------------------------------------------------
# своё у окружения в рельефе: вода прудов, ил по берегам и тропам, мох и корни с кромки, столбы обрыва
# ---------------------------------------------------------------------------------------
def terrain_extra(V, TR, ts, assets):
    t = ts["arena"]
    n_w = 0
    for k, L in enumerate(POND_LINES):
        n_w += TR.water_poly(assets["water"], TR.offset_outline(L, 0.12), WATER_Z, tile=3.0, rot=23.0 * k)
    for k, L in enumerate(POND_LINES):                    # ил по берегам
        outer = [p + n * (0.4 + 0.22 * T.noise.noise(Vector((p.x * 0.9, p.y * 0.9, k * 3.3))))
                 for p, n in zip(L, TR.edge_normals(L))]
        outer = TR.closed_spline([(p.x, p.y) for p in outer[::2]], 0.3)
        TR.decal(assets["ground"], t, outer, "dirt",
                 lambda x, y, r: 0.3 + 0.45 * min(1.0, max(0.0, 0.5 - r) / 0.5), lift=0.028, grid=0.4)
    for c, r, rot, sd in MUD:
        TR.decal(assets["ground"], t, _blob(c, r, rot, sd), "dirt",
                 lambda x, y, rr: 0.15 + 0.45 * min(1.0, rr / 0.5), lift=0.022, grid=0.5)
    n_m = moss_drapes(TR, t, assets["cliffs"])
    n_k = rim_columns(t, assets["cliffs"], random.Random(29))
    n_b = rim_blocks(t, assets["cliffs"], random.Random(41))
    print("graveyard: water %d tris in %d ponds, mud %d, drapes %d, rim columns %d, rim blocks %d"
          % (n_w, len(POND_LINES), len(MUD), n_m, n_k, n_b))


def rim_blocks(t, a, rng):
    """Тёсаные глыбы на самой кромке (макушки столбов скалы вровень с травой, как на макете): рвут ровный свес
    дёрна; бока — камень, макушка — мох кочками. Только там, где кромку видно из камеры боя."""
    pts, nrm = t.outline, t.normals
    n = len(pts)
    L = [0.0]
    for k in range(n):
        L.append(L[-1] + (pts[(k + 1) % n] - pts[k]).length)
    total = L[-1]
    s, i, made = rng.uniform(0.5, 2.0), 0, 0
    rock = lambda f: "moss" if f.normal.z > 0.7 else ("rock" if f.normal.z > -0.3 else "rock_dark")
    while s < total:
        while L[i + 1] < s:
            i += 1
        f = (s - L[i]) / max(1e-6, L[i + 1] - L[i])
        P = pts[i % n].lerp(pts[(i + 1) % n], f)
        N = nrm[i % n].lerp(nrm[(i + 1) % n], f).normalized()
        s += rng.uniform(2.4, 4.2)
        if not T.in_view(P.x, P.y, 1.0) or N.y > 0.35:
            continue
        if ladder_dist(P.x, P.y) < 1.6 or pier_dist(P.x, P.y) < 1.4 or pond_dist(P.x, P.y) > -1.0:
            continue
        w, d, h = rng.uniform(0.9, 1.5), rng.uniform(0.7, 1.05), rng.uniform(0.55, 0.9)
        top = rng.uniform(-0.04, 0.12)
        c = P + N * rng.uniform(0.0, 0.25)
        theta = math.atan2(N.y, N.x) - math.pi / 2 + math.radians(rng.uniform(-9.0, 9.0))
        M = Matrix.Translation(Vector((c.x, c.y, top - h / 2))) @ Matrix.Rotation(theta, 4, "Z") @ \
            Matrix.Rotation(math.radians(rng.uniform(-4, 4)), 4, "X")
        part = p_box((w, d, h), bevel=0.06)
        bmesh.ops.transform(part, matrix=M, verts=part.verts)
        a.add(part, rock)
        for j in range(rng.randint(1, 2)):
            mx, my = rng.uniform(-0.3, 0.3) * w, rng.uniform(-0.25, 0.25) * d
            cap = p_ico(rng.uniform(0.22, 0.34) * w, 1, loc=(mx, my, h / 2), scl=(1.3, 1.0, 0.32), jitter=0.12, rng=rng,
                        cut=-0.02)
            bmesh.ops.transform(cap, matrix=M, verts=cap.verts)
            a.add(cap, by_normal_moss)
        made += 1
    return made


def moss_drapes(TR, t, a):
    """Мох бородами и корни с кромки обрыва — там, где кромку видно из камеры боя."""
    rng = random.Random(55)
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
        s += rng.uniform(0.6, 1.4)
        if not TR.in_view(P.x, P.y, 1.0) or N.y > 0.6:
            continue
        top = P + N * rng.uniform(0.0, 0.04) + Vector((0, 0, -0.1))
        u = rng.random()
        if u < 0.25:                                      # корень
            SA._strand(a, top, rng.uniform(0.9, 2.4), rng.uniform(0.07, 0.1), 0.07, "bark_dark", rng, kink=0.1)
        else:
            Lh = rng.uniform(0.3, 0.8) if rng.random() < 0.65 else rng.uniform(0.9, 1.8)
            SA.drape(a, top, N, Lh, rng.uniform(0.18, 0.4), rng.choice(["moss", "moss_hang", "sod_dark", "sod_dark"]),
                     rng, taper=0.5)
        made += 1
    return made


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


def rim_columns(t, a, rng):
    """Столбы обрыва, как на макете: тёсаные блоки серой скалы у кромки выступают из пластов, макушки на разной
    высоте под мхом (обрыв ступенями), с макушек свисает мох; наклон внутрь — вдоль пластов (DEEP batter)."""
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
        w = rng.uniform(1.1, 2.2)
        s += w * rng.uniform(0.9, 1.4)
        if not T.in_view(P.x, P.y, 1.5) or N.y > 0.35:
            continue
        if ladder_dist(P.x, P.y) < 1.3:
            continue
        d = rng.uniform(1.0, 1.6)
        h = rng.uniform(8.0, 13.0)
        top = rng.uniform(-0.42, -0.2) if rng.random() < 0.3 else rng.uniform(-2.6, -0.8)
        out = rng.uniform(0.12, 0.45)
        theta = math.atan2(N.y, N.x) - math.pi / 2 + math.radians(rng.uniform(-6.0, 6.0))
        M = Matrix.Translation(Vector((P.x + N.x * out, P.y + N.y * out, top))) @ \
            Matrix.Rotation(theta, 4, "Z") @ Matrix.Rotation(-math.radians(rng.uniform(7.0, 11.0)), 4, "X")
        part = p_box((w, d, h), loc=(0.0, -d / 2, -h / 2), bevel=0.06)
        bmesh.ops.transform(part, matrix=M, verts=part.verts)
        jit = rng.uniform(-1.0, 1.0) * jamp
        _add_ramp(a, part, lambda z, nz, j=jit: clamp((z - z0) / (z1 - z0) - 0.05 + j + 0.1 * nz), top="rock")
        for j in range(rng.randint(2, 3)):              # мох кочками на каменной макушке
            mx, my = rng.uniform(-0.35, 0.35) * w, -d / 2 + rng.uniform(-0.3, 0.3) * d
            r = rng.uniform(0.25, 0.45) * min(w, d)
            cap = p_ico(r, 1, loc=(mx, my, 0.0), scl=(1.3, 1.0, 0.32), jitter=0.12, rng=rng, cut=-0.02)
            bmesh.ops.transform(cap, matrix=M, verts=cap.verts)
            a.add(cap, by_normal_moss)
        side = Vector((-N.y, N.x, 0.0))
        for j in range(rng.randint(1, 3)):              # мох с кромки столба
            u = rng.uniform(-0.4, 0.4) * w
            tp = P + N * (out - 0.06) + side * u + Vector((0, 0, top - 0.03))       # верх пряди — внутри столба
            SA.drape(a, tp, N, rng.uniform(0.4, 1.4), rng.uniform(0.14, 0.32), rng.choice(["moss", "moss_hang", "sod_dark"]),
                     rng, taper=0.8)
        made += 1
    return made


def by_normal_moss(f):
    return "moss" if f.normal.z > 0.6 else "sod_dark"


# ---------------------------------------------------------------------------------------
# расстановка
# ---------------------------------------------------------------------------------------
PIER = [(-12.35, -0.55), (-15.65, -2.0)]               # мостки через левый пруд к кромке (секции по 2 м)
LADDERS = [((6.3, -8.2), (0.08, -1.0))]                # лестница с кромки: (точка у кромки, нормаль наружу)
STAIRS = ((-8.75, -6.25), (-0.38, -0.92))              # ступени с кромки


def _seg_dist(x, y, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
    return math.hypot(ax + dx * t - x, ay + dy * t - y)


def pier_dist(x, y):
    return _seg_dist(x, y, PIER[0], PIER[1])


def ladder_dist(x, y):
    pts = [p for p, _ in LADDERS] + [STAIRS[0]]
    return min(math.hypot(x - px, y - py) for px, py in pts)


def scatter_ok(tname, x, y, r):
    if pond_dist(x, y) > -(r + 0.25):
        return False
    if r > 0.33 and B.dist_to_board(x, y) < 1.8 + r:       # надгробия и кусты — не вплотную к полю
        return False
    if pier_dist(x, y) < 0.9 + r:
        return False
    if ladder_dist(x, y) < 1.0 + r:
        return False
    return True


def _face(n):
    """Поворот (град) ассета «лицом к -Y», чтобы лицо смотрело по n."""
    return math.degrees(math.atan2(n[0], -n[1]))


def placements():
    P = []

    def add(asset, x, y, rz=0.0, s=1.0, on="arena", z=None, tilt=(0.0, 0.0), zabs=None):
        P.append(dict(asset=asset, x=x, y=y, rz=rz, s=s, on=on, z=z, tilt=tilt, zabs=zabs))

    rng = random.Random(1313)
    # --- лагерь игрока (слева): часовня, деревья, знамя, факелы, фонарь у мостков, жаровня у пруда
    add("Grave_Chapel", -8.6, 8.95, 20)
    add("Grave_DeadTree_A", -11.45, 7.05, 30, 1.0)
    add("Grave_DeadTree_B", -5.4, 7.55, 200, 0.95)
    add("Grave_Torch", -9.85, 6.3, 0)
    add("Grave_Candles", -9.25, 6.05, 0, 0.9)
    add("Grave_Stone_A", -10.35, 4.3, 10)
    add("Grave_Cross_Wood", -11.2, 5.35, -20)
    add("Prop_Banner_Blue", -12.2, 2.9, -10)
    add("Grave_Torch", -12.85, 2.15, 0)
    add("Grave_DeadTree_A", -15.0, 1.85, 120, 0.95)
    add("Grave_LampPost", -16.0, -1.05, 0)
    add("Grave_Pillar_Cross", -12.95, -0.25, 0)
    add("Grave_Stone_Tall", -15.35, 0.45, -35)
    add("Grave_Stone_B", -11.35, -3.6, -55)
    add("Prop_Brazier", -11.5, -4.85, 0)
    add("Grave_Pumpkins", -6.7, 6.3, 20)
    add("Grave_Mound", -4.6, 6.15, 80)
    add("Grave_Rock_B", -12.6, 4.6, 0, 1.0)
    add("Grave_Stone_C", -12.55, 0.55, 25)
    add("Grave_Cross", -13.75, 1.2, -15)
    add("Grave_Stone_A", -13.05, 6.2, 15)
    add("Grave_DryBush", -11.0, 3.4, 0, 1.1)
    add("Grave_DryBush", -10.9, -2.3, 0, 1.0)
    add("Grave_DryBush", 11.4, -0.6, 0, 1.05)
    add("Grave_DryBush", 10.6, 5.0, 0, 1.0)
    # ещё могилы и огни по краям поля (на макете всё кольцо вокруг поля занято)
    for asset, x, y, rz in (("Grave_Stone_B", -11.15, 0.75, 15), ("Grave_Stone_A", -3.1, 6.45, -6),
                            ("Grave_Cross", 0.65, 6.55, 8), ("Grave_Mound", 3.35, 6.15, 85),
                            ("Grave_Stone_C", -0.9, 6.9, -14), ("Grave_DryBush", -5.4, -5.35, 0),
                            ("Grave_DryBush", 2.1, -5.95, 0), ("Grave_Stone_A", 11.1, 1.6, 30),
                            ("Grave_Cross_Wood", 12.6, 4.6, -20), ("Grave_Stone_C", -12.6, -0.95, 40)):
        add(asset, x, y, rz)
    add("Grave_Torch", -6.75, 5.35, 0)
    add("Grave_Torch", 4.6, 6.05, 0)
    add("Grave_Lantern", -11.75, -0.85, 0)
    # --- мостки через левый пруд
    (x0, y0), (x1, y1) = PIER
    Lp = math.hypot(x1 - x0, y1 - y0)
    npc = max(1, int(round(Lp / GA.PIER_L)))
    ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
    for k in range(npc):
        f = (k + 0.5) / npc
        add("Grave_Pier", x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, ang, zabs=WATER_Z + 0.3)
    # --- ступени и лестница с кромки
    (sx, sy), sn = STAIRS
    add("Grave_Stairs", sx, sy, _face(sn), zabs=0.0)
    for (lx, ly), ln in LADDERS:
        add("Grave_Ladder", lx, ly, _face(ln), zabs=0.02)
    # --- передний край: надгробия, кресты, кости, свечи, «призрачное» надгробие, саркофаг, фонарь
    add("Grave_Cross", -6.3, -5.85, 8)
    add("Grave_Bones", -6.05, -6.75, 40)
    add("Grave_Stone_A", -4.0, -6.95, -8)
    add("Grave_Candles", -3.25, -7.05, 0, 0.85)
    add("Grave_Stone_Tall", -2.75, -6.45, 5)
    add("Grave_DeadTree_C", -1.95, -5.95, 60, 0.95)
    add("Grave_Stone_C", 0.05, -6.85, 12)
    add("Grave_Bones", 1.2, -7.0, 160, 0.9)
    add("Grave_Stone_Glow", 4.35, -5.95, -5)
    add("Grave_Mound", 5.85, -5.75, 95, 0.85)
    add("Grave_Sarcophagus", 7.35, -6.45, 8)
    add("Prop_Barrel", 7.2, -7.55, 30)
    add("Grave_Lantern", 9.65, -6.6, 0)
    # --- лагерь гоблинов (справа): знамя, факел, бочка, дерево, надгробия, телега с гробом, столб с огнём
    add("Prop_Banner_Red", 11.15, 3.7, 10)
    add("Prop_Barrel", 11.95, 3.1, 0)
    add("Grave_Torch", 12.0, 1.45, 0)
    add("Grave_DeadTree_A", 13.95, 0.05, 160, 1.05)
    add("Grave_Stone_B", 12.45, -1.3, 40)
    add("Grave_Stone_Tall", 14.85, -0.85, 70)
    add("Grave_CoffinCart", 10.95, -4.6, 0)
    add("Prop_Barrel", 9.85, -4.5, 20, 0.75)
    add("Grave_Pillar_Lamp", 13.35, -3.55, 60)
    add("Grave_Candles", 12.85, -5.0, 0, 0.9)
    add("Grave_Pumpkins", 12.9, 5.55, 200)
    add("Grave_Rock_A", 13.5, 6.9, 30, 0.9)
    # --- сзади: руина стены со свечами у пруда, сухое дерево, памятник; за оградой — кресты и надгробия
    add("Grave_Wall", 9.55, 5.55, -20)
    add("Grave_Candles", 9.2, 6.25, 0, 0.85)
    add("Grave_DeadTree_B", 10.05, 6.85, 300, 1.0)
    add("Grave_Monument", 7.6, 9.0, 5)
    for asset, x, y, rz in (("Grave_Cross", -6.55, 9.65, 10), ("Grave_Stone_Tall", -3.7, 8.75, -6),
                            ("Grave_Stone_A", -1.0, 8.7, 4), ("Grave_Cross_Wood", 0.5, 8.55, -12),
                            ("Grave_Stone_B", 3.0, 8.45, 8), ("Grave_Cross", 5.2, 9.2, -5), ("Grave_Stone_C", 9.6, 8.7, 20),
                            ("Grave_Cross", 11.2, 8.0, 12), ("Grave_Stone_A", 2.0, 8.9, 15)):
        add(asset, x, y, rz)
    # --- столбы ограды на изломах и концах
    for asset, x, y in (("Grave_Pillar", -4.65, 8.05), ("Grave_Pillar_Lamp", -2.05, 7.95), ("Grave_Pillar_Lamp", 1.75, 7.4),
                        ("Grave_Pillar", 4.3, 7.5), ("Grave_Pillar_Cross", -7.25, -5.9), ("Grave_Pillar", -5.0, -6.25),
                        ("Grave_Pillar", -1.7, -6.75), ("Grave_Pillar", 2.85, -7.2), ("Grave_Pillar", 8.7, -6.85),
                        ("Grave_Pillar", 14.1, -2.75), ("Grave_Pillar", 14.95, 0.85), ("Grave_Pillar_Cross", 14.6, 3.25),
                        ("Grave_Pillar", -13.4, 3.9), ("Grave_Pillar", -14.9, -0.85), ("Grave_Pillar", 11.3, 6.55)):
        add(asset, x, y, rng.uniform(-6, 6))
    # --- камни у кромки
    for asset, x, y, s in (("Grave_Rock_A", -9.6, -5.85, 0.9), ("Grave_Rock_B", -13.6, -4.95, 1.1),
                           ("Grave_Rock_B", 3.2, -7.75, 1.0), ("Grave_Rock_A", 11.6, -5.95, 0.85),
                           ("Grave_Rock_B", 15.0, 2.3, 1.0), ("Grave_Rock_A", -15.6, -3.3, 0.8)):
        add(asset, x, y, rng.uniform(0, 360), s)
    return P


# кованая ограда (сегмент 1 м): сзади, спереди, по правому краю, вокруг могилы у «призрачного» надгробия
FENCES = [
    dict(on="arena", pts=[(-4.65, 8.05), (-2.05, 7.95), (1.75, 7.4), (4.3, 7.5)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(4.9, 8.4), (5.6, 9.45), (9.2, 9.5), (11.3, 8.6)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(-7.25, -5.9), (-5.0, -6.25)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(-1.7, -6.75), (1.0, -6.95), (2.85, -7.2)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(5.15, -5.35), (6.6, -5.45)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(8.0, -6.55), (8.7, -6.85)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(14.1, -2.75), (14.95, 0.85), (14.6, 3.25), (13.7, 5.1)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(-13.4, 3.9), (-14.35, 1.6), (-14.9, -0.85)], asset="Grave_Fence", seg=1.0),
    dict(on="arena", pts=[(9.25, 7.3), (11.3, 6.55)], asset="Grave_Fence", seg=1.0),
]

ROADS = []
RIVER = None
WATER = None
FALLS = []
BRIDGE, MILL_AT = Mw.BRIDGE, Mw.MILL_AT

FOREST = [
    ("arena", [("Grave_DryBush", 2.0), ("Grave_Rock_A", 0.6), ("Grave_Stone_C", 0.6), ("Grave_Stone_A", 0.5),
               ("Grave_Cross_Wood", 0.6), ("Grave_Cross", 0.4)], 0.08, (0.85, 1.05), 0.6),
]
GROUND = [
    ("arena", [("Env_Tuft_B", 3.0), ("Env_Tuft_A", 1.5), ("Grave_Rock_B", 1.0), ("Grave_Slabs", 1.6),
               ("Grave_Bones", 0.15)], 0.3, (0.8, 1.2), 0.26),
]
RIM_TUFTS = [("arena", 0.6)]

# ---------------------------------------------------------------------------------------
# небо: облака под и вокруг острова, дальние островки с сухими деревьями и часовнями
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
    ((-18.6, 14.6), -2.6, 3.2, 51, 3),
    ((1.5, 31.0), 2.4, 5.2, 52, 4),
    ((18.4, 14.0), -2.2, 3.0, 53, 2),
    ((-17.5, -10.8), -5.0, 1.3, 54, 1),
    ((19.0, -12.2), -6.6, 1.2, 55, 0),
]
# на дальних островках — сухие деревья, часовни и памятники ((крупный островок), (малый))
DISTANT_TREES = (["Grave_DeadTree_A", "Grave_Chapel", "Grave_DeadTree_B", "Grave_Monument"], ["Grave_DeadTree_C"])

# ---------------------------------------------------------------------------------------
# look-dev превью: ночь, луна — холодный белый свет сверху слева, синие тени и дымка, тёплые огни фонарей
# (в игре — LOOK_VARIANTS["graveyard"] в build_isle.py)
# ---------------------------------------------------------------------------------------
LOOKDEV = dict(
    fog="#7d8aa3",
    ambient=(0.2, 0.24, 0.34),
    view="AgX", look="AgX - Base Contrast", exposure=0.1,
    sun_angle=6.0, sun_energy=4.0, world=1.0,
    shots={
        "hero": dict(fog=(24.0, 60.0, 0.82), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "wide": dict(fog=(24.0, 60.0, 0.82), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "ipad": dict(fog=(26.0, 64.0, 0.82), dof=(0.0, 0.0, 41.0, 60.0, 6.0)),
        "mock": dict(fog=(24.0, 60.0, 0.82), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
    },
    bloom=dict(threshold=0.6, strength=0.8, size=0.7),
    lift=(1.0, 1.0, 1.03), gamma=(1.0, 1.0, 1.0), gain=(1.0, 1.0, 1.02),
    saturation=1.06, vignette=0.28,
    sky_bounce=False,
    water=dict(tint=(1.0, 1.0, 1.0), falls_tint=(1.0, 1.0, 1.0), shallow="#6f9a8e", shallow_k=0.0,
               swamp_tint=(0.94, 1.0, 1.02)),
    grass_noise=dict(scale=0.22, dark=(0.88, 0.9, 0.9), light=(1.05, 1.04, 0.98)),
    palettes=["Vitaria_Palette", "Vitaria_Palette_Graveyard"],
)
