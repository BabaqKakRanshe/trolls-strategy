using System;
using System.Collections;
using System.Collections.Generic;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.Bots
{
    /// <summary>A step the hands could not do: what they looked for is not on the screen or does not answer.</summary>
    public sealed class GestureFailed : Exception
    {
        public GestureFailed(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Runs nested iterator steps one frame at a time: a step yields null to wait for the next frame, or another
    /// iterator to run it first. The show bot drives it from its own frame loop, so no MonoBehaviour coroutine is needed.
    /// </summary>
    internal sealed class Routine
    {
        private readonly Stack<IEnumerator> _stack = new();

        public Routine(IEnumerator root) => _stack.Push(root);

        public bool Done => _stack.Count == 0;
        public Exception Error { get; private set; }

        public void Step()
        {
            while (_stack.Count > 0)
            {
                var top = _stack.Peek();
                bool more;
                try
                {
                    more = top.MoveNext();
                }
                catch (Exception exception)
                {
                    Error = exception;
                    _stack.Clear();
                    return;
                }
                if (!more)
                {
                    _stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    _stack.Push(nested);
                    continue;
                }
                return;
            }
        }
    }

    /// <summary>
    /// The show bot's hand: moves its mouse like a person (a quick, slightly curved, easing stroke, a short hover before
    /// the click), clicks in separate frames for press and release, presses keys and pans the colony camera with the
    /// right button when a target lies off the screen or under the HUD. Every target is a function read each frame, so a
    /// walking creature is followed. Screen points are pixels from the bottom left, as Input System counts.
    /// </summary>
    internal sealed class BotHand
    {
        private const float EdgeMargin = 40f;
        private readonly BotMouse _mouse;
        private readonly BotOverlay _overlay;
        private readonly System.Random _random;

        public BotHand(BotMouse mouse, BotOverlay overlay, int seed)
        {
            _mouse = mouse;
            _overlay = overlay;
            _random = new System.Random(seed);
        }

        /// <summary>Seconds of video one frame lasts; the driver sets it every frame.</summary>
        public float FrameSeconds { get; set; } = 1f / 30f;
        /// <summary>How fast the hand moves: 1 is an unhurried player, 2 twice as quick.</summary>
        public float Speed { get; set; } = 1f;
        /// <summary>The colony camera, for panning; null in the battle.</summary>
        public Func<IslandCameraRig> Rig { get; set; }

        /// <summary>
        /// Something that pops up over the HUD in the middle of a move (a quest's reward window) and must be dealt
        /// with first: a step for it, or null. Waits stand still while it runs.
        /// </summary>
        public Func<IEnumerator> Interrupt { get; set; }

        private bool _interrupting;
        private int _interruptions;

        /// <summary>Runs a step that deals with a pop-up itself: no interruption may start inside it.</summary>
        public IEnumerator Exclusive(IEnumerator step)
        {
            bool was = _interrupting;
            _interrupting = true;
            yield return step;
            _interrupting = was;
        }

        /// <summary>A new move begins: an interruption cut short by a failed move is over.</summary>
        public void ResetInterrupts() => _interrupting = false;

        /// <summary>Deals with a pop-up now, if one is up; a step that checks the screen itself calls it first.</summary>
        public IEnumerator Checkpoint() => Interruptions();

        private IEnumerator Interruptions()
        {
            if (_interrupting || Interrupt == null) yield break;
            var step = Interrupt();
            if (step == null) yield break;
            _interrupting = true;
            _interruptions++;
            yield return step;
            _interrupting = false;
        }

        public Vector2 Position => _mouse.Position;

        // ---------- targets

        /// <summary>The middle of a HUD element on the screen; null while it is hidden, off its panel or covered.</summary>
        public static Vector2? Of(VisualElement element)
        {
            if (element == null || element.panel == null) return null;
            for (var e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden ||
                    e.resolvedStyle.opacity < 0.05f)
                    return null;
            }
            var bound = element.worldBound;
            if (float.IsNaN(bound.width) || bound.width < 2f || bound.height < 2f) return null;
            var center = bound.center;
            var picked = element.panel.Pick(center);
            if (picked == null || (picked != element && !element.Contains(picked))) return null;
            return PanelToScreen(element.panel, center);
        }

        /// <summary>A panel point (top-left origin, panel pixels) on the screen, from the bottom left.</summary>
        public static Vector2 PanelToScreen(IPanel panel, Vector2 point)
        {
            // the panel's scale, read off its own screen-to-panel mapping
            var origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            var far = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(1000f, 1000f));
            var scale = new Vector2((far.x - origin.x) / 1000f, (far.y - origin.y) / 1000f);
            var topLeft = new Vector2((point.x - origin.x) / scale.x, (point.y - origin.y) / scale.y);
            return new Vector2(topLeft.x, Screen.height - topLeft.y);
        }

        /// <summary>A world point on the screen through the camera; null behind it.</summary>
        public static Vector2? World(Camera camera, Vector3 world)
        {
            if (camera == null) return null;
            var point = camera.WorldToScreenPoint(world);
            return point.z > 0f ? new Vector2(point.x, point.y) : (Vector2?)null;
        }

        /// <summary>The point is on the screen, clear of the edges, and no HUD panel covers it.</summary>
        public static bool Clear(Vector2 point) =>
            point.x >= EdgeMargin && point.x <= Screen.width - EdgeMargin &&
            point.y >= EdgeMargin && point.y <= Screen.height - EdgeMargin &&
            !UIInputUtils.IsOverDocument(point);

        // ---------- gestures

        /// <summary>
        /// Waits this long both in video and in real time: the HUD's own motions (a band sliding in, a reward's reveal)
        /// run on the real clock, whatever pace the editor draws the video at.
        /// </summary>
        public IEnumerator Wait(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            for (float t = 0f; t < seconds || Time.realtimeSinceStartup - start < seconds; t += FrameSeconds) yield return null;
        }

        /// <summary>Waits until <paramref name="condition"/> holds; fails once the timeout has passed in video and real time.</summary>
        public IEnumerator Until(Func<bool> condition, float timeout, string what)
        {
            float start = Time.realtimeSinceStartup;
            for (float t = 0f; ; t += FrameSeconds)
            {
                int seen = _interruptions;
                yield return Interruptions();
                if (seen != _interruptions)
                {
                    t = 0f;
                    start = Time.realtimeSinceStartup;
                }
                if (condition()) yield break;
                if (t > timeout && Time.realtimeSinceStartup - start > timeout) throw new GestureFailed($"не дождался: {what}");
                yield return null;
            }
        }

        /// <summary>Moves the pointer onto the target, following it while it moves.</summary>
        public IEnumerator MoveTo(Func<Vector2?> target, string what)
        {
            yield return Interruptions();
            Vector2? end = null;
            float since = Time.realtimeSinceStartup;
            for (float t = 0f; (end = target()) == null; t += FrameSeconds)
            {
                int seen = _interruptions;
                yield return Interruptions();
                if (seen != _interruptions)
                {
                    t = 0f;
                    since = Time.realtimeSinceStartup;
                    continue;
                }
                if (t > 3f && Time.realtimeSinceStartup - since > 3f) throw new GestureFailed($"не вижу на экране: {what}");
                yield return null;
            }
            var start = _mouse.Position;
            float distance = Vector2.Distance(start, end.Value);
            if (distance < 2f) yield break;
            float duration = Mathf.Clamp((0.16f + distance / 1700f) / Mathf.Max(0.25f, Speed), 0.08f, 1.1f);
            // a hand's stroke bows a little to one side
            var normal = new Vector2(-(end.Value - start).y, (end.Value - start).x).normalized;
            float bow = ((float)_random.NextDouble() - 0.5f) * 0.25f * distance;
            for (float t = FrameSeconds; t < duration; t += FrameSeconds)
            {
                var goal = target() ?? end.Value;
                end = goal;
                float s = Ease(t / duration);
                var control = (start + goal) * 0.5f + normal * bow;
                var point = (1 - s) * (1 - s) * start + 2 * (1 - s) * s * control + s * s * goal;
                Put(point);
                yield return null;
            }
            Put(target() ?? end.Value);
            yield return null;
        }

        /// <summary>Moves onto the target, hovers a moment and clicks it.</summary>
        public IEnumerator Click(Func<Vector2?> target, string what, bool right = false, bool ctrl = false)
        {
            yield return MoveTo(target, what);
            // the hover: tooltips and highlights answer before the press
            yield return Wait(0.12f / Mathf.Max(0.25f, Speed));
            yield return Interruptions();
            // a panel still sliding in carries its button away: follow it until the pointer rests on it
            for (int settle = 0; settle < 5; settle++)
            {
                var now = target();
                if (now == null) throw new GestureFailed($"не вижу на экране: {what}");
                if (Vector2.Distance(now.Value, _mouse.Position) <= 3f) break;
                yield return MoveTo(target, what);
                yield return Wait(0.08f);
            }
            yield return null;
            if (ctrl)
            {
                _mouse.Ctrl(true);
                yield return null;
            }
            Follow(target);
            if (right) _mouse.Right(true);
            else _mouse.Left(true);
            _overlay.Click(right);
            yield return null;
            Follow(target);
            yield return null;
            Follow(target);
            if (right) _mouse.Right(false);
            else _mouse.Left(false);
            yield return null;
            if (ctrl)
            {
                _mouse.Ctrl(false);
                yield return null;
            }
            yield return Wait(0.1f / Mathf.Max(0.25f, Speed));
        }

        /// <summary>Clicks a HUD button, after waiting for it to show up and be available.</summary>
        public IEnumerator Press(Button button, string what, float timeout = 4f)
        {
            if (button == null) throw new GestureFailed($"нет кнопки: {what}");
            yield return Until(() => Of(button) != null, timeout, $"кнопка «{what}» на экране");
            if (!UiFeel.IsAvailable(button)) yield return Until(() => UiFeel.IsAvailable(button), timeout, $"кнопка «{what}» доступна");
            yield return Click(() => Of(button), what);
        }

        /// <summary>Turns the wheel over a point (a list that scrolls).</summary>
        public IEnumerator Scroll(Func<Vector2?> over, float notches, string what)
        {
            yield return MoveTo(over, what);
            _mouse.Wheel(notches);
            yield return null;
            yield return Wait(0.18f / Mathf.Max(0.25f, Speed));
        }

        /// <summary>Presses and lets go of a key; the key cap shows by the cursor.</summary>
        public IEnumerator Key(Key key, string label)
        {
            _overlay.Key(label);
            yield return Wait(0.15f / Mathf.Max(0.25f, Speed));
            _mouse.KeyDown(key);
            yield return null;
            yield return null;
            _mouse.KeyUp(key);
            yield return null;
            yield return Wait(0.12f / Mathf.Max(0.25f, Speed));
        }

        /// <summary>
        /// Clicks a point of the colony map: first brings it into view, clear of the HUD, by dragging the ground with
        /// the right button, as a player does.
        /// </summary>
        public IEnumerator ClickWorld(Func<Vector2?> target, string what, bool ctrl = false)
        {
            yield return Reveal(target, what);
            yield return Click(() =>
            {
                var point = target();
                return point != null && Clear(point.Value) ? point : null;
            }, what, ctrl: ctrl);
        }

        /// <summary>Pans the colony camera until the target is on the screen and clear of the HUD.</summary>
        public IEnumerator Reveal(Func<Vector2?> target, string what)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var point = target();
                if (point != null && Clear(point.Value)) yield break;
                var rig = Rig?.Invoke();
                if (rig == null) break;
                var middle = new Vector2(Screen.width * 0.42f, Screen.height * 0.55f);
                if (point == null)
                {
                    // behind the camera or lost: the hand cannot drag to it; frame it as the focus keys would
                    break;
                }
                var grab = FreeGround(middle);
                if (grab == null) break;
                var shift = middle - point.Value;
                float reach = Mathf.Min(Screen.width, Screen.height) * 0.38f;
                if (shift.magnitude > reach) shift = shift.normalized * reach;
                // a short drag would be a right click, and a right click calls the order off
                if (shift.magnitude < 40f) shift = (shift.sqrMagnitude > 1f ? shift.normalized : Vector2.up) * 40f;
                var release = grab.Value + shift;
                release.x = Mathf.Clamp(release.x, EdgeMargin, Screen.width - EdgeMargin);
                release.y = Mathf.Clamp(release.y, EdgeMargin, Screen.height - EdgeMargin);
                yield return DragGround(grab.Value, release);
            }
            var last = target();
            if (last != null && Clear(last.Value)) yield break;
            var fallback = Rig?.Invoke();
            if (fallback == null) throw new GestureFailed($"не вижу на карте: {what}");
            throw new GestureFailed($"не смог подвести камеру к цели: {what}");
        }

        /// <summary>Drags the ground with the right button from one screen point to another: the camera follows.</summary>
        public IEnumerator DragGround(Vector2 from, Vector2 to)
        {
            yield return MoveTo(() => from, "свободное место на карте");
            _mouse.Right(true);
            yield return null;
            yield return null;
            float duration = Mathf.Clamp(0.25f + Vector2.Distance(from, to) / 1400f, 0.25f, 0.8f) / Mathf.Max(0.25f, Speed);
            for (float t = FrameSeconds; t < duration; t += FrameSeconds)
            {
                Put(Vector2.Lerp(from, to, Ease(t / duration)));
                yield return null;
            }
            Put(to);
            yield return null;
            _mouse.Right(false);
            // the camera eases after the ground
            yield return Wait(0.45f);
        }

        /// <summary>
        /// Looks around the island as a new player does: drags the ground there and back and turns the wheel out and
        /// in. The first tutorial cards wait for exactly that.
        /// </summary>
        public IEnumerator LookAround()
        {
            var middle = FreeGround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            if (middle == null) yield break;
            var side = new Vector2(Screen.width * 0.22f, Screen.height * 0.06f);
            yield return DragGround(middle.Value, middle.Value + side);
            yield return DragGround(middle.Value + side, middle.Value - side);
            yield return DragGround(middle.Value - side, middle.Value);
            // the lesson counts the wheel from its own card on, which comes a moment after the drag
            yield return Wait(0.8f);
            for (int i = 0; i < 3; i++) yield return Scroll(() => middle, -1f, "колесо");
            yield return Wait(0.4f);
            for (int i = 0; i < 3; i++) yield return Scroll(() => middle, 1f, "колесо");
            yield return Wait(0.6f);
        }

        /// <summary>A map point near <paramref name="near"/> that no HUD panel covers.</summary>
        private static Vector2? FreeGround(Vector2 near)
        {
            for (int ring = 0; ring < 8; ring++)
            for (int step = 0; step < 8; step++)
            {
                float angle = step * Mathf.PI / 4f;
                var point = near + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 60f;
                if (Clear(point)) return point;
            }
            return null;
        }

        private void Follow(Func<Vector2?> target)
        {
            var point = target();
            // a small follow keeps a walking creature under the pointer; a jump would make the press a drag
            if (point != null && Vector2.Distance(point.Value, _mouse.Position) < 5f) Put(point.Value);
        }

        private void Put(Vector2 point)
        {
            point.x = Mathf.Clamp(point.x, 14f, Screen.width - 14f);
            point.y = Mathf.Clamp(point.y, 14f, Screen.height - 14f);
            _mouse.MoveTo(point);
            _overlay.Pointer(point);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
    }
}
