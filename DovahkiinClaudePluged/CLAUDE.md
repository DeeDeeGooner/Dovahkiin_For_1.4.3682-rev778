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
carries the three questlines, the realm-travel answer and the Vampire Lord verdict.

**The vampire questline is `MayRequire`-gated on Biotech** (user's call, 2026-08-01) and silently
absent without it. **Invariant 5 below is UNCHANGED** — the mod still loads and plays on the
baseline; only that questline disappears. A *global* Biotech requirement was considered and
rejected as all cost and no gain, since nothing else in the design needs it.

**Soul Tear is the design's first CONDITIONAL word**: a questline reward when Biotech is loaded,
an ordinary word wall when it is not. **So the word-wall count is install-dependent — 42 without
Biotech, 39 with it — and §4.4's Phase 7 re-cost must be done against the LARGER number.**

**TWO vampire xenotypes — `Vampire` and `Vampire Lord`** (§15.7), both acquired, never born into.
Orange glowing eyes, pale skin, night vision, frost resistance, fire weakness, and raised body
function — **all the body stats are more pronounced on a Lord EXCEPT fire and frost, which are
flat across both tiers on purpose.**

**Precedence: `VAMPIRE LORD > VAMPIRE > SANGUOPHAGE`** — *"supernatural beats the natural."* A
pawn is at most one of the three. Lords convert both and are converted by nobody; vampires convert
only sanguophages; **sanguophages convert NOBODY**, which deliberately strips a vanilla mechanic
and must be enforced with an explicit refusal, not a silent no-op. Both vampire tiers get a
`blooddrain` active ability — **its design is deferred by the user; do not invent its numbers.**

**The war is three-sided** (§15.2): the human/Divine Order faction, **several** ordinary vampire
factions (rogue and organised), and **exactly one** Vampire Lord faction — the Volkihar court.
The uniqueness rule applies to the court ONLY.

**Vampires are a BIOTECH XENOTYPE of custom genes** (§15.6) — not a race, and **not** an overlay.
Genes change the pawn for real: `skinColorOverride` for pale skin, `drawOnEyes` for the glowing
eyes (Biotech's own `GeneEyeColor` uses it), hemogen and `Bloodfeed` reused as-is. Only two pieces
need writing — the sunlight burn and the feeding madness. **The ordinary vampire is cheap; only
the Vampire Lord's winged form hits the art ceiling — do not conflate the two.**

**⚠ READ THE WHOLE MESSAGE BEFORE ANSWERING ITS FIRST CLAUSE. This user puts the operative
instruction in the PARENTHETICAL**, and it has produced two wrong answers in one day: *"make
biotech a hard requirement (…no dawnguard quest…)"* meant gate the content, not the mod; *"a new
race (with sanguophages's genetic overwritte active skill)"* meant the xenotype mechanism, not a
race. §15.6 has both.

Confirmed feasible: `QuestPart_SubquestGenerator` with `maxActiveSubquests = 1` is exactly this
behaviour, and the chain's progress is stored by the game rather than by us.

## ⚠ AFTER EVERY PLAYTEST, READ THE LOG. NOT WHEN ASKED — EVERY TIME.

**The user's standing instruction, 2026-08-06: "make sure that from now on every session always
reads the logs whenever a test is done."** It is not optional and it is not a last resort.

```
C:\Users\User\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

**⚠ ALSO READ `Player-prev.log` IN THE SAME FOLDER.** RimWorld **truncates `Player.log` on every
launch**, so if the user relaunched before reporting, the evidence is in `-prev`. Its creation
date is useless — the file is truncated, not recreated.

Grep for `[Dovahkiin]`, `Config error`, `Exception`, and any diagnostic prefix currently in the
build. **Do this BEFORE forming a theory**, not after one fails.

**The record on why:** this rule has been in §5 of the notebook since 2026-07-30 and was skipped
repeatedly anyway, because each new theory felt like progress. It has cost, at minimum:

- three wrong fixes for "motionless in flight" before one `HOVER-DIAG` line settled it in a single
  playtest
- a wrong A/B/C design decision put to the user over invisibility, when six config errors naming a
  body part group had been sitting in the log the whole time
- two rounds on flight stops, answered instantly once the log showed `job=Wait_Wander`
- a red `Config error` on a new FactionDef that had been firing at every load

**A plausible cause is not evidence.** Instrument on the SECOND failure at the latest, and read
the log on the FIRST.

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

## CREATURE ART — READ `Tools/DRAGON_ART_PIPELINE.md` BEFORE TOUCHING A SPRITE

That file is the operating manual and it lives beside the scripts. Headlines only here:

**THE ROUTE: generate a flat reference with `Tools/GEMINI_CREATURE_PROMPT.md` → trace it with
`Tools/TraceRef.ps1` → LOOK AT THE MASK → build with `Tools/BuildFromMask.ps1`.** Four
references made with that prompt traced perfectly first time; four made without it cost
multiple rounds each. **The prompt is the artefact — do not paraphrase or shorten it.**

**THE ONE-LINE LESSON, which cost most of 2026-08-03: YOU CANNOT SUBTRACT YOUR WAY TO RIMWORLD
SIMPLICITY.** Mean blur, median filter, fewer tone levels and area-opening were each tried on a
detailed reference; every one destroys structure and noise together and the result is mush.
RimWorld art is **drawn simple from the start**, so the fix is always upstream in the prompt,
never downstream in a filter.

**Hand-drawing a creature does not work either** — eight attempts, all failing the same way:
style right, proportions wrong. `GenerateAlduinHead.ps1` and `GenerateDovahEast.ps1` are kept
**marked as rejected**; do not build on them.

**Flight rotates, ground never does.** From directly overhead you see a creature's back
whichever way it flies, so all four flight facings come from one sprite
(`Tools/MakeFlightRotations.ps1`). RimWorld's ground sprites are drawn from slightly in front —
`_south` shows a face, `_north` the back of the skull, both head-up — so each needs its own
drawing. Applying the ground rule to a flying creature produced a dragon craning at the camera
mid-flight.

**Fallback when no reference can exist → `Tools/DovahArtEngine.ps1`.** Dot-source it; never copy pieces out of it.
Built 2026-08-03. It exists because hand-writing the same tapered-spike and wing code four
times in one session made every creature a sibling of the first — the user spotted that before
I did.

```powershell
. "$PSScriptRoot\DovahArtEngine.ps1"
$gfx   = Initialize-DovahCanvas -Frame 512 -Supersample 3
$spine = New-DovahSpine -ControlPoints $SPINE -Samples 140
$body  = New-DovahLoft  -Spine $spine -ThicknessProfile $THICKNESS
```

What it gives you: **`New-DovahSpine`** (Catmull-Rom centreline), **`New-DovahLoft`** (sweeps a
thickness profile along it — head, neck, chest and tail become ONE organic silhouette, which
is what stops bodies coming out as tubes or stacked blobs), **`New-DovahSpike`**,
**`Get-DovahCrestBlades`**, **`New-DovahWing`** (membrane sags as a **catenary**, not a bezier —
leather with weight in it), **`Get-DovahPlates`**, the flat RimWorld shading passes, and
**`New-DovahPreviewSheet`** (dark / lit ground / silhouette / play-distance, the standard sheet).

**`Test-DovahSilhouette` is the validator, and it deliberately does NOT count vertices.** A
vertex-count "complexity gate" was proposed and rejected on evidence: the most detailed dragon
built that session — 22 dorsal spines plus 13 cross-bands — was the worst of them (it read as a
beetle), and the fix was FEWER, LARGER features. A vertex gate would have passed the beetle and
failed the fix. It measures fill density, **concavity count** (valleys, not tips — the redraw
that failed had tips and no valleys), and legibility at 48px. **It reports; it never throws.**

**Creature scale, measured:** Dragon's Descent adults draw at **4.2 cells** (elder 4.4, ancient
4.6) against a colonist's 1.5, so creature frames are **512**, not 256.

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
