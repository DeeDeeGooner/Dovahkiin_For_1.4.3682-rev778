# Phase 2g test — Storm Call

**12 of 14 core shouts.** The first of the three hard ones.

Storm Call gathers a storm over the Dovahkiin and hunts enemies with lightning — but **only
those standing under open sky**. Anyone under a roof of any kind is untouched, and that is
deliberate, not a limitation.

**Restart RimWorld** (the assembly changed). **Play at normal speed.**

About ten minutes.

---

## Setup

1. Debug mode on, load or start a colony with the Dovahkiin.
2. **Dovahkiin → Learn all words** → ✅ should now report **36** words.
   - If it says 33, the Storm Call files did not load — stop and tell me.
3. **Raise a shout one level** → ✅ the list now shows **12** shouts, including **Storm Call**.
4. Raise **Storm Call** to level 1. It is expensive — 10 thu'um at one word — so refill first.

---

## Test 1 — It works outdoors

1. Spawn raiders **out in the open**, well away from any building.
2. Select the Dovahkiin, click **strun**.
   - ✅ Fires immediately, no targeting cursor. The storm gathers over the Dovahkiin.
   - ✅ **Lightning bolts fall on the raiders** over a couple of seconds — 3 at level 1.
   - ✅ Bolts land **on enemies**, not on random ground.
   - ✅ They take damage, and fires may start on the ground around them. That is intended
     outdoors.

## Test 2 — It does NOTHING under a roof

**This is the important test.** The whole design rests on it.

1. Put raiders **inside a roofed building** — a proper room with a roof, not just next to a wall.
   - Easiest: debug → spawn raiders, then let them path into your base, or build a quick roofed
     box and spawn them inside.
2. Cast **strun** with only those roofed raiders in range.
   - ✅ **No lightning at all.** Not one bolt.
   - ✅ A message appears: *"…every enemy in reach stands beneath a roof."*
   - **The message now names which rule rejected them**, which it did not before. You saw a
     "no targets" message once while enemies were plainly outdoors — that was almost certainly
     the range rule, and the old message could not tell you so. There are three now:
     | You see | It means |
     |---|---|
     | *"…stands beneath a roof"* | The outdoor rule. Working as designed |
     | *"…too far off for it to reach"* | They were outside the radius. Move closer |
     | *"…finds nothing to strike"* | No hostile pawns on the map at all |
3. Now have one raider **step outside** into the open and cast again.
   - ✅ Lightning hits **only** the one outdoors. The roofed ones stay untouched.

**If a bolt ever lands under a roof, stop and tell me.** That is a real bug, not a balance
issue — it would mean the shout can set fire to the inside of your base.

## Test 3 — It never hits your own people

1. Stand **colonists and tamed animals** right next to enemies, all outdoors.
2. Cast **strun** several times.
   - ✅ Lightning hits **only the enemies**. Never a colonist, never a tamed animal.
3. If you have a **neutral visitor or trader caravan** on the map, check them too.
   - ✅ Never struck. Only pawns actually hostile to you are valid.

## Test 4 — More words, bigger storm

1. Raise Storm Call to **level 3** (two more souls) and refill thu'um.
2. Cast **strun bah qo** on a large group outdoors.
   - ✅ Far more bolts — **12**, over a longer storm (about 15 seconds).
   - ✅ It should feel like the most powerful shout you have. It costs 24 thu'um and has the
     longest cooldown in the mod.

## Test 5 — Save mid-storm

1. Cast **strun bah qo**, and **while lightning is still falling**: save, quit to menu, reload.
   - ✅ No error on load. The storm either continues or has cleanly ended.
   - ✅ Nothing is left permanently striking.

## Test 6 — Log

Tell me when you are done and I will read it.

---

## Notes on what I decided

**The storm centres on the Dovahkiin, not on a spot you click.** In TES5 Storm Call is a storm
gathering over the Dragonborn, not artillery placed on a target, so the ability takes no target
at all.

**The storm now FOLLOWS you.** Range is measured from wherever the Dovahkiin is at that instant,
not from the cell you cast in — so walking during a storm no longer silently pulls enemies out
of reach. That was a real bug and it is the most likely explanation for the "no targets" you saw
while enemies were outdoors.

**Range raised 25 → 38 tiles**, as asked (+50%).

**Targets are re-checked for every single bolt**, not chosen once when you cast. Pawns move,
die, and duck under roofs mid-storm — a list captured at cast time would keep striking corpses
and people who have since taken cover.

**A strike is not "spent" when it finds nobody.** If everyone is under a roof at that instant,
the storm simply holds its bolt and tries again a moment later. Step into the open during the
storm and you will still be hit.

**Numbers to retune if it feels wrong** — all in `Defs/MiscDefs/DovahkiinTuningDef.xml`, no
rebuild needed:
`stormCallStrikesByLevel` (3/6/12), `stormCallDurationTicksByLevel` (180/420/900 ticks) and
`stormCallRadius` (25). Note strikes are spread evenly across the duration, so raising the
duration alone makes the storm *slower*, not heavier — raise both together.
