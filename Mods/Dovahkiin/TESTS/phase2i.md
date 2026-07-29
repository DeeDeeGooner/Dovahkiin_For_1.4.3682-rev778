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
3. It is expensive: 9 thu'um at one word, 22 at three. Refill often.
4. Its cooldown is by far the longest in the mod — a minute at one word, two at three.
   Use **Dovahkiin → Clear shout cooldown** between tests or you will be waiting.

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

1. With **mul** active, look at the Dovahkiin closely.
2. ✅ **Arm armour only** — spectral plates down both arms, two small spikes at each elbow.
   Nothing on the chest yet.
   - **Check the fit.** The armour should match the pawn's own width, not sit inside it.
     It now borrows the pawn's own body mesh, so it should be right for any body type
     or body mod. If it still looks the wrong size, tell me what that pawn is —
     body type, xenotype, child or adult.
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

## Test 6 — Shout cooldown, three words only

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
