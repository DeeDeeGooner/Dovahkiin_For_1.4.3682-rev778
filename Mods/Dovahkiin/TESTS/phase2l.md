# Phase 2l test — Call of Valor as a SHOUT

The summon itself is already signed off (`phase2k.md`, including the save-reload check). **This
script is only about the shout wrapped around him** — the words, the button, the aiming and the
portal appearing where you point.

**Restart RimWorld** — the assembly, four def files and a new icon all changed.
About ten minutes.

> **He is still granted from the debug menu, not from a quest.** The quest that hands over his
> three words does not exist yet. **Learn all words** now includes them, which is the whole of
> what the quest will eventually do.

---

## Setup

1. Dev mode on → **Debug actions** (⚙) → **Dovahkiin** → **Learn all words**.
2. **Dovahkiin → Raise a shout one level** → pick **Call of Valor**. Click it **three times**.
3. **Dovahkiin → Refill Thu'um / clear cooldown** — it costs 24, which is a lot.

---

## Test 1 — The button

1. Select your Dovahkiin.
2. ✅ A new shout button appears: **hun kaal zoor**.
3. ✅ Its icon is a **comet with a bright white head fading to grey**, with a small **blue circle**
   in the head. It should NOT look like Dragon Aspect's orange one.
4. Hover it. ✅ The description mentions Sovngarde and a greatsword.

**If the icon is orange, it is still pointing at Dragon Aspect's** — tell me.

---

## Test 2 — Aiming it

This is the only shout in the mod you aim at a **spot on the ground**. Every other one fires out
from your Dovahkiin.

1. Click the button. ✅ You get a **targeting cursor**, not an instant cast.
2. ✅ You can only target **empty ground** — not pawns, not walls, not items.
3. ✅ The reach is **short, about six tiles**. You should not be able to place him across the map.
4. Try to target a spot **through a wall**. ✅ It should refuse — line of sight is required.
5. Click a valid cell. ✅ The portal opens **there**, not on your Dovahkiin.

---

## Test 3 — He arrives properly

1. ✅ The portal spins up first, and **he steps out at the bright flash** — not the instant you
   click. That timing was fixed once already; this confirms it still holds through the shout.
2. ✅ Spectral white-blue armour, greatsword, **no aura**.
3. ✅ He is **not** in the colonist bar at the top of the screen.
4. ✅ His health tab shows the **hero of sovngarde** hediff, about **12 in-game hours**.

**All three words give the same hero.** There is no weaker one-word version — the quest grants
all three together, so there is nothing in between to test.

---

## Test 4 — The cost, and that it cannot be spammed

1. Check the Thu'um bar before casting. ✅ Casting takes **24** — the most expensive in the mod.
2. ✅ Afterwards, **every** shout is locked out for about **four minutes**. That is the shared
   cooldown, and it is deliberate: he is a whole armoured ally for twelve hours.
3. ✅ With too little Thu'um the button is disabled rather than doing nothing silently.

---

## Test 5 — Nothing else broke

The icon generator rewrites **all sixteen** icons, and shared drawing code has been touched
several times this session.

1. ✅ Every other shout's icon looks as it did — check the command bar with all words learned.
2. ✅ Cast **Dragon Aspect** at three words and confirm the **Ancient Dragonborn** still arrives
   with his **axe held correctly** on all facings.

That second one matters: his weapon angles and Call of Valor's now come from the same code, and
his are the ones already signed off.

---

## What to send me back

- Which tests passed, one line each.
- For anything wrong, **what you actually saw**.
- Red errors: I will read the log, don't transcribe it.
