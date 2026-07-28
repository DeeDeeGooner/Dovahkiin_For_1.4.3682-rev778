// Implements: SPEC.md 4.4e - Storm Call (Strun Bah Qo).
//
// ============================================================================================
// THE OUTDOOR RULE IS A HARD REQUIREMENT, NOT A HEURISTIC
// ============================================================================================
// SPEC.md 4.4e: a cell is a legal strike target only if ALL THREE hold.
//
//   1. it contains a pawn hostile to the player, AND
//   2. that pawn is not a colonist, not player-faction, not tamed, not a neutral visitor, AND
//   3. the cell is UNROOFED - open sky, no roof of any kind above it.
//
// Rule 3 is what makes the shout useless indoors, which is thematically right for calling a
// storm, and it is also what SETTLES THE FIRE QUESTION the spec previously left open: strikes
// cannot land inside a base, so they cannot ignite a stockpile, a wooden wall or a roofed
// corridor. Ignition on open outdoor terrain near enemies is acceptable and is left on.
//
// SPEC.md is explicit: "Do not cut this shout, and do not reintroduce indoor targeting for
// coverage." If a strike ever lands under a roof, that is a bug, not a tuning problem.
//
// Targets are chosen by walking the map's PAWN list and filtering, never by scanning cells.
// At radius 25 a radial cell scan is ~1,960 cells per strike; the pawn list is a few dozen
// entries. CLAUDE.md forbids avoidable per-tick cost and RocketMan is installed.
// ============================================================================================
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Dovahkiin
{
    /// <summary>
    /// The storm itself: a Thing that ticks only while the shout is active, firing one strike
    /// per interval at a freshly chosen legal target, then vanishing.
    ///
    /// Targets are re-evaluated for EVERY strike rather than picked once up front, because
    /// pawns move, die, and walk indoors mid-storm. A list captured on cast would keep striking
    /// corpses and pawns who have since taken cover under a roof.
    /// </summary>
    public class Thing_StormCall : Thing
    {
        private Pawn caster;
        private int ticksLeft;
        private int strikesLeft;
        private int intervalTicks = 30;
        private float radius = 25f;
        private int strikesLanded;
        private int ticksToNext;

        // Reused between strikes so the storm does not allocate a list per bolt.
        private static readonly List<Pawn> legalTargets = new List<Pawn>();

        public static Thing_StormCall Spawn(Pawn caster, IntVec3 centre, float radius,
            int strikes, int durationTicks)
        {
            if (caster == null || caster.Map == null
                || DovahkiinDefOf.Dovahkiin_StormCall == null || strikes <= 0)
            {
                return null;
            }
            Thing_StormCall storm = (Thing_StormCall)ThingMaker.MakeThing(
                DovahkiinDefOf.Dovahkiin_StormCall);
            storm.caster = caster;
            storm.radius = radius;
            storm.strikesLeft = strikes;
            storm.ticksLeft = Mathf.Max(30, durationTicks);
            // Spread the strikes evenly across the duration. The first lands almost at once so
            // the shout feels immediate rather than delayed.
            storm.intervalTicks = Mathf.Max(6, storm.ticksLeft / strikes);
            storm.ticksToNext = 6;
            GenSpawn.Spawn(storm, centre, caster.Map);
            return storm;
        }

        /// <summary>
        /// SPEC.md 4.4e's three rules, in one place. Every one of them is a hard exclusion.
        /// </summary>
        private bool IsLegalTarget(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p == caster || p.Map != Map)
            {
                return false;
            }

            // Rule 2, first half: never the player's own. Colonists, tamed animals and player
            // mechs are all Faction.OfPlayer, so this one test covers all of them.
            if (p.Faction != null && p.Faction.IsPlayer)
            {
                return false;
            }

            // Rule 1 and the rest of rule 2: must be actively hostile. This excludes neutral
            // visitors, traders and passive wildlife, while still catching manhunter animals,
            // which are hostile but factionless.
            if (!p.HostileTo(caster))
            {
                return false;
            }

            // Rule 3: OPEN SKY ONLY. The load-bearing rule - see the header.
            if (p.Position.Roofed(Map))
            {
                return false;
            }

            return (p.Position - Position).LengthHorizontalSquared <= radius * radius;
        }

        private void TryStrike()
        {
            legalTargets.Clear();
            List<Pawn> all = Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsLegalTarget(all[i]))
                {
                    legalTargets.Add(all[i]);
                }
            }
            if (legalTargets.Count == 0)
            {
                // No legal target this instant. The strike is NOT spent - everyone may simply be
                // under a roof right now, and stepping back into the open should draw a bolt.
                return;
            }

            Pawn victim = legalTargets[Rand.Range(0, legalTargets.Count)];
            IntVec3 cell = victim.Position;

            // Final belt-and-braces check. The roof could not have changed since IsLegalTarget,
            // but this is the invariant SPEC.md 4.4e says must never be violated, and it costs
            // one grid lookup on a rare event.
            if (cell.Roofed(Map))
            {
                return;
            }

            Map.weatherManager.eventHandler.AddEvent(
                new WeatherEvent_LightningStrike(Map, cell));
            strikesLeft--;
            strikesLanded++;
            legalTargets.Clear();
        }

        public override void Tick()
        {
            if (caster == null || Map == null)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            ticksLeft--;
            if (ticksLeft <= 0 || strikesLeft <= 0)
            {
                Finish();
                return;
            }

            ticksToNext--;
            if (ticksToNext > 0)
            {
                return;
            }
            ticksToNext = intervalTicks;
            TryStrike();
        }

        /// <summary>
        /// Tell the player when the storm found nothing. Without this, casting Storm Call inside
        /// a base looks identical to a broken shout - and the whole point of the outdoor rule is
        /// that it SHOULD do nothing there, which the player has no other way to learn.
        /// </summary>
        private void Finish()
        {
            if (strikesLanded == 0 && caster != null && caster.Faction != null
                && caster.Faction.IsPlayer)
            {
                Messages.Message(
                    "Dovahkiin_StormCall_NoTargets".Translate(),
                    caster, MessageTypeDefOf.RejectInput, false);
            }
            Destroy(DestroyMode.Vanish);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Scribe_References, never Scribe_Deep: the caster lives elsewhere in the save and
            // must not be duplicated into this Thing.
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
            Scribe_Values.Look(ref strikesLeft, "strikesLeft", 0);
            Scribe_Values.Look(ref intervalTicks, "intervalTicks", 30);
            Scribe_Values.Look(ref ticksToNext, "ticksToNext", 0);
            Scribe_Values.Look(ref radius, "radius", 25f);
            Scribe_Values.Look(ref strikesLanded, "strikesLanded", 0);
        }
    }
}
