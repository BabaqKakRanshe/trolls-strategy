"""
Реквизит арены: жаровня, знамёна (синее — игрок, красное — гоблины), стойка с оружием,
палатка, костёр и отдельные языки пламени (FX_Flame_*) — их в Unity покачивает скрипт,
поэтому огонь не вшит в жаровню и костёр, а ставится поверх отдельным мешем.

Соглашения кита: пивот внизу по центру, лицо к -Y, одна грань — один swatch, фаски везде.
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix
from build_vitaria import p_box, p_cyl, p_ico, p_prism, p_taper_box, by_normal
from vitaria_buildings.common import Frame, spear, sword, axe, round_shield, log_piece

WOOD_TOP = by_normal("wood_light", "wood_mid", "wood_dark", 0.7)
IRON = by_normal("iron", "iron_dark", "iron_dark", 0.6)
STONE = by_normal("stone_light", "stone_mid", "stone_dark", 0.55)


def _beam(a, p0, p1, s=0.08, col="wood_dark", bevel=0.02):
    """Брус квадратного сечения s от точки p0 до p1."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    q = d.to_track_quat("Z", "Y")
    a.add(p_box((s, s, d.length), mat=Matrix.LocRotScale((p0 + p1) / 2, q, (1, 1, 1)), bevel=bevel), col)


def _stones_ring(a, rng, r, n, z=0.0, size=(0.12, 0.17)):
    for k in range(n):
        ang = math.tau * k / n + rng.uniform(-0.12, 0.12)
        s = rng.uniform(*size)
        a.add(p_ico(s, 1, loc=(math.cos(ang) * r, math.sin(ang) * r, z + s * 0.25), scl=(1.0, 0.85, 0.7),
                    jitter=0.2, rng=rng, rot=(0, 0, math.degrees(ang)), cut=-s * 0.3), STONE)


def _coals(a, rng, r, z, n=9):
    for k in range(n):
        ang = rng.uniform(0, math.tau)
        d = r * math.sqrt(rng.random())
        s = rng.uniform(0.05, 0.08)
        a.add(p_ico(s, 1, loc=(math.cos(ang) * d, math.sin(ang) * d, z), scl=(1, 1, 0.7), jitter=0.2, rng=rng),
              "ember" if k % 3 else "glow")


# ---------------------------------------------------------------------------------------
# огонь: отдельные меши, пивот у основания пламени
# ---------------------------------------------------------------------------------------
def flame(a, size=1.0, seed=0):
    """Язык пламени KayKit: три вложенных конуса — горячая сердцевина, оранжевый, алый."""
    rng = random.Random(seed)
    s = size
    a.add(p_cyl(0.2 * s, 0.0, 0.42 * s, 6, loc=(0, 0, 0), spin=rng.uniform(0, 60)), "ember")
    for k in range(3):
        ang = math.tau * k / 3 + 0.4
        a.add(p_cyl(0.1 * s, 0.0, 0.34 * s, 5, loc=(math.cos(ang) * 0.12 * s, math.sin(ang) * 0.12 * s, 0.0),
                    rot=(math.cos(ang) * 14, -math.sin(ang) * 14, 0)), "glow")
    a.add(p_cyl(0.13 * s, 0.0, 0.62 * s, 6, loc=(0, 0, 0.02 * s), spin=30), "glow")
    a.add(p_cyl(0.075 * s, 0.0, 0.46 * s, 5, loc=(0, 0, 0.05 * s)), "glow_hot")


# ---------------------------------------------------------------------------------------
def brazier(a):
    """Жаровня на треноге: железная чаша с углями, пламя — отдельный FX_Flame_Small."""
    top = Vector((0, 0, 0.9))
    for k in range(3):
        ang = math.radians(90 + 120 * k)
        foot = Vector((math.cos(ang) * 0.44, math.sin(ang) * 0.44, 0.0))
        _beam(a, foot, top, 0.085, "wood_dark")
        a.add(p_box((0.13, 0.13, 0.07), loc=foot + Vector((0, 0, 0.035)), bevel=0.02), "iron_dark")
    a.add(p_cyl(0.15, 0.15, 0.1, 8, loc=(0, 0, 0.8)), "iron_dark")                  # хомут
    a.add(p_cyl(0.17, 0.17, 0.03, 10, loc=(0, 0, 0.9)), "iron_dark")                # дно чаши
    a.add(p_cyl(0.17, 0.37, 0.24, 10, loc=(0, 0, 0.9), cap=False), IRON)           # стенка
    a.add(p_cyl(0.395, 0.395, 0.05, 10, loc=(0, 0, 1.12)), "iron_dark")             # венец
    for k in range(5):                                                               # зубцы-держатели
        ang = math.tau * k / 5
        a.add(p_box((0.05, 0.05, 0.12), loc=(math.cos(ang) * 0.37, math.sin(ang) * 0.37, 1.2), bevel=0.012),
              "iron_dark")
    rng = random.Random(3)
    _coals(a, rng, 0.25, 1.1, 11)


def _cloth(a, x0, x1, z0, z1, y, col, rng, strips=4, waves=0.02):
    """Полотно из вертикальных полос с лёгкой волной: читается как ткань, не как доска."""
    w = (x1 - x0) / strips
    for k in range(strips):
        cx = x0 + w * (k + 0.5)
        dy = waves * math.sin(k * 1.9 + 0.4)
        rz = 7 * math.sin(k * 2.3 + 1.0)
        a.add(p_box((w + 0.012, 0.035, z1 - z0), loc=(cx, y + dy, (z0 + z1) / 2), rot=(0, 0, rz), bevel=0.008),
              col)


# точка подвеса полотна знамени (центр верхней кромки): пивот Prop_Banner_*_Cloth, ось качания — X
BANNER_HANG = (0.0, -0.05, 2.62)


def banner_pole(a, trim="gold", seed=5):
    """Древко знамени: камни у основания, шест с навершием, перекладина. Полотно — отдельный меш."""
    rng = random.Random(seed)
    for x, y, r in ((0.13, -0.07, 0.19), (-0.15, 0.05, 0.17), (0.03, 0.17, 0.14)):
        a.add(p_ico(r, 1, loc=(x, y, r * 0.3), scl=(1, 0.9, 0.75), jitter=0.22, rng=rng, cut=-r * 0.3), STONE)
    a.add(p_cyl(0.055, 0.045, 3.05, 8, loc=(0, 0.06, -0.05)), "wood_dark")
    a.add(p_ico(0.07, 1, loc=(0, 0.06, 3.02)), trim)
    a.add(p_cyl(0.05, 0.0, 0.2, 6, loc=(0, 0.06, 3.07)), trim)
    a.add(p_box((1.04, 0.075, 0.075), loc=(0, 0.0, 2.66), bevel=0.02), "wood_dark")
    for sx in (-1, 1):
        a.add(p_ico(0.05, 1, loc=(sx * 0.55, 0.0, 2.66)), trim)


def banner_cloth(a, cloth="roof", trim="gold", emblem="stag"):
    """Полотно знамени с эмблемой. Пивот — точка подвеса BANNER_HANG: в игре полотно качается
    вокруг перекладины (BattleArenaAmbience, поле sway в раскладке), древко стоит."""
    rng = random.Random(5 if cloth == "roof" else 6)
    # полотно: 0.86 x 1.45 и ласточкин хвост
    x0, x1, zt, zb = -0.43, 0.43, 2.62, 1.2
    _cloth(a, x0, x1, zb, zt, -0.05, cloth, rng)
    for sx in (-1, 1):
        a.add(p_prism([(0.0, 0.0), (sx * 0.43, 0.0), (sx * 0.43, -0.38)] if sx > 0 else
                      [(0.0, 0.0), (sx * 0.43, -0.38), (sx * 0.43, 0.0)], 0.035, loc=(0, -0.05, zb + 0.005)), cloth)
    a.add(p_box((0.9, 0.05, 0.07), loc=(0, -0.07, zt - 0.02), bevel=0.012), trim)     # кайма сверху
    # эмблема на лицевой стороне (-Y), кремовая; полотно с волной выходит максимум до y=-0.088
    ye = -0.118
    if emblem == "stag":
        # белое древо (как на макете, отсылка к Белому древу Гондора): ствол, три яруса ветвей
        # вверх, ромб кроны и холмик у корня — без «рук и ног», чтобы не читалось человечком
        a.add(p_box((0.07, 0.03, 0.66), loc=(0, ye, 1.93), bevel=0.01), "cream")
        a.add(p_box((0.3, 0.03, 0.06), loc=(0, ye, 1.6), bevel=0.01), "cream")
        a.add(p_box((0.16, 0.03, 0.06), loc=(0, ye, 1.66), bevel=0.01), "cream")
        for z, L, ang, off in ((1.84, 0.16, 52, 0.07), (2.0, 0.22, 44, 0.09), (2.15, 0.17, 34, 0.07)):
            for sx in (-1, 1):
                a.add(p_box((0.045, 0.03, L), loc=(sx * (off + math.sin(math.radians(ang)) * L / 2), ye,
                                                   z + math.cos(math.radians(ang)) * L / 2),
                            rot=(0, sx * ang, 0), bevel=0.008), "cream")
                a.add(p_ico(0.028, 1, loc=(sx * (off + math.sin(math.radians(ang)) * L), ye,
                                           z + math.cos(math.radians(ang)) * L), scl=(1, 0.5, 1)), "cream")
        a.add(p_prism([(0.0, 2.24), (0.07, 2.33), (0.0, 2.43), (-0.07, 2.33)], 0.03, loc=(0, ye, 0)), "cream")
    else:
        # клыки гоблинов: два клыка и перекладина
        for sx in (-1, 1):
            a.add(p_prism([(-0.08, 0.0), (0.08, 0.0), (0.0, -0.36)], 0.03, loc=(sx * 0.14, ye, 2.1)), "cream")
        a.add(p_box((0.5, 0.03, 0.07), loc=(0, ye, 2.14), bevel=0.01), "cream")
        a.add(p_ico(0.08, 1, loc=(0, ye, 2.34), scl=(1, 0.45, 1)), "cream")
    hx, hy, hz = BANNER_HANG
    bmesh.ops.translate(a.bm, vec=(-hx, -hy, -hz), verts=a.bm.verts)


def weapon_rack(a):
    """Стойка с оружием: два козла, две перекладины, копья, меч, топор и щит у ног."""
    for sx in (-1, 1):
        x = sx * 0.62
        a.add(p_box((0.09, 0.09, 1.12), loc=(x, 0.0, 0.56), bevel=0.02), "wood_dark")
        a.add(p_box((0.1, 0.52, 0.09), loc=(x, 0.0, 0.05), bevel=0.02), "wood_dark")
        _beam(a, (x, -0.22, 0.08), (x, 0.0, 0.5), 0.06, "wood_mid")
        _beam(a, (x, 0.22, 0.08), (x, 0.0, 0.5), 0.06, "wood_mid")
    a.add(p_box((1.42, 0.1, 0.09), loc=(0, 0.0, 1.06), bevel=0.02), WOOD_TOP)
    a.add(p_box((1.34, 0.22, 0.06), loc=(0, 0.02, 0.3), bevel=0.015), WOOD_TOP)
    # оружие прислонено к верхней перекладине (наклон назад ~12°)
    base = Frame((0, 0.0, 0.0))
    for x, L, rz in ((-0.42, 1.75, 0), (-0.22, 1.62, 10)):
        spear(a, base.sub((x, -0.16, 0.33), rot=(-12, 0, rz)), L=L)
    sword(a, base.sub((0.05, -0.14, 0.33), rot=(-12, 0, 0)), L=0.95)
    axe(a, base.sub((0.3, -0.15, 0.33), rot=(-12, 0, 180)), L=0.9)
    round_shield(a, base.sub((0.46, -0.32, 0.42), rot=(-14, 0, -8)), r=0.38, face="roof", paint="cream",
                 pattern="quarters", studs=6)


def tent(a):
    """Двускатная палатка: полотно на коньке, откинутые полы входа, растяжки и колья."""
    L, W, H = 2.5, 2.3, 1.85                 # вдоль X конёк, вход в торце -X
    # скаты: призмы-треугольники со «швами» — три полосы полотна на скат
    for k, (xa, xb) in enumerate(((-L / 2, -L / 6), (-L / 6, L / 6), (L / 6, L / 2))):
        col = "canvas" if k != 1 else "canvas_dark"
        for sy in (-1, 1):
            pts = [(0.0, H), (sy * W / 2, 0.02), (sy * (W / 2 - 0.07), 0.02), (0.0, H - 0.1)]
            if sy < 0:
                pts = [pts[0], pts[3], pts[2], pts[1]]
            ln = xb - xa
            a.add(p_prism(pts, ln + 0.01, loc=((xa + xb) / 2, 0, 0), rot=(0, 0, 90)),
                  lambda f, c=col: "canvas_dark" if f.normal.z < -0.3 else c)
    # задняя стенка
    a.add(p_prism([(-W / 2 + 0.07, 0.02), (W / 2 - 0.07, 0.02), (0.0, H - 0.1)], 0.05, loc=(L / 2 - 0.04, 0, 0),
                  rot=(0, 0, 90)), "canvas_dark")
    # вход: тёмный проём и две откинутые полы
    a.add(p_prism([(-W / 2 + 0.25, 0.02), (W / 2 - 0.25, 0.02), (0.0, H - 0.35)], 0.03,
                  loc=(-L / 2 + 0.06, 0, 0), rot=(0, 0, 90)), "black")
    for sy in (-1, 1):
        pts = [(0.0, H - 0.08), (sy * 0.62, 0.05), (sy * 0.18, 0.05)]
        if sy > 0:
            pts = [pts[0], pts[2], pts[1]]
        a.add(p_prism(pts, 0.03, loc=(-L / 2 - 0.02, sy * 0.28, 0), rot=(0, 0, 90 + sy * 28)), "canvas")
    # конёк и стойки
    a.add(p_box((L + 0.34, 0.08, 0.08), loc=(0, 0, H + 0.02), bevel=0.02), "wood_dark")
    for sx in (-1, 1):
        a.add(p_box((0.08, 0.08, H + 0.18), loc=(sx * (L / 2 + 0.1), 0, (H + 0.18) / 2), bevel=0.02), "wood_dark")
        # растяжки к кольям
        for sy in (-1, 1):
            p0 = Vector((sx * (L / 2 + 0.12), 0, H - 0.05))
            p1 = Vector((sx * (L / 2 + 0.75), sy * 0.55, 0.12))
            _beam(a, p0, p1, 0.022, "rope", bevel=0.0)
            a.add(p_box((0.06, 0.06, 0.2), loc=p1 + Vector((0, 0, -0.02)), rot=(sx * 10, 0, 0), bevel=0.01), "wood_mid")
    # колья по подолу
    for sx in (-0.9, 0.0, 0.9):
        for sy in (-1, 1):
            a.add(p_box((0.05, 0.05, 0.16), loc=(sx, sy * (W / 2 + 0.12), 0.06), bevel=0.01), "wood_mid")


def campfire(a):
    """Костёр: кольцо камней, шалаш из поленьев, угли; над ним вертел на рогатинах."""
    rng = random.Random(8)
    _stones_ring(a, rng, 0.46, 9)
    for k in range(5):                                       # шалаш
        ang = math.tau * k / 5 + 0.3
        foot = Vector((math.cos(ang) * 0.34, math.sin(ang) * 0.34, 0.05))
        top = Vector((math.cos(ang) * 0.05, math.sin(ang) * 0.05, 0.42))
        d = top - foot
        q = d.to_track_quat("Z", "Y")
        a.add(p_cyl(0.055, 0.045, d.length, 6, mat=Matrix.LocRotScale(foot, q, (1, 1, 1))),
              lambda f: "wood_pale" if abs(f.normal.z) > 0.8 else "bark")
    _coals(a, rng, 0.28, 0.06, 10)
    for sx in (-1, 1):                                       # рогатины и вертел
        x = sx * 0.62
        a.add(p_cyl(0.04, 0.035, 0.72, 6, loc=(x, 0.0, 0.0)), "wood_mid")          # не тоньше 7 см
        a.add(p_cyl(0.032, 0.027, 0.16, 6, loc=(x, 0.0, 0.66), rot=(0, sx * 30, 0)), "wood_mid")
    a.add(p_cyl(0.03, 0.03, 1.36, 6, loc=(-0.68, 0.0, 0.7), rot=(0, 90, 0)), "wood_dark")
    a.add(p_ico(0.11, 1, loc=(0.18, 0.0, 0.7), scl=(1.4, 0.9, 0.9), jitter=0.1, rng=rng), "leather")   # жаркое
    # котелок стоит сбоку на камнях: над огнём его прошивало бы отдельное пламя FX
    a.add(p_cyl(0.13, 0.15, 0.22, 8, loc=(0.0, -0.66, 0.0)), IRON)
    a.add(p_cyl(0.12, 0.12, 0.012, 8, loc=(0.0, -0.66, 0.2)), "leather")


ASSETS = [
    ("Arena", "Prop_Brazier", "Жаровня", brazier),
    ("Arena", "Prop_Banner_Blue", "Знамя игрока: древко", lambda a: banner_pole(a, "gold", 5)),
    ("Arena", "Prop_Banner_Blue_Cloth", "Знамя игрока: полотно (качается)", lambda a: banner_cloth(a, "roof", "gold", "stag")),
    ("Arena", "Prop_Banner_Red", "Знамя гоблинов: древко", lambda a: banner_pole(a, "iron_light", 6)),
    ("Arena", "Prop_Banner_Red_Cloth", "Знамя гоблинов: полотно (качается)",
     lambda a: banner_cloth(a, "berry", "iron_light", "fangs")),
    ("Arena", "Prop_WeaponRack", "Стойка с оружием", weapon_rack),
    ("Arena", "Prop_Tent", "Палатка", tent),
    ("Arena", "Prop_Campfire", "Костёр", campfire),
    ("Arena", "FX_Flame_Small", "Пламя (жаровня)", lambda a: flame(a, 0.8, 1)),
    ("Arena", "FX_Flame_Large", "Пламя (костёр)", lambda a: flame(a, 1.15, 2)),
]
