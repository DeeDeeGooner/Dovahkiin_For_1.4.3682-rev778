// Implements: SPEC.md 4.4a - shared body-part selection for spread damage.
//
// Extracted from HediffComp_DamageOverTime once Unrelenting Force needed the same behaviour.
// One copy, so a future balance change lands everywhere at once.
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public static class DovahkiinDamageUtility
    {
        /// <summary>
        /// Picks where to hit next: spreads before deepening, biased toward the core and away
        /// from extremities. Returns null if nothing suitable, which lets RimWorld pick as usual.
        ///
        /// Three revisions, all wrong in instructive ways:
        ///  - Comparing health *fractions* re-picked the torso forever, because a torso has far
        ///    more max health than a finger.
        ///  - Comparing absolute damage spread correctly but perfectly evenly, so every toe and
        ///    foot got crushed and victims were reliably downed by leg damage.
        ///  - Scoring damage-taken divided by a priority weight does both: a favoured part can
        ///    carry proportionally more damage and still be chosen, so the bias persists rather
        ///    than washing out after the first pass.
        /// </summary>
        public static BodyPartRecord SelectSpreadTarget(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return null;
            }
            List<BodyPartRecord> best = new List<BodyPartRecord>();
            float bestScore = float.MaxValue;

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (!CanBeHitDirectly(part))
                {
                    continue;
                }
                float max = part.def.GetMaxHealth(pawn);
                if (max <= 0f)
                {
                    continue;
                }
                float taken = max - pawn.health.hediffSet.GetPartHealth(part);
                float score = (taken + 1f) / PriorityWeight(part);

                if (score < bestScore - 0.001f)
                {
                    bestScore = score;
                    best.Clear();
                    best.Add(part);
                }
                else if (score <= bestScore + 0.001f)
                {
                    best.Add(part);
                }
            }
            if (best.Count == 0)
            {
                return null;
            }
            return best[Rand.Range(0, best.Count)];
        }

        /// <summary>
        /// The opposite of SelectSpreadTarget: the part that has ALREADY taken the most damage.
        /// Fire uses it to burn the same wound deeper instead of finding fresh skin, which is
        /// what makes flame more lethal than frost at equal totals - concentrated damage
        /// destroys parts, spread damage merely hurts.
        ///
        /// Organs are excluded. Deepening is meant to burn off a limb, not detonate a heart.
        /// </summary>
        public static BodyPartRecord SelectDeepenTarget(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return null;
            }
            BodyPartRecord worst = null;
            float worstTaken = 0f;

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                // Organs excluded on purpose here - deepening burns off a limb, it does not
                // detonate a heart. The coverage test is separate and is the ENGINE'S rule: a
                // zero-coverage part logs a red error when injured. See CanBeHitDirectly.
                if (part.depth == BodyPartDepth.Inside || !CanBeHitDirectly(part))
                {
                    continue;
                }
                float max = part.def.GetMaxHealth(pawn);
                if (max <= 0f)
                {
                    continue;
                }
                float taken = max - pawn.health.hediffSet.GetPartHealth(part);
                if (taken > worstTaken)
                {
                    worstTaken = taken;
                    worst = part;
                }
            }
            // Null when nothing is hurt yet, which lets RimWorld roll normally.
            return worst;
        }

        /// <summary>
        /// Can an aimed external hit land on this part at all?
        ///
        /// ⚠ THIS IS THE ENGINE'S OWN TEST, COPIED EXACTLY. `Verse.Hediff_Injury.PostAdd` does:
        ///
        ///     if (Part != null &amp;&amp; Part.coverageAbs &lt;= 0f &amp;&amp; dinfo.Def != SurgicalCut)
        ///         Log.Error("Added injury to " + Part.def + " but it should be impossible to hit it...")
        ///
        /// so anything with zero absolute coverage produces a RED ERROR every time it is targeted.
        /// The injury is still applied - it is in PostAdd, not a rejection - so nothing was losing
        /// damage. It was pure log spam, but red, and CLAUDE.md forbids leaving red errors.
        ///
        /// FOUND 2026-08-05 by the user testing a dragon's breath on pawns: "Added injury to
        /// Tongue but it should be impossible to hit it." **The bug is PRE-EXISTING and belongs to
        /// the Dovahkiin's shouts** - Marked for Death and Unrelenting Force use this same picker.
        /// The breath merely made it obvious: nine pulses x four instances is 36 aimed hits per
        /// victim, so a part that used to come up once in a long while now comes up constantly.
        ///
        /// NOTE THE TEST IS `coverageAbs`, NOT `depth == Inside`. They are different rules, and
        /// guessing at the second is how this would come back: an internal organ with real
        /// coverage stays legitimately targetable, which preserves Marked for Death's intent that
        /// organs are fair game, while the tongue and its like drop out.
        /// </summary>
        public static bool CanBeHitDirectly(BodyPartRecord part)
        {
            return part != null && part.coverageAbs > 0f;
        }

        /// <summary>
        /// How strongly to favour a part. Higher is hit sooner and more often.
        /// Core and head over limbs; limbs well over feet and toes, which are what down a pawn.
        /// </summary>
        public static float PriorityWeight(BodyPartRecord part)
        {
            // Organs are inside the body - thematically right for a death-mark, but weighted
            // below the torso so a heart is never the first thing crushed.
            if (part.depth == BodyPartDepth.Inside)
            {
                return 2.0f;
            }
            switch (part.height)
            {
                case BodyPartHeight.Middle:
                    return 3.0f; // torso, shoulders, arms
                case BodyPartHeight.Top:
                    return 2.5f; // head, neck
                case BodyPartHeight.Bottom:
                    return 0.6f; // legs, feet, toes
                default:
                    return 1.0f;
            }
        }
    }
}
