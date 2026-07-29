# Phase 2i test — Dragon Aspect (armour, resistances, melee, cooldown)

**The fourteenth and last core shout.** This round covers everything except the summon —
the Ancient Dragonborn is not built yet and is deliberately absent.

Dragon Aspect wraps you in spectral dragon armour. One word gives armour and heavier blows,
two adds fire and frost resistance, three adds a shorter shout cooldown.

**Restart RimWorld** (the assembly changed). **Play at normal speed** — control effects are
judged at normal speed, never at 3x.

About fifteen minutes. **Test 2 is the one I most want your eyes on**, because it is the
overlay the whole spec hangs on.

---

## Setup

1. Debug mode on. **Dovahkiin → Learn all words** → ✅ should report **42** words.
   - If it says 39, the Dragon Aspect files did not load — **stop and tell me**.
2. **Raise a shout one level** → ✅ **14** shouts listed, including **Dragon Aspect**.
3. It is **free** — no thu'um cost at any level. You never need to refill for it.
4. It is **once per day**: a 24-hour in-game cooldown on the button itself.
   You will need **Dovahkiin → Clear shout cooldown**, or dev-mode time skip, between
   nearly every test below. Expect that.

---

## Test 1 — It casts at all

1. Raise **Dragon Aspect** to level 1. Select the Dovahkiin, click **mul**.
2. ✅ It does **not** ask you to aim. The shout goes off on you where you stand.
3. ✅ An **orange ring** snaps outward from your feet — and then a second ring travels
   **back inward** to you, turning **blue** as it comes home.
   - The return only starts once the outgoing ring has finished, so watch for about a second.
4. ✅ Health tab shows **dragon aspect**, stage **Mul**, with a countdown.

If the button does nothing at all, stop — that is the specific failure this shout shape is
built to avoid and I want to know immediately.

---

## Test 2 — THE OVERLAY (the important one)

**This is the test that has now failed twice, for two different reasons.** Round one it was
drawn at a fixed size that ignored the pawn. Round two it borrowed a mesh that RimWorld
deliberately makes *smaller* than the pawn — which is why you saw it sitting inside them.
It now uses the exact mesh the pawn's own body is drawn on, so it should line up.

1. With **mul** active, look at the Dovahkiin closely.
2. ✅ **Arm armour only** — spectral plates down both arms, two small spikes at each elbow.
   Nothing on the chest yet.
   - **THE THING TO JUDGE: does the armour reach the edge of the pawn?** The plates should
     sit *on* the body's outline, not floating inside it with a gap of bare skin showing
     around them. The shoulder fins are meant to stick out past the outline — that part is
     deliberate.
   - If it is still inset, say roughly how much — "a hair", "a few pixels", "way inside".
     That distinguishes a leftover art problem from a wrong mesh, and they need opposite fixes.
3. **Walk the pawn around.** ✅ The armour follows exactly, with no lag and no drift.
4. **Turn the pawn** so it faces up, down, left and right.
   ✅ The armour turns with them. ✅ Facing left is the mirror of facing right.
5. **Draft and undraft.** ✅ Still correct.
6. Wait for the shout to expire. ✅ The armour **vanishes cleanly** — no leftovers.

Now raise it to **level 2** and cast **mul qah**:

7. ✅ Full torso plates, **three swept fins on each shoulder**, and a **jagged crystal crest**
   down each side of the chest.
8. ✅ Colour runs **blue at the shoulders into bronze at the waist**.

Now **level 3**, cast **mul qah diiv**:

9. ✅ A **horned helm** appears on the head, jagged at the back, gold crown into blue.
   - The helm should sit **on the head**, not floating above or behind it. If it is
     misplaced, tell me roughly by how much and in which direction.
10. ✅ An **aura**: two soft bands of light, plus small **S-shaped crescents** winking in and
    out around the pawn — some orange, some blue, some blending between the two.
11. ✅ Zoom out to normal play distance. Does it still read? Tell me if it is too subtle or too loud.

---

## Test 2b — The other body types (NEW — 30 textures, one set per body)

The armour used to be traced from a **male** body and worn by everyone. Male is widest at the
shoulders; female is narrow-shouldered with a pinched waist and widest at the hips; thin is a
straight tube; fat is widest at the belly; hulk is much taller. Each now has its own art.

Your Dovahkiin is **Leonid**, who is `Male` body type — so **the whole of this test needs the
Dovahkiin moved onto someone else.** Your colony has all five types (10 Female, 8 Male,
8 Hulk, 7 Thin, 2 Fat).

**To move it:** Debug → **Dovahkiin → Clear registry**, then **Dovahkiin → Force awaken pawn**
and pick a colonist of the body type you want. Then **Learn all words** and **Raise a shout
one level** again for that pawn.

> **Do this on a throwaway save, or reload afterwards.** It genuinely reassigns who the
> Dovahkiin is, and there is only ever one per save by design.

For each body type you try — **Female is the one I most want checked**:

1. Cast **mul qah** (two words) so the full torso plates are on.
2. ✅ The plates follow **that body's** outline. On a female pawn the waist should pull in and
   the plates should widen again at the hips, rather than running straight down.
3. ✅ The shoulder fins sit **on the shoulders**, not floating off the sides.
4. ✅ Nothing is stretched, squashed, or noticeably off-centre.
5. ✅ No pink or black squares anywhere — that would mean a texture failed to load.

If you would rather not move the Dovahkiin around, **say so and skip this whole test** — I can
check the fit from the sprites myself. It is your pawn and your save; the male case in Test 2
is the one that actually matters for your current game.

---

## Test 3 — Armour actually applies

1. Cast **mul** (one word). Open the Dovahkiin's **Health → Stats**, or hover the pawn.
2. Note **Sharp** and **Blunt** armour. ✅ Both up by **+10%** versus before the shout.
3. Cast **mul qah** → ✅ both up by **+40%**, and **Heat** armour up **+40%** too.
4. Cast **mul qah diiv** → ✅ **+60%** on Sharp, Blunt and Heat.
5. ✅ At two words and up, **cold insulation** also rises (+20°C, then +30°C).

If a number is there but feels wrong, tell me the number — I will do the arithmetic.

---

## Test 4 — Heavier blows

1. **No shout active.** Draft the Dovahkiin, melee a raider, watch the damage numbers.
   Note roughly what a hit does.
2. Cast **mul**, melee the same kind of target again.
3. ✅ Hits land for about **a quarter more** damage.
4. ✅ Cast **mul qah** and **mul qah diiv** — melee damage should be **the same** as at one
   word. That is intended: you asked for heavier blows at word one only.
5. **Have an ordinary colonist melee something while Dragon Aspect is up on the Dovahkiin.**
   ✅ Their damage is **unchanged**. The bonus is the Dovahkiin's alone.

Point 5 matters — it is a patch on the melee path, and I want to be sure it is not leaking
onto every pawn in the colony.

---

## Test 5 — Fire and frost

1. Cast **mul qah** (two words).
2. Have something set the Dovahkiin on fire, or walk them through flame.
   ✅ They take noticeably less heat damage than an unprotected colonist would.
3. If you have a frost source handy — a RimWorld of Magic ice spell, a Dragon's Descent frost
   breath — take a hit from it. ✅ Reduced.
4. Stand the Dovahkiin somewhere very cold. ✅ They tolerate it far better than normal, and
   frostbite is slow to start.

Frost was the awkward one: RimWorld has no frost-resistance stat, so this works by covering
the armour categories those mods actually use, plus real cold insulation. If some particular
frost effect still hurts as much as ever, **tell me which mod it came from** and I will look
up what damage type it uses.

---

## Test 6a — Once per day, and it barely blocks other shouts

This is the TES5 rhythm: a daily power, not a combat shout.

1. Cast **Dragon Aspect** at any level.
2. ✅ It cost **no thu'um** — the bar does not move.
3. ✅ The buff lasts **5 / 7 / 9 in-game hours** at one / two / three words. Check the
   countdown in the Health tab.
4. **Walk the pawn a long way from where you shouted, and off the screen edge and back.**
   ✅ The armour is still drawn the whole time, and still there when the countdown says so.
   - This was broken before: the overlay was culled by the cell it was cast on, so walking
     away made the armour vanish while the buff was still running.
4. **Immediately** try another shout — Unrelenting Force.
   ✅ Usable after about **one second**. Dragon Aspect barely locks the Voice at all.
5. Try to cast **Dragon Aspect** again.
   ✅ Refused, with a cooldown running to **24 in-game hours**.
6. Skip a day. ✅ Available again.

Point 4 and point 5 are the whole design: the *general shouting* lockout is a second, while
Dragon Aspect's *own* wait is a full day. If either is the other way round, tell me.

---

## Test 6b — Shout cooldown reduction, three words only

1. Cast **mul qah diiv**, then immediately cast any other shout — Unrelenting Force is cheap.
2. Watch the shared Thu'um cooldown. ✅ It runs about **a third shorter** than usual.
3. Let Dragon Aspect expire, then shout again. ✅ Cooldown back to normal.
4. Repeat with **one word** and **two words** active. ✅ **No** cooldown reduction —
   that is a three-word effect only.

---

## Test 7 — Save and reload

1. Cast **mul qah diiv**. While it is still running, **save**.
2. **Quit to menu, reload.**
3. ✅ The armour, helm and aura are all still there and still following the pawn.
4. ✅ The countdown resumes; the shout expires normally.
5. ✅ Nothing is left behind afterwards.

---

## Test 8 — Nothing else broke

1. Cast a few other shouts — Fire Breath, Unrelenting Force, Soul Tear.
   ✅ All behave exactly as before.
2. ✅ Dev-mode log has **no red errors**.

---

## Not in this build, on purpose

- **The Ancient Dragonborn** — the ghostly ally at three words. Next round. It is the
  riskiest thing left in the mod (temporary pawns are the top save-corruption risk in
  `RISKS.md` section 9), so it is being built on its own rather than bundled in here.
- **The ghostly two-handed axe** he carries comes with him.

---

## What to tell me

- Anything with a **red error** in the log — that first.
- Whether the **overlay reads well in normal play**, not just zoomed in.
- Whether the **helm sits right on the head**.
- Any number that **feels** wrong. You do not need to suggest a value — say which way it is
  off and I will bring you the arithmetic.
