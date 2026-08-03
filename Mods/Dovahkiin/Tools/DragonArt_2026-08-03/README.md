# Alduin — approved creature art, 2026-08-03

**Eight shipping sprites: the FLIGHT set and the SOAR set, all four facings each.**
Approved by the user as they were produced. `SHA256.txt` is the manifest — diff against it
before believing a change touched only what it meant to.

## What is here

| state | north | south | east | west | drawn how |
|---|---|---|---|---|---|
| **flight** — high altitude, fast | ✅ | ✅ | ✅ | ✅ | top-down; **south/east/west are rotations of north** |
| **soar** — low altitude, ground speed | ✅ | ✅ | ✅ | ✅ | profile E/W, front/rear N/S; **west is east mirrored** |
| **grounded** | — | — | — | — | **still needed** |

Three states was the user's design (2026-08-03): *"flight, soar (same speed as grounded), and
grounded... for more attack patterns and game dynamics."*

`*_reference.png` is the Gemini source each sprite was traced from. Keep them — a sprite
cannot be regenerated without its reference.

## Reproducing any of them

```powershell
$env:DOVAH_REF = "<this folder>\Alduin_soar_northview_reference.png"
$env:DOVAH_TAG = "soarn"
$env:DOVAH_CUT = "226"     # white background
$env:DOVAH_INSET = "2"
& "..\TraceRef.ps1"
& "..\BuildFromMask.ps1"
```

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

- **The grounded set** — north, south, east; west free by mirroring.
- **Nothing is wired into any def yet.** These are art files only; no ThingDef, no PawnKindDef,
  no graphic-swap code exists. The runtime mechanism for flight/soar/grounded switching is
  `Verse.PawnGraphicSet.nakedGraphic` (public) — **verified to exist, NOT verified to behave.**
  One decompile pass before building on it.
