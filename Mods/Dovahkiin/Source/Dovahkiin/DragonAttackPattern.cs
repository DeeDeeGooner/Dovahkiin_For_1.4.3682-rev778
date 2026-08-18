// Implements: SPEC.md 6.5c-4 - a dovah does not roll dice about which STATE to be in, it picks
// an ATTACK and executes it.
//
// ============================================================================================
// WHY THIS REPLACES THE STATE RHYTHM RATHER THAN SITTING ON TOP OF IT
// ============================================================================================
// The old machine rolled every interval for which movement state to be in, with a dwell timer
// stopping it flickering. THAT DWELL TIMER CAUSED FIVE SEPARATE DEFECTS across three sessions -
// hovering in flight, meleeing from the air, carrying a grabbed pawn off the map, jogging after
// fleeing colonists for nine seconds, and an anti-hover check that took 2.5s to fire against a
// 1.5s grace. Every one was the same shape: a rule expressing a FACT queued behind a rhythm.
//
// The user saw the deeper problem, 2026-08-06: "the timers set shouldn't be set as a single
// general constant 'X secondes in this state', but rather 4 set of timers with each set for a
// specific pattern." A dwell timer and an attack pattern are TWO AUTHORS OF THE SAME DECISION -
// the timer says "hold this state N seconds then roll", the pattern says "the state sequence IS
// the attack". They cannot both be in charge.
//
// So the rhythm is gone. A pattern owns the state while it runs, and the only things that may
// override it are genuine facts (too hurt to fly, something in his jaws, standing in melee).
//
// ============================================================================================
// FLIGHT IS HOME
// ============================================================================================
// Every pattern has the same three phases: APPROACH in flight -> EXECUTE the attack -> LEAVE,
// circling in flight until the next one is chosen. Flight is the connective tissue BETWEEN
// attacks, not an attack competing with soar and grounded for time spent.
using System.Collections.Generic;
using Verse;

namespace Dovahkiin
{
    /// <summary>The four attacks of SPEC.md 6.5c-4. Rolled, never sequenced.</summary>
    public enum DragonAttackPattern
    {
        /// <summary>Chase in flight, land ON the target using the landing stun, brawl, leave.</summary>
        DiveAndBrawl,

        /// <summary>Soar static, breathe a circle onto them, leave.</summary>
        HoverBreath,

        /// <summary>Grounded static, breathe a cone, lift briefly, leave.</summary>
        PerchBreath,

        /// <summary>
        /// Stay in flight, strafe with breath, leave.
        /// ⚠ NOT SELECTABLE YET - it needs a breath whose impact point MOVES with the dragon
        /// (SPEC.md 4.6a), and Thing_DragonBreath's shapes are both static. Listed here so the
        /// enum matches the design, and excluded by Selectable below rather than quietly missing.
        /// </summary>
        StrafingRun
    }

    /// <summary>Which phase of its pattern the dragon is in. Every pattern runs all three.</summary>
    public enum DragonPatternPhase
    {
        /// <summary>Closing in flight until the pattern's own engage distance is met.</summary>
        Approach,

        /// <summary>Doing the thing - brawling, or breathing.</summary>
        Execute,

        /// <summary>Peeling off and circling in flight until the next pattern is chosen.</summary>
        Leave
    }

    public static class DragonAttackPatterns
    {
        /// <summary>
        /// The patterns that can actually be rolled today. StrafingRun is absent because the
        /// moving breath it needs does not exist - a pattern that cannot execute must not be
        /// selectable, or the dragon periodically does nothing at all and it reads as a hang.
        /// </summary>
        private static readonly DragonAttackPattern[] Selectable =
        {
            DragonAttackPattern.DiveAndBrawl,
            DragonAttackPattern.HoverBreath,
            DragonAttackPattern.PerchBreath
        };

        /// <summary>
        /// Roll the next attack, NEVER the one just performed.
        ///
        /// The user's rule, 2026-08-06: "make sure that the same pattern doesn't happen twice in
        /// a row." That is worth more than it looks - at three patterns, an unconstrained roll
        /// repeats about a third of the time, and a dragon that dives twice running reads as the
        /// AI being stuck rather than as a choice.
        ///
        /// Excluding the previous one is done by BUILDING A LIST WITHOUT IT rather than rolling
        /// until something different comes up: a reroll loop is unbounded in the worst case, and
        /// silently becomes an infinite one the day somebody leaves a single selectable pattern.
        /// </summary>
        public static DragonAttackPattern SelectNext(DragonAttackPattern? previous)
        {
            return SelectNext(previous, true);
        }

        /// <summary>
        /// As above, but <paramref name="allowGrounded"/> false removes every pattern whose
        /// execute phase happens on the ground. The caller passes false when his target is under a
        /// roof, because a dovah does not land or walk under one (the user, 2026-08-18).
        /// </summary>
        public static DragonAttackPattern SelectNext(DragonAttackPattern? previous,
            bool allowGrounded)
        {
            // ⚠ TWO BREATH PATTERNS MAY NEVER FOLLOW EACH OTHER EITHER.
            //
            // The user, 2026-08-06, after seeing a soar breath followed by a ground breath:
            // "pattern 2 and 3 should never altern in a row… yes I meant the soar breath pattern
            // and the ground breath pattern."
            //
            // So the rule is stronger than "not the same twice": after ANY breath, the next
            // attack must be a non-breath one. With three patterns selectable that makes the
            // rhythm alternate - brawl, breath, brawl, breath - which is the point: two breaths
            // running is the same beat twice however different their geometry looks.
            bool previousWasBreath = previous.HasValue && BreathShapeOf(previous.Value).HasValue;

            List<DragonAttackPattern> pool = new List<DragonAttackPattern>(Selectable.Length);
            for (int i = 0; i < Selectable.Length; i++)
            {
                if (previous.HasValue && Selectable[i] == previous.Value)
                {
                    continue;
                }
                if (previousWasBreath && BreathShapeOf(Selectable[i]).HasValue)
                {
                    continue;
                }
                if (!allowGrounded
                    && ExecuteStateOf(Selectable[i]) == AlduinMovementState.Grounded)
                {
                    continue;
                }
                pool.Add(Selectable[i]);
            }
            // Only possible if Selectable holds one entry, or if the roof rule and the
            // no-two-breaths rule between them exclude everything. Repeating something is the
            // honest answer - better than throwing, and better than silently doing nothing.
            if (pool.Count == 0)
            {
                // THE FALLBACK MUST HONOUR THE ROOF RULE TOO. Selectable[0] is DiveAndBrawl and
                // it is GROUNDED, so returning it here would put him on the floor of the very
                // building the constraint exists to keep him out of - and only in the rare case
                // that is hardest to reproduce, which is the worst place to leave a hole.
                if (!allowGrounded)
                {
                    for (int i = 0; i < Selectable.Length; i++)
                    {
                        if (ExecuteStateOf(Selectable[i]) != AlduinMovementState.Grounded)
                        {
                            return Selectable[i];
                        }
                    }
                }
                return Selectable[0];
            }
            return pool[Rand.Range(0, pool.Count)];
        }

        /// <summary>Does this pattern breathe, and in which shape? Null means it does not.</summary>
        public static DragonBreathShape? BreathShapeOf(DragonAttackPattern pattern)
        {
            switch (pattern)
            {
                case DragonAttackPattern.HoverBreath: return DragonBreathShape.Pool;
                case DragonAttackPattern.PerchBreath: return DragonBreathShape.Cone;
                default: return null;
            }
        }

        /// <summary>
        /// Which movement state the EXECUTE phase runs in. Approach and Leave are always Flight -
        /// that is what "flight is home" means mechanically.
        /// </summary>
        public static AlduinMovementState ExecuteStateOf(DragonAttackPattern pattern)
        {
            switch (pattern)
            {
                case DragonAttackPattern.HoverBreath: return AlduinMovementState.Soar;
                case DragonAttackPattern.StrafingRun: return AlduinMovementState.Flight;
                default: return AlduinMovementState.Grounded;
            }
        }

        /// <summary>
        /// How close he must be before the EXECUTE phase begins, in cells. Each pattern has its
        /// own reach: a brawl needs to be on top of them, a breath only needs them in range.
        /// </summary>
        public static float EngageDistanceOf(DragonAttackPattern pattern, DovahkiinTuningDef t)
        {
            switch (pattern)
            {
                case DragonAttackPattern.HoverBreath:
                    return t != null ? t.dragonBreathSoarRange : 14f;
                case DragonAttackPattern.PerchBreath:
                    // Deliberately shorter than the cone's full reach, so he closes to a
                    // believable distance rather than breathing from the far edge of it.
                    return (t != null ? t.dragonBreathConeRange : 24f) * 0.6f;
                default:
                    return t != null ? t.dragonLandToBiteDistance : 4f;
            }
        }

        /// <summary>
        /// How long the EXECUTE phase lasts, in ticks. THIS IS THE PER-PATTERN TIMER SET the user
        /// asked for in place of one global "X seconds in this state".
        ///
        /// For the breath patterns it is derived from the breath's own duration plus a short tail,
        /// so the dragon cannot peel away mid-jet - two numbers that must agree, derived rather
        /// than stored twice, which is this project's most repeated bug.
        /// </summary>
        public static int ExecuteTicksOf(DragonAttackPattern pattern, DovahkiinTuningDef t)
        {
            int breath = t != null ? t.dragonBreathDurationTicks : 263;
            switch (pattern)
            {
                // BOTH breath patterns are breath + tail, and the tail is the user's 2-second
                // motionless beat: "circling -> soar/ground breath -> stay grounded/soaring
                // MOTIONLESS for 2s -> flight circling".
                //
                // PerchBreath used to add a "brief static soar" on its way out, from SPEC.md
                // 6.5c-4. That is REMOVED: the user's later description has him holding in the
                // state he breathed in - grounded stays grounded - and a lift would have been a
                // second, contradictory author of the same moment.
                case DragonAttackPattern.HoverBreath:
                case DragonAttackPattern.PerchBreath:
                    // ⚠ THE WIND-UP IS COUNTED HERE, NOT ADDED AT THE CALL SITE. The user,
                    // 2026-08-13: "flight circling -> Land -> wait 4s -> Launch breathing -> Xs of
                    // pause, the same amount actually set and used currently -> flight circling."
                    //
                    // Counting it inside the phase length keeps ONE number authoritative. Adding it
                    // outside would leave the phase timer and the hold-still stun disagreeing by
                    // four seconds, and two-numbers-that-must-agree is the failure this project has
                    // repeated more than any other.
                    return (t != null ? t.dragonBreathWindupTicks : 240)
                        + breath
                        + (t != null ? t.dragonPatternBreathTailTicks : 240);
                default:
                    return t != null ? t.dragonPatternBrawlTicks : 600;
            }
        }
    }
}
