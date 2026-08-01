// Implements: SPEC.md (tuning), CLAUDE.md "Anti-patterns" - all tuning numbers live in one def,
// never inline, so balance can be changed without a rebuild.
using System.Collections.Generic;
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
        public float callOfValorSwordAngleNorth = 62f;
        public float callOfValorSwordAngleWest = 10f;

        /// <summary>
        /// Draw his sword IN FRONT of him when he faces north, rather than behind.
        ///
        /// The user: *"when he faces north the sword is supposed to be in front of him not in
        /// his back"*. The axe is drawn behind on purpose - it is broad and covered his back -
        /// so this is per weapon, not a correction to the axe.
        /// </summary>
        public bool callOfValorSwordInFrontFacingNorth = true;

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
