// Implements: Dragon Aspect's third word - when the Ancient Dragonborn arrives.
//
// ============================================================================================
// THREE TRIGGERS, ONE RULE
// ============================================================================================
// The user asked for him on three occasions:
//
//   1. casting the three-word shout while ALREADY hurt enough
//   2. dropping to the threshold WHILE the shout is running
//   3. being DOWNED while the shout is running
//
// Those are not three mechanisms. They are one condition - "the Dovahkiin is in trouble and
// Dragon Aspect is up" - sampled at different moments. So this comp evaluates a single rule,
// immediately when the hediff is applied and again on every rare tick, and fires at most once
// per activation. Writing three separate triggers would have given three chances to disagree
// about what "in trouble" means.
//
// He is a RESCUE, not a guarantee: casting at full health summons nobody, and that is the
// whole point of the mechanic.
// ============================================================================================
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public class HediffCompProperties_AncientDragonbornWatch : HediffCompProperties
    {
        public HediffCompProperties_AncientDragonbornWatch()
        {
            compClass = typeof(HediffComp_AncientDragonbornWatch);
        }
    }

    public class HediffComp_AncientDragonbornWatch : HediffComp
    {
        /// <summary>
        /// At most one summon per activation of the shout. Without this he would reappear on
        /// every rare tick for as long as the Dovahkiin stayed below the threshold - which is
        /// most of a fight - and an army of them would each be holding a doomed pawn.
        /// </summary>
        private bool summonedThisActivation;

        /// <summary>
        /// Severity carries the word count: 1 = Mul, 2 = Mul Qah, 3 = Mul Qah Diiv. Only the
        /// third word summons, so this is the gate. Compared with a margin rather than == 3
        /// because severity is a float and other code writes it.
        /// </summary>
        private bool AtThirdWord
        {
            get { return parent != null && parent.Severity >= 2.5f; }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            // Trigger 1: cast while already hurt. Checked here rather than waiting for the
            // first rare tick so he arrives with the shout, not up to four seconds after it.
            Evaluate();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            // CLAUDE.md forbids per-tick work that could be rarer, and RocketMan is installed.
            // Health does not change fast enough to need a per-tick sample.
            if (Pawn == null || !Pawn.IsHashIntervalTick(250))
            {
                return;
            }
            Evaluate();
        }

        private void Evaluate()
        {
            if (summonedThisActivation || !AtThirdWord)
            {
                return;
            }
            Pawn p = Pawn;
            if (p == null || p.Dead || !p.Spawned || p.Map == null)
            {
                return;
            }

            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            float threshold = t != null ? t.ancientDragonbornSummonHealthThreshold : 0.65f;

            bool inTrouble = p.Downed;
            if (!inTrouble && p.health != null && p.health.summaryHealth != null)
            {
                inTrouble = p.health.summaryHealth.SummaryHealthPercent <= threshold;
            }
            if (!inTrouble)
            {
                return;
            }

            // Set BEFORE summoning, not after. If the summon throws for any reason we must not
            // retry it every rare tick for the rest of the shout.
            summonedThisActivation = true;
            AncientDragonbornUtility.TrySummon(p);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref summonedThisActivation, "summonedThisActivation", false);
        }
    }
}
