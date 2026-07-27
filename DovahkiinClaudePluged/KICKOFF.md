# KICKOFF PROMPT — paste this into Claude Code as your very first message

> Put `CLAUDE.md`, `SPEC.md`, `ROADMAP.md`, and `MODLIST.md` in an empty folder,
> open Claude Code in that folder, and paste everything below the line.

---

You are the sole engineer on a large RimWorld 1.4 content mod called **Dovahkiin**.
This is a multi-session project. Read `CLAUDE.md`, `SPEC.md`, `ROADMAP.md`, and `MODLIST.md`
in this directory before you do anything else. They are the contract. `SPEC.md` wins over
your instincts; `CLAUDE.md` wins over `SPEC.md` on anything procedural.

**Target:** RimWorld **1.4.3682 rev778**, DLCs owned: **Royalty, Ideology, Biotech**.
No Anomaly. No 1.5+ APIs. C# targets **.NET Framework 4.7.2**, Harmony 2.x via HugsLib/Harmony mod.

## Do these in order. Do not skip ahead.

### STEP 1 — Reconnaissance (no mod code yet)

Do not write a single line of the mod until this step is finished and I have approved
the output. Your job here is to replace assumptions with facts.

1. Locate the RimWorld install and the user's `Mods/` folder. Ask me for the paths if you
   cannot find them. Then read, on disk, the actual `Defs/`, `Patches/`, and `Assemblies/`
   of every mod in `MODLIST.md` marked **PRIMARY** or **SECONDARY**.
2. Where a mod's source is public (see `MODLIST.md` for repo links), fetch and read it.
   Prefer real source over wiki pages. Prefer the 1.4 branch/tag over `main` when they differ.
3. Decompile or at minimum enumerate the public API surface of: `Assembly-CSharp.dll`,
   `TorannMagic` (RimWorld of Magic), `Dragons Descent`, `VanillaExpandedFramework`,
   `JecsTools`, `MedievalOverhaul`, `The Profaned`. Use `ilspycmd`, `monodis`, or
   `System.Reflection` over the DLLs — whatever is available. Install a tool if you need one.
4. Produce **`COMPAT.md`** containing, with exact strings copied from disk (never guessed):
   - Every dragon `ThingDef`/`PawnKindDef` defName in Dragon's Descent, its faction, its
     `RaceProps`, and how it is spawned. Flag which ones are "real dragons" for this mod's
     purposes vs. drakes/wyverns/lesser kin, and propose the tagging strategy.
   - How RimWorld of Magic stores **mana** and **stamina**. Start from these verified facts,
     do not re-derive them: `TM_Mana` and `TM_Stamina` are **`NeedDef`s** (`needClass`
     `TorannMagic.Need_Mana` / `Need_Stamina`), both `<onlyIfCausedByHediff>true</…>`, created
     only by `TM_MagicUserHD` and `TM_MightUserHD` respectively. A vanilla `Need` is a 0–1 bar,
     so **there is no integer "max mana" field to increment** — `SPEC.md §5.2`'s "+2" is not a
     real unit and OD-9 exists because of it. What you still need to establish: how TorannMagic
     computes its internal `MaxMP`/`maxSP`, whether that is persisted or recomputed, and what
     the cleanest supported lever is for a *permanent* enlargement of the pool. This determines
     the entire Dragon Soul reward design — get it exactly right, and answer OD-9 with it.
   - Medieval Overhaul + Rimedieval tech-level gating: what would block a new faction, item,
     or site from generating, and what def tags I must apply to avoid being filtered out.
   - Vanilla Expanded Framework and JecsTools features I can reuse instead of writing:
     ability systems, custom faction/raid logic, world object generation, animation hooks.
   - `The Profaned`: does it already ship undead/ghost pawns, damage types, or a spirit
     mechanic I should extend rather than duplicate? Same question for `Lightless Empyrrean`.
   - Melee Animation and Simple Sidearms hook points that a shout-casting pawn could break.
   - RocketMan's caching/optimisation patches — list anything that would break a per-tick
     custom comp, and the safe pattern to avoid it.
   - The exact 1.4 vanilla API for: `AbilityDef` + `CompProperties_AbilityEffect`,
     `ComplexDef`/`GenStep_AncientComplex` (underground ruin generation), `SitePartDef`,
     `QuestScriptDef`, `ScenarioDef` + `ScenPart`, `GameComponent`, `IncidentWorker`.
     **All of these are already confirmed present in 1.4.3682 `Assembly-CSharp.dll`**, along
     with `RoyalTitleDef`, `DefModExtension`, `CompCanBeDormant` and `CompWakeUpDormant`;
     `LayoutDef`, `LayoutWorker` and `LayoutRoomDef` are confirmed **absent** (1.5+). Do not
     spend a cycle re-establishing existence — go straight to reading their members and the
     vanilla defs that use them.
     Copy the real vanilla examples you will be modelling on, especially the **Biotech
     sanguophage discovery quests**, whose real identities are already verified in
     `SPEC.md §3.4`: **`SanguophageMeetingHost`** and **`SanguophageShip`**, in
     `Data\Biotech\Defs\QuestScriptDefs\`, both `rootSelectionWeight 0.5`, with
     `minRefireDays 200` on the latter. Biotech has *no* sanguophage incident, only these two
     quests. **Their in-game names are generated per instance from `questNameRules`, so strings
     like "Bloodthirsty Parley" are not searchable identities** — go by defName. The Dovahkiin
     stranger quest in `SPEC.md §9.4` should match their shape and rarity.
   - The **actual game build** is already confirmed: `Version.txt` reads **`1.4.3682 rev778`**,
     matching the pin exactly. This is a **GOG** install (`goggame-*.info` in the root), so the
     Steam auto-update-to-1.4.3901 concern does not apply here. Re-check `Version.txt` if the
     install is ever moved or reinstalled, but do not treat this as an open question.
5. Produce **`RISKS.md`**: everything in `SPEC.md` that the modlist or the 1.4 engine makes
   hard, expensive, or impossible, each with a cheaper alternative that preserves the fantasy.
   Be blunt. I would rather cut a feature now than debug a broken save in forty hours.
6. Produce **`DECISIONS.md`**: answer the *Open Decisions* section at the end of `SPEC.md`
   with a recommendation for each, then **stop and ask me**. Do not proceed on assumptions.

Then stop. Report. Wait for my go-ahead.

### STEP 2 — Scaffold and prove the loop

Only after I approve Step 1. Build Phase 0 from `ROADMAP.md`: folder structure, `About.xml`,
`LoadFolders.xml`, a compiling `net472` assembly, one trivial def that visibly loads in-game.
Give me the exact steps to test it. Do not move to Phase 1 until I confirm it loads clean
with the full modlist active and a clean dev-mode log.

### STEP 3 onward — Follow `ROADMAP.md` phase by phase

One phase at a time. Every phase ends with: build succeeds, mod loads with the full modlist,
zero red errors in dev-mode log, and a written **test script** telling me exactly what to click
to verify the feature. I playtest, I report back, then and only then do you start the next phase.

## Standing rules for this project

- **Never invent a defName from another mod.** If you have not read it on disk or in source,
  you do not know it. Write `TODO(COMPAT)` and ask.
- **Never hard-link another mod's assembly.** Cross-mod integration goes through
  `MayRequire`/`MayRequireAnyOf` on XML defs, and reflection with null-guards in C#.
  The mod must load and run with *only* the DLCs active, with dragon features gracefully dormant.
- **One def, one file, sensible folders.** No thousand-line def dumps.
- **Every C# file starts with a comment saying which SPEC.md section it implements.**
- Prefer patching over overwriting. Prefer XML over C# when XML is genuinely sufficient —
  but do not contort XML to avoid writing code that ought to be code.
- Save compatibility: everything persisted goes through `ExposeData` with defaults that
  survive a load from a save made before the field existed.
- Performance: no per-tick work on every pawn. Cache, use rare ticks, register listeners.
  RocketMan is in this list and will punish you.
- When you are unsure whether something is faithful to Skyrim, say so and ask. When you are
  unsure whether something is possible in 1.4, go read the assembly.
- Keep a running `CHANGELOG.md` and update `COMPAT.md` whenever you learn a new fact.

## The one rule that outranks everything

**There is at most one Dovahkiin in a save, ever, at a time.** Every system you build must
route through the single authority object described in `SPEC.md §2`. If you find yourself
writing a second place that can set the trait, stop and refactor.

Begin with Step 1.
