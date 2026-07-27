// Implements: SPEC.md 5.4 - social consequences of being the Dovahkiin.
using RimWorld;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// A small flat opinion bonus from everyone, always (SPEC.md 5.4).
    ///
    /// Situational social thoughts are recalculated by the game on its own schedule, so this
    /// must stay cheap: two reference compares and a trait lookup, no allocation.
    /// </summary>
    public class ThoughtWorker_IsDovahkiin : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (pawn == other)
            {
                return ThoughtState.Inactive; // No opinion of yourself.
            }
            if (other == null || !other.RaceProps.Humanlike || !pawn.RaceProps.Humanlike)
            {
                return ThoughtState.Inactive;
            }
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null || !reg.IsDovahkiin(other))
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveDefault;
        }
    }

    /// <summary>
    /// SPEC.md 10 - dragonblood heirs carry a small social presence of their own.
    /// </summary>
    public class ThoughtWorker_IsDragonblood : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (pawn == other || other == null)
            {
                return ThoughtState.Inactive;
            }
            if (!DovahkiinUtility.IsDragonblood(other))
            {
                return ThoughtState.Inactive;
            }
            // The Dovahkiin themselves outranks this; don't stack the two.
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg != null && reg.IsDovahkiin(other))
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveDefault;
        }
    }
}
