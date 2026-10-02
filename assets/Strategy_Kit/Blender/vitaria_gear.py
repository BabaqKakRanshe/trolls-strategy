"""
Снаряжение Vitaria: оружие, броня, шлем и щиты — модели категории Resources (Kit_Resources в .blend,
FBX Res_*.fbx в Unity/Assets/Vitaria/Models/Resources) и иконки (render_icons.py берёт меши кита по имени).

У каждого предмета две версии: обычная и зачарованная (enchanted=True). Зачарование одним языком на всё
снаряжение: светящиеся голубые руны (rune), фиолетовый камень в золотой оправе (arcane), кристальные
клинки, острия и навершия (crystal*), золото фурнитуры. Руны и камень светятся: при выгрузке их грани
уходят во второй слот Vitaria_FX (build_vitaria.split_emissive).

Соглашения кита: 1 юнит = 1 м, пивот внизу по центру, лицом к -Y. Оружие стоит на навершии или пятке
древка (в иконке наклонено, как iron-sword), броня — низом на z = 0, шлем — краем нащёчников, щиты — на
кромке. Мечи, кирасы и деревянный щит перенесены из vitaria_icons.py, их геометрия не менялась (иконки те
же); боевой топор — из vitaria_resources.py.
"""
import math
import random

import bmesh
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, by_normal
from vitaria_buildings.common import Frame, sword, round_shield, breastplate, crystal, heater_outline

RUNE, GEM = "rune", "arcane"


# =========================================================================================
# зачарование: общие детали
# =========================================================================================
def gem(a, fr, r=0.045, loc=(0, 0, 0), rot=(0, 0, 0), bezel="gold"):
    """Светящийся камень arcane в золотой оправе, лицом к -Y рамы."""
    s = fr.sub(loc, rot=rot)
    s.cyl(a, r * 1.45, r * 1.45, r * 0.55, 8, loc=(0, r * 0.25, 0), rot=(90, 0, 0), col=bezel)
    s.ico(a, r, loc=(0, -r * 0.25, 0), scl=(1, 0.72, 1), col=GEM)


def decal(a, fr, w, h, loc=(0, 0, 0), col=RUNE, rot=(0, 0, 0)):
    """Плоская наклейка w x h в плоскости XZ рамы лицом к -Y: заклёпки, стежки, руны на ровной грани —
    два треугольника вместо двенадцати у бруска. Видна только спереди: ставить на грань тела."""
    s = fr.sub(loc, rot=rot)
    b = bmesh.new()
    b.faces.new([b.verts.new(s.at((x, 0, z))) for x, z in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2),
                                                          (-w / 2, h / 2))])
    b.normal_update()
    a.add(b, col)


def rune_ring(a, fr, r, n=12, y=0.0, dash=(0.07, 0.024), col=RUNE):
    """Круг рун в плоскости XZ рамы: n штрихов по касательной, лицом к -Y (наклейки)."""
    for i in range(n):
        phi = math.tau * (i + 0.5) / n
        decal(a, fr, dash[0], dash[1], (math.cos(phi) * r, y, math.sin(phi) * r), col=col,
              rot=(0, -(math.degrees(phi) + 90), 0))


def band_rivets(a, fr, r, z, n, size=0.026, col="iron_light", phase=0.5):
    """Заклёпки по цилиндрическому поясу радиуса r на высоте z: наклейки лицом наружу."""
    for i in range(n):
        phi = math.tau * (i + phase) / n
        decal(a, fr.sub((math.cos(phi) * (r + 0.002), math.sin(phi) * (r + 0.002), z), rz=math.degrees(phi) + 90),
              size, size, col=col)


def face_grid(a, corners, rows, cols, colfn, lift=0.004, outward=None):
    """Сетка квадов на грани тела (bl, br, tr, tl — точки в осях ассета, ряды идут от bl-br к tl-tr):
    у каждой ячейки свой swatch colfn(row, col) — кольца кольчуги без лишних объёмов, два треугольника
    на ячейку. lift — отступ над гранью наружу; outward — точка внутри тела, от неё считается «наружу»."""
    bl, br, tr, tl = [Vector(c) for c in corners]
    n = (br - bl).cross(tl - bl).normalized()
    if outward is not None and n.dot(bl - Vector(outward)) < 0:
        n = -n
    off = n * lift
    b = bmesh.new()
    grid = {}
    for r in range(rows + 1):
        v = r / rows
        for c in range(cols + 1):
            u = c / cols
            grid[r, c] = b.verts.new(bl.lerp(br, u).lerp(tl.lerp(tr, u), v) + off)
    cols_of = {}
    for r in range(rows):
        for c in range(cols):
            f = b.faces.new((grid[r, c], grid[r, c + 1], grid[r + 1, c + 1], grid[r + 1, c]))
            f.normal_update()
            if f.normal.dot(n) < 0:
                f.normal_flip()
            cols_of[f] = colfn(r, c)
    a.add(b, lambda f: cols_of[f])


# =========================================================================================
# мечи
# =========================================================================================
def iron_sword(a, enchanted=False):
    """Железный меч (iron-sword); зачарованный — enchanted_sword."""
    if enchanted:
        return enchanted_sword(a)
    sword(a, Frame((0, 0, 0)), L=1.0, guard="iron_light", grip="leather", blade="steel", pommel="iron_light")


def enchanted_sword(a):
    """Зачарованный железный меч (enchanted-sword): кристальный клинок, руна по долу, камень в гарде."""
    fr = Frame((0, 0, 0))
    sword(a, fr, L=1.05, guard="gold", grip="leather_dark", blade="crystal_light", pommel="crystal")
    body = (1.05 - 0.32) * 0.80
    fr.box(a, (0.03, 0.05, body * 0.8), (0, -0.004, 0.30 + body * 0.46), col="rune", bevel=0.0)
    fr.ico(a, 0.045, loc=(0, -0.03, 0.285), col="arcane", scl=(1, 0.7, 1))        # камень в гарде


def rusty_sword(a, enchanted=False):
    """Ржавый меч: тёмный клинок в рыжих пятнах, щербина на лезвии, обмотка вместо рукояти.
    Зачарованный: та же ржавчина, но сквозь неё светится разорванная руна, навершие — кристалл,
    в гарде камень."""
    fr = Frame((0, 0, 0))
    sword(a, fr, L=0.95, guard="iron_dark", grip="burlap_dark", blade="iron",
          pommel="crystal" if enchanted else "iron_dark")
    rng = random.Random(4)
    body = (0.95 - 0.32) * 0.8
    for k in range(6):
        z = 0.34 + rng.uniform(0.0, body)
        fr.box(a, (rng.uniform(0.04, 0.08), 0.05, rng.uniform(0.04, 0.09)), (rng.uniform(-0.03, 0.03), -0.003, z),
               col=rng.choice(["iron_ore_vein", "copper_dark", "leather"]), bevel=0.0)
    fr.box(a, (0.05, 0.06, 0.06), (0.06, 0.0, 0.3 + body * 0.62), col="black", bevel=0.0)     # щербина
    if enchanted:
        # руна по долу кусками: светится в трещинах ржавчины (обе стороны клинка)
        for z0, h in ((0.36, 0.09), (0.49, 0.06), (0.58, 0.11), (0.73, 0.05)):
            fr.box(a, (0.026, 0.058, h), (0.0, 0.0, z0 + h / 2), col=RUNE, bevel=0.0)
        fr.ico(a, 0.042, loc=(0, -0.034, 0.285), col=GEM, scl=(1, 0.7, 1))


def steel_sword(a, enchanted=False, L=1.15):
    """Стальной меч: длиннее и шире железного, синеватая сталь с тёмным долом, крестовина с отогнутыми к
    клинку концами, рукоять в тёмной коже с золотой проволокой. Зачарованный: кристальный клинок, руна
    по долу, золотая гарда с кристальными концами, камень в перекрестье."""
    E = enchanted
    fr = Frame((0, 0, 0))
    # кромки светлые, широкий дол синий: у железного меча клинок светлый целиком
    blade, band, fuller = ("crystal_light", "crystal", RUNE) if E else ("steel", "steel_mid", "steel_dark")
    metal = "gold" if E else "steel_dark"
    fr.ico(a, 0.06, loc=(0, 0, 0.05), scl=(1, 0.82, 1), col="crystal" if E else "steel_dark")     # навершие
    fr.cyl(a, 0.034, 0.03, 0.035, 6, loc=(0, 0, 0.09), col="gold")
    fr.box(a, (0.06, 0.06, 0.22), (0, 0, 0.215), col="leather_dark", bevel=0.012)                # рукоять
    for z in (0.16, 0.27):
        fr.box(a, (0.068, 0.068, 0.022), (0, 0, z), col="gold", bevel=0.0)
    zg = 0.345
    fr.box(a, (0.13, 0.085, 0.08), (0, 0, zg), col=metal, bevel=0.02)                           # перекрестье
    for sx in (-1, 1):
        q = fr.sub((sx * 0.055, 0, zg), rot=(0, -sx * 14, 0))
        q.box(a, (0.17, 0.062, 0.055), (sx * 0.085, 0, 0), col=metal, bevel=0.015)
        q.ico(a, 0.036, loc=(sx * 0.18, 0, 0), col="crystal_light" if E else "gold")
    fr.box(a, (0.10, 0.046, 0.06), (0, 0, zg + 0.07), col="crystal" if E else "steel_mid", bevel=0.006)  # пята
    z0 = zg + 0.10
    bl = L - z0
    body = bl * 0.80
    w = 0.13
    fr.taper(a, (w, 0.042), (w * 0.84, 0.038), body, loc=(0, 0, z0), col=blade)
    fr.taper(a, (w * 0.84, 0.038), (0.012, 0.012), bl - body, loc=(0, 0, z0 + body), col=blade)
    fr.taper(a, (w * 0.56, 0.048), (w * 0.46, 0.044), body * 0.92, loc=(0, 0, z0), col=band)
    fr.box(a, (0.022, 0.054, body * 0.78), (0, 0, z0 + body * 0.43), col=fuller, bevel=0.0)
    if E:
        gem(a, fr, 0.04, loc=(0, -0.046, zg))


# =========================================================================================
# древковое и ударное
# =========================================================================================
def battle_axe(a, L=1.10, enchanted=False):
    """Боевой топор: стоит на пятке топорища (пивот внизу); в иконке наклонён на 38°, как iron-sword.
    Зачарованный: кристальные лезвия со светлой кромкой, по руне на каждом, камень на проухе."""
    E = enchanted
    fr = Frame((0, 0, 0))
    fr.cyl(a, 0.042, 0.036, L - 0.05, 7, loc=(0, 0, 0.04), col="wood_dark")
    fr.cyl(a, 0.055, 0.05, 0.06, 7, loc=(0, 0, 0.0), col="iron_dark")                  # пятка
    for z in (0.12, 0.22, 0.32):                                                       # обмотка рукояти
        fr.cyl(a, 0.05, 0.05, 0.06, 7, loc=(0, 0, z), col="leather_dark" if E else "leather")
    zc = L - 0.20
    fr.box(a, (0.13, 0.11, 0.24), (0, 0, zc), col="iron_dark", bevel=0.02)            # проух
    fr.cyl(a, 0.04, 0.0, 0.16, 6, loc=(0, 0, zc + 0.12), col="crystal_light" if E else "iron_light")  # пика
    # два лезвия-полумесяца: сталь, светлая кромка, у проуха тёмная вставка с золотой заклёпкой
    blade = [(0.05, -0.08), (0.17, -0.13), (0.30, -0.24), (0.36, -0.10), (0.37, 0.0), (0.36, 0.10), (0.30, 0.24),
             (0.17, 0.13), (0.05, 0.08)]
    edge = [(0.30, -0.24), (0.37, -0.20), (0.41, -0.10), (0.42, 0.0), (0.41, 0.10), (0.37, 0.20), (0.30, 0.24),
            (0.36, 0.10), (0.37, 0.0), (0.36, -0.10)]
    for sx in (1, -1):
        mirror = (lambda pts: pts) if sx > 0 else (lambda pts: [(-x, z) for x, z in reversed(pts)])
        fr.prism(a, mirror(blade), 0.06, loc=(0, 0, zc), col="crystal" if E else "steel")
        fr.prism(a, mirror(edge), 0.045, loc=(0, 0, zc), col="crystal_light" if E else "iron_light")
        fr.box(a, (0.08, 0.075, 0.14), (sx * 0.10, 0, zc), col="crystal_dark" if E else "iron", bevel=0.0)
        fr.cyl(a, 0.025, 0.025, 0.10, 6, loc=(sx * 0.10, 0.05, zc), rot=(90, 0, 0), col="gold")
        if E:
            fr.box(a, (0.035, 0.066, 0.16), (sx * 0.25, 0, zc), col=RUNE, bevel=0.0)
    if E:
        gem(a, fr, 0.04, loc=(0, -0.058, zc + 0.05))


def spear(a, enchanted=False, L=1.75):
    """Копьё: ясеневое древко с подтоком и кожаным хватом, втулка с обмоткой, багряная кисть под
    наконечником-листом. Зачарованный: кристальный наконечник с руной по ребру, золотая втулка, камень,
    кисть из кристальных лент."""
    E = enchanted
    fr = Frame((0, 0, 0))
    fr.cyl(a, 0.042, 0.036, 0.07, 7, loc=(0, 0, 0), col="iron_dark")                  # подток
    fr.cyl(a, 0.032, 0.028, L - 0.40, 7, loc=(0, 0, 0.06), col="wood_dark" if E else "wood_mid")
    for z in (0.56, 0.64, 0.72):                                                       # хват
        fr.cyl(a, 0.039, 0.039, 0.06, 7, loc=(0, 0, z), col="leather")
    zs = L - 0.42
    fr.cyl(a, 0.038, 0.048, 0.15, 7, loc=(0, 0, zs), col="gold" if E else "iron_dark")    # втулка
    fr.cyl(a, 0.052, 0.052, 0.04, 7, loc=(0, 0, zs + 0.01), col="gold" if E else "rope")
    for dx, ang, h in ((-0.035, 14, 0.17), (0.0, 2, 0.20), (0.035, -12, 0.16)):       # кисть
        fr.box(a, (0.036, 0.032, h), (dx, 0.0, zs - h / 2 + 0.01), rot=(0, ang, 0),
               col="crystal" if E else "cloth", bevel=0.0)
    zh = zs + 0.15
    leaf = [(x * 1.2, z * 1.2) for x, z in ((0.0, 0.0), (0.075, 0.06), (0.088, 0.12), (0.055, 0.25), (0.0, 0.37),
                                             (-0.055, 0.25), (-0.088, 0.12), (-0.075, 0.06))]
    fr.prism(a, leaf, 0.038, loc=(0, 0, zh - 0.01), col="crystal_light" if E else "steel")
    fr.box(a, (0.026, 0.054, 0.33), (0, 0, zh + 0.165), col=RUNE if E else "iron_light", bevel=0.0)
    if E:
        gem(a, fr, 0.034, loc=(0, -0.052, zs + 0.085))


def bow(a, enchanted=False, H=1.12):
    """Лук: рекурсивные плечи из клиньев (тоньше к концам, концы загнуты), кожаная рукоять, тетива, на
    полке — стрела с багряным оперением. Стоит на нижнем конце, в плоскости XZ, лицом к -Y.
    Зачарованный: тёмные плечи с рунами, кристаллы на концах, светящаяся тетива, кристальная стрела."""
    E = enchanted
    fr = Frame((0, 0, 0))
    zc = H / 2

    def spine(t):                       # t от -1 (низ) до 1 (верх): дуга к -X, концы загнуты к +X
        x = -0.16 * (1 - t * t) + 0.06 * max(0.0, abs(t) - 0.72) / 0.28
        return x, zc + t * H / 2

    n = 7
    pts = [spine(-1 + k / n) for k in range(2 * n + 1)]
    for k in range(2 * n):
        (x0, z0), (x1, z1) = pts[k], pts[k + 1]
        tm = abs((k + 0.5) / n - 1)                     # 0 у рукояти, 1 у концов
        th = 0.06 - 0.032 * tm
        seg = math.hypot(x1 - x0, z1 - z0)
        ang = math.degrees(math.atan2(x1 - x0, z1 - z0))
        s = fr.sub(((x0 + x1) / 2, 0, (z0 + z1) / 2), rot=(0, ang, 0))
        grip = tm < 0.15
        col = "leather" if grip else ("wood_dark" if (E or tm > 0.8) else "wood_mid")
        s.box(a, (th + (0.02 if grip else 0.0), 0.048 + (0.012 if grip else 0.0), seg + 0.014), col=col,
              bevel=0.0)
        if E and 0.3 < tm < 0.75:
            s.box(a, (0.016, 0.06, seg * 0.7), (-th / 2 - 0.002, 0, 0), col=RUNE, bevel=0.0)
    xt = spine(1.0)[0]
    for zt in (0.012, H - 0.012):                       # концы: роговые накладки или кристаллы
        if E:
            fr.ico(a, 0.034, loc=(xt + 0.008, 0, zt), scl=(0.9, 0.9, 1.4), col="crystal_light")
        else:
            fr.box(a, (0.04, 0.05, 0.05), (xt + 0.006, 0, zt), col="cream", bevel=0.01)
    fr.box(a, (0.015, 0.015, H - 0.05), (xt + 0.02, 0, zc), col=RUNE if E else "cream", bevel=0.0)  # тетива
    # стрела на полке: от тетивы через рукоять, остриё к -X
    ya, za = -0.045, zc + 0.03
    fr.box(a, (0.62, 0.018, 0.018), (xt - 0.27, ya, za), col="wood_dark" if E else "wood_light", bevel=0.0)
    head = [(0.0, -0.035), (0.0, 0.035), (-0.09, 0.0)]
    fr.prism(a, [(xt - 0.58 + x, z) for x, z in head], 0.02, loc=(0, ya, za), col="crystal_light" if E else "steel")
    for dz in (-1, 1):                                  # оперение
        fr.prism(a, [(xt - 0.02, 0.0), (xt - 0.16, 0.0), (xt - 0.06, dz * 0.05)][::dz], 0.012, loc=(0, ya, za),
                 col="crystal" if E else "cloth")
    if E:
        gem(a, fr, 0.03, loc=(spine(0.0)[0], -0.042, zc - 0.07))


def war_hammer(a, enchanted=False, L=1.05):
    """Боевой молот: длинная рукоять с лангетами и кожаной обмоткой, тяжёлый боёк из тёмного железа со
    стальными торцами и бандажом, шип сверху. Зачарованный: кристальные торцы, руны на бойке, золотой
    бандаж с камнем."""
    E = enchanted
    fr = Frame((0, 0, 0))
    fr.cyl(a, 0.046, 0.042, 0.06, 7, loc=(0, 0, 0), col="iron_dark")                  # пятка
    fr.cyl(a, 0.038, 0.034, L - 0.14, 7, loc=(0, 0, 0.05), col="wood_dark")
    for z in (0.10, 0.18, 0.26, 0.34):
        fr.cyl(a, 0.046, 0.046, 0.06, 7, loc=(0, 0, z), col="leather_dark" if E else "leather")
    for sy in (-1, 1):                                                                 # лангеты
        fr.box(a, (0.03, 0.02, 0.28), (0, sy * 0.036, L - 0.29), col="gold" if E else "iron_dark", bevel=0.0)
    zc = L - 0.08
    face = "crystal_light" if E else "steel"
    fr.box(a, (0.34, 0.16, 0.18), (0, 0, zc), col=by_normal("iron", "iron_dark", "iron_dark", 0.6), bevel=0.03)
    for sx in (-1, 1):                                                                 # торцы-бойки
        fr.box(a, (0.08, 0.21, 0.22), (sx * 0.20, 0, zc),
               col=lambda f, c=face: c if abs(f.normal.x) > 0.7 else ("iron" if f.normal.z > 0.6 else "iron_dark"),
               bevel=0.03)
    fr.box(a, (0.09, 0.19, 0.205), (0, 0, zc), col="gold" if E else "iron", bevel=0.02)  # бандаж
    fr.cyl(a, 0.045, 0.0, 0.13, 6, loc=(0, 0, zc + 0.10), col="crystal_light" if E else "iron_light")  # шип
    for sx in (-1, 1):                                                                 # заклёпки
        for sy in (-1, 1):
            decal(a, fr, 0.03, 0.03, (sx * 0.10, sy * 0.0815, zc + 0.035), col="gold" if E else "iron_light",
                  rot=(0, 0, 0 if sy < 0 else 180))
    if E:
        for sx in (-1, 1):
            fr.box(a, (0.026, 0.17, 0.11), (sx * 0.105, 0, zc - 0.02), col=RUNE, bevel=0.0)
        gem(a, fr, 0.038, loc=(0, -0.10, zc))


# =========================================================================================
# броня
# =========================================================================================
def patched_armor(a, enchanted=False):
    """Латаная броня: тусклая кираса, кожаные заплаты на заклёпках и верёвка вместо ремня.
    Зачарованный: заклёпки и швы заплат светятся рунами, камень на узле верёвки, кристалл на плече."""
    breastplate(a, Frame((0, 0, 0)), col="iron", trim="iron_dark")
    for (x, z, w, h, col) in ((-0.1, 0.25, 0.14, 0.12, "leather"), (0.12, 0.13, 0.12, 0.1, "burlap"),
                              (0.06, 0.34, 0.09, 0.08, "hide")):
        a.add(p_box((w, 0.03, h), loc=(x, -0.135, z), rot=(0, 6, 0), bevel=0.0), col)
        for dx in (-w / 2 + 0.02, w / 2 - 0.02):
            a.add(p_box((0.022, 0.02, 0.022), loc=(x + dx, -0.152, z + h / 2 - 0.02), bevel=0.0),
                  RUNE if enchanted else "iron_light")
        if enchanted:
            a.add(p_box((w - 0.03, 0.012, 0.014), loc=(x, -0.153, z - h / 2 + 0.018), rot=(0, 6, 0), bevel=0.0), RUNE)
    a.add(p_cyl(0.215, 0.2, 0.035, 10, loc=(0, 0, 0.08)), "rope")
    if enchanted:
        gem(a, Frame((0, 0, 0)), 0.04, loc=(-0.06, -0.218, 0.095))
        crystal(a, Frame((0.25, 0.0, 0.5), rot=(0, 22, 0)), r=0.045, h=0.17, glow_faces=False)


def iron_armor(a, enchanted=False):
    """Железная кираса с золотой каймой и кожаными ремнями. Зачарованная: руна по ребру, камень на
    груди, кристаллы на наплечниках."""
    # золотая кайма и кожаные ремни: одной серой кирасой она сливалась со слитками
    breastplate(a, Frame((0, 0, 0)), col="iron_light", trim="gold")
    for sx in (-1, 1):
        a.add(p_box((0.05, 0.03, 0.34), loc=(sx * 0.13, -0.13, 0.2), bevel=0.0), "leather")
    if enchanted:
        a.add(p_box((0.032, 0.014, 0.25), loc=(0, -0.141, 0.19), bevel=0.0), RUNE)
        gem(a, Frame((0, 0, 0)), 0.05, loc=(0, -0.142, 0.355))
        for sx in (-1, 1):
            crystal(a, Frame((sx * 0.26, 0.0, 0.5), rot=(0, sx * 24, 0)), r=0.042, h=0.17, glow_faces=False)


def leather_armor(a, enchanted=False):
    """Кожаный доспех: выделанная кираса со шнуровкой на груди, три внахлёст полосы на животе со
    стежкой, мягкие наплечники с ремешком, медная пряжка. Зачарованный: шнуровка и стежка светятся
    рунами, камень на вороте, кристальные клёпки на наплечниках."""
    E = enchanted
    fr = Frame((0, 0, 0))
    lea = by_normal("leather_light", "leather", "leather_dark", 0.5)
    fr.taper(a, (0.34, 0.22), (0.44, 0.25), 0.44, col=lea, bevel=0.03)
    fr.taper(a, (0.26, 0.04), (0.32, 0.04), 0.19, loc=(0, -0.112, 0.23), col="leather_light", bevel=0.012)  # нагрудник
    lace = RUNE if E else "rope"
    for zz in (0.27, 0.33, 0.39):                                                      # шнуровка крестом
        for s in (-1, 1):
            decal(a, fr, 0.10, 0.018, (0, -0.1325, zz), col=lace, rot=(0, s * 30, 0))
    for k, z in enumerate((0.07, 0.13, 0.19)):                                         # полосы внахлёст
        fr.box(a, (0.37 - k * 0.01, 0.035, 0.07), (0, -0.112 - 0.004 * (2 - k), z),
               col="leather" if k % 2 else "leather_light", bevel=0.012)
        for i in range(5):                                                             # стежка
            decal(a, fr, 0.035, 0.012, (-0.14 + i * 0.07, -0.1305 - 0.004 * (2 - k), z - 0.022),
                  col=RUNE if E else "hide_light")
    fr.box(a, (0.38, 0.24, 0.05), (0, 0, 0.025), col="leather_dark", bevel=0.012)       # пояс
    fr.box(a, (0.085, 0.02, 0.06), (0.06, -0.125, 0.025), col="gold" if E else "copper", bevel=0.008)
    for sx in (-1, 1):                                                                 # наплечники
        fr.ico(a, 0.12, loc=(sx * 0.245, 0, 0.42), scl=(1.1, 1.05, 0.72), col=lea, cut=-0.02)
        sh = fr.sub((sx * 0.30, 0, 0.375), rot=(0, sx * 22, 0))
        sh.box(a, (0.15, 0.23, 0.045), col="leather_dark", bevel=0.012)
        sh.box(a, (0.03, 0.25, 0.05), (sx * 0.035, 0, 0.01), col="leather", bevel=0.0)     # ремешок
        if E:
            for dy in (-0.06, 0.02):
                fr.ico(a, 0.022, loc=(sx * 0.25, dy, 0.505), col="crystal_light")
    fr.box(a, (0.22, 0.20, 0.05), (0, 0, 0.455), col="leather_dark", bevel=0.015)      # ворот
    fr.box(a, (0.13, 0.11, 0.008), (0, 0.005, 0.482), col="black", bevel=0.0)
    if E:
        gem(a, fr, 0.036, loc=(0, -0.112, 0.455))


def neck_hole(a, fr, r, z, rim="leather_dark", hole="black", h=0.045, seg=10):
    """Ворот: кольцо и тёмный вырез горловины сверху — иначе верх кирасы читается крышкой."""
    fr.cyl(a, r, r * 0.95, h, seg, loc=(0, 0, z), col=rim, spin=180 / seg)
    fr.cyl(a, r * 0.72, r * 0.72, 0.008, seg, loc=(0, 0, z + h), col=hole, spin=180 / seg)


def chainmail(a, enchanted=False):
    """Кольчуга: рубаха с покатыми плечами и короткими рукавами; кольца — мелкая сетка из трёх тонов
    рядами со сдвигом, кожаный ворот, пояс с пряжкой и кайма по подолу. Зачарованная: часть колец
    светится рунами вразброс, золотые подол и пряжка, камень на вороте."""
    E = enchanted
    fr = Frame((0, 0, 0))
    W0, W1, D0, D1, H, z0 = 0.44, 0.40, 0.24, 0.22, 0.42, 0.02
    z1 = z0 + H
    fr.taper(a, (W0, D0), (W1, D1), H, loc=(0, 0, z0), col="iron_dark", bevel=0.0)

    def ring(r, c):
        if E and (r * 5 + c * 3) % 11 == 0:
            return RUNE
        return (("iron_light", "iron"), ("iron", "rail"))[r % 2][(c + r // 2) % 2]

    inside = (0, 0, 0.25)
    for sy, rows, cols in ((-1, 10, 11), (1, 6, 7)):                                  # перед мельче, спина крупнее
        y0, y1 = sy * D0 / 2, sy * D1 / 2
        face_grid(a, [(-W0 / 2, y0, z0), (W0 / 2, y0, z0), (W1 / 2, y1, z1), (-W1 / 2, y1, z1)], rows, cols, ring,
                  outward=inside)
    for sx in (-1, 1):                                                                 # бока
        x0, x1 = sx * W0 / 2, sx * W1 / 2
        face_grid(a, [(x0, -D0 / 2, z0), (x0, D0 / 2, z0), (x1, D1 / 2, z1), (x1, -D1 / 2, z1)], 8, 4, ring,
                  outward=inside)
    mail = by_normal("iron_light", "iron", "rail", 0.45)
    for sx in (-1, 1):                                                                 # покатые плечи и рукава
        sh = fr.sub((sx * 0.20, 0, z1 - 0.02), rot=(0, sx * 22, 0))
        sh.box(a, (0.16, D1 + 0.01, 0.07), (sx * 0.04, 0, 0), col=mail, bevel=0.0)
        sl = fr.sub((sx * 0.28, 0, z1 - 0.07), rot=(0, sx * 42, 0))
        sl.taper(a, (0.15, 0.17), (0.13, 0.16), 0.16, loc=(0, 0, -0.16), col=mail, bevel=0.0)
        sl.box(a, (0.165, 0.185, 0.03), (0, 0, -0.165), col="leather_dark", bevel=0.0)
    fr.box(a, (W0 + 0.02, D0 + 0.02, 0.04), (0, 0, 0.02), col="gold" if E else "leather_dark", bevel=0.0)  # подол
    fr.box(a, (W0 + 0.012, D0 + 0.03, 0.05), (0, 0, 0.17), col="leather_dark", bevel=0.0)   # пояс
    fr.box(a, (0.08, 0.02, 0.065), (0.07, -D0 / 2 - 0.02, 0.17), col="gold" if E else "iron_light", bevel=0.0)
    neck_hole(a, fr, 0.14, z1 - 0.005)
    if E:
        gem(a, fr, 0.034, loc=(0, -0.13, z1 + 0.02))


def steel_armor(a, enchanted=False):
    """Стальные латы: синеватая кираса с рёбром, юбка из трёх пластин, две набедренные пластины,
    наплечники из трёх лам, горжет с золотым кантом, золотые заклёпки. Зачарованные: рёбро светится
    руной, камень на груди, по паре кристальных шипов на наплечниках."""
    E = enchanted
    fr = Frame((0, 0, 0))
    st = by_normal("steel", "steel_mid", "steel_dark", 0.5)
    zb = 0.13                                           # низ кирасы: ниже — юбка и набедренники
    fr.taper(a, (0.34, 0.22), (0.48, 0.27), 0.42, loc=(0, 0, zb), col=st, bevel=0.03)
    fr.box(a, (0.05, 0.05, 0.32), (0, -0.118, zb + 0.20), rot=(0, 0, 45), col=RUNE if E else "steel", bevel=0.0)
    for k, z in enumerate((zb - 0.005, zb + 0.045, zb + 0.095)):                      # юбка: пластины внахлёст
        fr.box(a, (0.40 - k * 0.025, 0.25, 0.06), (0, 0, z), col="steel" if k % 2 else "steel_mid", bevel=0.0)
        for sx in (-1, 1):
            decal(a, fr, 0.026, 0.026, (sx * (0.17 - k * 0.012), -0.1265, z), col="gold")
    for sx in (-1, 1):                                                                 # набедренники
        t = fr.sub((sx * 0.105, -0.08, 0.0), rot=(0, sx * 8, 0))
        t.taper(a, (0.15, 0.03), (0.16, 0.03), 0.13, col=by_normal("steel", "steel_mid", "steel_dark", 0.5),
                bevel=0.0)
        t.box(a, (0.16, 0.034, 0.022), (0, 0, 0.12), col="gold", bevel=0.0)
    for sx in (-1, 1):                                                                 # наплечники
        fr.ico(a, 0.15, loc=(sx * 0.27, 0, zb + 0.39), scl=(1.12, 1.08, 0.70), col=st, cut=-0.03)
        for k, (dz, ang, w) in enumerate(((0.05, 24, 0.22), (0.0, 34, 0.20))):
            lame = fr.sub((sx * (0.33 + k * 0.035), 0, zb + 0.33 - k * 0.055 + dz), rot=(0, sx * ang, 0))
            lame.box(a, (w, 0.27 - k * 0.02, 0.05), col="steel_mid" if k else "steel", bevel=0.015)
            lame.box(a, (w + 0.005, 0.275 - k * 0.02, 0.016), (0, 0, -0.022), col="gold", bevel=0.0)
        if E:
            for dy, h in ((-0.05, 0.15), (0.05, 0.11)):
                crystal(a, Frame((sx * 0.27, dy, zb + 0.47), rot=(0, sx * 18, 0)), r=0.032, h=h, glow_faces=False)
    fr.cyl(a, 0.125, 0.115, 0.06, 10, loc=(0, 0, zb + 0.42), col="steel_dark")           # горжет
    neck_hole(a, fr, 0.13, zb + 0.475, rim="gold", h=0.022)
    for sx in (-1, 1):
        decal(a, fr, 0.028, 0.028, (sx * 0.15, -0.1325, zb + 0.34), col="gold")
    if E:
        gem(a, fr, 0.05, loc=(0, -0.15, zb + 0.30))


def helmet(a, enchanted=False):
    """Шлем-барбют: глубокий купол на обруче с заклёпками, Т-образная прорезь лица, гребень с багряным
    плюмажем. Стоит краем на z = 0. Зачарованный: золотой обруч, прорезь светится рунным светом, камень
    надо лбом, кристаллы вместо плюмажа."""
    E = enchanted
    fr = Frame((0, 0, 0))
    shell = by_normal("iron_light", "iron", "iron_dark", 0.35)
    fr.cyl(a, 0.165, 0.175, 0.17, 12, col=shell, spin=15)                               # стенки
    fr.ico(a, 0.175, loc=(0, 0, 0.17), scl=(1.0, 1.04, 0.95), col=shell, cut=0.0, sub=2)   # купол
    band = "gold" if E else "iron_dark"
    fr.cyl(a, 0.169, 0.172, 0.04, 12, loc=(0, 0, 0.0), col=band, spin=15)              # обруч
    band_rivets(a, fr, 0.172, 0.02, 10, col="crystal_light" if E else "iron_light")
    slit = RUNE if E else "black"                                                       # Т-прорезь
    fr.box(a, (0.21, 0.04, 0.036), (0, -0.162, 0.175), col=slit, bevel=0.0)
    fr.box(a, (0.05, 0.04, 0.12), (0, -0.168, 0.10), col=slit, bevel=0.0)
    fr.box(a, (0.03, 0.30, 0.04), (0, 0.0, 0.33), col=band, bevel=0.01)                 # гребень
    if E:
        gem(a, fr, 0.03, loc=(0, -0.168, 0.25))
        for dy, h, tilt in ((-0.07, 0.13, -18), (0.0, 0.18, -4), (0.08, 0.13, 12)):
            crystal(a, Frame((0, dy, 0.33), rot=(tilt, 0, 0)), r=0.03, h=h, glow_faces=False)
    else:
        # гребень-щётка ото лба к затылку: бруски по дуге над куполом, торчат по радиусу
        plume = by_normal("cloth_light", "cloth", "cloth_dark", 0.5)
        for k in range(7):
            th = math.radians(132 - k * 14)
            h = 0.13 - abs(k - 3) * 0.012
            rr = 0.185 + h / 2
            fr.box(a, (0.055, 0.06, h), (0, math.cos(th) * rr, 0.17 + math.sin(th) * rr * 0.95),
                   rot=(math.degrees(th) - 90, 0, 0), col=plume, bevel=0.0)


# =========================================================================================
# щиты
# =========================================================================================
def wooden_shield(a, enchanted=False):
    """Деревянный круглый щит с железной кромкой и умбоном. Зачарованный: круг рун по полю, золотые
    заклёпки и камень на умбоне."""
    E = enchanted
    fr = Frame((0, 0, 0.45), rot=(0, 0, 0))
    round_shield(a, fr, r=0.45, face="wood_mid", paint="wood_light",
                 pattern="band", rim="iron_dark", boss="gold" if E else "iron_light", studs=8)
    if E:
        rune_ring(a, fr, 0.27, n=12, y=-0.068)
        fr.ico(a, 0.055, loc=(0, -0.105, 0), scl=(1, 0.7, 1), col=GEM)


def iron_shield(a, enchanted=False, w=0.62, h=0.80):
    """Железный геральдический щит: светлое поле, синий шеврон, тёмная кромка на заклёпках, полоса-усиление
    поверху. Стоит на острие. Зачарованный: шеврон-руна светится, золотая кромка, кристальные заклёпки,
    камень посередине."""
    E = enchanted
    t = 0.07
    fr = Frame((0, 0, (h + 0.12) / 2 + 0.012))
    fr.prism(a, heater_outline(w + 0.12, h + 0.12), t, loc=(0, t / 2, -0.01), col="gold" if E else "iron_dark")
    fr.prism(a, heater_outline(w, h), 0.03, loc=(0, -0.005, 0), col="iron_light")
    yp = -0.026
    fr.box(a, (w, 0.014, 0.075), (0, yp, h / 2 - 0.05), col="iron", bevel=0.0)          # полоса поверху
    chev = [(0.0, h * 0.10), (w * 0.44, -h * 0.20), (w * 0.44, -h * 0.04), (0.0, h * 0.26),
            (-w * 0.44, -h * 0.04), (-w * 0.44, -h * 0.20)]
    fr.prism(a, chev, 0.014, loc=(0, yp, 0), col=RUNE if E else "roof")
    out = heater_outline(w + 0.06, h + 0.06)
    for i in range(len(out)):                                                          # заклёпки по кромке
        (x0, z0), (x1, z1) = out[i], out[(i + 1) % len(out)]
        for u in (0.0, 0.5):
            x, z = x0 + (x1 - x0) * u, z0 + (z1 - z0) * u
            decal(a, fr, 0.036, 0.036, (x, -0.002, z - 0.01), col="crystal_light" if E else "iron_light")
    if E:
        gem(a, fr, 0.05, loc=(0, -0.05, -h * 0.02))


# (имя ассета, билдер, иконка) — порядок = порядок в витрине кита
GEAR = [
    # имеющееся снаряжение (иконки прежние) и зачарованные версии
    ("Res_Sword_Rusty", rusty_sword, "rusty-sword"),
    ("Res_Sword_Rusty_Enchanted", lambda a: rusty_sword(a, True), "enchanted-rusty-sword"),
    ("Res_Sword_Iron", iron_sword, "iron-sword"),
    ("Res_Sword_Enchanted", enchanted_sword, "enchanted-sword"),
    ("Res_BattleAxe", battle_axe, "battle-axe"),
    ("Res_BattleAxe_Enchanted", lambda a: battle_axe(a, enchanted=True), "enchanted-battle-axe"),
    ("Res_Armor_Patched", patched_armor, "patched-armor"),
    ("Res_Armor_Patched_Enchanted", lambda a: patched_armor(a, True), "enchanted-patched-armor"),
    ("Res_Armor_Iron", iron_armor, "iron-armor"),
    ("Res_Armor_Iron_Enchanted", lambda a: iron_armor(a, True), "enchanted-iron-armor"),
    ("Res_Shield_Wood", wooden_shield, "wooden-shield"),
    ("Res_Shield_Wood_Enchanted", lambda a: wooden_shield(a, True), "enchanted-wooden-shield"),
    # новое снаряжение, обе версии
    ("Res_Sword_Steel", steel_sword, "steel-sword"),
    ("Res_Sword_Steel_Enchanted", lambda a: steel_sword(a, True), "enchanted-steel-sword"),
    ("Res_Spear", spear, "spear"),
    ("Res_Spear_Enchanted", lambda a: spear(a, True), "enchanted-spear"),
    ("Res_Bow", bow, "bow"),
    ("Res_Bow_Enchanted", lambda a: bow(a, True), "enchanted-bow"),
    ("Res_WarHammer", war_hammer, "war-hammer"),
    ("Res_WarHammer_Enchanted", lambda a: war_hammer(a, True), "enchanted-war-hammer"),
    ("Res_Armor_Leather", leather_armor, "leather-armor"),
    ("Res_Armor_Leather_Enchanted", lambda a: leather_armor(a, True), "enchanted-leather-armor"),
    ("Res_Armor_Chainmail", chainmail, "chainmail"),
    ("Res_Armor_Chainmail_Enchanted", lambda a: chainmail(a, True), "enchanted-chainmail"),
    ("Res_Armor_Steel", steel_armor, "steel-armor"),
    ("Res_Armor_Steel_Enchanted", lambda a: steel_armor(a, True), "enchanted-steel-armor"),
    ("Res_Helmet", helmet, "helmet"),
    ("Res_Helmet_Enchanted", lambda a: helmet(a, True), "enchanted-helmet"),
    ("Res_Shield_Iron", iron_shield, "iron-shield"),
    ("Res_Shield_Iron_Enchanted", lambda a: iron_shield(a, True), "enchanted-iron-shield"),
]
