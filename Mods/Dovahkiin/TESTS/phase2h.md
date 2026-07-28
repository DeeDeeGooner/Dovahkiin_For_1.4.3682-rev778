# Phase 2h test — Soul Tear

**13 of 14 core shouts.** Only Dragon Aspect left.

Soul Tear rips a single enemy's soul loose. Heavy damage, and at two words or more the body may
**rise and fight for you** — briefly. Then it dies, permanently, and nothing can save it.

**Restart RimWorld** (the assembly changed). **Play at normal speed.**

About fifteen minutes. **Test 5 is the important one** — it is the risk this whole design exists
to avoid.

---

## Setup

1. Debug mode on. **Dovahkiin → Learn all words** → ✅ should report **39** words.
   - If it says 36, the Soul Tear files did not load — stop and tell me.
2. **Raise a shout one level** → ✅ **13** shouts listed, including **Soul Tear**.
3. It is the most expensive shout in the mod — 12 thu'um at one word, 30 at three. Refill often.

---

## Test 1 — Single target only, and only enemies

1. Raise **Soul Tear** to level 1, select the Dovahkiin, click **rii**.
2. ✅ It asks you to click **a pawn**. You **cannot** target empty ground.
3. Try to target **one of your own colonists** → ✅ refused.
4. Try a **tamed animal** → ✅ refused.
5. If a neutral trader is around, try them → ✅ refused. Only hostiles are valid.
6. Cast it on a raider → ✅ heavy damage to that one raider. **No cone, no splash** — the
   raider standing next to them is untouched.

## Test 2 — Level 1 never raises anything

1. Cast **rii** on several raiders, killing some.
   - ✅ **Nothing ever rises.** Level 1 is damage only, by design.

## Test 3 — The puppet rises

1. Raise Soul Tear to **level 3** (two souls) and refill.
2. Cast **rii vaaz zol** on raiders repeatedly. Roughly **45%** should raise a puppet.
3. When one rises:
   - ✅ A message says it rises and will fight for you.
   - ✅ It **joins your colony** and fights raiders.
   - ✅ It has a **pulsing red mark** on it.
   - ✅ Select it → the inspect pane at the bottom says
     *"Torn soul: collapses in X. Cannot be healed, recruited or saved."* with a countdown.

**You should never be able to mistake it for a real colonist.** If you can, tell me — that is a
spec requirement, not a nicety.

## Test 4 — It cannot be saved

While a puppet is alive, try to:
1. ✅ **Tend its wounds** — the torn-soul hediff cannot be tended.
2. ✅ **Heal it** by any means — it still dies on schedule.
3. ✅ It is **not recruitable**, not a prisoner, not rescuable.
4. Wait for the countdown to reach zero.
   - ✅ **It dies.** Permanently.
   - ✅ Your colonists get **no "colonist died" mood hit** from it. Check a couple of moods
     before and after — this matters, and it is why it leaves your faction a moment before dying.

## Test 5 — THE RELOAD TEST

**This is the one that matters.** The whole design exists so that a puppet can never survive a
reload as an unkillable pseudo-colonist.

1. Raise a puppet with plenty of time left on its clock.
2. **Save the game. Quit to the main menu. Reload.**
3. ✅ The puppet is still there, still marked, and **its countdown is still running** and roughly
   where you left it.
4. ✅ No red errors on load.
5. Wait for the timer to run out → ✅ **it still dies.**

**If after a reload you find a puppet that is stuck, unkillable, cannot be banished, or has lost
its countdown — stop immediately and tell me.** That is the exact failure this design was chosen
to prevent, and I want the save file.

There is also a safety net: on every load the mod checks for any tracked puppet that has somehow
lost its hediff, kills it, and writes a loud red error. **You should never see that error.** If
you do, send it to me.

## Test 6 — Nothing else broke

1. Cast the other shouts once each. ✅ All behave as before.
2. Tell me you are done and I will read the log.

---

## What I decided, and why

**The puppet is always doomed — it is never restored.** The original design put the pawn back in
its old faction when the effect ended, and `RISKS.md §9` recorded that as the single highest
save-corruption risk in the mod: it needed a correct restore on seven different exit paths, one
of which was save→load, and getting it wrong leaves a player-faction pawn you cannot arrest,
banish or kill.

Making the puppet always die removes that entirely. Every exit already ends in death — the timer
kills it, being killed early is already death, being downed just means it dies downed, leaving
the map carries the clock with it. And the only thing that has to survive a reload is an
ordinary hediff, which uses RimWorld's normal save path.

**Level 1 raises nothing on purpose.** The spec makes the first word damage-only, so the puppet
is something you unlock by mastering the shout rather than a freebie.

**Numbers to retune**, in `Defs/MiscDefs/DovahkiinTuningDef.xml`, no rebuild needed:
`soulTearPuppetChanceByLevel` (0 / 0.25 / 0.45) and `soulTearPuppetHoursByLevel` (0 / 6 / 12).
Damage is per-ability in `Abilities_Batch3.xml` — 40 / 65 / 95.

**One thing for Phase 7, recorded so it is not lost:** `SPEC.md §4.4f` says Soul Tear's three
words belong in **high-tier crypts only**. That is a world-generation constraint for when word
walls get placed, not something this phase can enforce.
