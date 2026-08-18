# TEST — Alduin, the test dragon (art and rendering only)

**What this is testing:** twelve sprites and the three-state graphic swap, in the running game.
**What it is NOT:** the boss, the soul loop, or any gameplay. He is a separate def the registry
never sees — he cannot become the boss, cannot drop a soul, cannot set `SlainForever`.

Build: clean, 0 warnings, `Assemblies/Dovahkiin.dll`.

---

## Before you start

Turn on **dev mode**: Options → gear icon → tick *Development mode*.

---

## 1. Does the mod still load?

1. Start (or load) a game with the full modlist.
2. Open the dev log (the ▢ icon top-left, or `` ` ``).
3. **Look for red errors mentioning `Dovahkiin_Alduin_Test`.**

**Pass:** no red. Yellow warnings are fine but tell me what they say.
**If it fails:** the likely cause is the new def file. Send me the red lines verbatim.

---

## 2. Does he appear, and is he the right size?

1. Dev toolbar → **Debug actions** → category **Dovahkiin** → **Spawn Alduin (TEST creature)**.
2. He spawns near the middle of the map.

**Look at:**
- **Is he there at all?** A magenta square or a black shape means the textures did not load.
- **How big is he next to a colonist?** He should be about **three times a colonist's width** —
  aimed at 4.6 cells against their 1.5. Dragon's Descent's adults are 4.2 if you want a ruler.
- **Does he read as a dragon**, or as a dark blob?

---

## 3. All four facings

1. Draft a colonist and walk them around him so he turns to track them — or just wait, he
   wanders.
2. **Watch him from all four directions.**

**Look at:** north should be the back of his skull, south his face, east/west his profile.
**A head at the bottom of the sprite, or a face on his back view, is a wiring mistake — tell me
which facing.**

---

## 4. The three movement states — the real test

1. Dev toolbar → **Debug actions** → **Dovahkiin** → **Cycle Alduin sprite state**.
2. Pick **Grounded**, look at him. Then **Soar**. Then **Flight**.

**Pass:** the sprite changes **immediately**, without him having to turn or move.

> **If it only changes when he turns**, say so exactly — that is a specific bug (a stale
> material cache) and knowing it is *that* symptom rather than "it doesn't work" saves a round.

**Also look at:** flight and soar should draw **larger** than grounded (5.6 vs 4.6 cells) —
he is meant to look nearer the camera when airborne.

---

## 5. Does the swap survive a save and reload?

1. Set him to **Flight**.
2. **Save. Quit to menu. Load the save.**
3. Look at him.

**Expected:** he will be **back to Grounded** — that is correct for now and not a bug. The state
is not saved yet, because nothing decides it; grounded is the def's default and what he reverts
to. **I need to know it reverts cleanly rather than turning invisible or magenta.**

---

## 6. Timeless — no aging, no breeding

1. Click him, open the **Health** tab and the info card (**i**).
2. Let a few in-game days pass at speed 3.

**Look at:**
- His **age does not matter** and he should gain **no age-related conditions** — no bad back,
  no cataracts, no frailty. Ever.
- He should **never** produce a "gave birth" or mating message.
- **His sprite must not change on its own.** If it ever silently reverts to grounded while you
  are watching, tell me — that would mean something re-resolved his graphics and it is the one
  failure mode I could not rule out from reading the code alone.

---

---

## 7. He picks his own state — REVISED after your first run

Your report: *"he barely switches to soar at all and is using flight a bit too often"*, plus the
request for a cooldown between switches. **The cooldown was the fix for both** — the state was
*flickering* every time he started or stopped wandering, because idle-and-moving is Flight and
idle-and-stationary is Grounded. Each state now has a minimum dwell time before it can be left.

### 7a. Idle — no fight

1. Spawn him away from the colony and **leave him alone**.

| what he's doing | expected |
|---|---|
| wandering / circling | **Flight** |
| standing still | **Grounded** |

**Soar should barely appear while idle, and that is correct** — soar is a *combat* stance.

**What to watch for:** he should hold each state for a few seconds now, not flip every time he
takes a step. If he still flickers, tell me — the dwell timer isn't working.

### 7b. In a fight — the rhythm

**This needs you to actually attack him** (dev → damage, or set a colonist on him).

Target, per your spec: **soar ≥ grounded > flight**, with flight used occasionally to break off
and come back.

| in combat | expected |
|---|---|
| you get adjacent to him | he **lands** — he can't bite from the air |
| at range | mostly **Soar**, dropping to **Grounded** fairly often |
| occasionally | breaks into **Flight**, backs off, then **returns to Soar** |

**Flight should be the rarest of the three by time spent.** It's an excursion, not a stance —
its dwell is the shortest and it always returns to soar.

**These are the knobs**, all in `Defs/MiscDefs/DovahkiinTuningDef.xml`, edit and restart:

| if he… | change |
|---|---|
| flies too much | lower `dragonCombatSoarToFlightChance`, or shorten `dragonMinSecondsFlight` |
| stays grounded too much | raise `dragonCombatGroundedToSoarChance` |
| soars too much | raise `dragonCombatSoarToGroundedChance` |
| still switches too twitchily | raise all three `dragonMinSeconds*` |

Tell me roughly what the split *felt* like — "half the time grounded, barely soared" is more
useful than a number.

---

## 8. Speed

1. Watch him cross open ground in **Flight**, then in **Soar**, then **Grounded**.
2. Click him → **i** (info card) → find **Move Speed**.

**Expected:** the info card should say **"Airborne: x1.20"** when soaring and **"x1.80"** when
flying, and nothing at all when grounded.

Flight should look clearly faster than soar, and soar only slightly faster than grounded. If
soar and grounded look identical, tell me — that's a different bug from "the numbers are wrong".

**These numbers are provisional and you said you'd recalibrate in game.** They're in
`Defs/MiscDefs/DovahkiinTuningDef.xml` as `dragonSoarSpeedFactor` and `dragonFlightSpeedFactor`
— edit and restart, no rebuild.

---

## 9. Wounded, he stays down

1. Dev → **Damage** him (or shoot him) until he's **below half health** — watch the health bar.
2. Let him keep fighting or moving.

**Expected: once at or below half, he stays GROUNDED and does not take off again.**

**This is not a latch and shouldn't behave like one.** If you leave him alone for a long time
and he heals naturally back above half, he *should* be able to fly again. That's intended — he's
a recurring threat, not permanently crippled. **What must NOT happen is unusual regeneration:**
he heals like any vanilla creature, no faster.

> **NOT YET IMPLEMENTED, so don't test for it:** the wing-damage grounding rule (50% to each
> wing, or 80% to one). It needs a dragon `BodyDef` that doesn't exist — the test creature uses
> vanilla `Bird`, which has no wings to damage. Only the half-health half of the rule is live.

---

## Also not implemented yet — don't report these as bugs

Deliberately left out so that a failure in the state machine can be attributed to the state
machine:

- **Melee immunity while airborne**, and **inability to melee** while airborne
- **Crossing roofs, walls and obstacles** (and being blocked by natural mountain rock)
- **Dragon breath of any kind** — the shouts, the cone/circle/strafe geometries
- **Wing, maw, tail and leg wound effects**

## What to send back

A screenshot beats a description every time — `Tools/CaptureGame.ps1` writes one, or just use
Steam's. Most defects in this project have been visual, and every one of them cost a round of
guessing at what the words meant.

If something is wrong, the useful details are: **which facing**, **which state**, and **whether
it was wrong immediately or only after he moved**.
