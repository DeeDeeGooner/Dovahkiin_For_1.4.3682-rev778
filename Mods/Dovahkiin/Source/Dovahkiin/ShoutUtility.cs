// Implements: SPEC.md 4.1 (levels), 4.2 (shared cooldown + strain), 5.4 (witnessing a shout).
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public static class ShoutUtility
    {
        /// <summary>
        /// Reconciles the abilities RimWorld has granted a pawn with the shout levels this mod
        /// thinks they have. Idempotent, and safe to call on load - which is the point, since
        /// abilities are granted through the vanilla tracker and could drift from our state.
        /// </summary>
        public static void SyncAbilities(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null)
            {
                return;
            }
            Hediff_TheVoice voice = GetVoice(pawn);
            foreach (ShoutDef shout in DefDatabase<ShoutDef>.AllDefsListForReading)
            {
                int level = voice == null ? 0 : voice.GetShoutLevel(shout.defName);
                for (int lv = 1; lv <= 3; lv++)
                {
                    AbilityDef ability = shout.AbilityForLevel(lv);
                    if (ability == null)
                    {
                        continue;
                    }
                    bool shouldHave = (lv == level); // Only the current tier is castable.
                    bool has = pawn.abilities.GetAbility(ability, false) != null;
                    if (shouldHave && !has)
                    {
                        pawn.abilities.GainAbility(ability);
                    }
                    else if (!shouldHave && has)
                    {
                        pawn.abilities.RemoveAbility(ability);
                    }
                }
            }
        }

        /// <summary>
        /// SPEC.md 4.1 / OD-10. Spends souls to raise a shout one level. Fails, without spending,
        /// if the words have not been found or the pawn cannot afford it.
        /// </summary>
        public static bool TryRaiseLevel(Pawn pawn, ShoutDef shout, out string reason)
        {
            reason = null;
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            Hediff_TheVoice voice = GetVoice(pawn);
            if (reg == null || voice == null || shout == null)
            {
                reason = "Dovahkiin_Shout_Reason_NoVoice".Translate();
                return false;
            }

            int current = voice.GetShoutLevel(shout.defName);
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            int maxLevel = tuning == null ? 3 : tuning.maxWordLevel;
            if (current >= maxLevel)
            {
                reason = "Dovahkiin_Shout_Reason_Maxed".Translate();
                return false;
            }

            // OD-10 is the whole point of this check: souls alone are not enough.
            int attainable = shout.MaxAttainableLevel(reg);
            if (current + 1 > attainable)
            {
                reason = "Dovahkiin_Shout_Reason_NeedWord".Translate((current + 1).Named("LEVEL"));
                return false;
            }

            int cost = tuning == null ? 1 : tuning.soulCostPerWordLevel;
            if (!voice.TrySpendSouls(cost))
            {
                reason = "Dovahkiin_Shout_Reason_NoSouls".Translate(cost.Named("COST"));
                return false;
            }

            voice.SetShoutLevel(shout.defName, current + 1);
            SyncAbilities(pawn);
            return true;
        }

        // --- Casting: cost, shared cooldown, strain. SPEC.md 4.2 ---

        /// <summary>
        /// Everything that must be true before any shout leaves the mouth. Returns null when the
        /// shout may be cast, or a player-facing reason why not.
        /// </summary>
        public static string CannotCastReason(Pawn pawn, ShoutDef shout, int level)
        {
            Hediff_TheVoice voice = GetVoice(pawn);
            if (voice == null)
            {
                return "Dovahkiin_Shout_Reason_NoVoice".Translate();
            }
            if (!voice.ThuumReady)
            {
                return "Dovahkiin_Shout_Reason_Recovering".Translate();
            }
            Need_Thuum need = GetThuum(pawn);
            float cost = shout.ThuumCost(level);
            if (need == null || !need.CanAfford(cost))
            {
                return "Dovahkiin_Shout_Reason_NoThuum".Translate(cost.ToString("F0").Named("COST"));
            }
            return null;
        }

        /// <summary>Called once a shout has actually been cast. Spends, starts recovery, strains.</summary>
        public static void NotifyShoutCast(Pawn pawn, ShoutDef shout, int level)
        {
            Hediff_TheVoice voice = GetVoice(pawn);
            Need_Thuum need = GetThuum(pawn);
            if (voice == null)
            {
                return;
            }

            if (need != null)
            {
                need.TrySpend(shout.ThuumCost(level));
            }

            // SPEC.md 4.2: a single shared cooldown across ALL shouts, whose length is set by the
            // shout just used - not a per-shout timer. Strain lengthens it when shouting repeatedly.
            int baseTicks = shout.CooldownTicks(level);
            float strainMult = voice.RegisterStrainAndGetMultiplier();

            // SPEC.md 4.4d: three-word Dragon Aspect shortens the shared cooldown while it is
            // up. Applied here rather than as a statFactor because the shared cooldown is this
            // mod's own number and no vanilla stat touches it.
            //
            // Deliberately AFTER strain: strain should still lengthen the cooldown normally and
            // then be discounted, not be bypassed. Dragon Aspect makes shouting easier, it does
            // not make the Voice tireless.
            float aspectMult = DragonAspectCooldownFactor(pawn);
            voice.StartThuumCooldown((int)(baseTicks * strainMult * aspectMult));

            NotifyWitnesses(pawn);
        }

        /// <summary>
        /// SPEC.md 4.4d: the shared shout cooldown is shortened while THREE-word Dragon Aspect
        /// is active. Returns 1 otherwise.
        ///
        /// Three words only. One and two words buy armour and resistances; the cooldown cut is
        /// what the third word adds, alongside the Ancient Dragonborn.
        /// </summary>
        private static float DragonAspectCooldownFactor(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null
                || DovahkiinDefOf.Dovahkiin_DragonAspect == null)
            {
                return 1f;
            }
            Hediff aspect = pawn.health.hediffSet
                .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_DragonAspect);
            if (aspect == null || Mathf.RoundToInt(aspect.Severity) < 3)
            {
                return 1f;
            }
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning == null)
            {
                return 1f;
            }
            return Mathf.Clamp(tuning.dragonAspectShoutCooldownFactor, 0.1f, 1f);
        }

        /// <summary>SPEC.md 5.4: witnessing a shout is a temporary colony mood lift.</summary>
        private static void NotifyWitnesses(Pawn caster)
        {
            if (caster == null || caster.Map == null
                || DovahkiinDefOf.Dovahkiin_Thought_WitnessedShout == null)
            {
                return;
            }
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            float radius = t == null ? 30f : t.shoutWitnessRadius;
            float radiusSq = radius * radius;

            List<Pawn> pawns = caster.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == caster || p.needs == null || p.needs.mood == null)
                {
                    continue;
                }
                if ((p.Position - caster.Position).LengthHorizontalSquared > radiusSq)
                {
                    continue;
                }
                p.needs.mood.thoughts.memories.TryGainMemory(
                    DovahkiinDefOf.Dovahkiin_Thought_WitnessedShout);
            }
        }

        // --- small accessors ---

        public static Hediff_TheVoice GetVoice(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || DovahkiinDefOf.Dovahkiin_TheVoice == null)
            {
                return null;
            }
            return pawn.health.hediffSet.GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_TheVoice)
                as Hediff_TheVoice;
        }

        public static Need_Thuum GetThuum(Pawn pawn)
        {
            if (pawn == null || pawn.needs == null)
            {
                return null;
            }
            return pawn.needs.TryGetNeed<Need_Thuum>();
        }
    }
}
