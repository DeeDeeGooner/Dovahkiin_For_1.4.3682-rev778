# COMPAT.md — verified facts

Everything here was read off disk at `C:\Games\Rimworld\RimWorld\RimWorldFolder`.
Nothing in this file is guessed. Anything not yet verified is marked `TODO(COMPAT)`.

---

## 1. Environment — confirmed

| Thing | Verified value | Source |
|---|---|---|
| Game build | **1.4.3682 rev778** — matches the pin exactly | `Version.txt` |
| Distribution | **GOG**, not Steam | `goggame-*.info` in game root |
| DLCs | Royalty, Ideology, Biotech. **No Anomaly** | `Data\` |
| Mods installed | **39**, matching `MODLIST.md` exactly | `Mods\` |
| C# target | **net472** confirmed as correct for a 1.4 mod | `Mods\Rimedieval\1.4\Source\…\obj\Debug\.NETFramework,Version=v4.7.2.AssemblyAttributes.cs` |

The GOG install will not silently auto-update to 1.4.3901. The `CLAUDE.md` warning about Steam
updates does not apply here. Re-check `Version.txt` only if the game is reinstalled.

---

## 2. Vanilla 1.4 API — existence confirmed by reflection over `Assembly-CSharp.dll`

**Present:** `ComplexDef` (: `Def`) · `ComplexRoomDef` · `ComplexThreatDef` ·
`GenStep_AncientComplex` (: `GenStep_ScattererBestFit`) · `SitePartDef` · `QuestScriptDef` ·
`ScenarioDef` · `ScenPart` · `GameComponent` (Verse) · `IncidentWorker` · `AbilityDef` ·
`CompProperties_AbilityEffect` (: `AbilityCompProperties`) · `RoyalTitleDef` · `DefModExtension`
(Verse) · `CompCanBeDormant` · `CompWakeUpDormant` · `Pawn_AbilityTracker` · `Verb_CastAbility` ·
`MentalStateDef` · `BackstoryDef` · `SketchResolverDef` · `GenStep_FindPlayerStartSpot`

**Absent (do not use):** `LayoutDef` · `LayoutWorker` · `LayoutRoomDef` ·
`LayoutStructureSketch` — all 1.5+. Also **`GenStep_PlayerStart` does not exist**; the real
type is `GenStep_FindPlayerStartSpot`.

### Two structural facts that shape §7.3 and §11

- **`ComplexDef` has no ordering.** Its complete field list is `roomDefs`, `threats`,
  `workerClass`, `roomRewardCrateFactor`, `fixedHostileFactionChance`, `rewardThingSetMakerDef`.
  `ComplexRoomDef` is `sketchResolverDef`, `selectionWeight`, `maxCount`, `minArea`, `maxArea`,
  `requiresSingleRectRoom`, `floorTypes`. **No depth, no sequence, no terminal room.**
  `GenStep_AncientComplex.DefaultComplexSize` = `(80, 80)` ≈ 10% of a 250×250 map.
  Vanilla `ComplexDef`s live in `Data\Ideology` and `Data\Biotech` only.
- **`ScenPart` can write into the starting map.** It exposes `PreConfigure`, `PostWorldGenerate`,
  `PreMapGenerate`, **`GenerateIntoMap(Map)`**, `PostMapGenerate`, `PlayerStartingThings`,
  `PostGameStart`, `Tick`. Placing buildings and hostile pawns at game start needs no custom
  map generator.

---

## 3. Vanilla Expanded Framework — the single most important finding

VEF 1.4 ships **`KCSG.dll`** (164 KB), the "Custom Structure Generation" module. Confirmed
classes include:

- `GenStep_CustomStructureGen` — places a hand-authored structure into a map
- `StructureLayoutDef` — the authored layout itself
- `SymbolDef` / `SymbolsDef` / `SymbolResolver_*` (~20 resolvers, incl.
  `SymbolResolver_RoomGenFromStructure`, `SymbolResolver_RandomDamage`,
  `SymbolResolver_RandomFilth`, `SymbolResolver_ScatterPropsAround`)
- **`Dialog_ExportWindow` + `ExportUtils`** — an **in-game exporter**: build a structure in a
  dev-mode map, select it, export it as a `StructureLayoutDef`. This is the authoring path and
  it requires no code.
- `KCSG_UndergroundRoom` — underground room support
- `GenStep_Settlement` + `SettlementLayoutDef` + `SymbolResolver_Settlement` +
  `SettlementGenUtils` — a **full settlement generator**
- `LayoutValidator`, `LayoutCommonality`, `StructOption`, `SitePart`, `SitePartParams`

**All of it is driven from XML.** Dragon's Descent uses it without referencing KCSG in code:

```xml
<GenStepDef>
  <defName>Dragon_lair_1</defName>
  <linkWithSite>Dragon_lair_1</linkWithSite>
  <order>460</order>
  <genStep Class="KCSG.GenStep_CustomStructureGen">
    <structureLayoutDefs><li>Dragon_lair_1</li></structureLayoutDefs>
    <fullClear>true</fullClear>
    <preventBridgeable>true</preventBridgeable>
  </genStep>
</GenStepDef>
```

`Defs\Custom Structure Generation\Structures\` in Dragon's Descent is a **complete working 1.4
reference**: `Genstep.xml`, `SymbolDef.xml`, `Questscript.xml`, and two authored layouts
(`StructureLayoutDef_Dragon_lair_1.xml` 27 KB, `..._Gold_2.xml` **110 KB**). Copy this shape.

Because it is XML-only, using KCSG needs **`MayRequire`, not an assembly reference** — it does
not violate `CLAUDE.md`'s no-hard-reference rule.

Also in VEF 1.4: `MVCF.dll` (multi-verb framework, used by Dragon's Descent), `Outposts.dll`,
`PipeSystem.dll`, `VFECore.dll` (1 MB, contains `VFECore.Abilities.*`).

---

## 4. Dragon's Descent — `onyxae.dragonsdescent`

Hard-requires `brrainz.harmony` and `OskarPotocki.VanillaFactionsExpanded.Core`. Version
folders 1.1–1.6; **use `1.4/`**. Single assembly `DDLib.dll` (262 KB).

### The dragons (ThingDef and PawnKindDef share defNames)

| defName | Base | combatPower | wildness |
|---|---|---|---|
| `Green_Dragon` | `DragonRaceBase` | 1240 | 0.88 |
| `Black_Dragon` | `DragonRaceBase` | 1260 | 0.97 |
| `Blue_Dragon` | `DragonRaceBase` | 1260 | 0.92 |
| `Purple_Dragon` | `DragonRaceBase` | 1260 | 0.90 |
| `Red_Dragon` | `DragonRaceBase` | 1260 | 0.96 |
| `White_Dragon` | `DragonRaceBase` | 1260 | 0.94 |
| `Yellow_Dragon` | `DragonRaceBase` | 1460 | 0.90 |
| `Gold_Dragon` | `RDragonRaceBase` | 1320 | 0.90 |
| `Jade_Dragon` | `RDragonRaceBase` | 1320 | 0.90 |
| `Silver_Dragon` | `RDragonRaceBase` | 1320 | 0.90 |
| `True_Dragon` | `RDragonRaceBase` | **1650** | 0.987 (bodySize 4.6) |

**Tagging strategy:** all eleven are real dragons — there are no drakes/wyverns/lesser kin to
exclude. Two clean hooks exist: the parent defs `DragonRaceBase` (common) and `RDragonRaceBase`
(rare). Tag via `DovahkiinDragonExtension` on the two **parents** with a `MayRequire` patch, so
any dragon Aether-Guild adds later inherits the tag automatically. `True_Dragon` is the balance
yardstick — Alduin must sit clearly above 1650 combat power.

Dragons are **animals** with high wildness, i.e. tameable. `SPEC.md §6.2` (tamed dragon turns
on the Dovahkiin) is therefore a real scenario, not hypothetical.

### Dragons already use vanilla `AbilityDef`

`Defs\AbilityDefs\DragonBreath.xml` and `Flight.xml` define `DD_DragonBreath_Fire`,
`DD_DragonBreath_Frost`, `DD_IceBreath`, `DD_DragonSpit`, `DD_DragonLightning`,
`DD_ElectromagneticPulse`, `DraconicFlight`, `WingedFlyer` — as vanilla `AbilityDef`s with
`<verbClass>Verb_CastAbility</verbClass>`, extended by `VFECore.Abilities.AbilityExtension_Projectile`,
`AbilityExtension_Explosion`, and `DD.AbilityCompProperties_Flight` / `_Cooldown` /
`_RequireBodyPart`.

**This proves vanilla `AbilityDef` works on animal pawns in 1.4**, which settles the shout
backbone question — see RISKS.md §2.

---

## 5. RimWorld of Magic — mana and stamina

`TorannMagic.dll` 2.42 MB at `v1.4\Assemblies\`. `LoadFolders.xml` maps `v1.0`–`v1.4`.

### How the resources actually work

- `TM_Mana` is a **`NeedDef`**, `needClass` `TorannMagic.Need_Mana`.
- `TM_Stamina` is a **`NeedDef`**, `needClass` `TorannMagic.Need_Stamina`.
- Both are `<onlyIfCausedByHediff>true</onlyIfCausedByHediff>`.
- Mana is created by `TM_MagicUserHD` (`<causesNeed>TM_Mana</causesNeed>`);
  stamina by `TM_MightUserHD`.
- A vanilla `Need` is a **0–1 bar**. **There is no integer "max mana" field.**
  `SPEC.md §5.2`'s original "+2" was not a real unit.
- **A pawn with neither class hediff has neither need at all.**

### The supported way to enlarge the pool — XML only, no reflection

RWoM reads an `<enchantments>` block from hediff stages. Real example, `Hediffs_Golemancer.xml`:

```xml
<stages>
  <li>
    <minSeverity>0</minSeverity><maxSeverity>1</maxSeverity>
    <enchantments>
      <mpRegenRate>.1</mpRegenRate>
      <maxMP>.1</maxMP>
      <magicCooldown>-.05</magicCooldown>
      <mpCost>-.05</mpCost>
      <maxSP>.1</maxSP>
    </enchantments>
  </li>
  …
</stages>
```

Fields confirmed in `TorannMagic.dll`: `maxMP`, `maxSP`, `mpRegenRate`, `magicCooldown`,
`mpCost`, `arcaneRes` (plus `TM_HediffEnchantment_maxMP` / `_mpRegenRate` / `_arcaneRes`).
**Values are fractional multipliers** — `0.1` = +10%.

**This maps exactly onto `Hediff_DragonSoulAttunement`**, whose severity is already the soul
count: one `<li>` stage per soul tier, each carrying a larger `<maxMP>`/`<maxSP>`. Pure XML,
gated with `MayRequire="Torann.ARimworldOfMagic"`. No assembly reference, no reflection.

The remaining gap — what a Dovahkiin with **no** RWoM class gets — is **OD-9**.

---

## 6. Rimedieval — filtering is C#, not XML

`Rimedieval\1.4\Source\Rimedieval\DefCleaner.cs` (source ships with the mod).

`ClearDefs()` removes defs by **hardcoded defName blocklist** from `DefDatabase<QuestScriptDef>`,
`<IncidentDef>`, `<GenStepDef>`, `<PreceptDef>`, `<MemeDef>`, `<IdeoPresetDef>`.

**Consequence: new Dovahkiin quests, incidents, gensteps and sites are not filtered.** They are
not on the list, so they survive untouched. No special tagging needed for them to generate.

What *is* filtered is `ThingDef`s, by tech level:
`IsAllowedForRimedieval()` returns false for anything resolving to `techLevel >= Industrial`
(walking techprints, recipe research prerequisites, cost list materials, and the def's own
`techLevel`, taking the max). Those get `designationCategory = null`, and `GetAllowedThingDefs()`
strips them from thing lists. **Loot is the real exposure.**

Already-removed quests to be aware of: `AncientComplex_Standard`, `AncientComplex_Mission`,
`OpportunitySite_AncientComplex`, `OpportunitySite_AncientComplex_Mechanitor`,
`EndGame_ShipEscape`, `MechanitorShip`, all `Pollution*`.
Removed gensteps include `AncientTurret`, `AncientMechs`, `AncientLandingPad`,
`AncientExostriderRemains`, `AncientPipelineSection`, `AncientJunkClusters`.

Settings-driven: `RimedievalSettings.restrictTechToPreIndustrialOnly` switches the research
whitelist between "≤ Industrial minus microelectronics" and "≤ Medieval".

---

## 7. The Profaned — prefix `BotchJob_`

Ships undead and spirit content that overlaps `SPEC.md §12`:

- **Undead:** `BotchJob_Skeleton`, `BotchJob_UndeadColossus`, `BotchJob_UndeadHorse`,
  `BotchJob_UndeadWarg`, `BotchJob_UndeadFlesh`, `BotchJob_CorpsesUndead`
- **Ghost-equivalent:** `BotchJob_Wraith`, `BotchJob_WraithClaw`, `BotchJob_WraithOrb`
- **DamageDefs:** `BotchJob_BloodflameExplosion`, `BotchJob_BloodflameImpact`,
  `BotchJob_BoneImpact`, `BotchJob_ColossusSmash`, `BotchJob_GraspingDeadDamage`,
  `BotchJob_IceShardsDamage`, `BotchJob_RotArrow`, `BotchJob_RotCutDamage`,
  `BotchJob_RotstinkBlast`, `BotchJob_WraithClawBlast`

**No defName collision risk** — everything is `BotchJob_`-prefixed; Dovahkiin content will use
its own prefix. Recommendation: build draugr and ghosts as **our own** defs (they need Nordic
silhouettes, level-1 shouts and dormancy comps, none of which Profaned provides), but reuse
`BotchJob_IceShardsDamage` / `BotchJob_BoneImpact` style damage where flavour matches, behind
`MayRequire`.

---

## 8. Secondary mods — surveyed

| Mod | Assemblies | Version folders | Status |
|---|---|---|---|
| RocketMan | `Cosmodrome`, `Gagarin`, `Proton`, `Soyuz`, `XmlDiffPatch` | 1.2–1.5 | `TODO(COMPAT)` — patch enumeration outstanding |
| Melee Animation | `Meta.Numerics`, `AMRetextureSupport`, `0BetterFloatMenu`, … | 1.4 only | `TODO(COMPAT)` — hook points outstanding |
| JecsTools | `0JecsTools`, **`AbilityUser`**, `AbilityUserAI`, `CompActivatableEffect`, `CompAnimated` | 1.0–1.5 | Present; **not needed** — see RISKS.md §2 |
| Lightless Empyrrean | `Lightless_Empyrean.dll` (6 KB) | 1.4, 1.5 | Tiny. Low collision risk. `TODO(COMPAT)` |
| Medieval Overhaul (+Royalty) | — | — | `TODO(COMPAT)` — loot pool tagging outstanding |
| SimpleSidearms | — | — | `TODO(COMPAT)` |

None of the outstanding items block Phase 0 or Phase 1. RocketMan and Melee Animation must be
resolved before Phase 2 ships casting; Medieval Overhaul loot tagging before Phase 5.

---

## 8a. How to verify an XML field name — and how NOT to

Learned the hard way in Phase 1, twice.

**An unrecognised field does NOT discard the def.** RimWorld logs
`XML error: <field> doesn't correspond to any field in type X`, skips that one field, and keeps
the rest of the def. Proven on disk: a build that logged this error for `Dovahkiin_TheVoice`
still produced a working def — the pre-fix save contains a live `Hediff_TheVoice` with its
severity and dictionary intact. So the error is real and must be fixed, but it is **log noise,
not the cause** of a missing def. Do not stop investigating when you find one.

**A hediff whose severity is 0 is deleted on the next health tick.** `Hediff.ShouldRemove` is
`Severity <= 0f` by default. Any hediff that legitimately starts at zero — a counter, a
resource, an accumulator — needs a subclass overriding `ShouldRemove` to `false`. This is the
kind of failure that looks exactly like "my def didn't load": the def is fine, the hediff was
added, and it silently vanished a tick later.

**Reflection with default binding flags gives false negatives.** `Type.GetFields()` returns
public members only, but **many RimWorld def fields are non-public** — `TraitDef.commonality`
and `commonalityFemale` are, despite every vanilla trait using `<commonality>` in XML. Checking
a field name this way will wrongly report a valid field as missing. Use:

```
BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
```

and walk `BaseType` for inherited fields.

**The authoritative validator is the game itself.** Load once, then grep `Player.log` for
`XML error`. That checks every field of every def against the real loader, which no
hand-written reflection pass will match.

Confirmed real names that are easy to get wrong:

| Wrong | Right | Type |
|---|---|---|
| `scenarioCanAddHediff` | **`scenarioCanAdd`** | `HediffDef` |
| `<Melee>3</Melee>` | **`<li><key>Melee</key><value>3</value></li>`** | `BackstoryDef.skillGains` — it is a `Dictionary<SkillDef,int>`. The shorthand *is* right for `statOffsets`, a `List<StatModifier>`. |
| `Pawn_TraitsTracker` | **`TraitSet`** | `pawn.story.traits` |
| `GenStep_PlayerStart` | **`GenStep_FindPlayerStartSpot`** | map generation |

## 8b. Verified packageIds — read from `Config\ModsConfig.xml`, do not guess these

RimWorld **silently ignores** an unknown packageId in `loadAfter`/`loadBefore`/`modDependencies`.
A typo does not error; it just stops enforcing anything. Every id below is copied from the live
active-mods list.

| Mod | packageId |
|---|---|
| Harmony | `brrainz.harmony` |
| HugsLib | `unlimitedhugs.hugslib` |
| JecsTools | `jecrell.jecstools` |
| Vanilla Expanded Framework | `oskarpotocki.vanillafactionsexpanded.core` |
| Dragon's Descent | `onyxae.dragonsdescent` |
| RimWorld of Magic | `torann.arimworldofmagic` |
| Medieval Overhaul | `dankpyon.medieval.overhaul` |
| Medieval Overhaul Royalty | `accurex.medievalempireoverhaul` |
| **Rimedieval** | **`ogam.rimedieval`** — *not* `Kikohi.Rimedieval`, which is what the mod's own wiki suggests and which was wrong in our `About.xml` until Phase 0 |
| RocketMan | `krkr.rocketman` |
| Dovahkiin | `erzou.dovahkiin` |

## 9. Load order — corrected

Dragon's Descent requires VEF, and Dovahkiin will consume KCSG (VEF) from XML, so:

`Harmony → Core → Royalty → Ideology → Biotech → HugsLib → JecsTools → Vanilla Expanded
Framework → Dragons Descent → (everything else) → Medieval Overhaul family → Rimedieval →
**Dovahkiin** → RocketMan`

Dovahkiin after Rimedieval so our patches sit on top of its filtering; before RocketMan so its
optimisation layer sees our finished defs.

## 10. Frost damage across the modlist — what actually resists it

Read off disk 2026-07-29 while designing Dragon Aspect's word-2 "fire and frost resistance".
Every frost/ice damage source in the active modlist, with the `<armorCategory>` it declares:

| Source | DamageDef | armorCategory | Therefore resisted by |
|---|---|---|---|
| RimWorld of Magic — blizzards | `TM_Blizzard_Small`, `TM_Blizzard_Large` | **Heat** | `ArmorRating_Heat` |
| RimWorld of Magic — frost ray | `FrostRay` | **Heat** | `ArmorRating_Heat` |
| RimWorld of Magic — snowball | `Snowball` | **Heat** | `ArmorRating_Heat` |
| RimWorld of Magic — enchanted ice | `TM_Enchanted_Ice` | **Heat** | `ArmorRating_Heat` |
| RimWorld of Magic — freezing winds | `TM_FreezingWindsDD` | **Heat** | `ArmorRating_Heat` |
| RimWorld of Magic — iceshard | `Iceshard` | **Sharp** | `ArmorRating_Sharp` |
| Dragon's Descent — frost breath | `DD_Frost_Breath` | **Sharp** | `ArmorRating_Sharp` |
| The Profaned — ice shards | `BotchJob_IceShardsDamage` | **Blunt** | `ArmorRating_Blunt` |
| Vanilla — weather frostbite | `Frostbite` | **none** | nothing; see below |

**The headline: RimWorld of Magic files frost under the HEAT armour category.** Five of its
seven frost damages are Heat, so `ArmorRating_Heat` — the obvious "fire resistance" stat —
already buys most of the modlist's frost resistance as well. That is a coincidence of RWoM's
authoring, not a rule; re-check it if RWoM ever updates.

**Vanilla `Frostbite` cannot be resisted by armour at all.** It has no `armorCategory`, sets
`externalViolence: false`, and runs through `DamageWorker_Frostbite` — it is the environmental
cold-weather injury, not a weapon. The only defences are `Insulation_Cold` and
`ComfyTemperatureMin`. So anything billed as frost resistance must include cold insulation or
it will not stop frostbite or hypothermia.

**Consequence for Dragon Aspect:** no Harmony damage hook and no list of foreign defNames is
needed. Four Core stats cover every entry in the table —
`ArmorRating_Heat`, `ArmorRating_Sharp`, `ArmorRating_Blunt`, `Insulation_Cold`.
