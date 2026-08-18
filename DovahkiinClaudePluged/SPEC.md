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

> ### ⚠ RULE CHANGED BY THE USER, 2026-08-04. A DRAGON'S SHOUT IS **NOT** THE DOVAHKIIN'S.
>
> This section previously implied a dragon casts the same Fire Breath and Frost Breath the
> Dovahkiin does. **That is reversed.** A dragon's breath is its own thing, and it must feel
> like a different weapon from the other end:
>
> | | the Dovahkiin's (and other shout users') | **a DRAGON's** |
> |---|---|---|
> | damage | instant, on arrival | **+100%, but delivered GRADUALLY over time of exposure** |
> | cone | as authored | **slightly NARROWER** |
> | range | as authored | **twice the range, plus X** |
>
> **The gradual delivery is the whole point and the hardest part.** A dragon's breath is a
> sustained jet you must get *out of*, not a burst you either ate or dodged. Standing in it
> briefly should be survivable; standing in it is not. That inverts the tactical question from
> "did I dodge" to "how fast can I break line" — which is what makes a dragon feel like weather
> rather than an attack.
>
> **Consequences to respect when building it:**
> - Damage-over-exposure means the wave cannot simply apply its payload on arrival the way
>   `Thing_ShoutWave` does today (`SPEC.md §4.4`, and see the architecture note in the
>   notebook: the wave "carries the payload and applies it as it arrives"). A dragon's breath
>   needs a **lingering area**, or a per-tick re-application while a pawn stands in the cone.
> - **Longer range + narrower cone is a real balance lever, not a reskin.** It rewards the
>   dragon for lining up and rewards the player for moving off the axis.
> - The exact multipliers (`X` on range, the exposure tick rate, the per-tick fraction) are
>   **tuning numbers** and belong in `DovahkiinTuningDef.xml`, never inline.
>
> ### ⚙ HOW TO BUILD IT: ONE `Thing_DragonBreath`, NOT REPEATED `Thing_ShoutWave`s
>
> **Decided 2026-08-04 after the user raised performance, and after §4.6a made soar's geometry
> different from grounded's.** An earlier proposal here was to emit ~15 ordinary shout waves in
> quick succession, so that standing in the stream took repeated hits. **That is now rejected**,
> for two reasons that compound:
>
> 1. **The user's performance concern is legitimate.** Fifteen overlapping `Thing`s per breath,
>   each drawing its own quads every frame, is more Things than any shout in the mod currently
>   spawns — and RocketMan is installed. One Thing that owns the whole breath is strictly
>   cheaper than fifteen that each own a slice of it.
> 2. **A bespoke breath Thing has to be written anyway.** §4.6a's soar mode — circular damage at
>   an impact point plus a purely visual cone that stretches with range — cannot be expressed as
>   repeated cone waves at all. So the wave-emitter hack would be written now and thrown away
>   the moment soar arrives. **Build the thing that serves both states once.**
>
> **`Thing_DragonBreath`** — one Thing, living for the breath's duration, which:
> - draws the jet **as a jet**, authored, rather than as a stutter of rings
> - re-applies damage on an interval to whoever is currently inside its area, which is what
>   makes damage accrue by **exposure**
> - switches its area between **cone** (grounded) and **circle + cosmetic cone** (soar)
>
> **The environmental effects are already solved and must be REUSED, not rewritten.**
> `Thing_ShoutWave.StrikeBand` is the single method that does ignition (`FireUtility.TryStartFireIn`),
> snow (`Map.snowGrid.AddDepth`), damage, stun and hediffs. Extract that into a shared helper
> both classes call. Rewriting it would produce burned cells and snowy patches subtly different
> from the Dovahkiin's, which is exactly what the user asked NOT to happen.
>
> **This is the first real customer for the shared VFX kit** the notebook has had proposed and
> deferred since 2026-08-01 — `Thing_ShoutWave`, `Thing_ValorPortal` and the Dragon Aspect aura
> all draw N tinted rotated quads along a path over a lifetime curve and share not one line.
>
> **What is unchanged:** which shouts a dragon has, and how many. The table below still governs.

> ### 4.6a THE BREATH'S GEOMETRY DEPENDS ON THE MOVEMENT STATE — user, 2026-08-04
>
> A dragon on the ground and a dragon in the air are not breathing at the same thing. The user's
> words: *"in soar the shout goes from ground to ground to air to ground… in soar state the
> shout is doing damage in a 'circle', followed with a cone of purely effects that should adapt
> visually to the range where the shout is designated to land."*
>
> | | **GROUNDED** | **SOAR** |
> |---|---|---|
> | origin → target | ground → ground | **air → ground** |
> | damage shape | **cone**, sweeping the ground | **CIRCLE** at the designated impact point |
> | the cone | *is* the attack | **PURELY VISUAL** — no damage in it at all |
> | visual cone length | fixed by range | **adapts to how far the impact point is** |
>
> **This is the right call and it solves a geometry problem rather than adding one.** A cone
> swept along the ground from a creature that is thirty feet above it makes no sense; a jet
> angled *down* onto a point does. It also gives the two states genuinely different threat
> shapes — grounded is a wall of fire you break away from sideways, soar is a spot you must not
> be standing on — which is the whole reason for having movement states.
>
> **FLIGHT gets the STRAFING breath — specified 2026-08-04, and it is a THIRD geometry.**
>
> The user: *"strafing is air to ground shouting too. Less concentrated over time per cell but
> impacts way more cells in total. The dragon is strafing with his breath, and he should do that
> sometimes while in battle — soar → flight away a bit → comes back while in flight state
> strafing his breath."*
>
> | | SOAR breath | **STRAFING breath (flight)** |
> |---|---|---|
> | impact point | **fixed** — a chosen cell | **MOVES with the dragon** as he flies over |
> | per-cell intensity | full | **lower** |
> | total cells hit | few | **many more** |
> | shape over time | a circle | a **swathe** painted along his flight path |
>
> **THE ATTACK PATTERN IS THE POINT, and it is the user's:** soar and trade → peel off into
> flight → **come back on a strafing run**. That gives a dragon fight a rhythm instead of a
> single stance, which is what "more attack patterns and game dynamics" meant on 2026-08-03.
>
> > **The user offered to cut this if it were expensive — it is NOT, given the architecture
> > chosen below.** A strafing run is the SOAR mode with two changes: the impact point tracks
> > the dragon instead of staying put, and the per-tick damage is lower. Once
> > `Thing_DragonBreath` exists and already switches between cone and circle, this is a third
> > parameter set on the same class — **not a new system**. Build it.
>
> **⚠ MY READING, FLAG IT IF WRONG:** "air to ground" is taken to mean the breath *originates at
> the dragon in the air* and *lands at a targeted point*, with the visual cone connecting the two
> and stretching as that point gets further away. The circle is the damage; the cone is the
> tell.
>
> **The user's own assessment, recorded because it is correct:** *"that's a lot more headache on
> the way, but worth it nonetheless."*

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

### 6.1a A DOVAH ATTACKS EVERY LIVING THING — the exemption list is *closed*

**Given by the user 2026-08-13**, after a dragon declined to fight a wild insect:

> *"make sure every creature (apart from the draugrs and dragon priests this mod is about to add)
> are all not exceptions to dovah aggression."*

**So the rule is: NO creature is exempt.** Not wild animals, not insects, not other mods' fauna,
not tamed animals, not mechanoids — a dovah is hostile to all of it and will engage it. Species,
faction and race are **not** inputs to whether he attacks.

**The only exemptions, and this list may not grow without the user saying so:**

| exempt | why |
|---|---|
| **Draugr** (all tiers) and **Dragon Priests** | This mod's own Nordic undead (§12), not yet built. They are the dragon cult's servants; a dovah has no quarrel with them. |

⚠ **Both exemptions are FUTURE work — neither creature exists yet, so nothing implements this
today, and that is correct.** It is recorded here so that when §12's bestiary is built the
exemption is written *with* them rather than discovered later as "why is the dragon eating the
draugr".

⚠ **DO NOT implement the exemption as a species blocklist scattered through the targeting code.**
When those creatures arrive, give them one shared marker — a `DefModExtension` or a shared
`ThingDef` parent — and test that in the single place `Comp_AlduinFlight.IsEngageable` decides
what counts as a target. **Verified 2026-08-13: there is no species filter anywhere in the dragon's
targeting path today**, and it must stay that way — one test in one place, not a list in several.

### 6.1b THE DRAGON TIER LADDER — armour, health and damage scale together

**Given by the user 2026-08-13.** Their concern in their own words: giving dragons Dragon Aspect's
armour value outright *"would make them a nightmare on colony start"* — so the answer is a ladder,
not a flat buff, and the bottom rung is deliberately weak.

**⚠ THESE ARE SKYRIM'S OWN DRAGON TIERS, NOT ANOTHER MOD'S.** Checked on disk 2026-08-13:
Dragon's Descent names its dragons by **colour** (`Black_Dragon`, `Blue_Dragon`, `Gold_Dragon`,
`Jade_Dragon`, `True_Dragon`…). It has no Blood/Frost/Elder/Revered/Legendary ladder. So this is
**this mod's own progression**, built on the mod's own dragons (§12), and it borrows nothing.

**NONE OF THESE EXIST YET** except the test Alduin. This is the table each one is built to as
Phase 3/5 produces it.

| # | tier | Sharp | Blunt | **Heat** | `baseHealthScale` | damage × | armour pen |
|---|---|---|---|---|---|---|---|
| 1 | **Dragon** | 0.00 | 0.00 | **0.90** | 7.0 | 0.50 | **FINAL** |
| 2 | **Blood Dragon** | 0.10 | 0.07 | **0.96** | 9.8 | 0.59 | **FINAL** |
| 3 | **Frost Dragon** | 0.20 | 0.14 | **1.03** | 13.3 | 0.67 | **FINAL** |
| 4 | **Elder Dragon** | 0.30 | 0.21 | **1.09** | 17.4 | 0.76 | **FINAL** |
| 5 | **Revered Dragon** | 0.40 | 0.29 | **1.16** | 22.2 | 0.84 | **FINAL** |
| 6 | **Legendary Dragon** | 0.50 | 0.36 | **1.22** | 28.0 | 0.93 | **FINAL** |
| 7 | **Named** (Odahviing, Durnehviir, Paarthurnax) | 0.60 | 0.43 | **1.29** | 34.4 | 1.01 | **FINAL** |
| 8 | **Alduin** | 0.70 | 0.50 | **1.35** | 42.0 | **1.10** | **FINAL** |

> **⚠ HEAT DOES NOT START AT ZERO AND DOES NOT SCALE FROM ZERO — THE USER'S CORRECTION,
> 2026-08-13:** *"No heat and cold armor shouldnt be changed, instead it should be the same as
> Alduin TEST for dragon up to +50% of it for Alduin."*
>
> So **every** dragon, including the weakest, starts at the test Alduin's **0.90** and the ladder
> runs to **1.35**. Sharp and Blunt still climb from 0.00 — a starter dragon is soft to a spear and
> a club, and *never* to fire. **An earlier draft of this table scaled Heat 0.30 → 0.90 and that was
> wrong; do not reinstate it.**
>
> **⚠ HEAT ARMOUR AT OR ABOVE 1.00 MEANS FIRE CAN NEVER DO FULL DAMAGE.** Per
> `ArmorUtility.ApplyArmor`, effective armour splits into a full deflect below `effective/2` and a
> halved hit below `effective`. At Alduin's 1.35 against a zero-AP flame that is **~67% deflected,
> ~33% halved, 0% full** — near-immunity. Deliberate for the World-Eater, and worth knowing before
> anyone builds a fire-based counter to him and wonders why it does nothing.

**"FINAL" = the values after the 2026-08-13 −5% cut, and they are the REFERENCE for the whole
table:** maw **38.475** / wing bash **28.5** / tail sweep **32.3**, armour penetration **0.27075** /
**0.18** / **0.22**, and the maw's `chanceFactor` is **1.4**.

**THE MAW WAS CUT THREE TIMES ON 2026-08-13, IN THIS ORDER, AND ONLY THE LAST TWO DID MUCH:**
45 -> 42.75 (-5%) -> **38.475** (-10% again); AP 0.30 -> 0.285 -> **0.27075**; `chanceFactor`
1.6 -> **1.4**.

**The chanceFactor is the deliberate part.** Measured, the maw was **58% of every swing he threw** -
both his hardest attack and the majority of his attacks, which is why it did the killing. The user
kept him bite-led on purpose: *"dragons in skrim always bites more than often (they even tend to
turn around to bite you so I'd rather not change that tendency)."* 1.4 leaves the maw at **55%** of
swings - still clearly a biting creature, just less of a machine gun. **Do not "balance" this down
to parity with the sweeps; the bite-heaviness is intended flavour.**

> **⚠⚠ ARMOUR PENETRATION IS CONSTANT ACROSS EVERY TIER. THE USER'S RULE, 2026-08-13, VERBATIM:**
> *"Apply the -5% of damage and AP first (Final value), and then -50% of 'Final value' for dragon
> up to +10% of 'final value' for Alduin, ARMOR PEN DOESNT CHANGE and is constant (final value),
> only the DAMAGE changes."*
>
> **So the ladder multiplier applies to `<power>` ONLY.** Every tier — a starter Dragon and Alduin
> alike — pierces armour identically at 0.285 / 0.18 / 0.22.
>
> **⚠ AND THIS IS WHY EVERY TOOL MUST KEEP ITS EXPLICIT `armorPenetration` (§6.1c).** With AP
> derived from power, scaling damage down a tier would drag penetration down with it and the rule
> above would be impossible to express. **A tier-1 dragon hits softly but pierces exactly as well as
> Alduin** — which is the design: armour is the answer to a dragon's *damage*, never to its bite
> being sharp.

**Health and armour still scale** — both were the user's explicit earlier instruction (*"not only
armor should scale up with the dragon tiers, damage and HP too"*). Only **penetration** is exempt
from scaling.

> ### ⚠ THE HEALTH COLUMN WAS DOUBLED AT THE TOP ON 2026-08-13 — AND WHY
>
> The user loaded an **endgame** save and watched Alduin die *fast* to a fraction of the colony's
> defences: some summoned skeletons, two pawns, and one magic ultimate. **He was already carrying
> the Alduin row's armour and health at the time** — so the ceiling was demonstrably too low, not
> mis-assigned.
>
> Their instruction: *"increase the whole dragon tier's health (do not touch armor) by 0% for
> dragons up to 100% for Alduin."* So the original 7.0 → 14.0 ladder became **7.0 → 28.0**: the
> starter dragon is untouched, and Alduin's `baseHealthScale` **doubles**.
>
> **⚠ RAISED AGAIN THE SAME DAY: a further +50% at the top, 0% at the bottom** (*"another health
> scale up to add to the dragon tiers: +50% for Alduin down to 0% for dragons"*), so the ladder is
> now **7.0 -> 42.0** and Alduin is **THREE TIMES** his original 14.0. The starter dragon has still
> never moved from 7.0 - **every raise has been at the top only**, which is the shape that keeps a
> colony-start dragon survivable while making the World-Eater a wall.
>
> **⚠ ARMOUR WAS DELIBERATELY NOT TOUCHED**, on the user's explicit instruction. Health is the
> honest lever here: armour is a *probability* of deflection, so raising it makes a fight swingy and
> luck-driven, while health makes a boss take longer to kill in a way the player can feel and plan
> around. **Do not "help" by raising armour as well.**
>
> This also restores the shape `SPEC.md §6.5` always assumed — *"their strength is a huge HP pool"*,
> with **no unusual regeneration**. A dovah that is hard to kill because it is vast, not because it
> is slippery.

**⚠ THE TEST ALDUIN SITS AT 1.00, NOT 1.10.** He carries the FINAL reference values, because that
is what every number in this table is measured against and what the 2026-08-13 playtests were run
on. The real boss Alduin is **10% above the creature currently being tested**. Applying 1.10 to the
test dragon is a one-line change if the user wants to feel the boss numbers directly.

Three notes on the values, all of which are judgement calls the user gave a free hand on:

- **Tier 1 really is 0.00 Sharp and Blunt**, as asked. A starting colony fights a dragon with no
  physical armour at all, half damage and half the health — survivable with terrain and preparation,
  which is what §8.1's awakening event needs. **Its fire resistance is full from the first tier**;
  only steel and clubs get easier at the bottom of the ladder.
- **Armour is a PROBABILITY, not a reduction** (`ArmorUtility.ApplyArmor`): effective armour splits
  evenly into a full deflect and a halved-and-downgraded hit. 0.70 Sharp means 35% deflected and
  35% halved — *not* "70% less damage". Read §5 of the notebook before retuning any of it.

> **⚠⚠ "COLD ARMOUR" CANNOT BE DONE — THERE IS NO SUCH STAT IN 1.4.** Verified against
> `Assembly-CSharp` and `Data\Core\Defs` on 2026-08-13: the only armour ratings that exist are
> **`ArmorRating_Sharp`, `ArmorRating_Blunt` and `ArmorRating_Heat`.** There is no
> `ArmorRating_Cold`. `Insulation_Cold` exists but is **temperature comfort — it does nothing
> against frost DAMAGE.**
>
> **This is the same wall §15.7 already hit for the vampires** ("frost resistance has NO vanilla
> stat"), and the answer there is the answer here: cold resistance has to be built, not configured
> — a `StatPart` or hediff that reduces our own Frost Breath damage specifically. **Left unbuilt
> and unpromised rather than silently substituting `Insulation_Cold`, which would look like the
> feature exists while doing nothing in a fight.**

> **⚠ THE +50% IN THE FIRST BRIEF WAS SUPERSEDED BY THE USER THE SAME DAY — DO NOT RESTORE IT.**
> The original wording was *"-50% of it for dragon up to +50% of it for Alduin"*, which was put back
> to them because it would have placed Alduin at 1.5× the creature that had just three-shot an
> Aspected Dragonborn. Their correction set the top of the ladder at **+10%**, applied to the
> post-cut FINAL value, and exempted armour penetration from scaling entirely. The table above is
> that correction. **1.10, not 1.50.**

**The real cause of the three-shot was not the damage number.** See §6.1c.

### 6.1c EVERY CREATURE TOOL NEEDS AN EXPLICIT `armorPenetration`

`Verse.Tool.armorPenetration` defaults to **-1**, and `VerbProperties.AdjustedArmorPenetration`
then derives it as **`damage × 0.015`**. So an unspecified AP silently tracks the damage number.

On 2026-08-05 the maw was given an explicit **0.30** for exactly this reason, and the note added
then said: *"Wing bash and tail sweep are still on the derived default — revisit if the same
complaint arrives about them."* **It did, on 2026-08-13.** Measured against Dragon Aspect at three
words (+0.60 Sharp):

| tool | AP before | armour worked on | AP now | armour works on |
|---|---|---|---|---|
| maw | 0.30 explicit | 30% of hits | **0.27075** | 32.9% |
| wing bash | 0.45 **derived** | 15% | **0.18** | **42%** |
| tail sweep | 0.51 **derived** | 9% | **0.22** | **38%** |

**The signature bite was the LEAST dangerous of his three attacks**, and her armour was doing almost
nothing against the two sweeps — which is what actually three-shot her. Cutting damage could never
have fixed it, for the same reason the -15% was tried and reverted in August: *damage and
penetration were one number.*

**Rule for every creature this mod ever ships: state `armorPenetration` on every tool.** A tool
without one has a damage figure that is secretly two numbers.

> ### ⚠ AP IS A SMALL LEVER ONCE IT IS ALREADY BELOW THE TARGET'S ARMOUR — MEASURED
>
> The maw's AP was cut a second 5% on 2026-08-13 (0.285 → 0.27075) to stop it one-shotting a
> Dragonborn. **Measured, that buys −1.4% expected damage against a three-word Dragon Aspect** and
> **nothing at all** against one word or a naked pawn.
>
> The arithmetic is why: expected damage is `power × (1 − 0.75 × max(armour − AP, 0))`, so an AP
> change moves the result by **0.75 × ΔAP × power** at most, and only for targets whose armour
> already exceeds the AP. At ΔAP = 0.014 that is fractions of a point of damage.
>
> **THE LEVERS THAT ACTUALLY MOVE THE MAW, in descending order:**
>
> 1. **`power` (42.75).** Linear and unconditional — it is the only one that helps a naked pawn.
> 2. **`chanceFactor` — and this is the big one.** Measured off the def: maw **1.6**, wing bash
>    **0.8**, tail sweep **0.35**. So the maw is **58% of every swing he throws**, the wing 29%, the
>    tail 13%. **The user's instinct that "the maw is what kept one-shotting them" is arithmetically
>    correct** — it is both his hardest attack and the majority of his attacks.
>    Dropping it to 1.0 takes the maw to ~44% and moves those swings onto the far weaker sweeps.
>    **This is the cheapest way to make him less bite-centric without weakening the bite itself.**
> 3. **The `Bite` damage def** carries `harmAllLayersUntilOutside` — it cuts through every armour
>    layer to the outside, which is a large part of why bites feel lethal — and
>    `overkillPctToDestroyPart`. Changing this affects every biting creature in the game, so it is
>    a patch, not a tuning value.
> 4. **AP** — smallest, as above.
>
> **Reach for 1 or 2 before 4.** Recorded because "reduce the armour penetration" is the intuitive
> answer and is the weakest of the four.

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

### 6.5 Movement states — GROUNDED / SOAR / FLIGHT

**Given by the user 2026-08-03 and specified 2026-08-04, after the three sprite sets were
proved in game.** *"For more attack patterns and game dynamics."* This is the dragon's core
combat identity, not a visual flourish — a dragon that cannot leave the ground is a big animal.

**Dovah are timeless.** *"They do not age, they do not birth, they just are."* One life stage,
no age-related decline, no breeding. See §12 and `CHANGELOG.md`; it is also what keeps the
runtime sprite swap safe, since a life-stage change is the only thing that can undo it.

| | GROUNDED | SOAR | FLIGHT |
|---|---|---|---|
| speed | baseline | **+20% over grounded** (×1.2) | **+50% over SOAR** (×1.5 of soar = **×1.8 of grounded**) |
| crosses roofs / walls / obstacles | no | **yes** | **yes** |
| crosses natural mountain rock | no | **NO** | **NO** |
| **may be ON THE GROUND under a roof** | **NO** (§6.5a-2) | n/a | n/a |
| can be hit by melee | yes | **no — ranged only** | **no — ranged only** |
| can melee | yes | **no** | **no** |
| can shout | yes | **yes** | **no** — except the strafing shout, below |
| entered when | see §6.5c | see §6.5c | **only while actually MOVING** |

#### 6.5a What "crosses obstacles" means, and the one exception

Soaring and flying dragons pass over **roofs, walls, doors, fences and terrain** — the colony's
perimeter is not a perimeter to them. **The single exception is naturally generated mountain
rock**, the kind the player mines out. That is impassable in every state.

This is deliberate and load-bearing: it means a mountain base is genuinely dragon-proof from
above, and an open base is not. It gives the player a real architectural answer.

#### 6.5a-2 A DOVAH NEVER LANDS OR WALKS UNDER A ROOF — declared by the user 2026-08-18

The user, after finding him grounded inside a roofed building: *"He landed inside a roofed area
and I think it's kind off messy and unfair, so I hereby declare that dovah's cannot land nor walk
inside roofed area, only fly over it."*

**A roofed cell is not somewhere a dovah can be on the ground.** He crosses it in soar or flight,
as §6.5a already says, and that is the only way he interacts with it.

⚠ **THIS IS A DIFFERENT RULE FROM THE MOUNTAIN EXCEPTION AND MUST NOT BE MERGED WITH IT.**
§6.5a is about natural **rock** refusing to let him **fly through**; this is about a **roof**
refusing to let him **land**. A roofed courtyard he may fly over and not land in; a mountain he
may neither.

**What it gates: CHOOSING the ground.**

- the dive pounce may not pick a roofed cell to come down on
- "standing in melee range" does not ground him when every cell beside the target is roofed
- grounded attack patterns (dive-and-brawl, perch breath) leave the roll while his target is
  indoors, which leaves the hover breath — he stays up and breathes down at them
- found standing under a roof by any route, he lifts into a **soar**

**What it does NOT gate: being FORCED down.** Downed, at or below the grounded health fraction
(§6.5), or holding somebody in his jaws — all still ground him wherever he happens to be.

⚠ **THAT PRECEDENCE IS LOAD-BEARING, NOT AN OVERSIGHT.** The half-health grounding rule is what
converts an untouchable ranged duel into a winnable melee kill. If a roof could cancel it, a
colony would only have to fight dragons indoors. It also stops the two rules becoming two authors
of one decision: without it, melee range grounds him beside an indoor target every tick while the
roof rule lifts him straight back out. **Force wins; choice does not.**

**Consequence the player will feel, and it is intended:** a pawn who stays under a roof cannot be
brawled, only breathed at. Indoors is now a partial answer to a dragon, as a mountain is a total
one.

Tuning: `dragonNeverGroundedUnderRoof` (default true) turns the whole rule off without a rebuild.

#### 6.5b Untouchable by melee, and unable to melee

Airborne dragons **cannot be hit by melee attacks at all** — only ranged weapons and (by
extension) shouts reach them. In exchange they **cannot melee either**. A soaring dragon is a
shouting platform; a flying one is in transit.

**This is a two-way trade and both halves must ship together.** Only enforcing the immunity
makes an unkillable monster; only enforcing the restriction makes flight pointless.

#### 6.5c The state machine — REDESIGNED 2026-08-04 after the third playtest

> ### A DOVAH DOES NOT IDLE. THIS IS THE GOVERNING STATEMENT AND IT REPLACES THE FIRST DESIGN.
>
> The user: *"a dovah doesn't just idle — it's not a creature like anything else, it's a shard
> of a deity."* The first version modelled him as an animal with a wander loop and a combat
> mode, and it produced exactly the awkwardness that was reported. **Dragons are encountered in
> only three ways, and there is no fourth:**
>
> | encounter | what he does |
> |---|---|
> | **INVADING** | arrives in **manhunter**, already hostile |
> | **PASSING BY** | **in flight, crossing.** Not wandering — going somewhere |
> | **GUARDING** a mound, a site, a hoard | **grounded and MOTIONLESS** until you come close enough, which triggers manhunter |
>
> **So the only idle a player ever sees is a guardian sitting still on the ground.** Everything
> else is flight or a fight. That deletes most of the old state logic rather than adding to it.

| idle situation | state |
|---|---|
| **guarding** — has not been triggered yet | **Grounded, and does not move at all** |
| anything else with no target | **Flight** — he is going somewhere, not loitering |

**There is no wandering-on-the-ground state and no idle soar.** If he is not guarding, he flies.

#### 6.5c-2 The chase — do not change state mid-pursuit

**Reported after the third playtest, and it is the main fix:** *"they are flying, then soaring,
then go down grounded mid-chase — they should always be flying if the targets are far enough."*

The old machine rolled its rhythm every interval regardless of what he was doing, so a dragon
crossing the map after a fleeing colonist kept dropping out of flight. That reads as
indecision, which is the opposite of a shard of a deity.

> **THE USER'S OWN FIX, ADOPTED: he LANDS ON his target — Flight straight to Grounded, skipping
> soar entirely.**
>
> | distance to target | state |
> |---|---|
> | far | **Flight**, and it does not waver — no rhythm rolls while closing |
> | arriving | **lands directly: Flight → Grounded.** Never Flight → Soar → Grounded |
> | in close | the soar/grounded rhythm of §6.5c-3 |
>
> **This also gives the landing impact (§6.5e) a purpose it did not have.** A dragon that drops
> out of the sky onto the pawn it has been chasing, throwing dust and staggering everything
> around it, is an arrival. The same effect on a dragon merely settling down was decoration.

#### 6.5c-4 ATTACK PATTERNS — the real model, given by the user 2026-08-04

**This supersedes the per-interval "rhythm" in §6.5c-3.** A dovah does not roll dice about what
state to be in; he picks an ATTACK and executes it. The patterns below are the user's, verbatim
in structure.

> **THE SHAPE THEY ALL SHARE: FLIGHT IS HOME.** Every pattern starts from flight and ends
> *"circling around"* back in flight. Flight is the connective tissue between attacks, not an
> attack itself — which is why it should never have been competing with soar and grounded for
> "time spent". That framing was wrong and this replaces it.

| # | pattern | sequence |
|---|---|---|
| **1** | **DIVE AND BRAWL** | flight (chase) → **grounded LANDING on the target**, using the landing stun → stay grounded a while, fight close quarters → flight, circle away |
| **2** | **HOVER BREATH** | flight → **soar, static**, dragon breath → flight, circle |
| **3** | **PERCH BREATH** | flight → **grounded, static**, dragon breath → **static soar 1–2 real seconds** → flight, circle |
| **4** | **STRAFING RUN** | flight → **stay in flight, strafing breath** (§4.6a) → flight, circle |

**Pattern 1 is the only one buildable today** — the other three need `Thing_DragonBreath`, which
does not exist yet (§4.6a). Build the framework plus 1 now; 2, 3 and 4 drop in as data when the
breath lands.

**Why pattern 1 is the most important anyway:** it is the one that closes distance, and it is
what makes the landing impact (§6.5e) matter. A dragon dropping out of the sky onto a fleeing
colonist and staggering everything around the impact is the single most readable thing in the
whole design.

**Note pattern 3 deliberately ends in a short static soar before leaving.** That is a
*take-off*: he breathes from the ground, lifts, then goes. It reads as effort, and it is a
window where he is airborne but not yet fast — the one moment ranged fire gets a clean shot at
a hovering dragon.

#### 6.5c-5 Choosing a pattern — PROPOSED, not yet settled

The user asked for further ideas. Nothing here is decided.

**A selection heuristic, so the choice reads as intent rather than randomness:**

| situation | pattern that suits it |
|---|---|
| one target, isolated or FLEEING | **1** — dive and brawl |
| several targets clustered at range | **2** — hover breath |
| defenders behind cover, static siege | **3** — perch breath |
| targets strung out in a line | **4** — strafing run |

Weight the roll toward the fitting pattern rather than picking it outright, so he stays
unpredictable.

**THREE FURTHER PATTERNS PROPOSED:**

- **5. TAIL SWEEP** — grounded, no breath: a knockback burst around himself, then take off.
  **Gives grounded a reason to exist beyond biting**, and it finally uses the tail as the weapon
  §6.6 says it is. Cheap: it is Unrelenting Force's knockback with no damage def of its own.
- **6. THE PASS-OVER** — flight across the colony **with no attack at all**. Purely a tension
  beat: the shadow goes over, nothing happens, and the players scatter anyway. Costs almost
  nothing to build and does more for "shard of a deity" than another damage source would.
- **7. THE FLANK** — flight to the *opposite side* of the colony before attacking, forcing
  defenders to redeploy. Movement as a weapon; no new mechanics at all.

Of the three, **6 is the cheapest and adds the most character**, and **5 is the one that fixes a
real gap** — without it, grounded is only ever "bite the thing in front of me".

#### 6.5c-3 The close-range rhythm — SUPERSEDED by §6.5c-4

Once he is *at* his target, the alternation still applies — bite on the ground, lift to soar to
breathe (he cannot melee while airborne, and cannot be meleed either, §6.5b), come back down.
Dwell times and chances as tuned in `DovahkiinTuningDef.xml`.

> **⚠ MY READING, FLAG IT IF WRONG:** soar keeps its role as the *close-range shouting stance*,
> not as something entered while travelling. "Always flying if the targets are far enough" is
> read as being about the CHASE; soar still happens once he has arrived. If soar should
> disappear from combat entirely, say so — it is a one-line change.

**COMBAT — a rhythm, not a lookup.** The user's target is **SOAR ≥ GROUNDED > FLIGHT** by time
spent, *"and he shouldn't be using grounded or soar state more than the other"*. Flight is used
*"from time to time, to back away from the fight a bit then come back again — either normal
fight or, with the lower chance, the dragon breath strafing"* (§4.6a).

| in combat | behaviour |
|---|---|
| adjacent to a bite target | **Grounded**, not rolled — he *cannot* melee while airborne (§6.5b), so hovering beside a target is the one useless thing he could do |
| from **Grounded** | rolls to take off into **Soar** |
| from **Soar** | two exits — **Flight** (the user's "higher chance") or **Grounded** (the "lower chance") |
| from **Flight** | **always returns to Soar.** In combat, flight is an excursion, never a stance |

> #### ⚠ COOLDOWNS BETWEEN SWITCHES — REQUIRED, AND THEY FIX A REAL DEFECT
>
> The user asked for *"a system of cooldown between each switch"* in **real-time seconds**, per
> state. That is not polish — it is the fix for what the first playtest showed.
>
> **What went wrong without it:** he *"barely switches to soar at all and is using flight a bit
> too often"*. The cause was **flicker**, not weighting: idle-and-moving is Flight and
> idle-and-stationary is Grounded, so every time he started or stopped wandering the state
> flipped. A minimum dwell time in each state removes it.
>
> **Flight's dwell is the SHORTEST of the three on purpose.** Together with "flight always
> returns to soar", that is what keeps flight the rarest state by time even though soar's
> higher-chance exit leads to it.
>
> **The dwell times and the chances weight the outcome together**, which is why both are exposed
> in `DovahkiinTuningDef.xml`. **Expect to tune them in game** — three dwell times against three
> chances is not worth computing on paper. If he flies too much, lower
> `dragonCombatSoarToFlightChance` *or* shorten `dragonMinSecondsFlight`.
>
> **TWO things bypass the cooldown**, and both are facts rather than rhythm choices:
> - **The grounding rule.** Being too hurt to fly is not a decision; waiting out a dwell timer
>   to honour it would read as the rule not working.
> - **HE MUST NOT HOVER.** Reported 2026-08-04: *"he was still capable of being stationary while
>   in flight."* The cause was the dwell timer itself — having entered flight while moving, he
>   was **locked there by his own cooldown** after he stopped. So the fix cannot be a better
>   choice next time round; it has to override the lock. After
>   `dragonFlightStationaryGraceSeconds` motionless in flight, he lands regardless.
>   **A grace period, not an instant drop:** a wandering pawn pauses between destinations, and
>   landing him on every pause would reintroduce the flicker the dwell timer exists to cure.

#### 6.5e The landing — dust and a stagger

**User's request, 2026-08-04:** *"it would be quite nice if dovahs going from soar to grounded
causes a little dust flying around them and causes a brief stun."*

**Fires ONLY on soar → grounded**, because that is the only transition that is a landing. Flight
never drops straight to grounded (in combat it always exits via soar), and leaving grounded is a
take-off, not an impact. **Gate it on the SOURCE state, not on "is now grounded"** — otherwise a
take-off throws dust too.

Small on purpose: this is the thump of a heavy animal touching down, not a shout. All three
numbers (`dragonLandingDustPuffs`, `dragonLandingStunRadius`, `dragonLandingStunTicks`) are in
`DovahkiinTuningDef.xml`. The stun is deliberately brief so repeated landings cannot stunlock.

It reuses `FleckMaker.ThrowDustPuffThick` and the same stun call the Ancient Dragonborn's
arrival already uses, so the two impacts read as belonging to one mod.

> **THE ONE HARD RULE, GIVEN BY THE USER: at or below HALF HP (or equivalent injury) the dragon
> STAYS GROUNDED.** This is the player's reward for winning the first half of the fight — it
> converts an untouchable ranged duel into a melee kill, and it is what stops a wounded dragon
> kiting forever.
>
> **ANSWERED 2026-08-04, AND THE ANSWER IS BETTER THAN THE QUESTION.** It is **NOT a coded
> latch**. The user: *"dovahs shouldn't have unusual regeneration — same natural slow
> regeneration and wound mechanics used by the entities of the vanilla game. Their strength
> should rely on having a lot of HP… once they fall into the perma-grounded category they are
> basically doomed to stay that way, unless they are somehow not that much wounded and heal
> naturally over time enough to fly off again."*
>
> So: **a plain threshold check, re-evaluated normally, plus VANILLA healing.** No latch
> variable, no special case, nothing to save or reload.
>
> **Why this is the right design and not merely simpler:**
> - **The latch emerges from the healing rate instead of being coded.** RimWorld's natural
>   healing is slow, so within a single fight a grounded dragon stays grounded — which is
>   exactly the behaviour the latch was meant to guarantee — while nothing has to remember a
>   flag or restore it across a save.
> - **It makes a dragon a recurring threat rather than a one-off.** One that breaks off, escapes
>   and heals over days can return to the air. A hard latch would have made every survivor
>   permanently crippled, which is a worse story.
> - **Their strength is HP, not regeneration.** Dovah have far more of it than any vanilla
>   creature (scaled per body part, §6.6) and heal like everything else. **No custom healing
>   code of any kind** — if a dragon ever appears to regenerate unusually, that is a bug.

#### 6.5d Diagonal facings — FLIGHT ONLY

The user, 2026-08-04: entities move diagonally, so a flying dragon locked to four facings reads
badly. **Generate four diagonal sprites for the FLIGHT state only, by re-orienting the existing
flight art — no new image generation.** Flight is top-down, so this is a pure rotation, exactly
as `MakeFlightRotations.ps1` already produces the four cardinals from one drawing.

Soar and grounded keep four facings; they are eye-level projections and cannot be rotated
(see `Tools/DRAGON_ART_PIPELINE.md`).

> **THE CONSTRAINT IS REAL, AND THERE IS A ROUTE AROUND IT THAT NEEDS NO RENDER PATCH.**
> Verified 2026-08-04 by decompiling:
>
> - **`Rot4.RotationCount` is 4.** North/East/South/West and nothing else. The engine has no
>   diagonal facing for pawns, and `PawnGraphicSet` picks its material with a bare
>   `nakedGraphic.MatAt(facing)`. So eight-way facing cannot be expressed the normal way.
> - **But `Verse.AI.Pawn_PathFollower.nextCell` is a PUBLIC FIELD**, and `Moving` / `MovingNow`
>   are public properties. So the dragon's true heading — diagonals included — is readable with
>   no patch and no reflection.
>
> **THE ROUTE: eight flight graphic sets, one per compass octant, each a `Graphic_Multi` whose
> FOUR SLOTS ALL HOLD THE SAME rotated image.** Then whichever `Rot4` the engine hands the pawn,
> the correct octant sprite is what draws. Pick the set from `nextCell - Position` each time the
> heading changes, using the **same `nakedGraphic` swap that is already proven in play** (Test 1,
> 2026-08-04).
>
> That is why this is cheap: it reuses a mechanism that works rather than inventing one. It is
> verified at the API level but **not yet tested in game** — do not record it as working until
> it has been.
>
> Flight is top-down, so all eight come from rotating ONE drawing, exactly as
> `MakeFlightRotations.ps1` already produces the four cardinals.

> ### ⚠ OPEN — THE SUMMONS AND TARGETING. **DIAGNOSIS CONFOUNDED — DO NOT DECIDE YET.**
>
> Reported 2026-08-04: Alduin *"couldn't attack the ancient dragonborn… the ancient dragonborn
> and the hero of valor are supposed to be targetable, [their] primary role is to help divide
> enemy attack concentration, 'help tanking'."*
>
> > **🔴 CORRECTION, SAME DAY. THE INVISIBILITY WAS DIAGNOSED TOO FAST, AND THERE IS A SIMPLER
> > CAUSE THAT WAS OURS.** The game log — which was not read until the user asked for it —
> > carried six config errors: **three of Alduin's four melee tools named body part groups that
> > vanilla `Bird` does not have** (`Teeth`, `FrontLeftLeg`, `FrontRightLeg`; Bird has only
> > `Beak`, `Feet`, `HeadAttackTool`). Only the tail tool survived, at `chanceFactor` 0.4.
> >
> > **So the test dragon had almost no working attack at all**, which explains the observation
> > without invisibility being involved. Fixed; **retest before treating any of the options
> > below as necessary.**
> >
> > The lesson is the project's own, hit again: *a clean build and a "does every def parse"
> > check both pass this.* Parsing proves the XML is well-formed, not that the names in it
> > resolve. **READ THE LOG AFTER ADDING A DEF.**
>
> **The invisibility finding below is still TRUE as a fact about the engine** — it is simply no
> longer established as the cause of what the user saw.
>
> The notebook had predicted that
> `HediffComp_Invisibility` "makes the pawn hard to TARGET"; scanning all 12,819 types for
> callers of `PawnUtility.IsInvisible` confirms it and names them:
>
> | caller | effect |
> |---|---|
> | **`Verse.Pawn.ThreatDisabled`** | **the decisive one — an invisible pawn is not a threat at all** |
> | `JobGiver_AIFightEnemy.TryGiveJob` | AI will not pick them as a target |
> | `JobGiver_ReactToCloseMeleeThreat` | AI will not react to them in melee |
> | `JobGiver_Berserk.FindPawnTarget` | berserk pawns skip them |
> | `Toils_Combat.FollowAndMeleeAttack`, `JobDriver_AttackStatic` | attacks in progress drop them |
> | `Verse.GenUI.TargetsAt` | **the player cannot click them either** |
>
> So the summons' invisibility is not merely cosmetic — it removes them from the game's threat
> model entirely, which is the exact opposite of the tanking role they were designed for.
>
> **This is a real trade and the user has to pick, because both options cost something:**
>
> **A. Drop `HediffComp_Invisibility` from the summons.** They become targetable immediately, no
> patches, no new mechanism. **The cost is that their look changes**: the spectral appearance IS
> that comp swapping the material to a pale cyan-white at 50% alpha, and the armour overlay's
> palette was authored FOR a translucent body — the notebook measured the armour reading 12.6%
> darker over an invisible pawn than an opaque one. So the armour would come out brighter than
> the version that was signed off.
>
> **B. Keep the look, patch the targeting.** Harmony-patch `Pawn.ThreatDisabled` (and probably
> two or three JobGivers) to exempt our summons. **The cost is several patches on the AI
> targeting path**, which is a class of thing this mod has deliberately avoided, and each is a
> place another mod can collide with us.
>
> **Recommendation: A.** It is one line, it cannot break under another mod, and the visual cost
> is a palette re-tune of art we can regenerate — against B's permanent maintenance burden on
> the AI path. But the art is signed off, so **it is the user's call, not mine.**

### 6.6 Dragon anatomy, and what wounds actually do

**Given by the user 2026-08-04.** A dragon needs its own `BodyDef` — vanilla `Bird` is the
placeholder used by the test creature and is not the answer.

**Parts: tail, left wing, right wing, maw, legs, torso.**

> **BUILT 2026-08-04 — `Defs/Bodies/Dovah_Dovahkiin.xml` (`Dovahkiin_Dovah`).** It replaces
> vanilla `Bird`, which was a stopgap and was wrong three ways at once:
>
> 1. **Its jaws part is literally called a BEAK**, so the combat log credited the dragon's bite
>    to a beak — the user spotted it in a pawn's wounds. **Renaming the TOOL would not have
>    fixed it: wound text comes from the BODY PART.**
> 2. No wings to bash with.
> 3. No wings to **cripple**, so the grounding rule below could not exist at all.
>
> Vanilla has no wing `BodyPartDef`, so `Dovahkiin_DovahWing` is defined. Everything else reuses
> vanilla parts — a duplicate part def is just two things to keep in step. The wings carry
> **generous coverage (0.11 each)**: they are what a shooter aims for, and the grounding rule is
> only interesting if they are actually hittable. The maw is small — breaking a dragon's bite
> should take aim. Wing wounds use `permanentInjuryChanceFactor 1.0`: a dragon that shrugs off a
> shredded wing an hour later makes the whole rule pointless.

#### 6.6a His melee rolls — not all bites

**User, 2026-08-04.** Weighted so his attacks read as a repertoire rather than one animation:

| attack | damage | frequency | extra |
|---|---|---|---|
| **maw** | **pierce** | **most likely** | the signature attack |
| **wing bash** | blunt | middling | no stun |
| **tail sweep** | blunt | **rarest** | **stuns briefly** |
| claws | pierce | uncommon | — |

The tail sweep hits hardest of the blunt two, to be worth landing. **Its stun is applied in
code** — a vanilla `tool` has no stun field.

**NO CLAW ATTACK.** Removed at the user's instruction: *"dovahs have no claw attacks"*. His feet
carry him; they are not a weapon.

#### 6.6b THE GRAB — "bite down, shake left-right, throw away"

**The user's iconic move, 2026-08-04.** *"A very very small chance… the pawn or creature would be
locked on the dragon while the dragon alternates between east and west view for a bit, then the
victim is projected away."* They added: *"might be a real headache but also a nice surprise for
the player."*

**Triggered only by the MAW**, on a landed hit, at a deliberately tiny chance (2% by default).
A wing bash or tail sweep obviously cannot grab.

| step | what happens |
|---|---|
| seize | the victim is stunned, dragged to the dragon's cell, and held |
| shake | the dragon's facing flips **east ↔ west** on a timer — the whole animation |
| release | **slash AND pierce** damage, then **downed**, then **thrown** several cells |

> **THE SHAKE USES THE TWO PROFILE SPRITES WE ALREADY SHIP.** RimWorld has no animation system
> reachable from here, but flipping a facing back and forth reads exactly as a beast worrying
> something in its jaws. That is why the user's suggestion works at all — it is an animation
> made of the only thing the engine will let us move.
>
> **THE STATE LIVES ON THE VICTIM, AND THAT IS THE WHOLE SAFETY ARGUMENT.** A multi-second
> scripted sequence binding two pawns is exactly the bespoke cross-save state `RISKS.md` §9 puts
> at the top of the corruption risks. As a hediff on the victim: the game saves and restores it
> for us, a mid-grab save simply continues on load, and **every exit path releases** — including
> the one that fires when the dragon dies or despawns mid-shake. **A grapple must never leave a
> pawn permanently frozen**, and this one structurally cannot.
>
> **Downing uses vanilla's `HealthUtility.DamageUntilDowned`** rather than hoping the damage
> lands it. The user said *downed*, not *usually downed*, and a heavily armoured target would
> otherwise shrug it off. That helper sets `forceDowned` around the wounds it inflicts, so it
> cannot kill.
>
> **The throw reuses the shout knockback, NOT a `PawnFlyer`.** The notebook records a pawn being
> **destroyed with no corpse** when a flyer was started from the wrong place — and a rare
> surprise move is the worst possible place to risk that, because it would be almost impossible
> to reproduce.

| damage | consequence |
|---|---|
| **50% to EACH wing, or 80% to a SINGLE wing** | **forced to GROUNDED** |
| enough wounds to the **legs** | **reduced move speed** |
| enough damage to the **maw** | **reduced piercing damage** (the bite) |
| enough damage to the **tail + wings** | **reduced blunt damage** |

**Why this matters beyond flavour:** it makes shooting a dragon *tactical* rather than a damage
race. Crippling the wings brings it down; breaking the maw defangs its bite; the legs stop it
escaping. It also gives the half-HP grounding rule a second, more skilful route — a good shot
can ground a dragon at full health by aiming at the wings.

**The thresholds are deliberately unnamed here.** They go in `DovahkiinTuningDef.xml` so they
can be retuned without a rebuild, per `CLAUDE.md`.

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
  `(80, 80)` — about 10% of a 250×250 map, not "a large fraction" of it.

  > ### ⚠ THE CONCLUSION THAT USED TO FOLLOW HERE IS SUPERSEDED — CORRECTED 2026-08-13
  >
  > This paragraph used to end *"the ordering, the terminal word wall and the sealed treasure
  > room are therefore a **custom `GenStep`** … the biggest single risk in the project"*, and
  > `ROADMAP.md` Phase 5 still repeats it. **`RISKS.md §1` reversed that and the spec was never
  > updated** — a stale conclusion sitting in the contract, which is exactly the trap this
  > project keeps recording about numbers.
  >
  > **The facts above are all still true. The conclusion is not.** Vanilla's complex generator
  > is the wrong tool, but the answer is not to write a better one: **VEF's KCSG lets the crypt
  > be BUILT BY HAND IN-GAME in dev mode and exported as a `StructureLayoutDef`**
  > (`Dialog_ExportWindow`, `KCSG.GenStep_CustomStructureGen`, `KCSG_UndergroundRoom`,
  > `linkWithSite`). Dragon's Descent already ships a 110 KB hand-drawn dungeon this way in 1.4.
  >
  > Because the layout is authored, **every guarantee in this section holds by construction** —
  > if the word wall is at the end of the map you drew, it is at the end. There is no algorithm
  > to fight. Crypts stop being an ENGINEERING problem and become an AUTHORING one, which is
  > both far more predictable and **work the user can do themselves**.
  >
  > **Author 3–4 layouts per tier and pick randomly**, so crypts vary without being procedural.
  >
  > **⚠ THE RESIDUAL RISK IS THE DEPENDENCY, AND IT COLLIDES WITH INVARIANT 5.** KCSG is VEF, so
  > without VEF there are no crypts — and this section calls crypts "the primary way the player
  > grows their shout library". No crypts → no words → no shouts → an empty shell on the
  > baseline, which is *broken*, not dormant. **The resolution, on record in `RISKS.md §3`:
  > DRAGON MOUNDS (§7.1) ARE THE GUARANTEED WORD SOURCE, crypts the rich one.** Build mounds
  > first; declare VEF `MayRequire` and say plainly that crypts need it.

- **Some crypts have a Dragon Priest** as the final guardian — the highest-tier non-dragon
  threat in the mod, mask included as unique loot.
- **THE CRYPT AUDIO EXISTS ALREADY — `Sounds/DungeonBackgroundNoise.mp3`**, recorded by the user
  2026-08-13, 15.408s. Recorded here so it is not lost; nothing to attach it to until this
  section is built.

  **⚠ IT PLAYS ONCE, ON FIRST ENTRY. IT IS NOT A LOOPING AMBIENCE.** The user's correction,
  2026-08-13, given because this spec briefly said the opposite: *"plays only once when you first
  enter into a Nordic crypt."*

  **⚠ AND IT IS FOR CRYPTS ONLY — NOT §7.1 DRAGON MOUNDS, NOT §7.2 BURIAL SITES.** Those are
  open-air and get nothing. The name says "dungeon"; the two open-air site types are not one.

  **The design consequence, which is the part that will be missed:** *"first entry"* is
  per-site state that has to survive a save and a re-visit — leaving and returning must not
  replay it. That is a saved flag on the site or its map component, not a one-shot fired from
  a map-enter hook.

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

*Dawnguard-inspired.* **REVISED 2026-08-01 — it is a THREE-sided structure, not two, and the
Volkihar are Vampire LORDS rather than sanguophages:**

| side | who | how many |
|---|---|---|
| the mortals | a **human** faction — **Divine Order** is the user's preferred casting (*"much better visually and immersively"*), a **RECOMMENDED mod, never a requirement** | as the world generates |
| the vampires | **ordinary `Vampire` factions** — *"a few vampires rogue or organised factions here and there"* | **PLURAL, and deliberately so** |
| the court | **the Volkihar court — `Vampire Lord`s** | **EXACTLY ONE, uniquely** |

**The Volkihar court must exist as EXACTLY ONE faction on the world map.** Not one settlement —
one faction, uniquely. That is a hard constraint of the same family as the one-Dovahkiin and
one-Alduin invariants, and it belongs in the registry rather than being left to faction
generation.

**The ordinary vampire factions are NOT unique** — several may exist, some rogue and unorganised,
some structured. **Do not apply the Volkihar uniqueness rule to them**; the contrast between a
scattered vampire underclass and one singular court is the point of the structure.

> **THIS SUPERSEDES the earlier line calling the Volkihar "a sanguophage faction".** Under §15.7's
> precedence sanguophages are now the *weakest* tier, converting nobody — so a court of them
> would be the least threatening faction in the war rather than its apex. Sanguophages still
> exist in the world as Biotech generates them; they are simply prey and recruiting stock for the
> two vampire tiers, not a side.

At some point the Dovahkiin is **invited to the Soul Cairn**, and returns after a delay with
**Summon Durnehviir**. That in turn **triggers the side quest to summon Durnehviir, which rewards
SOUL TEAR**.

> **CONSEQUENCE, AND IT CONTRADICTS SHIPPED CONTENT: SOUL TEAR MUST NOT BE FOUND ON WORD WALLS.**
> It is a questline reward. Soul Tear is **already built, playtested and signed off** as a normal
> shout — so its three words need the `questOnly` flag added, and **the Phase 7 word-wall count
> drops by three.** §4.4's re-cost problem gets *easier*, not harder. Do not miss this when
> Phase 7 is planned.

> ### ⚠ AND THAT LEAVES A HOLE. SOUL TEAR BECOMES UNOBTAINABLE WITHOUT BIOTECH — DECIDE THIS.
>
> Follow the chain the two 2026-08-01 rulings create together:
>
> 1. Soul Tear leaves the word walls; it is now earned from the Durnehviir side quest.
> 2. That side quest is triggered by returning from the Soul Cairn, inside the vampire questline.
> 3. The vampire questline is `MayRequire`-gated on Biotech.
> 4. **So on a Biotech-less install, a fully built and signed-off shout can never be acquired.**
>
> **ANSWERED 2026-08-01, same day it was raised. The user: *"soultear becomes a word wall if
> biotech isn't on."*** Soul Tear's words are **conditionally quest-locked**:
>
> | install | how Soul Tear is learned |
> |---|---|
> | **with Biotech** | questline reward, via the Durnehviir side quest. NOT on walls |
> | **without Biotech** | ordinary word walls, exactly as it ships today |
>
> **This is the first CONDITIONAL word in the design, and Phase 7 must be built for it.** The
> `questOnly` flag on `WordOfPowerDef` is currently a plain bool; Soul Tear's three words need it
> to be *conditional on a loaded mod* instead. Simplest workable shape: keep the bool, and let a
> `MayRequire`d patch set it — so on a Biotech install the flag is on and Phase 7 skips those
> three, while on a baseline install it is never set and they scatter normally.
>
> **The wall count is therefore install-dependent: 42 without Biotech, 39 with it.** §4.4's
> re-cost must be done against the LARGER number, or a baseline game runs short of walls.

**Home and dungeons:** *Gothicstyle Vampire Furniture* is the user's preferred dressing for the
Volkihar home and for any vampire den or dungeon encounter. **Recommended, never required.**

> **BIOTECH — ANSWERED 2026-08-01. THIS QUESTLINE REQUIRES IT; THE MOD STILL DOES NOT.**
>
> The user: *"who is still playing vanilla without biotech and royalty at least on?? make biotech
> a hard requirement (the price to pay for not having would be having no dawnguard related quest
> at all then)"*
>
> **The parenthetical is the operative half and it is narrower than the opening sentence.** "The
> price to pay for not having it is no Dawnguard quest" only makes sense if the mod still LOADS
> without Biotech — a genuine hard requirement has no price, the mod simply refuses to run. So:
>
> - **The vampire war questline is `MayRequire`-gated on `Ludeon.RimWorld.Biotech`** and is
>   silently absent without it. The Volkihar clan really is sanguophages; no non-Biotech
>   substitute faction is built.
> - **`CLAUDE.md` invariant 5 stands unchanged.** The mod still loads and plays on Core + Royalty
>   + Ideology + Harmony + HugsLib.
>
> **Making Biotech a GLOBAL hard requirement would buy nothing.** This questline is the only
> Biotech-dependent content in the design — gating it achieves exactly the outcome the user
> described, while a global requirement would additionally rewrite invariant 5, `ROADMAP.md`'s
> universal exit criterion 5, and every phase's load-test matrix. All cost, no gain. **If a
> future session is ever told "make Biotech required", check whether gating the content is
> enough first — it was, here.**
>
> *(Royalty needs no decision: it is already in the baseline.)*
>
> **What "silently absent" must mean in practice:** no dangling quest that cannot complete, no
> letter referencing a faction that does not exist, and no red errors on a Biotech-less load.
> The whole questline — faction, quests, Durnehviir, and Soul Tear's reward path — is one gated
> unit. **Soul Tear itself is NOT gated**; see the word-wall consequence above, which applies on
> every install.

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

### 15.6 What a vampire IS, mechanically — race vs xenotype vs hediff

> **The user's question, 2026-08-01, and their own analysis was correct:** sanguophages are
> *bio-engineered* — they shrug off blood loss, fire biological projectiles, longjump, and are
> "physically vampires". **Skyrim vampires are magical**: no biological projectiles, no longjump,
> **blood boils in sunlight**, starvation drives them into an uncontrollable primal hunting
> madness, and they cast magic. Art is minimal — *orange glowing eyes, pale skin, fangs hidden in
> the mouth*.
>
> ### ⚠ THE FIRST ANSWER TO THIS WAS WRONG. CORRECTED 2026-08-01. USE THIS SECTION, NOT THE ONE BELOW IT.
>
> I recommended **a hediff, not a xenotype**, and argued at length against making "a new race".
> **The user never asked for a race.** Their question said *"a new race (with sanguophages's
> genetic overwritte active skill)"* — and the parenthetical was the actual request: **the
> XENOTYPE mechanism.** I answered the literal word and missed it.
>
> Their correction, and every clause of it checks out against the assembly:
>
> *"sanguophage isnt a race now is it? you aren't born one, you become one, and while you become
> one every of your base trait actually is kept, only a few are added on top of it... the glowing
> eyes shouldnt be an overlay like dragon aspect, it should be using biotech's features, changing
> the apparence for real not just as an overlay."*
>
> **CORRECT ON ALL COUNTS. VERIFIED, NOT CONCEDED:**
>
> | claim | evidence in 1.4's own assembly and defs |
> |---|---|
> | sanguophage is not a race | it is a `XenotypeDef`; the race stays Human |
> | you become one, keeping what you were | xenogerm implantation swaps genes, not the pawn |
> | genes change appearance **for real** | `GeneDef.graphicData` → `GeneGraphicData` with `graphicPath`, `drawLoc`, `layer`, `colorType`, `useSkinShader` |
> | pale skin | `GeneDef.skinColorOverride` / `skinColorBase` |
> | **glowing eyes specifically** | **`GeneGraphicData.drawOnEyes`** — and Biotech's own `GeneEyeColor` abstract base uses exactly that, with `drawOnEyes: true` and `layer: PostTattoo` |
> | genes can carry behaviour | `GeneDef.geneClass` (defaults to `Gene`, so a subclass can tick), plus `abilities`, `forcedTraits`, `statOffsets`, `capMods`, `makeImmuneTo` |
>
> **SO THE ANSWER IS: A XENOTYPE MADE OF CUSTOM GENES.** Not a race, and not the overlay I
> proposed. An overlay would have been *painting over* the pawn; genes change what the pawn IS,
> which is what the user asked for and is also simply better.
>
> **A NOTE ON HOW THIS USER WRITES, BECAUSE IT HAS NOW COST TWO WRONG ANSWERS IN ONE DAY: THE
> OPERATIVE INSTRUCTION IS OFTEN IN THE PARENTHETICAL.**
> - *"make biotech a hard requirement (the price to pay for not having would be having no dawnguard
>   related quest at all then)"* — the parenthetical means gate the CONTENT, not the mod.
> - *"a new race (with sanguophages's genetic overwritte active skill)"* — the parenthetical means
>   the xenotype mechanism, not a race.
>
> **Read the whole message before answering the first clause of it.**

### What to build, corrected

**A `XenotypeDef` with custom `GeneDef`s.** Biotech already provides nearly all of it natively:

| Skyrim vampire trait | Biotech feature |
|---|---|
| pale skin | `skinColorOverride` on a cosmetic gene |
| glowing orange eyes | an eye gene with `drawOnEyes` + `color`, modelled on `GeneEyeColor` |
| fangs | nothing to draw — the user's own point |
| blood hunger | **hemogen already exists** (`Gene_Hemogen`), and it is the sanguophage's own resource |
| bloodfeeding | Biotech's `Bloodfeed` ability, reusable as-is |
| burns in sunlight | **custom** — a `geneClass` subclass ticking a roof/daylight check, the same test Storm Call already does |
| feeding madness | **custom** — a `MentalStateDef` driven off the hemogen need |
| casts magic | `GeneDef.abilities` |

**Only two of those need writing.** The rest is configuration of features that already ship.

**Honest caveat on "glowing":** `drawOnEyes` draws a coloured graphic over the eyes; whether it
reads as *glowing* is a property of the texture, not of an emissive shader. A bright saturated
orange on a pale face will read as glowing at play distance. If a true light-emitting glow is
wanted, that is a separate and harder question — raise it rather than assume the gene delivers it.

### 15.7 THE TWO VAMPIRE XENOTYPES — full spec, given by the user 2026-08-01

**Two new `XenotypeDef`s: `Vampire` and `Vampire Lord`.** Both are acquired, never born into.

*(The user's note on the earlier confusion: "the fault is on my behalf, I missed to precise you
the term xenotype." Recorded because it is gracious and because the real lesson is the
parenthetical rule above, not who mis-worded what.)*

#### Traits, and how the two tiers differ

| trait | Vampire | Vampire Lord |
|---|---|---|
| orange glowing eyes | yes | yes |
| pale skin | yes | yes |
| night vision | yes | yes |
| **frost resistance** | yes | **same — NOT increased** |
| **fire weakness** | yes | **same — NOT increased** |
| blood filtration | raised | **more pronounced** |
| manipulation | raised | **more pronounced** |
| melee | raised | **more pronounced** |
| speed | raised | **more pronounced** |
| *(other body function)* | raised | **more pronounced** |

**THE FIRE AND FROST NUMBERS ARE DELIBERATELY FLAT ACROSS BOTH TIERS.** The user singled them
out — *"more pronounced on being a vampirelord (appart from the frost and fire effects)"*. A Lord
is a stronger creature, not a differently-vulnerable one. Do not "improve" this into a ladder.

#### Mapping each trait to what actually exists — checked, not assumed

| trait | implementation |
|---|---|
| orange glowing eyes | a gene with `graphicData.drawOnEyes` + `color`, modelled on Biotech's `GeneEyeColor` |
| pale skin | `GeneDef.skinColorOverride` |
| night vision | **Biotech already ships a dark-vision gene — reuse it, do not write one** |
| blood filtration / manipulation | `capMods` (`BloodFiltration`, `Manipulation`) |
| speed | `statOffsets` → `MoveSpeed` |
| melee buff | `MeleeDamageFactor` — **normally forbidden by invariant 5 as Biotech-only, but this content is Biotech-gated anyway (§15.2), so it is fair game HERE and only here** |
| fire weakness | negative `ArmorRating_Heat` offset |
| **frost resistance** | ⚠ **THERE IS NO FROST RESISTANCE STAT IN VANILLA.** Already learned the hard way on Dragon Aspect: `ArmorRating_Heat` really is fire, but cold has no damage-armour equivalent — `Insulation_Cold` is weather comfort only. So this must be `Insulation_Cold` **plus** resistance to this mod's own Frost Breath chill, or it will do nothing in a fight. See §5 of the save notebook. |

#### EXCLUSIVITY — a hard rule, in the same family as the one-Dovahkiin invariant

**A pawn may be at most ONE of: Vampire, Vampire Lord, Sanguophage.** Never two, never all three.

**And the precedence is directional: a vampire cannot convert a Vampire Lord back down into an
ordinary vampire.** The user's words, and it is a rule about *conversion attempts*, not only about
the end state — the bite must be refused or be a no-op, not silently demote a Lord.

**THE FULL PRECEDENCE, SETTLED 2026-08-01:**

```
VAMPIRE LORD  >  VAMPIRE  >  SANGUOPHAGE
```

The user's reasoning, worth keeping because it decides every future edge case: **"supernatural
beats the natural after all."** Sanguophages are bio-engineered; the vampires are not.

| tier | can convert | can be converted by |
|---|---|---|
| **Vampire Lord** | vampires, sanguophages | nobody |
| **Vampire** | sanguophages | Vampire Lords |
| **Sanguophage** | **NOBODY** | both |

**Sanguophages lose their defining trick here.** In vanilla a sanguophage's bite converts; under
this rule it converts nothing, because everything above it is supernatural and everything below
it is an ordinary human. **That is a deliberate demotion of a vanilla mechanic and it must be
enforced, not just documented** — a sanguophage attempting to convert a vampire or a Lord is a
refusal, and the refusal should say why rather than silently failing.

#### The blood drain

**Both vampire tiers get a `blooddrain` ACTIVE ability.** Sanguophages keep Biotech's own
`Bloodfeed`; this is ours and sits alongside it.

> **DESIGN DEFERRED BY THE USER — *"we'll work on that later."* Do not invent its numbers,
> targeting, cost or whether it converts. It is named here so it is not forgotten, and that is
> all it is.**

**Implementation notes:** gene-level exclusivity is `GeneDef.exclusionTags` (Biotech's own
`GeneEyeColor` uses `EyeColor` for exactly this). But exclusion tags alone will NOT deliver this
rule — a pawn carries one `XenotypeDef` yet genes can be mixed via xenogerms, so the guarantee has
to be enforced at every **conversion** path as well, and refusals must be explicit rather than
silent. **This is the same shape as `GameComponent_DragonbornRegistry`'s one-Dovahkiin invariant,
and it should be owned in one place for the same reason.**

### The one thing that survives from the wrong answer

**A NEW RACE IS STILL THE WRONG SHAPE** — and the user agrees, since they were never asking for
one. Kept because the reasoning is sound and someone may propose it again:

1. **`CLAUDE.md` invariant 4 forbids race swaps outright**, and invariant 3 already settled this
   exact shape for the Dovahkiin: *"not a xenotype, not a race, not a gene — a trait + hediff +
   title on an otherwise ordinary pawn of any race or xenotype."* Vampirism is the same kind of
   thing and should be the same kind of implementation.
2. **The player is meant to BECOME one.** A race swap on an existing colonist is the worst case:
   it discards apparel fit, body art, and every compatibility this mod has with the 40 installed
   mods. A hediff added to the pawn they already are costs nothing and is trivially reversible.
3. **A new race needs its own body art for every apparel item** in the modlist. That is the
   §15.5 art ceiling again, at ten times the scale.

> **~~BUILD IT AS A HEDIFF~~ — SUPERSEDED by the corrected answer above. A xenotype of custom
> genes is the right shape: it changes the pawn for real, which is what was asked. The table
> below is kept because the *mechanisms* named in it are still the reference for the two genes
> that need custom code — the sunlight burn and the feeding madness.**

| Skyrim vampire trait | machinery that already exists here |
|---|---|
| pale skin, glowing eyes | `Thing_DragonAspectOverlay` — paints art onto a pawn, no render patch, and as of 2026-08-01 takes **multiple texture sets** |
| burns in sunlight | the roof/outdoor check Storm Call already does, on a `TickRare` |
| blood hunger as a resource | `Need_Thuum` — a custom Need attached to ONE pawn via `causesNeed` + `onlyIfCausedByHediff` |
| starvation → primal madness | `MentalStateDef`, as Dismay already uses for `PanicFlee` |
| casts magic | `AbilityDef` + comps, which is the whole shout system |

**So the ordinary vampire is CHEAP.** Its art is a tint and two glowing dots — nothing like the
Vampire Lord's winged silhouette. **Keep those two asks apart:** §15.5's blocker does not apply
here, and conflating them would make an easy feature look impossible.

**Biotech's role, given §15.2's gate:** hemogen and bloodfeeding are genuinely good and genuinely
Biotech. Use them **as an enhancement layered on the hediff** where present — not as the thing
vampirism *is*. The Volkihar faction is Biotech-gated anyway, so the question only matters if
vampirism is ever wanted outside that questline.

**The one thing worth stealing from sanguophages regardless:** their *xenotype* is the vanilla
answer to "how does a pawn get converted", and reading how `Xenogerm` implantation converts a
colonist is the right reference for how the Dawnguard choice should feel — even if the
implementation is a hediff rather than a genome.
