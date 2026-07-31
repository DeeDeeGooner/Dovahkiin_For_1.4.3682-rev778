# Phase 2k test — Call of Valor's summon

The hero of Sovngarde. **Nothing about this has ever been in the game**, and a temporary pawn is
the top save-corruption risk in the mod (`RISKS.md` §9) — so **Test 5 is the one that matters
most**, exactly as it was for the Ancient Dragonborn.

**Restart RimWorld** — the assembly, four def files and a language file all changed.
**Play at normal speed.** About twenty minutes.

> **He is summoned from the DEBUG MENU, not from a shout, and that is expected.** Call of Valor
> is a quest-locked shout and neither the quest nor the ability exists yet. Testing the summon
> before building the shout around it is deliberate: the summon is the risky half, and building
> the cheap half on top of an unverified one is how a failure becomes impossible to attribute.

---

## Setup

1. Dev mode on → **Debug actions** (⚙) → category **Dovahkiin**.
2. You need a Dovahkiin to exist. If you have none: **Force awaken pawn**.
3. That is all — he needs no words, no Thu'um and no cooldown.

---

## Test 1 — He arrives, through a portal, where you point

1. **Dovahkiin → Summon Call of Valor (pick a cell)**.
2. Click an **open cell** a few tiles away from your colonists.
3. ✅ A **portal of bright white waves** opens on that cell.
4. ✅ **He is standing there** — spectral white-blue armour and helm, no visible skin.
5. ✅ He carries the **greatsword**, point up, not the Ancient Dragonborn's axe.
6. ✅ **NO AURA.** No ring of glow, no crescent particles around him. That is the whole point of
   his look — the aura belongs to your Dovahkiin and to nobody else.

**If you see crescents orbiting him, stop and tell me** — that means he is drawing your
Dovahkiin's aura and the two characters have been mixed up.

---

## Test 2 — He is HIM, not a recoloured Ancient Dragonborn

Summon **both** if you can (hurt your Dovahkiin below 65% and cast **mul qah diiv** for the
Ancient Dragonborn, then summon Valor from the debug menu). Put them side by side.

1. ✅ Valor's armour is **spectral white-blue**; the Ancient Dragonborn's is **bronze-into-blue**.
2. ✅ Valor has **pauldrons** — overlapping curved plates. The Ancient Dragonborn has **swept fins**.
3. ✅ Valor's helm has **feathered wings and a coronet**; the Ancient Dragonborn's has **horns**.
4. ✅ Valor carries a **greatsword**; the Ancient Dragonborn a **halberd/axe**.

**This is the check that the folder split worked.** Their 36 texture files share the same names
and are told apart only by which folder they live in. If Valor turns up in bronze, or your
Dovahkiin's Dragon Aspect armour suddenly looks white, the two sets have crossed.

---

## Test 3 — He fights, and he only knows TWO shouts

1. Summon him with enemies on the map — a raid, or spawn some raiders in dev mode.
2. ✅ He engages on his own. **He is never drafted and has no draft button** — that is correct.
3. Watch him over a few minutes.
4. ✅ He uses **Unrelenting Force** (a blue shove) and **Frost Breath** (a pale blue cone).
5. ❌ **He must NEVER breathe fire.** Fire is the Ancient Dragonborn's, not his. If you see an
   orange breath cone from Valor, tell me — that is the one thing his code overrides.
6. ✅ He does not breathe through your own colonists. If one walks into the cone he should hold.

---

## Test 4 — Twelve hours, and he is weaker than the Ancient Dragonborn

1. Click him → **health tab**.
2. ✅ The **hero of sovngarde** hediff shows about **12 in-game hours** remaining (30,000 ticks) —
   twice the Ancient Dragonborn's six.
3. ✅ Same tab, his armour reads **66% sharp / 33% blunt**. The Ancient Dragonborn is 75/75.
   He is the tougher one against blunt; Valor lasts longer. That trade is deliberate.

Both are editable without a rebuild: `Defs/MiscDefs/DovahkiinTuningDef.xml`,
`callOfValorLifetimeTicks`. Restart the game, no build.

---

## Test 5 — THE ONE THAT MATTERS: he leaves nothing behind

This is the save-safety test. `RISKS.md` §9 exists because a temporary pawn that outlives its
own timer becomes a permanent, unkillable pseudo-colonist in your save.

1. Summon him.
2. **Save the game.** Name it something you can find.
3. **Quit to the main menu and reload that save.**
4. ✅ He is still there, still armoured, still carrying his sword.
5. ✅ His timer is **still counting down** — and from roughly where it was, not reset to 12 hours.
6. Now let him **run out** (or reduce `callOfValorLifetimeTicks` to something small first).
7. ✅ He **vanishes**. No corpse, no grave, no "colonist died" letter, no mood hit on anyone.
8. ✅ His **armour and his sword vanish with him** — no floating plates, no greatsword left on
   the ground.
9. ✅ **No red errors in the log** at the moment he goes.
10. Save and reload once more. ✅ He is not back, and nothing references him.

**If a line beginning `SAFETY SWEEP` appears in the log at any point, tell me even if everything
else looked fine.** That means a summon existed in a state that is supposed to be impossible.

---

## Test 5b — Kill him instead

1. Summon him and have raiders kill him, or use the dev **Kill** tool.
2. ✅ Same as above: he goes, his gear goes, no corpse, no colonist-death letter, no red errors.

A player-faction pawn being destroyed used to throw an Ideology error every time. That was fixed
for the Ancient Dragonborn by dropping his faction first, and Valor inherits that code — but he
has never been through it, so this is worth its own look.

---

## What to send me back

- Which tests passed, one line each.
- For anything wrong, **what you actually saw**. "The aura was there" and "he was bronze" point
  at completely different faults.
- Red errors: I will read the log, don't transcribe it.
