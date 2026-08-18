// Implements: ROADMAP.md Phase 0 - Harmony entry point, HugsLib settings stub,
// and the load-time proof that the assembly and the custom def class both came up clean.
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// HugsLib entry point. HugsLib finds and instantiates this automatically; there is no
    /// [StaticConstructorOnStartup] and no manual bootstrapping.
    /// </summary>
    public class DovahkiinMod : ModBase
    {
        public override string ModIdentifier
        {
            get { return "Dovahkiin"; }
        }

        /// <summary>The mod's Harmony instance. Later phases patch through this, not their own.</summary>
        public static Harmony HarmonyInstance { get; private set; }

        /// <summary>Set once at startup so later phases never re-query for it.</summary>
        public static DovahkiinMod Instance { get; private set; }

        private SettingHandle<bool> verboseLogging;

        public DovahkiinMod()
        {
            Instance = this;
        }

        public override void Initialize()
        {
            HarmonyInstance = new Harmony("erzou.dovahkiin");
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

            // NOTHING EXTERNAL MOVES A DOVAH IN FLIGHT - the half that cannot be an attribute.
            //
            // Every other patch in this mod is found by PatchAll because it names a type this
            // assembly can see. This one patches ANOTHER MOD's classes, which may not be
            // installed and may not be named at compile time (CLAUDE.md: reflection + null-guard
            // only), so it has to be applied by hand.
            //
            // The count is logged deliberately. A reflection sweep that finds nothing looks
            // exactly like a sweep that worked, and this is the only place that difference can be
            // seen. 0 with RimWorld of Magic loaded means this protection is NOT in force.
            int carriersBlocked = DovahCargoRefusal.Apply(HarmonyInstance);
            Log.Message("[Dovahkiin] Airborne-cargo refusal applied to " + carriersBlocked
                + " third-party launcher(s); 0 is expected only with no such mod installed.");
        }

        public override void DefsLoaded()
        {
            verboseLogging = Settings.GetHandle<bool>(
                "verboseLogging",
                "Dovahkiin_Setting_VerboseLogging".Translate(),
                "Dovahkiin_Setting_VerboseLogging_Desc".Translate(),
                false);

            // Phase 0 proof: the custom Def class loaded and bound to its XML.
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning == null)
            {
                // ⚠⚠ AN ERROR, NOT A WARNING - RAISED 2026-08-13 AFTER THIS COST A PLAYTEST.
                //
                // A missing tuning def does not disable a feature; it silently swaps EVERY balance
                // number in the mod for its C# fallback. The user reported "his flightspeed is very
                // wrong" and "the cone's size was reduced" as two separate defects, and both were
                // this one condition - the file had failed to PARSE, so nothing in it was in force.
                //
                // It DID log, as a warning, and it was missed for a full round because the standing
                // log check greps for `Exception` and `Config error`. A yellow line among hundreds
                // is not a signal when the consequence is "every number in the mod is different".
                Logger.Error(
                    "TUNING DEF 'Dovahkiin_Tuning' NOT FOUND - every balance number in the mod is "
                    + "now running on its C# fallback instead of DovahkiinTuningDef.xml. This is "
                    + "almost always a def file that failed to PARSE. Check "
                    + "Defs/MiscDefs/DovahkiinTuningDef.xml - and note that a '--' ANYWHERE inside "
                    + "an XML comment makes RimWorld discard the WHOLE FILE.");
            }
            else
            {
                Logger.Message(
                    "Loaded. Tuning def OK (heir awaken chance {0:P1}, Thu'um per soul {1}).",
                    tuning.heirAwakenChance,
                    tuning.thuumPerSoul);
            }

            ValidateCriticalDefs();
        }

        /// <summary>
        /// Fails loudly when a def this mod cannot function without is missing.
        ///
        /// This exists because of a real Phase 1 bug: one invented XML field name
        /// (scenarioCanAddHediff, which is actually scenarioCanAdd) made RimWorld discard BOTH
        /// hediff defs. DefOf fields then silently resolved to null and the only symptom was a
        /// confusing runtime message. A missing def must be obvious at startup, not at use.
        /// </summary>
        private void ValidateCriticalDefs()
        {
            List<string> missing = new List<string>();
            if (DovahkiinDefOf.Dovahkiin_Trait_Dovahkiin == null) missing.Add("Dovahkiin_Trait_Dovahkiin");
            if (DovahkiinDefOf.Dovahkiin_Trait_Dragonblood == null) missing.Add("Dovahkiin_Trait_Dragonblood");
            if (DovahkiinDefOf.Dovahkiin_DragonSoulAttunement == null) missing.Add("Dovahkiin_DragonSoulAttunement");
            if (DovahkiinDefOf.Dovahkiin_TheVoice == null) missing.Add("Dovahkiin_TheVoice");
            if (DovahkiinDefOf.Dovahkiin_ShoutFlyer == null) missing.Add("Dovahkiin_ShoutFlyer");
            if (DovahkiinDefOf.Dovahkiin_ShoutWave == null) missing.Add("Dovahkiin_ShoutWave");
            if (DovahkiinDefOf.Dovahkiin_Fleck_ForceWave == null) missing.Add("Dovahkiin_Fleck_ForceWave");
            if (DovahkiinDefOf.Dovahkiin_Fleck_FireWave == null) missing.Add("Dovahkiin_Fleck_FireWave");
            // Both are read from Harmony patches on the combat path, where a null def would mean
            // the shout silently does nothing rather than erroring. SPEC.md 4.4a.
            if (DovahkiinDefOf.Dovahkiin_SlowTime == null) missing.Add("Dovahkiin_SlowTime");
            if (DovahkiinDefOf.Dovahkiin_Ethereal == null) missing.Add("Dovahkiin_Ethereal");
            if (DovahkiinDefOf.Dovahkiin_VitalityDrained == null) missing.Add("Dovahkiin_VitalityDrained");
            if (DovahkiinDefOf.Dovahkiin_Dismayed == null) missing.Add("Dovahkiin_Dismayed");
            if (DovahkiinDefOf.Dovahkiin_TimeSlowed == null) missing.Add("Dovahkiin_TimeSlowed");
            if (DovahkiinDefOf.Dovahkiin_StormCall == null) missing.Add("Dovahkiin_StormCall");
            // Read by the registry's puppet safety sweep - a null would disable that guard
            // silently, which RISKS.md section 9 specifically warns against.
            if (DovahkiinDefOf.Dovahkiin_DeadPuppet == null) missing.Add("Dovahkiin_DeadPuppet");
            // Null-guarded at the call site, so a missing thought would silently mean "Soul Tear
            // has no mood effect" rather than an error. That is exactly the kind of quiet
            // absence this check exists to catch.
            if (DovahkiinDefOf.Dovahkiin_Thought_SoulTorn == null) missing.Add("Dovahkiin_Thought_SoulTorn");

            if (missing.Count > 0)
            {
                Logger.Error("CRITICAL: " + missing.Count + " required def(s) failed to load: "
                    + string.Join(", ", missing.ToArray())
                    + ". Look further up the log for an 'XML error' line naming the file - a single "
                    + "unrecognised field makes RimWorld discard the entire def.");
            }
            else
            {
                Logger.Message("All critical defs present. Phase 1.");
            }
        }

        /// <summary>Gated debug logging. Off by default; RocketMan is installed.</summary>
        public static void VerboseLog(string message)
        {
            if (Instance != null && Instance.verboseLogging != null && Instance.verboseLogging.Value)
            {
                Instance.Logger.Message(message);
            }
        }
    }
}
