// ============================================================================================
// CALL OF VALOR - the shout itself.
//
// The first of the three QUEST-LOCKED shouts. Unlike every other shout in this mod it sends
// nothing travelling: it opens a portal on the targeted cell and a hero of Sovngarde steps out
// of it. All of that machinery already exists and is playtested - this comp only aims it.
//
// LEVELS SCALE THE HERO'S STAY, and that is a decision rather than a spec line. The user's brief
// fixed his lifetime ("twice the Ancient Dragonborn") and said nothing about what one or two
// words should do. Duration is what TES5's own Call of Valor scales, and it is what this mod
// already scales on every other multi-level shout, so it is the least surprising answer. One
// word is a third of the stay, two is two thirds, three is the full 12 hours the spec names.
// FLAGGED so it can be overruled: it is one list in DovahkiinTuningDef.
// ============================================================================================
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
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
            CallOfValorUtility.TrySummon(caster, target.Cell, LifetimeForLevel(Props.level));
        }

        /// <summary>
        /// How long the hero stays at this word count.
        ///
        /// Read from the tuning list when it is present and long enough, so the whole ladder can
        /// be retuned without a rebuild. Falls back to thirds of the full lifetime - derived, not
        /// written out, for the same reason the full lifetime itself derives from the Ancient
        /// Dragonborn's: a number written down stops meaning what it said the moment the number
        /// it was derived from moves, and that has already happened once in this project.
        /// </summary>
        private static int LifetimeForLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, 3);
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning == null)
            {
                return 30000 * clamped / 3;
            }
            List<int> ladder = tuning.callOfValorLifetimeByLevel;
            if (ladder != null && ladder.Count >= clamped && ladder[clamped - 1] > 0)
            {
                return ladder[clamped - 1];
            }
            int full = tuning.callOfValorLifetimeTicks > 0
                ? tuning.callOfValorLifetimeTicks
                : tuning.ancientDragonbornLifetimeTicks * 2;
            return full * clamped / 3;
        }
    }
}
