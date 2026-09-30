"""
Колония на парящем острове, который растёт блоками 5 x 5 (Colony_Isle): сцена, этапы игры, кадры.

  python build_isle.py --blend vitaria_kit.blend [--render] [--stages start,mid,max] [--res 100]
                       [--samples 40] [--prev DIR] [--tag T] [--save_as PATH]

Композиция и параметры — vitaria_colony/isle_grid.py. Строится всё сразу (64 столба-блока, дикая
земля и облачная полка для каждой ячейки, обвязка каждой стороны блока, острова-спутники, небо), а
этап игры — это только видимость и сдвиг объектов: так же, как это будет делать игра.
Ассеты и look-dev — общие с build_colony.py.
"""
import bpy, os, sys, math, random, importlib, zlib
from mathutils import Matrix, Vector, Euler, noise

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

SCENE = "Colony_Isle"
PFX = "CI_"
GROUPS = ["Terrain", "Water", "Blocks", "Wild", "Cover", "Rim", "Decor", "Nature", "Props", "Backdrop", "Sky",
          "Rig", "Preview_start", "Preview_mid", "Preview_max"]
SIDES = {"S": (0, -1), "E": (1, 0), "N": (0, 1), "W": (-1, 0)}
# солнце низко (32° над горизонтом) и тёплое: длинные тени читаются с высоты камеры; направление в осях Blender
SUN_DIR = (-0.69468, 0.48642, -0.52992)
SUN_COLOR = (1.0, 0.92, 0.8)
GAME_AMBIENT = [0.55, 0.62, 0.76]              # sRGB, RenderSettings.ambientLight острова
# sRGB: фон камеры, туман и дымка острова в игре. Синее, чем LOOKDEV["fog"]: Tonemapping Neutral в URP гасит
# синеву сильнее AgX, с этим цветом небо в кадре игры выходит (168, 187, 200)
GAME_SKY = [0.64, 0.78, 0.94]


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    o = {"blend": None, "render": False, "stages": "start,mid,max", "res": 100, "samples": 40, "prev": None,
         "tag": "", "save_as": None, "export": False}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--blend", "--stages", "--prev", "--tag", "--save_as") and i + 1 < len(argv):
            o[a[2:]] = argv[i + 1]
            i += 1
        elif a in ("--res", "--samples") and i + 1 < len(argv):
            o[a[2:]] = int(argv[i + 1])
            i += 1
        elif a in ("--render", "--export"):
            o[a[2:]] = True
        i += 1
    return o


def reset_scene():
    old = bpy.data.scenes.get(SCENE)
    if old is not None:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.scenes.remove(old)
    for c in list(bpy.data.collections):
        if c.name.startswith(PFX):
            bpy.data.collections.remove(c)
    scn = bpy.data.scenes.new(SCENE)
    scn.unit_settings.system = "METRIC"
    C = {}
    for g in GROUPS:
        c = bpy.data.collections.new(PFX + g)
        scn.collection.children.link(c)
        C[g] = c
    return scn, C


def sstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def lawn_t(x, y):
    """Тон газона на рампе травы — только по мировым координатам: у соседних блоков шов не виден."""
    n1 = noise.noise(Vector((x * 0.13, y * 0.13, 15.2)))
    n2 = noise.noise(Vector((x * 0.47, y * 0.47, 19.1)))
    t = 0.6 + 0.26 * n1 + 0.1 * n2
    k = noise.noise(Vector((x * 0.27 + 3.1, y * 0.27 - 7.7, 1.9))) + \
        0.35 * noise.noise(Vector((x * 0.61 - 2.3, y * 0.61 + 5.2, 4.4)))
    t += -0.3 * sstep(0.08, 0.4, k) + 0.16 * sstep(0.14, 0.44, -k)
    return max(0.0, min(1.0, t))


def square(x0, y0, x1, y1, step=0.5):
    pts = []
    for (ax, ay), (bx, by) in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
        n = max(1, int(round(math.hypot(bx - ax, by - ay) / step)))
        for k in range(n):
            t = k / n
            pts.append(Vector((ax + (bx - ax) * t, ay + (by - ay) * t, 0.0)))
    return pts


# =========================================================================================
# блок: газон, свес дёрна, пласты до своей глубины
# =========================================================================================
def build_block(V, TR, BC, G, bx, by, mat):
    x0, y0, x1, y1 = G.block_rect(bx, by)
    t = TR.Terrace("blk", [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z=0.0, seed=11, wob=0.0,
                   flat=lambda x, y: 1.0, grid=0.9, grass=(0.36, 0.84), ramp=G.GRASS)
    pts = square(x0, y0, x1, y1, 0.5)
    t.outline, t.normals, t.tangents = pts, TR.edge_normals(pts), TR.tangents(pts)
    t.bbox = (x0, x1, y0, y1)
    t.grass_t = lambda x, y, rim=None: lawn_t(x, y)
    name = "%sBlock_%d_%d" % (PFX, bx, by)
    a = V.Asset(name)
    t.build_top(a)
    # свеса дёрна у блока нет: наружу блок всегда выходит через уступ (build_skirt) со своим свесом
    top = BC.mesh_from_asset(a, name, mat)
    bot = G.block_bottom(bx, by)

    def bottom_of(P):
        return bot + G.JAG * noise.noise(Vector((P.x * 0.21, P.y * 0.21, 1.7)))

    # стены — отдельно по сторонам: игра прячет те, что закрыты купленным соседом (почти все внутренние)
    walls = {}
    for side in SIDES:
        (ax, ay), (bx_, by_) = edge_of(G, bx, by, side)
        A, B = Vector((ax, ay, 0.0)), Vector((bx_, by_, 0.0))
        m = int(round((B - A).length / 0.7))
        run = [A.lerp(B, k / m) for k in range(m + 1)]
        n = Vector(SIDES[side] + (0,))
        wn = "%sWall_%d_%d_%s" % (PFX, bx, by, side)
        w = V.Asset(wn)
        TR.strata_bands(w, run, [n.copy() for _ in run], False, [-0.02] * len(run), G.BLOCK_DEEP, G.FADE, bottom_of)
        walls[side] = BC.mesh_from_asset(w, wn, mat)
    return top, walls


# =========================================================================================
# уступ вокруг владений: у каждой стороны блока, за которой нет купленного блока, — полоса дикой
# земли на 0.45 м ниже газона шириной 0.6–2.4 м (ширина — шум по мировым координатам, поэтому
# соседние полосы сходятся в одной точке), со своим свесом дёрна и пластами; на наружных углах —
# скругление. Квадрат владений так читается островом, а не плитой.
# =========================================================================================
WALL_STEP = 2.0                 # стена блока нужна, только если сосед мельче больше чем на столько (м)
LEDGE = -0.02                   # вровень с газоном: остров — одна земля, стройка — квадрат внутри неё
SIDE_Z = {"S": 0.0, "E": -0.006, "N": -0.012, "W": -0.018}    # в вогнутом углу полосы перекрываются: разные высоты
CORNERS = {"SE": ("S", "E"), "NE": ("E", "N"), "NW": ("N", "W"), "SW": ("W", "S")}


def skirt_w(x, y):
    n = 0.75 * noise.noise(Vector((x * 0.33, y * 0.33, 7.3))) + 0.25 * noise.noise(Vector((x * 0.9, y * 0.9, 2.1)))
    return max(0.8, min(3.2, 1.9 + 1.7 * n))


def edge_of(G, bx, by, side):
    x0, y0, x1, y1 = G.block_rect(bx, by)
    return {"S": ((x0, y0), (x1, y0)), "E": ((x1, y0), (x1, y1)), "N": ((x1, y1), (x0, y1)),
            "W": ((x0, y1), (x0, y0))}[side]


def open_normals(pts):
    out, m = [], len(pts)
    for i in range(m):
        d = (pts[min(m - 1, i + 1)] - pts[max(0, i - 1)]).normalized()
        out.append(Vector((d.y, -d.x, 0.0)))
    return out


def lip_open(V, a, Q, N, z, ramp):
    """Свес дёрна вдоль открытой ломаной Q (нормали N наружу) — как Terrace.build_lip."""
    prof = [(0.0, 0.0), (0.15, -0.08), (0.2, -0.24), (0.07, -0.45), (-0.55, -0.5)]
    cols = [V.ramp_uv(ramp, 0.9), V.SW_UV["sod"], V.SW_UV["sod_dark"], V.SW_UV["soil_dark"]]
    rings = [[a.bm.verts.new((q.x + n.x * dn, q.y + n.y * dn, z + dz)) for q, n in zip(Q, N)] for dn, dz in prof]
    for k in range(len(rings) - 1):
        for i in range(len(Q) - 1):
            try:
                f = a.bm.faces.new((rings[k][i], rings[k + 1][i], rings[k + 1][i + 1], rings[k][i + 1]))
            except ValueError:
                continue
            for l in f.loops:
                l[a.uv].uv = cols[k]
            f.normal_update()


def _ledge_top(V, TR, G, a, poly, z, dist, dark=0.05):
    """Верх уступа: у кромки блока вровень с газоном (без ступеньки и шва), за 1.2 м — на z; тон
    чуть темнее газона и тоже плавно (dist(x, y) — расстояние от кромки блока)."""
    t = TR.Terrace("ledge", [(p.x, p.y) for p in poly], z=z, seed=11, wob=0.0, flat=lambda x, y: 1.0, grid=0.55,
                   grass=(0.3, 0.8), ramp=G.GRASS)
    pts = TR.ccw(poly)
    t.outline, t.normals, t.tangents = pts, TR.edge_normals(pts), TR.tangents(pts)
    xs, ys = [p.x for p in pts], [p.y for p in pts]
    t.bbox = (min(xs), max(xs), min(ys), max(ys))
    t.height = lambda x, y, rim=None: z * sstep(0.0, 1.2, dist(x, y))
    t.edge_height = lambda x, y: z * sstep(0.0, 1.2, dist(x, y))
    t.grass_t = lambda x, y, rim=None: max(0.0, lawn_t(x, y) - dark * sstep(0.1, 0.9, dist(x, y)))
    t.build_top(a)


def _ledge_edge(V, TR, G, a, Q, z, bx, by, N=None):
    N = N or open_normals(Q)
    lip_open(V, a, Q, N, z, G.GRASS)
    bot = G.block_bottom(bx, by)

    def bottom_of(P):
        return bot + G.JAG * noise.noise(Vector((P.x * 0.21, P.y * 0.21, 1.7)))

    TR.strata_bands(a, Q, N, False, [z - 0.36] * len(Q), G.BLOCK_DEEP, G.FADE, bottom_of)


def build_skirt(V, TR, BC, G, bx, by, side, mat):
    (ax, ay), (bx_, by_) = edge_of(G, bx, by, side)
    A, B = Vector((ax, ay, 0.0)), Vector((bx_, by_, 0.0))
    n = Vector(SIDES[side] + (0,))
    m = int(round((B - A).length / 0.5))
    P = [A.lerp(B, k / m) for k in range(m + 1)]
    Q = [p + n * skirt_w(p.x, p.y) for p in P]
    z = LEDGE + SIDE_Z[side]
    name = "%sSkirt_%d_%d_%s" % (PFX, bx, by, side)
    a = V.Asset(name)
    _ledge_top(V, TR, G, a, P + list(reversed(Q)), z, lambda x, y: (Vector((x, y, 0.0)) - A).dot(n))
    # нормаль стены — нормаль стороны: у соседних полос одной стороны пласты в общей точке совпадают
    _ledge_edge(V, TR, G, a, Q, z, bx, by, [n.copy() for _ in Q])
    return BC.mesh_from_asset(a, name, mat)


def corner_geom(G, bx, by, ck, steps=6):
    s1, s2 = CORNERS[ck]
    x0, y0, x1, y1 = G.block_rect(bx, by)
    c = Vector({"SE": (x1, y0, 0.0), "NE": (x1, y1, 0.0), "NW": (x0, y1, 0.0), "SW": (x0, y0, 0.0)}[ck])
    n1, n2 = Vector(SIDES[s1] + (0,)), Vector(SIDES[s2] + (0,))
    w = skirt_w(c.x, c.y)
    arc = [c + (n1 * math.cos(k / steps * math.pi / 2) + n2 * math.sin(k / steps * math.pi / 2)) * w
           for k in range(steps + 1)]
    return c, arc, min(SIDE_Z[s1], SIDE_Z[s2])


def build_corner(V, TR, BC, G, bx, by, ck, mat):
    c, arc, dz = corner_geom(G, bx, by, ck)
    z = LEDGE + dz - 0.006
    name = "%sCorner_%d_%d_%s" % (PFX, bx, by, ck)
    a = V.Asset(name)
    s1, s2 = CORNERS[ck]
    n1, n2 = Vector(SIDES[s1] + (0,)), Vector(SIDES[s2] + (0,))

    def dist(x, y):                         # до ближайшей кромки блока (угол — вне обеих сторон)
        p = Vector((x, y, 0.0)) - c
        return max(p.dot(n1), p.dot(n2), 0.0)

    _ledge_top(V, TR, G, a, [c] + arc, z, dist)
    _ledge_edge(V, TR, G, a, arc, z, bx, by, [(q - c).normalized() for q in arc])
    return BC.mesh_from_asset(a, name, mat)


def skirt_dressing(BA, G, meshes, C, bx, by, side, key):
    """Кусты, камни, цветы на уступе и трава, свисающая с его кромки."""
    (ax, ay), (bx_, by_) = edge_of(G, bx, by, side)
    A, B = Vector((ax, ay, 0.0)), Vector((bx_, by_, 0.0))
    n = Vector(SIDES[side] + (0,))
    z = LEDGE + SIDE_Z[side]
    rng = random.Random(zlib.crc32(key.encode()))
    for k in range(rng.randint(5, 9)):                            # трава по уступу: дикая земля, не газон
        u = rng.uniform(0.02, 0.98)
        p = A.lerp(B, u)
        q = p + n * rng.uniform(0.2, skirt_w(p.x, p.y) - 0.15)
        tag(BA.place(meshes, C["Rim"], rng.choice(["Env_Tuft_A", "Env_Tuft_B", "Env_Tuft_B", "Env_Flowers_A"]), q.x, q.y,
                     z - 0.02, rng.uniform(0, 360), rng.uniform(1.0, 1.4)), "skirt", key)
    for k in range(rng.randint(2, 4)):
        u = rng.uniform(0.1, 0.9)
        p = A.lerp(B, u)
        w = skirt_w(p.x, p.y)
        if w < 1.0:
            continue
        q = p + n * rng.uniform(0.45, w - 0.45)
        asset = rng.choice(["Bush_A", "Bush_B", "Bush_B", "Bush_Berry", "Rock_Small", "Rock_Medium", "Env_FlowerPatch_A",
                            "Env_FlowerPatch_A", "Env_FlowerPatch_B"])
        s = rng.uniform(0.6, 0.95) if asset.startswith(("Bush", "Rock")) else rng.uniform(1.1, 1.6)
        tag(BA.place(meshes, C["Rim"], asset, q.x, q.y, z - 0.02, rng.uniform(0, 360), s,
                     (rng.uniform(-4, 4), rng.uniform(-4, 4))), "skirt", key)
    axis = Vector((-n.y, n.x, 0.0))
    for u in frange(0.05, 1.0, 0.12):
        if rng.random() < 0.35:
            continue
        p = A.lerp(B, u)
        q = p + n * (skirt_w(p.x, p.y) + 0.06)
        o = BA.place(meshes, C["Rim"], rng.choice(["Env_Tuft_A", "Env_Tuft_B", "Env_Tuft_B"]), q.x, q.y, z - 0.05, 0.0,
                     rng.uniform(1.0, 1.5))
        o.matrix_world = Matrix.Translation(o.location) @ Matrix.Rotation(math.radians(rng.uniform(25, 45)), 4, axis) @ \
            Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z") @ Matrix.Scale(o.scale.x, 4)
        tag(o, "skirt", key)


def tag(o, key, val):
    o[key] = val
    return o


def build_scene(V, BA, BC, VC, TR, meshes, mat, wmat):
    mods = VC.load(reload=True, layout="isle_grid")
    G, F = mods["layout"], mods["fields"]
    importlib.reload(TR)
    TR.VIEW_BOX = None
    scn, C = reset_scene()

    # ------------------------------------------------------------------ блоки
    blocks = {}
    for bx in range(G.GRID):
        for by in range(G.GRID):
            top, walls = build_block(V, TR, BC, G, bx, by, mat)
            o = bpy.data.objects.new(top.name, top)
            C["Blocks"].objects.link(o)
            blocks[bx, by] = tag(o, "block", "%d,%d" % (bx, by))
            for side, me in walls.items():
                w = bpy.data.objects.new(me.name, me)
                C["Blocks"].objects.link(w)
                tag(w, "wall", "%d,%d,%s" % (bx, by, side))
    print("blocks %d" % len(blocks))

    # ------------------------------------------------------------------ дикая земля, облака, обвязка, клевер
    def natural(asset):
        return asset.startswith(("Tree_", "Bush_", "Rock_"))

    for (bx, by) in blocks:
        x0, y0, x1, y1 = G.block_rect(bx, by)
        rng = random.Random(bx * 131 + by * 977 + 5)
        key = "%d,%d" % (bx, by)
        if not G.is_start(bx, by):
            kind = G.biome(bx, by)
            items, (n0, n1), srange = G.WILD[kind]
            names, wts = [a for a, _ in items], [w for _, w in items]
            sp = BA.Spots(1.0)
            got, tries, want = 0, 0, rng.randint(n0, n1)
            while got < want and tries < 300:
                tries += 1
                x, y = rng.uniform(x0 + 0.45, x1 - 0.45), rng.uniform(y0 + 0.45, y1 - 0.45)
                asset = rng.choices(names, wts)[0]
                s = rng.uniform(*srange)
                r = BA.radius_of(asset, s) if natural(asset) else 0.3 * s
                if not sp.free(x, y, r * 0.72):
                    continue
                o = BA.place(meshes, C["Wild"], asset, x, y, -0.02, rng.uniform(0, 360), s,
                             (rng.uniform(-3, 3), rng.uniform(-3, 3)))
                tag(o, "wild", key)
                sp.add(x, y, r * 0.72)
                got += 1
            for k in range(rng.randint(8, 12)):                  # трава и цветы между деревьями
                x, y = rng.uniform(x0 + 0.3, x1 - 0.3), rng.uniform(y0 + 0.3, y1 - 0.3)
                if not sp.free(x, y, 0.12):
                    continue
                asset = rng.choice(["Env_Tuft_A", "Env_Tuft_B", "Env_Tuft_B", "Env_Flowers_A"])
                tag(BA.place(meshes, C["Wild"], asset, x, y, -0.02, rng.uniform(0, 360), rng.uniform(0.9, 1.3)), "wild", key)
            # облачная полка над некупленной ячейкой
            cv = G.COVER
            cn, cw = [a for a, _ in cv["items"]], [w for _, w in cv["items"]]
            n = rng.randint(*cv["n"])
            for k in range(n):
                u = (k + 0.5) / n
                x = x0 + BLK_JIT(rng, u, x1 - x0)
                y = y0 + rng.uniform(0.2, 0.8) * (y1 - y0)
                o = BA.place(meshes, C["Cover"], rng.choices(cn, cw)[0], x, y, rng.uniform(*cv["z"]),
                             rng.uniform(0, 360), rng.uniform(*cv["scale"]))
                tag(o, "cover", key)
        # клевер и камешки на расчищенном блоке (в игре под зданием их закрывает подложка)
        drng = random.Random(bx * 71 + by * 37 + 3)
        for c in range(1):
            cx, cy = drng.uniform(x0 + 1.0, x1 - 1.0), drng.uniform(y0 + 1.0, y1 - 1.0)
            for k in range(drng.randint(2, 4)):
                x, y = cx + drng.gauss(0, 0.5), cy + drng.gauss(0, 0.4)
                if not (x0 + 0.2 < x < x1 - 0.2 and y0 + 0.2 < y < y1 - 0.2):
                    continue
                asset = drng.choice(["Env_Clover_A", "Env_Clover_A", "Env_Clover_B", "Env_Pebbles_Flat"])
                tag(BA.place(meshes, C["Decor"], asset, x, y, -0.004, drng.uniform(0, 360), drng.uniform(0.9, 1.5)),
                    "decor", key)

    # ------------------------------------------------------------------ уступ-обвязка вокруг владений
    n_sk = n_co = 0
    for (bx, by) in blocks:
        for side in SIDES:
            if all(G.is_start(bx + dx, by + dy) for dx, dy in [SIDES[side]]) and G.is_start(bx, by):
                continue                                  # сторона между стартовыми блоками: наружу не выходит никогда
            me = build_skirt(V, TR, BC, G, bx, by, side, mat)
            o = bpy.data.objects.new(me.name, me)
            C["Rim"].objects.link(o)
            key = "%d,%d,%s" % (bx, by, side)
            tag(o, "skirt", key)
            skirt_dressing(BA, G, meshes, C, bx, by, side, key)
            n_sk += 1
        for ck, (s1, s2) in CORNERS.items():
            me = build_corner(V, TR, BC, G, bx, by, ck, mat)
            o = bpy.data.objects.new(me.name, me)
            C["Rim"].objects.link(o)
            tag(o, "corner", "%d,%d,%s" % (bx, by, ck))
            n_co += 1
    print("skirts %d, corners %d" % (n_sk, n_co))

    # ------------------------------------------------------------------ острова-спутники
    ts = {}
    for name, (ctrl, z, depth, seed, hills) in G.SATELLITES.items():
        xs, ys = [p[0] for p in ctrl], [p[1] for p in ctrl]
        r = ((max(xs) - min(xs)) + (max(ys) - min(ys))) / 4.0
        deep = dict(bottom=z - depth, jag=0.28 * depth, strata=(0.06, 0.14, 0.24, 0.36, 0.5, 0.66, 0.82),
                    batter=-0.8 * r / 8.0, amp=min(0.4, 0.1 * r), wave=min(0.5, 0.12 * r), lean=0.15, crack=0.18)
        ts[name] = TR.Terrace(name, ctrl, z=z, bottom=z - depth, seed=seed, wob=0.2 if r > 3 else 0.1,
                              step=0.6 if r > 3 else 0.4, grid=1.0 if r > 3 else 0.6, hills=hills, grass=(0.34, 0.84),
                              ramp=G.GRASS, col_w=(1.0, 2.2), deep=deep, fade=G.FADE)
    ground, cliffs = V.Asset(G.NAME + "_Sat_Ground"), V.Asset(G.NAME + "_Sat_Cliffs")
    for t in ts.values():
        t.build_top(ground)
        t.build_lip(ground)
        t.build_cliff(cliffs, boulders=False)
    falls, foam = V.Asset(G.NAME + "_Falls"), V.Asset(G.NAME + "_Foam")
    for k, fd in enumerate(G.SAT_FALLS):
        t = ts[fd["src"]]
        tx, ty = fd["toward"]
        i = min(range(len(t.outline)), key=lambda i: (t.outline[i].x - tx) ** 2 + (t.outline[i].y - ty) ** 2)
        P, N = t.outline[i], t.normals[i]
        z_top = t.height(P.x - N.x * 0.8, P.y - N.y * 0.8)
        TR.waterfall(falls, (P.x, P.y), (N.x, N.y), fd["width"], z_top, fd["z_bot"], seed=k, reach=fd["reach"],
                     stream=fd["stream"], segs=fd["segs"])
        print("satellite waterfall at (%.1f, %.1f), top %.2f" % (P.x, P.y, z_top))
    fields = V.Asset(G.NAME + "_Sat_Fields")
    for k, (tname, x, y, w, d, ang, kind) in enumerate(G.SAT_FIELDS):
        F.field_patch(fields, ts[tname], x, y, w, d, ang, kind, seed=k + 1)
    for name, a in ((G.NAME + "_Sat_Ground", ground), (G.NAME + "_Sat_Cliffs", cliffs), (G.NAME + "_Sat_Fields", fields)):
        me = BC.mesh_from_asset(a, name, mat)
        meshes[name] = me
        C["Terrain"].objects.link(bpy.data.objects.new(name, me))
    me = BC.mesh_from_asset(falls, G.NAME + "_Falls", wmat["falls"])
    meshes[me.name] = me
    C["Water"].objects.link(bpy.data.objects.new(me.name, me))

    spots = BA.Spots()
    for p in G.sat_placements():
        t = ts[p["on"]]
        z = t.height(p["x"], p["y"])
        grp = "Backdrop" if p["asset"].startswith(("Bld_", "Env_")) else "Props"
        BA.place(meshes, C[grp], p["asset"], p["x"], p["y"], z, p["rz"], p["s"])
        spots.add(p["x"], p["y"], 1.4 if p["asset"].startswith("Bld_") else 0.5)
    for (tname, x, y, w, d, ang, kind) in G.SAT_FIELDS:
        spots.add(x, y, max(w, d) * 0.62)
    rng = random.Random(7)
    for tname, items, dens, srange in G.SAT_FOREST:
        t = ts[tname]
        x0, x1, y0, y1 = t.bbox
        names, wts = [a for a, _ in items], [w for _, w in items]
        want, got, tries = int((x1 - x0) * (y1 - y0) * dens), 0, 0
        while got < want and tries < want * 30:
            tries += 1
            x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
            asset = rng.choices(names, wts)[0]
            s = rng.uniform(*srange)
            r = BA.radius_of(asset, s)
            if not t.contains(x, y, 0.5 * r + 0.2) or not spots.free(x, y, r * 0.65):
                continue
            BA.place(meshes, C["Nature"], asset, x, y, t.height(x, y) - 0.03, rng.uniform(0, 360), s,
                     (rng.uniform(-3, 3), rng.uniform(-3, 3)))
            spots.add(x, y, r * 0.65)
            got += 1
    for tname, dens in G.SAT_GROUND:
        t = ts[tname]
        x0, x1, y0, y1 = t.bbox
        want = int((x1 - x0) * (y1 - y0) * dens)
        for k in range(want * 4):
            if want <= 0:
                break
            x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
            if not t.contains(x, y, 0.35) or not spots.free(x, y, 0.14):
                continue
            asset = rng.choice(["Env_Tuft_A", "Env_Tuft_B", "Env_Flowers_A", "Env_FlowerPatch_A"])
            BA.place(meshes, C["Nature"], asset, x, y, t.height(x, y) - 0.02, rng.uniform(0, 360), rng.uniform(0.9, 1.3))
            spots.add(x, y, 0.14)
            want -= 1

    # ------------------------------------------------------------------ небо: облака вокруг сетки и спутников, дальние острова
    class _Outline:
        def __init__(self, pts):
            self.outline = pts
    edge = [_Outline([Vector((x, y, 0)) for x, y in ((-G.HALF, -G.HALF), (G.HALF, -G.HALF), (G.HALF, G.HALF),
                                                      (-G.HALF, G.HALF))])]
    sky_ts = {"grid": edge[0], **ts}
    BC.build_sky(V, BA, TR, G, meshes, C, mat, sky_ts)

    # ------------------------------------------------------------------ здания этапов (в игру не идут)
    import vitaria_buildings as VB
    occupied = {}
    for stage, st in G.STAGES.items():
        coll = C["Preview_" + stage]
        cells = {}
        for (mesh, cx, cy) in st["buildings"]:
            w, d = VB.FOOTPRINT[mesh]
            for i in range(cx, cx + w):
                for j in range(cy, cy + d):
                    b = (i // int(G.BLOCK), j // int(G.BLOCK))
                    if (i, j) in cells:
                        print("!! %s: %s overlaps %s at %s" % (stage, mesh, cells[i, j], (i, j)))
                    if b not in st["owned"] or b in st["wild"] or b == st.get("rising"):
                        print("!! %s: %s on block %s that is not buildable" % (stage, mesh, b))
                    cells[i, j] = mesh
            x, y = cx + w / 2.0 - G.HALF, cy + d / 2.0 - G.HALF
            if mesh not in meshes and bpy.data.meshes.get(mesh):
                meshes[mesh] = bpy.data.meshes[mesh]
            BA.place(meshes, coll, mesh, x, y, 0.0, 0.0, VB.KIT_SCALE, name="Preview_" + mesh)
            pad = "Env_Footprint_%dx%d" % (w, d)
            if pad in meshes:
                BA.place(meshes, coll, pad, x, y, 0.0, 0.0, 1.0, name="Preview_" + pad)
        occupied[stage] = len(st["buildings"])
    print("stage buildings:", occupied)

    # ------------------------------------------------------------------ камеры этапов, солнце, мир
    cams = {}
    for stage in G.STAGES:
        pos, tgt, d = G.stage_camera(stage)
        cd = bpy.data.cameras.new(PFX + "Cam_" + stage)
        cd.sensor_fit, cd.sensor_height = "VERTICAL", 24.0
        cd.lens = 12.0 / math.tan(math.radians(45.0) / 2)
        cd.clip_start, cd.clip_end = 0.3, 500
        co = bpy.data.objects.new(PFX + "Cam_" + stage, cd)
        co.location = Vector(pos)
        co.rotation_euler = (Vector(tgt) - co.location).to_track_quat("-Z", "Y").to_euler()
        C["Rig"].objects.link(co)
        cams[stage] = co
        print("camera %-5s distance %.1f at (%.1f, %.1f, %.1f)" % ((stage, d) + tuple(pos)))
    scn.camera = cams["start"]
    sd = bpy.data.lights.new(PFX + "Sun", "SUN")
    sd.energy, sd.color = 5.0, SUN_COLOR
    so = bpy.data.objects.new(PFX + "Sun", sd)
    so.rotation_euler = Vector(SUN_DIR).normalized().to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(so)
    world = bpy.data.worlds.get("AM_World") or bpy.data.worlds.new("AM_World")
    world.use_nodes = True
    scn.world = world
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.use_denoising = True
    scn.cycles.max_bounces = 4
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGB"
    return scn, cams, so, blocks


def frange(a, b, step):
    out, x = [], a
    while x < b:
        out.append(x)
        x += step
    return out


def BLK_JIT(rng, u, size):
    return (u + rng.uniform(-0.12, 0.12)) * size


# =========================================================================================
# экспорт в Unity: Models/Isle (FBX) и Layout/isle_layout.json
# =========================================================================================
PIECE_SIDE = {"S": 0, "E": 1, "N": 2, "W": 3}
PIECE_CORNER = {"SE": 0, "NE": 1, "NW": 2, "SW": 3}
# дикая земля: крупное — отдельными объектами (игра убирает их по одному при расчистке), мелочь — одним мешем
WILD_INSTANCE = ("Tree_", "Bush_", "Rock_", "Res_")
# ветер в дикой земле, как у деревьев колонии (layout.SWAY): градусы, Гц; пень не качается
WILD_SWAY = {"Tree_": (1.4, 0.32), "Bush_": (2.4, 0.45)}
STATIC_INSTANCE = ("Bld_",)                    # на спутниках — модели кита как есть (трубы, крыши)


def _restore(scn):
    for o in scn.objects:
        if "_base" in o:
            x, y, z, sc = o["_base"]
            o.location = (x, y, z)
            o.scale = (sc, sc, sc)


def _merged(V, BC, name, objs, mat):
    a = V.Asset(name)
    for o in objs:
        a.add_mesh(o.data, BC.obj_matrix(o))
    return BC.mesh_from_asset(a, name, mat)


def _export_objects(BA, pairs, path):
    """FBX из нескольких объектов: pairs — [(имя объекта в FBX, меш)]. Меши — в пространстве колонии."""
    scn = bpy.context.scene
    tmp = []
    for name, me in pairs:
        if bpy.data.objects.get(name) is not None:
            raise RuntimeError("object name taken, FBX child would be renamed: " + name)
        o = bpy.data.objects.new(name, me)
        scn.collection.objects.link(o)
        tmp.append(o)
    bpy.context.view_layer.update()
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    for o in tmp:
        o.select_set(True)
    bpy.context.view_layer.objects.active = tmp[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, **BA.FBX_KW)
    for o in tmp:
        bpy.data.objects.remove(o, do_unlink=True)


def _tris(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def export_isle(scn, G, V, BA, BC, mat, root):
    import json
    _restore(scn)
    unity = os.path.join(root, "Unity", "Assets", "Vitaria")
    mdir = os.path.join(unity, "Models", "Isle")
    items = []
    blocks = {}
    tris = {"blocks": 0, "satellites": 0, "sky": 0}

    def item(o, group, block="", role=""):
        p, q, sc = BA.to_unity(BC.obj_matrix(o))
        it = {"name": o.name, "asset": o.data.name, "group": group, "p": p, "r": q, "s": sc}
        if block:
            it["block"], it["role"] = block, role
        return it

    # ------------------------------------------------------------ блоки: куски по ключу
    parts = {}                                  # (bx, by) -> {кусок: [объекты]}
    for o in scn.objects:
        if o.type != "MESH":
            continue
        if "block" in o:
            key, piece = o["block"], "Top"
        elif "wall" in o:
            bx, by, side = o["wall"].split(",")
            key, piece = "%s,%s" % (bx, by), "Wall_" + side
        elif "skirt" in o:
            bx, by, side = o["skirt"].split(",")
            key, piece = "%s,%s" % (bx, by), "Ledge_" + side
        elif "corner" in o:
            bx, by, ck = o["corner"].split(",")
            key, piece = "%s,%s" % (bx, by), "Corner_" + ck
        elif "decor" in o:
            key, piece = o["decor"], "Decor"
        elif "wild" in o:
            if o.data.name.startswith(WILD_INSTANCE):
                it = item(o, "Blocks", o["wild"], "wild")
                for pre, (deg, hz) in WILD_SWAY.items():
                    if o.data.name.startswith(pre) and o.data.name != "Tree_Stump":
                        wr = random.Random(zlib.crc32(o.name.encode()))
                        it["sway"] = [round(deg * wr.uniform(0.8, 1.2), 2), round(hz * wr.uniform(0.85, 1.15), 3),
                                      round(wr.random(), 3)]
                items.append(it)
                continue
            key, piece = o["wild"], "WildGround"
        elif "cover" in o:
            items.append(item(o, "Blocks", o["cover"], "cover"))
            continue
        else:
            continue
        b = tuple(int(v) for v in key.split(","))
        parts.setdefault(b, {}).setdefault(piece, []).append(o)
    for (bx, by), pieces in sorted(parts.items()):
        pairs = []
        for piece in sorted(pieces):
            me = _merged(V, BC, "Isle_%d_%d_%s" % (bx, by, piece), pieces[piece], mat)
            pairs.append((piece, me))
            tris["blocks"] += _tris(me)
        model = "Isle_Block_%d_%d" % (bx, by)
        _export_objects(BA, pairs, os.path.join(mdir, "Blocks", model + ".fbx"))
        for _, me in pairs:
            bpy.data.meshes.remove(me)
        blocks[bx, by] = {"bx": bx, "by": by, "model": model, "bottom": round(G.block_bottom(bx, by), 3),
                          "biome": "" if G.is_start(bx, by) else G.biome(bx, by), "pieces": sorted(pieces)}
    print("isle blocks exported: %d FBX, %d tris in all pieces" % (len(blocks), tris["blocks"]))

    # ------------------------------------------------------------ спутники и небо
    coll = {g: bpy.data.collections.get(PFX + g) for g in ("Terrain", "Water", "Nature", "Props", "Backdrop", "Sky")}
    static_meshes = []
    for o in list(coll["Terrain"].objects) + list(coll["Water"].objects):
        if o.type == "MESH":
            static_meshes.append((o.data.name, o.data))
            it = item(o, "Satellites")
            if o.data.name.endswith("_Falls"):
                it["scroll"] = 0.6
            items.append(it)
    scatter = []
    for g in ("Nature", "Props", "Backdrop"):
        for o in coll[g].objects:
            if o.type != "MESH":
                continue
            if o.data.name.startswith(STATIC_INSTANCE):
                items.append(item(o, "Satellites"))
            else:
                scatter.append(o)
    me = _merged(V, BC, G.NAME + "_Sat_Scatter", scatter, mat)
    static_meshes.append((me.name, me))
    items.append({"name": me.name, "asset": me.name, "group": "Satellites", "p": [0.0, 0.0, 0.0],
                  "r": BA.to_unity(Matrix.Identity(4))[1], "s": [1.0, 1.0, 1.0]})
    sky = [o for o in coll["Sky"].objects if o.type == "MESH"]
    me = _merged(V, BC, G.NAME + "_Sky", sky, mat)
    static_meshes.append((me.name, me))
    items.append({"name": me.name, "asset": me.name, "group": "Sky", "p": [0.0, 0.0, 0.0],
                  "r": BA.to_unity(Matrix.Identity(4))[1], "s": [1.0, 1.0, 1.0]})
    for name, me in static_meshes:
        BA.export_mesh(me, os.path.join(mdir, name + ".fbx"))
        tris["sky" if name.endswith("_Sky") else "satellites"] += _tris(me)
    # облака над пустыми ячейками — модели кита (игра двигает их, когда блок поднимается)
    for n in ("Env_CloudPuff_A", "Env_CloudPuff_B", "Env_CloudPuff_C"):
        BA.export_mesh(bpy.data.meshes[n], os.path.join(mdir, "Sky", n + ".fbx"))
    print("isle static: %s" % ", ".join("%s %d" % (n, _tris(m)) for n, m in static_meshes))

    # ------------------------------------------------------------ раскладка
    sun_fwd = BA.to_unity_vec(Vector(SUN_DIR).normalized())
    stages = []
    for name, st in G.STAGES.items():
        stages.append({"name": name, "owned": [by * G.GRID + bx for bx, by in st["owned"]],
                       "wild": [by * G.GRID + bx for bx, by in st["wild"]],
                       "rising": (st["rising"][1] * G.GRID + st["rising"][0]) if st.get("rising") else -1})
    sx, sy, sw, sh = G.START
    layout = {
        "name": G.NAME,
        "kitVersion": V.KIT_VERSION,
        "grid": {"cells": G.CELLS, "cellSize": 1.0, "blockSize": G.BLOCK, "blocksPerSide": G.GRID,
                 "start": [sx, sy, sw, sh],
                 "note": "начало координат — середина сетки 40x40: MapToWorld(20, 20); блок (bx, by) — клетки "
                         "bx*5..bx*5+4, by*5..by*5+4; индекс блока = by * 8 + bx. Куски блоков лежат в "
                         "Models/Isle/Blocks/Isle_Block_<bx>_<by>.fbx в координатах острова: ставить в 0 "
                         "с поворотом pieceRotation"},
        "pieceRotation": BA.to_unity(Matrix.Identity(4))[1],
        "wallStep": WALL_STEP,
        "rise": {"depth": 8.0, "seconds": 2.2},
        "blocks": [blocks[k] for k in sorted(blocks, key=lambda b: (b[1], b[0]))],
        "objects": items,
        "stages": stages,
        "camera": {"fov": 45.0, "pitch": 45.0, "offsetX": 1.5, "distancePerSide": 1.16},
        "sun": {"forward": sun_fwd, "color": list(SUN_COLOR), "intensity": 1.8, "shadowStrength": 0.92},
        # плоский амбиент Unity темнее и холоднее мира Blender: с ним тени не заливаются и уходят в синеву
        "ambient": {"color": GAME_AMBIENT},
        "background": GAME_SKY,
        "fog": {"color": GAME_SKY, "startPerDistance": 0.85, "endPerDistance": 3.0},
        "post": {"exposure": -0.1, "saturation": 4.0, "contrast": 8.0, "temperature": 0.0,
                 "bloomThreshold": 0.9, "bloomIntensity": 0.35,
                 "bloomScatter": 0.6, "dofStartPerDistance": 1.4, "dofEndPerDistance": 2.0, "dofMaxRadius": 1.0,
                 "vignette": 0.0, "vignetteSmoothness": 0.45, "lift": [1.0, 1.0, 1.005, 0.0],
                 "gamma": [1.0, 1.0, 1.0, 0.0], "gain": [1.05, 1.02, 0.97, 0.0]},
        # дымка (IslandHaze в игре): ниже газона всё тонет по высоте в цвете неба — столбы, облака пустых ячеек,
        # корни спутников; края кадра светлеют вместо тёмной виньетки. Глубины — метры вниз от газона
        "haze": {"color": GAME_SKY, "startDepth": 2.0, "fullDepth": 16.0, "opacity": 1.0,
                 "edgeColor": [0.96, 0.98, 1.0], "edgeIntensity": 0.55, "edgeStart": 0.35, "edgeFull": 1.1,
                 "edgeTop": 0.25},
        "tris": tris,
    }
    lay = os.path.join(unity, "Layout", "isle_layout.json")
    os.makedirs(os.path.dirname(lay), exist_ok=True)
    with open(lay, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, separators=(",", ":"))
    n_wild = sum(1 for i in items if i.get("role") == "wild")
    n_cover = sum(1 for i in items if i.get("role") == "cover")
    print("isle layout: %d blocks, %d objects (%d wild, %d cover), %s -> %s" % (
        len(blocks), len(items), n_wild, n_cover, tris, lay))
    return layout


# =========================================================================================
# этап игры: видимость и сдвиг
# =========================================================================================
def apply_stage(scn, G, stage):
    st = G.STAGES[stage]
    owned = set(st["owned"])
    wild = set(st["wild"])
    rising = st.get("rising")
    rise = st.get("rise", 0.0)

    def key_b(s):
        bx, by = s.split(",")[:2]
        return int(bx), int(by)

    for o in scn.objects:
        if "_base" not in o:
            o["_base"] = [o.location.x, o.location.y, o.location.z, o.scale.x]
        bx0, by0, bz0, bs0 = o["_base"]
        o.location = (bx0, by0, bz0)
        o.scale = (bs0, bs0, bs0)
        hide = False
        if "block" in o:
            b = key_b(o["block"])
            hide = b not in owned
            if b == rising:
                o.location.z = bz0 + rise
        elif "wild" in o:
            b = key_b(o["wild"])
            hide = not (b in wild or b == rising)
            if b == rising:
                o.location.z = bz0 + rise
        elif "decor" in o:
            b = key_b(o["decor"])
            hide = b not in owned or b in wild or b == rising
        elif "cover" in o:
            b = key_b(o["cover"])
            hide = b in owned and b != rising
            if b == rising:                       # облака над поднимающимся блоком расходятся
                cx, cy = G.block_center(*b)
                d = Vector((bx0 - cx, by0 - cy, 0.0))
                d = d.normalized() if d.length > 1e-3 else Vector((1, 0, 0))
                o.location = (bx0 + d.x * 3.2, by0 + d.y * 3.2, bz0 - 0.6)
                o.scale = (bs0 * 0.75,) * 3
        elif "wall" in o:                           # стена блока: видна под более мелким соседом и у поднимающегося
            bx, by, side = o["wall"].split(",")
            b = (int(bx), int(by))
            nx, ny = SIDES[side]
            nb = (b[0] + nx, b[1] + ny)
            hide = not (b in owned and (b == rising or (nb in owned and (nb == rising or
                        G.block_bottom(*nb) > G.block_bottom(*b) + WALL_STEP))))
            if b == rising:
                o.location.z = bz0 + rise
        elif "skirt" in o:                          # уступ стороны: блок есть, за стороной — пусто
            bx, by, side = o["skirt"].split(",")
            b = (int(bx), int(by))
            nx, ny = SIDES[side]
            hide = not (b in owned and (b[0] + nx, b[1] + ny) not in owned)
            if b == rising:
                o.location.z = bz0 + rise
        elif "corner" in o:                         # наружный угол: обе стороны и диагональ пусты
            bx, by, ck = o["corner"].split(",")
            b = (int(bx), int(by))
            (ax, ay), (cx_, cy_) = SIDES[CORNERS[ck][0]], SIDES[CORNERS[ck][1]]
            hide = not (b in owned and (b[0] + ax, b[1] + ay) not in owned and (b[0] + cx_, b[1] + cy_) not in owned
                        and (b[0] + ax + cx_, b[1] + ay + cy_) not in owned)
            if b == rising:
                o.location.z = bz0 + rise
        o.hide_render = hide
        o.hide_viewport = hide
    for s in G.STAGES:
        c = bpy.data.collections.get(PFX + "Preview_" + s)
        if c is not None:
            c.hide_render = s != stage
            c.hide_viewport = s != stage


def render_stages(scn, cams, G, BC, stages, prev, res, samples, tag=""):
    os.makedirs(prev, exist_ok=True)
    out = []
    for stage in [s for s in stages.split(",") if s]:
        apply_stage(scn, G, stage)
        scn.camera = cams[stage]
        scn.render.resolution_x, scn.render.resolution_y = 1920, 1080
        scn.render.resolution_percentage = res
        scn.cycles.samples = samples
        BC.lookdev_shot(scn, G.LOOKDEV, stage, 1080 * res / 100.0)
        path = os.path.join(prev, "colony_isle_%s%s.png" % (stage, tag))
        scn.render.filepath = path
        bpy.ops.render.render(write_still=True, scene=scn.name)
        print("rendered", path)
        out.append(path)
    return out


def main():
    o = parse_args()
    if o["blend"]:
        bpy.ops.wm.open_mainfile(filepath=o["blend"])
    import build_vitaria as V
    V = importlib.reload(V)
    import build_arena as BA
    BA = importlib.reload(BA)
    import build_colony as BC
    BC = importlib.reload(BC)
    import vitaria_arena as VA
    VA = importlib.reload(VA)
    VA.load(reload=True)
    import vitaria_arena.terrain as TR
    import vitaria_colony as VC
    VC = importlib.reload(VC)
    root = os.path.normpath(os.path.join(HERE, ".."))
    tex_dir = os.path.join(root, "Unity", "Assets", "Vitaria", "Textures")
    mat = BA.palette_material(V, tex_dir)
    wmat = BA.water_materials(VA.load()["terrain"], tex_dir)
    kit_root = bpy.data.collections.get("Vitaria_Kit")
    if kit_root is None:
        kit_root = bpy.data.collections.new("Vitaria_Kit")
        bpy.context.scene.collection.children.link(kit_root)
    ref_dir = BA.find_ref(HERE, None)
    BA.BACKDROP = list(BA.BACKDROP) + [m for m in BC.EXTRA_BACKDROP if m not in BA.BACKDROP]
    meshes = BA.build_assets(V, VA, ref_dir, mat, kit_root, False)
    for cat, n, title, fn in VC.asset_entries(reload=True):
        meshes[n] = BA.build_mesh(V, n, fn, mat)
    for n in [m.name for m in bpy.data.meshes if m.name.startswith(("Bld_", "Prop_", "Res_", "Tree_", "Rock_", "Bush_"))]:
        meshes.setdefault(n, bpy.data.meshes[n])
    BC.colony_backdrop(V, BA, meshes)
    BC.player_buildings(V, BA, ref_dir, mat, meshes)
    scn, cams, sun, blocks = build_scene(V, BA, BC, VC, TR, meshes, mat, wmat)
    G = VC.load(layout="isle_grid")["layout"]
    BC.lookdev_scene(scn, G.LOOKDEV, sun)
    tris = 0
    for ob in scn.objects:
        if ob.type == "MESH":
            ob.data.calc_loop_triangles()
            tris += len(ob.data.loop_triangles)
    print("scene objects %d, tris (all stages) %d" % (len(scn.objects), tris))
    if o["save_as"]:
        bpy.ops.wm.save_as_mainfile(filepath=o["save_as"], copy=True)
        print("saved copy", o["save_as"])
    if o["export"]:
        export_isle(scn, G, V, BA, BC, mat, root)
    if o["render"]:
        prev = o["prev"] or os.path.join(root, "Previews")
        render_stages(scn, cams, G, BC, o["stages"], prev, o["res"], o["samples"], o["tag"])
    print("done")


if __name__ == "__main__":
    main()
