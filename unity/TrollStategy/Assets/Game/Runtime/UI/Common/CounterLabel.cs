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

        private static readonly Color Gain = new(.2f, .58f, .1f, 1f);
        private static readonly Color Loss = new(.76f, .2f, .12f, 1f);

        private readonly Label _label;
        private readonly bool _punch;
        private IVisualElementScheduledItem _tick;
        private bool _initialised;
        private int _shown;
        private int _from;
        private int _target;
        private float _start;
        private float _lastPunch = -10f;
        private float _countTime = CountTime;
        private float _nextDelay = -1f;
        private float _nextCountTime;
        private bool _punchPending;

        public CounterLabel(Label label, bool punch)
        {
            _label = label;
            _punch = punch;
        }

        public int Value => _target;

        /// <summary>
        /// The next change starts counting after <paramref name="delay"/> seconds and takes <paramref name="duration"/>
        /// (for gold that flies in first). A zero duration drops a wait that was asked for and not used.
        /// </summary>
        public void DelayNext(float delay, float duration)
        {
            _nextDelay = duration > 0f ? Mathf.Max(0f, delay) : -1f;
            _nextCountTime = duration;
        }

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
            bool delayed = _nextDelay >= 0f;
            _start = Time.unscaledTime + (delayed ? _nextDelay : 0f);
            _countTime = delayed ? _nextCountTime : CountTime;
            _nextDelay = -1f;
            // the punch comes with the count, so a delayed one waits for it
            _punchPending = _punch;
            _tick ??= _label.schedule.Execute(Step).Every(FrameMs);
            _tick.Resume();
            Step();
        }

        private void Step()
        {
            if (Time.unscaledTime < _start) return;
            if (_punchPending)
            {
                _punchPending = false;
                if (Time.unscaledTime - _lastPunch >= MinPunchInterval)
                {
                    _lastPunch = Time.unscaledTime;
                    UiMotion.Punch(_label, _target > _from ? .16f : .12f, .3f);
                    UiMotion.Flash(_label, _target > _from ? Gain : Loss, .45f);
                }
            }
            float t = (Time.unscaledTime - _start) / _countTime;
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
