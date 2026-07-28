// Implements: SPEC.md 4.4f - Soul Tear's dead puppet. RISKS.md section 9.
//
// ============================================================================================
// THE PUPPET IS ALWAYS DOOMED, AND THAT IS WHAT MAKES IT SAFE
// ============================================================================================
// RISKS.md section 9 called the original design the highest save-corruption risk in the mod.
// That design moved a hostile pawn into the player faction and RESTORED it afterwards, which
// required a correct restore-or-kill on seven exit paths - one of them save -> load. A puppet
// surviving a reload while still player-faction with its hediff gone is an unremovable
// pseudo-colonist that cannot be arrested, banished or killed cleanly.
//
// The adopted design removes the restore path entirely. A puppet never goes back to what it
// was, because it never survives:
//
//   1. It joins the player faction and gets this hediff: incurable, untendable, NON-REMOVABLE,
//      with a fixed lifetime.
//   2. When the lifetime expires, this hediff KILLS IT. There is no other outcome.
//   3. Every exit path therefore already terminates - timer expiry kills it, being killed early
//      is already death, being downed leaves the hediff ticking so it still dies, leaving the
//      map carries the hediff along, and the caster dying changes nothing because the puppet's
//      death does not depend on the caster.
//   4. Save -> load is safe BY CONSTRUCTION: an ordinary hediff serialises through RimWorld's
//      normal, well-tested path and keeps ticking. There is no bespoke state to lose.
//
// Do not add a way to remove, cure, tend or restore this. The absence of those is the design.
// ============================================================================================
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public class Hediff_DeadPuppet : HediffWithComps
    {
        private int ticksRemaining = 60000;
        private int ticksToGlow;

        /// <summary>
        /// NEVER removable. SPEC.md 4.4f: the puppet cannot be healed, cured or tended out of
        /// this state. The default implementation would drop the hediff at severity <= 0, which
        /// would strand a player-faction pawn with no timer - precisely the broken pawn that
        /// RISKS.md section 9 exists to prevent.
        /// </summary>
        public override bool ShouldRemove
        {
            get { return false; }
        }

        public override string LabelInBrackets
        {
            get
            {
                return ticksRemaining.ToStringTicksToPeriod(true, false, true, true, false);
            }
        }

        /// <summary>Seconds left, for the pawn inspect line. See Patch_Pawn_GetInspectString.</summary>
        public int TicksRemaining
        {
            get { return ticksRemaining; }
        }

        public void SetLifetime(int ticks)
        {
            ticksRemaining = Mathf.Max(60, ticks);
        }

        public override void Tick()
        {
            base.Tick();
            Pawn p = pawn;
            if (p == null || p.Dead)
            {
                return;
            }

            // The visible mark. SPEC.md 4.4f requires the player never mistake a puppet for a
            // real ally. AttachedOverlay rides the pawn's own draw position and touches nothing
            // in the render pipeline, so it cannot collide with RocketMan.
            if (DovahkiinDefOf.Dovahkiin_Fleck_PuppetGlow != null)
            {
                ticksToGlow--;
                if (ticksToGlow <= 0)
                {
                    ticksToGlow = 50;
                    FleckMaker.AttachedOverlay(p, DovahkiinDefOf.Dovahkiin_Fleck_PuppetGlow,
                        Vector3.zero, 1.15f, -1f);
                }
            }

            ticksRemaining--;

            // The faction is dropped ONE TICK BEFORE the kill, deliberately, for two reasons:
            //   - SPEC.md 4.4f: the puppet's death must not trigger colonist-death mood. A pawn
            //     that is no longer player-faction when it dies raises no such thought.
            //   - it splits two mutations of the pawn across separate ticks rather than doing
            //     both while the health tracker is mid-iteration.
            if (ticksRemaining == 1)
            {
                if (p.Faction != null && p.Faction.IsPlayer)
                {
                    p.SetFaction(null, null);
                }
                return;
            }

            if (ticksRemaining <= 0)
            {
                Expire(p);
            }
        }

        private void Expire(Pawn p)
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg != null)
            {
                reg.NotifyPuppetGone(p);
            }
            // exactCulprit is this hediff, so the death reason reads correctly rather than
            // appearing as an unexplained collapse.
            p.Kill(null, this);

            // Belt and braces on the mood rule: even though the pawn left the player faction a
            // tick ago, scrub any death thoughts that may have been raised about it.
            PawnDiedOrDownedThoughtsUtility.RemoveDiedThoughts(p);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            // Should be unreachable - ShouldRemove is false and nothing else removes this. If it
            // ever happens, the pawn is exactly the broken pseudo-colonist RISKS.md section 9
            // warns about, so say so loudly rather than leaving it walking around.
            Pawn p = pawn;
            if (p != null && !p.Dead && p.Faction != null && p.Faction.IsPlayer)
            {
                Log.Error("[Dovahkiin] Hediff_DeadPuppet was removed from " + p.LabelShortCap
                    + " while it was still alive and player-faction. This should be impossible. "
                    + "Killing it to avoid leaving an unremovable pseudo-colonist. "
                    + "See RISKS.md section 9.");
                p.SetFaction(null, null);
                p.Kill(null, null);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 60000);
        }
    }
}
