// Triggers the dovah's grab. SPEC.md 6.6b; the sequence itself lives in Hediff_DovahGrabbed.
//
// The user, 2026-08-04: "a very very small chance of triggering his iconic bite down, shake
// left right then throw away move - might be a real headache but also a nice surprise for the
// player."
//
// ============================================================================================
// WHY THIS HOOK
// ============================================================================================
// Verb_MeleeAttackDamage.DamageInfosToApply runs ONLY on a connecting hit - ApplyMeleeDamageToTarget
// calls it after the hit roll succeeds - so a postfix here fires exactly when a bite lands, and
// never on a miss. The mod already patches this method for Dragon Aspect's melee multiplier;
// this is a second, independent patch on it rather than an edit to that one, so a failure in
// either cannot take the other down.
//
// GATED ON THE TOOL, NOT THE LABEL. Tool.linkedBodyPartsGroup is a def reference, so it cannot
// drift the way a string comparison against "maw" would if the label were ever reworded.
// Only the MAW grabs - a wing bash or a tail sweep obviously cannot.
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Dovahkiin
{
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    public static class Patch_DovahGrab
    {
        private const string MawGroupDefName = "Dovahkiin_Maw";
        private const string GrabbedHediffDefName = "Dovahkiin_DovahGrabbed";

        // TAKE THE TARGET AS A PARAMETER, NOT FROM Verb.CurrentTarget.
        //
        // The first version read __instance.CurrentTarget and the grab NEVER FIRED, even with
        // the chance forced to 1.0. The signature is DamageInfosToApply(LocalTargetInfo target)
        // - the target is passed IN. Verb.currentTarget is set by the casting path and is not
        // reliably populated for a melee swing, so CurrentTarget.Thing came back null and the
        // early-out fired on every hit.
        //
        // Harmony binds this by NAME against the original's parameter, so it must stay "target".
        [HarmonyPostfix]
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            if (__instance == null)
            {
                return;
            }
            // Cheapest checks first: this runs on EVERY melee hit in the game, by every pawn.
            Tool tool = __instance.tool;
            if (tool == null || tool.linkedBodyPartsGroup == null
                || tool.linkedBodyPartsGroup.defName != MawGroupDefName)
            {
                return;
            }

            Pawn dragon = __instance.CasterPawn;
            if (dragon == null || !dragon.Spawned || dragon.Dead)
            {
                return;
            }

            Pawn victim = target.Thing as Pawn;
            if (victim == null || victim.Dead || !victim.Spawned || victim == dragon)
            {
                return;
            }

            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            float chance = tuning != null ? tuning.dovahGrabChance : 0.02f;
            if (chance <= 0f || !Rand.Chance(chance))
            {
                return;
            }

            // Never re-grab someone already held - the shake would restart forever and the
            // victim would never be released.
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(GrabbedHediffDefName);
            if (def == null)
            {
                // GetNamedSilentFail returns null with no message; without this line a missing
                // def would present as "the grab never triggers", which is indistinguishable
                // from bad luck on a 2% roll.
                Log.WarningOnce("[Dovahkiin] " + GrabbedHediffDefName + " missing - the dovah grab cannot fire.", 0x5A1D02);
                return;
            }
            if (victim.health.hediffSet.GetFirstHediffOfDef(def) != null)
            {
                return;
            }

            // A dragon can only hold one thing at a time. Without this, two bites in quick
            // succession would leave two pawns both being dragged to his mouth.
            if (AlreadyHoldingSomeone(dragon, def))
            {
                return;
            }

            Hediff_DovahGrabbed grabbed = HediffMaker.MakeHediff(def, victim) as Hediff_DovahGrabbed;
            if (grabbed == null)
            {
                return;
            }
            grabbed.Begin(dragon, tuning != null ? tuning.dovahGrabDurationTicks : 150);
            victim.health.AddHediff(grabbed);

            Messages.Message(dragon.LabelShortCap + " has seized " + victim.LabelShortCap + "!",
                victim, MessageTypeDefOf.ThreatBig, false);
        }

        private static bool AlreadyHoldingSomeone(Pawn dragon, HediffDef def)
        {
            Map map = dragon.Map;
            if (map == null)
            {
                return false;
            }
            List<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Hediff h = pawns[i].health == null ? null
                    : pawns[i].health.hediffSet.GetFirstHediffOfDef(def);
                Hediff_DovahGrabbed g = h as Hediff_DovahGrabbed;
                if (g != null && g.GrabbedBy == dragon)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

