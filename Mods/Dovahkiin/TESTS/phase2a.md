# Phase 2a test — the first shouts

This is the first time the mod does something you can actually *use*. Three shouts, the Thu'um
bar, and the progression rule where words gate levels.

**Not in this build yet:** Dragon Aspect's visual, Storm Call, Soul Tear, and the other shouts.
Those come once this foundation is verified. Shout icons are borrowed vanilla art — logged in
`ART_TODO.md`.

About ten minutes.

---

## Setup

1. Launch RimWorld. **Options → Development mode** on.
2. **New colony** — a fresh one is cleaner than your old save. Small map, any scenario.
3. Debug actions menu → **Dovahkiin → Force awaken pawn** → pick a colonist.

---

## Test 1 — The Thu'um bar exists

1. Click the Dovahkiin. Open the **Needs** tab.
2. ✅ There's a **thu'um** bar, full.
3. Hover it — ✅ tooltip reads *"Thu'um: 10 / 10"* and explains that souls deepen it.
4. Click any *other* colonist → ✅ they have **no** thu'um bar. It belongs to the Dovahkiin alone.

---

## Test 2 — Souls deepen the well

1. **Dovahkiin → Grant 10 souls**.
2. Look at the thu'um bar again.
   - ✅ Maximum is now **30** (10 base + 2 per soul × 10). The bar may show as partly full — the
     well got deeper, it didn't refill.
3. **Dovahkiin → Refill Thu'um / clear cooldown** → ✅ back to 30/30.

---

## Test 3 — Words gate levels (the OD-10 rule you chose)

1. **Dovahkiin → Raise a shout one level**.
2. ✅ The menu lists three shouts, each showing *"level 0/0 words found"*.
3. Pick **Unrelenting Force**.
   - ✅ It **refuses**: *"The word for level 1 has not been found yet."*
   - This is the rule working — souls alone are not enough.
4. **Dovahkiin → Learn all words (slice)** → ✅ *"Discovered 9 new word(s)."*
5. **Raise a shout one level** again → ✅ now shows *"level 0/3 words found"*.
6. Pick **Unrelenting Force** → ✅ *"Unrelenting Force raised to level 1."*
7. Do it twice more → ✅ level 2, then level 3.
   - Each one costs a soul. You had 10, so you can afford it.

---

## Test 4 — Actually shout

1. **Draft** the Dovahkiin (or leave undrafted — shouts work either way).
2. ✅ On the pawn's command bar at the bottom, there's a **fus ro dah** button.
   - Only the *current* level shows. You shouldn't see "fus" and "fus ro" as well.
3. Spawn something to shout at: debug menu → **Incidents → raid**, or use the pawn-spawner tool
   to drop a few hostiles some distance away.
4. Click **fus ro dah**, then click on a hostile.
5. ✅ The pawn winds up briefly, then:
   - hostiles in a cone in front of them are **thrown backwards several tiles**
   - they take light damage and are **briefly stunned**
   - ❌ if nothing moves, tell me.

---

## Test 5 — Cost, shared cooldown and strain

1. Right after shouting, look at the **thu'um bar** → ✅ noticeably drained.
2. Try to shout again immediately → ✅ the button is **greyed out**. Hover it → *"The Thu'um is
   still gathering."*
3. Now learn **Fire Breath** to level 1 (**Raise a shout one level** → Fire Breath).
4. Try to cast **yol** while still recovering → ✅ also greyed out.
   - **This is the important one.** The cooldown is shared across *all* shouts, not per-shout —
     you can't chain them. That's the Skyrim-faithful behaviour from the spec.
5. **Refill Thu'um / clear cooldown**, then shout twice in quick succession.
   - ✅ On the Dovahkiin's **Health** tab there's now a **voice strain** entry.
   - ✅ The second recovery is visibly longer than the first.

---

## Test 6 — Fire Breath and Clear Skies

1. Refill, then cast **yol** at some hostiles → ✅ a cone of fire, they take burn damage.
2. Raise Fire Breath to level 2, refill, cast **yol toor** → ✅ wider, and targets **catch fire**.
3. Learn **Clear Skies** level 1. Wait for rain or snow (or debug → weather → set to rain).
4. Cast **lok** → ✅ the weather clears.
   - ✅ No target needed — it fires immediately with no targeting cursor.

---

## Test 7 — Can't shout without a mouth

1. Debug → **Damage** → apply damage to the Dovahkiin's **jaw** until it's destroyed
   *(or just anaesthetise them: debug → **Hediffs → Anesthetic**)*.
2. ✅ All shout buttons grey out, and the reason names the missing capability.
3. That's vanilla RimWorld doing it, not custom code — SPEC §4.3 satisfied for free.

---

## Test 8 — Save and load

1. **Save**, quit to menu, **load**.
2. ✅ Shout levels preserved — **fus ro dah** still on the bar, still level 3.
3. ✅ Thu'um bar still shows the deepened maximum.
4. Press **`~`** → ✅ no red lines mentioning `Dovahkiin`.

---

## Reporting back

Tell me the test numbers that passed, and for anything that failed, **what you saw instead**.

Two things I especially want to know because I can't see them from the log:
- **Does the knockback look right?** Enemies flung backwards, not teleporting oddly or getting
  stuck in walls.
- **Does the shared cooldown feel right**, or is it so long it's annoying? Every number is in
  `Defs/MiscDefs/DovahkiinTuningDef.xml` and I can retune without a rebuild.
