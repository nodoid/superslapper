#!/usr/bin/env python3
"""Records the store gameplay video from the iOS build, on a simulator.

The app is launched with the scripted demo switched on (``SUPERSLAPPER_DEMO``),
the simulator screen is filmed while it plays, and the take is cut down to one
app preview: 15-30 seconds of landscape footage at the 1920x886 / 30 fps / H.264
High Profile the App Store accepts, with the game's own music under it.

The cut is not guessed at. Every beat the demo reaches is written to a log inside
the app's own container, stamped with the wall clock, and this script notes the
wall clock of the moment it starts filming, so the two line up: each segment of
the edit is named after the beat it comes from. Re-record and the edit follows
the new take.

    python3 tools/storegen/video.py                 # record, cut, write ~/Movies
    python3 tools/storegen/video.py --build         # build the simulator app first
    python3 tools/storegen/video.py --sheet         # also write a contact sheet
    python3 tools/storegen/video.py --app <path> --device <udid>

Needs Xcode's simulator tools and ffmpeg on the path.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time

# ---------------------------------------------------------------- configuration

APP = "src/SuperSlapper.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/SuperSlapper.app"
BUNDLE = "uk.co.allthejohnsons.superslapper"
LOG_NAME = "demo.log"
MOVIES = os.path.expanduser("~/Movies")


def output_name(device_name: str) -> str:
    """Named the way the other captures in ~/Movies are: what it is, and on what."""
    return os.path.join(MOVIES, f"SuperSlapper - iOS gameplay ({device_name}).mp4")

# The resolution App Store Connect accepts for an iPhone app preview, landscape.
# It is deliberately not the device's own 2868x1320: Apple defines 1920x886 as
# the accepted file size for every iPhone with a Dynamic Island, and rejects the
# native capture dimensions.
#   https://developer.apple.com/help/app-store-connect/reference/app-information/app-preview-specifications/
WIDTH, HEIGHT = 1920, 886

# App previews are capped at 30 fps (the simulator records at 60) and at
# 10-12 Mbps, High Profile Level 4.0, with 256 kbps stereo AAC. The encode is
# quality-driven with the 12 Mbps ceiling as its cap: x264 will not pad a target
# up to 11 Mbps on flat game art, and filler bits would buy nothing.
FRAMES = 30
QUALITY = 17
AUDIO_RATE = "256k"
SAMPLE_RATE = "48000"

# The music bed: the menu track under everything before the bout, the fight track
# from the bout on, crossfaded where the edit crosses over.
MENU_TRACK = "Content/audio/music_menu.wav"
FIGHT_TRACK = "Content/audio/music_fight.wav"
CROSSFADE = 0.5

# The edit. Each segment is (in event, seconds, out event, seconds): film from the
# in event plus its offset to the out event plus its offset. Event names come from
# the demo's own log, so the cut tracks whatever the take actually did.
#
# Beat events are "beat:<name>-in" (arrived on that beat's screen) and
# "beat:<name>-out" (left it again); "start" is the demo's first frame. Bout beats
# are "bout:<Beat>", as the fight screen names them, which is how the exchange and
# the slap replay inside a bout are found.
EDIT = [
    # The title, logo settling, and NEW CAREER going down.
    ("start", 0.1, "beat:title-out", 0.3),
    # The fighter builder, then START CAREER.
    ("beat:create-in", 0.15, "beat:create-out", 0.35),
    # The career dashboard, and TRAINING.
    ("beat:hub-in", 0.3, "beat:hub-out", 0.3),
    # A camp session going on the log.
    ("beat:training-in", 0.5, "beat:training-in", 2.2),
    # A month forward and a day picked on the calendar.
    ("beat:calendar-in", 0.2, "beat:calendar-in", 1.8),
    # Step on the scale, then into the ring.
    ("beat:weigh_in-in", 0.4, "beat:bout-in", 1.0),
    # The bout: the round card, the rival's turn, then the call, the power bar and
    # the slap. Starting on the placard rather than on the dark intro keeps the
    # card that tells the viewer which round they are watching.
    ("bout:RoundPlacard", 0.2, "bout:PlayerSwing", 0.4),
    # The finish, and the verdict on the card.
    ("beat:bout-out", -1.8, "beat:result-in", 1.2),
]
# -----------------------------------------------------------------------------


class EditError(RuntimeError):
    """The take or the log did not give the script what the edit needs."""


def run(command: list[str], **kwargs) -> subprocess.CompletedProcess:
    kwargs.setdefault("check", True)
    return subprocess.run(command, text=True, **kwargs)


def simctl(*args: str, **kwargs) -> subprocess.CompletedProcess:
    return run(["xcrun", "simctl", *args], **kwargs)


def stamp(text: str) -> dt.datetime:
    return dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%S.%fZ").replace(tzinfo=dt.timezone.utc)


def pick_device(requested: str | None) -> tuple[str, str]:
    """The 6.9-inch iPhone the App Store preview sizes belong to."""
    if requested:
        return requested, "iPhone"

    listed = simctl("list", "devices", "available", capture_output=True).stdout
    found: list[tuple[int, str, str]] = []
    for line in listed.splitlines():
        match = re.match(r"\s+(.+?) \(([0-9A-F-]{36})\) \((.+)\)\s*$", line)
        if not match:
            continue

        name, udid, state = match.group(1), match.group(2), match.group(3)
        if "iPhone" not in name or "Max" not in name:
            continue

        # A booted device first, then the newest model, which is the one whose
        # screen the sizes above were written for.
        model = re.search(r"iPhone (\d+)", name)
        rank = int(model.group(1)) if model else 0
        found.append((rank if state == "Booted" else rank - 100, name, udid))

    if not found:
        raise EditError("No 6.9-inch iPhone simulator is installed")

    found.sort(reverse=True)
    return found[0][2], found[0][1]


def read_log(log: str) -> dict[str, dt.datetime]:
    """Turns the demo's beat log into named wall-clock times.

    A beat contributes two events: ``beat:<name>-in`` when the screen it was
    waiting for arrived, and ``beat:<name>-out`` when the beat itself ended, which
    is the moment the next one starts.
    """
    events: dict[str, dt.datetime] = {}
    beaten = re.compile(r"\[demo\] \d+ (\w+): .*? (\d{4}-\d{2}-\d{2}T[\d:.]+Z)$")
    bout = re.compile(r"\[demo\] bout (\w+) (\d{4}-\d{2}-\d{2}T[\d:.]+Z)$")
    start = re.compile(r"\[demo\] start (\d{4}-\d{2}-\d{2}T[\d:.]+Z)$")
    complete = re.compile(r"\[demo\] complete (\d{4}-\d{2}-\d{2}T[\d:.]+Z)$")

    names: list[tuple[str, dt.datetime]] = []
    for line in log.splitlines():
        if match := start.search(line):
            events["start"] = stamp(match.group(1))
        elif match := complete.search(line):
            events["complete"] = stamp(match.group(1))
        elif match := bout.search(line):
            # A beat is reached once per round, so keep the first time it happened:
            # the edit wants the exchange in round one, not the last round's.
            events.setdefault(f"bout:{match.group(1)}", stamp(match.group(2)))
        elif match := beaten.search(line):
            names.append((match.group(1), stamp(match.group(2))))
            events[f"beat:{match.group(1)}-out"] = stamp(match.group(2))

    for index, (name, _) in enumerate(names):
        events[f"beat:{name}-in"] = names[index - 1][1] if index else events["start"]

    return events


def plan(events: dict[str, dt.datetime]) -> list[tuple[float, float]]:
    """Turns the named edit into absolute in/out seconds on the take."""
    windows: list[tuple[float, float]] = []

    for in_event, in_offset, out_event, out_offset in EDIT:
        for event in (in_event, out_event):
            if event not in events:
                raise EditError(f"the take never logged {event!r}")

        start = (events[in_event] + dt.timedelta(seconds=in_offset) - events["start"]).total_seconds()
        end = (events[out_event] + dt.timedelta(seconds=out_offset) - events["start"]).total_seconds()

        if end - start < 0.3:
            raise EditError(f"{in_event} -> {out_event} is only {end - start:.2f}s")
        windows.append((max(0.0, start), end))

    return windows


def housing_and_size(path: str) -> tuple[tuple[int, int, int, int] | None, tuple[int, int]]:
    """Where the device's sensor housing sits in the take, and how big the take is.

    A simulator recording has the Dynamic Island burnt into the picture, and it
    sits inside the game's own letterbox column. Its first frame is not usable -
    the recording starts before the game has drawn - so a frame from inside the
    run is sampled and the black pixels in that column are taken as the housing,
    which nothing else in the letterbox can be.
    """
    from PIL import Image  # imported here so the tool works without it for the rest

    handle = tempfile.NamedTemporaryFile(suffix=".png", delete=False)
    handle.close()

    try:
        run(["ffmpeg", "-y", "-v", "error", "-ss", "2", "-i", path,
             "-frames:v", "1", handle.name])
        with Image.open(handle.name) as image:
            frame = image.convert("RGB")
    finally:
        os.remove(handle.name)

    width, height = frame.size
    column = max(0, (width - round(height * 16 / 9)) // 2)
    pixels = frame.load()

    box = [(x, y) for y in range(height) for x in range(column)
           if sum(pixels[x, y]) < 21]

    if not box:
        return None, (width, height)

    left = min(x for x, _ in box) - 2
    top = min(y for _, y in box) - 2
    right = max(x for x, _ in box) + 2
    bottom = max(y for _, y in box) + 2

    return (max(0, left), max(0, top), right - left, bottom - top), (width, height)


def assemble(windows: list[tuple[float, float]], take: str, out: str) -> float:
    """Cuts the windows out of the take, joins them, and scores the result."""
    housing, (take_width, take_height) = housing_and_size(take)
    filters = []
    labels = []
    for index, (start, end) in enumerate(windows):
        # Trimming inside the filter graph rather than with -ss keeps the joins
        # frame-accurate and needs only one pass over the take.
        # The simulator records at a low, variable frame rate, so the take is made
        # constant-frame-rate before it is cut: trimming a variable take leaves each
        # segment ending on its last real frame, and the missing gap between that
        # frame and the window's end is time the joined edit silently loses.
        filters.append(
            f"[0:v]fps={FRAMES},trim=start={start:.3f}:end={end:.3f},"
            f"setpts=PTS-STARTPTS[v{index}]")
        labels.append(f"[v{index}]")

    filters.append(f"{''.join(labels)}concat=n={len(windows)}:v=1:a=0[vconcat]")

    # The take is the device's native 2868x1320 and has to be the 1920x886 App
    # Store Connect accepts. The housing is taken out by laying the game's own
    # untouched letterbox column - the one at the other edge - over the column it
    # sits in. Both are the same flat colour from the same renderer, so this is a
    # copy rather than a painted patch, and it leaves a clean, frame-high column
    # with no fill edge for the encoder to ring against.
    if housing is None:
        filters.append(f"[vconcat]scale={WIDTH}:{HEIGHT}:flags=lanczos,setsar=1[vout]")
    else:
        # A patch of picture immediately beside the housing, laid over it. On the
        # screens that letterbox, that patch is the flat column colour; on a bout,
        # where the arena runs to the edge, it is the arena beside it. Either way
        # it is the frame's own pixels rather than a invented fill.
        x, y, w, h = housing
        filters.append(
            f"[vconcat]split[frame][duplicate];"
            f"[duplicate]crop={w}:{h}:{min(x + w, take_width - w)}:{y}[patch];"
            f"[frame][patch]overlay={x}:{y}[covered];"
            f"[covered]scale={WIDTH}:{HEIGHT}:flags=lanczos,setsar=1[vout]")

    # The music swaps over where the edit reaches the bout, crossfaded so the
    # change of track is not a click.
    bout_index = next(i for i, segment in enumerate(EDIT) if segment[0].startswith("bout:"))
    bout_at = sum(end - start for start, end in windows[:bout_index])
    total = sum(end - start for start, end in windows)
    menu_length = bout_at + CROSSFADE / 2
    fight_length = total - bout_at + CROSSFADE / 2

    audio = (
        f"[1:a]atrim=0:{menu_length:.3f},asetpts=N/SR/TB,"
        f"afade=t=in:d=0.4[m];"
        f"[2:a]atrim=0:{fight_length:.3f},asetpts=N/SR/TB[f];"
        f"[m][f]acrossfade=d={CROSSFADE}:c1=tri:c2=tri,"
        f"afade=t=out:st={max(0.0, total - 1.5):.3f}:d=1.5[aout]"
    )

    run([
        "ffmpeg", "-y", "-v", "error",
        "-i", take,
        # Both tracks are short and loop in the game, so they are looped here too;
        # a finite count keeps the demuxer from running on forever.
        "-stream_loop", "4", "-i", MENU_TRACK,
        "-stream_loop", "4", "-i", FIGHT_TRACK,
        "-filter_complex", ";".join(filters + [audio]),
        "-map", "[vout]", "-map", "[aout]",
        "-c:v", "libx264", "-preset", "slow", "-profile:v", "high", "-level", "4.0",
        "-crf", str(QUALITY), "-maxrate", "12M", "-bufsize", "22M",
        "-pix_fmt", "yuv420p", "-r", str(FRAMES), "-g", str(FRAMES),
        "-c:a", "aac", "-b:a", AUDIO_RATE, "-ar", SAMPLE_RATE, "-ac", "2",
        "-movflags", "+faststart",
        out,
    ])

    return total


def duration(path: str) -> float:
    out = run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
               "-of", "csv=p=0", path], capture_output=True).stdout.strip()
    return float(out)


def contact_sheet(take: str, out: str) -> None:
    run(["ffmpeg", "-y", "-v", "error", "-i", take,
         "-vf", "transpose=2,fps=1,scale=360:-1,tile=6x11", "-frames:v", "1", out])


def record(args, work: str) -> tuple[str, dict[str, dt.datetime], str]:
    """Fills the simulator screen while the demo plays, and returns the take."""
    device = args.device
    print(f"preparing {device} ...")
    simctl("bootstatus", device, "-b", capture_output=True)
    simctl("install", device, args.app, capture_output=True)

    # Nothing is running yet on a fresh boot, and simctl counts that as an error.
    simctl("terminate", device, BUNDLE, capture_output=True, check=False)

    container = simctl("get_app_container", device, BUNDLE, "data",
                       capture_output=True).stdout.strip()
    log_path = os.path.join(container, "Documents", LOG_NAME)
    if os.path.exists(log_path):
        os.remove(log_path)

    take = os.path.join(work, "take.mov")
    print(f"recording {device} ...")
    recorder = subprocess.Popen(
        # --mask=ignored films the framebuffer rather than the device's shape: without
        # it the Dynamic Island is burnt in as a black pill down one edge of the
        # rotated picture, which is not something a store preview should show.
        ["xcrun", "simctl", "io", device, "recordVideo", "--codec=h264", "--mask=ignored",
         "--force", take],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)

    environment = dict(os.environ,
                       SIMCTL_CHILD_SUPERSLAPPER_DEMO="1",
                       SIMCTL_CHILD_SUPERSLAPPER_DEMO_LOG=LOG_NAME)
    simctl("launch", device, BUNDLE, env=environment, capture_output=True)

    # The demo writes its log inside the app's own container. Watching it is both
    # how the run is followed and how the recording knows to stop, so the take is
    # no longer than the run it filmed.
    log = ""
    deadline = time.time() + args.timeout
    while time.time() < deadline:
        time.sleep(0.5)
        if not os.path.exists(log_path):
            continue

        with open(log_path, encoding="utf-8") as handle:
            log = handle.read()

        if "[demo] complete" in log:
            time.sleep(3)  # a moment on the last screen to cut to
            break
    else:
        raise EditError(f"the demo did not finish in {args.timeout:.0f}s:\n{log[-800:]}")

    recorder.send_signal(signal.SIGINT)
    try:
        recorder.wait(timeout=30)
    except subprocess.TimeoutExpired:
        recorder.kill()

    if not os.path.exists(take):
        raise EditError("the simulator wrote no take")

    for line in log.splitlines():
        print("  " + line)

    return take, read_log(log), log


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--device", help="simulator udid (default: a 6.9-inch iPhone)")
    parser.add_argument("--app", default=APP, help="the built .app to install")
    parser.add_argument("--out", help="default: ~/Movies/SuperSlapper - iOS gameplay (<device>).mp4")
    parser.add_argument("--build", action="store_true", help="build the simulator app first")
    parser.add_argument("--timeout", type=float, default=180, help="seconds to film for")
    parser.add_argument("--sheet", action="store_true", help="also write a contact sheet")
    parser.add_argument("--keep-take", action="store_true", help="keep the raw recording")
    args = parser.parse_args()

    if args.build:
        run(["dotnet", "build", "src/SuperSlapper.iOS/SuperSlapper.iOS.csproj",
             "-c", "Debug", "-f", "net10.0-ios", "-r", "iossimulator-arm64"])

    if not os.path.isdir(args.app):
        raise EditError(f"{args.app} is not there - build it first (--build)")

    for tool in ("ffmpeg", "ffprobe"):
        if not shutil.which(tool):
            raise EditError(f"{tool} is not on the path")

    args.device, device_name = pick_device(args.device)
    args.out = args.out or output_name(device_name)
    print(f"device {device_name} ({args.device})")

    with tempfile.TemporaryDirectory() as work:
        take, events, log = record(args, work)
        print(f"take: {duration(take):.1f}s, {os.path.getsize(take) // 1024 // 1024} MB")

        if args.sheet:
            sheet = os.path.splitext(args.out)[0] + "-sheet.jpg"
            contact_sheet(take, sheet)
            print(f"contact sheet: {sheet}")

        windows = plan(events)
        total = sum(end - start for start, end in windows)
        print("\n".join(f"  cut {start:6.2f} -> {end:6.2f}" for start, end in windows))

        if not 15.0 <= total <= 30.0:
            raise EditError(f"the edit is {total:.1f}s; App Store previews are 15-30s")

        # The simulator films the device screen in its own orientation, so the
        # portrait recording of a landscape game is turned before it is cut.
        rotated = os.path.join(work, "landscape.mov")
        run(["ffmpeg", "-y", "-v", "error", "-i", take,
             "-vf", "transpose=2", "-c:v", "libx264", "-preset", "veryfast", "-crf", "16",
             "-an", rotated])

        if args.keep_take:
            kept = os.path.splitext(args.out)[0] + "-raw.mov"
            shutil.copy2(rotated, kept)
            print(f"raw take: {kept}")

        os.makedirs(os.path.dirname(args.out), exist_ok=True)
        written = assemble(windows, rotated, args.out)

        # Say what the file is, rather than what it was meant to be.
        print(run(["ffprobe", "-v", "error",
                   "-show_entries", "stream=codec_name,width,height,avg_frame_rate",
                   "-show_entries", "format=duration,size",
                   "-of", "default=noprint_wrappers=1", args.out],
                  capture_output=True).stdout.strip())

        if not 15.0 <= duration(args.out) <= 30.0:
            raise EditError("the written file is not an app preview length")

        probe = json.loads(run([
            "ffprobe", "-v", "error",
            "-show_entries", "stream=codec_type,codec_name,width,height,avg_frame_rate,duration",
            "-of", "json", args.out], capture_output=True).stdout)
        video = next(s for s in probe["streams"] if s["codec_type"] == "video")
        if (video["width"], video["height"]) != (WIDTH, HEIGHT):
            raise EditError(f"wrote {video['width']}x{video['height']}, App Store Connect "
                            f"accepts {WIDTH}x{HEIGHT}")
        if video["avg_frame_rate"] not in (f"{FRAMES}/1", f"{FRAMES}/1s"):
            raise EditError(f"wrote {video['avg_frame_rate']} fps, the cap is {FRAMES}")
        if not any(s["codec_type"] == "audio" for s in probe["streams"]):
            raise EditError("the preview has no audio track")

        # A variable-rate take can leave the picture short of the music: the file
        # plays on with the last frame frozen, which is not a preview.
        lengths = {s["codec_type"]: float(s.get("duration", 0)) for s in probe["streams"]}
        if abs(lengths["video"] - lengths["audio"]) > 0.1:
            raise EditError(f"picture is {lengths['video']:.2f}s but sound is "
                            f"{lengths['audio']:.2f}s")

        print(f"ok: {args.out} ({written:.1f}s planned)")

    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except EditError as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(1)
    except subprocess.CalledProcessError as error:
        print(f"error: {error.cmd[0]} failed", file=sys.stderr)
        sys.exit(error.returncode)
