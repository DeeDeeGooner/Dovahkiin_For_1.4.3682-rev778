# Phase 0 test — "does it load?"

Nothing in the game changes yet. This checks one thing only: **the mod is real, RimWorld sees
it, and it loads cleanly next to your other 39 mods.** Takes about five minutes.

If any step goes wrong, stop and tell me what you saw — don't try to fix it.

---

## 1. Turn the mod on

1. Launch RimWorld.
2. Click **Mods**.
3. In the left-hand list, find **Dovahkiin**. It should already be there — I put it in your
   `Mods` folder.
   - ❌ *Not in the list?* Stop. Tell me.
4. Click it to move it to the active list on the right.
5. Look at the bottom of the screen. If RimWorld shows a red warning about load order, click
   **Sort** (or **Auto-sort**) to fix it automatically.
6. Drag **Dovahkiin** so it sits **below** Rimedieval and **above** RocketMan.
   RimWorld may have already done this.
7. Click **Close**. RimWorld will ask to restart — say yes.

---

## 2. Turn on developer mode

This is how we read the log. You can turn it off again later.

1. After the restart, click **Options**.
2. Tick **Development mode**.
3. Close Options.

---

## 3. Read the log — the important step

1. Press the **`~`** key (top-left of the keyboard, above Tab). A debug log window opens.
   - If `~` does nothing, the button row at the very top of the screen has a **Log** icon.
2. Look for a line like this, in white:

   ```
   [Dovahkiin] Loaded. Tuning def OK (heir awaken chance 2.0 %, Thu'um per soul 2). Phase 0.
   ```

   **That line is the whole test.** It proves three separate things worked: the mod loaded, its
   code ran, and its settings file connected to its code correctly.

3. Now scroll the log and check for **red** lines mentioning `Dovahkiin`.
   - ✅ **No red Dovahkiin lines** = pass.
   - ⚠️ Red lines from *other* mods are not our problem right now — ignore anything that
     doesn't say Dovahkiin.
   - ❌ **Any red line mentioning Dovahkiin** = fail. Copy it and send it to me.

4. Yellow/orange warnings mentioning Dovahkiin: copy those too. They're not failures, but I
   need to explain each one in writing.

---

## 4. Check the settings panel

1. **Options** → **Mod Settings** → **Dovahkiin**.
2. You should see one checkbox: **Verbose logging**, unticked.
   - ✅ It's there = pass. Leave it unticked.
   - ❌ No Dovahkiin entry in Mod Settings = fail. Tell me.

---

## 5. Check it survives a save

1. Start a new colony — any scenario, any map, smallest size is fine and fastest.
2. Once you're on the map, save the game.
3. Quit to the main menu, then load that save.
4. Press `~` again and check for new red Dovahkiin lines.
   - ✅ None = pass.

---

## What "pass" means

All five steps clean means the foundation is sound: the folder layout is right, the code
compiles and runs on your actual game build, the settings system works, and nothing collides
with your other mods. That's the whole point of Phase 0 — catching a broken pipeline now,
instead of after a thousand files have been built on top of it.

**Tell me the result and I'll start Phase 1** (the Dragonborn identity itself: the trait, the
title, the one-per-save rule, dragonblood children, and the debug buttons that let us test
everything afterwards without waiting for rare events).

---

## Optional — the harder check

Only if you're willing. This is exit criterion 4 and 5 from `ROADMAP.md`.

**Does it work with almost nothing else installed?**
In the Mods menu, disable everything except: Core, Royalty, Ideology, Harmony, HugsLib,
Dovahkiin. Restart. Check the log again for red Dovahkiin lines. Then re-enable everything.

If that's a hassle, skip it — I'll ask again when it actually matters, at Phase 3.
