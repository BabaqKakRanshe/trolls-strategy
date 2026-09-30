"""
Колония на парящем острове, который растёт блоками (Colony_Isle, сборка — build_isle.py).

Зона стройки — сетка GRID x GRID блоков по BLOCK м (8 x 8 по 5 м = 40 x 40 клеток по 1 м), старт —
середина 4 x 4 блока (20 x 20). Каждый блок — свой каменный столб: газон ровно по 5 x 5 м на уровне
0, свес дёрна по краю и пласты скалы до своей глубины. Глубина растёт к середине сетки, поэтому низ
острова из многих столбов — перевёрнутый купол. Стены между соседними купленными блоками прячутся
под газоном соседа, а у более мелкого соседа видна стена более глубокого — это правильный уступ.
Поэтому у блока нет вариантов под соседей: блок только показывают или прячут (и поднимают).

Состояния блока (как решили в игре):
  cover   не куплен: над ячейкой облачная полка (CI_Cover_<bx>_<by>);
  rising  куплен, поднимается из облаков (облака над ячейкой расходятся);
  wild    пристыкован: дикая земля — лес, камни, кусты (CI_Wild_<bx>_<by>), строить нельзя;
  clear   расчищен: газон, клевер, стройка.
Край владений украшает обвязка стороны блока (CI_Rim_<bx>_<by>_<сторона>): видна, когда за этой
стороной нет купленного блока.

Всё, что не зона стройки (водопад, рудник, поля), — на островах-спутниках за пределами 40 x 40:
к концу игры остров дорастает до них.

Пространство колонии: начало — середина сетки (в игре MapToWorld(GridWidth/2, GridHeight/2) при
сетке 40 x 40), X на восток, Y на север, Z вверх. Клетка (cx, cy) сетки: x = cx - 20, y = cy - 20.
"""
import math
from . import island as I

NAME = "Colony_Isle"
BLOCK = 5.0
GRID = 8
START = (2, 2, 4, 4)          # (bx, by, w, h) стартовой зоны в блоках
HALF = GRID * BLOCK / 2.0     # 20 м
CELLS = int(GRID * BLOCK)     # 40 клеток

FADE = I.FADE
GRASS = "grass"
# пласты стен блока: уровни общие для всех блоков (zref и bottom — опорные), низ каждого — свой
BLOCK_DEEP = dict(bottom=-22.0, strata=(0.035, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.64, 0.77),
                  batter=-0.05, amp=0.3, wave=0.45, lean=0.14, crack=0.18, zref=-0.36, nseed=0.5)
JAG = 3.0                      # разброс низа столба, м


def block_rect(bx, by):
    x0, y0 = bx * BLOCK - HALF, by * BLOCK - HALF
    return x0, y0, x0 + BLOCK, y0 + BLOCK


def block_center(bx, by):
    x0, y0, x1, y1 = block_rect(bx, by)
    return (x0 + x1) / 2.0, (y0 + y1) / 2.0


def is_start(bx, by):
    sx, sy, w, h = START
    return sx <= bx < sx + w and sy <= by < sy + h


def block_bottom(bx, by):
    """Низ столба: в середине сетки -22 м, к краю -7 м — низ острова куполом."""
    cx, cy = block_center(bx, by)
    r = math.hypot(cx, cy)
    t = min(1.0, r / 26.0)
    k = t * t * (3 - 2 * t)
    return -(7.0 + 15.5 * (1.0 - k))


def biome(bx, by):
    """Дикая земля блока: лес (чаще), камни или луг с кустами — по шуму от номера блока."""
    h = (bx * 73856093 ^ by * 19349663) % 100
    if h < 52:
        return "forest"
    if h < 76:
        return "rocks"
    return "meadow"


WILD = {
    # (ассет, вес), число объектов, шкала
    "forest": ([("Tree_Pine_A", 3), ("Tree_Pine_B", 3), ("Tree_Pine_C", 1.5), ("Tree_Round_A", 2), ("Tree_Round_B", 2),
                ("Bush_A", 2), ("Bush_B", 1.5), ("Tree_Stump", 0.6), ("Rock_Medium", 0.5)], (9, 13), (0.8, 1.25)),
    "rocks": ([("Rock_Large", 2), ("Rock_Medium", 3), ("Rock_Small", 3), ("Bush_B", 1), ("Tree_Pine_B", 1),
               ("Res_StonePile", 0.5)], (9, 13), (0.7, 1.15)),
    "meadow": ([("Bush_A", 3), ("Bush_B", 3), ("Bush_Berry", 1.5), ("Tree_Round_A", 1), ("Tree_Round_B", 1),
                ("Env_FlowerPatch_A", 2.5), ("Env_FlowerPatch_B", 1.5)], (10, 14), (0.85, 1.3)),
}

# облачный пол под некупленной ячейкой (на 7–9 м ниже газона): пуфы, высота низа, масштаб
COVER = dict(items=[("Env_CloudPuff_A", 3), ("Env_CloudPuff_C", 2), ("Env_CloudPuff_B", 1)], n=(1, 2),
             z=(-11.0, -8.5), scale=(1.2, 1.7))

# ---------------------------------------------------------------------------------------
# этапы игры для кадров: купленные блоки, дикие, поднимающийся, здания (SW-клетка, вид)
# ---------------------------------------------------------------------------------------
_start_blocks = [(bx, by) for bx in range(2, 6) for by in range(2, 6)]
_all_blocks = [(bx, by) for bx in range(GRID) for by in range(GRID)]
B_START = [("Bld_Market", 22, 17), ("Bld_Warehouse", 22, 23), ("Bld_LumberCamp", 13, 23), ("Bld_Farm", 14, 13),
           ("Bld_Field", 18, 13)]
B_MID = B_START + [("Bld_Mine", 12, 17), ("Bld_Forge", 17, 21), ("Bld_Barracks", 26, 22), ("Bld_Smeltery", 16, 25),
                   ("Bld_Tannery", 26, 12), ("Bld_Armory", 20, 26), ("Bld_ShieldWorkshop", 26, 17),
                   ("Bld_LumberMill", 12, 26), ("Bld_Enchanter", 19, 17), ("Bld_Field", 31, 16), ("Bld_Farm", 31, 21)]
B_MAX = B_MID + [("Bld_Mine", 4, 12), ("Bld_LumberCamp", 4, 27), ("Bld_LumberMill", 5, 31), ("Bld_Field", 12, 4),
                 ("Bld_Field", 16, 4), ("Bld_Farm", 21, 5), ("Bld_Barracks", 32, 30), ("Bld_Warehouse", 27, 32),
                 ("Bld_Smeltery", 33, 8), ("Bld_Market", 15, 33), ("Bld_Tannery", 8, 8), ("Bld_Forge", 35, 25),
                 ("Bld_Armory", 9, 33), ("Bld_Field", 26, 5), ("Bld_LumberCamp", 5, 20)]
STAGES = {
    # камера — формула игры: середина владений, отъезд = сторона рамки владений x 1.16
    "start": dict(owned=_start_blocks, wild=[], rising=None, buildings=B_START),
    "mid": dict(owned=_start_blocks + [(6, 2), (6, 3), (6, 4), (3, 6), (4, 6), (7, 3)], wild=[(3, 6), (4, 6)],
                rising=(7, 3), rise=-3.2, buildings=B_MID),
    "max": dict(owned=_all_blocks, wild=[(0, 7), (7, 0)], rising=None, buildings=B_MAX),
}


def stage_camera(stage):
    """(позиция, цель): рамка купленных блоков -> середина и отъезд как в ThreeDSceneSetup (x 1.16)."""
    st = STAGES[stage]
    bl = [b for b in st["owned"] if b != st.get("rising")]
    x0 = min(block_rect(*b)[0] for b in bl)
    x1 = max(block_rect(*b)[2] for b in bl)
    y0 = min(block_rect(*b)[1] for b in bl)
    y1 = max(block_rect(*b)[3] for b in bl)
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    d = max(x1 - x0, y1 - y0) * 1.16
    return (cx + 1.5, cy - d, d), (cx, cy, 0.0), d


# ---------------------------------------------------------------------------------------
# острова-спутники: всё, что не стройка. За пределами 40 x 40 (край сетки +-20 м)
# ---------------------------------------------------------------------------------------
def blob(cx, cy, rx, ry, n=11, seed=0, jit=0.14):
    import random
    rng = random.Random(seed)
    out = []
    for k in range(n):
        a = math.tau * k / n + rng.uniform(-0.12, 0.12)
        r = 1.0 + rng.uniform(-jit, jit)
        out.append((cx + math.cos(a) * rx * r, cy + math.sin(a) * ry * r))
    return out


SATELLITES = {
    # имя: (контур, высота травы, глубина, сид, холмы)
    "massif": (blob(-29.5, 27.0, 6.8, 6.4, 12, 31), 3.6, 17.0, 31, 0.6),
    "ridge": (blob(3.0, 27.8, 12.5, 3.9, 14, 32, 0.1), 2.2, 13.0, 32, 0.45),
    "farm": (blob(29.5, 26.0, 6.4, 5.6, 12, 33), 0.8, 11.0, 33, 0.2),
    "rock_w": (blob(-28.0, 3.0, 3.2, 2.8, 9, 34), -1.5, 8.0, 34, 0.15),
    "rock_e": (blob(28.5, -3.0, 3.6, 3.0, 9, 35), -2.5, 8.5, 35, 0.15),
    "rock_s": (blob(5.0, -28.5, 2.6, 2.2, 8, 36), -4.0, 6.5, 36, 0.0),
}
SAT_FALLS = [
    # водопад массива в облака (виден в кадре старта слева сверху)
    dict(pt=None, src="massif", toward=(-20.0, 16.0), width=1.9, reach=1.2, z_bot=-16.0, stream=1.8, segs=16),
]
SAT_FIELDS = [("farm", 27.4, 24.6, 2.6, 3.0, 8.0, "wheat"), ("farm", 30.3, 25.4, 2.3, 2.8, 8.0, "greens"),
              ("farm", 29.0, 28.6, 3.0, 2.2, 8.0, "plowed")]


def sat_placements():
    out = []

    def add(asset, x, y, rz=0.0, s=1.0, on="massif", z=None):
        out.append(dict(asset=asset, x=x, y=y, rz=rz, s=s, on=on, z=z, tilt=(0.0, 0.0)))

    bs = 0.5
    add("Bld_Cottage_Muted", -31.0, 25.0, -40, bs, "massif")
    add("Prop_Barrel", -29.8, 24.3, 0, 0.8, "massif")
    add("Bld_MineHoist", 9.0, 27.6, 12, bs, "ridge")
    add("Res_StonePile", 7.2, 26.4, 15, 0.9, "ridge")
    add("Res_OrePile_Iron", 11.0, 26.6, 0, 0.8, "ridge")
    add("Prop_Minecart_Ore", 7.6, 28.6, 12, 0.85, "ridge")
    add("Prop_Rails", 6.4, 29.0, 12, 0.9, "ridge")
    add("Prop_Pickaxe", 10.4, 25.6, 40, 0.9, "ridge")
    add("Bld_House_Stone_Muted", -3.5, 28.2, -20, bs, "ridge")
    add("Bld_Barn", 31.5, 22.8, -80, bs, "farm")
    add("Env_Haystack", 26.4, 22.4, 0, 0.7, "farm")
    add("Env_Haystack", 33.2, 27.0, 30, 0.65, "farm")
    return out


SAT_FOREST = [
    ("massif", [("Tree_Pine_A", 4), ("Tree_Pine_B", 4), ("Tree_Pine_C", 2), ("Tree_Round_A", 1.2)], 0.3, (0.95, 1.35)),
    ("ridge", [("Tree_Pine_A", 3), ("Tree_Pine_B", 3), ("Tree_Round_A", 1.5), ("Tree_Round_B", 1.5), ("Bush_A", 1)],
     0.14, (0.9, 1.25)),
    ("farm", [("Tree_Round_A", 2), ("Tree_Round_B", 2), ("Bush_Berry", 1), ("Bush_A", 1)], 0.05, (0.8, 1.1)),
    ("rock_w", [("Tree_Pine_B", 2), ("Rock_Medium", 1), ("Bush_B", 1)], 0.25, (0.8, 1.1)),
    ("rock_e", [("Tree_Round_B", 2), ("Rock_Medium", 1), ("Bush_A", 1)], 0.2, (0.8, 1.1)),
    ("rock_s", [("Rock_Medium", 1), ("Bush_B", 1)], 0.2, (0.7, 1.0)),
]
SAT_GROUND = [("massif", 0.1), ("ridge", 0.12), ("farm", 0.12), ("rock_w", 0.1), ("rock_e", 0.1)]

# облака вокруг всего (от края сетки 40 x 40 и спутников) и дальние острова
CLOUDS = dict(items=[("Env_CloudPuff_A", 3), ("Env_CloudPuff_B", 2), ("Env_CloudPuff_C", 2)],
              center=(0.0, 3.0),
              layers=[dict(ring=(2.0, 48.0), n=52, z=(-15.0, -9.0), scale=(1.3, 2.5)),
                      dict(ring=(-2.0, 7.0), n=26, z=(-24.0, -15.0), scale=(1.7, 2.9))])
DISTANT = [
    ((-52.0, 40.0), 3.0, 6.0, 41, 6),
    ((48.0, 44.0), 5.0, 5.0, 42, 4),
    ((58.0, 2.0), -3.0, 6.5, 43, 6),
    ((-56.0, -12.0), -5.0, 4.5, 44, 3),
    ((-6.0, 62.0), 6.0, 8.0, 45, 8),
    ((36.0, -34.0), -8.0, 3.0, 46, 2),
    ((-34.0, -36.0), -9.0, 2.0, 47, 0),
]

# look-dev как у макета острова; туман и резкость — под расстояние камеры каждого этапа
LOOKDEV = dict(I.LOOKDEV)
LOOKDEV["shots"] = {
    # туман (начало, длина, макс.), резкость (ближн. полная, ближн. резко, дальн. резко, дальн. полная, радиус)
    "start": dict(fog=(26.0, 62.0, 0.85), dof=(0.0, 0.0, 45.0, 66.0, 6.0)),
    "mid": dict(fog=(42.0, 80.0, 0.85), dof=(0.0, 0.0, 62.0, 90.0, 6.0)),
    "max": dict(fog=(58.0, 90.0, 0.85), dof=(0.0, 0.0, 86.0, 125.0, 6.0)),
}
