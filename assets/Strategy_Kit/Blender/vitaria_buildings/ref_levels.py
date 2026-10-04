"""
Уровни 2 и 3 казармы и склада. Уровень 1 — модули Ref_Buildings/Source/parts (bld_barracks,
bld_warehouse), они не меняются; здесь перестройка тех же зданий в их осях (до доводки finish):
корпус уровня 1 повторён по его же константам, поверх — новая архитектура (vitaria_buildings/levels.py).
Модули parts берутся из sys.path, как в build_ref_player.
"""
import importlib
import math

from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from vitaria_buildings.common import (Frame, crate, barrel, lantern, gable_roof, gable_roof_x, gable_wall_x, gate,
                                      stone_base, cornice, plank_door)
from vitaria_buildings.levels import (Shift, quoins, win, fachwerk, gable_front, gable_y_at, ridge_louver, wall_lamp,
                                    arched_wall, dormer, cupola, shed_roof, porch_post, tower_square, WALL, PLASTER, BEAM,
                                    PLANKS)


# =========================================================================================
# Казарма: форт-зал -> казармы с угловыми башнями и учебным плацем -> крепость с донжоном и надвратной башней
# (уровень 1 — parts.bld_barracks: зал 5.6 x 2.7 под зубцами, низкая кровля коньком вдоль X)
# =========================================================================================
def _dummy(a, fr):
    """Учебное чучело: столб, перекладина-руки, мешок-туловище, голова, щит на руке."""
    fr.box(a, (0.10, 0.10, 1.50), (0, 0, 0.75), col="wood_dark", bevel=0.0)
    fr.box(a, (0.80, 0.08, 0.08), (0, 0, 1.12), col="wood_mid", bevel=0.0)
    fr.taper(a, (0.40, 0.30), (0.32, 0.26), 0.56, loc=(0, 0, 0.62), col="burlap")
    fr.box(a, (0.44, 0.33, 0.05), (0, 0, 0.92), col="rope", bevel=0.0)
    fr.ico(a, 0.15, loc=(0, 0, 1.40), col="burlap", sub=1)
    fr.box(a, (0.30, 0.05, 0.36), (0.36, -0.08, 0.98), col="wood_light", bevel=0.0)


def _target(a, fr, r=0.42):
    """Мишень на треноге лицом к -Y рамы: соломенный круг, кольца, яблочко."""
    for ang in (-20, 20):
        fr.box(a, (0.07, 0.07, 1.20), (math.sin(math.radians(ang)) * 0.30, 0.10, 0.56), rot=(0, ang, 0),
               col="wood_dark", bevel=0.0)
    fr.box(a, (0.07, 0.07, 1.00), (0, 0.32, 0.46), rot=(-22, 0, 0), col="wood_dark", bevel=0.0)
    fr.cyl(a, r, r, 0.12, 12, loc=(0, 0.04, 0.85), rot=(90, 0, 0), col="wood_yellow")
    for rr, col in ((r * 0.80, "cream"), (r * 0.58, "berry"), (r * 0.36, "cream"), (r * 0.16, "berry")):
        fr.cyl(a, rr, rr, 0.02, 12, loc=(0, -0.02 - (r - rr) * 0.02, 0.85), rot=(90, 0, 0), col=col)


def _bk_hall(a, B, gate=True):
    """Корпус казармы как у уровня 1 (parts.bld_barracks): цоколь, кладка, пилястры, пояс, зубцы, кровля,
    труба, бойницы на боках; gate=False — ворота ставит надвратная башня."""
    C = importlib.import_module("parts.common")
    W, D, F, ZT, BZ, BT, YF = B.W, B.D, B.F, B.ZT, B.BZ, B.BT, B.YF
    C.stone_base(a, W + 0.40, D + 0.40, h=0.30)
    a.add(p_box((W, D, ZT - F), loc=(0, 0, (F + ZT) / 2), bevel=0.05),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.36, 0.36, ZT - F), loc=(sx * W / 2, sy * D / 2, (F + ZT) / 2), bevel=0.05), "stone_light")
    for sx in (-1, 1):
        a.add(p_box((0.22, D + 0.18, BT), loc=(sx * W / 2, 0, BZ), bevel=0.04), "stone_light")
        a.add(p_box((1.99, 0.22, BT), loc=(sx * 1.895, YF, BZ), bevel=0.04), "stone_light")
    a.add(p_box((W + 0.18, 0.22, BT), loc=(0, -YF, BZ), bevel=0.04), "stone_light")
    C.battlements(a, W, D, ZT, bw=0.44, bh=0.50)
    zr = C.gable_roof_x(a, 4.60, 1.90, 2.62, pitch_deg=22.0, ox=0.14, oy=0.16)
    C.gable_wall_x(a, 4.60, 1.90, 2.50, zr, depth=0.16)
    C.chimney(a, -2.10, 0.72, 2.40, h=1.65, w=0.60)
    if gate:
        C.gate(a, YF, F, w=1.55, h=1.95)
        a.add(p_box((1.85, 0.50, 0.22), loc=(0, YF - 0.37, 0.07), bevel=0.05), "stone_light")
    for sx in (-1, 1):
        bx = sx * 1.40
        C.banner(a, (bx, YF - 0.04, 2.15), w=0.76, h=1.05, pole=False)
        for ss in (-1, 1):
            B._sword(a, bx + ss * 0.13, YF - 0.08, 1.50, -ss * 35)
    for sx in (-1, 1):
        for y in (-0.55, 0.55):
            C.arrow_slit(a, (sx * (W / 2 + 0.02), y, 1.65), rot=(0, 0, sx * 90), h=0.66)
    return zr


def _bk_l2(a):
    """Казармы: на передних углах — квадратные башни выше зубцов с шатрами цвета кровли, перед фасадом —
    учебный плац: два чучела слева и мишень справа; стойки со щитами и копьями — у башен."""
    B = importlib.import_module("parts.bld_barracks")
    _bk_hall(a, B)
    for sx in (-1, 1):
        tower_square(a, sx * 2.44, B.YF + 0.30, 0.94, 0.0, 3.30, roof_h=1.00, slits=2)
    B._shield_rack(a, -1.98)
    B._spear_rack(a)
    for x, rz in ((-1.60, 8), (-0.92, -10)):
        _dummy(a, Frame((x, -2.66, 0.0), rz=rz))
    _target(a, Frame((1.60, -2.70, 0.0), rz=-6))
    barrel(a, (2.62, -2.10, 0.0))


def _bk_l3(a):
    """Крепость: над залом поднимается донжон с зубцами и шатром, ворота вынесены в надвратную башню с
    зубцами и решёткой над проёмом, на углах — башни выше, чем у казарм; плац с чучелами и мишенью,
    фонари у ворот."""
    B = importlib.import_module("parts.bld_barracks")
    C = importlib.import_module("parts.common")
    _bk_hall(a, B, gate=False)
    # донжон в середине за воротами
    kx0, kx1, ky0, ky1, kz = -0.86, 0.86, -0.30, 1.05, 4.50
    a.add(p_box((kx1 - kx0, ky1 - ky0, kz), loc=(0, (ky0 + ky1) / 2, kz / 2), bevel=0.05),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))
    quoins(a, 0, (ky0 + ky1) / 2, kx1 - kx0, ky1 - ky0, 2.70, kz, corners=((-1, -1), (1, -1)))
    a.add(p_box((kx1 - kx0 + 0.18, ky1 - ky0 + 0.18, 0.16), loc=(0, (ky0 + ky1) / 2, kz + 0.02), bevel=0.0), "stone_light")
    C.battlements(a, kx1 - kx0 + 0.16, ky1 - ky0 + 0.16, kz + 0.10, bw=0.30, bh=0.36, gap=0.26)
    r = (max(kx1 - kx0, ky1 - ky0) / 2 + 0.02) * 1.4142
    Frame((0, (ky0 + ky1) / 2, kz + 0.12), s=(1.0, (ky1 - ky0) / (kx1 - kx0), 1.0)).cyl(
        a, r * 0.86, 0.0, 1.10, 4, spin=45, col=by_normal("roof", "roof", "roof_dark", 0.2))
    for x in (-0.40, 0.40):
        C.arrow_slit(a, (x, ky0 - 0.01, 3.55), h=0.56)
    # надвратная башня: выступ перед фасадом, ворота в нём, зубцы, решётка над проёмом
    gx0, gx1, gy0, gy1, gz = -1.08, 1.08, B.YF - 0.62, B.YF + 0.05, 3.30
    a.add(p_box((gx1 - gx0, gy1 - gy0, gz), loc=(0, (gy0 + gy1) / 2, gz / 2), bevel=0.05),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))
    quoins(a, 0, (gy0 + gy1) / 2, gx1 - gx0, gy1 - gy0, 0.0, gz, corners=((-1, -1), (1, -1)))
    C.battlements(a, gx1 - gx0 + 0.10, gy1 - gy0 + 0.10, gz, bw=0.32, bh=0.40, gap=0.28)
    C.gate(a, gy0, B.F, w=1.30, h=1.80)
    a.add(p_box((1.60, 0.46, 0.22), loc=(0, gy0 - 0.35, 0.07), bevel=0.05), "stone_light")
    for k in range(5):
        a.add(p_box((0.05, 0.05, 0.62), loc=(-0.48 + k * 0.24, gy0 - 0.05, 2.62), bevel=0.0), "iron_dark")
    a.add(p_box((1.18, 0.05, 0.05), loc=(0, gy0 - 0.05, 2.46), bevel=0.0), "iron_dark")
    a.add(p_box((1.30, 0.04, 0.70), loc=(0, gy0 - 0.01, 2.62), bevel=0.0), "black")
    for sx in (-1, 1):
        tower_square(a, sx * 2.42, B.YF + 0.30, 0.98, 0.0, 3.80, roof_h=1.10, slits=3)
        wall_lamp(a, sx * 1.20, gy0 + 0.02, 1.86, out=(sx * 0.3, -0.95))
    B._shield_rack(a, -1.98)
    B._spear_rack(a)
    for x, rz in ((-1.62, 8), (-0.98, -10)):
        _dummy(a, Frame((x, -2.78, 0.0), rz=rz))
    _target(a, Frame((1.62, -2.80, 0.0), rz=-6))


def barracks_evolve(a, level):
    """2: казармы с угловыми башнями и плацем. 3: крепость с донжоном и надвратной башней."""
    (_bk_l2 if level == 2 else _bk_l3)(a)


# =========================================================================================
# Склад: деревянный склад -> каменный амбар с погрузочной площадкой -> двухэтажный пакгауз с краном
# (уровень 1 — parts.bld_warehouse: W 4.2 x D 3.2, пол на 0.55, кровля конёк вдоль Y)
# =========================================================================================
WH_W, WH_D, WH_ZP = 4.2, 3.2, 0.55
WH_YF = -WH_D / 2                         # лицо фасадной стены


def _wh_base(a):
    stone_base(a, WH_W + 0.30, WH_D + 0.30, h=0.22)
    a.add(p_box((WH_W, WH_D, WH_ZP - 0.21), loc=(0, 0, (WH_ZP + 0.21) / 2), bevel=0.05),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))


def _sack(a, x, y, z=0.0, rz=0.0):
    fr = Frame((x, y, z), rz=rz)
    fr.ico(a, 0.20, loc=(0, 0, 0.16), scl=(1.1, 0.95, 1.0), col="burlap", cut=-0.7)
    fr.cyl(a, 0.09, 0.05, 0.10, 6, loc=(0, 0, 0.34), col="burlap_dark")


def _dock(a, x0, x1, y0, z, steps_x=0.0, steps_w=1.10, n=2):
    """Погрузочная площадка вдоль фасада: настил на каменных столбах вровень с полом склада, в середине —
    каменные ступени к воротам."""
    y1 = WH_YF
    a.add(p_box((x1 - x0, y1 - y0, 0.12), loc=((x0 + x1) / 2, (y0 + y1) / 2, z - 0.06), bevel=0.03), PLANKS)
    a.add(p_box((x1 - x0 + 0.04, 0.14, 0.16), loc=((x0 + x1) / 2, y0 + 0.05, z - 0.11), bevel=0.0), BEAM)
    for x in (x0 + 0.16, x1 - 0.16) + tuple(xx for xx in ((x0 + steps_x - steps_w / 2) / 2,
                                                         (x1 + steps_x + steps_w / 2) / 2)):
        a.add(p_box((0.24, 0.24, z - 0.10), loc=(x, y0 + 0.18, (z - 0.10) / 2), bevel=0.0), "stone_mid")
    for k in range(n):
        hz = z * (n - k) / (n + 1)
        a.add(p_box((steps_w, 0.27, hz + 0.02), loc=(steps_x, y0 - 0.135 - k * 0.27, hz / 2 - 0.01), bevel=0.03),
              by_normal("stone_light", "stone_mid", "stone_mid", 0.8))


def _barred_window(a, x, z, w=0.38, h=0.40):
    """Оконце склада в кладке: тёмный проём, железные прутья, каменная рама."""
    fr = Frame((x, WH_YF - 0.01, z))
    fr.box(a, (w + 0.16, 0.08, h + 0.16), (0, 0, 0), col="stone_light", bevel=0.0)
    fr.box(a, (w, 0.06, h), (0, -0.02, 0), col="black", bevel=0.0)
    for k in (-1, 0, 1):
        fr.box(a, (0.04, 0.04, h), (k * w / 3.2, -0.06, 0), col="iron_dark", bevel=0.0)


def _hoist(a, y_wall, z_beam, reach=0.62, z_load=1.70, load="crate"):
    """Подъёмная балка над воротами чердака: брус из фронтона, подкос, блок, трос и груз."""
    yb = y_wall - reach / 2
    a.add(p_box((0.16, reach + 0.30, 0.16), loc=(0, yb + 0.10, z_beam), bevel=0.0), BEAM)
    a.add(p_box((0.12, 0.12, 0.62), loc=(0, y_wall - 0.20, z_beam - 0.26), rot=(-40, 0, 0), bevel=0.0), BEAM)
    ye = y_wall - reach + 0.06
    a.add(p_cyl(0.11, 0.11, 0.08, 10, loc=(-0.04, ye, z_beam - 0.16), rot=(0, 90, 0)), "iron_dark")
    a.add(p_box((0.03, 0.03, z_beam - 0.16 - z_load - 0.40), loc=(0, ye - 0.11, (z_beam - 0.16 + z_load + 0.40) / 2),
                bevel=0.0), "rope")
    if load == "crate":
        crate(a, (0.0, ye - 0.11, z_load), s=0.40, rot=(0, 0, 12))
    else:
        _sack(a, 0.0, ye - 0.11, z_load)


def _wh_l2(a):
    """Каменный амбар: низ стен — кладка до 1.95 (угловые квадры, оконца с решёткой), верх — дощатый на
    стойках, стены выше на 0.5; кровля прежняя по форме, но выше; во фронтоне — дверь чердака с подъёмной
    балкой и ящиком на тросе; перед воротами — погрузочная площадка со ступенями, на ней груз."""
    _wh_base(a)
    W, D, ZP = WH_W, WH_D, WH_ZP
    zs, zt = 1.95, 2.85
    t = 0.30
    a.add(p_box((W, t, zs - ZP), loc=(0, D / 2 - t / 2, (ZP + zs) / 2), bevel=0.04), WALL)
    for sx in (-1, 1):
        a.add(p_box((t, D, zs - ZP), loc=(sx * (W / 2 - t / 2), 0, (ZP + zs) / 2), bevel=0.04), WALL)
    a.add(p_box((W, t, zs - ZP), loc=(0, WH_YF + t / 2, (ZP + zs) / 2), bevel=0.04), WALL)
    quoins(a, 0, 0, W, D, ZP, zs, corners=((-1, -1), (1, -1), (1, 1)))
    # верх: доски между стойками, пояс по кладке и обвязка
    a.add(p_box((W - 0.08, D - 0.08, zt - zs), loc=(0, 0, (zs + zt) / 2), bevel=0.03), "wood_mid")
    cornice(a, W, D, zs + 0.02, t=0.16, col=BEAM)
    cornice(a, W, D, zt - 0.06, t=0.16, col=BEAM)
    for x in (-W / 2, -1.10, 1.10, W / 2):
        a.add(p_box((0.18, 0.18, zt - zs), loc=(x, WH_YF + 0.02, (zs + zt) / 2), bevel=0.0), BEAM)
    for x in (-W / 2, W / 2):
        a.add(p_box((0.18, 0.18, zt - zs), loc=(x, D / 2 - 0.02, (zs + zt) / 2), bevel=0.0), BEAM)
    for y in (-0.55, 0.55):
        a.add(p_box((0.18, 0.18, zt - zs), loc=(W / 2 + 0.02, y, (zs + zt) / 2), bevel=0.0), BEAM)
    gate(a, y=WH_YF, z0=ZP, w=1.60, h=1.50)
    for sx in (-1, 1):
        _barred_window(a, sx * 1.48, 1.32)
    zr = gable_roof(a, W, D, zt, pitch_deg=36, ox=0.42, oy=0.40, bands=3)
    gable_front(a, 0.0, WH_YF + 0.15, W, zt - 0.02, zr, depth=0.30, face=-1)
    gable_front(a, 0.0, D / 2 - 0.15, W, zt - 0.02, zr, depth=0.30, face=1, boards=False)
    plank_door(a, WH_YF + 0.10, zt + 0.08, w=0.72, h=0.78)
    _hoist(a, WH_YF, zr - 0.62, reach=0.66, z_load=2.06)

    _dock(a, -1.95, 1.95, -2.20, ZP)
    crate(a, (-1.62, -1.98, ZP), s=0.42, rot=(0, 0, 6))
    crate(a, (-1.16, -1.94, ZP), s=0.42, rot=(0, 0, -8))
    crate(a, (-1.40, -1.96, ZP + 0.42), s=0.42, rot=(0, 0, 18))
    barrel(a, (1.22, -1.92, ZP), r=0.22, h=0.48)
    barrel(a, (1.66, -1.98, ZP), r=0.22, h=0.48)
    _sack(a, 1.44, -2.14, ZP + 0.0)
    _sack(a, -0.80, -2.12, ZP)


def _jib_crane(a, x, y, h=3.0, boom=1.45, z_load=1.40):
    """Поворотный кран у площадки: мачта на каменной тумбе, укосина к площадке, подкос, блок, трос, ящик."""
    a.add(p_box((0.44, 0.44, 0.30), loc=(x, y, 0.13), bevel=0.03), "stone_mid")
    a.add(p_box((0.20, 0.20, h), loc=(x, y, 0.28 + h / 2), bevel=0.03), BEAM)
    zt = 0.28 + h
    a.add(p_box((boom + 0.30, 0.16, 0.16), loc=(x - boom / 2 + 0.05, y, zt - 0.14), bevel=0.0), BEAM)
    dx, dz = boom * 0.62, 0.95
    ln = math.hypot(dx, dz)
    a.add(p_box((0.12, 0.12, ln), loc=(x - dx / 2, y, zt - 0.20 - dz / 2), rot=(0, -math.degrees(math.atan2(dx, dz)), 0),
                bevel=0.0), BEAM)
    a.add(p_cyl(0.12, 0.12, 0.08, 10, loc=(x - boom + 0.04, y - 0.04, zt - 0.32), rot=(90, 0, 0)), "iron_dark")
    xl = x - boom - 0.06
    a.add(p_box((0.03, 0.03, zt - 0.32 - z_load - 0.40), loc=(xl, y, (zt - 0.32 + z_load + 0.40) / 2), bevel=0.0),
          "rope")
    crate(a, (xl, y, z_load), s=0.40, rot=(0, 0, -10))
    a.add(p_box((0.30, 0.30, 0.30), loc=(x, y, zt + 0.02), bevel=0.0), "iron_dark")


def _wh_l3(a):
    """Пакгауз: два этажа под кровлей той же формы, что у склада, только выше. Низ — кладка с аркой ворот
    (внутри видны ящики), оконца с решёткой; верх — фахверк по штукатурке на выпуске, окна со светом,
    чердачная дверь во фронтоне с подъёмной балкой; на коньке — вентиляционная башенка с жалюзи; справа —
    навес на столбах с бочками; у площадки — поворотный кран с ящиком."""
    _wh_base(a)
    W, D, ZP = WH_W, WH_D, WH_ZP
    zg, zu = 2.30, 3.42
    t = 0.30
    a.add(p_box((W, t, zg - ZP), loc=(0, D / 2 - t / 2, (ZP + zg) / 2), bevel=0.04), WALL)
    for sx in (-1, 1):
        a.add(p_box((t, D, zg - ZP), loc=(sx * (W / 2 - t / 2), 0, (ZP + zg) / 2), bevel=0.04), WALL)
    arched_wall(a, -W / 2, W / 2, WH_YF + t / 2, t, ZP, zg, -0.76, 0.76, 1.40)
    quoins(a, 0, 0, W, D, ZP, zg, corners=((-1, -1), (1, -1)))
    for sx in (-1, 1):
        _barred_window(a, sx * 1.52, 1.40)
    crate(a, (-0.34, WH_YF + 0.62, ZP), s=0.46, rot=(0, 0, 8))
    crate(a, (0.30, WH_YF + 0.80, ZP), s=0.46, rot=(0, 0, -6))
    crate(a, (-0.10, WH_YF + 0.74, ZP + 0.46), s=0.44, rot=(0, 0, 20))
    # второй этаж: выпуск вперёд на 0.12, штукатурка и фахверк
    x0, x1, y0, y1 = -W / 2 - 0.04, W / 2 + 0.04, WH_YF - 0.12, D / 2 + 0.04
    yc = (y0 + y1) / 2
    a.add(p_box((x1 - x0, y1 - y0, zu - zg), loc=(0, yc, (zg + zu) / 2), bevel=0.03), PLASTER)
    a.add(p_box((x1 - x0 + 0.10, 0.22, 0.20), loc=(0, y0 + 0.03, zg + 0.04), bevel=0.0), BEAM)
    for k in (-1.70, -0.60, 0.60, 1.70):
        a.add(p_box((0.13, 0.30, 0.13), loc=(k, WH_YF - 0.05, zg - 0.10), rot=(-35, 0, 0), bevel=0.0), BEAM)
    L = x1 - x0
    wz = 2.92
    fachwerk(a, Frame((0, y0, 0)), L - 0.06, zg + 0.12, zu, posts=(-1.62, -0.52, 0.52, 1.62),
             rails=(wz - 0.30,), braces=((-2.08, zg + 0.18, -1.68, wz - 0.36), (2.08, zg + 0.18, 1.68, wz - 0.36)))
    for x in (-1.08, 1.08):
        win(a, Frame((x, y0 - 0.02, wz + 0.08)), w=0.44, h=0.46, lit=True, shutters="roof_dark", sill=None)
    plank_door(a, y0 + 0.10, zg + 0.18, w=0.70, h=1.00)
    fachwerk(a, Frame((x1, yc, 0), rz=90), y1 - y0 - 0.06, zg + 0.12, zu, posts=(-0.55, 0.55), rails=(wz - 0.30,))
    # кровля той же формы, что у склада, на два этажа выше; фронтон — фахверк
    zr = gable_roof(Shift(a, (0, yc, 0)), L, y1 - y0, zu, pitch_deg=36, ox=0.34, oy=0.32, bands=3)
    gf = Shift(a, (0, yc, 0))
    gable_front(gf, 0.0, -(y1 - y0) / 2 + 0.10, L, zu - 0.02, zr, col=PLASTER, depth=0.22, face=-1, boards=False)
    gable_front(gf, 0.0, (y1 - y0) / 2 - 0.10, L, zu - 0.02, zr, col=PLASTER, depth=0.22, face=1, boards=False)
    yg = y0 - 0.01
    a.add(p_box((L - 0.40, 0.12, 0.12), loc=(0, yg, zu + 0.40), bevel=0.0), BEAM)
    hg = (zr - zu) * 0.62
    for x in (-0.80, 0.80):
        a.add(p_box((0.12, 0.12, hg), loc=(x, yg + 0.012, zu + hg / 2), bevel=0.0), BEAM)
    for sx in (-1, 1):
        a.add(p_box((0.10, 0.10, 0.62), loc=(sx * 1.34, yg + 0.024, zu + 0.20), rot=(0, sx * 45, 0), bevel=0.0),
              BEAM)
    win(a, Frame((0, yg - 0.02, zu + 0.86)), w=0.40, h=0.40, lit=True, shutters=None, sill=None)
    _hoist(a, y0, zr - 0.46, reach=0.78, z_load=2.62, load="sack")
    cupola(a, 0.0, 0.10, zr, s=0.70, h=0.54, roof_h=0.66)
    # навес справа: односкатный, на столбах, под ним бочки
    sx0, sx1 = W / 2 + 0.04, W / 2 + 0.46
    for y in (-1.25, 1.25):
        porch_post(a, sx1 - 0.07, y, ZP - 0.33, 1.88, s=0.13)
    a.add(p_box((0.62, 2.80, 0.10), loc=((sx0 + sx1) / 2 - 0.02, 0, ZP - 0.30), bevel=0.0), "stone_mid")
    shed_roof(Shift(a, ((sx0 + sx1) / 2, 0, 0), rz=90), 0, 0, 2.80, sx1 - sx0, 1.92, 1.60, face=-1, ox=0.10,
              oy=0.10)
    for y in (-0.80, -0.30, 0.20, 0.70):
        barrel(a, (sx0 + 0.20, y, ZP - 0.25), r=0.18, h=0.42)

    _dock(a, -2.05, 1.70, -2.26, ZP, steps_x=-0.10)
    _jib_crane(a, 2.30, -2.20, h=2.95, boom=1.40, z_load=1.30)
    crate(a, (-1.74, -2.06, ZP), s=0.42, rot=(0, 0, 6))
    crate(a, (-1.30, -2.04, ZP), s=0.42, rot=(0, 0, -8))
    crate(a, (-1.52, -2.06, ZP + 0.42), s=0.42, rot=(0, 0, 18))
    barrel(a, (0.86, -2.00, ZP), r=0.22, h=0.48)
    barrel(a, (1.30, -2.02, ZP), r=0.22, h=0.48)
    barrel(a, (1.08, -2.02, ZP + 0.48), r=0.20, h=0.44, rot=(0, 90, 0))
    _sack(a, -0.86, -2.14, ZP)
    for sx in (-1, 1):
        wall_lamp(a, sx * 1.02, WH_YF - 0.02, 1.95, out=(sx * 0.3, -0.95))


def warehouse_evolve(a, level):
    """2: каменный амбар с площадкой. 3: двухэтажный пакгауз с краном."""
    (_wh_l2 if level == 2 else _wh_l3)(a)


EVOLVE = {"Bld_Barracks": barracks_evolve, "Bld_Warehouse": warehouse_evolve}
