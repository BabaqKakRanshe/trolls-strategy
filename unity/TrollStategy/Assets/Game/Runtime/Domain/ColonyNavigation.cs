using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    /// <summary>
    /// Walking rules for the colony: units cross open cells of cleared land, never a building footprint,
    /// and reach a building through the approach point in front of its entrance.
    /// </summary>
    public static class ColonyNavigation
    {
        // Straightened route legs keep this far from footprints so units do not clip wall corners.
        public const float WallClearanceCells = 0.25f;
        private const float ApproachDistanceCells = 0.5f;
        // Waiting units stand in a loose group in front of the door. All group sizes scale with the building's
        // crowd spacing; two places are never closer than CrowdMinGapRatio of it.
        public const float CrowdMinGapRatio = 0.9f;
        private const float CrowdInnerRatio = 1.5f;
        private const float CrowdJitterRatio = 0.2f;
        private const float CrowdSpreadRadians = 1.3f;
        private static readonly double GoldenAngle = Math.PI * (3 - Math.Sqrt(5));
        // Step costs are walking times: a lawn step costs this much, a step onto a trail less (TrailRules.PaceMap).
        private const int StraightCost = 1000;
        private const int DiagonalCost = 1400;

        /// <summary>Where a building is entered: the door, the open point in front of it and the facade normal.</summary>
        public readonly struct Doorway
        {
            public Doorway(WorldPosition entrance, WorldPosition approach, float normalX, float normalY)
            {
                Entrance = entrance;
                Approach = approach;
                NormalX = normalX;
                NormalY = normalY;
            }

            public WorldPosition Entrance { get; }
            public WorldPosition Approach { get; }
            public float NormalX { get; }
            public float NormalY { get; }
            // Along the facade, used to spread units beside the door.
            public float TangentX => -NormalY;
            public float TangentY => NormalX;
        }

        public static Doorway DoorwayOf(BuildingState building, GameContentCatalog catalog) =>
            DoorwayAt(building.Kind, building.Cell, catalog);

        /// <summary>
        /// An entrance in front of the building is walked to directly; one inside is reached from
        /// the open point just outside the facade nearest to it.
        /// </summary>
        public static Doorway DoorwayAt(BuildingKind kind, Cell cell, GameContentCatalog catalog)
        {
            var def = catalog.GetBuilding(kind);
            float cs = catalog.Economy.CellSize;
            float ex = def.EntranceX, ey = def.EntranceY;
            var entrance = new WorldPosition((cell.X + ex) * cs, (cell.Y + ey) * cs);
            if (ex < 0f || ey < 0f || ex > def.Width || ey > def.Height)
            {
                // The side the entrance stands furthest beyond faces it.
                float sx = 0f, sy = -1f, beyond = -ey;
                if (-ex > beyond) { beyond = -ex; sx = -1f; sy = 0f; }
                if (ex - def.Width > beyond) { beyond = ex - def.Width; sx = 1f; sy = 0f; }
                if (ey - def.Height > beyond) { sx = 0f; sy = 1f; }
                return new Doorway(entrance, entrance, sx, sy);
            }

            // Ties resolve south, west, east, north so a centred door keeps a stable side.
            float nx = 0f, ny = -1f, best = ey;
            float ax = ex, ay = -ApproachDistanceCells;
            if (ex < best) { best = ex; nx = -1f; ny = 0f; ax = -ApproachDistanceCells; ay = ey; }
            if (def.Width - ex < best) { best = def.Width - ex; nx = 1f; ny = 0f; ax = def.Width + ApproachDistanceCells; ay = ey; }
            if (def.Height - ey < best) { nx = 0f; ny = 1f; ax = ex; ay = def.Height + ApproachDistanceCells; }
            return new Doorway(entrance, new WorldPosition((cell.X + ax) * cs, (cell.Y + ay) * cs), nx, ny);
        }

        public static WorldPosition CrowdSlotPosition(BuildingState building, int slot, GameContentCatalog catalog) =>
            CrowdSlotPosition(DoorwayOf(building, catalog), slot, building.Cell.X * 31 + building.Cell.Y * 17,
                catalog.GetBuilding(building.Kind).CrowdSpacingCells, catalog.Economy.CellSize);

        /// <summary>
        /// Place <paramref name="slot"/> of the loose group in front of a door: a sunflower spiral over the
        /// half-disc facing out, nudged by a jitter fixed per slot and <paramref name="seed"/> so it looks unordered.
        /// </summary>
        public static WorldPosition CrowdSlotPosition(Doorway doorway, int slot, int seed, float spacingCells, float cellSize)
        {
            float radius = spacingCells * (CrowdInnerRatio + (float)Math.Sqrt(slot + 0.5));
            double turn = slot * GoldenAngle % (2 * Math.PI) / (2 * Math.PI);
            float angle = (float)(turn - 0.5) * 2f * CrowdSpreadRadians;
            float jitter = spacingCells * CrowdJitterRatio;
            float outward = radius * (float)Math.Cos(angle) + Jitter(slot, seed, 0) * jitter;
            float side = radius * (float)Math.Sin(angle) + Jitter(slot, seed, 1) * jitter;
            return new WorldPosition(
                doorway.Approach.X + (doorway.NormalX * outward + doorway.TangentX * side) * cellSize,
                doorway.Approach.Y + (doorway.NormalY * outward + doorway.TangentY * side) * cellSize);
        }

        // Repeatable offset in [-1, 1] from an integer hash.
        private static float Jitter(int slot, int seed, int axis)
        {
            unchecked
            {
                uint h = (uint)(slot * 73856093) ^ (uint)(seed * 19349663) ^ (uint)(axis * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return ((h & 0xffff) / 65535f - 0.5f) * 2f;
            }
        }

        public static bool IsWalkablePoint(GameState state, WorldPosition point, GameContentCatalog catalog) =>
            IsWalkable(state, CellAt(point, catalog.Economy.CellSize), catalog);

        /// <summary>Cell a building's approach point lies in; it must stay walkable for the building to be usable.</summary>
        public static Cell ApproachCell(BuildingKind kind, Cell cell, GameContentCatalog catalog) =>
            CellAt(DoorwayAt(kind, cell, catalog).Approach, catalog.Economy.CellSize);

        public static bool IsWalkable(GameState state, Cell cell, GameContentCatalog catalog, string ignoredId = null)
        {
            var economy = catalog.Economy;
            if (cell.X < 0 || cell.Y < 0 || cell.X >= economy.GridWidth || cell.Y >= economy.GridHeight) return false;
            // no walking through the forest or over the clouds: only cleared land
            if (!LandRules.IsOpen(state, cell)) return false;
            foreach (var building in state.Buildings)
            {
                if (building.Id == ignoredId) continue;
                var def = catalog.GetBuilding(building.Kind);
                if (ColonySimulation.BuildingOccupiesCell(building.Cell, def.Width, def.Height, cell)) return false;
            }
            return true;
        }

        /// <summary>
        /// Waypoints from <paramref name="from"/> to <paramref name="goal"/>, ending at the goal.
        /// A unit inside a building leaves through its doorway; a goal inside <paramref name="goalBuilding"/>
        /// is reached through that building's doorway. Returns null when the open ground does not connect.
        /// </summary>
        public static List<WorldPosition> FindRoute(GameState state, WorldPosition from, WorldPosition goal,
            BuildingState goalBuilding, GameContentCatalog catalog)
        {
            var route = new List<WorldPosition>();
            if (goalBuilding != null && ColonySimulation.IsPointWithinBuilding(goalBuilding, from, catalog))
            {
                route.Add(goal);
                return route;
            }

            var start = from;
            var occupied = BuildingAt(state, from, catalog);
            if (occupied != null)
            {
                start = DoorwayOf(occupied, catalog).Approach;
                route.Add(start);
            }

            var target = goalBuilding != null ? DoorwayOf(goalBuilding, catalog).Approach : goal;
            var open = OpenPath(state, start, target, catalog);
            if (open == null) return null;
            route.AddRange(open);
            if (goalBuilding != null) route.Add(goal);
            return route;
        }

        // Grid search between cell centres, then straightened where the line stays clear of footprints.
        private static List<WorldPosition> OpenPath(GameState state, WorldPosition start, WorldPosition target,
            GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            float cs = economy.CellSize;
            // trails make some cells quicker to cross; without them every lawn step costs the same
            var pace = TrailRules.PaceMap(state, economy);
            var cells = FindCellPath(state, CellAt(start, cs), CellAt(target, cs), catalog, pace);
            if (cells == null) return null;

            var points = new List<WorldPosition>(cells.Count + 1) { start };
            for (int i = 1; i < cells.Count - 1; i++)
                points.Add(new WorldPosition((cells[i].X + 0.5f) * cs, (cells[i].Y + 0.5f) * cs));
            points.Add(target);
            // with trails a straight cut may not be slower than the trodden way it replaces
            var along = pace != null ? WalkTimes(points, pace, economy) : null;

            var result = new List<WorldPosition>();
            int at = 0;
            while (at < points.Count - 1)
            {
                int next = at + 1;
                for (int j = points.Count - 1; j > next; j--)
                {
                    if (!IsSegmentClear(state, points[at], points[j], catalog)) continue;
                    if (along != null && WalkTime(points[at], points[j], pace, economy) > along[j] - along[at] + 1e-4)
                        continue;
                    next = j;
                    break;
                }
                result.Add(points[next]);
                at = next;
            }
            return result;
        }

        // A* over walkable cells with 8 neighbours; diagonals never cut a blocked corner. Ties break by cell index.
        // A step costs its walking time at the pace of the cell stepped into (all lawn when pace is null).
        private static List<Cell> FindCellPath(GameState state, Cell start, Cell goal, GameContentCatalog catalog,
            int[] pace)
        {
            var economy = catalog.Economy;
            int width = economy.GridWidth, height = economy.GridHeight;
            if (!Inside(start, width, height) || !IsWalkable(state, goal, catalog)) return null;
            if (start.Equals(goal)) return new List<Cell> { start };

            int count = width * height;
            var walkable = new bool[count];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                walkable[y * width + x] = IsWalkable(state, new Cell(x, y), catalog);
            // The unit already stands in its start cell, even if it is at a doorway on a footprint edge.
            walkable[start.Y * width + start.X] = true;

            var cost = new int[count];
            var previous = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++) { cost[i] = int.MaxValue; previous[i] = -1; }
            int startIndex = start.Y * width + start.X, goalIndex = goal.Y * width + goal.X;
            cost[startIndex] = 0;
            // the estimate counts every step at the quickest pace there is, so it never overshoots
            int maxPace = pace != null ? TrailRules.MaxPace(economy) : TrailRules.LawnPace;
            int fastStraight = StraightCost * TrailRules.LawnPace / maxPace;
            int fastDiagonal = DiagonalCost * TrailRules.LawnPace / maxPace;
            var open = new List<int> { startIndex };

            while (open.Count > 0)
            {
                int bestSlot = 0;
                for (int i = 1; i < open.Count; i++)
                {
                    int a = open[i], b = open[bestSlot];
                    int fa = cost[a] + Heuristic(a, goalIndex, width, fastStraight, fastDiagonal);
                    int fb = cost[b] + Heuristic(b, goalIndex, width, fastStraight, fastDiagonal);
                    if (fa < fb || (fa == fb && a < b)) bestSlot = i;
                }
                int current = open[bestSlot];
                open.RemoveAt(bestSlot);
                if (current == goalIndex) break;
                if (closed[current]) continue;
                closed[current] = true;

                int cx = current % width, cy = current / width;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int neighbour = ny * width + nx;
                    if (!walkable[neighbour] || closed[neighbour]) continue;
                    if (dx != 0 && dy != 0 && (!walkable[cy * width + nx] || !walkable[ny * width + cx])) continue;
                    int step = dx != 0 && dy != 0 ? DiagonalCost : StraightCost;
                    int candidate = cost[current] + (pace != null ? step * TrailRules.LawnPace / pace[neighbour] : step);
                    if (candidate >= cost[neighbour]) continue;
                    cost[neighbour] = candidate;
                    previous[neighbour] = current;
                    if (!open.Contains(neighbour)) open.Add(neighbour);
                }
            }

            if (previous[goalIndex] < 0) return null;
            var path = new List<Cell>();
            for (int at = goalIndex; at >= 0; at = previous[at])
                path.Add(new Cell(at % width, at / width));
            path.Reverse();
            return path;
        }

        private static int Heuristic(int from, int to, int width, int straight, int diagonal)
        {
            int dx = Math.Abs(from % width - to % width), dy = Math.Abs(from / width - to / width);
            return straight * Math.Max(dx, dy) + (diagonal - straight) * Math.Min(dx, dy);
        }

        // Walking time from the first point to each point of the polyline, in lawn cells.
        private static double[] WalkTimes(List<WorldPosition> points, int[] pace, EconomyConfig economy)
        {
            var along = new double[points.Count];
            for (int i = 1; i < points.Count; i++)
                along[i] = along[i - 1] + WalkTime(points[i - 1], points[i], pace, economy);
            return along;
        }

        // Walking time of a straight leg in lawn cells: each cell's share of the leg at that cell's pace.
        private static double WalkTime(WorldPosition a, WorldPosition b, int[] pace, EconomyConfig economy)
        {
            float cs = economy.CellSize;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double length = Math.Sqrt(dx * dx + dy * dy) / cs;
            if (length <= 0) return 0;
            var walk = new CellWalk(a, b, cs);
            var cell = walk.Start;
            double time = 0, at = 0;
            while (walk.Next(out var next, out float enteredAt))
            {
                time += (enteredAt - at) / PaceAt(cell, pace, economy);
                at = enteredAt;
                cell = next;
            }
            time += (1 - at) / PaceAt(cell, pace, economy);
            return time * length * TrailRules.LawnPace;
        }

        private static double PaceAt(Cell cell, int[] pace, EconomyConfig economy) =>
            Inside(cell, economy.GridWidth, economy.GridHeight)
                ? pace[cell.Y * economy.GridWidth + cell.X]
                : TrailRules.LawnPace;

        /// <summary>True when the segment keeps the wall clearance from every footprint.</summary>
        public static bool IsSegmentClear(GameState state, WorldPosition a, WorldPosition b, GameContentCatalog catalog)
        {
            float cs = catalog.Economy.CellSize;
            float margin = WallClearanceCells * cs;
            foreach (var building in state.Buildings)
            {
                var def = catalog.GetBuilding(building.Kind);
                if (SegmentHitsBox(a, b,
                        building.Cell.X * cs - margin, building.Cell.Y * cs - margin,
                        (building.Cell.X + def.Width) * cs + margin, (building.Cell.Y + def.Height) * cs + margin))
                    return false;
            }
            return true;
        }

        // Liang-Barsky clip of segment a-b against an axis-aligned box.
        private static bool SegmentHitsBox(WorldPosition a, WorldPosition b, float minX, float minY, float maxX, float maxY)
        {
            float t0 = 0f, t1 = 1f;
            float dx = b.X - a.X, dy = b.Y - a.Y;
            return Clip(-dx, a.X - minX, ref t0, ref t1) && Clip(dx, maxX - a.X, ref t0, ref t1) &&
                   Clip(-dy, a.Y - minY, ref t0, ref t1) && Clip(dy, maxY - a.Y, ref t0, ref t1);
        }

        private static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        private static BuildingState BuildingAt(GameState state, WorldPosition point, GameContentCatalog catalog)
        {
            foreach (var building in state.Buildings)
                if (ColonySimulation.IsPointWithinBuilding(building, point, catalog)) return building;
            return null;
        }

        private static Cell CellAt(WorldPosition point, float cellSize) =>
            new((int)Math.Floor(point.X / cellSize), (int)Math.Floor(point.Y / cellSize));

        private static bool Inside(Cell cell, int width, int height) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
    }
}
