# DECISIONS.md — the open questions, in plain language

Written for a player, not a programmer. Ten questions. **Seven I have answered myself** because
they are purely technical and have no effect on how the game feels. **Three need your taste** —
they change what you actually play.

---

# ANSWERED — 2026-07-25

| Question | Your answer |
|---|---|
| **Q1 / OD-9** — power source for shouts | **Own Thu'um bar.** Magic-mod bars grow as a bonus when present. |
| **Q2 / OD-10** — word-to-level rule | **Faithful (word N → level N), trimmed to ~10 shouts.** ~30 word walls. |
| **Q3 / OD-1** — death of the Dragonborn | **Slot reopens slowly.** Heirs of the dead one stay locked out. |

All three matched the recommendation. `SPEC.md` has been updated to match: §4.1 and §4.4
(shout list trimmed to ten plus Dragonrend), §5.2 (Thu'um resource), §3.2 and §8.1
("once per Dovahkiin slot"), and §14 (OD-1, OD-9, OD-10 marked answered).

The original question text is kept below for the record.

---

# Part A — three questions only you can answer

## Q1 (OD-9) — Should the Dragonborn have their own power bar?

**The situation.** Your brief said each dragon soul gives +2 mana and +2 stamina forever. Mana
and stamina come from the *RimWorld of Magic* mod. I checked how that mod works, and there's a
catch: **those bars only exist on pawns that have taken a magic or fighter class from that mod.**
An ordinary colonist who becomes Dragonborn has no mana bar at all — so the reward would
silently do nothing for most people who awaken.

I found the clean way to grow those bars when they *do* exist (it's a supported feature of that
mod, no hacks). The question is what happens when they don't.

| Option | What you'd see in game |
|---|---|
| **A — The Dragonborn gets their own "Thu'um" bar** | A new bar appears on the Dragonborn only. Shouts spend Thu'um. If the pawn also has magic-mod classes, souls grow those bars too, as a bonus. Works even if you uninstall the magic mod. |
| **B — Awakening also makes them a mage and a fighter** | Your Dragonborn automatically gains the magic mod's classes, so they get mana and stamina — but also spellbooks and abilities from that mod, whether you wanted them or not. Very powerful, less "Skyrim". |
| **C — Souls give no bar at all unless they already have a class** | Simplest. But it deletes something you asked for by name, and most Dragonborn get nothing from it. |

**My recommendation: A.** It's the only one where the Dragonborn always feels like a Dragonborn,
it keeps shouts working even without the magic mod, and it still honours your "+souls grow the
pool forever" idea. The cost is one extra bar on one pawn.

---

## Q2 (OD-10) — How much dungeon crawling should a full shout cost?

**The situation.** In Skyrim every shout has three words, and you find each word on a separate
word wall. Your brief said a word is learned at level 0 and each level costs one dragon soul, up
to three.

What was never pinned down: **do you need to find all three words to use a shout at full
strength?**

| Option | What it means for you |
|---|---|
| **A — Skyrim-faithful.** Word 1 → level 1, word 2 → level 2, word 3 → level 3 | Authentic. But the full shout list would need **63 word walls** to complete — 63 separate dungeon or mound expeditions. That is a *lot* of playing, and a lot of building on my side. |
| **B — One wall unlocks the shout; souls buy the levels** | **21 walls.** Far more achievable, still rewards exploration, but less Skyrim-accurate. |
| **C — Faithful, but far fewer shouts** | Keep the three-words rule, cut the shout list from 21 to about 10 strong ones. ~30 walls. |

**My recommendation: C.** Ten well-made shouts with real effects and real visuals beat
twenty-one thin ones, and it keeps the "three words, three levels" feel that makes it Skyrim.
The shout list already marks several as optional extras.

This is the single biggest lever on how long the mod takes to build.

---

## Q3 (OD-1) — If your Dragonborn dies, can a new one ever appear?

**The situation.** There is only ever one Dragonborn at a time. If yours is killed by a raid at
year three, is that it for the save?

| Option | What it means for you |
|---|---|
| **A — The slot reopens, slowly** | The colony mourns. After several days the world "forgets", and the rare dragon event can fire one more time. Children of the old Dragonborn stay locked out — their chance already passed. |
| **B — Gone forever** | One Dragonborn per save, full stop. Brutal, high stakes, and a stray bullet can end the entire storyline permanently. |
| **C — The heirs inherit** | The slot reopens immediately, but only the dead Dragonborn's dragonblood children can claim it. |

**My recommendation: A.** B is faithful to "one per save" but risks a 40-hour save losing its
whole plot to bad luck, with no path back. A keeps the loss painful without making it
unrecoverable.

---

# Part B — seven questions I answered myself

These are technical or minor. I've picked the option and moved on; say the word if you disagree.

**OD-2 — When do dragonblood children burn their one chance?**

> **~~Decided: only when there is no living Dragonborn.~~ OVERTURNED BY THE USER, 2026-08-01.**
> This was one of the seven I answered myself, and the user has now taken it back. **Their ruling
> stands; do not restore the reasoning below.**

**The rule is now: PRESENCE burns the roll.** *"If a dovahkiin is alive, everybody present and
was present during his time burns their roll."* Not on a dragon death — on having lived alongside
him at all. Once he dies, only a heir who appears **after** that carries a live roll, spendable on
any dragon death they witness. **Ordinary pawns never roll at all** — the §3.2 dragon event,
arrival, or a scenario are their only routes. Full text and implementation consequences in
`SPEC.md §3.3`.

*Why my original answer was wrong, kept because the mistake is instructive:* I argued the harsh
reading "quietly kills the whole heir storyline in the background without the player ever seeing
it happen". That is true of the version I was imagining — heirs burning rolls on dragon deaths
they had no stake in. It is **not** true of what the user actually wants, which is narrower and
has a clear fiction behind it: *the universe already had its Dragonborn while you stood next to
him.* The succession still works, it just runs through pawns who arrive after the old hero is
gone rather than the ones who grew up beside him.

**I answered a design question by predicting how it would feel, and predicted wrong.** The
questions in Part B are the ones I judged safe to settle alone; this is the evidence that "minor"
and "technical" are not the same thing, and that a rule about *fiction* is never mine to close.

⚠ **See also the OD-1 conflict flagged in `SPEC.md §3.3`** — the same message described the dragon
event as "once per save", where OD-1 and the shipped code say once per Dovahkiin *slot*. Unresolved
on purpose.

**OD-3 — What makes Alduin stay dead?**
*Decided: the killing blow must come from the Dragonborn.* Cleaner to read and to explain than
"absorb his soul afterwards", and it makes the final fight about the Dragonborn personally.

**OD-4 — What if a dragon dies with no Dragonborn around?**
*Decided: the soul is lost, with a short message.* A soul lying on the ground waiting to be
collected invites save-scumming and clutters the map.

**OD-5 — Dragonrend (the shout that grounds a dragon).**
*Decided: implement it as a real shout, locked to the final quest.* It's the emotional centre of
the Alduin fight in Skyrim. Gating it there means it can't unbalance the mid-game.

**OD-6 — What if the magic mod is removed mid-save?**
*Decided: the Dragonborn keeps everything.* Under Q1 option A the Thu'um bar is ours, so nothing
breaks — the magic-mod bonuses simply stop applying. This is another reason to prefer A.

**OD-7 — Multiplayer.**
*Decided: single player only.* Multiplayer support would roughly double the work for a feature
you haven't asked for.

**OD-8 — Can a second Dragonborn re-learn words the first one found?**
*Decided: yes — the words stay discovered, but the levels must be bought again with souls.*
Finding a wall twice is tedious; re-earning the power is not.

---

# What happens next

Once Q1–Q3 are answered I start **Phase 0**: the skeleton of the mod. Nothing interesting yet —
a folder structure, the mod's name card so RimWorld can see it, and one small visible thing to
prove it loads cleanly alongside your other 39 mods. Then I hand you a short list of clicks to
confirm it works before anything else gets built.

The good news from reconnaissance: **the hardest part of the project got much easier.** The big
Nordic crypts were expected to need a custom dungeon generator written from scratch. It turns
out one of your installed mods (Vanilla Expanded Framework) already includes a tool that lets a
dungeon be *built by hand in-game and saved as a template* — and Dragon's Descent already uses
it exactly that way. Full details in `RISKS.md`.
