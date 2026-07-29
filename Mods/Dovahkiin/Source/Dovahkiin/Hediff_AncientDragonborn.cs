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

        /// <summary>False = Fire Breath, true = Frost Breath. Rolled once, at summon.</summary>
        private bool usesFrost;

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

        public bool UsesFrost
        {
            get { return usesFrost; }
        }

        public void Configure(int lifetimeTicks, bool frost)
        {
            ticksRemaining = Mathf.Max(60, lifetimeTicks);
            usesFrost = frost;
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
                if (breathCooldown <= 0)
                {
                    TryBreathe(p);
                }
                KeepNearDovahkiin(p);
            }
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
        private void TryBreathe(Pawn p)
        {
            DovahkiinTuningDef t = Tuning;
            float range = t != null ? t.ancientDragonbornBreathRange : 9f;
            float cone = t != null ? t.ancientDragonbornBreathCone : 46f;

            Pawn target = FindBreathTarget(p, range);
            if (target == null)
            {
                return;
            }
            if (!ConeIsClearOfAllies(p, target.Position, range, cone))
            {
                return;
            }

            Color head = usesFrost
                ? new Color(0.62f, 0.85f, 1f)
                : new Color(1f, 0.52f, 0.16f);
            FleckDef fleck = usesFrost
                ? DovahkiinDefOf.Dovahkiin_Fleck_ForceWave
                : DovahkiinDefOf.Dovahkiin_Fleck_FireWave;

            Thing_ShoutWave wave = Thing_ShoutWave.Spawn(
                p, target.Position, range, cone, head, fleck, 1.6f);
            if (wave == null)
            {
                return;
            }

            float dmg = t != null ? t.ancientDragonbornBreathDamage : 18f;
            int parts = t != null ? t.ancientDragonbornBreathInstances : 5;

            if (usesFrost)
            {
                // Mirrors Frost Breath's shape at a reduced strength: spread, chilling, with a
                // short stun, and no ignition.
                wave.SetPayload(DamageDefOf.Frostbite, dmg, 0f, 90, false, false,
                    null, 1f, parts, 0.35f, null, 1f, true, 0f, null, 0f, 0.35f);
            }
            else
            {
                // Fire: ignites pawns but NOT the ground. An ally-safe cone is still no reason to
                // start a fire the colony then has to put out, and Storm Call already established
                // that setting fire to a base is never acceptable collateral.
                wave.SetPayload(DamageDefOf.Flame, dmg, 0f, 0, true, false,
                    null, 1f, parts, 0f, null, 1f, false, 0.25f, null, 0f, 0.35f);
            }

            SoundDef snd = usesFrost
                ? DovahkiinVanillaDefOf.PsychicSoothePulserCast
                : DovahkiinVanillaDefOf.Explosion_Flame;
            if (snd != null)
            {
                snd.PlayOneShot(new TargetInfo(p.Position, p.Map, false));
            }

            breathCooldown = t != null ? t.ancientDragonbornBreathCooldownTicks : 420;
        }

        private static Pawn FindBreathTarget(Pawn p, float range)
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
                if (!other.HostileTo(p))
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
        private static bool ConeIsClearOfAllies(Pawn p, IntVec3 targetCell, float range, float cone)
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
                if (other.HostileTo(p))
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
            Scribe_Values.Look(ref usesFrost, "usesFrost", false);
            Scribe_Values.Look(ref breathCooldown, "breathCooldown", 0);
        }
    }
}
