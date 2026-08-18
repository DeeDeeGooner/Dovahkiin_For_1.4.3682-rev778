// Implements: SPEC.md (tuning), CLAUDE.md "Anti-patterns" - all tuning numbers live in one def,
// never inline, so balance can be changed without a rebuild.
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// The single def that holds every tunable number in the mod.
    /// One instance only, defName "Dovahkiin_Tuning". Reached via <see cref="Current"/>.
    /// Fields are added here as each phase needs them; nothing else in the mod may hardcode a
    /// balance number.
    /// </summary>
    public class DovahkiinTuningDef : Def
    {
        // --- Phase 1: identity (SPEC.md 3.3, 5.4) ---

        /// <summary>Chance a dragonblood heir awakens on their single roll. SPEC.md 3.3.</summary>
        public float heirAwakenChance = 0.02f;

        /// <summary>Days the registry waits after a Dovahkiin dies before the slot reopens. OD-1.</summary>
        public float slotReopenDelayDays = 8f;

        /// <summary>Flat opinion bonus everyone has toward the Dovahkiin. SPEC.md 5.4.</summary>
        public int opinionBonusIsDovahkiin = 12;

        /// <summary>Permanent opinion bonus per witnessed soul absorption. SPEC.md 5.4.</summary>
        public int opinionBonusWitnessedAbsorption = 6;

        // --- Phase 2: the Voice (SPEC.md 4, 5.2) ---

        /// <summary>Max Thu'um gained per dragon soul. Linear and uncapped by design. OD-9.</summary>
        public float thuumPerSoul = 2f;

        /// <summary>Starting max Thu'um at zero souls.</summary>
        public float thuumBaseMax = 10f;

        /// <summary>Highest word level a shout can reach. SPEC.md 4.1.</summary>
        public int maxWordLevel = 3;

        /// <summary>Souls needed to raise a shout by one level. SPEC.md 4.1.</summary>
        public int soulCostPerWordLevel = 1;

        /// <summary>Thu'um regenerated per day, as a fraction of max. SPEC.md 5.2.</summary>
        public float thuumRegenPerDay = 12f;

        /// <summary>Strain severity added per cast, and how much each point lengthens recovery. SPEC.md 4.2.</summary>
        public float voiceStrainPerCast = 0.34f;
        public float voiceStrainCooldownFactor = 0.6f;

        /// <summary>How far a shout is heard for the witness mood boost. SPEC.md 5.4.</summary>
        public float shoutWitnessRadius = 30f;

        // --- Phase 2: Slow Time (SPEC.md 4.4a) ---

        /// <summary>
        /// Attack cooldown multiplier while Slow Time is up, indexed by shout level 1-3.
        /// Below 1 is faster; 0.40 means a swing takes 40% as long. Applied to melee and ranged
        /// alike in Patch_VerbProperties_AdjustedCooldownTicks.
        ///
        /// This lives here rather than on the hediff because there is no vanilla pawn-side stat
        /// for melee cooldown to hang it on - see that patch's comment.
        /// </summary>
        public List<float> slowTimeCooldownFactorByLevel = new List<float> { 0.75f, 0.55f, 0.40f };

        // --- Phase 2: Storm Call (SPEC.md 4.4e) ---

        /// <summary>Lightning strikes per cast, indexed by shout level 1-3.</summary>
        public List<int> stormCallStrikesByLevel = new List<int> { 3, 6, 12 };

        /// <summary>
        /// How long the storm lasts, by level. Strikes are spread evenly across it, so raising
        /// this without raising the strike count makes the storm slower rather than bigger.
        /// </summary>
        public List<int> stormCallDurationTicksByLevel = new List<int> { 180, 420, 900 };

        /// <summary>
        /// Radius the shout searches for legal targets, measured from the caster's CURRENT
        /// position each strike - the storm follows the Dragonborn rather than staying where
        /// it was cast. Raised 25 -> 38 (+50%) then 38 -> 46 (+20%) over two playtests.
        /// </summary>
        public float stormCallRadius = 46f;

        // --- Phase 2: Soul Tear and the dead puppet (SPEC.md 4.4f) ---

        /// <summary>Chance a Soul Tear hit raises a dead puppet, indexed by level 1-3.
        /// Level 1 is 0 by design: damage only.</summary>
        public List<float> soulTearPuppetChanceByLevel = new List<float> { 0f, 0.25f, 0.45f };

        /// <summary>How long a dead puppet serves before dying, in in-game hours, by level 1-3.</summary>
        public List<float> soulTearPuppetHoursByLevel = new List<float> { 0f, 6f, 12f };

        // --- Phase 3: dragon shouts (SPEC.md 4.6) ---

        /// <summary>Multipliers applied to a shout when the caster is a tagged dragon.
        /// Same assets, bigger numbers - never duplicate defs.</summary>
        public float dragonShoutRadiusFactor = 2.2f;
        public float dragonShoutRangeFactor = 2.0f;
        public float dragonShoutIntensityFactor = 2.5f;

        // --- Phase 5: draugr shout ladder (SPEC.md 4.5) ---
        // Rolled ONCE at pawn generation and stored. Never re-rolled on load.
        // Pool is Unrelenting Force + Frost Breath only, so 2 is the hard ceiling.

        /// <summary>Chance a Draugr Wight knows one shout. Common draugr are never rolled.</summary>
        public float draugrWightOneShoutChance = 0.20f;

        /// <summary>Chance a Draugr Overlord knows one shout, and separately, both.</summary>
        public float draugrOverlordOneShoutChance = 0.90f;
        public float draugrOverlordTwoShoutChance = 0.15f;

        /// <summary>Chance a Draugr Deathlord knows one shout, and separately, both.</summary>
        public float draugrDeathlordOneShoutChance = 1.00f;
        public float draugrDeathlordTwoShoutChance = 0.50f;

        /// <summary>Hard ceiling on shouts known by any non-Dovahkiin undead. Never raise above 2.</summary>
        public int undeadMaxShoutsKnown = 2;

        // --- Phase 3: souls (SPEC.md 5.1, 5.3) ---

        /// <summary>How close the Dovahkiin must be to a dying dragon to absorb it. SPEC.md 5.1.</summary>
        public float soulAbsorptionRadius = 40f;

        /// <summary>Akatosh's Child, damage dealt to dragons. SPEC.md 5.3.</summary>
        public float akatoshDamageBase = 0.05f;
        public float akatoshDamagePerSoul = 0.03f;
        public float akatoshDamageCap = 0.40f;

        /// <summary>Akatosh's Child, damage taken from dragons. SPEC.md 5.3.</summary>
        public float akatoshMitigationBase = 0.05f;
        public float akatoshMitigationPerSoul = 0.02f;
        public float akatoshMitigationCap = 0.30f;

        // --- Dragon Aspect: the Ancient Dragonborn summon ---
        //
        // NOT IN SPEC.md - specified by the user directly. He is a RESCUE, not a guarantee:
        // casting the three-word shout at full health summons nobody. He arrives when the
        // Dovahkiin is already hurt at cast time, or is downed while the shout is running.

        /// <summary>
        /// Health fraction at or below which a level-3 cast summons him. 1.0 would make him
        /// unconditional; 0 would make him unreachable except by being downed.
        /// </summary>
        public float ancientDragonbornSummonHealthThreshold = 0.65f;

        /// <summary>
        /// How long he lasts. 2500 ticks is one in-game hour, so 15000 is SIX hours.
        ///
        /// Raised from 3750 (1.5 hours) at the user's instruction 2026-07-31. The save file
        /// settled that this was never a regression: his hediff read ageTicks 463 plus
        /// ticksRemaining 3287 = 3750 exactly, the value it had been built with all along.
        /// The notebook recorded 1.5 hours as settled; the user's new figure supersedes it.
        ///
        /// Now also exposed in DovahkiinTuningDef.xml, which it was not - NONE of his numbers
        /// were, so none of them could be retuned without a rebuild.
        /// </summary>
        public int ancientDragonbornLifetimeTicks = 15000;

        /// <summary>Ticks after arriving before he will use his breath at all.</summary>
        public int ancientDragonbornBreathFirstDelayTicks = 120;

        /// <summary>Ticks between breaths. Long on purpose - he is support, not a turret.</summary>
        public int ancientDragonbornBreathCooldownTicks = 420;

        /// <summary>Reach and width of his breath. Matched to the Dovahkiin's own level-1 breath.</summary>
        public float ancientDragonbornBreathRange = 9f;
        public float ancientDragonbornBreathCone = 46f;

        /// <summary>Total breath damage, split across this many body parts.</summary>
        public float ancientDragonbornBreathDamage = 18f;
        public int ancientDragonbornBreathInstances = 5;

        /// <summary>
        /// His Unrelenting Force. He knows three shouts, not one - Fire, Frost and Force - and
        /// cycles them, so all three appear within a single summoning.
        ///
        /// Scaled off the Dovahkiin's own level-2 fus ro (knockback 4, damage 7 over 3 parts,
        /// stun 180) and pulled slightly below it: he is support, not a second Dovahkiin. It
        /// reuses the breath's range and cone deliberately, so the one ally-safety check covers
        /// all three shouts rather than each needing its own.
        /// </summary>
        public float ancientDragonbornForceDamage = 7f;
        public int ancientDragonbornForceInstances = 3;
        public float ancientDragonbornForceKnockbackCells = 3f;
        public int ancientDragonbornForceStunTicks = 150;

        /// <summary>
        /// Armour penetration for his Unrelenting Force. 0.35, matching his own fire and frost.
        ///
        /// It was 0 until 2026-07-31 - the only one of his three shouts with none - and Blunt
        /// with no AP is FULLY reduced by blunt armour, so against anything plated his Force did
        /// nothing while his other two still landed. Soul Tear shipped with exactly this fault
        /// once and read as "completely broken"; see CHANGELOG.
        ///
        /// The Dovahkiin's own Unrelenting Force uses CompAbilityEffect_Shout's default of 0.75.
        /// He stays deliberately under her, as every other number of his does.
        /// </summary>
        public float ancientDragonbornForceArmorPenetration = 0.35f;

        /// <summary>
        /// How far he may drift from the Dovahkiin before walking back. He is a bodyguard,
        /// not a wanderer. Only nudges him when idle - never interrupts a fight.
        /// </summary>
        public float ancientDragonbornFollowRadius = 8f;

        /// <summary>
        /// How far he will go to join something the Dovahkiin has picked a fight with.
        ///
        /// Needed because a wild animal is NOT hostile. `GenHostility.HostileTo` is true only for
        /// faction hostility, a manhunter mental state, a predator hunting us, a prison break or a
        /// slave rebellion - verified by reading its IL. So a boar the Dovahkiin attacks is not an
        /// enemy to anyone, his own AI has no work types to hunt with, and he stood and watched.
        /// The user reported exactly that.
        ///
        /// Bounded on purpose: without a radius he would chase a hunt across the whole map and
        /// abandon the person he exists to protect. 24 cells is three times the follow leash.
        /// Set to 0 to turn the behaviour off entirely.
        /// </summary>
        public float ancientDragonbornAssistRadius = 24f;

        // --- Dragon Aspect (SPEC.md 4.4d) ---

        /// <summary>
        /// Melee damage multiplier while Dragon Aspect is up.
        ///
        /// This lives here rather than in the hediff because there is NO Core stat for
        /// pawn-side melee damage: MeleeDamageFactor ships in Biotech, and CLAUDE.md
        /// invariant 5 requires the mod to run without it. A Harmony postfix on
        /// Verb_MeleeAttackDamage.DamageInfosToApply scales each DamageInfo by this instead.
        ///
        /// Flat across all three levels, on purpose - the user specified heavier blows at
        /// word ONE only, with words two and three adding armour, resistances and the
        /// summon rather than more damage.
        /// </summary>
        public float dragonAspectMeleeDamageFactor = 1.25f;

        /// <summary>
        /// Shout cooldown multiplier at THREE words of Dragon Aspect. The shared shout
        /// cooldown is this mod's own number, not a vanilla stat, so it is applied directly
        /// rather than through statFactors. 0.65 = cooldowns run 35% shorter.
        /// </summary>
        public float dragonAspectShoutCooldownFactor = 0.65f;

        /// <summary>
        /// How long Call of Valor's hero stays, in ticks. 30000 = 12 in-game hours.
        ///
        /// The user's rule is "TWICE the Ancient Dragonborn", and the code honours the rule
        /// rather than this number: at 0 or below it falls back to
        /// <see cref="ancientDragonbornLifetimeTicks"/> x 2, so the relationship survives the
        /// next time his lifetime moves. It moved once already - 3750 to 15000 - at which point
        /// a literal "7500" written down elsewhere silently stopped meaning "double his".
        ///
        /// Set a positive value here to break the link deliberately.
        /// </summary>
        public int callOfValorLifetimeTicks = 30000;

        // A callOfValorLifetimeByLevel list briefly existed here and was REMOVED 2026-08-01.
        // The quest grants all three of his words at once, so he is only ever at three words and
        // a per-level ladder describes states that cannot happen. Left in, it would have invited
        // a future session to tune numbers no player could ever see. A knob nobody can turn is
        // not harmless - it is a lie about the design.

        /// <summary>
        /// How Call of Valor holds his greatsword, per facing, in degrees.
        ///
        /// **These are the NEGATIVE of the Ancient Dragonborn's axe angles**, and that is the
        /// user's own report rather than a derivation: after the first playtest they said the
        /// sword *"tilts in the wrong direction (the opposite being the right one)"* on east,
        /// south and north. Negating a hold angle mirrors the lean about vertical, which is
        /// exactly "the opposite".
        ///
        /// Exposed rather than hardcoded because a hold angle is the single thing in this mod
        /// that has needed the most retuning - the axe's took three playtest rounds - and only
        /// the game can settle it. Edit, restart, no rebuild.
        /// </summary>
        public float callOfValorSwordAngleSouthEast = -20f;
        public float callOfValorSwordAngleNorth = -70f;
        public float callOfValorSwordAngleWest = -70f;

        /// <summary>
        /// Draw his sword IN FRONT of him when he faces north, rather than behind.
        ///
        /// The user: *"when he faces north the sword is supposed to be in front of him not in
        /// his back"*. The axe is drawn behind on purpose - it is broad and covered his back -
        /// so this is per weapon, not a correction to the axe.
        /// </summary>
        /// <summary>
        /// Whether the BODY hides part of the blade, per facing. FALSE means the body is drawn
        /// over the sword.
        ///
        /// The user derived these from where the weapon actually hangs, which is the right way
        /// round and not something arithmetic could have supplied:
        ///
        ///   NORTH - back to us, blade held on his FRONT -> body in front of it -> FALSE
        ///   WEST  - we see his LEFT side, blade on his RIGHT -> body in front of it -> FALSE
        ///   EAST  - we see his RIGHT side, blade on his right -> fully visible -> in front
        ///   SOUTH - facing us, blade out to his side -> fully visible -> in front
        ///
        /// South and east are always drawn in front and have no switch, because there is no
        /// arrangement of a right-hand weapon that puts the body between it and the camera on
        /// those two facings.
        /// </summary>
        public bool callOfValorSwordInFrontFacingNorth = false;
        public bool callOfValorSwordInFrontFacingWest = false;

        // --- Call of Valor's portal (the cast effect) ---
        //
        // The orbit table itself is NOT here, deliberately - see Thing_ValorPortal. Those radii
        // are welded to the arc sprite's own baked radius, so changing one without re-running
        // GenerateValorPortal.ps1 moves an arc off its own orbit. A number that also needs a
        // generator re-run is not a number the user can retune without a rebuild, which is what
        // this file is for. The four genuinely tunable ones are below.

        /// <summary>
        /// How long the portal lives, in ticks. 90 = 1.5 seconds at 60 ticks/second.
        ///
        /// Short on purpose: this is a cast effect, not a standing feature, and it is the only
        /// thing in the mod with tickerType Normal that is not a travelling wave.
        /// </summary>
        public int valorPortalLifetimeTicks = 90;

        /// <summary>
        /// The portal's radius in CELLS at full open. A pawn draws 1.5 cells wide, so 1.10 is a
        /// gateway rather than a puddle - wide enough to read as something a man steps out of.
        /// </summary>
        public float valorPortalRadiusCells = 1.10f;

        /// <summary>
        /// The fraction of the portal's life at which the hero steps through.
        ///
        /// Two things read this and they must not drift apart: the core's flash spikes here, and
        /// the summon should arrive here. Ask Thing_ValorPortal.ArrivalTick rather than
        /// recomputing it at the call site.
        /// </summary>
        public float valorPortalArriveAtFraction = 0.60f;

        /// <summary>
        /// How much light the effect adds, over and above the sprites' own alpha.
        ///
        /// Below about 1.4 the faint parts of the arcs never clear the ground's own brown. Note
        /// this multiplies the COLOUR, not the alpha - with an additive shader those are not
        /// interchangeable, and pushing alpha instead fattens the arcs until the three orbits
        /// merge into one solid ring.
        /// </summary>
        public float valorPortalGlowGain = 1.42f;

        // --- Phase 3: dragon movement states (SPEC.md 6.5) ---

        /// <summary>Soar speed as a multiple of grounded. SPEC.md 6.5: +20%.</summary>
        public float dragonSoarSpeedFactor = 1.32f;

        /// <summary>
        /// Flight speed as a multiple of GROUNDED. The user gave it as "+50% of soar", i.e.
        /// 1.20 x 1.50 = 1.80. Stored against GROUNDED rather than against soar on purpose: a
        /// number written as a multiple of another number goes stale the moment that other
        /// number moves, and this project has already been bitten by exactly that (the Ancient
        /// Dragonborn's lifetime was specified as "double his" and silently went wrong).
        /// Provisional - the user asked to recalibrate in game.
        /// </summary>
        public float dragonFlightSpeedFactor = 3.60f;

        /// <summary>
        /// At or below this fraction of health the dragon is grounded. SPEC.md 6.5c.
        ///
        /// THIS IS NOT A LATCH AND MUST NOT BECOME ONE. It is re-evaluated normally; the
        /// "perma-grounded" behaviour emerges because RimWorld's natural healing is slow, so a
        /// dragon cannot recover mid-fight. One that escapes and heals over days SHOULD fly
        /// again - that is what makes him a recurring threat rather than permanently crippled.
        /// Dovah get NO custom regeneration of any kind; if one ever appears to, it is a bug.
        /// </summary>
        public float dragonGroundedHealthFraction = 0.50f;

        /// <summary>
        /// How often the movement state is reconsidered, in ticks. Not every tick - the decision
        /// reads health and distances, and CLAUDE.md forbids per-tick work that can live on an
        /// interval. 60 is once a second at normal speed: responsive enough that a change reads
        /// as a reaction, cheap enough to ignore.
        /// </summary>
        public int dragonStateIntervalTicks = 60;

        /// <summary>
        /// Beyond this many cells from its target a dragon crosses in FLIGHT; inside it, it
        /// settles to SOAR and fights. SPEC.md 6.5c.
        /// </summary>
        public float dragonFlightEngageDistance = 22f;

        /// <summary>
        /// Within this many cells of a melee target a dragon lands to bite. SPEC.md 6.5c.
        /// </summary>
        public float dragonLandToBiteDistance = 4f;

        // --- Phase 3: DISENGAGE. "A dovah does not jog after you." ---
        //
        // The user, 2026-08-05: "during combat in general, he still seems to chase around people
        // in grounded state... If his target/no target is not within 2-4 cells around him, he
        // directly goes back to soar (stays static for 1 realtime second and then goes back into
        // the flight circling phase)."
        //
        // This is a FACT rule, not a rhythm one, so it bypasses the dwell timer - the distinction
        // the changelog already records as the cause of three separate defects.

        /// <summary>
        /// A GROUNDED dragon whose target is further than this breaks off and takes to the air
        /// rather than walking after them. The user asked for "2-4 cells"; 4 matches
        /// dragonLandToBiteDistance so he does not land and immediately disengage.
        /// </summary>
        public float dragonDisengageDistance = 4f;

        /// <summary>
        /// How long he hangs motionless in SOAR after breaking off, before peeling away into
        /// flight. The user's beat: "stays static for 1 realtime second".
        /// </summary>
        public float dragonDisengagePauseSeconds = 1f;

        /// <summary>
        /// How far he ranges when CIRCLING - flight with nothing to chase. Without this flight
        /// has no destination, he hangs motionless, and the anti-hover rule drops him straight
        /// back to the ground: disengage, soar, flight, land, disengage, for ever.
        /// </summary>
        public int dragonCirclingRadius = 20;

        /// <summary>
        /// HOW FAR AHEAD ALONG HIS PATH THE FLIGHT SPRITE READS HIS HEADING, IN CELLS.
        ///
        /// The user, 2026-08-12: "goes north, north east, north, north, north east instead of
        /// north a good amount of time, then north east for a while longer".
        ///
        /// That is not a movement fault - he flies dead straight to his waypoint. The octant was
        /// read from `pather.nextCell`, which is ONE cell away, and RimWorld walks a shallow
        /// diagonal as a STAIRCASE of N and NE steps. The sprite was faithfully reporting each
        /// individual stair.
        ///
        /// Reading several cells ahead averages the staircase back into the heading a human sees.
        /// Larger = steadier but slower to show a real turn; smaller = twitchier. At 1 or below
        /// this restores the original per-step behaviour exactly.
        ///
        /// ⚠ DELIBERATELY A LOOK-AHEAD AND NOT A DWELL TIMER. "Hold the last facing for N ticks"
        /// would also hide the flicker, and this project has been bitten FIVE times by exactly
        /// that shape - a cooldown added to stop flicker outranks correctness and then lies about
        /// a real turn. This changes what is MEASURED, not how long an answer is honoured, so a
        /// genuine change of heading still shows on the tick it happens.
        /// </summary>
        public int dragonFacingLookaheadCells = 6;

        /// <summary>
        /// HOW BIG A DOVAH DRAWS, GROUNDED AND AIRBORNE. ABSOLUTE VALUES. NOT A DELTA.
        ///
        /// ⚠ DO NOT SCALE THESE, EVER, UNLESS THE USER ASKS IN SO MANY WORDS. Standing
        /// instruction, 2026-08-18: "it is supposed to be done once, i'll tell you again when
        /// I want to touch the dovah's visual size."
        ///
        /// ⚠ WHY: this used to read "Raised 10% at the user's request 2026-08-12", which
        /// states an OPERATION rather than a fact. A later session read it as a live
        /// instruction and applied the 10% a SECOND time (5.06 became 5.566, 6.16 became
        /// 6.776). A delta can be re-applied; an absolute cannot. Record the number that
        /// stands, never the change that produced it. History, reference only: 4.6 / 5.6 as
        /// authored, then 5.06 / 6.16 after the single +10% of 2026-08-12.
        ///
        /// ⚠ AND THE 5.566f / 6.776f BELOW IS SANCTIONED. Shown the arithmetic on 2026-08-18
        /// the user chose to KEEP the doubled size rather than revert to 5.06 / 6.16. Do not
        /// "correct" it back: the value is right, only the way it was arrived at was wrong.
        ///
        /// ⚠ THESE WERE C# CONSTS UNTIL NOW, WHICH BROKE TWO RULES AT ONCE. `CLAUDE.md`: "all
        /// tuning numbers go in Defs/DovahkiinTuningDef.xml so I can retune without a rebuild".
        /// And the grounded size existed in BOTH the const and the ThingDef's own `drawSize` -
        /// two numbers that must agree, held in two places, which this project has already been
        /// bitten by (the landing distance vs the landing stun radius).
        ///
        /// ⚠ `dragonDrawSizeGrounded` MUST STILL MATCH `<drawSize>` in
        /// `Defs/ThingDefs_Races/Alduin_Dovahkiin.xml`. The def's value is what RimWorld uses
        /// before our first graphic swap of the game; this one is what every swap after it uses.
        /// A mismatch shows as the dragon changing size the first time he takes off.
        ///
        /// Airborne is deliberately LARGER than grounded - he is nearer the camera in the air.
        /// </summary>
        public float dragonDrawSizeGrounded = 5.566f;

        /// <summary>Airborne draw size. See <see cref="dragonDrawSizeGrounded"/>.</summary>
        public float dragonDrawSizeAirborne = 6.776f;

        /// <summary>
        /// THE SHARPEST TURN HE WILL MAKE BETWEEN CIRCLING LEGS, IN DEGREES.
        ///
        /// The user, 2026-08-12: *"prevent him from making aggressive turns like North -&gt;
        /// north-east -&gt; south"*.
        ///
        /// The circling angle always advances the same way round, so the *waypoints* progress
        /// smoothly - but the HEADING to the next one depends on where he happens to be when it is
        /// chosen, and near the middle of a circuit that can be most of a reversal. Candidates
        /// whose heading differs from his current one by more than this are skipped in favour of
        /// one further round the ring.
        ///
        /// 180 disables the rule. Below about 60 he can run out of candidates on a cramped map and
        /// fall back to the old behaviour, which is a graceful degradation rather than a stall.
        /// </summary>
        public float dragonMaxTurnDegrees = 110f;

        /// <summary>
        /// LENGTH OF `Sounds/DragonTakeOff.wav`, IN TICKS. **Measured off the file, not estimated:
        /// 2.237s at 48kHz stereo = 134.2 ticks, rounded UP to 135 so the clip is never clipped.**
        ///
        /// Used for two things at once, which is why it is a number and not a constant: it delays
        /// the circling clip so the two play in sequence rather than over each other, and it is part
        /// of the circling leg's length.
        ///
        /// ⚠ CHANGE THIS IF THE CLIP IS EVER REPLACED, or the circling audio starts over the tail of
        /// the take-off. 60 ticks = 1 second.
        /// </summary>
        public int dragonTakeOffSoundTicks = 135;

        /// <summary>
        /// LENGTH OF THE LONGER CIRCLING CLIP, IN TICKS. `DragonFlightCircling1.mp3` and
        /// `DragonFlightCircling2.mp3` are **both 15.192s** (633 MPEG frames each), = 911.5 ticks,
        /// rounded UP to 912.
        ///
        /// The user's rule, 2026-08-13: tune the circling phase to *the longest of the two* if the
        /// dragon can alternate between them - he can, so both are used and this is the longer one.
        /// They happen to be identical in length, so "longest" is unambiguous here.
        ///
        /// ⚠ THE CIRCLING PHASE IS `Max(dragonPatternLeaveTicks, dragonTakeOffSoundTicks + this)`.
        /// It is DERIVED so the audio can never be cut off by a phase length somebody forgot to
        /// update - two numbers that must agree, held in two places, is this project's most
        /// frequently repeated failure. Raising `dragonPatternLeaveTicks` still lengthens the leg;
        /// lowering it below the audio does nothing, on purpose.
        /// </summary>
        public int dragonCirclingSoundTicks = 912;

        /// <summary>
        /// DEGREES OF THE CIRCUIT COVERED BY ONE LEG. 360 / this = the number of sides.
        ///
        /// ⚠ LEG LENGTH IS `2 * dragonCirclingRadius * sin(step/2)` - **BOTH numbers decide how
        /// long a straight run is**, and it is easy to turn only one and wonder why little changed.
        ///
        /// | step | radius | sides | leg |
        /// |---|---|---|---|
        /// | 45 | 14 | 8 | 10.7 cells - the first attempt, too busy |
        /// | **72** | **20** | **5** | **23.5 cells - current** |
        /// | 90 | 20 | 4 | 28.3 cells (⚠ needs dragonMaxTurnDegrees above 90) |
        /// | 120 | 22 | 3 | 38.1 cells |
        ///
        /// The user drew the shape they wanted on 2026-08-13, and then asked for it straighter
        /// still: *"make the trajectory of flight less octogonal for more 'rectiline' flighttime"*.
        /// 45/14 became 72/20, more than doubling the straight run.
        ///
        /// ⚠ THIS REPLACED `Rand.Range(55f, 95f)`, AND THE CONSTANCY IS THE POINT. Legs of unequal
        /// length, with the rotation direction also unrolled per leg, cannot close into a loop -
        /// they wander. The direction is now rolled ONCE per circuit and held.
        ///
        /// ⚠ RAISING THIS TO 90 OR MORE MEANS RAISING <see cref="dragonMaxTurnDegrees"/> TOO.
        /// That filter rejects a waypoint demanding a sharper turn than it allows; a 90 step
        /// against a 90 limit sits exactly on the boundary, falls through to the unlimited second
        /// pass, and the shape goes ragged again with nothing obvious to point at.
        /// </summary>
        public float dragonCirclingStepDegrees = 90f;

        /// <summary>
        /// HOW FAR HE WILL LOOK FOR A NEW VICTIM WHEN THE ONE HE WAS BRAWLING STOPS COUNTING.
        ///
        /// `IsEngageable` rejects DOWNED pawns as well as dead ones, so a brawl loses its target the
        /// moment he knocks somebody down rather than killing them. Before 2026-08-13 he then stood
        /// over the body for the rest of the phase - *"caught him frozen, not attacking on some of
        /// his pattern 1 session"*, intermittent precisely because it depended on whether the victim
        /// died or was downed.
        ///
        /// Bounded rather than map-wide on purpose: a brawl is a local event, and an unbounded scan
        /// would send a grounded dragon jogging across the map at 1.00x speed, which is the "he
        /// chases people on foot" failure this project already fixed once.
        ///
        /// 0 restores the old stand-still behaviour.
        /// </summary>
        public float dragonBrawlRetargetRadius = 12f;

        /// <summary>
        /// THE PAUSE BEFORE A BREATH FIRES, IN TICKS. 60 ticks = 1 second, so 240 is the user's
        /// four seconds (2026-08-13):
        ///
        ///   *"flight circling -&gt; Land -&gt; wait 4s -&gt; Launch breathing -&gt; Xs of pause,
        ///   the same amount actually set and used currently -&gt; flight circling."*
        ///
        /// He enters the breathing state (grounded for a perch, soar for a hover) at the START of
        /// the wind-up and is stunned motionless throughout, so it reads as a dragon settling and
        /// drawing breath rather than as a hesitation.
        ///
        /// ⚠ THIS IS COUNTED INSIDE `DragonAttackPatterns.ExecuteTicksOf`, so raising it lengthens
        /// the Execute phase and the hold-still stun together. Do not also add it at a call site.
        /// </summary>
        public int dragonBreathWindupTicks = 240;

        /// <summary>
        /// DOES AN AIRBORNE DOVAH FLY OVER WALLS, ROOFS AND GATES? SPEC.md 6.5, built 2026-08-13
        /// after the user found one trapped inside a castle's curtain wall, waiting for a colonist
        /// to open the gate.
        ///
        /// **A master switch on four Harmony patches** (reachability, the path itself, the blocker
        /// check, and the terrain cost) - see `Patch_DragonFlyOver.cs`. Set false and every one of
        /// them stands down, restoring ordinary pathing, without a rebuild.
        ///
        /// ⚠ It exists because this is the largest behavioural change in the dragon work and it
        /// touches THREE hot engine paths. If a pathing oddity ever appears anywhere in a save with
        /// this mod, **turn this off first** - it is the fastest way to prove whether the dragon's
        /// flight is responsible before reading any code.
        ///
        /// **Natural mountain rock still stops him regardless of this setting** - that exception is
        /// what makes a mountain base the player's architectural answer to a dragon.
        /// </summary>
        public bool dragonFliesOverObstacles = true;

        /// <summary>
        /// A DOVAH NEVER LANDS OR WALKS UNDER A ROOF. HE FLIES OVER IT.
        ///
        /// The user, 2026-08-18, after finding him grounded inside a roofed building: *"I hereby
        /// declare that dovah's cannot land nor walk inside roofed area, only fly over it."* It
        /// was reported as unfair as well as untidy, and it is both - a 5.6-cell dragon standing
        /// in somebody's bedroom reads as the engine losing track of him.
        ///
        /// WHAT THIS GATES: choosing to be on the ground. It stops the dive pounce picking a
        /// roofed cell, stops "standing in melee range" grounding him when the only ground is
        /// roofed, keeps grounded attack patterns out of the roll when his target is indoors, and
        /// lifts him back into a soar if he is ever found standing under one.
        ///
        /// WHAT IT DOES *NOT* GATE, ON PURPOSE: being FORCED down. Downed, at or below
        /// dragonGroundedHealthFraction, or holding somebody in his jaws all still ground him
        /// wherever he is. That precedence is deliberate and it is what stops the two rules
        /// oscillating - the health rule is what converts an untouchable ranged duel into a
        /// winnable melee kill, and a roof must not be able to cancel it. Force wins; choice does
        /// not.
        /// </summary>
        public bool dragonNeverGroundedUnderRoof = true;

        /// <summary>
        /// THE SHORTEST CHORD A FLIGHT HEADING MAY BE READ FROM, IN CELLS. Below this the sprite
        /// keeps whatever facing it already had.
        ///
        /// The user, 2026-08-18: *"He oddly flies a lot in diagonal sprite (while going
        /// east/west) when flying over roofed areas."*
        ///
        /// The octant dead zone is `(chord * 5) / 12` in INTEGER arithmetic, so it is 0 at a
        /// chord of 1 or 2 cells, 1 at 3 to 4, and 2 at 5 to 7. With no dead zone a single
        /// Bresenham staircase step on a due-east run reads as NE - which is precisely the defect
        /// `dragonFacingLookaheadCells` was raised to 6 to cure on 2026-08-12. It survived in the
        /// TAIL of every leg, where `Min(want, NodesLeftCount - 1)` cannot reach six cells.
        ///
        /// 3 is therefore the lowest honest value: the first chord length whose dead zone is not
        /// zero. Raising it makes the facing steadier and slower to show a real turn; 1 restores
        /// the old behaviour, diagonals included.
        /// </summary>
        public int dragonFacingMinChordCells = 3;

        /// <summary>
        /// THE DIVE. How far he may cover in the pounce that ends a chase - he drops onto a cell
        /// beside his target rather than touching down wherever he happened to be.
        ///
        /// The user, 2026-08-05: "his stun-landing is 95% of the time outran by his fleeing
        /// target." Two numbers made that certain rather than likely. He LANDS at
        /// dragonLandToBiteDistance (4) but the landing stun only reaches
        /// dragonLandingStunRadius (2.4), so a target at the trigger distance was outside the
        /// blast BY CONSTRUCTION; and touching down swaps his 3.60x flight speed for 1.00x,
        /// which is roughly a fleeing colonist's own speed, so he could never close the gap
        /// afterwards either.
        ///
        /// Set to 0 to disable the pounce and land where he stands.
        /// </summary>
        public float dragonDiveMaxCells = 6f;

        // --- Phase 3: THE BREATH (SPEC.md 4.6, 4.6a) ---
        //
        // "+100% damage but delivered GRADUALLY over time of exposure", a slightly narrower cone
        // and roughly twice the range. Compare the Dovahkiin's own Fire Breath at level 3: 74
        // damage, range 13, cone 46 - so a dragon's is about double the damage over double the
        // range, but spread across the whole burn rather than landing at once.
        //
        // ⚠ THE BREATH IS NOT A TOOL OR A VERB, and must never become one - RimWorld's combat AI
        // would then fire it on its own schedule. The attack-pattern executor is the only caller.

        /// <summary>
        /// Total damage a full-duration breath deals, spread over every pulse.
        /// Reduced 148 -> 118.4 (-20%) on 2026-08-06 at the user's request: a Dovahkiin under
        /// Dragon Aspect was still "quiet frail" against it. Applies to fire and frost alike.
        /// </summary>
        public float dragonBreathDamage = 90.576f;

        /// <summary>Hits per pulse per victim, so a burn lands across several body parts.</summary>
        public int dragonBreathDamageInstances = 4;

        /// <summary>
        /// Without this a plated raider shrugs the breath off - the Soul Tear lesson.
        ///
        /// 0.35 -> 0.315 (-10%) on 2026-08-06, so Dragon Aspect's heat resistance keeps more of
        /// its value. ⚠ SHARED BY FIRE AND FROST, like every other breath number - the user asked
        /// about "the dragon's fire breath", and this was not split, because fire and frost differ
        /// only in damage def, hediff and palette by design. Split it if they ever need to differ.
        /// </summary>
        public float dragonBreathArmorPenetration = 0.25515f;

        /// <summary>How long the breath burns. 180 ticks is three real seconds.</summary>
        public int dragonBreathDurationTicks = 263;

        /// <summary>
        /// Gap between damage pulses. Duration / interval gives the pulse count, and the per-pulse
        /// share is the total divided by that - DERIVED, so the two can never disagree.
        /// Shorter interval = smoother burn, same total.
        /// </summary>
        public int dragonBreathPulseIntervalTicks = 20;

        /// <summary>
        /// ⚠ TESTING SWITCH, 2026-08-05. TRUE is the real behaviour and this MUST go back to
        /// true before the dragon ships.
        ///
        /// False makes a GUARDING dovah never rouse: he still sits motionless on his mound, but
        /// walking up to him no longer turns him manhunter. Added at the user's request so the
        /// breath can be tested by loading, clicking him, firing a debug breath and quitting -
        /// instead of fighting him with unarmoured colonists every single round.
        ///
        /// It does NOT make him invulnerable or passive in general: manhunterOnDamageChance is
        /// still 1.0 on his ThingDef, so hitting him will still start a fight.
        /// </summary>
        public bool dragonGuardTriggerEnabled = true;

        /// <summary>GROUNDED cone: reach in cells. About twice the Dovahkiin's 13.</summary>
        public float dragonBreathConeRange = 24f;

        /// <summary>GROUNDED cone: width in degrees. Narrower than her 46 on purpose.</summary>
        public float dragonBreathConeAngle = 38f;

        /// <summary>
        /// GROUNDED cone: the SHORTEST it may be. The cone reaches its target and stops, rather
        /// than always spanning dragonBreathConeRange.
        ///
        /// The user, 2026-08-05: "the grounded breath is always spanning a fixed range, it's
        /// always the same cone." It was: the direction followed the aim but the LENGTH never
        /// did, so breathing at something three cells away still burned everything twenty cells
        /// past it. He could breathe in a direction, never AT anything.
        ///
        /// ⚠ SET THIS EQUAL TO dragonBreathConeRange to go back to a fixed-length jet.
        /// </summary>
        public float dragonBreathConeMinRange = 24f;

        /// <summary>
        /// SOAR: radius of the damaging circle at the impact point. The reaching cone drawn from
        /// the dragon down to it is COSMETIC and damages nothing - SPEC.md 4.6a.
        /// </summary>
        public float dragonBreathPoolRadius = 3.5f;

        /// <summary>
        /// SOAR: how far from himself he may place that circle.
        ///
        /// Added 2026-08-05 - the user: "He shouldn't be able to target such a faraway distance."
        /// There was NO cap at all: Pool's reach is the distance from him to the aimed cell, and
        /// nothing bounded it, so he could hit anywhere on the map. Aiming further than this now
        /// clamps to it rather than being refused.
        /// </summary>
        public float dragonBreathSoarRange = 14f;

        // --- THE FILL GRADIENT. All four are live-tunable; edit the XML and restart. ---
        //
        // The wash under the flecks runs two ramps at once, both measured from the MOUTH:
        //   LIGHTNESS - pale at the mouth, base colour at the far end, everything settling to
        //               fillColor x 0.8 as the breath burns out. That 0.8 floor is the user's
        //               stated limit: "at most a 20% darker of the color used rightnow".
        //   YELLOWNESS - strongest at the mouth, gone by the far end. The user, 2026-08-06:
        //               "Grounded: yellower at the begining of the cone gradienting down to
        //               unchanged color at the tip." A soaring breath yellows only its reaching
        //               CONE and leaves the impact circle alone - "untouched yellowness for the
        //               circle" - so the circle is excluded outright rather than merely landing
        //               at the pale end of the ramp.

        /// <summary>FIRE: the wash's base colour - reached at the far end of the shape, at t=0.</summary>
        public Color dragonBreathFillColor = new Color(0.72f, 0.26f, 0.05f, 0.34f);

        /// <summary>FIRE: the pale end, at the mouth. Deliberately lighter than the fleck tint.</summary>
        public Color dragonBreathFillBright = new Color(1f, 0.76f, 0.40f);

        /// <summary>FIRE: the hue the near end is biased toward. Raise green for a hotter yellow.</summary>
        public Color dragonBreathFillYellow = new Color(1f, 0.98f, 0.28f);

        // --- FROST. The same three slots, in cold. The near-end bias is WHITE rather than
        // yellow: the hottest part of a flame goes yellow, the harshest part of a frost jet goes
        // white, and both are "the near end is more extreme than the far end". Same machinery,
        // different palette - which is why the breath class takes colours rather than an element.

        // ⚠ FROST NEEDS A PALETTE PER SHAPE, AND THE TWO ARE INVERTED. Fire does not - its cone
        // and its soaring jet share one set. Frost, per the user 2026-08-06:
        //
        //   GROUNDED - "blue at the start down to snow white at the tip of the cone"
        //              => near = BLUE, far = WHITE
        //   SOAR     - "make the cone whiter, dont touch the circle"
        //              => near (the reaching cone) = WHITE, far (the circle) = BLUE, unchanged
        //
        // Those are opposite arrangements, so one shared frost palette cannot express both. They
        // are not a mistake to be reconciled: a ground jet reads as cold breath freezing white as
        // it travels, while a falling jet reads as white air condensing into a blue pool where it
        // lands. Keep them separate.

        /// <summary>FROST GROUNDED: the far end, at the tip of the cone - snow white.</summary>
        public Color dragonBreathFrostConeFillColor = new Color(0.93f, 0.97f, 1f, 0.34f);

        /// <summary>
        /// FROST GROUNDED: the mouth end of the lightness ramp - blue.
        /// Softened 2026-08-06 ("reduce the blue overall a bit"): (0.30,0.58,0.95) -> this.
        /// Lifted 25% toward WHITE rather than having its blue channel lowered - dropping B alone
        /// makes a colour darker and greyer, not less blue. Blueness is dominance, so the way to
        /// reduce it is to bring red and green UP toward blue.
        /// </summary>
        public Color dragonBreathFrostConeFillBright = new Color(0.545f, 0.729f, 0.965f);

        /// <summary>FROST GROUNDED: the hue the mouth is biased toward. Softened, as above.</summary>
        public Color dragonBreathFrostConeFillTint = new Color(0.449f, 0.641f, 0.948f);

        /// <summary>FROST GROUNDED: how strongly the mouth is pushed toward that blue.</summary>
        public float dragonBreathFrostConeTintStrength = 0.75f;

        /// <summary>
        /// FROST SOAR: the far end - the impact circle.
        /// Softened 2026-08-06 ("reduce the blue overall a bit for BOTH soar and grounded"):
        /// (0.16,0.38,0.70) -> this. Note this DOES change the circle, which an earlier
        /// instruction had asked be left alone - but the circle is where soar's blue lives, so
        /// "reduce the blue for soar" cannot mean anything else.
        /// </summary>
        public Color dragonBreathFrostSoarFillColor = new Color(0.449f, 0.598f, 0.808f, 0.34f);

        /// <summary>FROST SOAR: the mouth end of the lightness ramp - pale ice.</summary>
        public Color dragonBreathFrostSoarFillBright = new Color(0.80f, 0.94f, 1f);

        /// <summary>FROST SOAR: the hue the reaching cone is biased toward - near-pure white.</summary>
        public Color dragonBreathFrostSoarFillTint = new Color(0.97f, 1f, 1f);

        /// <summary>
        /// FROST SOAR: how white the reaching cone goes. Higher than the others because the user
        /// asked for that cone specifically to be whiter, and the tint is the only lever that
        /// does not touch the circle.
        /// </summary>
        public float dragonBreathFrostSoarTintStrength = 0.95f;

        /// <summary>FROST: snow laid in the cells it covers, as the Dovahkiin's own frost does.</summary>
        public float dragonBreathFrostSnowDepth = 0.22f;

        /// <summary>FROST: severity of Dovahkiin_Chilled applied to anything caught in it.</summary>
        public float dragonBreathFrostChillSeverity = 1f;

        /// <summary>
        /// SOAR ONLY: how much LESS TRANSPARENT the damaging circle is than the reaching cone.
        /// A multiplier on the circle's alpha; 1 = the same as everything else.
        ///
        /// ⚠ THE GOAL IS THAT THE CIRCLE STANDS OUT FROM THE CONE, and the LEVER IS OPACITY.
        /// A brightness multiplier on RGB was tried first, 2026-08-06, and REJECTED IN PLAY:
        /// "now it feels like the cone and circle has the same color… let's tweak the opacity
        /// channel instead."
        ///
        /// The reason it failed is measurable rather than a matter of taste. RED WAS ALREADY
        /// SATURATED at 1.0 across every circle band, so multiplying could only raise green and
        /// blue - which walks the circle's hue TOWARD the cone's bright yellow-orange instead of
        /// away from it. It made them converge, which is the opposite of what was wanted.
        ///
        /// Opacity has no such ceiling here (the fill sits at 0.34) and changes weight without
        /// touching hue at all.
        /// </summary>
        public float dragonBreathSoarCircleOpacity = 1.4f;

        /// <summary>
        /// How far toward that yellow the mouth goes, 0 to 1. 0 disables the yellow ramp
        /// entirely and restores the plain lightness gradient.
        ///
        /// ⚠ KEEP THIS IN STEP WITH THE XML. These two are ONE number written twice, and the
        /// notebook records a live example of what happens when they drift: the Ancient
        /// Dragonborn's lifetime read 3750 in C# and 15000 in the def, and because
        /// GetNamedSilentFail returns null without a message, the disagreement could only ever
        /// have surfaced in the one situation where nobody could see it.
        /// </summary>
        public float dragonBreathYellowStrength = 0.75f;

        // --- Phase 3: ATTACK PATTERNS (SPEC.md 6.5c-4) ---
        //
        // ⚠ THESE REPLACE THE GLOBAL STATE DWELL TIMES BELOW FOR ANYTHING IN COMBAT.
        // The user, 2026-08-06: "the timers set shouldn't be set as a single general constant
        // 'X secondes in this state', but rather 4 set of timers with each set for a specific
        // pattern." A pattern owns the state while it runs; the dwell rhythm no longer competes
        // with it. See DragonAttackPattern.cs for why those two could never coexist.

        /// <summary>DIVE AND BRAWL: how long he stays on the ground fighting. 60 ticks = 1s.</summary>
        public int dragonPatternBrawlTicks = 600;

        /// <summary>
        /// Both breath patterns: how long he holds position AFTER the breath burns out, before
        /// peeling away. Without it he leaves on the breath's last tick and it reads as clipped.
        /// </summary>
        public int dragonPatternBreathTailTicks = 240;

        /// <summary>
        /// How long he circles in flight between attacks. This is the "leave, come back around"
        /// beat that makes flight the connective tissue rather than a fourth attack.
        /// </summary>
        public int dragonPatternLeaveTicks = 900;

        /// <summary>
        /// Safety valve: if he cannot reach engage distance within this, the pattern is abandoned
        /// and a new one rolled. Without it an unreachable target - inside a mountain base, across
        /// broken ground - leaves him approaching for ever, which is exactly the hovering the
        /// HOVER-DIAG hunt chased for four rounds.
        /// </summary>
        public int dragonPatternApproachTimeoutTicks = 1500;

        // --- Phase 3: state DWELL TIMES and the combat rhythm (SPEC.md 6.5c) ---
        //
        // The user, after the first playtest: he "barely switches to soar at all and is using
        // flight a bit too often", and there should be "a system of cooldown between each
        // switch". The cooldown is the fix for BOTH complaints - without a minimum dwell the
        // state flickered every time he started or stopped wandering, because idle-and-moving
        // is flight and idle-and-stationary is grounded.
        //
        // In REAL-TIME SECONDS, as the user specified them. Converted at 60 ticks/second.

        /// <summary>
        /// Seconds he must stay grounded before he may take off again. Raised from 6 to 9
        /// (+50%) at the user's request after the second playtest.
        /// </summary>
        public float dragonMinSecondsGrounded = 9f;

        /// <summary>Seconds he must stay soaring before he may change again.</summary>
        public float dragonMinSecondsSoar = 5f;

        /// <summary>
        /// Seconds he must stay in flight before he may drop back to soar. DOUBLED from 4 to 8
        /// at the user's request after the second playtest.
        ///
        /// It is no longer the shortest of the three - grounded is - so flight is now kept the
        /// rarest state by "in combat, flight always returns to soar" rather than by a short
        /// dwell. If flight starts feeling too common again, that is the reason.
        /// </summary>
        public float dragonMinSecondsFlight = 8f;

        /// <summary>
        /// How long he may hover motionless before the dwell timer is overridden and he lands.
        ///
        /// Reported by the user: "he was still capable of being stationary while in flight."
        /// The cause was the dwell timer itself - having entered flight while moving, he was
        /// locked there even after stopping. This is the escape hatch.
        ///
        /// A GRACE period rather than zero, because a wandering pawn pauses between
        /// destinations, and dropping him out of flight on every pause would reintroduce the
        /// very flicker the dwell timer was added to cure.
        /// </summary>
        public float dragonFlightStationaryGraceSeconds = 1.5f;

        // The combat rhythm. The user's target is SOAR >= GROUNDED > FLIGHT by time spent,
        // with flight used "from time to time" to break off and return.
        //
        // These are per-decision chances, rolled only once a state's dwell time has expired.
        // They are NOT time shares - the dwell times above weight the result as much as these
        // do, which is why both are exposed. EXPECT TO TUNE THESE IN GAME; the interaction of
        // three dwell times and three chances is not something to compute on paper.

        /// <summary>Chance a grounded dragon in a fight takes off to soar.</summary>
        public float dragonCombatGroundedToSoarChance = 0.55f;

        /// <summary>
        /// Chance a soaring dragon breaks off into flight. The user's "higher chance" of the
        /// two exits from soar.
        /// </summary>
        public float dragonCombatSoarToFlightChance = 0.30f;

        /// <summary>
        /// Chance a soaring dragon lands instead. The user's "lower chance" - but note it is
        /// numerically close, because grounded and soar are meant to be roughly equal in TIME
        /// and grounded has the longer dwell.
        /// </summary>
        public float dragonCombatSoarToGroundedChance = 0.34f;

        // --- Phase 3: the landing (SPEC.md 6.5e) ---
        // The user, 2026-08-04: "dovahs going from soar to grounded causes a little dust flying
        // around them and causes a brief stun." Fires ONLY on soar -> grounded: that is the one
        // transition that is a landing. Flight never drops straight to grounded (it always
        // exits via soar), and leaving grounded is a take-off, not an impact.

        /// <summary>Dust puffs thrown when he touches down.</summary>
        public int dragonLandingDustPuffs = 30;

        /// <summary>
        /// Cells around the landing point that are staggered. Small on purpose - this is the
        /// thump of a heavy animal touching down, not a shout.
        /// </summary>
        public float dragonLandingStunRadius = 4.4f;

        /// <summary>
        /// Stun length in ticks. 60 = one second at normal speed. "Brief", per the user: long
        /// enough to read as a stagger, short enough that repeated landings cannot stunlock.
        /// </summary>
        public int dragonLandingStunTicks = 120;

        /// <summary>
        /// How close a colonist must come before a GUARDING dovah wakes and turns manhunter.
        /// SPEC.md 6.5c. Generous, because a mound guardian that only notices you at arm's
        /// length is not guarding anything.
        /// </summary>
        public float dragonGuardTriggerRadius = 12f;

        // --- Phase 3: THE GRAB (SPEC.md 6.6b) ---
        // "Bite down, shake left-right, throw away." The user's iconic move, 2026-08-04, and
        // deliberately RARE - a surprise, not a rotation staple.

        /// <summary>
        /// Chance a landed MAW hit becomes the grab. "Very very small" per the user: at 0.02
        /// a player might see it once in a long fight, which is what makes it memorable.
        /// </summary>
        public float dovahGrabChance = 0.02f;

        /// <summary>How long he worries the victim, in ticks. 60 = one real second.</summary>
        public int dovahGrabDurationTicks = 150;

        /// <summary>Ticks between facing flips during the shake. Lower is more violent.</summary>
        public int dovahGrabShakeIntervalTicks = 12;

        /// <summary>Damage on release - both types, per the user.</summary>
        public float dovahGrabSlashDamage = 22f;
        public float dovahGrabPierceDamage = 18f;

        /// <summary>How far the victim is flung when he lets go.</summary>
        public float dovahGrabThrowCells = 7f;

        private static DovahkiinTuningDef cached;

        /// <summary>
        /// The one tuning def. Cached, because this is read from hot paths and
        /// DefDatabase lookups by name are not free (CLAUDE.md: RocketMan is installed).
        /// </summary>
        public static DovahkiinTuningDef Current
        {
            get
            {
                if (cached == null)
                {
                    cached = DefDatabase<DovahkiinTuningDef>.GetNamedSilentFail("Dovahkiin_Tuning");
                }
                return cached;
            }
        }
    }
}



