"""
Архитектура уровней зданий поля: из чего перестраивается здание на уровнях 2 и 3.

Уровень 2 и 3 — не уровень 1 с украшениями, а то же здание, перестроенное богаче (evolve(a, level) модуля
собирает модель целиком). С камеры колонии здание в игре размером с палец, поэтому рост читается массой,
материалом и силуэтом, а не мелочами:

  уровень 1 — постройка: навес, шатёр, сарай; дерево, открытые стороны;
  уровень 2 — мастерская: здание закрыто и стоит на каменном цоколе (камень там, где было дерево),
              выше и шире кровля, появляется второй объём (пристройка, навес, крыльцо, труба-стояк),
              производство вдвое: второй станок, второй штабель;
  уровень 3 — гильдия: два этажа — низ тёсаный камень с угловыми квадрами, верх фахверк по светлой
              штукатурке или второй сруб; окна со светом, слуховые окна, высокая труба, крупная машина
              своего ремесла (кран, ворот, копёр, водяное колесо).

Кровля остаётся цвета своей цепочки (ROOF_THEME): по ней игрок узнаёт здание на любом уровне.
Ни флажков, ни знамён: знак уровня — само здание.

Все функции ставят детали в мировых осях участка (фасад — к -Y, как у кита). Общие сборки common.py,
которые считают от центра (gable_roof, plank_door, window, chimney), ставятся в любое место через Shift.
"""
import math
import random

import bmesh
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal, TM
from vitaria_buildings.common import Frame, gable_roof, gable_roof_x, gable_wall_x, hip_roof, lantern

WALL = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)       # кладка стен (как у кузницы уровня 1)
RUBBLE = by_normal("stone_mid", "stone_dark", "stone_dark", 0.8)      # цоколь и бутовые основания
PLASTER = "wool"                                                        # штукатурка фахверка
BEAM = "wood_dark"                                                      # брус фахверка и обвязки
PLANKS = by_normal("wood_light", "wood_mid", "wood_dark", 0.8)         # дощатые полы и настилы


class Shift:
    """Обёртка над Asset: всё, что добавлено через неё, переносится сдвигом и поворотом вокруг Z.
    Общие сборки с координатами от центра участка (gable_roof, plank_door, window) так встают в любое
    место; вложенные Shift складываются. Цвет по нормали считается уже по мировой нормали."""

    def __init__(self, a, loc=(0.0, 0.0, 0.0), rz=0.0):
        m = TM(loc, (0, 0, rz))
        if isinstance(a, Shift):
            self.a, self.m = a.a, a.m @ m
        else:
            self.a, self.m = a, m

    def add(self, part, color, smooth=False):
        bmesh.ops.transform(part, matrix=self.m, verts=part.verts)
        part.normal_update()
        self.a.add(part, color, smooth)


# =========================================================================================
# Камень
# =========================================================================================
def plinth(a, cx, cy, w, d, h=0.30, col=RUBBLE, bevel=0.05):
    """Цоколь w x d: верх на h, низ уходит под землю на 14 см (как stone_base кита)."""
    a.add(p_box((w, d, h + 0.14), loc=(cx, cy, (h - 0.14) / 2), bevel=bevel), col)


def quoins(a, cx, cy, w, d, z0, z1, corners=((-1, -1), (1, -1)), col="stone_light", hb=0.30, out=0.035,
           long=0.38, short=0.24):
    """Угловые квадры коробки w x d (центр cx, cy) от z0 до z1: камни вперевязку — длинный по X, короткий
    по Y и наоборот, на out наружу из стены. corners — какие углы класть ((sx, sy) по знакам)."""
    n = max(2, int(round((z1 - z0) / hb)))
    hq = (z1 - z0) / n
    for sx, sy in corners:
        for i in range(n):
            lx, ly = (long, short) if (i % 2 == 0) else (short, long)
            a.add(p_box((lx, ly, hq - 0.035), loc=(cx + sx * (w / 2 - lx / 2 + out), cy + sy * (d / 2 - ly / 2 + out),
                                                  z0 + (i + 0.5) * hq), bevel=0.0), col)


def masonry(a, cx, cy, w, d, z0, z1, col=WALL, corners=((-1, -1), (1, -1)), qcol="stone_light", bevel=0.05):
    """Каменная коробка с угловыми квадрами."""
    a.add(p_box((w, d, z1 - z0), loc=(cx, cy, (z0 + z1) / 2), bevel=bevel), col)
    if corners:
        quoins(a, cx, cy, w, d, z0, z1, corners=corners, col=qcol)


def stone_arch(a, fr, w, h, depth=0.30, ring=0.16, col="stone_light", fill="black", key=True):
    """Арочный проём на лице стены: рама fr — низ проёма посередине, X вдоль стены, -Y наружу.
    Тёмная ниша (fill) на всю глубину, венец из клиньев и замковый камень."""
    r = w / 2
    zs = h - r
    if fill:
        fr.box(a, (w, depth, zs), (0, depth / 2 - 0.02, zs / 2), col=fill, bevel=0.0)
        fr.cyl(a, r, r, depth, 12, loc=(0, depth - 0.02, zs), rot=(90, 0, 0), col=fill)
    # венец: 7 клиньев по полуокружности и пяты
    n = 7
    for i in range(n):
        t0 = math.pi * i / n
        t1 = math.pi * (i + 1) / n
        pts = [(math.cos(t0) * r, zs + math.sin(t0) * r), (math.cos(t0) * (r + ring), zs + math.sin(t0) * (r + ring)),
               (math.cos(t1) * (r + ring), zs + math.sin(t1) * (r + ring)), (math.cos(t1) * r, zs + math.sin(t1) * r)]
        pts = list(reversed(pts))
        c = col if (i % 2 == 0) else "stone_mid"
        if key and i == n // 2:
            continue
        fr.prism(a, pts, 0.10, loc=(0, -0.04, 0), col=c)
    if key:
        fr.prism(a, [(-0.11, zs + r - 0.02), (0.11, zs + r - 0.02), (0.15, zs + r + ring + 0.06),
                     (-0.15, zs + r + ring + 0.06)], 0.14, loc=(0, -0.06, 0), col=col)
    for sx in (-1, 1):
        fr.box(a, (ring, 0.10, zs), (sx * (r + ring / 2), -0.04, zs / 2), col="stone_mid", bevel=0.0)
        fr.box(a, (ring + 0.06, 0.12, 0.10), (sx * (r + ring / 2), -0.05, zs), col=col, bevel=0.0)


def stack(a, x, y, z0, h, w=0.56, d=None, col=WALL, cap="stone_dark", flues=1, band=True):
    """Труба-стояк от z0 на h: тело, пояс-обрез под верхом, карниз и чёрные устья (flues — 1 или 2)."""
    d = d or w
    a.add(p_box((w, d, h), loc=(x, y, z0 + h / 2), bevel=0.04), col)
    if band:
        a.add(p_box((w + 0.08, d + 0.08, 0.10), loc=(x, y, z0 + h * 0.62), bevel=0.0), cap)
    a.add(p_box((w + 0.14, d + 0.14, 0.14), loc=(x, y, z0 + h - 0.05), bevel=0.03), cap)
    if flues == 1:
        a.add(p_box((w - 0.18, d - 0.18, 0.03), loc=(x, y, z0 + h + 0.03), bevel=0.0), "black")
    else:
        # два стояка-оголовка: читаются парой труб над коньком
        for s in (-1, 1):
            cx = x + s * w * 0.24
            a.add(p_box((w * 0.40, d * 0.70, 0.26), loc=(cx, y, z0 + h + 0.13), bevel=0.0), col)
            a.add(p_box((w * 0.46, d * 0.76, 0.06), loc=(cx, y, z0 + h + 0.28), bevel=0.0), cap)
            a.add(p_box((w * 0.24, d * 0.50, 0.03), loc=(cx, y, z0 + h + 0.31), bevel=0.0), "black")


# =========================================================================================
# Дерево
# =========================================================================================
def _log_col(axis, tone):
    """Бревно: торцы светлые, верх коры светлее (wood_light), бока — tone."""
    def col(f):
        n = f.normal
        if abs(n[axis]) > 0.7:
            return "wood_pale"
        return "wood_light" if n.z > 0.45 else tone
    return col


def log_box(a, cx, cy, w, d, z0, courses, r=0.12, ext=0.16, openings=None, sides="xy",
            tones=("wood_mid", "wood_mid2"), seg=6):
    """Сруб: стены из брёвен «в чашу» — концы выходят за угол на ext, венцы стен вдоль X и вдоль Y
    сдвинуты на полшага. Оси брёвен — по контуру w x d (центр cx, cy). openings — проёмы, в которых бревно
    разрывается: {"-y": [(u0, u1, z0, z1)], "+y": [...], "-x": [...], "+x": [...]}, u — координата вдоль
    стены (x для ±y, y для ±x). sides — какие стены класть ("xy" — все; "x" — только вдоль X).
    seg — граней у бревна: 6 (плоский верх и низ) дешевле 8 на треть, на игровой дистанции не отличить.
    Возвращает отметку верха сруба (верх последнего венца стен вдоль Y)."""
    openings = openings or {}
    spin_x, spin_y = (30.0, 0.0) if seg == 6 else (180.0 / seg, 180.0 / seg)
    step = 2 * r * 0.88
    top = z0
    for k in range(courses):
        tone = tones[k % len(tones)]
        if "x" in sides:
            z = z0 + r + k * step
            for sy, key in ((-1, "-y"), (1, "+y")):
                y = cy + sy * d / 2
                u0, u1 = cx - w / 2 - ext, cx + w / 2 + ext
                cuts = sorted(o for o in openings.get(key, ()) if o[2] - r * 0.5 < z < o[3] + r * 0.5)
                segs, u = [], u0
                for o0, o1, _, _ in cuts:
                    if o0 > u:
                        segs.append((u, o0))
                    u = max(u, o1)
                if u < u1:
                    segs.append((u, u1))
                for s0, s1 in segs:
                    if s1 - s0 > 0.06:
                        a.add(p_cyl(r, r, s1 - s0, seg, loc=(s0, y, z), rot=(0, 90, 0), spin=spin_x),
                              _log_col(0, tone))
            top = max(top, z + r)
        if "y" in sides:
            z = z0 + r + (k + 0.5) * step
            for sx, key in ((-1, "-x"), (1, "+x")):
                x = cx + sx * w / 2
                u0, u1 = cy - d / 2 - ext, cy + d / 2 + ext
                cuts = sorted(o for o in openings.get(key, ()) if o[2] - r * 0.5 < z < o[3] + r * 0.5)
                segs, u = [], u0
                for o0, o1, _, _ in cuts:
                    if o0 > u:
                        segs.append((u, o0))
                    u = max(u, o1)
                if u < u1:
                    segs.append((u, u1))
                for s0, s1 in segs:
                    if s1 - s0 > 0.06:
                        a.add(p_cyl(r, r, s1 - s0, seg, loc=(x, s0, z), rot=(-90, 0, 0), spin=spin_y),
                              _log_col(1, tones[(k + 1) % len(tones)]))
            top = max(top, z + r)
    return top


def fachwerk(a, fr, L, z0, z1, posts=(), braces=(), rails=(), t=0.12, col=BEAM, plate=True):
    """Брусья фахверка на лице стены: рама fr — X вдоль стены (середина в 0), -Y наружу, z — высота в раме.
    plate — нижняя и верхняя обвязка; posts — x стоек (крайние стойки на углах — ±L/2 — добавляются всегда);
    rails — высоты ригелей; braces — раскосы (x0, z0, x1, z1)."""
    # Лица брусьев разнесены по глубине на 1 см (обвязка впереди стоек, стойки впереди ригелей, раскосы
    # глубже всех): совпавшие плоскости в местах пересечения Cycles рисует чёрными квадратами.
    if plate:
        fr.box(a, (L + t, t, t), (0, -0.03, z0 + t / 2), col=col, bevel=0.0)
        fr.box(a, (L + t, t, t), (0, -0.03, z1 - t / 2), col=col, bevel=0.0)
    for x in (-L / 2, L / 2) + tuple(posts):
        fr.box(a, (t, t, z1 - z0), (x, -0.02, (z0 + z1) / 2), col=col, bevel=0.0)
    for z in rails:
        fr.box(a, (L, t * 0.9, t * 0.9), (0, -0.016, z), col=col, bevel=0.0)
    for x0, za, x1, zb in braces:
        ln = math.hypot(x1 - x0, zb - za)
        ang = math.degrees(math.atan2(x1 - x0, zb - za))
        fr.box(a, (t * 0.8, t * 0.8, ln), ((x0 + x1) / 2, -0.006, (za + zb) / 2), rot=(0, ang, 0), col=col,
               bevel=0.0)


def win(a, fr, w=0.44, h=0.52, lit=False, shutters="roof_dark", sill="stone_light", frame=BEAM, cross=True,
        open_shutters=True):
    """Окно в раме fr (центр проёма в начале рамы, лицом к -Y рамы — рама стоит на лице стены).
    lit — стекло светится (lantern_glow уходит в слот Vitaria_FX): жилое и рабочее окно уровня 3."""
    fr.box(a, (w + 0.14, 0.10, h + 0.14), (0, 0, 0), col=frame, bevel=0.0)
    fr.box(a, (w, 0.08, h), (0, -0.03, 0), col="lantern_glow" if lit else "glass", bevel=0.0)
    if cross:
        fr.box(a, (0.05, 0.05, h), (0, -0.075, 0), col=frame, bevel=0.0)
        fr.box(a, (w, 0.05, 0.05), (0, -0.075, h * 0.08), col=frame, bevel=0.0)
    if shutters:
        sw = w * 0.5 + 0.03
        for s in (-1, 1):
            xs = s * (w / 2 + 0.07 + sw / 2) if open_shutters else s * w / 4
            fr.box(a, (sw, 0.06, h + 0.06), (xs, -0.05, 0), col=shutters, bevel=0.0)
            fr.box(a, (sw - 0.06, 0.03, 0.05), (xs, -0.09, h * 0.22), col=frame, bevel=0.0)
            fr.box(a, (sw - 0.06, 0.03, 0.05), (xs, -0.09, -h * 0.22), col=frame, bevel=0.0)
    if sill:
        fr.box(a, (w + 0.26, 0.17, 0.08), (0, -0.06, -h / 2 - 0.10), col=sill, bevel=0.0)


def shed_roof(a, cx, cy, w, d, z_hi, z_lo, face=-1, ox=0.20, oy=0.22, top="roof", bot="roof_dark",
              trim="wood_dark", thick=0.16):
    """Односкатная кровля над прямоугольником w (по X) x d (по Y): верхний край у стороны -face (z_hi),
    нижний у стороны face (z_lo); face = -1 — скат к фасаду. Свес ox по бокам, oy за нижний край."""
    run = d
    ang = math.atan2(z_hi - z_lo, run)
    L = run / math.cos(ang) + oy
    # ось ската: от верхнего края (над стеной -face) до нижнего с выносом oy
    yh = cy - face * d / 2
    zc_line = z_hi - (L / 2) * math.sin(ang)
    yc_line = yh + face * (L / 2) * math.cos(ang)
    rot = (-face * math.degrees(ang), 0, 0)
    fr = Frame((cx, yc_line, zc_line), rot=rot)
    fr.box(a, (w + 2 * ox, L, 0.09), (0, 0, -0.03), col=trim, bevel=0.0)
    half = L / 2
    fr.box(a, (w + 2 * ox, half + 0.04, thick), (0, -face * (half / 2), 0.07), col=top, bevel=0.03)
    fr.box(a, (w + 2 * ox, half + 0.04, thick), (0, face * (half / 2), 0.07), col=bot, bevel=0.03)
    fr.box(a, (w + 2 * ox + 0.06, 0.12, 0.22), (0, face * (L / 2 - 0.04), 0.02), col=trim, bevel=0.0)
    for s in (-1, 1):
        fr.box(a, (0.10, L + 0.04, 0.22), (s * (w / 2 + ox - 0.02), 0, 0.03), col=trim, bevel=0.0)


def gable_x_at(a, cx, cy, w, d, ztop, **kw):
    """gable_roof_x (конёк вдоль X) с центром в (cx, cy). Возвращает отметку конька."""
    return gable_roof_x(Shift(a, (cx, cy, 0)), w, d, ztop, **kw)


def gable_y_at(a, cx, cy, w, d, ztop, **kw):
    """gable_roof (конёк вдоль Y) с центром в (cx, cy). Возвращает отметку конька."""
    return gable_roof(Shift(a, (cx, cy, 0)), w, d, ztop, **kw)


def gable_end(a, x, cy, d, ztop, zr, col="wood_light", depth=0.12, vent=True, face=1):
    """Дощатый фронтон торца по X (сруб, конёк вдоль X): треугольник над стеной x, нормаль ±X (face)."""
    a.add(p_prism([(-d / 2, ztop), (d / 2, ztop), (0, zr - 0.06)], depth, loc=(x, cy, 0), rot=(0, 0, 90)), col)
    for k in range(1, 4):
        zz = ztop + (zr - ztop) * k / 4.2
        hw = d / 2 * (1 - (zz - ztop) / (zr - ztop))
        a.add(p_box((0.03, 2 * hw - 0.05, 0.03), loc=(x + face * (depth / 2 + 0.005), cy, zz), bevel=0.0), "wood_mid")
    if vent:
        a.add(p_box((0.04, 0.20, 0.20), loc=(x + face * (depth / 2 + 0.01), cy, ztop + (zr - ztop) * 0.45),
                    rot=(45, 0, 0), bevel=0.0), "black")


def gable_front(a, cx, y, w, ztop, zr, col="wood_light", depth=0.12, face=-1, boards=True):
    """Фронтон торца по Y (конёк вдоль Y), лицом к face: треугольник и горизонтальные доски-нащельники."""
    a.add(p_prism([(-w / 2, ztop), (w / 2, ztop), (0, zr - 0.06)], depth, loc=(cx, y, 0)), col)
    if boards:
        for k in range(1, 4):
            zz = ztop + (zr - ztop) * k / 4.2
            hw = w / 2 * (1 - (zz - ztop) / (zr - ztop))
            a.add(p_box((2 * hw - 0.05, 0.03, 0.03), loc=(cx, y + face * (depth / 2 + 0.005), zz), bevel=0.0),
                  "wood_mid")


def stovepipe(a, x, y, z0, h, r=0.09):
    """Железная печная труба с колпаком: дымоход избушки до каменной трубы."""
    a.add(p_cyl(r, r, h, 8, loc=(x, y, z0)), "iron_dark")
    a.add(p_cyl(r + 0.03, r + 0.03, 0.06, 8, loc=(x, y, z0 + h * 0.55)), "iron")
    a.add(p_cyl(r * 2.2, 0.0, 0.16, 8, loc=(x, y, z0 + h + 0.06)), "iron_dark")
    for s in (-1, 1):
        a.add(p_box((0.03, 0.03, 0.10), loc=(x + s * r * 1.2, y, z0 + h + 0.03), bevel=0.0), "iron_dark")


def porch_post(a, x, y, z0, h, s=0.16, col="wood_mid", base="stone_mid"):
    """Столб навеса на каменной подушке."""
    a.add(p_box((s + 0.12, s + 0.12, 0.12), loc=(x, y, z0 + 0.06), bevel=0.0), base)
    a.add(p_box((s, s, h - 0.12), loc=(x, y, z0 + 0.12 + (h - 0.12) / 2), bevel=0.03), col)


def wall_lamp(a, x, y, z, out=(0.0, -1.0)):
    """Фонарь на кронштейне у двери (свет — Vitaria_FX)."""
    ox, oy = out
    rz = math.degrees(math.atan2(oy, ox))
    fr = Frame((x, y, z), rz=rz)
    fr.box(a, (0.30, 0.05, 0.05), (0.15, 0, 0), col="iron_dark", bevel=0.0)
    lantern(a, Frame((x + ox * 0.27, y + oy * 0.27, z - 0.42)))


def log_ends(a, cx, y0, L, rows=(4, 3, 2), r=0.15, seg=6, chocks=True, seed=4):
    """Штабель брёвен торцами к камере: брёвна вдоль Y от y0 (передний торец) на L вглубь, ряды ложатся
    в ложбины нижнего. На переднем торце — кольцо светлого среза. Дешёвый: 6 граней, без фасок."""
    rng = random.Random(seed)
    step = 2 * r * 1.03
    if chocks:
        for y in (y0 + L * 0.2, y0 + L * 0.8):
            a.add(p_box((rows[0] * step + 0.20, 0.14, 0.10), loc=(cx, y, 0.05), bevel=0.0), "wood_dark")
    zs = 0.10 if chocks else 0.0
    for row, n in enumerate(rows):
        z = zs + r + row * step * 0.866
        for k in range(n):
            x = cx + (k - (n - 1) / 2) * step
            ll = L * rng.uniform(0.94, 1.0)
            rr = r * rng.uniform(0.94, 1.04)
            a.add(p_cyl(rr, rr, ll, seg, loc=(x, y0, z), rot=(-90, 0, 0), spin=0.0),
                  lambda f: "wood_pale" if abs(f.normal.y) > 0.7 else ("bark" if f.normal.z > -0.3 else "bark_dark"))
            a.add(p_cyl(rr * 0.55, rr * 0.55, 0.012, seg, loc=(x, y0 - 0.004, z), rot=(90, 0, 0), spin=0.0),
                  "wood_light")
    if chocks:
        half = (rows[0] - 1) / 2 * step + r + 0.05
        hs = zs + 2 * r + step * 0.866 * 1.1
        for sx in (-1, 1):
            a.add(p_box((0.10, 0.10, hs), loc=(cx + sx * half, y0 + 0.10, hs / 2 - 0.03), bevel=0.0), "wood_dark")


def arched_wall(a, x0, x1, y, depth, z0, z1, ax0, ax1, zs, col=WALL, ring="stone_light", ring_w=0.16,
                key=True, inner="black"):
    """Стена вдоль X (центр толщины в y) от x0 до x1 и от z0 до z1 с настоящим арочным проёмом ax0..ax1:
    простенки по бокам, над пятой (zs) — две пазухи с вырезом по дуге, впереди — венец из клиньев и замок.
    Сквозь проём видно то, что внутри; inner — тёмная обводка-откос проёма (None — без неё)."""
    r = (ax1 - ax0) / 2
    xc = (ax0 + ax1) / 2
    if ax0 - x0 > 0.01:
        a.add(p_box((ax0 - x0, depth, z1 - z0), loc=((x0 + ax0) / 2, y, (z0 + z1) / 2), bevel=0.04), col)
    if x1 - ax1 > 0.01:
        a.add(p_box((x1 - ax1, depth, z1 - z0), loc=((ax1 + x1) / 2, y, (z0 + z1) / 2), bevel=0.04), col)
    n = 8
    for s in (-1, 1):
        # пазуха: от пяты по дуге к замку и по прямой к верху стены
        arc = [(xc + s * r * math.cos(math.pi / 2 * k / n), zs + r * math.sin(math.pi / 2 * k / n)) for k in range(n + 1)]
        pts = [(xc + s * r, zs), (xc + s * r, z1), (xc, z1)] + list(reversed(arc))[:-1]
        if s < 0:
            pts = list(reversed(pts))
        a.add(p_prism(pts, depth, loc=(0, y, 0)), col)
    if inner:
        # откосы: тёмная лента по контуру проёма на глубине стены — проём читается глубоким
        for s in (-1, 1):
            a.add(p_box((0.04, depth - 0.04, zs - z0), loc=(xc + s * (r - 0.02), y, (z0 + zs) / 2), bevel=0.0), inner)
    stone_arch(a, Frame((xc, y - depth / 2, z0)), ax1 - ax0, zs - z0 + r, depth=depth, ring=ring_w, col=ring,
               fill=None, key=key)


def ridge_louver(a, x, y, zr, w=0.56, d=0.86, h=0.40, pitch=34.0):
    """Дымовой фонарь на коньке (конёк вдоль Y): короб с тёмными щелями под жалюзи и своя крышечка.
    Стоит на седле выше конька: ниже его съедает коньковый брус кровли (верх на zr + 0.36)."""
    a.add(p_box((w + 0.06, d + 0.06, 0.52), loc=(x, y, zr + 0.12), bevel=0.0), "wood_dark")
    zb = zr + 0.36
    a.add(p_box((w, d, h), loc=(x, y, zb + h / 2), bevel=0.0), "wood_light")
    for s in (-1, 1):
        a.add(p_box((0.03, d - 0.20, h * 0.56), loc=(x + s * (w / 2 + 0.005), y, zb + h * 0.56), bevel=0.0), "black")
        for k in range(3):
            a.add(p_box((0.05, d - 0.16, 0.05), loc=(x + s * (w / 2 + 0.03), y, zb + h * 0.36 + k * 0.10),
                        rot=(0, s * 25, 0), bevel=0.0), "wood_dark")
    a.add(p_box((w - 0.16, 0.03, h * 0.56), loc=(x, y - d / 2 - 0.005, zb + h * 0.56), bevel=0.0), "black")
    gable_y_at(a, x, y, w, d, zb + h, pitch_deg=pitch, ox=0.10, oy=0.10, bands=1)


def dormer(a, x, y_face, z_face, w=0.64, h=0.62, depth=0.90, pitch=40.0, lit=True, wall="wood_light",
           roof_pitch=40.0):
    """Слуховое окно на скате, который опускается к -Y (конёк кровли вдоль X): короб с окном лицом к -Y и
    своя двускатная крышечка цвета кровли (конёк вдоль Y). (x, y_face, z_face) — точка ската под фасадом окна:
    короб уходит в скат на depth, его низ не висит над кровлей."""
    t = math.tan(math.radians(pitch))
    zb = z_face - 0.06
    hb = h + depth * t                     # задняя часть короба утоплена в скат
    a.add(p_box((w, depth, hb), loc=(x, y_face + depth / 2, zb + h - hb / 2), bevel=0.0), wall)
    win(a, Frame((x, y_face - 0.02, zb + h * 0.52)), w=w - 0.24, h=h - 0.24, lit=lit, shutters=None, sill=None)
    gable_y_at(a, x, y_face + depth / 2 - 0.04, w, depth + 0.10, zb + h, pitch_deg=roof_pitch, ox=0.08, oy=0.06,
               bands=1)
    gable_front(a, x, y_face + 0.05, w, zb + h - 0.02, zb + h + 0.08 + (w / 2) * math.tan(math.radians(roof_pitch)),
                col=wall, depth=0.10, face=-1, boards=False)


def cupola(a, x, y, z0, s=0.62, h=0.52, roof_h=0.62):
    """Вентиляционная башенка на коньке: седло на коньке, квадратный короб с жалюзи на гранях, шатёр цвета
    кровли, железный шпиль с шаром. z0 — отметка конька (zr): короб стоит на седле выше конькового бруса."""
    a.add(p_box((s + 0.08, s + 0.08, 0.60), loc=(x, y, z0 + 0.10), bevel=0.0), "wood_dark")
    z0 = z0 + 0.38
    a.add(p_box((s, s, h), loc=(x, y, z0 + h / 2), bevel=0.0), "wood_light")
    for k in range(4):
        rz = 90 * k
        fr = Frame((x, y, z0), rz=rz)
        fr.box(a, (s - 0.18, 0.03, h - 0.20), (0, -s / 2 - 0.005, h / 2 + 0.02), col="black", bevel=0.0)
        for j in range(3):
            fr.box(a, (s - 0.16, 0.05, 0.05), (0, -s / 2 - 0.03, 0.16 + j * 0.11), rot=(-30, 0, 0), col="wood_dark",
                   bevel=0.0)
    a.add(p_box((s + 0.10, s + 0.10, 0.08), loc=(x, y, z0 + h), bevel=0.0), "wood_dark")
    r = (s / 2 + 0.12) * 1.4142
    a.add(p_cyl(r, 0.0, roof_h, 4, loc=(x, y, z0 + h + 0.04), spin=45), by_normal("roof", "roof", "roof_dark", 0.2))
    zt = z0 + h + 0.04 + roof_h
    a.add(p_cyl(0.03, 0.0, 0.30, 6, loc=(x, y, zt - 0.06)), "iron_dark")
    a.add(p_ico(0.05, 1, loc=(x, y, zt + 0.02)), "iron_dark")


def stepped_gable(a, cx, y, w, zt, zr, depth=0.30, steps=4, rise=0.30, col=WALL, cap="stone_light"):
    """Ступенчатый каменный щипец над стеной (лицом по Y): уступы стоят выше скатов кровли на rise,
    на каждом уступе — плита-слив; венчает щипец столбик с шаром. Скат кровли должен кончаться за щипцом
    (свес по торцу отрицательный), иначе он прорежет уступы."""
    H = zr - zt + rise
    n = steps
    for k in range(n + 1):
        hw = w / 2 * (1 - k / (n + 0.6))
        z0 = zt + H * k / (n + 1)
        z1 = zt + H * (k + 1) / (n + 1)
        a.add(p_box((2 * hw, depth, z1 - z0 + 0.02), loc=(cx, y, (z0 + z1) / 2), bevel=0.0), col)
        a.add(p_box((2 * hw + 0.06, depth + 0.08, 0.07), loc=(cx, y, z1 + 0.025), bevel=0.0), cap)
    ztop = zt + H + 0.06
    a.add(p_box((0.20, 0.20, 0.26), loc=(cx, y, ztop + 0.13), bevel=0.0), cap)
    a.add(p_ico(0.12, 1, loc=(cx, y, ztop + 0.36)), cap)
    return ztop


def tower_square(a, x, y, s, z0, h, roof_h=1.0, crenel=True, col=WALL, slits=2, face=-1):
    """Квадратная каменная башня: ствол s x s от z0 на h, пояс-машикули, зубцы и шатёр цвета кровли
    над ними; бойницы на грани к камере (face — направление нормали по Y)."""
    a.add(p_box((s, s, h), loc=(x, y, z0 + h / 2), bevel=0.04), col)
    zt = z0 + h
    a.add(p_box((s + 0.16, s + 0.16, 0.18), loc=(x, y, zt + 0.02), bevel=0.0), "stone_light")
    for k in range(4):
        fr = Frame((x, y, zt + 0.02), rz=90 * k)
        fr.box(a, (s + 0.10, 0.10, 0.14), (0, -(s / 2 + 0.03), -0.16), col="stone_mid", bevel=0.0)
    if crenel:
        m = 3
        for k in range(4):
            fr = Frame((x, y, zt + 0.11), rz=90 * k)
            for i in range(m):
                u = (i - (m - 1) / 2) * (s + 0.16) / m
                fr.box(a, (0.18, 0.16, 0.24), (u, -(s / 2 + 0.0), 0.12), col="stone_light", bevel=0.0)
    r = (s / 2 + 0.06) * 1.4142
    a.add(p_cyl(r, 0.0, roof_h, 4, loc=(x, y, zt + 0.12), spin=45), by_normal("roof", "roof", "roof_dark", 0.2))
    for i in range(slits):
        zz = z0 + h * (0.30 + 0.40 * i / max(1, slits - 1)) if slits > 1 else z0 + h * 0.5
        a.add(p_box((0.20, 0.06, 0.46), loc=(x, y + face * (s / 2 + 0.01), zz), bevel=0.0), "stone_light")
        a.add(p_box((0.08, 0.06, 0.36), loc=(x, y + face * (s / 2 + 0.03), zz), bevel=0.0), "black")
    return zt + 0.12 + roof_h


def arcade(a, x0, x1, y, depth, z0, z1, n, zs, col=WALL, ring="stone_light", pier=0.24):
    """Аркада вдоль X: n арочных проёмов поровну между x0 и x1, простенки шириной pier, пазухи над пятой zs
    и венцы с замками — как arched_wall, но в ряд (соседние участки стены сходятся посередине простенков).
    Возвращает центры проёмов."""
    span = (x1 - x0 - (n + 1) * pier) / n
    centers = []
    for i in range(n):
        ax0 = x0 + pier + i * (span + pier)
        ax1 = ax0 + span
        wl = x0 if i == 0 else ax0 - pier / 2
        wr = x1 if i == n - 1 else ax1 + pier / 2
        arched_wall(a, wl, wr, y, depth, z0, z1, ax0, ax1, zs, col=col, ring=ring, inner=None)
        centers.append((ax0 + ax1) / 2)
    return centers


def cross_gable(a, cx, y_front, w, z_eave, z_wall, zr_main, yc_main, pitch_main, pitch=42.0, col=PLASTER,
                oy=0.24, ox=0.10, depth=0.20, beams=True):
    """Фронтон-ризалит на скате с коньком вдоль X: стена фасада поднимается от карниза z_eave до z_wall,
    над ней свой фронтон и своя двускатная кровля коньком вдоль Y, которая уходит в основной скат там, где
    её конёк встречает его (yc_main, zr_main, pitch_main — конёк и уклон основной кровли). Возвращает zr2."""
    tp = math.tan(math.radians(pitch_main))
    zr2 = z_wall + 0.08 + (w / 2) * math.tan(math.radians(pitch))
    yb = yc_main - (zr_main - zr2 + 0.10) / tp
    a.add(p_box((w, depth, z_wall - z_eave + 0.06), loc=(cx, y_front + depth / 2, (z_eave + z_wall) / 2), bevel=0.0), col)
    gable_y_at(a, cx, (y_front + yb) / 2, w, yb - y_front, z_wall, pitch_deg=pitch, ox=ox, oy=oy, bands=2)
    gable_front(a, cx, y_front + depth / 2, w, z_wall - 0.02, zr2, col=col, depth=depth, face=-1, boards=False)
    if beams and col == PLASTER:
        a.add(p_box((w - 0.20, 0.12, 0.12), loc=(cx, y_front - 0.012, z_wall + 0.14), bevel=0.0), BEAM)
        for sx in (-1, 1):
            a.add(p_box((0.12, 0.12, z_wall - z_eave + 0.10), loc=(cx + sx * (w / 2 - 0.08), y_front, (z_eave + z_wall) / 2),
                        bevel=0.0), BEAM)
    return zr2
