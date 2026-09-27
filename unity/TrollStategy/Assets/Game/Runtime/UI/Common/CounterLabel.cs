using TrollStrategy.Presentation.Feel;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// A HUD number that counts to its new value and flashes green on gain, red on loss, so the player
    /// sees what an action cost or earned. Counters that tick on their own (ore mined every step) count
    /// without the flash. Owns its label's text.
    /// </summary>
    public sealed class CounterLabel
    {
        private const float CountTime = .45f;
        private const float MinPunchInterval = .35f;
        private const long FrameMs = 16;

        private static readonly Color Gain = new(.55f, 1f, .55f, 1f);
        private static readonly Color Loss = new(1f, .45f, .4f, 1f);

        private readonly Label _label;
        private readonly bool _punch;
        private IVisualElementScheduledItem _tick;
        private bool _initialised;
        private int _shown;
        private int _from;
        private int _target;
        private float _start;
        private float _lastPunch = -10f;

        public CounterLabel(Label label, bool punch)
        {
            _label = label;
            _punch = punch;
        }

        public int Value => _target;

        public void Set(int value)
        {
            if (_label == null) return;
            // before the HUD is on screen there is nothing to animate: show the number as is
            if (!_initialised || _label.panel == null)
            {
                _initialised = true;
                _shown = _from = _target = value;
                _tick?.Pause();
                _label.text = value.ToString();
                return;
            }
            if (value == _target) return;

            _from = _shown;
            _target = value;
            _start = Time.unscaledTime;
            if (_punch && Time.unscaledTime - _lastPunch >= MinPunchInterval)
            {
                _lastPunch = Time.unscaledTime;
                UiMotion.Punch(_label, value > _from ? .16f : .12f, .3f);
                UiMotion.Flash(_label, value > _from ? Gain : Loss, .45f);
            }
            _tick ??= _label.schedule.Execute(Step).Every(FrameMs);
            _tick.Resume();
        }

        private void Step()
        {
            float t = (Time.unscaledTime - _start) / CountTime;
            int shown = t >= 1f ? _target : Mathf.RoundToInt(Mathf.Lerp(_from, _target, Ease.OutCubic(t)));
            if (shown != _shown)
            {
                _shown = shown;
                _label.text = shown.ToString();
            }
            if (t >= 1f) _tick.Pause();
        }
    }
}
