"""Rough campaign simulator: a scripted player follows the quest chain, the economy runs as flows.

Not the game's code: rates are averaged (work per second, haul trips per second), walking is a fixed leg
that grows with the colony. Good for comparing balance variants and spotting long waits, not for exact times.
"""
import json, math, sys

MARKET = "market"


class Sim:
    def __init__(self, cfg, active_growth=True, verbose=False):
        self.cfg, self.active, self.verbose = cfg, active_growth, verbose
        self.t = 0.0
        self.gold = cfg["start_gold"]
        self.earned = 0.0
        self.sold = {}
        self.sold_total = 0.0
        self.wins = 0
        self.units = []          # dict(kind, job) job: None | ("work", bid) | ("haul", rid) | ("battle",)
        self.buildings = []      # dict(id, kind, level, stock, progress)
        self.routes = []         # dict(id, src, dst, goods)
        self.land_cells = cfg["land"]["start_cells"]
        self.land_bought = 0
        self.used_cells = 0
        self.battle_ready_at = None
        self.battle_unlocked = False
        self.busy_until = 0.0
        self.log = []
        self.income_marks = []
        for kind in ("warehouse", MARKET):
            self._add_building(kind, free=True)
        self.market_level = 1

    # ---------- world helpers
    def bdef(self, kind):
        return self.cfg["buildings"][kind]

    def _add_building(self, kind, free=False):
        d = self.cfg["buildings"].get(kind, {"size": 9})
        self.used_cells += d.get("size", 9) * self.cfg["land"]["path_factor"]
        b = dict(id=f"{kind}-{sum(1 for x in self.buildings if x['kind'] == kind) + 1}", kind=kind, level=1,
                 stock={}, progress=0.0)
        self.buildings.append(b)
        return b

    def find(self, kind):
        return [b for b in self.buildings if b["kind"] == kind]

    def leg(self):
        p = self.cfg["player"]
        return min(p["leg_max"], p["leg_base"] + p["leg_per_building"] * len(self.buildings))

    def ustats(self, kind):
        u = self.cfg["units"][kind]
        return 0.1 * u["strength"], (120 + u["speed"] * 12) / 48, u["stamina"] / 100

    def workers(self, b):
        return [u for u in self.units if u["job"] == ("work", b["id"])]

    def haulers(self, r):
        return [u for u in self.units if u["job"] == ("haul", r["id"])]

    def capacity(self, b):
        d = self.bdef(b["kind"])
        return d.get("cap", 0) + d.get("cap_per_level", 0) * (b["level"] - 1)

    def worker_slots(self, b):
        d = self.bdef(b["kind"])
        return d.get("workers", 0) + d.get("workers_per_level", 0) * (b["level"] - 1)

    def price_of(self, res):
        return self.cfg["sell"][res] + self.cfg["market_bonus_per_level"] * (self.market_level - 1)

    # ---------- economy step
    def step(self, dt):
        self.t += dt
        for b in self.buildings:
            recs = self.bdef(b["kind"]).get("recipes", [])
            if not recs:
                continue
            work = sum(self.ustats(u["kind"])[0] for u in self.workers(b)) * dt
            if work <= 0:
                continue
            b["progress"] += work
            while True:
                r = next((r for r in recs if all(b["stock"].get(i, 0) >= n for i, n in r["in"].items())
                          and all(b["stock"].get(o, 0) + n <= self.capacity(b) for o, n in r["out"].items())), None)
                if r is None:
                    b["progress"] = 0.0
                    break
                if b["progress"] < r["work"]:
                    break
                b["progress"] -= r["work"]
                for i, n in r["in"].items():
                    b["stock"][i] -= n
                for o, n in r["out"].items():
                    b["stock"][o] = b["stock"].get(o, 0) + n
                b["cycles"] = b.get("cycles", 0) + 1
                bonus = r.get("bonus")
                if bonus and b["cycles"] % bonus["every"] == 0:
                    b["stock"][bonus["res"]] = b["stock"].get(bonus["res"], 0) + bonus["n"]
        leg = self.leg()
        # haulers of different routes share a source's stock; rotate who is served first each step
        k = int(self.t) % max(1, len(self.routes))
        for r in self.routes[k:] + self.routes[:k]:
            src = next(b for b in self.buildings if b["id"] == r["src"])
            rate = 0.0
            for u in self.haulers(r):
                _, speed, carry = self.ustats(u["kind"])
                rate += carry / (2 * leg / speed + 0.5)
            r["credit"] = min(r.get("credit", 0.0) + rate * dt, max(1.0, rate * 3))
            for g in r["goods"]:
                have = src["stock"].get(g, 0)
                if have <= 0 or r["credit"] < 1:
                    continue
                if r["dst"] == MARKET:
                    room = 10 ** 9
                else:
                    dst = next(b for b in self.buildings if b["id"] == r["dst"])
                    room = 10 ** 9 if self.bdef(dst["kind"]).get("sink") else self.capacity(dst) - dst["stock"].get(g, 0)
                n = int(min(have, room, r["credit"]))
                if n <= 0:
                    continue
                src["stock"][g] -= n
                r["credit"] -= n
                if r["dst"] == MARKET:
                    income = n * self.price_of(g)
                    self.gold += income
                    self.earned += income
                    self.sold[g] = self.sold.get(g, 0) + n
                    self.sold_total += n
                else:
                    dst["stock"][g] = dst["stock"].get(g, 0) + n
        for u in self.units:
            if u["job"] == ("battle",) and self.t >= u.get("back", 0):
                u["job"] = u.pop("prev", None)

    # ---------- player actions (each costs act seconds)
    def can_afford(self, cost):
        return self.gold >= cost

    def act(self, seconds=None):
        self.busy_until = max(self.busy_until, self.t) + (seconds if seconds is not None else self.cfg["player"]["act_s"])

    def hire_price(self, kind, n=1):
        r = self.cfg.get("hire_growth", 0.0)
        base = self.cfg["units"][kind]["price"]
        pop = len(self.units)
        return sum(round(base * (1 + r * (pop + i))) for i in range(n))

    def build_price(self, kind):
        g = self.cfg.get("copy_growth", 1.0)
        return round(self.bdef(kind)["price"] * g ** len(self.find(kind)))

    def hire(self, kind, n=1):
        cost = self.hire_price(kind, n)
        if not self.can_afford(cost):
            return False
        self.gold -= cost
        for _ in range(n):
            self.units.append(dict(kind=kind, job=None))
        self.act()
        return True

    def idle_units(self, kind):
        return [u for u in self.units if u["job"] is None and (kind is None or u["kind"] == kind)]

    def ensure_space(self, kind):
        need = self.bdef(kind).get("size", 9) * self.cfg["land"]["path_factor"]
        while self.used_cells + need > self.land_cells:
            price = self.cfg["land"]["base"] + self.cfg["land"]["step"] * self.land_bought
            if not self.can_afford(price):
                return False
            self.gold -= price
            self.land_bought += 1
            self.land_cells += 25
            self.act(self.cfg["player"]["act_s"] + self.cfg["land"]["clear_s"])
        return True

    def build(self, kind):
        price = self.build_price(kind)
        if not self.ensure_space(kind) or not self.can_afford(price):
            return None
        self.gold -= price
        self.act()
        return self._add_building(kind)

    def staff(self, b, n, kind="goblin"):
        """Put n more workers of kind on building b, hiring if needed."""
        for _ in range(n):
            if len(self.workers(b)) >= self.worker_slots(b):
                return True
            idle = self.idle_units(kind)
            if not idle:
                if not self.hire(kind):
                    return False
                idle = self.idle_units(kind)
            idle[0]["job"] = ("work", b["id"])
            self.act(1.5)
        return True

    def route(self, src, dst, goods, n, kind="goblin"):
        dst_id = MARKET if dst == MARKET else dst["id"]
        r = next((r for r in self.routes if r["src"] == src["id"] and r["dst"] == dst_id), None)
        if r is None:
            r = dict(id=f"r{len(self.routes) + 1}", src=src["id"], dst=dst_id, goods=goods)
            self.routes.append(r)
        for _ in range(n):
            idle = self.idle_units(kind)
            if not idle:
                if not self.hire(kind):
                    return r
                idle = self.idle_units(kind)
            idle[0]["job"] = ("haul", r["id"])
            self.act(2.0)
        return r

    def upgrade(self, kind, level):
        for b in self.find(kind) if kind != MARKET else [None]:
            cur = self.market_level if kind == MARKET else b["level"]
            if cur >= level:
                return True
            cost = self.bdef(kind)["upgrades"][cur - 1]
            if not self.can_afford(cost):
                return False
            self.gold -= cost
            if kind == MARKET:
                self.market_level += 1
            else:
                b["level"] += 1
            self.act()
            return cur + 1 >= level
        return False

    def battle(self):
        bc = self.cfg["battle"]
        if not self.battle_unlocked or self.t < bc["unlock_s"] or (self.battle_ready_at and self.t < self.battle_ready_at):
            return False
        fighters = sorted(self.units, key=lambda u: u["kind"] != "troll")[:bc["fighters"]]
        for u in fighters:
            u["prev"], u["job"], u["back"] = u["job"], ("battle",), self.t + bc["away_s"]
        self.act(bc["away_s"])
        self.wins += 1
        reward = bc["first_win"] if self.wins == 1 else bc["repeat_win"]
        self.gold += reward
        self.battle_ready_at = self.t + bc["cooldown_s"]
        return True

    # ---------- growth policy: what a reasonable player does while waiting
    def push_goal(self, quest, spare):
        """While a 'sell R' goal waits, staff the building making R and the haulers around it."""
        for g in quest["goals"]:
            if g[0] != "sell":
                continue
            makers = [b for b in self.buildings
                      if any(g[1] in r["out"] for r in self.bdef(b["kind"]).get("recipes", []))]
            if not makers or spare < self.hire_price("goblin"):
                return False
            b = makers[0]
            if len(self.workers(b)) < self.worker_slots(b):
                self.staff(b, 1)
                return True
            around = [r for r in self.routes if r["src"] == b["id"] or r["dst"] == b["id"]]
            if around:
                r = min(around, key=lambda r: len(self.haulers(r)))
                if len(self.haulers(r)) < 6:
                    src = next(x for x in self.buildings if x["id"] == r["src"])
                    dst = MARKET if r["dst"] == MARKET else next(x for x in self.buildings if x["id"] == r["dst"])
                    self.route(src, dst, r["goods"], 1)
                    return True
            # full and well hauled: a second copy, fed and emptied by the same kind of routes
            if (self.cfg["player"].get("copies_for_goals") and len(makers) < 2
                    and spare >= self.build_price(b["kind"]) + 4 * self.hire_price("goblin")):
                copy_ = self.build(b["kind"])
                if copy_:
                    self.staff(copy_, 2)
                    for r in [r for r in self.routes if r["dst"] == b["id"]]:
                        src = next(x for x in self.buildings if x["id"] == r["src"])
                        self.route(src, copy_, r["goods"], 1)
                    for r in [r for r in self.routes if r["src"] == b["id"]]:
                        dst = MARKET if r["dst"] == MARKET else next(x for x in self.buildings if x["id"] == r["dst"])
                        self.route(copy_, dst, r["goods"], 1)
                    return True
        return False

    def grow(self, reserve, quest=None):
        """Greedy: keep raw producers hauled at <=80% load, fill their slots, then build another copy."""
        spare = self.gold - reserve
        if quest is not None and self.push_goal(quest, spare):
            return
        raws = [b for b in self.buildings if b["kind"] in self.cfg["raw_goods"]]
        if not raws:
            return
        leg = self.leg()
        for b in sorted(raws, key=lambda b: b["id"]):
            goods = self.cfg["raw_goods"][b["kind"]]
            r = next((r for r in self.routes if r["src"] == b["id"] and r["dst"] == MARKET), None)
            prod = sum(self.ustats(u["kind"])[0] for u in self.workers(b)) * self.cfg["raw_units_per_work"].get(b["kind"], 1)
            haul = 0.0
            if r:
                for u in self.haulers(r):
                    _, speed, carry = self.ustats(u["kind"])
                    haul += carry / (2 * leg / speed + 0.5)
            stock = sum(b["stock"].get(g, 0) for g in goods)
            if (r is None or haul < prod * 1.25 or stock > 40) and spare >= self.hire_price("goblin"):
                self.route(b, MARKET, goods, 1)
                return
        for b in sorted(raws, key=lambda b: len(self.workers(b))):
            if len(self.workers(b)) < self.worker_slots(b) and spare >= self.hire_price("goblin"):
                self.staff(b, 1)
                return
        unlocked = {b["kind"] for b in raws}
        best = min(unlocked, key=self.build_price)
        cost = self.build_price(best) + 4 * self.hire_price("goblin")
        if spare >= cost * self.cfg["player"]["new_mine_factor"] and len(raws) < self.cfg["player"]["max_raw"]:
            b = self.build(best)
            if b:
                self.staff(b, 2)
                self.route(b, MARKET, self.cfg["raw_goods"][best], 2)

    # ---------- quests
    def goal_met(self, g, q0):
        k = g[0]
        if k == "own_units":
            return sum(1 for u in self.units if g[1] is None or u["kind"] == g[1]) >= g[2]
        if k == "own":
            return len(self.find(g[1])) >= g[2]
        if k == "work_at":
            return any(sum(1 for u in self.workers(b) if g[1] is None or u["kind"] == g[1]) >= g[3] for b in self.find(g[2]))
        if k == "haul":
            src_ids = {b["id"] for b in self.find(g[2])}
            dst_ids = {b["id"] for b in self.find(g[3])} | ({MARKET} if g[3] == MARKET else set())
            return any(r["src"] in src_ids and r["dst"] in dst_ids and self.haulers(r) for r in self.routes)
        if k == "have_gold":
            return self.gold >= g[1]
        if k == "earn":
            return self.earned - q0["earned"] >= g[1]
        if k == "sell":
            return self.sold.get(g[1], 0) - q0["sold"].get(g[1], 0) >= g[2]
        if k == "sell_any":
            return self.sold_total - q0["sold_total"] >= g[1]
        if k == "win":
            return self.wins - q0["wins"] >= g[1]
        if k == "level":
            if g[1] == MARKET:
                return self.market_level >= g[2]
            return any(b["level"] >= g[2] for b in self.find(g[1]))
        if k == "population":
            return len(self.units) >= g[1]
        raise ValueError(k)

    def run_script(self, steps):
        """Executes quest set-up steps; returns False while some step still waits for gold."""
        for s in steps:
            if s.get("done"):
                continue
            k = s["do"]
            ok = True
            if k == "hire":
                have = sum(1 for u in self.units if u["kind"] == s["kind"])
                ok = have >= s["total"] or self.hire(s["kind"])
                ok = ok and sum(1 for u in self.units if u["kind"] == s["kind"]) >= s["total"]
            elif k == "build":
                ok = len(self.find(s["kind"])) >= s.get("count", 1) or self.build(s["kind"]) is not None
            elif k == "staff":
                b = self.find(s["at"])[0]
                ok = self.staff(b, max(0, s["n"] - len(self.workers(b))), s.get("kind", "goblin"))
                ok = ok and len(self.workers(b)) >= min(s["n"], self.worker_slots(b))
            elif k == "route":
                src = self.find(s["src"])[0]
                dst = MARKET if s["dst"] == MARKET else self.find(s["dst"])[0]
                r = self.route(src, dst, s["goods"], 0)
                need = s["n"] - len(self.haulers(r))
                if need > 0:
                    self.route(src, dst, s["goods"], need, s.get("kind", "goblin"))
                ok = len(self.haulers(r)) >= s["n"]
            elif k == "upgrade":
                ok = self.upgrade(s["kind"], s["level"])
            elif k == "battle":
                ok = self.battle()
            elif k == "unlock_battle":
                self.battle_unlocked = True
            if not ok:
                return False
            s["done"] = True
            if self.t < self.busy_until:
                return False
        return True

    def run(self, max_minutes=240):
        quests = self.cfg["quests"]
        p = self.cfg["player"]
        qi = 0
        q0 = None
        start_q = 0.0
        next_check = 0.0
        while qi < len(quests) and self.t < max_minutes * 60:
            q = quests[qi]
            if q0 is None:
                q0 = dict(earned=self.earned, sold=dict(self.sold), sold_total=self.sold_total, wins=self.wins)
                self.busy_until = self.t + p["think_s"]
                start_q = self.t
                gold_wait = flow_wait = 0
            if self.t >= self.busy_until:
                done = self.run_script(q["steps"])
                met = done and all(self.goal_met(g, q0) for g in q["goals"])
                if not done and self.t >= self.busy_until:
                    gold_wait += 1
                elif done and not met and self.t >= self.busy_until:
                    flow_wait += 1
                if met:
                    self.gold += q["reward"]
                    if q.get("unlock_battle"):
                        self.battle_unlocked = True
                    self.log.append((qi + 1, q["title"], start_q, self.t, self.gold, self.income_per_min(),
                                     gold_wait, flow_wait))
                    qi += 1
                    q0 = None
                    continue
                if self.active and self.t >= next_check and self.t >= self.busy_until:
                    self.grow(q.get("reserve", 0), q if done else None)
                    next_check = self.t + p["check_every_s"]
            self.step(1.0)
            if int(self.t) % 60 == 0:
                self.income_marks.append((self.t, self.earned))
        return self

    def income_per_min(self):
        # market income over the last two minutes
        past = [e for (t, e) in self.income_marks if t <= self.t - 120]
        base = past[-1] if past else 0.0
        span = min(120.0, max(1.0, self.t))
        return (self.earned - base) / span * 60


def show(sim):
    print(f"{'lvl':>3} {'задание':22}{'старт':>7}{'конец':>7}{'длит':>6}{'ждёт $':>7}{'ждёт поток':>11}{'золото':>8}{'доход/мин':>10}{'запас,мин':>10}")
    for lvl, title, a, b, gold, inc, gw, fw in sim.log:
        stash = gold / inc if inc > 1 else 0
        print(f"{lvl:>3} {title:22}{a / 60:>7.1f}{b / 60:>7.1f}{(b - a) / 60:>6.1f}{gw:>7.0f}{fw:>11.0f}{gold:>8.0f}{inc:>10.0f}{stash:>10.1f}")
    print(f"total {sim.t / 60:.1f} min, units {len(sim.units)}, buildings {len(sim.buildings)}, land blocks {sim.land_bought}, gold {sim.gold:.0f}")


if __name__ == "__main__":
    cfg = json.load(open(sys.argv[1], encoding="utf-8"))
    mode = sys.argv[2] if len(sys.argv) > 2 else "active"
    show(Sim(cfg, active_growth=(mode == "active")).run())
