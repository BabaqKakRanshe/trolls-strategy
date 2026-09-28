using System;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// A hint card over the HUD element under the pointer: a title and a few lines. UI Toolkit's own tooltip
    /// only works in editor panels, so the runtime HUD draws this one. The card never catches the pointer and
    /// goes away when its element leaves the pointer or the panel.
    /// </summary>
    public sealed class HudTooltip
    {
        private readonly VisualElement _root;
        private readonly VisualElement _card;
        private readonly Label _title;
        private readonly Label _body;
        private VisualElement _target;

        public HudTooltip(VisualElement root)
        {
            _root = root;
            _card = Ui.Box("panel tooltip");
            _card.pickingMode = PickingMode.Ignore;
            _title = Ui.Text(string.Empty, "tooltip__title t-bold");
            _body = Ui.Text(string.Empty, "tooltip__body");
            _title.pickingMode = PickingMode.Ignore;
            _body.pickingMode = PickingMode.Ignore;
            _card.Add(_title);
            _card.Add(_body);
            root.Add(_card);
            Ui.Show(_card, false);
        }

        public bool IsShown => Ui.IsShown(_card);
        public string Title => _title.text;
        public string Body => _body.text;

        /// <summary>Shows the hint while the pointer is over <paramref name="target"/>; texts are read on entry.</summary>
        public void Attach(VisualElement target, Func<string> title, Func<string> body)
        {
            target.RegisterCallback<PointerEnterEvent>(_ => Show(target, title?.Invoke(), body?.Invoke()));
            target.RegisterCallback<PointerLeaveEvent>(_ => Hide(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => Hide(target));
        }

        /// <summary>Opens the card above the element; tests call it directly, since they have no pointer.</summary>
        public void Show(VisualElement target, string title, string body)
        {
            _target = target;
            Ui.SetText(_title, title);
            Ui.SetText(_body, body);
            Ui.Show(_body, !string.IsNullOrEmpty(body));
            Ui.Show(_card, true);
            _card.BringToFront();
            var bounds = target.worldBound;
            var origin = _root.worldBound;
            if (float.IsNaN(bounds.x) || float.IsNaN(origin.x)) return;
            // .tooltip centres itself above this point with a translate
            _card.style.left = bounds.center.x - origin.x;
            _card.style.top = bounds.yMin - origin.y - 8f;
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
