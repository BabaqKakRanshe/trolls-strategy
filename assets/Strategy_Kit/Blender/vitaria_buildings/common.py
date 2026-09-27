"""
Общие детали производственных зданий Vitaria.

Первая половина файла — детали из Ref_Buildings/Source/parts/common.py (кровли, двери,
окна, бочки, брёвна), перенесены почти без изменений, чтобы новые здания собирались теми же
приёмами (правка одна: чёрная плита в chimney() без фаски). Вторая половина — новое: локальная рама Frame и оружие, щиты, доспехи,
изгороди, кристаллы.

Соглашения кита: 1 юнит = 1 м, пивот внизу по центру (z = 0), фасад смотрит в -Y
(= +Z в Unity). Минимальная толщина видимой детали около 5–10 см, фаска на каждом блоке,
одна грань — один swatch палитры, верх светлее боков (by_normal).
"""
import math
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_prism, p_ico, p_taper_box, by_normal, TM


# =========================================================================================
# Из Ref_Buildings (не менять поведение: на них держится общий вид кровель и дверей)
# =========================================================================================
def gable_roof(a, w, d, ztop, pitch_deg=36.0, ox=0.25, oy=0.22, bands=2,
               top="roof", bot="roof_dark", trim="wood_dark", ridge_cap=True):
    """Двускатная кровля над коробкой w x d с верхом стен на ztop, конёк вдоль Y. Возвращает zr."""
    pitch = math.radians(pitch_deg)
    zr = ztop + 0.08 + (w / 2) * math.tan(pitch)
    L = (w / 2 + ox) / math.cos(pitch)
    Ly = d + 2 * oy
    for sx in (-1, 1):
        nrm = Vector((sx * math.sin(pitch), 0, math.cos(pitch)))
        down = Vector((sx * math.cos(pitch), 0, -math.sin(pitch)))
        ridge = Vector((0, 0, zr))
        c = ridge + down * (L / 2) - nrm * 0.03
        a.add(p_box((L, Ly, 0.1), loc=c, rot=(0, sx * math.degrees(pitch), 0), bevel=0.03), trim)
        rl = L / bands
        for k in range(bands):
            cc = ridge + down * ((k + 0.5) * rl) + nrm * 0.06
            a.add(p_box((rl + 0.1, Ly, 0.17), loc=(cc.x, 0, cc.z),
                        rot=(0, sx * math.degrees(pitch), 0), bevel=0.05),
                  top if k == 0 else bot)
        for sy in (-1, 1):
            cb = ridge + down * (L / 2) + nrm * 0.05
            a.add(p_box((L + 0.08, 0.12, 0.24), loc=(cb.x, sy * (Ly / 2 + 0.04), cb.z),
                        rot=(0, sx * math.degrees(pitch), 0), bevel=0.035), trim)
    if ridge_cap:
        a.add(p_box((0.38, Ly + 0.14, 0.38), loc=(0, 0, zr + 0.09), rot=(0, 45, 0), bevel=0.055), bot)
    return zr


def gable_roof_x(a, w, d, ztop, pitch_deg=32.0, ox=0.25, oy=0.22, bands=2,
                 top="roof", bot="roof_dark", trim="wood_dark", ridge_cap=True):
    """Как gable_roof, но конёк вдоль X: w — длина конька, d — пролёт поперёк."""
    pitch = math.radians(pitch_deg)
    zr = ztop + 0.08 + (d / 2) * math.tan(pitch)
    L = (d / 2 + oy) / math.cos(pitch)
    Lx = w + 2 * ox
    for sy in (-1, 1):
        ang = -sy * math.degrees(pitch)
        nrm = Vector((0, sy * math.sin(pitch), math.cos(pitch)))
        down = Vector((0, sy * math.cos(pitch), -math.sin(pitch)))
        ridge = Vector((0, 0, zr))
        c = ridge + down * (L / 2) - nrm * 0.03
        a.add(p_box((Lx, L, 0.1), loc=c, rot=(ang, 0, 0), bevel=0.03), trim)
        rl = L / bands
        for k in range(bands):
            cc = ridge + down * ((k + 0.5) * rl) + nrm * 0.06
            a.add(p_box((Lx, rl + 0.1, 0.17), loc=(0, cc.y, cc.z), rot=(ang, 0, 0), bevel=0.05),
                  top if k == 0 else bot)
        for sx in (-1, 1):
            cb = ridge + down * (L / 2) + nrm * 0.05
            a.add(p_box((0.12, L + 0.08, 0.24), loc=(sx * (Lx / 2 + 0.04), cb.y, cb.z),
                        rot=(ang, 0, 0), bevel=0.035), trim)
    if ridge_cap:
        a.add(p_box((Lx + 0.14, 0.38, 0.38), loc=(0, 0, zr + 0.09), rot=(45, 0, 0), bevel=0.055), bot)
    return zr


def gable_wall_x(a, w, d, ztop, zr, col="stone_mid", inset=0.07, depth=0.14):
    """Фронтоны для gable_roof_x — на торцах по X."""
    for sx in (-1, 1):
        a.add(p_prism([(-d / 2, ztop), (d / 2, ztop), (0, zr - 0.04)], depth,
                      loc=(sx * (w / 2 - inset), 0, 0), rot=(0, 0, 90)), col)


def hip_roof(a, r, h, zbase, seg=4, spin=45.0, top="roof", bot="roof_dark"):
    """Шатёр/конус: seg=4 + spin=45 даёт квадратную пирамиду, seg=12 — круглый конус."""
    a.add(p_cyl(r, r * 0.62, h * 0.45, seg, loc=(0, 0, zbase), spin=spin, bevel=0.03), top)
    a.add(p_cyl(r * 0.62, 0.0, h * 0.60, seg, loc=(0, 0, zbase + h * 0.45), spin=spin), bot)
    a.add(p_cyl(r + 0.10, r + 0.10, 0.10, seg, loc=(0, 0, zbase - 0.06), spin=spin, bevel=0.03), "wood_dark")
    return zbase + h


def gable_wall(a, w, ztop, zr, d, col="stone_mid", inset=0.07, depth=0.14):
    """Треугольные фронтоны, закрывающие торцы под скатами gable_roof."""
    for sy in (-1, 1):
        a.add(p_prism([(-w / 2, ztop), (w / 2, ztop), (0, zr - 0.04)], depth,
                      loc=(0, sy * (d / 2 - inset), 0)), col)


def chimney(a, x, y, zbase, h=1.05, w=0.42, col="stone_mid"):
    a.add(p_box((w, w, h), loc=(x, y, zbase + h / 2), bevel=0.045), col)
    a.add(p_box((w + 0.13, w + 0.13, 0.14), loc=(x, y, zbase + h - 0.05), bevel=0.045), "stone_dark")
    # без фаски: у плиты 3 см фаска 3 см схлопывала грани в вырожденные (нулевой площади)
    a.add(p_box((w - 0.16, w - 0.16, 0.03), loc=(x, y, zbase + h + 0.03), bevel=0.0), "black")


def cornice(a, w, d, z, t=0.16, col="wood_dark"):
    """Карниз/пояс по периметру коробки w x d на высоте z."""
    for sy in (-1, 1):
        a.add(p_box((w + t, 0.20, t), loc=(0, sy * d / 2, z), bevel=0.04), col)
    for sx in (-1, 1):
        a.add(p_box((0.20, d + t, t), loc=(sx * w / 2, 0, z), bevel=0.04), col)


def plank_door(a, y, z0, w=0.62, h=1.16, col="wood_light", frame="wood_dark", x=0.0):
    """Дверь на фасаде (-Y): полотно, две поперечины, раскос, ручка, наличник."""
    a.add(p_box((w + 0.20, 0.16, h + 0.20), loc=(x, y - 0.02, z0 + h / 2), bevel=0.04), frame)
    a.add(p_box((w, 0.12, h), loc=(x, y - 0.06, z0 + h / 2), bevel=0.03), col)
    for z in (z0 + 0.18, z0 + h - 0.18):
        a.add(p_box((w - 0.04, 0.09, 0.13), loc=(x, y - 0.11, z), bevel=0.025), frame)
    a.add(p_box((0.12, 0.09, h - 0.30), loc=(x, y - 0.115, z0 + h / 2), rot=(0, 24, 0), bevel=0.02), frame)
    a.add(p_box((0.09, 0.09, 0.16), loc=(x + w / 2 - 0.16, y - 0.14, z0 + h * 0.52), bevel=0.022), "iron_dark")


def arch_door(a, y, z0, w=0.72, h=1.30, x=0.0, col="wood_mid"):
    """Арочная дверь в каменном портале."""
    a.add(p_box((w + 0.34, 0.20, h), loc=(x, y - 0.02, z0 + h / 2), bevel=0.05), "stone_light")
    a.add(p_cyl(w / 2 + 0.17, w / 2 + 0.17, 0.20, 12, loc=(x, y + 0.08, z0 + h),
                rot=(90, 0, 0), bevel=0.03), "stone_light")
    a.add(p_box((w, 0.14, h), loc=(x, y - 0.08, z0 + h / 2), bevel=0.03), col)
    a.add(p_cyl(w / 2, w / 2, 0.14, 12, loc=(x, y - 0.01, z0 + h), rot=(90, 0, 0), bevel=0.02), col)
    for z in (z0 + 0.22, z0 + h - 0.10):
        a.add(p_box((w - 0.02, 0.10, 0.12), loc=(x, y - 0.14, z), bevel=0.022), "wood_dark")
    a.add(p_cyl(0.07, 0.07, 0.06, 8, loc=(x + w / 2 - 0.16, y - 0.17, z0 + h * 0.46),
                rot=(90, 0, 0)), "iron_dark")


def gate(a, y, z0, w=1.50, h=1.90, x=0.0):
    """Большие двустворчатые ворота с оковкой."""
    a.add(p_box((w + 0.40, 0.22, h + 0.26), loc=(x, y - 0.01, z0 + h / 2), bevel=0.05), "stone_light")
    for sx in (-1, 1):
        a.add(p_box((w / 2 - 0.03, 0.15, h), loc=(x + sx * w / 4, y - 0.10, z0 + h / 2), bevel=0.035), "wood_mid")
        for z in (z0 + 0.24, z0 + h * 0.52, z0 + h - 0.20):
            a.add(p_box((w / 2 - 0.06, 0.10, 0.14), loc=(x + sx * w / 4, y - 0.16, z), bevel=0.025), "iron_dark")
        a.add(p_cyl(0.08, 0.08, 0.07, 8, loc=(x + sx * 0.14, y - 0.19, z0 + h * 0.50),
                    rot=(90, 0, 0)), "iron_dark")
    a.add(p_box((w + 0.50, 0.26, 0.24), loc=(x, y - 0.03, z0 + h + 0.16), bevel=0.05), "wood_dark")


def window(a, loc, rot=(0, 0, 0), w=0.46, h=0.54, shutters=True, sill=True):
    """Окно на фасаде (-Y). Смещения идут по мировому -Y: для боковых стен — window_on()."""
    x, y, z = loc
    a.add(p_box((w + 0.16, 0.14, h + 0.16), loc=(x, y, z), rot=rot, bevel=0.035), "wood_dark")
    a.add(p_box((w, 0.10, h), loc=(x, y - 0.03, z), rot=rot, bevel=0.025), "glass")
    if shutters:
        for sx in (-1, 1):
            a.add(p_box((0.26, 0.09, h + 0.10), loc=(x + sx * (w / 2 + 0.17), y - 0.06, z),
                        rot=rot, bevel=0.025), "roof_dark")
    if sill:
        a.add(p_box((w + 0.30, 0.22, 0.10), loc=(x, y - 0.04, z - h / 2 - 0.11), rot=rot, bevel=0.03), "stone_light")


def arrow_slit(a, loc, rot=(0, 0, 0), h=0.62):
    x, y, z = loc
    a.add(p_box((0.26, 0.10, h + 0.14), loc=(x, y, z), rot=rot, bevel=0.025), "stone_light")
    a.add(p_box((0.11, 0.08, h), loc=(x, y - 0.04, z), rot=rot, bevel=0.0), "black")


def banner(a, loc, w=0.44, h=1.05, rot=(0, 0, 0), col="roof", pole=True):
    """Хоругвь: полотно с вырезанным низом (две лопасти) + древко."""
    x, y, z = loc
    a.add(p_box((w, 0.05, h), loc=(x, y, z - h / 2), rot=rot, bevel=0.012), col)
    for sx in (-1, 1):
        a.add(p_box((w / 2 - 0.03, 0.05, 0.20), loc=(x + sx * w / 4, y, z - h - 0.09),
                    rot=rot, bevel=0.012), col)
    a.add(p_box((w + 0.10, 0.09, 0.10), loc=(x, y, z + 0.05), rot=rot, bevel=0.02), "wood_dark")
    if pole:
        a.add(p_cyl(0.045, 0.045, 0.34, 6, loc=(x, y, z + 0.05), rot=(0, 0, 0)), "wood_dark")


def ladder(a, x, y, z0, h, w=0.56, rungs=None, rot=(0, 0, 0)):
    rungs = rungs or max(3, int(h / 0.34))
    for sx in (-1, 1):
        a.add(p_box((0.09, 0.09, h), loc=(x + sx * w / 2, y, z0 + h / 2), rot=rot, bevel=0.02), "wood_mid")
    for i in range(rungs):
        z = z0 + (i + 0.5) * h / rungs
        a.add(p_box((w, 0.07, 0.07), loc=(x, y, z), rot=rot, bevel=0.015), "wood_light")


def barrel(a, loc, r=0.22, h=0.46, rot=(0, 0, 0)):
    x, y, z = loc
    a.add(p_cyl(r, r, h, 12, loc=(x, y, z), rot=rot, bevel=0.03),
          by_normal("wood_pale", "wood_mid", "wood_dark", 0.7))
    for t in (0.22, 0.72):
        a.add(p_cyl(r + 0.025, r + 0.025, 0.06, 12, loc=(x, y, z + h * t), rot=rot), "iron_dark")


def crate(a, loc, s=0.42, rot=(0, 0, 0)):
    x, y, z = loc
    a.add(p_box((s, s, s), loc=(x, y, z + s / 2), rot=rot, bevel=0.025),
          by_normal("wood_pale", "wood_light", "wood_dark", 0.7))
    for sx in (-1, 1):
        a.add(p_box((s + 0.02, 0.06, 0.07), loc=(x, y + sx * s / 2, z + s * 0.5), rot=rot, bevel=0.012), "wood_dark")
        a.add(p_box((0.06, s + 0.02, 0.07), loc=(x + sx * s / 2, y, z + s * 0.5), rot=rot, bevel=0.012), "wood_dark")


def log_piece(a, loc, r=0.15, L=1.10, rot=(0, 90, 0)):
    """Бревно вдоль +X от loc (p_cyl растёт от основания): торцы светлые, бока — кора."""
    x, y, z = loc
    a.add(p_cyl(r, r, L, 8, loc=(x, y, z), rot=rot, bevel=0.02),
          lambda f: "wood_pale" if abs(f.normal.x) > 0.7 else "bark")


def hay_bale(a, loc, s=(0.52, 0.40, 0.34), rot=(0, 0, 0)):
    x, y, z = loc
    a.add(p_box(s, loc=(x, y, z + s[2] / 2), rot=rot, bevel=0.04),
          by_normal("wood_yellow", "burlap", "burlap_dark", 0.7))
    a.add(p_box((s[0] + 0.02, 0.06, s[2] + 0.02), loc=(x, y, z + s[2] / 2), rot=rot, bevel=0.012), "rope")


def stone_base(a, w, d, h=0.26, col="stone_dark"):
    """Цоколь: низ уходит под 0 на 14 см (как у Bld_House кита)."""
    a.add(p_box((w, d, h + 0.14), loc=(0, 0, (h - 0.14) / 2), bevel=0.06), col)


def post(a, x, y, z0, h, s=0.20, col="wood_dark"):
    a.add(p_box((s, s, h), loc=(x, y, z0 + h / 2), bevel=0.04), col)


def brace(a, x, y, z, length=0.5, angle=38.0, axis="y", col="wood_dark"):
    """Угловой раскос."""
    rot = (0, angle, 0) if axis == "y" else (angle, 0, 0)
    a.add(p_box((0.11, 0.11, length), loc=(x, y, z), rot=rot, bevel=0.02), col)


# =========================================================================================
# Новое: локальная рама
# =========================================================================================
STONE_TOP = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)     # каменные массы
STONE_DARK_TOP = by_normal("stone_mid", "stone_dark", "stone_dark", 0.8)
PLANK_TOP = by_normal("wood_light", "wood_mid", "wood_dark", 0.8)       # дощатые настилы


class Frame:
    """
    Локальная система координат: всё, что добавлено через раму, задаётся в её осях,
    а поворот/масштаб рамы применяется к детали целиком. Нужна для составных вещей,
    которые ставятся под углом (стойки с оружием, животные, щиты на стене), — в
    window()/arrow_slit() смещения идут по мировым осям и под поворотом «уезжают».

    fr = Frame((1.2, -0.8, 0.3), rz=25)     # позиция и поворот вокруг Z (градусы)
    fr.box(a, (0.4, 0.1, 0.3), (0, 0, 0.15), col="wood_mid")
    sub = fr.sub((0.2, 0, 0.5), rot=(0, 30, 0))  # вложенная рама
    """

    def __init__(self, loc=(0, 0, 0), rz=0.0, rot=None, s=1.0, parent=None):
        r = rot if rot is not None else (0, 0, rz)
        sc = s if isinstance(s, (tuple, list)) else (s, s, s)
        m = TM(loc, r, sc)
        self.m = parent.m @ m if parent is not None else m

    def sub(self, loc=(0, 0, 0), rz=0.0, rot=None, s=1.0):
        return Frame(loc, rz, rot, s, parent=self)

    def M(self, loc=(0, 0, 0), rot=(0, 0, 0)):
        return self.m @ TM(loc, rot)

    def at(self, loc=(0, 0, 0)):
        """Мировая точка для локальной."""
        return self.m @ Vector(loc)

    # примитивы в осях рамы (сигнатуры повторяют p_*; цвет — именованный параметр col)
    def box(self, a, size, loc=(0, 0, 0), rot=(0, 0, 0), col="wood_mid", bevel=0.03):
        a.add(p_box(size, bevel=bevel, mat=self.M(loc, rot)), col)

    def cyl(self, a, r1, r2, h, seg=8, loc=(0, 0, 0), rot=(0, 0, 0), col="wood_mid", bevel=0.0,
            spin=0.0, cap=True):
        a.add(p_cyl(r1, r2, h, seg, bevel=bevel, spin=spin, cap=cap, mat=self.M(loc, rot)), col)

    def taper(self, a, bottom, top, h, loc=(0, 0, 0), rot=(0, 0, 0), col="wood_mid", bevel=0.0):
        a.add(p_taper_box(bottom, top, h, bevel=bevel, mat=self.M(loc, rot)), col)

    def ico(self, a, r, loc=(0, 0, 0), scl=(1, 1, 1), rot=(0, 0, 0), col="wool", sub=1,
            jitter=0.0, rng=None, cut=None):
        a.add(p_ico(r, sub, scl=scl, jitter=jitter, rng=rng, cut=cut, mat=self.M(loc, rot)), col)

    def prism(self, a, pts_xz, depth, loc=(0, 0, 0), rot=(0, 0, 0), col="wood_mid"):
        a.add(p_prism(pts_xz, depth, mat=self.M(loc, rot)), col)


def seg_frame(p0, p1, z=0.0):
    """Рама, у которой начало в p0, ось +X смотрит на p1 (оба (x, y)), и длина отрезка."""
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    return Frame((p0[0], p0[1], z), rz=math.degrees(math.atan2(dy, dx))), math.hypot(dx, dy)


# =========================================================================================
# Изгороди
# =========================================================================================
def fence_line(a, p0, p1, h=0.72, posts=None, rails=(0.28, 0.56), post_s=0.14,
               post_col="wood_dark", rail_col="wood_light", z0=0.0, end_posts=(True, True),
               pointed=True):
    """
    Жердевая изгородь от p0 до p1 (точки (x, y)). Столбы толщиной post_s, жерди 0.09 x 0.13.
    end_posts=(False, True) — не ставить столб в начале (угол уже поставлен соседним пряслом).
    """
    fr, L = seg_frame(p0, p1, z0)
    n = posts or max(2, int(round(L / 1.0)) + 1)
    for i in range(n):
        if (i == 0 and not end_posts[0]) or (i == n - 1 and not end_posts[1]):
            continue
        x = L * i / (n - 1)
        fr.box(a, (post_s, post_s, h), (x, 0, h / 2 - 0.06), col=post_col, bevel=0.035)
        if pointed:
            fr.cyl(a, post_s * 0.72, 0.0, 0.12, 4, loc=(x, 0, h - 0.06), col=post_col, spin=45)
    for zr in rails:
        fr.box(a, (L + 0.06, 0.09, 0.13), (L / 2, -0.02, zr), col=rail_col, bevel=0.03)


# =========================================================================================
# Оружие и снаряжение (общие для кузницы, склада экипировки и мастерской щитов)
# Всё строится в раме fr; «стоя» — ось предмета вдоль локального +Z, широкая сторона к -Y.
# =========================================================================================
def sword(a, fr, L=1.0, guard="gold", grip="leather_dark", blade="steel", pommel="gold", w=0.11):
    """Меч навершием вниз: навершие в z=0, остриё в z=L. Клинок широкой стороной к -Y."""
    fr.ico(a, 0.052, loc=(0, 0, 0.045), col=pommel, scl=(1, 0.8, 1))
    fr.box(a, (0.055, 0.055, 0.19), (0, 0, 0.17), col=grip, bevel=0.012)
    fr.box(a, (0.34, 0.075, 0.07), (0, 0, 0.285), col=guard, bevel=0.02)
    bl = L - 0.32
    body = bl * 0.80
    fr.taper(a, (w, 0.04), (w * 0.86, 0.036), body, loc=(0, 0, 0.30), col=blade, bevel=0.0)
    fr.taper(a, (w * 0.86, 0.036), (0.012, 0.012), bl - body, loc=(0, 0, 0.30 + body), col=blade)
    # дол — тёмная полоса по центру клинка, иначе на дистанции клинок читается как планка
    fr.box(a, (0.026, 0.046, body * 0.78), (0, 0, 0.30 + body * 0.45), col="iron_light", bevel=0.0)


def axe(a, fr, L=0.95, head="steel", handle="wood_light", double=False):
    """Топор: пятка топорища в z=0, лезвие у верха, режущая кромка смотрит в +X."""
    fr.cyl(a, 0.034, 0.03, L, 6, loc=(0, 0, 0), col=handle)
    zc = L - 0.13
    fr.box(a, (0.12, 0.085, 0.16), (0.02, 0, zc), col="iron_dark", bevel=0.015)
    for sx in ((1, -1) if double else (1,)):
        # клин: трапеция в плоскости XZ, толщина по Y
        pts = [(0.05, -0.07), (0.25, -0.15), (0.25, 0.15), (0.05, 0.07)]
        if sx < 0:
            pts = [(-x, z) for x, z in reversed(pts)]
        fr.prism(a, pts, 0.06, loc=(0, 0, zc), col=head)
        fr.box(a, (0.035, 0.07, 0.30), (sx * 0.255, 0, zc), col="iron_light", bevel=0.01)


def hammer(a, fr, L=0.62, head="iron_dark"):
    """Молот: рукоять вдоль +Z, боёк поперёк по X."""
    fr.cyl(a, 0.03, 0.026, L, 6, col="wood_light")
    fr.box(a, (0.24, 0.11, 0.11), (0, 0, L - 0.02), col=head, bevel=0.02)
    fr.box(a, (0.05, 0.12, 0.12), (0.12, 0, L - 0.02), col="iron_light", bevel=0.015)


def spear(a, fr, L=2.0, head="steel", shaft="wood_mid"):
    """Копьё: низ древка в z=0, наконечник сверху."""
    fr.cyl(a, 0.036, 0.03, L - 0.22, 6, col=shaft)
    fr.cyl(a, 0.05, 0.05, 0.06, 6, loc=(0, 0, L - 0.26), col="iron_dark")
    fr.taper(a, (0.12, 0.04), (0.01, 0.01), 0.26, loc=(0, 0, L - 0.22), col=head)


def _circle(r, n, a0=0.0, a1=360.0, center=True):
    pts = [(0.0, 0.0)] if center else []
    steps = n if (a1 - a0) < 359.9 else n - 1
    for i in range(steps + 1):
        t = math.radians(a0 + (a1 - a0) * i / n)
        pts.append((r * math.cos(t), r * math.sin(t)))
    return pts


def round_shield(a, fr, r=0.45, t=0.07, face="roof", paint="cream", pattern="quarters",
                 rim="iron_dark", boss="gold", studs=0, seg=16):
    """
    Круглый щит в плоскости XZ рамы, лицом к -Y, центр в начале рамы.
    pattern: plain | halves | quarters | band | cross | ring | wedges.
    Кромка — это задний диск радиуса r, лицевой диск на 6 см меньше: кольцо без булевых.
    """
    fr.cyl(a, r, r, t, seg, loc=(0, t / 2, 0), rot=(90, 0, 0), col=rim, bevel=0.012)
    rf = r - 0.065
    yf = -t / 2
    fr.cyl(a, rf, rf, 0.02, seg, loc=(0, yf + 0.002, 0), rot=(90, 0, 0), col=face)
    yp = yf - 0.02          # роспись на 8 мм перед лицевым диском: без общих плоскостей
    if pattern == "halves":
        fr.prism(a, _circle(rf, seg // 2, 90, 270), 0.012, loc=(0, yp, 0), col=paint)
    elif pattern == "quarters":
        for a0 in (0, 180):
            fr.prism(a, _circle(rf, seg // 4, a0, a0 + 90), 0.012, loc=(0, yp, 0), col=paint)
    elif pattern == "wedges":
        for a0 in (0, 90, 180, 270):
            fr.prism(a, _circle(rf, 2, a0 + 22.5, a0 + 67.5), 0.012, loc=(0, yp, 0), col=paint)
    elif pattern == "band":
        fr.box(a, (rf * 0.62, 0.012, rf * 1.84), (0, yp, 0), col=paint, bevel=0.0)
    elif pattern == "cross":
        fr.box(a, (rf * 0.42, 0.012, rf * 1.86), (0, yp, 0), col=paint, bevel=0.0)
        fr.box(a, (rf * 1.86, 0.012, rf * 0.42), (0, yp - 0.005, 0), col=paint, bevel=0.0)
    elif pattern == "ring":
        fr.cyl(a, rf * 0.62, rf * 0.62, 0.012, seg, loc=(0, yp + 0.006, 0), rot=(90, 0, 0), col=paint)
        fr.cyl(a, rf * 0.40, rf * 0.40, 0.012, seg, loc=(0, yp - 0.002, 0), rot=(90, 0, 0), col=face)
    fr.ico(a, max(0.06, r * 0.2), loc=(0, yf - 0.01, 0), scl=(1, 1, 0.62), col=boss, cut=0.0,
           rot=(90, 0, 0))
    for i in range(studs):
        ang = math.tau * (i + 0.5) / studs
        fr.box(a, (0.055, 0.03, 0.055), (math.cos(ang) * (r - 0.032), yf - 0.012, math.sin(ang) * (r - 0.032)),
               col=boss, bevel=0.01)


def heater_outline(w, h):
    """Контур геральдического щита (плоский верх, остриё внизу) в XZ, CCW, центр в 0."""
    return [(0.0, -h / 2), (w * 0.30, -h * 0.31), (w * 0.45, -h * 0.10), (w * 0.50, h * 0.12),
            (w * 0.50, h / 2), (-w * 0.50, h / 2), (-w * 0.50, h * 0.12), (-w * 0.45, -h * 0.10),
            (-w * 0.30, -h * 0.31)]


def heater_shield(a, fr, w=0.60, h=0.78, t=0.07, face="roof", rim="gold", emblem="chevron",
                  paint="cream"):
    """
    Геральдический щит лицом к -Y. emblem: none | chevron | cross | swords | band | star.
    Кромка — увеличенный задний контур, как у round_shield.
    """
    fr.prism(a, heater_outline(w + 0.12, h + 0.12), t, loc=(0, t / 2, -0.01), col=rim)
    yf = -0.005
    fr.prism(a, heater_outline(w, h), 0.03, loc=(0, yf, 0), col=face)
    yp = yf - 0.02
    if emblem == "chevron":
        pts = [(0.0, h * 0.18), (w * 0.44, -h * 0.12), (w * 0.44, h * 0.04), (0.0, h * 0.34),
               (-w * 0.44, h * 0.04), (-w * 0.44, -h * 0.12)]
        fr.prism(a, pts, 0.016, loc=(0, yp, 0), col=paint)
    elif emblem == "cross":
        fr.box(a, (w * 0.20, 0.016, h * 0.78), (0, yp, -h * 0.03), col=paint, bevel=0.0)
        fr.box(a, (w * 0.84, 0.016, h * 0.18), (0, yp - 0.005, h * 0.12), col=paint, bevel=0.0)
    elif emblem == "band":
        fr.box(a, (w * 0.26, 0.016, h * 0.86), (0, yp, -h * 0.02), col=paint, bevel=0.0)
    elif emblem == "star":
        pts = []
        for i in range(10):
            ang = math.radians(90 + 36 * i)
            rr = (w * 0.26) if i % 2 == 0 else (w * 0.11)
            pts.append((rr * math.cos(ang), h * 0.06 + rr * math.sin(ang)))
        fr.prism(a, pts, 0.016, loc=(0, yp, 0), col=paint)
    elif emblem == "swords":
        for sgn in (-1, 1):
            s = fr.sub((0, yp - 0.01, h * 0.02), rot=(0, sgn * 38, 0), s=0.52 * h / 0.78)
            sword(a, s.sub((0, 0, -0.48)), L=0.96, guard="gold", grip="leather_dark", blade=paint,
                  pommel="gold", w=0.12)


def breastplate(a, fr, col="steel", trim="gold"):
    """Кираса с наплечниками: низ в z=0, высота ~0.62, лицом к -Y."""
    fr.taper(a, (0.34, 0.22), (0.46, 0.26), 0.44, loc=(0, 0, 0.0), col=col, bevel=0.03)
    fr.box(a, (0.36, 0.20, 0.06), (0, 0, 0.02), col=trim, bevel=0.015)       # пояс
    fr.box(a, (0.07, 0.02, 0.30), (0, -0.125, 0.22), col="iron_light", bevel=0.0)  # рёбро кирасы
    for sx in (-1, 1):
        fr.ico(a, 0.13, loc=(sx * 0.25, 0, 0.44), scl=(1.05, 1.0, 0.8), col=col, cut=-0.02)
        fr.box(a, (0.16, 0.22, 0.05), (sx * 0.27, 0, 0.36), col=trim, bevel=0.015)
    fr.box(a, (0.20, 0.20, 0.08), (0, 0, 0.47), col="iron_dark", bevel=0.02)   # горжет


def helmet(a, fr, col="steel", crest="roof", kind="nasal"):
    """Шлем: низ в z=0, ~0.30 высотой. kind: nasal (купол + наносник) | great (ведро с прорезью)."""
    if kind == "great":
        fr.cyl(a, 0.15, 0.14, 0.24, 8, col=col, bevel=0.02, spin=22.5)
        fr.cyl(a, 0.14, 0.05, 0.08, 8, loc=(0, 0, 0.24), col=col, spin=22.5)
        fr.box(a, (0.22, 0.03, 0.035), (0, -0.14, 0.15), col="black", bevel=0.0)
        fr.box(a, (0.035, 0.03, 0.14), (0, -0.145, 0.10), col="iron_dark", bevel=0.0)
    else:
        fr.cyl(a, 0.15, 0.15, 0.10, 8, col=col, bevel=0.015, spin=22.5)
        fr.ico(a, 0.15, loc=(0, 0, 0.09), scl=(1, 1, 1.05), col=col, cut=0.0)
        fr.box(a, (0.045, 0.04, 0.16), (0, -0.155, 0.06), col="iron_dark", bevel=0.01)
        fr.box(a, (0.33, 0.33, 0.035), (0, 0, 0.02), col="iron_dark", bevel=0.01)
    if crest:
        fr.box(a, (0.05, 0.24, 0.10), (0, 0.02, 0.29 if kind == "nasal" else 0.33), col=crest, bevel=0.015)


def armor_stand(a, fr, col="steel", trim="gold", crest="roof", helm="nasal", base="wood_dark"):
    """Манекен с доспехом: крестовина-основание, стойка, кираса, шлем. Высота ~1.55."""
    for rz in (0, 90):
        fr.box(a, (0.46, 0.09, 0.07), (0, 0, 0.035), rot=(0, 0, rz), col=base, bevel=0.015)
    fr.box(a, (0.08, 0.08, 0.86), (0, 0, 0.43), col=base, bevel=0.015)
    breastplate(a, fr.sub((0, 0, 0.80)), col=col, trim=trim)
    helmet(a, fr.sub((0, 0, 1.30)), col=col, crest=crest, kind=helm)


def crystal(a, fr, r=0.3, h=1.2, glow_faces=True, top="crystal_light", side="crystal",
            dark="crystal_dark", glow="arcane", seg=6):
    """
    Кристалл-бипирамида по оси Z, центр основания нижнего острия в z=0, верх в z=h.
    Грани раскрашены по нормали; glow_faces — каждая вторая боковая грань светится (эмиссия).
    """
    hb, hm, ht = h * 0.30, h * 0.40, h * 0.30

    def col_fn(f):
        n = f.normal
        if n.z > 0.45:
            return top
        if n.z < -0.45:
            return dark
        # боковые: чередуем по азимуту нормали — светящиеся и тёмные полосы
        az = math.degrees(math.atan2(n.y, n.x)) % 360
        k = int(az // (360 / seg))
        if glow_faces and k % 2 == 0:
            return glow
        return side

    fr.cyl(a, 0.0, r, hb, seg, loc=(0, 0, 0), col=col_fn)
    fr.cyl(a, r, r * 0.92, hm, seg, loc=(0, 0, hb), col=col_fn)
    fr.cyl(a, r * 0.92, 0.0, ht, seg, loc=(0, 0, hb + hm), col=col_fn)


def lantern(a, fr, hang=True):
    """Фонарь как у Prop_LanternPost: светящийся куб с железной шапкой; hang — на цепи-стержне."""
    if hang:
        fr.box(a, (0.04, 0.04, 0.16), (0, 0, 0.30), col="iron_dark", bevel=0.0)
    fr.cyl(a, 0.15, 0.0, 0.12, 4, loc=(0, 0, 0.14), spin=45, col="iron_dark")
    fr.box(a, (0.17, 0.17, 0.22), (0, 0, 0.03), col="lantern_glow", bevel=0.035)
    fr.box(a, (0.21, 0.21, 0.05), (0, 0, -0.09), col="iron_dark", bevel=0.015)


def bucket(a, fr, r=0.14, h=0.22, fill="water"):
    """Ведро без крышки: стенка — открытая труба, дно и зеркало жидкости отдельными дисками."""
    fr.cyl(a, r * 0.86, r, h, 10, col="wood_mid", cap=False)
    fr.cyl(a, r * 0.86, r * 0.86, 0.03, 10, loc=(0, 0, 0), col="wood_dark")
    fr.cyl(a, r + 0.012, r + 0.012, 0.04, 10, loc=(0, 0, h * 0.62), col="iron_dark")
    fr.cyl(a, r + 0.014, r + 0.014, 0.035, 10, loc=(0, 0, h - 0.035), col="iron_dark")
    fr.cyl(a, r * 0.94, r * 0.94, 0.012, 10, loc=(0, 0, h - 0.06), col=fill or "wood_dark")


def tub(a, fr, r=0.45, h=0.50, fill="water", hoops=(0.25, 0.75)):
    """Кадка/чан без крышки: стенка — открытая труба, жидкость на 6 см ниже края."""
    fr.cyl(a, r * 0.9, r, h, 14, col="wood_mid", cap=False)
    fr.cyl(a, r * 0.9, r * 0.9, 0.04, 14, loc=(0, 0, 0), col="wood_dark")
    for t in hoops:
        rr = r * (0.9 + 0.1 * t) + 0.02
        fr.cyl(a, rr, rr, 0.05, 14, loc=(0, 0, h * t - 0.025), col="iron_dark")
    fr.cyl(a, r + 0.02, r + 0.02, 0.06, 14, loc=(0, 0, h - 0.06), col="wood_dark")   # венец по краю
    fr.cyl(a, r * 0.97, r * 0.97, 0.02, 14, loc=(0, 0, h - 0.08), col=fill or "wood_dark")


def plank_stack(a, fr, L=1.3, w=0.22, t=0.06, cols=2, layers=4, spacers=True,
                tones=("wood_pale", "wood_light")):
    """Штабель досок вдоль X рамы, низ в z=0; прокладки поперёк через слой."""
    z = 0.0
    if spacers:
        for x in (-L * 0.36, L * 0.36):
            fr.box(a, (0.10, cols * (w + 0.03) + 0.06, 0.07), (x, 0, 0.035), col="wood_dark", bevel=0.015)
        z = 0.07
    for k in range(layers):
        for j in range(cols):
            y = (j - (cols - 1) / 2) * (w + 0.03)
            fr.box(a, (L - 0.04 * ((k + j) % 2), w, t), (0.02 * ((k * 3 + j) % 3 - 1), y, z + t / 2),
                   col=tones[(k + j) % len(tones)], bevel=0.012)
        z += t


def log_pyramid(a, fr, L=1.8, r=0.16, rows=(4, 3, 2), stakes=True):
    """Штабель брёвен вдоль X рамы: ряды ложатся в ложбины нижнего ряда, низ в z=0."""
    step = 2 * r * 1.02
    for row, n in enumerate(rows):
        z = r + row * step * 0.866
        for k in range(n):
            y = (k - (n - 1) / 2) * step
            fr.cyl(a, r, r, L, 8, loc=(-L / 2, y, z), rot=(0, 90, 0),
                   col=lambda f, fr=fr: "wood_pale" if abs((fr.m.to_3x3().inverted() @ f.normal).x) > 0.7 else "bark",
                   bevel=0.02)
    if stakes:
        half = (rows[0] - 1) / 2 * step + r + 0.06
        hs = (2 * r + (len(rows) - 1) * step * 0.866) * 0.8
        for sx in (-1, 1):
            for sy in (-1, 1):
                fr.box(a, (0.10, 0.10, hs), (sx * L * 0.36, sy * half, hs / 2 - 0.04),
                       col="wood_dark", bevel=0.02)
