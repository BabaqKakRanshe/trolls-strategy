"""
Локация 1 (колония) «Долина» — окружение поля стройки в стиле арены: сцена, превью, экспорт в Unity.

Запуск (как build_arena.py):
  Консоль:   blender -b vitaria_kit.blend --python build_colony.py [-- --render --shots hero,wide]
  Через bpy: python build_colony.py --blend <путь к vitaria_kit.blend> [--render] [--export]

Что делает:
  1. ассеты арены и фона (как build_arena) + новые ассеты колонии (vitaria_colony/assets.py);
  2. сцена Colony_Meadow в том же .blend: рельеф, река, водопад, поля, лес, фон, реквизит, камера колонии.
     Здания поля в коллекции CM_Preview только для превью: в игре их ставит BuildingVisualsManager;
  3. превью (--render) -> Previews/colony_*.png;
  4. FBX (--export) -> Unity/Assets/Vitaria/Models/Colony (+ /Backdrop, /Meadow) и раскладка
     Unity/Assets/Vitaria/Layout/colony_meadow_layout.json в координатах Unity относительно середины
     поля (MapToWorld(7, 7, 0)), мировые оси.

Оси: пространство колонии X на восток, Y на север (от камеры), Z вверх. В Unity: X -> X, Y -> Z, Z -> Y
(та же пересадка to_unity, что у арены: её камера тоже смотрит вдоль +Z).
"""
import bpy, os, sys, json, math, random, importlib
from mathutils import Matrix, Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

SCENE = "Colony_Meadow"
PFX = "CM_"
GROUPS = ["Terrain", "Water", "Backdrop", "Nature", "Props", "FX"]
EXTRA_BACKDROP = ["bld_barn", "bld_keep", "bld_minehoist", "bld_barracks", "bld_warehouse"]
SCROLL = {"Colony_Meadow_River": 0.22, "Colony_Meadow_Falls": 0.6}
SOCKETS = []
RIG = {}                 # дополнительные камеры сцены: {"over": общий план острова}
# фон арены в колонии идёт приглушёнными копиями (<имя>_Muted, крыша roof_old): крыши фона не спорят
# с кровлями зданий поля. Свой фон колонии перекрашивается на месте (он больше нигде не стоит).
MUTED = ["Bld_Cottage", "Bld_House_Timber", "Bld_House_Stone", "Bld_Tower_Round", "Bld_WatchTower"]
COLONY_THEME = {"Bld_Barn": "old", "Bld_MineHoist": "old", "Bld_Keep": "old", "Bld_Watermill": "thatch"}
# россыпь (Scatter) режется на куски по сетке мира: при приближении камеры Unity отсекает лишние
CHUNK_X = (-9.0, 9.0)
CHUNK_Y = (6.0,)
FRAME_ASPECT = 21 / 9          # самый широкий кадр: 16:9 и 4:3 лежат внутри него
PREVIEW_SETS = {
    # (меш, SW-клетка x, y, ширина, глубина) — как префабы Assets/Game/Prefabs/Buildings: модель в масштабе
    # vitaria_buildings.KIT_SCALE по центру следа, под ней подложка Env_Footprint_<w>x<d>
    "start": [("Bld_Market", 9, 4, 3, 2), ("Bld_Warehouse", 9, 10, 3, 3)],
    "grown": [("Bld_Market", 9, 4, 3, 2), ("Bld_Warehouse", 9, 10, 3, 3),
              ("Bld_Mine", 1, 9, 3, 3), ("Bld_LumberCamp", 1, 4, 2, 2),
              ("Bld_Farm", 5, 0, 3, 3), ("Bld_Forge", 5, 6, 2, 2),
              ("Bld_Barracks", 5, 10, 3, 3), ("Bld_Smeltery", 1, 0, 3, 3),
              ("Bld_Tannery", 10, 0, 2, 2), ("Bld_Armory", 3, 6, 2, 2), ("Bld_ShieldWorkshop", 12, 2, 2, 2)],
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    o = {"blend": None, "render": False, "export": False, "save": False, "res": 100, "samples": 48,
         "shots": "hero", "prev": None, "preview": "start", "tag": "", "layout": "layout", "look": False,
         "save_as": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--blend", "--prev", "--shots", "--preview", "--tag", "--layout", "--save_as") and i + 1 < len(argv):
            o[a[2:]] = argv[i + 1]
            i += 1
        elif a in ("--res", "--samples") and i + 1 < len(argv):
            o[a[2:]] = int(argv[i + 1])
            i += 1
        elif a in ("--render", "--export", "--save", "--look"):
            o[a[2:]] = True
        i += 1
    return o


# =========================================================================================
# кадр камеры колонии
# =========================================================================================
def cam_basis(cam):
    C = Vector(cam["position"])
    T = Vector(cam["target"])
    fw = (T - C).normalized()
    rt = fw.cross(Vector((0, 0, 1))).normalized()
    up = rt.cross(fw)
    return C, fw, rt, up


def frame_region(cam, z, aspect=2.4, margin=3.0):
    """След кадра на плоскости z (с запасом под 21:9) — где имеет смысл сажать мелочь."""
    C, fw, rt, up = cam_basis(cam)
    tv = math.tan(math.radians(cam["fov"]) / 2) * 1.12
    th = tv * aspect
    poly = []
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        d = fw + rt * (sx * th) + up * (sy * tv)
        if d.z > -0.05:
            d.z = -0.05
        t = (z - C.z) / d.z
        q = C + d * t
        poly.append(Vector((q.x, q.y, 0)))
    c = sum(poly, Vector()) / 4
    return [c + (q - c) * (1 + margin / max(1e-3, (q - c).length)) for q in poly]


def in_frame(cam, pts, aspect=FRAME_ASPECT, pad=0.02):
    """Попадает ли что-то из точек pts в кадр камеры колонии (исходный кадр: приближение и сдвиг
    камеры в игре остаются внутри него, поэтому всё, что вне, не видно никогда)."""
    C, fw, rt, up = cam_basis(cam)
    tv = math.tan(math.radians(cam["fov"]) / 2)
    xs, ys = [], []
    for p in pts:
        d = Vector(p) - C
        z = d.dot(fw)
        if z <= 0.05:
            return True
        xs.append(d.dot(rt) / z / (tv * aspect))
        ys.append(d.dot(up) / z / tv)
    return max(xs) >= -1 - pad and min(xs) <= 1 + pad and max(ys) >= -1 - pad and min(ys) <= 1 + pad


def obj_matrix(o):
    return o.matrix_world.copy() if o.parent else Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(),
                                                                       o.scale)


_BOX = {}


def obj_corners(o):
    """Углы габарита объекта в мире (габарит меша считается один раз на меш)."""
    me = o.data
    box = _BOX.get(me.name)
    if box is None:
        xs, ys, zs = zip(*[tuple(v.co) for v in me.vertices]) if me.vertices else ((0,), (0,), (0,))
        box = [Vector((x, y, z)) for x in (min(xs), max(xs)) for y in (min(ys), max(ys)) for z in (min(zs), max(zs))]
        _BOX[me.name] = box
    mw = obj_matrix(o)
    return [mw @ c for c in box]


def chunk_of(x, y):
    i = sum(1 for e in CHUNK_X if x >= e)
    j = sum(1 for e in CHUNK_Y if y >= e)
    return "ABCDEFGH"[j * (len(CHUNK_X) + 1) + i]


def flat_dab(a, rng, x, y, z, r, col="foam", n=7):
    """Плоский неровный n-угольник (пена у берега): 5 треугольников вместо шарика."""
    bm, uvl = a.bm, a.uv
    import build_vitaria as V
    a0 = rng.uniform(0, math.tau)
    vs = []
    for k in range(n):
        ang = a0 + math.tau * k / n
        rr = r * rng.uniform(0.6, 1.15)
        vs.append(bm.verts.new((x + math.cos(ang) * rr, y + math.sin(ang) * rr * rng.uniform(0.7, 1.0), z)))
    f = bm.faces.new(vs)
    f.normal_update()
    if f.normal.z < 0:
        f.normal_flip()
    for l in f.loops:
        l[uvl].uv = V.SW_UV[col]


def dist_to_polyline(x, y, pts):
    best = 1e9
    p = Vector((x, y, 0.0))
    for a, b in zip(pts, pts[1:]):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
        best = min(best, (a + ab * t - p).length)
    return best


# =========================================================================================
# сцена
# =========================================================================================
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
    colls = {}
    for g in GROUPS + ["Preview", "Rig", "Sky"]:
        c = bpy.data.collections.new(PFX + g)
        scn.collection.children.link(c)
        colls[g] = c
    return scn, colls


def mesh_from_asset(a, name, mats):
    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    a.bm.to_mesh(me)
    a.bm.free()
    me.materials.clear()
    for m in (mats if isinstance(mats, (list, tuple)) else [mats]):
        me.materials.append(m)
    me.validate()
    return me


def build_scene(V, BA, VC, meshes, mat, wmat, preview="start"):
    mods = VC.load(reload=True)
    M, P, F, CA = mods["layout"], mods["plot"], mods["fields"], mods["assets"]
    import vitaria_arena.terrain as TR
    importlib.reload(TR)
    scn, C = reset_scene()
    rng = random.Random(91)
    SOCKETS.clear()
    TR.VIEW_BOX = M.VIEW_BOX
    N = M.NAME

    # ---------------------------------------------------------------- рельеф
    ts = M.terraces()
    for name, occ in M.OCCLUDERS.items():
        ts[name].occluders = [ts[o].outline for o in occ]
    for name, fl in getattr(M, "FLOORS", {}).items():          # парящий остров: короткие столбы над соседками
        ts[name].floors = [(ts[f].outline, ts[f].z - ts[f].hills) for f in fl]
    ground, cliffs = V.Asset(N + "_Ground"), V.Asset(N + "_Cliffs")
    for t in ts.values():
        t.build_top(ground)
        t.build_lip(ground)
        t.build_cliff(cliffs, boulders=getattr(M, "BOULDERS", True))
    deep = getattr(M, "DEEP", None)
    if deep and deep.get("union"):                 # парящий остров: наружный обрыв одной лентой пластов
        outline, n_loops = TR.build_island_strata(cliffs, list(ts.values()), deep, M.FADE)
        print("island strata: %d outline points, %d contours (1 = no gaps between terraces)" % (len(outline), n_loops))
    roads = V.Asset(N + "_Roads")
    road_pts = []
    for tname, ctrl, half, taper in M.ROADS:
        road_pts += [(p.x, p.y, half) for p in
                     TR.build_road(roads, ctrl, ts[tname], half=half, taper=taper, seed=len(road_pts))]
    for k, (tname, x, y, r) in enumerate(M.ROAD_YARDS):
        F.dirt_yard(roads, ts[tname], x, y, r, seed=k)
        road_pts += [(x, y, r)]
    fields = V.Asset(N + "_Fields")
    for k, (tname, x, y, w, d, ang, kind) in enumerate(M.FIELDS):
        F.field_patch(fields, ts[tname], x, y, w, d, ang, kind, seed=k + 1)

    # ---------------------------------------------------------------- мельница: колесо в воде
    # x мельницы — от настоящей кромки западного берега (контур с шумом), чтобы колесо не ушло в берег
    my, mrz, ms = M.WATERMILL
    near = [p for p in ts["west"].outline if abs(p.y - my) < 0.7 and p.x > -13.5]
    edge_x = max(p.x for p in near) if near else -12.2
    hub_local = Vector(CA.WMILL_HUB) * ms
    mx = edge_x + M.WHEEL_IN_WATER - hub_local.x
    mill = dict(asset="Bld_Watermill", x=mx, y=my, rz=mrz, s=ms, on="west", z=None, tilt=(0.0, 0.0))
    wheel_c = Vector((mx + hub_local.x, my + hub_local.y))
    print("watermill x %.2f (edge %.2f), wheel at (%.2f, %.2f)" % (mx, edge_x, wheel_c.x, wheel_c.y))

    # ---------------------------------------------------------------- вода и пена
    river, falls, foam = V.Asset(N + "_River"), V.Asset(N + "_Falls"), V.Asset(N + "_Foam")
    fs, fl = M.RIVER_FLARE
    TR.river_ribbon(river, M.RIVER, M.RIVER_HALF + 0.35, z=M.RIVER_Z, tile=4.0, seed=3, flare_start=fs,
                    flare_len=fl)
    for k, fd in enumerate(M.FALLS):
        # водопад: кортеж (точка кромки, нормаль, ширина, откуда, вынос, уровень воды внизу) или dict с
        # теми же полями и необязательными z_top (вода сверху — река, а не трава), stream, segs, foam
        if not isinstance(fd, dict):
            fd = dict(zip(("pt", "normal", "width", "src", "reach", "z_bot"), fd))
        pt, nr, width, z_bot = fd["pt"], fd["normal"], fd["width"], fd["z_bot"]
        z_top = fd.get("z_top")
        if z_top is None:
            z_top = ts[fd["src"]].height(pt[0] - nr[0] * 0.6, pt[1] - nr[1] * 0.6)
        end = TR.waterfall(falls, pt, nr, width, z_top, z_bot - 0.05, seed=k, reach=fd["reach"],
                           stream=fd.get("stream", 2.2), segs=fd.get("segs", 12))
        if fd.get("foam", True):
            TR.foam_patch(foam, (end.x, end.y), width * 0.75, seed=k * 3 + 1, n=10, z=z_bot)
            SOCKETS.append(("mist", (end.x, end.y, z_bot + 0.15), round(width, 2)))
    # пена вдоль берегов: пунктир плоских клочков у подножия скал русла
    frng = random.Random(29)
    river_line = TR.open_spline(M.RIVER, 0.5)
    zf = M.RIVER_Z + 0.012
    dabs = 0
    for tname in ("home", "west"):
        t = ts[tname]
        acc = 0.0
        for i, p in enumerate(t.outline):
            q = t.outline[(i + 1) % len(t.outline)]
            acc += (q - p).length
            if acc < 0.32:
                continue
            acc = frng.uniform(-0.1, 0.05)
            if dist_to_polyline(p.x, p.y, river_line) > M.RIVER_HALF + 1.1 or not TR.in_view(p.x, p.y):
                continue
            if frng.random() > 0.7:
                continue
            nv = t.normals[i]
            c = p + nv * frng.uniform(0.18, 0.42)
            flat_dab(foam, frng, c.x, c.y, zf, frng.uniform(0.07, 0.16))
            dabs += 1
    # опора моста: бурун выше по течению и след ниже
    bx, by, brz, bs = M.BRIDGE[1], M.BRIDGE[2], M.BRIDGE[3], M.BRIDGE[4]
    for k in range(9):
        flat_dab(foam, frng, bx + frng.uniform(-0.3, 0.3), by + 1.35 * bs / 0.72 + frng.uniform(-0.05, 0.25), zf,
                 frng.uniform(0.1, 0.2))
    for k in range(14):
        t = k / 13
        spread = 0.15 + 0.5 * t
        flat_dab(foam, frng, bx + frng.choice((-1, 1)) * spread * frng.uniform(0.6, 1.0),
                 by - 1.4 - 1.6 * t, zf, frng.uniform(0.06, 0.14) * (1.1 - 0.5 * t))
    # колесо мельницы: пена там, где лопасти входят в воду, и след вниз по течению
    for k in range(12):
        t = k / 11
        flat_dab(foam, frng, wheel_c.x + frng.uniform(-0.25, 0.25) * (1 + t), wheel_c.y - 0.1 - 1.3 * t, zf,
                 frng.uniform(0.07, 0.17) * (1.15 - 0.5 * t))
    print("bank foam dabs", dabs)
    for name, a in ((N + "_Ground", ground), (N + "_Cliffs", cliffs), (N + "_Roads", roads),
                    (N + "_Fields", fields), (N + "_Foam", foam)):
        me = mesh_from_asset(a, name, mat)
        meshes[name] = me
        C["Terrain"].objects.link(bpy.data.objects.new(name, me))
    for name, a, style in ((N + "_River", river, "lake"), (N + "_Falls", falls, "falls")):
        me = mesh_from_asset(a, name, wmat[style])
        meshes[name] = me
        C["Water"].objects.link(bpy.data.objects.new(name, me))
    # мелководье у берегов: цвет вершин реки (1 у краёв ленты, 0 посередине) — шейдер воды светлеет
    # к берегу. Лента реки — ряды по 5 вершин поперёк (river_ribbon, cols = 4).
    # Пока только в макете острова (LOOKDEV): у Colony_Meadow экспорт в игру не меняется.
    if getattr(M, "LOOKDEV", None):
        me = meshes[N + "_River"]
        ca = me.color_attributes.get("Col") or me.color_attributes.new("Col", "BYTE_COLOR", "POINT")
        shallow = (1.0, 0.25, 0.0, 0.25, 1.0)
        for v in me.vertices:
            s = shallow[v.index % 5]
            ca.data[v.index].color = (s, s, s, 1.0)

    # ---------------------------------------------------------------- расстановка
    spots = BA.Spots()
    for (x, y, h) in road_pts[::2]:
        spots.add(x, y, h + 0.35)
    for (tname, x, y, w, d, ang, kind) in M.FIELDS:          # делянки заняты целиком
        c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        u = -w / 2
        while u <= w / 2 + 1e-6:
            v = -d / 2
            while v <= d / 2 + 1e-6:
                spots.add(x + u * c - v * s, y + u * s + v * c, 0.55)
                v += 0.6
            u += 0.6

    # коридор взгляда на водопад: деревья и кусты здесь закрывали бы главный вид кадра
    (fx0, fy0), flen, frad = M.FALLS_VIEW
    cdir = (Vector((M.CAMERA["position"][0], M.CAMERA["position"][1], 0)) - Vector((fx0, fy0, 0))).normalized()
    k = 0.0
    while k <= flen:
        spots.add(fx0 + cdir.x * k, fy0 + cdir.y * k, frad)
        k += 0.7

    placed = []
    for p in M.placements() + [mill]:
        asset = p["asset"]
        if asset not in meshes:
            print("!! нет ассета", asset)
            continue
        on = p["on"]
        if asset == "Env_Bridge":
            z = ts[on].z + (p["z"] or 0.0)
        elif p["z"] is not None:
            z = ts[on].height(p["x"], p["y"]) + p["z"]
        else:
            z = ts[on].height(p["x"], p["y"])
        grp = "Backdrop" if asset.startswith(("Bld_", "Env_")) else ("Nature" if asset.startswith(BA.NATURE) else "Props")
        o = BA.place(meshes, C[grp], asset, p["x"], p["y"], z - (0.02 if grp == "Nature" else 0.0), p["rz"], p["s"],
                     p["tilt"])
        placed.append(o)
        big = {"Bld_Castle": 4.6, "Bld_Tower_Round": 1.9, "Bld_Cottage": 2.1, "Bld_House_Stone": 2.2,
               "Bld_House_Timber": 2.3, "Bld_WatchTower": 1.6, "Bld_Barn": 2.4, "Bld_Keep": 1.8,
               "Bld_MineHoist": 2.2, "Env_Bridge": 3.0, "Bld_Watermill": 2.6}.get(asset.replace("_Muted", ""))
        spots.add(p["x"], p["y"], big * p["s"] / 0.78 if big else BA.radius_of(asset, p["s"]) + 0.15)
        # водяная мельница: колесо отдельным объектом в точке ступицы, ось колеса вдоль X мельницы
        if asset == "Bld_Watermill" and "Bld_Watermill_Wheel" in meshes:
            hub = Matrix.Rotation(math.radians(p["rz"]), 4, "Z") @ (Vector(CA.WMILL_HUB) * p["s"])
            BA.place(meshes, C["Backdrop"], "Bld_Watermill_Wheel", p["x"] + hub.x, p["y"] + hub.y, z + hub.z,
                     p["rz"] + 90.0, p["s"])
            spots.add(p["x"] + hub.x, p["y"] + hub.y, 0.8)
    # трубы домов фона: верх каждой трубы — сокет дыма
    for o in placed:
        if o.data.name.startswith("Bld_"):
            mw = Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
            for q in BA.CHIMNEYS.get(o.data.name, []):
                w = mw @ q
                SOCKETS.append(("smoke", (w.x, w.y, w.z + 0.08), round(0.7 * o.scale.x, 2)))
    for tname, pts in M.FENCES:
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            L = math.hypot(x1 - x0, y1 - y0)
            n = max(1, int(round(L / 1.0)))
            ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
            for k in range(n):
                t = k / n
                x, y = x0 + (x1 - x0) * (t + 0.5 / n), y0 + (y1 - y0) * (t + 0.5 / n)
                BA.place(meshes, C["Props"], "Prop_Fence", x, y, ts[tname].height(x, y), ang + rng.uniform(-3, 3),
                         tilt=(rng.uniform(-2, 2), rng.uniform(-2, 2)))
                spots.add(x, y, 0.45)

    # ---------------------------------------------------------------- обрыв гряды: лестница, осыпи, кусты
    def edge_station(t, x_target):
        """Точка контура террасы с x ближе всего к x_target среди рёбер, смотрящих к камере (на юг)."""
        best = None
        for i, p in enumerate(t.outline):
            if t.normals[i].y > -0.35:
                continue
            d = abs(p.x - x_target)
            if best is None or d < best[0]:
                best = (d, i)
        return best[1] if best else None

    st = M.STAIRS
    tn = ts[st["terrace"]]
    ci = edge_station(tn, st["x"] + st["run"] / 2)
    if ci is not None and "Prop_CliffStairs" in meshes:
        P0, N0 = tn.outline[ci], tn.normals[ci]
        ang = math.degrees(math.atan2(-N0.x, N0.y)) + 180.0          # +Y лестницы -> в скалу (против нормали)
        tx = Vector((math.cos(math.radians(ang)), math.sin(math.radians(ang)), 0))
        base = P0 + N0 * (st["width"] / 2 + 0.45) - tx * (st["run"] / 2)
        o = BA.place(meshes, C["Props"], "Prop_CliffStairs", base.x, base.y, ts["home"].height(base.x, base.y), ang, 1.0)
        for k in range(6):
            q = base + tx * (k * st["run"] / 5)
            spots.add(q.x, q.y, 0.55)
        print("stairs at (%.2f, %.2f) rz %.1f" % (base.x, base.y, ang))
    sc = M.SCREE
    t = ts[sc["terrace"]]
    home = ts["home"]
    srng = random.Random(57)
    x = sc["x"][0]
    n_scree = 0
    while x < sc["x"][1]:
        i = edge_station(t, x)
        x += srng.uniform(*sc["every"])
        if i is None:
            continue
        P0, N0 = t.outline[i], t.normals[i]
        Tg = Vector((-N0.y, N0.x, 0))
        for k in range(srng.randint(*sc["n"])):
            q = P0 + N0 * srng.uniform(*sc["off"]) + Tg * srng.uniform(-0.6, 0.6)
            asset = "Rock_Medium" if srng.random() < 0.3 else "Rock_Small"
            s = srng.uniform(0.45, 0.75) if asset == "Rock_Medium" else srng.uniform(0.55, 1.0)
            r = BA.radius_of(asset, s)
            if not home.contains(q.x, q.y) or P.dist_to_plot(q.x, q.y) < M.KEEP_OUT_PLOT + r or not spots.free(q.x, q.y, r * 0.45):
                continue
            BA.place(meshes, C["Nature"], asset, q.x, q.y, home.height(q.x, q.y) - 0.03, srng.uniform(0, 360), s,
                     (srng.uniform(-8, 8), srng.uniform(-8, 8)))
            spots.add(q.x, q.y, r * 0.8)
            n_scree += 1
    rb = M.RIM_BUSHES
    t = ts[rb["terrace"]]
    x = rb["x"][0]
    n_rim = 0
    while x < rb["x"][1]:
        i = edge_station(t, x)
        x += srng.uniform(*rb["every"])
        if i is None or srng.random() > rb["chance"]:
            continue
        P0, N0 = t.outline[i], t.normals[i]
        q = P0 - N0 * srng.uniform(*rb["inset"])
        asset = srng.choice(["Bush_A", "Bush_B", "Bush_B"])
        s = srng.uniform(0.6, 0.95)
        if not spots.free(q.x, q.y, 0.3):
            continue
        BA.place(meshes, C["Nature"], asset, q.x, q.y, t.height(q.x, q.y) - 0.03, srng.uniform(0, 360), s)
        spots.add(q.x, q.y, BA.radius_of(asset, s))
        n_rim += 1
    print("scree rocks %d, rim bushes %d" % (n_scree, n_rim))

    # ---------------------------------------------------------------- россыпь
    def keep_out(tname, x, y, r):
        return tname == M.FIELD_TERRACE and P.dist_to_plot(x, y) < M.KEEP_OUT_PLOT + r * 0.6

    def scatter(tname, items, density, srange, rad, grp, wide=False, pack=1.0):
        t = ts[tname]
        if wide:
            bx0, bx1, by0, by1 = M.VIEW_BOX
            region = [Vector((bx0, by0, 0)), Vector((bx1, by0, 0)), Vector((bx1, by1, 0)), Vector((bx0, by1, 0))]
        else:
            region = frame_region(M.CAMERA, t.z)
        x0, x1, y0, y1 = t.bbox
        rx = [q.x for q in region]
        ry = [q.y for q in region]
        x0, x1 = max(x0, min(rx)), min(x1, max(rx))
        y0, y1 = max(y0, min(ry)), min(y1, max(ry))
        if x1 <= x0 or y1 <= y0:
            return 0
        names = [a for a, _ in items]
        wts = [w for _, w in items]
        want = int((x1 - x0) * (y1 - y0) * density)
        got, tries = 0, 0
        while got < want and tries < want * 25:
            tries += 1
            x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
            if not TR.point_in_poly(x, y, region) or not t.contains(x, y):
                continue
            if any(TR.point_in_poly(x, y, occ) or TR.dist_to_outline(x, y, occ) < 0.9 for occ in t.occluders):
                continue
            asset = rng.choices(names, wts)[0]
            s = rng.uniform(*srange)
            r = BA.radius_of(asset, s) if asset.startswith(("Tree_", "Bush_", "Rock_")) else rad
            margin = 0.35 if asset.startswith(("Grass_", "Flowers_", "Env_Tuft", "Env_Flowers")) else 0.55 * r
            if t.rim(x, y) < margin:
                continue
            if keep_out(tname, x, y, r) or not spots.free(x, y, r * pack):
                continue
            BA.place(meshes, C[grp], asset, x, y, t.height(x, y) - 0.02, rng.uniform(0, 360), s,
                     (rng.uniform(-3, 3), rng.uniform(-3, 3)))
            spots.add(x, y, r * pack)
            got += 1
        return got

    wide_all = getattr(M, "SCATTER_WIDE", False)
    for fe in M.FOREST:
        tname, items, dens, srange, rad = fe[:5]
        pack = fe[5] if len(fe) > 5 else 1.0
        print("forest %-8s %d" % (tname, scatter(tname, items, dens, srange, rad, "Nature", wide=True, pack=pack)))
    # куртины цветов (остров): центры по террасам, вокруг каждого несколько куртин
    fl = getattr(M, "FLOWERS", None)
    if fl:
        frng2 = random.Random(131)
        names, wts = [a for a, _ in fl["items"]], [w for _, w in fl["items"]]
        got = 0
        for tname, n_centers in fl["centers"].items():
            t = ts[tname]
            x0, x1, y0, y1 = t.bbox
            centers, tries = [], 0
            while len(centers) < n_centers and tries < n_centers * 60:
                tries += 1
                cx, cy = frng2.uniform(x0, x1), frng2.uniform(y0, y1)
                if not t.contains(cx, cy, 0.8) or keep_out(tname, cx, cy, 0.9):
                    continue
                if any(TR.point_in_poly(cx, cy, occ) for occ in t.occluders) or not spots.free(cx, cy, 0.3):
                    continue
                if any(math.hypot(cx - q[0], cy - q[1]) < 2.2 for q in centers):
                    continue
                centers.append((cx, cy))
            for (cx, cy) in centers:
                sp = frng2.uniform(*fl["spread"])
                for k in range(frng2.randint(*fl["per"])):
                    x, y = cx + frng2.gauss(0, sp), cy + frng2.gauss(0, sp * 0.8)
                    s = frng2.uniform(*fl["scale"])
                    r = 0.26 * s
                    if not t.contains(x, y, fl["rim"]) or keep_out(tname, x, y, r) or not spots.free(x, y, r * 0.8):
                        continue
                    if any(TR.point_in_poly(x, y, occ) or TR.dist_to_outline(x, y, occ) < 0.4 for occ in t.occluders):
                        continue
                    BA.place(meshes, C["Nature"], frng2.choices(names, wts)[0], x, y, t.height(x, y) - 0.01,
                             frng2.uniform(0, 360), s)
                    spots.add(x, y, r * 0.8)
                    got += 1
        print("flower clumps %d" % got)
    for tname, items, dens, srange, rad in M.GROUND:
        print("ground %-8s %d" % (tname, scatter(tname, items, dens, srange, rad, "Nature", wide=wide_all)))
    # поле стройки: клевер и камешки куртинами внутри клеток (в игре их закрывают подложки зданий)
    items, dens, srange, rad = M.PLOT_DECOR
    cl = M.PLOT_CLUSTERS
    names, wts = [a for a, _ in items], [w for _, w in items]
    crng = random.Random(77)
    centers = []
    tries = 0
    while len(centers) < cl["n"] and tries < 400:
        tries += 1
        c = (crng.uniform(-P.HALF_W + 1.0, P.HALF_W - 1.0), crng.uniform(-P.HALF_H + 1.0, P.HALF_H - 1.0))
        if all(math.hypot(c[0] - q[0], c[1] - q[1]) >= cl["min_gap"] for q in centers):
            centers.append(c)
    spots_plot = []
    for (cx, cy) in centers:
        sp = crng.uniform(*cl["spread"])
        for k in range(crng.randint(*cl["per"])):
            spots_plot.append((cx + crng.gauss(0, sp), cy + crng.gauss(0, sp * 0.8)))
    for k in range(cl["singles"]):
        spots_plot.append((crng.uniform(-P.HALF_W + 0.3, P.HALF_W - 0.3), crng.uniform(-P.HALF_H + 0.3, P.HALF_H - 0.3)))
    got = 0
    for (x, y) in spots_plot:
        if not P.inside(x, y, -0.3) or not spots.free(x, y, rad):
            continue
        asset = crng.choices(names, wts)[0]
        BA.place(meshes, C["Nature"], asset, x, y, ts[M.FIELD_TERRACE].height(x, y) - 0.004, crng.uniform(0, 360),
                 crng.uniform(*srange))
        spots.add(x, y, rad)
        got += 1
    print("plot decor %d in %d clusters" % (got, len(centers)))
    for tname, step in M.RIM_TUFTS:
        t = ts[tname]
        s_acc = 0.0
        if wide_all:
            bx0, bx1, by0, by1 = M.VIEW_BOX
            region = [Vector((bx0, by0, 0)), Vector((bx1, by0, 0)), Vector((bx1, by1, 0)), Vector((bx0, by1, 0))]
        else:
            region = frame_region(M.CAMERA, t.z)
        for i in range(len(t.outline)):
            p0, p1 = t.outline[i], t.outline[(i + 1) % len(t.outline)]
            s_acc += (p1 - p0).length
            if s_acc < step:
                continue
            s_acc = rng.uniform(-0.25, 0.1) * step
            n = t.normals[i]
            q = p0 + n * 0.06
            if not TR.in_view(q.x, q.y) or not TR.point_in_poly(q.x, q.y, region):
                continue
            if not spots.free(q.x, q.y, 0.12):
                continue
            if any(TR.point_in_poly(q.x, q.y, occ) or TR.dist_to_outline(q.x, q.y, occ) < 0.5 for occ in t.occluders):
                continue
            asset = rng.choice(["Env_Tuft_A", "Env_Tuft_B", "Env_Tuft_B"])
            o = BA.place(meshes, C["Nature"], asset, q.x, q.y, t.z - 0.05, 0.0, rng.uniform(1.0, 1.5))
            axis = Vector((-n.y, n.x, 0.0))
            o.matrix_world = Matrix.Translation(o.location) @ Matrix.Rotation(math.radians(rng.uniform(25, 45)), 4, axis) @ \
                Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z") @ Matrix.Scale(o.scale.x, 4)

    # ---------------------------------------------------------------- вне кадра — долой
    culled = 0
    for g in ("Nature", "Props") if getattr(M, "CULL", True) else ():
        for o in list(C[g].objects):
            if o.type == "MESH" and not in_frame(M.CAMERA, obj_corners(o)):
                bpy.data.objects.remove(o, do_unlink=True)
                culled += 1
    print("culled outside the 21:9 frame: %d objects" % culled)

    # ---------------------------------------------------------------- ветер: деревья и кусты у поля отдельно
    sw = M.SWAY
    swayers = []
    for o in C["Nature"].objects:
        nm = o.data.name
        if not nm.startswith(("Tree_", "Bush_")):
            continue
        x, y = o.location.x, o.location.y
        if P.dist_to_plot(x, y) > sw["near"]:
            continue
        if not any(ts[tn].contains(x, y) for tn in ("home", "east", "north")):
            continue
        swayers.append((P.dist_to_plot(x, y), o))
    swayers.sort(key=lambda e: e[0])
    wrng = random.Random(3)
    for _, o in swayers[:40]:
        deg, hz = sw["tree"] if o.data.name.startswith("Tree_") else sw["bush"]
        o["sway"] = [round(deg * wrng.uniform(0.8, 1.2), 2), round(hz * wrng.uniform(0.85, 1.15), 3),
                     round(wrng.random(), 3)]
    print("sway objects %d" % min(40, len(swayers)))

    # ---------------------------------------------------------------- слияние мелочи в куски по сетке
    merges = {}
    gone = []
    stats = {}
    for g in ("Nature", "Props"):
        for o in list(C[g].objects):
            if o.type != "MESH" or "sway" in o:
                continue
            if o.data.name.startswith(BA.MERGE_PREFIX + ("Res_", "Prop_Rails", "Prop_Pickaxe", "Prop_Minecart",
                                                          "Tree_Stump", "Env_Clover", "Env_Pebbles", "Env_Haystack",
                                                          "Prop_CliffStairs", "Env_FlowerPatch")):
                mw = obj_matrix(o)
                key = chunk_of(o.location.x, o.location.y)
                if key not in merges:
                    merges[key] = V.Asset("%s_Scatter_%s" % (N, key))
                merges[key].add_mesh(o.data, mw)
                gone.append(o)
                o.data.calc_loop_triangles()
                stats[o.data.name] = stats.get(o.data.name, 0) + len(o.data.loop_triangles)
    for o in gone:
        bpy.data.objects.remove(o, do_unlink=True)
    for key in sorted(merges):
        name = "%s_Scatter_%s" % (N, key)
        me = mesh_from_asset(merges[key], name, mat)
        meshes[name] = me
        C["Nature"].objects.link(bpy.data.objects.new(name, me))
        me.calc_loop_triangles()
        print("  %s: %d tris" % (name, len(me.loop_triangles)))
    print("merged %d scatter objects: %s" % (len(gone), ", ".join("%s %d" % kv for kv in
                                                                    sorted(stats.items(), key=lambda kv: -kv[1])[:8])))

    # ---------------------------------------------------------------- небо острова: облака, дальние острова
    if getattr(M, "CLOUDS", None) or getattr(M, "DISTANT", None):
        build_sky(V, BA, TR, M, meshes, C, mat, ts)

    # ---------------------------------------------------------------- превью зданий (в игру не идёт)
    import vitaria_buildings as VB
    for (mesh, cx, cy, w, d) in PREVIEW_SETS.get(preview, []):
        if mesh not in meshes and bpy.data.meshes.get(mesh) is not None:
            meshes[mesh] = bpy.data.meshes[mesh]
        if mesh not in meshes:
            print("!! превью: нет меша", mesh)
            continue
        x, y = P.footprint_center(cx, cy, w, d)
        BA.place(meshes, C["Preview"], mesh, x, y, 0.0, 0.0, VB.KIT_SCALE, name="Preview_" + mesh)
        pad = "Env_Footprint_%dx%d" % (w, d)
        if pad in meshes:
            BA.place(meshes, C["Preview"], pad, x, y, 0.0, 0.0, 1.0, name="Preview_" + pad)

    # ---------------------------------------------------------------- камера, солнце, мир
    cam = M.CAMERA
    cd = bpy.data.cameras.get("CM_Camera") or bpy.data.cameras.new("CM_Camera")
    cd.sensor_fit = "VERTICAL"
    cd.sensor_height = 24.0
    cd.lens = 12.0 / math.tan(math.radians(cam["fov"]) / 2)
    cd.clip_start, cd.clip_end = 0.3, 300
    co = bpy.data.objects.new("CM_Camera", cd)
    co.location = Vector(cam["position"])
    co.rotation_euler = (Vector(cam["target"]) - co.location).to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(co)
    scn.camera = co
    ov = getattr(M, "OVERVIEW", None)
    if ov:                                   # общий план острова: отъезд камеры игры (предел зума)
        od = bpy.data.cameras.get("CM_Overview") or bpy.data.cameras.new("CM_Overview")
        od.sensor_fit, od.sensor_height = "VERTICAL", 24.0
        od.lens = 12.0 / math.tan(math.radians(ov["fov"]) / 2)
        od.clip_start, od.clip_end = 0.5, 400
        p, y = math.radians(ov["pitch"]), math.radians(ov["yaw"])
        tgt = Vector(ov["target"])
        oo = bpy.data.objects.new("CM_Overview", od)
        oo.location = tgt + Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p))) * ov["distance"]
        oo.rotation_euler = (tgt - oo.location).to_track_quat("-Z", "Y").to_euler()
        C["Rig"].objects.link(oo)
        RIG["over"] = oo
        print("overview camera at (%.1f, %.1f, %.1f)" % tuple(oo.location))
    sd = bpy.data.lights.get("CM_Sun") or bpy.data.lights.new("CM_Sun", "SUN")
    sd.energy = M.SUN["energy"]
    sd.color = M.SUN["color"]
    sd.angle = math.radians(3)
    so = bpy.data.objects.new("CM_Sun", sd)
    so.rotation_euler = Vector(M.SUN["direction"]).normalized().to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(so)
    world = bpy.data.worlds.get("AM_World") or bpy.data.worlds.new("AM_World")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.56, 0.68, 0.84, 1)
    bg.inputs[1].default_value = 1.05
    scn.world = world
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.use_denoising = True
    scn.cycles.max_bounces = 4
    scn.view_settings.view_transform = "Standard"
    scn.view_settings.look = "None"
    scn.view_settings.exposure = -0.3
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGB"
    for vl in scn.view_layers:
        vl.update()
    return scn, co, so, ts


# =========================================================================================
# небо парящего острова: облака и дальние острова (только вид; в раскладку игры пока не идут)
# =========================================================================================
def build_sky(V, BA, TR, M, meshes, C, mat, ts):
    N = M.NAME
    outer = [t.outline for t in ts.values()]

    def island_dist(x, y):
        """Расстояние до кромки острова: > 0 снаружи, < 0 внутри."""
        d = min(TR.dist_to_outline(x, y, o) for o in outer)
        return -d if any(TR.point_in_poly(x, y, o) for o in outer) else d

    rng = random.Random(404)
    cl = getattr(M, "CLOUDS", None)
    if cl:
        names, wts = [a for a, _ in cl["items"]], [w for _, w in cl["items"]]
        cx, cy = cl["center"]
        for layer in cl["layers"]:
            r0, r1 = layer["ring"]
            R = r1 + 20.0
            got, tries = 0, 0
            while got < layer["n"] and tries < layer["n"] * 80:
                tries += 1
                x, y = cx + rng.uniform(-R, R), cy + rng.uniform(-R, R)
                box = layer.get("box")                  # (x0, x1, y0, y1): только там, где облако видно
                if box and not (box[0] <= x <= box[1] and box[2] <= y <= box[3]):
                    continue
                d = island_dist(x, y)
                if d < r0 or d > r1 or rng.random() > 1.0 - 0.55 * (d - r0) / (r1 - r0):
                    continue
                BA.place(meshes, C["Sky"], rng.choices(names, wts)[0], x, y, rng.uniform(*layer["z"]),
                         rng.uniform(0, 360), rng.uniform(*layer["scale"]))
                got += 1
            print("clouds ring %s: %d" % (layer["ring"], got))
    ds = getattr(M, "DISTANT", None)
    if ds:
        vb = TR.VIEW_BOX
        TR.VIEW_BOX = None
        g, c = V.Asset(N + "_Distant_Ground"), V.Asset(N + "_Distant_Cliffs")
        for (ix, iy), z, r, seed, n_trees in ds:
            irng = random.Random(seed)
            k = 7 + int(r * 1.5)
            ctrl = []
            for i in range(k):
                ang = math.tau * i / k + irng.uniform(-0.15, 0.15)
                rr = r * irng.uniform(0.8, 1.15)
                ctrl.append((ix + math.cos(ang) * rr, iy + math.sin(ang) * rr * irng.uniform(0.75, 0.95)))
            small = r < 2.5
            depth = 1.5 + 1.7 * r
            # перевёрнутый конус: ярусы сходятся к низу, низ столбов рваный
            deep = dict(bottom=z - depth, jag=0.3 * depth, strata=(0.1, 0.22, 0.36, 0.52, 0.72),
                        batter=-0.85 * r / 6, amp=min(0.3, 0.12 * r), wave=min(0.4, 0.15 * r), lean=min(0.15, 0.08 * r))
            t = TR.Terrace("isle_%d" % seed, ctrl, z=z, bottom=z - depth, seed=seed, wob=0.08 if small else 0.18,
                           step=0.35 if small else 0.6, grid=0.5 if small else 1.0, hills=0.0 if small else 0.25,
                           grass=(0.35, 0.85), ramp=getattr(M, "GRASS", "arena_grass"), lean=0.05,
                           col_w=(0.4, 0.9) if small else (1.0, 2.2), deep=deep,
                           fade=("cliff_fade", z - depth * 0.8, z + 0.4, 0.07))
            t.build_top(g)
            t.build_lip(g)
            t.build_cliff(c, boulders=False)
            sp = BA.Spots()
            placed, tries = 0, 0
            dt = getattr(M, "DISTANT_TREES", None)      # окружение арены: свои деревья (ели в снегу), (крупные, малые)
            kinds = (dt[1] if small else dt[0]) if dt else \
                (["Tree_Pine_A", "Tree_Pine_B", "Tree_Round_A", "Tree_Round_B"] if not small else
                 ["Tree_Pine_B", "Tree_Round_B"])
            while placed < n_trees and tries < 300:
                tries += 1
                x, y = irng.uniform(ix - r, ix + r), irng.uniform(iy - r, iy + r)
                if not t.contains(x, y, 0.35 if small else 0.7) or not sp.free(x, y, 0.75):
                    continue
                BA.place(meshes, C["Sky"], irng.choice(kinds), x, y, t.height(x, y) - 0.03, irng.uniform(0, 360),
                         irng.uniform(0.9, 1.3))
                sp.add(x, y, 0.75)
                placed += 1
            for i in range(2 + int(r > 3)):             # у подножия каждого острова — облака
                ang = irng.uniform(0, math.tau)
                cxx, cyy = ix + math.cos(ang) * r * 0.9, iy + math.sin(ang) * r * 0.8
                BA.place(meshes, C["Sky"], irng.choice(["Env_CloudPuff_A", "Env_CloudPuff_B"]), cxx, cyy,
                         z - depth * irng.uniform(0.45, 0.7), irng.uniform(0, 360), irng.uniform(0.6, 1.0) * max(0.7, r / 3))
        TR.VIEW_BOX = vb
        for name, a in ((N + "_Distant_Ground", g), (N + "_Distant_Cliffs", c)):
            me = mesh_from_asset(a, name, mat)
            meshes[name] = me
            C["Sky"].objects.link(bpy.data.objects.new(name, me))
        print("distant islands %d" % len(ds))


# =========================================================================================
# look-dev: то, что в URP делает постобработка (Fog, Depth of Field, Bloom, Tonemapping,
# Lift/Gamma/Gain, Saturation, Vignette) — здесь компоновщиком по проходам Mist и Depth
# =========================================================================================
def hex_lin(h):
    import build_vitaria as V
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in V.hex2rgb(h))


def lookdev_scene(scn, LD, sun):
    scn.view_settings.view_transform = LD["view"]
    scn.view_settings.look = LD["look"]
    scn.view_settings.exposure = LD["exposure"]
    # мир: камера видит цвет тумана (фон кадра), свет неба — свой цвет (в Unity это разные настройки:
    # Camera Background и Environment Lighting)
    world = scn.world
    nt = world.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputWorld")
    sky = nt.nodes.new("ShaderNodeBackground")
    sky.inputs[0].default_value = tuple(LD["ambient"]) + (1.0,)
    sky.inputs[1].default_value = LD["world"]
    back = nt.nodes.new("ShaderNodeBackground")
    back.inputs[0].default_value = hex_lin(LD["fog"]) + (1.0,)
    lp = nt.nodes.new("ShaderNodeLightPath")
    mx = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lp.outputs["Is Camera Ray"], mx.inputs[0])
    nt.links.new(sky.outputs[0], mx.inputs[1])
    nt.links.new(back.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs["Surface"])
    sun.data.angle = math.radians(LD["sun_angle"])
    sun.data.energy = LD["sun_energy"]
    vl = scn.view_layers[0]
    vl.use_pass_mist = True
    vl.use_pass_z = True
    if hasattr(scn.render, "use_compositing"):
        scn.render.use_compositing = True
    lookdev_materials(LD)


def _node(nt, kind, name, loc):
    n = nt.nodes.get(name)
    if n is None:
        n = nt.nodes.new(kind)
        n.name = n.label = name
        n.location = loc
    return n


def lookdev_materials(LD):
    """Материалы вида (в URP — свои шейдеры, см. выводы):
    вода — бирюзовый оттенок и светлая полоса мелководья по цвету вершин (Col) у берегов;
    трава (строки рамп grass/arena_grass палитры) — мягкие пятна шума по мировым координатам."""
    import build_vitaria as V
    w = LD.get("water")
    for mname, tint in (("Vitaria_Water", w and w["tint"]), ("Vitaria_Waterfall", w and w["falls_tint"]),
                        ("Vitaria_Water_Swamp", w and w.get("swamp_tint"))):
        mat = bpy.data.materials.get(mname)
        if mat is None or not tint:
            continue
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
        mul = _node(nt, "ShaderNodeMix", "LD_Tint", (-180, 300))
        mul.data_type, mul.blend_type = "RGBA", "MULTIPLY"
        mul.inputs[0].default_value = 1.0
        nt.links.new(tex.outputs["Color"], mul.inputs[6])
        mul.inputs[7].default_value = tuple(tint) + (1.0,)
        out = mul.outputs[2]
        if mname == "Vitaria_Water":
            at = _node(nt, "ShaderNodeVertexColor", "LD_Shallow", (-420, -80))
            at.layer_name = "Col"
            fac = _node(nt, "ShaderNodeMath", "LD_ShallowK", (-180, -80))
            fac.operation = "MULTIPLY"
            nt.links.new(at.outputs["Color"], fac.inputs[0])
            fac.inputs[1].default_value = w["shallow_k"]
            sh = _node(nt, "ShaderNodeMix", "LD_ShallowMix", (40, 300))
            sh.data_type, sh.blend_type = "RGBA", "MIX"
            nt.links.new(fac.outputs[0], sh.inputs[0])
            nt.links.new(out, sh.inputs[6])
            sh.inputs[7].default_value = hex_lin(w["shallow"]) + (1.0,)
            out = sh.outputs[2]
        nt.links.new(out, bsdf.inputs["Base Color"])
    g = LD.get("grass_noise")
    for pname in LD.get("palettes", ["Vitaria_Palette"]):      # окружение арены: и его вариант палитры
        _grass_noise(V, bpy.data.materials.get(pname), g)


def _grass_noise(V, mat, g):
    if g and mat is not None:
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image and n.image.name.startswith("Vitaria_Palette")
                   and "Emission" not in n.image.name)
        # маска: строки рамп травы (v внутри строки RAMP_ROW) — только земля, не здания и не реквизит
        uv = _node(nt, "ShaderNodeTexCoord", "LD_UV", (-900, 500))
        sep = _node(nt, "ShaderNodeSeparateXYZ", "LD_Sep", (-720, 500))
        nt.links.new(uv.outputs["UV"], sep.inputs[0])
        mask = None
        for k, rn in enumerate(("grass", "arena_grass")):
            row = V.RAMP_ROW[rn]
            v0, v1 = 1.0 - (row + 1) / V.NCELL, 1.0 - row / V.NCELL
            a = _node(nt, "ShaderNodeMath", "LD_Gt%d" % k, (-540, 560 - k * 160))
            a.operation = "GREATER_THAN"
            nt.links.new(sep.outputs["Y"], a.inputs[0])
            a.inputs[1].default_value = v0
            b = _node(nt, "ShaderNodeMath", "LD_Lt%d" % k, (-540, 480 - k * 160))
            b.operation = "LESS_THAN"
            nt.links.new(sep.outputs["Y"], b.inputs[0])
            b.inputs[1].default_value = v1
            m = _node(nt, "ShaderNodeMath", "LD_In%d" % k, (-380, 520 - k * 160))
            m.operation = "MULTIPLY"
            nt.links.new(a.outputs[0], m.inputs[0])
            nt.links.new(b.outputs[0], m.inputs[1])
            if mask is None:
                mask = m.outputs[0]
            else:
                mx = _node(nt, "ShaderNodeMath", "LD_Mask", (-220, 440))
                mx.operation = "MAXIMUM"
                nt.links.new(mask, mx.inputs[0])
                nt.links.new(m.outputs[0], mx.inputs[1])
                mask = mx.outputs[0]
        geo = _node(nt, "ShaderNodeNewGeometry", "LD_Geo", (-900, 200))
        nz = _node(nt, "ShaderNodeTexNoise", "LD_Noise", (-720, 200))
        nz.inputs["Scale"].default_value = g["scale"]
        nz.inputs["Detail"].default_value = 2.0
        nt.links.new(geo.outputs["Position"], nz.inputs["Vector"])
        # пятна: темнее и холоднее / светлее и теплее вокруг цвета рампы
        ramp = _node(nt, "ShaderNodeValToRGB", "LD_NoiseRamp", (-540, 200))
        els = ramp.color_ramp.elements
        els[0].position, els[1].position = 0.3, 0.7
        els[0].color = tuple(g["dark"]) + (1.0,)
        els[1].color = tuple(g["light"]) + (1.0,)
        nt.links.new(nz.outputs["Fac"], ramp.inputs[0])
        mul = _node(nt, "ShaderNodeMix", "LD_GrassMul", (-200, 260))
        mul.data_type, mul.blend_type = "RGBA", "MULTIPLY"
        nt.links.new(mask, mul.inputs[0])
        nt.links.new(tex.outputs["Color"], mul.inputs[6])
        nt.links.new(ramp.outputs["Color"], mul.inputs[7])
        nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])


def lookdev_shot(scn, LD, shot, height):
    """Компоновщик кадра: туман (Mist) -> резкость по глубине (Defocus с картой радиуса) -> Bloom ->
    Lift/Gamma/Gain -> насыщенность -> виньетка."""
    sp = LD["shots"][shot]
    f0, flen, fmax = sp["fog"]
    ms = scn.world.mist_settings
    ms.start, ms.depth, ms.falloff = f0, flen, "LINEAR"
    old = bpy.data.node_groups.get("CM_Look")
    if old is not None:
        bpy.data.node_groups.remove(old)
    tree = bpy.data.node_groups.new("CM_Look", "CompositorNodeTree")
    tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    nodes, links = tree.nodes, tree.links

    def put(sock, v):
        if isinstance(v, bpy.types.NodeSocket):
            links.new(v, sock)
        else:
            sock.default_value = v

    def math_op(op, a, b=None, clamp=False):
        n = nodes.new("ShaderNodeMath")
        n.operation, n.use_clamp = op, clamp
        put(n.inputs[0], a)
        if b is not None:
            put(n.inputs[1], b)
        return n.outputs[0]

    def map_range(v, a0, a1, b0, b1):
        n = nodes.new("ShaderNodeMapRange")
        n.interpolation_type, n.clamp = "SMOOTHSTEP", True
        put(n.inputs[0], v)
        for i, x in zip((1, 2, 3, 4), (a0, a1, b0, b1)):
            n.inputs[i].default_value = x
        return n.outputs[0]

    def mix(fac, a, b, blend="MIX"):
        n = nodes.new("ShaderNodeMix")
        n.data_type, n.blend_type = "RGBA", blend
        put(n.inputs[0], fac)
        put(n.inputs[6], a)
        put(n.inputs[7], b)
        return n.outputs[2]

    rl = nodes.new("CompositorNodeRLayers")
    rl.scene = scn
    img = rl.outputs["Image"]
    # туман: доля Mist (линейно от start на depth метров) к цвету фона
    img = mix(math_op("MULTIPLY", rl.outputs["Mist"], fmax), img, hex_lin(LD["fog"]) + (1.0,))
    # глубина резкости: радиус (px) по глубине — ближняя зона и дальняя, как Gaussian DoF в URP
    n_full, n_end, d0, d1, rad = sp["dof"]
    rad_px = rad * height / 1080.0
    coc = map_range(rl.outputs["Depth"], d0, d1, 0.0, 1.0)
    if n_end > 0:
        coc = math_op("MAXIMUM", coc, map_range(rl.outputs["Depth"], n_full, n_end, 1.0, 0.0))
    df = nodes.new("CompositorNodeDefocus")
    df.use_zbuffer, df.z_scale, df.blur_max = False, 1.0, max(1.0, rad_px)
    put(df.inputs["Image"], img)
    put(df.inputs["Z"], math_op("MULTIPLY", coc, rad_px))
    img = df.outputs["Image"]
    # свечение
    b = LD["bloom"]
    gl = nodes.new("CompositorNodeGlare")
    gl.inputs["Type"].default_value = "Bloom"
    gl.inputs["Quality"].default_value = "High"
    gl.inputs["Threshold"].default_value = b["threshold"]
    gl.inputs["Smoothness"].default_value = 0.6
    gl.inputs["Strength"].default_value = b["strength"]
    gl.inputs["Size"].default_value = b["size"]
    put(gl.inputs["Image"], img)
    img = gl.outputs["Image"]
    # цвет: тени к голубому, света к тёплому; насыщенность
    cb = nodes.new("CompositorNodeColorBalance")
    cb.inputs["Type"].default_value = "Lift/Gamma/Gain"
    for ident, key in (("Color Lift", "lift"), ("Color Gamma", "gamma"), ("Color Gain", "gain")):
        s = next(s for s in cb.inputs if s.identifier == ident)
        s.default_value = tuple(LD[key]) + (1.0,)
    put(cb.inputs["Image"], img)
    hs = nodes.new("CompositorNodeHueSat")
    hs.inputs["Saturation"].default_value = LD["saturation"]
    put(hs.inputs["Image"], cb.outputs["Image"])
    img = hs.outputs["Image"]
    # виньетка: размытый эллипс
    if LD.get("vignette"):
        em = nodes.new("CompositorNodeEllipseMask")
        put(em.inputs["Mask"], math_op("MULTIPLY", rl.outputs["Alpha"], 0.0))
        em.inputs["Size"].default_value = (0.92, 0.86)
        bl = nodes.new("CompositorNodeBlur")
        put(bl.inputs["Image"], em.outputs["Mask"])
        bl.inputs["Size"].default_value = (height * 0.22, height * 0.22)
        vg = map_range(bl.outputs["Image"], 0.0, 1.0, 1.0 - LD["vignette"], 1.0)
        img = mix(1.0, img, vg, "MULTIPLY")
    out = nodes.new("NodeGroupOutput")
    links.new(img, out.inputs[0])
    scn.compositing_node_group = tree


# =========================================================================================
# превью
# =========================================================================================
def render_shots(scn, cam, prev_dir, shots, res, samples, tag="", name="Colony_Meadow", look=None, cams=None):
    os.makedirs(prev_dir, exist_ok=True)
    sizes = {"hero": (1920, 1080), "wide": (2520, 1080), "ipad": (1440, 1080), "top": (1600, 1100),
             "play": (1920, 1080), "over": (1920, 1080)}
    cams = cams or {}
    out = []
    top_cam = None
    for sh in [s for s in shots.split(",") if s]:
        if sh not in sizes:
            continue
        w, h = sizes[sh]
        if sh == "top":
            td = bpy.data.cameras.new("CM_TopCam")
            td.type = "ORTHO"
            td.ortho_scale = 84
            td.clip_end = 300
            top_cam = bpy.data.objects.new("CM_TopCam", td)
            top_cam.location = (-2.0, 11.0, 60.0)
            scn.collection.objects.link(top_cam)
            scn.camera = top_cam
        else:
            scn.camera = cams.get(sh, cam)
        scn.render.resolution_x, scn.render.resolution_y = w, h
        scn.render.resolution_percentage = res
        scn.cycles.samples = samples
        if look and sh in look["shots"]:
            lookdev_shot(scn, look, sh, h * res / 100.0)
        path = os.path.join(prev_dir, "%s_%s%s.png" % (name.lower(), sh, tag))
        scn.render.filepath = path
        bpy.ops.render.render(write_still=True, scene=scn.name)
        print("rendered", path)
        out.append(path)
    if top_cam is not None:
        scn.camera = cam
        d = top_cam.data
        bpy.data.objects.remove(top_cam, do_unlink=True)
        bpy.data.cameras.remove(d)
    return out



# =========================================================================================
# экспорт
# =========================================================================================
ARENA_BACKDROP = {"Bld_Castle", "Bld_Tower_Round", "Bld_Cottage", "Bld_House_Stone", "Bld_House_Timber",
                  "Bld_WatchTower"}          # уже лежат в Models/Arena/Backdrop (экспорт build_arena)
PLAYER_BUILDINGS = {"Bld_Barracks", "Bld_Warehouse"}   # свои модели казармы и склада -> Models/Buildings
WHEEL_SPIN = -30.0        # град/с вокруг оси ступицы: низ колеса идёт по течению (на юг)


def export_colony(BA, VC, meshes, scn, cam, sun, unity, root):
    import build_vitaria as V
    mods = VC.load()
    M, P = mods["layout"], mods["plot"]
    N = M.NAME
    if N != "Colony_Meadow":
        print("!! экспорт %s не подключён: это макет вида, раскладка игры — Colony_Meadow" % N)
        return None
    fx = BA.fx_material()
    base = os.path.join(unity, "Models", "Colony")
    colony_names = [n for c, n, _, _ in VC.asset_entries() if c == "Colony"]
    terrain = [n for n in meshes if n.startswith(N + "_")]
    used = {o.data.name for g in GROUPS for o in bpy.data.collections[PFX + g].objects if o.type == "MESH"}
    backdrop = sorted(n for n in used if n.startswith("Bld_") and n not in ARENA_BACKDROP
                      and n not in colony_names and n not in PLAYER_BUILDINGS)
    for n in used:                                   # имена моделей уникальны на весь Models/: сборщик
        if n.endswith("_Muted") and n.replace("_Muted", "") not in ARENA_BACKDROP:   # ищет их по имени
            raise RuntimeError("приглушённая копия не из фона арены: " + n)
    info = []
    for n in colony_names + terrain:
        hot = BA.split_emissive(V, meshes[n], fx)
        if hot:
            print("fx faces %-24s %d" % (n, hot))
    for n in colony_names:
        BA.export_mesh(meshes[n], os.path.join(base, n + ".fbx"))
    for n in backdrop:
        BA.export_mesh(meshes[n], os.path.join(base, "Backdrop", n + ".fbx"))
    for n in terrain:
        BA.export_mesh(meshes[n], os.path.join(base, "Meadow", n + ".fbx"))
    for n in sorted(PLAYER_BUILDINGS):
        if n in meshes:
            BA.export_mesh(meshes[n], os.path.join(unity, "Models", "Buildings", n + ".fbx"))
    for n in colony_names + backdrop + terrain + sorted(PLAYER_BUILDINGS):
        if n not in meshes:
            continue
        t, sz = BA.tris_size(meshes[n])
        cat = ("Colony" if n in colony_names else "Backdrop" if n in backdrop else
               "Buildings" if n in PLAYER_BUILDINGS else "ColonyMeadow")
        info.append({"asset": n, "category": cat, "tris": t, "size_unity": sz})
        print("fbx %-26s %6d tris" % (n, t))

    items, total = [], 0
    for g in GROUPS:
        for o in bpy.data.collections[PFX + g].objects:
            if o.type != "MESH":
                continue
            p, q, sc = BA.to_unity(o.matrix_world)
            it = {"name": o.name, "asset": o.data.name, "group": g, "p": p, "r": q, "s": sc}
            if o.data.name == "Bld_Watermill_Wheel":
                it["spin"] = WHEEL_SPIN
            if "sway" in o:
                it["sway"] = [float(v) for v in o["sway"]]
            if o.data.name in SCROLL:
                it["scroll"] = SCROLL[o.data.name]
            items.append(it)
            o.data.calc_loop_triangles()
            total += len(o.data.loop_triangles)
    cw = cam.matrix_world
    fwd = cw.to_3x3() @ Vector((0, 0, -1))
    up = cw.to_3x3() @ Vector((0, 1, 0))
    sun_fwd = sun.matrix_world.to_3x3() @ Vector((0, 0, -1))
    layout = {
        "name": N,
        "kitVersion": V.KIT_VERSION,
        "plot": {"width": P.W, "height": P.H, "cellSize": P.CELL,
                 "note": "начало координат — середина поля: worldView.MapToWorld((7, 7, 0)); мировые оси, "
                         "корень окружения без поворота (X на восток, Y вверх, Z на север)",
                 "corners": [BA.to_unity_vec((x, y, 0.0)) for x, y in P.corners()]},
        "objects": items,
        "camera": {"position": BA.to_unity_vec(cw.translation), "forward": BA.to_unity_vec(fwd),
                   "up": BA.to_unity_vec(up), "fov": M.CAMERA["fov"]},
        "sun": {"forward": BA.to_unity_vec(sun_fwd), "color": list(M.SUN["color"]), "intensity": 1.3},
        "ambient": {"sky": [0.64, 0.72, 0.82], "equator": [0.52, 0.58, 0.5], "ground": [0.28, 0.26, 0.24]},
        "background": [0.33, 0.45, 0.6],
        "fx": BA.FX_ROLES,
        "sockets": [{"kind": k, "p": BA.to_unity_vec(p), "size": sz} for k, p, sz in SOCKETS],
        "tris": total,
    }
    lay_dir = os.path.join(unity, "Layout")
    os.makedirs(lay_dir, exist_ok=True)
    with open(os.path.join(lay_dir, "colony_meadow_layout.json"), "w", encoding="utf-8") as f:
        json.dump(layout, f, indent=1, ensure_ascii=False)
    print("layout: %d objects, %d tris, %d sockets" % (len(items), total, len(SOCKETS)))
    list_path = os.path.join(root, "asset_list.json")
    rows = []
    if os.path.exists(list_path):
        try:
            rows = json.load(open(list_path, encoding="utf-8"))
        except Exception:
            rows = []
    names = {x["asset"] for x in info}
    rows = [r for r in rows if r.get("asset") not in names and r.get("category") not in ("Colony", "ColonyMeadow")]
    rows += info
    with open(list_path, "w", encoding="utf-8") as f:
        json.dump(rows, f, indent=1)
    return layout

# =========================================================================================
def player_buildings(V, BA, ref_dir, mat, meshes):
    """Казарма и склад (Ref_Buildings) по контракту зданий поля — vitaria_buildings.build_ref_player."""
    import vitaria_buildings as VB
    if not ref_dir:
        return
    for me, line in VB.build_ref_player(V, BA.build_mesh, mat):
        meshes[me.name] = me
        print(line)


def colony_backdrop(V, BA, meshes):
    """Фон колонии: приглушённые копии фона арены (<имя>_Muted) и перекраска своего фона на месте."""
    for n in MUTED:
        src = meshes.get(n)
        if src is None:
            continue
        name = n + "_Muted"
        old = bpy.data.meshes.get(name)
        if old is not None:
            bpy.data.meshes.remove(old)
        me = src.copy()
        me.name = name
        k = V.recolor_mesh(me, "old")
        meshes[name] = me
        if n in BA.CHIMNEYS:
            BA.CHIMNEYS[name] = BA.CHIMNEYS[n]
        print("muted %-24s %d faces" % (name, k))
    for n, theme in COLONY_THEME.items():
        if n in meshes:
            print("theme %-24s %-7s %d faces" % (n, theme, V.recolor_mesh(meshes[n], theme)))


def main():
    o = parse_args()
    if o["blend"]:
        bpy.ops.wm.open_mainfile(filepath=o["blend"])
    import build_vitaria as V
    V = importlib.reload(V)
    import build_arena as BA
    BA = importlib.reload(BA)
    import vitaria_arena as VA
    VA = importlib.reload(VA)
    VA.load(reload=True)
    import vitaria_colony as VC
    VC = importlib.reload(VC)
    VC.LAYOUT = o["layout"]
    RIG.clear()
    root = os.path.normpath(os.path.join(HERE, ".."))
    unity = os.path.join(root, "Unity", "Assets", "Vitaria")
    tex_dir = os.path.join(unity, "Textures")
    mat = BA.palette_material(V, tex_dir)
    wmat = BA.water_materials(VA.load()["terrain"], tex_dir)
    kit_root = bpy.data.collections.get("Vitaria_Kit")
    if kit_root is None:
        kit_root = bpy.data.collections.new("Vitaria_Kit")
        bpy.context.scene.collection.children.link(kit_root)
    ref_dir = BA.find_ref(HERE, None)
    BA.BACKDROP = list(BA.BACKDROP) + [m for m in EXTRA_BACKDROP if m not in BA.BACKDROP]
    meshes = BA.build_assets(V, VA, ref_dir, mat, kit_root, False)
    kit_col = BA.ensure_coll("Kit_Colony", kit_root)
    for cat, n, title, fn in VC.asset_entries(reload=True):
        me = BA.build_mesh(V, n, fn, mat)
        meshes[n] = me
        BA.kit_object(me, kit_col, "Colony", title)
        print("colony asset %-22s" % n)
    for n in [m.name for m in bpy.data.meshes if m.name.startswith(("Bld_", "Prop_", "Res_", "Tree_", "Rock_", "Bush_"))]:
        meshes.setdefault(n, bpy.data.meshes[n])
    colony_backdrop(V, BA, meshes)
    player_buildings(V, BA, ref_dir, mat, meshes)
    scn, cam, sun, ts = build_scene(V, BA, VC, meshes, mat, wmat, o["preview"])
    M = VC.load()["layout"]
    look = getattr(M, "LOOKDEV", None) if o["look"] else None
    if look:
        lookdev_scene(scn, look, sun)
    if o["save_as"]:
        bpy.ops.wm.save_as_mainfile(filepath=o["save_as"], copy=True)
        print("saved copy", o["save_as"])
    if o["render"]:
        prev = o["prev"] or os.path.join(root, "Previews")
        render_shots(scn, cam, prev, o["shots"], o["res"], o["samples"], o["tag"], M.NAME, look, dict(RIG))
    if o["save"] and bpy.data.filepath:
        # витрина кита: ряд ассетов колонии под рядами арены и фона
        if not any(r[0] == "Kit_Colony" for r in BA.SHOW_ROWS):
            BA.SHOW_ROWS = list(BA.SHOW_ROWS) + [("Kit_Colony", 60.0, -66.0, 1.0, 0.25, 99, 6.0)]
        BA.showcase(kit_root)
        bpy.ops.wm.save_mainfile()
        print("saved", bpy.data.filepath)
    if o["export"]:
        export_colony(BA, VC, meshes, scn, cam, sun, unity, root)
    print("done")


if __name__ == "__main__":
    main()
