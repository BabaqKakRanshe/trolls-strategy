"""
Геометрия боевого поля — точная копия BattleBoard / BattleBoardView из игры.

Поле odd-r: гекс острым углом вдоль оси глубины, 2 м между плоскими гранями, нечётные ряды
сдвинуты на полклетки вправо. Координаты арены («пространство макета»): начало — середина
поля (BattleBoardView.middle), X вправо, Y от камеры вглубь, Z вверх. Камера боя стоит
со стороны -Y и смотрит на +Y. В Unity это X -> X, Y -> Z, Z -> Y; при экспорте build_arena
пересчитывает это сам (см. to_unity в build_arena.py).
"""
import math

HEX = 2.0                          # BattleBoardView.HexAcrossFlats
RADIUS = HEX / math.sqrt(3.0)      # центр — вершина, 1.1547
ROW = RADIUS * 1.5                 # шаг рядов, 1.7321

# Mission_01: 9 x 5, расстановка игрока — колонки 0–1, зона врага — колонки 7–8.
WIDTH, HEIGHT = 9, 5


def cell_center(x, y, w=WIDTH, h=HEIGHT):
    """Центр клетки (x, y) относительно середины поля — как CellWorldPosition минус middle."""
    cx = (x + (0.5 if y & 1 else 0.0)) * HEX
    cy = y * ROW
    max_x = (w - 1 + (0.5 if h > 1 else 0.0)) * HEX
    mid_x = max_x / 2.0
    mid_y = (h - 1) * ROW / 2.0
    return cx - mid_x, cy - mid_y


def cells(w=WIDTH, h=HEIGHT):
    return [(x, y) for y in range(h) for x in range(w)]


def hex_corners(cx, cy, r=RADIUS):
    """Вершины гекса острым углом вдоль Y (углы 30 + 60k, как в MakeCell)."""
    return [(cx + r * math.cos(math.radians(30 + 60 * k)), cy + r * math.sin(math.radians(30 + 60 * k)))
            for k in range(6)]


def zone(x, y, w=WIDTH):
    """'player' | 'enemy' | 'neutral' — как у Mission_01 (по две крайние колонки)."""
    if x <= 1:
        return "player"
    if x >= w - 2:
        return "enemy"
    return "neutral"


def dist_to_board(px, py, w=WIDTH, h=HEIGHT):
    """Расстояние от точки до поля, с запасом: до ближайшего центра минус радиус описанной
    окружности. У плоской грани гекса это на 15 см меньше настоящего — реквизит встаёт
    чуть дальше, чем мог бы, зато никогда не наезжает на клетку."""
    best = 1e9
    for x, y in cells(w, h):
        cx, cy = cell_center(x, y, w, h)
        d = math.hypot(px - cx, py - cy)
        best = min(best, d)
    return max(0.0, best - RADIUS)


def board_bounds(w=WIDTH, h=HEIGHT):
    xs, ys = [], []
    for x, y in cells(w, h):
        for px, py in hex_corners(*cell_center(x, y, w, h)):
            xs.append(px)
            ys.append(py)
    return min(xs), max(xs), min(ys), max(ys)
