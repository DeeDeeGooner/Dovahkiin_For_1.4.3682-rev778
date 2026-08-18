// Makes a summon look spectral without making it untargetable. See Graphic_Spectral.cs for
// the whole rationale; this is just the application.
//
// A humanlike is drawn from SEVERAL graphics, not one - body, head, hair, beard, fur - and
// every one of them has to be wrapped or he comes out as a solid head floating over a ghostly
// body. That is the only reason this is more than one line.
using Verse;

namespace Dovahkiin
{
    public static class SpectralPawnUtility
    {
        /// <summary>
        /// Wrap every graphic this pawn is drawn from, so he renders exactly as vanilla
        /// invisibility would while remaining a legal attack target.
        ///
        /// Safe and cheap to call repeatedly: Graphic_Spectral.Wrap refuses to wrap a wrapper,
        /// and this returns false when nothing needed doing - so it can live on a tick that
        /// re-applies after anything re-resolves the pawn's graphics.
        /// </summary>
        public static bool MakeSpectral(Pawn pawn)
        {
            if (pawn == null || pawn.Drawer == null || pawn.Drawer.renderer == null)
            {
                return false;
            }
            PawnGraphicSet g = pawn.Drawer.renderer.graphics;
            if (g == null || !g.AllResolved)
            {
                // Nothing resolved yet - the pawn has just been generated. Trying now would
                // wrap nulls and then be overwritten by the first real resolve anyway.
                return false;
            }

            bool changed = false;
            changed |= Swap(ref g.nakedGraphic);
            changed |= Swap(ref g.headGraphic);
            changed |= Swap(ref g.hairGraphic);
            changed |= Swap(ref g.beardGraphic);
            changed |= Swap(ref g.furCoveredGraphic);
            // Corpse, rotting and dessicated graphics are deliberately NOT wrapped: a summon
            // that dies leaves no corpse (the doomed pattern - RISKS.md 9), and if one ever
            // did, a solid corpse is the correct thing to see.

            if (changed)
            {
                // REQUIRED. PawnGraphicSet caches materials against a hash of
                // (facing, RotDrawMode, drawClothes, dead); swapping a graphic changes none of
                // those, so without this the OLD material keeps drawing until he happens to
                // turn. Same trap as the dragon's state swap.
                //
                // NOT SetAllGraphicsDirty() - that calls ResolveAllGraphics(), which rebuilds
                // every graphic from the pawn's story and throws these wrappers away.
                g.ClearCache();
            }
            return changed;
        }

        private static bool Swap(ref Graphic slot)
        {
            if (slot == null || slot is Graphic_Spectral)
            {
                return false;
            }
            slot = Graphic_Spectral.Wrap(slot);
            return true;
        }

        /// <summary>Is this pawn currently wearing the spectral wrappers?</summary>
        public static bool IsSpectral(Pawn pawn)
        {
            if (pawn == null || pawn.Drawer == null || pawn.Drawer.renderer == null)
            {
                return false;
            }
            PawnGraphicSet g = pawn.Drawer.renderer.graphics;
            return g != null && g.nakedGraphic is Graphic_Spectral;
        }
    }
}
