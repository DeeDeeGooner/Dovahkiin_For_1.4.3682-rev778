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

        /// <summary>
        /// Fire a breath by hand. THIS IS THE ONLY WAY TO SEE ONE UNTIL THE PATTERN EXECUTOR
        /// EXISTS, and that is by design: the breath has no verb and no tool, so RimWorld's own
        /// combat AI can never fire it on its own schedule. The user's rule, 2026-08-05 - a
        /// dragon's breath is an active skill belonging to an attack pattern, not a melee roll.
        /// </summary>
        [DebugAction(Category, "Dragon breath (pick shape, then aim)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DragonBreathDebug()
        {
            List<DebugMenuOption> shapes = new List<DebugMenuOption>();
            shapes.Add(new DebugMenuOption("FIRE - grounded cone", DebugMenuOptionMode.Action,
                delegate { AimBreath(DragonBreathShape.Cone, true); }));
            shapes.Add(new DebugMenuOption("FIRE - soar circle + reaching cone", DebugMenuOptionMode.Action,
                delegate { AimBreath(DragonBreathShape.Pool, true); }));
            shapes.Add(new DebugMenuOption("FROST - grounded cone", DebugMenuOptionMode.Action,
                delegate { AimBreath(DragonBreathShape.Cone, false); }));
            shapes.Add(new DebugMenuOption("FROST - soar circle + reaching cone", DebugMenuOptionMode.Action,
                delegate { AimBreath(DragonBreathShape.Pool, false); }));
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(shapes));
        }

        private static void AimBreath(DragonBreathShape shape, bool fire)
        {
            DebugTools.curTool = new DebugTool("Breath: click the DRAGON", delegate
            {
                Pawn dragon = null;
                List<Thing> here = UI.MouseCell().GetThingList(Find.CurrentMap);
                for (int i = 0; i < here.Count; i++)
                {
                    Pawn p = here[i] as Pawn;
                    if (p != null) { dragon = p; break; }
                }
                if (dragon == null)
                {
                    Messages.Message("No pawn in that cell.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                DebugTools.curTool = new DebugTool("Breath: click the TARGET CELL", delegate
                {
                    FireDebugBreath(dragon, UI.MouseCell(), shape, fire);
                    DebugTools.curTool = null;
                });
            });
        }

        private static void FireDebugBreath(Pawn dragon, IntVec3 target, DragonBreathShape shape, bool fire)
        {
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;

            // Routed through the COMP, not straight at Thing_DragonBreath.Spawn - the comp is what
            // puts him in the right movement state, turns him to face the target and clamps a
            // soaring breath's reach. Calling Spawn directly is what produced the user's
            // "he didn't soar nor looked toward the targeted cell". The pattern executor will
            // call this same method.
            Comp_AlduinFlight flight = dragon.TryGetComp<Comp_AlduinFlight>();
            Thing_DragonBreath breath = flight != null
                ? flight.BreatheAt(target, shape)
                : Thing_DragonBreath.Spawn(dragon, target, shape,
                    t != null ? t.dragonBreathConeRange : 24f,
                    t != null ? t.dragonBreathConeAngle : 38f,
                    t != null ? t.dragonBreathPoolRadius : 3.5f,
                    t != null ? t.dragonBreathDurationTicks : 263,
                    t != null ? t.dragonBreathPulseIntervalTicks : 20);
            if (breath == null)
            {
                Messages.Message("Breath failed to spawn - check the dev log.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            float damage = t != null ? t.dragonBreathDamage : 90.576f;
            int instances = t != null ? t.dragonBreathDamageInstances : 4;
            float ap = t != null ? t.dragonBreathArmorPenetration : 0.25515f;

            if (fire)
            {
                breath.SetPayload(DamageDefOf.Flame, damage, instances, ap,
                    true, 0f, null, 1f);
                // The mod's OWN fleck, not a vanilla one - ours is authored without
                // renderInstanced so it accepts a per-instance tint. A batched vanilla fleck
                // would silently ignore the colour, a trap this project has hit before.
                breath.SetLook(DovahkiinDefOf.Dovahkiin_Fleck_FireWave, 1.6f,
                    new UnityEngine.Color(1f, 0.62f, 0.24f));
                // GetNamedSilentFail returns null with no message, so a missing SoundDef would
                // present as "the breath is silent" rather than as an error. Null is handled -
                // the breath simply plays nothing - so this cannot break the effect.
                breath.SetSound(DefDatabase<SoundDef>.GetNamedSilentFail("Dovahkiin_DragonBreathFire"));
                if (t != null)
                {
                    breath.SetFillGradient(t.dragonBreathFillColor, t.dragonBreathFillBright,
                        t.dragonBreathFillYellow, t.dragonBreathYellowStrength,
                        t.dragonBreathSoarCircleOpacity);
                }
            }
            else
            {
                // ⚠ Dovahkiin_DragonFrost, NOT vanilla Frostbite. Frostbite has no armorCategory,
                // so it ignores EVERY kind of armour - a dragon breathing it would go straight
                // through Dragon Aspect and through cataphract plate alike. Ours is the same
                // worker and hediff with a Heat category so protection means something.
                HediffDef chill = DefDatabase<HediffDef>.GetNamedSilentFail("Dovahkiin_Chilled");
                breath.SetPayload(
                    DefDatabase<DamageDef>.GetNamedSilentFail("Dovahkiin_DragonFrost") ?? DamageDefOf.Frostbite,
                    damage, instances, ap,
                    false,
                    t != null ? t.dragonBreathFrostSnowDepth : 0.22f,
                    chill,
                    t != null ? t.dragonBreathFrostChillSeverity : 1f);
                breath.SetLook(DovahkiinDefOf.Dovahkiin_Fleck_FrostWave, 1.6f,
                    new UnityEngine.Color(0.62f, 0.86f, 1f));
                breath.SetSound(DefDatabase<SoundDef>.GetNamedSilentFail("Dovahkiin_DragonBreathFrost"));
                if (t != null)
                {
                    // ⚠ FROST PICKS ITS PALETTE BY SHAPE, AND THE TWO ARE INVERTED - grounded runs
                    // blue at the mouth to white at the tip, soar runs a white reaching cone over
                    // an unchanged blue circle. See the note in DovahkiinTuningDef. Fire shares
                    // one palette across both shapes; frost cannot.
                    bool groundedFrost = shape == DragonBreathShape.Cone;
                    breath.SetFillGradient(
                        groundedFrost ? t.dragonBreathFrostConeFillColor : t.dragonBreathFrostSoarFillColor,
                        groundedFrost ? t.dragonBreathFrostConeFillBright : t.dragonBreathFrostSoarFillBright,
                        groundedFrost ? t.dragonBreathFrostConeFillTint : t.dragonBreathFrostSoarFillTint,
                        groundedFrost ? t.dragonBreathFrostConeTintStrength : t.dragonBreathFrostSoarTintStrength,
                        t.dragonBreathSoarCircleOpacity);
                }
            }
        }

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

        /// <summary>
        /// Summon Call of Valor's hero on a cell you pick.
        ///
        /// A DEBUG ACTION RATHER THAN A SHOUT, deliberately and temporarily. Call of Valor is one
        /// of the three QUEST-LOCKED shouts, and neither the quest nor the ability plumbing
        /// exists yet - so without this there would be no way to see the summon at all, and the
        /// riskiest part of the feature (a temporary pawn, RISKS.md section 9) would go untested
        /// while the cheap part around it got built on top of it. That is the exact ordering
        /// mistake this project has already paid for once.
        ///
        /// It targets a CELL because he arrives through a portal on the spot you aim at, unlike
        /// the Ancient Dragonborn who lands at the caster's shoulder.
        /// </summary>
        [DebugAction(Category, "Summon Call of Valor (pick a cell)",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SummonCallOfValor()
        {
            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            Pawn caster = reg == null ? null : reg.CurrentDovahkiin;
            if (caster == null || !caster.Spawned)
            {
                Messages.Message("No spawned Dovahkiin to call him for.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("Click a cell - the portal opens there.",
                MessageTypeDefOf.NeutralEvent, false);

            // THERE IS NO TargetingParameters.ForCell(). The first version of this called one
            // and the build caught it - the class has ForSelf, ForArrest, ForAttackHostile,
            // ForRescue and twenty more, and not one of them is a plain cell. A cell target is
            // built by hand, by turning on canTargetLocations. Read off RimWorld.TargetingParameters
            // rather than guessed a second time - and note the namespace is RimWorld, not Verse,
            // which the first guess also got wrong.
            TargetingParameters cellOnly = new TargetingParameters();
            cellOnly.canTargetLocations = true;
            cellOnly.canTargetPawns = false;
            cellOnly.canTargetBuildings = false;
            cellOnly.canTargetItems = false;

            // Typed explicitly rather than passed inline. Targeter.BeginTargeting has FOUR
            // overloads, two of them with five parameters differing only in whether the second
            // is an Action<LocalTargetInfo> or an ITargetingSource - so a bare lambda plus three
            // nulls is ambiguous. Naming the delegate's type picks the overload unambiguously.
            System.Action<LocalTargetInfo> onCellPicked = delegate(LocalTargetInfo target)
            {
                CallOfValorUtility.TrySummon(caster, target.Cell);
            };
            Find.Targeter.BeginTargeting(cellOnly, onCellPicked, caster, null, null);
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
            Messages.Message("Not yet - the BOSS Alduin is built in Phase 4. "
                + "For the art, use 'Spawn Alduin (TEST creature)'.",
                MessageTypeDefOf.RejectInput, false);
        }

        // --- The TEST dragon. Art and rendering only; NOT the boss. ---
        //
        // Deliberately separate from the stub above. CLAUDE.md invariant 2 gives the registry
        // sole ownership of the one boss Alduin per save; this is a third def the registry
        // never sees, so it can never be the boss, never drops a soul, and never sets
        // SlainForever. It exists to judge twelve sprites and the three-state swap in the
        // running game before any gameplay is built on them.

        [DebugAction(Category, "Spawn Alduin (TEST creature)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnAlduinTest()
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Dovahkiin_Alduin_Test");
            if (kind == null)
            {
                // GetNamedSilentFail returns null with NO message, so a def that failed to load
                // would otherwise look like a debug action that simply does nothing.
                Messages.Message("Dovahkiin_Alduin_Test PawnKindDef did not load - check the dev log for XML errors.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            // SPEC.md 6.5c: there are only three ways to meet a dovah, and two of them are
            // spawnable here. "A dovah doesn't just idle - it's a shard of a deity."
            List<DebugMenuOption> how = new List<DebugMenuOption>();
            how.Add(new DebugMenuOption("INVADING - arrives in manhunter", DebugMenuOptionMode.Action,
                delegate { SpawnAlduinAs(kind, true); }));
            how.Add(new DebugMenuOption("GUARDING - grounded and motionless until approached", DebugMenuOptionMode.Action,
                delegate { SpawnAlduinAs(kind, false); }));
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(how));
        }

        private static void SpawnAlduinAs(PawnKindDef kind, bool invading)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 12,
                    delegate(IntVec3 c) { return c.Standable(map) && !c.Fogged(map); }, out cell))
            {
                cell = map.Center;
            }
            Pawn alduin = PawnGenerator.GeneratePawn(kind, null);
            GenSpawn.Spawn(alduin, cell, map, WipeMode.Vanish);

            Comp_AlduinFlight comp = alduin.TryGetComp<Comp_AlduinFlight>();
            if (comp != null)
            {
                comp.isGuardian = !invading;
            }

            // ⚠ NO MANHUNTER, EITHER WAY. Hostility now comes from the dov FACTION, and a mental
            // state would outrank the Lord duty the attack patterns run on - which is exactly what
            // made three playtests report "he acts like a wild beast". EnsureUnderLord also ends
            // any mental state it finds, so setting one here would simply be undone next tick.
            DovahFactionUtility.EnsureUnderLord(alduin);

            if (invading)
            {
                Messages.Message("Alduin (test) invades. He should cross in FLIGHT, land on his target, "
                    + "then peel off and CIRCLE between attacks.",
                    alduin, MessageTypeDefOf.ThreatBig, false);
            }
            else
            {
                Messages.Message("Alduin (test) guards this spot. He should sit GROUNDED and not move.",
                    alduin, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        [DebugAction(Category, "Cycle Alduin sprite state", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CycleAlduinState()
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            foreach (Pawn p in Find.CurrentMap.mapPawns.AllPawnsSpawned)
            {
                if (!AlduinGraphicsUtility.IsAlduin(p))
                {
                    continue;
                }
                Pawn target = p;
                foreach (AlduinMovementState state in new[] {
                    AlduinMovementState.Grounded, AlduinMovementState.Soar, AlduinMovementState.Flight })
                {
                    AlduinMovementState wanted = state;
                    options.Add(new DebugMenuOption(
                        target.LabelShortCap + " -> " + wanted, DebugMenuOptionMode.Action,
                        delegate
                        {
                            bool changed = AlduinGraphicsUtility.SetState(target, wanted);
                            Messages.Message(
                                changed ? ("Alduin is now " + wanted + ".")
                                        : ("Alduin was already " + wanted + ", or the swap failed - see the log."),
                                target, MessageTypeDefOf.NeutralEvent, false);
                        }));
                }
            }
            if (options.Count == 0)
            {
                Messages.Message("No test Alduin on this map. Spawn one first.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }
    }
}
