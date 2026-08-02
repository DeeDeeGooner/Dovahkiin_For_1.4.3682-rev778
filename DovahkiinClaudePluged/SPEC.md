# SPEC.md — Dovahkiin

## 0. The fantasy, in one paragraph

A colonist is nobody. Then a dragon dies, and the sky screams a name, and it is theirs.
From that moment the player is not managing a stronger pawn — they are managing a person the
world has started reacting to. Strangers seek them out. Duellists come to test them. Dragons
hate them on sight, allied or not. Every dragon they kill leaves them a little less mortal in
ways that show up in what they *know* and what they can *do*, never in how big they are. The
arc runs from "a colonist who can shout someone over" to "the thing Alduin came back for."

Two design rules follow from this and they override any specific number below:

- **Nothing is strong at the moment of awakening.** The trait alone is a social event and a
  narrative promise. Combat relevance is earned across many dragon kills.
- **Power is spiritual, not physical.** Mana, stamina, technique, knowledge, presence. Never
  body size, never a race swap, never flat HP.

---

## 1. Scope of who can be a Dovahkiin

Any pawn of any race, xenotype, gene set, or faction origin. Awakening logic must never filter
on race or xenotype — modded ones included. It filters on the rules in §3 and nothing else.

Non-humanlike pawns (animals, mechs, dragons) are excluded. Children may awaken but the trait
stays dormant (§3.5).

---

## 2. The registry — the single authority

`GameComponent_DragonbornRegistry`. All state below is saved via `ExposeData` with
backward-safe defaults.

| Field | Meaning |
|---|---|
| `dovahkiin` | The current Dovahkiin, or null. **Persist with `Scribe_References.Look<Pawn>`, not a raw Thing ID** — the pawn may be caravanning, kidnapped, or a world pawn, and an ID cannot be resolved back without scanning every map. Ensure they are registered as a world pawn so the reference survives. |
| `dovahkiinEverExisted` | Has the world ever had one |
| `dovahkiinDeaths` | How many have died |
| `lockedOutPawnIds` | Pawns who rolled and failed their one awakening chance (§3.3) |
| `alduin` | The living **boss** Alduin (`Scribe_References.Look<Pawn>`), or null. Scripted cameos use a separate `Alduin_Scripted` def and are never stored here. |
| `alduinState` | `Unspawned / Alive / Dormant(revivalTick) / SlainForever` |
| `dragonEventFiredCount` | How many times the once-per-save awakening event has fired (a counter, not a bool — see OD-1) |
| `alduinFirstAppearanceDone` | For the scenario's opening appearance |
| `strangerQuestFired` | The once-per-game Dovahkiin stranger arrival (§9.4) |
| `wordsDiscoveredWorld` | Which words of power have been found on word walls anywhere |
| `treasureMapsSold` | Sites revealed by the map trader event |

**Public API — the only way to change Dovahkiin status:**

```
bool TryAwaken(Pawn p, AwakeningCause cause)   // returns false if a Dovahkiin exists
void NotifyDovahkiinDied(Pawn p)
bool IsDovahkiin(Pawn p)
Pawn CurrentDovahkiin { get; }
```

No other code path may add or remove the trait. If you need a second one, you have a design
bug — refactor.

**On death of the Dovahkiin:** see Open Decision **OD-1**. Do not implement until answered.

---

## 3. Becoming the Dovahkiin

### 3.1 The trait

`Trait_Dovahkiin`, one degree, never rolled at pawn generation, never purchasable, never
appearing in the trait pool. It is granted exclusively by the registry.

Alongside it the pawn gets:

- `Hediff_DragonSoulAttunement` — permanent, severity = number of souls absorbed. Carries all
  stat offsets and is the single place Dragon Soul power lives.
- `Hediff_TheVoice` — holds known words, word levels, unspent souls, shout cooldown state.
- A **title**: `Dovahkiin` — displayed in the pawn's name line, bio, and inspect string.
  **Do not use `RoyalTitleDef`.** It is granted through `Pawn_RoyaltyTracker.SetTitle(faction,…)`,
  is inherently faction-bound, and drags in permit points plus apparel/bedroom obligations and
  the Royalty tab. Build a custom title display instead: a name-suffix/bio system driven off
  `Hediff_DragonSoulAttunement`, upgradeable later (`Dovahkiin, World-Eater's Bane`, §6.4).
- A **backstory** override: the pawn keeps its original childhood; its adulthood gains a
  suffixed or replaced backstory reflecting the awakening cause. Write at least four:
  awakened-by-slaughter, dragonblood-heir, the-stranger-who-arrived, prophecy-scenario.

### 3.2 Route A — "A Dragon!!!" (the awakening event)

> **REWRITTEN 2026-08-01. IT IS NO LONGER A RARE ROLL — IT IS A CERTAINTY ON A RISING TIMER.**
>
> The user, deciding this as the deliberate counterweight to §3.3's harsher lockout: *"change it
> from a very rare event to an event bond to happen (so instead of keeping it's chance of
> happening constant, it should increase overtime more and more until it happens). So yes it is
> harsher now since the punishement of losing a dragonborn is heavier, but on the other you are
> guaranted the chance to get one."*
>
> **THE TWO RULES ARE A PAIR AND MUST NOT BE SEPARATED.** §3.3 makes losing the Dovahkiin close
> the door on everyone who knew him; this guarantees the door opened in the first place. Softening
> either alone breaks the bargain — a rare event plus a harsh lockout is a colony that may never
> see a Dragonborn at all, and a certain event plus a soft lockout is no loss worth fearing.

**Once per save, and bound to happen.** Eligibility is: no Dovahkiin currently exists **and** the
event has never fired. `dragonEventFiredCount` stays a counter rather than a bool, so the
once-per-slot behaviour is one comparison away if this is ever revisited — but the shipped rule is
once, ever.

**THE RISING CHANCE.** Rolled once per in-game day. Before `graceDays` nothing happens at all; a
colony three days old should not be fighting a dragon. After it, the per-day chance climbs
linearly and never falls:

```
chance(day) = clamp01( baseChancePerDay + rampPerDay * (day - graceDays) )
```

Three candidate tunings, with the day by which the event has fired for that share of colonies.
**Computed, not guessed** — a RimWorld year is 60 days:

| tuning | per-day chance | 50% | 75% | 90% | 99% |
|---|---|---|---|---|---|
| gentle | 0.5% rising +0.08%/day after day 20 | day 57 | 74 | 91 | 123 |
| **middle — CHOSEN BY THE USER 2026-08-01** | **0.8% rising +0.15%/day after day 20** | **day 45** | **57** | **70** | **92** |
| brisk | 1.0% rising +0.40%/day after day 15 | day 31 | 38 | 46 | 59 |

**Middle is the recommendation**: half of colonies meet their dragon inside the first year, nearly
all inside two, and it is still possible to be the colony that waited. Brisk makes it routine;
gentle risks a player finishing a run without ever seeing the mod's premise.

All three numbers belong in `DovahkiinTuningDef` — `dragonEventGraceDays`,
`dragonEventBaseChancePerDay`, `dragonEventRampPerDay`.

**Guaranteed to FIRE, not guaranteed to SUCCEED.** The dragon still has to die on the map. A
colony that loses the fight has had its one event and gets no Dovahkiin from this route — which is
the whole reason §8.1 says to tune the dragon so an unprepared colony can lose. The guarantee is
that the *opportunity* arrives, and the user's own words are "guaranteed the chance".

**Not built.** The dragon is Phase 3, and the timer belongs with it — a rising chance with nothing
to spawn is a knob nobody can turn.

An enraged dragon assaults the colony. If it dies on the map, a colonist awakens (see the
resolution order below). See §8.1.

Firing this event **permanently locks out every living dragonblooded pawn** (§3.3) — the
universe made its choice. Add them all to `lockedOutPawnIds` at the moment of awakening.

**Resolution order against §3.3 — specify this, do not leave it to evaluation order.** When the
§8.1 dragon dies, both this route and §3.3's heir roll are live at the same instant: no
Dovahkiin exists yet, so §3.3's precondition is satisfied too. Resolve as follows:

1. Every eligible dragonblood pawn on the map rolls §3.3 first, in a deterministic order.
2. If one succeeds, **they** become the Dovahkiin and the random-colonist awakening does not
   fire. The winner is *not* added to `lockedOutPawnIds`.
3. Only if every heir roll fails does a random eligible colonist awaken.
4. Either way, all *remaining* dragonblood pawns are then locked out.

`TryAwaken` returning false must never be the thing that decides which pawn got it.

### 3.3 Route B — Dragonblood heirs

> **OD-2 IS ANSWERED, AND IT REVERSES WHAT THIS SECTION USED TO SAY. 2026-08-01, the user:**
>
> *"yes all living heirs and pawns also burns their roll if a dovahkiin is alive. Think of it as
> if a dovahkiin is alive, everybody present and was present during his time burns their roll.
> Once the dovahkiin dies: if a dragonblood heir appears ONLY AFTER that, whether through birth
> or anything else, they then have that one time roll chance on any dragon death they witness.
> Normal pawns don't have that roll, they can only get the trait through the 'dragon!!!' event."*
>
> The old rule was the opposite — heirs did *not* burn their roll while a Dovahkiin lived,
> explicitly "to preserve the drama of the succession". **That reasoning is superseded. Do not
> restore it.**

Children of a Dovahkiin get the `Dragonblood` trait (§10). The rule is about **WHEN A PAWN CAME
INTO CONTACT WITH THE LIVING DOVAHKIIN**, not about what happens at the moment a dragon dies:

**1. While a Dovahkiin is alive, presence burns the roll.**
Any pawn present during his lifetime — heir or not — is locked out permanently. Not "when a
dragon dies", not "when they roll": **being there during his time is itself the disqualification.**
The universe already had its Dragonborn while they stood next to him.

**2. Only pawns who appear AFTER his death can ever roll.**
A dragonblood heir who arrives *after* the Dovahkiin dies — by birth, by joining, by any route —
carries a live, one-time roll. They spend it on **any dragon death they witness**: a small chance
(default **2%**, tunable) to awaken, and on failure they are locked out for good.

**3. Ordinary pawns never roll at all.**
Being non-dragonblood is not a small chance, it is *no* chance. An ordinary colonist can only
become the Dovahkiin through the §3.2 dragon event, or by arriving as one (§3.4), or by scenario
(§11).

**What this means for implementation — none of it is written yet, and it is not what the registry
currently does:**

- `LockOutAllDragonblood` fires today only at the moment of awakening. Under this rule the lockout
  must also catch **every pawn who joins the colony while a Dovahkiin lives** — so it needs a hook
  on pawn arrival/birth, not only on awakening.
- The registry must be able to answer **"did this pawn exist alongside the living Dovahkiin?"**
  The cheapest correct form is to keep locking out on contact rather than trying to reconstruct
  history later: lock a pawn out the moment they and a living Dovahkiin are both present.
- It must also lock out **ordinary** pawns, not just dragonblood ones, or rule 3 has to be
  enforced somewhere else and the two places can disagree.

> **⚠ UNRESOLVED CONFLICT WITH OD-1 — ASK BEFORE BUILDING.** The user's message above describes
> the "A Dragon!!!" event as *"only happens once per save"*. **§3.2 and the shipped code say
> otherwise**: it fires once per *Dovahkiin slot*, which is why `dragonEventFiredCount` is a
> counter compared against `dovahkiinDeaths + 1` rather than a bool.
>
> This may be an incidental phrasing rather than a reversal — it was said in passing while making
> a different point. **It matters, because combined with the rule above it decides whether a
> colony that loses its Dovahkiin can ever produce another from within:** if the event is truly
> once per save, and everyone who lived alongside him is locked out, then the only remaining
> routes are an heir born *after* his death, or an outsider arriving (§3.4). That is coherent and
> quite dramatic — but it should be chosen, not inherited from a parenthesis.

### 3.4 Route C — Arrival

A Dovahkiin can arrive from outside, via **two independent routes**, only if no Dovahkiin exists:

- **The stranger quest** (§9.4) — the rare, narrative one.
- **A wanderer-joins incident** (§8.7) — the quiet one, no quest, no fanfare.

Design note on the sanguophage parallel: Biotech surfaces sanguophages through **two quests**
plus scenario start and Empire noble generation — **there is no sanguophage incident**
(verified: no match under any `IncidentDefs` folder in `Data\Biotech`). So do not "mirror the
incident"; mirror the *shape*: more than one discovery route, at least one of them a quest,
rarity on par with the sanguophage quests.

The two real quests, verified on disk in `Data\Biotech\Defs\QuestScriptDefs\`:

| defName | File | Rarity |
|---|---|---|
| `SanguophageMeetingHost` | `Script_SanguophageMeetingHost.xml` | `rootSelectionWeight 0.5`, `expireDaysRange 2` |
| `SanguophageShip` | `Script_SanguophageShip.xml` | `rootSelectionWeight 0.5`, `minRefireDays 200` |

**Do not go looking for quest names like "Bloodthirsty Parley" or "Sanguophage Transport" —
they are not def identities.** Display names are generated per instance from `questNameRules`
(adjective × noun, e.g. `meetAdjective->bloodthirsty` combined with `meetNoun->parley`), so one
quest def has eighty possible names and grepping the disk for any of them finds nothing.

### 3.5 Children

A child may hold the trait but `Hediff_TheVoice` stays dormant until adulthood: no shouts, no
soul absorption. Souls that would be absorbed are banked and granted on coming of age.

---

## 4. The Voice — shouts

### 4.1 Structure

Every shout has **three words**. A word is discovered by reading a **word wall** (§7).
Discovery is world-wide and permanent — once found, that word exists in
`wordsDiscoveredWorld` and any Dovahkiin can unlock it.

A discovered word starts at **level 0: known but unusable.** The player spends **1 Dragon Soul
per level**, up to **level 3**. Levels are per-shout, sequential, and irreversible.

**Words gate levels. OD-10 is answered: the Skyrim-faithful rule.**

> **Shout level N requires N discovered words *and* N souls spent.** A shout with one word
> discovered can never exceed level 1, no matter how many souls are spent on it. Words are
> world state (`wordsDiscoveredWorld`); levels are per-pawn.

So a fully-mastered shout costs **3 word walls + 3 souls**. The §4.4 list is trimmed to **eleven
core shouts** plus Dragonrend accordingly — **33 word walls**, which is the world-content budget
for §7. Dragonrend's words come from the §9.3 quest chain, not walls. Do not grow the shout list
without re-costing §7.

Note the deliberate scarcity: maxing all eleven costs **33 souls**, and §13 puts "a legend" at
20+. The player is meant to specialise, not complete the list.

| Level | Effect |
|---|---|
| 0 | Known. Displayed greyed out. Cannot be cast. |
| 1 | Weak version, short cooldown |
| 2 | Moderate version, medium cooldown |
| 3 | Full version, long cooldown |

### 4.2 Cooldown model — shared Thu'um recovery

A **single shared cooldown** across all shouts, whose length is set by the shout just used.
This is Skyrim-faithful and it prevents the player from chaining five shouts in one fight.
On top of it, casting applies `Hediff_VoiceStrain` (stacking, decaying) that lengthens
recovery if the pawn shouts repeatedly in a short window. Strain is the anti-spam valve;
tune it rather than nerfing individual shouts.

### 4.3 Casting

Shouting is a **verbal, directional, telegraphed** act: a brief wind-up, a voice line in the
dragon tongue on the pawn, a shockwave/cone VFX in the facing direction, and a hearing-range
alert. It should be interruptible by downing/stunning. It requires the pawn to be conscious
and capable of talking — a pawn with a destroyed jaw or in a coma cannot shout, and this
should be an explicit, visible reason in the UI.

### 4.4 The shout list

**OD-10 is answered, so this list is trimmed to eleven core shouts plus Dragonrend.** Eleven ×
three words = **33 word walls**, which is §7's content budget. Dragonrend's three words are
granted by the §9.3 quest chain and are **not** walls, so they add nothing to that figure. The
deferred entries in §4.4c are **not** in scope; promoting any of them costs three more walls and
a re-cost of §7.

### 4.4a The eleven core shouts — build all of these

| Shout | Words | L1 → L2 → L3 |
|---|---|---|
| **Unrelenting Force** — Fus Ro Dah | Fus / Ro / Dah | Stagger one target → knockback cone, short → knockback cone, long, brief stun, scatters light items |
| **Fire Breath** — Yol Toor Shul | Yol / Toor / Shul | Short flame cone → longer, ignites → wide, sustained, ignites ground |
| **Frost Breath** — Fo Krah Diin | Fo / Krah / Diin | Chill cone, slows → damage + heavy slow → freeze-solid chance, Thu'um drain on hit |
| **Whirlwind Sprint** — Wuld Nah Kest | Wuld / Nah / Kest | Short dash → medium dash → long dash, ignores minor obstacles |
| **Slow Time** — Tiid Klo Ul | Tiid / Klo / Ul | **REWORKED: self-only extreme haste.** Never a global time effect. Big move + melee/ranged cooldown boost on the caster only, short → medium → long. Do not touch `Find.TickManager`. |
| **Become Ethereal** — Feim Zii Gron | Feim / Zii / Gron | Brief invulnerable + cannot attack → longer → longer still, movement retained |
| **Dragon Aspect** — Mul Qah Diiv | Mul / Qah / Diiv | **The mod's visual showpiece — see §4.4d. Not shippable without the overlay.** L1 armour + melee technique → L2 adds shout recovery reduction → L3 adds a ground shockwave on melee hit. Long cooldown, long duration, once-per-fight feel. |
| **Marked for Death** — Krii Lun Aus | Krii / Lun / Aus | Target takes increased damage + slow armour decay → stronger → strongest, lingers |
| **Storm Call** — Strun Bah Qo | Strun / Bah / Qo | **Strikes may only target hostile pawns that are *outdoors* — under open sky, in an unroofed cell.** Never colonists, never tamed animals, never neutral visitors, never player buildings, and never anything under a roof. Short duration, few strikes → more → many, longer. See §4.4e — the outdoor rule also settles the fire question. |
| **Soul Tear** — Rii Vaaz Zol | Rii / Vaaz / Zol | **Single target, no cone, no splash.** Heavy direct impact damage, plus a chance to raise the target as a **dead puppet** — see §4.4f. L1 damage only → L2 damage + puppet chance → L3 heavier damage, higher chance, longer puppet. The most powerful shout in the mod; place its three words in high-tier crypts only. |
| **Clear Skies** — Lok Vah Koor | Lok / Vah / Koor | Briefly clear local bad weather → longer → longest. Cheap, flavourful, good early pick. |

### 4.4d Dragon Aspect — the visual, in detail

**This is the mod's showpiece and it is not shippable as a stat buff with no overlay.** If the
overlay cannot be made to work, that is a stop-and-report, not a silent downgrade.

TES5-accurate reference — match these, translated into RimWorld's flat top-down style:

- **Spectral dragon-scale armour** layered over the pawn: translucent overlapping plates,
  brightest at the edges, semi-transparent over the body so the pawn's own apparel still reads
  underneath.
  > **AMENDED at the user's request, 2026-07-29. This line said "burnished bronze-gold".**
  > The shipped armour runs **bronze at the shoulders and chest into Unrelenting Force's exact
  > blue (95,165,240) at the waist**. That blue is not invented: it is the one Dragon Aspect's
  > own shout icon already uses at its head, on the reasoning that Dragon Aspect is the blue
  > shout and the fire shout at once — so the overlay matches its own icon. The art was signed
  > off in play. **Do not "restore" bronze-gold.**
- **Shoulder spurs / vestigial wing shapes** breaking the pawn's silhouette — this is what makes
  it recognisable at colony zoom. The silhouette change matters more than the texture detail.
- **Ember glow** — a soft amber rim-light, and slow drifting motes rising from the pawn.
- **On cast:** a brief expanding ring and a downward slam of light, not a puff.
- **At L3 only:** a visible ground shockwave ring on each melee hit connecting.

Implementation notes (decide in Phase 2, show the design first):

- Prefer a **hediff-driven pawn overlay** or an invisible apparel layer over Harmony-patching
  `PawnRenderer`. RocketMan patches pawn rendering aggressively and a render patch is the most
  likely thing in this mod to break under it.
- Two on-disk references for pawn-overlay VFX in this exact modlist: RimWorld of Magic's spell
  visuals (`TorannMagic`) and VEF's `GraphicCustomization.dll` / animation utilities.
- The overlay must follow the pawn through movement, drafting, downing and rotation, and must
  vanish cleanly when the hediff expires or the pawn dies.

Budget real time for this. `ROADMAP.md` Phase 2 already calls it its own sub-task.

### 4.4e Storm Call — the outdoor rule

Targeting is a **hard requirement** and is fully controllable, because we write the strike
rather than reusing the vanilla weather event. A cell is a legal strike target only if **all**
of these hold:

1. It contains a pawn hostile to the player, **and**
2. that pawn is **not** a colonist, not player-faction, not tamed, not a neutral visitor, **and**
3. the cell is **unroofed — open sky, no roof of any kind above it**.

Rule 3 is the user's addition and it is the cleanest part of the design: it makes the shout
useless indoors, which is thematically right for calling a storm, and it **resolves the fire
question that §4.4 previously left open**. Strikes cannot land inside a base, so they cannot
ignite a stockpile, a wooden wall, or a roofed corridor. Ignition on open outdoor terrain near
enemies is acceptable and is left on.

The three fallback resolutions the spec previously listed are no longer needed. Do not cut this
shout, and do not reintroduce indoor targeting for "coverage".

### 4.4f Soul Tear — the dead puppet

Single target. No cone, no chain, no splash. Heavy direct damage on impact, and on a roll
(scaling with level) the target rises as a **dead puppet**:

- It **fights for the colony** for a limited time — a tunable duration in `DovahkiinTuningDef`,
  scaling with shout level.
- When the timer expires it **dies**, permanently. It is never a recruit, never a permanent
  colonist, never breedable, and cannot be arrested, rescued, or healed out of the state.
- It is visibly marked as a puppet — a distinct overlay/tint and an inspect-string line — so the
  player never mistakes it for a real ally.

**Implementation — the puppet is always doomed, and that is what makes it safe.**

The original design here demanded the pawn's faction be *restored* on every exit path, which is
the part that could corrupt a save: a puppet surviving a reload while still player-faction, with
its hediff gone, is a permanently broken pawn nobody can arrest, banish or kill cleanly.

**Remove the restore path entirely.** A puppet never goes back to what it was — it dies. So:

1. On a successful roll, the target is moved to the player faction, and given
   `Hediff_DeadPuppet`: **incurable, untendable, non-removable**, with a fixed lifetime.
2. When that hediff expires, it **kills the pawn**. There is no other outcome.
3. Every exit path therefore already terminates: timer expiry kills it; being killed early is
   already death; being downed leaves the hediff ticking, so it still dies; leaving the map
   still carries the hediff; the caster dying or losing the trait changes nothing, because the
   puppet's death does not depend on the caster.
4. **Save → load is safe by construction.** Hediffs serialise through the normal, well-tested
   path and keep ticking. Nothing bespoke has to survive a reload.

Consequences to enforce:

- ~~Only valid on **hostile** pawns. Never colonists, never player-faction, never tamed animals,
  never a pawn already puppeted.~~
  **AMENDED 2026-07-28 by the user, and built this way.** Soul Tear may be turned on **anyone** —
  hostiles, neutrals, allies and your own colonists. Only two exclusions remain: the caster
  themselves, and a pawn **already puppeted** (which would stack a second doomed timer on a pawn
  already dying to one).

  Consequences that follow, and are implemented:
  - Tearing a neutral or an ally angers their faction through the normal damage path. That is
    RimWorld's own behaviour and is correct — no special handling.
  - **Tearing one of your own is an execution and is mourned as one.** The puppet normally drops
    out of the player faction a tick before dying precisely to suppress colonist-death grief;
    that suppression is skipped when the victim was already yours. Without it the shout would be
    a way to murder a colonist that nobody noticed.
- Never recruitable, never arrestable, never rescuable, never healable out of the state. It
  cannot be traded, married, or converted, and its death must not trigger colonist-death mood.
- Visibly marked — a distinct tint and an inspect-string line saying how long it has left — so
  the player never mistakes it for a real ally or plans around keeping it.
- Add a startup/load safety sweep: any player-faction pawn carrying the puppet marker but no
  live `Hediff_DeadPuppet` is killed. This should never fire; if it does, something upstream is
  wrong and it must be logged loudly.

On-disk precedent worth reading first: RimWorld of Magic's `TM_RaiseUndead` and its
`TM_UndeadHD`/`TM_UndeadStageHD` hediffs, which do use `SetFaction`/`SetFactionDirect` for a
servant that persists until destroyed. Note that RWoM's `TM_Dominate` is **not** a charm — it is
a fear/panic debuff driven by `mentalStateGivers` — so it is not the model here.
The Profaned's undead pawns are also relevant (`COMPAT.md §7`).

### 4.4b Dragonrend — Joor Zah Frul — quest-locked

OD-5 is answered: build it as a real shout, **not** narrative flavour. Words are not found on
ordinary word walls; all three are granted by the §9.3 World-Eater chain, and it is the gate on
reaching Alduin. Effect: grounds a flying dragon and suppresses its shouts — L1 brief, L2
longer, L3 long enough to matter in the Alduin fight. Alduin-relevant by design; it should feel
wasted on an ordinary dragon.

### 4.4c Deferred — out of scope, do not build

Cut by OD-10 to keep the world-content budget at 33 walls. Listed so the intent is recorded, not
as a backlog: **Ice Form** (Iiz Slen Nus) · **Dismay** (Faas Ru Maar) · **Aura Whisper** (Laas
Yah Nir) · **Disarm** (Zun Haal Viik) · **Elemental Fury** (Su Grah Dun) · **Animal Allegiance**
(Raan Mir Tah) · **Kyne's Peace** (Kaan Drem Ov) · **Call of Valor** (Hun Kaal Zoor) · **Drain
Vitality** (Gaan Lah Haas) · **Throw Voice** (Zul Mey Gut).

*Soul Tear was on this list and was promoted back into §4.4a at the user's request, which is why
the budget is 33 walls and not 30.*

Promoting any of these costs three more word walls and a re-cost of §7. Ask first.

### 4.5 Non-Dovahkiin shout users

Two sources, both capped at **level 1, permanently**, and unable to absorb souls:

1. **Veteran mortals — very rare.** A generated pawn (raider, mercenary, wanderer, trader
   guard) with high combat skill and age may carry a single level-1 shout: **Unrelenting
   Force**, **Fire Breath**, or **Frost Breath** only. Target rate: well under 1% of eligible
   generated pawns. This should feel like a story, not a mechanic.
2. **The undead and the priests — shout count scales with tier, and it is a *chance*, not a
   guarantee.** The draugr pool is **Unrelenting Force and Frost Breath only** — draugr never
   breathe fire; that is a dragon's province. Since the pool is two, "knows two" means it knows
   both, and two is the hard ceiling for any draugr.

   | Draugr tier | Shouts known | Roll |
   |---|---|---|
   | **Draugr** (common) | **0 — never** | none. The trash tier must stay trash. |
   | **Draugr Wight** | 0 or 1 | low chance of one shout |
   | **Draugr Overlord** | 1, rarely 2 | near-certain one; small chance of both |
   | **Draugr Deathlord** | 1 or 2 | certain one; good chance of both |

   Roll once at pawn generation and store it — never re-roll on load, or a save-scummed crypt
   changes difficulty. All chances live in `DovahkiinTuningDef`.

   The point of the ladder is that hearing a Thu'um in a crypt *means something*: it tells the
   player something worse than a common draugr just woke up, before they can see it.

   **Dragon Priests** are the exception to the level cap — they may use a **level-2** shout and
   are the most dangerous non-dragon shout users in the mod.

If a mortal shout-user is captured and recruited, they keep their one level-1 shout. They never
gain more. They are not the Dovahkiin and never can be while one lives.

### 4.6 Dragons shout too

**This is a hard requirement, not flavour.** A dragon that does not shout is not a dragon.

**Normal dragons get exactly three shouts, and no others:**

| Shout | Notes |
|---|---|
| **Fire Breath** — Yol Toor Shul | The default. Give it to fire-flavoured kin. |
| **Frost Breath** — Fo Krah Diin | Give it to frost-flavoured kin. |
| **Unrelenting Force** — Fus Ro Dah | Every dragon may have this one. It is what makes a dragon dangerous at range without being a damage check. |

**Exactly one of the three per dragon — never two, never all three.** A dragon kind is assigned
a single shout by element, fixed on the `PawnKindDef`, not rolled per spawn. Nothing from §4.4a
beyond these three ever appears on a normal dragon — no Dragon Aspect, no Soul Tear, no Storm
Call.

> **CARVE-OUT, granted by the user 2026-07-30: NAMED UNIQUE DRAGONS ARE EXEMPT.**
>
> The rule above governs **normal** dragons, and it stays exactly as written for them.
> **Durnehviir** carries three shouts (Frost Breath, Drain Vitality, and his own *Diil Qoth
> Zaam*) and **Odahviing** carries two (Fire Breath and Frost Breath).
>
> The balance reasoning below is why the rule exists and why the exception is safe: one shout
> means one telegraphed pattern to learn, which matters when dragons arrive *unbidden* at a
> wealth threshold. Durnehviir and Odahviing are neither normal nor unbidden — they are
> **summoned by the player**, once, from a quest-locked shout they had to earn. The player
> chooses when to face what they bring, so an unreadable fight is not a risk they can be
> ambushed by.
>
> **This exemption is for named uniques only.** It is not a licence to give a second shout to
> anything that spawns on its own.
>
> Separately, and worth stating because it now looks like a contradiction: **the Ancient
> Dragonborn is not a dragon**, so this rule never governed him. He carries Fire Breath, Frost
> Breath and Unrelenting Force by the user's decision of 2026-07-30.

This is a real balance constraint, not flavour: one shout means a dragon fight has **one**
telegraphed pattern to learn and counter. Two would make dragons unreadable at the wealth levels
where they arrive.

Note this stacks *on top of* the dragon's existing kit rather than replacing it — Dragon's
Descent dragons already carry their own breath abilities (`DD_DragonBreath_Fire`,
`DD_DragonBreath_Frost`, `DD_DragonSpit`, `DD_DragonLightning`; see `COMPAT.md §4`), so a dragon
whose shout is Unrelenting Force is not left without a breath weapon. For the mod's own fallback
dragon (§12), pick the shout to match whatever breath it ships with.

> **Assumption flagged:** the brief listed the three as "Frost, Unrelenting Force, and frost" —
> frost twice. Read as **Fire, Frost, Unrelenting Force**, which is the TES5 dragon kit. If Fire
> was not intended, it is a one-line def change; say so.

**Reuse the pawn assets exactly.** Same textures, same meshes, same effecters, same sounds as
the Dovahkiin's and the draugr's version of that shout. The dragon variant differs **only** in
numbers:

- larger **area of effect** (wider cone, longer reach)
- greater **range**
- higher **damage and effect intensity** (burn duration, slow magnitude, knockback force)

Implement as level-4+ entries on the same `ShoutDef`, or as a scalar multiplier applied when the
caster is dragon-tagged — **not** as duplicate defs with copied assets. If a future artist
retextures Fire Breath, the dragon version must change with it automatically.

This is deliberately cheap: three shouts × the assets we already built for §4.4a and §4.5 = no
new art, no new sound, no new VFX work. It is also why §4.5's veteran mortals and draugr use the
same three — one asset set covers every non-Dovahkiin shout user in the mod.

**Alduin is not a normal dragon** and is not bound by the three-shout limit — he carries the
full kit at dragon scale plus the meteor call (§6.4).

---

## 5. Dragon Souls

### 5.1 Absorption

When a **dragon** dies and the Dovahkiin **killed it or is within a generous radius**, the
soul is absorbed: a visible column of light and streaming energy from the corpse to the pawn,
a distinctive sound, a colony-wide letter, and a several-second sequence that other pawns can
witness.

Absorption grants **two separate things** — keep them separate in code, this is the crux of
the economy:

1. **+1 permanent Attunement** — `Hediff_DragonSoulAttunement` severity increases forever.
   This is never spent and never lost.
2. **+1 unspent Dragon Soul** — a spendable token used to raise word levels (§4.1).

So spending souls on shouts never costs the player their accumulated presence, and hoarding
souls never makes them stronger than spending them. Both curves rise with dragon kills.

### 5.2 What Attunement gives — spiritual, technique, never brawn

Two different curve shapes, deliberately. Do not unify them.

**Linear, uncapped — the resource pool:**

- **A flat, permanent increase to the mana and stamina pools per soul — linear, never
  diminishing, never spent.** This one curve is linear by design; the user asked for it
  explicitly and a deep pool is not the same thing as raw power.

  **Do not take "+2" literally. RimWorld of Magic has no such unit.** Verified on disk
  (`Mods\Rim World of magic\v1.4\Defs\NeedDefs\TM_Mana.xml`, `TM_Stamina.xml`):

  - Mana and stamina are **`NeedDef`s** — `TM_Mana` (`needClass` `TorannMagic.Need_Mana`) and
    `TM_Stamina` (`needClass` `TorannMagic.Need_Stamina`). A vanilla `Need` is a 0–1 bar.
    There is no integer maximum to add 2 to. TorannMagic computes an internal `MaxMP`/`maxSP`;
    it is not a stored per-pawn field you can offset.
  - Both defs are `<onlyIfCausedByHediff>true</onlyIfCausedByHediff>` and exist **only** on
    pawns carrying `TM_MagicUserHD` (mana) or `TM_MightUserHD` (stamina). **A Dovahkiin who is
    not an RWoM magic or might class has neither need at all** — and §1 imposes no class
    requirement, so that is the common case, not an edge case.

  **OD-9 is answered — the mod ships its own resource.** Implement as follows:

  1. **`Need_Thuum`** — the mod's own need, added by `Hediff_TheVoice`, present on the
     Dovahkiin and on nobody else. **This is what shouts actually spend.** It is the only
     resource the shout system depends on, so shouts work on the baseline environment with no
     other mods, and keep working if RimWorld of Magic is uninstalled mid-save (this also
     answers OD-6).
  2. **Per soul, max Thu'um rises by a flat tunable amount, linear and uncapped, forever.**
     This is the user's "+2 per soul" request, now in a unit that exists. Value lives in
     `DovahkiinTuningDef`.
  3. **RWoM mana and stamina grow too, as a bonus, only when the pawn already has them.**
     Use RWoM's own supported XML mechanism — `Hediff_DragonSoulAttunement` stages carrying
     `<enchantments><maxMP>…</maxMP><maxSP>…</maxSP><mpRegenRate>…</mpRegenRate></enchantments>`,
     values being fractional multipliers (`0.1` = +10%). Severity is already the soul count, so
     one stage per soul tier. Gate the whole file with
     `MayRequire="Torann.ARimworldOfMagic"`. **No assembly reference and no reflection** —
     see `COMPAT.md §5` for the verified field list and a real vanilla-RWoM example.

  Never make Thu'um and mana the same pool, and never make a shout spend mana.

**Strongly diminishing, capped — everything else.** Per soul, all values tunable, each with an
explicit asymptotic cap declared in `DovahkiinTuningDef`. Use `cap * (1 - e^(-k*souls))` or an
equivalent; never linear. The first three souls should feel like a real change, souls ten
through twenty like polish.

- Technique-flavoured stat offsets: melee **hit chance** and **dodge** (not damage),
  ranged **accuracy**, **work speed** on skilled crafts, **research speed**, **social impact**,
  **mental break threshold** resistance, **pain shock threshold**.
- **Shout recovery** reduction — small, capped around 25–30% total.
- **Global learning factor** — the Dovahkiin genuinely learns faster.
- **No** flat HP, **no** unconditional melee damage multiplier, **no** body size, **no**
  carrying capacity inflation, **no** move speed beyond a token amount.

A late-game Dovahkiin should be formidable, never a colony of one.

### 5.3 Akatosh's Child

A permanent passive on the Dovahkiin, **scaling with Attunement**:

- Damage dealt to dragons: **+5% base, +3% per soul, cap +40%**
- Damage taken from dragons: **−5% base, −2% per soul, cap −30%**

This is the answer to "dragons in this modlist are terrifying." At zero souls it barely
matters — the first dragon fight is still a colony-wide emergency. By soul ten the Dovahkiin
is the reason the colony survives dragons. That is the arc.

### 5.4 Social consequences

- **Witnessing a soul absorption**: permanent opinion boost toward the Dovahkiin for every
  colonist who saw it (memory that never decays, stacking with diminishing returns).
- **Witnessing a shout**: temporary mood boost, colony-wide if the shout was loud enough.
- **Being a Dovahkiin**: small flat opinion bonus from everyone, always.
- Optional flavour: a small negative from pawns of a "sceptic" or hostile-ideology bent, so
  the reaction is not uniformly worshipful. Keep it minor.

---

## 6. Dragons

### 6.1 What counts as a dragon

Determined in `COMPAT.md` from real Dragon's Descent defNames, expressed as a
`DovahkiinDragonExtension` DefModExtension or a def list, so the player can extend it and
other creature mods can be included via a settings toggle. Tag each with a **soul weight**
(most dragons: 1 soul; lesser kin: 0; Alduin: special, §6.4).

The mod also ships **its own dragon** (§12) so that none of this depends on Dragon's Descent
being installed. Note for Step 1: Dragon's Descent hard-requires Vanilla Expanded Framework,
so VEF is already a transitive dependency of all borrowed dragon content.

### 6.2 Dragons hate the Dovahkiin

**Any** dragon — hostile, neutral, allied faction, or the colony's own tamed one — turns on
the Dovahkiin on sight. Implement as a targeting/mental-state override, not a faction change,
so the rest of the colony's relationship with the dragon's faction is unaffected.

This is a real, intended drawback: a colony with a tamed dragon cannot also comfortably host
the Dovahkiin. Make the conflict legible with a letter the first time it happens
("Your dragon has turned on <pawn>. It knows what they are.").

Animal Allegiance (§4.4) never affects dragons.

### 6.3 Dragon shouts

See §4.6.

### 6.4 Alduin

The one boss. **Exactly one exists per save**, owned by the registry.

- **Abyssal HP pool** — a scale above any Dragon's Descent dragon. Tune so that a well-equipped
  late-game colony *with* a soul-rich Dovahkiin wins hard, and without one loses.
- Higher damage, high armour, flight, and the full shout kit at dragon scale.
- **Signature shout: the meteor call.** He calls down a rain of small meteors over an area —
  telegraphed impact markers, then explosive strikes with fire. Devastating to structures and
  formations, avoidable if the player reacts. This should be the thing players remember.
- **Revival:** if Alduin is killed by anything other than the Dovahkiin, he collapses, goes
  `Dormant`, and **revives a few hours later** in place or nearby, at full health, with a
  letter. Only a killing blow from the Dovahkiin — or a Dovahkiin soul-absorption on his
  corpse, your call, document it — sets `SlainForever`.
- **Appearances:** normally once per save, as the late-game final quest (§9.3). The exception
  is the Dragon Prophecy scenario (§11), where he also appears in the opening — and there he
  must be **unkillable and scripted to leave**, not a fight the player can win at hour zero.
- Killing him permanently: massive colony-wide mood event, a large soul reward, and a unique
  permanent title upgrade for the Dovahkiin (`Dovahkiin, World-Eater's Bane`).

---

## 7. New world content

All three site types must declare biome eligibility in a Regrowth-safe way and must survive
`MoreWorldFeaturesNames`. Loot value scales with the site's declared danger tier.

**Correction on Medieval Overhaul / Rimedieval filtering — verified in
`Mods\Rimedieval\1.4\Source\Rimedieval\DefCleaner.cs`, which is C#, not `Patches/`:**
Rimedieval strips `QuestScriptDef`, `IncidentDef`, `GenStepDef`, `PreceptDef`, `MemeDef` and
`IdeoPresetDef` by **hardcoded defName blocklist**. New Dovahkiin quests, incidents, gensteps
and sites are therefore **not** filtered and need no special tagging to generate. What *is*
filtered is `ThingDef`s at `techLevel >= Industrial`, via `IsAllowedForRimedieval()`
(`designationCategory` nulled) and `GetAllowedThingDefs()` (list filtering). **The real
exposure is loot, not generation** — §7.3's "loot drawn from the modlist's equipment pools" is
what will silently come up empty or medieval-only. Tag and test loot accordingly.

Note also that Rimedieval's `questsToRemove` deletes `AncientComplex_Standard`,
`AncientComplex_Mission`, `OpportunitySite_AncientComplex` and
`OpportunitySite_AncientComplex_Mechanitor`. The vanilla ancient-complex quests that §7.3 says
to model on are **absent at runtime** in this modlist — read them from `Data\Ideology` and
`Data\Biotech` on disk, never from a live `DefDatabase`.

### 7.1 Dragon mounds — small, open-air

- An exposed stone mound with a **word wall** carved into it.
- **Always guarded by one dragon.** Its tier scales with the mound's tier.
- Reading the wall teaches one word at level 0 (§4.1). Any pawn may find the wall; only the
  Dovahkiin can learn from it — others get a flavour message about a language they cannot hold.
- Small surface loot. Not a dungeon.

### 7.2 Dragon burial sites — small, open-air

- Contains the buried body of a dead dragon and modest grave goods.
- On arrival, a chance of the event **"I have a very bad feeling about this…"**: Alduin is
  already present. He shouts over the mound, the buried dragon **resurrects enraged**, and
  Alduin **flies away** without engaging. The player is left with a live, furious dragon.
  - Alduin here is **`Alduin_Scripted`** — a separate ThingDef, non-combatant, invulnerable,
    despawns on schedule, exempt from the boss singleton (`CLAUDE.md` invariant 2). It does
    not consume his once-per-save boss appearance and cannot drop a soul.
  - Gate the chance so it cannot fire before mid-game.

### 7.3 Nordic crypts — large, underground

Model on the 1.4 ancient-complex generation (`ComplexDef` / `GenStep_AncientComplex` /
`SitePartDef` — verify the real 1.4 types in Step 1). Not a `LayoutDef`; that is 1.5+.

- **Very large**, occupying a large fraction of the map, mountain-encased or subterranean.
- Populated by the crypt bestiary (§12): draugr, draugr overlords, draugr deathlords,
  ghosts, giant spiders, occasional skeevers.
- **Always ends in a word wall.** This is the primary way the player grows their shout library.
- **Always has a treasure room** near the end, sealed, with loot drawn from the modlist's
  equipment pools, value scaled to crypt tier.
- Multi-room progression with a sense of depth: entry halls → catacombs → inner sanctum.
  Puzzle/lever flavour if cheap; skip it if it fights the generator.
- **Engineering reality check — verified against 1.4.3682 `Assembly-CSharp.dll`:** `ComplexDef`
  carries only `roomDefs`, `threats`, `workerClass`, `roomRewardCrateFactor`,
  `fixedHostileFactionChance` and `rewardThingSetMakerDef`. `ComplexRoomDef` carries only
  `sketchResolverDef`, `selectionWeight`, `maxCount`, `minArea`, `maxArea`,
  `requiresSingleRectRoom` and `floorTypes`. **There is no depth, no ordering and no terminal
  room anywhere in the data model**, and `GenStep_AncientComplex.DefaultComplexSize` is
  `(80, 80)` — about 10% of a 250×250 map, not "a large fraction" of it. The ordering, the
  terminal word wall, and the sealed treasure room are therefore a **custom `GenStep`**, not a
  `ComplexDef` configuration. This is the biggest single risk in the project — cost it in
  `RISKS.md` and show the design before building.
- **Some crypts have a Dragon Priest** as the final guardian — the highest-tier non-dragon
  threat in the mod, mask included as unique loot.

### 7.4 Discovery

Sites appear on the world map through normal site generation, and additionally through the map
trader event (§8.3) which reveals one directly.

---

## 8. Incidents

Every incident below requires a living Dovahkiin in the colony unless stated otherwise, and
all rates are tunable. Ordered roughly from most to least common.

| # | Incident | Trigger | Effect |
|---|---|---|---|
| 8.2 | **"A strong warrior has come to duel the Dovahkiin."** | Uncommon | One very strong hostile pawn arrives alone. Build them from the best the modlist offers — high-tier gear, high skills, a mage class or a Melee Animation-friendly duellist. They seek out the Dovahkiin. Consider offering a formal duel with a mood/reputation payoff for accepting. |
| 8.3 | **"A mysterious individual has arrived… he claims to be a friend."** | Uncommon | A visitor offers to trade a **map to a dragon mound or a crypt** — revealing that site on the world map — for **0 silver**. Pure gift; the cost is the journey. |
| 8.4 | **"Bounty hunters are coming for the Dovahkiin."** | Uncommon | A small raid, specifically targeting the Dovahkiin's position rather than the usual raid AI. |
| 8.5a | **"A dragon has sensed its own kin…"** | Rare | One dragon arrives on the map. |
| 8.5b | **"Dragons descend upon us… feeling a strange presence…"** | Very rare | Two dragons. Reserve for late game / high wealth. |
| 8.6 | **"A strange individual begs to join the Dovahkiin's conquests."** | Rare | One pawn requests to join the colony. Requires a Dovahkiin. Flavour them as a follower/thane figure; give them decent combat stats and a bespoke backstory. |
| 8.7 | **"A wanderer with a strange bearing has joined."** | Rare. **Requires no living Dovahkiin.** | A wanderer-joins incident where the arriving pawn **is** a Dovahkiin (§3.4 Route C). No quest, no fanfare — the quiet discovery route. Distinct from 8.6, which is an ordinary follower joining an *existing* Dovahkiin. |
| 8.1 | **"A dragon!!!"** | **ONCE PER SAVE, AND BOUND TO HAPPEN — rewritten 2026-08-01.** No longer the rarest incident: a per-day chance that RISES until it fires, so every colony is guaranteed the opportunity. Fires only when no Dovahkiin exists and it has never fired. Full rule, formula and tuning table in §3.2. Paired with §3.3's harsher lockout — **do not retune one without the other.** | An enraged dragon attacks the colony. If it dies on the map, a random eligible colonist awakens as the Dovahkiin (§3.2), and every living dragonblood pawn is permanently locked out. Tune the dragon so an unprepared colony can lose — but make it possible to win with preparation and terrain. |

Discovery parity with sanguophages means **two routes, one of them a quest** — 8.7 and §9.4.
See the design note in §3.4: Biotech has no sanguophage *incident*, so match the shape, not a
def that does not exist.

---

## 9. Quests

Use `QuestScriptDef` where the vanilla pattern supports it. Narratives are yours to write,
but hit these beats.

### 9.1 The beast that ravages — *repeatable, early/mid*
A settlement is being destroyed by a legendary creature (a landwurm or equivalent from the
modlist, tier-scaled). They ask specifically for the Dovahkiin. Reward: goodwill, silver,
modlist gear, and occasionally a site map.

### 9.2 A dragon has been sighted — *repeatable, mid/late*
A dragon has taken a mountain, a pass, or a ruin. Clear it. Reward: the soul is the reward;
material payment is secondary and should be modest, because the soul is the real prize.

### 9.3 The World-Eater — *once per save, late game*
The finale. Multi-stage: omens and world events → recovering knowledge (a specific deep crypt,
or a set of shouts the player must already know) → the confrontation with Alduin.

Make the requirement to reach him narratively earned: a minimum soul count, or knowing a
specific shout (Dragonrend is the obvious Skyrim answer — if you implement it, gate it here
and make it Alduin-specific: it grounds and disables a dragon's flight and shouts). Ensure the
player cannot stumble into it at hour twenty.

### 9.4 "A stranger is seeking help… his arrival here feels like fate." — *once per save, very rare*
Rarity on par with the vanilla sanguophage meeting quest — concretely, a `rootSelectionWeight`
around `0.5` plus a `minRefireDays` gate; see the verified numbers in §3.4. A wounded, pursued
pawn who **is** a Dovahkiin asks for shelter. Help them survive their pursuers and their injuries and they
offer to join. Only fires if no Dovahkiin exists. If they die, the opportunity is gone.

### 9.5 Further ideas — implement what is cheap
- A word wall in a hostile faction's territory: negotiate, sneak, or fight.
- An old scholar who will translate a word for payment, cutting one level's soul cost, once.
- A rival who has been collecting dragon corpses and wants the Dovahkiin dead for the souls.
- A shrine pilgrimage granting a temporary Voice buff.

---

## 10. Dragonblood — the heirs

Children of the Dovahkiin (either parent) are born with the `Dragonblood` trait.

- Small **social impact** and **opinion** bonus.
- Small **global learning factor** bonus.
- Small **perception** bonus — express through the stats 1.4 actually has: ranged accuracy,
  hunting stealth detection, aiming time, and a modest sight-related offset if the modlist
  provides one.
- **Not** a shout user. **Not** a soul absorber.
- Carries the one-time awakening roll described in §3.3.
- Dragonblood is heritable onward — grandchildren of a Dovahkiin still have it, at the same
  strength. Do not stack it.

If Biotech genes are the cleanest implementation, use a gene **plus** the trait for display —
but the mod must not require Biotech at runtime for this to work. Confirm in Step 1.

---

## 11. Scenario — "The Dragon Prophecy"

Selectable at game start. Deliberately brutal, TES5 opening.

- You start with **one pawn**, who has the Dovahkiin trait, zero souls, zero words known.
- **Clothing only.** No weapons, no supplies, no starting resources.
- You spawn **inside a hostile settlement** — you were about to be executed. The locals are
  hostile from tick zero.
- **Alduin is ravaging the settlement.** He is unkillable in this scene and scripted to leave
  after a short, terrifying sequence. He is the reason the player escapes; he is not a fight.
- Survive, escape, and start over with nothing.
- This is the only path where Alduin appears twice in a save (§6.4).

Balance intent: the payoff for the hardest start is that you begin with the Dovahkiin instead
of praying for the once-per-game event. Do not soften it with a stockpile.

Consider a second, gentler scenario later — "The Last Dragonborn" — normal crashlanded start,
one colonist has the trait. Only after Phase 6 is stable.

---

## 12. New creatures — art and behaviour

**Art direction, non-negotiable:** silhouettes and palettes read as *Skyrim*, execution reads
as *RimWorld*. Flat, top-down, painterly, restrained saturation, readable at colony zoom, and
consistent with Medieval Overhaul's look — that is the visual neighbour these will sit next to.
Every creature needs a distinct silhouette at a glance.

| Creature | Notes |
|---|---|
| **Alduin** | The mod's own creature, not borrowed. Black scale, ragged wing membranes, red eye-glow, a silhouette that reads as *wrong* next to any other dragon. Built in Phase 4; art lands in Phase 5. |
| **Alduin_Scripted** | Same art, separate ThingDef. Invulnerable, non-combatant, despawns on schedule. Used for the burial-site flyover (§7.2) and the scenario opening (§11). |
| **Fallback dragon** — *"Dovah"* | The mod ships **one** dragon of its own so the soul loop, the awakening event, mound guardians, and dragon shouts all function on the baseline environment with no dragon mods installed. Modest by Dragon's Descent standards; the point is that the mod is never an empty shell. When Dragon's Descent is present, this one still spawns, just alongside better company. |
| **Draugr** | Dessicated Nordic dead. Ancient iron and fur, hollow blue eye-glow. Slow, tough, melee, ancient weapons. **Never shouts** (§4.5). |
| **Draugr Wight** | The first tier that can speak. Slightly taller, brighter eye-glow, better iron. **Low chance of one shout** (§4.5). Cheap to build — a retint and a kit swap on the base draugr, not new art. |
| **Draugr Overlord** | Larger, better armed. **One shout, rarely two** (§4.5). |
| **Draugr Deathlord** | Elite. Ebony-black armour, ancient Nord greatsword or bow. **One or two shouts** (§4.5), hits hard. |
| **Ghosts / spectres** | Translucent, partially incorporeal, resist mundane damage, vulnerable to fire/magic. Do not make them unkillable by a normal colony. |
| **Frostbite spiders** | Large, aggressive, venom. Check whether the modlist already has a giant spider before making one. |
| **Skeevers** | Cheap trash mob. Fast, weak, diseased, appear in packs. |
| **Dragon Priest** | Floating, robed, masked. The mask is unique loot with a real bonus. Level-2 shouts, magic attacks, the hardest non-dragon fight in the mod. |

Behavioural note: crypt dwellers should be **dormant until disturbed** — the sarcophagus that
opens as the player walks past is the whole feeling of a Nordic crypt. Use the 1.4 dormancy
comps rather than spawning everything awake.

---

## 13. Balance curve — the shape of the whole thing

| Stage | Souls | What it feels like |
|---|---|---|
| Awakening | 0 | A social event. One known word at most. No combat change. |
| Early | 1–3 | Two or three level-1 shouts. Utility: knock someone down, clear the weather, find something. Akatosh's Child is nearly invisible. |
| Mid | 4–10 | A shout kit with real tactical use. Mana/stamina noticeably deep. Dragons are survivable with the colony's help. |
| Late | 11–20 | Level-3 shouts. Dragon Aspect. The Dovahkiin is why dragon raids get won. Still killable by a bad raid. |
| Alduin | 20+ | A legend. Alduin is still a genuine threat. |

If at any point the Dovahkiin can solo the colony's threats alone, the curve is wrong.
Flatten the top, not the bottom.

---

## 14. Open Decisions

**All ten are answered as of 2026-07-25 — see `DECISIONS.md` for the record.** The text below is
kept for rationale. Answers, and where each now lives:

| | Answer | Implemented in |
|---|---|---|
| OD-1 | Slot reopens after a grieving delay; heirs of the dead one stay locked out | §3.2, §8.1, `ROADMAP.md` Phase 6 |
| OD-2 | Heirs only burn their chance when no Dovahkiin exists | §3.3 (unchanged) |
| OD-3 | Killing blow by the Dovahkiin sets `SlainForever` | §6.4 |
| OD-4 | Unabsorbed souls are lost, with a flavour message | §5.1 |
| OD-5 | Dragonrend is a real shout, quest-locked | §4.4b |
| OD-6 | Nothing breaks — shouts run on `Need_Thuum`, which is ours | §5.2 |
| OD-7 | Single player only | — |
| OD-8 | Words stay discovered; levels must be re-bought | §4.1 |
| OD-9 | The mod ships its own Thu'um resource; RWoM bars grow as a bonus | §5.2 |
| OD-10 | Word N gates level N; list trimmed to ten shouts, 33 walls | §4.1, §4.4 |

- **OD-1 — Death of the Dovahkiin. ANSWERED: yes, the slot reopens.** Does the slot reopen?
  *Recommendation, accepted:* yes, but not
  instantly and not silently. On death the world grieves (colony-wide mood event), the registry
  clears after a delay of several days, and the "A Dragon!!!" event becomes eligible again
  exactly once more. Dragonblood pawns locked out by the original awakening **stay** locked out.
  A dead Dovahkiin resurrected by any means resumes the role.
  *If accepted:* §3.2's "at most once per save" becomes "at most once per Dovahkiin slot,"
  which is why the registry field is `dragonEventFiredCount` (a counter) rather than a bool.
  Reword §3.2, §8.1 **and `ROADMAP.md` Phase 6** to match whichever answer I give — all three
  currently say "once per save" and must not be left contradicting §14.
- **OD-2 — Heir lockout timing.** §3.3 currently only burns an heir's chance when no Dovahkiin
  exists. The alternative — burn it on every dragon death regardless — is harsher and matches
  a literal reading of the brief. Confirm which.
- **OD-3 — Alduin's permanent death condition.** Killing blow by the Dovahkiin, or
  soul-absorption on the corpse?
- **OD-4 — Souls without a Dovahkiin.** If a dragon dies and nobody can absorb the soul, is it
  lost, or does it linger as a world object? *Recommendation:* lost, with a flavour message.
- **OD-5 — Dragonrend.** Implement as a real gated shout, or keep it purely narrative in §9.3?
- **OD-6 — RimWorld of Magic absent.** If RWoM is removed mid-save, what happens to mana-based
  Attunement rewards? Needs a graceful fallback. Distinct from OD-9, which is the *commoner*
  case: RWoM present, Dovahkiin has no class.
- **OD-7 — Multiplayer / other frameworks.** Assume single player unless told otherwise.
- **OD-8 — Word wall re-reading.** Can a *second* Dovahkiin (after OD-1) re-learn words the
  first one discovered? *Recommendation:* yes — `wordsDiscoveredWorld` is world state, but word
  *levels* are per-pawn and must be re-bought with souls.
- **OD-9 — The mana/stamina reward for a Dovahkiin with no RimWorld of Magic class.**
  **Blocks §5.2; answer before Phase 3.** Verified on disk: `TM_Mana` and `TM_Stamina` are
  `NeedDef`s marked `onlyIfCausedByHediff`, created only by `TM_MagicUserHD` and
  `TM_MightUserHD`. An ordinary colonist who awakens has neither need, so the single linear,
  uncapped, explicitly-requested reward does nothing for them. Options:
  (a) awakening grants the RWoM class hediffs, making every Dovahkiin a magic *and* might user
  — most faithful to "the Voice is a power," but it hands the player a free RWoM class and
  hard-couples the mod's core reward to RWoM;
  (b) the mod ships its own Thu'um resource that the shouts actually spend, and RWoM
  mana/stamina scaling becomes a *bonus* applied only when the pawn already has those needs —
  decoupled, works on the baseline environment, but adds a fourth resource bar;
  (c) souls grant nothing resource-shaped unless the pawn already has a class, and the linear
  curve is dropped in favour of §5.2's diminishing stats only — cheapest, but deletes a
  requirement the user asked for by name.
  *Recommendation:* **(b).** It is the only one that satisfies §1 (any pawn), `CLAUDE.md`
  invariant 5 (works with no other mods), and the user's linear-pool request at once. Decide
  before touching §5.2.
- **OD-10 — Do shout levels require discovered words?** **Blocks §4.1, §4.4 and all of §7;
  answer before Phase 2.** Faithful reading (level N needs word N) = 63 word walls across the
  §4.4 list; cheap reading (one word unlocks the shout, souls buy levels) = 21.
  *Recommendation:* faithful, with §4.4 trimmed and §7 wall density raised. Whichever is
  chosen, it sets the world-content budget, so it cannot wait until Phase 5.

---

## 15. QUESTLINES — the ongoing stories

> **ADDED 2026-08-01 from a single design message. Nothing here is built. Nothing here is
> negotiable without the user — it is their story, not a set of defaults.**

### 15.0 "QUESTLINE" IS PROJECT JARGON. Use the word.

**A QUESTLINE is a TRAIN OF QUESTS: the next one becomes available only after the previous one
has been completed.** The user coined it here and intends to add several.

It is not a synonym for "quest", not a synonym for "quest chain in general", and not the same as
a one-off side quest. When this document or any commit message says *questline*, it means that
specific structure.

**IT IS CONFIRMED FEASIBLE, AND CHEAPLY.** RimWorld ships `QuestPart_SubquestGenerator`, an
abstract `QuestPartActivable` whose `maxActiveSubquests` field set to **1** gives exactly this
behaviour: the next leg cannot be generated while one is still running. The order is chosen by
overriding `GetNextSubquestDef()`. Vanilla uses the same base class twice
(`QuestPart_SubquestGenerator_RelicHunt`, `QuestPart_SubquestGenerator_ArchonexusVictory`).

**The chain's progress is stored by the GAME, not by us** — `Quest.parent` plus each quest's
`QuestState`. There is no bespoke "which chapter am I on" counter to write, save, or corrupt,
which is the single reason this is cheap rather than a `RISKS.md` §9 hazard. Full research notes
in the save notebook.

### 15.1 The three shout questlines — NOT random quest drops

**All three quest-locked shouts are earned at or near the END of a questline, not from a quest
that happens to turn up.** This supersedes any earlier reading of §3.4/§9 that treated them as
standalone rewards.

| shout | questline | where in it |
|---|---|---|
| **Summon Durnehviir** | the Dawnguard-inspired vampire war | via the Soul Cairn, late |
| **Call of Valor** | **the MAIN questline** | one of the last quests |
| **Call Odahviing** | **the MAIN questline** | won by defeating him |

### 15.2 Questline: the vampire war → Summon Durnehviir

*Dawnguard-inspired.* A war between two factions:

- a **human** faction — **Divine Order** is the user's preferred casting (*"much better visually
  and immersively"*), and it is therefore a **RECOMMENDED mod, never a requirement**
- the **Volkihar clan**, a **sanguophage** faction

**The Volkihar clan must exist as EXACTLY ONE faction on the world map.** Not one settlement —
one faction, uniquely. That is a hard constraint of the same family as the one-Dovahkiin and
one-Alduin invariants, and it belongs in the registry rather than being left to faction
generation.

At some point the Dovahkiin is **invited to the Soul Cairn**, and returns after a delay with
**Summon Durnehviir**. That in turn **triggers the side quest to summon Durnehviir, which rewards
SOUL TEAR**.

> **CONSEQUENCE, AND IT CONTRADICTS SHIPPED CONTENT: SOUL TEAR MUST NOT BE FOUND ON WORD WALLS.**
> It is a questline reward. Soul Tear is **already built, playtested and signed off** as a normal
> shout — so its three words need the `questOnly` flag added, and **the Phase 7 word-wall count
> drops by three.** §4.4's re-cost problem gets *easier*, not harder. Do not miss this when
> Phase 7 is planned.

**Home and dungeons:** *Gothicstyle Vampire Furniture* is the user's preferred dressing for the
Volkihar home and for any vampire den or dungeon encounter. **Recommended, never required.**

> **⚠ BIOTECH. Sanguophages are a Biotech xenotype, and `CLAUDE.md` invariant 5 says the mod must
> run on the baseline with Biotech absent.** So this questline cannot simply *be* sanguophages.
> Either it degrades gracefully to a non-Biotech vampire faction of our own, or the whole
> questline is `MayRequire`-gated and silently absent without Biotech. **DECIDE THIS BEFORE
> BUILDING ANY OF IT** — it changes what the faction is made of, not just whether it appears.

### 15.3 Questline: the main story → Call of Valor, and Call Odahviing

**Call Odahviing** is won by **defeating him** — he must NOT die. In TES5 he is trapped, then
released to fly the Dragonborn to Skuldafn. A retreat/flee mechanic is needed;
`RimWorld.LordJob_AssistColony` is already recorded in the notebook as the vanilla shape for an
ally that arrives and leaves under its own AI. **The user's instruction: plan exactly what happens
before writing any code.**

**Call of Valor** is one of the last quests, and the climax is **the fight with Alduin in
Sovngarde**.

### 15.4 CAN WE ENTER ANOTHER REALM? — Sovngarde and the Soul Cairn

**YES, and it is a headache rather than a wall. Feasible in 1.4, moderate cost, not
game-breaking.** Checked on disk rather than assumed:

- **`MapGeneratorDef` is a normal, moddable def type.** Core ships `Base_Faction`, `Settlement`,
  `Encounter`, `EscapeShip`; Ideology ships `BasePlayer_SecondArchonexusCycle` and
  `_ThirdArchonexusCycle` — **generators whose entire job is relocating the colony to a new map.**
- Quest sites already generate maps on demand and are entered and left routinely.

So a realm is a **generated map with its own `MapGeneratorDef`** — its own terrain, no weather, its
own light — reached by a scripted transition rather than an ordinary caravan.

**The honest costs, none of them fatal:**

- Every map hangs off a **world object on a world tile**. Sovngarde is not a place on the planet,
  so it needs a world object somewhere — hidden or remote. Slightly awkward fiction, and it is how
  every mod that has done this handles it.
- **A second map costs performance.** RocketMan is installed, which helps, but it is a real cost.
- **Getting pawns BACK is the risky half**, and it is the same family of hazard as the temporary
  summons: a pawn stranded in a realm that no longer exists is `RISKS.md` §9 wearing a hat. It
  must survive save → reload mid-realm.

> **THE FALLBACK IS A LEGITIMATE DESIGN, NOT A CONSOLATION PRIZE.** The user proposed it
> themselves: send the Dovahkiin away for a delay, fight Alduin **on the colony map**, and be
> rewarded with Call of Valor on return. It costs a fraction as much, carries none of the
> stranded-pawn risk, and the "away for a delay" shape is already needed for the Soul Cairn trip
> in §15.2. **Recommendation: build the delay version FIRST for both realms.** If it plays well,
> the real map is a later upgrade to the same questline rather than a prerequisite for shipping
> it.

### 15.5 The Vampire Lord transformation — feasibility

**Mechanically: EASY-TO-MODERATE. Artistically: this is the whole problem.**

The mechanism is one this project has now built and playtested **twice**: a hediff plus
`Thing_DragonAspectOverlay`, a follower Thing that paints art onto a pawn with no Harmony patch
and nothing on the pawn render path. As of 2026-08-01 that overlay already supports **multiple
texture sets and per-wearer weapons**, because Call of Valor needed exactly that. Granting
abilities on transformation is what Dragon Aspect already does.

**So the code is largely a re-skin of solved work.** What is NOT solved is the art: a vampire lord
is a hunched, winged, bat-faced creature — **a completely different silhouette from a humanoid**,
not a costume over one. The overlay draws *onto* a human body quad, which is what makes Dragon
Aspect's armour work and is exactly wrong for a shape that is not human.

**This runs straight into the honest ceiling already recorded in the notebook:** the procedural
generator draws polygons at 256px on a ~102px-wide pawn, and it cannot draw a new creature. A
Vampire Lord needs **hand-drawn art**, or it does not happen. Say so plainly rather than shipping
a near-miss.

**And it inherits §15.2's Biotech problem** — a transformation into a sanguophage-adjacent form on
a baseline install has nothing to be adjacent to.

*Verdict to give the user:* the transformation is buildable and the machinery exists; the blocker
is a sprite set nobody in this project can draw procedurally.
