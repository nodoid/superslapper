"""Procedural sound effects: slaps, crowd reactions, applause, bell and UI clicks."""
from __future__ import annotations

import math
import os
import random
import struct
import wave

RATE = 22050


def _write(path: str, samples: list[float]) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    frames = bytearray()
    for s in samples:
        v = max(-1.0, min(1.0, s))
        frames += struct.pack("<h", int(v * 32000))
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(bytes(frames))


def _noise(n: int, seed: int) -> list[float]:
    rng = random.Random(seed)
    return [rng.uniform(-1, 1) for _ in range(n)]


def _lowpass(sig: list[float], cutoff: float) -> list[float]:
    """One-pole low pass."""
    alpha = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out = []
    prev = 0.0
    for s in sig:
        prev += alpha * (s - prev)
        out.append(prev)
    return out


def _highpass(sig: list[float], cutoff: float) -> list[float]:
    low = _lowpass(sig, cutoff)
    return [a - b for a, b in zip(sig, low)]


def _env(n: int, attack: float, decay: float, power: float = 2.0) -> list[float]:
    a = max(1, int(attack * RATE))
    env = []
    for i in range(n):
        if i < a:
            env.append(i / a)
        else:
            t = (i - a) / max(1, n - a)
            env.append(max(0.0, (1 - t) ** power) * math.exp(-decay * t))
    return env


def slap(heavy: bool = False, seed: int = 1) -> list[float]:
    duration = 0.34 if heavy else 0.20
    n = int(RATE * duration)
    noise = _noise(n, seed)
    body = _lowpass(noise, 900 if heavy else 1600)
    crack = _highpass(noise, 3200)
    env = _env(n, 0.002, 6.0 if heavy else 9.0, 2.4)
    out = []
    freq = 118 if heavy else 176
    for i in range(n):
        t = i / RATE
        thump = math.sin(2 * math.pi * freq * t) * math.exp(-28 * t) * (1.4 if heavy else 0.9)
        s = (body[i] * 1.5 + crack[i] * 0.9 + thump) * env[i]
        out.append(s * (0.95 if heavy else 0.8))
    return out


def whiff(seed: int = 2) -> list[float]:
    n = int(RATE * 0.24)
    noise = _noise(n, seed)
    sweep = []
    prev = 0.0
    for i in range(n):
        cutoff = 400 + 2600 * (i / n)
        alpha = 1 - math.exp(-2 * math.pi * cutoff / RATE)
        prev += alpha * (noise[i] - prev)
        sweep.append(prev)
    env = _env(n, 0.03, 3.0, 1.6)
    return [s * e * 0.6 for s, e in zip(sweep, env)]


def crowd(seconds: float, seed: int, boo: bool = False) -> list[float]:
    n = int(RATE * seconds)
    noise = _noise(n, seed)
    base = _lowpass(noise, 700 if boo else 1800)
    if boo:
        base = _lowpass(base, 500)
    shimmer = _highpass(_noise(n, seed + 4), 2400 if not boo else 1200)
    out = []
    for i in range(n):
        t = i / RATE
        # Slow swell, like a crowd reacting.
        swell = math.sin(math.pi * min(1.0, t / (seconds * 0.35))) 
        wave_lfo = 0.55 + 0.45 * math.sin(2 * math.pi * (0.7 if boo else 1.6) * t + math.sin(t * 3.1))
        tone = 0.0
        if boo:
            tone = 0.16 * math.sin(2 * math.pi * (110 + 12 * math.sin(t * 5)) * t)
        s = (base[i] * 1.4 + shimmer[i] * (0.25 if not boo else 0.12) + tone) * swell * wave_lfo
        out.append(s * (0.55 if boo else 0.6))
    # Fade the tail.
    fade = int(RATE * 0.6)
    for i in range(fade):
        out[n - 1 - i] *= i / fade
    return out


def applause(seconds: float, seed: int, density: float = 1.0) -> list[float]:
    n = int(RATE * seconds)
    out = [0.0] * n
    rng = random.Random(seed)
    claps = int(seconds * 340 * density)
    for _ in range(claps):
        start = rng.randint(0, n - 1)
        length = int(RATE * rng.uniform(0.012, 0.03))
        amp = rng.uniform(0.15, 0.5)
        for k in range(length):
            if start + k >= n:
                break
            out[start + k] += rng.uniform(-1, 1) * amp * math.exp(-k / (length * 0.35))
    out = _highpass(out, 900)
    # Gentle overall swell so it sounds like a room, not a machine.
    for i in range(n):
        t = i / n
        out[i] *= math.sin(math.pi * min(1.0, t * 1.6)) * 0.9
    peak = max(1e-6, max(abs(v) for v in out))
    return [v / peak * 0.75 for v in out]


def bell(seed: int = 3, pitch: float = 1.0) -> list[float]:
    n = int(RATE * 1.8)
    out = []
    partials = [(1.0, 1.0), (2.02, 0.5), (3.01, 0.28), (4.7, 0.16), (6.1, 0.09)]
    for i in range(n):
        t = i / RATE
        s = 0.0
        for mult, amp in partials:
            s += math.sin(2 * math.pi * 640 * pitch * mult * t) * amp * math.exp(-2.4 * t)
        strike = math.exp(-90 * t) * 0.4
        out.append((s * 0.5 + strike) * 0.8)
    return out


def fanfare(seed: int = 4) -> list[float]:
    notes = [392.0, 523.25, 659.25, 784.0]
    n = int(RATE * 1.9)
    out = [0.0] * n
    for index, freq in enumerate(notes):
        start = int(index * 0.16 * RATE)
        length = int(RATE * 0.55)
        for k in range(length):
            if start + k >= n:
                break
            t = k / RATE
            env = math.exp(-3.2 * t) * min(1.0, t * 80)
            s = (math.sin(2 * math.pi * freq * t) * 0.6 +
                 math.sin(2 * math.pi * freq * 2 * t) * 0.22 +
                 math.sin(2 * math.pi * freq * 3 * t) * 0.1)
            out[start + k] += s * env * 0.35
    return out


def click(seed: int = 5, up: bool = False) -> list[float]:
    n = int(RATE * 0.09)
    freq = 880 if up else 560
    return [math.sin(2 * math.pi * freq * (i / RATE)) * math.exp(-38 * (i / RATE)) * 0.5 for i in range(n)]


def impact(seed: int = 6) -> list[float]:
    heavy = slap(heavy=True, seed=seed)
    swell = crowd(1.2, seed + 1)
    out = list(heavy) + [0.0] * (len(swell) - len(heavy))
    for i, v in enumerate(swell):
        out[i] = out[i] + v * 0.55
    return out


def build_all(out_dir: str) -> list[str]:
    files = {
        "slap_light.wav": slap(False, 1),
        "slap_heavy.wav": slap(True, 2),
        "whiff.wav": whiff(3),
        "crowd_cheer.wav": crowd(2.8, 4),
        "crowd_boo.wav": crowd(2.4, 5, boo=True),
        "applause.wav": applause(3.4, 6, 1.0),
        "applause_wild.wav": applause(5.0, 7, 1.6),
        "bell.wav": bell(8, 1.0),
        "bell_end.wav": bell(9, 0.75),
        "fanfare.wav": fanfare(10),
        "ui_click.wav": click(11, False),
        "ui_confirm.wav": click(12, True),
        "ko.wav": impact(13),
    }
    written = []
    for name, samples in files.items():
        path = os.path.join(out_dir, name)
        _write(path, samples)
        written.append(path)
    return written
