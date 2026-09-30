"""
Мастерская щитов: низкий плотницкий сруб под огромной синей кровлей. Главный приём —
круглый щит почти во весь фронтон (r 0.74, заклёпки, красно-белые четверти), он висит
на причелинах над низкой дверью, а концы причелин скрещены над коньком «рогами», как
у северного зала. Перед входом ремесло: верстак с голым дощатым диском и краской,
козлы с доской, сбоку А-стойка готовых разноцветных щитов, под свесами доски и бочка.
"""
import math
import bmesh
from mathutils import Vector
from build_vitaria import p_box, by_normal, TM
from vitaria_buildings.common import (Frame, gable_roof, gable_wall, stone_base, cornice, plank_door,
                                      barrel, bucket, round_shield, heater_shield,
                                      STONE_DARK_TOP, PLANK_TOP)

NAME = "Bld_ShieldWorkshop"
TITLE = "Мастерская щитов"
TARGET = (3.8, 3.2, 3.2)   # w, d, h в метрах

W, D = 2.70, 2.10          # сруб
Y0 = 0.12                  # сруб сдвинут назад: спереди рабочая зона, габарит остаётся по центру
F = 0.28                   # верх цоколя
ZT = 1.70                  # верх стен: сруб низкий, всё решает кровля
PITCH = 36.0
OX, OY = 0.36, 0.10        # свес по бокам (был 0.44 — выносил кровлю за след 2x2), короткий по торцам
YF = Y0 - D / 2            # плоскость фасада в мировых координатах
T_SH = 0.08                # толщина щита-героя
YSH = Y0 - (D / 2 + OY + 0.10) - 0.005 - T_SH / 2   # тыл щита на 5 мм перед причелинами
ZSH, R_SH = 2.16, 0.74     # низ щита на 4 см выше наличника двери


class _Shift:
    """Прокси Asset: детали, добавленные через него, переносятся матрицей m.
    Нужен, чтобы звать gable_roof/cornice/stone_base для сруба, стоящего не в нуле."""

    def __init__(self, a, m):
        self.a, self.m = a, m

    def add(self, part, color, smooth=False):
        bmesh.ops.transform(part, matrix=self.m, verts=part.verts)
        part.normal_update()
        self.a.add(part, color, smooth)


def _horns(s, zr, sy, Lh=0.48):
    """Концы причелин, продолженные за конёк к соседнему скату, — скрещённые «рога».
    Пара разведена по Y на 3 см: перехлёст читается врезкой, общих граней нет."""
    p = math.radians(PITCH)
    ridge = Vector((0, 0, zr))
    yb = sy * (D / 2 + OY + 0.04)
    for sx in (-1, 1):
        nrm = Vector((sx * math.sin(p), 0, math.cos(p)))
        up = Vector((-sx * math.cos(p), 0, math.sin(p)))
        c = ridge + up * (Lh / 2) + nrm * 0.05
        s.add(p_box((Lh, 0.12, 0.24), loc=(c.x, yb + sx * 0.015, c.z), rot=(0, sx * PITCH, 0), bevel=0.035),
              "wood_dark")


def _window(a, fr, w=0.40, h=0.34, shutters=True):
    """Окно в раме fr (стена в y=0, наружу -Y): ставни идут вдоль стены при любом повороте."""
    fr.box(a, (w + 0.14, 0.12, h + 0.14), (0, -0.02, 0), col="wood_dark", bevel=0.03)
    fr.box(a, (w, 0.10, h), (0, -0.045, 0), col="glass", bevel=0.02)
    for sx in ((-1, 1) if shutters else ()):
        fr.box(a, (0.20, 0.08, h + 0.06), (sx * (w / 2 + 0.15), -0.05, 0), col="roof_dark", bevel=0.02)
    fr.box(a, (w + 0.24, 0.18, 0.09), (0, -0.07, -h / 2 - 0.09), col="wood_light", bevel=0.025)


def _plank_disc(a, fr, R=0.34, t=0.05, n=3, gap=0.016, tones=("wood_pale", "wood_light", "wood_yellow")):
    """Заготовка щита: n досок вдоль Z рамы, обрезанных по кругу, со щелями между ними.
    Диск в плоскости XZ рамы (лицом к -Y), центр в начале рамы."""
    xs = [-R + 2 * R * i / n for i in range(n + 1)]
    for i in range(n):
        x0 = xs[i] + (gap / 2 if i > 0 else 0.0)
        x1 = xs[i + 1] - (gap / 2 if i < n - 1 else 0.0)
        pts = []
        for k in range(4):                       # нижняя дуга слева направо
            x = x0 + (x1 - x0) * k / 3
            pts.append((x, -math.sqrt(max(R * R - x * x, 0.0))))
        for k in range(4):                       # верхняя дуга справа налево
            x = x1 - (x1 - x0) * k / 3
            pts.append((x, math.sqrt(max(R * R - x * x, 0.0))))
        # у крайних досок точки на оси совпадают — дубли выкидываем, иначе грань вырождается
        clean = []
        for pt in pts:
            if not clean or abs(pt[0] - clean[-1][0]) + abs(pt[1] - clean[-1][1]) > 1e-4:
                clean.append(pt)
        if abs(clean[0][0] - clean[-1][0]) + abs(clean[0][1] - clean[-1][1]) < 1e-4:
            clean.pop()
        fr.prism(a, clean, t, col=tones[i % len(tones)])


def _workbench(a, fr):
    """Верстак: на нём заготовка щита, откинутая на упор (почти в лоб камере), и горшок синей краски."""
    fr.box(a, (1.06, 0.50, 0.10), (0, 0, 0.70), col=PLANK_TOP, bevel=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.box(a, (0.10, 0.10, 0.66), (sx * 0.44, sy * 0.17, 0.33), col="wood_dark", bevel=0.02)
    fr.box(a, (0.94, 0.08, 0.08), (0, 0.17, 0.20), col="wood_dark", bevel=0.02)
    # упор у заднего края: на него опирается диск
    fr.box(a, (0.30, 0.10, 0.20), (0.17, 0.09, 0.85), col="wood_dark", bevel=0.02)
    # диск откинут на 30 град: нижняя кромка на столешнице, спина на упоре
    tilt = 30.0
    R = 0.34
    zc = 0.75 + R * math.cos(math.radians(tilt)) + 0.01
    yc = -0.10 + R * math.sin(math.radians(tilt))
    _plank_disc(a, fr.sub((0.17, yc, zc), rot=(-tilt, 0, 0)), R=R, t=0.05)
    # горшок краски: у bucket() зеркало на 6 см ниже края и сверху читалось чёрным — краска
    # налита почти до края, из неё торчит кисть
    pot = fr.sub((-0.31, -0.02, 0.75))
    bucket(a, pot, r=0.15, h=0.22, fill="roof")
    pot.cyl(a, 0.137, 0.137, 0.012, 10, loc=(0, 0, 0.19), col="roof")
    pot.cyl(a, 0.025, 0.022, 0.32, 6, loc=(-0.03, 0.0, 0.07), rot=(0, -18, 0), col="wood_light")
    # потёк по стенке (поверх обручей) и лужица на столешнице — синий виден и сбоку, и сверху
    pot.box(a, (0.06, 0.03, 0.12), (0.0, -0.162, 0.15), col="roof", bevel=0.0)
    fr.cyl(a, 0.09, 0.09, 0.008, 8, loc=(-0.14, -0.14, 0.75), col="roof")


def _sawhorse(a, fr, L=0.86, h=0.60):
    """Козлы вдоль X рамы: брус на двух парах разведённых ног, сверху доска."""
    ang = 16.0
    fr.box(a, (L, 0.12, 0.10), (0, 0, h - 0.05), col="wood_mid", bevel=0.025)
    hl = h / math.cos(math.radians(ang))
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.box(a, (0.08, 0.08, hl), (sx * (L / 2 - 0.12), sy * h * math.tan(math.radians(ang)) / 2, h / 2 - 0.02),
                   rot=(sy * ang, 0, 0), col="wood_dark", bevel=0.02)
    fr.box(a, (1.20, 0.24, 0.05), (0.06, 0.0, h + 0.025), col=by_normal("wood_pale", "wood_light"), bevel=0.015)


def _plank_pile(a, fr, L=1.10):
    """Стопка досок вдоль X рамы на двух прокладках."""
    for x in (-L * 0.34, L * 0.34):
        fr.box(a, (0.10, 0.30, 0.07), (x, 0, 0.035), col="wood_dark", bevel=0.015)
    for k, (dx, col) in enumerate(((0.0, "wood_pale"), (0.04, "wood_light"), (-0.03, "wood_pale"),
                                   (0.02, "wood_yellow"))):
        fr.box(a, (L - 0.05 * (k % 2), 0.24, 0.06), (dx, 0.01 * (k % 2), 0.10 + k * 0.06), col=col, bevel=0.012)


def _shield_rack(a, fr):
    """А-образная стойка готовых щитов. Передний скат наклонён на 13 град: щиты почти
    в лоб игровой камере. Два ряда (нижний ближе к зрителю), тыл стойки пустой."""
    Hh, Sp, Hx = 1.22, 0.28, 0.62
    lean = math.degrees(math.atan2(Sp, Hh))
    Ll = math.hypot(Sp, Hh)
    for sx in (-1, 1):
        for sy in (-1, 1):
            fr.box(a, (0.09, 0.09, Ll + 0.02), (sx * Hx, sy * Sp / 2, Hh / 2), rot=(sy * lean, 0, 0),
                   col="wood_dark", bevel=0.02)
    fr.box(a, (2 * Hx + 0.16, 0.10, 0.10), (0, 0, Hh - 0.03), col="wood_mid", bevel=0.025)
    for z in (0.22, 0.70):
        fr.box(a, (2 * Hx + 0.10, 0.08, 0.08), (0, -Sp * (1 - z / Hh) - 0.075, z), col="wood_mid", bevel=0.02)

    def slot(x, zc, dy=0.0):
        """Рама щита на переднем скате: тыл щита касается перекладин."""
        y = Sp * (1 - zc / Hh) + 0.12 + 0.04 + dy
        return fr.sub((x, -y, zc), rot=(-lean, 0, 0))

    # нижний ряд: соседние щиты разнесены по Y на 1.5 см — перехлёст без общих плоскостей
    round_shield(a, slot(-0.31, 0.40), r=0.30, face="roof", paint="cream", pattern="cross", seg=12)
    round_shield(a, slot(0.31, 0.40, dy=0.015), r=0.30, face="berry", paint="cream", pattern="halves",
                 rim="wood_dark", boss="iron_light", seg=12)
    # верхний ряд
    round_shield(a, slot(-0.30, 0.95), r=0.27, face="leaf_mid", paint="gold", pattern="band", seg=12)
    # у heater_shield тыл кромки в y=t, а не t/2: сдвиг, чтобы не врезаться в перекладину
    heater_shield(a, slot(0.30, 0.96, dy=0.03), w=0.42, h=0.50, t=0.06, face="cream", rim="wood_dark",
                  emblem="chevron", paint="berry")


def build(a):
    s = _Shift(a, TM((0, Y0, 0)))      # сруб строится в своих осях, центр в (0, Y0)

    # --- цоколь, сруб, каркас
    stone_base(s, W + 0.26, D + 0.26, h=F, col=STONE_DARK_TOP)
    s.add(p_box((W, D, ZT - F + 0.02), loc=(0, 0, (F + ZT) / 2 - 0.01), bevel=0.05), "wood_mid")
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.add(p_box((0.24, 0.24, ZT - F), loc=(sx * W / 2, sy * D / 2, (F + ZT) / 2), bevel=0.05),
                  "wood_dark")
    cornice(s, W, D, ZT, t=0.16, col="wood_dark")
    # пояс одним брусом на весь периметр: над окнами, за наличником двери не виден
    s.add(p_box((W + 0.07, D + 0.07, 0.14), loc=(0, 0, 1.12), bevel=0.035), "wood_dark")

    # --- кровля: свес 0.44 по бокам уводит карниз до 1.4 м — сруб прячется под «шапкой»
    zr = gable_roof(s, W, D, ZT, pitch_deg=PITCH, ox=OX, oy=OY)
    gable_wall(s, W, ZT - 0.02, zr, D, col="wood_light", inset=0.05, depth=0.14)
    for sy in (-1, 1):
        _horns(s, zr, sy)

    # --- вход: низкая дверь под щитом и ступень
    plank_door(s, -D / 2, F, w=0.78, h=1.00, col="wood_light", frame="wood_dark")
    s.add(p_box((1.10, 0.40, 0.18), loc=(0, -D / 2 - 0.31, 0.05), bevel=0.04),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))

    # --- герой: щит почти во весь фронтон, висит на причелинах
    round_shield(a, Frame((0, YSH, ZSH)), r=R_SH, t=T_SH, face="berry", paint="cream", pattern="quarters",
                 rim="iron_dark", boss="gold", studs=10, seg=16)
    # опорный брус между щитом и фронтоном: снаружи не виден, сбоку держит щит
    a.add(p_box((0.22, 0.20, 0.22), loc=(0, YF - 0.11, ZSH + 0.15), bevel=0.0), "wood_dark")

    # --- окна: по одному на бок, низко — выше их съедает тень широкого свеса
    for sx in (-1, 1):
        _window(a, Frame((sx * W / 2, Y0 + 0.25, 0.80), rz=sx * 90))
    _window(a, Frame((0.55, Y0 + D / 2, 0.80), rz=180), shutters=False)

    # --- рабочая зона перед входом: слева верстак, справа стойка готовых щитов
    _workbench(a, Frame((-1.18, -1.42, 0), rz=4))
    _shield_rack(a, Frame((1.16, -1.36, 0), rz=-4))

    # --- у левого угла козлы с доской (задний угол верстака в 5 см от доски), под свесами
    # стопка досок и бочка
    _sawhorse(a, Frame((-1.5, -0.62, 0), rz=90))
    _plank_pile(a, Frame((-1.5, 0.72, 0), rz=90))
    barrel(a, (1.52, -0.40, 0.0))
