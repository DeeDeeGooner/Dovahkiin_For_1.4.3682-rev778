# Alduin — approved creature art, 2026-08-03

**TWELVE shipping sprites. All three movement states, all four facings each — COMPLETE.**
Approved by the user as they were produced. `SHA256.txt` is the manifest — diff against it
before believing a change touched only what it meant to.

## What is here

| state | north | south | east | west | drawn how |
|---|---|---|---|---|---|
| **flight** — high altitude, fast | ✅ | ✅ | ✅ | ✅ | top-down; **south/east/west are rotations of north** |
| **soar** — low altitude, ground speed | ✅ | ✅ | ✅ | ✅ | profile E/W, front/rear N/S; **west is east mirrored** |
| **grounded** | ✅ | ✅ | ✅ | ✅ | profile E/W, front/rear N/S; **west is east mirrored** |

**The art is finished. What is NOT done is wiring any of it into the game** — see "Still open".

> ### Grounded SOUTH has a hand-added tail — `AddTail.ps1`
>
> Gemini's pose was right, but his tail was **completely occluded by his own body**, so he read
> as having no tail at all. `AddTail.ps1` adds a length of it back **in source space**, so trace
> and build run unchanged — the same principle as `ExtendTail.ps1`.
>
> `Alduin_ground_southview_reference_untailed.png` is Gemini's raw output;
> `Alduin_ground_southview_reference.png` is that file with the tail, and is what was traced.
> **Keep both** — the second cannot be regenerated or re-tweaked without the first.
>
> **The script can only paint where the source is background**, so the dragon is untouchable by
> construction and the tail is clipped at his own outline — which is what makes it read as
> passing *behind* him. Verified after the final render: **0 changed pixels on him.**
>
> **FOUR VERSIONS WERE REJECTED, AND THE REASON GENERALISES TO THE EAST VIEW.** The first three
> put the tail out of his FLANK. On a front-on creature a flank tail has nothing in front of it
> to hide behind, so its whole length is visible at once and it becomes the loudest shape on the
> sprite — the user's words were *"a huge spike sticking out his right leg"*.
>
> **Size the tail to the hole it shows through, not to his body.** What shipped is almost
> entirely hidden: it runs down inside his left leg and only the tip clears his left foot. The
> settled numbers are `$SPINE` ending at x=185, `$ROOT_HALF` 24, `$TIP_HALF` 2.5, `$KEYLINE` 12.
>
> Two traps found while fitting it, both silent:
> - **`$TIP_HALF` must be finer than `$KEYLINE`.** The stroke's round cap adds ~12px all round,
>   so a half-width of 5 ends in a 17px blob and the tip reads as a cut-off bar, not a point.
> - **Check the changed-pixel bounding box against the leg gap.** An earlier root at x=500 put
>   an 18px sliver of tail in the gap between his legs; `max x` 524 against a gap edge of 507
>   caught it in one number, where the render did not.

Three states was the user's design (2026-08-03): *"flight, soar (same speed as grounded), and
grounded... for more attack patterns and game dynamics."*

`*_reference.png` is the Gemini source each sprite was traced from. Keep them — a sprite
cannot be regenerated without its reference.

> ### ⚠ THE GROUNDED SET IS INCOMPLETE, AND THE POSE IS THE CONSTRAINT
>
> `Alduin_ground_northview` has his **WINGS FULLY SPREAD**. The south and east references must
> match that pose. Wings spread in one facing and folded in another reads as two different
> animals the moment the player rotates him — and it is **not fixable downstream**; it needs a
> new reference. Say *"wings spread wide, same as the rear view"* when asking for them.
>
> They must also use **the same three tones** (below), or the three views will be the same
> dragon in three different colour schemes.
>
> ### ⚠ AND GROUNDED SOUTH MUST NOT LOOK LIKE SOAR SOUTH
>
> `Alduin_soar_southview_reference.png` is a **front view of a dragon with his legs hanging and
> his tail straight down** — which is very nearly what "standing" looks like from the front. It
> was checked, not assumed: the user's `LOOKINGatme.jfif` is **pixel-identical to it, max delta
> 0**, so the two front views start from the same drawing.
>
> **If the grounded south reference comes back in that same pose, grounded and soar are the same
> sprite from the front, and one of the three movement states stops existing visually.** The
> whole point of three states was *"more attack patterns and game dynamics"*.
>
> What has to differ, and it must be asked for explicitly — a model will not infer it:
>
> | | soar south (exists) | grounded south (needed) |
> |---|---|---|
> | legs | dangling, straight, not weight-bearing | **planted, visibly BENT, feet flat and splayed** |
> | body | stretched long, suspended | **settled LOWER, compact between the shoulders** |
> | tail | straight down, streaming | **resting on the ground, curving to ONE SIDE** |
> | camera | eye level | **slightly ABOVE, looking down at him** |
>
> The curving tail is also what matches `Alduin_ground_northview`, whose tail curls to the side
> on the ground. Straight-down tail = airborne; curved tail = resting on something.

## Reproducing any of them

```powershell
$env:DOVAH_REF = "<this folder>\Alduin_soar_northview_reference.png"
$env:DOVAH_TAG = "soarn"
$env:DOVAH_CUT = "226"     # white background
$env:DOVAH_INSET = "2"
& "..\TraceRef.ps1"
& "..\BuildFromMask.ps1"
```

### Grounded north needs its palette passed in — it is NOT the default

The default colour table in `BuildFromMask.ps1` was hand-written for the first reference. On
the grounded reference it put **8.4% of the creature within a coin flip of the wrong band**,
and the back rendered as speckle — the user caught it on sight. `FindPalette.ps1` derives the
right one; **k=4 scored 1.7%**. Full write-up in `../DRAGON_ART_PIPELINE.md`.

```powershell
$env:DOVAH_REF  = "<this folder>\Alduin_ground_northview_reference.png"
$env:DOVAH_TAG  = "Alduin_ground"
$env:DOVAH_VIEW = "north"          # north/south do NOT mirror; only east does
$env:DOVAH_CUT  = "226"
$env:DOVAH_INSET = "0"
$env:DOVAH_SRC_PALETTE = "4,4,5;64,67,73;107,109,118;192,191,191"
$env:DOVAH_OUT_PALETTE = "4,4,5;64,67,73;107,109,118;0,0,0"
$env:DOVAH_OUT_ALPHA   = "255;255;255;0"
& "..\TraceRef.ps1"
& "..\BuildFromMask.ps1"
```

**Those three tones — `#040405`, `#404349`, `#6B6D76` — are Alduin's grounded palette.** Put
them in the prompt for the east reference so all three views match.

### Grounded south — its own palette and the tail step

```powershell
# 1. put the tail back (per-image geometry lives in the script's $SPINE)
$env:DOVAH_REF = "<this folder>\Alduin_ground_southview_reference_untailed.png"
$env:DOVAH_OUT = "<scratch>\tailed.png"
& "..\AddTail.ps1"
# 2. then the normal route, with THIS image's derived palette
$env:DOVAH_REF  = "<scratch>\tailed.png"
$env:DOVAH_TAG  = "Alduin_ground_s"
$env:DOVAH_VIEW = "south"
$env:DOVAH_CUT  = "226"; $env:DOVAH_INSET = "0"
$env:DOVAH_SRC_PALETTE = "4,4,5;63,64,70;105,107,116;189,187,186"
$env:DOVAH_OUT_PALETTE = "4,4,5;63,64,70;105,107,116;0,0,0"
$env:DOVAH_OUT_ALPHA   = "255;255;255;0"
& "..\TraceRef.ps1"
& "..\BuildFromMask.ps1"
```

Its palette is within 3 levels of grounded north's, so the set is consistent — but it was
**derived, not copied**.

### Grounded east had its TAIL LIFTED — `LiftTail.ps1`

Gemini's pose was right but the tail **hung 81px below the ground he stands on** (underside
y=851 against a near foot bottoming at y≈770) and tangled with the legs. `LiftTail.ps1` swings
it up about the hip, moving **Gemini's own tail** — its dorsal spines, its lighter stripe, its
taper — rather than drawing a new one.

`Alduin_ground_eastview_reference_unlifted.png` is Gemini's raw output;
`Alduin_ground_eastview_reference.png` is that file with the tail lifted 20°, and is what was
traced. **Keep both.**

```powershell
$env:DOVAH_REF = "<this folder>\Alduin_ground_eastview_reference_unlifted.png"
$env:DOVAH_OUT = "<scratch>\lifted.png"
$env:DOVAH_LIFT = "20"
& "..\LiftTail.ps1"          # then TraceRef + BuildFromMask with DOVAH_VIEW=east
$env:DOVAH_SRC_PALETTE = "4,6,5;60,64,69;113,116,125;194,193,193"
$env:DOVAH_OUT_PALETTE = "4,6,5;60,64,69;113,116,125;0,0,0"
```

> **THE LESSON, AND IT IS THE ONE WORTH KEEPING: BEND, DO NOT CUT AND ROTATE.**
>
> The first version cut the tail at the hip and rotated it rigidly. That tears the tail away
> from the body and leaves a wedge — a cut's corners swing by ±halfWidth·sin θ, about 22px at
> 20° — which showed as a **white notch** between tail and leg.
>
> Patching the notch by extruding the cut cross-section backward filled the hole but **dragged
> the tail's own keylines and stripe into the body as straight bands**, and the user reported
> that immediately: *"the part where the cut was made is still noticeable."*
>
> The fix is to **ramp the angle along the tail** — 0° at the root, easing to the full lift
> further out. At the root nothing moves, so there is no join to blend at all; the pixels are
> written back exactly where they came from, and the tail curves out of the hip the way a real
> one does. The extrusion could then be deleted outright: its footprint fell from 13,779px
> hidden behind the body to **80**.
>
> **It is exact, not an approximation.** Rotation about the pivot preserves distance from the
> pivot, so a destination pixel's radius equals its source's and the angle to undo is known
> from the destination alone — no search, no iteration.
>
> **Use a SMOOTHSTEP for the ramp, not a power law.** `t^n` is flat at the root but arrives at
> the full angle with its slope still climbing, so curvature jumps where the bend ends and
> leaves a faint kink across the tail. `3t²−2t³` has zero slope at both ends.
>
> Also measured: his wings span x 5..1019 of 1024 — **4-5px of margin**, so the canvas must be
> PADDED before anything rotates or the lifted tip lands outside the frame.

**The script snapshots in this folder were refreshed 2026-08-03** when `BuildFromMask.ps1`
gained `DOVAH_VIEW` and the palette overrides. The refactor was **proved inert** rather than
assumed: a fresh default-settings run reproduced `Alduin_soar_northview.png` **byte-identical**
to its manifest hash.

Flight east/south/west are not traced at all — they come from `MakeFlightRotations.ps1` run on
`Alduin_flight_northview.png`.

## The two things that decide whether new views can be rotated or must be generated

**Flight rotates. Nothing else does.** From directly overhead you see a creature's back
whichever way it is going, so flight south is flight north turned 180°. RimWorld's *ground*
sprites are drawn from slightly in front — `_south` shows a face, `_north` shows the back of
the skull, **both head-up** — so those can never be derived from each other.

Applying the ground rule to a flying creature produced a dragon craning at the camera
mid-flight. Recorded in `../DRAGON_ART_PIPELINE.md` so it is not repeated.

**West is east mirrored** for profile views, which is how RimWorld handles west for every
creature. Flight east/west use *rotation* rather than mirroring, because mirroring flips
handedness and any asymmetric detail would swap sides.

## Scale, when these are wired into defs

Dragon's Descent adults draw at **4.2 cells**, elder 4.4, ancient 4.6, against a colonist's
1.5 — and their *flying* graphic is **5.2**. Alduin must sit clearly above their `True_Dragon`
(combat power 1650), so expect roughly 4.6 grounded and 5.6 flying.

## Still open

- ~~**The grounded set**~~ — **DONE 2026-08-03/04.** All twelve sprites exist.
- **Nothing is wired into any def yet.** These are art files only; no ThingDef, no PawnKindDef,
  no graphic-swap code exists. The runtime mechanism for flight/soar/grounded switching is
  `Verse.PawnGraphicSet.nakedGraphic` (public) — **verified to exist, NOT verified to behave.**
  One decompile pass before building on it. **This is now the whole of what remains** between
  the art and a dragon in the game.
- **Scale, when the defs are written:** Dragon's Descent adults draw at 4.2 cells, elder 4.4,
  ancient 4.6, against a colonist's 1.5 — their flying graphic is 5.2. Alduin must sit clearly
  above their `True_Dragon` (combat power 1650), so expect roughly **4.6 grounded, 5.6 flying**.
