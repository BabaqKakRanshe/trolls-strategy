"""
Таверна: уютный двухэтажный трактир. Низ каменный, верх бревенчато-дощатый с выносом над
фасадом, длинный синий скат кровли смотрит на камеру. Главные приметы с игровой дистанции:
широкая каменная труба у правого торца с жаром в устье, тёплые окна первого этажа, вывеска
с кружкой на кронштейне у левого угла и стол с кружками и бочки у входа.
Вход — дверь посередине фасада (вход в игре — середина передней кромки следа).
"""
import math
import random

from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from vitaria_buildings.common import (Frame, gable_roof, gable_roof_x, gable_wall_x, cornice, plank_door,
                                      barrel, log_pyramid, STONE_TOP, PLANK_TOP)
from vitaria_buildings.levels import (Shift, quoins, win, fachwerk, dormer, shed_roof, cross_gable, wall_lamp,
                                    porch_post, WALL as AWALL, BEAM, PLASTER, PLANKS)

NAME = "Bld_Tavern"
TITLE = "Таверна"
TARGET = (5.0, 4.3, 4.9)   # след 3x3 при масштабе 0.5

W2, D2 = 3.60, 2.70        # верхний этаж (по нему кровля)
J = 0.16                   # вынос верхнего этажа над фасадом и боками
W, D = W2 - 2 * J, D2 - J  # каменный низ: тыл вровень с верхом
Y1 = J / 2                 # центр низа по Y
YF1 = Y1 - D / 2           # фасад низа
YF2 = -D2 / 2              # фасад верха
F = 0.24                   # верх цоколя = пол
Z1 = 1.62                  # верх каменного этажа
Z2 = 2.94                  # верх бревенчатого этажа
PITCH = 40.0
CX, CY = W2 / 2 + 0.10, 0.30   # ось трубы у правого торца: низ трубы утоплен в стену

WALL = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
TIMBER = by_normal("wood_light", "wood_light", "wood_mid", 0.8)


def _window(a, fr, w=0.46, h=0.52, glass="glass", shutters=True, sill=True):
    """Окно в раме fr (стоит на плоскости стены, локальный -Y — наружу)."""
    fr.box(a, (w + 0.16, 0.14, h + 0.16), (0, 0, 0), col="wood_dark", bevel=0.035)
    fr.box(a, (w, 0.10, h), (0, -0.03, 0), col=glass, bevel=0.0)
    # переплёт крестом: на тёплом стекле без него окно читалось фонарём
    fr.box(a, (0.07, 0.06, h), (0, -0.07, 0), col="wood_dark", bevel=0.0)
    fr.box(a, (w, 0.06, 0.07), (0, -0.07, 0), col="wood_dark", bevel=0.0)
    if shutters:
        for sx in (-1, 1):
            fr.box(a, (0.24, 0.09, h + 0.10), (sx * (w / 2 + 0.16), -0.06, 0), col="roof_dark", bevel=0.025)
    if sill:
        fr.box(a, (w + 0.30, 0.22, 0.10), (0, -0.05, -h / 2 - 0.11), col="stone_light", bevel=0.03)


def _quoins(a):
    """Угловые русты каменного этажа через ряд, выступ 6 см."""
    n, p = 3, 0.06
    hq = (Z1 - F) / n
    for sx in (-1, 1):
        for sy in (-1, 1):
            for i in range(n):
                lx, ly = (0.46, 0.28) if i % 2 == 0 else (0.28, 0.46)
                z0 = F + i * hq + (0.018 if i else -0.02)
                z1 = F + (i + 1) * hq - 0.018
                a.add(p_box((lx, ly, z1 - z0), loc=(sx * (W / 2 + p - lx / 2), Y1 + sy * (D / 2 + p - ly / 2),
                                                    (z0 + z1) / 2), bevel=0.04), "stone_light")


def _timber_floor(a):
    """Верхний этаж: дощатая коробка с выносом, тёмные угловые стойки, пояс-обвязка внизу."""
    zb = Z1 + 0.08
    a.add(p_box((W2, D2, Z2 - zb + 0.02), loc=(0, 0, (zb + Z2) / 2), bevel=0.05), TIMBER)
    # балки выноса: торцы под фасадным свесом — главный знак «верх на консолях»
    for x in (-1.25, -0.42, 0.42, 1.25):
        a.add(p_box((0.16, 0.30, 0.16), loc=(x, YF1 - 0.08, Z1 - 0.02), bevel=0.0), "wood_dark")
    cornice(a, W2, D2, zb + 0.02, t=0.20, col="wood_dark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.18, 0.18, Z2 - zb), loc=(sx * (W2 / 2 - 0.02), sy * (D2 / 2 - 0.02), (zb + Z2) / 2),
                        bevel=0.04), "wood_dark")
    # средние стойки фасада делят доски на панели; в средней панели над дверью — косой крест
    hz = Z2 - zb - 0.1
    for x in (-0.42, 0.42):
        a.add(p_box((0.14, 0.10, hz), loc=(x, YF2 - 0.03, (zb + Z2) / 2), bevel=0.0), "wood_dark")
    L = math.hypot(0.70, hz - 0.1)
    ang = math.degrees(math.atan2(0.70, hz - 0.1))
    for k, s2 in enumerate((1, -1)):
        a.add(p_box((0.11, 0.08, L), loc=(0, YF2 - 0.025 - 0.008 * k, (zb + Z2) / 2), rot=(0, s2 * ang, 0),
                    bevel=0.0), "wood_dark")
    for sx in (-1, 1):
        a.add(p_box((0.10, 0.14, hz), loc=(sx * (W2 / 2 + 0.03), 0.35, (zb + Z2) / 2), bevel=0.0), "wood_dark")
    cornice(a, W2, D2, Z2, t=0.18, col="wood_dark")


def _chimney(a, rng, zr=None):
    """Широкая каменная труба у правого торца: очаг-выступ до второго этажа, ствол выше конька,
    в устье жар (как у горна кузницы — грани ember/glow уходят в слот Vitaria_FX)."""
    zr = zr if zr is not None else _ridge()
    # нижний выступ очага и ствол — с завалом, уступ-«плечо» между ними
    a.add(p_box((0.62, 1.10, 2.10), loc=(CX, CY, 1.03), bevel=0.06), WALL)
    a.add(p_prism([(-0.55, 2.08), (0.55, 2.08), (0.30, 2.42), (-0.30, 2.42)], 0.56, loc=(CX, CY, 0),
                  rot=(0, 0, 90)), WALL)
    top = zr + 0.45
    a.add(p_box((0.56, 0.62, top - 2.30), loc=(CX, CY, (2.30 + top) / 2), bevel=0.05), WALL)
    for z in (1.10, 2.95):                            # светлые пояса кладки
        a.add(p_box((0.60 if z > 2 else 0.66, 0.66 if z > 2 else 1.14, 0.14), loc=(CX, CY, z), bevel=0.03),
              "stone_light")
    # венец и устье
    a.add(p_box((0.74, 0.80, 0.16), loc=(CX, CY, top + 0.02), bevel=0.04), "stone_dark")
    a.add(p_box((0.40, 0.46, 0.04), loc=(CX, CY, top + 0.085), bevel=0.0), "black")
    for dx, dy, r, col in ((-0.07, -0.08, 0.11, "ember"), (0.07, 0.06, 0.10, "glow"), (-0.02, 0.13, 0.08, "glow_hot"),
                           (0.08, -0.12, 0.08, "ember")):
        a.add(p_ico(r, 1, loc=(CX + dx, CY + dy, top + 0.09), scl=(1.0, 1.0, 0.5), jitter=0.18, rng=rng), col)


def _ridge():
    return Z2 + 0.08 + (D2 / 2) * math.tan(math.radians(PITCH))


def _sign(a):
    """Вывеска: кронштейн из-под угловой стойки влево, синяя доска лицом к камере,
    золотая кружка с белой пеной — читается и в иконке."""
    x0, y, zb = -W2 / 2, YF2 - 0.02, 2.62
    a.add(p_box((0.60, 0.12, 0.12), loc=(x0 - 0.24, y, zb), bevel=0.03), "wood_dark")
    a.add(p_box((0.09, 0.09, 0.40), loc=(x0 - 0.13, y, zb - 0.17), rot=(0, -48, 0), bevel=0.0), "wood_dark")
    xs = x0 - 0.31
    for dx in (-0.17, 0.17):
        a.add(p_box((0.05, 0.05, 0.14), loc=(xs + dx, y, zb - 0.12), bevel=0.0), "iron_dark")
    zc = zb - 0.44
    a.add(p_box((0.56, 0.10, 0.46), loc=(xs, y, zc), bevel=0.03), "wood_dark")
    a.add(p_box((0.46, 0.06, 0.36), loc=(xs, y - 0.04, zc), bevel=0.0), "roof")
    # кружка: корпус, ручка-скоба справа, шапка пены с подтёком
    fr = Frame((xs - 0.03, y - 0.075, zc - 0.03))
    fr.box(a, (0.18, 0.03, 0.20), (0, 0, -0.02), col="gold", bevel=0.0)
    fr.box(a, (0.05, 0.03, 0.15), (0.125, 0, -0.02), col="gold", bevel=0.0)
    fr.box(a, (0.07, 0.03, 0.04), (0.10, 0, 0.035), col="gold", bevel=0.0)
    fr.box(a, (0.07, 0.03, 0.04), (0.10, 0, -0.075), col="gold", bevel=0.0)
    fr.prism(a, [(-0.11, 0.07), (0.11, 0.07), (0.11, 0.11), (0.06, 0.15), (-0.02, 0.13), (-0.08, 0.16),
                 (-0.12, 0.11)], 0.035, loc=(0, -0.003, 0.0), col="cream")
    fr.box(a, (0.04, 0.035, 0.07), (-0.07, -0.003, 0.05), col="cream", bevel=0.0)


def _mug(a, fr, r=0.07, h=0.15):
    fr.cyl(a, r, r * 1.08, h, 8, col="wood_light")
    fr.cyl(a, r * 1.12, r * 1.12, 0.03, 8, loc=(0, 0, h * 0.25), col="iron_dark")
    fr.cyl(a, r * 1.12, r * 1.02, 0.05, 8, loc=(0, 0, h - 0.005), col="cream")
    fr.box(a, (0.05, 0.04, h * 0.6), (r + 0.025, 0, h * 0.5), col="wood_mid", bevel=0.0)


def _table(a, fr):
    """Стол с двумя скамьями вдоль X, на столе кружки и блюдо с хлебом."""
    top = PLANK_TOP
    fr.box(a, (1.10, 0.58, 0.08), (0, 0, 0.56), col=top, bevel=0.03)
    for sy in (-1, 1):
        fr.box(a, (1.00, 0.24, 0.07), (0, sy * 0.52, 0.32), col=top, bevel=0.025)       # скамья
    for sx in (-1, 1):
        fr.box(a, (0.10, 0.44, 0.10), (sx * 0.40, 0, 0.06), col="wood_dark", bevel=0.0)
        fr.box(a, (0.10, 0.10, 0.50), (sx * 0.40, 0, 0.30), col="wood_dark", bevel=0.0)
        for sy in (-1, 1):
            fr.box(a, (0.09, 0.20, 0.30), (sx * 0.36, sy * 0.52, 0.15), col="wood_dark", bevel=0.0)
    for x, y, rz in ((-0.32, -0.10, 0), (0.06, 0.12, 40), (0.34, -0.12, 160)):
        _mug(a, fr.sub((x, y, 0.60), rz=rz))
    fr.cyl(a, 0.16, 0.16, 0.03, 10, loc=(-0.06, -0.08, 0.60), col="iron_light")
    fr.ico(a, 0.11, loc=(-0.06, -0.08, 0.64), scl=(1.3, 0.85, 0.6), col=by_normal("wood_yellow", "wood_light"),
           cut=-0.02)


def _lying_barrel(a, fr, r=0.24, L=0.52):
    """Бочка на боку вдоль X рамы на козлах-подкладках, с краником на торце."""
    inv = fr.m.to_3x3().inverted()
    col = lambda f: "wood_pale" if abs((inv @ f.normal).normalized().x) > 0.75 else "wood_mid"
    fr.cyl(a, r, r, L, 12, loc=(-L / 2, 0, r + 0.06), rot=(0, 90, 0), col=col, bevel=0.03)
    for t in (0.2, 0.8):
        fr.cyl(a, r + 0.025, r + 0.025, 0.06, 12, loc=(-L / 2 + L * t - 0.03, 0, r + 0.06), rot=(0, 90, 0),
               col="iron_dark")
    for sx in (-1, 1):
        fr.box(a, (0.12, 0.50, 0.12), (sx * L * 0.3, 0, 0.06), col="wood_dark", bevel=0.0)
    fr.box(a, (0.10, 0.06, 0.06), (-L / 2 - 0.05, 0, r - 0.02), col="gold", bevel=0.0)
    fr.box(a, (0.05, 0.05, 0.08), (-L / 2 - 0.09, 0, r - 0.07), col="gold", bevel=0.0)


def build(a):
    rng = random.Random(11)

    # ---- терраса перед входом и цоколь --------------------------------------------------------
    a.add(p_box((W2 + 0.10, 1.50, 0.14), loc=(0, YF1 - 0.75, 0.03), bevel=0.04), STONE_TOP)
    a.add(p_box((W + 0.24, D + 0.24, F + 0.14), loc=(0, Y1, (F - 0.14) / 2), bevel=0.06),
          by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    for x in (-0.55, 0.55):                          # швы мощения: иначе плита читается столом
        a.add(p_box((0.05, 1.40, 0.02), loc=(x, YF1 - 0.75, 0.105), bevel=0.0), "stone_dark")

    # ---- каменный этаж -------------------------------------------------------------------------
    a.add(p_box((W, D, Z1 - F + 0.04), loc=(0, Y1, (F + Z1) / 2), bevel=0.05), "stone_mid")
    _quoins(a)
    plank_door(a, YF1, F, w=0.70, h=1.22, col="wood_light", frame="wood_dark")
    a.add(p_box((1.00, 0.36, 0.12), loc=(0, YF1 - 0.18, F - 0.06), bevel=0.03), "stone_light")    # порог
    # тёплые окна первого этажа: светятся (lantern_glow -> Vitaria_FX), это и есть «уют»
    for x in (-1.02, 1.02):
        _window(a, Frame((x, YF1, 0.98)), w=0.52, h=0.50, glass="lantern_glow", shutters=False)
    _window(a, Frame((-W / 2, Y1 - 0.45, 0.98), rz=-90), w=0.48, h=0.46, glass="lantern_glow", shutters=False)

    # ---- бревенчатый этаж, окна с синими ставнями ----------------------------------------------
    _timber_floor(a)
    for x in (-1.05, 1.05):
        _window(a, Frame((x, YF2, 2.38)), w=0.44, h=0.50, sill=False)
    _window(a, Frame((-W2 / 2, -0.45, 2.38), rz=-90), w=0.44, h=0.50, sill=False)
    _window(a, Frame((0.55, D2 / 2, 2.38), rz=180), w=0.44, h=0.50, sill=False)       # тыл не глухой

    # ---- кровля: конёк вдоль X, длинный синий скат к фасаду; фронтоны дощатые ---------------------
    zr = gable_roof_x(a, W2, D2, Z2, pitch_deg=PITCH, ox=0.22, oy=0.26)
    gable_wall_x(a, W2, D2, Z2 - 0.02, zr, col="wood_mid", inset=0.05, depth=0.14)
    a.add(p_box((0.40, 0.12, 0.34), loc=(-W2 / 2 - 0.03, 0, Z2 + 0.42), rot=(0, 0, 90), bevel=0.03), "wood_dark")
    a.add(p_box((0.08, 0.26, 0.22), loc=(-W2 / 2 - 0.06, 0, Z2 + 0.42), bevel=0.0), "lantern_glow")

    _chimney(a, rng)
    _sign(a)

    # ---- у входа: слева стол со скамьями, справа бочки -------------------------------------------
    _table(a, Frame((-1.12, YF1 - 0.80, 0.10)))
    barrel(a, (0.98, YF1 - 0.36, 0.10), r=0.23, h=0.50)
    barrel(a, (1.46, YF1 - 0.42, 0.10), r=0.23, h=0.50)
    a.add(p_cyl(0.20, 0.20, 0.02, 12, loc=(1.46, YF1 - 0.42, 0.60)), "wood_pale")
    _mug(a, Frame((1.40, YF1 - 0.44, 0.62)))
    _lying_barrel(a, Frame((1.38, YF1 - 0.96, 0.10), rz=-8))
    # поленница у очага трубы: топливо для камина, заодно уравновешивает вывеску слева
    # (габарит по X симметричен — дверь ровно посередине следа)
    log_pyramid(a, Frame((2.21, -0.62, 0.0), rz=90), L=0.74, r=0.11, rows=(2, 1), stakes=False)


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): трактир -> постоялый двор -> гостиница в три этажа
# =========================================================================================
def _base(a, rng):
    """Терраса, цоколь, каменный этаж с дверью и тёплыми окнами, бревенчатый этаж — как у уровня 1."""
    a.add(p_box((W2 + 0.10, 1.50, 0.14), loc=(0, YF1 - 0.75, 0.03), bevel=0.04), STONE_TOP)
    a.add(p_box((W + 0.24, D + 0.24, F + 0.14), loc=(0, Y1, (F - 0.14) / 2), bevel=0.06),
          by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    for x in (-0.55, 0.55):
        a.add(p_box((0.05, 1.40, 0.02), loc=(x, YF1 - 0.75, 0.105), bevel=0.0), "stone_dark")
    a.add(p_box((W, D, Z1 - F + 0.04), loc=(0, Y1, (F + Z1) / 2), bevel=0.05), "stone_mid")
    _quoins(a)
    plank_door(a, YF1, F, w=0.70, h=1.22, col="wood_light", frame="wood_dark")
    a.add(p_box((1.00, 0.36, 0.12), loc=(0, YF1 - 0.18, F - 0.06), bevel=0.03), "stone_light")
    for x in (-1.02, 1.02):
        _window(a, Frame((x, YF1, 0.98)), w=0.52, h=0.50, glass="lantern_glow", shutters=False)
    _timber_floor(a)
    for x in (-1.05, 1.05):
        _window(a, Frame((x, YF2, 2.38)), w=0.44, h=0.50, sill=False)


def _flower_box(a, x, y, z, w=0.62):
    """Ящик с цветами под окном: доска и пёстрые шапки цветов."""
    a.add(p_box((w, 0.20, 0.16), loc=(x, y - 0.10, z), bevel=0.0), "wood_dark")
    for k in range(5):
        xx = x - w / 2 + 0.08 + k * (w - 0.16) / 4
        col = ("flower_y", "berry", "leaf_light", "berry", "flower_y")[k]
        a.add(p_ico(0.08, 0, loc=(xx, y - 0.12, z + 0.11), scl=(1.0, 1.0, 0.8)), col)


def _wing(a, two_storey=False):
    """Пристройка-кухня у левого торца: каменная коробка с окном, односкатная кровля от стены
    (two_storey — второй этаж фахверком и своя двускатная кровля коньком вдоль Y)."""
    x0, x1, y0, y1 = -2.48, -W / 2, -0.70, 1.20
    zw = 1.70
    a.add(p_box((x1 - x0, y1 - y0, zw), loc=((x0 + x1) / 2, (y0 + y1) / 2, zw / 2 - 0.04), bevel=0.04), AWALL)
    quoins(a, (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0, 0.0, zw, corners=((-1, -1),))
    _window(a, Frame(((x0 + x1) / 2, y0, 1.00)), w=0.40, h=0.40, glass="lantern_glow", shutters=False)
    if not two_storey:
        shed_roof(Shift(a, ((x0 + x1) / 2, (y0 + y1) / 2, 0), rz=-90), 0, 0, y1 - y0, x1 - x0, 2.40, zw + 0.10,
                  face=-1, ox=0.14, oy=0.08)
        return
    z2 = 2.86
    a.add(p_box((x1 - x0 + 0.02, y1 - y0 + 0.08, z2 - zw), loc=((x0 + x1) / 2, (y0 + y1) / 2, (zw + z2) / 2),
                bevel=0.03), PLASTER)
    fachwerk(a, Frame(((x0 + x1) / 2, y0 - 0.04, 0)), x1 - x0 - 0.04, zw + 0.06, z2, posts=(),
             braces=((-0.30, zw + 0.10, 0.20, z2 - 0.10),))
    win(a, Frame(((x0 + x1) / 2 + 0.16, y0 - 0.06, 2.34)), w=0.30, h=0.34, lit=True, shutters=None, sill=None)
    gable_y = gable_roof(Shift(a, ((x0 + x1) / 2, (y0 + y1) / 2, 0)), x1 - x0, y1 - y0 + 0.08, z2, pitch_deg=40,
                         ox=0.08, oy=0.16)
    a.add(p_prism([(-(x1 - x0) / 2, z2), ((x1 - x0) / 2, z2), (0, gable_y - 0.05)], 0.12,
                  loc=((x0 + x1) / 2, y0 + 0.02, 0)), PLASTER)


def _l2(a):
    """Постоялый двор: на переднем скате два слуховых окна со светом — комнаты для постояльцев, слева
    каменная пристройка-кухня под односкатной кровлей, под окнами второго этажа ящики с цветами; на
    террасе второй стол и бочки. Дом, труба с жаром и вывеска — прежние."""
    rng = random.Random(11)
    _base(a, rng)
    zr = gable_roof_x(a, W2, D2, Z2, pitch_deg=PITCH, ox=0.22, oy=0.26)
    gable_wall_x(a, W2, D2, Z2 - 0.02, zr, col="wood_mid", inset=0.05, depth=0.14)
    tp = math.tan(math.radians(PITCH))
    for x in (-0.82, 0.82):
        yd = -D2 / 2 + 0.36
        dormer(a, x, yd, zr - (0 - yd) * tp, w=0.60, h=0.54, depth=0.70, pitch=PITCH, lit=True)
    _chimney(a, rng)
    _sign(a)
    _wing(a)
    for x in (-1.05, 1.05):
        _flower_box(a, x, YF2, 2.04)
    _table(a, Frame((-1.12, YF1 - 0.80, 0.10)))
    _table(a, Frame((0.70, YF1 - 0.92, 0.10)))
    barrel(a, (1.62, YF1 - 0.36, 0.10), r=0.23, h=0.50)
    _lying_barrel(a, Frame((1.70, YF1 - 1.00, 0.10), rz=-80))
    log_pyramid(a, Frame((2.21, -0.62, 0.0), rz=90), L=0.74, r=0.11, rows=(2, 1), stakes=False)


def _l3(a):
    """Гостиница в три этажа: над бревенчатым этажом — третий, фахверк по штукатурке с выносом, окна со
    светом; посередине фасада — фронтон-ризалит с балконом; труба выше конька; пристройка слева в два
    этажа под своей кровлей; фонари у двери, на террасе столы и бочки."""
    rng = random.Random(11)
    _base(a, rng)
    z3 = Z2 + 1.10
    j = 0.10
    a.add(p_box((W2 + 2 * j, D2 + j, z3 - Z2), loc=(0, -j / 2, (Z2 + z3) / 2), bevel=0.03), PLASTER)
    a.add(p_box((W2 + 2 * j + 0.08, 0.20, 0.18), loc=(0, -D2 / 2 - j + 0.03, Z2 + 0.04), bevel=0.0), BEAM)
    fachwerk(a, Frame((0, -D2 / 2 - j, 0)), W2 + 2 * j - 0.04, Z2 + 0.12, z3, posts=(-1.20, -0.42, 0.42, 1.20),
             rails=(Z2 + 0.50,), braces=((-1.82, Z2 + 0.16, -1.30, Z2 + 0.46), (1.82, Z2 + 0.16, 1.30, Z2 + 0.46)))
    for x in (-1.62, -0.81, 0.81, 1.62):
        win(a, Frame((x, -D2 / 2 - j - 0.02, Z2 + 0.72)), w=0.30, h=0.36, lit=True, shutters=None, sill=None)
    pitch = 40.0
    yc = -j / 2
    zr = gable_roof_x(Shift(a, (0, yc, 0)), W2 + 2 * j, D2 + j, z3, pitch_deg=pitch, ox=0.18, oy=0.24)
    gable_wall_x(Shift(a, (0, yc, 0)), W2 + 2 * j, D2 + j, z3 - 0.02, zr, col=PLASTER, inset=0.05, depth=0.14)
    yf3 = -D2 / 2 - j
    zr2 = cross_gable(a, 0.0, yf3, 1.30, z3, z3 + 0.34, zr, yc, pitch)
    plank_door(a, yf3 + 0.08, z3 - 0.30, w=0.52, h=0.86)
    a.add(p_box((1.10, 0.40, 0.08), loc=(0, yf3 - 0.20, z3 - 0.34), bevel=0.0), PLANKS)
    for x in (-0.52, 0.52):
        a.add(p_box((0.06, 0.06, 0.46), loc=(x, yf3 - 0.38, z3 - 0.10), bevel=0.0), BEAM)
    a.add(p_box((1.12, 0.06, 0.06), loc=(0, yf3 - 0.38, z3 + 0.12), bevel=0.0), "wood_mid")
    for x in (-0.36, -0.12, 0.12, 0.36):
        a.add(p_box((0.04, 0.04, 0.40), loc=(x, yf3 - 0.38, z3 - 0.10), bevel=0.0), "wood_light")
    win(a, Frame((0.0, yf3 - 0.02, z3 + 0.62)), w=0.28, h=0.28, lit=True, shutters=None, sill=None)
    _chimney(a, rng, zr=zr)
    _sign(a)
    _wing(a, two_storey=True)
    for sx in (-1, 1):
        wall_lamp(a, sx * 0.52, YF1 - 0.04, 1.52, out=(sx * 0.3, -0.95))
    _table(a, Frame((-1.12, YF1 - 0.80, 0.10)))
    _table(a, Frame((0.70, YF1 - 0.92, 0.10)))
    barrel(a, (1.62, YF1 - 0.36, 0.10), r=0.23, h=0.50)
    _lying_barrel(a, Frame((1.70, YF1 - 1.00, 0.10), rz=-80))
    log_pyramid(a, Frame((2.21, -0.62, 0.0), rz=90), L=0.74, r=0.11, rows=(2, 1), stakes=False)


def evolve(a, level):
    """2: постоялый двор. 3: гостиница в три этажа."""
    (_l2 if level == 2 else _l3)(a)
