"""Procedural background music: the menu loop, the fight-night loop and the victory loop.

Same sample rate and WAV layout as the sound effects, so the game loads music
through exactly the same path as every other sound. Each track is a whole number
of bars and every voice has decayed by the final beat, so the loop point lands in
silence rather than on a click.
"""
from __future__ import annotations

import math
import os
import random
import struct
import wave

RATE = 22050

_SEMITONES = {
    "C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5,
    "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11,
}


def _f(note: str) -> float:
    """Frequency of a note name like A2 or F#3, in equal temperament."""
    name, octave = note[:-1], int(note[-1])
    midi = (octave + 1) * 12 + _SEMITONES[name]
    return 440.0 * 2 ** ((midi - 69) / 12)


def _write(path: str, samples: list[float]) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    frames = bytearray()
    for s in samples:
        frames += struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32000))
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(bytes(frames))


def _noise(n: int, seed: int) -> list[float]:
    rng = random.Random(seed)
    return [rng.uniform(-1, 1) for _ in range(n)]


def _lowpass(sig: list[float], cutoff: float) -> list[float]:
    alpha = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out = []
    prev = 0.0
    for s in sig:
        prev += alpha * (s - prev)
        out.append(prev)
    return out


def _blank(seconds: float) -> list[float]:
    return [0.0] * int(RATE * seconds)


def _mix(target: list[float], source: list[float], at: float) -> None:
    start = int(at * RATE)
    for i, value in enumerate(source):
        j = start + i
        if 0 <= j < len(target):
            target[j] += value


def _voice(freq: float, seconds: float, amp: float, attack: float = 0.01, release: float = 0.09,
           partials: tuple = ((1.0, 1.0), (2.0, 0.3), (3.0, 0.12)), detune: float = 0.0,
           decay: float = 0.0) -> list[float]:
    """One note: a few sine partials under an attack/release envelope."""
    n = int(RATE * seconds)
    a = max(1, int(attack * RATE))
    r = max(1, int(release * RATE))
    out = []
    for i in range(n):
        t = i / RATE
        s = 0.0
        for mult, pamp in partials:
            s += math.sin(2 * math.pi * freq * mult * t) * pamp
        if detune:
            s += math.sin(2 * math.pi * freq * (1 + detune) * t) * 0.35
        if decay:
            s *= math.exp(-decay * t)
        if i < a:
            s *= i / a
        elif i > n - r:
            s *= max(0.0, (n - i) / r)
        out.append(s * amp)
    return out


def _pad(freq: float, seconds: float, amp: float) -> list[float]:
    """Slow swell for the sustained chord tones."""
    return _voice(freq, seconds, amp, attack=0.45, release=0.6,
                  partials=((1.0, 1.0), (2.0, 0.22), (3.0, 0.08)), detune=0.004)


def _kick(seconds: float, amp: float, start: float = 118.0, end: float = 46.0) -> list[float]:
    n = int(RATE * seconds)
    out = []
    for i in range(n):
        t = i / RATE
        freq = end + (start - end) * math.exp(-26 * t)
        out.append(math.sin(2 * math.pi * freq * t) * math.exp(-11 * t) * amp)
    return out


def _snare(seconds: float, amp: float, seed: int) -> list[float]:
    n = int(RATE * seconds)
    body = _lowpass(_noise(n, seed), 1800)
    crack = _noise(n, seed + 1)
    out = []
    for i in range(n):
        t = i / RATE
        out.append((body[i] * 1.4 + crack[i] * 0.5) * math.exp(-30 * t) * amp)
    return out


def _hat(seconds: float, amp: float, seed: int) -> list[float]:
    n = int(RATE * seconds)
    hiss = _noise(n, seed)
    low = _lowpass(hiss, 5000)
    out = []
    for i in range(n):
        t = i / RATE
        out.append((hiss[i] - low[i]) * math.exp(-70 * t) * amp)
    return out


def _normalise(samples: list[float], peak: float) -> list[float]:
    top = max(1e-6, max(abs(v) for v in samples))
    return [v / top * peak for v in samples]


def menu() -> list[float]:
    """The hub, the records and the walk to the ring: slow, minor, patient."""
    bpm = 92.0
    bar = 4 * 60.0 / bpm
    chords = [
        ("A2", ("C4", "E4")),
        ("F2", ("A3", "C4")),
        ("C3", ("E4", "G4")),
        ("G2", ("B3", "D4")),
    ]
    out = _blank(bar * len(chords))

    for index, (bass, tones) in enumerate(chords):
        at = index * bar
        for tone in tones:
            _mix(out, _pad(_f(tone), bar * 0.98, 0.16), at)

        for step in range(8):
            beat = at + step * bar / 8
            amp = 0.26 if step % 4 == 0 else 0.17
            _mix(out, _voice(_f(bass), bar / 8 * 0.92, amp, attack=0.008, release=0.07), beat)
            _mix(out, _hat(0.05, 0.10 if step % 2 == 0 else 0.06, seed=200 + index * 8 + step), beat)

        _mix(out, _kick(0.32, 0.55), at)
        _mix(out, _kick(0.32, 0.42), at + bar / 2)

        if index == 2:
            # A sparse motif so the loop is not just a bed. It sits in the middle
            # of the track and is over well before the bar line, which is what
            # keeps the loop point silent.
            motif = [("E4", 0.25), ("G4", 0.25), ("A4", 0.5)]
            cursor = at + bar * 0.25
            for note, length in motif:
                _mix(out, _voice(_f(note), bar * length * 0.9, 0.15, attack=0.03, release=0.18,
                                 partials=((1.0, 1.0), (2.0, 0.18))), cursor)
                cursor += bar * length * 0.5

    return _normalise(out, 0.8)


def fight() -> list[float]:
    """Fight night: eight bars of kick, bass and a two-note stab."""
    bpm = 138.0
    bar = 4 * 60.0 / bpm
    chords = [
        ("D2", ("D4", "F4")),
        ("D2", ("D4", "F4")),
        ("A#2", ("D4", "F4")),
        ("A#2", ("D4", "F4")),
        ("F2", ("C4", "F4")),
        ("F2", ("C4", "F4")),
        ("C3", ("E4", "G4")),
        ("C3", ("E4", "G4")),
    ]
    out = _blank(bar * len(chords))

    for index, (bass, tones) in enumerate(chords):
        at = index * bar
        for tone in tones:
            _mix(out, _pad(_f(tone), bar * 0.9, 0.10), at)

        # Pumping eighths, accent on the beat.
        for step in range(8):
            beat = at + step * bar / 8
            amp = 0.34 if step % 2 == 0 else 0.22
            _mix(out, _voice(_f(bass), bar / 8 * 0.85, amp, attack=0.004, release=0.05,
                             partials=((1.0, 1.0), (2.0, 0.45), (3.0, 0.2))), beat)
            _mix(out, _hat(0.045, 0.12 if step % 2 == 0 else 0.07, seed=500 + index * 8 + step), beat)

        for step in range(4):
            _mix(out, _kick(0.24, 0.7), at + step * bar / 4)
            if step in (1, 3):
                _mix(out, _snare(0.18, 0.42, seed=700 + index * 4 + step), at + step * bar / 4)

        if index % 4 == 3:
            # A stab to close each half - it lands on the loop seam every other time.
            for offset in (0.0, 0.5):
                _mix(out, _voice(_f(tones[0]), bar * 0.22, 0.26, attack=0.006, release=0.12,
                                 partials=((1.0, 1.0), (2.0, 0.5), (4.0, 0.2)), decay=3.5),
                     at + bar * offset)

    return _normalise(out, 0.82)


def victory() -> list[float]:
    """The award ceremony: major, bright and short, with the belt going up."""
    bpm = 112.0
    bar = 4 * 60.0 / bpm
    chords = [
        ("C3", ("E4", "G4", "C5")),
        ("G2", ("D4", "G4", "B4")),
        ("A2", ("E4", "A4", "C5")),
        ("F2", ("C4", "F4", "A4")),
    ]
    out = _blank(bar * len(chords))

    for index, (bass, tones) in enumerate(chords):
        at = index * bar
        for tone in tones:
            _mix(out, _pad(_f(tone), bar * 0.95, 0.12), at)

        for step in range(4):
            beat = at + step * bar / 4
            _mix(out, _voice(_f(bass), bar / 4 * 0.9, 0.24, attack=0.006, release=0.07), beat)
            _mix(out, _kick(0.26, 0.5 if step % 2 == 0 else 0.34), beat)
            _mix(out, _hat(0.05, 0.10 if step % 2 == 0 else 0.06, seed=900 + index * 4 + step), beat)

        if index == 0:
            # The four-note fanfare the result screen plays, in the loop's own key.
            for offset, note in enumerate(("C5", "E5", "G5", "C6")):
                _mix(out, _voice(_f(note), bar * 0.4, 0.2, attack=0.01, release=0.2,
                                 partials=((1.0, 1.0), (2.0, 0.35), (3.0, 0.12)), decay=2.6),
                     at + offset * bar * 0.16)

    return _normalise(out, 0.8)


def build_all(out_dir: str) -> list[str]:
    files = {
        "music_menu.wav": menu(),
        "music_fight.wav": fight(),
        "music_victory.wav": victory(),
    }

    written = []
    for name, samples in files.items():
        path = os.path.join(out_dir, name)
        _write(path, samples)
        written.append(path)
    return written
