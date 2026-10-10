"""
Арена «Лес» (Arena_Forest) — биом боя по макету «Forest»: тот же парящий остров с полем 9x5, что у «Луга», но
вокруг поля — лесная чаща. Прохладный свет под пологом леса: мягкое солнце слева, сине-зелёная дымка; тёплые
огни костра и факелов.

Контракт поля и камеры — как у «Луга»: клетки, зоны, камера боя, корень на высоте поля; меняется окружение.
Раскладка снята с макета (гомография по центрам синих и красных клеток макета -> плоскость поля, ошибка до 6 см):
- остров одной террасой, сочная трава, вокруг поля — кольцо светлой тропы; у лагерей — вытоптанные поляны;
- ручей слева: начинается у камней в глубине, течёт перекатами к передней кромке и срывается с неё водопадом
  в облака (как на макете, но на метр ближе к середине кадра: в 16:9 водопад не уходит за край);
  через ручей — дощатый мостик, вдоль берега к водопаду — жердевая ограда;
- лагерь игрока слева: охотничий шалаш, костёр, рама со шкурой, бревно-скамья, старый дуб, валуны во мху,
  синее знамя, факел, ведро;
- лагерь гоблинов справа: красное знамя, факел, большой валун, торговый навес с бочками и вёдрами;
- сзади — жердевая ограда, штабель брёвен с колодой, валуны; спереди — ограда, малые ёлки у кромки (не выше
  линии взгляда на нижний ряд клеток), грибы, штабель брёвен у края, ромашки;
- по краю — высокие ели, сосны и лиственные деревья, кусты, папоротник;
- обрыв — пласты и столбы серой скалы, мох на уступах, бороды мха и лианы с кромки;
- вокруг — облака и дальние лесистые островки с водопадами.

Рельеф, россыпь, небо и моховые плитки поля (Hex_Tile_Forest_*) красятся палитрой биома
Vitaria_Palette_Forest (build_vitaria.PALETTE_VARIANTS["Forest"]); вода ручья — Vitaria_Water (течёт, как река
«Луга»), водопады — Vitaria_Waterfall; знамёна, костёр, огонь, зоны и препятствия — общей палитрой. Прохлада
сумерек в чаще — светом, туманом и постом (build_isle.LOOK_VARIANTS["forest"]), а не тёмной палитрой; угли
костра и факелов — в слоте Vitaria_FX.
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
from . import forest_assets as FA
import build_isle as _BI

NAME = "Arena_Forest"
PREFIX = "AF_"                       # коллекции сцены: AF_Terrain, AF_Props...
TERRAIN_DIR = "Forest"               # Models/Arena/Forest
PALETTE_VARIANT = "Forest"           # Vitaria_Palette_Forest: рельеф, россыпь, небо, плитки поля
PALETTE_ASSETS = ("Hex_Tile_Forest_",)  # свои ассеты в палитре биома (в ките и в FBX)
WATER_STYLE = "lake"                 # Vitaria_Water: ручей течёт (scroll — SCROLL ниже)
BACKDROP_USED = False

CAMERA = Mw.CAMERA
# солнце слева спереди, ниже, чем у «Кладбища»: тени деревьев левого края ложатся вправо-назад, длинные
SUN = dict(direction=(0.55, 0.45, -0.7), energy=3.1, color=(1.0, 0.89, 0.76))

TILES = {"neutral": ["Hex_Tile_Forest_A", "Hex_Tile_Forest_A", "Hex_Tile_Forest_B", "Hex_Tile_Forest_C"],
         "player": "Hex_Tile_Blue", "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]}


def GAME_LOOK(sun_fwd):
    return _BI.game_look(sun_fwd, "forest")


VIEW_BOX = Mw.VIEW_BOX
board_flat = Mw.board_flat
KEEP_OUT_BOARD = Mw.KEEP_OUT_BOARD

NATURE_EXTRA = ("Forest_",)
# радиусы занятости (м при масштабе 1): длинные префиксы раньше коротких — берётся первый подходящий
RADII = {"Forest_FencePost": 0.2, "Forest_Fence": 0.5, "Forest_Spruce": 0.62, "Forest_Pine": 0.6,
         "Forest_Tree_A": 1.4, "Forest_Tree": 0.95, "Forest_Bush": 0.5, "Forest_Fern": 0.4, "Forest_Boulder": 1.2,
         "Forest_Rock": 0.4, "Forest_FallenLog": 2.0, "Forest_LogPile": 1.3, "Forest_ChopBlock": 0.5,
         "Forest_LeanTo": 1.6, "Forest_HideRack": 0.8, "Forest_Bucket": 0.25, "Forest_Torch": 0.3,
         "Forest_Bridge": 1.8, "Forest_Stall": 1.7, "Forest_Mushrooms": 0.4, "Forest_Flowers": 0.2}
ATTACHMENTS = {"Forest_Torch": dict(fx=("FX_Flame_Small", 0.0, 0.0, FA.TORCH_TOP, 0.72),
                                    sockets=[("embers", FA.TORCH_TOP + 0.35, 0.4)])}
SCROLL = {"Arena_Forest_Water": 0.22}  # ручей течёт, как река «Луга» (водопады — по окончанию _Falls)
PREVIEW_FIGHTERS = [("Preview_Troll", 1, 1, 0), ("Preview_Troll", 0, 3, 0), ("Preview_Goblin", 7, 1, 0),
                    ("Preview_Goblin", 8, 2, 0), ("Preview_Goblin", 7, 3, 0)]

# ---------------------------------------------------------------------------------------
# остров: одна терраса, обрыв — пласты и столбы серой скалы, мох на уступах
# ---------------------------------------------------------------------------------------
FADE = ("cliff_fade", -20.0, 2.4, 0.06)
DEEP = dict(bottom=-22.0, jag=7.0, strata=(0.035, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.64, 0.77),
            batter=-0.25, amp=0.42, wave=0.6, lean=0.09, crack=0.22, zref=-0.36, nseed=4.3, union=True, ledge=0.16)
GRASS = "grass"
BOULDERS = False
WOB, TSEED = 0.24, 17

# кромка по макету; спереди слева — уступ, с которого срывается ручей
CTRL = [
    # перед (слева направо)
    (-15.3, -3.6), (-14.3, -4.55), (-13.0, -5.25), (-11.95, -5.6), (-11.0, -5.85), (-9.9, -6.1), (-8.9, -6.35),
    (-7.5, -6.6),
    (-5.9, -6.95), (-4.4, -7.15), (-2.8, -7.25), (-1.1, -7.25), (0.6, -7.3), (2.3, -7.45), (4.0, -7.55),
    (5.4, -7.95), (6.5, -8.35), (7.6, -8.4), (8.7, -8.15), (10.0, -7.7), (11.2, -7.25), (12.3, -6.7), (13.4, -5.85),
    (14.3, -4.85), (15.0, -3.55), (15.55, -1.95),
    # правый край
    (15.9, 0.1), (16.0, 2.4), (15.7, 4.8), (15.0, 7.0), (13.8, 8.9), (12.2, 10.3),
    # зад (справа налево)
    (10.0, 11.3), (7.5, 11.9), (5.0, 12.1), (2.5, 12.2), (0.0, 12.3), (-2.5, 12.3), (-5.0, 12.1), (-7.5, 11.8),
    (-10.0, 11.2), (-12.2, 10.2),
    # левый край
    (-13.9, 8.6), (-15.1, 6.4), (-16.0, 4.0), (-16.6, 1.6), (-16.7, -0.6), (-16.3, -2.3),
]
# тот же контур, что построит терраса (сплайн + шум кромки): по нему ставится устье ручья и водопад
OUTLINE = T.wobble(T.closed_spline(T.ccw(CTRL), 0.42), WOB, 0.33, TSEED)
OUT_N = T.edge_normals(OUTLINE)


def _rim_point(x, y):
    i = min(range(len(OUTLINE)), key=lambda k: (OUTLINE[k].x - x) ** 2 + (OUTLINE[k].y - y) ** 2)
    return OUTLINE[i].copy(), OUT_N[i].copy()


FALL_P, FALL_N = _rim_point(-11.05, -5.85)             # устье ручья на кромке и наружная нормаль

# ---------------------------------------------------------------------------------------
# ручей: русло от камней в глубине к передней кромке; уровень воды падает перекатами
# ---------------------------------------------------------------------------------------
STREAM_CTRL = [(-14.95, 3.75), (-14.3, 2.8), (-13.55, 1.75), (-12.75, 0.45), (-12.2, -0.9), (-12.0, -2.3),
               (-11.8, -3.7), (-11.45, -4.95), (FALL_P.x, FALL_P.y), (FALL_P.x + FALL_N.x * 0.9, FALL_P.y + FALL_N.y * 0.9)]
STREAM = T.open_spline(STREAM_CTRL, 0.25)
_CUM = [0.0]
for _k in range(1, len(STREAM)):
    _CUM.append(_CUM[-1] + (STREAM[_k] - STREAM[_k - 1]).length)
S_FALL = min(range(len(STREAM)), key=lambda k: (STREAM[k] - FALL_P).length)
S_FALL = _CUM[S_FALL]                                   # длина русла до кромки
HALF = 0.62                                            # полуширина дна русла
# уровень воды по длине русла (м от истока): перекаты — короткие ступеньки
LEVELS = [(0.0, -0.05), (1.45, -0.08), (1.8, -0.15), (5.4, -0.18), (5.75, -0.24), (8.0, -0.26), (8.3, -0.3),
          (99.0, -0.31)]
CASCADES = [1.62, 5.58, 8.15]                           # где ступеньки: там пена и камни
_BOX = (min(p.x for p in STREAM) - 2.0, max(p.x for p in STREAM) + 2.0,
        min(p.y for p in STREAM) - 2.0, max(p.y for p in STREAM) + 2.0)


def water_z(s):
    for (s0, z0), (s1, z1) in zip(LEVELS, LEVELS[1:]):
        if s <= s1:
            t = (s - s0) / max(1e-6, s1 - s0)
            return z0 + (z1 - z0) * T.smoothstep(0.0, 1.0, t)
    return LEVELS[-1][1]


def stream_query(x, y):
    """(расстояние до оси русла минус полуширина дна, длина русла в ближайшей точке)."""
    if not (_BOX[0] <= x <= _BOX[1] and _BOX[2] <= y <= _BOX[3]):
        return 9.0, 0.0
    best, bs = 1e9, 0.0
    for k in range(len(STREAM) - 1):
        a, b = STREAM[k], STREAM[k + 1]
        dx, dy = b.x - a.x, b.y - a.y
        L2 = dx * dx + dy * dy
        t = max(0.0, min(1.0, ((x - a.x) * dx + (y - a.y) * dy) / max(1e-9, L2)))
        d = math.hypot(a.x + dx * t - x, a.y + dy * t - y)
        if d < best:
            best, bs = d, _CUM[k] + (_CUM[k + 1] - _CUM[k]) * t
    return best - HALF, bs


def stream_dist(x, y):
    return stream_query(x, y)[0]


def stream_sink(x, y):
    """Русло: дно на 0.1 м ниже воды, откосы берегов 0.85 м; у кромки не мелеет — ручей прорезает её (устье)."""
    d, s = stream_query(x, y)
    if d > 0.6:
        return 0.0
    return -(water_z(s) - 0.1) * T.smoothstep(0.6, -0.25, d)


def _line_normals(L):
    out = []
    for i in range(len(L)):
        a, b = L[max(0, i - 1)], L[min(len(L) - 1, i + 1)]
        d = (b - a).normalized()
        out.append(Vector((-d.y, d.x, 0.0)))
    return out


def stream_points():
    """Вершины по берегам русла: ровный откос без зубцов сетки."""
    pts = []
    nr = _line_normals(STREAM)
    for d in (0.0, HALF - 0.3, HALF, HALF + 0.3, HALF + 0.6):
        for sgn in ((1,) if d == 0.0 else (1, -1)):
            for p, n in zip(STREAM, nr):
                q = p + n * (sgn * d)
                pts.append((q.x, q.y))
    return pts


# мостик через ручей: центр, поворот (пролёт поперёк русла), верх настила
BRIDGE_AT = (-12.69, 0.3, 27.0, 0.14)


def _bridge_ends():
    x, y, rz, _ = BRIDGE_AT
    c, s = math.cos(math.radians(rz)), math.sin(math.radians(rz))
    h = FA.BRIDGE_L / 2
    return (x - c * h, y - s * h), (x + c * h, y + s * h)


# ---------------------------------------------------------------------------------------
# тропы и поляны (грунт по рельефу)
# ---------------------------------------------------------------------------------------
_BW, _BE = _bridge_ends()
PATHS = [
    # от мостика вверх к лагерю, по заднему краю поля к красному знамени и вниз, к навесу
    ([(_BE[0] + 0.15, _BE[1] - 0.05), (-10.55, 2.5), (-9.95, 4.05), (-8.6, 5.1), (-6.2, 5.45), (-3.0, 5.5), (0.5, 5.55),
      (3.8, 5.58), (6.6, 5.45), (9.1, 5.0), (10.85, 3.85), (11.4, 2.0), (11.3, 0.0), (11.15, -1.6), (11.25, -2.2)],
     0.48, (0.6, 1.0)),
    # от мостика вниз вдоль поля к передней кромке и вдоль неё — к навесу
    ([(_BE[0] + 0.05, _BE[1] - 0.2), (-10.4, -0.9), (-10.3, -2.6), (-10.05, -4.2), (-9.2, -5.15), (-7.0, -5.4),
      (-4.0, -5.5), (-1.0, -5.45), (2.0, -5.42), (5.0, -5.38), (7.8, -5.25), (9.5, -4.95), (10.15, -4.75)],
     0.47, (0.6, 1.0)),
    # за мостиком — в лес
    ([(_BW[0] - 0.1, _BW[1] + 0.05), (-14.9, -0.95), (-15.7, -1.6)], 0.45, (0.0, 1.4)),
]
PATH_LINES = [T.open_spline(p, 0.3) for p, _, _ in PATHS]

# поляны: (центр, полуоси, поворот, сид)
CLEARINGS = [((-6.6, 6.6), (2.3, 1.15), 8, 1), ((11.9, -3.5), (2.4, 1.8), 35, 2), ((6.3, 6.5), (1.8, 0.8), -8, 3),
             ((-10.8, 4.9), (1.2, 0.8), 25, 4), ((11.6, 2.4), (1.1, 0.75), 70, 5)]


def path_dist(x, y):
    best = 9.0
    for L, (_, half, _) in zip(PATH_LINES, PATHS):
        for a, b in zip(L, L[1:]):
            if abs(a.x - x) > 3.0 and abs(b.x - x) > 3.0:
                continue
            dx, dy = b.x - a.x, b.y - a.y
            t = max(0.0, min(1.0, ((x - a.x) * dx + (y - a.y) * dy) / max(1e-9, dx * dx + dy * dy)))
            best = min(best, math.hypot(a.x + dx * t - x, a.y + dy * t - y) - half)
    return best


def grass_patches(x, y):
    """Трава пятнами; у ручья — сырая и темнее; дальше от поля, под пологом леса, — темнее (поле — светлая
    поляна, как на макете)."""
    d = stream_dist(x, y)
    k = T.smoothstep(1.4, 0.2, d) if d < 1.4 else 0.0
    n = T.noise.noise(Vector((x * 0.21, y * 0.21, 5.4)))
    m = T.noise.noise(Vector((x * 0.43, y * 0.43, 2.2)))
    far = T.smoothstep(1.8, 6.5, B.dist_to_board(x, y))
    return -0.22 * k - 0.24 * T.smoothstep(0.05, 0.45, n) + 0.16 * T.smoothstep(0.15, 0.5, m) - 0.24 * far


class _Terrace(T.Terrace):
    """Терраса «Леса»: свес дёрна идёт за кромкой и там, где её прорезает русло ручья (edge_height)."""

    def build_lip(self, a):
        if not self.lip:
            return
        bm, uvl = a.bm, a.uv
        prof = [(0.0, 0.0), (0.15, -0.08), (0.2, -0.24), (0.07, -0.45), (-0.55, -0.5)]
        cols = [ramp_uv(self.ramp, 0.96), SW_UV["sod"], SW_UV["sod_dark"], SW_UV["soil_dark"]]
        zb = [self.edge_height(p.x, p.y) if self.edge_height is not None else self.z for p in self.outline]
        rings = []
        for pi, (dn, dz) in enumerate(prof):
            ring = []
            for p, nv, z0 in zip(self.outline, self.normals, zb):
                j = 0.0
                if pi in (2, 3):
                    j = 0.09 * T.noise.noise(Vector((p.x * 0.8, p.y * 0.8, self.seed + 11.0))) - \
                        0.08 * abs(T.noise.noise(Vector((p.x * 2.3, p.y * 2.3, self.seed + 13.0))))
                q = p + nv * dn
                ring.append(bm.verts.new((q.x, q.y, z0 + dz + j)))
            rings.append(ring)
        n = len(self.outline)
        keep = []
        for i in range(n):
            p0, p1 = self.outline[i], self.outline[(i + 1) % n]
            keep.append(T.in_view(p0.x, p0.y, 2.0) or T.in_view(p1.x, p1.y, 2.0))
        for k in range(len(rings) - 1):
            for i in range(n):
                if not keep[i]:
                    continue
                j = (i + 1) % n
                try:
                    f = bm.faces.new((rings[k][i], rings[k + 1][i], rings[k + 1][j], rings[k][j]))
                except ValueError:
                    continue
                for l in f.loops:
                    l[uvl].uv = cols[k]
                f.normal_update()
        loose = [v for ring in rings for v in ring if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")


def terraces():
    t = _Terrace("arena", CTRL, z=0.0, bottom=-3.0, seed=TSEED, wob=WOB, flat=board_flat, grid=0.72, hills=0.0,
                 grass=(0.32, 0.88), shelves=(0.42, 0.72), fade=FADE, deep=DEEP, ramp=GRASS, sink=stream_sink,
                 extra_pts=stream_points(), patches=grass_patches)
    t.edge_height = lambda x, y: -stream_sink(x, y)
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


# ---------------------------------------------------------------------------------------
# своё у окружения в рельефе: ручей, тропы, поляны, мох и лианы с кромки, столбы обрыва
# ---------------------------------------------------------------------------------------
def stream_ribbon(a, tile=2.4):
    """Вода ручья лентой по руслу: уровень по LEVELS (перекаты — ступеньки), края заходят под берега.
    UV как у реки «Луга»: U поперёк, V вдоль течения (её крутит игра — SCROLL)."""
    bm, uvl = a.bm, a.uv
    nr = _line_normals(STREAM)
    rows = []
    last = max(k for k in range(len(STREAM)) if _CUM[k] <= S_FALL - 0.3)
    for k in range(last + 1):
        c, s = STREAM[k], _CUM[k]
        hw = HALF + 0.3 + 0.08 * T.noise.noise(Vector((s * 0.7, 3.3, 0.5)))
        z = water_z(s)
        row = []
        for i in range(5):
            u = i / 4 - 0.5
            p = c + nr[k] * (u * 2 * hw)
            row.append((bm.verts.new((p.x, p.y, z)), (u * 2 * hw / tile, -s / tile)))
        rows.append(row)
    n = 0
    for k in range(len(rows) - 1):
        for i in range(4):
            q = (rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i])
            try:
                f = bm.faces.new([x[0] for x in q])
            except ValueError:
                continue
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
            uvm = {x[0]: x[1] for x in q}
            for l in f.loops:
                l[uvl].uv = uvm[l.vert]
            n += 1
    return n


def cascade_foam(a, rng):
    """Пена на перекатах и у камней: плоские белые комья поперёк русла (палитра биома: foam)."""
    nr = _line_normals(STREAM)
    made = 0
    for sc in CASCADES:
        k = min(range(len(STREAM)), key=lambda j: abs(_CUM[j] - sc))
        c, n = STREAM[k], nr[k]
        z = water_z(sc + 0.15)
        for j in range(6):
            u = rng.uniform(-0.8, 0.8) * HALF
            along = (STREAM[min(k + 1, len(STREAM) - 1)] - STREAM[max(k - 1, 0)]).normalized()
            p = c + n * u + along * rng.uniform(0.05, 0.4)
            r = rng.uniform(0.1, 0.18)
            a.add(p_ico(r, 1, loc=(p.x, p.y, z + r * 0.04), scl=(1.5, 1.0, 0.24), jitter=0.15, rng=rng, cut=-0.02),
                  lambda f: "foam" if f.normal.z > -0.2 else "water_dark")
            made += 1
    return made


def terrain_extra(V, TR, ts, assets):
    t = ts["arena"]
    g = assets["ground"]
    n_w = stream_ribbon(assets["water"])
    n_f = cascade_foam(g, random.Random(5))
    n_p = 0
    for k, (L, (ctrl, half, taper)) in enumerate(zip(PATH_LINES, PATHS)):
        TR.build_road(g, ctrl, t, half=half, taper=taper, seed=k + 3, lift=0.026)
        n_p += 1
    for c, r, rot, sd in CLEARINGS:
        TR.decal(g, t, _blob(c, r, rot, sd), "dirt",
                 lambda x, y, rr: 0.35 + 0.4 * min(1.0, rr / 0.6), lift=0.02, grid=0.5)
    n_m = moss_drapes(t, assets["cliffs"])
    n_k = rim_columns(t, assets["cliffs"], random.Random(29))
    n_b = rim_blocks(t, assets["cliffs"], random.Random(41))
    n_s = rim_stones(t, assets["cliffs"], random.Random(43))
    print("forest: stream %d tris, foam %d, paths %d, clearings %d, drapes %d, rim columns %d, rim blocks %d, "
          "rim stones %d" % (n_w, n_f, n_p, len(CLEARINGS), n_m, n_k, n_b, n_s))


def _near_fall(x, y, r=2.0):
    return math.hypot(x - FALL_P.x, y - FALL_P.y) < r


def moss_drapes(t, a):
    """Бороды мха, корни и лианы с кромки обрыва — там, где кромку видно из камеры боя; у водопада — нет."""
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
        s += rng.uniform(0.55, 1.3)
        if not T.in_view(P.x, P.y, 1.0) or N.y > 0.6 or _near_fall(P.x, P.y, 1.6):
            continue
        top = P + N * rng.uniform(0.0, 0.04) + Vector((0, 0, -0.1))
        u = rng.random()
        if u < 0.18:                                      # корень
            SA._strand(a, top, rng.uniform(0.9, 2.2), rng.uniform(0.07, 0.1), 0.07, "bark_dark", rng, kink=0.1)
        elif u < 0.45:                                    # лиана: длинная плеть с листьями
            vine(a, top, N, rng.uniform(1.4, 3.4), rng)
        else:
            Lh = rng.uniform(0.3, 0.8) if rng.random() < 0.65 else rng.uniform(0.9, 1.7)
            SA.drape(a, top, N, Lh, rng.uniform(0.18, 0.4), rng.choice(["moss", "moss_hang", "sod_dark", "sod"]),
                     rng, taper=0.5)
        made += 1
    return made


def vine(a, top, N, length, rng):
    """Лиана с кромки: тонкая плеть вниз по скале и по ней комочки листьев."""
    top = Vector(top)
    N = Vector((N.x, N.y, 0.0)).normalized()
    pts = [top]
    k = 3
    for j in range(1, k + 1):
        pts.append(top + N * (0.12 * j / k + rng.uniform(-0.03, 0.05)) +
                   Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), -length * j / k)))
    for p0, p1 in zip(pts, pts[1:]):
        SA._limb(a, p0, p1, 0.035, 0.03, "leaf_dark", seg=4)
    for j in range(rng.randint(2, 4)):
        p = pts[0].lerp(pts[-1], rng.uniform(0.1, 0.95)) + N * 0.06
        a.add(p_ico(rng.uniform(0.1, 0.17), 1, loc=p, scl=(1.2, 0.7, 1.0), jitter=0.15, rng=rng),
              lambda f: "leaf_mid" if f.normal.z > 0.2 else "leaf_dark")


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


def by_normal_moss(f):
    return "moss" if f.normal.z > 0.6 else "sod_dark"


def _column(a, rng, P, N, w, d, h, top, out, tilt):
    """Тёсаный столб скалы у кромки: брусок наклонён внутрь вдоль пластов, грани — рампой обрыва по высоте,
    макушка — камень с кочками мха, с макушки свисает мох."""
    z0, z1, jamp = FADE[1], FADE[2], FADE[3]
    theta = math.atan2(N.y, N.x) - math.pi / 2 + math.radians(rng.uniform(-6.0, 6.0))
    M = Matrix.Translation(Vector((P.x + N.x * out, P.y + N.y * out, top))) @ \
        Matrix.Rotation(theta, 4, "Z") @ Matrix.Rotation(-math.radians(tilt), 4, "X")
    part = p_box((w, d, h), loc=(0.0, -d / 2, -h / 2), bevel=0.06)
    bmesh.ops.transform(part, matrix=M, verts=part.verts)
    jit = rng.uniform(-1.0, 1.0) * jamp
    _add_ramp(a, part, lambda z, nz, j=jit: clamp((z - z0) / (z1 - z0) - 0.05 + j + 0.1 * nz), top="rock")
    for j in range(rng.randint(2, 3)):                  # мох кочками на каменной макушке
        mx, my = rng.uniform(-0.35, 0.35) * w, -d / 2 + rng.uniform(-0.3, 0.3) * d
        r = rng.uniform(0.25, 0.45) * min(w, d)
        cap = p_ico(r, 1, loc=(mx, my, 0.0), scl=(1.3, 1.0, 0.32), jitter=0.12, rng=rng, cut=-0.02)
        bmesh.ops.transform(cap, matrix=M, verts=cap.verts)
        a.add(cap, by_normal_moss)
    side = Vector((-N.y, N.x, 0.0))
    for j in range(rng.randint(1, 3)):                  # мох и лианы с кромки столба
        u = rng.uniform(-0.4, 0.4) * w
        tp = P + N * (out - 0.06) + side * u + Vector((0, 0, top - 0.03))
        if rng.random() < 0.35:
            vine(a, tp, N, rng.uniform(1.0, 2.6), rng)
        else:
            SA.drape(a, tp, N, rng.uniform(0.4, 1.4), rng.uniform(0.14, 0.32),
                     rng.choice(["moss", "moss_hang", "sod_dark"]), rng, taper=0.8)


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


def rim_columns(t, a, rng):
    """Столбы обрыва, как на макете: тёсаные блоки серой скалы у кромки выступают из пластов, макушки на разной
    высоте под мхом (обрыв ступенями), с макушек свисают мох и лианы; у водопада — нет."""
    made = [0]

    def step(P, N):
        w = rng.uniform(1.1, 2.2)
        if not T.in_view(P.x, P.y, 1.5) or N.y > 0.35 or _near_fall(P.x, P.y, 2.2):
            return w * rng.uniform(0.9, 1.4)
        top = rng.uniform(-0.42, -0.2) if rng.random() < 0.3 else rng.uniform(-2.6, -0.8)
        _column(a, rng, P, N, w, rng.uniform(1.0, 1.6), rng.uniform(8.0, 13.0), top, rng.uniform(0.12, 0.45),
                rng.uniform(7.0, 11.0))
        made[0] += 1
        return w * rng.uniform(0.9, 1.4)
    _walk(t, rng, step)
    return made[0]


def rim_blocks(t, a, rng):
    """Тёсаные глыбы на самой кромке (макушки столбов вровень с травой): рвут ровный свес дёрна."""
    made = [0]
    rock = lambda f: "sod" if f.normal.z > 0.7 else ("rock" if f.normal.z > -0.3 else "rock_dark")

    def step(P, N):
        if not T.in_view(P.x, P.y, 1.0) or N.y > 0.35 or _near_fall(P.x, P.y, 2.0):
            return rng.uniform(3.4, 6.0)
        w, d, h = rng.uniform(0.9, 1.5), rng.uniform(0.7, 1.05), rng.uniform(0.55, 0.9)
        top = rng.uniform(-0.04, 0.12)
        c = P + N * rng.uniform(0.0, 0.25)
        theta = math.atan2(N.y, N.x) - math.pi / 2 + math.radians(rng.uniform(-9.0, 9.0))
        M = Matrix.Translation(Vector((c.x, c.y, top - h / 2))) @ Matrix.Rotation(theta, 4, "Z") @ \
            Matrix.Rotation(math.radians(rng.uniform(-4, 4)), 4, "X")
        part = p_box((w, d, h), bevel=0.06)
        bmesh.ops.transform(part, matrix=M, verts=part.verts)
        a.add(part, rock)
        mx, my = rng.uniform(-0.3, 0.3) * w, rng.uniform(-0.25, 0.25) * d
        cap = p_ico(rng.uniform(0.18, 0.26) * w, 1, loc=(mx, my, h / 2), scl=(1.3, 1.0, 0.3), jitter=0.12, rng=rng,
                    cut=-0.02)
        bmesh.ops.transform(cap, matrix=M, verts=cap.verts)
        a.add(cap, lambda f: "sod" if f.normal.z > 0.6 else "sod_dark")
        made[0] += 1
        return rng.uniform(3.4, 6.0)
    _walk(t, rng, step, (0.5, 2.0))
    return made[0]


# столбы-«пальцы» у передней кромки справа, как на макете: макушки выше травы (x, y, ширина, глубина, верх)
RIM_STONES = [(4.15, -7.45, 1.15, 1.0, 0.42), (5.75, -7.85, 1.55, 1.25, 0.66)]


def rim_stones(t, a, rng):
    made = 0
    for x, y, w, d, top in RIM_STONES:
        P, N = _rim_point(x, y)
        _column(a, rng, P - N * (d * 0.45), N, w, d, rng.uniform(9.0, 12.0), top, d * 0.45, rng.uniform(3.0, 5.0))
        made += 1
    return made


# ---------------------------------------------------------------------------------------
# расстановка
# ---------------------------------------------------------------------------------------
def _face(n):
    """Поворот (град) ассета «лицом к -Y», чтобы лицо смотрело по n."""
    return math.degrees(math.atan2(n[0], -n[1]))


# Высота дерева перед полем: верх не выше линии взгляда камеры боя на нижние вершины клеток переднего ряда
# (y = -4.62 на траве) — ёлки у кромки не заслоняют клетки
_CAM_Y = CAMERA["target"][1] - CAMERA["distance"] * math.cos(math.radians(CAMERA["pitch"]))
_CAM_Z = CAMERA["distance"] * math.sin(math.radians(CAMERA["pitch"]))


def front_height(y, margin=0.3):
    y0 = -4.62
    if y >= y0:
        return 0.0
    return _CAM_Z * (y - y0) / (_CAM_Y - y0) - margin


def scatter_ok(tname, x, y, r):
    if stream_dist(x, y) < r + 0.35:
        return False
    if path_dist(x, y) < r * 0.6 + 0.25:
        return False
    bx, by = BRIDGE_AT[0], BRIDGE_AT[1]
    if math.hypot(x - bx, y - by) < 2.2 + r:
        return False
    if _near_fall(x, y, 1.6 + r):
        return False
    db = B.dist_to_board(x, y)
    if r > 0.33 and db < 1.6 + r:                       # кусты и камни — не вплотную к полю
        return False
    if r >= 0.9:                                         # деревья: не ближе 2.5 м к полю и не перед ним
        if db < 2.0 + r * 0.5:
            return False
        if y < -4.3 and abs(x) < 12.0:
            return False
    elif r >= 0.5 and y < -4.3 and abs(x) < 11.0:      # подрост перед полем — только руками (placements)
        return False
    for (cx, cy), (ra, rb), _, _ in CLEARINGS:
        if r > 0.33 and (x - cx) ** 2 / (ra + r) ** 2 + (y - cy) ** 2 / (rb + r) ** 2 < 1.0:
            return False
    return True


SPRUCE_H = {"Forest_Spruce_A": 6.4, "Forest_Spruce_B": 5.0, "Forest_Spruce_C": 2.9, "Forest_Spruce_D": 5.8,
            "Forest_Tree_C": 3.0, "Forest_Bush_A": 0.6, "Forest_Bush_B": 0.65}


def _front_rim_y(x):
    """y передней кромки острова над точкой x (по тому же контуру, что строит терраса)."""
    pts = [p for p in OUTLINE if p.y < -2.0]
    pts.sort(key=lambda p: p.x)
    for p0, p1 in zip(pts, pts[1:]):
        if p0.x <= x <= p1.x:
            t = (x - p0.x) / max(1e-6, p1.x - p0.x)
            return p0.y + (p1.y - p0.y) * t
    return min(pts, key=lambda p: abs(p.x - x)).y


# перед полем, между оградой и кромкой: (ассет, x, отступ от кромки внутрь, масштаб) — ёлки ниже линии взгляда
FRONT = [
    ("Forest_Fern", -8.3, 0.45, 1.0), ("Forest_Bush_A", -7.2, 0.5, 1.1), ("Forest_Spruce_C", -5.9, 0.55, 1.3),
    ("Forest_Fern", -5.0, 0.4, 1.1), ("Forest_Tree_C", -4.0, 0.6, 1.0), ("Forest_Spruce_C", -2.75, 0.5, 0.85),
    ("Forest_Spruce_C", -0.4, 0.75, 1.0), ("Forest_Bush_A", 0.7, 0.45, 1.1), ("Forest_Spruce_C", 1.6, 0.55, 0.8),
    ("Forest_Spruce_C", 2.95, 0.6, 1.3), ("Forest_Fern", 3.75, 1.15, 1.0), ("Forest_Spruce_C", 4.6, 1.2, 0.75),
    ("Forest_Tree_C", 8.6, 0.85, 1.0), ("Forest_Spruce_C", 9.75, 0.6, 1.3), ("Forest_Bush_B", 10.6, 0.5, 1.0),
    ("Forest_Spruce_D", 11.75, 0.8, 0.8),
]


def placements():
    P = []

    def add(asset, x, y, rz=0.0, s=1.0, on="arena", z=None, tilt=(0.0, 0.0), zabs=None):
        P.append(dict(asset=asset, x=x, y=y, rz=rz, s=s, on=on, z=z, tilt=tilt, zabs=zabs))

    rng = random.Random(2024)
    # --- лагерь игрока (слева): шалаш, костёр, рама со шкурой, бревно-скамья, дуб, валуны, знамя, факел, ведро
    add("Forest_LeanTo", -6.7, 7.75, 12)
    add("Prop_Campfire", -6.2, 6.38, 0)
    add("Forest_FallenLog", -8.45, 6.35, -5, 0.45)
    add("Forest_HideRack", -4.4, 7.55, -8)
    add("Forest_Tree_A", -10.4, 7.25, 30, 1.0)
    add("Forest_Boulder_A", -12.4, 8.3, 40, 1.0)
    add("Forest_Boulder_B", -8.85, 8.95, 200, 0.95)
    add("Prop_Banner_Blue", -12.35, 3.2, 0)
    add("Forest_Torch", -10.55, 5.4, 0)
    add("Forest_Bucket", -11.25, 5.55, 20)
    add("Forest_Rock_A", -12.0, 4.45, 30, 1.1)
    # --- ручей: камни у истока (из-под них бьёт каскад), по берегам и у водопада, камни-пороги, мостик
    add("Forest_Boulder_A", -15.0, 4.9, 160, 0.95)
    add("Forest_Boulder_B", -13.55, 5.3, 70, 0.8)
    add("Forest_Rock_A", -13.55, 2.85, 10, 1.0)
    add("Forest_Rock_A", -14.95, 1.7, 200, 1.1)
    add("Forest_Rock_A", -11.35, -1.4, 60, 0.9)
    add("Forest_Rock_B", -12.75, -2.6, 0, 1.2)
    add("Forest_Boulder_B", -13.75, -3.45, 300, 0.95)
    add("Forest_Boulder_B", -12.55, -4.6, 20, 0.8)
    add("Forest_Rock_A", -9.85, -5.95, 120, 1.0)
    nr = _line_normals(STREAM)
    for j, sc in enumerate(CASCADES):                    # камни-пороги в воде
        k = min(range(len(STREAM)), key=lambda q: abs(_CUM[q] - sc))
        for side in (-1, 1):
            p = STREAM[k] + nr[k] * (side * HALF * rng.uniform(0.5, 0.85))
            add("Forest_Rock_A", p.x, p.y, rng.uniform(0, 360), rng.uniform(0.75, 0.95), zabs=water_z(sc) - 0.16)
    bx, by, brz, bz = BRIDGE_AT
    add("Forest_Bridge", bx, by, brz, zabs=bz)
    # --- лагерь гоблинов (справа): знамя, факел, валун, навес с бочками и вёдрами
    add("Prop_Banner_Red", 11.25, 3.85, 0)
    add("Forest_Torch", 11.85, 1.15, 0)
    add("Forest_Boulder_A", 13.45, 1.75, 120, 0.95)
    add("Forest_Rock_A", 12.55, 0.25, 0, 1.0)
    add("Forest_Stall", 12.15, -3.2, -62)
    add("Prop_Barrel", 10.3, -3.35, 0, 1.0)
    add("Prop_Barrel", 10.6, -4.0, 40, 0.95)
    add("Forest_Bucket", 9.95, -3.8, 0, 1.0)
    add("Forest_Bucket", 13.15, -4.65, 50, 0.9)
    add("Prop_Crate", 13.85, -3.55, 15, 0.9)
    # --- сзади: штабель брёвен с колодой, валуны, грибы
    add("Forest_LogPile_A", 6.55, 6.95, -12)
    add("Forest_ChopBlock", 4.75, 6.75, 30)
    add("Forest_Boulder_B", 4.8, 8.3, 80, 1.0)
    add("Forest_Boulder_A", 8.6, 8.4, 10, 0.85)
    add("Forest_Mushrooms_S", -2.6, 7.3, 0)
    add("Forest_Mushrooms", 10.0, 6.9, 140, 0.8)
    # --- спереди: валун у кромки, грибы, брёвна у края, ромашки и жёлтые цветы
    add("Forest_Boulder_B", -1.55, _front_rim_y(-1.55) + 0.55, 30, 0.75)
    add("Forest_Mushrooms", 5.45, _front_rim_y(5.45) + 1.45, 0)
    add("Forest_Mushrooms_S", 6.2, _front_rim_y(6.2) + 1.6, 40)
    add("Forest_LogPile_B", 7.6, _front_rim_y(7.6) + 0.95, -24)
    for x, y in ((-9.55, -5.1), (-8.9, -5.75), (-2.7, -5.0), (-0.5, -5.95), (2.5, -5.95), (8.6, -5.95), (-6.6, -6.0)):
        add("Forest_Flowers_W", x, y, rng.uniform(0, 360))
    for x, y in ((4.1, -6.3), (-13.4, -2.4), (10.9, -5.5)):
        add("Forest_Flowers_Y", x, y, rng.uniform(0, 360))
    # --- перед полем: ёлки, кусты, папоротник (высота ёлок — по линии взгляда на нижний ряд клеток)
    for asset, x, d, s in FRONT:
        y = _front_rim_y(x) + d
        if asset in SPRUCE_H and abs(x) < 11.0:
            s = min(s, front_height(y) / SPRUCE_H[asset])
        add(asset, x, y, rng.uniform(0, 360), s)
    # --- опушка: пояса деревьев (позади поля — ниже и ближе, дальше — высокие; по бокам — высокие ели)
    taken = [(p["x"], p["y"], _rad(p["asset"], p["s"])) for p in P]
    for f in FENCES:
        for (x0, y0), (x1, y1) in zip(f["pts"], f["pts"][1:]):
            n = max(1, int(math.hypot(x1 - x0, y1 - y0) / 0.6))
            taken += [(x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n, 0.3) for k in range(n + 1)]

    def ok_spot(x, y, r):
        if not T.point_in_poly(x, y, OUTLINE) or T.dist_to_outline(x, y, OUTLINE) < 0.45 + r * 0.45:
            return False
        if stream_dist(x, y) < r + 0.3 or path_dist(x, y) < r * 0.5 + 0.25 or _near_fall(x, y, 1.5 + r):
            return False
        if math.hypot(x - BRIDGE_AT[0], y - BRIDGE_AT[1]) < 2.1 + r or B.dist_to_board(x, y) < 1.4 + r:
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

    for asset, x, y, sc in (("Forest_Tree_B", 13.25, 6.1, 1.0), ("Forest_Tree_B", 2.3, 8.75, 1.0),
                            ("Forest_Tree_C", -1.7, 8.45, 1.0), ("Forest_Tree_B", -14.45, 0.75, 0.9)):
        r = _rad(asset, sc)
        if ok_spot(x, y, r):
            add(asset, x, y, rng.uniform(0, 360), sc)
            taken.append((x, y, r))
    TALL = [("Forest_Spruce_A", 3.0), ("Forest_Spruce_B", 3.0), ("Forest_Spruce_D", 2.5), ("Forest_Tree_B", 2.0),
            ("Forest_Pine_A", 0.6)]
    UNDER = [("Forest_Spruce_C", 2.5), ("Forest_Tree_C", 1.6), ("Forest_Bush_A", 1.5), ("Forest_Bush_B", 1.3),
             ("Forest_Fern", 0.8), ("Forest_Spruce_B", 0.8)]
    belt((-13.0, 12.0, 6.6, 8.4), 26, UNDER, (0.85, 1.2))              # за оградой: подрост и кусты
    belt((-14.0, 13.0, 8.2, 10.6), 30, TALL, (0.85, 1.15))             # стена высоких деревьев
    belt((-12.0, 12.0, 10.4, 12.0), 14, TALL, (0.9, 1.2))
    belt((12.6, 15.8, 2.6, 8.8), 10, TALL, (0.9, 1.15))                # справа
    belt((13.4, 15.8, -2.6, 2.6), 6, TALL, (0.85, 1.1))
    belt((12.2, 15.2, -3.0, 5.0), 8, UNDER, (0.85, 1.15))
    belt((-16.6, -13.8, -2.8, 8.4), 14, TALL, (0.85, 1.15))            # слева, за ручьём
    belt((-16.4, -12.0, -4.6, 8.0), 10, UNDER, (0.8, 1.1))
    belt((-14.6, -12.4, -4.8, -3.0), 3, UNDER, (0.85, 1.1))            # у водопада слева
    belt((12.4, 14.6, -6.2, -4.0), 3, UNDER, (0.85, 1.05))             # у навеса справа
    return P


def _rad(asset, s):
    for k, v in RADII.items():
        if asset.startswith(k):
            return v * s
    return 0.5 * s


# жердевая ограда (сегмент 1.25 м, столб в начале сегмента, в конце ломаной — столб)
_F = dict(on="arena", asset="Forest_Fence", seg=FA.FENCE_SEG, end="Forest_FencePost")
FENCES = [
    dict(_F, pts=[(-3.5, 6.55), (-1.0, 7.0), (1.6, 7.15), (4.1, 7.25)]),                   # сзади
    dict(_F, pts=[(8.55, 6.95), (9.95, 6.05), (11.05, 4.95)]),                             # сзади справа, у знамени
    dict(_F, pts=[(-10.8, -1.75), (-10.75, -3.0), (-10.6, -4.25)]),                        # берег ручья к водопаду
    dict(_F, pts=[(-9.0, -5.85), (-7.7, -6.0), (-6.4, -6.2)]),                             # спереди слева
    dict(_F, pts=[(-3.6, -6.1), (-1.5, -6.0), (0.4, -6.15), (2.35, -6.2)]),                 # спереди посередине
    dict(_F, pts=[(5.6, -6.05), (7.0, -6.1)]),
    dict(_F, pts=[(9.75, -6.3), (11.3, -5.95), (12.75, -5.3), (13.85, -4.4)]),              # спереди справа
]

ROADS = []
RIVER = None
WATER = None
# водопады: ручей срывается с передней кромки в облака; у истока — малый каскад с камней
FALLS = [
    dict(pt=(FALL_P.x, FALL_P.y), normal=(FALL_N.x, FALL_N.y), width=1.75, z_top=water_z(S_FALL), z_bot=-17.0,
         reach=1.25, stream=0.55, segs=16, foam=False),
    dict(pt=(-14.98, 4.17), normal=(0.05, -1.0), width=0.75, z_top=0.78, z_bot=water_z(0.0) - 0.06, reach=0.12,
         stream=0.25, segs=5, foam=False),
]
BRIDGE, MILL_AT = Mw.BRIDGE, Mw.MILL_AT

FOREST = [
    # высокие деревья опушки (радиус 1.0: scatter_ok держит их за 2.6 м от поля и не пускает перед полем)
    ("arena", [("Forest_Spruce_A", 2.4), ("Forest_Spruce_B", 3.0), ("Forest_Spruce_D", 2.0), ("Forest_Pine_A", 0.6),
               ("Forest_Tree_B", 1.3)], 0.3, (0.85, 1.15), 1.0),
    # подрост: ёлки, деревца, кусты
    ("arena", [("Forest_Spruce_C", 2.0), ("Forest_Tree_C", 1.0), ("Forest_Bush_A", 1.6), ("Forest_Bush_B", 1.2),
               ("Forest_Boulder_B", 0.25), ("Forest_Rock_A", 0.5), ("Forest_FallenLog", 0.08)], 0.12, (0.8, 1.15), 0.6),
]
GROUND = [
    ("arena", [("Env_Tuft_B", 3.0), ("Env_Tuft_A", 2.0), ("Forest_Fern", 1.0), ("Env_Flowers_A", 1.0),
               ("Forest_Rock_B", 0.5), ("Forest_Mushrooms_S", 0.08)], 0.45, (0.8, 1.2), 0.26),
]
RIM_TUFTS = [("arena", 0.6)]

# ---------------------------------------------------------------------------------------
# небо: облака под и вокруг острова, дальние лесистые островки с водопадами
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
    ((-19.6, 15.4), -2.2, 3.4, 61, 5),
    ((2.5, 32.0), 2.0, 5.4, 62, 6),
    ((19.4, 14.6), -2.4, 3.1, 63, 4),
    ((-17.8, -10.6), -5.0, 1.4, 64, 1),
    ((19.2, -12.0), -6.6, 1.2, 65, 1),
]
# на дальних островках — ели и лиственные деревья ((крупный островок), (малый))
DISTANT_TREES = (["Forest_Spruce_A", "Forest_Spruce_B", "Forest_Spruce_D", "Forest_Tree_B"],
                 ["Forest_Spruce_C", "Forest_Tree_C"])


def _distant_rim(center, z, r, seed, want=(0.0, -1.0)):
    """Точка кромки дальнего островка, смотрящая на want (как его строит build_colony.build_sky)."""
    ix, iy = center
    irng = random.Random(seed)
    k = 7 + int(r * 1.5)
    ctrl = []
    for i in range(k):
        ang = math.tau * i / k + irng.uniform(-0.15, 0.15)
        rr = r * irng.uniform(0.8, 1.15)
        ctrl.append((ix + math.cos(ang) * rr, iy + math.sin(ang) * rr * irng.uniform(0.75, 0.95)))
    small = r < 2.5
    vb = T.VIEW_BOX
    T.VIEW_BOX = None
    t = T.Terrace("isle_%d" % seed, ctrl, z=z, seed=seed, wob=0.08 if small else 0.18, step=0.35 if small else 0.6)
    T.VIEW_BOX = vb
    w = Vector((want[0], want[1], 0.0)).normalized()
    i = max(range(len(t.outline)), key=lambda q: t.normals[q].dot(w) - 0.05 * abs((t.outline[q] - Vector((ix, iy, 0))).x))
    return t.outline[i], t.normals[i]


for _c, _z, _r, _sd, _n in DISTANT[:3]:                  # водопады дальних островков, лицом к камере
    _p, _nn = _distant_rim(_c, _z, _r, _sd, (0.25 if _c[0] < 0 else -0.25, -1.0))
    FALLS.append(dict(pt=(_p.x, _p.y), normal=(_nn.x, _nn.y), width=0.9 + 0.2 * _r, z_top=_z, z_bot=_z - 9.0,
                      reach=0.5, stream=0.6, segs=10, foam=False))

# ---------------------------------------------------------------------------------------
# look-dev превью: прохладный свет в чаще — мягкое солнце слева, сине-зелёная дымка, тёплые огни
# (в игре — LOOK_VARIANTS["forest"] в build_isle.py). Солнце слабее колониального, экспозиция выше: зоны поля
# на превью того же тона, что на «Луге» (синяя #6699b8 против #6a9fb9, красная #b2796f против #b97d6f)
# ---------------------------------------------------------------------------------------
LOOKDEV = dict(
    fog="#8299ab",
    ambient=(0.31, 0.39, 0.5),
    view="AgX", look="AgX - Base Contrast", exposure=0.42,
    sun_angle=6.0, sun_energy=3.1, world=1.0,
    shots={
        "hero": dict(fog=(24.0, 60.0, 0.8), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "wide": dict(fog=(24.0, 60.0, 0.8), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "ipad": dict(fog=(26.0, 64.0, 0.8), dof=(0.0, 0.0, 41.0, 60.0, 6.0)),
        "mock": dict(fog=(24.0, 60.0, 0.8), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
    },
    bloom=dict(threshold=0.8, strength=0.5, size=0.6),
    lift=(1.0, 1.0, 1.035), gamma=(1.0, 1.0, 1.0), gain=(0.96, 1.0, 1.05),
    saturation=1.06, vignette=0.25,
    sky_bounce=False,
    water=dict(tint=(0.86, 1.0, 1.0), falls_tint=(0.92, 1.0, 1.0), shallow="#8fd9cf", shallow_k=0.0),
    grass_noise=dict(scale=0.22, dark=(0.86, 0.9, 0.9), light=(1.06, 1.05, 0.96)),
    palettes=["Vitaria_Palette", "Vitaria_Palette_Forest"],
)
