# Phase 2e test — the polish round

Everything you asked for after the last test. **Dismay is unchanged** — you signed it off.

**Restart RimWorld** (the assembly changed). Your existing save is fine.
**Play at normal speed, not 3x.**

About ten minutes.

---

## Test 1 — Slow Time now slows the world

This is the big one, and it is the design decision you left to me. Short version: **RimWorld
cannot run below normal speed at all** — there is no such setting, and forcing one means
fighting RocketMan over the game's tick loop. So instead of slowing the clock, **everyone else
gets slowed** and you don't. Same picture, none of the risk. Longer reasoning at the bottom.

1. Put a few colonists and a few raiders near the Dovahkiin.
2. Cast **tiid**.
   - ✅ A **near-white, very transparent** ring sweeps outward — bigger and slightly faster
     than last time.
   - ✅ The Dovahkiin speeds up as before, and it now lasts **15 seconds** at one word.
   - ✅ **Everyone else nearby gets a "slowed" entry** in their Health tab and visibly crawls.
   - ✅ **Allies are slowed too.** That is deliberate — the effect is about relative speed.
3. Check that nothing turned hostile.
   - ✅ No faction reacts, no ally gets angry, no "attacked by" message. It applies a condition
     and nothing else — it is not registered as an attack in any way.

**Tell me:** is slowing allies too annoying in practice? Making it enemies-only is one line.

---

## Test 2 — Cyclone is a tornado now

I built the wrong thing last time — a spiral fanning outward instead of a vortex travelling.

1. Cast **ven** at a group.
   - ✅ It is a **compact spinning column that travels toward the target**, not a spreading
     cone or spiral.
   - ✅ It visibly **rotates** as it crosses the ground.
   - ✅ Still very faint grey.
2. ✅ Damage now lands on **fewer body parts** (halved again), same small total.
3. ✅ Stun unchanged — about 2.5s at one word, 7.5s at three.

---

## Test 3 — Become Ethereal, now see-through

1. Cast **feim**.
   - ✅ The Dovahkiin renders **semi-transparent** for the duration.
   - ✅ Now lasts **12 seconds** at one word.
2. **The thing to judge:** this uses RimWorld's own invisibility system rather than a custom
   render hack — which is why it should behave with your other mods rather than fighting them.
   The side effect is that **enemies also lose track of you** while it lasts.
   - Is that good (you *are* out of the world) or does it make the shout too strong?
   - If you dislike it, I delete one line and the invulnerability stays exactly as it is.

---

## Test 4 — Drain Vitality steals life

1. Wound the Dovahkiin first — a bruise or two, nothing serious.
2. Cast **gaan** at some enemies.
3. ✅ Victims take steady damage over time now (from level 1, not just level 3).
4. ✅ **The Dovahkiin's own wounds visibly close** as the drain ticks.
   - Watch the Health tab. It heals the worst injury first.
5. ✅ It **stops** after a while — the drain is capped, as Marked for Death is.

It deals **half** of Marked for Death's damage and carries **no armour penalty**, so the two
shouts stay distinct.

---

## Test 5 — The colours and the bar

1. Cast **krii** (Marked for Death) and **gaan** (Drain Vitality) one after the other.
   - ✅ Marked for Death is now **blue-grey, leaning grey** — no violet left in it.
   - ✅ Drain Vitality is the only **deep purple** one. They no longer look like the same shout.
2. Cast **lok** (Clear Skies) → ✅ the ring is **smaller** than last time and a bit **bluer**.
3. Open the Dovahkiin's **Needs** tab and look at the **thu'um** bar.
   - ✅ It is now a **50/50 split** — violet lower half, ember upper half — rather than one flat
     colour that only changed as it drained.
   - Spend some Thu'um and watch it: ✅ both halves still cool toward violet as it empties.

---

## Why Slow Time doesn't actually slow time

You asked whether we could run the game at 0.5x with the Dovahkiin exempt. I checked the game's
code rather than guessing, and it isn't a good idea:

- RimWorld's speed setting has **only** Paused, Normal, Fast, Superfast, Ultrafast. There is no
  half-speed to select — it would have to be forced into the game's core timing loop.
- **RocketMan**, which you have installed, exists specifically to manage that timing loop. Two
  mods pulling on it is the worst possible place for a conflict.
- It would slow **everything** — your whole colony, every caravan, the world map — not just the
  fight you're in.
- If it ever failed to switch back (a crash, a save mid-shout), your game would be stuck at half
  speed permanently.
- And it wouldn't even save effort: making the Dovahkiin the exception means speeding them back
  up by 2x, which is the haste that already exists. It's the current shout **plus** a risky
  global change.

So: everyone near you crawls, you don't. It looks the same on screen and cannot break your save.
If it doesn't *feel* right in play, tell me — the slow strength and radius are both easy numbers.
