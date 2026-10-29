#!/usr/bin/env python3
"""Records the store gameplay video from the macOS build, on the desktop.

The desktop app is launched with the scripted demo switched on, the game's own
window is filmed while it plays, and the take is cut down to one app preview:
15-30 seconds of 1920x1080 footage at 30 fps, H.264 High Profile, with the game's
music under it.

The cut is shared with the iOS recorder: ``tools/storegen/video.py`` owns the edit
and the beat log parsing, and this script only differs in how the picture is
filmed and in the size the file has to be.

    python3 tools/storegen/mac_video.py                # record, cut, write ~/Movies
    python3 tools/storegen/mac_video.py --build        # build the desktop app first
    python3 tools/storegen/mac_video.py --sheet        # also write a contact sheet

Needs the desktop build, Xcode's command line tools (for the window list) and
ffmpeg on the path.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import video as shared  # noqa: E402  (the shared edit, log parser and music bed)

APP = "src/SuperSlapper.Desktop/bin/Debug/net10.0/SuperSlapper"
BUNDLE_OWNER = "SuperSlapper"
MOVIES = os.path.expanduser("~/Movies")
LOG_NAME = "demo.log"

# The Mac app preview size: the game renders a 1920x1080 canvas, so the file is
# that resolution rather than a phone's. Apple's Mac previews are landscape only.
WIDTH, HEIGHT = 1920, 1080

# The window's title bar, in points. The client area below it is the game. The
# recorder is asked for the window without its shadow (-o), so the picture starts
# at the window's own edge.
TITLE_BAR = 28
SHADOW = 0


class RecordError(RuntimeError):
    """The take or the window did not give the script what it needs."""


def run(command: list[str], **kwargs) -> subprocess.CompletedProcess:
    kwargs.setdefault("check", True)
    return subprocess.run(command, text=True, **kwargs)


def helper(directory: str) -> str:
    """Compiles the window-list helper once, into the work folder."""
    source = os.path.join(HERE, "windows.swift")
    binary = os.path.join(directory, "windows")
    run(["swiftc", "-O", source, "-o", binary])
    return binary


def game_window(binary: str) -> dict | None:
    """The game's window as the window server sees it, or None before it exists."""
    report = json.loads(run([binary], capture_output=True).stdout)
    for window in report["windows"]:
        if window["owner"] == BUNDLE_OWNER and window["width"] > 200:
            return window

    return None


def launch(app: str, log: str) -> subprocess.Popen:
    return subprocess.Popen(
        [app, "--demo", "--demo-log", log],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def film(binary: str, out: str, log: str, app: str, timeout: float) -> tuple[str, dt.datetime]:
    """Films the game window while the demo plays, and returns the take and start time."""
    game = launch(app, log)

    # Filmed by window id, not by screen rectangle: the window is captured whether
    # or not something else is in front of it, which a region of the screen is not.
    window = None
    for _ in range(120):
        time.sleep(0.15)
        window = game_window(binary)
        if window is not None:
            break

    if window is None:
        game.terminate()
        raise RecordError("the game window never appeared")

    print(f"recording window {window['id']} ({window['width']:.0f}x{window['height']:.0f}) ...")

    # No -V: a recording stopped by SIGINT is finalised, while one with a fixed
    # length aborts on the signal and leaves no file behind.
    recorder = subprocess.Popen(
        ["screencapture", "-x", "-o", "-v", f"-l{window['id']}", out],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    started = dt.datetime.now(dt.timezone.utc)

    try:
        deadline = time.time() + timeout
        while time.time() < deadline:
            time.sleep(0.5)
            if not os.path.exists(log):
                continue

            with open(log, encoding="utf-8") as handle:
                text = handle.read()

            if "[demo] complete" in text:
                time.sleep(2.5)  # a moment on the last screen to cut to
                break
        else:
            raise RecordError(f"the demo did not finish in {timeout:.0f}s")

        recorder.send_signal(signal.SIGINT)
        recorder.wait(timeout=60)
    finally:
        game.terminate()
        try:
            game.wait(timeout=10)
        except subprocess.TimeoutExpired:
            game.kill()

    if not os.path.exists(out):
        raise RecordError("the screen recorder wrote no take")

    with open(log, encoding="utf-8") as handle:
        return handle.read(), started


def assemble(windows: list[tuple[float, float]], take: str, out: str) -> float:
    """Cuts the windows out of the take, joins them, and scores the result."""
    filters = []
    labels = []
    for index, (start, end) in enumerate(windows):
        # Constant frame rate before the cut: the screen recorder's take is variable,
        # and trimming one of those ends each segment on its last real frame.
        filters.append(
            f"[0:v]fps={shared.FRAMES},trim=start={start:.3f}:end={end:.3f},"
            f"setpts=PTS-STARTPTS[v{index}]")
        labels.append(f"[v{index}]")

    filters.append(f"{''.join(labels)}concat=n={len(windows)}:v=1:a=0[vconcat]")
    # The window recording carries the title bar and a shadow margin; only the
    # game's 16:9 picture inside it is kept.
    size = json.loads(run(["ffprobe", "-v", "error", "-select_streams", "v:0",
                           "-show_entries", "stream=width,height", "-of", "json", take],
                          capture_output=True).stdout)["streams"][0]
    client_width = size["width"] - SHADOW * 2
    client_height = size["height"] - SHADOW * 2 - TITLE_BAR
    content_width = int(round(client_height * 16 / 9))
    left = SHADOW + (client_width - content_width) // 2
    top = SHADOW + TITLE_BAR

    filters.append(f"[vconcat]crop={content_width}:{client_height}:{left}:{top},"
                   f"scale={WIDTH}:{HEIGHT}:flags=lanczos,setsar=1[vout]")

    bout_index = next(i for i, segment in enumerate(shared.EDIT) if segment[0].startswith("bout:"))
    bout_at = sum(end - start for start, end in windows[:bout_index])
    total = sum(end - start for start, end in windows)
    menu_length = bout_at + shared.CROSSFADE / 2
    fight_length = total - bout_at + shared.CROSSFADE / 2

    audio = (
        f"[1:a]atrim=0:{menu_length:.3f},asetpts=N/SR/TB,afade=t=in:d=0.4[m];"
        f"[2:a]atrim=0:{fight_length:.3f},asetpts=N/SR/TB[f];"
        f"[m][f]acrossfade=d={shared.CROSSFADE}:c1=tri:c2=tri,"
        f"afade=t=out:st={max(0.0, total - 1.5):.3f}:d=1.5[aout]"
    )

    run([
        "ffmpeg", "-y", "-v", "error",
        "-i", take,
        "-stream_loop", "4", "-i", shared.MENU_TRACK,
        "-stream_loop", "4", "-i", shared.FIGHT_TRACK,
        "-filter_complex", ";".join(filters + [audio]),
        "-map", "[vout]", "-map", "[aout]",
        "-c:v", "libx264", "-preset", "slow", "-profile:v", "high", "-level", "4.0",
        "-crf", str(shared.QUALITY), "-maxrate", "12M", "-bufsize", "22M",
        "-pix_fmt", "yuv420p", "-r", str(shared.FRAMES), "-g", str(shared.FRAMES),
        "-c:a", "aac", "-b:a", shared.AUDIO_RATE, "-ar", shared.SAMPLE_RATE, "-ac", "2",
        "-movflags", "+faststart",
        out,
    ])

    return total


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", default=APP, help="the built desktop app to run")
    parser.add_argument("--out", help="default: ~/Movies/SuperSlapper - macOS gameplay.mp4")
    parser.add_argument("--build", action="store_true", help="build the desktop app first")
    parser.add_argument("--timeout", type=float, default=180, help="seconds to film for")
    parser.add_argument("--sheet", action="store_true", help="also write a contact sheet")
    args = parser.parse_args()

    if args.build:
        run(["dotnet", "build", "src/SuperSlapper.Desktop/SuperSlapper.Desktop.csproj",
             "-f", "net10.0"])

    if not os.path.exists(args.app):
        raise RecordError(f"{args.app} is not there - build it first (--build)")

    for tool in ("ffmpeg", "ffprobe", "screencapture", "swiftc"):
        if not shutil.which(tool):
            raise RecordError(f"{tool} is not on the path")

    args.out = args.out or os.path.join(MOVIES, "SuperSlapper - macOS gameplay.mp4")
    os.makedirs(os.path.dirname(args.out), exist_ok=True)

    with tempfile.TemporaryDirectory() as work:
        binary = helper(work)
        log = os.path.join(work, LOG_NAME)
        take = os.path.join(work, "take.mov")

        text, started = film(binary, take, log, args.app, args.timeout)
        events = shared.read_log(text)

        print(f"take: {shared.duration(take):.1f}s")
        if args.sheet:
            sheet = os.path.splitext(args.out)[0] + "-sheet.jpg"
            run(["ffmpeg", "-y", "-v", "error", "-i", take, "-vf",
                 "fps=1,scale=360:-1,tile=6x11", "-frames:v", "1", sheet])
            print(f"contact sheet: {sheet}")

        # The edit is written against the demo's own clock; the recording started
        # before the app did, so everything is moved by that difference.
        offset = (events["start"] - started).total_seconds()
        windows = [(max(0.0, start + offset), end + offset)
                   for start, end in shared.plan(events)]
        total = sum(end - start for start, end in windows)
        print("\n".join(f"  cut {start:6.2f} -> {end:6.2f}" for start, end in windows))

        if not 15.0 <= total <= 30.0:
            raise RecordError(f"the edit is {total:.1f}s; app previews are 15-30s")

        assemble(windows, take, args.out)
        print(run(["ffprobe", "-v", "error",
                   "-show_entries", "stream=codec_name,width,height,avg_frame_rate",
                   "-show_entries", "format=duration,size",
                   "-of", "default=noprint_wrappers=1", args.out],
                  capture_output=True).stdout.strip())

        if not 15.0 <= shared.duration(args.out) <= 30.0:
            raise RecordError("the written file is not an app preview length")

        print(f"ok: {args.out} ({total:.1f}s planned)")

    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except RecordError as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(1)
