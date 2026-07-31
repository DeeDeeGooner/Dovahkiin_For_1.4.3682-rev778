// ============================================================================================
// CALL OF VALOR - the hero of Sovngarde.
//
// A SUBCLASS OF Hediff_AncientDragonborn, deliberately, and it is nearly empty. Everything that
// makes a summon safe lives in the parent and is playtested: the doomed lifetime, the kill on
// expiry, the ally-safety cone check before any breath, joining whatever the Dovahkiin is
// fighting, dropping the player faction before being destroyed so Ideology does not throw.
//
// The user's brief was "the Ancient Dragonborn's pattern with different numbers", and RISKS.md
// section 9 puts temporary pawns as the top save-corruption risk in the mod. Copying 589 lines
// to change one of them would double the surface area of the riskiest thing here and guarantee
// the two drift apart - and a fix to the shared AI would then reach one summon and not the
// other. So the only thing overridden is the only thing that actually differs in code.
//
// EVERYTHING ELSE ABOUT HIM IS DEF DATA, not C#:
//   - armour 0.66 sharp / 0.33 blunt (his HediffDef's stages) against the Ancient Dragonborn's
//     0.75/0.75. Deliberately weaker, and the numbers are real: vanilla plate carries
//     StuffEffectMultiplierArmor 0.73 and steel StuffPower_Armor_Sharp 0.9 / Blunt 0.45, giving
//     0.657 and 0.329.
//   - twice the lifetime - 30000 ticks, 12 in-game hours, against his 15000. "Double his" is the
//     rule the user gave, so it is derived from that field rather than written out again.
//   - his own greatsword, his own armour folder, and NO AURA - see CallOfValorUtility.
// ============================================================================================
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// The doomed hediff every Call of Valor summon carries. Incurable, untendable, kills on
    /// expiry - the same construction that made Soul Tear's puppet safe, which is what removed
    /// the highest save-corruption risk in the mod.
    /// </summary>
    public class Hediff_CallOfValor : Hediff_AncientDragonborn
    {
        /// <summary>
        /// UNRELENTING FORCE AND FROST BREATH ONLY - the user's spec, and no fire.
        ///
        /// Force first so the coin flip the summon already makes seeds which of the two he opens
        /// with, exactly as it does for the Ancient Dragonborn: that flip sets shoutCycle to
        /// SHOUT_FROST (1) or SHOUT_FIRE (0), which index this two-element array as slots 1 and
        /// 0 - Frost or Force. The flip keeps meaning something rather than being discarded.
        ///
        /// Cycled rather than rolled, for the parent's reason: he gets only a handful of casts
        /// in one summoning, and at that sample size a random pick routinely gives a whole
        /// summoning of one shout.
        /// </summary>
        private static readonly int[] ForceAndFrost = { SHOUT_FORCE, SHOUT_FROST };

        protected override int[] ShoutSequence
        {
            get { return ForceAndFrost; }
        }
    }
}
