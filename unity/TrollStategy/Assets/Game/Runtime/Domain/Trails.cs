using System;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    /// <summary>What a cell's wear looks like and how fast creatures cross it (EconomyConfig: Trail*).</summary>
    public enum TrailStage
    {
        /// <summary>Lawn as it grew.</summary>
        Grass = 0,
        /// <summary>Trodden-down grass: only the look changes.</summary>
        Trampled = 1,
        /// <summary>An earth path: creatures walk faster.</summary>
        Path = 2,
        /// <summary>A wide beaten road: faster still.</summary>
        Road = 3
    }

    /// <summary>
    /// How trodden every cell of the colony is, from 0 (lawn) to <see cref="Max"/>, and the colony time since a
    /// creature last stepped into it. Cell (x, y) has index y * Width + x.
    /// </summary>
    [Serializable]
    public sealed class TrailState
    {
        public const int Max = 100;

        private readonly byte[] _wear;
        private readonly int[] _quietMs;

        public TrailState(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _wear = new byte[width * height];
            _quietMs = new int[_wear.Length];
        }

        public int Width { get; }
        public int Height { get; }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int Wear(int x, int y) => Inside(x, y) ? _wear[y * Width + x] : 0;

        public int Wear(Cell cell) => Wear(cell.X, cell.Y);

        /// <summary>Milliseconds of colony time since a creature last stepped into the cell.</summary>
        public int QuietMs(int x, int y) => Inside(x, y) ? _quietMs[y * Width + x] : 0;

        /// <summary>Copies the wear of every cell, by index, into <paramref name="target"/>.</summary>
        public void CopyWear(byte[] target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Array.Copy(_wear, target, Math.Min(_wear.Length, target.Length));
        }

        /// <summary>Sets a cell's wear as if a creature had just stepped in (a new game, a test).</summary>
        public void Set(int x, int y, int wear)
        {
            if (!Inside(x, y)) throw new ArgumentOutOfRangeException($"({x}, {y})");
            int i = y * Width + x;
            _wear[i] = (byte)Math.Clamp(wear, 0, Max);
            _quietMs[i] = 0;
        }

        internal void Step(int x, int y, int amount)
        {
            if (amount <= 0 || !Inside(x, y)) return;
            int i = y * Width + x;
            _wear[i] = (byte)Math.Min(Max, _wear[i] + amount);
            _quietMs[i] = 0;
        }

        internal void Erase(int x, int y)
        {
            if (!Inside(x, y)) return;
            int i = y * Width + x;
            _wear[i] = 0;
            _quietMs[i] = 0;
        }

        /// <summary>
        /// Lets every cell rest for <paramref name="milliseconds"/>: one quiet longer than the grace loses a point
        /// of wear every <paramref name="decayMs"/>.
        /// </summary>
        internal void Overgrow(int milliseconds, int graceMs, int decayMs)
        {
            if (milliseconds <= 0) return;
            decayMs = Math.Max(1, decayMs);
            for (int i = 0; i < _wear.Length; i++)
            {
                if (_wear[i] == 0) continue;
                int before = _quietMs[i];
                int after = before > int.MaxValue - milliseconds ? int.MaxValue : before + milliseconds;
                _quietMs[i] = after;
                int lost = Lost(after, graceMs, decayMs) - Lost(before, graceMs, decayMs);
                if (lost <= 0) continue;
                _wear[i] = (byte)Math.Max(0, _wear[i] - lost);
                if (_wear[i] == 0) _quietMs[i] = 0;
            }
        }

        private static int Lost(int quietMs, int graceMs, int decayMs) =>
            quietMs <= graceMs ? 0 : (quietMs - graceMs) / decayMs;

        public TrailState Clone()
        {
            var clone = new TrailState(Width, Height);
            Array.Copy(_wear, clone._wear, _wear.Length);
            Array.Copy(_quietMs, clone._quietMs, _quietMs.Length);
            return clone;
        }
    }

    /// <summary>
    /// Trails: every cell a creature steps into wears a little, worn cells turn into paths and roads that are
    /// quicker to walk, and cells nobody steps into grow back over (EconomyConfig: Trail*). Buildings erase what
    /// lies under them.
    /// </summary>
    public static class TrailRules
    {
        /// <summary>Walking pace of the lawn in the pace map: a cell with pace 1300 is crossed 1.3 times as fast.</summary>
        public const int LawnPace = 1000;

        /// <summary>A new colony's trails, all lawn; null when the economy has no trails.</summary>
        public static TrailState CreateStart(EconomyConfig economy) =>
            economy != null && economy.TrailsEnabled
                ? new TrailState(Math.Max(1, economy.GridWidth), Math.Max(1, economy.GridHeight))
                : null;

        public static TrailStage Stage(int wear, EconomyConfig economy) =>
            wear >= economy.TrailRoadAt ? TrailStage.Road
            : wear >= economy.TrailPathAt ? TrailStage.Path
            : wear >= economy.TrailTrampledAt ? TrailStage.Trampled
            : TrailStage.Grass;

        /// <summary>How many times faster than on the lawn creatures walk over a cell of this wear.</summary>
        public static float SpeedFactor(int wear, EconomyConfig economy) =>
            wear >= economy.TrailRoadAt ? economy.TrailRoadSpeed
            : wear >= economy.TrailPathAt ? economy.TrailPathSpeed
            : 1f;

        /// <summary>Speed factor of the cell under the point; 1 without trails.</summary>
        public static float SpeedAt(GameState state, WorldPosition point, EconomyConfig economy)
        {
            if (state?.Trails == null || economy == null) return 1f;
            float cs = economy.CellSize;
            return SpeedFactor(state.Trails.Wear((int)Math.Floor(point.X / cs), (int)Math.Floor(point.Y / cs)), economy);
        }

        /// <summary>
        /// A creature walked from <paramref name="from"/> to <paramref name="to"/>: every cell it stepped into on
        /// the way, not the one it left, gains <paramref name="wear"/>.
        /// </summary>
        public static void Walk(GameState state, WorldPosition from, WorldPosition to, int wear, EconomyConfig economy)
        {
            var trails = state?.Trails;
            if (trails == null || wear <= 0 || economy == null) return;
            var walk = new CellWalk(from, to, economy.CellSize);
            while (walk.Next(out var cell, out _))
                trails.Step(cell.X, cell.Y, wear);
        }

        /// <summary>One colony step: quiet cells grow over, footprints erase what lies under them.</summary>
        public static void Tick(GameState state, float deltaSeconds, GameContentCatalog catalog)
        {
            var trails = state?.Trails;
            if (trails == null || deltaSeconds <= 0f) return;
            var economy = catalog.Economy;
            trails.Overgrow(Milliseconds(deltaSeconds), Milliseconds(economy.TrailGraceSeconds),
                Milliseconds(economy.TrailDecaySeconds));
            foreach (var building in state.Buildings)
            {
                var def = catalog.GetBuilding(building.Kind);
                if (def == null) continue;
                for (int y = building.Cell.Y; y < building.Cell.Y + def.Height; y++)
                for (int x = building.Cell.X; x < building.Cell.X + def.Width; x++)
                    trails.Erase(x, y);
            }
        }

        /// <summary>Pace of a speed factor in thousandths of the lawn's.</summary>
        public static int Pace(float speedFactor) => Math.Max(1, (int)Math.Round(speedFactor * LawnPace));

        /// <summary>The quickest pace any cell can have: the road's.</summary>
        public static int MaxPace(EconomyConfig economy) => Math.Max(LawnPace, Pace(economy.TrailRoadSpeed));

        /// <summary>Pace of every cell by index (y * GridWidth + x); null without trails, when all cells are lawn.</summary>
        public static int[] PaceMap(GameState state, EconomyConfig economy)
        {
            var trails = state?.Trails;
            if (trails == null || economy == null) return null;
            int width = economy.GridWidth, height = economy.GridHeight;
            int path = Pace(economy.TrailPathSpeed), road = Pace(economy.TrailRoadSpeed);
            var pace = new int[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int wear = trails.Wear(x, y);
                pace[y * width + x] = wear >= economy.TrailRoadAt ? road : wear >= economy.TrailPathAt ? path : LawnPace;
            }
            return pace;
        }

        private static int Milliseconds(float seconds) => (int)Math.Round(Math.Max(0f, seconds) * 1000.0);
    }

    /// <summary>
    /// The cells a segment passes through after the one it starts in, in order (a grid walk after Amanatides and
    /// Woo), each with the share of the segment walked when it is entered. Always ends in the end point's cell;
    /// through an exact corner it steps along Y first.
    /// </summary>
    public struct CellWalk
    {
        private int _x, _y;
        private readonly int _endX, _endY, _stepX, _stepY;
        private float _nextX, _nextY;
        private readonly float _deltaX, _deltaY;

        public CellWalk(WorldPosition from, WorldPosition to, float cellSize)
        {
            float cs = cellSize > 0f ? cellSize : 1f;
            _x = (int)Math.Floor(from.X / cs);
            _y = (int)Math.Floor(from.Y / cs);
            _endX = (int)Math.Floor(to.X / cs);
            _endY = (int)Math.Floor(to.Y / cs);
            _stepX = Math.Sign(_endX - _x);
            _stepY = Math.Sign(_endY - _y);
            float dx = to.X - from.X, dy = to.Y - from.Y;
            _deltaX = dx != 0f ? cs / Math.Abs(dx) : float.PositiveInfinity;
            _deltaY = dy != 0f ? cs / Math.Abs(dy) : float.PositiveInfinity;
            _nextX = dx > 0f ? ((_x + 1) * cs - from.X) / dx : dx < 0f ? (_x * cs - from.X) / dx : float.PositiveInfinity;
            _nextY = dy > 0f ? ((_y + 1) * cs - from.Y) / dy : dy < 0f ? (_y * cs - from.Y) / dy : float.PositiveInfinity;
            Start = new Cell(_x, _y);
        }

        /// <summary>The cell the segment starts in.</summary>
        public Cell Start { get; }

        public bool Next(out Cell cell, out float enteredAt)
        {
            if (_x == _endX && _y == _endY)
            {
                cell = default;
                enteredAt = 1f;
                return false;
            }
            bool alongX = _y == _endY || (_x != _endX && _nextX < _nextY);
            if (alongX)
            {
                _x += _stepX;
                enteredAt = _nextX;
                _nextX += _deltaX;
            }
            else
            {
                _y += _stepY;
                enteredAt = _nextY;
                _nextY += _deltaY;
            }
            enteredAt = Math.Clamp(enteredAt, 0f, 1f);
            cell = new Cell(_x, _y);
            return true;
        }
    }
}
