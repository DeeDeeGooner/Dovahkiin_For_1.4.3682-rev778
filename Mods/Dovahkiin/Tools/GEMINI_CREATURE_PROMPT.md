# The creature-sprite prompt — the single most valuable artefact of 2026-08-03

**Four references generated with this template traced PERFECTLY on the first attempt** — one
blob, ≤2 pixels of holes, no threshold tuning, no debugging. The four generated *without* it
each cost multiple rounds, and two were unusable.

**Do not paraphrase it. Do not shorten it.** Every clause is there because something specific
broke. If you drop the grid line, you get a grid. If you drop the shadow line, the drop shadow
bridges the wings and the mask fills solid.

> **One correction, 2026-08-03:** the opening line used to read *"Create a **top-down** video
> game creature sprite of…"*. That word contradicted every VIEW block except the flight one —
> a profile or front view is not top-down, and the prompt was asserting both at once. Removed;
> the VIEW block is the only thing that should name the camera.

---

## Why it matters more than any of our drawing code

The hard lesson of 2026-08-03, paid for over a whole session:

> **You cannot render something detailed and then subtract your way to RimWorld simplicity.**
> Mean blur, median filter and area-opening were all tried on a rendered reference. Every one
> of them destroys structure and noise together, and the result is mush. RimWorld art is
> **drawn simple from the start**.

So the reference must arrive already flat. That is what this prompt buys, and nothing
downstream can recover it if the prompt is weakened.

---

## The template

Swap only the block marked **VIEW** for each facing.

```text
Create a video game creature sprite of <CREATURE DESCRIPTION>.

=== VIEW ===
<one of the view blocks below>

=== ART STYLE — THIS IS THE MOST IMPORTANT PART ===
Draw it in FLAT VECTOR GAME-SPRITE style, like a 2D strategy game icon:
- FLAT COLOUR FILLS ONLY. Absolutely no gradients, no airbrushing, no soft
  shading, no blur, no glow, no texture, no noise.
- CEL-SHADED with a STRICT LIMIT OF 4 FLAT TONES total for the whole body,
  plus black for outlines. Each tone must be a solid uniform block of colour
  with a HARD edge where it meets the next tone.
- A THICK, BOLD, SOLID BLACK OUTLINE around the entire silhouette.
- Thinner solid black lines for the few internal details only.
- SIMPLE, BOLD, READABLE SHAPES. This must be legible when shrunk to 50x50
  pixels. Think clean flat illustration, NOT a detailed painting.
- DO NOT draw individual scales. DO NOT draw feather-by-feather or scale-by-
  scale texture. DO NOT cross-hatch.
- The look I want is a clean flat game sprite, similar in simplicity to
  RimWorld or Don't Starve creature art.

=== BACKGROUND — STRICT ===
- The background must be PURE SOLID WHITE (#FFFFFF) and completely uniform.
- NO drop shadow. NO cast shadow. NO ambient occlusion under the creature.
- NO parchment, NO paper texture, NO GRID LINES, NO graph paper, NO squares.
- NO frame, NO border, NO card, NO vignette, NO decorative edging.
- NO text, NO labels, NO captions, NO watermark, NO signature, NO logo.
- Nothing at all in the image except the creature on white.

=== FRAMING — STRICT ===
- The ENTIRE creature must be fully inside the image with clear white margin
  on all four sides.
- The TAIL MUST BE COMPLETE and end in a visible point inside the frame. Do
  not let the tail, wings, horns or any body part touch or run off any edge.

=== COLOURS ===
Use exactly these five flat colours and nothing else:
- Outline: #0A0A0D (near-black)
- Darkest body tone: #1E1F25
- Mid-dark body tone: #3A3C45
- Mid body tone: #5C606C
- Lightest body tone: #8E92A2
- Eyes only: #E8602A (glowing orange)

=== OUTPUT ===
- PNG format. Square image. As high resolution as possible.
```

---

## The VIEW blocks

RimWorld needs three *projections*, and they are not interchangeable — see
`DRAGON_ART_PIPELINE.md` for which state uses which.

**Top-down, flying away (flight north):**
```text
Viewed from DIRECTLY ABOVE, flying AWAY from me.
- Head at the TOP of the image, tail running DOWN to the bottom.
- I see the BACK of the skull and the back of the neck. The FACE MUST NOT BE
  VISIBLE. NO EYES, no muzzle, no jaw.
- A ridge of a few large spines runs down the centre of neck, back and tail.
- Wings spread wide and symmetrically, seen from above. Legs tucked.
```

**Profile, gliding past (soar east):**
```text
A SIDE PROFILE, viewed from the side at roughly eye level, as if gliding past
me from left to right.
- Faces RIGHT. Head on the RIGHT, tail trailing to the LEFT.
- Body roughly HORIZONTAL across the image.
- Legs TUCKED UP under the body — flying, not standing or perched.
- Wings SPREAD; the near wing fully visible, the far wing behind the body.
- I see ONE SIDE only. Do NOT draw both wings spread symmetrically as if from
  above. One glowing orange eye on the near side of the head.
```

**Front-on, coming toward me (soar south):**
```text
Gliding LOW and coming STRAIGHT TOWARD ME, seen from the front at roughly
his own height.
- CRITICAL: I see his FRONT, not his back. FULL FACE visible and pointed at
  me: two glowing orange eyes, muzzle, nostrils, jaw, mouth line.
- I see his CHEST and BELLY with smooth belly plating down the underside.
- There must be NO dorsal spine ridge visible - that is on his back.
- I see the UNDERSIDES of both wings, so the finger-bones show from beneath.
- Head still points toward the TOP of the image; tail runs DOWN behind him.
- Do NOT flip, rotate or mirror a previous image. Only the SIDE of the
  creature I am seeing changes, never the direction he points.
```

### The GROUNDED blocks — added 2026-08-03

**Grounded is NOT soar with the legs down.** A soaring creature is seen slightly from above
with its legs tucked; a grounded one stands with its weight on four legs and the camera
slightly in front. The legs are the whole difference and they must be *drawn*, not implied.

**Standing, seen from behind (grounded north)** — this is the one Alduin already has:
```text
STANDING ON THE GROUND on all four legs, seen from BEHIND and very slightly
above, as if I am walking up behind him.
- Head at the TOP of the image, tail running DOWN to the bottom and ending
  in a visible point.
- I see the BACK of the skull and the back of the neck. The FACE MUST NOT BE
  VISIBLE. NO EYES, no muzzle, no jaw.
- A ridge of large spines runs down the centre of neck, back and tail.
- All FOUR legs planted on the ground and clearly visible, two either side of
  the tail. He is STANDING, not flying and not lying down.
- Wings SPREAD WIDE and symmetrically to both sides, seen from behind.
```

**Standing, seen from the front (grounded south):**
```text
STANDING ON THE GROUND on all four legs, facing me, seen from the FRONT and
very slightly above.
- CRITICAL: I see his FRONT, not his back. FULL FACE visible and pointed at
  me: two glowing orange eyes, muzzle, nostrils, jaw, mouth line.
- I see his CHEST and BELLY, with smooth belly plating down the underside.
- There must be NO dorsal spine ridge visible - that is on his back.
- All FOUR legs planted on the ground and clearly visible. He is STANDING.
- Wings SPREAD WIDE and symmetrically to both sides. I see the FRONT faces
  of the wings, not the undersides - he is standing, not flying overhead.
- Head still points toward the TOP of the image; tail runs DOWN behind him
  and is visible between or past the hind legs.
- Do NOT flip, rotate or mirror a previous image. Only the SIDE of the
  creature I am seeing changes, never the direction he points.
```

**Standing, seen from the side (grounded east):**
```text
STANDING ON THE GROUND on all four legs, in a full SIDE PROFILE at roughly
his own height. Facing RIGHT.
- Head on the RIGHT, tail trailing to the LEFT and ending in a visible point.
- Body roughly HORIZONTAL, held up on four legs. I can see the ground line
  his feet stand on.
- All FOUR legs visible and planted - the near pair fully, the far pair
  behind them. He is STANDING, NOT flying and NOT perched.
- One glowing orange eye on the near side of the head.
- The dorsal spine ridge runs along his back in profile.
- Wings SPREAD; the near wing fully visible, the far wing behind the body.
- I see ONE SIDE only. Do NOT draw both wings spread symmetrically as if
  seen from the front or from above.
```

### Two clauses that a FRONT view needs and the others do not

Both learned on Alduin's grounded south, 2026-08-03, and both were produced by a model that had
followed every other instruction correctly.

**1. LOCK THE LIMB COUNT, AND MAKE IT COUNTABLE.** Asked for a standing winged dragon, the model
added a **pair of arms in addition to the wings** — a six-limbed creature. "It is a wyvern" does
not prevent it. What does: an explicit numbered list of the four limbs, the statement that *the
wings ARE the front limbs*, and a named exception for the small claw at the wing's leading bend,
which is otherwise the seed the arm grows from.

**2. SAY WHICH LIMB IS IN FRONT WHERE THEY OVERLAP.** A front view is the only one where the
tail is *behind* the body, so the model has to be told what hides what. Left to itself it drew
the tail **curling around in front of the feet**, which reads as the tail being in front of the
creature. "Tail behind him" is not enough — the drawable instruction is **"where the tail and a
leg overlap, the LEG is in front"**, plus a ban on the tip coming back toward the viewer.

A profile view needs neither clause: the limb count is self-evident and the tail cannot overlap
anything ambiguously.

### Matching an existing set — override the COLOURS block

The colour list in the template is a *starting* palette. Once a creature's first view exists,
**every later view must use that creature's measured tones instead**, or the same animal comes
back in a different colour scheme each time and the three facings will not sit together.

Run `FindPalette.ps1` on the approved reference, then paste its centres into the COLOURS block
as hex. Alduin's grounded set is `#040405` outline, `#404349` dark, `#6B6D76` light.

**And the pose has to be restated too.** Alduin's grounded north has his **wings fully spread**;
a south reference with folded wings is a different animal, and nothing downstream can fix it.

---

## Two rules for driving it

**Restate everything, every time.** Image models do not reliably remember their own previous
output. "Same as before but from the side" produces a different creature. Every prompt must
carry the full style, background, framing and colour blocks.

**Lead with the CONTRAST when asking for the opposite side.** Asking for "the south view"
gets the north view redrawn. Naming what was in the last image and saying *that is wrong for
this one* is what actually flips it.

**The one correction that works** when it comes back painted rather than flat:

> Too detailed and too painted. Redo it much flatter — only 4 solid blocks of colour, hard
> edges between them, thick black outline, no shading at all.

---

## Checking a result before tracing it

- **Flat?** Squint at it. If you can see a smooth ramp anywhere, reject it.
- **White background, no shadow?** A drop shadow will bridge the gaps between wing and body
  and the mask will fill solid.
- **Whole creature inside the frame?** A tail running off the edge traces as a straight cut
  and needs `ExtendTail.ps1` to repair.
- **Right view?** If asking for a rear view and the spine ridge is missing, or a front view
  and the ridge is present, the model drew the wrong side.
