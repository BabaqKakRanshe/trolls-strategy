"""
Ресурсы Vitaria второй и третьей очереди: модели категории Resources (Kit_Resources в .blend, FBX в
Unity/Assets/Vitaria/Models/Resources) и их иконки (render_icons.py берёт меши кита по имени).

Вторая очередь: уголь, золотые самородки, солома, золотая пшеница, мясо, молоко, железный лом, пир.
Сырьё, у которого раньше была только модель иконки (vitaria_icons.py): сноп пшеницы, шкура, кожа, друза
кристаллов — та же функция строит и иконку, и модель.
Третья очередь: мука, хлеб, сыр, эль, шерсть, ткань, сталь (слиток и стопка).
Снаряжение (мечи, топор, копьё, лук, молот, брони, шлем, щиты и их зачарованные версии) —
vitaria_gear.py, в RESOURCES оно входит отсюда.

Соглашения кита: 1 юнит = 1 м, пивот внизу по центру, ничего ниже z = 0, лицом к -Y (+Z Unity),
одна грань — один swatch палитры, верх светлее боков. Новые цвета — только в конец PALETTE
(build_vitaria.py). Размеры — как у прежних ресурсов кита (слиток 0.3 м, куча руды 1 м, доски 1 м).

Добавить в .blend и выгрузить FBX:   add_buildings.py (вместе со зданиями; --only Res_Flour,... — только эти)
Полная пересборка набора:            build_vitaria.py подхватывает RESOURCES сам (ext_resources()).
"""
import math
import random

import bmesh
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, p_prism, p_taper_box, by_normal
from vitaria_buildings.common import Frame, crystal
import build_vitaria as V
import vitaria_gear as VG


def floor_cut(a):
    """Подрезать всё, что ушло ниже z = 0 (у валунов и комков кита так же: низ по мировому нулю)."""
    bm = a.bm
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0),
                           plane_no=(0, 0, 1), clear_inner=True)


# ---------------------------------------------------------------------------------------------
# уголь: горка чёрных гранёных комков с серыми бликами на верхних гранях
# ---------------------------------------------------------------------------------------------
def _coal_col(f):
    n = f.normal
    if n.z > 0.80:
        return "rail"                 # блик: без него комки на тёмной панели HUD сливаются в пятно
    if n.z > 0.25:
        return "iron_dark"
    return "coal" if n.z > -0.4 else "black"


def coal(a):
    rng = random.Random(41)
    lumps = [(0.00, 0.02, 0.16, 0.17), (-0.20, 0.06, 0.08, 0.13), (0.19, 0.08, 0.08, 0.13), (0.05, -0.18, 0.07, 0.12),
             (-0.13, -0.15, 0.06, 0.11), (0.02, 0.21, 0.07, 0.12), (0.24, -0.11, 0.05, 0.10),
             (-0.26, -0.06, 0.05, 0.09), (0.10, -0.02, 0.27, 0.10)]
    for x, y, z, r in lumps:
        a.add(p_ico(r, 1, loc=(x, y, z), scl=(1.0, rng.uniform(0.8, 0.95), rng.uniform(0.72, 0.86)), jitter=0.26,
                    rng=rng, rot=(rng.uniform(0, 40), rng.uniform(0, 40), rng.uniform(0, 360))), _coal_col)
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# золотые самородки: три-четыре бугристых комка чистого золота (не руда: камня нет)
# ---------------------------------------------------------------------------------------------
def gold_nugget(a):
    rng = random.Random(52)
    gold = by_normal("gold_light", "gold", "gold_dark", 0.45)
    # крупные округлые комки (jitter умеренный: острые иглы читались крошкой и хлопьями)
    for x, y, z, r, sc in ((0.0, 0.0, 0.10, 0.15, (1.2, 1.0, 0.78)), (-0.20, 0.08, 0.07, 0.10, (1.1, 0.95, 0.82)),
                           (0.19, 0.08, 0.06, 0.09, (1.0, 1.1, 0.8)), (0.07, -0.18, 0.05, 0.08, (1.2, 0.9, 0.8))):
        a.add(p_ico(r, 1, loc=(x, y, z), scl=sc, jitter=0.16, rng=rng, rot=(0, 0, rng.uniform(0, 360))), gold)
        # пара наплывов на боках — самородок, а не галька
        for k in range(2):
            d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.3, 1.0))).normalized()
            p = Vector((x, y, z)) + Vector((d.x * sc[0], d.y * sc[1], d.z * sc[2])) * r * 0.8
            a.add(p_ico(r * rng.uniform(0.38, 0.5), 1, loc=p, jitter=0.12, rng=rng), "ore_gold")
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# солома: тюк, перехваченный двумя верёвками, торчащие соломины на торцах
# ---------------------------------------------------------------------------------------------
def straw(a):
    rng = random.Random(63)
    L, Wd, H = 0.62, 0.42, 0.34
    a.add(p_box((L, Wd, H), loc=(0, 0, H / 2), bevel=0.07),
          lambda f: "thatch_light" if f.normal.z > 0.6 else ("wheat_light" if abs(f.normal.x) > 0.6 else "thatch"))
    # слои прессовки: тёмные борозды вдоль длинных сторон — тюк соломы, а не ящик
    for z in (0.10, 0.18, 0.26):
        for sy in (-1, 1):
            a.add(p_box((L - 0.10, 0.02, 0.025), loc=(0, sy * (Wd / 2 + 0.002), z), bevel=0.0), "thatch_dark")
    for x in (-0.16, 0.16):
        a.add(p_box((0.05, Wd + 0.03, H + 0.03), loc=(x, 0, H / 2), bevel=0.0), "rope")
    # пучки на торцах: тюк, а не ящик (соломины сверху давали чёрную щель тени)
    for sx in (-1, 1):
        for k in range(6):
            y, z = rng.uniform(-0.15, 0.15), rng.uniform(0.06, H - 0.06)
            a.add(p_box((0.13, 0.035, 0.035), loc=(sx * (L / 2 + 0.02), y, z),
                        rot=(rng.uniform(-30, 30), rng.uniform(-25, 25), rng.uniform(-25, 25)), bevel=0.0),
                  rng.choice(("wheat_light", "thatch_light", "wheat")))
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# золотая пшеница: сноп, как у иконки wheat, но плотнее, колосья крупнее и золотые,
# перевязь синей лентой с бантом — с обычной пшеницей не спутать ни цветом, ни силуэтом
# ---------------------------------------------------------------------------------------------
def golden_wheat(a):
    rng = random.Random(74)
    n = 15
    for k in range(n):
        tilt_x = rng.uniform(-18, 18)
        tilt_y = (k - (n - 1) / 2) * 3.4 + rng.uniform(-3, 3)
        fr = Frame((rng.uniform(-0.04, 0.04), rng.uniform(-0.035, 0.035), 0.30), rot=(tilt_x, tilt_y, 0))
        fr.cyl(a, 0.02, 0.017, 0.36, 5, loc=(0, 0, 0.0), col="gold")
        fr.ico(a, 0.052, loc=(0, 0, 0.44), scl=(1.0, 0.85, 2.3), col="ore_gold")
        fr.ico(a, 0.036, loc=(0, 0, 0.535), scl=(1.0, 0.85, 1.6), col="gold_light")
        low = Frame((rng.uniform(-0.035, 0.035), rng.uniform(-0.035, 0.035), 0.30),
                    rot=(-tilt_x * 0.5, 180 - tilt_y * 0.6, 0))
        low.cyl(a, 0.02, 0.02, 0.30, 5, loc=(0, 0, 0.0), col="gold_dark")
    a.add(p_cyl(0.098, 0.098, 0.08, 10, loc=(0, 0, 0.26)), "roof")
    a.add(p_cyl(0.10, 0.10, 0.02, 10, loc=(0, 0, 0.29)), "roof_light")
    # бант спереди: две петли и хвосты
    for sx in (-1, 1):
        a.add(p_box((0.09, 0.04, 0.07), loc=(sx * 0.06, -0.11, 0.31), rot=(0, sx * 25, 0), bevel=0.0), "roof")
        a.add(p_box((0.035, 0.03, 0.12), loc=(sx * 0.035, -0.11, 0.21), rot=(0, sx * 18, 0), bevel=0.0), "roof_dark")
    a.add(p_box((0.04, 0.05, 0.05), loc=(0, -0.115, 0.30), bevel=0.0), "roof_light")
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# мясо: окорок — румяная грушевидная мякоть, светлая кость с двумя мослами
# ---------------------------------------------------------------------------------------------
def meat(a):
    rng = random.Random(85)
    roast = by_normal("tile_light", "tile", "tile_dark", 0.35)
    a.add(p_ico(0.20, 2, loc=(0.06, 0, 0.15), scl=(1.25, 0.95, 0.80), jitter=0.04, rng=rng), roast)
    a.add(p_cyl(0.15, 0.075, 0.24, 9, loc=(-0.10, 0, 0.15), rot=(0, -90, 0)), roast)
    # жирная кайма у кости
    a.add(p_cyl(0.085, 0.085, 0.05, 9, loc=(-0.30, 0, 0.15), rot=(0, -90, 0)), "pig")
    a.add(p_cyl(0.045, 0.045, 0.16, 7, loc=(-0.33, 0, 0.15), rot=(0, -90, 0)), "cream")
    for dy in (-0.045, 0.045):
        a.add(p_ico(0.05, 1, loc=(-0.50, dy, 0.15)), by_normal("wool", "cream"))
    # блик-глазурь сверху: тёплое пятно, как у жареного
    a.add(p_ico(0.07, 1, loc=(0.10, -0.04, 0.29), scl=(1.6, 1.1, 0.25)), "tile_light")
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# молоко: голубой кувшин с белыми поясами, полный до края, капля по носику
# ---------------------------------------------------------------------------------------------
def milk(a):
    jug = by_normal("roof_light", "roof_light", "roof", 0.6)
    a.add(p_cyl(0.13, 0.17, 0.14, 12, loc=(0, 0, 0.0), bevel=0.015), jug)
    a.add(p_cyl(0.17, 0.15, 0.12, 12, loc=(0, 0, 0.14)), jug)
    a.add(p_cyl(0.15, 0.105, 0.10, 12, loc=(0, 0, 0.26)), jug)
    a.add(p_cyl(0.105, 0.125, 0.07, 12, loc=(0, 0, 0.36)), jug)
    for z, r in ((0.12, 0.172), (0.38, 0.122)):
        a.add(p_cyl(r, r, 0.035, 12, loc=(0, 0, z)), "cream")
    a.add(p_cyl(0.108, 0.108, 0.012, 12, loc=(0, 0, 0.428)), "flower_w")           # молоко вровень с краем
    # носик к -X, по нему капля
    a.add(p_prism([(-0.10, 0.36), (-0.19, 0.43), (-0.16, 0.44), (-0.08, 0.43)], 0.08, loc=(0, 0, 0)), "roof_light")
    a.add(p_box((0.05, 0.045, 0.10), loc=(-0.17, 0, 0.38), bevel=0.0), "flower_w")
    a.add(p_ico(0.03, 1, loc=(-0.17, 0, 0.32), scl=(1, 1, 1.3)), "flower_w")
    # ручка-скоба к +X
    a.add(p_box((0.05, 0.05, 0.22), loc=(0.20, 0, 0.25), bevel=0.0), "roof_light")
    for z in (0.15, 0.35):
        a.add(p_box((0.08, 0.05, 0.05), loc=(0.165, 0, z), bevel=0.0), "roof_light")
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# железный лом: горка гнутых прутков, пластин, шестерня и обод, рыжие пятна ржавчины
# ---------------------------------------------------------------------------------------------
def _bent_bar(a, fr, L1, L2, ang, t=0.06, col="iron"):
    """Гнутый пруток в раме fr: первое колено вдоль X, второе под углом ang вверх."""
    fr.box(a, (L1, t, t), (L1 / 2, 0, 0), col=col, bevel=0.0)
    s = fr.sub((L1, 0, 0), rot=(0, -ang, 0))
    s.box(a, (L2, t, t), (L2 / 2 - t / 2, 0, 0), col=col, bevel=0.0)


def scrap(a):
    rng = random.Random(96)
    a.add(p_ico(0.30, 1, loc=(0, 0, 0), scl=(1.15, 0.95, 0.42), jitter=0.12, rng=rng),
          by_normal("iron_dark", "rail", "iron_dark", 0.4))
    # пластины: гнутый лист и обрезок, у листа рыжая кромка
    p1 = Frame((-0.12, -0.04, 0.10), rot=(18, -22, 30))
    p1.box(a, (0.30, 0.20, 0.035), (0, 0, 0), col=by_normal("iron_light", "iron"), bevel=0.0)
    p1.box(a, (0.18, 0.20, 0.035), (0.20, 0, 0.05), rot=(0, -30, 0), col=by_normal("iron_light", "iron"), bevel=0.0)
    p1.box(a, (0.08, 0.14, 0.04), (-0.08, 0.03, 0.005), col="iron_ore_vein", bevel=0.0)
    p2 = Frame((0.16, 0.12, 0.08), rot=(-14, 10, -40))
    p2.box(a, (0.22, 0.16, 0.035), (0, 0, 0), col="iron", bevel=0.0)
    p2.box(a, (0.07, 0.07, 0.04), (0.05, -0.03, 0.004), col="copper_dark", bevel=0.0)
    # прутки: коленом вверх, торчат из кучи
    _bent_bar(a, Frame((-0.30, 0.10, 0.06), rot=(0, -10, -15)), 0.30, 0.22, 55, col="iron_dark")
    _bent_bar(a, Frame((0.05, -0.20, 0.06), rot=(0, -5, 35)), 0.26, 0.18, 70, col="iron")
    _bent_bar(a, Frame((0.12, 0.20, 0.10), rot=(0, 0, 160)), 0.24, 0.16, 40, col="iron_light")
    # шестерня, прислонённая к куче
    g = Frame((0.20, -0.10, 0.14), rot=(70, 0, 25))
    g.cyl(a, 0.10, 0.10, 0.045, 10, loc=(0, 0, -0.02), col=by_normal("iron_light", "iron"))
    for k in range(8):
        t = math.radians(45 * k)
        g.box(a, (0.05, 0.05, 0.045), (math.cos(t) * 0.115, math.sin(t) * 0.115, 0.0025), rot=(0, 0, 45 * k),
              col="iron", bevel=0.0)
    g.cyl(a, 0.035, 0.035, 0.05, 6, loc=(0, 0, 0.0), col="iron_dark")
    # обод-кольцо (гнутая полоса) у левого края
    ring = Frame((-0.26, -0.12, 0.03), rot=(0, 15, 0))
    for k in range(7):
        t = math.radians(-20 + 32 * k)
        ring.box(a, (0.09, 0.05, 0.04), (math.cos(t) * 0.13, math.sin(t) * 0.13, 0.0), rot=(0, 0, math.degrees(t) + 90),
                 col="rail" if k % 2 else "iron_dark", bevel=0.0)
    # ржавые пятна на куче
    for k in range(5):
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0.08, 0.26)
        a.add(p_ico(0.05, 1, loc=(math.cos(ang) * d, math.sin(ang) * d * 0.8, 0.10 - d * 0.25), scl=(1.3, 1.0, 0.4)),
              rng.choice(("iron_ore_vein", "copper_dark")))
    floor_cut(a)


# ---------------------------------------------------------------------------------------------
# пир: оловянное блюдо, на нём румяная птица с ножками, каравай; рядом кружка с пеной
# ---------------------------------------------------------------------------------------------
def feast(a):
    rng = random.Random(107)
    plate = Frame((0, 0, 0), s=(1.25, 0.95, 1.0))
    plate.cyl(a, 0.33, 0.30, 0.04, 16, col=by_normal("iron_light", "iron", "iron_dark", 0.6))
    plate.cyl(a, 0.27, 0.27, 0.012, 16, loc=(0, 0, 0.036), col="steel")
    roast = by_normal("copper_light", "leather_light", "leather", 0.35)
    # птица: тушка, две ножки с косточками
    b = Frame((-0.06, 0.04, 0.05), rz=-20)
    b.ico(a, 0.15, loc=(0, 0, 0.09), scl=(1.25, 1.0, 0.72), col=roast, jitter=0.05, rng=rng, sub=2)
    for sy in (-1, 1):
        lg = b.sub((0.12, sy * 0.10, 0.07), rot=(0, -20, sy * 25))
        lg.ico(a, 0.065, loc=(0.04, 0, 0.0), scl=(1.4, 1.0, 1.0), col=roast)
        lg.cyl(a, 0.02, 0.02, 0.10, 6, loc=(0.08, 0, 0.0), rot=(0, 90, 0), col="cream")
        lg.ico(a, 0.028, loc=(0.185, 0, 0.0), col="wool")
    b.ico(a, 0.06, loc=(-0.04, -0.06, 0.18), scl=(1.6, 1.0, 0.3), col="copper_light")     # блик глазури
    # каравай с надрезами
    br = Frame((0.20, -0.10, 0.045), rz=25)
    br.ico(a, 0.10, loc=(0, 0, 0.03), scl=(1.35, 0.9, 0.62), col=by_normal("wood_yellow", "wood_light", "wood_mid", 0.4),
           cut=-0.02, sub=2)
    for x in (-0.05, 0.03):
        br.box(a, (0.025, 0.12, 0.02), (x, 0, 0.085), rot=(0, 0, 20), col="wheat_light", bevel=0.0)
    # яблоко сбоку блюда
    a.add(p_ico(0.05, 1, loc=(0.16, 0.15, 0.09)), "berry")
    a.add(p_box((0.04, 0.02, 0.025), loc=(0.17, 0.15, 0.145), rot=(0, 0, 30), bevel=0.0), "leaf_mid")
    # кружка с пеной на столе рядом с блюдом
    m = Frame((-0.36, -0.20, 0.0))
    m.cyl(a, 0.075, 0.085, 0.20, 10, col="wood_light", bevel=0.01)
    for z in (0.04, 0.15):
        m.cyl(a, 0.09, 0.09, 0.03, 10, loc=(0, 0, z), col="iron_dark")
    m.cyl(a, 0.092, 0.08, 0.05, 10, loc=(0, 0, 0.195), col="cream")
    m.ico(a, 0.06, loc=(0.02, 0.0, 0.25), scl=(1.2, 1.0, 0.5), col="flower_w")
    m.box(a, (0.05, 0.04, 0.12), (-0.11, 0, 0.11), col="wood_mid", bevel=0.0)
    for z in (0.06, 0.16):
        m.box(a, (0.05, 0.04, 0.03), (-0.09, 0, z), col="wood_mid", bevel=0.0)
    floor_cut(a)




# =============================================================================================
# сырьё, у которого раньше была только модель иконки (перенесено из vitaria_icons.py без изменений:
# иконки wheat, animal-hide, leather, violet-crystal те же)
# =============================================================================================
def wheat_sheaf(a):
    """Сноп: стебли веером из перевязи, колосья сверху, срез снизу."""
    rng = random.Random(5)
    for k in range(13):
        tilt_x = rng.uniform(-16, 16)
        tilt_y = (k - 6) * 3.4 + rng.uniform(-3, 3)
        fr = Frame((rng.uniform(-0.035, 0.035), rng.uniform(-0.03, 0.03), 0.3), rot=(tilt_x, tilt_y, 0))
        fr.cyl(a, 0.016, 0.014, 0.36, 5, loc=(0, 0, 0.0), col="wheat_dark")            # верх стебля
        fr.ico(a, 0.042, loc=(0, 0, 0.43), scl=(1.0, 0.8, 2.3), col="wheat", sub=1)      # колос
        fr.ico(a, 0.03, loc=(0, 0, 0.51), scl=(1.0, 0.8, 1.6), col="wheat_light", sub=1)
        low = Frame((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), 0.3), rot=(-tilt_x * 0.5, 180 - tilt_y * 0.6, 0))
        low.cyl(a, 0.016, 0.016, 0.3, 5, loc=(0, 0, 0.0), col="wheat_dark")              # низ стебля
    a.add(p_cyl(0.085, 0.085, 0.07, 10, loc=(0, 0, 0.27)), "rope")                       # перевязь
    a.add(p_cyl(0.088, 0.088, 0.02, 10, loc=(0, 0, 0.3)), "burlap_dark")


def _hide_outline(n=44):
    pts = []
    for k in range(n):
        t = math.tau * k / n
        r = 1.0
        for c, w, amp in ((math.radians(38), 0.17, 0.75), (math.radians(142), 0.17, 0.75), (math.radians(222), 0.18, 0.85),
                          (math.radians(318), 0.18, 0.85), (math.pi / 2, 0.2, 0.35), (3 * math.pi / 2, 0.1, 0.45)):
            d = math.atan2(math.sin(t - c), math.cos(t - c))
            r += amp * math.exp(-(d / w) ** 2)
        pts.append((math.cos(t) * 0.22 * r, math.sin(t) * 0.3 * r))
    return pts


def animal_hide(a, tilt=0.0, z=0.0175):
    """Распластанная шкура: контур с четырьмя лапами, шеей и хвостом; светлое брюхо.
    Модель кита лежит на земле (tilt 0); в иконке шкура приподнята к зрителю — tilt 62, z 0.25,
    иначе в кадре сверху одна щель."""
    outer = _hide_outline()
    fr = Frame((0, 0, z), rot=(tilt, 0, 0))
    face_n = Vector((0.0, -math.sin(math.radians(tilt)), math.cos(math.radians(tilt))))
    fr.prism(a, [(x, y) for x, y in outer], 0.035, loc=(0, 0, 0), rot=(90, 0, 0),
             col=lambda f: "hide" if abs(f.normal.dot(face_n)) > 0.8 else "hide_dark")
    inner = [(x * 0.55, y * 0.6) for x, y in outer]
    fr.prism(a, inner, 0.02, loc=(0, 0, 0.022), rot=(90, 0, 0), col="hide_light")
    for sx in (-1, 1):                                         # пятна
        fr.prism(a, [(x * 0.14 + sx * 0.13, y * 0.12 - 0.12) for x, y in outer], 0.02, loc=(0, 0, 0.026),
                 rot=(90, 0, 0), col="hide_dark")


def leather(a):
    """Кожа: стопка из трёх выделанных листов, перехваченная ремнём с медной пряжкой.
    Прежние два свёртка на мелких размерах читались брёвнами — плоская стопка другой формы,
    а тёплый рыжий верх отделяет её от коры и досок."""
    top = by_normal("leather_light", "leather", "leather_dark", 0.5)
    rng = random.Random(9)
    for k, (w, d, rz) in enumerate(((0.62, 0.48, 4), (0.6, 0.46, -5), (0.58, 0.45, 3))):
        z = 0.03 + k * 0.058
        a.add(p_box((w, d, 0.05), loc=(rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02), z),
                    rot=(0, 0, rz), bevel=0.018), top)
        # загнутый угол верхнего листа — «кожа», а не доска
    a.add(p_prism([(0.0, 0.0), (0.16, 0.0), (0.0, 0.14)], 0.02, loc=(0.2, -0.2, 0.2), rot=(90, 0, 18)), "hide_light")
    # стежок по кромке верхнего листа: светлый пунктир
    for k in range(6):
        a.add(p_box((0.05, 0.012, 0.008), loc=(-0.2 + k * 0.08, -0.205, 0.2), rot=(0, 0, 3), bevel=0.0), "hide_light")
    # ремень поперёк стопки и медная пряжка на фасаде
    a.add(p_box((0.08, 0.52, 0.215), loc=(0.05, 0.0, 0.11), rot=(0, 0, 3), bevel=0.012), "wood_dark")
    a.add(p_box((0.13, 0.03, 0.1), loc=(0.05, -0.255, 0.12), rot=(0, 0, 3), bevel=0.01), "copper")
    a.add(p_box((0.07, 0.034, 0.05), loc=(0.05, -0.26, 0.12), rot=(0, 0, 3), bevel=0.0), "wood_dark")


def crystal_cluster(a):
    crystal(a, Frame((0, 0, 0), rot=(0, 6, 0)), r=0.22, h=0.86)
    crystal(a, Frame((0.2, 0.06, 0), rot=(0, 28, 0)), r=0.12, h=0.48)
    crystal(a, Frame((-0.19, 0.04, 0), rot=(0, -24, 0)), r=0.11, h=0.42)
    a.add(p_ico(0.2, 1, loc=(0, 0.02, 0.0), scl=(1.4, 1.0, 0.45), cut=0.0,
                jitter=0.15, rng=random.Random(3)), by_normal("stone_light", "stone_mid", "stone_dark", 0.5))


# =============================================================================================
# третья очередь: товары
# =============================================================================================
def _cut_above(b, z):
    """Срезать всё выше z (мировая высота): горловина мешка открыта сверху."""
    bmesh.ops.bisect_plane(b, geom=b.verts[:] + b.edges[:] + b.faces[:], plane_co=(0, 0, z), plane_no=(0, 0, 1),
                           clear_outer=True)
    return b


def flour(a):
    """Мука: раскрытый холщовый мешок с отвёрнутой горловиной, мука горкой и совок в ней, синие полосы
    мельника по мешку, у подножия рассыпано."""
    rng = random.Random(118)
    a.add(_cut_above(p_ico(0.25, 2, loc=(0, 0, 0.19), scl=(1.0, 0.92, 1.0), jitter=0.05, rng=rng, cut=-0.19), 0.34),
          by_normal("burlap", "burlap", "burlap_dark", 0.6))
    for z in (0.12, 0.17):                                                                 # полосы мельника
        a.add(p_cyl(0.262, 0.262, 0.025, 14, loc=(0, 0, z)), "roof")
    a.add(p_cyl(0.205, 0.225, 0.06, 12, loc=(0, 0, 0.30), spin=15),
          by_normal("burlap", "burlap_dark", "burlap_dark", 0.6))                           # отворот
    a.add(p_ico(0.20, 2, loc=(0, 0, 0.33), scl=(1.0, 0.95, 0.45), cut=0.0), by_normal("flower_w", "cream", "cream", 0.3))
    sc = Frame((0.06, 0.02, 0.40), rot=(0, 38, 25))                                        # совок
    sc.box(a, (0.13, 0.10, 0.03), (0, 0, -0.03), col="wood_light", bevel=0.01)
    sc.box(a, (0.035, 0.035, 0.20), (0.0, 0.0, 0.08), col="wood_mid", bevel=0.008)
    a.add(p_ico(0.08, 1, loc=(-0.22, -0.20, 0.0), scl=(1.7, 1.1, 0.35), cut=0.0), "flower_w")   # рассыпано
    a.add(p_ico(0.04, 1, loc=(-0.08, -0.28, 0.0), scl=(1.4, 1.0, 0.35), cut=0.0), "flower_w")

def bread(a):
    """Хлеб: круглый каравай с надрезом крест-накрест и длинный батон наискось перед ним; румяная корка,
    светлый мякиш в надрезах."""
    crust = by_normal("wheat_dark", "wood_yellow", "wood_light", 0.45)
    a.add(p_ico(0.19, 2, loc=(0.07, 0.08, 0.065), scl=(1.12, 1.05, 0.62), cut=-0.065), crust)
    for s in (-1, 1):
        a.add(p_box((0.17, 0.028, 0.03), loc=(0.07, 0.08, 0.175), rot=(0, 0, s * 45), bevel=0.0), "wheat_light")
    b = Frame((-0.06, -0.12, 0.052), rz=-24)
    b.ico(a, 0.078, scl=(3.1, 1.0, 0.85), col=crust, sub=2, cut=-0.052)
    for x in (-0.13, 0.0, 0.13):
        b.box(a, (0.075, 0.026, 0.024), (x, 0.0, 0.058), rot=(0, 0, 38), col="wheat_light", bevel=0.0)


def _cheese_sector(a, fr, R, H, k0, k1, n, rind="berry", meat="wheat_light", seed=0):
    """Сектор круга сыра от k0 до k1 (из n долей): кожура воском по бокам, сверху и снизу, мякоть на
    срезах с дырками. Срезы смотрят по нормали наружу сектора."""
    b = bmesh.new()
    angs = [math.tau * k / n for k in range(k0, k1 + 1)]
    c0, c1 = b.verts.new((0, 0, 0)), b.verts.new((0, 0, H))
    bot = [b.verts.new((R * math.cos(t), R * math.sin(t), 0)) for t in angs]
    top = [b.verts.new((R * math.cos(t), R * math.sin(t), H)) for t in angs]
    role = {}
    for i in range(len(angs) - 1):
        role[b.faces.new((bot[i], bot[i + 1], top[i + 1], top[i]))] = rind
        role[b.faces.new((c1, top[i], top[i + 1]))] = rind
        role[b.faces.new((c0, bot[i + 1], bot[i]))] = "cloth_dark"
    role[b.faces.new((c0, bot[0], top[0], c1))] = meat
    role[b.faces.new((c0, c1, top[-1], bot[-1]))] = meat
    bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
    bmesh.ops.transform(b, matrix=fr.m, verts=b.verts)
    b.normal_update()
    a.add(b, lambda f: role[f])
    # дырки на срезах: тёмные кружки чуть над плоскостью среза
    rng = random.Random(seed)
    for t, sgn in ((angs[0], -1), (angs[-1], 1)):
        s = fr.sub((0, 0, 0), rz=math.degrees(t))
        for k in range(3):
            d, z, r = rng.uniform(0.25, 0.8) * R, rng.uniform(0.25, 0.75) * H, rng.uniform(0.018, 0.03)
            s.cyl(a, r, r, 0.006, 7, loc=(d, sgn * 0.002, z), rot=(-sgn * 90, 0, 0), col="wheat_dark")


def cheese(a):
    """Сыр: круг в красном воске с вырезанным клином — видна светлая мякоть с дырками; вырезанный клин
    лежит перед кругом."""
    n, gap, R, H = 14, 2, 0.27, 0.16
    rz = -66 - 360 / n * gap / 2
    _cheese_sector(a, Frame((0.04, 0.06, 0), rz=rz), R, H, gap, n, n, seed=3)
    _cheese_sector(a, Frame((-0.20, -0.30, 0), rz=rz + 205), R, H, 0, gap, n, seed=5)


def ale(a):
    """Эль: деревянная кружка из клёпок с двумя железными обручами, пена шапкой через край с потёками;
    за ней — бочонок на козлах с краником."""
    staves = lambda f: ("wood_light" if f.normal.z > 0.6 else
                        ("wood_mid" if f.normal.z < -0.6 else
                         ("wood_light" if int((math.degrees(math.atan2(f.normal.y, f.normal.x)) % 360) // 36) % 2
                          else "wood_mid2")))
    k = Frame((-0.20, 0.20, 0.0), rz=24)                                                   # бочонок
    for sx in (-1, 1):
        k.box(a, (0.05, 0.30, 0.10), (sx * 0.13, 0, 0.05), col="wood_dark", bevel=0.012)
    k.cyl(a, 0.15, 0.15, 0.36, 10, loc=(-0.18, 0, 0.19), rot=(0, 90, 0), col=staves)
    for x in (-0.12, 0.12):
        k.cyl(a, 0.157, 0.157, 0.03, 10, loc=(x - 0.015, 0, 0.19), rot=(0, 90, 0), col="iron_dark")
    k.cyl(a, 0.115, 0.115, 0.012, 10, loc=(-0.188, 0, 0.19), rot=(0, -90, 0), col="wood_pale")
    k.box(a, (0.07, 0.035, 0.035), (-0.21, 0, 0.15), col="gold", bevel=0.0)              # краник
    k.box(a, (0.03, 0.03, 0.05), (-0.235, 0, 0.125), col="gold", bevel=0.0)
    m = Frame((0.08, -0.08, 0.0))                                                          # кружка
    m.cyl(a, 0.128, 0.142, 0.30, 10, col=staves)
    for z in (0.05, 0.22):
        m.cyl(a, 0.15, 0.147, 0.035, 10, loc=(0, 0, z), col="iron_dark")
    for z0, h, x in ((0.06, 0.05, 0.185), (0.18, 0.05, 0.185)):                           # ручка
        m.box(a, (0.10, 0.05, h), (x - 0.04, 0, z0 + h / 2), col="wood_mid", bevel=0.01)
    m.box(a, (0.05, 0.05, 0.17), (0.20, 0, 0.145), col="wood_mid", bevel=0.01)
    m.ico(a, 0.152, loc=(0, 0, 0.30), scl=(1.0, 1.0, 0.52), col=by_normal("flower_w", "cream", "cream", 0.3),
          sub=2, cut=-0.03)                                                                 # пена
    for ang, h in ((-105, 0.07), (-60, 0.11), (200, 0.06)):                              # потёки
        t = math.radians(ang)
        m.ico(a, 0.03, loc=(math.cos(t) * 0.138, math.sin(t) * 0.138, 0.29 - h / 2), scl=(1.0, 0.8, h / 0.06),
              col="cream")


def wool(a):
    """Шерсть: пышное руно горкой из завитков (тёплый белый верх, бежевый низ) и перед ним синий клубок
    пряжи с витками и свободной нитью — клубок отличает шерсть от облака и муки."""
    rng = random.Random(129)
    fleece = by_normal("wool", "cream", "hide_light", 0.2)
    for x, y, z, r in ((-0.22, 0.06, 0.09, 0.10), (-0.10, 0.10, 0.12, 0.12), (0.03, 0.10, 0.13, 0.12),
                       (0.16, 0.08, 0.10, 0.11), (0.26, 0.05, 0.07, 0.08), (-0.17, -0.04, 0.08, 0.09),
                       (-0.05, -0.02, 0.11, 0.11), (0.09, -0.03, 0.10, 0.10), (-0.12, 0.08, 0.22, 0.08),
                       (0.02, 0.07, 0.24, 0.09), (0.13, 0.06, 0.20, 0.08), (-0.03, 0.18, 0.15, 0.09),
                       (0.12, 0.18, 0.12, 0.08), (-0.18, 0.17, 0.10, 0.08)):
        a.add(p_ico(r, 1, loc=(x, y, z), scl=(1.0, 0.95, 0.9), jitter=0.08, rng=rng,
                    rot=(rng.uniform(0, 40), rng.uniform(0, 40), rng.uniform(0, 360))), fleece)
    ball = Frame((0.10, -0.20, 0.105))                                                     # клубок
    ball.ico(a, 0.105, col=by_normal("roof_light", "roof", "roof_dark", 0.4), sub=2)
    for rot, col in (((0, 90, 20), "roof_light"), ((0, 90, -40), "roof_dark"), ((65, 0, 0), "roof_light"),
                     ((-50, 30, 0), "roof_dark"), ((20, 70, 50), "roof_light")):
        ball.cyl(a, 0.108, 0.108, 0.014, 12, loc=(0, 0, -0.007), rot=rot, col=col)
    for k, (x, y, rz) in enumerate(((-0.02, -0.27, 20), (-0.11, -0.25, -15), (-0.19, -0.29, 25))):  # нить
        a.add(p_box((0.10, 0.018, 0.018), loc=(x, y, 0.009), rot=(0, 0, rz), bevel=0.0), "roof")
    floor_cut(a)

def cloth(a):
    """Ткань: рулон багряного сукна на боку, свободный конец лежит языком с золотой каймой; за ним
    рулон поменьше синего полотна."""
    sw = lambda f: "cloth_light" if f.normal.z > 0.55 else ("cloth_dark" if abs(f.normal.x) > 0.7 or f.normal.z < -0.55
                                                           else "cloth")
    a.add(p_cyl(0.10, 0.10, 0.46, 12, loc=(-0.20, 0.20, 0.10), rot=(0, 90, 0), spin=15),
          lambda f: "roof_light" if f.normal.z > 0.55 else ("roof_dark" if abs(f.normal.x) > 0.7 else "roof"))
    a.add(p_cyl(0.13, 0.13, 0.58, 12, loc=(-0.29, -0.02, 0.13), rot=(0, 90, 0), spin=15), sw)
    for sx in (-1, 1):                                                                     # торцы: виток и втулка
        x = sx * 0.292
        a.add(p_cyl(0.085, 0.085, 0.006, 12, loc=(x, -0.02, 0.13), rot=(0, 90 * sx, 0), spin=15), "cloth")
        a.add(p_cyl(0.03, 0.03, 0.01, 8, loc=(x, -0.02, 0.13), rot=(0, 90 * sx, 0)), "wood_light")
    a.add(p_box((0.56, 0.06, 0.05), loc=(0.0, -0.14, 0.03), rot=(-35, 0, 0), bevel=0.0), sw)   # сход с рулона
    a.add(p_box((0.56, 0.24, 0.016), loc=(0.0, -0.27, 0.008), bevel=0.0), sw)              # язык
    a.add(p_box((0.565, 0.035, 0.018), loc=(0.0, -0.35, 0.01), bevel=0.0), "gold")        # кайма


def steel_stack(a):
    """Сталь: слитки стопкой «колодцем» — три вдоль, три поперёк, два сверху; синеватая сталь, иначе
    чем пирамидка железа."""
    z = 0.0
    for count, rz in ((3, 0), (3, 90), (2, 0)):
        for i in range(count):
            off = (i - (count - 1) / 2) * 0.15
            V.ingot(a, "steel", loc=(off, 0, z) if rz == 90 else (0, off, z), rz=rz)
        z += 0.074


# (имя ассета, билдер) — порядок = порядок в витрине: прежние места не сдвигаются
RESOURCES = [
    ("Res_Coal", coal),
    ("Res_GoldNugget", gold_nugget),
    ("Res_Straw", straw),
    ("Res_GoldenWheat", golden_wheat),
    ("Res_Meat", meat),
    ("Res_Milk", milk),
    ("Res_Scrap", scrap),
    ("Res_Feast", feast),
    ("Res_BattleAxe", VG.battle_axe),
    # сырьё с прежними иконками
    ("Res_Wheat", wheat_sheaf),
    ("Res_Hide", animal_hide),
    ("Res_Leather", leather),
    ("Res_Crystal", crystal_cluster),
    # третья очередь
    ("Res_Flour", flour),
    ("Res_Bread", bread),
    ("Res_Cheese", cheese),
    ("Res_Ale", ale),
    ("Res_Wool", wool),
    ("Res_Cloth", cloth),
    ("Res_Ingot_Steel", lambda a: V.ingot(a, "steel")),
    ("Res_IngotStack_Steel", steel_stack),
] + [(n, fn) for n, fn, _ in VG.GEAR if n != "Res_BattleAxe"]


def kit_entries():
    """Строки для KIT в build_vitaria.py: (категория, имя, билдер)."""
    return [("Resources", n, fn) for n, fn in RESOURCES]
