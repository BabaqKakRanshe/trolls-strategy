"""
Ассеты арены «Снега» (Arena_Snow): ели в снегу, заснеженные валуны и скальные гряды, ледяные глыбы, сухие
кусты из-под снега, голое дерево, сугробы, сторожевая вышка, шатёр, верёвочный мост и скальные столбы за краем
острова, забор в снегу, разбитые сани, факел; ледяные плитки поля.

Соглашения кита: пивот внизу по центру, лицо к -Y, одна грань — один swatch, фаски на блоках, детали не
тоньше 6–10 см. Природа и мелочь (префикс Snow_) сливаются в Arena_Snow_Scatter и красятся палитрой биома
Vitaria_Palette_Snow (build_vitaria.PALETTE_VARIANTS["Snow"]): снег — sod, moss (верх), тень снега — sod_dark,
лёд — water, water_dark, foam, хвоя — pine_*, скалы — rock, rock_dark. Огонь факела — FX_Flame_Small «Луга»,
угли — на Vitaria_FX.
"""
import math
import random
import bmesh
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, by_normal
from . import tiles as TL
from . import swamp_assets as SA

SNOW = by_normal("sod", "sod_dark", "sod_dark", 0.45)          # снег: верх белый, бока в голубой тени
ICE = by_normal("foam", "water", "water_dark", 0.55)           # лёд: сверху белёсый, бока голубые
ICE_FLOW = by_normal("water", "water", "water_dark", 0.55)     # натёки ледопада: голубые целиком


# ---------------------------------------------------------------------------------------
# природа
# ---------------------------------------------------------------------------------------
def snow_pine(a, seed=1, height=4.0, tiers=4, spread=1.0):
    """Ель в снегу, как на макете: ярусы-конусы тёмной хвои, на каждом — снежная шапка (тот же конус уже и выше,
    у яруса остаётся зелёная юбка) и комья снега по краю."""
    rng = random.Random(seed)
    hs = [1.2 - 0.32 * i / max(1, tiers - 1) for i in range(tiers)]
    s = height / (0.45 + 0.52 * sum(hs[:-1]) + 1.06 * hs[-1])      # верх снежной шапки — на height
    a.add(p_cyl(0.15 * s, 0.1 * s, 0.9 * s, 6, loc=(0, 0, -0.08)), "bark_dark")
    z = 0.45 * s
    for i in range(tiers):
        f = i / max(1, tiers - 1)
        r = (1.0 - 0.58 * f) * spread * s
        h = hs[i] * s
        jx, jy = rng.uniform(-0.05, 0.05) * s, rng.uniform(-0.05, 0.05) * s
        spin = rng.uniform(0, 60)
        green = "pine_mid" if i % 2 else "pine_dark"
        a.add(p_cyl(r, 0.0, h, 7, loc=(jx, jy, z), spin=spin),
              lambda fc, g=green: "pine_dark" if fc.normal.z < -0.3 else g)
        a.add(p_cyl(r * 0.8, 0.0, h * 0.8, 7, loc=(jx, jy, z + h * 0.26), spin=spin + rng.uniform(-10, 10)), SNOW)
        nk = 4 if i < 2 else 3
        for k in range(nk):
            ang = math.radians(spin) + math.tau * (k + rng.uniform(-0.2, 0.2)) / nk
            rr = r * rng.uniform(0.62, 0.78)
            a.add(p_ico(0.16 * s * (1.0 - 0.35 * f), 1, loc=(jx + math.cos(ang) * rr, jy + math.sin(ang) * rr,
                                                             z + h * 0.2),
                        scl=(1.3, 1.1, 0.6), jitter=0.15, rng=rng), SNOW)
        z += h * 0.52


ROCK = by_normal("rock", "rock", "rock_dark", 0.3)


def snow_rock(a, seed=13, size=0.55, n=2, flat=0.72):
    """Валун в снегу, как на макете: тёмный сине-серый камень гранями, на макушке — снежная подушка чуть шире
    макушки (свисает за край), бока камня открыты."""
    rng = random.Random(seed)
    for k in range(n):
        s = size * (1.0 if k == 0 else rng.uniform(0.5, 0.7))
        off = (0, 0) if k == 0 else (rng.uniform(-1, 1) * size * 0.9, rng.uniform(-1, 1) * size * 0.9)
        sy, rz = rng.uniform(0.8, 1.0), rng.uniform(0, 360)
        a.add(p_ico(s, 1, loc=(off[0], off[1], s * 0.25), jitter=0.18, rng=rng, scl=(1, sy, flat),
                    rot=(0, 0, rz), cut=-s * 0.3), ROCK)
        a.add(p_ico(s * 0.8, 1, loc=(off[0], off[1], s * (0.25 + flat * 0.84)), jitter=0.1, rng=rng,
                    scl=(1.05, sy * 1.05, 0.38), rot=(0, 0, rz + rng.uniform(-20, 20)), cut=-s * 0.08), SNOW)


def crag(a, seed=31, w=2.8, d=1.9, h=2.8, n=7):
    """Скальная гряда, как на макете: тёсаные серые столбы разной высоты с подушками снега на макушках."""
    rng = random.Random(seed)
    for k in range(n):
        x = rng.uniform(-w / 2, w / 2)
        y = rng.uniform(-d / 2, d / 2)
        hh = h * rng.uniform(0.45, 1.0) * (1.0 - 0.35 * abs(x) / (w / 2))
        bw, bd = rng.uniform(0.6, 1.0), rng.uniform(0.55, 0.9)
        rz = rng.uniform(-25, 25)
        a.add(p_box((bw, bd, hh), loc=(x, y, hh / 2 - 0.15), rot=(rng.uniform(-4, 4), rng.uniform(-4, 4), rz),
                    bevel=0.06),
              lambda f: "sod" if f.normal.z > 0.6 else ("rock_dark" if f.normal.z < -0.3 else "rock"))
        m = max(bw, bd)
        a.add(p_ico(m * 0.62, 1, loc=(x, y, hh - 0.16), scl=(bw / m * 1.1, bd / m * 1.1, 0.32), rot=(0, 0, rz),
                    jitter=0.12, rng=rng, cut=-0.05), SNOW)


def ice_block(a, seed=41, size=0.75):
    """Ледяная глыба: две бруска льда, сверху белёсые, бока голубые."""
    rng = random.Random(seed)
    for k in range(2):
        s = size * (1.0 if k == 0 else 0.55)
        loc = (0, 0, s * 0.4) if k == 0 else (size * 0.75, rng.uniform(-0.15, 0.15), s * 0.36)
        a.add(p_box((s, s * rng.uniform(0.8, 1.0), s * 0.85), loc=loc,
                    rot=(rng.uniform(-6, 6), rng.uniform(-6, 6), rng.uniform(0, 90)), bevel=0.05), ICE)


def twigs(a, seed=51, n=5, h=0.7):
    """Сухой куст из-под снега: несколько голых прутьев с сучками и снег у основания."""
    rng = random.Random(seed)
    for k in range(n):
        ang = math.tau * k / n + rng.uniform(-0.3, 0.3)
        base = Vector((math.cos(ang) * 0.06, math.sin(ang) * 0.06, -0.05))
        lean = rng.uniform(0.15, 0.45)
        top = base + Vector((math.cos(ang) * lean, math.sin(ang) * lean, rng.uniform(0.7, 1.0) * h))
        SA._limb(a, base, top, 0.04, 0.03, "blade_dark" if k % 2 else "blade", seg=4)
        if rng.random() < 0.6:
            mid = base.lerp(top, rng.uniform(0.45, 0.65))
            tip = mid + Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.uniform(0.15, 0.3)))
            SA._limb(a, mid, tip, 0.03, 0.03, "blade_dark", seg=4)
    a.add(p_ico(0.17, 1, loc=(0, 0, 0.0), scl=(1.3, 1.1, 0.4), jitter=0.15, rng=rng, cut=-0.03), SNOW)


def drift(a, seed=61, size=1.0):
    """Сугроб: два пологих снежных горба (сфера мельче, чем у камней: снег мягкий, не гранёный)."""
    rng = random.Random(seed)
    a.add(p_ico(0.62 * size, 2, loc=(0, 0, -0.06), scl=(1.6, 1.0, 0.36), jitter=0.06, rng=rng, cut=0.0), SNOW)
    a.add(p_ico(0.42 * size, 2, loc=(0.55 * size, 0.18 * size, -0.06), scl=(1.3, 1.0, 0.3), jitter=0.06, rng=rng,
                cut=0.0), SNOW)


# ---------------------------------------------------------------------------------------
# постройки лагерей
# ---------------------------------------------------------------------------------------
def roof(a, R, H, z, col, under, eave=0.15, snow_gap=0.09):
    """Кровля-пирамида (4 ската) и снег на ней: пирамида, параллельная скатам на snow_gap выше, от высоты eave над
    свесом — у края видна полоса кровли."""
    a.add(p_cyl(R, 0.0, H, 4, loc=(0, 0, z), spin=45), lambda f, c=col, u=under: u if f.normal.z < 0 else c)
    rb = R * (1.0 - eave / H) + snow_gap * 1.4
    hb = H - eave + snow_gap * 1.4 * H / R
    a.add(p_cyl(rb, 0.0, hb, 4, loc=(0, 0, z + eave), spin=45), SNOW)


def watchtower(a, seed=71):
    """Сторожевая вышка лагеря игрока: четыре бревна-ноги с раскосами, площадка на 3.3 м с перилами, крыша-
    пирамида под снегом, лестница к -Y. Пивот — земля под серединой."""
    H, B, T = 3.3, 0.95, 0.68                 # высота площадки, полуширина у земли и у площадки
    top = H + 1.5
    legs = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            p0, p1 = Vector((sx * B, sy * B, -0.1)), Vector((sx * T, sy * T, top))
            SA._limb(a, p0, p1, 0.11, 0.09, "wood_dark", seg=6)
            legs.append((p0, p1))

    def leg_at(i, z):
        p0, p1 = legs[i]
        return p0.lerp(p1, (z + 0.1) / (top + 0.1))

    for i, j in ((0, 1), (1, 3), (3, 2), (2, 0)):        # раскосы крест-накрест на четырёх гранях и обвязка
        for z0, z1 in ((0.35, 1.75), (1.75, 3.15)):
            SA._limb(a, leg_at(i, z0), leg_at(j, z1), 0.05, 0.05, "wood_mid", seg=5)
            SA._limb(a, leg_at(j, z0), leg_at(i, z1), 0.05, 0.05, "wood_mid", seg=5)
        SA._limb(a, leg_at(i, 0.35), leg_at(j, 0.35), 0.055, 0.055, "wood_mid", seg=5)
        SA._limb(a, leg_at(i, H + 0.8), leg_at(j, H + 0.8), 0.05, 0.05, "wood_mid", seg=5)      # перила
    a.add(p_box((2 * T + 0.55, 2 * T + 0.55, 0.14), loc=(0, 0, H), bevel=0.03),
          lambda f: "sod" if f.normal.z > 0.6 else "wood_mid")
    for sx in (-1, 1):                                    # доски ограждения площадки
        a.add(p_box((0.08, 2 * T + 0.3, 0.32), loc=(sx * (T + 0.02), 0, H + 0.3), bevel=0.02), "wood_light")
        a.add(p_box((2 * T + 0.3, 0.08, 0.32), loc=(0, sx * (T + 0.02), H + 0.3), bevel=0.02), "wood_light")
    roof(a, T * 1.75, 0.95, top, "roof_old", "wood_dark", 0.14)
    for sx in (-1, 1):                                    # лестница к -Y
        SA._limb(a, (sx * 0.28, -B - 0.6, -0.05), (sx * 0.28, -T - 0.06, H + 0.05), 0.045, 0.045, "wood_mid", seg=4)
    p0, p1 = Vector((0, -B - 0.6, -0.05)), Vector((0, -T - 0.06, H + 0.05))
    for k in range(8):
        a.add(p_box((0.62, 0.07, 0.06), loc=p0.lerp(p1, (k + 0.5) / 8), bevel=0.0), "wood_light")
    for sx in (-1, 1):                                    # снег у ног
        for sy in (-1, 1):
            a.add(p_ico(0.24, 1, loc=(sx * B, sy * B, -0.04), scl=(1.3, 1.3, 0.4), cut=0.0), SNOW)


def pavilion(a, seed=81):
    """Шатёр гоблинов: четыре столба, полотняная кровля-пирамида со снегом и фестонами; бочки под ним ставятся
    отдельно. Пивот — земля под серединой."""
    rng = random.Random(seed)
    W = 1.25
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.14, 0.14, 2.38), loc=(sx * W, sy * W, 1.12), rot=(0, 0, rng.uniform(-4, 4)), bevel=0.03),
                  "wood_dark")
            a.add(p_ico(0.2, 1, loc=(sx * W, sy * W, -0.04), scl=(1.3, 1.3, 0.45), cut=0.0), SNOW)
    for sy in (-1, 1):
        a.add(p_box((2 * W + 0.2, 0.1, 0.1), loc=(0, sy * W, 2.28), bevel=0.02), "wood_mid")
        a.add(p_box((0.1, 2 * W + 0.2, 0.1), loc=(sy * W, 0, 2.28), bevel=0.02), "wood_mid")
    roof(a, W * 1.62, 1.0, 2.3, "canvas", "canvas_dark", 0.5)          # снег на верхней половине: полотно видно
    for k in range(4):                                    # фестоны по краю кровли
        ang = math.radians(90 * k)
        a.add(p_box((2 * W + 0.36, 0.04, 0.22), loc=((W + 0.2) * math.sin(ang), -(W + 0.2) * math.cos(ang), 2.2),
                    rot=(0, 0, 90 * k), bevel=0.0), "canvas_dark")
    a.add(p_cyl(0.05, 0.03, 0.5, 5, loc=(0, 0, 3.2)), "wood_dark")


BRIDGE_L = 3.6


def rope_bridge(a, seed=91, L=BRIDGE_L, sag=0.22):
    """Верёвочный мост: доски поперёк на двух канатах, перила-канаты на столбах по концам, провис посередине.
    Вдоль X, пивот — середина пролёта на уровне настила у концов."""
    rng = random.Random(seed)
    n = int(L / 0.24)

    def zf(x):
        return -sag * (1 - (2 * x / L) ** 2)

    for k in range(n):
        x = -L / 2 + (k + 0.5) * L / n
        if 1 < k < n - 2 and rng.random() < 0.08:          # выпавшая доска
            continue
        a.add(p_box((L / n - 0.05, 0.86 + rng.uniform(-0.05, 0.05), 0.06), loc=(x, rng.uniform(-0.03, 0.03), zf(x) - 0.03),
                    rot=(rng.uniform(-3, 3), 0, rng.uniform(-4, 4)), bevel=0.012),
              ("wood_mid", "wood_light", "wood_mid2")[k % 3])
    m = 10
    xs = [-L / 2 - 0.05 + (L + 0.1) * i / m for i in range(m + 1)]
    for sy in (-0.36, 0.36):                              # несущие канаты под досками
        for x0, x1 in zip(xs, xs[1:]):
            SA._limb(a, (x0, sy, zf(x0) - 0.08), (x1, sy, zf(x1) - 0.08), 0.035, 0.035, "rope", seg=4)
    for sy in (-0.5, 0.5):                                # перила и подвесы
        for x0, x1 in zip(xs, xs[1:]):
            SA._limb(a, (x0, sy, 0.6 * zf(x0) + 0.8), (x1, sy, 0.6 * zf(x1) + 0.8), 0.035, 0.035, "rope", seg=4)
        for x in xs[2:-2:2]:
            SA._limb(a, (x, sy, 0.6 * zf(x) + 0.8), (x, sy * 0.86, zf(x) - 0.02), 0.03, 0.03, "rope", seg=4)
    for sx in (-1, 1):                                    # столбы по концам, снег на макушках
        for sy in (-1, 1):
            x, y = sx * (L / 2 + 0.06), sy * 0.52
            a.add(p_cyl(0.09, 0.08, 1.4, 6, loc=(x, y, -0.45)), "wood_dark")
            a.add(p_ico(0.1, 1, loc=(x, y, 0.97), scl=(1.0, 1.0, 0.55)), SNOW)


def pillar(a, seed=101, r=1.05, depth=7.0):
    """Скальный столб за краем острова (дальний конец моста): шапка снега, ярусы сужаются вниз, сосульки с кромки."""
    rng = random.Random(seed)
    z, rr = 0.0, r
    while z > -depth:
        h = rng.uniform(1.2, 2.0)
        r2 = rr * rng.uniform(0.72, 0.86)
        a.add(p_cyl(rr, r2, h, 7, loc=(rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), z - h - 0.08),
                    spin=rng.uniform(0, 50)),
              lambda f: "sod_dark" if f.normal.z > 0.6 else ("rock_dark" if f.normal.z < -0.3 else "rock"))
        z -= h
        rr = r2
    a.add(p_ico(r * 1.08, 1, loc=(0, 0, -0.14), scl=(1.0, 0.95, 0.34), jitter=0.1, rng=rng, cut=-0.04), SNOW)
    for k in range(9):                                    # сосульки с кромки
        ang = math.tau * k / 9 + rng.uniform(-0.15, 0.15)
        L = rng.uniform(0.35, 1.1)
        top = Vector((math.cos(ang) * r * 0.98, math.sin(ang) * r * 0.98, -0.1))
        a.add(p_cyl(rng.uniform(0.06, 0.1), 0.0, L, 4, loc=top, rot=(180, 0, rng.uniform(0, 90))),
              "foam" if k % 3 else "water")


def snow_fence(a, seed=111):
    """Сегмент забора 1 м (как Prop_Fence кита): столбы и две жерди, на жердях и макушках — снег."""
    rng = random.Random(seed)
    for x in (-0.42, 0.42):
        a.add(p_box((0.17, 0.17, 0.74), loc=(x, 0, 0.3), bevel=0.04), "wood_dark")
        a.add(p_ico(0.12, 1, loc=(x, 0, 0.67), scl=(1.1, 1.1, 0.6), jitter=0.1, rng=rng, cut=-0.02), SNOW)
    for z in (0.24, 0.53):
        a.add(p_box((1.0, 0.11, 0.16), loc=(0, -0.08, z), bevel=0.035), "wood_mid")
        a.add(p_box((0.9, 0.12, 0.05), loc=(rng.uniform(-0.04, 0.04), -0.08, z + 0.1), bevel=0.02), "sod")


def sled(a, seed=121):
    """Разбитые сани: полозья с загнутыми носами, настил из реек (одна сломана и торчит), спинка, снег на настиле."""
    rng = random.Random(seed)
    for sy in (-0.32, 0.32):
        a.add(p_box((1.5, 0.07, 0.07), loc=(0, sy, 0.05), bevel=0.015), "wood_dark")
        a.add(p_box((0.32, 0.07, 0.07), loc=(0.84, sy, 0.16), rot=(0, -45, 0), bevel=0.015), "wood_dark")
        for x in (-0.5, 0.0, 0.5):
            a.add(p_box((0.06, 0.06, 0.22), loc=(x, sy, 0.18), bevel=0.01), "wood_mid")
    for k, x in enumerate((-0.55, -0.32, -0.09, 0.14, 0.37)):
        if k == 3:
            a.add(p_box((0.16, 0.36, 0.045), loc=(x, -0.2, 0.36), rot=(28, 0, 12), bevel=0.01), "wood_light")
            continue
        a.add(p_box((0.17, 0.78, 0.045), loc=(x, 0, 0.31), bevel=0.01), "wood_light")
    a.add(p_box((0.08, 0.7, 0.3), loc=(-0.72, 0, 0.42), rot=(0, -8, 0), bevel=0.015), "wood_mid")
    a.add(p_ico(0.3, 1, loc=(-0.2, 0.06, 0.33), scl=(1.5, 1.1, 0.24), jitter=0.12, rng=rng, cut=-0.02), SNOW)
    a.add(p_ico(0.3, 1, loc=(0.1, 0.4, -0.02), scl=(1.6, 0.8, 0.4), jitter=0.12, rng=rng, cut=0.0), SNOW)


ASSETS = [
    ("Snow", "Snow_Pine_A", "Ель в снегу", lambda a: snow_pine(a, 1, 4.0, 4, 1.0)),
    ("Snow", "Snow_Pine_B", "Ель в снегу, высокая", lambda a: snow_pine(a, 2, 5.0, 5, 0.95)),
    ("Snow", "Snow_Pine_C", "Ёлка в снегу, малая", lambda a: snow_pine(a, 3, 2.6, 3, 1.05)),
    ("Snow", "Snow_Rock_A", "Валун в снегу", lambda a: snow_rock(a, 13, 0.6, 2)),
    ("Snow", "Snow_Rock_B", "Камень в снегу", lambda a: snow_rock(a, 14, 0.32, 1)),
    ("Snow", "Snow_Crag_A", "Скальная гряда в снегу", lambda a: crag(a, 31, 2.8, 1.9, 2.8, 7)),
    ("Snow", "Snow_Crag_B", "Скальная гряда в снегу, низкая", lambda a: crag(a, 32, 2.2, 1.5, 1.6, 5)),
    ("Snow", "Snow_IceBlock", "Ледяная глыба", ice_block),
    ("Snow", "Snow_Twigs", "Сухой куст из-под снега", twigs),
    ("Snow", "Snow_DeadTree", "Голое дерево в снегу",
     lambda a: SA.swamp_tree(a, 7, 4.6, 0.55, 0.9, (0.05, 0.0), 3, 0.0, 0.0, 0.0, moss=0.25)),
    ("Snow", "Snow_Drift", "Сугроб", drift),
    ("Snow", "Snow_Watchtower", "Сторожевая вышка", watchtower),
    ("Snow", "Snow_Pavilion", "Шатёр гоблинов", pavilion),
    ("Snow", "Snow_RopeBridge", "Верёвочный мост 3.6 м", rope_bridge),
    ("Snow", "Snow_Pillar", "Скальный столб за краем острова", pillar),
    ("Snow", "Snow_Fence", "Забор в снегу, сегмент 1 м", snow_fence),
    ("Snow", "Snow_Sled", "Разбитые сани", sled),
    ("Snow", "Snow_Torch", "Факел на шесте", SA.torch),
    # плитки поля биома: геометрия и UV — как у Hex_Tile_A/B/C, цвет — палитра Vitaria_Palette_Snow (лёд)
    ("Snow", "Hex_Tile_Snow_A", "Гекс-плитка снегов A", TL.tile_plain(11, base=0.5)),
    ("Snow", "Hex_Tile_Snow_B", "Гекс-плитка снегов B (кочки)", TL.tile_tufts),
    ("Snow", "Hex_Tile_Snow_C", "Гекс-плитка снегов C (камни)", TL.tile_pebbles),
    ("Preview", "Preview_Troll", "Тролль-заглушка (превью)", SA.preview_troll),
    ("Preview", "Preview_Goblin", "Гоблин-заглушка (превью)", SA.preview_goblin),
]
