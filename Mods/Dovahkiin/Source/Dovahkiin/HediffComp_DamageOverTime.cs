// Implements: SPEC.md 4.4a - Marked for Death bleeds its victim over time, as in TES5.
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public class HediffCompProperties_DamageOverTime : HediffCompProperties
    {
        /// <summary>Damage per tick interval, multiplied by the hediff's severity.</summary>
        public float damagePerIntervalPerSeverity = 1.2f;

        /// <summary>Ticks between applications. 300 = every 5 seconds.</summary>
        public int intervalTicks = 300;

        public DamageDef damageDef;

        /// <summary>
        /// Hard ceiling on how many times this ever deals damage. Zero or less means unlimited.
        ///
        /// This is the balance lever. With spreading enabled the curse touches a new body part
        /// each time, and an uncapped version covered the whole body - which RimWorld turns into
        /// lethal cumulative blood loss. Capping the count bounds total damage to
        /// maxApplications x damage, so the shout is dangerous but finite.
        ///
        /// The armour penalty on the hediff is NOT bounded by this and lasts until the mark
        /// decays, so the shout keeps its tactical value after it stops biting.
        /// </summary>
        public int maxApplications = 0;

        /// <summary>
        /// Optional glow pinned to the victim while the hediff lasts - TES5 marks its targets
        /// visibly. Emitted as an attached overlay fleck, which rides the pawn's own draw
        /// position and touches nothing in the render pipeline, so it cannot collide with other
        /// mods the way a PawnRenderer patch would.
        /// </summary>
        public FleckDef glowFleck;
        public float glowScale = 1.1f;
        public int glowIntervalTicks = 30;

        public HediffCompProperties_DamageOverTime()
        {
            compClass = typeof(HediffComp_DamageOverTime);
        }
    }

    /// <summary>
    /// Applies steady damage while the hediff is present, scaled by severity - which for
    /// Marked for Death is the shout's word level, so a fuller shout bleeds harder.
    ///
    /// Runs on an interval, not every tick: CLAUDE.md forbids avoidable per-tick work and
    /// RocketMan is installed. The counter is per-hediff and costs one integer compare a tick.
    /// </summary>
    public class HediffComp_DamageOverTime : HediffComp
    {
        private int ticksToNext;
        private int ticksToGlow;
        private int applicationsUsed;

        public HediffCompProperties_DamageOverTime Props
        {
            get { return (HediffCompProperties_DamageOverTime)props; }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }

            // The mark pulses on the victim while it lasts.
            if (Props.glowFleck != null)
            {
                ticksToGlow--;
                if (ticksToGlow <= 0)
                {
                    ticksToGlow = Mathf.Max(10, Props.glowIntervalTicks);
                    FleckMaker.AttachedOverlay(pawn, Props.glowFleck, Vector3.zero,
                        Props.glowScale, -1f);
                }
            }

            // Once the budget is spent the mark stops biting, but its armour penalty remains
            // until the hediff itself decays.
            if (Props.maxApplications > 0 && applicationsUsed >= Props.maxApplications)
            {
                return;
            }

            ticksToNext--;
            if (ticksToNext > 0)
            {
                return;
            }
            ticksToNext = Mathf.Max(30, Props.intervalTicks);
            applicationsUsed++;

            float amount = Props.damagePerIntervalPerSeverity * Mathf.Max(0.5f, parent.Severity);
            if (amount <= 0f)
            {
                return;
            }
            DamageDef def = Props.damageDef ?? DamageDefOf.Cut;

            // Spread before deepening: always hit the LEAST damaged part still attached, so the
            // curse covers the whole body before it starts working on a wound it already made.
            //
            // This is the balance lever. Untargeted damage concentrates on whatever RimWorld
            // rolls and reliably destroys a vital part quickly, which made a level-3 mark an
            // unavoidable death sentence in playtest. Spreading it means the same total damage
            // takes far longer to kill, and a tended victim can outlast it - the shout stays
            // stronger than a breath, but it is slow rather than certain.
            BodyPartRecord target = DovahkiinDamageUtility.SelectSpreadTarget(pawn);
            pawn.TakeDamage(new DamageInfo(def, amount, 0f, -1f, null, target));
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToNext, "ticksToNext", 0);
            Scribe_Values.Look(ref applicationsUsed, "applicationsUsed", 0);
        }
    }
}

