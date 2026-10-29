"""What each store asks for, and where that is written down.

One entry per file the generator produces. The dimensions and the constraints are
taken from the store's own documentation, cited per group so a size can be checked
without hunting:

  Microsoft Store / Xbox
    https://learn.microsoft.com/en-us/gaming/game-publishing/concepts/store-listing
    Required set for a game: box art, poster art, super hero art, titled hero art
    (Xbox), featured promotional square art (Xbox), screenshots. The branded key art
    format has been withdrawn, so it is deliberately absent here.

    https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images
    Screen sizes and the "no transparent layers" rule.

  Apple App Store
    https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/
    The required iPhone size is "iPhone with Dynamic Island (medium display)":
    1206x2622 or 1179x2556 portrait, so 2622x1206 in landscape. The 6.9-inch
    2868x1320 set is still accepted and is what the large phones show. A 13-inch
    iPad set (2752x2064 landscape) is required for a title that supports iPad,
    which this one does (UIDeviceFamily 1 and 2). The app is landscape only.

  Google Play
    https://support.google.com/googleplay/android-developer/answer/9866151
    512x512 icon (32-bit PNG with alpha), 1024x500 feature graphic (24-bit PNG or
    JPEG, no alpha), 2-8 phone screenshots.
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Spec:
    """One file to produce: what it is, how big, and what may not be in it."""

    filename: str
    size: tuple[int, int]
    requirement: str
    notes: str
    alpha: bool = True
    jpeg: bool = False


# --------------------------------------------------------------- key art
# Title placement is enforced for the title-bearing assets: the wordmark sits in
# the upper two-thirds, because the storefront may lay text or a gradient over the
# bottom third. Artwork-only assets must carry no title and no text at all.
XBOX_ART: tuple[Spec, ...] = (
    Spec("box_art_2160.png", (2160, 2160), "required",
         "Title in the upper two-thirds, no transparency, no publisher or rating marks.",
         alpha=False),
    Spec("poster_art_1440x2160.png", (1440, 2160), "required",
         "Title in the upper two-thirds, no transparency.", alpha=False),
    Spec("titled_hero_art_1920x1080.png", (1920, 1080), "required",
         "Title required, upper two-thirds. 4K is not supported for this asset.", alpha=False),
    Spec("super_hero_art_3840x2160.png", (3840, 2160), "required",
         "Artwork only: no title, no text, no logos.", alpha=False),
    Spec("featured_promotional_square_1080.png", (1080, 1080), "required",
         "Artwork only: no title, no text.", alpha=False),
)

MICROSOFT_ART: tuple[Spec, ...] = (
    Spec("box_art_2160.png", (2160, 2160), "required",
         "Title in the upper two-thirds, no transparency.", alpha=False),
    Spec("poster_art_1440x2160.png", (1440, 2160), "required",
         "Title in the upper two-thirds, no transparency.", alpha=False),
    Spec("super_hero_art_3840x2160.png", (3840, 2160), "required",
         "Artwork only: no title, no text, no logos.", alpha=False),
    Spec("store_icon_1080.png", (1080, 1080), "required",
         "App icon at store size, from the same artwork the app ships.", alpha=False),
    Spec("store_icon_450.png", (450, 450), "recommended", "Small listing icon.", alpha=False),
    Spec("store_icon_300.png", (300, 300), "required", "App tile, the size the Store asks for first.",
         alpha=False),
)

GOOGLE_ICON: tuple[Spec, ...] = (
    Spec("icon_512.png", (512, 512), "required",
         "32-bit PNG with alpha, 1 MB or less. Play masks it, so it is not pre-rounded."),
    Spec("feature_graphic_1024x500.png", (1024, 500), "required",
         "24-bit PNG or JPEG, no alpha. The middle is covered by the play button, so it is kept clear.",
         alpha=False),
)

APPLE_ICON: tuple[Spec, ...] = (
    Spec("icon_1024.png", (1024, 1024), "required",
         "App Store version of the icon: square, no transparency, no rounded corners.", alpha=False),
)


# --------------------------------------------------------------- screenshots
@dataclass(frozen=True)
class Shot:
    """One screenshot: which capture it uses and what it is selling."""

    source: str
    headline: str
    subline: str


# Eight shots that walk the career the way a player meets it. Every one is a real
# capture of the running game, taken by the self-test harness.
SHOTS: tuple[Shot, ...] = (
    Shot("02_create.png", "BUILD A FIGHTER",
         "Nine divisions, four leagues, one career."),
    Shot("04_hub.png", "RUN THE CAREER",
         "Ranking, purse, condition and who is next."),
    Shot("05_training.png", "TRAIN THE CAMP",
         "Eleven drills and a rest day. Energy is the currency."),
    Shot("29_calendar_month.png", "FIGHT NIGHTS ON A CALENDAR",
         "Monthly cards on real dates."),
    Shot("25_card.png", "FIVE BOUTS A CARD",
         "Take yours, run the rest."),
    Shot("17_weighin.png", "MAKE THE WEIGHT",
         "Step on the scale, then rehydrate."),
    Shot("20c_fight_measure.png", "AIM THE SLAP",
         "Cheek or jaw. A closed fist is a foul."),
    Shot("22_result_win.png", "BELTS AND PURSES",
         "Rankings, titles, injuries and money."),
)

# Landscape 1920x1080 is the game's native render size and is accepted by all
# three stores: Microsoft lists 1920x1080 for Xbox and mobile screenshots and
# 1366x768-or-larger for desktop, and Google accepts 320-3840 px sides.
SHOT_LANDSCAPE = (1920, 1080)

# The required set: iPhone with Dynamic Island (medium display), the size App
# Store Connect asks for first. Landscape, because the app is landscape only.
APPLE_IPHONE_MEDIUM = (2622, 1206)

# The largest accepted set (6.9-inch), used for the large phones. Accepted, not
# required; App Store Connect will not scale up to it on its own.
APPLE_IPHONE = (2868, 1320)
APPLE_IPAD = (2752, 2064)
