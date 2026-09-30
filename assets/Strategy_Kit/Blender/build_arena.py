"""
Арена боя «Луг» (Arena_Meadow) для Vitaria: ассеты арены, сцена, превью, экспорт в Unity.

Запуск (как add_buildings.py):
  Blender GUI:  открыть vitaria_kit.blend > Scripting > Open > build_arena.py > Run Script
  Консоль:      blender -b vitaria_kit.blend --python build_arena.py [-- --render --no-save --no-export]
  Через bpy:    python build_arena.py --blend <путь к vitaria_kit.blend> [--render]
  Черновик:     python build_arena.py --fresh --render      (пустой файл, только превью)

Повторный запуск безопасен — это и есть способ обновить арену после правки vitaria_arena/*.py.

Что делает:
  1. палитра (+ новые цвета арены) и текстура воды -> Unity/Assets/Vitaria/Textures;
  2. ассеты арены (плитки, препятствия, реквизит, огонь, мельница, мост) -> Vitaria_Kit/Kit_Arena;
     фоновые здания из Ref_Buildings/Source (замок, дома, башни) -> Vitaria_Kit/Kit_Backdrop;
  3. сцена Arena_Meadow — отдельная сцена в том же .blend: рельеф, вода, расстановка, камера боя.
     Плитки поля в коллекции AM_Preview только для превью: в игре их кладёт BattleBoardView;
  4. превью (--render) -> Previews/arena_*.png;
  5. FBX -> Unity/Assets/Vitaria/Models/Arena (+ /Backdrop, /Meadow) и раскладка
     Unity/Assets/Vitaria/Layout/arena_meadow_layout.json в координатах Unity относительно
     середины поля. Префаб собирает Tools > Vitaria > 6. Build Arena Prefab.

Оси: в .blend арена стоит как весь кит — камера боя со стороны -Y. В игре камера боя смотрит
вдоль +Z, поэтому при выгрузке раскладка поворачивается на 180° (to_unity): X арены -> X Unity,
Y арены -> Z Unity. Сами FBX выгружаются как всегда, в начале координат.
"""
import bpy, os, sys, json, math, random, importlib
from mathutils import Matrix, Vector, Euler

SAVE, EXPORT = True, True
FBX_KW = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
              axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
              use_mesh_modifiers=True, use_triangles=True, use_tspace=False, add_leaf_bones=False,
              bake_anim=False, path_mode="STRIP", embed_textures=False, use_custom_props=False)

KIT_USED = ["Tree_Pine_A", "Tree_Pine_B", "Tree_Pine_C", "Tree_Round_A", "Tree_Round_B", "Bush_A", "Bush_B",
            "Bush_Berry", "Rock_Small", "Rock_Medium", "Rock_Large", "Tree_Stump", "Prop_Crate", "Prop_Barrel", "Prop_Sack", "Prop_Fence"]
BACKDROP = ["bld_castle", "bld_tower_round", "bld_cottage", "bld_house_stone", "bld_house_timber", "bld_watchtower"]
SCENE = "Arena_Meadow"
SCROLL = {"Arena_Meadow_Water": 0.04, "Arena_Meadow_River": 0.22, "Arena_Meadow_Falls": 0.6}
BOARD_SHARE = 0.69       # поле 9x5 (19 м) при 27 м и 16:9 занимает 69% ширины кадра
GROUPS = ["Terrain", "Water", "Backdrop", "Nature", "Props", "FX"]      # то, что уходит в игру
SOCKETS = []             # точки для частиц игры: (вид, (x, y, z) арены, размер) — заполняет build_scene
CHIMNEYS = {}            # фоновое здание -> верхушки его труб в осях модели (из вызовов chimney)
SWAY = (5.0, 0.35)       # полотна знамён: амплитуда (град) и частота (Гц) качания вокруг перекладины
GUST = 0.3               # крылья мельницы: доля порывов ветра в скорости
FX_ROLES = {             # меши эффектов по ролям — раскладка отдаёт их игре списками
    "dust": ["FX_Dust_Puff_A", "FX_Dust_Puff_B", "FX_Dust_Puff_C"],
    "smoke": ["FX_Smoke_Puff_A", "FX_Smoke_Puff_B"],
    "debrisStone": ["FX_Debris_Stone_A", "FX_Debris_Stone_B", "FX_Debris_Stone_C"],
    "debrisEarth": ["FX_Debris_Earth_A", "FX_Debris_Earth_B"],
    "debrisWood": ["FX_Debris_Wood_A", "FX_Debris_Wood_B"],
    "rock": ["FX_Rock_Throw"],
    "spark": ["FX_Spark_Shard"],
}


# =========================================================================================
def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    o = {"blend": None, "fresh": False, "render": False, "save": SAVE, "export": EXPORT, "ref": None,
         "res": 100, "samples": 48, "shots": "mock,hero", "root": None, "prev": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--blend", "--ref", "--root", "--prev", "--shots") and i + 1 < len(argv):
            o[a[2:]] = argv[i + 1]
            i += 1
        elif a in ("--res", "--samples") and i + 1 < len(argv):
            o[a[2:]] = int(argv[i + 1])
            i += 1
        elif a == "--fresh":
            o["fresh"] = True
        elif a == "--render":
            o["render"] = True
        elif a == "--no-save":
            o["save"] = False
        elif a == "--no-export":
            o["export"] = False
        i += 1
    if o["fresh"]:
        o["save"] = False if o["blend"] is None else o["save"]
    return o


def script_dir():
    cands = []
    try:
        cands.append(os.path.dirname(os.path.abspath(__file__)))
    except NameError:
        pass
    if bpy.data.filepath:
        cands.append(os.path.dirname(bpy.data.filepath))
    for d in cands:
        if os.path.exists(os.path.join(d, "vitaria_arena", "__init__.py")):
            return d
    raise RuntimeError("Не найден пакет vitaria_arena рядом со скриптом или .blend: %s" % cands)


def find_ref(here, explicit):
    for d in ([explicit] if explicit else []) + [os.path.join(here, "..", "..", "Ref_Buildings", "Source"),
                                                  os.path.join(here, "..", "Ref_Buildings", "Source")]:
        if d and os.path.exists(os.path.join(d, "parts", "bld_castle.py")):
            return os.path.normpath(d)
    return None


# =========================================================================================
# материалы
# =========================================================================================
def palette_material(V, tex_dir):
    os.makedirs(tex_dir, exist_ok=True)
    before = set(bpy.data.images)
    V.build_palette_images(tex_dir)
    for img in list(bpy.data.images):
        if img not in before and img.name.split(".")[0] in ("Vitaria_Palette", "Vitaria_Palette_Emission") \
                and "." in img.name:
            bpy.data.images.remove(img)
    for n in ("Vitaria_Palette", "Vitaria_Palette_Emission"):
        img = bpy.data.images.get(n)
        if img:
            img.reload()
    mat = bpy.data.materials.get("Vitaria_Palette")
    if mat is None:
        pal = bpy.data.images.get("Vitaria_Palette") or bpy.data.images.load(os.path.join(tex_dir, "Vitaria_Palette.png"))
        emi = bpy.data.images.get("Vitaria_Palette_Emission") or \
            bpy.data.images.load(os.path.join(tex_dir, "Vitaria_Palette_Emission.png"))
        mat = V.make_material(pal, emi)
    return mat


WATER_MATS = {"lake": "Vitaria_Water", "falls": "Vitaria_Waterfall"}


def water_materials(TR, tex_dir):
    """Два материала воды с одной раскладкой UV: спокойная (озеро, река) и струи водопадов.
    Имя материала = имя PNG; в Unity постпроцессор переназначает оба по имени."""
    out = {}
    for style, name in WATER_MATS.items():
        path = os.path.join(tex_dir, name + ".png")
        TR.make_water_texture(path, style)
        img = bpy.data.images.get(name)
        if img is None:
            img = bpy.data.images.load(path)
            img.name = name
        else:
            img.filepath = path
            img.reload()
        mat = bpy.data.materials.get(name)
        if mat is None:
            mat = bpy.data.materials.new(name)
            mat.use_nodes = True
            nt = mat.node_tree
            bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            t = nt.nodes.new("ShaderNodeTexImage")
            t.location = (-420, 200)
            t.interpolation = "Linear"
            nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.32 if style == "lake" else 0.5
            bsdf.inputs["Specular IOR Level"].default_value = 0.45 if style == "lake" else 0.25
            if style == "falls":                      # струи чуть светятся — как пена на макете
                nt.links.new(t.outputs["Color"], bsdf.inputs["Emission Color"])
                bsdf.inputs["Emission Strength"].default_value = 0.08
        tex = next(n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE")
        tex.image = img
        out[style] = mat
    return out


# =========================================================================================
# ассеты
# =========================================================================================
def ensure_coll(name, parent):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
    if parent is not None and c.name not in [x.name for x in parent.children]:
        parent.children.link(c)
    return c


def build_mesh(V, name, fn, mats):
    a = V.Asset(name)
    fn(a)
    me = bpy.data.meshes.get(name)
    if me is None:
        me = bpy.data.meshes.new(name)
    a.bm.to_mesh(me)
    a.bm.free()
    if isinstance(mats, (list, tuple)):
        me.materials.clear()
        for m in mats:
            me.materials.append(m)
    elif not me.materials:
        me.materials.append(mats)
    me.validate()
    me.update()
    return me


def kit_object(me, coll, tag, title=""):
    o = bpy.data.objects.get(me.name)
    if o is None or o.data is not me:
        o = bpy.data.objects.new(me.name, me)
    if coll is not None and o.name not in coll.objects:
        coll.objects.link(o)
    if o.asset_data is None:
        try:
            o.asset_mark()
            o.asset_data.tags.new(tag)
            o.asset_data.description = title
        except Exception as ex:
            print("asset mark skipped:", ex)
    return o


def build_assets(V, VA, ref_dir, mat, kit_root, fresh):
    meshes = {}
    # базовый кит: в .blend уже есть; в черновике собираем из генератора
    kit_fns = {n: fn for _, n, fn in V.KIT}
    for n in KIT_USED:
        me = bpy.data.meshes.get(n)
        if me is None or fresh:
            me = build_mesh(V, n, kit_fns[n], mat)
        meshes[n] = me
    kit_arena = ensure_coll("Kit_Arena", kit_root)
    for cat, n, title, fn in VA.asset_entries(reload=True):
        me = build_mesh(V, n, fn, mat)
        meshes[n] = me
        kit_object(me, kit_arena, "Arena", title)
        print("arena asset %-20s" % n)
    kit_bd = ensure_coll("Kit_Backdrop", kit_root)
    if ref_dir:
        if ref_dir not in sys.path:
            sys.path.insert(0, ref_dir)
        # верхушки труб записываем прямо из вызовов parts.common.chimney — точки дыма для игры
        import parts.common as PC
        orig = getattr(PC, "_orig_chimney", None) or PC.chimney
        PC._orig_chimney = orig
        rec = []

        def chimney_rec(a, x, y, zbase, h=1.05, w=0.42, col="stone_mid"):
            rec.append(Vector((x, y, zbase + h + 0.03)))
            return orig(a, x, y, zbase, h=h, w=w, col=col)

        PC.chimney = chimney_rec
        try:
            for mod_name in BACKDROP:
                m = importlib.import_module("parts." + mod_name)
                m = importlib.reload(m)
                rec.clear()
                me = build_mesh(V, m.NAME, m.build, mat)
                CHIMNEYS[m.NAME] = list(rec)
                meshes[m.NAME] = me
                kit_object(me, kit_bd, "Backdrop", "Фон арены (Ref_Buildings)")
                print("backdrop      %-20s  труб: %d" % (m.NAME, len(rec)))
        finally:
            PC.chimney = orig
    else:
        print("!! Ref_Buildings/Source не найден — фон без замка и домов")
    return meshes


# =========================================================================================
# витрина кита: ряды арены и фона под рядами build_vitaria (Buildings 0, Nature -12,
# Props -25, Resources -33), те же подписи и отступы
# =========================================================================================
SHOW_ROWS = [("Kit_Arena", 60.0, -44.0, 0.8, 0.2, 99, 6.0), ("Kit_Backdrop", 60.0, -55.0, 1.2, 0.3, 99, 8.0)]
SHOW_ORDER = ["Hex_Tile_A", "Hex_Tile_B", "Hex_Tile_C", "Hex_Tile_Blue", "Hex_Tile_Red", "Obst_Boulders",
              "Obst_Stump", "Obst_Bush", "Prop_Brazier", "FX_Flame_Small", "Prop_Campfire", "FX_Flame_Large",
              "Prop_Banner_Blue", "Prop_Banner_Blue_Cloth", "Prop_Banner_Red", "Prop_Banner_Red_Cloth",
              "Prop_WeaponRack", "Prop_Tent", "Bld_Windmill", "Bld_Windmill_Sails", "Env_Bridge",
              "FX_Dust_Puff_A", "FX_Dust_Puff_B", "FX_Dust_Puff_C", "FX_Smoke_Puff_A", "FX_Smoke_Puff_B",
              "FX_Debris_Stone_A", "FX_Debris_Stone_B", "FX_Debris_Stone_C", "FX_Debris_Earth_A",
              "FX_Debris_Earth_B", "FX_Debris_Wood_A", "FX_Debris_Wood_B", "FX_Rock_Throw", "FX_Spark_Shard",
              "Env_Tuft_A", "Env_Tuft_B", "Env_Flowers_A"]


def showcase(kit_root):
    show = bpy.data.collections.get("Showcase")
    lab = bpy.data.materials.get("Label")
    if show is None:
        return
    for cname, x0, y0, gap, lsize, per_row, lsp in SHOW_ROWS:
        coll = bpy.data.collections.get(cname)
        if coll is None:
            continue
        objs = [o for o in coll.objects if o.type == "MESH"]
        rank = {n: i for i, n in enumerate(SHOW_ORDER)}
        objs.sort(key=lambda o: (rank.get(o.name, 999), o.name))
        x = x0
        for k, o in enumerate(objs):
            bb = [Vector(v) for v in o.bound_box]
            w = max(max(v.x for v in bb) - min(v.x for v in bb), len(o.name) * lsize * 0.62)
            yy = y0 - (k // per_row) * lsp
            if k and k % per_row == 0:
                x = x0
            o.location = (x + w / 2 - (max(v.x for v in bb) + min(v.x for v in bb)) / 2, yy, 0)
            lbl = bpy.data.objects.get(o.name + "_lbl")
            if lbl is None:
                cu = bpy.data.curves.new(o.name + "_lbl", "FONT")
                cu.body = o.name
                cu.size = lsize
                cu.align_x = "CENTER"
                if lab:
                    cu.materials.append(lab)
                lbl = bpy.data.objects.new(o.name + "_lbl", cu)
                show.objects.link(lbl)
            lbl.location = (x + w / 2, yy + min(v.y for v in bb) - lsize * 1.4, 0.005)
            x += w + gap


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
        if c.name.startswith("AM_"):
            bpy.data.collections.remove(c)
    for n in ("AM_Camera", "AM_Sun"):
        for coll in (bpy.data.cameras, bpy.data.lights):
            x = coll.get(n)
            if x is not None and x.users == 0:
                coll.remove(x)
    scn = bpy.data.scenes.new(SCENE)
    scn.unit_settings.system = "METRIC"
    colls = {}
    for g in GROUPS + ["Preview", "Rig"]:
        c = bpy.data.collections.new("AM_" + g)
        scn.collection.children.link(c)
        colls[g] = c
    return scn, colls


def view_region(cam, z, aspect=2.4, margin=3.0):
    """След кадра на плоскости z (с запасом по бокам под 21:9) — где вообще имеет смысл сажать лес."""
    p, f = math.radians(cam["pitch"]), math.radians(cam["fov"])
    T = Vector(cam["target"])
    C = T + cam["distance"] * Vector((0, -math.cos(p), math.sin(p)))
    fw = Vector((0, math.cos(p), -math.sin(p)))
    rt = Vector((1, 0, 0))
    up = Vector((0, math.sin(p), math.cos(p)))
    tv = math.tan(f / 2) * 1.15
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


def place(meshes, coll, asset, x, y, z, rz=0.0, s=1.0, tilt=(0.0, 0.0), name=None):
    o = bpy.data.objects.new(name or asset, meshes[asset])
    o.location = (x, y, z)
    o.rotation_euler = Euler((math.radians(tilt[0]), math.radians(tilt[1]), math.radians(rz)))
    o.scale = (s, s, s)
    coll.objects.link(o)
    return o


NATURE = ("Tree_", "Bush_", "Rock_", "Grass_", "Flowers_", "Env_Tuft", "Env_Flowers")
MERGE_PREFIX = NATURE + ("Prop_Fence", "Prop_Barrel", "Prop_Crate", "Prop_Sack")
MERGE_STATS = {}


def radius_of(asset, s):
    base = {"Tree_Pine": 0.95, "Tree_Round": 1.0, "Bush_": 0.55, "Rock_Large": 0.9, "Rock_Medium": 0.5,
            "Rock_Small": 0.25, "Grass_": 0.14, "Flowers_": 0.16, "Env_Tuft": 0.16, "Env_Flowers": 0.18,
            "Tree_Stump": 0.4}
    for k, v in base.items():
        if asset.startswith(k):
            return v * s
    return 0.5 * s


class Spots:
    """Занятые круги (x, y, r) с сеткой-хешем — для проверки расстояний при россыпи."""

    def __init__(self, cell=2.0):
        self.cell, self.grid = cell, {}

    def add(self, x, y, r):
        k = (int(math.floor(x / self.cell)), int(math.floor(y / self.cell)))
        self.grid.setdefault(k, []).append((x, y, r))

    def free(self, x, y, r):
        cx, cy = int(math.floor(x / self.cell)), int(math.floor(y / self.cell))
        reach = int(math.ceil((r + 2.5) / self.cell))
        for i in range(cx - reach, cx + reach + 1):
            for j in range(cy - reach, cy + reach + 1):
                for (ox, oy, orr) in self.grid.get((i, j), ()):
                    if (ox - x) ** 2 + (oy - y) ** 2 < (orr + r) ** 2:
                        return False
        return True


def build_scene(V, VA, meshes, mat, wmat):
    mods = VA.load()
    M, TR, B = mods["meadow"], mods["terrain"], mods["board"]
    LM = mods["landmarks"]
    scn, C = reset_scene()
    rng = random.Random(77)
    SOCKETS.clear()
    PR = mods["props"]
    TR.VIEW_BOX = getattr(M, "VIEW_BOX", None)          # отсечение рельефа за пределами всех кадров

    # ---------------------------------------------------------------- рельеф
    ts = M.terraces()
    for name, occ in M.OCCLUDERS.items():
        ts[name].occluders = [ts[o].outline for o in occ]
    ground, cliffs = V.Asset("Arena_Meadow_Ground"), V.Asset("Arena_Meadow_Cliffs")
    for t in ts.values():
        t.build_top(ground)
        t.build_lip(ground)
        t.build_cliff(cliffs)
    roads = V.Asset("Arena_Meadow_Roads")
    road_pts = []
    for tname, ctrl, half in M.ROADS:
        road_pts += [(p.x, p.y, half) for p in TR.build_road(roads, ctrl, ts[tname], half=half, seed=len(road_pts))]
    water, river = V.Asset("Arena_Meadow_Water"), V.Asset("Arena_Meadow_River")
    falls, foam = V.Asset("Arena_Meadow_Falls"), V.Asset("Arena_Meadow_Foam")
    W = M.WATER
    TR.water_plane(water, W["x0"], W["x1"], W["y0"], W["y1"], flow=W["flow"], tile=W["tile"])
    TR.river_ribbon(river, M.RIVER, M.RIVER_HALF + 0.35, z=TR.RIVER_Z, tile=4.0, seed=3)
    for k, (pt, nr, width, src, reach) in enumerate(M.FALLS):
        if src == "river":
            z_top, stream = TR.RIVER_Z, 0.5
        else:
            z_top, stream = ts[src].height(pt[0] - nr[0] * 0.6, pt[1] - nr[1] * 0.6), 2.2
        end = TR.waterfall(falls, pt, nr, width, z_top, TR.WATER_Z - 0.05, seed=k, reach=reach, stream=stream)
        TR.foam_patch(foam, (end.x, end.y), width * 0.75, seed=k * 3 + 1, n=10)
        SOCKETS.append(("mist", (end.x, end.y, TR.WATER_Z + 0.15), round(width, 2)))
    for name, a in (("Arena_Meadow_Ground", ground), ("Arena_Meadow_Cliffs", cliffs), ("Arena_Meadow_Roads", roads),
                    ("Arena_Meadow_Foam", foam)):
        me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        a.bm.to_mesh(me)
        a.bm.free()
        me.materials.clear()
        me.materials.append(mat)
        me.validate()
        meshes[name] = me
        o = bpy.data.objects.new(name, me)
        C["Terrain"].objects.link(o)
    for name, a, style in (("Arena_Meadow_Water", water, "lake"), ("Arena_Meadow_River", river, "lake"),
                           ("Arena_Meadow_Falls", falls, "falls")):
        me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        a.bm.to_mesh(me)
        a.bm.free()
        me.materials.clear()
        me.materials.append(wmat[style])
        me.validate()
        meshes[name] = me
        o = bpy.data.objects.new(name, me)
        C["Water"].objects.link(o)

    # ---------------------------------------------------------------- расстановка
    spots = Spots()
    for (x, y, h) in road_pts[::2]:
        spots.add(x, y, h + 0.35)

    placed = []
    for p in M.placements():
        asset = p["asset"]
        if asset not in meshes:
            print("!! нет ассета", asset)
            continue
        on = p["on"]
        if asset == "Env_Bridge":
            z = ts[on].z + (p["z"] or 0.0)          # мост — по уровню берега, а не по рельефу под ним
        elif p["z"] is not None:
            z = ts[on].height(p["x"], p["y"]) + p["z"]
        else:
            z = ts[on].height(p["x"], p["y"])
        grp = "Backdrop" if asset.startswith(("Bld_", "Env_")) else ("Nature" if asset.startswith(NATURE) else "Props")
        o = place(meshes, C[grp], asset, p["x"], p["y"], z - (0.02 if grp == "Nature" else 0.0), p["rz"], p["s"], p["tilt"])
        placed.append(o)
        r = {"Bld_Castle": 4.6, "Bld_Tower_Round": 1.9, "Bld_Cottage": 2.1, "Bld_House_Stone": 2.2,
             "Bld_House_Timber": 2.3, "Bld_WatchTower": 1.6, "Bld_Windmill": 2.1, "Prop_Tent": 1.7}.get(asset)
        spots.add(p["x"], p["y"], r if r else radius_of(asset, p["s"]) + 0.15)
        # мельница: крылья отдельным объектом в точке ступицы
        if asset == "Bld_Windmill":
            hub = Matrix.Rotation(math.radians(p["rz"]), 4, "Z") @ (Vector(LM.MILL_HUB) * p["s"])
            sl = place(meshes, C["Backdrop"], "Bld_Windmill_Sails", p["x"] + hub.x, p["y"] + hub.y, z + hub.z, 0.0,
                       p["s"])
            sl.rotation_euler = Euler((0.0, math.radians(18.0), math.radians(p["rz"])), "XYZ")
        if asset == "Prop_Brazier":
            place(meshes, C["FX"], "FX_Flame_Small", p["x"], p["y"], z + 1.08, rng.uniform(0, 360))
            SOCKETS.append(("embers", (p["x"], p["y"], z + 1.3), 0.6))
        if asset == "Prop_Campfire":
            place(meshes, C["FX"], "FX_Flame_Large", p["x"], p["y"], z + 0.06, rng.uniform(0, 360))
            SOCKETS.append(("embers", (p["x"], p["y"], z + 0.45), 0.9))
            SOCKETS.append(("smoke", (p["x"], p["y"], z + 1.05), 1.0))
        # знамя: полотно отдельным объектом в точке подвеса, качается вокруг перекладины
        if asset.startswith("Prop_Banner_") and asset + "_Cloth" in meshes:
            hang = Matrix.Rotation(math.radians(p["rz"]), 4, "Z") @ (Vector(PR.BANNER_HANG) * p["s"])
            place(meshes, C["Props"], asset + "_Cloth", p["x"] + hang.x, p["y"] + hang.y, z + hang.z, p["rz"], p["s"])
        if asset == "Env_Bridge":
            spots.add(p["x"], p["y"], 3.4)

    # трубы домов фона: верх каждой трубы — сокет дыма (грань swatch-а black, смотрит вверх, выше 1.5 м)
    for o in placed:
        if o.data.name.startswith("Bld_") and not o.data.name.startswith("Bld_Windmill"):
            mw = Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
            for q in CHIMNEYS.get(o.data.name, []):
                w = mw @ q
                SOCKETS.append(("smoke", (w.x, w.y, w.z + 0.08), round(0.7 * o.scale.x, 2)))

    # заборы вдоль ломаных (сегмент Prop_Fence = 1 м вдоль X, лицом -Y)
    for tname, pts in M.FENCES:
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            L = math.hypot(x1 - x0, y1 - y0)
            n = max(1, int(round(L / 1.0)))
            ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
            for k in range(n):
                t = (k + 0.5) / n
                x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                place(meshes, C["Props"], "Prop_Fence", x, y, ts[tname].height(x, y), ang + rng.uniform(-3, 3),
                      tilt=(rng.uniform(-2, 2), rng.uniform(-2, 2)))
                spots.add(x, y, 0.45)

    # ---------------------------------------------------------------- россыпь природы
    def keep_out(tname, x, y, r):
        if tname == "arena" and B.dist_to_board(x, y) < M.KEEP_OUT_BOARD + r * 0.6:
            return True
        return False

    def scatter(tname, items, density, srange, rad, grp, wide=False):
        t = ts[tname]
        if wide and TR.VIEW_BOX is not None:            # деревья: вся рамка видимости (и 4:3, и 21:9)
            bx0, bx1, by0, by1 = TR.VIEW_BOX
            region = [Vector((bx0, by0, 0)), Vector((bx1, by0, 0)), Vector((bx1, by1, 0)), Vector((bx0, by1, 0))]
        else:                                           # мелочь: только кадр боевой камеры до 21:9
            region = view_region(M.CAMERA, t.z)
        x0, x1, y0, y1 = t.bbox
        rx = [q.x for q in region]
        ry = [q.y for q in region]
        x0, x1 = max(x0, min(rx)), min(x1, max(rx))
        y0, y1 = max(y0, min(ry)), min(y1, max(ry))
        if x1 <= x0 or y1 <= y0:
            return 0
        names = [a for a, _ in items]
        wts = [w for _, w in items]
        area = (x1 - x0) * (y1 - y0)
        want = int(area * density)
        got, tries = 0, 0
        while got < want and tries < want * 25:
            tries += 1
            x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
            if not TR.point_in_poly(x, y, region) or not t.contains(x, y):
                continue
            if any(TR.point_in_poly(x, y, occ) or TR.dist_to_outline(x, y, occ) < 0.9 for occ in t.occluders):
                continue                                        # под плато / под скалой соседки
            asset = rng.choices(names, wts)[0]
            s = rng.uniform(*srange)
            r = radius_of(asset, s) if asset.startswith(("Tree_", "Bush_", "Rock_")) else rad
            margin = 0.35 if asset.startswith(("Grass_", "Flowers_", "Env_Tuft", "Env_Flowers")) else 0.55 * r
            if t.rim(x, y) < margin:
                continue
            if keep_out(tname, x, y, r) or not spots.free(x, y, r):
                continue
            place(meshes, C[grp], asset, x, y, t.height(x, y) - 0.02, rng.uniform(0, 360), s,
                  (rng.uniform(-3, 3), rng.uniform(-3, 3)))
            spots.add(x, y, r)
            got += 1
        return got

    for tname, items, dens, srange, rad in M.FOREST:
        print("forest %-11s %d" % (tname, scatter(tname, items, dens, srange, rad, "Nature", wide=True)))
    for tname, items, dens, srange, rad in M.GROUND:
        print("ground %-11s %d" % (tname, scatter(tname, items, dens, srange, rad, "Nature")))
    # пучки травы на самой кромке, наклонены наружу — «свес» дёрна как на макете
    for tname, step in M.RIM_TUFTS:
        t = ts[tname]
        s_acc = 0.0
        for i in range(len(t.outline)):
            p0, p1 = t.outline[i], t.outline[(i + 1) % len(t.outline)]
            s_acc += (p1 - p0).length
            if s_acc < step:
                continue
            s_acc = rng.uniform(-0.25, 0.1) * step
            n = t.normals[i]
            q = p0 + n * 0.06
            if not TR.in_view(q.x, q.y) or not TR.point_in_poly(q.x, q.y, view_region(M.CAMERA, t.z)):
                continue                                    # кромку видно только в кадре боя
            if not spots.free(q.x, q.y, 0.12):
                continue
            if any(TR.point_in_poly(q.x, q.y, occ) or TR.dist_to_outline(q.x, q.y, occ) < 0.5 for occ in t.occluders):
                continue
            asset = rng.choice(["Env_Tuft_A", "Env_Tuft_B", "Env_Tuft_B"])
            o = place(meshes, C["Nature"], asset, q.x, q.y, t.z - 0.05, 0.0, rng.uniform(1.0, 1.5))
            axis = Vector((-n.y, n.x, 0.0))
            o.matrix_world = Matrix.Translation(o.location) @ Matrix.Rotation(math.radians(rng.uniform(25, 45)), 4, axis) @ \
                Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z") @ Matrix.Scale(o.scale.x, 4)

    # ---------------------------------------------------------------- слияние мелочи
    # Деревья, кусты, камни, трава, цветы, заборы, бочки, ящики и мешки — статичный декор: сотни
    # объектов сливаются в один меш Arena_Meadow_Scatter (одна отрисовка с общим материалом).
    # Отдельными остаются здания, мост, мельница, крылья, реквизит лагерей и огонь.
    merge = V.Asset("Arena_Meadow_Scatter")
    gone = []
    for g in ("Nature", "Props"):
        for o in list(C[g].objects):
            if o.type == "MESH" and o.data.name.startswith(MERGE_PREFIX):
                mw = Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
                merge.add_mesh(o.data, mw)
                gone.append(o)
                o.data.calc_loop_triangles()
                MERGE_STATS[o.data.name] = MERGE_STATS.get(o.data.name, 0) + len(o.data.loop_triangles)
    for o in gone:
        bpy.data.objects.remove(o, do_unlink=True)
    me = bpy.data.meshes.get("Arena_Meadow_Scatter") or bpy.data.meshes.new("Arena_Meadow_Scatter")
    merge.bm.to_mesh(me)
    merge.bm.free()
    me.materials.clear()
    me.materials.append(mat)
    me.validate()
    meshes["Arena_Meadow_Scatter"] = me
    C["Nature"].objects.link(bpy.data.objects.new("Arena_Meadow_Scatter", me))
    print("merged %d scatter objects: %s" % (len(gone), ", ".join(
        "%s %d" % kv for kv in sorted(MERGE_STATS.items(), key=lambda kv: -kv[1]))))

    # ---------------------------------------------------------------- превью поля (в игру не идёт)
    zones = {"player": "Hex_Tile_Blue", "enemy": "Hex_Tile_Red"}
    for (cx, cy) in B.cells():
        x, y = B.cell_center(cx, cy)
        kind = zones.get(B.zone(cx, cy))
        if kind is None:
            kind = ["Hex_Tile_A", "Hex_Tile_A", "Hex_Tile_B", "Hex_Tile_C"][(cx * 7 + cy * 13) % 4]
        place(meshes, C["Preview"], kind, x, y, 0.0, 60 * ((cx * 5 + cy * 3) % 6), name="Cell_%d_%d" % (cx, cy))

    # ---------------------------------------------------------------- камера, солнце, мир
    cam = M.CAMERA
    cd = bpy.data.cameras.new("AM_Camera")
    # FOV задан по вертикали, как Camera.fieldOfView в Unity: объектив считаем от высоты сенсора
    # (свойство angle у Blender берёт ширину сенсора и даёт кадр в полтора раза уже)
    cd.sensor_fit = "VERTICAL"
    cd.sensor_height = 24.0
    cd.lens = 12.0 / math.tan(math.radians(cam["fov"]) / 2)
    cd.clip_start, cd.clip_end = 0.3, 300
    co = bpy.data.objects.new("AM_Camera", cd)
    p = math.radians(cam["pitch"])
    T = Vector(cam["target"])
    co.location = T + cam["distance"] * Vector((0, -math.cos(p), math.sin(p)))
    co.rotation_euler = (T - co.location).to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(co)
    scn.camera = co
    sd = bpy.data.lights.new("AM_Sun", "SUN")
    sd.energy = M.SUN["energy"]
    sd.color = M.SUN["color"]
    sd.angle = math.radians(3)
    so = bpy.data.objects.new("AM_Sun", sd)
    so.rotation_euler = Vector(M.SUN["direction"]).normalized().to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(so)
    world = bpy.data.worlds.get("AM_World") or bpy.data.worlds.new("AM_World")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    # небо как трёхцветный ambient Unity (Setup Lighting): тени светлые, как на макете
    bg.inputs[0].default_value = (0.56, 0.68, 0.84, 1)
    bg.inputs[1].default_value = 1.05
    scn.world = world
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.use_denoising = True
    scn.cycles.max_bounces = 4
    scn.view_settings.view_transform = "Standard"
    scn.view_settings.look = "None"
    scn.view_settings.exposure = -0.3            # как освещённость боя в Unity (солнце 1.25 + ambient)
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGB"
    # сцена арены не активна в окне: без явного пересчёта matrix_world у всех объектов единичная,
    # и раскладка ушла бы в Unity с нулевыми позициями
    for vl in scn.view_layers:
        vl.update()
    return scn, co, so


# =========================================================================================
# превью
# =========================================================================================
def render_shots(scn, cam, prev_dir, shots, res, samples):
    os.makedirs(prev_dir, exist_ok=True)
    sizes = {"mock": (1672, 941), "hero": (1920, 1080), "wide": (2520, 1080), "ipad": (1600, 1200),
             "top": (1600, 1400)}
    out = []
    top_cam = None
    for sh in [s for s in shots.split(",") if s]:
        if sh not in sizes:
            continue
        w, h = sizes[sh]
        if sh == "top":                              # план сверху для отладки раскладки
            if top_cam is None:
                td = bpy.data.cameras.new("AM_TopCam")
                td.type = "ORTHO"
                td.ortho_scale = 56
                td.clip_end = 300
                top_cam = bpy.data.objects.new("AM_TopCam", td)
                top_cam.location = (2.0, 4.0, 60.0)
                scn.collection.objects.link(top_cam)
            scn.camera = top_cam
        else:
            scn.camera = cam
        scn.render.resolution_x, scn.render.resolution_y = w, h
        scn.render.resolution_percentage = res
        scn.cycles.samples = samples
        path = os.path.join(prev_dir, "arena_meadow_%s.png" % sh)
        scn.render.filepath = path
        if bpy.context.window is not None:              # в GUI рендерим активную сцену окна
            bpy.context.window.scene = scn
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
CU = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))     # Blender -> Unity (как в ките)
R180 = Matrix.Rotation(math.pi, 4, "Z")                                    # арена: камера боя смотрит вдоль +Z


def to_unity(mw):
    Mu = CU @ R180 @ mw @ CU.inverted()
    loc, rot, scl = Mu.decompose()
    return [round(v, 4) for v in loc], [round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)], \
           [round(v, 4) for v in scl]


def to_unity_vec(v, point=True):
    w = (CU @ R180).to_3x3() @ Vector(v)
    return [round(w.x, 5), round(w.y, 5), round(w.z, 5)]


def export_mesh(me, path, mat_names=None):
    """FBX одного меша в начале координат (временный объект в текущей сцене)."""
    scn = bpy.context.scene
    o = bpy.data.objects.new(me.name, me)
    scn.collection.objects.link(o)
    bpy.context.view_layer.update()
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, **FBX_KW)
    bpy.data.objects.remove(o, do_unlink=True)


def tris_size(me):
    me.calc_loop_triangles()
    xs, ys, zs = zip(*[tuple(v.co) for v in me.vertices])
    return len(me.loop_triangles), [round(max(xs) - min(xs), 3), round(max(zs) - min(zs), 3), round(max(ys) - min(ys), 3)]


def fx_material():
    """Копия палитры под именем Vitaria_FX: в Unity на неё ремапится материал с включённой эмиссией."""
    m = bpy.data.materials.get("Vitaria_FX")
    if m is None:
        m = bpy.data.materials["Vitaria_Palette"].copy()
        m.name = "Vitaria_FX"
    return m


def split_emissive(V, me, fx):
    """Грани на светящихся swatch-ах (огонь, угли, искры) -> второй материал Vitaria_FX. В Unity у
    Vitaria_Palette эмиссия выключена (колония), а у Vitaria_FX включена: светится только огонь арены."""
    cells = [V.SW_UV[n] for n in V.EMISSIVE]
    uv = me.uv_layers.active.data if me.uv_layers.active else None
    if uv is None:
        return 0
    hot = []
    for poly in me.polygons:
        u, v = uv[poly.loop_start].uv
        if any(abs(u - cu) < 1e-4 and abs(v - cv) < 1e-4 for cu, cv in cells):
            hot.append(poly.index)
    if not hot:
        return 0
    names = [m.name if m else "" for m in me.materials]
    if len(hot) == len(me.polygons):                  # весь меш светится — один материал FX
        me.materials.clear()
        me.materials.append(fx)
        for poly in me.polygons:
            poly.material_index = 0
        return len(hot)
    if fx.name not in names:
        me.materials.append(fx)
        names.append(fx.name)
    fi = names.index(fx.name)
    for i in hot:
        me.polygons[i].material_index = fi
    return len(hot)


def export_all(VA, meshes, scn, cam, sun, unity, root):
    M = VA.load()["meadow"]
    import build_vitaria as V
    fx = fx_material()
    base = os.path.join(unity, "Models", "Arena")
    info = []
    arena_names = [n for c, n, _, _ in VA.asset_entries() if c == "Arena"]      # декор плато — только в Scatter
    backdrop = [n for n in meshes if n.startswith("Bld_") and n not in arena_names]
    terrain = [n for n in meshes if n.startswith("Arena_Meadow_")]
    for n in arena_names:
        hot = split_emissive(V, meshes[n], fx)
        if hot:
            print("fx faces %-24s %d" % (n, hot))
        export_mesh(meshes[n], os.path.join(base, n + ".fbx"))
    for n in backdrop:
        export_mesh(meshes[n], os.path.join(base, "Backdrop", n + ".fbx"))
    for n in terrain:
        export_mesh(meshes[n], os.path.join(base, "Meadow", n + ".fbx"))
    for n in arena_names + backdrop + terrain:
        t, sz = tris_size(meshes[n])
        cat = "Arena" if n in arena_names else ("Backdrop" if n in backdrop else "Meadow")
        info.append({"asset": n, "category": cat, "tris": t, "size_unity": sz})
        print("fbx %-26s %6d tris" % (n, t))

    # раскладка: всё из коллекций GROUPS, координаты Unity относительно середины поля
    items = []
    total = 0
    for g in GROUPS:
        for o in bpy.data.collections["AM_" + g].objects:
            if o.type != "MESH":
                continue
            p, q, s = to_unity(o.matrix_world)
            it = {"name": o.name, "asset": o.data.name, "group": g, "p": p, "r": q, "s": s}
            if o.data.name == "Bld_Windmill_Sails":
                it["spin"] = 16.0                  # град/с вокруг локальной оси ступицы
                it["gust"] = GUST                  # доля порывов: скорость гуляет ±30% по шуму
            if o.data.name.endswith("_Cloth"):
                # качание вокруг локальной X (перекладина): амплитуда, частота, фаза 0..1
                it["sway"] = [SWAY[0], SWAY[1], round((sum(map(ord, o.name)) % 97) / 97.0, 3)]
            if o.data.name.startswith("FX_Flame"):
                it["flicker"] = 1.0
            if o.data.name in SCROLL:
                it["scroll"] = SCROLL[o.data.name]          # повторов текстуры в секунду вдоль V
            items.append(it)
            o.data.calc_loop_triangles()
            total += len(o.data.loop_triangles)
    cw = cam.matrix_world
    fwd = cw.to_3x3() @ Vector((0, 0, -1))
    up = cw.to_3x3() @ Vector((0, 1, 0))
    sun_fwd = sun.matrix_world.to_3x3() @ Vector((0, 0, -1))
    import build_vitaria as V
    layout = {
        "name": M.NAME,
        "kitVersion": V.KIT_VERSION,
        "board": {"width": 9, "height": 5, "hexAcrossFlats": 2.0,
                  "note": "координаты относительно середины поля (BattleBoardView.middle)"},
        "objects": items,
        "camera": {"position": to_unity_vec(cw.translation), "forward": to_unity_vec(fwd), "up": to_unity_vec(up),
                   "fov": M.CAMERA["fov"], "referenceAspect": round(M.CAMERA["aspect"], 4)},
        # кадр боя для игры: наклон и FOV, дистанция при 16:9, точка фокуса перед серединой поля
        # (по оси глубины) и доля ширины кадра под поле — на узких экранах камера отъезжает
        "framing": {"pitch": M.CAMERA["pitch"], "fov": M.CAMERA["fov"], "distance": M.CAMERA["distance"],
                    "focusOffset": M.CAMERA["target"][1], "boardWidthShare": BOARD_SHARE},
        "sun": {"forward": to_unity_vec(sun_fwd), "color": list(M.SUN["color"]), "intensity": 1.3},
        "ambient": {"sky": [0.64, 0.72, 0.82], "equator": [0.52, 0.58, 0.5], "ground": [0.28, 0.26, 0.24]},
        "background": [0.33, 0.45, 0.6],
        "tiles": {"neutral": ["Hex_Tile_A", "Hex_Tile_A", "Hex_Tile_B", "Hex_Tile_C"], "player": "Hex_Tile_Blue",
                  "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]},
        # меши эффектов (Models/Arena/FX_*.fbx) по ролям и точки частиц окружения
        "fx": FX_ROLES,
        "sockets": [{"kind": k, "p": to_unity_vec(p), "size": sz} for k, p, sz in SOCKETS],
        "tris": total,
    }
    lay_dir = os.path.join(unity, "Layout")
    os.makedirs(lay_dir, exist_ok=True)
    with open(os.path.join(lay_dir, "arena_meadow_layout.json"), "w", encoding="utf-8") as f:
        json.dump(layout, f, indent=1, ensure_ascii=False)
    print("layout: %d objects, %d tris in game view" % (len(items), total))
    # asset_list.json: строки арены заменяются, остальное как было
    list_path = os.path.join(root, "asset_list.json")
    rows = []
    if os.path.exists(list_path):
        try:
            rows = json.load(open(list_path, encoding="utf-8"))
        except Exception:
            rows = []
    names = {x["asset"] for x in info}
    rows = [r for r in rows if r.get("asset") not in names and r.get("category") not in ("Arena", "Backdrop", "Meadow")]
    rows += info
    with open(list_path, "w", encoding="utf-8") as f:
        json.dump(rows, f, indent=1)
    return layout


# =========================================================================================
def main():
    o = parse_args()
    if o["blend"]:
        bpy.ops.wm.open_mainfile(filepath=o["blend"])
    elif o["fresh"]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    here = script_dir()
    if here not in sys.path:
        sys.path.insert(0, here)
    import build_vitaria as V
    V = importlib.reload(V)
    import vitaria_arena as VA
    VA = importlib.reload(VA)
    VA.load(reload=True)
    root = os.path.normpath(o["root"] or os.path.join(here, ".."))
    unity = os.path.join(root, "Unity", "Assets", "Vitaria")
    tex_dir = os.path.join(unity, "Textures")
    mods = VA.load()
    mat = palette_material(V, tex_dir)
    wmat = water_materials(mods["terrain"], tex_dir)
    kit_root = bpy.data.collections.get("Vitaria_Kit")
    if kit_root is None:
        kit_root = bpy.data.collections.new("Vitaria_Kit")
        bpy.context.scene.collection.children.link(kit_root)
    ref_dir = find_ref(here, o["ref"])
    meshes = build_assets(V, VA, ref_dir, mat, kit_root, o["fresh"])
    if not o["fresh"]:
        showcase(kit_root)
    scn, cam, sun = build_scene(V, VA, meshes, mat, wmat)
    if o["render"]:
        prev = o["prev"] or os.path.join(root, "Previews")
        render_shots(scn, cam, prev, o["shots"], o["res"], o["samples"])
    if o["save"] and bpy.data.filepath:
        bpy.ops.wm.save_mainfile()
        print("saved", bpy.data.filepath)
    if o["export"]:
        export_all(VA, meshes, scn, cam, sun, unity, root)
    print("done")


if __name__ == "__main__":
    main()
