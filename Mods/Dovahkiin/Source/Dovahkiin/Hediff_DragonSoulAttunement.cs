// Implements: SPEC.md 5.1, 5.2 - permanent Attunement. Severity IS the soul count.
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// The single place Dragon Soul power lives (SPEC.md 3.1). Severity equals the number of
    /// souls absorbed, so it legitimately starts at 0 and must survive there.
    ///
    /// A custom class is required for exactly that reason: Hediff.ShouldRemove is
    /// "Severity &lt;= 0f" by default, which would silently delete this hediff on the first
    /// health tick after awakening, before the player ever kills a dragon.
    /// </summary>
    public class Hediff_DragonSoulAttunement : HediffWithComps
    {
        /// <summary>Never auto-removed. Zero souls is a valid, expected state.</summary>
        public override bool ShouldRemove
        {
            get { return false; }
        }

        /// <summary>Attunement is permanent and is never spent - SPEC.md 5.1.</summary>
        public int Souls
        {
            get { return Mathf_RoundToInt(Severity); }
        }

        public void AbsorbSouls(int count)
        {
            if (count > 0)
            {
                Severity += count;
            }
        }

        public override string TipStringExtra
        {
            get { return "Dovahkiin_Attunement_Tip".Translate(Souls.Named("SOULS")); }
        }

        // UnityEngine.Mathf is available, but keeping the rounding local avoids pulling a
        // Unity dependency into a file that otherwise needs none.
        private static int Mathf_RoundToInt(float f)
        {
            return (int)(f + 0.5f);
        }
    }
}
