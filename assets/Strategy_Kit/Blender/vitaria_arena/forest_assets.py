"""
Ассеты арены «Лес» (Arena_Forest): высокие ели ярусами, сосны, лиственные деревья, кусты, валуны во мху, камни,
поваленный ствол, штабели брёвен, колода с топором, охотничий шалаш, рама со шкурой, жердевая ограда, дощатый
мостик через ручей, торговый навес, ведро, факел, грибы, цветы; моховые плитки поля.

Соглашения кита: пивот внизу по центру, лицо к -Y, одна грань — один swatch, фаски на блоках, детали не
тоньше 6 см. Природа, постройки и мелочь (префикс Forest_) сливаются в Arena_Forest_Scatter и красятся палитрой
биома Vitaria_Palette_Forest (build_vitaria.PALETTE_VARIANTS["Forest"]): хвоя — pine_*, листва — leaf_*, bush,
кора — bark, bark_dark, мох — moss, sod, камень — rock, rock_dark, дерево построек — wood_*, полотно — canvas,
burlap, шкура — hide*, шляпки грибов — leaf_autumn, цветы — flower_*. Угли факела — на светящихся swatch-ах
(при выгрузке — слот Vitaria_FX), огонь над ним — FX_Flame_Small «Луга».
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from . import tiles as TL
from . import swamp_assets as SA

BARK = by_normal("bark", "bark", "bark_dark", 0.6)
WOOD = by_normal("wood_light", "wood_mid", "wood_dark", 0.6)
MOSS = by_normal("moss", "sod_dark", "sod_dark", 0.5)


def _rock_col(f):
    """Камень во мху: макушка — мох, плечи — дёрн, бока — серый камень, низ в тени."""
    if f.normal.z > 0.74:
        return "moss"
    if f.normal.z > 0.5:
        return "sod"
    return "rock" if f.normal.z > -0.3 else "rock_dark"


# ---------------------------------------------------------------------------------------
# хвойные
# ---------------------------------------------------------------------------------------
def _skirt(a, c, z, r, h, k, spin, droop, rng, base, lit, notch=0.68, under=0.32):
    """Ярус ели — «юбка» звездой: 2k вершин по краю (кончики лап ниже и дальше, выемки между ними выше и ближе),
    вершина яруса сверху, вогнутый низ к стволу — снизу хвоя тёмная, как на макете."""
    b = bmesh.new()
    apex = b.verts.new(c + Vector((0.0, 0.0, z + h)))
    low = b.verts.new(c + Vector((0.0, 0.0, z + h * under)))
    rim = []
    for j in range(2 * k):
        ang = spin + math.pi * j / k + rng.uniform(-0.06, 0.06)
        if j % 2 == 0:
            rr, zz = r * rng.uniform(0.92, 1.08), z - droop * r * rng.uniform(0.75, 1.0)
        else:
            rr, zz = r * notch * rng.uniform(0.94, 1.06), z + h * 0.05
        rim.append(b.verts.new(c + Vector((math.cos(ang) * rr, math.sin(ang) * rr, zz))))
    n = len(rim)
    for j in range(n):
        b.faces.new((apex, rim[j], rim[(j + 1) % n]))
        b.faces.new((low, rim[(j + 1) % n], rim[j]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    b.normal_update()
    a.add(b, lambda f: "pine_dark" if f.normal.z < -0.15 else (lit if f.normal.z > 0.6 else base))


def spruce(a, seed=1, height=5.5, tiers=6, spread=1.0, k=8, droop=0.32, lean=(0.0, 0.0)):
    """Ель, как на макете: ярусы-«юбки» звездой (лапы свисают кончиками), низ каждого яруса тёмный,
    верхние ярусы светлее; ствол почти не виден. lean — наклон верхушки (доля высоты)."""
    rng = random.Random(seed)
    R0 = 0.26 * height * spread
    tz = 0.1 * height
    hs = [1.0 - 0.34 * i / max(1, tiers - 1) for i in range(tiers)]
    H0 = (height - tz) / (0.5 * sum(hs[:-1]) + hs[-1])
    rt = 0.05 * height ** 0.85
    a.add(p_cyl(rt * 1.3, rt * 0.75, tz + H0 * 0.7, 6, loc=(0, 0, -0.15)), "bark_dark")
    z = tz
    for i in range(tiers):
        f = i / max(1, tiers - 1)
        r = R0 * (1.0 - 0.74 * f) * rng.uniform(0.95, 1.05)
        h = H0 * hs[i]
        c = Vector((lean[0] * z + rng.uniform(-0.04, 0.04) * R0, lean[1] * z + rng.uniform(-0.04, 0.04) * R0, 0.0))
        base = "pine_dark" if i == 0 else "pine_mid"
        lit = "pine_light" if f > 0.3 else "pine_mid"
        _skirt(a, c, z, r, h, k if f < 0.75 else max(5, k - 2), rng.uniform(0, math.tau), droop * (1.0 - 0.4 * f),
               rng, base, lit)
        z += h * 0.5


def pine(a, seed=5, height=7.0, spread=1.0, lean=(0.0, 0.0)):
    """Сосна: высокий стройный ствол, крона ярусами плоских гранёных лап по верхней трети — каждая лапа на своём
    суке, верхушка — малая шапка."""
    rng = random.Random(seed)
    top = Vector((lean[0] * height, lean[1] * height, height * 0.97))
    rt = 0.03 * height ** 0.9
    SA._limb(a, (0, 0, -0.15), top * 0.55, rt, rt * 0.78, BARK, seg=7)
    SA._limb(a, top * 0.55, top, rt * 0.78, rt * 0.3, "bark", seg=6)
    R = 0.17 * height * spread
    k = 7
    for j in range(k):
        f = j / (k - 1)
        z = height * (0.56 + 0.36 * f)
        on = top * (z / top.z)
        ang = j * 2.4 + rng.uniform(-0.3, 0.3)                       # золотой угол: лапы не встают в ряд
        d = R * (0.85 - 0.55 * f) * rng.uniform(0.85, 1.1)
        c = on + Vector((math.cos(ang) * d, math.sin(ang) * d, R * 0.12))
        SA._limb(a, on - Vector((0, 0, 0.1)), c, rt * 0.36, rt * 0.2, "bark", seg=5)
        s = R * (0.62 - 0.22 * f) * rng.uniform(0.9, 1.1)
        a.add(p_ico(s, 1, loc=c + Vector((0, 0, s * 0.18)), scl=(1.45, 1.2, 0.5), jitter=0.1, rng=rng,
                    rot=(0, 0, math.degrees(ang))),
              lambda fc: "pine_dark" if fc.normal.z < -0.25 else ("pine_light" if fc.normal.z > 0.55 else "pine_mid"))
    a.add(p_ico(R * 0.42, 1, loc=top + Vector((0, 0, -0.05)), scl=(1.2, 1.2, 0.75), jitter=0.1, rng=rng),
          lambda fc: "pine_dark" if fc.normal.z < -0.25 else "pine_light")


# ---------------------------------------------------------------------------------------
# лиственные, кусты
# ---------------------------------------------------------------------------------------
def _leaf(col):
    dark = {"leaf_light": "leaf_mid", "leaf_mid": "leaf_dark", "leaf_dark": "leaf_dark", "bush": "leaf_dark"}[col]
    lit = {"leaf_light": "leaf_light", "leaf_mid": "leaf_light", "leaf_dark": "bush", "bush": "leaf_mid"}[col]
    return lambda f: dark if f.normal.z < -0.35 else (lit if f.normal.z > 0.7 else col)


def round_tree(a, seed=7, height=4.2, spread=1.0, girth=1.0, clumps=7, roots=False, lean=(0.0, 0.0)):
    """Лиственное дерево, как на макете: ствол с изгибом и сучьями, крона из гранёных комьев — снизу темнее,
    сверху светлее (жёлто-зелёная листва леса). roots — корни-подпорки у толстого ствола (старый дуб)."""
    rng = random.Random(seed)
    rt = 0.11 * girth * height ** 0.6
    cz = height * 0.6
    R = 0.3 * height * spread
    crown = Vector((lean[0] * height, lean[1] * height, cz))
    bend = Vector((crown.x * 0.5 + rng.uniform(-0.15, 0.15), crown.y * 0.5 + rng.uniform(-0.15, 0.15), cz * 0.5))
    SA._limb(a, (0, 0, -0.15), bend, rt, rt * 0.8, BARK, seg=7)
    SA._limb(a, bend, crown - Vector((0, 0, R * 0.25)), rt * 0.8, rt * 0.55, BARK, seg=7)
    if roots:
        for j in range(5):
            ang = math.tau * j / 5 + rng.uniform(-0.3, 0.3)
            p0 = Vector((math.cos(ang) * rt * 0.4, math.sin(ang) * rt * 0.4, rt * 1.6))
            p1 = Vector((math.cos(ang) * rt * 2.6, math.sin(ang) * rt * 2.6, -0.12))
            SA._limb(a, p0, p1, rt * 0.5, rt * 0.16, BARK, seg=5)
    blobs = [(Vector((0, 0, 0.05)), 0.62, "leaf_mid")]
    for j in range(clumps - 2):
        ang = math.tau * j / (clumps - 2) + rng.uniform(-0.3, 0.3)
        d = R * rng.uniform(0.55, 0.78)
        dz = R * rng.uniform(-0.38, 0.18)
        blobs.append((Vector((math.cos(ang) * d, math.sin(ang) * d, dz)), rng.uniform(0.42, 0.56),
                      rng.choice(["leaf_mid", "leaf_dark", "leaf_mid"])))
    blobs.append((Vector((rng.uniform(-0.15, 0.15) * R, rng.uniform(-0.15, 0.15) * R, R * 0.58)), 0.5, "leaf_light"))
    for off, s, col in blobs:
        c = crown + off
        if off.length > 0.4 * R:                           # сук к кому кроны
            SA._limb(a, crown - Vector((0, 0, R * 0.3)), crown + off * 0.62, rt * 0.4, rt * 0.22, "bark", seg=5)
        a.add(p_ico(R * s, 2 if s > 0.58 else 1, loc=c, scl=(1.0, 1.0, 0.86), jitter=0.12, rng=rng,
                    rot=(0, 0, rng.uniform(0, 90))), _leaf(col))


def bush(a, seed=11, size=1.0, n=4, col="bush"):
    """Куст: три–четыре гранёных кома листвы, сверху светлее."""
    rng = random.Random(seed)
    for j in range(n):
        if j == 0:
            off, r = Vector((0, 0, 0.22)), 0.42
        else:
            ang = math.tau * j / (n - 1) + rng.uniform(-0.4, 0.4)
            off, r = Vector((math.cos(ang) * 0.34, math.sin(ang) * 0.3, 0.14)), rng.uniform(0.26, 0.34)
        a.add(p_ico(r * size, 1, loc=off * size, scl=(1.0, 1.0, 0.85), jitter=0.12, rng=rng, cut=-0.12 * size),
              _leaf(col if j == 0 else rng.choice([col, "leaf_dark", "bush"])))


def fern(a, seed=13, n=7, length=0.8):
    """Папоротник: розетка изогнутых вай (плоских сужающихся листьев) — подлесок у стволов и камней."""
    rng = random.Random(seed)
    for j in range(n):
        ang = math.tau * j / n + rng.uniform(-0.25, 0.25)
        d = Vector((math.cos(ang), math.sin(ang), 0.0))
        L = length * rng.uniform(0.75, 1.0)
        p0 = Vector((0, 0, 0.02))
        p1 = p0 + d * L * 0.5 + Vector((0, 0, L * 0.42))
        p2 = p0 + d * L + Vector((0, 0, L * 0.18))
        for q0, q1, w0, w1 in ((p0, p1, 0.05, 0.2), (p1, p2, 0.2, 0.03)):
            dv = q1 - q0
            b = bmesh.new()
            bmesh.ops.create_cube(b, size=1.0)
            for v in b.verts:
                kk = v.co.z + 0.5
                v.co.x *= w0 + (w1 - w0) * kk
                v.co.y *= 0.03
                v.co.z = kk * dv.length
            q = dv.to_track_quat("Z", "Y")
            # трек «Z вдоль вайи, Y вверх»: тонкая сторона листа смотрит вверх — лист лежит плашмя
            bmesh.ops.transform(b, matrix=Matrix.LocRotScale(q0, q, (1, 1, 1)), verts=b.verts)
            b.normal_update()
            a.add(b, lambda f: "bush" if f.normal.z > 0.3 else "leaf_dark")


# ---------------------------------------------------------------------------------------
# камни
# ---------------------------------------------------------------------------------------
def boulder(a, seed=21, size=1.0, n=2, flat=0.72, sub=2):
    """Валун во мху, как на макете: крупная серая глыба гранями, на макушке — подушка мха чуть шире макушки;
    рядом — камни поменьше."""
    rng = random.Random(seed)
    for k in range(n):
        s = size * (1.0 if k == 0 else rng.uniform(0.4, 0.58))
        off = (0.0, 0.0) if k == 0 else (rng.choice([-1, 1]) * size * rng.uniform(0.8, 1.05),
                                         rng.uniform(-0.6, 0.6) * size)
        sy, rz = rng.uniform(0.82, 1.0), rng.uniform(0, 360)
        a.add(p_ico(s, sub if k == 0 else 1, loc=(off[0], off[1], s * 0.28), jitter=0.16, rng=rng,
                    scl=(1.0, sy, flat), rot=(0, 0, rz), cut=-s * 0.32), _rock_col)
        if k == 0 or s > 0.45:
            a.add(p_ico(s * 0.74, 1, loc=(off[0] + rng.uniform(-0.1, 0.1) * s, off[1], s * (0.28 + flat * 0.8)),
                        jitter=0.12, rng=rng, scl=(1.12, sy * 1.08, 0.3), rot=(0, 0, rz + 30), cut=-s * 0.06),
                  lambda f: "moss" if f.normal.z > 0.35 else "sod_dark")


def rock(a, seed=25, size=0.4, n=2):
    """Камень поменьше: серый, мох только на макушке."""
    rng = random.Random(seed)
    for k in range(n):
        s = size * (1.0 if k == 0 else rng.uniform(0.45, 0.65))
        off = (0, 0) if k == 0 else (rng.uniform(-1, 1) * size * 0.95, rng.uniform(-1, 1) * size * 0.9)
        a.add(p_ico(s, 1, loc=(off[0], off[1], s * 0.2), jitter=0.24, rng=rng, scl=(1, rng.uniform(0.8, 1.0), 0.66),
                    rot=(0, 0, rng.uniform(0, 360)), cut=-s * 0.3), _rock_col)


# ---------------------------------------------------------------------------------------
# брёвна
# ---------------------------------------------------------------------------------------
def _log(a, p0, p1, r, rng, moss=0, seg=8):
    """Бревно от p0 до p1: кора гранями, торцы светлые с кольцом; moss — сколько подушек мха лежит сверху."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    q = d.to_track_quat("Z", "Y")
    axis = d.normalized()

    def col(f):
        if abs(f.normal.dot(axis)) > 0.9:
            return "wood_pale"
        return "bark" if f.normal.z > -0.4 else "bark_dark"
    a.add(p_cyl(r, r * 0.96, d.length, seg, spin=rng.uniform(0, 45), mat=Matrix.LocRotScale(p0, q, (1, 1, 1))), col)
    for e, sgn in ((p0, -1), (p1, 1)):                    # годовые кольца торца
        a.add(p_cyl(r * 0.52, r * 0.52, 0.02, seg, mat=Matrix.LocRotScale(e + axis * (sgn * 0.012 - 0.01), q, (1, 1, 1))),
              "wood_light")
    side = axis.cross(Vector((0, 0, 1)))
    side = side.normalized() if side.length > 1e-6 else Vector((1, 0, 0))
    for j in range(moss):                                 # подушки мха на спине бревна
        t = (j + 0.5) / moss + rng.uniform(-0.12, 0.12) / moss
        c = p0.lerp(p1, min(0.92, max(0.08, t))) + side * rng.uniform(-0.25, 0.25) * r + Vector((0, 0, r * 0.82))
        m = Matrix.LocRotScale(c, axis.to_track_quat("X", "Z"), (1, 1, 1))
        part = p_ico(r * rng.uniform(0.55, 0.8), 1, scl=(1.9, 1.15, 0.38), jitter=0.12, rng=rng)
        bmesh.ops.transform(part, matrix=m, verts=part.verts)
        a.add(part, lambda f: "moss" if f.normal.z > 0.3 else "sod_dark")


def fallen_log(a, seed=31, length=4.6, r=0.34):
    """Поваленный ствол: толстое бревно в мху, сломанные сучья, корневище-выворотень на одном конце."""
    rng = random.Random(seed)
    p0, p1 = Vector((-length / 2, 0, r * 0.85)), Vector((length / 2, 0, r * 0.75))
    _log(a, p0, p1, r, rng, moss=3, seg=9)
    for j in range(3):                                    # обломки сучьев
        t = rng.uniform(0.2, 0.85)
        c = p0.lerp(p1, t)
        ang = rng.uniform(-0.8, 0.8) + (math.pi / 2 if j % 2 else -math.pi / 2)
        tip = c + Vector((rng.uniform(-0.3, 0.3), math.cos(ang) * 0.1, 0.0)) + \
            Vector((rng.uniform(-0.2, 0.2), math.sin(ang) * r * 2.2, r * rng.uniform(0.6, 1.4)))
        SA._limb(a, c, tip, r * 0.28, r * 0.12, "bark", seg=5)
    for j in range(6):                                    # выворотень: корни веером
        ang = math.tau * j / 6 + rng.uniform(-0.25, 0.25)
        tip = p0 + Vector((-0.25, math.cos(ang) * r * 2.6, math.sin(ang) * r * 2.2 + 0.1))
        tip.z = max(0.05, tip.z)
        SA._limb(a, p0 + Vector((0.05, 0, 0)), tip, r * 0.32, r * 0.08, "bark_dark", seg=5)
    a.add(p_ico(r * 1.05, 1, loc=p0 + Vector((-0.12, 0, -r * 0.2)), scl=(0.45, 1.0, 1.0), jitter=0.2, rng=rng),
          lambda f: "soil_dark" if f.normal.x < 0.2 else "soil_mid")


def log_pile(a, seed=33, n_bottom=3, length=2.4, r=0.24, spread=1.0):
    """Штабель брёвен, как на макете: нижний ряд, на нём ряд меньше и верхнее бревно; концы вразнобой."""
    rng = random.Random(seed)
    z, rows, nb = r, [], n_bottom
    while nb > 0:
        rows.append((nb, z))
        z += r * 1.7
        nb -= 1
    for nb, zz in rows:
        for j in range(nb):
            y = (j - (nb - 1) / 2) * r * 2.02 * spread
            L = length * rng.uniform(0.86, 1.0)
            dx = rng.uniform(-0.15, 0.15)
            rr = r * rng.uniform(0.9, 1.06)
            ang = math.radians(rng.uniform(-4, 4))
            hx, hy = math.cos(ang) * L / 2, math.sin(ang) * L / 2
            _log(a, (dx - hx, y - hy, zz), (dx + hx, y + hy, zz), rr, rng, moss=1 if rng.random() < 0.3 else 0)


def chop_block(a, seed=35):
    """Колода для колки дров: широкий пень с кольцами, в нём топор, рядом поленья."""
    rng = random.Random(seed)
    a.add(p_cyl(0.36, 0.32, 0.5, 9, loc=(0, 0, -0.06)), lambda f: "wood_pale" if f.normal.z > 0.9 else BARK(f))
    a.add(p_cyl(0.2, 0.2, 0.02, 9, loc=(0, 0, 0.44)), "wood_light")
    for j in range(4):                                    # корни
        ang = math.tau * j / 4 + rng.uniform(-0.3, 0.3)
        SA._limb(a, (math.cos(ang) * 0.22, math.sin(ang) * 0.22, 0.12),
                 (math.cos(ang) * 0.55, math.sin(ang) * 0.55, -0.06), 0.1, 0.03, "bark", seg=5)
    # топор: топорище наклонно, обух в колоде
    h0, h1 = Vector((0.04, 0.0, 0.44)), Vector((0.42, -0.1, 0.98))
    SA._limb(a, h0, h1, 0.035, 0.03, "wood_light", seg=6)
    a.add(p_box((0.1, 0.05, 0.22), loc=(0.02, 0.0, 0.47), rot=(0, -28, -10), bevel=0.012),
          by_normal("iron_light", "iron", "iron_dark", 0.5))
    for j in range(3):                                    # поленья
        ang = rng.uniform(0, math.tau)
        c = Vector((math.cos(ang) * 0.62, math.sin(ang) * 0.62, 0.09))
        _log(a, c - Vector((0.18, 0.05, 0)), c + Vector((0.18, 0.05, 0)), 0.085, rng, seg=6)


# ---------------------------------------------------------------------------------------
# лагерь охотников
# ---------------------------------------------------------------------------------------
def leanto(a, seed=41):
    """Охотничий шалаш, как на макете: шалаш-«домик» вдоль Y, вход — фронтон к -Y; на фронтоне жерди «козел»
    крест-накрест торчат над коньком, скаты — настил из коры и брёвна поверх, задний фронтон закрыт,
    внутри — подстилка из шкур. Пивот — земля под серединой."""
    rng = random.Random(seed)
    L, H, half = 2.5, 1.9, 1.1                           # длина по Y, высота конька, полуширина основания по X
    slope = math.atan2(H, half)
    for sy in (-1, 1):                                    # козлы на фронтонах
        y = sy * (L / 2 - 0.08)
        for sx in (-1, 1):
            p0 = Vector((sx * (half + 0.08), y + rng.uniform(-0.04, 0.04), -0.1))
            p1 = Vector((-sx * 0.36, y, H + 0.45))
            SA._limb(a, p0, p1, 0.08, 0.065, BARK, seg=6)
    SA._limb(a, (0, -L / 2 - 0.3, H), (0, L / 2 + 0.3, H + 0.03), 0.09, 0.08, BARK, seg=6)          # конёк
    Ls = math.hypot(H, half) + 0.12
    for sx in (-1, 1):                                    # скаты: настил коры и брёвна поверх
        ang = -(90 - math.degrees(slope)) * sx           # локальная Z плиты — вверх по скату
        c = Vector((sx * half / 2, 0.0, H / 2 - 0.02))
        a.add(p_box((0.08, L - 0.05, Ls), loc=c, rot=(0, ang, 0), bevel=0.02), "bark_dark")
        for j in range(5):
            y = -L / 2 + 0.2 + (L - 0.4) * j / 4 + rng.uniform(-0.05, 0.05)
            p0 = Vector((sx * (half + 0.16), y, -0.08))
            p1 = Vector((sx * 0.02, y + rng.uniform(-0.06, 0.06), H + 0.08 + rng.uniform(-0.04, 0.08)))
            off = Vector((sx * math.sin(slope), 0.0, math.cos(slope))) * 0.07
            SA._limb(a, p0 + off, p1 + off, 0.075, 0.062, "bark" if j % 2 else "wood_mid", seg=5)
    a.add(p_prism([(-half + 0.12, -0.05), (half - 0.12, -0.05), (0.0, H - 0.12)], 0.1, loc=(0, L / 2 - 0.14, 0)),
          "bark_dark")                                                                              # задний фронтон
    for y, w, col in ((0.45, 1.0, "hide"), (-0.25, 0.85, "hide_dark")):                             # подстилка
        a.add(p_box((half * 1.3 * w, 0.9, 0.07), loc=(rng.uniform(-0.08, 0.08), y, 0.02),
                    rot=(0, 0, rng.uniform(-8, 8)), bevel=0.03), col)
    for sy in (-1, 1):                                    # обвязка у козел
        a.add(p_cyl(0.11, 0.11, 0.07, 6, loc=(0.0, sy * (L / 2 - 0.08), H - 0.05)), "rope")


def hide_rack(a, seed=43):
    """Рама для сушки шкуры: две стойки, перекладина, растянутая на жердях шкура, связки верёвкой."""
    rng = random.Random(seed)
    for sx in (-1, 1):
        SA._limb(a, (sx * 0.72, 0, -0.1), (sx * 0.66, 0.02, 1.62), 0.06, 0.05, BARK, seg=6)
    SA._limb(a, (-0.86, 0.02, 1.52), (0.86, 0.02, 1.56), 0.05, 0.045, "bark", seg=6)
    SA._limb(a, (-0.7, -0.02, 0.42), (0.7, -0.02, 0.4), 0.04, 0.04, "bark", seg=5)
    # шкура: лоскут на раме с вытянутыми «лапами»
    a.add(p_box((1.05, 0.05, 0.92), loc=(0, -0.03, 0.96), rot=(4, 0, 0), bevel=0.03),
          lambda f: "hide" if f.normal.y < -0.5 else "hide_dark")
    for sx in (-1, 1):
        for z in (0.55, 1.36):
            a.add(p_box((0.2, 0.045, 0.12), loc=(sx * 0.58, -0.03, z), rot=(0, sx * 25, 0), bevel=0.015), "hide")
            a.add(p_cyl(0.035, 0.035, 0.14, 5, loc=(sx * 0.67, -0.02, z - 0.07)), "rope")


def bucket(a, seed=45, water=True):
    """Деревянное ведро с обручами и дужкой."""
    a.add(p_cyl(0.2, 0.24, 0.36, 9, loc=(0, 0, -0.02)), lambda f: "wood_light" if f.normal.z > 0.9 else "wood_mid")
    for z in (0.06, 0.26):
        a.add(p_cyl(0.236 - z * 0.1, 0.232 - z * 0.1, 0.035, 9, loc=(0, 0, z)), "iron_dark")
    if water:
        a.add(p_cyl(0.205, 0.205, 0.02, 9, loc=(0, 0, 0.3)), "water")
    SA._limb(a, (-0.23, 0, 0.3), (0.0, 0, 0.5), 0.016, 0.016, "iron_dark", seg=4)
    SA._limb(a, (0.0, 0, 0.5), (0.23, 0, 0.3), 0.016, 0.016, "iron_dark", seg=4)


TORCH_TOP = SA.TORCH_TOP


def torch(a, seed=21):
    SA.torch(a, seed)


# ---------------------------------------------------------------------------------------
# ограда, мостик, навес
# ---------------------------------------------------------------------------------------
FENCE_SEG = 1.25


def fence_post(a, seed=47):
    """Столб жердевой ограды: толстый тёсаный кол с плоским верхом, у земли — кочка травы."""
    rng = random.Random(seed)
    a.add(p_box((0.17, 0.17, 0.98), loc=(0, 0, 0.4), rot=(rng.uniform(-2, 2), rng.uniform(-2, 2), rng.uniform(-8, 8)),
                bevel=0.035), lambda f: "wood_pale" if f.normal.z > 0.9 else "wood_mid")


def rail_fence(a, seed=49):
    """Сегмент жердевой ограды, как на макете: столб в начале (x = -FENCE_SEG/2) и две жерди-бревна до
    следующего столба, нижняя чуть провисает."""
    rng = random.Random(seed)
    fence_post(SA._Sub(a, (-FENCE_SEG / 2, 0, 0)), seed)
    for z, sag in ((0.36, 0.03), (0.7, 0.0)):
        p0 = Vector((-FENCE_SEG / 2 + 0.02, -0.11, z))
        p1 = Vector((FENCE_SEG / 2 + 0.02, -0.11, z + rng.uniform(-0.03, 0.03)))
        m = p0.lerp(p1, 0.5) - Vector((0, 0, sag))
        SA._limb(a, p0, m, 0.07, 0.065, "bark", seg=6)
        SA._limb(a, m, p1, 0.065, 0.065, "bark", seg=6)


BRIDGE_L = 3.4                                            # пролёт мостика вдоль X


def plank_bridge(a, seed=51):
    """Дощатый мостик через ручей, как на макете: настил из досок поперёк на двух бревенчатых лагах, по краям —
    перила из жердей на четырёх столбах, опоры уходят в берега. Пивот — верх настила по центру."""
    rng = random.Random(seed)
    W = 1.4
    n = 11
    pitch = BRIDGE_L / n
    for k in range(n):
        x = -BRIDGE_L / 2 + pitch * (k + 0.5)
        a.add(p_box((pitch - 0.04, W + rng.uniform(-0.1, 0.1), 0.08), loc=(x, rng.uniform(-0.04, 0.04), -0.04),
                    rot=(rng.uniform(-1.5, 1.5), 0, rng.uniform(-3, 3)), bevel=0.012),
              ("wood_mid", "wood_light", "wood_mid2")[k % 3])
    for sy in (-1, 1):                                    # лаги
        _log(a, (-BRIDGE_L / 2 - 0.2, sy * 0.45, -0.17), (BRIDGE_L / 2 + 0.2, sy * 0.45, -0.17), 0.1, rng, seg=7)
    for sx in (-1, 1):                                    # столбы перил и опоры в берег
        for sy in (-1, 1):
            x, y = sx * (BRIDGE_L / 2 - 0.25), sy * (W / 2 + 0.06)
            a.add(p_box((0.14, 0.14, 1.75), loc=(x, y, 0.88 - 0.85), bevel=0.03),
                  lambda f: "wood_pale" if f.normal.z > 0.9 else "wood_dark")
    for sy in (-1, 1):                                    # перила: жердь поверху и косая внизу
        y = sy * (W / 2 + 0.06)
        SA._limb(a, (-BRIDGE_L / 2 + 0.1, y, 0.78), (BRIDGE_L / 2 - 0.1, y, 0.8), 0.05, 0.05, "bark", seg=6)
        SA._limb(a, (-BRIDGE_L / 2 + 0.3, y, 0.15), (BRIDGE_L / 2 - 0.3, y, 0.42), 0.04, 0.04, "bark", seg=5)


def stall(a, seed=53):
    """Торговый навес лагеря гоблинов, как на макете: полотно скатом на четырёх столбах (высокая сторона — к -Y,
    к полю), бахрома спереди, прилавок с товаром (шкуры, горшки), под навесом бочонок и мешок.
    Пивот — земля под серединой."""
    import build_vitaria as V
    rng = random.Random(seed)
    W, D = 2.6, 1.8
    H0, H1 = 2.3, 1.75
    yf, yb = -D / 2, D / 2
    xs = W / 2 - 0.08
    for sx in (-1, 1):
        for y, h in ((yf, H0), (yb, H1)):
            SA._limb(a, (sx * xs, y, -0.1), (sx * xs + rng.uniform(-0.03, 0.03), y, h + 0.12), 0.07, 0.06, BARK, seg=6)
    for y, z in ((yf, H0), (yb, H1)):
        SA._limb(a, (-W / 2 - 0.15, y, z), (W / 2 + 0.15, y, z), 0.055, 0.055, "bark", seg=6)
    y0, z0 = yf - 0.22, H0 + 0.07
    y1, z1 = yb + 0.35, H1 - 0.2
    L = math.hypot(y1 - y0, z1 - z0)
    ang = math.degrees(math.atan2(z1 - z0, y1 - y0))
    strips = ((-W / 2 - 0.2, -W / 6, 0.0, "canvas"), (-W / 6, W / 6, -0.05, "canvas_dark"), (W / 6, W / 2 + 0.2, 0.0, "canvas"))
    for xa, xb, sag, col in strips:
        a.add(p_box((xb - xa + 0.01, L, 0.045), loc=((xa + xb) / 2, (y0 + y1) / 2, (z0 + z1) / 2 + sag),
                    rot=(ang, 0, 0), bevel=0.012),
              lambda f, c=col: "canvas_dark" if f.normal.z < -0.3 else c)
    for j in range(7):                                    # бахрома
        x = -W / 2 + (W + 0.4) * (j + 0.5) / 7 - 0.2
        a.add(p_box((W / 7 - 0.05, 0.035, 0.24), loc=(x, y0 - 0.01, z0 - 0.14), rot=(-6, 0, 0), bevel=0.01),
              "canvas_dark" if j % 2 else "burlap")
    tz = 0.86                                             # прилавок
    a.add(p_box((W - 0.4, 0.55, 0.08), loc=(0, yf + 0.45, tz), bevel=0.02), "wood_light")
    a.add(p_box((W - 0.5, 0.06, 0.62), loc=(0, yf + 0.2, tz - 0.36), bevel=0.02), "wood_mid")
    for sx in (-1, 1):
        a.add(p_box((0.08, 0.5, 0.84), loc=(sx * (W / 2 - 0.3), yf + 0.45, tz / 2 - 0.02), bevel=0.015), "wood_dark")
    a.add(p_box((0.55, 0.42, 0.12), loc=(-0.55, yf + 0.45, tz + 0.1), rot=(0, 0, 8), bevel=0.04), "hide")
    a.add(p_box((0.5, 0.38, 0.1), loc=(-0.5, yf + 0.47, tz + 0.21), rot=(0, 0, -6), bevel=0.04), "hide_dark")
    for j, x in enumerate((0.2, 0.55)):                   # горшки
        a.add(p_cyl(0.12, 0.09, 0.2, 8, loc=(x, yf + 0.42, tz + 0.04)),
              lambda f: "black" if f.normal.z > 0.9 else ("copper_dark" if j else "soil_mid"))
    V.build_crate(a, 0.5, loc=(0.75, yb - 0.4, 0.0))
    V.build_sack(a, loc=(-0.85, yb - 0.45, 0.0), seed=4)


# ---------------------------------------------------------------------------------------
# грибы, цветы
# ---------------------------------------------------------------------------------------
def _mushroom(a, x, y, h, r, rng, lean=(0.0, 0.0)):
    top = Vector((x + lean[0] * h, y + lean[1] * h, h))
    SA._limb(a, (x, y, -0.05), top, r * 0.32, r * 0.26, "cream", seg=6)
    a.add(p_ico(r, 2 if r > 0.2 else 1, loc=top + Vector((0, 0, -r * 0.08)), scl=(1.0, 1.0, 0.62), cut=0.0),
          "leaf_autumn")
    a.add(p_cyl(r * 0.96, r * 0.4, r * 0.12, 8, loc=top + Vector((0, 0, -r * 0.13))), "wood_pale")
    for j in range(4 if r > 0.2 else (2 if r > 0.12 else 0)):         # белые пятна на шляпке
        ang = rng.uniform(0, math.tau)
        d = r * rng.uniform(0.15, 0.62)
        zz = math.sqrt(max(0.0, 1.0 - (d / r) ** 2)) * r * 0.62
        a.add(p_cyl(r * 0.13, r * 0.11, 0.025, 6, loc=top + Vector((math.cos(ang) * d, math.sin(ang) * d, zz - r * 0.1))),
              "flower_w")


def mushrooms(a, seed=55, big=True):
    """Грибы, как на макете: два–три крупных оранжевых гриба с белыми пятнами и мелочь у ножек."""
    rng = random.Random(seed)
    s = 1.0 if big else 0.55
    _mushroom(a, 0.0, 0.0, 0.5 * s, 0.26 * s, rng, (0.06, 0.0))
    _mushroom(a, 0.32 * s, -0.12 * s, 0.32 * s, 0.17 * s, rng, (0.12, -0.05))
    _mushroom(a, -0.2 * s, -0.22 * s, 0.2 * s, 0.11 * s, rng, (-0.1, -0.08))


def _daisy(a, x, y, h, r, col, rng):
    SA._limb(a, (x, y, -0.03), (x, y, h), 0.012, 0.01, "blade", seg=3)
    b = bmesh.new()
    c = b.verts.new((x, y, h + 0.015))
    k = 12
    rim = []
    for j in range(k):
        ang = math.tau * j / k + rng.uniform(-0.05, 0.05)
        rr = r if j % 2 == 0 else r * 0.55
        rim.append(b.verts.new((x + math.cos(ang) * rr, y + math.sin(ang) * rr, h - 0.01 * (j % 2))))
    low = b.verts.new((x, y, h - 0.03))
    for j in range(k):
        b.faces.new((c, rim[j], rim[(j + 1) % k]))
        b.faces.new((low, rim[(j + 1) % k], rim[j]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    b.normal_update()
    a.add(b, lambda f: col if f.normal.z > 0 else "blade_dark")
    a.add(p_cyl(r * 0.36, r * 0.3, 0.035, 6, loc=(x, y, h + 0.005)), "flower_y" if col != "flower_y" else "wood_yellow")


def flowers(a, seed=57, col="flower_w", n=3):
    """Цветы, как на макете: крупные ромашки (или жёлтые) на стеблях и пучок травы у основания."""
    rng = random.Random(seed)
    for j in range(n):
        ang = math.tau * j / n + rng.uniform(-0.4, 0.4)
        d = 0.0 if j == 0 else rng.uniform(0.1, 0.17)
        _daisy(a, math.cos(ang) * d, math.sin(ang) * d, rng.uniform(0.2, 0.32), rng.uniform(0.08, 0.11), col, rng)
    for j in range(5):
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0.0, 0.12)
        p0 = Vector((math.cos(ang) * d, math.sin(ang) * d, -0.02))
        p1 = p0 + Vector((math.cos(ang) * 0.06, math.sin(ang) * 0.06, rng.uniform(0.12, 0.2)))
        SA._limb(a, p0, p1, 0.02, 0.0, "blade" if j % 2 else "blade_dark", seg=3)


ASSETS = [
    ("Forest", "Forest_Spruce_A", "Ель высокая", lambda a: spruce(a, 1, 6.4, 7, 1.0, 8, 0.32)),
    ("Forest", "Forest_Spruce_B", "Ель", lambda a: spruce(a, 2, 5.0, 6, 1.0, 8, 0.32)),
    ("Forest", "Forest_Spruce_C", "Ёлка малая", lambda a: spruce(a, 3, 2.9, 4, 1.05, 7, 0.3)),
    ("Forest", "Forest_Spruce_D", "Ель узкая, с наклоном", lambda a: spruce(a, 4, 5.8, 7, 0.82, 8, 0.36, (0.03, 0.0))),
    ("Forest", "Forest_Pine_A", "Сосна", lambda a: pine(a, 5, 7.2, 1.0, (0.03, 0.01))),
    ("Forest", "Forest_Tree_A", "Старый дуб с корнями", lambda a: round_tree(a, 7, 6.2, 1.12, 1.5, 9, True)),
    ("Forest", "Forest_Tree_B", "Лиственное дерево", lambda a: round_tree(a, 8, 4.6, 1.0, 1.0, 7)),
    ("Forest", "Forest_Tree_C", "Деревце", lambda a: round_tree(a, 9, 3.0, 0.95, 0.8, 5)),
    ("Forest", "Forest_Bush_A", "Куст", lambda a: bush(a, 11, 1.0, 4)),
    ("Forest", "Forest_Bush_B", "Куст тёмный", lambda a: bush(a, 12, 1.15, 4, "leaf_dark")),
    ("Forest", "Forest_Fern", "Папоротник", fern),
    ("Forest", "Forest_Boulder_A", "Валун во мху, большой", lambda a: boulder(a, 21, 1.15, 2, 0.74, 2)),
    ("Forest", "Forest_Boulder_B", "Валун во мху", lambda a: boulder(a, 22, 0.75, 2, 0.7, 2)),
    ("Forest", "Forest_Rock_A", "Камень", lambda a: rock(a, 25, 0.42, 2)),
    ("Forest", "Forest_Rock_B", "Камешки", lambda a: rock(a, 26, 0.2, 3)),
    ("Forest", "Forest_FallenLog", "Поваленный ствол с выворотнем", fallen_log),
    ("Forest", "Forest_LogPile_A", "Штабель брёвен", lambda a: log_pile(a, 33, 3, 2.4, 0.24)),
    ("Forest", "Forest_LogPile_B", "Брёвна у края", lambda a: log_pile(a, 34, 2, 2.8, 0.27)),
    ("Forest", "Forest_ChopBlock", "Колода с топором", chop_block),
    ("Forest", "Forest_LeanTo", "Охотничий шалаш", leanto),
    ("Forest", "Forest_HideRack", "Рама со шкурой", hide_rack),
    ("Forest", "Forest_Bucket", "Ведро", bucket),
    ("Forest", "Forest_Torch", "Факел на шесте", torch),
    ("Forest", "Forest_FencePost", "Столб жердевой ограды", fence_post),
    ("Forest", "Forest_Fence", "Жердевая ограда, сегмент 1.25 м", rail_fence),
    ("Forest", "Forest_Bridge", "Дощатый мостик через ручей", plank_bridge),
    ("Forest", "Forest_Stall", "Торговый навес", stall),
    ("Forest", "Forest_Mushrooms", "Грибы", mushrooms),
    ("Forest", "Forest_Mushrooms_S", "Грибы мелкие", lambda a: mushrooms(a, 56, False)),
    ("Forest", "Forest_Flowers_W", "Ромашки", lambda a: flowers(a, 57, "flower_w", 3)),
    ("Forest", "Forest_Flowers_Y", "Жёлтые цветы", lambda a: flowers(a, 58, "flower_y", 3)),
    # плитки поля биома: геометрия и UV — как у Hex_Tile_A/B/C, цвет — палитра Vitaria_Palette_Forest (мох)
    ("Forest", "Hex_Tile_Forest_A", "Гекс-плитка леса A", TL.tile_plain(11, base=0.5)),
    ("Forest", "Hex_Tile_Forest_B", "Гекс-плитка леса B (кочки)", TL.tile_tufts),
    ("Forest", "Hex_Tile_Forest_C", "Гекс-плитка леса C (камни)", TL.tile_pebbles),
    ("Preview", "Preview_Troll", "Тролль-заглушка (превью)", SA.preview_troll),
    ("Preview", "Preview_Goblin", "Гоблин-заглушка (превью)", SA.preview_goblin),
]
