# SuperSlapper

A slap-fighting career game. Build a fighter, train the camp, make the weight,
then slap your way up a league: every slap, foul, count and scorecard comes out
of a real rules engine, and the career runs on a calendar with monthly cards,
rankings and belts.

One shared game ships to Windows, macOS and Linux desktop, iOS and Android. It is
built on MonoGame 3.8.6 and .NET 10.

- [How to play](#how-to-play) — for players
- [The rules](#the-rules) — divisions, rulebooks and what counts as a foul
- [Building it](#layout) — for developers
- [Licence](#licence)

## How to play

Everything in the game is a tap: a mouse click on a desktop, a finger on a phone
or tablet. On a desktop, <kbd>Esc</kbd>, <kbd>Backspace</kbd> or <kbd>B</kbd> also
goes back a screen. There is no typing anywhere — every name and setting is
chosen by tapping it until it reads the way you want.

### Start a career

**NEW CAREER** on the title screen. Pick a first name, a last name, a nickname,
which hand you slap with and your division, then **START CAREER**. Your nickname
is what gets used on the posters; your division decides the weight limit you
have to make.

**CONTINUE** picks up the career saved on this device. The career saves itself
after every training day and every bout, so there is no save button and nothing
to lose by closing the game mid-camp.

### The hub

The hub is home. It shows your record, money, division, ranking, popularity,
condition and any injuries you are carrying, along with the next bout and how
many days there are until it.

| Button | What it opens |
| --- | --- |
| **TRAINING** | The camp: one day, one session. |
| **RANKINGS** | The ranked list for your division, with movement arrows. |
| **BELTS** | Who holds what, and whether it is vacant. |
| **RULES** | Your league's rulebook, in full. |
| **CALENDAR** | The month, with your fight and every card on it. |
| **WEIGH IN** | The scale, on fight day. |
| **SETTINGS** | Volume and units. |

The two chips at the top step the music and the sound effects from OFF through
LOW and NORMAL to LOUD. They are separate so you can keep the slaps and quieten
the soundtrack. **SETTINGS** also switches the scale between pounds and
kilograms, which every screen then uses.

### Camp

**TRAINING** spends one day on one session. Energy is what gates it: a heavy
session costs more than a light one, and you cannot start a session you have not
got the tank for. Leftover energy carries into the ring as a penalty, so a
fighter who arrives at fight night exhausted slaps softer.

| Session | What it does |
| --- | --- |
| **Heavy Weights** | Power and mass. Costs 35 energy and adds a little walking weight. |
| **Roadwork** | Endurance. Costs 30 energy and takes a little weight off. |
| **Slap Aim Drill** | Aim, which is what keeps the palm on the cheek instead of the temple. |
| **Slap Power Pads** | The most power per session, and the highest chance of a hand or wrist injury. |
| **Heavy Bag** | Endurance with a little chin work. |
| **Neck & Chin Work** | Chin, for surviving a big slap without going down. |
| **Slip & Brace Drills** | Reflex, which is how well you get the brace up in time. |
| **Live Sparring** | The best all-round gains in the game and the highest injury risk of anything you can do. |
| **Recovery Session** | Heals an injury day and tops up energy. |
| **Rest Day** | Free, and the cheapest way to put energy back. |
| **Weight Cut** | Two pounds off the scale weight, at the cost of energy. |
| **Film Study** | A little aim and reflex, and a little popularity for the buzz. |

Repeating the same session day after day brings diminishing returns, and
training with the tank nearly empty halves the gains and doubles the injury risk,
so a real camp mixes the work. Injuries happen; each one drags down specific
attributes for a number of days and shows on the hub until it heals. Recovery
sessions and time heal them.

On fight day the camp is closed: the game asks for the scale first.

### Making weight

The weigh-in is on the morning of the bout. Each division publishes a limit, and
each rulebook adds an allowance on top, bigger for a non-title bout than a title
one. **STEP ON THE SCALE** reads your weight against that limit.

- **Made weight** — you fight for the full purse and, if it is a title bout, for
  the belt.
- **Up to 1 lb over** — a 10% purse fine. Up to 3 lb over is 20%, more is 30%.
- **Miss weight** — the fine applies and you cannot win or defend a championship
  in that bout, whatever the result.

The scale then rehydrates you, so the weight comes back on before the first slap.

### Fight night

The calendar shows your fight and the rest of the card. On a monthly card your
bout is one line among several; you can run the rest of the undercard and read
the results, or go straight to your own. **WEIGH IN** takes you to the scale, and
the scale takes you to the ring.

### In the ring

Every round follows the same ritual. It is the whole game, and it is worth
reading once.

1. **The round card.** The ring announces the round — one of two, three or five,
   depending on the rulebook — with the division, the rules and both names.
2. **Call the hand.** Left or right: the hand the slap comes from.
3. **Call the count.** One, two or three, the number of the count the slap lands
   on. A count of one is a single swing; three means two practice draws first.
4. **Measure the hand.** Your palm comes up on the other man's cheek and stays
   there until you place it. **HIGHER** and **LOWER** walk it up and down; the
   banner names where it is sitting. On the cheek is clean, too low is the jaw,
   too high is the temple and a foul. **SET THE HAND** throws.
5. **The count.** Each number draws the arm back and puts the palm back on the
   cheek. The number you called is the one that goes through.
6. **The slap and the replay.** The arm comes through, the hand lands, and a
   close-up replay shows the landing.

Between slaps the HUD tells you whose slap is next. The order is fixed for the
whole bout: whoever throws first in round one throws first in every round, and
inside a round the two of you alternate slap for slap.

On his turn the rival measures on your cheek too, and then you choose how to
take it:

| Defence | What it costs |
| --- | --- |
| **TAKE IT** | Full damage, and a clean record. |
| **BRACE** | 25% less damage, paid for in stamina. |
| **FLINCH** | 40% less damage, but a foul. |
| **STEP OFF** | No damage at all, but a foul. |

A slap that hurts enough puts a man down. He gets the count — beat it and the
fight goes on with a little health back, fail it and the bout is over. Going down
the maximum number of times in one round ends it too, as a technical knockout.
Fouls are the other way out: hit the illegal targets, or flinch and step off, and
the referee will count them. Reach the rulebook's limit — two or three — and you
are disqualified.

Between rounds the bell goes, the scores go up, and the next round card follows.
If nobody is stopped, the judges decide on the scorecards.

After the verdict you get the purse, the ranking points, any belt, and the next
bout is booked. Win enough and get high enough in the rankings and you are
promoted to a bigger league, where the purses are larger and the fighters better.

## The rules

### Divisions

| Division | Limit |
| --- | --- |
| Flyweight | 125 lb (56.7 kg) |
| Bantamweight | 135 lb (61.2 kg) |
| Featherweight | 145 lb (65.8 kg) |
| Lightweight | 155 lb (70.3 kg) |
| Welterweight | 170 lb (77.1 kg) |
| Middleweight | 185 lb (83.9 kg) |
| Light Heavyweight | 205 lb (93 kg) |
| Heavyweight | 265 lb (120.2 kg) |
| Super Heavyweight | No limit |

### Leagues and rulebooks

| League | Rules | Rounds × slaps | Fouls to DQ | Judges | Count-out |
| --- | --- | --- | --- | --- | --- |
| Regional Amateur Circuit | Amateur Rules | 2 × 1 | 2 | 3 | 8 |
| European Slap Circuit | ESC Continental Rules | 3 × 2 | 2 | 3 | 10 |
| Atlantic Slap Federation | ASF Open Rules | 3 × 1 | 2 | 5 | 10 |
| World Slap League | WSL World Rules | 5 × 1 | 3 | 3 | 10 |

Every career starts on the amateur circuit. A win on the amateur circuit puts
you in the professional ranks as soon as you have two wins there and a top-three
ranking.

### Legal and illegal

An open hand, on the cheek or the jaw. That is the whole sport.

- **Legal:** the left cheek, the right cheek, the jaw.
- **A foul:** the temple, the ear, the eye, the nose, the throat, the back of the
  head, the neck — and flinching or stepping off instead of taking the slap.

Aim, your palm's position at the measure and the call all decide where the hand
actually lands, so a fighter with low aim can drift off a clean cheek and into a
foul without meaning to.

### Attributes

| Attribute | What it does |
| --- | --- |
| **Power** | How hard the open hand lands. |
| **Aim** | How reliably the slap finds the cheek instead of the temple. |
| **Chin** | Resistance to knockdowns and knockouts. |
| **Endurance** | How much power is left in the late rounds. |
| **Reflex** | How well you brace for a slap. |
| **Mass** | Walking weight, which is also what a heavy slap is built on. |

All six run from 1 to 100. Injuries take points off the attributes they name
until they heal.

## Layout

| Path | What it is |
| --- | --- |
| `src/SuperSlapper.Core` | The simulation: career, fight engine, rules, rankings, belts, training, persistence. No graphics, no platform code — this is what the tests test. |
| `src/SuperSlapper.Game` | The MonoGame front end shared by every head: screen stack, widgets, asset loader, the bout, and the automated self-test harness. |
| `src/SuperSlapper.Desktop` | Windows, macOS and Linux head. `net10.0` uses DesktopGL, `net10.0-windows` uses WindowsDX. |
| `src/SuperSlapper.iOS` | iPhone head (`net10.0-ios`). |
| `src/SuperSlapper.Android` | Android head (`net10.0-android`). |
| `Content/` | Generated art, bitmap fonts and audio. Plain PNG, WAV and JSON, loaded by name at runtime. |
| `tools/artgen/` | The Python generator that produces everything in `Content/` plus the platform icons. |
| `tests/SuperSlapper.Core.Tests` | xunit suite for `SuperSlapper.Core`. |

## Requirements

- **.NET SDK 10.0.400** or later in the 10.x band.
- **Workloads for the mobile heads**, if you build them: `dotnet workload install android ios`.
  Android needs a JDK 17 and the Android SDK; iOS needs Xcode on a Mac.
- **Python 3 with Pillow**, only to regenerate content.
- **No MonoGame tooling.** The framework comes from NuGet and there is no MGCB
  content pipeline, so no MonoGame templates, content builder or extra install
  step is needed.

## Build

```bash
dotnet build SuperSlapper.slnx
```

The solution covers all five heads, so that needs the `android` and `ios`
workloads. To build just the desktop game:

```bash
dotnet build src/SuperSlapper.Desktop -f net10.0
```

## Test

```bash
dotnet test tests/SuperSlapper.Core.Tests
```

214 tests covering the fight engine, fouls, weigh-ins, rulesets, rankings, belts,
training, persistence, the player preferences, the string catalogue and the
career loop.

## Run it

```bash
dotnet run --project src/SuperSlapper.Desktop -f net10.0          # macOS and Linux
dotnet run --project src/SuperSlapper.Desktop -f net10.0-windows  # Windows
```

The game renders to a 1920x1080 virtual surface that each head letterboxes to
the window or device. The save and the player preferences are written to
`LocalApplicationData/SuperSlapper` as `career.json` and `settings.json`.

## Automated checks

Three switches on the desktop head drive the game with no human in front of it.
All set a non-zero exit code on failure, so they work as build steps.

Render every screen to PNG and assert each scripted tap lands where it was aimed:

```bash
dotnet run --project src/SuperSlapper.Desktop -f net10.0 -- --selftest out/selftest
```

52 scenarios cover the title, career setup, hub, training, records, the
no-career stand-ins, the weigh-in, the bout - its round card, the call of a hand
and a count, the measure, the practice draws, the slap itself, the bell that
ends the round and the second round it hands over to, which has to open on the
same man who opened the first - on both the player's turn and the rival's - all
three result branches, the tournament card and its recorded replays, the
calendar, loading a saved career, saving and reading one back, the settings
screen, both volume controls and the routes between them. The last scenario
checks that every string the run put on screen came out of the language
catalogue, and the run finishes with a line saying how many it holds. Each
scenario prints `[selftest] PASS`/`FAIL` plus the screen it landed on, and the
captures land in the directory you pass. An automated run also keeps its save in a scratch folder
of its own, cleared at startup, so it can never touch your career and the title
screen looks the same on every machine.

The last scenario is a **button sweep**. Every screen declares the controls it puts
on show, through the same list the screen itself updates, and the sweep presses
every one of them from a fresh career and insists something happened: the screen
changed, or the career did, or the settings did. A control that does nothing fails
the run. The controls that are meant to do nothing - the tab a screen is already
on, the chip for the setting already in force, the day cells of a calendar with
nothing on them - are declared inert in the harness, and then the sweep insists
they change nothing, so a dead button cannot hide behind them:

```
[buttons] swept 161 control(s): 98 acted, 63 inert by design, 0 dead
[buttons] 88 button(s) on screen: 80 acted; 73 declared region(s): 18 acted
[strings] 528 in en, none missing
```

Play three full bouts through the real screens — hub, weigh-in, ring, result:

```bash
dotnet run --project src/SuperSlapper.Desktop -f net10.0 -- --playthrough
```

Play a whole season — one year of the calendar in a league that runs monthly
cards, training between bouts, running every undercard and taking every bout the
league books:

```bash
dotnet run --project src/SuperSlapper.Desktop -f net10.0 -- --season
```

The season run ends by promotion or when the year runs out, and fails if a card
is left unbooked, the camp stalls on the way to fight day, the purse goes
negative or a bout never reaches a result. It reports what it did, so a run that
tested nothing cannot pass quietly:

```
[play] SEASON OK: the year ran out on day 374 - 12 bouts, 12 cards run,
       48 undercard bouts watched, record 2-10-0 (0 KO), $14474,
       69 background bouts in the book
```

Platform smoke tests that cannot pass arguments can drop a `launch.txt` next to
the executable instead, with `selftest=<dir>`, `playthrough=true` and
`season=true` lines.

None of the three write to your save folder, and all set a non-zero exit code
when they fail.

## Publish

### Desktop

Self-contained, one folder per platform, all of them under `Release/`:

```bash
dotnet publish src/SuperSlapper.Desktop -f net10.0 -c Release -r osx-arm64 --self-contained -o Release/mac
dotnet publish src/SuperSlapper.Desktop -f net10.0 -c Release -r linux-x64 --self-contained -o Release/linux
dotnet publish src/SuperSlapper.Desktop -f net10.0-windows -c Release -r win-x64 --self-contained -o Release/windows
```

Windows also accepts `-r win-arm64`. `Release/` is not tracked by git: it holds
the things that are handed out, one folder per platform, so a build to upload is
a folder to zip rather than something dug out of `bin/`. The mobile heads publish
into their own `bin/Release` folders, which is where the SDK puts the package;
their finished artefacts are copied into `Release/` too:

```bash
dotnet publish src/SuperSlapper.Android -f net10.0-android -c Release -o Release/android
dotnet build src/SuperSlapper.iOS -f net10.0-ios -c Release -r ios-arm64 -p:MtouchUseLlvm=false
cp -R src/SuperSlapper.iOS/bin/Release/net10.0-ios/ios-arm64/SuperSlapper.app Release/ios/
```

The iOS build leaves LLVM code generation off: it is a Release build either way,
and LLVM AOT turns a two-minute link into an hour. `Release/mac` carries
`PrivacyInfo.xcprivacy` beside the executable (see the privacy note above);
`Release/ios/SuperSlapper.app` has it at the root of the bundle.

### Android

```bash
dotnet publish src/SuperSlapper.Android -f net10.0-android -c Release
```

An APK and an AAB land in `src/SuperSlapper.Android/bin/Release/net10.0-android/publish`.
That publishes every ABI; add `-r android-arm64` (or `android-x64`, `android-arm`)
to build one. The package is signed with the Android debug key unless you
configure a release keystore, so it is installable but not store-ready.

### iOS

```bash
dotnet build src/SuperSlapper.iOS -f net10.0-ios -r ios-arm64
```

For the simulator use `-r iossimulator-arm64` (or `iossimulator-x64`). A device
build needs an Apple Development certificate and the provisioning profile named
in `SuperSlapper.iOS.csproj` (`devel-slapper`); edit `CodesignKey`,
`CodesignProvision` and `Entitlements.plist` for your own team.

The app ships a privacy manifest, `src/SuperSlapper.iOS/PrivacyInfo.xcprivacy`,
declaring the two required-reason APIs it uses: file timestamps (`C61E.1`) when it
reads and writes the save, and user defaults (`CA92.1`) for the preferences that
sit beside it. It is copied to the root of the bundle by the iOS project; App
Store review looks for it there. The macOS head carries the same file
(`src/SuperSlapper.Desktop/PrivacyInfo.xcprivacy`), and it is published beside the
executable in `Release/mac`, because a macOS build is handed out as a folder
rather than a bundle. A Mac App Store build has to be a `.app` with the manifest
at `Contents/Resources/PrivacyInfo.xcprivacy` - that is the path review checks,
and a copy next to the binary will not be found. The iPhone smoke
test runs the self-test on device by launching with `SUPERSLAPPER_SELFTEST=selftest`,
which writes the captures into the app's own Documents folder. With a phone
plugged in and paired, that is:

```bash
dotnet build src/SuperSlapper.iOS/SuperSlapper.iOS.csproj -c Debug -f net10.0-ios -r ios-arm64
xcrun devicectl device install app --device <udid> \
  src/SuperSlapper.iOS/bin/Debug/net10.0-ios/ios-arm64/SuperSlapper.app
xcrun devicectl device process launch --device <udid> --console \
  --environment-variables '{"SUPERSLAPPER_SELFTEST":"selftest"}' \
  uk.co.allthejohnsons.superslapper
```

The console shows every `[selftest]` and `[buttons]` line as it happens, and
`--console` is the only report on a phone: iOS forbids an app terminating itself,
so there is no exit code to read. The captures end up in the app's own
`Documents/selftest`, which `xcrun devicectl device info files` lists.

## Regenerating content

```bash
python3 tools/artgen/main.py
```

This rewrites `Content/art`, `Content/font`, `Content/audio` (sound effects and
music) and the app icons for all three platforms. Every module also stands on its
own — each exposes `build_all(out_dir)` — so you can regenerate one slice:

```bash
cd tools/artgen && python3 -c "import music; music.build_all('../../Content/audio')"
```

Committed content is generated output. Regenerate it rather than hand-editing the
PNG and WAV files.

## How it fits together

- **Screens** are a stack (`ScreenManager`). Each screen names the music it wants
  (`Screen.MusicTrack`); the player swaps tracks only when the request changes and
  crossfades between them on an equal-power curve, so the loudness holds through
  the middle of a change. The bout and the award ceremony are the two screens that
  differ from the menu bed.
- **A settings screen** off the hub holds both volume controls and the weight
  units (lb/kg), with the same two chips on the title screen for the sound. Every
  choice is written straight to `settings.json` as it is made, so nothing is lost
  if the game is closed from the middle of a career. The units are the ones the
  weigh-in, the hub and the record book all format through.
- **Two volume controls**, music and effects, each stepping OFF, LOW, NORMAL and
  LOUD. The music level drives the music player and the effects level scales
  everything the screens play. Effects and music are separate on purpose: a
  player can keep the slaps and quieten the soundtrack, or the other way round.
- **Assets** are loaded by name from `Content/` with no content pipeline. A
  missing file is recorded in `Assets.Errors` and falls back to a stand-in, so a
  partial build still runs.
- **One engine** drives everything: the bout you play, the bouts simulated on a
  tournament undercard and the results the tests assert all go through
  `FightEngine`. `SuperSlapper.Core` has no dependency on MonoGame.
- **Careers persist** as JSON in `LocalApplicationData/SuperSlapper`. The
  self-test and playthrough runs disable persistence so a scripted career can
  never overwrite a real save.

## Localisation

Every word the player can read is a key, not a literal. The strings live in
`Content/i18n/<language>.json` as a flat map, and are looked up through
`Loc.T("key")` or `Loc.T("key", arg0, arg1)` with the usual `{0}` placeholders.
That covers the screens, the buttons, the commentary, the rulebook, injury and
drill names and the verdicts, so the simulation and the front end speak with the
same voice.

- **English is the fallback.** `Content/i18n/en.json` is loaded first; the chosen
  language is laid over it, and any key a translation has not covered still reads
  in English. A missing catalogue is English, never a blank screen.
- **A key with no entry returns itself** and is recorded in `Loc.Missing`. The
  self-test fails on any missing key and prints how many strings it holds, so a
  forgotten entry cannot ship quietly.
- **Adding a language** is one file, `Content/i18n/<code>.json`, plus that code in
  the player's settings (`language` in `settings.json`). The bitmap fonts are
  generated from `tools/artgen/fonts.py`; its `CHARS` string is what decides which
  glyphs exist, so a language with accents needs them added there and the fonts
  regenerated.
- **The catalogue is checked by the tests.** `LocalisationTests` loads the real
  English file and walks every division, drill, zone, injury, league, rulebook and
  outcome, failing if one of them comes back as a key.

## Licence

MIT — see [LICENSE](LICENSE). Copyright © 2026 Paul F. Johnson.

In short: you may use, copy, modify, merge, publish, distribute, sublicense and
sell copies of the game and its source, provided the copyright notice and the
permission notice travel with it. The software is provided as is, with no
warranty of any kind.

The licence covers everything in this repository, including the generated
content in `Content/` and the tools in `tools/`, except where a file says
otherwise. Names, logos and trademarks of any real promotion or person are not
part of the game; `Content/` is generated art by `tools/artgen/`.

SuperSlapper is a work of fiction. The fighters, leagues and venues are invented.
Slap fighting is a real sport with real risks; nothing here is advice on how to
take or throw one.
