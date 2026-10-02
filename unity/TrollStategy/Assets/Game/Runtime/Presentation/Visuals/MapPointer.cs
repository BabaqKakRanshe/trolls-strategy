using UnityEngine;
using UnityEngine.InputSystem;

namespace TrollStrategy.Presentation.Visuals
{
    /// <summary>
    /// Fingers on a touchscreen, read once a frame for everything that clicks the map or the battle board. A gesture
    /// runs from the first finger down to the last one up. A finger that moves past the drag threshold, or a second
    /// finger, makes the gesture the camera's (<c>IslandCameraRig</c> pans and pinches); one finger that lifts without
    /// either, and did not start over the HUD, is a tap: the touch form of a left click.
    /// Browsers may follow a touch with mouse events of their own, so mouse input right after a touch does not count
    /// as the mouse.
    /// </summary>
    public static class MapPointer
    {
        private const float MouseQuietSeconds = .5f;

        private static int s_frame = -1;
        private static int s_fingers;
        private static bool s_gesture, s_moved, s_overUi, s_tapped;
        private static Vector2 s_start, s_finger;
        private static float s_gestureStart;
        private static float s_lastTouch = float.NegativeInfinity, s_lastMouse = float.NegativeInfinity;

        /// <summary>A finger must move this far to stop being a tap, px (larger than the mouse's: fingers shake).</summary>
        public static float TouchDragThresholdPixels => Mathf.Max(12f, Screen.dpi * .06f);

        /// <summary>The player points with a finger rather than the mouse (the last of the two used).</summary>
        public static bool UsesTouch
        {
            get
            {
                Refresh();
                return s_gesture || s_lastTouch > s_lastMouse;
            }
        }

        /// <summary>Fingers down now.</summary>
        public static int Fingers
        {
            get
            {
                Refresh();
                return s_fingers;
            }
        }

        /// <summary>The finger gesture is the camera's: it moved or took a second finger.</summary>
        public static bool Moved
        {
            get
            {
                Refresh();
                return s_moved;
            }
        }

        /// <summary>The finger gesture started over the HUD, so the map leaves it alone.</summary>
        public static bool StartedOverUi
        {
            get
            {
                Refresh();
                return s_overUi;
            }
        }

        /// <summary>Where the map is pointed at: the finger (down or last lifted) for touch, the mouse otherwise.</summary>
        public static Vector2 Position
        {
            get
            {
                if (UsesTouch || Mouse.current == null) return s_finger;
                return Mouse.current.position.ReadValue();
            }
        }

        /// <summary>How long the finger has stayed down without moving, s; 0 without such a gesture.</summary>
        public static float HeldSeconds
        {
            get
            {
                Refresh();
                return s_gesture && !s_moved ? Time.unscaledTime - s_gestureStart : 0f;
            }
        }

        /// <summary>The gesture did its job (a long press): lifting the finger is not a tap any more.</summary>
        public static void ConsumeGesture()
        {
            Refresh();
            if (s_gesture) s_moved = true;
        }

        /// <summary>A tap ended this frame at <paramref name="at"/>.</summary>
        public static bool Tapped(out Vector2 at)
        {
            Refresh();
            at = s_finger;
            return s_tapped;
        }

        private static void Refresh()
        {
            if (s_frame == Time.frameCount) return;
            s_frame = Time.frameCount;
            s_tapped = false;
            float now = Time.unscaledTime;

            int fingers = 0;
            Vector2 first = default;
            var screen = Touchscreen.current;
            if (screen != null)
            {
                var touches = screen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (!touches[i].press.isPressed) continue;
                    if (fingers == 0) first = touches[i].position.ReadValue();
                    fingers++;
                }
            }
            s_fingers = fingers;

            var mouse = Mouse.current;
            if (mouse != null && now - s_lastTouch > MouseQuietSeconds &&
                (mouse.delta.ReadValue() != Vector2.zero || mouse.leftButton.isPressed ||
                 mouse.rightButton.isPressed || mouse.middleButton.isPressed))
                s_lastMouse = now;

            if (fingers > 0)
            {
                s_lastTouch = now;
                s_finger = first;
                if (!s_gesture)
                {
                    s_gesture = true;
                    s_moved = false;
                    s_start = first;
                    s_gestureStart = now;
                    s_overUi = UIInputUtils.IsOverDocument(first);
                }
                if (fingers > 1 || Vector2.Distance(s_start, first) >= TouchDragThresholdPixels) s_moved = true;
                return;
            }
            if (!s_gesture) return;
            s_gesture = false;
            s_lastTouch = now;
            s_tapped = !s_moved && !s_overUi;
        }
    }
}
