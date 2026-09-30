using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// The island's air, drawn by <see cref="IslandHazeFeature"/> before URP's post-processing (so depth of field,
    /// bloom and grading treat it like the rest of the frame):
    /// <list type="bullet">
    /// <item>height fog: what lies below the lawn fades into the sky colour by its world height — the rock pillars,
    /// the clouds under empty slots, the satellites' roots — the same at every zoom;</item>
    /// <item>edge haze: the frame turns milky towards its corners, most at the bottom, a light vignette.</item>
    /// </list>
    /// Off unless a volume turns it on. The colony volume does, and it is disabled with the colony camera
    /// (<see cref="IslandAtmosphere"/>), so the battle arena never gets it.
    /// </summary>
    [Serializable, VolumeComponentMenu("TrollStrategy/Island Haze")]
    public sealed class IslandHaze : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("The colour the land fades into below the lawn: the sky (camera background) colour.")]
        public ColorParameter fogColor = new ColorParameter(new Color(.71f, .82f, .93f), false, false, true);

        [Tooltip("World height where the fog starts.")]
        public FloatParameter fogStart = new FloatParameter(0f);

        [Tooltip("World height where the fog reaches its opacity; below the start.")]
        public FloatParameter fogFull = new FloatParameter(-15f);

        [Tooltip("How much of the colour stays at the full height and below.")]
        public ClampedFloatParameter fogOpacity = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("The colour of the edge haze.")]
        public ColorParameter edgeColor = new ColorParameter(Color.white, false, false, true);

        [Tooltip("How far the edge haze covers the corners.")]
        public ClampedFloatParameter edgeIntensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Where the edge haze starts and where it is full: 0 is the middle of the frame, 1 its corners.")]
        public FloatRangeParameter edgeRange = new FloatRangeParameter(new Vector2(.45f, 1.05f), 0f, 1.5f);

        [Tooltip("How much of the edge haze is left at the top of the frame (1: as much as at the bottom).")]
        public ClampedFloatParameter edgeTop = new ClampedFloatParameter(1f, 0f, 1f);

        public bool IsActive() => fogOpacity.value > 0f || edgeIntensity.value > 0f;
    }
}
