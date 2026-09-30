"""
Делянки полей колонии: прямоугольники поверх террасы с рядами посевов.

  wheat   рожь: ряды-гребни с зубчатым верхом (колосья), между рядами борозды
  greens  грядки: круглые кочаны на тёмной земле
  plowed  пашня: пологие валики светлой земли на тёмной

Ряды идут вдоль локальной оси X делянки; высота берётся из террасы (terrace.height), поэтому
делянка ложится и на пологие холмы. Цвета — swatch-и палитры, без новых рамп.
"""
import math
import random
from build_vitaria import SW_UV, p_prism, p_ico, p_cyl, by_normal


def _quad(a, pts, col):
    bm, uvl = a.bm, a.uv
    vs = [bm.verts.new(p) for p in pts]
    try:
        f = bm.faces.new(vs)
    except ValueError:
        return None
    f.normal_update()
    if f.normal.z < 0:
        f.normal_flip()
    for l in f.loops:
        l[uvl].uv = SW_UV[col]
    return f


WHEAT = by_normal("wheat_light", "wheat", "wheat_dark", 0.62)
CABBAGE = by_normal("leaf_light", "leaf_mid", "leaf_dark", 0.5)
RIDGE = by_normal("soil_light", "soil_mid", "soil_mid", 0.5)


def field_patch(a, terrace, cx, cy, w, d, ang, kind="wheat", seed=0, lift=0.02):
    """Делянка w x d (м) с центром (cx, cy), повернута на ang градусов."""
    rng = random.Random(seed)
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))

    def P(u, v, dz=0.0):
        x = cx + u * c - v * s
        y = cy + u * s + v * c
        return (x, y, terrace.height(x, y) + lift + dz)

    # земля делянки (кромка 8 см шире посевов): сетка 0.8 м, чтобы ложилась по холмам
    border = 0.08
    soil = {"wheat": "wheat_dark", "greens": "soil_dark", "plowed": "soil_dark"}[kind]
    nu, nv = max(2, int(math.ceil((w + 2 * border) / 0.8))), max(2, int(math.ceil((d + 2 * border) / 0.8)))
    for i in range(nu):
        for j in range(nv):
            u0 = -w / 2 - border + (w + 2 * border) * i / nu
            u1 = -w / 2 - border + (w + 2 * border) * (i + 1) / nu
            v0 = -d / 2 - border + (d + 2 * border) * j / nv
            v1 = -d / 2 - border + (d + 2 * border) * (j + 1) / nv
            _quad(a, [P(u0, v0), P(u1, v0), P(u1, v1), P(u0, v1)], soil)
    rows = max(3, int(round(d / (0.24 if kind == "wheat" else 0.52))))
    step = d / rows
    for r in range(rows):
        vm = -d / 2 + (r + 0.5) * step
        if kind == "greens":
            u = -w / 2 + rng.uniform(0.12, 0.22)
            while u < w / 2 - 0.12:
                x, y, z = P(u, vm + rng.uniform(-0.03, 0.03))
                rr = rng.uniform(0.13, 0.17)
                a.add(p_ico(rr, 1, loc=(x, y, z), scl=(1, 1, 0.75), jitter=0.12, rng=rng,
                            rot=(0, 0, rng.uniform(0, 360)), cut=0.0), CABBAGE)
                u += rng.uniform(0.34, 0.42)
            continue
        if kind == "wheat":
            # колосья: плотный ковёр четырёхгранных пик по ряду (как у KayKit), шаг 0.17–0.21 м
            u = -w / 2 + rng.uniform(0.06, 0.12)
            while u < w / 2 - 0.06:
                x, y, z = P(u, vm + rng.uniform(-0.05, 0.05))
                h = rng.uniform(0.2, 0.3)
                a.add(p_cyl(rng.uniform(0.085, 0.11), 0.0, h, 4, loc=(x, y, z - 0.01), spin=rng.uniform(0, 90)),
                      WHEAT)
                u += rng.uniform(0.17, 0.21)
            continue
        # пашня: пологие валики кусками по 0.8–1.4 м
        u = -w / 2 + rng.uniform(0.03, 0.08)
        while u < w / 2 - 0.2:
            L = min(rng.uniform(0.8, 1.4), w / 2 - u - 0.04)
            if L < 0.25:
                break
            x, y, z = P(u + L / 2, vm)
            h = rng.uniform(0.07, 0.1)
            wd = step * 0.6
            sec = [(-wd / 2, 0.0), (wd / 2, 0.0), (wd * 0.22, h), (-wd * 0.22, h)]
            a.add(p_prism(sec, L, loc=(x, y, z), rot=(0, 0, ang + 90)), RIDGE)
            u += L + rng.uniform(0.04, 0.1)


def dirt_yard(a, terrace, x, y, r, seed=0, lift=0.02):
    """Утоптанная площадка: светлая середина, тёмный неровный край (рампа dirt), как у дорог."""
    from build_vitaria import ramp_uv
    rng = random.Random(seed * 17 + 3)
    bm, uvl = a.bm, a.uv
    n = 18
    c = bm.verts.new((x, y, terrace.height(x, y) + lift))
    rings = []
    for f, tt, dz in ((0.6, 0.72, 0.0), (1.0, 0.1, -0.008)):
        ring = []
        for k in range(n):
            ang = math.tau * k / n
            rr = r * (1 + 0.12 * math.sin(ang * 3 + seed) + rng.uniform(-0.06, 0.06)) * f
            px, py = x + math.cos(ang) * rr, y + math.sin(ang) * rr
            ring.append((bm.verts.new((px, py, terrace.height(px, py) + lift + dz)),
                         tt + rng.uniform(-0.06, 0.06)))
        rings.append(ring)
    tmap = {c: 0.86}
    for ring in rings:
        for v, t in ring:
            tmap[v] = t
    polys = [(c, rings[0][k][0], rings[0][(k + 1) % n][0]) for k in range(n)]
    polys += [(rings[0][k][0], rings[1][k][0], rings[1][(k + 1) % n][0], rings[0][(k + 1) % n][0]) for k in range(n)]
    for q in polys:
        f = bm.faces.new(q)
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = ramp_uv("dirt", tmap[l.vert])
