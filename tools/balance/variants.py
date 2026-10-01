"""Balance configs for campaign.py: the catalog before the 2026-10-01 pass and after it."""
import copy

M = "market"


def R(work, out, inp=None, bonus=None):
    r = {"work": work, "in": inp or {}, "out": out}
    if bonus:
        r["bonus"] = bonus
    return r


CURRENT = {
    "start_gold": 1000,
    "market_bonus_per_level": 1,
    "units": {"goblin": {"price": 40, "strength": 3, "speed": 5, "stamina": 100},
              "troll": {"price": 170, "strength": 9, "speed": 2, "stamina": 150}},
    "sell": {"IronOre": 3, "IronIngot": 9, "Wheat": 2, "AnimalHide": 9, "Leather": 14, "Logs": 2, "Planks": 4,
             "IronSword": 26, "IronArmor": 45, "WoodenShield": 18, "VioletCrystal": 30, "EnchantedSword": 90},
    "buildings": {
        "warehouse": {"price": 0, "size": 9, "cap": 500},
        "market": {"price": 0, "size": 6, "upgrades": [180, 300, 420]},
        "armory": {"price": 160, "size": 4, "sink": True},
        "mine": {"price": 200, "size": 9, "cap": 100, "workers": 5, "upgrades": [150, 250, 350],
                 "cap_per_level": 50, "workers_per_level": 2,
                 "recipes": [R(1, {"IronOre": 1}, bonus={"every": 25, "res": "VioletCrystal", "n": 1})]},
        "field": {"price": 80, "size": 9, "cap": 100, "workers": 5, "recipes": [R(10, {"Wheat": 10})]},
        "smeltery": {"price": 220, "size": 9, "cap": 50, "workers": 4,
                     "recipes": [R(2, {"IronIngot": 1}, {"IronOre": 2})]},
        "forge": {"price": 180, "size": 4, "cap": 20, "workers": 4,
                  "recipes": [R(5, {"IronArmor": 1}, {"IronIngot": 2, "Leather": 1}),
                              R(4, {"IronSword": 1}, {"IronIngot": 2})]},
        "farm": {"price": 140, "size": 9, "cap": 50, "workers": 4, "recipes": [R(3, {"AnimalHide": 1}, {"Wheat": 3})]},
        "tannery": {"price": 150, "size": 4, "cap": 50, "workers": 4,
                    "recipes": [R(2, {"Leather": 1}, {"AnimalHide": 1})]},
        "lumber": {"price": 120, "size": 4, "cap": 100, "workers": 5, "recipes": [R(1, {"Logs": 1})]},
        "mill": {"price": 200, "size": 9, "cap": 50, "workers": 4, "recipes": [R(1, {"Planks": 1}, {"Logs": 1})]},
        "shields": {"price": 190, "size": 4, "cap": 20, "workers": 4,
                    "recipes": [R(4, {"WoodenShield": 1}, {"Planks": 3})]},
        "enchanter": {"price": 300, "size": 4, "cap": 20, "workers": 3,
                      "recipes": [R(6, {"EnchantedSword": 1}, {"IronSword": 1, "VioletCrystal": 1})]},
    },
    "land": {"start_cells": 225, "base": 200, "step": 100, "clear_s": 10, "path_factor": 1.8},
    "battle": {"unlock_s": 120, "cooldown_s": 120, "fighters": 4, "away_s": 75, "first_win": 275, "repeat_win": 90},
    "player": {"think_s": 30, "act_s": 6, "check_every_s": 30, "leg_base": 6, "leg_per_building": 0.4,
               "leg_max": 14, "new_mine_factor": 1.5, "max_raw": 6},
    "raw_goods": {"mine": ["IronOre", "VioletCrystal"], "field": ["Wheat"], "lumber": ["Logs"]},
    "raw_units_per_work": {"mine": 1, "field": 1, "lumber": 1},
}

ORE = ["IronOre", "VioletCrystal"]


def quests(rewards, goals):
    """The current chain's structure; rewards and flow goal sizes come from the variant."""
    g = goals
    q = [
        ("Первый работник", [dict(do="hire", kind="goblin", total=1)], [("own_units", "goblin", 1)]),
        ("Своя шахта", [dict(do="build", kind="mine")], [("own", "mine", 1)]),
        ("За работу!", [dict(do="hire", kind="troll", total=1), dict(do="staff", at="mine", n=1, kind="troll"),
                        dict(do="route", src="mine", dst="warehouse", goods=ORE, n=1)],
         [("work_at", "troll", "mine", 1), ("haul", "goblin", "mine", "warehouse")]),
        ("Первая выручка", [dict(do="route", src="warehouse", dst=M, goods=ORE, n=1)],
         [("haul", None, "warehouse", M), ("earn", g["first_earn"])]),
        ("Казна", [dict(do="route", src="mine", dst="warehouse", goods=ORE, n=3),
                   dict(do="route", src="warehouse", dst=M, goods=ORE, n=3)], [g["treasury"]]),
        ("Отряд", [dict(do="hire", kind="goblin", total=6), dict(do="hire", kind="troll", total=2)],
         [("own_units", "goblin", 6), ("own_units", "troll", 2)]),
        ("Первый бой", [dict(do="battle")], [("win", 1)]),
        ("Урожай", [dict(do="build", kind="field")], [("own", "field", 1)]),
        ("Жатва", [dict(do="staff", at="field", n=3)], [("work_at", None, "field", 3)]),
        ("Хлеб на рынок", [dict(do="route", src="field", dst=M, goods=["Wheat"], n=2)], [("sell", "Wheat", g["wheat"])]),
        ("Плавильня", [dict(do="build", kind="smeltery"), dict(do="staff", at="smeltery", n=2)],
         [("own", "smeltery", 1), ("work_at", None, "smeltery", 2)]),
        ("Железо в цене", [dict(do="route", src="mine", dst="smeltery", goods=["IronOre"], n=2),
                           dict(do="route", src="smeltery", dst=M, goods=["IronIngot"], n=1)],
         [("sell", "IronIngot", g["ingots"])]),
        ("Кузница", [dict(do="build", kind="forge"), dict(do="staff", at="forge", n=2)],
         [("own", "forge", 1), ("work_at", None, "forge", 2)]),
        ("Богатый рынок", [dict(do="upgrade", kind=M, level=2)], [("level", M, 2)]),
        ("Склад экипировки", [dict(do="build", kind="armory"),
                              dict(do="route", src="smeltery", dst="forge", goods=["IronIngot"], n=1),
                              dict(do="route", src="forge", dst="armory", goods=["IronSword"], n=1)],
         [("own", "armory", 1), ("haul", None, "forge", "armory")]),
        ("Боевой опыт", [dict(do="battle")], [("win", 1)]),
        ("Ферма", [dict(do="build", kind="farm"), dict(do="staff", at="farm", n=2)],
         [("own", "farm", 1), ("work_at", None, "farm", 2)]),
        ("Растущее поселение", [dict(do="hire", kind="goblin", total=g["population"] - 2)],
         [("population", g["population"])]),
        ("Кожевня", [dict(do="build", kind="tannery"), dict(do="staff", at="tannery", n=2)],
         [("own", "tannery", 1), ("work_at", None, "tannery", 2)]),
        ("Кожа на продажу", [dict(do="route", src="field", dst="farm", goods=["Wheat"], n=1),
                             dict(do="route", src="farm", dst="tannery", goods=["AnimalHide"], n=1),
                             dict(do="route", src="tannery", dst=M, goods=["Leather"], n=1)],
         [("sell", "Leather", g["leather"])]),
        ("Лесозаготовка", [dict(do="build", kind="lumber"), dict(do="staff", at="lumber", n=3)],
         [("own", "lumber", 1), ("work_at", None, "lumber", 3)]),
        ("Торговый путь", [], [("earn", g["trade"])]),
        ("Пилорама", [dict(do="build", kind="mill"), dict(do="staff", at="mill", n=2)],
         [("own", "mill", 1), ("work_at", None, "mill", 2)]),
        ("Доски", [dict(do="route", src="lumber", dst="mill", goods=["Logs"], n=2),
                   dict(do="route", src="mill", dst=M, goods=["Planks"], n=2)], [("sell", "Planks", g["planks"])]),
        ("Мастерская щитов", [dict(do="build", kind="shields"), dict(do="staff", at="shields", n=2)],
         [("own", "shields", 1), ("work_at", None, "shields", 2)]),
        ("Глубокая шахта", [dict(do="upgrade", kind="mine", level=3)], [("level", "mine", 3)]),
        ("Зачарователь", [dict(do="build", kind="enchanter"), dict(do="staff", at="enchanter", n=1)],
         [("own", "enchanter", 1), ("work_at", None, "enchanter", 1)]),
    ]
    if g.get("barracks"):
        q[5] = ("Бараки", [dict(do="build", kind="barracks")], [("own", "barracks", 1)])
    if g.get("merge_field"):
        build, staff = q[7], q[8]
        q[7:9] = [("Урожай", build[1] + staff[1], build[2] + staff[2])]
    out = []
    for i, (title, steps, gl) in enumerate(q):
        out.append(dict(title=title, steps=steps, goals=[tuple(x) for x in gl], reward=rewards[i],
                        unlock_battle=(title in ("Отряд", "Бараки"))))
    return out


CURRENT_REWARDS = [0, 0, 100, 150, 100, 100, 0, 120, 150, 150, 150, 200, 200, 250, 200, 300, 250, 250, 250, 300,
                   250, 300, 300, 300, 300, 400, 400]
CURRENT_GOALS = dict(first_earn=30, treasury=("have_gold", 1000), wheat=20, ingots=10, population=16, leather=5,
                     trade=800, planks=20)


def reserve(cfg, q):
    total = 0
    for s in q["steps"]:
        if s["do"] == "build":
            total += cfg["buildings"][s["kind"]]["price"]
        elif s["do"] in ("staff", "route", "hire"):
            total += cfg["units"][s.get("kind", "goblin")]["price"] * s.get("n", 1)
        elif s["do"] == "upgrade":
            total += sum(cfg["buildings"][s["kind"]]["upgrades"][: s["level"] - 1])
    return total


def make(base, rewards, goals):
    cfg = copy.deepcopy(base)
    cfg["quests"] = quests(rewards, goals)
    for q in cfg["quests"]:
        q["reserve"] = reserve(cfg, q)
    return cfg


VARIANTS = {"current": lambda: make(CURRENT, CURRENT_REWARDS, CURRENT_GOALS)}


def shipped():
    """The 2026-10-01 balance pass (docs/economy-balance.md): what the content assets hold after it."""
    c = copy.deepcopy(CURRENT)
    c["hire_growth"] = 0.03          # EconomyConfig: +3% of the catalog price per creature in the colony
    c["copy_growth"] = 1.12          # EconomyConfig: each standing building of a kind makes the next ×1.12
    c["sell"] = {"IronOre": 3, "Wheat": 3, "Logs": 3, "IronIngot": 13, "IronSword": 45, "AnimalHide": 18,
                 "Leather": 32, "IronArmor": 90, "Planks": 8, "WoodenShield": 40, "VioletCrystal": 30,
                 "EnchantedSword": 130}
    b = c["buildings"]
    # raw producers stay cheap; a processing building pays back in ~7 min against raw work for its goblins
    for kind, price in dict(mine=200, field=250, lumber=400, smeltery=700, forge=1500, armory=600, farm=600,
                            tannery=1400, mill=1000, shields=1200, enchanter=3000).items():
        b[kind]["price"] = price
    # 2026-10-01: building the barracks opens the battle instead of hiring a squad of 6 goblins and 2 trolls
    b["barracks"] = {"price": 300, "size": 9, "sink": True}
    b["mine"].update(upgrades=[600, 900, 1300], workers_per_level=3)
    b["market"]["upgrades"] = [1000, 2000, 3000]
    b["tannery"]["recipes"] = [R(3, {"Leather": 1}, {"AnimalHide": 1})]
    c["land"].update(base=250, step=150)
    c["battle"].update(first_win=325, repeat_win=200)     # Mission_01: 250-400 first, 150-250 again
    c["player"]["copies_for_goals"] = True
    # a "use it" quest pays ~40% of the building it opens; the colony earns the rest
    rewards = [0, 0, 100, 150, 150, 100, 0,
               100, 300, 100, 600, 150, 250, 150, 250, 150, 550, 150, 200, 150, 400, 150, 500, 200, 1200, 400]
    goals = dict(first_earn=30, treasury=("earn", 300), wheat=40, ingots=30, population=20, leather=15,
                 trade=2000, planks=60, merge_field=True, barracks=True)
    return make(c, rewards, goals)


VARIANTS["shipped"] = shipped
