// Move speed for an airborne dragon. SPEC.md 6.5: soar x1.2 of grounded, flight x1.8.
//
// ============================================================================================
// WHY A StatPart AND NOT statFactors ON THE HEDIFF
// ============================================================================================
// The obvious route is a HediffDef with two stages carrying <statFactors><MoveSpeed>. It works,
// it needs no code and no patch - and it was rejected, because it would write each speed TWICE:
// once as the stage's factor in the hediff XML, and once as the severity the comp sets to
// select that stage. The notebook already records what that costs - ancientDragonbornLifetimeTicks
// had an XML default of 15000 and a C# fallback of 3750, and the disagreement could only ever
// surface in the failure case, which is precisely when nobody is in a position to notice.
//
// So the severity CARRIES the factor and this reads it back. One number, in DovahkiinTuningDef,
// where CLAUDE.md says tuning numbers live.
//
// ON COST: this runs for every MoveSpeed query on every pawn in the game, and RocketMan is
// installed. The guard is a null check plus a hediff-set lookup that returns immediately for
// anything that is not a dragon - the same shape as the mod's existing combat-path patches,
// which all open with registry.IsDovahkiin.
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public class StatPart_DragonAirborne : StatPart
    {
        private static HediffDef airborneDef;

        private static HediffDef AirborneDef
        {
            get
            {
                if (airborneDef == null)
                {
                    airborneDef = DefDatabase<HediffDef>.GetNamedSilentFail("Dovahkiin_DragonAirborne");
                }
                return airborneDef;
            }
        }

        private static float FactorFor(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.health == null)
            {
                return 1f;
            }
            HediffDef def = AirborneDef;
            if (def == null)
            {
                return 1f;
            }
            Hediff airborne = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (airborne == null)
            {
                return 1f;
            }
            // Severity IS the multiplier - see the header. Clamped low so a corrupt or
            // half-written severity can never freeze a dragon in place.
            return airborne.Severity < 0.05f ? 1f : airborne.Severity;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing)
            {
                return;
            }
            val *= FactorFor(req.Thing);
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing)
            {
                return null;
            }
            float factor = FactorFor(req.Thing);
            if (factor <= 1.0001f && factor >= 0.9999f)
            {
                return null;
            }
            // Named rather than silent: a dragon moving faster than its stat card claims is
            // exactly the kind of thing that reads as a bug.
            return "Airborne: x" + factor.ToString("F2");
        }
    }
}
