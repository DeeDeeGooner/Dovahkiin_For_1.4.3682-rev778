# CLAUDE.md — Dovahkiin (RimWorld 1.4)

> ## START HERE IF YOU ARE A FRESH SESSION
>
> Read the save notebook before anything else:
> **`C:\Users\User\Documents\SaveNotebooks\Dovahkiin-RimWorld-Mod.md`**
>
> It states the current phase, what is built, what is next, the RimWorld 1.4 gotchas this
> project has already paid for, and how the playtest loop with this user works. It lives in the
> central library with every other project's notebook — **not** in this repo, and there is no
> second copy anywhere.
>
> Then read **`Mods\Dovahkiin\CHANGELOG.md`** — every bug, its real root cause, and why each fix
> was chosen. Several bugs in this project looked identical from the outside but had unrelated
> causes; the changelog is what stops you re-treading them.

Persistent rules for this repository. Read `SPEC.md` for *what* to build, `ROADMAP.md` for
*when*, `MODLIST.md` for *what it must coexist with*, `COMPAT.md` for *facts you verified*,
`RISKS.md` for *what is hard*, `DECISIONS.md` for *what has been settled*.

---

## The notebook — mandatory, every session

**`C:\Users\User\Documents\SaveNotebooks\Dovahkiin-RimWorld-Mod.md`** is the project's memory
across conversations. Chat history is lost; that file is not. **Keeping it current is not
optional and is not a chore to do "later".**

It lives in the central notebook library with every other project's, **not in this repo**, and
there is exactly one copy. `Mods\Dovahkiin\NOTEBOOK.md` is a one-line signpost pointing at it —
a signpost, never a duplicate. There is no `HANDOFF.md`; earlier revisions of this file named
one, and nothing of that name has ever existed on disk.

**Update the notebook whenever any of these happen — in the same turn, not at the end:**

- a phase or sub-phase completes, or a playtest passes
- a feature is added, cut, or deferred
- **a RimWorld 1.4 gotcha is discovered** — add it to §5. This is the highest-value section in
  the project; every entry cost a playtest round to learn
- an architectural decision is made or reversed
- the "what's next" list changes

**Also update `CHANGELOG.md` every time work lands.** Record the *root cause* and *why this fix
over the alternatives*, not just what changed. Several bugs here looked identical from the
outside and had unrelated causes; that reasoning is what stops them being re-tread.

### `/SAVE_66/` — the handoff command

When the user types **`/SAVE_66/`**, they are about to start a fresh conversation. Do this,
in order, without asking:

1. **Bring `HANDOFF.md` fully up to date** — current phase, what is built, what is next, any new
   gotchas, anything deferred. Do not skip this because it "looks recent".
2. **Bring `CHANGELOG.md` up to date** for any work not yet recorded.
3. **Send `HANDOFF.md` to the user** with `SendUserFile` so they can hand it to the new chat.
4. Reply with a **short** summary: current state in a few lines, and the single most useful
   thing the next session should know. No wall of text — the file carries the detail.

The user is not a programmer. `/SAVE_66/` is their save button; treat it as one.

## Where things live

| Path | Contents |
|---|---|
| `RimWorldFolder\DovahkiinClaudePluged\` — **this folder** | The design documents. No code. |
| `RimWorldFolder\Mods\Dovahkiin\` | **The mod.** RimWorld loads it from here. Source, defs, assembly, `CHANGELOG.md`, `TESTS/`. |

Nothing is duplicated between them. Build commands below run from the **mod** folder.

## Target environment — non-negotiable

| Thing | Value |
|---|---|
| Game version | RimWorld **1.4.3682 rev778** — but **read the real build off the install** in Step 1 and record it in `COMPAT.md`. 1.4.3682 is Mar 2023; final 1.4 is **1.4.3901** (Nov 2023). A Steam install left to auto-update will be on 3901. Build against whatever is actually there and tell me if it differs. |
| DLCs present | Royalty, Ideology, Biotech (**no Anomaly**) |
| C# target | .NET Framework **4.7.2**, C# 7.3-safe syntax |
| Harmony | 2.x, via the Harmony mod already in the list. **Hard reference permitted.** |
| HugsLib | Present — use it for settings/logging, do not reimplement. **Hard reference permitted.** |
| Mod id | `erzou.dovahkiin` |

**Baseline environment** = Core + Royalty + Ideology + Harmony + HugsLib. That is the minimum
the mod must load and run on. **Biotech is present but optional at runtime**, per `SPEC.md §10`
("the mod must not require Biotech at runtime") and `ROADMAP.md` universal exit criterion 5.
Everything else in `MODLIST.md` is optional at runtime.

Note when testing criterion 5: five mods in `MODLIST.md` (`XenotypeCharmweaverResplice`,
`XenotypeFaun`, `XenotypeLycan`, `XenotypeNephilim`, `XenotypeSatyr`) hard-require Biotech, so
"Biotech disabled" is a baseline-environment pass, never a full-modlist pass.

## Hard invariants

1. **At most one Dovahkiin exists per save at any time.** All awakening, transfer, and death
   handling goes through `GameComponent_DragonbornRegistry`. No other code may add the trait.
2. **At most one *boss* Alduin exists per save.** Same registry owns him. Scripted,
   non-combatant Alduin cameos (the burial-site flyover, the scenario opening) use a **separate
   `Alduin_Scripted` ThingDef** that is exempt from this invariant and can never be the boss,
   never drops a soul, and never sets `SlainForever`.
3. The Dovahkiin is **not a xenotype, not a race, not a gene**. It is a trait + hediff +
   title on an otherwise ordinary pawn of any race or xenotype.
4. **Power comes from souls and words, never from biology.** No body-size, no race swap,
   no raw HP inflation, no unconditional melee-damage multiplier. Growth goes into mana,
   stamina, technique-flavoured stats, and shout access. See `SPEC.md §5`.
   **Explicit carve-out:** *Akatosh's Child* (`SPEC.md §5.3`) is a conditional damage and
   mitigation multiplier that applies **only in interactions with dragons**. It is intended,
   it is capped, and it does not violate this invariant.
5. The mod loads and plays on the **baseline environment above with no other mods**.
   Dragon-dependent content is dormant, not broken — with one exception: the mod ships its
   **own** Alduin and its own fallback dragon (`SPEC.md §12`), so the core loop is never empty.
   Hard-referencing Harmony and HugsLib is fine; hard-referencing anything else is not.
6. Nothing the player unlocks is lost by loading a save. All state in `ExposeData`.

## Project jargon — "QUESTLINE"

**A QUESTLINE is a TRAIN OF QUESTS: the next becomes available only after the previous one is
completed.** The user coined the word on 2026-08-01 and intends to add several. It is not a
synonym for "quest" and not a one-off side quest — when anything in this project says
*questline*, it means that structure.

**All three quest-locked shouts are earned at or near the END of a questline, never from a random
quest drop.** Call of Valor and Call Odahviing come from **the main questline**; Summon Durnehviir
from the Dawnguard-inspired vampire war. **Read `SPEC.md §15` before touching any of it** — it
carries the three questlines, the realm-travel answer, the Vampire Lord verdict, and two
constraints that will bite (Soul Tear must leave the word walls; sanguophages are Biotech and
invariant 5 says the mod runs without it).

Confirmed feasible: `QuestPart_SubquestGenerator` with `maxActiveSubquests = 1` is exactly this
behaviour, and the chain's progress is stored by the game rather than by us.

## Tools installed on this machine — use them

**A DECOMPILER IS INSTALLED. Read RimWorld's real C# instead of guessing at it.**

```powershell
& "$env:USERPROFILE\.dotnet\tools\ilspycmd.exe" -t Verse.PawnRenderer `
  "C:\Games\Rimworld\RimWorld\RimWorldFolder\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" `
  | Out-File "$scratch\PawnRenderer.cs" -Encoding utf8
```

One `-t Type.Name` per call, seconds each, and it gives **property bodies and the order of
operations** — not just member lists. Installed 2026-08-01.

**This is the first thing to reach for when you need to know how vanilla does something.** The
reflection recipe in the notebook still has its uses (is this member public, scanning IL for
callers) but it dies with a `StackOverflowException` on every run and can never show you code.
That limit is what produced two wrong weapon hold angles in a row: it could show *that* an angle
was used, never *how*.

If it is ever missing, reinstall **pinned** — unpinned fails on this machine's SDK with a
misleading *"DotnetToolSettings.xml is not found in the package"*:
`dotnet tool install -g ilspycmd --version 8.2.0.7535`

**AND YOU CAN SEE THE RUNNING GAME. `Tools/CaptureGame.ps1`.**

```powershell
$env:DOVAH_PREVIEW = "<scratch folder>"
& "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Tools\CaptureGame.ps1"
```

Captures the RimWorld window to `game_capture.png`, which you then read like any other image.
**Ask for it whenever a report is visual** — nearly every defect in this project has been, and
each one has cost at least one round of guessing at what the words meant. The sword hold angle
took three. "Show me" beats "describe it".

Exit 2 means RimWorld is not running. **It captures the RimWorld window ONLY and never falls
back to the whole desktop** — that is deliberate and must stay that way; the user's screen is
their private business.

## Anti-patterns — do not do these

- Do not use any API introduced after 1.4 (`LayoutDef`, Anomaly types, 1.5 ability rework).
  If a doc page or a memory says an API exists, verify it in the 1.4 `Assembly-CSharp.dll` first.
- Do not `AddReference` to another mod's DLL **other than Harmony and HugsLib**. Everything
  else — RimWorld of Magic, Dragon's Descent, VEF, JecsTools — is reflection + null-guard only.
  If you conclude the shout system genuinely needs a hard dependency on VEF or JecsTools,
  **stop and make that case to me before Phase 2**; do not decide it silently.
- Do not guess another mod's defNames. Read them on disk. Unverified names get `TODO(COMPAT)`.
- Do not run logic in `Pawn.Tick` or `Thing.Tick` for anything that could live in
  `TickRare`/`TickLong`, an event hook, or a cached lookup. RocketMan is installed.
- Do not overwrite vanilla or third-party defs. Patch with `PatchOperation` and `MayRequire`.
- Do not implement "temporary" balance numbers inline. All tuning numbers go in
  `Defs/DovahkiinTuningDef.xml` or mod settings so I can retune without a rebuild.
- Do not ship placeholder art silently. Missing textures get a magenta placeholder and an
  entry in `ART_TODO.md`.
- Do not mark a phase complete with red errors in the dev-mode log. Yellow warnings get
  explained in writing.

## Workflow

- **One phase at a time**, per `ROADMAP.md`. Each phase ends with a build, a load test against
  the full modlist, and a written click-by-click test script for me.
- **Ask before assuming.** Ambiguity in `SPEC.md` is a question, not a coin flip.
- Update `COMPAT.md` the moment you learn a real fact about another mod.
- Update `CHANGELOG.md` every phase.
- Commit per logical unit with a message naming the SPEC section.

## Build

```
# from repo root
dotnet build Source/Dovahkiin/Dovahkiin.csproj -c Release
# output must land in ./Assemblies/Dovahkiin.dll
#   — unless Phase 0 adopts the 1.4/ + LoadFolders.xml layout, in which case it is
#     1.4/Assemblies/Dovahkiin.dll. Pick one in Phase 0 and correct this line.
```

`Dovahkiin.csproj` needs a `Microsoft.NETFramework.ReferenceAssemblies` `PackageReference` —
`dotnet build` cannot target `net472` without it on a machine lacking Visual Studio's .NET
Framework targeting packs.

Reference DLLs are resolved from the local RimWorld install; keep the path in a
`.csproj.user` or a `RimWorldPath.props` that is gitignored. Never commit game DLLs.

## Definition of done for any feature

1. Builds clean, no warnings you cannot justify.
2. Loads with the full modlist, dev-mode log clean.
3. Survives save → quit → load with state intact.
4. Degrades gracefully when its dependency mods are absent.
5. Has a test script in `TESTS/phaseN.md`.
6. Numbers exposed for tuning, not hardcoded.
