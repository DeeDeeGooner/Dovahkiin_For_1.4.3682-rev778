// Implements: the three movement states of Alduin's art (SPEC - Alduin; ROADMAP Phase 3 prep).
//
// The user's design, 2026-08-03: flight (high, fast) / soar (low, ground speed) / grounded,
// "for more attack patterns and game dynamics". Twelve sprites exist for it; this is the code
// that puts the right one on the pawn.
//
// ============================================================================================
// WHY THIS NEEDS NO HARMONY PATCH - verified 2026-08-04, do not re-derive
// ============================================================================================
//
// Verse.PawnGraphicSet.nakedGraphic is a PUBLIC FIELD, and AllResolved is simply
// "nakedGraphic != null". Every re-resolve on the draw path is guarded by !AllResolved -
// PawnRenderer.RenderPawnAt, PawnRenderer.RenderPawnInternal and Widgets.GetIconFor all test
// it - so a non-null assignment turns every one of them into a no-op. Nothing re-resolves per
// frame or per tick.
//
// That matters more here than it sounds: a patch on the pawn render path is the single most
// fragile thing available under RocketMan, and this mod has avoided one throughout.
//
// TWO TRAPS, both of which would have cost a playtest round:
//
//   1. DO NOT CALL SetAllGraphicsDirty(). It sounds like "refresh the visuals" and it is
//      exactly the method that UNDOES this: it calls ResolveAllGraphics(), which for an animal
//      does nakedGraphic = curKindLifeStage.bodyGraphicData.Graphic - reinstating the
//      PawnKindDef's sprite and throwing ours away.
//
//   2. ClearCache() IS REQUIRED. PawnGraphicSet.MatsBodyBaseAt caches materials against a hash
//      of (facing, RotDrawMode, drawClothes, dead). Assigning nakedGraphic changes none of
//      those, so the OLD material keeps drawing until the creature happens to turn - which
//      reads as "the swap works, but only sometimes", the worst kind of bug to chase.
//
// The ONLY thing that can undo the swap for an animal is a LIFE STAGE CHANGE, via
// Pawn_AgeTracker.RecalculateLifeStageIndex, and only when the index actually changes. Alduin
// has ONE life stage because dovah are timeless, so it fires at most once, at spawn. The lore
// requirement and the graphics mechanism protect each other. Found by scanning all 12,819
// types in Assembly-CSharp for callers - the other 14 call sites are humanlike-only or portrait
// paths behind "pawn.story != null", and pawn.story is null for animals.
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Dovahkiin
{
    public enum AlduinMovementState
    {
        Grounded,
        Soar,
        Flight
    }

    /// <summary>Compass heading, clockwise from north. FLIGHT ONLY - see the note below.</summary>
    public enum AlduinOctant
    {
        N, NE, E, SE, S, SW, W, NW
    }

    public static class AlduinGraphicsUtility
    {
        // Must match the texPath in Defs/ThingDefs_Races/Alduin_Dovahkiin.xml. Grounded is the
        // one named in the def, so it is what he reverts to if anything ever re-resolves him.
        private const string GroundedPath = "Things/Pawn/Animal/Alduin/AlduinGround";
        private const string SoarPath     = "Things/Pawn/Animal/Alduin/AlduinSoar";
        private const string FlightPath   = "Things/Pawn/Animal/Alduin/AlduinFlight";

        // ========================================================================================
        // EIGHT-WAY FACING, AND WHY IT WORKS WITHOUT A RENDER PATCH
        // ========================================================================================
        // The user, 2026-08-04: creatures move diagonally, so a flying dragon locked to four
        // facings reads badly. Diagonals are FLIGHT ONLY - flight is the only top-down state, and
        // top-down is the only projection that can be rotated (see DRAGON_ART_PIPELINE.md).
        //
        // Verse.Rot4.RotationCount is 4. The engine has NO diagonal facing for pawns, and
        // PawnGraphicSet picks its material with a bare nakedGraphic.MatAt(facing). So eight-way
        // facing cannot be expressed the normal way.
        //
        // THE WAY ROUND IT: each octant is a Graphic_SINGLE - one texture returned for EVERY
        // Rot4 (verified: MatSingle, MatNorth, MatEast, MatSouth and MatWest all return the same
        // 'mat'). So whichever Rot4 the engine hands the pawn, the octant sprite is what draws,
        // and the engine's own facing becomes irrelevant. We pick the octant from the pawn's
        // TRUE heading instead - Verse.AI.Pawn_PathFollower.nextCell is a public field.
        //
        // No Harmony patch, no reflection, and it reuses the same nakedGraphic swap that was
        // proved in play on 2026-08-04.
        private const string FlightOctPrefix = "Things/Pawn/Animal/Alduin/AlduinFlightOct_";

        // Measured off Dragon's Descent, whose adults draw at 4.2 and whose ancient draws at
        // 4.6, against a colonist's 1.5 - and whose FLYING graphic is larger than its ground
        // one. Alduin sits clearly above their True_Dragon, so 4.6 grounded and 5.6 airborne.
        private const float GroundedDrawSize = 4.6f;
        private const float AirborneDrawSize = 5.6f;

        // THE OCTANTS ARE ON A BIGGER FRAME AND THEREFORE NEED A BIGGER DRAW SIZE.
        // Rotating a 512 sprite by 45 degrees would clip its wingtips - measured, the farthest
        // ink is 344.4px from the frame centre against a half-frame of 256 - so MakeFlightOctants
        // renders them at 704. drawSize scales the FRAME, not the creature, so the same creature
        // in a bigger frame draws SMALLER unless this is compensated:
        //     512 frame: 5.6 x (491/512) = 5.369 cells on screen
        //     704 frame: d   x (491/704) = 5.369  ->  d = 7.70
        // If MakeFlightOctants' frame size ever changes, this changes with it.
        private const float FlightOctDrawSize = 7.70f;

        // Graphics are cached because GraphicDatabase.Get is not free and a state change can
        // happen on a combat path. Keyed by state; there are three.
        private static readonly Dictionary<AlduinMovementState, Graphic> cache =
            new Dictionary<AlduinMovementState, Graphic>();

        private static string PathFor(AlduinMovementState state)
        {
            switch (state)
            {
                case AlduinMovementState.Flight: return FlightPath;
                case AlduinMovementState.Soar:   return SoarPath;
                default:                         return GroundedPath;
            }
        }

        /// <summary>
        /// ⚠ THESE COME FROM THE TUNING DEF NOW, NOT FROM THE CONSTS ABOVE. Moved 2026-08-12 when
        /// the user asked for +10%: `CLAUDE.md` requires every tuning number to be editable without
        /// a rebuild, and these were the last two creature numbers that were not.
        ///
        /// The consts remain as the FALLBACK for a missing def only, and they carry the pre-raise
        /// values on purpose - if the def ever fails to load, a visibly wrong size is a better
        /// signal than a silently plausible one.
        /// </summary>
        private static float DrawSizeFor(AlduinMovementState state)
        {
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            if (state == AlduinMovementState.Grounded)
            {
                return t != null && t.dragonDrawSizeGrounded > 0f
                    ? t.dragonDrawSizeGrounded : GroundedDrawSize;
            }
            return t != null && t.dragonDrawSizeAirborne > 0f
                ? t.dragonDrawSizeAirborne : AirborneDrawSize;
        }

        public static Graphic GraphicFor(AlduinMovementState state)
        {
            Graphic cached;
            if (cache.TryGetValue(state, out cached) && cached != null)
            {
                return cached;
            }
            float size = DrawSizeFor(state);
            // Graphic_Multi gives four rotations; we ship all four rather than letting it
            // mirror east into west, because flight west is a ROTATION of north rather than a
            // mirror - mirroring would flip handedness and swap any asymmetric detail.
            Graphic built = GraphicDatabase.Get<Graphic_Multi>(
                PathFor(state),
                ShaderDatabase.Cutout,
                new Vector2(size, size),
                Color.white);
            cache[state] = built;
            return built;
        }

        private static readonly Dictionary<AlduinOctant, Graphic> octantCache =
            new Dictionary<AlduinOctant, Graphic>();

        public static Graphic GraphicForOctant(AlduinOctant octant)
        {
            Graphic cached;
            if (octantCache.TryGetValue(octant, out cached) && cached != null)
            {
                return cached;
            }
            Graphic built = GraphicDatabase.Get<Graphic_Single>(
                FlightOctPrefix + octant,
                ShaderDatabase.Cutout,
                new Vector2(FlightOctDrawSize, FlightOctDrawSize),
                Color.white);
            octantCache[octant] = built;
            return built;
        }

        /// <summary>
        /// Which way the dragon is actually going, diagonals included. Falls back to the
        /// engine's own four-way Rotation when he is not pathing anywhere - a stationary
        /// dragon should not be in Flight at all (the user's rule), but this must still
        /// return something sane rather than throw.
        ///
        /// ⚠⚠ READ THE HEADING SEVERAL CELLS AHEAD, NEVER FROM `nextCell` ALONE.
        ///
        /// The user, 2026-08-12: "goes north, north east, north, north, north east instead of
        /// north a good amount of time, then north east for a while longer".
        ///
        /// `pather.nextCell` is ADJACENT - one step - so this reported the direction of a single
        /// stair. RimWorld has no sub-cell movement, so it walks any line that is not exactly
        /// axial or exactly 45 degrees as a STAIRCASE: a leg 14 cells north and 5 east comes out
        /// N, N, NE, N, N, NE. Every one of those was a sprite swap. Nothing was wrong with his
        /// MOVEMENT - the path was straight the whole time and the flicker was in the reading.
        ///
        /// Sampling `Peek(lookahead)` measures the chord across several stairs instead of one
        /// tread, which is the heading a person actually sees. A real turn still shows on the tick
        /// it happens, because the destination changes and the chord changes with it.
        /// </summary>
        /// <summary>
        /// The shortest chord we will read a heading from, in cells. Below this the dead zone
        /// rounds to zero and every diagonal step reads as a diagonal octant.
        /// </summary>
        private static int MinFacingChordCells()
        {
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            int min = t != null ? t.dragonFacingMinChordCells : 3;
            return min < 1 ? 1 : min;
        }

        public static AlduinOctant? HeadingOctant(Pawn pawn)
        {
            Pawn_PathFollower pather = pawn == null ? null : pawn.pather;
            if (pather != null && pather.Moving && pawn.Spawned)
            {
                IntVec3 ahead = pather.nextCell;

                // Verse.AI.PawnPath: `Peek(n)` is `nodes[curNodeIndex - n]` and `NodesLeftCount`
                // is `curNodeIndex + 1`, so the furthest legal index is NodesLeftCount - 1.
                // Both members are public; curPath is a public field. Verified by decompiling
                // PawnPath and Pawn_PathFollower, 2026-08-12.
                PawnPath path = pather.curPath;
                if (path != null && path.NodesLeftCount > 1)
                {
                    DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
                    int want = tuning != null ? tuning.dragonFacingLookaheadCells : 6;
                    if (want > 1)
                    {
                        int lookahead = Mathf.Min(want, path.NodesLeftCount - 1);
                        if (lookahead > 0)
                        {
                            ahead = path.Peek(lookahead);
                        }
                    }
                }

                IntVec3 delta = ahead - pawn.Position;
                // RimWorld's ground plane is x/z: +z is north, +x is east. The delta may now be
                // several cells, so reduce it to a sign per axis - but with a DEAD ZONE, or a
                // chord that is 14 north and 1 east reads as north-east when it is plainly north.
                //
                // ⚠ THE DEAD ZONE IS THE OCTANT BOUNDARY, AND IT MUST BE tan(22.5°) = 0.4142.
                // The first version used a third of the dominant axis - 18.4° - which let a
                // heading up to four degrees short of the boundary read as diagonal. The user,
                // 2026-08-12: "caught him going east while facing south-east." 5/12 is 0.4167,
                // i.e. 22.6°, which is the true boundary to within a fifth of a degree and needs
                // no floating point on a per-tick path.
                int ax = Mathf.Abs(delta.x);
                int az = Mathf.Abs(delta.z);
                int chord = Mathf.Max(ax, az);

                // A CHORD THIS SHORT HAS NO DEAD ZONE AT ALL, SO IT MUST NOT BE ASKED.
                //
                // The user, 2026-08-18: "He oddly flies a lot in diagonal sprite (while going
                // east/west) when flying over roofed areas."
                //
                // `dead` below is INTEGER division, and the table is unforgiving:
                //
                //     chord 1-2 cells -> dead 0     <-- ANY diagonal step reads as diagonal
                //     chord 3-4 cells -> dead 1
                //     chord 5-7 cells -> dead 2
                //
                // So at one or two cells the dead zone rounds to ZERO, and a single Bresenham
                // staircase step - (1,1) on a due-east run - comes back NE at full confidence.
                // That is the very bug the look-ahead was added to kill on 2026-08-12. It
                // survived because the look-ahead cannot always REACH its six cells:
                // `lookahead = Min(want, NodesLeftCount - 1)`, which collapses to 1 or 2 in the
                // TAIL OF EVERY LEG.
                //
                // ROOFED AREAS ARE WHERE IT SHOWS BECAUSE THEY ARE BUILDINGS. Cluttered geometry
                // means short circling legs, so he spends most of his flight in that tail.
                // Nothing in this mod reads the roof grid for facing - grep confirms Roofed()
                // appears only in Thing_StormCall - so the correlation is the GEOMETRY, not the
                // roof. Worth remembering the next time a report names a place rather than a
                // condition.
                //
                // Null means "no opinion, keep the sprite he has", and that is the right answer:
                // a heading measured over one cell is not a heading, and the last real one beats
                // a fresh wrong one.
                if (chord < MinFacingChordCells())
                {
                    return null;
                }

                int dead = (chord * 5) / 12;
                int ex = ax <= dead ? 0 : (delta.x > 0 ? 1 : -1);
                int ez = az <= dead ? 0 : (delta.z > 0 ? 1 : -1);
                if (ex == 0 && ez == 0)
                {
                    // Inside the dead zone on BOTH axes. This used to fall back to
                    // `pather.nextCell` - ONE cell, with no dead zone applied to it at all -
                    // which produced the same wrong diagonal by a second route. Decline instead.
                    return null;
                }
                if (ex == 0 && ez > 0) { return AlduinOctant.N; }
                if (ex > 0 && ez > 0) { return AlduinOctant.NE; }
                if (ex > 0 && ez == 0) { return AlduinOctant.E; }
                if (ex > 0 && ez < 0) { return AlduinOctant.SE; }
                if (ex == 0 && ez < 0) { return AlduinOctant.S; }
                if (ex < 0 && ez < 0) { return AlduinOctant.SW; }
                if (ex < 0 && ez == 0) { return AlduinOctant.W; }
                if (ex < 0 && ez > 0) { return AlduinOctant.NW; }
            }
            // not moving - fall back to the four-way facing the engine already gave him
            if (pawn != null)
            {
                switch (pawn.Rotation.AsInt)
                {
                    case 0: return AlduinOctant.N;
                    case 1: return AlduinOctant.E;
                    case 2: return AlduinOctant.S;
                    default: return AlduinOctant.W;
                }
            }
            return AlduinOctant.S;
        }

        /// <summary>
        /// Re-point a FLYING dragon at his current heading. Cheap enough to call every tick:
        /// it reference-compares and returns false when nothing changed.
        /// </summary>
        public static bool RefreshFlightFacing(Pawn pawn)
        {
            if (pawn == null || pawn.Drawer == null || pawn.Drawer.renderer == null)
            {
                return false;
            }
            AlduinOctant? octant = HeadingOctant(pawn);
            if (!octant.HasValue)
            {
                return false; // no measurable heading - keep the sprite he already has
            }
            PawnGraphicSet graphics = pawn.Drawer.renderer.graphics;
            Graphic wanted = GraphicForOctant(octant.Value);
            if (wanted == null || graphics.nakedGraphic == wanted)
            {
                return false;
            }
            graphics.nakedGraphic = wanted;
            graphics.ClearCache();
            return true;
        }

        /// <summary>
        /// Put the sprite set for <paramref name="state"/> on this pawn. Safe to call every
        /// time; it returns early when nothing would change, so it is cheap enough for TickRare.
        /// </summary>
        public static bool SetState(Pawn pawn, AlduinMovementState state)
        {
            // Flight is eight-way and heading-driven, so it goes through its own path.
            if (state == AlduinMovementState.Flight)
            {
                return RefreshFlightFacing(pawn);
            }
            if (pawn == null || pawn.Destroyed || pawn.Drawer == null)
            {
                return false;
            }
            PawnGraphicSet graphics = pawn.Drawer.renderer == null ? null : pawn.Drawer.renderer.graphics;
            if (graphics == null)
            {
                return false;
            }
            Graphic wanted = GraphicFor(state);
            if (wanted == null)
            {
                // A missing texture folder is a silent black square in game. Say so.
                Log.Warning("[Dovahkiin] Alduin: no graphic for state " + state
                    + " at '" + PathFor(state) + "'. Check the Textures folder.");
                return false;
            }
            // Reference compare: nothing to do if it is already this set. This is what makes
            // the method safe to call from a tick.
            if (graphics.nakedGraphic == wanted)
            {
                return false;
            }
            graphics.nakedGraphic = wanted;
            // REQUIRED - see the header. Without it the old material keeps drawing until he
            // happens to change facing.
            graphics.ClearCache();
            return true;
        }

        // ========================================================================================
        // THE SHAKE, AND WHY IT CANNOT BE DONE WITH pawn.Rotation
        // ========================================================================================
        // The grab flips the dragon east/west to read as a beast worrying something in its jaws.
        // The obvious implementation - assigning pawn.Rotation every few ticks - was built first
        // and showed NOTHING in play: "no east-west view change".
        //
        // THE CAUSE: Verse.Pawn_RotationTracker.UpdateRotation() overwrites it. It runs from
        // Pawn.ProcessPostTickVisuals, which is a SEPARATE PASS AFTER the whole tick - so it is
        // always later than anything we set during Tick, no matter where we set it. Its body:
        //
        //     if (pawn.Destroyed || pawn.jobs.HandlingFacing) return;
        //     if (curStance is Stance_Busy s && s.focusTarg.IsValid) { Face(...); return; }
        //     if (pawn.pather.Moving) { FaceAdjacentCell(nextCell); return; }
        //     if (pawn.jobs.curJob != null) FaceTarget(CurJob.GetTarget(curDriver.rotateToFace));
        //
        // A grabbing dragon has a job targeting his victim, so that last line re-points him at
        // the victim every single tick and our flip is discarded before anything is drawn.
        //
        // THE FIX IS THE OCTANT TRICK, ALREADY PROVEN ABOVE: a Graphic_SINGLE returns the same
        // material for EVERY Rot4, so once one is installed the engine's facing stops mattering
        // and UpdateRotation can do as it likes. We simply swap between the grounded east and
        // west sprites we already ship. No Harmony patch, no fight with the rotation tracker.
        private static readonly Dictionary<bool, Graphic> profileCache = new Dictionary<bool, Graphic>();

        /// <summary>
        /// The grounded profile sprite as a Graphic_Single, so it draws whatever Rot4 the engine
        /// decides on. <paramref name="east"/> false gives the west-facing sprite.
        /// </summary>
        public static Graphic GroundedProfile(bool east)
        {
            Graphic cached;
            if (profileCache.TryGetValue(east, out cached) && cached != null)
            {
                return cached;
            }
            Graphic built = GraphicDatabase.Get<Graphic_Single>(
                GroundedPath + (east ? "_east" : "_west"),
                ShaderDatabase.Cutout,
                // Via DrawSizeFor, not the const - or the shake profile would stay at the old size
                // while every other grounded sprite grew, and he would shrink mid-shake.
                new Vector2(DrawSizeFor(AlduinMovementState.Grounded),
                            DrawSizeFor(AlduinMovementState.Grounded)),
                Color.white);
            profileCache[east] = built;
            return built;
        }

        /// <summary>
        /// Show the dragon in profile for the shake. Call on each flip; returns false when
        /// nothing changed, so it is safe to call every tick.
        /// </summary>
        public static bool SetShakeProfile(Pawn pawn, bool east)
        {
            // GUARDED ON THE DEF. Without this, any future creature with a Dovahkiin_Maw tool
            // that triggers the grab would have ALDUIN'S sprite installed on it - the swap does
            // not care whose graphic it is. The caller falls back to Rotation when this is false.
            if (!IsAlduin(pawn))
            {
                return false;
            }
            if (pawn == null || pawn.Destroyed || pawn.Drawer == null || pawn.Drawer.renderer == null)
            {
                return false;
            }
            PawnGraphicSet graphics = pawn.Drawer.renderer.graphics;
            if (graphics == null)
            {
                return false;
            }
            Graphic wanted = GroundedProfile(east);
            if (wanted == null || graphics.nakedGraphic == wanted)
            {
                return false;
            }
            graphics.nakedGraphic = wanted;
            // REQUIRED, same reason as everywhere else in this file - the material is cached
            // against a hash that does not include which Graphic is installed.
            graphics.ClearCache();
            return true;
        }

        // ========================================================================================
        // FREEZING A FACING - the same trick as the shake, generalised to any state and facing.
        // ========================================================================================
        // The user, 2026-08-06, on the motionless beat after a breath: "he respects the no move
        // rule after a breath yes, but he changes sprite direction nonetheless."
        //
        // Assigning pawn.Rotation cannot fix that, for the reason already recorded above:
        // Pawn_RotationTracker.UpdateRotation runs from ProcessPostTickVisuals - AFTER the whole
        // tick - and re-faces any pawn that has a job. There is no moment in the tick where an
        // assignment survives to be drawn.
        //
        // A Graphic_SINGLE returns the same material for EVERY Rot4, so installing one makes the
        // rotation tracker's opinion irrelevant. He is then locked to the facing we chose until
        // the normal set is restored.
        private static readonly Dictionary<string, Graphic> frozenCache = new Dictionary<string, Graphic>();

        private static string FacingSuffix(Rot4 rot)
        {
            if (rot == Rot4.North) { return "_north"; }
            if (rot == Rot4.East) { return "_east"; }
            if (rot == Rot4.West) { return "_west"; }
            return "_south";
        }

        /// <summary>
        /// Lock him to one facing of one state's sprite set, so nothing can turn him.
        /// Returns false for anything that is not our dragon - the caller then does nothing.
        /// </summary>
        public static bool SetFrozenFacing(Pawn pawn, AlduinMovementState state, Rot4 rot)
        {
            if (!IsAlduin(pawn) || pawn.Drawer == null || pawn.Drawer.renderer == null)
            {
                return false;
            }
            PawnGraphicSet graphics = pawn.Drawer.renderer.graphics;
            if (graphics == null)
            {
                return false;
            }
            string path = PathFor(state) + FacingSuffix(rot);
            Graphic wanted;
            if (!frozenCache.TryGetValue(path, out wanted) || wanted == null)
            {
                float size = DrawSizeFor(state);
                wanted = GraphicDatabase.Get<Graphic_Single>(
                    path, ShaderDatabase.Cutout, new Vector2(size, size), Color.white);
                frozenCache[path] = wanted;
            }
            if (wanted == null || graphics.nakedGraphic == wanted)
            {
                return false;
            }
            graphics.nakedGraphic = wanted;
            // REQUIRED - the material is cached against a hash that does not include which
            // Graphic is installed, so without this the old one keeps drawing until he turns.
            graphics.ClearCache();
            return true;
        }

        /// <summary>Which set is currently on the pawn. Null if it is none of ours.</summary>
        public static AlduinMovementState? CurrentState(Pawn pawn)
        {
            if (pawn == null || pawn.Drawer == null || pawn.Drawer.renderer == null)
            {
                return null;
            }
            Graphic current = pawn.Drawer.renderer.graphics.nakedGraphic;
            if (current == null)
            {
                return null;
            }
            foreach (AlduinMovementState state in new[] {
                AlduinMovementState.Grounded, AlduinMovementState.Soar })
            {
                if (current == GraphicFor(state))
                {
                    return state;
                }
            }
            // Flight is any of the eight octants, not one graphic.
            foreach (AlduinOctant octant in new[] {
                AlduinOctant.N, AlduinOctant.NE, AlduinOctant.E, AlduinOctant.SE,
                AlduinOctant.S, AlduinOctant.SW, AlduinOctant.W, AlduinOctant.NW })
            {
                if (current == GraphicForOctant(octant))
                {
                    return AlduinMovementState.Flight;
                }
            }
            return null;
        }

        public static bool IsAlduin(Pawn pawn)
        {
            return pawn != null && pawn.def != null && pawn.def.defName == "Dovahkiin_Alduin_Test";
        }
    }
}
