# ART_PROMPTS.md — copy-paste prompts for an image tool

Companion to `ART_TODO.md`. That file is the *record* of what is missing and why; this one is
the bit you can hand straight to Gemini, ChatGPT, Midjourney or an artist.

**Do these in the order below.** Item 1 fixes a thing you have complained about twice in
playtest. Item 2 is cosmetic polish.

---

## How to get a usable file out of an image tool

Image models will not hand you a game-ready asset. Every one of these needs the same three
steps afterwards:

1. **Generate** — you will get a square image, usually 1024×1024, *with a background*.
2. **Remove the background** — any background remover, or Photopea (free, browser,
   `photopea.com`): open the image, magic-wand the background, delete, export as PNG.
3. **Resize** to the size given below, and save as **PNG with transparency**.

Then drop the file where the prompt says and **tell me** — I wire it into the XML. Don't edit
the defs yourself; the paths have to match exactly and I'd rather catch a typo than have you
hunt a missing-texture square.

> **The one thing that matters most:** for the shout icons, generate **ONE** image and recolour
> it eleven times. Do not generate eleven images. They will not match, and the whole point of
> the family is that they are identical but for colour.

---

## 1. Cyclone's tornado sprite — **do this one first**

This is the one that actually fixes a complaint. Cyclone currently fakes a vortex out of
vanilla dust particles, and you have twice said it doesn't read as a tornado. You are right,
and one drawn sprite fixes it properly.

**File:** save as `Dovahkiin_Vortex.png` → put in
`Mods\Dovahkiin\Textures\Things\Mote\`
**Size:** 256×256, transparent background.

### Prompt

```
A top-down view of a tornado funnel, drawn as a flat 2D game sprite for a
strategy game viewed from directly above.

Pure greyscale — white and light grey only, no colour at all.

Structure: a small dense bright-white core at the exact centre, with three
soft arms spiralling outward from it in an anticlockwise direction. The arms
get thinner, fainter and more broken up as they reach the outer edge, fading
to nothing before they touch the border. Wispy, like dust and wind caught in
rotation, not solid.

Style: flat and slightly painterly, soft edges, no outlines, no bevels, no
drop shadow, no lens flare, no 3D shading. It should look like a smoke or
dust effect, not an illustration of a tornado.

Perfectly centred and radially balanced, so it can be rotated about its
centre without wobbling.

Plain black background, no scenery, no ground, no text, no border, no frame.
```

**Why greyscale:** the game tints it at runtime. Cyclone tints it faint grey, but the same
sprite can be reused later for Storm Call in violet, or any other swirl, for free. A coloured
sprite would lock it to one shout.

**Why centred and balanced:** the code spins it about its own centre every tick. Anything
off-centre will visibly wobble like a bad wheel.

---

## 2. The shout icon family — SAME image as prompt 1, recoloured eleven times

**CORRECTED 2026-07-26.** This originally asked for a *comet with a long curving tail*. That was
wrong — the user checked TES5's Magic menu, and the shout icons are the same **wispy rotating
swirl** as the tornado in prompt 1. So there is no second prompt: **the image from prompt 1 is
the master for the buttons too.**

**Files:** `Dovahkiin_Shout_<Name>.png` → `Mods\Dovahkiin\Textures\UI\Abilities\`
**Size:** 128×128, transparent background.

### You do not need to recolour these by hand

Hand-recolouring eleven images in Photopea is fiddly and drifts. **I do it in code instead** —
each pixel keeps its transparency and brightness, and only the hue is replaced. That makes the
eleven exactly consistent by construction, and re-running it after a colour change costs
nothing. Just give me the master; the table below is what I apply.

**FIFTEEN, not eleven.** 14 core shouts plus Dragonrend. The last four are not built yet, but
their icons cost nothing to generate now since they are only recolours of the same master.

| # | File name | Colour |
|---|---|---|
| 1 | `Dovahkiin_Shout_UnrelentingForce` | Cold pale grey-white, faint blue edge. No elemental tint — this one is *force* |
| 2 | `Dovahkiin_Shout_FireBreath` | Ember orange, deepening to dark red |
| 3 | `Dovahkiin_Shout_FrostBreath` | Ice blue into white |
| 4 | `Dovahkiin_Shout_WhirlwindSprint` | Pale white-grey, edge blurred as if moving |
| 5 | `Dovahkiin_Shout_MarkedForDeath` | Blue-grey leaning grey — cold and dead, no violet |
| 6 | `Dovahkiin_Shout_ClearSkies` | Pale sky-blue into soft white. The calm one |
| 7 | `Dovahkiin_Shout_SlowTime` | Warm sand-gold, strands longer and thinner |
| 8 | `Dovahkiin_Shout_BecomeEthereal` | Translucent blue-white, core faded rather than bright |
| 9 | `Dovahkiin_Shout_DrainVitality` | Deep dark violet, darker and more purple than Marked for Death |
| 10 | `Dovahkiin_Shout_Dismay` | Red, strands longer and more scattered |
| 11 | `Dovahkiin_Shout_Cyclone` | Faint translucent grey, swirl tighter than the default |
| 12 | `Dovahkiin_Shout_StormCall` | Violet into charcoal, sparks brighter than the core |
| 13 | `Dovahkiin_Shout_SoulTear` | Deep blood-crimson bleeding into black |
| 14 | `Dovahkiin_Shout_DragonAspect` | Spectral bronze-gold |
| 15 | `Dovahkiin_Shout_Dragonrend` | Cold bone-white with a hard iron-grey edge |

**Bonus:** landing these also fixes a real bug. Three shouts currently borrow **Biotech** icons
(`FireSpew`, `AcidSpray`, `Longjump`), so on an install without Biotech they show a
missing-texture square. Own icons remove that permanently — see `ART_TODO.md`.

---

## 3. Dragon Aspect's overlay — the big one, not yet needed

Don't start this until Dragon Aspect is actually built. Listed so it isn't forgotten:
spectral bronze-gold plating over the pawn, shoulder spurs that break the silhouette, ember
rim-light, drifting motes. `SPEC.md §4.4d`. It is the largest single art task in the project
and needs to be specced against the working shout before anyone draws it.

---

## If the results are poor

Tell me what came back and I will rewrite the prompt. Image models are inconsistent with
"flat, no shading, no glow" — they love adding lens flare and 3D bevels to anything called an
icon. If that happens, adding **"vector logo, single flat colour, no shading, no highlights"**
usually beats it into shape.

And if it simply doesn't work out, nothing here is a blocker. The mod is fully playable on
vanilla placeholders; this is polish.
