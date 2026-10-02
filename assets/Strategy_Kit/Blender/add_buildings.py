"""
Добавить производственные здания (пакет vitaria_buildings) и ресурсы второй очереди (vitaria_resources.py)
в УЖЕ существующий vitaria_kit.blend и выгрузить их FBX. Набор не пересобирается, сцена и ручные правки
не трогаются.

Blender GUI:  открыть vitaria_kit.blend > Scripting > Open > add_buildings.py > Run Script.
              Файл сохраняется сам (отключить: SAVE = False ниже).
Консоль:      blender -b vitaria_kit.blend --python add_buildings.py [-- --no-save --no-export]
              [-- --only Bld_Tavern,Res_Coal]  только эти ассеты (остальные FBX не перезаписываются)
Через bpy:    python add_buildings.py --blend <путь к vitaria_kit.blend>

Что делает (повторный запуск безопасен — это и способ обновить здания после правки модулей):
  1. переписывает PNG палитры по build_vitaria.PALETTE (новые цвета дописаны в конец,
     старые ячейки не меняются) и перечитывает их в .blend;
  2. собирает здания из vitaria_buildings и кладёт в Vitaria_Kit/Kit_Buildings как ассеты.
     Если здание уже есть — меняется только геометрия его меша, поэтому все копии в сцене
     обновятся сами, а объект набора останется там, где стоит;
     затем уровни улучшения <имя>_L2 и <имя>_L3 каждого здания поля (upgrade(a, level) модуля,
     у казармы и склада — vitaria_buildings/ref_levels.py; их уровень 1 по-прежнему из build_colony.py);
  3. то же для ресурсов vitaria_resources.RESOURCES -> Vitaria_Kit/Kit_Resources;
  4. перестраивает строки зданий и ресурсов в витрине так же, как build_vitaria.py (с подписями);
     уровни 2 и 3 — две строки за строкой зданий (y +7 и +14), каждый под своим уровнем 1;
  5. сохраняет .blend;
  6. пишет FBX в Unity/Assets/Vitaria/Models/Buildings и Models/Resources и обновляет их строки
     в asset_list.json.
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

# витрина — те же числа, что у категорий в build_vitaria.py: (x0, y0, зазор, подпись, в строке, шаг строк)
SHOW = {"Buildings": (60.0, 0.0, 1.2, 0.3, 99, 6.0), "Resources": (60.0, -33.0, 0.42, 0.075, 6, 1.6)}
SHOW_LEVEL_X = {}          # x уровней казармы и склада в витрине (у них нет уровня 1 в строке зданий)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    opts = {"blend": None, "save": SAVE, "export": EXPORT, "only": None}
    for i, a in enumerate(argv):
        if a == "--blend" and i + 1 < len(argv):
            opts["blend"] = os.path.abspath(argv[i + 1])
        elif a == "--only" and i + 1 < len(argv):
            opts["only"] = {x for x in argv[i + 1].split(",") if x}
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


def showcase_layout(names, cat="Buildings"):
    """Раскладка категории точно как в build_vitaria.main(): (имя -> (loc, label_loc))."""
    sx0, sy0, gap, lsize, per_row, lsp = SHOW[cat]
    out = {}
    x, line, count = sx0, 0, 0
    for n in names:
        o = bpy.data.objects.get(n)
        if o is None:
            continue
        x0, x1, y0, y1 = bbox_local(o)
        w = max(x1 - x0, len(n) * lsize * 0.62)
        if count and count % per_row == 0:
            line += 1
            x = sx0
        yy = sy0 - line * lsp
        out[n] = (Vector((x + w / 2 - (x1 + x0) / 2, yy, 0)), Vector((x + w / 2, yy + y0 - lsize * 1.4, 0.005)))
        x += w + gap
        count += 1
    return out


def add_label(name, loc, coll, size=SHOW["Buildings"][3]):
    if bpy.data.objects.get(name + "_lbl"):
        return
    cu = bpy.data.curves.new(name + "_lbl", "FONT")
    cu.body = name
    cu.size = size
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
    import vitaria_resources as VR
    VR = importlib.reload(VR)
    import build_arena as BA
    BA = importlib.reload(BA)
    ref_dir = BA.find_ref(here, None)
    if ref_dir and ref_dir not in sys.path:
        sys.path.insert(0, ref_dir)
    ref_names = ["Bld_Barracks", "Bld_Warehouse"] if ref_dir else []
    resources = list(VR.RESOURCES)
    only = opts["only"]
    if only:
        unknown = only - {m.NAME for m in mods} - {n for n, _ in resources} - set(ref_names)
        if unknown:
            raise RuntimeError("--only: нет таких ассетов: %s" % ", ".join(sorted(unknown)))
        mods = [m for m in mods if m.NAME in only]
        resources = [(n, fn) for n, fn in resources if n in only]

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

    # уровни 2 и 3: сборка уровня 1 + upgrade(a, level), доводка уровня 1 (vitaria_buildings.build_level)
    level_objs = []
    bases = [m.NAME for m in mods if hasattr(m, "upgrade")]
    bases += [n for n in ref_names if not only or n in only]
    for base in bases:
        for lv in VB.LEVELS[1:]:
            me = VB.build_level(V, base, lv, mat, BA.build_mesh)
            report.append(VB.check(me))
            o = bpy.data.objects.get(me.name)
            if o is None or o.data is not me:
                o = bpy.data.objects.new(me.name, me)
                created.append(me.name)
            if o.name not in kit_bld.objects:
                kit_bld.objects.link(o)
            if o.asset_data is None:
                try:
                    o.asset_mark()
                    o.asset_data.tags.new("Buildings")
                    o.asset_data.tags.new("Level %d" % lv)
                    title = next((getattr(m, "TITLE", "") for m in mods if m.NAME == base), base)
                    o.asset_data.description = "%s, уровень %d" % (title, lv)
                except Exception as ex:
                    print("asset mark skipped:", ex)
            level_objs.append(o)
            print("built %-20s %s" % (me.name, "new" if me.name in created else "updated"))
    print("\n".join(["контракт зданий (кит %.2f):" % VB.KIT_SCALE] + report))

    # ресурсы: тот же приём — у существующего меша меняется только геометрия
    kit_res = ensure_coll("Kit_Resources", kit_root)
    res_objs = []
    for name, fn in resources:
        a = V.Asset(name)
        fn(a)
        me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        a.bm.to_mesh(me)
        a.bm.free()
        if not me.materials:
            me.materials.append(mat)
        while len(me.materials) > 1:
            me.materials.pop()
        me.validate()
        me.update()
        hot = V.split_emissive(me)
        o = bpy.data.objects.get(name)
        if o is None or o.data is not me:
            o = bpy.data.objects.new(name, me)
            created.append(name)
        if o.name not in kit_res.objects:
            kit_res.objects.link(o)
        if o.asset_data is None:
            try:
                o.asset_mark()
                o.asset_data.tags.new("Resources")
            except Exception as ex:
                print("asset mark skipped:", ex)
        res_objs.append(o)
        me.calc_loop_triangles()
        print("built %-20s %s  %d tris%s" % (name, "new" if name in created else "updated", len(me.loop_triangles),
                                           "  fx %d" % hot if hot else ""))

    # витрина: порядок как у полной пересборки — здания кита, затем пакет
    # Витрина выстраивается заново целиком: у обновлённого здания мог измениться габарит,
    # и старые места привели бы к наложению соседей. Порядок — как у полной пересборки.
    order = [n for n in ("Bld_House",)] + [m.NAME for m in VB.load()]
    order += [o.name for o in kit_bld.objects if o.type == "MESH" and o.name not in order
              and VB.split_level(o.name)[1] == 1]
    # ресурсы: сначала ресурсы build_vitaria.KIT (их места не меняются), за ними — vitaria_resources
    res_order = [n for c, n, _ in V.KIT if c == "Resources"] + [n for n, _ in VR.RESOURCES]
    res_order += [o.name for o in kit_res.objects if o.type == "MESH" and o.name not in res_order]
    for cat, names in (("Buildings", order), ("Resources", res_order)):
        for n, (loc, lab) in showcase_layout(names, cat).items():
            bpy.data.objects[n].location = loc
            lbl = bpy.data.objects.get(n + "_lbl")
            if lbl:
                lbl.location = lab
            elif show:
                add_label(n, lab, show, SHOW[cat][3])
    # уровни 2 и 3 — строками за зданиями, каждый под своим уровнем 1; казарма и склад (их уровень 1
    # в витрине не стоит) — в конце строки
    x_end = max([bpy.data.objects[n].location.x + bbox_local(bpy.data.objects[n])[1]
                 for n in order if bpy.data.objects.get(n)] or [SHOW["Buildings"][0]])
    for o in sorted(level_objs, key=lambda x: x.name):
        base, lv = VB.split_level(o.name)
        b = bpy.data.objects.get(base)
        if b is not None and b.name in kit_bld.objects:
            x = b.location.x
        else:
            x0, x1, _, _ = bbox_local(o)
            key = "_x_" + base
            x = SHOW_LEVEL_X.setdefault(key, x_end + SHOW["Buildings"][2] - x0)
            if lv == VB.LEVELS[1]:
                x_end = x + x1
        o.location = Vector((x, SHOW["Buildings"][1] + 7.0 * (lv - 1), 0.0))
        lab = Vector((x, o.location.y + bbox_local(o)[2] - SHOW["Buildings"][3] * 1.4, 0.005))
        lbl = bpy.data.objects.get(o.name + "_lbl")
        if lbl:
            lbl.location = lab
        elif show:
            add_label(o.name, lab, show, SHOW["Buildings"][3])
    bpy.context.view_layer.update()

    if opts["save"]:
        bpy.ops.wm.save_mainfile()
        print("saved", bpy.data.filepath)

    if opts["export"]:
        info = []
        for cat, olist in (("Buildings", [o for o, m in objs] + level_objs), ("Resources", res_objs)):
            d = os.path.join(unity, "Models", cat)
            os.makedirs(d, exist_ok=True)
            for o in olist:
                export_at_origin(o, os.path.join(d, o.name + ".fbx"))
                n, size = tris_and_size(o)
                info.append({"asset": o.name, "category": cat, "tris": n, "size_unity": size})
                print("exported %-20s %5d tris  %s" % (o.name, n, size))
        list_path = os.path.join(root, "asset_list.json")
        rows = []
        if os.path.exists(list_path):
            try:
                rows = json.load(open(list_path, encoding="utf-8"))
            except Exception:
                rows = []
        # обновлённые строки остаются на своих местах; новые — сразу за последней строкой своей
        # категории, чтобы список шёл по категориям
        fresh = {x["asset"]: x for x in info}
        rows = [fresh.pop(r.get("asset"), r) for r in rows]
        for cat in ("Buildings", "Resources"):
            new = [x for x in info if x["category"] == cat and x["asset"] in fresh]
            at = max([i + 1 for i, r in enumerate(rows) if r.get("category") == cat] or [len(rows)])
            rows[at:at] = new
        with open(list_path, "w", encoding="utf-8") as f:
            json.dump(rows, f, indent=1)
        print("asset_list.json:", len(rows), "entries")
    print("done: %d buildings, %d levels, %d resources (%d new)" % (len(objs), len(level_objs), len(res_objs),
                                                                    len(created)))


if __name__ == "__main__":
    main()
