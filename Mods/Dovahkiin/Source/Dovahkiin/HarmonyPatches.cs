// Implements: SPEC.md 2 (death handling / OD-1), 10 (Dragonblood heritability),
// 4.4a (Slow Time and Become Ethereal, the two shouts with no data-only route).
//
// Deliberately few and shallow. CLAUDE.md forbids per-tick work and RocketMan punishes it, so
// every patch here is on a rare, event-shaped method - a pawn dying, parentage being set, or
// an attack beginning. Nothing here ticks.
//
// The two combat-path patches are the only ones that run during a fight, and both open with
// GameComponent_DragonbornRegistry.IsDovahkiin, a reference compare that at most one pawn per
// save can pass. Every other pawn leaves before touching a hediff list. Do not add a patch here
// that skips that guard.
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// SPEC.md 2 / OD-1. When the Dovahkiin dies the registry clears and starts the grieving
    /// delay. Patching Pawn.Kill covers every death path in one place.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Patch_Pawn_Kill
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if (__instance == null)
            {
                return;
            }
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null)
            {
                return;
            }
            // Cheap reference compare first - this runs on every pawn death in the game.
            if (reg.IsDovahkiin(__instance))
            {
                reg.NotifyDovahkiinDied(__instance);
            }
        }
    }

    /// <summary>
    /// SPEC.md 10. Children of a Dovahkiin - or of a dragonblood pawn - are born dragonblooded.
    ///
    /// Hooking parentage rather than birth is deliberate: ParentRelationUtility.SetMother /
    /// SetFather is a two-argument, stable method that fires for births AND for generated
    /// families, and it works identically with Biotech absent. The Biotech birth method
    /// (PregnancyUtility.ApplyBirthOutcome) takes ten parameters and only exists in play when
    /// that DLC is active, which would make this feature silently DLC-locked.
    ///
    /// Positional __0 / __1 are used rather than parameter names so a Ludeon rename cannot
    /// silently break the patch.
    /// </summary>
    [HarmonyPatch(typeof(ParentRelationUtility), "SetMother")]
    public static class Patch_SetMother
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __0, Pawn __1)
        {
            DragonbloodInheritance.TryInherit(__0, __1);
        }
    }

    [HarmonyPatch(typeof(ParentRelationUtility), "SetFather")]
    public static class Patch_SetFather
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __0, Pawn __1)
        {
            DragonbloodInheritance.TryInherit(__0, __1);
        }
    }

    /// <summary>
    /// SPEC.md 4.4a - Become Ethereal. "Brief invulnerable + cannot attack, movement retained."
    ///
    /// RimWorld 1.4 has NO invulnerability type of any kind - verified by reflecting over
    /// Assembly-CSharp, zero types match "Invulnerab"; that machinery arrived with Anomaly in
    /// 1.5. So the invulnerable half is a statFactor of 0 on IncomingDamageFactor declared on
    /// the hediff, and this patch is the other half.
    ///
    /// Pawn.TryStartAttack is the single funnel through which a pawn begins an attack, melee or
    /// ranged alike, so one prefix covers both. It is an event, never per-tick work.
    ///
    /// GUARD: only the Dovahkiin can ever be ethereal - SPEC.md 4.5/4.6 give draugr and dragons
    /// nothing but Unrelenting Force, Fire Breath and Frost Breath - so the registry
    /// reference-compare early-out makes this free for every other pawn on the map. If a later
    /// phase ever grants Become Ethereal to anything else, that guard must come out.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "TryStartAttack")]
    public static class Patch_Pawn_TryStartAttack
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn __instance, ref bool __result)
        {
            if (!ShoutSelfBuffUtility.IsEthereal(__instance))
            {
                return true;
            }
            __result = false;
            return false; // Skip the original outright - the attack never begins.
        }
    }

    /// <summary>
    /// SPEC.md 4.4a - Become Ethereal, "cannot attack", THE PART THAT ACTUALLY WORKS.
    ///
    /// Patch_Pawn_TryStartAttack above is not enough on its own, and playtest proved it: the
    /// caster could still hit things. Pawn.TryStartAttack is the AI's entry point - a pawn
    /// choosing its own target. A PLAYER-ordered attack on a drafted pawn never goes through
    /// it; the job driver calls Verb.TryStartCastOn directly.
    ///
    /// Verb.TryStartCastOn is the real chokepoint that every attack passes through, melee and
    /// ranged, AI-driven and player-ordered alike. Both overloads are patched because the
    /// five-argument form is not guaranteed to delegate to the six-argument one.
    ///
    /// Shouts are deliberately still allowed - see EtherealAttackBlock.
    /// </summary>
    [HarmonyPatch(typeof(Verb), "TryStartCastOn", new Type[] {
        typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Patch_Verb_TryStartCastOn_Five
    {
        [HarmonyPrefix]
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!EtherealAttackBlock.ShouldBlock(__instance))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Verb), "TryStartCastOn", new Type[] {
        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool),
        typeof(bool), typeof(bool) })]
    public static class Patch_Verb_TryStartCastOn_Six
    {
        [HarmonyPrefix]
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!EtherealAttackBlock.ShouldBlock(__instance))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    internal static class EtherealAttackBlock
    {
        internal static bool ShouldBlock(Verb verb)
        {
            if (verb == null)
            {
                return false;
            }
            Pawn caster = verb.CasterPawn;
            // IsEthereal opens with the registry reference compare, so this is free for every
            // other pawn swinging a weapon anywhere on the map.
            if (caster == null || !ShoutSelfBuffUtility.IsEthereal(caster))
            {
                return false;
            }
            // Shouts still work while ethereal. Blocking them too would leave the Dovahkiin with
            // no way to act at all for the duration, including no way to end it, and the Voice
            // is not a weapon in the hand - it is the one thing that is still entirely theirs.
            // VerbProperties.violent is NOT used as the test: it defaults to true, so it would
            // also block Clear Skies and every other harmless shout.
            return !(verb is Verb_CastAbility);
        }
    }

    /// <summary>
    /// SPEC.md 4.4a - Become Ethereal, "nothing can harm the caster". Playtest asked for this to
    /// be absolute: magic, traps, explosions, anything.
    ///
    /// IncomingDamageFactor 0 on the hediff is a multiplier, and a multiplier only helps for
    /// damage that is routed through the stat. Pawn.PreApplyDamage is the chokepoint EVERY
    /// source passes through - vanilla, DLC, other mods, traps, fire, explosions - because it
    /// sits inside Thing.TakeDamage itself. Absorbing here is what vanilla shield belts do.
    ///
    /// The stat factor is kept as well, deliberately: it is what makes the Stats tab read
    /// "incoming damage 0%", which is the only in-game feedback the player gets for this.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
    public static class Patch_Pawn_PreApplyDamage
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn __instance, ref bool absorbed)
        {
            if (!ShoutSelfBuffUtility.IsEthereal(__instance))
            {
                return true;
            }
            absorbed = true;
            return false; // Nothing lands. Skip the original entirely.
        }
    }

    /// <summary>
    /// SPEC.md 4.4a - Slow Time. Self-only haste: the caster swings and shoots faster.
    ///
    /// Verb.TryCastNextBurstShot is the ONLY runtime caller of AdjustedCooldownTicks(Verb, Pawn)
    /// - established by scanning the IL of every method in Assembly-CSharp, not assumed - and
    /// Verb is the shared base of Verb_MeleeAttack and Verb_Shoot, so this single postfix covers
    /// melee and ranged together. Every other caller is a stat-display or debug worker, which
    /// means the Melee DPS readout reflects the buff for free.
    ///
    /// Why a patch at all, when SPEC.md's other buffs are pure XML: RangedCooldownFactor is a
    /// BIOTECH stat, and CLAUDE.md forbids requiring Biotech at runtime - putting it in a hediff
    /// would break the baseline environment silently. And there is no pawn-side melee cooldown
    /// stat in vanilla at all; MeleeWeapon_CooldownMultiplier belongs to the weapon, not the
    /// pawn, so a hediff cannot touch it. No data-only route exists for either half.
    /// </summary>
    [HarmonyPatch(typeof(VerbProperties), "AdjustedCooldownTicks")]
    public static class Patch_VerbProperties_AdjustedCooldownTicks
    {
        // Positional __1 (the attacker) rather than the parameter name, matching the patches
        // above: a Ludeon rename should break the build, not silently stop matching.
        [HarmonyPostfix]
        public static void Postfix(Pawn __1, ref int __result)
        {
            float factor = ShoutSelfBuffUtility.AttackCooldownFactor(__1);
            if (factor >= 1f)
            {
                return;
            }
            // Never round down to zero: a zero cooldown is an infinite attack rate.
            __result = Mathf.Max(1, Mathf.RoundToInt(__result * factor));
        }
    }

    /// <summary>
    /// Shared lookups for the two self-buff shouts. Both sit on paths that run during combat -
    /// every attack start, every cooldown calculation - so both begin with the registry
    /// reference compare and leave immediately for anyone who is not the Dovahkiin.
    /// </summary>
    internal static class ShoutSelfBuffUtility
    {
        internal static bool IsEthereal(Pawn p)
        {
            return SelfBuffOn(p, DovahkiinDefOf.Dovahkiin_Ethereal) != null;
        }

        /// <summary>
        /// Slow Time's attack-cooldown multiplier, below 1 meaning faster. Severity is the shout
        /// level, and the numbers live in DovahkiinTuningDef so they retune without a rebuild.
        /// </summary>
        internal static float AttackCooldownFactor(Pawn p)
        {
            Hediff h = SelfBuffOn(p, DovahkiinDefOf.Dovahkiin_SlowTime);
            if (h == null)
            {
                return 1f;
            }
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning == null)
            {
                return 1f;
            }
            List<float> byLevel = tuning.slowTimeCooldownFactorByLevel;
            if (byLevel == null || byLevel.Count == 0)
            {
                return 1f;
            }
            int index = Mathf.Clamp(Mathf.RoundToInt(h.Severity) - 1, 0, byLevel.Count - 1);
            return byLevel[index];
        }

        private static Hediff SelfBuffOn(Pawn p, HediffDef def)
        {
            if (p == null || def == null)
            {
                return null;
            }
            // Cheap reference compare first - see the class comment. At most one pawn per save
            // can pass this, so the hediff scan below almost never runs.
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null || !reg.IsDovahkiin(p))
            {
                return null;
            }
            if (p.health == null || p.health.hediffSet == null)
            {
                return null;
            }
            return p.health.hediffSet.GetFirstHediffOfDef(def);
        }
    }

    /// <summary>
    /// SPEC.md 4.4f: a puppet must be "visibly marked - a distinct overlay/tint AND an
    /// inspect-string line saying how long it has left - so the player never mistakes it for a
    /// real ally or plans around keeping it."
    ///
    /// The tint is an attached fleck emitted by the hediff. This is the other half. It runs only
    /// for the pawn currently selected, when the inspect pane is drawn, so it is not a hot path
    /// despite touching a common method.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "GetInspectString")]
    public static class Patch_Pawn_GetInspectString
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (__instance == null || __instance.health == null
                || DovahkiinDefOf.Dovahkiin_DeadPuppet == null)
            {
                return;
            }
            Hediff_DeadPuppet puppet = __instance.health.hediffSet
                .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_DeadPuppet) as Hediff_DeadPuppet;
            if (puppet == null)
            {
                return;
            }
            string line = "Dovahkiin_SoulTear_InspectLine".Translate(
                puppet.TicksRemaining.ToStringTicksToPeriod(true, false, true, true, false)
                    .Named("DURATION"));
            __result = string.IsNullOrEmpty(__result) ? line : __result + "\n" + line;
        }
    }

    internal static class DragonbloodInheritance
    {
        internal static void TryInherit(Pawn child, Pawn parent)
        {
            if (child == null || parent == null)
            {
                return;
            }
            // Guard: this fires during world generation for large numbers of pawns. The
            // trait check is the cheapest possible early-out and is false almost always.
            if (!DovahkiinUtility.ShouldInheritDragonblood(parent))
            {
                return;
            }
            if (DovahkiinUtility.IsDragonblood(child))
            {
                return; // Never stack - SPEC.md 10.
            }
            DovahkiinUtility.GrantDragonblood(child);
            DovahkiinMod.VerboseLog("Dragonblood inherited: " + child.LabelShortCap
                + " from " + parent.LabelShortCap);
        }
    }
}
