"""
Vitaria — low-poly kit: procedural build in Blender + export for Unity.

Run (any of):
  blender --background --python build_vitaria.py -- --out ./Vitaria_Kit [--render]
  python3 build_vitaria.py --out ./Vitaria_Kit [--render]          (pip install bpy==4.5.*)
  Blender GUI: open in the Text Editor of an EMPTY file, set DEFAULT_OUT below, Run Script.

Every asset is its own object with its pivot at the bottom-centre, front facing Blender -Y
(= Unity +Z after export). One shared material "Vitaria_Palette" samples a 256x256 palette
texture (16px swatches + gradient ramps), so the whole kit batches with a single material.

Outputs (under --out):
  Blender/vitaria_kit.blend
  Unity/Assets/Vitaria/Models/<Category>/<Asset>.fbx      one FBX per object
  Unity/Assets/Vitaria/Models/Vitaria_Scene.fbx           whole scene, objects separate
  Unity/Assets/Vitaria/Textures/Vitaria_Palette*.png
  Unity/Assets/Vitaria/Layout/vitaria_layout.json         placements in Unity coordinates
  Previews/*.png                                          (with --render)
"""
import bpy, bmesh, math, random, os, sys, json
from mathutils import Vector, Matrix, Euler, Quaternion, noise

DEFAULT_OUT = os.path.join(os.path.expanduser("~"), "Vitaria_Kit")
SAMPLES_SCALE, RES_SCALE = 1.0, 100
HERO_ONLY = False

# ----------------------------------------------------------------------------------------
# args
# ----------------------------------------------------------------------------------------
def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out, render = DEFAULT_OUT, False
    global SAMPLES_SCALE, RES_SCALE
    i = 0
    while i < len(argv):
        if argv[i] == "--out" and i + 1 < len(argv):
            out = os.path.abspath(argv[i + 1]); i += 1
        elif argv[i] == "--draft":
            SAMPLES_SCALE, RES_SCALE = 0.25, 50
        elif argv[i] == "--hero-only":
            global HERO_ONLY
            HERO_ONLY = True
        elif argv[i] == "--render":
            render = True
        i += 1
    return out, render

# ----------------------------------------------------------------------------------------
# palette
# ----------------------------------------------------------------------------------------
TEX = 256
NCELL = 16                      # 16x16 swatches of 16px
PALETTE = [
    # foliage / ground cover
    ("pine_dark", "#315a35"), ("pine_mid", "#407040"), ("pine_light", "#56884a"),
    ("leaf_dark", "#4a7a3a"), ("leaf_mid", "#5d9046"), ("leaf_light", "#79a756"),
    ("bush", "#55863f"), ("blade", "#6a9a45"), ("blade_dark", "#4c7a36"), ("moss", "#6f8f45"),
    ("berry", "#c23a2e"), ("flower_w", "#f3efe2"), ("flower_y", "#f2c94c"), ("flower_p", "#c47bbd"),
    ("grass_lip", "#4d7a36"), ("leaf_autumn", "#c98a3a"),
    # wood
    ("wood_dark", "#583822"), ("wood_mid", "#7b5031"), ("wood_mid2", "#8a5b37"), ("wood_light", "#a57549"),
    ("wood_pale", "#caa47b"), ("wood_yellow", "#c49a52"), ("bark", "#664630"), ("bark_dark", "#4e3524"),
    # stone
    ("stone_dark", "#5d5e5c"), ("stone_mid", "#81827e"), ("stone_light", "#a4a49d"), ("stone_warm", "#958b7d"),
    ("rock", "#8a8983"), ("rock_dark", "#6a6964"), ("coal", "#2b2a2c"), ("soil_rock", "#6c665e"),
    # roof / cloth
    ("roof", "#b4462f"), ("roof_dark", "#933620"), ("roof_light", "#c85a3d"), ("stripe_red", "#b8422e"),
    ("cream", "#efe4c9"), ("burlap", "#b59c6c"), ("burlap_dark", "#8e7750"), ("rope", "#c9b27e"),
    # metal
    ("iron_dark", "#3b3e44"), ("iron", "#6d737c"), ("iron_light", "#a6adb6"), ("rail", "#575c64"),
    ("gold", "#e2ae30"), ("gold_dark", "#b0801c"), ("gold_light", "#f9dd74"),
    ("copper", "#c8703a"), ("copper_dark", "#94502a"), ("copper_light", "#eb9c63"),
    # ore
    ("ore_rock", "#5d5961"), ("ore_rock_dk", "#46434a"), ("ore_gold", "#f4c63e"),
    ("iron_ore_rock", "#6a5249"), ("iron_ore_vein", "#b45a35"), ("copper_ore_vein", "#dc7b3a"), ("malachite", "#3e9d85"),
    # soil (island sides)
    ("soil_light", "#7a5638"), ("soil_mid", "#624430"), ("soil_dark", "#4a3323"),
    # misc
    ("black", "#16110e"), ("glass", "#3b4b57"), ("glow", "#ff7f24"), ("glow_hot", "#ffd06a"), ("lantern_glow", "#ffc160"),
    ("label", "#1d231b"),
]
EMISSIVE = {"glow", "glow_hot", "lantern_glow"}
RAMPS = [
    ("grass", [(0.0, "#46652f"), (0.35, "#5b7d3c"), (0.7, "#7b9a4e"), (1.0, "#a2b769")]),
    ("dirt", [(0.0, "#6e4d33"), (0.3, "#936a47"), (0.7, "#b58658"), (1.0, "#cda172")]),
]
RAMP_ROW0 = NCELL - len(RAMPS)   # ramps occupy the bottom rows

def hex2rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5))

SW_UV, RAMP_ROW = {}, {}
for _i, (_n, _h) in enumerate(PALETTE):
    _c, _r = _i % NCELL, _i // NCELL
    assert _r < RAMP_ROW0, "palette overflow"
    SW_UV[_n] = ((_c + 0.5) / NCELL, 1.0 - (_r + 0.5) / NCELL)
for _i, (_n, _s) in enumerate(RAMPS):
    RAMP_ROW[_n] = RAMP_ROW0 + _i

def clamp(x, a=0.0, b=1.0):
    return a if x < a else b if x > b else x

def ramp_uv(name, t):
    t = clamp(t)
    return ((2.0 + t * (TEX - 4.0)) / TEX, 1.0 - (RAMP_ROW[name] + 0.5) / NCELL)

def lerp_stops(stops, t):
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            k = (t - t0) / (t1 - t0)
            a, b = hex2rgb(c0), hex2rgb(c1)
            return tuple(a[j] + (b[j] - a[j]) * k for j in range(3))
    return hex2rgb(stops[-1][1])

def build_palette_images(tex_dir):
    import numpy as np
    sw = TEX // NCELL
    albedo = np.zeros((TEX, TEX, 4), dtype=np.float32); albedo[..., 3] = 1
    emis = np.zeros((TEX, TEX, 4), dtype=np.float32); emis[..., 3] = 1
    for i, (name, hx) in enumerate(PALETTE):
        c, r = i % NCELL, i // NCELL
        albedo[r * sw:(r + 1) * sw, c * sw:(c + 1) * sw, :3] = hex2rgb(hx)
        if name in EMISSIVE:
            emis[r * sw:(r + 1) * sw, c * sw:(c + 1) * sw, :3] = hex2rgb(hx)
    for name, stops in RAMPS:
        r = RAMP_ROW[name]
        for px in range(TEX):
            t = clamp((px + 0.5 - 2.0) / (TEX - 4.0))
            albedo[r * sw:(r + 1) * sw, px, :3] = lerp_stops(stops, t)
    imgs = []
    for name, arr in (("Vitaria_Palette", albedo), ("Vitaria_Palette_Emission", emis)):
        img = bpy.data.images.new(name, TEX, TEX, alpha=False)
        img.pixels.foreach_set(np.flipud(arr).ravel())      # Blender stores rows bottom-up
        path = os.path.join(tex_dir, name + ".png")
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        imgs.append(img)
    return imgs

def make_material(pal, emi):
    mat = bpy.data.materials.new("Vitaria_Palette")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = pal; t.interpolation = "Linear"; t.location = (-420, 260)
    e = nt.nodes.new("ShaderNodeTexImage"); e.image = emi; e.interpolation = "Linear"; e.location = (-420, -120)
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(e.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Roughness"].default_value = 0.86
    bsdf.inputs["Specular IOR Level"].default_value = 0.3
    bsdf.inputs["Emission Strength"].default_value = 4.0
    return mat

# ----------------------------------------------------------------------------------------
# geometry helpers — every part is a temp bmesh merged into the asset with one colour
# ----------------------------------------------------------------------------------------
def R(*deg):
    return Euler([math.radians(a) for a in deg])

def TM(loc=(0, 0, 0), rot=(0, 0, 0), scl=(1, 1, 1)):
    return Matrix.LocRotScale(Vector(loc), R(*rot).to_quaternion(), Vector(scl))

def _bevel(b, off, seg=1, edges=None):
    bmesh.ops.bevel(b, geom=list(edges if edges is not None else b.edges), offset=off, offset_type="OFFSET",
                    segments=seg, profile=0.5, affect="EDGES", clamp_overlap=True)

def _place(b, loc, rot, mat):
    m = mat if mat is not None else TM(loc, rot)
    bmesh.ops.transform(b, matrix=m, verts=b.verts)
    b.normal_update()
    return b

def p_box(size, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, mat=None):
    """Box centred on loc."""
    b = bmesh.new()
    bmesh.ops.create_cube(b, size=1.0)
    bmesh.ops.scale(b, vec=Vector(size), verts=b.verts)
    if bevel > 0:
        _bevel(b, bevel)
    return _place(b, loc, rot, mat)

def p_taper_box(bottom, top, h, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, mat=None):
    """Box with different bottom/top XY sizes, base at loc.z."""
    b = bmesh.new()
    bmesh.ops.create_cube(b, size=1.0)
    for v in b.verts:
        sx, sy = (top if v.co.z > 0 else bottom)
        v.co.x *= sx; v.co.y *= sy; v.co.z = (v.co.z + 0.5) * h
    if bevel > 0:
        _bevel(b, bevel)
    return _place(b, loc, rot, mat)

def p_cyl(r1, r2, h, seg=8, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, spin=0.0, cap=True, mat=None):
    """Cylinder/cone with its BASE at loc, axis = local Z."""
    b = bmesh.new()
    bmesh.ops.create_cone(b, cap_ends=cap, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h)
    bmesh.ops.translate(b, vec=(0, 0, h / 2), verts=b.verts)
    if spin:
        bmesh.ops.rotate(b, matrix=Matrix.Rotation(math.radians(spin), 3, "Z"), verts=b.verts, cent=(0, 0, 0))
    if bevel > 0 and cap:
        rim = [e for e in b.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-5 and e.is_boundary is False
               and (abs(e.verts[0].co.z) < 1e-5 or abs(e.verts[0].co.z - h) < 1e-5)]
        if r2 < 1e-4:
            rim = [e for e in rim if abs(e.verts[0].co.z) < 1e-5]
        _bevel(b, bevel, edges=rim)
    return _place(b, loc, rot, mat)

def p_ico(r, sub=2, loc=(0, 0, 0), scl=(1, 1, 1), rot=(0, 0, 0), jitter=0.0, rng=None, cut=None, mat=None):
    """Icosphere centred on loc; jitter is a fraction of r; cut = local z below which geometry is removed."""
    b = bmesh.new()
    bmesh.ops.create_icosphere(b, subdivisions=sub, radius=r)
    if jitter:
        rng = rng or random.Random(0)
        for v in b.verts:
            v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * jitter * r
    bmesh.ops.scale(b, vec=Vector(scl), verts=b.verts)
    if cut is not None:
        bmesh.ops.bisect_plane(b, geom=b.verts[:] + b.edges[:] + b.faces[:], plane_co=(0, 0, cut),
                               plane_no=(0, 0, 1), clear_inner=True)
    return _place(b, loc, rot, mat)

def p_prism(pts_xz, depth, loc=(0, 0, 0), rot=(0, 0, 0), mat=None):
    """Polygon in the XZ plane (counter-clockwise seen from -Y) extruded along Y, centred."""
    b = bmesh.new()
    f = [b.verts.new((x, -depth / 2, z)) for x, z in pts_xz]
    k = [b.verts.new((x, depth / 2, z)) for x, z in pts_xz]
    n = len(pts_xz)
    b.faces.new(f)
    b.faces.new(list(reversed(k)))
    for i in range(n):
        j = (i + 1) % n
        b.faces.new((f[i], k[i], k[j], f[j]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    return _place(b, loc, rot, mat)

def by_normal(up, side, down=None, thr=0.55):
    down = down or side
    return lambda f: up if f.normal.z > thr else (down if f.normal.z < -thr else side)


class Asset:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def add(self, part, color, smooth=False):
        vmap = {v: self.bm.verts.new(v.co) for v in part.verts}
        for f in part.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.smooth = smooth(f) if callable(smooth) else smooth
            col = color(f) if callable(color) else color
            uv = SW_UV[col]
            for l in nf.loops:
                l[self.uv].uv = uv
        part.free()


# ----------------------------------------------------------------------------------------
# island & paths
# ----------------------------------------------------------------------------------------
IW, ID = 20.0, 15.0

def smoothstep(e0, e1, x):
    t = clamp((x - e0) / (e1 - e0))
    return t * t * (3 - 2 * t)

def edge_dist(x, y):
    return min(IW / 2 - abs(x), ID / 2 - abs(y))

def ground_h(x, y):
    e = edge_dist(x, y)
    w = smoothstep(2.9, 1.2, e)                       # flat build area, gentle bumps near the rim
    z = w * (0.07 + 0.09 * noise.noise(Vector((x * 0.45, y * 0.45, 1.7))))
    z -= smoothstep(0.45, 0.0, e) * 0.10              # rounded top edge
    return z

def grass_t(x, y):
    n1 = noise.noise(Vector((x * 0.16, y * 0.16, 4.2)))
    n2 = noise.noise(Vector((x * 0.55, y * 0.55, 8.1)))
    e = edge_dist(x, y)
    return 0.58 + 0.42 * n1 + 0.14 * n2 - 0.3 * smoothstep(1.8, 0.0, e)

def build_island(a):
    bm, uv = a.bm, a.uv
    cs = 0.5
    nx, ny = int(IW / cs), int(ID / cs)
    rng = random.Random(3)
    grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x, y = -IW / 2 + i * cs, -ID / 2 + j * cs
            z = ground_h(x, y)
            px, py = x, y
            on_b = i in (0, nx) or j in (0, ny)
            if on_b:                                    # slightly irregular outline
                n = noise.noise(Vector((x * 0.7, y * 0.7, 2.0))) * 0.09
                if i in (0, nx): px += math.copysign(n, x)
                if j in (0, ny): py += math.copysign(n, y)
            grid[i, j] = bm.verts.new((px, py, z))
    for i in range(nx):
        for j in range(ny):
            f = bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
            for l in f.loops:
                l[uv].uv = ramp_uv("grass", grass_t(l.vert.co.x, l.vert.co.y))
    # perimeter loop (counter-clockwise from above)
    loop = [(i, 0) for i in range(nx)] + [(nx, j) for j in range(ny)] + \
           [(i, ny) for i in range(nx, 0, -1)] + [(0, j) for j in range(ny, 0, -1)]
    # soil rings: (depth, outward offset, colour of band above this ring)
    rings_spec = [(0.13, 0.03, "grass_lip"), (0.24, -0.02, "soil_light"), (0.52, 0.05, "soil_mid"),
                  (0.80, 0.14, "soil_dark"), (1.05, 0.30, "soil_dark")]
    prev = [grid[k] for k in loop]
    for ri, (depth, inset, col) in enumerate(rings_spec):
        ring = []
        for idx, k in enumerate(loop):
            v0 = grid[k]
            i, j = k
            ox = -1 if i == 0 else 1 if i == nx else 0
            oy = -1 if j == 0 else 1 if j == ny else 0
            n = Vector((ox, oy, 0)).normalized()
            wob = noise.noise(Vector((v0.co.x * 0.9, v0.co.y * 0.9, 5 + ri)))
            d = depth + 0.06 * wob * (1 if ri else 0.4)
            off = -inset + 0.05 * rng.uniform(-1, 1) * (1 if ri else 0.3)
            ring.append(bm.verts.new((v0.co.x + n.x * off, v0.co.y + n.y * off, -d)))
        L = len(loop)
        for idx in range(L):
            j2 = (idx + 1) % L
            f = bm.faces.new((prev[idx], ring[idx], ring[j2], prev[j2]))
            c = col
            if col == "soil_mid" and rng.random() < 0.12:
                c = "soil_rock"
            for l in f.loops:
                l[uv].uv = SW_UV[c]
        prev = ring
    bottom = bm.faces.new(list(reversed(prev)))
    for l in bottom.loops:
        l[uv].uv = SW_UV["soil_dark"]
    bmesh.ops.triangulate(bm, faces=[bottom])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

# paths: (control points, taper at start, taper at end, z offset)
PATHS = [
    ([(-8.6, 1.9), (-5.2, 1.6), (-3.0, 1.45), (-0.6, 1.7), (1.6, 1.9), (3.4, 1.75), (4.8, 1.45)], 1.6, 0.5, 0.020),
    ([(0.45, 1.85), (0.25, 0.4), (-0.35, -1.0), (-0.8, -2.35)], 0.0, 0.0, 0.026),
    ([(-8.8, -2.7), (-5.0, -2.4), (-2.4, -2.5), (-0.8, -2.35), (1.2, -2.9), (2.6, -3.55), (3.8, -3.85), (4.7, -3.75)], 1.6, 0.6, 0.022),
]

def catmull_rom(pts, step=0.2):
    P = [Vector((p[0], p[1], 0.0)) for p in pts]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        n = max(2, int((p2 - p1).length / step))
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(P[-2].copy())
    return out

PATH_SAMPLES = []

def build_paths(a):
    bm, uv = a.bm, a.uv
    for pi, (pts, tap0, tap1, zoff) in enumerate(PATHS):
        s = catmull_rom(pts)
        PATH_SAMPLES.extend(s)
        cum = [0.0]
        for k in range(1, len(s)):
            cum.append(cum[-1] + (s[k] - s[k - 1]).length)
        total = cum[-1]
        rows = []
        for k, c in enumerate(s):
            tan = (s[min(k + 1, len(s) - 1)] - s[max(k - 1, 0)]).normalized()
            nrm = Vector((-tan.y, tan.x, 0))
            taper = 1.0
            if tap0 > 0: taper *= smoothstep(0, tap0, cum[k])
            if tap1 > 0: taper *= smoothstep(0, tap1, total - cum[k])
            half = max(0.05, 0.43 * (1 + 0.22 * noise.noise(Vector((cum[k] * 0.55, pi * 7.3, 0.5)))) * taper)
            offs = [-(half + 0.14), -half, -half * 0.5, 0.0, half * 0.5, half, half + 0.14]
            ts = [0.0, 0.28, 0.72, 0.9, 0.72, 0.28, 0.0]
            # ends that join another path: fade the dark edge bands into the light centre (no seam)
            jf = 0.0
            if tap0 == 0: jf = max(jf, 1 - smoothstep(0.0, 1.1, cum[k]))
            if tap1 == 0: jf = max(jf, 1 - smoothstep(0.0, 1.1, total - cum[k]))
            ts = [t + (0.86 - t) * jf for t in ts]
            row = []
            for oi, (o, t) in enumerate(zip(offs, ts)):
                jit = 0.05 * noise.noise(Vector((cum[k] * 1.7, oi * 3.1, pi * 2.0))) if oi not in (3,) else 0
                p = c + nrm * (o + jit)
                z = ground_h(p.x, p.y) + (zoff - 0.013 if oi in (0, 6) else zoff)
                tt = t + 0.1 * noise.noise(Vector((p.x * 1.3, p.y * 1.3, 9.0)))
                row.append((bm.verts.new((p.x, p.y, z)), tt))
            rows.append(row)
        for k in range(len(rows) - 1):
            for i in range(6):
                q = (rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i])
                f = bm.faces.new([x[0] for x in q])
                f.normal_update()
                if f.normal.z < 0:
                    f.normal_flip()
                tmap = {x[0]: x[1] for x in q}
                for l in f.loops:
                    l[uv].uv = ramp_uv("dirt", tmap[l.vert])


# ----------------------------------------------------------------------------------------
# nature
# ----------------------------------------------------------------------------------------
def build_pine(a, seed, height=3.4, radius=1.0, tiers=4):
    rng = random.Random(seed)
    trunk_h = height * 0.22
    a.add(p_cyl(0.13 * radius, 0.08 * radius, trunk_h + 0.15, 6, loc=(0, 0, -0.12)), "bark")
    z = trunk_h * 0.7
    tier_h = (height - z) / (1 + (tiers - 1) * 0.5) * 1.02
    cols = ["pine_dark", "pine_mid", "pine_mid", "pine_light", "pine_light"]
    for i in range(tiers):
        f = i / max(1, tiers - 1)
        r = radius * (1.0 - 0.6 * f) * rng.uniform(0.92, 1.08)
        h = tier_h * (1.0 - 0.18 * f)
        b = bmesh.new()
        bmesh.ops.create_cone(b, cap_ends=True, cap_tris=False, segments=7, radius1=r, radius2=0, depth=h)
        bmesh.ops.translate(b, vec=(0, 0, h / 2), verts=b.verts)
        for v in b.verts:
            if v.co.z < 1e-4:
                s = rng.uniform(0.86, 1.12)
                v.co.x *= s; v.co.y *= s; v.co.z += rng.uniform(-0.07, 0.05)
        _place(b, None, None, TM((rng.uniform(-.03, .03), rng.uniform(-.03, .03), z),
                                 (rng.uniform(-4, 4), rng.uniform(-4, 4), rng.uniform(0, 360))))
        col = cols[min(i, len(cols) - 1)]
        a.add(b, lambda fc, c=col: "pine_dark" if fc.normal.z < -0.5 else c)
        z += h * 0.5

def build_round_tree(a, seed, height=2.9, spread=1.0):
    rng = random.Random(seed)
    a.add(p_cyl(0.15, 0.09, height * 0.62, 6, loc=(0, 0, -0.12)), "bark")
    a.add(p_cyl(0.06, 0.035, 0.6, 5, loc=(0.02, 0, height * 0.3), rot=(0, 38, 20)), "bark")
    c = Vector((0, 0, height * 0.66))
    blobs = [((0, 0, 0.05), 0.72, "leaf_mid")]
    for k in range(4):
        ang = k * 90 + rng.uniform(-25, 25)
        d = rng.uniform(0.42, 0.58) * spread
        blobs.append(((math.cos(math.radians(ang)) * d, math.sin(math.radians(ang)) * d, rng.uniform(-0.25, 0.1)),
                      rng.uniform(0.45, 0.58), rng.choice(["leaf_dark", "leaf_mid"])))
    blobs.append(((rng.uniform(-.15, .15), rng.uniform(-.15, .15), 0.5), 0.5, "leaf_light"))
    for off, r, col in blobs:
        a.add(p_ico(r * spread, 2, loc=c + Vector(off), jitter=0.07, rng=rng, scl=(1, 1, 0.92)), col, smooth=True)

def build_bush(a, seed, size=1.0, berries=False):
    rng = random.Random(seed)
    parts = [((0, 0, 0.18), 0.34, "bush"), ((0.3, 0.08, 0.12), 0.26, "leaf_mid"), ((-0.26, -0.05, 0.1), 0.24, "leaf_dark")]
    for off, r, col in parts:
        a.add(p_ico(r * size, 2, loc=Vector(off) * size, jitter=0.08, rng=rng, scl=(1, 1, 0.85)), col, smooth=True)
    if berries:
        for k in range(9):
            d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.1, 0.9))).normalized()
            base, r = parts[k % 3][0], parts[k % 3][1]
            p = (Vector(base) + Vector((d.x, d.y, d.z * 0.85)) * r * 0.97) * size
            a.add(p_ico(0.035 * size, 1, loc=p), "berry")

def build_rock(a, seed, size=0.3, flat=0.6, n=1):
    rng = random.Random(seed)
    for k in range(n):
        s = size * (1.0 if k == 0 else rng.uniform(0.45, 0.65))
        off = (0, 0, 0) if k == 0 else (rng.uniform(-1, 1) * size * 0.9, rng.uniform(-1, 1) * size * 0.9, 0)
        a.add(p_ico(s, 1 if size < 0.4 else 2, loc=(off[0], off[1], s * flat * 0.35), jitter=0.22, rng=rng,
                    scl=(1, rng.uniform(0.8, 1.0), flat), rot=(0, 0, rng.uniform(0, 360)), cut=-s * flat * 0.4),
              by_normal("stone_light", "rock", "rock_dark", 0.7))

def build_grass(a, seed, n=6, h=0.22, col="blade"):
    rng = random.Random(seed)
    for k in range(n):
        ang = rng.uniform(0, math.tau); d = rng.uniform(0, 0.07)
        b = bmesh.new()
        hh = h * rng.uniform(0.6, 1.1)
        bmesh.ops.create_cone(b, cap_ends=False, cap_tris=False, segments=3, radius1=0.018, radius2=0.0, depth=hh)
        bmesh.ops.translate(b, vec=(0, 0, hh / 2), verts=b.verts)
        lean = Vector((math.cos(ang), math.sin(ang), 0)) * rng.uniform(0.03, 0.09)
        for v in b.verts:
            if v.co.z > hh * 0.99:
                v.co += lean
        _place(b, None, None, TM((math.cos(ang) * d, math.sin(ang) * d, -0.02), (0, 0, rng.uniform(0, 360))))
        a.add(b, col if k % 3 else "blade_dark")

def build_flowers(a, seed):
    rng = random.Random(seed)
    for k in range(4):
        x, y = rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1)
        h = rng.uniform(0.14, 0.24)
        a.add(p_cyl(0.008, 0.006, h, 3, loc=(x, y, -0.02)), "blade")
        a.add(p_ico(0.03, 1, loc=(x, y, h - 0.01), scl=(1, 1, 0.7)), rng.choice(["flower_w", "flower_y", "flower_p"]))
    build_grass(a, seed + 1, n=4, h=0.14)

def build_stump(a):
    a.add(p_cyl(0.26, 0.22, 0.32, 8, loc=(0, 0, -0.05), bevel=0.02), by_normal("wood_pale", "bark"))
    for ang in (20, 140, 260):
        a.add(p_cyl(0.07, 0.03, 0.28, 5, loc=(math.cos(math.radians(ang)) * 0.2, math.sin(math.radians(ang)) * 0.2, -0.03),
                    rot=(0, 70, ang)), "bark")


# ----------------------------------------------------------------------------------------
# buildings
# ----------------------------------------------------------------------------------------
def rock_face_color(seed):
    def f(face):
        if face.normal.z > 0.74:
            return "moss"
        c = face.calc_center_median()
        n = noise.noise(c * 3.1 + Vector((seed, 0, 0)))
        return "stone_light" if n > 0.35 else ("rock_dark" if n < -0.4 else "rock")
    return f

def build_mine(a):
    rng = random.Random(11)
    b = bmesh.new()
    bmesh.ops.create_icosphere(b, subdivisions=2, radius=1.0)
    for v in b.verts:
        v.co *= 1 + 0.15 * noise.noise(v.co * 1.6 + Vector((3, 1, 7))) + rng.uniform(-0.05, 0.05)
    bmesh.ops.scale(b, vec=(1.95, 1.5, 1.95), verts=b.verts)
    bmesh.ops.bisect_plane(b, geom=b.verts[:] + b.edges[:] + b.faces[:], plane_co=(0, 0, -0.08),
                           plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.translate(b, vec=(0, 0.4, 0), verts=b.verts)
    for v in b.verts:                               # flat rock face for the portal
        if v.co.y < -0.76 and abs(v.co.x) < 1.2:
            v.co.y = -0.76 + rng.uniform(0, 0.05)
    b.normal_update()
    a.add(b, rock_face_color(1.0))
    # a couple of boulders at the foot
    a.add(p_ico(0.32, 1, loc=(-1.45, -0.55, 0.12), jitter=0.2, rng=rng, scl=(1, 0.9, 0.75)), rock_face_color(2.0))
    a.add(p_ico(0.22, 1, loc=(1.35, -0.75, 0.08), jitter=0.2, rng=rng, scl=(1, 0.9, 0.7)), rock_face_color(3.0))
    # dark tunnel
    a.add(p_box((1.08, 0.3, 1.32), loc=(0, -0.73, 0.62)), "black")
    # timber portal
    for sx in (-1, 1):
        a.add(p_box((0.17, 0.17, 1.45), loc=(sx * 0.63, -0.93, 0.66), bevel=0.02), "wood_mid")
        a.add(p_box((0.42, 0.09, 0.09), loc=(sx * 0.44, -0.95, 1.2), rot=(0, sx * 45, 0), bevel=0.012), "wood_dark")
    a.add(p_box((1.75, 0.22, 0.2), loc=(0, -0.94, 1.45), bevel=0.02), "wood_yellow")
    a.add(p_box((1.95, 0.75, 0.07), loc=(0, -0.92, 1.64), rot=(14, 0, 0), bevel=0.012), "wood_light")
    for x in (-0.8, -0.4, 0.0, 0.4, 0.8):
        a.add(p_box((0.035, 0.7, 0.02), loc=(x, -0.92, 1.685), rot=(14, 0, 0)), "wood_mid")
    # rails coming out
    for sx in (-1, 1):
        a.add(p_box((0.05, 1.5, 0.06), loc=(sx * 0.25, -1.4, 0.075)), by_normal("iron_light", "rail"))
    for y in (-0.8, -1.15, -1.5, -1.85):
        a.add(p_box((0.72, 0.13, 0.05), loc=(0, y, 0.025), bevel=0.01), "wood_dark")
    # lantern on the right post
    a.add(p_box((0.26, 0.04, 0.04), loc=(0.8, -1.0, 1.2)), "iron_dark")
    a.add(p_box((0.11, 0.11, 0.15), loc=(0.9, -1.0, 1.04)), "lantern_glow")
    a.add(p_cyl(0.1, 0.0, 0.08, 4, loc=(0.9, -1.0, 1.115), spin=45), "iron_dark")
    a.add(p_box((0.14, 0.14, 0.025), loc=(0.9, -1.0, 0.955)), "iron_dark")

def build_house(a):
    W, D, H, F = 3.0, 2.6, 1.75, 0.22
    zt = F + H
    a.add(p_box((W + 0.26, D + 0.26, F + 0.15), loc=(0, 0, (F - 0.15) / 2), bevel=0.03), "stone_mid")
    a.add(p_box((W - 0.12, D - 0.12, H), loc=(0, 0, F + H / 2)), "wood_dark")
    nb = 6; bh = H / nb
    for k in range(nb):
        z = F + bh * (k + 0.5)
        col = "wood_mid" if k % 2 == 0 else "wood_mid2"
        for sy in (-1, 1):
            a.add(p_box((W, 0.1, bh), loc=(0, sy * (D / 2 - 0.05), z), bevel=0.018), col)
        for sx in (-1, 1):
            a.add(p_box((0.1, D - 0.2, bh), loc=(sx * (W / 2 - 0.05), 0, z), bevel=0.018), col)
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.2, 0.2, H + 0.06), loc=(sx * W / 2, sy * D / 2, F + H / 2), bevel=0.02), "wood_dark")
    for sy in (-1, 1):
        a.add(p_box((W + 0.12, 0.16, 0.14), loc=(0, sy * D / 2, zt), bevel=0.02), "wood_dark")
    for sx in (-1, 1):
        a.add(p_box((0.16, D + 0.12, 0.14), loc=(sx * W / 2, 0, zt), bevel=0.02), "wood_dark")
    # roof geometry
    pitch = math.radians(38)
    ox, oy = 0.35, 0.3
    zr = zt + 0.08 + (W / 2) * math.tan(pitch)
    L = (W / 2 + ox) / math.cos(pitch)
    Ly = D + 2 * oy
    # gables
    for sy in (-1, 1):
        a.add(p_prism([(-W / 2, zt + 0.07), (W / 2, zt + 0.07), (0, zr - 0.02)], 0.12, loc=(0, sy * (D / 2 - 0.06), 0)),
              "wood_light")
        for x in (-0.75, 0.0, 0.75):
            hh = (zr - zt) * (1 - abs(x) / (W / 2)) - 0.1
            a.add(p_box((0.05, 0.04, hh), loc=(x, sy * (D / 2 - 0.0), zt + 0.07 + hh / 2)), "wood_mid")
    a.add(p_cyl(0.17, 0.17, 0.05, 8, loc=(0, -D / 2 - 0.02, zt + 0.52), rot=(90, 0, 0)), "wood_dark")
    a.add(p_cyl(0.12, 0.12, 0.05, 8, loc=(0, -D / 2 - 0.04, zt + 0.52), rot=(90, 0, 0)), "black")
    rng = random.Random(5)
    R_ = 5
    for sx in (-1, 1):
        nrm = Vector((sx * math.sin(pitch), 0, math.cos(pitch)))
        down = Vector((sx * math.cos(pitch), 0, -math.sin(pitch)))
        ridge = Vector((0, 0, zr))
        # underlay
        c = ridge + down * (L / 2) - nrm * 0.03
        a.add(p_box((L, Ly, 0.06), loc=c, rot=(0, sx * math.degrees(pitch), 0)), "wood_dark")
        # tile rows (shingled: each row slightly shallower so the lower edge lifts)
        rl = L / R_
        for k in range(R_):
            s = (k + 0.5) * rl
            cc = ridge + down * s + nrm * 0.05
            n = 8; tw = Ly / n
            shift = tw / 2 if k % 2 else 0.0
            i = 0
            y0 = -Ly / 2 - shift
            while y0 < Ly / 2 - 1e-4:
                ya, yb = max(y0, -Ly / 2), min(y0 + tw, Ly / 2)
                if yb - ya > 0.05:
                    col = "roof" if (i + k) % 2 == 0 else "roof_dark"
                    if rng.random() < 0.12:
                        col = "roof_light"
                    a.add(p_box((rl + 0.07, yb - ya - 0.014, 0.05), loc=(cc.x, (ya + yb) / 2, cc.z),
                                rot=(0, sx * (math.degrees(pitch) - 6), 0), bevel=0.012), col)
                y0 += tw; i += 1
        # barge boards
        for sy in (-1, 1):
            cb = ridge + down * (L / 2) + nrm * 0.03
            a.add(p_box((L + 0.06, 0.06, 0.15), loc=(cb.x, sy * (Ly / 2 + 0.03), cb.z),
                        rot=(0, sx * math.degrees(pitch), 0), bevel=0.01), "wood_dark")
    a.add(p_box((0.24, Ly + 0.1, 0.24), loc=(0, 0, zr + 0.07), rot=(0, 45, 0), bevel=0.02), "roof_dark")
    # chimney
    a.add(p_box((0.42, 0.42, 1.5), loc=(-0.78, 0.5, zt + 0.55), bevel=0.02), "stone_mid")
    a.add(p_box((0.52, 0.52, 0.1), loc=(-0.78, 0.5, zt + 1.33), bevel=0.02), "stone_dark")
    a.add(p_box((0.3, 0.3, 0.02), loc=(-0.78, 0.5, zt + 1.385)), "black")
    # double door + frame + step
    yd = -D / 2
    for sx in (-1, 1):
        a.add(p_box((0.11, 0.13, 1.42), loc=(sx * 0.64, yd - 0.03, F + 0.71), bevel=0.012), "wood_dark")
        a.add(p_box((0.57, 0.06, 1.34), loc=(sx * 0.295, yd - 0.02, F + 0.67), bevel=0.01), "wood_light")
        for z in (F + 0.2, F + 1.15):
            a.add(p_box((0.5, 0.04, 0.09), loc=(sx * 0.295, yd - 0.06, z)), "wood_dark")
        a.add(p_box((0.08, 0.04, 1.04), loc=(sx * 0.295, yd - 0.065, F + 0.675), rot=(0, sx * 26, 0)), "wood_dark")
        a.add(p_box((0.05, 0.04, 0.12), loc=(sx * 0.08, yd - 0.08, F + 0.7)), "iron_dark")
    a.add(p_box((1.4, 0.13, 0.13), loc=(0, yd - 0.03, F + 1.42), bevel=0.012), "wood_dark")
    a.add(p_box((1.5, 0.45, 0.16), loc=(0, yd - 0.3, 0.03), bevel=0.02), "stone_light")
    # side windows with open shutters
    for sx in (-1, 1):
        xw = sx * (W / 2 + 0.02)
        a.add(p_box((0.1, 0.64, 0.56), loc=(xw, 0.25, F + 1.05), bevel=0.012), "wood_dark")
        a.add(p_box((0.11, 0.5, 0.43), loc=(xw + sx * 0.005, 0.25, F + 1.05)), "glass")
        a.add(p_box((0.13, 0.04, 0.43), loc=(xw + sx * 0.01, 0.25, F + 1.05)), "wood_dark")
        a.add(p_box((0.13, 0.5, 0.04), loc=(xw + sx * 0.01, 0.25, F + 1.05)), "wood_dark")
        for sy in (-1, 1):
            a.add(p_box((0.05, 0.27, 0.52), loc=(xw + sx * 0.03, 0.25 + sy * 0.47, F + 1.05), bevel=0.01), "roof_dark")
        a.add(p_box((0.18, 0.72, 0.06), loc=(xw + sx * 0.05, 0.25, F + 0.76), bevel=0.01), "wood_light")

def build_market(a):
    W, D = 3.0, 2.0
    a.add(p_box((W, D, 0.22), loc=(0, 0, 0.06), bevel=0.02), "wood_dark")
    n = 6; pw = D / n
    for i in range(n):
        a.add(p_box((W + 0.05, pw - 0.02, 0.06), loc=(0, -D / 2 + pw * (i + 0.5), 0.2), bevel=0.012),
              "wood_light" if i % 2 else "wood_mid2")
    cz, ch, cy = 0.23, 0.82, -0.62
    a.add(p_box((W - 0.12, 0.44, ch - 0.02), loc=(0, cy + 0.26, cz + ch / 2)), "wood_dark")
    nb = 10; bw = (W - 0.1) / nb
    for i in range(nb):
        a.add(p_box((bw - 0.016, 0.08, ch), loc=(-W / 2 + 0.05 + bw * (i + 0.5), cy, cz + ch / 2), bevel=0.012),
              "wood_mid" if i % 2 else "wood_mid2")
    for sx in (-1, 1):
        a.add(p_box((0.08, 0.5, ch), loc=(sx * (W / 2 - 0.06), cy + 0.25, cz + ch / 2), bevel=0.012), "wood_mid")
    a.add(p_box((W + 0.1, 0.64, 0.07), loc=(0, cy + 0.2, cz + ch + 0.035), bevel=0.015), "wood_light")
    a.add(p_box((W + 0.04, 0.1, 0.1), loc=(0, cy - 0.02, cz + 0.08), bevel=0.012), "wood_dark")
    # posts
    for sx in (-1, 1):
        a.add(p_box((0.13, 0.13, 1.95), loc=(sx * W / 2, -0.95, 0.17 + 0.975), bevel=0.015), "wood_dark")
        a.add(p_box((0.13, 0.13, 2.38), loc=(sx * W / 2, 0.9, 0.17 + 1.19), bevel=0.015), "wood_dark")
    # back shelf
    a.add(p_box((W - 0.2, 0.34, 0.05), loc=(0, 0.74, 1.05), bevel=0.01), "wood_mid")
    a.add(p_box((W - 0.2, 0.34, 0.05), loc=(0, 0.74, 0.55), bevel=0.01), "wood_mid")
    # beams
    a.add(p_box((W + 0.12, 0.11, 0.11), loc=(0, -0.95, 2.06), bevel=0.012), "wood_dark")
    a.add(p_box((W + 0.12, 0.11, 0.11), loc=(0, 0.9, 2.5), bevel=0.012), "wood_dark")
    y_back, z_back, y_front, z_front = 1.02, 2.62, -1.32, 2.02
    ang = math.degrees(math.atan2(z_back - z_front, y_back - y_front))
    L = math.hypot(z_back - z_front, y_back - y_front)
    for sx in (-1, 1):
        a.add(p_box((0.08, 1.95, 0.08), loc=(sx * W / 2, -0.02, 2.26), rot=(ang, 0, 0)), "wood_dark")
    # striped awning + flaps
    Wa, ns = W + 0.4, 8
    sw = Wa / ns
    cyy, czz = (y_back + y_front) / 2, (z_back + z_front) / 2 + 0.06
    for i in range(ns):
        x = -Wa / 2 + sw * (i + 0.5)
        col = "stripe_red" if i % 2 == 0 else "cream"
        a.add(p_box((sw + 0.002, L, 0.05), loc=(x, cyy, czz), rot=(ang, 0, 0)), col)
        a.add(p_prism([(-sw / 2, 0.0), (sw / 2, 0.0), (sw / 2, -0.14), (0, -0.23), (-sw / 2, -0.14)], 0.03,
                      loc=(x, y_front - 0.01, z_front + 0.05)), col)
    # hanging sign with a coin
    a.add(p_box((0.5, 0.05, 0.05), loc=(-W / 2 - 0.22, -0.95, 1.85)), "wood_dark")
    for x in (-W / 2 - 0.16, -W / 2 - 0.42):
        a.add(p_box((0.015, 0.015, 0.14), loc=(x, -0.95, 1.76)), "rope")
    a.add(p_box((0.42, 0.05, 0.3), loc=(-W / 2 - 0.29, -0.95, 1.55), bevel=0.015), "wood_light")
    a.add(p_cyl(0.09, 0.09, 0.02, 12, loc=(-W / 2 - 0.29, -0.975, 1.55), rot=(90, 0, 0), bevel=0.005), "gold")

def build_smelter(a):
    rng = random.Random(21)
    a.add(p_cyl(0.98, 0.95, 0.3, 8, loc=(0, 0, -0.1), bevel=0.03, spin=22.5), "stone_dark")
    z = 0.2
    rings = [(0.84, 0.8, 0.38, "stone_mid"), (0.8, 0.74, 0.36, "stone_light"), (0.74, 0.64, 0.34, "stone_mid")]
    for i, (r1, r2, h, col) in enumerate(rings):
        a.add(p_cyl(r1, r2, h, 8, loc=(0, 0, z), bevel=0.04, spin=22.5 + rng.uniform(-4, 4)), col)
        z += h
    a.add(p_cyl(0.64, 0.3, 0.48, 8, loc=(0, 0, z), bevel=0.03, spin=22.5), "stone_dark")
    z += 0.46
    a.add(p_cyl(0.24, 0.22, 0.9, 8, loc=(0, 0, z - 0.05), bevel=0.02, spin=22.5), "stone_mid")
    a.add(p_cyl(0.31, 0.31, 0.11, 8, loc=(0, 0, z + 0.82), bevel=0.02, spin=22.5), "stone_light")
    a.add(p_cyl(0.19, 0.19, 0.02, 8, loc=(0, 0, z + 0.925), spin=22.5), "black")
    # fire mouth
    a.add(p_box((0.64, 0.3, 0.52), loc=(0, -0.72, 0.56)), "black")
    a.add(p_box((0.46, 0.1, 0.3), loc=(0, -0.84, 0.51)), "glow")
    a.add(p_box((0.34, 0.08, 0.07), loc=(0, -0.88, 0.39)), "glow_hot")
    a.add(p_box((0.84, 0.22, 0.15), loc=(0, -0.86, 0.85), bevel=0.02), "stone_light")
    for sx in (-1, 1):
        a.add(p_box((0.15, 0.22, 0.52), loc=(sx * 0.38, -0.86, 0.54), bevel=0.02), "stone_light")
    a.add(p_box((0.95, 0.38, 0.13), loc=(0, -0.97, 0.26), bevel=0.02), "stone_dark")
    # ingot mould with molten metal
    a.add(p_box((0.44, 0.22, 0.06), loc=(0, -1.02, 0.35), bevel=0.01), "iron_dark")
    a.add(p_box((0.36, 0.15, 0.01), loc=(0, -1.02, 0.382)), "glow_hot")
    # coal heap on the side
    for k in range(7):
        a.add(p_ico(rng.uniform(0.07, 0.11), 1, loc=(0.85 + rng.uniform(-.14, .14), -0.45 + rng.uniform(-.14, .14),
                                                   0.05 + k * 0.012), jitter=0.2, rng=rng), "coal")


# ----------------------------------------------------------------------------------------
# props
# ----------------------------------------------------------------------------------------
def build_crate(a, s=0.5, open_top=False, loc=(0, 0, 0)):
    o = Vector(loc)
    t = 0.06
    if not open_top:
        a.add(p_box((s - 0.04, s - 0.04, s - 0.04), loc=o + Vector((0, 0, s / 2))), "wood_light")
    else:
        a.add(p_box((s - 0.06, s - 0.06, 0.03), loc=o + Vector((0, 0, 0.05))), "wood_mid")
        for sx in (-1, 1):
            a.add(p_box((0.03, s - 0.04, s - 0.06), loc=o + Vector((sx * (s / 2 - 0.035), 0, s / 2))), "wood_light")
            a.add(p_box((s - 0.04, 0.03, s - 0.06), loc=o + Vector((0, sx * (s / 2 - 0.035), s / 2))), "wood_light")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((t, t, s), loc=o + Vector((sx * (s / 2 - t / 2), sy * (s / 2 - t / 2), s / 2)), bevel=0.01), "wood_mid")
    for z in (t / 2, s - t / 2):
        for sy in (-1, 1):
            a.add(p_box((s - 2 * t, t, t), loc=o + Vector((0, sy * (s / 2 - t / 2), z)), bevel=0.008), "wood_mid")
            a.add(p_box((t, s - 2 * t, t), loc=o + Vector((sy * (s / 2 - t / 2), 0, z)), bevel=0.008), "wood_mid")
    dl = math.sqrt(2) * (s - 2 * t)
    for sy in (-1, 1):
        a.add(p_box((dl, 0.03, 0.045), loc=o + Vector((0, sy * (s / 2 - 0.012), s / 2)), rot=(0, 45 * sy, 0)), "wood_mid")

def build_barrel(a, loc=(0, 0, 0)):
    o = Vector(loc)
    a.add(p_cyl(0.23, 0.27, 0.35, 12, loc=o), "wood_mid", smooth=False)
    a.add(p_cyl(0.27, 0.23, 0.35, 12, loc=o + Vector((0, 0, 0.35))), by_normal("wood_light", "wood_mid"))
    for z in (0.1, 0.54):
        a.add(p_cyl(0.262, 0.262, 0.045, 12, loc=o + Vector((0, 0, z))), "iron_dark")
    a.add(p_cyl(0.2, 0.2, 0.02, 12, loc=o + Vector((0, 0, 0.695))), "wood_pale")

def build_minecart(a, filled=False):
    a.add(p_taper_box((0.55, 0.78), (0.66, 0.9), 0.42, loc=(0, 0, 0.15), bevel=0.02),
          lambda f: ("black" if not filled else "ore_rock_dk") if f.normal.z > 0.9 else
          ("iron_dark" if f.normal.z < -0.9 else "wood_mid2"))
    for sx in (-1, 1):
        a.add(p_box((0.05, 0.94, 0.05), loc=(sx * 0.335, 0, 0.575)), "iron_dark")
        a.add(p_box((0.7, 0.05, 0.05), loc=(0, sx * 0.455, 0.575)), "iron_dark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_cyl(0.12, 0.12, 0.06, 8, loc=(sx * 0.22, sy * 0.24, 0.12), rot=(0, 90, 0)), "iron_dark")
            a.add(p_cyl(0.045, 0.045, 0.08, 6, loc=(sx * 0.21, sy * 0.24, 0.12), rot=(0, 90, 0)), "iron")
    if filled:
        rng = random.Random(9)
        for k in range(9):
            x, y = rng.uniform(-0.2, 0.2), rng.uniform(-0.3, 0.3)
            ore_chunk(a, "gold", 100 + k, loc=(x, y, 0.5 + (0.08 - abs(x) * 0.2 - abs(y) * 0.1)), size=0.11)

def build_rails(a):
    for sx in (-1, 1):
        a.add(p_box((0.05, 1.0, 0.06), loc=(sx * 0.25, 0, 0.08)), by_normal("iron_light", "rail"))
    for y in (-0.33, 0.0, 0.33):
        a.add(p_box((0.72, 0.13, 0.05), loc=(0, y, 0.025), bevel=0.01), "wood_dark")

def build_sack(a, loc=(0, 0, 0), seed=0):
    rng = random.Random(seed)
    o = Vector(loc)
    a.add(p_ico(0.24, 2, loc=o + Vector((0, 0, 0.17)), scl=(1, 0.85, 1.05), jitter=0.06, rng=rng, cut=-0.16),
          "burlap", smooth=True)
    a.add(p_cyl(0.1, 0.06, 0.1, 8, loc=o + Vector((0, 0, 0.39))), "burlap_dark", smooth=True)
    a.add(p_cyl(0.075, 0.075, 0.035, 8, loc=o + Vector((0, 0, 0.44))), "rope")
    a.add(p_cyl(0.07, 0.11, 0.07, 8, loc=o + Vector((0, 0, 0.47))), "burlap_dark")

def build_fence(a):
    for x in (-0.44, 0.44):
        a.add(p_box((0.1, 0.1, 0.72), loc=(x, 0, 0.3), bevel=0.012), "wood_mid")
        a.add(p_cyl(0.075, 0.0, 0.1, 4, loc=(x, 0, 0.66), spin=45), "wood_mid")
    for z in (0.25, 0.52):
        a.add(p_box((1.0, 0.05, 0.1), loc=(0, -0.055, z), bevel=0.01), "wood_light")

def build_pickaxe(a):
    a.add(p_cyl(0.024, 0.02, 0.78, 6, loc=(-0.39, 0, 0.03), rot=(0, 90, 0)), "wood_light")
    for sy in (-1, 1):
        a.add(p_box((0.05, 0.22, 0.05), loc=(0.37, sy * 0.1, 0.05), rot=(sy * 10, 0, 0)), "iron")
        a.add(p_cyl(0.028, 0.0, 0.08, 4, loc=(0.37, sy * 0.21, 0.07), rot=(-sy * 80, 0, 0), spin=45), "iron_light")
    a.add(p_box((0.08, 0.08, 0.07), loc=(0.37, 0, 0.04)), "iron_dark")

def build_anvil(a):
    a.add(p_cyl(0.24, 0.21, 0.36, 8, loc=(0, 0, -0.03), bevel=0.02), by_normal("wood_pale", "bark"))
    a.add(p_box((0.18, 0.13, 0.08), loc=(0, 0, 0.37)), "iron_dark")
    a.add(p_box((0.34, 0.17, 0.08), loc=(0, 0, 0.44), bevel=0.012), "iron_dark")
    a.add(p_box((0.4, 0.18, 0.06), loc=(0, 0, 0.51), bevel=0.012), by_normal("iron_light", "iron"))
    a.add(p_cyl(0.075, 0.0, 0.22, 6, loc=(0.2, 0, 0.51), rot=(0, 90, 0)), "iron")
    a.add(p_cyl(0.015, 0.015, 0.3, 6, loc=(-0.05, 0.03, 0.56), rot=(0, 90, 25)), "wood_light")
    a.add(p_box((0.06, 0.12, 0.05), loc=(0.25 - 0.05, 0.14, 0.565), rot=(0, 0, 25)), "iron_dark")

def build_lantern_post(a):
    a.add(p_box((0.3, 0.3, 0.16), loc=(0, 0, 0.04), bevel=0.02), "stone_mid")
    a.add(p_box((0.1, 0.1, 1.72), loc=(0, 0, 0.86), bevel=0.015), "wood_dark")
    a.add(p_box((0.5, 0.07, 0.07), loc=(0.2, 0, 1.64), bevel=0.01), "wood_dark")
    a.add(p_box((0.3, 0.05, 0.05), loc=(0.1, 0, 1.5), rot=(0, 45, 0)), "wood_dark")
    a.add(p_box((0.02, 0.02, 0.1), loc=(0.39, 0, 1.56)), "iron_dark")
    a.add(p_cyl(0.11, 0.0, 0.09, 4, loc=(0.39, 0, 1.43), spin=45), "iron_dark")
    a.add(p_box((0.12, 0.12, 0.17), loc=(0.39, 0, 1.345)), "lantern_glow")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.02, 0.02, 0.18), loc=(0.39 + sx * 0.065, sy * 0.065, 1.345)), "iron_dark")
    a.add(p_box((0.16, 0.16, 0.03), loc=(0.39, 0, 1.255)), "iron_dark")


# ----------------------------------------------------------------------------------------
# resources
# ----------------------------------------------------------------------------------------
ORE = {
    "gold": ("ore_rock", "ore_rock_dk", ["ore_gold"]),
    "iron": ("iron_ore_rock", "ore_rock_dk", ["iron_ore_vein"]),
    "copper": ("ore_rock", "ore_rock_dk", ["copper_ore_vein", "malachite"]),
}
METAL = {"gold": ("gold_light", "gold", "gold_dark"), "iron": ("iron_light", "iron", "iron_dark"),
         "copper": ("copper_light", "copper", "copper_dark")}

def ore_chunk(a, kind, seed, loc=(0, 0, 0), size=0.15):
    rng = random.Random(seed)
    rock, dark, veins = ORE[kind]
    sc = Vector((1.0, rng.uniform(0.75, 0.9), rng.uniform(0.62, 0.78)))
    c = Vector(loc) + Vector((0, 0, size * sc.z * 0.8))
    rz = rng.uniform(0, 360)
    a.add(p_ico(size, 1, loc=c, jitter=0.3, rng=rng, scl=sc, rot=(0, 0, rz)),
          lambda f: dark if f.normal.z < -0.35 else rock)
    for k in range(rng.randint(3, 5)):
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.15, 1))).normalized()
        p = c + Vector((d.x * sc.x, d.y * sc.y, d.z * sc.z)) * size * 0.88
        g = size * rng.uniform(0.2, 0.3)
        a.add(p_ico(g, 1, loc=p, scl=(1, 0.8, 1.3), rot=(rng.uniform(0, 60), rng.uniform(0, 60), rng.uniform(0, 360))),
              veins[k % len(veins)])

def ore_pile(a, kind, seed):
    rng = random.Random(seed)
    rock, dark, veins = ORE[kind]
    a.add(p_ico(0.5, 2, loc=(0, 0, 0), scl=(1, 0.9, 0.45), jitter=0.1, rng=rng, cut=0.0),
          lambda f: rock if f.normal.z > 0.2 else dark)
    for k in range(13):
        ang = rng.uniform(0, math.tau); d = rng.uniform(0.0, 0.46)
        x, y = math.cos(ang) * d, math.sin(ang) * d * 0.9
        z = max(0.0, 0.2 * (1 - (d / 0.5) ** 2)) - 0.02
        ore_chunk(a, kind, seed * 31 + k, loc=(x, y, z), size=rng.uniform(0.1, 0.16))

def ingot(a, metal, loc=(0, 0, 0), rz=0.0, size=1.0):
    light, base, dark = METAL[metal]
    b = bmesh.new()
    bmesh.ops.create_cube(b, size=1.0)
    for v in b.verts:
        if v.co.z > 0:
            v.co.x *= 0.8; v.co.y *= 0.68
    bmesh.ops.scale(b, vec=Vector((0.3, 0.13, 0.075)) * size, verts=b.verts)
    _bevel(b, 0.008 * size)
    _place(b, None, None, TM(Vector(loc) + Vector((0, 0, 0.0375 * size)), (0, 0, rz)))
    a.add(b, by_normal(light, base, dark, 0.8))

def ingot_stack(a, metal):
    z = 0.0
    for layer, count in enumerate((4, 3, 2, 1)):
        for i in range(count):
            x = (i - (count - 1) / 2) * 0.14
            ingot(a, metal, loc=(x, 0, z), rz=90)
        z += 0.074

def coin(a, loc=(0, 0, 0), mat=None, hi=False):
    if mat is None:
        mat = Matrix.Translation(Vector(loc))
    a.add(p_cyl(0.06, 0.06, 0.014, 12 if hi else 9, bevel=0.004 if hi else 0.0, mat=mat),
          by_normal("gold_light", "gold", "gold_dark"))

def coin_stack(a, seed):
    rng = random.Random(seed)
    for (x, y), n in (((0, 0), 9), ((0.13, 0.03), 6), ((0.05, 0.12), 4)):
        for k in range(n):
            coin(a, mat=TM((x + rng.uniform(-.005, .005), y + rng.uniform(-.005, .005), k * 0.0145), (0, 0, rng.uniform(0, 360))))
    coin(a, mat=TM((-0.1, 0.06, 0.004), (8, 0, 30)))

def coin_pile(a, seed):
    rng = random.Random(seed)
    rx, ry, rz = 0.36, 0.32, 0.17
    a.add(p_ico(1.0, 2, scl=(rx, ry, rz), jitter=0.03, rng=rng, cut=0.0), "gold_dark")
    for k in range(34):
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.05, 1))).normalized()
        p = Vector((d.x * rx, d.y * ry, d.z * rz))
        nrm = Vector((d.x / rx, d.y / ry, d.z / rz)).normalized()
        q = Vector((0, 0, 1)).rotation_difference(nrm)
        m = Matrix.Translation(p) @ q.to_matrix().to_4x4() @ Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z")
        coin(a, mat=m)
    for k in range(6):
        ang = rng.uniform(0, math.tau); d = rng.uniform(0.42, 0.55)
        coin(a, mat=TM((math.cos(ang) * d, math.sin(ang) * d, 0.0), (rng.uniform(-6, 6), 0, rng.uniform(0, 360))))

def log(a, length=0.8, r=0.1, mat=None):
    b = bmesh.new()
    bmesh.ops.create_cone(b, cap_ends=True, cap_tris=False, segments=10, radius1=r, radius2=r * 0.94, depth=length)
    bmesh.ops.rotate(b, matrix=Matrix.Rotation(math.radians(90), 3, "Y"), verts=b.verts, cent=(0, 0, 0))
    _place(b, None, None, mat)
    axis = (mat.to_3x3() @ Vector((1, 0, 0))).normalized()
    is_cap = lambda f: abs(f.normal.dot(axis)) > 0.9
    a.add(b, lambda f: "wood_pale" if is_cap(f) else "bark", smooth=lambda f: not is_cap(f))
    for s in (-1, 1):
        rr = r * (0.47 if s > 0 else 0.5)
        a.add(p_cyl(rr, rr, 0.008, 8, mat=mat @ TM((s * (length / 2 - 0.002), 0, 0), (0, 90 * s, 0))), "wood_light")

def log_pile(a, seed):
    rng = random.Random(seed)
    r = 0.11
    rows = [(-0.22, 0.0), (0.0, 0.0), (0.22, 0.0), (-0.11, 0.19), (0.11, 0.19), (0.0, 0.38)]
    for y, zz in rows:
        L = rng.uniform(0.8, 0.95)
        log(a, L, r * rng.uniform(0.92, 1.05), mat=TM((rng.uniform(-0.05, 0.05), y, r + zz), (rng.uniform(0, 360), 0, rng.uniform(-4, 4))))

def planks(a, seed):
    rng = random.Random(seed)
    for x in (-0.3, 0.3):
        a.add(p_box((0.1, 0.5, 0.06), loc=(x, 0, 0.03)), "wood_dark")
    z = 0.06
    for layer in range(3):
        for y in (-0.1, 0.1):
            a.add(p_box((1.0, 0.18, 0.04), loc=(rng.uniform(-0.03, 0.03), y + rng.uniform(-0.01, 0.01), z + 0.02),
                        rot=(0, 0, rng.uniform(-3, 3)), bevel=0.006), rng.choice(["wood_light", "wood_pale", "wood_light"]))
        z += 0.042

def stone_block(a, loc=(0, 0, 0), rz=0.0):
    a.add(p_box((0.34, 0.22, 0.2), loc=Vector(loc) + Vector((0, 0, 0.1)), rot=(0, 0, rz), bevel=0.025),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))

def stone_pile(a, seed):
    rng = random.Random(seed)
    for x, y in ((-0.18, -0.12), (0.18, -0.12), (-0.18, 0.13), (0.18, 0.13)):
        stone_block(a, (x, y, 0), rng.uniform(-5, 5))
    stone_block(a, (0.0, 0.0, 0.2), 90 + rng.uniform(-8, 8))
    a.add(p_ico(0.12, 1, loc=(0.38, 0.3, 0.06), jitter=0.2, rng=rng, scl=(1, 0.9, 0.7)), "rock")

def crate_ore(a):
    build_crate(a, 0.5, open_top=True)
    rng = random.Random(77)
    for k in range(8):
        ore_chunk(a, "gold", 300 + k, loc=(rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 0.34 + rng.uniform(0, 0.06)), size=0.1)


# ----------------------------------------------------------------------------------------
# kit registry  (category, name, builder)
# ----------------------------------------------------------------------------------------
KIT = [
    ("Buildings", "Bld_Mine", build_mine),
    ("Buildings", "Bld_House", build_house),
    ("Buildings", "Bld_Market", build_market),
    ("Buildings", "Bld_Smelter", build_smelter),

    ("Nature", "Tree_Pine_A", lambda a: build_pine(a, 1, 3.4, 1.0, 4)),
    ("Nature", "Tree_Pine_B", lambda a: build_pine(a, 2, 4.4, 1.05, 5)),
    ("Nature", "Tree_Pine_C", lambda a: build_pine(a, 3, 2.3, 0.8, 3)),
    ("Nature", "Tree_Round_A", lambda a: build_round_tree(a, 4, 2.9, 1.0)),
    ("Nature", "Tree_Round_B", lambda a: build_round_tree(a, 5, 3.5, 1.15)),
    ("Nature", "Bush_A", lambda a: build_bush(a, 6, 1.0)),
    ("Nature", "Bush_B", lambda a: build_bush(a, 7, 1.35)),
    ("Nature", "Bush_Berry", lambda a: build_bush(a, 8, 1.1, berries=True)),
    ("Nature", "Rock_Small", lambda a: build_rock(a, 9, 0.2, 0.6)),
    ("Nature", "Rock_Medium", lambda a: build_rock(a, 10, 0.38, 0.65, n=3)),
    ("Nature", "Rock_Large", lambda a: build_rock(a, 11, 0.75, 0.7, n=2)),
    ("Nature", "Grass_Tuft_A", lambda a: build_grass(a, 12, 6, 0.22)),
    ("Nature", "Grass_Tuft_B", lambda a: build_grass(a, 13, 9, 0.3, "blade_dark")),
    ("Nature", "Flowers_A", lambda a: build_flowers(a, 14)),
    ("Nature", "Tree_Stump", build_stump),

    ("Props", "Prop_Crate", build_crate),
    ("Props", "Prop_Barrel", build_barrel),
    ("Props", "Prop_Sack", build_sack),
    ("Props", "Prop_Minecart", lambda a: build_minecart(a, False)),
    ("Props", "Prop_Minecart_Ore", lambda a: build_minecart(a, True)),
    ("Props", "Prop_Rails", build_rails),
    ("Props", "Prop_Fence", build_fence),
    ("Props", "Prop_Pickaxe", build_pickaxe),
    ("Props", "Prop_Anvil", build_anvil),
    ("Props", "Prop_LanternPost", build_lantern_post),

    ("Resources", "Res_Ore_Gold", lambda a: ore_chunk(a, "gold", 21)),
    ("Resources", "Res_Ore_Iron", lambda a: ore_chunk(a, "iron", 22)),
    ("Resources", "Res_Ore_Copper", lambda a: ore_chunk(a, "copper", 23)),
    ("Resources", "Res_OrePile_Gold", lambda a: ore_pile(a, "gold", 24)),
    ("Resources", "Res_OrePile_Iron", lambda a: ore_pile(a, "iron", 25)),
    ("Resources", "Res_OrePile_Copper", lambda a: ore_pile(a, "copper", 26)),
    ("Resources", "Res_Ingot_Gold", lambda a: ingot(a, "gold")),
    ("Resources", "Res_Ingot_Iron", lambda a: ingot(a, "iron")),
    ("Resources", "Res_Ingot_Copper", lambda a: ingot(a, "copper")),
    ("Resources", "Res_IngotStack_Gold", lambda a: ingot_stack(a, "gold")),
    ("Resources", "Res_IngotStack_Iron", lambda a: ingot_stack(a, "iron")),
    ("Resources", "Res_IngotStack_Copper", lambda a: ingot_stack(a, "copper")),
    ("Resources", "Res_Coin", lambda a: coin(a, hi=True)),
    ("Resources", "Res_CoinStack", lambda a: coin_stack(a, 31)),
    ("Resources", "Res_CoinPile", lambda a: coin_pile(a, 32)),
    ("Resources", "Res_Crate_Ore", crate_ore),
    ("Resources", "Res_Stone", lambda a: stone_block(a)),
    ("Resources", "Res_StonePile", lambda a: stone_pile(a, 35)),
    ("Resources", "Res_Log", lambda a: log(a, 0.8, 0.1, mat=TM((0, 0, 0.1)))),
    ("Resources", "Res_LogPile", lambda a: log_pile(a, 33)),
    ("Resources", "Res_Planks", lambda a: planks(a, 34)),
]


# ----------------------------------------------------------------------------------------
# scene layout (recreates the screenshot, plus a smelter between the mine and the storehouse)
# ----------------------------------------------------------------------------------------
BUILDINGS = [
    ("Bld_Mine", -3.0, 3.2, 0), ("Bld_House", 4.8, 3.35, 0), ("Bld_Market", 4.6, -2.15, 0), ("Bld_Smelter", 0.9, 4.25, 0),
]
FOOTPRINTS = [(-5.1, 0.9, -0.9, 5.4), (3.0, 1.5, 6.6, 5.1), (2.7, -3.5, 6.5, -0.8), (-0.3, 2.9, 2.1, 5.6)]
PROPS = [
    # (asset, x, y, rot_z, z_override)
    ("Prop_Minecart_Ore", -3.0, 1.62, 0, None),
    ("Res_OrePile_Gold", -4.75, 2.05, 0, None), ("Res_Ore_Gold", -5.6, 2.45, 110, None), ("Res_Ore_Gold", -4.15, 2.3, 40, None),
    ("Res_Crate_Ore", -1.45, 2.45, 12, None), ("Prop_Crate", -0.85, 2.38, -8, None), ("Prop_Pickaxe", -5.3, 0.85, 25, None),
    ("Res_IngotStack_Gold", -0.15, 3.35, 15, None), ("Res_IngotStack_Iron", 1.95, 3.35, -20, None),
    ("Prop_Anvil", 2.3, 4.5, 30, None), ("Res_LogPile", -0.3, 5.15, 80, None), ("Res_Ingot_Gold", 0.35, 3.0, 60, None),
    ("Prop_Barrel", 2.9, 3.0, 0, None), ("Prop_Barrel", 2.8, 3.58, 40, None), ("Prop_Sack", 2.95, 4.15, 20, None),
    ("Prop_Crate", 6.75, 2.3, 15, None), ("Prop_Crate", 6.8, 2.86, -5, None), ("Res_Planks", 6.9, 4.2, 88, None),
    ("Res_CoinStack", 3.75, -2.6, 0, 1.12), ("Res_Ingot_Gold", 4.35, -2.62, 10, 1.12), ("Res_Ingot_Gold", 4.42, -2.55, -5, 1.195),
    ("Res_Ore_Gold", 5.0, -2.62, 30, 1.12), ("Res_Ingot_Copper", 5.55, -2.6, -15, 1.12),
    ("Res_CoinPile", 6.6, -3.3, 0, None), ("Prop_Sack", 6.55, -1.35, -30, None), ("Prop_Barrel", 2.75, -1.35, 0, None),
    ("Prop_Crate", 2.85, -0.8, 20, None),
    ("Res_StonePile", -5.6, -0.55, 10, None), ("Res_Planks", -4.4, -0.35, -35, None), ("Res_LogPile", -6.35, 0.25, 70, None),
    ("Prop_LanternPost", 1.05, 0.9, 180, None), ("Prop_LanternPost", 2.25, -2.5, 0, None),
    ("Prop_Fence", -5.0, -4.6, 0, None), ("Prop_Fence", -4.0, -4.6, 0, None), ("Prop_Fence", -3.0, -4.6, 0, None),
    ("Tree_Stump", -6.4, -3.9, 0, None), ("Rock_Small", -2.1, 0.2, 30, None), ("Rock_Small", 2.4, 0.4, 70, None),
]


def unity_trs(obj):
    """Blender world matrix -> Unity position / rotation (x,y,z,w) / scale."""
    C = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))
    Mu = C @ obj.matrix_world @ C.inverted()
    loc, rot, scl = Mu.decompose()
    return [round(v, 4) for v in loc], [round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)], \
           [round(v, 4) for v in scl]

def to_unity_vec(v):
    return [round(-v.x, 5), round(v.z, 5), round(-v.y, 5)]


def main():
    out, do_render = parse_args()
    blend_dir = os.path.join(out, "Blender")
    unity_root = os.path.join(out, "Unity", "Assets", "Vitaria")
    tex_dir = os.path.join(unity_root, "Textures")
    model_dir = os.path.join(unity_root, "Models")
    layout_dir = os.path.join(unity_root, "Layout")
    prev_dir = os.path.join(out, "Previews")
    for d in (blend_dir, tex_dir, model_dir, layout_dir, prev_dir):
        os.makedirs(d, exist_ok=True)

    # clean slate
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for x in list(coll):
            coll.remove(x)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    scn = bpy.context.scene
    scn.unit_settings.system = "METRIC"

    pal, emi = build_palette_images(tex_dir)
    MAT = make_material(pal, emi)

    def new_coll(name, parent=None):
        c = bpy.data.collections.new(name)
        (parent or scn.collection).children.link(c)
        return c

    kit_root = new_coll("Vitaria_Kit")
    scene_root = new_coll("Vitaria_Scene")
    show_coll = new_coll("Showcase")
    light_coll = new_coll("Lighting")
    cats = ["Environment", "Buildings", "Nature", "Props", "Resources"]
    kit_c = {c: new_coll("Kit_" + c, kit_root) for c in cats if c != "Environment"}
    scn_c = {c: new_coll("Scene_" + c, scene_root) for c in cats}

    def finish(a, coll):
        me = bpy.data.meshes.new(a.name)
        a.bm.to_mesh(me)
        a.bm.free()
        me.materials.append(MAT)
        me.validate()
        o = bpy.data.objects.new(a.name, me)
        coll.objects.link(o)
        return o

    # --- kit
    kit_objs, kit_cat = {}, {}
    for cat, name, fn in KIT:
        a = Asset(name)
        fn(a)
        kit_objs[name] = finish(a, kit_c[cat])
        kit_cat[name] = cat
        try:
            kit_objs[name].asset_mark()
            kit_objs[name].asset_data.tags.new(cat)
        except Exception as ex:
            print("asset mark skipped:", ex)
        print("built", name)

    # --- environment (lives only in the scene)
    isl = Asset("Env_Island"); build_island(isl); env_island = finish(isl, scn_c["Environment"])
    pth = Asset("Env_Paths"); build_paths(pth); env_paths = finish(pth, scn_c["Environment"])
    kit_cat["Env_Island"] = kit_cat["Env_Paths"] = "Environment"

    # --- scene instances
    occupied = []

    def free_at(x, y, r):
        for (ox, oy, orr) in occupied:
            if (ox - x) ** 2 + (oy - y) ** 2 < (orr + r) ** 2:
                return False
        return True

    def in_footprint(x, y, m=0.0):
        return any(x0 - m < x < x1 + m and y0 - m < y < y1 + m for x0, y0, x1, y1 in FOOTPRINTS)

    def near_path(x, y, d):
        d2 = d * d
        return any((p.x - x) ** 2 + (p.y - y) ** 2 < d2 for p in PATH_SAMPLES)

    def place(asset, x, y, rz=0.0, s=1.0, z=None, tilt=(0.0, 0.0), r=0.0):
        o = bpy.data.objects.new(asset, kit_objs[asset].data)
        o.location = (x, y, ground_h(x, y) if z is None else z)
        o.rotation_euler = R(tilt[0], tilt[1], rz)
        o.scale = (s, s, s)
        scn_c[kit_cat[asset]].objects.link(o)
        if r:
            occupied.append((x, y, r))
        return o

    for name, x, y, rz in BUILDINGS:
        place(name, x, y, rz, z=0.0)
    for name, x, y, rz, z in PROPS:
        place(name, x, y, rz, z=z, r=0.35)

    rng = random.Random(2024)
    # rim trees
    tries = 0
    while tries < 6000:
        tries += 1
        x, y = rng.uniform(-9.55, 9.55), rng.uniform(-7.05, 7.05)
        e = edge_dist(x, y)
        if e > 2.5:
            continue
        front = y < -4.6
        if front and abs(x) < 7.2:
            continue
        if near_path(x, y, 1.3) or in_footprint(x, y, 0.6):
            continue
        if e < 1.6:
            kind = rng.choices(["pine", "round", "bush"], [0.68, 0.22, 0.10])[0]
        else:
            kind = rng.choices(["pine", "round", "bush", "rock"], [0.3, 0.3, 0.28, 0.12])[0]
        if front:
            kind = rng.choice(["bush", "bush", "round", "rock"])
        rad = {"pine": 0.95, "round": 1.0, "bush": 0.5, "rock": 0.45}[kind]
        if not free_at(x, y, rad):
            continue
        if kind == "pine":
            asset = rng.choices(["Tree_Pine_A", "Tree_Pine_B", "Tree_Pine_C"], [0.45, 0.35, 0.2])[0]
        elif kind == "round":
            asset = rng.choice(["Tree_Round_A", "Tree_Round_B"])
        elif kind == "bush":
            asset = rng.choices(["Bush_A", "Bush_B", "Bush_Berry"], [0.4, 0.4, 0.2])[0]
        else:
            asset = rng.choice(["Rock_Small", "Rock_Medium"] if front else ["Rock_Small", "Rock_Medium", "Rock_Large"])
        place(asset, x, y, rng.uniform(0, 360), s=rng.uniform(0.85, 1.15), z=ground_h(x, y) - 0.02,
              tilt=(rng.uniform(-3, 3), rng.uniform(-3, 3)), r=rad)
    # ground cover
    for count, assets, rr in ((95, ["Grass_Tuft_A", "Grass_Tuft_A", "Grass_Tuft_B"], 0.25), (22, ["Flowers_A"], 0.3),
                              (5, ["Rock_Small"], 0.3)):
        n = 0; t = 0
        while n < count and t < 8000:
            t += 1
            x, y = rng.uniform(-9.3, 9.3), rng.uniform(-6.9, 6.9)
            if near_path(x, y, 0.75) or in_footprint(x, y, 0.2) or not free_at(x, y, rr):
                continue
            place(rng.choice(assets), x, y, rng.uniform(0, 360), s=rng.uniform(0.8, 1.25), r=rr)
            n += 1

    # --- showcase (kit laid out with labels, away from the scene)
    lab_mat = bpy.data.materials.new("Label")
    lab_mat.diffuse_color = hex2rgb("#1d231b") + (1,)
    lab_mat.use_nodes = True
    lab_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hex2rgb("#262b24") + (1,)
    # category: (x0, y0, gap, label size, items per line, line spacing)
    rows = {"Buildings": (60.0, 0.0, 1.2, 0.3, 99, 6.0), "Nature": (60.0, -12.0, 0.8, 0.2, 8, 5.2),
            "Props": (60.0, -25.0, 0.6, 0.11, 5, 2.6), "Resources": (60.0, -33.0, 0.42, 0.075, 6, 1.6)}
    show_members = {}
    show_bounds = {}
    for cat, (x0, y0, gap, lsize, per_row, lsp) in rows.items():
        names = [n for c, n, _ in KIT if c == cat]
        x = x0
        mins, maxs = [], []
        line = 0
        count = 0
        for n in names:
            o = kit_objs[n]
            bb = [Vector(v) for v in o.bound_box]
            w = max(max(v.x for v in bb) - min(v.x for v in bb), len(n) * lsize * 0.62)
            dpt = max(v.y for v in bb) - min(v.y for v in bb)
            if count and count % per_row == 0:
                line += 1; x = x0
            yy = y0 - line * lsp
            o.location = (x + w / 2 - (max(v.x for v in bb) + min(v.x for v in bb)) / 2, yy, 0)
            cu = bpy.data.curves.new(n + "_lbl", "FONT")
            cu.body = n
            cu.size = lsize
            cu.align_x = "CENTER"
            t = bpy.data.objects.new(n + "_lbl", cu)
            t.location = (x + w / 2, yy + min(v.y for v in bb) - lsize * 1.4, 0.005)
            t.data.materials.append(lab_mat)
            show_coll.objects.link(t)
            show_members.setdefault(cat, []).extend([o, t])
            mins.append(Vector((x, yy + min(v.y for v in bb) - lsize * 2.2, 0)))
            maxs.append(Vector((x + w, yy + max(v.y for v in bb), max(v.z for v in bb))))
            x += w + gap
            count += 1
        show_bounds[cat] = (Vector((min(v.x for v in mins), min(v.y for v in mins), 0)),
                            Vector((max(v.x for v in maxs), max(v.y for v in maxs), max(v.z for v in maxs))))
    ground = bpy.data.meshes.new("Showcase_Ground")
    gb = bmesh.new()
    bmesh.ops.create_grid(gb, x_segments=1, y_segments=1, size=60)
    gb.to_mesh(ground); gb.free()
    gmat = bpy.data.materials.new("Showcase_Ground")
    gmat.use_nodes = True
    gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hex2rgb("#aeb294") + (1,)
    gmat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    ground.materials.append(gmat)
    go = bpy.data.objects.new("Showcase_Ground", ground)
    go.location = (85, -20, -0.001)
    show_coll.objects.link(go)

    # --- lighting & camera
    sun_d = bpy.data.lights.new("Sun", "SUN")
    sun_d.energy = 4.2
    sun_d.color = (1.0, 0.95, 0.86)
    sun_d.angle = math.radians(4)
    sun = bpy.data.objects.new("Sun", sun_d)
    sun.rotation_euler = R(50, 0, -35)
    light_coll.objects.link(sun)
    world = bpy.data.worlds.new("World") if not bpy.data.worlds else bpy.data.worlds[0]
    scn.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.52, 0.62, 0.78, 1)
    bg.inputs["Strength"].default_value = 0.62

    cam_d = bpy.data.cameras.new("Camera")
    cam_d.lens = 35
    cam = bpy.data.objects.new("Camera", cam_d)
    cam.location = (0.0, -21.0, 19.0)
    target = Vector((0.0, 0.3, 0.0))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    light_coll.objects.link(cam)
    scn.camera = cam

    # --- save blend
    scn.render.engine = "CYCLES"
    scn.cycles.samples = 64
    scn.view_settings.view_transform = "Standard"
    scn.view_settings.look = "None"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, "vitaria_kit.blend"))

    # --- FBX export
    def select_only(objs):
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]

    fbx_kw = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                  axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                  use_mesh_modifiers=True, use_triangles=True, use_tspace=False, add_leaf_bones=False,
                  bake_anim=False, path_mode="STRIP", embed_textures=False, use_custom_props=False)
    asset_info = []
    for name, o in list(kit_objs.items()) + [("Env_Island", env_island), ("Env_Paths", env_paths)]:
        cat = kit_cat[name]
        d = os.path.join(model_dir, cat)
        os.makedirs(d, exist_ok=True)
        saved = o.location.copy()
        o.location = (0, 0, 0)
        select_only([o])
        bpy.ops.export_scene.fbx(filepath=os.path.join(d, name + ".fbx"), **fbx_kw)
        o.location = saved
        me = o.data
        me.calc_loop_triangles()
        bb = [Vector(v) for v in o.bound_box]
        size = [max(v[i] for v in bb) - min(v[i] for v in bb) for i in range(3)]
        asset_info.append({"asset": name, "category": cat, "tris": len(me.loop_triangles),
                           "size_unity": [round(size[0], 3), round(size[2], 3), round(size[1], 3)]})
    scene_objs = [o for c in scn_c.values() for o in c.objects]
    select_only(scene_objs)
    bpy.ops.export_scene.fbx(filepath=os.path.join(model_dir, "Vitaria_Scene.fbx"), **fbx_kw)

    # --- layout json (Unity coordinates)
    items = []
    for o in scene_objs:
        p, q, s = unity_trs(o)
        items.append({"name": o.name, "asset": o.data.name, "group": kit_cat[o.data.name], "p": p, "r": q, "s": s})
    cam_fwd = cam.matrix_world.to_3x3() @ Vector((0, 0, -1))
    cam_up = cam.matrix_world.to_3x3() @ Vector((0, 1, 0))
    aspect = 16 / 9
    hfov = 2 * math.atan(18.0 / cam_d.lens)
    vfov = math.degrees(2 * math.atan(math.tan(hfov / 2) / aspect))
    sun_fwd = sun.matrix_world.to_3x3() @ Vector((0, 0, -1))
    layout = {
        "objects": items,
        "camera": {"position": to_unity_vec(cam.location), "forward": to_unity_vec(cam_fwd), "up": to_unity_vec(cam_up),
                   "fov": round(vfov, 3)},
        "sun": {"forward": to_unity_vec(sun_fwd), "color": [1.0, 0.95, 0.86], "intensity": 1.35},
        "ambient": {"sky": [0.62, 0.70, 0.80], "equator": [0.50, 0.55, 0.47], "ground": [0.27, 0.24, 0.20]},
        "background": [0.176, 0.216, 0.165],
    }
    with open(os.path.join(layout_dir, "vitaria_layout.json"), "w") as f:
        json.dump(layout, f, indent=1)
    with open(os.path.join(out, "asset_list.json"), "w") as f:
        json.dump(asset_info, f, indent=1)
    print("objects in scene:", len(items), "kit assets:", len(asset_info))

    if do_render:
        render_previews(scn, cam, show_bounds, show_members, prev_dir)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, "vitaria_kit.blend"))


def render_previews(scn, cam, show_bounds, show_members, prev_dir):
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.use_denoising = True
    scn.render.film_transparent = True
    scn.render.resolution_percentage = RES_SCALE
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGBA"
    scn.cycles.max_bounces = 4

    def shot(path, w, h, samples):
        scn.render.resolution_x, scn.render.resolution_y = w, h
        scn.cycles.samples = max(8, int(samples * SAMPLES_SCALE))
        scn.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("rendered", path)

    shot(os.path.join(prev_dir, "scene_hero.png"), 1920, 1080, 48)
    if HERO_ONLY:
        composite_bg(os.path.join(prev_dir, "scene_hero.png"))
        return

    # kit sheets with an orthographic camera
    cam_k = bpy.data.objects.new("KitCam", bpy.data.cameras.new("KitCam"))
    cam_k.data.type = "ORTHO"
    bpy.data.collections["Lighting"].objects.link(cam_k)
    scn.camera = cam_k
    for cat in ("Buildings", "Nature", "Props", "Resources"):
        for c2, objs in show_members.items():
            for o in objs:
                o.hide_render = (c2 != cat)
        lo, hi = show_bounds[cat]
        ctr = (lo + hi) / 2
        wdt = hi.x - lo.x
        dep = hi.y - lo.y
        ang = math.radians(58)
        vis_h = dep * math.cos(ang) + hi.z * math.sin(ang)
        aspect = 16 / 9
        cam_k.data.ortho_scale = max(wdt * 1.1, vis_h * aspect * 1.3)
        d = 60
        look = Vector((ctr.x, ctr.y + dep * 0.05, hi.z * 0.35))
        cam_k.location = look + Vector((0, -math.sin(ang), math.cos(ang))) * d
        cam_k.rotation_euler = (look - cam_k.location).to_track_quat("-Z", "Y").to_euler()
        cam_k.data.clip_end = 200
        shot(os.path.join(prev_dir, "kit_" + cat.lower() + ".png"), 1920, 1080, 32)
    for objs in show_members.values():
        for o in objs:
            o.hide_render = False
    scn.camera = cam
    composite_bg(os.path.join(prev_dir, "scene_hero.png"))


def composite_bg(path):
    try:
        from PIL import Image
    except ImportError:
        return
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    bg = Image.new("RGBA", (w, h))
    top, bot = (52, 64, 48), (33, 41, 31)
    px = bg.load()
    for y in range(h):
        k = y / (h - 1)
        c = tuple(int(top[i] + (bot[i] - top[i]) * k) for i in range(3)) + (255,)
        for x in range(w):
            px[x, y] = c
    bg.alpha_composite(im)
    bg.convert("RGB").save(path)


if __name__ == "__main__":
    main()
