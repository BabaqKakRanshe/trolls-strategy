using System.Collections.Generic;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Units;
using UnityEngine;

namespace TrollStrategy.Presentation.Visuals
{
    /// <summary>
    /// Where things of the colony show on the screen, in the camera's pixels from the top left (as UI Toolkit counts):
    /// a point of the map, a creature's view (it walks between snapshots), a building's model. The tutorial pointer's
    /// window and hand go around them; the bootstrap hands these to the HUD as delegates, so the HUD never reaches into
    /// the scene. Null when the thing has no view or lies behind the camera.
    /// </summary>
    public sealed class ScreenLocator
    {
        private static readonly List<Renderer> s_renderers = new();

        private readonly Camera _camera;
        private readonly TilemapWorldView _world;
        private readonly BuildingVisualsManager _buildings;
        private readonly UnitVisualsManager _units;

        public ScreenLocator(Camera camera, TilemapWorldView world, BuildingVisualsManager buildings, UnitVisualsManager units)
        {
            _camera = camera;
            _world = world;
            _buildings = buildings;
            _units = units;
        }

        /// <summary>A point of the colony map (map units: cells) on the screen.</summary>
        public Vector2? MapToScreen(WorldPosition position)
        {
            if (_camera == null || _world == null) return null;
            var point = _camera.WorldToScreenPoint(_world.MapToWorld(new Vector3(position.X, position.Y, 0f)));
            return point.z > 0f ? new Vector2(point.x, _camera.pixelHeight - point.y) : (Vector2?)null;
        }

        /// <summary>The screen rectangle around a creature's sprite.</summary>
        public Rect? UnitToScreen(string unitId)
        {
            if (_units == null || string.IsNullOrEmpty(unitId) || !_units.Views.TryGetValue(unitId, out var view) || view == null)
                return null;
            return BoundsOnScreen(view.gameObject);
        }

        /// <summary>The screen rectangle around a building's model.</summary>
        public Rect? BuildingToScreen(string buildingId)
        {
            if (_buildings == null || string.IsNullOrEmpty(buildingId) ||
                !_buildings.Views.TryGetValue(buildingId, out var view) || view == null)
                return null;
            return BoundsOnScreen(view.gameObject);
        }

        private Rect? BoundsOnScreen(GameObject owner)
        {
            if (_camera == null || owner == null || !owner.activeInHierarchy) return null;
            owner.GetComponentsInChildren(false, s_renderers);
            bool any = false;
            var bounds = new Bounds();
            foreach (var renderer in s_renderers)
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }
            s_renderers.Clear();
            if (!any) return null;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var point = _camera.WorldToScreenPoint(corner);
                if (point.z <= 0f) return null;
                var flipped = new Vector2(point.x, _camera.pixelHeight - point.y);
                min = Vector2.Min(min, flipped);
                max = Vector2.Max(max, flipped);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
