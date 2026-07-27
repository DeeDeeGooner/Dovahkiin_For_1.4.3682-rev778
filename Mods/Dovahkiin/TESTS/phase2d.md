# Phase 2d test — three new shouts, ring bursts, longer buffs

Three brand-new shouts, plus the four fixes you asked for. **11 of 14 core shouts now built.**

**Play at normal speed, not 3x.** Stuns and buffs are measured in game ticks.

About fifteen minutes.

---

## Setup

1. Restart RimWorld (the assembly changed this time, not just XML).
2. Load your `Dovahkiindebug` save, or start fresh — either works.
3. **Dovahkiin → Learn all words.** ✅ It should now report **33** words total.
   - If it says 24, the batch-three files did not load — stop and tell me.
4. **Dovahkiin → Grant 10 souls** a couple of times; you will need souls for levels.
5. **Raise a shout one level** → ✅ the list now shows **11** shouts, including **Drain
   Vitality**, **Dismay** and **Cyclone**.

---

## Test 1 — The two fixes to what you already had

1. **Clear Skies**: set weather to rain, cast **lok**.
   - ✅ It now makes a **thunder crack** at the Dovahkiin, not a distant rumble.
   - ✅ A pale blue ring sweeps outward from the caster. It had no visual at all before.
2. **Slow Time**: cast **tiid**.
   - ✅ A **gold ring expands outward from the Dovahkiin**, a circle centred on them.
   - ✅ Along with it, a faint shimmer/ripple in the air — the "ceremony boom".
   - ✅ It now lasts **10 seconds** at one word, up from 6.
3. **Become Ethereal**: cast **feim** → ✅ now **8 seconds** at one word, up from 4.

**Tell me if these are still too short.** They are one number each and trivial to change.

---

## Test 2 — Cyclone (the stun one)

1. Raise **Cyclone** to level 1. Cast **ven** at some hostiles.
2. ✅ The wave is a **swirl that turns as it travels**, not a straight cone.
3. ✅ It is **very faint grey** — deliberately less visible than Whirlwind Sprint's trail.
4. ✅ Targets are **stunned for about 2.5 seconds** and take almost no damage.
5. Raise to level 3 and cast **ven gaar nos**.
   - ✅ Stun is now about **7.5 seconds** — Fus Ro Dah's 5s plus half again, as asked.
   - ✅ Damage is still tiny (5 total, against Fus Ro Dah's 12).

**Judgement call for you:** is the swirl readable, or too faint to see at all? I erred toward
faint because you asked for less visible than Whirlwind Sprint, but that is easy to dial up.

---

## Test 3 — Dismay (the fear one)

1. Raise **Dismay** to level 1, cast **faas** at a group of raiders.
2. ✅ A **red** wave travels out.
3. ✅ Roughly **a third** of them break and **run away** (a panic mental state).
4. ✅ Survivors get a **dismayed** entry in their Health tab — worse aim, worse melee.
5. Raise to level 3, cast **faas ru maar** → ✅ **nearly everything that hears it routs.**

---

## Test 4 — Drain Vitality (the one that touches the magic mod)

This is the one with the mod-compatibility question in it, so it has two halves.

1. Raise **Drain Vitality** to level 1, cast **gaan** at hostiles.
2. ✅ A **deep dark purple** wave — noticeably darker and more purple than Marked for Death.
3. ✅ Victims get **vitality drained** in the Health tab, and visibly slow down.
4. Click a victim → **Needs** tab.
   - ✅ Their **Rest** is draining faster than normal.
   - ✅ **If they are a magic/might user from RimWorld of Magic**, their **stamina** bar drains
     too. Ordinary raiders have no such bar and that is correct, not a bug.
5. Raise to level 2 → ✅ **mana** drains as well on magic users, and Joy drops.
6. Raise to level 3, cast **gaan lah haas**
   - ✅ Victims now also take **steady health damage**, spread over the body.
   - ✅ It stops after a while — the health drain is deliberately capped so it wears people
     down rather than guaranteeing a kill.

**What I want to know:** does level 3 feel like a slow kill or an instant one? The cap is one
number and I would rather tune it than have it feel like Marked for Death again.

---

## Test 5 — Nothing broke

1. Cast the older shouts once each — **fus ro dah**, **yol**, **fo**, **wuld**, **krii**.
   ✅ All still behave as before.
2. Save, quit to menu, reload. ✅ Everything still present, no pawn stuck buffed.
3. Tell me you are done and I will read the log.

---

## Notes on decisions I made

**The shout list grew from 11 to 14.** Drain Vitality and Dismay were on the "deferred, ask
first" list in the spec; you asked, so they are in. Cyclone was on no list at all. The cost is
that word walls go from 33 to 42 — that is a **Phase 7** number (the world content), so I have
recorded it rather than inventing a figure now. Nothing about it affects play today.

**Cyclone level 1 has a 2.5s stun rather than none.** You asked for "Fus Ro Dah plus half", and
Fus Ro Dah level 1 has no stun at all — so the arithmetic would have given zero, leaving a
level-1 Cyclone that does 1 damage and nothing else. Say if you would rather it be literal.

**The magic mod stays optional.** Drain Vitality looks up the mana and stamina bars by name and
simply skips them when they are not there. With the magic mod removed the shout still works,
draining rest and joy instead. Nothing in the code references that mod directly.
