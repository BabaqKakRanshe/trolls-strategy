"""
Поле: приподнятая вспаханная делянка, семь гребней вдоль фасада и пугало среди колосьев.

Главный приём — прогрессия спереди назад: голые гребни, зелёные пучки всходов, затем три
ряда высокой золотой пшеницы. Высокое стоит сзади, поэтому игровая камера (с -Y под ~40°)
видит все стадии сразу, а пугало в синей рубахе поднимается над колосьями и даёт силуэт.
"""
import math, random
import bmesh
from build_vitaria import p_box, p_cyl, p_ico, by_normal
from vitaria_buildings.common import Frame, fence_line, bucket
from vitaria_buildings.levels import (Shift, gable_y_at, gable_front, porch_post, WALL, BEAM, PLANKS)

NAME = "Bld_Field"
TITLE = "Поле"
TARGET = (5.3, 5.2, 1.7)     # след 3x3 при масштабе 0.5

PW, PD = 4.70, 4.10          # делянка (была 3.9 x 3.4: при общем масштабе 0.5 не заполняла след 3x3)
PY = 0.2                     # центр делянки сдвинут назад: спереди место под тачку и снопы
ZP = 0.13                    # верх делянки
ROWS = [-1.48 + i * 0.555 for i in range(7)]  # 2 борозды, 2 ряда всходов, 3 ряда пшеницы
RX = PW / 2 - 0.17           # концы гребней
SC = (0.66, 1.01)            # пугало — между первым и вторым рядом пшеницы
FX = 2.52                    # линия изгороди по бокам
EAR = by_normal("wheat_light", "wheat", "wheat", 0.2)
SOIL = by_normal("soil_light", "soil_mid", "soil_mid", 0.85)


def _ridge(a, x0, x1, y, h=0.10, hw=0.23, slope=1.8):
    """Гребень под посадками: профиль-шестиугольник со светлым верхом, торцы пологие.
    Плоская трапеция читалась доской настила, а прямой торец скруглённого профиля — бревном;
    пологий торец уходит в землю, и гребень читается насыпью. Низ утоплен на 2 см в делянку."""
    prof = [(-hw, -0.02), (hw, -0.02), (hw * 0.62, h * 0.62), (hw * 0.24, h),
            (-hw * 0.24, h), (-hw * 0.62, h * 0.62)]
    b = bmesh.new()
    ends = [[b.verts.new((xe + sg * slope * (z + 0.02), y + py, ZP + z)) for py, z in prof]
            for xe, sg in ((x0, 1), (x1, -1))]
    b.faces.new(ends[0])
    b.faces.new(ends[1])
    n = len(prof)
    for i in range(n):
        j = (i + 1) % n
        b.faces.new((ends[0][i], ends[1][i], ends[1][j], ends[0][j]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    b.normal_update()
    a.add(b, SOIL)


def _clod_row(a, y, rng, n=6):
    """Свежая пахота: сплошной гребень и гранёные комья, вдавленные в него через неравный шаг.
    Голый ровный гребень читался брусом, отдельные бугры без гребня — мощёной дорожкой."""
    _ridge(a, -RX, RX, y, h=0.12, hw=0.24)
    xs = [-RX + 0.35 + k * (2 * RX - 0.7) / (n - 1) + rng.uniform(-0.12, 0.12) for k in range(n)]
    for k, x in enumerate(xs):
        r = rng.uniform(0.10, 0.15)
        a.add(p_ico(r, 1, loc=(x, y + rng.uniform(-0.05, 0.05), ZP + 0.08),
                    scl=(1.35, 1.0, 0.72), jitter=0.2, rng=rng, rot=(0, 0, rng.uniform(0, 72)),
                    cut=-0.2 * r),
              by_normal("soil_light", "soil_mid", "soil_mid", 0.72))


def _ear(a, e, L, r=0.064, col=EAR):
    """Колос: короткий низ, прямое тело, острый верх; трёхгранный — две грани видно всегда.
    Бипирамида без «тела» читалась наконечником копья, а не колосом."""
    e.cyl(a, 0.0, r, L * 0.22, 3, col=col, cap=False)
    e.cyl(a, r, r * 0.85, L * 0.5, 3, loc=(0, 0, L * 0.22), col=col, cap=False)
    e.cyl(a, r * 0.85, 0.0, L * 0.28, 3, loc=(0, 0, L * 0.72), col=col, cap=False)


def _leaf(a, x, y, z0, rng, h, col, psi, tilt, r=0.06):
    """Лист: сплюснутый открытый 4-гранный конус, наклонённый наружу."""
    fr = Frame((x, y, z0), rot=(tilt, 0, psi + 90), s=(1.0, 0.4, 1.0))
    fr.cyl(a, r, 0.0, h, 4, col=col, cap=False)


def _wheat_bush(a, x, y, z0, rng, hmax, n):
    """Куст: n почти вертикальных стеблей с колосьями, чуть разведённых от центра.
    Колос на вертикальном стебле читается пшеницей; веер из наклонных стеблей — звездой."""
    for k in range(n):
        psi = rng.uniform(0, 360)
        d = rng.uniform(0.0, 0.08)
        c, s = math.cos(math.radians(psi)), math.sin(math.radians(psi))
        fr = Frame((x + c * d, y + s * d, z0), rot=(rng.uniform(3, 12), 0, psi + 90))
        h = hmax * rng.uniform(0.85, 1.0)
        L = 0.32
        fr.cyl(a, 0.026, 0.02, h - L + 0.03, 3, col="wheat_dark", cap=False)
        e = fr.sub((0, 0, h - L), rot=(rng.uniform(0, 10), 0, rng.uniform(0, 360)))
        _ear(a, e, L, col=EAR if k % 2 else "wheat")


def _sprout(a, x, y, z0, rng, h, n=4):
    """Пучок всходов: центральный лист прямо, остальные веером наружу."""
    a0 = rng.uniform(0, 360)
    for k in range(n):
        tilt = rng.uniform(16, 30) if k else rng.uniform(0, 8)
        _leaf(a, x, y, z0, rng, h * (1.1 if k == 0 else rng.uniform(0.7, 0.95)),
              "leaf_light" if k % 2 else "blade", a0 + k * 360.0 / n + rng.uniform(-20, 20), tilt,
              r=0.065)


def _sheaf(a, fr):
    """Сноп: связка стеблей с перетяжкой, наверху расходятся колосья."""
    fr.cyl(a, 0.14, 0.085, 0.30, 7, col="wheat_dark", cap=False)
    fr.cyl(a, 0.105, 0.105, 0.065, 7, loc=(0, 0, 0.25), col="rope")
    fr.cyl(a, 0.085, 0.15, 0.20, 7, loc=(0, 0, 0.30), col="wheat")
    fr.cyl(a, 0.15, 0.06, 0.07, 7, loc=(0, 0, 0.50), col="wheat_light")
    for k in range(5):
        psi = k * 72 + 20
        c, s = math.cos(math.radians(psi)), math.sin(math.radians(psi))
        _ear(a, fr.sub((c * 0.08, s * 0.08, 0.40), rot=(22, 0, psi + 90)), 0.28, r=0.058,
             col=EAR if k % 2 else "wheat")


def _scarecrow(a, fr):
    """Пугало: шест с перекладиной, синяя рубаха, голова-мешок, соломенная шляпа, ворона на руке."""
    fr.box(a, (0.09, 0.09, 1.36), (0, 0, 0.60), col="wood_mid", bevel=0.02)       # низ в земле на 8 см
    fr.box(a, (1.12, 0.08, 0.08), (0, 0, 1.06), col="wood_mid", bevel=0.02)
    # рубаха расширяется книзу — висит на перекладине
    fr.taper(a, (0.46, 0.27), (0.38, 0.23), 0.48, loc=(0, 0, 0.66), col="roof", bevel=0.03)
    fr.box(a, (0.46, 0.29, 0.06), (0, 0, 0.78), col="rope", bevel=0.015)            # верёвка-пояс
    fr.box(a, (0.12, 0.03, 0.12), (-0.09, -0.135, 0.98), rot=(0, 12, 0), col="berry", bevel=0.0)  # заплата
    for sx in (-1, 1):
        fr.box(a, (0.30, 0.17, 0.16), (sx * 0.33, 0, 1.06), col="roof", bevel=0.03)   # рукава
        for dz, spread in ((0.04, 22), (-0.03, -8), (0.0, 8)):
            fr.cyl(a, 0.045, 0.0, 0.15, 4, loc=(sx * 0.47, 0, 1.06 + dz),
                   rot=(0, sx * (90 - spread), 0), col="wheat_light")
    for x, y in ((-0.15, -0.06), (-0.05, -0.10), (0.06, -0.09), (0.16, -0.04), (0.0, 0.08)):
        fr.cyl(a, 0.045, 0.0, 0.14, 4, loc=(x, y, 0.70), rot=(180, 0, 0), col="wheat_light")
    # голова-мешок, перетянутая верёвкой
    fr.cyl(a, 0.08, 0.08, 0.06, 8, loc=(0, 0, 1.14), col="rope")
    fr.ico(a, 0.165, loc=(0, 0, 1.33), scl=(1.0, 0.92, 1.08), col="burlap")
    for sx in (-1, 1):
        fr.cyl(a, 0.032, 0.032, 0.07, 6, loc=(sx * 0.065, -0.10, 1.36), rot=(90, 0, 0), col="coal")
    fr.box(a, (0.12, 0.03, 0.03), (0, -0.14, 1.26), col="coal", bevel=0.0)
    # соломенная шляпа, чуть набекрень
    h = fr.sub((0.0, 0.0, 1.45), rot=(6, -9, 0))
    h.cyl(a, 0.29, 0.29, 0.045, 10, col=by_normal("wheat_light", "wheat", "wheat_dark", 0.6))
    h.cyl(a, 0.15, 0.12, 0.17, 8, loc=(0, 0, 0.03), col=by_normal("wheat_light", "wheat", "wheat_dark", 0.6))
    h.cyl(a, 0.155, 0.15, 0.05, 8, loc=(0, 0, 0.045), col="roof_dark")
    # ворона на левой руке — сразу объясняет, зачем тут пугало
    c = fr.sub((-0.50, 0.0, 1.10), rz=-35)
    c.box(a, (0.05, 0.03, 0.06), (0.0, 0.0, 0.02), col="flower_y", bevel=0.0)
    c.ico(a, 0.075, loc=(0, 0, 0.10), scl=(1.0, 1.35, 0.9), col="coal")
    c.ico(a, 0.05, loc=(0, -0.10, 0.16), col="coal")
    c.cyl(a, 0.025, 0.0, 0.07, 4, loc=(0, -0.14, 0.155), rot=(90, 0, 0), col="flower_y")
    c.box(a, (0.07, 0.10, 0.03), (0, 0.13, 0.13), rot=(-25, 0, 0), col="coal", bevel=0.0)


def _wheelbarrow(a, fr):
    """Тачка: колесо в +X, ручки в -X. Кузов — призма со скошенным передом, как у настоящей
    тачки; прямоугольный ящик на ножках читался тележкой."""
    tray = lambda f: "wood_dark" if f.normal.z < -0.5 else "wood_mid"
    # кузов стоит на ручках: низ на 1 см ниже их верха — пересечение, а не общая плоскость
    fr.prism(a, [(-0.34, 0.0), (0.16, 0.0), (0.44, 0.30), (-0.40, 0.30)], 0.54,
             loc=(0, 0, 0.30), col=tray)
    fr.box(a, (0.86, 0.60, 0.07), (0.02, 0, 0.61), col="wood_dark", bevel=0.02)      # борт-обвязка
    fr.box(a, (0.78, 0.48, 0.06), (0.02, 0, 0.625), col="wheat", bevel=0.0)         # зерно вровень с бортом
    fr.ico(a, 0.24, loc=(-0.04, 0, 0.64), scl=(1.25, 0.9, 0.55), col=EAR, cut=0.0)   # горка колосьев
    for sy in (-1, 1):
        fr.box(a, (1.30, 0.07, 0.08), (-0.15, sy * 0.24, 0.27), col="wood_dark", bevel=0.02)
        fr.box(a, (0.07, 0.07, 0.28), (-0.30, sy * 0.22, 0.14), col="wood_dark", bevel=0.02)
    fr.cyl(a, 0.21, 0.21, 0.09, 10, loc=(0.52, 0.045, 0.21), rot=(90, 0, 0), col="wood_mid")
    fr.cyl(a, 0.07, 0.07, 0.14, 6, loc=(0.52, 0.07, 0.21), rot=(90, 0, 0), col="iron_dark")
    for k, (y, rz) in enumerate(((-0.12, 16), (0.1, -12))):
        _ear(a, fr.sub((0.10, y, 0.68), rot=(0, 72 + 8 * k, rz)), 0.30)


def build(a):
    rng = random.Random(7)

    # делянка: верх земли светлее боков — как срез тайла острова
    a.add(p_box((PW, PD, 0.19), loc=(0, PY, ZP - 0.095), bevel=0.06),
          by_normal("soil_mid", "soil_dark", "soil_dark", 0.8))

    # голые борозды: свежая пахота комьями
    for i in (0, 1):
        _clod_row(a, ROWS[i], rng)

    # всходы: второй ряд выше первого — рост читается и внутри зелёной полосы
    for i, h in ((2, 0.24), (3, 0.36)):
        _ridge(a, -RX, RX, ROWS[i])
        for k in range(14):
            x = -2.0 + k * 0.308 + rng.uniform(-0.03, 0.03)
            _sprout(a, x, ROWS[i] + rng.uniform(-0.03, 0.03), ZP + 0.08, rng, h)

    # пшеница: к заднему ряду выше, силуэт поднимается ступенькой
    for i, hmax in ((4, 0.78), (5, 0.88), (6, 0.96)):
        _ridge(a, -RX, RX, ROWS[i])
        for k in range(13):
            x = -2.0 + k * 0.333 + rng.uniform(-0.03, 0.03)
            y = ROWS[i] + rng.uniform(-0.04, 0.04)
            if i in (4, 5) and abs(x - SC[0]) < 0.24:
                continue                      # место под пугало
            _wheat_bush(a, x, y, ZP + 0.06, rng, hmax, 4 if (k + i) % 2 else 3)

    _scarecrow(a, Frame((SC[0], SC[1], ZP), rz=-8, s=0.94))

    # изгородь по бокам и сзади, фасад открыт — камера смотрит на ряды
    yb, yf = PY + PD / 2 + 0.14, PY - PD / 2 + 0.05
    fence_line(a, (-FX, yf), (-FX, yb), h=0.62, posts=4, rails=(0.24, 0.46), post_s=0.12)
    fence_line(a, (-FX, yb), (FX, yb), h=0.62, posts=4, rails=(0.24, 0.46), post_s=0.12,
               end_posts=(False, False))
    fence_line(a, (FX, yb), (FX, yf), h=0.62, posts=4, rails=(0.24, 0.46), post_s=0.12)

    # передние углы: справа тачка с колосьями, слева суслон из двух снопов
    _wheelbarrow(a, Frame((1.75, -2.2, 0.0), rz=-150, s=0.9))
    _sheaf(a, Frame((-2.0, -2.2, 0.0), rot=(0, 10, 0), s=1.12))
    _sheaf(a, Frame((-1.66, -2.26, 0.0), rot=(0, -10, 8), s=1.12))


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): делянка -> орошаемое поле с колодцем -> хлебное поле с мельницей
# =========================================================================================
def _plot(a):
    a.add(p_box((PW, PD, 0.19), loc=(0, PY, ZP - 0.095), bevel=0.06),
          by_normal("soil_mid", "soil_dark", "soil_dark", 0.8))


def _rows(a, rng, skip=lambda x, y: False):
    """Посадки уровней 2-3: на месте голых борозд — всходы (пахоты больше нет), дальше как у уровня 1;
    skip(x, y) — куст не ставится (место под мельницу)."""
    for i, h in ((0, 0.20), (1, 0.28), (2, 0.24), (3, 0.36)):
        _ridge(a, -RX, RX, ROWS[i])
        for k in range(14):
            x = -2.0 + k * 0.308 + rng.uniform(-0.03, 0.03)
            if skip(x, ROWS[i]):
                continue
            _sprout(a, x, ROWS[i] + rng.uniform(-0.03, 0.03), ZP + 0.08, rng, h, n=3 if i < 2 else 4)
    for i, hmax in ((4, 0.78), (5, 0.88), (6, 0.96)):
        _ridge(a, -RX if not skip(RX, ROWS[i]) else -RX, RX if not skip(RX, ROWS[i]) else 1.30, ROWS[i])
        for k in range(13):
            x = -2.0 + k * 0.333 + rng.uniform(-0.03, 0.03)
            y = ROWS[i] + rng.uniform(-0.04, 0.04)
            if i in (4, 5) and abs(x - SC[0]) < 0.24:
                continue
            if skip(x, y):
                continue
            _wheat_bush(a, x, y, ZP + 0.06, rng, hmax, 4 if (k + i) % 2 else 3)


def _irrigation(a, x_end=None):
    """Оросительные канавки с водой между гребнями и магистральная канава с каменной обкладкой слева."""
    x1 = RX if x_end is None else x_end
    for i in range(6):
        y = ROWS[i] + 0.2775
        a.add(p_box((x1 + RX, 0.11, 0.02), loc=((x1 - RX) / 2, y, ZP + 0.004), bevel=0.0), "water")
    xm = -RX - 0.13
    a.add(p_box((0.16, PD - 0.30, 0.02), loc=(xm, PY - 0.05, ZP + 0.006), bevel=0.0), "water")
    for sx in (-1, 1):
        a.add(p_box((0.08, PD - 0.30, 0.06), loc=(xm + sx * 0.12, PY - 0.05, ZP + 0.02), bevel=0.0), "stone_light")


def _well(a, x, y):
    """Колодец: каменный сруб, ворот с верёвкой, двускатная крышечка на двух стойках."""
    a.add(p_cyl(0.36, 0.36, 0.56, 8, loc=(x, y, 0.0), spin=22.5), by_normal("stone_light", "stone_mid", "stone_dark", 0.8))
    a.add(p_cyl(0.30, 0.30, 0.02, 8, loc=(x, y, 0.53), spin=22.5), "water")
    for sx in (-1, 1):
        a.add(p_box((0.09, 0.09, 1.10), loc=(x + sx * 0.34, y, 0.55 + 0.42), bevel=0.0), BEAM)
    a.add(p_cyl(0.06, 0.06, 0.74, 6, loc=(x - 0.37, y, 1.06), rot=(0, 90, 0)), "wood_light")
    a.add(p_box((0.03, 0.03, 0.34), loc=(x, y, 0.88), bevel=0.0), "rope")
    a.add(p_cyl(0.09, 0.10, 0.14, 6, loc=(x, y, 0.64)), "wood_mid")
    gable_y_at(a, x, y, 0.80, 0.70, 1.48, pitch_deg=40, ox=0.06, oy=0.08, bands=1)


def _windmill(a, cx, cy):
    """Ветряная мельница-башня: гранёный каменный ствол, деревянная шапка цвета кровли, четыре крыла
    крестом (X) лицом к камере — решётка с холстом, дверь и окошко."""
    st = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
    a.add(p_cyl(0.60, 0.60, 0.20, 8, loc=(cx, cy, -0.02), spin=22.5), "stone_dark")
    a.add(p_cyl(0.56, 0.42, 2.50, 8, loc=(cx, cy, 0.16), spin=22.5), st)
    a.add(p_cyl(0.48, 0.48, 0.10, 8, loc=(cx, cy, 2.62), spin=22.5), "wood_dark")
    a.add(p_cyl(0.52, 0.0, 0.80, 8, loc=(cx, cy, 2.70), spin=22.5), by_normal("roof", "roof", "roof_dark", 0.2))
    a.add(p_ico(0.07, 1, loc=(cx, cy, 3.54)), "wood_dark")
    a.add(p_box((0.34, 0.10, 0.60), loc=(cx - 0.02, cy - 0.55, 0.46), rot=(8, 0, 0), bevel=0.0), "wood_mid")
    a.add(p_box((0.16, 0.06, 0.20), loc=(cx + 0.02, cy - 0.48, 1.56), bevel=0.0), "glass")
    hub = (cx, cy - 0.64, 2.66)
    a.add(p_cyl(0.07, 0.07, 0.36, 8, loc=(cx, cy - 0.30, 2.66), rot=(90, 0, 0)), "wood_dark")
    a.add(p_cyl(0.11, 0.11, 0.10, 8, loc=(cx, cy - 0.64, 2.66), rot=(90, 0, 0)), "iron_dark")
    for k in range(4):
        fr = Frame(hub, rot=(0, 45 + 90 * k, 0))
        fr.box(a, (0.07, 0.06, 1.30), (0, -0.04, 0.68), col="wood_mid", bevel=0.0)
        fr.box(a, (0.26, 0.025, 0.92), (0.15, -0.02, 0.82), col="cream", bevel=0.0)
        for z in (0.46, 0.76, 1.06):
            fr.box(a, (0.32, 0.04, 0.035), (0.13, -0.05, z), col="wood_dark", bevel=0.0)
        fr.box(a, (0.035, 0.04, 0.92), (0.29, -0.05, 0.82), col="wood_dark", bevel=0.0)


def _stone_wall(a, p0, p1, h=0.50):
    """Сухая каменная ограда вместо жердей: тело и светлая шапка."""
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
    rz = math.degrees(math.atan2(p1[1] - p0[1], p1[0] - p0[0]))
    fr = Frame(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, 0.0), rz=rz)
    fr.box(a, (L, 0.26, h), (0, 0, h / 2 - 0.04), col=by_normal("stone_mid", "stone_mid", "stone_dark", 0.8),
           bevel=0.0)
    fr.box(a, (L + 0.04, 0.32, 0.08), (0, 0, h - 0.02), col="stone_light", bevel=0.0)


def _gate(a, y, stone=False):
    """Въездные ворота делянки посередине фасада: столбы, перекладина, вывеска-сноп."""
    for sx in (-1, 1):
        if stone:
            a.add(p_box((0.28, 0.28, 1.50), loc=(sx * 0.66, y, 0.71), bevel=0.0), WALL)
            a.add(p_box((0.34, 0.34, 0.08), loc=(sx * 0.66, y, 1.48), bevel=0.0), "stone_light")
        else:
            a.add(p_box((0.16, 0.16, 1.62), loc=(sx * 0.62, y, 0.77), bevel=0.0), BEAM)
    a.add(p_box((1.60, 0.14, 0.14), loc=(0, y, 1.56), bevel=0.0), BEAM)
    a.add(p_box((0.66, 0.06, 0.32), loc=(0, y - 0.05, 1.30), bevel=0.0), "wood_pale")
    _sheaf(a, Frame((0, y - 0.09, 1.16), s=0.55))


def _l2(a):
    """Орошаемое поле: пахоты больше нет — всходы на всех передних гребнях, между гребнями канавки с водой,
    слева магистральная канава в каменной обкладке; спереди слева колодец под крышечкой, посередине —
    въездные ворота с вывеской-снопом. Пшеница, пугало, изгородь, тачка и снопы — прежние."""
    rng = random.Random(7)
    _plot(a)
    _irrigation(a)
    _rows(a, rng)
    _scarecrow(a, Frame((SC[0], SC[1], ZP), rz=-8, s=0.94))
    yb, yf = PY + PD / 2 + 0.14, PY - PD / 2 + 0.05
    fence_line(a, (-FX, yf), (-FX, yb), h=0.62, posts=4, rails=(0.24, 0.46), post_s=0.12)
    fence_line(a, (-FX, yb), (FX, yb), h=0.62, posts=4, rails=(0.24, 0.46), post_s=0.12, end_posts=(False, False))
    fence_line(a, (FX, yb), (FX, yf), h=0.62, posts=4, rails=(0.24, 0.46), post_s=0.12)
    _gate(a, PY - PD / 2 - 0.10)
    _well(a, -1.92, -2.18)
    _wheelbarrow(a, Frame((1.75, -2.2, 0.0), rz=-150, s=0.9))
    _sheaf(a, Frame((-1.22, -2.32, 0.0), rot=(0, 10, 0), s=1.12))


def _l3(a):
    """Хлебное поле с мельницей: в правом заднем углу — ветряная мельница-башня (каменный ствол, шапка,
    крылья крестом к камере), ограда из сухого камня, каменные столбы ворот; канавки, колодец, всходы и
    пшеница — как на уровне 2."""
    rng = random.Random(7)
    mx, my = 1.58, 1.84
    skip = lambda x, y: math.hypot(x - mx, y - my) < 0.80
    _plot(a)
    _irrigation(a, x_end=0.80)
    _rows(a, rng, skip=skip)
    _scarecrow(a, Frame((SC[0], SC[1], ZP), rz=-8, s=0.94))
    yb, yf = PY + PD / 2 + 0.14, PY - PD / 2 + 0.05
    _stone_wall(a, (-FX, yf), (-FX, yb))
    _stone_wall(a, (-FX, yb), (0.90, yb))
    _stone_wall(a, (FX, yb), (FX, yf))
    _windmill(a, mx, my)
    _gate(a, PY - PD / 2 - 0.10, stone=True)
    _well(a, -1.92, -2.18)
    _wheelbarrow(a, Frame((1.75, -2.2, 0.0), rz=-150, s=0.9))
    _sheaf(a, Frame((-1.22, -2.32, 0.0), rot=(0, 10, 0), s=1.12))
    _sheaf(a, Frame((-0.92, -2.38, 0.0), rot=(0, -10, 8), s=1.12))


def evolve(a, level):
    """2: орошаемое поле с колодцем и воротами. 3: хлебное поле с мельницей."""
    (_l2 if level == 2 else _l3)(a)
