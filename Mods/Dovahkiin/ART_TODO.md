# ART_TODO.md

Every placeholder currently in the mod. `CLAUDE.md` forbids shipping placeholder art silently,
so nothing gets removed from this list without being replaced.

Art direction is `SPEC.md §12`: **silhouettes and palettes read as Skyrim, execution reads as
RimWorld.** Flat, top-down, painterly, restrained saturation, readable at colony zoom, sitting
comfortably next to Medieval Overhaul.

> **Want to actually make these?** This file is the *record* of what is missing and why, mixed
> with engineering notes. The copy-paste prompts for an image tool are in **`ART_PROMPTS.md`**
> next to it. Use that one; don't paste this file into an image model.

---

## Phase 2 — shout icons — **DONE (generated), replaceable later**

**Status changed 2026-07-28.** All 15 icons now ship with the mod, in
`Textures/UI/Abilities/Dovahkiin_Shout_*.png`, 256×256 with real alpha. **No shout borrows
vanilla art any more, and the Biotech icon defect below is CLOSED.**

They are **generated, not drawn**. Two PowerShell scripts in `Tools/` are the source:

| File | Does |
|---|---|
| `Tools/GenerateIconMaster.ps1` | Draws the white master comet — solid body, dark rim, hot core. Output `Tools/icon_master.png` |
| `Tools/GenerateShoutIcons.ps1` | Recolours the master into all 15 icons from a table |

**To retune one shout, edit one row of the table in `GenerateShoutIcons.ps1` and re-run it.**
Nothing else changes — the defs point at fixed filenames. The scripts are deterministic, so the
same input always gives the same output.

### The three levers that distinguish one shout from another

Set by the user during review, and the reason the family works:

1. **Body colour** — the comet itself
2. **Core colour** — the bright circle inside the head, tinted **independently** of the body
3. **Opacity** — for shouts that should read as faint or translucent

Lever 2 is the subtle one. Slow Time is a pale grey-white comet with a **blue** core; before
the core was tintable it always blew out to white and Slow Time was indistinguishable from
Whirlwind Sprint. Become Ethereal uses lever 3 at 0.72, Cyclone at 0.60.

### Still worth replacing, when there is art

Held against A RimWorld of Magic's icons — which is the fair comparison, since they sit on the
same command bar — ours are **cleaner but plainer**. Theirs have internal detail: individual
cloud curls, separate flame tongues, layered highlights. Ours is one smooth shape with one
highlight. That gap is the difference between generated and drawn, and more generation passes
would refine it rather than close it.

For scale, RWoM ships **1,406 PNGs / 50 MB**, built up by its author across RimWorld 1.0–1.4 —
413 ability icons, each drawn individually and pictorially (a cloud, a cross). That is the
method, and it is not one we can imitate. **But it is also not what shouts want:** they are 15
variants of one concept, and Skyrim itself gives its shouts a shared motif rather than 15
different pictures. A recoloured family is the right answer here, not a compromise.

If better art does arrive, it drops straight in: replace the PNGs at the same paths, or replace
`icon_master.png` and re-run the recolour script. **No def or code change is needed.**

### Original spec, kept for whoever draws the replacements

### The look the user asked for

**CORRECTED 2026-07-26, by the user, against the actual game.** This section previously
described the motif as a *"swirling comet of fire — a bright dense head with a long curved tail"*.
That was wrong. The user checked TES5's Magic menu and the shout icons are a **wispy rotating
swirl**: a soft dense core with feathery strands spiralling outward and breaking up at the
edges, like wind or breath caught turning. No comet head, no single long tail. Near-monochrome
pale grey-white on a dark field.

The wrong description was acted on once, and the image tool produced a comet when a swirl was
wanted. Do not reintroduce it.

**For this mod:** keep that swirl silhouette identically across every shout so they read as one
family at a glance, and change **only the colour** per shout. Execute it in RimWorld's style:
flat, slightly painterly, restrained saturation, readable at small size, no bevels or outer glow.

**One master image serves everything.** The same swirl is used for the Cyclone effect sprite
(256×256, in `Textures/Things/Mote/`) and, recoloured, for all eleven shout buttons
(128×128, in `Textures/UI/Abilities/`). The recolouring is done programmatically - luminance
preserved, hue replaced - so the eleven are exactly consistent by construction rather than by
an artist's hand.

**Risk to watch in play:** the swirl is fine and wispy, and RimWorld's ability buttons are
small. Skyrim shows its icons large in a menu; if the fine strands turn to mush at button size,
the fix is more contrast and thicker strands in the master, not a different shape.

**FIFTEEN icons are needed in the end, not eleven.** Corrected 2026-07-26 — the earlier table
listed only what was built at the time. The full set is the **14 core shouts** (the original 11
from `SPEC.md §4.4a`, plus Drain Vitality, Dismay and Cyclone promoted/added at the user's
request) **plus Dragonrend**, which is quest-locked via `§9.3` and sits outside the core list but
still needs a button.

| # | Shout | Colour | Status | Currently borrowing | Ships with |
|---|---|---|---|---|---|
| 1 | Unrelenting Force | Cold pale grey-white, faint blue edge. No elemental tint — this one is *force*. | built | `UI/Abilities/BerserkPulse` | Royalty |
| 2 | Fire Breath | Ember orange into deep red at the tail. | built | `UI/Abilities/FireSpew` | **Biotech** ⚠ |
| 3 | Frost Breath | Ice blue into white. | built | `UI/Abilities/AcidSpray` | **Biotech** ⚠ |
| 4 | Whirlwind Sprint | Pale white-grey, motion-blurred edge. | built | `UI/Abilities/Longjump` | **Biotech** ⚠ |
| 5 | Marked for Death | Blue-grey leaning grey — cold and dead, no violet. | built | `UI/Abilities/Burden` | Royalty |
| 6 | Clear Skies | Pale sky-blue into soft white. The calm one. | built | `UI/Abilities/SolarPinhole` | Royalty |
| 7 | Slow Time | Warm sand-gold, strands longer and thinner than the rest. | built | `UI/Abilities/Focus` | Royalty |
| 8 | Become Ethereal | Translucent blue-white, core faded rather than bright. | built | `UI/Abilities/Invisibility` | Royalty |
| 9 | Drain Vitality | Deep dark violet, darker and more purple than Marked for Death. | built | `UI/Abilities/NauseaPulse` | Royalty |
| 10 | Dismay | Red, strands longer and more scattered than the rest. | built | `UI/Abilities/BlindingPulse` | Royalty |
| 11 | Cyclone | Faint translucent grey, swirl tighter than the family default. | built | `UI/Abilities/SkipChaos` | Royalty |
| 12 | Storm Call | Violet into charcoal, sparks brighter than the core. | **not built** | — | — |
| 13 | Soul Tear | Deep blood-crimson bleeding into black. The most powerful shout in the mod. | **not built** | — | — |
| 14 | Dragon Aspect | Spectral bronze-gold, matching its armour overlay (`SPEC.md §4.4d`). | **not built** | — | — |
| 15 | Dragonrend | Cold bone-white with a hard iron-grey edge. The only shout not learned from a wall. | **not built** (Phase 6) | — | — |

Icons 12–15 can be generated **now** alongside the rest — they are only recolours of the same
master, and having them ready costs nothing. They simply will not be referenced by any
`AbilityDef` until those shouts are built.

### ✅ CLOSED — the Biotech icon defect is fixed

Kept for the record. Fire Breath, Frost Breath and Whirlwind Sprint used to borrow
`UI/Abilities/FireSpew`, `AcidSpray` and `Longjump`, **all shipped by Biotech**, so on a legal
baseline install with Biotech disabled those three showed the missing-texture square. Shipping
our own icons removed the dependency entirely — verified by grepping every `<iconPath>` in the
mod: 23 of 23 now resolve to files inside `Textures/`, and none point at vanilla art.

<details>
<summary>Original entry</summary>

### ⚠ Three borrowed icons are Biotech-only — a real baseline defect

`CLAUDE.md` sets the baseline environment as **Core + Royalty + Ideology**, with Biotech
present but **optional at runtime**, and `ROADMAP.md` universal exit criterion 5 requires the
mod to run without it. But `UI/Abilities/FireSpew`, `AcidSpray` and `Longjump` are all shipped
**by Biotech**, verified by grepping every `<iconPath>` in `Data\`. On a legal baseline install
with Biotech disabled, Fire Breath, Frost Breath and Whirlwind Sprint therefore show RimWorld's
missing-texture square instead of an icon.

It is cosmetic, not a crash, and it does not affect the user's own install (they have Biotech).
**Not fixed unilaterally** because swapping them changes the look of three shouts already
signed off in playtest. Two ways out, user's choice:

1. Swap to Royalty equivalents now — e.g. Fire Breath → `UI/Abilities/Flashstorm`, Frost Breath
   → `UI/Abilities/BlindingPulse`, Whirlwind Sprint → `UI/Abilities/Skip`. Free, immediate,
   changes their appearance for everyone.
2. Leave them until the bespoke icons above land, which removes the problem permanently.

Either way this must be closed before Phase 2 is declared finished. New shouts from batch two
onward use **Royalty** placeholders only, so the list does not grow.

</details>

**Technical:** 128×128 PNG, transparent background, drop into
`Textures/UI/Abilities/`, then change `iconPath` in `Defs/AbilityDefs/Abilities_Shouts.xml`
from `UI/Abilities/…` to `UI/Abilities/Dovahkiin_<Name>`. Nothing else needs touching — the
three levels of a shout deliberately share one icon, since they are the same shout.

### Cyclone — the funnel wants a real swirl texture

**This one is genuinely limited by art, and the current version is an approximation.**

Cyclone should look like a tornado travelling across the ground. It is currently drawn as three
concentric orbits of vanilla dust particles, each individually rotated, spinning at different
rates so the shape reads as a funnel with a taper. That is the best structure obtainable from
scattered particles — the first attempt filled the whole disc with dust and playtest correctly
reported "there is no vortex", because a filled disc has no structure to read as rotation.

**The right fix is one purpose-drawn spinning sprite**, which is exactly how RimWorld of Magic
does it: `Mote_ManaVortex`, a single texture (`UI/manavortex_trans`) rotated in place. That
reads as a vortex instantly and costs a fraction of the particles.

Blocked because: I cannot draw it, we cannot use another mod's asset, and **Core's own textures
are packed into Unity bundles** (`Data\Core` has no `Textures` folder at all), so there is no
vanilla swirl texture to point a `FleckDef` at.

**Spec for whoever draws it:** 128×128 PNG, transparent background, a top-down funnel — a
tight bright core with two or three trailing arms spiralling outward anticlockwise, greyscale
so the shout's tint does the colouring. Drop in `Textures/Things/Mote/`, add a `FleckDef` using
it, and point Cyclone's `fleckDef` at it; `Thing_ShoutWave.DrawFunnel` can then emit one large
rotated fleck at the column centre instead of twelve small ones.

### Whirlwind Sprint — ground-hugging dash (deferred, not blocked on art)

Requested: the pawn should **skim the ground** during the dash rather than arc through the air
like a jump, at the same speed as its trail wave.

The arc comes from `PawnJumper.DrawPos`, which *is* virtual and could be overridden by a custom
flyer class with no vertical offset. The obstacle is that the flyer def is selected inside
vanilla's own jump path, so swapping it needs a Harmony patch on the exact movement code that
destroyed a colonist in Phase 2b.

**Deferred on purpose.** Not worth patching that path again for a cosmetic change while there
are still shouts to build. Revisit in Phase 8 polish, when it can be tested in isolation.
The trail VFX is already in and does not depend on this.

**Also outstanding for Phase 2**, and not yet started:

- **Shout VFX.** Right now shouts have vanilla warmup and no bespoke effect. `SPEC.md §4.3`
  wants a wind-up, a dragon-tongue voice line, and a directional shockwave/cone in the facing
  direction. The cone geometry already exists in code (`ShoutTargeting.CellsInCone`), so the
  effecter can be driven off the same shape.
- **Dragon-tongue audio.** No sound is attached to any shout yet. `SPEC.md §8` (Phase 8) wants a
  distinct voice line per shout.
- **Dragon Aspect overlay** — `SPEC.md §4.4d`. Not yet built and the largest single art task in
  Phase 2. Spectral bronze-gold plating, silhouette-breaking shoulder spurs, ember rim-light,
  drifting motes. Failure to deliver the overlay is a stop-and-report, not a silent downgrade.

## Audio already recorded but NOT yet wired

**⚠ An unreferenced audio file is invisible — nothing in RimWorld reports one.** That cost six
days with `DragonLanding.mp3`, which sat in `Sounds/` unused from 2026-08-06 to 2026-08-12.
Anything recorded ahead of the feature it belongs to goes here until it is wired.

| clip | length | for | state |
|---|---|---|---|
| `DungeonBackgroundNoise.mp3` | 15.408s | **Nordic crypts ONLY** (`SPEC.md §7.3`) — the user's own recording, 2026-08-13 | **Waiting on Phase 5.** ⚠ Plays **ONCE on FIRST ENTRY** — *not* a loop, *not* an ambience. ⚠ **Not** for dragon mounds (§7.1) or burial sites (§7.2). "First entry" is saved per-site state, so leaving and returning must not replay it. |

Wired and working: `DragonBreathFire`, `DragonBreathFrost`, `DragonLanding`, `DragonTakeOff`,
`DragonFlightCircling1`, `DragonFlightCircling2`.

## Not yet reached

Phases 3–5 creatures (Alduin, the fallback dragon, draugr and the wight tier, deathlord,
overlord, ghosts, frostbite spiders, skeevers, dragon priest and its mask), word walls, dragon
mounds, burial sites and crypt interiors. All listed in `SPEC.md §12` and `§7`.
