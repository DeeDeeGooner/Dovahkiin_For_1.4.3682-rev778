// Implements: SPEC.md 4.1 - the word/shout data model, and OD-10's word-gates-level rule.
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// One word of power. Discovery is world state and permanent (SPEC.md 4.1, OD-8), so a word
    /// is identified by defName in the registry's wordsDiscoveredWorld list.
    /// </summary>
    public class WordOfPowerDef : Def
    {
        /// <summary>The dragon-tongue word as written on a wall: "Fus", "Ro", "Dah".</summary>
        public string word;

        /// <summary>Its meaning, shown when the word is learned: "Force".</summary>
        public string meaning;
    }

    /// <summary>
    /// A shout: three words, three levels, one AbilityDef per level.
    ///
    /// One AbilityDef per level rather than one scaling def, because SPEC.md 4.4 gives the levels
    /// genuinely different behaviour - Unrelenting Force goes from staggering a single target to
    /// a knockback cone - which is cleaner declared than computed.
    /// </summary>
    public class ShoutDef : Def
    {
        /// <summary>Exactly three, in order. Word N gates level N (OD-10).</summary>
        public List<WordOfPowerDef> words = new List<WordOfPowerDef>();

        /// <summary>Exactly three, in order: the ability granted at level 1, 2 and 3.</summary>
        public List<AbilityDef> abilitiesByLevel = new List<AbilityDef>();

        /// <summary>Thu'um spent per cast, indexed by level 1..3.</summary>
        public List<float> thuumCostByLevel = new List<float>();

        /// <summary>Shared-cooldown length in ticks this shout imposes, indexed by level 1..3.</summary>
        public List<int> cooldownTicksByLevel = new List<int>();

        /// <summary>
        /// Dragons and undead use the same shout with bigger numbers, never a duplicate def
        /// (SPEC.md 4.6). False for shouts no non-Dovahkiin may ever use.
        /// </summary>
        public bool availableToDragons;

        /// <summary>SPEC.md 4.5: the draugr pool is Unrelenting Force and Frost Breath only.</summary>
        public bool availableToUndead;

        /// <summary>Dragonrend is granted by the World-Eater quest, not found on walls (SPEC.md 4.4b).</summary>
        public bool questGrantedWords;

        public int MaxAttainableLevel(GameComponent_DragonbornRegistry reg)
        {
            if (reg == null || words == null)
            {
                return 0;
            }
            // OD-10: level N requires N discovered words. Words are ordered, so the attainable
            // level is simply how many of them have been found, counting from the first.
            int found = 0;
            for (int i = 0; i < words.Count; i++)
            {
                if (words[i] == null || !reg.IsWordDiscovered(words[i].defName))
                {
                    break;
                }
                found++;
            }
            return found;
        }

        public float ThuumCost(int level)
        {
            return IndexOr(thuumCostByLevel, level, 5f);
        }

        public int CooldownTicks(int level)
        {
            return (int)IndexOr(cooldownTicksByLevel, level, 1200f);
        }

        public AbilityDef AbilityForLevel(int level)
        {
            if (abilitiesByLevel == null || level < 1 || level > abilitiesByLevel.Count)
            {
                return null;
            }
            return abilitiesByLevel[level - 1];
        }

        private static float IndexOr(List<float> list, int level, float fallback)
        {
            if (list == null || level < 1 || level > list.Count)
            {
                return fallback;
            }
            return list[level - 1];
        }

        private static float IndexOr(List<int> list, int level, float fallback)
        {
            if (list == null || level < 1 || level > list.Count)
            {
                return fallback;
            }
            return list[level - 1];
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
            {
                yield return e;
            }
            // Catch authoring mistakes at startup rather than at cast time.
            if (words == null || words.Count != 3)
            {
                yield return "ShoutDef needs exactly 3 words (SPEC.md 4.1), has "
                    + (words == null ? 0 : words.Count);
            }
            if (abilitiesByLevel == null || abilitiesByLevel.Count != 3)
            {
                yield return "ShoutDef needs exactly 3 abilities, one per level, has "
                    + (abilitiesByLevel == null ? 0 : abilitiesByLevel.Count);
            }
        }
    }
}
