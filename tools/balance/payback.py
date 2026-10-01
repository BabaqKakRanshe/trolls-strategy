"""Payback of each producer at full goblin staffing: building + staff + haulers vs value added per minute."""
from pricing import haul_s

def payback(name, price, slots, work, inputs, out_n, p_out, prices, gob=40, leg=8.0):
    work_s = slots * 0.3
    cycles = work_s / work
    out_s = cycles * out_n
    in_value = sum(prices[i] * k for i, k in inputs.items()) * cycles
    added = out_s * p_out - in_value               # gold/s over selling the inputs
    flow = out_s + sum(k * cycles for k in inputs.values())   # units hauled per second (in + out)
    haulers = flow * haul_s(leg) / 0.8             # haulers at 80% load
    total = price + (slots + haulers) * gob
    return dict(name=name, added_min=added * 60, haulers=haulers,
                pb_building=price / (added * 60), pb_total=total / (added * 60))

def report(rows):
    print(f"{'building':10}{'+g/min':>8}{'haul':>6}{'pb bld':>8}{'pb all':>8}")
    for r in rows:
        print(f"{r['name']:10}{r['added_min']:>8.0f}{r['haulers']:>6.1f}{r['pb_building']:>8.2f}{r['pb_total']:>8.2f}")

if __name__ == "__main__":
    import copy
    from variants import CURRENT, shipped
    for name, cfg in (("current", CURRENT), ("shipped", shipped())):
        p, b = cfg["sell"], cfg["buildings"]
        tan = b["tannery"]["recipes"][0]["work"]
        print(f"--- {name}: payback in minutes at full goblin staffing (base hire price), hauling legs of 8 cells")
        report([
            payback("mine", b["mine"]["price"], 5, 1, {}, 1, p["IronOre"] + p["VioletCrystal"] / 25, p),
            payback("field", b["field"]["price"], 5, 10, {}, 10, p["Wheat"], p),
            payback("lumber", b["lumber"]["price"], 5, 1, {}, 1, p["Logs"], p),
            payback("smeltery", b["smeltery"]["price"], 4, 2, {"IronOre": 2}, 1, p["IronIngot"], p),
            payback("forge", b["forge"]["price"], 4, 4, {"IronIngot": 2}, 1, p["IronSword"], p),
            payback("farm", b["farm"]["price"], 4, 3, {"Wheat": 3}, 1, p["AnimalHide"], p),
            payback("tannery", b["tannery"]["price"], 4, tan, {"AnimalHide": 1}, 1, p["Leather"], p),
            payback("mill", b["mill"]["price"], 4, 1, {"Logs": 1}, 1, p["Planks"], p),
            payback("shields", b["shields"]["price"], 4, 4, {"Planks": 3}, 1, p["WoodenShield"], p),
            payback("enchant", b["enchanter"]["price"], 3, 6, {"IronSword": 1, "VioletCrystal": 1}, 1,
                    p["EnchantedSword"], p),
        ])
