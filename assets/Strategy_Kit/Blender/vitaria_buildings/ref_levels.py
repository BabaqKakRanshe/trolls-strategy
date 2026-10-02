"""
Уровни 2 и 3 казармы и склада. Сами здания — модули Ref_Buildings/Source/parts (bld_barracks,
bld_warehouse) и не меняются: здесь только детали, которые добавляются поверх уровня 1, в тех же осях
(до доводки finish). Модули parts берутся из sys.path, как в build_ref_player.
"""
import importlib
import math

from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from vitaria_buildings.common import Frame, crate, barrel, lantern
from vitaria_buildings.levels import (pennant, gold_ridge, wall_banner, wall_lantern, brazier, dummy, target,
                                      finial)


def _tower(a, x, y, level, s=0.92, h=3.90):
    """Сторожевая башня в тыльном углу казармы: каменный ствол, зубцы, шатёр цвета цепочки, навершие."""
    st = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
    a.add(p_box((s + 0.14, s + 0.14, 0.30), loc=(x, y, 0.08), bevel=0.04), "stone_dark")
    a.add(p_box((s, s, h), loc=(x, y, h / 2), bevel=0.05), st)
    a.add(p_box((s + 0.18, s + 0.18, 0.16), loc=(x, y, h - 0.02), bevel=0.03), "stone_light")
    for k in range(4):
        for j in (-1, 1):
            cx = x + (j * s * 0.32 if k % 2 == 0 else (1 if k == 1 else -1) * s * 0.52)
            cy = y + ((1 if k == 0 else -1) * s * 0.52 if k % 2 == 0 else j * s * 0.32)
            a.add(p_box((0.24, 0.24, 0.30), loc=(cx, cy, h + 0.21), bevel=0.0), "stone_light")
    r = (s / 2 + 0.16) * 1.4142
    a.add(p_cyl(r, 0.0, 1.05, 4, loc=(x, y, h + 0.10), spin=45), "roof")
    finial(a, x, y, h + 1.10, h=0.40, col="gold" if level >= 3 else "iron_dark")
    a.add(p_box((0.12, 0.08, 0.56), loc=(x, y - s / 2 - 0.02, h - 0.80), bevel=0.0), "black")
    a.add(p_box((0.12, 0.08, 0.56), loc=(x, y - s / 2 - 0.02, h - 2.10), bevel=0.0), "black")
    wall_banner(a, x, y - s / 2 - 0.03, h - 0.20, w=0.40, h=0.70)


def barracks(a, level):
    """2: флажок на переднем скате, учебные чучела слева перед двором, мишень справа.
    3: + сторожевая башня в правом тыльном углу со знаменем, золото по коньку с навершиями, жаровни
    по сторонам ворот."""
    B = importlib.import_module("parts.bld_barracks")
    zr = 2.62 + 0.08 + (1.90 / 2) * math.tan(math.radians(22.0))
    t = math.tan(math.radians(22.0))
    pennant(a, 1.30, -0.55, zr - 0.55 * t + 0.08, level, h=1.30, side=1)
    for x, rz in ((-2.05, 8), (-1.30, -10)):
        dummy(a, Frame((x, -2.66, 0.0), rz=rz))
    target(a, Frame((2.05, -2.70, 0.0), rz=-6))
    if level < 3:
        return
    _tower(a, 2.40, 0.70, level)
    gold_ridge(a, 4.60 + 2 * 0.14 + 0.14, zr, axis="x")
    for sx in (-1, 1):
        brazier(a, sx * 1.25, B.YF - 1.10, 0.0, h=0.66)


def warehouse(a, level):
    """2: флажок на правом скате, ящик на верёвке подъёмника, мешки и ящики у левого угла перед складом.
    3: + герб с весами на фронтоне, золото по коньку с навершиями, фонари у ворот, тележка с грузом справа."""
    W = importlib.import_module("parts.bld_warehouse")
    pitch = 36.0
    zr = W.ZT + 0.08 + (W.W / 2) * math.tan(math.radians(pitch))
    t = math.tan(math.radians(pitch))
    pennant(a, 1.00, -1.10, zr - 1.00 * t + 0.08, level, h=1.25, side=1)
    crate(a, (0.0, -2.08, 2.22), s=0.40, rot=(0, 0, 12))
    a.add(p_box((0.03, 0.03, 0.40), loc=(0.0, -2.08, 2.62), bevel=0.0), "rope")
    for k, (x, y, z, rz) in enumerate(((-2.18, -2.42, 0.0, 8), (-1.74, -2.48, 0.0, -6), (-1.96, -2.42, 0.42, 20))):
        crate(a, (x, y, z), s=0.42, rot=(0, 0, rz))
    for x, y in ((-2.50, -2.10), (-1.42, -2.56)):
        fr = Frame((x, y, 0.0))
        fr.ico(a, 0.20, loc=(0, 0, 0.16), scl=(1.1, 0.95, 1.0), col="burlap", cut=-0.7)
        fr.cyl(a, 0.09, 0.05, 0.10, 6, loc=(0, 0, 0.34), col="burlap_dark")
    if level < 3:
        return
    zc = W.ZT + (zr - W.ZT) * 0.36
    yg = -W.D / 2 - 0.12
    a.add(p_cyl(0.40, 0.40, 0.08, 16, loc=(0, yg + 0.04, zc), rot=(90, 0, 0)), "gold")
    a.add(p_cyl(0.34, 0.34, 0.04, 16, loc=(0, yg - 0.01, zc), rot=(90, 0, 0)), "roof")
    fr = Frame((0, yg - 0.05, zc))
    fr.box(a, (0.05, 0.03, 0.40), (0, 0, -0.02), col="gold", bevel=0.0)
    fr.box(a, (0.44, 0.03, 0.04), (0, 0, 0.14), col="gold", bevel=0.0)
    for sx in (-1, 1):
        fr.box(a, (0.02, 0.03, 0.14), (sx * 0.19, 0, 0.06), col="gold", bevel=0.0)
        fr.box(a, (0.16, 0.03, 0.04), (sx * 0.19, 0, -0.02), col="gold", bevel=0.0)
    gold_ridge(a, W.D + 2 * 0.40 + 0.14, zr, axis="y")
    for sx in (-1, 1):
        wall_lantern(a, sx * 1.02, -W.D / 2 + 0.05, 1.90, out=(sx * 0.3, -0.95))
    cart = Frame((1.95, -2.38, 0.0), rz=-12)
    cart.box(a, (0.90, 0.56, 0.08), (0, 0, 0.40), col=by_normal("wood_light", "wood_mid", "wood_dark", 0.8), bevel=0.0)
    for sy in (-1, 1):
        cart.box(a, (0.90, 0.06, 0.18), (0, sy * 0.28, 0.52), col="wood_mid", bevel=0.0)
        cart.cyl(a, 0.25, 0.25, 0.06, 12, loc=(0.0, sy * 0.33, 0.25), rot=(90 * sy, 0, 0), col="wood_dark")
    crate(a, tuple(cart.at((-0.18, 0.0, 0.44))), s=0.36, rot=(0, 0, -12))
    barrel(a, tuple(cart.at((0.22, 0.0, 0.44))), r=0.16, h=0.34)


UPGRADE = {"Bld_Barracks": barracks, "Bld_Warehouse": warehouse}
