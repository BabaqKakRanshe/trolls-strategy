"""
Гильдия носильщиков: крепкий каменный зал под синей кровлей, фронтоном к камере, с широкой
арочной дверью в рустованном портале (через неё носят грузы), над ней на фронтоне — герб гильдии,
золотое колесо на синем круге. Слева к залу пристроена башенка с шатровой кровлей и флагом —
она даёт зданию вершину и отличает его от склада (бревенчатый сарай под сланцем) и рынка (тенты).
У входа: справа ручная тележка с ящиками и мешками, оглоблями к зрителю; слева стойка
с коромыслом и заплечными мешками, под ней вёдра, рядом горка мешков.
Вход — арка посередине фасада (вход в игре — середина передней кромки следа).
"""
import math
import random

from build_vitaria import p_box, p_cyl, p_ico, p_prism, p_taper_box, by_normal
from vitaria_buildings.common import (Frame, gable_roof, gable_wall, cornice, crate, bucket, lantern,
                                      STONE_TOP, PLANK_TOP)
from vitaria_buildings.levels import pennant, gold_ridge, wall_banner, belfry, finial

NAME = "Bld_HaulersGuild"
TITLE = "Гильдия носильщиков"
TARGET = (5.0, 4.4, 4.9)   # след 3x3 при масштабе 0.5

W, D = 3.30, 2.60          # зал, по центру
F = 0.26                   # пол
ZT = 2.40                  # верх стен
PITCH = 34.0
YF = -D / 2                # фасад
OW, R = 1.44, 0.72         # проём арки: ширина и радиус свода
ZS = F + 0.95              # пята свода
TX, TY, TS = -W / 2 - 0.40, 0.58, 1.02   # башенка: центр и сторона у земли
TZ = 3.40                  # верх стен башенки

WALL = by_normal("stone_light", "stone_mid", "stone_dark", 0.8)
PORTAL = by_normal("stone_light", "stone_light", "stone_mid", 0.8)


def _quoins(a, x0, x1, y0, y1, z0, z1, n=4):
    """Угловые русты передних углов [x0, x1] x [y0, y1] через ряд, выступ 6 см. Тыл без рустов:
    камера колонии смотрит с юга, а 8 брусков с фаской — 350 треугольников бюджета."""
    p = 0.06
    hq = (z1 - z0) / n
    for x, sx in ((x0, -1), (x1, 1)):
        for y, sy in ((y0, -1),):
            for i in range(n):
                lx, ly = (0.46, 0.28) if i % 2 == 0 else (0.28, 0.46)
                za = z0 + i * hq + (0.018 if i else -0.02)
                zb = z0 + (i + 1) * hq - 0.018
                a.add(p_box((lx, ly, zb - za), loc=(x + sx * (p - lx / 2), y + sy * (p - ly / 2), (za + zb) / 2),
                            bevel=0.04), "stone_light")


def _window(a, fr, w=0.42, h=0.52):
    """Окно с синими ставнями в раме fr (локальный -Y — наружу)."""
    fr.box(a, (w + 0.16, 0.14, h + 0.16), (0, 0, 0), col="wood_dark", bevel=0.035)
    fr.box(a, (w, 0.10, h), (0, -0.03, 0), col="glass", bevel=0.0)
    fr.box(a, (0.07, 0.06, h), (0, -0.07, 0), col="wood_dark", bevel=0.0)
    for sx in (-1, 1):
        fr.box(a, (0.24, 0.09, h + 0.10), (sx * (w / 2 + 0.16), -0.06, 0), col="roof_dark", bevel=0.025)
    fr.box(a, (w + 0.30, 0.22, 0.10), (0, -0.05, -h / 2 - 0.11), col="stone_light", bevel=0.03)


# ---------------------------------------------------------------------------------------------
# зал
# ---------------------------------------------------------------------------------------------
def _hall(a):
    a.add(p_box((W + 0.26, D + 0.26, F + 0.14), loc=(0, 0, (F - 0.14) / 2), bevel=0.06),
          by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    a.add(p_box((W, D, ZT - F + 0.02), loc=(0, 0, (F + ZT) / 2 - 0.01), bevel=0.05), "stone_mid")
    _quoins(a, -W / 2, W / 2, -D / 2, D / 2, F, ZT, n=3)
    cornice(a, W, D, ZT, t=0.18, col="wood_dark")
    zr = gable_roof(a, W, D, ZT, pitch_deg=PITCH, ox=0.26, oy=0.30)
    gable_wall(a, W, ZT - 0.02, zr, D, col="wood_mid", inset=0.05, depth=0.14)
    # доски фронтона: три тёмных нащельника, средний уходит за герб
    for x in (-0.80, 0.80):
        h = (zr - ZT) * (1 - abs(x) / (W / 2)) - 0.14
        a.add(p_box((0.09, 0.05, h), loc=(x, YF - 0.025, ZT + h / 2), bevel=0.0), "wood_dark")
    # окна на правом боку; левый закрывают башенка, стойка и мешки
    for y in (-0.45, 0.45):
        _window(a, Frame((W / 2, y, 1.42), rz=90))
    return zr


def _arch_door(a):
    """Широкая арка: косяки и клинчатый свод из семи камней с замковым, двустворчатые
    дубовые полотна с оковкой, тёмная щель между створками, порог-ступень."""
    y = YF
    # тёмная глубина проёма (видна в щели между створками и по краю свода)
    a.add(p_box((OW, 0.05, ZS - F), loc=(0, y - 0.01, (F + ZS) / 2), bevel=0.0), "black")
    a.add(p_cyl(R, R, 0.05, 16, loc=(0, y + 0.015, ZS), rot=(90, 0, 0)), "black")
    for sx in (-1, 1):
        # створка: прямая часть и четверть круга сверху
        rr = R - 0.03
        arc = [(max(0.02, rr * math.cos(math.radians(t))), ZS + rr * math.sin(math.radians(t)))
               for t in (0, 18, 36, 54, 72, 90)]
        pts = [(0.02, ZS)] + arc
        if sx < 0:
            pts = [(-x, z) for x, z in reversed(pts)]
        a.add(p_prism(pts, 0.10, loc=(0, y - 0.06, 0)), "wood_mid")
        xc = sx * (OW / 4 + 0.01)
        a.add(p_box((OW / 2 - 0.05, 0.10, ZS - F), loc=(xc, y - 0.06, (F + ZS) / 2), bevel=0.0), "wood_mid")
        for z in (F + 0.24, ZS - 0.12):
            a.add(p_box((OW / 2 - 0.12, 0.06, 0.12), loc=(xc, y - 0.13, z), bevel=0.0), "iron_dark")
        # кольца-ручки у щели
        a.add(p_cyl(0.075, 0.075, 0.05, 8, loc=(sx * 0.15, y - 0.11, F + 0.70), rot=(90, 0, 0)), "gold")
        # косяк до пяты свода
        a.add(p_box((0.28, 0.32, ZS - F + 0.04), loc=(sx * (OW / 2 + 0.14), y - 0.06, (F + ZS) / 2), bevel=0.04),
              PORTAL)
    # клинчатый свод: камни по радиусу, через один светлее; замковый крупнее и выше
    n = 7
    for i in range(n):
        ang = 180.0 * (i + 0.5) / n
        t = math.radians(ang)
        key = i == n // 2
        rr = R + (0.17 if key else 0.14)
        loc = (math.cos(t) * rr, y - 0.06, ZS + math.sin(t) * rr)
        a.add(p_box((0.25 if key else 0.30, 0.32 if key else 0.30, 0.36 if key else 0.28), loc=loc,
                    rot=(0, 90 - ang, 0), bevel=0.04),
              PORTAL if (i % 2 == 0 or key) else WALL)
    # порог: пол на 26 см выше земли, ступень во всю ширину портала
    a.add(p_box((OW + 0.70, 0.44, 0.16), loc=(0, y - 0.30, 0.05), bevel=0.04), STONE_TOP)


def _emblem(a, zr):
    """Герб гильдии на фронтоне: синий круг в золотом ободе, на нём золотое колесо со спицами."""
    fr = Frame((0, YF - 0.06, ZT + (zr - ZT) * 0.36))
    fr.cyl(a, 0.38, 0.38, 0.08, 16, loc=(0, 0.04, 0), rot=(90, 0, 0), col="gold", bevel=0.015)
    fr.cyl(a, 0.32, 0.32, 0.03, 16, loc=(0, -0.035, 0), rot=(90, 0, 0), col="roof")
    fr.cyl(a, 0.27, 0.27, 0.03, 12, loc=(0, -0.055, 0), rot=(90, 0, 0), col="gold")
    fr.cyl(a, 0.20, 0.20, 0.03, 12, loc=(0, -0.07, 0), rot=(90, 0, 0), col="roof")
    for k in range(3):
        fr.box(a, (0.40, 0.03, 0.06), (0, -0.11, 0), rot=(0, 60 * k, 0), col="gold", bevel=0.0)
    fr.cyl(a, 0.08, 0.08, 0.04, 8, loc=(0, -0.10, 0), rot=(90, 0, 0), col="gold_dark")


# ---------------------------------------------------------------------------------------------
# башенка
# ---------------------------------------------------------------------------------------------
def _tower(a):
    a.add(p_box((TS + 0.16, TS + 0.16, F + 0.14), loc=(TX, TY, (F - 0.14) / 2), bevel=0.05),
          by_normal("stone_mid", "stone_dark", "stone_dark", 0.8))
    top = TS - 0.10
    a.add(p_taper_box((TS, TS), (top, top), TZ - F + 0.02, loc=(TX, TY, F - 0.02), bevel=0.05), WALL)
    for z in (1.55, ZT + 0.02):                 # пояса: нижний вровень с подоконниками, верхний — с карнизом зала
        s = TS - 0.10 * (z - F) / (TZ - F) + 0.10
        a.add(p_box((s, s, 0.14), loc=(TX, TY, z), bevel=0.03), "stone_light")
    a.add(p_box((top + 0.20, top + 0.20, 0.18), loc=(TX, TY, TZ - 0.02), bevel=0.04), "wood_dark")
    # окна: бойница внизу фасада, арочное окно под кровлей на фасаде и на левом боку
    yf = TY - TS / 2
    a.add(p_box((0.24, 0.10, 0.62), loc=(TX, yf - 0.01, 1.05), bevel=0.025), "stone_light")
    a.add(p_box((0.10, 0.08, 0.48), loc=(TX, yf - 0.05, 1.05), bevel=0.0), "black")
    for fr in (Frame((TX, TY - top / 2 - 0.005, 2.90)), Frame((TX - top / 2 - 0.005, TY, 2.90), rz=-90)):
        fr.box(a, (0.46, 0.10, 0.50), (0, 0, -0.04), col="stone_light", bevel=0.03)
        fr.box(a, (0.28, 0.06, 0.34), (0, -0.04, -0.06), col="black", bevel=0.0)
        fr.cyl(a, 0.14, 0.14, 0.06, 8, loc=(0, -0.01, 0.11), rot=(90, 0, 0), col="black")
    # шатёр: нижний пояс темнее, верх светлее (верх кита всегда светлее боков)
    r0 = (top / 2 + 0.14) * math.sqrt(2)
    zb = TZ + 0.06
    a.add(p_cyl(r0 + 0.08, r0 + 0.08, 0.10, 4, loc=(TX, TY, zb - 0.06), spin=45, bevel=0.03), "wood_dark")
    a.add(p_cyl(r0, r0 * 0.6, 0.42, 4, loc=(TX, TY, zb), spin=45, bevel=0.03), "roof_dark")
    a.add(p_cyl(r0 * 0.6, 0.0, 0.58, 4, loc=(TX, TY, zb + 0.42), spin=45), "roof")
    apex = zb + 1.0
    # древко и флаг-ласточкин хвост: золотое полотно с синей полосой, вьётся вправо, над залом
    a.add(p_cyl(0.05, 0.045, 0.66, 6, loc=(TX, TY, apex - 0.12)), "wood_dark")
    a.add(p_ico(0.07, 1, loc=(TX, TY, apex + 0.56)), "gold")
    zf = apex + 0.48
    flag = [(0.0, 0.0), (0.0, -0.42), (0.70, -0.42), (0.56, -0.21), (0.70, 0.0)]
    a.add(p_prism([(TX + 0.04 + x, zf + z) for x, z in flag], 0.05, loc=(0, TY, 0)), "gold")
    a.add(p_prism([(TX + 0.04 + x, zf + z) for x, z in [(0.0, -0.15), (0.0, -0.27), (0.63, -0.27), (0.61, -0.21),
                                                         (0.63, -0.15)]], 0.07, loc=(0, TY, 0)), "roof")


# ---------------------------------------------------------------------------------------------
# реквизит
# ---------------------------------------------------------------------------------------------
def _sack(a, fr, rng, s=1.0, col="burlap"):
    """Мешок: гранёная икосфера с плоским дном, горловина с перетяжкой (как у фермы)."""
    fr.ico(a, 0.22 * s, loc=(0, 0, 0.16 * s), scl=(1.15, 1.0, 0.92), col=col, jitter=0.06, rng=rng, cut=-0.7)
    fr.cyl(a, 0.12 * s, 0.07 * s, 0.10 * s, 7, loc=(0, 0, 0.32 * s), col="burlap_dark")
    fr.cyl(a, 0.08 * s, 0.08 * s, 0.04 * s, 7, loc=(0, 0, 0.36 * s), col="rope")
    fr.cyl(a, 0.07 * s, 0.10 * s, 0.06 * s, 7, loc=(0, 0, 0.40 * s), col="burlap_dark")


def _cart(a, fr, rng):
    """Ручная тележка: кузов на двух колёсах, оглобли вперёд (-X рамы) лежат концами на земле,
    подпорка под передком; в кузове два ящика и мешки."""
    zb = 0.46                                        # верх днища
    fr.box(a, (1.06, 0.70, 0.08), (0, 0, zb - 0.04), col=PLANK_TOP, bevel=0.025)
    for sy in (-1, 1):
        fr.box(a, (1.06, 0.07, 0.20), (0, sy * 0.33, zb + 0.08), col="wood_mid", bevel=0.02)
    fr.box(a, (0.07, 0.60, 0.20), (0.50, 0, zb + 0.08), col="wood_mid", bevel=0.02)
    for sy in (-1, 1):
        # колесо: диск, железный обод, ступица; ось чуть сзади середины кузова
        fr.cyl(a, 0.34, 0.34, 0.08, 12, loc=(0.10, sy * 0.40 + sy * 0.04, 0.34), rot=(sy * 90, 0, 0),
               col="wood_mid")
        fr.cyl(a, 0.36, 0.36, 0.05, 12, loc=(0.10, sy * 0.40 + sy * 0.03, 0.34), rot=(sy * 90, 0, 0),
               col="iron_dark")
        fr.cyl(a, 0.10, 0.10, 0.06, 8, loc=(0.10, sy * 0.49, 0.34), rot=(sy * 90, 0, 0), col="iron_dark")
        fr.box(a, (0.08, 0.08, 0.36), (-0.44, sy * 0.24, 0.20), col="wood_dark", bevel=0.0)     # подпорка
        # оглобля: от кузова вниз-вперёд, конец на земле
        p0, p1 = (-0.40, sy * 0.24, zb - 0.06), (-1.12, sy * 0.30, 0.04)
        L = math.dist(p0, p1)
        ang = math.degrees(math.atan2(p0[2] - p1[2], p0[0] - p1[0]))
        fr.box(a, (L + 0.04, 0.08, 0.08), ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2),
               rot=(0, ang, 0), col="wood_light", bevel=0.0)
    fr.box(a, (0.08, 0.60, 0.08), (-1.00, 0, 0.10), col="wood_dark", bevel=0.0)     # поперечина-ручка
    # груз
    for (x, y, s, rz) in ((0.24, 0.0, 0.40, 6), (-0.18, -0.10, 0.34, -12)):
        P = fr.at((x, y, zb))
        crate(a, (P.x, P.y, P.z), s=s, rot=(0, 0, math.degrees(math.atan2(fr.m[1][0], fr.m[0][0])) + rz))
    crate(a, tuple(fr.at((0.24, 0.0, zb + 0.40))), s=0.30,
          rot=(0, 0, math.degrees(math.atan2(fr.m[1][0], fr.m[0][0])) + 20))
    _sack(a, fr.sub((-0.20, 0.17, zb), rz=30), rng, s=0.80)
    _sack(a, fr.sub((-0.26, 0.05, zb + 0.30), rot=(70, 0, 20)), rng, s=0.70, col="burlap_dark")


def _pack(a, fr, body="burlap", flap="leather", roll="roof"):
    """Заплечный мешок, висящий на колышке: корпус, клапан, скатка сверху, лямки до колышка.
    Верх корпуса в z=0 рамы, лицом к -Y."""
    fr.box(a, (0.34, 0.20, 0.42), (0, 0, -0.21), col=body, bevel=0.05)
    fr.box(a, (0.36, 0.22, 0.16), (0, -0.01, -0.06), col=flap, bevel=0.04)
    fr.box(a, (0.10, 0.04, 0.10), (0, -0.13, -0.15), col="gold", bevel=0.0)
    fr.cyl(a, 0.08, 0.08, 0.40, 8, loc=(-0.20, 0.02, 0.04), rot=(0, 90, 0), col=roll)
    for sx in (-1, 1):
        fr.box(a, (0.05, 0.05, 0.16), (sx * 0.09, 0.06, 0.10), col="leather_dark", bevel=0.0)


def _rack(a, fr):
    """Стойка носильщиков: два столба, брус с колышками; сверху лежит коромысло с крюками,
    на колышках — два заплечных мешка, у столбов — вёдра для коромысла."""
    for sx in (-1, 1):
        fr.box(a, (0.13, 0.13, 1.48), (sx * 0.60, 0, 0.74), col="wood_dark", bevel=0.03)
        fr.cyl(a, 0.10, 0.0, 0.12, 4, loc=(sx * 0.60, 0, 1.48), spin=45, col="wood_dark")
    fr.box(a, (1.38, 0.12, 0.14), (0, 0, 1.24), col="wood_mid", bevel=0.03)
    fr.box(a, (1.30, 0.10, 0.10), (0, 0.0, 0.30), col="wood_mid", bevel=0.0)
    # коромысло: дуга из трёх брусков, лежит на рогатинах над брусом
    for sx in (-1, 1):
        fr.box(a, (0.06, 0.10, 0.16), (sx * 0.30, -0.02, 1.36), col="wood_dark", bevel=0.0)
    fr.box(a, (0.62, 0.11, 0.10), (0, -0.02, 1.47), col="wood_light", bevel=0.0)
    for sx in (-1, 1):
        fr.box(a, (0.44, 0.10, 0.09), (sx * 0.50, -0.02, 1.41), rot=(0, sx * 14, 0), col="wood_light", bevel=0.0)
        fr.box(a, (0.05, 0.05, 0.16), (sx * 0.71, -0.02, 1.30), col="iron_dark", bevel=0.0)       # крюк
    for x, kw in ((-0.27, dict()), (0.25, dict(body="leather", flap="burlap_dark", roll="cream"))):
        fr.box(a, (0.06, 0.16, 0.06), (x, -0.10, 1.20), col="iron_dark", bevel=0.0)                # колышек
        _pack(a, fr.sub((x, -0.20, 1.10), rot=(4, 0, 0)), **kw)
    for sx in (-1, 1):
        bucket(a, fr.sub((sx * 0.36, 0.14, 0.0)), r=0.15, h=0.26, fill="water")


def build(a):
    rng = random.Random(23)
    # мощёный двор перед входом: тележка и стойка стоят на нём, как реквизит таверны на террасе
    a.add(p_box((W + 1.60, 1.30, 0.10), loc=(0.10, YF - 0.63, 0.01), bevel=0.03), STONE_TOP)
    zr = _hall(a)
    _arch_door(a)
    _emblem(a, zr)
    _tower(a)
    # фонарь справа от портала на кронштейне
    a.add(p_box((0.10, 0.34, 0.10), loc=(1.22, YF - 0.15, 1.92), bevel=0.0), "wood_dark")
    lantern(a, Frame((1.22, YF - 0.30, 1.56)))
    _cart(a, Frame((1.55, YF - 0.58, 0.06), rz=155), rng)
    _rack(a, Frame((-1.08, YF - 0.42, 0.06)))
    for (x, y, rz, s, col) in ((-2.22, -1.62, 10, 1.0, "burlap"), (-1.86, -1.92, -30, 0.9, "burlap_dark"),
                               (-2.10, -1.80, 60, 0.85, "burlap")):
        _sack(a, Frame((x, y, 0.06), rz=rz), rng, s=s, col=col)


# =========================================================================================
# Уровни 2 и 3 (vitaria_buildings/levels.py)
# =========================================================================================
def upgrade(a, level):
    """2: флажок на правом скате зала, тачка-тачанка с мешками перед правым углом, штабель ящиков
    у левого угла зала. 3: + звонница с золотым колоколом на коньке, золото по коньку и навершия, знамя
    на башенке, фонарь слева от портала, золотое навершие шатра башенки."""
    rng = random.Random(43)
    zr = ZT + 0.08 + (W / 2) * math.tan(math.radians(PITCH))
    t = math.tan(math.radians(PITCH))
    pennant(a, 0.72, -0.62, zr - 0.72 * t + 0.08, level, h=1.20, side=1)
    for k, (x, y, z, rz) in enumerate(((-2.30, -0.80, 0.0, 6), (-2.28, -0.36, 0.0, -8), (-2.30, -0.58, 0.44, 14))):
        crate(a, (x, y, z), s=0.44, rot=(0, 0, rz))
    wb = Frame((1.95, -2.78, 0.0), rz=-20)
    wb.prism(a, [(-0.30, 0.0), (0.12, 0.0), (0.36, 0.26), (-0.36, 0.26)], 0.46, loc=(0, 0, 0.22),
             col=lambda f: "wood_dark" if f.normal.z < -0.5 else "wood_mid")
    wb.cyl(a, 0.16, 0.16, 0.06, 10, loc=(0.40, 0.0, 0.16), rot=(90, 0, 0), col="wood_dark")
    for sy in (-1, 1):
        wb.box(a, (0.62, 0.05, 0.05), (-0.52, sy * 0.18, 0.28), rot=(0, -10, 0), col="wood_mid", bevel=0.0)
    _sack(a, wb.sub((-0.02, 0.0, 0.30)), rng, s=0.8)
    if level < 3:
        return
    belfry(a, 0.0, 0.55, zr - 0.10, level)
    gold_ridge(a, D + 2 * 0.30 + 0.14, zr, axis="y")
    top = TS - 0.10
    wall_banner(a, TX, TY - top / 2 - 0.06, 2.50, w=0.36, h=0.62)
    a.add(p_box((0.10, 0.34, 0.10), loc=(-1.22, YF - 0.15, 1.92), bevel=0.0), "wood_dark")
    lantern(a, Frame((-1.22, YF - 0.30, 1.56)))
    zb = TZ + 0.06
    finial(a, TX - 0.02, TY - 0.10, zb + 0.70, h=0.20)
