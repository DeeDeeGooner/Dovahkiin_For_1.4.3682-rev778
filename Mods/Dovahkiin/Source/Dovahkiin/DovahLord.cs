// THE AI LAYER FOR DRAGONS. Faction + Lord + a duty per attack-pattern phase.
//
// ============================================================================================
// WHY THIS EXISTS - THE FINDING THAT FORCED IT
// ============================================================================================
// The attack-pattern executor could set a dragon's STATE but never his BEHAVIOUR. The HOVER-DIAG
// log showed the manhunter think tree owning every job he ran - AttackMelee, Wait_MaintainPosture,
// Wait_Wander - and discarding our circling Goto outright: the destination changed between
// samples while the job came back as Wait. So the sprite and the speed followed the pattern while
// the fighting was ordinary manhunter AI. The user, across three playtests: "doesn't circle
// around at all and just chases and kills like any normal manhunting beast", then "he still acts
// very much like a wild beast".
//
// THE CAUSE IS STRUCTURAL, not a bug to be patched: **MENTAL STATES OUTRANK LordDuty IN THE
// ANIMAL THINK TREE.** Read off Core/ThinkTreeDefs/Animal.xml, in priority order:
//
//     Downed -> BurningResponse -> MentalStateCritical -> ... -> MentalStateNonCritical
//            -> RopedPawn -> LordDuty -> Animal_PreMain (mod insert tag)
//
// While a dragon is in ManhunterPermanent, NOTHING below that line ever runs - not a Lord duty,
// not a custom think node, not an injected job. Job injection cannot win against a mental state;
// it is a tug-of-war, and the tug-of-war IS the twitching that was reported for four sessions.
//
// So the mental state has to go. But hostility came FROM the mental state - a factionless animal
// is hostile to nobody - hence the faction. The two are one change, not two.
//
// ============================================================================================
// WHY THE TOIL DOES NOTHING
// ============================================================================================
// A normal LordJob drives its pawns by having each LordToil assign duties in UpdateAllDuties.
// Ours deliberately does not: the ATTACK PATTERN decides the duty, phase by phase, and a toil
// that also assigned one would be a second author of the same decision - which is precisely the
// mistake that made the dwell timer cause five defects. The Lord exists here only to satisfy
// ThinkNode_ConditionalHasLord so the duty branch runs at all.
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Dovahkiin
{
    /// <summary>
    /// A toil that assigns nothing. Comp_AlduinFlight sets each dragon's duty per pattern phase;
    /// this exists so the Lord has a graph and the pawn passes ThinkNode_ConditionalHasLord.
    /// </summary>
    public class LordToil_DovahPatterns : LordToil
    {
        /// <summary>
        /// ⚠ DELIBERATELY EMPTY. The pattern owns the duty. If this ever starts assigning one,
        /// the pattern and the toil will fight and the dragon will twitch between them.
        /// </summary>
        public override void UpdateAllDuties()
        {
        }
    }

    public class LordJob_DovahPatterns : LordJob
    {
        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            graph.AddToil(new LordToil_DovahPatterns());
            return graph;
        }
    }

    public static class DovahFactionUtility
    {
        private const string FactionDefName = "Dovahkiin_DovFaction";

        /// <summary>
        /// The dov faction, created on demand if the world has none.
        ///
        /// ⚠ CREATED AT RUNTIME RATHER THAN AT WORLD GENERATION, and that is the whole reason
        /// requiredCountAtGameStart is 0. A faction with a required count is generated when the
        /// world is made - which does nothing whatsoever for a save that already exists, and this
        /// project is being tested on one. Creating it on demand behaves identically on an old
        /// save and a new one.
        /// </summary>
        public static Faction DovFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (def == null)
            {
                // GetNamedSilentFail returns null with no message - without this a def that failed
                // to load would present as "dragons are passive", which is indistinguishable from
                // the AI being broken.
                Log.WarningOnce("[Dovahkiin] " + FactionDefName + " FactionDef missing - dragons "
                    + "cannot be made hostile and will ignore everyone.", 0x5A1D04);
                return null;
            }
            if (Find.FactionManager == null)
            {
                return null;
            }
            Faction existing = Find.FactionManager.FirstFactionOfDef(def);
            if (existing != null)
            {
                return existing;
            }
            Faction made = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
            Find.FactionManager.Add(made);
            return made;
        }

        /// <summary>
        /// Put a dragon under the dov faction and give him a Lord, so his duties can run. Safe to
        /// call every tick; it returns immediately once he is set up.
        ///
        /// ⚠ AND IT ENDS ANY MENTAL STATE. A manhunting dragon outranks his own Lord duty, so
        /// leaving the mental state on would silently undo everything this method exists for.
        /// Hostility now comes from the faction, so the mental state is not merely unnecessary -
        /// it is the thing that was preventing the patterns from working.
        /// </summary>
        public static void EnsureUnderLord(Pawn dragon)
        {
            if (dragon == null || !dragon.Spawned || dragon.Dead || dragon.Map == null)
            {
                return;
            }

            if (dragon.InMentalState)
            {
                dragon.mindState.mentalStateHandler.CurState.RecoverFromState();
            }

            Faction dov = DovFaction();
            if (dov != null && dragon.Faction != dov)
            {
                // previousFaction null: he was never anybody's, and a dragon does not defect.
                dragon.SetFaction(dov);
            }

            if (dragon.GetLord() == null)
            {
                LordMaker.MakeNewLord(dragon.Faction, new LordJob_DovahPatterns(), dragon.Map,
                    new List<Pawn> { dragon });
            }
        }

        /// <summary>
        /// Hand the dragon the duty his current pattern phase wants. Called every tick by the
        /// pattern executor - assigning the same duty twice is free, so there is no need to track
        /// what was set last.
        ///
        /// The three duties, and why each:
        ///   APPROACH / BRAWL -> AssaultThing. Its think node is JobGiver_AIFightEnemies, which
        ///                       finds targets BY HOSTILITY - which is exactly what the faction
        ///                       now provides and what a mental state used to.
        ///   BREATHING        -> Defend on his own cell at radius 0. He must not wander off
        ///                       mid-jet; the breath and the state hold do the rest.
        ///   LEAVE            -> WanderClose around the fight. THIS IS THE CIRCLING, and it needs
        ///                       no hostility at all - JobGiver_WanderNearDutyLocation just moves
        ///                       him about near a point. It is what four rounds of injected Goto
        ///                       jobs were failing to achieve.
        /// </summary>
        public static void SetDuty(Pawn dragon, DutyDef def, LocalTargetInfo focus, float radius)
        {
            if (dragon == null || dragon.mindState == null || def == null)
            {
                return;
            }
            PawnDuty duty = dragon.mindState.duty;
            if (duty != null && duty.def == def && duty.focus == focus)
            {
                return;
            }
            dragon.mindState.duty = new PawnDuty(def, focus, radius);
            // The think tree only re-decides when the current job ends, so an old job outlives a
            // new duty by however long it takes to finish. Ending it makes the duty take effect
            // on the next tick instead of whenever the previous chase happens to conclude.
            if (dragon.jobs != null && dragon.jobs.curJob != null)
            {
                dragon.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            }
        }
    }
}
