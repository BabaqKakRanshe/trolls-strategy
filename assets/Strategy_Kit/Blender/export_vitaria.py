"""
Vitaria — export the CURRENT .blend to Unity (does not rebuild anything, your edits stay as they are).

Blender GUI:  Scripting tab > open this file > Run Script (the .blend must be saved).
Command line: blender -b vitaria_kit.blend --python export_vitaria.py -- [--out <Strategy_Kit folder>]

By default writes next to the .blend:  <blend folder>/../Unity/Assets/Vitaria/...
  Models/<Category>/<Asset>.fbx   one FBX per kit object (collections Kit_*)
                                  + one FBX per unique scene object (mesh not shared with the kit)
  Models/Vitaria_Scene.fbx        whole scene, objects separate
  Layout/vitaria_layout.json      every object of Vitaria_Scene in Unity coordinates + camera + sun
  ../../../asset_list.json        triangles and sizes; warns when a kit asset changed a lot

Rules the script relies on:
  * kit assets live in collections Vitaria_Kit/Kit_<Category>
  * the scene lives in Vitaria_Scene/Scene_<Category>
  * a scene object whose mesh is shared with a kit object is an instance of that asset;
    any other scene mesh is exported as its own asset named after the mesh.
  Before Ctrl+J on an instance, do Object > Relations > Make Single User > Object & Data,
  otherwise the join also changes the kit object that shares the mesh.
"""
import bpy, os, sys, json, math
from mathutils import Matrix, Vector

AMBIENT = {"sky": [0.62, 0.70, 0.80], "equator": [0.50, 0.55, 0.47], "ground": [0.27, 0.24, 0.20]}
BACKGROUND = [0.176, 0.216, 0.165]
SUN_INTENSITY_UNITY = 1.35


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, blend = None, None
    for i, a in enumerate(argv):
        if a == "--out" and i + 1 < len(argv):
            out = os.path.abspath(argv[i + 1])
        if a == "--blend" and i + 1 < len(argv):
            blend = os.path.abspath(argv[i + 1])
    return out, blend


C4 = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))   # Blender -> Unity axes (FBX -Z fwd, Y up)


def unity_trs(mw):
    loc, rot, scl = (C4 @ mw @ C4.inverted()).decompose()
    return ([round(v, 4) for v in loc], [round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)],
            [round(v, 4) for v in scl])


def uvec(v):
    return [round(-v.x, 5), round(v.z, 5), round(-v.y, 5)]


FBX_KW = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
              axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
              use_mesh_modifiers=True, use_triangles=True, use_tspace=False, add_leaf_bones=False,
              bake_anim=False, path_mode="STRIP", embed_textures=False, use_custom_props=False)


def select_only(objs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def export_at_origin(o, path):
    saved = o.matrix_world.copy()
    o.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()
    select_only([o])
    bpy.ops.export_scene.fbx(filepath=path, **FBX_KW)
    o.matrix_world = saved
    bpy.context.view_layer.update()


def tris_and_size(o):
    dg = bpy.context.evaluated_depsgraph_get()
    me = o.evaluated_get(dg).to_mesh()
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    if len(me.vertices):
        xs, ys, zs = zip(*[tuple(v.co) for v in me.vertices])
        size = [round(max(xs) - min(xs), 3), round(max(zs) - min(zs), 3), round(max(ys) - min(ys), 3)]
    else:
        size = [0, 0, 0]
    o.evaluated_get(dg).to_mesh_clear()
    return n, size


def main():
    out, blend = parse_args()
    if blend:
        bpy.ops.wm.open_mainfile(filepath=blend)
    if not bpy.data.filepath and not out:
        raise RuntimeError("Save the .blend first or pass --out")
    root = out or os.path.normpath(os.path.join(os.path.dirname(bpy.data.filepath), ".."))
    unity = os.path.join(root, "Unity", "Assets", "Vitaria")
    models = os.path.join(unity, "Models")
    layout_dir = os.path.join(unity, "Layout")
    os.makedirs(layout_dir, exist_ok=True)
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    # --- kit
    kit_by_mesh, assets = {}, {}
    for coll in bpy.data.collections["Vitaria_Kit"].children:
        cat = coll.name.replace("Kit_", "", 1)
        for o in coll.all_objects:
            if o.type != "MESH":
                continue
            kit_by_mesh[o.data.name] = o.name
            assets[o.name] = (o, cat)

    # --- scene
    items, unique = [], {}
    for coll in bpy.data.collections["Vitaria_Scene"].children:
        cat = coll.name.replace("Scene_", "", 1)
        for o in coll.all_objects:
            if o.type != "MESH":
                print("skip (not a mesh):", o.name)
                continue
            if o.data.name in kit_by_mesh:
                asset = kit_by_mesh[o.data.name]
                group = assets[asset][1]
            else:
                asset = o.data.name
                group = cat
                if asset not in unique and asset not in assets:
                    unique[asset] = (o, cat)
            p, q, s = unity_trs(o.matrix_world)
            items.append({"name": o.name, "asset": asset, "group": group, "p": p, "r": q, "s": s})

    # --- FBX per asset
    info = []
    for name, (o, cat) in list(assets.items()) + list(unique.items()):
        d = os.path.join(models, cat)
        os.makedirs(d, exist_ok=True)
        export_at_origin(o, os.path.join(d, name + ".fbx"))
        n, size = tris_and_size(o)
        info.append({"asset": name, "category": cat, "tris": n, "size_unity": size})
        print("exported", cat, name, n, "tris")

    scene_objs = [o for o in bpy.data.collections["Vitaria_Scene"].all_objects if o.type == "MESH"]
    select_only(scene_objs)
    bpy.ops.export_scene.fbx(filepath=os.path.join(models, "Vitaria_Scene.fbx"), **FBX_KW)

    # --- layout
    scn = bpy.context.scene
    layout = {"objects": items, "ambient": AMBIENT, "background": BACKGROUND}
    cam = scn.camera
    if cam:
        aspect = scn.render.resolution_x / max(1, scn.render.resolution_y)
        if cam.data.sensor_fit == "VERTICAL" or (cam.data.sensor_fit == "AUTO" and aspect < 1):
            vfov = cam.data.angle
        else:
            vfov = 2 * math.atan(math.tan(cam.data.angle / 2) / aspect)
        m3 = cam.matrix_world.to_3x3()
        layout["camera"] = {"position": uvec(cam.matrix_world.translation), "forward": uvec(m3 @ Vector((0, 0, -1))),
                            "up": uvec(m3 @ Vector((0, 1, 0))), "fov": round(math.degrees(vfov), 3)}
    sun = next((o for o in scn.objects if o.type == "LIGHT" and o.data.type == "SUN"), None)
    if sun:
        layout["sun"] = {"forward": uvec(sun.matrix_world.to_3x3() @ Vector((0, 0, -1))),
                         "color": [round(c, 4) for c in sun.data.color], "intensity": SUN_INTENSITY_UNITY}
    with open(os.path.join(layout_dir, "vitaria_layout.json"), "w") as f:
        json.dump(layout, f, indent=1)

    # --- asset list + warnings about kit assets that changed a lot
    list_path = os.path.join(root, "asset_list.json")
    prev = {}
    if os.path.exists(list_path):
        try:
            prev = {x["asset"]: x for x in json.load(open(list_path))}
        except Exception:
            prev = {}
    for x in info:
        old = prev.get(x["asset"])
        if old and x["asset"] in assets and (x["tris"] > old["tris"] * 2 or
                                             max(x["size_unity"]) > max(old["size_unity"]) * 2 + 0.2):
            print("WARNING: kit asset %s changed: %d -> %d tris, size %s -> %s (joined into a shared mesh?)" %
                  (x["asset"], old["tris"], x["tris"], old["size_unity"], x["size_unity"]))
    with open(list_path, "w") as f:
        json.dump(info, f, indent=1)
    print("done: %d assets (%d scene-only), %d scene objects -> %s" % (len(info), len(unique), len(items), unity))


if __name__ == "__main__":
    main()
