using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// What the viewer of a show bot sees on top of the game: the bot's cursor (the real one is not drawn into the
    /// game view), a ring for every click, the key it presses, and a caption with what it does and why. A screen
    /// panel of its own above the HUD; nothing in it takes the pointer, so the game never notices it. Developer text,
    /// not translated.
    /// </summary>
    public sealed class BotOverlay
    {
        private const string StylePath = "Assets/Game/Bots/Show/BotShow.uss";
        private const string ThemePath = "Assets/Game/UI/Styles/Theme.uss";

        private readonly GameObject _host;
        private readonly PanelSettings _settings;
        private readonly VisualElement _root, _cursor, _rings, _card;
        private readonly Label _title, _doing, _why, _badge, _keycap;
        private readonly List<(Vector2 At, float Age, bool Right)> _clicks = new();
        private Vector2 _pointer;
        private float _keyAge = 99f;

        public BotOverlay(PanelSettings like)
        {
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.name = "BotShow overlay";
            _settings.themeStyleSheet = like != null ? like.themeStyleSheet : null;
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _settings.sortingOrder = 30000;
            _settings.clearColor = false;
            _host = new GameObject("BotShow overlay") { hideFlags = HideFlags.DontSave };
            var document = _host.AddComponent<UIDocument>();
            Host = document;
            document.panelSettings = _settings;
            document.sortingOrder = 30000;

            _root = document.rootVisualElement;
            foreach (string path in new[] { ThemePath, StylePath })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null) _root.styleSheets.Add(sheet);
            }
            _root.AddToClassList("botshow");
            _root.pickingMode = PickingMode.Ignore;
            _root.StretchToParentSize();

            _rings = Add(new VisualElement(), "botshow__rings");
            _rings.StretchToParentSize();
            _rings.generateVisualContent += DrawRings;

            _card = Add(new VisualElement(), "botshow__card");
            _title = Add(new Label(), "botshow__title", _card);
            _doing = Add(new Label(), "botshow__doing", _card);
            _why = Add(new Label(), "botshow__why", _card);
            _badge = Add(new Label(), "botshow__badge", _card);

            _keycap = Add(new Label(), "botshow__key");
            _cursor = Add(new VisualElement(), "botshow__cursor");
            _cursor.generateVisualContent += DrawCursor;
            Scale();
        }

        /// <summary>The overlay's document: a scene behaviour the show bot runs its coroutines on.</summary>
        public MonoBehaviour Host { get; }

        /// <summary>The bot's line: its name, the quest and the colony clock.</summary>
        public void SetTitle(string text) => _title.text = text;

        /// <summary>What the bot does now and why; null hides a line.</summary>
        public void SetThought(string doing, string why)
        {
            _doing.text = doing ?? "";
            _doing.style.display = string.IsNullOrEmpty(doing) ? DisplayStyle.None : DisplayStyle.Flex;
            _why.text = string.IsNullOrEmpty(why) ? "" : "Зачем: " + why;
            _why.style.display = string.IsNullOrEmpty(why) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The time badge: how fast the colony runs while the bot waits; null hides it.</summary>
        public void SetBadge(string text)
        {
            _badge.text = text ?? "";
            _badge.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The cursor at a screen point (pixels from the bottom left, as the mouse counts).</summary>
        public void Pointer(Vector2 screen)
        {
            _pointer = ToPanel(screen);
            _cursor.style.left = _pointer.x;
            _cursor.style.top = _pointer.y;
            _keycap.style.left = _pointer.x + 26f;
            _keycap.style.top = _pointer.y + 26f;
        }

        public void Click(bool right) => _clicks.Add((_pointer, 0f, right));

        public void Key(string label)
        {
            _keycap.text = label;
            _keyAge = 0f;
        }

        /// <summary>Ages the rings and the key cap; call once a frame with the video's frame time.</summary>
        public void Tick(float seconds)
        {
            Scale();
            for (int i = _clicks.Count - 1; i >= 0; i--)
            {
                var click = _clicks[i];
                click.Age += seconds;
                if (click.Age > RingSeconds) _clicks.RemoveAt(i);
                else _clicks[i] = click;
            }
            _rings.MarkDirtyRepaint();
            _keyAge += seconds;
            _keycap.style.opacity = Mathf.Clamp01(1.4f - _keyAge * 1.6f);
        }

        public void Dispose()
        {
            if (_host != null) Object.Destroy(_host);
            if (_settings != null) Object.Destroy(_settings);
        }

        private const float RingSeconds = 0.45f;

        // panel pixels follow a 1080-high screen, so the overlay reads the same at any game view size
        private float PanelScale => Mathf.Max(0.5f, Screen.height / 1080f);

        private void Scale()
        {
            if (!Mathf.Approximately(_settings.scale, PanelScale)) _settings.scale = PanelScale;
        }

        private Vector2 ToPanel(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y) / PanelScale;

        private T Add<T>(T element, string className, VisualElement parent = null) where T : VisualElement
        {
            element.AddToClassList(className);
            element.pickingMode = PickingMode.Ignore;
            (parent ?? _root).Add(element);
            return element;
        }

        private void DrawRings(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            foreach (var (at, age, right) in _clicks)
            {
                float t = age / RingSeconds;
                var color = right ? new Color(1f, 0.78f, 0.25f) : new Color(1f, 1f, 1f);
                color.a = 1f - t;
                painter.strokeColor = color;
                painter.lineWidth = 4f * (1f - t) + 1f;
                painter.BeginPath();
                painter.Arc(at, 8f + 26f * t, 0f, 360f);
                painter.Stroke();
            }
        }

        // an arrow pointer: white with a dark rim, its tip on the point
        private static void DrawCursor(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var points = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 26f), new Vector2(6.5f, 20f), new Vector2(11f, 30f),
                new Vector2(15.5f, 28f), new Vector2(11f, 18.5f), new Vector2(19.5f, 18.5f)
            };
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
            painter.ClosePath();
            painter.fillColor = Color.white;
            painter.Fill();
            painter.strokeColor = new Color(0.12f, 0.13f, 0.18f);
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }
    }
}
