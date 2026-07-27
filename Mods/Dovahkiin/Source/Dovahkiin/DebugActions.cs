// Implements: ROADMAP.md Phase 1 - dev-mode tools.
//
// These exist so that every later phase can be tested without waiting for rare random events.
// Build them early and keep them working; they are not throwaway.
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Dovahkiin
{
    public static class DovahkiinDebugActions
    {
        private const string Category = "Dovahkiin";

        [DebugAction(Category, "Force awaken pawn", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceAwaken()
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            foreach (Pawn p in Find.CurrentMap.mapPawns.FreeColonistsAndPrisoners)
            {
                Pawn target = p;
                options.Add(new DebugMenuOption(target.LabelShortCap, DebugMenuOptionMode.Action, delegate
                {
                    GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
                    if (reg == null)
                    {
                        return;
                    }
                    if (reg.TryAwaken(target, AwakeningCause.Debug))
                    {
                        Messages.Message(target.LabelShortCap + " is the Dovahkiin.",
                            target, MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        // Say WHY, or this tool is useless for diagnosing the invariant.
                        Messages.Message("Could not awaken " + target.LabelShortCap + ": "
                            + AwakenBlockedReason(reg, target),
                            MessageTypeDefOf.RejectInput, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private static string AwakenBlockedReason(GameComponent_DragonbornRegistry reg, Pawn p)
        {
            if (reg.CurrentDovahkiin != null)
            {
                return "a Dovahkiin already exists (" + reg.CurrentDovahkiin.LabelShortCap + ")";
            }
            if (!reg.SlotOpen)
            {
                return "the slot is still closed after a death (OD-1 grieving delay)";
            }
            if (reg.IsLockedOut(p))
            {
                return "that pawn is permanently locked out";
            }
            if (!DovahkiinUtility.EligibleToAwaken(p))
            {
                return "that pawn is not eligible";
            }
            return "unknown";
        }

        [DebugAction(Category, "Grant 1 soul", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrantOneSoul()
        {
            GrantSouls(1);
        }

        [DebugAction(Category, "Grant 10 souls", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrantTenSouls()
        {
            GrantSouls(10);
        }

        private static void GrantSouls(int count)
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            Pawn d = reg == null ? null : reg.CurrentDovahkiin;
            if (d == null)
            {
                Messages.Message("No Dovahkiin exists.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            // Idempotent, and normally a no-op. Present so this tool cannot be blocked by a
            // pawn awakened under an older build with missing hediffs.
            DovahkiinUtility.RepairIdentity(d);

            Hediff_TheVoice voice = d.health.hediffSet
                .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_TheVoice) as Hediff_TheVoice;
            Hediff_DragonSoulAttunement attunement = d.health.hediffSet
                .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_DragonSoulAttunement)
                as Hediff_DragonSoulAttunement;
            if (voice == null || attunement == null)
            {
                // Say which one, and where to look. The generic version of this message cost a
                // whole playtest round in Phase 1.
                Messages.Message("Missing hediff: "
                    + (voice == null ? "the Voice " : "")
                    + (attunement == null ? "attunement " : "")
                    + "- check the log for an XML error naming Hediffs_Dovahkiin.xml.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            // SPEC.md 5.1: absorption grants two SEPARATE things. Attunement is permanent and
            // never spent; the soul token is spendable. Keep them separate here too.
            voice.GrantSouls(count, false);
            attunement.AbsorbSouls(count);

            Messages.Message(d.LabelShortCap + ": +" + count + " soul(s). Unspent "
                + voice.UnspentSouls + ", attunement " + attunement.Souls + ".",
                d, MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction(Category, "Grant Dragonblood to pawn", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrantDragonblood()
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            foreach (Pawn p in Find.CurrentMap.mapPawns.FreeColonistsAndPrisoners)
            {
                Pawn target = p;
                options.Add(new DebugMenuOption(target.LabelShortCap, DebugMenuOptionMode.Action, delegate
                {
                    DovahkiinUtility.GrantDragonblood(target);
                    Messages.Message(target.LabelShortCap + " is dragonblooded.",
                        target, MessageTypeDefOf.PositiveEvent, false);
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction(Category, "Registry status", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RegistryStatus()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null)
            {
                Log.Error("[Dovahkiin] Registry component is missing from the game. That is a bug.");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Dovahkiin registry ===");
            Pawn d = reg.CurrentDovahkiin;
            sb.AppendLine("Dovahkiin:            " + (d == null ? "<none>" : d.LabelShortCap));
            sb.AppendLine("Ever existed:         " + reg.DovahkiinEverExisted);
            sb.AppendLine("Deaths:               " + reg.DovahkiinDeaths);
            sb.AppendLine("Slot open:            " + reg.SlotOpen);
            sb.AppendLine("Awakening event fired:" + reg.DragonEventFiredCount
                + "  (may fire again: " + reg.CanFireAwakeningEvent + ")");
            sb.AppendLine("Alduin state:         " + reg.AlduinState);

            int dragonblood = 0;
            int lockedOut = 0;
            foreach (Pawn p in DovahkiinUtility.AllDragonbloodPawns())
            {
                dragonblood++;
                if (reg.IsLockedOut(p))
                {
                    lockedOut++;
                }
            }
            sb.AppendLine("Dragonblood pawns:    " + dragonblood + " (" + lockedOut + " locked out)");
            Log.Message(sb.ToString());
            Messages.Message("Registry status written to the log.", MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Kill Dovahkiin (test OD-1)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void KillDovahkiin()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            Pawn d = reg == null ? null : reg.CurrentDovahkiin;
            if (d == null)
            {
                Messages.Message("No Dovahkiin exists.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            d.Kill(null);
            Messages.Message("Killed. The slot reopens after the grieving delay.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction(Category, "Clear registry", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ClearRegistry()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null)
            {
                return;
            }
            reg.DebugClear();
            Messages.Message("Dovahkiin registry cleared.", MessageTypeDefOf.TaskCompletion, false);
        }

        // --- Phase 2: the Voice ---

        // Label was "Learn all words (slice)" when only the Phase 2a slice existed. It has always
        // walked every WordOfPowerDef in the database, so it covers all eight shouts now.
        [DebugAction(Category, "Learn all words", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnAllWords()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg == null)
            {
                return;
            }
            int n = 0;
            foreach (WordOfPowerDef w in DefDatabase<WordOfPowerDef>.AllDefsListForReading)
            {
                if (reg.TryDiscoverWord(w.defName))
                {
                    n++;
                }
            }
            Messages.Message("Discovered " + n + " new word(s). Word walls will do this properly in Phase 5.",
                MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Raise a shout one level", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RaiseShoutLevel()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            Pawn d = reg == null ? null : reg.CurrentDovahkiin;
            if (d == null)
            {
                Messages.Message("No Dovahkiin exists.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Hediff_TheVoice voice = ShoutUtility.GetVoice(d);
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            foreach (ShoutDef s in DefDatabase<ShoutDef>.AllDefsListForReading)
            {
                ShoutDef shout = s;
                int lv = voice == null ? 0 : voice.GetShoutLevel(shout.defName);
                int attainable = shout.MaxAttainableLevel(reg);
                string label = shout.label + "  (level " + lv + "/" + attainable + " words found)";
                options.Add(new DebugMenuOption(label, DebugMenuOptionMode.Action, delegate
                {
                    string reason;
                    if (ShoutUtility.TryRaiseLevel(d, shout, out reason))
                    {
                        Messages.Message(shout.label + " raised to level "
                            + ShoutUtility.GetVoice(d).GetShoutLevel(shout.defName) + ".",
                            d, MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("Cannot raise " + shout.label + ": " + reason,
                            MessageTypeDefOf.RejectInput, false);
                    }
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction(Category, "Refill Thu'um / clear cooldown", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RefillThuum()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            Pawn d = reg == null ? null : reg.CurrentDovahkiin;
            if (d == null)
            {
                Messages.Message("No Dovahkiin exists.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Need_Thuum need = ShoutUtility.GetThuum(d);
            if (need != null)
            {
                need.CurLevel = need.MaxLevel;
            }
            Hediff_TheVoice voice = ShoutUtility.GetVoice(d);
            if (voice != null)
            {
                voice.ClearThuumCooldown();
            }
            if (DovahkiinDefOf.Dovahkiin_VoiceStrain != null)
            {
                Hediff strain = d.health.hediffSet.GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_VoiceStrain);
                if (strain != null)
                {
                    d.health.RemoveHediff(strain);
                }
            }
            Messages.Message("Thu'um refilled, recovery and strain cleared.",
                d, MessageTypeDefOf.TaskCompletion, false);
        }

        /// <summary>
        /// Safety net. A PawnFlyer carries a despawned pawn inside it; if the flyer is ever
        /// stranded, that pawn is invisible and unreachable - it looks like it stopped existing.
        /// This lands every flyer on the map immediately.
        ///
        /// Added after a Phase 2b bug destroyed a colonist this way. The cause is fixed, but a
        /// recovery tool costs nothing and a lost pawn is unrecoverable without one.
        /// </summary>
        [DebugAction(Category, "Recover pawns stuck in flight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RecoverStuckFlyers()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            List<PawnFlyer> flyers = new List<PawnFlyer>();
            foreach (Thing t in map.listerThings.AllThings)
            {
                PawnFlyer f = t as PawnFlyer;
                if (f != null)
                {
                    flyers.Add(f);
                }
            }
            if (flyers.Count == 0)
            {
                Messages.Message("No pawns are in flight - nothing to recover.",
                    MessageTypeDefOf.TaskCompletion, false);
                return;
            }
            // RespawnPawn is protected, so it has to go through reflection. This is a rescue
            // tool for an abnormal state - it is not on any normal code path.
            MethodInfo respawn = AccessTools.Method(typeof(PawnFlyer), "RespawnPawn");
            int recovered = 0;
            foreach (PawnFlyer f in flyers)
            {
                Pawn carried = f.FlyingPawn;
                if (carried != null)
                {
                    Log.Warning("[Dovahkiin] Recovering " + carried.LabelShortCap
                        + " from a stranded PawnFlyer at " + f.Position + ".");
                }
                try
                {
                    if (respawn != null)
                    {
                        respawn.Invoke(f, null);
                        recovered++;
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error("[Dovahkiin] Could not recover a flyer: " + e.Message);
                    continue; // Leave it alone rather than destroying it with a pawn inside.
                }
                if (!f.Destroyed)
                {
                    f.Destroy(DestroyMode.Vanish);
                }
            }
            Messages.Message("Recovered " + recovered + " pawn(s) from flight.",
                MessageTypeDefOf.TaskCompletion, false);
        }

        // --- Stubs. The creatures do not exist until Phases 3 and 4 (ROADMAP.md Phase 1). ---

        [DebugAction(Category, "Spawn dragon (Phase 3)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnDragon()
        {
            Messages.Message("Not yet - the fallback dragon is built in Phase 3.",
                MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Category, "Spawn Alduin (Phase 4)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnAlduin()
        {
            Messages.Message("Not yet - Alduin is built in Phase 4.",
                MessageTypeDefOf.RejectInput, false);
        }
    }
}
