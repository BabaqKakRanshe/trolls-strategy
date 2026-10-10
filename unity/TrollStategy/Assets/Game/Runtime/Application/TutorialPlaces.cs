using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>
    /// Where the tutorial suggests putting things: the free cell nearest the warehouse door for a hire, a place west
    /// of the warehouse (the left of the screen, where the start land is open) for a new building, the free creature
    /// nearest a building for an order. It only reads: every cell it
    /// offers passes the same check as the purchase (<see cref="GameSession.CanBuyUnits"/>,
    /// <see cref="GameSession.CanPlaceBuilding"/>), so the pointer, the first worker's quick hire and the map of free
    /// cells agree with what the session accepts.
    /// </summary>
    public static class TutorialPlaces
    {
        /// <summary>Cells left free between the warehouse and the building the tutorial suggests beside it.</summary>
        public const int LaneCells = 2;

        /// <summary>The colony's first warehouse, or null.</summary>
        public static BuildingSnapshot Warehouse(GameSnapshot snapshot)
        {
            if (snapshot == null) return null;
            foreach (var building in snapshot.Buildings)
                if (building.Kind == BuildingKind.Warehouse) return building;
            return null;
        }

        /// <summary>
        /// The free cell nearest the warehouse door where <paramref name="amount"/> creatures of the kind may be hired
        /// now; null without a warehouse or a cell the session would accept.
        /// </summary>
        public static Cell? HireCell(GameSession session, UnitKind kind, int amount = 1)
        {
            if (session == null) return null;
            var door = DoorCell(session);
            if (door == null) return null;
            foreach (var cell in Rings(door.Value, Reach(session)))
                if (session.CanBuyUnits(kind, amount, cell).Ok) return cell;
            return null;
        }

        /// <summary>
        /// The lower-left cell of the place nearest the warehouse's west side, a free lane of
        /// <see cref="LaneCells"/> between them, where a building of the kind fits now; null without a warehouse or room.
        /// </summary>
        public static Cell? BuildingCell(GameSession session, BuildingKind kind)
        {
            if (session == null) return null;
            var warehouse = Warehouse(session.CurrentSnapshot);
            var definition = session.Catalog.GetBuilding(kind);
            if (warehouse == null || definition == null) return null;
            // anchors around the one level with the warehouse to its west, nearest first
            var start = new Cell(warehouse.Cell.X - LaneCells - definition.Width,
                warehouse.Cell.Y + (warehouse.Height - definition.Height) / 2);
            foreach (var cell in Rings(start, Reach(session)))
                if (session.CanPlaceBuilding(kind, cell).Ok) return cell;
            return null;
        }

        /// <summary>
        /// The idle creature nearest <paramref name="near"/> that <paramref name="counts"/> accepts (any kind without
        /// it), or null. An idle creature stands outside and takes an order at once.
        /// </summary>
        public static UnitSnapshot NearestIdle(GameSnapshot snapshot, Func<UnitKind, bool> counts, WorldPosition near)
        {
            if (snapshot == null) return null;
            UnitSnapshot best = null;
            float bestDistance = float.MaxValue;
            foreach (var unit in snapshot.Units)
            {
                if (unit.Assignment.Kind != AssignmentKind.Idle) continue;
                if (counts != null && !counts(unit.UnitKind)) continue;
                float dx = unit.Position.X - near.X, dy = unit.Position.Y - near.Y;
                float distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                best = unit;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>The middle of a building's footprint in map units.</summary>
        public static WorldPosition CenterOf(BuildingSnapshot building, GameContentCatalog catalog)
        {
            float cs = catalog.Economy.CellSize;
            return new WorldPosition((building.Cell.X + building.Width * .5f) * cs, (building.Cell.Y + building.Height * .5f) * cs);
        }

        /// <summary>The middle of a cell in map units.</summary>
        public static WorldPosition CenterOf(Cell cell, GameContentCatalog catalog)
        {
            float cs = catalog.Economy.CellSize;
            return new WorldPosition((cell.X + .5f) * cs, (cell.Y + .5f) * cs);
        }

        // the cell in front of the warehouse door, where its haulers come and go
        private static Cell? DoorCell(GameSession session)
        {
            var warehouse = Warehouse(session.CurrentSnapshot);
            if (warehouse == null) return null;
            var doorway = ColonyNavigation.DoorwayAt(warehouse.Kind, warehouse.Cell, session.Catalog);
            float cs = session.Catalog.Economy.CellSize;
            return new Cell((int)Math.Floor(doorway.Approach.X / cs), (int)Math.Floor(doorway.Approach.Y / cs));
        }

        private static int Reach(GameSession session) =>
            Math.Max(session.Catalog.Economy.GridWidth, session.Catalog.Economy.GridHeight);

        // square rings out from the centre; within a ring the nearest cells first, then by row and column
        private static IEnumerable<Cell> Rings(Cell centre, int reach)
        {
            var ring = new List<Cell>();
            for (int r = 0; r <= reach; r++)
            {
                ring.Clear();
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r)
                        ring.Add(new Cell(centre.X + dx, centre.Y + dy));
                ring.Sort((a, b) =>
                {
                    int da = Square(a.X - centre.X) + Square(a.Y - centre.Y);
                    int db = Square(b.X - centre.X) + Square(b.Y - centre.Y);
                    if (da != db) return da.CompareTo(db);
                    return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
                });
                foreach (var cell in ring) yield return cell;
            }
        }

        private static int Square(int value) => value * value;
    }
}
