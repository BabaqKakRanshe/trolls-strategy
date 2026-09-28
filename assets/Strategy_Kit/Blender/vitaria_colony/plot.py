"""
Поле колонии — копия сетки игры: EconomyConfig 14 x 14 клеток по 1 м (TilemapWorldView).

Пространство колонии: начало — середина поля (клетка карты (7, 7), уровень земли), X на восток,
Y на север (от камеры вглубь), Z вверх. В Unity это X -> X, Y -> Z, Z -> Y; корень окружения
ставится в worldView.MapToWorld((7, 7, 0)) без поворота (мировые оси: Y вверх, Z на север).
"""
import math

W, H, CELL = 14, 14, 1.0
HALF_W, HALF_H = W * CELL / 2.0, H * CELL / 2.0

# Стартовая раскладка сцены MainColonyScene: (вид, SW-клетка карты x, y, ширина, глубина)
STARTING = [("Market", 9, 4, 3, 2), ("Warehouse", 9, 10, 3, 3)]


def to_colony(mx, my):
    """Точка карты (в клетках, от SW-угла поля) -> координаты колонии."""
    return mx * CELL - HALF_W, my * CELL - HALF_H


def footprint_center(cx, cy, w, h):
    return to_colony(cx + w / 2.0, cy + h / 2.0)


def dist_to_plot(x, y):
    """Расстояние до поля (0 внутри)."""
    dx = max(abs(x) - HALF_W, 0.0)
    dy = max(abs(y) - HALF_H, 0.0)
    return math.hypot(dx, dy)


def inside(x, y, pad=0.0):
    return abs(x) <= HALF_W + pad and abs(y) <= HALF_H + pad


def corners(pad=0.0):
    return [(-HALF_W - pad, -HALF_H - pad), (HALF_W + pad, -HALF_H - pad), (HALF_W + pad, HALF_H + pad),
            (-HALF_W - pad, HALF_H + pad)]
