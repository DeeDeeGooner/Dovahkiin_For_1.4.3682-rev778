// Implements: SPEC.md 3.1, 10 - the defs the registry and identity systems need by name.
using RimWorld;
using Verse;

namespace Dovahkiin
{
    [DefOf]
    public static class DovahkiinDefOf
    {
        public static TraitDef Dovahkiin_Trait_Dovahkiin;
        public static TraitDef Dovahkiin_Trait_Dragonblood;

        public static HediffDef Dovahkiin_DragonSoulAttunement;
        public static HediffDef Dovahkiin_TheVoice;
        public static HediffDef Dovahkiin_VoiceStrain;

        /// <summary>Slow Time's self-haste. Read on the attack-cooldown path. SPEC.md 4.4a.</summary>
        public static HediffDef Dovahkiin_SlowTime;

        /// <summary>Become Ethereal. Read on the attack-start path. SPEC.md 4.4a.</summary>
        public static HediffDef Dovahkiin_Ethereal;

        /// <summary>Drain Vitality and Dismay - promoted from SPEC.md 4.4c at the user's request.</summary>
        public static HediffDef Dovahkiin_VitalityDrained;
        public static HediffDef Dovahkiin_Dismayed;

        /// <summary>
        /// Soul Tear's dead puppet. SPEC.md 4.4f. Read by the registry's load-time safety
        /// sweep, so a null here would silently disable that guard - hence the startup check.
        /// </summary>
        public static HediffDef Dovahkiin_DeadPuppet;

        /// <summary>The crimson mark on a puppet, so it is never mistaken for a real ally.</summary>
        public static FleckDef Dovahkiin_Fleck_PuppetGlow;

        /// <summary>Soul Tear's travelling bolt.</summary>
        public static FleckDef Dovahkiin_Fleck_SoulTearWave;

        /// <summary>Slow Time's slow on everyone who is not the caster. SPEC.md 4.4a.</summary>
        public static HediffDef Dovahkiin_TimeSlowed;

        public static NeedDef Dovahkiin_Need_Thuum;

        /// <summary>The PawnFlyer used to fling targets. SPEC.md 4.4a.</summary>
        public static ThingDef Dovahkiin_ShoutFlyer;

        /// <summary>Carrier for the travelling shout cone. SPEC.md 4.3.</summary>
        public static ThingDef Dovahkiin_ShoutWave;

        /// <summary>The Storm Call storm. Ticks only while the shout is active. SPEC.md 4.4e.</summary>
        public static ThingDef Dovahkiin_StormCall;

        /// <summary>Tintable cone particles. Vanilla's are renderInstanced and ignore colour.</summary>
        public static FleckDef Dovahkiin_Fleck_ForceWave;
        public static FleckDef Dovahkiin_Fleck_FireWave;

        public static ThoughtDef Dovahkiin_Thought_WitnessedSoulAbsorption;
        public static ThoughtDef Dovahkiin_Thought_WitnessedShout;

        /// <summary>Surviving Soul Tear. SPEC.md 4.4f.</summary>
        public static ThoughtDef Dovahkiin_Thought_SoulTorn;

        static DovahkiinDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DovahkiinDefOf));
        }
    }

    /// <summary>
    /// Vanilla defs this mod uses that Ludeon did not put in a DefOf class. Field names must
    /// match the vanilla defName exactly - that is how DefOf resolution works.
    /// All of these are Core, so none of it is DLC-gated.
    /// </summary>
    [DefOf]
    public static class DovahkiinVanillaDefOf
    {
        /// <summary>Fire whoosh, used by Fire Breath.</summary>
        public static SoundDef Explosion_Flame;

        /// <summary>Slow Time. Authored onCamera, so it must be played with PlayOneShotOnCamera.</summary>
        public static SoundDef PsychicPulseGlobal;

        /// <summary>Become Ethereal. Positional, not onCamera.</summary>
        public static SoundDef PsychicSoothePulserCast;

        /// <summary>
        /// The near-invisible heat-shimmer ripple, used as the "boom" under a ring burst.
        /// Core, so it is safe on the baseline environment.
        /// </summary>
        public static FleckDef Fleck_HeatWaveDistortion;

        static DovahkiinVanillaDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DovahkiinVanillaDefOf));
        }
    }
}
