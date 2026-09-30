using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// The island look that depends on how far the camera is: linear fog and Gaussian depth of field grow with the
    /// distance from the camera to the lawn in the middle of the view, by the factors of the kit previews
    /// (Layout/isle_layout.json: fog.startPerDistance, post.dofStartPerDistance and so on).
    /// <para>Lives on the scene's global post-processing volume (ColonyVolume). The volume only works while the colony
    /// camera is enabled: the battle camera copies the colony camera's volume mask, and the island's grading, fog
    /// distances and blur must not reach the arena.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]      // after IslandCameraRig has placed the camera for the frame
    public sealed class IslandAtmosphere : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Volume _volume;
        [Tooltip("The island root: its height is the lawn level.")]
        [SerializeField] private Transform _ground;
        [SerializeField] private float _fogStartPerDistance = .85f;
        [SerializeField] private float _fogEndPerDistance = 3f;
        [SerializeField] private float _dofStartPerDistance = 1.4f;
        [SerializeField] private float _dofEndPerDistance = 2f;

        private float _applied = -1f;
        private VolumeProfile _profile;

        public Camera Camera => _camera;
        public Volume Volume => _volume;

        public void Configure(Camera camera, Volume volume, Transform ground, float fogStartPerDistance,
            float fogEndPerDistance, float dofStartPerDistance, float dofEndPerDistance)
        {
            _camera = camera;
            _volume = volume;
            _ground = ground;
            _fogStartPerDistance = fogStartPerDistance;
            _fogEndPerDistance = fogEndPerDistance;
            _dofStartPerDistance = dofStartPerDistance;
            _dofEndPerDistance = dofEndPerDistance;
            _applied = -1f;
        }

        /// <summary>Distance from the camera to the lawn plane along the middle of the view (camera height if it
        /// looks above the horizon).</summary>
        public static float ViewDistance(Camera camera, float groundY)
        {
            var view = camera.transform;
            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (plane.Raycast(new Ray(view.position, view.forward), out float distance) && distance > 0f) return distance;
            return Mathf.Max(1f, view.position.y - groundY);
        }

        /// <summary>Fog distances (RenderSettings) and the depth of field band (<paramref name="profile"/>) for a
        /// camera <paramref name="distance"/> metres from the land it looks at.</summary>
        public void Apply(float distance, VolumeProfile profile) =>
            Apply(distance, profile, _fogStartPerDistance, _fogEndPerDistance, _dofStartPerDistance, _dofEndPerDistance);

        public static void Apply(float distance, VolumeProfile profile, float fogStartPerDistance, float fogEndPerDistance,
            float dofStartPerDistance, float dofEndPerDistance)
        {
            RenderSettings.fogStartDistance = distance * fogStartPerDistance;
            RenderSettings.fogEndDistance = distance * fogEndPerDistance;
            if (profile != null && profile.TryGet(out DepthOfField dof))
            {
                dof.gaussianStart.Override(distance * dofStartPerDistance);
                dof.gaussianEnd.Override(distance * dofEndPerDistance);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            bool colony = _camera.isActiveAndEnabled;
            if (_volume != null && _volume.enabled != colony) _volume.enabled = colony;
            if (!colony) return;
            float groundY = _ground != null ? _ground.position.y : 0f;
            float distance = ViewDistance(_camera, groundY);
            if (_applied > 0f && Mathf.Abs(distance - _applied) < _applied * .005f) return;
            _applied = distance;
            // Volume.profile is this volume's own copy: the saved profile asset stays as the builder made it
            if (_profile == null && _volume != null && _volume.sharedProfile != null) _profile = _volume.profile;
            Apply(distance, _profile);
        }
    }
}
