// The per-cell terrain effects shared by everything in this mod that sweeps fire or frost over
// ground: the Dovahkiin's shout waves and, from 2026-08-05, a dragon's breath.
//
// WHY THIS EXISTS RATHER THAN A SECOND COPY IN Thing_DragonBreath. The notebook's instruction
// when the breath was designed: "REUSE Thing_ShoutWave.StrikeBand for ignition and snow rather
// than rewriting them, or the burned cells and snowy patches come out subtly different from the
// Dovahkiin's." Two independent copies of "0.25 chance of fire, 0.8 chance of snow" drift the
// moment either is tuned, and the drift is invisible until someone compares two screenshots.
//
// Deliberately NARROW. Only the terrain half is shared. The DAMAGE half stays where it is:
// Thing_ShoutWave hits each victim once as its front passes, Thing_DragonBreath hits whoever is
// standing in it on every pulse, and those are genuinely different rules rather than one rule
// with a flag. Merging them would need a parameter that means "are you a wave or a breath",
// which is the shape of a function that should have stayed two functions.
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public static class DovahkiinStrike
    {
        /// <summary>Chance a cell under a fire effect actually catches. Tuned for the shouts.</summary>
        private const float IgniteChance = 0.25f;

        /// <summary>Fire strength handed to FireUtility. Low - this lights ground, not a bonfire.</summary>
        private const float IgniteStrength = 0.4f;

        /// <summary>Chance a cell under a frost effect takes snow.</summary>
        private const float SnowChance = 0.8f;

        /// <summary>
        /// Scorch or freeze one cell. Safe to call on any cell; does nothing when both effects
        /// are off, which is the common case.
        /// </summary>
        public static void ScorchCell(IntVec3 c, Map map, bool igniteGround, float snowDepth)
        {
            if (map == null || !c.InBounds(map))
            {
                return;
            }
            if (igniteGround && Rand.Chance(IgniteChance))
            {
                FireUtility.TryStartFireIn(c, map, IgniteStrength);
            }
            // Snow is a real terrain layer, so this slows anything crossing it afterwards - the
            // effect leaves the ground changed rather than just flashing over it.
            if (snowDepth > 0f && map.snowGrid != null && Rand.Chance(SnowChance))
            {
                map.snowGrid.AddDepth(c, snowDepth);
            }
        }
    }
}
