using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Presentation.Buildings
{
    /// <summary>
    /// Reads the building prefabs placed in the scene as the colony's starting layout.
    /// The scene only authors kind and cell; the session validates and owns the buildings.
    /// </summary>
    public static class SceneBuildingPlacements
    {
        public readonly struct Placement
        {
            public Placement(BuildingView view, StartingBuilding building)
            {
                View = view;
                Building = building;
            }

            public BuildingView View { get; }
            public StartingBuilding Building { get; }
        }

        /// <summary>Active scene BuildingViews in a stable order: kind, then row, then column.</summary>
        public static List<Placement> Collect(TilemapWorldView worldView, GameContentCatalog catalog)
        {
            if (worldView == null) throw new ArgumentNullException(nameof(worldView));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var placements = new List<Placement>();
            foreach (var view in Object.FindObjectsByType<BuildingView>(FindObjectsInactive.Exclude))
                placements.Add(new Placement(view, new StartingBuilding(view.Kind, CellOf(view, worldView, catalog))));

            placements.Sort((a, b) =>
            {
                int byKind = a.Building.Kind.CompareTo(b.Building.Kind);
                if (byKind != 0) return byKind;
                int byRow = a.Building.Cell.Y.CompareTo(b.Building.Cell.Y);
                return byRow != 0 ? byRow : a.Building.Cell.X.CompareTo(b.Building.Cell.X);
            });
            return placements;
        }

        /// <summary>South-west footprint cell nearest to where the building stands; the view snaps to it.</summary>
        public static Cell CellOf(BuildingView view, TilemapWorldView worldView, GameContentCatalog catalog)
        {
            var definition = catalog.GetBuilding(view.Kind);
            var center = worldView.WorldToMap(view.transform.position);
            float cellSize = worldView.CellSize;
            return new Cell(
                Mathf.RoundToInt(center.x / cellSize - definition.Width * 0.5f),
                Mathf.RoundToInt(center.y / cellSize - definition.Height * 0.5f));
        }
    }
}
