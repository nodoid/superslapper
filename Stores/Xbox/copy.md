# SuperSlapper — Xbox store listing copy

Publishing a title to Xbox uses the same Partner Center store listing as the
Windows build, with an extra set of required artwork and the certification rules
that go with it. The text fields are shared with the Microsoft Store listing, so
they are written once: see `../MicrosoftStore/copy.md` for the description,
features, keywords and rating answers. What follows is what is different about
Xbox.

| Field | Value |
| --- | --- |
| Product name | SuperSlapper |
| Developer / publisher | Paul F. Johnson |
| Device family | Xbox (with the Windows listing sharing the same text) |
| Genre | Sports |
| Players | 1 (offline, single player) |
| Online play | No |
| Cross-save | No |
| Version | 1.0 (first release) |

## Required Xbox artwork

The Xbox submission will not certify without these, which is why they are in this
folder as well as the shared ones. Each one comes with a rule, and each file here
was built to it:

| File | Requirement | Rule |
| --- | --- | --- |
| `images/box_art_2160.png` | Required | 1:1, 2160x2160. Title in the upper two-thirds: the storefront may lay a gradient or text over the bottom third |
| `images/poster_art_1440x2160.png` | Required for Xbox games | 2:3, 1440x2160. Title in the upper two-thirds |
| `images/titled_hero_art_1920x1080.png` | Required | Exactly 1920x1080 - 4K is not supported for this asset. Title required, upper two-thirds |
| `images/super_hero_art_3840x2160.png` | Required | 16:9, 3840x2160. Artwork only: no title and no text of any kind |
| `images/featured_promotional_square_1080.png` | Required | 1:1, 1080x1080. Artwork only: no title and no text |
| `images/screenshots_1920x1080/01-08` | Required (one), 4+ recommended | Xbox screenshots at 1920x1080; 3840x2160 is preferred but this build renders at 1920x1080 natively, so upscaling would only add weight |

None of these files carries a publisher logo, a developer logo, an age rating
icon, an award icon or a platform button glyph, which is the store's stated rule
for every image slot above.

## Trailer

Xbox trailers are 1920x1080. The gameplay recording is cut once and used twice:
`../Shared/trailer_1920x1080.mp4` is the 16:9 master for this store, and the
iPhone-shaped cut of the same take is the iOS app preview (see
`../AppStore/copy.md`).

## Description and features

Copied from the Microsoft Store listing when the submission is made:

- description: `../MicrosoftStore/copy.md` → Description
- product features: `../MicrosoftStore/copy.md` → Product features

## Xbox certification notes

| Check | Status in this build |
| --- | --- |
| Title readable on box art, poster and titled hero | Yes, wordmark in the upper two-thirds |
| No text in the artwork-only slots | Yes, `super_hero_art` and `featured_promotional_square` are artwork only |
| No transparency in any submitted image | Yes, every file is flattened to 24-bit PNG |
| Achievements | 1,000 gamerscore is planned; none are wired into the build yet - do not claim them in the listing |
| Suspend and resume | Handled by MonoGame's iOS/desktop lifecycle; console suspend is untested |
| Input | Pointer driven (mouse or touch). Controller navigation is not implemented in this build and must be declared as such in the submission |

Two things need a human before submission: the achievements set (nothing in the
build reports them yet) and an Xbox test account pass on real devkit hardware.
The input line above is the one most likely to be raised in certification, so it
is stated plainly rather than left for the tester to find.
