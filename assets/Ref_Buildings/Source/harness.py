"""
Harness: строит здания из parts/*.py в стиле kit-а Vitaria и экспортирует FBX для Unity.

Ничего в build_vitaria.py не меняет — импортирует его как библиотеку примитивов,
палитры и класса Asset.

Запуск:
  blender --background --factory-startup --python harness.py -- --part bld_cottage
  blender --background --factory-startup --python harness.py -- --all --out <dir>
  blender --background --factory-startup --python harness.py -- --all --out <dir> --render
"""
import bpy, bmesh, math, os, sys, json, importlib

HERE = os.path.dirname(os.path.abspath(__file__))
KIT_BLENDER = "F:/ClaudeGames/trollstrategy/assets/Strategy_Kit/Blender"
for p in (KIT_BLENDER, HERE):
    if p not in sys.path:
        sys.path.insert(0, p)

import build_vitaria as V   # noqa: E402  — только константы и примитивы, сцену не трогает

# Порядок = порядок на референсном листе (слева направо, сверху вниз)
PARTS = [
    "bld_castle", "bld_barracks", "bld_watchtower",
    "bld_sawmill", "bld_warehouse", "bld_house_timber",
    "bld_cottage", "bld_keep", "bld_tower_round",
    "bld_barn", "bld_minehoist", "bld_house_stone",
]

# Повтор FBX_KW из export_vitaria.py дословно: bake_space_transform=True и
# apply_unit_scale=True — то, из-за чего Unity получает метры и верный разворот осей.
FBX_KW = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
              apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
              bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True,
              use_triangles=True, use_tspace=False, add_leaf_bones=False, bake_anim=False,
              path_mode="STRIP", embed_textures=False, use_custom_props=False)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, part, do_all, render = None, None, False, False
    i = 0
    while i < len(argv):
        if argv[i] == "--out" and i + 1 < len(argv):
            out = os.path.abspath(argv[i + 1]); i += 1
        elif argv[i] == "--part" and i + 1 < len(argv):
            part = argv[i + 1]; i += 1
        elif argv[i] == "--all":
            do_all = True
        elif argv[i] == "--render":
            render = True
        i += 1
    return out, part, do_all, render


def wipe():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images,
                 bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for x in list(coll):
            coll.remove(x)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    bpy.context.scene.unit_settings.system = "METRIC"


def make_material(tex_dir):
    """Имя материала обязано быть Vitaria_Palette — по нему Unity делает remap на общий .mat."""
    os.makedirs(tex_dir, exist_ok=True)
    pal, emi = V.build_palette_images(tex_dir)
    mat = bpy.data.materials.new("Vitaria_Palette")
    mat.use_nodes = True
    nt = mat.node_tree
    # Ищем узел по типу, а не по имени: на локализованном Blender имя другое и .get() вернёт None.
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = pal; t.interpolation = "Linear"; t.location = (-420, 260)
    e = nt.nodes.new("ShaderNodeTexImage"); e.image = emi; e.interpolation = "Linear"; e.location = (-420, -120)
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(e.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Roughness"].default_value = 0.86
    bsdf.inputs["Specular IOR Level"].default_value = 0.3
    bsdf.inputs["Emission Strength"].default_value = 4.0
    return mat


def build_one(modname, mat, coll=None):
    mod = importlib.import_module("parts." + modname)
    importlib.reload(mod)
    a = V.Asset(mod.NAME)
    mod.build(a)
    me = bpy.data.meshes.new(mod.NAME)
    a.bm.to_mesh(me)
    a.bm.free()
    me.materials.append(mat)
    me.validate()
    o = bpy.data.objects.new(mod.NAME, me)
    (coll or bpy.context.scene.collection).objects.link(o)
    return o, mod


def stats(o):
    dg = bpy.context.evaluated_depsgraph_get()
    me = o.evaluated_get(dg).to_mesh()
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    if len(me.vertices):
        xs, ys, zs = zip(*[tuple(v.co) for v in me.vertices])
        bb = dict(w=max(xs) - min(xs), d=max(ys) - min(ys), h=max(zs) - min(zs),
                  minz=min(zs), cx=(max(xs) + min(xs)) / 2, cy=(max(ys) + min(ys)) / 2)
    else:
        bb = dict(w=0, d=0, h=0, minz=0, cx=0, cy=0)
    o.evaluated_get(dg).to_mesh_clear()
    return n, bb


def check(o, mod):
    """Проверки контракта кита: пивот снизу-по-центру, размер в пределах цели."""
    n, bb = stats(o)
    tgt = getattr(mod, "TARGET", None)
    msgs = []
    if abs(bb["minz"]) > 0.2:
        msgs.append("PIVOT: низ меша на z=%.3f, ожидается ~0" % bb["minz"])
    if abs(bb["cx"]) > 0.45:
        msgs.append("PIVOT: центр по X смещён на %.3f" % bb["cx"])
    if abs(bb["cy"]) > 0.60:
        msgs.append("PIVOT: центр по Y смещён на %.3f" % bb["cy"])
    if tgt:
        for k, want in zip(("w", "d", "h"), tgt):
            got = bb[k]
            if want and abs(got - want) / want > 0.28:
                msgs.append("SIZE %s: %.2f, цель %.2f" % (k, got, want))
    if n > 9000:
        msgs.append("TRIS: %d — многовато для кита" % n)
    print("STAT %-20s tris=%-6d w=%.2f d=%.2f h=%.2f minz=%.3f cx=%.2f cy=%.2f"
          % (o.name, n, bb["w"], bb["d"], bb["h"], bb["minz"], bb["cx"], bb["cy"]))
    for m in msgs:
        print("WARN %-20s %s" % (o.name, m))
    return n, bb, msgs


def export(o, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    from mathutils import Matrix
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


def setup_render(objs, res=1600):
    """Контактный лист: солнце + мягкий заполняющий, камера 3/4 как на превью кита."""
    from mathutils import Vector
    scn = bpy.context.scene
    sun_d = bpy.data.lights.new("Sun", "SUN")
    sun_d.energy, sun_d.angle = 3.4, math.radians(9)
    sun = bpy.data.objects.new("Sun", sun_d)
    sun.rotation_euler = V.R(52, 0, 38)
    scn.collection.objects.link(sun)
    fill_d = bpy.data.lights.new("Fill", "SUN")
    fill_d.energy = 1.1
    fill = bpy.data.objects.new("Fill", fill_d)
    fill.rotation_euler = V.R(64, 0, -128)
    scn.collection.objects.link(fill)

    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.90, 0.92, 0.86, 1)
    bg.inputs[1].default_value = 1.0
    scn.world = world

    # земля под зданиями
    g = bmesh.new()
    bmesh.ops.create_grid(g, x_segments=1, y_segments=1, size=200)
    gm = bpy.data.meshes.new("Ground")
    g.to_mesh(gm); g.free()
    gmat = bpy.data.materials.new("Ground")
    gmat.use_nodes = True
    gb = next(n for n in gmat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    gb.inputs["Base Color"].default_value = (0.86, 0.88, 0.80, 1)
    gb.inputs["Roughness"].default_value = 1.0
    gm.materials.append(gmat)
    scn.collection.objects.link(bpy.data.objects.new("Ground", gm))

    aspect = 0.62 if len(objs) > 3 else 0.80
    scn.render.engine = "CYCLES"
    scn.cycles.samples = 48
    scn.cycles.use_denoising = True
    scn.render.film_transparent = False
    scn.render.resolution_x = res
    scn.render.resolution_y = int(res * aspect)

    cam_d = bpy.data.cameras.new("Cam")
    cam_d.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_d)
    cam.rotation_euler = V.R(62, 0, -26)
    cam.location = Vector((0, 0, 0))
    scn.collection.objects.link(cam)
    scn.camera = cam
    bpy.context.view_layer.update()

    # Кадр считаем в системе камеры: проецируем все углы боксов и берём реальные габариты.
    # Иначе наклонённая орто-камера режет ряд снизу, а сверху остаётся пустое поле.
    inv = cam.matrix_world.inverted()
    pts = [inv @ (o.matrix_world @ Vector(c)) for o in objs for c in o.bound_box]
    xs = [p.x for p in pts]; ys = [p.y for p in pts]; zs = [p.z for p in pts]
    pad = 0.5
    wspan = (max(xs) - min(xs)) + 2 * pad
    hspan = (max(ys) - min(ys)) + 2 * pad
    cam_d.ortho_scale = max(wspan, hspan / aspect)
    # сдвигаем камеру в её собственной плоскости так, чтобы центр бокса попал в центр кадра
    ctr = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, max(zs) + 30.0))
    cam.location = cam.matrix_world @ ctr
    bpy.context.view_layer.update()
    scn.render.image_settings.file_format = "PNG"


def main():
    out, part, do_all, render = parse_args()
    wipe()
    tex_dir = os.path.join(out, "Textures") if out else os.path.join(HERE, "_tex")
    mat = make_material(tex_dir)

    names = PARTS if do_all else [part]
    names = [n for n in names if n]
    built, total, failed = [], 0, []
    for nm in names:
        try:
            o, mod = build_one(nm, mat)
        except Exception as ex:
            import traceback; traceback.print_exc()
            print("FAIL %s: %s" % (nm, ex))
            failed.append(nm)
            continue
        n, bb, msgs = check(o, mod)
        total += n
        built.append((o, mod, n, bb))
        if out:
            export(o, os.path.join(out, "Buildings", mod.NAME + ".fbx"))

    print("TOTAL tris=%d over %d assets; failed=%s" % (total, len(built), failed or "none"))

    if out:
        manifest = [dict(name=m.NAME, tris=n,
                         size_unity=[round(bb["w"], 3), round(bb["h"], 3), round(bb["d"], 3)])
                    for _, m, n, bb in built]
        with open(os.path.join(out, "buildings.json"), "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)

    if render and built:
        # сетка 4 x N: длинный ряд в орто-кадре мельчает, сеткой каждый объект крупнее
        cols = 4 if len(built) > 4 else len(built)
        cw = max(bb["w"] for _, _, _, bb in built) + 1.1
        cd = max(bb["d"] for _, _, _, bb in built) + 1.8
        rows = (len(built) + cols - 1) // cols
        for i, (o, m, n, bb) in enumerate(built):
            r, c = divmod(i, cols)
            o.location.x = (c - (cols - 1) / 2) * cw
            # дальние ряды уходят в +Y, ближний ряд ближе к камере
            o.location.y = ((rows - 1) / 2 - r) * cd
        # без update() matrix_world остаётся единичной и камера кадрирует пустое место
        bpy.context.view_layer.update()
        setup_render([o for o, _, _, _ in built])
        bpy.context.scene.render.filepath = os.path.join(out or HERE, "preview.png")
        bpy.ops.render.render(write_still=True)
        print("rendered", bpy.context.scene.render.filepath)

    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
