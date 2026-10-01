"""Feedback cues for TrollStrategy: struck wood, kalimba, marimba, glockenspiel, wind chimes, small coins,
cloth and breath, tuned to F major pentatonic (the key of the YannZ music). Modal synthesis, no samples.

    python assets/audio/build_sfx.py [out_dir] [cue ...]

Writes name.wav, name_2.wav, ... (24-bit, 44.1 kHz, every take at -18 LUFS over its loudest 150 ms) into
unity/TrollStategy/Assets/Game/Audio/Resources/Sfx by default, then prints the GameAudio level of every cue
from the in-game loudness targets below. Renders are repeatable: each take is seeded by its name.
See docs/audio-direction.md."""
import os
import sys
import zlib
import numpy as np
import soundfile as sf
from sfxlib import *

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.join(HERE, '..', '..', 'unity', 'TrollStategy', 'Assets', 'Game', 'Audio', 'Resources', 'Sfx')
# in-game loudness of each cue (LUFS over its loudest 150 ms) with the music at -28 LUFS underneath
TARGETS = {"ui_hover": -43,
           "ui_click": -35,
           "ui_back": -36,
           "ui_denied": -34,
           "select": -35,
           "pause": -34,
           "unpause": -34,
           "equip": -34,
           "unequip": -35,
           "coins": -31,
           "land": -31,
           "build": -30,
           "spawn": -30,
           "upgrade": -28,
           "demolish": -31,
           "step": -44,
           "swing": -33,
           "throw": -34,
           "hit": -31,
           "hit_heavy": -29,
           "block": -32,
           "death": -30,
           "battle_start": -24,
           "victory": -22,
           "defeat": -25}


# ---- instruments -----------------------------------------------------------------------------------


def kalimba(note, rng, dur=0.8, hard=0.55, t60=0.6):
    f = hz(note) if isinstance(note, str) else note
    x = modes(f, [1, 6.12, 2.01, 16.9], [1, .3, .045, .03], t60, dur, rng, hardness=hard, tilt=1.35,
              glide=0.0035, onset_ms=0.6)
    return x + at(buf(dur), contact(0.03, 2500, 11000, 2.2, rng, .10), 0)


def marimba(note, rng, dur=0.9, hard=0.35, t60=0.55):
    f = hz(note) if isinstance(note, str) else note
    x = modes(f, [1, 3.99, 9.8], [1, .26, .06], t60, dur, rng, hardness=hard, tilt=1.1, onset_ms=1.4)
    # tube resonator: the fundamental swells a touch longer than the bar alone
    x += 0.25 * modes(f, [1], [1], t60 * 1.5, dur, rng, hardness=0, detune=0.3, onset_ms=6)
    return x + at(buf(dur), lowpass(contact(0.02, 300, 3000, 3, rng, .08), 2500), 0)


def glock(note, rng, dur=1.4, hard=0.8, t60=1.1):
    f = hz(note) if isinstance(note, str) else note
    x = modes(f, [1, 2.756, 5.404, 8.933], [1, .2, .07, .025], t60, dur, rng, hardness=hard, tilt=0.9,
              onset_ms=0.4)
    return x + at(buf(dur), contact(0.02, 4000, 14000, 1.5, rng, .08), 0)


def wood(f0, rng, dur=0.12, hard=0.7, t60=0.05, noise=0.3):
    x = modes(f0, [1, 2.76, 5.4, 8.9], [1, .38, .14, .05], t60, dur, rng, hardness=hard, tilt=0.7,
              detune=4, onset_ms=0.25)
    return x + noise * contact(dur, 1500, 9000, 1.6, rng)


def glass(f0, rng, dur=0.08, t60=0.035, level=1.0):
    x = modes(f0, [1, 2.32, 4.25, 6.63], [1, .5, .25, .1], t60, dur, rng, hardness=0.95, tilt=0.6,
              detune=3, onset_ms=0.2)
    return level * x


def coin(f0, rng, dur=0.45, t60=0.28):
    x = modes(f0, [1, 1.47, 2.09, 2.56, 3.14, 3.73], [1, .8, .6, .45, .3, .18], t60, dur, rng,
              hardness=0.95, tilt=0.35, detune=6, onset_ms=0.15)
    return x + at(buf(dur), contact(0.02, 5000, 15000, 1.2, rng, .25), 0)


def chimes(rng, dur=1.4, count=5, spread=0.45, notes=('F7', 'G7', 'A7', 'C8', 'D8')):
    """Wind chimes: a few tiny tubes rung at random, high and long; the 'air' over positive cues."""
    out = buf(dur)
    for _ in range(count):
        f = hz(str(rng.choice(notes))) * 2 ** (rng.normal(0, 0.1) / 12)
        tube = modes(f, [1, 2.756, 5.404], [1, .12, .03], rng.uniform(0.9, 1.4), dur, rng, hardness=0.9,
                     tilt=0.8, detune=3, onset_ms=0.3)
        at(out, tube * rng.uniform(0.35, 1.0), rng.uniform(0, spread))
    return out


def breath(dur, rng, lo=3000, hi=12000, rise=0.5):
    """Soft air: high band noise that swells and fades, no pitch."""
    n = int(round(dur * SR))
    x = bandpass(pink(n, rng), lo, hi) * bell_env(n, rise, 1.5, 2.0)
    return x / (np.max(np.abs(x)) + 1e-12)


# ---- helpers ---------------------------------------------------------------------------------------

PENTA = ['F', 'G', 'A', 'C', 'D']


def st(x, p=0.0):
    return pan(x, p)


def mix(dur, *layers):
    """layers: (mono, start, gain, pan)."""
    out = np.zeros((int(round(dur * SR)), 2))
    for mono, start, gain, p in layers:
        s = pan(mono * gain, p)
        i = int(round(start * SR))
        k = min(len(s), len(out) - i)
        if k > 0:
            out[i:i + k] += s[:k]
    return out


def jit(rng, s=0.006):
    return float(rng.uniform(-s, s))


# ---- events ----------------------------------------------------------------------------------------

def ui_hover(rng, v):
    return mix(0.1, (glass(rng.uniform(2900, 3300), rng, 0.08, 0.025), 0, 1, rng.uniform(-.1, .1))), 0


def ui_click(rng, v):
    f = 1550 * 2 ** (rng.uniform(-0.6, 0.6) / 12)
    return mix(0.14, (wood(f, rng, 0.12, hard=0.72, t60=0.045), 0, 1, jit(rng, .08)),
               (glass(f * 3.02, rng, 0.06, 0.02), 0.001, 0.12, 0)), 0


def ui_back(rng, v):
    f = 1120 * 2 ** (rng.uniform(-0.5, 0.5) / 12)
    return mix(0.2, (wood(f, rng, 0.14, hard=0.5, t60=0.06, noise=0.2), 0, 1, 0),
               (whoosh(0.12, 2200, 700, 1.1, rng, rise=0.2), 0.0, 0.12, -0.2)), 0


def ui_denied(rng, v):
    f = 560 * 2 ** (rng.uniform(-0.3, 0.3) / 12)
    return mix(0.3, (wood(f, rng, 0.14, hard=0.45, t60=0.07, noise=0.18), 0, 1, -0.05),
               (wood(f * 0.84, rng, 0.14, hard=0.42, t60=0.08, noise=0.15), 0.095 + jit(rng, .004), 0.85, 0.05)), 0


def equip(rng, v):
    return mix(0.3, (whoosh(0.09, 1100, 3600, 0.9, rng, rise=0.55), 0, 0.55, -0.15),
               (coin(rng.uniform(3000, 3500), rng, 0.2, 0.06), 0.07 + jit(rng, .006), 0.45, 0.1),
               (wood(900, rng, 0.08, hard=0.6, t60=0.03, noise=0.3), 0.07, 0.35, 0)), .06


def unequip(rng, v):
    return mix(0.3, (coin(rng.uniform(2500, 2900), rng, 0.2, 0.05), 0, 0.4, 0.1),
               (wood(800, rng, 0.08, hard=0.55, t60=0.03, noise=0.3), 0, 0.3, 0),
               (whoosh(0.1, 3200, 1000, 0.9, rng, rise=0.3), 0.03, 0.5, -0.15)), .06


def coins(rng, v):
    n = 3
    layers = []
    t = 0.0
    for i in range(n):
        layers.append((coin(rng.uniform(2900, 4300), rng, 0.4, rng.uniform(0.18, 0.3)), t, [1, .7, .5][i],
                       rng.uniform(-.35, .35)))
        t += rng.uniform(0.035, 0.06)
    return mix(0.6, *layers), .12


def pause(rng, v):
    return mix(0.5, (marimba('C6', rng, 0.45, t60=0.3), 0, 1, 0.05),
               (marimba('F5', rng, 0.45, t60=0.35), 0.085, 0.8, -0.05)), .1


def unpause(rng, v):
    return mix(0.5, (marimba('F5', rng, 0.45, t60=0.3), 0, 0.8, -0.05),
               (marimba('C6', rng, 0.45, t60=0.35), 0.085, 1, 0.05)), .1


def select(rng, v):
    # always the tonic: GameAudio.Step() walks the pentatonic scale up from it (reward reels)
    return mix(0.7, (kalimba('F5', rng, 0.65, t60=0.45), 0, 1, jit(rng, .15))), .1


def land(rng, v):
    return mix(0.4, (thump(190, 110, 0.12, rng, drop_ms=22, drive=1.2), 0, 0.45, 0),
               (rustle(0.2, 1400, 7000, 14, rng), 0.004, 0.35, jit(rng, .2)),
               (chimes(rng, 0.9, count=2, spread=0.12), 0.02, 0.05, 0.25)), .06


def build(rng, v):
    a = rng.uniform(640, 700)
    return mix(0.8, (wood(a, rng, 0.16, hard=0.8, t60=0.07, noise=0.35), 0, 1, -0.1),
               (wood(a * 1.12, rng, 0.16, hard=0.8, t60=0.07, noise=0.35), 0.13 + jit(rng, .006), 0.85, 0.1),
               (kalimba('C6', rng, 0.6, t60=0.45), 0.26 + jit(rng, .004), 0.55, 0)), .1


def step(rng, v):
    return mix(0.14, (rustle(0.1, 900, 5500, int(rng.integers(5, 9)), rng, (3, 9)), 0, 0.8, jit(rng, .3)),
               (thump(110, 70, 0.07, rng, drop_ms=15, drive=1.0), 0, 0.25, 0)), 0


def spawn(rng, v):
    return mix(0.8, (whoosh(0.26, 500, 3200, 1.3, rng, rise=0.7, curve=0.8), 0, 0.45, -0.2),
               (kalimba('F5', rng, 0.55, t60=0.4), 0.16 + jit(rng, .004), 0.8, -0.05),
               (kalimba('C6', rng, 0.6, t60=0.5), 0.24 + jit(rng, .004), 0.9, 0.08),
               (breath(0.3, rng, rise=0.75), 0, 0.10, 0.2)), .12


def upgrade(rng, v):
    return mix(1.4, (marimba('F5', rng, 0.9, t60=0.55), 0, 0.55, -0.1),
               (glock('F6', rng, 1.0, t60=0.8), 0.0, 0.55, -0.15),
               (glock('A6', rng, 1.0, t60=0.8), 0.075 + jit(rng, .004), 0.5, 0.0),
               (glock('C7', rng, 1.2, t60=1.0), 0.15 + jit(rng, .004), 0.55, 0.15),
               (chimes(rng, 1.3, count=4, spread=0.35), 0.12, 0.10, 0)), .18


def demolish(rng, v):
    layers = [(thump(170, 90, 0.18, rng, drop_ms=30, drive=1.4), 0, 0.45, 0),
              (lowpass(whoosh(0.5, 2500, 500, 0.8, rng, rise=0.15), 3000), 0.02, 0.35, 0.1)]
    t, f, g = 0.03, 620.0, 0.9
    for _ in range(6):
        layers.append((wood(f * rng.uniform(0.95, 1.05), rng, 0.14, hard=0.65, t60=0.06, noise=0.3), t, g,
                       rng.uniform(-.4, .4)))
        t += rng.uniform(0.045, 0.085)
        f *= 0.88
        g *= 0.78
    return mix(0.8, *layers), .1


def swing(rng, v):
    d = rng.uniform(0.17, 0.22)
    p = rng.choice([-1, 1]) * 0.25
    return mix(0.3, (whoosh(d, rng.uniform(380, 450), rng.uniform(1400, 1800), 1.4, rng, rise=0.55), 0, 1, p)), .03


def throw(rng, v):
    return mix(0.25, (wood(1300, rng, 0.05, hard=0.6, t60=0.02, noise=0.4), 0, 0.3, 0),
               (whoosh(0.15, 700, 2600, 1.6, rng, rise=0.35), 0.01, 1, rng.uniform(-.3, .3))), .03


def hit(rng, v):
    return mix(0.25, (thump(rng.uniform(300, 340), 170, 0.08, rng, drop_ms=12, drive=1.6), 0, 0.7, 0),
               (contact(0.03, 1500, 7000, 3, rng), 0, 0.45, jit(rng, .2)),
               (wood(rng.uniform(380, 430), rng, 0.1, hard=0.55, t60=0.045, noise=0.15), 0, 0.55, 0)), .04


def hit_heavy(rng, v):
    return mix(0.35, (thump(230, 115, 0.14, rng, drop_ms=20, drive=2.0), 0, 0.8, 0),
               (rustle(0.08, 900, 6000, 8, rng, (3, 8)), 0, 0.45, jit(rng, .2)),
               (wood(rng.uniform(270, 300), rng, 0.14, hard=0.6, t60=0.07, noise=0.2), 0.003, 0.6, 0)), .05


def block(rng, v):
    return mix(0.3, (wood(rng.uniform(400, 440), rng, 0.2, hard=0.85, t60=0.11, noise=0.35), 0, 1, jit(rng, .15)),
               (coin(rng.uniform(3300, 3700), rng, 0.12, 0.04), 0.002, 0.25, 0.1)), .06


def death(rng, v):
    return mix(0.9, (lowpass(whoosh(0.35, 2600, 300, 0.9, rng, rise=0.12, curve=0.6), 3500), 0, 0.55, 0),
               (marimba('C5', rng, 0.6, hard=0.25, t60=0.35), 0.03, 0.7, -0.05),
               (marimba('F4', rng, 0.7, hard=0.25, t60=0.45), 0.15 + jit(rng, .005), 0.6, 0.05)), .1


def battle_start(rng, v):
    return mix(1.3, (whoosh(0.5, 300, 2600, 1.2, rng, rise=0.8, curve=0.9), 0, 0.4, 0),
               (thump(95, 60, 0.3, rng, drop_ms=40, drive=1.3), 0.42, 0.45, 0),
               (marimba('F5', rng, 0.8, t60=0.5), 0.42, 0.8, -0.15),
               (marimba('A5', rng, 0.8, t60=0.5), 0.52 + jit(rng, .004), 0.75, 0),
               (marimba('C6', rng, 0.8, t60=0.6), 0.62 + jit(rng, .004), 0.8, 0.15)), .15


def victory(rng, v):
    notes = ['F5', 'A5', 'C6', 'D6', 'F6']
    layers = []
    t = 0.0
    for i, nt in enumerate(notes):
        layers.append((marimba(nt, rng, 0.9, t60=0.5), t, 0.75, -0.3 + 0.15 * i))
        t += 0.085 + jit(rng, .006)
    for nt, g in (('F6', .45), ('A6', .38), ('C7', .35)):
        layers.append((glock(nt, rng, 1.6, t60=1.3), t - 0.085, g, rng.uniform(-.3, .3)))
    layers.append((chimes(rng, 1.6, count=6, spread=0.5), t - 0.05, 0.12, 0))
    return mix(2.0, *layers), .18


def defeat(rng, v):
    layers = []
    t = 0.0
    for nt, g in (('A5', .8), ('F5', .7), ('D5', .65)):
        layers.append((marimba(nt, rng, 0.9, hard=0.25, t60=0.55), t, g, jit(rng, .15)))
        t += 0.15 + jit(rng, .008)
    layers.append((lowpass(whoosh(0.5, 1200, 400, 0.8, rng, rise=0.3), 2000), 0.2, 0.2, 0))
    return mix(1.4, *layers), .15


EVENTS = {
    'ui_hover': (ui_hover, 3), 'ui_click': (ui_click, 4), 'ui_back': (ui_back, 3), 'ui_denied': (ui_denied, 2),
    'equip': (equip, 3), 'unequip': (unequip, 3), 'coins': (coins, 4), 'pause': (pause, 1), 'unpause': (unpause, 1),
    'select': (select, 4), 'land': (land, 4), 'build': (build, 3), 'step': (step, 6), 'spawn': (spawn, 3),
    'upgrade': (upgrade, 2), 'demolish': (demolish, 3), 'swing': (swing, 4), 'throw': (throw, 4),
    'hit': (hit, 4), 'hit_heavy': (hit_heavy, 3), 'block': (block, 3), 'death': (death, 3),
    'battle_start': (battle_start, 1), 'victory': (victory, 1), 'defeat': (defeat, 1),
}


def render(name, v, target=-18.0):
    fn, _ = EVENTS[name]
    rng = np.random.default_rng(zlib.crc32(f'{name}:{v}'.encode()))
    dry, wet = fn(rng, v)
    wet_rng = np.random.default_rng(zlib.crc32(f'room:{name}'.encode()))
    y = add_room(dry, wet_rng, wet=wet, t60=0.6, bright=0.55)
    return finish(y, target_lufs=target)


# Unity encodes WebGL audio as AAC, which fails on clips under 2049 samples; iOS browsers then refuse
# the whole bank ("EncodingError: Decoding failed"). Short cues get trailing silence.
MIN_SAMPLES = 4096


def pad_short(y):
    if len(y) >= MIN_SAMPLES:
        return y
    return np.concatenate([y, np.zeros((MIN_SAMPLES - len(y),) + y.shape[1:], dtype=y.dtype)])


def level_name(cue):
    return ''.join(part.capitalize() for part in cue.split('_'))


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    only = sys.argv[2:] or list(EVENTS)
    os.makedirs(out, exist_ok=True)
    levels = []
    for name in only:
        _, count = EVENTS[name]
        loud = []
        for v in range(count):
            y = render(name, v)
            loud.append(loudness150(y))
            fname = f'{name}.wav' if v == 0 else f'{name}_{v + 1}.wav'
            sf.write(os.path.join(out, fname), pad_short(y), SR, subtype='PCM_24')
        level = 10 ** ((TARGETS[name] - float(np.mean(loud))) / 20)
        levels.append(f'[Sfx.{level_name(name)}] = {level:.2f}f')
        print(f'{name:13} {count} takes  level {level:.2f}')
    print('GameAudio.Levels:', ', '.join(levels))
