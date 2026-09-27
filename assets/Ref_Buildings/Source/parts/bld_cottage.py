"""Малый каменный дом с синей кровлей — эталон идиомы для остальных зданий."""
from build_vitaria import p_box, by_normal
from parts.common import gable_roof, gable_wall, chimney, cornice, plank_door, window

NAME = "Bld_Cottage"
TARGET = (3.15, 2.80, 3.10)   # w, d, h в метрах


def build(a):
    W, D, H, F = 2.30, 2.00, 1.45, 0.26
    zt = F + H

    # цоколь и каменные стены; by_normal даёт светлый верх и тёмный низ без света
    a.add(p_box((W + 0.26, D + 0.26, F + 0.14), loc=(0, 0, (F - 0.14) / 2), bevel=0.06), "stone_dark")
    a.add(p_box((W, D, H), loc=(0, 0, F + H / 2), bevel=0.05),
          by_normal("stone_light", "stone_mid", "stone_dark", 0.8))

    # угловые пилястры — читаемый силуэт на игровой дистанции
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.add(p_box((0.26, 0.26, H), loc=(sx * W / 2, sy * D / 2, F + H / 2), bevel=0.05), "stone_light")

    cornice(a, W, D, zt - 0.05)

    zr = gable_roof(a, W, D, zt)
    gable_wall(a, W, zt, zr, D)
    chimney(a, -W / 2 + 0.34, 0.42, zt - 0.1, h=1.05, w=0.40)

    yf = -D / 2
    plank_door(a, yf, F)
    a.add(p_box((1.05, 0.44, 0.16), loc=(0, yf - 0.24, 0.07), bevel=0.04), "stone_light")
    window(a, (-0.76, yf + 0.02, F + 0.98), w=0.36, h=0.44)
    window(a, (0.76, yf + 0.02, F + 0.98), w=0.36, h=0.44)
    for sx in (-1, 1):
        window(a, (sx * (W / 2 + 0.02), 0.22, F + 0.96), rot=(0, 0, 90), w=0.40, h=0.46)
