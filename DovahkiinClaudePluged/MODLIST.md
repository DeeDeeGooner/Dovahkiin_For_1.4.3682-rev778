# MODLIST.md — the environment this mod must live in

RimWorld **1.4.3682 rev778**. DLCs: **Core, Royalty, Ideology, Biotech**. No Anomaly.

Priority tags tell you how much reconnaissance each mod deserves in Step 1.
**PRIMARY** = this mod's features depend on it. **SECONDARY** = likely conflict or reuse surface.
**AMBIENT** = read the About.xml, note the version, move on.

## PRIMARY — read the source

| Mod | Why it matters | Where to look |
|---|---|---|
| **Dragons Descent** | The dragons. Source of most dragon souls, mound guardians, and the balance yardstick for Alduin. | Public repo: `github.com/Aether-Guild/Dragons-Descent`. Its README is a placeholder and it uses a `release` branch with **per-version subfolders**, not a 1.4 branch — navigate accordingly. **The on-disk `Defs/` is the authority**; the workshop build may differ from the repo. Note: it hard-requires Vanilla Expanded Framework. |
| **Rim World of magic** (A RimWorld of Magic) | Owns mana and stamina. Dragon Soul rewards scale these. Also the best in-list reference for how to do custom abilities, VFX, and a per-pawn power resource in this engine. | `github.com/TorannD/RWoM` (and older `TorannD/TMagic`). Read `TorannMagic` assembly on disk for the 1.4 build. |
| **Vanilla Expanded Framework** | Reuse before you rewrite: ability framework, custom faction/raid logic, world object and site helpers, animation and graphics utilities. Already a transitive dependency of Dragon's Descent. | `github.com/Vanilla-Expanded/VanillaExpandedFramework` + the on-disk copy. |
| **JecsTools** | `CompAbilityUser` and related. Evaluate as the shout backbone vs. vanilla `AbilityDef` vs. VEF. | On-disk assembly + roxxploxx modding wiki `SHORTUTORIAL: JecsTools.CompAbilityUser`. |
| **Medieval Overhaul** + **Medieval Overhaul Royalty** + **Rimedieval** | Tech-level gating. **Rimedieval's filtering is C#, not XML** — see the note below; the exposure is loot, not site generation. | **`Rimedieval/1.4/Source/Rimedieval/DefCleaner.cs`** and `HarmonyPatches.cs`. `Rimedieval/1.4/Patches/` is *not* where the stripping happens. For Medieval Overhaul, check its `Patches/` as normal. |
| **The profaned** | Possible existing undead / dark-spirit / corruption mechanics. Extend rather than duplicate for draugr and ghosts. Also check for damage-type and hediff collisions. | On-disk defs + assembly. |

### Rimedieval — verified, do not re-derive from scratch

Read `Rimedieval/1.4/Source/Rimedieval/DefCleaner.cs` to confirm, but the facts are:

- `DefCleaner.ClearDefs()` removes `QuestScriptDef`, `IncidentDef`, `GenStepDef`, `PreceptDef`,
  `MemeDef` and `IdeoPresetDef` by **hardcoded defName blocklist**. A new def that is not on the
  list is not touched. **Dovahkiin quests, incidents, gensteps and sites therefore need no
  special tagging to survive Rimedieval.**
- `IsAllowedForRimedieval(ThingDef)` is the tech-level test: anything resolving to
  `techLevel >= Industrial` gets `designationCategory` nulled, and `GetAllowedThingDefs()`
  filters it out of thing lists. **This is the real risk surface — loot.** `SPEC.md §7.3`'s
  "loot drawn from the modlist's equipment pools" is what will silently come up empty or
  medieval-only.
- `questsToRemove` deletes `AncientComplex_Standard`, `AncientComplex_Mission`,
  `OpportunitySite_AncientComplex` and `OpportunitySite_AncientComplex_Mechanitor`. The vanilla
  ancient-complex quests `SPEC.md §7.3` says to model on are **gone at runtime** in this
  modlist — read them from `Data\Ideology\Defs` and `Data\Biotech\Defs` on disk instead.
- `genStepsToRemove` also strips the vanilla ancient-tech scatter gensteps
  (`AncientTurret`, `AncientMechs`, `AncientLandingPad`, …). Do not model crypt dressing on them.

## SECONDARY — enumerate and check for collisions

| Mod | Concern |
|---|---|
| **Melee Animation** | Shout casting, knockback, and stagger may fight its animation state machine. Find the hook points. |
| **SimpleSidearms** | Weapon-swap during shouts; Disarm/Elemental Fury interactions. |
| **RocketMan** | Aggressive caching. Any per-tick comp, stat recalculation, or dynamic graphic you add must be RocketMan-safe. Enumerate its patches and note the safe patterns. |
| **Lightless Empyrrean** | Another dark/otherworldly content mod — check for overlapping incidents, factions, and creature niches. |
| **Dragons Descent — related creature mods** | If any other mod also defines "dragon-like" pawns, decide whether they count as dragons for soul purposes. Recommend: only tagged Dragons Descent kin count, plus a settings toggle to include others. |
| **Divine Order** | Ideology/religion content — the Dovahkiin title and Akatosh flavour may want a precept hook or at least must not clash. |
| **Harmony Library**, **HugsLib**, **JecsTools** | Load order roots. Confirm the mod's `About.xml` `loadAfter` list. |
| **Vanilla Expanded Framework** | Also secondary as a conflict surface, not just a tool. |
| **Fortifications Neolitic**, **Armor Rack**, **Replace Stuff** | Building/def collisions for crypt and dragon-mound structures. |

## AMBIENT — note version, move on

`B.B` · `Filth Vanish with Time and Rain` · `GiddyUp2` · `Gloomy Face Mod` ·
`MoreWorldFeaturesNames` · `Regrowth` (+ `RegrowthAspenForests`, `RegrowthBorealForests`,
`RegrowthColdBog`, `RegrowthDesert`, `RegrowthTropical`, `RegrowthTundra`) · `RocketMan` ·
`sized-apparel-zero-v3.10.7` · `SYR Processor Framework` ·
`The vanity project Female hair` · `The vanity project Male hair` ·
`XenotypeCharmweaverResplice` · `XenotypeFaun` · `XenotypeLycan` · `XenotypeNephilim` · `XenotypeSatyr`

Two notes on the ambient set that are **not** optional:

- **Regrowth biome mods** change biome defs. Dragon mounds, burial sites, and crypts must
  declare biome eligibility in a way that still works when Regrowth's biomes replace or
  supplement vanilla ones. Do not whitelist vanilla biomes by name only.
- **The Xenotype mods** matter because of `SPEC.md §1`: *every race can produce a Dovahkiin*.
  Awakening logic must not filter on xenotype, gene set, or race — including modded ones.
  `MoreWorldFeaturesNames` also matters for naming generated sites consistently.

## Full alphabetical list (39 mods, for completeness)

Armor Rack · B.B · Divine Order · Dragons Descent · Filth Vanish with Time and Rain ·
Fortifications Neolitic · GiddyUp2 · Gloomy Face Mod · Harmony Library · HugsLib · JecsTools ·
Lightless Empyrrean · Medieval Overhaul · Medieval Overhaul Royalty · Melee Animation ·
MoreWorldFeaturesNames · Regrowth · RegrowthAspenForests · RegrowthBorealForests ·
RegrowthColdBog · RegrowthDesert · RegrowthTropical · RegrowthTundra · Replace Stuff ·
Rim World of magic · Rimedieval · RocketMan · SimpleSidearms · sized-apparel-zero-v3.10.7 ·
SYR Processor Framework · The profaned · The vanity project Female hair ·
The vanity project Male hair · Vanilla Expanded Framework · XenotypeCharmweaverResplice ·
XenotypeFaun · XenotypeLycan · XenotypeNephilim · XenotypeSatyr

## Load order intent

`Harmony → Core → Royalty → Ideology → Biotech → HugsLib → JecsTools → Vanilla Expanded
Framework → (everything else) → Medieval Overhaul family → Rimedieval → **Dovahkiin** → RocketMan`

Dovahkiin loads late so its patches apply on top of Medieval Overhaul's filtering.
Verify this against each mod's stated requirements in Step 1 and correct it in `COMPAT.md`.
