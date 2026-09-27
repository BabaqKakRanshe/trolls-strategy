"""
Рельеф арены: террасы (травяной верх + свес дёрна + гранёные скальные столбы), дороги,
вода, водопады и пена.

Всё строится в пространстве арены (board.py): X вправо, Y от камеры вглубь, Z вверх,
начало — середина поля. Контур террасы — замкнутая ломаная против часовой стрелки
(если смотреть сверху); наружная нормаль ребра смотрит вправо от направления обхода.

Как у KayKit: скала — это крупные столбы-призмы с 2–3 гранями на лицевой стороне, верх
каждого яруса светлее боков, между ярусами видна светлая полка. Трава — сетка с
плавным градиентом рампы «grass», как у острова кита.
"""
import math
import random
import bmesh
from mathutils import Vector, noise, geometry
from build_vitaria import SW_UV, ramp_uv, p_ico, clamp, smoothstep

WATER_Z = -5.2          # уровень озера вокруг плато: плато стоит столовой горой над водой
BED_Z = WATER_Z - 0.5   # докуда уходят столбы и берега под воду


# =========================================================================================
# 2D
# =========================================================================================
def signed_area(pts):
    s = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i][0], pts[i][1]
        x1, y1 = pts[(i + 1) % len(pts)][0], pts[(i + 1) % len(pts)][1]
        s += x0 * y1 - x1 * y0
    return s / 2.0


def ccw(pts):
    return list(pts) if signed_area(pts) > 0 else list(reversed(pts))


def _cr(p0, p1, p2, p3, t):
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                  (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t)


def closed_spline(ctrl, step=0.45):
    """Замкнутый Catmull-Rom через контрольные точки, равномерно ~step."""
    P = [Vector((p[0], p[1], 0.0)) for p in ctrl]
    n = len(P)
    dense = []
    for i in range(n):
        p0, p1, p2, p3 = P[i - 1], P[i], P[(i + 1) % n], P[(i + 2) % n]
        k = max(4, int((p2 - p1).length / (step * 0.5)))
        for j in range(k):
            dense.append(_cr(p0, p1, p2, p3, j / k))
    return resample_closed(dense, step)


def open_spline(ctrl, step=0.3):
    P = [Vector((p[0], p[1], 0.0)) for p in ctrl]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        k = max(2, int((p2 - p1).length / step))
        for j in range(k):
            out.append(_cr(p0, p1, p2, p3, j / k))
    out.append(P[-2].copy())
    return out


def resample_closed(pts, step):
    L = [0.0]
    n = len(pts)
    for i in range(n):
        L.append(L[-1] + (pts[(i + 1) % n] - pts[i]).length)
    total = L[-1]
    m = max(8, int(round(total / step)))
    out, j = [], 0
    for k in range(m):
        s = total * k / m
        while L[j + 1] < s:
            j += 1
        t = (s - L[j]) / max(1e-9, L[j + 1] - L[j])
        out.append(pts[j].lerp(pts[(j + 1) % n], t))
    return out


def edge_normals(pts):
    """Наружная нормаль в каждой вершине замкнутого CCW-контура (среднее двух рёбер)."""
    n = len(pts)
    out = []
    for i in range(n):
        a, b, c = pts[i - 1], pts[i], pts[(i + 1) % n]
        d0 = (b - a).normalized()
        d1 = (c - b).normalized()
        n0 = Vector((d0.y, -d0.x, 0.0))
        n1 = Vector((d1.y, -d1.x, 0.0))
        v = n0 + n1
        out.append(v.normalized() if v.length > 1e-6 else n1)
    return out


def tangents(pts):
    n = len(pts)
    return [(pts[(i + 1) % n] - pts[i - 1]).normalized() for i in range(n)]


def wobble(pts, amp, freq, seed):
    """Сдвиг контура по нормали шумом — «рваный» край без острых зубцов."""
    if amp <= 0:
        return pts
    nr = edge_normals(pts)
    out = []
    for p, nv in zip(pts, nr):
        k = noise.noise(Vector((p.x * freq, p.y * freq, seed * 3.1))) + \
            0.45 * noise.noise(Vector((p.x * freq * 2.7, p.y * freq * 2.7, seed * 5.3 + 2)))
        out.append(p + nv * (k * amp))
    return out


def point_in_poly(x, y, pts):
    inside = False
    n = len(pts)
    j = n - 1
    for i in range(n):
        xi, yi = pts[i][0], pts[i][1]
        xj, yj = pts[j][0], pts[j][1]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi:
            inside = not inside
        j = i
    return inside


def dist_to_outline(x, y, pts):
    best = 1e9
    p = Vector((x, y, 0.0))
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        ab = b - a
        t = clamp((p - a).dot(ab) / max(1e-9, ab.length_squared))
        best = min(best, (a + ab * t - p).length)
    return best


def _set_uv(face, uvl, fn):
    for l in face.loops:
        l[uvl].uv = fn(l.vert.co)


def _prism(bm, bottom, top):
    """Замкнутая призма из двух колец одинаковой длины. Возвращает её грани."""
    vb = [bm.verts.new(p) for p in bottom]
    vt = [bm.verts.new(p) for p in top]
    faces = []
    n = len(vb)
    try:
        faces.append(bm.faces.new(list(reversed(vb))))
        faces.append(bm.faces.new(vt))
    except ValueError:
        pass
    for i in range(n):
        j = (i + 1) % n
        try:
            faces.append(bm.faces.new((vb[i], vb[j], vt[j], vt[i])))
        except ValueError:
            pass
    return faces


# =========================================================================================
# цвета
# =========================================================================================
def cliff_color(seed, z_top=0.0, height=8.0):
    """Скала арены: голубовато-серая, как на макете. Полки светлые, низ тёмный; к воде скала
    темнеет — у макета подножие уходит в тень, а верх освещён."""
    def f(face):
        nz = face.normal.z
        if nz > 0.55:
            return "cliff_light"
        if nz < -0.5:
            return "cliff_deep"
        c = face.calc_center_median()
        depth = clamp((z_top - c.z) / max(1.0, height))          # 0 у кромки, 1 у воды
        k = noise.noise(c * 0.55 + Vector((seed * 1.7, 0.0, 3.3))) - depth * 0.7 + 0.05
        if k > 0.36:
            return "stone_mid"
        if k > -0.08:
            return "cliff_mid"
        if k > -0.46:
            return "cliff_dark"
        return "cliff_deep"
    return f


# =========================================================================================
# терраса
# =========================================================================================
class Terrace:
    """
    ctrl    контрольные точки контура (любой обход — развернём в CCW)
    z       высота травы у кромки
    bottom  низ скалы (под водой по умолчанию)
    hills   амплитуда холмов в глубине террасы (у кромки сходит в ноль)
    flat    функция (x, y) -> 0..1: где трава обязана быть ровно на z (поле боя)
    grid    шаг внутренней сетки травы
    cliff   строить ли скалу; open_ranges — дуги контура (доли 0..1) без скалы
            (там, где терраса утыкается в соседнюю и стена не видна)
    """

    def __init__(self, name, ctrl, z, bottom=BED_Z, wob=0.14, seed=0, hills=0.0, flat=None,
                 grid=0.7, grass=(0.42, 0.95), col_w=(1.3, 2.8), lip=True, cliff=True,
                 open_ranges=(), step=0.42, lean=0.1, batter=0.42, ramp="arena_grass", wide=0.22,
                 shelves=None, shelf_jit=0.3, notch=0.18):
        self.name, self.z, self.bottom, self.seed = name, z, bottom, seed
        self.hills, self.flat, self.grid, self.grass = hills, flat, grid, grass
        self.ramp, self.wide = ramp, wide          # рампа травы; доля широких скальных плит
        self.col_w, self.lip, self.cliff, self.open_ranges = col_w, lip, cliff, open_ranges
        self.lean, self.batter = lean, batter
        # полки: доли высоты скалы от кромки (0) до низа (1). Общие для всей террасы, поэтому
        # соседние столбы ломаются примерно на одной высоте и читаются сквозные светлые уступы,
        # как у скал KayKit; None — случайные разломы у каждого столба.
        self.shelves, self.shelf_jit, self.notch = shelves, shelf_jit, notch
        self.occluders = []          # контуры более высоких соседей: там столбы не нужны
        base = closed_spline(ccw(ctrl), step)
        self.outline = wobble(base, wob, 0.33, seed)
        self.normals = edge_normals(self.outline)
        self.tangents = tangents(self.outline)
        xs = [p.x for p in self.outline]
        ys = [p.y for p in self.outline]
        self.bbox = (min(xs), max(xs), min(ys), max(ys))

    # -------------------------------------------------------------- queries
    def contains(self, x, y, margin=0.0):
        if not point_in_poly(x, y, self.outline):
            return False
        return margin <= 0 or dist_to_outline(x, y, self.outline) >= margin

    def rim(self, x, y):
        return dist_to_outline(x, y, self.outline)

    def height(self, x, y, rim=None):
        z = self.z
        if self.hills:
            r = self.rim(x, y) if rim is None else rim
            k = smoothstep(0.6, 4.0, r)
            h = noise.noise(Vector((x * 0.09, y * 0.09, self.seed + 0.7))) * 0.7 + \
                noise.noise(Vector((x * 0.23, y * 0.23, self.seed + 4.1))) * 0.3
            z += k * self.hills * h
        else:
            r = self.rim(x, y) if rim is None else rim
            k = smoothstep(0.25, 1.4, r)
            z += k * 0.05 * noise.noise(Vector((x * 0.5, y * 0.5, self.seed + 1.3)))
        if self.flat is not None:
            w = self.flat(x, y)
            z = z * (1 - w) + self.z * w
        return z

    def grass_t(self, x, y, rim=None):
        lo, hi = self.grass
        n1 = noise.noise(Vector((x * 0.13, y * 0.13, self.seed + 4.2)))
        n2 = noise.noise(Vector((x * 0.47, y * 0.47, self.seed + 8.1)))
        r = self.rim(x, y) if rim is None else rim
        t = (lo + hi) / 2 + (hi - lo) * (0.55 * n1 + 0.2 * n2) - 0.12 * smoothstep(1.2, 0.0, r)
        return clamp(t, 0.0, 1.0)

    # -------------------------------------------------------------- top
    def build_top(self, a, ramp=None):
        ramp = ramp or self.ramp
        pts2 = [Vector((p.x, p.y)) for p in self.outline]
        n_out = len(pts2)
        rng = random.Random(self.seed * 7 + 1)
        x0, x1, y0, y1 = self.bbox
        g = self.grid
        inner = []
        yy = y0 + g * 0.5
        row = 0
        while yy < y1:
            xx = x0 + g * 0.5 + (g * 0.5 if row % 2 else 0.0)
            while xx < x1:
                px = xx + rng.uniform(-0.22, 0.22) * g
                py = yy + rng.uniform(-0.22, 0.22) * g
                if in_view(px, py, 2.0) and point_in_poly(px, py, self.outline) and \
                        dist_to_outline(px, py, self.outline) > g * 0.5:
                    inner.append(Vector((px, py)))
                xx += g
            yy += g * 0.866
            row += 1
        res = geometry.delaunay_2d_cdt(pts2 + inner, [], [list(range(n_out))], 1, 1e-5)
        vco, faces = res[0], res[2]
        bm, uvl = a.bm, a.uv
        verts = []
        for v in vco:
            r = dist_to_outline(v.x, v.y, self.outline)
            z = self.height(v.x, v.y, r) if r > 1e-4 else self.z
            verts.append((bm.verts.new((v.x, v.y, z)), self.grass_t(v.x, v.y, r)))
        used = 0
        for f in faces:
            if VIEW_BOX is not None and not any(in_view(vco[i].x, vco[i].y, 1.0) for i in f):
                continue
            try:
                nf = bm.faces.new([verts[i][0] for i in f])
            except ValueError:
                continue
            used += 1
            nf.normal_update()
            if nf.normal.z < 0:
                nf.normal_flip()
            tmap = {verts[i][0]: verts[i][1] for i in f}
            for l in nf.loops:
                l[uvl].uv = ramp_uv(ramp, tmap[l.vert])
        loose = [v for v, _ in verts if not v.link_faces]          # вершины отсечённых треугольников
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        return used

    # -------------------------------------------------------------- sod lip
    def build_lip(self, a):
        """Свес дёрна: светлая скруглённая кромка, тёмная «борода» травы, подворот под скалу."""
        if not self.lip:
            return
        bm, uvl = a.bm, a.uv
        # толстая «шапка» дёрна как на макете: скруглённая светлая кромка, тёмный навес травы,
        # подворот под скалу (верх скалы на LIP_DROP ниже травы прячется под ним)
        prof = [(0.0, 0.0), (0.15, -0.08), (0.2, -0.24), (0.07, -0.45), (-0.55, -0.5)]
        cols = [ramp_uv(self.ramp, 0.96), SW_UV["sod"], SW_UV["sod_dark"], SW_UV["soil_dark"]]
        rings = []
        for pi, (dn, dz) in enumerate(prof):
            ring = []
            for p, nv in zip(self.outline, self.normals):
                j = 0.0
                if pi in (2, 3):
                    j = 0.09 * noise.noise(Vector((p.x * 0.8, p.y * 0.8, self.seed + 11.0))) - \
                        0.08 * abs(noise.noise(Vector((p.x * 2.3, p.y * 2.3, self.seed + 13.0))))
                q = p + nv * dn
                ring.append(bm.verts.new((q.x, q.y, self.z + dz + j)))
            rings.append(ring)
        n = len(self.outline)
        keep = []
        for i in range(n):
            p0, p1 = self.outline[i], self.outline[(i + 1) % n]
            mid = (p0 + p1) / 2 + self.normals[i] * 0.3
            vis = in_view(p0.x, p0.y, 2.0) or in_view(p1.x, p1.y, 2.0)
            keep.append(vis and not any(point_in_poly(mid.x, mid.y, o) for o in self.occluders))
        for k in range(len(rings) - 1):
            for i in range(n):
                if not keep[i]:
                    continue
                j = (i + 1) % n
                try:
                    f = bm.faces.new((rings[k][i], rings[k + 1][i], rings[k + 1][j], rings[k][j]))
                except ValueError:
                    continue
                for l in f.loops:
                    l[uvl].uv = cols[k]
                f.normal_update()
        loose = [v for ring in rings for v in ring if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")

    # -------------------------------------------------------------- cliff
    def _open(self, s01):
        return any(lo <= s01 <= hi for lo, hi in self.open_ranges)

    def build_cliff(self, a, boulders=True):
        if not self.cliff:
            return
        rng = random.Random(self.seed * 13 + 5)
        pts, nrm, tan = self.outline, self.normals, self.tangents
        n = len(pts)
        L = [0.0]
        for i in range(n):
            L.append(L[-1] + (pts[(i + 1) % n] - pts[i]).length)
        total = L[-1]
        z_top = self.z - 0.36
        H = z_top - self.bottom
        colf = cliff_color(self.seed, z_top, H)
        if self.shelves is not None:
            n_tiers = len(self.shelves) + 1
        else:
            n_tiers = max(1, min(4, int(round(H / 2.9))))
        self._tiers = n_tiers
        s = rng.uniform(0, 0.6)
        j = 0
        while s < total:
            # в основном столбы, изредка широкая плита — ломает ритм «частокола»
            w = rng.uniform(2.3, 3.2) if rng.random() < self.wide else rng.uniform(*self.col_w)
            sc = s + w * 0.5
            while j + 1 < len(L) - 1 and L[j + 1] < sc:
                j += 1
            t = (sc - L[j]) / max(1e-9, L[j + 1] - L[j])
            i0, i1 = j % n, (j + 1) % n
            P = pts[i0].lerp(pts[i1], t)
            N = nrm[i0].lerp(nrm[i1], t).normalized()
            T = Vector((-N.y, N.x, 0.0))
            probe = P + N * 0.35
            buried = any(point_in_poly(probe.x, probe.y, o) for o in self.occluders)
            if not buried and not self._open((sc % total) / total) and in_view(P.x, P.y, 3.0):
                self._column(a, rng, P, N, T, w, z_top, n_tiers, colf)
            s += w * rng.uniform(0.74, 0.86)
        if boulders and self.bottom <= WATER_Z:
            self._waterline(a, rng, total, L)

    def _column(self, a, rng, P, N, T, w, z_top, n_tiers, colf):
        H = z_top - self.bottom
        if self.shelves is not None:
            cuts = sorted(min(0.9, max(0.08, c + rng.uniform(-1, 1) * self.shelf_jit / max(1.0, H)))
                          for c in self.shelves)
        else:
            cuts = sorted(rng.uniform(0.18, 0.82) for _ in range(n_tiers - 1))
        top = z_top - rng.uniform(0.0, 0.14)
        if rng.random() < self.notch:                       # выщербина: столб начинается ниже кромки
            top -= rng.uniform(0.35, 0.9)
        levels = [top] + [z_top - H * c + rng.uniform(-0.12, 0.12) for c in cuts] + [self.bottom]
        for k in range(1, len(levels) - 1):                 # ярус не тоньше 0.5 м
            levels[k] = min(levels[k], levels[k - 1] - 0.5)
        D = rng.uniform(1.1, 1.6)
        step0 = rng.uniform(-0.18, 0.2)                     # у всего столба свой вынос: рельеф в плане
        for k in range(len(levels) - 1):
            zt, zb = levels[k], levels[k + 1] - (0.03 if k < len(levels) - 2 else 0.0)
            if zt - zb < 0.2:
                continue
            out = -0.07 + step0 + k * self.batter + rng.uniform(-0.08, 0.1)       # нижние ярусы шире
            ww = w * rng.uniform(0.92, 1.08)
            b1, b2 = rng.uniform(0.04, 0.3), rng.uniform(0.02, 0.26)
            m = rng.uniform(-0.18, 0.18) * ww
            prof = [(-ww / 2, out + rng.uniform(-0.08, 0.04)), (m - ww / 6, out + b1), (m + ww / 6, out + b2),
                    (ww / 2, out + rng.uniform(-0.08, 0.04)), (ww / 2 - 0.12, out - D), (-ww / 2 + 0.12, out - D)]
            lean = self.lean * rng.uniform(0.4, 1.8)
            spin = rng.uniform(-0.16, 0.16)

            def ring(z, back):
                res = []
                for tt, nn in prof:
                    tt2 = tt * (1 - back * 0.25) + spin * nn
                    nn2 = nn - back
                    q = P + T * tt2 + N * nn2
                    res.append(Vector((q.x, q.y, z)))
                return res

            bm = bmesh.new()
            _prism(bm, ring(zb, 0.0), ring(zt, lean))
            bm.normal_update()
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            a.add(bm, colf)

    def _waterline(self, a, rng, total, L):
        """Валуны и пена у подножия, где скала входит в воду."""
        pts, nrm = self.outline, self.normals
        n = len(pts)
        s = rng.uniform(0, 2)
        j = 0
        while s < total:
            while j + 1 < len(L) - 1 and L[j + 1] < s:
                j += 1
            i0 = j % n
            probe = pts[i0] + nrm[i0] * 0.8
            buried = any(point_in_poly(probe.x, probe.y, o) for o in self.occluders)
            if not buried and not self._open((s % total) / total) and rng.random() < 0.55 and \
                    in_view(probe.x, probe.y, 2.0):
                P, N = pts[i0], nrm[i0]
                r = rng.uniform(0.45, 1.0)
                off = self.batter * (getattr(self, "_tiers", 1) - 1) + rng.uniform(0.15, 0.7)   # за низом скалы
                c = P + N * off
                a.add(p_ico(r, 1, loc=(c.x, c.y, WATER_Z + r * 0.15), scl=(1, rng.uniform(0.8, 1.0), 0.7),
                            jitter=0.25, rng=rng, rot=(0, 0, rng.uniform(0, 360)), cut=-r * 0.35),
                      cliff_color(self.seed + 3, self.z, self.z - self.bottom))
            s += rng.uniform(1.6, 3.2)


# =========================================================================================
# дорога
# =========================================================================================
def build_road(a, ctrl, terrace, half=0.62, taper=(1.2, 1.2), seed=0, lift=0.025):
    """Грунтовая дорога по террасе: светлая середина, тёмные края, как Env_Paths кита."""
    bm, uvl = a.bm, a.uv
    s = open_spline(ctrl, 0.25)
    cum = [0.0]
    for k in range(1, len(s)):
        cum.append(cum[-1] + (s[k] - s[k - 1]).length)
    total = cum[-1]
    rows = []
    for k, c in enumerate(s):
        tan = (s[min(k + 1, len(s) - 1)] - s[max(k - 1, 0)]).normalized()
        nr = Vector((-tan.y, tan.x, 0))
        tp = smoothstep(0, taper[0], cum[k]) * smoothstep(0, taper[1], total - cum[k]) if any(taper) else 1.0
        hw = max(0.08, half * (1 + 0.2 * noise.noise(Vector((cum[k] * 0.5, seed * 7.3, 0.5)))) * tp)
        offs = [-(hw + 0.16), -hw, -hw * 0.5, 0.0, hw * 0.5, hw, hw + 0.16]
        ts = [0.0, 0.3, 0.72, 0.9, 0.72, 0.3, 0.0]
        row = []
        for oi, (o, t) in enumerate(zip(offs, ts)):
            jit = 0.05 * noise.noise(Vector((cum[k] * 1.7, oi * 3.1, seed * 2.0))) if oi != 3 else 0
            p = c + nr * (o + jit)
            z = terrace.height(p.x, p.y) + (lift - 0.012 if oi in (0, 6) else lift)
            tt = t + 0.08 * noise.noise(Vector((p.x * 1.3, p.y * 1.3, 9.0)))
            row.append((bm.verts.new((p.x, p.y, z)), tt))
        rows.append(row)
    for k in range(len(rows) - 1):
        for i in range(6):
            q = (rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i])
            try:
                f = bm.faces.new([x[0] for x in q])
            except ValueError:
                continue
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
            tm = {x[0]: x[1] for x in q}
            for l in f.loops:
                l[uvl].uv = ramp_uv("dirt", tm[l.vert])
    return s


# =========================================================================================
# вода (отдельный материал Vitaria_Water, своя текстура с полосами течения)
# =========================================================================================
WATER_TEX = (64, 256)
RIVER_Z = -4.0          # река справа: своя, более высокая вода; в озеро падает каскадом

# Рамка видимости (x0, x1, y0, y1): объединение кадров боевой камеры от 4:3 (камера отъезжает)
# до 21:9 с запасом. Трава, дёрн, скалы и валуны за её пределами не строятся — их не видно
# ни на одном экране, а треугольники стоили бы как половина арены. None — строить всё.
VIEW_BOX = None


def in_view(x, y, pad=0.0):
    if VIEW_BOX is None:
        return True
    x0, x1, y0, y1 = VIEW_BOX
    return x0 - pad <= x <= x1 + pad and y0 - pad <= y <= y1 + pad

# Две текстуры с одной раскладкой UV (V — вдоль течения, её крутит скрипт в Unity, U — поперёк):
#   lake  — Vitaria_Water:     озеро и река, спокойная, редкие мягкие блики;
#   falls — Vitaria_Waterfall: водопады, плотные светлые струи и пена.
WATER_STYLES = {
    "lake": dict(base="#3f9ad6", deep="#2f80c0", lite="#79c3ec", foam="#dff2ff", streaks=16, foam_share=0.12,
                 length=(26, 90), strength=0.55, bands=0.55),
    "falls": dict(base="#4aa6df", deep="#3288c8", lite="#8ccbee", foam="#d2ebf7", streaks=64, foam_share=0.3,
                  length=(20, 80), strength=0.9, bands=0.8),
}


def make_water_texture(path, style="lake"):
    """Текстура воды: полосы течения вдоль V, тайлится по обеим осям."""
    import numpy as np
    st = WATER_STYLES[style]
    w, h = WATER_TEX
    rng = np.random.default_rng(7 if style == "lake" else 11)

    def rgb(hx):
        return np.array([int(hx[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255

    base, deep, lite, foam = rgb(st["base"]), rgb(st["deep"]), rgb(st["lite"]), rgb(st["foam"])
    img = np.zeros((h, w, 3), np.float32)
    u = np.arange(w) / w
    band = 0.5 + st["bands"] * (0.3 * np.sin(u * np.pi * 2 * 3 + 0.7) + 0.2 * np.sin(u * np.pi * 2 * 7 + 2.1))
    for y in range(h):
        img[y] = deep + (base - deep) * band[:, None].clip(0, 1)
    for _ in range(st["streaks"]):
        cx = rng.integers(0, w)
        cy = rng.integers(0, h)
        ln = rng.integers(*st["length"])
        wd = rng.integers(1, 3 if style == "lake" else 4)
        col = foam if rng.random() < st["foam_share"] else lite
        for dy in range(ln):
            yy = (cy + dy) % h
            k = np.sin(np.pi * dy / ln) * st["strength"]
            for dx in range(wd):
                xx = (cx + dx) % w
                img[yy, xx] = img[yy, xx] * (1 - k) + col * k
    from PIL import Image
    Image.fromarray((img.clip(0, 1) * 255).astype(np.uint8)).save(path)
    return path


def river_ribbon(a, ctrl, half, z=RIVER_Z, tile=4.0, seed=0, flare_end=0.0):
    """Лента реки по ломаной ctrl (по течению). Края чуть заходят под берега — щелей нет.
    UV: U поперёк (half*2/tile), V = -длина/tile — та же раскладка, что у водопадов."""
    bm, uvl = a.bm, a.uv
    s = open_spline(ctrl, 0.5)
    cum = [0.0]
    for k in range(1, len(s)):
        cum.append(cum[-1] + (s[k] - s[k - 1]).length)
    total = cum[-1]
    cols = 4
    rows = []
    for k, c in enumerate(s):
        tan = (s[min(k + 1, len(s) - 1)] - s[max(k - 1, 0)]).normalized()
        nr = Vector((-tan.y, tan.x, 0))
        hw = half * (1 + 0.1 * noise.noise(Vector((cum[k] * 0.3, seed * 3.7, 0.5))))
        if flare_end:
            hw *= 1 + flare_end * smoothstep(total - 3.0, total, cum[k])
        row = []
        for i in range(cols + 1):
            u = i / cols - 0.5
            p = c + nr * (u * 2 * hw)
            row.append((bm.verts.new((p.x, p.y, z)), (u * 2 * hw / tile, -cum[k] / tile)))
        rows.append(row)
    for k in range(len(rows) - 1):
        for i in range(cols):
            q = (rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i])
            try:
                f = bm.faces.new([x[0] for x in q])
            except ValueError:
                continue
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
            uvm = {x[0]: x[1] for x in q}
            for l in f.loops:
                l[uvl].uv = uvm[l.vert]
    end = s[-1]
    d = (s[-1] - s[-2]).normalized()
    return end, Vector((d.x, d.y, 0.0))


def water_plane(a, x0, x1, y0, y1, z=WATER_Z, flow=(0.0, 1.0), tile=5.0, n=6):
    """Плоскость воды n x n; UV: V вдоль течения flow, U поперёк, tile метров на повтор."""
    bm, uvl = a.bm, a.uv
    fl = Vector((flow[0], flow[1], 0)).normalized()
    pr = Vector((fl.y, -fl.x, 0))
    vs = {}
    for i in range(n + 1):
        for j in range(n + 1):
            x = x0 + (x1 - x0) * i / n
            y = y0 + (y1 - y0) * j / n
            vs[i, j] = bm.verts.new((x, y, z))
    for i in range(n):
        for j in range(n):
            f = bm.faces.new((vs[i, j], vs[i + 1, j], vs[i + 1, j + 1], vs[i, j + 1]))
            for l in f.loops:
                p = l.vert.co
                l[uvl].uv = (p.dot(pr) / tile, -p.dot(fl) / tile)


def waterfall(a, P, N, width, z_top, z_bot, seed=0, reach=1.6, stream=2.2, segs=12):
    """
    Лента водопада: ручей по верху террасы (stream м от кромки), перелив через кромку и падение
    с выносом вперёд на reach м. P — точка кромки, N — наружная нормаль (xy), ширина width.
    UV: U поперёк (1 повтор на 2 м), V вдоль пути воды (1 повтор на 2 м).
    """
    bm, uvl = a.bm, a.uv
    N = Vector((N[0], N[1], 0)).normalized()
    T = Vector((-N.y, N.x, 0))
    P = Vector((P[0], P[1], 0))
    path = []
    for k in range(4):                                        # ручей по верху
        d = -stream + stream * k / 3
        path.append((P + N * d, z_top + 0.04, 1.0 - 0.25 * (1 - k / 3)))
    H = z_top - z_bot
    for k in range(1, segs + 1):                              # перелив и падение
        t = k / segs
        out = 0.25 + reach * (1 - (1 - min(1.0, t * 3.2)) ** 2)
        zz = z_top - H * (t ** 1.15)
        path.append((P + N * out, zz, 1.0 + 0.35 * t))
    cum = [0.0]
    for k in range(1, len(path)):
        a0 = Vector((path[k - 1][0].x, path[k - 1][0].y, path[k - 1][1]))
        a1 = Vector((path[k][0].x, path[k][0].y, path[k][1]))
        cum.append(cum[-1] + (a1 - a0).length)
    cols = 4
    rows, where = [], {}
    for k, (c, z, wmul) in enumerate(path):
        row = []
        for i in range(cols + 1):
            u = i / cols - 0.5
            wob = 0.06 * noise.noise(Vector((u * 3, z * 0.6, seed)))
            q = c + T * (u * width * wmul) + N * wob
            v = bm.verts.new((q.x, q.y, z))
            where[v] = (k, i)
            row.append(v)
        rows.append(row)
    for k in range(len(rows) - 1):
        for i in range(cols):
            f = bm.faces.new((rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i]))
            f.normal_update()
            # лицом к зрителю: у падающей части — наружу от скалы, у ручья — вверх
            if (f.normal.z < 0.5 and f.normal.dot(N) < 0) or (f.normal.z < -0.5):
                f.normal_flip()
            for l in f.loops:
                kk, ii = where[l.vert]
                wd = width * path[kk][2]
                l[uvl].uv = ((ii / cols - 0.5) * wd / 2.0, -cum[kk] / 2.0)
    return P + N * (0.25 + reach)


def foam_patch(a, c, r, seed=0, n=9, z=WATER_Z):
    """Пена у подножия водопада: плоские белые комья вокруг точки падения."""
    rng = random.Random(seed)
    for k in range(n):
        ang = rng.uniform(0, math.tau)
        d = r * (0.2 + 0.8 * math.sqrt(rng.random()))
        rr = rng.uniform(0.25, 0.55) * (1.2 if d < r * 0.4 else 0.9)
        x, y = c[0] + math.cos(ang) * d, c[1] + math.sin(ang) * d
        a.add(p_ico(rr, 1, loc=(x, y, z + rr * 0.12), scl=(1.0, rng.uniform(0.7, 1.0), 0.42), jitter=0.2,
                    rng=rng, cut=-rr * 0.1), lambda f: "foam" if f.normal.z > -0.2 else "water_dark")
