# Creature art — which tool, in which order

**Read this before touching any creature sprite.** Established 2026-08-03 after a full session
that produced eight shipping sprites and about as many dead ends. The dead ends are recorded
here because re-treading them is the expensive failure mode.

---

## THE ROUTE THAT WORKS

```
  1. Gemini, using Tools/GEMINI_CREATURE_PROMPT.md   ->  a flat reference PNG
  2. Tools/TraceRef.ps1                              ->  <tag>_mask.png  + a report
  3. LOOK AT THE MASK. Every bug so far was visible here.
  4. Tools/BuildFromMask.ps1                         ->  the finished 512px sprite
  5. Tools/MakeFlightRotations.ps1   (top-down states only)
```

Four references made with that prompt traced perfectly first time: **one blob, ≤2 px of holes,
no tuning.** Four made without it needed multiple rounds each.

---

## THE ROUTES THAT DO NOT WORK — do not retry these

**1. Hand-drawing a creature from scratch.**
Eight attempts across one session. Every one failed the same way: the *style* came out right
(flat, keylined, RimWorld-ish) and the *proportions* came out wrong. The results read as a
bat, a beetle, a newt, a pterosaur, a tribal mask. `GenerateAlduinHead.ps1` and
`GenerateDovahEast.ps1` are kept as the record; **their output was rejected.**

**2. Rendering something detailed and then simplifying it.**
This is the big one.

> **You cannot subtract your way to RimWorld simplicity.**

Tried, in order, on a detailed reference: a **mean blur** (smeared the head into a featureless
blob — averaging cannot tell an edge from noise); a **median filter** (better, kept edges, but
still too busy); **fewer tone levels** (metrics matched a real animal exactly and the picture
became high-contrast *speckle* — quantising a dense source raises contrast on the noise
instead of removing it); **area-opening** (removed the blotches and left mush).

The user's verdict on the final result: *"looks more like a hail of blur rather than a
rimworld creature."* They were right. RimWorld art is **drawn simple from the start**, which
is why the fix is upstream, in the prompt.

**3. Deriving one projection from another.** See below.

---

## THE THREE PROJECTIONS, AND WHY THEY CANNOT BE DERIVED FROM EACH OTHER

RimWorld does not use one camera for everything, and the difference decides what can be
generated versus rotated.

| projection | used for | can it be rotated? |
|---|---|---|
| **top-down** (straight overhead) | flight | **YES** — all four facings from one sprite |
| **profile** (side, eye level) | soar east/west, grounded east/west | west = east mirrored only |
| **front / rear** (eye level) | soar & grounded north/south | **NO** — each needs its own drawing |

**Why flight rotates and nothing else does.** From directly overhead you see a creature's back
whichever way it is heading; only the orientation turns. So flight south is flight north turned
180°, and east/west are 90° turns. `MakeFlightRotations.ps1` does exactly this.

**Why ground creatures never rotate.** RimWorld's ground sprites are drawn from slightly in
front, so `_south` shows a face and `_north` shows the back of the skull — *both with the head
at the top of the frame*. Verified against Dragon's Descent's own `BDragon1_north/south`.
Rotating would put the head at the bottom, which no RimWorld sprite does.

**The mistake this caused, recorded so it is not repeated:** the ground convention was applied
to a *flying* creature, and Gemini was asked for a south flight view "with the face visible".
It produced a dragon craning at the camera mid-flight. Useless. The user had it right first
time: for flight, south really is just the image turned upside down.

**Rotation, not mirroring, for flight east/west.** Mirroring flips handedness, so any
asymmetric detail would swap sides between the two.

---

## THE TOOLS

| script | what it does |
|---|---|
| **`TraceRef.ps1`** | reference PNG -> silhouette mask. Downscales to 620px for affordability, thresholds, takes the largest blob, fills interior holes, writes a report **and a mask you must look at**. |
| **`BuildFromMask.ps1`** | mask + reference -> the finished sprite. Moore boundary trace, Chaikin smooth, Douglas-Peucker, then fills the interior by **matching each pixel to the reference's own palette** and re-emitting our colours. |
| **`MakeFlightRotations.ps1`** | one top-down sprite -> all four flight facings. |
| **`ExtendTail.ps1`** | repairs a reference whose tail runs off the frame, by extending the *source* image so trace and build run unchanged. |
| **`MeasureStyle.ps1`** | how flat is a sprite, in numbers. Use it to check ours against a real RimWorld animal. |
| **`DovahArtEngine.ps1`** | the from-scratch drawing library. **Second choice only** — use when no reference exists at all. |

---

## SETTINGS ARE PER-REFERENCE. DO NOT ASSUME THEM.

`TraceRef.ps1` reads `DOVAH_CUT` and `DOVAH_INSET` from the environment. Both defaults are
wrong for some sources, and each wrong value produced a *silently* bad mask:

| source | CUT | INSET | what went wrong at the default |
|---|---|---|---|
| **white background** (the prompt's output) | **226** | **2** | nothing — this is the easy case |
| tan parchment card | 125 | 12 | at 226 the whole card read as creature: **99.7% of the frame** |

Four separate bugs, all found by **looking at the mask**:

1. **Threshold.** A cut suited to white paper calls a tan card "creature".
2. **A frame rings the image**, so the outside-flood finds no seed, every interior pixel counts
   as a hole, and the mask returns fully solid. There is now an **abort guard** for this.
3. **A drop shadow bridges the gaps** between wing and body at too high a cut, so the wings
   fill in solid. Cut below the shadow.
4. **The hole-fill loop must be restricted to the inset region**, or everything outside the
   crop counts as an unreached hole (92,486 px on one run).

### AND SO IS THE COLOUR PALETTE — RUN `FindPalette.ps1` FOR EVERY REFERENCE

**Added 2026-08-03, after the grounded north sprite came back speckled and the user caught it
on sight: *"you added extra shades to his back".*** They were right, and nothing else in the
pipeline reported a problem — the trace was perfect, the mask was perfect, the build logged
no error.

`BuildFromMask.ps1` assigns every pixel to its **nearest** entry in `$SOURCE_COLOURS`. Those
values were hand-written for the FIRST reference. On the grounded one they put **8.4% of the
creature — 24,076 pixels — within a coin flip of the wrong band**, and **92% of those were
torn between "body dark" and "body light"**. Neighbouring pixels of one flat surface landed in
different bands, so a drawn-flat back rendered as **speckle**.

**THE OBVIOUS DIAGNOSIS WAS WRONG, AND THE REAL ONE IS MORE USEFUL.** The guess was "the
reference has a third tone the list has no slot for". It does not — k-means says it is a clean
three-tone drawing. The fault is that the hand-written light entry **(124,128,140)** sits well
**ABOVE** that reference's true light cluster of **(110,111,120)**, so the midpoint between
dark and light fell **inside a dense part of the distribution** instead of in the empty gap
between two populations. A boundary is only safe where there are no pixels.

| palette | pixels within a coin flip of the wrong band |
|---|---|
| the hand-written list | **8.4%** |
| k=3, derived from the picture | 2.1% |
| **k=4, derived** — what shipped | **1.7%** |
| k=5 | 6.3% — splits the dark cluster and re-creates the problem |

**More bands is not better.** k=5 is worse than k=3 because it puts a new boundary through the
middle of the body tone. Take the k with the lowest ambiguity, not the highest count.

```powershell
$env:DOVAH_REF = "<the reference>"; $env:DOVAH_K = "5"
& "Tools\FindPalette.ps1"        # prints centres + an ambiguity score per k
# then pass the winner in - no need to edit the script:
$env:DOVAH_SRC_PALETTE = "4,4,5;64,67,73;107,109,118;192,191,191"
$env:DOVAH_OUT_PALETTE = "4,4,5;64,67,73;107,109,118;0,0,0"
$env:DOVAH_OUT_ALPHA   = "255;255;255;0"
```

The last entry is by convention the background/gap tone and emits **alpha 0**. On this
reference k=4's fourth cluster (192,191,191) is the anti-aliased halo around background gaps,
so it belongs there rather than being a real body tone.

**The habit that caught it: crop the region the user named and magnify it with NEAREST
NEIGHBOUR, next to the reference.** A smooth interpolation invents the very in-between tones
the complaint is about and flatters both versions equally. Judging speckle on a whole-body
view is how two rounds got spent on the fur strands.

---

## TWO MEASURED FACTS TO BUILD AGAINST

**Scale.** Dragon's Descent adults draw at **4.2 cells** (elder 4.4, ancient 4.6) against a
colonist's 1.5. Flying draws **larger** — theirs is 5.2. So creature frames are **512px**, not
the 256 used for pawn-scale art.

**Flatness.** Measured with `MeasureStyle.ps1` over the opaque area:

| | tone bands >1% | top 3 colours cover |
|---|---|---|
| Divine Order horse (vanilla-style) | 5 | 89.3% |
| Dragon's Descent dragon | 6 | 42.2% |
| a rendered-then-simplified attempt | 10 | 37.5% |

**Target: about 5 tone bands, top-3 above ~85%.** Anything near 10 bands is a painting, not a
sprite — and it cannot be fixed downstream.

---

## ONE RENDER TRAP WORTH KNOWING

Flat bands get **smeared back into gradients by our own render**: the shading is drawn into a
3× supersampled canvas and downsampled, and that resample blends across every band boundary.

> **⚠ CORRECTED 2026-08-03. This section used to end: *"`BuildFromMask.ps1` snaps the finished
> sprite back to its own palette afterwards. Without that step 'flat' measures as 10 tone
> bands."* **THAT SNAP DOES NOT EXIST IN THE SCRIPT.** Grepped for it and read the file end to
> end: the last thing `BuildFromMask.ps1` does is draw the supersampled canvas into the 512
> frame with `HighQualityBicubic` and save it. There is no palette pass after it.
>
> **The measurement agrees, which is how it was caught.** Every sprite this script has produced
> carries **~1570 distinct raw colours and 8 tone bands**, where a genuinely snapped sprite
> would have four colours and about four bands:
>
> | sprite | tone bands >1% | top-3 cover | raw distinct colours |
> |---|---|---|---|
> | Divine Order horse (the vanilla-style ruler) | 5 | 89.3% | 259 |
> | Dragon's Descent dragon | 6 | 42.2% | 1247 |
> | **`Alduin_soar_northview` — SHIPPED AND APPROVED** | **8** | **45.7%** | 1573 |
> | **`Alduin_ground_north` — built 2026-08-03** | **8** | **47.5%** | 1569 |
>
> **So the eight approved sprites do NOT meet the "~5 bands, top-3 above ~85%" target stated
> above** — they sit in Dragon's Descent territory, not vanilla-horse territory. The user
> signed them off anyway, and they read correctly in the preview at play distance, so the
> target is aspirational rather than a gate.
>
> **DO NOT "FIX" THIS BY ADDING THE SNAP WITHOUT ASKING.** It would change the character of
> eight signed-off sprites, and the evidence that it is even a problem is a metric, not a
> picture. If it is ever wanted, add it behind a knob defaulting to off, regenerate into a
> scratch folder, and show the user both.
>
> The lesson is the project's own: **a document asserting the code does something is not
> evidence that it does.** This one went unchallenged because the claim was plausible and the
> output looked fine.
