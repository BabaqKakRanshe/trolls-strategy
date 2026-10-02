using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// The colony camera over the island. It starts where the camera stands in the scene: the point of the lawn it
    /// looks at becomes the target, and its angle, field of view and distance stay as set. Panning moves that point,
    /// zooming moves the camera along its view; distance = framed side x distancePerSide x sqrt 2. A camera that
    /// does not look at the ground gets the framing of the kit previews over the docked land: from the south at
    /// 45 degrees with a small offset to the east, from target + (offsetX, d, -d), d = side x distancePerSide
    /// (1.16 frames a square of that side in 16:9 with a margin).
    /// <list type="bullet">
    /// <item>Pan: middle or right mouse drag (the ground follows the pointer; a right press that does not move is
    /// still a click for <see cref="MapInputHandler"/>), WASD and arrow keys, the pointer at the screen edge, one
    /// or two fingers dragging (a finger that does not move is a tap, see <see cref="MapPointer"/>).</item>
    /// <item>Zoom: mouse wheel, pinch; from <c>minSide</c> metres of land to the whole island.</item>
    /// <item>Panning keeps the target over the island grid; <see cref="Frame"/> slides over to a bought block and
    /// <see cref="FrameOwned"/> shows the docked land.</item>
    /// </list>
    /// Input stops over the HUD (UIInputUtils) and while the camera is disabled (battles).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private IslandView _island;
        [Tooltip("Field of view of the kit framing, used only when the scene camera does not look at the ground.")]
        [SerializeField] private float _fieldOfView = 45f;
        [SerializeField] private float _offsetX = 1.5f;
        [SerializeField] private float _distancePerSide = 1.16f;
        [Tooltip("Closest zoom: side of the land square that fills the frame, m.")]
        [SerializeField, Min(2f)] private float _minSide = 10f;
        [Tooltip("Farthest zoom: the whole island times this.")]
        [SerializeField, Min(.2f)] private float _maxSideShare = 1.15f;
        [SerializeField, Min(0f)] private float _smoothTime = .15f;
        [SerializeField] private bool _readInput = true;
        [Tooltip("Arrow keys and screen edge: share of the framed side per second.")]
        [SerializeField] private float _keyPanSpeed = .9f;
        [Tooltip("The pointer this close to the screen edge pans the view, px; 0 turns it off.")]
        [SerializeField, Min(0f)] private float _edgeBand = 12f;
        [Tooltip("Zoom per mouse wheel notch.")]
        [SerializeField, Range(.02f, .5f)] private float _wheelStep = .12f;

        private Vector3 _target, _goal, _velocity;
        // from the target towards the camera; the camera keeps its own rotation, which looks back along it
        private Vector3 _viewDirection;
        private bool _pointerSeen;
        private float _side, _goalSide, _sideVelocity;
        private bool _ready;
        private bool _dragging;
        private bool _rightHeld;
        private Vector2 _rightPress;
        private Vector3 _dragAnchor;
        private bool _fingerHeld;
        private Vector3 _fingerAnchor;
        private bool _pinching;
        private float _pinchDistance;
        private Vector3 _pinchAnchor;
        private bool _intro;
        private float _introTime, _introSeconds;
        private Vector3 _introFrom, _introTo;
        private float _introFromSide, _introToSide;

        public Camera Camera => _camera;
        public IslandView Island => _island;
        /// <summary>The point of the lawn the camera looks at (world).</summary>
        public Vector3 Target => _target;
        /// <summary>Side of the land square the camera frames, m.</summary>
        public float Side => _side;
        public bool ReadInput { get => _readInput; set => _readInput = value; }
        /// <summary>The first-launch flight is on; input waits until it lands or is skipped.</summary>
        public bool IsPlayingIntro => _intro;

        /// <summary>
        /// The first-launch flight: from high above the whole island down to the view the scene set, over
        /// <paramref name="seconds"/>. Any key, click or touch lands it at once.
        /// </summary>
        public void PlayIntro(float seconds = 4f)
        {
            if (_camera == null || _island == null) return;
            if (!_ready) Begin();
            _introTo = _goal;
            _introToSide = _goalSide;
            _introFrom = ClampTarget(_island.transform.position);
            _introFromSide = ClampSide(float.MaxValue);
            _target = _introFrom;
            _side = _introFromSide;
            _velocity = Vector3.zero;
            _sideVelocity = 0f;
            _introTime = 0f;
            _introSeconds = Mathf.Max(.1f, seconds);
            _intro = true;
            Place();
        }

        public void SkipIntro()
        {
            if (!_intro) return;
            _intro = false;
            _target = _goal = _introTo;
            _side = _goalSide = _introToSide;
            _velocity = Vector3.zero;
            _sideVelocity = 0f;
            Place();
        }

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
            if (!_ready) Begin();
            _goal = ClampTarget(target);
            _goalSide = ClampSide(side);
            if (!instant) return;
            _target = _goal;
            _side = _goalSide;
            _velocity = Vector3.zero;
            _sideVelocity = 0f;
            Place();
        }

        // takes over the camera as the scene left it; the kit framing over the docked land if it misses the ground
        private void Begin()
        {
            var view = _camera.transform;
            if (GroundPoint(new Ray(view.position, view.forward), out var point))
            {
                _viewDirection = (view.position - point).normalized;
                _target = _goal = point;
                _side = _goalSide = Vector3.Distance(view.position, point) / Distance(1f);
            }
            else
            {
                var bounds = _island.OwnedBounds();
                _target = _goal = ClampTarget(_island.transform.TransformPoint(bounds.center));
                _side = _goalSide = ClampSide(Mathf.Max(bounds.size.x, bounds.size.z));
                Place(_camera, _target, _side, _fieldOfView, _offsetX, _distancePerSide);
                _viewDirection = (view.position - _target).normalized;
            }
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
            if (!_ready) Begin();
            if (!_camera.isActiveAndEnabled) return;
            if (_intro)
            {
                TickIntro(Time.unscaledDeltaTime);
                return;
            }
            if (_readInput) ReadPointer();
            float dt = Time.unscaledDeltaTime;
            _target = Vector3.SmoothDamp(_target, _goal, ref _velocity, _smoothTime, Mathf.Infinity, dt);
            _side = Mathf.SmoothDamp(_side, _goalSide, ref _sideVelocity, _smoothTime, Mathf.Infinity, dt);
            Place();
        }

        private void Place() => _camera.transform.position = _target + _viewDirection * Distance(_side);

        private void TickIntro(float dt)
        {
            bool skip = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                        (UnityEngine.InputSystem.Touchscreen.current != null &&
                         UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
            _introTime += dt;
            float t = Mathf.Clamp01(_introTime / _introSeconds);
            if (skip || t >= 1f)
            {
                SkipIntro();
                return;
            }
            // eases in and out: a slow start over the island, a soft landing over the colony
            float e = t * t * (3f - 2f * t);
            _target = Vector3.Lerp(_introFrom, _introTo, e);
            _side = Mathf.Lerp(_introFromSide, _introToSide, e);
            Place();
        }

        private float Distance(float side) => side * _distancePerSide * 1.41421356f;

        private void ReadPointer()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                if (mouse.middleButton.wasPressedThisFrame && !UIInputUtils.IsOverDocument(pointer) &&
                    GroundPoint(pointer, out _dragAnchor))
                    _dragging = true;
                // the right button grabs the ground only once it moves past a click; until then it is a click
                if (mouse.rightButton.wasPressedThisFrame && !_dragging && !UIInputUtils.IsOverDocument(pointer) &&
                    GroundPoint(pointer, out _dragAnchor))
                {
                    _rightHeld = true;
                    _rightPress = pointer;
                }
                if (_rightHeld && !mouse.rightButton.isPressed) _rightHeld = false;
                if (_rightHeld && !_dragging && UIInputUtils.IsDrag(_rightPress, pointer)) _dragging = true;
                if (_dragging && !mouse.middleButton.isPressed && !_rightHeld) _dragging = false;
                if (_dragging && GroundPoint(pointer, out var under)) DragTo(_dragAnchor, under);

                // the pointer reads (0, 0) until it first moves, which is a corner; it also keeps its last place
                // after leaving the Game view for another editor window
                if (mouse.delta.ReadValue() != Vector2.zero) _pointerSeen = true;
                var edge = _dragging || !_pointerSeen || MapPointer.UsesTouch || !PointerInGame()
                    ? Vector2.zero
                    : EdgeDirection(pointer, new Vector2(Screen.width, Screen.height), _edgeBand);
                if (edge != Vector2.zero)
                    PanWorld(new Vector3(edge.x, 0f, edge.y) * (_goalSide * _keyPanSpeed * Time.unscaledDeltaTime));

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
                float x = (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1f : 0f) -
                          (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1f : 0f);
                float z = (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1f : 0f) -
                          (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1f : 0f);
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
            // one finger that moved drags the ground; it takes a fresh hold whenever it is left the only finger
            if (down == 1 && MapPointer.Moved && !MapPointer.StartedOverUi)
            {
                _pinching = false;
                if (!_fingerHeld) _fingerHeld = GroundPoint(a, out _fingerAnchor);
                else if (GroundPoint(a, out var held)) DragTo(_fingerAnchor, held);
                return;
            }
            _fingerHeld = false;
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

        /// <summary>Which way the pointer at the edge of a <paramref name="screen"/> pans (x right, y up; -1, 0 or 1
        /// each). The pointer outside the screen, such as over another editor window, pans nowhere.</summary>
        public static Vector2 EdgeDirection(Vector2 pointer, Vector2 screen, float band)
        {
            if (band <= 0f || pointer.x < 0f || pointer.y < 0f || pointer.x > screen.x || pointer.y > screen.y)
                return Vector2.zero;
            float x = pointer.x >= screen.x - band ? 1f : pointer.x <= band ? -1f : 0f;
            float y = pointer.y >= screen.y - band ? 1f : pointer.y <= band ? -1f : 0f;
            return new Vector2(x, y);
        }

        private static bool PointerInGame()
        {
            if (!UnityEngine.Application.isFocused) return false;
#if UNITY_EDITOR
            var over = UnityEditor.EditorWindow.mouseOverWindow;
            return over != null && over.GetType().Name == "GameView";
#else
            return true;
#endif
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

        private bool GroundPoint(Vector2 screen, out Vector3 point) =>
            GroundPoint(_camera.ScreenPointToRay(screen), out point);

        private bool GroundPoint(Ray ray, out Vector3 point)
        {
            point = default;
            var plane = new Plane(Vector3.up, _island.transform.position);
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
