# CHANGELOG

## Audit of the summons' shouts (2026-07-31)

Asked for as a precaution. Five things checked on the Ancient Dragonborn, one defect fixed, one
balance question raised rather than answered.

### What is correct

- **The three-shout cycle works.** `which = ((shoutCycle % 3) + 3) % 3` then `shoutCycle = which + 1`
  gives 0,1,2,0,1,2 — all three appear, which was the point of cycling rather than rolling.
- **Ally safety is evaluated ONCE, before the shout is chosen** (line 352 against 365), so a single
  `ConeIsClearOfAllies` covers all three. That was the design and it holds: Unrelenting Force reuses
  the breath's range and cone deliberately, so the check can never drift from the thing it guards.
- **Fire ignites pawns but NOT the ground** (`igniteGround false`), keeping Storm Call's invariant.
- **Force is Blunt + spread**, mirroring the Dovahkiin's own fus ro — cutting damage spread over
  many parts kills by cumulative blood loss, which is not what a shove should do.
- **Frost is Frostbite + spread + a 90-tick stun + snow**, with no ignition.

### The defect: his frost was firing the FORCE fleck

`Dovahkiin_Fleck_FrostWave` exists and the Dovahkiin's own Frost Breath uses it at all three
levels. The summon used `Dovahkiin_Fleck_ForceWave`.

*Root cause, and it is worth more than the bug:* **`FrostWave` had no field in `DovahkiinDefOf`.**
Force and Fire did. Whoever wrote that branch used what was reachable. **A DefOf class is a menu,
and code orders from the menu** — anything left off it gets silently substituted rather than
missed, and the substitution then reads as a design choice to whoever reviews it later.

*And the cost was NOT what it looks like.* Not colour: `Thing_ShoutWave` sets `instanceColor` per
particle and these flecks omit `renderInstanced`, so the pale blue was always applied. It was
**timing** — frost holds 0.20s and fades over 0.55, force holds 0.16 and fades over 0.45. His frost
cleared the air about a fifth faster than the Dovahkiin's, reading as a pressure wave that happened
to be blue rather than as ice hanging in the air.

### Raised, not changed: his Unrelenting Force has ZERO armour penetration

| | armour penetration |
|---|---|
| Dovahkiin's own Unrelenting Force | **0.75** (`CompAbilityEffect_Shout` default) |
| summon's fire and frost | 0.35 |
| **summon's Unrelenting Force** | **0** |

Blunt damage with no AP is **fully reduced by blunt armour**. This changelog already records that
exact failure once: Soul Tear shipped at 0 and read as "completely broken" against an armoured
modded raider. Against anything plated his Force will do nothing while his fire and frost still
land.

It looks like an oversight rather than a decision, but it is a balance number and those are the
user's call. **Not changed.** 0.35 would match his own other two shouts.

### Call of Valor's shouts do not exist

Checked rather than assumed: **0 C# lines** mention Valor and the only two def references are
comments. There is no Fus Ro Dah and no Frost Breath to audit — they arrive with the summon.

---

## Dragon Aspect: the armour stayed STANDING over a downed Dovahkiin (2026-07-31)

Reported precisely: the Dovahkiin went down and her armour stayed upright over her, followed by
*"I dont understand what messed everything up like this."*

**Nothing messed it up. This has been here since the overlay was written.**
`Thing_DragonAspectOverlay.cs` was last touched at `fe33e61`, before any of this session's art
work, and **no C# in the mod has changed since 2026-07-30** — checked with `git log`, not assumed.
The gap simply needed the Dovahkiin to be downed *with Dragon Aspect up* for anyone to see it.

*Root cause:* every draw in the overlay used `Quaternion.identity`. That is upright and only
upright, so the moment RimWorld laid the body over, the overlay carried on as though nothing had
happened.

*Fix:* borrow **`Verse.PawnRenderer.BodyAngle()`** — public, returns a float, **verified by
reflection over 1.4's own `Assembly-CSharp`** rather than assumed. It is the same value
`PawnRenderer` uses to lay the body down, so armour and body can no longer disagree about which way
is up. The same reasoning as taking the body's mesh from `GetHumanlikeBodySetForPawn` instead of
inventing a size: **when the engine already computes the number, use ITS number.**

### The part that is easy to get half-right

**Rotating a sprite is not enough — its OFFSET has to be rotated too.**

- `BaseHeadOffsetAt` returns the head's position in the pawn's *own* space, "up from the chest".
  On a downed pawn that vector still points up the screen while the body it belongs to lies
  sideways. Rotating the helm without rotating its offset leaves a correctly-tilted helm floating
  above her chest instead of on her head.
- The weapon's hold offsets say "out to his right, slightly back" — true only while standing. They
  are now built as a local vector, rotated by the body, and its hold angle has the body angle added.

`bodyQuat` turns about Y, so it never touches altitude; those are set afterwards.

Build clean, 0 warnings. **Needs a playtest**: down the Dovahkiin with Dragon Aspect at level 3 and
check the armour, the helm and — on the Ancient Dragonborn — his weapon all lie with the body.

---

## Ancient Dragonborn: lifetime 1.5h -> 6h, and it was never a regression (2026-07-31)

**Test 5's core passed.** The user reported the timer still running after a save and reload —
which is the thing `RISKS.md` section 9 exists to protect, and it has been outstanding since his
very first playtest round.

Two faults reported alongside it. The first turned out not to be a fault at all.

### "His timer is back to 1 hour, what happened in the files?"

**Nothing happened. It has always been 3750 ticks.** The save settles it beyond argument — his
hediff read `ageTicks 463` and `ticksRemaining 3287`, summing to exactly **3750**, the C# default
the summon was built with. There was no XML override to lose and no edit to regress. What the user
saw as "about an hour" was 3287 ticks remaining, 1.31 hours, of a 1.5-hour life.

*The record disagreed with the user, and the user wins:* the notebook carried "1.5 in-game hours"
as **settled, do not re-litigate**. Their new figure is **6 in-game hours**, so
`ancientDragonbornLifetimeTicks` is now **15000**. Call of Valor's hero is meant to be double
that — **30000, 12 hours** — and gets his own field when that summon is built. The notebook entry
has been corrected rather than left to contradict the code.

### And a real gap found while fixing it: NONE of his tuning was reachable

`ancientDragonbornLifetimeTicks` existed **only as a C# default**. So did every other
`ancientDragonborn*` number — breath damage, cooldowns, cone, assist radius, all of it. None
appeared in `DovahkiinTuningDef.xml`, which means **none of them could be retuned without a
rebuild**, and `CLAUDE.md` requires the opposite in as many words: *"All tuning numbers go in
`Defs/DovahkiinTuningDef.xml` or mod settings so I can retune without a rebuild."*

The lifetime is now exposed there. Edit the line, restart the game, no build. The rest of his
numbers should follow the same way — logged rather than done in passing, since it is a sweep and
not a one-liner.

Build clean, 0 warnings. All def files parse.

---

## Call of Valor: the hero's blue through the hilt (2026-07-31)

A gradient of his blue from the pommel up to the **second crossguard**, fading to nothing there.
The blade above keeps the grey steel. Two knobs: `$HILT_BLUE_MIX` 0.55 at the pommel,
`$HILT_BLUE_END` 0.352 where it reaches zero.

**The blue is darkened but NOT greyed, and that is the point of adding it.** `$GREY_MIX` is what
pulled the weapon toward steel in the first place; putting the blue through it as well would cancel
the request before it drew a pixel. It takes `$VALUE_MUL` only, so it sits in the same value range
as everything around it and reads as more *colour* rather than as a brighter patch.

**`C_AZURE` specifically** — the aura's own colour on his armour, the most saturated blue he
carries. The hilt is quoting a part of him rather than a blue picked because it looked nice.

**Smoothstepped, not linear.** A straight ramp to zero leaves a visible band edge exactly where two
pieces of furniture already meet, and that reads as a drawing seam rather than as colour running
out.

---

## Call of Valor: variant A settled, and the palette handed over properly (2026-07-31)

**Variant A is the weapon** — katana tip, short kissaki. Settled by the user; B (glaive) and C
(spirit-blue) stay in the preview sheet as reference only.

And the outstanding palette handoff is done — the one the notebook has listed as *"NOT DONE. His
colours are settled; the sword still uses its own lookalike ramp."*

### It was a lookalike, and that is a real defect rather than a tidiness complaint

| role | sword had | armour had |
|---|---|---|
| bright end | (196,232,255) | (206,232,252) |
| dim end | (120,168,216) | (120,162,200) |
| bloom | (168,216,255) | (120,196,255) |

Near enough that nobody could tell on the two sprites separately. Far enough that they were **two
palettes**, and two palettes drift the moment either is retuned — with the drift only showing when
the weapon is in his hand, which is the one view nobody renders while tuning either piece.

*Fix:* `Tools/ValorPalette.ps1`, the single source, dot-sourced by both generators. The armour's
`DOVAH_PALETTE=valor` block no longer carries the fourteen values; it reads them.

**Mapped BY ROLE, not by eye** — each entry given the job it already does on the armour:

- `C_HOT` his hot edge → the weapon's luminous rim
- `C_GOLD` his lit face → the bright end of the blade's gradient, toward the tip
- `C_MID` his pale steel-blue → the dim end, at the hilt
- `C_AZURE` his aura's second colour → the weapon's outer bloom

`C_DEEP` is deliberately **not** used for the body: it shades plate, and at 104 alpha on a
translucent blade it would only make the hilt murky. Matching by role is what makes this a handoff
rather than a coincidence.

### And then the opacity, where a shared constant turned out not to be a shared appearance

The user's next note: the weapon still read as **more ghostly than the hero**. It did. It had been
set at `$BODY_ALPHA` 104 back when the armour was a faint scale field, and the armour has since
been rebuilt around solid plate — the weapon's number simply never followed.

**The first correction was 152, to match the cuirass's own constant, and it was wrong.** Measuring
the finished textures caught it: median interior alpha came back **sword 173, cuirass 215** — still
42 points apart after supposedly matching.

*Why:* the cuirass is not one fill. It is a plate body, then pectoral domes, then creases and lit
lips, then a rim — four or five translucent layers accumulating over one another — while the blade
is essentially one. **The shared constant is not the shared appearance.** Two pieces match when
their *composited* results match, and that can only be established on the finished art.

At 196 the blade measured **206 against the plate's 215**, inside 4%. The user then asked for a bit
more still, so parity turned out to be the waypoint rather than the destination: **220**, measuring
**225 against 215**, puts the weapon slightly *above* his armour. Defensible on its own terms — a
blade is a forged object where the plate is a translucent overlay on a body.

**Still translucent, and that is the floor this must not cross.** "You can see the ground through
it" is the one thing none of the pre-2026-07-30 attempts managed and the whole reason this file was
rebuilt from scratch. Measured after every change rather than assumed — 225 of a possible 255.

### The grey went in, then came back out — and measurement is what settled it

Asked for in two steps, "a tiny bit of grey" then "slightly grey-darker", reaching `$GREY_MIX 0.30`
and `$VALUE_MUL 0.88`. The user then reported the weapon as **still too ghostly** and asked for the
chestplate's colour instead. Those two requests pull against each other, so the textures were
sampled rather than argued about:

| | median RGB |
|---|---|
| chestplate | (159,195,220) — a proper pale steel-blue |
| sword, greyed | (168,186,202) — flatter, redder, far less blue |

**The grey was causing the very thing being complained about.** Desaturating a translucent object
pushes it *toward the mid-tone of the ground behind it*, so it loses separation and reads as
vapour — which is what "ghostly" describes. Opacity had already been raised twice by then and could
not fix it, because opacity was never the problem.

**The rule worth keeping: saturation is what separates a translucent object from lit ground. Value
and alpha alone cannot do it.** This is the cousin of the additive-glow lesson — there, white light
on brown ground read as cream; here, a desaturated body on brown ground read as nothing at all.

`$GREY_MIX` is now **0.00** and `$VALUE_MUL` **0.90** — grey removed entirely, value trimmed only
enough to sit on the plate's own brightness. The blade measured **(164,192,214)** against the
plate's (159,195,220): within 6 in every channel. Both knobs are left in place at neutral rather
than deleted, so the greyer look is one number away if it is ever wanted back.

### Then the interior alone, down to the helmet's value

Asked for as "as dark as the helmet", and the three pieces turn out to be further apart than they
look:

| | median RGB | luma |
|---|---|---|
| helmet | (151,166,179) | 164 |
| chestplate | (159,195,220) | 189 |

Mean channel ratio helm/sword gave **0.874**, and that is `$INTERIOR_MUL`. The blade now measures
**(145,170,189), luma 166** against the helm's 164.

**Value only — the helm's FLATNESS is deliberately not copied.** The helm is less saturated than
the plate because its dome runs down to `C_DEEP` at the edges, but desaturation is precisely what
made this weapon read as vapour two rounds earlier. Taking the helm's colour wholesale would have
walked straight back into it. The ask was *darker*; darker is what it got, and the blade keeps a
44-point channel spread against the helm's 28.

**Applied to the body and the hilt's blue, NOT to the bloom or the rim.** The bloom is the halo
outside the weapon and the rim is its luminous edge — neither is interior, and dimming them is how
a spectre stops being legible on lit ground. The hilt blue does take it, since it is mixed *into*
the body and would otherwise light the grip brighter than the blade it belongs to.

**Two knobs, not one.** "Grey-darker" is two requests, and this project has already been caught
treating saturation and value as a single lever. Kept apart, either can be retuned without
disturbing the other.

Applied in the sword generator and **not** in `ValorPalette.ps1` — that file is shared, and greying
it would grey his armour along with the weapon. The palette stays canonical and the sword carries a
named tweak on top, so anyone comparing the two later finds this rather than concluding they have
drifted apart again.

**Desaturated toward each colour's own luminance, not toward a fixed grey.** Mixing toward mid-grey
would darken the bright end and lighten the dim one — a contrast change wearing a saturation
change's clothes. Rec.709 luma, so the darkening is the *only* thing changing value:

| | original | after |
|---|---|---|
| bright end | (206,232,252) | (187,203,216) |
| dim end | (120,162,200) | (115,141,165) |
| bloom | (120,196,255) | (122,169,206) |

**The rim is greyed but NOT darkened**, deliberately. It is the luminous edge, and this weapon has
no keyline, no bevel and no specular — the rim is the only thing holding its shape against lit
ground. Dimming it would trade legibility for a colour note. The body and the bloom carry the
darkening instead. Being already neutral, the grey call is a no-op on it; left in place rather than
special-cased, since an exception is one more thing to get wrong later.

**The refactor is proven inert on the armour:** regenerating against the checkpoint's manifest
changed **0 of 36** files. A palette move that silently altered signed-off art would be the exact
failure this file is meant to prevent.

---

## Call of Valor: the greatsword's hilt gets its furniture (2026-07-31)

Everything above the blade root was bare — the body gradient and the rim, nothing else. The blade
had its meander and its centreline while the whole handle carried no information at all. Now:

| piece | what it gets |
|---|---|
| pommel | a cap arc across the butt, a raised centre boss, two nicks |
| both grips | cord wrap — 9 bands on the lower run, 8 on the middle |
| both guards | a collar round the tang, a moulded seam down each arm, a boss near each arm's end |
| upper guard | two **langets**, tongues reaching up onto the blade root and clasping it |

**All of it is interior.** Every stroke is inside the existing clip region, so the outline cannot
move — the silhouette is signed off, and this is detail drawn *on* it rather than a reshaping of
it. That is exactly the distinction the arms got wrong, and the clip enforces it rather than
leaving it to care.

**Curves, and for a reason rather than for its own sake.** Every one of these features wraps a
*round* object: cord spiralling a grip, a collar round a tang, a langet clasping a blade. Seen
flat, each is an arc — drawn as a straight line each reads as a sticker laid on top. The bow is
small, 0.002–0.004 of the weapon's length, about a pixel and a half, and it is the whole difference
between "wrapped" and "striped".

Two details worth keeping:

- **The wrap bands all lean the SAME way.** Alternating the lean reads as a lattice, which is a
  different binding entirely and not the one this weapon has.
- **The collar is what makes a guard read as a separate forged piece** rather than as a wide spot
  in the outline. Without it, a crossguard drawn as one continuous silhouette with the grip looks
  moulded from the same billet.

---

## Two blade variants explored from a sketch, both set aside (2026-07-31)

Recorded so nobody re-runs the exploration. The user sketched a barbed blade and asked for it in
two steps, then reverted both: *"let's stick to the weapon's version before the last two
modifications."*

1. **The greatsword's blade reprofiled** to four barbs. Worth keeping from it: a notch must **hold**
   its narrow width before stepping back out — stepping in and straight out over 0.010 of the
   length is under 3px on the sprite, so the two diagonals meet and the notch collapses into a
   wobble in the edge.
2. **An entirely new weapon** from the reference alone, every inherited constraint dropped. Its one
   real finding: `$PROFILE` stores **one half-width per station and mirrors it**, so both edges must
   notch in the same place — and the sketch's barbs are **staggered**. Expressing that needs two
   independent edge tables, which is what the asymmetric tip already does one side at a time. If a
   staggered blade is ever wanted, that is the change.

**Neither was measured.** The sketch was pasted into chat and never written to disk, so there was
nothing to trace — unlike the battleaxe, which `extract_blade.ps1` pixel-traced from a real file.
Both attempts were eye transcriptions, and that is very likely why neither landed.

The shipping weapon is unchanged and **verified byte-identical** to the version in the checkpoint.
`GenerateValorGreatsword.ps1` was reverted via git rather than hand-edited back, and
`GenerateValorBlade.ps1` deleted.

### Two encoding lessons while reverting, and the second corrects the first

Removing the two entries with `Set-Content` **added a BOM and rewrote every line** of a document
full of em-dashes — `git diff` showed 664 insertions against 664 deletions on a file that should
have lost 65 lines. Restored from git, which was clean because those entries had never been
committed. **Use the Write/Edit tools for any file containing non-ASCII. Never `Set-Content`.**

**But the check that "proved" it was corrupt was itself wrong, and that is the more useful half.**
Scanning with `Get-Content -Raw` reported 592 mojibake sequences — and reported them again on the
*repaired* file. In Windows PowerShell 5.1 `Get-Content` defaults to **ANSI**, so it decodes a
perfectly good UTF-8 em-dash as `â€"`. The mojibake was in the reading, not the file.

**To check an encoding, read the BYTES, or read with the encoding named explicitly:**
`[System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)`, then count `U+FFFD` and look
for `E2 80 94`. The notebook already warns that the obvious reverse-decode repair makes these files
worse — this is why: **the diagnostic lies in exactly the way that invites the repair.**

---

## Call of Valor: an entirely new helm, and no horns (2026-07-31)

The old helm is gone — not adjusted, replaced. Nothing of Dragon Aspect's is reused: not the
serrated skull cap, not the scale field over it, not the horns. `BuildValorHelm`, dispatched from
`BuildHelm` in one line so Dragon Aspect's own helm is untouched.

It is built from **the same vocabulary as the cuirass**, which is what makes it read as part of the
suit rather than as a hat:

- a **skull drawn as a closed curve and filled with a `PathGradientBrush` dome**, exactly as the
  pectorals are — a helm is a rounded mass and wants radial shading, not a linear ramp
- **plate seams as a dark crease with a lit lip under it** — the same pairing that made the
  pectorals read as muscle does the job here for a joint between two pieces of plate
- a **hot rim** on the outline, matching every other piece

Per rotation, because a helm is not symmetric: south gets a brow band dipping over the nose, two
eye slots either side of a **nasal bar that is not drawn** — it is the helm surface left between
the slots, which is how a real nasal works and costs nothing — and cheek seams sweeping to the
chin. East gets one slot forward on the profile. North gets no face at all, just nape seams.
**In place of horns there is a comb**: a raised ridge over the crown, which gives the helm a top
without adding anything that points outward.

**Sizing was inherited, not re-derived.** Those numbers were tuned against the *game* in an earlier
playtest round, and this script's own preview sheet draws the helm at about 60% of its real size —
it cannot be used to judge them.

### Two faults in the first render

- **The pawn's face read straight through it.** At the body's 152 alpha the eyes and features were
  clearly visible under a *closed* helm, which looks like a bug rather than a design choice. Raised
  to **205**, and the gap from the cuirass is deliberate: SPEC 4.4d wants apparel to read under the
  body plates, but a helm has the opposite job — it is a solid object over a face.
- **On east the cheek seam ran from crown to chin down the middle of the profile**, which reads as
  a crack across the face rather than as a joint. It now runs from *behind* the eye slot down and
  backwards, where two plates would actually meet. The comb was pulled up onto the crown for the
  same reason — run further down a profile it becomes a second seam.

### Then: hussar wings, and a coronet in place of the eye slots

The user's next note — *"looks too round"* — and they were right. A smooth dome has no silhouette,
and this project's own rule is that an overlay which does not break the outline does not read.

**Wings**, five feathers per side, **fanned by angle** — the same lesson the shoulder fins taught,
that three shapes at one angle in descending sizes render as *one* shape because the smaller ones
sit inside the largest. A feather is deliberately not the fin's blade: it is widest a third of the
way up, narrows again at the root, and carries a **quill** — one bright line down the spine, which
is what separates a feather from a leaf. Drawn *before* the skull so it laps their roots and they
read as mounted rather than stuck on.

**And the eye slots are gone**, replaced by a **coronet**. Those dark slots read as eyes rather
than as armour. A coronet says the same thing — *there is a face under here* — without drawing
one. It is a circlet, so it goes all the way round: five fleurons front and back, three in profile.

Three faults in the first pass at these, all of them size or placement:

- **The wings were half the helm's height and rooted on the crown**, fanning from near-vertical.
  They came out as small tufts clustered at the top corners and changed the silhouette not at all —
  which was the entire point. Now 0.74 of the half-height, rooted at the **temple**, fanned 10° to
  82° so they sweep *out* rather than up. **A wing has to be comparable to the thing it is mounted
  on.**
- **The fleurons were drawn inside the skull's clip**, so they were confined to the dome and read
  as five triangles painted on the helm. A coronet is a separate object worn *over* a helm — it has
  to be allowed past the outline. Heights now fall away hard from the centre (0.56 to 0.16): five
  points of similar height read as a saw blade, one dominant point with the rest stepping down
  reads as a crown.
- **On east the wing was rooted at −0.20**, deep inside the skull. Since the skull is drawn over
  the wings, only the feather tips cleared it and the profile had almost no wing while south and
  north had a full fan. Moved back to −0.62.

The **comb was removed outright**, not shortened. The coronet occupies exactly that strip of crown
and two features sharing it read as one muddle.

Change scope: **3 of 36** files — the three helm rotations, nothing else. Dragon Aspect untouched:
36 of 36 byte-identical.

---

## Call of Valor: the scale field is gone entirely — no scales on any view (2026-07-31)

The arm band keeps **exactly** the shape it always had — `BuildArmsPath`'s own sleeve, same outer
line — and is filled smooth instead of scaled. Nothing else about it moved. And the **dragon-scale
field is removed outright**, which is what actually cleared the scales.

### It took three goes, and the first two were aimed at the wrong layer

1. **Cut the fur's outline out of the scale field.** Correct for the fur, and the scales still
   covered everything else.
2. **Empty the ARM BAND of its own scales.** The user's report: *"you took the scales away on one
   pic, then they are still on the others."* They were right, and it was not a per-rotation bug —
   the arms were scaled on **every** view including the one I had checked, and I had looked at a
   single south crop and called it done.
3. **Remove the field.** That worked.

*Why (2) could never have worked:* **`BuildTorsoPath` spans the ENTIRE silhouette, arms included.**
It is not a torso-minus-arms shape. So the scale field was painting the arms no matter what the arm
band drew, and the smooth arm fill at ~112 alpha was simply layered on top of a scale pattern that
was still underneath it.

**The general trap: a clip named for one part of the body may not be limited to it.** Check what a
path actually covers before concluding that the feature drawn inside it is the thing you can see.
And the method lesson under that: **verify a "remove X everywhere" change on every view before
reporting it**, not on the one crop that is convenient — a three-rotation zoom took one script and
showed the truth immediately.

Nothing is left bare. Every region now carries its own treatment — cuirass over the trunk, belt,
fur, arm bands — and the plate's edge (0.755 of the half-width at the chest) overlaps the arm
band's inner edge (~0.70), so there is no seam to expose.

### The correction, recorded because the mistake is an instructive one

Told *"remove the scales from the arms"*, the first attempt replaced the band with four articulated
lames — rerebrace, couter, vambrace, cuff. The user's answer: *"just the scales, don't change its
shape. keep its outer lines as it was but remove the details/drawings inside of it."*

They were right, and the failure was not one of craft — the lames were fine. **It was scope.** The
instruction was about what is drawn *inside* an outline; I changed the outline, and the outline was
part of a silhouette they had already approved and checkpointed. *When an instruction names a
surface treatment, it is not licence to redraw the form underneath it* — especially not one that is
already signed off. `DrawArmPlates` is kept, uncalled and clearly marked, purely so the geometry is
not lost.

### What the smooth fill needed anyway

- **One side at a time.** A single `LinearGradientBrush` over both arms' combined bounds would
  light the left arm and shade the right, because a linear gradient only knows its bounding box.
- **Shaded ACROSS the limb, not down it.** An arm is a cylinder seen along its length, so the
  gradient runs outer-edge to inner. The cuirass's vertical 90° made the arms read as flat ribbons.
- **A thin edge, fainter than the cuirass's rim.** Not a new detail — the scales' own boundary was
  what made that outline crisp, and removing them removed the edge with them.

**And the level-1 trap, which nearly went unnoticed:** the arm band is the **only thing drawn at
level 1**. Emptying it without a fill would have left word 1 of Dragon Aspect with no visible
effect whatsoever on this build. The fill is a little stronger there, exactly as the scales were.

Dragon Aspect untouched: 36 of 36 byte-identical.

**Still Dragon Aspect's on the arms: the ELBOW SPIKES** — now the only dragon element left on the
limb. Untouched, because the instruction was the scales.

---

## Call of Valor: the fur runs the whole lower body (2026-07-31)

The user's correction: the fur should cover the whole lower part, down to the extremities.
`$F_FUR_BOT` 0.888 → 0.980, so the skirt now reaches the bottom of the body sprite instead of
stopping around the upper thigh. Its width still comes from the body's own half-side per row, so
it narrows with the legs rather than hanging square.

### Extending it broke the strands, and the reason is worth keeping

**Everything sized as a fraction of the band's own height silently doubled**, because the band's
height doubled — 0.138 of the body to 0.230. Strand widths went from ~3px to ~6px and the fur
turned to rope. *A dimension expressed as a fraction of a region scales with that region, which is
right for anything that should grow with it and wrong for everything that should not.*

**Strand size now comes off the PITCH** — the band's width divided by the strand count — which is
what actually decides whether strands fill the width or freckle it, and which does not move when
the band grows taller.

**And one run of full-height strands is spaghetti, not fur.** At 29px tall and 3px wide a strand
is a noodle. Real fur of any depth is layered, so this is too: **three overlapping tiers**, each
rooted lower than the one above, each offset by half a pitch so they interleave rather than stack,
and the lower ones drawn last so they lap over the tier above exactly as shingles do. Tone still
alternates, now on `(strand + tier)` so the interleaving does not line up matching tones.

A dead pre-tier span block was removed rather than left sitting above the loop that replaced it —
code that looks live and is not is exactly what misleads a later session.

---

## Call of Valor: the fur becomes zigzag strands, and the scales are cut out of it (2026-07-31)

The user's instruction, and the first half of it is the part that mattered: **take the scale field
out of the fur, then replace it with randomly curving pointy zigzags.** The scales were clipped to
the whole torso, which includes the fur band, and **a dragon-scale grid inside a fur skirt cancels
the fur outright** — no amount of strand drawing survives a regular pattern behind it. The fur's
outline is now a reusable `BuildFurPath`, and the scale fill runs against a `Region` with that path
excluded.

### On "fur strands are not drawable" — this is not a reversal of it

A single strand is 2–4px and does not **resolve** at play distance. That was true and still is.
But a strand does not have to resolve to do its job: what is being drawn is a **texture made of
many**, and en masse they read as *broken, hairy, not metal* at any zoom. Close up you see strands;
at 48px you see a band that is not smooth. Both are wanted. What genuinely cannot be done is a
strand meant to be looked at individually — and none of these are.

Three things make a zigzag read as hair rather than as a scribble:

- **it must come to a POINT** — width tapers to zero, so the strand *ends* rather than stopping. A
  constant-width zigzag is a piece of wire.
- **the zig amplitude must shrink towards the tip too**, or the point sits at the end of a wide
  oscillation and reads as a lightning bolt.
- **each strand needs its own curvature, applied as t²** so it is straight at the root and bends
  near the end. All curving alike looks combed; none curving looks like a comb.

`AddPolygon`, never `AddClosedCurve` — a curve tension rounds the zigzag's corners off and it
becomes a wavy ribbon. **The corners are the fur.**

### Two passes, and the second was only informed because it was MEASURED

- **Pass one: the strands were sub-pixel.** Width at 0.052 of the band's height drew them **0.9px
  wide** — they existed, were correct, and were invisible. *Scale detail against the region's
  actual pixels, not against intuition:* the fur band is only ~17px tall and ~72px wide on a male
  south sprite.
- **Pass two looked identical, so it got diffed rather than tweaked again.** The texture had
  **1516 pixels changed across exactly the right rows** and the strands covered ~70px of a 72px
  band. They were drawing perfectly. **The density was the problem, not the size:** rolling each
  strand's tone independently across one range put neighbours at similar values often enough that
  the band read as one flat mass, and with no gaps there was nothing else to separate them.

*Fix:* **alternate dark and light rather than rolling**, so every strand contrasts with the two
beside it, with an occasional hash-driven flip so it does not read as stripes. Same rule the aura's
particle sides already use — for a binary choice that has to come out even, alternate, do not roll.

All hashing is deterministic. Randomness would change the art on every regeneration and make the
checkpoint's manifest worthless.

---

## Call of Valor: the cuirass sat INSIDE the pawn on the side view (2026-07-31)

Reported by the user: on east, the chest, abs and belt all sat inside the body. They were right,
and the cause was that the **front view's width fractions were being reused on the side view**,
where they are the wrong number twice over:

- **The front and back views are inset because of the ARMS.** Roughly the outer 30% of that
  silhouette is arm, so a cuirass has to stop short of it. **Side-on there is only ONE arm** —
  `BuildArmsPath` is called with `@(-1.0)` for east, the column down the *rear* edge — so the
  front half of the side view has no arm over it and no reason to be inset at all. That bare
  strip of pawn in front of the plate is exactly what read as "the armour is inside him".
- **And side-on the torso is seen in DEPTH, not in width.** Vanilla plate measures 0.75–1.04 of
  the body's own side profile through the torso, against wider-than-body everywhere on the front.
  Nothing about the front's numbers transfers.

*Fix:* east rescales the profile so its **widest point lands on a per-side target** — 0.965 on the
front, 0.880 on the rear where the arm column sits. Rescaling rather than clamping keeps the
profile's *shape*: the neck narrowing and the waist draw-in survive, where a clamp would have
flattened the whole chest onto one value.

### The pectoral was measuring off the wrong thing, and that only showed here

`DrawPectoral` took its widths from the **body**, while `DrawAbdomen` already took them from the
**plate**. On the front views those are proportional and nobody could tell. On east the plate moved
outward and the pectoral did not, which would have left the muscle floating inside its own armour.

Both now derive from `PlateEdge` and divide the plate fraction back out, which is **exactly** a
no-op on the front and back — `PlateEdge` is `bodyHalf × frac`, so dividing by `frac` returns
`bodyHalf` and every fraction below keeps the value it already had.

**Proven, not asserted:** regenerating against the checkpoint's manifest changed **5 files of 37**,
and all five are `DragonAspect_L2_*_east.png`. Every south and north texture is byte-identical.

*The rule worth keeping:* **a feature drawn ON a plate should be a fraction OF THAT PLATE, never of
the body underneath it.** Then it follows the plate on every rotation by construction, and a
per-rotation change to the plate cannot leave its own detail behind.

---

## Call of Valor: a curving belt and a fur skirt below the abdomen (2026-07-31)

The third piece, below the abdomen. Drawn **in the order the layers are worn** — fur first, hanging
from under the belt, then the belt over its top edge — because each has to lap the one above, and
in any other order the seams show. Each band also **overlaps the one above by a few thousandths**:
butted edge to edge they leave a hairline of bare pawn between them on some body types and not
others, which reads as a gap in the armour rather than as layers.

### The belt, and the two things the first version got wrong

Both reported by the user, both worth keeping because they are general:

1. **It took its width from the BODY**, at a flat 0.825 of the silhouette, so it did not line up
   with the armour it is strapped over. The extents now come from the **cuirass's own edge** —
   `PlateEdge` at the plate's lower rim, per side independently — so the belt ends exactly where
   the abdominal plate ends.
2. **It was a straight band with a sag, and a sag is not curvature.** A belt goes *round* a
   roughly cylindrical waist, so from the front it is an **ellipse**, and the near part of an
   ellipse is its **lower arc**: the middle of the visible band sits below its two ends, and both
   edges drop together so the band keeps a constant width. That is what makes it read as passing
   behind the body instead of as a painted stripe. It is now built **across X rather than down Y**
   for exactly that reason — the vertical offset is a function of horizontal position, which the
   old row-by-row loop could not express at all.

Side-on the belt is seen along the ellipse's major axis, so almost none of that curvature projects;
east uses 0.35 of the bow. Applying the front's value there bends it like a banana.

The **clasp** is one broad boss, roughly 17×8px, sized off the belt's own half-width and dropped by
the full bow so it sits at the lowest point of the curve — at the band's flat mid-height it floated
above its own belt. Buckle teeth and a prong are the 2–4px features that cannot be drawn here; one
clear shape at the centre says "belt" far better than hardware that resolves to mush.

**And it was drawn on the BACK as well.** Caught by the user on the north preview: *"belts only
have one buckle, not two."* The test was `$rot -ne "east"`, which passes for north. The pawn wore a
buckle front and rear.

*The general trap, and this file is full of per-rotation code so it is worth naming:* **a feature
that is not symmetric front-to-back needs an explicit `south` test, not an "everything except the
side view" test.** North is a different view of the same object, not a mirror of the front — which
is exactly the reasoning that already gives north shoulder blades instead of pectorals, a spine
instead of a sternum, and erector spinae instead of abs. The clasp was simply missed when those
were done, and an `-ne` test is what let it slip through: it opts a rotation *in* by default.

### The thigh plates are OFF, at the user's request

*"Delete the upper thighs part for now."* The word is **for now**, so `DrawTasset` is kept intact
behind `$DRAW_TASSETS = $false` rather than deleted — its two-lame construction and its centre-gap
fractions took a round to get right and would otherwise have to be re-derived. Same treatment
`GenerateValorArmour.ps1` got when the normal-plate route was dropped.

### The fur, stated honestly — because this project has been here before

Fur **strands** are 2–4px at this scale and cannot be drawn. That was true when the normal-plate
armour failed on exactly this, and it is still true. What can be drawn is fur at **silhouette
level**, and the difference is where the information lives:

- a **ragged, tufted lower edge** instead of a clean one — the outline carries it. The tuft profile
  is two sine waves at incommensurate frequencies, **deterministic, not random**: randomness would
  change the art silently on every regeneration and make a hash check against the approved snapshot
  worthless.
- **matte shading**, and deliberately **darker in value** than the plate either side of it.
- and **no hot rim**. Every other piece of this armour gets a bright specular edge; the fur does
  not, and that *absent* highlight is what says "this is not metal" more than any texture inside
  the shape could.

Read it as a fur-shaped mass, not as strands. That is the honest ceiling at ~102px per pawn.

**One layering bug, found on the first render:** the fur was drawn and then almost entirely covered
by the thigh plates, so it might as well not have existed. The fur's hem now hangs **lower than the
plates begin** (0.888 against 0.830), so it shows in two places — a band above the plates, and down
the gap between them.

Dragon Aspect untouched — 36 of 36 byte-identical.

---

## Call of Valor: the ABDOMINAL half of the cuirass (2026-07-31)

The second half of the chest plate. The cuirass now runs from the throat to the bottom of the
abdomen — `$PLATE_PROFILE` extended from 0.520 to 0.712 of the body's height, with a real waist
in it (narrowest 0.612 at 0.545, flaring back to 0.646 over the hips) rather than a straight
taper.

**The emphasis is deliberately inverted from the chest, and that is the whole design.** Six
abdominal segments occupy roughly the space of one pectoral, so at the 48px the game is played at
they *will* blur — that is arithmetic, not pessimism. So the segments are drawn soft, and the read
is carried by the three forms that survive a downscale:

- **the linea alba** — and the sternum groove is *extended* into it rather than a second line
  being started, because two grooves that stop just short of each other read as a mistake in the
  art. One continuous channel, throat to belly.
- **the iliac line** — the long diagonal from the flank to the groin. The single most
  recognisable curve on a muscled cuirass and the biggest form in the lower half. Dark, with a lit
  lip on its inboard side.
- **the plate's own lower rim**, which the outline already carried.

Zoom in and there are abs; zoom out and there is a waist, a centre line and a V.

Each row is **narrower than the one above** (0.720 → 0.665 → 0.560 of the plate's half-width) and
each row's outer end **sweeps up**. Both are real anatomy — the rectus tapers as it descends and
the segments follow the ribs outward — and without them a stack of equal rectangles reads as a
radiator. Per segment: a `PathGradientBrush` dome, a dark lower edge and a lit upper edge. Doing
the crease-and-lip pairing *per segment* means the divisions between rows fall out of the shapes
themselves, so no grooves have to be drawn between them.

**North gets one long erector-spinae mass per side instead of three rows.** A back has no rectus
and no transverse divisions; stamping the front's segments onto it would be the same class of
error as reusing south's widths on east.

Contrast was raised once after the first render (dome surround 0.46 → 0.52, lower crease 0.72 →
0.82, upper lit 0.48 → 0.56) — soft was landing as absent.

**Note on the snapshot:** `Tools/ValorApproved_2026-07-31/` was taken *before* this entry, at the
user's request, and holds the pauldrons-and-chest version they called "the best of every version
we had until now". It is deliberately not updated here — it is the restore point this change was
made against. If the abdominal version supersedes it, re-snapshot then.

---

## Call of Valor: a MUSCLED CUIRASS, meeting the pauldrons (2026-07-31)

The next detail in the user's one-at-a-time pass: a chest plate running up to the pauldrons,
curved, precise, and *"it looks like it follows the chest muscles"*.

**Why this was drawable when the banded cuirass was not, since that looks like a contradiction.**
The normal-plate armour was rejected partly because its detail — banding, fur, buckles — is 2–4px
on a ~102px pawn and becomes noise. That reason still stands. A *muscled* plate is a different
proposition: a pectoral is a **large form**, ~26×30px here, and what makes it read is the shading
of a broad curved mass, not fine detail. Big forms survive the downscale to 48px; hatching does
not. So the pecs get domes and creases, and there is deliberately no attempt at striations, rivets
or strap detail.

**How the muscle is drawn — not with outlines.** Each pectoral is a closed `AddClosedCurve` filled
with a **`PathGradientBrush`**, whose bright centre sits at the upper-middle of the dome and falls
to a mid tone at the boundary. That is a radial falloff inside an arbitrary shape, which is what a
rounded mass does and what no linear gradient can express. Then the single most important stroke on
the piece: **the under-pec crease** — a dark arc with a *lit lip* just beneath it. A crease alone is
a smudge; the pairing is what makes it a fold. Without it, two bright ovals read as bosses riveted
to a plate.

`AddClosedCurve` here, note — the **opposite** of the crest shards and the pauldron lames, which
both need `AddPolygon` because their read is angular. Muscle is the one thing in this generator
that genuinely must be round.

Per rotation, because a back is not a front: **south/east get pectorals and a sternum groove;
north gets shoulder blades and a spine groove, and no crease at all** — a back has no overhang.
The centre line is drawn as a *groove* (dark channel, lit lip either side), not a line: a single
dark stroke reads as a crack in the plate rather than as a valley between two masses.

Every horizontal measurement is a fraction of the body's **own half-width at that row**, read from
the measured profile, so the plate follows all five silhouettes' taper instead of imposing Male's.
The outline is built row-by-row like `BuildTorsoPath`, with a `$PLATE_PROFILE` table smoothstepped
between landmarks — neckline 0.30 of the body's half-width, flaring to 0.755 across the nipple
line, drawing back to 0.58 at the waist. Chest values sit near 0.72–0.76 deliberately: the arm
bands occupy the outer `$F_ARM_W`, so anything wider would be drawn over the arms.

### Two things that had to change around it

- **DRAW ORDER IS NOW STYLE-DEPENDENT.** Fins are drawn *before* the scale field so the plates lap
  their roots and they look grown out of the body. Plate is the opposite — it is **worn**, so the
  order is scales → cuirass → pauldrons, and the pauldron laps *over* the cuirass it is strapped
  to. Using the fins' order put the breastplate on top of the shoulder piece, which read as a bib.
- **THE CREST SHARDS ARE SUPPRESSED for the pauldron build.** This is a real decision, not a
  tidy-up, and the first render is what forced it: the crest is two rows of bright crystal shards
  running down the middle of the chest — exactly the surface the cuirass occupies. Drawn together
  the shards win outright, because they are the brightest thing on the sprite, and the pectorals
  underneath simply stopped existing. **There is no alpha at which both read: they are not layered,
  they are competing for the same forms.** It is also right thematically — the crest is a dragon's
  spine breaking through the skin, which is the Dovahkiin's signature, and this hero is a man in
  armour. One line to restore.

Plate alpha ended at 152, up from 132: the scale field is drawn underneath and its regular pattern
shows through a thin plate, muddying exactly the broad smooth shading the pectorals depend on.

Dragon Aspect untouched throughout — **36 of 36 shipped textures byte-identical** after every run.

---

## Call of Valor: aura off, shoulder fins become PAULDRONS (2026-07-31)

The user's plan, in their words: *"slowly correct the details little by little to make it
different enough from the ancient dragonborn while it still looks good."* First two details.

**The aura is gone.** `DOVAH_NO_AURA` on `PreviewAncientDragonborn.ps1`, which was written for the
Ancient Dragonborn and drew one unconditionally. It nulls the ring and crescent images rather than
branching at each draw site — the ring was already null-guarded, and `DrawCrescents` now returns 0
early, so a missing texture and a deliberate omission take the same path and the caption's
"N crescents alight" stays honest. In game this is simply the valor overlay not drawing them.

**The three swept fins per shoulder become a pauldron** — `$SHOULDER_STYLE`, which follows
`DOVAH_PALETTE=valor` and is overridable with `DOVAH_SHOULDER`. Dragon Aspect's default path is
untouched: **36 of 36 shipped textures verified byte-identical** after every run in this session.

It is a genuinely different construction, not the fin with new numbers. A fin is a straight
tapering blade swung about its root; a lame is a band of plate swept along an arc about a pivot at
the shoulder joint, from outboard-below, over the joint, and down inboard across the chest. Three
lames, overlapping by nearly half, drawn outermost-first so the top one laps the ones beneath.

### Three passes, and each failure was the shape — as it always is here

- **They read as hollow wire hoops.** The hot edge stroke was `band * 0.20`, which on a *tapered*
  band is most of its width, so the fill never showed. Cut to `0.09`, and the band widened from
  `len*0.26` to `len*0.42`. **The edge has to outline the band, not be it.**
- **The fill was near-black through the middle.** The gradient ran from `C_DEEP` (34,58,84) to the
  lit tone across about ten pixels and read as a shadow with a bright rim. The dark stop is now
  lifted 42% towards the lit one. *This is the third time in this project that a "deep" palette
  stop has been used to fill a narrow shape and produced a hole* — the crest shards and the aura
  rings did the same. **A deep stop shades a broad lit surface; it does not fill a thin one.**
- **It sat too high — a raised collar, not a shoulder piece.** The sweep ran entirely *above* the
  pivot (φ 2°→150°) with radii up to `len*0.80`, throwing the top of every arc ~16px above the
  shoulder line, beside the neck. Now both ends sit *below* the horizontal (φ −28°→186°, fanning
  outward per lame) about a pivot on the joint itself.

The pivot was then pulled inboard to `len*0.18` to reach across the chest as the brief asks. That
is a **trade, not a free win**: moving it inboard also pulls the outboard end back inside the body
outline, and an overlay that does not break the silhouette does not read at all. The base radius
was raised with it to keep ~9px of overhang past the body edge.

### Still Dragon Aspect's, and deliberately not touched yet

The **elbow spikes down the arms** and the **helm horns** both go through `DrawSpur` and are
unchanged — the user asked for the shoulders. They are the obvious next details.

---

## Call of Valor wears DRAGON ASPECT'S ARMOUR after all — the user's reversal (2026-07-31)

**The user's decision, reversing their own governing statement of earlier the same day. It is
recorded here so it is not "corrected" back.**

That statement was *"he is a hero of Sovngarde, not a second Ancient Dragonborn"*, and its first
consequence was that his armour must be **normal armour in shape** — horned helm, fur-trimmed
pauldrons, banded cuirass, belted skirt — with Dragon Aspect's scales, fins and crest explicitly
forbidden. That was built: measured off vanilla plate's per-rotation ratios, fitted to each body
silhouette's own outline, rendered over lit ground at play distance. Shown. Verdict: *"the armor
still looks very...dull."* Replaced by: *"let's use the ancient dragonborn's armor model first but
with call of valor's gradient."*

**Why the first version lost, stated plainly, because it is the general lesson:** a pawn is ~102px
wide in a 256 frame. Banding, fur strands, buckles and cloth folds are 2–4px each — they are noise
at the 48px the game is actually played at, and no amount of tuning changes that. Only two things
survived the downscale: the horned helm's silhouette and the rim light. Dragon Aspect's geometry
works for the opposite reason — **its fins and crest break the outline**, and a silhouette reads at
any zoom. This is the same rule this changelog already recorded from three failed passes at the
plate ("if a pawn overlay is not breaking the silhouette, it will not read"); the normal-armour
brief could not satisfy it, because normal armour does not break a silhouette.

**Consequences 2, 3 and 4 of the original brief are untouched** — no aura, the portal cast effect,
and the 2× lifetime were not part of the reversal.

*Cost of the change: one environment variable.* `DOVAH_PALETTE=valor` on
`GenerateDragonAspect.ps1` was already written and already proven inert by default. Nothing was
redrawn.

### `GenerateDragonAspect.ps1` gained `DOVAH_DEST`, and it is a safety fix

`$DEST` was **hardcoded to the mod's own texture folder**, so simply running the script overwrote
the 36 signed-off Dragon Aspect textures — which is why the notebook warned it "must not be run
casually". A warning is not a guard. `DOVAH_DEST` redirects the output, and the valor run was done
through it, then proved: **36 of 36 shipped textures byte-identical afterwards.**

`Tools/GenerateValorArmour.ps1` is **kept, not deleted**. It holds the measured vanilla-plate
ratios and the per-rotation body-outline measuring, both of which cost real time and are the
reference for any future worn-armour art in this project.

**One trap in the preview, worth knowing before reading the sheet:** `DOVAH_OVERLAY_DIR` swaps the
whole texture set, so on `ancient_dragonborn_preview.png` *every* figure wears the valor palette —
including the cell captioned "the DOVAHKIIN, Dragon Aspect L3". That caption is wrong under an
override. The sheet's own labels assume the shipping art.

---

## Call of Valor — THE PORTAL CAST EFFECT, proved as art. No C# yet (2026-07-31)

The user's spec: *"bright white waves circling the TARGET cell like an opening portal, not a
wave from the caster."* Preview-only, per the project's own rule — prove a render approach and
show it before building anything around it. `Tools/GenerateValorPortal.ps1`,
`$WRITE_TEXTURE = $false`.

### Why this could not be `Thing_ShoutWave`, stated so it is not revisited

Every other shout in the mod is a `Thing_ShoutWave`, so the first question was whether this one
could be too. It cannot, and the reason is structural rather than a missing parameter:

- `origin` is hard-set to `caster.Position` inside `Spawn()`, and the wave is spawned there
- `BuildRings` buckets cells purely by **distance from that origin**
- `Tick` draws band `head = progress * bands` — a front marching outward
- `inward` reverses that march and does nothing else

There is no rotation anywhere in the class, and no way to seat the effect on a cell that is not
the caster's. A portal is the opposite shape: it does not travel, it **spins**, and it sits on
the target. Bending the wave class to cover both would put a rotation branch on the code path
every shout in the mod already runs through — for one shout's benefit.

So it gets its own effect: in game, `Thing_ValorPortal`, a `RealtimeOnly` Thing on the target
cell overriding `DrawAt` and drawing rotated quads — the same route `Thing_DragonAspectOverlay`
already uses for the aura, so **no Harmony patch and nothing on the pawn render path**.
`Matrix4x4.TRS(pos, AngleAxis(a, up), scale)` + `Graphics.DrawMesh(MeshPool.plane10, …)`.
`drawOffscreen` will be required — it draws where it is spawned, but the same culling rule that
killed the Ancient Dragonborn's overlay applies.

Three orbits of tapering arcs, **counter-rotating** (co-rotating rings read as one disc turning),
arc counts 3/2/4 so the composite does not repeat every 360/N degrees, plus a core and an
anchoring hairline ring. One arc sprite serves all three orbits: it is baked at `$R_ARC` 0.70 of
its half-frame, so quad size inverts to `2·R/0.70` — the single piece of arithmetic tying art to
code, named once.

### Three defects found in three passes, all visual, none of which a build would catch

- **It came out cream, not white.** A glow is *additive*, and the ground is brown ≈ (122,106,84):
  adding equal R, G and B saturates red long before blue, so a white glow reads warm at anything
  short of clipping. Fixed by biasing the **tint** cool (206,234,255) — putting the extra light
  where the ground has least. This is not a preview artefact; the game's additive shader will do
  the same thing over the same terrain. The sprites stay authored white and are tinted at draw
  time, per the standing rule.
- **The waves merged into one solid ring** at every frame past half-open. An arc's world thickness
  is `T_ARC/R_ARC ×` its orbit radius, so fat arcs are fat in absolute terms and the three orbits'
  gaussian tails overlapped once the glow was bright enough to read white. `$T_ARC` 0.082 → 0.060,
  orbits spread to 0.45/0.73/1.02, gain 1.55 → 1.42, core 0.72 → 0.55.
- **The still sheet's height was guessed** and cropped its own bottom row off.

### The preview composites ADDITIVELY, deliberately

GDI+ has no additive blend, so the portal is drawn into its own layer and summed onto the scene
with saturation (~30 lines). Alpha-blending it instead would *darken* the ground under the
effect — the opposite of what light does — and would have made the effect look weaker than it is.

### `Tools/WriteAnimatedGif.ps1` now exists, and this time it is committed

The save notebook has claimed since 2026-07-29 that a working animated-GIF writer lived in
`Tools/`. **It never did** — `git log --all --diff-filter=A` finds no such file in the repo's
whole history. It has now been written twice and lost twice. It is a real file now.

**And it was broken in a way worth recording: PowerShell's `-shl` PRESERVES THE LEFT OPERAND'S
TYPE.** `$bytes[1] -shl 8` on a `byte[]` element returns a **byte**, so the high bits shift
straight out and the answer is `0` — no error, no warning, no coercion. Every frame's image
descriptor got width 0 and height 0, and GDI+ rejected the finished file with *"the parameter is
not valid"*, a message pointing nowhere near the arithmetic. `[int]` casts on both halves fix it.
Verified empirically rather than reasoned about: `([byte]2 -shl 8).GetType()` is `Byte`.

---

## Call of Valor — art groundwork only, UNCOMMITTED (2026-07-31)

Nothing in this entry is in the mod. Every generator is preview-only (`$WRITE_TEXTURE = $false`)
and five files sit uncommitted deliberately, because the art is not signed off.

**Done:** the greatsword's shape (user picked the katana/kissaki tip), and his armour built to
**vanilla plate armour's measured per-rotation ratios** with a horned helm.
**Not done:** the portal cast effect, the 2× lifetime, the palette handoff from champion to
sword, and the summon itself.

### What was learned, since the lessons outlive the art

- **Worn armour is WIDER than the body almost everywhere.** Vanilla plate peaks at 1.63× the
  body's half-width at the gorget and 1.36× through the chest, dipping to 1.02 at the waist.
  Three passes failed because they drew plates INSET inside the silhouette, which at 256px on a
  ~102px pawn can only ever be a ten-pixel stripe. **If a pawn overlay is not breaking the
  silhouette, it will not read.**
- **Per rotation, always.** From the side vanilla plate is NARROWER than the body's side profile
  (0.75–1.04 through the torso) while front and back are wider everywhere. Reusing south's
  numbers on east is the error that once hung 58px of armour off a Hulk.
- **The helm was drawn at half size**, and that — not its shape — was what read as wrong. A head
  is 60px in a 192 frame, so ~80px on a 256 frame; a real medieval helm is 1.31× that, half-width
  ~52. It had been 27. The horns were removed on that same misreading and have been restored.
- **Vanilla textures cannot be read** — `Core`, `Royalty`, `Ideology` and `Biotech` ship no
  `Textures` folder at all. Sized Apparel ships vanilla's plate as loose PNGs, which is what was
  measured. Only ratios are used; no third-party PNG is copied or shipped, the same standard both
  weapons were held to.
- **`GenerateDragonAspect.ps1` gained a `DOVAH_PALETTE=valor` override**, proven inert by
  default: the default run was hashed against the shipped art, **36 of 36 byte-identical**.

### The ceiling, stated plainly

The user's reference is a painted render with muscled plate, fur strands and buckles. Those are
2–4 pixels each at this scale, are not drawable procedurally, and would be noise at the 48px the
game is played at. Reaching that fidelity needs hand-drawn sprites. Recorded so the next session
does not spend rounds implying it is one parameter away.

---

## The Ancient Dragonborn knows three shouts, not one (2026-07-30)

The user's call, overriding a rule this project had recorded as settled: he was rolling **fire OR
frost** once at summon and keeping it for his whole life. He is the Dragonborn's own shard, so he
now has **Fire Breath, Frost Breath and Unrelenting Force** alike.

*Cycled, not rolled.* He gets only three or four casts in a 1.5-hour life. At that sample size a
per-cast random pick routinely produces an entire summoning using one shout — which is precisely
the thing being removed. Cycling 0-1-2 guarantees all three appear. The coin flip the summon code
already makes now **seeds which he opens with** rather than being discarded, so two summons still
don't open identically.

Unrelenting Force is scaled off the Dovahkiin's own level-2 *fus ro* (knockback 4, 7 damage over
3 parts, stun 180) and pulled slightly under it — he is support, not a second Dovahkiin. Defaults:
7 damage over 3 parts, 3 cells of knockback, 150-tick stun, all in `DovahkiinTuningDef`.

Two details worth keeping:

- **It reuses the breath's range and cone deliberately.** One ally-safety check then covers all
  three shouts. Giving Force its own cone would have meant a second `ConeIsClearOfAllies` call
  with its own numbers, and a safety check that can drift from the thing it is guarding is how
  the "never breathe through an ally" rule would quietly stop holding.
- **Blunt and spread together**, as the Dovahkiin's own version does. Cutting damage spread over
  many body parts kills by cumulative blood loss, which is not what a shove is supposed to do.

`usesFrost` is gone rather than carried forward dead. An old save has no `shoutCycle` node, loads
as 0, and he opens with Fire — harmless.

---

## Phase 2j — the duplicated weapon, the other half of it (2026-07-30)

The round-3 fix removed most of the doubling but not all. Reported precisely: *"a few times the 2
weapon glitch still happens, not as much as before... when the ancient dragonborn swings facing
north."* The "not as much" and the swing were both the clue.

*Root cause: mirroring vanilla's rule was necessary but not sufficient, because Melee Animation
draws the weapon in one MORE case than vanilla does.* Read out of its own IL:

- **`Patch_PawnRenderer_DrawEquipment.Prefix` always returns false.** It suppresses vanilla's
  `DrawEquipment` entirely and draws the weapon itself. So vanilla's rule is not the one in force.
- **`IdleControllerComp.ShouldBeActive`** draws when `CarryWeaponOpenly()` is true **OR** when the
  pawn is in a `Stance_Busy` with a valid `focusTarg` and not `neverAimWeapon`.

That second branch is the melee swing and its **cooldown stance, which outlives the attack job**.
In the gap between the job ending and the stance expiring, `CarryWeaponOpenly()` is false — so our
overlay resumed drawing while Melee Animation was still drawing. A brief window, which is exactly
why it went from constant to occasional rather than disappearing.

*Fix:* the gate now also stands down for a `Stance_Busy` with a valid focus target, and for an
active Melee Animation animator (`AM.AnimRenderer.TryGetAnimator`, public and static).

**Only when Melee Animation is actually loaded.** Detected once via `AccessTools.TypeByName`, so
the baseline environment keeps vanilla's condition exactly and cannot lose the axe for a second
after every swing — which is what applying the stance rule unconditionally would have caused.
Reflection with null guards throughout; no assembly reference, per `CLAUDE.md`. The probe logs
what it found, because a silent fallback is indistinguishable from the bug it hides.

*Why north made it visible:* nothing in the draw branches on rotation before the gate, so the
mechanism is rotation-independent. Facing north our axe is drawn behind the pawn at −62°, far from
where the animation puts it, so the pair separates clearly instead of overlapping.

---

## Phase 2j — fourth playtest: he ignored a wild boar (2026-07-30)

Reported precisely: the Dovahkiin was sent at some wild boar and the Ancient Dragonborn stood and
watched.

*Root cause: a wild animal is hostile to nobody, and he had no other reason to act.*
`GenHostility.HostileTo` returns true only for faction hostility, a manhunter mental state
(`MentalState.ForceHostileTo`), a predator hunting us, a prison break or a slave rebellion — read
out of its IL rather than assumed. A boar the player attacks is none of those. So:

- `FindBreathTarget` filtered on `other.HostileTo(p)` and skipped it, correctly by its own logic
- his vanilla AI had nothing to offer either — **hunting is a work job and he has no work types**,
  and he is never drafted

So this was not a broken check. It was a **missing behaviour**: nothing connected "the Dovahkiin
is fighting something" to "help him". Which is odd for a pawn whose entire reason to exist is to
come to the Dovahkiin's aid.

*Fix:* he now joins whatever the Dovahkiin is fighting. The target is resolved once per scan and
shared by the breath and the melee nudge, so those two can never disagree about what he is
fighting.

Guards, each there for a reason:

| guard | why |
|---|---|
| player-faction targets excluded | the Dovahkiin can be ordered to attack a colonist or a tamed animal; a summoned ally piling on would be far worse than doing nothing |
| bounded by `ancientDragonbornAssistRadius` (24 cells, tunable, 0 disables) | unbounded he would chase a hunt across the map and abandon the man he exists to protect |
| downed targets ignored | already beaten — let the Dovahkiin finish it |
| only overrides idling, wandering and Goto | same light-touch rule the follow nudge uses: if he is already in a fight, that fight is his |
| the follow nudge is skipped while assisting | otherwise the two behaviours fight over him every scan, one pulling him back to the Dovahkiin and the other sending him at the target |

Both `CurJob` (player-ordered attacks) and `mindState.enemyTarget` (AI-chosen ones) are checked,
because the Dovahkiin is a colonist and can be either.

### One trap this fix walked into and back out of

`ConeIsClearOfAllies` skips anything `HostileTo(p)` and treats everything else as a friend to
avoid burning. A wild boar is not hostile — so whitelisting it as a breath *target* without also
whitelisting it *there* would have had it count as an ally standing in the cone and **block the
very breath aimed at it**. A self-cancelling change, and one that would have read as "the breath
still does not work on animals" rather than as a new bug. Both sites now take the same target.

---

## The spectral weapon reshaped: a dragonbone battleaxe, traced from the user's own drawing (2026-07-30)

The user asked for the halberd to be reshaped to Skyrim's **Dragonbone Battleaxe** — thematically
the right weapon for the Ancient Dragonborn anyway. Colours are untouched; this was a shape-only
request and it still carries the armour's ember-to-blue ramp.

Now shipping: a **curved haft** (cubic Bezier, bend concentrated in the upper half so the grip
stays straight under the hand), a **ring pommel**, a wrapped grip with two bands, a small riveted
**collar**, **two spikes** roughly perpendicular on the side opposite the blade, and a blade
**traced from the user's own painted drawing**.

### The reference orientation mattered enormously

The first reference was mirrored relative to our sprite, and three features were read wrong from
it. The user's second reference arrived already head-at-top-right, which allowed measurement
instead of guesswork, and corrected all three:

| first reading | what measurement showed |
|---|---|
| haft bowing away from the blade | bows **toward** it — the haft sits ~68px right of the pommel→socket chord at mid-height |
| a spear point running **along** the haft | two spikes roughly **perpendicular**, on the **far side** from the blade, leaning ~8° toward the head |
| solid blade | a hole pierced through it, ~0.086 of weapon length across |

### The blade is measured, not designed — and the first attempt at it was wrong twice

The user painted white over a render to mark what to remove, then said it "still feels chaotic".

**First mistake: the drawing was overridden.** The traced result was dismissed as "ragged brush
strokes" and a 6-point cleaver of our own invention was substituted. That was not what was asked
for.

**Second mistake, and the cause of the first:** the white threshold was >225 brightness, so only
the solid core of each stroke counted as painted-out. A **1–2px anti-aliased rim** along every
stroke edge came through as "keep", and *that* is what made the traced polygon look like a
scribble — the drawing's edges were straight all along. Widening to >198 plus a close+open
morphological pass made the contour follow the real edges.

**A third, nearly invisible one:** after the cleaver was swapped in, the extractor — which
dot-sources the generator — re-measured against **the new blade instead of the one that had been
painted over** (2262px of blade rather than 4659). The base polygon is now pinned in the generator
with a comment saying it must not be edited, and the traced result lives in a separate
`$TRACED_BLADE` variable.

Final method, all reproducible by `Tools/extract_blade.ps1`:

| step | result |
|---|---|
| align the screenshot to the render (ring pommel + spike tip) | scale 2.0093, **0.0px** error on both cross-checks |
| subtract the white paint from the known blade polygon | 65.2% removed, one connected blob |
| trace the boundary | 201 contour points |
| Douglas-Peucker at 1.8px | **20 points** |
| convert back through the published blade basis | pasted verbatim |

Verified: **98.1% coverage** of the shape the user left, 41px missed. The remaining difference is
the old hole (kept by their mask, so solid now) and keyline pixels.

### The hold angle was wrong, and had been all along

The art runs bottom-left to top-right, so its head points up-right at ~48° above horizontal and
the drawn direction is `angle − 48`. The shipped **145° therefore gave +97 — head pointing at the
ground, pommel in the air.** True of the halberd too; its near-symmetric head simply hid it. Now
**−70°** for south and east, the pose the user reviewed. West and north are set by the same
arithmetic but were **not** previewed and remain eyeballed, like the offsets beside them.

*Consequences to watch:* the weapon is **8.5% shorter** in apparent bbox diagonal, and the head
sits slightly nearer along the axis, so `BladeStart 0.8519` / `BladeEnd 1.5263` may want a look.
`drawSize` stays **(1.5,1.5)** because the tweak data's `Scale` must match it. The orientation
check passes — TR 3862 dominant, head at top-right — so the tweak file remains valid.

---

## Phase 2j — third playtest: the duplicated weapon (2026-07-30)

Reported precisely: the halberd behaves correctly and animates like Medieval Overhaul's, but when
he swings there are **two** of them on the pawn — the animated one plus a static one.

*Root cause: the previous fix treated a conditional as a constant.* Round 2 established that
`PawnRenderer.DrawEquipment` gates on `CarryWeaponOpenly()`, which is false for an undrafted pawn,
so the summon's axe was invisible — and the fix was to draw it ourselves in the overlay,
unconditionally. But **`CarryWeaponOpenly()` is job-dependent, not constant.** It returns true when
`CurJob.def.alwaysShowWeapon` is set, and vanilla sets that on **`AttackMelee`**, **`AttackStatic`**
and **`Wait_Combat`** — every state he occupies in a fight. So the moment he engages, the game
starts drawing the weapon too, and ours was still going. Right about the idle case, silently wrong
about the combat case.

*Fix:* the overlay now draws the axe only when the game is **not** already drawing it, by mirroring
vanilla's own gate rather than inventing an "is he fighting" test of our own. Two independent
conditions could drift apart and give a frame with two axes or a frame with none; one mirrored
condition cannot.

`CarryWeaponOpenly()` is **private** on `Verse.PawnRenderer`, but its IL shows **every member it
touches is public** — each verified against 1.4's assembly before writing — so it is reimplemented
directly, with no reflection and no private-member dependency:

```
carryTracker.CarriedThing != null   -> false
Drafted                             -> true
CurJob.def.alwaysShowWeapon         -> true
mindState.duty.def.alwaysShowWeapon -> true
GetLord().LordJob.AlwaysShowWeapon  -> true
otherwise                           -> false
```

Behaviour now: idle, following or wandering, **we** draw it (which is what round 2 fixed); fighting,
**the game and Melee Animation** draw it and we stand down. No gap in either direction, and the
animation the user confirmed as correct is untouched.

Also settled this round: the armour had **not** got darker — the user's PC was in a power-saving
mode that dimmed the whole display. The art is byte-identical, which git confirmed independently.
The measurement below about the summon reading darker than the Dovahkiin still stands on its own,
and is a separate, real effect.

---

## Tooling — a faithful preview of the Ancient Dragonborn (2026-07-30)

No behaviour change; `Tools/PreviewAncientDragonborn.ps1` is new and reads the shipping art
without regenerating any of it. Deliberately separate from `GenerateDragonAspect.ps1`, which
REWRITES 36 signed-off textures and must not be run to look at something.

*Why a second preview at all:* the generator's sheet answers "does each body type's armour fit
that body". It draws the pawn **opaque** and has no axe, so it has never shown the summon — who
is an **invisible** pawn in the level-3 armour carrying the halberd. This one composites exactly
what `Thing_DragonAspectOverlay.DrawAt` does at level 3 with `drawAxe`, over lit ground.

Nothing in it is eyeballed: the orbit/flare/ring fractions, the Ember and Azure colours, the
21-slot crescent table and its hash all come straight out of the overlay class, so a frame on the
sheet is a real frame of the 3.4-second loop. The head offset `(0.04, 0.34)` is read from Core's
`BodyTypes.xml`, and the body and head art are the ones this modlist actually loads — Beautiful
Bodies and Gloomy Face, both verified active in `ModsConfig.xml`, with `Male_Average_Pointy` to
match the user's Dovahkiin.

**The invisible pawn is drawn with vanilla's own numbers: `(0.75, 0.93, 0.98)` at 50% alpha.**
Recovered by scanning `InvisibilityMatPool`'s static constructor IL for its `ldc.r4` constants —
reading the field directly throws, because Unity is not initialised outside the game.

### Three things this turned up

**The generator's sheet draws the helm at about 60% of its real size.** It scales the helm into
`$CELL*0.62` and offsets it by eye, with a comment admitting as much. The game draws it on the
**full body mesh** at `BaseHeadOffsetAt`, so the shipping art covers 110px on a 232px pawn where
that sheet showed 69px. The helm art itself is fine — it was tuned against the game in a playtest
round — but the old sheet misreports it, and that is now recorded so nobody re-tunes good art
against a bad picture.

**A resampling preview can invent a defect.** Drawing the 256-frame sprites into 208px cells
aliased the armour's regular scale field into a fishnet that is not in the texture. On signed-off
art that is worse than useless. The sheet now renders at native 256.

**The pawn underneath is a BRIGHTNESS SOURCE — so the summon's armour is darker than the
Dovahkiin's, by construction.** The user reported the armour looking darker than they remembered
and assumed they were imagining it. They were not. Measured over the armour's own footprint, same
lit ground, same texture, Rec.709 luma:

| armour over | median | vs the Dovahkiin |
|---|---|---|
| an **opaque** pawn (the Dovahkiin) | 154.9 | — |
| an **invisible** pawn (the summon) | 135.4 | **−12.6%** |
| **no pawn at all** | 111.5 | **−28.0%** |

The plates run 10–35% alpha and the palette is authored *for translucency* — the pale body supplies
the brightness. The summon is 50% invisible, so half that source is gone. **No art differs.** At
12.6% this is five times the 2% median shift the user correctly caught by eye once before.

**Correction to an earlier claim in this entry.** A first pass called the fully-hidden change
"small" on the strength of mean 4.2/255, median 1.0/255 — but that diffed the whole 256 frame,
which is mostly bare ground identical in both variants, so the average was diluted by area that
could not change. Restricted to the 12146 pixels the armour covers, hiding the pawn is a further
**17.7%** median drop. The rule was already written down and broken anyway: measure the median
**over the region in question**, never over a frame padded with pixels that cannot move.

Consequence for the open question: the render patch is **not** cosmetically free. It would also
need the palette re-authored lighter for the summon — a second change to signed-off art. That is a
stronger argument against it than the wrong one it replaces, but it is a different argument.

### And a PowerShell trap, for the third time

A function parameter named `$cell` alongside a script constant `$CELL` is **one variable**, so
`if ($cell -le 0) { $cell = $CELL }` assigned the parameter to itself. Every cell drew at size 0,
`SetClip` clipped a 0x0 rect, and the sheet came back **blank with no error** while the function
still returned the right number. Cells that passed an explicit size worked, which disguised it as
a layout bug. Parameters now use distinct words (`$cellPx`, `$groundPx`), not distinct cases.

---

## Phase 2j — second playtest: four fixes, the echo, and the spectral halberd (2026-07-30)

He manifested and expired correctly. Everything below came out of that round.

### The armour and aura vanished about four seconds after he arrived

*Root cause:* `Thing_DragonAspectOverlay.StillValid()` looked for `Dovahkiin_DragonAspect`
specifically. The summon carries `Dovahkiin_AncientDragonborn`, so the overlay judged itself
orphaned on its first rare tick and deleted itself while he walked on without it.

*Fix:* the watched hediff is now a field set by `Attach`, defaulting to Dragon Aspect. Null on
an old save falls back to Dragon Aspect rather than destroying a good overlay on load.

### The axe was invisible — and had been equipped the whole time

*Root cause:* `PawnRenderer.DrawEquipment` gates on `CarryWeaponOpenly()`, which is **false for
an undrafted pawn**. Verified by reading the method's IL. He is autonomous and never drafted, so
his axe only appeared mid-swing.

*Fix:* the overlay draws it. That also keeps it off the pawn render path, which is the same
reason the overlay is a follower Thing at all. It reads the graphic and `drawSize` from
whichever ThingDef is actually equipped, so the drawn axe and the carried one cannot diverge.

### A red error every single time he expired

*Root cause:* Ideology treats a player-faction pawn as a colony **member**, so destroying him
fired `Ideo.Notify_MemberCorpseDestroyed` → `RitualObligationTrigger_MemberCorpseDestroyed`,
which dereferenced null because he deliberately has no ideo. The exception is Ideology's own.

*Fix:* he leaves the player faction immediately before being destroyed, so he never looks like a
member. `Hediff_DeadPuppet` already does this for the same class of reason. Guarding code we do
not own was the wrong instinct.

### He wandered off after fights

Now walks back when he drifts past `ancientDragonbornFollowRadius` (8 cells, tunable).
Deliberately light-touch: only fires when he is idle or wandering, never when he holds a real
job, so it cannot interrupt a fight or re-order a pawn already returning. There is **no
`JobMaker` type in 1.4** — checked, not assumed — so the job is constructed directly.

### The invisibility comp named a type that does not exist

Caught while building a preview. The def used `<li Class="HediffCompProperties_Invisibility">`
with a `visibleToPlayer` field. **Neither exists in 1.4**; both were invented. RimWorld logs an
XML error, drops the comp, and loads the def anyway — so he would have walked around fully
visible while every comment, commit message and test line claimed otherwise.

Corrected to the form Royalty's `PsychicInvisibility` uses, and the form this mod's own Become
Ethereal already used **two hediffs away in the same file**. The right answer was on disk and
was not looked at.

*Known side effect, flagged for playtest:* vanilla invisibility also makes a pawn hard to
**target**, so he is far harder to kill than his health suggests. Deleting the one comp makes
him an ordinary visible ally if that is unwanted.

### The fallen Dovahkiin's echo — user's idea

Once a Dovahkiin dies, later summons wear **that Dovahkiin's face**: body type, head, hair, hair
colour, skin, gender. Captured on every death, so it is always the most recent.

**Appearance only** — no name, traits, backstory, skills or relations. A summon carrying a dead
colonist's identity is something the colony could recognise or grieve, and each of those is a
hook into a system expecting the pawn to persist. Scribed **deep**, unlike the pawn lists either
side of it: it is a record of how someone looked, not a reference to them, and must outlive a
pawn that may be discarded. `ApplyTo` ends with `SetAllGraphicsDirty` — without it every field
is set correctly in data and ignored on screen.

### Armour, and the ladder

1.00 → **0.75** sharp and blunt on the user's instruction once the scale was spelled out.
RimWorld armour is a fraction, so 0.75 is 75 percentage points. The ladder is recorded in the
def so a later session cannot reverse it:

| | sharp | blunt |
|---|---|---|
| Ancient Dragonborn | 0.75 | 0.75 |
| Call of Valor hero *(not built)* | 0.66 | 0.33 |

The hero's numbers are not invented: vanilla plate armour is `StuffEffectMultiplierArmor` 0.73
and steel is `StuffPower_Armor_Sharp` 0.9 / Blunt 0.45, giving 0.657 / 0.329. That satisfies both
constraints at once — "full plated value" and "weaker than the Ancient Dragonborn" — and makes
the hero much softer against blunt, which is what plate really is.

### The spectral halberd

The user's design call, and a better one than what preceded it: ship **our** art with the full
blue-to-orange gradient and borrow only the **behaviour** of a Medieval Overhaul weapon. No
`MayRequire`, no fallback, no second def, identical for every player. The earlier
recolour-their-texture approach is deleted along with its tweak file and a runtime def lookup.

The weapon it behaves as is `DankPyon_MeleeWeapon_Halberd`, and everything borrowed is verbatim:
tools (shaft 13 blunt/poke, blade 27 cut at 0.3 AP), Mass 2.75, `drawSize` (1.5,1.5),
`equippedAngleOffset` 45 from `DankPyon_Base_Sharp_Oversize`, and the halberd's whole Melee
Animation entry — **`MeleeWeaponType` 7**, a polearm, not the axe type 2 first used.

**Melee Animation compatibility ships in our own `WeaponTweakData/` folder.** Checked rather
than assumed that a mod may do this: `XenotypeSatyr` does, which proves the convention. Writing
into Melee Animation's folder would have worked and been wiped by its next update. It is not a
dependency — no `MayRequire`, no assembly reference; absent that mod the folder is never read.

*The art had to be mirrored, and that was measured.* Opaque pixels per quadrant: their halberd
is topLeft 105 / **topRight 5583** / botLeft 3252 / botRight 105 — bottom-left to top-right, head
at top-right. Ours ran the opposite diagonal. Their tweak values live in their texture's frame,
so on a mirrored sprite the pawn would have gripped it by the blade.

*Then reshaped to their proportions*, again measured along the weapon axis rather than eyeballed:

| as a fraction of length | theirs | ours before | ours now |
|---|---|---|---|
| haft half-width | 0.029 | 0.051 | **0.0292** |
| head half-width | 0.137 | 0.217 | **0.1192** |

Three faults: the **haft tapered** 50% from butt to head where theirs is a constant 9.9px
parallel pole — that, more than the head, is why it read as a wedge; it was **too short**; and it
had **no spear point**, which is what makes a halberd a halberd rather than an axe on a pole.

Also fixed: the dark keyline was a fixed 6px. Fine on the old oversized head, but once the head
shrank it was proportionally enormous and swallowed every facet, so the blade reverted to a blob.
It now scales with the shape it outlines.

---

## Phase 2j fix — the summon never appeared: the axe had no CompEquippable (2026-07-29)

First playtest. Reported as "the ancient dragonborn didn't appear" with the Dovahkiin both
dying and downed — so every trigger condition was met and nothing came.

**The trigger was fine.** The log settled that in one line: `[Dovahkiin] Ancient Dragonborn
summon failed: System.NullReferenceException`. The watch comp resolved, fired, and the summon
was attempted. Nothing in the log mentions the comp at all, which rules out the whole class of
"the def didn't load" causes.

*Root cause:* the stack trace ends at `AncientDragonbornUtility.EquipAxe` →
`Pawn_EquipmentTracker.AddEquipment` → `Notify_EquipmentAdded` → null. **The axe ThingDef had
no `CompEquippable`.** RimWorld had already said so at load, twice:

```
Config error in Dovahkiin_AncientDragonbornAxe: is equipment but has no CompEquippable
Config error in Dovahkiin_AncientDragonbornAxe: destroyOnDrop but tradeability is All
```

Both were in the log before the summon was ever cast, and neither had been read. A clean build
and a "does every def parse" check both pass this — parsing proves the XML is well-formed, not
that it is *valid*. **Read the config errors after adding a def.**

The def was written standalone rather than inheriting `BaseWeapon`, to avoid stuff and quality,
and that is still right — but `BaseWeapon` is also where `CompEquippable` comes from, and
dropping the parent dropped the comp silently. Note there is no `CompProperties_Equippable`
type: vanilla declares it as a plain comp carrying `compClass`, exactly the shape
`HediffComp_Invisibility` needs. That is the second time this session the same trap has bitten.

*Fixes:* `CompEquippable` added, `tradeability` set to `None`, plus `drawerType MapMeshOnly`
and `tickerType Never` to match `BaseWeapon`.

### The deeper fault: a decorative failure cost the entire feature

The summon was abandoned wholesale because a **weapon graphic** failed. The catch-all was
written to abandon on any failure, on the reasoning that a half-built summon is the stranded
pseudo-colonist `RISKS.md` §9 exists to prevent. That reasoning holds only up to the point the
doomed hediff is attached — after that the pawn is guaranteed to end itself, and the axe, the
armour overlay and the arrival puff are decoration.

Those three steps are now individually wrapped. Each logs loudly on failure — a silent fallback
is indistinguishable from the bug it hides — and none of them can take the ally down. Had this
been the case originally, the playtest would have produced an armed-less summon and a clear log
line instead of nothing at all.

---

## Phase 2j — the Ancient Dragonborn summon (2026-07-29)

Dragon Aspect's last piece, and the riskiest thing in the mod. **Built and building clean;
never yet run in game.** `TESTS/phase2j.md` is the gate.

### What he is

An invisible pawn wearing the level-3 spectral armour and carrying a ghostly greataxe, so what
the player sees is the armour walking on its own. Autonomous ally, appears beside the
Dovahkiin, lasts 1.5 in-game hours, breathes fire **or** frost rolled 50/50 at summon.

**A rescue, not a guarantee.** Casting at full health summons nobody. He arrives at three words
when the Dovahkiin is downed, or at/below 65% health — whether that is already true at cast
time or becomes true while the shout runs.

The user described three triggers. They are implemented as **one rule sampled at two moments**
(immediately on cast, then each rare tick), because they are the same condition — "the
Dovahkiin is in trouble and Dragon Aspect is up". Three separate triggers would have been three
chances to disagree about what that means. At most one summon per activation.

### He will not breathe through your own people

Checked with `ShoutTargeting.CellsInCone`, at the same range and angle the wave is spawned
with. Reusing the real geometry rather than writing a second "is anyone in front of me" test is
the point: a separate check is free to drift away from where the flames actually land. Strict
by design — colonists, animals, neutrals and prisoners all block him, and he waits for a clean
line rather than accepting collateral. Fire ignites pawns but never the ground.

### Safety, which is most of the work

`RISKS.md` §9 names temporary pawns as the top save-corruption risk. He is doomed by
construction like Soul Tear's puppet — incurable, untendable, non-removable — and additionally:

- **vanishes rather than dying**, so no corpse; equipment, apparel and inventory go with him,
  and the axe is `destroyOnDrop` as a second line
- **is discarded from `Find.WorldPawns`**. Once per day over a five-year colony is ~1800
  summons; this would never appear in a playtest and would surface months later as a save
  growing without bound
- **is cleaned up by the `Pawn.Kill` patch** when killed in a fight, because RimWorld does not
  tick a dead pawn's hediffs so the hediff's own death branch never runs on that path
- **is generated inert**: no relations, ideo, backstory or title; not recruitable; no needs, no
  work, no drafting
- **is tracked and swept on load** exactly as puppets are, scribed as a Reference so the
  registry can never deep-copy and resurrect one
- **is abandoned entirely if any step throws.** A half-built summon is the stranded
  pseudo-colonist the design exists to prevent, so there is no partial-success path

### The fallen Dovahkiin's echo — user's idea

Once a Dovahkiin dies, later summons wear **that Dovahkiin's face**: body type, head, hair,
hair colour, skin, gender. The ally who arrives to save you is the ghost of the one before.
Captured on every death, so the echo is always the most recent.

**Appearance only** — no name, traits, backstory, skills or relations. That restraint is the
design: a summon carrying a dead colonist's identity is something the colony could recognise,
grieve, or form opinions about, and every one of those is a hook into a system expecting the
pawn to persist. A face is safe; an identity is not.

Scribed **deep**, unlike the pawn lists, and correctly so: it is a record of how someone looked,
not a reference to them, and it must outlive a pawn that may be discarded from the save.
Every field is null-tolerant on load, so an echo whose hair def came from a since-removed mod
produces generated hair rather than a failed summon.

### Verified rather than assumed — and two were wrong

`PawnGenerationRequest`'s 50-parameter constructor, `DestroyMode.Vanish`,
`WorldPawns.RemoveAndDiscardPawnViaGC`, `PawnKindDef` using `backstoryFilters` in 1.4, and
`ThingDef.destroyOnDrop` (used by mechanoid weapons).

- **`Interact_BladelikeWeapon` does not exist** in Core. Replaced with `Standard_Pickup` — an
  unrecognised sound defName is an XML error at load.
- **`PawnKindDef` has no `<skills>` field** in 1.4, so his melee skill is set in code. A
  randomly generated tribal can roll Melee 2, which would make the rescue arrive and lose.

### The invisibility comp was declared as a type that does not exist

Caught while building a preview, by checking what `HediffComp_Invisibility` actually is rather
than trusting the def just written. The first version used
`<li Class="HediffCompProperties_Invisibility">` with a `visibleToPlayer` field. **Neither
exists in 1.4** — both invented. RimWorld would have logged an XML error, dropped the comp, and
he would have walked around fully visible while the def comments, the commit message and the
test script all claimed otherwise.

Corrected to the form Royalty's `PsychicInvisibility` uses — and the form this mod's own Become
Ethereal already used, two hediffs away in the same folder. The right answer was on disk and
was not looked at.

**Known side effect, flagged for playtest:** vanilla invisibility also makes a pawn hard to
*target*, so enemies largely ignore him and he is far harder to kill than his health suggests.
Deleting the one comp makes him an ordinary visible ally if that is unwanted.

### Art

`Tools/GenerateAncientAxe.ps1`, palette taken from the armour generator so the two read as one
conjuration. **The blade took three attempts.** An axe bit must grow its along-haft extent
*monotonically* with distance from the haft: narrow at the root, tallest at the cutting edge.
The first two versions put the tallest points at mid-span, which is a lozenge — and a lozenge
with a thick outline renders as a hexagon, which is exactly what appeared both times. The
outward reach was never the problem; the profile was.

---

## Phase 2i — plate opacity up ~25% at unchanged brightness (2026-07-29)

Two requests: *"add a bit more opacity, make sure it doesn't darken"*, and *"make sure the
pikes and fins are more distinguishable"*. Both measured rather than eyeballed. The opacity
change ships; the fin separation was built, compared side by side, and **rejected in favour
of the fins as authored** — see below.

### Opacity and darkness are the same lever until you split them

*Root cause of the tension:* the plates are darker than the pale body sprite under them, so
raising alpha shows less body and the composite dims. A first attempt confirmed it — +25%
opacity cost 2.3% brightness, +70% cost 7.7%.

*Fix:* two knobs instead of one. `$PLATE_ALPHA` sets how opaque; a second knob puts the
brightness back. **Shipped at `$PLATE_ALPHA 1.55` with `$PLATE_GAIN 1.12`** — the "B —
recommended" option the user picked from a preview, reproduced exactly (the shipped
south and east textures are byte-identical to that preview).

### MEAN LUMINANCE IS NOT PERCEIVED BRIGHTNESS — this shipped wrong once

A `$PLATE_LIFT 0.32` version went out first. The lift raises each stop's **value** towards
255 while holding the channel ratios, so hue and saturation survive exactly and nothing can
clip — it looked like the better mechanism, and it was tuned until mean luminance matched.

The user reported it as darker than the preview they had approved. They were right, and the
metric was at fault:

| Male south | mean | **median** | brightest 15% |
|---|---|---|---|
| B preview (gain 1.12) | 154.8 | **159.9** | 217.3 |
| lift 0.32 version | 153.8 | **156.3** | 217.8 |

Same mean to within 0.6%, **median 2.3% lower**. The two mechanisms put the light in
different places: a gain multiplies, so midtones and highlights rise together; a value lift
gives a much larger boost to dark stops than bright ones (`C_BLUE_DEEP`, peak 66, gains ~90%
at 0.32 while `C_GOLD`, peak 228, gains ~4%). So the lift opened the shadows and left the
midtones where they were — and the midtone is most of the surface the eye reads.

**Match the median, not the mean.** Both knobs are kept, documented with this difference and
defaulting to no-op. The clipping objection that motivated the lift was also overstated at
these strengths: at gain 1.12 `C_GOLD`'s red overshoots 255 by 0.4 of 255. It only becomes a
real problem at the ~1.47 a fully-opaque variant would need.

Measured over the body silhouette, front and side, Male and Female, as shipped:

| | opacity | brightness (mean / median) |
|---|---|---|
| Male south | +19.5% | −0.2% / −0.2% |
| Male east | +24.4% | +0.3% / +0.2% |
| Female south | +24.7% | −0.2% / — |
| Female east | +30.5% | +0.6% / — |

**Do not raise `$PLATE_ALPHA` on its own.** The header comment on it used to say "leave this
at 1.0", from the round where raising it alone was correctly reverted. That reasoning still
holds — it just no longer applies now the lift exists to pair with it. Moving one means
re-sweeping the other against the brightness measurement.

### Fin separation — built, compared, and NOT shipped

The fins were reported as hard to distinguish from the plates. **The opacity bump is what
caused that**, and the mechanism is not a matter of opinion: the plates gained ~20% opacity
while the fins were left exactly as they were, so fin-against-plate contrast necessarily
fell. Underneath it, a fin takes `CoolAt` at its own height and so does the plate field
behind it — at the shoulders, a blue fin on blue plates, separated only by a hot edge
`thick × 0.16` wide. Brightening the palette never helps, because it lifts both equally.

`$SPUR_SEP` was built to fix it: three levers together — a dark rim under the fill, a thicker
hot edge, and a brightness lift on the fins alone. Turning any one alone trades one kind of
mush for another. The rim colour is derived from `$C_DEEP_RAW` / `$C_BLUE_DEEP_RAW`,
**captured before any lifting**; deriving it from the lifted deeps was the first attempt and
gave a mid-tone rim that separated nothing, because a contrast rim cannot be brightened by
the same knob that brightens the thing it contrasts with.

**It ships at 0 — the fins are as originally authored.** It was compared at 0.35 / 0.60 /
0.85 / 1.00 against untouched, at the shipped opacity, and the user chose untouched. The
machinery stays in the generator because the finding is worth keeping and re-deriving it
would cost another round; 0.85 was the value that read best if it is ever wanted.

Opacity and brightness are effectively unaffected by the choice — fins are a small fraction
of the pawn's area, so with and without measured +19.5–30.5% vs +19.9–31.1% opacity, and
−0.2–+0.6% vs 0.0–+0.6% brightness. It was purely a look decision.

### Not shipped, deliberately

A fully-opaque "dragon-scale bodysuit" variant was explored at the user's request and
rejected. It hides the colonist completely — no skin, no apparel, no body shading — which
crosses the `SPEC.md 4.4d` line about apparel reading underneath. It is reachable at
`$PLATE_ALPHA 10` with `$PLATE_LIFT ~0.70`, `$DEEP_LIFT ~0.65`, `$LIT_FALLOFF ~0.22` if it is
ever wanted; those numbers are recorded here so the exploration does not have to be redone.

### Geometry unchanged

Silhouette-fit re-measured after the change and identical to before: overhang past the body
outline through 10–90% of the body is 0–2px on all five body types.

---

## Phase 2i fix — every SIDE view was wrong: the profile mirrored one half-width (2026-07-29)

Reported off the preview, on all four body types shown, and described on the Hulk as "a
sagging veil in front of the abs". Correct on every count.

*Root cause:* the measured profile stored **one** half-width per row — `max(left, right)` —
and mirrored it about a centre line. That is nearly harmless on the front and back views,
which really are symmetric, and badly wrong on a side view, where the pawn faces one way.
Measured on the east sprites:

| east view | left | right | mirrored overhang |
|---|---|---|---|
| Hulk at y=230 | 69.5 | 11.5 | **58px** |
| Thin at y=200 | 38.5 | 0.5 | **38px** |
| Female at y=181 | 54.5 | 24.5 | **30px** |
| Male at y=200 | 35.5 | 19.5 | **16px** |

Taking the larger edge and mirroring it hangs that much armour off the front of the lower
body — which is exactly the veil described.

Two compounding errors sat underneath it. The side sprites are **not centred on 127.5**
(Female east is centred on x=113, Thin on 121.5, Fat on 121) and **not the same height as the
front** (Female east runs y 82..224 against the front's 86..224; Hulk east 73..248 against
66..250). The generator took the centre line and the vertical extent from the *front* view for
all three rotations, so the side outline was mispositioned before asymmetry was even in play.

*Fix:* the profile is now `@(y, halfLeft, halfRight)` and every place that **positions**
geometry against the outline takes its own side — torso path, arm bands, plate fill and its
alpha ramp, shoulder fins, elbow spikes, chest crest. `HalfWidthAt` survives only for
*scaling* decisions (how long is a fin), with a new `HalfSideAt` for placement. Each rotation
now also carries its own centre, extent and vertical landmarks, built once into a `$GEOM`
table that `UseRotation` selects.

Size quantities are still taken from the **front** view and shared across rotations: a
shoulder fin does not shrink when the pawn turns sideways.

*Verified numerically, not by eye.* Armour overhang past the body outline, east view, from
10% to 90% down the body: **0–2px on all five body types**, against 58px before. The 27–53px
readings on the topmost row are the shoulder fins, which SPEC 4.4d wants breaking the
silhouette.

*Method note:* the throwaway sheet built to check this first failed with every sprite load
erroring, because it used `$REF` for the bodies folder and `$ref` for a Bitmap — **one
variable**, PowerShell being case-insensitive. The notebook already carried that warning
twice. Names in these scripts must differ by more than case.

---

## Phase 2i fix — the armour was drawn INSIDE the pawn, and only ever fitted one body type (2026-07-29)

Reported as "it still looked weird on my pawn (inside them)", after the previous round's
sizing fix had been confirmed as an improvement but "still not enough". Two independent
defects, found by taking "inside them" literally.

### Defect 1 — the overlay borrowed a mesh that is deliberately smaller than the pawn

*Root cause:* `PawnRenderer.GetBodyOverlayMeshSet()` looks like the correct call and is not.
It returns `MeshPool`'s **per-body-type** sets, and those are **inset on purpose** — they
exist for wounds and firefoam, which are meant to sit *within* the silhouette. Read out of
`Verse.MeshPool..cctor` IL in order, matching each `newobj GraphicMeshSet` to its `stsfld`:

| mesh set | size |
|---|---|
| `humanlikeBodySet` — **what the body is actually drawn on** | **1.5 × 1.5** |
| `humanlikeBodySet_Male` | 1.3 × 1.3 |
| `humanlikeBodySet_Female` | 1.3 × 1.4 |
| `humanlikeBodySet_Hulk` | 1.5 × 1.65 |
| `humanlikeBodySet_Fat` | 1.6 × 1.4 |
| `humanlikeBodySet_Thin` | 1.2 × 1.4 |

`Verse.PawnRenderer.DrawPawnBody` draws the body through
`HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn)`, which returns
`MeshPool.humanlikeBodySet` (1.5) for an ordinary adult. So the armour was rendering at
**1.3 / 1.5 = 87%** of the pawn on a Male-bodyType colonist — which is exactly the reported
symptom, and the user's Dovahkiin is Male bodyType (confirmed by reading `<bodyType>` out of
`Dovahkiindebug.rws`).

*The previous fix caused this.* It replaced a hardcoded 1.5 with `GetBodyOverlayMeshSet()`
on the reasoning that "it is what firefoam and wounds use". That reasoning was backwards:
wounds and armour are both drawn on a pawn and want *opposite* insets.

*Fix:* call `HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(target)` — the same method
`DrawPawnBody` uses, so armour and body share a quad by construction. `public static` on a
public static class in `Verse`; no patch, no reflection. It also handles Biotech children
correctly for free, diverting to `MeshPool.GetMeshSetForWidth` when the pawn's life stage
carries a body-width override — which is what the hardcoded 1.5 got wrong originally.
Rejected going back to a hardcoded 1.5: it is right for an adult by luck and wrong for every
child, and it re-introduces the bug the previous round was trying to fix.

`BodyScaleOf` now takes the pawn and asks
`HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn`, which returns the width directly. The
old `mesh.bounds.size.x` read was a workaround for `GraphicMeshSet` not exposing its width;
it stays as a fallback.

### Defect 2 — one traced silhouette, worn by five different body shapes

*Root cause:* `GenerateDragonAspect.ps1` traced its entire geometry from
`Naked_Male_south.png` and nothing else. Measured off the Beautiful Bodies sprites
(`mireia.bodies`, active at load position 31, and textures-only so vanilla's `BodyTypeDef`
numbers are live):

| body type | shoulder | waist | hip | height | widest at |
|---|---|---|---|---|---|
| Male | 102px | 84 | 88 | 127 | shoulders |
| Female | 74px | **60** | **92** | 139 | **hips** |
| Thin | 52px | 52 | 52 | 130 | nowhere — a tube |
| Fat | 138px | 138 | **162** | 148 | belly |
| Hulk | 150px | 120 | 130 | **185** | shoulders |

Male and Female are opposite shapes. These are different **shapes**, not different sizes, so
no scale factor reconciles them — the outline has to be read per body type.

*Fix:* the generator now measures each body sprite's own alpha outline per rotation and fits
the plates to it. Every landmark is a fraction of the measured body, and each fraction is the
value the old hardcoded male numbers already implied — so **Male comes out unchanged**
(verified pixel-wise against the previous textures: mean delta 1.67–4.42 of 255 on the front
views, total ink within 1%). Rejected hand-tuning five sets of landmarks: five times the
numbers to maintain, and it would not fit a body sprite any future mod supplies.

30 body textures now ship in place of 6. The **helm is deliberately not per body type** —
head art does not vary by body type, and `BaseHeadOffsetAt` already moves it per type via
`BodyTypeDef.headOffset`. Aura unchanged.

Upper-body features (fins, arm bands, chest crest) scale off the half-width **at the shoulder
line**, not the body's maximum. Scaling by the maximum was tried first and gave a Fat pawn
fins 1.59× the male's when its shoulders are only 1.35× wider — they read as wings, because
a Fat body's maximum is its belly.

`Child` and `Baby` have no art of their own and fall back to Male: wrong shape, right size,
never a missing-texture square.

### Preview harness now paints lit ground

The notebook had carried this as an unactioned lesson since the plates were signed off
against a dark backdrop and then reported as barely visible over real terrain. Every preview
cell now paints rough deterministic lit ground under the real body sprite for its own type.

---

## Phase 2i fix — the armour stopped drawing when the pawn walked away (2026-07-29)

Reported as "the visual wears off way too soon, even though it still says the power lasts
3 hours". The hediff was fine; the picture was being culled.

*Root cause:* `Thing_DragonAspectOverlay` deliberately never moves - it stays on the cell it
was cast on and draws at the pawn instead. But RimWorld culls dynamic drawing by the **Thing's
own cell**, so once that cell left the view rect the overlay stopped rendering while the buff
carried on running. Walking any distance from where you shouted made the armour disappear.

*Fix:* `<drawOffscreen>true</drawOffscreen>` on the ThingDef. This is exactly what vanilla's
other `RealtimeOnly` movers do - `Tornado` and `PawnFlyerBase` both set it, and for the same
reason. Rejected the alternative of syncing the Thing's `Position` to the pawn each tick:
it adds thing-grid churn on a per-tick path for no visible benefit once culling is disabled,
and CLAUDE.md forbids per-tick work that has a cheaper form.

*How it was found:* by reading the vanilla defs that already use `RealtimeOnly` and noticing
what they all had that this one did not.

### Duration now scales with words again

5 / 7 / 9 in-game hours at one / two / three words (12500 / 17500 / 22500 ticks). The previous
flat 5 hours had removed a progression axis, which was flagged at the time.

---

## Phase 2i — Dragon Aspect becomes a once-per-day power (2026-07-29)

Retuned at the user's request to TES5's rhythm: a daily power, not a combat shout.

| | was | now |
|---|---|---|
| Duration | 20 / 30 / 45 s | **5 in-game hours** (12500 ticks) at every level |
| Own cooldown | — | **24 in-game hours** (60000 ticks) |
| Thu'um cost | 9 / 15 / 22 | **0** |
| Shared shout lockout | 3600 / 5400 / 7200 | **60 ticks — one second** |

### The two cooldowns are different things, and that is the whole point

`ShoutDef.cooldownTicksByLevel` is the **shared** cooldown: per SPEC.md 4.2 there is one
Thu'um cooldown across ALL shouts, and its length is set by whichever shout was last used.
Putting a day into it would silence the Voice entirely for that day — which is not what a
once-per-day power means.

So the shared lockout is one second, and the day-long wait lives on
`AbilityDef.cooldownTicksRange`, which is vanilla's own per-ability cooldown. Verified by
reflection that it exists as an `IntRange` and that `Ability` exposes `CanCast`,
`HasCooldown` and `StartCooldown` to enforce it. **Dragon Aspect is the only shout in the mod
that needs both**; every other one leaves `cooldownTicksRange` at 0.

### Zero cost needed no special case

Checked rather than assumed: `Need_Thuum.CanAfford(0)` is `CurLevel >= 0`, always true, and
`TrySpend(0)` subtracts nothing. There is no division anywhere on that path, so the "use 1 if
0 breaks things" fallback the user offered was not needed.

### Duration no longer scales with words

All three levels last 5 hours. Word count now buys armour, resistances, the cooldown cut and
the summon — not time. Flagged because it is a deliberate loss of a progression axis.

---

## Phase 2i fix — armour smaller than the pawn, and the wave never returned (2026-07-29)

Second playtest. Both reports correct, and the first one was a better guess than it looked.

### "The helmet is literally smaller than the pawn, the shoulder pikes are inside its width"

The user wondered whether their body mod was to blame. **Essentially yes** — though the fault
was mine for hardcoding a size rather than asking the pawn.

*Root cause:* `Thing_DragonAspectOverlay` drew at a fixed `1.5` world units.
`MeshPool.HumanlikeBodyWidth` is indeed 1.5, but that is only the DEFAULT. `MeshPool` also
holds `humanlikeBodySet_Male`, `_Female`, `_Hulk`, `_Fat`, `_Thin` and a
`humanlikeMeshSet_Custom` dictionary that body mods populate. Any pawn not on the default
width got an overlay that did not match them.

*Fix:* draw with `PawnRenderer.GetBodyOverlayMeshSet().MeshAt(rot)` — public, and the same
mesh firefoam and wounds use to paint onto a body. It fits any body type, child or modded
frame without the mod knowing anything about them. Everything else — helm, aura rings,
particle orbits and sizes — is now expressed as a FRACTION of that mesh, measured off
`mesh.bounds.size.x`, so it all scales together. Rejected: enumerating the body-type mesh sets
by hand, which would have needed updating for every body mod ever installed.

*Second, separate cause for the helm.* Head and body quads are both 1.5
(`HumanlikeHeadAverageWidth` = `HumanlikeBodyWidth`). What differs is how much of the texture
the art fills: a head is about 60×74 of a 192 frame, so 0.31 × 0.39 of its quad. The helm was
drawn at 62×76 in a 256 frame — 0.24 × 0.30 — **and** at draw size 0.93, which stacked into
less than half a head. Redrawn at 88×108 and drawn on the body mesh.

### "The wave isn't coming back at all"

Correct, and it was never built. Earlier in the session I established that
`Thing_ShoutWave` travels one way with a single fixed colour, said the return needed three new
fields, and then shipped the outgoing half without flagging the gap in the test script. That
is on me — the test script should have listed it as absent, as it does for the summon.

*Fix:* three fields on `Thing_ShoutWave`, all optional so no existing caller changes meaning.

- `inward` — runs the front from the outer edge home instead of outward
- `endColor` — `Color.Lerp(headColor, endColor, progress)` across the wave's life
- `startDelayTicks` — lets the return be queued behind the outgoing ring by exactly its flight
  time, so it begins as the first finishes rather than overlapping it

**An inward wave skips `StrikeBand` entirely** and is cosmetic by construction. That is
deliberate rather than incidental: a returning wave passes back over ground the outgoing wave
already hit, and striking everyone a second time on the way home is not what "the shout comes
back" should mean. `startDelayTicks` and `inward` are in `ExposeData` — unlike `age`, a queued
return has not started yet, so without saving it the wave would fire the instant a save loaded.

---

## Phase 2i fix — log spam on every tick, and near-invisible plates (2026-07-29)

First playtest of Dragon Aspect. Two reports, both real.

### "The logs kept appearing whenever I was moving"

`Exception ticking Dovahkiin_DragonAspectOverlay: NotImplementedException`, repeating.

*Root cause:* `Thing_DragonAspectOverlay.TickRare` opened with `base.TickRare()`.
**`Verse.Thing.Tick`, `TickRare` and `TickLong` are all six-byte stubs containing a `throw`
opcode** — confirmed by reading their IL rather than guessing. Calling base threw every 250
ticks.

*Why it mattered more than log noise:* the exception aborted the rest of the method, so the
overlay never reached its own `StillValid` check and **outlived the hediff it follows**. The
armour would have stayed on the pawn after the shout ended.

*Fix:* do not call base. Checked the rest of the mod for the same shape —
`Hediff_DeadPuppet.Tick` also calls base, but that is `HediffWithComps`, whose `Tick` is a
real implementation, so it is correct there. The rule is class-specific, not general.

### "The armor was barely visible"

*Root cause:* not a rendering fault — the plates really are authored at alpha 26 (centre) to
88 (edge). They were signed off against a **dark preview background with a plain untextured
pawn**, which flatters low alpha enormously. Over real apparel on lit ground they disappear.

*Fix:* one knob, `$PLATE_ALPHA` in the generator, at 1.85. Chosen over the alternatives:
raising the authored numbers at each call site (three places to keep in sync), or switching
the body to the `MoteGlow` shader (additive, so it would glow at night like light rather than
sit on the pawn like a surface).

*Second bug found by the first fix:* raising alpha threw 410 `FromArgb` exceptions. Alpha is
multiplied downstream in several independent places — scale jitter, rim factor, highlight
factor — so a value clamped in one place gets pushed back over 255 by the next. Clamped
inside the `RGB` helper instead, which is the single point every colour is built through.

*Process note:* the preview harness now paints rough lit ground under the pawn. Ten lines,
and it would have caught this before it reached a playtest.

---

## Phase 2i — Dragon Aspect, everything but the summon (2026-07-29)

The fourteenth and last core shout. Builds clean, 0 warnings. Not yet playtested.
Test script: `TESTS/phase2i.md`. The Ancient Dragonborn is deliberately **not** in this
build — see the end of this entry.

**Effects, as specified by the user:** word 1 armour and heavier melee; word 2 armour ×4 plus
fire and frost resistance; word 3 armour ×6 plus a shorter shout cooldown plus the summon.
Armour resolves to **+0.10 / +0.40 / +0.60** on Sharp and Blunt.

### Two of the three effects had no vanilla stat, and both were found by checking

**Melee damage: `MeleeDamageFactor` is BIOTECH-ONLY.** It appears in `StatDefOf`, which is
exactly the trap `RangedCooldownFactor` set earlier in this project — being in a `DefOf` class
proves the field exists, never that the def does. It is defined in
`Biotech/Defs/Stats/Stats_Pawns_Combat.xml`, and `CLAUDE.md` invariant 5 requires the mod to
run without Biotech. Listing Core's own pawn-combat stats settles it: hit chance, dodge,
armour penetration and a DPS *readout* — **no melee damage multiplier exists in Core at all**.

*Fix, and why this one over the alternatives:* a Harmony postfix on
`Verb_MeleeAttackDamage.DamageInfosToApply`. Verified by reflection that the method exists and
carries `IteratorStateMachineAttribute` — it is a compiler-generated iterator, so its body
cannot usefully be patched and wrapping the returned sequence is the correct shape. Rejected:
`MeleeArmorPenetration` (Core, but penetration is not damage and would read differently in
play), and `MayRequire`-gating the Biotech stat (leaves baseline players with a word-1 effect
that silently does nothing).

**Frost resistance does not exist as a concept in RimWorld.** There is no cold-damage armour
category. Rather than invent one, every frost source in the active modlist was read off disk —
full table in `COMPAT.md` section 10. The result was better than expected:

- **RimWorld of Magic files its frost damage under the `Heat` armour category** (5 of its 7
  frost defs), so `ArmorRating_Heat` — the obvious "fire resistance" — buys most of the
  modlist's frost resistance too.
- `Iceshard` and Dragon's Descent's `DD_Frost_Breath` are **Sharp**; The Profaned's ice is
  **Blunt**. Both already raised from word 1.
- Vanilla `Frostbite` has **no armour category**, `externalViolence: false`, and runs through
  `DamageWorker_Frostbite`. Armour cannot touch it at any value — only `Insulation_Cold` can.

*Fix:* four Core stats cover the whole table. No Harmony damage hook and no list of foreign
defNames, both of which were considered and are now unnecessary. The user specifically asked
for cold insulation on the suspicion that frost hazards apply frostbite; that instinct was
right and is the only reason weather-driven frostbite is covered.

**Shout cooldown reduction** needed no stat at all — the shared Thu'um cooldown is this mod's
own number. Applied in `ShoutUtility` *after* strain, deliberately: strain should still
lengthen the cooldown and then be discounted. Dragon Aspect makes shouting easier; it does not
make the Voice tireless. Three words only.

### The overlay — SPEC 4.4d's stop-and-report clause

`Thing_DragonAspectOverlay`, a follower Thing with `drawerType RealtimeOnly` that reads
`pawn.Drawer.DrawPos` and `pawn.Rotation` each frame. **No render patch anywhere.**

Two routes were checked against the real assembly and rejected:

- `RimWorld.PawnOverlayDrawer` **does** exist in 1.4 and is exactly the right machinery — it
  is how firefoam and wounds paint onto a pawn's body mesh. But `PawnRenderer` only ever calls
  the two instances it owns, from the private `RenderPawnInternal`. A third means patching pawn
  rendering, which is the single thing RocketMan is most likely to break.
- Invisible apparel needs **15 textures, not 3** — `ApparelGraphicRecordGetter` resolves
  body-layer apparel per `BodyTypeDef` — and is a real item that shows in the Gear tab, can be
  removed, and drops on death.

The helm is positioned from `PawnRenderer.BaseHeadOffsetAt`, which is public, so it follows the
head rather than sitting at a guessed offset. The overlay holds **no game state**: if it failed
to spawn the shout would still work and simply be invisible, which is why it is a separate
Thing rather than something the hediff depends on.

### The cast ring is not new art

A bespoke expanding-ring texture was built for this and **thrown away**. The mod already has
the machinery: `SpawnRingBurst` spawns the ordinary `Thing_ShoutWave` at `coneAngle 360` with
no payload — the same call Slow Time and Clear Skies make. Dragon Aspect just passes the
armour's ember tint. When a shout needs a stock effect, check `CompAbilityEffect_Shout` before
drawing anything.

### Deliberately not in this build

**The Ancient Dragonborn**, the ghostly ally at three words, and the gradient axe he carries.
Temporary pawns are the top save-corruption risk in `RISKS.md` section 9, and Soul Tear's
puppet only became safe by being *always doomed*. That deserves its own build and its own test
round rather than being bundled in behind fourteen other checks.

---

## Art — five icons now use head-to-tip gradients (2026-07-28)

Soul Tear's tip changed from crimson to **bright clear purple**, giving it one hue running dark
to light — which also matches its purple bolt in play. Four more shouts then gained gradients:

| Shout | Head | Tip |
|---|---|---|
| Soul Tear | deep dark purple | bright clear purple |
| Marked for Death | blue lilac | its existing cold blue-grey |
| Storm Call | storm-cloud dark grey | thunder blue |
| Dragon Aspect | **Unrelenting Force's exact blue** | **Fire Breath's exact orange** |
| Dragonrend | deep azure | clear light azure |

**Dragon Aspect borrows the other two shouts' literal RGB values** rather than approximating
them. It is the shout that makes you part dragon — being visibly the blue shout and the fire
shout at once says that better than a third invented colour would.

Two follow-on adjustments the change forced:

- **Storm Call's tip is now thunder blue**, not the violet it was. With storm-cloud grey at the
  head, keeping a violet tail would have read as two unrelated colours rather than a cloud
  discharging.
- **Dragonrend's core was lightened** from grey to near-white. A grey core inside an all-azure
  comet read as a smudge instead of a highlight.

### Gotcha found while doing it: `Select-Object -First` stops a pipeline

Piping the generator into `Select-Object -First 2` to shorten its output **terminated the script
after two of fifteen icons**. The preview then showed thirteen stale files with no error
anywhere — it simply looked as though the colour changes had done nothing. Capture to a variable
and index that instead. Recorded in the notebook.

---

## Polish — Soul Tear: gradient icon, execute-grade damage, and a terror thought (2026-07-28)

Playtest passed. Three requested changes.

### The icon now runs dark purple at the head into crimson at the tip

The recolour pipeline could only apply a **flat** body colour. It now supports an optional
**head-to-tip gradient**, and Soul Tear is the first shout to use it.

Two details make it work rather than smear:

- **The blend is the Thu'um bar's curve** — smoothstepped across the middle 40% — so each colour
  still owns roughly half the shape. A straight linear ramp reads as mud through the centre.
  That is what "50/50 blend" means here, and the numbers 0.30/0.70 now appear in both places
  for the same reason.
- **The head is found, not hard-coded.** It is the centroid of the master's brightest pixels —
  the hot core the generator already draws there — so redrawing the master cannot silently
  misplace the gradient. Reported at generation time: head at 87,108, tail reach 128px.

The main pixel loop had to move from a flat byte walk to nested x/y, because a raw buffer index
carries no position to measure a gradient against.

Result: Soul Tear stays clearly distinct from Dismay's flat red and Drain Vitality's flat
violet, which was the risk in giving it a purple.

### Damage: it now out-kills Marked for Death decisively

Asked for it to be deadlier than Marked for Death, given it is single-target and instant.
Measured against the real numbers rather than by feel:

| | Marked for Death | Soul Tear |
|---|---|---|
| Level 1 | 16 over 40s | **60 instant** |
| Level 2 | 32 over 40s | **95 instant** |
| Level 3 | 48 over 40s | **140 instant** |

Raised 50/80/115 → **60/95/140**, and — the more important half — **concentrated** from 3/4/5
hits down to **2/3/3**. `SelectSpreadTarget` weights core and head, so the same total split into
fewer, bigger hits destroys vital parts instead of leaving a dozen survivable bruises. That is
what turns it from a heavy poke into an execute. AP at level 3 raised 0.80 → 0.85.

Marked for Death keeps its own identity: it softens armour and amplifies *all* incoming damage
for 40 seconds, which Soul Tear does not. One is an opener, the other is a finisher.

### Surviving it leaves a mark

New `Dovahkiin_Thought_SoulTorn` — *"terrified soul"*, **−28 mood for 12 days**, non-stacking:

> Something reached inside me and pulled. It very nearly came away in its hand. What kind of
> fate would have awaited me...?

Applied to anyone who **lives** through a tear and was not puppeted — a corpse has no mood and a
puppet is dying on a clock anyway. Given on a failed roll *and* at level 1, so a held soul is no
longer a pure non-event.

It matters more than it looks now that Soul Tear can be turned on your own people: this is the
standing cost of using it on someone you intend to keep.

Builds clean, 0 warnings; all XML parses.

---

## SPEC CHANGE — Soul Tear may be used on anyone (2026-07-28)

Requested: it should work on allies and neutrals too. **This overrules `SPEC.md §4.4f`**, which
read *"only valid on hostile pawns — never colonists, never player-faction, never tamed
animals"*. The spec has been amended in place rather than left contradicting the build, because
a future session reading it as the contract would otherwise "fix" the code back.

**Technically trivial** — the restriction was three checks. The consequence was the real work.

### Two exclusions remain, and they are not stylistic

- **The caster.** Tearing your own soul is nonsense.
- **A pawn already puppeted.** Re-tearing would stack a second doomed timer on a pawn already
  dying to one.

### Tearing your own is an execution, and is now mourned as one

The puppet normally leaves the player faction one tick before dying, *specifically* to suppress
colonist-death grief — correct for a raider, badly wrong for a colonist. Left as it was, Soul
Tear would have been a way to murder one of your own people that **nobody in the colony
noticed**.

`Hediff_DeadPuppet` now carries `grieveOnDeath`, captured at the moment of tearing from whether
the victim was already player-faction:

| Victim | Faction dropped before death | `RemoveDiedThoughts` | Result |
|---|---|---|---|
| Enemy, neutral, ally | yes | yes | no grief, as before |
| Your own colonist or animal | **no** | **no** | mourned normally |

It serialises, so the distinction survives a reload.

### Not special-cased, deliberately

Tearing a neutral or an ally angers their faction. That happens through the ordinary
`TakeDamage` path with the caster as instigator — it is RimWorld's own behaviour, it is correct,
and adding handling to soften it would be inventing a rule nobody asked for.

Builds clean, 0 warnings; all XML parses.

---

## Phase 2h-fix — Soul Tear had no armour penetration, and is now a visible bolt (2026-07-28)

Playtest: cast on a **Profaned Legion** (a heavy elite from The Profaned), which was "still
alive and kicking and still hostile".

### Root cause: zero armour penetration on the mod's most powerful shout

`Dovahkiin_SoulWither` is **Blunt-parented** — its own def comment says so, and says plainly
that it "is still reduced by armour". The comp passed `armorPenetration = 0f`, so against a
heavily armoured target most of the damage was simply absorbed. Fine for a breath weapon; wrong
for the shout the spec calls the most powerful in the mod.

**Fixed:** armour penetration **0.50 / 0.65 / 0.80** by level, and damage raised alongside it
(40/65/95 → **50/80/115**). AP is the actual fix; the damage bump is because the shout should
also simply hit harder than it did.

Worth noting for later: **every other shout still has zero AP**, which is deliberate for the
breath weapons but should be revisited if armoured enemies start shrugging those off too.

### Still hostile is not necessarily a bug — but it was unreadable

The puppet chance is **0 at level 1 by design**, 0.25 at two words, 0.45 at three. So "still
hostile" can be a correct failed roll. But there was no way to tell that from a broken shout,
which is exactly the confusion Storm Call's silent misses caused.

A failed roll now says so: *"{PAWN}'s soul holds. Nothing rises."*

### A visible purple bolt that stops at the first body

Asked for: a seen projectile, purple, stopping at the first target as it does in TES5 — a narrow
travelling line like Cyclone's rather than a cone, with a longer trail.

Soul Tear now spawns the ordinary `Thing_ShoutWave` in **lane mode**, and three capabilities
were added to that class to support it:

- **`armorPenetration`** on the payload, applied to both the normal and re-burn damage paths.
- **`stopOnFirstPawn`** — the wave destroys itself the moment it reaches any pawn, so the bolt
  halts at the first body instead of carrying on through the rank behind.
- **`trailBands`** — trail length, defaulting to the previous 2. Soul Tear uses 7.

**The alpha falloff had to change with it.** It was hard-coded at `1 - back * 0.33`, which
reaches zero at three bands — so any trail longer than three was *silently invisible*. It now
scales to the configured trail length.

**Damage and the puppet roll ride with the front and land on arrival**, not on cast. That is the
rule this class has followed since Phase 2a: cause and effect must line up on screen. Resolving
the tear on cast would have raised the puppet a second before the bolt visibly arrived.

The puppet logic moved out of the ability comp into a static `SoulTearUtility`, because the comp
no longer holds the victim at the moment that matters — the wave does.

Colour is a brighter, more magenta violet than Drain Vitality's deep purple, so the two purple
shouts stay distinguishable at a glance.

Builds clean, 0 warnings; all XML parses, all translate keys resolve. **Awaiting retest.**

---

## Phase 2h — Soul Tear and the dead puppet (2026-07-28)

`SPEC.md §4.4f`, `RISKS.md §9`. **13 of 14 core shouts built.** Only Dragon Aspect remains.
Builds clean, 0 warnings; all XML parses, all translate keys and icon paths resolve.
**Awaiting playtest** — `TESTS/phase2h.md`.

### The design that removes the risk

`RISKS.md §9` recorded the dead puppet as **the highest save-corruption risk in the mod**. The
original plan moved a hostile pawn into the player faction and *restored* it afterwards — which
required a correct restore-or-kill on seven exit paths, one of them save→load. Getting it wrong
leaves a player-faction pawn nobody can arrest, banish or kill cleanly.

**The adopted design: the puppet is always doomed.** It joins the player faction, receives
`Hediff_DeadPuppet` — incurable, untendable, **non-removable** — and that hediff **kills it** on
expiry. It is never restored, because it never survives.

That collapses the whole failure surface:

- timer expiry kills it;
- being killed early is already death;
- being downed leaves the hediff ticking, so it still dies;
- leaving the map carries the hediff along;
- the caster dying changes nothing — the puppet's death does not depend on the caster;
- **save→load is safe by construction**, because the only thing that must survive is an ordinary
  hediff using RimWorld's normal, well-tested serialisation. There is no bespoke state to lose.

`Hediff_DeadPuppet.ShouldRemove` is hard-coded `false`. The def sets `tendable false`,
`everCurableByItem false`, `makesSickThought false`. **The absence of a way out is the design**,
and both the class and the def say so in comments so a future session does not "helpfully" add
one.

### Enforcement details

- **Single target only.** `canTargetLocations false` so it cannot be thrown at empty ground, and
  the comp re-checks legality on cast: never colonists, never player-faction, never a pawn
  already puppeted, and it must be genuinely hostile.
- **Level 1 raises nothing.** The tuning def's `soulTearPuppetChanceByLevel` starts at 0
  deliberately, so the puppet is unlocked by mastering the shout rather than given free.
- **No colonist-death mood.** The puppet drops out of the player faction **one tick before**
  dying, so the death raises no such thought — and `RemoveDiedThoughts` runs afterwards as belt
  and braces. Splitting the faction change and the kill across two ticks also avoids mutating
  the pawn twice while the health tracker is mid-iteration.
- **Visibly marked**, as the spec requires: a pulsing crimson attached fleck, plus a patched
  inspect line giving the countdown and stating it cannot be healed, recruited or saved. The
  patch is on `Pawn.GetInspectString`, which runs only for the selected pawn.
- **Resurrect, not ResurrectWithSideEffects.** The side-effect version can inflict brain damage
  and resurrection sickness, which would produce a puppet unable to fight — and fighting for its
  short life is the entire point.

### The safety sweep

`SPEC.md §4.4f` asks for a load-time sweep, so the registry now tracks raised puppets by
reference and checks them in `FinalizeInit`. Any tracked puppet still alive and player-faction
but **missing its hediff** is killed, with a loud red error naming `RISKS.md §9`.

**This should never fire** — the hediff is non-removable. It exists because the failure it
guards against is bad enough to be worth a check that costs nothing on load.

### Balance

The most expensive shout in the mod, above Storm Call: 12/20/30 thu'um, cooldown 3000/5000/7500.
Damage 40/65/95 via `Dovahkiin_SoulWither` (Blunt-parented, so spreading it cannot kill by
cumulative blood loss). Puppet chance 0/0.25/0.45, lifetime 0/6/12 in-game hours.

**Recorded for Phase 7:** `SPEC.md §4.4f` says Soul Tear's three words belong in **high-tier
crypts only**. That is a world-generation constraint for when word walls are placed, and cannot
be enforced from this phase.

---

## Balance — Drain Vitality heals more per victim when draining few (2026-07-28)

Follow-up: the raised healing was good against four victims but still thin against **one**.

A flat raise would have been wrong — it would overshoot the multi-target case that was already
judged good. So the boost is now **per victim count**: largest for a lone target, smaller for
two, gone by five.

**The rule this must always keep**, and the reason it is stated in three places (the C# summary,
the XML comment, and here): *count × multiplier must never fall as the count rises*, or the
shout would perversely pay less for hitting more people.

| victims | multiplier | heal/interval (lvl 1) | total | change |
|---|---|---|---|---|
| 1 | 1.80 | 3.6 | 3.6 | **+80%** |
| 2 | 1.35 | 2.7 | 5.4 | **+35%** |
| 3 | 1.15 | 2.3 | 6.9 | +15% |
| 4 | 1.05 | 2.1 | 8.4 | +5% |
| 5+ | 1.00 | 2.0 | 10, 12, 14… | unchanged |

Totals are strictly increasing, and the four-victim case that already worked barely moves.

**Implementation:** a hediff comp only ever knows its own pawn, so the victim count is taken by
scanning for other pawns carrying the same hediff with the same caster recorded on it. That is
one pass over the spawned-pawn list, run **only on the drain interval, never per tick** — which
is also why it is counted live rather than cached, since a cache would have to stay correct as
victims die and new ones are struck.

Verified after the edit by reading the multipliers back out of the XML and checking the
monotonic rule numerically, rather than trusting the arithmetic in the comment.

---

## Balance — Drain Vitality's healing raised, and it now clears blood loss (2026-07-28)

Reported: a bleeding Dovahkiin "barely recovered his wound over time despite hitting 4 raiders
at the same time". Two separate causes, and the second is the interesting one.

### 1. The yield really was thin

At `casterHealFraction` 1.0 the caster got back exactly what each victim lost: **0.8 HP per
victim every 2 seconds**, capped at 10 applications. Four victims at level 1 is 3.2 HP per
2 seconds — **32 HP over 20 seconds**. Against a fresh arrow wound that is barely distinguishable
from natural healing.

Raised to **2.5**. Four victims at level 1 now return ~80 HP across the drain.

**The damage was deliberately NOT raised to compensate.** It is pinned at exactly half of Marked
for Death's by an earlier decision, and raising it would collapse the two shouts together. The
*yield* moves instead — this is a drain, and a Thu'um that steals life may reasonably draw more
than the wound cost its victim.

### 2. Healing a wound never touched the blood already lost

The real gap. `Hediff_Injury.Heal` lowers a wound's severity, which slows the bleed — but blood
loss is a **separate hediff** and nothing was reducing it. So the caster's wounds visibly closed
while they carried on reading as badly hurt, which is precisely what was described.

`HealCaster` now also drains the caster's `BloodLoss` severity, at
`casterBloodLossFraction` (0.5) of the healing, scaled down because blood loss severity runs on a
much smaller 0–1 scale than injury severity.

Both numbers are in `Hediffs_Dovahkiin.xml` and retune without a rebuild.

Builds clean, 0 warnings; all XML parses. **Awaiting playtest.**

---

## Polish — Slow Time goes map-wide, breath weapons up again (2026-07-28)

Storm Call confirmed working after the range fix. Four changes from that session.

### Slow Time now affects the WHOLE MAP

Reported: raiders slightly outside the radius carried on at normal speed while their neighbours
crawled, and it looked wrong. It is — **time does not have an edge.** A visible boundary makes
the effect read as a bug rather than as slowed time.

`bystanderRadius` **0 or less now means the entire map**, and all three levels use it. Cost is
one pass over the spawned-pawn list, a few dozen entries, not a cell scan. Allies are still
slowed too, deliberately, and it is still applied as a bare hediff with no `DamageInfo` and no
instigator, so no faction takes offence.

### Breath weapons raised again

Reported as feeling like "a poke attack rather than a heavy power move" — fair, given these are
devastating in TES5 even on Legendary.

| Shout | Phase 2a | +35% | Now |
|---|---|---|---|
| Fire Breath | 16 / 30 / 46 | 22 / 41 / 62 | **26 / 49 / 74** (+20%) |
| Frost Breath | 14 / 28 / 44 | 19 / 38 / 59 | **22 / 44 / 68** (+15%) |

Fire's `reburnFraction` puts its effective totals near **32 / 61 / 92**. Frost was raised at the
slightly lower rate on purpose, keeping the relationship settled over eight rounds in Phase 2b:
fire is deadlier by **behaviour** — re-burn concentrates hits on existing wounds and destroys
parts — not merely by carrying a bigger number.

### Fire and Frost already had identical range — but both had a 1-tile flaw

Asked to check whether their ranges differ and match them if so. **They did not differ**: both
run cone 40/45/55 and range 7/10/13 at every level. The impression of a difference is most
likely fire's ignition and re-burn making its reach *look* longer.

But the check turned up a genuine bug affecting **both equally**: at level 1 the abstract base
grants verb range **8** while the cone reached only **7**, leaving a one-tile band that could be
aimed at but never hit. Both now use 8.

### Storm Call range 38 -> 46 (+20%)

Third range change across two playtests: 25 -> 38 -> 46.

Builds clean, 0 warnings; all XML parses. Verified after the edits that Unrelenting Force's
3 / 7 / 12 is untouched — it shares a file and a field name with Fire Breath.

---

## Phase 2g-fix — Storm Call reported "no targets" with enemies plainly outdoors (2026-07-28)

Playtest: Storm Call worked, but once claimed there was nothing under open sky while **more than
one unroofed target** was present. Also requested: range +50%.

### The bug: range was measured from the wrong place

The radius was checked against **the cell the storm spawned in**, fixed at cast. The storm did
not follow the caster. Walk away after casting — which is the natural thing to do in a fight —
and enemies silently fell out of reach while remaining visible and outdoors. At 25 tiles on a
250-tile map that is easy to trigger without noticing.

**Fixed:** range is now measured from the caster's **current position, re-read every strike**.
That is also more faithful — in TES5 the storm follows the Dragonborn rather than hanging over
the spot where it was called. Falls back to the storm's own cell if the caster dies or despawns
mid-storm.

### The deeper problem: the message could not say which rule rejected them

One generic "no enemy under open sky" message covered three completely different situations, so
a range failure read as a roof failure. **The report was impossible to act on**, which is the
real defect here — worse than the range bug itself.

Now three distinct messages, chosen from sticky flags recorded during the storm:

| Situation | Message |
|---|---|
| Hostiles in range, all roofed | *"…every enemy in reach stands beneath a roof."* |
| Hostiles present but outside the radius | *"…too far off for it to reach."* |
| No hostile pawns at all | *"…finds nothing to strike."* |

The counters are set only **after** a pawn has passed the hostility and faction tests, so a
peaceful trade caravan across the map can never be reported as an out-of-range enemy.

### Also

- **Range 25 → 38** (+50%), in `DovahkiinTuningDef`.
- **`legalTargets` was static; it is now an instance field.** A shared scratch list between
  concurrent storms is a latent bug — two storms can coexist after a save is loaded mid-storm.
  Not the cause of this report, but found while reading the code for it.
- Added a check that every `"Dovahkiin_*".Translate()` key in C# exists in the keyed XML. All
  resolve; it would have caught a stale reference to the old message key.

Builds clean, 0 warnings; all XML parses. **Awaiting retest.**

---

## Phase 2g — Storm Call (2026-07-28)

`SPEC.md §4.4e`. **12 of 14 core shouts built.** First of the three hard ones. Builds clean,
0 warnings; all XML parses. **Awaiting playtest** — `TESTS/phase2g.md`.

### The outdoor rule is the whole design

`SPEC.md §4.4e` makes a cell a legal strike target only if **all three** hold: it contains a
pawn hostile to the player; that pawn is not a colonist, player-faction, tamed or a neutral
visitor; and **the cell is unroofed**.

Rule 3 is what makes the shout useless indoors — thematically right for calling a storm — and it
is also what **settles the fire question** the spec previously left open. Strikes cannot land
inside a base, so they cannot ignite a stockpile, a wooden wall or a roofed corridor. Ignition
on open outdoor terrain near enemies is acceptable and is deliberately left on.

All three rules live in `Thing_StormCall.IsLegalTarget`, and there is a **second roof check
immediately before the bolt is fired**. Redundant by construction, and deliberately so: `SPEC.md`
states a strike must never land under a roof, and one extra grid lookup on a rare event is a
cheap way to make that unconditional rather than merely likely.

### Implementation

- **`Thing_StormCall`** — an ethereal Thing that ticks only while the storm runs, same shape as
  `Thing_ShoutWave`. One strike per interval, spread evenly across the duration.
- **Targets are re-evaluated for every bolt**, never captured on cast. Pawns move, die and duck
  under roofs mid-storm; a list taken at cast time would keep striking corpses and pawns who
  have since taken cover.
- **A strike is not consumed when no legal target exists.** If everyone happens to be roofed at
  that instant the storm holds its bolt and retries, so stepping into the open mid-storm still
  draws lightning. Casting with nothing outdoors therefore costs the shout but fires nothing —
  which is correct.
- **Selection walks the map's pawn list, not cells.** At radius 25 a radial cell scan is ~1,960
  cells per bolt; the pawn list is a few dozen entries. `CLAUDE.md` forbids avoidable cost and
  RocketMan is installed.
- Lightning is vanilla's `WeatherEvent_LightningStrike(Map, IntVec3)` fired through
  `map.weatherManager.eventHandler`, so the bolt visual, damage and ignition all come from the
  game rather than being reimplemented. Only the *targeting* is ours, which is exactly what
  `SPEC.md` asks for: "we write the strike rather than reusing the vanilla weather event."

### No target, and a message when it finds nothing

The ability takes **no target**: in TES5 the storm gathers over the Dragonborn, it is not
artillery placed on a spot. It therefore uses the self-cast shape — `targetRequired false` plus
`canTargetSelf` and a positive range — which is the shape that took two rounds to get right in
Phase 2c. Getting it wrong again would have given another dead button.

When a storm ends having landed **zero** bolts it posts a message explaining that nothing stood
under open sky. Without it, casting indoors is indistinguishable from a broken shout — and doing
nothing indoors is the entire point of the rule, which the player has no other way to learn.

### Balance

Most expensive shout in the mod, deliberately. Cost 10/16/24 thu'um and cooldown 2500/4000/6000
ticks, both the highest of anything built. The outdoor rule is what stops it simply being the
best option everywhere: it does nothing at all inside a base.

Strikes 3/6/12 and durations 180/420/900 ticks live in `DovahkiinTuningDef`. Strikes are spread
evenly across the duration, so raising the duration alone makes the storm slower rather than
heavier — noted in the def comment, since that is a non-obvious interaction.

---

## Balance — the two breath weapons up 35% (2026-07-28)

Playtest signed off the Thu'um gradient bar and confirmed Drain Vitality's transfer working.
One balance change requested: **both breath weapons +35%**.

| Shout | Was | Now |
|---|---|---|
| Fire Breath | 16 / 30 / 46 | **22 / 41 / 62** |
| Frost Breath | 14 / 28 / 44 | **19 / 38 / 59** |

Damage instances are unchanged (6 / 9 / 12 for both), so the extra lands as heavier hits on the
same number of body parts rather than spreading wider — which keeps the blood-loss behaviour
that Phase 2b-fix5 was careful to bound.

**Both raised by the same 35%**, deliberately: Phase 2b-fix7 and fix8 spent eight rounds getting
these two into the right relationship, where fire is decisively deadlier than frost by
*behaviour* rather than by a bigger number. Fire's `reburnFraction` of 0.25 repeats a quarter of
its hits on already-damaged parts, so its effective totals are now roughly **27 / 51 / 77**
against frost's 19 / 38 / 59. Raising only one would have thrown that away.

Unrelenting Force's spread blunt (3 / 7 / 12) is untouched and was explicitly verified after the
edit — it lives in the same file as Fire Breath and shares the `damageAmount` field name.

---

## Fix — Drain Vitality now actually transfers, and the Thu'um bar is a real gradient (2026-07-28)

Two defects from playtest. Both were mine; both reports were exactly right.

### Drain Vitality drained stamina into nothing

Reported: casting it on two pawns left the caster's stamina regenerating at the same rate as
before. Correct, and there were **two independent reasons**, which is why it looked like total
silence rather than a partial effect:

1. **The transfer did not exist.** Only *health* was given back to the caster; drained stamina
   and mana were simply removed from the victim and discarded. A drain that destroys rather than
   transfers is not a drain, and the original spec's "drains stamina, then stamina and mana"
   plainly implied the caster receives it.
2. **Both victims were classless** — the user noted this themselves and it is the decisive
   detail. `COMPAT.md §5`: `TM_Stamina` exists only on a pawn carrying `TM_MightUserHD`. A
   classless pawn has **no stamina bar at all**, so there was nothing to take and nothing to
   hand over. Even a correct implementation would have shown zero on that test.

**Fixed.** `TryDrain` now returns **how much was actually taken** rather than a bool — which
matters, because a nearly-empty bar yields less than asked and an absent bar yields nothing. New
`TryGive` hands exactly that figure to the caster, capped at their own maximum. So the caster
gains precisely what the victim lost, never a flat amount conjured from nowhere.

**Rest and Joy are deliberately NOT transferred.** They are the vanilla stand-ins used when a
victim has no magic class; refilling the caster's sleep meter by shouting at people would be an
exploit rather than a drain.

**Precondition worth knowing:** the caster only gains stamina if the *caster* has a stamina bar,
i.e. carries an RWoM might class. A classless Dovahkiin draining a classless victim correctly
does nothing on that axis, and the vanilla Rest/Joy drain plus the health drain still apply.

Tuned by `casterNeedGainFraction` (default 1.0) in `Hediffs_Dovahkiin.xml`.

### The Thu'um bar was split the wrong way, and was not a gradient

Reported: *"you split it down the middle horizontally and not vertically + it's not gradient,
it's literally just orange on one half and purple on the other."* Both true.

The previous attempt drew two **flat** colours as **stacked halves** — a horizontal seam,
top/bottom, with a hard edge. What was wanted is a vertical seam, left/right, with the two
colours fading into each other.

**Now a real horizontal gradient**, from a single cached 128×1 texture: deep violet on the left
through to ember orange on the right. The blend is smoothstepped across the middle 40% so each
colour still owns roughly half the bar — a "50/50 gradient" rather than a straight linear ramp,
which would read as mud through the centre.

The strip is anchored to the **full** bar width and clipped by fill via `texCoords`, not squashed
into the filled part. That is what keeps the colour meaningful: a given x is always the same
shade, a full bar reaches the ember end, and a nearly-spent one shows only violet — so the bar
visibly cools as it empties, which was the original intent all along.

Third attempt at this bar. All three are documented in the method comment so the next session
does not re-tread them.

Builds clean, 0 warnings; all XML parses. **Awaiting playtest.**

---

## PLAYTEST PASS — Phases 2d, 2e and 2f all signed off (2026-07-28)

User confirmed every shout in those three phases works. **11 of 14 core shouts are now built
AND signed off**, with no outstanding defects against any of them.

Confirmed working:

- **Drain Vitality** — need drain by level, damage-over-time at half Marked-for-Death strength,
  and the caster healing for what it takes. The RimWorld-of-Magic soft integration holds: mana
  and stamina drain when the mod is present, vanilla Rest/Joy carry it when it is not.
- **Dismay** — vanilla `PanicFlee` routing enemies, plus the lingering debuff.
- **Cyclone** — the travelling vortex, after two wrong shapes (a spiral arm, then a filled disc).
- **Become Ethereal** — the attack block via `Verb.TryStartCastOn` after `Pawn.TryStartAttack`
  proved to cover only AI-initiated attacks, and total damage immunity via
  `Pawn.PreApplyDamage`.
- **Slow Time** — self-haste plus `Dovahkiin_TimeSlowed` on everyone else in radius, with no
  faction taking offence, which was the risk in applying it to allies.
- **Clear Skies** — still working after the ring and thunder changes.

This closes the run of defects that started with the dead-button bug in 2c. Nothing from 2c
through 2f is outstanding.

**Not yet verified:** the 15 shout icons, which landed after this playtest.

**Next:** the three hard shouts — Storm Call (`SPEC.md §4.4e`), Soul Tear (`§4.4f`) and
Dragon Aspect (`§4.4d`). Dragon Aspect's overlay is a stop-and-report, never a silent downgrade
to a stat buff.

---

## Art — all 15 shout icons now ship with the mod (2026-07-28)

**The mod no longer borrows a single piece of vanilla art for its shouts**, and the Biotech icon
defect is closed. 15 icons at 256×256 with real alpha, in `Textures/UI/Abilities/`.

### They are generated, and the generator is the source

Two deterministic PowerShell scripts live in `Tools/`:
`GenerateIconMaster.ps1` draws the white master; `GenerateShoutIcons.ps1` recolours it into all
15 from a table. **Retuning a shout is one row and a re-run** — the defs point at fixed
filenames, so nothing else moves. The master PNG is kept as `Tools/icon_master.png` so the icons
can be re-derived without redrawing.

### The three levers, set by the user during review

1. **body colour**, 2. **core colour** (the bright circle in the head, tinted *independently*),
3. **opacity**.

Lever 2 was added specifically because of Slow Time: it wanted a pale grey-white comet with a
**blue** core. Before the core was separately tintable it always blew out to white, which made
Slow Time nearly identical to Whirlwind Sprint. Become Ethereal uses opacity 0.72, Cyclone 0.60.

Two corrections applied from review: **Unrelenting Force is now blue**, deeper than Frost
Breath; **Slow Time takes Unrelenting Force's old pale grey-white** body with the blue core.
The rest stay as specced and will be tuned one at a time during playtest.

### How the design was chosen

Four candidates were rendered and compared on a RimWorld-dark button at 120/64/40/24 px:
a pinwheel swirl, a thin comet, a beefed comet, and the beefed comet with A RimWorld of Magic's
treatment. The last won: **256×256 (matching RWoM's own icon size), a dark rim, a hot core, and
a saturated tint that keeps the rim dark and lets only the core blow out.**

Two bugs were found and fixed during that process, both instructive:

- **A flat-white shape cannot be tinted.** The first outlined version was drawn at a uniform
  luminance of 1.0, and the tint's "hot core" rule whitens anything above 0.86 — so the *entire*
  icon bleached and Fire, Frost and Drain came out identical. The shape needs internal shading
  for a luminance-driven tint to have anything to grip.
- **PowerShell variable names are case-insensitive.** `$out` silently clobbered `$OUT` (the
  output path) and the PNG was saved to a file literally named after a float; separately
  `$final` clobbered `$FINAL`. Both cost a run to find. Noted in the scripts.

### Honest limitation, recorded rather than hidden

Against RWoM's icons — the fair comparison, since they share a command bar — ours are cleaner
but plainer. Theirs carry internal detail that comes from being drawn by hand; RWoM ships 1,406
PNGs across RimWorld 1.0–1.4. More generation passes would refine ours, not close that gap.
Replacement art drops in with no def or code change. Full write-up in `ART_TODO.md`.

**Verified:** 23 of 23 `<iconPath>` entries resolve to files inside `Textures/`; no vanilla path
remains; all XML parses; build clean, 0 warnings.

---

## Phase 2e-fix — the ethereal caster could still swing (2026-07-26)

### Become Ethereal's "cannot attack" never worked for a player-ordered attack

Reported as *"the caster can still harm pawns"*. Correct, and the diagnosis is a clean one.

`Pawn.TryStartAttack` — which the patch hooked — is the **AI's** entry point: a pawn choosing
its own target. A **player-ordered attack on a drafted pawn never goes through it.** The job
driver calls `Verb.TryStartCastOn` directly. So the block worked against raiders attacking of
their own accord and did nothing at all when the player clicked an enemy, which is exactly the
case a playtester exercises.

**`Verb.TryStartCastOn` is the real chokepoint** every attack passes through — melee and ranged,
AI-driven and player-ordered. Both overloads are now patched, because the five-argument form is
not guaranteed to delegate to the six-argument one. The old `TryStartAttack` patch is kept: it
still catches the AI path earlier and more cheaply.

**Shouts are deliberately still allowed.** `VerbProperties.violent` was rejected as the test —
it **defaults to true**, so it would have blocked Clear Skies and every other harmless shout as
well. The test is `!(verb is Verb_CastAbility)` instead: hands blocked, Voice free.

### "Nothing can harm the caster" is now absolute

Asked for magic, traps and explosions to be covered too. `IncomingDamageFactor 0` is a
*multiplier*, and a multiplier only helps for damage routed through that stat.

Now prefixed on **`Pawn.PreApplyDamage`**, which sits inside `Thing.TakeDamage` itself and is
therefore the one place every damage source in the game passes through — vanilla, DLC, other
mods, traps, fire, explosions. Absorbing there is what vanilla shield belts do. The stat factor
is **kept as well**, deliberately: it is what makes the Stats tab read "incoming damage 0%",
which is the only in-game feedback the player gets that the shout is working.

### Cyclone: the problem was never the radius

Reported as "too wide, and there is no vortex/tornado", with a pointer at RimWorld of Magic's
visuals. Looking at how RWoM actually does it was the answer: `Mote_ManaVortex` is **one
purpose-drawn sprite that spins in place** (`UI/manavortex_trans`).

That exposed the real fault. Cyclone was filling every cell of its disc with dust, and **a
filled disc of particles has no structure to read as rotation** — no radius or tint would ever
have fixed it. It is now drawn as a **funnel**: three concentric orbits of particles at
different radii, spinning at different rates (inner tighter and faster), each fleck individually
rotated via `FleckCreationData.rotation`/`rotationRate`, with the outer edge fainter so the core
reads as solid. Same particle count, arranged instead of scattered.

Radius also cut, as asked: 2.2/2.6/3.0 → **1.2/1.5/1.8**.

**Honest limitation, logged in `ART_TODO.md`:** one drawn swirl sprite would beat this outright
and is the correct fix. It is blocked on art — I cannot draw it, we cannot use RWoM's asset, and
**Core's textures are packed into Unity bundles** (`Data\Core` has no `Textures` folder), so
there is no vanilla swirl to point a `FleckDef` at. The spec for an artist is written up.

### No change needed — Drain Vitality already heals 100%

The user thought they had specified 50% and asked for 100%. `casterHealFraction` was already
**1.0**: the original wording ("heal equal to those 50% of damage done") was read as *heal equal
to the damage dealt*, that damage itself being half of Marked for Death's. Already correct, so
nothing was changed. Recorded here so it is not "fixed" again later.

### Also

- **Clear Skies ring 50% more transparent** — new `ringAlpha` 0.5 on all three levels.

Builds clean, 0 warnings; every def file parses. **8 Harmony patches**, all event-shaped, all
combat-path ones still opening with the registry reference compare.

---

## Phase 2e — Cyclone becomes a tornado, Slow Time slows the world (2026-07-26)

Playtest feedback on 2d. Dismay signed off with no changes. Everything else adjusted, plus two
design questions the user explicitly handed to me.

### DECISION — Slow Time: slow everyone else, do not touch the clock

The user asked directly whether the game could run at **0.5x** with the Dovahkiin exempt, rather
than merely hasting the caster, and asked me to judge the risk. **Answer: no, and here is why**,
established from the assembly rather than assumed:

- **RimWorld has no sub-normal speed to reach for.** `TimeSpeed` is `Paused, Normal, Fast,
  Superfast, Ultrafast` — nothing below Normal exists.
- **`TickRateMultiplier` is a computed getter with no setter**, read inside `TickManagerUpdate`
  in the innermost tick loop. Forcing 0.5 means Harmony-patching that getter.
- **`TimeSlower` cannot help** — it only has `forceNormalSpeedUntil`, which *forces normal
  speed*, it does not slow below it.
- **RocketMan is installed**, and manipulating tick throughput is precisely what RocketMan
  exists to do. A mod contending with it over the tick rate is the worst possible place to
  fight.
- It is **global** — every map, every caravan, world time — and a failure to restore speed on
  save/load leaves the player's game permanently at half speed.
- **It would not even save work.** Exempting the Dovahkiin from a global slowdown means speeding
  them back up 2x, which is exactly the self-haste already built. Option 2 = option 1 **plus** a
  dangerous global patch.

`SPEC.md §4.4a` already forbade touching `Find.TickManager`; this is that ruling re-derived
rather than merely obeyed. **Option 1 built instead, and extended:** the caster is hasted *and*
every other pawn in radius gets `Dovahkiin_TimeSlowed` (MoveSpeed −1.2/−2.0/−2.8, aim penalty).
The picture is identical — the world crawls, you do not — with none of the risk.

**Allies are slowed too, deliberately**, because the effect is about relative speed. It is
**not an attack**: a bare hediff, no `DamageInfo`, no instigator, no `TakeDamage`, and the
ability is `hostile=false` — so no faction takes offence and no ally turns on you.

### DECISION — Become Ethereal renders semi-transparent, via vanilla invisibility

Asked whether the pawn could be made transparent, and flagged the risk of modded pawn content
not respecting it. That worry is well founded, and it is exactly why this does **not** patch
`PawnRenderer`: that is what RocketMan contends with, and it would have to be taught about every
modded rendering path in the list (Melee Animation, the xenotype mods, Gloomy Face).

Instead it uses **vanilla's own invisibility**, declared exactly as Royalty's
`PsychicInvisibility` does — a plain `HediffCompProperties` carrying
`compClass HediffComp_Invisibility`. The Royalty *def* is DLC content, but the **class lives in
`Verse` inside `Assembly-CSharp`**, so this is baseline-safe. Same reasoning that made
`PawnJumper` usable in Phase 2a. Anything that renders a vanilla invisible pawn correctly
renders this correctly, for free.

**Known and intended side effect:** vanilla invisibility also makes the pawn hard to target. For
Become Ethereal that is arguably more faithful — "nothing can touch you" — and it removes the
odd sight of raiders beating on an invulnerable colonist. Flagged for judgement in play; if
unwanted, deleting that one comp leaves the invulnerability intact.

### Cyclone was the wrong shape entirely

Reported as "doesn't behave like Skyrim's cyclone at all" — correct. It was built as a spiral
**arm sweeping outward**; what was wanted is a **tornado advancing toward the target**.

The distinction that matters: **a vortex is local and moves; a cone or spiral is a front that
expands.** New `vortexRadius` mode is a compact disc of cells centred on a point that slides
along the travel line, banded by distance **along** that line rather than radially — which is
what makes the column travel as one body. `Thing_ShoutWave` now caches each band's centre so the
visible arc can spin around it (two opposed arms, so it reads as rotation rather than a wobble).
Spin is cosmetic only; damage still covers the whole band.

Damage instances halved again to **1 / 2 / 2**. Totals unchanged, just fewer body parts.

### Drain Vitality now steals life

Asked to behave like Marked for Death at half strength, without the armour penalty, and to heal
the caster by what it drains. Marked for Death deals 1.6 per interval per severity over 10
applications, so this is **0.8 over 10** — literally half — and it now runs from level 1 rather
than only level 3.

The hediff needed to know **who** cast it, which an ordinary `Hediff` cannot carry, so
`Hediff_VitalityDrained` subclasses `HediffWithComps` with a `drainedBy` pawn saved via
`Scribe_References` (never `Scribe_Deep` — the caster exists elsewhere in the save and must not
be duplicated into the hediff). A null after load simply means no healing. Healing targets the
caster's **worst** injury and reduces its severity, so it cannot restore a destroyed part, cure
disease, or overheal.

### Everything else in this pass

- **Marked for Death recoloured** to blue-grey leaning grey `(0.58, 0.63, 0.70)`. It was a
  grey-blue-violet, and once Drain Vitality arrived in deep purple the two read as the same
  effect on screen. Marked for Death gives up the violet entirely; hediff label, wave fleck,
  victim glow and all three ability tints updated together.
- **Thu'um bar is now a 50/50 split**, not a continuous blend. The old bar lerped one flat
  colour across the whole range, so at any given moment it was a single shade and the gradient
  was only visible by watching it drain. It is now drawn as two stacked halves — violet
  underneath, ember on top — each still shaded slightly by fullness, so it reads as a gradient
  standing still.
- **Durations raised by half again**, both still judged too brief:
  Slow Time 600/1200/1800 → **900/1800/2700** (15s / 30s / 45s);
  Become Ethereal 480/900/1440 → **720/1350/2160** (12s / 22s / 36s).
- **Slow Time's ring** is bigger (9/11/13 → **14/17/20**), slightly faster (26/28/30 → **30/32/34**
  cells/s) and now near-white at **30% opacity** instead of solid sand-gold. The transparency is
  a new `ringAlpha`, because the wave computes alpha per band — a low alpha in the fleck def's
  colour would simply be overwritten.
- **Clear Skies' ring** reduced 14 → **9** and pushed further toward blue.

### Caught during the work

- A `HediffDef` may have **only one `<comps>` block**. Adding the invisibility comp created a
  second one on `Dovahkiin_Ethereal`; the XML still parsed, so nothing would have complained —
  RimWorld would simply have taken one and silently dropped the other. Found by an explicit
  duplicate-`<comps>` check, now part of the validation sweep.
- A chained PowerShell string-replace on the duration values collided (720→1200 then 1200→1800
  hit the same text twice, producing 1800 twice). Inspected before writing rather than after.
  **Bulk find-and-replace on numbers is not safe when the replacements overlap** — the Edit tool
  with surrounding context is.

Builds clean, 0 warnings; every def file parses. **Awaiting playtest** — `TESTS/phase2e.md`.

---

## Phase 2d — three new shouts, ring bursts, and a duration pass (2026-07-26)

Playtest confirmed Phase 2c working: all three shouts fired, voice strain present for each.
Clear Skies confirmed repaired. Four pieces of feedback, all acted on, plus a scope change.

### Scope change — the shout list grows from 11 to 14

**Drain Vitality (Gaan Lah Haas)** and **Dismay (Faas Ru Maar)** were on `SPEC.md §4.4c`'s
deferred list, which says *"Promoting any of these costs three more word walls and a re-cost of
§7. Ask first."* The user asked for both by name, which answers that. **Cyclone (Ven Gaar Nos)**
is from the Dragonborn DLC and was on neither list; also requested by name.

**Consequence, recorded rather than rediscovered:** core shouts 11 → **14**, word walls 33 →
**42**. `SPEC.md §4.4` forbids growing the list "without re-costing §7". That re-cost belongs to
Phase 7, which builds the world content, and is **deliberately not done here** — it would be a
number invented against unbuilt content. Phase 7 must raise wall density or accept more walls
per site. Flagged in `Shouts_Batch3.xml` and in the notebook.

### The three shouts

- **Drain Vitality** — no direct damage at all; the whole effect is `Dovahkiin_VitalityDrained`,
  whose severity is the shout level. New `HediffComp_DrainNeeds` reads it: level 1 stamina,
  level 2 adds magicka, level 3 adds health (capped at 12 applications).

  **RimWorld of Magic is recommended, never required**, per `CLAUDE.md`. `COMPAT.md §5` already
  established that `TM_Stamina` and `TM_Mana` are ordinary `NeedDef`s, so the comp resolves them
  by defName through `DefDatabase.GetNamedSilentFail` and drains them through the **vanilla
  `Need.CurLevel` API**. No assembly reference, no reflection, no `MayRequire` needed in C#.
  With the magic mod absent — or on any pawn with no magic class, which is most pawns — those
  lookups return null and the vanilla Rest/Joy drain carries the shout alone. The fallback is
  deliberately not nothing: the victim is always visibly worn down.

  Health damage is `Dovahkiin_SoulWither`, the Blunt-parented def written for Marked for Death.
  A wasting curse must not make the victim *bleed*, or spreading it kills by cumulative blood
  loss. The C# fallback is `Blunt` and explicitly **not** `Deterioration`, which is the item
  decay type and does nothing to a pawn — the Phase 2b-fix2 bug, nearly re-trodden.
- **Dismay** — fear. The wave now carries a `MentalStateDef`, applied as a **control effect
  before damage**, per the standing ordering rule: a downed pawn cannot flee. Uses **vanilla
  `PanicFlee`**, not RWoM's private `TM_PanicFlee`, so it works with no other mod present.
  Chance 0.35 / 0.60 / 0.90 by level, plus a lingering `Dovahkiin_Dismayed` debuff.
- **Cyclone** — very light spread blunt (1/3/5 against Unrelenting Force's 3/7/12) traded for a
  much longer stun. Asked for "Fus Ro Dah plus half": FRD stuns 0/180/300, so this is 270/450 at
  levels 2 and 3. **Level 1 gets 150 rather than 0** — a deliberate departure, because a level-1
  Cyclone with no stun and 1 damage would do nothing whatsoever, and the stun is the shout.

### New wave capabilities

- **Swirl mode.** `Thing_ShoutWave` gains `swirlWidth` / `swirlTwistPerCell`: a spiral arm whose
  direction rotates as it advances, so the front visibly turns instead of moving flat. Plain
  `Mathf.Atan2`/`DeltaAngle` trigonometry — RimWorld does have a `Vector3.RotatedBy` extension,
  but it was not verified against the 1.4 assembly, so it is not used.
- **`alphaScale`.** Colour alone cannot make a front fainter; the wave computes alpha per band,
  so "even less visible than Whirlwind Sprint" needed an explicit multiplier. Cyclone uses 0.45.
- **`spreadDamage` on the cone comp.** Existed only on the knockback comp. Cyclone's light
  bruising would otherwise crush toes.

### Ring bursts — and a latent bug they exposed

`ShoutTargeting.SpawnRingBurst` spawns the ordinary wave at **`coneAngle 360`** with **no
payload**: no damage, no hediff, no stun, so it cannot harm a bystander. At 360, `half` is 180
and `Vector3.Angle` never exceeds 180, so every cell passes and the front expands as a circle —
no special-case geometry was needed.

**It did expose a real latent bug.** `BuildRings` returned early when `towards == origin`, which
is exactly the self-cast case, leaving the wave with no cells at all — an invisible shout. It
now falls back to an arbitrary facing. Nothing shipped had hit this only because no shout had
ever spawned a wave centred on the caster.

- **Slow Time** now fires a sand-gold ring outward from the Dovahkiin, plus vanilla's
  `Fleck_HeatWaveDistortion` — a near-invisible ripple, which is the "ceremony boom" effect
  requested. It is **Core**, so it is baseline-safe.
- **Clear Skies** gains the same treatment in pale sky-blue. It previously had **no cosmetic
  effect at all**, which is why a successful cast could look like nothing happening.

### Tuning and audio

- **Durations raised**, both judged too brief even at one word.
  Slow Time 360/720/1200 → **600/1200/1800** (10s / 20s / 30s).
  Become Ethereal 240/480/780 → **480/900/1440** (8s / 15s / 24s).
- **Clear Skies sound** was `Thunder_OffMap` played on camera — the distant weather rumble,
  which read as no sound. Now `Thunder_OnMap` positional, matching Unrelenting Force.

**8 → 11 of 14 core shouts built.** Builds clean, 0 warnings; every def file parses.
33 words, 11 shouts, 33 abilities. **Awaiting playtest** — `TESTS/phase2d.md`.

---

## Phase 2c-fix — the dead button, and Clear Skies was broken all along (2026-07-26)

Playtest: *"None of them worked, no effects, nothing in the health tabs."* Taken literally, that
says the hediff was never **added** — a different bug from one that adds it and does nothing —
and that turned out to be the whole key.

### Diagnosis, before any code was touched

The log was clean: `All critical defs present`, zero XML errors, zero exceptions, no
cross-reference failures. So the defs loaded and `hediffDef` resolved. The save settled the rest:

| Evidence from `Dovahkiindebug.rws` | Meaning |
|---|---|
| all 24 words discovered | `Learn all words` worked |
| both shouts at level 1 | levels were raised |
| `Dovahkiin_Ability_SlowTime_1` + `..._BecomeEthereal_1` on the pawn | **the buttons existed** |
| **`Dovahkiin_VoiceStrain` absent, Voice `ageTicks` 1101** | **no shout was ever cast** |

Strain is added on every successful cast and decays at 6/day — ~3400 ticks. The Voice was 1101
ticks old, so strain could not have decayed away. **The ability never fired**, which ruled out
the entire self-buff comp and pointed at the ability def itself.

### Root cause

**A no-target ability must declare `<canTargetSelf>true</canTargetSelf>` in `targetParams`, and
a positive `range`.** Ours had neither: `targetRequired false` with `range -1` and no
`targetParams` at all. RimWorld auto-selects the caster as the target, the verb rejects it as
invalid, and the cast silently never begins — no error, no exception, nothing in the log. A
button that is present, enabled, clickable, and completely inert.

Verified against the only three vanilla abilities that self-cast this way — `SmokepopMech`,
`FirefoampopMech` (Biotech) and `Neuroquake` (Royalty). All three carry `canTargetSelf` and a
positive range.

**Why the wrong shape was chosen:** all of this was modelled on vanilla's `SpeechBase`, which
genuinely does use `range -1` with no `targetParams`. But `SpeechBase` is not a normal ability —
it runs through ritual machinery (`gizmoClass Command_AbilitySpeech`, `Precept_Ritual`) that a
plain self-buff cannot reach. Copying its shape without its machinery yields a dead button.

A first guess that `gizmoClass` was the missing piece was **wrong** and discarded: reflection
shows `Command_AbilitySpeech` overrides only `Ritual` and `Tooltip`, never `ProcessInput`.

### Which means Clear Skies has never worked

`Dovahkiin_ClearSkiesBase` has carried the identical defect since Phase 2a. It was in
`TESTS/phase2a.md` as Test 6 step 3, but the changelog only ever records Unrelenting Force and
Fire Breath being confirmed — Clear Skies was never actually verified, and its effect (weather
changing) is easy to miss. **Three phases of "done" rested on an untested button.**

### Fixed

- `Dovahkiin_SlowTimeBase`, `Dovahkiin_EtherealBase`, `Dovahkiin_ClearSkiesBase`: added
  `targetParams` with `canTargetSelf`, `range` −1 → **3.9**, plus `drawAimPie false` and
  `requireLineOfSight false` (line of sight to yourself is meaningless).
- The per-level `verbProperties` overrides on Slow Time 2 and 3 repeat the whole block —
  `verbProperties` is replaced wholesale by a child def, never merged, so omitting
  `targetParams` there would break exactly one level and no other. Audited: all 5 blocks carry it.
- **Caught by validation, not by the game:** the explanatory comment first written into
  `Abilities_Batch2.xml` contained `<--`, and **an XML comment may not contain a double dash**.
  RimWorld discards the entire file on that, which would have removed all six abilities and
  looked like a completely different bug. Every def file is now parse-checked with
  `[xml](Get-Content -Raw)`; all pass.

### Method note

The distinction that solved this in one round was *"the shout fired and did nothing"* versus
*"the shout never fired"*. The save answers it directly and the log cannot. Recorded in the
notebook as step 4a of the playtest loop.

---

## Phase 2c — Slow Time and Become Ethereal (2026-07-26)

`SPEC.md §4.4a`. **8 of 11 core shouts built.** Builds clean, 0 warnings. XML parses.
**Awaiting playtest** — `TESTS/phase2c.md`.

Both are self-buffs: no target, no wave, no damage. One new comp
(`CompProperties_ShoutSelfBuff`) covers both, so they differ only in XML. All three levels of a
shout share **one** HediffDef and differ by severity and duration, severity being the shout
level, so the hediff's stages scale with words known.

Two Harmony patches were added — the mod goes from **2 patches to 4**. That is an architectural
change, so the reasoning is recorded in full rather than assumed:

- **`Pawn.TryStartAttack` (prefix)** — Become Ethereal's "cannot attack" half.
- **`VerbProperties.AdjustedCooldownTicks` (postfix)** — Slow Time's attack-speed half.

Both are event-shaped, never per-tick, and both open with
`GameComponent_DragonbornRegistry.IsDovahkiin` — a reference compare that at most one pawn per
save can pass — so every other pawn in a battle leaves before touching a hediff list.

### Why patches at all, when every other buff in this mod is pure XML

Three facts established by reading the real assembly and the real Core defs, not from memory:

1. **RimWorld 1.4 has no invulnerability mechanic.** Reflecting over `Assembly-CSharp` returns
   **zero** types matching `Invulnerab`. That machinery arrived with Anomaly in 1.5, which this
   project does not have. So invulnerability is `IncomingDamageFactor` at factor **0** — Core
   defines it with `minValue 0` and describes it as *"a multiplier on all incoming damage"*, and
   Marked for Death already proves the stat works in this build by using it the other way.
   That half needs no patch. Only "cannot attack" does.
2. **`RangedCooldownFactor` is a Biotech stat.** It is in `StatDefOf`, which is exactly what
   makes it dangerous — its `StatDef` lives in `Data\Biotech`, not Core. Putting it in a hediff
   would have silently broken the baseline environment, which `CLAUDE.md` requires to run
   without Biotech. Caught before it shipped.
3. **There is no pawn-side melee cooldown stat at all.** `MeleeWeapon_CooldownMultiplier`
   belongs to the *weapon*, so a pawn hediff cannot touch it. No data-only route exists for
   either half of Slow Time's attack speed.

**Why `AdjustedCooldownTicks` specifically, and not one of the three `AdjustedCooldown`
overloads:** the IL of every method in `Assembly-CSharp` was scanned for callers.
`Verse.Verb.TryCastNextBurstShot` is the **only** runtime caller, and `Verb` is the shared base
of `Verb_MeleeAttack` and `Verb_Shoot` — so one postfix covers melee and ranged together. Every
other caller is a stat-display or debug worker, which means the Melee DPS readout reflects the
buff for free. Picking the wrong overload would have made Slow Time's melee half silently do
nothing, which is the exact "accepted and ignored" failure mode that cost two rounds in 2a-fix3.

### Slow Time does not slow time

`SPEC.md §4.4a` had already reworked this to self-haste and forbidden touching
`Find.TickManager`. Restating why, because the shout's name argues against it: slowing the world
in RimWorld means slowing the whole colony, every caravan and every job on the map, and it
fights RocketMan directly. The caster gets a MoveSpeed offset (+1.6 / +3.0 / +4.6) and an attack
cooldown multiplier (0.75 / 0.55 / 0.40) instead. Flagged to the user in `TESTS/phase2c.md`,
since the name now slightly oversells the effect.

### Become Ethereal's deliberate limit

`IncomingDamageFactor 0` stops **damage**, not everything. The pawn can still collapse from an
existing wound, starvation or a mental break. Recorded in the def comment and in the test
script as intent, not oversight — it is a combat panic button, not god mode.

### Also in this pass

- **Duration override**: `CompProperties_ShoutSelfBuff.durationTicks` rewrites
  `HediffComp_Disappears.ticksToDisappear` **after** `HediffMaker.MakeHediff`, because
  `CompPostMake` fills that field from its own props and would overwrite anything set earlier.
- **Refresh, never stack**: re-casting removes the existing hediff first. Severity is the shout
  level, so a second copy cast at a lower level would otherwise quietly weaken the first.
- Both new hediffs added to `ValidateCriticalDefs`. They are read from Harmony patches, where a
  null def means the shout silently does nothing rather than erroring — the worst failure shape
  this project has.
- `PsychicPulseGlobal` and `PsychicSoothePulserCast` resolved through `DovahkiinVanillaDefOf`.
  Both are Core. The first is authored `onCamera`, so it is played with `PlayOneShotOnCamera`;
  playing an onCamera sound positionally is inaudible from anywhere but the caster's own tile.
- Debug action renamed `Learn all words (slice)` → `Learn all words`. It always walked the whole
  database; only the label was stale.

### Found while working, not fixed — three icons are Biotech-only

`UI/Abilities/FireSpew`, `AcidSpray` and `Longjump` are shipped **by Biotech**, and Fire Breath,
Frost Breath and Whirlwind Sprint all borrow them. On a baseline install with Biotech disabled
those three shouts show the missing-texture square — a real violation of `CLAUDE.md` invariant 5
and `ROADMAP.md` exit criterion 5, cosmetic but real. **Not fixed unilaterally**, because
swapping them changes the appearance of three shouts already signed off. Options recorded in
`ART_TODO.md`. Batch two uses Royalty icons only, so the problem does not grow.

---

## Documentation — save notebook library (2026-07-26)

No mod code changed. Context-survival infrastructure, because this project spans many
conversations and chat history does not survive between them.

- **All project notebooks now live in one library:**
  `C:\Users\User\Documents\SaveNotebooks\`, with an `_INDEX.md` listing every project.
  This project's is `Dovahkiin-RimWorld-Mod.md`. It was briefly `Mods\Dovahkiin\HANDOFF.md`,
  which was the wrong call — buried five folders deep and unfindable. A one-line `NOTEBOOK.md`
  signpost remains in the mod folder. **One canonical copy, never duplicated**, since two copies
  drift and then neither can be trusted.
- **`/SAVE_66/` protocol defined** in `C:\Users\User\.claude\CLAUDE.md`, which loads in *every*
  project, not just this one: update the notebook, update the changelog, update the index, send
  the file, reply briefly. Works for any future project, including non-RimWorld ones.
- The notebook carries a **RimWorld 1.4 gotcha list** — every trap that cost a playtest round in
  this project. It is the highest-value section and must be appended to whenever a new one is
  found.

**New gotcha recorded, learned the hard way during this very task:** Windows PowerShell 5.1
`Set-Content` **double-encodes UTF-8**, silently turning dashes and apostrophes into mojibake.
It corrupted four documents here, and the obvious reverse-decode repair made them worse — they
had to be rewritten from scratch. Use the Write/Edit tools for any file containing non-ASCII
characters. Bulk regex passes over XML are safe only when the file is pure ASCII.

Also corrected `README.md`, which still claimed "Phase 0 complete, nothing playable yet".

---

## Phase 2b-fix8 — fire deepens, force concentrates (2026-07-26)

- **Unrelenting Force hits fewer parts**: 2/4/6 → **2/3/4**, totals unchanged (3/7/12), so each
  bruise lands harder. A shove should leave a few solid impacts, not a fine mist of them.
- **Fire Breath is now decisively deadlier than Frost Breath** — and by *behaviour*, not by a
  bigger number, which is the better lever. New `reburnFraction` (0.25) repeats a quarter of the
  hits immediately on the parts already burned, via a new
  `DovahkiinDamageUtility.SelectDeepenTarget` — the exact inverse of the spread rule.

  Concentrated damage destroys body parts; spread damage only hurts them. So fire burns the same
  wound deeper while frost keeps finding fresh skin, and the two end up feeling genuinely
  different despite similar totals. Effective totals ~20/37/58 against frost's 14/28/44.

  Organs are excluded from re-burning: this is meant to burn off a limb, not detonate a heart.

**Phase 2b is complete.** Six of eleven core shouts built, playtested and balanced across eight
rounds of feedback: Unrelenting Force, Fire Breath, Frost Breath, Clear Skies, Whirlwind Sprint,
Marked for Death.

---

## Phase 2b-fix7 — cross-shout rebalance (2026-07-26)

All six shouts signed off. Two balance consequences of the previous round.

- **Fire Breath's spread matched to Frost Breath's.** Frost was raised to 6/9/12 instances last
  round while fire stayed at 3/4/5, so identical totals burned far fewer body parts than they
  froze. Fire is now **6/9/12** as well. Totals unchanged; only the distribution.
- **Unrelenting Force was made redundant by Frost Breath.** Frost had gained the same stun
  duration *plus* heavy damage, leaving Fus Ro Dah as a strictly worse option. It now deals
  spread blunt damage of its own: **3 / 7 / 12** total across **2 / 4 / 6** hits, aimed by the
  same core-over-extremities rule as Marked for Death but applied instantly.

  12 at three words is roughly a quarter of Marked for Death's ~48 budget, per the user's
  "about 75% less" steer. Unrelenting Force keeps its identity — the only shout that *moves*
  people, and the cheapest hard stun — while no longer being pure utility.
- **Part-selection logic deduplicated** into `DovahkiinDamageUtility.SelectSpreadTarget`, now
  shared by `HediffComp_DamageOverTime` and `Thing_ShoutWave`. It had been rewritten three times
  in one file; a second copy would have guaranteed they drifted apart. Wave payload gained
  `spreadDamage`, opt-in per shout.

**Deliberately not spread:** Fire and Frost still hit random parts. A breath weapon engulfing a
target *should* catch a foot; the priority rule exists to stop a curse crushing toes, which is a
different problem.

---

## Phase 2b-fix6 — body-part priority, lane trail, frost spread (2026-07-26)

All three shouts signed off in playtest bar these refinements.

- **Marked for Death was downing everyone via leg damage.** The even spread worked, but "even"
  meant every toe and foot got crushed, and leg damage is what downs a pawn. Part selection now
  scores `damage taken / priority weight`, so a favoured part can carry proportionally more
  damage and still be picked — the bias persists instead of washing out after one pass.
  Weights: torso/shoulders/arms **3.0**, head/neck **2.5**, organs **2.0**, legs/feet/toes
  **0.6**. Organs sit below the torso deliberately, so a heart is never the first thing crushed.
  The spread system itself is untouched, as requested.

  This is the third revision of the selection rule. All three are documented in the method
  comment: fractions re-picked the torso forever, absolute damage spread too evenly, weighted
  score does both.
- **Whirlwind Sprint's trail is now a lane, not a cone.** `Thing_ShoutWave` gained a
  `laneWidth` mode: cells are chosen by perpendicular distance from the travel line rather than
  by angle, so the trail keeps a constant 2.4-cell width instead of fanning out at the landing
  site. A dash is a corridor, not a blast.
- **Frost Breath frostbites more of the body**: damage instances 4/6/8 → **6/9/12**. Same total
  damage, spread wider.

---

## Phase 2b-fix5 — Marked for Death bleed-out, frost stun parity (2026-07-26)

Frost Breath's freeze confirmed working once effect ordering was fixed. Whirlwind Sprint signed
off. One real problem left.

- **Spreading the damage made Marked for Death *more* lethal, not less.** Correct diagnosis from
  playtest: a small wound on every body part means many simultaneous bleeding wounds, and
  RimWorld sums bleed rate across all of them. Everything marked died of blood loss.

  Fixed at the root **and** capped, since the two solve different halves:
  - `Dovahkiin_SoulWither` re-parented from `Scratch` to `Blunt`. Scratch makes **cuts**;
    Blunt makes crushes and bruises, which barely bleed. It still damages properly and is still
    reduced by armour, pairing with the mark's armour penalty. A wasting curse should not make
    someone bleed in the first place — that was the actual mistake.
  - New `maxApplications` cap (10) on `HediffComp_DamageOverTime`, so total damage is bounded at
    `10 x 1.6 x severity` — about 48 at three words, spread across the body. Per-hit damage
    lowered 2.5 → 1.6.
  - The **armour penalty is deliberately not capped** and runs until the mark decays, so the
    shout keeps its tactical value after it stops biting. That was the user's option 2, kept
    alongside option 1.

  This is the third revision of this def; the file now carries the full history in a comment so
  the next person does not re-tread it.
- **Frost Breath's stun raised to match Fus Ro Dah** rather than half it: 180 ticks at level 2,
  **300** at level 3. The earlier "half" figure was set before the ordering bug was found, when
  the freeze appeared not to work at all.

---

## Phase 2b-fix4 — effect ordering, body-part spread, trail speed (2026-07-26)

- **The freeze was downing victims instead of holding them upright.** Zeroing the `Moving`
  capacity crosses RimWorld's downing threshold, so the victim collapsed and then stood up when
  the hediff expired — reported as "downed instantly, then stood up". `Dovahkiin_IceEncased` no
  longer touches capacities; it is now the visible marker plus a heavy `MoveSpeed` penalty, and
  the actual hold is a real stun again (75 ticks at level 2, 150 at level 3).
- **Effect ordering fixed, and this was the underlying bug.** Damage ran *before* control
  effects, so a heavy breath could down the victim first — and stunning an already-collapsed
  pawn is invisible and meaningless. Stun and secondary hediffs are now applied **before** any
  damage lands. This is why the stun appeared not to work in two consecutive playtests while
  the XML was correct both times.
- **Marked for Death was concentrating on the torso.** The spread logic compared health
  *fractions*, and a torso has far more max health than a finger — so its fraction stayed high
  and it kept winning selection. Now compares **absolute damage taken**, with a random tiebreak
  among equally-damaged parts (which is every part at the start). One hit on a finger
  immediately makes that finger the worst candidate, so the curse genuinely walks the body.
- **Whirlwind Sprint trail slowed** from 20/26/32 to 15/19/24 cells per second.

**Worth noting for future playtests:** 150 ticks is 2.5 seconds of *game* time, which at 3x
speed is well under a second of real time. Control effects should be judged at normal speed.

---

## Phase 2b-fix3 — ice-encasing, Marked for Death balance, sprint trail (2026-07-26)

Frost Breath declared good. Three follow-ups.

- **Frost Breath's stun did not reliably work.** A pawn walked out of a level-3 freeze. The XML
  was correct (`stunTicks 150` in the right comp), so the fault was the direct
  `StunHandler.StunFor` call, whose behaviour could not be pinned down by reflection.
  **Replaced rather than debugged:** new `Dovahkiin_IceEncased` hediff sets the `Moving` capacity
  to 0 (and Manipulation to 0.1) with `severityPerDay -400`, emptying in ~150 ticks. Guaranteed,
  visible in the health tab, and it reads as being frozen in place rather than merely stunned —
  closer to TES5 than a stun was. Level 2 gets ~1s, level 3 ~2.5s (half of Fus Ro Dah level 3).
  Payload gained `secondaryHediff` so a shout can leave two marks.
- **Marked for Death was an unavoidable death sentence.** Option 3 from the user's three:
  damage now always lands on the **least-damaged attached body part**, so the curse covers the
  whole body before deepening any wound. Same total damage, far slower to kill, and a tended
  victim can now outlast it. The shout stays stronger than a breath, but it is *slow* rather
  than *certain* — which was the design goal stated.
  Also fixed the fallback damage def from `Deterioration` to `Cut` (the former does nothing to
  pawns).
- **Whirlwind Sprint's trail was outrun by the pawn.** The wave speed was a shared constant.
  It is now per-shout (`waveCellsPerSecond`), and the dash trail runs at 20/26/32 cells per
  second — faster than the pawn, so it catches up — over a shorter range (8/13/19 rather than
  10/16/24).

**Not changed:** the pawn's own dash speed. That belongs to vanilla's `PawnJumper`, and altering
it means either editing a vanilla def other mods share or patching the movement path that
destroyed a colonist in Phase 2b. The trail now overtakes the pawn, which achieves the same
visual result from the other direction.

---

## Phase 2b-fix2 — balance and effects pass (2026-07-25)

Whirlwind Sprint confirmed no longer destroying pawns. Marked for Death confirmed travelling as
a wave. Five follow-ups from playtest.

- **Marked for Death dealt no damage at all.** `DamageDefOf.Deterioration` is the **item decay**
  type and does nothing to a pawn — which is exactly why none showed in the health tab. New
  `Dovahkiin_SoulWither` DamageDef (parented to `Scratch`, so it is real external violence
  reduced by armour, which pairs with the mark's armour penalty). Damage also raised
  1.2 → 2.5 per interval per severity, interval 300 → 240 ticks.
- **Marked for Death recoloured** from red to TES5's grey-blue-violet, via a new
  `Dovahkiin_Fleck_MarkWave`.
- **Marked victims now glow.** `Dovahkiin_Fleck_MarkGlow` is pulsed on the pawn through
  `FleckMaker.AttachedOverlay`, which rides the pawn's own draw position and touches nothing in
  the render pipeline — so, unlike a `PawnRenderer` patch, it cannot collide with other mods.
  This was the "skip it if it's trouble" request; it turned out to be cheap and safe.
- **Frost Breath made genuinely dangerous.** Damage 6/13/20 → **14/28/44**, split across
  **4/6/8** instances so it frostbites many body parts instead of destroying one. Level 2 gains
  a 1s stun, level 3 a **2.5s** stun — deliberately half of Fus Ro Dah level 3, as requested —
  for TES5's ice-encasing. It now also lays **snow in the wake of the front** (0.12/0.22/0.35
  depth) via `SnowGrid.AddDepth`, so the ground stays frozen and slows anything crossing it.
- **Fire Breath raised to match**: 10/18/26 → **16/30/46**, split across 3/4/5 instances.
- **Fus Ro Dah stun +1s** at both levels: level 2 → 3s, level 3 → **5s**.
- **Whirlwind Sprint gained a trail** — a near-white fading wave along the dash line, built from
  the existing cone comp with zero damage and no hediff, so it moves and harms nothing. The jump
  itself is still entirely `Verb_CastAbilityJump`; no movement code was reintroduced.

**Not done — ground-hugging dash.** The request was for the pawn to skim the ground rather than
arc. The arc lives in `PawnJumper.DrawPos`, and while that is virtual and overridable, the flyer
def is chosen inside vanilla's jump path — changing it needs a Harmony patch on the movement
code that destroyed a pawn last round. Deferred deliberately: not worth that risk for a
cosmetic change. Logged in `ART_TODO.md`.

---

## Phase 2b-fix — Whirlwind Sprint destroyed a pawn (2026-07-25)

Frost Breath verified good. Two faults, one severe.

### Whirlwind Sprint deleted the caster — **worst bug so far**

Reported as *"teleported the pawn into oblivion — no death signals, no body, nothing"*. Exactly
right: the pawn was destroyed outright.

**Cause:** the dash was implemented in an ability **comp**, moving the caster from inside
`Apply()`. That despawns the pawn into a `PawnFlyer` *in the middle of its own cast*, the cast
machinery then unwinds against a despawned pawn, and the flyer never lands it. The pawn is
inside a stranded flyer, invisible and unreachable.

**Fix:** vanilla's own Longjump (Biotech) does the entire jump in
`<verbClass>Verb_CastAbilityJump</verbClass>` and has **no comp for it at all** — the verb
orders the job so the despawn happens at a safe point. All three Whirlwind Sprint levels now use
that verb, and `CompProperties_ShoutDash` is **deleted** with a warning comment in the XML
against reintroducing it. `Verb_CastAbilityJump` is in `Assembly-CSharp`, so no DLC is needed.

Knockback was never affected — it flies *other* pawns, not the caster, and outside its own cast.

**Also added:** a `Recover pawns stuck in flight` debug tool that lands every `PawnFlyer` on the
map and logs what it rescued. The cause is fixed, but a pawn lost this way is otherwise
unrecoverable, and the tool costs nothing. `RespawnPawn` is protected, so it goes through
reflection, guarded — a failure leaves the flyer alone rather than destroying it with a pawn
inside.

### Marked for Death was a point-and-click, not a shout

Playtest: it should behave like the other shouts — travel as a wave, hit multiple targets — and
bleed the victim over time scaling with words known, as in TES5.

- Migrated from the bespoke single-target comp to `CompProperties_ShoutCone` with zero direct
  damage, carrying only the mark. It now travels and hits a cone (35°/45°/55°, range 16/18/20)
  and can target ground like every other cone shout.
- New `HediffComp_DamageOverTime` on `Dovahkiin_MarkedForDeath`: damage every 300 ticks scaled
  by severity — and severity *is* the shout level, so the bleed grows with each word.
  Interval-based, never per tick.
- `CompProperties_ShoutMark` deleted; the generalised cone comp covers it.

---

## Phase 2b — batch one: three more shouts (2026-07-25)

**6 of 11 core shouts done.** 18 words, 18 abilities. Builds clean, XML valid.
**Awaiting playtest.**

- **Frost Breath** — Fo Krah Diin. Cone, `Frostbite` damage, leaves `Dovahkiin_Chilled`
  (movement penalty and worse aim, decaying, three stages). Completes the trio dragons use and
  is the second of the two shouts draugr may know (`SPEC.md §4.5`, §4.6).
- **Whirlwind Sprint** — Wuld Nah Kest. A dash reusing the same `PawnFlyer` as knockback, so it
  reads as the same kind of motion. Lands on the furthest clear cell along the line, so it can
  never put the pawn inside a wall.
- **Marked for Death** — Krii Lun Aus. Single target only (`canTargetLocations false`, so it
  cannot be wasted on empty ground). Armour factors down, `IncomingDamageFactor` up, decaying.

**Refactor:** `CompProperties_ShoutFlameCone` generalised to `CompProperties_ShoutCone`, taking
`damageDef`, `appliedHediff`, `fleckDef` and `tint` as data. Fire and Frost now differ only in
XML rather than in code — the next elemental shout costs no C# at all. Fire Breath's three
abilities were migrated to it. The wave payload gained hediff application, stacking severity on
repeat hits rather than adding duplicate entries.

**Remaining:** Slow Time, Become Ethereal (batch one leftovers), then Storm Call, Soul Tear and
Dragon Aspect — the three with real risk attached.

---

## Design change — Soul Tear's dead puppet (2026-07-25)

Playtest confirmed the synced wave works for both Unrelenting Force and Fire Breath: damage and
effects now land only as the front arrives. **Phase 2a slice verified.**

User proposed handling Soul Tear's puppet as a charm plus an incurable timed wound rather than a
faction swap-and-restore. **Adopted**, because it removes the dangerous part rather than
mitigating it.

- The old design restored the puppet's original faction, and that restore had to be correct on
  seven exit paths including save → load. Getting it wrong leaves an unremovable
  pseudo-colonist — the mod's highest save-corruption risk (`RISKS.md §9`).
- The new design never restores anything: the puppet joins the player faction and carries an
  incurable, non-removable hediff that **kills it on expiry**. Every exit path already ends in
  death, so there is no broken-pawn state to reach, and the only thing that must survive a
  reload is an ordinary hediff on RimWorld's normal serialisation path.
- `SPEC.md §4.4f` rewritten; `RISKS.md §9` marked resolved and downgraded.

**Reconnaissance note:** RWoM's `TM_Dominate` turns out **not** to be a charm — it is a
fear/panic debuff driven by `mentalStateGivers`, so it is not the model. Its `TM_RaiseUndead`
plus `TM_UndeadHD`/`TM_UndeadStageHD` *is* the relevant precedent, and it does use
`SetFaction`/`SetFactionDirect` for a servant that persists until destroyed. Recorded in
`SPEC.md §4.4f`.

---

## Phase 2a-fix4 — the wave now delivers the hit (2026-07-25)

Playtest: visuals praised, `yol toor shul` confirmed working. Two faults left.

- **Effects were desynced from the wave, and the architecture was the cause.** The comp applied
  damage, stun and knockback on cast, while the visual took up to a second to arrive — so
  victims were flung before the blast reached them. Matching the two *speeds* in fix3 could
  never have fixed this, because the effects were not travelling at all.

  `Thing_ShoutWave` now **carries the payload** (damage def and amount, knockback, stun,
  ignition) and applies it band by band as the front passes, tracking who it has already hit.
  Every band between the previous tick and the current one is struck, so nobody is skipped when
  the front advances more than one cell per tick. The comps no longer damage anything — they
  spawn the wave and hand it the payload. Cause and effect now line up because they are the
  same event.
- **Fire Breath's sound was an insect noise.** `SoundDefOf.Hive_Spawn` was a poor pick; it is
  now `Explosion_Flame`. That def exists but is not in `SoundDefOf`, so it is resolved through a
  new `DovahkiinVanillaDefOf` class — field names there must equal the vanilla defName. It is
  Core, so no DLC dependency.

---

## Phase 2a-fix3 — the red X, the missing blue, matched speeds (2026-07-25)

Third round. The user's report contained the decisive clue — *"for a split second it showed a
red crossed square"* — which is RimWorld's missing-texture marker and identified both remaining
bugs as rendering faults, not logic faults.

- **The flyer's `thingClass` was wrong.** `PawnFlyer` **does not override `DrawAt` or
  `DrawPos`** — only its subclass `PawnJumper` does. A raw `PawnFlyer` therefore fell through to
  `Thing.DrawAt`, tried to draw its own graphic, found none (`PawnFlyerBase` has no
  `graphicData`) and rendered the red X for the whole flight. The pawn really was flying; it was
  invisible while doing it, which read as a blink. Now `<thingClass>PawnJumper</thingClass>`.
  The PawnJumper *ThingDef* is DLC-gated, but the *class* is in `Assembly-CSharp`, so this still
  works on the baseline environment with no DLC.
- **Colour was silently discarded.** Vanilla `DustPuffThick` sets
  `<renderInstanced>true</renderInstanced>`, which batches flecks into one draw call and ignores
  per-instance colour — so `FleckMaker.ThrowDustPuffThick(..., Color)` did nothing visible and
  the wave stayed ash-grey. Two own fleck defs added
  (`Dovahkiin_Fleck_ForceWave`, `Dovahkiin_Fleck_FireWave`): `MoteGlow` shader, explicit def
  colour, and **no** `renderInstanced`. Emission now goes through
  `FleckMaker.GetDataStatic` + `instanceColor` + `Map.flecks.CreateFleck`, so per-band alpha
  works and the wave genuinely fades from a bright front to nothing.
- **Wave and fling now share one speed.** `Thing_ShoutWave.CellsPerSecond = 10`, and the wave's
  lifespan is derived from its range rather than hardcoded, so every shout's front travels at
  the same rate and a longer shout simply takes longer to arrive. The PawnFlyer is tuned to the
  same 10 cells/second, so a thrown pawn rides the blast instead of outrunning it.
- New defs added to the startup critical-def check.

**Method note:** two rounds were lost to *silent* failures — a fallback that looked like the bug
it was hiding, and a colour parameter that was accepted and discarded. Both are now either loud
(the flyer logs a warning) or impossible (our own fleck defs). Worth remembering that RimWorld
frequently accepts a parameter and ignores it rather than erroring.

---

## Phase 2a-fix2 — travelling wave, slower fling, longer stun (2026-07-25)

Second round of playtest feedback on the same slice. All three notes were fair; the first two
were the same underlying mistake — **things happening instantly that should take time.**

- **The cone now travels.** Previously every fleck spawned on the same tick, which the user
  described exactly right: *"an instant cone of dust and shockwave just manifest."* New
  `Thing_ShoutWave` emits the cone as an **expanding front over ~0.75s**, with a bright leading
  edge and a two-band fading tail, so the shout is visibly seen leaving the mouth and passing
  through. Colour is per-shout and fades to transparent: cold blue-white for Unrelenting Force
  (it has no element — it is pressure), ember orange for Fire Breath.
  Geometry is pre-bucketed into distance rings on spawn, so each tick costs a list index rather
  than a radial scan. It is a `Thing`, not a `MapComponent`, so it ticks only while a shout is
  actually in flight.
  `FleckMaker.ThrowDustPuffThick` takes a `Color`, which is what made per-shout tinting possible
  without authoring custom fleck art.
- **The fling was real but far too fast.** `PawnFlyer` *was* firing — the def loads fine
  (`PawnFlyerProperties` is exactly `flightDurationMin`/`flightSpeed`/`shadow`, so the XML was
  valid). Speed 22 over seven tiles is a third of a second, which is indistinguishable from a
  teleport. Now **speed 5.5, minimum duration 0.9s** — slow enough to watch, as requested.
  The silent fallback is now a logged warning: an invisible fallback here looks identical to the
  bug it was masking.
- **Stun lengthened.** Level 3 was 2s, judged too short: now **4s** (240 ticks). Level 2 had no
  stun at all and now gets **2s**.
- `Dovahkiin_ShoutFlyer` and `Dovahkiin_ShoutWave` added to the startup critical-def check, so a
  future load failure reports itself instead of silently degrading.

---

## Phase 2a-fix — playtest feedback (2026-07-25)

Phase 2a playtested. Log clean: no XML errors, no exceptions. Thu'um bar present, Unrelenting
Force pushed and stunned as intended, casting time judged right ("stuck true to TES5"). Four
notes, three fixed here.

**Fixed**
- **Knockback now flings rather than teleports.** The victim was being repositioned instantly,
  which read as a glitch instead of a blow — fair criticism. Now uses vanilla `PawnFlyer`, the
  same mechanism behind skip and jump, so they visibly arc through the air and land. New
  `Dovahkiin_ShoutFlyer` ThingDef, fast and flat (speed 22, min duration 0.25) because this is
  being hit by a wall of sound, not leaping. Parented to `PawnFlyerBase`, which is in **Core** —
  only the `PawnJumper` subclass is DLC-gated — so it works on the baseline environment.
  Still stops at the first wall; nobody is ever flung into terrain.
- **Shouts now have visuals and sound.** There were none at all. Cone VFX is driven off the same
  geometry as the damage, so what is seen is exactly what was hit: thick dust for Unrelenting
  Force, fire glow plus smoke for Fire Breath, and a bright pulse at the caster's mouth so the
  origin reads. Thunder and hive-spawn sounds as stand-ins.
- **Thu'um bar is now a gradient**, ember orange at full fading to deep violet when spent, as
  requested. Required overriding `Need.DrawOnGUI`. Bar textures are cached in 24 fixed steps —
  generating one per frame would leak GPU memory, since this redraws whenever the Needs tab is
  open. Also added `UnityEngine.TextRenderingModule` to the csproj; `TextAnchor` lives there and
  any future custom GUI will need it.

**Not fixed — cannot be**
- **Shout icons.** The user wants Skyrim's swirling comet-of-fire motif from the Powers menu,
  per-shout colours, RimWorld execution. **I cannot draw textures.** Specced precisely in
  `ART_TODO.md` for an artist or an image tool; still borrowing vanilla icons meanwhile.

**No change needed**
- Casting time confirmed good; no retune.
- Only one raider appeared, which is raid points on a new colony, not a mod problem.

---

## Phase 2a — The Voice: foundation + three-shout slice (2026-07-25)

`SPEC.md §4.1–4.3, §4.4a, §5.2, §5.4`. Builds clean, 0 warnings. **Awaiting `TESTS/phase2a.md`.**

Per `ROADMAP.md` Phase 2, a vertical slice first: the whole machinery plus **Unrelenting Force,
Fire Breath and Clear Skies** — one knockback, one damage cone, one utility. Between them they
exercise every system the remaining eight shouts need.

**Added — C#**
- `Need_Thuum` (OD-9) — the mod's own shout resource. `Need.MaxLevel` is virtual, which is what
  makes "flat linear growth per soul, forever" expressible at all; a vanilla `Need` is otherwise
  a fixed 0–1 bar. Regenerates on `NeedInterval`, never per tick.
- `WordOfPowerDef` / `ShoutDef` — the knowledge model. `MaxAttainableLevel` implements OD-10:
  words are ordered and level N requires N of them found. `ConfigErrors` rejects a shout that
  does not have exactly three words and three abilities.
- `ShoutUtility` — level raising, the shared cooldown, cost checks, witness thoughts, and
  `SyncAbilities`, which reconciles vanilla's ability tracker against our shout levels.
- `CompAbilityEffect_Shout` — economy comp; owns cost, cooldown, strain and witnesses so the
  effect comps only implement what a shout *does*. Plus knockback, flame-cone and clear-skies
  effects, and shared cone/knockback geometry.
- Strain (`SPEC.md §4.2`) as a real decaying `Dovahkiin_VoiceStrain` hediff, visible to the
  player. The multiplier uses strain accrued *before* the current cast, so the first shout of a
  fight is never penalised.

**Added — XML**
- The Thu'um `NeedDef`, gated behind `Dovahkiin_TheVoice` via `causesNeed` +
  `onlyIfCausedByHediff` — the same proven pattern RimWorld of Magic uses (`COMPAT.md §5`), so
  the bar appears on the Dovahkiin and nobody else with no patching.
- Nine `WordOfPowerDef`s, three `ShoutDef`s, nine `AbilityDef`s (one per shout per level).

**Design notes**
- **One AbilityDef per level**, not one scaling def. `SPEC.md §4.4a` gives the levels genuinely
  different behaviour — Unrelenting Force goes from staggering one target to a knockback cone —
  which is cleaner declared than computed.
- **`CompProperties_AbilityRequiresCapacity` with `Talking` is vanilla**, and is exactly
  §4.3's "a pawn with a destroyed jaw or in a coma cannot shout, and the UI must say why".
  RimWorld greys the gizmo and states the reason for free.
- Ability cooldowns are set to **0** deliberately: recovery is the *shared* Thu'um cooldown
  owned by `Hediff_TheVoice` (§4.2). A per-ability cooldown would fight it.
- `RepairIdentity` now also syncs abilities, so a load can never leave a shout
  learned-but-uncastable or castable-but-unlearned.

**Placeholders** — shout icons borrow vanilla art; no bespoke VFX or audio yet. Logged in the
new `ART_TODO.md`, along with Dragon Aspect's overlay as the largest outstanding art task.

**Still to come in Phase 2:** the other eight shouts, Dragon Aspect's overlay (§4.4d), Storm
Call's outdoor targeting (§4.4e), Soul Tear's dead puppet (§4.4f), and Melee Animation /
RocketMan interaction testing.

---

## Phase 1 — COMPLETE (playtested 2026-07-25)

All seven tests pass. Verified by the user in game and by me from `Player.log` and the save file.

| Test | Result |
|---|---|
| 1. Awaken a colonist — trait, title, both hediffs | pass |
| 2. Second awakening refused, with the reason given | pass |
| 3. Grant souls — *"Zero: +10 soul(s). Unspent 10, attunement 10."* | pass |
| 4. Dragonblood trait grants and shows its stat bonuses | pass |
| 5. Save → quit → load, registry byte-identical | pass |
| 6. Death: deaths=1, slot stays shut, replacement refused (OD-1) | pass |
| 7. Log clean — no XML errors, no exceptions | pass |

Phase 1 took three playtest rounds. Both defects were in the same area — hediff lifecycle — and
neither was visible at build time. The startup def validation and the load-time identity repair
added along the way are permanent and cover the whole mod, not just this phase.

---

## Phase 1b — identity self-repair on load (2026-07-25)

Second playtest: startup log clean (`All critical defs present`, zero `XML error` lines), but
**Grant 10 souls still failed.** Cause was not a def problem — the log shows
`Loading game from file Dovahkiindebug`, a save written *before* the Phase 1a fix. The pawn in
it was awakened while the hediff defs were failing to load, so it carries the trait and title
but no hediffs, and nothing put them back on load.

That is a real defect against `CLAUDE.md` invariant 6 — a Dovahkiin must never exist without
their hediffs regardless of how the save reached that state.

**Added**
- `DovahkiinUtility.RepairIdentity(Pawn)` — idempotent, additive-only. Restores a missing trait,
  either hediff, or the title, and returns what it fixed. Never removes anything, and never
  touches the backstory (the awakening cause is not recoverable after the fact).
- `GameComponent_DragonbornRegistry.FinalizeInit()` — runs the repair on every load and new
  game, and logs a warning naming what it fixed. Normally a silent no-op.
- The Grant-souls debug tool repairs first, so it cannot be blocked by an old pawn.

This makes old saves self-heal rather than requiring a fresh colony, and covers any future
def-loading hiccup the same way.

---

## Phase 1a — hediff fixes after first playtest (2026-07-25)

Playtest reported *"Dovahkiin is missing its hediffs"* on **Grant 10 souls**. Two bugs, the
second hidden behind the first.

1. **`<scenarioCanAddHediff>` is not a field on `HediffDef`** — the real name is
   `scenarioCanAdd`. Removed; it was not needed.
2. **`Hediff.ShouldRemove` is `Severity <= 0f` by default.** Attunement's severity *is* the soul
   count and correctly starts at 0, so it was auto-removed on the first health tick after being
   added. New `Hediff_DragonSoulAttunement` class overrides `ShouldRemove` to false;
   `Hediff_TheVoice` got the same override.

**Correction to the original diagnosis.** Bug 1 was first reported here as the cause — the claim
being that RimWorld discards a whole def on one unrecognised field. **That is wrong**, and the
save file proves it: the pre-fix `Dovahkiindebug.rws` contains a live `Hediff_TheVoice` with
severity 1 and its dictionary intact, written by the very build that logged the XML error. The
field error is real log noise, but **bug 2 alone caused the failure** — the Voice (severity 1)
survived, attunement (severity 0) was deleted a tick after being added. `COMPAT.md §8a` has been
corrected, since it was carrying the wrong rule as guidance.

**Also added**
- `DovahkiinMod.ValidateCriticalDefs()` — logs a loud error at startup naming any required def
  that failed to load, and points at the `XML error` line. This class of bug must never again
  be diagnosed from a runtime message.
- The debug message now names *which* hediff is missing and where to look.

**Verified from the playtest log** (all three `Registry status` dumps):
- Before save: `Zero`, ever existed `True`, deaths `0`, slot closed — **Tests 1 and 2 pass.**
- After save → quit → load: **byte-identical** — **Test 5 passes.**
- After `Kill Dovahkiin`: `<none>`, deaths `1`, slot closed, and a second awakening was refused
  — **Test 6 / OD-1 passes.**
- No exceptions anywhere in the log.

**Method note** (now in `COMPAT.md §8a`): reflection with default binding flags gives false
negatives on RimWorld defs — `TraitDef.commonality` is non-public despite being used by every
vanilla trait. The authoritative XML validator is the game's own load-time check; grep
`Player.log` for `XML error`.

---

## Phase 1 — Identity: registry, trait, backstories, title (2026-07-25)

`SPEC.md §1, §2, §3, §5.4, §10`. Builds clean, 0 warnings.

**Added — C#**
- `GameComponent_DragonbornRegistry` — the single authority. Full `ExposeData` with
  backward-safe defaults on every field, including Alduin's fields so Phase 1 saves stay
  loadable in Phase 4. `TryAwaken` / `NotifyDovahkiinDied` / `IsDovahkiin` / `CurrentDovahkiin`
  are the only public mutators. `CurrentDovahkiin` self-heals if it finds a dead holder rather
  than trusting the death hook.
- OD-1 implemented: `slotReopensAtTick` plus `CanFireAwakeningEvent`, which compares
  `dragonEventFiredCount <= dovahkiinDeaths` — the counter earns its keep here.
- `DovahkiinUtility` — eligibility (humanlike only; **never** filters on race or xenotype, per
  §1), identity apply/strip, Dragonblood inheritance and lockout enumeration.
- `Hediff_TheVoice` — per-pawn shout levels, unspent souls, banked child souls (§3.5), shared
  Thu'um cooldown. Data model and save/load only; nothing casts yet.
- `ThoughtWorker_IsDovahkiin` / `_IsDragonblood` — situational social opinion.
- `HarmonyPatches` — two shallow hooks only: `Pawn.Kill` for death, and
  `ParentRelationUtility.SetMother`/`SetFather` for Dragonblood inheritance.
- `DovahkiinDebugActions` — nine dev tools, including `Registry status` and
  `Kill Dovahkiin (test OD-1)`. Awaken failures report *why*, which is what makes them useful.

**Added — XML**
- Traits (`commonality 0`, never rolled), the two hediffs (Attunement deliberately has **zero**
  stat effects in Phase 1 — §0: "nothing is strong at the moment of awakening"), four
  thoughts, four adulthood backstories.

**Design notes**
- **The title needs no Harmony patch.** `Pawn_StoryTracker.title` is a public settable field, so
  §3.1's custom title is a one-line assignment. Confirmed by reflection before writing any code.
- **Dragonblood hooks parentage, not birth.** `PregnancyUtility.ApplyBirthOutcome` takes ten
  parameters and only runs with Biotech active, which would have made heirs silently
  DLC-locked. `ParentRelationUtility.SetMother`/`SetFather` is two arguments, stable, and fires
  for generated families too. Patched positionally (`__0`/`__1`) so a Ludeon parameter rename
  cannot silently break it.

**Bugs caught before shipping** (verified against `Assembly-CSharp.dll`, not assumed)
- `Pawn_TraitsTracker` does not exist in 1.4 — the type is `TraitSet`.
- `BackstoryDef.skillGains` is a `Dictionary<SkillDef,int>` and needs
  `<li><key/><value/></li>`. The `<SkillName>n</SkillName>` shorthand — which *is* correct for
  `statOffsets`, a `List<StatModifier>` — would have thrown red errors at load.
- Backstories given a dead-end `spawnCategory` rather than an empty list, so they can never be
  rolled onto a generated pawn without risking a config error.

---

## Spec amendment — shout distribution (2026-07-25)

Follow-up clarification on who knows how many shouts.

- **Dragons: exactly one of the three, never two** (`SPEC.md §4.6`). Fixed on the `PawnKindDef`
  by element, not rolled per spawn. Rationale recorded: one shout means one telegraphed pattern
  to read and counter. Noted that this is additive to a dragon's existing kit — Dragon's Descent
  dragons keep their native breath abilities, so a Fus Ro Dah dragon is not left toothless.
- **Draugr: 0–2 shouts, chance-scaled by tier** (`SPEC.md §4.5`). The pool is **Unrelenting
  Force + Frost Breath only** — draugr never breathe fire — so "two" means both, and two is the
  hard ceiling.

  | Tier | Shouts | Roll |
  |---|---|---|
  | Draugr | 0, never | — |
  | Draugr Wight | 0–1 | 20% |
  | Draugr Overlord | 1, rarely 2 | 90% / 15% |
  | Draugr Deathlord | 1–2 | 100% / 50% |

  Rolled **once at pawn generation and stored** — never re-rolled on load, so a crypt cannot be
  save-scummed into being easier.
- **New creature: Draugr Wight** (`SPEC.md §12`), the first tier that can speak. Cheap — a
  retint and kit swap on the base draugr, not new art.
- `DovahkiinTuningDef` gained the draugr ladder chances and `undeadMaxShoutsKnown` (2). Rebuilt
  clean.

---

## Spec amendment — shout details (2026-07-25)

User clarifications after the OD-10 trim. Storm Call, Dragon Aspect and dragon shouts were
already in scope and gained detail; Soul Tear is a genuine addition.

- **Storm Call** (`SPEC.md §4.4e`) — targeting narrowed to hostile pawns **outdoors, in
  unroofed cells**. This also closes the old ignition question: strikes cannot land under a
  roof, so they cannot burn a base. The three fallback resolutions are obsolete and were removed.
- **Dragon Aspect** (`SPEC.md §4.4d`) — the one-line "must have a strong visual" is now a full
  TES5-accurate spec: spectral bronze-gold plating, silhouette-breaking shoulder spurs, ember
  rim-light and motes, cast ring, L3 melee shockwave. Failure to deliver the overlay is now a
  stop-and-report, not a silent downgrade to a stat buff.
- **Soul Tear** (`SPEC.md §4.4f`) — **promoted back out of the deferred list.** Single target,
  heavy impact, level-scaled chance to raise a **dead puppet**: fights for the colony, then
  dies. Full exit-path and save/load rules written, because this is the mod's highest
  save-corruption risk (`RISKS.md §9`).
- **Dragons shout** (`SPEC.md §4.6`) — now a hard requirement with exactly **three** shouts for
  normal dragons (Fire Breath, Frost Breath, Unrelenting Force), reusing the pawn assets
  unchanged and differing only in area, range and intensity. Implemented as scalars on the same
  `ShoutDef`, never duplicate defs. Alduin is exempt and keeps the full kit.
- **Budget moved:** eleven core shouts, **33 word walls** (was ten / 30). Maxing everything now
  costs 33 souls.
- `DovahkiinTuningDef` gained Storm Call, Soul Tear and dragon-scaling numbers. Rebuilt clean.

> Flagged assumption: the brief listed dragons' three shouts as "Frost, Unrelenting Force, and
> frost". Read as **Fire, Frost, Unrelenting Force** (the TES5 kit). One-line change if wrong.

---

## Phase 0 — Scaffold (2026-07-25)

First build. Nothing is playable yet; this phase exists to prove the pipeline.

**Added**
- `About/About.xml` — `erzou.dovahkiin`, 1.4 only, hard dependencies on Harmony and HugsLib,
  load order per `COMPAT.md §9` (after Rimedieval, before RocketMan).
- `Source/Dovahkiin/Dovahkiin.csproj` — net472, C# 7.3, output to `Assemblies/`.
  Includes `Microsoft.NETFramework.ReferenceAssemblies` because this machine has the .NET 8 SDK
  but no Visual Studio targeting packs; without it `dotnet build` cannot target net472 at all.
- `Source/Dovahkiin/RimWorldPath.props` — machine-specific game path, isolated to one line.
- `DovahkiinMod` — HugsLib `ModBase` entry point. Creates the mod's single Harmony instance
  (`erzou.dovahkiin`), registers the settings handle, logs a load confirmation.
- `DovahkiinTuningDef` + `Defs/MiscDefs/DovahkiinTuningDef.xml` — the one def holding every
  balance number, pre-populated for Phases 1–3. Cached static accessor, no repeated
  `DefDatabase` lookups.
- `Languages/English/Keyed/Dovahkiin.xml` — no hardcoded user-facing strings.
- `TESTS/phase0.md` — verification steps.

**Build result:** clean. 0 errors, 0 warnings. Output `Assemblies/Dovahkiin.dll` (6.5 KB), and
only that file — no game or library DLLs copied alongside it.

**In-game load test: PASSED** (2026-07-25, verified from `Player.log`).

- `[HugsLib] initializing ARimWorldOfMagic, Dovahkiin`
- `[Dovahkiin] Loaded. Tuning def OK (heir awaken chance 2.0 %, Thu'um per soul 2). Phase 0.`
- **Zero errors or exceptions mentioning Dovahkiin.** The 21 errors in the log are all
  pre-existing and belong to other mods: d3d11 texture-creation failures, and Melee Animation
  failing to reach `hjpwdfmbh9.execute-api.eu-west-2.amazonaws.com` for its missing-weapon
  telemetry.
- Load order confirmed against `ModsConfig.xml`: 40 active mods, Dovahkiin at **39** — after
  Rimedieval (14) and Dragon's Descent (38), before RocketMan (40).

**Fixed during verification:** `About.xml` declared `loadAfter` on `Kikohi.Rimedieval`, which is
not a real packageId — the real one is **`ogam.rimedieval`**. RimWorld silently ignores unknown
ids, so the correct load order today came from auto-sort, not from our declaration. All thirteen
declared ids are now validated against the live `ModsConfig.xml` and recorded in `COMPAT.md §8b`.

---

## Step 1 — Reconnaissance (2026-07-25)

No mod code. Read the real game and mod files on disk and recorded the results.

**Added** `COMPAT.md`, `RISKS.md`, `DECISIONS.md`.

**Findings that changed the plan**
- Nordic crypts stop being the project's biggest engineering risk. VEF ships **KCSG**, with an
  in-game structure exporter, and Dragon's Descent already uses it in 1.4 to build large
  authored lairs. Crypts become authoring work, not a bespoke generator.
- The shout backbone is settled: **vanilla `AbilityDef`**, no hard dependency. Dragon's Descent
  proves it works on animal pawns. JecsTools `AbilityUser` is not needed.
- The scenario's hostile-settlement start is much cheaper than costed — `ScenPart` exposes
  `GenerateIntoMap(Map)`.
- `SPEC.md §5.2`'s original "+2 max mana" was not implementable; RWoM mana is a 0–1 `Need`
  gated behind class hediffs. RWoM's `<enchantments><maxMP>` mechanism is the supported path.

**Corrected before this** — 13 defects found in the prompt bundle itself, including a
non-existent API (`GenStep_PlayerStart`), the Biotech baseline contradiction, wrong sanguophage
quest identities, and Rimedieval's filtering being attributed to XML patches when it is C#.

**Decisions taken** — all ten open decisions answered; see `DECISIONS.md`. The three that
needed the user: own Thu'um resource (OD-9), Skyrim-faithful word gating with the shout list
trimmed to ten (OD-10), and the Dovahkiin slot reopening after a delay (OD-1).
