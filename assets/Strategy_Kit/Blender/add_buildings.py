"""
Добавить производственные здания (пакет vitaria_buildings) в УЖЕ существующий vitaria_kit.blend
и выгрузить их FBX. Набор не пересобирается, сцена и ручные правки не трогаются.

Blender GUI:  открыть vitaria_kit.blend > Scripting > Open > add_buildings.py > Run Script.
              Файл сохраняется сам (отключить: SAVE = False ниже).
Консоль:      blender -b vitaria_kit.blend --python add_buildings.py [-- --no-save --no-export]
Через bpy:    python add_buildings.py --blend <путь к vitaria_kit.blend>

Что делает (повторный запуск безопасен — это и способ обновить здания после правки модулей):
  1. переписывает PNG палитры по build_vitaria.PALETTE (новые цвета дописаны в конец,
     старые ячейки не меняются) и перечитывает их в .blend;
  2. собирает здания из vitaria_buildings и кладёт в Vitaria_Kit/Kit_Buildings как ассеты.
     Если здание уже есть — меняется только геометрия его меша, поэтому все копии в сцене
     обновятся сами, а объект набора останется там, где стоит;
  3. перестраивает строку зданий в витрине так же, как build_vitaria.py (с подписями);
  4. сохраняет .blend;
  5. пишет FBX в Unity/Assets/Vitaria/Models/Buildings и обновляет их строки в asset_list.json.
"""
import bpy, os, sys, json, importlib
from mathutils import Matrix, Vector

SAVE = True
EXPORT = True

# Повтор FBX_KW из export_vitaria.py дословно — тот же контракт импорта в Unity.
FBX_KW = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
              axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
              use_mesh_modifiers=True, use_triangles=True, use_tspace=False, add_leaf_bones=False,
              bake_anim=False, path_mode="STRIP", embed_textures=False, use_custom_props=False)

# витрина — те же числа, что у категории Buildings в build_vitaria.py
SHOW_X0, SHOW_Y0, SHOW_GAP, SHOW_LABEL, SHOW_PER_ROW, SHOW_LINE = 60.0, 0.0, 1.2, 0.3, 99, 6.0


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    opts = {"blend": None, "save": SAVE, "export": EXPORT}
    for i, a in enumerate(argv):
        if a == "--blend" and i + 1 < len(argv):
            opts["blend"] = os.path.abspath(argv[i + 1])
        elif a == "--no-save":
            opts["save"] = False
        elif a == "--no-export":
            opts["export"] = False
    return opts


def script_dir():
    """Папка Blender/: рядом с .blend или рядом с самим скриптом."""
    cands = []
    if bpy.data.filepath:
        cands.append(os.path.dirname(bpy.data.filepath))
    try:
        cands.append(os.path.dirname(os.path.abspath(__file__)))
    except NameError:
        pass
    for d in cands:
        if os.path.exists(os.path.join(d, "vitaria_buildings", "__init__.py")):
            return d
    raise RuntimeError("Не найден пакет vitaria_buildings рядом с .blend или скриптом: %s" % cands)


def refresh_palette(V, tex_dir):
    """PNG палитры из PALETTE; в .blend перечитываются уже подключённые картинки."""
    os.makedirs(tex_dir, exist_ok=True)
    before = set(bpy.data.images)
    V.build_palette_images(tex_dir)
    for img in list(bpy.data.images):
        if img not in before:                         # временные копии, которые создал build_palette_images
            bpy.data.images.remove(img)
    for name in ("Vitaria_Palette", "Vitaria_Palette_Emission"):
        img = bpy.data.images.get(name)
        if img:
            img.reload()


def get_material(V, tex_dir):
    mat = bpy.data.materials.get("Vitaria_Palette")
    if mat:
        return mat
    pal = bpy.data.images.load(os.path.join(tex_dir, "Vitaria_Palette.png"), check_existing=True)
    emi = bpy.data.images.load(os.path.join(tex_dir, "Vitaria_Palette_Emission.png"), check_existing=True)
    return V.make_material(pal, emi)


def ensure_coll(name, parent):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        parent.children.link(c)
    return c


def bbox_local(o):
    bb = [Vector(v) for v in o.bound_box]
    return (min(v.x for v in bb), max(v.x for v in bb), min(v.y for v in bb), max(v.y for v in bb))


def showcase_layout(names):
    """Раскладка категории Buildings точно как в build_vitaria.main(): (имя -> (loc, label_loc))."""
    out = {}
    x, line, count = SHOW_X0, 0, 0
    for n in names:
        o = bpy.data.objects.get(n)
        if o is None:
            continue
        x0, x1, y0, y1 = bbox_local(o)
        w = max(x1 - x0, len(n) * SHOW_LABEL * 0.62)
        if count and count % SHOW_PER_ROW == 0:
            line += 1
            x = SHOW_X0
        yy = SHOW_Y0 - line * SHOW_LINE
        out[n] = (Vector((x + w / 2 - (x1 + x0) / 2, yy, 0)), Vector((x + w / 2, yy + y0 - SHOW_LABEL * 1.4, 0.005)))
        x += w + SHOW_GAP
        count += 1
    return out


def add_label(name, loc, coll):
    if bpy.data.objects.get(name + "_lbl"):
        return
    cu = bpy.data.curves.new(name + "_lbl", "FONT")
    cu.body = name
    cu.size = SHOW_LABEL
    cu.align_x = "CENTER"
    lab = bpy.data.materials.get("Label")
    if lab:
        cu.materials.append(lab)
    t = bpy.data.objects.new(name + "_lbl", cu)
    t.location = loc
    coll.objects.link(t)


def export_at_origin(o, path):
    saved = o.matrix_world.copy()
    o.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    o.hide_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, **FBX_KW)
    o.matrix_world = saved
    bpy.context.view_layer.update()


def tris_and_size(o):
    me = o.data
    me.calc_loop_triangles()
    xs, ys, zs = zip(*[tuple(v.co) for v in me.vertices])
    return len(me.loop_triangles), [round(max(xs) - min(xs), 3), round(max(zs) - min(zs), 3), round(max(ys) - min(ys), 3)]


def main():
    opts = parse_args()
    if opts["blend"]:
        bpy.ops.wm.open_mainfile(filepath=opts["blend"])
    if not bpy.data.filepath:
        raise RuntimeError("Сначала откройте или сохраните vitaria_kit.blend")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    here = script_dir()
    if here not in sys.path:
        sys.path.insert(0, here)
    import build_vitaria as V
    V = importlib.reload(V)
    import vitaria_buildings as VB
    VB = importlib.reload(VB)
    mods = VB.load(reload=True)

    root = os.path.normpath(os.path.join(here, ".."))
    unity = os.path.join(root, "Unity", "Assets", "Vitaria")
    tex_dir = os.path.join(unity, "Textures")
    refresh_palette(V, tex_dir)
    mat = get_material(V, tex_dir)

    kit_root = bpy.data.collections.get("Vitaria_Kit") or bpy.data.collections.new("Vitaria_Kit")
    if kit_root.name not in bpy.context.scene.collection.children:
        try:
            bpy.context.scene.collection.children.link(kit_root)
        except RuntimeError:
            pass
    kit_bld = ensure_coll("Kit_Buildings", kit_root)
    show = bpy.data.collections.get("Showcase")

    created, objs = [], []
    V.BEVEL_MIN = VB.BEVEL_MIN               # здания поля: без невидимых фасок на мелочи
    report = []
    for m in mods:
        a = V.Asset(m.NAME)
        m.build(a)
        me = bpy.data.meshes.get(m.NAME)
        fresh_mesh = me is None
        if fresh_mesh:
            me = bpy.data.meshes.new(m.NAME)
        a.bm.to_mesh(me)                  # у существующего меша заменяется только геометрия
        a.bm.free()
        if not me.materials:
            me.materials.append(mat)
        me.validate()
        me.update()
        # кровля по цепочке и слот Vitaria_FX (материалы: палитра первой, FX второй)
        while len(me.materials) > 1:
            me.materials.pop()
        themed, hot = VB.finish(V, me)
        report.append(VB.check(me) + ("  roof %d" % themed if themed else "") + ("  fx %d" % hot if hot else ""))
        o = bpy.data.objects.get(m.NAME)
        if o is None or o.data is not me:
            o = bpy.data.objects.new(m.NAME, me)
            created.append(m.NAME)
        if o.name not in kit_bld.objects:
            kit_bld.objects.link(o)
        if o.asset_data is None:
            try:
                o.asset_mark()
                o.asset_data.tags.new("Buildings")
                o.asset_data.description = getattr(m, "TITLE", "")
            except Exception as ex:
                print("asset mark skipped:", ex)
        objs.append((o, m))
        print("built %-20s %s" % (m.NAME, "new" if o.name in created else "updated"))

    V.BEVEL_MIN = 0.0
    print("\n".join(["контракт зданий (кит %.2f):" % VB.KIT_SCALE] + report))

    # витрина: порядок как у полной пересборки — здания кита, затем пакет
    # Витрина выстраивается заново целиком: у обновлённого здания мог измениться габарит,
    # и старые места привели бы к наложению соседей. Порядок — как у полной пересборки.
    order = [n for n in ("Bld_House",)] + [m.NAME for m in mods]
    order += [o.name for o in kit_bld.objects if o.type == "MESH" and o.name not in order]
    lay = showcase_layout(order)
    for n, (loc, lab) in lay.items():
        bpy.data.objects[n].location = loc
        lbl = bpy.data.objects.get(n + "_lbl")
        if lbl:
            lbl.location = lab
        elif show:
            add_label(n, lab, show)
    bpy.context.view_layer.update()

    if opts["save"]:
        bpy.ops.wm.save_mainfile()
        print("saved", bpy.data.filepath)

    if opts["export"]:
        d = os.path.join(unity, "Models", "Buildings")
        os.makedirs(d, exist_ok=True)
        info = []
        for o, m in objs:
            export_at_origin(o, os.path.join(d, o.name + ".fbx"))
            n, size = tris_and_size(o)
            info.append({"asset": o.name, "category": "Buildings", "tris": n, "size_unity": size})
            print("exported %-20s %5d tris  %s" % (o.name, n, size))
        list_path = os.path.join(root, "asset_list.json")
        rows = []
        if os.path.exists(list_path):
            try:
                rows = json.load(open(list_path, encoding="utf-8"))
            except Exception:
                rows = []
        names = {x["asset"] for x in info}
        rows = [r for r in rows if r.get("asset") not in names]
        # новые здания — сразу за последним зданием кита, чтобы список шёл по категориям
        at = max([i + 1 for i, r in enumerate(rows) if r.get("category") == "Buildings"] or [0])
        rows[at:at] = info
        with open(list_path, "w", encoding="utf-8") as f:
            json.dump(rows, f, indent=1)
        print("asset_list.json:", len(rows), "entries")
    print("done: %d buildings (%d new)" % (len(objs), len(created)))


if __name__ == "__main__":
    main()
