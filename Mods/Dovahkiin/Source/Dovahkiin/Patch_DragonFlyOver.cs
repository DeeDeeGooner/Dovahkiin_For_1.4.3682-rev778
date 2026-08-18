// A DOVAH IN THE AIR FLIES OVER WALLS, ROOFS AND GATES. SPEC.md 6.5.
//
// ============================================================================================
// WHY THIS EXISTS, AND WHY THE EARLIER PATCHES WERE NOT ENOUGH
// ============================================================================================
// The user, 2026-08-13, on an old save: the dragon spawned inside a castle's curtain wall and
// "was stuck in there as in he couldnt fly over the walls. He had to wait for the gates to be
// opened by a pawn before being able to exit."
//
// Patch_AirborneIgnoresTerrain made crossing a cell FREE, and Patch_AirborneIgnoresDoors stopped
// him queueing at doors. Neither makes a wall PASSABLE, because a wall is not a cost - it is a
// cell the pathfinder will not enter and the reachability system will not route through.
//
// FOUR THINGS HAVE TO AGREE BEFORE A DRAGON CAN CROSS A WALL, and missing any one of them looks
// like the whole feature is broken:
//
//   1. REACHABILITY says the destination is reachable, or the job giver never issues the job.
//      (JobGiver_GotoTravelDestination opens with a CanReach check and returns null otherwise.)
//   2. THE PATHFINDER produces a path, or StartPath calls PatherFailed immediately.
//   3. THE BLOCKER CHECK does not see the wall, or TryEnterNextPathCell hands him a BASH job -
//      ⚠ `pawn.HostileTo(building)` is TRUE for a dov-faction dragon against a colony wall, so
//      without this he does not merely stop, he starts DEMOLISHING the wall.
//   4. Nothing charges him for the crossing - already handled by Patch_AirborneIgnoresTerrain.
//
// ============================================================================================
// THE ONE EXCEPTION, AND IT IS LOAD-BEARING DESIGN
// ============================================================================================
// SPEC.md 6.5: "THE ONE EXCEPTION TO CROSSING: natural mountain rock is impassable in every
// state - which is what makes a mountain base genuinely dragon-proof and is the player's
// architectural answer." A dragon that could cross solid rock would delete that answer, so the
// straight line stops at natural rock and he falls back to ordinary pathing.
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Dovahkiin
{
    /// <summary>
    /// Shared helpers for flying over things. Everything here is gated on the same cheap
    /// reference compare the other airborne patches use.
    /// </summary>
    public static class DragonFlyOver
    {
        /// <summary>Master switch, read from the tuning def. False restores ordinary pathing.</summary>
        public static bool Enabled
        {
            get
            {
                DovahkiinTuningDef t = DovahkiinTuningDef.Current;
                return t == null || t.dragonFliesOverObstacles;
            }
        }

        /// <summary>
        /// ⚠ THE MOUNTAIN RULE. Natural rock stops him; everything a colonist BUILT does not.
        ///
        /// `isNaturalRock` is the honest test - it distinguishes a mountain from a granite wall
        /// somebody quarried and placed, which look identical by stuff and by `passability`.
        /// </summary>
        public static bool IsNaturalRock(IntVec3 c, Map map)
        {
            if (map == null || !c.InBounds(map))
            {
                return true; // off-map counts as solid: never fly out of the world
            }
            Building edifice = c.GetEdifice(map);
            return edifice != null
                && edifice.def != null
                && edifice.def.building != null
                && edifice.def.building.isNaturalRock;
        }

        /// <summary>
        /// A straight Bresenham line from start to dest, as the cells he will actually occupy.
        /// Returns null if natural rock stands anywhere on it - the mountain rule above.
        /// </summary>
        public static List<IntVec3> StraightLine(IntVec3 start, IntVec3 dest, Map map)
        {
            List<IntVec3> cells = new List<IntVec3>();
            int x = start.x, z = start.z;
            int dx = Mathf.Abs(dest.x - x), dz = Mathf.Abs(dest.z - z);
            int sx = dest.x > x ? 1 : -1, sz = dest.z > z ? 1 : -1;
            int err = dx - dz;
            // Bounded: a straight line can never be longer than the map diagonal, and the guard
            // stops a malformed call spinning rather than trusting the arithmetic.
            int guard = dx + dz + 4;
            while (guard-- > 0)
            {
                if (x == dest.x && z == dest.z)
                {
                    cells.Add(dest);
                    return cells;
                }
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
                IntVec3 c = new IntVec3(x, 0, z);
                if (IsNaturalRock(c, map))
                {
                    return null; // he cannot fly through a mountain - fall back to real pathing
                }
                cells.Add(c);
            }
            return null;
        }
    }

    /// <summary>
    /// (0) THE CELL IS WALKABLE *FOR HIM*. Added 2026-08-13 after the first fly-over playtest,
    /// which produced **42 red errors in one skirmish**:
    ///
    /// ```
    /// Dovahkiin_Alduin_Test entering (129, 0, 144) which is unwalkable.
    /// Dovahkiin_Alduin_Test on unwalkable cell (129, 0, 144). Teleporting to (128, 0, 143)
    /// ```
    ///
    /// **The flying worked; the engine simply disagreed that it should.** `WalkableBy` is the
    /// shared predicate behind both complaints, and the second one is not cosmetic at all -
    /// `PatherTick` calls `TryRecoverFromUnwalkablePosition`, which **teleports him off the wall**
    /// mid-crossing. So the flight was being undone by the engine's own repair routine.
    ///
    /// Answering "walkable" for an airborne dragon is the honest answer to the question actually
    /// being asked - *can this pawn be in this cell* - and it silences the error, stops the
    /// rescue-teleport, and needs no change to any of the three patches below.
    ///
    /// ⚠ It takes the PAWN, so it can be answered per-creature; almost nothing else in the pathing
    /// stack can. That is why this seam and not another.
    /// </summary>
    [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.WalkableBy))]
    public static class Patch_AirborneWalksOnAnything
    {
        private static bool Prefix(IntVec3 c, Map map, Pawn pawn, ref bool __result)
        {
            if (!DragonFlyOver.Enabled || !DragonAirborneCheck.IsAirborneDragon(pawn))
            {
                return true;
            }
            // In bounds and not solid rock. Everything a colony built, he is over.
            __result = c.InBounds(map) && !DragonFlyOver.IsNaturalRock(c, map);
            return false;
        }
    }

    /// <summary>
    /// (1) REACHABILITY. Without this the job giver never even asks for a path: it opens with
    /// `if (!pawn.CanReach(destination, ...)) return null;` and a waypoint on the far side of a
    /// wall is in a different region, so the answer is no.
    ///
    /// ⚠ Answering "yes" here is safe ONLY because the pathfinder patch below can actually deliver
    /// a route. A reachability lie without a matching path is a pawn that accepts a job and then
    /// fails it every tick.
    /// </summary>
    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach),
        new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) })]
    public static class Patch_AirborneCanReachAnything
    {
        private static bool Prefix(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParams,
            ref bool __result, Map ___map)
        {
            if (!DragonFlyOver.Enabled
                || !DragonAirborneCheck.IsAirborneDragon(traverseParams.pawn))
            {
                return true;
            }
            if (!dest.IsValid || !dest.Cell.InBounds(___map))
            {
                return true; // let vanilla handle nonsense
            }
            // Reachable exactly when a straight line exists that no mountain blocks.
            __result = DragonFlyOver.StraightLine(start, dest.Cell, ___map) != null;
            return false;
        }
    }

    /// <summary>
    /// (2) THE PATH ITSELF. `CanReach` saying yes is not enough - `Pawn_PathFollower.StartPath`
    /// calls the pathfinder and, finding nothing, calls `PatherFailed` on the spot.
    ///
    /// So an airborne dragon gets a **hand-built straight-line path**. That is cheaper than the A*
    /// it replaces, not more expensive, and everything downstream - cost per cell, the tweener,
    /// arrival detection - keeps working because it is a perfectly ordinary `PawnPath`.
    ///
    /// ⚠ NODE ORDER IS REVERSED AND IT IS NOT OPTIONAL. `PawnPath.SetupFound` sets
    /// `curNodeIndex = nodes.Count - 1` and `Peek(n)` reads `nodes[curNodeIndex - n]`, walking the
    /// list DOWNWARD - so **the destination must be added first and the start last.** Build it
    /// forwards and he walks the route backwards.
    /// </summary>
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.FindPath),
        new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode), typeof(PathFinderCostTuning) })]
    public static class Patch_AirborneStraightPath
    {
        private static bool Prefix(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms,
            ref PawnPath __result, Map ___map)
        {
            if (!DragonFlyOver.Enabled
                || !DragonAirborneCheck.IsAirborneDragon(traverseParms.pawn)
                || ___map == null || !dest.IsValid)
            {
                return true;
            }
            List<IntVec3> line = DragonFlyOver.StraightLine(start, dest.Cell, ___map);
            if (line == null || line.Count == 0)
            {
                return true; // a mountain is in the way - let vanilla path around it
            }

            PawnPath path = ___map.pawnPathPool.GetEmptyPawnPath();
            // Destination first, start last - see the warning above.
            for (int i = line.Count - 1; i >= 0; i--)
            {
                path.AddNode(line[i]);
            }
            path.AddNode(start);
            path.SetupFound(line.Count, usedRegionHeuristics: false);
            __result = path;
            return false;
        }
    }

    /// <summary>
    /// (3) THE BLOCKER CHECK, AND THIS IS THE ONE THAT WOULD HAVE BEEN A DISASTER TO MISS.
    ///
    /// `TryEnterNextPathCell` opens with `BuildingBlockingNextPathCell()`, and if something is
    /// there:
    ///
    /// ```csharp
    /// if ((pawn.CurJob != null && pawn.CurJob.canBashDoors) || pawn.HostileTo(building))
    ///     MakeBashBlockerJob(building);      // <-- he ATTACKS it
    /// else
    ///     PatherFailed();
    /// ```
    ///
    /// **`pawn.HostileTo(building)` is TRUE for a dov-faction dragon against any colony
    /// structure.** So without this patch, a dragon handed a path through a wall does not stop at
    /// it and does not fly over it - **he stops and demolishes it**, which is a far worse and much
    /// more confusing outcome than the original bug.
    ///
    /// Returning null means "nothing is in the way", which for something flying overhead is simply
    /// true.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.BuildingBlockingNextPathCell))]
    public static class Patch_AirborneNothingBlocks
    {
        private static bool Prefix(Pawn ___pawn, ref Building __result)
        {
            if (!DragonFlyOver.Enabled || !DragonAirborneCheck.IsAirborneDragon(___pawn))
            {
                return true;
            }
            __result = null;
            return false;
        }
    }
}
