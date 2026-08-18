// A DOVAH IN THE AIR DOES NOT WADE THROUGH MUD, AND CANNOT BE STUNNED. SPEC.md 6.5.
//
// Both of these were settled by the user on 2026-08-12 after a playtest, and both are here rather
// than in the comp because they have to intercept the ENGINE, not our own logic.
//
// ============================================================================================
// ⚠ THESE ARE THE FIRST PATCHES THIS MOD HAS PUT ON A HOT PATH. READ BEFORE EDITING.
// ============================================================================================
// CLAUDE.md's architecture rule: "Harmony patches: eight, all event-shaped, none per-tick" and
// "Every combat-path patch opens with registry.IsDovahkiin, a reference compare at most one pawn
// per save can pass."
//
// CostToMoveIntoCell runs for EVERY pawn's EVERY cell of movement, with RocketMan installed. So
// the guard is the strictest thing available and it comes FIRST:
//
//     a cached ThingDef REFERENCE COMPARE - one pointer comparison, no string work, no comp
//     lookup, no allocation - and every pawn that is not a dragon leaves on that line.
//
// ⚠ DO NOT use AlduinGraphicsUtility.IsAlduin here. It compares def.defName as a STRING, which is
// perfectly fine on the event-shaped paths it was written for and is far too expensive here.
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Dovahkiin
{
    /// <summary>
    /// Shared, cheap "is this our dragon, and is he off the ground" test for the hot-path patches
    /// below. Everything about it is built to make the NEGATIVE answer as cheap as possible.
    /// </summary>
    public static class DragonAirborneCheck
    {
        private const string AlduinDefName = "Dovahkiin_Alduin_Test";

        private static ThingDef cachedDef;
        private static bool cacheAttempted;

        /// <summary>
        /// The dragon's ThingDef, resolved once. Null until defs have loaded, and null for ever if
        /// the def is missing - in which case every check below simply returns false and the game
        /// behaves exactly as it would without this file.
        /// </summary>
        private static ThingDef AlduinDef
        {
            get
            {
                if (!cacheAttempted)
                {
                    cacheAttempted = true;
                    cachedDef = DefDatabase<ThingDef>.GetNamedSilentFail(AlduinDefName);
                }
                return cachedDef;
            }
        }

        /// <summary>
        /// Is this our dragon at all, airborne or not? One reference compare. Used wherever a rule
        /// must never be applied to a dovah - starting a mental state on one, above all.
        /// </summary>
        public static bool IsDragon(Pawn pawn)
        {
            return pawn != null && AlduinDef != null && pawn.def == AlduinDef;
        }

        /// <summary>
        /// True only for our dragon while he is off the ground. **The def compare is first and it
        /// is a reference compare** - that is what keeps this affordable on the movement path.
        /// </summary>
        public static bool IsAirborneDragon(Pawn pawn)
        {
            if (pawn == null || pawn.def != AlduinDef || AlduinDef == null)
            {
                return false;
            }
            Comp_AlduinFlight comp = pawn.TryGetComp<Comp_AlduinFlight>();
            return comp != null && comp.IsAirborne;
        }
    }

    /// <summary>
    /// TERRAIN DOES NOT TOUCH SOMETHING THAT IS FLYING OVER IT.
    ///
    /// The user, 2026-08-06: *"i caught him slowing down while in flight, flight's velocity is
    /// CONSTANT and shouldn't be changing."* Locomotion urgency was fixed then; this is the other
    /// half, and it was left deliberately unfixed until the user chose to take the patch.
    ///
    /// `Pawn_PathFollower.CostToMoveIntoCell(Pawn, IntVec3)` is
    ///
    ///     ticksPerMove (cardinal or diagonal)
    ///   + pathGrid.CalculatedCostAt(c, ...)      <-- snow, mud, rubble, sand
    ///   + edifice.PathWalkCostFor(pawn)          <-- doors and the like
    ///   , clamped to 450
    ///
    /// For an airborne creature every term after the first is meaningless, so an airborne dragon
    /// pays exactly his own move time and nothing else.
    ///
    /// ⚠ WHAT THIS DOES **NOT** DO, AND IT MATTERS: this decides how fast he crosses a cell, NOT
    /// which cells he chooses. The ROUTE comes from `Verse.AI.PathFinder`, which has its own cost
    /// model, so he will still path AROUND expensive terrain even though crossing it now costs him
    /// nothing. Straightening the route means patching the pathfinder's per-node cost - called
    /// thousands of times per path rather than once per cell moved - and that has NOT been taken.
    /// Do not describe this patch as "the dragon now flies straight"; it is the speed half.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell",
        new[] { typeof(Pawn), typeof(IntVec3) })]
    public static class Patch_AirborneIgnoresTerrain
    {
        private static void Postfix(Pawn pawn, IntVec3 c, ref int __result)
        {
            if (!DragonAirborneCheck.IsAirborneDragon(pawn))
            {
                return;
            }
            // His own move time only. Diagonal costs more than cardinal for the same reason it
            // does for anyone - the distance really is longer - so that distinction is kept.
            __result = (c.x != pawn.Position.x && c.z != pawn.Position.z)
                ? pawn.TicksPerMoveDiagonal
                : pawn.TicksPerMoveCardinal;
        }
    }

    /// <summary>
    /// A DOVAH DOES NOT QUEUE FOR A DOOR.
    ///
    /// The user, 2026-08-12: *"He slows down again to wait for the door in the ruins to open up
    /// before over it (through)."*
    ///
    /// `Patch_AirborneIgnoresTerrain` made the door's PATH COST free, but the wait is a separate
    /// mechanism entirely. `Pawn_PathFollower.TryEnterNextPathCell` does:
    ///
    /// ```
    /// Building_Door d = NextCellDoorToWaitForOrManuallyOpen();
    /// if (d != null) {
    ///     if (!d.Open) d.StartManualOpenBy(pawn);
    ///     pawn.stances.SetStance(new Stance_Cooldown(d.TicksTillFullyOpened, d, null));
    ///     return;                       // <-- he does not move this tick
    /// }
    /// pawn.Position = nextCell;         // <-- and this is what he does INSTEAD when it is null
    /// ```
    ///
    /// **Returning null does not block him - it moves him straight into the cell**, which is
    /// exactly right for something flying over the doorway. So the patch is a refusal, not a
    /// redirect: no wait, no manual-open, no `Stance_Cooldown`.
    ///
    /// `pawn` is `protected` on `Pawn_PathFollower`, so it comes in through Harmony's
    /// `___fieldName` injection rather than being reached with reflection at the call site.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower),
        nameof(Pawn_PathFollower.NextCellDoorToWaitForOrManuallyOpen))]
    public static class Patch_AirborneIgnoresDoors
    {
        private static bool Prefix(Pawn ___pawn, ref Building_Door __result)
        {
            if (!DragonAirborneCheck.IsAirborneDragon(___pawn))
            {
                return true;
            }
            __result = null;
            return false;
        }
    }

    /// <summary>
    /// ⚠⚠ NOTHING SHARES A CELL WITH A FLYING DOVAH, AND NOTHING BLOCKS ONE.
    ///
    /// **THE USER'S INVIOLABLE RULE, 2026-08-13:** *"One of the UNVIOLABLE rules about dovahs is
    /// that they aren't and NEVER ARE stoppable or movable by an external factor during their
    /// flight course."*
    ///
    /// Two reports, one cause: *"got stuck midflight multiple times because of incoming attacks"*
    /// and *"got cornered by a lot of pawns and skeletons and couldn't move at all despite being in
    /// flight."*
    ///
    /// `Pawn_PathFollower.PatherTick` refuses to move a pawn into a cell occupied by someone it
    /// collides with, and **`PawnUtility.PawnsCanShareCellBecauseOfBodySize` returns false outright
    /// when either pawn is `BodySize >= 1.5`** - his is 4.6, so he collides with *everything*. Ring
    /// him with bodies and he is walled in by them, thirty feet up.
    ///
    /// `ShouldCollideWithPawns` false for an airborne dragon fixes both halves: he passes over
    /// crowds, and a crowd can no longer pin him. **A creature in the air occupying the same map
    /// square as one on the ground is not a collision - it is the point of flying.**
    ///
    /// ⚠ AIRBORNE ONLY. On the ground he collides normally, which is what keeps the grab's
    /// "never park a pawn in a living pawn's cell" rule meaningful.
    /// </summary>
    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.ShouldCollideWithPawns))]
    public static class Patch_AirborneCollidesWithNobody
    {
        private static bool Prefix(Pawn p, ref bool __result)
        {
            if (!DragonAirborneCheck.IsAirborneDragon(p))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// ⚠ A DOVAH IS NOT LIVESTOCK. HE CANNOT BE ROPED OR LASSOED, BY ANYONE, EVER.
    ///
    /// **The user's call, 2026-08-13**, after watching giant skeletons lasso the dragon and drag him
    /// around: *"No, a Dovah shouldn't be ropeable nor lasso'ed at all."*
    ///
    /// It was not merely undignified - **it was the cause of a real bug.** Every pull ended with him
    /// arriving somewhere new, which registered as an airborne-to-grounded transition and fired a
    /// FULL landing impact: 74 dust puffs and a two-second stun over 4.4 cells, once per yank, on
    /// everything nearby including the colony's own pawns. The dust plates the user could see were
    /// the visible half of a stun-spam they could not.
    ///
    /// `CreateRope` is the single chokepoint - **both** `RopePawn` (roped by a pawn) and
    /// `RopeToSpot` (tied to a hitching post) route through it, so one prefix covers every way a
    /// rope can be attached. It is private and static; Harmony patches it happily, and patching only
    /// the public `RopePawn` would have left the hitching post open.
    ///
    /// ⚠ THIS GUARDS THE ROPEE, NOT THE ROPER, which is the direction that matters.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_RopeTracker), "CreateRope")]
    public static class Patch_DovahCannotBeRoped
    {
        private static bool Prefix(Pawn ropee)
        {
            return !DragonAirborneCheck.IsDragon(ropee);
        }
    }

    /// <summary>
    /// A DOVAH IN THE AIR CANNOT BE STUNNED. **The user's call, 2026-08-12**, chosen over the
    /// alternative of having him fall out of the sky.
    ///
    /// The report: *"i caught him frozen while in flight state because a pawn engaged him."*
    /// `HOVER-DIAG` showed it exactly - a perfectly good flight order he could not act on:
    ///
    ///     state=Flight job=Goto jobTargetA=cell (115, 0, 125)
    ///     fullBodyBusy=True stunned=True stance=Stance_Mobile
    ///
    /// A stun sets `Pawn_StanceTracker.FullBodyBusy`, and `Pawn_PathFollower.PatherTick` returns
    /// immediately on it - so a stunned flyer hangs motionless in the sky.
    ///
    /// **`StunFor` is the single chokepoint and that is why the patch is here.**
    /// `StunHandler.Notify_DamageApplied` - the EMP, stun-damage and Empathy paths - routes through
    /// `StunFor` for all three of its cases, so one prefix covers every source in the game and any
    /// mod that stuns the honest way.
    ///
    /// ⚠ OUR OWN HOLD MUST STILL WORK. `Comp_AlduinFlight.HoldStill` stuns him ON PURPOSE for the
    /// post-breath beat, and a breath happens in SOAR - which is airborne. A blanket refusal would
    /// silently delete the "he stays motionless after a breath" rule the user asked for twice. So
    /// the comp raises `AllowSelfStun` around its own call, and only that call gets through.
    /// A flag rather than an instigator test: `HoldStill` passes the pawn as its own instigator,
    /// which *looks* like a safe discriminator right up until something else does the same.
    /// </summary>
    [HarmonyPatch(typeof(StunHandler), nameof(StunHandler.StunFor))]
    public static class Patch_AirborneIgnoresStun
    {
        /// <summary>
        /// Raised by <see cref="Comp_AlduinFlight"/> around its own deliberate holds. Static rather
        /// than passed, because the engine's signature is not ours to change.
        /// </summary>
        public static bool AllowSelfStun;

        private static bool Prefix(StunHandler __instance)
        {
            if (AllowSelfStun)
            {
                return true;
            }
            // StunHandler.parent is a public field on the class.
            Pawn pawn = __instance == null ? null : __instance.parent as Pawn;
            if (!DragonAirborneCheck.IsAirborneDragon(pawn))
            {
                return true;
            }
            // Skip the original entirely: no stunTicksLeft, no mote, no battle log entry.
            return false;
        }
    }
}
