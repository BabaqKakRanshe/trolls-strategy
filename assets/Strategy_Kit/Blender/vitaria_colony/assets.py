"""
Новые ассеты колонии (категория Colony в раскладке; ColonyDecor — только внутри Colony_Meadow_Scatter).

  Env_Clover_A/B     плоские пятна клевера с цветками (1–2 см): оживляют траву поля стройки,
                     не торчат из-под зданий и не спорят с подсветкой клеток
  Env_Pebbles_Flat   два-три плоских камешка вровень с травой (поле стройки)
  Env_Haystack       стог сена у полей
  Bld_Watermill      водяная мельница на кромке берега; колесо — отдельный Bld_Watermill_Wheel (крутится)

Детали зданий колонии (вместо голых кубов Unity в префабах Assets/Game/Prefabs/Buildings):
  Prop_Doorstep      каменный порог у двери (вместо куба EntranceSill), пивот — низ по центру
  Env_EntrancePath   утоптанная земля с плитками перед входом (вместо EntranceApproach), пивот — центр
  FX_Select_Corner   золотой уголок рамки выделения (4 штуки по углам следа вместо брусков SelectionRim);
                     пивот — внутренний угол, плечи вдоль +X и +Y: SW 0°, SE 90°, NE 180°, NW 270° вокруг вертикали
"""
import math
import random
from build_vitaria import p_ico, p_cyl, p_box, p_prism, by_normal
from vitaria_buildings.common import gable_roof, gable_wall, plank_door, window, stone_base, cornice

HAY = by_normal("wheat_light", "wheat", "wheat_dark", 0.45)
STONE = by_normal("stone_light", "stone_mid", "stone_dark", 0.55)
PLANK = by_normal("wood_light", "wood_mid", "wood_dark", 0.6)

# Водяная мельница: корпус вдоль Y (фасад -Y), колесо на восточной стороне (+X), ось колеса вдоль Y.
# Ступица в осях мельницы — точка постановки Bld_Watermill_Wheel (как MILL_HUB у ветряной мельницы).
# Ступица ниже пивота: мельница стоит на кромке берега, колесо уходит в русло и черпает воду.
WMILL_W, WMILL_D = 2.9, 2.3
WHEEL_R, WHEEL_T = 1.05, 0.46
WMILL_HUB = (WMILL_W / 2 + 0.52, 0.1, -0.62)


def _disc(a, rng, x, y, r, col, h=0.012, n=7):
    a.add(p_cyl(r, r * 0.92, h, n, loc=(x, y, 0.0), spin=rng.uniform(0, 60)), col)


def _patch_ramp(a, rng, x, y, r, h, ramp, t, n=8):
    """Плоское пятно травы цветом рампы (как земля террасы): n-угольник на высоте h."""
    from build_vitaria import ramp_uv
    bm, uvl = a.bm, a.uv
    a0 = rng.uniform(0, math.tau)
    vs = []
    for k in range(n):
        ang = a0 + math.tau * k / n
        rr = r * rng.uniform(0.82, 1.12)
        vs.append(bm.verts.new((x + math.cos(ang) * rr, y + math.sin(ang) * rr, h)))
    f = bm.faces.new(vs)
    f.normal_update()
    if f.normal.z < 0:
        f.normal_flip()
    for l in f.loops:
        l[uvl].uv = ramp_uv(ramp, t)


def clover(seed, flowers=("flower_w",), n=4):
    """Пятна травы на тон темнее поля (цвета той же рампы arena_grass) и редкие цветки."""
    def build(a):
        rng = random.Random(seed)
        for k in range(n):
            ang = rng.uniform(0, math.tau)
            d = rng.uniform(0.0, 0.22)
            r = rng.uniform(0.12, 0.22)
            _patch_ramp(a, rng, math.cos(ang) * d, math.sin(ang) * d, r, 0.006 + 0.002 * k, "arena_grass",
                        rng.uniform(0.0, 0.12))
        for k in range(rng.randint(2, 4)):
            ang = rng.uniform(0, math.tau)
            d = rng.uniform(0.05, 0.25)
            a.add(p_cyl(0.035, 0.03, 0.022, 5, loc=(math.cos(ang) * d, math.sin(ang) * d, 0.0),
                        spin=rng.uniform(0, 60)), rng.choice(flowers))
    return build


def pebbles_flat(a):
    rng = random.Random(41)
    for k in range(3):
        ang = rng.uniform(0, math.tau)
        d = rng.uniform(0.05, 0.2)
        r = rng.uniform(0.07, 0.12)
        a.add(p_ico(r, 1, loc=(math.cos(ang) * d, math.sin(ang) * d, 0.0), scl=(1, rng.uniform(0.7, 1), 0.35),
                    jitter=0.2, rng=rng, cut=-0.001), by_normal("stone_light", "stone_mid", "stone_mid", 0.5))


def haystack(a):
    rng = random.Random(53)
    a.add(p_ico(0.62, 2, loc=(0, 0, 0.0), scl=(1, 1, 1.15), jitter=0.06, rng=rng, cut=0.0), HAY)
    a.add(p_cyl(0.66, 0.6, 0.12, 10, loc=(0, 0, 0.0)), "wheat_dark")
    # жердь по макушке
    a.add(p_cyl(0.03, 0.025, 0.4, 5, loc=(0, 0, 0.62)), "wood_dark")


def watermill(a):
    rng = random.Random(71)
    w, d = WMILL_W, WMILL_D
    stone_base(a, w + 0.3, d + 0.3, h=0.3)
    # низ стен — камень, верх — дощатый сруб со стойками по углам
    a.add(p_box((w, d, 0.8), loc=(0, 0, 0.3 + 0.4), bevel=0.05), STONE)
    for k in range(14):                                        # выступающие камни кладки
        side = rng.choice((-1, 1))
        if rng.random() < 0.5:
            x, y, rz = rng.uniform(-w / 2 + 0.3, w / 2 - 0.3), side * (d / 2 + 0.02), 0
        else:
            x, y, rz = side * (w / 2 + 0.02), rng.uniform(-d / 2 + 0.3, d / 2 - 0.3), 90
        a.add(p_box((rng.uniform(0.3, 0.5), 0.08, rng.uniform(0.16, 0.24)),
                    loc=(x, y, rng.uniform(0.45, 0.95)), rot=(0, 0, rz), bevel=0.025),
              rng.choice(["stone_light", "stone_mid", "stone_dark"]))
    zt = 2.25
    a.add(p_box((w - 0.08, d - 0.08, zt - 1.1), loc=(0, 0, 1.1 + (zt - 1.1) / 2), bevel=0.03), "wood_mid")
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.18, 0.18, zt - 1.05), loc=(sx * (w / 2 - 0.04), sy * (d / 2 - 0.04), 1.05 + (zt - 1.05) / 2),
                        bevel=0.03), "wood_dark")
    cornice(a, w, d, 1.1, t=0.14)
    cornice(a, w, d, zt, t=0.16)
    for sy in (-1, 1):                                          # доски обшивки: вертикальные рейки
        for k in range(5):
            x = -w / 2 + 0.45 + k * (w - 0.9) / 4
            a.add(p_box((0.06, 0.05, zt - 1.25), loc=(x, sy * (d / 2 - 0.01), 1.18 + (zt - 1.25) / 2), bevel=0.012),
                  "wood_dark")
    zr = gable_roof(a, w, d, zt, pitch_deg=38.0, ox=0.26, oy=0.3)
    gable_wall(a, w, zt, zr, d, col="wood_light")
    plank_door(a, -d / 2 - 0.02, 0.3, w=0.6, h=1.05, x=-0.55)
    window(a, (0.55, -d / 2 - 0.03, 1.55), w=0.42, h=0.46)
    window(a, (0.0, -d / 2 - 0.03, zt + 0.55), w=0.34, h=0.38, shutters=False)
    window(a, (w / 2 + 0.03, -0.5, 1.6), rot=(0, 0, 90), w=0.4, h=0.44)
    # ось и опоры колеса: вал из стены, наружная стойка-козлы уходит в воду
    hx, hy, hz = WMILL_HUB
    a.add(p_cyl(0.09, 0.09, 1.25, 8, loc=(w / 2 - 0.1, hy, hz), rot=(0, 90, 0)), "wood_dark")
    for sy in (-1, 1):
        a.add(p_box((0.16, 0.16, 1.9), loc=(hx + WHEEL_T / 2 + 0.2, hy + sy * 0.22, hz - 0.45),
                    rot=(sy * 8, 0, 0), bevel=0.03), "wood_dark")
    a.add(p_box((0.2, 0.62, 0.14), loc=(hx + WHEEL_T / 2 + 0.2, hy, hz + 0.18), bevel=0.03), "wood_mid")
    # каменная стенка протока под колесом (видна над водой)
    a.add(p_box((0.3, d + 0.2, 1.3), loc=(w / 2 + 0.05, 0, -0.35), bevel=0.05), STONE)
    # мешки муки у двери
    a.add(p_ico(0.2, 1, loc=(-1.1, -d / 2 - 0.35, 0.3), scl=(1, 0.8, 1.1), cut=-0.19), "canvas")
    a.add(p_ico(0.17, 1, loc=(-0.95, -d / 2 - 0.62, 0.28), scl=(1, 0.8, 1.1), cut=-0.16), "canvas_dark")


def watermill_wheel(a):
    """Колесо: ступица в начале координат, ось вдоль Y (как у крыльев мельницы). В Unity крутится
    вокруг локальной Z (spin в раскладке)."""
    r, t = WHEEL_R, WHEEL_T
    for sy in (-1, 1):                                          # два обода: кольцо из 12 брусков
        n = 12
        for k in range(n):
            ang = (k + 0.5) * 360.0 / n
            ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
            chord = 2 * r * math.sin(math.pi / n) + 0.04
            a.add(p_box((chord, 0.08, 0.13), loc=(ca * r, sy * t / 2, sa * r), rot=(0, -(ang + 90), 0), bevel=0.02),
                  "wood_dark")
    for k in range(10):                                         # лопасти
        ang = math.radians(k * 36)
        cx, cz = math.cos(ang) * (r - 0.18), math.sin(ang) * (r - 0.18)
        a.add(p_box((0.42, t + 0.06, 0.07), loc=(cx, 0, cz), rot=(0, -math.degrees(ang), 0), bevel=0.015), PLANK)
    for k in range(5):                                          # спицы (через центр)
        ang = k * 36
        for sy in (-1, 1):
            a.add(p_box((2 * r - 0.25, 0.07, 0.08), loc=(0, sy * t / 2, 0), rot=(0, ang, 0), bevel=0.015), "wood_mid")
    a.add(p_cyl(0.2, 0.2, t + 0.12, 8, loc=(0, -(t + 0.12) / 2, 0), rot=(-90, 0, 0)), "wood_dark")


def doorstep(a):
    """Порог: широкая нижняя плита и узкая верхняя, отступ к двери (+Y). Натуральный размер 0.52 x 0.26 м."""
    a.add(p_box((0.52, 0.26, 0.07), loc=(0, 0, 0.035), bevel=0.02), STONE)
    a.add(p_box((0.44, 0.16, 0.05), loc=(0, 0.04, 0.07 + 0.025), bevel=0.018),
          by_normal("stone_light", "stone_mid", "stone_mid", 0.55))


def entrance_path(a):
    """Пятно утоптанной земли 0.7 x 0.66 м (рампа dirt: светлая середина, тёмный край) и три плитки."""
    from build_vitaria import ramp_uv
    rng = random.Random(83)
    bm, uvl = a.bm, a.uv
    n = 14
    center = bm.verts.new((0.0, 0.0, 0.012))
    ring = []
    for k in range(n):
        ang = math.tau * k / n
        r = 0.34 * (1 + 0.1 * math.sin(ang * 3 + 0.4) + rng.uniform(-0.05, 0.05))
        ring.append(bm.verts.new((math.cos(ang) * r * 1.03, math.sin(ang) * r * 0.97, 0.004)))
    for k in range(n):
        f = bm.faces.new((center, ring[k], ring[(k + 1) % n]))
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = ramp_uv("dirt", 0.82 if l.vert is center else 0.28)
    for (x, y, r) in ((-0.1, 0.12, 0.09), (0.11, -0.02, 0.1), (-0.04, -0.17, 0.085)):
        a.add(p_cyl(r, r * 0.9, 0.022, 6, loc=(x, y, 0.004), spin=rng.uniform(0, 60), bevel=0.008),
              by_normal("stone_light", "stone_mid", "stone_mid", 0.5))


def select_corner(a):
    """Уголок рамки выделения: два плеча по 0.34 м вдоль +X и +Y от внутреннего угла (пивот)."""
    L, w, h = 0.34, 0.07, 0.045
    gold = by_normal("gold_light", "gold", "gold_dark", 0.55)
    a.add(p_box((L, w, h), loc=(L / 2 - w / 2, 0, h / 2), bevel=0.015), gold)
    a.add(p_box((w, L - w, h), loc=(0, (L - w) / 2 + w / 2, h / 2), bevel=0.015), gold)


ASSETS = [
    ("ColonyDecor", "Env_Clover_A", "Клевер с белыми цветками (поле стройки)", clover(61, ("flower_w",))),
    ("ColonyDecor", "Env_Clover_B", "Клевер с жёлтыми цветками (поле стройки)", clover(62, ("flower_y", "flower_w"), 3)),
    ("ColonyDecor", "Env_Pebbles_Flat", "Плоские камешки (поле стройки)", pebbles_flat),
    ("Colony", "Env_Haystack", "Стог сена", haystack),
    ("Colony", "Bld_Watermill", "Водяная мельница", watermill),
    ("Colony", "Bld_Watermill_Wheel", "Колесо водяной мельницы (крутится)", watermill_wheel),
    ("Colony", "Prop_Doorstep", "Порог у двери здания", doorstep),
    ("Colony", "Env_EntrancePath", "Утоптанная земля перед входом", entrance_path),
    ("Colony", "FX_Select_Corner", "Уголок рамки выделения здания", select_corner),
]
