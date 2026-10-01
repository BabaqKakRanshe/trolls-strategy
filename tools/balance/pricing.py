"""Derive sell prices so each processing step pays `step_gain` times more per goblin-second than selling its inputs.

Goblin-seconds (gs) of a unit = worker time for all work in it + hauler time for every leg it travels.
"""
import math

GOB_WORK = 0.3                     # work per second
GOB_SPEED = (120 + 5 * 12) / 48    # cells per second
OVERHEAD = 0.5                     # load + step rounding per trip


def haul_s(leg):
    return 2 * leg / GOB_SPEED + OVERHEAD


def content(recipes, product, leg, memo=None):
    """(goblin-seconds to make and bring one unit to the next building, input products)"""
    memo = {} if memo is None else memo
    if product in memo:
        return memo[product]
    work, inputs, n = recipes[product]
    gs = work / n / GOB_WORK
    for i, k in inputs.items():
        gs += (content(recipes, i, leg, memo) + haul_s(leg)) * k / n
    memo[product] = gs
    return gs


def solve(recipes, raw_prices, step_gain, leg=8.0, byproducts=None):
    """Prices: raw ones given; each processed product priced so gold/gs of the step = step_gain x inputs' gold/gs."""
    prices = dict(raw_prices)
    prices.update(byproducts or {})
    order = []

    def visit(p):
        if p in order or p in prices and not recipes[p][1]:
            return
        for i in recipes[p][1]:
            if i in recipes:
                visit(i)
        order.append(p)

    for p in recipes:
        visit(p)
    for p in order:
        if p in raw_prices:
            continue
        work, inputs, n = recipes[p]
        in_gold = sum(prices[i] * k for i, k in inputs.items()) / n
        in_gs = sum((content(recipes, i, leg) if i in recipes else 0.0) + haul_s(leg) * 0 for i in inputs)  # noqa
        # gold per gs of selling the inputs straight (each input still needs its haul to market)
        sell_inputs_gs = sum(((content(recipes, i, leg) if i in recipes else 0.0) + haul_s(leg)) * k
                             for i, k in inputs.items()) / n
        rate_inputs = in_gold / sell_inputs_gs if sell_inputs_gs else 0
        my_gs = content(recipes, p, leg) + haul_s(leg)
        prices[p] = rate_inputs * step_gain * my_gs
    return prices


def table(recipes, prices, leg=8.0):
    rows = []
    for p, (work, inputs, n) in recipes.items():
        gs = content(recipes, p, leg) + haul_s(leg)
        rows.append((p, prices[p], gs, prices[p] / gs * 60))
    return rows


if __name__ == "__main__":
    recipes = {
        "IronOre": (1, {}, 1), "Wheat": (10, {}, 10), "Logs": (1, {}, 1),
        "IronIngot": (2, {"IronOre": 2}, 1), "IronSword": (4, {"IronIngot": 2}, 1),
        "AnimalHide": (3, {"Wheat": 3}, 1), "Leather": (3, {"AnimalHide": 1}, 1),
        "IronArmor": (5, {"IronIngot": 2, "Leather": 1}, 1),
        "Planks": (1, {"Logs": 1}, 1), "WoodenShield": (4, {"Planks": 3}, 1),
    }
    raw = {"IronOre": 3, "Wheat": 3, "Logs": 3}
    for gain in (1.0, 1.3):
        prices = solve(recipes, raw, gain)
        print(f"--- each step pays {gain:.2f}x the gold per goblin-minute of its inputs")
        for p, price, gs, gpm in table(recipes, prices):
            print(f"{p:14}{price:7.1f}  {gs:6.1f} gs  {gpm:6.1f} g/gob-min")
