using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// The colony camera over the island. It keeps the framing of the kit previews: it looks at a point of the lawn
    /// from the south at 45 degrees with a small offset to the east, from target + (offsetX, d, -d), where
    /// d = framed side x distancePerSide (1.16 frames a square of that side in 16:9 with a margin).
    /// <list type="bullet">
    /// <item>Pan: middle mouse drag (the ground follows the pointer), arrow keys, two-finger drag.</item>
    /// <item>Zoom: mouse wheel, pinch; from <c>minSide</c> metres of land to the whole island.</item>
    /// <item>The target stays over the island grid; <see cref="FrameOwned"/> shows the docked land (after a purchase,
    /// at the start of a game).</item>
    /// </list>
    /// Input stops over the HUD (UIInputUtils) and while the camera is disabled (battles).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private IslandView _island;
        [SerializeField] private float _fieldOfView = 45f;
        [SerializeField] private float _offsetX = 1.5f;
        [SerializeField] private float _distancePerSide = 1.16f;
        [Tooltip("Closest zoom: side of the land square that fills the frame, m.")]
        [SerializeField, Min(2f)] private float _minSide = 10f;
        [Tooltip("Farthest zoom: the whole island times this.")]
        [SerializeField, Min(.2f)] private float _maxSideShare = 1.15f;
        [SerializeField, Min(0f)] private float _smoothTime = .15f;
        [SerializeField] private bool _readInput = true;
        [Tooltip("Arrow keys: share of the framed side per second.")]
        [SerializeField] private float _keyPanSpeed = .9f;
        [Tooltip("Zoom per mouse wheel notch.")]
        [SerializeField, Range(.02f, .5f)] private float _wheelStep = .12f;

        private Vector3 _target, _goal, _velocity;
        private float _side, _goalSide, _sideVelocity;
        private bool _ready;
        private bool _dragging;
        private Vector3 _dragAnchor;
        private bool _pinching;
        private float _pinchDistance;
        private Vector3 _pinchAnchor;

        public Camera Camera => _camera;
        public IslandView Island => _island;
        /// <summary>The point of the lawn the camera looks at (world).</summary>
        public Vector3 Target => _target;
        /// <summary>Side of the land square the camera frames, m.</summary>
        public float Side => _side;
        public bool ReadInput { get => _readInput; set => _readInput = value; }

        public void Configure(Camera camera, IslandView island, float fieldOfView, float offsetX, float distancePerSide)
        {
            _camera = camera;
            _island = island;
            _fieldOfView = fieldOfView;
            _offsetX = offsetX;
            _distancePerSide = distancePerSide;
            _ready = false;
        }

        /// <summary>Camera position that frames a <paramref name="side"/> m square around <paramref name="target"/>.</summary>
        public static Vector3 PositionFor(Vector3 target, float side, float offsetX, float distancePerSide)
        {
            float d = side * distancePerSide;
            return target + new Vector3(offsetX, d, -d);
        }

        /// <summary>Puts <paramref name="camera"/> where the rig would for this frame (edit-time framing).</summary>
        public static void Place(Camera camera, Vector3 target, float side, float fieldOfView, float offsetX,
            float distancePerSide)
        {
            camera.fieldOfView = fieldOfView;
            camera.transform.position = PositionFor(target, side, offsetX, distancePerSide);
            camera.transform.LookAt(target, Vector3.up);
        }

        /// <summary>Frames the docked land of the island (the start zone before anything is bought).</summary>
        public void FrameOwned(bool instant = false)
        {
            if (_island == null) return;
            var bounds = _island.OwnedBounds();
            Frame(_island.transform.TransformPoint(bounds.center), Mathf.Max(bounds.size.x, bounds.size.z), instant);
        }

        /// <summary>Frames a <paramref name="side"/> m square of land around <paramref name="target"/> (world).</summary>
        public void Frame(Vector3 target, float side, bool instant = false)
        {
            _goal = ClampTarget(target);
            _goalSide = ClampSide(side);
            if (!instant && _ready) return;
            _target = _goal;
            _side = _goalSide;
            _velocity = Vector3.zero;
            _sideVelocity = 0f;
            _ready = true;
            Place();
        }

        public void PanWorld(Vector3 delta) => _goal = ClampTarget(_goal + new Vector3(delta.x, 0f, delta.z));

        /// <summary>factor &lt; 1 zooms in, &gt; 1 zooms out.</summary>
        public void Zoom(float factor) => _goalSide = ClampSide(_goalSide * Mathf.Max(.01f, factor));

        private void LateUpdate()
        {
            if (_camera == null || _island == null) return;
            if (!_ready) FrameOwned(true);
            if (!_camera.isActiveAndEnabled) return;
            if (_readInput) ReadPointer();
            float dt = Time.unscaledDeltaTime;
            _target = Vector3.SmoothDamp(_target, _goal, ref _velocity, _smoothTime, Mathf.Infinity, dt);
            _side = Mathf.SmoothDamp(_side, _goalSide, ref _sideVelocity, _smoothTime, Mathf.Infinity, dt);
            Place();
        }

        private void Place() => Place(_camera, _target, _side, _fieldOfView, _offsetX, _distancePerSide);

        private void ReadPointer()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                if (mouse.middleButton.wasPressedThisFrame && !UIInputUtils.IsOverDocument(pointer) &&
                    GroundPoint(pointer, out _dragAnchor))
                    _dragging = true;
                if (_dragging && !mouse.middleButton.isPressed) _dragging = false;
                if (_dragging && GroundPoint(pointer, out var under)) DragTo(_dragAnchor, under);

                // a wheel notch is 120 on some platforms and 1 on others; trackpads send small steps every frame
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > .01f && !UIInputUtils.IsOverDocument(pointer))
                {
                    float notches = Mathf.Clamp(Mathf.Abs(wheel) >= 20f ? wheel / 120f : wheel, -3f, 3f);
                    Zoom(Mathf.Pow(1f - _wheelStep, notches));
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                float z = (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || z != 0f)
                    PanWorld(new Vector3(x, 0f, z) * (_goalSide * _keyPanSpeed * Time.unscaledDeltaTime));
            }

            var touch = Touchscreen.current;
            if (touch == null) return;
            var touches = touch.touches;
            int down = 0;
            Vector2 a = default, b = default;
            for (int i = 0; i < touches.Count && down < 2; i++)
            {
                if (!touches[i].press.isPressed) continue;
                if (down == 0) a = touches[i].position.ReadValue();
                else b = touches[i].position.ReadValue();
                down++;
            }
            if (down < 2)
            {
                _pinching = false;
                return;
            }
            var middle = (a + b) * .5f;
            float spread = Vector2.Distance(a, b);
            if (!_pinching)
            {
                if (UIInputUtils.IsOverDocument(middle) || !GroundPoint(middle, out _pinchAnchor)) return;
                _pinching = true;
                _pinchDistance = Mathf.Max(1f, spread);
                return;
            }
            if (spread > 1f)
            {
                Zoom(_pinchDistance / spread);
                _pinchDistance = spread;
            }
            if (GroundPoint(middle, out var point)) DragTo(_pinchAnchor, point);
        }

        // moves the view so that the ground point grabbed at `anchor` comes back under the pointer (now over `under`);
        // both were cast from the camera where it stands now, so the shift applies to the current target
        private void DragTo(Vector3 anchor, Vector3 under)
        {
            _goal = ClampTarget(_target + (anchor - under));
            _target = _goal;
            _velocity = Vector3.zero;
            Place();
        }

        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            point = default;
            var plane = new Plane(Vector3.up, _island.transform.position);
            var ray = _camera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance);
            return true;
        }

        private float Half => _island != null ? _island.BlocksPerSide * _island.BlockSize * .5f : 20f;

        private Vector3 ClampTarget(Vector3 target)
        {
            if (_island == null) return target;
            var origin = _island.transform.position;
            float half = Half;
            return new Vector3(Mathf.Clamp(target.x, origin.x - half, origin.x + half), origin.y,
                Mathf.Clamp(target.z, origin.z - half, origin.z + half));
        }

        private float ClampSide(float side) =>
            Mathf.Clamp(side, _minSide, Mathf.Max(_minSide, 2f * Half * _maxSideShare));
    }
}
