# Dovahkiin - RimWorld 1.4

Skyrim's Last Dragonborn, for RimWorld 1.4.3682. In development.

## Resuming work on this project?

Read the save notebook first:
**`C:\Users\User\Documents\SaveNotebooks\Dovahkiin-RimWorld-Mod.md`**

It carries the current state, what is built, what is next, and every RimWorld 1.4 gotcha this
project has already paid for in playtest time. Then **[CHANGELOG.md](CHANGELOG.md)** for the
full history.

## Where things live

There are **three** locations, on purpose:

| Folder | What's in it |
|---|---|
| `Mods\Dovahkiin\` - *this one* | **The mod itself.** RimWorld loads it from here. |
| `RimWorldFolder\DovahkiinClaudePluged\` | **The design documents.** `CLAUDE.md`, `SPEC.md`, `ROADMAP.md`, `MODLIST.md`, `COMPAT.md`, `RISKS.md`, `DECISIONS.md`. |
| `Documents\SaveNotebooks\` | **The save notebook**, alongside every other project's. |

Nothing is duplicated between them, so there is nothing to keep in sync.

## Current state - Phase 2b complete

Six of eleven core shouts built, playtested and balanced: Unrelenting Force, Fire Breath,
Frost Breath, Clear Skies, Whirlwind Sprint, Marked for Death. The identity systems (registry,
trait, title, backstories, dragonblood, death handling) are complete and verified.

**Next:** Slow Time and Become Ethereal, then Storm Call, Soul Tear and Dragon Aspect.

## Building

```
dotnet build Source/Dovahkiin/Dovahkiin.csproj -c Release
```

Output lands in `Assemblies/Dovahkiin.dll`. If you ever move your RimWorld install, change the
single path in `Source/Dovahkiin/RimWorldPath.props` and nothing else.

## Requirements

- RimWorld **1.4** (developed against 1.4.3682 rev778)
- **Harmony** and **HugsLib** - required
- Royalty / Ideology / Biotech - optional, all supported
- **Dragon's Descent** - optional. Its eleven dragons become soul sources when present; the mod
  ships its own dragon so the loop works without it.
- **Vanilla Expanded Framework** - optional, but required for the large Nordic crypts
- **RimWorld of Magic** - optional. Souls grow its mana and stamina pools when present.
