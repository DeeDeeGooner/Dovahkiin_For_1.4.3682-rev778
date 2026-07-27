# Phase 2c test — Slow Time and Become Ethereal

Two new shouts, and they behave unlike anything built so far: **neither one asks you for a
target**. You press the button and it happens to the Dovahkiin. No cone, no wave, no damage.

- **Slow Time** (*Tiid Klo Ul*) — you move and fight faster. The world does **not** slow down;
  that is deliberate, and explained at the bottom.
- **Become Ethereal** (*Feim Zii Gron*) — nothing can hurt you, and you cannot attack. You can
  still walk.

**Not in this build:** Storm Call, Soul Tear, Dragon Aspect. Icons are still borrowed vanilla
art (`ART_TODO.md`).

About ten minutes. **Play at normal speed, not 3x** — these effects are measured in game ticks,
and at 3x a six-second buff is over in two.

---

## Setup

1. Launch RimWorld. **Options → Development mode** on.
2. **New colony**, small map, any scenario.
3. Debug actions → **Dovahkiin → Force awaken pawn** → pick a colonist.
4. **Dovahkiin → Grant 10 souls**.
5. **Dovahkiin → Learn all words**. ✅ It should say *"Discovered 24 new word(s)."*
   - If it says 18, the two new shout files did not load — stop and tell me.
6. **Dovahkiin → Raise a shout one level**. ✅ The list now has **8** shouts, including
   **Slow Time** and **Become Ethereal**.

---

## Test 0 — Clear Skies (retest, it was broken)

Do this one **first**. Clear Skies had the same fault as the two new shouts and has never
actually worked since Phase 2a — the button was there, but pressing it did nothing.

1. Raise **Clear Skies** to level 1.
2. Debug → weather → set to **Rain**.
3. Select the Dovahkiin, click **lok**.
   - ✅ It fires immediately, no targeting cursor.
   - ✅ **The rain stops.**
4. Check the Dovahkiin's **Health** tab → ✅ a **voice strain** entry appeared.
   - This is the proof a shout actually fired. If there's no strain, nothing was cast, and the
     fix did not work — stop and tell me.

---

## Test 1 — They appear, and they don't ask for a target

1. Raise **Slow Time** to level 1, and **Become Ethereal** to level 1.
2. Select the Dovahkiin.
3. ✅ Two new buttons on the command bar: **tiid** and **feim**.
4. Click **tiid**.
   - ✅ It fires **immediately** on the pawn. No targeting cursor, no "click a spot".
   - This is the main thing to check. If it asks you to pick a target, that is a bug.

---

## Test 2 — Slow Time actually makes you faster

1. Before shouting, click the Dovahkiin → **Stats** tab → find **Move speed**. Write it down.
2. Shout **tiid**.
3. ✅ A **slow time** entry appears in the **Health** tab, with a countdown.
4. ✅ **Move speed** in the Stats tab has gone **up** by about 1.6.
5. Draft the pawn and send them somewhere far. ✅ They visibly outrun the other colonists.
6. Wait for the countdown to finish. ✅ The hediff disappears and move speed returns to normal.

### Test 2b — and faster in a fight

1. Raise Slow Time to **level 3** (two more souls).
2. Give the Dovahkiin a melee weapon. Note **Melee DPS** in the Stats tab.
3. Shout **tiid klo ul**, then check **Melee DPS** again.
   - ✅ It should be noticeably **higher** — at level 3 attacks take 40% as long.
4. Spawn something hostile and let them fight. ✅ They swing visibly faster than normal.
   - Works the same with a gun; the shout speeds up both.

---

## Test 3 — Become Ethereal: untouchable, and harmless

This is the one I most want your judgement on.

1. Shout **feim**. ✅ An **ethereal** entry appears in the Health tab with a countdown.
2. While it is up, **spawn a raid** or a manhunter pack near the pawn (debug → incidents).
3. ✅ **Nothing damages them.** No wounds appear in the Health tab at all.
4. ✅ The Dovahkiin **will not attack**. Draft them and right-click an enemy —
   they will refuse to strike. This is intended, not a bug.
5. ✅ They **can still walk**. Movement is deliberately untouched, so this is an escape tool.
6. When the countdown ends, ✅ they become vulnerable again and can fight normally.

**What I want to know:** does the trade feel right? Invulnerable-but-harmless is the Skyrim
behaviour, but it may feel useless in RimWorld, or it may be too strong as a
get-out-of-a-lost-fight button. Your call, and the duration is easy to change.

---

## Test 4 — It survives a save

1. Shout **tiid klo ul**, and while it is still counting down: **save, quit to menu, reload**.
2. ✅ The buff is still there with time remaining, or has cleanly expired. Either is fine.
3. ✅ Move speed is correct for whichever of those happened — no pawn left permanently fast.
   - A pawn stuck fast forever would be the bad outcome. That is what this test is for.

---

## Test 5 — The log is clean

Don't read it yourself — just tell me you finished, and I'll read it. If anything looked wrong,
say what you saw in your own words; the specifics are usually the clue.

---

## Two things I decided, that you should know about

**Slow Time does not slow time.** In Skyrim it slows the world. Here it makes *you* faster
instead. Slowing the world in RimWorld would mean slowing your whole colony, every caravan and
every job on the map, and it would fight RocketMan directly. `SPEC.md §4.4a` already called for
this rework — I am flagging it because the name now slightly oversells what it does, and if
that bothers you in play, say so.

**Become Ethereal stops damage, not everything.** You cannot be wounded, but you can still
collapse from a wound you already had, from starvation, or from a mental break. It is a combat
panic button, not god mode.
