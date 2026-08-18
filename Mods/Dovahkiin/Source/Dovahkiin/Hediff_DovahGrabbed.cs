// THE GRAB. A dovah catches a pawn in its maw, shakes it, and throws it away.
//
// The user, 2026-08-04: "a very very small chance of triggering his iconic bite down, shake
// left right then throw away move, which would result in the pawn being downed while suffering
// both slash and piercing damage - the pawn or creature would be locked on the dragon while the
// dragon alternates between east and west view for a bit, then the victim is projected away."
//
// ============================================================================================
// WHY THE STATE LIVES ON THE VICTIM
// ============================================================================================
// This is a multi-second scripted sequence involving two pawns, and RISKS.md 9 puts exactly
// that kind of bespoke cross-save state at the top of the corruption risks. A hediff on the
// VICTIM is the safest home available:
//
//   - it is saved and restored by the game, with no bookkeeping of ours
//   - if the game is saved mid-grab and reloaded, it simply continues
//   - it CANNOT strand the victim: the countdown runs regardless, and every exit path releases
//   - if the DRAGON dies, despawns or vanishes mid-shake, the next tick sees a broken grabber
//     and releases immediately rather than holding the victim forever
//
// The one thing a grapple must never do is leave a pawn permanently frozen. Every branch below
// ends in Release().
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public class Hediff_DovahGrabbed : HediffWithComps
    {
        private Pawn grabber;
        private int ticksLeft;
        private int shakeTimer;
        private bool facingEast;

        /// <summary>
        /// Where the victim is held: a cell ADJACENT to the dragon, never his own. Chosen once
        /// and kept, so the shake's east/west flipping does not drag the victim back and forth.
        /// See the long note in Tick - putting the victim in the dragon's cell is what made the
        /// engine shunt him across the map.
        /// </summary>
        private IntVec3 holdCell = IntVec3.Invalid;
        private bool holdCellSet;

        /// <summary>
        /// A cell ADJACENT to the dragon on the side he is currently facing, so the victim swings
        /// with the shake. Prefers the exact east/west neighbour; failing that, any usable
        /// neighbour on that half; failing that, any usable neighbour at all.
        ///
        /// Returns IntVec3.Invalid if nothing works, in which case the victim is left where they
        /// are - they are stunned, so they cannot walk off regardless. **It must never return the
        /// dragon's own cell**; see the note in Tick for what that costs.
        /// </summary>
        private static IntVec3 FindHoldCell(Pawn dragon, Pawn victim, bool east)
        {
            Map map = dragon.Map;
            if (map == null)
            {
                return IntVec3.Invalid;
            }
            IntVec3 origin = dragon.Position;

            // First choice: straight out to the side he is facing - the jaws.
            IntVec3 preferred = origin + (east ? IntVec3.East : IntVec3.West);
            if (Usable(preferred, map, victim))
            {
                return preferred;
            }

            IntVec3 best = IntVec3.Invalid;
            float bestScore = float.MaxValue;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 c = origin + GenAdj.AdjacentCells[i];
                if (!Usable(c, map, victim))
                {
                    continue;
                }
                // Prefer the facing half, then whatever is nearest to the victim, so a blocked
                // side degrades to something sensible instead of teleporting them behind him.
                bool onFacingSide = east ? (c.x >= origin.x) : (c.x <= origin.x);
                float score = (c - victim.Position).LengthHorizontalSquared + (onFacingSide ? 0f : 100f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        private static bool Usable(IntVec3 c, Map map, Pawn victim)
        {
            if (!c.InBounds(map) || !c.Standable(map))
            {
                return false;
            }
            // Do not stack the victim onto a third pawn - that would hand the SAME collision
            // shunt to whoever is standing there.
            return PawnUtility.PawnBlockingPathAt(c, victim) == null;
        }

        /// <summary>Which dovah is holding this pawn. Null once released.</summary>
        public Pawn GrabbedBy { get { return grabber; } }

        public void Begin(Pawn dragon, int durationTicks)
        {
            grabber = dragon;
            ticksLeft = durationTicks;
        }

        public override void Tick()
        {
            // HediffWithComps.Tick IS a real implementation, unlike Verse.Thing's throwing
            // stubs - calling base here is required, not optional.
            base.Tick();

            Pawn victim = pawn;
            if (victim == null || victim.Dead || !victim.Spawned)
            {
                return;
            }

            // The grabber is gone - dead, despawned, or never restored. Let go at once rather
            // than holding a pawn hostage to a dragon that no longer exists.
            if (grabber == null || grabber.Destroyed || !grabber.Spawned || grabber.Dead)
            {
                Release(victim, false);
                return;
            }

            // ================= PIN THE DRAGON =================
            // WITHOUT THIS HE WALKS OFF WITH THE VICTIM IN HIS JAWS. The user, twice:
            // "he flew to a pawn, grabbed him, and kept flying straight - I just witnessed a live
            // kidnapping", then again after the first fix: "he still kidnaps pawns and fly them
            // away... He is supposed to be static while he does the bite and throw."
            //
            // ⚠ THE FIRST FIX DID NOT STOP MOVEMENT AND COULD NOT HAVE. Both of its halves were
            // aimed at the wrong thing, which is why raising or lowering anything would never
            // have helped:
            //
            //   StopDead()          clears curPath / moving / nextCell and NOTHING ELSE. It does
            //                       not touch the JOB, so the job driver simply paths again on
            //                       its next tick. Worse, this hediff lives on the VICTIM, so it
            //                       runs during the VICTIM's health.HealthTick() - a different
            //                       pawn's tick entirely - and the dragon's own Pawn.Tick() then
            //                       runs pather.PatherTick() and jobs.JobTrackerTick() as usual.
            //   HoldGroundedUntil() only constrains which MOVEMENT STATE the machine picks. A
            //                       GROUNDED dragon still walks. It was never a movement stop,
            //                       though its own doc comment claimed to be one.
            //
            // THE REAL GATE IS THE ENGINE'S, AND IT IS ONE LINE. Verse.AI.Pawn_PathFollower
            // .PatherTick() opens with "if (pawn.stances.FullBodyBusy) return;", and
            // Pawn_StanceTracker.FullBodyBusy returns true whenever stunner.Stunned. So a stunned
            // pawn CANNOT move - enforced by RimWorld, not by us fighting the job system.
            //
            // RimWorld.StunHandler.StunFor only sets stunTicksLeft (plus an optional battle-log
            // entry and mote flag); it does NOT stop or clear jobs. So the dragon keeps the job
            // he had and resumes it the moment the grab ends.
            //
            // This is the same treatment the VICTIM already gets a few lines below - which is the
            // tell that it was the right tool all along. Re-stunned every tick so it can never
            // outlive the grab.
            if (grabber.stances != null && grabber.stances.stunner != null)
            {
                grabber.stances.stunner.StunFor(10, victim, false, false);
            }
            // Still clear the path: without this he resumes the stale path to his old destination
            // the instant the stun lapses. This is cleanup, NOT the pin.
            if (grabber.pather != null)
            {
                grabber.pather.StopDead();
            }
            // And hold the STATE at grounded, so the machine cannot decide mid-shake that he
            // ought to be flying and swap his sprite out from under the animation.
            Comp_AlduinFlight flight = grabber.TryGetComp<Comp_AlduinFlight>();
            if (flight != null)
            {
                flight.HoldGroundedUntil(Find.TickManager.TicksGame + 30);
            }

            // HELD. Stunned every tick so he cannot act. Re-stunning each tick rather than once
            // for the whole duration means the stun can never outlive the grab.
            if (victim.stances != null && victim.stances.stunner != null)
            {
                victim.stances.stunner.StunFor(10, grabber, false, false);
            }

            // THE SHAKE. Alternating the dragon's facing is the whole animation - RimWorld has
            // no bespoke animation system reachable from here, and flipping east/west reads
            // exactly as a beast worrying something in its jaws.
            //
            // ⚠ SETTING pawn.Rotation DOES NOT WORK AND SHOWED NOTHING IN PLAY. The user:
            // "no east-west view change nor pawn localisation change". Verse.Pawn_RotationTracker
            // .UpdateRotation() runs from Pawn.ProcessPostTickVisuals - a SEPARATE PASS AFTER the
            // whole tick - and while the dragon has a job it calls FaceTarget() on that job's
            // target, so any Rotation we assign is overwritten before a single frame is drawn.
            // There is no point in the tick where we could win that race.
            //
            // So the flip is done with the OCTANT TRICK instead: a Graphic_Single returns the
            // same material for every Rot4, which makes the engine's facing irrelevant. Same
            // mechanism as the eight-way flight facing, and already proven in play.
            shakeTimer--;
            if (shakeTimer <= 0)
            {
                DovahkiinTuningDef t = DovahkiinTuningDef.Current;
                shakeTimer = t != null ? t.dovahGrabShakeIntervalTicks : 12;
                facingEast = !facingEast;
                if (flight != null && AlduinGraphicsUtility.IsAlduin(grabber))
                {
                    // Routed through the COMP, not straight at the graphics utility, so the
                    // dragon owns putting his own sprite back - see NotifyShaking's note. The
                    // window outlives one shake interval so an ordinary flip never lets it lapse.
                    flight.NotifyShaking(facingEast, Find.TickManager.TicksGame + shakeTimer + 30);
                }
                else
                {
                    // Not one of ours. Rotation is the best available fallback for any other
                    // creature that grows a maw later - it will be overwritten by the rotation
                    // tracker whenever that creature has a job, but it costs nothing to try.
                    grabber.Rotation = facingEast ? Rot4.East : Rot4.West;
                }
                // The victim swings with him. Recomputed on the flip, never every tick, so it
                // reads as being whipped side to side rather than as jitter.
                holdCellSet = false;
            }

            // ⚠⚠ NEVER PUT THE VICTIM IN THE DRAGON'S OWN CELL. THAT WAS THE KIDNAPPING BUG, AND
            // IT SURVIVED TWO FIXES BECAUSE IT LOOKED LIKE THE DRAGON WAS DOING THE MOVING.
            //
            // This used to do "victim.Position = grabber.Position" every tick. The result is a
            // conveyor belt that drags the DRAGON across the map, built entirely out of the grab:
            //
            //   1. we teleport the victim into the dragon's cell
            //   2. the dragon's Pawn_PathFollower.PatherTick then sees
            //      WillCollideWithPawnAt(his own Position) == true, and takes its FIRST branch
            //   3. ⚠ THAT BRANCH DOES NOT CHECK stances.FullBodyBusy - the stun gate is only in
            //      the else. It does "pawn.Position = <a free neighbouring cell>" DIRECTLY, then
            //      ResetToCurrentPosition() and "if (moving && TrySetNewPath()) TryEnterNextPathCell()"
            //   4. so he is shunted one cell and put back on his journey, every single tick
            //
            // That is why StopDead() never helped (step 3 re-paths immediately afterwards) and
            // why stunning him never helped (step 3 never reads the stun). It also explains the
            // user's "chaotic mixt with disappearing kidnapping trajectory" - teleport, shunt,
            // teleport, shunt - and their observation that the victim "survived and was down":
            // PawnUtility.PawnBlockingPathAt SKIPS DOWNED PAWNS, so the conveyor stops itself the
            // moment the victim goes down.
            //
            // A standing victim always collides here: ShouldCollideWithPawns is true for anyone
            // not downed/dead with hostiles nearby (a stun does not exempt them), and
            // PawnsCanShareCellBecauseOfBodySize returns FALSE outright when either pawn is
            // BodySize >= 1.5 - the dragon is 4.6.
            //
            // So the victim is held ADJACENT instead. At drawSize 4.6 the dragon covers roughly
            // four cells, so a neighbouring cell is still visually inside his sprite and reads
            // exactly as "in the jaws" - while the engine sees an empty cell under him and leaves
            // him alone.
            //
            // Recomputed only when the flip invalidates it, or when the cell stops being usable.
            if (!holdCellSet || !holdCell.IsValid || !holdCell.InBounds(victim.Map)
                || !holdCell.AdjacentTo8WayOrInside(grabber))
            {
                holdCell = FindHoldCell(grabber, victim, facingEast);
                holdCellSet = holdCell.IsValid;
            }
            if (holdCellSet && victim.Position != holdCell)
            {
                victim.Position = holdCell;
                victim.Notify_Teleported(false, false);
            }

            ticksLeft--;
            if (ticksLeft <= 0)
            {
                Release(victim, true);
            }
        }

        /// <summary>
        /// End the grab. <paramref name="completed"/> false means the dragon was lost partway -
        /// the victim is simply let go, unharmed by the finisher he never received.
        /// </summary>
        private void Release(Pawn victim, bool completed)
        {
            Pawn dragon = grabber;
            // Removed FIRST, so nothing below can re-enter this or leave the hediff attached if
            // a later step throws.
            victim.health.RemoveHediff(this);

            // ⚠ PUT HIS NORMAL SPRITE BACK, ON EVERY EXIT PATH INCLUDING THE ABORTS BELOW.
            // The shake installs a Graphic_SINGLE profile, which by design ignores Rot4 - so if
            // it is left on he is frozen in profile for the rest of his life, facing one way
            // whichever direction he walks. The movement state machine cannot be relied on to
            // undo it either: SetState returns early when the STATE has not changed, and he is
            // held Grounded across the whole grab, so a released dragon could stay Grounded and
            // never re-swap.
            if (dragon != null && !dragon.Destroyed)
            {
                Comp_AlduinFlight df = dragon.TryGetComp<Comp_AlduinFlight>();
                if (df != null)
                {
                    // Immediate, rather than waiting for the comp's own timeout to notice.
                    df.EndShakeProfile();
                }
            }

            if (!completed || dragon == null || !dragon.Spawned)
            {
                return;
            }

            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            float slash = t != null ? t.dovahGrabSlashDamage : 22f;
            float pierce = t != null ? t.dovahGrabPierceDamage : 18f;
            float throwCells = t != null ? t.dovahGrabThrowCells : 7f;

            // BOTH damage types, per the user: teeth that both puncture and tear.
            if (slash > 0f)
            {
                victim.TakeDamage(new DamageInfo(DamageDefOf.Cut, slash, 0.6f, -1f, dragon));
            }
            if (pierce > 0f && !victim.Dead)
            {
                victim.TakeDamage(new DamageInfo(DamageDefOf.Stab, pierce, 0.6f, -1f, dragon));
            }

            // DOWNED, as specified. The damage above may not manage it on a heavily armoured
            // target, and "usually downed" is not what was asked for - so vanilla's own helper
            // finishes the job. It is what the game uses for scripted downings and it will not
            // kill: HealthUtility.DamageUntilDowned sets health.forceDowned around the wounds
            // it inflicts.
            if (!victim.Dead && !victim.Downed)
            {
                HealthUtility.DamageUntilDowned(victim, false);
            }

            // THROWN. Reuses the shout knockback rather than a PawnFlyer: the notebook records
            // a pawn being DESTROYED WITH NO CORPSE when a flyer was started from the wrong
            // place, and a rare surprise move is the worst possible place to risk that.
            if (!victim.Dead)
            {
                ShoutTargeting.Knockback(victim, dragon.Position, throwCells);
            }

            FleckMaker.ThrowDustPuffThick(victim.DrawPos, victim.Map, 3.2f,
                new Color(0.7f, 0.62f, 0.55f, 0.8f));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref grabber, "dovahGrabber");
            Scribe_Values.Look(ref ticksLeft, "dovahGrabTicksLeft", 0);
            Scribe_Values.Look(ref shakeTimer, "dovahGrabShakeTimer", 0);
            Scribe_Values.Look(ref facingEast, "dovahGrabFacingEast", false);
            Scribe_Values.Look(ref holdCell, "dovahGrabHoldCell", IntVec3.Invalid);
            Scribe_Values.Look(ref holdCellSet, "dovahGrabHoldCellSet", false);
        }

        public override string LabelInBrackets
        {
            get { return "in its jaws"; }
        }
    }
}

