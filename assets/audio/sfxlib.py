"""Small physically-inspired synthesis kit for build_sfx.py: modal mallets, wood, glass, metal, breath,
rustle, thumps, a small room, and short-window K-weighted loudness (ITU-R BS.1770 filters)."""
import numpy as np
from scipy import signal

SR = 44100
TAU = 2 * np.pi

_NOTE = {'C': 0, 'C#': 1, 'D': 2, 'D#': 3, 'E': 4, 'F': 5, 'F#': 6, 'G': 7, 'G#': 8, 'A': 9, 'A#': 10, 'B': 11}


def hz(name):
    n, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((12 * (o + 1) + _NOTE[n] - 69) / 12)


def buf(dur):
    return np.zeros(int(round(dur * SR)))


def at(dst, src, start):
    """Mix src into dst at start seconds (in place, truncated)."""
    i = int(round(start * SR))
    if i >= len(dst):
        return dst
    k = min(len(src), len(dst) - i)
    dst[i:i + k] += src[:k]
    return dst


def ramp_in(x, ms):
    k = max(1, int(ms / 1000 * SR))
    k = min(k, len(x))
    x[:k] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, k))
    return x


def fade_out(x, ms):
    k = min(len(x), max(1, int(ms / 1000 * SR)))
    x[-k:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, k))
    return x


def pink(n, rng):
    w = rng.standard_normal(n)
    f = np.fft.rfft(w)
    s = np.fft.rfftfreq(n, 1 / SR)
    s[0] = s[1] if n > 1 else 1
    y = np.fft.irfft(f / np.sqrt(s), n)
    return y / (np.std(y) + 1e-12)


def bandpass(x, lo, hi, order=2):
    hi = min(hi, 0.45 * SR)
    sos = signal.butter(order, [lo, hi], 'bandpass', fs=SR, output='sos')
    return signal.sosfilt(sos, x)


def highpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, 'highpass', fs=SR, output='sos'), x)


def lowpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, min(f, 0.45 * SR), 'lowpass', fs=SR, output='sos'), x)


def modes(f0, ratios, gains, t60, dur, rng, hardness=0.5, tilt=0.8, detune=1.5, onset_ms=0.8,
          glide=0.0, glide_ms=18.0):
    """Struck resonator: pairs of slightly detuned decaying partials (natural beating), mallet roll-off,
    higher modes dying faster, a soft contact onset and an optional pitch settle (plucked tines)."""
    n = int(round(dur * SR))
    t = np.arange(n) / SR
    out = np.zeros(n)
    fc = 1200 * (10 ** hardness)  # soft yarn ~1.2 kHz .. hard plastic ~12 kHz
    for r, g in zip(ratios, gains):
        f = f0 * r
        if f > 0.44 * SR:
            continue
        tk = t60 * (f0 / f) ** tilt
        amp = g / (1 + (f / fc) ** 2)
        env = np.exp(-6.91 * t / tk)
        for share, cents in ((0.62, -detune), (0.38, detune)):
            fi = f * 2 ** ((cents + rng.normal(0, 0.4)) / 1200)
            if glide:
                inst = fi * (1 + glide * np.exp(-t / (glide_ms / 1000)))
                ph = TAU * np.cumsum(inst) / SR
            else:
                ph = TAU * fi * t
            out += share * amp * env * np.sin(ph + rng.uniform(0, TAU))
    return ramp_in(out, onset_ms)


def contact(dur, lo, hi, decay_ms, rng, level=1.0):
    """Short filtered noise burst: the stick, nail or felt touching the surface."""
    n = int(round(dur * SR))
    t = np.arange(n) / SR
    x = bandpass(rng.standard_normal(n), lo, hi) * np.exp(-t / (decay_ms / 1000))
    x = ramp_in(x, 0.15)
    return level * x / (np.max(np.abs(x)) + 1e-12)


def svf_sweep(x, f_start, f_end, q, curve=1.0):
    """Time-varying state-variable band-pass (Chamberlin); cutoff glides f_start -> f_end."""
    n = len(x)
    s = np.linspace(0, 1, n) ** curve
    fc = f_start * (f_end / f_start) ** s
    f = 2 * np.sin(np.pi * np.minimum(fc, 0.2 * SR) / SR)
    damp = 1 / q
    low = band = 0.0
    y = np.empty(n)
    for i in range(n):
        high = x[i] - low - damp * band
        band += f[i] * high
        low += f[i] * band
        y[i] = band
    return y


def bell_env(n, rise=0.4, a=1.6, b=2.4):
    s = np.linspace(0, 1, n)
    u = np.where(s < rise, s / rise, 1 - (s - rise) / (1 - rise))
    e = np.where(s < rise, u ** a, u ** b)
    return e


def whoosh(dur, f_start, f_end, q, rng, rise=0.4, curve=1.0):
    n = int(round(dur * SR))
    x = svf_sweep(pink(n, rng), f_start, f_end, q, curve) * bell_env(n, rise)
    return x / (np.max(np.abs(x)) + 1e-12)


def thump(f_start, f_end, dur, rng, drop_ms=40, drive=1.4):
    """Soft low body: a sine that settles in pitch, gently saturated."""
    n = int(round(dur * SR))
    t = np.arange(n) / SR
    f = f_end + (f_start - f_end) * np.exp(-t / (drop_ms / 1000))
    ph = TAU * np.cumsum(f) / SR
    x = np.sin(ph) * np.exp(-t / (dur / 4.0))
    x = np.tanh(drive * x) / np.tanh(drive)
    return ramp_in(x, 1.2)


def rustle(dur, lo, hi, grains, rng, grain_ms=(4, 14)):
    """Sparse crackle of noise grains: grass, cloth, paper, dust."""
    n = int(round(dur * SR))
    out = np.zeros(n)
    times = np.sort(rng.beta(1.6, 2.8, grains)) * dur * 0.85
    for tt in times:
        gl = rng.uniform(*grain_ms) / 1000
        g = rng.standard_normal(int(gl * SR)) * np.hanning(int(gl * SR))
        at(out, g * rng.uniform(0.35, 1.0), tt)
    out = bandpass(out, lo, hi)
    out *= bell_env(n, 0.15, 1.0, 1.5)
    return out / (np.max(np.abs(out)) + 1e-12)


def pan(x, p):
    """Constant-power pan, p in -1..1 -> (n, 2)."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def room_ir(rng, t60=0.55, predelay_ms=9, bright=0.5):
    """Small airy room: decorrelated stereo tail with band-dependent decay and a few early reflections."""
    n = int(t60 * 1.3 * SR)
    t = np.arange(n) / SR
    chans = []
    for _ in range(2):
        w = rng.standard_normal(n)
        lo = lowpass(w, 500) * np.exp(-6.91 * t / t60)
        mid = bandpass(w, 500, 4000) * np.exp(-6.91 * t / (t60 * 0.8))
        hi = highpass(w, 4000) * np.exp(-6.91 * t / (t60 * (0.3 + 0.35 * bright)))
        tail = 0.7 * lo + mid + (0.5 + bright) * hi
        er = np.zeros(n)
        for d in rng.uniform(0.004, 0.035, 7):
            er[int(d * SR)] += rng.uniform(0.2, 0.55) * rng.choice([-1, 1])
        ir = np.concatenate([np.zeros(int(predelay_ms / 1000 * SR)), er + 0.35 * tail])[:n]
        chans.append(ir / np.sqrt(np.sum(ir ** 2)))
    return np.stack(chans, axis=1)


def add_room(stereo, rng, wet=0.12, **kw):
    if wet <= 0:
        return stereo
    ir = room_ir(rng, **kw)
    tail = np.stack([signal.fftconvolve(stereo[:, c], ir[:, c]) for c in range(2)], axis=1)
    out = np.zeros_like(tail)
    out[:len(stereo)] += stereo
    return out + wet * tail


# ---- loudness: K-weighting (ITU-R BS.1770) and a short 150 ms momentary window for short cues

def _biquad_shelf(fs):
    G, Q, fc = 3.999843853973347, 0.7071752369554196, 1681.974450955533
    K = np.tan(np.pi * fc / fs)
    Vh = 10 ** (G / 20)
    Vb = Vh ** 0.4996667741545416
    a0 = 1 + K / Q + K * K
    b = [(Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0]
    a = [1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0]
    return b, a


def _biquad_hp(fs):
    Q, fc = 0.5003270373238773, 38.13547087602444
    K = np.tan(np.pi * fc / fs)
    a0 = 1 + K / Q + K * K
    return [1, -2, 1], [1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0]


def kweight(x, fs=SR):
    b, a = _biquad_shelf(fs)
    y = signal.lfilter(b, a, x, axis=0)
    b, a = _biquad_hp(fs)
    return signal.lfilter(b, a, y, axis=0)


def loudness150(stereo, fs=SR):
    """Max K-weighted loudness over 150 ms windows (LUFS-like, dB). Short cues are judged by their loudest moment."""
    if stereo.ndim == 1:
        stereo = stereo[:, None]
    y = kweight(stereo, fs)
    p = np.sum(y ** 2, axis=1)
    w = int(0.15 * fs)
    if len(p) < w:
        p = np.pad(p, (0, w - len(p)))
    c = np.convolve(p, np.ones(w) / w, mode='valid')
    return -0.691 + 10 * np.log10(np.max(c) + 1e-20)


def finish(stereo, target_lufs=-18.0, peak_db=-1.0, hp=70, tail_db=-62):
    """High-pass the mud, trim the silent tail, fade, and set loudness with a peak ceiling."""
    y = np.stack([highpass(stereo[:, c], hp) for c in range(2)], axis=1)
    env = np.max(np.abs(y), axis=1)
    thr = np.max(env) * 10 ** (tail_db / 20)
    last = np.nonzero(env > thr)[0]
    end = min(len(y), (last[-1] if len(last) else len(y)) + int(0.01 * SR))
    y = y[:end]
    fade = min(int(0.03 * SR), len(y) // 3)
    y[-fade:] *= (0.5 + 0.5 * np.cos(np.linspace(0, np.pi, fade)))[:, None]
    g = 10 ** ((target_lufs - loudness150(y)) / 20)
    y *= g
    pk = np.max(np.abs(y))
    ceil = 10 ** (peak_db / 20)
    if pk > ceil:
        y *= ceil / pk
    return y
