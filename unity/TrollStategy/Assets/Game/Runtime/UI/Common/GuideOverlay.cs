using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The tutorial pointer drawn over a HUD: a light veil with a soft window around the next press, a white hand
    /// pointing at it and a white hint card with the step's words ("Шаг 2 из 3"). A target off the screen gets a
    /// round button at the edge instead, which brings the camera to it. While the player picks where to carry, an
    /// arrow runs from the source to the pointer. Nothing here catches the pointer except that edge button: presses
    /// go through to the game, and a press outside the window lifts the veil of that step (the hand and the card
    /// stay). The layer is a document of its own above the HUD's dialogs; colours come from the theme (Guide.uss).
    /// </summary>
    public sealed class GuideOverlay
    {
        // the window's soft edge (spot.png, 9-sliced at 48 px) clears 42 px in from its border
        private const float WindowPad = 50f;
        private const float WindowMin = 104f;
        // hand.png is drawn at 90x108; the fingertip sits here in it
        private const float HandWidth = 90f;
        private const float HandHeight = 108f;
        private const float TipX = 38.5f;
        private const float TipY = 5.5f;
        private const float HandTilt = 25f;
        // the card (.sheet) reaches past its visible edge by its baked shadow
        private const float ShadowSide = 28f;
        private const float ShadowAbove = 22f;
        private const float ShadowBelow = 36f;
        private const float Gap = 14f;
        private const float EdgeInset = 56f;
        private const float EdgeSize = 64f;

        private static readonly CustomStyleProperty<Color> RouteColorProperty = new("--route-color");
        private static readonly CustomStyleProperty<Color> RouteHaloProperty = new("--route-halo");

        private readonly VisualElement _root;
        private readonly VisualElement[] _veil = new VisualElement[4];
        private readonly VisualElement _window;
        private readonly VisualElement _hand;
        private readonly VisualElement _card;
        private readonly Label _step;
        private readonly Label _title;
        private readonly Label _text;
        private readonly Button _edge;
        private readonly VisualElement _edgeGlyph;
        private readonly VisualElement _route;
        private GuideStep _shown = GuideStep.None;
        private string _liftedKey;
        private Rect? _windowRect;
        private Vector2? _routeFrom;
        private Vector2? _routeTo;
        private Color _routeColor = new(.184f, .357f, .682f, 1f);
        private Color _routeHalo = Color.white;
        private float _time;

        public GuideOverlay(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _root.pickingMode = PickingMode.Ignore;
            _root.AddToClassList("guide");

            _route = Box("guide-route");
            _route.generateVisualContent += DrawRoute;
            _route.RegisterCallback<CustomStyleResolvedEvent>(OnRouteStyle);
            _root.Add(_route);
            for (int i = 0; i < _veil.Length; i++)
            {
                _veil[i] = Box("guide-veil");
                _root.Add(_veil[i]);
            }
            _window = Box("guide-window");
            _root.Add(_window);
            _hand = Box("guide-hand");
            _root.Add(_hand);

            _card = Box("sheet guide-card");
            _step = Ui.Text(string.Empty, "guide-card__step t-bold");
            _title = Ui.Text(string.Empty, "guide-card__title t-bold");
            _text = Ui.Text(string.Empty, "guide-card__text");
            foreach (var label in new VisualElement[] { _step, _title, _text }) label.pickingMode = PickingMode.Ignore;
            _card.Add(_step);
            _card.Add(_title);
            _card.Add(_text);
            _root.Add(_card);

            // the one thing that takes a press: the way to a target off the screen
            _edge = UiFeel.Bind(Ui.TextButton(string.Empty, "btn btn-disc guide-edge"),
                () => FocusRequested?.Invoke(_shown.Target));
            _edgeGlyph = Box("glyph glyph--chevron guide-edge__glyph");
            _edge.Add(_edgeGlyph);
            _root.Add(_edge);
            HideAll();
        }

        /// <summary>The edge button was pressed: the camera should go to the step's target.</summary>
        public event Action<GuideTarget> FocusRequested;

        public GuideStep Step => _shown;
        public bool IsShowing => _shown.IsShown;
        /// <summary>Whether the veil covers the screen now (shown, not lifted, placed).</summary>
        public bool VeilShown => Ui.IsShown(_window);
        public bool HandShown => Ui.IsShown(_hand);
        public bool EdgeShown => Ui.IsShown(_edge);
        public Button EdgeButton => _edge;
        public string CardStep => _step.text;
        public string CardTitle => _title.text;
        public string CardText => _text.text;
        /// <summary>The window around the target in the layer's coordinates, once it is placed.</summary>
        public Rect? Window => _windowRect;

        /// <summary>Shows a step (or none); the same step again keeps a lifted veil lifted.</summary>
        public void Show(GuideStep step)
        {
            step ??= GuideStep.None;
            bool changed = step.Key != _shown.Key;
            _shown = step;
            if (changed) _liftedKey = null;
            if (!step.IsShown)
            {
                HideAll();
                return;
            }
            Ui.Show(_card, true);
            Ui.SetText(_step, step.Count > 1 ? $"Шаг {step.Number} из {step.Count}" : string.Empty);
            Ui.Show(_step, step.Count > 1);
            Ui.SetText(_title, step.Title ?? string.Empty);
            Ui.SetText(_text, step.Text ?? string.Empty);
            Ui.Show(_text, !string.IsNullOrEmpty(step.Text));
        }

        /// <summary>A press somewhere on the screen (layer coordinates): outside the window it lifts this step's veil.</summary>
        public void PointerPressed(Vector2 point)
        {
            if (!_shown.IsShown || _windowRect == null || _windowRect.Value.Contains(point)) return;
            _liftedKey = _shown.Key;
        }

        /// <summary>
        /// The arrow from where the goods come from to the pointer while the player picks where they go, both in the
        /// layer's coordinates; nulls hide it.
        /// </summary>
        public void SetRoute(Vector2? from, Vector2? to)
        {
            if (from == _routeFrom && to == _routeTo) return;
            _routeFrom = from;
            _routeTo = to;
            Ui.Show(_route, from != null && to != null);
            _route.MarkDirtyRepaint();
        }

        /// <summary>
        /// Every frame: places the window, the hand and the card around the target. <paramref name="locate"/> gives a
        /// target in the world as a rectangle in the layer's coordinates, or null when it cannot be seen.
        /// </summary>
        public void Tick(float deltaTime, Func<GuideTarget, Rect?> locate)
        {
            _time += deltaTime;
            if (!_shown.IsShown)
            {
                HideAll();
                return;
            }
            var size = new Vector2(_root.layout.width, _root.layout.height);
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x <= 0f || size.y <= 0f) return;
            var target = Locate(_shown.Target, locate);
            if (target == null)
            {
                // nowhere to point: a hidden button, a target the camera cannot show; the card waits alone
                HidePointer();
                PlaceCard(null, size);
                return;
            }
            var rect = target.Value;
            var screen = new Rect(Vector2.zero, size);
            if (_shown.Target.InWorld && !screen.Overlaps(rect))
            {
                PlaceEdge(rect.center, size);
                return;
            }
            Ui.Show(_edge, false);
            var window = Inflate(rect, WindowPad, WindowMin);
            _windowRect = window;
            bool veil = _shown.Veil && _liftedKey != _shown.Key;
            PlaceVeil(window, size, veil);
            PlaceHand(rect, size);
            PlaceCard(window, size);
        }

        private Rect? Locate(GuideTarget target, Func<GuideTarget, Rect?> locate)
        {
            if (target.Kind == GuideTargetKind.Element)
            {
                var element = target.Element;
                if (element == null || !IsVisible(element)) return null;
                var bound = element.worldBound;
                if (float.IsNaN(bound.width) || bound.width <= 0f || bound.height <= 0f) return null;
                var local = _root.WorldToLocal(bound);
                return local;
            }
            return locate?.Invoke(target);
        }

        private static bool IsVisible(VisualElement element)
        {
            if (element.panel == null) return false;
            for (var e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.style.display == DisplayStyle.None) return false;
                if (e.resolvedStyle.visibility == Visibility.Hidden) return false;
            }
            return true;
        }

        private static Rect Inflate(Rect rect, float pad, float min)
        {
            float width = Mathf.Max(rect.width + 2f * pad, min);
            float height = Mathf.Max(rect.height + 2f * pad, min);
            return new Rect(rect.center.x - width / 2f, rect.center.y - height / 2f, width, height);
        }

        private void PlaceVeil(Rect window, Vector2 size, bool shown)
        {
            Ui.Show(_window, shown);
            foreach (var strip in _veil) Ui.Show(strip, shown);
            if (!shown) return;
            float x0 = Mathf.Round(window.xMin), y0 = Mathf.Round(window.yMin);
            float x1 = Mathf.Round(window.xMax), y1 = Mathf.Round(window.yMax);
            SetRect(_window, x0, y0, x1 - x0, y1 - y0);
            SetRect(_veil[0], 0f, 0f, size.x, Mathf.Max(0f, y0));                       // above
            SetRect(_veil[1], 0f, y1, size.x, Mathf.Max(0f, size.y - y1));              // below
            SetRect(_veil[2], 0f, y0, Mathf.Max(0f, x0), y1 - y0);                      // left
            SetRect(_veil[3], x1, y0, Mathf.Max(0f, size.x - x1), y1 - y0);             // right
        }

        // the fingertip below the middle of the target, the hand leaning away from the nearer side of the screen;
        // it taps: comes to the target and draws back a little
        private void PlaceHand(Rect target, Vector2 size)
        {
            Ui.Show(_hand, true);
            float tilt = target.center.x > size.x * .66f ? HandTilt : -HandTilt;
            // a wide button (a card's action, a dialog's confirm) is touched near its end, so its words stay readable
            bool wide = target.width > target.height * 2.2f;
            float x = wide
                ? (tilt < 0f ? target.xMax - target.height * .55f : target.xMin + target.height * .55f)
                : target.center.x + Mathf.Sign(-tilt) * target.width * .12f;
            var tip = new Vector2(x, target.center.y + Mathf.Min(target.height * .22f, 26f));
            // too near the bottom for the hand to rise from below: it hangs from above and points down at the target
            if (tip.y + HandHeight * .9f > size.y)
            {
                tilt = 180f - tilt;
                tip = new Vector2(x, target.center.y - Mathf.Min(target.height * .22f, 26f));
            }
            float radians = tilt * Mathf.Deg2Rad;
            // the hand points up its own picture; turned, "up" leans with it
            var pointing = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            float tap = (1f - Mathf.Cos(_time * Mathf.PI * 2f / 1.1f)) * 3.5f;
            var offset = -pointing * (8f - tap);
            _hand.style.left = tip.x - TipX + offset.x;
            _hand.style.top = tip.y - TipY + offset.y;
            _hand.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
        }

        private void PlaceEdge(Vector2 towards, Vector2 size)
        {
            HideVeil();
            Ui.Show(_hand, false);
            Ui.Show(_edge, true);
            var centre = size / 2f;
            var direction = towards - centre;
            if (direction.sqrMagnitude < 1f) direction = Vector2.up;
            // where the line to the target leaves the screen, kept clear of the edge
            float sx = (size.x / 2f - EdgeInset) / Mathf.Max(Mathf.Abs(direction.x), .001f);
            float sy = (size.y / 2f - EdgeInset) / Mathf.Max(Mathf.Abs(direction.y), .001f);
            var point = centre + direction * Mathf.Min(sx, sy);
            SetRect(_edge, point.x - EdgeSize / 2f, point.y - EdgeSize / 2f, EdgeSize, EdgeSize);
            // the chevron points up in its picture
            float angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
            _edgeGlyph.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            PlaceCard(new Rect(point.x - EdgeSize / 2f, point.y - EdgeSize / 2f, EdgeSize, EdgeSize), size);
        }

        // above the window when there is room, else below it, else beside it; always on the screen
        private void PlaceCard(Rect? window, Vector2 size)
        {
            Ui.Show(_card, true);
            float width = _card.layout.width, height = _card.layout.height;
            if (float.IsNaN(width) || width <= 0f)
            {
                _card.style.visibility = Visibility.Hidden;
                return;
            }
            _card.style.visibility = Visibility.Visible;
            float visibleWidth = width - 2f * ShadowSide;
            float visibleHeight = height - ShadowAbove - ShadowBelow;
            float x, y;
            if (window == null)
            {
                x = (size.x - visibleWidth) / 2f;
                y = size.y * .28f;
            }
            else
            {
                var w = window.Value;
                x = w.center.x - visibleWidth / 2f;
                // a dialog's button: the card goes below it, off the dialog's content; elsewhere above the target
                bool below = !_shown.Veil;
                bool roomAbove = w.yMin - Gap - visibleHeight >= Gap;
                bool roomBelow = w.yMax + Gap + visibleHeight <= size.y - Gap;
                if (below && roomBelow) y = w.yMax + Gap;
                else if (roomAbove) y = w.yMin - Gap - visibleHeight;
                else if (roomBelow) y = w.yMax + Gap;
                else
                {
                    y = w.center.y - visibleHeight / 2f;
                    x = w.center.x > size.x / 2f ? w.xMin - Gap - visibleWidth : w.xMax + Gap;
                }
            }
            x = Mathf.Clamp(x, Gap, Mathf.Max(Gap, size.x - visibleWidth - Gap));
            y = Mathf.Clamp(y, Gap, Mathf.Max(Gap, size.y - visibleHeight - Gap));
            // the element's box starts at the shadow's edge
            _card.style.left = Mathf.Round(x - ShadowSide);
            _card.style.top = Mathf.Round(y - ShadowAbove);
        }

        private void HideAll()
        {
            HidePointer();
            Ui.Show(_card, false);
        }

        private void HidePointer()
        {
            HideVeil();
            Ui.Show(_hand, false);
            Ui.Show(_edge, false);
        }

        private void HideVeil()
        {
            _windowRect = null;
            Ui.Show(_window, false);
            foreach (var strip in _veil) Ui.Show(strip, false);
        }

        private static void SetRect(VisualElement element, float x, float y, float width, float height)
        {
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
        }

        private static VisualElement Box(string classes)
        {
            var box = Ui.Box(classes);
            box.pickingMode = PickingMode.Ignore;
            return box;
        }

        private void OnRouteStyle(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(RouteColorProperty, out var color)) _routeColor = color;
            if (evt.customStyle.TryGetValue(RouteHaloProperty, out var halo)) _routeHalo = halo;
            _route.MarkDirtyRepaint();
        }

        // a bent dashed arrow on a light halo, from the source up and over to the pointer
        private void DrawRoute(MeshGenerationContext context)
        {
            if (_routeFrom == null || _routeTo == null) return;
            var from = _route.WorldToLocal(_root.LocalToWorld(_routeFrom.Value));
            var to = _route.WorldToLocal(_root.LocalToWorld(_routeTo.Value));
            float length = Vector2.Distance(from, to);
            if (length < 24f) return;
            var lift = new Vector2(0f, -Mathf.Min(length * .35f, 140f));
            var control = (from + to) / 2f + lift;
            var painter = context.painter2D;

            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.strokeColor = new Color(_routeHalo.r, _routeHalo.g, _routeHalo.b, .9f);
            painter.lineWidth = 11f;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.QuadraticCurveTo(control, to);
            painter.Stroke();

            painter.strokeColor = _routeColor;
            painter.lineWidth = 5f;
            const int segments = 40;
            for (int i = 0; i < segments; i += 2)
            {
                painter.BeginPath();
                painter.MoveTo(Bezier(from, control, to, i / (float)segments));
                painter.LineTo(Bezier(from, control, to, (i + 1) / (float)segments));
                painter.Stroke();
            }

            var tangent = (to - control).normalized;
            var normal = new Vector2(-tangent.y, tangent.x);
            var tip = to;
            var back = tip - tangent * 22f;
            painter.fillColor = _routeColor;
            painter.strokeColor = new Color(_routeHalo.r, _routeHalo.g, _routeHalo.b, .9f);
            painter.lineWidth = 3f;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(back + normal * 11f);
            painter.LineTo(back - normal * 11f);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
