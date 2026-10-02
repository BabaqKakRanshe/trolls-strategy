using System;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// A hint card by the HUD element under the pointer: a title, a few lines and the element's key. UI
    /// Toolkit's own tooltip only works in editor panels, so the runtime HUD draws this one. The card opens
    /// above its element, below it along the top edge of the screen and to its left along the right edge.
    /// It never catches the pointer and goes away when its element leaves the pointer or the panel.
    /// </summary>
    public sealed class HudTooltip
    {
        private const string BelowClass = "tooltip--below";
        private const string LeftClass = "tooltip--left";
        // Distance from the element to the card's visible edge.
        private const float Gap = 8f;
        // The card's picture (.sheet) reaches past its visible edge by this much on each side.
        private const float ShadowSide = 28f;
        private const float ShadowAbove = 22f;
        private const float ShadowBelow = 36f;
        // Elements this close to the top open the card below them, this close to the right to their left.
        private const float TopZone = 180f;
        private const float RightZone = 300f;

        private readonly VisualElement _root;
        private readonly VisualElement _card;
        private readonly Label _title;
        private readonly Label _body;
        private readonly Label _key;
        private VisualElement _target;

        public HudTooltip(VisualElement root)
        {
            _root = root;
            _card = Ui.Box("sheet tooltip");
            _card.pickingMode = PickingMode.Ignore;
            var text = Ui.Box("tooltip__text");
            text.pickingMode = PickingMode.Ignore;
            _title = Ui.Text(string.Empty, "tooltip__title t-bold");
            _body = Ui.Text(string.Empty, "tooltip__body");
            _key = Ui.Text(string.Empty, "hotkey tooltip__key");
            _title.pickingMode = PickingMode.Ignore;
            _body.pickingMode = PickingMode.Ignore;
            _key.pickingMode = PickingMode.Ignore;
            text.Add(_title);
            text.Add(_body);
            _card.Add(text);
            _card.Add(_key);
            root.Add(_card);
            Ui.Show(_card, false);
        }

        public bool IsShown => Ui.IsShown(_card);
        public string Title => _title.text;
        public string Body => _body.text;
        public string Key => _key.text;

        /// <summary>
        /// Shows the hint while the pointer is over <paramref name="target"/>; texts are read on entry, so
        /// they follow the game. <paramref name="key"/> is the keyboard shortcut, if the element has one.
        /// </summary>
        public void Attach(VisualElement target, Func<string> title, Func<string> body, string key = null)
        {
            target.RegisterCallback<PointerEnterEvent>(_ => Show(target, title?.Invoke(), body?.Invoke(), key));
            // a finger leaves as it lifts: the card stays long enough to be read
            target.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (evt.pointerType == UnityEngine.UIElements.PointerType.touch)
                    target.schedule.Execute(() => Hide(target)).ExecuteLater(TouchHoldMs);
                else Hide(target);
            });
            target.RegisterCallback<DetachFromPanelEvent>(_ => Hide(target));
        }

        private const long TouchHoldMs = 2500;

        /// <summary>Opens the card by the element; tests call it directly, since they have no pointer.</summary>
        public void Show(VisualElement target, string title, string body, string key = null)
        {
            _target = target;
            Ui.SetText(_title, title);
            Ui.SetText(_body, body);
            Ui.SetText(_key, key);
            Ui.Show(_body, !string.IsNullOrEmpty(body));
            Ui.Show(_key, !string.IsNullOrEmpty(key));
            Ui.Show(_card, true);
            _card.BringToFront();
            var bounds = target.worldBound;
            var origin = _root.worldBound;
            if (float.IsNaN(bounds.x) || float.IsNaN(origin.x)) return;

            // the classes translate the card so that left/top is its anchor point on the element's edge
            bool left = bounds.xMax > origin.xMax - RightZone;
            bool below = !left && bounds.yMin - origin.y < TopZone;
            _card.EnableInClassList(LeftClass, left);
            _card.EnableInClassList(BelowClass, below);
            if (left)
            {
                _card.style.left = bounds.xMin - origin.x - Gap + ShadowSide;
                _card.style.top = bounds.center.y - origin.y + (ShadowBelow - ShadowAbove) * .5f;
            }
            else if (below)
            {
                _card.style.left = bounds.xMin - origin.x - ShadowSide;
                _card.style.top = bounds.yMax - origin.y + Gap - ShadowAbove;
            }
            else
            {
                _card.style.left = bounds.center.x - origin.x;
                _card.style.top = bounds.yMin - origin.y - Gap + ShadowBelow;
            }
        }

        /// <summary>Keeps the card above parts added to the HUD after it.</summary>
        public void BringToFront() => _card.BringToFront();

        public void Hide(VisualElement target)
        {
            if (_target != target) return;
            _target = null;
            Ui.Show(_card, false);
        }
    }
}
