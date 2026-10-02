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
# Колесо крупное и вынесено от стены на длинном валу: в кадре колонии (масштаб 0.5) оно должно
# целиком стоять в воде, низом на 0.2 м под водой, верхом над берегом.
WMILL_W, WMILL_D = 2.9, 2.3
WHEEL_R, WHEEL_T = 1.3, 0.46
WMILL_HUB = (WMILL_W / 2 + 0.78, 0.1, -0.9)


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


def flower_clump(seed, heads=("flower_y",), n=11, r=0.28):
    """Куртина цветов: два-три низких купола листвы и головки цветков по их поверхности — жёлтые
    шапки в траве, как на референсах острова. Пивот — середина на земле, ~300 треугольников."""
    def build(a):
        rng = random.Random(seed)
        leaf = by_normal("leaf_mid", "leaf_dark", "leaf_dark", 0.5)
        domes = []
        for k in range(rng.randint(2, 3)):
            ang = rng.uniform(0, math.tau)
            d = rng.uniform(0.0, r * 0.4)
            rr = rng.uniform(0.6, 0.8) * r
            cx, cy, hz = math.cos(ang) * d, math.sin(ang) * d, rng.uniform(0.45, 0.6)
            domes.append((cx, cy, rr, hz))
            a.add(p_ico(rr, 1, loc=(cx, cy, -0.02), scl=(1, rng.uniform(0.8, 1.0), hz), jitter=0.12, rng=rng,
                        cut=0.0), leaf)
        for k in range(n):
            cx, cy, rr, hz = rng.choice(domes)
            ang = rng.uniform(0, math.tau)
            d = rr * math.sqrt(rng.random()) * 0.85
            x, y = cx + math.cos(ang) * d, cy + math.sin(ang) * d
            z = -0.02 + rr * hz * math.sqrt(max(0.0, 1.0 - (d / rr) ** 2)) + rng.uniform(0.0, 0.025)
            a.add(p_ico(rng.uniform(0.038, 0.058), 0, loc=(x, y, z), scl=(1, 1, 0.75),
                        rot=(0, 0, rng.uniform(0, 72))), rng.choice(heads))
    return build


def _add_ramp_z(a, parts, ramp, smooth=True):
    """Влить части с UV рампы по высоте вершины (низ — 0, верх — 1) и сглаженными нормалями."""
    from build_vitaria import ramp_uv
    zs = [v.co.z for p in parts for v in p.verts]
    z0, z1 = min(zs), max(zs)
    for part in parts:
        vmap = {v: a.bm.verts.new(v.co) for v in part.verts}
        for f in part.faces:
            try:
                nf = a.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.smooth = smooth
            for l in nf.loops:
                l[a.uv].uv = ramp_uv(ramp, (l.vert.co.z - z0) / max(1e-6, z1 - z0))
        part.free()


def cloud_puff(seed, n=6, L=5.0, H=1.8):
    """Облако у острова: цепочка сфер вдоль X, крупнее к середине, плоское дно. Цвет — рампа cloud
    по высоте (низ голубоватый, верх белый). Пивот — середина дна."""
    def build(a):
        rng = random.Random(seed)
        parts = []
        for k in range(n):
            u = (k / (n - 1) - 0.5) if n > 1 else 0.0
            x = u * L + rng.uniform(-0.15, 0.15) * L / n
            rr = H * (1.0 - 1.1 * abs(u)) * rng.uniform(0.85, 1.1)
            rr = max(rr, 0.35 * H)
            y = rng.uniform(-0.25, 0.25) * H
            z = rr * rng.uniform(0.05, 0.25)
            parts.append(p_ico(rr, 2, loc=(x, y, z), scl=(1, rng.uniform(0.75, 0.95), 0.66),
                               jitter=0.05, rng=rng, cut=-rr * 0.3))
        _add_ramp_z(a, parts, "cloud")
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
    a.add(p_cyl(0.09, 0.09, hx - w / 2 + 0.55, 8, loc=(w / 2 - 0.1, hy, hz), rot=(0, 90, 0)), "wood_dark")
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


PAD_TOP = 0.03             # верх подложки здания (Env_Footprint_*): выше клевера поля (2.2 см)


def _path_z(y):
    """Высота пятна входа: задняя половина (+Y, к зданию) лежит на подложке, передняя (-Y) — на траве.
    Пятно ставится серединой в 8 см перед краем следа, край подложки
    с откосом — в 5–17 см внутри следа, то есть на локальных y 0.13–0.25."""
    from build_vitaria import smoothstep
    return 0.006 + (PAD_TOP - 0.002) * smoothstep(0.1, 0.27, y)


def entrance_path(a):
    """Пятно утоптанной земли 0.7 x 0.66 м (рампа dirt: светлая середина, тёмный край) и три плитки.
    Пивот — центр пятна на земле; ставится на кромку следа перед входом: задний край на подложке
    здания (PAD_TOP), передний на траве."""
    from build_vitaria import ramp_uv
    rng = random.Random(83)
    bm, uvl = a.bm, a.uv
    n = 14
    center = bm.verts.new((0.0, 0.0, _path_z(0.0) + 0.008))
    ring = []
    for k in range(n):
        ang = math.tau * k / n
        r = 0.34 * (1 + 0.1 * math.sin(ang * 3 + 0.4) + rng.uniform(-0.05, 0.05))
        x, y = math.cos(ang) * r * 1.03, math.sin(ang) * r * 0.97
        ring.append(bm.verts.new((x, y, _path_z(y) + 0.002)))
    for k in range(n):
        f = bm.faces.new((center, ring[k], ring[(k + 1) % n]))
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = ramp_uv("dirt", 0.82 if l.vert is center else 0.28)
    for (x, y, r) in ((-0.1, 0.12, 0.09), (0.11, -0.02, 0.1), (-0.04, -0.17, 0.085)):
        a.add(p_cyl(r, r * 0.9, 0.022, 6, loc=(x, y, _path_z(y) + 0.002), spin=rng.uniform(0, 60), bevel=0.008),
              by_normal("stone_light", "stone_mid", "stone_mid", 0.5))


def footprint_pad(w, d, seed=0):
    """Подложка здания (метры игры, масштаб 1): утоптанная земля по следу w x d без 5 см по краю,
    скруглённые углы, неровная кромка. Верх PAD_TOP закрывает клевер поля под зданием; у всех
    зданий один приём основания. Пивот — середина следа на земле."""
    def build(a):
        from build_vitaria import ramp_uv
        rng = random.Random(101 + seed)
        bm, uvl = a.bm, a.uv
        hw, hd, rc = w / 2 - 0.05, d / 2 - 0.05, 0.22
        sx, sy = hw - rc, hd - rc
        # скруглённый прямоугольник, точки равномерно по периметру: (точка, наружная нормаль)
        segs = []
        for (x0, y0, x1, y1, nx, ny) in ((-sx, -hd, sx, -hd, 0, -1), (hw, -sy, hw, sy, 1, 0),
                                         (sx, hd, -sx, hd, 0, 1), (-hw, sy, -hw, -sy, -1, 0)):
            segs.append(("line", (x0, y0, x1, y1, nx, ny), math.hypot(x1 - x0, y1 - y0)))
        corners = ((sx, -sy, -90), (sx, sy, 0), (-sx, sy, 90), (-sx, -sy, 180))
        order = [segs[0], ("arc", corners[0], rc * math.pi / 2), segs[1], ("arc", corners[1], rc * math.pi / 2),
                 segs[2], ("arc", corners[2], rc * math.pi / 2), segs[3], ("arc", corners[3], rc * math.pi / 2)]
        total = sum(L for _, _, L in order)
        n = max(40, int(total / 0.12))

        def at(s):
            for kind, p, L in order:
                if s <= L:
                    if kind == "line":
                        x0, y0, x1, y1, nx, ny = p
                        t = s / L
                        return x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, nx, ny
                    cx, cy, a0 = p
                    ang = math.radians(a0 + 90 * s / L)
                    return cx + rc * math.cos(ang), cy + rc * math.sin(ang), math.cos(ang), math.sin(ang)
                s -= L
            return at(0.0)
        outer, inner, mid = [], [], []
        for k in range(n):
            x, y, nx, ny = at(total * k / n)
            jit = 0.03 * math.sin(k * 0.9 + seed) + rng.uniform(-0.018, 0.018)
            x, y = x + nx * jit, y + ny * jit
            outer.append(bm.verts.new((x, y, 0.004)))
            inner.append(bm.verts.new((x - nx * 0.12, y - ny * 0.12, PAD_TOP)))
            mid.append(bm.verts.new((x * 0.62, y * 0.62, PAD_TOP + 0.002)))
        c = bm.verts.new((0.0, 0.0, PAD_TOP + 0.002))
        # светлая утоптанная земля, край на тон темнее: тёмная кайма превращала следы в коричневые плитки
        tcol = {c: 0.8}
        for v in outer:
            tcol[v] = 0.36 + rng.uniform(-0.05, 0.05)
        for v in inner:
            tcol[v] = 0.6 + rng.uniform(-0.05, 0.05)
        for v in mid:
            tcol[v] = 0.72 + rng.uniform(-0.06, 0.06)
        faces = []
        for k in range(n):
            j = (k + 1) % n
            faces.append((outer[k], outer[j], inner[j], inner[k]))
            faces.append((inner[k], inner[j], mid[j], mid[k]))
            faces.append((mid[k], mid[j], c))
        for q in faces:
            f = bm.faces.new(q)
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
            for l in f.loops:
                l[uvl].uv = ramp_uv("dirt", tcol[l.vert])
    return build


def select_corner(a):
    """Уголок рамки выделения: два плеча по 0.34 м вдоль +X и +Y от внутреннего угла (пивот)."""
    L, w, h = 0.34, 0.07, 0.045
    gold = by_normal("gold_light", "gold", "gold_dark", 0.55)
    a.add(p_box((L, w, h), loc=(L / 2 - w / 2, 0, h / 2), bevel=0.015), gold)
    a.add(p_box((w, L - w, h), loc=(0, (L - w) / 2 + w / 2, h / 2), bevel=0.015), gold)


def cliff_stairs(H=1.8, L=2.2, W=0.7, land=1.5, n=8):
    """Деревянная лестница вдоль обрыва (метры игры, ставится в масштабе 1). Пивот — низ первой
    ступени на оси марша; марш идёт вдоль +X, обрыв со стороны +Y; наверху площадка уходит на land
    м в +Y — на кромку гряды."""
    def build(a):
        rng = random.Random(97)
        run = L / n
        for k in range(n):                                        # ступени
            z = (k + 1) * H / n
            a.add(p_box((run + 0.03, W, 0.06), loc=((k + 0.5) * run, 0.0, z - 0.03),
                        rot=(0, 0, rng.uniform(-2, 2)), bevel=0.012), PLANK)
        ang = math.degrees(math.atan2(H, L))
        Ls = math.hypot(L, H) + 0.1
        for sy in (-1, 1):                                        # тетивы
            a.add(p_box((Ls, 0.07, 0.15), loc=(L / 2, sy * (W / 2 + 0.02), H / 2 - 0.08),
                        rot=(0, -ang, 0), bevel=0.015), "wood_dark")
        for k in range(3):                                        # стойки и поручень с внешней стороны
            x = 0.1 + k * (L - 0.2) / 2
            z0 = x / L * H - 0.1
            a.add(p_box((0.07, 0.07, 0.62), loc=(x, -W / 2 - 0.04, z0 + 0.31), bevel=0.012), "wood_dark")
        a.add(p_box((Ls, 0.06, 0.06), loc=(L / 2, -W / 2 - 0.04, H / 2 + 0.46), rot=(0, -ang, 0), bevel=0.012),
              "wood_mid")
        # площадка наверху: настил, балки, две ноги до земли
        D = W + land
        a.add(p_box((0.8, D, 0.07), loc=(L + 0.4, (D - W) / 2, H - 0.035), bevel=0.015), PLANK)
        for sy in (0.0, 1.0):
            a.add(p_box((0.08, 0.08, H), loc=(L + 0.72, -W / 2 + 0.06 + sy * 0.45, H / 2), bevel=0.012), "wood_dark")
        a.add(p_box((0.07, 0.07, 0.55), loc=(L + 0.76, -W / 2 - 0.02, H + 0.27), bevel=0.012), "wood_dark")
        a.add(p_box((0.06, 0.62, 0.06), loc=(L + 0.76, -W / 2 + 0.29, H + 0.5), bevel=0.012), "wood_mid")
        # камень под первой ступенью
        a.add(p_ico(0.22, 1, loc=(-0.12, 0.05, 0.0), scl=(1.2, 1.0, 0.45), jitter=0.2, rng=rng, cut=-0.02), STONE)
    return build


ASSETS = [
    ("ColonyDecor", "Env_Clover_A", "Клевер с белыми цветками (поле стройки)", clover(61, ("flower_w",))),
    ("ColonyDecor", "Env_Clover_B", "Клевер с жёлтыми цветками (поле стройки)", clover(62, ("flower_y", "flower_w"), 3)),
    ("ColonyDecor", "Env_Pebbles_Flat", "Плоские камешки (поле стройки)", pebbles_flat),
    ("ColonyDecor", "Prop_CliffStairs", "Деревянная лестница на гряду (метры игры)", cliff_stairs()),
    ("ColonyDecor", "Env_FlowerPatch_A", "Куртина жёлтых цветов", flower_clump(131, ("flower_y",), 11)),
    ("ColonyDecor", "Env_FlowerPatch_B", "Куртина жёлтых и белых цветов",
     flower_clump(132, ("flower_y", "flower_y", "flower_w"), 10)),
    ("Sky", "Env_CloudPuff_A", "Облако у острова, среднее", cloud_puff(141, 6, 5.0, 1.8)),
    ("Sky", "Env_CloudPuff_B", "Облако у острова, малое", cloud_puff(142, 4, 3.2, 1.4)),
    ("Sky", "Env_CloudPuff_C", "Облако у острова, длинное", cloud_puff(143, 8, 7.5, 2.1)),
    ("Colony", "Env_Haystack", "Стог сена", haystack),
    ("Colony", "Bld_Watermill", "Водяная мельница", watermill),
    ("Colony", "Bld_Watermill_Wheel", "Колесо водяной мельницы (крутится)", watermill_wheel),
    ("Colony", "Prop_Doorstep", "Порог у двери здания", doorstep),
    ("Colony", "Env_EntrancePath", "Утоптанная земля перед входом", entrance_path),
    ("Colony", "FX_Select_Corner", "Уголок рамки выделения здания", select_corner),
    ("Colony", "Env_Footprint_2x2", "Подложка здания 2x2 (утоптанная земля)", footprint_pad(2.0, 2.0, 1)),
    ("Colony", "Env_Footprint_3x3", "Подложка здания 3x3 (утоптанная земля)", footprint_pad(3.0, 3.0, 2)),
    ("Colony", "Env_Footprint_3x2", "Подложка здания 3x2 (утоптанная земля)", footprint_pad(3.0, 2.0, 3)),
]
