"""
Уровни зданий поля: общий язык улучшений. Здание уровня 2 и 3 — та же сборка build(a), поверх неё
upgrade(a, level) модуля добавляет детали (сам уровень 1 не меняется). Читаться уровень должен с игровой
камеры колонии (сверху под 45°), поэтому знаки уровня — крупные: силуэт над кровлей и цвет.

Уровень 2 — «добротное»:
  - флажок цвета цепочки над кровлей (pennant, шарик железный);
  - больше товара и второй станок у входа: здание работает больше;
  - железо и камень там, где было дерево (оковка, каменные ступени).
Уровень 3 — «мастерское», всё уровня 2 и:
  - золото: полоса по коньку и навершия на его концах (gold_ridge), шарик флажка, оправа вывески;
  - знамя цвета цепочки с золотым знаком на фасаде (wall_banner) или на древке (standard);
  - фонари со светом у входа (wall_lantern);
  - своя крупная пристройка: башенка, вторая труба, кран, силос — то, что меняет силуэт.

Флажки и знамёна — багряные (cloth) с кремом и золотом, одни на всех: на кровле своего цвета
(roof, перекраска по цепочке) флажок того же цвета терялся бы, а один цвет знака уровня у всех зданий
читается как знак, а не как деталь здания.
"""
import math

from build_vitaria import p_box, p_cyl, p_ico, p_prism
from vitaria_buildings.common import Frame, lantern

TOP = {2: "iron_dark", 3: "gold"}         # шарик на древке по уровню


def pennant(a, x, y, z, level, h=1.25, side=1, col="cloth", band="cream", fl=0.92, fh=0.56):
    """Флагшток от z вверх на h и флажок-вымпел у верхушки, развёрнутый по X (side = ±1) в плоскости
    фасада: с камеры колонии он всегда виден плашмя. Вымпел багряный (cloth) с кремовой полосой у древка —
    на любой кровле цепочки (синей, сланцевой, соломенной, терракотовой) он контрастен."""
    a.add(p_cyl(0.06, 0.05, h, 6, loc=(x, y, z)), "wood_dark")
    a.add(p_ico(0.09, 1, loc=(x, y, z + h + 0.06)), TOP[min(level, 3)])
    zt = z + h - 0.06
    zb = zt - fh
    k = 0.22                                             # кремовая полоса у древка — доля длины
    band_pts = [(0.0, zt), (side * fl * k, zt - fh * k * 0.5), (side * fl * k, zb + fh * k * 0.5), (0.0, zb)]
    tail_pts = [(side * fl * k, zt - fh * k * 0.5), (side * fl, zt - fh * 0.5), (side * fl * k, zb + fh * k * 0.5)]
    for pts, c in ((band_pts, band), (tail_pts, col)):
        if side < 0:
            pts = list(reversed(pts))
        a.add(p_prism(pts, 0.05, loc=(x + side * 0.04, y, 0)), c)


def standard(a, x, y, z, level, h=1.7, w=0.50, bh=0.78, col="cloth", face=-1):
    """Знамя на древке: поперечина у верхушки и полотно с двумя лопастями; золотой знак-ромб на
    полотне. face — куда смотрит лицо полотна по Y (-1 — на юг, к камере)."""
    a.add(p_cyl(0.055, 0.045, h, 6, loc=(x, y, z)), "wood_dark")
    a.add(p_ico(0.09, 1, loc=(x, y, z + h + 0.06)), TOP[min(level, 3)])
    zt = z + h - 0.10
    a.add(p_box((w + 0.14, 0.07, 0.07), loc=(x, y, zt), bevel=0.0), "wood_dark")
    a.add(p_box((w, 0.05, bh), loc=(x, y + face * 0.05, zt - bh / 2 - 0.04), bevel=0.0), col)
    for sx in (-1, 1):
        a.add(p_box((w / 2 - 0.03, 0.05, 0.20), loc=(x + sx * w / 4, y + face * 0.05, zt - bh - 0.13), bevel=0.0),
              col)
    a.add(p_box((0.20, 0.03, 0.20), loc=(x, y + face * 0.085, zt - bh * 0.45), rot=(0, 45, 0), bevel=0.0), "gold")


def wall_banner(a, x, y, z, w=0.46, h=0.86, col="cloth", face=-1, rz=0.0):
    """Знамя на стене: золотой прут с концами, полотно цвета цепочки с двумя лопастями, золотая кайма по
    низу полотна и ромб-знак. z — верх прута; face — нормаль стены по оси (−1: стена смотрит в −Y)."""
    fr = Frame((x, y, z), rz=rz)
    fr.box(a, (w + 0.16, 0.06, 0.06), (0, 0, 0), col="gold", bevel=0.0)
    fr.box(a, (w, 0.045, h), (0, face * 0.04, -h / 2 - 0.03), col=col, bevel=0.0)
    for sx in (-1, 1):
        fr.box(a, (w / 2 - 0.03, 0.045, 0.20), (sx * w / 4, face * 0.04, -h - 0.13), col=col, bevel=0.0)
    fr.box(a, (w, 0.03, 0.06), (0, face * 0.07, -h + 0.02), col="gold", bevel=0.0)
    fr.box(a, (0.19, 0.03, 0.19), (0, face * 0.07, -h * 0.42), rot=(0, 45, 0), col="gold", bevel=0.0)


def gold_ridge(a, length, zr, axis="y", x=0.0, y=0.0, ends=True, cap=0.38):
    """Золотая полоса по ребру конька кита (брус cap x cap, повёрнутый на 45°, центр на zr + 0.09 —
    как в gable_roof) и навершия на её концах. length — длина конька (Ly или Lx кровли)."""
    zt = zr + 0.09 + cap * 0.7071
    size = (0.10, length + 0.10, 0.07) if axis == "y" else (length + 0.10, 0.10, 0.07)
    a.add(p_box(size, loc=(x, y, zt - 0.01), bevel=0.0), "gold")
    if ends:
        for s in (-1, 1):
            px, py = (x, y + s * length / 2) if axis == "y" else (x + s * length / 2, y)
            finial(a, px, py, zt - 0.04)


def finial(a, x, y, z, h=0.46, col="gold"):
    """Навершие: подставка, шпиль и шарик."""
    a.add(p_cyl(0.10, 0.07, 0.08, 6, loc=(x, y, z)), col)
    a.add(p_cyl(0.05, 0.0, h, 6, loc=(x, y, z + 0.08)), col)
    a.add(p_ico(0.085, 1, loc=(x, y, z + 0.08 + h * 0.42)), col)


def wall_lantern(a, x, y, z, out=(0.0, -1.0)):
    """Фонарь на кронштейне: железный прут из стены на 0.3 м по out, фонарь кита висит на конце.
    Свечение (lantern_glow) уходит в слот Vitaria_FX."""
    ox, oy = out
    rz = math.degrees(math.atan2(oy, ox))
    fr = Frame((x, y, z), rz=rz)
    fr.box(a, (0.34, 0.05, 0.05), (0.17, 0, 0), col="iron_dark", bevel=0.0)
    fr.box(a, (0.05, 0.05, 0.20), (0.02, 0, -0.08), col="iron_dark", bevel=0.0)
    lantern(a, Frame((x + ox * 0.30, y + oy * 0.30, z - 0.44)))


def ingot_pallet(a, x, y, z, rz=0.0, col="iron", rows=(3, 2)):
    """Поддон со слитками: брусья поддона и слитки стопкой (3 + 2), как Res_IngotStack в малом."""
    fr = Frame((x, y, z), rz=rz)
    for sy in (-1, 1):
        fr.box(a, (0.56, 0.08, 0.07), (0, sy * 0.13, 0.035), col="wood_dark", bevel=0.0)
    fr.box(a, (0.58, 0.40, 0.04), (0, 0, 0.09), col="wood_mid", bevel=0.0)
    lt = {"iron": "iron_light", "gold": "gold_light", "copper": "copper", "steel": "steel"}.get(col, col)
    for row, n in enumerate(rows):
        for k in range(n):
            xx = (k - (n - 1) / 2) * 0.17
            fr.taper(a, (0.15, 0.30), (0.11, 0.25), 0.07, loc=(xx, 0, 0.11 + row * 0.075),
                     col=lambda f, c=col, l=lt: l if f.normal.z > 0.5 else c)


def lamp_post(a, x, y, z0=0.0, h=1.55):
    """Фонарный столб: каменная подушка, столб, кронштейн и фонарь кита со светом."""
    a.add(p_box((0.24, 0.24, 0.12), loc=(x, y, z0 + 0.06), bevel=0.0), "stone_mid")
    a.add(p_box((0.11, 0.11, h), loc=(x, y, z0 + h / 2), bevel=0.0), "wood_dark")
    a.add(p_box((0.34, 0.07, 0.07), loc=(x + 0.13, y, z0 + h - 0.05), bevel=0.0), "wood_dark")
    lantern(a, Frame((x + 0.26, y, z0 + h - 0.48)))


def brazier(a, x, y, z0=0.0, h=0.72, r=0.26):
    """Жаровня на каменной тумбе: железная чаша с углями и языками жара (свет — Vitaria_FX)."""
    st = lambda f: "stone_light" if f.normal.z > 0.5 else "stone_mid"
    a.add(p_cyl(r * 0.85, r * 0.7, h, 6, loc=(x, y, z0)), st)
    a.add(p_cyl(r * 0.95, r * 0.95, 0.06, 6, loc=(x, y, z0 + h - 0.04)), "stone_dark")
    a.add(p_cyl(r * 0.55, r * 1.05, 0.20, 8, loc=(x, y, z0 + h)), "iron_dark")
    a.add(p_cyl(r * 0.95, r * 0.95, 0.03, 8, loc=(x, y, z0 + h + 0.16)), "ember")
    for dx, dy, s, c in ((0.0, 0.0, 1.0, "glow"), (0.07, -0.05, 0.7, "glow_hot"), (-0.08, 0.04, 0.75, "ember")):
        a.add(p_cyl(0.10 * s, 0.0, 0.34 * s, 5, loc=(x + dx, y + dy, z0 + h + 0.15)), c)


def gold_chest(a, fr):
    """Открытый сундук с горкой монет: корпус, железные полосы, откинутая крышка, золото."""
    fr.box(a, (0.56, 0.36, 0.30), (0, 0, 0.15), col="wood_mid", bevel=0.03)
    for x in (-0.20, 0.20):
        fr.box(a, (0.05, 0.38, 0.31), (x, 0, 0.15), col="iron_dark", bevel=0.0)
    fr.box(a, (0.56, 0.08, 0.30), (0, 0.20, 0.42), rot=(-12, 0, 0), col="wood_dark", bevel=0.0)
    fr.ico(a, 0.24, loc=(0, 0, 0.28), scl=(1.1, 0.70, 0.40), col="gold", cut=0.0)
    for x, y in ((-0.13, -0.06), (0.10, -0.08), (0.0, 0.05), (0.17, 0.06)):
        fr.cyl(a, 0.06, 0.06, 0.02, 8, loc=(x, y, 0.33), rot=(20, 0, 0), col="gold_light")


def turret(a, x, y, z0, level, r=0.34, h=1.5, rh=0.9, col="stone_light", roof="roof", seg=8):
    """Угловая башенка: выступ-кронштейн снизу, круглый ствол, карниз, конус кровли, навершие."""
    st = lambda f: col if f.normal.z > 0.5 else "stone_mid"
    a.add(p_cyl(r * 0.45, r, 0.36, seg, loc=(x, y, z0 - 0.36)), "stone_mid")
    a.add(p_cyl(r, r, h, seg, loc=(x, y, z0)), st)
    a.add(p_cyl(r + 0.07, r + 0.07, 0.10, seg, loc=(x, y, z0 + h - 0.04)), "stone_dark")
    a.add(p_cyl(r + 0.12, 0.0, rh, seg, loc=(x, y, z0 + h + 0.06)), roof)
    a.add(p_box((0.09, 0.05, 0.30), loc=(x, y - r - 0.01, z0 + h * 0.55), bevel=0.0), "black")
    finial(a, x, y, z0 + h + 0.06 + rh - 0.06, h=0.36, col="gold" if level >= 3 else "iron_dark")


def belfry(a, x, y, z0, level, s=0.62, h=0.78):
    """Звонница на коньке: четыре столба на обвязке, шатёр цвета цепочки, колокол (золотой на 3)."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.09, 0.09, h), loc=(x + sx * s / 2, y + sy * s / 2, z0 + h / 2), bevel=0.0), "wood_dark")
    a.add(p_box((s + 0.16, s + 0.16, 0.10), loc=(x, y, z0 + 0.04), bevel=0.0), "wood_dark")
    a.add(p_box((s + 0.16, s + 0.16, 0.10), loc=(x, y, z0 + h), bevel=0.0), "wood_dark")
    r = (s / 2 + 0.16) * 1.4142
    a.add(p_cyl(r, 0.0, 0.62, 4, loc=(x, y, z0 + h + 0.05), spin=45), "roof")
    bell = "gold" if level >= 3 else "copper"
    a.add(p_cyl(0.10, 0.18, 0.26, 8, loc=(x, y, z0 + h - 0.40)), bell)
    a.add(p_ico(0.07, 1, loc=(x, y, z0 + h - 0.14)), bell)
    finial(a, x, y, z0 + h + 0.62, h=0.30, col="gold" if level >= 3 else "iron_dark")


def dormer(a, x, y_eave, z_eave, pitch_deg, level, w=0.62, h=0.62, depth=0.80, glow=True):
    """Слуховое окно на скате с коньком вдоль X (скат опускается к -Y): короб с окном лицом к -Y,
    двускатная крышечка цвета кровли. (x, y_eave, z_eave) — точка ската, на которую садится фасад окна."""
    t = math.tan(math.radians(pitch_deg))
    zb = z_eave - 0.10
    zt = zb + h
    a.add(p_box((w, depth, h + depth * t), loc=(x, y_eave + depth / 2, zb + (h - depth * t) / 2),
                bevel=0.0), "wood_light")
    a.add(p_box((w - 0.18, 0.06, h - 0.20), loc=(x, y_eave - 0.01, zb + h / 2 + 0.02), bevel=0.0),
          "lantern_glow" if glow else "glass")
    a.add(p_box((w - 0.08, 0.08, 0.08), loc=(x, y_eave - 0.02, zb + h / 2 + 0.02), bevel=0.0), "wood_dark")
    for sx in (-1, 1):
        a.add(p_box((w / 2 + 0.14, depth + 0.16, 0.08), loc=(x + sx * (w / 4 + 0.02), y_eave + depth / 2 - 0.06,
                                                              zt + 0.13), rot=(0, sx * 32, 0), bevel=0.0),
              "roof" if sx < 0 else "roof_dark")


def dummy(a, fr):
    """Учебное чучело: столб, перекладина-руки, мешок-туловище, голова, щит на руке."""
    fr.box(a, (0.10, 0.10, 1.50), (0, 0, 0.75), col="wood_dark", bevel=0.0)
    fr.box(a, (0.80, 0.08, 0.08), (0, 0, 1.12), col="wood_mid", bevel=0.0)
    fr.taper(a, (0.40, 0.30), (0.32, 0.26), 0.56, loc=(0, 0, 0.62), col="burlap")
    fr.box(a, (0.44, 0.33, 0.05), (0, 0, 0.92), col="rope", bevel=0.0)
    fr.ico(a, 0.15, loc=(0, 0, 1.40), col="burlap", sub=1)
    fr.box(a, (0.30, 0.05, 0.36), (0.36, -0.08, 0.98), col="wood_light", bevel=0.0)


def target(a, fr, r=0.42):
    """Мишень на треноге лицом к -Y рамы: соломенный круг, кольца, яблочко."""
    for ang in (-20, 20):
        fr.box(a, (0.07, 0.07, 1.20), (math.sin(math.radians(ang)) * 0.30, 0.10, 0.56), rot=(0, ang, 0),
               col="wood_dark", bevel=0.0)
    fr.box(a, (0.07, 0.07, 1.00), (0, 0.32, 0.46), rot=(-22, 0, 0), col="wood_dark", bevel=0.0)
    fr.cyl(a, r, r, 0.12, 12, loc=(0, 0.04, 0.85), rot=(90, 0, 0), col="wood_yellow")
    for rr, col in ((r * 0.80, "cream"), (r * 0.58, "berry"), (r * 0.36, "cream"), (r * 0.16, "berry")):
        fr.cyl(a, rr, rr, 0.02, 12, loc=(0, -0.02 - (r - rr) * 0.02, 0.85), rot=(90, 0, 0), col=col)
