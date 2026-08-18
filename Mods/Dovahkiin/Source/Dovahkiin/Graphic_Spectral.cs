// A pawn that LOOKS invisible but is not. SPEC.md 6.5 (the summons and targeting).
//
// ============================================================================================
// THE PROBLEM THIS SOLVES
// ============================================================================================
// The Ancient Dragonborn and Call of Valor's hero were made spectral with vanilla's
// HediffComp_Invisibility. That gives exactly the right look - and it also removes them from
// the game's threat model entirely, which is the opposite of the tanking role they exist for.
//
// Verified by scanning all 12,819 types in Assembly-CSharp for callers of PawnUtility.IsInvisible:
//   Verse.Pawn.ThreatDisabled          <- the decisive one; an invisible pawn is not a threat
//   JobGiver_AIFightEnemy, JobGiver_ReactToCloseMeleeThreat, JobGiver_Berserk.FindPawnTarget
//   Toils_Combat.FollowAndMeleeAttack, JobDriver_AttackStatic
//   Verse.GenUI.TargetsAt              <- so the PLAYER cannot click them either
//
// The user reported it in play: Alduin chased fleeing colonists and never once attacked the
// summon standing next to him.
//
// ============================================================================================
// WHY THIS COSTS NOTHING VISUALLY - IT IS THE SAME MATERIALS, NOT A LOOKALIKE
// ============================================================================================
// Verse.InvisibilityMatPool.GetInvisibleMat is PUBLIC STATIC, and it is the whole of what
// vanilla invisibility does to a material:
//
//     value.shader = ShaderDatabase.Invisible;
//     value.SetTexture(NoiseTex, TexGame.InvisDistortion);
//     value.color = new Color(0.75f, 0.93f, 0.98f, 0.5f);
//
// So calling it ourselves gives the same shader, the same distortion noise and the same colour.
// This is pixel-identical to the signed-off look, shimmer included - not an approximation of it.
//
// What changes is only that the pawn no longer carries HediffComp_Invisibility, so
// IsInvisible() is false, so ThreatDisabled is false, so enemies can see him.
//
// ============================================================================================
// WHY NOT HARMONY
// ============================================================================================
// The alternative was patching ThreatDisabled plus four or five JobGivers - several needing
// TRANSPILERS, because the IsInvisible check sits mid-method. That is the AI targeting hot path,
// RocketMan caches exactly that kind of evaluation, and a missed call site would give enemies
// that target the summon SOMETIMES. This has none of that: no patch, no reflection, and it
// reuses the nakedGraphic swap already proven in play on the dragon.
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// Wraps another Graphic and hands out vanilla's own invisibility materials for it.
    /// Verse.Graphic has no abstract members, so only the Mat* surface needs overriding;
    /// everything else - DrawWorker, Print, the draw size - runs on the base implementation
    /// reading the fields copied from the inner graphic.
    /// </summary>
    public class Graphic_Spectral : Graphic
    {
        private Graphic inner;

        public Graphic Inner { get { return inner; } }

        public static Graphic Wrap(Graphic source)
        {
            if (source == null)
            {
                return null;
            }
            // Never wrap a wrapper - re-applying on a tick would otherwise nest them forever.
            Graphic_Spectral already = source as Graphic_Spectral;
            if (already != null)
            {
                return already;
            }
            Graphic_Spectral wrapper = new Graphic_Spectral();
            wrapper.inner = source;
            // Carry the inner graphic's identity so anything reading these still behaves:
            // MeshPool sizing, corpse colouring, and Graphic's own DrawWorker all use them.
            wrapper.path = source.path;
            wrapper.color = source.color;
            wrapper.colorTwo = source.colorTwo;
            wrapper.drawSize = source.drawSize;
            wrapper.data = source.data;
            // ⚠ BUILD THE INVISIBLE MATERIALS NOW, HERE, OUTSIDE THE RENDER PATH. Wrapping is the
            // one moment we are guaranteed not to be mid-draw, and leaving it to the first MatAt
            // costs one skipped frame and one red render error per material. See Prewarm.
            wrapper.Prewarm();
            return wrapper;
        }

        private static Material Spectral(Material baseMat)
        {
            if (baseMat == null)
            {
                return null;
            }
            // ⚠ CACHED PER BASE MATERIAL - BUT THE **FIRST** CALL IS NOT FREE, AND THAT IS WHY
            // Prewarm EXISTS. This comment used to end "...and safe to reach from a draw path",
            // which was the wrong half of the truth. See Prewarm below.
            return InvisibilityMatPool.GetInvisibleMat(baseMat);
        }

        /// <summary>
        /// BUILD EVERY INVISIBLE MATERIAL THIS WRAPPER CAN HAND OUT, *BEFORE* ANYTHING DRAWS.
        ///
        /// ⚠⚠ WITHOUT THIS, EVERY SPECTRAL PAWN THROWS ONE RENDER ERROR PER MATERIAL ON ITS FIRST
        /// FRAME. The user, across three sessions: *"the logs popped up a lot"*, and the log:
        ///
        ///     SetPass(0) call failed on material Custom/CutoutRecolor_Naked_Male_east
        ///                                        with shader Custom/Invisible
        ///     DrawMesh requires a successful material.SetPass before!
        ///
        /// **Exactly one pair per material, at the instant the summon first drew, then never
        /// again** - which is the shape of a cache miss, and `Verse.InvisibilityMatPool` is where
        /// it happens (decompiled, not guessed):
        ///
        /// ```csharp
        /// if (!materials.TryGetValue(baseMat, out var value)) {
        ///     value = MaterialAllocator.Create(baseMat);        // a NEW Unity Material...
        ///     value.shader = ShaderDatabase.Invisible;          // ...whose shader is set...
        ///     value.SetTexture(NoiseTex, TexGame.InvisDistortion);
        ///     materials.Add(baseMat, value);
        /// }
        /// ```
        ///
        /// So the first `MatAt` of a draw **allocates a material and assigns its shader**, and
        /// Unity is then asked to `SetPass` it in that same frame. It refuses, the draw is skipped,
        /// and the pawn is invisible for that frame - not shimmering, *absent*.
        ///
        /// **THE FIX IS ORDER, NOT MACHINERY: touch every material once from outside the render
        /// path.** By the first draw the pool is warm and `SetPass` succeeds. Doing this at wrap
        /// time also means it costs nothing per frame.
        ///
        /// ⚠ Wrapped in try/catch because `MatSingle` legitimately throws on some Graphic
        /// subclasses (a multi-directional graphic has no single material), and a pre-warm must
        /// never be able to take down the thing it is optimising.
        /// </summary>
        public void Prewarm()
        {
            if (inner == null)
            {
                return;
            }
            try
            {
                Spectral(inner.MatAt(Rot4.North));
                Spectral(inner.MatAt(Rot4.East));
                Spectral(inner.MatAt(Rot4.South));
                Spectral(inner.MatAt(Rot4.West));
            }
            catch { /* a graphic that cannot answer per-rotation is not an error here */ }
            try
            {
                Spectral(inner.MatSingle);
            }
            catch { /* MatSingle throws on Graphic_Multi and friends - expected */ }
        }

        public override Material MatSingle { get { return Spectral(inner.MatSingle); } }
        public override Material MatNorth  { get { return Spectral(inner.MatNorth); } }
        public override Material MatEast   { get { return Spectral(inner.MatEast); } }
        public override Material MatSouth  { get { return Spectral(inner.MatSouth); } }
        public override Material MatWest   { get { return Spectral(inner.MatWest); } }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            return Spectral(inner.MatAt(rot, thing));
        }

        public override Material MatSingleFor(Thing thing)
        {
            return Spectral(inner.MatSingleFor(thing));
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            // Recolouring a spectral pawn is meaningless - the invisibility material overrides
            // the colour anyway - so keep wrapping rather than silently dropping back to the
            // solid graphic, which is what returning inner would do.
            return Wrap(inner.GetColoredVersion(newShader, newColor, newColorTwo));
        }

        public override string ToString()
        {
            return "Spectral(" + (inner == null ? "null" : inner.ToString()) + ")";
        }
    }
}
