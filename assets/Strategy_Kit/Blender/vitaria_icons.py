"""
Модели иконок ресурсов Vitaria: только для рендера иконок (render_icons.py), в игру мешами не идут.

Большая часть иконок — готовые модели кита (руда, слитки, брёвна, доски, монеты) и детали
vitaria_buildings.common (меч, щит, кираса, кристалл). Здесь — то, чего в ките нет:
сноп пшеницы, шкура, свёрнутая кожа, зачарованный меч, друза кристаллов.
Соглашения кита: 1 юнит = 1 м, низ в z = 0, лицом к -Y, один swatch палитры на грань.
"""
import math
import random
from mathutils import Vector
from build_vitaria import p_box, p_cyl, p_ico, p_prism, by_normal
from vitaria_buildings.common import Frame, sword, round_shield, breastplate, crystal


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


def animal_hide(a):
    """Распластанная шкура: контур с четырьмя лапами, шеей и хвостом; светлое брюхо."""
    outer = _hide_outline()
    tilt = 62.0
    fr = Frame((0, 0, 0.25), rot=(tilt, 0, 0))                # приподнята к зрителю: иначе в иконке — щель
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


def iron_sword(a):
    sword(a, Frame((0, 0, 0), rot=(0, -38, 0)), L=1.0, guard="iron_light", grip="leather",
          blade="steel", pommel="iron_light")


def enchanted_sword(a):
    fr = Frame((0, 0, 0), rot=(0, -38, 0))
    sword(a, fr, L=1.05, guard="gold", grip="leather_dark", blade="crystal_light", pommel="crystal")
    body = (1.05 - 0.32) * 0.80
    fr.box(a, (0.03, 0.05, body * 0.8), (0, -0.004, 0.30 + body * 0.46), col="rune", bevel=0.0)
    fr.ico(a, 0.045, loc=(0, -0.03, 0.285), col="arcane", scl=(1, 0.7, 1))        # камень в гарде


def wooden_shield(a):
    round_shield(a, Frame((0, 0, 0.45), rot=(0, 0, 0)), r=0.45, face="wood_mid", paint="wood_light",
                 pattern="band", rim="iron_dark", boss="iron_light", studs=8)


def iron_armor(a):
    # золотая кайма и кожаные ремни: одной серой кирасой она сливалась со слитками
    breastplate(a, Frame((0, 0, 0)), col="iron_light", trim="gold")
    for sx in (-1, 1):
        a.add(p_box((0.05, 0.03, 0.34), loc=(sx * 0.13, -0.13, 0.2), bevel=0.0), "leather")


def rusty_sword(a):
    """Ржавый меч: тёмный клинок в рыжих пятнах, щербина на лезвии, обмотка вместо рукояти."""
    fr = Frame((0, 0, 0), rot=(0, -38, 0))
    sword(a, fr, L=0.95, guard="iron_dark", grip="burlap_dark", blade="iron", pommel="iron_dark")
    rng = random.Random(4)
    body = (0.95 - 0.32) * 0.8
    for k in range(6):
        z = 0.34 + rng.uniform(0.0, body)
        fr.box(a, (rng.uniform(0.04, 0.08), 0.05, rng.uniform(0.04, 0.09)), (rng.uniform(-0.03, 0.03), -0.003, z),
               col=rng.choice(["iron_ore_vein", "copper_dark", "leather"]), bevel=0.0)
    fr.box(a, (0.05, 0.06, 0.06), (0.06, 0.0, 0.3 + body * 0.62), col="black", bevel=0.0)     # щербина


def patched_armor(a):
    """Латаная броня: тусклая кираса, кожаные заплаты на заклёпках и верёвка вместо ремня."""
    breastplate(a, Frame((0, 0, 0)), col="iron", trim="iron_dark")
    for (x, z, w, h, col) in ((-0.1, 0.25, 0.14, 0.12, "leather"), (0.12, 0.13, 0.12, 0.1, "burlap"),
                              (0.06, 0.34, 0.09, 0.08, "hide")):
        a.add(p_box((w, 0.03, h), loc=(x, -0.135, z), rot=(0, 6, 0), bevel=0.0), col)
        for dx in (-w / 2 + 0.02, w / 2 - 0.02):
            a.add(p_box((0.022, 0.02, 0.022), loc=(x + dx, -0.152, z + h / 2 - 0.02), bevel=0.0), "iron_light")
    a.add(p_cyl(0.215, 0.2, 0.035, 10, loc=(0, 0, 0.08)), "rope")


def crystal_cluster(a):
    crystal(a, Frame((0, 0, 0), rot=(0, 6, 0)), r=0.22, h=0.86)
    crystal(a, Frame((0.2, 0.06, 0), rot=(0, 28, 0)), r=0.12, h=0.48)
    crystal(a, Frame((-0.19, 0.04, 0), rot=(0, -24, 0)), r=0.11, h=0.42)
    a.add(p_ico(0.2, 1, loc=(0, 0.02, 0.0), scl=(1.4, 1.0, 0.45), cut=0.0,
                jitter=0.15, rng=random.Random(3)), by_normal("stone_light", "stone_mid", "stone_dark", 0.5))


# Имя иконки -> [(меш или функция, (x, y, z), (rx, ry, rz) градусы, масштаб)]
# Строка — готовый меш кита из .blend; функция — модель отсюда.
RESOURCE_ICONS = {
    "iron-ore": [("Res_Ore_Iron", (0, 0, 0), (0, 0, 20), 1.0)],
    "iron-ingot": [("Res_IngotStack_Iron", (0, 0, 0), (0, 0, 15), 1.0)],
    "wheat": [(wheat_sheaf, (0, 0, 0), (0, 0, 0), 1.0)],
    "animal-hide": [(animal_hide, (0, 0, 0), (0, 0, 0), 1.0)],
    "leather": [(leather, (0, 0, 0), (0, 0, 20), 1.0)],
    "logs": [("Res_LogPile", (0, 0, 0), (0, 0, 15), 1.0)],
    "planks": [("Res_Planks", (0, 0, 0), (0, 0, 20), 1.0)],
    "wooden-shield": [(wooden_shield, (0, 0, 0), (0, 0, 10), 1.0)],
    "coins": [("Res_CoinStack", (0, 0, 0), (0, 0, 10), 1.0), ("Res_Coin", (0.2, -0.16, 0.012), (0, 0, 30), 1.0),
              ("Res_Coin", (-0.19, -0.12, 0.05), (70, 0, 25), 1.0)],
    "iron-sword": [(iron_sword, (0, 0, 0), (0, 0, 0), 1.0)],
    "enchanted-sword": [(enchanted_sword, (0, 0, 0), (0, 0, 0), 1.0)],
    "iron-armor": [(iron_armor, (0, 0, 0), (0, 0, 15), 1.0)],
    "violet-crystal": [(crystal_cluster, (0, 0, 0), (0, 0, 10), 1.0)],
    # стартовое снаряжение отряда (EquipmentDefinition rusty-sword, patched-armor)
    "rusty-sword": [(rusty_sword, (0, 0, 0), (0, 0, 0), 1.0)],
    "patched-armor": [(patched_armor, (0, 0, 0), (0, 0, 15), 1.0)],
}

# BuildingKind -> меш здания (как в префабах; казарма и склад — свои модели из Ref_Buildings)
BUILDING_ICONS = {
    "Barracks": "Bld_Barracks", "Enchanter": "Bld_Enchanter", "LumberCamp": "Bld_LumberCamp",
    "LumberMill": "Bld_LumberMill", "Warehouse": "Bld_Warehouse", "Mine": "Bld_Mine", "Armory": "Bld_Armory",
    "Market": "Bld_Market", "Farm": "Bld_Farm", "ShieldWorkshop": "Bld_ShieldWorkshop", "Forge": "Bld_Forge",
    "Tannery": "Bld_Tannery", "Smeltery": "Bld_Smeltery", "Field": "Bld_Field",
}

# Кадр иконки по части модели: (x0, x1, y0, y1) в осях меша. Плавильня стоит посреди мощёного двора
# 4.9 x 4.3 (след 3x3) — по всей модели печь в иконке выходила мелкой.
ICON_FOCUS = {"BuildingIcon_Smeltery": (-1.75, 1.75, -1.55, 1.7)}
