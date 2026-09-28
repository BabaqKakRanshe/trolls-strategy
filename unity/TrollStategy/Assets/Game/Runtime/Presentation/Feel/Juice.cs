using UnityEngine;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>
    /// Short presentation-only reactions of world objects (punch, squash, pop-in, shake, nudge) that confirm
    /// an action within a frame. Screen UI has its own in UiMotion. They run on unscaled time and always return the target to its rest state.
    /// </summary>
    public static class Juice
    {
        /// <summary>Uniform spring: a click, a pickup, a counter that changed.</summary>
        public static void Punch(Transform target, float strength = .12f, float duration = .28f) =>
            Get(target)?.PlayScale(new Vector3(strength, strength, strength), duration, false);

        /// <summary>Wide-then-tall spring: something landed or was placed.</summary>
        public static void Squash(Transform target, float strength = .18f, float duration = .38f) =>
            Get(target)?.PlayScale(new Vector3(strength, -strength, strength), duration, false);

        /// <summary>Grows from nothing with a small overshoot: something appeared.</summary>
        public static void PopIn(Transform target, float duration = .32f) =>
            Get(target)?.PlayScale(Vector3.zero, duration, true);

        /// <summary>Random jitter that decays: an impact.</summary>
        public static void Shake(Transform target, float amplitude, float duration = .25f) =>
            Get(target)?.PlayMove(amplitude, duration, false);

        /// <summary>Side-to-side wobble: "no" — the action was refused.</summary>
        public static void Nudge(Transform target, float amplitude = 8f, float duration = .35f) =>
            Get(target)?.PlayMove(amplitude, duration, true);

        /// <summary>Hover scale the target eases to (1 = rest); combines with punches.</summary>
        public static void Hover(Transform target, float scale) => Get(target)?.SetHover(scale);

        /// <summary>Shrinks the object away, then destroys it: something was removed, not just gone.</summary>
        public static void ShrinkAndDestroy(GameObject target, float duration = .22f)
        {
            if (target == null) return;
            if (!target.TryGetComponent<JuiceRemoval>(out var removal)) removal = target.AddComponent<JuiceRemoval>();
            removal.Begin(duration);
        }

        private static JuiceTarget Get(Transform target)
        {
            if (target == null) return null;
            return target.TryGetComponent<JuiceTarget>(out var juice) ? juice : target.gameObject.AddComponent<JuiceTarget>();
        }
    }

    /// <summary>Shrink-away before destruction; see <see cref="Juice.ShrinkAndDestroy"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class JuiceRemoval : MonoBehaviour
    {
        private Vector3 _startScale;
        private float _time;
        private float _duration;
        private bool _running;

        internal void Begin(float duration)
        {
            if (_running) return;
            _running = true;
            if (TryGetComponent<JuiceTarget>(out var juice))
            {
                // disabling snaps an interrupted punch or hover back to rest, so the shrink starts from there
                juice.enabled = false;
                Destroy(juice);
            }
            _startScale = transform.localScale;
            _duration = Mathf.Max(.01f, duration);
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        }

        private void Update()
        {
            if (!_running) return;
            _time += Time.unscaledDeltaTime;
            float t = _time / _duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            // a tiny swell first, then gone: reads as "poof" rather than a pop-out
            float scale = t < .25f ? 1f + .12f * Ease.OutCubic(t / .25f) : 1.12f * (1f - Ease.InCubic((t - .25f) / .75f));
            transform.localScale = _startScale * scale;
        }
    }

    /// <summary>Per-object state of <see cref="Juice"/> reactions; added on first use.</summary>
    [DisallowMultipleComponent]
    public sealed class JuiceTarget : MonoBehaviour
    {
        private Vector3 _restScale;
        private bool _scaleActive;
        private bool _popIn;
        private Vector3 _scaleAmount;
        private float _scaleTime;
        private float _scaleDuration;
        private float _hover = 1f;
        private float _hoverTarget = 1f;

        private Vector3 _restPosition;
        private bool _moveActive;
        private bool _nudge;
        private float _moveAmplitude;
        private float _moveTime;
        private float _moveDuration;
        private float _seed;

        private bool ScaleBusy => _scaleActive || !Mathf.Approximately(_hover, 1f) || !Mathf.Approximately(_hoverTarget, 1f);

        private void Awake() => _seed = (GetInstanceHash() % 1000) * .173f;

        internal void PlayScale(Vector3 amount, float duration, bool popIn)
        {
            if (!ScaleBusy) _restScale = transform.localScale;
            _scaleAmount = amount;
            _popIn = popIn;
            _scaleTime = 0f;
            _scaleDuration = Mathf.Max(.01f, duration);
            _scaleActive = true;
            enabled = true;
            ApplyScale();
        }

        internal void SetHover(float scale)
        {
            if (!ScaleBusy) _restScale = transform.localScale;
            _hoverTarget = Mathf.Max(.1f, scale);
            enabled = true;
        }

        internal void PlayMove(float amplitude, float duration, bool nudge)
        {
            if (!_moveActive) _restPosition = transform.localPosition;
            _moveAmplitude = amplitude;
            _nudge = nudge;
            _moveTime = 0f;
            _moveDuration = Mathf.Max(.01f, duration);
            _moveActive = true;
            enabled = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (ScaleBusy)
            {
                _scaleTime += dt;
                _hover = Mathf.MoveTowards(_hover, _hoverTarget, dt * 1.2f);
                if (_scaleActive && _scaleTime >= _scaleDuration) _scaleActive = false;
                ApplyScale();
            }

            if (_moveActive)
            {
                _moveTime += dt;
                float t = _moveTime / _moveDuration;
                if (t >= 1f)
                {
                    _moveActive = false;
                    transform.localPosition = _restPosition;
                }
                else if (_nudge)
                {
                    transform.localPosition = _restPosition + Vector3.right * (_moveAmplitude * Ease.Spring(t, 2.5f));
                }
                else
                {
                    float decay = (1f - t) * _moveAmplitude;
                    float x = Mathf.PerlinNoise(_seed, _moveTime * 28f) - .5f;
                    float y = Mathf.PerlinNoise(_moveTime * 28f, _seed + 7f) - .5f;
                    transform.localPosition = _restPosition + new Vector3(x, y, 0f) * (2f * decay);
                }
            }

            if (!ScaleBusy && !_moveActive) enabled = false;
        }

        private void ApplyScale()
        {
            var scale = _restScale * _hover;
            if (_scaleActive)
            {
                float t = _scaleTime / _scaleDuration;
                if (_popIn) scale *= Ease.OutBack(t, 2.2f);
                else scale += Vector3.Scale(scale, _scaleAmount) * Ease.Spring(t);
            }
            transform.localScale = scale;
        }

        private void OnDisable()
        {
            // an interrupted reaction must not leave the object squashed or offset
            if (_scaleActive || !Mathf.Approximately(_hover, 1f) || !Mathf.Approximately(_hoverTarget, 1f))
            {
                _scaleActive = false;
                transform.localScale = _restScale;
                _hover = _hoverTarget = 1f;
            }
            if (_moveActive)
            {
                _moveActive = false;
                transform.localPosition = _restPosition;
            }
        }

        private int GetInstanceHash() => Mathf.Abs(GetHashCode());
    }
}
