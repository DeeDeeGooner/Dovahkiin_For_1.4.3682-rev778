# Phase 2f test — the ethereal fix, and Cyclone again

Short round. **Restart RimWorld** (the assembly changed). Existing save is fine.
**Normal speed, not 3x.**

---

## Test 1 — Become Ethereal: you cannot hit anything

This was a real bug and the fix is the important part of this round.

1. Draft the Dovahkiin, give them a weapon, cast **feim**.
2. **Right-click an enemy and order an attack.**
   - ✅ They **will not attack**. Melee or ranged, doesn't matter.
   - This is what was broken: the old block only stopped *the AI* choosing to attack, so a
     raider couldn't swing while ethereal but you could, because a player-ordered attack takes
     a completely different route through the game.
3. ✅ You **can still shout** while ethereal. Deliberate — otherwise you'd have no way to act
   at all for the duration. Say if you'd rather shouts were blocked too.

## Test 2 — Become Ethereal: nothing can hurt you

1. While ethereal, throw everything you can at them:
   - ✅ raiders shooting and meleeing
   - ✅ walk them **onto a trap**
   - ✅ set off an **explosion** next to them (debug → spawn IED, or a grenade)
   - ✅ **fire** — light the ground under them
   - ✅ if you have a magic user, **hit them with a spell**
2. ✅ **Nothing lands.** No wounds appear in the Health tab at all, from any source.
3. Check the **Stats** tab → ✅ "incoming damage" reads 0%.
4. When it expires → ✅ they are vulnerable again immediately.

---

## Test 3 — Cyclone, third attempt

1. Cast **ven** at a group.
   - ✅ It is **much narrower** now.
   - ✅ It reads as a **funnel** — particles orbiting a centre at different speeds, so the
     shape visibly turns as it travels, rather than a grey smear.
2. ✅ Damage still lands on very few body parts.

**Be blunt about this one.** It is built from vanilla dust particles arranged into orbits,
because a real tornado needs a drawn swirl texture that I cannot produce — the magic mod does it
with one purpose-made spinning sprite, and RimWorld's own textures are packed away where a mod
cannot reach them. If this still doesn't read as a tornado, the honest answer is that it needs
art, and I have written the exact spec in `ART_TODO.md` for an artist or an image tool.

---

## Test 4 — Clear Skies

1. Cast **lok** → ✅ the ring is **half as visible** as last time.

---

## Test 5 — Drain Vitality (no change, just confirm)

You asked for the caster to heal 100% of the damage done. **It already does** — I had read your
original note that way, so nothing changed. Worth one confirmation:

1. Wound the Dovahkiin lightly, cast **gaan** at enemies.
2. ✅ As victims take damage, the Dovahkiin's wounds close by the same amount.

If it looks like less than the damage dealt, tell me — that would mean a real bug rather than a
setting.
