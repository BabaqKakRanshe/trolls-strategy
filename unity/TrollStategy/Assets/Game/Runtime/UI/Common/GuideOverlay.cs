using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The tutorial pointer drawn over a HUD: a light veil with a soft round window around the next press, a touch
    /// pressing it and a white hint card with the step's words ("Шаг 2 из 3"). A target off the screen gets a
    /// round button at the edge instead, which brings the camera to it. A place on the map (where to build, the cell
    /// for a hire) gets no veil and no hand: it lights up, and a pin floats over it with the picture of what goes
    /// there (GuidePlace). While the player picks
    /// where to carry, an arrow runs from the source to the pointer. Nothing here catches the pointer except that edge button: presses
    /// go through to the game, and a press outside the window lifts the veil of that step (the touch and the card
    /// stay). The layer is a document of its own above the HUD's dialogs; colours come from the theme (Guide.uss).
    /// </summary>
    public sealed class GuideOverlay
    {
        // spot.png is clear within this share of its half size and fades to the veil at its rim; the clear circle (or
        // ellipse, for a wide target) passes through the target's corners and this much beyond
        private const float SpotClear = .62f;
        private const float WindowPad = 12f;
        private const float ClearMin = 52f;
        private const float RoundAspect = 1.6f;
        // a place on the map: the light on it, the pin's shadow on the ground (a share of a cell across, fainter as
        // the pin rises), the pin's outline and how much of its disc the picture fills
        private const float PlaceLight = .6f;
        private const float LightRounding = .16f;
        private const float SpotCells = .26f;
        private const float SpotAlpha = .32f;
        private const int SpotSegments = 24;
        private const int PinSegments = 48;
        private const float PinOutline = 3f;
        private const float PinArtShare = 1.45f;
        // the touch: a dot in the touch's colour in a white rim presses the target once a cycle, coming in bigger with
        // a long soft shadow and pressed small with a short one, and a wave goes out from the press and fades
        private const float TouchSeconds = 1.4f;
        private const float TouchRadius = 15f;
        private const float TouchRim = 3f;
        private const float WaveFrom = 14f;
        private const float WaveTo = 75f;
        private const float WaveLine = 4f;
        private const float WaveHalo = 2f;
        // the cycle's moments (coming in, pressed, settled, coming in again) and the dot at each
        private static readonly float[] TouchMoments = { 0f, .28f, .45f, 1f };
        private static readonly float[] TouchScale = { 1.18f, .8f, 1f, 1.18f };
        private static readonly float[] TouchShadowAlpha = { .3f, .38f, .32f, .3f };
        private static readonly float[] TouchShadowDrop = { 8f, 2f, 4f, 8f };
        private static readonly float[] TouchShadowBlur = { 7f, 2f, 4f, 7f };
        private const float WaveFromMoment = .3f;
        // the card (.sheet) reaches past its visible edge by its baked shadow
        private const float ShadowSide = 28f;
        private const float ShadowAbove = 22f;
        private const float ShadowBelow = 36f;
        private const float Gap = 14f;
        private const float EdgeInset = 56f;
        private const float EdgeSize = 64f;

        private static readonly CustomStyleProperty<Color> MarkInkProperty = new("--mark-ink");
        private static readonly CustomStyleProperty<Color> MarkPaperProperty = new("--mark-paper");
        private static readonly CustomStyleProperty<Color> MarkTouchProperty = new("--mark-touch");
        // the haul's arrow in the tutorial touch's look: the head and the track's dots the touch's colour in a white
        // rim, the goods' tokens white in a ring of it, with the good's picture on top
        private const float HeadRim = 3f;
        private const float TrackRim = 1.4f;
        private const float TokenRing = .16f;
        private const float CargoPicture = 22f;
        // the marks' soft shadow: faint layers of each shape grown and shrunk around its edge, a little below,
        // spread as a blur would spread it
        private const float ShadowAlpha = .3f;
        private static readonly Vector2 ShadowOffset = new(0f, 3.5f);
        private static readonly float[] ShadowLayers = { -3.3f, -1.35f, 0f, 1.35f, 3.3f };

        private readonly VisualElement _root;
        private readonly VisualElement[] _veil = new VisualElement[4];
        private readonly VisualElement _window;
        private readonly VisualElement _touch;
        private readonly VisualElement _card;
        private readonly Label _step;
        private readonly Label _title;
        private readonly Label _text;
        private readonly VisualElement _keys;
        private readonly List<string> _keyLabels = new();
        private readonly Button _edge;
        private readonly VisualElement _edgeGlyph;
        private readonly VisualElement _route;
        private readonly Image _pinArt;
        private GuideStep _shown = GuideStep.None;
        private string _liftedKey;
        private Rect? _windowRect;
        private Rect? _clearRect;
        private Vector2? _routeFrom;
        private Vector2? _routeTo;
        // the place the pin floats over: its corners in the layer's coordinates and its size in cells
        private Vector2[] _place;
        private int _placeWidth = 1;
        private int _placeHeight = 1;
        private readonly Vector2[] _placeCorners = new Vector2[4];
        private readonly Vector2[] _placeLight = new Vector2[4];
        private Color _markInk = new(.118f, .165f, .251f, 1f);
        private Color _markPaper = Color.white;
        private Color _markTouch = new(1f, .478f, .102f, 1f);
        private readonly List<Vector2> _routeTrack = new();
        private readonly List<GuideRoute.Token> _routeTokens = new();
        private readonly Vector2[] _head = new Vector2[GuideRoute.HeadCorners];
        private readonly Vector2[] _grown = new Vector2[GuideRoute.HeadCorners];
        private readonly float[] _rounding = new float[GuideRoute.HeadCorners];
        private readonly List<Image> _cargoPictures = new();
        private IReadOnlyList<Sprite> _routeCargo = Array.Empty<Sprite>();
        private bool _routeTraced;
        private float _time;
        private Vector2 _touchAt;

        public GuideOverlay(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _root.pickingMode = PickingMode.Ignore;
            _root.AddToClassList("guide");

            _route = Box("guide-route");
            _route.generateVisualContent += DrawMarks;
            _route.RegisterCallback<CustomStyleResolvedEvent>(OnRouteStyle);
            _root.Add(_route);
            // the picture in the pin's disc; the pin itself is drawn on the marks' layer under it
            _pinArt = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _pinArt.AddToClassList("guide-pin__art");
            _root.Add(_pinArt);
            for (int i = 0; i < _veil.Length; i++)
            {
                _veil[i] = Box("guide-veil");
                _root.Add(_veil[i]);
            }
            _window = Box("guide-window");
            _root.Add(_window);
            // the touch over the veil, drawn in the marks' colours
            _touch = Box("guide-touch");
            _touch.generateVisualContent += DrawTouch;
            _root.Add(_touch);

            _card = Box("sheet guide-card");
            _step = Ui.Text(string.Empty, "guide-card__step t-bold");
            _title = Ui.Text(string.Empty, "guide-card__title t-bold");
            _text = Ui.Text(string.Empty, "guide-card__text");
            foreach (var label in new VisualElement[] { _step, _title, _text }) label.pickingMode = PickingMode.Ignore;
            _card.Add(_step);
            _card.Add(_title);
            _card.Add(_text);
            _keys = Box("guide-card__keys");
            _card.Add(_keys);
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
        public bool TouchShown => Ui.IsShown(_touch);
        /// <summary>Where the touch presses, in the layer's coordinates, while it shows.</summary>
        public Vector2? TouchPoint => TouchShown ? _touchAt : null;
        public bool EdgeShown => Ui.IsShown(_edge);
        /// <summary>Whether the pin floats over the step's place now.</summary>
        public bool PinShown => _place != null;
        public Button EdgeButton => _edge;
        /// <summary>The pictures of the goods the haul's arrow carries in turn; empty when no haul is being given.</summary>
        public IReadOnlyList<Sprite> RouteCargo => _routeCargo;
        public string CardStep => _step.text;
        public string CardTitle => _title.text;
        public string CardText => _text.text;
        /// <summary>The card's keys as shown: a keycap's letter or a picture's glyph name.</summary>
        public IReadOnlyList<string> CardKeys => _keyLabels;
        /// <summary>The window around the target in the layer's coordinates, once it is placed (its soft rim included).</summary>
        public Rect? Window => _windowRect;
        /// <summary>The clear round middle of the window: the box of its circle or ellipse.</summary>
        public Rect? Clear => _clearRect;

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
            if (changed) ShowKeys(step.Keys);
        }

        // keycaps and mouse pictures in one row; keys with names as a list of two columns
        private void ShowKeys(IReadOnlyList<GuideKey> keys)
        {
            _keys.Clear();
            _keyLabels.Clear();
            Ui.Show(_keys, keys.Count > 0);
            bool named = false;
            foreach (var key in keys) named |= !string.IsNullOrEmpty(key.Name);
            VisualElement row = null;
            for (int i = 0; i < keys.Count; i++)
            {
                if (row == null || (named && i % 2 == 0))
                {
                    row = Box("guide-keys__row");
                    _keys.Add(row);
                }
                var key = keys[i];
                var item = Box(named ? "guide-key guide-key--named" : "guide-key");
                if (key.Glyph != null) item.Add(Box("glyph glyph--" + key.Glyph + " guide-key__glyph"));
                else
                {
                    var cap = Ui.Text(key.Label, "keycap guide-key__cap");
                    cap.pickingMode = PickingMode.Ignore;
                    item.Add(cap);
                }
                if (!string.IsNullOrEmpty(key.Name))
                {
                    var name = Ui.Text(key.Name, "guide-key__name");
                    name.pickingMode = PickingMode.Ignore;
                    item.Add(name);
                }
                row.Add(item);
                _keyLabels.Add(key.ToString());
            }
        }

        /// <summary>A press somewhere on the screen (layer coordinates): outside the window it lifts this step's veil.</summary>
        public void PointerPressed(Vector2 point)
        {
            if (!_shown.IsShown || _clearRect == null || _clearRect.Value.Contains(point)) return;
            _liftedKey = _shown.Key;
        }

        /// <summary>
        /// The arrow from where the goods come from to the pointer while the player picks where they go, both in the
        /// layer's coordinates (nulls hide it), and the pictures of the goods its tokens carry in turn.
        /// </summary>
        public void SetRoute(Vector2? from, Vector2? to, IReadOnlyList<Sprite> cargo = null)
        {
            _routeCargo = cargo ?? Array.Empty<Sprite>();
            // while the arrow shows, its tokens ride on: it is traced and drawn again every frame
            if (from == _routeFrom && to == _routeTo && (from == null || to == null)) return;
            _routeFrom = from;
            _routeTo = to;
            _routeTraced = from != null && to != null &&
                           GuideRoute.Trace(OnRoute(from.Value), OnRoute(to.Value), _time, _routeTrack, _routeTokens, _head);
            if (!_routeTraced)
            {
                _routeTrack.Clear();
                _routeTokens.Clear();
            }
            PlaceCargo();
            ShowMarks();
        }

        // the arrow and the place share one layer under the veil
        private void ShowMarks()
        {
            Ui.Show(_route, (_routeFrom != null && _routeTo != null) || _place != null);
            _route.MarkDirtyRepaint();
        }

        private void SetPlace(Vector2[] place)
        {
            if (place == null) Ui.Show(_pinArt, false);
            if (place == null && _place == null) return;
            _place = place;
            ShowMarks();
        }

        /// <summary>
        /// Every frame: places the window, the touch and the card around the target. <paramref name="locate"/> gives a
        /// target in the world as a rectangle in the layer's coordinates, or null when it cannot be seen;
        /// <paramref name="ground"/> gives a place on the map as its four corners (<see cref="GuidePlace.Corners"/>), in
        /// the same coordinates.
        /// </summary>
        public void Tick(float deltaTime, Func<GuideTarget, Rect?> locate, Func<GuideTarget, Vector2[]> ground = null)
        {
            _time += deltaTime;
            if (!_shown.IsShown)
            {
                HideAll();
                return;
            }
            // the pin floats, and the camera may move: draw it again every frame
            if (_place != null) _route.MarkDirtyRepaint();
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
            var place = GuidePlace.OnGround(_shown.Target) ? ground?.Invoke(_shown.Target) : null;
            if (place != null)
            {
                // a place on the ground: the pin over it shows where and what; no veil over the island, no hand
                SetPlace(place);
                _placeWidth = Math.Max(1, _shown.Target.Width);
                _placeHeight = Math.Max(1, _shown.Target.Height);
                var box = Bounds(place);
                _clearRect = box;
                _windowRect = box;
                PlaceVeil(box, size, false);
                Ui.Show(_touch, false);
                PlaceCard(PlacePin(box), size);
                return;
            }
            SetPlace(null);
            var clear = RoundAround(rect);
            var window = Inflate(clear, 1f / SpotClear);
            _clearRect = clear;
            _windowRect = window;
            bool veil = _shown.Veil && _liftedKey != _shown.Key;
            PlaceVeil(window, size, veil);
            PlaceTouch(rect);
            PlaceCard(clear, size);
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

        // the box of the round clear middle: a circle through the corners of a squarish target, an ellipse through the
        // corners of a wide one (its axes the target's, times the square root of two), a little wider all round
        private static Rect RoundAround(Rect rect)
        {
            float a = rect.width / 2f, b = rect.height / 2f;
            if (Mathf.Max(a, b) <= RoundAspect * Mathf.Min(a, b))
            {
                a = b = Mathf.Sqrt(a * a + b * b);
            }
            else
            {
                a *= 1.41421356f;
                b *= 1.41421356f;
            }
            a = Mathf.Max(a + WindowPad, ClearMin);
            b = Mathf.Max(b + WindowPad, ClearMin);
            return new Rect(rect.center.x - a, rect.center.y - b, 2f * a, 2f * b);
        }

        private static Rect Bounds(Vector2[] points)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in points)
            {
                x0 = Mathf.Min(x0, p.x);
                y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x);
                y1 = Mathf.Max(y1, p.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        private static Rect Inflate(Rect rect, float factor)
        {
            float width = rect.width * factor, height = rect.height * factor;
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

        // the touch presses near the target's middle, a little down and right of it, so the picture on a round button
        // stays readable; a wide button (a card's action, a dialog's confirm) is pressed near its end, off its words
        private void PlaceTouch(Rect target)
        {
            Ui.Show(_touch, true);
            bool wide = target.width > target.height * 2.2f;
            _touchAt = wide
                ? new Vector2(target.xMax - target.height * .5f, target.center.y)
                : target.center + new Vector2(Mathf.Min(target.width * .13f, 16f), Mathf.Min(target.height * .15f, 18f));
            _touch.MarkDirtyRepaint();
        }

        private void PlaceEdge(Vector2 towards, Vector2 size)
        {
            HideVeil();
            Ui.Show(_touch, false);
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
                // a card alone (the controls lesson, a target out of sight) stands high, over the sky, off the island
                x = (size.x - visibleWidth) / 2f;
                y = size.y * .1f;
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
            Ui.Show(_touch, false);
            Ui.Show(_edge, false);
        }

        private void HideVeil()
        {
            SetPlace(null);
            _windowRect = null;
            _clearRect = null;
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

        // the touch: the wave first, under the dot, then the dot's shadow, then the dot in its white rim
        private void DrawTouch(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var at = _touch.WorldToLocal(_root.LocalToWorld(_touchAt));
            float phase = Mathf.Repeat(_time / TouchSeconds, 1f);
            if (phase >= WaveFromMoment)
            {
                float t = (phase - WaveFromMoment) / (1f - WaveFromMoment);
                // the wave's outer edge runs out fast and slows; the line sits inside it with its halo
                float radius = Mathf.Lerp(WaveFrom, WaveTo, 1f - (1f - t) * (1f - t)) - WaveLine / 2f - WaveHalo;
                painter.strokeColor = new Color(_markPaper.r, _markPaper.g, _markPaper.b, .95f * (1f - t));
                painter.lineWidth = WaveLine + 2f * WaveHalo;
                painter.BeginPath();
                Circle(painter, at, radius);
                painter.Stroke();
                painter.strokeColor = new Color(_markTouch.r, _markTouch.g, _markTouch.b, 1f - t);
                painter.lineWidth = WaveLine;
                painter.BeginPath();
                Circle(painter, at, radius);
                painter.Stroke();
            }
            float radiusNow = TouchRadius * AtMoment(TouchScale, phase);
            float blur = AtMoment(TouchShadowBlur, phase);
            var drop = new Vector2(0f, AtMoment(TouchShadowDrop, phase));
            painter.fillColor = new Color(_markInk.r, _markInk.g, _markInk.b, AtMoment(TouchShadowAlpha, phase) / ShadowLayers.Length);
            foreach (float grow in ShadowLayers)
            {
                painter.BeginPath();
                Circle(painter, at + drop, radiusNow + grow * blur / 3.3f);
                painter.Fill();
            }
            painter.fillColor = _markPaper;
            painter.BeginPath();
            Circle(painter, at, radiusNow);
            painter.Fill();
            painter.fillColor = _markTouch;
            painter.BeginPath();
            Circle(painter, at, radiusNow - TouchRim * radiusNow / TouchRadius);
            painter.Fill();
        }

        // a value of the touch's cycle between its moments, eased in and out
        private static float AtMoment(float[] values, float phase)
        {
            for (int i = 1; i < TouchMoments.Length; i++)
            {
                if (phase > TouchMoments[i]) continue;
                float k = Mathf.InverseLerp(TouchMoments[i - 1], TouchMoments[i], phase);
                return Mathf.Lerp(values[i - 1], values[i], Mathf.SmoothStep(0f, 1f, k));
            }
            return values[values.Length - 1];
        }

        private void OnRouteStyle(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(MarkInkProperty, out var ink)) _markInk = ink;
            if (evt.customStyle.TryGetValue(MarkPaperProperty, out var paper)) _markPaper = paper;
            if (evt.customStyle.TryGetValue(MarkTouchProperty, out var touch)) _markTouch = touch;
            _route.MarkDirtyRepaint();
        }

        private void DrawMarks(MeshGenerationContext context)
        {
            DrawPlace(context.painter2D);
            DrawRoute(context.painter2D);
        }

        // the place: the light inside the grid's lines, the pin's shadow on the ground under its middle (smaller and
        // fainter as the pin rises) and the pin over it, every shadow first
        private void DrawPlace(Painter2D painter)
        {
            if (_place == null) return;
            var corners = _placeCorners;
            for (int i = 0; i < corners.Length; i++) corners[i] = _route.WorldToLocal(_root.LocalToWorld(_place[i]));
            float up = GuidePlace.Float(_time);

            GuidePlace.Inset(corners, _placeWidth, _placeHeight, GuidePlace.LightInset, _placeLight);
            painter.fillColor = new Color(_markPaper.r, _markPaper.g, _markPaper.b, PlaceLight);
            painter.BeginPath();
            RoundedQuad(painter, _placeLight, GuidePlace.CellSide(corners, _placeWidth, _placeHeight) * LightRounding);
            painter.Fill();

            float spot = SpotCells * (1f - .3f * up);
            painter.fillColor = new Color(_markInk.r, _markInk.g, _markInk.b, SpotAlpha * (1f - .4f * up));
            painter.BeginPath();
            for (int i = 0; i < SpotSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / SpotSegments;
                var point = GuidePlace.At(corners, .5f + Mathf.Cos(angle) * spot / _placeWidth,
                    .5f + Mathf.Sin(angle) * spot / _placeHeight);
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            painter.ClosePath();
            painter.Fill();

            float radius = GuidePlace.PinRadius(_placeWidth, _placeHeight);
            var centre = GuidePlace.PinCentre(corners, radius, up);
            painter.fillColor = new Color(_markInk.r, _markInk.g, _markInk.b, ShadowAlpha / ShadowLayers.Length);
            foreach (float grow in ShadowLayers)
            {
                painter.BeginPath();
                Pin(painter, centre + ShadowOffset, radius + grow);
                painter.Fill();
            }
            painter.fillColor = _markInk;
            painter.BeginPath();
            Pin(painter, centre, radius);
            painter.Fill();
            painter.fillColor = _markPaper;
            painter.BeginPath();
            Pin(painter, centre, radius - PinOutline);
            painter.Fill();
        }

        // a disc with a tail down to its tip, the tail's sides touching the disc (GuidePlace.PinArc)
        private static void Pin(Painter2D painter, Vector2 centre, float radius)
        {
            var (from, to) = GuidePlace.PinArc;
            painter.MoveTo(GuidePlace.PinTip(centre, radius));
            for (int i = 0; i <= PinSegments; i++)
            {
                float angle = Mathf.Lerp(from, to, i / (float)PinSegments);
                painter.LineTo(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            painter.ClosePath();
        }

        // a closed outline through four corners, each rounded
        private static void RoundedQuad(Painter2D painter, Vector2[] corners, float radius)
        {
            int count = corners.Length;
            painter.MoveTo((corners[count - 1] + corners[0]) / 2f);
            for (int i = 0; i < count; i++)
            {
                var previous = corners[(i + count - 1) % count];
                var next = corners[(i + 1) % count];
                float rounding = Mathf.Min(radius, Vector2.Distance(corners[i], previous) / 2f,
                    Vector2.Distance(corners[i], next) / 2f);
                painter.ArcTo(corners[i], next, rounding);
            }
            painter.ClosePath();
        }

        // the picture rides in the pin's disc; the card stands over the pin's highest point, so it keeps still
        private Rect PlacePin(Rect box)
        {
            float radius = GuidePlace.PinRadius(_placeWidth, _placeHeight);
            var centre = GuidePlace.PinCentre(_place, radius, GuidePlace.Float(_time));
            var art = _shown.Target.Art;
            if (_pinArt.sprite != art) _pinArt.sprite = art;
            Ui.Show(_pinArt, art != null);
            float side = radius * PinArtShare;
            SetRect(_pinArt, centre.x - side / 2f, centre.y - side / 2f, side, side);
            float top = GuidePlace.PinCentre(_place, radius, 1f).y - radius;
            return Rect.MinMaxRect(Mathf.Min(box.xMin, centre.x - radius), Mathf.Min(box.yMin, top),
                Mathf.Max(box.xMax, centre.x + radius), box.yMax);
        }

        // the haul's arrow (GuideRoute): the track's dots and the head the touch's colour in a white rim, the goods'
        // tokens white discs in a ring of it. Every shadow goes first, so none falls on a neighbour; the head last, over
        // the token going into it. The goods' pictures are elements of their own over the tokens (PlaceCargo)
        private void DrawRoute(Painter2D painter)
        {
            if (!_routeTraced) return;
            painter.fillColor = new Color(_markInk.r, _markInk.g, _markInk.b, ShadowAlpha / ShadowLayers.Length);
            foreach (float grow in ShadowLayers)
            {
                painter.BeginPath();
                // the track lies low: a smaller, nearer shadow
                foreach (var dot in _routeTrack) Circle(painter, dot + ShadowOffset * .5f, GuideRoute.TrackRadius + grow * .4f);
                foreach (var token in _routeTokens) Circle(painter, token.Centre + ShadowOffset, token.Radius + grow);
                Rounded(painter, _head, ShadowOffset, grow);
                painter.Fill();
            }
            painter.fillColor = _markPaper;
            painter.BeginPath();
            foreach (var dot in _routeTrack) Circle(painter, dot, GuideRoute.TrackRadius);
            painter.Fill();
            painter.fillColor = _markTouch;
            painter.BeginPath();
            foreach (var dot in _routeTrack) Circle(painter, dot, GuideRoute.TrackRadius - TrackRim);
            foreach (var token in _routeTokens) Circle(painter, token.Centre, token.Radius);
            painter.Fill();
            painter.fillColor = _markPaper;
            painter.BeginPath();
            foreach (var token in _routeTokens) Circle(painter, token.Centre, token.Radius * (1f - TokenRing));
            Rounded(painter, _head, Vector2.zero, 0f);
            painter.Fill();
            painter.fillColor = _markTouch;
            painter.BeginPath();
            Rounded(painter, _head, Vector2.zero, -HeadRim);
            painter.Fill();
        }

        // the goods' pictures sit over their tokens, each token's turn choosing its good
        private void PlaceCargo()
        {
            int shown = _routeCargo.Count > 0 ? _routeTokens.Count : 0;
            while (_cargoPictures.Count < shown)
            {
                var picture = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                picture.AddToClassList("guide-route__cargo");
                _route.Add(picture);
                _cargoPictures.Add(picture);
            }
            for (int i = 0; i < _cargoPictures.Count; i++)
            {
                var picture = _cargoPictures[i];
                Ui.Show(picture, i < shown);
                if (i >= shown) continue;
                var token = _routeTokens[i];
                int turn = token.Turn % _routeCargo.Count;
                picture.sprite = _routeCargo[turn < 0 ? turn + _routeCargo.Count : turn];
                float size = CargoPicture * token.Radius / GuideRoute.TokenRadius;
                picture.style.left = token.Centre.x - size / 2f;
                picture.style.top = token.Centre.y - size / 2f;
                picture.style.width = size;
                picture.style.height = size;
            }
        }

        private Vector2 OnRoute(Vector2 point) => _route.WorldToLocal(_root.LocalToWorld(point));

        private static void Circle(Painter2D painter, Vector2 centre, float radius)
        {
            if (radius <= .3f) return;
            painter.MoveTo(centre + new Vector2(radius, 0f));
            painter.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(180f));
            painter.Arc(centre, radius, Angle.Degrees(180f), Angle.Degrees(360f));
            painter.ClosePath();
        }

        // a closed outline with rounded corners, moved by offset and grown (or shrunk, when negative) all round: each
        // side moves out along its normal and each corner to where its sides meet, an outward corner rounding by more
        // and a notch by less. It runs the way Circle's arcs do, so one fill joins it with the dots
        private void Rounded(Painter2D painter, Vector2[] corners, Vector2 offset, float grow)
        {
            int count = corners.Length;
            float area = 0f;
            for (int i = 0; i < count; i++)
                area += corners[i].x * corners[(i + 1) % count].y - corners[(i + 1) % count].x * corners[i].y;
            float turn = Mathf.Sign(area);
            for (int i = 0; i < count; i++)
            {
                var inSide = (corners[i] - corners[(i + count - 1) % count]).normalized;
                var outSide = (corners[(i + 1) % count] - corners[i]).normalized;
                var inNormal = new Vector2(inSide.y, -inSide.x) * turn;
                var outNormal = new Vector2(outSide.y, -outSide.x) * turn;
                _grown[i] = corners[i] + offset + (inNormal + outNormal) * (grow / (1f + Vector2.Dot(inNormal, outNormal)));
                bool outward = (inSide.x * outSide.y - inSide.y * outSide.x) * turn > 0f;
                _rounding[i] = Mathf.Max(0f, GuideRoute.HeadRounding[i] + (outward ? grow : -grow));
            }
            // clockwise on the screen, as Circle's arcs run: backwards through the corners when they turn the other way
            int Corner(int k) => turn > 0f ? k % count : (count - k % count) % count;
            painter.MoveTo((_grown[Corner(count - 1)] + _grown[Corner(0)]) / 2f);
            for (int k = 0; k < count; k++)
            {
                int i = Corner(k);
                if (_rounding[i] > .01f) painter.ArcTo(_grown[i], _grown[Corner(k + 1)], _rounding[i]);
                else painter.LineTo(_grown[i]);
            }
            painter.ClosePath();
        }
    }
}
