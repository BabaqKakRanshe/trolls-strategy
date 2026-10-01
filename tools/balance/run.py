"""Runs the campaign model.

    python run.py shipped                 per-quest timeline for the active, typical and passive player
    python run.py shipped typical         one profile
    python run.py current summary         one line per profile: length, waits, gold left at the end

Configs live in variants.py ("current" = before the 2026-10-01 pass, "shipped" = after it). See
docs/economy-balance.md for what the profiles and columns mean and how far to trust them.
"""
import copy, sys
from variants import VARIANTS
from campaign import Sim, show

PROFILES = {
    "active": dict(active=True, player={}),
    "typical": dict(active=True, player=dict(check_every_s=60, new_mine_factor=3.0, max_raw=4)),
    "passive": dict(active=False, player={}),
}


def run(name, profile, quiet=False):
    cfg = VARIANTS[name]()
    cfg["player"].update(PROFILES[profile]["player"])
    sim = Sim(cfg, active_growth=PROFILES[profile]["active"]).run()
    if not quiet:
        print(f"== {name}, {profile}")
        show(sim)
    return sim


if __name__ == "__main__":
    name = sys.argv[1]
    profiles = sys.argv[2:] or ["active", "typical", "passive"]
    if profiles == ["summary"]:
        for prof in PROFILES:
            s = run(name, prof, quiet=True)
            gw = sum(x[6] for x in s.log) / 60
            fw = sum(x[7] for x in s.log) / 60
            worst = max(s.log, key=lambda x: x[6] + x[7])
            print(f"{name:10}{prof:9} total {s.t / 60:5.1f} min  gold-wait {gw:4.1f}  flow-wait {fw:4.1f}  "
                  f"worst L{worst[0]} {(worst[6] + worst[7]) / 60:.1f} min  end gold {s.gold:6.0f}  units {len(s.units)}")
    else:
        for prof in profiles:
            run(name, prof)
