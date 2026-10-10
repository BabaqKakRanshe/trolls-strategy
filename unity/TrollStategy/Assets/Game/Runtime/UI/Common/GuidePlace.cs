using System;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.UI
{
    /// <summary>
    /// A place on the map the tutorial pointer marks (a building's place, the cell for a creature), as the camera sees
    /// it: its four corners, the light inside them and the pin that floats over its middle with the picture of what goes
    /// there (<see cref="GuideTarget.Art"/>). Such a place gets the pin instead of the veil's window and the hand.
    /// Shapes only, in the layer's coordinates; <see cref="GuideOverlay"/> paints them in the hand's look.
    /// </summary>
    public static class GuidePlace
    {
        /// <summary>The pin's disc over one cell, and over a building's place.</summary>
        public const float CellPinRadius = 28f;
        public const float PlacePinRadius = 40f;
        /// <summary>The tail's tip is this many radii below the disc's middle: its sides then touch the disc.</summary>
        public const float TipReach = 1.5f;
        /// <summary>The tip floats this high over the place's middle, and once a period rises this much higher.</summary>
        public const float Hover = 4f;
        public const float FloatHeight = 10f;
        public const float FloatSeconds = 2.2f;
        /// <summary>The light on the place keeps this share of a cell inside the grid's lines.</summary>
        public const float LightInset = .06f;

        /// <summary>Whether a target is a place on the ground, marked by the pin rather than by the veil's window.</summary>
        public static bool OnGround(GuideTarget target) =>
            target.Kind == GuideTargetKind.Footprint || target.Kind == GuideTargetKind.Cell;

        /// <summary>
        /// The four corners of a place of <paramref name="width"/> × <paramref name="height"/> cells, in order round it,
        /// as <paramref name="project"/> puts map positions on the screen; null when one cannot be shown.
        /// </summary>
        public static Vector2[] Corners(Cell cell, int width, int height, float cellSize, Func<WorldPosition, Vector2?> project)
        {
            if (project == null) return null;
            var corners = new Vector2[4];
            for (int i = 0; i < corners.Length; i++)
            {
                // the lower left cell's corner, along the width, across, back along the height
                int u = i == 1 || i == 2 ? width : 0, v = i >= 2 ? height : 0;
                var point = project(new WorldPosition((cell.X + u) * cellSize, (cell.Y + v) * cellSize));
                if (point == null) return null;
                corners[i] = point.Value;
            }
            return corners;
        }

        /// <summary>A point of the place: <paramref name="u"/> across its width, <paramref name="v"/> across its height, 0 to 1 inside.</summary>
        public static Vector2 At(Vector2[] corners, float u, float v) =>
            Vector2.LerpUnclamped(Vector2.LerpUnclamped(corners[0], corners[1], u),
                Vector2.LerpUnclamped(corners[3], corners[2], u), v);

        /// <summary>The place drawn in by <paramref name="cells"/> on every side, put into <paramref name="inside"/>.</summary>
        public static void Inset(Vector2[] corners, int width, int height, float cells, Vector2[] inside)
        {
            float u = cells / Mathf.Max(1, width), v = cells / Mathf.Max(1, height);
            inside[0] = At(corners, u, v);
            inside[1] = At(corners, 1f - u, v);
            inside[2] = At(corners, 1f - u, 1f - v);
            inside[3] = At(corners, u, 1f - v);
        }

        /// <summary>A cell's shorter side on the screen.</summary>
        public static float CellSide(Vector2[] corners, int width, int height) =>
            Mathf.Min(Vector2.Distance(corners[0], corners[1]) / Mathf.Max(1, width),
                Vector2.Distance(corners[1], corners[2]) / Mathf.Max(1, height));

        public static float PinRadius(int width, int height) => width > 1 || height > 1 ? PlacePinRadius : CellPinRadius;

        /// <summary>How high the pin floats at <paramref name="time"/> seconds, from 0 to 1: a slow rise and fall.</summary>
        public static float Float(float time) => (1f - Mathf.Cos(time * Mathf.PI * 2f / FloatSeconds)) * .5f;

        /// <summary>The middle of the pin's disc, its tip over the place's middle and <paramref name="up"/> of the way up.</summary>
        public static Vector2 PinCentre(Vector2[] corners, float radius, float up) =>
            At(corners, .5f, .5f) - new Vector2(0f, Hover + FloatHeight * up + radius * TipReach);

        /// <summary>The tip of the tail of a pin round <paramref name="centre"/>.</summary>
        public static Vector2 PinTip(Vector2 centre, float radius) => centre + new Vector2(0f, radius * TipReach);

        /// <summary>
        /// Where the tail's sides touch the disc, as angles in radians (a quarter turn points down the screen): the pin's
        /// outline runs from the one up round the top to the other. The same at any radius, so a pin grown or shrunk
        /// about its middle keeps its shape and its outline stays as thick along the tail as round the disc.
        /// </summary>
        public static (float From, float To) PinArc
        {
            get
            {
                float side = Mathf.Acos(1f / TipReach);
                return (Mathf.PI / 2f + side, Mathf.PI * 2.5f - side);
            }
        }
    }
}
