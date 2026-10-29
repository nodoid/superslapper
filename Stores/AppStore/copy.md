# SuperSlapper — App Store Connect copy (iOS)

| Field | Value |
| --- | --- |
| App name | SuperSlapper |
| Bundle id | `uk.co.allthejohnsons.superslapper` |
| Version | 1.0 (build 1) |
| Seller / artist | Paul F. Johnson |
| SKU (suggested) | `superslapper-ios` |
| Primary category | Games → Sports |
| Secondary category | Games → Simulation |
| Price | Paid _(set in App Store Connect)_ |
| In-app purchases | None |
| Ads | None |
| Devices | iPhone and iPad, landscape only |
| Minimum OS | iOS 18.0 |

## Name (max 30)

<!-- field name="Name" limit="30" -->
SuperSlapper
<!-- /field -->

## Subtitle (max 30)

<!-- field name="Subtitle" limit="30" -->
Slap-fighting career sim
<!-- /field -->

## Promotional text (max 170)

<!-- field name="Promotional text" limit="170" -->
Nine divisions, four leagues and a rulebook that disqualifies you for a closed fist. Train the camp, make the weight and fight your way to a world title.
<!-- /field -->

## Description (max 4,000)

<!-- field name="Description" limit="4000" -->
Slap fighting is the oldest contest there is: two fighters, one open hand, and no guard to hide behind. SuperSlapper puts you inside that career.

Start on the regional amateur circuit with nothing but a name, a division and a reach. Train the camp, make the weight, step on the scale, take your place on the card - then slap your way up four sanctioning leagues to a world title.

EVERY SLAP COMES OFF A REAL ENGINE
Power, aim, chin, endurance, reflex and mass all feed the same simulation, so a bout is decided by the fighters, the rulebook and the slap you chose to throw - not by a dice roll behind the curtain. Land on the cheek or the jaw. Catch the temple or the ear and the referee is looking at you; foul your way through and you are disqualified with the purse still in the promoter's pocket.

TRAIN LIKE IT IS A JOB
Eleven drills and a rest day, each one spending energy out of the same tank you need on fight night. Heavy weights build power and mass, roadwork buys endurance, sparring sharpens timing and hands back the occasional injury. Overtrain and the camp log will tell you so.

MAKE THE WEIGHT, THEN REHYDRATE
Weigh-in morning is its own decision: cut hard and step on the scale drained, or come in comfortable and give away size. Watch the needle, make the limit if you can, then spend the day putting the weight back on.

A CALENDAR, NOT A MENU
Bouts are booked on real dates. Every professional league stages a card a month in each division: three undercard bouts, a co-main and a main event. Yours is on the bill, and the rest of the card can be run out and watched bout by bout from the bottom up.

RANKINGS, BELTS AND MONEY
Every result earns ranking points in the league it was fought in. Climb the table, take a title shot, win the belt and defend it before an interim title is created over your head. Purses grow with the league, the belt and your following.

NINE DIVISIONS, FOUR LEAGUES
Flyweight to super heavyweight, from the regional amateur circuit up to the World Slap League. Each league runs its own rulebook: the number of rounds, the slaps allowed in a round, and how many fouls end your night.

ON A PHONE
Landscape, thumb-sized controls and a big-tap layout: stop the power bar, pick your spot, or choose your defence when the other fighter is throwing. Weights read in kilos or pounds, and the music and effects have their own volume.

Single player, offline. No account, no ads, no in-app purchases. The career is saved on the device.
<!-- /field -->

## Keywords (max 100)

<!-- field name="Keywords" limit="100" -->
slap,fight,career,sports,boxing,sim,simulation,manager,championship,indie
<!-- /field -->

## Support, marketing and privacy URLs

These need a real domain before submission; the values below are the shape Apple
expects, not live pages. **Replace all three.**

| Field | Value |
| --- | --- |
| Support URL | `https://example.com/superslapper/support` |
| Marketing URL | `https://example.com/superslapper` |
| Privacy policy URL | `https://example.com/superslapper/privacy` |
| Support email | `support@example.com` |

## What's new in this version (max 4,000)

<!-- field name="What's new" limit="4000" -->
First release.
<!-- /field -->

## Age rating questionnaire

Apple's questionnaire is answered in App Store Connect. The honest answers for
this build produce a **9+** rating:

| Question | Answer |
| --- | --- |
| Cartoon or fantasy violence | Infrequent/mild |
| Realistic violence | None |
| Profanity or crude humour | None |
| Mature or suggestive themes | None |
| Horror or fear themes | None |
| Medical or treatment information | None |
| Gambling, contests | None |
| Alcohol, tobacco, drug use or references | None |
| Simulated gambling | None |
| Sexual content or nudity | None |
| Unrestricted web access | No |
| User-generated content | No |

There is no blood, no injury shown and no online interaction to rate.

## App privacy

| Data type | Collected? |
| --- | --- |
| Contact info, health, financial, location, contacts, user content, browsing, search history, identifiers, usage data, diagnostics | No |

The game writes a career file and a settings file into its own container
(`LocalApplicationData/SuperSlapper`: `career.json` and `settings.json`) and
nothing leaves the device. Answer "Data Not Collected" for every category, and
"Used for tracking: No".

## Accessibility and orientation

| Field | Value |
| --- | --- |
| Supported orientations | Landscape left, landscape right |
| Full screen | Yes (`UIRequiresFullScreen`) |
| Accessibility | No VoiceOver support in this build; do not claim it |
| Game controllers | Optional - the game is pointer driven, a controller is not required |

## Screenshots and preview

| Device | Files | Size | Required? |
| --- | --- | --- | --- |
| iPhone with Dynamic Island (medium display) | `images/screenshots_iphone_medium_2622x1206/01-08` | 2622x1206 | Yes - this is the required iPhone size |
| iPhone 6.9-inch (large display) | `images/screenshots_iphone_6.9_2868x1320/01-08` | 2868x1320 | Optional - accepted size, used on the large phones |
| iPad 13-inch | `images/screenshots_ipad_13_2752x2064/01-08` | 2752x2064 | Yes - the app declares iPad support (`UIDeviceFamily` 1 and 2) |
| App icon | `images/icon_1024.png` | 1024x1024 | Yes, square, no transparency |

All three sets are landscape, 8 shots each (the maximum Apple accepts is 10 per
size), PNG with no alpha channel. The iPhone sizes are the two the current
screenshot specification lists: 2622x1206 for the medium display and 2868x1320
for the 6.9-inch, both of which App Store Connect accepts in landscape.

The screenshots are real captures of the running game on a branded canvas, each
with one line of copy down the left. Nothing in them is a mock-up: the HUD, the
weigh-in needle and the fight screen are exactly what the app draws.

The **app preview** video is the gameplay recording kept outside this repository
at `~/Movies/SuperSlapper - iOS gameplay (iPhone 18 Pro Max).mp4`: 1920x886
landscape, 30 fps, H.264 High Profile 4.0, 26-27 seconds, stereo AAC 256 kbps -
the resolution App Store Connect accepts for an iPhone preview and the length
band it requires. `tools/storegen/video.py` records it from the simulator and
cuts it on the demo's own beats; `tools/storegen/mac_video.py` makes the macOS
counterpart at 1920x1080.
