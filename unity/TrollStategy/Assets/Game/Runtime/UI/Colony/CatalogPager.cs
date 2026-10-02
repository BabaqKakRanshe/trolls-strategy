using System.Collections.Generic;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// One tray of catalog tokens shown a page at a time: as many whole tokens as the band holds (eight on a
    /// 1920 px screen), so the band's edge never cuts one. Round arrows at the tray's ends and the mouse wheel
    /// turn the page, a dot per page shows where the player is. The arrow towards the quest's token and that
    /// page's dot wear the accent, and a new quest turns to its page once.
    /// </summary>
    public sealed class CatalogPager
    {
        public const int MaxPerPage = 8;
        // .token: 139 px and 13 px margins; .catalog keeps 300 px clear at both ends of the band
        private const float TokenWidth = 165f;
        private const float BandEnds = 600f;

        private readonly VisualElement _band;
        private readonly VisualElement _tray;
        private readonly Button _prev;
        private readonly Button _next;
        private readonly VisualElement _prevPip;
        private readonly VisualElement _nextPip;
        private readonly VisualElement _dots;
        private readonly List<VisualElement> _all = new();
        private readonly List<VisualElement> _listed = new();
        private VisualElement _focus;
        private int _page;
        private int _perPage = MaxPerPage;

        /// <summary>The page turned: the panel lays out what depends on the arrows (the hire stepper).</summary>
        public event System.Action Turned;

        public CatalogPager(VisualElement band, VisualElement tray, Button prev, Button next, VisualElement dots)
        {
            _band = band;
            _tray = tray;
            _dots = dots;
            _prev = UiFeel.Bind(prev, () => Turn(-1), Sfx.UiClick);
            _next = UiFeel.Bind(next, () => Turn(1), Sfx.UiClick);
            _prevPip = Pip(_prev);
            _nextPip = Pip(_next);
            // the wheel over the tokens bubbles up to the tray
            _tray.RegisterCallback<WheelEvent>(OnWheel);
            _band?.RegisterCallback<GeometryChangedEvent>(_ => Measure());
        }

        public int Page => _page;
        public int PerPage => _perPage;
        public int PageCount => Mathf.Max(1, (_listed.Count + _perPage - 1) / _perPage);
        public Button PrevButton => _prev;
        public Button NextButton => _next;
        public bool HasPages => PageCount > 1;

        /// <summary>Whether a listed token stands on the page shown now.</summary>
        public bool IsOnPage(VisualElement token)
        {
            int at = _listed.IndexOf(token);
            return at >= 0 && at / _perPage == _page;
        }

        /// <summary>
        /// The tray's tokens in order and whether each is listed at all (closed ones stay out); the quest's
        /// token, or null. A new quest token turns the tray to its page.
        /// </summary>
        public void Show(IReadOnlyList<(VisualElement Root, bool Listed)> tokens, VisualElement focus)
        {
            _all.Clear();
            _listed.Clear();
            foreach (var (root, listed) in tokens)
            {
                _all.Add(root);
                if (listed) _listed.Add(root);
            }
            if (focus != _focus)
            {
                _focus = focus;
                int at = focus != null ? _listed.IndexOf(focus) : -1;
                if (at >= 0) _page = at / _perPage;
            }
            Apply();
        }

        public void Turn(int step)
        {
            int page = Mathf.Clamp(_page + step, 0, PageCount - 1);
            if (page == _page) return;
            _page = page;
            Apply();
            UiMotion.PopIn(_tray, .2f);
        }

        private void OnWheel(WheelEvent evt)
        {
            if (!HasPages || Mathf.Approximately(evt.delta.y, 0f)) return;
            Turn(evt.delta.y > 0f ? 1 : -1);
            evt.StopPropagation();
        }

        // tokens of the page shown, the others hidden; arrows only where there is somewhere to go
        private void Apply()
        {
            _page = Mathf.Clamp(_page, 0, PageCount - 1);
            foreach (var root in _all) Ui.Show(root, false);
            for (int i = 0; i < _listed.Count; i++) Ui.Show(_listed[i], i / _perPage == _page);

            bool paged = HasPages;
            Ui.Show(_prev, paged);
            Ui.Show(_next, paged);
            UiFeel.SetAvailable(_prev, _page > 0);
            UiFeel.SetAvailable(_next, _page < PageCount - 1);
            int focusPage = _focus != null && _listed.Contains(_focus) ? _listed.IndexOf(_focus) / _perPage : -1;
            Ui.Show(_prevPip, focusPage >= 0 && focusPage < _page);
            Ui.Show(_nextPip, focusPage > _page);

            _dots.Clear();
            Ui.Show(_dots, paged);
            if (paged)
                for (int i = 0; i < PageCount; i++)
                {
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("catalog-dot");
                    dot.EnableInClassList("is-on", i == _page);
                    dot.EnableInClassList("is-quest", i == focusPage && i != _page);
                    _dots.Add(dot);
                }
            Turned?.Invoke();
        }

        // a narrower band (a phone, a larger interface) takes fewer tokens a page; the first one shown stays
        private void Measure()
        {
            float width = _band.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) return;
            int perPage = Mathf.Clamp(Mathf.FloorToInt((width - BandEnds) / TokenWidth), 3, MaxPerPage);
            if (perPage == _perPage) return;
            int first = _page * _perPage;
            _perPage = perPage;
            _page = first / _perPage;
            Apply();
        }

        private static VisualElement Pip(Button arrow)
        {
            var pip = new VisualElement { pickingMode = PickingMode.Ignore };
            pip.AddToClassList("catalog-arrow__pip");
            arrow.Add(pip);
            Ui.Show(pip, false);
            return pip;
        }
    }
}
