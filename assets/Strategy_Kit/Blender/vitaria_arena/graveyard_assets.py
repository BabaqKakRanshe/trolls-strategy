"""
Ассеты арены «Кладбище» (Arena_Graveyard): часовня с горящим входом, кривые сухие деревья, кованая ограда со
столбами, надгробия, кресты, памятник, саркофаг, «призрачное» надгробие, фонари на столбах, свечи, телега с
гробом, кости, тыквы, руины стены, лестница с кромки, деревянные ступени, мостки; каменные плитки поля.

Соглашения кита: пивот внизу по центру, лицо к -Y, одна грань — один swatch, фаски на блоках, детали не
тоньше 6 см. Природа, постройки и мелочь (префикс Grave_) сливаются в Arena_Graveyard_Scatter и красятся палитрой
биома Vitaria_Palette_Graveyard (build_vitaria.PALETTE_VARIANTS["Graveyard"]): камень надгробий и часовни —
stone_*, сланец крыши — slate_*, кованое железо — iron_dark, iron, мох — moss, sod, кора — bark, bark_dark.
Свет — на светящихся swatch-ах (lantern_glow — фонари и вход часовни, glow_hot — свечи, rune — голубое
«призрачное» надгробие): при выгрузке эти грани уходят в слот Vitaria_FX (общая палитра с эмиссией).
"""
import math
import random
import bmesh
from mathutils import Vector, Matrix
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from . import tiles as TL
from . import swamp_assets as SA

STONE = by_normal("stone_light", "stone_mid", "stone_dark", 0.6)        # надгробия: верх светлее, низ в тени
DARK_STONE = by_normal("stone_mid", "stone_dark", "stone_dark", 0.6)     # стены часовни, столбы ограды
IRON = by_normal("iron", "iron_dark", "iron_dark", 0.7)
MOSS = by_normal("moss", "sod_dark", "sod_dark", 0.5)


def _mossy(base):
    """Камень с мхом на макушке: грани, смотрящие вверх, — мох."""
    return lambda f: "moss" if f.normal.z > 0.85 else base(f)


# ---------------------------------------------------------------------------------------
# надгробия
# ---------------------------------------------------------------------------------------
def _plinth(a, w, d, h=0.14, z=0.0):
    a.add(p_box((w, d, h), loc=(0, 0, z + h / 2 - 0.04), bevel=0.03), _mossy(STONE))


def _relief_cross(a, x, y, z, s=1.0, col="stone_dark"):
    """Крест рельефом на лицевой грани (выступ 2 см)."""
    a.add(p_box((0.07 * s, 0.04, 0.34 * s), loc=(x, y, z), bevel=0.01), col)
    a.add(p_box((0.22 * s, 0.04, 0.07 * s), loc=(x, y, z + 0.07 * s), bevel=0.01), col)


def headstone(a, seed=1, w=0.62, h=0.95, d=0.18, top="round", tilt=(0.0, 0.0)):
    """Надгробие на цоколе: плита со скруглённым (round), треугольным (gable) или прямым (flat) верхом,
    рельеф-крест; tilt — завал плиты (град)."""
    rng = random.Random(seed)
    _plinth(a, w + 0.2, d + 0.26, 0.16)
    m = Matrix.Translation((0, 0, 0.1)) @ Matrix.Rotation(math.radians(tilt[0]), 4, "X") @ \
        Matrix.Rotation(math.radians(tilt[1]), 4, "Y")
    body_h = h - (w / 2 if top == "round" else (0.22 if top == "gable" else 0.0))
    parts = [p_box((w, d, body_h), loc=(0, 0, body_h / 2), bevel=0.035)]
    if top == "round":
        parts.append(p_cyl(w / 2, w / 2, d, 10, loc=(0, d / 2, body_h), rot=(90, 0, 0)))
    elif top == "gable":
        parts.append(p_prism([(-w / 2 - 0.03, 0), (w / 2 + 0.03, 0), (0, 0.24)], d + 0.02, loc=(0, 0, body_h)))
    for part in parts:
        bmesh.ops.transform(part, matrix=m, verts=part.verts)
        a.add(part, STONE)
    for size, loc in (((0.07, 0.04, 0.32), (0, -d / 2 - 0.01, body_h * 0.55)),        # рельеф-крест
                      ((0.22, 0.04, 0.07), (0, -d / 2 - 0.01, body_h * 0.55 + 0.06))):
        b = p_box(size, loc=loc, bevel=0.01)
        bmesh.ops.transform(b, matrix=m, verts=b.verts)
        a.add(b, "stone_dark")
    if rng.random() < 0.7:                               # мох у цоколя
        a.add(p_ico(0.16, 1, loc=(rng.uniform(-0.2, 0.2), -d / 2 - 0.12, 0.02), scl=(1.4, 0.8, 0.45), jitter=0.15,
                    rng=rng, cut=-0.02), MOSS)


def stone_cross(a, seed=3, h=1.25, wood=False):
    """Крест на цоколе: каменный (с утолщением на перекрестье) или деревянный, чуть завален."""
    rng = random.Random(seed)
    col = by_normal("wood_mid", "wood_dark", "wood_dark", 0.6) if wood else STONE
    _plinth(a, 0.5, 0.4, 0.18)
    lean = Matrix.Rotation(math.radians(rng.uniform(-6, 6)), 4, "Y") @ Matrix.Rotation(math.radians(rng.uniform(-4, 4)), 4, "X")
    for size, loc in (((0.17, 0.15, h), (0, 0, h / 2 + 0.1)), ((0.62, 0.15, 0.16), (0, 0, h * 0.72 + 0.1))):
        b = p_box(size, loc=loc, bevel=0.03)
        bmesh.ops.transform(b, matrix=lean, verts=b.verts)
        a.add(b, col)
    if not wood:
        b = p_box((0.25, 0.19, 0.25), loc=(0, 0, h * 0.72 + 0.1), rot=(0, 45, 0), bevel=0.03)
        bmesh.ops.transform(b, matrix=lean, verts=b.verts)
        a.add(b, "stone_light")


def tall_stone(a, seed=5):
    """Высокое надгробие с крестом на макушке: ступенчатый цоколь, плита со щипцом, крест."""
    rng = random.Random(seed)
    _plinth(a, 0.95, 0.62, 0.16)
    a.add(p_box((0.78, 0.48, 0.16), loc=(0, 0, 0.2), bevel=0.03), STONE)
    a.add(p_box((0.62, 0.3, 1.0), loc=(0, 0, 0.78), bevel=0.04), STONE)
    a.add(p_prism([(-0.36, 0), (0.36, 0), (0, 0.22)], 0.34, loc=(0, 0, 1.28)), STONE)
    a.add(p_box((0.1, 0.1, 0.42), loc=(0, 0, 1.68), bevel=0.02), "stone_light")
    a.add(p_box((0.3, 0.1, 0.09), loc=(0, 0, 1.76), bevel=0.02), "stone_light")
    _relief_cross(a, 0, -0.16, 0.66, 1.1)
    a.add(p_ico(0.2, 1, loc=(0.25, -0.3, 0.02), scl=(1.4, 0.8, 0.4), jitter=0.15, rng=rng, cut=-0.02), MOSS)


def monument(a, seed=7):
    """Памятник-столп с крестом (сзади справа на макете): цоколь, ствол с карнизом, щипцовая шапка, крест."""
    rng = random.Random(seed)
    a.add(p_box((1.05, 1.05, 0.3), loc=(0, 0, 0.11), bevel=0.04), _mossy(STONE))
    a.add(p_box((0.82, 0.82, 0.22), loc=(0, 0, 0.36), bevel=0.04), STONE)
    a.add(p_box((0.62, 0.62, 1.7), loc=(0, 0, 1.32), bevel=0.04), DARK_STONE)
    a.add(p_box((0.2, 0.06, 0.95), loc=(0, -0.33, 1.35), bevel=0.02), "stone_dark")          # ниша
    a.add(p_box((0.78, 0.78, 0.14), loc=(0, 0, 2.22), bevel=0.03), STONE)                     # карниз
    a.add(p_cyl(0.5, 0.0, 0.42, 4, loc=(0, 0, 2.29), spin=45), STONE)
    a.add(p_box((0.13, 0.13, 0.62), loc=(0, 0, 2.95), bevel=0.02), "stone_light")
    a.add(p_box((0.42, 0.13, 0.12), loc=(0, 0, 3.05), bevel=0.02), "stone_light")
    a.add(p_ico(0.25, 1, loc=(-0.45, -0.45, 0.05), scl=(1.4, 1.0, 0.4), jitter=0.15, rng=rng, cut=-0.02), MOSS)


def glow_stone(a, seed=9):
    """«Призрачное» надгробие (голубое свечение на макете): скруглённая плита, лицевая вставка-руна светится
    (rune — в слот Vitaria_FX), у цоколя свечи."""
    rng = random.Random(seed)
    _plinth(a, 0.92, 0.5, 0.16)
    a.add(p_box((0.72, 0.24, 0.62), loc=(0, 0, 0.42), bevel=0.04), STONE)
    a.add(p_cyl(0.36, 0.36, 0.24, 10, loc=(0, 0.12, 0.73), rot=(90, 0, 0)), STONE)
    a.add(p_box((0.48, 0.04, 0.5), loc=(0, -0.13, 0.5), bevel=0.012), "rune")                 # светящаяся плита
    a.add(p_cyl(0.24, 0.24, 0.04, 8, loc=(0, -0.13, 0.75), rot=(90, 0, 0)), "rune")
    _relief_cross(a, 0, -0.16, 0.48, 1.0, "stone_light")
    candles(a, seed + 1, n=4, ring=0.24, centre=(0.0, -0.42))


def sarcophagus(a, seed=11):
    """Саркофаг: цоколь, короб, крышка шире короба со скосом, рельеф-крест, мох по углам."""
    rng = random.Random(seed)
    a.add(p_box((2.05, 1.05, 0.18), loc=(0, 0, 0.05), bevel=0.04), _mossy(STONE))
    a.add(p_box((1.8, 0.82, 0.5), loc=(0, 0, 0.38), bevel=0.04), DARK_STONE)
    a.add(p_box((1.96, 0.98, 0.14), loc=(0, 0, 0.68), bevel=0.04), STONE)
    a.add(p_box((1.7, 0.7, 0.12), loc=(0, 0, 0.8), bevel=0.04), STONE)
    a.add(p_box((1.0, 0.1, 0.07), loc=(0, 0, 0.88), bevel=0.015), "stone_dark")
    a.add(p_box((0.1, 0.42, 0.07), loc=(-0.2, 0, 0.88), bevel=0.015), "stone_dark")
    for sx, sy in ((-1, -1), (1, 1)):
        a.add(p_ico(0.2, 1, loc=(sx * 0.95, sy * 0.48, 0.74), scl=(1.3, 1.0, 0.35), jitter=0.15, rng=rng, cut=-0.02),
              MOSS)


# ---------------------------------------------------------------------------------------
# ограда
# ---------------------------------------------------------------------------------------
def fence_pillar(a, seed=13, top="cap", h=1.55):
    """Столб ограды: цоколь, ствол, карниз, шапка-пирамида; top="cross" — крест, "lamp" — фонарь-ниша."""
    rng = random.Random(seed)
    a.add(p_box((0.62, 0.62, 0.28), loc=(0, 0, 0.1), bevel=0.04), _mossy(STONE))
    a.add(p_box((0.46, 0.46, h - 0.4), loc=(0, 0, 0.24 + (h - 0.4) / 2), bevel=0.035), DARK_STONE)
    a.add(p_box((0.6, 0.6, 0.12), loc=(0, 0, h - 0.1), bevel=0.03), STONE)
    a.add(p_cyl(0.4, 0.0, 0.3, 4, loc=(0, 0, h - 0.04), spin=45), STONE)
    if top == "cross":
        a.add(p_box((0.1, 0.1, 0.5), loc=(0, 0, h + 0.4), bevel=0.02), "stone_light")
        a.add(p_box((0.34, 0.1, 0.1), loc=(0, 0, h + 0.47), bevel=0.02), "stone_light")
    elif top == "lamp":                                   # ниша с огнём под карнизом (лицом к -Y)
        a.add(p_box((0.26, 0.06, 0.34), loc=(0, -0.22, h - 0.55), bevel=0.012), "lantern_glow")
        a.add(p_box((0.34, 0.08, 0.06), loc=(0, -0.235, h - 0.36), bevel=0.012), "stone_light")


FENCE_SEG = 1.0


def iron_fence(a, seed=15):
    """Кованая ограда, сегмент 1 м (как Prop_Fence кита: вдоль X, лицом к -Y): две перекладины, пять прутьев
    с пиками, у некоторых прут погнут."""
    rng = random.Random(seed)
    for z in (0.26, 0.94):                               # без фасок: ограды много, держим треугольники
        a.add(p_box((FENCE_SEG + 0.02, 0.06, 0.06), loc=(0, 0, z), bevel=0.0), IRON)
    for k in range(5):
        x = -0.4 + 0.2 * k
        h = 1.16 + (0.08 if k % 2 == 0 else 0.0)
        bend = rng.uniform(-3.5, 3.5) if rng.random() < 0.35 else 0.0
        m = Matrix.Translation((x, 0, 0)) @ Matrix.Rotation(math.radians(bend), 4, "Y")
        b = p_box((0.06, 0.06, h), loc=(0, 0, h / 2 - 0.05), bevel=0.0)
        bmesh.ops.transform(b, matrix=m, verts=b.verts)
        a.add(b, "iron_dark")
        b = p_cyl(0.075, 0.0, 0.17, 4, loc=(0, 0, h - 0.06), spin=45)
        bmesh.ops.transform(b, matrix=m, verts=b.verts)
        a.add(b, "iron")


def ruin_wall(a, seed=17, L=2.4):
    """Руина низкой стены: ряды тёсаных блоков с выпавшими камнями, мох по верху, обломки у подножия."""
    rng = random.Random(seed)
    x = -L / 2
    row_h = 0.24
    for r in range(3):
        x = -L / 2 + (0.2 if r % 2 else 0.0)
        end = L / 2 - (0.5 if r == 2 else 0.0) - rng.uniform(0.0, 0.3) * r
        while x < end:
            w = min(rng.uniform(0.38, 0.62), end - x)
            if w > 0.16 and not (r == 2 and rng.random() < 0.3):
                a.add(p_box((w - 0.03, 0.42 - 0.04 * r, row_h - 0.02), loc=(x + w / 2, rng.uniform(-0.02, 0.02),
                                                                               r * row_h + row_h / 2 - 0.04),
                            rot=(0, 0, rng.uniform(-3, 3)), bevel=0.03), _mossy(DARK_STONE))
            x += w
    for k in range(3):
        a.add(p_box((rng.uniform(0.2, 0.32),) * 2 + (0.16,), loc=(rng.uniform(-L / 2, L / 2), -0.42 - rng.uniform(0, 0.2), 0.05),
                    rot=(rng.uniform(-10, 10), rng.uniform(-10, 10), rng.uniform(0, 90)), bevel=0.03), STONE)


# ---------------------------------------------------------------------------------------
# свет: фонари, свечи, факел
# ---------------------------------------------------------------------------------------
def _lantern_head(a, z, s=1.0, x=0.0):
    """Фонарь: поддон, четыре стойки, светящийся короб, шатёр с навершием (x — сдвиг вбок, для кронштейна)."""
    a.add(p_box((0.3 * s, 0.3 * s, 0.05 * s), loc=(x, 0, z + 0.025 * s), bevel=0.012), "iron_dark")
    a.add(p_box((0.22 * s, 0.22 * s, 0.3 * s), loc=(x, 0, z + 0.2 * s), bevel=0.0), "lantern_glow")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.06 * s, 0.06 * s, 0.32 * s), loc=(x + sx * 0.12 * s, sy * 0.12 * s, z + 0.2 * s), bevel=0.0),
                  "iron_dark")
    a.add(p_cyl(0.25 * s, 0.0, 0.2 * s, 4, loc=(x, 0, z + 0.36 * s), spin=45), IRON)
    a.add(p_ico(0.05 * s, 1, loc=(x, 0, z + 0.58 * s)), "iron_dark")


def lantern_post(a, seed=19):
    """Фонарь на столбике (спереди справа на макете): каменная тумба, железный столбик, фонарь сверху."""
    a.add(p_box((0.34, 0.34, 0.22), loc=(0, 0, 0.07), bevel=0.03), _mossy(STONE))
    a.add(p_box((0.09, 0.09, 1.0), loc=(0, 0, 0.66), bevel=0.015), "iron_dark")
    a.add(p_box((0.16, 0.16, 0.08), loc=(0, 0, 1.16), bevel=0.015), "iron")
    _lantern_head(a, 1.2)


def lamp_post(a, seed=21):
    """Фонарь на кривом столбе (слева у мостков): столб, кронштейн с завитком, фонарь на крюке."""
    rng = random.Random(seed)
    a.add(p_box((0.42, 0.42, 0.24), loc=(0, 0, 0.08), bevel=0.03), _mossy(STONE))
    a.add(p_box((0.1, 0.1, 2.2), loc=(0, 0, 1.2), bevel=0.015), "iron_dark")
    SA._limb(a, (0, 0, 2.25), (0.35, 0, 2.42), 0.045, 0.045, "iron_dark", seg=4)
    SA._limb(a, (0.35, 0, 2.42), (0.62, 0, 2.36), 0.045, 0.04, "iron_dark", seg=4)
    a.add(p_ico(0.07, 1, loc=(0.0, 0, 2.32)), "iron")
    a.add(p_box((0.06, 0.06, 0.2), loc=(0.62, 0, 2.24), bevel=0.0), "iron_dark")
    _lantern_head(a, 1.62, 0.95, x=0.62)                 # фонарь висит на конце кронштейна


def candles(a, seed=23, n=5, ring=0.18, centre=(0.0, 0.0)):
    """Свечи кучкой: восковые столбики разной высоты (bone), натёки, огоньки (glow_hot -> Vitaria_FX)."""
    rng = random.Random(seed)
    cx, cy = centre
    a.add(p_cyl(ring + 0.08, ring + 0.06, 0.03, 8, loc=(cx, cy, -0.01)), "bone")
    for k in range(n):
        ang = math.tau * k / n + rng.uniform(-0.3, 0.3)
        d = ring * (0.3 if k == 0 else rng.uniform(0.6, 1.0))
        x, y = cx + math.cos(ang) * d * (0 if k == 0 else 1), cy + math.sin(ang) * d * (0 if k == 0 else 1)
        h = rng.uniform(0.12, 0.34) if k else 0.38
        r = rng.uniform(0.045, 0.06)
        a.add(p_cyl(r, r * 0.92, h, 6, loc=(x, y, 0.0)), "bone")
        a.add(p_cyl(r * 0.75, 0.0, 0.1, 5, loc=(x, y, h + 0.01)), "glow_hot")
        a.add(p_cyl(0.025, 0.025, 0.03, 4, loc=(x, y, h - 0.005)), "black")


def candle_cluster(a, seed=25):
    candles(a, seed, n=6, ring=0.22)


# ---------------------------------------------------------------------------------------
# деревья, камни, мелочь
# ---------------------------------------------------------------------------------------
DEAD_BARK = by_normal("bark", "bark", "bark_dark", 0.6)


def dead_tree(a, seed=31, height=6.0, girth=1.0, spread=1.0, lean=(0.0, 0.0), arms=4):
    """Кривое сухое дерево кладбища: раструб корней, S-ствол, руки с когтями-кончиками; без листвы и мха — голая
    тёмная кора (SA.swamp_tree без листвы и бород)."""
    SA.swamp_tree(a, seed, height, girth, spread, lean, arms, 0.0, 0.0, 0.0, bark_fn=DEAD_BARK)


def bones(a, seed=33):
    """Кости и череп на траве (bone): две-три кости с утолщениями, череп с глазницами."""
    rng = random.Random(seed)
    for k in range(3):
        ang = rng.uniform(0, math.tau)
        L = rng.uniform(0.32, 0.5)
        c = Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.25, 0.25), 0.04))
        d = Vector((math.cos(ang), math.sin(ang), 0)) * L / 2
        SA._limb(a, c - d, c + d, 0.035, 0.035, "bone", seg=5)
        for e in (c - d, c + d):
            a.add(p_ico(0.06, 1, loc=e, scl=(1.0, 1.0, 0.8)), "bone")
    x, y = rng.uniform(-0.15, 0.15), rng.uniform(-0.1, 0.1)
    a.add(p_ico(0.15, 1, loc=(x, y, 0.13), scl=(1.0, 1.1, 0.9)), "bone")
    a.add(p_box((0.17, 0.12, 0.09), loc=(x, y - 0.11, 0.07), bevel=0.03), "bone")
    for sx in (-1, 1):
        a.add(p_box((0.055, 0.03, 0.05), loc=(x + sx * 0.055, y - 0.155, 0.14), bevel=0.01), "black")


def pumpkins(a, seed=35, n=3):
    """Тыквенная грядка: тыквы долями (кольцо приплюснутых долек), хвостики, листья, плеть по земле."""
    rng = random.Random(seed)
    spots = [(0.0, 0.0, 0.34), (0.62, 0.22, 0.26), (-0.5, 0.35, 0.22), (0.25, -0.5, 0.2)][:n]
    for x, y, r in spots:
        k = 7
        rz = rng.uniform(0, 360)
        for j in range(k):
            ang = math.radians(rz) + math.tau * j / k
            a.add(p_ico(r * 0.62, 1, loc=(x + math.cos(ang) * r * 0.42, y + math.sin(ang) * r * 0.42, r * 0.62),
                        scl=(1.0, 0.75, 1.05), rot=(0, 0, math.degrees(ang) + 90)),
                  lambda f: "leaf_autumn" if f.normal.z > -0.2 else "copper_dark")
        a.add(p_cyl(0.05, 0.035, 0.16, 5, loc=(x, y, r * 1.15), rot=(rng.uniform(-15, 15), rng.uniform(-15, 15), 0)),
              "wood_dark")
        for j in range(2):
            ang = rng.uniform(0, math.tau)
            a.add(p_ico(0.2, 1, loc=(x + math.cos(ang) * r * 1.1, y + math.sin(ang) * r * 1.1, 0.04),
                        scl=(1.3, 0.9, 0.22), rot=(0, 0, math.degrees(ang))), "leaf_dark")
    pts = [Vector((-0.75, -0.2, 0.03)), Vector((-0.2, 0.1, 0.04)), Vector((0.4, -0.15, 0.03)), Vector((0.85, 0.2, 0.03))]
    for p0, p1 in zip(pts, pts[1:]):
        SA._limb(a, p0, p1, 0.03, 0.03, "leaf_dark", seg=4)


def mossy_rock(a, seed=37, size=0.5, n=2):
    SA.mossy_rock(a, seed, size, n)


def dry_bush(a, seed=42, n=8, h=0.75):
    """Сухой куст: голые прутья веером из кочки, сухая трава у основания (blade — палитры кладбища)."""
    rng = random.Random(seed)
    a.add(p_ico(0.2, 1, loc=(0, 0, 0.0), scl=(1.4, 1.2, 0.45), jitter=0.15, rng=rng, cut=-0.02), MOSS)
    for k in range(n):
        ang = math.tau * k / n + rng.uniform(-0.3, 0.3)
        base = Vector((math.cos(ang) * 0.08, math.sin(ang) * 0.08, 0.02))
        lean = rng.uniform(0.2, 0.5)
        top = base + Vector((math.cos(ang) * lean, math.sin(ang) * lean, rng.uniform(0.6, 1.0) * h))
        SA._limb(a, base, top, 0.035, 0.03, "blade_dark" if k % 2 else "bark", seg=4)
        if rng.random() < 0.6:
            mid = base.lerp(top, rng.uniform(0.45, 0.7))
            tip = mid + Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.uniform(0.12, 0.28)))
            SA._limb(a, mid, tip, 0.03, 0.03, "blade_dark", seg=4)
    for k in range(5):                                    # сухая трава
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0.05, 0.2)
        p0 = Vector((math.cos(ang) * d, math.sin(ang) * d, 0.0))
        p1 = p0 + Vector((math.cos(ang) * 0.12, math.sin(ang) * 0.12, rng.uniform(0.3, 0.5)))
        SA._limb(a, p0, p1, 0.045, 0.0, "blade", seg=3)


def slabs(a, seed=40):
    """Битые плиты дорожки в траве: три-четыре плоских камня вразброс, края в мху."""
    rng = random.Random(seed)
    for k in range(rng.randint(3, 4)):
        w, d = rng.uniform(0.24, 0.42), rng.uniform(0.2, 0.32)
        x, y = rng.uniform(-0.35, 0.35), rng.uniform(-0.25, 0.25)
        a.add(p_box((w, d, 0.07), loc=(x, y, 0.0), rot=(rng.uniform(-3, 3), rng.uniform(-3, 3), rng.uniform(0, 90)),
                    bevel=0.02), by_normal("stone_dark", "rock_dark", "rock_dark", 0.6))


def grave_mound(a, seed=39):
    """Свежая могила: продолговатый холмик земли и деревянный крест в изголовье."""
    rng = random.Random(seed)
    a.add(p_ico(0.6, 1, loc=(0, 0, -0.08), scl=(1.0, 1.6, 0.32), jitter=0.12, rng=rng, cut=0.0),
          by_normal("soil_mid", "soil_dark", "soil_dark", 0.5))
    lean = rng.uniform(-8, 8)
    a.add(p_box((0.09, 0.09, 0.85), loc=(0, 0.95, 0.38), rot=(lean, 0, 0), bevel=0.015), "wood_dark")
    a.add(p_box((0.42, 0.08, 0.08), loc=(0, 0.95, 0.62), rot=(lean, 0, 0), bevel=0.015), "wood_mid")


# ---------------------------------------------------------------------------------------
# постройки
# ---------------------------------------------------------------------------------------
def chapel(a, seed=41):
    """Часовня-склеп (сзади слева на макете): цоколь, тёмные каменные стены с контрфорсами, крутая сланцевая
    крыша щипцом к -Y, арочный вход с огнём внутри (lantern_glow), окно в левой стене, ступени, крест на щипце,
    шпиль на заднем щипце. Пивот — земля под серединой, вход к -Y."""
    rng = random.Random(seed)
    W, D, Hw, Hr, z0 = 2.7, 3.6, 2.45, 4.35, 0.3
    a.add(p_box((W + 0.3, D + 0.3, z0 + 0.06), loc=(0, 0, z0 / 2 - 0.05), bevel=0.04), _mossy(STONE))
    a.add(p_box((W, D, Hw), loc=(0, 0, z0 + Hw / 2), bevel=0.04), DARK_STONE)
    for sx in (-1, 1):                                    # контрфорсы по углам и посередине боковых стен
        for sy in (-1, 0, 1):
            h = Hw * (0.82 if sy else 0.6)
            a.add(p_box((0.36, 0.36 if sy else 0.42, h), loc=(sx * (W / 2 + 0.06), sy * (D / 2 - 0.12), z0 + h / 2),
                        bevel=0.04), STONE)
            a.add(p_prism([(-0.2, 0), (0.2, 0), (0.0, 0.2)], 0.38 if sy else 0.44,
                          loc=(sx * (W / 2 + 0.06), sy * (D / 2 - 0.12), z0 + h), rot=(0, 0, 90)), STONE)
    # щипцы: треугольные призмы поверх стен, по всей длине (держат кровлю)
    gz = z0 + Hw
    a.add(p_prism([(-W / 2, 0), (W / 2, 0), (0, Hr - Hw)], D, loc=(0, 0, gz)), DARK_STONE)
    # кровля: два ската сланца с напуском, конёк
    half = W / 2 + 0.32
    rise = Hr - Hw + 0.18
    ang = math.degrees(math.atan2(rise, half))
    slope = math.hypot(half, rise)
    for sx in (-1, 1):
        a.add(p_box((slope + 0.05, D + 0.5, 0.16), loc=(sx * half / 2, 0, gz + rise / 2 - 0.02), rot=(0, sx * ang, 0),
                    bevel=0.03), by_normal("slate", "slate_dark", "slate_dark", 0.4))
        for k in range(1, 4):                             # ряды сланца — ступеньки по скату
            f = k / 4.0
            a.add(p_box((0.08, D + 0.46, 0.05), loc=(sx * half * (1 - f), 0, gz + rise * f + 0.08),
                        rot=(0, sx * ang, 0), bevel=0.0), "slate_dark")
    a.add(p_box((0.2, D + 0.56, 0.16), loc=(0, 0, gz + rise + 0.02), bevel=0.03), "slate_dark")
    # обрамление щипца спереди (светлый камень по скатам)
    for sx in (-1, 1):
        a.add(p_box((slope - 0.1, 0.22, 0.2), loc=(sx * half / 2 * 0.95, -D / 2 - 0.1, gz + rise / 2 - 0.08),
                    rot=(0, sx * ang, 0), bevel=0.03), "stone_light")
    # вход: арка из светлого камня, внутри огонь
    dz, dw, dh = z0, 1.1, 1.5
    a.add(p_box((dw, 0.06, dh), loc=(0, -D / 2 - 0.02, dz + dh / 2), bevel=0.0), "lantern_glow")
    a.add(p_cyl(dw / 2, dw / 2, 0.06, 10, loc=(0, -D / 2 + 0.01, dz + dh), rot=(90, 0, 0)), "lantern_glow")
    for sx in (-1, 1):
        a.add(p_box((0.22, 0.24, dh + 0.05), loc=(sx * (dw / 2 + 0.1), -D / 2 - 0.06, dz + (dh + 0.05) / 2),
                    bevel=0.03), "stone_light")
    for k in range(7):                                    # арка из клиньев
        t = math.pi * k / 6
        r = dw / 2 + 0.11
        a.add(p_box((0.24, 0.24, 0.2), loc=(math.cos(t) * r, -D / 2 - 0.06, dz + dh + math.sin(t) * r),
                    rot=(0, -math.degrees(t) + 90, 0), bevel=0.025), "stone_light")
    for x in (-0.2, 0.2):                                 # створки распахнуты: тёмные доски по краям проёма
        a.add(p_box((0.08, 0.36, dh - 0.1), loc=(x * 2.2, -D / 2 - 0.2, dz + (dh - 0.1) / 2),
                    rot=(0, 0, 50 if x < 0 else -50), bevel=0.015), "wood_dark")
    a.add(p_box((0.08, 0.08, 1.25), loc=(0, -D / 2 - 0.06, dz + 0.9), bevel=0.0), "wood_dark")   # переплёт
    a.add(p_box((0.7, 0.08, 0.08), loc=(0, -D / 2 - 0.06, dz + 1.1), bevel=0.0), "wood_dark")
    # окно в левой стене (к -X): арка со светом
    a.add(p_box((0.06, 0.5, 0.75), loc=(-W / 2 - 0.02, -0.4, z0 + 1.35), bevel=0.0), "lantern_glow")
    a.add(p_cyl(0.25, 0.25, 0.06, 8, loc=(-W / 2 - 0.05, -0.4, z0 + 1.72), rot=(0, 90, 0)), "lantern_glow")
    a.add(p_box((0.14, 0.7, 0.12), loc=(-W / 2 - 0.06, -0.4, z0 + 0.92), bevel=0.02), "stone_light")      # подоконник
    a.add(p_box((0.1, 0.06, 0.8), loc=(-W / 2 - 0.05, -0.4, z0 + 1.38), bevel=0.0), "iron_dark")         # переплёт
    # ступени
    for k in range(3):
        a.add(p_box((1.9 - 0.2 * k, 0.42, 0.15), loc=(0, -D / 2 - 0.62 + 0.3 * k, 0.03 + 0.12 * k), bevel=0.03),
              _mossy(STONE) if k == 0 else STONE)
    # крест на переднем щипце, шпиль на заднем
    a.add(p_box((0.14, 0.14, 0.75), loc=(0, -D / 2 - 0.05, gz + rise + 0.4), bevel=0.02), "stone_light")
    a.add(p_box((0.46, 0.14, 0.13), loc=(0, -D / 2 - 0.05, gz + rise + 0.55), bevel=0.02), "stone_light")
    a.add(p_box((0.34, 0.34, 0.5), loc=(0, D / 2 - 0.05, gz + rise + 0.2), bevel=0.03), STONE)
    a.add(p_cyl(0.26, 0.0, 0.55, 4, loc=(0, D / 2 - 0.05, gz + rise + 0.44), spin=45), "slate_dark")
    # мох и обломки у стен
    for k in range(5):
        x = rng.choice((-1, 1)) * (W / 2 + 0.25) if k % 2 else rng.uniform(-W / 2, W / 2)
        y = rng.uniform(-D / 2, D / 2) if k % 2 else D / 2 + 0.25
        a.add(p_ico(rng.uniform(0.16, 0.26), 1, loc=(x, y, 0.02), scl=(1.4, 1.0, 0.45), jitter=0.15, rng=rng,
                    cut=-0.02), MOSS)


def coffin_cart(a, seed=43):
    """Телега с гробом (справа на макете): кузов на оси, два колеса со спицами, оглобли, на кузове — гроб
    шестигранником с крестом на крышке. Вдоль X, пивот — земля под серединой кузова."""
    rng = random.Random(seed)
    L, Wd, zb = 2.1, 1.15, 0.62
    a.add(p_box((L, Wd, 0.1), loc=(0, 0, zb), bevel=0.02), by_normal("wood_mid", "wood_dark", "wood_dark", 0.5))
    for sy in (-1, 1):
        a.add(p_box((L, 0.08, 0.26), loc=(0, sy * (Wd / 2 - 0.02), zb + 0.16), bevel=0.02), "wood_mid2")
        for x in (-0.95, -0.3, 0.35, 0.95):
            a.add(p_box((0.08, 0.1, 0.3), loc=(x, sy * (Wd / 2 + 0.02), zb + 0.14), bevel=0.015), "wood_dark")
    a.add(p_box((0.08, Wd, 0.26), loc=(-L / 2 + 0.02, 0, zb + 0.16), bevel=0.02), "wood_mid2")
    a.add(p_box((0.1, Wd + 0.5, 0.1), loc=(0.15, 0, 0.45), rot=(90, 0, 0), bevel=0.0), "iron_dark")     # ось
    for sy in (-1, 1):                                                    # колёса
        y = sy * (Wd / 2 + 0.17)
        a.add(p_cyl(0.48, 0.48, 0.09, 12, loc=(0.15, y - sy * 0.045, 0.45), rot=(90, 0, 0)),
              lambda f: "iron_dark" if abs(f.normal.y) < 0.5 else "wood_dark")
        for k in range(6):
            ang = math.radians(30 * k)
            a.add(p_box((0.72, 0.06, 0.06), loc=(0.15, y, 0.45), rot=(0, math.degrees(ang), 0), bevel=0.0), "wood_mid")
        a.add(p_cyl(0.11, 0.11, 0.16, 8, loc=(0.15, y - sy * 0.08, 0.45), rot=(90, 0, 0)), "iron")
    for sy in (-1, 1):                                                    # оглобли к земле
        SA._limb(a, (L / 2 - 0.1, sy * 0.38, zb - 0.02), (L / 2 + 1.25, sy * 0.3, 0.06), 0.05, 0.045, "wood_dark", seg=5)
    a.add(p_box((0.08, 0.7, 0.08), loc=(L / 2 + 1.1, 0, 0.12), bevel=0.0), "wood_dark")
    # гроб: шестигранник в плане (узкое изголовье, плечи, сужение к ногам), вытянут по X
    pts = [(-0.85, -0.17), (-0.5, -0.3), (0.85, -0.2), (0.85, 0.2), (-0.5, 0.3), (-0.85, 0.17)]
    for zz, h, scale, col in ((zb + 0.05, 0.36, 1.0, "wood_dark"), (zb + 0.41, 0.08, 1.06, "slate_dark")):
        bm = bmesh.new()
        lo = [bm.verts.new((x * scale, y * scale, zz)) for x, y in pts]
        hi = [bm.verts.new((x * scale, y * scale, zz + h)) for x, y in pts]
        bm.faces.new(list(reversed(lo)))
        bm.faces.new(hi)
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.normal_update()
        a.add(bm, col)
    a.add(p_box((0.7, 0.08, 0.04), loc=(0.05, 0, zb + 0.51), bevel=0.0), "iron")
    a.add(p_box((0.08, 0.36, 0.04), loc=(-0.12, 0, zb + 0.51), bevel=0.0), "iron")


def ladder(a, seed=45, L=3.6):
    """Лестница с кромки: две тетивы и перекладины; пивот — верх (на кромке), висит вниз и чуть отходит от скалы."""
    rng = random.Random(seed)
    tilt = 4.0
    m = Matrix.Rotation(math.radians(-tilt), 4, "X")
    for sx in (-1, 1):
        b = p_box((0.09, 0.09, L), loc=(sx * 0.27, 0, -L / 2 + 0.25), bevel=0.015)
        bmesh.ops.transform(b, matrix=m, verts=b.verts)
        a.add(b, "wood_dark")
    n = int(L / 0.42)
    for k in range(n):
        if k == n - 2:                                     # выломанная перекладина
            continue
        b = p_box((0.62, 0.07, 0.07), loc=(0, 0, 0.1 - k * 0.42), rot=(0, rng.uniform(-4, 4), 0), bevel=0.012)
        bmesh.ops.transform(b, matrix=m, verts=b.verts)
        a.add(b, "wood_mid")


def stairs(a, seed=47):
    """Деревянные ступени с кромки вниз (спереди слева на макете): помост на сваях у кромки, марш из трёх
    ступеней вниз вдоль -Y, тетивы, столбы. Пивот — верх помоста у кромки."""
    rng = random.Random(seed)
    a.add(p_box((1.3, 0.9, 0.1), loc=(0, -0.2, 0.01), bevel=0.02), by_normal("wood_light", "wood_mid", "wood_dark", 0.5))
    for k in range(4):
        z = -0.32 - k * 0.3
        y = -0.85 - k * 0.32
        a.add(p_box((1.1, 0.34, 0.08), loc=(rng.uniform(-0.03, 0.03), y, z), rot=(0, 0, rng.uniform(-3, 3)),
                    bevel=0.015), "wood_mid" if k % 2 else "wood_mid2")
    for sx in (-1, 1):
        SA._limb(a, (sx * 0.58, -0.6, -0.12), (sx * 0.58, -2.25, -1.42), 0.055, 0.055, "wood_dark", seg=4)
        for y, top in ((-0.6, 0.35), (-2.2, -0.8)):
            a.add(p_box((0.11, 0.11, 2.6), loc=(sx * 0.62, y, top - 1.3), bevel=0.015), "wood_dark")


def pier(a, seed=49):
    """Мостки через пруд (секция 2 м, как у «Болота»): доски поперёк, лаги, сваи."""
    SA.boardwalk(a, seed, posts_up=True)


PIER_L = SA.BOARD_L

ASSETS = [
    ("Graveyard", "Grave_Chapel", "Часовня-склеп", chapel),
    ("Graveyard", "Grave_DeadTree_A", "Кривое сухое дерево", lambda a: dead_tree(a, 31, 6.4, 0.68, 1.15, (0.05, 0.0), 4)),
    ("Graveyard", "Grave_DeadTree_B", "Кривое сухое дерево, наклонное",
     lambda a: dead_tree(a, 32, 5.6, 0.62, 1.05, (0.12, 0.04), 3)),
    ("Graveyard", "Grave_DeadTree_C", "Сухое деревце", lambda a: dead_tree(a, 33, 2.8, 0.6, 0.85, (0.0, 0.06), 3)),
    ("Graveyard", "Grave_Stone_A", "Надгробие скруглённое", lambda a: headstone(a, 1, 0.62, 0.95, 0.18, "round", (-4, 2))),
    ("Graveyard", "Grave_Stone_B", "Надгробие со щипцом", lambda a: headstone(a, 2, 0.66, 1.05, 0.2, "gable", (3, -3))),
    ("Graveyard", "Grave_Stone_C", "Надгробие покосившееся", lambda a: headstone(a, 3, 0.56, 0.78, 0.18, "flat", (-12, 8))),
    ("Graveyard", "Grave_Stone_Tall", "Высокое надгробие с крестом", tall_stone),
    ("Graveyard", "Grave_Stone_Glow", "Надгробие с голубым свечением и свечами", glow_stone),
    ("Graveyard", "Grave_Cross", "Каменный крест", lambda a: stone_cross(a, 3, 1.25)),
    ("Graveyard", "Grave_Cross_Wood", "Деревянный крест", lambda a: stone_cross(a, 4, 1.1, wood=True)),
    ("Graveyard", "Grave_Monument", "Памятник-столп с крестом", monument),
    ("Graveyard", "Grave_Sarcophagus", "Саркофаг", sarcophagus),
    ("Graveyard", "Grave_Mound", "Могильный холмик с крестом", grave_mound),
    ("Graveyard", "Grave_Pillar", "Столб ограды", lambda a: fence_pillar(a, 13, "cap")),
    ("Graveyard", "Grave_Pillar_Cross", "Столб ограды с крестом", lambda a: fence_pillar(a, 14, "cross")),
    ("Graveyard", "Grave_Pillar_Lamp", "Столб ограды с огнём в нише", lambda a: fence_pillar(a, 15, "lamp", 1.7)),
    ("Graveyard", "Grave_Fence", "Кованая ограда, сегмент 1 м", iron_fence),
    ("Graveyard", "Grave_Wall", "Руина низкой стены", ruin_wall),
    ("Graveyard", "Grave_Lantern", "Фонарь на столбике", lantern_post),
    ("Graveyard", "Grave_LampPost", "Фонарь на кривом столбе", lamp_post),
    ("Graveyard", "Grave_Candles", "Свечи", candle_cluster),
    ("Graveyard", "Grave_Torch", "Факел на шесте", SA.torch),
    ("Graveyard", "Grave_CoffinCart", "Телега с гробом", coffin_cart),
    ("Graveyard", "Grave_Bones", "Кости и череп", bones),
    ("Graveyard", "Grave_Pumpkins", "Тыквенная грядка", pumpkins),
    ("Graveyard", "Grave_Slabs", "Битые плиты в траве", slabs),
    ("Graveyard", "Grave_DryBush", "Сухой куст", dry_bush),
    ("Graveyard", "Grave_Rock_A", "Замшелый валун", lambda a: mossy_rock(a, 37, 0.55, 2)),
    ("Graveyard", "Grave_Rock_B", "Замшелый камень", lambda a: mossy_rock(a, 38, 0.3, 1)),
    ("Graveyard", "Grave_Ladder", "Лестница с кромки", ladder),
    ("Graveyard", "Grave_Stairs", "Ступени с кромки", stairs),
    ("Graveyard", "Grave_Pier", "Мостки через пруд, секция 2 м", pier),
    # плитки поля биома: геометрия и UV — как у Hex_Tile_A/B/C, цвет — палитра Vitaria_Palette_Graveyard (камень)
    ("Graveyard", "Hex_Tile_Grave_A", "Гекс-плитка кладбища A", TL.tile_plain(11, base=0.5)),
    ("Graveyard", "Hex_Tile_Grave_B", "Гекс-плитка кладбища B (мох)", TL.tile_tufts),
    ("Graveyard", "Hex_Tile_Grave_C", "Гекс-плитка кладбища C (камни)", TL.tile_pebbles),
    ("Preview", "Preview_Troll", "Тролль-заглушка (превью)", SA.preview_troll),
    ("Preview", "Preview_Goblin", "Гоблин-заглушка (превью)", SA.preview_goblin),
]
