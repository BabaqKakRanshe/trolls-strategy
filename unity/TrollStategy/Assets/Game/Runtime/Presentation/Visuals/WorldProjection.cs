using UnityEngine;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Visuals
{
    // Campaign coordinates remain in the Grid's local XY plane.
    public static class WorldProjection
    {
        public static bool TryGroundPoint(Camera camera, Vector2 screen, TilemapWorldView worldView, out Vector3 point)
        {
            point = default;
            if (camera == null) return false;
            var grid = worldView != null ? worldView.Grid : null;
            var ground = grid != null
                ? new Plane(grid.transform.forward, grid.transform.position)
                : new Plane(Vector3.back, Vector3.zero);
            var ray = camera.ScreenPointToRay(screen);
            if (!ground.Raycast(ray, out var distance)) return false;
            point = ray.GetPoint(distance);
            return true;
        }
    }
}
