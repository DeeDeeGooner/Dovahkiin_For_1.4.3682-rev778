# RISKS.md — what is hard, expensive, or impossible

Ranked by how much they change the plan. Every entry has a cheaper alternative that keeps the
fantasy. Facts cited here are verified in `COMPAT.md`.

---

## 1. Nordic crypts — **risk massively reduced.** Was the biggest engineering risk; now it isn't

**What the bundle assumed:** `SPEC.md §7.3` and `ROADMAP.md` Phase 5 say the ordered
entry → catacombs → sanctum layout, the terminal word wall and the sealed treasure room require
a bespoke `GenStep` written from scratch, and call it "the single largest engineering risk in
the project."

**What is actually on disk:** Vanilla Expanded Framework ships **KCSG**, and Dragon's Descent
already uses it in 1.4 to generate large authored dragon lairs — one of its layouts is a 110 KB
hand-drawn dungeon. KCSG gives us:

- `KCSG.GenStep_CustomStructureGen` — place an authored layout, XML only
- `Dialog_ExportWindow` — **build the crypt in-game in dev mode and export it**. No code.
- `KCSG_UndergroundRoom` — underground rooms
- `linkWithSite` — bind the genstep to a `SitePartDef`, exactly as DD's `Questscript.xml` does

Because the layout is **hand-drawn**, every guarantee in §7.3 is satisfied *by construction*.
There is no algorithm to fight: if the word wall is at the end of the map you drew, it is at the
end. The ordering, the sealed treasure room, the dragon priest's chamber — all just placement.

**Revised cost:** this stops being an engineering problem and becomes an **authoring** problem
— time spent building crypts in-game and exporting them. That is work the user can even do
themselves, and it is far more predictable than a procedural generator.

**Residual risk:** KCSG is VEF. See §3 below for what happens without it.

**Recommendation:** drop the custom `GenStep` plan. Build crypts as KCSG `StructureLayoutDef`s.
Author 3–4 layouts per tier and pick randomly, so crypts vary without being procedural.

---

## 2. Shout backbone — **settled, no dependency needed**

`CLAUDE.md` asks to be told before Phase 2 if shouts need a hard dependency on VEF or JecsTools.
**They don't.**

Dragon's Descent implements dragon breath as plain vanilla `AbilityDef` with
`<verbClass>Verb_CastAbility</verbClass>`, on **animal** pawns, in 1.4 — proving the vanilla
ability system works for everything the mod needs, including §4.6 "dragons shout too".

**Recommendation:** vanilla `AbilityDef` + `CompProperties_AbilityEffect` as the backbone.
Layer `VFECore.Abilities.AbilityExtension_Projectile` / `_Explosion` for richer cone and
projectile behaviour **behind `MayRequire`**, degrading to a plainer vanilla effect without VEF.
**JecsTools `AbilityUser` is not needed** and should not be adopted — it is an older parallel
system and adding it buys nothing vanilla doesn't already do.

---

## 3. VEF becomes a soft requirement — and that collides with `CLAUDE.md` invariant 5

Invariant 5 says the mod must play on the baseline environment with dragon content dormant but
not broken. But if crypts (§1 above) are KCSG structures, then **without VEF there are no
crypts**, and §7.3 calls crypts "the primary way the player grows their shout library". No
crypts → no words → no shouts → the mod is an empty shell on baseline. That is broken, not
dormant.

**Cheaper alternative that preserves the fantasy:** make **dragon mounds (§7.1) the baseline
word source.** They are small, open-air, and need no structure generator at all — a stone
object, a word wall, and a guardian dragon placed by an ordinary vanilla site part. Crypts
become the *rich* word source when VEF is present, mounds the *guaranteed* one.

**Recommendation:** build mounds first (they are simple and they de-risk the whole progression),
crypts second. Declare VEF a soft dependency in `About.xml` via `MayRequire`, and state plainly
in the workshop description that crypts need VEF. VEF is already installed here and is a hard
dependency of Dragon's Descent, so in practice this user always has it.

---

## 4. The mana reward — **solved, but one decision remains**

`SPEC.md §5.2`'s original "+2 max mana" was not implementable: RWoM mana is a 0–1 `Need`, not an
integer pool. **But RWoM has a supported XML mechanism** — hediff stages carrying
`<enchantments><maxMP>` / `<maxSP>` / `<mpRegenRate>`, as fractional multipliers. This maps
cleanly onto `Hediff_DragonSoulAttunement`, whose severity is already the soul count. No
reflection, no assembly reference.

**Remaining problem:** those needs only exist on pawns carrying `TM_MagicUserHD` or
`TM_MightUserHD`. An ordinary colonist who awakens has no mana bar to enlarge — and `SPEC.md §1`
lets any pawn awaken. **This is OD-9** and it is the one decision that most changes what the mod
*is*. See `DECISIONS.md`.

---

## 5. The scenario's hostile-settlement start — cheaper than costed, two ways

`ROADMAP.md` Phase 7 priced the faithful version as "comparable in cost to Phase 5". Two
verified facts reduce that:

- `ScenPart` exposes `GenerateIntoMap(Map)`, so the **reduced** option (b) — pre-placed hostile
  buildings and a hostile force already on the map — is a straightforward ScenPart. No custom
  map generator.
- KCSG ships `GenStep_Settlement` + `SettlementLayoutDef` + `SymbolResolver_Settlement`, a
  working settlement fabricator. So even the **full** option (a) is largely configuration rather
  than new engineering, when VEF is present.

**Recommendation:** build (b) as the shipped version — it is cheap, has no dependency, and the
README already judged it "~80% of the feeling". Treat (a) as a VEF-gated upgrade after Phase 7
is stable. Genuinely starting *inside another faction's settlement map* remains impossible in
1.4 and always will be; both options simulate it on a player-owned tile.

---

## 6. Dragon hostility toward a tamed dragon — still genuinely hard

Unchanged by reconnaissance, and still deserves its own sub-task. Dragon's Descent dragons are
**animals with wildness 0.88–0.987**, so they are tameable and `SPEC.md §6.2` is a real
situation, not a hypothetical. 1.4 has no per-target hostility for a player-faction animal:
manhunter and berserk states target everyone nearby and break the tame bond, which is not what
§6.2 describes.

**Cheaper alternative:** rather than a true per-target hostility system, give the dragon a
custom `MentalStateDef` with a forced target (the Dovahkiin) that **suspends** rather than
breaks the tame bond, and ends when the Dovahkiin leaves the map or the dragon is downed. It
reads identically to the player and avoids rewriting faction logic.

**Do not cut this** — `ROADMAP.md`'s never-cut list is right that it is core to the fantasy.

---

## 7. Word-wall content volume — unresolved and it gates Phase 5's size

**This is OD-10.** If a level-3 shout needs all three of its words, the §4.4 list of 21 shouts
needs **63 word walls**; if one word unlocks a shout, it needs 21. §7.3 gives one wall per
crypt. That is a 3× swing in how much world content Phase 5 must produce, and with §1 above
turning crypts into hand-authored work, it is a 3× swing in *hours*, directly.

**Recommendation:** answer OD-10 before Phase 2, and if the faithful reading is chosen, trim
§4.4 hard. Twenty-one shouts × 3 words is not a realistic content budget for this project; ten
well-built shouts is a better mod than twenty-one thin ones. The `ROADMAP.md` cut list already
orders the stretch shouts for removal.

---

## 8. Rimedieval will thin the crypt loot, not block the crypts

Verified: Rimedieval's quest/incident/genstep removal is a hardcoded blocklist, so our content
passes through untouched. But `IsAllowedForRimedieval()` strips `ThingDef`s at
`techLevel >= Industrial` from thing lists. `SPEC.md §7.3` wants "loot drawn from the modlist's
equipment pools" scaled to danger — with Rimedieval active, the high-tier end of those pools is
gone, so a tier-5 crypt may reward the same medieval gear as a tier-1.

**Cheaper alternative:** do not lean on generic pools. Give each crypt tier an explicit
`ThingSetMakerDef` weighted toward Medieval Overhaul's high-end gear, plus mod-specific rewards
(dragon priest masks, Nordic weapons) that we control and that carry `techLevel Medieval`
so they always survive the filter.

---

## 9. Soul Tear's dead puppet — **risk resolved by design change**

**Superseded 2026-07-25.** This was written as the highest save-corruption risk in the mod. The
design has changed and the risk is now largely gone.

**The old plan** moved a hostile pawn into the player faction and *restored* it afterwards. That
restore was the dangerous part: a puppet surviving a reload while still player-faction, with its
hediff gone, is an unremovable pseudo-colonist. It required a correct restore-or-kill on seven
different exit paths, one of which was save → load.

**The new plan (user's suggestion, adopted): the puppet is always doomed.** It joins the player
faction and receives an incurable, untendable, non-removable hediff that kills it when it
expires. It is never restored, because it never survives.

That collapses the whole failure surface: every exit path already ends in death, and the only
thing that has to survive a reload is an ordinary hediff, which uses RimWorld's normal,
well-tested serialisation. There is no bespoke state to lose.

**Residual risk, much smaller:** a player-faction pawn that somehow keeps the puppet marker
after losing the hediff. Mitigated by a load-time safety sweep that kills any such pawn and logs
loudly — it should never fire.

**Still worth writing the reload test first**, but it is now a confirmation rather than a gate.

See `SPEC.md §4.4f` for the full rules.

---

## 10. Dragon Aspect's overlay — the most likely thing to break under RocketMan

`SPEC.md §4.4d` now makes the overlay a shipping requirement, not a nice-to-have. Pawn rendering
is exactly where RocketMan does its most aggressive caching, so a Harmony patch on
`PawnRenderer` is the single most fragile approach available.

**Cheaper alternative, in preference order:** (1) a hediff-driven overlay using the game's own
pawn-overlay path; (2) an invisible apparel item carrying the graphic, which RimWorld already
renders, caches and rotates correctly for free; (3) a `PawnRenderer` patch only as a last resort,
and only after testing with RocketMan active.

Two working references are already installed: RimWorld of Magic's spell visuals and VEF's
`GraphicCustomization.dll`.

**Do not silently downgrade this to a stat buff** — `ROADMAP.md` Phase 2 makes failure a
stop-and-report.

---

## 11. Performance — not yet assessed

`TODO(COMPAT)`. RocketMan ships five assemblies (`Cosmodrome`, `Gagarin`, `Proton`, `Soyuz`,
`XmlDiffPatch`) and aggressively caches. Melee Animation ships a large animation state machine.
Neither has been enumerated yet. Neither blocks Phase 0 or Phase 1, but both must be resolved
before Phase 2 ships casting.

**Standing mitigation regardless:** no per-tick work. Soul absorption, shout cooldowns and
hostility checks all belong on `TickRare` or event hooks. `CLAUDE.md` already mandates this.

---

## Summary — what changed from the bundle's assumptions

| Item | Bundle's estimate | After reconnaissance |
|---|---|---|
| Nordic crypts | Biggest engineering risk in the project | **Authoring work, not engineering.** KCSG + in-game exporter |
| Shout backbone | Open question; may need VEF or JecsTools hard dep | **Settled** — vanilla `AbilityDef`, no dependency |
| Scenario start | Custom map generator, ~Phase 5 cost | **A `ScenPart`.** Much cheaper |
| Mana reward | "Route through RWoM's real fields" | **Solved** — hediff `<enchantments>`, XML only |
| Dragon hostility | Own sub-task, budget real time | **Unchanged** — still the hardest remaining piece |
| Word-wall volume | Not costed | **New risk** — 3× swing, gates Phase 5 |
