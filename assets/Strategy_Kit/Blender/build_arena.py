"""
Арены боя Vitaria: «Луг» (Arena_Meadow), «Болото» (Arena_Swamp), «Снега» (Arena_Snow), «Кладбище»
(Arena_Graveyard), «Лес» (Arena_Forest), «Горная застава» (Arena_MountainPass) — ассеты, сцена, превью, экспорт
в Unity.

Запуск (как add_buildings.py):
  Blender GUI:  открыть vitaria_kit.blend > Scripting > Open > build_arena.py > Run Script
  Консоль:      blender -b vitaria_kit.blend --python build_arena.py [-- --render --no-save --no-export]
  Через bpy:    python build_arena.py --blend <путь к vitaria_kit.blend> [--render]
  Черновик:     python build_arena.py --fresh --render      (пустой файл, только превью)
  Окружение:    --env swamp — «Болото» (vitaria_arena/swamp.py), --env snow — «Снега» (snow.py),
                --env graveyard — «Кладбище» (graveyard.py), --env forest — «Лес» (forest.py),
                --env mountainpass — «Горная застава» (mountainpass.py); без ключа — «Луг», как было.
                Сборка окружения не трогает «Луг»: его сцену, FBX, раскладку и общие ассеты арены
                (плитки, реквизит, огонь) — выгружается только своё: Models/Arena/<Окружение>/,
                Layout/arena_<окружение>_layout.json, своя палитра и вода, превью arena_<окружение>_*.png.

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
# Окружение задаёт composition-модуль (M.NAME, M.PREFIX, M.TERRAIN_DIR); у «Луга» имена прежние
ENV = "meadow"
SCENE = "Arena_Meadow"
PFX = "AM_"                  # префикс коллекций, камеры и солнца сцены окружения
TERRAIN_DIR = "Meadow"       # Models/Arena/<TERRAIN_DIR>: рельеф, вода, россыпь и небо окружения
PREVIEW_SLUG = "meadow"      # превью Previews/arena_<slug>_<кадр>.png
SCROLL = {"_Water": 0.04, "_River": 0.22, "_Falls": 0.6}   # по окончанию имени меша: повторов текстуры в секунду


def configure(M):
    """Имена сцены и выгрузки по окружению (M — composition-модуль)."""
    global SCENE, PFX, TERRAIN_DIR, PREVIEW_SLUG
    SCENE = M.NAME
    PREVIEW_SLUG = env_slug(M)
    PFX = getattr(M, "PREFIX", "AM_")
    TERRAIN_DIR = getattr(M, "TERRAIN_DIR", "Meadow")


def env_slug(M):
    """'Arena_Meadow' -> 'meadow': раскладка arena_<slug>_layout.json, превью arena_<slug>_*.png."""
    return M.NAME.split("_", 1)[1].lower()


def scroll_of(name, M=None):
    own = getattr(M, "SCROLL", {}) if M is not None else {}
    if name in own:
        return own[name]
    for suf, v in SCROLL.items():
        if name.startswith("Arena_") and name.endswith(suf):
            return v
    return None
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
         "res": 100, "samples": 48, "shots": "mock,hero", "root": None, "prev": None, "layout": None, "tag": "",
         "env": "meadow"}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--blend", "--ref", "--root", "--prev", "--shots", "--layout", "--tag", "--env") and i + 1 < len(argv):
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


def variant_material(V, tex_dir, variant):
    """Vitaria_Palette_<variant>: копия палитры с текстурой варианта биома (build_vitaria.PALETTE_VARIANTS) —
    ею красятся рельеф, россыпь и небо окружения; в Unity FBX-слот ремапится по имени на свой материал."""
    img = V.build_palette_variant(tex_dir, variant)
    name = "Vitaria_Palette_" + variant
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials["Vitaria_Palette"].copy()
        mat.name = name
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image and n.image.name.startswith("Vitaria_Palette") and \
                "Emission" not in n.image.name:
            n.image = img
    return mat


WATER_MATS = {"lake": "Vitaria_Water", "falls": "Vitaria_Waterfall", "swamp": "Vitaria_Water_Swamp"}


def water_materials(TR, tex_dir, styles=("lake", "falls")):
    """Материалы воды с одной раскладкой UV: спокойная (озеро, река), струи водопадов, стоячая вода болота.
    Имя материала = имя PNG; в Unity постпроцессор переназначает их по имени. Пишутся только styles —
    сборка «Болота» не перезаписывает воду «Луга»."""
    out = {}
    for style in styles:
        name = WATER_MATS[style]
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
            # болото: стоячая вода блестит сильнее озера — в ней отражается небо, как на макете
            bsdf.inputs["Roughness"].default_value = {"lake": 0.32, "swamp": 0.22}.get(style, 0.5)
            bsdf.inputs["Specular IOR Level"].default_value = {"lake": 0.45, "swamp": 0.5}.get(style, 0.25)
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


def build_assets(V, VA, ref_dir, mat, kit_root, fresh, smat=None, palette_assets=(), backdrop=True):
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
    # ассеты окружения (у «Болота» — vitaria_arena/swamp_assets.py): своя коллекция Kit_<Окружение>
    for cat, n, title, fn in VA.env_asset_entries(reload=True):
        # плитки поля биома и прочее из PALETTE_ASSETS окружения — в палитре биома (Vitaria_Palette_<Биом>)
        own = smat is not None and n.startswith(tuple(palette_assets))
        me = bpy.data.meshes.get(n)
        if me is not None:
            me.materials.clear()
        me = build_mesh(V, n, fn, smat if own else mat)
        meshes[n] = me
        kit_object(me, ensure_coll("Kit_" + cat, kit_root), cat, title)
        print("%s asset %-20s" % (cat.lower(), n))
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
    elif backdrop:
        print("!! Ref_Buildings/Source не найден — фон без замка и домов")
    return meshes


# =========================================================================================
# витрина кита: ряды арены и фона под рядами build_vitaria (Buildings 0, Nature -12,
# Props -25, Resources -33), те же подписи и отступы
# =========================================================================================
# плитки поля «Луга» (блок tiles раскладки); окружение может задать свой TILES — перекрашенные нейтральные
TILES = {"neutral": ["Hex_Tile_A", "Hex_Tile_A", "Hex_Tile_B", "Hex_Tile_C"], "player": "Hex_Tile_Blue",
         "enemy": "Hex_Tile_Red", "obstacles": ["Obst_Boulders", "Obst_Stump", "Obst_Bush"]}
SHOW_ROWS = [("Kit_Arena", 60.0, -44.0, 0.8, 0.2, 99, 6.0), ("Kit_Backdrop", 60.0, -55.0, 1.2, 0.3, 99, 8.0),
             ("Kit_Swamp", 60.0, -66.0, 0.8, 0.2, 99, 6.0), ("Kit_Snow", 60.0, -77.0, 0.8, 0.2, 99, 6.0),
             ("Kit_Graveyard", 60.0, -88.0, 0.8, 0.2, 99, 6.0), ("Kit_Forest", 60.0, -99.0, 0.8, 0.2, 99, 7.0),
             ("Kit_MountainPass", 60.0, -118.0, 0.8, 0.2, 99, 7.0),   # вершины по 25 м: ряд ниже
             ("Kit_Preview", 60.0, -150.0, 0.8, 0.2, 99, 6.0)]          # бойцы-заглушки превью (в игру не идут)
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
        if c.name.startswith(PFX):
            bpy.data.collections.remove(c)
    for n in (PFX + "Camera", PFX + "Sun"):
        for coll in (bpy.data.cameras, bpy.data.lights):
            x = coll.get(n)
            if x is not None and x.users == 0:
                coll.remove(x)
    scn = bpy.data.scenes.new(SCENE)
    scn.unit_settings.system = "METRIC"
    colls = {}
    for g in GROUPS + ["Preview", "Rig", "Sky"]:
        c = bpy.data.collections.new(PFX + g)
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


def battle_views(M, B, aspects=(4 / 3, 1.5, 1.6, 16 / 9, 2.0, 21 / 9)):
    """Кадры камеры боя на экранах от 4:3 до 21:9: (позиция, поворот, tg половины FOV по вертикали, аспект).
    Как BattleSceneController.FrameCamera: на узком экране камера отъезжает, чтобы поле заняло BOARD_SHARE
    ширины кадра."""
    cam = M.CAMERA
    tv = math.tan(math.radians(cam["fov"]) / 2)
    p = math.radians(cam["pitch"])
    T = Vector(cam["target"])
    xs = [B.cell_center(cx, cy)[0] for cx, cy in B.cells()]
    board_w = max(xs) - min(xs) + 2.0                    # 2 м между гранями гекса
    out = []
    for a in aspects:
        d = max(cam["distance"], board_w / (2 * BOARD_SHARE * tv * a))
        P = T + d * Vector((0, -math.cos(p), math.sin(p)))
        out.append((P, (T - P).to_track_quat("-Z", "Y").to_matrix(), tv, a))
    return out


_LOCAL_BOX = {}


def in_battle_view(o, views, margin=1.08):
    """Попадает ли рамка объекта хоть в один кадр боя (с запасом margin по краям)."""
    me = o.data
    if me.name not in _LOCAL_BOX:
        vs = [v.co for v in me.vertices] or [Vector()]
        lo = Vector([min(v[i] for v in vs) for i in range(3)])
        hi = Vector([max(v[i] for v in vs) for i in range(3)])
        _LOCAL_BOX[me.name] = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    mw = Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
    corners = [mw @ c for c in _LOCAL_BOX[me.name]]
    for P, R, tv, a in views:
        Rt = R.transposed()
        xs, ys = [], []
        for w in corners:
            v = Rt @ (w - P)
            if v.z > -0.3:                                # у камеры или за ней: считаем видимым
                return True
            xs.append(v.x / (-v.z * tv * a))
            ys.append(v.y / (-v.z * tv))
        if min(xs) <= margin and max(xs) >= -margin and min(ys) <= margin and max(ys) >= -margin:
            return True
    return False


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


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
EXTRA_RADII = {}             # радиусы занятости ассетов окружения (M.RADII): префикс -> м при масштабе 1


def radius_of(asset, s):
    base = {"Tree_Pine": 0.95, "Tree_Round": 1.0, "Bush_": 0.55, "Rock_Large": 0.9, "Rock_Medium": 0.5,
            "Rock_Small": 0.25, "Grass_": 0.14, "Flowers_": 0.16, "Env_Tuft": 0.16, "Env_Flowers": 0.18,
            "Tree_Stump": 0.4}
    for k, v in EXTRA_RADII.items():
        if asset.startswith(k):
            return v * s
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


def build_scene(V, VA, meshes, mat, wmat, smat=None):
    """smat — палитра окружения (Vitaria_Palette_<Биом>) для рельефа, россыпи и неба; None — общая палитра."""
    mods = VA.load()
    M, TR, B = mods["meadow"], mods["terrain"], mods["board"]
    LM = mods["landmarks"]
    N = M.NAME
    smat = smat or mat
    # своя природа и мелочь окружения (префиксы его ассетов) — в россыпь и слияние, как деревья кита
    nature = NATURE + tuple(getattr(M, "NATURE_EXTRA", ()))
    merge_prefix = MERGE_PREFIX + tuple(getattr(M, "NATURE_EXTRA", ())) + tuple(getattr(M, "MERGE_EXTRA", ()))
    EXTRA_RADII.clear()
    EXTRA_RADII.update(getattr(M, "RADII", {}))
    scn, C = reset_scene()
    rng = random.Random(77)
    SOCKETS.clear()
    PR = mods["props"]
    TR.VIEW_BOX = getattr(M, "VIEW_BOX", None)          # отсечение рельефа за пределами всех кадров

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
        print("island strata: %d outline points, %d contours" % (len(outline), n_loops))
    roads = V.Asset(N + "_Roads")
    road_pts = []
    for tname, ctrl, half in M.ROADS:
        road_pts += [(p.x, p.y, half) for p in TR.build_road(roads, ctrl, ts[tname], half=half, seed=len(road_pts))]
    water, river = V.Asset(N + "_Water"), V.Asset(N + "_River")
    falls, foam = V.Asset(N + "_Falls"), V.Asset(N + "_Foam")
    W = M.WATER
    if W:                                           # у парящего острова озера нет
        TR.water_plane(water, W["x0"], W["x1"], W["y0"], W["y1"], flow=W["flow"], tile=W["tile"])
    if getattr(M, "RIVER", None):
        TR.river_ribbon(river, M.RIVER, M.RIVER_HALF + 0.35, z=TR.RIVER_Z, tile=4.0, seed=3)
    # своё у окружения: у «Болота» — пруды в низинах (вода), мох и корни по обрыву
    extra = getattr(M, "terrain_extra", None)
    if extra:
        extra(V, TR, ts, dict(ground=ground, cliffs=cliffs, water=water))
    for k, fd in enumerate(M.FALLS):
        if isinstance(fd, dict):
            # водопад острова: в облака (z_bot), без пены; z_top — вода сверху, иначе трава источника
            pt, nr, width, z_bot = fd["pt"], fd["normal"], fd["width"], fd["z_bot"]
            z_top = fd.get("z_top")
            if z_top is None:
                z_top = ts[fd["src"]].height(pt[0] - nr[0] * 0.6, pt[1] - nr[1] * 0.6)
            end = TR.waterfall(falls, pt, nr, width, z_top, z_bot - 0.05, seed=k, reach=fd["reach"],
                               stream=fd.get("stream", 2.2), segs=fd.get("segs", 12))
            if fd.get("foam", True):
                TR.foam_patch(foam, (end.x, end.y), width * 0.75, seed=k * 3 + 1, n=10, z=z_bot)
                SOCKETS.append(("mist", (end.x, end.y, z_bot + 0.15), round(width, 2)))
            continue
        pt, nr, width, src, reach = fd
        if src == "river":
            z_top, stream = TR.RIVER_Z, 0.5
        else:
            z_top, stream = ts[src].height(pt[0] - nr[0] * 0.6, pt[1] - nr[1] * 0.6), 2.2
        end = TR.waterfall(falls, pt, nr, width, z_top, TR.WATER_Z - 0.05, seed=k, reach=reach, stream=stream)
        TR.foam_patch(foam, (end.x, end.y), width * 0.75, seed=k * 3 + 1, n=10)
        SOCKETS.append(("mist", (end.x, end.y, TR.WATER_Z + 0.15), round(width, 2)))
    for name, a in ((N + "_Ground", ground), (N + "_Cliffs", cliffs), (N + "_Roads", roads),
                    (N + "_Foam", foam)):
        if not a.bm.faces:                          # пены у острова нет: пустой меш в игру не уходит
            a.bm.free()
            continue
        me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        a.bm.to_mesh(me)
        a.bm.free()
        me.materials.clear()
        me.materials.append(smat)
        me.validate()
        meshes[name] = me
        o = bpy.data.objects.new(name, me)
        C["Terrain"].objects.link(o)
    still = getattr(M, "WATER_STYLE", "lake")      # стоячая вода окружения: у «Болота» — swamp
    for name, a, style in ((N + "_Water", water, still), (N + "_River", river, still),
                           (N + "_Falls", falls, "falls")):
        if not a.bm.faces:                          # озера у острова нет
            a.bm.free()
            continue
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
        if p.get("zabs") is not None:
            z = p["zabs"]                           # по воде (кувшинки, кочки) и мостки — абсолютная высота
        elif asset == "Env_Bridge":
            z = ts[on].z + (p["z"] or 0.0)          # мост — по уровню берега, а не по рельефу под ним
        elif p["z"] is not None:
            z = ts[on].height(p["x"], p["y"]) + p["z"]
        else:
            z = ts[on].height(p["x"], p["y"])
        grp = "Backdrop" if asset.startswith(("Bld_", "Env_")) else ("Nature" if asset.startswith(nature) else "Props")
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
        # навесное окружения: огонь факела (отдельный меш, мерцает) и точки частиц над ним. Смещения (ox, oy) — в
        # осях ассета, поворачиваются с ним (горн кузни сбоку от середины); сокет — (вид, dz, размер[, ox, oy])
        att = getattr(M, "ATTACHMENTS", {}).get(asset)
        if att:
            turn = Matrix.Rotation(math.radians(p["rz"]), 3, "Z")
            if "fx" in att:
                fa, ox, oy, oz, fs = att["fx"]
                off = turn @ Vector((ox * p["s"], oy * p["s"], 0.0))
                place(meshes, C["FX"], fa, p["x"] + off.x, p["y"] + off.y, z + oz * p["s"],
                      rng.uniform(0, 360), fs * p["s"])
            for sk in att.get("sockets", ()):
                kind, dz, size = sk[:3]
                off = turn @ Vector((sk[3] * p["s"], sk[4] * p["s"], 0.0)) if len(sk) > 3 else Vector()
                SOCKETS.append((kind, (p["x"] + off.x, p["y"] + off.y, z + dz * p["s"]), size))

    # трубы домов фона: верх каждой трубы — сокет дыма (грань swatch-а black, смотрит вверх, выше 1.5 м)
    for o in placed:
        if o.data.name.startswith("Bld_") and not o.data.name.startswith("Bld_Windmill"):
            mw = Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
            for q in CHIMNEYS.get(o.data.name, []):
                w = mw @ q
                SOCKETS.append(("smoke", (w.x, w.y, w.z + 0.08), round(0.7 * o.scale.x, 2)))

    # заборы вдоль ломаных (сегмент Prop_Fence = 1 м вдоль X, лицом -Y). Запись окружения может быть словарём:
    # свой сегмент (asset, длина seg — столб в начале сегмента), столб в конце ломаной (end), доля случайного
    # разворота и наклона сегментов (jitter: у каменной стены меньше, чем у жердей) и столб по ломаной (align_end)
    for fence in M.FENCES:
        jit, align = 1.0, False
        if isinstance(fence, dict):
            tname, pts = fence["on"], fence["pts"]
            fa, seg, end = fence.get("asset", "Prop_Fence"), fence.get("seg", 1.0), fence.get("end")
            jit, align = fence.get("jitter", 1.0), fence.get("align_end", False)
        else:
            (tname, pts), fa, seg, end = fence, "Prop_Fence", 1.0, None
        ang = 0.0
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            L = math.hypot(x1 - x0, y1 - y0)
            n = max(1, int(round(L / seg)))
            ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
            for k in range(n):
                t = (k + 0.5) / n
                x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                sx = L / n / seg if end else 1.0             # сегменты от столба до столба: ровно по ломаной
                o = place(meshes, C["Props"], fa, x, y, ts[tname].height(x, y), ang + rng.uniform(-3, 3) * jit,
                          tilt=(rng.uniform(-2, 2) * jit, rng.uniform(-2, 2) * jit))
                if end:
                    o.scale = (sx, 1.0, 1.0)
                spots.add(x, y, 0.45)
        if end:
            x, y = pts[-1]
            rz = rng.uniform(0, 360)
            place(meshes, C["Props"], end, x, y, ts[tname].height(x, y), ang if align else rz)
            spots.add(x, y, 0.3)

    # ---------------------------------------------------------------- россыпь природы
    scatter_ok = getattr(M, "scatter_ok", None)        # окружение: не в воде прудов, не на мостках

    def keep_out(tname, x, y, r):
        if tname == "arena" and B.dist_to_board(x, y) < M.KEEP_OUT_BOARD + r * 0.6:
            return True
        if scatter_ok is not None and not scatter_ok(tname, x, y, r):
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
    # объектов сливаются в один меш <окружение>_Scatter (одна отрисовка с общим материалом).
    # Отдельными остаются здания, мост, мельница, крылья, реквизит лагерей и огонь.
    merge = V.Asset(N + "_Scatter")
    gone = []
    for g in ("Nature", "Props"):
        for o in list(C[g].objects):
            if o.type == "MESH" and o.data.name.startswith(merge_prefix):
                mw = Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale)
                merge.add_mesh(o.data, mw)
                gone.append(o)
                o.data.calc_loop_triangles()
                MERGE_STATS[o.data.name] = MERGE_STATS.get(o.data.name, 0) + len(o.data.loop_triangles)
    for o in gone:
        bpy.data.objects.remove(o, do_unlink=True)
    me = bpy.data.meshes.get(N + "_Scatter") or bpy.data.meshes.new(N + "_Scatter")
    merge.bm.to_mesh(me)
    merge.bm.free()
    me.materials.clear()
    me.materials.append(smat)
    me.validate()
    meshes[N + "_Scatter"] = me
    C["Nature"].objects.link(bpy.data.objects.new(N + "_Scatter", me))
    print("merged %d scatter objects: %s" % (len(gone), ", ".join(
        "%s %d" % kv for kv in sorted(MERGE_STATS.items(), key=lambda kv: -kv[1]))))

    # ---------------------------------------------------------------- небо острова: облака, дальние островки
    if getattr(M, "CLOUDS", None) or getattr(M, "DISTANT", None):
        import build_colony as BC
        BC.build_sky(V, sys.modules[__name__], TR, M, meshes, C, smat, ts)
        # своё в небе окружения (дальние вершины «Горной заставы»): (ассет, x, y, z подошвы, поворот, масштаб)
        for asset, x, y, z, rz, s in getattr(M, "SKY_PROPS", ()):
            place(meshes, C["Sky"], asset, x, y, z, rz, s)
        sky = V.Asset(N + "_Sky")
        parts = [o for o in C["Sky"].objects if o.type == "MESH"]
        # облака и деревья островков, которых камера боя не видит ни на одном экране, в игру не идут
        views = battle_views(M, B)
        hidden = [o for o in parts if not in_battle_view(o, views)]
        tris = {o.name: tri_count(o.data) for o in parts}
        print("sky: %d of %d pieces never in the battle frame (%d of %d tris)" % (
            len(hidden), len(parts), sum(tris[o.name] for o in hidden), sum(tris.values())))
        for o in hidden:
            bpy.data.objects.remove(o, do_unlink=True)
        parts = [o for o in C["Sky"].objects if o.type == "MESH"]
        for o in parts:
            sky.add_mesh(o.data, Matrix.LocRotScale(o.location, o.rotation_euler.to_quaternion(), o.scale))
            if o.data.name.startswith(N + "_"):
                meshes.pop(o.data.name, None)          # дальние островки — внутри неба, отдельно не выгружаются
        for o in parts:
            bpy.data.objects.remove(o, do_unlink=True)
        me = bpy.data.meshes.get(N + "_Sky") or bpy.data.meshes.new(N + "_Sky")
        sky.bm.to_mesh(me)
        sky.bm.free()
        me.materials.clear()
        me.materials.append(smat)
        me.validate()
        meshes[N + "_Sky"] = me
        C["Sky"].objects.link(bpy.data.objects.new(N + "_Sky", me))
        print("sky: %d pieces merged" % len(parts))

    # ---------------------------------------------------------------- превью поля (в игру не идёт)
    tiles = getattr(M, "TILES", None) or TILES
    zones = {"player": tiles["player"], "enemy": tiles["enemy"]}
    for (cx, cy) in B.cells():
        x, y = B.cell_center(cx, cy)
        kind = zones.get(B.zone(cx, cy))
        if kind is None:
            kind = tiles["neutral"][(cx * 7 + cy * 13) % len(tiles["neutral"])]
        place(meshes, C["Preview"], kind, x, y, 0.0, 60 * ((cx * 5 + cy * 3) % 6), name="Cell_%d_%d" % (cx, cy))
    # бойцы-заглушки на клетках (только превью: масштаб поля и реквизита рядом с отрядом)
    for asset, cx, cy, rz in getattr(M, "PREVIEW_FIGHTERS", ()):
        if asset in meshes:
            x, y = B.cell_center(cx, cy)
            place(meshes, C["Preview"], asset, x, y, 0.0, rz, name="Fighter_%d_%d" % (cx, cy))

    # ---------------------------------------------------------------- камера, солнце, мир
    cam = M.CAMERA
    cd = bpy.data.cameras.new(PFX + "Camera")
    # FOV задан по вертикали, как Camera.fieldOfView в Unity: объектив считаем от высоты сенсора
    # (свойство angle у Blender берёт ширину сенсора и даёт кадр в полтора раза уже)
    cd.sensor_fit = "VERTICAL"
    cd.sensor_height = 24.0
    cd.lens = 12.0 / math.tan(math.radians(cam["fov"]) / 2)
    cd.clip_start, cd.clip_end = 0.3, 300
    co = bpy.data.objects.new(PFX + "Camera", cd)
    p = math.radians(cam["pitch"])
    T = Vector(cam["target"])
    co.location = T + cam["distance"] * Vector((0, -math.cos(p), math.sin(p)))
    co.rotation_euler = (T - co.location).to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(co)
    scn.camera = co
    sd = bpy.data.lights.new(PFX + "Sun", "SUN")
    sd.energy = M.SUN["energy"]
    sd.color = M.SUN["color"]
    sd.angle = math.radians(3)
    so = bpy.data.objects.new(PFX + "Sun", sd)
    so.rotation_euler = Vector(M.SUN["direction"]).normalized().to_track_quat("-Z", "Y").to_euler()
    C["Rig"].objects.link(so)
    wname = PFX + ("World_Isle" if getattr(M, "LOOKDEV", None) else "World")     # look-dev меняет узлы мира
    world = bpy.data.worlds.get(wname) or bpy.data.worlds.new(wname)
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
def render_shots(scn, cam, prev_dir, shots, res, samples, look=None, tag=""):
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
                td = bpy.data.cameras.new(PFX + "TopCam")
                td.type = "ORTHO"
                td.ortho_scale = 56
                td.clip_end = 300
                top_cam = bpy.data.objects.new(PFX + "TopCam", td)
                top_cam.location = (2.0, 4.0, 60.0)
                scn.collection.objects.link(top_cam)
            scn.camera = top_cam
        else:
            scn.camera = cam
        scn.render.resolution_x, scn.render.resolution_y = w, h
        scn.render.resolution_percentage = res
        scn.cycles.samples = samples
        if look and sh in look.get("shots", {}):    # туман, резкость, свечение, грейдинг кадра (как у колонии)
            import build_colony as BC
            BC.lookdev_shot(scn, look, sh, h * res / 100.0)
        path = os.path.join(prev_dir, "arena_%s_%s%s.png" % (PREVIEW_SLUG, sh, tag))
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
    N = M.NAME
    own = ENV != "meadow"            # окружение кроме «Луга»: общие ассеты арены и «Луг» не перевыгружаются
    info = []
    terrain = [n for n in meshes if n.startswith(N + "_")]
    if not own:
        arena_names = [n for c, n, _, _ in VA.asset_entries() if c == "Arena"]      # декор плато — только в Scatter
        backdrop = [n for n in meshes if n.startswith("Bld_") and n not in arena_names]
        env_assets = []
    else:
        # свои ассеты окружения, которые в раскладке стоят отдельным объектом (факел, огонь), — в папку
        # окружения; то, что слито в россыпь, отдельно не нужно. Плитки, знамёна, палатка, огонь — из «Луга»
        arena_names, backdrop = [], []
        used = {o.data.name for g in GROUPS for o in bpy.data.collections[PFX + g].objects if o.type == "MESH"}
        tiles = getattr(M, "TILES", None) or {}
        used |= set(tiles.get("neutral", ())) | {tiles.get("player"), tiles.get("enemy")} | set(tiles.get("obstacles", ()))
        env_assets = [n for c, n, _, _ in VA.env_asset_entries() if n in used]
    for n in arena_names:
        hot = split_emissive(V, meshes[n], fx)
        if hot:
            print("fx faces %-24s %d" % (n, hot))
        export_mesh(meshes[n], os.path.join(base, n + ".fbx"))
    for n in env_assets:
        hot = split_emissive(V, meshes[n], fx)
        if hot:
            print("fx faces %-24s %d" % (n, hot))
        export_mesh(meshes[n], os.path.join(base, TERRAIN_DIR, n + ".fbx"))
    for n in backdrop:
        export_mesh(meshes[n], os.path.join(base, "Backdrop", n + ".fbx"))
    for n in terrain:
        if own:
            hot = split_emissive(V, meshes[n], fx)
            if hot:
                print("fx faces %-24s %d" % (n, hot))
        export_mesh(meshes[n], os.path.join(base, TERRAIN_DIR, n + ".fbx"))
    for n in arena_names + env_assets + backdrop + terrain:
        t, sz = tris_size(meshes[n])
        cat = "Arena" if n in arena_names else ("Backdrop" if n in backdrop else TERRAIN_DIR)
        info.append({"asset": n, "category": cat, "tris": t, "size_unity": sz})
        print("fbx %-26s %6d tris" % (n, t))

    # раскладка: всё из коллекций GROUPS, координаты Unity относительно середины поля
    items = []
    total = 0
    sky = bpy.data.collections.get(PFX + "Sky")
    for g in GROUPS + (["Sky"] if sky is not None and len(sky.objects) else []):
        for o in bpy.data.collections[PFX + g].objects:
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
            sc = scroll_of(o.data.name, M)
            if sc is not None:
                it["scroll"] = sc                           # повторов текстуры в секунду вдоль V
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
        "tiles": getattr(M, "TILES", None) or TILES,
        # меши эффектов (Models/Arena/FX_*.fbx) по ролям и точки частиц окружения
        "fx": FX_ROLES,
        "sockets": [{"kind": k, "p": to_unity_vec(p), "size": sz} for k, p, sz in SOCKETS],
        "tris": total,
    }
    game_look = getattr(M, "GAME_LOOK", None)
    if game_look:
        # вид острова колонии в игре (build_isle.game_look, блоки isle_layout.json в том же формате): солнце,
        # ровный амбиент (color; sky = equator = ground — для прежнего трилайта), фон = небо, туман и резкость —
        # доли расстояния камеры до фокуса, пост, дымка IslandHaze. Глубины дымки — вниз от уровня поля
        look = game_look(to_unity_vec(sun_fwd))
        amb = look["ambient"]["color"]
        layout["sun"] = look["sun"]
        layout["ambient"] = {"color": amb, "sky": amb, "equator": amb, "ground": amb}
        layout["background"] = look["background"]
        ordered = {}
        for k, v in layout.items():
            ordered[k] = v
            if k == "background":
                ordered.update(fog=look["fog"], post=look["post"], haze=look["haze"])
        layout = ordered
    lay_dir = os.path.join(unity, "Layout")
    os.makedirs(lay_dir, exist_ok=True)
    with open(os.path.join(lay_dir, "arena_%s_layout.json" % env_slug(M)), "w", encoding="utf-8") as f:
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
    # Backdrop есть и у колонии (build_colony): чужие строки фона не трогаем, свои заменяются по имени
    drop = (TERRAIN_DIR,) if own else ("Arena", "Meadow")          # строки других окружений не трогаем
    rows = [r for r in rows if r.get("asset") not in names and r.get("category") not in drop]
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
    if o["layout"]:
        VA.LAYOUT = o["layout"]
    global ENV
    ENV = VA.ENV = o["env"]
    VA.load(reload=True)
    root = os.path.normpath(o["root"] or os.path.join(here, ".."))
    unity = os.path.join(root, "Unity", "Assets", "Vitaria")
    tex_dir = os.path.join(unity, "Textures")
    mods = VA.load()
    M = mods["meadow"]
    configure(M)
    mat = palette_material(V, tex_dir)
    # вода: «Луг» — озеро/река и водопады; окружение пишет только свою (воду «Луга» не перезаписывает)
    styles = ("lake", "falls") if ENV == "meadow" else \
        tuple(s for s in [getattr(M, "WATER_STYLE", "lake")] + (["falls"] if M.FALLS else []) if s)
    wmat = water_materials(mods["terrain"], tex_dir, styles)
    # палитра окружения: рельеф, россыпь и небо «Болота» красятся своим вариантом палитры
    smat = variant_material(V, tex_dir, M.PALETTE_VARIANT) if getattr(M, "PALETTE_VARIANT", None) else None
    kit_root = bpy.data.collections.get("Vitaria_Kit")
    if kit_root is None:
        kit_root = bpy.data.collections.new("Vitaria_Kit")
        bpy.context.scene.collection.children.link(kit_root)
    # фон из Ref_Buildings (замок, дома) нужен только окружениям, которые его ставят
    ref_dir = find_ref(here, o["ref"]) if getattr(M, "BACKDROP_USED", True) else None
    meshes = build_assets(V, VA, ref_dir, mat, kit_root, o["fresh"], smat, getattr(M, "PALETTE_ASSETS", ()),
                          getattr(M, "BACKDROP_USED", True))
    if not o["fresh"]:
        showcase(kit_root)
    if getattr(M, "CLOUDS", None) or getattr(M, "DISTANT", None):
        import vitaria_colony as VC                  # облака острова — ассеты колонии
        for cat, n, title, fn in VC.asset_entries(reload=True):
            if n.startswith("Env_CloudPuff"):
                meshes[n] = build_mesh(V, n, fn, mat)
    scn, cam, sun = build_scene(V, VA, meshes, mat, wmat, smat)
    # сохранение и выгрузка — до look-dev: он меняет общие материалы (оттенок воды, пятна травы: между текстурой
    # и BSDF встают узлы, и FBX теряет ссылку на текстуру). Look-dev — только для превью
    if o["save"] and bpy.data.filepath:
        # палитры биомов и вода пишутся по абсолютному пути машины сборки: в .blend пути — относительно него
        # (//../Unity/Assets/Vitaria/Textures/...), иначе на другом компьютере текстуры не находятся и в Material
        # Preview рельеф и россыпь окружений фиолетовые
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()
        print("saved", bpy.data.filepath)
    if o["export"]:
        export_all(VA, meshes, scn, cam, sun, unity, root)
    look = getattr(M, "LOOKDEV", None)
    if look and o["render"]:
        import build_colony as BC
        BC.lookdev_scene(scn, look, sun)
        if look.get("sky_bounce") is False:
            # облака и дальние островки не подсвечивают обрыв снизу отражённым светом: в игре его нет
            # (солнце и ровный амбиент), а у тёмных пластов «Болота» он съедал весь тон
            sky = bpy.data.collections.get(PFX + "Sky")
            for ob in (sky.all_objects if sky is not None else ()):
                ob.visible_diffuse = False
    if o["render"]:
        prev = o["prev"] or os.path.join(root, "Previews")
        render_shots(scn, cam, prev, o["shots"], o["res"], o["samples"], look=look, tag=o["tag"])
    print("done")


if __name__ == "__main__":
    main()
