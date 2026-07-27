// Implements: SPEC.md 1 (who may awaken), 3.1 (trait, hediffs, title, backstory), 10 (heirs).
//
// Helpers only. Nothing here may grant Trait_Dovahkiin on its own - everything routes through
// GameComponent_DragonbornRegistry.TryAwaken (CLAUDE.md invariant 1).
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public static class DovahkiinUtility
    {
        /// <summary>
        /// SPEC.md 1. Any race, any xenotype, any gene set - modded ones included.
        /// The filter is capability and circumstance, never biology.
        /// </summary>
        public static bool EligibleToAwaken(Pawn p)
        {
            if (p == null || p.Dead || p.Destroyed)
            {
                return false;
            }
            // Humanlike only. Animals, mechs and dragons are excluded (SPEC.md 1) - but note
            // this is a check on intelligence, NOT on race or xenotype defName.
            if (p.RaceProps == null || !p.RaceProps.Humanlike)
            {
                return false;
            }
            if (p.story == null || p.story.traits == null)
            {
                return false;
            }
            if (p.story.traits.HasTrait(DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin))
            {
                return false;
            }
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg != null && reg.IsLockedOut(p))
            {
                return false;
            }
            return true;
        }

        /// <summary>Grants the trait, hediffs and title. Registry-only - do not call directly.</summary>
        public static void ApplyDovahkiinIdentity(Pawn p, AwakeningCause cause)
        {
            if (!p.story.traits.HasTrait(DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin))
            {
                p.story.traits.GainTrait(new Trait(DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin, 0, true));
            }

            EnsureHediff(p, DovahkiinDefOf.Dovahkiin_DragonSoulAttunement);
            EnsureHediff(p, DovahkiinDefOf.Dovahkiin_TheVoice);

            // SPEC.md 3.1: a custom title, deliberately NOT a RoyalTitleDef. Pawn_StoryTracker
            // exposes a plain settable title field, so this needs no Harmony patch at all.
            p.story.title = "Dovahkiin_Title".Translate();

            ApplyAwakeningBackstory(p, cause);

            // Recache anything that reads traits or story.
            p.Notify_DisabledWorkTypesChanged();
            if (p.needs != null)
            {
                p.needs.AddOrRemoveNeedsAsAppropriate();
            }
        }

        /// <summary>
        /// Restores any missing piece of the Dovahkiin's identity and reports what it fixed.
        /// Called on every load (see the registry's FinalizeInit).
        ///
        /// Deliberately idempotent and additive: it never removes anything, never touches the
        /// backstory (the awakening cause is not recoverable after the fact), and returns an
        /// empty list when the pawn is already whole - which is the normal case.
        /// </summary>
        public static List<string> RepairIdentity(Pawn p)
        {
            List<string> repaired = new List<string>();
            if (p == null || p.story == null || p.story.traits == null || p.health == null)
            {
                return repaired;
            }

            if (DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin != null
                && !p.story.traits.HasTrait(DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin))
            {
                p.story.traits.GainTrait(new Trait(DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin, 0, true));
                repaired.Add("trait");
            }

            if (DovahkiinDefOf.Dovahkiin_DragonSoulAttunement != null
                && p.health.hediffSet.GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_DragonSoulAttunement) == null)
            {
                p.health.AddHediff(DovahkiinDefOf.Dovahkiin_DragonSoulAttunement);
                repaired.Add("attunement");
            }

            if (DovahkiinDefOf.Dovahkiin_TheVoice != null
                && p.health.hediffSet.GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_TheVoice) == null)
            {
                p.health.AddHediff(DovahkiinDefOf.Dovahkiin_TheVoice);
                repaired.Add("the Voice");
            }

            if (p.story.title.NullOrEmpty())
            {
                p.story.title = "Dovahkiin_Title".Translate();
                repaired.Add("title");
            }

            // Phase 2: abilities are granted through the vanilla tracker, which is separate
            // state from our shout levels. Reconcile them here so a load can never leave a
            // shout learned-but-uncastable, or castable-but-unlearned.
            ShoutUtility.SyncAbilities(p);

            return repaired;
        }

        /// <summary>Dev tool only, for Clear registry.</summary>
        public static void StripDovahkiinIdentity(Pawn p)
        {
            if (p == null || p.story == null)
            {
                return;
            }
            Trait t = p.story.traits.GetTrait(DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin);
            if (t != null)
            {
                p.story.traits.RemoveTrait(t);
            }
            RemoveHediff(p, DovahkiinDefOf.Dovahkiin_DragonSoulAttunement);
            RemoveHediff(p, DovahkiinDefOf.Dovahkiin_TheVoice);
            p.story.title = null;
        }

        /// <summary>
        /// SPEC.md 3.1: the pawn keeps its childhood; its adulthood reflects how it awakened.
        /// A pawn awakening as a child has no adulthood slot yet, so this is a no-op for them
        /// and is reapplied when they come of age (SPEC.md 3.5).
        /// </summary>
        private static void ApplyAwakeningBackstory(Pawn p, AwakeningCause cause)
        {
            if (p.story.Adulthood == null)
            {
                return;
            }

            string defName;
            switch (cause)
            {
                case AwakeningCause.Slaughter:
                    defName = "Dovahkiin_Backstory_AwakenedBySlaughter";
                    break;
                case AwakeningCause.DragonbloodHeir:
                    defName = "Dovahkiin_Backstory_DragonbloodHeir";
                    break;
                case AwakeningCause.Stranger:
                    defName = "Dovahkiin_Backstory_TheStranger";
                    break;
                case AwakeningCause.Scenario:
                    defName = "Dovahkiin_Backstory_Prophecy";
                    break;
                default:
                    return; // Debug awakenings leave the pawn's history alone.
            }

            BackstoryDef bs = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (bs != null)
            {
                p.story.Adulthood = bs;
            }
        }

        // --- Dragonblood, SPEC.md 10 ---

        public static bool IsDragonblood(Pawn p)
        {
            return p != null
                && p.story != null
                && p.story.traits != null
                && p.story.traits.HasTrait(DovahkiinDefOf.Dovahkiin_Trait_Dragonblood);
        }

        /// <summary>
        /// SPEC.md 10: heritable onward at the same strength, never stacked. A child qualifies
        /// if either parent is the Dovahkiin or is themselves dragonblood.
        /// </summary>
        public static bool ShouldInheritDragonblood(Pawn parent)
        {
            if (parent == null)
            {
                return false;
            }
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg != null && reg.IsDovahkiin(parent))
            {
                return true;
            }
            return IsDragonblood(parent);
        }

        public static void GrantDragonblood(Pawn p)
        {
            if (p == null || p.story == null || p.story.traits == null)
            {
                return;
            }
            if (p.RaceProps == null || !p.RaceProps.Humanlike)
            {
                return;
            }
            if (p.story.traits.HasTrait(DovahkiinDefOf.Dovahkiin_Trait_Dragonblood))
            {
                return; // Never stack - SPEC.md 10.
            }
            p.story.traits.GainTrait(new Trait(DovahkiinDefOf.Dovahkiin_Trait_Dragonblood, 0, true));
        }

        /// <summary>
        /// Living dragonblood pawns anywhere the game can see them. Used by the lockout in
        /// SPEC.md 3.2. Deliberately not cached - it runs once, on a once-per-save event.
        /// </summary>
        public static IEnumerable<Pawn> AllDragonbloodPawns()
        {
            foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive)
            {
                if (IsDragonblood(p))
                {
                    yield return p;
                }
            }
        }

        // --- small helpers ---

        private static void EnsureHediff(Pawn p, HediffDef def)
        {
            if (def == null || p.health == null)
            {
                return;
            }
            if (p.health.hediffSet.GetFirstHediffOfDef(def) == null)
            {
                p.health.AddHediff(def);
            }
        }

        private static void RemoveHediff(Pawn p, HediffDef def)
        {
            if (def == null || p.health == null)
            {
                return;
            }
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (h != null)
            {
                p.health.RemoveHediff(h);
            }
        }
    }
}
