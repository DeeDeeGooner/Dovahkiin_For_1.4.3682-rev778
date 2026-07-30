// Implements: Dragon Aspect's third word - the Ancient Dragonborn summon. RISKS.md section 9.
//
// ============================================================================================
// THE SUMMON IS ALWAYS DOOMED, FOR THE SAME REASON THE DEAD PUPPET IS
// ============================================================================================
// RISKS.md section 9 names temporary pawns as the top save-corruption risk in this mod. Soul
// Tear's puppet only became safe by being incurable, untendable, non-removable and lethal on
// expiry - every exit path already ends, so save -> load carries nothing bespoke.
//
// This follows that pattern exactly, and then handles TWO FAILURE MODES THE PUPPET DOES NOT
// HAVE, because a summon CREATES a pawn where the puppet converts an existing one:
//
//   1. NO CORPSE. Destroy(DestroyMode.Vanish), not Kill. A spectral ally that leaves a body to
//      haul, butcher or bury is wrong, and a corpse is another object holding a reference to a
//      pawn that should stop existing.
//
//   2. NO WORLD-PAWN LEAK. A generated pawn that reaches Find.WorldPawns and is never discarded
//      stays in the save forever. Dragon Aspect is once per day, so a five-year colony is on the
//      order of 1800 summons - this would never show up in a playtest and would surface months
//      later as a save that grows without bound. RemoveAndDiscardPawnViaGC is the fix and it is
//      public.
//
// Do not add a way to remove, cure, tend or keep this. The absence of those is the design.
// ============================================================================================
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;      // Job, JobCondition, LocomotionUrgency
using Verse.Sound;   // PlayOneShot is an extension method on SoundDef, not a member

namespace Dovahkiin
{
    public class Hediff_AncientDragonborn : HediffWithComps
    {
        private int ticksRemaining = 3750;   // 1.5 in-game hours; overwritten at summon

        // HE KNOWS THREE SHOUTS, NOT ONE.
        //
        // The original design rolled fire OR frost once at summon and kept it for his whole life.
        // The user removed that rule: he is the Dragonborn's own shard and should have Fire
        // Breath, Frost Breath and Unrelenting Force alike. The notebook recorded the 50/50 roll
        // as settled - it has been superseded deliberately, not forgotten.
        private const int SHOUT_FIRE  = 0;
        private const int SHOUT_FROST = 1;
        private const int SHOUT_FORCE = 2;

        /// <summary>
        /// Which shout comes next, cycling 0-1-2. Seeded at summon so two summons do not open
        /// with the same one. Cycled rather than rolled: he only gets three or four casts in a
        /// 1.5-hour life, and at that sample size a random roll routinely produces a whole
        /// summoning using one shout - which is the thing being fixed.
        /// </summary>
        private int shoutCycle;

        private int breathCooldown;
        private int scanCounter;

        /// <summary>NEVER removable - see the header. Same rule as Hediff_DeadPuppet.</summary>
        public override bool ShouldRemove
        {
            get { return false; }
        }

        public override string LabelInBrackets
        {
            get { return ticksRemaining.ToStringTicksToPeriod(true, false, true, true, false); }
        }

        public int TicksRemaining
        {
            get { return ticksRemaining; }
        }

        /// <summary>
        /// <paramref name="frost"/> no longer decides his only element - he has all three now.
        /// It seeds which shout he opens with, so the coin flip the summon code already makes
        /// still varies him instead of being discarded.
        /// </summary>
        public void Configure(int lifetimeTicks, bool frost)
        {
            ticksRemaining = Mathf.Max(60, lifetimeTicks);
            shoutCycle = frost ? SHOUT_FROST : SHOUT_FIRE;
            // He does not open with a breath the instant he lands - that reads as a scripted
            // cutscene rather than an ally joining a fight.
            breathCooldown = Tuning != null ? Tuning.ancientDragonbornBreathFirstDelayTicks : 120;
        }

        private static DovahkiinTuningDef Tuning
        {
            get { return DovahkiinTuningDef.Current; }
        }

        public override void Tick()
        {
            base.Tick();
            Pawn p = pawn;
            if (p == null)
            {
                return;
            }

            // Killed in a fight rather than expiring. Clean up on the next tick we get - though
            // the Pawn.Kill patch normally beats us to it, this costs nothing and covers the
            // case where the hediff outlives the kill path.
            if (p.Dead)
            {
                VanishNow(p);
                return;
            }

            ticksRemaining--;
            if (ticksRemaining <= 0)
            {
                VanishNow(p);
                return;
            }

            if (breathCooldown > 0)
            {
                breathCooldown--;
            }

            // CLAUDE.md forbids per-tick work that could be less frequent, and RocketMan is
            // installed. The lifetime countdown has to be per-tick to stay accurate; deciding
            // whether to breathe does not.
            scanCounter--;
            if (scanCounter > 0)
            {
                return;
            }
            scanCounter = 30;

            if (p.Spawned && !p.Downed && p.Map != null)
            {
                // Resolved once per scan and shared by the breath and the melee nudge, so the two
                // can never disagree about what he is fighting.
                Pawn assist = FindAssistTarget(p);

                if (breathCooldown <= 0)
                {
                    TryBreathe(p, assist);
                }
                if (!TryAssistDovahkiin(p, assist))
                {
                    KeepNearDovahkiin(p);
                }
            }
        }

        /// <summary>
        /// What, if anything, the Dovahkiin is currently fighting that this summon should join.
        ///
        /// WHY THIS IS NEEDED AT ALL: a wild animal is not hostile to anybody.
        /// `GenHostility.HostileTo` returns true only for faction hostility, a manhunter mental
        /// state (`MentalState.ForceHostileTo`), a predator hunting us, a prison break or a slave
        /// rebellion - read out of its IL, not assumed. So when the user sent the Dovahkiin at a
        /// wild boar, the boar was an enemy to no one: the breath scan skipped it, and his own AI
        /// had no reason to touch it either, since hunting is a work job and he has no work types.
        /// He stood and watched, which the user reported.
        ///
        /// Returns null when there is nothing to help with, which is the normal case.
        /// </summary>
        private static Pawn FindAssistTarget(Pawn p)
        {
            DovahkiinTuningDef t = Tuning;
            float radius = t != null ? t.ancientDragonbornAssistRadius : 24f;
            if (radius <= 0f)
            {
                return null;      // switched off by tuning
            }

            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null)
            {
                return null;
            }
            Pawn dov = reg.CurrentDovahkiin;
            if (dov == null || dov.Dead || !dov.Spawned || dov.Map != p.Map)
            {
                return null;
            }

            // A player-ordered attack lands in CurJob; an AI-chosen one in mindState.enemyTarget.
            // Both are checked because the Dovahkiin is a colonist and can be either.
            Thing target = null;
            Job dovJob = dov.CurJob;
            if (dovJob != null && dovJob.def != null
                && (dovJob.def == JobDefOf.AttackMelee
                    || dovJob.def == JobDefOf.AttackStatic
                    || dovJob.def == JobDefOf.Hunt))
            {
                if (dovJob.targetA.IsValid)
                {
                    target = dovJob.targetA.Thing;
                }
            }
            if (target == null && dov.mindState != null)
            {
                target = dov.mindState.enemyTarget;
            }

            Pawn victim = target as Pawn;
            if (victim == null || victim == p || victim == dov)
            {
                return null;
            }
            if (victim.Dead || !victim.Spawned || victim.Map != p.Map || victim.Downed)
            {
                return null;      // already beaten - let the Dovahkiin finish it
            }

            // NEVER join an attack on our own side. The Dovahkiin can be ordered to attack a
            // colonist or a tamed animal, and a summoned ally piling onto one would be far worse
            // than the summon doing nothing. Anything in the player faction is off limits.
            if (victim.Faction != null && victim.Faction.IsPlayer)
            {
                return null;
            }

            if (p.Position.DistanceTo(victim.Position) > radius)
            {
                return null;      // bounded, or he abandons the man he exists to protect
            }
            return victim;
        }

        /// <summary>
        /// Send him at whatever the Dovahkiin is fighting, if he is not already busy.
        ///
        /// Same light-touch rule as <see cref="KeepNearDovahkiin"/>: only overrides idling and
        /// wandering, never a real job. If he is already in a fight of his own that fight is his,
        /// and if he is already attacking this target there is nothing to do.
        ///
        /// Returns true when he has been given, or already holds, an attack job - which is the
        /// signal to skip the follow nudge, because otherwise the two would fight each other for
        /// his attention every scan.
        /// </summary>
        private static bool TryAssistDovahkiin(Pawn p, Pawn victim)
        {
            if (victim == null || p.jobs == null)
            {
                return false;
            }

            Job cur = p.CurJob;
            if (cur != null && cur.def == JobDefOf.AttackMelee
                && cur.targetA.IsValid && cur.targetA.Thing == victim)
            {
                return true;      // already on it
            }
            if (cur != null
                && cur.def != JobDefOf.Wait
                && cur.def != JobDefOf.Wait_Wander
                && cur.def != JobDefOf.Wait_Combat
                && cur.def != JobDefOf.GotoWander
                && cur.def != JobDefOf.Goto)
            {
                return false;     // his own fight, or some other real job
            }

            // JobMaker does not exist in 1.4 - jobs are constructed directly.
            Job job = new Job(JobDefOf.AttackMelee, victim);
            // Fight until it is down, rather than landing one blow and wandering off. Set
            // explicitly rather than trusting the field's default.
            job.maxNumMeleeAttacks = int.MaxValue;
            job.killIncappedTarget = false;
            // Re-decide periodically instead of locking on: if the target flees, or the Dovahkiin
            // moves on, the next scan picks that up.
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return p.jobs.TryTakeOrderedJob(job, null, false);
        }

        /// <summary>
        /// Stay with the Dovahkiin. He is a bodyguard, not a wanderer.
        ///
        /// Only nudges him when he has drifted past the leash AND is idle or merely wandering -
        /// an existing job is left alone, so this never interrupts a fight, and a pawn already
        /// walking back is not re-ordered every rare tick. Deliberately light-touch: the AI is
        /// perfectly capable of fighting on its own, and the only thing wrong with it was that
        /// it had no reason to stay close once the fighting stopped.
        /// </summary>
        private static void KeepNearDovahkiin(Pawn p)
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null)
            {
                return;
            }
            Pawn dov = reg.CurrentDovahkiin;
            if (dov == null || dov.Dead || !dov.Spawned || dov.Map != p.Map)
            {
                return;
            }

            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            float leash = t != null ? t.ancientDragonbornFollowRadius : 8f;
            if (p.Position.DistanceTo(dov.Position) <= leash)
            {
                return;
            }

            // Never interrupt real work - and for this pawn "real work" is fighting. Wandering
            // and standing idle are the only states worth overriding.
            Job cur = p.CurJob;
            if (cur != null
                && cur.def != JobDefOf.Wait
                && cur.def != JobDefOf.Wait_Wander
                && cur.def != JobDefOf.GotoWander
                && cur.def != JobDefOf.Goto)
            {
                return;
            }

            IntVec3 spot;
            if (!CellFinder.TryRandomClosewalkCellNear(dov.Position, dov.Map, 3, out spot, null))
            {
                spot = dov.Position;
            }
            if (!spot.IsValid || !spot.Standable(p.Map))
            {
                return;
            }
            // Constructed directly: there is no JobMaker type in 1.4 - checked, not assumed.
            Job goTo = new Job(JobDefOf.Goto, spot);
            goTo.locomotionUrgency = LocomotionUrgency.Jog;
            p.jobs.StartJob(goTo, JobCondition.InterruptForced, null, false, true, null, null, false, false);
        }

        /// <summary>
        /// Fire or frost at the nearest hostile, but ONLY down a cone with no ally in it.
        ///
        /// The safety check walks the SAME cells the wave will travel through, via
        /// ShoutTargeting.CellsInCone with the same range and angle the breath is spawned with.
        /// Writing a second, separate "is anyone in front of me" test is how a safety check
        /// drifts away from where the flames actually land.
        /// </summary>
        private void TryBreathe(Pawn p, Pawn assist)
        {
            DovahkiinTuningDef t = Tuning;
            float range = t != null ? t.ancientDragonbornBreathRange : 9f;
            float cone = t != null ? t.ancientDragonbornBreathCone : 46f;

            Pawn target = FindBreathTarget(p, range, assist);
            if (target == null)
            {
                return;
            }
            if (!ConeIsClearOfAllies(p, target.Position, range, cone, assist))
            {
                return;
            }

            // WHICH OF THE THREE. He is a Dragonborn, not an elemental - the user removed the
            // one-element rule, so Fire, Frost and Force all belong to him.
            //
            // CYCLED, not rolled. A per-cast random roll produces streaks, and at three or four
            // casts in a 1.5-hour life a streak means a whole summoning where he only ever used
            // one shout - which is the very thing being fixed. Cycling guarantees all three
            // appear. The starting point is rolled at summon so two summons do not open
            // identically.
            int which = ((shoutCycle % 3) + 3) % 3;
            shoutCycle = which + 1;

            Color head;
            FleckDef fleck;
            SoundDef snd;
            if (which == SHOUT_FROST)
            {
                head = new Color(0.62f, 0.85f, 1f);
                fleck = DovahkiinDefOf.Dovahkiin_Fleck_ForceWave;
                snd = DovahkiinVanillaDefOf.PsychicSoothePulserCast;
            }
            else if (which == SHOUT_FORCE)
            {
                // Unrelenting Force has no element - it is pressure. Same blue-white the
                // Dovahkiin's own fus ro uses, so the two read as the same shout.
                head = new Color(0.45f, 0.75f, 1f);
                fleck = DovahkiinDefOf.Dovahkiin_Fleck_ForceWave;
                snd = SoundDefOf.Thunder_OnMap;
            }
            else
            {
                head = new Color(1f, 0.52f, 0.16f);
                fleck = DovahkiinDefOf.Dovahkiin_Fleck_FireWave;
                snd = DovahkiinVanillaDefOf.Explosion_Flame;
            }

            Thing_ShoutWave wave = Thing_ShoutWave.Spawn(
                p, target.Position, range, cone, head, fleck, 1.6f);
            if (wave == null)
            {
                return;
            }

            float dmg = t != null ? t.ancientDragonbornBreathDamage : 18f;
            int parts = t != null ? t.ancientDragonbornBreathInstances : 5;

            if (which == SHOUT_FROST)
            {
                // Mirrors Frost Breath's shape at a reduced strength: spread, chilling, with a
                // short stun, and no ignition.
                wave.SetPayload(DamageDefOf.Frostbite, dmg, 0f, 90, false, false,
                    null, 1f, parts, 0.35f, null, 1f, true, 0f, null, 0f, 0.35f);
            }
            else if (which == SHOUT_FORCE)
            {
                // Mirrors the Dovahkiin's fus ro: Blunt, spread, knocked back and stunned. Blunt
                // and spread together on purpose - cutting damage spread over many parts kills by
                // cumulative blood loss, which is not what a push is meant to do.
                float force = t != null ? t.ancientDragonbornForceDamage : 7f;
                int fParts = t != null ? t.ancientDragonbornForceInstances : 3;
                float push = t != null ? t.ancientDragonbornForceKnockbackCells : 3f;
                int fStun = t != null ? t.ancientDragonbornForceStunTicks : 150;
                wave.SetPayload(DamageDefOf.Blunt, force, push, fStun, false, false,
                    null, 1f, fParts, 0f, null, 1f, true, 0f, null, 0f, 0f);
            }
            else
            {
                // Fire: ignites pawns but NOT the ground. An ally-safe cone is still no reason to
                // start a fire the colony then has to put out, and Storm Call already established
                // that setting fire to a base is never acceptable collateral.
                wave.SetPayload(DamageDefOf.Flame, dmg, 0f, 0, true, false,
                    null, 1f, parts, 0f, null, 1f, false, 0.25f, null, 0f, 0.35f);
            }

            if (snd != null)
            {
                snd.PlayOneShot(new TargetInfo(p.Position, p.Map, false));
            }

            breathCooldown = t != null ? t.ancientDragonbornBreathCooldownTicks : 420;
        }

        /// <summary>
        /// Nearest thing worth breathing on. <paramref name="assist"/> is whatever the Dovahkiin
        /// is fighting, and counts as a valid target even though it is not hostile to anyone -
        /// a wild animal never is. See FindAssistTarget for why.
        /// </summary>
        private static Pawn FindBreathTarget(Pawn p, float range, Pawn assist)
        {
            List<Pawn> all = p.Map.mapPawns.AllPawnsSpawned;
            Pawn best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == null || other == p || other.Dead || other.Downed)
                {
                    continue;
                }
                if (!other.HostileTo(p) && other != assist)
                {
                    continue;
                }
                float d = p.Position.DistanceTo(other.Position);
                if (d > range || d < 1.5f)
                {
                    continue;
                }
                if (!GenSight.LineOfSight(p.Position, other.Position, p.Map, true))
                {
                    continue;
                }
                if (d < bestDist)
                {
                    bestDist = d;
                    best = other;
                }
            }
            return best;
        }

        /// <summary>
        /// True when nothing friendly stands anywhere in the cone the breath will sweep.
        ///
        /// Deliberately strict: the Dovahkiin, colonists, tamed animals, neutral visitors and
        /// prisoners all block the shout. He waits for a clean line rather than accepting
        /// "acceptable" collateral - a summoned ally that burns the colony is worse than one
        /// that does nothing.
        /// </summary>
        private static bool ConeIsClearOfAllies(Pawn p, IntVec3 targetCell, float range, float cone,
            Pawn assist)
        {
            Map map = p.Map;
            HashSet<IntVec3> cells = new HashSet<IntVec3>(
                ShoutTargeting.CellsInCone(p, targetCell, range, cone));

            List<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == null || other == p || other.Dead)
                {
                    continue;
                }
                // The assist target is NOT hostile - a wild animal never is - so without this it
                // would count as an ally standing in the cone and block the very breath aimed at
                // it. Adding the target to the breath's whitelist without adding it here too is a
                // self-cancelling change, and the sort that reads as "the breath still does not
                // work" rather than as a new bug.
                if (other.HostileTo(p) || other == assist)
                {
                    continue;
                }
                if (cells.Contains(other.Position))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The only ending. No corpse, no world-pawn entry, nothing left behind.
        /// Safe to call twice - every step is guarded.
        /// </summary>
        public static void VanishNow(Pawn p)
        {
            if (p == null)
            {
                return;
            }

            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg != null)
            {
                reg.NotifyAncientDragonbornGone(p);
            }

            if (p.Spawned && p.Map != null)
            {
                // A short dissipation so he does not simply blink out of existence.
                for (int i = 0; i < 12; i++)
                {
                    FleckMaker.ThrowDustPuffThick(
                        p.DrawPos + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.4f, 0.4f)),
                        p.Map, Rand.Range(1.2f, 2.0f), new Color(0.55f, 0.75f, 1f, 0.6f));
                }
            }

            // LEAVE THE PLAYER FACTION BEFORE BEING DESTROYED.
            //
            // Ideology treats a player-faction pawn as a colony MEMBER, so destroying him fires
            // Ideo.Notify_MemberCorpseDestroyed -> RitualObligationTrigger_MemberCorpseDestroyed,
            // which then dereferences null because he deliberately has no ideo. That threw a
            // red error every time a summon expired, in playtest, and the error is Ideology's
            // rather than ours - so the fix is to stop looking like a member, not to guard
            // something we do not own.
            //
            // Hediff_DeadPuppet already does this for the same class of reason, dropping
            // faction a tick before it kills the puppet.
            if (p.Faction != null && p.Faction.IsPlayer)
            {
                p.SetFaction(null, null);
            }

            // Anything he was carrying goes with him - a ghostly axe must not drop as loot.
            if (p.equipment != null)
            {
                p.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            }
            if (p.apparel != null)
            {
                p.apparel.DestroyAll(DestroyMode.Vanish);
            }
            if (p.inventory != null)
            {
                p.inventory.DestroyAll(DestroyMode.Vanish);
            }

            Corpse corpse = p.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }

            if (!p.Destroyed)
            {
                p.Destroy(DestroyMode.Vanish);
            }

            // THE SAVE-SIZE GUARD. Without this every summon ever made stays in the world pawn
            // list for the life of the colony. See the header.
            if (Find.WorldPawns != null && Find.WorldPawns.Contains(p))
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            // Should be unreachable: ShouldRemove is false and nothing else removes this. If it
            // ever happens the pawn is a permanent player-faction pseudo-colonist, which is
            // exactly what RISKS.md section 9 exists to prevent - so say so and vanish it.
            Pawn p = pawn;
            if (p != null && !p.Destroyed)
            {
                Log.Error("[Dovahkiin] Hediff_AncientDragonborn was removed from "
                    + p.LabelShortCap + " while it still existed. This should be impossible. "
                    + "Vanishing it rather than leaving a permanent summon. "
                    + "See RISKS.md section 9.");
                VanishNow(p);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 3750);
            // "usesFrost" is gone. An old save simply has no shoutCycle node, so it loads as 0
            // and he opens with Fire - harmless, and better than carrying a dead field forward.
            Scribe_Values.Look(ref shoutCycle, "shoutCycle", 0);
            Scribe_Values.Look(ref breathCooldown, "breathCooldown", 0);
        }
    }
}
