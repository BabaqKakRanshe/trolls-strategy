"""
Плавильня: низкая дварфийская печь двумя уступами, широкая окованная труба и огненный зев
в тяжёлом портале. Всё, что делает здание плавильней с игровой дистанции, собрано у фасада:
зев с углями, лётка с жёлобом расплава и изложница, рядом стопки слитков.
От круглого «улья» Bld_Smelter уходит формой: прямоугольный приземистый массив с контрфорсами,
труба квадратная и втрое толще.
"""
import math
import random

from build_vitaria import p_box, p_cyl, p_ico, p_prism, p_taper_box, by_normal, ingot, ore_chunk
from vitaria_buildings.common import Frame, seg_frame, STONE_TOP, PLANK_TOP
from vitaria_buildings.levels import (Shift, quoins, stack, win, arched_wall, gable_front, gable_y_at, porch_post,
                                    wall_lamp, WALL as AWALL, BEAM, PLANKS)

NAME = "Bld_Smeltery"
TITLE = "Плавильня"
TARGET = (5.0, 4.5, 3.4)   # след 3x3 при масштабе 0.5: печь та же, двор шире

F = 0.16                                   # верх подиума
W1, D1, Y1, Z1 = 2.40, 1.50, 0.38, 1.16    # нижний ярус у земли: ширина, глубина, центр по Y; верх фриза
W1T, D1T = 2.24, 1.38                      # нижний ярус поверху: стены с завалом внутрь
W2, D2, Y2, Z2 = 1.90, 1.10, 0.42, 1.50    # верхний уступ-«колпак» у основания
W2T, D2T = 1.56, 0.96                      # и поверху
YF = Y1 - D1 / 2                           # фасад нижнего яруса у земли
YB = Y1 + D1 / 2                           # тыл нижнего яруса у земли
YC, ZC = 0.78, 3.02                        # труба: центр по Y и верх ствола
CB, CT = 1.06, 0.92                        # сечение трубы внизу и вверху (>= 0.9 по заданию)
Z0C = F - 0.02                             # низ ствола трубы

WALL = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
PORTAL = by_normal("stone_light", "stone_light", "stone_mid", 0.8)
FRIEZE = by_normal("stone_mid", "stone_dark", "stone_dark", 0.8)


def _rhomb(a, x, y, z, side, w=0.20, h=0.15, col="gold"):
    """Золотой ромб-инкрустация. side: 'f' фасад (-Y), 'b' тыл (+Y), 'l'/'r' — бок -X/+X.
    Задняя грань ромба утоплена в кладку на 6 мм: без общей плоскости со стеной."""
    pts = [(0.0, -h / 2), (w / 2, 0.0), (0.0, h / 2), (-w / 2, 0.0)]
    t = 0.024
    off = t / 2 - 0.006
    if side == "f":
        a.add(p_prism(pts, t, loc=(x, y - off, z)), col)
    elif side == "b":
        a.add(p_prism(pts, t, loc=(x, y + off, z)), col)
    else:
        sx = -1 if side == "l" else 1
        a.add(p_prism(pts, t, loc=(x + sx * off, y, z), rot=(0, 0, 90)), col)


def _chimney_side(z, top=ZC):
    """Сечение сужающегося ствола трубы на высоте z — под него подгоняются обручи."""
    return CB - (CB - CT) * (z - Z0C) / (top - Z0C)


def _ingot_stack(a, metal, x, y, rz=0.0, layers=(3, 2, 1), s=1.3, z0=F - 0.005):
    """Пирамида слитков: каждый ряд ложится в ложбины нижнего и «прикусывает» его на 5 мм —
    зазор в воздухе на рендере читается как парящий слиток."""
    c, sn = math.cos(math.radians(rz)), math.sin(math.radians(rz))
    pitch, dz = 0.145 * s, 0.07 * s
    for k, n in enumerate(layers):
        for i in range(n):
            u = (i - (n - 1) / 2) * pitch
            ingot(a, metal, loc=(x + c * u, y + sn * u, z0 + k * dz), rz=90 + rz, size=s)


def _bellows(a, fr, floor_z, rz, L=0.66, w=0.50, h=0.34):
    """Кожаные мехи «грушей»: носик в начале рамы смотрит в -X, тело и рукояти уходят в +X,
    ось мехов — z=0 рамы. Рама может быть наклонена: хвост вверх — так сверху виден контур доски.
    Доски раскрыты симметрично, поэтому кожаный клин собран одним p_taper_box."""
    lx0 = 0.12
    ln = L - 0.08 - lx0
    h0, w0 = 0.07, 0.16                   # сечение кожи у носика
    fr.taper(a, (h0, w0), (h, w * 0.84), ln, loc=(lx0, 0, 0), rot=(0, 90, 0), col="leather", bevel=0.015)
    # складки: тёмные рёбра выступают на 1.5 см — без них клин читается как мешок
    for t in (0.28, 0.54, 0.80):
        hh = h0 + (h - h0) * t + 0.03
        ww = w0 + (w * 0.84 - w0) * t + 0.03
        fr.box(a, (0.05, ww, hh), (lx0 + ln * t, 0, 0), col="leather_dark", bevel=0.012)
    # доски-«груши» по граням клина; сверху видно именно их контур
    th = math.atan2((h - h0) / 2, ln)
    blen = ln / math.cos(th) + 0.10
    outline = [(0.0, -0.07), (0.28 * blen, -0.13), (0.62 * blen, -w / 2), (0.9 * blen, -w * 0.45),
               (blen, -w * 0.28), (blen, w * 0.28), (0.9 * blen, w * 0.45), (0.62 * blen, w / 2),
               (0.28 * blen, 0.13), (0.0, 0.07)]
    for sz in (1, -1):
        bd = fr.sub((lx0 - 0.04, 0, sz * h0 / 2), rot=(0, -sz * math.degrees(th), 0))
        bd.prism(a, outline, 0.05, loc=(0, 0, sz * 0.025), rot=(90, 0, 0),
                 col="wood_light" if sz > 0 else "wood_mid")
        bd.box(a, (0.16, 0.08, 0.05), (blen + 0.05, 0, sz * 0.025), col="wood_dark", bevel=0.015)
    # носик и медное кольцо у кожи
    fr.cyl(a, 0.045, 0.028, lx0 + 0.02, 8, loc=(lx0 + 0.02, 0, 0), rot=(0, -90, 0), col="iron_dark")
    fr.cyl(a, 0.06, 0.06, 0.05, 8, loc=(lx0 - 0.01, 0, 0), rot=(0, 90, 0), col="copper")
    # козлы под хвостом считаются в мировых осях: рама наклонена, а ножки стоят отвесно
    x = lx0 + ln * 0.74
    zu = -h0 / 2 - (x - lx0 + 0.04) * math.tan(th) - 0.05 + 0.006     # низ нижней доски
    feet = [fr.at((x, sy * w * 0.26, zu)) for sy in (-1, 1)]
    for P in feet:
        hz = P.z - floor_z + 0.01
        a.add(p_box((0.08, 0.08, hz), loc=(P.x, P.y, floor_z - 0.01 + hz / 2), rot=(0, 0, rz), bevel=0.02),
              "wood_dark")
    mid = (feet[0] + feet[1]) / 2
    a.add(p_box((0.06, w * 0.52 + 0.10, 0.07), loc=(mid.x, mid.y, floor_z + 0.14), rot=(0, 0, rz), bevel=0.015),
          "wood_dark")


def _podium(a):
    # ---- подиум ------------------------------------------------------------------------
    # одна плита на весь двор: реквизит стоит на одном уровне, и ничего не свисает с уступа.
    # Двор шире печи (было 3.6 x 2.85): при общем масштабе 0.5 печь занимала бы треть следа 3x3,
    # поэтому вокруг неё мощёный двор с рудой, углём и слитками
    a.add(p_box((4.90, 4.30, 0.30), loc=(0, 0.02, 0.01), bevel=0.06),
          by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    # тёмные швы-полосы мощения: без них большая плита читалась пустым столом
    for x in (-1.62, 1.62):
        a.add(p_box((0.06, 4.1, 0.02), loc=(x, 0.02, F + 0.005), bevel=0.0), "stone_dark")
    a.add(p_box((4.7, 0.06, 0.02), loc=(0, -1.52, F + 0.005), bevel=0.0), "stone_dark")


def _furnace_low(a):
    # ---- нижний ярус: цоколь, кладка с завалом, фриз ---------------------------------------
    # Угловые контрфорсы с золотыми навершиями были на первом рендере: четыре светлых
    # обелиска превращали печь в храм. Массивность теперь держит завал стен внутрь.
    a.add(p_box((W1 + 0.10, D1 + 0.10, 0.26), loc=(0, Y1, F + 0.11), bevel=0.04), FRIEZE)
    a.add(p_taper_box((W1, D1), (W1T, D1T), Z1 - 0.04 - Z0C, loc=(0, Y1, Z0C), bevel=0.06), WALL)
    # фриз — тёмный пояс с золотыми ромбами: дварфийская примета и терраса над нижним ярусом
    fw, fd = W1T + 0.10, D1T + 0.10
    a.add(p_box((fw, fd, 0.20), loc=(0, Y1, Z1 - 0.10), bevel=0.04), FRIEZE)
    zf = Z1 - 0.10
    for sx, side in ((-1, "l"), (1, "r")):
        for y in (0.02, 0.38, 0.74):
            _rhomb(a, sx * fw / 2, y, zf, side)
        _rhomb(a, sx * 1.06, Y1 - fd / 2, zf, "f", w=0.16, h=0.13)
    for x in (-0.84, 0.84):
        _rhomb(a, x, Y1 + fd / 2, zf, "b")


def _furnace_ledge(a):
    # ---- верхний уступ: низкий колпак, из которого выходит труба ------------------------------
    # колпак тёмный: на втором рендере колпак, навершие портала и труба сливались в один серый ком
    a.add(p_taper_box((W2, D2), (W2T, D2T), Z2 - Z1 + 0.04, loc=(0, Y2, Z1 - 0.04), bevel=0.05), FRIEZE)
    a.add(p_box((W2T + 0.10, D2T + 0.10, 0.10), loc=(0, Y2, Z2 + 0.01), bevel=0.035), FRIEZE)


def _chimney(a, rng, top=ZC):
    # ---- труба: стоит на земле за тылом, поэтому сзади читается башней, а не наростом ----
    a.add(p_taper_box((CB, CB), (CT, CT), top - Z0C, loc=(0, YC, Z0C), bevel=0.05), WALL)
    for zh in (1.92, 2.52):
        s = _chimney_side(zh, top) + 0.06
        a.add(p_box((s, s, 0.13), loc=(0, YC, zh), bevel=0.025), "iron_dark")
        # золотые заклёпки по обручу — дварфийская оковка, заодно блик на тёмной полосе
        for u in (-0.26, 0.26):
            for sy in (-1, 1):
                a.add(p_box((0.07, 0.03, 0.07), loc=(u, YC + sy * (s / 2 + 0.005), zh), bevel=0.0), "gold")
                a.add(p_box((0.03, 0.07, 0.07), loc=(sy * (s / 2 + 0.005), YC + u, zh), bevel=0.0), "gold")
    # венец ступенью наружу и кольцо устья. Жар в устье — пара утопленных углей: плоский
    # светящийся квадрат читался лампой, а не тягой печи
    a.add(p_box((CT + 0.14, CT + 0.14, 0.12), loc=(0, YC, top - 0.02), bevel=0.035), "stone_light")
    ro, rt, zr = 1.18, 0.20, top + 0.14
    for sy in (-1, 1):
        a.add(p_box((ro, rt, 0.20), loc=(0, YC + sy * (ro - rt) / 2, zr), bevel=0.035), FRIEZE)
        a.add(p_box((rt, ro - 2 * rt + 0.02, 0.20), loc=(sy * (ro - rt) / 2, YC, zr), bevel=0.035), FRIEZE)
    a.add(p_box((ro - 2 * rt + 0.02, ro - 2 * rt + 0.02, 0.04), loc=(0, YC, zr - 0.06), bevel=0.0), "black")
    for dx, dy, r, col in ((-0.10, 0.04, 0.12, "ember"), (0.11, -0.03, 0.11, "glow"), (0.0, 0.14, 0.10, "ember")):
        a.add(p_ico(r, 1, loc=(dx, YC + dy, zr - 0.05), scl=(1.0, 1.0, 0.45), jitter=0.2, rng=rng), col)
    # чистка у основания трубы с тыла
    yb = YC + CB / 2
    a.add(p_box((0.58, 0.12, 0.52), loc=(0, yb + 0.03, F + 0.26), bevel=0.035), "stone_light")
    a.add(p_box((0.38, 0.06, 0.34), loc=(0, yb + 0.10, F + 0.25), bevel=0.02), "iron_dark")


def _portal(a, rng):
    # ---- портал топки -----------------------------------------------------------------------
    # под — каменный порог, на нём угли; низ зева на его верхней грани
    a.add(p_box((1.24, 0.56, 0.26), loc=(0, YF - 0.24, F + 0.09), bevel=0.04), WALL)
    zo0, zo1 = F + 0.22, 0.98             # низ и верх проёма
    for sx in (-1, 1):
        # косяк глубже сзади: стена с завалом уходит от него, и без запаса у верха виден зазор
        a.add(p_taper_box((0.34, 0.48), (0.28, 0.45), 1.00 - Z0C, loc=(sx * 0.69, YF - 0.14, Z0C),
                          bevel=0.04), PORTAL)
        # ступенчатые «плечи» в верхних углах проёма — дварфийская ступенчатая арка
        a.add(p_box((0.20, 0.36, 0.14), loc=(sx * 0.48, YF - 0.16, zo1 - 0.06), bevel=0.03), PORTAL)
    a.add(p_box((1.90, 0.44, 0.28), loc=(0, YF - 0.18, 1.10), bevel=0.05), PORTAL)
    # навершие портала уходит назад в верхний уступ: портал и печь — одна масса
    yc0, yc1 = YF - 0.28, 0.05
    a.add(p_box((1.00, yc1 - yc0, 0.26), loc=(0, (yc0 + yc1) / 2, 1.27), bevel=0.04), PORTAL)
    a.add(p_prism([(-0.22, -0.07), (-0.12, -0.07), (0.0, 0.03), (0.12, -0.07), (0.22, -0.07), (0.0, 0.10)],
                  0.024, loc=(0, yc0 - 0.006, 1.28)), "gold")
    for x in (-0.62, -0.31, 0.31, 0.62):
        _rhomb(a, x, YF - 0.40, 1.10, "f", w=0.18, h=0.14)

    # зев: чёрная глубина, раскалённая задняя стенка, горка углей
    a.add(p_box((1.16, 0.06, zo1 + 0.02 - (zo0 - 0.04)), loc=(0, YF - 0.04, (zo0 - 0.04 + zo1 + 0.02) / 2),
                bevel=0.0), "black")
    a.add(p_box((0.86, 0.04, 0.26), loc=(0, YF - 0.10, zo0 + 0.11), bevel=0.0), "glow")
    coals = [(-0.34, -0.20, 0.12, "ember"), (-0.12, -0.25, 0.14, "glow"), (0.12, -0.22, 0.13, "ember"),
             (0.34, -0.19, 0.11, "glow"), (0.00, -0.14, 0.12, "glow_hot"), (-0.22, -0.33, 0.09, "coal"),
             (0.24, -0.33, 0.09, "ember")]
    for x, dy, r, col in coals:
        a.add(p_ico(r, 1, loc=(x, YF + dy, zo0 + r * 0.35), scl=(1.1, 0.9, 0.75), jitter=0.15, rng=rng,
                    rot=(0, 0, rng.uniform(0, 360))), col)


def _melt(a):
    # ---- лётка, жёлоб расплава и изложница ----------------------------------------------------
    # Расплав — glow, а не glow_hot: крупная светлая грань под эмиссией выгорала в белый,
    # и жёлоб читался бледной рейкой.
    ys = YF - 0.52                        # передняя грань порога
    xs = 0.40
    a.add(p_box((0.16, 0.14, 0.09), loc=(xs, ys - 0.05, F + 0.17), bevel=0.02), "iron_dark")
    a.add(p_box((0.07, 0.07, 0.10), loc=(xs, ys - 0.09, F + 0.12), bevel=0.0), "glow")
    # жёлоб лежит на плите: наклон в 3 см на полметра не читается, а зазор под ним читается
    fr, L = seg_frame((xs, ys - 0.09), (0.88, -1.16), z=F)
    fr.box(a, (L + 0.10, 0.22, 0.08), (L / 2, 0, 0.035), col="stone_dark", bevel=0.02)
    for sy in (-1, 1):
        fr.box(a, (L + 0.10, 0.06, 0.13), (L / 2, sy * 0.085, 0.06), col="stone_dark", bevel=0.015)
    fr.box(a, (L + 0.06, 0.11, 0.03), (L / 2, 0, 0.085), col="glow", bevel=0.0)
    # изложница: тёмный лоток с тремя свежими, ещё светящимися отливками
    mx, my = 1.14, -1.20
    a.add(p_box((0.66, 0.38, 0.10), loc=(mx, my, F + 0.045), bevel=0.025), "iron_dark")
    for i in (-1, 0, 1):
        a.add(p_box((0.15, 0.28, 0.02), loc=(mx + i * 0.20, my, F + 0.095), bevel=0.0), "glow")


def _ingots(a):
    # ---- слитки: золото и медь слева от зева, железо справа на поддоне ----------------------
    # золото — пирамидой 4-3-2-1, как Res-стопка кита, только в полтора раза крупнее
    _ingot_stack(a, "gold", -1.3, -1.25, rz=0, layers=(4, 3, 2, 1), s=1.3)
    _ingot_stack(a, "copper", -0.5, -1.62, rz=14, layers=(2, 1), s=1.25)
    # железо на сером камне пропадало — тёплый поддон отделяет стопку от плиты
    px, py = 1.78, -0.62
    for dx in (-0.18, 0.18):
        a.add(p_box((0.08, 0.46, 0.06), loc=(px + dx, py, F + 0.025), bevel=0.015), "wood_dark")
    a.add(p_box((0.54, 0.46, 0.06), loc=(px, py, F + 0.08), bevel=0.02), PLANK_TOP)
    _ingot_stack(a, "iron", px, py, rz=0, layers=(2, 1), s=1.25, z0=F + 0.105)


def _coal_bin(a, rng):
    # ---- угольный ларь у левого бока --------------------------------------------------------
    bx0, bx1, by0, by1, bh = -2.12, -1.62, -0.25, 0.75, 0.40
    bxc, byc = (bx0 + bx1) / 2, (by0 + by1) / 2
    for y in (by0 + 0.035, by1 - 0.035):
        a.add(p_box((bx1 - bx0, 0.07, bh), loc=(bxc, y, F + bh / 2), bevel=0.02), "wood_mid")
    for x in (bx0 + 0.035, bx1 - 0.035):
        a.add(p_box((0.07, by1 - by0 - 0.10, bh - 0.04), loc=(x, byc, F + bh / 2 - 0.02), bevel=0.02),
              "wood_light")
    a.add(p_box((bx1 - bx0 - 0.10, by1 - by0 - 0.10, 0.04), loc=(bxc, byc, F + 0.28), bevel=0.0), "coal")
    a.add(p_ico(0.36, 1, loc=(bxc, byc, F + 0.29), scl=(0.62, 1.15, 0.45), jitter=0.12, rng=rng, cut=0.0),
          "coal")
    for dx, dy, r in ((-0.06, -0.22, 0.09), (0.08, 0.05, 0.10), (-0.04, 0.26, 0.09), (0.02, -0.02, 0.08)):
        a.add(p_ico(r, 1, loc=(bxc + dx, byc + dy, F + 0.36), jitter=0.2, rng=rng), "coal")
    # лопата воткнута в уголь, черенок наклонён наружу
    sh = Frame((bxc - 0.02, byc + 0.18, F + 0.28), rot=(12, -14, 0))
    sh.box(a, (0.20, 0.035, 0.22), (0, 0, 0.0), col="iron", bevel=0.01)
    sh.cyl(a, 0.03, 0.03, 0.70, 6, loc=(0, 0, 0.10), col="wood_light")
    sh.box(a, (0.16, 0.05, 0.05), (0, 0, 0.80), col="wood_dark", bevel=0.012)


def _bellows_side(a):
    # ---- мехи у правого бока: носик в стену, хвост приподнят ----------------------------------
    zb = 0.47
    xw = W1 / 2 - (W1 - W1T) / 2 * (zb - Z0C) / (Z1 - 0.04 - Z0C)   # стена с завалом на высоте носика
    # Мехи повёрнуты на 35 градусов: вдоль оси X с герой-ракурса был виден только торец хвоста,
    # и они читались ящиком на табурете. Под углом видны и профиль-клин, и контур доски.
    _bellows(a, Frame((xw - 0.05, -0.04, zb), rot=(0, -14, 35)), floor_z=F, rz=35, L=0.72, w=0.52, h=0.30)


def _ore(a, rng):
    # ---- руда у правого тыльного угла -----------------------------------------------------------
    # ---- руда у тыла: куча справа и куча слева от трубы, ларь с углём у левого угла --------------
    for n, (ox, oy, rs) in enumerate(((1.62, 1.28, 0.42), (-1.55, 1.45, 0.36), (2.02, 0.8, 0.26))):
        a.add(p_ico(rs, 1, loc=(ox, oy, F - 0.02), scl=(0.95, 0.85, 0.5), jitter=0.12, rng=rng, cut=0.0),
              lambda f: "ore_rock" if f.normal.z > 0.3 else "ore_rock_dk")
        for k, (dx, dy, dz, kind, sz) in enumerate(((-0.08, -0.08, 0.10, "iron", 0.14), (0.10, 0.06, 0.07, "gold", 0.12),
                                                     (0.00, 0.16, 0.05, "iron", 0.12), (0.13, -0.13, 0.02, "copper", 0.11))):
            if n and k > 1:
                continue
            ore_chunk(a, kind, 40 + k + 7 * n, loc=(ox + dx * rs / 0.3, oy + dy * rs / 0.3, F + dz), size=sz)
    # горка угля у задней кромки справа от трубы
    a.add(p_ico(0.34, 1, loc=(0.62, 1.72, F - 0.02), scl=(1.2, 0.8, 0.42), jitter=0.12, rng=rng, cut=0.0), "coal")


def build(a):
    rng = random.Random(7)
    _podium(a)
    _furnace_low(a)
    _furnace_ledge(a)
    _chimney(a, rng)
    _portal(a, rng)
    _melt(a)
    _ingots(a)
    _coal_bin(a, rng)
    _bellows_side(a)
    _ore(a, rng)


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py): печь во дворе -> плавильный двор -> литейная
# =========================================================================================
def _band(a, z, top):
    """Железный обруч на трубе с золотыми заклёпками (как у уровня 1)."""
    s = _chimney_side(z, top) + 0.06
    a.add(p_box((s, s, 0.13), loc=(0, YC, z), bevel=0.0), "iron_dark")
    for u in (-0.26, 0.26):
        a.add(p_box((0.07, 0.03, 0.07), loc=(u, YC - (s / 2 + 0.005), z), bevel=0.0), "gold")


def _ore2(a, rng, spots):
    """Кучи руды с самородками железа, золота и меди."""
    for n, (ox, oy, rs) in enumerate(spots):
        a.add(p_ico(rs, 1, loc=(ox, oy, F - 0.02), scl=(0.95, 0.85, 0.5), jitter=0.12, rng=rng, cut=0.0),
              lambda f: "ore_rock" if f.normal.z > 0.3 else "ore_rock_dk")
        for k, (dx, dy, dz, kind, sz) in enumerate(((-0.08, -0.08, 0.10, "iron", 0.14), (0.10, 0.06, 0.07, "gold", 0.12))):
            ore_chunk(a, kind, 60 + k + 7 * n, loc=(ox + dx * rs / 0.3, oy + dy * rs / 0.3, F + dz), size=sz)


def _casting_shed(a, stone=False):
    """Литейный навес справа: столбы, кровля коньком вдоль Y фронтоном к камере; под ним изложница,
    поддон с железом и мехи (stone — каменные столбы и дощатый фронтон с вывеской-слитком)."""
    x0, x1, y0, y1 = 1.04, 2.32, -1.74, 0.16
    for x in (x0, x1):
        for y in (y0, y1):
            if stone:
                a.add(p_box((0.26, 0.26, 1.86), loc=(x, y, F + 0.93), bevel=0.03), AWALL)
            else:
                porch_post(a, x, y, F, 1.86, s=0.16)
    for x in (x0, x1):
        a.add(p_box((0.16, y1 - y0 + 0.20, 0.18), loc=(x, (y0 + y1) / 2, F + 1.86), bevel=0.0), BEAM)
    for y in (y0, y1):
        a.add(p_box((x1 - x0 + 0.20, 0.16, 0.18), loc=((x0 + x1) / 2, y, F + 1.86 + 0.02), bevel=0.0), BEAM)
    zr = gable_y_at(a, (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0, F + 1.96, pitch_deg=32, ox=0.14, oy=0.16)
    gable_front(a, (x0 + x1) / 2, y0, x1 - x0, F + 1.96, zr, depth=0.12, face=-1)
    if stone:
        fr = Frame(((x0 + x1) / 2, y0 - 0.08, F + 2.28))
        fr.box(a, (0.46, 0.05, 0.26), (0, 0, 0), col="wood_pale", bevel=0.0)
        fr.taper(a, (0.30, 0.04), (0.22, 0.04), 0.10, loc=(0, -0.04, -0.05), col="gold")


def _kiln(a, cx, cy):
    """Тигельная печь-«улей»: каменный барабан, купол, малая труба, светящийся зев к камере."""
    a.add(p_cyl(0.56, 0.52, 0.70, 10, loc=(cx, cy, F - 0.02), bevel=0.03), AWALL)
    a.add(p_ico(0.52, 1, loc=(cx, cy, F + 0.66), scl=(1.0, 1.0, 0.72), cut=0.0), by_normal("stone_light", "stone_mid", "stone_dark", 0.6))
    a.add(p_box((0.34, 0.20, 0.36), loc=(cx, cy - 0.48, F + 0.30), bevel=0.0), "stone_light")
    a.add(p_box((0.22, 0.06, 0.24), loc=(cx, cy - 0.58, F + 0.28), bevel=0.0), "glow")
    a.add(p_box((0.28, 0.28, 0.80), loc=(cx + 0.10, cy + 0.12, F + 1.10), bevel=0.0), AWALL)
    a.add(p_box((0.36, 0.36, 0.08), loc=(cx + 0.10, cy + 0.12, F + 1.52), bevel=0.0), "stone_dark")


def _yard_wall(a, pts, h=0.66):
    """Низкая каменная ограда двора по ломаной (x, y): тело, светлая шапка, столбики на изломах."""
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        L = math.hypot(xb - xa, yb - ya)
        rz = math.degrees(math.atan2(yb - ya, xb - xa))
        fr = Frame(((xa + xb) / 2, (ya + yb) / 2, F), rz=rz)
        fr.box(a, (L, 0.24, h), (0, 0, h / 2), col=AWALL, bevel=0.0)
        fr.box(a, (L + 0.06, 0.32, 0.08), (0, 0, h + 0.03), col="stone_light", bevel=0.0)
    for x, y in pts:
        a.add(p_box((0.34, 0.34, h + 0.20), loc=(x, y, F + (h + 0.20) / 2), bevel=0.0), "stone_light")


def _l2(a):
    """Плавильный двор: печь та же, труба выше на 0.7 с третьим обручем; справа — литейный навес под
    черепицей (изложница, слитки, мехи под ним); слева сзади — вторая, тигельная печь-«улей»; по тылу и
    левому боку — низкая каменная ограда."""
    rng = random.Random(7)
    top = ZC + 0.70
    _podium(a)
    _furnace_low(a)
    _furnace_ledge(a)
    _chimney(a, rng, top=top)
    _band(a, 3.12, top)
    _portal(a, rng)
    _melt(a)
    _ingots(a)
    _coal_bin(a, rng)
    _bellows_side(a)
    _casting_shed(a)
    _kiln(a, -1.62, 1.30)
    _yard_wall(a, [(-2.30, -0.40), (-2.30, 1.98), (2.30, 1.98)])
    _ore2(a, rng, ((1.62, 1.30, 0.40), (0.62, 1.66, 0.30)))


def _charging_ramp(a):
    """Колошниковый мост слева: наклонный настил на козлах от земли к колошнику печи, тачка с рудой."""
    x = -2.02
    ya, za, yb, zb = -1.62, F, 0.70, 1.96
    L = math.hypot(yb - ya, zb - za)
    ang = math.degrees(math.atan2(zb - za, yb - ya))
    fr = Frame((x, (ya + yb) / 2, (za + zb) / 2), rot=(ang, 0, 0))
    fr.box(a, (0.62, L, 0.08), (0, 0, 0), col=PLANKS, bevel=0.0)
    for k in range(7):
        fr.box(a, (0.64, 0.05, 0.04), (0, -L / 2 + 0.25 + k * (L - 0.5) / 6, 0.05), col="wood_dark", bevel=0.0)
    for sx in (-1, 1):
        fr.box(a, (0.06, L, 0.20), (sx * 0.33, 0, 0.08), col=BEAM, bevel=0.0)
    for t in (0.34, 0.68, 1.0):
        y = ya + (yb - ya) * t
        z = za + (zb - za) * t
        for sx in (-1, 1):
            a.add(p_box((0.11, 0.11, z - 0.08), loc=(x + sx * 0.26, y, (z - 0.08) / 2 + 0.02), bevel=0.0), BEAM)
    # площадка у колошника и вход в стену цеха
    a.add(p_box((0.62, 0.62, 0.10), loc=(x, yb + 0.28, zb - 0.02), bevel=0.0), PLANKS)
    a.add(p_box((0.06, 0.40, 0.64), loc=(-1.47, yb + 0.30, zb + 0.34), bevel=0.0), "black")
    # тачка с рудой посреди подъёма
    t = 0.45
    tf = Frame((x, ya + (yb - ya) * t, za + (zb - za) * t + 0.06), rot=(ang, 0, 0))
    tf.box(a, (0.36, 0.50, 0.20), (0, 0, 0.14), col="wood_mid", bevel=0.0)
    tf.ico(a, 0.20, loc=(0, 0, 0.26), scl=(0.85, 1.1, 0.5), col="ore_rock", sub=1)
    tf.cyl(a, 0.10, 0.10, 0.05, 8, loc=(0, -0.30, 0.06), rot=(0, 90, 0), col="wood_dark")


def _l3(a):
    """Литейная: печь внутри каменного цеха с черепичной кровлей — зев печи светит сквозь большую арку,
    труба на 1.6 м выше прежней, над коньком цеха; слева колошниковый мост с тачкой руды к колошнику, справа литейный навес
    на каменных столбах с вывеской-слитком; окна цеха светятся."""
    rng = random.Random(7)
    top = ZC + 1.60
    _podium(a)
    _furnace_low(a)
    _furnace_ledge(a)
    _chimney(a, rng, top=top)
    _band(a, 3.12, top)
    _band(a, 3.72, top)
    _portal(a, rng)
    _melt(a)
    _ingots(a)
    _bellows_side(a)
    # цех: стены вокруг печи, фасад — арка над зевом
    x0, x1, yf, yb, zw = -1.46, 1.46, -0.80, 1.56, 2.22
    t = 0.26
    a.add(p_box((t, yb - yf, zw - F), loc=(x0 + t / 2, (yf + yb) / 2, (F + zw) / 2), bevel=0.04), AWALL)
    a.add(p_box((t, yb - yf, zw - F), loc=(x1 - t / 2, (yf + yb) / 2, (F + zw) / 2), bevel=0.04), AWALL)
    a.add(p_box((x1 - x0, t, zw - F), loc=(0, yb - t / 2, (F + zw) / 2), bevel=0.04), AWALL)
    arched_wall(a, x0, x1, yf, t, F, zw, -0.88, 0.88, 0.98)
    quoins(a, 0, (yf + yb) / 2, x1 - x0, yb - yf + t, F, zw, corners=((-1, -1), (1, -1)))
    for x in (x1 + 0.005,):
        win(a, Frame((x, 0.42, 1.36), rz=90), w=0.40, h=0.52, lit=True, shutters=None, sill="stone_light")
    zr = gable_y_at(a, 0.0, (yf + yb) / 2 - t / 2, x1 - x0, yb - yf + t, zw, pitch_deg=34, ox=0.16, oy=0.18)
    gf_y = yf - t / 2 + 0.11
    gable_front(a, 0.0, gf_y, x1 - x0, zw - 0.02, zr, col=AWALL, depth=0.22, face=-1, boards=False)
    a.add(p_cyl(0.24, 0.24, 0.06, 12, loc=(0, gf_y - 0.10, zw + 0.42), rot=(90, 0, 0)), "stone_light")
    a.add(p_cyl(0.17, 0.17, 0.06, 12, loc=(0, gf_y - 0.12, zw + 0.42), rot=(90, 0, 0)), "lantern_glow")
    _casting_shed(a, stone=True)
    _charging_ramp(a)
    for x in (-1.10, 1.10):
        wall_lamp(a, x, yf - t / 2 - 0.02, 1.62, out=(0.3 if x > 0 else -0.3, -0.95))
    _ore2(a, rng, ((2.05, 1.30, 0.40), (-2.00, 1.55, 0.32)))
    a.add(p_ico(0.36, 1, loc=(-2.05, -0.70, F - 0.02), scl=(1.0, 1.3, 0.45), jitter=0.12, rng=rng, cut=0.0), "coal")


def evolve(a, level):
    """2: плавильный двор с навесом и второй печью. 3: литейный цех."""
    (_l2 if level == 2 else _l3)(a)
