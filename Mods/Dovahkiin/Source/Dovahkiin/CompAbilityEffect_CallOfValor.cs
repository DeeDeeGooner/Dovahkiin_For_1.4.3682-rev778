// ============================================================================================
// CALL OF VALOR - the shout itself.
//
// The first of the three QUEST-LOCKED shouts. Unlike every other shout in this mod it sends
// nothing travelling: it opens a portal on the targeted cell and a hero of Sovngarde steps out
// of it. All of that machinery already exists and is playtested - this comp only aims it.
//
// THERE IS NO LEVEL LADDER, AND THAT IS THE POINT.
//
// A level ladder WAS built here - range, stay and cost scaling across one, two and three words -
// and it was wrong. The user: *"Call of valor's ALL three words are learned at the same time
// during a quest... So no need to actualy tinker on what one word or two words will."*
//
// He is never at one or two words. The quest hands over all three at once, so the only state
// that can exist is three, and a ladder describing the other two describes nothing. Worse than
// useless: a future session reading `callOfValorLifetimeByLevel` would reasonably conclude the
// shout has a progression and tune it, and none of that tuning could ever be seen in play.
//
// The three AbilityDefs remain because the shout system is built on one ability per word, but
// they are IDENTICAL. Whichever one the level resolves to, the hero arrives at full strength.
//
// **The general rule: a knob nobody can turn is not harmless, it is a lie about the design.**
// ============================================================================================
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public class CompProperties_CallOfValor : CompProperties_AbilityEffect
    {
        /// <summary>
        /// Which word of the shout this ability is - 1, 2 or 3. Drives how long the hero stays.
        /// </summary>
        public int level = 3;

        public CompProperties_CallOfValor()
        {
            compClass = typeof(CompAbilityEffect_CallOfValor);
        }
    }

    public class CompAbilityEffect_CallOfValor : CompAbilityEffect
    {
        public new CompProperties_CallOfValor Props
        {
            get { return (CompProperties_CallOfValor)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
            {
                return;
            }
            // 0 = "use the tuning value", the same path the debug action takes. No level is
            // passed because no level can differ: all three words arrive together.
            CallOfValorUtility.TrySummon(caster, target.Cell, 0);
        }
    }
}
