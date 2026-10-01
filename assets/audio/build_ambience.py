"""Ambience beds: field recordings turned into seamless 44.1 kHz loops at -30 LUFS short-term median.

    python assets/audio/build_ambience.py <out_dir> meadow=<wav> shimmer=<wav>

Sources (Sonniss #GameAudioGDC 2026 bundle, licence in assets/audio/licenses; the recordings themselves are
not kept in this public repository, the bundle licence forbids passing them on as sound effects):
  meadow  = part 3, Just Sound Effects - Highlands of Norway / "AMBSwmp_Meadow Pipits calling many Insects
            humming Wind blowing through Grass_JSE_HoN_Stereo.wav"
  shimmer = part 2, Epic Stock Media - Strange Game Ambient Loops 3 / "MAGShim_Shimmer Loop Small Bell Metal
            Taps_ESM_SGA3.wav"
Unity takes the .ogg (Vorbis, near-lossless) into Assets/Game/Audio/Resources/Ambience."""
import os
import sys
import numpy as np
import soundfile as sf
from scipy import signal
from sfxlib import SR, kweight, highpass

TARGET = -30.0   # median short-term loudness of a bed before the in-game level
XFADE = 3.0


def short_term_median(x):
    y = kweight(x, SR)
    p = np.sum(y ** 2, axis=1)
    w = 3 * SR
    st = [p[i:i + w].mean() for i in range(0, max(1, len(p) - w), w)]
    return -0.691 + 10 * np.log10(np.median(st) + 1e-20)


def loop(x, xf):
    n = int(xf * SR)
    t = np.linspace(0, np.pi / 2, n)[:, None]
    out = x[:len(x) - n].copy()
    out[:n] = x[:n] * np.sin(t) + x[len(x) - n:] * np.cos(t)
    return out


def prep(src, start=None, end=None):
    x, sr = sf.read(src, always_2d=True)
    x = x[:, :2]
    if start is not None:
        x = x[int(start * sr):int(end * sr)]
    if sr != SR:
        g = np.gcd(SR, sr)
        x = signal.resample_poly(x, SR // g, sr // g, axis=0)
    x = np.stack([highpass(x[:, c], 40) for c in range(2)], axis=1)
    x = loop(x, XFADE)
    x *= 10 ** ((TARGET - short_term_median(x)) / 20)
    return np.clip(x, -0.99, 0.99)


def write_ogg(path, y, block=8192):
    # libsndfile's Vorbis encoder crashes on one huge write; feed it in blocks
    with sf.SoundFile(path, 'w', SR, y.shape[1], format='OGG', subtype='VORBIS', compression_level=0.1) as f:
        for i in range(0, len(y), block):
            f.write(y[i:i + block])


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for spec in sys.argv[2:]:
        name, src = spec.split('=', 1)
        rng = None
        if '@' in src:
            src, r = src.rsplit('@', 1)
            rng = tuple(float(v) for v in r.split('-'))
        y = prep(src, *(rng or ()))
        write_ogg(os.path.join(out, name + '.ogg'), y)
        print(f'{name:14} {len(y) / SR:5.1f}s loop')
