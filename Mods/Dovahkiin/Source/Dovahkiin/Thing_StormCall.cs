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

        // Sticky across the whole storm, so the "found nothing" message can name the ACTUAL
        // reason rather than a generic one. Playtest reported "no targets" while enemies were
        // visibly outdoors, and the old message gave no way to tell which rule had rejected
        // them - out of range, or under a roof.
        private bool sawRoofedHostile;
        private bool sawOutOfRangeHostile;

        // Instance, NOT static. A static scratch list is shared between concurrent storms, and
        // two Storm Calls can overlap - the cooldown is shared but a second Dovahkiin, or a
        // save loaded mid-storm, can produce two live instances.
        private readonly List<Pawn> legalTargets = new List<Pawn>();

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
        /// Where the storm is centred RIGHT NOW.
        ///
        /// The caster's current position, not the cell the storm spawned in. In TES5 the storm
        /// follows the Dragonborn; here that also removes a real failure mode found in playtest,
        /// where walking away after casting silently pulled enemies out of range while they were
        /// still plainly visible and outdoors.
        ///
        /// Falls back to the storm's own cell if the caster has died or despawned mid-storm.
        /// </summary>
        private IntVec3 Centre
        {
            get
            {
                return (caster != null && caster.Spawned && caster.Map == Map)
                    ? caster.Position
                    : Position;
            }
        }

        /// <summary>
        /// SPEC.md 4.4e's three rules, in one place, plus range.
        ///
        /// The two rules that can legitimately reject an otherwise valid enemy - range and roof -
        /// are counted separately, so the end-of-storm message can say which one applied.
        /// </summary>
        private void ScanTargets()
        {
            legalTargets.Clear();
            IntVec3 centre = Centre;
            float radiusSq = radius * radius;

            List<Pawn> all = Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p == null || p.Dead || !p.Spawned || p == caster || p.Map != Map)
                {
                    continue;
                }

                // Rule 2, first half: never the player's own. Colonists, tamed animals and
                // player mechs are all Faction.OfPlayer, so this one test covers all of them.
                if (p.Faction != null && p.Faction.IsPlayer)
                {
                    continue;
                }

                // Rule 1 and the rest of rule 2: must be actively hostile. This excludes neutral
                // visitors, traders and passive wildlife, while still catching manhunter
                // animals, which are hostile but factionless.
                if (!p.HostileTo(caster))
                {
                    continue;
                }

                // From here on it IS a valid enemy, so a rejection is worth reporting.
                if ((p.Position - centre).LengthHorizontalSquared > radiusSq)
                {
                    sawOutOfRangeHostile = true;
                    continue;
                }

                // Rule 3: OPEN SKY ONLY. The load-bearing rule - see the header.
                if (p.Position.Roofed(Map))
                {
                    sawRoofedHostile = true;
                    continue;
                }

                legalTargets.Add(p);
            }
        }

        private void TryStrike()
        {
            ScanTargets();
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
        /// Tell the player when the storm found nothing, AND WHY.
        ///
        /// Without a message at all, casting Storm Call inside a base looks identical to a
        /// broken shout - and doing nothing there is the whole point of the outdoor rule, which
        /// the player has no other way to learn.
        ///
        /// But a single generic message is not enough either: playtest reported "no targets"
        /// while enemies were visibly outdoors, and there was no way to tell whether they had
        /// been rejected for being roofed or simply for being out of range. Naming the actual
        /// rule turns a confusing non-event into an explanation.
        /// </summary>
        private void Finish()
        {
            if (strikesLanded == 0 && caster != null && caster.Faction != null
                && caster.Faction.IsPlayer)
            {
                string key;
                if (sawRoofedHostile)
                {
                    key = "Dovahkiin_StormCall_AllRoofed";
                }
                else if (sawOutOfRangeHostile)
                {
                    key = "Dovahkiin_StormCall_OutOfRange";
                }
                else
                {
                    key = "Dovahkiin_StormCall_NoEnemies";
                }
                Messages.Message(key.Translate(), caster, MessageTypeDefOf.RejectInput, false);
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
            Scribe_Values.Look(ref sawRoofedHostile, "sawRoofedHostile", false);
            Scribe_Values.Look(ref sawOutOfRangeHostile, "sawOutOfRangeHostile", false);
        }
    }
}
