"""
Арена «Болото» (Arena_Swamp) — второй биом боя, по макету «Swamp»: тот же остров-плато с полем 9x5,
что у «Луга», но вокруг поля — болото.

Контракт поля и камеры — как у «Луга» (meadow.py / isle.py): клетки, зоны, камера боя, корень на высоте поля;
меняется окружение. Раскладка снята с макета: центры синих и красных клеток макета дают гомографию
«картинка -> плоскость поля», по ней пруды, знамёна, деревья, навес и мостки встали в метры арены
(тот же кадр боя: в 16:9 видно то же, что на макете, кроме краёв за рамкой).
- остров одной террасой: оливковая мшистая земля в пятнах ила и яркого мха; пруды в низинах у края плато:
  длинный слева (мостки к кромке, кувшинки), у переднего края справа, два у заднего края и затянутый ряской
  слева сзади; лужа с ряской у факела гоблинов; рогоз и осока по берегам, камни по бровке;
- лагерь игрока слева: синее знамя, факел, столб с рогатым черепом, сухое корявое дерево в бородах мха,
  бочка, пень, верёвочный забор вдоль пруда;
- лагерь гоблинов справа: красное знамя, факел, большое болотное дерево, навес на настиле с бочками,
  мостки в задний пруд;
- обрыв — пласты парящего острова (как у «Луга»), тёмные сине-серые, кромка в мху; с неё свисают
  мох-сосульки и корни;
- вокруг — облака и дальние островки в верхних углах кадра; вид — «туманное зеленоватое утро»
  (build_isle.LOOK_VARIANTS["swamp"]).

Рельеф, россыпь и небо красятся палитрой биома Vitaria_Palette_Swamp (build_vitaria.PALETTE_VARIANTS),
ей же — оливковые плитки поля Hex_Tile_Swamp_*; вода — своим материалом Vitaria_Water_Swamp;
плитки зон, знамёна, навес, факел и огонь — общей палитрой.
"""
import math
import random
from mathutils import Vector
from . import board as B
from . import terrain as T
from . import meadow as Mw
from . import swamp_assets as SA
import build_isle as _BI

NAME = "Arena_Swamp"
PREFIX = "AS_"                       # коллекции сцены: AS_Terrain, AS_Props...
TERRAIN_DIR = "Swamp"                # Models/Arena/Swamp
PALETTE_VARIANT = "Swamp"            # Vitaria_Palette_Swamp: рельеф, россыпь, небо, плитки поля
PALETTE_ASSETS = ("Hex_Tile_Swamp_",)  # свои ассеты в палитре биома (в ките и в FBX)
WATER_STYLE = "swamp"                # Vitaria_Water_Swamp: пруды
BACKDROP_USED = False                # замка и деревни нет — Ref_Buildings не нужен

CAMERA = Mw.CAMERA                   # камера боя «Луга»: блок framing тот же
# утреннее солнце слева и чуть со стороны камеры (как на макете): передние пласты обрыва — в полутени,
# тени деревьев лагерей падают вправо, мимо поля
SUN = dict(direction=(0.66, 0.1, -0.74), energy=4.2, color=(1.0, 0.95, 0.84))

# плитки поля: нейтральные — оливковые болотные (те же формы, что у Hex_Tile_A/B/C), зоны и препятствия — общие
TILES = {"neutral": ["Hex_Tile_Swamp_A", "Hex_Tile_Swamp_A", "Hex_Tile_Swamp_B", "Hex_Tile_Swamp_C"],
         "player": "Hex_Tile_Blue", "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]}


def GAME_LOOK(sun_fwd):
    return _BI.game_look(sun_fwd, "swamp")


VIEW_BOX = Mw.VIEW_BOX
board_flat = Mw.board_flat
KEEP_OUT_BOARD = Mw.KEEP_OUT_BOARD

# своя природа и мелочь: в россыпь и слияние (Arena_Swamp_Scatter)
NATURE_EXTRA = ("Swamp_",)
RADII = {"Swamp_Willow": 1.6, "Swamp_DeadTree": 1.6, "Swamp_Snag": 1.0, "Swamp_Cattail": 0.32, "Swamp_Reeds": 0.18,
         "Swamp_LilyPad": 0.45, "Swamp_Hummock": 0.32, "Swamp_Mud": 0.3, "Swamp_Rock_A": 0.55, "Swamp_Rock_B": 0.3,
         "Swamp_SkullPost": 0.35, "Swamp_Boardwalk": 1.1, "Swamp_Rope": 0.3, "Swamp_Stake": 0.2,
         "Swamp_Awning": 1.6}
# огонь факела — FX_Flame_Small «Луга» (мерцает), над ним искры
ATTACHMENTS = {"Prop_Torch": dict(fx=("FX_Flame_Small", 0.0, 0.0, SA.TORCH_TOP, 0.72),
                                  sockets=[("embers", SA.TORCH_TOP + 0.35, 0.4)])}
SCROLL = {"Arena_Swamp_Water": 0.012}      # стоячая вода: еле дрейфует
# бойцы-заглушки на превью: (ассет, колонка, ряд, поворот)
PREVIEW_FIGHTERS = [("Preview_Troll", 1, 1, 0), ("Preview_Troll", 0, 3, 0), ("Preview_Goblin", 7, 1, 0),
                    ("Preview_Goblin", 8, 2, 0), ("Preview_Goblin", 7, 3, 0)]

WATER_Z = -0.2                       # уровень прудов (трава поля — 0)

# ---------------------------------------------------------------------------------------
# остров: одна терраса, обрыв — пласты парящего острова (как у isle.py)
# ---------------------------------------------------------------------------------------
FADE = ("cliff_fade", -20.0, 2.4, 0.06)
# пласты почти отвесные (на макете — тёмные столбы скалы в бородах мха): уступы мельче, чем у «Луга», и замшелые
DEEP = dict(bottom=-22.0, jag=7.0, strata=(0.035, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.64, 0.77),
            batter=-0.22, amp=0.4, wave=0.6, lean=0.08, crack=0.22, zref=-0.36, nseed=1.7, union=True, ledge=0.14)
GRASS = "grass"                      # мшистая земля биома (рампа grass палитры Swamp)
BOULDERS = False

# кромка плато по макету (зад — за задними прудами); передний край — как на макете: под ним в кадре боя
# видно 3–4 м пластов обрыва
CTRL = [
    # перед (слева направо)
    (-13.9, -5.0), (-12.9, -5.9), (-11.4, -6.15), (-9.6, -6.3), (-7.8, -6.45), (-6.0, -6.6), (-4.2, -6.75),
    (-2.4, -6.9), (-0.6, -7.05), (1.4, -7.25), (3.2, -7.4), (5.0, -7.5), (6.6, -7.45), (8.2, -7.25), (9.8, -7.1),
    (11.3, -6.7), (12.6, -6.05), (13.7, -5.15),
    # правый край
    (14.5, -3.8), (15.05, -2.0), (15.15, -0.4), (14.9, 1.3), (14.6, 3.0), (14.7, 4.8), (14.4, 6.6), (13.6, 8.4),
    # зад (справа налево)
    (12.3, 9.6), (10.6, 10.4), (8.8, 10.3), (6.9, 9.6), (5.0, 9.4), (3.0, 9.5), (1.0, 9.3), (-1.0, 9.0),
    (-3.0, 9.1), (-5.0, 8.9), (-7.0, 8.9), (-8.9, 8.7), (-10.8, 8.9), (-12.6, 8.7), (-14.2, 8.0),
    # левый край
    (-15.3, 6.6), (-15.9, 4.6), (-15.8, 2.5), (-15.4, 0.4), (-15.0, -1.6), (-14.6, -3.4),
]

# пруды в низинах (контуры по макету; до клеток не ближе метра, до кромки — 0.7 м бровки)
PONDS = [
    dict(name="left", seed=1, lily=0.32, duck=0.35, pts=[      # длинный вдоль левого края, мостки к кромке
        (-11.0, -1.6), (-10.65, -2.6), (-10.7, -3.7), (-11.15, -4.55), (-11.9, -5.05), (-12.75, -5.0), (-13.35, -4.4),
        (-13.75, -3.1), (-14.15, -1.5), (-14.45, 0.4), (-14.8, 2.4), (-14.95, 4.4), (-14.6, 6.1), (-14.0, 7.15),
        (-13.3, 7.0), (-12.95, 5.7), (-12.85, 4.0), (-12.75, 2.1), (-12.45, 0.6), (-11.9, -0.5)]),
    dict(name="front", seed=2, lily=0.45, duck=0.25, pts=[     # у переднего края справа
        (2.8, -5.95), (3.3, -5.45), (4.4, -5.3), (5.7, -5.45), (6.4, -5.95), (6.2, -6.55), (5.2, -6.8), (4.0, -6.85),
        (3.1, -6.55)]),
    dict(name="back", seed=3, lily=0.45, duck=0.25, pts=[      # сзади за серединой
        (0.9, 6.2), (1.7, 5.75), (2.9, 5.75), (3.75, 6.3), (3.85, 7.3), (3.2, 8.1), (2.0, 8.3), (1.0, 7.8), (0.6, 7.0)]),
    dict(name="back_right", seed=4, lily=0.4, duck=0.35, pts=[  # сзади справа, мостки
        (8.6, 7.0), (9.4, 6.4), (10.6, 6.5), (11.5, 7.1), (11.6, 8.2), (10.9, 9.2), (9.7, 9.4), (8.8, 8.8), (8.4, 7.9)]),
    dict(name="duck", seed=5, lily=0.1, duck=1.0, pts=[       # сзади слева: затянут ряской
        (-6.9, 7.0), (-6.3, 6.35), (-5.0, 6.05), (-3.8, 6.2), (-3.4, 6.8), (-3.9, 7.6), (-5.2, 8.0), (-6.4, 7.8)]),
    dict(name="puddle", seed=6, lily=0.0, duck=1.0, pts=[      # лужа у факела гоблинов
        (9.95, 0.2), (10.3, -0.35), (11.0, -0.4), (11.35, 0.15), (11.1, 0.8), (10.4, 0.95)]),
]
# берег неровный: сплайн по точкам контура + шум по нормали
POND_LINES = [T.wobble(T.closed_spline(T.ccw(p["pts"]), 0.3), 0.16, 0.9, p["seed"]) for p in PONDS]
POND_BOX = [(min(p.x for p in L) - 1.5, max(p.x for p in L) + 1.5, min(p.y for p in L) - 1.5, max(p.y for p in L) + 1.5)
            for L in POND_LINES]
POND_D = 0.62                        # глубина низины пруда (м)


def pond_dist(x, y):
    """Наибольшая знаковая глубина захода в пруды: > 0 — внутри контура воды, < 0 — снаружи (м)."""
    best = -9.0
    for L, (x0, x1, y0, y1) in zip(POND_LINES, POND_BOX):
        if x0 <= x <= x1 and y0 <= y <= y1:
            best = max(best, T.signed_dist(x, y, L))
    return best


def pond_sink(x, y):
    """Низина пруда; у кромки плато (ближе 0.6 м к контуру острова) — не проседает: бровка держит обрыв."""
    d = pond_dist(x, y)
    if d < -0.6:
        return 0.0
    k = T.smoothstep(0.2, 0.6, T.dist_to_outline(x, y, _ISLAND)) if T.point_in_poly(x, y, _ISLAND) else 0.0
    return POND_D * T.smoothstep(-0.6, 0.75, d) * k


def pond_points():
    """Вершины травы по берегам: кольца вдоль контуров — берег спускается к воде ровно, без зубцов сетки."""
    pts = []
    for L in POND_LINES:
        for off in (-0.62, -0.32, 0.0, 0.3, 0.7):
            ring = T.offset_outline(L, -off) if off else L
            step = 2 if off > 0.5 else 1
            for p in ring[::step]:
                pts.append((p.x, p.y))
    return pts


def grass_patches(x, y):
    """Трава темнеет к воде и пятнами (сырые места), местами светлеет (сухие кочки) — пятнистая, как на макете."""
    d = pond_dist(x, y)
    k = T.smoothstep(-1.6, -0.2, d) if d > -1.6 else 0.0
    n = T.noise.noise(Vector((x * 0.21, y * 0.21, 7.7)))
    m = T.noise.noise(Vector((x * 0.33, y * 0.33, 2.9)))
    return -0.28 * k - 0.22 * T.smoothstep(0.1, 0.45, n) + 0.16 * T.smoothstep(0.15, 0.5, m)


def terraces():
    t = T.Terrace("arena", CTRL, z=0.0, bottom=-3.0, seed=11, wob=0.2, flat=board_flat, grid=0.72, hills=0.0,
                  grass=(0.3, 0.86), shelves=(0.42, 0.72), fade=FADE, deep=DEEP, ramp=GRASS, sink=pond_sink,
                  extra_pts=pond_points(), patches=grass_patches)
    return {"arena": t}


OCCLUDERS = {}
FLOORS = {}

# ---------------------------------------------------------------------------------------
# своё у окружения в рельефе: вода прудов, ряска, ил по берегам и у поля, пятна яркого мха,
# мох-сосульки и корни по кромке обрыва
# ---------------------------------------------------------------------------------------
MUD = [   # грязь, где ходят: (центр, полуоси, поворот, сид)
    ((-6.0, -5.55), (1.6, 0.45), -3.0, 21), ((-0.6, 5.55), (2.2, 0.5), 2.0, 22), ((10.45, -2.6), (0.6, 1.3), 8.0, 23),
    ((-10.0, 3.2), (0.55, 0.95), 10.0, 24), ((7.6, -5.55), (1.1, 0.42), -8.0, 25), ((5.9, 5.45), (1.0, 0.4), 6.0, 26),
    ((-9.9, -5.4), (0.75, 0.42), 20.0, 27), ((10.6, 3.0), (0.45, 0.8), -5.0, 28),
]
MOSS = []  # пятна яркого мха на земле (сейчас нет: плоские диски читались дырами; яркие акценты — на воде)


def _blob(c, r, rot, seed, k=15, rag=0.25):
    """Пятно с рваным краем: эллипс r (полуоси), повёрнутый на rot, радиус гуляет на ±rag."""
    rng = random.Random(seed * 31 + 7)
    pts = []
    ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    for i in range(k):
        ang = math.tau * i / k + rng.uniform(-0.12, 0.12)
        rr = rng.uniform(1.0 - rag, 1.0 + rag * 0.6)
        x, y = math.cos(ang) * r[0] * rr, math.sin(ang) * r[1] * rr
        pts.append((c[0] + x * ca - y * sa, c[1] + x * sa + y * ca))
    return T.closed_spline(T.ccw(pts), 0.3)


def duckweed(V, a, L, z, cover, rng):
    """Ряска на воде: плоские многоугольники-островки у берега, гуще к краю; cover — доля берега под ряской."""
    if cover <= 0:
        return 0
    bm, uvl = a.bm, a.uv
    xs = [p.x for p in L]
    ys = [p.y for p in L]
    area = abs(T.signed_area(L))
    n = int(area * cover * 4.0)
    made = 0
    off = rng.uniform(0, 50)
    for _ in range(n * 6):
        if made >= n:
            break
        x, y = rng.uniform(min(xs), max(xs)), rng.uniform(min(ys), max(ys))
        d = T.signed_dist(x, y, L)
        if d < 0.12:
            continue
        edge = 1.0 - min(1.0, d / 1.4)                      # у берега — гуще
        k = 0.5 * (T.noise.noise(Vector((x * 0.8, y * 0.8, off))) + 1.0)
        if rng.random() > edge * 0.8 + k * cover * 0.6:
            continue
        r = rng.uniform(0.06, 0.2) * (0.6 + edge)
        m = rng.randint(5, 7)
        ang0 = rng.uniform(0, math.tau)
        zz = z + 0.006 + rng.uniform(0, 0.004)
        c = bm.verts.new((x, y, zz))
        ring = [bm.verts.new((x + math.cos(ang0 + math.tau * i / m) * r * rng.uniform(0.7, 1.15),
                              y + math.sin(ang0 + math.tau * i / m) * r * rng.uniform(0.7, 1.15), zz))
                for i in range(m)]
        uv = V.SW_UV["leaf_light" if rng.random() < 0.7 else "blade"]
        for i in range(m):
            try:
                f = bm.faces.new((c, ring[i], ring[(i + 1) % m]))
            except ValueError:
                continue
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
            for l in f.loops:
                l[uvl].uv = uv
        made += 1
    return made


def terrain_extra(V, TR, ts, assets):
    t = ts["arena"]
    rng = random.Random(77)
    # вода прудов: контур на 12 см шире линии берега — край воды уходит под откос
    n_w = 0
    for k, L in enumerate(POND_LINES):
        n_w += TR.water_poly(assets["water"], TR.offset_outline(L, 0.12), WATER_Z, tile=3.2, rot=17.0 * k)
    # ряска на воде
    n_d = 0
    for p, L in zip(PONDS, POND_LINES):
        n_d += duckweed(V, assets["ground"], L, WATER_Z, p["duck"], rng)
    # ил по берегам: кольцо поверх травы откоса (часть под водой не видна)
    for k, L in enumerate(POND_LINES):
        outer = [p + n * (0.45 + 0.25 * T.noise.noise(Vector((p.x * 0.9, p.y * 0.9, k * 3.3))))
                 for p, n in zip(L, TR.edge_normals(L))]
        outer = TR.closed_spline([(p.x, p.y) for p in outer[::2]], 0.3)
        TR.decal(assets["ground"], t, outer, "dirt",
                 lambda x, y, r: 0.35 + 0.45 * min(1.0, max(0.0, 0.55 - r) / 0.55), lift=0.028, grid=0.4)
    for c, r, rot, sd in MUD:
        TR.decal(assets["ground"], t, _blob(c, r, rot, sd), "dirt",
                 lambda x, y, rr: 0.62 + 0.3 * min(1.0, rr / 0.45), lift=0.022, grid=0.5)
    for c, r, rot, sd in MOSS:
        TR.decal(assets["ground"], t, _blob(c, r, rot, sd), None, None, lift=0.03, grid=0.5, swatch="leaf_light")
    # мох и корни с кромки обрыва: свисают с дёрна, там, где кромку видно из камеры боя
    n_m = moss_drapes(TR, t, assets["cliffs"])
    print("swamp: water %d tris in %d ponds, duckweed %d, mud %d, moss %d, drapes %d"
          % (n_w, len(POND_LINES), n_d, len(MUD), len(MOSS), n_m))


def moss_drapes(TR, t, a):
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
        s += rng.uniform(0.28, 0.55)
        if not TR.in_view(P.x, P.y, 1.0) or N.y > 0.6:          # задний край из камеры не виден
            continue
        top = P + N * rng.uniform(0.0, 0.08) + Vector((0, 0, -0.14))
        u = rng.random()
        if u < 0.1:                                              # корень
            SA._strand(a, top, rng.uniform(0.8, 1.8), rng.uniform(0.08, 0.11), 0.08, "bark_dark", rng, kink=0.08)
        else:
            Lh = rng.uniform(0.5, 1.3) if rng.random() < 0.55 else rng.uniform(1.4, 3.2)
            SA.drape(a, top, N, Lh, rng.uniform(0.45, 1.0), rng.choice(["moss", "moss", "sod", "sod_dark", "sod_dark"]),
                     rng, taper=0.7)
        made += 1
    return made


# ---------------------------------------------------------------------------------------
# расстановка
# ---------------------------------------------------------------------------------------
PIERS = [   # мостки: ломаная от берега над водой (секции по 2 м), верх настила — над водой
    [(-12.15, 0.3), (-13.7, -0.75), (-14.5, -2.4)],          # через левый пруд к кромке
    [(9.3, 5.1), (10.35, 6.85)],                              # в задний правый пруд
]


def _seg_dist(x, y, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
    return math.hypot(ax + dx * t - x, ay + dy * t - y)


def pier_dist(x, y):
    return min(_seg_dist(x, y, a, b) for P in PIERS for a, b in zip(P, P[1:]))


def scatter_ok(tname, x, y, r):
    if pond_dist(x, y) > -(r + 0.25):
        return False
    if pier_dist(x, y) < 0.9 + r:
        return False
    for c, (rx, ry), _, _ in MOSS:                       # пятна мха — без россыпи поверх
        if math.hypot((x - c[0]) / (rx + r), (y - c[1]) / (ry + r)) < 1.0:
            return False
    return True


# рогоз по макету: кучки у прудов и вдоль заднего края (точки — центры кучек)
CATTAILS = [
    (-14.95, -2.25), (-14.55, -3.5), (-14.35, 4.7), (-14.0, 2.6), (-13.75, 7.45), (-11.35, 1.15), (-11.25, -4.95),
    (-12.95, -5.3),
    (2.45, -6.35), (6.75, -5.95), (6.6, -5.05),
    (0.35, 6.95), (1.05, 8.55), (3.2, 8.55), (4.15, 7.95),
    (8.25, 8.5), (11.85, 7.55), (11.3, 9.4),
    (-7.15, 7.75), (-6.75, 8.45), (-2.35, 8.45), (-1.05, 8.3), (0.15, 8.75), (5.6, 9.05), (7.2, 9.2),
]


def placements():
    P = []

    def add(asset, x, y, rz=0.0, s=1.0, on="arena", z=None, tilt=(0.0, 0.0), zabs=None):
        P.append(dict(asset=asset, x=x, y=y, rz=rz, s=s, on=on, z=z, tilt=tilt, zabs=zabs))

    rng = random.Random(909)
    # --- лагерь игрока (слева): знамя у пруда, факел и столб с черепом у заднего края, сухое дерево
    add("Prop_Banner_Blue", -11.9, 2.6, -10)
    add("Prop_Torch", -8.75, 5.9, 0)
    add("Swamp_SkullPost", -7.4, 6.25, -15, 1.35)
    add("Swamp_DeadTree", -12.05, 4.65, 205, 1.0)          # корни — в воду пруда
    add("Swamp_Willow_B", -9.9, 7.65, 40, 0.8)
    add("Prop_Barrel", -10.2, 5.85, 25)
    add("Tree_Stump", -9.45, 5.25, 30, 1.05)
    # --- лагерь гоблинов (справа): знамя, факел у лужи, большое дерево, навес с бочками
    add("Prop_Banner_Red", 11.25, 3.35, 10)
    add("Prop_Torch", 11.75, 1.45, 0)
    add("Swamp_Willow_A", 12.9, -1.0, 150, 1.05)
    add("Swamp_Awning", 11.85, -4.15, -90)
    add("Prop_Barrel", 9.75, -3.75, 0)
    add("Prop_Barrel", 9.6, -4.85, 35)
    add("Prop_Barrel", 10.15, -5.0, 70)
    add("Prop_Sack", 10.5, -5.85, -20)
    add("Swamp_Willow_B", 12.6, 5.75, 120, 0.9)
    add("Swamp_Willow_B", 8.3, 9.75, 260, 0.75)
    # --- мостки: секции по 2 м вдоль ломаных
    for pts in PIERS:
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            L = math.hypot(x1 - x0, y1 - y0)
            n = max(1, int(round(L / SA.BOARD_L)))
            ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
            for k in range(n):
                f = (k + 0.5) / n
                add("Swamp_Boardwalk", x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, ang, zabs=WATER_Z + 0.3)
    # --- коряги, камни, пни по краям
    add("Swamp_Snag_A", -14.95, 1.15, 100, 0.95)
    add("Swamp_Snag_A", 6.8, 9.15, 170, 0.8)
    add("Swamp_Rock_A", -2.15, -6.15, 20, 0.85)
    add("Swamp_Rock_B", -8.9, -5.75, 0, 1.1)
    add("Swamp_Rock_B", 8.35, -6.55, 140, 1.2)
    add("Swamp_Rock_A", 14.2, 2.2, 70, 0.9)
    add("Swamp_Rock_B", -13.9, 5.4, 50, 1.0)
    add("Tree_Stump", 13.6, 4.4, 110, 0.95)
    add("Bush_B", 14.1, -2.9, 30, 0.9)
    add("Bush_A", -13.6, -0.6, 80, 0.85)
    add("Swamp_Stake", 2.95, 6.55, 0, 1.0, zabs=WATER_Z - 0.05)
    add("Swamp_Stake", -13.05, -3.7, 40, 0.9, zabs=WATER_Z - 0.05)
    # --- рогоз кучками
    for k, (x, y) in enumerate(CATTAILS):
        add("Swamp_Cattail_B" if k % 3 == 0 else "Swamp_Cattail_A", x, y, rng.uniform(0, 360), rng.uniform(0.9, 1.15))
        add("Swamp_Reeds_A", x + rng.uniform(-0.45, 0.45), y + rng.uniform(-0.35, 0.35), rng.uniform(0, 360),
            rng.uniform(0.8, 1.1))
    # --- в прудах: кувшинки, кочки, ил; по бровке у края — камни
    for p, L in zip(PONDS, POND_LINES):
        area = abs(T.signed_area(L))
        xs = [q.x for q in L]
        ys = [q.y for q in L]
        for j in range(int(round(area * p["lily"] / 1.2))):
            for _ in range(40):
                x, y = rng.uniform(min(xs), max(xs)), rng.uniform(min(ys), max(ys))
                if T.signed_dist(x, y, L) > 0.5 and pier_dist(x, y) > 0.9:
                    break
            else:
                continue
            u = rng.random()
            asset = "Swamp_LilyPad_C" if u < 0.3 else ("Swamp_LilyPad_B" if u < 0.45 else "Swamp_LilyPad_A")
            add(asset, x, y, rng.uniform(0, 360), rng.uniform(0.85, 1.15), zabs=WATER_Z + 0.004)
        if p["name"] in ("left", "back_right", "back"):
            for j in range(2):
                for _ in range(40):
                    x, y = rng.uniform(min(xs), max(xs)), rng.uniform(min(ys), max(ys))
                    if T.signed_dist(x, y, L) > 0.45 and pier_dist(x, y) > 1.0:
                        break
                else:
                    continue
                add("Swamp_Hummock_A" if j == 0 else "Swamp_Mud_A", x, y, rng.uniform(0, 360), rng.uniform(0.8, 1.1),
                    zabs=WATER_Z - 0.05)
        # камни по бровке со стороны кромки обрыва (как бордюр пруда на макете)
        if p["name"] in ("left", "front"):
            nr = T.edge_normals(L)
            for k in range(0, len(L), 3):
                q, nv = L[k], nr[k]
                c = q + nv * 0.3
                if T.dist_to_outline(c.x, c.y, _ISLAND) > 1.4 or B.dist_to_board(c.x, c.y) < 1.2:
                    continue
                if pier_dist(c.x, c.y) < 1.0:
                    continue
                add("Swamp_Rock_B", c.x, c.y, rng.uniform(0, 360), rng.uniform(0.7, 1.0))
    return P


_ISLAND = T.closed_spline(T.ccw(CTRL), 0.45)

# заборы: жерди (Prop_Fence, 1 м) — у поля спереди и у задних прудов; верёвочный — вдоль левого пруда
FENCES = [
    ("arena", [(-6.4, -5.35), (-4.5, -5.45), (-2.6, -5.5)]),
    ("arena", [(4.3, 5.85), (4.6, 7.1), (5.6, 8.05)]),
    ("arena", [(-3.15, 7.3), (-1.85, 7.75)]),
    dict(on="arena", pts=[(-10.55, 1.0), (-10.85, -0.85), (-10.3, -2.35)], asset="Swamp_RopeFence", seg=SA.ROPE_SEG,
         end="Swamp_RopePost"),
]

ROADS = []
RIVER = None
WATER = None
FALLS = []
BRIDGE, MILL_AT = Mw.BRIDGE, Mw.MILL_AT

# россыпь: (терраса, [(ассет, вес)], плотность на м², шкала, радиус занятости)
FOREST = [
    ("arena", [("Bush_A", 1.0), ("Bush_B", 1.0), ("Swamp_Rock_A", 0.5), ("Swamp_Cattail_A", 1.0)],
     0.008, (0.75, 1.0), 0.6),
]
GROUND = [
    ("arena", [("Swamp_Reeds_A", 3.0), ("Env_Tuft_A", 2.0), ("Env_Tuft_B", 1.5), ("Swamp_Rock_B", 0.25),
               ("Swamp_Mud_A", 0.25)], 0.12, (0.8, 1.2), 0.24),
]
RIM_TUFTS = [("arena", 0.5)]

# ---------------------------------------------------------------------------------------
# небо: облака под и вокруг острова, дальние островки — в верхних углах кадра боя и в нижних углах
# ---------------------------------------------------------------------------------------
CLOUDS = dict(items=[("Env_CloudPuff_A", 3), ("Env_CloudPuff_B", 2), ("Env_CloudPuff_C", 2)],
              center=(0.0, 1.0),
              layers=[
                  dict(ring=(1.5, 9.0), n=16, z=(-13.0, -8.5), scale=(0.9, 1.5), box=(-42.0, -10.0, -20.0, 12.0)),
                  dict(ring=(1.5, 9.0), n=12, z=(-13.0, -8.5), scale=(0.9, 1.5), box=(10.0, 42.0, -20.0, 12.0)),
                  dict(ring=(-1.5, 7.0), n=28, z=(-20.0, -12.0), scale=(1.4, 2.4), box=(-42.0, 42.0, -20.0, 16.0)),
                  dict(ring=(8.0, 34.0), n=22, z=(-15.0, -8.0), scale=(1.3, 2.4), box=(-52.0, 52.0, -40.0, 2.0)),
                  # за задним краем: низкие облака между островом и дальними островками
                  dict(ring=(3.0, 26.0), n=18, z=(-9.0, -4.0), scale=(1.3, 2.2), box=(-40.0, 40.0, 10.0, 40.0)),
              ])
# (центр, высота травы, радиус, сид, деревьев)
DISTANT = [
    ((-18.6, 14.6), -2.6, 3.2, 41, 3),
    ((1.5, 31.0), 2.4, 5.2, 42, 5),
    ((18.4, 14.0), -2.2, 3.0, 43, 2),
    ((-17.5, -10.8), -5.0, 1.3, 44, 1),
    ((19.0, -12.2), -6.6, 1.2, 45, 0),
]

# ---------------------------------------------------------------------------------------
# look-dev превью: «туманное зеленоватое утро» — дымка цвета неба дальше поля, приглушённая
# насыщенность, мягкое солнце (в игре — LOOK_VARIANTS["swamp"] в build_isle.py)
# ---------------------------------------------------------------------------------------
LOOKDEV = dict(
    fog="#aab8bf",
    ambient=(0.42, 0.47, 0.48),
    view="AgX", look="AgX - Base Contrast", exposure=0.05,
    sun_angle=12.0, sun_energy=4.2, world=1.0,
    shots={
        "hero": dict(fog=(25.0, 66.0, 0.8), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "wide": dict(fog=(25.0, 66.0, 0.8), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
        "ipad": dict(fog=(27.0, 70.0, 0.8), dof=(0.0, 0.0, 41.0, 60.0, 6.0)),
        "mock": dict(fog=(25.0, 66.0, 0.8), dof=(0.0, 0.0, 38.0, 56.0, 6.0)),
    },
    bloom=dict(threshold=0.85, strength=0.32, size=0.6),
    lift=(1.0, 1.01, 1.02), gamma=(1.0, 1.0, 1.0), gain=(1.02, 1.02, 0.97),
    saturation=1.08, vignette=0.18,
    sky_bounce=False,
    water=dict(tint=(1.0, 1.0, 1.0), falls_tint=(1.0, 1.0, 1.0), shallow="#6f9a88", shallow_k=0.0,
               swamp_tint=(0.96, 1.0, 0.98)),
    grass_noise=dict(scale=0.22, dark=(0.86, 0.88, 0.84), light=(1.07, 1.05, 0.95)),
    palettes=["Vitaria_Palette", "Vitaria_Palette_Swamp"],
)
