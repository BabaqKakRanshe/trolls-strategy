"""
Меши эффектов арены в стиле кита — вместо плоских спрайтов там, где удар и приземление должны
ощущаться «телом» (game feel: viscerality). Все непрозрачные, на палитре: не дают перерисовки
прозрачности и исчезают масштабом, а не альфой.

  FX_Dust_Puff_A/B/C   облачко пыли: приземление бойца, тяжёлый удар, смерть (пивот внизу)
  FX_Smoke_Puff_A/B    клуб дыма: костёр, трубы домов фона (пивот внизу)
  FX_Debris_Stone_A-C  осколки камня: попадание брошенным камнем, удар о препятствие (пивот в центре)
  FX_Debris_Earth_A/B  комья земли с дёрном: тяжёлый удар в землю (пивот в центре)
  FX_Debris_Wood_A/B   щепа: удар о пень, ящики (пивот в центре)
  FX_Rock_Throw        камень, который бросает тролль (пивот в центре, вращать)
  FX_Spark_Shard       искра-осколок: светится (грани glow_hot уходят в материал Vitaria_FX)

Размеры — при масштабе 1; в игре их масштабируют и крутят. Треугольников 20–80 на меш.
"""
import math
import random
from build_vitaria import p_ico, p_cyl, p_box, by_normal

DUST = by_normal("dust", "dust_dark", "dust_dark", 0.25)
SMOKE = by_normal("smoke", "smoke_dark", "smoke_dark", 0.25)
STONE = by_normal("stone_light", "stone_mid", "stone_dark", 0.55)
EARTH = by_normal("sod", "soil_mid", "soil_dark", 0.55)
WOOD = by_normal("wood_pale", "wood_mid", "wood_dark", 0.6)


def _puff(a, seed, col, main=0.2, parts=2, spread=0.17, flat=0.85):
    rng = random.Random(seed)
    a.add(p_ico(main, 1, loc=(0, 0, main * flat), scl=(1, 1, flat), jitter=0.12, rng=rng,
                rot=(0, 0, rng.uniform(0, 360))), col)
    for k in range(parts):
        ang = math.tau * k / parts + rng.uniform(-0.6, 0.6)
        r = main * rng.uniform(0.6, 0.75)
        a.add(p_ico(r, 1, loc=(math.cos(ang) * spread, math.sin(ang) * spread, r * flat), scl=(1, 1, flat),
                    jitter=0.12, rng=rng, rot=(0, 0, rng.uniform(0, 360))), col)


def dust_a(a):
    _puff(a, 1, DUST)


def dust_b(a):
    _puff(a, 2, DUST, main=0.18, parts=3, spread=0.2)


def dust_c(a):
    _puff(a, 3, DUST, main=0.22, parts=1, spread=0.14, flat=0.75)


def smoke_a(a):
    _puff(a, 4, SMOKE, main=0.22, parts=2, spread=0.15, flat=0.95)


def smoke_b(a):
    _puff(a, 5, SMOKE, main=0.2, parts=3, spread=0.16, flat=0.9)


def _chunk(a, seed, r, col, scl=(1.0, 0.8, 0.6)):
    rng = random.Random(seed)
    a.add(p_ico(r, 1, loc=(0, 0, 0), scl=scl, jitter=0.35, rng=rng, rot=(rng.uniform(0, 90), 0, rng.uniform(0, 360))),
          col)


def stone_a(a):
    _chunk(a, 11, 0.06, STONE)


def stone_b(a):
    _chunk(a, 12, 0.08, STONE, (1.0, 0.7, 0.75))


def stone_c(a):
    _chunk(a, 13, 0.1, STONE, (1.0, 0.85, 0.55))


def earth_a(a):
    _chunk(a, 21, 0.075, EARTH, (1.0, 0.9, 0.7))


def earth_b(a):
    _chunk(a, 22, 0.1, EARTH, (1.0, 0.8, 0.6))


def wood_a(a):
    a.add(p_cyl(0.035, 0.012, 0.24, 4, loc=(0, 0, -0.12), spin=45), WOOD)


def wood_b(a):
    a.add(p_box((0.05, 0.035, 0.18), loc=(0, 0, 0), bevel=0.008), WOOD)
    a.add(p_cyl(0.02, 0.0, 0.07, 4, loc=(0, 0, 0.09), spin=45), WOOD)


def rock_throw(a):
    rng = random.Random(31)
    a.add(p_ico(0.16, 1, loc=(0, 0, 0), scl=(1, 0.9, 0.8), jitter=0.3, rng=rng), STONE)


def spark_shard(a):
    a.add(p_cyl(0.035, 0.0, 0.11, 4, loc=(0, 0, 0), spin=45), "glow_hot")
    a.add(p_cyl(0.035, 0.0, 0.07, 4, loc=(0, 0, 0), rot=(180, 0, 0), spin=45), "glow")


ASSETS = [
    ("Arena", "FX_Dust_Puff_A", "Облачко пыли A", dust_a),
    ("Arena", "FX_Dust_Puff_B", "Облачко пыли B", dust_b),
    ("Arena", "FX_Dust_Puff_C", "Облачко пыли C", dust_c),
    ("Arena", "FX_Smoke_Puff_A", "Клуб дыма A", smoke_a),
    ("Arena", "FX_Smoke_Puff_B", "Клуб дыма B", smoke_b),
    ("Arena", "FX_Debris_Stone_A", "Осколок камня A", stone_a),
    ("Arena", "FX_Debris_Stone_B", "Осколок камня B", stone_b),
    ("Arena", "FX_Debris_Stone_C", "Осколок камня C", stone_c),
    ("Arena", "FX_Debris_Earth_A", "Ком земли A", earth_a),
    ("Arena", "FX_Debris_Earth_B", "Ком земли B", earth_b),
    ("Arena", "FX_Debris_Wood_A", "Щепа A", wood_a),
    ("Arena", "FX_Debris_Wood_B", "Щепа B", wood_b),
    ("Arena", "FX_Rock_Throw", "Камень для броска", rock_throw),
    ("Arena", "FX_Spark_Shard", "Искра", spark_shard),
]
