// Implements: SPEC.md 2 (the registry), 3.2, 3.3, 3.4, OD-1.
//
// THE ONE RULE: there is at most one Dovahkiin in a save at any time, and this class is the
// only thing allowed to grant or remove the trait. If you find yourself writing a second place
// that sets Trait_Dovahkiin, stop and refactor - see CLAUDE.md invariant 1.
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Dovahkiin
{
    /// <summary>Why a pawn awakened. Drives which backstory they receive (SPEC.md 3.1).</summary>
    public enum AwakeningCause
    {
        Slaughter,      // The "A Dragon!!!" event, SPEC.md 3.2
        DragonbloodHeir, // SPEC.md 3.3
        Stranger,       // The stranger quest or the wanderer incident, SPEC.md 3.4
        Scenario,       // The Dragon Prophecy, SPEC.md 11
        Debug
    }

    public enum AlduinState
    {
        Unspawned,
        Alive,
        Dormant,
        SlainForever
    }

    public class GameComponent_DragonbornRegistry : GameComponent
    {
        // --- The Dovahkiin ---
        private Pawn dovahkiin;
        private bool dovahkiinEverExisted;
        private int dovahkiinDeaths;

        /// <summary>
        /// OD-1: when the slot reopens after a death. -1 means "not waiting".
        /// Absolute game tick, compared against Find.TickManager.TicksGame.
        /// </summary>
        private int slotReopensAtTick = -1;

        /// <summary>Pawns who rolled their one awakening chance and failed. SPEC.md 3.3.</summary>
        private List<int> lockedOutPawnIds = new List<int>();

        /// <summary>How many times the "A Dragon!!!" event has fired. A counter, not a bool - OD-1.</summary>
        private int dragonEventFiredCount;

        // --- Alduin (Phase 4; stored here now so saves made in Phase 1 stay loadable) ---
        private Pawn alduin;
        private AlduinState alduinState = AlduinState.Unspawned;
        private int alduinRevivalTick = -1;
        private bool alduinFirstAppearanceDone;

        // --- World progress ---
        private bool strangerQuestFired;
        private List<string> wordsDiscoveredWorld = new List<string>();
        private int treasureMapsSold;

        // Runtime-only lookup, rebuilt from lockedOutPawnIds on load. Never serialised.
        private HashSet<int> lockedOutCache;

        public GameComponent_DragonbornRegistry(Game game)
        {
        }

        public static GameComponent_DragonbornRegistry Get
        {
            get
            {
                Game game = Current.Game;
                return game == null ? null : game.GetComponent<GameComponent_DragonbornRegistry>();
            }
        }

        // ------------------------------------------------------------------
        // Public API - SPEC.md 2. The only way to change Dovahkiin status.
        // ------------------------------------------------------------------

        public Pawn CurrentDovahkiin
        {
            get
            {
                // A dead or destroyed pawn is not the Dovahkiin. Self-heal rather than
                // trusting that NotifyDovahkiinDied always fired.
                if (dovahkiin != null && dovahkiin.Dead)
                {
                    NotifyDovahkiinDied(dovahkiin);
                }
                return dovahkiin;
            }
        }

        public bool DovahkiinEverExisted
        {
            get { return dovahkiinEverExisted; }
        }

        public int DovahkiinDeaths
        {
            get { return dovahkiinDeaths; }
        }

        public int DragonEventFiredCount
        {
            get { return dragonEventFiredCount; }
        }

        public bool IsDovahkiin(Pawn p)
        {
            return p != null && CurrentDovahkiin == p;
        }

        /// <summary>
        /// OD-1: the slot is open when nobody holds it AND any grieving delay has elapsed.
        /// </summary>
        public bool SlotOpen
        {
            get
            {
                if (CurrentDovahkiin != null)
                {
                    return false;
                }
                if (slotReopensAtTick < 0)
                {
                    return true;
                }
                return Find.TickManager.TicksGame >= slotReopensAtTick;
            }
        }

        /// <summary>
        /// SPEC.md 3.2 / 8.1. The awakening event may fire once per Dovahkiin slot, not once
        /// per save - which is why dragonEventFiredCount is a counter.
        /// </summary>
        public bool CanFireAwakeningEvent
        {
            get { return SlotOpen && dragonEventFiredCount <= dovahkiinDeaths; }
        }

        public void Notify_AwakeningEventFired()
        {
            dragonEventFiredCount++;
        }

        public bool IsLockedOut(Pawn p)
        {
            if (p == null)
            {
                return false;
            }
            if (lockedOutCache == null)
            {
                lockedOutCache = new HashSet<int>(lockedOutPawnIds);
            }
            return lockedOutCache.Contains(p.thingIDNumber);
        }

        public void LockOut(Pawn p)
        {
            if (p == null || IsLockedOut(p))
            {
                return;
            }
            lockedOutPawnIds.Add(p.thingIDNumber);
            if (lockedOutCache != null)
            {
                lockedOutCache.Add(p.thingIDNumber);
            }
        }

        /// <summary>
        /// SPEC.md 3.2: firing the awakening event permanently locks out every living
        /// dragonblood pawn, except one who just won the slot.
        /// </summary>
        public void LockOutAllDragonblood(Pawn exception = null)
        {
            foreach (Pawn p in DovahkiinUtility.AllDragonbloodPawns())
            {
                if (p != exception)
                {
                    LockOut(p);
                }
            }
        }

        /// <summary>
        /// The single entry point for becoming the Dovahkiin.
        /// Returns false and changes nothing if the slot is unavailable or the pawn is ineligible.
        /// </summary>
        public bool TryAwaken(Pawn p, AwakeningCause cause)
        {
            if (!DovahkiinUtility.EligibleToAwaken(p))
            {
                return false;
            }
            if (!SlotOpen)
            {
                return false;
            }

            dovahkiin = p;
            dovahkiinEverExisted = true;
            slotReopensAtTick = -1;

            DovahkiinUtility.ApplyDovahkiinIdentity(p, cause);

            // A Dovahkiin who wanders off must still be resolvable on load - SPEC.md 2.
            if (!p.Spawned && !Find.WorldPawns.Contains(p))
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            }

            DovahkiinMod.VerboseLog("Awakened " + p.LabelShort + " (cause: " + cause + ")");
            return true;
        }

        /// <summary>
        /// OD-1: the world grieves, then the slot reopens after a delay and the awakening event
        /// becomes eligible once more. Heirs locked out by the previous awakening stay locked out.
        /// </summary>
        public void NotifyDovahkiinDied(Pawn p)
        {
            if (p == null || dovahkiin != p)
            {
                return;
            }

            dovahkiin = null;
            dovahkiinDeaths++;

            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            float delayDays = tuning != null ? tuning.slotReopenDelayDays : 8f;
            slotReopensAtTick = Find.TickManager.TicksGame + (int)(delayDays * GenDate.TicksPerDay);

            DovahkiinMod.VerboseLog("Dovahkiin died: " + p.LabelShort + ". Slot reopens in " + delayDays + " days.");
        }

        /// <summary>Dev tool only. Clears the holder without counting it as a death.</summary>
        public void DebugClear()
        {
            if (dovahkiin != null)
            {
                DovahkiinUtility.StripDovahkiinIdentity(dovahkiin);
            }
            dovahkiin = null;
            dovahkiinEverExisted = false;
            dovahkiinDeaths = 0;
            dragonEventFiredCount = 0;
            slotReopensAtTick = -1;
            lockedOutPawnIds.Clear();
            lockedOutCache = null;
            wordsDiscoveredWorld.Clear();
            strangerQuestFired = false;
            treasureMapsSold = 0;
        }

        // --- Word discovery, SPEC.md 4.1 / OD-8. World state, survives a second Dovahkiin. ---

        public bool IsWordDiscovered(string wordDefName)
        {
            return wordsDiscoveredWorld.Contains(wordDefName);
        }

        public bool TryDiscoverWord(string wordDefName)
        {
            if (wordDefName.NullOrEmpty() || wordsDiscoveredWorld.Contains(wordDefName))
            {
                return false;
            }
            wordsDiscoveredWorld.Add(wordDefName);
            return true;
        }

        // --- Alduin, Phase 4. Accessors only for now. ---

        public AlduinState AlduinState
        {
            get { return alduinState; }
        }

        public Pawn Alduin
        {
            get { return alduin; }
        }

        public bool StrangerQuestFired
        {
            get { return strangerQuestFired; }
            set { strangerQuestFired = value; }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Runs after a save is loaded and after a new game starts.
        ///
        /// CLAUDE.md invariant 6 says nothing is lost by loading a save. A Dovahkiin must
        /// therefore never exist without their trait, hediffs and title - whatever went wrong
        /// earlier. This repaired real saves written while the hediff defs were failing to
        /// load, and it will cover any future def hiccup the same way.
        /// </summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RepairDovahkiinIdentity();
            SweepDeadPuppets();
        }

        // --- SPEC.md 4.4f / RISKS.md section 9: the dead puppets -----------------------------

        private List<Pawn> deadPuppets = new List<Pawn>();

        /// <summary>Records a pawn raised by Soul Tear, so the load sweep can find it later.</summary>
        public void NotifyPuppetRaised(Pawn p)
        {
            if (p == null)
            {
                return;
            }
            if (deadPuppets == null)
            {
                deadPuppets = new List<Pawn>();
            }
            if (!deadPuppets.Contains(p))
            {
                deadPuppets.Add(p);
            }
        }

        /// <summary>Drops a puppet from tracking once it has died. Safe to call twice.</summary>
        public void NotifyPuppetGone(Pawn p)
        {
            if (p != null && deadPuppets != null)
            {
                deadPuppets.Remove(p);
            }
        }

        /// <summary>
        /// SPEC.md 4.4f's safety sweep. **This should never fire.**
        ///
        /// A puppet is meant to be impossible to strand: the hediff is non-removable and kills
        /// on expiry, so every exit path ends in death. But RISKS.md section 9 is explicit that
        /// the failure this guards against - a player-faction pawn that kept the puppet marker
        /// and lost its hediff - is an unremovable pseudo-colonist nobody can arrest, banish or
        /// kill cleanly. That is bad enough to warrant a check that costs nothing on load.
        ///
        /// If it ever does fire, something upstream is wrong and the log says so loudly.
        /// </summary>
        private void SweepDeadPuppets()
        {
            if (deadPuppets == null || deadPuppets.Count == 0)
            {
                return;
            }
            HediffDef puppetDef = DovahkiinDefOf.Dovahkiin_DeadPuppet;
            for (int i = deadPuppets.Count - 1; i >= 0; i--)
            {
                Pawn p = deadPuppets[i];
                if (p == null || p.Dead || p.Destroyed)
                {
                    deadPuppets.RemoveAt(i);
                    continue;
                }
                bool stillPuppet = puppetDef != null && p.health != null
                    && p.health.hediffSet.GetFirstHediffOfDef(puppetDef) != null;
                if (stillPuppet)
                {
                    continue; // Normal: alive, doomed, still counting down.
                }

                Log.Error("[Dovahkiin] SAFETY SWEEP: " + p.LabelShortCap + " is tracked as a "
                    + "Soul Tear puppet but no longer carries Dovahkiin_DeadPuppet. That should "
                    + "be impossible - the hediff is non-removable. Killing it rather than "
                    + "leaving an unremovable pseudo-colonist. See RISKS.md section 9.");
                if (p.Faction != null && p.Faction.IsPlayer)
                {
                    p.SetFaction(null, null);
                }
                p.Kill(null, null);
                deadPuppets.RemoveAt(i);
            }
        }

        private void RepairDovahkiinIdentity()
        {
            Pawn p = dovahkiin;
            if (p == null || p.Dead)
            {
                return;
            }
            List<string> repaired = DovahkiinUtility.RepairIdentity(p);
            if (repaired.Count > 0)
            {
                Log.Warning("[Dovahkiin] Repaired missing identity on " + p.LabelShortCap
                    + ": " + string.Join(", ", repaired.ToArray())
                    + ". This save was written by an older or broken build; it is now consistent.");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Every field defaults to a value that is correct for a save written before it
            // existed - CLAUDE.md invariant 6.
            Scribe_References.Look(ref dovahkiin, "dovahkiin", true);
            Scribe_Values.Look(ref dovahkiinEverExisted, "dovahkiinEverExisted", false);
            Scribe_Values.Look(ref dovahkiinDeaths, "dovahkiinDeaths", 0);
            Scribe_Values.Look(ref slotReopensAtTick, "slotReopensAtTick", -1);
            Scribe_Collections.Look(ref lockedOutPawnIds, "lockedOutPawnIds", LookMode.Value);
            Scribe_Values.Look(ref dragonEventFiredCount, "dragonEventFiredCount", 0);

            Scribe_References.Look(ref alduin, "alduin", true);
            Scribe_Values.Look(ref alduinState, "alduinState", AlduinState.Unspawned);
            Scribe_Values.Look(ref alduinRevivalTick, "alduinRevivalTick", -1);
            Scribe_Values.Look(ref alduinFirstAppearanceDone, "alduinFirstAppearanceDone", false);

            // LookMode.Reference: puppets are pawns that live elsewhere in the save and must not
            // be deep-copied into the registry. SPEC.md 4.4f / RISKS.md section 9.
            Scribe_Collections.Look(ref deadPuppets, "deadPuppets", LookMode.Reference);

            Scribe_Values.Look(ref strangerQuestFired, "strangerQuestFired", false);
            Scribe_Collections.Look(ref wordsDiscoveredWorld, "wordsDiscoveredWorld", LookMode.Value);
            Scribe_Values.Look(ref treasureMapsSold, "treasureMapsSold", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Collections come back null if the save predates the field.
                if (lockedOutPawnIds == null)
                {
                    lockedOutPawnIds = new List<int>();
                }
                if (wordsDiscoveredWorld == null)
                {
                    wordsDiscoveredWorld = new List<string>();
                }
                lockedOutCache = null;
            }
        }
    }
}
