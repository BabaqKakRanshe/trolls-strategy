"""
Таблица иконок Vitaria для render_icons.py: какая модель кита в каком ракурсе стоит в кадре.

Все иконки ресурсов и снаряжения — модели кита (Kit_Resources в .blend): руда, слитки, брёвна, доски,
монеты (build_vitaria.py), товары (vitaria_resources.py) и снаряжение с зачарованными версиями
(vitaria_gear.py). Модель и иконка строятся одной функцией; отдельная поза только у шкуры: в ките она
лежит на земле, в иконке приподнята к зрителю.
Соглашения кита: 1 юнит = 1 м, низ в z = 0, лицом к -Y, один swatch палитры на грань.
"""
from vitaria_resources import animal_hide

# наклон оружия в кадре: стоит на навершии или пятке, в иконке лежит по диагонали
SWORD = (0, -38, 0)
POLE = (0, -40, 0)

# Имя иконки -> [(меш или функция, (x, y, z), (rx, ry, rz) градусы, масштаб)]
# Строка — готовый меш кита из .blend; функция — модель, собранная только для кадра.
# Порядок = порядок кадров в атласе: новые иконки дописываются в конец, прежние кадры не сдвигаются.
RESOURCE_ICONS = {
    "iron-ore": [("Res_Ore_Iron", (0, 0, 0), (0, 0, 20), 1.0)],
    "iron-ingot": [("Res_IngotStack_Iron", (0, 0, 0), (0, 0, 15), 1.0)],
    "wheat": [("Res_Wheat", (0, 0, 0), (0, 0, 0), 1.0)],
    "animal-hide": [(lambda a: animal_hide(a, tilt=62.0, z=0.25), (0, 0, 0), (0, 0, 0), 1.0)],
    "leather": [("Res_Leather", (0, 0, 0), (0, 0, 20), 1.0)],
    "logs": [("Res_LogPile", (0, 0, 0), (0, 0, 15), 1.0)],
    "planks": [("Res_Planks", (0, 0, 0), (0, 0, 20), 1.0)],
    "wooden-shield": [("Res_Shield_Wood", (0, 0, 0), (0, 0, 10), 1.0)],
    "coins": [("Res_CoinStack", (0, 0, 0), (0, 0, 10), 1.0), ("Res_Coin", (0.2, -0.16, 0.012), (0, 0, 30), 1.0),
              ("Res_Coin", (-0.19, -0.12, 0.05), (70, 0, 25), 1.0)],
    "iron-sword": [("Res_Sword_Iron", (0, 0, 0), SWORD, 1.0)],
    "enchanted-sword": [("Res_Sword_Enchanted", (0, 0, 0), SWORD, 1.0)],
    "iron-armor": [("Res_Armor_Iron", (0, 0, 0), (0, 0, 15), 1.0)],
    "violet-crystal": [("Res_Crystal", (0, 0, 0), (0, 0, 10), 1.0)],
    # стартовое снаряжение отряда (EquipmentDefinition rusty-sword, patched-armor)
    "rusty-sword": [("Res_Sword_Rusty", (0, 0, 0), SWORD, 1.0)],
    "patched-armor": [("Res_Armor_Patched", (0, 0, 0), (0, 0, 15), 1.0)],
    # ресурсы второй очереди (vitaria_resources.py)
    "coal": [("Res_Coal", (0, 0, 0), (0, 0, 10), 1.0)],
    "gold-nugget": [("Res_GoldNugget", (0, 0, 0), (0, 0, 15), 1.0)],
    "straw": [("Res_Straw", (0, 0, 0), (0, 0, 20), 1.0)],
    "golden-wheat": [("Res_GoldenWheat", (0, 0, 0), (0, 0, 0), 1.0)],
    "meat": [("Res_Meat", (0, 0, 0), (0, 0, -25), 1.0)],
    "milk": [("Res_Milk", (0, 0, 0), (0, 0, 20), 1.0)],
    "scrap": [("Res_Scrap", (0, 0, 0), (0, 0, 10), 1.0)],
    "feast": [("Res_Feast", (0, 0, 0), (0, 0, 0), 1.0)],
    # топор стоит на пятке; в иконке наклонён вокруг неё на 38°, как меч iron-sword
    "battle-axe": [("Res_BattleAxe", (0, 0, 0), SWORD, 1.0)],
    # --- третья очередь (02.10). Камень, медь и золото — прежние модели кита, иконок у них не было
    "stone": [("Res_StonePile", (0, 0, 0), (0, 0, 15), 1.0)],
    "copper-ore": [("Res_Ore_Copper", (0, 0, 0), (0, 0, 20), 1.0)],
    "copper-ingot": [("Res_IngotStack_Copper", (0, 0, 0), (0, 0, 15), 1.0)],
    "gold-ore": [("Res_Ore_Gold", (0, 0, 0), (0, 0, 20), 1.0)],
    "gold-ingot": [("Res_IngotStack_Gold", (0, 0, 0), (0, 0, 15), 1.0)],
    # товары
    "flour": [("Res_Flour", (0, 0, 0), (0, 0, 15), 1.0)],
    "bread": [("Res_Bread", (0, 0, 0), (0, 0, 10), 1.0)],
    "cheese": [("Res_Cheese", (0, 0, 0), (0, 0, 0), 1.0)],
    "ale": [("Res_Ale", (0, 0, 0), (0, 0, 0), 1.0)],
    "wool": [("Res_Wool", (0, 0, 0), (0, 0, 10), 1.0)],
    "cloth": [("Res_Cloth", (0, 0, 0), (0, 0, 20), 1.0)],
    "steel": [("Res_IngotStack_Steel", (0, 0, 0), (0, 0, 15), 1.0)],
    # зачарованные версии имеющегося снаряжения
    "enchanted-rusty-sword": [("Res_Sword_Rusty_Enchanted", (0, 0, 0), SWORD, 1.0)],
    "enchanted-battle-axe": [("Res_BattleAxe_Enchanted", (0, 0, 0), SWORD, 1.0)],
    "enchanted-patched-armor": [("Res_Armor_Patched_Enchanted", (0, 0, 0), (0, 0, 15), 1.0)],
    "enchanted-iron-armor": [("Res_Armor_Iron_Enchanted", (0, 0, 0), (0, 0, 15), 1.0)],
    "enchanted-wooden-shield": [("Res_Shield_Wood_Enchanted", (0, 0, 0), (0, 0, 10), 1.0)],
    # новое снаряжение: обычное и зачарованное рядом
    "steel-sword": [("Res_Sword_Steel", (0, 0, 0), SWORD, 1.0)],
    "enchanted-steel-sword": [("Res_Sword_Steel_Enchanted", (0, 0, 0), SWORD, 1.0)],
    "spear": [("Res_Spear", (0, 0, 0), POLE, 1.0)],
    "enchanted-spear": [("Res_Spear_Enchanted", (0, 0, 0), POLE, 1.0)],
    "bow": [("Res_Bow", (0, 0, 0), (0, -12, 0), 1.0)],
    "enchanted-bow": [("Res_Bow_Enchanted", (0, 0, 0), (0, -12, 0), 1.0)],
    "war-hammer": [("Res_WarHammer", (0, 0, 0), SWORD, 1.0)],
    "enchanted-war-hammer": [("Res_WarHammer_Enchanted", (0, 0, 0), SWORD, 1.0)],
    "leather-armor": [("Res_Armor_Leather", (0, 0, 0), (0, 0, 15), 1.0)],
    "enchanted-leather-armor": [("Res_Armor_Leather_Enchanted", (0, 0, 0), (0, 0, 15), 1.0)],
    "chainmail": [("Res_Armor_Chainmail", (0, 0, 0), (0, 0, 15), 1.0)],
    "enchanted-chainmail": [("Res_Armor_Chainmail_Enchanted", (0, 0, 0), (0, 0, 15), 1.0)],
    "steel-armor": [("Res_Armor_Steel", (0, 0, 0), (0, 0, 15), 1.0)],
    "enchanted-steel-armor": [("Res_Armor_Steel_Enchanted", (0, 0, 0), (0, 0, 15), 1.0)],
    "helmet": [("Res_Helmet", (0, 0, 0), (0, 0, 20), 1.0)],
    "enchanted-helmet": [("Res_Helmet_Enchanted", (0, 0, 0), (0, 0, 20), 1.0)],
    "iron-shield": [("Res_Shield_Iron", (0, 0, 0), (0, 0, 10), 1.0)],
    "enchanted-iron-shield": [("Res_Shield_Iron_Enchanted", (0, 0, 0), (0, 0, 10), 1.0)],
}

# BuildingKind -> меш здания (как в префабах; казарма и склад — свои модели из Ref_Buildings)
BUILDING_ICONS = {
    "Barracks": "Bld_Barracks", "Enchanter": "Bld_Enchanter", "LumberCamp": "Bld_LumberCamp",
    "LumberMill": "Bld_LumberMill", "Warehouse": "Bld_Warehouse", "Mine": "Bld_Mine", "Armory": "Bld_Armory",
    "Market": "Bld_Market", "Farm": "Bld_Farm", "ShieldWorkshop": "Bld_ShieldWorkshop", "Forge": "Bld_Forge",
    "Tannery": "Bld_Tannery", "Smeltery": "Bld_Smeltery", "Field": "Bld_Field",
    "Tavern": "Bld_Tavern", "HaulersGuild": "Bld_HaulersGuild",
}

# Кадр иконки по части модели: (x0, x1, y0, y1) в осях меша. Плавильня стоит посреди мощёного двора
# 4.9 x 4.3 (след 3x3) — по всей модели печь в иконке выходила мелкой.
ICON_FOCUS = {"BuildingIcon_Smeltery": (-1.75, 1.75, -1.55, 1.7)}
