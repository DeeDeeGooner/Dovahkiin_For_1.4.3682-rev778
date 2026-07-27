# Phase 1 test — the Dragonborn identity

Now there's something to see. This checks that a colonist can become the Dovahkiin, that the
game refuses to make a second one, that children inherit the blood, and that all of it survives
saving and loading.

**You'll need developer mode on this time** — it's how the test buttons appear.

Takes about fifteen minutes. Go in order; later steps build on earlier ones.

---

## Setup

1. Launch RimWorld. **Options → tick Development mode.** Close Options.
2. Start a **new colony**. Any scenario, any map — the smallest map size is fastest.
   Three starting colonists is ideal.
3. Once you're on the map, find the **debug tools** button in the row of icons at the top of the
   screen — it looks like a small wrench or bug. Click it, then choose **Actions**.
4. Scroll to the **Dovahkiin** section. You should see about nine entries.
   - ❌ *No Dovahkiin section?* Stop and tell me.

---

## Test 1 — Awaken someone

1. Debug tools → **Dovahkiin → Force awaken pawn**.
2. Pick a colonist from the list.
3. ✅ A green message says *"[name] is the Dovahkiin."*
4. Click that colonist, open the **Bio** tab (the "i" / character tab).
   - ✅ Under their name it says **Dovahkiin**.
   - ✅ In their traits list there's a **Dovahkiin** trait. Hover it — the description mentions
     dragons hating them on sight.
5. Open their **Health** tab.
   - ✅ Two entries: **dragon soul attunement** and **the Voice**.
   - Both should look harmless — no bleeding, no pain, nothing red.

**What you should NOT see:** any change to their stats or combat ability. That's deliberate —
awakening is a social event, not a power-up. The power comes from dragon souls later.

---

## Test 2 — The one-at-a-time rule

This is the mod's single most important rule, so test it properly.

1. Debug tools → **Dovahkiin → Force awaken pawn**.
2. Pick a **different** colonist.
3. ✅ It **refuses**, with an orange message saying *"Could not awaken [name]: a Dovahkiin
   already exists ([first name])."*
   - ❌ If a second pawn becomes Dovahkiin, that's a serious failure — stop and tell me.

---

## Test 3 — Souls (the counter, not the powers yet)

1. Debug tools → **Dovahkiin → Grant 10 souls**.
2. ✅ Message: *"[name]: +10 soul(s). Unspent 10, attunement 10."*
3. On the Dovahkiin's **Health** tab, hover **dragon soul attunement**.
   - ✅ The severity has gone up.
4. Hover **the Voice** — ✅ the tooltip says *"Unspent dragon souls: 10"*.

Two numbers, tracked separately on purpose: attunement is permanent and never spent, souls get
spent on shouts later.

---

## Test 4 — Dragonblood children

1. Debug tools → **Dovahkiin → Grant Dragonblood to pawn**. Pick any *other* colonist.
2. ✅ Message confirms it. Check their Bio tab — ✅ a **dragonblood** trait, and their stats show
   small bonuses to social impact, learning and shooting accuracy.

*(Real inheritance happens when a baby is born to a Dovahkiin or a dragonblood parent. That needs
Biotech and a pregnancy, which is too slow for this test — the button proves the trait itself
works, and I'll verify the birth path in a later phase.)*

---

## Test 5 — Save and load (the one that catches real bugs)

1. **Save** the game. Name it something you'll recognise.
2. **Quit to main menu**, then **load** that save.
3. Check the Dovahkiin again:
   - ✅ Still says **Dovahkiin** under their name.
   - ✅ Still has both health entries.
   - ✅ **the Voice** still says *Unspent dragon souls: 10*.
4. Debug tools → **Dovahkiin → Registry status**.
5. Press **`~`** to open the log. ✅ You should see a block like:

   ```
   === Dovahkiin registry ===
   Dovahkiin:            [name]
   Ever existed:         True
   Deaths:               0
   Slot open:            False
   Awakening event fired:0  (may fire again: False)
   Alduin state:         Unspawned
   Dragonblood pawns:    1 (0 locked out)
   ```

---

## Test 6 — Death, and your "slot reopens slowly" rule

1. Debug tools → **Dovahkiin → Kill Dovahkiin (test OD-1)**.
2. ✅ The pawn dies. Message: *"Killed. The slot reopens after the grieving delay."*
3. Immediately try **Force awaken pawn** on someone else.
   - ✅ It **refuses**: *"the slot is still closed after a death (OD-1 grieving delay)."*
   - This is your Q3 answer working — the world doesn't hand out a replacement instantly.
4. Run **Registry status** again and check the log:
   - ✅ `Deaths: 1`, `Slot open: False`.
5. *(Optional, if you want to see it reopen:)* set game speed to fastest and let **8 days**
   pass, then try awakening someone again — ✅ it should now succeed.

---

## Test 7 — The log is clean

1. Press **`~`**.
2. Look for **red** lines mentioning `Dovahkiin`.
   - ✅ None = pass.
   - ❌ Any = copy them to me.
3. Orange/yellow warnings mentioning Dovahkiin: copy those too.

---

## Reporting back

Tell me which tests passed. If something failed, tell me **which test number** and what you saw
instead — that's enough for me to find it.

If everything passes, Phase 1 is done and we move to **Phase 2: the Voice** — the actual shouts,
the Thu'um bar, cooldowns, and the first three working shouts with real effects and visuals.
