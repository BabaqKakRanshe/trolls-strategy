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
                 shelves=None, shelf_jit=0.3, notch=0.18, calm=None, patches=None, extra_pts=None, fade=None,
                 deep=None, sink=None):
        self.name, self.z, self.bottom, self.seed = name, z, bottom, seed
        # sink(x, y) -> м вниз: низины внутри террасы (пруды «Болота»); None — как раньше
        self.sink = sink
        self.hills, self.flat, self.grid, self.grass = hills, flat, grid, grass
        # calm(x, y) -> 0..1: где пятна травы гасятся к середине рампы (поле под стройку колонии)
        self.calm = calm
        # patches(x, y) -> сдвиг по рампе после calm: пятна травы и межа поля колонии
        self.patches = patches
        # extra_pts: [(x, y)] — вершины травы сверх сетки (межа: ряды точек вдоль края поля)
        self.extra_pts = list(extra_pts or [])
        # fade: (рампа, z низа, z верха[, разброс ярусов]) — скала красится по высоте рампой (UV каждой
        # вершины): у парящего острова низ голубеет и уходит в дымку; разброс даёт каждому ярусу свой
        # тон — горизонтальные пласты. None — как раньше, swatch-и по граням.
        self.fade = fade
        # deep: dict(bottom, shelves, batter) — парящий остров. Столб над более низкой соседкой (floors)
        # кончается чуть ниже её травы; столб над пустотой — наружный обрыв острова: уходит до deep
        # bottom, ярусами deep shelves, с batter < 0 (низ уже верха). None — все столбы до self.bottom.
        self.deep = deep
        self.floors = []             # [(контур, z)] более низких соседей: задаёт сборка, как occluders
        # edge_height(x, y) -> z вершин контура в build_top; None — все на self.z (как было)
        self.edge_height = None
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
        if self.sink is not None:
            z -= self.sink(x, y)
        return z

    def grass_t(self, x, y, rim=None):
        lo, hi = self.grass
        n1 = noise.noise(Vector((x * 0.13, y * 0.13, self.seed + 4.2)))
        n2 = noise.noise(Vector((x * 0.47, y * 0.47, self.seed + 8.1)))
        r = self.rim(x, y) if rim is None else rim
        t = (lo + hi) / 2 + (hi - lo) * (0.55 * n1 + 0.2 * n2) - 0.12 * smoothstep(1.2, 0.0, r)
        if self.calm is not None:
            k = self.calm(x, y)
            if k > 0:
                mid = (lo + hi) / 2
                t = mid + (t - mid) * (1.0 - 0.75 * k)
        if self.patches is not None:
            t += self.patches(x, y)
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
        if self.extra_pts:
            ex = [Vector((px, py)) for px, py in self.extra_pts
                  if in_view(px, py, 2.0) and point_in_poly(px, py, self.outline) and
                  dist_to_outline(px, py, self.outline) > 0.08]
            if ex:
                # точки сетки у самых рядов межи убираем: иначе тонкие треугольники-щепки
                inner = [p for p in inner if min((p - q).length for q in ex) > g * 0.3] + ex
        res = geometry.delaunay_2d_cdt(pts2 + inner, [], [list(range(n_out))], 1, 1e-5)
        vco, faces = res[0], res[2]
        bm, uvl = a.bm, a.uv
        verts = []
        for v in vco:
            r = dist_to_outline(v.x, v.y, self.outline)
            if r > 1e-4:
                z = self.height(v.x, v.y, r)
            else:
                z = self.edge_height(v.x, v.y) if self.edge_height is not None else self.z
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
        strata = self.deep is not None and self.deep.get("strata") is not None
        s = rng.uniform(0, 0.6)
        j = 0
        while s < total:
            # в основном столбы, изредка широкая плита — ломает ритм «частокола»
            w = rng.uniform(2.3, 3.2) if rng.random() < self.wide else rng.uniform(*self.col_w)
            sc = s + w * 0.5
            P, N, j = self._at(L, sc, j)
            base = self._column_base(P, N)
            if strata and base[3] is None:       # наружный обрыв строят пласты (_build_strata)
                s += w * 0.8
                continue
            if base[3] not in (None, 1.0):       # наружный обрыв острова — столбы шире
                w *= base[3]
                sc = s + w * 0.5
                P, N, j = self._at(L, sc, j)
            T = Vector((-N.y, N.x, 0.0))
            probe = P + N * 0.35
            buried = any(point_in_poly(probe.x, probe.y, o) for o in self.occluders)
            if not buried and not self._open((sc % total) / total) and in_view(P.x, P.y, 3.0):
                self._column(a, rng, P, N, T, w, z_top, n_tiers, colf, *base[:3])
            s += w * rng.uniform(0.74, 0.86)
        if strata and not self.deep.get("union"):
            self._build_strata(a)
        if boulders and self.bottom <= WATER_Z:
            self._waterline(a, rng, total, L)

    def _at(self, L, sc, j):
        """Точка и нормаль контура на длине дуги sc (j — текущий отрезок, растёт монотонно)."""
        pts, nrm, n = self.outline, self.normals, len(self.outline)
        while j + 1 < len(L) - 1 and L[j + 1] < sc:
            j += 1
        t = (sc - L[j]) / max(1e-9, L[j + 1] - L[j])
        i0, i1 = j % n, (j + 1) % n
        return pts[i0].lerp(pts[i1], t), nrm[i0].lerp(nrm[i1], t).normalized(), j

    def _floor_z(self, P, N):
        """Трава более низкой соседки под кромкой в точке P (самая высокая из floors) или None — пустота."""
        probe = P + N * 0.7
        fz = None
        for poly, z in self.floors:
            if point_in_poly(probe.x, probe.y, poly):
                fz = z if fz is None else max(fz, z)
        return fz

    def _deep_bottom(self, P):
        """Низ наружного обрыва: гуляет на jag м — одни места свисают ниже, другие кончаются выше;
        рваный низ острова вместо ровного дна коробки."""
        d = self.deep
        sd = d.get("nseed", self.seed * 1.9)
        return d["bottom"] + d.get("jag", 0.0) * noise.noise(Vector((P.x * 0.21, P.y * 0.21, sd)))

    def _column_base(self, P, N):
        """(низ, полки, откос, множитель ширины) столба: как у всей террасы, а у парящего острова — по тому,
        что под столбом: трава соседки (короткий столб чуть ниже неё; множитель 1) или пустота (наружный
        обрыв; множитель deep wscale, а при пластах — None: там столбов нет)."""
        if self.deep is None:
            return self.bottom, self.shelves, self.batter, 1.0
        fz = self._floor_z(P, N)
        if fz is not None:
            return fz - 1.0, self.shelves, self.batter, 1.0
        d = self.deep
        wscale = None if d.get("strata") is not None else d.get("wscale", 1.0)
        return self._deep_bottom(P), d.get("shelves", self.shelves), d.get("batter", self.batter), wscale

    # -------------------------------------------------------------- strata (парящий остров)
    def _build_strata(self, a):
        """Наружный обрыв пластами: вдоль каждого участка кромки над пустотой — сплошные ленты-пласты,
        каждая чуть отступает внутрь (deep batter на пласт) и гуляет в плане (amp) и по высоте (wave);
        грань пласта наклонена назад (lean), между пластами — узкий уступ. Тон каждого пласта свой
        (разброс fade), уступы светлее, свесы темнее: горизонтальная слоистость, как у скал на
        референсах, вместо кладки из столбов."""
        pts, nrm = self.outline, self.normals
        n = len(pts)
        flags = []
        for P, N in zip(pts, nrm):
            probe = P + N * 0.35
            buried = any(point_in_poly(probe.x, probe.y, o) for o in self.occluders)
            flags.append(not buried and self._floor_z(P, N) is None and in_view(P.x, P.y, 3.0))
        runs = []
        if all(flags):
            runs.append((list(range(n)), True))
        elif any(flags):
            start = flags.index(False)
            cur = []
            for k in range(1, n + 1):
                i = (start + k) % n
                if flags[i]:
                    cur.append(i)
                elif cur:
                    runs.append((cur, False))
                    cur = []
            if cur:
                runs.append((cur, False))
        for idx, closed in runs:
            if not closed:                       # на стык с короткими столбами — по точке с каждой стороны
                idx = [(idx[0] - 1) % n] + idx + [(idx[-1] + 1) % n]
            if len(idx) >= 2:
                strata_bands(a, [pts[i] for i in idx], [nrm[i] for i in idx], closed, [self.z - 0.36] * len(idx),
                             self.deep, self.fade, self._deep_bottom)

    def _column(self, a, rng, P, N, T, w, z_top, n_tiers, colf, bottom=None, shelves=None, batter=None):
        bottom = self.bottom if bottom is None else bottom
        shelves = self.shelves if shelves is None else shelves
        batter = self.batter if batter is None else batter
        H = z_top - bottom
        if shelves is not None:
            cuts = sorted(min(0.9, max(0.08, c + rng.uniform(-1, 1) * self.shelf_jit / max(1.0, H)))
                          for c in shelves)
        else:
            cuts = sorted(rng.uniform(0.18, 0.82) for _ in range(n_tiers - 1))
        top = z_top - rng.uniform(0.0, 0.14)
        if rng.random() < self.notch:                       # выщербина: столб начинается ниже кромки
            top -= rng.uniform(0.35, 0.9)
        levels = [top] + [z_top - H * c + rng.uniform(-0.12, 0.12) for c in cuts] + [bottom]
        for k in range(1, len(levels) - 1):                 # ярус не тоньше 0.5 м
            levels[k] = min(levels[k], levels[k - 1] - 0.5)
        D = rng.uniform(1.1, 1.6)
        step0 = rng.uniform(-0.18, 0.2)                     # у всего столба свой вынос: рельеф в плане
        for k in range(len(levels) - 1):
            zt, zb = levels[k], levels[k + 1] - (0.03 if k < len(levels) - 2 else 0.0)
            if zt - zb < 0.2:
                continue
            out = -0.07 + step0 + k * batter + rng.uniform(-0.08, 0.1)       # нижние ярусы шире
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
            if self.fade:
                amp = self.fade[3] if len(self.fade) > 3 else 0.05
                self._add_faded(a, bm, rng.uniform(-1.0, 1.0) * amp)
            else:
                a.add(bm, colf)

    def _add_faded(self, a, part, jit=0.0):
        """Грани столба с UV по высоте вершины на рампе fade: плавный переход кромка -> дымка.
        Верх ярусов (полки) на тон светлее, низ (свес) темнее."""
        ramp, z0, z1 = self.fade[:3]
        vmap = {v: a.bm.verts.new(v.co) for v in part.verts}
        for f in part.faces:
            try:
                nf = a.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nz = f.normal.z
            bump = 0.08 if nz > 0.55 else (-0.08 if nz < -0.5 else 0.0)
            for l in nf.loops:
                t = clamp((l.vert.co.z - z0) / (z1 - z0) + bump + jit)
                l[a.uv].uv = ramp_uv(ramp, t)
        part.free()

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
# пласты наружного обрыва парящего острова
# =========================================================================================
def strata_bands(a, P, N, closed, z_tops, deep, fade, bottom_of):
    """Пласты вдоль кромки P (нормали N наружу), верх в точке i — z_tops[i] (под свесом дёрна).
    Уровни общие для всего острова: абсолютные высоты от deep zref (выше — через up_step м), шум — по
    мировым координатам. Пласт узнаётся по ключу — номеру его нижнего уровня: от ключа тон, наклон
    грани, отступ и шум, одинаковые по всей кромке. Каждый пласт на batter м отступает внутрь,
    гуляет в плане (amp) и по высоте (wave), грань наклонена (lean: назад — светлее, нависает —
    темнее), между пластами — узкий уступ. Низ — bottom_of(P) (рваный)."""
    d = deep
    bm, uvl = a.bm, a.uv
    ztop_max = max(z_tops)
    zref = d.get("zref", ztop_max)
    H = zref - d["bottom"]
    up = d.get("up_step", 0.9)
    lows = [zref - H * c for c in d["strata"]]
    bounds = []
    k = 1
    while zref + up * k < ztop_max - 0.25:
        bounds.append((zref + up * k, -k))
        k += 1
    bounds.reverse()
    bounds += [(z, i + 1) for i, z in enumerate(lows) if z < ztop_max - 0.25]
    keys = [kk for _, kk in bounds] + [len(lows) + 1]
    K = len(keys)
    wav, amp, lean = d.get("wave", 0.35), d.get("amp", 0.3), d.get("lean", 0.16)
    batter, crack = d.get("batter", -0.4), d.get("crack", 0.14)
    ns = d.get("nseed", 0.5)
    ramp, z0, z1 = fade[:3]
    jamp = fade[3] if len(fade) > 3 else 0.05
    ledge = d.get("ledge", 0.09)            # сдвиг по рампе уступов между пластами (вверх — светлее/мшистее)

    def rnd(key, salt):
        return random.Random(key * 7919 + salt).uniform(-1.0, 1.0)

    jit = [rnd(kk, 13) * jamp for kk in keys]
    leans = [lean * (0.35 + 1.05 * rnd(kk, 29)) for kk in keys]
    m = len(P)
    Z, OFF = [], []
    for i in range(m):
        x, y = P[i].x, P[i].y
        zt = z_tops[i]
        zb = bottom_of(P[i])
        col = [zt]
        first = None
        for bi, (zl, kk) in enumerate(bounds):
            if zl >= zt - 0.25:                  # уровень выше верха этой точки: пласт схлопнут
                col.append(zt)
                continue
            if first is None:
                first = bi
            zk = zl + wav * noise.noise(Vector((x * 0.12, y * 0.12, kk * 3.7 + ns)))
            zk = min(zk, col[-1] - 0.25)
            col.append(max(zk, zb))
        if first is None:
            first = len(bounds)
        col.append(min(zb, col[-1]))
        Z.append(col)
        off = []
        for b, kk in enumerate(keys):
            o = batter * max(0, kk - 1) + amp * noise.noise(Vector((x * 0.3, y * 0.3, kk * 5.1 + ns + 2.2))) + \
                crack * noise.noise(Vector((x * 1.1, y * 1.1, kk * 0.35 + ns + 4.4)))      # трещины через пласты
            if b <= first:
                o = min(o * 0.4, 0.08)                                              # верх — под свесом дёрна
            off.append(o)
        OFF.append(off)

    def vert(i, o, z):
        q = P[i] + N[i] * o
        return bm.verts.new((q.x, q.y, z))

    top = [[vert(i, OFF[i][b], Z[i][b]) for i in range(m)] for b in range(K)]
    bot = [[vert(i, OFF[i][b] - leans[b], Z[i][b + 1]) for i in range(m)] for b in range(K)]
    segs = range(m if closed else m - 1)

    def face(vs, want, t_of):
        try:
            f = bm.faces.new(vs)
        except ValueError:
            return
        f.normal_update()
        if f.normal.dot(want) < 0:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = ramp_uv(ramp, t_of(l.vert.co.z, f.normal.z))

    for b in range(K):
        for i in segs:
            i2 = (i + 1) % m
            if Z[i][b] - Z[i][b + 1] < 0.04 and Z[i2][b] - Z[i2][b + 1] < 0.04:
                continue
            out = (N[i] + N[i2]).normalized()
            face((top[b][i], top[b][i2], bot[b][i2], bot[b][i]), out,
                 lambda z, nz, b=b: clamp((z - z0) / (z1 - z0) + jit[b] + 0.12 * nz))
            if b + 1 < K:
                pr = (OFF[i][b + 1] + OFF[i2][b + 1]) - (OFF[i][b] + OFF[i2][b] - 2 * leans[b]) > 0
                want = Vector((0, 0, 1 if pr else -1))
                face((bot[b][i], bot[b][i2], top[b + 1][i2], top[b + 1][i]), want,
                     lambda z, nz, b=b: clamp((z - z0) / (z1 - z0) + jit[b] + (ledge if nz > 0 else -0.09)))
    loose = [v for row in top + bot for v in row if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")


def union_outline(polys, res=0.05, step=0.4):
    """Внешний контур объединения многоугольников (растр PIL + marching squares): кромка острова из
    нескольких террас одной лентой. Возвращает (контур CCW, число найденных контуров)."""
    import numpy as np
    from PIL import Image, ImageDraw
    xs = [p[0] for poly in polys for p in poly]
    ys = [p[1] for poly in polys for p in poly]
    x0, x1, y0, y1 = min(xs) - 1.0, max(xs) + 1.0, min(ys) - 1.0, max(ys) + 1.0
    W, Hh = int((x1 - x0) / res) + 2, int((y1 - y0) / res) + 2
    img = Image.new("L", (W, Hh), 0)
    dr = ImageDraw.Draw(img)
    for poly in polys:
        dr.polygon([((p[0] - x0) / res, (y1 - p[1]) / res) for p in poly], fill=255)
    g = (np.asarray(img) > 127).astype(np.int8)
    # marching squares по клеткам (r, c): углы TL, TR, BR, BL
    tl, tr_, br, bl = g[:-1, :-1], g[:-1, 1:], g[1:, 1:], g[1:, :-1]
    case = tl * 8 + tr_ * 4 + br * 2 + bl
    rs, cs = np.nonzero((case > 0) & (case < 15))
    # точки на серединах рёбер клетки: T, R, B, L (в координатах пикселей: x = c, y = r)
    def mid(r, c, e):
        return {"T": (c + 0.5, r), "R": (c + 1.0, r + 0.5), "B": (c + 0.5, r + 1.0), "L": (c, r + 0.5)}[e]
    table = {1: [("L", "B")], 2: [("B", "R")], 3: [("L", "R")], 4: [("T", "R")], 5: [("L", "T"), ("B", "R")],
             6: [("T", "B")], 7: [("L", "T")], 8: [("L", "T")], 9: [("T", "B")], 10: [("T", "R"), ("L", "B")],
             11: [("T", "R")], 12: [("L", "R")], 13: [("B", "R")], 14: [("L", "B")]}
    nxt = {}
    for r, c in zip(rs.tolist(), cs.tolist()):
        for e0, e1 in table[int(case[r, c])]:
            p0, p1 = mid(r, c, e0), mid(r, c, e1)
            nxt.setdefault(p0, []).append(p1)
            nxt.setdefault(p1, []).append(p0)
    seen, loops = set(), []
    for start in list(nxt.keys()):
        if start in seen:
            continue
        loop, prev, cur = [start], None, start
        seen.add(start)
        while True:
            cand = [q for q in nxt[cur] if q != prev and q not in seen]
            if not cand:
                break
            prev, cur = cur, cand[0]
            seen.add(cur)
            loop.append(cur)
        loops.append(loop)
    best = max(loops, key=len)
    pts = [Vector((x0 + px * res + res * 0.5, y1 - py * res - res * 0.5, 0.0)) for px, py in best]
    pts = resample_closed(ccw(pts), step)
    return pts, len([lp for lp in loops if len(lp) > 20])


def build_island_strata(a, terraces, deep, fade, step=0.4):
    """Наружный обрыв парящего острова одной лентой пластов по общему контуру всех террас. Верх
    ленты в каждой точке — под дёрном той террасы, чья это кромка (самая высокая рядом внутри)."""
    outline, n_loops = union_outline([t.outline for t in terraces], step=step)
    normals = edge_normals(outline)
    z_tops = []
    for P, N in zip(outline, normals):
        q = P - N * 0.3
        zs = [t.z for t in terraces if point_in_poly(q.x, q.y, t.outline)]
        if not zs:
            zs = [min(terraces, key=lambda t: dist_to_outline(P.x, P.y, t.outline)).z]
        z_tops.append(max(zs) - 0.36)
    # ступенька верха между террасами: в пределах 0.6 м берём более низкий верх (он у обеих под дёрном)
    m = len(outline)
    zt2 = [min(z_tops[(i + k) % m] for k in (-1, 0, 1)) for i in range(m)]
    sd = deep.get("nseed", 0.5)

    def bottom_of(P):
        return deep["bottom"] + deep.get("jag", 0.0) * noise.noise(Vector((P.x * 0.21, P.y * 0.21, sd)))

    flags = [in_view(P.x, P.y, 3.0) for P in outline]
    if all(flags):
        strata_bands(a, outline, normals, True, zt2, deep, fade, bottom_of)
        return outline, n_loops
    # остров шире рамки видимости (арена): пласты только там, где кромку видно; концы лент — за рамкой
    start = flags.index(False)
    run = []
    for k in range(1, m + 1):
        i = (start + k) % m
        if flags[i]:
            run.append(i)
        if (not flags[i] or k == m) and len(run) > 1:
            strata_bands(a, [outline[j] for j in run], [normals[j] for j in run], False, [zt2[j] for j in run],
                         deep, fade, bottom_of)
        if not flags[i]:
            run = []
    return outline, n_loops


# =========================================================================================
# дорога
# =========================================================================================
def _ease(a, x):
    """Сужение конца дороги на длине a (0 — конец обрезан, например под настилом моста)."""
    return 1.0 if a <= 0 else smoothstep(0, a, x)


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
        tp = _ease(taper[0], cum[k]) * _ease(taper[1], total - cum[k]) if any(taper) else 1.0
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
    # Vitaria_Water_Swamp: стоячая вода болота — тёмная зелено-бирюзовая, светлые разводы неба и пятна ряски;
    # почти не течёт (scroll в раскладке малый)
    # пятна вместо полос: UV болота квадратные (V — 4 повтора U), пятно круглое и на воде
    "swamp": dict(base="#3f6b5c", deep="#2f5447", lite="#71a08b", foam="#7f9c4a", blobs=34, foam_share=0.4,
                  radius=(3.0, 10.0), strength=0.5, bands=0.12, seed=13),
}


def make_water_texture(path, style="lake"):
    """Текстура воды: полосы течения вдоль V, тайлится по обеим осям."""
    import numpy as np
    st = WATER_STYLES[style]
    w, h = WATER_TEX
    rng = np.random.default_rng(st.get("seed", 7 if style == "lake" else 11))

    def rgb(hx):
        return np.array([int(hx[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255

    base, deep, lite, foam = rgb(st["base"]), rgb(st["deep"]), rgb(st["lite"]), rgb(st["foam"])
    img = np.zeros((h, w, 3), np.float32)
    u = np.arange(w) / w
    band = 0.5 + st["bands"] * (0.3 * np.sin(u * np.pi * 2 * 3 + 0.7) + 0.2 * np.sin(u * np.pi * 2 * 7 + 2.1))
    for y in range(h):
        img[y] = deep + (base - deep) * band[:, None].clip(0, 1)
    if st.get("blobs"):
        yy, xx = np.mgrid[0:h, 0:w]
        for _ in range(st["blobs"]):
            cx, cy = rng.integers(0, w), rng.integers(0, h)
            r = rng.uniform(*st["radius"])
            col = foam if rng.random() < st["foam_share"] else lite
            dx = np.minimum(np.abs(xx - cx), w - np.abs(xx - cx))
            dy = np.minimum(np.abs(yy - cy), h - np.abs(yy - cy))
            k = (np.clip(1.0 - np.sqrt(dx * dx + dy * dy) / r, 0.0, 1.0) ** 1.4 * st["strength"])[..., None]
            img = img * (1 - k) + col * k
    for _ in range(st.get("streaks", 0)):
        cx = rng.integers(0, w)
        cy = rng.integers(0, h)
        ln = rng.integers(*st["length"])
        wd = rng.integers(*st.get("width", (1, 3 if style == "lake" else 4)))
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


def river_ribbon(a, ctrl, half, z=RIVER_Z, tile=4.0, seed=0, flare_end=0.0, flare_start=0.0, flare_len=3.0):
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
        if flare_start:                       # плёс у истока: вода под водопадом шире русла
            hw *= 1 + flare_start * smoothstep(flare_len, 0.0, cum[k])
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


# =========================================================================================
# стоячая вода и пятна по рельефу (окружения арены: «Болото»)
# =========================================================================================
def signed_dist(x, y, pts):
    """> 0 внутри контура, < 0 снаружи (м)."""
    d = dist_to_outline(x, y, pts)
    return d if point_in_poly(x, y, pts) else -d


def offset_outline(pts, d):
    """Контур, сдвинутый по нормали на d (> 0 — наружу)."""
    nr = edge_normals(pts)
    return [p + n * d for p, n in zip(pts, nr)]


def water_poly(a, outline, z, tile=3.0, rot=0.0):
    """Плоская вода по контуру (пруд в низине). UV квадратные в мире: U — 1 повтор на tile м, V — на 4·tile
    (текстура воды 64 x 256), повёрнуты на rot градусов; V скроллит игра (раскладка scroll)."""
    bm, uvl = a.bm, a.uv
    pts2 = [Vector((p.x, p.y)) for p in outline]
    res = geometry.delaunay_2d_cdt(pts2, [], [list(range(len(pts2)))], 1, 1e-5)
    vco, faces = res[0], res[2]
    ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    vs = [bm.verts.new((v.x, v.y, z)) for v in vco]
    for f in faces:
        try:
            nf = bm.faces.new([vs[i] for i in f])
        except ValueError:
            continue
        nf.normal_update()
        if nf.normal.z < 0:
            nf.normal_flip()
        for l in nf.loops:
            x, y = l.vert.co.x, l.vert.co.y
            u, v = x * ca + y * sa, -x * sa + y * ca
            l[uvl].uv = (u / tile, v / (4.0 * tile))
    return len(faces)


def decal(a, terrace, outline, ramp, t_of, lift=0.022, grid=0.55, swatch=None):
    """Пятно по рельефу террасы (ил у берега, грязь у поля): контур + внутренняя сетка, высота — рельеф + lift,
    цвет — рампа ramp, t_of(x, y, r) -> 0..1, r — расстояние до края пятна; или один swatch (мох)."""
    bm, uvl = a.bm, a.uv
    pts2 = [Vector((p.x, p.y)) for p in outline]
    xs = [p.x for p in pts2]
    ys = [p.y for p in pts2]
    inner = []
    yy = min(ys) + grid * 0.5
    row = 0
    while yy < max(ys):
        xx = min(xs) + grid * (0.5 + 0.5 * (row % 2))
        while xx < max(xs):
            if point_in_poly(xx, yy, outline) and dist_to_outline(xx, yy, outline) > grid * 0.45:
                inner.append(Vector((xx, yy)))
            xx += grid
        yy += grid * 0.866
        row += 1
    res = geometry.delaunay_2d_cdt(pts2 + inner, [], [list(range(len(pts2)))], 1, 1e-5)
    vco, faces = res[0], res[2]
    verts = []
    for v in vco:
        r = dist_to_outline(v.x, v.y, outline)
        verts.append((bm.verts.new((v.x, v.y, terrace.height(v.x, v.y) + lift)),
                      None if swatch else t_of(v.x, v.y, r)))
    n = 0
    for f in faces:
        try:
            nf = bm.faces.new([verts[i][0] for i in f])
        except ValueError:
            continue
        nf.normal_update()
        if nf.normal.z < 0:
            nf.normal_flip()
        tm = {verts[i][0]: verts[i][1] for i in f}
        for l in nf.loops:
            l[uvl].uv = SW_UV[swatch] if swatch else ramp_uv(ramp, tm[l.vert])
        n += 1
    return n
