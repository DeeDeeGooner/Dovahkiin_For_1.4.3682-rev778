// The dragon's movement state machine, and the facing that goes with it. SPEC.md 6.5.
//
// The user's design 2026-08-03, specified 2026-08-04: GROUNDED / SOAR / FLIGHT, "for more
// attack patterns and game dynamics". This decides which one he is in, keeps his sprite
// pointed the right way, and applies the speed for that state.
//
// ============================================================================================
// WHAT THIS DELIBERATELY DOES **NOT** DO YET
// ============================================================================================
// SPEC.md 6.5 also gives airborne dragons melee immunity, an inability to melee, and passage
// over roofs and walls (but never natural mountain rock). NONE of that is here. Each is a
// separate mechanism - a damage-path gate, a verb gate and a pathing override - and bolting
// them onto the state machine before the state machine itself has been seen working would make
// a failure impossible to attribute. That is the same reasoning that kept the Ancient
// Dragonborn's summon and his ability apart.
//
// Also absent: the WING DAMAGE grounding rule (SPEC.md 6.6 - 50% to each wing or 80% to one).
// It needs a Dovah BodyDef that does not exist yet; the test creature uses vanilla "Bird",
// which has no wings to check. The health-fraction rule below is live and is the half of the
// grounding rule that can be honoured today.
//
// ============================================================================================
// ON TICKING
// ============================================================================================
// CLAUDE.md forbids per-tick work that could live on an interval, and RocketMan is installed.
// Two different cadences are needed and they are kept apart:
//
//   FACING  - must track a pawn moving a cell every few ticks, so it is checked every tick,
//             but early-outs on an IntVec3 compare before doing any real work.
//   STATE   - reads health and distances, so it runs on dragonStateIntervalTicks (60).
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
// PlayOneShot is an EXTENSION METHOD on Verse.Sound.SoundStarter, not a member of SoundDef - so
// without this using the call does not resolve and the error names SoundDef, which reads like the
// method does not exist at all.
using Verse.Sound;

namespace Dovahkiin
{
    public class CompProperties_AlduinFlight : CompProperties
    {
        public CompProperties_AlduinFlight()
        {
            compClass = typeof(Comp_AlduinFlight);
        }
    }

    public class Comp_AlduinFlight : ThingComp
    {
        // Not saved: both are pure render/decision detail, recomputed on the first tick after a
        // load. Nothing here is state the save needs to carry - which is deliberate, because
        // RISKS.md 9 puts bespoke cross-save state at the top of the corruption risks.
        // (lastNextCell removed 2026-08-18 - it throttled the flight facing on `nextCell` while
        //  the facing is measured six cells out. See the note in the facing block below.)
        // (nextStateCheckTick removed 2026-08-06 with the poll it drove - the pattern executor
        // now runs every tick. A poll is the dwell timer's mistake in slower form.)
        private AlduinMovementState currentState = AlduinMovementState.Grounded;

        /// <summary>
        /// When the current state may next be left. THE COOLDOWN the user asked for after the
        /// first playtest, and the fix for what he saw: without it the state flickered every
        /// time he started or stopped wandering, because idle-and-moving is flight and
        /// idle-and-stationary is grounded. That flicker is what read as "using flight a bit
        /// too often".
        /// </summary>
        private int stateLockedUntilTick;

        /// <summary>
        /// Tick he stopped moving while in flight, or -1 while he is moving. Drives the
        /// no-hovering rule. Not saved: a reload simply re-observes it within a second.
        /// </summary>
        private int stationarySinceTick = -1;

        /// <summary>
        /// A GUARDIAN sits grounded and motionless on its mound until something comes close
        /// enough to trigger it. SPEC.md 6.5c - one of only three ways a dovah is ever met.
        ///
        /// This one IS saved: it is a property of the encounter the world generated, not a
        /// transient render detail, and a guardian that took off after a reload would be a
        /// different creature.
        /// </summary>
        public bool isGuardian;

        /// <summary>
        /// While this tick is in the future the dragon's movement STATE is pinned to Grounded.
        /// Set by Hediff_DovahGrabbed so the state machine cannot swap his sprite to flight
        /// mid-shake.
        ///
        /// ⚠ THIS DOES NOT STOP HIM MOVING, and an earlier version of this comment claimed it
        /// did - which is how the kidnapping bug survived its first fix. A GROUNDED dragon walks.
        /// The thing that actually pins him is the STUN applied in Hediff_DovahGrabbed.Tick,
        /// because Pawn_PathFollower.PatherTick early-returns on stances.FullBodyBusy. Do not
        /// treat this flag as a movement lock.
        /// </summary>
        private int groundedHoldUntilTick;

        public void HoldGroundedUntil(int tick)
        {
            if (tick > groundedHoldUntilTick)
            {
                groundedHoldUntilTick = tick;
            }
        }

        /// <summary>
        /// IS HE OFF THE GROUND? Read by the hot-path patches in Patch_DragonAirborne.cs, which is
        /// why it is a public property over a field rather than something they reach in and read.
        /// Soar counts: SPEC.md 6.5 gives soar and flight the same immunities.
        /// </summary>
        public bool IsAirborne
        {
            get { return currentState != AlduinMovementState.Grounded; }
        }

        /// <summary>
        /// While this tick is in the future a SHAKE PROFILE sprite is installed - a
        /// Graphic_Single, which by design ignores Rot4. Zero means none is.
        /// </summary>
        private int shakeUntilTick;

        /// <summary>
        /// While this tick is in the future he is hanging motionless in SOAR, having just broken
        /// off. When it passes he peels away into a circling flight. Zero means not disengaging.
        /// Saved: a game reloaded mid-pause would otherwise leave him hovering for ever, since
        /// nothing else would ever move him on to flight.
        /// </summary>
        private int soarPauseUntilTick;

        /// <summary>
        /// Called by Hediff_DovahGrabbed on each flip of the shake. The DRAGON owns the undo, not
        /// the hediff, and that is deliberate: a hediff stops ticking the moment its pawn dies
        /// (Pawn_HealthTracker.HealthTick opens with "if (Dead) return"), so a victim who bleeds
        /// out mid-shake would otherwise leave him frozen in profile for the rest of his life.
        /// EnterState cannot rescue him either - it only fires on a state CHANGE, and he is
        /// pinned Grounded throughout.
        /// </summary>
        public void NotifyShaking(bool east, int untilTick)
        {
            Pawn pawn = parent as Pawn;
            if (!AlduinGraphicsUtility.IsAlduin(pawn))
            {
                return;
            }
            // SetShakeProfile returns false when the side has not changed; that is not a failure,
            // so the restore is armed either way.
            AlduinGraphicsUtility.SetShakeProfile(pawn, east);
            if (untilTick > shakeUntilTick)
            {
                shakeUntilTick = untilTick;
            }
        }

        /// <summary>
        /// While this tick is in the future his state is PINNED - he is mid-breath and the
        /// rhythm must not roll him out of the state the breath belongs to.
        /// </summary>
        private int stateHoldUntilTick;

        /// <summary>
        /// PERFORM A BREATH. State, facing and the breath itself, in that order.
        ///
        /// ⚠ THIS IS WHAT THE ATTACK-PATTERN EXECUTOR WILL CALL. It lives here rather than in the
        /// debug action because the three steps belong together: the user, 2026-08-05, on seeing
        /// a Pool breath fired at a grounded dragon - "he didn't soar nor looked toward the
        /// targeted cell. So make sure that the SOAR breath makes him look and soar."
        ///
        /// The shape decides the state, not the other way round: SPEC.md 4.6a gives soar a circle
        /// and grounded a cone, so firing a Pool breath means being in the air by definition.
        /// </summary>
        public Thing_DragonBreath BreatheAt(IntVec3 target, DragonBreathShape shape)
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }
            DovahkiinTuningDef t = Tuning;

            // ---- 1. THE STATE THE SHAPE BELONGS TO.
            AlduinMovementState wanted = shape == DragonBreathShape.Pool
                ? AlduinMovementState.Soar
                : AlduinMovementState.Grounded;
            if (currentState != wanted)
            {
                EnterState(pawn, wanted);
            }

            // ---- 2. REACH. Both shapes were unbounded in their own way and both were wrong.
            float coneRange = t != null ? t.dragonBreathConeRange : 24f;
            float aimed = pawn.Position.DistanceTo(target);

            if (shape == DragonBreathShape.Pool)
            {
                // Pool had NO range cap at all: its reach is simply the distance to the aimed
                // cell, so he could place a circle anywhere on the map.
                float maxReach = t != null ? t.dragonBreathSoarRange : 14f;
                if (aimed > maxReach && aimed > 0.01f)
                {
                    Vector3 dir = (target.ToVector3Shifted() - pawn.Position.ToVector3Shifted());
                    dir.y = 0f;
                    dir.Normalize();
                    IntVec3 clamped = (pawn.Position.ToVector3Shifted() + dir * maxReach).ToIntVec3();
                    if (clamped.InBounds(pawn.Map))
                    {
                        target = clamped;
                    }
                }
            }
            else
            {
                // Cone had the opposite fault: it ALWAYS spanned the full range, so the direction
                // followed the aim and the length never did. The user, 2026-08-05: "the grounded
                // breath is always spanning a fixed range, it's always the same cone."
                //
                // It now reaches the target and stops, plus a short overshoot so the jet washes
                // OVER whoever is aimed at instead of stopping dead at their feet. The overshoot
                // is a constant rather than a knob deliberately: it is a property of how a jet
                // looks, not a balance number, and a second range knob that has to agree with
                // this one is the trap this session keeps recording.
                const float ConeOvershootCells = 3f;
                // ⚠ 24f, MATCHING THE FIELD DEFAULT. It said 8f, and on 2026-08-13 that turned a
                // broken tuning def into a SECOND, invisible bug: with the def gone this fallback
                // shortened the jet to the target's distance and the user reported "the cone's
                // size was reduced, it is supposed to be constant". A fallback that disagrees with
                // its own field default is a different set of numbers waiting for a bad day.
                float minRange = t != null ? t.dragonBreathConeMinRange : 24f;
                coneRange = Mathf.Clamp(aimed + ConeOvershootCells, minRange, coneRange);
            }

            // ---- 3. LOOK AT IT. Safe here because a scripted breath runs on a pawn whose job
            // has no rotate-to-face target: Pawn_RotationTracker.FaceTarget returns immediately
            // on an invalid target, so this assignment survives. It would NOT survive on a pawn
            // mid-attack, which is why the pattern must own the job while a breath is running.
            IntVec3 delta = target - pawn.Position;
            if (delta != IntVec3.Zero)
            {
                pawn.Rotation = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                    ? (delta.x >= 0 ? Rot4.East : Rot4.West)
                    : (delta.z >= 0 ? Rot4.North : Rot4.South);
            }

            int duration = t != null ? t.dragonBreathDurationTicks : 263;
            Thing_DragonBreath breath = Thing_DragonBreath.Spawn(
                pawn, target, shape,
                coneRange,
                t != null ? t.dragonBreathConeAngle : 38f,
                t != null ? t.dragonBreathPoolRadius : 3.5f,
                duration,
                t != null ? t.dragonBreathPulseIntervalTicks : 20);
            if (breath == null)
            {
                return null;
            }

            // ---- PAYLOAD, LOOK AND SOUND.
            //
            // FIRE FOR NOW. SPEC.md 4.6 says each dragon kind knows exactly ONE element, so this
            // will come off the kind once there is more than one; until then the pattern executor
            // breathes fire and the debug action is how frost gets tested.
            breath.SetPayload(DamageDefOf.Flame,
                t != null ? t.dragonBreathDamage : 90.576f,
                t != null ? t.dragonBreathDamageInstances : 4,
                t != null ? t.dragonBreathArmorPenetration : 0.25515f,
                true, 0f, null, 1f);
            breath.SetLook(DovahkiinDefOf.Dovahkiin_Fleck_FireWave, 1.6f,
                new Color(1f, 0.62f, 0.24f));
            // Null is silent, never an error - GetNamedSilentFail returning null must not be able
            // to take the breath down with it.
            breath.SetSound(DefDatabase<SoundDef>.GetNamedSilentFail("Dovahkiin_DragonBreathFire"));
            if (t != null)
            {
                breath.SetFillGradient(t.dragonBreathFillColor, t.dragonBreathFillBright,
                    t.dragonBreathFillYellow, t.dragonBreathYellowStrength,
                    t.dragonBreathSoarCircleOpacity);
            }

            // Hold the state for the whole breath, or something else rolls him out of soar
            // mid-jet - a guardian in particular is returned to Grounded within a second.
            stateHoldUntilTick = Find.TickManager.TicksGame + duration;
            return breath;
        }

        /// <summary>Put the normal sprite set back at once. Safe to call when none is installed.</summary>
        public void EndShakeProfile()
        {
            if (shakeUntilTick == 0)
            {
                return;
            }
            shakeUntilTick = 0;
            Pawn pawn = parent as Pawn;
            if (pawn != null && !pawn.Destroyed)
            {
                AlduinGraphicsUtility.SetState(pawn, currentState);
            }
        }

        private bool IsHeldGrounded
        {
            get { return Find.TickManager.TicksGame < groundedHoldUntilTick; }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isGuardian, "dovahIsGuardian", false);
            // Saved, or a game saved mid-shake reloads with a Graphic_Single installed and
            // nothing left armed to take it off again.
            Scribe_Values.Look(ref shakeUntilTick, "dovahShakeUntilTick", 0);
            Scribe_Values.Look(ref soarPauseUntilTick, "dovahSoarPauseUntilTick", 0);
            Scribe_Values.Look(ref stateHoldUntilTick, "dovahStateHoldUntilTick", 0);
            // The pattern in progress. Saved so a reload does not drop him mid-attack into a
            // state nothing owns - and lastPattern too, or the no-repeat rule silently resets
            // across a save and he can dive twice running.
            Scribe_Values.Look(ref currentPattern, "dovahPattern", DragonAttackPattern.DiveAndBrawl);
            Scribe_Values.Look(ref lastPattern, "dovahLastPattern");
            Scribe_Values.Look(ref patternPhase, "dovahPatternPhase", DragonPatternPhase.Leave);
            Scribe_Values.Look(ref phaseUntilTick, "dovahPatternPhaseUntil", 0);
            Scribe_Values.Look(ref hasPattern, "dovahHasPattern", false);
            // The circling centre once the target is dead. Without this a reload mid-leg would
            // re-centre the circle on the dragon himself.
            Scribe_Values.Look(ref lastTargetPos, "dovahLastTargetPos", IntVec3.Invalid);
            // The circling audio. The alternation flag, or a reload replays the same take for ever;
            // the pending tick, or a save during the take-off delay drops the clip entirely.
            Scribe_Values.Look(ref circlingSoundAlternate, "dovahCirclingSoundAlt", false);
            Scribe_Values.Look(ref circlingSoundAtTick, "dovahCirclingSoundAt", 0);
            // The circuit itself. Without these a reload mid-leg re-centres on the dragon and
            // re-rolls the direction, which is a visible kink in the middle of a lap.
            Scribe_Values.Look(ref circleCentre, "dovahCircleCentre", IntVec3.Invalid);
            Scribe_Values.Look(ref circleClockwise, "dovahCircleClockwise", false);
            Scribe_Values.Look(ref circleAngle, "dovahCircleAngle", 0f);
            // The armed breath. Without these a save during the four-second wind-up loses the jet
            // and the pattern plays as "land, wait, leave".
            Scribe_Values.Look(ref pendingBreathTick, "dovahPendingBreathTick", 0);
            Scribe_Values.Look(ref pendingBreathShape, "dovahPendingBreathShape", DragonBreathShape.Cone);
        }

        /// <summary>
        /// The landing that fires the dust and the stagger - flight straight down onto a
        /// target. Exposed so the landing effect can be told apart from merely being grounded.
        /// </summary>
        public bool LandedThisTransition { get; private set; }

        public AlduinMovementState CurrentState { get { return currentState; } }

        private static DovahkiinTuningDef Tuning { get { return DovahkiinTuningDef.Current; } }

        private const int TicksPerRealSecond = 60;

        private static int DwellTicksFor(AlduinMovementState state)
        {
            DovahkiinTuningDef t = Tuning;
            float seconds;
            switch (state)
            {
                case AlduinMovementState.Flight: seconds = t != null ? t.dragonMinSecondsFlight : 8f; break;
                case AlduinMovementState.Soar:   seconds = t != null ? t.dragonMinSecondsSoar : 5f; break;
                default:                         seconds = t != null ? t.dragonMinSecondsGrounded : 9f; break;
            }
            if (seconds < 0f) { seconds = 0f; }
            return (int)(seconds * TicksPerRealSecond);
        }

        public override void CompTick()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            // THE SHAKE PROFILE HEALS ITSELF HERE. This runs on the DRAGON every tick, so it is
            // the one place guaranteed to run no matter how the grab ended - including the victim
            // dying mid-shake, which stops the hediff ticking altogether.
            if (shakeUntilTick != 0 && Find.TickManager.TicksGame >= shakeUntilTick)
            {
                EndShakeProfile();
            }

            // FACTS EVERY TICK, RHYTHM ON THE INTERVAL. Running the facts on the one-second poll
            // is what let him hang motionless in flight for up to 2.5s against a 1.5s grace, and
            // what gave him nine grounded seconds to chase people on foot after every landing.
            bool factDecided = ApplyStateFacts(pawn);

            // ⚠⚠ THESE FIVE RUN UNCONDITIONALLY, *OUTSIDE* THE factDecided GATE BELOW.
            //
            // They used to sit at the top of RunPattern - which is skipped whenever a FACT has
            // already decided the state. The comment on UpdateBrawlGaze even argued that calling it
            // "unconditionally from the top of RunPattern" left no exit path to miss. **That
            // reasoning was right about the method and wrong about its caller**, and the user found
            // both consequences in one playtest:
            //
            //   * "sliding east (grounded deplacement east) while facing north after landing" - the
            //     gaze's Graphic_Single was never released, because the release lives in the method
            //     that stopped being called. A frozen facing is the known trap of that mechanism and
            //     it fired the moment its owner went quiet.
            //   * "froze in place on his second pattern 1 execution right after landing" - the
            //     airborne Wait_Wander cancel is in the same place, so a wander job survived and he
            //     stood there. HOVER-DIAG: `job=Wait_Wander patherMoving=False curPathNull=True`.
            //
            // **AN INVARIANT CANNOT LIVE INSIDE A CONDITIONAL.** Anything phrased as "he must never
            // X" belongs here; anything phrased as "he should now do Y" belongs in RunPattern.
            DovahFactionUtility.EnsureUnderLord(pawn);
            KeepFlightSpeedConstant(pawn);
            UpdateBrawlGaze(pawn);
            UpdateCirclingSound(pawn);
            UpdatePendingBreath(pawn);
            KeepFlightUninterruptible(pawn);
            CancelAirborneMelee(pawn);
            KeepDovahOffRoofedGround(pawn);
            // TEMPORARY - delete with BrawlDiagnostic once pattern 1 is proven.
            BrawlDiagnostic(pawn);

            // ⚠ THE PATTERN EXECUTOR RUNS EVERY TICK, NOT ON A POLL.
            //
            // It used to run on dragonStateIntervalTicks (60), which meant a pattern could sit a
            // whole second past its phase boundary before advancing - and a poll is the same
            // mistake as the dwell timer in slower form, which this project has now paid for
            // five times. A pattern phase ends when it ends.
            //
            // Still skipped when a FACT has already decided the state - but the only facts left
            // are the ones a pattern must never override (too hurt to fly, something in his jaws,
            // standing in melee, mid-breath). The pre-pattern rules that used to preempt it are
            // gone; see ApplyStateFacts.
            if (!factDecided)
            {
                UpdateGuardian(pawn);
                RunPattern(pawn);
            }

            // Facing is only eight-way in flight; grounded and soar are four-way and the
            // engine's own Rot4 already handles them.
            if (currentState != AlduinMovementState.Flight)
            {
                return;
            }

            // NO THROTTLE HERE ANY MORE, AND REMOVING IT IS HALF OF THE 2026-08-18 FACING FIX.
            //
            // This used to skip the refresh unless `pather.nextCell` had changed - but the octant
            // is measured from `path.Peek(dragonFacingLookaheadCells)`, SIX cells out. The trigger
            // and the measurement were two different quantities, so the sprite could be held stale
            // while the thing it depends on moved, and re-evaluated when it had not.
            //
            // TRIGGER AND MEASUREMENT MUST BE THE SAME QUANTITY. RefreshFlightFacing is built for
            // exactly this: it reference-compares the graphic it wants against the one he has and
            // returns false having touched nothing. Calling it every tick for ONE pawn is cheaper
            // than keeping a second piece of state honest, and it deletes a whole class of bug.
            AlduinGraphicsUtility.RefreshFlightFacing(pawn);
        }

        /// <summary>
        /// A GUARDIAN sits motionless on its mound until something comes close, then turns
        /// manhunter. SPEC.md 6.5c.
        ///
        /// REPORTED AND FIXED 2026-08-04: "when I spawned Alduin in guarding mode he still kept
        /// his old idle pattern (wanders around instead of staying static), and also he isn't
        /// attacking anybody despite the pawns being real close."
        ///
        /// Both were the same omission: isGuardian only ever reached the state machine, which
        /// picks his SPRITE and speed. It never touched his AI, so he remained an ordinary wild
        /// animal - wandering because animals wander, and ignoring colonists because a wild
        /// animal is hostile to nobody (the notebook's own gotcha, hit from a new direction).
        /// Guarding has to be enforced on his JOBS, not on his appearance.
        /// </summary>
        private void UpdateGuardian(Pawn pawn)
        {
            if (!isGuardian)
            {
                return;
            }

            // ---- THE TRIGGER. Approach closely enough and the mound wakes up.
            //
            // ⚠ TEMPORARY TESTING SWITCH, 2026-08-05. dragonGuardTriggerEnabled=false leaves him
            // sitting motionless but never rouses him, so the breath can be tested by walking up
            // and firing a debug breath instead of fighting him with unarmoured colonists every
            // round. THE DEFAULT IN C# IS TRUE; only the XML currently turns it off.
            // PUT IT BACK TO TRUE BEFORE THE DRAGON SHIPS.
            if (Tuning != null && !Tuning.dragonGuardTriggerEnabled)
            {
                HoldGuardPosition(pawn);
                return;
            }

            float trigger = Tuning != null ? Tuning.dragonGuardTriggerRadius : 12f;
            Pawn intruder = NearestIntruder(pawn, trigger);
            if (intruder != null)
            {
                // ⚠ NO MENTAL STATE. Rousing used to start ManhunterPermanent - and that mental
                // state OUTRANKS LordDuty in the Animal think tree, so waking a guardian would
                // have switched his attack patterns straight back off. He is already hostile
                // through the dov faction; waking him is simply ceasing to hold the mound, and
                // the pattern executor takes it from there.
                isGuardian = false;
                Messages.Message(pawn.LabelShortCap + " has been roused.",
                    pawn, MessageTypeDefOf.ThreatBig, false);
                return;
            }

            HoldGuardPosition(pawn);
        }

        /// <summary>
        /// A wait job rather than fighting his pather every tick: this is what vanilla uses to
        /// park a pawn, and it stops the wander job being issued at all instead of cancelling it
        /// after the fact.
        /// </summary>
        private static void HoldGuardPosition(Pawn pawn)
        {
            if (pawn.jobs == null)
            {
                return;
            }
            bool idleOrWandering = pawn.CurJob == null
                || pawn.CurJobDef == JobDefOf.Wait_Wander
                || pawn.CurJobDef == JobDefOf.GotoWander
                || pawn.CurJobDef == JobDefOf.Goto;
            if (idleOrWandering)
            {
                pawn.jobs.StartJob(new Job(JobDefOf.Wait, 2000),
                    JobCondition.InterruptForced, null, false, true);
            }
        }

        /// <summary>
        /// A free cell next to <paramref name="target"/>, preferring the one nearest the dragon
        /// so the dive reads as coming in from the direction he was flying. Never the target's
        /// own cell - see the note in DoLandingImpact.
        /// </summary>
        private static IntVec3 BestCellBeside(Thing target, Pawn dragon, Map map)
        {
            return BestCellBeside(target, dragon, map, false);
        }

        /// <summary>
        /// DOES A ROOF FORBID HIM STANDING HERE? The user, 2026-08-18: "dovah's cannot land
        /// nor walk inside roofed area, only fly over it."
        ///
        /// Deliberately asks about the ROOF and not about being indoors, in a room, or under an
        /// edifice. A roof is precisely what "fly over it" is the opposite of, it is one grid
        /// lookup, and it covers a castle interior and an overhead-mountain tunnel with the same
        /// test.
        ///
        /// NOT TO BE CONFUSED WITH THE MOUNTAIN RULE in Patch_DragonFlyOver. That one is natural
        /// ROCK refusing to let him FLY through it; this one is a ROOF refusing to let him LAND.
        /// They are different questions about different halves of his movement.
        /// </summary>
        private static bool RoofBarsGrounding(IntVec3 c, Map map)
        {
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            if (t != null && !t.dragonNeverGroundedUnderRoof)
            {
                return false;
            }
            return map != null && c.InBounds(map) && c.Roofed(map);
        }

        /// <summary>
        /// A free cell next to <paramref name="target"/>, as above, but optionally refusing any
        /// cell a roof covers - which is what makes the landing itself honour the roof rule,
        /// rather than leaning on a correction afterwards.
        /// </summary>
        private static IntVec3 BestCellBeside(Thing target, Pawn dragon, Map map,
            bool requireUnroofed)
        {
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 c = target.Position + GenAdj.AdjacentCells[i];
                if (!c.InBounds(map) || !c.Standable(map))
                {
                    continue;
                }
                if (requireUnroofed && RoofBarsGrounding(c, map))
                {
                    continue;
                }
                if (PawnUtility.PawnBlockingPathAt(c, dragon) != null)
                {
                    continue;
                }
                float d = (c - dragon.Position).LengthHorizontalSquared;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>
        /// The nearest pawn a guardian would consider an intruder. Deliberately NOT a hostility
        /// test: a factionless dragon is hostile to nobody, so GenHostility would return nothing
        /// and the mound would never wake - the same blind spot that once had the Ancient
        /// Dragonborn ignoring a boar.
        /// </summary>
        private static Pawn NearestIntruder(Pawn guardian, float radius)
        {
            Map map = guardian.Map;
            if (map == null)
            {
                return null;
            }
            float best = radius * radius;
            Pawn found = null;
            List<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p == guardian || p.Dead || p.Downed)
                {
                    continue;
                }
                // Colonists, their guests and their animals. Not other wildlife - a squirrel
                // wandering past should not wake the World-Eater.
                if (p.Faction == null || !p.Faction.IsPlayer)
                {
                    continue;
                }
                float d = (p.Position - guardian.Position).LengthHorizontalSquared;
                if (d < best)
                {
                    best = d;
                    found = p;
                }
            }
            return found;
        }

        /// <summary>
        /// The FACT rules, run EVERY TICK. Returns true when one of them has already decided his
        /// state, in which case the rhythm below must not run.
        ///
        /// ⚠ WHY EVERY TICK, AND NOT ON dragonStateIntervalTicks LIKE THE REST. These express
        /// things that are TRUE, not things he chooses - too hurt to fly, target out of reach,
        /// hanging motionless in mid air. The changelog records three separate defects caused by
        /// making facts queue behind the dwell cooldown, and running them on a one-second poll is
        /// the same mistake in slower form: the user reported "I still caught him standing still
        /// while in flight" precisely because the anti-hover check could only fire once a second
        /// against a 1.5s grace, so he could hang there for two and a half seconds.
        /// </summary>
        private bool ApplyStateFacts(Pawn pawn)
        {
            // ---- TOO HURT TO FLY, or standing in melee range. Both facts.
            if (MustBeGrounded(pawn))
            {
                soarPauseUntilTick = 0;
                if (currentState != AlduinMovementState.Grounded)
                {
                    EnterState(pawn, AlduinMovementState.Grounded);
                }

                // ⚠⚠ FORCING THE STATE IS NOT A REASON TO SILENCE THE PATTERN. THIS BROKE
                // DIVE-AND-BRAWL COMPLETELY: *"he lands, stay there doing nothing, flies away."*
                //
                // The melee clause in MustBeGrounded is true for the WHOLE of a brawl - standing
                // in melee range is what a brawl IS - so returning true here skipped RunPattern
                // for the entire Execute phase. And RunPattern is the only thing that:
                //
                //   * re-asserts the AssaultThing duty (so nothing told him to fight), and
                //   * checks `now >= phaseUntilTick` (so the phase could not end on time - it
                //     ended only when the target died or stepped out of range, and THAT is why he
                //     eventually flew away).
                //
                // The landing teleport disrupts his current job, and with no duty re-asserted
                // afterwards there was nothing left driving him at all. Hence: lands, nothing,
                // leaves.
                //
                // **The fact still forces the STATE - that part was always right. It must no
                // longer veto the DECISION.** A pattern in progress keeps running; only the two
                // hard vetoes below stop it, and they are genuine "he physically cannot" cases.
                //
                // This is the same sort that CompTick already had to make: `he must never X` runs
                // regardless, `he should now do Y` is the pattern's business. A fact that forces a
                // state is the first kind. It had been doing both.
                if (pawn.Downed || IsHeldGrounded)
                {
                    return true;
                }
                return false;
            }

            // ---- MID-BREATH. The state belongs to the breath until it burns out; nothing below
            // may roll him out of it. A guardian would otherwise be back on the ground within a
            // second of starting a soaring breath.
            if (Find.TickManager.TicksGame < stateHoldUntilTick)
            {
                return true;
            }

            // ⚠⚠ THE DISENGAGE, ITS SOAR PAUSE AND THE ANTI-HOVER DROP WERE ALL REMOVED HERE ON
            // 2026-08-06, AND THE REASON IS THE WHOLE POINT OF THIS METHOD.
            //
            // Every one of them ended in `return true`, and CompTick only runs the pattern
            // executor when this method returns FALSE. In a fight at least one of them was true
            // almost every tick - the disengage fires whenever he is grounded with a target out
            // of reach, which is most of a chase - so RunPattern was barely executing. The user
            // saw the result exactly: "doesn't circle around at all and just chases and kills
            // like any normal manhunting beast... he uses breathing from time to time but not by
            // the patterns."
            //
            // They were all PRE-PATTERN rules solving problems the patterns now solve properly:
            //   - the disengage      -> the LEAVE phase
            //   - the soar pause     -> a pattern's own timing
            //   - the anti-hover drop -> Approach owns the state, and hovering turned out to be a
            //                            JOB problem, not a state problem, so landing him for it
            //                            was treating a symptom with the wrong lever
            //
            // What is left below is only what a pattern must never be allowed to override.
            if (currentState == AlduinMovementState.Flight)
            {
                bool movingNow = pawn.pather != null && pawn.pather.Moving;
                if (movingNow)
                {
                    stationarySinceTick = -1;
                }
                else
                {
                    if (stationarySinceTick < 0)
                    {
                        stationarySinceTick = Find.TickManager.TicksGame;
                    }
                    // TEMPORARY DIAGNOSTIC - delete with HoverDiagnostic once patterns are proven.
                    HoverDiagnostic(pawn);
                    // Still worth attempting, and it no longer decides anything: a successful
                    // circle just means he has somewhere to be. The pattern keeps control either
                    // way, which is why this returns nothing.
                    if (CurrentTargetOf(pawn) == null)
                    {
                        TryCircle(pawn);
                    }
                }
            }
            else
            {
                stationarySinceTick = -1;
            }

            return false;
        }

        // ================== TEMPORARY DIAGNOSTIC - DELETE WHEN SOLVED ==================
        // Added 2026-08-05 after three failed fixes for "motionless while in flight". Reports the
        // whole decision context so the cause can be READ rather than guessed at a fourth time.
        private int lastHoverLogTick;

        private void HoverDiagnostic(Pawn pawn)
        {
            if (Find.TickManager.TicksGame - lastHoverLogTick < 120)
            {
                return;
            }
            lastHoverLogTick = Find.TickManager.TicksGame;

            Thing target = CurrentTargetOf(pawn);
            string targetDesc = "none";
            if (target != null)
            {
                Pawn tp = target as Pawn;
                targetDesc = target.LabelShortCap + " [" + target.GetType().Name + " def=" + target.def.defName
                    + " spawned=" + target.Spawned + " destroyed=" + target.Destroyed
                    + (tp != null ? " dead=" + tp.Dead + " downed=" + tp.Downed : " notPawn")
                    + " dist=" + pawn.Position.DistanceTo(target.Position).ToString("0.0") + "]";
            }

            // What CurJob.targetA actually holds, even when IsEngageable rejected it - the
            // difference between the two is the whole question.
            string rawJobTarget = "none";
            if (pawn.CurJob != null && pawn.CurJob.targetA.IsValid)
            {
                LocalTargetInfo a = pawn.CurJob.targetA;
                rawJobTarget = a.HasThing
                    ? a.Thing.LabelShortCap + " [" + a.Thing.GetType().Name + " def=" + a.Thing.def.defName + "]"
                    : "cell " + a.Cell;
            }

            string stance = pawn.stances == null || pawn.stances.curStance == null
                ? "null" : pawn.stances.curStance.GetType().Name;

            Log.Warning("[Dovahkiin] HOVER-DIAG"
                + " PATTERN=" + PatternDebugLabel
                + " phaseIn=" + (phaseUntilTick - Find.TickManager.TicksGame)
                + " state=" + currentState
                + " stationaryTicks=" + (stationarySinceTick < 0 ? -1 : Find.TickManager.TicksGame - stationarySinceTick)
                + " | job=" + (pawn.CurJobDef != null ? pawn.CurJobDef.defName : "NONE")
                + " jobTargetA=" + rawJobTarget
                + " | engageableTarget=" + targetDesc
                + " | patherMoving=" + (pawn.pather != null && pawn.pather.Moving)
                + " curPathNull=" + (pawn.pather == null || pawn.pather.curPath == null)
                + " dest=" + (pawn.pather != null ? pawn.pather.Destination.ToString() : "?")
                + " | fullBodyBusy=" + (pawn.stances != null && pawn.stances.FullBodyBusy)
                + " stunned=" + (pawn.stances != null && pawn.stances.stunner != null && pawn.stances.stunner.Stunned)
                + " stance=" + stance
                + " | heldGrounded=" + IsHeldGrounded
                + " soarPause=" + soarPauseUntilTick
                + " mentalState=" + (pawn.InMentalState ? pawn.MentalStateDef.defName : "none")
                + " downed=" + pawn.Downed);
        }
        // ================ END TEMPORARY DIAGNOSTIC ================

        /// <summary>
        /// True when he has nothing in reach worth standing on the ground for. Either no target
        /// at all, or one that has walked out of his bite range.
        /// </summary>
        private bool OutOfReach(Pawn pawn)
        {
            Thing target = CurrentTargetOf(pawn);
            if (target == null || !target.Spawned)
            {
                return true;
            }
            float reach = Tuning != null ? Tuning.dragonDisengageDistance : 4f;
            return pawn.Position.DistanceTo(target.Position) > reach;
        }

        /// <summary>
        /// Hang motionless for the disengage beat. A STUN, because that is the engine's own
        /// movement gate - Pawn_PathFollower.PatherTick returns immediately on
        /// stances.FullBodyBusy, which is true whenever stunner.Stunned. StopDead alone clears
        /// the path but not the JOB, so the job driver simply paths again next tick; that cost
        /// two rounds on the grab and the lesson is recorded in §5 of the notebook.
        /// </summary>
        private static void HoldStill(Pawn pawn, int ticks)
        {
            if (ticks < 1) { return; }
            if (pawn.stances != null && pawn.stances.stunner != null)
            {
                // ⚠ THIS IS THE ONE STUN AN AIRBORNE DRAGON IS STILL ALLOWED. Since 2026-08-12 a
                // dovah in the air cannot be stunned (Patch_AirborneIgnoresStun, the user's call),
                // and a BREATH happens in SOAR - which is airborne. Without this flag the patch
                // would silently delete the "he stays motionless after a breath" rule, and it
                // would present as a behaviour regression with nothing in the log.
                Patch_AirborneIgnoresStun.AllowSelfStun = true;
                try
                {
                    // StunHandler.StunFor takes Max(existing, ticks), so ONE call for the whole
                    // duration is both sufficient and exact. Calling it per tick with a short
                    // window is what made the stun outlive its purpose.
                    pawn.stances.stunner.StunFor(ticks, pawn, false, false);
                }
                finally
                {
                    // finally, not a plain reset: an exception inside StunFor would otherwise
                    // leave every dragon on the map permanently stunnable again.
                    Patch_AirborneIgnoresStun.AllowSelfStun = false;
                }
            }
            if (pawn.pather != null)
            {
                pawn.pather.StopDead();
            }
        }

        /// <summary>
        /// CIRCLING. Flight is the connective tissue between attacks (SPEC.md 6.5c-4, "FLIGHT IS
        /// HOME"), so a dragon in flight with nothing to chase should be sweeping the area, not
        /// hanging in the sky. Sends him to a cell within dragonCirclingRadius.
        ///
        /// Only ever overrides an EMPTY or wandering job - never a real one, or it would interrupt
        /// a fight. Same gate the guardian's Wait job uses.
        /// </summary>
        private bool TryCircle(Pawn pawn)
        {
            if (pawn.jobs == null || pawn.Map == null)
            {
                return false;
            }

            // ⚠ NO JOB GATE HERE, AND THAT IS DELIBERATE. Callers only reach this when
            // CurrentTargetOf(pawn) is null - and with no target there is no fight to interrupt,
            // so whatever idle job his think tree handed him may be replaced freely. The previous
            // version only overrode Wait / Wait_Wander / GotoWander, so ANY other idle job left
            // him unable to circle: he hovered to the grace, dropped to Grounded, disengaged,
            // soared, flew, hovered again. That cycle is what the user saw as "the speed seemed
            // to periodically be dialed down, alternatively" - it was not the speed being
            // modulated, it was him cycling 3.60x -> 1.00x -> 1.32x -> 3.60x through the states.
            int radius = Tuning != null ? Tuning.dragonCirclingRadius : 20;
            if (radius < 2) { radius = 2; }

            // A MINIMUM LEG, DERIVED rather than given its own knob. RandomClosewalkCellNear
            // treats the radius as a MAXIMUM, so it happily returns a cell one step away - and a
            // circling dragon doing one-cell hops stops between every one of them, which reads as
            // stuttering rather than flying. Half the radius keeps each leg a real sweep.
            //
            // Derived on purpose: a second knob here would be a number that has to agree with
            // dragonCirclingRadius, and the landing stun is this session's lesson in what happens
            // to two numbers that must agree and are stored apart.
            int minLeg = radius / 2;
            if (minLeg < 2) { minLeg = 2; }
            IntVec3 origin = pawn.Position;

            IntVec3 dest;
            if (!CellFinder.TryRandomClosewalkCellNear(origin, pawn.Map, radius, out dest,
                    c => c.DistanceToSquared(origin) >= minLeg * minLeg))
            {
                // Nothing far enough - take anything rather than hover.
                if (!CellFinder.TryRandomClosewalkCellNear(origin, pawn.Map, radius, out dest, null))
                {
                    return false;
                }
            }
            if (dest == origin)
            {
                return false;
            }
            pawn.jobs.StartJob(new Job(JobDefOf.Goto, dest),
                JobCondition.InterruptForced, null, false, true);
            return true;
        }

        // ============================================================================
        // THE ATTACK-PATTERN EXECUTOR (SPEC.md 6.5c-4)
        // ============================================================================
        private DragonAttackPattern currentPattern = DragonAttackPattern.DiveAndBrawl;
        private DragonAttackPattern? lastPattern;
        private DragonPatternPhase patternPhase = DragonPatternPhase.Leave;
        private int phaseUntilTick;
        private bool hasPattern;

        /// <summary>Which attack he is running, for the debug readout. Null when idle.</summary>
        public string PatternDebugLabel
        {
            get { return hasPattern ? currentPattern + "/" + patternPhase : "none"; }
        }

        /// <summary>
        /// Is a pattern deliberately holding him in the air right now? Approach and Leave are
        /// always flight, and HOVER BREATH executes in soar.
        ///
        /// Used to stop "he is in melee range" grounding a dragon who is airborne ON PURPOSE -
        /// otherwise anyone can cancel a soaring breath just by walking up and swinging at him.
        /// </summary>
        private bool PatternWantsAirborne
        {
            get
            {
                if (!hasPattern)
                {
                    return false;
                }
                if (patternPhase != DragonPatternPhase.Execute)
                {
                    return true; // Approach and Leave are both flight.
                }
                return DragonAttackPatterns.ExecuteStateOf(currentPattern) != AlduinMovementState.Grounded;
            }
        }

        /// <summary>
        /// Run the current attack, or roll a new one. This REPLACES the old state rhythm - there
        /// is no dwell timer and no per-interval roll about which state to be in, because the
        /// pattern owns the state while it runs. See DragonAttackPattern.cs for why the two could
        /// never coexist.
        /// </summary>
        private void RunPattern(Pawn pawn)
        {
            DovahkiinTuningDef t = Tuning;

            // ⚠ THE LORD IS WHAT MAKES ANY OF THIS REAL. Faction for hostility, Lord so the duty
            // branch of the Animal think tree runs at all, and NO mental state - a manhunting
            // dragon outranks his own duty and the patterns go back to being decoration. See
            // DovahLord.cs. Cheap to call every tick; it returns at once once he is set up.
            // ⚠ EnsureUnderLord, KeepFlightSpeedConstant, UpdateBrawlGaze, UpdateCirclingSound and
            // CancelAirborneMelee USED TO BE CALLED HERE. They are invariants, this method is
            // conditional, and that mismatch cost two defects - see the block in CompTick.

            int now = Find.TickManager.TicksGame;

            // ⚠⚠ A MOUTHFUL IS A MOUTHFUL - NO PATTERN ADVANCES WHILE HE IS HOLDING A PAWN.
            //
            // The user, 2026-08-12: "i caught him bite-shaking a pawn then finishing him with the
            // ground-breath...insane combo? yes... should be possible? NO, SHOULDN'T BE POSSIBLE!!!"
            //
            // IsHeldGrounded is set ONLY by Hediff_DovahGrabbed (verified - it is the sole caller
            // of HoldGroundedUntil), refreshed +30 ticks each tick of the grab, so it is exactly
            // "a grab is in progress" and nothing else.
            //
            // ⚠ THE CLOCK KEEPS RUNNING. An earlier version paused it (`phaseUntilTick++`) on the
            // reasoning that he should not be "already overdue" when he lets go. **That was wrong
            // and the user caught it immediately**: with dovahGrabChance at 1.0 for testing, every
            // bite grabs, so IsHeldGrounded is true almost continuously through a brawl - and a
            // paused clock meant the phase COULD NOT EXPIRE. *"him staying too long locked in
            // pattern 1 (he never stopped attacking until his target died)."*
            //
            // Letting it run costs nothing now that Execute always hands off to Leave: the first
            // tick after he releases, the phase is over and he goes circling. The 15-second leg is
            // what stops him snapping into the next attack - a paused clock was solving a problem
            // that the phase order already solves.
            if (IsHeldGrounded)
            {
                return;
            }

            Thing target = CurrentTargetOf(pawn);
            if (target != null)
            {
                lastTargetPos = target.Position;
            }

            // ⚠⚠ LOSING THE TARGET MUST NOT ABORT THE PHASE HE IS IN. THIS ONE LINE CAUSED THREE
            // OF THE FOUR DEFECTS REPORTED ON 2026-08-12, and the log named it: SEVEN of nine
            // HOVER-DIAG samples read PATTERN=none WHILE A LIVE TARGET EXISTED - the pattern had
            // just been wiped and was about to be re-rolled.
            //
            // The old code dropped into the idle branch the instant CurrentTargetOf returned null,
            // and that branch does `hasPattern = false` and `EnterState(Flight)`. A BREATH KILLS
            // ITS TARGET, so:
            //
            //   * "directly switches to flight-state while still locked in the after-breath
            //     motionless cooldown" - the victim died to the breath, the target went null, and
            //     EnterState(Flight) fired while the hold still had seconds to run. Reported twice,
            //     once for a grounded breath and once "after a soar breathing".
            //   * "engaging with a new pattern less than 15 seconds after the previous one" -
            //     hasPattern was false, so the next target to appear started a pattern immediately
            //     instead of finishing the 15-second circling leg.
            //
            // ⚠⚠ AND THE FIX HAD A HOLE, WHICH THE NEXT PLAYTEST FOUND: it stopped the phase being
            // aborted MID-WAY, but an Execute that EXPIRED with no target still fell through to the
            // idle branch, which sets hasPattern = false. A new target then started a fresh attack
            // with no circling at all. The user, same day: *"he still switched to another pattern
            // directly after killing his target"* and *"he directly finished another pawn off with
            // a breath again right after throwing him to the ground."*
            //
            // ⚠ SO THE PHASE ORDER IS NOW ABSOLUTE: **EXECUTE ALWAYS HANDS OFF TO LEAVE, AND A NEW
            // PATTERN CAN ONLY BE ROLLED AT THE END OF A LEAVE OR FROM A STANDING START.** There is
            // no path from "attack finished" to "attack" that does not pass through a full
            // dragonPatternLeaveTicks of circling. That is the user's rule - *"the flight-circling
            // phase should always be >=15 seconds"* - expressed as structure rather than as a check
            // that something has to remember to make.
            //
            // A pattern in progress therefore OWNS him to the end of its Leave, target or no target,
            // and the idle branch below is reachable only when nothing is running at all.
            if (!hasPattern)
            {
                // ---- NOTHING RUNNING. A guardian holds its ground; anyone else hunts.
                if (target == null)
                {
                    if (isGuardian)
                    {
                        if (currentState != AlduinMovementState.Grounded)
                        {
                            EnterState(pawn, AlduinMovementState.Grounded);
                        }
                        // Hold the mound. Radius 0 so he does not drift off it.
                        DovahFactionUtility.SetDuty(pawn, DutyDefOf.Defend, pawn.Position, 0f);
                        return;
                    }
                    if (currentState != AlduinMovementState.Flight)
                    {
                        EnterState(pawn, AlduinMovementState.Flight);
                    }
                    // ⚠ HUNT, DO NOT WANDER. This branch used to set WanderClose, and that was a
                    // DEADLOCK: a wandering dragon never acquires a target, CurrentTargetOf stays
                    // null, so he wanders for ever. The user: "he stayed blocked in the cycling in
                    // flight state and thus I couldnt see if he was hostile."
                    //
                    // AssaultColony's think node is JobGiver_AIFightEnemies at targetAcquireRadius
                    // 65, so he goes looking - and the moment he acquires someone, mindState
                    // .enemyTarget is set, CurrentTargetOf returns it, and a pattern begins.
                    DovahFactionUtility.SetDuty(pawn, DutyDefOf.AssaultColony, LocalTargetInfo.Invalid, 0f);
                    return;
                }
                BeginPattern(pawn, t, now);
                return;
            }

            // From here the phase may be running with a DEAD target - see the block above. Anything
            // that needs a live one is guarded; anything that needs only a place uses lastTargetPos.
            float distance = target != null
                ? pawn.Position.DistanceTo(target.Position)
                : float.MaxValue;

            switch (patternPhase)
            {
                case DragonPatternPhase.Approach:
                    // Approach is the ONE phase that genuinely needs a live target. Without one
                    // there is nothing to close on - but he still owes a full circling leg, so it
                    // becomes Leave rather than an abort.
                    if (target == null)
                    {
                        BeginLeave(pawn, t, now);
                        return;
                    }
                    // Closing. Flight, unconditionally - "while he is closing there is no
                    // decision to make", the fix the user gave for the wavering chase.
                    if (currentState != AlduinMovementState.Flight)
                    {
                        EnterState(pawn, AlduinMovementState.Flight);
                    }
                    // GO AND GET HIM. AssaultThing's think node finds targets by hostility, which
                    // the dov faction now supplies - this is the job the manhunter AI used to do
                    // for us, except now it is ours to start and stop.
                    DovahFactionUtility.SetDuty(pawn, DutyDefOf.AssaultThing, target, 0f);
                    if (distance <= DragonAttackPatterns.EngageDistanceOf(currentPattern, t))
                    {
                        BeginExecute(pawn, t, now, target);
                    }
                    else if (StationaryTooLong(t))
                    {
                        // ⚠ FLIGHT IS NEVER MOTIONLESS - the user's rule, and it has to hold in
                        // APPROACH as well as Leave. If the duty has not moved him past the grace
                        // he cannot reach his target (inside a mountain base, across broken
                        // ground), and waiting out the 25-second approach timeout hovering is
                        // exactly the symptom reported for five sessions. Cut to Leave, which
                        // always has a waypoint and therefore always moves him.
                        BeginLeave(pawn, t, now);
                    }
                    else if (now >= phaseUntilTick)
                    {
                        // Could not reach him. Roll something else rather than approaching for
                        // ever - the unreachable-target case that produced four rounds of
                        // "motionless in flight" reports.
                        BeginLeave(pawn, t, now);
                    }
                    return;

                case DragonPatternPhase.Execute:
                    // A BRAWL keeps fighting; a BREATH must hold still, or he wanders out of his
                    // own jet. Defend at radius 0 on his own cell is the stillest duty available.
                    if (DragonAttackPatterns.BreathShapeOf(currentPattern).HasValue)
                    {
                        // No target needed - and that is the point. The breath has already been
                        // fired; what remains is the motionless hold, which must run out whether
                        // or not the breath killed whoever it was aimed at.
                        DovahFactionUtility.SetDuty(pawn, DutyDefOf.Defend, pawn.Position, 0f);
                    }
                    else
                    {
                        // ⚠⚠ A BRAWL WITH NO TARGET MUST LOOK FOR THE NEXT ONE, NOT STAND STILL.
                        //
                        // This branch used to read "Brawling over a body. Nothing to assault, so
                        // hold the spot until the phase times out" - and the user found it:
                        // *"caught him frozen, not attacking on some of his pattern 1 session."*
                        //
                        // INTERMITTENT BECAUSE IT DEPENDS ON HOW THE VICTIM DIES. IsEngageable
                        // rejects DOWNED pawns as well as dead ones (deliberately, since 2026-08-05
                        // - a downed target used to hold him hovering). So the moment he DOWNS
                        // someone rather than killing them outright, his target goes null and he
                        // stood over the body for the rest of the eight-second phase. Kill them
                        // cleanly and the brawl looked fine; down them and it "froze".
                        //
                        // The intent recorded when downed pawns were excluded was "he leaves the
                        // downed alone and LOOKS FOR THE NEXT THREAT". Only the first half was ever
                        // implemented; this is the second.
                        Pawn next = target as Pawn;
                        if (next == null)
                        {
                            Pawn nearby = NearestHostilePawn(pawn, now);
                            float reach = t != null ? t.dragonBrawlRetargetRadius : 12f;
                            if (nearby != null && pawn.Position.DistanceTo(nearby.Position) <= reach)
                            {
                                next = nearby;
                            }
                        }
                        if (next != null)
                        {
                            DovahFactionUtility.SetDuty(pawn, DutyDefOf.AssaultThing, next, 0f);
                        }
                        else
                        {
                            // Genuinely nobody left within reach. Hold the spot - the phase will
                            // time out shortly and Leave takes him back into the air.
                            DovahFactionUtility.SetDuty(pawn, DutyDefOf.Defend, pawn.Position, 0f);
                        }
                    }
                    if (now >= phaseUntilTick)
                    {
                        BeginLeave(pawn, t, now);
                    }
                    return;

                default: // Leave - circling until the next attack is chosen.
                    if (currentState != AlduinMovementState.Flight)
                    {
                        EnterState(pawn, AlduinMovementState.Flight);
                    }
                    // THE CIRCLING - LONG STRAIGHT LEGS, NOT A WANDER.
                    //
                    // ⚠ WanderClose was tried first and rejected in play: its
                    // JobGiver_WanderNearDutyLocation has wanderRadius 3 baked into the DutyDef,
                    // so he changed direction every two or three cells. The user: "What I want is
                    // him flying in straight lines not change directions every 2-3 cells, making
                    // him look like a maniac."
                    //
                    // TravelOrWait uses JobGiver_GotoTravelDestination - a straight run to one
                    // point. So circling is built from WAYPOINTS around the fight: fly a full leg,
                    // arrive, turn, fly the next. Straight lines by construction.
                    // ⚠ THE CENTRE CAPTURED AT BeginLeave, *NOT* the target's live position. Handing
                    // this a moving target dragged the whole ring along behind it, and when the ring
                    // swept over him the next waypoint landed behind his own back - which is the
                    // inward fold in the red path the user drew. He orbits the PLACE, not the pawn.
                    UpdateCirclingWaypoint(
                        pawn,
                        circleCentre.IsValid ? circleCentre : pawn.Position,
                        t);
                    if (now >= phaseUntilTick)
                    {
                        // ⚠ THE ONLY DOOR TO A NEW ATTACK. With a target, the next pattern starts
                        // here and nowhere else. Without one, drop to idle so he goes hunting -
                        // and note the circling leg has already been served in full either way.
                        if (target != null)
                        {
                            BeginPattern(pawn, t, now);
                        }
                        else
                        {
                            hasPattern = false;
                        }
                    }
                    return;
            }
        }

        /// <summary>
        /// FLIGHT IS A CONSTANT VELOCITY. The user, 2026-08-06: "i caught him slowing down while
        /// in flight, flight's velocity is CONSTANT and shouldn't be changing (unless of course
        /// he is under the effect of slowtime or a cc that renders movespeed in general)".
        ///
        /// `dragonFlightSpeedFactor` only scales the MoveSpeed stat. Two other things change how
        /// fast a pawn actually crosses a cell, and neither goes through that stat:
        ///
        ///   1. LOCOMOTION URGENCY. Job.locomotionUrgency scales movement directly - Amble is far
        ///      slower than Sprint - and the job givers behind our duties choose their own. That
        ///      is fixed here: anything he does while airborne is a Sprint.
        ///   2. TERRAIN PATH COST, added per cell in Pawn_PathFollower.CostToMoveIntoCell. Snow,
        ///      mud and rubble slow him, which for a creature IN THE AIR is simply wrong.
        ///
        /// ⚠ ONLY (1) IS FIXED HERE. (2) needs a Harmony patch on CostToMoveIntoCell, which runs
        /// for every move of every pawn on the map - a hot path, with RocketMan installed, for a
        /// creature there is one of. Raised with the user rather than taken unilaterally; if the
        /// slowdown survives this, terrain is the remaining suspect and snow is the easiest test.
        /// </summary>
        private void KeepFlightSpeedConstant(Pawn pawn)
        {
            if (currentState != AlduinMovementState.Flight)
            {
                return;
            }
            Job job = pawn.CurJob;
            if (job != null && job.locomotionUrgency != LocomotionUrgency.Sprint)
            {
                job.locomotionUrgency = LocomotionUrgency.Sprint;
            }
        }

        /// <summary>
        /// A DOVAH DOES NOT TRADE BLOWS IN MID-AIR. SPEC.md 6.5: soar and flight are "immune to
        /// melee and unable to melee".
        ///
        /// The user, 2026-08-06: "an insect attacked him, and it made him attack in return too
        /// (making him stay motionless again while roling melee while in his flight sprite)."
        ///
        /// The travel duty has no fight branch at all - `TravelOrWait` is goto, needs, wander -
        /// so the retaliation comes from higher in the Animal think tree, ABOVE LordDuty, where no
        /// duty of ours can reach it. Rather than hunt which node it is, this enforces the
        /// invariant where it can never be argued with: if he is airborne and swinging, the swing
        /// is cancelled and the phase's own duty re-asserted.
        ///
        /// It also fixes the second half of the report for free - a melee swing halts movement,
        /// which is why he read as motionless in a flight sprite.
        /// </summary>
        private bool CancelAirborneMelee(Pawn pawn)
        {
            if (currentState == AlduinMovementState.Grounded || pawn.jobs == null)
            {
                return false;
            }
            JobDef cur = pawn.CurJobDef;
            // ⚠ ExtinguishSelf IS IN THIS LIST, and it is not a melee job - it is here because it
            // roots him exactly the same way. Caught in the log 2026-08-12:
            //
            //   PATTERN=PerchBreath/Leave state=Flight job=ExtinguishSelf jobTargetA=Fire
            //   patherMoving=False stationaryTicks=29
            //
            // He had set himself alight with his own breath and was hanging in mid-air patting
            // the flames out. A dovah does not stop flying to beat out a fire; he burns.
            //
            // (The deeper question - whether a dragon should be ignitable by his OWN breath at
            // all - is left alone deliberately. This stops the freeze; immunity is a design call
            // for the user, not something to fold into a bug fix.)
            //
            // ⚠⚠ Wait_Wander AND GotoWander ARE HERE, AND THEY ARE THE SIDE-STEPPING.
            // The user, 2026-08-12: *"His trajectory still is instable (he side steps a lot)."*
            // `HOVER-DIAG` caught it mid-leg: `DiveAndBrawl/Leave … job=Wait_Wander`.
            //
            // `JobGiver_GotoTravelDestination` opens by FLIPPING `mindState.nextMoveOrderIsWait`
            // and returns a 30-80 tick `Wait_Wander` when it lands true. UpdateCirclingWaypoint
            // sets that flag true every tick so the giver's own flip lands on travel - but that
            // only works if the giver runs EXACTLY ONCE per tick, and it does not: a job ending
            // plus a CheckForJobOverride in the same tick flips it twice and the wait comes back.
            //
            // **Fighting the flag is arithmetic; cancelling the job is a fact.** A wander order is
            // wrong for an airborne dovah under every circumstance, so it is refused outright and
            // replaced with the leg he was already flying - which is what a wander was interrupting.
            if (cur != JobDefOf.AttackMelee && cur != JobDefOf.Wait_Combat
                && cur != JobDefOf.ExtinguishSelf
                && cur != JobDefOf.Wait_Wander && cur != JobDefOf.GotoWander)
            {
                return false;
            }

            // ⚠⚠ BUILD THE REPLACEMENT *BEFORE* ENDING THE JOB, AND NEVER LEAVE HIM JOBLESS.
            // See the block comment on AirborneReplacementJob - the ordering IS the fix.
            Job replacement = AirborneReplacementJob(pawn);

            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            // Clear the stance too, or the attack's cooldown keeps him rooted for its remainder
            // even though the job is gone - which would read as exactly the same bug.
            if (pawn.stances != null)
            {
                pawn.stances.CancelBusyStanceSoft();
            }

            // ⚠ addToJobsThisTick: FALSE. Our replacement must not itself count towards
            // Pawn_JobTracker's 10-jobs-in-10-ticks detector, or the cure trips the very alarm it
            // was written to silence.
            pawn.jobs.StartJob(replacement, JobCondition.InterruptForced, addToJobsThisTick: false);
            return true;
        }

        /// <summary>
        /// WHAT AN AIRBORNE DRAGON DOES THE INSTANT HIS SWING IS TAKEN AWAY. NEVER RETURNS NULL.
        ///
        /// ⚠⚠ THIS IS NOT POLISH. WITHOUT IT, CANCELLING THE MELEE IS A JOB-THRASH THAT RIMWORLD
        /// PUNISHES BY FREEZING HIM FOR 2.5 SECONDS. Diagnosed 2026-08-12 from Player.log, which
        /// was full of:
        ///
        ///     Dovahkiin_Alduin_Test55060 started 10 jobs in 10 ticks.
        ///     List: (AttackMelee (Job_1462) A=Thing_Human723) , (AttackMelee (Job_1463) …
        ///
        /// The loop, decompiled rather than guessed:
        ///
        ///   1. our CompTick cancels the melee with `startNewJob: false`, leaving curJob NULL.
        ///      CompTick runs from Pawn.Tick's base.Tick(), BEFORE jobs.JobTrackerTick().
        ///   2. Pawn_JobTracker.JobTrackerTick then hits
        ///        `if (curJob == null && !pawn.Dead && pawn.mindState.Active) TryFindAndStartJob();`
        ///      which runs the WHOLE Animal think tree from the top - and the retaliation node
        ///      sits ABOVE LordDuty, so it hands back AttackMelee. That counts as a job given.
        ///   3. Every tick. At ten, Pawn_JobTracker.FinalizeTick sums jobsGivenRecentTicks, finds
        ///      >= 10, and calls JobUtility.TryStartErrorRecoverJob - which does `Log.Error(...)`
        ///      and then `StartJob(JobMaker.MakeJob(JobDefOf.Wait, 150))`.
        ///
        /// **That forced Wait is 150 ticks - 2.5 seconds motionless in mid-air**, and it is exactly
        /// what HOVER-DIAG recorded during Leave: `job=Wait patherMoving=False curPathNull=True`
        /// with stationaryTicks climbing 29 -> 58 -> 87. The log spam and the stalls were never two
        /// problems: the freeze is the engine's anti-thrash net disciplining us, and the error line
        /// is that same net announcing itself.
        ///
        /// THE FIX IS TO STOP CREATING THE VACUUM, not to cancel more cleverly. Every variation of
        /// "end the job better" still leaves curJob null for one tick, and one tick is all the
        /// think tree needs. Fill it and step 2 never runs at all. Nothing re-opens the decision
        /// either: a Goto has no expiry, and Notify_DamageTaken gates CheckForJobOverride behind
        /// `TicksGame >= lastDamageCheckTick + 180`, so being hit costs at most one job every three
        /// seconds.
        ///
        /// **It must honour the PHASE, or the cure breaks something else.** Flying him off to a
        /// waypoint is right while circling and wrong mid-breath - Execute holds him still on
        /// purpose (Defend radius 0) so he does not wander out of his own jet, and a Goto there
        /// would drag him out of a breath he is casting. So a breath gets a short Wait: still no
        /// vacuum, still motionless.
        /// </summary>
        private Job AirborneReplacementJob(Pawn pawn)
        {
            Map map = pawn.Map;

            // EXECUTING A BREATH - hold the cell. A Goto here would pull him out of his own jet.
            if (patternPhase == DragonPatternPhase.Execute
                && DragonAttackPatterns.BreathShapeOf(currentPattern).HasValue)
            {
                return HoldStillJob();
            }

            IntVec3 flyTo = IntVec3.Invalid;

            if (patternPhase == DragonPatternPhase.Leave)
            {
                // Circling - the waypoint IS where he is supposed to be going.
                if (circleWaypoint.IsValid && circleWaypoint != pawn.Position)
                {
                    flyTo = circleWaypoint;
                }
            }
            else
            {
                // APPROACHING - keep closing. Sending him to a circling waypoint here would undo
                // the phase: he would fly away from the pawn he is meant to be diving on.
                Thing target = CurrentTargetOf(pawn);
                if (target != null && target.Spawned && target.Position != pawn.Position)
                {
                    // ⚠⚠ BESIDE HIM, NEVER *ON* HIM - AND THIS LINE IS WHAT BROKE PATTERN 1.
                    //
                    // It read `flyTo = target.Position`, and **a dovah can never enter a living
                    // pawn's cell**: PawnUtility.PawnsCanShareCellBecauseOfBodySize returns false
                    // outright when either pawn is BodySize >= 1.5, and his is 4.6. So this issued
                    // a Goto to a destination he could not reach *while the target stood there* -
                    // a job that never completes.
                    //
                    // A job that never completes is never re-decided, so the AssaultThing duty
                    // never got to produce an AttackMelee and he simply stood next to his target.
                    // BRAWL-DIAG caught it exactly: `job=Goto jobTargetA=(133, 0, 108)` unchanged
                    // for eight seconds while the target drifted 1.4 -> 6.1 cells away.
                    //
                    // ⚠ AND IT WAS INTERMITTENT FOR A REASON THAT MADE IT WORSE OVER TIME: if the
                    // target MOVES, the cell frees, he arrives, the job ends and the fight starts
                    // normally - which is why colonists usually worked. The landing stun keeps the
                    // victim exactly where it is, so **widening and lengthening that stun on
                    // 2026-08-13 made this fire more often, not less.**
                    //
                    // This is the same rule the grab already learned the hard way ("never park a
                    // pawn in a living pawn's cell"), and BestCellBeside is the same helper the
                    // landing pounce uses - it skips cells occupied by a third pawn too.
                    IntVec3 beside = BestCellBeside(target, pawn, map);
                    if (beside.IsValid && beside != pawn.Position)
                    {
                        flyTo = beside;
                    }
                }
            }

            if ((!flyTo.IsValid || flyTo == pawn.Position) && map != null)
            {
                DovahkiinTuningDef t = Tuning;
                int radius = t != null ? t.dragonCirclingRadius : 20;
                if (radius < 4) { radius = 4; }
                circleAngle += Rand.Range(55f, 95f);
                // FindCircleCell carries its own reachable fallback and picks a cell `radius` out,
                // so it can never come back as the cell he is already standing in.
                flyTo = FindCircleCell(pawn, pawn.Position, radius, map);
            }

            if (!flyTo.IsValid || flyTo == pawn.Position)
            {
                // Walled in with nowhere reachable. A Wait we own still beats a vacuum: it is
                // 30 ticks rather than the engine's punitive 150, and the anti-hover rule will
                // pick him a fresh waypoint on the very next pass.
                return HoldStillJob();
            }

            Job go = JobMaker.MakeJob(JobDefOf.Goto, flyTo);
            go.locomotionUrgency = LocomotionUrgency.Sprint;
            return go;
        }

        /// <summary>
        /// A SHORT WAIT WE OWN. Verse.JobMaker.MakeJob(JobDef, int expiryInterval) - the same
        /// overload vanilla's own error recovery uses, at a fifth of its duration.
        /// </summary>
        private static Job HoldStillJob()
        {
            return JobMaker.MakeJob(JobDefOf.Wait, 30);
        }

        // ============================================================================================
        // THE BRAWL GAZE - A LANDED DOVAH LOOKS FOR SOMETHING TO KILL. FACING ONLY.
        // ============================================================================================
        //
        // The user, 2026-08-12: "in brawl pattern mode, he isnt just standing there when no target
        // is nearby but is still looking at the nearest available target or doing two/three facing
        // changing before taking off (I m talking about the facing only here, NO BEHAVIOR CHANGE)."
        //
        // So this touches NOTHING but which sprite faces which way. No job, no duty, no state, no
        // timing. It runs only while he is GROUNDED in the execute half of a brawl - the one moment
        // the design has him standing still on the floor with nothing in reach.
        //
        // ⚠ IT CANNOT BE DONE BY ASSIGNING pawn.Rotation. Pawn_RotationTracker.UpdateRotation runs
        // from ProcessPostTickVisuals, a SEPARATE PASS AFTER the whole tick, and re-faces any pawn
        // that has a job at that job's target. There is no point inside Tick where an assignment
        // survives to be drawn. That cost a round on the grab's shake and is written up in §5.
        //
        // The mechanism this project already trusts is SetFrozenFacing: install a Graphic_Single,
        // which returns the same material for every Rot4, so the tracker's opinion stops mattering.
        //
        // ⚠⚠ AND THE TRAP THAT COMES WITH IT: a Graphic_Single ignores Rot4 BY DESIGN, so leaving
        // one installed freezes him in that pose for ever. Whatever installs one must own taking it
        // off on EVERY exit path. That is why this is one method called unconditionally from the top
        // of RunPattern rather than something wired into the Execute branch: it re-evaluates its own
        // condition every tick and releases the moment the condition stops holding, so there is no
        // exit path for it to miss.

        /// <summary>Tick at which the look-around turns to its next facing. 0 = not looking.</summary>
        private int gazeNextTurnTick;

        /// <summary>Which way the look-around is currently pointing him.</summary>
        private int gazeFacing;

        /// <summary>True while WE have a gaze sprite installed, so we know to take it off.</summary>
        private bool gazeInstalled;

        /// <summary>
        /// Point a grounded, idle, brawling dovah at the nearest thing worth killing - or, if there
        /// is nothing at all, have him look slowly about. Installs and releases its own sprite.
        /// </summary>
        private void UpdateBrawlGaze(Pawn pawn)
        {
            bool wants = currentState == AlduinMovementState.Grounded
                && hasPattern
                && patternPhase == DragonPatternPhase.Execute
                && !DragonAttackPatterns.BreathShapeOf(currentPattern).HasValue
                // Never while the grab's shake owns the sprite slot - two authors of one graphic.
                && Find.TickManager.TicksGame >= shakeUntilTick
                && CurrentTargetOf(pawn) == null;

            if (!wants)
            {
                ReleaseBrawlGaze(pawn);
                return;
            }

            int now = Find.TickManager.TicksGame;

            // Someone to look at? Face them, and keep facing them as they move.
            Pawn nearest = NearestHostilePawn(pawn, now);
            if (nearest != null)
            {
                InstallGaze(pawn, RotToward(pawn.Position, nearest.Position));
                return;
            }

            // Nobody in sight - "two/three facing changing before taking off". A slow sweep, not a
            // spin: a fifth of a second is a twitch, two seconds reads as a head turning.
            if (gazeNextTurnTick == 0 || now >= gazeNextTurnTick)
            {
                gazeNextTurnTick = now + Rand.Range(90, 150);
                // Step by one quarter turn, direction rolled, so he looks about rather than
                // rotating like a turret.
                gazeFacing = (gazeFacing + (Rand.Bool ? 1 : 3)) % 4;
            }
            InstallGaze(pawn, new Rot4(gazeFacing));
        }

        private void InstallGaze(Pawn pawn, Rot4 rot)
        {
            if (AlduinGraphicsUtility.SetFrozenFacing(pawn, AlduinMovementState.Grounded, rot))
            {
                gazeInstalled = true;
            }
            else if (!gazeInstalled)
            {
                // SetFrozenFacing returns false when the sprite is ALREADY the one asked for, so
                // a first call that changes nothing must still be recorded as ours to undo.
                gazeInstalled = true;
            }
        }

        /// <summary>
        /// Put the ordinary four-way grounded set back. Safe to call every tick - it does nothing
        /// unless we actually installed something.
        /// </summary>
        private void ReleaseBrawlGaze(Pawn pawn)
        {
            gazeNextTurnTick = 0;
            if (!gazeInstalled)
            {
                return;
            }
            gazeInstalled = false;
            // Restore whatever set the current state calls for. SetState reference-compares, so
            // this is free when nothing needs changing.
            AlduinGraphicsUtility.SetState(pawn, currentState);
        }

        /// <summary>Cached so the pawn scan runs twice a second, not sixty times. RocketMan.</summary>
        private int gazeScanTick;
        private Pawn gazeScanResult;

        private Pawn NearestHostilePawn(Pawn pawn, int now)
        {
            if (now < gazeScanTick && gazeScanResult != null && IsEngageable(gazeScanResult))
            {
                return gazeScanResult;
            }
            gazeScanTick = now + 30;
            gazeScanResult = null;

            Map map = pawn.Map;
            if (map == null || map.mapPawns == null)
            {
                return null;
            }
            // Walk the PAWNS, not the cells - the notebook's rule for anything that scans an area.
            float best = float.MaxValue;
            List<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || !IsEngageable(other))
                {
                    continue;
                }
                if (!GenHostility.HostileTo(other, pawn))
                {
                    continue;
                }
                float d = pawn.Position.DistanceToSquared(other.Position);
                if (d < best)
                {
                    best = d;
                    gazeScanResult = other;
                }
            }
            return gazeScanResult;
        }

        /// <summary>
        /// The four-way facing that points from one cell at another. Done by hand rather than
        /// through Rot4.FromAngleFlat so there is no unverified API on a per-tick path.
        /// </summary>
        private static Rot4 RotToward(IntVec3 from, IntVec3 to)
        {
            int dx = to.x - from.x;
            int dz = to.z - from.z;
            if (Mathf.Abs(dx) > Mathf.Abs(dz))
            {
                return dx > 0 ? Rot4.East : Rot4.West;
            }
            return dz > 0 ? Rot4.North : Rot4.South;
        }

        /// <summary>
        /// WHERE AN AIRBORNE DRAGON GOES THE INSTANT HIS SWING IS TAKEN AWAY.
        ///
        /// ⚠⚠ THIS IS NOT POLISH. WITHOUT IT, CANCELLING THE MELEE IS A JOB-THRASH THAT RIMWORLD
        /// PUNISHES BY FREEZING HIM FOR 2.5 SECONDS. Diagnosed 2026-08-12 from Player.log, which
        /// was full of:
        ///
        ///     Dovahkiin_Alduin_Test55060 started 10 jobs in 10 ticks.
        ///     List: (AttackMelee) , (AttackMelee) , (AttackMelee) , ...
        ///
        /// The loop, decompiled rather than guessed:
        ///
        ///   1. our CompTick cancels the melee with `startNewJob: false`, leaving curJob NULL.
        ///      CompTick runs from Pawn.Tick's base.Tick(), BEFORE jobs.JobTrackerTick().
        ///   2. Pawn_JobTracker.JobTrackerTick then hits
        ///        `if (curJob == null && !pawn.Dead && pawn.mindState.Active) TryFindAndStartJob();`
        ///      which runs the WHOLE Animal think tree from the top - and the retaliation node
        ///      sits ABOVE LordDuty, so it hands back AttackMelee. That counts as a job given.
        ///   3. Repeat every tick. At ten, Pawn_JobTracker.FinalizeTick sums the last ten ticks,
        ///      finds >= 10, and calls JobUtility.TryStartErrorRecoverJob - which does
        ///      `Log.Error(...)` (the spam the user saw) and then
        ///      `StartJob(JobMaker.MakeJob(JobDefOf.Wait, 150))`.
        ///
        /// **That forced Wait is 150 ticks - 2.5 seconds of a dragon standing still in mid-air**,
        /// and it is exactly what HOVER-DIAG recorded: `job=Wait patherMoving=False
        /// curPathNull=True` with stationaryTicks climbing 29 -> 58 -> 87 during Leave. So the log
        /// spam and the stalls were never two problems. They are one, and the freeze is RimWorld
        /// disciplining us for the thrash.
        ///
        /// THE FIX IS TO STOP CREATING THE VACUUM, not to cancel more cleverly. Filling curJob in
        /// the same tick means step 2 never runs, so the think tree is never asked, so there is
        /// nothing to fight. Damage cannot reopen it either: Pawn_JobTracker.Notify_DamageTaken
        /// gates CheckForJobOverride behind `TicksGame >= lastDamageCheckTick + 180`, so being hit
        /// can cost at most one job every three seconds - nowhere near the detector's threshold.
        ///
        /// This is the notebook's own rule about the circling pause, one layer down: **if a
        /// hand-off pauses, hand off BEFORE the current step completes.** A gap does not stay
        /// empty; something else fills it.
        ///
        /// He goes to the circling waypoint when there is one - peeling away from what he was
        /// swinging at is what SPEC.md 6.5 wants anyway - and otherwise to a fresh circle cell
        /// around himself, which carries FindCircleCell's own reachable fallback.
        /// </summary>
        private IntVec3 FlyAwayCell(Pawn pawn)
        {
            if (circleWaypoint.IsValid && circleWaypoint != pawn.Position)
            {
                return circleWaypoint;
            }
            Map map = pawn.Map;
            if (map == null)
            {
                return IntVec3.Invalid;
            }
            DovahkiinTuningDef t = Tuning;
            int radius = t != null ? t.dragonCirclingRadius : 20;
            if (radius < 4) { radius = 4; }
            circleAngle += Rand.Range(55f, 95f);
            return FindCircleCell(pawn, pawn.Position, radius, map);
        }

        /// <summary>
        /// Has he been standing still longer than the grace allows? `stationarySinceTick` is
        /// maintained every tick by ApplyStateFacts' flight branch.
        /// </summary>
        private bool StationaryTooLong(DovahkiinTuningDef t)
        {
            if (stationarySinceTick < 0)
            {
                return false;
            }
            float graceSeconds = t != null ? t.dragonFlightStationaryGraceSeconds : 1.5f;
            return Find.TickManager.TicksGame - stationarySinceTick
                >= (int)(graceSeconds * TicksPerRealSecond);
        }

        /// <summary>Where he is flying on this leg of the circle. Invalid means "pick one".</summary>
        private IntVec3 circleWaypoint = IntVec3.Invalid;

        /// <summary>Where he is around the circle, in degrees. Advanced a big step per leg.</summary>
        private float circleAngle;

        // ============================================================================================
        // THE SHAPE OF THE CIRCUIT - REDESIGNED 2026-08-13 FROM THE USER'S DRAWING
        // ============================================================================================
        // They drew it: a ragged red path against the clean blue loop they wanted. The red had two
        // faults and only one of them was the grid.
        //
        //   1. **THE CENTRE MOVED.** UpdateCirclingWaypoint was handed `target.Position` EVERY TICK,
        //      so a target that walked dragged the whole ring with it. Waypoints computed against a
        //      sliding centre do not lie on one circle, and when the centre moved toward him the
        //      "ring" swept past his own position - which is the big inward excursion in the red
        //      drawing, the path folding back through the middle.
        //   2. **THE STEP WAS RANDOM (55-95 degrees) AND SO WAS THE DIRECTION.** Consecutive legs
        //      of unequal length, occasionally reversing, cannot close into a loop.
        //
        // Both are now fixed for the whole leg: the centre is captured ONCE at BeginLeave, the
        // rotation direction is rolled ONCE, and the step is a constant. At 45 degrees that is a
        // regular octagon - which is what the blue drawing is - with legs of
        // 2*r*sin(22.5) = 10.7 cells at radius 14.
        //
        // ⚠ THE CENTRE IS DELIBERATELY STALE. A leg lasts 17 seconds and the target may well move
        // during it; letting the circuit follow is exactly the bug above. He is orbiting the PLACE
        // the fight is, and re-centres on the next pattern.

        /// <summary>Centre of the current circuit. Captured once per Leave, never chased.</summary>
        private IntVec3 circleCentre = IntVec3.Invalid;

        /// <summary>Which way round this circuit goes. Rolled once per Leave, then held.</summary>
        private bool circleClockwise;

        /// <summary>
        /// WHERE HE LAST SAW SOMETHING WORTH ATTACKING. The centre of the circling leg once the
        /// target is gone - and a breath very often kills its own target, so "gone" is the normal
        /// case, not an edge one. Saved, or a reload mid-leg would re-centre the circle on him.
        /// </summary>
        private IntVec3 lastTargetPos = IntVec3.Invalid;

        // ============================================================================================
        // THE CIRCLING AUDIO, AND WHY THE PHASE LENGTH IS DERIVED FROM IT
        // ============================================================================================
        // The user, 2026-08-13: "atune the duration of flight circling to the length of the audio who
        // is the longest between 1&2 IF it is possible to make a dovah alternate between those two
        // sounds… Dont forget to take in account dragonTakeoff's duration too, the sounds should be,
        // if from ground, dragontakeoff THEN dragonflightcircling."
        //
        // Alternating IS possible - it is a saved counter - so both clips are used and the phase is
        // cut to the LONGER of the two. Measured off the files rather than estimated:
        //
        //     DragonTakeOff.wav          2.237s   =  135 ticks (rounded up)
        //     DragonFlightCircling1.mp3 15.192s   =  912 ticks   } identical lengths,
        //     DragonFlightCircling2.mp3 15.192s   =  912 ticks   } 633 MPEG frames each
        //
        // So a leg from the ground is 135 + 912 = 1047 ticks and the audio ends exactly as the next
        // attack begins.
        //
        // ⚠ THE PHASE LENGTH IS `Max(dragonPatternLeaveTicks, takeoff + circling)`, NOT the sum on
        // its own. Two numbers that must agree, held in two places, is this project's most-repeated
        // failure - so the phase is DERIVED from the clip lengths and cannot be shorter than them,
        // while the user's existing knob can still make circling longer if they want more of it.
        // Swap the audio and change one number; nothing can silently truncate a clip.

        /// <summary>Which circling clip is next. Saved, or a reload replays the same one for ever.</summary>
        private bool circlingSoundAlternate;

        /// <summary>
        /// Tick at which the circling clip starts. Set to now + take-off length when the leg begins
        /// from the ground so the two play in sequence, or to now when he is already airborne.
        /// 0 = nothing pending. Saved: a reload mid-delay would otherwise drop the clip entirely.
        /// </summary>
        private int circlingSoundAtTick;

        /// <summary>
        /// Did the current leg start with a take-off? Set by EnterState, consumed by BeginLeave.
        /// </summary>
        private bool tookOffFromGround;

        /// <summary>
        /// THE WIND-UP. While this tick is in the future he has landed (or is hanging in soar) and
        /// is drawing breath; when it arrives, the jet fires. 0 = nothing armed.
        ///
        /// **Saved.** A game reloaded during the four-second pause would otherwise lose the breath
        /// entirely and the pattern would play as "land, wait, leave" - which is almost exactly the
        /// defect this session already spent a round on from a different cause. **A pending action
        /// that lives only in memory is a pending action a save can delete.**
        /// </summary>
        private int pendingBreathTick;

        /// <summary>Which shape the armed breath will use. Saved alongside the tick above.</summary>
        private DragonBreathShape pendingBreathShape;

        /// <summary>
        /// TRUE FOR THE ONE TICK BETWEEN A DIVE DECIDING TO LAND AND THE LANDING HAPPENING.
        /// The only thing that authorises a landing impact - see EnterState.
        ///
        /// Deliberately NOT saved: it lives for a single tick inside one method call, and a save
        /// cannot land between the two. A flag that cannot survive a save is better than one that
        /// can be reloaded in a state nothing will ever clear.
        /// </summary>
        private bool divePending;

        /// <summary>
        /// CIRCLE THE FIGHT IN STRAIGHT LEGS, and never hang still while doing it.
        ///
        /// Both of the user's remaining complaints are answered here by the same mechanism:
        ///
        ///   "flying in straight lines not change directions every 2-3 cells" - each leg is ONE
        ///   travel destination, so the path between waypoints is a straight run. The angle steps
        ///   55-95 degrees per leg, which is a real turn rather than a wobble.
        ///
        ///   "we need something that FORCES him to either always move while in flight or never be
        ///   in flight while motionless" - if he is stationary past the grace, that alone picks a
        ///   NEW waypoint. He cannot sit still in flight, because standing still is precisely the
        ///   condition that gives him somewhere new to be.
        /// </summary>
        private void UpdateCirclingWaypoint(Pawn pawn, IntVec3 centre, DovahkiinTuningDef t)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return;
            }
            int radius = t != null ? t.dragonCirclingRadius : 20;
            if (radius < 4) { radius = 4; }

            // ⚠ RE-TARGET BEFORE HE ARRIVES, NOT ON ARRIVAL. The user, 2026-08-06: "in his flight
            // circling phase he sometimes stays motionless for a brief moment before changing
            // trajectory."
            //
            // That pause was structural. Waiting for arrival meant the goto job COMPLETED on its
            // own first - and between a finished job and our next-tick waypoint the think tree
            // handed him something idle, which is a visible stop. Switching the destination while
            // he is still flying means the old job never finishes: he simply turns.
            //
            // 40% of the radius, so the turn happens well out from the corner rather than at it.
            float retargetAt = Mathf.Max(5f, radius * 0.4f);
            bool arrived = circleWaypoint.IsValid && pawn.Position.DistanceTo(circleWaypoint) <= retargetAt;
            bool stuck = StationaryTooLong(t);

            if (!circleWaypoint.IsValid || arrived || stuck)
            {
                // ⚠ A CONSTANT STEP, ALWAYS THE SAME WAY ROUND. This was Rand.Range(55f, 95f) with
                // no direction held, and legs of unequal length that sometimes reversed cannot
                // close into a loop - see the block comment on circleCentre and the user's drawing.
                // A fixed step is a regular polygon; 45 degrees is the octagon they drew.
                float step = t != null ? t.dragonCirclingStepDegrees : 90f;
                circleAngle += circleClockwise ? -step : step;
                circleWaypoint = FindCircleCell(pawn, centre, radius, map);
                stationarySinceTick = -1;
            }

            if (circleWaypoint.IsValid)
            {
                // ⚠⚠ THE STOPS WERE RIMWORLD'S, ON PURPOSE, AND THIS IS THE ONE LINE THAT ENDS
                // THEM. The user: "he keeps making stops during his flight", and the HOVER-DIAG
                // log showed job=Wait_Wander throughout the circling.
                //
                // JobGiver_GotoTravelDestination opens with:
                //     pawn.mindState.nextMoveOrderIsWait = !pawn.mindState.nextMoveOrderIsWait;
                //     if (nextMoveOrderIsWait && !exactCell) return a Wait_Wander job (30-80 ticks);
                //
                // It ALTERNATES travelling with waiting by design - right for a caravan ambling
                // across a map, wrong for a dragon. `exactCell` would disable it, but that is a
                // field on the job giver set in the DutyDef, not something a duty can pass.
                //
                // So we set the flag TRUE and let the giver's own flip turn it false: every order
                // it produces is then a travel order. CompTick runs from Pawn.Tick's base.Tick(),
                // BEFORE jobs.JobTrackerTick(), so the value is always correct when the giver next
                // runs.
                if (pawn.mindState != null)
                {
                    pawn.mindState.nextMoveOrderIsWait = true;
                }
                DovahFactionUtility.SetDuty(pawn, DutyDefOf.TravelOrWait, circleWaypoint, 0f);
            }
        }

        /// <summary>
        /// A REACHABLE standable cell on the circle at the current angle, trying angles outward
        /// from it before giving up. Returns Invalid only if the whole ring is unusable.
        ///
        /// ⚠ REACHABILITY IS THE POINT, AND ITS ABSENCE WAS A REAL BUG. The waypoint was picked
        /// geometrically - in bounds and standable - and handed to the travel duty. But
        /// JobGiver_GotoTravelDestination opens with
        /// `if (!pawn.CanReach(destination, PathEndMode.OnCell, ...)) return null;`, so a cell
        /// across a wall or in another room produced NO JOB AT ALL. He then stood still until the
        /// stuck-timer rescued him a second and a half later - which is precisely the "he keeps
        /// making stops during his flight" the HOVER-DIAG log caught, showing `job=Wait` with
        /// stationaryTicks climbing to 87.
        ///
        /// Danger.Deadly because a dovah does not route around a fire.
        /// </summary>
        /// <summary>
        /// Which way he is going right now, in degrees, or -1 if he is not going anywhere - in
        /// which case there is no turn to limit and any waypoint is fair.
        ///
        /// Measured over the whole remaining path rather than off `nextCell`, for exactly the
        /// reason the flight SPRITE does: one cell of a staircase is not a heading. See
        /// AlduinGraphicsUtility.HeadingOctant.
        /// </summary>
        private static float CurrentHeadingDegrees(Pawn pawn)
        {
            Pawn_PathFollower pather = pawn == null ? null : pawn.pather;
            if (pather == null || !pather.Moving || !pawn.Spawned)
            {
                return -1f;
            }
            IntVec3 ahead = pather.nextCell;
            PawnPath path = pather.curPath;
            if (path != null && path.NodesLeftCount > 1)
            {
                ahead = path.Peek(Mathf.Min(6, path.NodesLeftCount - 1));
            }
            Vector3 delta = (ahead - pawn.Position).ToVector3();
            if (delta.sqrMagnitude < 1f)
            {
                return -1f;
            }
            return delta.AngleFlat();
        }

        private IntVec3 FindCircleCell(Pawn pawn, IntVec3 centre, int radius, Map map)
        {
            // ⚠ NO HANDBRAKE TURNS. The user, 2026-08-12: "prevent him from making aggressive turns
            // like North -> north-east -> south".
            //
            // circleAngle always advances the same way round, so the WAYPOINTS progress smoothly -
            // but the HEADING to the next one depends on where he happens to be standing when it is
            // chosen, and from the middle of a circuit that can be most of a reversal. So each
            // candidate is also judged on the turn it would demand of him.
            //
            // Two passes rather than one loop with a relaxing limit: the first tries every angle
            // WITH the turn limit, the second drops it. That way a cramped map degrades to the old
            // behaviour instead of stalling, and the limit never costs him a waypoint he needs.
            DovahkiinTuningDef tune = Tuning;
            float maxTurn = tune != null ? tune.dragonMaxTurnDegrees : 110f;
            float currentHeading = CurrentHeadingDegrees(pawn);

            for (int pass = 0; pass < 2; pass++)
            {
                bool limitTurn = pass == 0 && maxTurn < 180f && currentHeading >= 0f;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float a = (circleAngle + attempt * 45f) * Mathf.Deg2Rad;
                    IntVec3 c = centre + new IntVec3(
                        Mathf.RoundToInt(Mathf.Cos(a) * radius), 0,
                        Mathf.RoundToInt(Mathf.Sin(a) * radius));
                    if (!c.InBounds(map) || !c.Standable(map)
                        || !pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                    {
                        continue;
                    }
                    if (limitTurn)
                    {
                        Vector3 leg = (c - pawn.Position).ToVector3();
                        // A waypoint he is already standing on says nothing about a heading.
                        if (leg.sqrMagnitude < 4f)
                        {
                            continue;
                        }
                        float legHeading = leg.AngleFlat();
                        if (Mathf.Abs(Mathf.DeltaAngle(currentHeading, legHeading)) > maxTurn)
                        {
                            continue;
                        }
                    }
                    circleAngle += attempt * 45f;
                    return c;
                }
            }
            // Nowhere on the ring is reachable - a walled-in target, say. Fall back to a cell he
            // certainly CAN get to, so he keeps flying instead of standing there.
            IntVec3 near;
            if (CellFinder.TryFindRandomReachableCellNear(pawn.Position, map, radius,
                    TraverseParms.For(pawn), c => c.Standable(map), null, out near))
            {
                return near;
            }
            return IntVec3.Invalid;
        }

        private void BeginPattern(Pawn pawn, DovahkiinTuningDef t, int now)
        {
            // NEVER THE SAME ATTACK TWICE RUNNING - the user's rule. At three selectable
            // patterns an unconstrained roll would repeat about a third of the time, and a
            // dragon that dives twice in a row reads as the AI being stuck rather than choosing.
            // A GROUNDED PATTERN NEEDS SOMEWHERE TO STAND, AND UNDER A ROOF THERE IS NOWHERE.
            // Against an indoor target this leaves the hover breath, which is the better attack
            // anyway: he stays up and breathes down at them instead of walking into the house.
            currentPattern = DragonAttackPatterns.SelectNext(lastPattern,
                GroundedPatternsViable(pawn));
            lastPattern = currentPattern;
            hasPattern = true;
            patternPhase = DragonPatternPhase.Approach;
            phaseUntilTick = now + (t != null ? t.dragonPatternApproachTimeoutTicks : 1500);
            if (currentState != AlduinMovementState.Flight)
            {
                EnterState(pawn, AlduinMovementState.Flight);
            }
        }

        private void BeginExecute(Pawn pawn, DovahkiinTuningDef t, int now, Thing target)
        {
            patternPhase = DragonPatternPhase.Execute;
            phaseUntilTick = now + DragonAttackPatterns.ExecuteTicksOf(currentPattern, t);

            DragonBreathShape? shape = DragonAttackPatterns.BreathShapeOf(currentPattern);
            if (shape.HasValue)
            {
                // ⚠ THE BREATH NO LONGER FIRES HERE. It is armed, and UpdatePendingBreath fires it
                // dragonBreathWindupTicks later. The user, 2026-08-13: "The breaths 2 patterns
                // should has a 4s real life pause before firing off… flight circling -> Land ->
                // wait 4s -> Launch breathing -> Xs of pause -> flight circling."
                //
                // ⚠⚠ THE STATE MUST BE ENTERED NOW, NOT WITH THE BREATH. BreatheAt sets the state
                // itself, so leaving it to fire would mean he spends the wind-up still FLYING and
                // then snaps to the ground as the jet starts - the opposite of "land, wait, then
                // breathe". Entering here is what makes the pause read as a dragon settling and
                // drawing breath.
                AlduinMovementState breatheIn = DragonAttackPatterns.ExecuteStateOf(currentPattern);
                if (currentState != breatheIn)
                {
                    EnterState(pawn, breatheIn);
                }
                pendingBreathTick = now + (t != null ? t.dragonBreathWindupTicks : 240);
                pendingBreathShape = shape.Value;

                // ⚠ AND HE DOES NOT MOVE UNTIL IT IS OVER. The user, 2026-08-06: "he directly
                // moves after a breath, he is supposed to stay motionless untill it's over."
                //
                // The Defend duty was not enough: its think node is JobGiver_AIDefendPoint at
                // targetAcquireRadius 65, so it happily sends him at anything nearby. A STUN is
                // the engine's own movement gate (PatherTick returns on stances.FullBodyBusy) and
                // is the only thing a duty cannot argue with.
                //
                // ONE call for the WHOLE phase - breath plus the 2-second tail - not a rolling
                // window. A rolling stun outlived its purpose once already and left him frozen in
                // flight afterwards; stunning for exactly the phase length cannot leak.
                int holdTicks = DragonAttackPatterns.ExecuteTicksOf(currentPattern, t);
                HoldStill(pawn, holdTicks);

                // ⚠ AND HIS FACING IS LOCKED TOO. The user: "he respects the no move rule after a
                // breath yes, but he changes sprite direction nonetheless."
                //
                // Assigning pawn.Rotation cannot achieve this - Pawn_RotationTracker.UpdateRotation
                // runs AFTER the whole tick and re-faces anything with a job, so no assignment
                // survives to be drawn. Installing a Graphic_Single makes Rot4 irrelevant instead,
                // which is the same mechanism the grab's shake uses.
                //
                // Routed through the flight comp's shake-profile slot so the RESTORE is already
                // handled - it heals itself from CompTick even if the breath ends unusually.
                // ⚠⚠ THE FACING FREEZE IS **NOT** DONE HERE ANY MORE - IT MOVED TO THE MOMENT THE
                // JET FIRES. Leaving it here was a bug I introduced with the wind-up, and the user
                // caught it on the next playtest: *"He was facing east yet fired his ground-breathing
                // south-west."*
                //
                // BreatheAt is what AIMS him - it sets pawn.Rotation from the target - and it now
                // runs four seconds after this point. Freezing the sprite here captured the facing
                // he happened to LAND with, so the locked sprite and the cone pointed different ways
                // for the whole breath.
                //
                // **A freeze must be taken AFTER the thing that decides what is being frozen.**
                // Before the wind-up existed these were the same instant, which is exactly why the
                // ordering survived unnoticed until a delay was inserted between them.
                //
                // Leaving him unfrozen through the wind-up is also better than freezing him early:
                // he turns to face his target as he settles, which is what a dragon drawing breath
                // should look like.
                return;
            }

            // DIVE AND BRAWL. Entering Grounded from the air is what fires the pounce, the dust
            // and the landing stun - the arrival IS the attack's opening.
            //
            // ⚠ `divePending` is THE ONLY THING THAT AUTHORISES AN IMPACT, and this is its only
            // assignment. Set it immediately before the transition, never earlier: EnterState
            // consumes and clears it, so anything that grounds him between here and there would
            // otherwise steal the impact meant for this dive.
            if (currentState != AlduinMovementState.Grounded)
            {
                divePending = true;
                EnterState(pawn, AlduinMovementState.Grounded);
            }
        }

        private void BeginLeave(Pawn pawn, DovahkiinTuningDef t, int now)
        {
            patternPhase = DragonPatternPhase.Leave;

            int takeOffTicks = t != null ? t.dragonTakeOffSoundTicks : 135;
            int circlingTicks = t != null ? t.dragonCirclingSoundTicks : 912;
            int knob = t != null ? t.dragonPatternLeaveTicks : 900;
            // Derived, never merely configured - see the block comment on circlingSoundAtTick.
            // The leg is at least long enough for take-off plus a whole circling clip, so the audio
            // can never be cut off by a number somebody forgot to update.
            phaseUntilTick = now + Mathf.Max(knob, takeOffTicks + circlingTicks);

            // Forget the old leg so the first waypoint of this circuit is chosen fresh, rather
            // than resuming a heading picked before the attack.
            circleWaypoint = IntVec3.Invalid;

            // ⚠ THE CIRCUIT IS FIXED HERE AND NOWHERE ELSE - centre, direction, and the angle it
            // starts from. Capturing them once per leg is what turns the user's ragged red path
            // into their blue loop; see the block comment on circleCentre.
            circleCentre = lastTargetPos.IsValid ? lastTargetPos : pawn.Position;
            circleClockwise = Rand.Bool;
            // Start the circuit from where he ALREADY IS relative to the centre, so the first leg
            // continues his approach instead of doubling back to an arbitrary angle.
            Vector3 fromCentre = (pawn.Position - circleCentre).ToVector3();
            circleAngle = fromCentre.sqrMagnitude > 1f ? fromCentre.AngleFlat() : Rand.Range(0f, 360f);

            // ⚠ ENTER FLIGHT *BEFORE* SCHEDULING, NOT AFTER. EnterState is what fires the take-off
            // sound and sets tookOffFromGround, so reading that flag first would always see the
            // PREVIOUS leg's answer.
            bool wasGrounded = currentState == AlduinMovementState.Grounded;
            tookOffFromGround = false;
            if (currentState != AlduinMovementState.Flight)
            {
                EnterState(pawn, AlduinMovementState.Flight);
            }

            // "if from ground, dragontakeoff THEN dragonflightcircling" - so the circling clip waits
            // out the take-off rather than playing over it. Already airborne, it starts at once.
            circlingSoundAtTick = now + ((wasGrounded || tookOffFromGround) ? takeOffTicks : 0);
        }

        /// <summary>
        /// Decide what he should be doing, and move him to it. SPEC.md 6.5c.
        /// The FACTS run every tick in ApplyStateFacts; this is the deliberate half.
        /// </summary>
        private void UpdateState(Pawn pawn)
        {
            RunPattern(pawn);
        }

        // ⚠ THE OLD PER-INTERVAL STATE RHYTHM WAS DELETED HERE ON 2026-08-06, NOT DISABLED.
        //
        // It rolled every interval for which movement state to be in, with a dwell timer to stop
        // the flicker. Its dwell caused FIVE separate defects across three sessions - hovering in
        // flight, meleeing from the air, carrying a grabbed pawn off the map, jogging after
        // fleeing colonists for nine grounded seconds, and an anti-hover check that took 2.5s to
        // fire against a 1.5s grace.
        //
        // The user's call, and the reasoning is the important part: a dwell timer and an attack
        // pattern are TWO AUTHORS OF ONE DECISION. Patterns now own the state while they run, so
        // the rhythm had to go rather than be tuned - leaving it behind a flag would have kept
        // both authors in the file. RunPattern replaces it entirely.
        //
        // Do not reintroduce a global "x seconds in this state". Per-pattern timings live in
        // DovahkiinTuningDef as dragonPattern*Ticks.

        private void EnterState(Pawn pawn, AlduinMovementState state)
        {
            AlduinMovementState from = currentState;
            currentState = state;
            stateLockedUntilTick = Find.TickManager.TicksGame + DwellTicksFor(state);
            stationarySinceTick = -1;
            AlduinGraphicsUtility.SetState(pawn, state);
            ApplySpeed(pawn, state);

            // THE LANDING. Fires coming down from EITHER airborne state, and after the
            // 2026-08-04 redesign the important one is FLIGHT -> GROUNDED: that is him dropping
            // out of the sky onto the target he has been chasing, which is what the user asked
            // for ("inciting them to land on/close their target, switching directly from flight
            // to grounded"). Soar -> Grounded is the gentler version of the same thing.
            //
            // Gated on the SOURCE state, not on "is now grounded", so a take-off never throws
            // dust.
            // ⚠⚠ ONLY A DELIBERATE DIVE MAKES AN IMPACT. `divePending` is set by BeginExecute
            // immediately before it grounds him for a dive-and-brawl, and cleared here.
            //
            // Without that gate this fired on EVERY airborne-to-grounded transition - and most
            // groundings are not dives at all. They are FACTS reacting to circumstance: too hurt to
            // fly, standing in melee, or, as the user found on 2026-08-13, **being lassoed by giant
            // skeletons and dragged across the map.** Each yank produced a full landing: 74 dust
            // puffs and a two-second stun over 4.4 cells, on everything nearby including the
            // colony's own pawns. The opaque dust plates were the visible half of a stun-spam.
            //
            // The roping itself is now refused outright (Patch_DovahCannotBeRoped), but that fixed
            // one cause of a general fault: **an impact is something he DOES, not something that
            // happens to him.** Anything that can force him to the ground - a mod, a spell, a future
            // rule of ours - would otherwise trigger it again.
            LandedThisTransition = divePending
                && state == AlduinMovementState.Grounded
                && (from == AlduinMovementState.Soar || from == AlduinMovementState.Flight);
            divePending = false;
            if (LandedThisTransition)
            {
                // The pounce needs the target BEFORE the impact, so the stun is centred where he
                // actually comes down rather than where he happened to be flying.
                DoLandingImpact(pawn, IsHeldGrounded ? null : CurrentTargetOf(pawn));
            }

            // ⚠ TAKE-OFF IS THE MIRROR OF THE LANDING AND IS GATED THE SAME WAY - on the SOURCE
            // state, not on "is now airborne". Grounded -> Flight and Grounded -> Soar both count;
            // Soar <-> Flight does not, because he never leaves the air. The user's words:
            // "whenever the dragon's goes from ground to either flight or soar".
            //
            // Positional and short (2.24s), so unlike the circling loop there is no drift problem:
            // he is standing still on the ground at the moment it plays.
            if (from == AlduinMovementState.Grounded && state != AlduinMovementState.Grounded)
            {
                PlayDovahSound(pawn, "Dovahkiin_DragonTakeOff", pawn.Position);
                tookOffFromGround = true;
            }
        }

        /// <summary>
        /// ⚠ TEMPORARY DIAGNOSTIC - `BRAWL-DIAG`. DELETE ONCE PATTERN 1 IS PROVEN.
        ///
        /// **THIS EXISTS BECAUSE THREE FIXES IN A ROW DID NOT SETTLE IT.** Each found a real defect
        /// - a fact vetoing the pattern executor, a downed target leaving nothing to assault, and a
        /// wild animal that could not be re-acquired because it was hostile to nobody - and after
        /// each one the user reported *"still not attacking, sometimes"*.
        ///
        /// `CLAUDE.md` and the notebook both say it plainly: **if a fix does not work twice, stop
        /// and question the diagnosis rather than trying a third variation. Instrument on the SECOND
        /// failure at the latest.** This is already the fourth. The record shows a single
        /// `HOVER-DIAG` line settling a symptom that had survived three wrong fixes; this is the
        /// same move for the brawl.
        ///
        /// `HOVER-DIAG` cannot see this: it only samples a pawn stationary IN FLIGHT, and every
        /// report here is of a dragon standing on the GROUND.
        ///
        /// It prints once a second through any brawl Execute - working or not - because the useful
        /// comparison is between a brawl that attacks and one that does not, and *"sometimes he
        /// did"* means both will appear in one log.
        /// </summary>
        private void BrawlDiagnostic(Pawn pawn)
        {
            if (!hasPattern || patternPhase != DragonPatternPhase.Execute
                || DragonAttackPatterns.BreathShapeOf(currentPattern).HasValue)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (now % 60 != 0)
            {
                return;
            }

            Thing target = CurrentTargetOf(pawn);
            Pawn nearest = NearestHostilePawn(pawn, now);
            PawnDuty duty = pawn.mindState != null ? pawn.mindState.duty : null;
            Thing rawEnemy = pawn.mindState != null ? pawn.mindState.enemyTarget : null;

            Log.Message("[Dovahkiin] BRAWL-DIAG"
                + " phaseIn=" + (phaseUntilTick - now)
                + " state=" + currentState
                + " | duty=" + (duty == null ? "NONE" : duty.def.defName
                    + " focus=" + duty.focus.ToStringSafe())
                + " | job=" + (pawn.CurJobDef == null ? "none" : pawn.CurJobDef.defName)
                + " jobTargetA=" + (pawn.CurJob == null ? "none" : pawn.CurJob.targetA.ToStringSafe())
                + " | target=" + (target == null ? "NULL" : target.LabelShort
                    + " dist=" + pawn.Position.DistanceTo(target.Position).ToString("F1"))
                // Why it is null matters more than that it is null: enemyTarget surviving while
                // CurrentTargetOf rejects it means IsEngageable is the one saying no.
                + " | rawEnemyTarget=" + (rawEnemy == null ? "null"
                    : rawEnemy.LabelShort + " engageable=" + IsEngageable(rawEnemy)
                      + (rawEnemy is Pawn ? " downed=" + ((Pawn)rawEnemy).Downed
                         + " dead=" + ((Pawn)rawEnemy).Dead : " notPawn"))
                + " | nearestHostile=" + (nearest == null ? "none"
                    : nearest.LabelShort + " dist=" + pawn.Position.DistanceTo(nearest.Position).ToString("F1"))
                + " | fullBodyBusy=" + (pawn.stances != null && pawn.stances.FullBodyBusy)
                + " stunned=" + (pawn.stances != null && pawn.stances.stunner != null
                    && pawn.stances.stunner.Stunned)
                + " stance=" + (pawn.stances == null || pawn.stances.curStance == null
                    ? "none" : pawn.stances.curStance.GetType().Name)
                + " | heldGrounded=" + IsHeldGrounded
                + " gazeInstalled=" + gazeInstalled
                + " downed=" + pawn.Downed);
        }

        /// <summary>
        /// BEING LANDED ON IS AN ACT OF AGGRESSION. THE VICTIM FIGHTS BACK.
        ///
        /// The user's diagnosis, 2026-08-13, and it went straight to the cause rather than the
        /// symptom: *"he landed on one of those flying around bugs but the bug just resumed it's
        /// way and the dovah just stood there doing nothing since his target was out of reach."*
        ///
        /// **This is this project's oldest gotcha biting from a new angle: A WILD ANIMAL IS HOSTILE
        /// TO NOBODY.** `GenHostility.HostileTo` is true only for faction hostility, a manhunter
        /// mental state, a predator hunting, a prison break or a slave rebellion - and a wild insect
        /// that has just been flattened by a dragon is none of those. So the moment it wandered off:
        ///
        ///   * `CurrentTargetOf` went null (it was only ever his target via `mindState.enemyTarget`)
        ///   * the brawl retarget could not find it either, because that scan filters on `HostileTo`
        ///   * so the brawl had nothing to attack and he stood in the crater
        ///
        /// **Making the stun aggressive fixes both halves with one change:** a manhunting animal is
        /// `ForceHostileTo` everything, so it both fights back AND becomes a legal target again.
        ///
        /// ⚠ VANILLA'S OWN PATH CANNOT DO THIS FOR US. `Pawn_MindState.Notify_DamageTaken` only
        /// starts manhunter when the instigator's faction is `humanlikeFaction` or the instigator's
        /// race intelligence is >= 1. Our dragon is an animal in a hidden faction, so it fails both
        /// tests - which is exactly why being hit by him never provoked anything.
        ///
        /// So the mental state is started directly. `MentalStateHandler.TryStartMentalState` is
        /// public and `MentalStateDefOf.Manhunter` exists in 1.4 (both verified).
        ///
        /// ⚠ ANIMALS ONLY, AND **NEVER ON A DRAGON**. A humanlike is already hostile through the
        /// dov faction and needs nothing; starting a mental state on a dovah is the mistake that
        /// cost four rounds in 2026-08-06 (a mental state outranks his own LordDuty and the whole
        /// pattern system stops working). Both are excluded explicitly rather than by luck.
        /// </summary>
        private static void MakeItPersonal(Pawn victim, Pawn dragon)
        {
            if (victim == null || dragon == null || victim.Dead || victim.mindState == null)
            {
                return;
            }
            // Remember who did it whatever kind of pawn this is - it is what makes him engage
            // rather than resume whatever he was doing.
            victim.mindState.enemyTarget = dragon;

            // ⚠ NEVER a dragon, and never anything that is not an animal.
            if (DragonAirborneCheck.IsDragon(victim) || victim.RaceProps == null
                || !victim.RaceProps.Animal || victim.Faction != null)
            {
                return;
            }
            if (victim.mindState.mentalStateHandler == null
                || victim.mindState.mentalStateHandler.InMentalState)
            {
                return;
            }
            victim.mindState.mentalStateHandler.TryStartMentalState(
                MentalStateDefOf.Manhunter, null, false, false, dragon);
        }

        /// <summary>
        /// ⚠⚠ A DOVAH IN FLIGHT IS NEVER STOPPED BY ANYTHING OUTSIDE HIMSELF.
        ///
        /// **THE USER'S INVIOLABLE RULE, 2026-08-13:** *"they aren't and NEVER ARE stoppable or
        /// movable by an external factor during their flight course."*
        ///
        /// Reported as *"got stuck midflight multiple times because of incoming attacks (he gets
        /// blocked for a brief moment until he keeps on moving again)"*, and it cost him a whole
        /// skirmish: over twenty seconds locked in circling because every hit stole another beat.
        ///
        /// **The mechanism is the STANCE, not the damage.** `Pawn_PathFollower.PatherTick` opens
        /// with `if (pawn.stances.FullBodyBusy) return;`, and being struck puts a pawn into a busy
        /// stance. Each blow therefore froze him for its duration - imperceptible on a colonist,
        /// ruinous for a creature whose whole design says flight is never motionless.
        ///
        /// So while airborne, any busy stance that is not one of OURS is cleared on the tick it
        /// appears. `CancelBusyStanceSoft` is the same call the melee cancel already uses.
        ///
        /// ⚠ OUR OWN HOLDS MUST SURVIVE THIS, which is why it is gated on the two things that
        /// legitimately pin him: the post-breath hold (a stun we raise deliberately) and a grab.
        /// Clearing indiscriminately would delete the "motionless after a breath" beat the user
        /// asked for twice - the same trap the stun-immunity patch had to dodge.
        /// </summary>
        private void KeepFlightUninterruptible(Pawn pawn)
        {
            if (currentState == AlduinMovementState.Grounded || pawn.stances == null)
            {
                return;
            }
            // Our own deliberate holds: the breath's motionless beat, and anything in his jaws.
            if (IsHeldGrounded || Find.TickManager.TicksGame < stateHoldUntilTick
                || pendingBreathTick != 0)
            {
                return;
            }
            if (pawn.stances.curStance is Stance_Busy)
            {
                pawn.stances.CancelBusyStanceSoft();
            }
        }

        /// <summary>
        /// FIRE THE ARMED BREATH WHEN ITS WIND-UP EXPIRES.
        ///
        /// ⚠ CALLED FROM THE UNCONDITIONAL BLOCK IN CompTick, NOT FROM RunPattern - deliberately.
        /// A HOVER breath happens in soar and a PERCH breath on the ground with a colonist usually
        /// standing right next to him, and BOTH of those are situations where a FACT can decide the
        /// state and skip the pattern executor. An armed breath that never fires would read as the
        /// dragon landing, pausing, and leaving without attacking - a defect this session has
        /// already produced twice from exactly that shape.
        ///
        /// The wind-up itself needs no enforcement: BeginExecute stuns him for the whole phase, so
        /// he is motionless from landing to lift-off with nothing else able to move him.
        /// </summary>
        private void UpdatePendingBreath(Pawn pawn)
        {
            if (pendingBreathTick == 0 || Find.TickManager.TicksGame < pendingBreathTick)
            {
                return;
            }
            pendingBreathTick = 0;

            // He may have been dragged out of the pattern during the pause - killed, downed, grabbed
            // something, or the phase cut short. Breathing after that would be a jet from a dragon
            // doing something else entirely.
            if (!hasPattern || patternPhase != DragonPatternPhase.Execute
                || pawn.Dead || pawn.Downed || !pawn.Spawned)
            {
                return;
            }

            // Aim at whoever he is fighting NOW, not at whoever he landed for. Four seconds is long
            // enough to walk out of a cone, and re-reading the target is what stops the jet going
            // where the fight WAS. Falls back to the remembered cell so a dead target still gets
            // breathed on rather than cancelling the attack.
            Thing target = CurrentTargetOf(pawn);
            IntVec3 at = target != null ? target.Position
                : (lastTargetPos.IsValid ? lastTargetPos : pawn.Position);
            BreatheAt(at, pendingBreathShape);

            // ⚠ FREEZE THE FACING **HERE**, AFTER BreatheAt HAS AIMED HIM - never at BeginExecute.
            // BreatheAt is what sets pawn.Rotation from the target, and it only just ran; freezing
            // before it captured the facing he happened to land with, which is how the user got
            // *"facing east yet fired his ground-breathing south-west."*
            //
            // Held for the REST of the phase - the jet plus the motionless tail - so he does not
            // turn while breathing or during the beat afterwards. Routed through the shake-profile
            // slot, whose restore already heals itself from CompTick however the phase ends.
            if (AlduinGraphicsUtility.SetFrozenFacing(pawn, currentState, pawn.Rotation))
            {
                shakeUntilTick = phaseUntilTick;
            }
        }

        /// <summary>
        /// Fire the circling clip when its scheduled tick arrives, alternating the two takes.
        ///
        /// ⚠ ANCHORED AT THE CENTRE OF THE CIRCLE, NOT AT THE DRAGON. `PlayOneShot` fixes a sound at
        /// a world position for its whole length, and this clip runs FIFTEEN SECONDS - long enough
        /// for a dragon at 3.42x to be a long way from wherever he was when it started. Anchoring it
        /// at the circle's centre puts it in the middle of the area he is orbiting, so he stays
        /// within about one `dragonCirclingRadius` of it the entire time instead of flying away from
        /// his own noise.
        ///
        /// That works BECAUSE circling is bounded. If a future state ever needs a continuous sound
        /// while he crosses the map, this is not the tool - that wants a Sustainer maintained per
        /// tick against the pawn, which follows him properly and is a good deal more machinery.
        /// </summary>
        private void UpdateCirclingSound(Pawn pawn)
        {
            if (circlingSoundAtTick == 0 || Find.TickManager.TicksGame < circlingSoundAtTick)
            {
                return;
            }
            circlingSoundAtTick = 0;
            // He may have been dragged out of Leave during the take-off delay - by a new pattern,
            // a grab, or death. A circling loop over a brawl would be a lie about what he is doing.
            if (patternPhase != DragonPatternPhase.Leave || currentState != AlduinMovementState.Flight)
            {
                return;
            }
            IntVec3 centre = circleWaypoint.IsValid ? circleWaypoint
                : (lastTargetPos.IsValid ? lastTargetPos : pawn.Position);
            PlayDovahSound(pawn,
                circlingSoundAlternate
                    ? "Dovahkiin_DragonFlightCircling2"
                    : "Dovahkiin_DragonFlightCircling1",
                centre);
            // Alternate rather than roll. The two takes are the same length and similar in
            // character, so a coin flip would routinely play one of them three legs running and
            // read as a single looping clip - the very thing having two is meant to avoid.
            circlingSoundAlternate = !circlingSoundAlternate;
        }

        /// <summary>
        /// One place for every dovah one-shot, so the "missing SoundDef must be silent, never
        /// fatal" rule cannot be forgotten at one call site out of four.
        /// </summary>
        private static void PlayDovahSound(Pawn pawn, string defName, IntVec3 at)
        {
            if (pawn == null || pawn.Map == null)
            {
                return;
            }
            SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.PlayOneShot(new TargetInfo(at, pawn.Map));
            }
        }

        /// <summary>
        /// Dust and a brief stagger as he touches down. The user, 2026-08-04: "dovahs going from
        /// soar to grounded causes a little dust flying around them and causes a brief stun."
        /// </summary>
        private static void DoLandingImpact(Pawn pawn, Thing diveTarget)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return;
            }
            DovahkiinTuningDef t = Tuning;
            int puffs = t != null ? t.dragonLandingDustPuffs : 30;   // MUST match the field default; 74 here meant a def-load failure threw 2.5x the dust
            float radius = t != null ? t.dragonLandingStunRadius : 4.4f;
            int stunTicks = t != null ? t.dragonLandingStunTicks : 120;

            // ================= THE POUNCE =================
            // The user, 2026-08-05: "his stun-landing is 95% of the time outran by his fleeing
            // target." It was not bad luck - the landing was UNLOSEABLE FOR THE TARGET by
            // construction, for two independent reasons:
            //
            //   1. He lands at dragonLandToBiteDistance (4 cells) but the stun only reaches
            //      dragonLandingStunRadius (2.4). A target at the trigger distance is OUTSIDE
            //      the blast. Two numbers that have to agree, and nothing made them.
            //   2. Touching down swaps his 3.60x flight speed for 1.00x. Base MoveSpeed 4.6 is
            //      about what a fleeing colonist runs at, so having missed once he could never
            //      close the gap again - he simply trailed them until the disengage fired.
            //
            // So the landing now finishes the dive: he comes down on a cell BESIDE the target,
            // which is what "LAND ON the target using the landing stun" meant in SPEC.md 6.5c-4's
            // pattern 1 all along. The stun below is then centred on where he actually lands.
            //
            // ⚠ BESIDE, NEVER ON. Putting him in the target's own cell would hand the collision
            // shunt to the target and start the conveyor that caused three rounds of kidnapping.
            if (diveTarget != null && diveTarget.Spawned && diveTarget.Map == map)
            {
                float maxDive = t != null ? t.dragonDiveMaxCells : 6f;
                float gap = pawn.Position.DistanceTo(diveTarget.Position);
                if (maxDive > 0f && gap > 1.5f && gap <= maxDive)
                {
                    // TRUE = never a roofed cell. This is the reported bug's exact seam: the
                    // pounce is HOW he "landed inside a roofed area", because it teleports him to
                    // whichever cell is beside the target without asking anything else about it.
                    IntVec3 landing = BestCellBeside(diveTarget, pawn, map, true);
                    if (landing.IsValid && landing != pawn.Position)
                    {
                        pawn.Position = landing;
                        // endCurrentJob FALSE - he is mid-attack on this very target and ending
                        // the job would make him forget it. resetTweenedPos TRUE, or he visibly
                        // slides across the intervening cells instead of dropping.
                        pawn.Notify_Teleported(false, true);
                    }
                }
            }

            // ⚠ THE SOUND GOES HERE - AFTER THE POUNCE, NOT BEFORE IT. The teleport above may have
            // moved him several cells, and this def is positional (context MapOnly + distRange), so
            // playing it earlier would put a dive's impact at the point he LEFT rather than the
            // point he hit.
            //
            // The clip was recorded 2026-08-06 and sat in Sounds/ unwired until 2026-08-12 - it
            // postdates the last edit to Sounds_Dovahkiin.xml by twelve hours and simply never got
            // a def. **Nothing anywhere reports an unused audio file**; there is nothing for
            // RimWorld to complain about.
            //
            // GetNamedSilentFail plus a null check, per the notebook's rule: a missing SoundDef must
            // be SILENT, never fatal, so audio can never take the landing effect down with it.
            SoundDef landingSound = DefDatabase<SoundDef>.GetNamedSilentFail("Dovahkiin_DragonLanding");
            if (landingSound != null)
            {
                landingSound.PlayOneShot(new TargetInfo(pawn.Position, map));
            }

            // Same helper and the same shape the Ancient Dragonborn's arrival already uses, so
            // the two impacts read as belonging to one mod rather than two.
            //
            // ⚠ THE SPREAD IS DERIVED FROM THE STUN RADIUS, NOT A CONSTANT. It was a hardcoded
            // +/-1.1 while the stun reached 2.4, so the dust never showed how far the impact
            // actually went - and when the user asked for the radius to grow on 2026-08-13 they
            // had to ask for the dust to be adjusted with it, in the same breath. **That is the
            // signature of two numbers that should have been one.** Now the cloud always covers
            // the ground the stun covers, and raising one raises both.
            //
            // sqrt keeps the scatter even: area grows as r^2, so a flat random radius piles the
            // puffs up in the middle (the notebook's own rule for scattering inside a disc).
            for (int i = 0; i < puffs; i++)
            {
                float ang = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist = radius * Mathf.Sqrt(Rand.Value);
                // ⚠ SIZE CUT 1.8-3.2 -> 1.2-2.2 ALONGSIDE THE COUNT (74 -> 30) ON 2026-08-13.
                // At the old figures the puffs overlapped into a SOLID OPAQUE DISC about thirteen
                // cells across - the user's *"huge pile of dusts"* - which hid the dragon entirely
                // and made him impossible to click.
                //
                // The mistake was scaling the count by AREA when the radius grew, i.e. holding
                // DENSITY constant. They were already overlapping at the old radius, so constant
                // density meant constant opacity over a much bigger circle. **What wants holding
                // steady is how the burst READS, not how many puffs per cell it contains.**
                FleckMaker.ThrowDustPuffThick(
                    pawn.DrawPos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist),
                    map, Rand.Range(1.2f, 2.2f), new Color(0.78f, 0.72f, 0.62f, 0.75f));
            }

            if (stunTicks <= 0 || radius <= 0f)
            {
                return;
            }
            foreach (Pawn other in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (other == pawn || other.Dead || other.Downed)
                {
                    continue;
                }
                // Nothing else airborne is standing on the ground he just hit.
                if (other.Position.DistanceTo(pawn.Position) > radius)
                {
                    continue;
                }
                if (other.stances != null && other.stances.stunner != null)
                {
                    other.stances.stunner.StunFor(stunTicks, pawn, false, false);
                }
                MakeItPersonal(other, pawn);
            }
        }

        /// <summary>
        /// A DOVAH IS NEVER FOUND STANDING UNDER A ROOF - he lifts back into a soar.
        ///
        /// This is the backstop, not the mechanism. The pounce and the pattern roll already keep
        /// him out from under roofs; this catches every route nobody has thought of - spawned
        /// inside a building, a roof built over his head, another mod setting him down indoors.
        /// It lives in CompTick's unconditional block because it is phrased "he must never X",
        /// and this file's own hard-won rule is that an invariant cannot live inside a
        /// conditional.
        ///
        /// SOAR, NOT FLIGHT: soar is the low, ground-speed state, so leaving a building reads as
        /// him picking himself up off the floor rather than snapping into a dive. The pattern
        /// executor takes him back up to Flight on its own next phase.
        ///
        /// FORCE OUTRANKS THE ROOF, AND THE ORDER OF THESE GUARDS *IS* THE PRECEDENCE. When
        /// MustBeGrounded is true - downed, at or below the grounded health fraction, or holding
        /// somebody in his jaws - he stays exactly where he is, roof or no roof. Lifting a dying
        /// dragon off the floor would cancel the rule that makes him killable at all, and that is
        /// worth more than tidiness.
        /// </summary>
        private void KeepDovahOffRoofedGround(Pawn pawn)
        {
            if (currentState != AlduinMovementState.Grounded)
            {
                return;
            }
            if (!RoofBarsGrounding(pawn.Position, pawn.Map))
            {
                return; // one grid lookup, and the common answer - keep it before the rest
            }
            if (MustBeGrounded(pawn))
            {
                return; // forced down: the roof does not get a vote
            }
            EnterState(pawn, AlduinMovementState.Soar);
        }

        /// <summary>
        /// Can a GROUNDED attack pattern execute against his current target at all? False when
        /// the target is under a roof, because then every cell he could brawl or perch on is too.
        ///
        /// Asked when a pattern is ROLLED rather than checked while one runs: a pattern that
        /// cannot execute must never be selectable, or the dragon periodically does nothing and it
        /// reads as a hang. Same reasoning that keeps StrafingRun out of the pool.
        /// </summary>
        private bool GroundedPatternsViable(Pawn pawn)
        {
            Map map = pawn == null ? null : pawn.Map;
            if (map == null)
            {
                return true;
            }
            Thing target = CurrentTargetOf(pawn);
            if (target == null || !target.Spawned)
            {
                return true; // nothing to attack yet, so nothing to rule out
            }
            return BestCellBeside(target, pawn, map, true).IsValid;
        }

        /// <summary>
        /// The grounding rule. NOT a latch - re-evaluated every interval, and the
        /// "perma-grounded" feel comes from vanilla healing being slow rather than from a flag.
        /// A dragon that breaks off and heals over days passes this again and flies, which is
        /// the user's intent: a recurring threat, not something permanently crippled.
        /// </summary>
        private bool MustBeGrounded(Pawn pawn)
        {
            if (pawn.Downed)
            {
                return true;
            }
            // Shaking something in his jaws - he is not going anywhere.
            if (IsHeldGrounded)
            {
                return true;
            }

            // ---- HE MUST LAND TO FIGHT. Reported 2026-08-04: "he still does melee rolls while
            // still in flight (he shouldn't be able to), and he still is sometimes being static
            // while in flight (flight state IS NEVER motionless)".
            //
            // BOTH are the same bug, and the cause was the DWELL TIMER. Arrival already returns
            // Grounded - but flight's dwell is 8 seconds, so he was locked airborne after
            // reaching his target and spent it swinging. And a manhunting animal stops moving
            // to attack, which is why a flying dragon appeared to hover.
            //
            // So landing-to-attack has to be a HARD OVERRIDE like the grounding rule, not a
            // choice the cooldown can veto. That also honours SPEC.md 6.5b - he cannot melee
            // while airborne - by making it impossible to be airborne and in melee range at all.
            // ⚠ NOT WHILE A PATTERN IS DELIBERATELY AIRBORNE.
            //
            // The user, 2026-08-06: "him being targeted and swung on by a pawn deactivates his
            // intent to soar (soar briefly happens then vanishes back to grounded as a pawn is
            // swinging at him)."
            //
            // Exactly this clause. The moment a colonist swings, that colonist becomes his
            // CurrentTargetOf and is inside landAt - so "standing in melee range" reads true and
            // slams him to the ground, cancelling a HOVER BREATH that had only just lifted.
            //
            // The rule is still right for its own case (he cannot melee from the air, so being in
            // melee range means he has landed to fight). It is wrong when the pattern has PUT him
            // in the air on purpose: then somebody walking up and swinging should not be able to
            // ground a dragon. Being attacked is not a reason to abandon an attack.
            if (!PatternWantsAirborne)
            {
                Thing meleeTarget = CurrentTargetOf(pawn);
                if (meleeTarget != null)
                {
                    float landAt = Tuning != null ? Tuning.dragonLandToBiteDistance : 4f;
                    // AND THERE MUST BE SOMEWHERE UNROOFED TO STAND. Without this clause the roof
                    // rule and this one become two authors of one decision: this would ground him
                    // beside an indoor target every tick while KeepDovahOffRoofedGround lifted him
                    // straight back out. That oscillation is what the deleted dwell timer caused
                    // five times over. Answering "no" here is what makes him stay up and breathe.
                    if (pawn.Position.DistanceTo(meleeTarget.Position) <= landAt
                        && (pawn.Map == null
                            || BestCellBeside(meleeTarget, pawn, pawn.Map, true).IsValid))
                    {
                        return true;
                    }
                }
            }
            float groundedAt = Tuning != null ? Tuning.dragonGroundedHealthFraction : 0.5f;
            return pawn.health != null && pawn.health.summaryHealth != null
                && pawn.health.summaryHealth.SummaryHealthPercent <= groundedAt;
        }

        // ⚠ ChooseNextState WAS DELETED HERE ON 2026-08-06, along with the dwell rhythm above.
        //
        // It answered "which movement state should he be in", rolled per interval. Attack
        // patterns answer that question by construction - the state IS whichever phase of the
        // attack he is running - so keeping both would have left two authors of one decision,
        // which is exactly what produced the five dwell-timer defects.
        //
        // Its useful parts were not lost: the chase rule (flight unconditionally while closing)
        // is the Approach phase, the arrival rule (land straight from flight, never via soar) is
        // BeginExecute, and the guardian rule lives in RunPattern's no-target branch.

        /// <summary>
        /// What this dragon is currently engaged with, or null. Deliberately does NOT use a
        /// hostility test: GenHostility.HostileTo is false for wild animals and for anything
        /// the dragon has merely been ordered at, and this project has already lost a playtest
        /// round to that exact blind spot (the Ancient Dragonborn ignoring a boar). Asking what
        /// he is ACTUALLY doing is both cheaper and correct.
        /// </summary>
        private static Thing CurrentTargetOf(Pawn pawn)
        {
            if (pawn.CurJob != null)
            {
                LocalTargetInfo targetA = pawn.CurJob.targetA;
                if (targetA.IsValid && targetA.HasThing && targetA.Thing != pawn
                    && IsEngageable(targetA.Thing))
                {
                    return targetA.Thing;
                }
            }
            if (pawn.mindState != null && pawn.mindState.enemyTarget != null
                && IsEngageable(pawn.mindState.enemyTarget))
            {
                return pawn.mindState.enemyTarget;
            }
            return null;
        }

        /// <summary>
        /// Is this thing still worth being in a fight over?
        ///
        /// ⚠ THIS FILTER IS WHY HE USED TO HANG IN THE AIR OVER A CORPSE. The user, 2026-08-05:
        /// "still motionless sometimes (after his target's demise ESPECIALLY)", and before that,
        /// "the moment his target was downed".
        ///
        /// The old test was `!Destroyed`, and A KILLED PAWN IS NOT DESTROYED - it is DESPAWNED
        /// into a Corpse, while the Pawn object itself lives on inside it. So a dead target kept
        /// passing as a live one, and every rule downstream believed he was still engaged:
        /// CurrentTargetOf returned it, so OutOfReach said he was in reach (the corpse is right
        /// there), so the disengage never fired; and TryCircle is skipped whenever there is a
        /// target, so he never got a destination either. The result is a dragon hovering over a
        /// body with nothing able to move him on.
        ///
        /// DOWNED counts as disengaged too, deliberately: the user reported a downed target as a
        /// hovering trigger in the previous round. It does mean he leaves the downed alone and
        /// looks for the next threat - say so if that is wrong, it is one word here.
        /// </summary>
        private static bool IsEngageable(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }
            // ⚠ A TARGET MUST BE A PAWN. This line used to read `p == null || (...)`, which
            // returns TRUE for everything that is not a pawn - and on 2026-08-12 the log caught
            // what that costs:
            //
            //   job=ExtinguishSelf jobTargetA=Fire | engageableTarget=Fire [notPawn dist=0.0]
            //
            // He had caught fire, RimWorld gave him ExtinguishSelf, CurrentTargetOf read that
            // job's targetA, and A FIRE BURNING ON HIM BECAME HIS COMBAT TARGET - at distance
            // zero, so every "am I close enough" test in the pattern logic said yes. Every rule
            // that consumes CurrentTargetOf was being fed a fire.
            //
            // The dragon's whole pattern vocabulary - approach, engage distance, dive, breathe,
            // circle a fight - is about pawns. Nothing downstream means anything against a Fire,
            // a wall or a chunk, so the filter says so rather than leaving it to luck.
            Pawn p = thing as Pawn;
            return p != null && !p.Dead && !p.Downed;
        }

        /// <summary>
        /// Speed for the state, as a hediff severity the StatPart reads. SPEC.md 6.5.
        /// </summary>
        private static void ApplySpeed(Pawn pawn, AlduinMovementState state)
        {
            if (pawn.health == null) { return; }
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("Dovahkiin_DragonAirborne");
            if (def == null)
            {
                // GetNamedSilentFail returns null with no message; a def that failed to load
                // would otherwise present as a dragon that simply never speeds up.
                Log.WarningOnce("[Dovahkiin] Dovahkiin_DragonAirborne hediff missing - "
                    + "dragon movement states will render but not change speed.", 0x5A1D01);
                return;
            }
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (state == AlduinMovementState.Grounded)
            {
                if (existing != null) { pawn.health.RemoveHediff(existing); }
                return;
            }
            float factor = 1f;
            if (Tuning != null)
            {
                factor = state == AlduinMovementState.Flight
                    ? Tuning.dragonFlightSpeedFactor
                    : Tuning.dragonSoarSpeedFactor;
            }
            if (existing == null)
            {
                existing = HediffMaker.MakeHediff(def, pawn);
                pawn.health.AddHediff(existing);
            }
            // Severity CARRIES the factor, so one hediff def covers both airborne states and
            // the numbers stay in DovahkiinTuningDef where they can be retuned without a
            // rebuild. The StatPart reads it back.
            existing.Severity = Mathf.Max(0.01f, factor);
        }
    }
}


