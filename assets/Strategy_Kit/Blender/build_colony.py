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
PREVIEW_SETS = {
    # (меш, SW-клетка x, y, ширина, глубина, масштаб модели) — как префабы Assets/Game/Prefabs/Buildings
    "start": [("Bld_Market", 9, 4, 3, 2, 0.5744), ("Bld_Warehouse", 9, 10, 3, 3, 0.56)],
    "grown": [("Bld_Market", 9, 4, 3, 2, 0.5744), ("Bld_Warehouse", 9, 10, 3, 3, 0.56),
              ("Bld_Mine", 1, 9, 3, 3, 0.5456), ("Bld_LumberCamp", 1, 4, 2, 2, 0.4036),
              ("Bld_Farm", 5, 0, 3, 3, 0.505), ("Bld_Forge", 5, 6, 2, 2, 0.4036),
              ("Bld_Barracks", 5, 10, 3, 3, 0.49), ("Bld_Smeltery", 1, 0, 3, 3, 0.7061),
              ("Bld_Tannery", 10, 0, 2, 2, 0.41)],
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    o = {"blend": None, "render": False, "export": False, "save": False, "res": 100, "samples": 48,
         "shots": "hero", "prev": None, "preview": "start", "tag": ""}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--blend", "--prev", "--shots", "--preview", "--tag") and i + 1 < len(argv):
            o[a[2:]] = argv[i + 1]
            i += 1
        elif a in ("--res", "--samples") and i + 1 < len(argv):
            o[a[2:]] = int(argv[i + 1])
            i += 1
        elif a in ("--render", "--export", "--save"):
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
    for g in GROUPS + ["Preview", "Rig"]:
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
    ground, cliffs = V.Asset(N + "_Ground"), V.Asset(N + "_Cliffs")
    for t in ts.values():
        t.build_top(ground)
        t.build_lip(ground)
        t.build_cliff(cliffs)
    roads = V.Asset(N + "_Roads")
    road_pts = []
    for tname, ctrl, half, taper in M.ROADS:
        road_pts += [(p.x, p.y, half) for p in
                     TR.build_road(roads, ctrl, ts[tname], half=half, taper=taper, seed=len(road_pts))]
    fields = V.Asset(N + "_Fields")
    for k, (tname, x, y, w, d, ang, kind) in enumerate(M.FIELDS):
        F.field_patch(fields, ts[tname], x, y, w, d, ang, kind, seed=k + 1)
    river, falls, foam = V.Asset(N + "_River"), V.Asset(N + "_Falls"), V.Asset(N + "_Foam")
    fs, fl = M.RIVER_FLARE
    TR.river_ribbon(river, M.RIVER, M.RIVER_HALF + 0.35, z=M.RIVER_Z, tile=4.0, seed=3, flare_start=fs,
                    flare_len=fl)
    for k, (pt, nr, width, src, reach, z_bot) in enumerate(M.FALLS):
        z_top = ts[src].height(pt[0] - nr[0] * 0.6, pt[1] - nr[1] * 0.6)
        end = TR.waterfall(falls, pt, nr, width, z_top, z_bot - 0.05, seed=k, reach=reach, stream=2.2)
        TR.foam_patch(foam, (end.x, end.y), width * 0.75, seed=k * 3 + 1, n=10, z=z_bot)
        SOCKETS.append(("mist", (end.x, end.y, z_bot + 0.15), round(width, 2)))
    for name, a in ((N + "_Ground", ground), (N + "_Cliffs", cliffs), (N + "_Roads", roads),
                    (N + "_Fields", fields), (N + "_Foam", foam)):
        me = mesh_from_asset(a, name, mat)
        meshes[name] = me
        C["Terrain"].objects.link(bpy.data.objects.new(name, me))
    for name, a, style in ((N + "_River", river, "lake"), (N + "_Falls", falls, "falls")):
        me = mesh_from_asset(a, name, wmat[style])
        meshes[name] = me
        C["Water"].objects.link(bpy.data.objects.new(name, me))

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

    placed = []
    for p in M.placements():
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
               "Bld_MineHoist": 2.2, "Env_Bridge": 3.0}.get(asset)
        spots.add(p["x"], p["y"], big * p["s"] / 0.78 if big else BA.radius_of(asset, p["s"]) + 0.15)
        # водяная мельница: колесо отдельным объектом в точке ступицы, ось колеса вдоль X мельницы
        if asset == "Bld_Watermill" and "Bld_Watermill_Wheel" in meshes:
            hub = Matrix.Rotation(math.radians(p["rz"]), 4, "Z") @ (Vector(CA.WMILL_HUB) * p["s"])
            BA.place(meshes, C["Backdrop"], "Bld_Watermill_Wheel", p["x"] + hub.x, p["y"] + hub.y, z + hub.z,
                     p["rz"] + 90.0, p["s"])
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
                t = (k + 0.5) / n
                x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                BA.place(meshes, C["Props"], "Prop_Fence", x, y, ts[tname].height(x, y), ang + rng.uniform(-3, 3),
                         tilt=(rng.uniform(-2, 2), rng.uniform(-2, 2)))
                spots.add(x, y, 0.45)

    # ---------------------------------------------------------------- россыпь
    def keep_out(tname, x, y, r):
        return tname == M.FIELD_TERRACE and P.dist_to_plot(x, y) < M.KEEP_OUT_PLOT + r * 0.6

    def scatter(tname, items, density, srange, rad, grp, wide=False):
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
            if keep_out(tname, x, y, r) or not spots.free(x, y, r):
                continue
            BA.place(meshes, C[grp], asset, x, y, t.height(x, y) - 0.02, rng.uniform(0, 360), s,
                     (rng.uniform(-3, 3), rng.uniform(-3, 3)))
            spots.add(x, y, r)
            got += 1
        return got

    for tname, items, dens, srange, rad in M.FOREST:
        print("forest %-8s %d" % (tname, scatter(tname, items, dens, srange, rad, "Nature", wide=True)))
    for tname, items, dens, srange, rad in M.GROUND:
        print("ground %-8s %d" % (tname, scatter(tname, items, dens, srange, rad, "Nature")))
    # поле стройки: плоский клевер и камешки внутри клеток (в игре лежат под зданиями)
    items, dens, srange, rad = M.PLOT_DECOR
    names, wts = [a for a, _ in items], [w for _, w in items]
    want, got, tries = int(P.W * P.H * dens), 0, 0
    while got < want and tries < want * 30:
        tries += 1
        x, y = rng.uniform(-P.HALF_W + 0.3, P.HALF_W - 0.3), rng.uniform(-P.HALF_H + 0.3, P.HALF_H - 0.3)
        if not spots.free(x, y, rad):
            continue
        asset = rng.choices(names, wts)[0]
        BA.place(meshes, C["Nature"], asset, x, y, ts[M.FIELD_TERRACE].height(x, y) - 0.004, rng.uniform(0, 360),
                 rng.uniform(*srange))
        spots.add(x, y, rad)
        got += 1
    print("plot decor", got)
    for tname, step in M.RIM_TUFTS:
        t = ts[tname]
        s_acc = 0.0
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

    # ---------------------------------------------------------------- слияние мелочи в один меш
    merge = V.Asset(N + "_Scatter")
    gone = []
    stats = {}
    for g in ("Nature", "Props"):
        for o in list(C[g].objects):
            if o.type == "MESH" and o.data.name.startswith(BA.MERGE_PREFIX + ("Res_", "Prop_Rails", "Prop_Pickaxe",
                                                                                "Prop_Minecart", "Tree_Stump", "Env_Clover",
                                                                                "Env_Pebbles", "Env_Haystack")):
                mw = o.matrix_world.copy() if o.parent else Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
                merge.add_mesh(o.data, mw)
                gone.append(o)
                o.data.calc_loop_triangles()
                stats[o.data.name] = stats.get(o.data.name, 0) + len(o.data.loop_triangles)
    for o in gone:
        bpy.data.objects.remove(o, do_unlink=True)
    me = mesh_from_asset(merge, N + "_Scatter", mat)
    meshes[N + "_Scatter"] = me
    C["Nature"].objects.link(bpy.data.objects.new(N + "_Scatter", me))
    print("merged %d scatter objects: %s" % (len(gone), ", ".join("%s %d" % kv for kv in
                                                                    sorted(stats.items(), key=lambda kv: -kv[1])[:8])))

    # ---------------------------------------------------------------- превью зданий (в игру не идёт)
    for (mesh, cx, cy, w, d, s) in PREVIEW_SETS.get(preview, []):
        if mesh not in meshes and bpy.data.meshes.get(mesh) is not None:
            meshes[mesh] = bpy.data.meshes[mesh]
        if mesh not in meshes:
            print("!! превью: нет меша", mesh)
            continue
        x, y = P.footprint_center(cx, cy, w, d)
        BA.place(meshes, C["Preview"], mesh, x, y, 0.0, 0.0, s, name="Preview_" + mesh)

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
# превью
# =========================================================================================
def render_shots(scn, cam, prev_dir, shots, res, samples, tag=""):
    os.makedirs(prev_dir, exist_ok=True)
    sizes = {"hero": (1920, 1080), "wide": (2520, 1080), "ipad": (1440, 1080), "top": (1600, 1100)}
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
            scn.camera = cam
        scn.render.resolution_x, scn.render.resolution_y = w, h
        scn.render.resolution_percentage = res
        scn.cycles.samples = samples
        path = os.path.join(prev_dir, "colony_meadow_%s%s.png" % (sh, tag))
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
    fx = BA.fx_material()
    base = os.path.join(unity, "Models", "Colony")
    colony_names = [n for c, n, _, _ in VC.asset_entries() if c == "Colony"]
    terrain = [n for n in meshes if n.startswith(N + "_")]
    used = {o.data.name for g in GROUPS for o in bpy.data.collections[PFX + g].objects if o.type == "MESH"}
    backdrop = sorted(n for n in used if n.startswith("Bld_") and n not in ARENA_BACKDROP
                      and n not in colony_names and n not in PLAYER_BUILDINGS)
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
    scn, cam, sun, ts = build_scene(V, BA, VC, meshes, mat, wmat, o["preview"])
    if o["render"]:
        prev = o["prev"] or os.path.join(root, "Previews")
        render_shots(scn, cam, prev, o["shots"], o["res"], o["samples"], o["tag"])
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
