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
./tools/release/macos.sh
./tools/release/windows.sh
dotnet publish src/SuperSlapper.Desktop -f net10.0 -c Release -r linux-x64 --self-contained -o Release/linux
```

Windows also accepts `-r win-arm64`. `Release/` is not tracked by git: it holds
the things that are handed out, one folder per platform, so a build to upload is
a folder to zip rather than something dug out of `bin/`. The mobile heads publish
into their own `bin/Release` folders, which is where the SDK puts the package;
their finished artefacts are moved into `Release/`, so the copy in `Release/` is
the only build that is handed out:

```bash
dotnet publish src/SuperSlapper.Android -f net10.0-android -c Release -o Release/android
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=$KS -p:AndroidSigningStorePass=$SP
  -p:AndroidSigningKeyAlias=superslapper -p:AndroidSigningKeyPass=$KP
dotnet build src/SuperSlapper.iOS -f net10.0-ios -c Release -r ios-arm64 -p:MtouchUseLlvm=false
rm -rf Release/ios/SuperSlapper.app
mv src/SuperSlapper.iOS/bin/Release/net10.0-ios/ios-arm64/SuperSlapper.app Release/ios/
```

The iOS build leaves LLVM code generation off: it is a Release build either way,
and LLVM AOT turns a two-minute link into an hour. Both Apple artefacts are
signed for distribution, not for development: `Release/mac/SuperSlapper.app`
carries the Apple Distribution signature, the `rel-slapper-mac` profile and the
privacy manifest under `Contents/Resources` (see the privacy note above), and
`Release/ios/SuperSlapper.app` the same identity with `rel-slapper` and the
manifest at the root of the bundle. Neither installs on a local machine - a
production profile only works through the App Store - so they are uploads, not
test builds.

### macOS

```bash
./tools/release/macos.sh
productbuild --component Release/mac/SuperSlapper.app /Applications \
  --sign "3rd Party Mac Developer Installer: Paul Johnson (3UH7BE38T3)" \
  Release/mac/SuperSlapper.pkg
```

Two artefacts land in `Release/mac`: the `.app` and the `.pkg` App Store Connect
takes. The installer package is signed with the *installer* identity, not the app
identity - `pkgutil --check-signature` names it, and an upload signed with the
Apple Distribution certificate alone is rejected.

`Release/mac` is a `.app`, not a folder: the macOS build is signed for the Mac
App Store with the Apple Distribution certificate and the `rel-slapper-mac`
profile named at the top of the script. The profile has to be installed in
`~/Library/MobileDevice/Provisioning Profiles`, or passed as
`MAC_PROFILE=<path>`. The bundle carries `Contents/Info.plist`, the profile at
`Contents/embedded.provisionprofile`, the privacy manifest and the art and audio
under `Contents/Resources`. Content belongs there and not in `Contents/MacOS`:
SDL answers a macOS bundle with its `Resources` directory as the base path, so a
`Content` folder next to the binary leaves the app with no art, fonts or sound.

The entitlements are `src/SuperSlapper.Desktop/Entitlements.plist`: the three the
`rel-slapper-mac` profile grants, plus `com.apple.security.app-sandbox`. The Mac
App Store rejects a bundle without the sandbox, and validation names every
executable that lacks it - which is why the script deletes `createdump` after
publishing. That file is the runtime's crash-dump helper, a nested executable,
and the store expects a provisioning profile carrying an application identifier
for anything it finds. Nothing starts it: the game never asks for a dump.

The icon is `src/SuperSlapper.Desktop/Assets/SuperSlapper.icns`, written by
`tools/artgen` and copied into `Contents/Resources` under the name
`CFBundleIconFile` gives. Validation reads that file and wants a 512pt @2x
image (1024px) inside it, so the size that matters is the one in that iconset's
`icon_512x512@2x.png`.

The sandbox has only been checked on paper. A distribution-signed bundle does
not launch outside the store, so the shipped artefact cannot be run here; a
development-signed copy of the same bundle, with the same entitlements, dies at
`HIServices _RegisterApplication` because launchd refuses its lookup of
`com.apple.coreservices.launchservicesd`. Granting that lookup through
`com.apple.security.temporary-exception.mach-lookup.global-name` makes the same
build run under the sandbox, but Apple rejects that exception, so the entitlement
is not in the plist. Exercise this build on a Mac before submitting it.

### Android

```bash
dotnet publish src/SuperSlapper.Android -f net10.0-android -c Release -o Release/android
```

`Release/android` keeps the two files a store takes, and nothing else: the
`-Signed.apk` to install and the `-Signed.aab` to upload, both built from the
same publish. The folder `-o` writes also carries the unsigned packages and a
resource-designer assembly, so anything else that appears there is not part of
the artefact. That publishes every ABI; add `-r android-arm64` (or `android-x64`,
`android-arm`) to build one.

Signing uses an upload keystore, kept out of the repository in `~/keystores`
alongside a properties file that holds the passwords:

```bash
P=~/keystores/superslapper-signing.properties
SP=$(grep '^storePassword' $P | cut -d= -f2); KP=$(grep '^keyPassword' $P | cut -d= -f2)
KS=$(grep '^storeFile' $P | cut -d= -f2); AL=$(grep '^keyAlias' $P | cut -d= -f2)
dotnet publish src/SuperSlapper.Android -f net10.0-android -c Release -o Release/android \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=$KS -p:AndroidSigningStorePass=$SP \
  -p:AndroidSigningKeyAlias=$AL -p:AndroidSigningKeyPass=$KP
```

Without `AndroidKeyStore` the package is signed with the Android debug key, which
installs but is refused by Play. The keystore is the upload key: back it up, and
enrol Play App Signing so a lost upload key can still be replaced.

### iOS

```bash
dotnet build src/SuperSlapper.iOS -f net10.0-ios -r ios-arm64
```

For the simulator use `-r iossimulator-arm64` (or `iossimulator-x64`). Signing is
chosen by configuration in `SuperSlapper.iOS.csproj`: a `Debug` device build uses
the Apple Development certificate, the development profile (`devel-slapper`) and
`Entitlements.plist`, so it can be launched onto a phone; every other
configuration uses the Apple Distribution certificate, the App Store profile
(`rel-slapper`) and `Entitlements.Distribution.plist`, which has no
`get-task-allow` because the store profile forbids it. Edit those four settings
for your own team. `Release/ios/SuperSlapper.app` is the distribution-signed
build; it will not install on a phone for testing, so it is an upload, not a
test build.

App Store Connect takes an `.ipa`, which is the same bundle in a `Payload`
folder:

```bash
rm -rf /tmp/ipa && mkdir -p /tmp/ipa/Payload
cp -R Release/ios/SuperSlapper.app /tmp/ipa/Payload/
(cd /tmp/ipa && zip -qry SuperSlapper.ipa Payload)
mv /tmp/ipa/SuperSlapper.ipa Release/ios/
```

The app ships a privacy manifest, `src/SuperSlapper.iOS/PrivacyInfo.xcprivacy`,
declaring the two required-reason APIs it uses: file timestamps (`C61E.1`) when it
reads and writes the save, and user defaults (`CA92.1`) for the preferences that
sit beside it. It is copied to the root of the bundle by the iOS project; App
Store review looks for it there. The macOS head carries the same file
(`src/SuperSlapper.Desktop/PrivacyInfo.xcprivacy`), placed by
`tools/release/macos.sh` at `Contents/Resources/PrivacyInfo.xcprivacy` - that is
the path review checks for a macOS bundle, and a copy next to the binary would
not be found. The iPhone smoke
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

### Windows

```bash
./tools/release/windows.sh
```

`Release/windows` holds two MSIX packages, `SuperSlapper-x64.msix` and
`SuperSlapper-arm64.msix`, plus the self-signed certificate they are signed with.

The publish runs here; the packaging runs in the Parallels VM, because `makeappx`
and `signtool` are Windows tools. The VM (`WINDOWS_VM`, default `Windows 11`) reads
the staged layout through its shared folder and writes the package back.
`tools/release/msix.py` writes the `AppxManifest.xml` and the tile images, and the
scripts in `tools/release/windows` are the ones that run inside the guest; see
`tools/release/windows/README.md` for the one-off setup.

A self-signed certificate installs on a machine that trusts it and nowhere else:
the Microsoft Store wants a certificate from a CA, or one issued through Partner
Center, whose subject matches the publisher in the manifest. The certificate is
created once and then reused, because signing each build with a fresh one changes
the package identity and orphans the previous install.

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
