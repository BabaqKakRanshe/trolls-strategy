using System;

namespace TrollStrategy.Support
{
    /// <summary>
    /// Frames per second over half-second windows and the slowest frame of the last five seconds. Fed with
    /// unscaled frame times, so a paused colony or a sped-up battle replay does not bend the reading.
    /// </summary>
    public sealed class FrameRateMeter
    {
        public const float WindowSeconds = .5f;
        /// <summary>Windows the slowest frame is remembered for.</summary>
        public const int WorstWindows = 10;

        private readonly float[] _windowWorst = new float[WorstWindows];
        private int _nextWindow;
        private float _elapsed;
        private int _frames;
        private float _worst;

        /// <summary>Frames per second in the last finished window; 0 before the first one.</summary>
        public float Fps { get; private set; }

        /// <summary>The longest frame over the last <see cref="WorstWindows"/> windows, milliseconds.</summary>
        public float WorstFrameMs { get; private set; }

        public void Add(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds)) return;
            _elapsed += deltaSeconds;
            _frames++;
            _worst = Math.Max(_worst, deltaSeconds);
            if (_elapsed < WindowSeconds) return;

            Fps = _frames / _elapsed;
            _windowWorst[_nextWindow] = _worst;
            _nextWindow = (_nextWindow + 1) % WorstWindows;
            float worst = 0f;
            foreach (float frame in _windowWorst) worst = Math.Max(worst, frame);
            WorstFrameMs = worst * 1000f;
            _elapsed = 0f;
            _frames = 0;
            _worst = 0f;
        }
    }
}
