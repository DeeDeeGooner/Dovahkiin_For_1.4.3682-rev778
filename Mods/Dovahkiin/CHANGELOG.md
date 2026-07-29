# CHANGELOG

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
