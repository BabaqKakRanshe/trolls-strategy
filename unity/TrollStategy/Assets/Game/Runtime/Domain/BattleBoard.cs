using System;
using System.Collections.Generic;

namespace TrollStrategy.Domain
{
    public readonly struct BattlePlacement
    {
        public string UnitId { get; }
        public Cell Cell { get; }

        public BattlePlacement(string unitId, Cell cell)
        {
            UnitId = unitId;
            Cell = cell;
        }
    }

    /// <summary>Mission-sized odd-r hex board. Cell rules are independent of the Unity scene.</summary>
    public sealed class BattleBoard
    {
        private readonly HashSet<Cell> _blocked;
        private readonly HashSet<Cell> _deployment;

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyCollection<Cell> BlockedCells => _blocked;
        public IReadOnlyCollection<Cell> DeploymentCells => _deployment;

        public BattleBoard(int width, int height, IEnumerable<Cell> blocked, IEnumerable<Cell> deployment)
        {
            if (width < 3 || height < 3)
                throw new ArgumentOutOfRangeException(nameof(width), "Поле должно быть не меньше 3×3");

            Width = width;
            Height = height;
            _blocked = CopyCells(blocked, nameof(blocked));
            _deployment = CopyCells(deployment, nameof(deployment));

            if (_deployment.Count == 0)
                throw new ArgumentException("Нужна хотя бы одна клетка расстановки", nameof(deployment));
            foreach (var cell in _deployment)
                if (_blocked.Contains(cell))
                    throw new ArgumentException($"Преграда перекрывает клетку расстановки {cell}", nameof(blocked));
        }

        public bool Contains(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
        public bool IsBlocked(Cell cell) => _blocked.Contains(cell);
        public bool CanPlace(Cell cell) => _deployment.Contains(cell) && !_blocked.Contains(cell);
        public bool IsWalkable(Cell cell) => Contains(cell) && !_blocked.Contains(cell);

        public IEnumerable<Cell> Neighbours(Cell cell)
        {
            // Fixed order makes shortest-path ties repeatable.
            int diagonalLeft = (cell.Y & 1) == 0 ? -1 : 0;
            int diagonalRight = diagonalLeft + 1;
            var candidates = new[]
            {
                new Cell(cell.X - 1, cell.Y), new Cell(cell.X + 1, cell.Y),
                new Cell(cell.X + diagonalLeft, cell.Y - 1),
                new Cell(cell.X + diagonalRight, cell.Y - 1),
                new Cell(cell.X + diagonalLeft, cell.Y + 1),
                new Cell(cell.X + diagonalRight, cell.Y + 1)
            };
            for (int i = 0; i < candidates.Length; i++)
                if (IsWalkable(candidates[i]))
                    yield return candidates[i];
        }

        public static int HexDistance(Cell a, Cell b)
        {
            int aq = a.X - (a.Y - (a.Y & 1)) / 2;
            int bq = b.X - (b.Y - (b.Y & 1)) / 2;
            int dr = a.Y - b.Y;
            int dq = aq - bq;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        public bool TryShortestPath(Cell start, Cell destination, ISet<Cell> occupied, out List<Cell> path)
        {
            path = null;
            if (!IsWalkable(start) || !IsWalkable(destination)) return false;
            if (occupied != null && occupied.Contains(destination) && destination != start) return false;

            var queue = new Queue<Cell>();
            var previous = new Dictionary<Cell, Cell>();
            queue.Enqueue(start);
            previous.Add(start, start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    path = new List<Cell>();
                    for (var at = destination; at != start; at = previous[at]) path.Add(at);
                    path.Add(start);
                    path.Reverse();
                    return true;
                }

                foreach (var next in Neighbours(current))
                {
                    if (previous.ContainsKey(next) || (occupied != null && occupied.Contains(next))) continue;
                    previous.Add(next, current);
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private HashSet<Cell> CopyCells(IEnumerable<Cell> source, string argument)
        {
            var result = new HashSet<Cell>();
            if (source == null) return result;
            foreach (var cell in source)
            {
                if (!Contains(cell))
                    throw new ArgumentException($"Клетка {cell} вне поля {Width}×{Height}", argument);
                if (!result.Add(cell))
                    throw new ArgumentException($"Клетка {cell} указана дважды", argument);
            }
            return result;
        }
    }
}
