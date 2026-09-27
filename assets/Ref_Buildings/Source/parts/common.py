"""
Общие детали зданий Vitaria. Всё здесь уже проверено рендером — не переписывай,
вызывай. Фасад любого здания смотрит в -Y, пивот внизу по центру (z=0).
"""
import math
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_prism, p_ico, by_normal


def gable_roof(a, w, d, ztop, pitch_deg=36.0, ox=0.25, oy=0.22, bands=2,
               top="roof", bot="roof_dark", trim="wood_dark", ridge_cap=True):
    """Двускатная кровля над коробкой w x d с верхом стен на ztop. Возвращает высоту конька zr."""
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
    """
    То же, что gable_roof, но конёк идёт вдоль X: w — длина конька, d — перекрываемый
    пролёт поперёк. Нужна для зданий, вытянутых по X (зал казармы): у gable_roof конёк
    всегда вдоль Y, и на широком корпусе скаты разворачивало поперёк длинной оси.
    """
    pitch = math.radians(pitch_deg)
    zr = ztop + 0.08 + (d / 2) * math.tan(pitch)
    L = (d / 2 + oy) / math.cos(pitch)
    Lx = w + 2 * ox
    for sy in (-1, 1):
        # вращение вокруг X на -sy*pitch даёт нормаль (0, sy*sin, cos) — скат вниз к sy
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
    """Фронтоны для gable_roof_x — они стоят на торцах по X."""
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
    """Треугольные фронтоны, закрывающие торцы под скатами."""
    for sy in (-1, 1):
        a.add(p_prism([(-w / 2, ztop), (w / 2, ztop), (0, zr - 0.04)], depth,
                      loc=(0, sy * (d / 2 - inset), 0)), col)


def chimney(a, x, y, zbase, h=1.05, w=0.42, col="stone_mid"):
    a.add(p_box((w, w, h), loc=(x, y, zbase + h / 2), bevel=0.045), col)
    a.add(p_box((w + 0.13, w + 0.13, 0.14), loc=(x, y, zbase + h - 0.05), bevel=0.045), "stone_dark")
    a.add(p_box((w - 0.16, w - 0.16, 0.03), loc=(x, y, zbase + h + 0.03)), "black")


def cornice(a, w, d, z, t=0.16, col="wood_dark"):
    """Карниз/пояс по периметру коробки w x d на высоте z."""
    for sy in (-1, 1):
        a.add(p_box((w + t, 0.20, t), loc=(0, sy * d / 2, z), bevel=0.04), col)
    for sx in (-1, 1):
        a.add(p_box((0.20, d + t, t), loc=(sx * w / 2, 0, z), bevel=0.04), col)


def battlements(a, w, d, z, bw=0.40, bh=0.46, gap=0.34, col="stone_light"):
    """Зубцы по периметру прямоугольника w x d, низ зубцов на z."""
    def run(length):
        n = max(2, int((length + gap) // (bw + gap)))
        step = (length - bw) / max(1, n - 1)
        return [-length / 2 + bw / 2 + i * step for i in range(n)]
    for sy in (-1, 1):
        for x in run(w):
            a.add(p_box((bw, 0.30, bh), loc=(x, sy * (d / 2 - 0.13), z + bh / 2), bevel=0.04), col)
    for sx in (-1, 1):
        for y in run(d - 0.7):
            a.add(p_box((0.30, bw, bh), loc=(sx * (w / 2 - 0.13), y, z + bh / 2), bevel=0.04), col)


def round_battlements(a, r, z, n=10, bw=0.32, bh=0.42, col="stone_light"):
    """Зубцы по окружности радиуса r."""
    for i in range(n):
        ang = 2 * math.pi * i / n
        x, y = r * math.cos(ang), r * math.sin(ang)
        a.add(p_box((bw, 0.26, bh), loc=(x, y, z + bh / 2), rot=(0, 0, math.degrees(ang)), bevel=0.035), col)


def plank_door(a, y, z0, w=0.62, h=1.16, col="wood_light", frame="wood_dark", x=0.0):
    """Дверь на фасаде (-Y): полотно, две поперечины, раскос, ручка, наличник."""
    a.add(p_box((w + 0.20, 0.16, h + 0.20), loc=(x, y - 0.02, z0 + h / 2), bevel=0.04), frame)
    a.add(p_box((w, 0.12, h), loc=(x, y - 0.06, z0 + h / 2), bevel=0.03), col)
    for z in (z0 + 0.18, z0 + h - 0.18):
        a.add(p_box((w - 0.04, 0.09, 0.13), loc=(x, y - 0.11, z), bevel=0.025), frame)
    a.add(p_box((0.12, 0.09, h - 0.30), loc=(x, y - 0.115, z0 + h / 2), rot=(0, 24, 0), bevel=0.02), frame)
    a.add(p_box((0.09, 0.09, 0.16), loc=(x + w / 2 - 0.16, y - 0.14, z0 + h * 0.52), bevel=0.022), "iron_dark")


def arch_door(a, y, z0, w=0.72, h=1.30, x=0.0, col="wood_mid"):
    """Арочная дверь в каменном портале: скруглённый верх набран тремя блоками."""
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
    """Большие двустворчатые ворота с оковкой — казарма, склад, замок."""
    a.add(p_box((w + 0.40, 0.22, h + 0.26), loc=(x, y - 0.01, z0 + h / 2), bevel=0.05), "stone_light")
    for sx in (-1, 1):
        a.add(p_box((w / 2 - 0.03, 0.15, h), loc=(x + sx * w / 4, y - 0.10, z0 + h / 2), bevel=0.035), "wood_mid")
        for z in (z0 + 0.24, z0 + h * 0.52, z0 + h - 0.20):
            a.add(p_box((w / 2 - 0.06, 0.10, 0.14), loc=(x + sx * w / 4, y - 0.16, z), bevel=0.025), "iron_dark")
        a.add(p_cyl(0.08, 0.08, 0.07, 8, loc=(x + sx * 0.14, y - 0.19, z0 + h * 0.50),
                    rot=(90, 0, 0)), "iron_dark")
    a.add(p_box((w + 0.50, 0.26, 0.24), loc=(x, y - 0.03, z0 + h + 0.16), bevel=0.05), "wood_dark")


def window(a, loc, rot=(0, 0, 0), w=0.46, h=0.54, shutters=True, sill=True):
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
    """Бойница: узкий тёмный проём в светлом обрамлении."""
    x, y, z = loc
    a.add(p_box((0.26, 0.10, h + 0.14), loc=(x, y, z), rot=rot, bevel=0.025), "stone_light")
    a.add(p_box((0.11, 0.08, h), loc=(x, y - 0.04, z), rot=rot, bevel=0.0), "black")


def banner(a, loc, w=0.44, h=1.05, rot=(0, 0, 0), col="roof", pole=True):
    """Синяя хоругвь: полотно с вырезанным низом (две лопасти) + древко."""
    x, y, z = loc
    a.add(p_box((w, 0.05, h), loc=(x, y, z - h / 2), rot=rot, bevel=0.012), col)
    for sx in (-1, 1):
        a.add(p_box((w / 2 - 0.03, 0.05, 0.20), loc=(x + sx * w / 4, y, z - h - 0.09),
                    rot=rot, bevel=0.012), col)
    a.add(p_box((w + 0.10, 0.09, 0.10), loc=(x, y, z + 0.05), rot=rot, bevel=0.02), "wood_dark")
    if pole:
        a.add(p_cyl(0.045, 0.045, 0.34, 6, loc=(x, y, z + 0.05), rot=(0, 0, 0)), "wood_dark")


def flag_pole(a, x, y, z0, h=1.0, col="cream"):
    a.add(p_cyl(0.05, 0.04, h, 6, loc=(x, y, z0)), "wood_dark")
    a.add(p_box((0.05, 0.62, 0.30), loc=(x, y - 0.33, z0 + h - 0.20), bevel=0.01), col)


def ladder(a, x, y, z0, h, w=0.56, rungs=None, rot=(0, 0, 0)):
    rungs = rungs or max(3, int(h / 0.34))
    for sx in (-1, 1):
        a.add(p_box((0.09, 0.09, h), loc=(x + sx * w / 2, y, z0 + h / 2), rot=rot, bevel=0.02), "wood_mid")
    for i in range(rungs):
        z = z0 + (i + 0.5) * h / rungs
        a.add(p_box((w, 0.07, 0.07), loc=(x, y, z), rot=rot, bevel=0.015), "wood_light")


def railing(a, w, d, z, h=0.50, col="wood_mid", posts_x=3, posts_y=3):
    """Перила по периметру площадки w x d, низ на z."""
    for sy in (-1, 1):
        for i in range(posts_x):
            x = -w / 2 + w * i / (posts_x - 1)
            a.add(p_box((0.10, 0.10, h), loc=(x, sy * d / 2, z + h / 2), bevel=0.02), "wood_dark")
        a.add(p_box((w + 0.10, 0.10, 0.10), loc=(0, sy * d / 2, z + h - 0.05), bevel=0.02), col)
        a.add(p_box((w + 0.10, 0.08, 0.14), loc=(0, sy * d / 2, z + h * 0.42), bevel=0.02), col)
    for sx in (-1, 1):
        for i in range(posts_y):
            y = -d / 2 + d * i / (posts_y - 1)
            a.add(p_box((0.10, 0.10, h), loc=(sx * w / 2, y, z + h / 2), bevel=0.02), "wood_dark")
        a.add(p_box((0.10, d + 0.10, 0.10), loc=(sx * w / 2, 0, z + h - 0.05), bevel=0.02), col)
        a.add(p_box((0.08, d + 0.10, 0.14), loc=(sx * w / 2, 0, z + h * 0.42), bevel=0.02), col)


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
    """Бревно: торцы светлее боков — как в Res_LogPile кита."""
    x, y, z = loc
    a.add(p_cyl(r, r, L, 8, loc=(x, y, z), rot=rot, bevel=0.02),
          lambda f: "wood_pale" if abs(f.normal.x) > 0.7 else "bark")


def hay_bale(a, loc, s=(0.52, 0.40, 0.34), rot=(0, 0, 0)):
    x, y, z = loc
    a.add(p_box(s, loc=(x, y, z + s[2] / 2), rot=rot, bevel=0.04),
          by_normal("wood_yellow", "burlap", "burlap_dark", 0.7))
    a.add(p_box((s[0] + 0.02, 0.06, s[2] + 0.02), loc=(x, y, z + s[2] / 2), rot=rot, bevel=0.012), "rope")


def stone_base(a, w, d, h=0.26, col="stone_dark"):
    """Цоколь под здание: чуть шире корпуса, низ уходит под 0."""
    a.add(p_box((w, d, h + 0.14), loc=(0, 0, (h - 0.14) / 2), bevel=0.06), col)


def post(a, x, y, z0, h, s=0.20, col="wood_dark"):
    a.add(p_box((s, s, h), loc=(x, y, z0 + h / 2), bevel=0.04), col)


def brace(a, x, y, z, length=0.5, angle=38.0, axis="y", col="wood_dark"):
    """Угловой раскос — читается как каркас, дёшев по треугольникам."""
    rot = (0, angle, 0) if axis == "y" else (angle, 0, 0)
    a.add(p_box((0.11, 0.11, length), loc=(x, y, z), rot=rot, bevel=0.02), col)
