# ROADMAP.md — build order with playtest gates

One phase at a time. A phase is not complete until **all** of its exit criteria pass and the
user has playtested it and said so. Never start the next phase early.

Every phase ends with a file `TESTS/phaseN.md` containing click-by-click verification steps,
and an update to `CHANGELOG.md` and `COMPAT.md`.

**Universal exit criteria, every phase:**
1. `dotnet build` clean.
2. Mod loads with the **full 39-mod list + 3 DLCs** active, dev mode on, **zero red errors**.
3. Save → quit → reload → state intact.
4. Mod still loads and runs on the **baseline environment** as defined in `CLAUDE.md` — Core +
   Royalty + Ideology + Harmony + HugsLib, nothing else. Third-party integrations dormant, not
   broken.
5. Mod still loads with **Biotech disabled** (relevant from Phase 1 onward, because of the
   Dragonblood gene question in `SPEC.md §10`, which states the mod must not require Biotech at
   runtime). **This test cannot be run against the full 39-mod list**: five of the 39
   (`XenotypeCharmweaverResplice`, `XenotypeFaun`, `XenotypeLycan`, `XenotypeNephilim`,
   `XenotypeSatyr`) hard-require Biotech and must be disabled alongside it. Run criterion 5 as
   its own pass over the baseline environment, not as a variant of criterion 2.
6. Written test script delivered.

---

## Phase 0 — Scaffold

Prove the pipeline before building anything interesting.

- Repo structure: `About/`, `Defs/`, `Patches/`, `Textures/`, `Sounds/`, `Languages/`,
  `Assemblies/`, `Source/Dovahkiin/`, `1.4/` if `LoadFolders.xml` needs it.
- `About.xml` with correct `packageId`, `supportedVersions`, `modDependencies`,
  `loadAfter` (per `MODLIST.md` load-order intent).
- `Dovahkiin.csproj` targeting net472, referencing the local install. **Add a
  `Microsoft.NETFramework.ReferenceAssemblies` `PackageReference`** — without it,
  `dotnet build` on `net472` fails on any machine that does not have Visual Studio's .NET
  Framework targeting packs installed, and proving the build pipeline is this phase's whole job.
- **Settle the output path here and record it in `CLAUDE.md`.** `CLAUDE.md § Build` says
  `./Assemblies/Dovahkiin.dll`; a `1.4/` + `LoadFolders.xml` layout requires
  `1.4/Assemblies/Dovahkiin.dll`. Pick one before anything references it.
- Harmony patch entry point + HugsLib mod settings stub.
- `DovahkiinTuningDef` — the single def holding every tunable number in the mod.
- One trivial visible def to prove loading.

**Exit:** it loads, dev log clean, the trivial def is visible in game.

---

## Phase 1 — Identity: registry, trait, backstories, title

`SPEC.md §1, §2, §3, §5.4, §10`

- `GameComponent_DragonbornRegistry` with full `ExposeData` and the public API.
- `Trait_Dovahkiin`, `Hediff_DragonSoulAttunement` (present but with zero effects yet),
  `Hediff_TheVoice` (present, empty), the title display, four backstories.
- `Trait_Dragonblood` + heritability.
- Social effects: opinion bonuses, thoughts, memories.
- Dev-mode debug actions: `Force awaken selected pawn`, `Grant N souls`, `Clear registry`.
  **Build these in Phase 1 — every later phase depends on being able to test without waiting
  for rare events.** Register the `Spawn dragon` and `Spawn Alduin` actions in the same menu
  now, but they stay **no-op stubs until Phase 3 and Phase 4 respectively** — the fallback
  dragon (`SPEC.md §12`) does not exist until Phase 3 and Alduin until Phase 4, and on the
  baseline environment there is nothing else for them to spawn.
- Answer OD-1 and implement the chosen death handling.

**Exit:** a pawn can be made Dovahkiin via debug, is displayed correctly, the trait cannot be
granted twice, survives save/load, children inherit Dragonblood.

---

## Phase 2 — The Voice

`SPEC.md §4`

- Word / shout data model: `ShoutDef` with three words, three levels, per-level parameters.
- Discovery state (world) vs. level state (pawn). Soul spending UI.
- Shared Thu'um cooldown + `Hediff_VoiceStrain`.
- Casting: gizmo, targeting, wind-up, interruptibility, capacity requirements.
- **Ship a small vertical slice first:** Unrelenting Force, Fire Breath, Clear Skies — all
  three levels, real VFX, real sound. Get these *right* before adding breadth.
- Then the remaining core shouts from `SPEC.md §4.4a`. **The list is now ten, not twenty-one**
  (OD-10); §4.4c is out of scope. Dragonrend (§4.4b) is built in Phase 6 with the World-Eater
  chain, not here.
- `Need_Thuum` — the mod's own shout resource (`SPEC.md §5.2`, OD-9). Shouts spend this and
  nothing else. Build it here, before Phase 3 wires souls to it.
- **Dragon Aspect's visual overlay is its own sub-task — budget real time for it.** Build to
  `SPEC.md §4.4d`, which is now a full TES5-accurate spec, not a one-liner. Prefer a hediff
  overlay or invisible-apparel layer over patching `PawnRenderer` (RocketMan). **If the overlay
  cannot be made to work, stop and report — do not ship the shout as a bare stat buff.**
- **Soul Tear + the dead puppet** (`SPEC.md §4.4f`) — its own sub-task, and the riskiest thing
  in this phase. Temporary faction reassignment with restore-or-kill on every exit path,
  including save → load mid-puppet. Design it, show me the state machine, then build it. Test
  reload while a puppet is alive before calling it done.
- Slow Time: **self-haste only.** Never touch the tick manager.
- Storm Call: **hostile-only *and* outdoor-only targeting** per `SPEC.md §4.4e`. Write the
  strike; do not reuse the vanilla weather event. **This shout is not cuttable.** The outdoor
  rule also closes the old fire question — strikes cannot land under a roof, so they cannot
  ignite a base, and the three fallback resolutions the spec used to list are obsolete.
  Verify with a test that has colonists and tamed animals standing in the strike zone **and** a
  hostile standing under a roof — neither may ever be struck.
- Melee Animation and Simple Sidearms interaction testing.

**Exit:** the slice casts cleanly; cooldowns behave; Storm Call never strikes a friendly or a
roofed cell; Dragon Aspect's overlay is visibly on the pawn and survives movement, drafting and
downing; a Soul Tear puppet fights, dies on schedule, and **survives a save → load without
losing its original faction**; no animation deadlocks; save/load preserves known words and
levels.

---

## Phase 3 — Dragons and souls

`SPEC.md §5, §6`

- Dragon tagging via `COMPAT.md` facts + `DovahkiinDragonExtension` + settings toggle.
- **The mod's own fallback dragon** (`SPEC.md §12`) so the soul loop works on the baseline
  environment. Build this before wiring Dragon's Descent, so the loop is testable in isolation.
- Soul absorption: trigger conditions, the sequence, VFX/sound, letter, witnesses.
- Attunement stat scaling with the diminishing curve; mana/stamina integration with RWoM
  through the verified fields; fallback when RWoM is absent.
- Akatosh's Child damage/mitigation scaling.
- **Dragon hostility toward the Dovahkiin — its own sub-task, budget real time.** There is no
  per-target hostility for a player-faction tamed animal in 1.4; ordinary manhunter/berserk
  states target everyone nearby and break the tame bond. Expect to build a custom
  `MentalStateDef` with a forced target, or a `JobGiver`/verb-target Harmony patch. Design it,
  show me the design, then build it. Include the first-time letter.
- Dragons using scaled-up shouts.
- Non-Dovahkiin shout users (veteran mortals) — the rare generation hook.

**Exit:** kill a debug dragon near the Dovahkiin → soul absorbed, attunement rises, witnesses
gain opinion, mana/stamina max actually increased and persists across load. A tamed dragon
attacks the Dovahkiin.

---

## Phase 4 — Alduin

`SPEC.md §6.4`

- The Alduin pawn: stats, armour, flight, full dragon shout kit.
- The meteor-call shout with telegraphed impacts.
- Registry-enforced singleton.
- Revival state machine: killed-by-non-Dovahkiin → dormant → revives after N hours.
  Killed by Dovahkiin (per OD-3) → permanent.
- Victory consequences: mood event, soul reward, title upgrade.

**Exit:** debug-spawn Alduin, kill him with a normal pawn → he revives on schedule; kill him
with the Dovahkiin → he stays dead and the title upgrades. Only ever one exists.

---

## Phase 5 — World content

`SPEC.md §7, §12`

Build in this order, each individually testable:

1. **Creatures first** — draugr, overlord, deathlord, ghost, spider, skeever, dragon priest.
   Placeholder art is acceptable here; log it in `ART_TODO.md`. (Alduin and the fallback dragon
   were built in Phases 3–4; their art lands here too.)
2. **Dragon mounds** — word wall object + guardian dragon + the learn interaction.
3. **Dragon burial sites** — the corpse, the loot, and the "very bad feeling" Alduin flyover
   + resurrection event.
4. **Nordic crypts** — the big one, and **the single largest engineering risk in the project.**
   Budget accordingly. 1.4's complex generator produces a randomised room graph with no notion
   of depth or a terminal room, and its footprint is well below "a large fraction of the map."
   The guarantees in `SPEC.md §7.3` — ordered entry → catacombs → sanctum, a word wall *at the
   end*, a sealed treasure room *near the end* — are therefore **a custom `GenStep`, not a
   `ComplexDef` configuration.** Design it, cost it in `RISKS.md`, and show me before building.
   Then: dormant occupants (`CompCanBeDormant`/`CompWakeUpDormant`), tier-scaled loot,
   optional dragon priest.

Regrowth biome compatibility gets explicitly tested here, not assumed. **Rimedieval filtering
does not threaten site generation** — see the verified correction in `SPEC.md §7`: its
quest/incident/genstep removal is a hardcoded defName blocklist, so new defs pass through
untouched. What it *does* filter is `ThingDef`s at `techLevel >= Industrial`, so test the
**crypt and treasure-room loot tables**, which is where this will actually bite.

**Exit:** each site type generates on the world map, is enterable, contains what it promises,
and the Dovahkiin can learn a word from a wall in each.

---

## Phase 6 — Events and quests

`SPEC.md §8, §9`

- All incidents from §8 with tunable rates and correct gating.
- The "A Dragon!!!" awakening event and its lockout consequences. **OD-1 is answered: once per
  *Dovahkiin slot*, not once per save** — the slot reopens after the grieving delay and the
  event becomes eligible again. `SPEC.md §3.2` and `§8.1` are already updated to match.
- Implement `SPEC.md §3.2`'s resolution order between the random-colonist awakening and the
  §3.3 heir roll. Both fire on the same dragon death; the order is specified, not incidental.
- Quests §9.1, §9.2, §9.4 first; §9.3 (The World-Eater) last since it depends on everything.
- Sanguophage-parity check: Dovahkiin discoverable via both incident and quest.

**Exit:** each incident can be debug-fired and behaves correctly; the awakening event awakens a
pawn and locks out heirs; the World-Eater quest chain completes end to end.

---

## Phase 7 — The Dragon Prophecy scenario

`SPEC.md §11`

- `ScenarioDef` + custom `ScenPart`s: single Dovahkiin pawn, clothes only, scripted
  unkillable Alduin cameo (`Alduin_Scripted`, per `CLAUDE.md` invariant 2).
- **Literally starting inside another faction's settlement map is not possible.** 1.4 always
  generates a fresh map on a player-owned tile and picks the landing cell with
  `GenStep_FindPlayerStartSpot` (**not** `GenStep_PlayerStart` — that type does not exist in
  1.4; verify every type name in `Assembly-CSharp.dll` before writing it down). Present both
  options in `RISKS.md` and let me choose:
  - **(a) Full:** custom `GenStep` fabricating an execution-block settlement layout and
    populating it with hostiles. Faithful, expensive — comparable in cost to Phase 5.
  - **(b) Reduced:** normal start, but the tile spawns pre-placed hostile structures and a
    hostile force already on the map, mid-assault. Cheaper, ~80% of the feeling. **This one
    *is* a `ScenPart`** — 1.4's `RimWorld.ScenPart` exposes `PreMapGenerate`,
    `GenerateIntoMap(Map)`, `PostMapGenerate` and `PlayerStartingThings`, which is exactly the
    vanilla hook for placing arbitrary buildings and pawns into the starting map. No custom map
    generator required. Cost it accordingly; do not reach for (a) by default.
- Verify it does not consume Alduin's once-per-save boss appearance.

**Exit:** the scenario is selectable, playable, brutal, and escapable; a subsequent World-Eater
quest in the same save still spawns the real Alduin.

---

## Phase 8 — Art, audio, polish, balance

- Replace every placeholder in `ART_TODO.md`. Skyrim silhouettes, RimWorld execution,
  Medieval Overhaul-adjacent palette.
- Dragon tongue voice lines for shouts; distinct absorption and word-wall audio.
- Full `Languages/English/` keyed strings — no hardcoded user-facing text anywhere.
- Balance pass against §13's curve, driven by actual playtest reports.
- Performance pass: profile with RocketMan active, eliminate per-tick hot spots.
- `README.md` and a Steam Workshop description.

**Exit (objective, not vibes):** `ART_TODO.md` empty; zero hardcoded user-facing strings
(grep the source); a profiler capture with RocketMan active showing no Dovahkiin method in the
top 20 by cost; and a documented playthrough reaching soul count 10 whose measured numbers sit
inside the `SPEC.md §13` bands.

---

## Cut list — if time or the engine runs out

Cut in this order, and say so in `RISKS.md` rather than shipping something broken:
stretch shouts (§4.4) → Call of Valor → Soul Tear → crypt puzzles → the second gentler
scenario → veteran mortal shout users → dragon priest masks as unique gear.

**Never cut:** the one-Dovahkiin invariant, the soul economy, the slow power curve,
dragon hostility, or Alduin.
