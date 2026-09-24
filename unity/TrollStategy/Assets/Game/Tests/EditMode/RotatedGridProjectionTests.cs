using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class RotatedGridProjectionTests
    {
        [Test]
        public void RotatedAndTranslatedGrid_ProjectsCellsAndScreenPointsToTheSamePlane()
        {
            var gridObject = new GameObject("GridUnderTest");
            var cameraObject = new GameObject("CameraUnderTest");
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            var target = new RenderTexture(800, 600, 16);
            try
            {
                economy.Init(14, 14, 1f, 1000, 20, 0.25f, 3, 0.1f, 0.5f);
                var grid = gridObject.AddComponent<Grid>();
                grid.transform.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(90f, 0f, 0f));
                var worldView = gridObject.AddComponent<TilemapWorldView>();
                worldView.Init(grid, null, null, economy);

                var cell = new Cell(3, 5);
                var corner = worldView.CellToWorld(cell);
                var center = worldView.BuildingCenterWorld(cell, 2, 2);
                Assert.That(Vector3.Distance(corner, new Vector3(5f, 3f, 9f)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(center, new Vector3(6f, 3f, 10f)), Is.LessThan(0.001f));
                Assert.That(worldView.WorldToCell(center), Is.EqualTo(new Cell(4, 6)));

                var cellCenter = worldView.MapToWorld(new Vector3(3.5f, 5.5f, 0f));
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                camera.transform.SetPositionAndRotation(cellCenter + Vector3.up * 12f, Quaternion.Euler(90f, 0f, 0f));
                Assert.That(WorldProjection.TryGroundPoint(camera, new Vector2(400f, 300f), worldView, out var hit), Is.True);
                Assert.That(Vector3.Distance(hit, cellCenter), Is.LessThan(0.001f));
                Assert.That(worldView.WorldToCell(hit), Is.EqualTo(cell));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(gridObject);
                Object.DestroyImmediate(economy);
                Object.DestroyImmediate(target);
            }
        }
    }
}
