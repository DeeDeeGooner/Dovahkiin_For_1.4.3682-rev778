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
