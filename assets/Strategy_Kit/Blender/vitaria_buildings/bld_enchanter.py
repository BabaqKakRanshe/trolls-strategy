"""
Зачарователь: открытый каменный павильон, над постаментом парит крупный фиолетовый
кристалл, спереди — отдельно стоящая стрельчатая арка с рунными камнями.

Верх павильона открыт: кристалл — главный силуэт, и он обязан подниматься над кольцом
колоннады, а не прятаться под куполом. Колонн четыре, по диагоналям: фасадный проём
остаётся свободным, и постамент с кольцом рун видно сквозь арку.
"""
import math
import random

import bmesh
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal, TM
from vitaria_buildings.common import Frame, crystal, STONE_TOP, STONE_DARK_TOP

NAME = "Bld_Enchanter"
TITLE = "Зачарователь"
TARGET = (3.6, 3.6, 4.2)

FL = 0.40                  # пол павильона (верх второй ступени)
CR = 1.13                  # радиус осей колонн (по диагоналям)
ZC0, ZC1 = 2.60, 2.76      # капитель колонн
ZRG = 2.76                 # низ кольца
YA = -1.60                 # плоскость арки

GOLD = by_normal("gold_light", "gold", "gold_dark", 0.6)


def _oct_r(flat):
    """Радиус описанной окружности восьмиугольника по размеру «грань — грань»."""
    return flat / (2 * math.cos(math.radians(22.5)))


def _ring(a, r_out, r_in, h, seg, mat, col):
    """Шайба с отверстием по оси Z рамы mat (основание в z=0): кольцо без булевых."""
    b = bmesh.new()
    ob, ot, ib, it = [], [], [], []
    for i in range(seg):
        t = math.tau * i / seg
        c, s = math.cos(t), math.sin(t)
        ob.append(b.verts.new((r_out * c, r_out * s, 0.0)))
        ot.append(b.verts.new((r_out * c, r_out * s, h)))
        ib.append(b.verts.new((r_in * c, r_in * s, 0.0)))
        it.append(b.verts.new((r_in * c, r_in * s, h)))
    for i in range(seg):
        j = (i + 1) % seg
        b.faces.new((ob[i], ob[j], ot[j], ot[i]))
        b.faces.new((ib[j], ib[i], it[i], it[j]))
        b.faces.new((ot[i], ot[j], it[j], it[i]))
        b.faces.new((ob[j], ob[i], ib[i], ib[j]))
    bmesh.ops.transform(b, matrix=mat, verts=b.verts)
    b.normal_update()
    a.add(b, col)


def _arc(half, zs, c, t):
    """Точка правой дуги стрельчатой арки: центр дуги в (-c, zs), от пяты (half, zs) до вершины на x=0."""
    R = half + c
    th = t * math.atan2(math.sqrt(R * R - c * c), c)
    return (-c + R * math.cos(th), zs + R * math.sin(th))


def _platform(a):
    # две восьмигранные ступени: нижняя темнее, чтобы уступ читался и в тени
    a.add(p_cyl(_oct_r(3.56), _oct_r(3.56) * 0.99, 0.28, 8, loc=(0, 0, -0.10), spin=22.5, bevel=0.05),
          STONE_DARK_TOP)
    a.add(p_cyl(_oct_r(2.90), _oct_r(2.90) * 0.99, FL - 0.14, 8, loc=(0, 0, 0.14), spin=22.5, bevel=0.05),
          STONE_TOP)


def _columns(a):
    for k in range(4):
        ang = math.radians(45 + 90 * k)
        x, y = CR * math.cos(ang), CR * math.sin(ang)
        a.add(p_cyl(0.20, 0.18, 0.14, 8, loc=(x, y, FL), spin=22.5), STONE_TOP)
        a.add(p_cyl(0.115, 0.095, ZC0 - FL - 0.14, 8, loc=(x, y, FL + 0.14), spin=22.5), "stone_light")
        a.add(p_cyl(0.125, 0.125, 0.06, 8, loc=(x, y, ZC0 - 0.20), spin=22.5), "gold")
        # капитель раструбом — эльфийская «чаша» под кольцом
        a.add(p_cyl(0.10, 0.20, ZC1 - ZC0 - 0.05, 8, loc=(x, y, ZC0), spin=22.5), "stone_light")
        a.add(p_cyl(0.22, 0.22, 0.06, 8, loc=(x, y, ZC1 - 0.055), spin=22.5), GOLD)


def _halo(a):
    """Кольцо на колоннах: светлый верх, золотой пояс по внешней кромке, светильники над колоннами."""
    def ring_col(f):
        n = f.normal
        if n.z > 0.7:
            return "stone_light"
        if n.z < -0.7:
            return "stone_dark"
        c = f.calc_center_median()
        return "stone_mid" if c.x * n.x + c.y * n.y > 0 else "stone_dark"
    # кольцо сужено до 22 см: широкая плита читалась тяжёлой крышкой над кристаллом
    _ring(a, 1.30, 1.08, 0.16, 16, TM((0, 0, ZRG)), ring_col)
    _ring(a, 1.335, 1.29, 0.07, 16, TM((0, 0, ZRG + 0.045)), "gold")
    for k in range(4):
        ang = math.radians(45 + 90 * k)
        x, y = CR * math.cos(ang), CR * math.sin(ang)
        z = ZRG + 0.16
        a.add(p_cyl(0.07, 0.12, 0.10, 8, loc=(x, y, z - 0.005), spin=22.5), GOLD)
        crystal(a, Frame((x, y, z + 0.02), rz=30 + 90 * k), r=0.085, h=0.34)


def _pedestal(a):
    z = FL
    a.add(p_cyl(0.48, 0.44, 0.22, 8, loc=(0, 0, z - 0.01), spin=22.5, bevel=0.03), STONE_TOP)
    a.add(p_cyl(0.34, 0.31, 0.16, 8, loc=(0, 0, z + 0.20), spin=22.5), STONE_TOP)
    a.add(p_cyl(0.22, 0.19, 1.34, 8, loc=(0, 0, z + 0.35), spin=22.5), "stone_light")
    a.add(p_cyl(0.215, 0.215, 0.06, 8, loc=(0, 0, z + 1.50), spin=22.5), "gold")
    zb = z + 1.68
    a.add(p_cyl(0.19, 0.38, 0.26, 8, loc=(0, 0, zb), spin=22.5), by_normal("stone_light", "stone_mid"))
    a.add(p_cyl(0.40, 0.40, 0.06, 8, loc=(0, 0, zb + 0.25), spin=22.5), GOLD)
    # светящееся зеркало в чаше: источник, из которого «растёт» кристалл
    a.add(p_cyl(0.30, 0.30, 0.02, 8, loc=(0, 0, zb + 0.30), spin=22.5), "arcane")
    # руны на гранях ствола (на 1 см наружу от грани)
    for k in range(8):
        ang = math.radians(k * 45)
        if k % 2:
            continue
        r = 0.205 * math.cos(math.radians(22.5)) + 0.004
        a.add(p_box((0.08, 0.02, 0.36), loc=(r * math.cos(ang), r * math.sin(ang), z + 1.0),
                    rot=(0, 0, k * 45 + 90), bevel=0.0), "rune")
    return zb + 0.31


def _crystals(a, ztop):
    zc = ztop + 0.25                                   # зазор — кристалл парит
    crystal(a, Frame((0, 0, zc)), r=0.42, h=1.55)
    # три осколка на орбите, наклонены по касательной — «летят» вокруг
    for ang, dz, tilt in ((205, 0.25, 18), (330, 0.55, -16), (85, 0.85, 20)):
        t = math.radians(ang)
        x, y = 0.66 * math.cos(t), 0.66 * math.sin(t)
        crystal(a, Frame((x, y, zc + dz), rot=(tilt, 0, ang + 90)), r=0.075, h=0.30)


def _floor_runes(a):
    for k in range(12):
        ang = math.radians(k * 30 + 15)
        a.add(p_box((0.17, 0.08, 0.015), loc=(0.80 * math.cos(ang), 0.80 * math.sin(ang), FL + 0.0025),
                    rot=(0, 0, k * 30 + 15 + 90), bevel=0.0), "rune")


def _arch(a):
    """Стрельчатая арка из клиньев: пяты на капителях опор, замок — светящийся рунный камень."""
    si, so, c = 0.60, 0.88, 0.30          # полупролёт по внутренней и внешней дуге, сдвиг центров дуг
    zs = 1.91
    for sx in (-1, 1):
        x = sx * 0.74
        a.add(p_box((0.38, 0.28, 0.16), loc=(x, YA, 0.26), bevel=0.03), STONE_TOP)
        a.add(p_box((0.28, 0.26, 1.50), loc=(x, YA, 0.33 + 0.75), bevel=0.04), "stone_light")
        a.add(p_box((0.30, 0.28, 0.05), loc=(x, YA, 1.78), bevel=0.0), "gold")
        a.add(p_box((0.38, 0.30, 0.12), loc=(x, YA, 1.86), bevel=0.03), STONE_TOP)
        # вертикальная руна на лице опоры
        a.add(p_box((0.08, 0.02, 0.46), loc=(x, YA - 0.135, 1.05), bevel=0.0), "rune")
    n = 5
    for sx in (-1, 1):
        for k in range(n):
            t0, t1 = k / n, (k + 1) / n
            if k == n - 1:
                t1 = 1.0
            i0, o0 = _arc(si, zs, c, t0), _arc(so, zs, c, t0)
            i1, o1 = _arc(si, zs, c, t1), _arc(so, zs, c, t1)
            pts = [(sx * p[0], p[1]) for p in (i0, o0, o1, i1)]
            col = "rune" if k == 2 else ("stone_light" if k % 2 == 0 else "stone_mid")
            a.add(p_prism(pts, 0.24, loc=(0, YA, 0)), col)
    # замок и навершие
    zt_i = _arc(si, zs, c, 1.0)[1]
    zt_o = _arc(so, zs, c, 1.0)[1]
    a.add(p_prism([(-0.13, zt_i - 0.10), (0.13, zt_i - 0.10), (0.15, zt_o - 0.02), (0, zt_o + 0.14),
                   (-0.15, zt_o - 0.02)], 0.30, loc=(0, YA, 0)), "rune")
    a.add(p_cyl(0.07, 0.0, 0.28, 4, loc=(0, YA, zt_o + 0.10), spin=45), GOLD)


def _banner(a, fr):
    """Фиолетовая хоругвь (цвет арканной магии) с золотой перекладиной и ромбом; рама смотрит -Y наружу."""
    fr.box(a, (0.50, 0.08, 0.08), (0, 0.0, 0.03), col=GOLD, bevel=0.02)
    fr.box(a, (0.38, 0.045, 0.78), (0, 0, -0.39), col="crystal_dark", bevel=0.012)
    for sx in (-1, 1):
        fr.box(a, (0.17, 0.045, 0.20), (sx * 0.095, 0, -0.86), col="crystal_dark", bevel=0.012)
    fr.box(a, (0.13, 0.02, 0.13), (0, -0.03, -0.36), rot=(0, 45, 0), col="gold", bevel=0.0)


def build(a):
    _platform(a)
    _columns(a)
    _halo(a)
    # хоругви между колоннами слева, справа и сзади; фасад оставлен открытым под арку
    for ang in (0, 90, 180):
        t = math.radians(ang)
        _banner(a, Frame((1.37 * math.cos(t), 1.37 * math.sin(t), ZRG + 0.10), rz=ang + 90))
    ztop = _pedestal(a)
    _crystals(a, ztop)
    _floor_runes(a)
    _arch(a)
