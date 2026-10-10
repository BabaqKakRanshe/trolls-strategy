"""
Ассеты арены «Болото» (Arena_Swamp): корявые болотные деревья в мху (с листвой и сухое), коряги, рогоз,
осока, кувшинки, кочки, замшелые камни, сломанный кол, мостки, верёвочный забор, столб с черепом, навес
гоблинов на настиле, факел, мох-сосульки для кромки обрыва (drape) и оливковые плитки поля.

Соглашения кита: пивот внизу по центру, лицо к -Y, одна грань — один swatch, фаски на блоках, детали не
тоньше 6–10 см (на дистанции боя тоньше — рябь). Природа и мелочь (префикс Swamp_) сливаются в
Arena_Swamp_Scatter и красятся вариантом палитры биома (Vitaria_Palette_Swamp: приглушённая влажная
зелень); факел ставится отдельным объектом с общей палитрой, огонь над ним — FX_Flame_Small «Луга».
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix, noise
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal, _place, TM
from . import tiles as TL

BARK = by_normal("bark", "bark", "bark_dark", 0.6)
WET_WOOD = by_normal("bark", "bark_dark", "bark_dark", 0.6)


def _limb(a, p0, p1, r0, r1, col, seg=6, spin=0.0):
    """Сужающийся цилиндр от p0 (радиус r0) до p1 (r1): ствол, ветка, корень."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    q = d.to_track_quat("Z", "Y")
    a.add(p_cyl(r0, r1, d.length, seg, spin=spin, mat=Matrix.LocRotScale(p0, q, (1, 1, 1))), col)


def _strand(a, top, length, w, t, col, rng, kink=0.12):
    """Борода мха: два сужающихся плоских звена вниз от top, лёгкий излом — висит, а не торчит."""
    top = Vector(top)
    mid = top + Vector((rng.uniform(-kink, kink), rng.uniform(-kink, kink), -length * 0.55))
    end = mid + Vector((rng.uniform(-kink, kink), rng.uniform(-kink, kink), -length * 0.45))
    for p0, p1, w0, w1 in ((top, mid, w, w * 0.7), (mid, end, w * 0.7, w * 0.25)):
        d = p1 - p0
        q = d.to_track_quat("Z", "Y")
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0)
        for v in b.verts:
            k = v.co.z + 0.5                                  # 0 у верха звена, 1 у низа
            ww = w0 + (w1 - w0) * k
            v.co.x *= ww
            v.co.y *= t
            v.co.z = k * d.length
        ang = rng.uniform(0, 180)
        bmesh.ops.rotate(b, matrix=Matrix.Rotation(math.radians(ang), 3, "Z"), verts=b.verts, cent=(0, 0, 0))
        _place(b, None, None, Matrix.LocRotScale(p0, q, (1, 1, 1)))
        a.add(b, col)


# ---------------------------------------------------------------------------------------
# деревья
# ---------------------------------------------------------------------------------------
def mossy_bark(seed, moss=0.3):
    """Кора в пятнах мха: верхние грани и пятна по шуму — мох, остальное — кора (снизу темнее)."""
    off = Vector((seed * 1.7, seed * 0.9, seed * 2.3))

    def col(f):
        n = noise.noise(f.calc_center_median() * 1.4 + off)
        if f.normal.z > 0.6 or (f.normal.z > -0.25 and n > 0.45 - moss):
            return "moss" if n > 0.15 else "sod_dark"
        return "bark" if f.normal.z > -0.35 else "bark_dark"
    return col


def swamp_tree(a, seed=1, height=6.0, girth=1.0, spread=1.0, lean=(0.0, 0.0), arms=3, leaf=1.0, beards=1.0,
               drips=1.0, moss=0.3, bark_fn=None):
    """Болотное дерево, как на макете: раструб корней-подпорок, толстый ствол S-изгибом в пятнах мха,
    2–3 кривые руки, каждая делится на две ветки с загнутыми вверх кончиками. На кончиках — плоские
    клочья листвы, с их краёв свисает мох; с ветвей — длинные бороды. leaf — доля кончиков с листвой
    (0 — сухое дерево), beards — бород на звено ветви, drips — прядей под клочком; bark_fn — своя раскраска коры
    (по умолчанию кора в пятнах мха, mossy_bark)."""
    rng = random.Random(seed)
    s = height / 6.0
    g = girth * s
    bark = bark_fn or mossy_bark(seed, moss)
    # корни-подпорки: дуга из двух звеньев с узлом, от ствола в землю
    nr = 6
    for k in range(nr):
        ang = math.tau * k / nr + rng.uniform(-0.25, 0.25)
        reach = rng.uniform(1.0, 1.5) * g
        zt = rng.uniform(0.75, 1.2) * g
        ca, sa = math.cos(ang), math.sin(ang)
        p0 = Vector((ca * 0.22 * g, sa * 0.22 * g, zt))
        p1 = Vector((ca * reach * 0.55, sa * reach * 0.55, zt * 0.38))
        p2 = Vector((ca * reach, sa * reach, -0.18))
        _limb(a, p0, p1, 0.27 * g, 0.19 * g, bark, seg=6)
        _limb(a, p1, p2, 0.19 * g, 0.08 * g, bark, seg=5)
        a.add(p_ico(0.2 * g, 1, loc=p1, jitter=0.1, rng=rng), bark)
    # ствол: S-изгиб с наклоном, узлы на стыках, к развилке тоньше
    lx, ly = lean
    ph = rng.uniform(0, math.tau)
    pts = [Vector((0, 0, -0.1))]
    for k, f in enumerate((0.15, 0.29, 0.41, 0.52)):
        w = 0.34 * s * math.sin(ph + k * 1.8)
        v = 0.3 * s * math.cos(ph + k * 1.3)
        pts.append(Vector((w + lx * f * height, v + ly * f * height, f * height)))
    rad = [0.62 * g, 0.52 * g, 0.44 * g, 0.38 * g, 0.33 * g]
    for i in range(len(pts) - 1):
        _limb(a, pts[i], pts[i + 1], rad[i], rad[i + 1] * 1.06, bark, seg=8, spin=rng.uniform(0, 45))
        if i:
            a.add(p_ico(rad[i] * 1.12, 1, loc=pts[i], jitter=0.12, rng=rng), bark)
    fork = pts[-1]
    a.add(p_ico(rad[-1] * 1.3, 1, loc=fork, jitter=0.12, rng=rng), bark)
    # руки: два звена (крутятся и поднимаются), с конца — две ветки с крючком-кончиком вверх
    tips, hang = [], []
    a0 = rng.uniform(0, math.tau)
    for k in range(arms):
        ang = a0 + math.tau * k / arms + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(ang), math.sin(ang) * 0.85, rng.uniform(0.45, 0.85))).normalized()
        p, r = fork.copy(), 0.27 * g
        for j in range(2):
            d = Matrix.Rotation(rng.uniform(-0.45, 0.45), 3, "Z") @ d
            d.z += 0.15 * j
            d.normalize()
            q = p + d * rng.uniform(0.85, 1.2) * spread * s * (1.0 - 0.25 * j)
            _limb(a, p, q, r, r * 0.72, bark, seg=6)
            a.add(p_ico(r * 0.8, 1, loc=q, jitter=0.1, rng=rng), bark)
            hang.append(p.lerp(q, 0.6))
            p, r = q, r * 0.72
        for j in range(2):
            dd = Matrix.Rotation((j - 0.5) * rng.uniform(0.9, 1.4), 3, "Z") @ d
            dd.z = max(dd.z, 0.1) + rng.uniform(-0.1, 0.25)
            dd.normalize()
            q = p + dd * rng.uniform(0.7, 1.1) * spread * s
            _limb(a, p, q, r * 0.8, r * 0.5, bark, seg=5)
            hang.append(p.lerp(q, 0.5))
            t2 = q + (dd + Vector((0, 0, 0.9))).normalized() * rng.uniform(0.3, 0.55) * s
            _limb(a, q, t2, r * 0.5, max(0.035, r * 0.3), "bark", seg=4)
            tips.append((q, t2))
    # листва: плоские клочья на кончиках, по краю — пряди мха
    cols = [by_normal("moss", "leaf_mid", "leaf_dark", 0.5), by_normal("blade", "moss", "leaf_dark", 0.5),
            by_normal("moss", "leaf_dark", "pine_dark", 0.5)]
    for k, (q, t2) in enumerate(tips):
        if rng.random() > leaf:
            continue
        R = rng.uniform(0.5, 0.72) * s
        c = q.lerp(t2, 0.5) + Vector((0, 0, 0.05 * R))
        a.add(p_ico(R, 1, loc=c, jitter=0.24, rng=rng, scl=(1.35, 1.15, 0.46)), cols[k % len(cols)])
        a.add(p_ico(R * 0.6, 1, loc=c + Vector((rng.uniform(-0.5, 0.5) * R, rng.uniform(-0.4, 0.4) * R, 0.26 * R)),
                    jitter=0.2, rng=rng, scl=(1.2, 1.0, 0.55)), cols[(k + 1) % len(cols)])
        a.add(p_ico(R * 0.85, 1, loc=c + Vector((0, 0, -0.16 * R)), jitter=0.14, rng=rng, scl=(1.25, 1.1, 0.32)),
              "leaf_dark")
        nd = int(round(rng.uniform(4, 7) * drips))
        for j in range(nd):
            ang = math.tau * j / max(1, nd) + rng.uniform(-0.3, 0.3)
            top = c + Vector((math.cos(ang) * R * 1.05, math.sin(ang) * R * 0.9, -0.2 * R))
            _strand(a, top, rng.uniform(0.35, 1.0) * s, rng.uniform(0.16, 0.28) * s, 0.06,
                    ("moss_hang", "moss", "moss_hang")[j % 3], rng, kink=0.05)
    # бороды с ветвей
    nb = int(round(len(hang) * beards))
    for j in range(nb):
        p = hang[j % len(hang)]
        top = p + Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), -0.06))
        L = min(rng.uniform(0.9, 2.2) * s, top.z - 0.4)
        if L > 0.45:
            _strand(a, top, L, rng.uniform(0.14, 0.24) * s, 0.06, ("moss_hang", "moss_hang", "moss")[j % 3], rng)


def snag(a, seed=3):
    """Коряга: лежащий кривой ствол с выворотнем корней и сучьями, мох на спине."""
    rng = random.Random(seed)
    P = [Vector((-0.95, 0, 0.18)), Vector((-0.2, 0.08, 0.24)), Vector((0.55, -0.05, 0.2)), Vector((1.05, 0.1, 0.32))]
    R = [0.24, 0.22, 0.19, 0.13]
    for i in range(3):
        _limb(a, P[i], P[i + 1], R[i], R[i + 1], WET_WOOD, seg=6)
        a.add(p_ico(R[i + 1] * 1.05, 1, loc=P[i + 1], jitter=0.1, rng=rng), "bark_dark")
    # выворотень: плоский диск корней с торчащими корнями
    a.add(p_cyl(0.42, 0.36, 0.14, 7, loc=(-1.02, 0, 0.0), rot=(0, 90, 0)), by_normal("soil_dark", "bark_dark"))
    for k in range(6):
        ang = math.tau * k / 6 + rng.uniform(-0.2, 0.2)
        p0 = Vector((-1.06, math.cos(ang) * 0.3, 0.18 + math.sin(ang) * 0.3))
        p1 = p0 + Vector((-rng.uniform(0.15, 0.35), math.cos(ang) * rng.uniform(0.2, 0.4),
                          math.sin(ang) * rng.uniform(0.2, 0.4)))
        _limb(a, p0, p1, 0.065, 0.035, "bark_dark", seg=5)
    # сучья вверх
    for x, h, ang in ((0.1, 0.62, 18), (0.75, 0.48, -25)):
        _limb(a, (x, 0.0, 0.3), (x + 0.12, math.sin(math.radians(ang)) * 0.3, 0.3 + h), 0.08, 0.045, "bark", seg=5)
    # мох по спине
    for x in (-0.55, 0.2):
        a.add(p_ico(0.2, 1, loc=(x, 0.0, 0.36), scl=(1.6, 0.9, 0.42), jitter=0.15, rng=rng), "sod")


# ---------------------------------------------------------------------------------------
# у воды
# ---------------------------------------------------------------------------------------
def cattail(a, seed=5, n=7, h=(1.2, 1.8), r=0.28):
    """Рогоз: пучок стеблей с бурыми початками и длинные листья-ленты вразлёт."""
    rng = random.Random(seed)
    for k in range(n):
        ang = rng.uniform(0, math.tau)
        d = r * math.sqrt(rng.random())
        x, y = math.cos(ang) * d, math.sin(ang) * d
        hh = rng.uniform(*h)
        tilt = Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 1.0)).normalized()
        base = Vector((x, y, -0.05))
        top = base + tilt * hh
        _limb(a, base, top, 0.035, 0.028, "blade_dark", seg=4)
        hs = top - tilt * (0.1 + 0.36)                       # початок под кончиком стебля
        _limb(a, hs, hs + tilt * 0.34, 0.07, 0.066, "bark_dark" if k % 3 else "leather_dark", seg=6)
    for k in range(n + 3):                                   # листья: плоские ленты, наклонены наружу
        ang = math.tau * k / (n + 3) + rng.uniform(-0.3, 0.3)
        L = rng.uniform(0.75, 1.25) * (sum(h) / 2) / 1.5
        lean = rng.uniform(12, 30)
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0)
        for v in b.verts:
            kk = v.co.z + 0.5
            v.co.x *= 0.1 * (1.0 - 0.75 * kk)
            v.co.y *= 0.035
            v.co.z = kk * L
        _place(b, None, None, TM((math.cos(ang) * 0.1, math.sin(ang) * 0.1, -0.05),
                                 (lean * math.sin(ang), -lean * math.cos(ang), math.degrees(ang))))
        a.add(b, "leaf_mid" if k % 2 else "blade")


def reeds(a, seed=7, n=11, h=(0.42, 0.72)):
    """Осока: тёмный пучок толстых листьев вразлёт — кочки и берег."""
    rng = random.Random(seed)
    for k in range(n):
        ang = math.tau * k / n + rng.uniform(-0.2, 0.2)
        L = rng.uniform(*h)
        lean = rng.uniform(10, 34)
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0)
        for v in b.verts:
            kk = v.co.z + 0.5
            v.co.x *= 0.085 * (1.0 - 0.8 * kk)
            v.co.y *= 0.03
            v.co.z = kk * L
        _place(b, None, None, TM((math.cos(ang) * 0.05, math.sin(ang) * 0.05, -0.03),
                                 (lean * math.sin(ang), -lean * math.cos(ang), math.degrees(ang))))
        a.add(b, ("blade_dark", "leaf_dark", "blade")[k % 3])


def _pad(a, x, y, r, rot, z=0.0):
    """Лист кувшинки: диск с вырезом-клином, верх светлый (leaf_light палитры болота), кромка темнее."""
    n = 11
    cut = math.radians(34)
    pts = [(0.0, 0.0)]
    for i in range(n + 1):
        ang = cut / 2 + (math.tau - cut) * i / n
        pts.append((math.cos(ang) * r, math.sin(ang) * r))
    b = bmesh.new()
    lo = [b.verts.new((px, py, 0.0)) for px, py in pts]
    hi = [b.verts.new((px, py, 0.035)) for px, py in pts]
    b.faces.new(list(reversed(lo)))
    b.faces.new(hi)
    m = len(pts)
    for i in range(m):
        j = (i + 1) % m
        b.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    _place(b, None, None, TM((x, y, z), (0, 0, rot)))
    a.add(b, by_normal("leaf_light", "leaf_mid", "leaf_dark", 0.6))


def _lily_flower(a, x, y, s=1.0):
    for k in range(6):
        ang = math.tau * k / 6
        a.add(p_cyl(0.07 * s, 0.0, 0.18 * s, 4, loc=(x + math.cos(ang) * 0.06 * s, y + math.sin(ang) * 0.06 * s, 0.02),
                    rot=(math.sin(ang) * 40, -math.cos(ang) * 40, 0)), "flower_p" if k % 2 else "flower_w")
    a.add(p_ico(0.06 * s, 1, loc=(x, y, 0.08 * s)), "flower_y")


def lilypads(a, seed=9, n=3, flower=False, r=(0.3, 0.46)):
    """Кувшинки: несколько крупных листьев на воде (пивот — уровень воды), у одной — цветок."""
    rng = random.Random(seed)
    placed = []
    for k in range(n):
        rr = rng.uniform(*r) * (1.0 if k == 0 else rng.uniform(0.6, 0.85))
        for _ in range(20):
            ang = rng.uniform(0, math.tau)
            d = 0.0 if k == 0 else rng.uniform(0.5, 0.95)
            x, y = math.cos(ang) * d, math.sin(ang) * d
            if all(math.hypot(x - px, y - py) > (rr + pr) * 0.92 for px, py, pr in placed):
                break
        placed.append((x, y, rr))
        _pad(a, x, y, rr, rng.uniform(0, 360), z=rng.uniform(0, 0.008))
    if flower:
        x, y, rr = placed[-1]
        _lily_flower(a, x + rr * 0.3, y - rr * 0.2, 1.0)


def stake(a, seed=25):
    """Сломанный кол из воды: старая свая, верх расщеплён."""
    rng = random.Random(seed)
    a.add(p_box((0.15, 0.15, 1.15), loc=(0, 0, 0.32), rot=(rng.uniform(-8, 8), rng.uniform(-8, 8), 20), bevel=0.03),
          WET_WOOD)
    a.add(p_cyl(0.1, 0.0, 0.22, 4, loc=(0.02, 0.01, 0.88), rot=(6, -10, 65)), "bark_dark")
    a.add(p_ico(0.14, 1, loc=(0, 0, 0.0), scl=(1.4, 1.2, 0.4), jitter=0.15, rng=rng), "sod_dark")


def hummock(a, seed=11):
    """Кочка: травяной горб из воды с пучком осоки."""
    rng = random.Random(seed)
    a.add(p_ico(0.42, 1, loc=(0, 0, 0.02), scl=(1.0, 0.85, 0.42), jitter=0.12, rng=rng, cut=-0.12),
          by_normal("sod", "sod_dark", "soil_dark", 0.5))
    a.add(p_ico(0.3, 1, loc=(0, 0, -0.02), scl=(1.25, 1.1, 0.35), jitter=0.1, rng=rng, cut=-0.08), "soil_dark")
    reeds_b = random.Random(seed + 1)
    for k in range(6):
        ang = math.tau * k / 6 + reeds_b.uniform(-0.3, 0.3)
        L = reeds_b.uniform(0.3, 0.5)
        lean = reeds_b.uniform(14, 30)
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0)
        for v in b.verts:
            kk = v.co.z + 0.5
            v.co.x *= 0.08 * (1.0 - 0.8 * kk)
            v.co.y *= 0.03
            v.co.z = kk * L
        _place(b, None, None, TM((math.cos(ang) * 0.08, math.sin(ang) * 0.08, 0.12),
                                 (lean * math.sin(ang), -lean * math.cos(ang), math.degrees(ang))))
        a.add(b, "blade_dark" if k % 2 else "leaf_dark")


def mossy_rock(a, seed=13, size=0.45, n=2):
    """Замшелый валун: огранка как у камней кита, на макушке — мох."""
    rng = random.Random(seed)
    for k in range(n):
        s = size * (1.0 if k == 0 else rng.uniform(0.5, 0.7))
        off = (0, 0) if k == 0 else (rng.uniform(-1, 1) * size * 0.9, rng.uniform(-1, 1) * size * 0.9)
        a.add(p_ico(s, 1, loc=(off[0], off[1], s * 0.22), jitter=0.28, rng=rng, scl=(1, rng.uniform(0.8, 1.0), 0.7),
                    rot=(0, 0, rng.uniform(0, 360)), cut=-s * 0.3),
              lambda f: "sod" if f.normal.z > 0.72 else ("moss" if f.normal.z > 0.45 else
                                                         ("rock" if f.normal.z > -0.3 else "rock_dark")))


def mud_lump(a, seed=15):
    """Кочка ила: тёмный плоский горб у воды (кочки в мелкой воде и у берега)."""
    rng = random.Random(seed)
    a.add(p_ico(0.32, 1, loc=(0, 0, 0.0), scl=(1.2, 0.9, 0.3), jitter=0.15, rng=rng, cut=-0.06),
          by_normal("bog_dark", "soil_dark", "soil_dark", 0.5))


# ---------------------------------------------------------------------------------------
# постройки
# ---------------------------------------------------------------------------------------
BOARD_L, BOARD_W = 2.0, 1.05


def boardwalk(a, seed=17, posts_up=True):
    """Секция мостков 2 м: поперечные доски на двух лагах, сваи в воду. Пивот — верх настила по центру."""
    rng = random.Random(seed)
    n = 7
    pitch = BOARD_L / n
    for k in range(n):
        x = -BOARD_L / 2 + pitch * (k + 0.5)
        a.add(p_box((pitch - 0.035, BOARD_W + rng.uniform(-0.06, 0.06), 0.075),
                    loc=(x + rng.uniform(-0.01, 0.01), rng.uniform(-0.04, 0.04), -0.0375),
                    rot=(rng.uniform(-1.5, 1.5), 0, rng.uniform(-3, 3)), bevel=0.015),
              "wood_light" if k % 3 == 1 else ("wood_mid" if k % 3 else "wood_mid2"))
    for sy in (-1, 1):
        a.add(p_box((BOARD_L + 0.06, 0.12, 0.14), loc=(0, sy * 0.36, -0.145), bevel=0.02), "wood_dark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            top = 0.32 if (posts_up and sy < 0) else -0.04
            a.add(p_cyl(0.075, 0.07, top + 1.0, 6, loc=(sx * 0.86, sy * 0.5, -1.0)), "bark_dark")
            if top > 0:
                a.add(p_cyl(0.085, 0.0, 0.08, 6, loc=(sx * 0.86, sy * 0.5, top)), "bark_dark")


ROPE_SEG = 1.4


def rope_post(a):
    """Столб верёвочного забора: тёсаный кол с затёсом, обмотка верёвки."""
    a.add(p_box((0.13, 0.13, 0.86), loc=(0, 0, 0.36), bevel=0.03), "wood_dark")
    a.add(p_cyl(0.1, 0.0, 0.14, 4, loc=(0, 0, 0.79), spin=45), "wood_dark")
    a.add(p_cyl(0.085, 0.085, 0.07, 6, loc=(0, 0, 0.66)), "rope")


class _Sub:
    """Сдвиг: всё, что добавлено через обёртку, переносится на loc (столб в начале сегмента)."""

    def __init__(self, a, loc):
        self.a, self.loc = a, Vector(loc)

    def add(self, part, col, smooth=False):
        bmesh.ops.translate(part, vec=self.loc, verts=part.verts)
        self.a.add(part, col, smooth)


def rope_fence(a):
    """Сегмент верёвочного забора: столб в начале (x = -ROPE_SEG/2) и провисшая верёвка до следующего."""
    rope_post(_Sub(a, (-ROPE_SEG / 2, 0, 0)))
    pts = [Vector((-ROPE_SEG / 2 + 0.06, 0, 0.69)), Vector((-0.2, 0, 0.58)), Vector((0.25, 0, 0.58)),
           Vector((ROPE_SEG / 2 - 0.06, 0, 0.69))]
    for p0, p1 in zip(pts, pts[1:]):
        _limb(a, p0, p1, 0.035, 0.035, "rope", seg=5)


def skull_post(a, seed=19):
    """Столб с рогатым черепом: метка гоблинского болота у края поля."""
    rng = random.Random(seed)
    for x, y, r in ((0.12, -0.06, 0.16), (-0.13, 0.05, 0.14)):
        a.add(p_ico(r, 1, loc=(x, y, r * 0.25), scl=(1, 0.9, 0.7), jitter=0.2, rng=rng, cut=-r * 0.3),
              by_normal("sod", "rock", "rock_dark", 0.6))
    a.add(p_box((0.14, 0.14, 1.78), loc=(0, 0, 0.85), rot=(0, 2, 0), bevel=0.03), "bark")
    a.add(p_box((0.62, 0.1, 0.1), loc=(0, 0.02, 1.42), rot=(0, 6, 0), bevel=0.025), "bark_dark")   # перекладина
    for sx in (-1, 1):                                                                            # подвески-кости
        a.add(p_box((0.07, 0.07, 0.22), loc=(sx * 0.25, 0.0, 1.24), rot=(0, sx * 8, 0), bevel=0.015), "bone")
    a.add(p_box((0.18, 0.04, 0.26), loc=(0.0, -0.08, 1.52), rot=(0, 0, 0), bevel=0.01), "cloth")  # лоскут
    z = 1.86
    a.add(p_ico(0.19, 1, loc=(0, 0.02, z), scl=(1.0, 0.95, 0.85)), "bone")                       # свод черепа
    a.add(p_box((0.2, 0.18, 0.13), loc=(0, -0.13, z - 0.1), bevel=0.04), "bone")                  # морда
    for sx in (-1, 1):
        a.add(p_box((0.06, 0.03, 0.06), loc=(sx * 0.06, -0.2, z + 0.0), bevel=0.012), "black")    # глазницы
        a.add(p_cyl(0.06, 0.0, 0.34, 6, loc=(sx * 0.13, 0.02, z + 0.08), rot=(0, sx * 62, 0)), "bone")   # рога
        a.add(p_cyl(0.045, 0.0, 0.22, 5, loc=(sx * 0.4, 0.02, z + 0.25), rot=(0, -sx * 18, 0)), "bone")


TORCH_TOP = 1.74      # верх чаши факела: здесь стоит FX_Flame_Small


def torch(a, seed=21):
    """Факел на шесте: кол в земле, обмотка, железная чаша с углями. Пламя — отдельный FX_Flame_Small."""
    rng = random.Random(seed)
    for x, y, r in ((0.1, -0.05, 0.12), (-0.1, 0.06, 0.11)):
        a.add(p_ico(r, 1, loc=(x, y, r * 0.2), scl=(1, 0.9, 0.7), jitter=0.2, rng=rng, cut=-r * 0.3),
              by_normal("sod", "rock", "rock_dark", 0.6))
    a.add(p_cyl(0.065, 0.055, 1.58, 6, loc=(0, 0, -0.05)), "wood_dark")
    a.add(p_cyl(0.085, 0.085, 0.2, 6, loc=(0, 0, 1.26)), "burlap_dark")
    a.add(p_cyl(0.09, 0.09, 0.06, 6, loc=(0, 0, 1.46)), "rope")
    a.add(p_cyl(0.1, 0.17, 0.2, 8, loc=(0, 0, 1.54), cap=True), by_normal("iron_dark", "iron", "iron_dark", 0.6))
    a.add(p_cyl(0.185, 0.185, 0.035, 8, loc=(0, 0, TORCH_TOP - 0.02)), "iron_dark")
    for k in range(5):
        ang = rng.uniform(0, math.tau)
        d = 0.09 * math.sqrt(rng.random())
        a.add(p_ico(0.05, 1, loc=(math.cos(ang) * d, math.sin(ang) * d, TORCH_TOP - 0.03), scl=(1, 1, 0.7)),
              "ember" if k % 2 else "glow")


def awning(a, seed=23):
    """Навес гоблинов на настиле, как на макете: полотно скатом на четырёх столбах (высокая сторона — к -Y),
    дощатый настил на коротких сваях над сырой землёй; под навесом стол, ящик и мешок.
    Пивот — земля под серединой настила."""
    import build_vitaria as V
    rng = random.Random(seed)
    W, D, Z = 2.5, 2.1, 0.3                      # настил: ширина (X), глубина (Y), верх
    n = 8
    pitch = W / n
    for k in range(n):
        x = -W / 2 + pitch * (k + 0.5)
        a.add(p_box((pitch - 0.035, D + rng.uniform(-0.08, 0.08), 0.07), loc=(x, rng.uniform(-0.04, 0.04), Z - 0.035),
                    rot=(0, rng.uniform(-1.2, 1.2), rng.uniform(-1.5, 1.5)), bevel=0.015),
              ("wood_mid", "wood_light", "wood_mid2")[k % 3])
    for sy in (-1, 1):                            # лаги
        a.add(p_box((W + 0.1, 0.12, 0.13), loc=(0, sy * 0.72, Z - 0.135), bevel=0.02), "wood_dark")
    for sx in (-1, 0, 1):                         # сваи
        for sy in (-1, 1):
            a.add(p_cyl(0.08, 0.075, Z + 0.25, 6, loc=(sx * 1.05, sy * 0.72, -0.25)), "bark_dark")
    H0, H1 = 2.35, 1.45                           # перекладины: передняя и задняя
    yf, yb = -D / 2 + 0.08, D / 2 - 0.08
    xs = W / 2 - 0.06
    for sx in (-1, 1):
        a.add(p_box((0.13, 0.13, H0 - Z + 0.1), loc=(sx * xs, yf, (Z + H0 + 0.1) / 2), rot=(0, 0, rng.uniform(-4, 4)),
                    bevel=0.03), "wood_dark")
        a.add(p_cyl(0.1, 0.0, 0.14, 4, loc=(sx * xs, yf, H0 + 0.1), spin=45), "wood_dark")
        a.add(p_box((0.13, 0.13, H1 - Z + 0.08), loc=(sx * xs, yb, (Z + H1 + 0.08) / 2), bevel=0.03), "wood_dark")
    for y, z in ((yf, H0), (yb, H1)):
        a.add(p_box((W + 0.3, 0.1, 0.1), loc=(0, y, z), bevel=0.025), "wood_mid")
    # полотно: три полосы скатом от передней перекладины назад, средняя чуть провисает; свес за заднюю
    y0, z0 = yf - 0.16, H0 + 0.08
    y1, z1 = yb + 0.5, H1 - 0.3
    L = math.hypot(y1 - y0, z1 - z0)
    ang = math.degrees(math.atan2(z1 - z0, y1 - y0))
    strips = ((-W / 2 - 0.22, -W / 6, 0.0, "burlap"), (-W / 6, W / 6, -0.05, "canvas_dark"),
              (W / 6, W / 2 + 0.22, 0.0, "burlap"))
    for xa, xb, sag, col in strips:
        a.add(p_box((xb - xa + 0.01, L, 0.045), loc=((xa + xb) / 2, (y0 + y1) / 2, (z0 + z1) / 2 + sag),
                    rot=(ang, 0, 0), bevel=0.012),
              lambda f, c=col: "burlap_dark" if f.normal.z < -0.3 else c)
    a.add(p_box((W + 0.44, 0.04, 0.3), loc=(0, y0 - 0.02, z0 - 0.13), rot=(-6, 0, 0), bevel=0.012), "burlap_dark")
    # под навесом: стол, ящик, мешок
    tz = Z + 0.74
    a.add(p_box((0.95, 0.55, 0.07), loc=(-0.45, 0.25, tz), bevel=0.02), "wood_light")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.07, 0.07, 0.72), loc=(-0.45 + sx * 0.4, 0.25 + sy * 0.2, Z + 0.36), bevel=0.015), "wood_dark")
    a.add(p_box((0.3, 0.22, 0.12), loc=(-0.55, 0.3, tz + 0.1), rot=(0, 0, 12), bevel=0.03), "leather_dark")
    a.add(p_cyl(0.09, 0.08, 0.16, 6, loc=(-0.2, 0.18, tz + 0.035)), "iron_dark")
    V.build_crate(a, 0.48, loc=(0.6, 0.45, Z))
    V.build_sack(a, loc=(0.75, -0.25, Z), seed=3)


def drape(a, top, nrm, length, w, col, rng, t=0.09, bow=0.14, taper=0.85):
    """Мох с кромки обрыва: широкая сосулька плашмя к наружной нормали nrm, книзу сужается и чуть
    отходит от скалы (под кромкой пласты уходят внутрь — мох висит в воздухе, как на макете)."""
    top = Vector(top)
    n = Vector((nrm.x, nrm.y, 0.0)).normalized()
    side = Vector((-n.y, n.x, 0.0))
    k = 3
    b = bmesh.new()
    rows = []
    for i in range(k + 1):
        f = i / k
        width = w * (1.0 - taper * f ** 1.6)
        c = top + n * (bow * f * f + rng.uniform(-0.03, 0.03)) + side * (rng.uniform(-0.06, 0.06) * f) + \
            Vector((0, 0, -length * f))
        th = t * (1.0 - 0.5 * f)
        rows.append([b.verts.new(c + side * (width / 2 * sx) + n * (th / 2 * sy))
                     for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    for i in range(k):
        r0, r1 = rows[i], rows[i + 1]
        for j in range(4):
            j2 = (j + 1) % 4
            b.faces.new((r0[j], r0[j2], r1[j2], r1[j]))
    b.faces.new(rows[0][::-1])
    b.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    b.normal_update()
    a.add(b, col)


# ---------------------------------------------------------------------------------------
# бойцы-заглушки для превью (в игру не идут): масштаб поля рядом с отрядом
# ---------------------------------------------------------------------------------------
def preview_troll(a):
    a.add(p_cyl(0.16, 0.14, 0.75, 6, loc=(-0.2, 0, 0)), "slate_dark")
    a.add(p_cyl(0.16, 0.14, 0.75, 6, loc=(0.2, 0, 0)), "slate_dark")
    a.add(p_ico(0.55, 1, loc=(0, 0, 1.1), scl=(1.05, 0.8, 0.95)), "slate")
    a.add(p_box((0.8, 0.5, 0.3), loc=(0, 0, 0.72), bevel=0.05), "leather")
    a.add(p_ico(0.3, 1, loc=(0, -0.08, 1.72), scl=(1, 0.95, 0.9)), "slate_light")
    for sx in (-1, 1):
        _limb(a, (sx * 0.55, 0, 1.35), (sx * 0.72, -0.1, 0.65), 0.14, 0.12, "slate", seg=6)
    _limb(a, (0.74, -0.15, 0.55), (0.9, -0.35, 1.6), 0.07, 0.13, "wood_mid", seg=6)          # дубина


def preview_goblin(a):
    a.add(p_cyl(0.09, 0.08, 0.42, 6, loc=(-0.12, 0, 0)), "leaf_dark")
    a.add(p_cyl(0.09, 0.08, 0.42, 6, loc=(0.12, 0, 0)), "leaf_dark")
    a.add(p_ico(0.3, 1, loc=(0, 0, 0.66), scl=(1, 0.8, 1.0)), "leather")
    a.add(p_ico(0.27, 1, loc=(0, -0.04, 1.05), scl=(1.1, 1.0, 0.9)), "leaf_light")
    for sx in (-1, 1):
        a.add(p_cyl(0.07, 0.0, 0.32, 4, loc=(sx * 0.24, 0.0, 1.08), rot=(0, sx * 75, 0)), "leaf_light")   # уши
        _limb(a, (sx * 0.3, 0, 0.82), (sx * 0.4, -0.08, 0.45), 0.07, 0.06, "leaf_mid", seg=5)
    _limb(a, (0.42, -0.1, 0.4), (0.55, -0.3, 0.95), 0.03, 0.03, "iron_light", seg=4)            # нож


ASSETS = [
    ("Swamp", "Swamp_Willow_A", "Болотное дерево, большое: клочья листвы и мох",
     lambda a: swamp_tree(a, 1, 6.4, 1.1, 1.15, (0.06, 0.0), 3, 1.0, 0.8, 1.0)),
    ("Swamp", "Swamp_Willow_B", "Болотное дерево, малое", lambda a: swamp_tree(a, 2, 4.6, 0.9, 1.0, (-0.1, 0.03), 2, 1.0, 0.6, 0.9)),
    ("Swamp", "Swamp_DeadTree", "Сухое корявое дерево в бородах мха",
     lambda a: swamp_tree(a, 4, 6.0, 1.15, 1.2, (0.05, -0.03), 3, 0.12, 2.2, 0.6, moss=0.4)),
    ("Swamp", "Swamp_Snag_A", "Коряга", snag),
    ("Swamp", "Swamp_Cattail_A", "Рогоз", lambda a: cattail(a, 5, 7, (1.2, 1.75), 0.26)),
    ("Swamp", "Swamp_Cattail_B", "Рогоз, густой", lambda a: cattail(a, 6, 11, (1.3, 2.0), 0.36)),
    ("Swamp", "Swamp_Reeds_A", "Осока", reeds),
    ("Swamp", "Swamp_LilyPad_A", "Кувшинки", lambda a: lilypads(a, 9, 3, False)),
    ("Swamp", "Swamp_LilyPad_B", "Кувшинки с цветком", lambda a: lilypads(a, 10, 2, True)),
    ("Swamp", "Swamp_LilyPad_C", "Кувшинка, большой лист", lambda a: lilypads(a, 12, 1, False, (0.46, 0.52))),
    ("Swamp", "Swamp_Hummock_A", "Кочка", hummock),
    ("Swamp", "Swamp_Mud_A", "Кочка ила", mud_lump),
    ("Swamp", "Swamp_Rock_A", "Замшелый валун", lambda a: mossy_rock(a, 13, 0.5, 2)),
    ("Swamp", "Swamp_Rock_B", "Замшелый камень", lambda a: mossy_rock(a, 14, 0.3, 1)),
    ("Swamp", "Swamp_Stake", "Сломанный кол в воде", stake),
    ("Swamp", "Swamp_Boardwalk", "Мостки, секция 2 м", boardwalk),
    ("Swamp", "Swamp_RopeFence", "Верёвочный забор, сегмент 1.4 м", rope_fence),
    ("Swamp", "Swamp_RopePost", "Столб верёвочного забора", rope_post),
    ("Swamp", "Swamp_SkullPost", "Столб с черепом", skull_post),
    ("Swamp", "Swamp_Awning", "Навес гоблинов на настиле", awning),
    ("Swamp", "Prop_Torch", "Факел на шесте", torch),
    # плитки поля биома: геометрия и UV — как у Hex_Tile_A/B/C, цвет — палитра Vitaria_Palette_Swamp (оливковые)
    ("Swamp", "Hex_Tile_Swamp_A", "Гекс-плитка болота A", TL.tile_plain(11, base=0.5)),
    ("Swamp", "Hex_Tile_Swamp_B", "Гекс-плитка болота B (кочки)", TL.tile_tufts),
    ("Swamp", "Hex_Tile_Swamp_C", "Гекс-плитка болота C (камни)", TL.tile_pebbles),
    ("Preview", "Preview_Troll", "Тролль-заглушка (превью)", preview_troll),
    ("Preview", "Preview_Goblin", "Гоблин-заглушка (превью)", preview_goblin),
]
