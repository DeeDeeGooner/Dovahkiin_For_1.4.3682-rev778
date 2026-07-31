# Phase 2j test — the Ancient Dragonborn summon

**The last piece of Dragon Aspect.** A shard of the Dovahkiin's soul that arrives when you are
losing, fights for you, and vanishes.

**Restart RimWorld** — the assembly, the defs and a new texture all changed.
**Play at normal speed.**

He is a **rescue, not a guarantee**. Casting Dragon Aspect at full health summons nobody, and
that is correct.

He arrives when, at **three words**, either:
- you cast the shout while already at **65% health or below**, or
- your health **drops to 65% or below** while the shout is running, or
- you are **downed** while the shout is running.

At most **one per casting**, and he lasts **6 in-game hours** (15,000 ticks).

> **Corrected 2026-07-31.** This line said 1.5 hours / 3,750 ticks, which was true when it was
> written and is not any more. The lifetime now lives in
> `Defs/MiscDefs/DovahkiinTuningDef.xml` as `ancientDragonbornLifetimeTicks` and can be changed
> without a rebuild — so **read it there rather than trusting this number**. A stale figure in a
> document that presents itself as correct is worse than no figure: it invites someone to "fix"
> code that is already right.

About twenty minutes. **Test 5 is the one that matters most** — it is the save-safety check,
and it is the reason this feature was built carefully rather than quickly.

---

## Setup

1. Debug mode on. **Dovahkiin → Learn all words** → ✅ still **42** words.
2. Raise **Dragon Aspect** to **level 3**.
3. **Dovahkiin → Refill Thu'um / clear cooldown** between casts — it is once per day.

---

## Test 1 — He does NOT come when you are healthy

1. With the Dovahkiin at full health, cast **mul qah diiv**.
2. ✅ The armour appears as usual.
3. ✅ **Nobody is summoned.** No second pawn, no message.

If someone appears here, the rescue condition is inverted and I want to know immediately.

---

## Test 2 — He comes when you are hurt at cast time

1. Hurt the Dovahkiin below 65% health — dev mode **Damage** tool, or a real fight.
   - Check the health tab; you are looking at overall health, not one body part.
2. Cast **mul qah diiv**.
3. ✅ **A blue puff, a sound, and the Ancient Dragonborn appears right beside you** — within a
   cell or two, never across the map.
4. ✅ He looks like **the level-3 spectral armour walking on its own** — the pawn inside is
   invisible, so you should see plates, fins, helm and aura, and no skin or clothing.
5. ✅ He carries a **blue-and-ember two-handed axe**.

---

## Test 3 — He comes when you are hurt *during* the shout, and when downed

1. Cast **mul qah diiv** at **full health** → nobody appears (Test 1).
2. Now take damage until you drop **to or below 65%** while the shout is still running.
   ✅ He appears at that moment.
3. Clear the cooldown, cast again at full health, then let the Dovahkiin be **downed**.
   ✅ He appears.
4. ✅ In both cases **only one** of him. Staying below 65% must not keep summoning more.

Point 4 is the one I would most expect to go wrong. If you ever see two, tell me.

---

## Test 4 — The three shouts, and the friendly-fire rule

**Changed 2026-07-30:** he no longer has one element for life. He knows **Fire Breath, Frost
Breath and Unrelenting Force**, and cycles through all three.

1. Watch him in a long fight. ✅ After a few seconds he shouts at a hostile.
2. ✅ Over three casts you should see **all three**, not the same one repeatedly:
   - **fire** — orange cone, sets pawns alight
   - **frost** — pale blue cone, chills and briefly stuns, leaves snow
   - **force** — blue-white cone, a thunder crack, enemies **knocked backwards** and stunned
3. ✅ Two different summons should not always open with the same shout.
4. **THE IMPORTANT ONE:** manoeuvre a colonist so they stand **between him and an enemy**.
   ✅ He does **not** shout — *any* of the three. The safety rule covers all of them, not just
   the breaths.
5. ✅ Move your colonist clear → he shouts again once the line is clear.
6. ✅ Fire breath may set **pawns** alight but should **not** set the ground or your base on fire.
7. Force knocks enemies **away from him**. ✅ Say if that pulls them out of the Dovahkiin's reach
   annoyingly — the knockback distance is one tunable number (currently 3 cells).

If he ever breathes through one of your own, stop and tell me — that is a hard rule, not a
tuning preference.

---

## Test 4d — He joins the Dovahkiin's fight  ← NEW, fixed after round 4

He used to ignore anything that was not *hostile* — and a wild animal is hostile to nobody, so
sending the Dovahkiin at a boar left him standing there. He now joins in.

1. Summon him, then order the Dovahkiin to attack a **wild animal** (a boar, a deer, anything
   peaceful). ✅ He goes for it too, in melee.
2. ✅ He will also **breathe** on it once his cooldown allows — that was blocked before as well.
3. ✅ **He still will not breathe through one of your own.** Put a colonist between him and the
   animal; he should wait. That rule is not allowed to bend for this.
4. **THE IMPORTANT NEGATIVE:** order the Dovahkiin to attack **one of your own colonists** or a
   **tamed animal**. ✅ He must **NOT** join in. Player-faction targets are excluded deliberately.
5. ✅ He does not chase a hunt across the map. Beyond about **24 cells** he loses interest and
   comes back to the Dovahkiin.
6. ✅ If he is already fighting a raider, that fight stays his — he does not abandon it to go and
   help with a rabbit.

If he stands and watches again, tell me **what the Dovahkiin was doing** (drafted attack, hunting
designation, or an auto-attack), because those are three different code paths.

---

## Test 4c — The weapon's new shape and how he carries it  ← NEW

The weapon is now a **dragonbone battleaxe** rather than a halberd: curved haft, ring pommel at
the butt, wrapped grip with two bands, a riveted collar, two spikes on the far side from the
blade, and the blade shape traced from your own painted drawing. Colours are unchanged.

1. ✅ It looks like the preview — one clean blade, no hole, two spikes, ring pommel.
2. **He should carry it head UP, blade over his shoulder** — not head-down with the pommel in
   the air. The old angle did the latter and nobody spotted it while the weapon was a
   symmetrical halberd.
3. **Check all FOUR facings** — south, north, east and west. South and east were previewed and
   approved; **north and west were not**, they are set by the same arithmetic and are guesswork.
   If a facing looks wrong, tell me *which one* and whether the head points the wrong way or
   the whole weapon sits in the wrong place.
4. ✅ The swing itself should be unchanged — it is still Medieval Overhaul's halberd animation,
   which you already confirmed as correct.
5. It is about **8.5% shorter** than the old halberd looked. That is expected, not a bug — say
   if it now reads as too small.

---

## Test 4b — ONE halberd, never two  ← fixed after round 3, please re-check

Round 3 found **two** halberds on him mid-fight: the animated one plus a static one. The cause was
that we drew it ourselves all the time, not realising the game starts drawing it too the moment he
picks up an attack job.

1. Watch him **idle / following you**. ✅ Exactly **one** halberd, held at his side.
2. Send him into a melee fight and watch him swing. ✅ Still exactly **one** — the swinging one.
3. ✅ At the moment he stops fighting and walks back, still **one**. There may be a small pop as
   it changes hands between the game's drawing and ours; a *second* axe appearing is the bug.
4. ✅ The swing animation itself is unchanged — it should still look like Medieval Overhaul's
   halberd, which you already confirmed as correct.
5. **The one that survived round 3 — watch the moment a swing FINISHES**, especially facing
   **north**. The melee cooldown lasts about a second longer than the attack itself, and that was
   the window where the second axe reappeared. ✅ Still one throughout.
6. ✅ Check the log once at load for a line reading `Melee Animation detected` — it should say the
   animator lookup **resolved**. If it says "NOT resolved", tell me: the fix falls back to a
   narrower check and the doubling may survive in animations.

If you ever see two again, tell me **which state he was in** — idle, mid-swing, just-finished a
swing, or walking back. Those are now four different code paths and that detail says which.

---

## Test 5 — He leaves nothing behind  ← THE IMPORTANT TEST

This is the save-safety check. Temporary pawns are the top save-corruption risk in this mod.

1. Summon him, then **wait out the full 1.5 hours**.
   ✅ He fades with a blue puff.
   ✅ **No corpse.** Nothing to haul, butcher or bury.
   ✅ **No axe on the ground.** It goes with him.
2. Summon him again and **kill him** in a fight instead.
   ✅ Same result — vanishes, no corpse, no dropped axe.
3. Summon him, and while he is alive **save → quit to menu → reload**.
   ✅ He is still there, still counting down, still following you.
   ✅ He expires normally afterwards and leaves nothing.
4. ✅ Check the colonist bar. He should **never** appear in it, and you should **not** be able
   to draft him or give him work.
5. ✅ Dev-mode log has **no red errors** — in particular nothing mentioning `SAFETY SWEEP`.

**If you ever see a `SAFETY SWEEP` line in the log, copy it to me verbatim.** It means a summon
survived in a state that is supposed to be impossible, and the message says which.

---

## Test 5b — The fallen Dovahkiin's echo

Once a Dovahkiin has **died**, every Ancient Dragonborn summoned afterwards wears **that
Dovahkiin's face** — body type, head, hair, hair colour and skin. The ally who comes to save
you is the ghost of the one who came before.

Before the first Dovahkiin dies there is no echo, and summons look like nobody in particular.
That is correct, not a bug — there is nobody to echo yet.

1. Note what your current Dovahkiin looks like — hair, hair colour, skin, build.
2. **Dovahkiin → Kill Dovahkiin (test OD-1)**.
3. Awaken a new Dovahkiin (**Force awaken pawn**), give them **Dragon Aspect at level 3**, hurt
   them below 65%, and cast.
4. ✅ The summon now has the **dead** Dovahkiin's hair, colour, skin and build.
   - He is semi-transparent, so look closely. Hair shape and colour are usually the clearest.
5. ✅ His **name is still "Ancient Dragonborn"** — never the dead colonist's name.
6. ✅ Nobody in the colony reacts to him. No mood, no recognition, no grief.
7. Kill a second Dovahkiin, summon again.
   ✅ He now wears the **second** one's face. The echo is always the most recent.

Point 5 and 6 matter: he inherits a **face**, not an identity. If the colony ever treats him
as the dead colonist, tell me — that would mean something copied across that should not have.

---

## Test 6 — Nothing else broke

1. Cast a few other shouts — Fire Breath, Unrelenting Force, Soul Tear. ✅ All as before.
2. ✅ Dragon Aspect's armour, resistances, melee bonus and cooldown are unchanged.
3. ✅ Load an **older save** made before this update. ✅ No errors, no missing-def warnings.

---

## What to tell me

- Anything with a **red error** — that first, and `SAFETY SWEEP` above all.
- Whether he **arrives at the right moment** — too easily, too rarely, or about right.
- Whether **1.5 hours** feels right, and whether his axe and breath feel too strong or too weak.
- Whether the **invisible-pawn-in-armour** look reads the way you pictured it.

Every number here is in `DovahkiinTuningDef.xml` — the health threshold, his lifetime, breath
range, damage and cooldown — so retuning any of it needs no rebuild.
