# README-FIRST — how to use this bundle

Six files. Four are for the agent; two (this one and `KICKOFF.md`) are for you.

| File | Who reads it | What it does |
|---|---|---|
| **KICKOFF.md** | You paste it | The actual prompt. First message you send to Claude Code. |
| **CLAUDE.md** | Agent, automatically, every session | Standing rules. Claude Code auto-loads this file by name — don't rename it. |
| **SPEC.md** | Agent, on demand | Everything the mod does. The contract. |
| **ROADMAP.md** | Agent, on demand | Build order + the gates it isn't allowed to skip. |
| **MODLIST.md** | Agent, on demand | Your 39 mods + 3 DLCs, tagged by how much research each deserves. |

## Steps

1. Make an empty folder for the mod project.
2. Drop `CLAUDE.md`, `SPEC.md`, `ROADMAP.md`, `MODLIST.md` in it. Leave `README-FIRST.md` and
   `KICKOFF.md` out of it if you like — they aren't needed by the agent beyond the first paste.
3. Open Claude Code in that folder.
4. Open `KICKOFF.md`, copy everything below the `---`, paste it as your first message.
5. It will do reconnaissance and come back with `COMPAT.md`, `RISKS.md`, and `DECISIONS.md`.
   **Read `DECISIONS.md` carefully and answer it** — those eight questions shape the whole mod.
6. Approve, and it starts Phase 0.

## Two things that make or break this

**Give it the file paths.** It needs to read your actual `Mods/` folder and your RimWorld
install to get real defNames out of Dragon's Descent and RimWorld of Magic. Everything else in
the project rests on that. If it can't reach them, tell it where they are, or copy the relevant
mod folders somewhere it can read.

**Don't let it skip the gates.** The single most common way a project this size dies is the
agent building Phases 1–5 without you ever loading the game, then handing you a thousand files
that crash on startup with no way to bisect. If it offers to "keep going and test later,"
say no.

## Things I decided for you — override freely

These were underspecified in your brief, so `SPEC.md` picks a default and flags it. Skim
`SPEC.md §14 (Open Decisions)` before you start; it's ten questions, and the agent will ask
you about them anyway. **OD-9 and OD-10 block Phase 3 and Phase 2 respectively — those two
are the ones to read first.**

- **A second arrival route was added.** Your brief said a random pawn awakens only via the
  "A Dragon!!!" event, plus the stranger quest. To hit sanguophage parity (two routes, one a
  quest) `SPEC.md §8.7` adds a quiet wanderer-joins incident where the arrival already *is* a
  Dovahkiin. It is an arrival, not an awakening, so it doesn't touch your "only via A Dragon!!!"
  rule — but it is content you didn't ask for. Cut it if you'd rather the quest be the only
  outside route.

- **Souls do two things at once.** You said a soul gives +2 mana/stamina permanently, *and*
  that a soul is spent per shout level. Those conflict if it's one resource. So absorbing a
  dragon gives permanent attunement (the stat growth, never spent) *plus* one spendable soul
  token (for word levels). You get both curves and never feel punished for spending.
  The mana/stamina pool stays flat and linear per soul forever, as you asked; every *other*
  stat bonus uses a diminishing curve so the late-game Dovahkiin doesn't become a colony of one.
  **The "+2" number had to go, though** — RimWorld of Magic's mana and stamina are 0–1 `Need`
  bars, not integer pools, and they only exist on pawns that have one of its classes. So an
  ordinary colonist who awakens has no mana bar to enlarge. That's **OD-9**, and it's the one
  decision that most changes what the mod is; read it first.
- **The mod ships its own dragon and its own Alduin.** Otherwise everything — souls, the
  awakening event, mound guardians — is hostage to Dragon's Descent staying installed and
  unchanged. Dragon's Descent dragons still work and are still the scary ones.
- **The title isn't a Royalty title.** `RoyalTitleDef` drags in permit points, the Empire, and
  apparel/bedroom requirements. It's a custom name/bio display instead.
- **Heir lockout is softer than you wrote.** A dragonblood pawn only burns their one chance
  when there's no living Dovahkiin — otherwise they never had an opportunity to begin with.
  The harsher literal reading is OD-2 if you want it.
- **Shared shout cooldown**, not per-shout. More Skyrim-accurate and it stops shout-chaining.
- **Storm Call's lightning only ever targets hostiles** — that part is guaranteed. Fire spread
  afterwards isn't fully controllable in RimWorld, so the agent has to pick one of three
  documented answers (suppress ignition, exclude tiles near your stuff, or warn you). It is not
  allowed to quietly cut the shout over it.
- **The scenario's "start inside a hostile settlement" is expensive.** RimWorld always starts
  you on a fresh player-owned tile; there's no setting for this. The agent will offer you a
  full version (custom map generator, costly) and a reduced version (normal start, but hostiles
  and their buildings are already on your map, mid-assault). Reduced gets ~80% of the feeling
  for a fraction of the work.
- **Alduin at the burial site doesn't count** as his once-per-save appearance — he's a scripted
  flyover there, not a fight.
- **Alduin in the scenario is unkillable and scripted to leave.** Otherwise the hardest start in
  the game is also an unwinnable one.

## Rough expectation setting

This is a big mod — realistically a few hundred files and a real C# assembly. Phases 0–3 are
the spine and are very achievable. Phase 5 (crypt generation) is the hardest engineering and
Phase 8 (art) is the largest amount of non-code work; the agent can't draw, so budget for
placeholder art or commissioning. `ROADMAP.md` ends with a cut list ordered by what hurts least.
