"""
Иконки зданий и ресурсов Vitaria из моделей кита: один ракурс, один свет, прозрачный фон.

Запуск:  python render_icons.py --blend vitaria_kit.blend [--samples 64] [--only buildings|resources]
                                [--keys flour,bread,...]

--keys: рендерятся только эти иконки, остальные кадры берутся из прежнего атласа как есть, пиксель в пиксель.
Без --keys рендерится всё.

Выход (Strategy_Kit/Icons), формат TexturePacker как у старых атласов игры:
  buildings-icons-0.png + buildings-icons.json   512 x 512, клетки 128, кадры 126 x 126 (16 мест)
  resources-icons-0.png + resources-icons.json   2048 x 2048, клетки 256, кадры 254 x 254 (64 места). Кадры идут
                                                 блоками 1024 px по 4 в ряд: первые 32 стоят там же, где стояли
                                                 в атласе 1024 x 2048, следующие 32 — в правой половине
  icons_preview.png                             оба атласа на фоне панели HUD
Имена кадров прежние (BuildingIcon_<Kind>.png, iron-ore.png, ...): ResourceAtlasImporter режет по ним,
spriteID сохраняются по имени — ссылки в префабах и определениях не рвутся.
"""
import bpy, os, sys, json, math, importlib
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

RENDER_PX = 512
ATLASES = {
    # имя атласа: ((ширина, высота) атласа, клетка, кадр, азимут камеры, высота камеры)
    "buildings-icons": ((512, 512), 128, 126, 34.0, 30.0),
    "resources-icons": ((2048, 2048), 256, 254, 24.0, 36.0),
}
# Кадры раскладываются блоками BLOCK px по 4 в ряд: атлас растёт вправо, а прежние кадры не сдвигаются.
# 2048 — потолок maxTextureSize импорта атласа в игре.
BLOCK = 1024
LIGHT_FROM = (-0.5, -0.62, 0.6)      # ключевой свет спереди-слева-сверху: видимые грани с разной светотенью


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    o = {"blend": None, "samples": 64, "only": None, "out": None, "keys": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--blend", "--only", "--out", "--keys") and i + 1 < len(argv):
            o[a[2:]] = argv[i + 1]
            i += 1
        elif a == "--samples" and i + 1 < len(argv):
            o["samples"] = int(argv[i + 1])
            i += 1
        i += 1
    return o


def icon_scene(samples):
    scn = bpy.data.scenes.get("Icons")
    if scn is not None:
        for ob in list(scn.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
    else:
        scn = bpy.data.scenes.new("Icons")
    cd = bpy.data.cameras.get("IconCam") or bpy.data.cameras.new("IconCam")
    cd.type = "ORTHO"
    cd.clip_start, cd.clip_end = 0.1, 400
    cam = bpy.data.objects.new("IconCam", cd)
    scn.collection.objects.link(cam)
    scn.camera = cam
    sd = bpy.data.lights.get("IconSun") or bpy.data.lights.new("IconSun", "SUN")
    sd.energy = 4.0
    sd.color = (1.0, 0.96, 0.9)
    sd.angle = math.radians(8)
    sun = bpy.data.objects.new("IconSun", sd)
    sun.rotation_euler = (-Vector(LIGHT_FROM)).normalized().to_track_quat("-Z", "Y").to_euler()
    scn.collection.objects.link(sun)
    world = bpy.data.worlds.get("Icon_World") or bpy.data.worlds.new("Icon_World")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.56, 0.68, 0.84, 1)
    bg.inputs[1].default_value = 1.0
    scn.world = world
    r = scn.render
    r.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.samples = samples
    scn.cycles.use_denoising = True
    scn.cycles.max_bounces = 4
    r.film_transparent = True
    r.resolution_x = r.resolution_y = RENDER_PX
    r.resolution_percentage = 100
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    scn.view_settings.view_transform = "Standard"
    scn.view_settings.look = "None"
    scn.view_settings.exposure = -0.3
    # ловец тени: мягкая тень под предметом остаётся в альфе PNG
    me = bpy.data.meshes.get("IconGround")
    if me is None:
        me = bpy.data.meshes.new("IconGround")
        s = 6.0
        me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new("IconGround", me)
    ground.is_shadow_catcher = True
    scn.collection.objects.link(ground)
    return scn, cam, ground


def frame_camera(cam, objs, az, el, pad=0.07, focus=None):
    """focus (x0, x1, y0, y1) в осях модели: кадр по вершинам только внутри рамки (у плавильни —
    печь без углов мощёного двора, иначе печь в иконке мелкая)."""
    d = Vector((math.sin(math.radians(az)) * math.cos(math.radians(el)),
                -math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
    fwd = -d
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd)
    xs, ys, cs = [], [], []
    for ob in objs:
        mw = ob.matrix_world
        for v in ob.data.vertices:
            if focus and not (focus[0] <= v.co.x <= focus[1] and focus[2] <= v.co.y <= focus[3]):
                continue
            w = mw @ v.co
            xs.append(w.dot(right))
            ys.append(w.dot(up))
            cs.append(w)
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    ext = max(max(xs) - min(xs), max(ys) - min(ys))
    depth = sum(cs, Vector()) / len(cs)
    center = right * cx + up * cy + fwd * depth.dot(fwd)
    cam.location = center - fwd * 60.0
    cam.rotation_euler = fwd.to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = ext * (1 + 2 * pad)


def build_subject(V, mat, placements, key):
    """Меши предмета: строка — готовый меш кита, функция — модель из vitaria_icons."""
    out = []
    for k, (src, loc, rot, s) in enumerate(placements):
        if isinstance(src, str):
            me = bpy.data.meshes.get(src)
            if me is None:
                raise RuntimeError("нет меша %s для иконки %s" % (src, key))
        else:
            name = "Icon_%s_%d" % (key, k)
            a = V.Asset(name)
            src(a)
            me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
            a.bm.to_mesh(me)
            a.bm.free()
            me.materials.clear()
            me.materials.append(mat)
        out.append((me, loc, rot, s))
    return out


def render_icon(scn, cam, subject, az, el, path, focus=None):
    objs = []
    for me, loc, rot, s in subject:
        ob = bpy.data.objects.new(me.name + "_icon", me)
        ob.location = loc
        ob.rotation_euler = tuple(math.radians(x) for x in rot)
        ob.scale = (s, s, s)
        scn.collection.objects.link(ob)
        objs.append(ob)
    for vl in scn.view_layers:
        vl.update()
    frame_camera(cam, objs, az, el, focus=focus)
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=scn.name)
    for ob in objs:
        bpy.data.objects.remove(ob, do_unlink=True)


def slot(i, size, cell):
    """Левый верхний угол клетки i: блоки шириной BLOCK, в блоке — по строкам слева направо."""
    aw, ah = size
    bw = min(aw, BLOCK)
    per_row = bw // cell
    per_block = per_row * (ah // cell)
    b, j = divmod(i, per_block)
    return b * bw + (j % per_row) * cell, (j // per_row) * cell, (aw // bw) * per_block


def atlas_frames(out_dir, name):
    """Кадры прежнего атласа: имя кадра -> картинка кадра как есть (их не пересэмплируем)."""
    from PIL import Image
    png, js = os.path.join(out_dir, name + "-0.png"), os.path.join(out_dir, name + ".json")
    if not (os.path.exists(png) and os.path.exists(js)):
        return {}
    atlas = Image.open(png).convert("RGBA")
    out = {}
    for f in json.load(open(js, encoding="utf-8"))["textures"][0]["frames"]:
        r = f["frame"]
        out[os.path.splitext(f["filename"])[0]] = atlas.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"]))
    return out


def pack(name, frames, out_dir):
    """frames: [(имя кадра, путь к рендеру или готовый кадр PIL)] -> атлас PNG + JSON TexturePacker (без
    поворотов и обрезки). Готовый кадр нужного размера (из прежнего атласа) ставится как есть."""
    from PIL import Image
    (aw, ah), cell, fpx, _, _ = ATLASES[name]
    cap = slot(0, (aw, ah), cell)[2]
    if len(frames) > cap:
        raise RuntimeError("%s: %d кадров не помещаются в %dx%d (клетка %d)" % (name, len(frames), aw, ah, cell))
    atlas = Image.new("RGBA", (aw, ah), (0, 0, 0, 0))
    items = []
    for i, (fname, src) in enumerate(frames):
        if isinstance(src, str):
            im = Image.open(src).convert("RGBA")
            im = im.convert("RGBa").resize((fpx, fpx), Image.LANCZOS).convert("RGBA")   # без тёмной каймы по альфе
        else:
            im = src
            assert im.size == (fpx, fpx), (fname, im.size)
        cx, cy, _ = slot(i, (aw, ah), cell)
        x, y = cx + (cell - fpx) // 2, cy + (cell - fpx) // 2
        atlas.paste(im, (x, y))
        items.append({"filename": fname, "rotated": False, "trimmed": False,
                      "sourceSize": {"w": fpx, "h": fpx},
                      "spriteSourceSize": {"x": 0, "y": 0, "w": fpx, "h": fpx},
                      "frame": {"x": x, "y": y, "w": fpx, "h": fpx}})
    png = os.path.join(out_dir, name + "-0.png")
    atlas.save(png)
    data = {"textures": [{"image": name + "-0.png", "format": "RGBA8888", "size": {"w": aw, "h": ah},
                          "scale": 1, "frames": items}],
            "meta": {"app": "Strategy_Kit/Blender/render_icons.py", "version": "1.0"}}
    with open(os.path.join(out_dir, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print("atlas", png, len(items))
    return png


def preview(out_dir, names):
    from PIL import Image, ImageDraw
    ims = [Image.open(os.path.join(out_dir, n + "-0.png")).convert("RGBA") for n in names]
    W = sum(im.width for im in ims) + 40 * (len(ims) + 1)
    H = max(im.height for im in ims) + 80
    sheet = Image.new("RGBA", (W, H), (19, 27, 35, 255))       # --surface панели HUD
    x = 40
    d = ImageDraw.Draw(sheet)
    for n, im in zip(names, ims):
        sheet.alpha_composite(im, (x, 60))
        d.text((x, 24), n, fill=(241, 232, 213, 255))
        x += im.width + 40
    path = os.path.join(out_dir, "icons_preview.png")
    sheet.convert("RGB").save(path)
    print("preview", path)


def main():
    o = parse_args()
    if o["blend"]:
        bpy.ops.wm.open_mainfile(filepath=o["blend"])
    import build_vitaria as V
    V = importlib.reload(V)
    import build_arena as BA
    BA = importlib.reload(BA)
    import vitaria_icons as VI
    VI = importlib.reload(VI)
    root = os.path.normpath(os.path.join(HERE, ".."))
    out_dir = o["out"] or os.path.join(root, "Icons")
    tmp = os.path.join(out_dir, "_renders")
    os.makedirs(tmp, exist_ok=True)
    mat = BA.palette_material(V, os.path.join(root, "Unity", "Assets", "Vitaria", "Textures"))
    ref_dir = BA.find_ref(HERE, None)
    if ref_dir and ref_dir not in sys.path:
        sys.path.insert(0, ref_dir)
    import vitaria_buildings as VB                            # свои модели казармы и склада (Ref_Buildings)
    VB = importlib.reload(VB)
    for me, line in VB.build_ref_player(V, BA.build_mesh, mat):
        print(line)
    scn, cam, ground = icon_scene(o["samples"])
    todo = []
    if o["only"] in (None, "buildings"):
        todo.append(("buildings-icons", [("BuildingIcon_%s.png" % k, [(mesh, (0, 0, 0), (0, 0, 0), 1.0)])
                                         for k, mesh in VI.BUILDING_ICONS.items()]))
    if o["only"] in (None, "resources"):
        todo.append(("resources-icons", [("%s.png" % k, pl) for k, pl in VI.RESOURCE_ICONS.items()]))
    keys = o["keys"].split(",") if o["keys"] else None
    for atlas, entries in todo:
        _, _, _, az, el = ATLASES[atlas]
        prev = atlas_frames(out_dir, atlas) if keys is not None else {}
        frames = []
        for fname, placements in entries:
            key = os.path.splitext(fname)[0]
            if keys is not None and key not in keys and key in prev:      # --keys: прочие кадры — из атласа
                frames.append((fname, prev[key]))
                continue
            path = os.path.join(tmp, key + ".png")
            if keys is None or key in keys or not os.path.exists(path):
                focus = VI.ICON_FOCUS.get(key)
                render_icon(scn, cam, build_subject(V, mat, placements, key), az, el, path, focus)
                print("icon", fname)
            frames.append((fname, path))
        pack(atlas, frames, out_dir)
    preview(out_dir, [a for a in ATLASES if os.path.exists(os.path.join(out_dir, a + "-0.png"))])
    print("done")


if __name__ == "__main__":
    main()
