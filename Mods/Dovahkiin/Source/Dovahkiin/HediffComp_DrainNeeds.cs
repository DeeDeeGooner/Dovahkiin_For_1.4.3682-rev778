// Implements: SPEC.md 4.4c promoted -> 4.4a - Drain Vitality (Gaan Lah Haas).
//
// Gaan = Stamina, Lah = Magicka, Haas = Health. The shout drains progressively more of the
// victim as words are learned:
//
//   level 1  stamina
//   level 2  stamina + magicka
//   level 3  stamina + magicka + health
//
// ---------------------------------------------------------------------------------------
// RimWorld of Magic is RECOMMENDED, NEVER REQUIRED  (CLAUDE.md anti-patterns)
// ---------------------------------------------------------------------------------------
// COMPAT.md section 5 establishes the facts this relies on, all read off disk:
//   - TM_Stamina and TM_Mana are ordinary NeedDefs (needClass TorannMagic.Need_Stamina /
//     Need_Mana), both <onlyIfCausedByHediff>true</onlyIfCausedByHediff>.
//   - Stamina exists only on a pawn carrying TM_MightUserHD; mana only with TM_MagicUserHD.
//     A pawn with neither class hediff HAS NEITHER NEED AT ALL.
//
// So this drains them through the *vanilla* Need.CurLevel API after a null-guarded
// DefDatabase lookup by defName. No assembly reference, no reflection, no MayRequire needed
// in the C#: with RWoM absent the defs simply resolve to null and those drains are skipped.
//
// The fallback when there is no magic mod - or when the victim is an ordinary colonist with no
// magic class - is deliberately NOT nothing. Vanilla Rest and Joy stand in for physical and
// mental fatigue, so the shout always does something recognisable: the victim is worn down,
// sluggish and dispirited, whatever mods are loaded.
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// Drain Vitality's hediff needs to remember WHO drained the victim, because the health it
    /// takes is given back to that caster. An ordinary Hediff carries no instigator, so this
    /// subclass adds one and serialises it.
    ///
    /// Scribe_References, not Scribe_Deep: the caster is a pawn that exists elsewhere in the
    /// save and must not be duplicated into this hediff. A null after load - caster dead, or
    /// gone from the map - simply means no healing happens, which is handled.
    /// </summary>
    public class Hediff_VitalityDrained : HediffWithComps
    {
        public Pawn drainedBy;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref drainedBy, "drainedBy");
        }
    }

    public class HediffCompProperties_DrainNeeds : HediffCompProperties
    {
        /// <summary>Ticks between drains. Interval-based, never per tick.</summary>
        public int intervalTicks = 120;

        /// <summary>
        /// Fraction of a need's full bar removed per interval, per point of severity.
        /// Severity is the shout level, so a fuller shout drains harder.
        /// </summary>
        public float restDrainPerInterval = 0.010f;
        public float joyDrainPerInterval = 0.014f;
        public float staminaDrainPerInterval = 0.030f;
        public float manaDrainPerInterval = 0.030f;

        /// <summary>
        /// Severity at or above which magicka is drained as well as stamina (shout level 2),
        /// and at which health starts draining too (level 3).
        /// </summary>
        public float manaDrainMinSeverity = 2f;
        public float healthDrainMinSeverity = 3f;

        /// <summary>Health drained per interval at level 3. Small - this ticks often.</summary>
        public float healthDrainPerInterval = 1.2f;

        /// <summary>Bounds total health drain, exactly as Marked for Death's cap does.</summary>
        public int maxHealthApplications = 12;

        /// <summary>
        /// Multiplier on the health returned to the caster. 1.0 gives back exactly what the
        /// victim lost.
        ///
        /// Raised above 1 after playtest: at 1.0 the drain healed 0.8 per victim every two
        /// seconds, so even four victims at once closed a fresh arrow wound barely faster than
        /// natural healing. The damage itself is deliberately NOT raised to compensate - it is
        /// pinned at half of Marked for Death's by design - so the yield is what moves instead.
        /// This shout steals life; it is allowed to draw more than the wound costs.
        /// </summary>
        public float casterHealFraction = 2.5f;

        /// <summary>
        /// Share of the healing that also goes into clearing the caster's BLOOD LOSS.
        ///
        /// Healing an injury lowers its severity, which slows the bleed - but blood already lost
        /// is a separate hediff and was never touched, so a bleeding caster kept reading as badly
        /// hurt while their wounds visibly closed. That was the exact playtest report.
        /// </summary>
        public float casterBloodLossFraction = 0.5f;

        /// <summary>
        /// Fraction of the drained stamina and mana handed to the caster. 1.0 means the caster
        /// gains exactly what the victim lost. Only ever transfers what was actually taken.
        /// </summary>
        public float casterNeedGainFraction = 1f;

        public DamageDef healthDamageDef;

        public FleckDef glowFleck;
        public float glowScale = 1.1f;
        public int glowIntervalTicks = 45;

        public HediffCompProperties_DrainNeeds()
        {
            compClass = typeof(HediffComp_DrainNeeds);
        }
    }

    public class HediffComp_DrainNeeds : HediffComp
    {
        private int ticksToNext;
        private int ticksToGlow;
        private int healthApplicationsUsed;

        // Resolved once, not per drain. DefDatabase lookups by name are not free and RocketMan
        // is installed. Null is a legitimate, expected result - it means RWoM is not loaded.
        private static bool magicDefsResolved;
        private static NeedDef staminaDef;
        private static NeedDef manaDef;

        public HediffCompProperties_DrainNeeds Props
        {
            get { return (HediffCompProperties_DrainNeeds)props; }
        }

        private static void ResolveMagicDefs()
        {
            if (magicDefsResolved)
            {
                return;
            }
            magicDefsResolved = true;
            // GetNamedSilentFail: absence is the normal case, not an error to log.
            staminaDef = DefDatabase<NeedDef>.GetNamedSilentFail("TM_Stamina");
            manaDef = DefDatabase<NeedDef>.GetNamedSilentFail("TM_Mana");
            DovahkiinMod.VerboseLog("Drain Vitality resolved magic needs - stamina: "
                + (staminaDef != null) + ", mana: " + (manaDef != null));
        }

        /// <summary>
        /// Drop a need and report HOW MUCH WAS ACTUALLY TAKEN, which is not always the amount
        /// asked for - a bar already near empty yields less, and a pawn without that need at all
        /// yields nothing.
        ///
        /// Returning the real figure is what makes the transfer to the caster honest: the caster
        /// gains exactly what the victim lost, never a flat amount conjured out of nothing.
        ///
        /// Zero is a perfectly normal result. It means either the magic mod is absent, or - far
        /// more common - the victim is an ordinary pawn with no magic class and therefore has
        /// no stamina or mana bar at all. See COMPAT.md section 5.
        /// </summary>
        private static float TryDrain(Pawn pawn, NeedDef def, float amount)
        {
            if (def == null || amount <= 0f || pawn == null || pawn.needs == null)
            {
                return 0f;
            }
            Need need = pawn.needs.TryGetNeed(def);
            if (need == null)
            {
                return 0f;
            }
            // CurLevel is vanilla Need API. Nothing here touches a TorannMagic type, so this
            // compiles and runs identically with the magic mod absent.
            float before = need.CurLevel;
            need.CurLevel = Mathf.Max(0f, before - amount);
            return before - need.CurLevel;
        }

        /// <summary>
        /// Hand a drained need back to the caster, capped at their own maximum.
        ///
        /// Silently does nothing when the caster lacks that need - a Dovahkiin with no
        /// RimWorld-of-Magic class has no stamina bar to refill, which is expected, not a fault.
        /// </summary>
        private static void TryGive(Pawn pawn, NeedDef def, float amount)
        {
            if (def == null || amount <= 0f || pawn == null || pawn.needs == null)
            {
                return;
            }
            Need need = pawn.needs.TryGetNeed(def);
            if (need == null)
            {
                return;
            }
            need.CurLevel = Mathf.Min(need.MaxLevel, need.CurLevel + amount);
        }

        /// <summary>Whoever cast the shout. Null is normal - dead, gone, or a pre-feature save.</summary>
        private Pawn Caster
        {
            get
            {
                Hediff_VitalityDrained drain = parent as Hediff_VitalityDrained;
                if (drain == null || drain.drainedBy == null || drain.drainedBy.Dead)
                {
                    return null;
                }
                return drain.drainedBy;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }

            if (Props.glowFleck != null)
            {
                ticksToGlow--;
                if (ticksToGlow <= 0)
                {
                    ticksToGlow = Mathf.Max(10, Props.glowIntervalTicks);
                    FleckMaker.AttachedOverlay(pawn, Props.glowFleck, Vector3.zero,
                        Props.glowScale, -1f);
                }
            }

            ticksToNext--;
            if (ticksToNext > 0)
            {
                return;
            }
            ticksToNext = Mathf.Max(30, Props.intervalTicks);

            ResolveMagicDefs();
            float severity = Mathf.Max(1f, parent.Severity);

            // A drain TRANSFERS. Whatever is taken off the victim is handed to the caster, up to
            // their own maximum - it is not destroyed, and it is not conjured either. Playtest
            // found the caster's stamina unchanged after draining two victims, which was correct
            // for two separate reasons: the transfer did not exist yet, AND both victims were
            // classless, so they had no stamina bar and there was nothing to take.
            Pawn caster = Caster;

            // --- Level 1 and up: stamina, or its vanilla stand-in ---------------------------
            float gotStamina = TryDrain(pawn, staminaDef, Props.staminaDrainPerInterval * severity);

            // Rest always drains: it is the vanilla reading of "physical fatigue", and it is
            // what makes this shout do something visible on a pawn with no magic class at all.
            TryDrain(pawn, NeedDefOf.Rest, Props.restDrainPerInterval * severity);

            // --- Level 2 and up: magicka, or mental fatigue ---------------------------------
            float gotMana = 0f;
            if (severity >= Props.manaDrainMinSeverity)
            {
                gotMana = TryDrain(pawn, manaDef, Props.manaDrainPerInterval * severity);
                TryDrain(pawn, NeedDefOf.Joy, Props.joyDrainPerInterval * severity);
            }

            // --- Hand the stolen vitality to the caster -------------------------------------
            // Deliberately NOT Rest or Joy: those are the vanilla stand-ins for a victim with no
            // magic class, and refilling the caster's sleep meter by shouting at people would be
            // an exploit rather than a drain.
            if (caster != null)
            {
                TryGive(caster, staminaDef, gotStamina * Props.casterNeedGainFraction);
                TryGive(caster, manaDef, gotMana * Props.casterNeedGainFraction);
                if (gotStamina > 0f || gotMana > 0f)
                {
                    DovahkiinMod.VerboseLog(string.Format(
                        "Drain Vitality transferred stamina {0:F3}, mana {1:F3} to {2}",
                        gotStamina * Props.casterNeedGainFraction,
                        gotMana * Props.casterNeedGainFraction, caster.LabelShortCap));
                }
            }

            // --- Health drain, and the life it gives back -----------------------------------
            //
            // Asked for: behave like Marked for Death, but at half strength, WITHOUT the armour
            // penalty, and heal the caster by what it takes. Marked for Death deals 1.6 per
            // interval per severity, so this is 0.8 - literally half.
            if (severity >= Props.healthDrainMinSeverity
                && healthApplicationsUsed < Props.maxHealthApplications)
            {
                healthApplicationsUsed++;
                // NOT Deterioration. That is the ITEM decay type and does nothing whatsoever to
                // a pawn - it is the bug that made Marked for Death deal no damage at all in
                // Phase 2b-fix2. Blunt is the safe fallback: it is real damage and, unlike a
                // cutting def, it barely bleeds, so spreading it cannot kill by blood loss.
                // The XML supplies Dovahkiin_SoulWither explicitly; this is only a backstop.
                DamageDef def = Props.healthDamageDef ?? DamageDefOf.Blunt;
                float amount = Props.healthDrainPerInterval * severity;
                // Spread over the body by the shared priority rule, so this wears a victim down
                // rather than destroying one organ. Capped, like Marked for Death, because
                // uncapped spreading damage kills by cumulative blood loss.
                BodyPartRecord part = DovahkiinDamageUtility.SelectSpreadTarget(pawn);
                pawn.TakeDamage(new DamageInfo(def, amount, 0f, -1f, null, part));

                HealCaster(amount * Props.casterHealFraction);
            }

        }

        /// <summary>
        /// Give the drained life back to whoever cast the shout - the point of a drain.
        ///
        /// Heals real injuries only, and by reducing their severity rather than removing them,
        /// so it behaves like natural healing: it cannot restore a destroyed body part, cannot
        /// cure disease, and cannot raise the caster above uninjured. Oldest-tended-first is
        /// avoided deliberately; the WORST injury is healed first, which is what makes the
        /// shout feel like a lifeline in a fight.
        /// </summary>
        private void HealCaster(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }
            Hediff_VitalityDrained drain = parent as Hediff_VitalityDrained;
            Pawn caster = drain == null ? null : drain.drainedBy;
            // Null is entirely normal: the caster may be dead, gone, or the hediff may predate
            // this feature in an old save. No caster simply means no healing.
            if (caster == null || caster.Dead || caster.health == null)
            {
                return;
            }

            Hediff_Injury worst = null;
            Hediff bloodLoss = null;
            List<Hediff> hediffs = caster.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h.def == HediffDefOf.BloodLoss)
                {
                    bloodLoss = h;
                    continue;
                }
                Hediff_Injury injury = h as Hediff_Injury;
                if (injury == null || injury.Severity <= 0f)
                {
                    continue;
                }
                if (worst == null || injury.Severity > worst.Severity)
                {
                    worst = injury;
                }
            }

            if (worst != null)
            {
                worst.Heal(amount);
            }

            // Blood already lost is a separate hediff and closing a wound does not restore it.
            // A caster who is bleeding should feel the drain putting life back, not just sealing
            // the hole - that gap was the playtest complaint.
            if (bloodLoss != null && Props.casterBloodLossFraction > 0f)
            {
                bloodLoss.Severity = Mathf.Max(0f,
                    bloodLoss.Severity - amount * Props.casterBloodLossFraction * 0.02f);
            }

            if (worst != null || bloodLoss != null)
            {
                DovahkiinMod.VerboseLog("Drain Vitality healed " + caster.LabelShortCap
                    + " for " + amount.ToString("F1"));
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToNext, "ticksToNext", 0);
            Scribe_Values.Look(ref healthApplicationsUsed, "healthApplicationsUsed", 0);
        }
    }
}
