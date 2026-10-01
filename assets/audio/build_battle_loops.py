"""Battle loops from YannZ "Indie Meditations" (CC BY 4.0). The pack's loops end mid-sound and start from
silence, so a looping AudioSource clicks at the seam; a 4 ms fade-out and 1 ms fade-in turn the click into a
tiny dip. The length stays 3 880 800 samples (44 bars at 120 BPM). Colony tracks play once and are used as
shipped (Soundscape fades them out before their end).

    python assets/audio/build_battle_loops.py "<pack>/Audio/WAV/lvl 1 (the royal palace).wav" <Music>/yannz_royal_palace.ogg
    python assets/audio/build_battle_loops.py "<pack>/Audio/WAV/lvl 8 (the volcanic sea shore).wav" <Music>/yannz_volcanic_shore.ogg"""
import sys
import numpy as np
import soundfile as sf

FADE_OUT_MS, FADE_IN_MS = 4.0, 1.0

src, dst = sys.argv[1:3]
x, sr = sf.read(src, always_2d=True)
n_out, n_in = int(sr * FADE_OUT_MS / 1000), int(sr * FADE_IN_MS / 1000)
x[-n_out:] *= (np.cos(np.linspace(0, np.pi / 2, n_out)) ** 2)[:, None]
x[:n_in] *= (np.sin(np.linspace(0, np.pi / 2, n_in)) ** 2)[:, None]
with sf.SoundFile(dst, 'w', sr, x.shape[1], format='OGG', subtype='VORBIS', compression_level=0.0) as f:
    for i in range(0, len(x), 8192):
        f.write(x[i:i + 8192])
y, _ = sf.read(dst, always_2d=True)
assert len(y) == len(x), (len(y), len(x))
m = y[:, 0]
ref = np.median(np.abs(np.diff(np.concatenate([m[-200:-100], m[100:200]])))) + 1e-9
print(f'{dst.rsplit("/", 1)[-1]}: {len(y)} frames, seam {abs(m[0] - m[-1]):.5f} (local step {ref:.5f}), end {m[-3:].round(5)}')
